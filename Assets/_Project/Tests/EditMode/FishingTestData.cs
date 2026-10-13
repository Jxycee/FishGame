// Tests/EditMode/FishingTestData.cs - spec 9.6/9.7/9.12 tables for the Core fishing tests (T5; read-only for T6-T9).
// R72 baseline values (plan rework-fight F2.7): Rust's shared tuning, Rust's rods ratios, quirk-free BaseProfiles for the hand checks and
// the five-style MotionProfiles for the machine, ledger and quirk tests.
// Index order = ContentRegistry ordinal id sort. Runtime seeds live in FishingSeedData (T14); both must equal the spec.
using FishGame.Core;

namespace FishGame.Tests.EditMode
{
    internal static class FishingTestData
    {
        public const int RodCarbon = 0, RodDeepsea = 1, RodHandmade = 2, RodTwig = 3;
        public const int BaitBareHook = 0, BaitGrub = 1, BaitMinnow = 2, BaitRawMeat = 3, BaitShrimp = 4, BaitWorm = 5;
        public const int ProfileBulldog = 0, ProfileDarter = 1, ProfileRunner = 2, ProfileSteady = 3, ProfileThrasher = 4;
        public const int SpeciesBluegill = 0, SpeciesCarp = 1, SpeciesCatfish = 2, SpeciesPerch = 3, SpeciesTrout = 4;
        public const int JunkOldBoot = 0, JunkTinCan = 1;

        public static FishingTuning Tuning() => new FishingTuning
        {
            CastChargeTime = 1f, ChargePingPong = true, CastMinDist = 2.5f, MinLandDepth = 0.8f,
            BiteWaitMin = 4f, BiteWaitMax = 18f, MinBiteWait = 1f, HookBeat = 0.25f, LandDistance = 1f,
            ReelStrainBurst = 0.30f, CounterRelief = 0.5f, SteerStrain = 0.03f, OutOfWindowStrain = 0.20f,
            SpoolStrain = 0.50f, SpoolExtra = 12f, RelaxRate = 1.5f, RelaxWhileReeling = 0.10f,
            ReelBurstPenalty = 1.0f, DiveRunMult = 1.5f, SnapGrace = 0.25f, SnapGraceAssist = 0.75f,
            StrainScale = 0.75f, StrainScaleAssist = 0.45f, PinEscapeTime = 5f, SlackEscapeTime = 12f,
            FishStaminaRegen = 0.04f, CalmCenterPull = 0.15f, LateralMaxDeg = 60f, HookedLeash = 3f,
            JunkDragSpeed = 3f, ResultTimeout = 3f, CommitTimeout = 3f, HookTolerance = 0.75f,
            MaxFightTime = 240f, RodStateHz = 15, CancelRefundsBait = true,
            CreelMaxEntries = 12, CreelMaxWeightG = 15000, CleanCatchMaxTension = 0.8f,
            PityAfterFailures = 3, PityStrainMult = 0.85f, PityGraceMult = 1.5f, PityHookWindowBonus = 0.3f,   // R65 / M2-D23 seeds
            // R72 Rust fight baseline (spec 9.12): additive in F1, read by the new FightSim from F2.
            StrainBarFull = 6f, StrainSnap = 7f, StrainRestInput = 0.5f, StrainReelRun = 2.2f, StrainReelBack = 2.5f,
            StrainCounterSteer = 0.8f, StrainOtherSteer = 0.5f, SideMoveSpeed = 1.2f, BackMoveSpeed = 1.3f,
            TireSteerRate = 0.8f, TireCounterRate = 1.25f, TireDecayRate = 0.1f, TireRunFull = 20f, TireReelFull = 5f,
            TireReelMaxMult = 3f, BackPullMargin = 2f, FightTimeout = 120f,
        };

        static RodStats Rod(byte tier, float cast, float reel, float strength, float window, float steer, float relax) =>
            new RodStats { Tier = tier, CastMaxDist = cast, ReelSpeed = reel, LineStrength = strength,
                           AngleWindow = window, SteerRate = steer, RelaxMult = relax };

        public static RodStats[] Rods() => new[]
        {
            Rod(2, 24f, 0.72f, 1.3f, 0.70f, 0.93f, 1.1f),   // rod_carbon
            Rod(3, 30f, 0.78f, 1.6f, 0.75f, 1.07f, 1.2f),   // rod_deepsea
            Rod(1, 18f, 0.6f, 1.0f, 0.60f, 0.8f, 1.0f),     // rod_handmade (Rust's rod)
            Rod(0, 14f, 0.54f, 0.8f, 0.50f, 0.67f, 0.9f),   // rod_twig
        };

        static BaitStats Bait(int rating, int stack, int max, int nibbles, float speed, float junk, bool unlimited = false) =>
            new BaitStats { Rating = rating, StackMax = stack, MaxRating = max, MaxNibbles = nibbles,
                            BiteSpeedMod = speed, JunkMult = junk, Unlimited = unlimited };

        public static BaitStats[] Baits() => new[]
        {
            Bait(1, 1, 1, 1, 1.30f, 1.5f, true),   // bait_bare_hook
            Bait(4, 3, 12, 2, 0.85f, 0.9f),        // bait_grub
            Bait(2, 2, 4, 1, 1.00f, 1.1f),         // bait_minnow
            Bait(10, 1, 10, 0, 0.70f, 0.5f),       // bait_raw_meat
            Bait(5, 2, 10, 1, 0.80f, 0.8f),        // bait_shrimp
            Bait(3, 3, 9, 2, 0.90f, 1.0f),         // bait_worm
        };

        static FishFightProfile Profile(float pull, float lat, float run, float drain, float calmMin, float calmMax,
                                        float burstMin, float burstMax, float flip, float dive) =>
            new FishFightProfile { PullStrength = pull, LateralSpeed = lat, RunSpeed = run, StaminaDrain = drain,
                                   CalmMin = calmMin, CalmMax = calmMax, BurstMin = burstMin, BurstMax = burstMax,
                                   FlipRate = flip, DiveChance = dive, FlipMinGap = 0.5f };

        // Spec 9.6 columns; every motion field 0 and FlipMinGap 0.5 (the base spec 9.5 model; FightSimTests).
        public static FishFightProfile[] BaseProfiles()
        {
            FishFightProfile[] p =
            {
                Profile(0.60f, 0.60f, 0.50f, 0.06f, 1.0f, 3.0f, 2.0f, 4.0f, 0.0f, 0.35f),   // bulldog
                Profile(0.45f, 0.70f, 0.40f, 0.07f, 1.0f, 2.5f, 1.5f, 3.0f, 0.2f, 0.00f),   // darter
                Profile(0.85f, 0.80f, 0.80f, 0.05f, 1.0f, 2.0f, 2.5f, 4.0f, 0.1f, 0.25f),   // runner
                Profile(0.30f, 0.60f, 0.30f, 0.08f, 1.0f, 2.5f, 1.5f, 2.5f, 0.0f, 0.00f),   // steady
                Profile(0.70f, 1.00f, 0.60f, 0.09f, 1.0f, 2.5f, 1.5f, 3.0f, 0.5f, 0.00f),   // thrasher
            };
            // R72 Rust tiers (spec 9.6): small 0.8 / 0.8 / 1.2, medium 1 / 1 / 1, large 1.25 / 1.25 / 0.9. Nothing reads them before F2.
            Tier(ref p[ProfileSteady], 0.8f, 0.8f, 1.2f);
            Tier(ref p[ProfileDarter], 1f, 1f, 1f);
            Tier(ref p[ProfileThrasher], 1f, 1f, 1f);
            Tier(ref p[ProfileBulldog], 1.25f, 1.25f, 0.9f);
            Tier(ref p[ProfileRunner], 1.25f, 1.25f, 0.9f);
            // Rust-faithful and quirk-free (the hand checks): rest 2-4 s, run 3-7 s, back 0.39, no flips, ramp, drift or jitter, 4 s wave.
            for (int i = 0; i < p.Length; i++)
            {
                p[i].CalmMin = 2f; p[i].CalmMax = 4f; p[i].BurstMin = 3f; p[i].BurstMax = 7f; p[i].DiveChance = 0.39f;
                p[i].FlipRate = 0f; p[i].FlipMinGap = 0.5f; p[i].BurstRamp = 0f; p[i].CalmDrift = 0f; p[i].CalmDriftRamp = 0f;
                p[i].LateralJitter = 0f; p[i].WavePeriod = 4f;
            }
            return p;
        }

        static void Tier(ref FishFightProfile p, float strainMod, float moveMult, float reelInMult)
        {
            p.StrainMod = strainMod; p.MoveMult = moveMult; p.ReelInMult = reelInMult;
        }

        // R72: the five styles (spec 9.6 quirks, plan rework-fight 0.3) on top of the quirk-free base: the shipped values (FightSim quirk tests;
        // machine and ledger tests).
        public static FishFightProfile[] MotionProfiles()
        {
            FishFightProfile[] p = BaseProfiles();
            p[ProfileSteady].BurstMin = 3f; p[ProfileSteady].BurstMax = 5f; p[ProfileSteady].DiveChance = 0.15f; p[ProfileSteady].BurstRamp = 0.6f;
            p[ProfileDarter].CalmMin = 1f; p[ProfileDarter].CalmMax = 2f; p[ProfileDarter].BurstMin = 2f; p[ProfileDarter].BurstMax = 3.5f;
            p[ProfileDarter].DiveChance = 0.15f; p[ProfileDarter].FlipRate = 1.0f; p[ProfileDarter].FlipMinGap = 0.3f;
            p[ProfileThrasher].CalmMin = 1.5f; p[ProfileThrasher].CalmMax = 3f; p[ProfileThrasher].BurstMin = 3f; p[ProfileThrasher].BurstMax = 5f;
            p[ProfileThrasher].DiveChance = 0.20f; p[ProfileThrasher].FlipRate = 0.4f; p[ProfileThrasher].FlipMinGap = 0.5f;
            p[ProfileThrasher].LateralJitter = 0.3f; p[ProfileThrasher].WavePeriod = 1.5f;
            p[ProfileBulldog].BurstMin = 2.5f; p[ProfileBulldog].BurstMax = 4.5f; p[ProfileBulldog].DiveChance = 0.55f;
            p[ProfileBulldog].CalmDrift = 0.06f; p[ProfileBulldog].CalmDriftRamp = 2.5f;
            p[ProfileRunner].CalmMin = 2f; p[ProfileRunner].CalmMax = 3.5f; p[ProfileRunner].BurstMin = 5f; p[ProfileRunner].BurstMax = 9f;
            p[ProfileRunner].DiveChance = 0.30f; p[ProfileRunner].BurstRamp = 0.8f;
            return p;
        }

        static FishSpeciesData Fish(int profile, int minG, int maxG, int value, int minBait, float waitMod, float minDepth) =>
            new FishSpeciesData { FightProfileIndex = (ushort)profile, WeightMinG = minG, WeightMaxG = maxG, BaseValue = value,
                                  MinBaitRating = minBait, WeightSkew = 2.2f, LengthShapeK = 1.0f, BiteWaitMod = waitMod,
                                  HookWindow = 0f, MinDepth = minDepth, IsSpecial = false, Rarity = FishRarity.Common };

        public static FishSpeciesData[] Species() => new[]
        {
            Fish(ProfileSteady, 200, 800, 5, 1, 0.9f, 0.8f),       // bluegill
            Fish(ProfileBulldog, 1000, 4000, 12, 1, 1.2f, 1.5f),   // carp
            Fish(ProfileRunner, 2000, 7000, 35, 6, 1.4f, 2.5f),    // catfish
            Fish(ProfileDarter, 300, 1200, 8, 3, 1.0f, 1.2f),      // perch
            Fish(ProfileThrasher, 600, 2500, 20, 5, 0.9f, 2.0f),   // trout
        };

        public static JunkData[] Junk() => new[] { new JunkData { ValueCoins = 2 }, new JunkData { ValueCoins = 2 } };

        public static FishingDatabase Database(bool motion = true)
        {
            FishingTuning t = Tuning();
            return new FishingDatabase(Rods(), Baits(), motion ? MotionProfiles() : BaseProfiles(), Species(), Junk(), in t);
        }
    }
}
