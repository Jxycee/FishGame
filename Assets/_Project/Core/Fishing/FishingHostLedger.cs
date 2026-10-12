// Core/Fishing/FishingHostLedger.cs: the host's pure fishing ledger (spec 9.9, C 7.9): bait stock, rolls, ActiveCast, outcomes.
// Authority: Host only. Owns bait stock, rolls, ActiveCast and outcomes (spec 9.9 N6); the owner reports outcomes, the host validates and never replays the fight.
// Keys (M2-D20): rod, selected bait, bait stock and the bad-luck FailStreak by SLOT (survive rejoin); lastAcceptedCastId, ActiveCast
// and the last 16 stored resolutions by CLIENT ID (reset on disconnect, since castIds restart per connection).
// Bad-luck nudge (R65 / M2-D23): host-decided and deterministic, no RNG. See Resolve and RequestCast.
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

        private readonly FishingDatabase _db;
        private readonly Dictionary<byte, SlotState> _slots = new Dictionary<byte, SlotState>();
        private readonly SortedDictionary<ulong, ClientState> _clients = new SortedDictionary<ulong, ClientState>();   // ascending clientId: deterministic Tick order
        private readonly List<CatchResult> _catchLog = new List<CatchResult>();
        private readonly ushort _defaultRod;
        private readonly byte _defaultBait;

        public FishingHostLedger(FishingDatabase db, ulong runSeed, ushort defaultRodIndex, byte defaultBaitIndex)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            if (defaultRodIndex >= db.Rods.Count) throw new ArgumentOutOfRangeException(nameof(defaultRodIndex));
            if (defaultBaitIndex >= db.Baits.Count) throw new ArgumentOutOfRangeException(nameof(defaultBaitIndex));
            RunSeed = runSeed;
            _defaultRod = defaultRodIndex;
            _defaultBait = defaultBaitIndex;
        }

        public ulong RunSeed { get; }
        public uint HostCastCounter { get; private set; }
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

        // Host rod castMaxDist. False for an unknown slot, a charge that is NaN or outside [0, 1], or a non-finite yaw.
        public bool TryComputeLanding(byte slot, in CastRequestData req, out float x, out float z)
        {
            x = 0f; z = 0f;
            if (!_slots.TryGetValue(slot, out SlotState s)) return false;
            float charge = req.ChargeNorm;
            if (!(charge >= 0f && charge <= 1f)) return false;
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

            // Roll first: a bad override throws before any state changes.
            uint counter = HostCastCounter + 1;
            Pcg32 rng = Rng.Stream(RunSeed, FishingConstants.CatchStreamId, counter);
            var rr = new RollRequest
            {
                BaitIndex = req.BaitIndex, LoadedCount = 1, Depth = view.Probe.Depth, Luck = 0f,
                ForcedBiteWait = overrides.ForcedBiteWait, ForcedItemIndex = overrides.ForcedItemIndex,
                ForceJunk = overrides.ForceJunk, Twitchy = overrides.Twitchy,
            };
            FishingRoll roll = PlaceholderRoller.Roll(_db, in rr, ref rng);
            roll.Pity = t.PityAfterFailures > 0 && s.FailStreak >= t.PityAfterFailures;   // R65: ledger decision, no extra draw

            float distance = CastMath.Distance(req.ChargeNorm, t.CastMinDist, _db.Rods[s.RodIndex].CastMaxDist);
            CastMath.Landing(req.OriginX, req.OriginZ, req.YawDeg, distance, out float lx, out float lz);

            if (!_db.Baits[req.BaitIndex].Unlimited) s.Stock[req.BaitIndex]--;
            HostCastCounter = counter;
            c.Slot = slot;
            c.LastAccepted = req.CastId;
            c.HasActive = true;
            c.Active = new ActiveCast
            {
                CastId = req.CastId, Roll = roll, Phase = CastPhase.Waiting, TAccepted = Now, THooked = 0.0,
                LineLength0 = 0f, TFlight = CastMath.FlightTime(distance), CastDistance = distance, Assist = req.Assist,
                RodIndex = s.RodIndex, BaitIndex = req.BaitIndex, LandingX = lx, LandingZ = lz,
            };
            return new CastVerdict { Valid = true, Reason = CastRejectReason.None, Roll = roll, LandingX = lx, LandingZ = lz };
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
            a.LineLength0 = CatchValidator.WalkBoundedLineLength0(hostLine, a.CastDistance, Now - a.TAccepted, landDistance);
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
            float speed = junk ? t.JunkDragSpeed : _db.Rods[a.RodIndex].ReelSpeed;   // host rod, never an owner value

            bool valid;
            switch (outcome)
            {
                case FightOutcome.Landed:
                    valid = wasHooked && !CatchValidator.LandedTooFast(Now, in a, t.LandDistance, speed);
                    reason = EscapeReason.None; break;
                case FightOutcome.Snapped:
                    valid = wasHooked; reason = EscapeReason.None; break;
                case FightOutcome.Escaped:
                    valid = reason == EscapeReason.Missed
                        ? !wasHooked
                        : wasHooked && (reason == EscapeReason.Pinned || reason == EscapeReason.ThrewHook || reason == EscapeReason.Dropped);
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
            foreach (KeyValuePair<ulong, ClientState> kv in _clients)
            {
                ClientState c = kv.Value;
                if (!c.HasActive) continue;
                ActiveCast a = c.Active;
                if (a.Phase == CastPhase.Waiting)
                {
                    if (Now - a.TAccepted > a.TFlight + a.Roll.BiteWait + FishingConstants.HostWaitSlackSeconds)
                        unsolicited.Add(ResolveCancelledWaiting(kv.Key, c));
                }
                else if (Now - a.THooked > _db.Tuning.MaxFightTime)
                {
                    unsolicited.Add(new CommitDecision
                    {
                        ClientId = kv.Key, Slot = c.Slot, CastId = a.CastId, Accepted = true,
                        Outcome = FightOutcome.Escaped, Reason = EscapeReason.Timeout, Roll = a.Roll,
                        Announce = CatchValidator.AnnounceFor(FightOutcome.Escaped, EscapeReason.Timeout, true),
                    });
                    Resolve(c, FightOutcome.Escaped, EscapeReason.Timeout);
                }
            }
        }

        // Removes the client entry: ActiveCast, last accepted id and stored resolutions. No commit and no refund of a Waiting cast
        // (B-Q11). Slot data (stock, rod, bait, FailStreak) is untouched. True if the client held an active cast.
        public bool Disconnect(ulong clientId)
        {
            if (!_clients.TryGetValue(clientId, out ClientState c)) return false;
            _clients.Remove(clientId);
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
            if (outcome != FightOutcome.Cancelled && _slots.TryGetValue(c.Slot, out SlotState s))
            {
                if (a.Roll.Pity || outcome == FightOutcome.Landed) s.FailStreak = 0;
                else if (s.FailStreak < int.MaxValue) s.FailStreak++;
            }
            c.Resolved.Add(new Resolution { CastId = a.CastId, Outcome = outcome, Reason = reason });
            if (c.Resolved.Count > ResolutionHistory) c.Resolved.RemoveAt(0);
            c.HasActive = false;
            c.Active = default;
        }
    }
}
