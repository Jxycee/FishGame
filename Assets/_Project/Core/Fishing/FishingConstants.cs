// Core/Fishing/FishingConstants.cs: formula constants from spec section 9 that are not in the 9.12 tuning table (C 5.2).
// Tuning lives in assets (C 5.1); this class holds formula constants only.
namespace FishGame.Core
{
    public static class FishingConstants
    {
        public const float FightStep = 0.02f;               // 50 Hz
        public const int MaxFightStepsPerFrame = 5;          // extra accumulated time is dropped
        public const float ReadyHoldSeconds = 0.25f;         // Readying -> Ready
        public const float MinChargeNorm = 0.1f;             // below: thunk
        public const float FeedbackBeatSeconds = 1.5f;       // terminal / unsolicited-commit beat
        public const float CalmLeadInSeconds = 0.8f;         // first Calm phase
        public const float CleanClaimClockSlackSeconds = 2.0f;  // |owner fight seconds - host hooked-to-result seconds| bound (clean-catch claim)
        public const float ExhaustedStamina = 0.05f;
        public const float SideRunAgainstChance = 0.75f;     // P(new side run is away from the current side); else a random side, so 87.5% away overall (spec 9.5 literal, B-Q4)
        public const float CenterDeadzone = 0.1f;            // |lateral| below: random side
        public const float PinnedLateral = 0.95f;
        public const float NearSnapHigh = 0.8f, NearSnapLow = 0.5f;
        public const float WeightFactorMin = 0.85f, WeightFactorMax = 1.25f;
        public const float ReelRateFloor = 0.1f;             // max(0.1, 1 - penalty * P)
        public const float NibbleJitterSeconds = 0.3f, NibbleMinOffsetSeconds = 0.5f, NibbleBiteGapSeconds = 0.6f;
        public const float NibbleMinBiteWait = 1.4f;         // below: zero nibbles
        public const int MaxNibbleSlots = 3;                 // FishingRoll.Nibble0..2
        public const float DepthMultFullDepth = 6.0f, DepthMultMin = 0.8f;
        public const float FlightBaseSeconds = 0.45f, FlightSecondsPerMeter = 0.012f;
        public const float ApexMin = 0.6f, ApexPerMeter = 0.12f;
        public const float PitchApexMultMin = 0.7f, PitchApexMultMax = 1.3f;
        public const float PitchApexLowDeg = -30f, PitchApexHighDeg = 45f;   // pitch mapped linearly, clamped
        public const float WaterProbeHeight = 20f, BedProbeDistance = 50f;
        public const float HostOriginSlack = 3.0f;           // cast origin vs host view, horizontal metres
        public const float HostWaitSlackSeconds = 30f;       // stuck-Waiting refund after tFlight + biteWait + 30
        // Landed floor (CatchValidator.LandedFloorSeconds). Do not shave 0.8 / 0.5: lag when walking toward the bobber is absorbed by the floor's
        // 0.8 factor plus the 0.5 s slack (delta <= 0.625 * reel).
        public const float LandedFloorFactor = 0.8f, LandedFloorSlackSeconds = 0.5f;
        public const float HookedLineSlack = 3.1f;           // lineLength0 = hostDistance - slack, floor landDistance; derived from the rig's max tip reach (2.52 m) + 0.5 m margin, T19-R1. Unchanged by R67: hostDistance is root -> landing and the owner's line is tip -> landing, both measured to the same (CastTipReach-shifted) landing
        // R67 (spec 3.2 / 9.3): a cast's distance is measured from the rod tip. The landing is origin + yawForward * (CastTipReach + distance).
        // Rig geometry, not tuning: the rod tip's horizontal forward reach from the player root in the owner rest pose at look pitch 0,
        // measured from TipAnchor by PlayMode FishingSoloTests.RodTip_ForwardReachAtPitchZero_MatchesCastTipReach (rounded to 0.1 m). Fixed on
        // owner AND host (identical inputs give a bit-identical landing); pitch never changes it. Re-derive after changing the rod rig.
        public const float CastTipReach = 2.4f;
        public const float JunkWeightLowRating = 0.6f;      // effRating 1-2
        public const float JunkWeightMidRating = 0.2f;       // effRating 3-5
        public const float JunkWeightHighRating = 0.05f;     // effRating 6+
        public const float TrophyT = 0.9f, GoodT = 0.6f, TrophyMult = 1.5f, GoodMult = 1.15f;
        public const float ValueBase = 0.6f, ValueSpan = 0.8f, LengthScaleMm = 400f;
        public const float SkewMin = 1.2f, SkewMax = 3.0f, LuckSkew = 0.4f;
        public const float FishPointSampleSeconds = 0.2f;    // view water sampling cadence
        public const ushort DebugBaitStock = 20;             // M2 debug loadout per bait
        public const ushort UnlimitedBaitCount = 0xFFFF;     // bare hook
        public const float CreelWarnFraction = 0.8f;
        public const float CatchHoldSeconds = 1.5f;          // reveal
        public const float CrewToastMinIntervalSeconds = 3f; // per player
        public const ulong DefaultDevRunSeed = 0xF15F15F1UL; // M2 runSeed (shown on the F3 overlay; no key changes it in M2, G-Q5)
        public const string CatchStreamId = "catch";         // Rng.Derive stream id (spec 9.8 StreamId.Catch)
    }
}
