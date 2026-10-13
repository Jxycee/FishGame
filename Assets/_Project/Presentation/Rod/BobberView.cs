// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.
// The bobber (spec 10.5, C 7.24, F6): world-positioned every LateUpdate from the polled view, so a late joiner is right with no replay.
//  - Baseline: the first LateUpdate after spawn (and after every re-enable) records the observed state and CastId and, for a line-out state,
//    places the bobber AT ONCE at its steady spot. No arc, no replayed cue.
//  - Arc: only on an observed CHANGE into Casting (previous observed state != Casting, current == Casting) with a CastId not seen before.
//    A repeated or resent Casting value never restarts it; nothing else starts one. The arc runs to completion if the state moves on, unless
//    the cast ends (Landed or Snapped hide the bobber at once; a recall replaces the arc).
//  - Steady spot: water height from WaterSurface.HeightAt (bobber bob), by state: Waiting +0.02, Bite -0.25, Hooked fish point -0.1,
//    dive -FightViewMath.DiveDepth (0.75, F9: fully under, out of sight; was 0.35), exhausted +0.03 with a slow roll; the Nibble cue adds a
//    0.25 s dip. Easing at 1 - exp(-20 dt); a dive's downward move eases at FightViewMath.PlungeEaseRate (50), a yank (the splash crown, ring
//    and bubble trail of the Dive cue are SplashFx's). The bobber grows with its distance from the rod tip (FightViewMath.DistanceScale, F6).
//  - Hooked: rolls +-30 deg toward the pull side (FlagRight), halved on a back pull (FlagDive), eased at clamp01(dt) (Rust, R72); a spent fish rolls slowly.
//  - Recall: a 0.4 s arc back to the bent tip on Cue Recall, on Escaped with FlagRecall, or on a line-out -> Idle transition; then hide.
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    // Order 240 (F7): after PlayerBody (200); LineView (260) reads Position.
    [DefaultExecutionOrder(240)]
    public sealed class BobberView : MonoBehaviour
    {
        private const float EaseRate = FightViewMath.EaseRate;
        private const float RecallSeconds = 0.4f, RecallApex = 0.5f;
        private const float NibbleSeconds = 0.25f, NibbleDip = 0.04f;
        private const float ExhaustedRollDeg = 30f, RollRadPerSecond = 1.5f;

        private enum Phase : byte { Hidden, Steady, Arc, Recall }

        private FishingRodNetwork _rod;
        private PlayerCamera _cam;
        private RodPresenter _presenter;
        private Transform _bobber;
        private MeshRenderer _renderer;

        private bool _live, _baselined;
        private Phase _phase;
        private RodState _lastState;
        private uint _arcCastId;
        private float _nibbleAt = -10f;
        private float _roll;
        private float _tilt01 = 0.5f;   // R72: 0 = pulling left, 1 = pulling right, 0.5 = at rest

        private Vector3 _arcStart, _arcEnd;
        private float _arcStartTime, _arcFlight, _arcApex;
        private Vector3 _recallFrom;
        private float _recallStart;

        // The world position last set (valid even while hidden: LineView's snap whip starts from it).
        public Vector3 Position { get; private set; }

        internal bool IsShown => _phase != Phase.Hidden;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            _cam = GetComponent<PlayerCamera>();
            _presenter = GetComponent<RodPresenter>();
            _bobber = transform.Find(PlayerRigPaths.Bobber);
            if (_bobber != null)
            {
                _renderer = _bobber.GetComponent<MeshRenderer>();
                Position = _bobber.position;
            }

            SetShown(false);
        }

        private void OnEnable()
        {
            if (_rod == null) return;
            _rod.Cue += HandleCue;
            _live = true;
            _baselined = false;   // a re-enable is a fresh observation: snap, never replay
            _tilt01 = 0.5f;
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null) _rod.Cue -= HandleCue;
            _phase = Phase.Hidden;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || !isActiveAndEnabled || !_baselined) return;
            if (cue == RodCue.Nibble)
            {
                _nibbleAt = Time.time;
            }
            else if (cue == RodCue.Recall)
            {
                StartRecall();
            }
        }

        private void LateUpdate()
        {
            if (!_live || _bobber == null || _rod == null || !_rod.IsSpawned) return;

            RodViewState vs = _rod.ViewState;
            float now = Time.time;
            float dt = Time.deltaTime;

            if (!_baselined)
            {
                _baselined = true;
                _lastState = vs.State;
                _arcCastId = vs.CastId;
                if (IsLineOut(vs.State) && IsFinite(vs.BobberPos))
                {
                    Position = SteadyTarget(vs, now);
                    _phase = Phase.Steady;
                    Apply(vs, now, dt);
                }
                else
                {
                    _phase = Phase.Hidden;
                    SetShown(false);
                }

                return;
            }

            // Cast over: the reveal or the silhouette jump takes over at once.
            if (vs.State == RodState.Landed || vs.State == RodState.Snapped)
            {
                _phase = Phase.Hidden;
            }
            else
            {
                // The arc rule (C 7.24, C 9, spec 9.3): an observed change into Casting with a new CastId, nothing else.
                if (_lastState != RodState.Casting && vs.State == RodState.Casting && vs.CastId != _arcCastId && IsFinite(vs.BobberPos))
                {
                    _arcCastId = vs.CastId;
                    StartArc(vs, now);
                }
                else if (_phase == Phase.Hidden && IsLineOut(vs.State) && IsFinite(vs.BobberPos) && _lastState != vs.State)
                {
                    // A line-out state reached without a Casting we saw (a missed update): show the bobber at its spot, no arc.
                    Position = SteadyTarget(vs, now);
                    _phase = Phase.Steady;
                }

                if (_phase == Phase.Steady || _phase == Phase.Arc)
                {
                    if (vs.State == RodState.Escaped && (vs.Flags & RodWireQuant.FlagRecall) != 0) StartRecall();
                    else if (FightViewMath.RecallOnLeavingLineOut(_lastState, vs.State)) StartRecall();
                }
            }

            Apply(vs, now, dt);
            _lastState = vs.State;
        }

        private void Apply(in RodViewState vs, float now, float dt)
        {
            switch (_phase)
            {
                case Phase.Hidden:
                    SetShown(false);
                    return;

                case Phase.Arc:
                {
                    float u = _arcFlight > 0f ? (now - _arcStartTime) / _arcFlight : 1f;
                    if (u >= 1f)
                    {
                        _phase = Phase.Steady;   // continues from the arc's end with the normal easing
                        goto case Phase.Steady;
                    }

                    CastMath.ArcPoint(u, _arcStart.x, _arcStart.y, _arcStart.z, _arcEnd.x, _arcEnd.y, _arcEnd.z, _arcApex, out float x, out float y, out float z);
                    Position = new Vector3(x, y, z);
                    Place(0f);
                    return;
                }

                case Phase.Steady:
                {
                    bool hooked = vs.State == RodState.Hooked;
                    bool exhausted = hooked && (vs.Flags & RodWireQuant.FlagExhausted) != 0;
                    if (IsFinite(vs.BobberPos))
                    {
                        Vector3 target = SteadyTarget(vs, now);
                        float k = 1f - Mathf.Exp(-EaseRate * dt);
                        bool diving = hooked && !exhausted && (vs.Flags & RodWireQuant.FlagDive) != 0;
                        float ky = 1f - Mathf.Exp(-FightViewMath.YEaseRate(diving, Position.y, target.y) * dt);
                        Position = new Vector3(Mathf.Lerp(Position.x, target.x, k), Mathf.Lerp(Position.y, target.y, ky), Mathf.Lerp(Position.z, target.z, k));
                    }

                    if (hooked && !exhausted)
                    {
                        // R72 (Rust): lean toward the pull side, halved on a back pull, eased at clamp01(dt).
                        bool run = (vs.Flags & RodWireQuant.FlagBurst) != 0;
                        bool right = (vs.Flags & RodWireQuant.FlagRight) != 0;
                        bool back = (vs.Flags & RodWireQuant.FlagDive) != 0;
                        _tilt01 = FightViewMath.TiltStep(_tilt01, FightViewMath.TiltTarget(run, right, back), dt);
                        _roll = FightViewMath.RollDeg(_tilt01);
                    }
                    else
                    {
                        _tilt01 = 0.5f;
                        float rollTarget = exhausted ? ExhaustedRollDeg * Mathf.Sin(RollRadPerSecond * now) : 0f;
                        _roll = Mathf.Lerp(_roll, rollTarget, 1f - Mathf.Exp(-10f * dt));
                    }
                    Place(_roll);
                    return;
                }

                case Phase.Recall:
                {
                    Vector3 tip = TipPosition();
                    float u = (now - _recallStart) / RecallSeconds;
                    if (u >= 1f)
                    {
                        Position = tip;
                        _phase = Phase.Hidden;
                        SetShown(false);
                        return;
                    }

                    CastMath.ArcPoint(u, _recallFrom.x, _recallFrom.y, _recallFrom.z, tip.x, tip.y, tip.z, RecallApex, out float x, out float y, out float z);
                    Position = new Vector3(x, y, z);
                    Place(0f);
                    return;
                }
            }
        }

        private void StartArc(in RodViewState vs, float now)
        {
            float endX = vs.BobberPos.x, endZ = vs.BobberPos.z;
            _arcEnd = new Vector3(endX, WaterSurface.HeightAt(endX, endZ), endZ);
            _arcStart = TipPosition();

            // R67: the cast distance is measured from the rod tip, so the flight time and apex follow the horizontal tip -> landing distance
            // (the owner machine's FlightTime(CastDistance) uses the same number; measuring from the root would add CastTipReach to it).
            float dx = endX - _arcStart.x, dz = endZ - _arcStart.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            _arcFlight = CastMath.FlightTime(distance);
            _arcApex = CastMath.Apex(distance, CastMath.PitchApexMult(PitchDeg()));
            _arcStartTime = now;
            if (!(_arcFlight > 0f) || !IsFinite(_arcStart) || !IsFinite(_arcEnd))
            {
                _phase = Phase.Steady;   // nothing sane to fly: just float at the landing spot
                return;
            }

            _phase = Phase.Arc;
        }

        private void StartRecall()
        {
            if (_phase == Phase.Hidden || _phase == Phase.Recall) return;
            _recallFrom = Position;
            _recallStart = Time.time;
            _phase = Phase.Recall;
        }

        // Owner: the live look pitch; remotes: the synced NetPitchQ (C 7.24).
        private float PitchDeg()
        {
            if (_rod.IsOwner && _cam != null) return _cam.PitchDeg;
            return _rod.Player != null ? PitchQuant.Dequantize(_rod.Player.NetPitchQ.Value) : 0f;
        }

        private Vector3 TipPosition()
        {
            if (_presenter != null && _presenter.Tip != null) return _presenter.Tip.position;
            return transform.position + Vector3.up * 1.5f;
        }

        private Vector3 SteadyTarget(in RodViewState vs, float now)
        {
            float x = vs.BobberPos.x, z = vs.BobberPos.z;
            float h = WaterSurface.HeightAt(x, z);
            float y;
            switch (vs.State)
            {
                case RodState.Bite:
                    y = h - 0.25f;
                    break;
                case RodState.Hooked:
                    y = h + FightViewMath.HookedDepthOffset((vs.Flags & RodWireQuant.FlagDive) != 0, (vs.Flags & RodWireQuant.FlagExhausted) != 0);
                    break;
                default:
                    y = h + 0.02f;
                    break;
            }

            float u = now - _nibbleAt;
            // The dip scales with the bobber (DistanceScale), so a nibble stays visible on a far, enlarged float.
            if (u >= 0f && u < NibbleSeconds)
                y -= NibbleDip * FightViewMath.DistanceScale(Vector3.Distance(Position, TipPosition())) * Mathf.Sin(Mathf.PI * u / NibbleSeconds);
            return new Vector3(x, y, z);
        }

        // Rolls about the horizontal tip-to-bobber direction. A positive roll leans the top to the player's left, so a left pull gives +30.
        private void Place(float rollDeg)
        {
            SetShown(true);
            Vector3 d = Position - TipPosition();
            d.y = 0f;
            Quaternion basis = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
            _bobber.SetPositionAndRotation(Position, basis * Quaternion.Euler(0f, 0f, rollDeg));
            // F6: the float grows with its distance from the rod tip, so it stays readable at cast distance (continuous from 1 at the tip).
            _bobber.localScale = Vector3.one * FightViewMath.DistanceScale(Vector3.Distance(Position, TipPosition()));
        }

        private void SetShown(bool shown)
        {
            if (_renderer != null && _renderer.enabled != shown) _renderer.enabled = shown;
        }

        private static bool IsLineOut(RodState state)
        {
            return FightViewMath.IsLineOut(state);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
