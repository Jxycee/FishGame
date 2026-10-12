# FishGame M2 - Plan Section D: Data and tuning (T13, T14, T15)

- **Date:** 2026-10-11. Branch `m2-fishing-core`.
- **Contract (binding):** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` ("C" below). Sections used: 1, 2, 3, 5, 6, 7.7, 7.13-7.15, 10.3, 12, 13, 14.
- **Skeleton:** `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md` (rules in its section 1, rows T13-T15, section 6 row D).
- **Spec (behaviour source):** `docs/superpowers/specs/2026-10-09-fishgame-design.md` 9.5 (fight), 9.6 (profiles, species), 9.7 (rods, bait), 9.10 (creel cap), 9.12 (tuning table, bot targets), 10.2-10.4 (palette, phases, water), 11.7 (cue rows), 17.2 (tuning bots, acceptance 8, 9, 11, fun gate), 3.2 R62-R64.
- **Tasks:** T13 data defs and registry, T14 seeds + content builder + fishing validators, T15 tuning bots + retune.

Every brief below carries C 2.8 (global constraints) by reference. The ones that bite in this section are repeated in each task header.

---

## 0. Shared notes for T13-T15

### 0.1 Commands (all three tasks)

```powershell
$env:FG_OUT = "C:\Users\jayce\AppData\Local\Temp\claude\C--Dev-FishGame\<session>\scratchpad\<task>"   # absolute, outside the repo (C 2.8 #21)
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -TimeoutMinutes 15
```
Expected last lines (M1 contract 11): `FG RESULT compile PASS log=...`, `FG RESULT build PASS ...` (log contains `FG BUILDALL PASS`), `FG RESULT test-edit PASS total=N passed=N failed=0 skipped=K log=...`. `LOCKED` means the user has the editor open: ask them to close it, never kill Unity (C 2.8 #11).

### 0.2 Ordinal index spaces (C 7.5 `FishingDatabase`, C 5.3 category names)

Indices follow `StringComparer.Ordinal` over ids. Tests in this section may hard-code these:

| Category | Index 0..n-1 |
|---|---|
| `rods` | `rod_carbon`, `rod_deepsea`, `rod_handmade`, `rod_twig` |
| `baits` | `bait_bare_hook`, `bait_grub`, `bait_minnow`, `bait_raw_meat`, `bait_shrimp`, `bait_worm` |
| `fight_profiles` | `bulldog`, `darter`, `runner`, `steady`, `thrasher` |
| `species` | `bluegill`, `carp`, `catfish`, `perch`, `trout` |
| `junk` | `old_boot`, `tin_can` |

### 0.3 Global constraints that bite here (C 2.8)

- #2 ASCII: docs and code use ASCII punctuation; after any Write whose text contains `\u`, check no byte > 0x7F.
- #6 Scene churn: after `build`, `git restore Assets/_Project/Scenes/*.unity` (no task here changes a scene).
- #7 `core.autocrlf` true: committed blobs are LF; `.asset` YAML edits keep LF.
- #8 EditMode reach: tests here touch Core, Data, Runtime, Editor only.
- #11 Editor lock. #12 no `rm -rf` on project paths. #20 explicit `git add` paths, trailer per C 2.1. #21 logs in `$env:FG_OUT`.
- C 5.1: tuning values live in assets; seeds live only in `FishingSeedData`; Def and Core types carry no tuning field initialisers.

---

## 1. Seed and range tables (single source for T14 seeds, T14 validators and T14/T15 tests)

Seed values are the spec 9.6/9.7/9.12 tables, the C 7.7 motion seeds, creel caps 12 entries / 15 kg (9.10) and clean threshold 0.8 (C M2-D16). **Seeds must equal these tables exactly.** Ranges are the validator ranges (`FishingValidators.FishingContent`, test `FishingData_Ranges_Valid`). Where 9.12 gives a range, it is used verbatim; other ranges are this plan's (marked "plan").

### 1.1 `FishingTuning` (field order = C 7.5 struct order = `FishingTuningDef` field order)

| Member (`FishingTuningDef` field camelCase) | Seed | Range |
|---|---|---|
| CastChargeTime | 1.0 | 0.6-1.5 |
| ChargePingPong | true | - |
| CastMinDist | 2.5 | 2-4 |
| MinLandDepth | 0.8 | 0.5-1.5 |
| BiteWaitMin / BiteWaitMax | 4 / 18 | 2-8 / 10-30, Min < Max |
| MinBiteWait | 1.0 | 0.5-2, <= BiteWaitMin (plan) |
| HookBeat | 0.25 | 0.15-0.4 |
| LandDistance | 2.0 | 1.5-3 |
| ReelStrainBurst | 0.30 | 0.2-0.45 |
| CounterRelief | 0.5 | 0.3-0.7 |
| SteerStrain | 0.03 | 0-0.08 |
| OutOfWindowStrain | 0.20 | 0.1-0.4 |
| SpoolStrain | 0.50 | 0.3-0.8 |
| SpoolExtra | 12 | 6-20 |
| RelaxRate | 0.45 | 0.3-0.7 |
| RelaxWhileReeling | 0.10 | 0.05-0.2 |
| ReelBurstPenalty | 1.0 | 0.6-1.2 |
| DiveRunMult | 1.5 | 1.2-2 |
| SnapGrace / SnapGraceAssist | 0.25 / 0.75 | 0.2-0.75 each; Assist >= SnapGrace (plan) |
| StrainScale / StrainScaleAssist | 1.0 / 0.6 | 0.5-1.5 / 0.3-1.0; Assist <= StrainScale (plan) |
| PinEscapeTime | 5 | 4-10 |
| SlackEscapeTime | 12 | 8-20 |
| FishStaminaRegen | 0.04 | 0.02-0.08 |
| CalmCenterPull | 0.15 | 0.05-0.3 |
| LateralMaxDeg | 75 | 60-85 |
| HookedLeash | 3 | 2-5 |
| JunkDragSpeed | 3 | 2-5 |
| ResultTimeout / CommitTimeout | 3 / 3 | 1-10 each (plan) |
| HookTolerance | 0.75 | 0.5-1.5 |
| MaxFightTime | 240 | 180-400 |
| RodStateHz | 15 | 10-20 |
| CancelRefundsBait | true | - |
| CreelMaxEntries | 12 | 1-99 (plan) |
| CreelMaxWeightG (`creelMaxKg` in the Def) | 15000 g (15 kg) | 1-100 kg (plan) |
| CleanCatchMaxTension | 0.8 | 0.5-0.95 (C 7.14) |

### 1.2 Rods (spec 9.7). Ranges (plan): tier 0-3; castMaxDist 8-40 and > CastMinDist; reelSpeed 0.5-2; lineStrength 0.5-2.5; angleWindow 0.3-0.9 (must stay below `FishingConstants.PinnedLateral` 0.95); steerRate 0.3-1.2; relaxMult 0.5-1.5; shopCost >= 0; firstCamp 0-6.

| id | displayName | tier | castMaxDist | reelSpeed | lineStrength | angleWindow | steerRate | relaxMult | shopCost | firstCamp |
|---|---|---|---|---|---|---|---|---|---|---|
| `rod_twig` | Twig Rod | 0 | 14 | 0.9 | 0.8 | 0.50 | 0.50 | 0.9 | 0 | 0 |
| `rod_handmade` | Handmade Rod | 1 | 18 | 1.0 | 1.0 | 0.60 | 0.60 | 1.0 | 120 | 1 |
| `rod_carbon` | Carbon Rod | 2 | 24 | 1.2 | 1.3 | 0.70 | 0.70 | 1.1 | 300 | 2 |
| `rod_deepsea` | Deep-Sea Rod | 3 | 30 | 1.3 | 1.6 | 0.75 | 0.80 | 1.2 | 600 | 4 |

### 1.3 Baits (spec 9.7). Ranges (plan): rating 1-10; stackMax 1-5; maxRating rating..20; biteSpeedMod 0.5-1.5; junkMult 0-2; maxNibbles 0-3 (`FishingConstants.MaxNibbleSlots`); exactly one `unlimited` bait.

| id | displayName | rating | stackMax | maxRating | biteSpeedMod | junkMult | maxNibbles | unlimited |
|---|---|---|---|---|---|---|---|---|
| `bait_bare_hook` | Bare Hook | 1 | 1 | 1 | 1.30 | 1.5 | 1 | true |
| `bait_worm` | Worm | 3 | 3 | 9 | 0.90 | 1.0 | 2 | false |
| `bait_grub` | Grub | 4 | 3 | 12 | 0.85 | 0.9 | 2 | false |
| `bait_minnow` | Minnow | 2 | 2 | 4 | 1.00 | 1.1 | 1 | false |
| `bait_shrimp` | Shrimp | 5 | 2 | 10 | 0.80 | 0.8 | 1 | false |
| `bait_raw_meat` | Raw Meat | 10 | 1 | 10 | 0.70 | 0.5 | 0 | false |

### 1.4 Fight profiles (spec 9.6 columns + C 7.7 motion seeds)

Ranges (plan, except motion ranges and the Thrasher rule from C 7.14): pullStrength 0.1-1.0; lateralSpeed 0.2-1.5; runSpeed 0.1-1.5; staminaDrain 0.01-0.2; calmMin 0.2-5 and calmMax calmMin..6; burstMin 0.3-6 and burstMax burstMin..8; flipRate 0-2; diveChance 0-1; burstRamp 0-2; calmDrift 0-0.5; calmDriftRamp 0-5; flipMinGap 0.2-2; lateralJitter 0-0.5; `thrasher` calmMin >= 1.0.

| id | displayName | pull | lateral | run | drain | calmMin-Max | burstMin-Max | flipRate | dive | burstRamp | calmDrift | calmDriftRamp | flipMinGap | lateralJitter |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `steady` | Steady | 0.30 | 0.60 | 0.30 | 0.08 | 1.0-2.5 | 1.5-2.5 | 0 | 0 | **0.6** | 0 | 0 | 0.5 | 0 |
| `darter` | Darter | 0.45 | 0.70 | 0.40 | 0.07 | **0.3-0.8** | **0.4-0.9** | **1.0** | 0 | 0 | 0 | 0 | **0.3** | 0 |
| `bulldog` | Bulldog | 0.60 | 0.60 | 0.50 | 0.06 | 1.0-3.0 | 2.0-4.0 | 0 | 0.35 | 0 | **0.12** | **2.5** | 0.5 | 0 |
| `thrasher` | Thrasher | 0.70 | 1.00 | 0.60 | 0.09 | 1.0-2.5 | 1.5-3.0 | 0.5 | 0 | 0 | 0 | 0 | 0.5 | **0.3** |
| `runner` | Runner | 0.85 | 0.80 | 0.80 | 0.05 | 1.0-2.0 | 2.5-4.0 | 0.1 | 0.25 | **0.8** | 0 | 0 | 0.5 | 0 |

Bold = C 7.7 values that differ from the spec 9.6 table or the motion defaults. The spec 9.6 table row for Darter (calm 1.0-2.5, burst 1.5-3.0, flipRate 0.2) is **not** the seed; C 7.7 overrides it.

### 1.5 Species (spec 9.6 table + 12.3 presentation fields; R62 model files)

Common seeds: `weightSkew` 2.2, `lengthShapeK` 1.0, `hookWindow` 0, `isSpecial` false, `model` null at creation (filled by the 6.2 rule). Rarity, body shape and colours are presentation seeds of this plan. Ranges (plan): baseValue >= 1; weightMinG >= 1 and weightMaxG > weightMinG; weightSkew 1.2-3.0 (`FishingConstants.SkewMin/Max`); lengthShapeK 0.5-2.0; biteWaitMod 0.5-2; hookWindow 0 or 0.8-1.2 (spec 9.4); minBaitRating 0-20; minDepth 0-6 (`DepthMultFullDepth`); fight non-null and listed.

| id | displayName | profile | g min-max | baseValue | biteWaitMod | minBaitRating | minDepth | rarity | bodyShape | colorA | colorB | modelFile |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `bluegill` | Bluegill | steady | 200-800 | 5 | 0.9 | 1 | 0.8 | Common | Round | (0.32,0.50,0.62) | (0.95,0.62,0.22) | `Fish_Bluegill.fbx` |
| `perch` | Perch | darter | 300-1200 | 8 | 1.0 | 3 | 1.2 | Common | Long | (0.62,0.68,0.30) | (0.20,0.24,0.16) | `Fish_Perch.fbx` |
| `carp` | Carp | bulldog | 1000-4000 | 12 | 1.2 | 1 | 1.5 | Common | Round | (0.72,0.58,0.30) | (0.45,0.34,0.18) | `Fish_Carp.fbx` |
| `trout` | Trout | thrasher | 600-2500 | 20 | 0.9 | 5 | 2.0 | Uncommon | Long | (0.55,0.62,0.55) | (0.88,0.45,0.48) | `Fish_Trout.fbx` |
| `catfish` | Catfish | runner | 2000-7000 | 35 | 1.4 | 6 | 2.5 | Uncommon | Long | (0.36,0.34,0.30) | (0.70,0.66,0.56) | `Fish_Catfish.fbx` |

Colours are RGB with alpha 1. Reachability (validator + test): a species is reachable when some bait has `EffRating(1) >= minBaitRating` and `minDepth <= 6`. With these seeds: worm (3) reaches bluegill, perch, carp; shrimp (5) trout; raw meat (10) catfish; bare hook (1) only bluegill and carp (matches T6 `PlaceholderRoller_BareHookShallow_OnlySteadyBulldogOrJunk`).

### 1.6 Junk (spec 9.6; value range 0-5 per spec 12.3)

| id | displayName | valueCoins | bodyShape | color |
|---|---|---|---|---|
| `old_boot` | Old Boot | 2 | Long | (0.30,0.22,0.16) |
| `tin_can` | Tin Can | 2 | Round | (0.68,0.70,0.72) |

### 1.7 Art palette (spec 10.2/10.3; plan colour seeds, M3 retunes)

- `slotNames[16]`: Grass, Foliage, FoliageDark, Wood, WoodDark, Sand, Rock, Reed, Dock, Roof, Accent, Snow, Reserved12, Reserved13, Reserved14, Reserved15.
- `faceJitter` 0.04; `skinTone` = `SlotPalette.GetSkinRgb` (0.96, 0.80, 0.66); `m2Season` 1 (Summer).
- `seasons[4]` = Spring, Summer, Autumn, Winter; each `colors[16]` RGB, alpha 1. Summer is the base; the other seasons copy Summer and override slots 0, 1, 2, 7. Slots 12-15 are (0.5,0.5,0.5).

| Slot | Summer | Spring | Autumn | Winter |
|---|---|---|---|---|
| 0 Grass | (0.42,0.66,0.30) | (0.50,0.74,0.34) | (0.62,0.60,0.30) | (0.80,0.84,0.86) |
| 1 Foliage | (0.30,0.52,0.24) | (0.40,0.62,0.30) | (0.85,0.50,0.18) | (0.32,0.44,0.36) |
| 2 FoliageDark | (0.17,0.34,0.17) | (0.22,0.42,0.20) | (0.55,0.26,0.12) | (0.18,0.28,0.24) |
| 3 Wood | (0.66,0.48,0.30) | = | = | = |
| 4 WoodDark | (0.40,0.27,0.17) | = | = | = |
| 5 Sand | (0.88,0.80,0.58) | = | = | = |
| 6 Rock | (0.55,0.55,0.53) | = | = | = |
| 7 Reed | (0.62,0.68,0.36) | (0.56,0.70,0.34) | (0.72,0.62,0.32) | (0.66,0.62,0.48) |
| 8 Dock | (0.58,0.44,0.30) | = | = | = |
| 9 Roof | (0.70,0.30,0.24) | = | = | = |
| 10 Accent | (0.95,0.45,0.15) | = | = | = |
| 11 Snow | (0.95,0.96,0.98) | = | = | = |

`phases[4]` (`DayPhaseAmbient`, spec 10.3 order Dawn, Day, Dusk, Night):

| Field | Dawn | Day | Dusk | Night |
|---|---|---|---|---|
| startFraction | 0 | 0.10 | 0.55 | 0.70 |
| lightColor / lightIntensity | (1.00,0.78,0.60) / 0.8 | (1.00,0.96,0.88) / 1.1 | (1.00,0.62,0.40) / 0.8 | (0.62,0.70,0.92) / 0.25 |
| skyTop | (0.38,0.52,0.78) | (0.33,0.58,0.92) | (0.30,0.30,0.58) | (0.04,0.06,0.16) |
| skyHorizon | (0.98,0.74,0.56) | (0.74,0.86,0.96) | (0.98,0.56,0.36) | (0.12,0.16,0.30) |
| skyGround | (0.36,0.34,0.36) | (0.40,0.40,0.36) | (0.30,0.26,0.30) | (0.05,0.05,0.08) |
| toonShade | (0.42,0.40,0.52) | (0.46,0.50,0.62) | (0.40,0.32,0.46) | (0.12,0.14,0.26) |
| toonAmbient | (0.70,0.62,0.66) | (0.80,0.86,0.92) | (0.66,0.52,0.56) | (0.30,0.36,0.52) |
| fogColor / fogDensity | (0.86,0.74,0.66) / 0.010 | (0.74,0.84,0.92) / 0.008 | (0.80,0.56,0.46) / 0.010 | (0.10,0.13,0.22) / 0.014 |
| warmWeight / coolWeight | 0.6 / 0 | 0 / 0 | 1 / 0 | 0 / 1 |

UI colours (RGBA): uiAccent (0.95,0.62,0.20,1), uiGood (0.40,0.80,0.40,1), uiWarn (0.98,0.78,0.25,1), uiBad (0.90,0.30,0.25,1), uiPanel (0.08,0.10,0.14,0.85), uiText (0.95,0.95,0.92,1).

Palette rules (validator, plan): 16 slot names; 4 seasons with 16 colours each; 4 phases with strictly ascending `startFraction` starting at 0 and below 1; every `fogDensity` in (0, 0.033] (spec 10.3 readability rule); `faceJitter` 0-0.1; `m2Season` 0-3.

### 1.8 Water settings (C 7.13 `WaterSettingsDef`, C 7.14 `FishingSeedData.Waves`)

| Wave | directionDeg (x) | amplitude m (y) | wavelength m (z) | speed m/s (w) |
|---|---|---|---|---|
| W0 | 20 | 0.050 | 9.0 | 1.2 |
| W1 | 75 | 0.040 | 7.0 | 1.0 |
| W2 | 320 | 0.025 | 6.5 | 0.8 |

Sum of amplitudes 0.115 <= 0.12; every wavelength >= 6 (4 x the 1.25 m grid cell of C 6.4 is 5 m). `shallowColor` (0.36,0.72,0.70,0.75), `deepColor` (0.08,0.28,0.42,0.95), `depthRange` 3.0, `foamWidth` 0.35. Rules (validator): exactly 3 waves; amplitude 0-0.08 each and sum <= 0.12; wavelength >= 5; speed 0-3; depthRange > 0; foamWidth > 0.

### 1.9 Cue table rows (C M2-D11)

One `SfxCueRow` per recipe in `SfxRecipes.All`, in that order: `id`, `category`, `baseVolume`, `minDistance`, `maxDistance`, `minInterval`, `spatial` copied from the Core recipe (`MinDistance` from T11's field). Rules (validator): ids unique and known to `SfxRecipes.TryGet`; baseVolume 0-1; minDistance > 0; maxDistance > minDistance; minInterval >= 0.

---

## 2. T13 - Data defs and registry

- **Goal:** every Def of C 7.13 as a `ScriptableObject` with `Configure` and `ToCore`, plus `ContentRegistry` fishing categories, singletons and the Core `FishingDatabase`.
- **Owned files (C 12):** `Assets/_Project/Data/Fishing/{RodDef,BaitDef,FightProfileDef,FishSpeciesDef,JunkDef,FishingTuningDef}.cs`, `Data/Audio/SfxCueTableDef.cs`, `Data/Art/{ArtPaletteDef,PaletteSeason,DayPhaseAmbient,WaterSettingsDef}.cs`, `Data/ContentRegistry.cs`; tests `Tests/EditMode/FishingDefsTests.cs` (new), `Tests/EditMode/ContentRegistryTests.cs` (add).
- **Shared edits (C 2.6):** `Data/ContentRegistry.cs` (T13 is its only M2 editor).
- **Depends on:** T5 (structs, `FishingDatabase`), T6, T11 (`SfxRecipe` 9-arg ctor, `SfxRecipes.All`), T12 (`Wave`, `WaveSet`). All committed.
- **Implementer:** Sonnet. **Reviewer:** Sonnet.
- **C 2.8 that bite:** #2 ASCII; #8 EditMode reach (tests use Data + Core only); 5.1 no tuning field initialisers.

### 2.1 Def rules (all Defs)

- `namespace FishGame.Data`, `public sealed class XDef : ScriptableObject`, one type per file, file header comment `// Content data only (not networked). <one line purpose>. Owned by T13.`
- Private `[SerializeField]` fields with exactly the C 7.13 names, in the C 7.13 order. No field initialisers except `m2Season = 1` (C 7.13) and empty collections (`new List<...>()`, arrays left null are allowed; getters return empty for null).
- One read-only getter per field, PascalCase (`castMaxDist` -> `CastMaxDist`). Collections are exposed as `IReadOnlyList<T>`; Unity references as their type (`FishSpeciesDef.Fight` is `FightProfileDef`, `Model` is `Mesh`).
- `public void Configure(...)` takes every field in the listed order (collections as `IEnumerable<T>` or arrays) and copies collections defensively. `FishingTuningDef.Configure(in FishingTuning t)` is the exception (C 7.13).
- `ToCore()` copies fields one to one into the C 7.5 struct (same names, PascalCase). Types: `tier` byte; counts and coins `int`; everything else float or bool or the Core enum.

### 2.2 Per-Def specifics

| Def | Behaviour beyond 2.1 |
|---|---|
| `RodDef` | `ToCore()` -> `RodStats` (`shopCost`, `firstCamp`, `displayName` are Data-only). |
| `BaitDef` | `ToCore()` -> `BaitStats` (`EffRating` is T5's). |
| `FightProfileDef` | `ToCore()` -> `FishFightProfile`, all 15 numeric fields including the 5 motion fields. |
| `FishSpeciesDef` | `ToCore(ushort fightProfileIndex)` -> `FishSpeciesData` with `FightProfileIndex = fightProfileIndex`; presentation fields (`displayName`, `bodyShape`, colours, `model`) stay Data-only. Field types: `rarity` `FishRarity`, `bodyShape` `BodyShape`, `colorA/colorB` `Color`, `model` `Mesh` (may be null). |
| `JunkDef` | `ToCore()` -> `JunkData { ValueCoins }`. |
| `FishingTuningDef` | One field per `FishingTuning` member in struct order and camelCase, **except** `CreelMaxWeightG`, which is stored as `creelMaxKg` (float) at its position. `ToCore()`: `CreelMaxWeightG = (int)Math.Round(creelMaxKg * 1000.0, MidpointRounding.AwayFromZero)`. `Configure(in FishingTuning t)`: `creelMaxKg = t.CreelMaxWeightG / 1000f`; all other members copied. |
| `SfxCueTableDef` | `[Serializable] public struct SfxCueRow { public string id; public AudioCategory category; public float baseVolume, minDistance, maxDistance, minInterval; public bool spatial; }` (declared in `SfxCueTableDef.cs`). Field `rows` (`List<SfxCueRow>`), getter `Rows`, `Configure(IEnumerable<SfxCueRow> rows)`. `ToRecipes()`: for each row whose `id` is found by `SfxRecipes.TryGet`, a new `SfxRecipe` with `Id`, `LengthSeconds`, `Loop` from the Core recipe and the six mixing fields from the row (use T11's 9-arg ctor; check its parameter order in `Core/Audio/SfxRecipe.cs`). Unknown ids are skipped (no logging in Data). Returns a new list each call, in row order. |
| `PaletteSeason` | `[Serializable] public sealed class PaletteSeason { public string name; public Color[] colors; }` (public fields, like `SfxCueRow`). |
| `DayPhaseAmbient` | `[Serializable] public struct DayPhaseAmbient` with public fields in C 7.13 order: `startFraction` float, `lightColor` Color, `lightIntensity` float, `skyTop`, `skyHorizon`, `skyGround`, `toonShade`, `toonAmbient`, `fogColor` Color, `fogDensity`, `warmWeight`, `coolWeight` float. |
| `ArtPaletteDef` | Fields per C 7.13 (`slotNames` string[], `seasons` PaletteSeason[], `faceJitter`, `skinTone`, `phases` DayPhaseAmbient[], six UI Colors, `m2Season` int = 1). **No `slotTints` field** (M2-D3). `PaletteVectors(int season)`: `ArgumentOutOfRangeException` when `season` is outside 0..3 or `seasons` is shorter than season + 1; returns a new `Vector4[16]` with `(r, g, b, a)` of `colors[i]`, and `(1, 0, 1, 1)` (magenta) for any missing entry (so the global array is always full length, C 2.8 #18). `GetSlotTint(int slot)`: `SlotPalette.GetRgb(slot, ...)` -> `new Color(r, g, b, 1f)` (exceptions pass through). `GetSkinTone()` returns `skinTone`. |
| `WaterSettingsDef` | Fields `dirAmpLenSpeed` (Vector4[]), `shallowColor`, `deepColor` (Color), `depthRange`, `foamWidth` (float); seeds 3.0 and 0.35 come from T14, not initialisers. `ToWaveSet()`: `W0..W2 = new Wave { DirectionDeg = v.x, Amplitude = v.y, Wavelength = v.z, Speed = v.w }` for entries 0..2; a missing entry is a zero `Wave` (flat term). |

### 2.3 `ContentRegistry` (edit; M1 members and behaviour unchanged)

- Add the C 7.13 constants, five serialized lists (`rods`, `baits`, `fightProfiles`, `species`, `junk`, each `new List<...>()`), four singleton fields (`fishingTuning`, `sfxCueTable`, `artPalette`, `waterSettings`), getters, `ConfigureFishing(...)`, the index/at/try methods, `Fishing`, `DisplayNameOf`.
- **Name clash:** the property `FishingTuning` (type `FishingTuningDef`) hides the Core struct name inside `ContentRegistry`. Inside this class write the Core type as `global::FishGame.Core.FishingTuning` (or use `var`).
- **Built flag:** replace the "`spotsIndex == null` means not built" check with a `[NonSerialized] private bool built;`. `Configure` and `ConfigureFishing` clear every cache and `built`. `ConfigureFishing` does not touch spots or `dataVersion`; M1 `Configure` does not touch the fishing fields.
- **`Validate()`** keeps the M1 spot messages and order, then appends, per fishing list in the order rods, baits, fightProfiles, species, junk (list names exactly as the serialized field names):
  - `"<list>[i] is null."`, `"<list>[i] has invalid id '<id>'."`, `"<list>[i] duplicate id '<id>'."` (same wording as spots, invalid ids skip the duplicate check);
  - species: `"species[i] '<id>' has no fight profile."`, `"species[i] '<id>' fight '<fightId>' is not listed in fightProfiles."` (reference containment in `fightProfiles`);
  - when any fishing list is non-empty: `"rods is empty."` / `"baits is empty."` / `"fightProfiles is empty."` for an empty one of those three, `"fishingTuning is missing."`, `"sfxCueTable is missing."`, `"artPalette is missing."`, `"waterSettings is missing."`, and `"baits has no unlimited bait."` when no non-null bait has `Unlimited`.
  - With every fishing list empty, no fishing message is produced (the M1 registry stays valid).
- **`Build()`:** throws `InvalidOperationException` on validation errors exactly like M1. Builds one `RegistryIndex` plus an id dictionary per category (spots unchanged). When any fishing list is non-empty, builds `Fishing = new FishingDatabase(rods, baits, profiles, species, junk, tuning)` with arrays in index order: `rods[i] = RodAt(i).ToCore()` and so on, `species[i] = SpeciesAt(i).ToCore(FightProfileIndex(SpeciesAt(i).Fight.Id))`, `tuning = fishingTuning.ToCore()`. Otherwise `Fishing = null`. Sets `built = true`.
- Index/at methods: `RodIndex(string)`, `RodAt(ushort)`, `BaitIndex`/`BaitAt`, `FightProfileIndex`/`FightProfileAt`, `SpeciesIndex`/`SpeciesAt`, `JunkIndex`/`JunkAt`, `TryGetSpecies` - same shapes and exceptions as the spot methods (`KeyNotFoundException`, `ArgumentOutOfRangeException` from `RegistryIndex`), each calling `EnsureBuilt()` first. `Fishing` getter calls `EnsureBuilt()`.
- `DisplayNameOf(bool isJunk, ushort itemIndex)`: `JunkAt(i).DisplayName` or `SpeciesAt(i).DisplayName`.
- **`ComputeContentHash()`:** spots always; each fishing category (`CategoryRods`, ...) **only when its list is non-empty**. A spots-only registry therefore hashes exactly as in M1, so the M1 test `ContentRegistry_Hash_StableAcrossInstances` keeps passing (`ContentHash.Compute` appends the category name even for an empty id list). `dataVersion` stays 1 (C 5.3). See Contract question Q1.
- Header comment: replace "M1 holds one category (spots)" with "Categories: spots (M1), rods, baits, fight_profiles, species, junk (M2). Fishing database built in Build() when fishing content is present." Keep "Owned by" text as is.

### 2.4 Steps (TDD, C 2.4)

1. Write `FishingDefsTests.cs` (cases in 2.5) and the four `ContentRegistryTests` additions. `compile` fails (types missing): expected red.
2. Write `PaletteSeason`, `DayPhaseAmbient`, then the six Fishing Defs, `SfxCueTableDef`, `ArtPaletteDef`, `WaterSettingsDef`. `compile` PASS. `test-edit -Filter "FishGame.Tests.EditMode.FishingDefsTests"`: all pass.
3. Edit `ContentRegistry`. `test-edit -Filter "FishGame.Tests.EditMode.ContentRegistryTests"`: all pass, including the M1 tests unchanged.
4. Full `test-edit` (whole EditMode suite, C 2.4). `ValidationTests` still pass (the registry asset has no fishing content yet, so `Validate()` adds nothing).
5. Commit.

### 2.5 Tests (exact names, C 10.3)

`Tests/EditMode/FishingDefsTests.cs` (class `FishingDefsTests`; `[TearDown]` destroys every created ScriptableObject, like M1 `ContentRegistryTests`):

| Test | Cases |
|---|---|
| `FishingDefs_ToCore_MapsEveryField` | `[TestCase("rod")] ("bait") ("fight_profile") ("species") ("junk") ("tuning")`. Each case calls `Configure` with a distinct non-zero value per field (for example 1.1, 2.2, 3.3 ... so a swapped pair fails), then asserts every getter and every `ToCore()` field. Species: `ToCore(7)` gives `FightProfileIndex == 7`. Tuning: `Configure(in t)` then `ToCore()` equals `t` member by member, with `CreelMaxWeightG = 12345` round-tripping to 12345 and `CreelMaxKg == 12.345f` within 1e-4. |
| `ArtPaletteDef_SlotTint_EqualsSlotPalette` | slots 0..3 equal `SlotPalette.GetRgb` with alpha 1; slot 4 throws `ArgumentOutOfRangeException`. |
| `ArtPaletteDef_PaletteVectors_LengthSixteen` | 4 seasons of 16 distinct colours: `PaletteVectors(s)` length 16 and equal component-wise for s = 0..3; a season with 3 colours still returns 16 with magenta padding; season -1 and 4 throw. |
| `WaterSettingsDef_ToWaveSet_MapsComponents` | three Vector4 entries map to `W0..W2` (x -> DirectionDeg, y -> Amplitude, z -> Wavelength, w -> Speed); a 2-entry array gives a zero `W2`. |
| `SfxCueTableDef_ToRecipes_OverridesMixingKeepsLength` | rows for `SfxIds.SplashBig` (changed category, volume, min/max distance, interval, spatial) and an unknown id `"no_such_cue"`: result count 1; mixing fields from the row; `LengthSeconds` and `Loop` equal `SfxRecipes.TryGet(SfxIds.SplashBig)`; a `LineCreak` row keeps `Loop == true`. |

`Tests/EditMode/ContentRegistryTests.cs` (add; test helper `MakeFishingSet()` builds 2 rods, 2 baits (one unlimited), 2 profiles, 2 species, 1 junk, and the four singletons, all in memory):

| Test | Cases |
|---|---|
| `ContentRegistry_FishingCategories_IndexedOrdinal` | lists passed scrambled (`rod_twig` before `rod_carbon`, ...): `RodIndex("rod_carbon") == 0`, `RodAt(1).Id == "rod_twig"`; same for baits, fight profiles, species, junk; `TryGetSpecies` true/false; unknown id throws `KeyNotFoundException`; index past the end throws `ArgumentOutOfRangeException`; `DisplayNameOf(false, i)` and `(true, 0)`. |
| `ContentRegistry_FishingCategories_ChangeHash` | spots-only hash equals `ContentHash.Compute` over the spots category alone; adding fishing content changes it; adding one more species changes it; the same content in another list order gives the same hash. |
| `ContentRegistry_SpeciesWithForeignProfile_FailsValidate` | species whose `fight` is a profile not in `fightProfiles` -> exactly the message `species[0] '<id>' fight '<fightId>' is not listed in fightProfiles.`; a null fight -> `has no fight profile.`; `Build()` throws `InvalidOperationException`. Also: no unlimited bait -> `baits has no unlimited bait.`; a missing singleton -> `fishingTuning is missing.` |
| `ContentRegistry_Fishing_BuildsDatabase` | full set: `Fishing` non-null; counts equal list sizes; `Fishing.Species[i].FightProfileIndex == FightProfileIndex(SpeciesAt(i).Fight.Id)`; `Fishing.UnlimitedBaitIndex == BaitIndex(<unlimited id>)`; `Fishing.Tuning` equals the tuning Def's `ToCore()`; a spots-only registry has `Fishing == null`. |

### 2.6 Acceptance (reviewer checks)

- Field names and order match C 7.13 exactly (diff against the table); getters PascalCase; no tuning initialisers; no `slotTints`.
- `FishingTuningDef` covers every `FishingTuning` member (count them: 39 struct members, 39 serialized fields with `creelMaxKg` replacing `CreelMaxWeightG`).
- M1 `ContentRegistryTests` unchanged and green; spots-only hash unchanged.
- No Unity logging in Data; `PaletteVectors` always returns length 16.
- Full `test-edit` PASS line pasted in the report.

### 2.7 Commit

```
git add Assets/_Project/Data/Fishing Assets/_Project/Data/Audio Assets/_Project/Data/Art Assets/_Project/Data/Fishing.meta Assets/_Project/Data/Audio.meta Assets/_Project/Data/Art.meta Assets/_Project/Data/ContentRegistry.cs Assets/_Project/Tests/EditMode/FishingDefsTests.cs Assets/_Project/Tests/EditMode/FishingDefsTests.cs.meta Assets/_Project/Tests/EditMode/ContentRegistryTests.cs
git commit -m "m2(data): fishing, audio and art defs; ContentRegistry fishing categories and database" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(`git status` first; add every new `.meta` Unity generated for the new files and folders, nothing else.)

---

## 3. T14 - Seeds, content builder, fishing validators

- **Goal:** one seed source (`FishingSeedData`), a create-if-missing builder that generates every C 6.2 data asset and wires the registry, validators for the fishing data, the cue-table hook in `AppBootstrap`; generate and commit the assets.
- **Owned files (C 12):** `Editor/Builders/Fishing/{FishingSeedData,FishingContentBuilder}.cs`, `Editor/Validation/FishingValidators.cs`; tests `Tests/EditMode/FishingContentTests.cs` (new), `Tests/EditMode/ValidationTests.cs` (add two); generated `Content/{Fishing,Art,Audio}/**` and the updated `Content/ContentRegistry.asset`.
- **Shared edits (C 2.6):** `Editor/Builders/ContentBuilder.cs` (one hook line), `Editor/Validation/ValidationRunner.cs` (one hook line), `App/AppBootstrap.cs` (one hook line), `Editor/Builders/AssetPaths.cs` (constants block).
- **Depends on:** T13, T4. **Implementer:** Sonnet. **Reviewer:** Sonnet.
- **C 2.8 that bite:** #2 ASCII; #6 scene churn after `build`; #11 editor lock; #20 explicit paths; C 6.2 create-if-missing (never overwrite an existing asset's values).

### 3.1 `AssetPaths` block (append, constants only)

```csharp
// T14 additions (fishing, art and audio data; C 6.2). T20/T21 reuse ModelsFolder; do not redeclare it.
public const string FishingFolder = "Assets/_Project/Content/Fishing";
public const string RodsFolder = FishingFolder + "/Rods";
public const string BaitsFolder = FishingFolder + "/Baits";
public const string FightProfilesFolder = FishingFolder + "/FightProfiles";
public const string SpeciesFolder = FishingFolder + "/Species";
public const string JunkFolder = FishingFolder + "/Junk";
public const string FishingTuningAsset = FishingFolder + "/FishingTuning.asset";
public const string ArtFolder = "Assets/_Project/Content/Art";
public const string ArtPaletteAsset = ArtFolder + "/ArtPalette.asset";
public const string WaterSettingsAsset = ArtFolder + "/WaterSettings.asset";
public const string AudioFolder = "Assets/_Project/Content/Audio";
public const string SfxCueTableAsset = AudioFolder + "/SfxCueTable.asset";
public const string ModelsFolder = "Assets/_Project/Content/Models";
```
A Def asset path is `<Folder>/<id>.asset` (C 6.2).

### 3.2 `FishingSeedData` (public surface exactly C 7.14)

- `public static class FishingSeedData` in `namespace FishGame.EditorTools`, header comment `// The single seed source (contract 5.1): spec 9.6, 9.7, 9.12, 9.10 tables, contract 7.7 motion seeds, clean threshold 0.8. Used only to create missing assets and by tests. Owned by T14; T15 updates retuned values.`
- `public readonly struct SpeciesSeed` (top-level, same file): `Id`, `Name`, `ProfileId` (string), `Data` (`FishSpeciesData`; `FightProfileIndex` = the sorted index of `ProfileId`, 0.2), `BodyShape`, `ColorA`, `ColorB` (`UnityEngine.Color`), `ModelFile` (string file name, e.g. `"Fish_Bluegill.fbx"`); a constructor taking all of them.
- Properties return the 1.1-1.6 and 1.8 tables: `Tuning`, `Rods` (`(id, name, RodStats, shopCost, firstCamp)`), `Baits`, `Profiles`, `Species`, `Junk`, `Waves`. Lists are built once in a static constructor and exposed as `IReadOnlyList` (`ReadOnlyCollection`). List order: the table order in section 1 (not sorted).
- `BuildDatabase()`: copies each category, sorts by id with `StringComparer.Ordinal`, maps species `ProfileId` to the sorted profile index, and returns `new FishingDatabase(...)` with `Tuning`.
- Art and audio seeds (palette 1.7, water colours 1.8, cue rows 1.9) live here as `internal static` members (`PaletteSlotNames`, `PaletteSeasons()`, `PalettePhases()`, `PaletteUi...`, `WaterShallowColor`, `WaterDeepColor`, `WaterDepthRange`, `WaterFoamWidth`, `CueRows()`), so 5.1's single seed file holds them without new public surface.
- No `UnityEngine.Random`; colours are literals; floats written with an `f` suffix exactly as in the tables.

### 3.3 `FishingContentBuilder.Build()` (create-if-missing)

Order (each step uses `AssetDatabase.LoadAssetAtPath`; assets are created with `ScriptableObject.CreateInstance` + `Configure` + `AssetDatabase.CreateAsset`; existing assets are never reconfigured):
1. `BuilderUtil.EnsureFolder` for the five fishing folders, `ArtFolder`, `AudioFolder`.
2. Fight profiles, then rods, baits, junk (each from the seeds).
3. Species: `fight` = the profile asset at `FightProfilesFolder/<ProfileId>.asset` (throw `InvalidOperationException` naming the id if missing); `model` null.
4. Singletons: `FishingTuning.asset` (`Configure(in FishingSeedData.Tuning)`), `SfxCueTable.asset` (rows 1.9), `ArtPalette.asset` (1.7), `WaterSettings.asset` (1.8, `dirAmpLenSpeed` from `Waves`).
5. Species models (C 6.2 allowed edit): for every species asset whose `model` is null and whose seed `ModelFile` exists under `ModelsFolder`, take the first `Mesh` from `AssetDatabase.LoadAllAssetsAtPath(path)`. If that mesh has a colour channel (`mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)`), set `model` through `SerializedObject` (`FindProperty("model")`, `ApplyModifiedPropertiesWithoutUndo`, `EditorUtility.SetDirty`). If it has none, leave `model` null and `Debug.Log("FG CONTENT species <id>: <file> has no vertex colours; model left empty (fallback mesh)")` (log, not warning). This keeps `Validation_SpeciesModels_Pass` green while the perch colours are unverified (C G6, skeleton risk 1; Q4).
6. Registry (`AssetPaths.ContentRegistryAsset`; it exists because `ContentBuilder.EnsureRegistry` ran first): through `SerializedObject`, for each list property (`rods`, `baits`, `fightProfiles`, `species`, `junk`) append each seed asset not already referenced; fill each singleton property (`fishingTuning`, `sfxCueTable`, `artPalette`, `waterSettings`) only when null; `ApplyModifiedPropertiesWithoutUndo`; `EditorUtility.SetDirty(registry)`. Throw `InvalidOperationException` if a property is not found (like M1 `EnsureRegistry`).
7. No `SaveAssets` here (`ContentBuilder.Build` saves after the hook).

Re-running `build` with all assets present changes nothing on disk (check in 3.6 step 6).

### 3.4 `FishingValidators` (public surface exactly C 7.14)

- `All()`: `FishingContent()` items prefixed `"FishingContent: "`, then `SpeciesModels()` items prefixed `"SpeciesModels: "`.
- `FishingContent()`: loads the registry asset (missing -> one error and return). Null list entries and id problems are **not** repeated (M1 `Validators.ContentIds` already reports `registry.Validate()`); null entries are skipped. Checks:
  - every seed id of each category has an asset at its C 6.2 path and that asset is in the registry list; the four singletons are present;
  - every Def value inside the section 1 ranges and cross-field rules (tuning 1.1, rods 1.2, baits 1.3 including exactly one unlimited, profiles 1.4 including `thrasher` calmMin >= 1.0, species 1.5, junk 1.6, palette 1.7 rules, water 1.8 rules, cue rows 1.9 rules);
  - every species reachable (1.5 rule, using `BaitDef.ToCore().EffRating(1)`).
  - Message format (InvariantCulture, `G` floats): `"<category> <id>: <field> = <value> outside [<min>, <max>]"`, `"<category> <id>: <rule text>"` for cross-field rules, `"species <id>: not reachable with any bait at depth <= 6 m"`, `"missing asset <path>"`, `"<path> is not listed in ContentRegistry.<list>"`.
- `SpeciesModels()`: for each non-null species `model`: `AssetDatabase.GetAssetPath(model)` must start with `ModelsFolder + "/"` and end with `.fbx` (ordinal ignore case), and the mesh must have a colour channel. Messages `"species <id>: model is not from <ModelsFolder>/*.fbx (<path>)"`, `"species <id>: model <path> has no vertex colours"`. Null models are allowed.

### 3.5 Hooks (one line each; hot-file rule C 2.3)

- `ContentBuilder.Build()`: `FishingContentBuilder.Build();` after `EnsurePanelSettings();`, before `AssetDatabase.SaveAssets();`.
- `ValidationRunner.Run()`: `all.AddRange(FishingValidators.All());` after the last existing `Add(...)` (T4's three), before `return all;`.
- `AppBootstrap.Awake`: right after `ContentHashValue = contentRegistry.ComputeContentHash();`: `audio.ApplyCueTable(contentRegistry.SfxCueTable != null ? contentRegistry.SfxCueTable.ToRecipes() : null);` (the C 2.6 hook with this file's variable names; an explicit `!= null` instead of `?.` because a missing Unity reference is a fake null that `?.` does not catch).

### 3.6 Steps (TDD)

1. Write `FishingContentTests.cs` (3.7) and the two `ValidationTests` lines. `compile`: red (types missing).
2. Write the `AssetPaths` block and `FishingSeedData`. `test-edit -Filter "FishGame.Tests.EditMode.FishingContentTests.FishingData_SeedsMatchSpecTables"` PASS (seed-only test).
3. Write `FishingContentBuilder` and the `ContentBuilder` hook. Check `Temp/UnityLockfile`; `build`. Expect `FG BUILDALL PASS` once the validator exists (step 4); until then the build passes without fishing validation.
4. Write `FishingValidators` and the `ValidationRunner` hook. `build` again: `FG BUILDALL PASS`.
5. Write the `AppBootstrap` hook. `compile` PASS.
6. `git restore Assets/_Project/Scenes/*.unity`. `git status --porcelain` must show only: new `Assets/_Project/Content/Fishing/**`, `Content/Art/**`, `Content/Audio/**` (assets + `.meta`, folder metas), modified `Content/ContentRegistry.asset`, the owned code files and their metas. Run `build` a second time, restore scenes, and confirm `git status` is unchanged (idempotent builder).
7. Full `test-edit` (PlayMode not required before T19, C 2.4). All green, including `FishingContentTests`, `Validation_FishingContent_Pass`, `Validation_SpeciesModels_Pass`, and M1 `Validation_ContentIds_Pass`.
8. Open `Content/ContentRegistry.asset` and `Content/Fishing/FightProfiles/darter.asset` as text and confirm the serialized values match 1.4 (paste the darter YAML numbers in the report).
9. Commit.

### 3.7 Tests (exact names, C 10.3)

`Tests/EditMode/FishingContentTests.cs` (class `FishingContentTests`). "Assets" means the generated assets loaded through the registry at `AssetPaths.ContentRegistryAsset` (which `build` produced); "seeds" means `FishingSeedData`.

| Test | Cases |
|---|---|
| `FishingData_Ids_UniquePerCategory` | seeds: per category every id passes `ContentIds.IsValidId` and is unique (ordinal); assets: `registry.Validate()` is empty and each fishing list count equals its seed count. |
| `FishingData_Ranges_Valid` | assets: every value of every fishing Def inside the section 1 ranges and cross-field rules, via a test-local copy of the 1.1-1.8 range table (the plan's tables are the single source; the reviewer checks both copies match). One assertion message per violation, collected, then `Assert.IsEmpty`. Also `FishingSeedData.BuildDatabase()` does not throw. |
| `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth` | assets via `registry.Fishing`: for each species some bait index `b` gives `PlaceholderRoller.IsEligible(species, baits[b].EffRating(1), 6f)` true; also each species has some bait in the M2 debug loadout (every bait is, C M2-D14). |
| `FishingData_SeedsMatchSpecTables` | seeds vs test-local literal copies of: spec 9.12 (all 1.1 seeds incl. assist values, creel 12 / 15000 g, clean 0.8), spec 9.7 rods and baits (every column incl. shopCost/firstCamp), spec 9.6 profile columns **except** the motion fields and Darter's calmMin/calmMax/burstMin/burstMax/flipRate, spec 9.6 species columns (kg -> g, weightSkew 2.2, lengthShapeK 1.0, hookWindow 0), junk value 2; and the C 7.7 motion seeds (all five profiles, all five motion fields, plus Darter calm 0.3-0.8, burst 0.4-0.9, flipRate 1.0). Exact float equality. Values T15 retunes are taken from a `TunedDeviations` map at the top of the file (`static readonly Dictionary<string, float> TunedDeviations`, key `"<category>.<id>.<field>"`, e.g. `"profile.thrasher.staminaDrain"`); T14 creates it **empty** (Q2). |
| `FishingData_ThrasherCalm_AtLeastOneSecond` | seed and asset `thrasher` calmMin >= 1.0 and calmMax >= calmMin. |
| `FishingData_AssetsExistForEverySeed` | each seed id has an asset at `<Folder>/<id>.asset` whose `Id` equals the seed id, listed in the registry; the four singletons exist at their paths and are the registry's references; every species asset's `Fight.Id` equals its seed `ProfileId`. |

`Tests/EditMode/ValidationTests.cs` (add, M1 style): `[Test] public void Validation_FishingContent_Pass() => AssertNoErrors(FishingValidators.FishingContent());` and `[Test] public void Validation_SpeciesModels_Pass() => AssertNoErrors(FishingValidators.SpeciesModels());`.

### 3.8 Acceptance (reviewer checks)

- Seeds equal section 1 (spot-check every Darter, Thrasher, Bulldog motion value and the tuning table line by line).
- Builder never reconfigures an existing asset (read the code: every `Configure` is inside an `if (asset == null)` branch); the second `build` leaves `git status` unchanged.
- Generated asset count: 4 rods, 6 baits, 5 profiles, 5 species, 2 junk, 4 singletons (+ metas); registry lists hold exactly these.
- Hooks are one line each, placed as in 3.5; `AssetPaths` has only constants.
- `FishingValidators` adds no public members beyond C 7.14; no message duplicates `registry.Validate()`.
- `build` and full `test-edit` PASS lines pasted.

### 3.9 Commit

```
git add Assets/_Project/Editor/Builders/Fishing Assets/_Project/Editor/Builders/Fishing.meta Assets/_Project/Editor/Validation/FishingValidators.cs Assets/_Project/Editor/Validation/FishingValidators.cs.meta Assets/_Project/Editor/Builders/ContentBuilder.cs Assets/_Project/Editor/Validation/ValidationRunner.cs Assets/_Project/App/AppBootstrap.cs Assets/_Project/Editor/Builders/AssetPaths.cs Assets/_Project/Tests/EditMode/FishingContentTests.cs Assets/_Project/Tests/EditMode/FishingContentTests.cs.meta Assets/_Project/Tests/EditMode/ValidationTests.cs Assets/_Project/Content/Fishing Assets/_Project/Content/Fishing.meta Assets/_Project/Content/Art Assets/_Project/Content/Art.meta Assets/_Project/Content/Audio Assets/_Project/Content/Audio.meta Assets/_Project/Content/ContentRegistry.asset
git commit -m "m2(data): fishing seed data, create-if-missing content builder, fishing validators, generated data assets" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
If `Content/Models/Fish_Perch.fbx` got no model reference (no vertex colours), say so in the report (T29 follow-up).

---

## 4. T15 - Tuning bots and retune

- **Goal:** three scripted bots (Greedy, CounterReel, Rhythm) that fight the **real assets** through `FightSim`, print distributions and signature-motion stats, assert the spec 9.12 targets, and retune data (assets + seeds) until the targets pass or are recorded as tuning items.
- **Owned files (C 12):** `Assets/_Project/Tests/EditMode/Tuning/FishingBots.cs` (new, with the `Tuning` folder meta); retuned `.asset` values under `Content/Fishing/**` with matching seeds in `Editor/Builders/Fishing/FishingSeedData.cs` (C 2.6 data-asset row); the `TunedDeviations` map in `Tests/EditMode/FishingContentTests.cs` (pending Q2).
- **Shared edits (C 2.6):** data assets and `FishingSeedData` seeds only. Never `FightSim` or any Core file (a target that needs code stops the task: report to the controller; the review then goes to Opus high).
- **Depends on:** T7 (`FightSim`), T14 (assets). **Implementer:** Sonnet. **Reviewer:** Sonnet (Opus high if a target needs more than data).
- **C 2.8 that bite:** #16 (bots draw nothing random: seeds are the fixed formula below); #21 results in `$env:FG_OUT`; #20 explicit paths; #2 ASCII in the printed lines.
- **Background (do not add mechanics):** the Stardew notes' recommendation 5 is already adopted as the clean-catch bonus (C M2-D16), which the bots measure; recommendation 1 (bite wait) is not adopted (spec 9.4 unchanged) and the bots never touch bite timing.

### 4.1 File and class layout

- `namespace FishGame.Tests.EditMode`; `[Category("Tuning")] public class FishingBots` (the class name keeps every test name under the C 11 filter `FishGame.Tests.EditMode.FishingBots`).
- Private nested helpers (free to name): `enum BotKind { Greedy, CounterReel, Rhythm }`, a bot decision struct, `FightRecord`, `BatchStats`, `SignatureStats`, a loaded-data holder, a batch cache.
- The file must not reference `FishingSeedData` (reviewer greps). Bots use only `UnityEditor.AssetDatabase`, the Defs, `ToCore()`, `FightSim`, `FightInput`, `FishingConstants`, `AssetPaths`.

### 4.2 Loading the real assets

`LoadData()` (once per batch call; cheap):
- `tuning = AssetDatabase.LoadAssetAtPath<FishingTuningDef>(AssetPaths.FishingTuningAsset).ToCore()`.
- Rods `rod_twig`, `rod_handmade` from `AssetPaths.RodsFolder + "/<id>.asset"`; profiles `steady`, `darter`, `bulldog`, `thrasher`, `runner` from `AssetPaths.FightProfilesFolder + "/<id>.asset"`; each `.ToCore()`.
- A missing asset fails the test with `"missing <path>: run Tools/unity.ps1 build first"`.

### 4.3 One fight (exact procedure)

```
inputs : bot kind, FishFightProfile p, RodStats rod, FishingTuning t, weightT, seed
start  : startLine = rod.CastMaxDist          // 18 m Handmade, 14 m Twig (C 7.15, spec 9.6 "full 18 m cast")
         sim = new FightSim(p, rod, t, weightT, seed, startLine, assist: false)
         easing = false                       // Rhythm only
         maxTicks = (int)Math.Ceiling(t.MaxFightTime / FightSim.Step)
loop k = 0 .. maxTicks-1:
         s = sim.State                         // read BEFORE the tick (state after the previous tick)
         input = Decide(kind, s, ref easing)
         record per-tick signature data (4.6) around the tick
         if sim.Tick(input) != FightOutcome.None: break
result : Outcome/Reason/Elapsed/NearSnapRecoveries/MaxTension from sim.State;
         if no outcome after maxTicks: outcome "Timeout" (bot-side; mirrors the host's maxFightTime)
```

### 4.4 Bot decisions (identical rules for every implementer)

Constants in the file: `const float CounterHold = 0.1f;` (equal to `FishingConstants.CenterDeadzone`; Q3), `const float EaseOnTension = 0.8f;`, `const float EaseOffTension = 0.5f;`.

```
sideRun   = s.Phase == FightPhase.Burst && s.BurstDir != 0
dive      = s.Phase == FightPhase.Burst && s.BurstDir == 0
counter   = sideRun && s.BurstDir * s.Lateral > -CounterHold      // keep countering until the fish is CounterHold past centre
counterSteer = counter ? -s.BurstDir : 0                          // spec 9.1: steer A (-1) against a run to the right (+1)

Greedy      : steer = 0;            reel = true
CounterReel : steer = counterSteer; reel = true                    // steer 0 in Calm and in dives
Rhythm      : if s.Tension >= EaseOnTension: easing = true
              else if easing && s.Tension < EaseOffTension: easing = false
              steer = counterSteer
              reel  = !easing && !dive                             // "let the dives run", "ease off at the creak"
input = new FightInput(steer, reel)
```
Why `CounterHold`: a literal "steer against the run for the whole run" drags a weak fish (Steady, Darter) across centre to the far side, out of the angle window and towards the pin, which no player does (they watch the line). The hold rule stops at the far side of the dead zone; the fish's own run brings it back.

### 4.5 Batches

- A batch = (bot, profile id, rod id, weightT): 200 fights with `seed_i = unchecked(2654435761u * (uint)(i + 1))`, i = 0..199 (never 0).
- Matrix: 3 bots x 5 profiles x {`rod_handmade`, `rod_twig`} x {0.5, 0.9} = 60 batches, 12,000 fights. Cache results in a static `Dictionary<string, BatchStats>` keyed `"<bot>|<profile>|<rod>|<weightT:F1>"` so tests share them within one run.
- Per batch, over the 200 fights: counts and percentages of Landed, Snapped, Escaped by reason (Pinned, ThrewHook, other), Timeout; landed fight seconds p10 / median / p90 (nearest rank on the sorted landed list: `p(q) = sorted[ceil(q * n) - 1]`; median = `p(0.5)`; "n/a" when no landing); near-snap recoveries mean and "% of fights with >= 1"; clean-catch rate = landed fights with `MaxTension < tuning.CleanCatchMaxTension`, as % of landed and % of all; median `MaxTension` of all fights; median `Elapsed` of snapped fights (`snapT`, nearest rank, "n/a" when none). A `Stopwatch` around the first full matrix computation is printed as `FG TIME` (4.7).

### 4.6 Signature-motion stats (Rhythm, `rod_handmade`, weightT 0.5, the same 200 seeds)

Collected per tick from the state before (`b`) and after (`a`) the tick, `dt = FightSim.Step`, using only observable state and the loaded profile/rod (independent of what `EffectivePull` includes):

| Stat | Definition |
|---|---|
| side-run seconds | `dt` x ticks with `b.Phase == Burst && b.BurstDir != 0 && a.Phase == Burst` |
| flips per second | ticks with `b.Phase == Burst && a.Phase == Burst && b.BurstDir != 0 && a.BurstDir == -b.BurstDir`, divided by side-run seconds; also the minimum time between two flips inside one burst (`minFlipGap`, "n/a" if fewer than 2) |
| creep (m per Calm second) | over ticks with `b.Phase == Calm && a.Phase == Calm && !sim.Exhausted` and `a.LineLength` strictly between 0 and `rod.CastMaxDist + t.SpoolExtra`: sum of `(a.LineLength - b.LineLength) + (input.Reel ? rod.ReelSpeed * dt : 0)` divided by those ticks x `dt` (Calm reel rate is exactly `rod.ReelSpeed`, spec 9.5) |
| burst force multiplier `m` | on side-run ticks where `abs(a.Lateral) < 0.999` and `abs(b.Lateral) < 0.999` and `b.FishStamina > 0.05`: `push = (a.Lateral - b.Lateral - input.Steer * rod.SteerRate * dt) / dt`; `m = abs(push) / (p.PullStrength * sim.WeightFactor * b.FishStamina * p.LateralSpeed)` (spec 9.5: P uses the stamina at the start of the tick; `m = ramp x burstLateralMult`, C 7.7) |
| time to full burst force | per side-run burst with at least 0.5 s of usable ticks: `mMax` = max `m` in that burst; first burst time (seconds since burst start) with `m >= 0.95 * mMax`; print the mean over bursts. Expected about 0.95 x `burstRamp` (Steady ~0.57 s, Runner ~0.76 s, others ~0.02 s) |
| per-burst lateral-speed spread | per side-run burst, the median `m` over its ticks with burst time >= `p.BurstRamp`; print min, max and standard deviation over bursts. Expected Thrasher about [0.7, 1.3] and sd ~0.17; others 1.00 with sd ~0 |
| dive share | dives / all bursts (expected ~ diveChance) |

### 4.7 Printed lines (exact formats; `CultureInfo.InvariantCulture`; percentages `F1`, seconds `F1`, other floats `F2`)

Every line goes through `TestContext.WriteLine` and starts with a fixed tag so the report can be grepped out of `editmode.xml`:
```
FG DATA tuning relaxWhileReeling=<F2> reelStrainBurst=<F2> counterRelief=<F2> snapGrace=<F2> cleanMax=<F2> maxFight=<F1>
FG DATA rod <id> cast=<F1> reel=<F2> strength=<F2> window=<F2> steer=<F2> relax=<F2>
FG DATA profile <id> pull=<F2> lat=<F2> run=<F2> drain=<F3> calm=<F2>-<F2> burst=<F2>-<F2> flip=<F2> dive=<F2> ramp=<F2> drift=<F2>/<F2> gap=<F2> jitter=<F2>
FG BOT <bot> <profile> <rod> w=<F1> n=200 land=<%> snap=<%> esc=<%> (pin=<%> slack=<%>) timeout=<%> len p10/med/p90=<s>/<s>/<s> snapT med=<s> rec mean=<F2> rec>=1=<%> clean=<% of landed>/<% of all> maxT med=<F2>
FG SIG <profile> sideRunSec=<F1> flips/s=<F2> minFlipGap=<F2> creep=<F3>m/s fullForce=<F2>s jitter min/max/sd=<F2>/<F2>/<F2> dives=<%> bursts=<n>
FG TARGET <test name> <PASS|FAIL|TUNING-ITEM> <measured> (target <text>)
FG PERF FightSim.Tick <F2> us/step over <n> steps
FG TIME FishingBots matrix=<F1>s
```
`<bot>` is `Greedy`, `CounterReel` or `Rhythm`. To extract them after a run:
```powershell
Select-Xml -Path "$env:FG_OUT\editmode.xml" -XPath "//test-case[contains(@fullname,'FishingBots')]/output" | ForEach-Object { $_.Node.InnerText } | Select-String '^FG '
```

### 4.8 Tests (exact names, C 10.3) and what each asserts

Each asserting test prints its `FG BOT` lines and one `FG TARGET` line per target, then asserts. The weightT each one asserts at is fixed here (C 7.15 names the batches but not the asserting weight; Q5): **0.5** for every median and snap target (a mid-size fish; the 9.8 roll's median weightT is about 0.22, so 0.5 is the harder side of typical); the Twig test uses both weights as its name says.

| Test | Batches | Assertion |
|---|---|---|
| `FishingBots_BulldogHandmade_MeetsGreedyCounterRhythmTargets` | bulldog, handmade, w 0.5, all three bots | Greedy snap >= 70%; CounterReel snap in [10%, 40%]; Rhythm snap <= 5% **and** Rhythm landed median in [35, 55] s |
| `FishingBots_SteadyRhythm_MedianFifteenToThirtySeconds` | steady, handmade, w 0.5, Rhythm | landed median in [15, 30] s |
| `FishingBots_DarterRhythm_MedianFifteenToThirtySeconds` | darter, handmade, w 0.5, Rhythm | landed median in [15, 30] s |
| `FishingBots_ThrasherRhythm_MedianThirtyFiveToFiftyFiveSeconds` | thrasher, handmade, w 0.5, Rhythm | landed median in [35, 55] s |
| `FishingBots_RunnerRhythm_MedianSixtyToOneTenSnapsAtMostFifteenPercent` | runner, handmade, w 0.5, Rhythm | landed median in [60, 110] s and snap <= 15% |
| `FishingBots_TwigRod_RhythmLandsNinetyPercentAtHalfWeightSeventyFiveAtPointNine` | all 5 profiles, twig, Rhythm, w 0.5 and 0.9 | land >= 90% at 0.5 and >= 75% at 0.9, for every profile (one `FG TARGET` per profile and weight) |
| `FishingBots_AllProfiles_PrintSignatureMotionAndDistributions` | all 60 batches + the 5 signature runs | prints `FG DATA`, all 60 `FG BOT` lines (order: profile, rod handmade then twig, w 0.5 then 0.9, Greedy, CounterReel, Rhythm) and 5 `FG SIG` lines; also prints the fun-gate proxies per profile (Rhythm handmade w 0.5: `rec>=1` for bulldog/thrasher/runner against the 40% fun-gate line, informational). Fails only if any recorded number is NaN or infinite. |
| `FishingBots_FightSimStep_UnderFiftyMicroseconds` | runner, handmade, w 0.9, Rhythm decisions, seeds as 4.5 cycled | 2,000 warm-up ticks, then a `Stopwatch` over at least 200,000 `Tick` calls (new sims created as fights end; construction included, so the figure is an upper bound). Prints `FG PERF`. Fails only above 200 us per step (C 10.3: "prints; fails only above 0.2 ms"). Acceptance 11 (< 0.05 ms) is judged from this print plus the Profiler in T30. |

**Tuning items (acceptance 8 escape hatch).** `static readonly Dictionary<string, string> TuningItems` at the top of the file, key = test name, value = one line with the measured numbers and the reason. When a target fails and its test name is in the map, the test prints `FG TARGET ... TUNING-ITEM` and calls `Assert.Ignore("FG TUNING ITEM " + value)` instead of failing, so `test-edit` stays green and the item shows as skipped (`skipped=K` in the result line). T15 starts with the map empty; an entry needs the controller's OK (Q6) and is copied into the T30 acceptance checklist. A target that passes while listed prints `PASS` and asserts normally (the stale entry is reported for removal).

### 4.9 Expected first run (author's rough estimate, not FightSim)

A rough Python model of spec 9.5 + C 7.7 (the author's guesses for phase-timer order, flips and exhaustion; T7's draw order will differ) with the 4.4 bots and the seeds, 100 fights per batch, handmade:

| Batch (w 0.5) | Model estimate | Target |
|---|---|---|
| Bulldog Greedy / CounterReel / Rhythm | snap ~100% / snap ~99% / snap 0%, median ~38 s | >= 70% / 10-40% / <= 5%, 35-55 s |
| Steady Rhythm | median ~19 s | 15-30 s |
| Darter Rhythm | median ~23 s | 15-30 s |
| Thrasher Rhythm | median ~26 s | 35-55 s |
| Runner Rhythm | median ~50 s, snap 0% | 60-110 s, <= 15% |
| Twig Rhythm, all profiles, w 0.5 and 0.9 | land ~100% | >= 90% / >= 75% |

So the likely first-run misses are **Bulldog CounterReel (snaps too often)**, **Thrasher Rhythm (too short)** and **Runner Rhythm (too short)**. Treat this as a hint for where to look, never as data: the bots on the real `FightSim` decide.

### 4.10 Retune procedure (data only)

1. Run the bots, paste the full `FG` output in the report (first-run block).
2. For each failing target, change **one** knob per iteration, inside the section 1 ranges, then rerun `test-edit -Filter "FishGame.Tests.EditMode.FishingBots"` (about 1-3 minutes per run). Keep a table in the report: iteration, change, the affected `FG BOT`/`FG TARGET` lines.
3. Knob guide (first moves from the rough model; confirm on the real sim):

| Miss | Knob (data) | Direction and model effect |
|---|---|---|
| Bulldog CounterReel snaps too often | tuning `relaxWhileReeling` (global) | 0.10 -> 0.2 took CounterReel to ~45% snap in the model and kept Greedy at 100%; also raises every profile's clean rate and lowers near-snap recoveries (fun gate wants >= 40% on bulldog/thrasher/runner: watch `rec>=1`) |
| (same) | bulldog `calmMin/calmMax` | 1.0-3.0 -> 2.0-4.0 gave ~69% snap; combine with a smaller global change |
| (same) | bulldog `pullStrength` | 0.60 -> 0.50 lowers CounterReel snaps; Greedy must stay >= 70% |
| Thrasher Rhythm too short | thrasher `staminaDrain` | 0.09 -> 0.05 gave ~35 s; ~0.045 should centre the band; `runSpeed` up lengthens further |
| Runner Rhythm too short | runner `staminaDrain` | 0.05 -> 0.035 gave ~71 s |
| Runner snaps > 15% | runner `pullStrength` down or `burstRamp` up | |
| Twig misses at 0.9 (Thrasher or Runner) | that profile's `lateralSpeed` down, or twig `steerRate` up | spec 9.12 names exactly these |
| Steady/Darter median out of 15-30 s | that profile's `runSpeed` or `staminaDrain` | |

4. Guard rails: never change `FightSim` or other code; stay inside the section 1 ranges (the validator enforces them); keep every passing target passing; keep the profiles distinct in `FG SIG` (flips/s: darter > thrasher > runner > 0 = steady = bulldog; bulldog creep > 0.03 m/s and the others ~0; steady and runner fullForce > 0.3 s; thrasher jitter sd > 0.1, others < 0.02). Motion seeds may be retuned too (skeleton risk 4) under the same rules.
5. Each kept change edits, in the same commit: the `.asset` YAML value (field names are the serialized names, e.g. `staminaDrain: 0.09` in `Content/Fishing/FightProfiles/thrasher.asset`; keep LF), the matching seed in `FishingSeedData`, and (Q2) the `TunedDeviations` entry in `FishingContentTests.cs`. Exploratory sweeps may use a local, uncommitted scratch test; only asset + seed (+ deviation) edits are committed.
6. Time box: at most 8 bot-run iterations after the first run. A target still failing then becomes a tuning item (4.8) with the controller's OK. A target that cannot move inside the ranges, or needs a code change, stops the task: report to the controller (Opus high review, escalation per handoff).
7. Final run: `build` (create-if-missing must leave the retuned assets untouched: check `git diff` on the edited `.asset` files is exactly your edits), then full `test-edit -TimeoutMinutes 15`; paste the final `FG` block and the `FG RESULT` line.

### 4.11 Steps (TDD)

1. Write `FishingBots.cs` with the loader, the fight procedure, the bots and the stats, and the eight tests. `compile` PASS.
2. `test-edit -Filter "FishGame.Tests.EditMode.FishingBots" -TimeoutMinutes 15`. Expect the perf and print tests green and some targets red (4.9). Extract and paste the `FG` lines.
3. Sanity-check the bots against spec 9.5's hand-worked checks before tuning: compare `snapT med` of Bulldog and Runner Greedy (handmade, w 0.5) with spec 9.5's ~3.8 s (carp greedy) and ~2.8 s (catfish reel-no-steer); within a factor of about 2 is fine, since the bot's runs vary, Steady Greedy should almost never snap ("bluegill effectively can't snap"). A gross mismatch means a bot bug or a `FightSim` bug: stop and report, do not tune around it.
4. Retune per 4.10.
5. Full `test-edit` PASS (bots green or tuning items skipped). `git status`: only `FishingBots.cs` (+ metas), edited `.asset` files, `FishingSeedData.cs`, `FishingContentTests.cs`.
6. Commit.

### 4.12 Acceptance (reviewer checks)

- Bot rules match 4.4 line by line (constants, read-before-tick, easing starts false, steer 0 in dives and Calm, `assist: false`, tick cap, seed formula, start line = `rod.CastMaxDist`).
- No reference to `FishingSeedData`, `UnityEngine.Random` or `System.Random` in `FishingBots.cs`; data comes from `AssetDatabase.LoadAssetAtPath` + `ToCore()`.
- Printed formats match 4.7; the report contains the first-run and final `FG` blocks and the iteration table.
- Every retuned value is identical in the `.asset`, `FishingSeedData` and `TunedDeviations`; values inside section 1 ranges; `Validation_FishingContent_Pass` green.
- Any `TuningItems` entry has controller approval noted in the report and its numbers.
- Runtime budget: in the final full `test-edit`, `FG TIME` plus the perf test stay at or under about 90 s, so later tasks' `test-edit` runs (default 5-minute timeout) keep passing. Over budget: report it (Q10), do not cut seeds on your own.
- If a target needed code: task stopped and escalated (no code change in this commit).

### 4.13 Commit

```
git add Assets/_Project/Tests/EditMode/Tuning Assets/_Project/Tests/EditMode/Tuning.meta <each edited Content/Fishing/**/*.asset> Assets/_Project/Editor/Builders/Fishing/FishingSeedData.cs Assets/_Project/Tests/EditMode/FishingContentTests.cs
git commit -m "m2(tuning): fishing tuning bots; retuned <list of id.field old->new>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
With no retune, drop the asset, seed and deviation paths and say "no retune needed".

---

## 5. Contract questions

- **Q1 (T13, C 7.13 "ComputeContentHash() includes all six categories").** `ContentHash.Compute` appends the category name even for an empty id list, so always including the five fishing categories changes the spots-only hash and breaks the M1 test `ContentRegistry_Hash_StableAcrossInstances` (C 10: M1 tests keep passing). This plan includes a fishing category in the hash only when its list is non-empty. Confirm.
- **Q2 (T14/T15 ownership).** `FishingData_SeedsMatchSpecTables` (T14) compares seeds to the spec tables, so the first value T15 retunes breaks it, and C 12 does not give T15 `FishingContentTests.cs`. Proposal: T14 creates an empty `TunedDeviations` map in that file, T15 edits only that map, and the controller amends the spec 9.6/9.12 tables after T15. Needs a C 12 row change for T15.
- **Q3 (T15 bot rule).** "Steers against side runs" taken literally (steer -burstDir for the whole run) drags weak fish to the far side: in the author's rough model CounterReel then snapped 100% on Steady and Darter, worse than Greedy. This plan adds `CounterHold = 0.1` (counter while `BurstDir * Lateral > -0.1`, equal to `CenterDeadzone`). With 0 or 0.2 the model's results were close (Steady CounterReel 100% landed either way). Confirm the rule, since it is the bot's definition.
- **Q4 (T14, C 6.2 model fill vs C 7.14 `SpeciesModels`).** If `Fish_Perch.fbx` has no vertex colours (C G6 unverified), filling `perch.model` would fail `Validation_SpeciesModels_Pass` and the build. This plan fills a null model only when the mesh has a colour channel and logs otherwise. Confirm (T29 re-export then fills it on the next build).
- **Q5 (T15, C 7.15).** The contract runs batches at weightT 0.5 and 0.9 but does not say which weight the median and Bulldog targets assert at. This plan asserts at 0.5 (the Twig test uses both). Alternatives: assert both, or pool.
- **Q6 (T15, acceptance 8).** A target T15 cannot meet would leave `test-edit` red for every later task. This plan adds a `TuningItems` map that turns a listed failing target into `Assert.Ignore` (counted as skipped) with its numbers, entries only with the controller's OK, copied into T30. Confirm the mechanism.
- **Q7 (C 7.13 `FishingTuningDef`).** Read as: every `FishingTuning` member has a camelCase field except `CreelMaxWeightG`, which is stored as `creelMaxKg` (no grams field). Confirm there is no separate `creelMaxWeightG` field.
- **Q8 (AssetPaths).** T14 declares `AssetPaths.ModelsFolder`; T20 (`FishModelPostprocessor`) and T29 must reuse it rather than declare it again (a duplicate const breaks compile).
- **Q9 (SfxCueTable growth).** The builder is create-if-missing, so a recipe added after T14 gets no `SfxCueRow` and plays with Core mixing defaults (C M2-D11: `ApplyCueTable` overrides by id only). C 6.2's allowed in-place edits do not include appending rows. Fine for M2 (T11 lands first); flag for M3+.
- **Q10 (bot runtime in every later `test-edit`).** C 11 puts the bots in the full `test-edit` that T16-T30 run with the default 5-minute timeout. 12,000 fights are fine at typical lengths, but a retune toward long or timeout-heavy fights (up to 12,000 ticks each) could push the suite past it. T15 prints `FG TIME` and checks about 90 s; if that cannot hold, the controller chooses between a longer `-TimeoutMinutes` for later tasks and fewer seeds.
