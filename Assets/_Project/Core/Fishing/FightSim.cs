// Core/Fishing/FightSim.cs - owner-simulated fight: the Rust-faithful baseline (spec 9.5, R72; maths from research note 2026-10-12 s8).
// Pure C#; seeded Pcg32 only. Authority: runs on the owner only; the host never replays it (spec 9.9). Deterministic for a seed and
// an input sequence. RNG draw order is fixed (plan rework-fight 0.9): constructor 1 (pull-wave phase), run start 3 (side, back,
// duration) + 1 jitter when LateralJitter > 0, rest start 1 (duration), 1 flip check per eligible run tick when FlipRate > 0.
// Per-profile quirks (R72, spec 9.6: timing, back chance, flips, ramp, rest creep, jitter, wave period) change only how the fish
// moves; the strain formula, tire, reel and endings are Rust's.
using System;

namespace FishGame.Core
{
    public sealed class FightSim
    {
        public const float Step = FishingConstants.FightStep;
        internal const float TimeEps = 1e-4f;

        private readonly FishFightProfile _fish;
        private readonly RodStats _rod;
        private readonly FishingTuning _t;
        private readonly float _gainScale;      // (StrainScale or StrainScaleAssist) x (PityStrainMult on a Pity cast) / LineStrength
        private readonly float _snapStrain;     // StrainBarFull + (StrainSnap - StrainBarFull) x (PityGraceMult on a Pity cast)
        private readonly float _lateralMaxRad;
        private readonly float _maxLineOut;
        private readonly float _wavePeriod;     // quirk: the profile's WavePeriod (Rust 4 s)
        private readonly float _wavePhase;
        private Pcg32 _rng;
        private FightState _s;
        private bool _nearSnapArmed;
        private float _phaseElapsed;            // seconds since the current run or rest began (ramp, rest creep)
        private float _sinceFlip;               // seconds since the last flip; set to FlipMinGap at a run start (a first-tick flip is allowed)
        private float _runSpeedMult = 1f;       // quirk: per-run speed multiplier (LateralJitter)

        // weightT is accepted for API stability and ignored: weight does not enter the R72 baseline (Rust has no weight term).
        public FightSim(in FishFightProfile fish, in RodStats rod, in FishingTuning tuning, float weightT, uint seed,
                        float startLineLength, bool assist, bool pity = false)
        {
            _fish = fish; _rod = rod; _t = tuning;
            _rng = new Pcg32(seed);
            _gainScale = (assist ? _t.StrainScaleAssist : _t.StrainScale) * (pity ? _t.PityStrainMult : 1f) / _rod.LineStrength;
            _snapStrain = _t.StrainBarFull + (_t.StrainSnap - _t.StrainBarFull) * (pity ? _t.PityGraceMult : 1f);
            _lateralMaxRad = _t.LateralMaxDeg * (float)(Math.PI / 180.0);
            _maxLineOut = _rod.MaxLineOut(_t.SpoolExtra);
            _s.LineLength = Clamp(startLineLength, 0f, _maxLineOut);
            _s.Phase = FightPhase.Calm;
            _s.PhaseTimer = 0f;                                                          // the first tick starts a run (Rust)
            _wavePeriod = _fish.WavePeriod > 0f ? _fish.WavePeriod : FishingConstants.RustWavePeriod;
            _sinceFlip = _fish.FlipMinGap;
            _wavePhase = Uniform(0f, _wavePeriod);                                       // draw 1: Rust's wave runs on global time
        }

        private FightSim(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength)
        {
            IsJunk = true; _rod = rod; _t = tuning;
            _rng = new Pcg32(seed);
            _s.LineLength = Math.Max(0f, startLineLength);
            _s.Phase = FightPhase.Calm;
        }

        public static FightSim ForJunk(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength)
            => new FightSim(rod, tuning, seed, startLineLength);

        public bool IsJunk { get; }
        public ref readonly FightState State => ref _s;
        public bool Exhausted => !IsJunk && _s.Tire >= _t.TireRunFull;          // tire01 = 1: runs have no length any more
        public float SnapTension => IsJunk ? 0f : _snapStrain / _t.StrainBarFull; // display units: 7/6 (1.25 on a Pity cast)
        public float PullWave { get; private set; }                               // p of the last tick, 0.2..2
        internal int DrawCount { get; private set; }
        internal float RunSpeedMult => _runSpeedMult;                             // test hook (Thrasher jitter)

        public FightOutcome Tick(in FightInput input)
        {
            if (_s.Outcome != FightOutcome.None) return _s.Outcome;
            const float dt = Step;
            _s.Elapsed += dt;
            bool reel = input.Reel;
            if (IsJunk) return TickJunk(reel, dt);

            // 1. fish state machine at the start of the tick (Rust: the first run starts at once)
            if (_s.PhaseTimer <= TimeEps)
            {
                if (_s.Phase == FightPhase.Burst) StartRest(); else StartRun();
            }
            bool run = _s.Phase == FightPhase.Burst;
            _phaseElapsed += dt;

            // 1b. quirk: mid-run side flips (Darter, Thrasher); one draw per eligible run tick, after the switch draws
            if (run && _fish.FlipRate > 0f)
            {
                _sinceFlip += dt;
                if (_sinceFlip >= _fish.FlipMinGap - TimeEps && Next01() < _fish.FlipRate * dt)
                {
                    _s.BurstDir = (sbyte)-_s.BurstDir;
                    _sinceFlip = 0f;
                }
            }
            bool back = run && _s.Back;

            // 2. steer: digital, dropped unless it pulls the line toward the cast line (Rust dot test; FG frame = cast line)
            int steer = SteerDir(input.Steer, _s.Lateral);
            bool counter = run && steer != 0 && steer == -_s.BurstDir;

            // 3. pull wave, and the fish's speed this tick (quirks: ramp-in, per-run multiplier); speed never touches strain
            float p = Wave(_wavePhase + _s.Elapsed, _wavePeriod);
            PullWave = p;
            float ramp = run && _fish.BurstRamp > 0f ? Clamp01(_phaseElapsed / _fish.BurstRamp) : 1f;
            float speed = p * _fish.MoveMult * ramp * _runSpeedMult;

            // 4. sideways in m/s, turned into lateral at the current radius; the cone edge flips the run's side
            float side = 0f;
            if (run) side += _s.BurstDir * _t.SideMoveSpeed * speed * (counter ? FishingConstants.CounterSideMult : 1f);
            if (steer != 0 && !(run && steer == _s.BurstDir)) side += steer * _rod.SteerRate;
            float radius = Math.Max(_s.LineLength, FishingConstants.LateralMinRadius);
            float lateral = _s.Lateral + side * dt / (radius * _lateralMaxRad);
            if (lateral > 1f || lateral < -1f)
            {
                lateral = Clamp(lateral, -1f, 1f);
                if (run) _s.BurstDir = (sbyte)-_s.BurstDir;
            }
            _s.Lateral = lateral;

            // 5. line: the reel speeds up as the fish tires; a back pull takes line, halved while reeling; the clamp adds no strain.
            // User playtest 2026-10-13 (F1): for the first ReelOpenSeconds the reel multiplier is floored by a surge that starts at ReelOpenMult and
            // settles into 1 (max with the tire curve, never a product, so CatchValidator.MaxReelSpeed still bounds every honest reel), and the
            // fish's back pull ramps in over the same window so the opening run cannot cancel the reel. Movement only: strain is untouched.
            float tireReel = 1f + (_t.TireReelMaxMult - 1f) * Clamp01(_s.Tire / _t.TireReelFull);
            bool surge = _t.ReelOpenMult > 1f && _t.ReelOpenSeconds > 0f;
            float reelMult = tireReel;
            float backRamp = 1f;
            if (surge)
            {
                float open = 1f + (_t.ReelOpenMult - 1f) * (1f - Clamp01(_s.Elapsed / _t.ReelOpenSeconds));
                if (open > reelMult) reelMult = open;
                backRamp = Clamp01(_s.Elapsed / _t.ReelOpenSeconds);
            }
            if (reel) _s.LineLength -= _rod.ReelSpeed * _fish.ReelInMult * reelMult * dt;
            if (back) _s.LineLength += _t.BackMoveSpeed * speed * backRamp * (reel ? FishingConstants.ReelBackMult : 1f) * dt;
            else if (!run && _fish.CalmDrift > 0f && !Exhausted)                           // quirk: Bulldog creeps line out at rest
            {
                float driftRamp = _fish.CalmDriftRamp > 0f ? Clamp01(_phaseElapsed / _fish.CalmDriftRamp) : 1f;
                _s.LineLength += _fish.CalmDrift * driftRamp * (1f - Clamp01(_s.Tire / _t.TireRunFull)) * dt;
            }
            _s.LineLength = Clamp(_s.LineLength, 0f, _maxLineOut);

            // 6. tire: only steering tires the fish; no floor
            if (steer != 0) _s.Tire += (counter && !back ? _t.TireCounterRate : _t.TireSteerRate) * dt;
            else _s.Tire -= _t.TireDecayRate * dt;

            // 7. strain: any input loads the line, only a full release drains it
            if (reel || steer != 0)
            {
                float k = !run ? _t.StrainRestInput
                        : reel ? (back ? _t.StrainReelBack : _t.StrainReelRun)
                        : counter ? _t.StrainCounterSteer : _t.StrainOtherSteer;
                _s.Strain += k * _fish.StrainMod * _gainScale * dt;
            }
            else _s.Strain = Math.Max(0f, _s.Strain - _t.RelaxRate * _rod.RelaxMult * dt);
            _s.Tension = _s.Strain / _t.StrainBarFull;
            if (_s.Tension > _s.MaxTension) _s.MaxTension = _s.Tension;

            // 8. timers and near-snap recoveries (over the full bar, then back under half)
            _s.SnapTimer = _s.Strain > _t.StrainBarFull ? _s.SnapTimer + dt : 0f;
            _s.SlackTimer = reel ? 0f : _s.SlackTimer + dt;
            _s.IdleTimer = reel || RawSteer(input.Steer) ? 0f : _s.IdleTimer + dt;   // F7: raw input, not the effective steer (a dropped steer is still a press)
            if (_s.Tension > FishingConstants.NearSnapHigh) _nearSnapArmed = true;
            else if (_nearSnapArmed && _s.Tension < FishingConstants.NearSnapLow) { _nearSnapArmed = false; _s.NearSnapRecoveries++; }

            // 9. endings (priority: Landed, Snapped, Timeout); a fish has no pinned or slack escape (Rust has neither)
            if (reel && _s.LineLength <= _t.LandDistance) return End(FightOutcome.Landed, EscapeReason.None);
            if (_s.Strain > _snapStrain) return End(FightOutcome.Snapped, EscapeReason.None);
            if (_s.Elapsed >= _t.FightTimeout - TimeEps) return End(FightOutcome.Escaped, EscapeReason.Timeout);
            // F7 (user playtest 2026-10-13): a walked-away player loses the fish after IdleEscapeTime. ThrewHook, not Timeout: the host takes ThrewHook
            // on any hooked cast, while an owner Timeout is checked against the host clock (>= FightTimeout - 2 s). IdleEscapeTime <= 0 = off.
            if (_t.IdleEscapeTime > 0f && _s.IdleTimer >= _t.IdleEscapeTime - TimeEps) return End(FightOutcome.Escaped, EscapeReason.ThrewHook);

            // 10. phase clock
            _s.PhaseTimer -= dt;
            return FightOutcome.None;
        }

        private FightOutcome TickJunk(bool reel, float dt)
        {
            if (reel) _s.LineLength = Math.Max(0f, _s.LineLength - _t.JunkDragSpeed * dt);
            _s.SlackTimer = reel ? 0f : _s.SlackTimer + dt;
            if (reel && _s.LineLength <= _t.LandDistance) return End(FightOutcome.Landed, EscapeReason.None);
            if (_s.SlackTimer >= _t.SlackEscapeTime - TimeEps) return End(FightOutcome.Escaped, EscapeReason.ThrewHook);
            return FightOutcome.None;
        }

        private void StartRun()
        {
            float tire01 = Clamp01(_s.Tire / _t.TireRunFull);
            sbyte dir = Next01() < FishingConstants.SideRunLeftChance ? (sbyte)-1 : (sbyte)1;     // draw 1
            float backRoll = Next01();                                                              // draw 2 (always made)
            bool back = backRoll < _fish.DiveChance && _s.LineLength < _rod.CastMaxDist - _t.BackPullMargin;
            float duration = Uniform(_fish.BurstMin, _fish.BurstMax) * (1f - tire01);              // draw 3
            float j = _fish.LateralJitter;
            _runSpeedMult = j > 0f ? Uniform(1f - j, 1f + j) : 1f;                                  // draw 4 (jitter profiles only)
            _s.Phase = FightPhase.Burst; _s.BurstDir = dir; _s.Back = back; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap;
        }

        private void StartRest()
        {
            float tire01 = Clamp01(_s.Tire / _t.TireRunFull);
            float duration = Uniform(_fish.CalmMin, _fish.CalmMax) * (1f + tire01);                // draw 1
            _s.Phase = FightPhase.Calm; _s.BurstDir = 0; _s.Back = false; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _runSpeedMult = 1f;
        }

        // True when the raw steer axis is a key press (NaN is none). Unlike SteerDir this ignores the side: any press shows the player is there.
        private static bool RawSteer(float steer)
        {
            return !float.IsNaN(steer) && Math.Abs(steer) >= FishingConstants.SteerDigitalThreshold;
        }

        // Rust: steering counts only while it pulls the line away from the bobber's side; here, toward the cast line (sign opposite to lateral).
        private static int SteerDir(float steer, float lateral)
        {
            if (float.IsNaN(steer)) return 0;
            int d = steer >= FishingConstants.SteerDigitalThreshold ? 1 : steer <= -FishingConstants.SteerDigitalThreshold ? -1 : 0;
            return d * lateral < 0f ? d : 0;
        }

        // Pull wave: a triangle of the given period peaking at 2, clamped to [0.2, 2]. With period 4 it is Rust's clamp(PingPong(t, 2), 0.2, 2).
        // The mean over one period is 1.01 for any period. A non-positive period falls back to Rust's 4 s.
        internal static float Wave(float t, float period)
        {
            if (!(period > 0f)) period = FishingConstants.RustWavePeriod;
            float half = 0.5f * period;
            float m = t - period * (float)Math.Floor(t / period);
            float pp = (m < half ? m : period - m) * (FishingConstants.PullWavePeak / half);
            return Clamp(pp, FishingConstants.PullWaveFloor, FishingConstants.PullWavePeak);
        }

        // Test seams (no draws). Both reset the quirk clocks (phase elapsed 0, flip gap satisfied, run speed x1). ForceState derives Tension from Strain.
        internal void ForcePhase(FightPhase phase, sbyte burstDir, bool back, float duration)
        {
            bool run = phase == FightPhase.Burst;
            _s.Phase = phase; _s.BurstDir = run ? burstDir : (sbyte)0; _s.Back = run && back; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _runSpeedMult = 1f;
        }

        internal void ForceState(in FightState state)
        {
            _s = state;
            _s.Tension = _s.Strain / _t.StrainBarFull;
            _nearSnapArmed = _s.Tension > FishingConstants.NearSnapHigh;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _runSpeedMult = 1f;
        }

        private FightOutcome End(FightOutcome o, EscapeReason r) { _s.Outcome = o; _s.Reason = r; return o; }
        private float Next01() { DrawCount++; return _rng.NextFloat(); }
        private float Uniform(float min, float max)
        {
            float u = Next01();
            return max > min ? Pcg32.MapUnitToRange(u, min, max) : min;
        }
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
