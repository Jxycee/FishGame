// Core/Fishing/FishFightProfile.cs: one fight profile; mirrors FightProfileDef (spec 9.6: Rust tier + quirks, C 7.5).
// No default tuning here (C 5.1); the data assets and FishingSeedData carry the numbers.
namespace FishGame.Core
{
    public struct FishFightProfile
    {
        // R72 quirks (spec 9.6): timing, back chance, flips, ramp, rest creep, per-run speed jitter, wave period; behaviour only, never strain.
        public float CalmMin, CalmMax, BurstMin, BurstMax, FlipRate, DiveChance;   // rest and run ranges (s), flips per run-second, back-pull chance

        public float BurstRamp;        // s; the fish's speed (side and back) ramps 0 -> 1 over this time from run start (Steady, Runner)
        public float CalmDrift;        // m/s; line creeps out at rest (Bulldog), x (1 - tire01), none once spent
        public float CalmDriftRamp;    // s; CalmDrift ramps 0 -> 1 from rest start (0 = immediate)
        public float FlipMinGap;       // s; minimum time between mid-run flips (default 0.5; Darter 0.3)
        public float LateralJitter;    // 0..0.5; per-run speed multiplier (side and back) drawn from [1 - j, 1 + j] (Thrasher)

        // R72 Rust tiers (spec 9.6): small 0.8 / 0.8 / 1.2, medium 1 / 1 / 1, large 1.25 / 1.25 / 0.9.
        public float StrainMod;        // multiplies every strain gain (Rust StrainModifier)
        public float MoveMult;         // multiplies the side and back pull speeds (Rust MoveMultiplier)
        public float ReelInMult;       // multiplies the reel speed (Rust ReelInSpeedMultiplier)
        // R72 quirk (spec 9.6): pull-wave period in s (Rust 4); shape and mean unchanged, shorter = faster surges (Thrasher 1.5).
        public float WavePeriod;
    }
}
