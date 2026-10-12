// Authority: owner only. Disabled on the prefab; FishingRodNetwork.OnNetworkSpawn enables it when IsOwner.
// Input -> RodStateMachine (Tick each frame, TickFight from a 50 Hz accumulator, max 5 steps) -> RPCs and cues.
// Requests its InputContext through IPlayerContextSource; PlayerLocomotion writes it (the only writer of GameplayContext).
// The host validates everything (FishingHost + ledger); this component only sends intents and writes NetRodState through the network
// component. It writes no presentation state (C 2.8 #17).
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Fishing
{
    [DefaultExecutionOrder(-100)]   // after PlayerInputAdapter (-1000), before PlayerLocomotion (0)
    public sealed class FishingRodController : MonoBehaviour, IPlayerContextSource
    {
        private const float DefaultTipHeight = 1.4f;

        public static FishingRodController Local { get; private set; }
        public static event Action<FishingRodController> LocalChanged;

        public RodStateMachine Machine { get; private set; }
        public RodStats Rod { get; private set; }
        public bool MarkerVisible { get; private set; }
        public bool MarkerValid { get; private set; }
        public Vector3 MarkerPosition { get; private set; }

        // Owner; one per cast that reached Waiting (telemetry).
        public event Action<FightEnded> FightEnded;

        public bool RodOut => _player != null && _player.Tool == ToolKind.Rod;

        public float ChargeNorm => Machine != null && Machine.State == RodState.Charging ? Machine.ChargeNorm : 0f;

        // World position of the STATIC TipAnchor (BF-3), never the bent TipVisual. Falls back to the root + 1.4 m up.
        public Vector3 RodTipPosition => _tip != null ? _tip.position : transform.position + Vector3.up * DefaultTipHeight;

        public InputContext RequestedContext => Machine == null ? InputContext.Free : RodControllerRules.ContextFor(Machine.State, RodOut);

        public bool LocksMovement => Machine != null && RodControllerRules.LocksMovement(Machine.State, RodOut);

        private readonly List<RodEffect> _effects = new List<RodEffect>(8);
        private readonly FishPointSampler _sampler = new FishPointSampler();
        private readonly FightRecorder _recorder = new FightRecorder();
        private readonly LogOnce _log = new LogOnce();

        private IInputService _input;
        private ISettings _settings;
        private ContentRegistry _registry;
        private FishingDatabase _db;
        private FishingTuning _tuning;
        private FishingRodNetwork _net;
        private PlayerNetState _player;
        private PlayerLocomotion _locomotion;
        private Transform _tip;

        private bool _setUp;
        private bool _suppressSends;
        private bool _draining, _redrain;
        private bool _wasSwimming, _swimStowed;
        private bool _hasLanding;
        private Vector3 _landing, _lastPoint, _hookRoot;
        private float _castYaw;
        private float _clock;
        private float _fightAccumulator;
        private FightInput _lastFightInput;
        private RodState _lastState = RodState.Idle;
        private int _appliedRodIndex = -1;
        private int _lastIgnored;
        private float _lastDt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalChanged = null;
        }

        private void OnEnable()
        {
            _suppressSends = false;
            ServiceRegistry app = ServiceRoots.App;
            if (app != null)
            {
                app.TryGet(out _input);
                app.TryGet(out _settings);
                app.TryGet(out _registry);
            }

            _db = _registry != null ? _registry.Fishing : null;
            _net = GetComponent<FishingRodNetwork>();
            _player = GetComponent<PlayerNetState>();
            _locomotion = GetComponent<PlayerLocomotion>();
            Transform tip = transform.Find(PlayerRigPaths.RodTipAnchor);   // the STATIC anchor (BF-3); never TipVisual
            _tip = tip;

            if (_input == null || _settings == null || _db == null || _net == null || _player == null || _locomotion == null)
            {
                Debug.LogError("FishingRodController: IInputService, ISettings, the fishing database or a sibling component is missing; disabling.", this);
                enabled = false;
                return;
            }

            _tuning = _db.Tuning;
            Machine = new RodStateMachine(in _tuning);
            _appliedRodIndex = -1;
            _lastIgnored = 0;
            _lastState = RodState.Idle;
            _hasLanding = false;
            _lastPoint = Vector3.zero;
            _fightAccumulator = 0f;
            _swimStowed = false;
            _wasSwimming = _locomotion.IsSwimming;
            _recorder.Reset();
            ApplyRod(true);

            _net.CastResultReceived += HandleCastResult;
            _net.CommitReceived += HandleCommit;
            _player.ToolChanged += HandleToolChanged;
            _locomotion.Teleported += HandleTeleported;
            _setUp = true;

            Local = this;
            EventIsolation.Invoke(LocalChanged, this);
        }

        private void OnDisable()
        {
            if (!_setUp) return;
            _setUp = false;

            // Despawn: the host clears the cast through Disconnect, so nothing may be sent from here.
            _suppressSends = true;
            if (Machine != null)
            {
                if (Machine.State != RodState.Idle)
                {
                    Machine.ForceEnd(RodForceReason.Disconnect);
                    ProcessEffects();
                }

                FinishRecording();   // a cast still in its terminal beat is recorded with its captured outcome
            }

            _input.SetRumble(0f, 0f);
            _net.CastResultReceived -= HandleCastResult;
            _net.CommitReceived -= HandleCommit;
            _player.ToolChanged -= HandleToolChanged;
            _locomotion.Teleported -= HandleTeleported;
            MarkerVisible = false;
            MarkerValid = false;

            if (Local == this)
            {
                Local = null;
                EventIsolation.Invoke(LocalChanged, (FishingRodController)null);
            }
        }

        private void Update()
        {
            if (Machine == null) return;
            float dt = Time.deltaTime;
            _lastDt = dt;
            bool paused = IsPaused();   // ServiceRoots.Game IPauseState (solo only)
            if (!paused) _clock += dt;   // unpaused owner clock (telemetry)
            PlayerIntent intent = _input.Intent;   // routed with the context locomotion wrote last frame

            if (!paused)
            {
                HandleEquip(in intent, _input.GameplayContext);
                HandleSwim();
                ApplyRod(false);
                if (Machine.State == RodState.Idle) Machine.Assist = _settings.Current != null && _settings.Current.fishingAssist;
            }

            RodFrame frame = BuildFrame(in intent, paused);
            Machine.Tick(dt, in frame);   // all non-fight rows of spec 9.2; Paused freezes timers
            ProcessEffects();

            if (Machine.State == RodState.Hooked && !paused)
            {
                _fightAccumulator += dt;
                int steps = 0;
                while (_fightAccumulator >= FishingConstants.FightStep && steps < FishingConstants.MaxFightStepsPerFrame
                       && Machine.State == RodState.Hooked)
                {
                    _lastFightInput = _input.Fight;   // RodFighting: (Steer, Reel || PrimaryHeld)
                    frame.HookDisplacement = Flat(transform.position, _hookRoot);
                    Machine.TickFight(in _lastFightInput, in frame);
                    _fightAccumulator -= FishingConstants.FightStep;
                    steps++;
                    ProcessEffects();
                }

                if (steps == FishingConstants.MaxFightStepsPerFrame) _fightAccumulator = 0f;   // spec 9.1: extra time is dropped
            }
            else if (Machine.State != RodState.Hooked)
            {
                _fightAccumulator = 0f;
            }

            PublishView();
        }

        // ------------------------------------------------------------------ equip, swim, rod stats

        private void HandleEquip(in PlayerIntent intent, InputContext context)
        {
            if (!intent.EquipPressed || !RodControllerRules.EquipAllowed(context)) return;
            if (RodOut) Stow(RodForceReason.Stow);
            else Equip();
        }

        // Owner Equip/Stow cues come only from these two methods (C 8.1); remotes get theirs from FishingRodNetwork's ToolChanged handler.
        private void Equip()
        {
            _player.OwnerSetTool(ToolKind.Rod);
            _net.RaiseLocalCue(RodCue.Equip, 0);
        }

        private void Stow(RodForceReason reason)
        {
            if (Machine.State != RodState.Idle)
            {
                Machine.ForceEnd(reason);
                ProcessEffects();
            }

            _player.OwnerSetTool(ToolKind.None);
            _net.RaiseLocalCue(RodCue.Stow, 0);
        }

        private void HandleSwim()
        {
            bool swimming = _locomotion.IsSwimming;
            if (swimming == _wasSwimming) return;
            _wasSwimming = swimming;
            if (swimming)
            {
                if (RodOut)
                {
                    Stow(RodForceReason.Swim);
                    _swimStowed = true;
                }
            }
            else if (_swimStowed)
            {
                Equip();
                _swimStowed = false;
            }
        }

        // NGO's default NetRodIndex is 0 until the host's OnPlayerReady mirror lands (G2), so the real rod is re-applied as soon as it arrives.
        private void ApplyRod(bool force)
        {
            int idx = _net.NetRodIndex.Value;
            if (idx == _appliedRodIndex) return;
            if (!force && Machine.State != RodState.Idle) return;
            if (idx < 0 || idx >= _db.Rods.Count)
            {
                if (_log.ShouldLog("bad-rod-index")) Debug.LogError($"FishingRodController: rod index {idx} is outside the database ({_db.Rods.Count} rods); ignored.", this);
                return;
            }

            Machine.SetRod(_db.Rods[idx]);
            Rod = _db.Rods[idx];
            _appliedRodIndex = idx;
        }

        // ------------------------------------------------------------------ event handlers

        // Both arrive from deferred RPCs in EarlyUpdate (N1), never inside Update.
        private void HandleCastResult(uint castId, bool valid, CastRejectReason reason, Vector3 landing, FishingRoll roll)
        {
            if (Machine == null) return;
            FishFightProfile profile = default;
            if (valid && !roll.Catch.IsJunk)
            {
                profile = _db.Profiles[_db.Species[roll.Catch.ItemIndex].FightProfileIndex];
            }

            // Never sends CancelRpc (BF-5): a host-rejected result has nothing to refund, and for an insane roll the CastResultRpc handler
            // has already sent the single CancelRpc.
            if (!valid && Debug.isDebugBuild) Debug.Log($"[Rod] cast {castId} rejected by the host: {reason}");
            Machine.OnCastResult(castId, valid, in roll, in profile);
            ProcessEffects();
        }

        private void HandleCommit(uint castId, bool accepted, FightOutcome outcome, EscapeReason reason)
        {
            if (Machine == null) return;
            Machine.OnCommit(castId, accepted, outcome, reason);
            ProcessEffects();
        }

        // Externally forced stows (M5 carry, a script): the line cannot stay out with the rod away.
        private void HandleToolChanged(ToolKind tool)
        {
            if (Machine == null || tool == ToolKind.Rod || Machine.State == RodState.Idle) return;
            Machine.ForceEnd(RodForceReason.Stow);
            ProcessEffects();
        }

        // Raised from locomotion Update or TeleportRpc, never from this class, so it is safe to run inside the event.
        private void HandleTeleported()
        {
            if (Machine == null) return;
            Machine.ForceEnd(RodForceReason.Teleport);
            ProcessEffects();
        }

        // ------------------------------------------------------------------ frame, marker

        private RodFrame BuildFrame(in PlayerIntent intent, bool paused)
        {
            var f = new RodFrame { Paused = paused, InWater = _locomotion.IsSwimming };
            if (RodOut && !paused)
            {
                f.PrimaryPressed = intent.PrimaryPressed; f.PrimaryReleased = intent.PrimaryReleased; f.PrimaryHeld = intent.PrimaryHeld;
                f.SecondaryPressed = intent.SecondaryPressed; f.SecondaryReleased = intent.SecondaryReleased; f.SecondaryHeld = intent.SecondaryHeld;
            }

            f.MarkerValid = UpdateMarker();   // Ready/Charging only; false otherwise
            Vector3 root = transform.position;
            f.PlayerToBobber = _hasLanding ? Flat(root, _landing) : 0f;
            f.TipToBobber = _hasLanding ? Flat(RodTipPosition, _landing) : 0f;
            f.HookDisplacement = Machine.State == RodState.Hooked ? Flat(root, _hookRoot) : 0f;
            return f;
        }

        // Spec 9.3: the landing the cast would make right now, probed every frame. On a disagreement the host wins (recall).
        private bool UpdateMarker()
        {
            MarkerVisible = Machine.MarkerOn && RodOut;
            if (!MarkerVisible)
            {
                MarkerValid = false;
                return false;
            }

            float charge = Machine.State == RodState.Charging ? Machine.ChargeNorm : 0f;
            float dist = CastMath.Distance(charge, _tuning.CastMinDist, Rod.CastMaxDist);
            Vector3 root = transform.position;
            CastMath.Landing(root.x, root.z, transform.eulerAngles.y, dist, out float lx, out float lz);
            WaterProbe probe = CastProbe.Probe(lx, lz);
            MarkerValid = CastMath.IsValidLanding(in probe, _tuning.MinLandDepth);
            MarkerPosition = new Vector3(lx, probe.FirstHitIsWater ? probe.WaterY : WaterSurface.HeightAt(lx, lz), lz);
            return MarkerValid;
        }

        // ------------------------------------------------------------------ effects

        // Drains into a scratch list first: an effect may run a host handler synchronously on a host-owned rod (N2).
        // Replies to the owner are deferred (N1), so no machine call re-enters from inside this loop; the guard is insurance.
        private void ProcessEffects()
        {
            if (_draining)
            {
                _redrain = true;
                return;
            }

            _draining = true;
            try
            {
                do
                {
                    _redrain = false;
                    _effects.Clear();
                    Machine.DrainEffects(_effects);
                    for (int i = 0; i < _effects.Count; i++)
                    {
                        RodEffect e = _effects[i];
                        switch (e.Kind)
                        {
                            case RodEffectKind.Cue:
                                if (!_suppressSends)   // despawn path (OnDisable ForceEnd): no Recall/Escaped cue for a rod that is going away
                                    _net.RaiseLocalCue(e.Cue, e.Cue == RodCue.Snapped ? e.SizeClass : (byte)0);   // BF-1: the Snapped cue carries Roll.SizeClass
                                break;
                            case RodEffectKind.SendCastRequest:
                            {
                                Vector3 root = transform.position;
                                float yaw = transform.eulerAngles.y;
                                CastMath.Landing(root.x, root.z, yaw, Machine.CastDistance, out float lx, out float lz);
                                _landing = new Vector3(lx, WaterSurface.FlatHeight, lz);   // set before the RPC: solo handles it inline
                                _hasLanding = true;
                                _castYaw = yaw;
                                _recorder.OnCastRequested(_clock, Machine.Assist);
                                if (CanSend) _net.CastRequestRpc(e.CastId, root, yaw, e.ChargeNorm, _net.NetSelectedBait.Value, Machine.Assist);
                                break;
                            }
                            case RodEffectKind.SendCancel:
                                if (CanSend) _net.CancelRpc(e.CastId);
                                break;
                            case RodEffectKind.SendHooked:
                                if (CanSend) _net.HookedRpc(e.CastId);
                                break;
                            case RodEffectKind.SendResult:
                                if (CanSend)
                                {
                                    _net.ResultRpc(e.CastId, (byte)e.Outcome, (byte)e.Reason, e.Report.MaxTension, e.Report.FightSeconds,
                                                   (ushort)Mathf.Clamp(e.Report.NearSnapRecoveries, 0, ushort.MaxValue));
                                }
                                break;
                            case RodEffectKind.SendRodEvent:
                                if (CanSend) _net.RodEventRpc((byte)e.EventType, e.SizeClass);
                                break;
                            case RodEffectKind.StartFight:
                                _fightAccumulator = 0f;
                                _hookRoot = transform.position;
                                _sampler.Begin(_landing);
                                break;
                            case RodEffectKind.CommitTimedOut:
                                Debug.LogWarning($"FishingRodController: no CommitRpc for cast {e.CastId} within commitTimeout; returning to Idle.", this);
                                break;
                        }
                    }

                    ObserveState();   // logs, rumble off at Idle only (BF-2), telemetry capture, _hasLanding reset at Idle
                } while (_redrain);
            }
            finally
            {
                _draining = false;
            }
        }

        private bool CanSend => !_suppressSends && _net != null && _net.IsSpawned;

        // Runs at the end of every ProcessEffects, so it sees every state the machine settled in after one machine call.
        private void ObserveState()
        {
            RodState s = Machine.State;
            if (s != _lastState)
            {
                RodState from = _lastState;
                _lastState = s;
                if (Debug.isDebugBuild) Debug.Log($"[Rod] {from} -> {s} (cast {Machine.CastId})");
                OnStateEntered(from, s);
            }

            int ignored = Machine.IgnoredEventCount;
            if (ignored > _lastIgnored)
            {
                _lastIgnored = ignored;
                if (Debug.isDebugBuild && _log.ShouldLog("ignored-" + s)) Debug.Log($"[Rod] the machine ignored an event in {s} (cast {Machine.CastId}); count {ignored}");
            }
        }

        private void OnStateEntered(RodState from, RodState to)
        {
            switch (to)
            {
                case RodState.Waiting:
                    if (from == RodState.Casting) _recorder.OnWaiting(Machine.Roll, Machine.CastDistance);   // ResetCast will wipe the roll
                    break;
                case RodState.Bite:
                    _recorder.OnBite(_clock);
                    break;
                case RodState.Landed:
                case RodState.Snapped:
                case RodState.Escaped:
                {
                    FightSim fight = Machine.Fight;
                    float maxTension = 0f, seconds = 0f;
                    int near = 0;
                    if (fight != null)
                    {
                        maxTension = fight.State.MaxTension;
                        seconds = fight.State.Elapsed;
                        near = fight.State.NearSnapRecoveries;
                    }

                    _recorder.OnTerminal(Machine.Outcome, Machine.Reason, maxTension, seconds, near);
                    break;
                }
                case RodState.Idle:
                    _hasLanding = false;
                    _lastPoint = Vector3.zero;
                    _input.SetRumble(0f, 0f);   // spec 9.2: everything off at Idle (terminal pulses belong to FishingFeedback, BF-2)
                    FinishRecording();
                    break;
            }
        }

        private void FinishRecording()
        {
            if (!_recorder.ReachedWaiting) return;
            FishingRoll roll = _recorder.Roll;
            string profileId = "junk", itemId = "";
            if (roll.Catch.IsJunk)
            {
                if (roll.Catch.ItemIndex < _db.Junk.Count) itemId = _registry.JunkAt(roll.Catch.ItemIndex).Id;
            }
            else if (roll.Catch.ItemIndex < _db.Species.Count)
            {
                itemId = _registry.SpeciesAt(roll.Catch.ItemIndex).Id;
                profileId = _registry.FightProfileAt(_db.Species[roll.Catch.ItemIndex].FightProfileIndex).Id;
            }

            string rodId = _appliedRodIndex >= 0 ? _registry.RodAt((ushort)_appliedRodIndex).Id : "";
            if (!_recorder.TryFinish(_clock, profileId, itemId, rodId, _tuning.CleanCatchMaxTension, out FightEnded ended)) return;

            EventIsolation.Invoke(FightEnded, ended);
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out EventBus bus)) bus.Publish(ended);
        }

        // ------------------------------------------------------------------ published view

        private void PublishView()
        {
            RodState s = Machine.State;
            FightSim fight = Machine.Fight;
            byte flags = 0;
            float lateral = 0f, tension = 0f;
            if (fight != null)
            {
                ref readonly FightState fs = ref fight.State;
                lateral = fs.Lateral;
                tension = fs.Tension;
                if (s == RodState.Hooked)
                    flags |= RodControllerRules.HookedFlags(fs.Phase, fs.BurstDir, _lastFightInput.Reel, fight.Exhausted, fs.SnapTimer);   // FlagGrace: snap timer running
            }

            if (Machine.Recalling) flags |= RodWireQuant.FlagRecall;
            Vector3 bobber = RodControllerRules.FiniteOr(BobberPoint(s, fight, lateral), _lastPoint);   // never a NaN into the NetVar
            _lastPoint = bobber;
            uint castId = Machine.CastId;
            _net.OwnerWriteState(new RodStateData
            {
                State = (byte)s,
                Flags = flags,
                LateralQ = RodWireQuant.QuantizeLateral(lateral),
                TensionQ = RodWireQuant.QuantizeTension(tension),
                BobberPos = bobber,
                CastId = castId,
            });
            _net.OwnerSetView(new RodViewState
            {
                State = s,
                Flags = flags,
                Lateral = lateral,
                Tension = tension,
                BobberPos = bobber,
                CastId = castId,
                IsOwner = true,
                RodOut = RodOut,
            });
        }

        // Flat water point (no wave bob, so a resting Waiting rod does not dirty the NetVar).
        private Vector3 BobberPoint(RodState s, FightSim fight, float lateral)
        {
            switch (s)
            {
                case RodState.Casting:
                case RodState.Waiting:
                case RodState.Bite:
                    return _landing;
                case RodState.Hooked:
                    if (fight == null) return _lastPoint;
                    return _sampler.Tick(_lastDt, RodTipPosition.x, RodTipPosition.z, _castYaw, lateral, _tuning.LateralMaxDeg, fight.State.LineLength);
                case RodState.Landed:
                case RodState.Snapped:
                case RodState.Escaped:
                    return _lastPoint;
                default:
                    return Vector3.zero;   // Idle, Readying, Ready, Charging: no line
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool IsPaused()
        {
            ServiceRegistry game = ServiceRoots.Game;
            return game != null && game.TryGet(out IPauseState pause) && pause.Paused;
        }
    }
}
