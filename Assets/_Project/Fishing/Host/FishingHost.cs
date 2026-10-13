// Authority: HOST ONLY (inert on clients: every entry point first checks IsServerActive, and the Ledger stays null there).
// Scene object "Fishing" (a plain MonoBehaviour, not a NetworkObject; C 2.8 #15). Owns the session's FishingHostLedger, ticks its pause-aware
// clock (IPauseState) and routes every host decision back through the sender's FishingRodNetwork: CommitRpc, creel add, CatchAnnouncedRpc,
// FishCaught. All decisions are made by the ledger (Core); this class is the Physics / NGO / EventBus adapter around it (spec 9.9).
// The ledger is created lazily on first server use, so it exists before the first player's OnPlayerReady regardless of scene/spawn order.
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Gameplay;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Fishing
{
    public sealed class FishingHost : MonoBehaviour
    {
        private const string DefaultRodId = "rod_handmade";   // C 5.3, M2-D14
        private const string DefaultBaitId = "bait_worm";

        private static FishingHost s_current;

        public static FishingHost Current => s_current;

        // Null on clients and before the first server use.
        public FishingHostLedger Ledger { get; private set; }

        public ulong RunSeed
        {
            get
            {
                if (Ledger != null) return Ledger.RunSeed;
                return _runSeed;
            }
        }

        private readonly List<CommitDecision> _unsolicited = new List<CommitDecision>();
        private readonly LogOnce _log = new LogOnce();
        private ContentRegistry _registry;
        private FishingDatabase _db;
        private ulong _runSeed = FishingConstants.DefaultDevRunSeed;
        private NetworkManager _nm;
        private bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_current = null;
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            if (s_current != null && s_current != this)
            {
                Debug.LogError("FishingHost: another FishingHost is already current; this one stays inert.", this);
                return;
            }
            s_current = this;
            _registered = true;

            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out INetworkSession session)) FishingDebugSettings.ClearOnSessionEnd(session);   // F6/F7 clear when the session ends

            ResolveNetworkManager();   // may still be null here; resolved lazily on first server use
            FishingRodNetwork.Despawned += HandleRodDespawned;
            SessionState.SlotTakenOver += HandleSlotTakenOver;

            // Rods that spawned before this host enabled.
            var rods = FishingRodNetwork.Active;
            for (int i = 0; i < rods.Count; i++)
            {
                FishingRodNetwork rod = rods[i];
                if (rod != null && rod.IsServer && rod.Player != null && rod.Player.IsServerInitialized) OnPlayerReady(rod);
            }
        }

        private void OnDisable()
        {
            if (!_registered) return;
            _registered = false;
            if (_nm != null) _nm.OnConnectionEvent -= HandleConnectionEvent;
            _nm = null;
            FishingRodNetwork.Despawned -= HandleRodDespawned;
            SessionState.SlotTakenOver -= HandleSlotTakenOver;
            Ledger = null;
            if (s_current == this) s_current = null;
            // F6/F7 clear on INetworkSession.Ended (FishingDebugSettings.ClearOnSessionEnd), not here: a Spot load must keep them (M3 map switch).
        }

        private bool IsServerActive
        {
            get
            {
                NetworkManager nm = ResolveNetworkManager();
                return nm != null && nm.IsServer && nm.IsListening;
            }
        }

        // Lazy: the scene object can enable before the NetworkManager singleton exists. Subscribes to connection events on first resolve.
        private NetworkManager ResolveNetworkManager()
        {
            if (_nm == null)
            {
                NetworkManager nm = NetworkManager.Singleton;
                if (nm != null)
                {
                    _nm = nm;
                    _nm.OnConnectionEvent += HandleConnectionEvent;
                }
            }
            return _nm;
        }

        private bool IsPaused()
        {
            ServiceRegistry game = ServiceRoots.Game;
            return game != null && game.TryGet(out IPauseState pause) && pause.Paused;
        }

        private bool EnsureLedger()
        {
            if (Ledger != null) return true;
            if (!IsServerActive) return false;

            if (_registry == null)
            {
                ServiceRegistry app = ServiceRoots.App;
                if (app != null && app.TryGet(out ContentRegistry registry)) _registry = registry;
            }
            if (_registry != null && _db == null) _db = _registry.Fishing;
            if (_db == null)
            {
                if (_log.ShouldLog("no-db")) Debug.LogError("FishingHost: no fishing database; the ledger cannot start.", this);
                return false;
            }

            try
            {
                ushort rod = _registry.RodIndex(DefaultRodId);
                byte bait = (byte)_registry.BaitIndex(DefaultBaitId);
                Ledger = new FishingHostLedger(_db, _runSeed, rod, bait);
            }
            catch (Exception e)   // KeyNotFoundException (missing default id) or a bad database: the handlers then use their fallback replies
            {
                Ledger = null;
                if (_log.ShouldLog("ledger-create-failed")) Debug.LogError("FishingHost: the ledger could not be created: " + e.Message, this);
                return false;
            }
            return true;
        }

        // Dev: only before the first accepted cast (else ignored, logged once). A new seed recreates the ledger and every loadout.
        public void SetRunSeed(ulong seed)
        {
            if (Ledger != null && Ledger.HostCastCounter > 0)
            {
                if (_log.ShouldLog("seed-late")) Debug.LogWarning("FishingHost: SetRunSeed ignored after the first accepted cast.", this);
                return;
            }
            _runSeed = seed;
            if (Ledger == null) return;

            Ledger = null;
            if (!EnsureLedger()) return;
            var rods = FishingRodNetwork.Active;
            for (int i = 0; i < rods.Count; i++)
            {
                FishingRodNetwork rod = rods[i];
                if (rod != null && rod.IsServer && rod.Player != null && rod.Player.IsServerInitialized) OnPlayerReady(rod);
            }
        }

        // ------------------------------------------------------------------ player / disconnect

        // Server: EnsureSlot, then mirror the loadout into the rod's NetworkVariables.
        internal void OnPlayerReady(FishingRodNetwork rod)
        {
            if (!EnsureLedger() || rod == null || rod.Player == null) return;
            Ledger.EnsureSlot(rod.Player.Slot);
            MirrorLoadout(rod, rod.Player.Slot);
        }

        private void MirrorLoadout(FishingRodNetwork rod, byte slot)
        {
            if (Ledger == null) return;
            rod.ServerSetLoadout(Ledger.GetLoadout(slot));
        }

        // Two idempotent disconnect paths (neither commits nor announces; bait, rod and creel stay with the slot).
        private void HandleConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (data.EventType != ConnectionEvent.ClientDisconnected) return;
            if (Ledger != null) Ledger.Disconnect(data.ClientId);
        }

        // A different identity took a slot over (SessionState.Upsert, host only): its rod, bait, stock and pity streak start fresh, like a new
        // slot. Mirrors the reset loadout into that slot's rod if one is already spawned; otherwise OnPlayerReady mirrors it later.
        private void HandleSlotTakenOver(int slot)
        {
            if (Ledger == null || !IsServerActive) return;
            if (!Ledger.ResetSlot(slot)) return;
            var rods = FishingRodNetwork.Active;
            for (int i = 0; i < rods.Count; i++)
            {
                FishingRodNetwork rod = rods[i];
                if (rod != null && rod.IsServer && rod.Player != null && rod.Player.IsServerInitialized && rod.Player.Slot == slot)
                    MirrorLoadout(rod, (byte)slot);
            }
        }

        private void HandleRodDespawned(FishingRodNetwork rod)
        {
            if (rod == null || _nm == null || !_nm.IsServer) return;
            if (Ledger != null) Ledger.Disconnect(rod.OwnerClientId);
        }

        // ------------------------------------------------------------------ handlers (server)

        internal void HandleCastRequest(FishingRodNetwork rod, in CastRequestData req)
        {
            if (!EnsureLedger() || rod.Player == null || !rod.Player.IsServerInitialized)
            {
                rod.CastResultRpc(req.CastId, false, (byte)CastRejectReason.UnknownPlayer, Vector3.zero, default);   // never echo the client origin
                return;
            }

            // The origin and yaw come from the client: never hand a non-finite one to the physics probe (reply through the normal invalid path).
            if (!CatchValidator.IsOriginFinite(in req))
            {
                rod.CastResultRpc(req.CastId, false, (byte)CastRejectReason.OriginTooFar, Vector3.zero, default);
                return;
            }

            byte slot = rod.Player.Slot;
            Ledger.EnsureSlot(slot);   // B-Q10: RequestCast for a slot never ensured is UnknownPlayer
            Vector3 seen = rod.transform.position;   // the host's interpolated view of the sender's root
            if (!CatchValidator.OriginWithinSlack(req.OriginX, req.OriginZ, seen.x, seen.z))   // same 3 m rule as the ledger, BEFORE any raycast
            {
                rod.CastResultRpc(req.CastId, false, (byte)CastRejectReason.OriginTooFar, Vector3.zero, default);
                return;
            }
            var view = new HostCastView { PlayerX = seen.x, PlayerY = seen.y, PlayerZ = seen.z, CastingAllowed = true };
            if (Ledger.TryComputeLanding(slot, in req, out float lx, out float lz)) view.Probe = CastProbe.Probe(lx, lz);

            CastVerdict v = Ledger.RequestCast(rod.OwnerClientId, slot, in req, in view, FishingDebugSettings.ToOverrides(_registry));
            MirrorLoadout(rod, slot);   // bait consumed on accept -> NetSelectedBaitCount

            float y = v.Valid ? view.Probe.WaterY : WaterSurface.FlatHeight;
            rod.CastResultRpc(req.CastId, v.Valid, (byte)v.Reason, new Vector3(v.LandingX, y, v.LandingZ),
                v.Valid ? FishingRollNet.From(in v.Roll) : default);
            if (v.Valid && v.AnnouncePity)   // R65: the crew sees the lucky line (toast.pity, T25); once per streak, not per cancel-and-recast
                rod.CatchAnnouncedRpc(slot, default, (byte)AnnounceKind.PityCast, 0);
        }

        internal void HandleCancel(FishingRodNetwork rod, uint castId)
        {
            if (!EnsureLedger())
            {
                rod.CommitRpc(castId, false, (byte)FightOutcome.Cancelled, (byte)EscapeReason.None);
                return;
            }
            Commit(rod, Ledger.Cancel(rod.OwnerClientId, castId));   // Waiting: refund + clear; orphan: stored resolution
        }

        internal void HandleHooked(FishingRodNetwork rod, uint castId)
        {
            if (!EnsureLedger())
            {
                rod.CommitRpc(castId, false, (byte)FightOutcome.Escaped, (byte)EscapeReason.Invalid);
                return;
            }
            ulong client = rod.OwnerClientId;
            float hostDistance = 0f;
            if (Ledger.TryGetActive(client, out ActiveCast cast))
            {
                Vector3 p = rod.transform.position;   // spec 9.9: the host's CURRENT view of the player
                float dx = p.x - cast.LandingX, dz = p.z - cast.LandingZ;
                hostDistance = Mathf.Sqrt(dx * dx + dz * dz);   // NaN stays NaN: the ledger fails closed on it
            }
            if (Ledger.Hooked(client, castId, hostDistance, out CommitDecision commit)) Commit(rod, in commit);   // too early / orphan
        }

        internal void HandleResult(FishingRodNetwork rod, uint castId, FightOutcome outcome, EscapeReason reason, in FightReport report)
        {
            if (!EnsureLedger())
            {
                rod.CommitRpc(castId, false, (byte)FightOutcome.Escaped, (byte)EscapeReason.Invalid);
                return;
            }
            ref readonly FishingTuning t = ref _db.Tuning;
            CreelSnapshot creel = rod.Creel != null
                ? rod.Creel.Snapshot(t.CreelMaxEntries, t.CreelMaxWeightG)
                : new CreelSnapshot(0, 0, t.CreelMaxEntries, t.CreelMaxWeightG);
            Commit(rod, Ledger.Result(rod.OwnerClientId, castId, outcome, reason, in report, in creel));
        }

        internal void HandleSelectBait(FishingRodNetwork rod, byte baitIndex)
        {
            if (!EnsureLedger() || rod.Player == null || !rod.Player.IsServerInitialized) return;
            Ledger.SelectBait(rod.OwnerClientId, rod.Player.Slot, baitIndex);   // refused while active or invalid
            MirrorLoadout(rod, rod.Player.Slot);
        }

        internal void HandleSelectRod(FishingRodNetwork rod, ushort rodIndex)
        {
            if (!EnsureLedger() || rod.Player == null || !rod.Player.IsServerInitialized) return;
            Ledger.SelectRod(rod.OwnerClientId, rod.Player.Slot, rodIndex);
            MirrorLoadout(rod, rod.Player.Slot);
        }

        // C 7.17 commit path: CommitRpc -> creel add -> CatchAnnouncedRpc -> FishCaught. CatchLog is kept by the ledger itself.
        // Ruling E-Q6: Accepted is false ONLY for orphan repeats, so effects are driven by the decision fields, not by Accepted:
        // creel add iff AddToCreel; announce iff Announce != None; FishCaught iff Outcome == Landed && AddToCreel. An orphan repeat
        // carries AddToCreel false and Announce None, so it only unlocks the owner. Invalid and cancel resolutions announce nothing by design.
        private void Commit(FishingRodNetwork rod, in CommitDecision d)
        {
            rod.CommitRpc(d.CastId, d.Accepted, (byte)d.Outcome, (byte)d.Reason);   // 1. owner unlock (deferred on a host-owned rod)
            byte slot = rod.Player != null ? rod.Player.Slot : d.Slot;
            ref readonly FishingTuning t = ref _db.Tuning;

            SessionState session = SessionState.Current;
            bool creelLedger = session != null && session.ServerCreels != null;
            if (d.AddToCreel && !creelLedger)
            {
                // No slot creel store (session despawning): skip the add but still announce, publish FishCaught and mirror the loadout.
                if (_log.ShouldLog("no-creel-ledger")) Debug.LogError("FishingHost: SessionState.ServerCreels is missing; the catch is not added to the creel.", this);
            }
            else if (d.AddToCreel)   // 2. host creel (slot ledger + NetCreel mirror)
            {
                var item = new CreelItem
                {
                    ItemIndex = d.Roll.Catch.ItemIndex, IsJunk = d.Roll.Catch.IsJunk, WeightG = d.Roll.Catch.WeightG,
                    Quality = d.Roll.Catch.Quality, ValueCoins = d.Roll.Catch.ValueCoins, CaughtAtRunMinute = 0,
                };
                if (rod.Creel == null || !rod.Creel.ServerTryAdd(in item, t.CreelMaxEntries, t.CreelMaxWeightG, out _))
                    Debug.LogError($"FishingHost: creel refused a catch the ledger accepted (slot {slot}).", this);   // same-frame snapshot: a bug
            }

            if (d.Announce != AnnounceKind.None)   // 3. everyone's reveal / toast
            {
                rod.CatchAnnouncedRpc(slot, CatchResultNet.From(in d.Roll.Catch), (byte)d.Announce,
                    d.CleanBonus ? RodWireQuant.AnnounceFlagClean : (byte)0);
            }

            if (d.Outcome == FightOutcome.Landed && d.AddToCreel)   // 4. host bus, published only on the host (not for ReleasedCreelFull)
            {
                EventBus bus = ResolveBus();
                if (bus != null) bus.Publish(new FishCaught(slot, in d.Roll.Catch));
            }

            MirrorLoadout(rod, slot);   // 5. refunds show at once
        }

        private static EventBus ResolveBus()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out EventBus bus)) return bus;
            return null;
        }

        private FishingRodNetwork FindRod(ulong clientId)
        {
            var rods = FishingRodNetwork.Active;
            for (int i = 0; i < rods.Count; i++)
            {
                FishingRodNetwork rod = rods[i];
                if (rod != null && rod.OwnerClientId == clientId) return rod;
            }
            return null;
        }

        private void Update()
        {
            if (Ledger == null || !IsServerActive) return;
            _unsolicited.Clear();
            Ledger.Tick(Time.deltaTime, IsPaused(), _unsolicited);   // pause-aware clock; stuck-Waiting refund; MaxFightTime -> Escaped(Timeout)
            for (int i = 0; i < _unsolicited.Count; i++)
            {
                CommitDecision d = _unsolicited[i];
                FishingRodNetwork rod = FindRod(d.ClientId);
                if (rod != null) Commit(rod, in d);   // null: the client left; Disconnect already cleared it
            }
        }
    }
}
