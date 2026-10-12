// Core/Fishing/FightSim.cs - owner-simulated fight (spec 9.5 + C 7.7 motion parameters). Pure C#; seeded Pcg32 only.
// Authority: runs on the owner only; the host never replays it (spec 9.9). Deterministic for a seed and input sequence.
// RNG draw order is fixed (plan T7 table): burst start 3 draws (+1 jitter), calm start 1, flip check 1 per eligible tick.
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
        private readonly bool _assist;
        private readonly bool _pity;          // R65 / M2-D23 bad-luck nudge: owner-side, deterministic, no RNG
        private Pcg32 _rng;
        private FightState _s;
        private float _phaseElapsed;          // seconds since the current phase began (burst ramp / calm drift ramp)
        private float _sinceFlip;             // seconds since the last flip (set to FlipMinGap at burst start)
        private float _burstLateralMult = 1f;
        private bool _nearSnapArmed;

        public FightSim(in FishFightProfile fish, in RodStats rod, in FishingTuning tuning, float weightT, uint seed,
                        float startLineLength, bool assist, bool pity = false)
        {
            _fish = fish; _rod = rod; _t = tuning; _assist = assist; _pity = pity;
            _rng = new Pcg32(seed);
            WeightFactor = Lerp(FishingConstants.WeightFactorMin, FishingConstants.WeightFactorMax, Clamp01(weightT));
            _s.LineLength = Clamp(startLineLength, 0f, _rod.MaxLineOut(_t.SpoolExtra));
            _s.FishStamina = 1f;
            _s.Phase = FightPhase.Calm;
            _s.PhaseTimer = FishingConstants.CalmLeadInSeconds;
            _sinceFlip = _fish.FlipMinGap;
            EffectivePull = _fish.PullStrength * WeightFactor;
        }

        private FightSim(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength)
        {
            IsJunk = true; _rod = rod; _t = tuning;
            _rng = new Pcg32(seed);
            WeightFactor = FishingConstants.WeightFactorMin;
            _s.LineLength = Math.Max(0f, startLineLength);
            _s.FishStamina = 1f;
            _s.Phase = FightPhase.Calm;
        }

        public static FightSim ForJunk(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength)
            => new FightSim(rod, tuning, seed, startLineLength);

        public bool IsJunk { get; }
        public ref readonly FightState State => ref _s;
        public float WeightFactor { get; }
        public float EffectivePull { get; private set; }
        public bool Spooled { get; private set; }
        public bool Exhausted { get; private set; }
        internal int DrawCount { get; private set; }
        internal float BurstLateralMult => _burstLateralMult;

        public FightOutcome Tick(in FightInput input)
        {
            if (_s.Outcome != FightOutcome.None) return _s.Outcome;
            const float dt = Step;
            _s.Elapsed += dt;
            float steer = float.IsNaN(input.Steer) ? 0f : Clamp(input.Steer, -1f, 1f);
            bool reel = input.Reel;
            if (IsJunk) return TickJunk(reel, dt);

            bool burst = _s.Phase == FightPhase.Burst;
            _phaseElapsed += dt;

            // 1. flips (side runs only)
            if (burst && _s.BurstDir != 0 && _fish.FlipRate > 0f)
            {
                _sinceFlip += dt;
                if (_sinceFlip >= _fish.FlipMinGap - TimeEps && Next01() < _fish.FlipRate * dt)
                {
                    _s.BurstDir = (sbyte)-_s.BurstDir;
                    _sinceFlip = 0f;
                }
            }
            bool side = burst && _s.BurstDir != 0;
            bool dive = burst && _s.BurstDir == 0;

            // 2. effective pull; burst terms use the ramped Pb (C 7.7)
            float p = _fish.PullStrength * WeightFactor * _s.FishStamina;
            float ramp = burst && _fish.BurstRamp > 0f ? Clamp01(_phaseElapsed / _fish.BurstRamp) : 1f;
            float pb = burst ? p * ramp : p;
            EffectivePull = p;

            // 3. lateral
            if (side) _s.Lateral += _s.BurstDir * pb * _fish.LateralSpeed * _burstLateralMult * dt;
            _s.Lateral += steer * _rod.SteerRate * dt;
            if (!burst) _s.Lateral -= _s.Lateral * _t.CalmCenterPull * dt;
            _s.Lateral = Clamp(_s.Lateral, -1f, 1f);
            float against = side ? Math.Max(0f, -steer * _s.BurstDir) : 0f;

            // 4. stamina: fight the side runs, let the dives run
            if (side) _s.FishStamina -= _fish.StaminaDrain * against * dt;
            else if (dive) _s.FishStamina -= _fish.StaminaDrain * (reel ? 0f : 1f) * dt;
            else _s.FishStamina += _t.FishStaminaRegen * dt;
            _s.FishStamina = Clamp01(_s.FishStamina);

            // 5. line
            float reelRate = burst
                ? _rod.ReelSpeed * Math.Max(FishingConstants.ReelRateFloor, 1f - _t.ReelBurstPenalty * pb)
                : _rod.ReelSpeed;
            if (reel) _s.LineLength -= reelRate * dt;
            if (burst) _s.LineLength += _fish.RunSpeed * pb * (dive ? _t.DiveRunMult : 1f) * dt;
            else if (_fish.CalmDrift > 0f && !Exhausted)
            {
                float driftRamp = _fish.CalmDriftRamp > 0f ? Clamp01(_phaseElapsed / _fish.CalmDriftRamp) : 1f;
                _s.LineLength += _fish.CalmDrift * driftRamp * _s.FishStamina * dt;
            }
            float maxOut = _rod.MaxLineOut(_t.SpoolExtra);
            Spooled = burst && _s.LineLength >= maxOut;      // calm drift never sets spooled
            _s.LineLength = Clamp(_s.LineLength, 0f, maxOut);

            // 6. tension
            float strain = 0f;
            if (burst && reel) strain += _t.ReelStrainBurst * pb * (1f - _t.CounterRelief * against);
            strain += _t.SteerStrain * Math.Abs(steer);
            if (Math.Abs(_s.Lateral) > _rod.AngleWindow) strain += _t.OutOfWindowStrain;
            if (Spooled) strain += _t.SpoolStrain * pb;
            float relax = !reel ? _t.RelaxRate * _rod.RelaxMult
                        : !burst ? _t.RelaxWhileReeling * _rod.RelaxMult
                        : 0f;
            float scale = (_assist ? _t.StrainScaleAssist : _t.StrainScale) * (_pity ? _t.PityStrainMult : 1f);   // R65
            _s.Tension = Clamp01(_s.Tension + (strain * scale / _rod.LineStrength - relax) * dt);
            if (_s.Tension > _s.MaxTension) _s.MaxTension = _s.Tension;

            // 7. timers and near-snap recoveries
            _s.SnapTimer = _s.Tension >= 1f ? _s.SnapTimer + dt : 0f;
            _s.PinnedTimer = Math.Abs(_s.Lateral) >= FishingConstants.PinnedLateral ? _s.PinnedTimer + dt : 0f;
            _s.SlackTimer = reel ? 0f : _s.SlackTimer + dt;
            if (_s.Tension > FishingConstants.NearSnapHigh) _nearSnapArmed = true;
            else if (_nearSnapArmed && _s.Tension < FishingConstants.NearSnapLow) { _nearSnapArmed = false; _s.NearSnapRecoveries++; }

            // 8. endings (priority: Landed, Snapped, Pinned, ThrewHook)
            float grace = (_assist ? _t.SnapGraceAssist : _t.SnapGrace) * (_pity ? _t.PityGraceMult : 1f);          // R65
            if (reel && _s.LineLength <= _t.LandDistance) return End(FightOutcome.Landed, EscapeReason.None);
            if (_s.SnapTimer >= grace - TimeEps) return End(FightOutcome.Snapped, EscapeReason.None);
            if (_s.PinnedTimer >= _t.PinEscapeTime - TimeEps) return End(FightOutcome.Escaped, EscapeReason.Pinned);
            if (_s.SlackTimer >= _t.SlackEscapeTime - TimeEps) return End(FightOutcome.Escaped, EscapeReason.ThrewHook);

            // 9. exhaustion, then the phase schedule (end of tick)
            if (!Exhausted && _s.FishStamina <= FishingConstants.ExhaustedStamina)
            {
                Exhausted = true;
                if (burst) { _s.Phase = FightPhase.Calm; _s.BurstDir = 0; _s.PhaseTimer = 0f; _phaseElapsed = 0f; }
            }
            if (!Exhausted)
            {
                _s.PhaseTimer -= dt;
                if (_s.PhaseTimer <= TimeEps) { if (burst) StartCalm(); else StartBurst(); }
            }
            return FightOutcome.None;
        }

        private FightOutcome TickJunk(bool reel, float dt)
        {
            Spooled = false; EffectivePull = 0f;
            if (reel) _s.LineLength = Math.Max(0f, _s.LineLength - _t.JunkDragSpeed * dt);
            _s.SlackTimer = reel ? 0f : _s.SlackTimer + dt;
            if (reel && _s.LineLength <= _t.LandDistance) return End(FightOutcome.Landed, EscapeReason.None);
            if (_s.SlackTimer >= _t.SlackEscapeTime - TimeEps) return End(FightOutcome.Escaped, EscapeReason.ThrewHook);
            return FightOutcome.None;
        }

        private void StartBurst()
        {
            bool isDive = Next01() < _fish.DiveChance;                 // draw 1
            float s = Next01();                                        // draw 2
            float duration = Uniform(_fish.BurstMin, _fish.BurstMax);  // draw 3
            float mult = 1f;
            if (_fish.LateralJitter > 0f)                              // draw 4 (jitter profiles only)
                mult = Uniform(1f - _fish.LateralJitter, 1f + _fish.LateralJitter);
            sbyte dir;
            if (isDive) dir = 0;
            else if (Math.Abs(_s.Lateral) < FishingConstants.CenterDeadzone) dir = s < 0.5f ? (sbyte)-1 : (sbyte)1;
            else
            {
                sbyte away = _s.Lateral > 0f ? (sbyte)-1 : (sbyte)1;
                // spec 9.5: 75% away, else a random side (50/50): 87.5% away overall, one draw (ruling B-Q4)
                dir = s < FishingConstants.SideRunAgainstChance + (1f - FishingConstants.SideRunAgainstChance) * 0.5f ? away : (sbyte)-away;
            }
            _s.Phase = FightPhase.Burst; _s.BurstDir = dir; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _burstLateralMult = mult;
        }

        private void StartCalm()
        {
            float duration = Uniform(_fish.CalmMin, _fish.CalmMax);    // draw 1
            _s.Phase = FightPhase.Calm; _s.BurstDir = 0; _s.PhaseTimer = duration; _phaseElapsed = 0f;
        }

        // Test seams (no draws). ForcePhase also clears the exhaustion latch.
        internal void ForcePhase(FightPhase phase, sbyte burstDir, float duration)
        {
            _s.Phase = phase; _s.BurstDir = phase == FightPhase.Burst ? burstDir : (sbyte)0; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _burstLateralMult = 1f; Exhausted = false;
        }

        internal void ForceState(in FightState state)
        {
            _s = state;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _burstLateralMult = 1f;
            _nearSnapArmed = state.Tension > FishingConstants.NearSnapHigh; Exhausted = false; Spooled = false;
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
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
