// Core/Fishing/FishingTuning.cs: every spec 9.12 parameter plus creel caps (C 7.5). No defaults here (C 5.1):
// values come from the FishingTuningDef asset (seeded by FishingSeedData) or from test data.
namespace FishGame.Core
{
    public struct FishingTuning
    {
        public float CastChargeTime; public bool ChargePingPong; public float CastMinDist, MinLandDepth;
        public float BiteWaitMin, BiteWaitMax, MinBiteWait, HookBeat, LandDistance;
        public float ReelStrainBurst, CounterRelief, SteerStrain, OutOfWindowStrain, SpoolStrain, SpoolExtra;
        public float RelaxRate, RelaxWhileReeling, ReelBurstPenalty, DiveRunMult;
        public float SnapGrace, SnapGraceAssist, StrainScale, StrainScaleAssist;
        public float PinEscapeTime, SlackEscapeTime, FishStaminaRegen, CalmCenterPull, LateralMaxDeg, HookedLeash, JunkDragSpeed;
        public float ResultTimeout, CommitTimeout, HookTolerance, MaxFightTime; public int RodStateHz; public bool CancelRefundsBait;
        public int CreelMaxEntries; public int CreelMaxWeightG;
        public float CleanCatchMaxTension;   // clean-catch bonus threshold (seed 0.8; 13 M2-D16)

        // Bad-luck nudge (R65 / M2-D23; seeds 3, 0.85, 1.5, 0.3; validator ranges in 7.14):
        public int PityAfterFailures;        // consecutive failed casts per slot before the next accepted cast is Pity; 0 = off
        public float PityStrainMult;         // FightSim strain/tension gain multiplier on a Pity cast
        public float PityGraceMult;          // FightSim snap grace multiplier on a Pity cast
        public float PityHookWindowBonus;    // seconds added to a real bite hook window (HookWindow > 0) on a Pity cast
    }
}
