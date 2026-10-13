// Core/Fishing/FishingTuning.cs: every spec 9.12 parameter plus creel caps (C 7.5). No defaults here (C 5.1):
// values come from the FishingTuningDef asset (seeded by FishingSeedData) or from test data.
namespace FishGame.Core
{
    public struct FishingTuning
    {
        public float CastChargeTime; public bool ChargePingPong; public float CastMinDist, MinLandDepth;
        public float BiteWaitMin, BiteWaitMax, MinBiteWait, HookBeat, LandDistance;
        public float SpoolExtra;             // maxLineOut = castMaxDist + SpoolExtra (the line clamps there; R72: no strain)
        public float RelaxRate;              // release drain, strain per second (Rust 1.5)
        public float StrainScale, StrainScaleAssist;   // Rust GlobalStrainSpeedMultiplier 0.75; assist 0.45
        public float SlackEscapeTime, LateralMaxDeg, HookedLeash, JunkDragSpeed;
        public float ResultTimeout, CommitTimeout, HookTolerance, MaxFightTime; public int RodStateHz; public bool CancelRefundsBait;
        public int CreelMaxEntries; public int CreelMaxWeightG;
        public float CleanCatchMaxTension;   // clean-catch bonus threshold (seed 0.8; 13 M2-D16)

        // Bad-luck nudge (R65 / M2-D23; seeds 3, 0.85, 1.5, 0.3; validator ranges in 7.14):
        public int PityAfterFailures;        // consecutive failed casts per slot before the next accepted cast is Pity; 0 = off
        public float PityStrainMult;         // FightSim strain/tension gain multiplier on a Pity cast
        public float PityGraceMult;          // FightSim headroom multiplier on a Pity cast (R72: snap strain = bar + (snap - bar) x this)
        public float PityHookWindowBonus;    // seconds added to a real bite hook window (HookWindow > 0) on a Pity cast

        // R72 Rust fight baseline (spec 9.5, 9.12; research note 8). Strain is in Rust units: the bar is full at StrainBarFull and the
        // line snaps above StrainSnap. Per-second strain gains k by situation, before x StrainMod x StrainScale / LineStrength:
        public float StrainBarFull, StrainSnap;
        public float StrainRestInput, StrainReelRun, StrainReelBack, StrainCounterSteer, StrainOtherSteer;
        public float SideMoveSpeed, BackMoveSpeed;                                   // m/s at pull wave 1, x MoveMult
        public float TireSteerRate, TireCounterRate, TireDecayRate;                  // /s
        public float TireRunFull, TireReelFull, TireReelMaxMult;                     // run/rest scaling saturates at TireRunFull; reel x1..x3 over 0..TireReelFull
        public float BackPullMargin;                                                 // m: no back pull while lineLength >= castMaxDist - margin
        public float FightTimeout;                                                   // s: Escaped(Timeout)
    }
}
