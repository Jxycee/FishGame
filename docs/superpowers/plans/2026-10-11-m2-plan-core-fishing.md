# FishGame M2 - Section B: Core fishing (T5, T6, T7, T8, T9)

- **Date:** 2026-10-11. Branch `m2-fishing-core`. Author: section B (Opus, effort high).
- **Status (2026-10-12):** T5-T9 are implemented and committed. Where the code and review rulings differ from the task text below, C 16 wins: T8-R1 (an unsolicited commit while Casting plays the cancel beat; the text below that lists Casting as illegal for commits is superseded), T8-R2 (an invalid cast result remembers the cast id so the host's Cancelled commit is consumed silently), T9-R1 (`CatchValidator.WalkBoundedLineLength0`, `ActiveCast.CastDistance`), I-1/I-2 (non-finite yaw and host distance), `FishingRollValidation.IsSaneCatch` public, the `ForcedItemIndex` throw. Effect order in the T8 transitions is "cue first, then the send effect" (decision 9).
- **Binding contract:** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` ("C" below). Public names, paths and test names come from C 7.5-7.9, C 10.1-10.2 and C 12 and are not changed here. Every doubt raised is closed by a controller ruling, listed under "Contract questions" at the end.
- **Behaviour source:** spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` sections 9.1-9.9, 9.12, 9.13 and 3.2 (R63 fight motions, R64 clean-catch bonus).
- **Length note:** this file is about 1,260 lines, slightly over the 1,200 guide. The extra lines are the three code blocks the skeleton assigns to this section (`FightSim`, `RodStateMachine`, `FishingHostLedger.Result` + `CatchValidator.CleanClaimAccepted`).

## 0. Rules for every task in this section

**Read first:** C 2.8 (global constraints), C 5.2 (`FishingConstants`), your task's C 7.x block, and its C 10 rows. Then read the spec sections your task cites. Everything in this section is pure Core (`FishGame.Core`, folder `Assets/_Project/Core/Fishing/`, namespace `FishGame.Core`, one public type per file). Tests go in `Assets/_Project/Tests/EditMode/` (namespace `FishGame.Tests.EditMode`).

**C 2.8 constraints that bite here:**
- #16: no `UnityEngine.Random` and no `System.Random`. All randomness comes from a seeded `Pcg32` passed in or owned by the class. `FishGame.Core` has `noEngineReferences: true`, so never `using UnityEngine;` and never `Mathf`. Use `System.Math` / `System.MathF`.
- #2: the Write tool may decode `\uXXXX`. Code and comments are ASCII only. After writing, scan for non-ASCII bytes (command below). Spec text contains multiplication signs, minus signs, `>=` glyphs and degree marks; never paste them.
- #8: EditMode tests reach Core directly. Fishing tests never need Unity objects.
- #11: before any Unity run, the wrapper checks the editor lock. On `LOCKED` (exit 2), stop and report. Never kill Unity.
- #20 / #21: commit with explicit paths only. Logs and XML go in `$env:FG_OUT` (your scratchpad), never in the repo.
- #7: `core.autocrlf` is true. Write files with LF or CRLF; git stores LF.
- Unity generates every `.meta` file (including the new folder meta `Core/Fishing.meta`) on the first batchmode run. Never hand-write a `.meta`. Commit the generated ones.
- Do not edit any `.asmdef` (C 4). A missing reference is a contract bug: stop and report.

**Commands** (PowerShell tool, `timeout: 600000` on every Unity run; `<absolute scratchpad path>` is the only placeholder):
```powershell
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex from the task>"
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
```
Expected last lines: red = `FG RESULT test-edit FAIL ...` (compile errors naming the missing types count as red); green = `FG RESULT test-edit PASS total=N passed=N failed=0 ...`. Non-ASCII scan before every commit (Bash tool): `grep -nP '[^\x00-\x7F]' <your files>` must print nothing.

**TDD cadence (C 2.4):** write all tests for the task, one red run with the task filter, implement, one green run with the same filter, then the full `test-edit` once before committing.

**Shared test data.** T5 creates `Tests/EditMode/FishingTestData.cs` (complete code in T5). It holds the spec 9.6, 9.7 and 9.12 tables in ContentRegistry index order (ordinal id sort, confirmed in `Core/Content/RegistryIndex.cs`). T6-T9 tests only read it. Nobody edits it after T5. Runtime seeds live separately in T14's `FishingSeedData`.

**Fixed index map (ordinal sort):** rods `rod_carbon` 0, `rod_deepsea` 1, `rod_handmade` 2, `rod_twig` 3. Baits `bait_bare_hook` 0, `bait_grub` 1, `bait_minnow` 2, `bait_raw_meat` 3, `bait_shrimp` 4, `bait_worm` 5. Profiles `bulldog` 0, `darter` 1, `runner` 2, `steady` 3, `thrasher` 4. Species `bluegill` 0 (steady), `carp` 1 (bulldog), `catfish` 2 (runner), `perch` 3 (darter), `trout` 4 (thrasher). Junk `old_boot` 0, `tin_can` 1.

---

## Task T5 - Core fishing model

- **Read section 0 of this file first** (constraints, commands, index map, shared test data).
- **Goal:** every enum, stat struct and pure helper of C 7.5, plus `FishingConstants` (C 5.2) and the shared test data file.
- **Owned files (C 12):** `Core/Fishing/{RodState,FightPhase,FightOutcome,EscapeReason,CatchQuality,FishRarity,BodyShape,CastRejectReason,RodEventType,AnnounceKind,LineGaugeMode,RodForceReason,RodCue,RodStats,BaitStats,FishFightProfile,FishSpeciesData,JunkData,FishingTuning,FishingDatabase,WaterProbe,CastMath,FishPointMath,RodWireQuant,FishingConstants}.cs`, `Core/Player/ToolKind.cs`. Tests: `CastMathTests.cs`, `FishPointMathTests.cs`, `RodWireQuantTests.cs`, `FishingDatabaseTests.cs`. Also `Tests/EditMode/FishingTestData.cs`, a test helper listed in the C 12 T5 manifest (ruling B-Q1).
- **Shared edits (C 2.6):** none.
- **Depends on:** T1 committed (`Pcg32.MapUnitToRange`, the EditMode asmdef refs).
- **Implementer:** Sonnet. **Reviewer:** Sonnet.

### Behaviour
- **Enums and structs:** copy C 7.5 verbatim: names, underlying types, explicit values and field order. `ToolKind` lives in `Core/Player/ToolKind.cs`. Structs have public fields and no default tuning values (C 5.1). `RodStats.MaxLineOut(spoolExtra)` = `CastMaxDist + spoolExtra`. `BaitStats.EffRating(n)` = `min(MaxRating, Rating * max(1, n))`.
- **`FishingConstants`:** copy C 5.2 exactly, comments included.
- **`FishingTuning` (R65 / M2-D23):** C 7.5 adds `int PityAfterFailures` (0 = off) and `float PityStrainMult, PityGraceMult, PityHookWindowBonus`. Copy them from C 7.5; no Core default (C 5.1). `RodWireQuant` gains nothing.
- **`FishingDatabase`:**
  - The constructor copies every array, so later caller mutation has no effect. It throws `ArgumentNullException` for a null array, and `ArgumentException` for empty rods, baits or profiles, for any species with `FightProfileIndex >= profiles.Length`, or when no bait has `Unlimited`.
  - Junk may be empty.
  - Lists are exposed as `IReadOnlyList<T>` (for example `Array.AsReadOnly` over the copy).
  - `Tuning` is `public ref readonly FishingTuning Tuning => ref _tuning;` over a `private readonly FishingTuning _tuning`.
  - `UnlimitedBaitIndex` is the first bait with `Unlimited`.
- **`CastMath`:** use the C 7.5 comments as the formulas.
  - `ChargeNorm`: `held <= 0` gives 0. `u = held / chargeTime`. Ping-pong gives `1 - |(u % 2) - 1|`; otherwise `clamp01(u)`.
  - `Distance`: lerp with charge clamped to 0..1.
  - `FlightTime` = `FlightBaseSeconds + FlightSecondsPerMeter * d`.
  - `PitchApexMult` maps -30..45 degrees linearly onto 0.7..1.3, clamped.
  - `Apex` = `max(ApexMin, ApexPerMeter * d) * mult`.
  - `Landing`: yaw is in degrees, clockwise from +Z, so `x = ox + sin(yaw) * d` and `z = oz + cos(yaw) * d`.
  - `ArcPoint`: x and z lerp; `y = lerp(sy, ey, t) + 4 * (apex + 0.5 * |sy - ey|) * t * (1 - t)`, so `y(0.5) = max(sy, ey) + apex`.
  - `IsValidLanding` / `ProbeReason`: not `FirstHitIsWater` gives `NotWater`; `!HasBed || Depth < minLandDepth` gives `TooShallow`; otherwise `None`.
- **`FishPointMath.FishPoint`:** `angle = castYawDeg + lateral * lateralMaxDeg`, then `x = tipX + sin(angle) * lineLength` and `z = tipZ + cos(angle) * lineLength`. Positive lateral is right of the cast line (spec 9.1).
- **`RodWireQuant`:** NaN quantizes to 0. `QuantizeLateral` = `(sbyte)Math.Round(clamp(l, -1, 1) * 127, MidpointRounding.AwayFromZero)`; dequantize is `q / 127f` (-128 dequantizes below -1, so clamp it to -1). `QuantizeTension` = `(byte)Math.Round(clamp01(t) * 255, AwayFromZero)`; dequantize is `q / 255f`.

### FishingTestData.cs (complete code; write exactly)
```csharp
// Tests/EditMode/FishingTestData.cs - spec 9.6/9.7/9.12 tables for the Core fishing tests (T5; read-only for T6-T9).
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
            BiteWaitMin = 4f, BiteWaitMax = 18f, MinBiteWait = 1f, HookBeat = 0.25f, LandDistance = 2f,
            ReelStrainBurst = 0.30f, CounterRelief = 0.5f, SteerStrain = 0.03f, OutOfWindowStrain = 0.20f,
            SpoolStrain = 0.50f, SpoolExtra = 12f, RelaxRate = 0.45f, RelaxWhileReeling = 0.10f,
            ReelBurstPenalty = 1.0f, DiveRunMult = 1.5f, SnapGrace = 0.25f, SnapGraceAssist = 0.75f,
            StrainScale = 1.0f, StrainScaleAssist = 0.6f, PinEscapeTime = 5f, SlackEscapeTime = 12f,
            FishStaminaRegen = 0.04f, CalmCenterPull = 0.15f, LateralMaxDeg = 75f, HookedLeash = 3f,
            JunkDragSpeed = 3f, ResultTimeout = 3f, CommitTimeout = 3f, HookTolerance = 0.75f,
            MaxFightTime = 240f, RodStateHz = 15, CancelRefundsBait = true,
            CreelMaxEntries = 12, CreelMaxWeightG = 15000, CleanCatchMaxTension = 0.8f,
            PityAfterFailures = 3, PityStrainMult = 0.85f, PityGraceMult = 1.5f, PityHookWindowBonus = 0.3f,   // R65 / M2-D23 seeds
        };

        static RodStats Rod(byte tier, float cast, float reel, float strength, float window, float steer, float relax) =>
            new RodStats { Tier = tier, CastMaxDist = cast, ReelSpeed = reel, LineStrength = strength,
                           AngleWindow = window, SteerRate = steer, RelaxMult = relax };

        public static RodStats[] Rods() => new[]
        {
            Rod(2, 24f, 1.2f, 1.3f, 0.70f, 0.70f, 1.1f),   // rod_carbon
            Rod(3, 30f, 1.3f, 1.6f, 0.75f, 0.80f, 1.2f),   // rod_deepsea
            Rod(1, 18f, 1.0f, 1.0f, 0.60f, 0.60f, 1.0f),   // rod_handmade
            Rod(0, 14f, 0.9f, 0.8f, 0.50f, 0.50f, 0.9f),   // rod_twig
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
        public static FishFightProfile[] BaseProfiles() => new[]
        {
            Profile(0.60f, 0.60f, 0.50f, 0.06f, 1.0f, 3.0f, 2.0f, 4.0f, 0.0f, 0.35f),   // bulldog
            Profile(0.45f, 0.70f, 0.40f, 0.07f, 1.0f, 2.5f, 1.5f, 3.0f, 0.2f, 0.00f),   // darter
            Profile(0.85f, 0.80f, 0.80f, 0.05f, 1.0f, 2.0f, 2.5f, 4.0f, 0.1f, 0.25f),   // runner
            Profile(0.30f, 0.60f, 0.30f, 0.08f, 1.0f, 2.5f, 1.5f, 2.5f, 0.0f, 0.00f),   // steady
            Profile(0.70f, 1.00f, 0.60f, 0.09f, 1.0f, 2.5f, 1.5f, 3.0f, 0.5f, 0.00f),   // thrasher
        };

        // C 7.7 motion seeds on top of the base columns (FightMotionTests; machine and ledger tests).
        public static FishFightProfile[] MotionProfiles()
        {
            FishFightProfile[] p = BaseProfiles();
            p[ProfileSteady].BurstRamp = 0.6f;
            p[ProfileDarter].CalmMin = 0.3f; p[ProfileDarter].CalmMax = 0.8f;
            p[ProfileDarter].BurstMin = 0.4f; p[ProfileDarter].BurstMax = 0.9f;
            p[ProfileDarter].FlipRate = 1.0f; p[ProfileDarter].FlipMinGap = 0.3f;
            p[ProfileBulldog].CalmDrift = 0.12f; p[ProfileBulldog].CalmDriftRamp = 2.5f;
            p[ProfileThrasher].LateralJitter = 0.3f;
            p[ProfileRunner].BurstRamp = 0.8f;
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
```

### Tests (exact names from C 10.1)
| Test | Cases / expected |
|---|---|
| `CastMath_ChargeNorm_PingPongAtSampleTimes` | chargeTime 1, pingPong: held 0, 0.5, 1, 1.5, 2 gives 0, 0.5, 1, 0.5, 0 (within 1e-5); held -1 gives 0 |
| `CastMath_ChargeNorm_NoPingPong_Clamps` | held 0.5 gives 0.5; held 1, 1.5, 3 give 1 |
| `CastMath_Distance_MonotonicAndWithinHalfMeterOfFormula` | min 2.5, max 18, charge 0, 0.25, 0.5, 0.75, 1: strictly increasing, each within 0.5 m of `2.5 + 15.5 c` (exact in practice); charge 1.5 gives 18 |
| `CastMath_FlightTimeAndApex_MatchFormulas` | FlightTime(30) = 0.81; FlightTime(10.25) = 0.573; Apex(30, 1) = 3.6; Apex(10, 1.3) = 1.56 (1e-5) |
| `CastMath_Apex_NeverBelowPointSixMeters` | mult 1.0, distances 0, 1, 2.5, 5: all >= 0.6 and Apex(2.5, 1) = 0.6 (C 7.5 applies the floor before the pitch mult, so the test fixes mult = 1) |
| `CastMath_PitchApexMult_ClampedPointSevenToOnePointThree` | -90 gives 0.7, -30 gives 0.7, 7.5 gives 1.0, 45 gives 1.3, 90 gives 1.3 |
| `CastMath_Landing_UsesYawOnly` | origin (1, 2), yaw 0, d 10 gives (1, 12); yaw 90 gives (11, 2); yaw 180 gives (1, -8) (1e-4) |
| `CastMath_IsValidLanding_RequiresWaterFirstAndMinDepth` | water first + bed + depth 0.8 is valid (`None`); depth 0.79 gives `TooShallow`; no bed gives `TooShallow`; not water first gives `NotWater` (even with depth 5) |
| `FishPointMath_PositiveLateral_IsRightOfCastLine` | tip (0, 0), yaw 0, lateral 0.5, maxDeg 75, length 10: x > 0; lateral -0.5: x < 0; lateral 0: (0, 10) |
| `FishPointMath_LateralOne_IsAtMaxAngle` | yaw 0, lateral 1, length 10: (10 sin 75deg, 10 cos 75deg) = (9.659, 2.588) (1e-3) |
| `RodWireQuant_LateralAndTension_RoundTripWithinOneStep` | lateral -1..1 in steps of 0.01: within 1/127; tension 0..1 in steps of 0.01: within 1/255 |
| `RodWireQuant_OutOfRange_Clamps` | lateral 2 gives 127, -2 gives -127, NaN gives 0; tension 1.5 gives 255, -1 gives 0, NaN gives 0 |
| `FishingDatabase_BadProfileIndex_Throws` | species `FightProfileIndex = 5` with 5 profiles: `ArgumentException` |
| `FishingDatabase_NoUnlimitedBait_Throws` | all baits `Unlimited = false`: `ArgumentException` |
| `FishingDatabase_UnlimitedBaitIndex_IsBareHook` | `FishingTestData.Database().UnlimitedBaitIndex == BaitBareHook` (0); mutating the source arrays after construction does not change `Rods[2].CastMaxDist` |

Filter: `FishGame.Tests.EditMode.(CastMathTests|FishPointMathTests|RodWireQuantTests|FishingDatabaseTests)`.

### Steps
1. Write `FishingTestData.cs` and the four test files. Red run with the filter.
2. Write the 26 Core files. Green run with the filter.
3. Full `test-edit`. Non-ASCII scan.
4. Commit:
```powershell
git add Assets/_Project/Core/Fishing Assets/_Project/Core/Fishing.meta Assets/_Project/Core/Player/ToolKind.cs Assets/_Project/Core/Player/ToolKind.cs.meta Assets/_Project/Tests/EditMode/FishingTestData.cs Assets/_Project/Tests/EditMode/FishingTestData.cs.meta Assets/_Project/Tests/EditMode/CastMathTests.cs Assets/_Project/Tests/EditMode/CastMathTests.cs.meta Assets/_Project/Tests/EditMode/FishPointMathTests.cs Assets/_Project/Tests/EditMode/FishPointMathTests.cs.meta Assets/_Project/Tests/EditMode/RodWireQuantTests.cs Assets/_Project/Tests/EditMode/RodWireQuantTests.cs.meta Assets/_Project/Tests/EditMode/FishingDatabaseTests.cs Assets/_Project/Tests/EditMode/FishingDatabaseTests.cs.meta; git commit -m "m2(core): fishing model types, CastMath, FishPointMath, RodWireQuant, FishingDatabase" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Acceptance (reviewer)
- Every C 7.5 type is present, and only the C 12 T5 files changed (plus the helper). Enum values and field order match C 7.5 exactly. `FishingConstants` is byte-identical in values to C 5.2.
- `FishingTestData` numbers equal spec 9.6, 9.7 and 9.12 and the C 7.7 motion seeds, in the ordinal index order above.
- No engine references, no `Random`, ASCII only. Test count is 15, all green.

---

## Task T6 - Bite planner, catch math, placeholder roller

- **Read section 0 of this file first** (constraints, commands, index map, shared test data).
- **Goal:** the bite wait and nibble plan (spec 9.4), weight, quality and value (spec 9.8, including the R64 clean-bonus math), the M2 placeholder roll and the client sanity check (C 7.6).
- **Owned files (C 12):** `Core/Fishing/{CatchResult,FishingRoll,BitePlan,BitePlanner,CatchMath,RollRequest,PlaceholderRoller,FishingRollValidation}.cs`. Tests: `BitePlannerTests.cs`, `CatchMathTests.cs`, `PlaceholderRollerTests.cs`, `FishingRollValidationTests.cs`.
- **Shared edits:** none. **Depends on:** T5. **Implementer:** Sonnet. **Reviewer:** Sonnet. (Batch with T7: disjoint files.)

### Behaviour (signatures verbatim from C 7.6)
- **`BitePlanner.DepthMult(depth)`** = `lerp(1.0, 0.8, clamp01(depth / 6))`. **`WaitMultiplier`** = `baitMod * fishMod * eventMult * DepthMult(depth) * skillMult * (inHotspot ? 0.6 : 1)`.
- **`BitePlanner.Plan`, fixed draw order:**
  1. `waitBase = rng.Range(BiteWaitMin, BiteWaitMax)` (float). It requires max > min; T14 validates the data.
  2. `biteWait = max(MinBiteWait, waitBase * mult)`.
  3. `n = rng.Range(0, maxNibbles + 1)` (int). This draw is always made. Then `n = 0` if `biteWait < NibbleMinBiteWait` (1.4).
  4. For `k = 0..n-1`: `o_k = biteWait * (k + 1) / (n + 1) + rng.Range(-0.3f, 0.3f)`, clamped to `[0.5, biteWait - 0.6]`.
  5. Sort ascending into `Nibble0..2`. Unused slots stay 0.
- **`CatchMath`:** per C 7.6.
  - `Value` is computed in double: `Math.Max(1, (int)Math.Round(baseValue * (0.6 + 0.8 * (double)t) * qMult, MidpointRounding.AwayFromZero))` with `qMult` 1.0, 1.15 or 1.5.
  - `WeightG` uses `Math.Round(..., AwayFromZero)`.
  - `LengthMm` = `round(400 * Math.Cbrt(weightG / 1000.0) * k)`, AwayFromZero, clamped to the ushort range.
  - `RollFish`: one draw `u = rng.NextFloat()`; `t = SkewedT(u, Skew(s.WeightSkew, luck))`; then weight, quality, value and length from `t`; `out t`.
  - `Junk`: `IsJunk = true`, `ItemIndex = junkIndex`, weight 0, length 0, `Normal`, value = `junk.ValueCoins` (may be 0).
  - `WithCleanBonus`: junk is returned unchanged. Otherwise `Quality = UpgradeOneTier(q)` and `ValueCoins = Value(s.BaseValue, t, upgraded)`; weight and length are untouched.
- **`PlaceholderRoller.Roll`, fixed draw order:**
  1. **Item draw (always one `NextFloat`, even when forced).**
     - `bait = db.Baits[req.BaitIndex]` and `eff = bait.EffRating(max(1, req.LoadedCount))`.
     - Weights: every eligible species gets 1 (`IsEligible`: `eff >= MinBaitRating && depth >= MinDepth`). Junk gets `JunkWeight(eff) * bait.JunkMult`, or 0 when `db.Junk.Count == 0`.
     - Walk the species in index order, then junk, against `u * total`.
     - Overrides: `ForceJunk` picks junk; `ForcedItemIndex >= 0` picks that species with no eligibility check.
     - If nothing is eligible and no override applies, pick junk. If junk is then needed but `db.Junk` is empty, throw `InvalidOperationException` (T14 guarantees two junk items).
  2. If junk: `junkIndex = rng.Range(0, junkCount)`, which is always drawn, even for a single item. If fish: `CatchMath.RollFish` (one draw).
  3. `BitePlanner.Plan` with `WaitMultiplier(bait.BiteSpeedMod, junk ? 1f : species.BiteWaitMod, req.Depth)` and `bait.MaxNibbles`. It is always drawn. If `ForcedBiteWait > 0` the plan is then overwritten: `BiteWait = ForcedBiteWait`, `NibbleCount = 0`, nibbles 0.
  4. `FightSeed = rng.NextUInt()`, with 0 replaced by 1.
  5. `WeightT` = t (junk 0). `SizeClass = CatchMath.SizeClass(t)` (junk 0). `HookWindow = species.HookWindow`, overridden to 1.0 when `Twitchy` and not junk (junk 0).
- **`FishingRoll.Pity` (R65 / M2-D23):** C 7.6 adds `public bool Pity;` to `FishingRoll`. `PlaceholderRoller.Roll` never sets it (it stays false, no extra draw, draw order unchanged); the host ledger sets it at accept (T9). `FishingRollValidation.IsSane` ignores it. `PlaceholderRoller_SameSeed_SameRoll` also asserts `Pity == false` on both rolls.
- **`FishingRollValidation.IsSane`:** exactly the C 7.6 comment.
  - Junk: `ItemIndex < db.Junk.Count` and `WeightG == 0`.
  - Fish: `ItemIndex < db.Species.Count` and weight within `[WeightMinG, WeightMaxG]`.
  - `Quality <= 2`; `BiteWait` finite and >= 0; `NibbleCount <= 3`; `FightSeed != 0`; `SizeClass <= 2`; `HookWindow` finite and >= 0.

### Tests (exact names from C 10.1)
Unless a case says otherwise, tests use `FishingTestData.Tuning()` / `Database()`, `var rng = new Pcg32(seed)` and seed 1234.

| Test | Setup / expected |
|---|---|
| `BitePlanner_TenThousandSamples_WithinBoundsAndFloor` | mult in {0.2, 1.0, 1.7}, maxNibbles 3: every `BiteWait >= 1.0`. For mult 1.0 and 1.7: `4 * mult <= BiteWait < 18 * mult`. For mult 0.2: at least one sample exactly 1.0 (the floor binds), none above 3.6. `NibbleCount <= 3` |
| `BitePlanner_Means_FollowBaitSpeciesAndDepthMultipliers` | `DepthMult(0) = 1`, `(3) = 0.9`, `(6) = 0.8`, `(12) = 0.8`. `WaitMultiplier(0.9, 1.4, 6) = 1.008`; `(1.3, 0.9, 0) = 1.17`; hotspot true gives x0.6. Mean `BiteWait` over 10,000 plans with mult 1.008 = 11.088 +/- 0.15; with mult 1.17 = 12.87 +/- 0.15 |
| `BitePlanner_Nibbles_OrderedInsideWaitAndClearOfBite` | 10,000 plans, maxNibbles 3, mult 1: for each k < n: `0.5 <= Nibble_k <= BiteWait - 0.6`, non-decreasing |
| `BitePlanner_ShortWait_ForcesZeroNibbles` | tuning copy with `BiteWaitMin 1.0, BiteWaitMax 1.3, MinBiteWait 1.0`, mult 1, maxNibbles 3, 1,000 plans: all `NibbleCount == 0`, `BiteWait < 1.4` |
| `BitePlanner_SameSeed_SamePlan` | two `Pcg32(99)`: identical plans; also identical `NextUInt()` afterwards (same draw count) |
| `CatchMath_WeightDistribution_MeanAndTrophyRate` | `RollFish(carp, 1, 0, ...)` x 20,000: Trophy rate 0.0468 +/- 0.006, Good rate 0.1604 +/- 0.01, mean t 0.3125 +/- 0.01 (u^2.2: 1 - 0.9^(1/2.2) = 0.0468; 1 - 0.6^(1/2.2) - 0.0468 = 0.1604; E = 1/3.2) |
| `CatchMath_Value_AwayFromZeroAndMinimumOne` | `Value(3, 0.5, Trophy) = 5` (3 x 1.0 x 1.5 = 4.5 rounds away from zero); `Value(0, 0, Normal) = 1`; `Value(1, 0, Normal) = 1`; `Value(12, 0.7, Good) = 16`; `Value(12, 0.7, Trophy) = 21` |
| `CatchMath_LengthMm_FollowsCubeRoot` | `LengthMm(1000, 1) = 400`; `(8000, 1) = 800`; `(125, 1) = 200`; `(1000, 1.25) = 500` |
| `CatchMath_SizeClass_FromWeightThirds` | t 0, 0.33 give 0; 0.34, 0.66 give 1; 0.67, 1 give 2 |
| `CatchMath_UpgradeOneTier_NormalGoodTrophyCapped` | Normal to Good, Good to Trophy, Trophy stays Trophy |
| `CatchMath_WithCleanBonus_RecomputesValueKeepsWeight` | carp t 0.7 Good value 16 becomes Trophy value 21, same `WeightG` and `LengthMm`; bluegill t 0.3 Normal (value 4) becomes Good (value 5) |
| `CatchMath_WithCleanBonus_JunkUnchanged` | `Junk(0, {2})` returned field-equal |
| `PlaceholderRoller_TenThousandRolls_FrequenciesWithinTwoPercentOfWeights` | worm (eff 3), depth 3: eligible bluegill, carp, perch (weight 1 each) plus junk 0.2: expected 0.3125 x 3 and 0.0625, each within 0.02 absolute over 10,000 rolls (one rng); never catfish or trout |
| `PlaceholderRoller_MinDepthAndMinBaitRating_Gate` | `IsEligible`: perch with eff 3 at depth 1.2 true, 1.19 false; trout eff 4 false, eff 5 at depth 2 true; catfish eff 6 at depth 2.49 false |
| `PlaceholderRoller_BareHookShallow_OnlySteadyBulldogOrJunk` | bare hook, depth 1.6, 5,000 rolls: only bluegill, carp or junk; junk share near 0.9 / 2.9 = 0.31 +/- 0.03 |
| `PlaceholderRoller_NothingEligible_ReturnsJunk` | bare hook, depth 0.5: every roll `IsJunk`, index in {0, 1}, weight 0, value 2, `WeightT` 0, `SizeClass` 0 |
| `PlaceholderRoller_FightSeed_NeverZero` | 10,000 rolls: `FightSeed != 0` |
| `PlaceholderRoller_Overrides_ForceBiteSpeciesJunkTwitchy` | `ForcedBiteWait 0.1`: BiteWait 0.1, NibbleCount 0. `ForcedItemIndex = SpeciesCatfish` with worm at depth 1: catfish. `ForceJunk`: junk. `Twitchy`: fish `HookWindow == 1.0`; with `ForceJunk` plus `Twitchy`, junk `HookWindow == 0` |
| `PlaceholderRoller_SameSeed_SameRoll` | two `Pcg32(7)` give field-equal rolls; afterwards the next `NextUInt` is equal too |
| `FishingRollValidation_SaneRoll_Passes` | 1,000 `PlaceholderRoller` rolls all pass |
| `FishingRollValidation_OutOfRangeFields_Rejected` | from a sane fish roll, one field changed per case: species index 5; junk index 2; weight max + 1; quality 3; `FightSeed` 0; `BiteWait` NaN; `BiteWait` -1; `NibbleCount` 4; `SizeClass` 3; `HookWindow` NaN; junk with weight 1 |

Filter: `FishGame.Tests.EditMode.(BitePlannerTests|CatchMathTests|PlaceholderRollerTests|FishingRollValidationTests)`.

### Steps
1. Write the four test files. Red run.
2. Implement the 8 files. Green run.
3. Full `test-edit` and the ASCII scan.
4. Commit with explicit paths (8 Core files + metas, 4 test files + metas): `git commit -m "m2(core): BitePlanner, CatchMath (clean bonus), PlaceholderRoller, FishingRollValidation" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`.

### Acceptance (reviewer)
- Draw order is exactly as written, both in `Plan` and in `Roll`.
- `MidpointRounding.AwayFromZero` is used everywhere spec 9.8 says so.
- Overrides do not shift the draw count.
- 21 tests, all green.

---

## Task T7 - Fight sim, motion parameters, tension cues

- **Read section 0 of this file first** (constraints, commands, index map, shared test data).
- **Goal:** `FightSim` exactly per spec 9.5, plus junk mode and the C 7.7 motion parameters (R63), the R65 pity multipliers (`pity` ctor argument: strain scale x `PityStrainMult`, snap grace x `PityGraceMult`; no RNG), forced-phase seams, and `TensionCues` (spec 9.11 table).
- **Owned files (C 12):** `Core/Fishing/{FightState,FightSim,TensionBand,TensionCueState,TensionCues}.cs` and `Core/AssemblyInfo.cs`. In `AssemblyInfo.cs`, add `using System.Runtime.CompilerServices;` and `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]`, and keep the existing marker class. Tests: `FightSimTests.cs`, `FightMotionTests.cs`, `TensionCuesTests.cs`.
- **Shared edits:** none. **Depends on:** T5 (and T1's `Pcg32.MapUnitToRange`). **Implementer:** Sonnet. **Reviewer:** Sonnet.
- **Input type:** `FightInput` already exists, as M1's `Core/Input/FightInput.cs` (`readonly struct`, `Steer`, `Reel`, ctor `(float steer, bool reel)`). Use it; do not create another.

### Decisions fixed here (also listed in Contract questions)
1. **Tick order:** elapsed, then flips, then P / ramp, then lateral, then stamina, then line, then tension, then timers and near-snap, then endings, then exhaustion, then the phase schedule. Phase changes happen at the **end** of a tick, so a new phase's physics starts on the next tick. After 40 ticks from spawn the phase is Burst.
2. **Ending priority on the same tick:** Landed, then Snapped, then Pinned, then ThrewHook. A fish reeled in on the same tick as a snap lands, which matches the spirit of "tension may touch 1.0 inside the grace window and the fish can still land".
3. **Side choice (literal spec 9.5, ruling B-Q4):** one draw `s`. Centre (`|lateral| < 0.1`): `s < 0.5` gives -1, else +1. Otherwise `away = -sign(lateral)`: with probability 0.75 (`SideRunAgainstChance`) the run is `away`; with the remaining 0.25 the side is random (50/50), so overall 87.5% away and 12.5% toward. One draw only (no extra draw, the draw table is unchanged): `s < 0.75 + 0.25 * 0.5` (that is `s < 0.875`) gives `away`, else `-away`.
4. **Flip gap:** the gap counter is set to `FlipMinGap` at burst start, so a flip may happen on the first burst tick. Flips are at least `FlipMinGap` apart within a burst.
5. **Epsilon:** `FightSim.TimeEps = 1e-4f` (internal) for every duration comparison: phase timer `<= TimeEps`, and snap / pin / slack timers `>= limit - TimeEps`. Tests count ticks.
6. **Calm drift** uses the stamina after this tick's stamina update. `EffectivePull` is the unramped P of the last tick.
7. **Test-only internals:** `internal int DrawCount` (every RNG draw), `internal float BurstLateralMult`, `internal const float TimeEps` (named in C 7.7, ruling B-Q2).

### RNG draw order (fixed; seeds depend on it)
All draws come from one `Pcg32(seed)` owned by the sim. `Next01()` = `NextFloat()` (one `NextUInt`). `Uniform(min, max)` = one `NextFloat` mapped by `Pcg32.MapUnitToRange(u, min, max)` when `max > min`, else `min` (the draw is still made). With max > min this gives the same numbers as `Pcg32.Range(float)` after T1.

| Event | Draws, in order |
|---|---|
| Constructor, `ForJunk`, `ForcePhase`, `ForceState` | none |
| Burst start (calm timer expired, not exhausted) | 1 `Next01` dive test (`< DiveChance`); 2 `Next01` side choice; 3 `Uniform(BurstMin, BurstMax)` duration; 4 **only if `LateralJitter > 0`**: `Uniform(1 - j, 1 + j)` lateral multiplier. Draws 1-3 are always made (dives too), so profiles without jitter draw exactly 3. |
| Calm start (burst timer expired) | 1 `Uniform(CalmMin, CalmMax)` |
| Exhaustion (burst ends, calm forever) | none; the exhausted calm never expires |
| Each tick of a side run with `FlipRate > 0` and gap satisfied | 1 `Next01` (`< FlipRate * dt` flips) |
| Junk sim | none, ever |

### FightSim code (write exactly)
```csharp
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
```
`FightState` is the C 7.7 struct (public fields, `PascalCase`). `Core/AssemblyInfo.cs` gains the `InternalsVisibleTo` line.

### TensionCues (behaviour)
- **`BandOf(tension, inGrace)`:** `inGrace` gives `Grace`. Otherwise `< 0.3` Rest, `< 0.6` Bend, `< 0.8` Creak, else Warn.
- **`Evaluate(tension, inGrace, reelSpeed, cameraMotion01)`:** clamp tension to 0..1 and `cameraMotion01` to 0..1. Let `m = cameraMotion01`.
  - `Band = BandOf(...)`.
  - `RodBend01 = tension < 0.3 ? 0 : clamp01((tension - 0.3) / 0.7)`.
  - `ReelClickRate = reelSpeed * 6`.
  - `CreakVolume01 = clamp01((tension - 0.6) / 0.4)`, 0 below 0.6.
  - `CreakPitch = 1 + 1.1 * clamp01((tension - 0.6) / 0.4)`.
  - `LineWarm01 = clamp01((tension - 0.6) / 0.2)`.
  - `LineWarn = LineDash = band >= Warn`.
  - `Shake = band >= Warn && m > 0`.
  - `CrackNow = band == Grace`.
  - Rumble in Warn: `r = clamp01((tension - 0.8) / 0.2)`, `RumbleLow = 0.6 r`, `RumbleHigh = 0.4 r`. In Grace: `RumbleLow = 0.8`, `RumbleHigh = 0.6`. Otherwise 0. Both are then multiplied by `m`.
  - `TraumaPerSecond = band >= Warn ? 0.1 * m : 0`.

### Hand-worked checks as numbers (spec 9.5; Handmade rod, base profiles)
These use `weightT = 0.375`, so `WeightFactor = 1.0` and `P = pullStrength` at full stamina. The side-run snap cases set `FlipRate = 0` on their profile copy: the spec arithmetic assumes no flip, and Runner (0.1 /s) or Thrasher (0.5 /s) could flip inside 3 s. These are the only departures from the plain 9.6 columns, and they are noted in each test comment. Each case starts with `ForcePhase(Burst, +1, 10)` on a fresh sim with line 18 m.
- **Runner, reel, no steer:**
  - Lateral 0.68 /s, so it leaves the 0.6 window on tick 45 (0.90 s).
  - Tension rises 0.255 /s (0.0051 per tick) to 0.2244 after 44 ticks, then 0.455 /s (0.0091 per tick). It reaches 1.0 on tick 130 (2.60 s).
  - The snap timer reaches 0.25 on tick 142: **snap at 2.84 s**.
  - The 0.8 (warn) band is reached about 2.16 s (the spec's "creaks ~2.0").
- **Runner, full counter-steer (steer -1, reel) for a forced 4.0 s run:**
  - Stamina 1 - 0.05 t, so P = 0.85 s(t).
  - Tension rate 0.1275 s(t) + 0.03 = 0.1575 /s at the start.
  - After 4 s: 0.1275 x 3.6 + 0.12 = **0.579**, no snap. Lateral stays inside the window (peak about 0.1).
- **Bulldog greedy:** P 0.6, lateral 0.36 /s, window exit tick 84, tension 0.2988, then 0.0076 per tick. Tension 1 on tick 176, **snap tick 188 (3.76 s)**. The spec says "Carp ~3.8".
- **Thrasher greedy:** P 0.7, lateral 0.7 /s, window exit tick 43, tension 0.1764, then 0.0082 per tick. Tension 1 on tick 143, **snap tick 155 (3.10 s)**. The spec says "Trout ~3.1".
- **Steady, trophy (`weightT 1.0`, P 0.375), reel, no steer, `ForcePhase(Burst, +1, 2.5)` for 125 ticks:**
  - Lateral reaches 0.5625 (< 0.6).
  - Tension reaches 0.281 (< 0.3).
  - "Bluegill effectively can't snap" holds.
- **Rest from 0.9 (no reel, lateral 0, Calm or dive):** -0.45 /s, so after 75 ticks tension is **0.225** (< 0.3).

### Tests (exact names from C 10.1)
Common setup: `FishingTestData` with Handmade rod and tuning; seed 4242 unless stated; `In(steer, reel) = new FightInput(steer, reel)`. "Profile X base" means `BaseProfiles()[X]`; "X motion" means `MotionProfiles()[X]`. Tolerance is 1e-4 for single-tick deltas unless stated.

| Test | Setup / input | Expected |
|---|---|---|
| `FightSim_FirstPhase_IsCalmLeadIn` | new sim (bulldog base), no input | Calm, `PhaseTimer` 0.8, `DrawCount` 0; after 39 ticks still Calm and 0 draws; after tick 40 Burst and `DrawCount` 3 |
| `FightSim_RunnerReelNoSteerSideRun_SnapsBetween2p5And3p2Seconds` | runner base with FlipRate 0, weightT 0.375, line 18, `ForcePhase(Burst, +1, 10)`, `In(0, true)` until ended | Snapped, `Elapsed` in [2.5, 3.2] (arithmetic says 2.84); `Lateral > 0.6` first on tick 45 (+/- 1) |
| `FightSim_RunnerFullCounterFourSecondRun_NoSnapTensionPointFiveToPointSeven` | same sim, `ForcePhase(Burst, +1, 4.0)`, `In(-1, true)` x 200 ticks | `Outcome` None; `Tension` in [0.5, 0.7] (about 0.579); `MaxTension < 1`; `|Lateral| < 0.6` throughout |
| `FightSim_SteadyReelNoSteerRun_StaysBelowPointThreeTensionAndPointSixLateral` | steady base, weightT 1.0, `ForcePhase(Burst, +1, 2.5)`, `In(0, true)` x 125 | max tension < 0.3, max `|Lateral|` < 0.6 over all ticks |
| `FightSim_RestFromPointNine_BelowPointThreeAfterOnePointFiveSeconds` | `ForceState{Tension 0.9, Calm, PhaseTimer 10, FishStamina 1, LineLength 10}`, `In(0, false)` x 75; repeat with `Phase Burst, BurstDir 0, PhaseTimer 10` (dive) | both cases: tension 0.225 +/- 1e-3 |
| `FightSim_PositiveBurstDirWithLeftSteer_ReducesLateralAndDrainsStamina` | bulldog base, weightT 0.375, `ForceState{Lateral 0.5, Burst, Dir +1, PhaseTimer 10, Stamina 1, Line 10}`, `In(-1, false)` x 10 | Lateral 0.5 - (0.6 - 0.36) x 0.2 ~ 0.452 (+/- 2e-3, P falls slightly as stamina drains); stamina 1 - 0.06 x 0.2 = 0.988 (+/- 1e-4) |
| `FightSim_Dive_DrainsStaminaOnlyWhileNotReeling` | bulldog base, `ForcePhase(Burst, 0, 10)`, line 10 | 10 ticks `In(0, true)`: stamina 1.0 exactly; then 10 ticks `In(0, false)`: 0.988 |
| `FightSim_Exhausted_StopsNewBursts` | runner base, `ForceState{Stamina 0.06, Burst, Dir 0, PhaseTimer 10, Line 29}`, `In(0, false)` until `Exhausted`, then `In(0, true)` x 500 | becomes Calm on the exhaustion tick; Calm on all 500 ticks; `DrawCount` unchanged after exhaustion |
| `FightSim_Pinned_EscapesAtFiveSeconds` | steady base, `ForceState{Lateral 1, Burst, Dir +1, PhaseTimer 10, Stamina 1, Line 10}`, `In(0, false)` | Escaped / Pinned on tick 250 (`Elapsed` 5.0 +/- 0.021) |
| `FightSim_NoReel_EscapesThrewHookAtTwelveSeconds` | steady base, `ForceState{Calm, PhaseTimer 100, Stamina 1, Line 10}`, `In(0.2, false)` | Escaped / ThrewHook on tick 600 (`Elapsed` 12.0 +/- 0.021) |
| `FightSim_Spooled_ClampsLineAndAddsStrain` | runner base, weightT 0.375. A: `ForceState{Burst, Dir +1, PhaseTimer 10, Line 29.99, Stamina 1}`; B: same with Line 20; one tick `In(0, true)` each | A: `Spooled`, `LineLength == 30`; `dTension(A) - dTension(B) = 0.5 x 0.85 x 0.02 = 0.0085` |
| `FightSim_TensionTouchesOneShorterThanGrace_DoesNotSnap` | runner base, `ForceState{Tension 1, Lateral 0.9, Burst, Dir +1, PhaseTimer 10, Stamina 1, Line 15}` | 12 ticks `In(0, true)`: None, `SnapTimer` 0.24; tick 13 `In(0, false)`: tension 0.995, `SnapTimer` 0, None; `MaxTension == 1`. Control from the same state: 13 ticks `In(0, true)` gives Snapped on tick 13 |
| `FightSim_LineAtLandDistanceWhileReeling_Lands` | bulldog base, start line 2.51, `In(0, true)` | not ended after 25 ticks; Landed on tick 26. Separate sim at line 1.5 with `In(0, false)` x 5: still None |
| `FightSim_NeverReeling_EndsBySlackEscape` | steady base, seed 7, natural fight, `In(0, false)` | Escaped / ThrewHook, `Elapsed` 12.0 +/- 0.021 |
| `FightSim_SameSeedAndInputs_IdenticalState` | two runner-base sims, seed 99, input `In(sin(i x 0.05), i % 7 != 0)` x 3000 or until ended | every `FightState` field equal on every tick |
| `FightSim_Assist_UsesLongGraceAndLowerStrain` | runner base, weightT 0.375, assist true. A: `ForcePhase(Burst, +1, 10)`, one tick `In(0, true)`. B: `ForceState{Tension 1, Lateral 0.9, Burst, Dir +1, PhaseTimer 10, Stamina 1, Line 15}`, `In(0, true)` | A: dTension = 0.6 x 0.255 x 0.02 = 0.00306; B: no snap on tick 37, Snapped on tick 38 (0.76 s) |
| `FightSim_NearSnapRecovery_Counted` | runner base, weightT 0.375, `ForceState{Tension 0.79, Lateral 0.7, Burst, Dir +1, PhaseTimer 10, Stamina 1, Line 15}`; 2 ticks `In(0, true)` (to 0.808), then `In(0, false)` x 80 | `NearSnapRecoveries == 1` (falls -0.25 /s to under 0.5 near tick 62); a second rise to 0.75 then rest leaves it at 1 |
| `FightSim_Junk_DragsAtJunkSpeedWithoutTension` | `ForJunk(handmade, tuning, 5, 10)` | `In(0, true)` x 50: line 7.0 +/- 1e-3, tension and `MaxTension` 0, `DrawCount` 0; continuing, Landed on tick 134 (line 1.96); a separate junk sim with `In(0, false)` gets ThrewHook on tick 600 |
| `FightSim_GreedySideRun_SnapTimesMatchSpecChecks` (extra, `[TestCase]` x 3) | runner / bulldog / thrasher base, FlipRate 0, weightT 0.375, `ForcePhase(Burst, +1, 10)`, `In(0, true)` | snap `Elapsed` 2.84 / 3.76 / 3.10, each +/- 0.06 |
| `FightMotion_ZeroParameters_MatchBaseModel` | base profiles (every motion field 0, gap 0.5), one tick each after `ForceState{Lateral 0.2, Stamina 1, Line 10}` + `ForcePhase`: (a) side run +1 `In(0.5, true)`; (b) dive `In(0, false)`; (c) Calm `In(0, false)` | (a) dLateral = (P x Ls + 0.5 x 0.6) x dt; dLine = (RunSpeed x P - max(0.1, 1 - P)) x dt; dTension = (0.3 x P + 0.03 x 0.5) x dt (steer +0.5 with burstDir +1 means against = 0; lateral stays inside the window). (b) dLine = RunSpeed x P x 1.5 x dt. (c) dLine == 0 (no drift). `DrawCount` +3 at a burst start and +1 at a calm start, for all 5 base profiles. Expected values come from the 9.5 formulas with the profile's numbers, never from `FightSim` internals; tolerance 1e-5 |
| `FightMotion_SteadyBurstRamp_LateralPushRampsOverRampTime` | steady motion, weightT 0.375, `ForcePhase(Burst, +1, 2.0)`, `In(0, false)` x 45 | dLateral_k = 0.3 x 0.6 x min(1, k x 0.02 / 0.6) x 0.02 for k = 1, 15, 30, 45 (1e-6); non-decreasing; equal for k >= 30 |
| `FightMotion_RunnerBurstRamp_LineOutRateRampsUp` | runner motion, weightT 0.375, `ForcePhase(Burst, +1, 3.0)`, line 10, `In(0, false)` x 60 | dLine_k = 0.8 x 0.85 x min(1, k x 0.02 / 0.8) x 0.02: dLine_10 < dLine_20 < dLine_40 == dLine_60 (1e-5) |
| `FightMotion_BulldogCalmDrift_LineCreepsOutDuringCalm` | bulldog motion, `ForcePhase(Calm, 0, 5.0)`, line 10, `In(0, false)` x 250 | line grew 0.12 x (2.5 / 2 + 2.5) = 0.45 +/- 0.01; dLine_1 < dLine_125; `Spooled` false throughout |
| `FightMotion_BulldogCalmDrift_StopsWhenExhausted` | bulldog motion, `ForceState{Stamina 0.06, Burst, Dir 0, PhaseTimer 10, Line 10}`, `In(0, false)` until `Exhausted`, then x 100 more | Calm and `Exhausted`; line constant over the 100 ticks |
| `FightMotion_DarterMicroBursts_FlipRateInRangeAndGapRespected` | darter motion, seeds 1..20. Each: `ForceState{Burst, Dir +1, PhaseTimer 1000, Stamina 1, Line 10}`, 750 ticks of `In(-clamp(Lateral x 5, -1, 1), i % 100 == 0)` | over all seeds: hazard = flips / (flip draws x 0.02) in [0.8, 1.2] (flip draws = `DrawCount` delta, the only draws in a forced long burst); every gap between consecutive flips >= 0.3 - 1e-4 s (tick count x 0.02); no fight ends. Natural darter fight (seed 3, 60 s, same input with phases unforced): every burst duration in [0.4, 0.9], every calm after the lead-in in [0.3, 0.8] (each +/- 0.021) |
| `FightMotion_ThrasherJitter_LateralSpeedVariesPerBurstWithinBounds` | thrasher motion, one sim; 60 times: `ForceState{Calm, PhaseTimer 0.02, Lateral 0, Stamina 1, Line 10}`, one tick `In(0, true)` (burst starts), read `BurstLateralMult`, one more tick `In(0, true)` | every mult in [0.7, 1.3]; max - min > 0.2; on side runs, dLateral of the second tick = `BurstDir x EffectivePull x 1.0 x mult x 0.02` (1e-6); `DrawCount` grows by 4 per burst start (plus flip draws) |
| `FightMotion_Pity_SnapsLaterThanNormalSameSeed` (R65) | runner base with FlipRate 0, weightT 0.375, line 18, `ForcePhase(Burst, +1, 10)`, `In(0, true)` until ended; two sims with the same seed, one built with `pity: true`; the tuning has `PityStrainMult 0.85`, `PityGraceMult 1.5` | both Snapped; normal at about 2.84 s; pity at about 3.36 s (tension rates x0.85: 1.0 at about 2.98 s, grace 0.25 x 1.5 = 0.375); assert pity `Elapsed` > normal `Elapsed` + 0.4 and in [3.2, 3.5]; `DrawCount` equal at every tick (Pity draws nothing); a sim built without the argument equals `pity: false` |
| `FightMotion_AllProfiles_SameSeedSameState` | each motion profile, two sims with seed 555, the input script from `SameSeedAndInputs`, x 3000 | identical `FightState` every tick; a seed-556 sim differs from seed 555 somewhere |
| `TensionCues_BandBoundaries_MatchSpecTable` | 0.29 Rest, 0.3 Bend, 0.59 Bend, 0.6 Creak, 0.79 Creak, 0.8 Warn, 1.0 (not in grace) Warn, 1.0 in grace Grace | as listed; `CrackNow` only in Grace; `LineWarn` from Warn |
| `TensionCues_CreakPitch_RisesToTwoPointOne` | tension 0.5, 0.6, 0.8, 1.0 | pitch 1.0, 1.0, 1.55, 2.1 (1e-5), non-decreasing |
| `TensionCues_CameraMotionZero_RemovesShakeTraumaAndRumble` | tension 0.9 and 1.0 in grace, `cameraMotion01` 0 | `Shake` false, trauma 0, both rumbles 0; `CreakVolume01`, `LineWarn` unchanged versus motion 1 |

Line-length deltas are differences of floats near 10 m, so compare them with tolerance 1e-5 (not 1e-6).

Filter: `FishGame.Tests.EditMode.(FightSimTests|FightMotionTests|TensionCuesTests)`.

### Steps
1. Write the three test files (any helper stays private in the test class). Red run.
2. Write `FightState.cs`, `FightSim.cs` (code above), `TensionBand.cs`, `TensionCueState.cs`, `TensionCues.cs`, and the `AssemblyInfo.cs` line. Green run.
3. If a hand-check test fails, do not widen its bounds. Recheck the tick order against the code block and report the numbers.
4. Full `test-edit`, ASCII scan, then commit with explicit paths: `git commit -m "m2(fight): FightSim (spec 9.5 + motions, fixed draw order), TensionCues" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`.

### Acceptance (reviewer)
- `FightSim` matches the code block line for line in logic and order.
- The draw table is implemented exactly, and `DrawCount` proves it in the tests.
- Hand-check tests use the stated weightT and FlipRate overrides and assert only what their names promise.
- 31 tests (30 required + 1 extra), all green. The ctor's `pity` parameter defaults to false so every other call site is unchanged.

---

## Task T8 - Rod state machine

- **Read section 0 of this file first** (constraints, commands, index map, shared test data).
- **Goal:** the owner-run `RodStateMachine` (C 7.8), with every spec 9.2 row mapped to one code path and one C 10.2 test, plus an effects queue and a `FightReport` on results.
- **Owned files (C 12):** `Core/Fishing/{RodFrame,FightReport,RodEffectKind,RodEffect,RodStateMachine}.cs`. Test: `RodStateMachineTests.cs`.
- **Shared edits:** none. **Depends on:** T5, T6, T7. **Implementer:** Sonnet. **Reviewer:** Opus high (acceptance criterion 2).

### Decisions fixed here (also in Contract questions)
1. **Edges and levels.** Readying needs a `SecondaryPressed` edge seen in Idle, and Charging needs a `PrimaryPressed` edge seen in Ready (spec 9.2 edge rules). Releases are level-based: `!SecondaryHeld` counts as Secondary released, and `!PrimaryHeld` (or `PrimaryReleased`) counts as Primary released. A missed release edge therefore never leaves the rod stuck. Tests that stay in Ready or Charging must keep `SecondaryHeld = true`.
2. **The Landing cue fires when the flight ends** (bobber touches water), whether or not the result is held. Casting becomes Waiting when both hold, and the bite timer starts at 0 at that moment, which is `max(landing, resultReceived)`.
3. **Pause** (`frame.Paused`) gates only `Tick` and `TickFight` (no time advances, no input is read). `OnCastResult`, `OnCommit` and `ForceEnd` still apply while paused: solo host replies arrive regardless.
4. **`Assist`** is captured into the cast at release, and the fight is built from that snapshot. Changing `Assist` later affects only the next cast.
5. **Unsolicited commits** (in Waiting, Bite or Hooked):
   - `finalOutcome == Cancelled` gives Escaped with `Outcome = Cancelled`, `Recalling = true` and cue `Recall` (C M2-D5).
   - Any other outcome gives Escaped with `Outcome = Escaped`, `Reason = finalReason` (Invalid if None), `SendRodEvent(Escaped)` and cue `Escaped`.
   - Neither sends `SendResult`. `accepted` is not used by the machine.
6. **Invalid cast results** never emit `SendCancel` (the host rejected; nothing to refund). A roll that fails `TryToCore` is handled by T17 and T18 (BF-5): T17's `CastResultRpc` handler sends the single `CancelRpc` and raises `CastResultReceived(valid:false)`; T18 only reacts by calling `OnCastResult(id, false, default, default)`. The machine itself never sends `SendCancel` for an invalid result and nothing here sends `CancelRpc` (C 8.2 note).
7. **`SendRodEvent` payloads:** `SizeClass = Roll.SizeClass` for Snapped only, 0 for the others (C 7.8 wording). Nibble events carry 0. The `Cue(Snapped)` effect carries the same `SizeClass` (BF-1), because the owner's local snap cue reads it; every other cue carries 0.
8. **`IgnoredEventCount`** (`public int IgnoredEventCount { get; private set; }`, ruling E-Q5 supersedes B-Q12) counts every ignored call: a mismatched `castId`, a wrong state for `OnCastResult`, `OnCommit` or `TickFight`, a duplicate commit, and `SetRod` outside Idle. The machine never logs. T18 reads the counter after each `Tick` / `TickFight` / `OnCastResult` / `OnCommit` and, in debug builds, logs (not warns) once per state when it grew; replies to the owner's own cancel never count (decision 10).
9. **Effect order** within one transition follows the code (cue first where shown, then sends). T18 relies on `SendHooked` before `SendRodEvent(HookSet)`, and `SendResult` before `SendRodEvent(Snapped/Escaped)`.
10. **Replies to our own cancel are not illegal.**
    - After `SendCancel` (Waiting cancel, maxLineOut, result timeout, `ForceEnd` from Casting/Waiting/Bite), the machine is already Idle. The host's `CommitRpc` for that `castId` would otherwise count as ignored, and T18 would warn on every ordinary cancel (C 2.8 #9).
    - The machine remembers the last cancelled id. A commit for it is consumed silently and clears the memory. A late `CastResult` for it is consumed silently too.
    - These replies never touch `IgnoredEventCount`, in any state.

### RodStateMachine code (write exactly)
```csharp
// Core/Fishing/RodStateMachine.cs - owner-run rod state machine (spec 9.2; C 7.8). Pure C#.
// Authority: the owning client runs it; the host only validates (FishingHostLedger). Row map: plan T8 / C 10.2.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class RodStateMachine
    {
        private const float Eps = 1e-4f;
        private readonly FishingTuning _t;
        private readonly List<RodEffect> _effects = new List<RodEffect>(8);
        private RodStats _rod;
        private FishFightProfile _profile;
        private float _chargeHeld;
        private bool _flightOver;        // Landing cue emitted for this cast
        private int _nibblesDone;
        private bool _commitReceived;
        private bool _castAssist;        // Assist captured at release
        private uint _cancelledCastId;   // last cast we sent SendCancel for (0 = none); never cleared by ResetCast

        public RodStateMachine(in FishingTuning tuning) { _t = tuning; }

        public RodState State { get; private set; }
        public float StateTime { get; private set; }
        public uint CastId { get; private set; }
        public float ChargeNorm { get; private set; }
        public float CastDistance { get; private set; }   // R67: the cast distance FROM THE ROD TIP; the landing is CastTipReach further from the root
        public bool MarkerOn => State == RodState.Ready || State == RodState.Charging;
        public bool HasResult { get; private set; }
        public FishingRoll Roll { get; private set; }
        public float BiteTimer { get; private set; }
        public float BiteWait { get; private set; }
        public FightSim Fight { get; private set; }
        public FightOutcome Outcome { get; private set; }
        public EscapeReason Reason { get; private set; }
        public bool Recalling { get; private set; }
        public bool Assist { get; set; }
        public int IgnoredEventCount { get; private set; }

        public void SetRod(in RodStats rod)
        {
            if (State != RodState.Idle) { IgnoredEventCount++; return; }
            _rod = rod;
        }

        public void Tick(float dt, in RodFrame f)
        {
            if (f.Paused || !(dt > 0f)) return;
            StateTime += dt;
            switch (State)
            {
                case RodState.Idle:                                                    // row: Idle / Secondary down
                    if (f.SecondaryPressed) Enter(RodState.Readying);
                    break;
                case RodState.Readying:
                    if (!f.SecondaryHeld) Enter(RodState.Idle);                        // row: Readying / released
                    else if (StateTime >= FishingConstants.ReadyHoldSeconds - Eps)    // row: Readying / held 0.25 s
                    { Enter(RodState.Ready); Cue(RodCue.Ready); }
                    break;
                case RodState.Ready:
                    if (!f.SecondaryHeld) Enter(RodState.Idle);                        // row: Ready / released
                    else if (f.PrimaryPressed)                                         // row: Ready / fresh Primary
                    { Enter(RodState.Charging); _chargeHeld = 0f; ChargeNorm = 0f; Cue(RodCue.ChargeStart); }
                    break;
                case RodState.Charging: TickCharging(dt, f); break;
                case RodState.Casting: TickCasting(); break;
                case RodState.Waiting: TickWaiting(dt, f); break;
                case RodState.Bite: TickBite(f); break;
                case RodState.Hooked:                                                  // rows: Hooked / leash, entered water
                    if (f.InWater || f.HookDisplacement > _t.HookedLeash) EndEscaped(EscapeReason.Dropped);
                    break;
                default: TickTerminal(); break;                                        // Landed, Snapped, Escaped
            }
        }

        private void TickCharging(float dt, in RodFrame f)
        {
            if (!f.SecondaryHeld) { Enter(RodState.Idle); ChargeNorm = 0f; return; }  // row: Charging / Secondary released
            _chargeHeld += dt;
            ChargeNorm = CastMath.ChargeNorm(_chargeHeld, _t.CastChargeTime, _t.ChargePingPong);
            if (f.PrimaryHeld && !f.PrimaryReleased) return;
            if (ChargeNorm >= FishingConstants.MinChargeNorm && f.MarkerValid)        // row: Charging / valid release
            {
                CastId++;
                CastDistance = CastMath.Distance(ChargeNorm, _t.CastMinDist, _rod.CastMaxDist);
                _castAssist = Assist;
                ResetCast();
                Enter(RodState.Casting);
                Cue(RodCue.CastRelease);
                Add(new RodEffect { Kind = RodEffectKind.SendCastRequest, CastId = CastId, ChargeNorm = ChargeNorm });
            }
            else { Enter(RodState.Ready); ChargeNorm = 0f; Cue(RodCue.InvalidRelease); }  // rows: short / invalid marker
        }

        private void TickCasting()
        {
            if (!_flightOver && StateTime >= CastMath.FlightTime(CastDistance)) { _flightOver = true; Cue(RodCue.Landing); }
            if (_flightOver && HasResult) { EnterWaiting(); return; }                 // row: flight over + result
            if (!HasResult && StateTime >= _t.ResultTimeout) Recall(sendCancel: true); // row: no result in 3 s
            // otherwise: flight over, no result -> stay Casting, bite timer not running (row)
        }

        private void TickWaiting(float dt, in RodFrame f)
        {
            if (f.SecondaryPressed) { Recall(sendCancel: true); return; }             // row: Waiting / Secondary pressed
            if (f.PlayerToBobber - FishingConstants.CastTipReach > _rod.MaxLineOut(_t.SpoolExtra)) { Recall(sendCancel: true); return; } // row: maxLineOut (R67: measured from the rod tip, so root-to-bobber minus the fixed CastTipReach)
            BiteTimer += dt;
            while (_nibblesDone < Roll.NibbleCount && _nibblesDone < FishingConstants.MaxNibbleSlots
                   && BiteTimer >= NibbleAt(_nibblesDone))
            { Cue(RodCue.Nibble); RodEvent(RodEventType.Nibble, 0); _nibblesDone++; }  // nibbles never hook or cancel
            if (BiteTimer >= BiteWait)                                                // row: bite timer
            { Enter(RodState.Bite); Cue(RodCue.Bite); RodEvent(RodEventType.Bite, 0); }
        }

        private void TickBite(in RodFrame f)
        {
            float window = Roll.HookWindow > 0f && Roll.Pity ? Roll.HookWindow + _t.PityHookWindowBonus : Roll.HookWindow;   // R65: pity extends a real hook window; auto-hook (0) is unchanged
            if (window > 0f)
            {
                if (f.PrimaryPressed) Hook(f);                                         // row: Primary in window
                else if (StateTime >= window) EndEscaped(EscapeReason.Missed);         // row: window elapsed
            }
            else if (StateTime >= _t.HookBeat - Eps) Hook(f);                          // row: auto-hook
        }

        private void Hook(in RodFrame f)
        {
            Enter(RodState.Hooked);
            Fight = Roll.Catch.IsJunk
                ? FightSim.ForJunk(_rod, _t, Roll.FightSeed, f.TipToBobber)
                : new FightSim(_profile, _rod, _t, Roll.WeightT, Roll.FightSeed, f.TipToBobber, _castAssist, Roll.Pity);
            Cue(RodCue.HookSet);
            Add(new RodEffect { Kind = RodEffectKind.SendHooked, CastId = CastId });
            RodEvent(RodEventType.HookSet, 0);
            Add(new RodEffect { Kind = RodEffectKind.StartFight, CastId = CastId });
        }

        public void TickFight(in FightInput input, in RodFrame f)
        {
            if (f.Paused) return;
            if (State != RodState.Hooked || Fight == null) { IgnoredEventCount++; return; }
            FightPhase phase = Fight.State.Phase;
            sbyte dir = Fight.State.BurstDir;
            bool wasExhausted = Fight.Exhausted;
            FightOutcome o = Fight.Tick(input);
            if (o == FightOutcome.None)
            {
                if (Fight.IsJunk) return;
                FightPhase now = Fight.State.Phase;
                if (Fight.Exhausted && !wasExhausted) Cue(RodCue.Exhausted);
                else if (now == FightPhase.Calm && phase == FightPhase.Burst) Cue(RodCue.Calm);
                else if (now == FightPhase.Burst && (phase == FightPhase.Calm || Fight.State.BurstDir != dir))
                    Cue(Fight.State.BurstDir == 0 ? RodCue.Dive : RodCue.SideRun);  // new run or a flip
                return;
            }
            FightReport report = ReportOf(Fight);
            if (o == FightOutcome.Landed)                                              // row: Hooked / land
            {
                EnterTerminal(RodState.Landed, FightOutcome.Landed, EscapeReason.None);
                SendResult(FightOutcome.Landed, EscapeReason.None, report);
                Cue(RodCue.LineLanded);
            }
            else if (o == FightOutcome.Snapped)                                        // row: Hooked / snap
            {
                EnterTerminal(RodState.Snapped, FightOutcome.Snapped, EscapeReason.None);
                SendResult(FightOutcome.Snapped, EscapeReason.None, report);
                RodEvent(RodEventType.Snapped, Roll.SizeClass);
                Cue(RodCue.Snapped);
            }
            else EndEscaped(Fight.State.Reason);                                       // rows: pinned, slack
        }

        private void TickTerminal()
        {
            if (StateTime < FishingConstants.FeedbackBeatSeconds - Eps) return;       // 1.5 s beat first
            if (_commitReceived) { EnterIdle(); return; }                              // row: beat + commit
            if (StateTime >= _t.CommitTimeout - Eps)                                   // row: no commit in 3 s
            { Add(new RodEffect { Kind = RodEffectKind.CommitTimedOut, CastId = CastId }); EnterIdle(); }
        }

        public void OnCastResult(uint castId, bool valid, in FishingRoll roll, in FishFightProfile profile)
        {
            if (_cancelledCastId != 0 && castId == _cancelledCastId) return;           // late reply to our own cancel
            if (castId != CastId || (State != RodState.Casting && State != RodState.Waiting)) { IgnoredEventCount++; return; }
            if (!valid) { Recall(sendCancel: false); return; }                         // rows: Casting / Waiting invalid
            if (State == RodState.Waiting || HasResult) { IgnoredEventCount++; return; } // duplicate
            HasResult = true; Roll = roll; _profile = profile; BiteWait = roll.BiteWait;
            if (_flightOver) EnterWaiting();                                           // timer starts at the result
        }

        public void OnCommit(uint castId, bool accepted, FightOutcome finalOutcome, EscapeReason finalReason)
        {
            if (_cancelledCastId != 0 && castId == _cancelledCastId) { _cancelledCastId = 0; return; } // our cancel's reply
            if (castId != CastId) { IgnoredEventCount++; return; }
            switch (State)
            {
                case RodState.Landed: case RodState.Snapped: case RodState.Escaped:
                    if (_commitReceived) IgnoredEventCount++; else _commitReceived = true;
                    return;
                case RodState.Waiting: case RodState.Bite: case RodState.Hooked:     // rows: unsolicited commit
                    if (finalOutcome == FightOutcome.Cancelled)
                    {
                        EnterTerminal(RodState.Escaped, FightOutcome.Cancelled, finalReason);
                        Recalling = true;
                        Cue(RodCue.Recall);
                    }
                    else
                    {
                        EnterTerminal(RodState.Escaped, FightOutcome.Escaped,
                                      finalReason == EscapeReason.None ? EscapeReason.Invalid : finalReason);
                        RodEvent(RodEventType.Escaped, 0);
                        Cue(RodCue.Escaped);
                    }
                    _commitReceived = true;
                    return;
                default:
                    IgnoredEventCount++;
                    return;
            }
        }

        public void ForceEnd(RodForceReason reason)                                    // row: any line-out state
        {
            switch (State)
            {
                case RodState.Readying: case RodState.Ready: case RodState.Charging: EnterIdle(); return;
                case RodState.Casting: case RodState.Waiting: case RodState.Bite: Recall(sendCancel: true); return;
                case RodState.Hooked: EndEscaped(EscapeReason.Dropped); return;
                default: return;                                                       // Idle and terminal: no-op
            }
        }

        public int DrainEffects(List<RodEffect> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int n = _effects.Count;
            into.AddRange(_effects);
            _effects.Clear();
            return n;
        }

        private void EndEscaped(EscapeReason reason)
        {
            FightReport report = Fight != null ? ReportOf(Fight) : default;
            EnterTerminal(RodState.Escaped, FightOutcome.Escaped, reason);
            SendResult(FightOutcome.Escaped, reason, report);
            RodEvent(RodEventType.Escaped, 0);
            Cue(RodCue.Escaped);
        }

        private void Recall(bool sendCancel)
        {
            if (sendCancel)
            {
                Add(new RodEffect { Kind = RodEffectKind.SendCancel, CastId = CastId });
                _cancelledCastId = CastId;   // the host's CommitRpc (and any late CastResult) for it is expected, not illegal
            }
            Cue(RodCue.Recall);
            EnterIdle();
        }

        private void EnterWaiting() { Enter(RodState.Waiting); BiteTimer = 0f; _nibblesDone = 0; }
        private void EnterTerminal(RodState s, FightOutcome o, EscapeReason r)
        { Enter(s); Outcome = o; Reason = r; Recalling = false; _commitReceived = false; }
        private void EnterIdle() { Enter(RodState.Idle); ResetCast(); ChargeNorm = 0f; }
        private void Enter(RodState s) { State = s; StateTime = 0f; }

        private void ResetCast()
        {
            HasResult = false; Roll = default; BiteTimer = 0f; BiteWait = 0f; Fight = null;
            Outcome = FightOutcome.None; Reason = EscapeReason.None; Recalling = false;
            _flightOver = false; _nibblesDone = 0; _commitReceived = false;
        }

        private float NibbleAt(int k) => k == 0 ? Roll.Nibble0 : (k == 1 ? Roll.Nibble1 : Roll.Nibble2);
        private static FightReport ReportOf(FightSim s) => new FightReport
        { MaxTension = s.State.MaxTension, FightSeconds = s.State.Elapsed, NearSnapRecoveries = s.State.NearSnapRecoveries };
        // BF-1: only the Snapped cue carries the size class (B-Q13); the owner's local cue reads it (net ProcessEffects).
        private void Cue(RodCue c) =>
            Add(new RodEffect { Kind = RodEffectKind.Cue, Cue = c, CastId = CastId, SizeClass = c == RodCue.Snapped ? Roll.SizeClass : (byte)0 });
        private void RodEvent(RodEventType t, byte size) =>
            Add(new RodEffect { Kind = RodEffectKind.SendRodEvent, EventType = t, SizeClass = size, CastId = CastId });
        private void SendResult(FightOutcome o, EscapeReason r, in FightReport rep) =>
            Add(new RodEffect { Kind = RodEffectKind.SendResult, CastId = CastId, Outcome = o, Reason = r, Report = rep });
        private void Add(in RodEffect e) => _effects.Add(e);
    }
}
```
`RodFrame`, `FightReport`, `RodEffectKind` and `RodEffect` are the C 7.8 structs and enum, verbatim. Note the `Hooked` row in `Tick`: T18 calls `Tick(dt, frame)` first each frame, then 0-5 `TickFight` steps. Leash and water are frame-level rows; fight outcomes come from `TickFight`.

### Test harness (private helpers inside `RodStateMachineTests`)
- **Defaults:** `Tuning()` from `FishingTestData`, rod Handmade (`MaxLineOut` 30), profile carp = `MotionProfiles()[ProfileBulldog]`.
- **`Fr(...)`:** builds a `RodFrame`. Defaults: `SecondaryHeld = true` (the player keeps aiming), `MarkerValid = true`, distances 0, `TipToBobber = 10`.
- **`Drain(m)`:** returns the drained effects.
- **`FishRoll(biteWait 2, nibbles 0, hookWindow 0, junk false)`:** carp index, `WeightT` 0.5, `FightSeed` 12345, `SizeClass` 1.
- **Drive helpers:**
  - `ToReady`: Tick(0.1, pressed+held), then Tick(0.1) x 3.
  - `ToCharging`: from Ready, Tick(0.1, PrimaryPressed+Held).
  - `ToCasting(charge 0.5)`: from Charging, Tick(0.4, held), then Tick(0.1, released). Charge 0.5, so distance 10.25 and flight 0.573 s. `CastId` becomes 1.
  - `ToWaiting`: `OnCastResult(id, true, roll, carp)`, then Tick(0.6).
  - `ToBite`: Tick until `BiteTimer >= 2.0`.
  - `ToHooked`: Tick(0.3).
  - `ToLanded`: TipToBobber 2.05, then `TickFight(In(0, true))` x 3.
  - `ToSnapped`: `Fight.ForceState{LineLength 10, Lateral 0.7, Tension 1, FishStamina 1, Phase Burst, BurstDir 1, PhaseTimer 5, SnapTimer 0.24}`, then one reeling `TickFight`.
  - `ToEscaped`: Bite with hookWindow 0.5, then Tick(0.6).
  - Drain effects after each helper unless a test inspects them.

### Tests: C 10.2 rows (exact names)
| Test | Arrange / act | Assert |
|---|---|---|
| `RodStateMachine_Idle_SecondaryPressed_EntersReadying` | Tick(0.1, SecondaryPressed+Held) | Readying, no effects; with Held but no Pressed edge: stays Idle |
| `RodStateMachine_Readying_SecondaryHeldQuarterSecond_EntersReadyWithMarker` | press, then Tick(0.1) x 2 / x 3 | Readying at 0.2 s, `MarkerOn` false; Ready at 0.3 s, `MarkerOn` true, cue Ready |
| `RodStateMachine_Readying_SecondaryReleased_ReturnsIdle` | press, Tick(0.1, SecondaryHeld false) | Idle, no effects |
| `RodStateMachine_Ready_SecondaryReleased_ReturnsIdleMarkerOff` | `ToReady`, Tick(0.1, Held false) | Idle, `MarkerOn` false |
| `RodStateMachine_Ready_FreshPrimaryPress_EntersChargingAtZero` | `ToReady`, Tick(0.1, PrimaryPressed+Held) | Charging, `ChargeNorm == 0`, cue ChargeStart |
| `RodStateMachine_Charging_SecondaryReleased_ReturnsIdleWithoutCast` | `ToCharging`, Tick(0.3, Primary held, Secondary false) | Idle, `CastId` 0, no SendCastRequest |
| `RodStateMachine_Charging_ValidRelease_EntersCastingAndRequestsCast` | `ToCharging`, Tick(0.5, held), Tick(0.0001, Primary released) | Casting, `CastId` 1, effects contain CastRelease cue and `SendCastRequest{CastId 1, ChargeNorm ~0.5}`; `CastDistance` 10.25 (+/- 0.01) |
| `RodStateMachine_Charging_ShortRelease_ThunksAndStaysReady` | `ToCharging`, Tick(0.05, released) | Ready, cue InvalidRelease, `CastId` 0, no SendCastRequest |
| `RodStateMachine_Charging_InvalidMarkerRelease_ThunksAndStaysReady` | `ToCharging`, Tick(0.5), release with `MarkerValid` false | Ready, InvalidRelease, no request |
| `RodStateMachine_Casting_FlightEndedWithResult_EntersWaiting` | `ToCasting`, `OnCastResult` (valid) before landing: still Casting; then Tick(0.6) | Waiting, cue Landing, `BiteTimer` 0 |
| `RodStateMachine_Casting_FlightEndedWithoutResult_StaysCastingTimerStopped` | `ToCasting`, Tick(0.6), Tick(1.0) | Casting, Landing cue once, `BiteTimer` 0, `HasResult` false |
| `RodStateMachine_Casting_ResultAfterLanding_BiteTimerStartsAtResult` | `ToCasting`, Tick(0.6), Tick(1.0), `OnCastResult` (biteWait 2) | Waiting at once; Tick(1.9): Waiting; Tick(0.2): Bite (so 2.0 s after the result, not after landing) |
| `RodStateMachine_Casting_InvalidResult_RecallsToIdle` | `ToCasting`, `OnCastResult(1, false, ...)` | Idle, cue Recall, **no** SendCancel |
| `RodStateMachine_Waiting_InvalidResult_RecallsToIdle` | `ToWaiting`, `OnCastResult(1, false, ...)` | Idle, Recall, no SendCancel |
| `RodStateMachine_Casting_ResultTimeout_RecallsAndSendsCancel` | `ToCasting`, Tick(1.0) x 2, then Tick(1.0) | Casting at 2.0 s; at 3.0 s Idle with SendCancel{CastId 1} and Recall; a late `OnCastResult(1, true, ...)` and then `OnCommit(1, true, Cancelled, None)` leave `IgnoredEventCount` at 0 |
| `RodStateMachine_Waiting_SecondaryPressed_CancelsToIdle` | `ToWaiting`, Tick(0.1, SecondaryPressed), then `OnCommit(1, true, Cancelled, None)` | Idle, SendCancel + Recall; the commit reply leaves `IgnoredEventCount` at 0 and the state Idle |
| `RodStateMachine_Waiting_BeyondMaxLineOut_AutoReelsAndCancels` | `ToWaiting`; R67: PlayerToBobber `CastTipReach` + 29.99 gives Waiting (the limit is 30 m from the tip); `CastTipReach` + 30.5 | Idle, SendCancel + Recall |
| `RodStateMachine_Waiting_BiteTimerElapsed_EntersBiteAndSendsBiteEvent` | `ToWaiting`, Tick(1.9) then Tick(0.2) | Waiting, then Bite with cue Bite and `SendRodEvent{Bite}` |
| `RodStateMachine_Bite_AutoHookAfterHookBeat_EntersHookedAndStartsFight` | `ToBite`, Tick(0.2) then Tick(0.1) | Bite at 0.2; Hooked at 0.3; effects in order: HookSet cue, SendHooked{1}, SendRodEvent{HookSet}, StartFight; `Fight` non-null, `Fight.State.LineLength == 10` |
| `RodStateMachine_Bite_PrimaryInsideHookWindow_EntersHooked` | roll hookWindow 1.0; `ToBite`; Tick(0.5, PrimaryPressed) | Hooked |
| `RodStateMachine_Bite_HookWindowElapsed_EscapesMissed` | hookWindow 1.0; `ToBite`; Tick(0.6) x 2 | Escaped, `Reason` Missed; `SendResult{Escaped, Missed, Report all 0}`, `SendRodEvent{Escaped}`. R65 extra case inside this test (no new test name): same with `Roll.Pity = true` and `PityHookWindowBonus 0.3`: still Bite after 1.2 s, Missed after 1.3 s; a pity roll with hookWindow 0 auto-hooks at `HookBeat` as usual |
| `RodStateMachine_Hooked_LineAtLandDistanceWhileReeling_LandsWithFightReport` | `ToHooked` with TipToBobber 2.05; `TickFight(In(0, true))` x 3 | Landed; `SendResult{Landed, None, Report{MaxTension 0, FightSeconds 0.06 (1e-5), Recoveries 0}}`; cue LineLanded |
| `RodStateMachine_Hooked_SnapTimerReachesGrace_Snaps` | `ToHooked`, `ToSnapped` | Snapped; `SendResult{Snapped}`, then `SendRodEvent{Snapped, SizeClass 1}`; the Snapped `Cue` effect has `SizeClass` 1 (BF-1), and every other cue effect has 0 |
| `RodStateMachine_Hooked_PinnedTooLong_EscapesPinned` | `Fight.ForceState{Lateral 1, Calm, PhaseTimer 5, PinnedTimer 4.99, Stamina 1, Line 10}`, TickFight(In(0, false)) | Escaped / Pinned with SendResult |
| `RodStateMachine_Hooked_SlackTooLong_EscapesThrewHook` | ForceState{Calm, PhaseTimer 5, SlackTimer 11.99, Stamina 1, Line 10}, TickFight(none) | Escaped / ThrewHook |
| `RodStateMachine_Hooked_DisplacedBeyondLeash_EscapesDropped` | Tick(0.1, HookDisplacement 3.0), then 3.1 | Hooked; then Escaped / Dropped with SendResult |
| `RodStateMachine_Hooked_EnteredWater_EscapesDropped` | Tick(0.1, InWater) | Escaped / Dropped |
| `RodStateMachine_Waiting_ForceEndTeleport_CancelsToIdle` | `ToWaiting`, `ForceEnd(Teleport)` | Idle, SendCancel + Recall |
| `RodStateMachine_Casting_ForceEndRespawn_CancelsToIdle` | `ToCasting`, `ForceEnd(Respawn)` | Idle, SendCancel + Recall |
| `RodStateMachine_Bite_ForceEndStow_CancelsToIdle` | `ToBite`, `ForceEnd(Stow)` | Idle, SendCancel + Recall |
| `RodStateMachine_Waiting_ForceEndSwim_CancelsToIdle` | `ToWaiting`, `ForceEnd(Swim)` | Idle, SendCancel + Recall |
| `RodStateMachine_Hooked_ForceEndDisconnect_EscapesDropped` | `ToHooked`, `ForceEnd(Disconnect)` | Escaped / Dropped, `SendResult{Escaped, Dropped}`; Readying / Ready / Charging + ForceEnd give Idle with no sends |
| `RodStateMachine_Waiting_UnsolicitedCancelCommit_RecallBeatThenIdle` | `ToWaiting`, `OnCommit(1, true, Cancelled, None)`, Tick(1.4), Tick(0.2) | Escaped, `Recalling`, `Outcome` Cancelled, cue Recall, no SendResult / SendCancel; still Escaped at 1.4; Idle at 1.6 |
| `RodStateMachine_Bite_UnsolicitedCommit_BeatThenIdleNoResult` | `ToBite`, `OnCommit(1, true, Escaped, Invalid)`, Tick(1.6) | Escaped / Invalid, `SendRodEvent{Escaped}`, no SendResult; then Idle |
| `RodStateMachine_Hooked_UnsolicitedEscapeCommit_SlackBeatThenIdle` | `ToHooked`, `OnCommit(1, true, Escaped, Timeout)`; TickFight once; Tick(1.6) | Escaped / Timeout, no SendResult; TickFight ignored (`IgnoredEventCount` +1); then Idle |
| `RodStateMachine_Terminal_BeatDoneAndCommit_ReturnsIdle` | `ToLanded`, Tick(1.6), `OnCommit(1, true, Landed, None)`, Tick(0.01) | Landed after 1.6 s without a commit; Idle after the commit |
| `RodStateMachine_Terminal_CommitBeforeBeatEnds_WaitsForBeat` | `ToLanded`, `OnCommit` at once, Tick(1.0), Tick(0.6) | Landed at 1.0; Idle at 1.6 |
| `RodStateMachine_Terminal_NoCommit_ReturnsIdleAfterCommitTimeout` | `ToSnapped`, Tick(1.0) x 2, Tick(1.0) | Snapped at 2.0; Idle at 3.0 with a `CommitTimedOut` effect |

### Tests: rules (exact names) plus one extra
| Test | Assert |
|---|---|
| `RodStateMachine_IllegalEvents_IgnoredInEveryState` | `[TestCaseSource]` over all 11 states, using the drive helpers; the Idle case uses a fresh machine (no cancelled id). In each state fire that state's illegal events: in every state, `OnCastResult(CastId + 1)` and `OnCommit(CastId + 1)`. Idle, Readying, Ready and Charging also get `OnCastResult(CastId)`, `OnCommit(CastId)` and `TickFight`. Casting gets `OnCommit(CastId)` and `TickFight`. Waiting gets a duplicate valid `OnCastResult(CastId)` and `TickFight`. Bite gets a valid `OnCastResult(CastId)` and `TickFight`. Hooked gets a valid `OnCastResult(CastId)`. Landed, Snapped and Escaped get `OnCastResult(CastId)` and `TickFight`. Every state except Idle also gets `SetRod`. After each, `State` is unchanged, no effects are drained, and `IgnoredEventCount` has grown by the number of events fired. |
| `RodStateMachine_HeldPrimaryThroughCatch_DoesNotStartNewCast` | land a fish with `PrimaryHeld` true on every frame, commit, beat, so the rod is Idle; press Secondary, hold to Ready with `PrimaryHeld` true and no `PrimaryPressed`; Tick(1.0) | still Ready, no SendCastRequest, `CastId` 1 |
| `RodStateMachine_MismatchedCastId_Ignored` | Casting with `CastId` 1: `OnCastResult(2, true)`, `OnCommit(0, ...)` | `HasResult` false, Casting, `IgnoredEventCount` 2 |
| `RodStateMachine_Paused_StopsBiteTimerAndBeat` | Waiting with `BiteTimer` 0.5: Tick(5, Paused): `BiteTimer` 0.5, `StateTime` unchanged. Landed: Tick(5, Paused): still Landed. `OnCommit` while paused still sets the flag: after unpausing, Tick(1.6) gives Idle |
| `RodStateMachine_NibbleInput_NeverHooksOrCancels` | roll: biteWait 2, nibbles 2 at 0.6 and 1.2. Tick(0.1, PrimaryPressed+Held) through 1.9 s | Waiting throughout; exactly 2 Nibble cues and 2 `SendRodEvent{Nibble}`; no SendHooked / SendCancel |
| `RodStateMachine_SetRod_IgnoredOutsideIdle` | `ToReady`, `SetRod(twig)`, `ToCharging`, Tick(1.0, held), Tick(0.0001, released) | `CastDistance` 18 +/- 0.01 (Handmade), not 14; `IgnoredEventCount` 1 |
| `RodStateMachine_Assist_CapturedAtRelease` (extra) | `Assist` false at release, set true before the hook; `ToHooked`; ForceState as in `ToSnapped` with SnapTimer 0 and continuous reel | snaps on fight tick 13 (no-assist grace), not 38 |

Filter: `FishGame.Tests.EditMode.RodStateMachineTests`.

### Steps
1. Write `RodStateMachineTests.cs` (38 row tests + 6 rules + 1 extra). Red run.
2. Write the four C 7.8 types and `RodStateMachine.cs` (code above). Green run. A failing row test means the test or the helper drives the wrong path. Fix the test against the code's row comments; never change the machine's row logic without reporting.
3. Full `test-edit`, ASCII scan, then commit with explicit paths: `git commit -m "m2(rod): RodStateMachine (every spec 9.2 row), effects queue, FightReport" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`.

### Acceptance (reviewer, Opus high)
- Every C 10.2 row maps to the commented code path, and the 44 + 1 tests pass.
- Edge triggers hold (no cast from a held Primary).
- Pause gates only time.
- Invalid results never refund via `SendCancel`.
- Unsolicited commits never send `SendResult`.
- Effect order is as listed in decision 9.

---

## Task T9 - Host ledger, validator, creel

- **Read section 0 of this file first** (constraints, commands, index map, shared test data).
- **Goal:** the host's pure ledger (C 7.9): bait stock, `ActiveCast` per client, validation (spec 9.9), timeouts, orphans, clean-claim bounds (R64), creel rules and the slot-keyed creel store.
- **Owned files (C 12):** `Core/Fishing/{CastPhase,ActiveCast,CastRequestData,HostCastView,CastVerdict,CommitDecision,PlayerLoadout,CreelSnapshot,CreelItem,CreelRules,CreelLedger,CatchValidator,RollOverrides,FishingHostLedger}.cs`. Tests: `FishingHostLedgerTests.cs`, `CreelRulesTests.cs`.
- **Shared edits:** none. **Depends on:** T5, T6, T8 (`FightReport`). **Implementer:** Sonnet. **Reviewer:** Opus high (acceptance criterion 7).
- **Authority comment** (required at the top of `FishingHostLedger.cs` and `CatchValidator.cs`): "Host only. Owns bait stock, rolls, ActiveCast and outcomes (spec 9.9 N6); the owner reports outcomes, the host validates and never replays the fight."

### Behaviour (signatures verbatim from C 7.9)
- **`RollOverrides.None`** = `new RollOverrides { ForcedItemIndex = -1 }`. A default struct would force species 0, so never use `default`. Overrides map 1:1 onto `RollRequest` fields.
- **Keys (C M2-D20):**
  - Per slot (`EnsureSlot` once; later calls are no-ops): `RodIndex = defaultRodIndex`, `BaitIndex = defaultBaitIndex`, and a stock per bait (`FishingConstants.DebugBaitStock` = 20; unlimited baits are not counted).
  - Per clientId: `Slot`, `LastAcceptedCastId` (starts 0), an optional `ActiveCast`, and the last 16 resolutions (`castId -> (Outcome, Reason)`) for orphans.
  - `Tick` iterates clients in ascending clientId order, so output order is deterministic.
- **Bad-luck nudge, ledger side (R65 / M2-D23; `Pity` is host-decided, deterministic, no RNG):**
  - Per slot (same lifetime as bait stock; survives rejoin): `FailStreak` (int, starts 0).
  - At accept, after `PlaceholderRoller.Roll`: `roll.Pity = Tuning.PityAfterFailures > 0 && FailStreak >= Tuning.PityAfterFailures`. The roller's draw order is untouched; the verdict's `Roll.Pity` and `ActiveCast.Roll.Pity` carry it. `FishingHost` (T17) announces a pity accept.
  - Counting, when a FIRST resolution is stored (never for orphans): `Landed` -> `FailStreak = 0`. `Snapped`, or `Escaped` with any reason (Missed, Pinned, ThrewHook, Dropped, Timeout, Invalid) -> `FailStreak++`, unless the resolved cast was a Pity cast, in which case `FailStreak = 0` whatever the outcome. `Cancelled` (owner cancel, stuck-Waiting refund, recall) neither counts nor resets: a cancelled Pity cast leaves `FailStreak` as it was, so the next accepted cast is Pity again. `Disconnect` clears the cast without a resolution: no change.
  - Landed-floor validation and everything else is unchanged by Pity.
- **`EnsureSlot` / `GetLoadout`:** `BaitCount` = the stock of the selected bait, or `UnlimitedBaitCount` (0xFFFF) when unlimited.
- **`SelectBait` / `SelectRod`:** false if the index is out of range or that client has an `ActiveCast`. Otherwise set it on the slot and return true.
- **`TryComputeLanding(slot, req, out x, out z)`:** false for an unknown slot or a charge that is NaN or outside [0, 1]. Otherwise `distance = CastMath.Distance(charge, CastMinDist, Rods[slot rod].CastMaxDist)` and `CastMath.Landing(req.OriginX, req.OriginZ, req.YawDeg, distance, ...)`. R67: `distance` is from the rod tip and the shared `CastMath.Landing` adds `FishingConstants.CastTipReach` inside, so owner and host stay bit-identical.
- **`CatchValidator.ValidateCast`:** order as in C 7.9.
  - `!view.CastingAllowed` gives `NotAllowedNow`.
  - `hasActive` gives `ActiveCast`.
  - `req.CastId <= lastAcceptedCastId` gives `StaleCastId`.
  - NaN or out-of-range charge gives `BadCharge`.
  - Horizontal distance from (OriginX, OriginZ) to (PlayerX, PlayerZ) `> HostOriginSlack` (3.0) gives `OriginTooFar`.
  - `!baitAvailable` gives `NoBait`.
  - Then `CastMath.ProbeReason(view.Probe, minLandDepth)`.
- **`RequestCast`:**
  - An unknown slot (never ensured) is rejected `UnknownPlayer`.
  - `baitAvailable` = `req.BaitIndex < Baits.Count && (unlimited || stock > 0)`.
  - On reject: verdict `Valid = false`, `Reason` set, nothing else changes.
  - On accept:
    1. Stock-- (not for unlimited).
    2. `HostCastCounter++`.
    3. `rng = Rng.Stream(RunSeed, FishingConstants.CatchStreamId, HostCastCounter)`.
    4. `RollRequest{BaitIndex = req.BaitIndex, LoadedCount = 1, Depth = view.Probe.Depth, Luck = 0, overrides}`.
    5. `PlaceholderRoller.Roll`.
    6. Landing via `TryComputeLanding`.
    7. `ActiveCast{CastId, Roll, Waiting, TAccepted = Now, TFlight = CastMath.FlightTime(distance), Assist, RodIndex = slot rod, BaitIndex, LandingX/Z}`.
    8. `LastAccepted = req.CastId`.
- **`Cancel`:**
  - Matching cast in Waiting: refund if `CancelRefundsBait` (not unlimited), resolve `Cancelled/None`, `Accepted = true`, `Refunded = true` (when refunded), `Announce` None.
  - Matching cast in Hooked: resolve `Escaped/Dropped`, no refund, `Announce = AnnounceFor(Escaped, Dropped, true)` = Lost. This is a decision; the owner never sends Cancel while Hooked.
  - No match: orphan (below).
- **`Hooked`:**
  - No match: orphan, return true.
  - Already Hooked: return false, no change (duplicate).
  - `CatchValidator.HookTooEarly(Now, a, HookTolerance)`: resolve `Escaped/Invalid`, `Accepted = true` (a first resolution), no refund, Announce None, return true.
  - Otherwise: `Phase = Hooked`, `THooked = Now`, `LineLength0 = HostLineLength0(hostDistanceToLanding, LandDistance)`, return false.
- **`CommitDecision.Accepted` (ruling E-Q6):** `false` only for an orphan repeat (a message for a cleared cast, below). Every FIRST resolution has `Accepted = true`, including timeouts, early-hook Invalid, validation Invalid, cancels and creel-full releases. `Accepted` does not say whether a fish was kept. Consumers (T17) act on the other fields: announce iff `Announce != None`; creel add iff `AddToCreel`; `FishCaught` iff `Outcome == Landed && AddToCreel`. Invalid and cancel resolutions have `Announce == None` by design.
- **`CommitDecision.Roll` (BF-4):** every non-orphan decision carries `Roll = ActiveCast.Roll`, including the Cancel-in-Waiting and Cancel-in-Hooked decisions and the `Tick` timeouts (stuck-Waiting refund, `MaxFightTime`). T17's Lost announcement sends `CatchResultNet.From(d.Roll.Catch)` and so needs it. The Cancel and Tick paths must build the decision from the stored `ActiveCast` before clearing it. Orphan decisions carry `default` Roll.
- **Orphan:** `Accepted = false`, `Outcome/Reason` = the stored resolution for that castId if any, else `Cancelled/None`. No refund, no announce, no creel (`Announce` None, `AddToCreel` false).
- **`Tick(dt, paused, unsolicited)`:**
  - `Now += dt` unless paused.
  - Then for each active cast:
    - Waiting with `Now - TAccepted > TFlight + Roll.BiteWait + HostWaitSlackSeconds`: refunded `Cancelled` (refund per `CancelRefundsBait`), `Accepted = true`.
    - Hooked with `Now - THooked > MaxFightTime`: `Escaped/Timeout`, `Announce` Lost.
  - Each is resolved, stored and appended to `unsolicited`.
- **`Disconnect(clientId)`:** removes the client entry: ActiveCast, LastAccepted and stored resolutions. Returns true if it held an active cast. No commit and **no refund** (plain C 7.9 reading; see Contract questions). Slot data (stock, rod, bait) is untouched.
- **`CatchLog`:** every Landed decision appends `Roll.Catch` (after any clean bonus), including creel-full releases (spec 9.10: "announced, journaled, and released").
- **`CreelRules`:** per C 7.9. `IsWarn` uses `FishingConstants.CreelWarnFraction`.
- **`CreelLedger`:** slots `0..slotCount-1`; other slots throw `ArgumentOutOfRangeException`.
  - `Add` assigns `EntryId` = the per-slot counter, starting at 1. The counter is never reset, even by `SellAll`.
  - `SellAll` returns the sum of `ValueCoins`, clears the items, and adds the sum to that slot's debug coins.
  - `Snapshot` counts entries and sums `WeightG`.

### Result and clean-claim code (write exactly; the rest of each class follows the behaviour above)
```csharp
// In CatchValidator (host only; pure):
public static bool CleanClaimAccepted(in FightReport report, double now, in ActiveCast cast, float landDistance,
                                      float reelSpeed, float cleanMaxTension)
{
    if (cast.Roll.Catch.IsJunk) return false;                                          // (1)
    float mt = report.MaxTension;
    if (float.IsNaN(mt) || float.IsInfinity(mt) || mt < 0f || mt >= cleanMaxTension) return false;   // (2)
    if (report.NearSnapRecoveries != 0) return false;                                  // (3) a recovery needs tension > 0.8
    float fs = report.FightSeconds;
    if (float.IsNaN(fs) || float.IsInfinity(fs)) return false;
    if (fs < LandedFloorSeconds(cast.LineLength0, landDistance, reelSpeed)) return false;           // (4)
    if (fs > (now - cast.THooked) + FishingConstants.CleanClaimClockSlackSeconds) return false;      // (5) never ahead of host
    return true;
}

// In FishingHostLedger:
public CommitDecision Result(ulong clientId, uint castId, FightOutcome outcome, EscapeReason reason,
                             in FightReport report, in CreelSnapshot creel)
{
    if (!_clients.TryGetValue(clientId, out ClientState c) || !c.HasActive || c.Active.CastId != castId)
        return Orphan(clientId, c, castId);
    ActiveCast a = c.Active;
    bool wasHooked = a.Phase == CastPhase.Hooked;
    bool junk = a.Roll.Catch.IsJunk;
    ref readonly FishingTuning t = ref _db.Tuning;
    float speed = junk ? t.JunkDragSpeed : _db.Rods[a.RodIndex].ReelSpeed;   // host rod, never an owner value

    bool valid;
    switch (outcome)
    {
        case FightOutcome.Landed:
            valid = wasHooked && !CatchValidator.LandedTooFast(Now, a, t.LandDistance, speed);
            reason = EscapeReason.None; break;
        case FightOutcome.Snapped:
            valid = wasHooked; reason = EscapeReason.None; break;
        case FightOutcome.Escaped:
            valid = reason == EscapeReason.Missed
                ? !wasHooked
                : wasHooked && (reason == EscapeReason.Pinned || reason == EscapeReason.ThrewHook || reason == EscapeReason.Dropped);
            break;
        default: valid = false; break;                                            // None, Cancelled
    }
    if (!valid) { outcome = FightOutcome.Escaped; reason = EscapeReason.Invalid; }

    var d = new CommitDecision { ClientId = clientId, Slot = c.Slot, CastId = castId, Accepted = true,
                                 Outcome = outcome, Reason = reason, Roll = a.Roll };
    if (outcome == FightOutcome.Landed)
    {
        if (CatchValidator.CleanClaimAccepted(report, Now, a, t.LandDistance, speed, t.CleanCatchMaxTension))
        {
            d.Roll.Catch = CatchMath.WithCleanBonus(a.Roll.Catch, _db.Species[a.Roll.Catch.ItemIndex], a.Roll.WeightT);
            d.CleanBonus = true;
        }
        d.AddToCreel = CreelRules.CanAccept(creel);
        d.Announce = d.AddToCreel ? AnnounceKind.Landed : AnnounceKind.ReleasedCreelFull;
        _catchLog.Add(d.Roll.Catch);
    }
    else d.Announce = CatchValidator.AnnounceFor(outcome, reason, wasHooked);
    Resolve(c, castId, outcome, reason);    // clears HasActive; stores (castId -> outcome, reason), keeps the last 16
    return d;
}
```
Every other `CatchValidator` member follows its C 7.9 comment.
- `LandedTooFast` = `now - cast.THooked < LandedFloorSeconds(cast.LineLength0, landDistance, speed)`.
- `AnnounceFor`:
  - Landed gives Landed; Snapped gives Lost.
  - Escaped gives Lost only when `wasHooked` and the reason is Pinned, ThrewHook, Dropped or Timeout.
  - Everything else gives None.

### Shared test setup (private helpers in `FishingHostLedgerTests`)
- **Ledger:** `db = FishingTestData.Database()`; `ledger = new FishingHostLedger(db, 42UL, RodHandmade, BaitWorm)`; `EnsureSlot(0)`; client 100, slot 0.
- **Request:** `Req(id, charge 0.5)` = origin (0, 0, 0), yaw 0, bait worm, assist false.
- **View:** `View()` = player (0, 0, 0), probe {water first, WaterY 0, bed, depth 3}, casting allowed.
- **Overrides:** `Ov` = `{ForcedBiteWait 2, ForcedItemIndex SpeciesCarp}`.
- **Timing:**
  - `tf = CastMath.FlightTime(CastMath.Distance(0.5f, 2.5f, 18f))` (0.573).
  - `HookAt`: `Tick(tf + 2.0)` then `Hooked(100, id, 10f, out _)`. Host distance 10 gives `LineLength0 = 8.5`, so the Handmade floor is `0.8 x 6.5 / 1.0 - 0.5 = 4.7 s`. The junk floor is `0.8 x 6.5 / 3 - 0.5 = 1.233 s`.
- **Reports:** `Clean(fs)` = `FightReport{MaxTension 0.5, FightSeconds fs, Recoveries 0}`.
- **Creel:** `Creel()` = `new CreelSnapshot(0, 0, 12, 15000)`.

### Tests (exact names from C 10.1)
| Test | Act | Assert |
|---|---|---|
| `FishingHostLedger_CastWhileActive_Rejected` | accept 1, request 2 | `ActiveCast`; stock 19 |
| `FishingHostLedger_StaleOrDuplicateCastId_Rejected` | accept 1, Cancel(1); request 1, then 0 | both `StaleCastId`; request 2 accepted |
| `FishingHostLedger_ChargeOutsideUnitRange_Rejected` | charge -0.01, 1.01, NaN | `BadCharge`; 0 and 1 accepted (cancel between) |
| `FishingHostLedger_OriginBeyondThreeMeters_Rejected` | origin (3.01, 0, 0) | `OriginTooFar`; origin (3.0, 5, 0) accepted (vertical ignored) |
| `FishingHostLedger_NoBait_Rejected` | 20 casts, each resolved `Escaped/Missed` (no refund); 21st | `NoBait`; stock 0; `GetLoadout.BaitCount` 0 |
| `FishingHostLedger_InvalidWater_Rejected` | probe not water first; depth 0.5 | `NotWater`; `TooShallow`; stock unchanged |
| `FishingHostLedger_Accept_ConsumesBaitAndRolls` | accept 1 (overrides None) | stock 19; `HostCastCounter` 1; roll field-equal to `PlaceholderRoller.Roll(db, worm/depth 3/no overrides, ref rng)` with `var rng = Rng.Stream(42, "catch", 1);`; `TryGetActive` gives `TAccepted` 0, `TFlight` tf, landing (0, 10.25) |
| `FishingHostLedger_BareHook_NeverRunsOut` | `SelectBait(bare)`; 25 casts resolved Missed | all accepted; `BaitCount` 0xFFFF |
| `FishingHostLedger_CancelInWaiting_RefundsBait` | accept, Cancel | `Accepted`, Cancelled, `Refunded`, stock back to 20, Announce None; `d.Roll` equals the accepted roll (BF-4) |
| `FishingHostLedger_SnapEscapeMiss_DoNotRefund` | three casts resolved Snapped (hooked), Escaped/Pinned (hooked), Escaped/Missed (waiting) | stock 17, none `Refunded` |
| `FishingHostLedger_EarlyHook_ResolvesEscapedInvalid` | Tick(tf + 2 - 0.75 - 0.05), Hooked | true; Escaped/Invalid, not refunded, Announce None. Second cast at Tick(tf + 2 - 0.75 + 0.01): false (accepted) |
| `FishingHostLedger_LandedBelowDurationFloor_ResolvesEscapedInvalid` | `HookAt`, Tick(4.6), Result Landed | Escaped/Invalid, `AddToCreel` false, Announce None. Next cast: Tick(4.8), then Landed accepted |
| `FishingHostLedger_JunkLanded_UsesJunkDragSpeedFloor` | `ForceJunk`, `HookAt`, Tick(1.3), Landed | Landed, `AddToCreel`, `CleanBonus` false. Fish cast with Tick(1.3): Invalid |
| `FishingHostLedger_StuckWaiting_TimesOutAsRefundedCancel` | Tick(tf + 2 + 29.9), then Tick(0.2) | first: no unsolicited; second: one decision `Cancelled`, `Refunded`, `Accepted`; stock 20; `TryGetActive` false |
| `FishingHostLedger_HookedPastMaxFightTime_TimesOutEscapedTimeout` | `HookAt`, Tick(239.9), Tick(0.2) | none, then one decision Escaped/Timeout, Announce Lost; `d.Roll` equals the cast's accepted `Roll` (field-equal; `Roll.Catch` is the announced catch, BF-4) |
| `FishingHostLedger_PausedClock_DoesNotTimeOut` | accept, Tick(1000, paused: true) | `Now` unchanged, no unsolicited, still active; a later `HookAt` timing is unaffected |
| `FishingHostLedger_Disconnect_ClearsWithoutCommit` | accept, Disconnect(100) | true; `TryGetActive` false; Tick(100) gives no unsolicited; Disconnect again gives false; stock 19 |
| `FishingHostLedger_Rejoin_KeepsSlotBaitAndRestartsCastIds` | client 100 slot 1 casts ids 1-3 (Missed), Disconnect; client 200 slot 1, `EnsureSlot(1)` again, request id 1 | accepted; slot 1 stock 16 after it; `HostCastCounter` 4 |
| `FishingHostLedger_FullCreel_ReleasesFishAndAnnounces` | `HookAt`, Tick(5), Landed with creel (12, 5000, 12, 15000) | Landed, `AddToCreel` false, Announce `ReleasedCreelFull`; `CatchLog` count 1 |
| `FishingHostLedger_OrphanMessage_RepeatsStoredResolution` | cast 1 resolved Snapped; then Result(1, Landed), Hooked(1) and Cancel(1); then Cancel(99) | the three: `Accepted` false, Outcome Snapped; id 99: `Accepted` false, Cancelled; stock unchanged by the orphans; every orphan decision has a default `Roll` (BF-4) |
| `FishingHostLedger_SnapAfterHook_AnnouncedLost` | `HookAt`, Tick(3), Result Snapped | Snapped, Announce Lost, `AddToCreel` false |
| `FishingHostLedger_Missed_NotAnnounced` | Tick(tf + 2.5), Result Escaped/Missed (Waiting) | Escaped/Missed, Announce None. `Escaped/Pinned` without a hook gives Invalid |
| `FishingHostLedger_SelectWhileActive_Refused` | active: `SelectBait(grub)`, `SelectRod(twig)`; then index 6 and rod 4 after resolution | false, false, then false (bad index) and valid selections true |
| `FishingHostLedger_RollSeed_DerivesFromRunSeedAndCounter` | two accepted casts on ledger seed 42; a second ledger seed 43 | rolls equal the roller with `Rng.Stream(42, "catch", 1)` and `(..., 2)`; seed 43's first roll differs |
| `FishingHostLedger_CleanCatch_UpgradesQualityOneTier` | `HookAt`, Tick(10), Landed with `Clean(9.5)` | `CleanBonus`; quality = `UpgradeOneTier(rolled)`; value = `Value(12, WeightT, upgraded)`; weight unchanged; `AddToCreel` |
| `FishingHostLedger_CleanCatchAtTrophy_StaysTrophy` | search runSeed 1..5000 for the first ledger whose first carp roll is Trophy (Assert.Fail if none); land clean | `CleanBonus` true, quality Trophy, value unchanged from the rolled value |
| `FishingHostLedger_ImplausibleCleanClaim_KeepsRolledQuality` | `HookAt`, Tick(10). Cases: `Clean(12.5)` (ahead of host 10 + 2); `Clean(4.0)` (below floor 4.7); control `Clean(11.9)` | first two: Landed, `AddToCreel`, `CleanBonus` false, quality and value = rolled; control: `CleanBonus` true |
| `FishingHostLedger_SelfContradictoryCleanClaim_KeepsRolledQuality` | `Clean(9.5)` with Recoveries 1 | no bonus, still Landed |
| `FishingHostLedger_NonCleanCatch_KeepsRolledQuality` | MaxTension 0.8 (equals threshold), 0.85, NaN | no bonus, Landed each time |
| `FishingHostLedger_ThreeFailures_NextCastPity` (R65) | tuning `PityAfterFailures 3`; three casts resolved Snapped, Escaped/Missed, Escaped/Pinned (mix hooked and waiting as needed); then the fourth cast accepted; then a fifth cast after the Pity cast resolved Snapped | casts 1-3 `Roll.Pity` false; cast 4 `Roll.Pity` true in the verdict and in `TryGetActive`, and all other roll fields equal the roller's output for `Rng.Stream(seed, "catch", 4)`; cast 5 `Pity` false (count reset by the Pity cast whatever its outcome); a `PityAfterFailures 0` ledger never gives Pity |
| `FishingHostLedger_Landed_ResetsPityCount` (R65) | two failures, then a Landed (valid, after `HookAt`), then two more failures, then a cast | no Pity on that cast (streak 2 after the reset); a third failure makes the next cast Pity |
| `FishingHostLedger_CancelDoesNotCountTowardPity` (R65) | two failures; then three Waiting cancels (refunded); then a cast; also a stuck-Waiting timeout (refunded Cancelled) | the cast after the cancels is not Pity (streak still 2); a third real failure then makes the next cast Pity; a cancelled Pity cast leaves the following cast Pity; `Disconnect` and rejoin keep the slot's streak |
| `FishingHostLedger_JunkCleanClaim_NoBonus` | `ForceJunk`, `HookAt`, Tick(1.5), Landed with `Clean(1.5)` | Landed, `CleanBonus` false, junk value 2 |
| `CreelRules_OverweightRule_AcceptsUntilAlreadyFull` | (11, 14999, 12, 15000) true; (11, 15000) false; (12, 0) false; (11, 16000) false | as listed |
| `CreelRules_Warn_AtEightyPercent` | entries 9 / 10 of 12 (weight 0): false / true; weight 11999 / 12000 of 15000: false / true | as listed |
| `CreelLedger_SellAll_ClearsSlotAndAddsCoins` | add values 5 and 12 to slot 2 | `SellAll(2) == 17`, items empty, `DebugCoins(2) == 17`, slot 0 untouched; slot 4 of 4 throws |
| `CreelLedger_EntryIds_MonotonicPerSlot` | slot 0 adds 3, `SellAll`, adds 1; slot 1 adds 1 | ids 1, 2, 3, then 4; slot 1 id 1 |

Filter: `FishGame.Tests.EditMode.(FishingHostLedgerTests|CreelRulesTests)`.

### Steps
1. Write both test files. Red run.
2. Implement the 14 files (`Result` and `CleanClaimAccepted` per the code above). Green run.
3. Full `test-edit`, ASCII scan, then commit with explicit paths: `git commit -m "m2(ledger): FishingHostLedger, CatchValidator (clean-claim bounds), CreelRules, CreelLedger" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`.

### Acceptance (reviewer, Opus high)
- Every clean-claim rejection keeps the rolled quality and still lands the fish (skeleton section 7).
- The host's rod and junk speed are the only speed sources.
- Every path clears the ActiveCast and stores a resolution.
- Orphans never refund or announce.
- The ledger clock stops while paused.
- 37 tests (34 + the three R65 pity tests), all green. Where an existing test compares a ledger roll with `PlaceholderRoller.Roll` output after three failures, it clears `Pity` before comparing.

---

## Contract questions (all closed by controller rulings of 2026-10-11; C 15)

1. **Test helper file (closed, B-Q1).** `Tests/EditMode/FishingTestData.cs` is in the C 12 T5 manifest; read-only for T6-T9.
2. **Internal test hooks (closed, B-Q2).** `FightSim.DrawCount`, `FightSim.BurstLateralMult`, `FightSim.TimeEps` (internal) are named in C 7.7.
3. **`FightInput` type (closed, B-Q3).** The M1 `Core/Input/FightInput.cs` is reused unchanged.
4. **Side-run choice (closed, B-Q4).** The literal spec 9.5 reading: 75% away, else a random side, so 87.5% away / 12.5% toward overall, with one draw (T7 decision 3 and the code block).
5. **Hand-check test data (closed, B-Q5).** The hand-check tests set `FlipRate = 0` on the Runner, Bulldog and Thrasher copies and use `weightT = 0.375` (test data only; C 7.7 test-data note amended).
6. **Same-tick endings (closed, B-Q6).** Landed > Snapped > Pinned > ThrewHook.
7. **Flip gap at burst start (closed, B-Q7).** A flip is allowed on the first burst tick; the minimum gap applies only between flips.
8. **Apex floor (closed, B-Q8).** Spec-literal: the 0.6 floor applies before the pitch multiplier; the test fixes the multiplier at 1.
9. **Cancel while Hooked (closed, B-Q9).** Host Cancel in Hooked resolves Escaped(Dropped), announced Lost, no refund.
10. **`RequestCast` for a slot never ensured (closed, B-Q10).** Rejected `UnknownPlayer`; T17 calls `EnsureSlot` before any `RequestCast`.
11. **Disconnect refund (closed, B-Q11).** Disconnect does not refund a Waiting cast's bait.
12. **`IgnoredEventCount` (closed, E-Q5 supersedes B-Q12).** Public getter (`public int IgnoredEventCount { get; private set; }`); T18 logs ignored events in debug builds and still must not warn on the reply to its own cancel (the machine consumes that silently, T8 decision 10).
13. **`RodEventRpc` size class (closed, B-Q13, BF-1).** Only Snapped carries the size class; other events send 0. The local `Cue(Snapped)` effect carries it too (the owner's snap cue reads it).
14. **`RollOverrides.None` (closed, B-Q14).** `ForcedItemIndex = -1`; T17's override builder starts from `RollOverrides.None`.
15. **Calm drift and exhaustion (closed, B-Q15).** An exhausted fish stays Calm: no drift, no draws.
16. **`CommitDecision.Accepted` (closed, E-Q6).** False only for orphan repeats; consumers act on `Announce`, `AddToCreel` and `Outcome` (T9 behaviour bullet).
17. **Bad-luck nudge (R65 / M2-D23, added 2026-10-11).** Pity is a roll flag set by the ledger; the multipliers live in `FightSim` (pity ctor argument) and the hook-window bonus in `RodStateMachine`. Interpretation to confirm: a cancelled Pity cast does not consume the pity (the ruling says both "resets when a Pity cast resolves, whatever its outcome" and "cancels neither count nor reset"; this plan lets the cancel win, so the next accepted cast is Pity again).
