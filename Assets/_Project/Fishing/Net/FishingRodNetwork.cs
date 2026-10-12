// Authority: the OWNER writes NetRodState and sends the intent RPCs (CastRequest, Cancel, Hooked, Result, RodEvent, SelectBait, SelectRod);
// the HOST owns bait, rolls, the ActiveCast, outcomes and the creel (FishingHost + FishingHostLedger) and writes NetSelectedBait,
// NetSelectedBaitCount and NetRodIndex (mirrors of the ledger loadout). Every RPC attribute sets InvokePermission explicitly.
// Remote peers DISPLAY only: a remote rod is drawn from NetRodState, NetTool and NetRodIndex alone (C 2.8 #10); species stay hidden
// until CatchAnnouncedRpc. Server handlers only forward to FishingHost; every decision lives in the host and the ledger.
// Host-owned rods: SendTo.Server RPCs run synchronously inside the caller (N2); the owner-bound replies and the announcement are
// DeferLocal and arrive on the next frame's EarlyUpdate (N1, ruling E-Q8), so commit-then-announce holds on every peer, the host included.
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
    public sealed class FishingRodNetwork : NetworkBehaviour
    {
        private const int DefaultRodStateHz = 15;
        private const float SnapJumpMeters = 5f;
        private const float RemoteSmoothingRate = 12f;

        public readonly NetworkVariable<RodStateData> NetRodState =
            new NetworkVariable<RodStateData>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> NetSelectedBait =
            new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<ushort> NetSelectedBaitCount =
            new NetworkVariable<ushort>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // 0xFFFF unlimited
        public readonly NetworkVariable<ushort> NetRodIndex =
            new NetworkVariable<ushort>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static readonly List<FishingRodNetwork> s_active = new List<FishingRodNetwork>();

        public static IReadOnlyList<FishingRodNetwork> Active => s_active;
        public static event Action<FishingRodNetwork> Spawned, Despawned;
        public static event Action<CatchAnnouncement> AnyAnnounced;

        public PlayerNetState Player => _player;

        // Owner: live from FishingRodController (OwnerSetView). Remotes: NetRodState smoothed (lerp, 1 - exp(-12 dt)); the first value snaps.
        public RodViewState ViewState
        {
            get
            {
                RodViewState v = _view;
                v.IsOwner = IsOwner;
                v.RodOut = _player != null && _player.Tool == ToolKind.Rod;
                return v;
            }
        }

        public event Action<RodCue, byte> Cue;                 // every peer; arg = sizeClass (Snapped) or 0
        public event Action<CatchAnnouncement> Announced;      // every peer, for this rod's catcher

        // Owner-side receive events (raised from the deferred owner RPCs).
        public event Action<uint, bool, CastRejectReason, Vector3, FishingRoll> CastResultReceived;   // roll already sanity-checked
        public event Action<uint, bool, FightOutcome, EscapeReason> CommitReceived;

        internal PlayerCreel Creel => _creel;

        private readonly LogOnce _log = new LogOnce();
        private PlayerNetState _player;
        private PlayerCreel _creel;
        private FishingRodController _controller;
        private FishingDatabase _db;
        private RodViewState _view;
        private RodStateData _target;
        private bool _subscribed;
        private bool _waitingForServerInit;
        private bool _controllerEnabledByUs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active.Clear();
            Spawned = null;
            Despawned = null;
            AnyAnnounced = null;
        }

        private void Awake()
        {
            _player = GetComponent<PlayerNetState>();
            _creel = GetComponent<PlayerCreel>();
            _controller = GetComponent<FishingRodController>();

            int hz = DefaultRodStateHz;
            ContentRegistry registry = ResolveRegistry();
            if (registry != null)
            {
                FishingDatabase db = registry.Fishing;
                if (db != null && db.Tuning.RodStateHz > 0) hz = db.Tuning.RodStateHz;
            }
            NetRodState.SetUpdateTraits(new NetworkVariableUpdateTraits { MinSecondsBetweenUpdates = 1f / hz });
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _db = ResolveDatabase();
            if (!s_active.Contains(this)) s_active.Add(this);

            NetRodState.OnValueChanged += HandleRodStateChanged;
            if (_player != null) _player.ToolChanged += HandleToolChanged;
            _subscribed = true;

            // N5: seed from the current values (a late joiner may never get a first OnValueChanged), then react to changes.
            _target = NetRodState.Value;
            _view = new RodViewState
            {
                State = (RodState)_target.State,
                Flags = _target.Flags,
                Lateral = RodWireQuant.DequantizeLateral(_target.LateralQ),
                Tension = RodWireQuant.DequantizeTension(_target.TensionQ),
                BobberPos = _target.BobberPos,
                CastId = _target.CastId,
            };

            if (IsOwner)
            {
                if (_controller != null)
                {
                    _controller.enabled = true;
                    _controllerEnabledByUs = true;
                }
                else if (_log.ShouldLog("no-controller"))
                {
                    Debug.LogError("FishingRodNetwork: the owner's rod has no FishingRodController on the player root.", this);
                }
            }

            if (IsServer && _player != null)
            {
                if (_player.IsServerInitialized)
                {
                    NotifyHostPlayerReady();
                }
                else
                {
                    _player.ServerInitialized += HandleServerInitialized;
                    _waitingForServerInit = true;
                }
            }

            EventIsolation.Invoke(Spawned, this);
        }

        public override void OnNetworkDespawn()
        {
            Teardown();
            EventIsolation.Invoke(Despawned, this);
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            Teardown();
            base.OnDestroy();
        }

        private void Teardown()
        {
            if (_waitingForServerInit)
            {
                if (_player != null) _player.ServerInitialized -= HandleServerInitialized;
                _waitingForServerInit = false;
            }

            if (_subscribed)
            {
                NetRodState.OnValueChanged -= HandleRodStateChanged;
                if (_player != null) _player.ToolChanged -= HandleToolChanged;
                _subscribed = false;
            }

            if (_controllerEnabledByUs)
            {
                if (_controller != null && _controller.enabled) _controller.enabled = false;
                _controllerEnabledByUs = false;
            }

            s_active.Remove(this);
        }

        // ------------------------------------------------------------------ owner API

        // Owner only; writes only when the value really changed (N3/N4).
        public void OwnerWriteState(in RodStateData data)
        {
            if (!IsSpawned || !IsOwner) return;
            if (data.Equals(NetRodState.Value)) return;
            NetRodState.Value = data;
        }

        // Owner only: the controller pushes its live view every frame.
        internal void OwnerSetView(in RodViewState v)
        {
            if (!IsOwner) return;
            _view = v;
        }

        // Owner path into Cue. Remote cues come from RodEventRpc, the NetRodState diff and ToolChanged.
        public void RaiseLocalCue(RodCue cue, byte arg)
        {
            EventIsolation.Invoke(Cue, cue, arg);
        }

        // Host only: mirrors the ledger loadout into the NetworkVariables, each only when changed.
        internal void ServerSetLoadout(in PlayerLoadout l)
        {
            if (!IsServer) return;
            if (NetRodIndex.Value != l.RodIndex) NetRodIndex.Value = l.RodIndex;
            if (NetSelectedBait.Value != l.BaitIndex) NetSelectedBait.Value = l.BaitIndex;
            if (NetSelectedBaitCount.Value != l.BaitCount) NetSelectedBaitCount.Value = l.BaitCount;
        }

        // ------------------------------------------------------------------ remote view

        private void HandleToolChanged(ToolKind tool)
        {
            if (IsOwner) return;   // the owner's controller raises its own Equip/Stow through RaiseLocalCue
            RaiseLocalCue(tool == ToolKind.Rod ? RodCue.Equip : RodCue.Stow, 0);
        }

        // Remotes only (the writer also receives OnValueChanged, N4).
        private void HandleRodStateChanged(RodStateData prev, RodStateData cur)
        {
            if (IsOwner) return;
            _target = cur;
            var ps = (RodState)prev.State;
            var cs = (RodState)cur.State;

            if (cur.State != prev.State)
            {
                _view.State = cs;
                _view.Flags = cur.Flags;
                _view.CastId = cur.CastId;
                float jump = (cur.BobberPos - _view.BobberPos).magnitude;
                if (cs == RodState.Casting || cs == RodState.Idle || jump > SnapJumpMeters) _view.BobberPos = cur.BobberPos;
                RodCue cue = StateCue(ps, cs, cur.Flags);
                if (cue != RodCue.None) RaiseLocalCue(cue, 0);
            }

            if (cs == RodState.Hooked)
            {
                bool wasBurst = (prev.Flags & RodWireQuant.FlagBurst) != 0;
                bool isBurst = (cur.Flags & RodWireQuant.FlagBurst) != 0;
                if (!wasBurst && isBurst) RaiseLocalCue((cur.Flags & RodWireQuant.FlagDive) != 0 ? RodCue.Dive : RodCue.SideRun, 0);
                else if (wasBurst && !isBurst) RaiseLocalCue(RodCue.Calm, 0);

                bool wasExhausted = (prev.Flags & RodWireQuant.FlagExhausted) != 0;
                bool isExhausted = (cur.Flags & RodWireQuant.FlagExhausted) != 0;
                if (!wasExhausted && isExhausted) RaiseLocalCue(RodCue.Exhausted, 0);
            }
        }

        // The remote state-transition cue map (plan T17). Nibble, Bite, HookSet, Snapped and Escaped come only from RodEventRpc.
        internal static RodCue StateCue(RodState from, RodState to, byte flags)
        {
            switch (to)
            {
                case RodState.Ready:
                    if (from == RodState.Readying) return RodCue.Ready;
                    if (from == RodState.Charging) return RodCue.InvalidRelease;
                    return RodCue.None;
                case RodState.Charging:
                    return RodCue.ChargeStart;
                case RodState.Casting:
                    return RodCue.CastRelease;
                case RodState.Waiting:
                    return from == RodState.Casting ? RodCue.Landing : RodCue.None;
                case RodState.Idle:
                    return from == RodState.Casting || from == RodState.Waiting || from == RodState.Bite ? RodCue.Recall : RodCue.None;
                case RodState.Landed:
                    return RodCue.LineLanded;
                case RodState.Escaped:
                    return (flags & RodWireQuant.FlagRecall) != 0 ? RodCue.Recall : RodCue.None;
                default:
                    return RodCue.None;
            }
        }

        private void Update()
        {
            if (!IsSpawned || IsOwner) return;
            float k = 1f - Mathf.Exp(-RemoteSmoothingRate * Time.deltaTime);
            _view.State = (RodState)_target.State;
            _view.Flags = _target.Flags;
            _view.CastId = _target.CastId;
            _view.Lateral = Mathf.Lerp(_view.Lateral, RodWireQuant.DequantizeLateral(_target.LateralQ), k);
            _view.Tension = Mathf.Lerp(_view.Tension, RodWireQuant.DequantizeTension(_target.TensionQ), k);
            _view.BobberPos = Vector3.Lerp(_view.BobberPos, _target.BobberPos, k);
        }

        // ------------------------------------------------------------------ server init

        private void HandleServerInitialized(PlayerNetState player)
        {
            if (_waitingForServerInit)
            {
                if (_player != null) _player.ServerInitialized -= HandleServerInitialized;
                _waitingForServerInit = false;
            }
            if (IsSpawned && IsServer) NotifyHostPlayerReady();
        }

        private void NotifyHostPlayerReady()
        {
            FishingHost host = FishingHost.Current;
            if (host != null) host.OnPlayerReady(this);
        }

        // ---------------- owner -> host intents ----------------
        // On a host-owned rod these run synchronously inside the caller (N2).
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void CastRequestRpc(uint castId, Vector3 origin, float yawDeg, float chargeNorm, byte baitIndex, bool assist, RpcParams p = default)
        {
            var req = new CastRequestData
            {
                CastId = castId, OriginX = origin.x, OriginY = origin.y, OriginZ = origin.z,
                YawDeg = yawDeg, ChargeNorm = chargeNorm, BaitIndex = baitIndex, Assist = assist,
            };
            FishingHost host = ResolveHost();
            if (host == null)
            {
                CastResultRpc(castId, false, (byte)CastRejectReason.NotAllowedNow, origin, default);
                return;
            }
            host.HandleCastRequest(this, in req);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void CancelRpc(uint castId, RpcParams p = default)
        {
            FishingHost host = ResolveHost();
            if (host == null)
            {
                CommitRpc(castId, false, (byte)FightOutcome.Cancelled, (byte)EscapeReason.None);
                return;
            }
            host.HandleCancel(this, castId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void HookedRpc(uint castId, RpcParams p = default)
        {
            FishingHost host = ResolveHost();
            if (host == null)
            {
                CommitRpc(castId, false, (byte)FightOutcome.Escaped, (byte)EscapeReason.Invalid);
                return;
            }
            host.HandleHooked(this, castId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void ResultRpc(uint castId, byte outcome, byte reason, float maxTension, float fightSeconds, ushort nearSnapRecoveries, RpcParams p = default)
        {
            FishingHost host = ResolveHost();
            if (host == null)
            {
                CommitRpc(castId, false, outcome, reason);
                return;
            }
            var report = new FightReport { MaxTension = maxTension, FightSeconds = fightSeconds, NearSnapRecoveries = nearSnapRecoveries };
            host.HandleResult(this, castId, (FightOutcome)outcome, (EscapeReason)reason, in report);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectBaitRpc(byte baitIndex, RpcParams p = default)
        {
            FishingHost host = ResolveHost();
            if (host != null) host.HandleSelectBait(this, baitIndex);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectRodRpc(ushort rodIndex, RpcParams p = default)
        {
            FishingHost host = ResolveHost();
            if (host != null) host.HandleSelectRod(this, rodIndex);
        }

        // ---------------- host -> owner replies (DeferLocal: one frame later even in solo, N1) ----------------
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server, DeferLocal = true)]
        public void CastResultRpc(uint castId, bool valid, byte reason, Vector3 landing, FishingRollNet roll)
        {
            FishingRoll core = default;
            if (valid)
            {
                FishingDatabase db = ResolveDatabase();
                if (db == null || !roll.TryToCore(db, out core))
                {
                    // C 8.2 (ruling E-Q7): drop with one warning, send CancelRpc once and raise CastResultReceived(valid: false) at once.
                    // The host refunds (its cast is Waiting) and commits; that commit arrives while the machine is already Idle and is
                    // ignored (BF-5: this handler is the ONLY sender of that CancelRpc).
                    if (_log.ShouldLog("insane-roll")) Debug.LogWarning($"FishingRodNetwork: dropped an insane roll for cast {castId}; cancelling.", this);
                    CancelRpc(castId);
                    valid = false;
                    reason = (byte)CastRejectReason.NotAllowedNow;
                    core = default;
                }
            }
            CastResultReceived?.Invoke(castId, valid, (CastRejectReason)reason, landing, core);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server, DeferLocal = true)]
        public void CommitRpc(uint castId, bool accepted, byte finalOutcome, byte finalReason)
        {
            CommitReceived?.Invoke(castId, accepted, (FightOutcome)finalOutcome, (EscapeReason)finalReason);
        }

        // ---------------- broadcasts ----------------
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server, DeferLocal = true)]   // E-Q8: commit-then-announce on every peer incl. the host
        public void CatchAnnouncedRpc(byte catcherSlot, CatchResultNet catchResult, byte announceKind, byte flags)
        {
            CatchAnnouncement a = CatchAnnouncement.FromWire(catcherSlot, in catchResult, announceKind, flags, OwnerClientId);
            if (!IsPlausible(in a))
            {
                if (_log.ShouldLog("bad-announce")) Debug.LogWarning($"FishingRodNetwork: dropped an implausible announcement (kind {announceKind}).", this);
                return;
            }
            EventIsolation.Invoke(Announced, a);
            EventIsolation.Invoke(AnyAnnounced, a);
            EventBus bus = ResolveBus();
            if (bus != null) bus.Publish(new CatchAnnounced(in a, IsOwner));
        }

        [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]   // reliable (default delivery)
        public void RodEventRpc(byte eventType, byte sizeClass)
        {
            RodCue cue;
            switch ((RodEventType)eventType)
            {
                case RodEventType.Nibble: cue = RodCue.Nibble; break;
                case RodEventType.Bite: cue = RodCue.Bite; break;
                case RodEventType.HookSet: cue = RodCue.HookSet; break;
                case RodEventType.Snapped: cue = RodCue.Snapped; break;
                case RodEventType.Escaped: cue = RodCue.Escaped; break;
                default: return;
            }
            RaiseLocalCue(cue, cue == RodCue.Snapped ? sizeClass : (byte)0);
        }

        // ------------------------------------------------------------------ helpers

        // Kinds 1..4 only; for kinds 1..3 the item index must lie inside the registry's species / junk count when a registry exists.
        // PityCast carries a default catch, so its index is not checked.
        private bool IsPlausible(in CatchAnnouncement a)
        {
            byte kind = (byte)a.Kind;
            if (kind < 1 || kind > 4) return false;
            if (a.Kind == AnnounceKind.PityCast) return true;
            FishingDatabase db = ResolveDatabase();
            if (db == null) return true;
            return a.Catch.IsJunk ? a.Catch.ItemIndex < db.Junk.Count : a.Catch.ItemIndex < db.Species.Count;
        }

        private FishingHost ResolveHost()
        {
            FishingHost host = FishingHost.Current;
            if (host == null && _log.ShouldLog("no-host")) Debug.LogWarning("FishingRodNetwork: no FishingHost in this scene.", this);
            return host;
        }

        private FishingDatabase ResolveDatabase()
        {
            if (_db != null) return _db;
            ContentRegistry registry = ResolveRegistry();
            if (registry != null) _db = registry.Fishing;
            return _db;
        }

        private static ContentRegistry ResolveRegistry()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out ContentRegistry registry)) return registry;
            return null;
        }

        private static EventBus ResolveBus()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out EventBus bus)) return bus;
            return null;
        }
    }
}
