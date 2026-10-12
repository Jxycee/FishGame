// Authority: local only. On PlayerFP; reads its FishingRodNetwork (Cue, ViewState, Announced). Never writes gameplay state (C 2.8 #17).
// Spec 9.11 feel. One-shots (sounds, ripples, splashes, kicks, captions, pulses) come only from Cue and Announced; loops, reel ticks and the
// tension-driven trauma and rumble come from polling ViewState every frame, so a late joiner is right with no replayed bursts (F6).
// Every peer: sounds, ripples, ducking, loops, the snap silhouette, the Line_Crack. Owner only: camera trauma and FOV kicks (passed raw:
// PlayerCamera scales them by cameraMotion itself), rumble (scaled here by cameraMotion), captions (settings.captions).
// cameraMotion limits camera motion only (shake, FOV kicks, bob, rumble): the snap silhouette jump, splash and ripple are world feedback seen
// by everyone and carry the fish size, so they are never scaled or hidden by it (spec 9.11).
// Grace (spec 9.11, tension >= 1.0): the owner's PublishView sets RodWireQuant.FlagGrace while the snap timer runs; every peer polls the flag
// (remote Tension is smoothed and may never reach 1.0) and plays Line_Crack on its rising edge.
// Sound placement: rod-side one-shots are 2D on the owner (Play2D) and positional on remotes; cues that need a pitch (ChargeStart,
// CastRelease) use the positional overload everywhere, because Play2D has no pitch overload (ruling C-Q8) and the owner's rod tip is at the ear.
// No coroutines: every delayed effect is a timer ticked in Update (the snap jump is posed in LateUpdate), so disabling the component cancels them all. Once OnDisable has run
// (or !isActiveAndEnabled) Cue and Announced are ignored, all loops are stopped, and the owner's rumble is zeroed.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    // Lazy service lookup shared by the feedback components. A missing or torn-down service is a silent null.
    internal static class FeedbackServices
    {
        public static T Get<T>() where T : class
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out T service)) return service;
            return null;
        }
    }

    public sealed class FishingFeedback : MonoBehaviour
    {
        private const int PulseCapacity = 8;
        private const float StrainCaptionGap = 3f;
        private const float MaxReelClicksPerSecond = 30f;
        private const float SnapJumpSeconds = 0.7f;
        private const string AnchorName = "FeedbackAnchor", SilhouetteName = "SnapSilhouette";

        // HookSet is four clicks. Each next click is scheduled from the frame the previous one FIRED (not from the first click), 0.055 s later:
        // AudioBus rate-limits Reel_Click to 0.05 s against the last click that played, so clicks anchored to the first one drop at frame rates
        // where two of them land less than 0.05 s apart. Coupling: the timers run on Time.time but AudioBus.AllowPlay's limiter uses
        // Time.unscaledTime; the gap is only equivalent while Time.timeScale is 1 (it is never scaled in this game).
        private const float ReelClickGap = 0.055f;
        private const byte HookSetClicksAfterFirst = 3;

        private enum TimerKind : byte { BiteFollowRipple, ReelClick }

        private struct Timer
        {
            public float At;
            public TimerKind Kind;
            public byte ClicksAfter;   // ReelClick: clicks still to schedule after this one
        }

        private struct Pulse
        {
            public float Start, End, Low, High;
        }

        [SerializeField] private Material silhouetteMaterial;

        private FishingRodNetwork _rod;
        private PlayerCamera _cam;
        private FishingRodController _controller;
        private Transform _rodTip, _anchor, _silhouette;
        private MeshRenderer _silhouetteRenderer;

        private bool _live;
        private bool _ownerSeen;

        private AudioBus _loopBus;
        private AudioLoop _creak, _thrum;
        private float _reelAcc;
        private float _lastStrainCaption = float.NegativeInfinity;
        private bool _wasHooked;
        private bool _polled, _wasGrace;

        private readonly List<Timer> _timers = new List<Timer>(8);
        private readonly List<Pulse> _pulses = new List<Pulse>(8);
        private float _lastLow, _lastHigh;

        private bool _snapActive;
        private float _snapStart, _snapHeight, _snapLength;
        private Vector3 _snapOrigin;
        private Quaternion _snapFacing;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            _cam = GetComponent<PlayerCamera>();
            _controller = GetComponent<FishingRodController>();
            _rodTip = transform.Find(PlayerRigPaths.RodTipAnchor);

            _anchor = new GameObject(AnchorName).transform;
            _anchor.SetParent(transform, false);

            var silhouette = new GameObject(SilhouetteName);
            _silhouette = silhouette.transform;
            _silhouette.SetParent(transform, false);
            silhouette.AddComponent<MeshFilter>().sharedMesh = FishFallbackMesh.Silhouette();
            _silhouetteRenderer = silhouette.AddComponent<MeshRenderer>();
            _silhouetteRenderer.sharedMaterial = silhouetteMaterial;
            _silhouetteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _silhouetteRenderer.receiveShadows = false;
            silhouette.SetActive(false);
        }

        private void OnEnable()
        {
            if (_rod == null) return;
            _rod.Cue += HandleCue;
            _rod.Announced += HandleAnnounced;
            _live = true;
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null)
            {
                _rod.Cue -= HandleCue;
                _rod.Announced -= HandleAnnounced;
            }

            StopLoops();
            _timers.Clear();
            _pulses.Clear();
            _reelAcc = 0f;
            _wasHooked = false;
            _polled = false;   // re-seed the grace edge on the first poll after the next enable (no replayed crack, F6)
            _wasGrace = false;
            _snapActive = false;
            if (_silhouette != null) _silhouette.gameObject.SetActive(false);

            if (_ownerSeen)
            {
                IInputService input = FeedbackServices.Get<IInputService>();
                if (input != null) input.SetRumble(0f, 0f);
                _lastLow = 0f;
                _lastHigh = 0f;
            }
        }

        private bool IsOwner => _rod != null && _rod.IsSpawned && _rod.IsOwner;

        // ------------------------------------------------------------------ cues (one-shots)

        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || !isActiveAndEnabled) return;

            bool owner = IsOwner;
            RodViewState vs = _rod.ViewState;
            Vector3 rodPos = RodPosition();
            Vector3 bobberPos = vs.BobberPos;
            AudioBus bus = FeedbackServices.Get<AudioBus>();
            string sfx = RodCueAudio.SfxFor(cue);
            float now = Time.time;

            switch (cue)
            {
                case RodCue.Equip:
                case RodCue.Stow:
                case RodCue.Ready:
                case RodCue.InvalidRelease:
                case RodCue.Recall:
                    PlayRod(bus, sfx, rodPos, owner);
                    break;

                case RodCue.ChargeStart:
                    if (owner && _controller != null && bus != null && sfx != null) bus.PlayAt(sfx, rodPos, 0.8f);
                    break;

                case RodCue.CastRelease:
                {
                    float pitch = 1f;
                    if (owner && _controller != null && _controller.Machine != null)
                        pitch = Mathf.Lerp(0.9f, 1.3f, Mathf.Clamp01(_controller.Machine.ChargeNorm));
                    if (bus != null && sfx != null) bus.PlayAt(sfx, rodPos, pitch);
                    if (owner)
                    {
                        if (_cam != null) _cam.AddFovKick(2f, 0.15f);
                        AddPulse(0.10f, 0.20f, 0.08f, 0f);
                    }
                    break;
                }

                case RodCue.Landing:
                    PlayAtPoint(bus, sfx, bobberPos);
                    SpawnRipple(bobberPos, RippleMath.CastLanding);
                    if (owner) AddPulse(0.10f, 0.20f, 0.08f, 0f);
                    break;

                case RodCue.Nibble:
                    PlayAtPoint(bus, sfx, bobberPos);
                    SpawnRipple(bobberPos, RippleMath.Nibble);
                    if (owner) AddPulse(0f, 0.15f, 0.05f, 0f);
                    break;

                case RodCue.Bite:
                    PlayAtPoint(bus, sfx, bobberPos);
                    if (owner && bus != null && sfx != null) bus.Play2D(sfx, AudioCategory.Sfx);   // spec 9.4: the owner always hears the bite
                    if (bus != null && RodCueAudio.IsStinger(cue)) bus.Duck(1f);
                    SpawnRipple(bobberPos, RippleMath.Bite);
                    AddTimer(now + 0.25f, TimerKind.BiteFollowRipple);
                    AddTimer(now + 0.5f, TimerKind.BiteFollowRipple);
                    if (owner)
                    {
                        AddTrauma(0.15f);
                        AddPulse(0.80f, 0.80f, 0.25f, 0f);
                        Caption("cap.bite");
                    }
                    break;

                case RodCue.HookSet:
                    PlayRod(bus, sfx, rodPos, owner);   // click 1 of 4
                    AddTimer(now + ReelClickGap, TimerKind.ReelClick, HookSetClicksAfterFirst - 1);
                    if (owner)
                    {
                        AddTrauma(0.10f);
                        AddPulse(0.40f, 0.50f, 0.15f, 0f);
                    }
                    break;

                case RodCue.SideRun:
                    PlayAtPoint(bus, sfx, bobberPos);
                    if (owner) AddPulse(0.30f, 0f, 0.30f, 0f);
                    break;

                case RodCue.Dive:
                    break;   // Dive's sfx id is the Dive_Thrum LOOP id: the loop starts by polling, never as a one-shot

                case RodCue.Exhausted:
                    PlayAtPoint(bus, sfx, bobberPos);
                    break;

                case RodCue.Escaped:
                    PlayAtPoint(bus, sfx, bobberPos);
                    if (owner)
                    {
                        AddPulse(0.20f, 0.20f, 0.10f, 0f);
                        if ((vs.Flags & RodWireQuant.FlagRecall) == 0) Caption("cap.escaped");
                    }
                    break;

                case RodCue.Snapped:
                    PlayRod(bus, sfx, rodPos, owner);
                    if (bus != null && RodCueAudio.IsStinger(cue)) bus.Duck(1f);
                    SpawnRipple(bobberPos, RippleMath.Snap);
                    StartSnapJump(bobberPos, arg);
                    if (owner)
                    {
                        AddTrauma(0.5f);
                        AddPulse(1.00f, 1.00f, 0.30f, 0f);
                        Caption("cap.snapped");
                    }
                    break;

                case RodCue.LineLanded:
                    PlayRod(bus, sfx, rodPos, owner);
                    SpawnRipple(bobberPos, RippleMath.Landed);
                    break;

                default:
                    break;   // None, Calm
            }
        }

        private void HandleAnnounced(CatchAnnouncement a)
        {
            if (!_live || !isActiveAndEnabled) return;

            if (a.Kind != AnnounceKind.Landed && a.Kind != AnnounceKind.ReleasedCreelFull) return;   // Lost, PityCast: the HUD toast covers them

            AudioBus bus = FeedbackServices.Get<AudioBus>();
            Vector3 pos = RodPosition();
            if (bus != null)
            {
                bus.PlayAt(SfxIds.CatchFlop, pos);
                if (a.Kind == AnnounceKind.Landed) bus.PlayAt(SfxIds.CatchCheer, pos);
            }

            if (a.Kind == AnnounceKind.Landed && IsOwner)
            {
                if (_cam != null) _cam.AddFovKick(-3f, 0.3f);
                AddPulse(0.5f, 0.5f, 0.1f, 0f);
                AddPulse(0.5f, 0.5f, 0.1f, 0.2f);   // 0.1 s pulse, 0.1 s gap, 0.1 s pulse
            }
        }

        // ------------------------------------------------------------------ polling (state-driven)

        private void Update()
        {
            if (!_live || _rod == null) return;

            float now = Time.time;
            float dt = Time.deltaTime;
            RodViewState vs = _rod.ViewState;
            bool owner = IsOwner;
            if (owner) _ownerSeen = true;
            bool hooked = vs.State == RodState.Hooked;

            TensionCueState cue = default;
            if (hooked)
            {
                float reelSpeed = ReelSpeed();
                cue = TensionCues.Evaluate(vs.Tension, (vs.Flags & RodWireQuant.FlagGrace) != 0, reelSpeed, 1f);   // motion 1: PlayerCamera scales trauma itself
            }

            UpdateGrace(vs, hooked, owner);

            RunTimers(now, vs, owner);
            UpdateLoops(vs, hooked, owner, in cue);
            UpdateReelTicks(vs, hooked, owner, in cue, dt);

            if (owner)
            {
                if (hooked)
                {
                    if (cue.TraumaPerSecond > 0f) AddTrauma(cue.TraumaPerSecond * dt);
                    if (cue.Band >= TensionBand.Warn && now - _lastStrainCaption >= StrainCaptionGap)
                    {
                        _lastStrainCaption = now;
                        Caption("cap.straining");
                    }
                }
                UpdateRumble(now, vs, hooked, in cue);
            }

            if (_wasHooked && !hooked) _lastStrainCaption = float.NegativeInfinity;   // next fight starts fresh
            _wasHooked = hooked;
        }

        // The silhouette is a child of the moving player root, so its world pose is written after every Update (PlayerLocomotion's order
        // against this component's Update is undefined).
        private void LateUpdate()
        {
            if (!_live || _rod == null || _anchor == null) return;
            _anchor.position = _rod.ViewState.BobberPos;
            UpdateSnapJump(Time.time);
        }

        // Rising edge of FlagGrace, polled from the view on every peer. The first poll after enabling only seeds the edge, so a late joiner
        // (or a re-enable) mid-grace hears nothing replayed. The Line_Crack rate limit (AudioBus minInterval) bounds repeated touches of 1.0.
        private void UpdateGrace(in RodViewState vs, bool hooked, bool owner)
        {
            bool grace = hooked && (vs.Flags & RodWireQuant.FlagGrace) != 0;
            bool rising = _polled && grace && !_wasGrace;
            _polled = true;
            _wasGrace = grace;
            if (rising) PlayRod(FeedbackServices.Get<AudioBus>(), SfxIds.LineCrack, RodPosition(), owner);
        }

        private void UpdateLoops(in RodViewState vs, bool hooked, bool owner, in TensionCueState cue)
        {
            bool creak = hooked && cue.Band >= TensionBand.Creak;
            bool thrum = hooked && (vs.Flags & RodWireQuant.FlagDive) != 0;
            if (!creak && !thrum && _creak == null && _thrum == null) return;

            AudioBus bus = FeedbackServices.Get<AudioBus>();
            if (bus == null)
            {
                _creak = null;   // the bus is gone; its loops went with it
                _thrum = null;
                return;
            }

            if (creak)
            {
                if (_creak == null) _creak = StartLoop(bus, SfxIds.LineCreak, _rodTip, !owner);
                if (_creak != null) bus.SetLoop(_creak, cue.CreakVolume01, cue.CreakPitch);
            }
            else if (_creak != null)
            {
                StopLoop(ref _creak);
            }

            if (thrum)
            {
                if (_thrum == null) _thrum = StartLoop(bus, SfxIds.DiveThrum, _anchor, !owner);
                if (_thrum != null) bus.SetLoop(_thrum, 1f, 1f);
            }
            else if (_thrum != null)
            {
                StopLoop(ref _thrum);
            }
        }

        // Every loop follows a transform (the owner's 2D loops too): a null-follow loop would stay at the bus forever after a scene change.
        private AudioLoop StartLoop(AudioBus bus, string id, Transform follow, bool spatial)
        {
            if (follow == null) return null;
            _loopBus = bus;
            return bus.StartLoop(id, follow, spatial);
        }

        private void StopLoop(ref AudioLoop loop)
        {
            if (loop == null) return;
            if (_loopBus != null) _loopBus.StopLoop(loop);
            loop = null;
        }

        private void StopLoops()
        {
            StopLoop(ref _creak);
            StopLoop(ref _thrum);
        }

        private void UpdateReelTicks(in RodViewState vs, bool hooked, bool owner, in TensionCueState cue, float dt)
        {
            if (!hooked || (vs.Flags & RodWireQuant.FlagReel) == 0)
            {
                _reelAcc = 0f;
                return;
            }

            _reelAcc += Mathf.Min(cue.ReelClickRate, MaxReelClicksPerSecond) * dt;
            if (_reelAcc < 1f) return;
            _reelAcc -= 1f;
            if (_reelAcc > 1f) _reelAcc = 1f;   // at most one tick per frame

            AudioBus bus = FeedbackServices.Get<AudioBus>();
            if (bus == null) return;
            if (owner)
            {
                bus.Play2D(SfxIds.ReelClick, AudioCategory.Sfx);   // no pitch overload in 2D (ruling C-Q8)
            }
            else
            {
                float pitch = 1f + 0.25f * Mathf.Clamp01((vs.Tension - 0.3f) / 0.3f);
                bus.PlayAt(SfxIds.ReelClick, RodPosition(), pitch);
            }
        }

        // ------------------------------------------------------------------ timers and pulses

        private void AddTimer(float at, TimerKind kind, byte clicksAfter = 0)
        {
            _timers.Add(new Timer { At = at, Kind = kind, ClicksAfter = clicksAfter });
        }

        private void RunTimers(float now, in RodViewState vs, bool owner)
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                Timer t = _timers[i];
                if (now < t.At) continue;
                _timers.RemoveAt(i);
                switch (t.Kind)
                {
                    case TimerKind.BiteFollowRipple:
                        SpawnRipple(vs.BobberPos, RippleMath.BiteFollow);
                        break;
                    case TimerKind.ReelClick:
                        PlayRod(FeedbackServices.Get<AudioBus>(), SfxIds.ReelClick, RodPosition(), owner);
                        if (t.ClicksAfter > 0) AddTimer(now + ReelClickGap, TimerKind.ReelClick, (byte)(t.ClicksAfter - 1));   // chained from this frame
                        break;
                }
            }
        }

        private void AddPulse(float low, float high, float seconds, float delay)
        {
            if (_pulses.Count >= PulseCapacity) return;
            float start = Time.time + delay;
            _pulses.Add(new Pulse { Start = start, End = start + seconds, Low = low, High = high });
        }

        // Target per motor = max(continuous, active pulse), times cameraMotion. Terminal states (and Idle) have no continuous part, but
        // their one-shot pulse plays to completion (BF-2). Idle, lost focus and disable cancel everything at once.
        private void UpdateRumble(float now, in RodViewState vs, bool hooked, in TensionCueState cue)
        {
            IInputService input = FeedbackServices.Get<IInputService>();
            if (input == null) return;

            float low = 0f, high = 0f;
            bool idle = vs.State == RodState.Idle;
            if (idle || !input.HasFocus)
            {
                _pulses.Clear();
            }
            else
            {
                if (hooked)
                {
                    low = cue.RumbleLow;
                    high = cue.RumbleHigh;
                    if ((vs.Flags & RodWireQuant.FlagDive) != 0)
                    {
                        low = Mathf.Max(low, 0.6f);
                        high = Mathf.Max(high, 0.1f);
                    }
                }

                for (int i = _pulses.Count - 1; i >= 0; i--)
                {
                    Pulse p = _pulses[i];
                    if (now >= p.End) { _pulses.RemoveAt(i); continue; }
                    if (now < p.Start) continue;
                    low = Mathf.Max(low, p.Low);
                    high = Mathf.Max(high, p.High);
                }

                float motion = CameraMotion();
                low *= motion;
                high *= motion;
            }

            bool changed = Mathf.Abs(low - _lastLow) > 0.01f || Mathf.Abs(high - _lastHigh) > 0.01f;
            bool becameZero = (low == 0f && _lastLow != 0f) || (high == 0f && _lastHigh != 0f);
            if (!changed && !becameZero) return;
            _lastLow = low;
            _lastHigh = high;
            input.SetRumble(low, high);
        }

        // ------------------------------------------------------------------ snap silhouette

        private void StartSnapJump(Vector3 bobberPos, byte sizeClass)
        {
            int size = Mathf.Clamp(sizeClass, 0, 2);
            _snapLength = size == 0 ? 0.25f : (size == 1 ? 0.4f : 0.6f);
            _snapHeight = 0.6f + 0.4f * size;   // world feedback seen by everyone: not scaled by settings.cameraMotion (spec 9.11)
            float waterY = WaterSurface.HeightAt(bobberPos.x, bobberPos.z);
            _snapOrigin = new Vector3(bobberPos.x, waterY, bobberPos.z);
            _snapStart = Time.time;

            Vector3 away = _snapOrigin - RodPosition();
            away.y = 0f;
            _snapFacing = away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away.normalized, Vector3.up) : Quaternion.identity;
            _snapActive = true;
            if (_silhouette != null && _silhouetteRenderer != null && _silhouetteRenderer.sharedMaterial != null)
            {
                _silhouette.gameObject.SetActive(true);   // a second Snapped during a jump restarts it
                UpdateSnapJump(_snapStart);
            }
        }

        private void UpdateSnapJump(float now)
        {
            if (!_snapActive) return;
            float u = (now - _snapStart) / SnapJumpSeconds;
            if (u >= 1f)
            {
                _snapActive = false;
                if (_silhouette != null) _silhouette.gameObject.SetActive(false);
                AudioBus bus = FeedbackServices.Get<AudioBus>();
                if (bus != null) bus.PlayAt(SfxIds.SplashSmall, _snapOrigin);
                SpawnRipple(_snapOrigin, RippleMath.Snap);
                return;
            }

            if (_silhouette == null || !_silhouette.gameObject.activeSelf) return;
            float height = 4f * _snapHeight * u * (1f - u);   // parabola: up and back down in SnapJumpSeconds
            float pitch = Mathf.Lerp(-50f, 70f, u);           // nose up, then nose over
            _silhouette.SetPositionAndRotation(_snapOrigin + Vector3.up * height, _snapFacing * Quaternion.Euler(pitch, 0f, 0f));
            _silhouette.localScale = Vector3.one * _snapLength;
        }

        // ------------------------------------------------------------------ helpers

        private Vector3 RodPosition()
        {
            return _rodTip != null ? _rodTip.position : transform.position + Vector3.up * 1.5f;
        }

        private void PlayRod(AudioBus bus, string sfx, Vector3 rodPos, bool owner)
        {
            if (bus == null || sfx == null) return;
            if (owner) bus.Play2D(sfx, AudioCategory.Sfx);
            else bus.PlayAt(sfx, rodPos);
        }

        private static void PlayAtPoint(AudioBus bus, string sfx, Vector3 pos)
        {
            if (bus != null && sfx != null) bus.PlayAt(sfx, pos);
        }

        private static void SpawnRipple(Vector3 pos, float strength)
        {
            RippleField field = RippleField.Current;
            if (field != null) field.Spawn(pos.x, pos.z, strength);
        }

        private void AddTrauma(float amount)
        {
            if (_cam != null) _cam.AddTrauma(amount);
        }

        private static void Caption(string key)
        {
            ISettings settings = FeedbackServices.Get<ISettings>();
            if (settings == null || settings.Current == null || !settings.Current.captions) return;
            EventBus bus = FeedbackServices.Get<EventBus>();
            if (bus != null) bus.Publish(new CaptionRequested(key));
        }

        private static float CameraMotion()
        {
            ISettings settings = FeedbackServices.Get<ISettings>();
            return settings != null && settings.Current != null ? Mathf.Clamp01(settings.Current.cameraMotion) : 1f;
        }

        private float ReelSpeed()
        {
            ContentRegistry registry = FeedbackServices.Get<ContentRegistry>();
            if (registry == null) return 1f;
            ushort index = _rod.NetRodIndex.Value;
            if (index >= registry.Rods.Count) return 1f;
            RodDef rod = registry.RodAt(index);
            return rod != null ? rod.ReelSpeed : 1f;
        }
    }
}
