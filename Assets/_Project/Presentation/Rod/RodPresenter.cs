// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.
// The visible rod (spec 10.5, C 7.24): six segments bend toward the line, the BENT visual tip is TipVisual (RodPresenter.Tip, ruling BF-3).
// RodPresenter writes only the Seg* transforms and TipVisual. It NEVER moves, rotates or re-parents TipAnchor (the controller reads it for the
// rod tip position, the fish point and TipToBobber, and the host slack is derived from its reach) and never SetActive()s anything: stowing the
// rod toggles the segment renderers only. The rod's rest pose is the T19-fix's (PlayerFishingRigBuilder); this adds bend on top of it.
// The Seg* transforms are siblings under Rod at 0.3 m steps, so the chain below works directly in Rod-local space.
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

        // Most bend at the tip (sum 1).
        private static readonly float[] Weights = { 0.05f, 0.10f, 0.15f, 0.20f, 0.22f, 0.28f };

        private FishingRodNetwork _rod;
        private Transform _rodRoot, _anchor, _tip;
        private readonly Transform[] _segments = new Transform[SegmentCount];
        private readonly MeshRenderer[] _renderers = new MeshRenderer[SegmentCount];
        private readonly Vector3[] _restPosition = new Vector3[SegmentCount + 1];   // [SegmentCount] = TipVisual
        private readonly Quaternion[] _restRotation = new Quaternion[SegmentCount + 1];

        private bool _ready, _live, _shown;
        private float _nibbleAt = -10f, _hookSetAt = -10f;

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
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null) _rod.Cue -= HandleCue;
            _nibbleAt = -10f;
            _hookSetAt = -10f;
            if (_ready) ApplyPose(0f, Vector3.right, 0f, 0f, 0f);   // leave the rest pose behind
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
            if (wantShown != _shown) SetSegmentsVisible(wantShown);

            RodViewState vs = _rod.ViewState;
            float now = Time.time;
            float bendDeg = 0f;
            bool shake = false;
            Vector3 axis = Vector3.right;

            if (vs.State == RodState.Hooked)
            {
                bool grace = (vs.Flags & RodWireQuant.FlagGrace) != 0;
                // Motion 1: the bend is the no-HUD tension readout, so camera motion 0 must not remove it (spec 9.11 removes shake, FOV kicks, bob and rumble).
                TensionCueState cue = TensionCues.Evaluate(vs.Tension, grace, 1f, 1f);
                bendDeg = MaxBendDeg * cue.RodBend01;
                if (cue.Band >= TensionBand.Warn) shake = TensionCues.Evaluate(vs.Tension, grace, 1f, CameraMotion()).Shake;
                if (bendDeg > 0f) axis = BendAxis(vs.BobberPos);
            }

            // Extra rotation about the rod's local X (negative = tip up): the Nibble wobble and the HookSet jerk.
            float extraX = 0f;
            float u = now - _nibbleAt;
            if (u >= 0f && u < NibbleSeconds) extraX += NibbleDeg * Mathf.Sin(NibbleHz * u) * (1f - u / NibbleSeconds);
            u = now - _hookSetAt;
            if (u >= 0f && u < HookSetSeconds)
            {
                float k = 1f - u / HookSetSeconds;
                extraX -= HookSetDeg * k * k;
            }

            float shakeX = 0f, shakeY = 0f;
            if (shake)
            {
                shakeX = (Mathf.PerlinNoise(now * ShakeHz, 0.31f) - 0.5f) * 2f * ShakeDeg;
                shakeY = (Mathf.PerlinNoise(now * ShakeHz, 7.73f) - 0.5f) * 2f * ShakeDeg;
            }

            ApplyPose(bendDeg, axis, extraX + shakeX, shakeY, 0f);
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
        // Written every frame, including at zero bend, so the rest pose is restored exactly.
        private void ApplyPose(float bendDeg, Vector3 axis, float extraXDeg, float extraYDeg, float extraZDeg)
        {
            if (_tip == null) return;   // torn down with the player
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

                _segments[i].localPosition = position;
                _segments[i].localRotation = r * _restRotation[i];
                position += r * (_restPosition[i + 1] - _restPosition[i]);
            }

            _tip.localPosition = position;
            _tip.localRotation = r * _restRotation[SegmentCount];
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
