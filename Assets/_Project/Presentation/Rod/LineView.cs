// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.
// The fishing line (spec 10.5, 9.11, C 7.24): a 24-point quadratic Bezier from the BENT rod tip (RodPresenter.Tip = TipVisual, BF-3) to the bobber,
// sag (1 - tension) * 0.15 * length, drawn with the FishGame/Line shader through this player's own material instance.
//  - Visible while the bobber is shown (flight, Waiting, Bite, Hooked, the Escaped beat, the recall arc); hidden otherwise. On a Snapped cue the
//    line whips for 0.2 s (the end slides from where the bobber was to the tip, full sag), then hides.
//  - Shader inputs: _Tension (colour warms from 0.6, warn colour plus the moving dash from 0.8) and _DashCount = max(1, length / 0.3).
//  - Grace shimmer (spec 9.11 grace row): the Line shader shimmers at _Tension >= 1. A remote's tension is smoothed and never reaches 1.0, so the
//    shimmer is keyed off RodWireQuant.FlagGrace (the owner sets it while the snap timer runs), not off the tension: with the flag, _Tension is
//    pushed above 1; without it, _Tension is held just below 1. (The crack SOUND is FishingFeedback's.)
//  - Width: base 0.012 m; in the Warn or Grace band it pulses base * (1 + 0.5 * (0.5 + 0.5 * sin(2 pi 4 t))) (C 7.21: the pulse lives here).
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using UnityEngine;

namespace FishGame.Presentation
{
    // Order 260 (F7): after BobberView (240) and RodPresenter (250), whose results it draws between.
    [DefaultExecutionOrder(260)]
    public sealed class LineView : MonoBehaviour
    {
        private const int PointCount = 24;
        private const float BaseWidth = 0.012f;
        private const float PulseHz = 4f;
        private const float WhipSeconds = 0.2f;
        private const float SlackSagFactor = 0.25f;   // Escaped: "the line goes slack" (spec 9.11)
        private const float GraceTension = 1.05f;     // the Line shader's shimmer is step(1.0, _Tension); its range runs to 1.2
        private const float JustBelowGrace = 0.999f;
        private static readonly int TensionId = Shader.PropertyToID("_Tension");
        private static readonly int DashCountId = Shader.PropertyToID("_DashCount");

        private FishingRodNetwork _rod;
        private BobberView _bobber;
        private RodPresenter _presenter;
        private LineRenderer _line;
        private Material _material;   // this player's instance of M_Line
        private readonly Vector3[] _points = new Vector3[PointCount];

        private bool _live;
        private float _lastTension = -1f, _lastDash = -1f, _lastWidth = -1f;
        private bool _whipActive;
        private float _whipStart;
        private Vector3 _whipFrom;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            _bobber = GetComponent<BobberView>();
            _presenter = GetComponent<RodPresenter>();
            Transform t = transform.Find(PlayerRigPaths.Line);
            _line = t != null ? t.GetComponent<LineRenderer>() : null;
            if (_line == null) return;

            _line.positionCount = PointCount;
            _line.useWorldSpace = true;
            _line.textureMode = LineTextureMode.Stretch;
            if (_line.sharedMaterial != null) _material = _line.material;   // one instance per player
            _line.enabled = false;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
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
            _whipActive = false;
            if (_line != null) _line.enabled = false;
        }

        // The whip is a one-shot, so it comes only from the cue (F6).
        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || !isActiveAndEnabled || cue != RodCue.Snapped || _bobber == null) return;
            _whipActive = true;
            _whipStart = Time.time;
            _whipFrom = _bobber.Position;
        }

        private void LateUpdate()
        {
            if (!_live || _line == null || _rod == null || !_rod.IsSpawned) return;

            float now = Time.time;
            bool whip = _whipActive && now - _whipStart < WhipSeconds;
            if (_whipActive && !whip) _whipActive = false;
            bool bobberShown = _bobber != null && _bobber.IsShown;
            if (!whip && !bobberShown)
            {
                if (_line.enabled) _line.enabled = false;
                return;
            }

            RodViewState vs = _rod.ViewState;
            Vector3 p0 = TipPosition();
            Vector3 p2 = whip ? Vector3.Lerp(_whipFrom, p0, (now - _whipStart) / WhipSeconds) : _bobber.Position;
            if (!IsFinite(p0) || !IsFinite(p2))
            {
                if (_line.enabled) _line.enabled = false;
                return;
            }

            bool hooked = !whip && vs.State == RodState.Hooked;
            float tension = hooked ? Mathf.Clamp01(vs.Tension) : 0f;
            bool grace = hooked && (vs.Flags & RodWireQuant.FlagGrace) != 0;

            float length = Vector3.Distance(p0, p2);
            float sag = whip ? 0.15f * length                                      // the whip: full sag
                : vs.State == RodState.Escaped ? SlackSagFactor * length           // the line goes slack
                : (1f - tension) * 0.15f * length;                                 // spec 10.5
            Vector3 p1 = (p0 + p2) * 0.5f + Vector3.down * (2f * sag);   // so the curve midpoint is `sag` below the chord midpoint
            for (int k = 0; k < PointCount; k++)
            {
                float u = k / (float)(PointCount - 1);
                float a = (1f - u) * (1f - u), b = 2f * u * (1f - u), c = u * u;
                _points[k] = a * p0 + b * p1 + c * p2;
            }

            _line.SetPositions(_points);
            if (!_line.enabled) _line.enabled = true;

            bool pulse = hooked && TensionCues.Evaluate(tension, grace, 1f, 1f).Band >= TensionBand.Warn;
            float width = pulse ? BaseWidth * (1f + 0.5f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * PulseHz * now))) : BaseWidth;
            if (Mathf.Abs(width - _lastWidth) > 1e-5f)
            {
                _lastWidth = width;
                _line.startWidth = width;
                _line.endWidth = width;
            }

            if (_material == null) return;
            float shaderTension = grace ? GraceTension : Mathf.Min(tension, JustBelowGrace);
            if (Mathf.Abs(shaderTension - _lastTension) > 0.005f || (_lastTension < 0f))
            {
                _lastTension = shaderTension;
                _material.SetFloat(TensionId, shaderTension);
            }

            float dash = Mathf.Max(1f, length / 0.3f);
            if (Mathf.Abs(dash - _lastDash) > 0.05f)
            {
                _lastDash = dash;
                _material.SetFloat(DashCountId, dash);
            }
        }

        private Vector3 TipPosition()
        {
            if (_presenter != null && _presenter.Tip != null) return _presenter.Tip.position;
            return transform.position + Vector3.up * 1.5f;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
