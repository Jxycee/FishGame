// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.
// The visible rod (spec 10.5, C 7.24): six segments bend toward the line, the BENT visual tip is TipVisual (RodPresenter.Tip, ruling BF-3).
// RodPresenter writes only the Seg* transforms and TipVisual. It NEVER moves, rotates or re-parents TipAnchor (the controller reads it for the
// rod tip position, the fish point and TipToBobber, and the host slack is derived from its reach) and never SetActive()s anything: stowing the
// rod toggles the segment renderers only. Since R70 every NetTool change animates first (ToolStowAnim, 0.2 s): the chain swings tip-down and
// back about Seg0, then hides; equip reverses it. TipAnchor is still never touched. The rod's rest pose is the T19-fix's (PlayerFishingRigBuilder); this adds bend on top of it.
// The Seg* transforms are siblings under Rod at 0.3 m steps, so the chain below works directly in Rod-local space.
// Idle life (playtest F6, 2026-10-13; maths in Core RodIdleMath, all view-only): a slow breath, a spring-damped lag of the tip behind the rod's own
// movement and the look, and a slight sag while the line is out. All of it fades out as the strain bend grows (S12: a near-snap reads from the
// rod alone), breath and lag are zero at camera motion 0 (spec 9.11 removes bob), and the stow fades them out. The sag is a pose and stays.
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    // Order 250 (F7): after PlayerBody (200) has posed the arm and BobberView (240), before LineView (260), which draws from Tip.
    [DefaultExecutionOrder(250)]
    public sealed class RodPresenter : MonoBehaviour
    {
        private const int SegmentCount = PlayerRigPaths.RodSegmentCount;
        private const float MaxBendDeg = 50f;
        private const float NibbleSeconds = 0.3f, NibbleDeg = 4f, NibbleHz = 40f;
        private const float HookSetSeconds = 0.25f, HookSetDeg = 15f;
        private const float ShakeDeg = 1.5f, ShakeHz = 25f;
        private const float StowPitchDeg = 70f;    // [tune] the tip swings down this far when fully stowed (about the rod's local X)
        private const float StowPullBack = 0.3f;   // [tune] metres the rod slides back along its own axis (-Z) when fully stowed
        private const float SagEaseRate = 4f;      // [tune] F6: the sag eases in and out at 1 - exp(-4 dt), about 0.7 s to settle

        // Most bend at the tip (sum 1).
        private static readonly float[] Weights = { 0.05f, 0.10f, 0.15f, 0.20f, 0.22f, 0.28f };

        private FishingRodNetwork _rod;
        private Transform _rodRoot, _anchor, _tip;
        private readonly Transform[] _segments = new Transform[SegmentCount];
        private readonly MeshRenderer[] _renderers = new MeshRenderer[SegmentCount];
        private readonly Vector3[] _restPosition = new Vector3[SegmentCount + 1];   // [SegmentCount] = TipVisual
        private readonly Quaternion[] _restRotation = new Quaternion[SegmentCount + 1];

        private bool _ready, _live, _shown;
        private ToolStowAnim _stow;
        private bool _stowStarted;
        private float _nibbleAt = -10f, _hookSetAt = -10f;

        // F6 idle life: the sag blend (0 = none, 1 = line out) and the two lag springs (position deg, velocity deg/s) about the rod's local X and Y.
        private float _sag01;
        private float _lagX, _lagVX, _lagY, _lagVY;
        private Vector3 _lastAnchor;
        private bool _haveLastAnchor;

        // The bent visual tip (ruling BF-3): LineView and BobberView read it.
        public Transform Tip => _tip;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            _rodRoot = transform.Find(PlayerRigPaths.Rod);
            _anchor = transform.Find(PlayerRigPaths.RodTipAnchor);   // read only
            _tip = transform.Find(PlayerRigPaths.RodTipVisual);
            _ready = _rodRoot != null && _anchor != null && _tip != null && _rod != null;
            if (!_ready) return;

            for (int i = 0; i < SegmentCount; i++)
            {
                _segments[i] = transform.Find(PlayerRigPaths.RodSegmentPrefix + i);
                if (_segments[i] == null)
                {
                    _ready = false;
                    return;
                }

                _restPosition[i] = _segments[i].localPosition;
                _restRotation[i] = _segments[i].localRotation;
                _renderers[i] = _segments[i].GetComponent<MeshRenderer>();
            }

            _restPosition[SegmentCount] = _tip.localPosition;
            _restRotation[SegmentCount] = _tip.localRotation;
            SetSegmentsVisible(false);   // shown once NetTool says Rod (the builder leaves the renderers enabled)
        }

        private void OnEnable()
        {
            if (_rod == null) return;
            _rod.Cue += HandleCue;
            _live = true;
            ResetIdle();
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null) _rod.Cue -= HandleCue;
            _nibbleAt = -10f;
            _hookSetAt = -10f;
            _stowStarted = false;
            ResetIdle();
            if (_ready) ApplyPose(0f, Vector3.right, 0f, 0f, 0f, 0f);   // leave the rest pose behind
        }

        // A fresh observation: no sag, no lag, no velocity from before (a re-enable or a teleport must not kick the springs).
        private void ResetIdle()
        {
            _sag01 = 0f;
            _lagX = _lagVX = _lagY = _lagVY = 0f;
            _haveLastAnchor = false;
        }

        // One-shots come only from Cue (F6); a cue after OnDisable is ignored.
        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || !isActiveAndEnabled) return;
            if (cue == RodCue.Nibble) _nibbleAt = Time.time;
            else if (cue == RodCue.HookSet) _hookSetAt = Time.time;
        }

        private void LateUpdate()
        {
            if (!_ready || !_rod.IsSpawned) return;

            // Visibility follows NetTool, polled: a late joiner's NetTool is synced on spawn, and the owner's change arrives the same way.
            bool wantShown = _rod.Player != null && _rod.Player.Tool == ToolKind.Rod;
            if (!_stowStarted)
            {
                _stow = ToolStowAnim.Create(wantShown);   // spawn / late join: no animation
                _stowStarted = true;
            }

            float stow = _stow.Tick(wantShown, Time.deltaTime);
            if (_stow.Visible != _shown) SetSegmentsVisible(_stow.Visible);

            RodViewState vs = _rod.ViewState;
            float now = Time.time;
            float dt = Time.deltaTime;
            float bend01 = 0f;
            bool shake = false;
            Vector3 axis = Vector3.right;
            float cameraMotion = CameraMotion();

            if (vs.State == RodState.Hooked)
            {
                bool grace = (vs.Flags & RodWireQuant.FlagGrace) != 0;
                // Motion 1: the bend is the no-HUD tension readout, so camera motion 0 must not remove it (spec 9.11 removes shake, FOV kicks, bob and rumble).
                TensionCueState cue = TensionCues.Evaluate(vs.Tension, grace, 1f, 1f);
                bend01 = cue.RodBend01;
                if (cue.Band >= TensionBand.Warn) shake = TensionCues.Evaluate(vs.Tension, grace, 1f, cameraMotion).Shake;
            }

            // F6 sag: with the line out the rod droops a little toward it, easing in and out; the strain bend takes over as the tension rises.
            // A pose, not a motion, so camera motion 0 keeps it.
            _sag01 = RodIdleMath.Ease(_sag01, IsLineOut(vs.State) ? 1f : 0f, SagEaseRate, dt);
            if (_sag01 < 0.002f) _sag01 = 0f;
            float bendDeg = RodIdleMath.TotalBendDeg(MaxBendDeg, bend01, _sag01);
            if (bendDeg > 0f) axis = BendAxis(vs.BobberPos);

            // F6 idle life: a slow breath plus a spring-damped lag of the tip behind the player's own movement and look, all faded out by the strain
            // bend (S12: a near-snap reads from the rod alone), by camera motion 0 and by the stow.
            float idleW = RodIdleMath.IdleWeight(bend01, cameraMotion, stow);
            TrackLag(dt);
            RodIdleMath.Breath(now, out float breathX, out float breathY);
            float idleX = (breathX + _lagX) * idleW;
            float idleY = (breathY + _lagY) * idleW;

            // Extra rotation about the rod's local X (negative = tip up): the Nibble wobble and the HookSet jerk.
            float extraX = idleX;
            float u = now - _nibbleAt;
            if (u >= 0f && u < NibbleSeconds) extraX += NibbleDeg * Mathf.Sin(NibbleHz * u) * (1f - u / NibbleSeconds);
            u = now - _hookSetAt;
            if (u >= 0f && u < HookSetSeconds)
            {
                float k = 1f - u / HookSetSeconds;
                extraX -= HookSetDeg * k * k;
            }

            float shakeX = 0f, shakeY = idleY;
            if (shake)
            {
                shakeX = (Mathf.PerlinNoise(now * ShakeHz, 0.31f) - 0.5f) * 2f * ShakeDeg;
                shakeY += (Mathf.PerlinNoise(now * ShakeHz, 7.73f) - 0.5f) * 2f * ShakeDeg;
            }

            ApplyPose(bendDeg, axis, extraX + shakeX, shakeY, 0f, stow);
        }

        // The rod's own velocity in Rod space (from the STATIC TipAnchor, which never moves with the pose, so there is no feedback loop; the arm
        // follows the look, so a turn or a pitch moves it too, on every peer, with no net data) drives two damped springs: moving up trails the tip
        // down, moving right trails it left. A teleport-sized step (respawn, ladder snap) is dropped by RodIdleMath.SanitizeSpeed.
        private void TrackLag(float dt)
        {
            Vector3 p = _anchor.position;
            float vx = 0f, vy = 0f;
            if (_haveLastAnchor && dt > 1e-5f)
            {
                Vector3 local = _rodRoot.InverseTransformDirection((p - _lastAnchor) / dt);
                vx = local.x;
                vy = local.y;
            }

            _lastAnchor = p;
            _haveLastAnchor = true;
            RodIdleMath.SpringStep(ref _lagX, ref _lagVX, RodIdleMath.LagXDeg(vy), dt);
            RodIdleMath.SpringStep(ref _lagY, ref _lagVY, RodIdleMath.LagYDeg(vx), dt);
        }

        private static bool IsLineOut(RodState state)
        {
            return state == RodState.Casting || state == RodState.Waiting || state == RodState.Bite || state == RodState.Hooked || state == RodState.Escaped;
        }

        // cross(rodForward, toBobber) in Rod space, from the STATIC anchor (the bend never depends on its own previous result).
        // Local X when the line is straight along the rod or no usable point exists.
        private Vector3 BendAxis(Vector3 bobberWorld)
        {
            Vector3 to = bobberWorld - _anchor.position;
            if (float.IsNaN(to.x) || float.IsNaN(to.y) || float.IsNaN(to.z) || float.IsInfinity(to.x) || float.IsInfinity(to.y) || float.IsInfinity(to.z))
                return Vector3.right;
            Vector3 cross = Vector3.Cross(Vector3.forward, _rodRoot.InverseTransformDirection(to));
            return cross.sqrMagnitude > 1e-6f ? cross.normalized : Vector3.right;
        }

        // The chain: link i is the rest pivot-to-pivot offset, segment i turns by the accumulated weighted angle a_i,
        // pos_{i+1} = pos_i + R_i * d_i, localRot_i = R_i * restRot_i; TipVisual (the end of the last link) gets the final position.
        // stow01 > 0 then moves the whole chain rigidly: rotate StowPitchDeg * stow01 about Rod-local X through Seg0's rest pivot and slide
        // StowPullBack * stow01 along -Z. At stow01 = 0 the pose is exactly the old one. Written every frame, so the rest pose is restored exactly.
        private void ApplyPose(float bendDeg, Vector3 axis, float extraXDeg, float extraYDeg, float extraZDeg, float stow01)
        {
            if (_tip == null) return;   // torn down with the player
            bool stowing = stow01 > 0f;
            Quaternion stowRot = stowing ? Quaternion.AngleAxis(StowPitchDeg * stow01, Vector3.right) : Quaternion.identity;
            Vector3 pivot = _restPosition[0];
            Vector3 shift = stowing ? Vector3.back * (StowPullBack * stow01) : Vector3.zero;

            Vector3 position = _restPosition[0];
            float accBend = 0f, accX = 0f, accY = 0f, accZ = 0f;
            Quaternion r = Quaternion.identity;
            for (int i = 0; i < SegmentCount; i++)
            {
                if (_segments[i] == null) return;
                accBend += bendDeg * Weights[i];
                accX += extraXDeg * Weights[i];
                accY += extraYDeg * Weights[i];
                accZ += extraZDeg * Weights[i];
                r = Quaternion.AngleAxis(accBend, axis) * Quaternion.Euler(accX, accY, accZ);

                _segments[i].localPosition = stowing ? pivot + stowRot * (position - pivot) + shift : position;
                _segments[i].localRotation = stowing ? stowRot * r * _restRotation[i] : r * _restRotation[i];
                position += r * (_restPosition[i + 1] - _restPosition[i]);
            }

            _tip.localPosition = stowing ? pivot + stowRot * (position - pivot) + shift : position;
            _tip.localRotation = stowing ? stowRot * r * _restRotation[SegmentCount] : r * _restRotation[SegmentCount];
        }

        private void SetSegmentsVisible(bool visible)
        {
            _shown = visible;
            for (int i = 0; i < SegmentCount; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = visible;   // renderers only: TipAnchor must stay valid for the controller
            }
        }

        private static float CameraMotion()
        {
            ISettings settings = FeedbackServices.Get<ISettings>();
            return settings != null && settings.Current != null ? Mathf.Clamp01(settings.Current.cameraMotion) : 1f;
        }
    }
}
