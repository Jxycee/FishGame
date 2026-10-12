// Core/Fishing/FishFightProfile.cs: one fight profile; mirrors FightProfileDef (spec 9.6 columns + motion parameters, C 7.5).
// No default tuning here (C 5.1); the data assets and FishingSeedData carry the numbers.
namespace FishGame.Core
{
    public struct FishFightProfile
    {
        public float PullStrength, LateralSpeed, RunSpeed, StaminaDrain, CalmMin, CalmMax, BurstMin, BurstMax, FlipRate, DiveChance;

        // Motion parameters (all 0 = the base spec 9.5 model, except FlipMinGap which must be > 0):
        public float BurstRamp;        // s; burst force ramps 0 -> 1 over this time from burst start (Steady, Runner)
        public float CalmDrift;        // m/s; line creeps out during Calm (Bulldog), x FishStamina
        public float CalmDriftRamp;    // s; CalmDrift ramps 0 -> 1 from Calm start (0 = immediate)
        public float FlipMinGap;       // s; minimum time between flips (spec default 0.5; Darter 0.3)
        public float LateralJitter;    // 0..0.5; per-burst lateralSpeed multiplier drawn from [1 - j, 1 + j] (Thrasher)
    }
}
