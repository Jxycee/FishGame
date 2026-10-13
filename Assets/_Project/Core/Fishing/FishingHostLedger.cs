// Core/Fishing/FishingHostLedger.cs: the host's pure fishing ledger (spec 9.9, C 7.9): bait stock, rolls, ActiveCast, outcomes.
// Authority: Host only. Owns bait stock, rolls, ActiveCast and outcomes (spec 9.9 N6); the owner reports outcomes, the host validates and never replays the fight.
// Keys (M2-D20): rod, selected bait, bait stock and the bad-luck FailStreak by SLOT (survive rejoin); lastAcceptedCastId, ActiveCast
// and the last 16 stored resolutions by CLIENT ID (reset on disconnect, since castIds restart per connection).
// Bad-luck nudge (R65 / M2-D23): host-decided and deterministic, no RNG. See Resolve and RequestCast.
// M3: rolls through FishingRoller/FishingResolver with the host context (view.Context world part; rod tier, bait and depth overwritten
// here, M3-D15); the cast counter and catch limits live in the session's EcologyRunState (M3-D17); limited species are reserved at roll
// and committed or released on the first resolution (M3-D14).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class FishingHostLedger
    {
        private const int ResolutionHistory = 16;

        private sealed class SlotState
        {
            public ushort RodIndex;
            public byte BaitIndex;
            public readonly ushort[] Stock;      // per bait; unlimited baits are never counted
            public int FailStreak;               // consecutive failed casts (R65)
            public bool PityAnnounced;           // the current streak's Pity cast was already announced (cleared when the streak resets)
            public SlotState(ushort rod, byte bait, ushort[] stock) { RodIndex = rod; BaitIndex = bait; Stock = stock; }
        }

        private struct Resolution
        {
            public uint CastId;
            public FightOutcome Outcome;
            public EscapeReason Reason;
        }

        private sealed class ClientState
        {
            public byte Slot;
            public uint LastAccepted;
            public bool HasActive;
            public ActiveCast Active;
            public readonly List<Resolution> Resolved = new List<Resolution>(ResolutionHistory + 1);
        }

        private readonly EcologyDatabase _eco;
        private readonly FishingDatabase _db;          // == _eco.Fishing; every M2 use of _db is unchanged
        private readonly FishingResolver _resolver;
        private readonly Dictionary<byte, SlotState> _slots = new Dictionary<byte, SlotState>();
        private readonly Dictionary<ulong, ClientState> _clients = new Dictionary<ulong, ClientState>();
        private readonly List<ulong> _clientOrder = new List<ulong>();   // ascending clientId (kept sorted on add/remove): deterministic Tick order, no per-frame allocation
        private readonly List<CatchResult> _catchLog = new List<CatchResult>();
        private readonly ushort _defaultRod;
        private readonly byte _defaultBait;

        public FishingHostLedger(EcologyDatabase db, EcologyRunState run, ushort defaultRodIndex, byte defaultBaitIndex)
        {
            _eco = db ?? throw new ArgumentNullException(nameof(db));
            Run = run ?? throw new ArgumentNullException(nameof(run));
            _db = db.Fishing;
            if (defaultRodIndex >= _db.Rods.Count) throw new ArgumentOutOfRangeException(nameof(defaultRodIndex));
            if (defaultBaitIndex >= _db.Baits.Count) throw new ArgumentOutOfRangeException(nameof(defaultBaitIndex));
            _resolver = new FishingResolver(db);
            _defaultRod = defaultRodIndex;
            _defaultBait = defaultBaitIndex;
        }

        public EcologyRunState Run { get; }                            // session run state: seed, cast counter, catch ledger (M3-D17)
        public ulong RunSeed => Run.RunSeed;
        public uint HostCastCounter => Run.HostCastCounter;
        public double Now { get; private set; }                          // pause-aware ledger clock
        public IReadOnlyList<CatchResult> CatchLog => _catchLog;         // landed catches this session (dev overlay)

        // ---- slots and loadout -------------------------------------------------------------------------------------

        // Debug loadout once per slot: FishingConstants.DebugBaitStock of each bait, unlimited baits uncounted, default rod and bait.
        // Later calls are no-ops. RequestCast for a slot never ensured is rejected UnknownPlayer (B-Q10).
        public void EnsureSlot(byte slot)
        {
            if (_slots.ContainsKey(slot)) return;
            _slots[slot] = NewSlotState();
        }

        // Slot takeover by a different persistent identity: the slot's rod, selected bait and bait stock go back to the fresh-slot loadout
        // (exactly what EnsureSlot gives a new slot) and its pity FailStreak to 0. Touches no other slot and no client state (cast ids,
        // ActiveCast, stored resolutions), so other clients' casts are unaffected. A slot never ensured is left alone: the EnsureSlot that
        // later creates it already gives the fresh loadout. Returns true if a slot was reset.
        public bool ResetSlot(int slot)
        {
            if (slot < byte.MinValue || slot > byte.MaxValue || !_slots.ContainsKey((byte)slot)) return false;
            _slots[(byte)slot] = NewSlotState();
            return true;
        }

        private SlotState NewSlotState()
        {
            var stock = new ushort[_db.Baits.Count];
            for (int i = 0; i < stock.Length; i++)
                stock[i] = _db.Baits[i].Unlimited ? FishingConstants.UnlimitedBaitCount : FishingConstants.DebugBaitStock;
            return new SlotState(_defaultRod, _defaultBait, stock);
        }

        // BaitCount is the stock of the selected bait, or UnlimitedBaitCount for an unlimited bait. An unknown slot gives default.
        public PlayerLoadout GetLoadout(byte slot)
        {
            if (!_slots.TryGetValue(slot, out SlotState s)) return default;
            return new PlayerLoadout
            {
                RodIndex = s.RodIndex, BaitIndex = s.BaitIndex,
                BaitCount = _db.Baits[s.BaitIndex].Unlimited ? FishingConstants.UnlimitedBaitCount : s.Stock[s.BaitIndex],
            };
        }

        // False for an unknown slot, an index out of range, or while that client has an ActiveCast.
        public bool SelectBait(ulong clientId, byte slot, byte baitIndex)
        {
            if (!_slots.TryGetValue(slot, out SlotState s) || baitIndex >= _db.Baits.Count || HasActiveCast(clientId)) return false;
            s.BaitIndex = baitIndex;
            return true;
        }

        public bool SelectRod(ulong clientId, byte slot, ushort rodIndex)
        {
            if (!_slots.TryGetValue(slot, out SlotState s) || rodIndex >= _db.Rods.Count || HasActiveCast(clientId)) return false;
            s.RodIndex = rodIndex;
            return true;
        }

        // Host rod castMaxDist. False for an unknown slot, a charge that is NaN or outside [MinChargeNorm, 1], or a non-finite yaw.
        public bool TryComputeLanding(byte slot, in CastRequestData req, out float x, out float z)
        {
            x = 0f; z = 0f;
            if (!_slots.TryGetValue(slot, out SlotState s)) return false;
            float charge = req.ChargeNorm;
            if (!CatchValidator.IsChargeAccepted(charge)) return false;
            if (!CatchValidator.IsFinite(req.YawDeg)) return false;
            float distance = CastMath.Distance(charge, _db.Tuning.CastMinDist, _db.Rods[s.RodIndex].CastMaxDist);
            CastMath.Landing(req.OriginX, req.OriginZ, req.YawDeg, distance, out x, out z);
            return true;
        }

        // ---- cast, cancel, hook, result ----------------------------------------------------------------------------

        public CastVerdict RequestCast(ulong clientId, byte slot, in CastRequestData req, in HostCastView view, in RollOverrides overrides)
        {
            if (!_slots.TryGetValue(slot, out SlotState s))
                return new CastVerdict { Valid = false, Reason = CastRejectReason.UnknownPlayer };
            ClientState c = GetOrAddClient(clientId, slot);
            ref readonly FishingTuning t = ref _db.Tuning;

            bool baitAvailable = req.BaitIndex < _db.Baits.Count
                                 && (_db.Baits[req.BaitIndex].Unlimited || s.Stock[req.BaitIndex] > 0);
            CastRejectReason reason = CatchValidator.ValidateCast(in req, c.LastAccepted, c.HasActive, in view, baitAvailable, t.MinLandDepth);
            if (reason != CastRejectReason.None)
                return new CastVerdict { Valid = false, Reason = reason };

            // M3-D15: the world part is the host's (view.Context); rod tier, bait and depth are the ledger's own truth, never the owner's.
            // "Slot bait" (C 7.13) is the bait this cast validated and consumes: req.BaitIndex (checked against the slot stock above).
            FishingContext ctx = view.Context;
            ctx.RodTier = _db.Rods[s.RodIndex].Tier;
            ctx.Bait = _db.Baits[req.BaitIndex].Mods(1);
            ctx.Depth = view.Probe.Depth;

            // Roll first, counter assigned after (D-F2): a bad override, an invalid context or an out-of-range zone throws before any state changes.
            uint counter = Run.HostCastCounter + 1;
            Pcg32 rng = Rng.Stream(Run.RunSeed, FishingConstants.CatchStreamId, counter);
            FishingRoll roll = FishingRoller.Roll(_eco, _resolver, in ctx, Run.Catches, in overrides, req.BaitIndex, ref rng, out int ruleIndex);
            bool reserve = LimitApplies(ctx.ZoneIndex, ruleIndex, in roll);

            roll.Pity = t.PityAfterFailures > 0 && s.FailStreak >= t.PityAfterFailures;   // R65: ledger decision, no extra draw
            bool announcePity = roll.Pity && !s.PityAnnounced;   // once per streak: a cancelled Pity cast leaves the streak, so the recast is Pity again
            if (roll.Pity) s.PityAnnounced = true;

            float distance = CastMath.Distance(req.ChargeNorm, t.CastMinDist, _db.Rods[s.RodIndex].CastMaxDist);
            CastMath.Landing(req.OriginX, req.OriginZ, req.YawDeg, distance, out float lx, out float lz);

            // ---- state changes from here on (nothing below throws) ----
            if (!_db.Baits[req.BaitIndex].Unlimited) s.Stock[req.BaitIndex]--;
            Run.HostCastCounter = counter;
            if (reserve) Run.Catches.Reserve(roll.Catch.ItemIndex);   // the next roll of ANY client now sees Caught + Reserved >= limit
            c.Slot = slot;
            c.LastAccepted = req.CastId;
            c.HasActive = true;
            c.Active = new ActiveCast
            {
                CastId = req.CastId, Roll = roll, Phase = CastPhase.Waiting, TAccepted = Now, THooked = 0.0,
                LineLength0 = 0f, TFlight = CastMath.FlightTime(distance), CastDistance = distance, Assist = req.Assist,
                RodIndex = s.RodIndex, BaitIndex = req.BaitIndex, LandingX = lx, LandingZ = lz, LimitReserved = reserve,
            };
            return new CastVerdict { Valid = true, Reason = CastRejectReason.None, Roll = roll, LandingX = lx, LandingZ = lz, AnnouncePity = announcePity };
        }

        // A limit applies only to a fish rolled THROUGH a rule whose CatchLimitPerRun >= 0. Junk and forced rolls (ruleIndex -1: debug overrides
        // bypass gates and limits, M3-D13) never reserve. Never re-derived from the species: Commit/Release on an unreserved index throw.
        private bool LimitApplies(int zoneIndex, int ruleIndex, in FishingRoll roll)
        {
            if (ruleIndex < 0 || roll.Catch.IsJunk) return false;
            SpawnRuleData rule = _eco.Zones[zoneIndex].Rules[ruleIndex];
            return rule.CatchLimitPerRun >= 0 && rule.SpeciesIndex == roll.Catch.ItemIndex;
        }

        // M3-D14: the first resolution settles the reservation exactly once. Landed (which also covers the ReleasedCreelFull announcement:
        // the outcome stays Landed) commits; every other outcome (Cancelled, Snapped, Escaped incl. Invalid, Missed, Timeout) releases.
        private void SettleLimit(in ActiveCast a, bool landed)
        {
            if (!a.LimitReserved) return;
            if (landed) Run.Catches.Commit(a.Roll.Catch.ItemIndex);
            else Run.Catches.Release(a.Roll.Catch.ItemIndex);
        }

        // Waiting + match -> Cancelled, refund (CancelRefundsBait), cleared. Hooked + match -> Escaped(Dropped), announced Lost, no refund
        // (B-Q9; the owner never sends Cancel while Hooked). No match -> orphan: Accepted = false, the stored resolution repeated.
        public CommitDecision Cancel(ulong clientId, uint castId)
        {
            if (!_clients.TryGetValue(clientId, out ClientState c) || !c.HasActive || c.Active.CastId != castId)
                return Orphan(clientId, c, castId);
            ActiveCast a = c.Active;
            if (a.Phase == CastPhase.Waiting) return ResolveCancelledWaiting(clientId, c);
            var d = new CommitDecision
            {
                ClientId = clientId, Slot = c.Slot, CastId = castId, Accepted = true,
                Outcome = FightOutcome.Escaped, Reason = EscapeReason.Dropped, Roll = a.Roll,
                Announce = CatchValidator.AnnounceFor(FightOutcome.Escaped, EscapeReason.Dropped, true),
            };
            Resolve(c, FightOutcome.Escaped, EscapeReason.Dropped);
            return d;
        }

        // true = a commit must be sent (too early or a non-finite host distance -> Escaped(Invalid); orphan). false = accepted (Phase Hooked, THooked = Now,
        // LineLength0 set) or an already-hooked duplicate (no change). commit is default when false is returned.
        public bool Hooked(ulong clientId, uint castId, float hostDistanceToLanding, out CommitDecision commit)
        {
            if (!_clients.TryGetValue(clientId, out ClientState c) || !c.HasActive || c.Active.CastId != castId)
            {
                commit = Orphan(clientId, c, castId);
                return true;
            }
            commit = default;
            if (c.Active.Phase == CastPhase.Hooked) return false;
            ActiveCast a = c.Active;
            // Fail closed on a non-finite host distance (the early-hook invalid path): HostLineLength0 would map NaN to the landing
            // distance, a zero landing floor.
            if (!CatchValidator.IsFinite(hostDistanceToLanding) || CatchValidator.HookTooEarly(Now, in a, _db.Tuning.HookTolerance))
            {
                commit = new CommitDecision
                {
                    ClientId = clientId, Slot = c.Slot, CastId = castId, Accepted = true,
                    Outcome = FightOutcome.Escaped, Reason = EscapeReason.Invalid, Roll = a.Roll, Announce = AnnounceKind.None,
                };
                Resolve(c, FightOutcome.Escaped, EscapeReason.Invalid);
                return true;
            }
            a.Phase = CastPhase.Hooked;
            a.THooked = Now;
            float landDistance = _db.Tuning.LandDistance;
            float hostLine = CatchValidator.HostLineLength0(hostDistanceToLanding, landDistance);
            // R67: a.CastDistance is measured from the rod tip; the landing is CastTipReach further from the cast origin, and the walk bound
            // is on the player's distance to the landing, so it takes the origin -> landing distance.
            a.LineLength0 = CatchValidator.WalkBoundedLineLength0(hostLine, FishingConstants.CastTipReach + a.CastDistance, Now - a.TAccepted, landDistance);
            c.Active = a;
            return false;
        }

        public CommitDecision Result(ulong clientId, uint castId, FightOutcome outcome, EscapeReason reason,
                                     in FightReport report, in CreelSnapshot creel)
        {
            if (!_clients.TryGetValue(clientId, out ClientState c) || !c.HasActive || c.Active.CastId != castId)
                return Orphan(clientId, c, castId);
            ActiveCast a = c.Active;
            bool wasHooked = a.Phase == CastPhase.Hooked;
            bool junk = a.Roll.Catch.IsJunk;
            ref readonly FishingTuning t = ref _db.Tuning;
            float speed = junk ? t.JunkDragSpeed
                : CatchValidator.MaxReelSpeed(_db.Rods[a.RodIndex], _db.Profiles[_db.Species[a.Roll.Catch.ItemIndex].FightProfileIndex], in t);   // host rod and rolled profile, never an owner value (R72 MaxReelSpeed)

            bool valid;
            switch (outcome)
            {
                case FightOutcome.Landed:
                    valid = wasHooked && !CatchValidator.LandedTooFast(Now, in a, t.LandDistance, speed);
                    reason = EscapeReason.None; break;
                case FightOutcome.Snapped:
                    valid = wasHooked; reason = EscapeReason.None; break;
                case FightOutcome.Escaped:
                    valid = reason == EscapeReason.Missed ? !wasHooked
                        : reason == EscapeReason.Timeout ? wasHooked && CatchValidator.OwnerTimeoutPlausible(Now, in a, t.FightTimeout)
                        : wasHooked && (reason == EscapeReason.Pinned || reason == EscapeReason.ThrewHook || reason == EscapeReason.Dropped);   // Pinned: harmless, the owner no longer sends it
                    break;
                default: valid = false; break;                                            // None, Cancelled
            }
            if (!valid) { outcome = FightOutcome.Escaped; reason = EscapeReason.Invalid; }

            var d = new CommitDecision
            {
                ClientId = clientId, Slot = c.Slot, CastId = castId, Accepted = true,
                Outcome = outcome, Reason = reason, Roll = a.Roll,
            };
            if (outcome == FightOutcome.Landed)
            {
                if (CatchValidator.CleanClaimAccepted(in report, Now, in a, t.LandDistance, speed, t.CleanCatchMaxTension))
                {
                    d.Roll.Catch = CatchMath.WithCleanBonus(a.Roll.Catch, _db.Species[a.Roll.Catch.ItemIndex], a.Roll.WeightT);
                    d.CleanBonus = true;
                }
                d.AddToCreel = CreelRules.CanAccept(in creel);
                d.Announce = d.AddToCreel ? AnnounceKind.Landed : AnnounceKind.ReleasedCreelFull;
                _catchLog.Add(d.Roll.Catch);
            }
            else d.Announce = CatchValidator.AnnounceFor(outcome, reason, wasHooked);
            Resolve(c, outcome, reason);    // clears HasActive; stores (castId -> outcome, reason), keeps the last 16
            return d;
        }

        // ---- clock and lifetime ------------------------------------------------------------------------------------

        // Advances Now unless paused. Stuck Waiting (Now - TAccepted > TFlight + BiteWait + 30) resolves as a refunded Cancelled;
        // Hooked past MaxFightTime resolves Escaped(Timeout), announced Lost. Clients are visited in ascending id order.
        public void Tick(double dt, bool paused, List<CommitDecision> unsolicited)
        {
            if (unsolicited == null) throw new ArgumentNullException(nameof(unsolicited));
            if (!paused && dt > 0 && !double.IsInfinity(dt)) Now += dt;   // NaN, negative, zero and infinite dt are ignored
            for (int i = 0; i < _clientOrder.Count; i++)   // index loop over the sorted id list: no enumerator allocation per host frame
            {
                ulong clientId = _clientOrder[i];
                ClientState c = _clients[clientId];
                if (!c.HasActive) continue;
                ActiveCast a = c.Active;
                if (a.Phase == CastPhase.Waiting)
                {
                    if (Now - a.TAccepted > a.TFlight + a.Roll.BiteWait + FishingConstants.HostWaitSlackSeconds)
                        unsolicited.Add(ResolveCancelledWaiting(clientId, c));
                }
                else if (Now - a.THooked > _db.Tuning.MaxFightTime)
                {
                    unsolicited.Add(new CommitDecision
                    {
                        ClientId = clientId, Slot = c.Slot, CastId = a.CastId, Accepted = true,
                        Outcome = FightOutcome.Escaped, Reason = EscapeReason.Timeout, Roll = a.Roll,
                        Announce = CatchValidator.AnnounceFor(FightOutcome.Escaped, EscapeReason.Timeout, true),
                    });
                    Resolve(c, FightOutcome.Escaped, EscapeReason.Timeout);
                }
            }
        }

        // Removes the client entry: ActiveCast, last accepted id and stored resolutions. No commit and no refund of a Waiting cast
        // (B-Q11). Slot data (stock, rod, bait, FailStreak) is untouched. True if the client held an active cast.
        // M3-D14: the one clear that bypasses Resolve (D-F1), so a held catch-limit reservation is released here (never committed).
        public bool Disconnect(ulong clientId)
        {
            if (!_clients.TryGetValue(clientId, out ClientState c)) return false;
            if (c.HasActive) SettleLimit(in c.Active, false);
            _clients.Remove(clientId);
            int at = _clientOrder.BinarySearch(clientId);
            if (at >= 0) _clientOrder.RemoveAt(at);
            return c.HasActive;
        }

        public bool TryGetActive(ulong clientId, out ActiveCast cast)
        {
            if (_clients.TryGetValue(clientId, out ClientState c) && c.HasActive)
            {
                cast = c.Active;
                return true;
            }
            cast = default;
            return false;
        }

        // ---- internals ---------------------------------------------------------------------------------------------

        private bool HasActiveCast(ulong clientId)
        {
            return _clients.TryGetValue(clientId, out ClientState c) && c.HasActive;
        }

        private ClientState GetOrAddClient(ulong clientId, byte slot)
        {
            if (!_clients.TryGetValue(clientId, out ClientState c))
            {
                c = new ClientState { Slot = slot };
                _clients[clientId] = c;
                int at = _clientOrder.BinarySearch(clientId);
                _clientOrder.Insert(~at, clientId);   // not present: BinarySearch returns the complement of the insertion index
            }
            return c;
        }

        // Cancel in Waiting and the stuck-Waiting timeout share this: refund per CancelRefundsBait (never for an unlimited bait).
        private CommitDecision ResolveCancelledWaiting(ulong clientId, ClientState c)
        {
            ActiveCast a = c.Active;
            bool refunded = false;
            if (_db.Tuning.CancelRefundsBait && !_db.Baits[a.BaitIndex].Unlimited)
            {
                SlotState s = _slots[c.Slot];
                if (s.Stock[a.BaitIndex] < ushort.MaxValue - 1) s.Stock[a.BaitIndex]++;
                refunded = true;
            }
            var d = new CommitDecision
            {
                ClientId = clientId, Slot = c.Slot, CastId = a.CastId, Accepted = true,
                Outcome = FightOutcome.Cancelled, Reason = EscapeReason.None, Refunded = refunded, Roll = a.Roll, Announce = AnnounceKind.None,
            };
            Resolve(c, FightOutcome.Cancelled, EscapeReason.None);
            return d;
        }

        // A message for a cast that is not the client active one: repeat the stored resolution, else Cancelled/None. Never refunds,
        // announces or adds to a creel; the roll is default (BF-4). c may be null (unknown client).
        private static CommitDecision Orphan(ulong clientId, ClientState c, uint castId)
        {
            FightOutcome outcome = FightOutcome.Cancelled;
            EscapeReason reason = EscapeReason.None;
            if (c != null)
            {
                for (int i = c.Resolved.Count - 1; i >= 0; i--)
                {
                    if (c.Resolved[i].CastId != castId) continue;
                    outcome = c.Resolved[i].Outcome;
                    reason = c.Resolved[i].Reason;
                    break;
                }
            }
            return new CommitDecision
            {
                ClientId = clientId, Slot = c != null ? c.Slot : (byte)0, CastId = castId, Accepted = false,
                Outcome = outcome, Reason = reason, Announce = AnnounceKind.None,
            };
        }

        // Stores the first resolution of the active cast, updates the slot FailStreak (R65) and clears the cast.
        //   Cancelled: no change. Otherwise a Pity cast resets the streak whatever happened; a normal cast resets it on Landed
        //   and adds one on Snapped or any Escaped.
        private void Resolve(ClientState c, FightOutcome outcome, EscapeReason reason)
        {
            ActiveCast a = c.Active;
            SettleLimit(in a, outcome == FightOutcome.Landed);    // first line: every first resolution passes here (D-F1, M3-D14)
            if (outcome != FightOutcome.Cancelled && _slots.TryGetValue(c.Slot, out SlotState s))
            {
                if (a.Roll.Pity || outcome == FightOutcome.Landed)
                {
                    s.FailStreak = 0;
                    s.PityAnnounced = false;
                }
                else if (s.FailStreak < int.MaxValue) s.FailStreak++;
            }
            c.Resolved.Add(new Resolution { CastId = a.CastId, Outcome = outcome, Reason = reason });
            if (c.Resolved.Count > ResolutionHistory) c.Resolved.RemoveAt(0);
            c.HasActive = false;
            c.Active = default;
        }
    }
}
