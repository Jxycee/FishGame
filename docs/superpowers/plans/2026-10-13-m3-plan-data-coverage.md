# FishGame M3 - Plan Section C: Data, content and coverage (T9, T10, T11, T12)

- **Date:** 2026-10-13 (written 2026-10-12). Branch `m3-fish-ecology`.
- **Contract (binding):** `docs/superpowers/plans/2026-10-13-m3-interfaces.md` ("C" below). Sections used: 1, 2, 5, 6.2, 7.5-7.12, 10.3, 11.1, 11.2, 11.4, 12, 13, 14. M2 contract `2026-10-11-m2-interfaces.md` ("C2").
- **Skeleton:** `docs/superpowers/plans/2026-10-13-m3-plan-skeleton.md` (section 2 rules, rows T9-T12, section 6 row C).
- **Spec (behaviour source):** `docs/superpowers/specs/2026-10-09-fishgame-design.md` 12.2 (weather table), 12.3 (data model), 12.4 (gate, algorithm), 12.5 (sub-areas), 12.6 (roster, specials, junk), 12.7 (hotspot numbers), 12.9 (validators, balance reports), 13.3 (variety), 9.3/9.4 (cast, bite wait), 9.8 (value), 10.3 (phases), 11.7 (ambience), 14.4 (quota calibration).
- **R72 end state:** `docs/superpowers/plans/2026-10-12-m2-rework-fight.md` (not built at the time of writing). T11 and T12 read its F4 bots (section F4.1) and the F8 numbers; R72 wins for every fight field (C 1.2 #1).
- **Model plan:** `2026-10-11-m2-plan-data-tuning.md` (same shape: shared notes, seed/range tables, one block per task).

Every brief below carries C 2.8 (global constraints) by reference. The ones that bite in a task are repeated in its header.

---

## Contract change requests

None of these renames a public name, path or test name. Each is needed because the contract, as written, turns a task red or breaks `build`. **Status: CR-1..CR-7 are all ACCEPTED as proposed (CCR accepted 2026-10-12) and applied in the contract (2.6, 6.2, 7.10, 11.2, 11.4, 12; rulings M3-D60 = CR-1, D61 = CR-2, D62 = CR-3, D63 = CR-4, D64 = CR-5, D65 = CR-6, D66 = CR-7). The "Default built by this plan" column is the accepted ruling; every refusal fallback below is NOT used.** CR-1 is applied by R72 F4 (FishingBots members internal), not by T11. CR-6 is a pending spec delta (the controller edits the spec after R72 F0 lands).

| # | Problem (evidence) | Request | Default built by this plan |
|---|---|---|---|
| CR-1 | **The R72 bots are private.** `Tests/EditMode/Tuning/FishingBots.cs` declares `BotKind`, `Decide`, `RunFight`, `CastFor`, `SeedFor` and the nested `FightRecord` as `private`/default-private, both today and in the R72 F4.1 code block. `Tuning/EcologyFightabilityTests.cs` (T11) and the V5 report (T12) must use "the R72 Rhythm bot" (C 1.3, 11.2, M3-D39) and must not redefine it (C 1.2 #1). | Visibility only: `BotKind`, `FightRecord` (and its fields), `SeedFor`, `CastFor`, `RunFight` become `internal` (same assembly `FishGame.Tests.EditMode`). Best: the controller amends R72 F4.1 before F4 is built. Otherwise add a C 2.6 row: "`Tests/EditMode/Tuning/FishingBots.cs`: T11 (visibility of those five members to `internal`; no behaviour change)". | **Applied by R72 F4: FishingBots members internal (CCR accepted 2026-10-12).** T11 step 0 only confirms it and stops and reports if F4 landed without it. |
| CR-2 | **Routing the species and junk seeds to the roster breaks M2 tests outside T10's manifest.** C 2.6 says `FishingSeedData.Species` returns `EcologySeedData.Species` after T10. Then: `FishingContentTests.FishingData_SeedsMatchSpecTables` asserts the species count (T3 leaves it at 1) and junk count 2; `FishingData_Ids_UniquePerCategory` asserts registry junk count == seed junk count; `ContentAssetParityTests.SeedSpecies_Models_AreFishModelMeshes_UnderTriangleBudget` asserts 5 species and a non-null model for every seed species (20 new species have no model until T23, contradicting C 2.8 #28). C 2.6 gives `FishingContentTests.cs` to T3/T13 only; `ContentAssetParityTests.cs` has no M3 owner. | Add C 2.6 rows: `Tests/EditMode/FishingContentTests.cs`: T10 (species and junk blocks of `FishingData_SeedsMatchSpecTables` become "same list as `EcologySeedData`" checks; no other edit). `Tests/EditMode/ContentAssetParityTests.cs`: T10 (the species-model test checks only species whose model file exists). Also: `FishingSeedData.Junk` returns `EcologySeedData.Junk` (7), like `Species` (C 2.6 row amended), so registry and seed counts agree and `FishingSeedData.BuildDatabase()` has every junk index the zones use. | As requested (CCR accepted 2026-10-12; T10 3.6). |
| CR-3 | **C 7.10 and C 2.6 disagree on who creates species and junk assets.** C 7.10's comment says `EcologyContentBuilder.Build` creates "species (24 new), junk (5 new)". C 2.6 says `FishingContentBuilder` builds species "from the ecology seeds", the model fill lives in `FishingContentBuilder` (T23 relies on it), and T13 edits `FishSpeciesDef.Configure` callers only in `FishingContentBuilder`. If `EcologyContentBuilder` created species after the model fill ran, the first `build` would leave the reused models empty and the second `build` would change assets. | `FishingContentBuilder` stays the only creator of species and junk assets (its loops read `FishingSeedData.Species/Junk`, now the roster). `EcologyContentBuilder` only checks that every species and junk asset its zones reference exists (throws `InvalidOperationException` naming the missing asset). C 7.10's comment is amended accordingly. | As requested (CCR accepted 2026-10-12). |
| CR-4 | **The harbour `SpotDef` breaks `build` from T10 to T17.** `Validators.ContentIds` (`Editor/Validation/Validators.cs` lines 60-75) fails every registry spot whose `sceneName` is not in the build list; `Spot_HarbourPier` joins the build list only in T17. | T10 creates `Content/Spots/harbour_pier.asset` (zone, `mapOrder` 1, scene `Spot_HarbourPier`) but appends it to `registry.spots` only when `SceneNames.BuildOrder` contains the string `"Spot_HarbourPier"` (checked at run time with `Array.IndexOf`, so T10 never references T17's constant). T17's first `build` then wires it in the Content step, before the Validate step reads the build list. No file edit in T17. | As requested (CCR accepted 2026-10-12; T10 3.4 step 8). |
| CR-5 | **V5 has no cycle-time source for four rods.** M3-D39 says "R72 F8 measured median cast-to-land per profile on the day's rod". F4/F8 measure only the fight (hook to land, landed fights), only on Handmade and Twig. The rod path needs Carbon (days 5-6) and Deep-sea (day 7). | Ruling: T12 measures the fight medians itself with the R72 Rhythm bot (CR-1 accessor, same seeds, same casts, real assets) on all four rods. For Handmade and Twig the numbers are by construction F8's (deterministic bot); the report prints them next to F8's table for a check. Cycle time = cast overhead + expected bite wait (spec 9.4 terms) + fight median + 10 s walking (T12 5.4). | As requested (CCR accepted 2026-10-12). Refusal fallback (literal F8 medians, Handmade's used for Carbon and Deep-sea) is not used. |
| CR-6 | **The 13.3 "species eligible in exactly one band" rule fails on the spec 12.6 roster.** Every roster row has 2 or more bands (rows 8, 10, 11, 20-23 have exactly 2; R1 split each old band into two). So `EcologyVariety_EverySpotSeason_FourEligibleAndOneSingleBand` fails for all 8 (spot, season) pairs, and seeds may not re-derive the roster. | Spec delta 13.3 bullet 4, **ruled wording**: "a species eligible in at most two adjacent bands (Night and Dawn are not adjacent)" (the R1 equivalent of one pre-R1 band; pending spec edit by the controller after R72 F0; M3-D65). Every (spot, season) passes this with the roster (lake: sunny_trout / glimmer_eel / frost_char; harbour: grumpy_puffer, bruiser_tuna, moon_squid, storm_ray, lanternfish). The test keeps its name and also prints the literal single-band count. | As requested (CCR accepted 2026-10-12). The refusal fallback (keep the literal rule, record the miss as an `EcologyTuningItems` entry) is not used. |
| CR-7 | **Undefined inputs for coverage, variety and balance.** C 11.2/11.4 leave open: which bait V4/V5 use; what "eligible" means in 13.3; what "differs by >= 2" means; whether specials count in V2/13.3/V5; the tie rule for "best-for-coins". The bare hook (`junkMult` 1.5) puts V4's mean junk at about (0.20 + 0.12 + 0.10) / 3 x 1.5 = 0.21, above the 20% bound. | Rulings (recorded here, spec deltas 12.9/13.3): V4 and V5 use `BaitMods.Neutral(1)` (M3-D5 neutral bait) with bite speed 1.0; 13.3 "eligible" = some non-special rule passes at rod 3, bait rating 10, depth 6 m, in any of Clear/Rain/Fog and any sub-area; "differs by >= 2" = symmetric difference of the Morning and Night sets has >= 2 species; specials are excluded from V2 shares, 13.3 counts and V5 economy (catch-limited, they do not scale with time) and included in V3 and V4; best-for-coins ties go to the lower sub-area index. | As requested (CCR accepted 2026-10-12). |

Minor additions (no ruling needed, listed for the record): `ContentRegistry.Validate` also reports an unknown sub-area id and a weather table with wrong array lengths (both are unresolved references that would make `Build` throw); `SfxCueTableDef.seasonAmbience` is written by the builder and tests through `SerializedObject` (no new public `Configure`).

## Cross-section questions for the controller

1. **Bluegill must match section A (T3) exactly.** The migration overwrites `bluegill.asset`; T10's roster row 2 must equal T3's `FishingSeedData` bluegill row field for field (200-600 g, value 8, Common, steady, tags Schooling, `biteWaitMod` - M2 had 0.9, M3-D35 says 1.0 - colours, `lengthShapeK`, model file), or `EcologyData_AssetValuesEqualSeeds` turns red (create-if-missing never rewrites bluegill). Default: T10 copies T3's committed row verbatim; the reviewer diffs the two. (Section A's plan, read at hand-back: its row is 200-600 g, value 8, Common, steady, Schooling, skew 2.2, K 1.0, `biteWaitMod` 1.0, hookWindow 0, Round, M2 colours, `Fish_Bluegill.fbx` - consistent with 1.1. Section A's T3 also sets the species count in `FishingData_SeedsMatchSpecTables` and `ContentAssetParityTests` to 1 (its CCR-A5), which is exactly what CR-2 lets T10 replace.)
2. **T3's `FishSpeciesDef.Configure` and `FishSpeciesData.Tags`** (section A): T10 assumes `tags` is the last `Configure` parameter and `FishSpeciesData.Tags` carries it, so `SpeciesSeed.Data.Tags` holds the roster tags (C 7.3).
3. **T13 removes `MinBaitRating`/`MinDepth`** (section D). `EcologySeedData` never names those two fields (they default to 0), so T13's removal compiles without touching T10's files; only `FishingContentBuilder`'s `Configure` call (T13's) and T3's bluegill row name them.
4. **`ShoreGridDef.WriteBake` signature** (section E, T16 calls it): pinned in T9 2.2.
5. **`SunMoonSettings`, `WeatherAmbient`, `SeasonAmbience` meanings** (section F, T18/T19): pinned in 1.3-1.5. Summer `SeasonAmbience` reproduces today's `AmbienceBed` exactly.
6. **Serializable helper types use public camelCase fields** (accepted 2026-10-12, contract 7.9 and M3-D76; T9 2.1, the M2 `SfxCueRow` precedent): `SubAreaBand.id/minShoreM/maxShoreM/junkChance`, `SpawnRule.fish/subAreaIds/...`, `JunkWeight.junk/weight`, `SunMoonSettings.sunriseFraction/...`, `WeatherAmbient.fogDensityMult/...`, `SeasonAmbience.windGain/...`. Sections E (T16/T17 read sub-area ids), F (T18/T19 read the looks and ambience) and G (T21 level 1 shows sub-area ids) must write these names, not PascalCase properties.

---

## 0. Shared notes for T9-T12

### 0.1 Commands

```powershell
$env:FG_OUT = "C:\Users\jayce\AppData\Local\Temp\claude\C--Dev-FishGame\<session>\scratchpad\<task>"   # absolute, outside the repo
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -TimeoutMinutes 15
```
- PowerShell tool only. Filtered runs in the foreground (`timeout: 600000`); the full `test-edit` with `run_in_background`, then wait for the notification (it can pass 10 minutes from T11 on).
- Expected last lines: `FG RESULT compile PASS ...`, `FG RESULT build PASS ...` (log has `FG BUILDALL PASS`), `FG RESULT test-edit PASS total=N passed=N failed=0 skipped=K ...`. A red run that is expected says "expected red" in the tool description.
- `LOCKED` (exit 2) means the user has the editor open: stop and report. Never kill Unity, never delete the lockfile (C 2.8 #11).
- After every `build`: `git restore Assets/_Project/Scenes/*.unity` (no task here changes a scene; T9-T12 run before T14, so no `WorldState` hash is involved yet, C 2.8 #24). Delete any `Assets/InitTestScene*.unity` and untracked `dev/null/`.
- No task in this section runs `test-play` (C 2.4: from T13 on).

### 0.2 Ordinal index spaces after T10 (tests may hard-code these)

| Category | Index 0..n-1 |
|---|---|
| `species` (25) | 0 bluegill, 1 bruiser_tuna, 2 bucketmouth_bass, 3 frost_char, 4 gawping_gurnard, 5 gilded_carp, 6 glimmer_eel, 7 grumble_mackerel, 8 grumpy_carp, 9 grumpy_puffer, 10 ice_herring, 11 lake_pike, 12 lanternfish, 13 mirror_minnow, 14 moon_squid, 15 mudcat, 16 old_barnacle, 17 pier_sardine, 18 red_snapper, 19 silver_mullet, 20 sleepy_sturgeon, 21 sneaky_flounder, 22 storm_ray, 23 sunny_trout, 24 yellow_perch |
| `junk` (7) | 0 empty_bait_tub, 1 lost_sock, 2 old_boot, 3 rope_scrap, 4 seaweed_clump, 5 soggy_newspaper, 6 tin_can |
| `zones` (2) | 0 harbour_pier, 1 lake_dock |
| `spots` | lake_dock only until T17 (CR-4); then 0 harbour_pier, 1 lake_dock |
| `fight_profiles`, `rods`, `baits` | unchanged (C2; R72 index map) |

### 0.3 Global constraints that bite here (C 2.8)

- #2 ASCII in code, comments, printed lines and docs; after a Write whose text contains `\u`, check no byte > 0x7F (`grep -nP '[^\x00-\x7F]' <files>` prints nothing).
- C2 #6 scene churn (restore after `build`); #7 LF blobs (`.asset` YAML keeps LF); #8 EditMode reach (tests touch Core, Data, Editor only); #11 editor lock; #12 no `rm -rf`; #20 explicit `git add` paths and the trailer of C 2.1; #21 logs and results in `$env:FG_OUT`.
- #22 tuning lives in assets: Defs and Core types carry no tuning field initialisers; seeds live only in `EcologySeedData` (and `FishingSeedData` for its existing tables).
- #28 fallback fish mesh: species without a model are normal; nothing in T10-T12 may require a model.
- C 1.2 #1: never redefine an R72 surface (profile/tuning fight fields, `FightSim`, `FishingTestData`, the bots' behaviour). CR-1 is a visibility change only.
- Tool discipline (R72 rule 1, restated by the rework handoff): create and edit files only with the Write and Edit tools, never with sed, python or heredocs, including `.asset` YAML.

### 0.4 Assets, not seeds

Validators, coverage tests, fightability and balance reports read the generated assets: `AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset)`, then `registry.Ecology`, `registry.Fishing`, the Defs. They never reference `EcologySeedData` or `FishingSeedData` (the reviewer greps `EcologyValidators.cs`, `EcologyCoverageTests.cs`, `Tuning/EcologyFightabilityTests.cs`, `Balance/EcologyBalanceReports.cs`). Only `EcologyContentTests` and `EcologyContentBuilderTests` compare seeds with assets. A missing asset fails with `"missing <path>: run Tools/unity.ps1 build first"`. Shore grids are unbaked (null in `ZoneData.Grid`) until T16; nothing in this section reads a grid.

### 0.5 Deviation and tuning-item maps (mirror C2 D-Q2 and D-Q6)

- **`EcologyTunedDeviations`** - `static readonly Dictionary<string, float>` at the top of `Tests/EditMode/Ecology/EcologyContentTests.cs`. T10 creates it **empty**. Key format `"<kind>.<owner>.<field>"`: `species.<id>.<field>` (e.g. `species.gilded_carp.baseValue`), `rule.<zone>.<speciesId>.<field>` (e.g. `rule.lake_dock.sunny_trout.weight`, `rule.harbour_pier.old_barnacle.specialChance`), `band.<zone>.<subAreaId>.junkChance`, `junk.<zone>.<junkId>.weight`. `EcologyData_SeedsMatchRoster` and `EcologyData_SubAreasMatchSpec125` read every spec literal through `E(key, specValue)` (returns the map value when present). T11/T12 may add entries only together with the matching `.asset` and seed edit (C 2.6 data-asset row). The controller amends the spec 12.5/12.6 tables after T12.
- **`EcologyTuningItems`** - `static readonly Dictionary<string, string>` (key = exact test name, value = one line with the measured numbers and the reason) at the top of each file whose tests may carry a bound that data cannot meet: `Ecology/EcologyCoverageTests.cs` (T11), `Tuning/EcologyFightabilityTests.cs` (T11), `Balance/EcologyBalanceReports.cs` (T12). Each file starts with an empty map. A failing target whose test name is in its file's map prints `FG TARGET <test> TUNING-ITEM <measured>` and calls `Assert.Ignore("FG TUNING ITEM " + value)`; a passing target that is listed prints `FG TUNING STALE <test>` and asserts normally. Every entry needs controller approval, quoted in the task report, and is carried to the T25 acceptance list.
- Shared target helper (copy the M2 `FishingBots.Targets` pattern privately into each of the three files; no shared test file): collects `(pass, measured, target)` per target, prints `FG TARGET <test> <PASS|FAIL|TUNING-ITEM> <measured> (target <text>)`, then asserts once.

### 0.6 Printed-line tags (all through `TestContext.WriteLine`, `CultureInfo.InvariantCulture`; percentages `F1`, seconds `F1`, other floats `F2`)

```
FG ECO V1 <zone> dead=<n> of <contexts>                       (Coverage report: one per zone)
FG ECO V2 <zone> <season> <band> <weather> species=<n> top=<id> <share%>
FG ECO V3 <speciesId> contexts=<n>
FG ECO VARIETY <zone> <season> eligible=<n> narrow=<ids> single=<n> morningNightDiff=<n>
FG FIGHT <profileId> rod_twig rhythm n=200 land=<%> snap=<%> to=<%> med=<s> species=<id,id,...>
FG V4 <zone> <season> <speciesId> <rarity> rod=<tier> perRun=<F2> bound=<F2> <PASS|FAIL>
FG V4 JUNK <zone> <season> run=<%> day1..7=<%>/<%>/<%>/<%>/<%>/<%>/<%> maxDay=<%>
FG V4 TOP <zone> <season> maxShareInAnyRun=<%> species=<id>
FG V5 MEDIAN <profileId> <rodId> med=<s> land=<%> (F8 <s> or n/a)
FG V5 <zone> <season> <subAreaId> <band> <weather> coinsPerMin=<F2> kgPerMin=<F2>
FG V5 BEST <zone> <season> <subAreaId> wins=<n>/18 (<%>)
FG QUOTA <zone> <season> day=<d> rod=<rodId> coins=<F1> kg=<F2> bestCoins=<F1>
FG QUOTA <zone> <season> plateau coins=<F1> kg=<F2> day4/day1=<F2>
FG TARGET <test> <PASS|FAIL|TUNING-ITEM> <measured> (target <text>)
FG TIME <file> <F1>s
```
Extract after a run: `Select-Xml -Path "$env:FG_OUT\editmode.xml" -XPath "//test-case/output" | ForEach-Object { $_.Node.InnerText } | Select-String '^FG '`.

### 0.7 Runtime budget (mirrors C2 D-Q10)

Every later full `test-edit` runs the Tuning and Balance categories (`Tools/unity.ps1` has no category filter). Budgets in the final full run: fightability (1,000 Twig fights) <= 15 s; balance file total (V4 about 1.34M resolves plus V5's 4,000 bot fights) <= 60 s; each file prints `FG TIME`. Over budget: report the measured `FG TIME`; the controller decides between a longer default timeout and fewer runs or seeds. Do not cut runs or seeds on your own.

---

## 1. Seed and range tables (single source for T10 seeds, T11 data rules and the T10/T11 tests)

**Roster values are spec 12.6 (rows 1-23, specials, junk lists), 12.5 (sub-areas) and 12.2 (weather), referenced, never copied here.** This section holds only the values the contract leaves to T10 and the validator ranges of this plan.

### 1.1 Species presentation and models (M3-D35, M3-D33; colours chosen here)

Common to all 24 rows other than bluegill (bluegill is T3's committed row in every field, and the test literal reads it from that row too): `weightSkew` 2.2, `biteWaitMod` 1.0, `hookWindow` 0, `lengthShapeK` 1.0 (except glimmer_eel 1.6, grumpy_puffer 0.8); `weightMinG/MaxG` = spec 12.6 kg x 1000; `isSpecial` true only for the two specials; `model` null at creation (filled by the C2 6.2 rule as amended by T4). Display name = id words in title case. Colours RGB, alpha 1. Model files: the M3-D33 reuse, otherwise `Fish_<PascalId>.fbx` (T23 produces them; until then the fallback mesh, #28).

| Row | id | displayName | bodyShape | colorA | colorB | modelFile |
|---|---|---|---|---|---|---|
| 1 | mirror_minnow | Mirror Minnow | Long | (0.80,0.84,0.88) | (0.20,0.62,0.66) | Fish_MirrorMinnow.fbx |
| 2 | bluegill | Bluegill | Round | as T3 (M2: (0.32,0.50,0.62)) | as T3 (M2: (0.95,0.62,0.22)) | Fish_Bluegill.fbx |
| 3 | yellow_perch | Yellow Perch | Round | (0.62,0.68,0.30) | (0.20,0.24,0.16) | Fish_Perch.fbx |
| 4 | grumpy_carp | Grumpy Carp | Round | (0.72,0.58,0.30) | (0.45,0.34,0.18) | Fish_Carp.fbx |
| 5 | bucketmouth_bass | Bucketmouth Bass | Round | (0.36,0.50,0.26) | (0.88,0.86,0.70) | Fish_BucketmouthBass.fbx |
| 6 | silver_mullet | Silver Mullet | Long | (0.70,0.74,0.78) | (0.30,0.36,0.44) | Fish_SilverMullet.fbx |
| 7 | lake_pike | Lake Pike | Long | (0.40,0.52,0.30) | (0.86,0.84,0.52) | Fish_LakePike.fbx |
| 8 | sunny_trout | Sunny Trout | Long | (0.55,0.62,0.55) | (0.88,0.45,0.48) | Fish_Trout.fbx |
| 9 | mudcat | Mudcat | Round | (0.36,0.34,0.30) | (0.70,0.66,0.56) | Fish_Catfish.fbx |
| 10 | frost_char | Frost Char | Long | (0.62,0.76,0.90) | (0.92,0.40,0.30) | Fish_FrostChar.fbx |
| 11 | glimmer_eel | Glimmer Eel | Eel | (0.16,0.22,0.40) | (0.40,0.95,0.85) | Fish_GlimmerEel.fbx |
| 12 | sleepy_sturgeon | Sleepy Sturgeon | Long | (0.42,0.40,0.38) | (0.78,0.72,0.60) | Fish_SleepySturgeon.fbx |
| 13 | pier_sardine | Pier Sardine | Long | (0.52,0.66,0.80) | (0.92,0.94,0.96) | Fish_PierSardine.fbx |
| 14 | ice_herring | Ice Herring | Long | (0.78,0.88,0.96) | (0.30,0.44,0.62) | Fish_IceHerring.fbx |
| 15 | grumble_mackerel | Grumble Mackerel | Long | (0.20,0.48,0.46) | (0.10,0.14,0.20) | Fish_GrumbleMackerel.fbx |
| 16 | moon_squid | Moon Squid | Squid | (0.88,0.86,0.96) | (0.58,0.40,0.80) | Fish_MoonSquid.fbx |
| 17 | gawping_gurnard | Gawping Gurnard | Round | (0.86,0.36,0.28) | (0.98,0.84,0.62) | Fish_GawpingGurnard.fbx |
| 18 | sneaky_flounder | Sneaky Flounder | Flat | (0.62,0.54,0.40) | (0.30,0.26,0.20) | Fish_SneakyFlounder.fbx |
| 19 | red_snapper | Red Snapper | Round | (0.86,0.24,0.22) | (0.98,0.70,0.66) | Fish_RedSnapper.fbx |
| 20 | grumpy_puffer | Grumpy Puffer | Round | (0.92,0.82,0.40) | (0.40,0.30,0.20) | Fish_GrumpyPuffer.fbx |
| 21 | bruiser_tuna | Bruiser Tuna | Long | (0.16,0.24,0.44) | (0.80,0.84,0.88) | Fish_BruiserTuna.fbx |
| 22 | storm_ray | Storm Ray | Flat | (0.32,0.36,0.44) | (0.84,0.88,0.94) | Fish_StormRay.fbx |
| 23 | lanternfish | Lanternfish | Long | (0.10,0.12,0.22) | (1.00,0.90,0.40) | Fish_Lanternfish.fbx |
| S1 | gilded_carp | Gilded Carp | Round | (0.95,0.76,0.25) | (0.70,0.46,0.12) | Fish_GildedCarp.fbx |
| S2 | old_barnacle | Old Barnacle | Round | (0.40,0.44,0.40) | (0.86,0.82,0.74) | Fish_OldBarnacle.fbx |

Seed list order = this table order (rows 1-23, then S1, S2); `BuildDatabase` sorts by id.

### 1.2 Junk (M3-D34 values; spec 12.6 lists; shapes and colours chosen here)

| id | displayName | valueCoins | bodyShape | color | zones (equal weight 1 per zone) |
|---|---|---|---|---|---|
| old_boot | Old Boot | 2 | Long | M2 (0.30,0.22,0.16) | lake_dock, harbour_pier |
| tin_can | Tin Can | 2 | Round | M2 (0.68,0.70,0.72) | lake_dock |
| soggy_newspaper | Soggy Newspaper | 0 | Flat | (0.82,0.80,0.70) | lake_dock |
| lost_sock | Lost Sock | 1 | Long | (0.86,0.30,0.36) | lake_dock |
| rope_scrap | Rope Scrap | 1 | Long | (0.72,0.60,0.40) | harbour_pier |
| seaweed_clump | Seaweed Clump | 0 | Round | (0.22,0.42,0.22) | harbour_pier |
| empty_bait_tub | Empty Bait Tub | 2 | Round | (0.30,0.52,0.86) | harbour_pier |

Junk colours move to `EcologySeedData` (internal `JunkColor(string id)` for all 7); `FishingSeedData.JunkColor` forwards to it (old_boot and tin_can keep their M2 values exactly).

### 1.3 `SunMoonSettings` seeds (C 7.9 gives the first four and the blend window)

`sunriseFraction` 0, `sunsetFraction` 0.70, `minElevationDeg` 12, `flipWindow` 0.02, `moonIntensity` 0.25, `moonEulerDeg` (35, 150, 0) (the lake sun is Euler(50, -30, 0); the moon comes from the opposite azimuth, lower), `moonTint` (0.62, 0.70, 0.92, 1) (the Night phase light colour), `blendWindow` 0.05 (M3-D24). T18 interprets them (`DayNightMath` signatures in C 7.8).

### 1.4 `WeatherAmbient` seeds (C 7.9; index Clear 0, Rain 1, Fog 2)

| Index | fogDensityMult | fogDensityMin | lightMult | desaturate | rainAmount |
|---|---|---|---|---|---|
| 0 Clear | 1 | 0 | 1 | 0 | 0 |
| 1 Rain | 1.2 | 0 | 0.85 | 0.1 | 1 |
| 2 Fog | 1 | 0.03 | 0.9 | 0.15 | 0 |

### 1.5 `SeasonAmbience` seeds (M3-D22; index = `Season`; meanings pinned for T19)

`windGain` multiplies the `Wind_Ambient` loop's base volume (1 = today); `windLfoHz` is the rate of a slow sine wobble on that gain (0 = steady, today; T19 picks the wobble depth); `lapIntervalMin/Max` are the `Water_Lap` interval bounds in seconds (today 0.6 / 1.4, the `AmbienceBed` constants). **Summer reproduces today's `AmbienceBed` exactly**, so a Summer world sounds like M2.

| Season | windGain | windLfoHz | lapIntervalMin | lapIntervalMax |
|---|---|---|---|---|
| Spring | 1.1 | 0.08 | 0.6 | 1.4 |
| Summer | 1.0 | 0 | 0.6 | 1.4 |
| Autumn | 1.3 | 0.12 | 0.5 | 1.2 |
| Winter | 1.5 | 0.15 | 1.0 | 2.0 |

### 1.6 Other ecology seeds (references)

- Weather table `weather_temperate`: spec 12.2 matrix row-major (rows current Clear/Rain/Fog, columns next) and the 12 season-column multipliers (`season * 3 + column`: Spring Rain 1.5, Summer Fog 0.3, Winter Fog 1.3, all others 1) - C 7.5 `WeatherTableData`.
- `EcologyTuning`: 60 / 120 / 60 / 120 / 45 / 60 minutes, radius 3 m (spec 12.7, C 7.10).
- Zones: `lake_dock` "Lake Dock" and `harbour_pier` "Harbour Pier"; sub-areas, shore distances and junk chances spec 12.5 (last band `maxShoreM` 0 = open-ended); junk lists 1.2.
- Rules: one per (roster row, zone) (row 6 gives two: lake reeds+open_water, harbour shallows+channel); sub-area ids, seasons, weather ("Any" = `WeatherMask.All`), bands, `weight` (Sw), `minRodTier` (Rod) from the row; `minBaitRating` 0, `minDepth` 0 (M3-D3, no column), `minPlayersInSession` 0 (M3-D36), `special` false, `specialChance` 0, `catchLimitPerRun` -1. Specials (spec 12.6): one rule each (gilded_carp lake `deep_water`, old_barnacle harbour `open_sea`), all masks `All`, rod 0, `special` true, `specialChance` 0.02 / 0.015, `catchLimitPerRun` 1, `weight` 0 (ignored for specials; 0 makes a wrongly cleared `special` flag fail the "non-special weight > 0" rule); species Bulldog, Legendary, `isSpecial` true, values and kg per 12.6.
- Spots: `harbour_pier` "Harbour Pier", scene `"Spot_HarbourPier"` (T10 used a literal; the cleanup round replaced it with `SceneNames.SpotHarbourPier`, same value, added early and still not in `BuildOrder`; T17 only appends the `BuildOrder` entry), `mapOrder` 1, zone harbour_pier; `lake_dock` keeps `mapOrder` 0 and gets zone lake_dock.

### 1.7 Ranges for the T11 data rules (plan ranges unless the spec gives them)

| Subject | Rule |
|---|---|
| Sub-areas | count 1..`EcologyConstants.MaxSubAreas` (8); first `minShoreM` 0; each `minShoreM` == previous `maxShoreM`; every band but the last has `maxShoreM > minShoreM`; last band `maxShoreM <= 0`; `junkChance` in [0, 0.6] (spec) |
| Rules | `seasons`, `weather`, `bands` non-zero; `weight > 0` when not special (spec); special: `specialChance` in (0, 0.5] and `fish.IsSpecial == special` (spec); non-special: `fish.IsSpecial == false`; `minRodTier` <= the highest rod tier in `rods`; `minBaitRating` >= 0; `minDepth` in [0, 6]; `catchLimitPerRun >= -1`; `minPlayersInSession` in [0, 4] |
| Species | `weightMinG < weightMaxG`, `baseValue > 0` (spec); every species with `isSpecial` or not appears in >= 1 rule of some zone |
| Junk weights | `> 0` and finite |
| Weather table | 9 base values finite and > 0; each row sum > 0; 12 season multipliers finite and > 0 |
| `castMinDist` | the tuning asset's `castMinDist` equals 2.5 (`EcologyConstants.ReachMinM` assumes it; message names the constant to re-derive) |
| Spots | `mapOrder` in [0, 4] and unique among registry spots |
| `EcologyTuningDef` | `0 <= hotspotFirstMinMin <= hotspotFirstMinMax <= 1200`; `1 <= hotspotGapMin <= hotspotGapMax <= 1200`; `1 <= hotspotDurationMin <= hotspotDurationMax <= 1200`; `hotspotRadiusM` in (0, 10] |
| `SunMoonSettings` | `0 <= sunriseFraction < sunsetFraction <= 1`; `minElevationDeg` in [0, 45]; `flipWindow` in (0, 0.1]; `moonIntensity` in [0, 1]; `blendWindow` in (0, 0.1] |
| `WeatherAmbient[]` | exactly 3; `fogDensityMult` [0.5, 2]; `fogDensityMin` [0, 0.0333] (readable at 25 m, `DayNightMath.IsReadable`); `lightMult` [0.3, 1.5]; `desaturate` [0, 1]; `rainAmount` [0, 1] |
| `SeasonAmbience[]` | exactly 4; `windGain` [0, 2]; `windLfoHz` [0, 1]; `lapIntervalMin` [0.2, 5]; `lapIntervalMax` in [`lapIntervalMin`, 10] |

Message format (InvariantCulture, `G` floats): `"<subject>: <field> = <value> outside [<min>, <max>]"` or `"<subject>: <rule text>"`, subjects `zone <id>`, `zone <id> sub-area <subId>`, `zone <id> rule <speciesId>`, `zone <id> junk <junkId>`, `species <id>`, `weather <id>`, `spot <id>`, `ecologyTuning`, `palette`, `cueTable`, `tuning`.

---

## 2. T9 - Data: ecology defs and registry

- **Goal:** the C 7.9 Defs and serializable types, the `ContentRegistry` zones partial with the Core `EcologyDatabase`, and the M3 fields on `SpotDef`, `ArtPaletteDef`, `SfxCueTableDef`.
- **Owned files (C 12):** `Data/Ecology/{FishingZoneDef,SubAreaBand,SpawnRule,JunkWeight,WeatherTableDef,ShoreGridDef,EcologyTuningDef}.cs`, `Data/ContentRegistry.Ecology.cs`, `Data/Art/{SunMoonSettings,WeatherAmbient}.cs`, `Data/Audio/SeasonAmbience.cs`; tests `Tests/EditMode/Ecology/EcologyDefsTests.cs` (new), `Tests/EditMode/ContentRegistryTests.cs` (add).
- **Shared edits (C 2.6):** `Data/ContentRegistry.cs` (`partial` + four one-line hooks), `Data/SpotDef.cs`, `Data/Art/ArtPaletteDef.cs`, `Data/Audio/SfxCueTableDef.cs`.
- **Depends on:** T7 (and T5: `ZoneData`, `ShoreGrid`, `EcologyDatabase`, masks, `WeatherTableData`, `EcologyTuning`). **Implementer:** Sonnet. **Reviewer:** Sonnet.
- **C 2.8 that bite:** #2 ASCII; #8 EditMode reach (tests use Data + Core); #22 no tuning field initialisers (empty collections allowed); Data has no Unity logging and no `UnityEditor`.

### 2.1 Def rules

- `namespace FishGame.Data`, one type per file, header `// Content data only (not networked). <purpose>. Owned by T9 (M3).`
- `ScriptableObject` Defs (`FishingZoneDef`, `WeatherTableDef`, `ShoreGridDef`, `EcologyTuningDef`): private `[SerializeField]` fields with exactly the C 7.9 names and order; one PascalCase getter per field (collections as `IReadOnlyList<T>`, null reads as empty); `Configure(...)` takes every field in order and copies collections defensively.
- `[Serializable]` helper types (`SubAreaBand`, `JunkWeight` structs; `SpawnRule` class; `SunMoonSettings`, `WeatherAmbient`, `SeasonAmbience` structs): **public camelCase fields** with the C 7.9 names and order (the M2 `SfxCueRow` / `DayPhaseAmbient` precedent), no initialisers. `SpawnRule.subAreaIds` null or empty = all sub-areas.
- Field types: masks are the Core enums (`SeasonMask`, `WeatherMask`, `TimeBandMask`); `minRodTier` byte; counts int; everything else float/bool/string/Unity types as C 7.9.

### 2.2 Per-type specifics

| Type | Behaviour beyond 2.1 |
|---|---|
| `SubAreaBand` | `public SubAreaBandData ToCore()` copies min/max/junkChance as given (the zone decides open-endedness). |
| `SpawnRule`, `JunkWeight` | No public `ToCore` (they need the registry's indices); mapped inside `FishingZoneDef.ToCore`. |
| `FishingZoneDef` | `ZoneData ToCore(ContentRegistry registry)`: (1) bands: `SubAreaBand.ToCore()` each; the **last** band with `maxShoreM <= 0` gets `float.PositiveInfinity`. (2) rules: a copy of `rules`, **stably** sorted by `fish.Id` with `StringComparer.Ordinal` (ties keep list order: sort `(rule, index)` pairs, never `List.Sort` alone, which is unstable); each maps to `SpawnRuleData` with `SpeciesIndex = registry.SpeciesIndex(fish.Id)`, `SubAreaMask` = OR of `1 << i` for each id's index `i` in `subAreas` (ordinal match; empty/null -> 0), every other field copied. An unknown sub-area id throws `ArgumentException("zone '<id>' rule '<fishId>' names unknown sub-area '<x>'")`. (3) junk: `JunkEntry { JunkIndex = registry.JunkIndex(junk.Id), Weight }`. (4) `weather.ToCore()`. (5) `shoreGrid != null ? shoreGrid.ToCore() : null`. (6) `new ZoneData(...)` (its `ArgumentException`s pass through). |
| `WeatherTableDef` | `WeatherTableData ToCore()`: new arrays copied from `baseMatrix` and `seasonColumnMult` (null -> empty arrays; lengths are checked by `Validate`). |
| `ShoreGridDef` | Fields per C 7.9 (`shoreDm` `ushort[]`, bit arrays `byte[]`, `standPoints` `Vector3[]`). `Configure(string id, int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm, byte[] castableBits, byte[] reachableBits, Vector3[] standPoints, string bakedFromScene)` copies everything, no checks (T10 creates the unbaked asset: width 0, height 0, cellSize 1, origin (-60, -60), empty arrays, no stand points, scene ""). **`public void WriteBake(int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm, byte[] castableBits, byte[] reachableBits, Vector3[] standPoints, string bakedFromScene)`** (T16's `ShoreGridBaker` only; `id` untouched): `ArgumentException` when width or height < 1, `cellSizeM <= 0`, any array null, `shoreDm.Length != width * height`, or a bit array length != `(width * height + 7) / 8`; otherwise copies every array. `ShoreGrid ToCore()`: null when `width == 0`; else `new ShoreGrid(...)` with copies (its length checks pass through). |
| `EcologyTuningDef` | `EcologyTuning ToCore()` one to one. |
| `SpotDef` (edit) | Append `[SerializeField] private int mapOrder; [SerializeField] private FishingZoneDef zone;`, getters `MapOrder`, `Zone`, and `Configure(string id, string displayName, string sceneName, int mapOrder, FishingZoneDef zone)`. The M1 3-arg `Configure` sets only the M1 fields (leaves `mapOrder` and `zone` untouched). Header line "M1 fields only; M3/M4 add more" becomes "M1 fields; M3 adds mapOrder and zone; M4 adds more". |
| `ArtPaletteDef` (edit) | Append `sunMoon` (`SunMoonSettings`) and `weatherLooks` (`WeatherAmbient[]`), getters `SunMoon` (by value) and `WeatherLooks` (`IReadOnlyList<WeatherAmbient>`, null -> empty), `ConfigureM3(in SunMoonSettings sunMoon, WeatherAmbient[] weatherLooks)` (copies the array; null stays null). The M2 `Configure` leaves both untouched. |
| `SfxCueTableDef` (edit) | Append `[SerializeField] private SeasonAmbience[] seasonAmbience;` and getter `SeasonAmbience` (`IReadOnlyList<SeasonAmbience>`, null -> empty). The M2 `Configure(rows)` leaves it untouched. No new `Configure` (builder and tests use `SerializedObject`). |

### 2.3 `ContentRegistry` (edit) and `ContentRegistry.Ecology.cs` (new)

`ContentRegistry.cs` changes, nothing else: `public sealed partial class ContentRegistry`; header line "Categories: ..." gains "zones (M3; ecology database built in Build when zones exist)"; four one-line calls:
- end of `Validate()` before `return errors;`: `ValidateEcology(errors);`
- in `Build()`, **after** `built = true;`: `BuildEcology();`
- in `ComputeContentHash()`, after the junk line: `AddEcologyCategories(categories);`
- end of `ClearCache()`: `ClearEcologyCache();`

`ContentRegistry.Ecology.cs` holds the C 7.9 members (`CategoryZones`, `zones` = `new List<FishingZoneDef>()`, `ecologyTuning`, `Zones`, `EcologyTuning`, `ConfigureEcology`, `ZoneIndex`, `ZoneAt`, `TryGetSpotByScene`, `Ecology`) and the four private hook methods.

**The recursion trap (code; the one tricky part of T9).** `FishingZoneDef.ToCore(registry)` calls `registry.SpeciesIndex`, which calls `EnsureBuilt()`. Called from inside `Build()` before `built = true`, that re-enters `Build()` forever. Therefore the hook runs after `built = true`, and a failure leaves no half-built cache:

```csharp
// ContentRegistry.Ecology.cs
private void BuildEcology()
{
    try
    {
        zonesById = BuildIndex(zones, d => d.Id, out zonesIndex);   // the existing private static helper
        ecology = null;
        if (zones.Count == 0 || fishing == null) return;
        var zoneData = new ZoneData[zonesIndex.Count];
        for (ushort i = 0; i < zoneData.Length; i++) zoneData[i] = zonesById[zonesIndex.IdAt(i)].ToCore(this);   // EnsureBuilt sees built == true
        var spotData = new SpotEcologyData[spotsIndex.Count];
        for (ushort i = 0; i < spotData.Length; i++)
        {
            SpotDef spot = spotsById[spotsIndex.IdAt(i)];
            spotData[i] = new SpotEcologyData { ZoneIndex = zonesIndex.IndexOf(spot.Zone.Id), MapOrder = (byte)spot.MapOrder };
        }
        EcologyTuning tuning = ecologyTuning.ToCore();
        ecology = new EcologyDatabase(fishing, zoneData, spotData, in tuning);
    }
    catch
    {
        ClearCache();   // built = false again; the next access rebuilds and throws the same error
        throw;
    }
}
```
(`spot.Zone` is non-null here because `Validate` reports a null zone when zones exist and `Build` throws first.) `Ecology` getter: `EnsureBuilt(); return ecology;`. `ZoneIndex`/`ZoneAt`: like the spot methods (`KeyNotFoundException`, `ArgumentOutOfRangeException` from `RegistryIndex`). `TryGetSpotByScene(sceneName, out spot)`: `EnsureBuilt()`; null -> false; otherwise the first spot in **index** order whose `SceneName` equals it ordinally. `ConfigureEcology(zones, tuning)`: null `zones` -> `ArgumentNullException`; copies the list, sets the tuning, `ClearCache()`; never touches spots, fishing or `dataVersion`. `AddEcologyCategories`: `if (zones.Count > 0) categories.Add(new ContentCategoryIds(CategoryZones, zonesIndex.SortedIds));` (M3-D45). `ClearEcologyCache`: nulls `zonesIndex`, `zonesById`, `ecology`.

**`ValidateEcology(errors)` messages (exact wording, InvariantCulture):**
- `ValidateIds("zones", zones, d => d.Id, errors)` (null / invalid id / duplicate, same wording as the other lists).
- For each non-null zone with a valid id, `Z = "zones[" + i + "] '" + id + "'"`: `Z + " has no sub-areas."`, `Z + " has no junk."`, `Z + " has no weather table."`, `Z + " weather '" + wid + "' needs 9 base and 12 season values."`; for rule j: `Z + " rules[" + j + "] has no fish."`, `Z + " rules[" + j + "] fish '" + fid + "' is not listed in species."` (reference containment in `species`, like `ValidateSpeciesFights`), `Z + " rules[" + j + "] fish '" + fid + "' names unknown sub-area '" + x + "'."`; for junk k: `Z + " junk[" + k + "] has no junk item."`, `Z + " junk[" + k + "] '" + jid + "' is not listed in junk."`.
- Only when `zones.Count > 0`: per spot i (non-null) `"spots[" + i + "] '" + id + "' has no zone."` / `"spots[" + i + "] '" + id + "' zone '" + zid + "' is not listed in zones."`; `"ecologyTuning is missing."`.
- With `zones` empty nothing is added (M1/M2 registries stay valid; every existing `ContentRegistryTests` case passes unchanged).

### 2.4 Steps (TDD, C 2.4)

1. Write `EcologyDefsTests.cs` and the four `ContentRegistryTests` additions (2.5). `compile`: expected red (types missing).
2. Write the serializable types, the four ScriptableObject Defs, the `SpotDef`/`ArtPaletteDef`/`SfxCueTableDef` edits. `compile` PASS. `test-edit -Filter "FishGame.Tests.EditMode.EcologyDefsTests"`: green.
3. Edit `ContentRegistry.cs` (partial + four hooks) and write `ContentRegistry.Ecology.cs`. `test-edit -Filter "FishGame.Tests.EditMode.ContentRegistryTests"`: green, every M1/M2 case unchanged.
4. Mutation checks (show red once, restore): replace the stable sort with `List.Sort` on a comparer that compares culture-sensitively -> `FishingZoneDef_RulesSortedByFishIdOrdinal` red; remove the `ClearCache()` in `BuildEcology`'s catch -> the non-contiguous-bands case of `ContentRegistry_Ecology_BuildsDatabase` red (the second `Ecology` access returns null instead of throwing again). Do NOT mutate the hook order to "show" the recursion: a stack overflow kills the batch test runner; the reviewer checks the order by reading.
5. `build` (expect `FG BUILDALL PASS`; the registry has no zones yet, so nothing ecological is validated). `git restore Assets/_Project/Scenes/*.unity`. If Unity re-serialized any `Content/**` asset only to add the new empty fields, restore it too (T10 commits generated output).
6. Full `test-edit -TimeoutMinutes 15` in the background. Green.
7. Commit.

### 2.5 Tests (exact names, C 10.3)

`Tests/EditMode/Ecology/EcologyDefsTests.cs` (class `EcologyDefsTests`, namespace `FishGame.Tests.EditMode`; `[TearDown]` destroys every created ScriptableObject):

| Test | Cases | Catches |
|---|---|---|
| `EcologyDefs_ToCore_MapsEveryField` | `[TestCase("zone")] ("sub_area_band") ("rule") ("junk_weight") ("weather_table") ("shore_grid") ("ecology_tuning")`. Every field gets a distinct non-zero value (1.1, 2.2, ...; masks with different bits) so a swapped pair fails. `zone`: a registry with 2 species, 2 junk, the zone, a spot and the tuning; `zone.ToCore(registry)` gives 3 bands (last `maxShoreM` 0 -> +Infinity), the rule and junk counts, the weather arrays and a null grid. `sub_area_band`: `ToCore()` fields. `rule`: through the zone: `ZoneData.Rules[0]` has every `SpawnRuleData` field incl. `SpeciesIndex` = the registry index of its fish, `SubAreaMask` 0b101 for ids {band0, band2}, 0 for an empty list, `MinPlayersInSession`. `junk_weight`: `JunkIndex` and `Weight`. `weather_table`: 9 + 12 values copied, and mutating the Def's array afterwards does not change the result. `shore_grid`: `Configure` with a 3 x 2 grid -> `ToCore()` width/height/cell/origin, `ShoreM` of a cell, `GetBit` of both bit arrays; `WriteBake` with a 4 x 1 grid replaces every field except `id`; `WriteBake` with a 5-entry `shoreDm` for 2 x 2 throws `ArgumentException`. `ecology_tuning`: all 7 fields. | a field mapped to the wrong target; a missing defensive copy; `WriteBake` touching `id` or accepting bad lengths |
| `FishingZoneDef_RulesSortedByFishIdOrdinal` | rules configured for fish ids `fisha`, `fish_a`, `fish0` (that order). `ToCore` order by `SpeciesIndex` id: `fish0`, `fish_a`, `fisha` (ordinal: '0' < '_' < 'a'; a culture-sensitive compare puts `fish_a` first). Two rules for the same fish keep list order (stable). | culture-sensitive or unstable sorting |
| `SpotDef_M3Configure_KeepsM1Fields` | 5-arg `Configure` sets all five getters; a later 3-arg `Configure` changes id/name/scene and keeps `MapOrder` and `Zone`. | the M1 overload resetting M3 fields (would wipe lake_dock's zone on every M1 `EnsureLakeDockSpot`) |
| `ArtPaletteDef_M3Fields_RoundTrip` | `ConfigureM3(in s, looks[3])`: every `SunMoonSettings` field and every `WeatherAmbient` field read back; mutating `looks` afterwards changes nothing; the M2 `Configure` afterwards leaves `SunMoon` and `WeatherLooks` intact; a fresh Def's `WeatherLooks` is empty, not null. Also `SfxCueTableDef`: four `SeasonAmbience` written through `SerializedObject` read back through the getter; `Configure(rows)` afterwards keeps them. | M3 fields not serialized, shared arrays, M2 configure clobbering M3 fields |
| `ShoreGridDef_Unbaked_ToCoreIsNull` | Configured with width 0 -> `ToCore()` null; a zone with that grid gives `ZoneData.Grid == null`. | an unbaked grid turning into a 0 x 0 `ShoreGrid` (would make every cast "not water" in T15) |

`Tests/EditMode/ContentRegistryTests.cs` (add; helper `MakeEcologySet()` builds on the existing `MakeFishingSet()`: 2 zones `zone_b`, `zone_a` (scrambled), each with 2 sub-areas, 1 rule, 1 junk entry, a weather table; the tuning; 2 spots pointing at them):

| Test | Cases | Catches |
|---|---|---|
| `ContentRegistry_Zones_IndexedOrdinalAndHashedWhenNonEmpty` | `ZoneIndex("zone_a") == 0`, `ZoneAt(1).Id == "zone_b"`; unknown id -> `KeyNotFoundException`; past the end -> `ArgumentOutOfRangeException`. Hash: a fishing-only registry hashes as before (M2 expectation); adding zones changes it; the same zones in another list order give the same hash; the hash equals `ContentHash.Compute` over the expected category list ending with `zones`. | zones hashed when empty (would change every M2 hash); list-order hashing |
| `ContentRegistry_Ecology_BuildsDatabase` | full set: `Ecology` non-null; `Zones.Count` 2 in zone index order; `Ecology.Fishing` is `Fishing`; `Spots[i].ZoneIndex` and `MapOrder` per spot; `Tuning` equals the Def's `ToCore()`; a fishing-only registry has `Ecology == null`; after `ConfigureEcology` with new zones the next `Ecology` access reflects them (cache cleared); a zone with non-contiguous bands (passes `Validate`, fails in `ZoneData`) makes `Ecology` throw `ArgumentException`, and a second access throws again (no half-built cache). | recursion (stack overflow), stale or half-built cache, spot/zone index mix-up |
| `ContentRegistry_TryGetSpotByScene_Resolves` | known scene -> true and the spot; unknown and null -> false; works on a registry without zones. | culture compare, null crash |
| `ContentRegistry_RuleWithForeignSpecies_FailsValidate` | a rule whose fish is a species not in `species` -> exactly `zones[0] 'zone_a' rules[0] fish '<id>' is not listed in species.`; `Build()` throws `InvalidOperationException`. Also exact messages for: a null rule fish, an unknown sub-area id, a junk entry not in `junk`, a spot with no zone, a missing `ecologyTuning`, a weather table with 8 base values. | weak validation letting `Build` throw an index exception at app start |

### 2.6 Acceptance (reviewer checks)

- Field names and order match C 7.9 exactly; getters PascalCase; no tuning initialisers; Data has no logging and no `UnityEditor`.
- `ContentRegistry.cs` diff = `partial`, the header line and four call lines; `BuildEcology()` sits after `built = true` and clears the cache on failure.
- Rules sort is ordinal and stable; `WriteBake` matches 2.2 exactly (section E depends on it).
- Every pre-existing `ContentRegistryTests` case unchanged and green; full `test-edit` PASS line pasted.

### 2.7 Commit

```
git add Assets/_Project/Data/Ecology Assets/_Project/Data/Ecology.meta Assets/_Project/Data/ContentRegistry.cs Assets/_Project/Data/ContentRegistry.Ecology.cs Assets/_Project/Data/ContentRegistry.Ecology.cs.meta Assets/_Project/Data/SpotDef.cs Assets/_Project/Data/Art/ArtPaletteDef.cs Assets/_Project/Data/Art/SunMoonSettings.cs Assets/_Project/Data/Art/SunMoonSettings.cs.meta Assets/_Project/Data/Art/WeatherAmbient.cs Assets/_Project/Data/Art/WeatherAmbient.cs.meta Assets/_Project/Data/Audio/SfxCueTableDef.cs Assets/_Project/Data/Audio/SeasonAmbience.cs Assets/_Project/Data/Audio/SeasonAmbience.cs.meta Assets/_Project/Tests/EditMode/Ecology Assets/_Project/Tests/EditMode/Ecology.meta Assets/_Project/Tests/EditMode/ContentRegistryTests.cs
git commit -m "m3(data): ecology defs, registry zones partial and ecology database, M3 fields on SpotDef, ArtPaletteDef and SfxCueTableDef" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(`git status` first; if the `Tests/EditMode/Ecology` folder was already created by T5-T8, add only this task's files in it.)

---

## 3. T10 - Content: seeds and content builder

- **Goal:** one seed source for the M3 ecology (`EcologySeedData`), a create-if-missing `EcologyContentBuilder`, the roster routed through `FishingSeedData` so `FishingContentBuilder` creates every species and junk asset (CR-3), and the generated assets committed.
- **Owned files (C 12):** `Editor/Builders/Ecology/{EcologySeedData,EcologySeedData.Roster,EcologyContentBuilder}.cs`; tests `Tests/EditMode/Ecology/EcologyContentTests.cs`, `Tests/EditMode/Ecology/EcologyContentBuilderTests.cs`; generated `Content/Fishing/{Species,Junk}/*`, `Content/Ecology/**`, `Content/Spots/harbour_pier.asset`, updated `Content/ContentRegistry.asset`, `Content/Spots/lake_dock.asset` (zone fill), `Content/Art/ArtPalette.asset` and `Content/Audio/SfxCueTable.asset` (M3 field fill).
- **Shared edits (C 2.6, plus CR-2):** `FishingSeedData.cs` (species and junk forwarding, junk colours), `FishingContentBuilder.cs` (no logic change needed beyond what the forwarding gives; see 3.3), `ContentBuilder.cs` (one hook), `AssetPaths.cs` (constants block), `Core/Content/ContentIds.cs` (`HarbourPier`); CR-2: `FishingContentTests.cs`, `ContentAssetParityTests.cs`.
- **Depends on:** T9 (and T3: tags, `Legendary`, bluegill row, bait M3 fields). **Implementer:** Sonnet (the seed tables are long but mechanical; not Haiku: the forwarding and builder order need judgment). **Reviewer:** Sonnet.
- **C 2.8 that bite:** #2 ASCII; C2 #6 scene churn; #7 LF; #11 lock; #20 explicit paths; #22 seeds only here; #28 (20 species without models must build, validate and test green); C 6.2 allowed in-place edits only (a null `SpotDef.zone`, a missing registry list entry, a null singleton, default palette/cue M3 fields).

### 3.1 `AssetPaths` block (append, constants only)

```csharp
// T10 additions (M3 ecology data; C 6.2). T16/T17 reuse ShoreGridsFolder; do not redeclare it.
public const string HarbourPierSpotAsset = "Assets/_Project/Content/Spots/harbour_pier.asset";
public const string EcologyFolder = "Assets/_Project/Content/Ecology";
public const string ZonesFolder = EcologyFolder + "/Zones";
public const string WeatherFolder = EcologyFolder + "/Weather";
public const string WeatherTableAsset = WeatherFolder + "/weather_temperate.asset";
public const string ShoreGridsFolder = EcologyFolder + "/ShoreGrids";
public const string EcologyTuningAsset = EcologyFolder + "/EcologyTuning.asset";
```
Zone asset `<ZonesFolder>/<zoneId>.asset`; grid asset `<ShoreGridsFolder>/<spotId>_grid.asset` (grid id `<spotId>_grid`). `ContentIds.HarbourPier = "harbour_pier"` next to `LakeDock`.

### 3.2 `EcologySeedData` (public surface exactly C 7.10)

- `public static partial class EcologySeedData` in `namespace FishGame.EditorTools`; header `// The single M3 ecology seed source (C 2.8 #22): spec 12.2, 12.5, 12.6, 12.7 and plan data-coverage section 1. Used only to create missing assets and by tests. Owned by T10; T11/T12 update retuned values.` `EcologySeedData.Roster.cs` holds the 25 species and the rule rows; `EcologySeedData.cs` the rest.
- `ZoneSeed` and `RuleSeed` (top-level `readonly struct`s, same file as the main class, constructors taking every field):
  - `ZoneSeed`: `Id`, `Name`, `Bands` (`SubAreaBandData[]`, last `MaxShoreM` = 0 = open-ended, exactly as the asset stores it), `BandIds` (`string[]`), `Junk` (`(string junkId, float weight)[]`), `Rules` (`RuleSeed[]`, roster row order).
  - `RuleSeed`: `SpeciesId` (string), `SubAreaIds` (string[]), `Seasons` (SeasonMask), `Weather` (WeatherMask), `Bands` (TimeBandMask), `Weight` (float), `MinRodTier` (byte), `Special` (bool), `SpecialChance` (float), `CatchLimitPerRun` (int). (`minBaitRating`, `minDepth`, `minPlayersInSession` are 0 for every M3 rule and are written as 0 by the builder.)
- Properties (built once in the static constructors, exposed as `ReadOnlyCollection`): `Species` (1.1 order), `Junk` (1.2 order), `Zones` (lake_dock, harbour_pier), `Weather`, `Tuning`, `SunMoon`, `WeatherLooks` (a new array per call), `SeasonAmbience` (a new array per call). `internal static Color JunkColor(string id)` (all 7; unknown id -> `ArgumentException`).
- Each species seed: `new SpeciesSeed(id, name, profileId, data, shape, colorA, colorB, modelFile)` with `data` = `FishSpeciesData { FightProfileIndex, WeightMinG, WeightMaxG, BaseValue, WeightSkew, LengthShapeK, BiteWaitMod, HookWindow, IsSpecial, Rarity, Tags }`. **Never name `MinBaitRating` or `MinDepth`** (they stay 0; T13 removes them without touching this file). `FightProfileIndex` = the ordinal-sorted index of the profile id among `FishingSeedData.Profiles` ids; unknown id -> `InvalidOperationException`. Roster values are spec 12.6 by row: each species line carries a `// 12.6 row N` comment and the values verbatim (kg x 1000 to grams).
- Bluegill (row 2): copy T3's committed `FishingSeedData` bluegill row exactly (cross-section question 1).
- `BuildDatabase()`: `fishing = FishingSeedData.BuildDatabase()` (now 25 species, 7 junk); zones sorted by id; for each zone: bands (last `MaxShoreM` 0 -> +Infinity, as `FishingZoneDef.ToCore`), rules stably sorted by species id ordinal and mapped with the sorted species index and the sub-area mask, junk by sorted junk index, the weather table, grid null; spots: `harbour_pier` (zone 0, mapOrder 1), `lake_dock` (zone 1, mapOrder 0) in id order; `new EcologyDatabase(fishing, zones, spots, Tuning)`. It must give the same `ZoneData` as `registry.Ecology` from the assets (tested in 3.7).
- No `UnityEngine.Random`; floats with an `f` suffix.

**Static-initialisation order (the tricky part of T10).** `EcologySeedData`'s static constructor reads `FishingSeedData.Profiles` (profile indices). So `FishingSeedData`'s static constructor must never touch `EcologySeedData`, otherwise the two constructors cycle and one sees nulls:

```csharp
// FishingSeedData.cs (T10 edit): the species and junk lists are no longer built here (CR-2/CR-3).
public static IReadOnlyList<SpeciesSeed> Species { get { return EcologySeedData.Species; } }    // read lazily, never from the cctor
public static IReadOnlyList<(string id, string name, int value, BodyShape shape)> Junk { get { return EcologySeedData.Junk; } }
internal static Color JunkColor(string id) { return EcologySeedData.JunkColor(id); }
// BuildDatabase(): iterate Species / Junk (the getters), not removed static fields.
```
Delete `s_species`, `s_junk`, `MakeSpecies` and the T3 bluegill row from `FishingSeedData` (the row now lives in the roster); keep every other table. The header comment gains "Species and junk forward to EcologySeedData (M3 T10)."

### 3.3 `FishingContentBuilder` (edit)

With the forwarding, its existing loops create every missing species (24) and junk (5) asset and fill the reused models in the same pass, so the first `build` is complete (CR-3). Required edits: none in logic; update the header comment ("species and junk come from the M3 roster through FishingSeedData"). Check its species `Configure` call passes `x.Tags` (T3) and the junk call uses `FishingSeedData.JunkColor` (now forwarded). If T3 left the call without tags, that is a T3 defect: report it, do not patch around it.

### 3.4 `EcologyContentBuilder` (create-if-missing)

`public static void Build()` loads the registry (missing -> `InvalidOperationException`, as `FishingContentBuilder.Build`) and calls `BuildAt("Assets/_Project/Content", registry)`. `internal static void BuildAt(string contentRoot, ContentRegistry registry)` re-roots every `AssetPaths` constant with `FishingContentBuilder.RootedPath(contentRoot, path)` (internal, same assembly) and writes nothing outside `contentRoot` and the registry. Order:
1. `BuilderUtil.EnsureFolder` for `ZonesFolder`, `WeatherFolder`, `ShoreGridsFolder`, `EcologyFolder`, and the Spots folder.
2. Check every species and junk asset the zone seeds name exists at `SpeciesFolder/<id>.asset` / `JunkFolder/<id>.asset` (`FishingContentBuilder` ran first); missing -> `InvalidOperationException("ecology: missing species asset <path> (FishingContentBuilder runs first)")`.
3. `Ensure<WeatherTableDef>(WeatherTableAsset)` (configure from `EcologySeedData.Weather`, id `weather_temperate`).
4. `Ensure<ShoreGridDef>` per zone: unbaked (T9 2.2 values), id `<zone>_grid`. An existing grid is never touched (T16/T17 bake in place, GUID stable).
5. `Ensure<FishingZoneDef>` per zone seed: `SubAreaBand`s from `Bands`/`BandIds`, `SpawnRule`s (fish asset, sub-area ids, masks, weight, `minRodTier`, `minBaitRating` 0, `minDepth` 0, special, chance, limit, `minPlayersInSession` 0), `JunkWeight`s, the weather asset, the grid asset.
6. `Ensure<EcologyTuningDef>(EcologyTuningAsset)` from `EcologySeedData.Tuning`.
7. Spots: lake_dock (`LakeDockSpotAsset`, created by `ContentBuilder.EnsureLakeDockSpot`): when its serialized `zone` is null, set it to the lake_dock zone through `SerializedObject` (`FindProperty("zone")`; `mapOrder` stays 0) and log `FG CONTENT ecology: filled lake_dock zone`. A non-null zone is never changed. Harbour: `Ensure<SpotDef>(HarbourPierSpotAsset)` with the 5-arg `Configure("harbour_pier", "Harbour Pier", "Spot_HarbourPier", 1, harbourZone)`.
8. Registry through `SerializedObject`: append missing `zones` entries (both zones), fill `ecologyTuning` when null. **CR-4:** append the harbour spot to `spots` only when `Array.IndexOf(SceneNames.BuildOrder, "Spot_HarbourPier") >= 0`; otherwise log once `FG CONTENT ecology: harbour_pier spot not registered until Spot_HarbourPier is in the build order (T17)`. Throw `InvalidOperationException` if a property is not found.
9. M3 fields on existing singletons (allowed in-place fill, logged `FG CONTENT ecology: filled <asset> <field>`): `ArtPalette.asset` when `WeatherLooks.Count == 0` -> `ConfigureM3(EcologySeedData.SunMoon, EcologySeedData.WeatherLooks)` + `SetDirty` (the two fields arrive together, so an empty `weatherLooks` means both are still default); `SfxCueTable.asset` when its serialized `seasonAmbience` array size is 0 -> write the four `SeasonAmbience` entries through `SerializedObject`. Values already present are never overwritten. In `BuildAt` the singletons are the registry's `artPalette`/`sfxCueTable` references; a null reference is skipped (temp tests without them).
10. No `SaveAssets` (`ContentBuilder.Build` saves after the hooks).

`Ensure<T>` = the `FishingContentBuilder` pattern (create and configure only when missing; a file at the path that does not load as `T` throws `InvalidOperationException("asset exists but is not a <T>: <path>")`). Copy it privately or make the existing one `internal` (same file owner rules: a visibility change in `FishingContentBuilder.cs` is within T10's shared edit).

**`ContentBuilder.Build()` hook:** `EcologyContentBuilder.Build();` on the line right after `FishingContentBuilder.Build();`.

### 3.5 Steps (TDD)

1. Write `EcologyContentTests.cs` and `EcologyContentBuilderTests.cs` (3.7). `compile`: expected red.
2. `AssetPaths` block, `ContentIds.HarbourPier`, `EcologySeedData` (+ Roster), the `FishingSeedData` forwarding. `test-edit -Filter "FishGame.Tests.EditMode.EcologyContentTests.EcologyData_SeedsMatchRoster"` green (seed-only).
3. `EcologyContentBuilder` and the hook; the CR-2 test edits (3.6). Check the lock; `build`. Expect `FG BUILDALL PASS` (validators: M2 `FishingContent` sees 25 species / 7 junk, all within range; no `EcologyValidators` yet).
4. `git restore Assets/_Project/Scenes/*.unity`. `git status --porcelain` shows only: new `Content/Fishing/Species/*` (24) and `Junk/*` (5) with metas, new `Content/Ecology/**` (2 zones, 1 weather, 2 grids, 1 tuning, folder metas), new `Content/Spots/harbour_pier.asset`, modified `ContentRegistry.asset`, `lake_dock.asset`, `ArtPalette.asset`, `SfxCueTable.asset`, the owned code and test files. Run `build` again, restore scenes, `git status` unchanged (idempotent). Paste both `git status` outputs.
5. Open `Content/Ecology/Zones/lake_dock.asset` and `Content/Fishing/Species/glimmer_eel.asset` as text; paste the band, rule-count and eel values in the report.
6. Full `test-edit -TimeoutMinutes 15` in the background. Green, including `FishingContentTests`, `ContentAssetParityTests`, `FishingContentBuilderTests`, `Validation_FishingContent_Pass`, `Validation_SpeciesModels_Pass`, `Validation_ContentIds_Pass`.
7. Commit.

### 3.6 CR-2 test edits (only these)

- `FishingContentTests.FishingData_SeedsMatchSpecTables`: replace the species block (count + rows) with `Assert.That(FishingSeedData.Species, Is.SameAs(EcologySeedData.Species), "species seeds are the M3 roster (T10)")` and the junk block with the same for `Junk`; keep the `sortedProfiles` FightProfileIndex loop. Roster values are checked by `EcologyData_SeedsMatchRoster`.
- `ContentAssetParityTests.SeedSpecies_Models_AreFishModelMeshes_UnderTriangleBudget`: drop the `Is.EqualTo(5)` line; iterate seeds whose `ModelsFolder/<ModelFile>` exists (`AssetDatabase.LoadAllAssetsAtPath` finds a mesh); assert at least 5 such (the M3-D33 reuse) and run the existing checks on each; species without a model file are skipped (C 2.8 #28). Header comment: "the seed species that have a model file (M3: the five reused models plus whatever T23 has delivered)". If T4 changed this test's rules, keep T4's rules and change only the species selection.

### 3.7 Tests (exact names, C 10.3)

`Tests/EditMode/Ecology/EcologyContentTests.cs` (class `EcologyContentTests`; "assets" = loaded through the registry asset; "seeds" = `EcologySeedData`; `EcologyTunedDeviations` at the top, empty, 0.5):

| Test | Cases | Catches |
|---|---|---|
| `EcologyData_SeedsMatchRoster` | seeds vs a test-local literal copy of spec 12.6 rows 1-23 (id, zone and sub-area ids, seasons, weather, bands, g min-max, baseValue, Sw, rarity, rod, profile, tags), the two specials (zone, sub-area, chance 0.02 / 0.015, g, value 200 / 250, Legendary, Bulldog, limit 1, all masks, rod 0, `isSpecial`), 1.1 presentation (shape, colours, model file, M3-D35 constants; the bluegill literal is T3's committed row, so a deliberate T3 value such as `biteWaitMod` cannot turn this test red), rule constants (rating 0, depth 0, players 0, limit -1 for non-specials). Exactly one rule per (row, zone); silver_mullet has two. Exact float equality; spec values through `E(key, spec)`. The reviewer diffs the literal table against spec 12.6 line by line. | a transcription error; a re-derived number; a missing second-zone rule |
| `EcologyData_SevenJunkOldBootShared` | 7 junk seeds with the 1.2 values; lake zone seed junk = old_boot, tin_can, soggy_newspaper, lost_sock; harbour = old_boot, rope_scrap, seaweed_clump, empty_bait_tub; weights equal within a zone; in the assets both zones reference **the same** `old_boot` JunkDef object. | a duplicated boot asset; unequal weights |
| `EcologyData_AssetsExistForEverySeed` | every species and junk seed has its asset (Id equals) listed in the registry; both zones, the weather table, both grids, the tuning exist at their paths; registry `zones` holds exactly the two zone assets and `ecologyTuning` the tuning asset; lake_dock spot: zone = lake_dock zone, mapOrder 0; harbour spot asset: id, name, scene `Spot_HarbourPier`, mapOrder 1, zone harbour_pier; harbour spot in `registry.Spots` iff `SceneNames.BuildOrder` contains `Spot_HarbourPier` (CR-4); each zone's `ShoreGrid` is its grid asset. | an orphan asset; wiring the harbour spot too early (breaks `build`) |
| `EcologyData_ModelFiles_ReuseFourM2Models` | seed `ModelFile`: yellow_perch Fish_Perch.fbx, grumpy_carp Fish_Carp.fbx, sunny_trout Fish_Trout.fbx, mudcat Fish_Catfish.fbx, bluegill Fish_Bluegill.fbx; the other 20 are `Fish_<PascalId>.fbx` and all 25 are distinct; for each of the five reused files that exists and passes `FishingContentBuilder.IsFishModelMesh`, the species asset's `Model` is that mesh. | wrong reuse mapping; models not filled on the first build |
| `EcologyData_AssetValuesEqualSeeds` | every species asset field, junk asset field, zone (bands, rules in asset list order vs seed order, junk weights), weather table, tuning, harbour spot, palette `SunMoon`/`WeatherLooks` and cue-table `SeasonAmbience` equal the seeds exactly. Also `EcologySeedData.BuildDatabase()` zones equal `registry.Ecology` zones field by field (grids aside). | a hand-edited YAML typo; a retune applied to only one of asset and seed |
| `EcologyData_SubAreasMatchSpec125` | zone assets: sub-area ids, `minShoreM`, `maxShoreM` (last 0), `junkChance` equal spec 12.5 (through `E`); contiguous from 0. | a band edge typo that shifts the reeds/open boundary |

`Tests/EditMode/Ecology/EcologyContentBuilderTests.cs` (class `EcologyContentBuilderTests`; SAFETY block as `FishingContentBuilderTests`: works only under `Assets/_Project/Tests/__Temp/EcologyContentBuilderTests`, gitignored, deleted in `OneTimeTearDown` and on a failed setup; setup creates a temp registry asset, a temp lake_dock `SpotDef` (zone null), runs `FishingContentBuilder.BuildAt(root, registry)` then `EcologyContentBuilder.BuildAt(root, registry)`):

| Test | Cases | Catches |
|---|---|---|
| `EcologyContentBuilder_BuildAt_CreatesMissingOnly` | after setup: 2 zones, 1 weather table, 2 unbaked grids (width 0), 1 tuning, the harbour spot exist; registry `zones` count 2, `ecologyTuning` set. Change a zone's band `junkChance`, a weather value and a grid's `bakedFromScene` on the assets, run `BuildAt` again: all three unchanged, registry `zones` still 2 (no duplicate append), harbour spot not appended twice. Deleting one zone asset and re-running recreates only it. | an overwrite of tuned or baked data; duplicate list entries |
| `EcologyContentBuilder_FillsNullSpotZoneOnly` | the temp lake_dock spot's zone was null -> now the lake zone, `mapOrder` still 0; set it to the harbour zone, re-run: stays harbour; a temp palette whose `WeatherLooks` already has 3 entries keeps them on re-run, an empty one is filled. | filling a non-null zone; overwriting tuned looks |
| `EcologyContentBuilder_WrongTypeAsset_Throws` | a separate temp root where `Zones/lake_dock.asset` is a `JunkDef`: `BuildAt` throws `InvalidOperationException` naming the path, and the JunkDef file is unchanged; a root without the species assets throws the "FishingContentBuilder runs first" message. | silently replacing a foreign asset; building zones with null fish |

### 3.8 Acceptance (reviewer checks)

- Seeds: every species line has its `// 12.6 row N` comment and matches the spec row; 1.1-1.5 values exact; bluegill identical to T3's row; `EcologySeedData` never names `MinBaitRating`/`MinDepth`.
- `FishingSeedData` forwarding is lazy (getter), its cctor does not reference `EcologySeedData`; `BuildDatabase` uses the getters.
- Builders never reconfigure an existing asset (every `Configure` inside the "missing" branch; the only in-place writes are 3.4 steps 7-9); the second `build` leaves `git status` unchanged; harbour spot wiring gated as CR-4.
- Generated counts: 24 + 5 new data assets, 2 zones, 1 weather, 2 grids, 1 tuning, 1 spot (+ metas); hooks one line each; `AssetPaths` constants only.
- CR-2 test edits limited to 3.6; `build` and full `test-edit` PASS lines pasted.

### 3.9 Commit

```
git add Assets/_Project/Editor/Builders/Ecology Assets/_Project/Editor/Builders/Ecology.meta Assets/_Project/Editor/Builders/Fishing/FishingSeedData.cs Assets/_Project/Editor/Builders/Fishing/FishingContentBuilder.cs Assets/_Project/Editor/Builders/ContentBuilder.cs Assets/_Project/Editor/Builders/AssetPaths.cs Assets/_Project/Core/Content/ContentIds.cs Assets/_Project/Tests/EditMode/Ecology/EcologyContentTests.cs Assets/_Project/Tests/EditMode/Ecology/EcologyContentTests.cs.meta Assets/_Project/Tests/EditMode/Ecology/EcologyContentBuilderTests.cs Assets/_Project/Tests/EditMode/Ecology/EcologyContentBuilderTests.cs.meta Assets/_Project/Tests/EditMode/FishingContentTests.cs Assets/_Project/Tests/EditMode/ContentAssetParityTests.cs Assets/_Project/Content/Fishing/Species Assets/_Project/Content/Fishing/Junk Assets/_Project/Content/Ecology Assets/_Project/Content/Ecology.meta Assets/_Project/Content/Spots Assets/_Project/Content/ContentRegistry.asset Assets/_Project/Content/Art/ArtPalette.asset Assets/_Project/Content/Audio/SfxCueTable.asset
git commit -m "m3(content): ecology seed data (spec 12.6 roster, 7 junk, zones, weather, tuning, looks, season ambience), EcologyContentBuilder, generated assets" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(If `Editor/Builders/Ecology` already holds T3's `EcologyMigration.cs`, add only T10's files in it.)

---

## 4. T11 - Coverage

- **Goal:** the 12.9 data rules, V1-V3, the 13.3 variety checks (M3 part) and fightability on the R72 Rhythm bot, wired into `build` validation.
- **Owned files (C 12):** `Editor/Validation/EcologyValidators.cs`; tests `Tests/EditMode/Ecology/EcologyValidatorTests.cs`, `Tests/EditMode/Ecology/EcologyCoverageTests.cs`, `Tests/EditMode/Tuning/EcologyFightabilityTests.cs`.
- **Shared edits (C 2.6):** `Editor/Validation/ValidationRunner.cs` (one hook); data assets + seeds + `EcologyTunedDeviations` only if a coverage miss is fixed in data; no edit to `Tuning/FishingBots.cs` (CR-1 is applied by R72 F4; step 0 only confirms it).
- **Depends on:** T10; T6 (`FishingResolver`); R72 F4 committed. **Implementer:** Sonnet. **Reviewer:** Sonnet.
- **C 2.8 that bite:** #22; 0.4 assets not seeds; C 1.2 #1 (no R72 behaviour change, no fight retune: M3-D38); #21 results in `$env:FG_OUT`; resolver contexts always carry `EventMods.Neutral` and `SkillMods.Neutral` (the resolver throws on a default struct, M3-D46).

### 4.1 Step 0: gate

Before writing code, open `Tests/EditMode/Tuning/FishingBots.cs` and confirm it matches R72 F4.1: `enum BotKind { Greedy, Reckless, Pump, Rhythm }`, `RunFight(BotKind kind, in FishFightProfile p, in RodStats rod, in FishingTuning t, int i)` returning a `FightRecord` with `Outcome`, `Reason`, `Elapsed`. If it differs, stop and report (the fightability brief rests on F4.1). The five members (`BotKind`, `FightRecord` and its fields, `SeedFor`, `CastFor`, `RunFight`) must already be `internal`: R72 F4 applies that (CR-1, CCR accepted 2026-10-12; M3-D60). Confirm it by compiling a call from `EcologyFightabilityTests`. If F4 landed without it, stop and report; do not edit `FishingBots.cs`.

### 4.2 `EcologyValidators` (public surface exactly C 7.11)

- `All()`: loads the registry asset (missing -> one error). `DataRules(registry)`; if that reported nothing and `registry.Ecology != null`: `Coverage(registry.Ecology, neutralRating, registry.Fishing.Tuning.MinLandDepth, names)` and `Variety(registry.Ecology, names)` where `neutralRating = registry.Fishing.Baits[registry.Fishing.UnlimitedBaitIndex].EffRating(1)` (the bare hook, 1) and `names` are the zone and sub-area ids for messages; with zones empty: `"no ecology database (registry has no zones)"`. Every message prefixed `"Ecology: "`.
- `internal static List<string> DataRules(ContentRegistry registry)`: if `registry.Validate()` is non-empty, return exactly one message `"registry invalid (see ContentIds); ecology data rules skipped"` (no duplicate reporting; the M2 rule). Otherwise every rule of 1.7 over the Defs (zones, rules, species, junk weights, weather tables, the tuning asset's `castMinDist`, spots, `EcologyTuningDef`, the palette's `SunMoon`/`WeatherLooks`, the cue table's `SeasonAmbience`), messages per 1.7.
- `internal static List<string> Coverage(EcologyDatabase db, int neutralBaitRating, float neutralDepth)` (contract signature; messages use indices) plus an internal overload with `(string[] zoneIds, string[][] subAreaIds)` for readable messages (`All()` and the reports use it). One `FishingResolver` per call; contexts built by a private `Ctx(zone, sub, minute, season, weather, rod, rating, depth)` with `InHotspot` false, `Luck` 0, `PlayersInSession` 1, neutral events and skills, `Bait = BaitMods.Neutral(rating)`.
  - **V1:** for every zone x season (4) x band (6; `Minute = DayClock.BandStart[b]`) x weather {Clear, Rain, Fog} x sub-area: `CollectEligible(ctx, null, specials: false, list) >= 1` at rod 0, `neutralBaitRating`, `neutralDepth`. Miss -> `"V1 dead sub-area: <zone> <subArea> <Season> <Band> <Weather>"`.
  - **V2:** same contexts minus the sub-area: for each sub-area the non-special eligible rules give shares `w / sum(w)` per species (`w = rule.Weight`, the 12.4 step 3 weight under neutral bait, events and skills); the mixed share of a species = the mean of its shares over all sub-areas (a sub-area with an empty pool contributes 0). Fewer than 3 species with a share > 0 -> `"V2 fewer than 3 species: <zone> <Season> <Band> <Weather> (<n>)"`; a share > 0.5 -> `"V2 dominant species: <zone> <Season> <Band> <Weather> <species> <share%>"`.
  - **V3:** every species index of `db.Fishing.Species` passes (`Passes`, ledger null, specials included) in at least one context of every zone x season x band x {Clear, Rain, Fog} x sub-area at rod 3, rating 10, depth 6. Miss -> `"V3 unreachable species: <speciesIndex>"` (the overload prints the id).
- `internal static List<string> Variety(EcologyDatabase db)` (+ the names overload), CR-6/CR-7 rulings: for each zone x season, `E(band)` = species with a non-special rule passing at rod 3, rating 10, depth 6 in some weather {Clear, Rain, Fog} and some sub-area at `BandStart[band]`. Checks: `|union over bands| >= 4` (`"Variety fewer than 4 species: <zone> <Season> (<n>)"`); at least one species whose eligible bands form one contiguous run of at most 2 bands by band index 0..5 (`"Variety no narrow-window species: <zone> <Season>"`; the day does not wrap, so Night (5) and Dawn (0) are NOT adjacent: never take band indices modulo 6); `|E(Morning) symmetric-difference E(Night)| >= 2` (`"Variety Morning and Night differ by <n>: <zone> <Season>"`).
- `internal static List<string> CoverageReport(EcologyDatabase db, ...)`: the `FG ECO V1/V2/V3/VARIETY` lines of 0.6 (V2 one line per context with its top species), used by tests and the menu.
- `[MenuItem("FishGame/Ecology/Print Coverage Report")] PrintCoverageMenu()`: logs `CoverageReport` lines and the `All()` result with `Debug.Log`.

**`ValidationRunner.Run()` hook:** `all.AddRange(EcologyValidators.All());` on the line after `Add(all, "Render", RenderValidators.All());` (T16 adds its `MapChecks` line after this one).

### 4.3 Fightability (`Tuning/EcologyFightabilityTests.cs`)

`[Category("Tuning")] public class EcologyFightabilityTests`; no reference to `EcologySeedData`/`FishingSeedData`; `EcologyTuningItems` map at the top (0.5).
- Load the registry asset; rod-0 species = species of every rule (specials included) with `MinRodTier == 0` in `registry.Ecology` zones; group them by fight profile id (`registry.SpeciesAt(i).Fight.Id`). The Twig rod = the `RodDef` with tier 0 (`ToCore()`); tuning = `registry.Fishing.Tuning`; profile = `registry.FightProfileAt(...).ToCore()`.
- Per profile: `FishingBots.RunFight(FishingBots.BotKind.Rhythm, profile, twig, tuning, i)` for i = 0..199 (R72 seeds and 10-14 m casts; weight cannot change an R72 fight, so species sharing a profile share the result). Land% = `Landed` count / 200; also snap% and timeout% and the landed median (nearest rank). Print `FG FIGHT` per profile and `FG TIME`.
- Target per profile: land >= 90% (`FG TARGET` per profile). A miss passes only as an approved `EcologyTuningItems` entry with the numbers (M3-D38: M3 never retunes R72 fight values).

### 4.4 Steps (TDD)

1. Step 0 (4.1).
2. Write the three test files (4.5). `compile`: expected red.
3. Write `EcologyValidators` (no hook yet). `test-edit -Filter "FishGame.Tests.EditMode.EcologyValidatorTests"` green.
4. Add the hook. `build`: expect `FG BUILDALL PASS`. A real-data miss here means the roster data fails a rule: V1-V3 and data rules must not be "fixed" by masks or sub-area changes (design data); report such a miss with the `FG ECO` lines and stop (controller). The CR-6 narrow-window rule is the only 13.3 interpretation; if the controller kept the literal single-band rule, the variety test gets an `EcologyTuningItems` entry (with approval).
5. `test-edit -Filter "FishGame.Tests.EditMode.EcologyCoverageTests|FishGame.Tests.EditMode.EcologyFightabilityTests"`; extract the `FG` lines (0.6) and paste them.
6. Mutation checks (show red once, restore): V2 using pooled weights over all sub-areas instead of the mean of shares -> the dominance unit case flips; dropping the sub-area loop in V1 (any sub-area) -> `EcologyValidators_Coverage_ReportsDeadSubArea` red; `DataRules` with `specialChance` upper bound `< 0.5` instead of `<= 0.5` -> the specials case at exactly 0.5 red.
7. Full `test-edit -TimeoutMinutes 15` in the background; `FG TIME` within 0.7. Green (tuning items show as skipped).
8. Commit.

### 4.5 Tests (exact names, C 10.3)

`Tests/EditMode/Ecology/EcologyValidatorTests.cs` (class `EcologyValidatorTests`; in-memory Defs and Core data only; synthetic `EcologyDatabase` from `FishingTestData.Database()` (5 species, 2 junk; read-only use, R72-owned) plus hand-built `ZoneData`; contexts use neutral bait explicitly, so the M3-D54 bait concern does not apply):

| Test | Cases | Catches |
|---|---|---|
| `EcologyValidators_DataRules_ReportEachViolation` | `[TestCase]` per rule, each on an otherwise valid minimal registry, asserting exactly one message containing the given text: `"duplicate_id"` and `"unresolved_reference"` (-> the single skip message, and `registry.Validate()` holds the specific text), `"weight_min_not_below_max"`, `"base_value_not_positive"`, `"nonspecial_weight_not_positive"`, `"special_chance_zero"`, `"special_chance_above_half"` (0.5 passes, 0.51 fails), `"special_flag_mismatch"` (both directions), `"subareas_not_contiguous"` (first min 1; a gap; last band closed), `"junk_chance_out_of_range"` (0.61; -0.01), `"species_in_no_rule"`, `"too_many_subareas"` (9), `"mask_zero"` (seasons, weather, bands each), `"catch_limit_below_minus_one"`, `"weather_rows_invalid"` (a 0, a NaN, a 0 season multiplier), `"junk_weight_not_positive"`, `"cast_min_dist_mismatch"` (3.0), `"min_rod_tier_above_rods"`, `"map_order_invalid"` (5; two spots with 1), `"hotspot_tuning_invalid"` (first min > max; radius 0), `"sun_moon_invalid"` (sunrise >= sunset), `"weather_looks_invalid"` (2 entries; fog min 0.04), `"season_ambience_invalid"` (3 entries; lap max < min). Plus: a fully valid registry gives no message. | a missing rule; a boundary off by one |
| `EcologyValidators_Coverage_ReportsDeadSubArea` | one zone, 2 sub-areas; species A rules all bands in sub-area 0 only, species B sub-area 1 every band except Night: exactly the messages `V1 dead sub-area` for sub-area 1 at Night in every season and weather (12 messages); adding a Night rule for sub-area 1 clears them. A rod-1-only rule does not count at rod 0; a special does not count. | ignoring sub-areas, rod tier or the specials exclusion |
| `EcologyValidators_Variety_ReportsDominantSpecies` | V2 (spec "variety"): one zone, 2 sub-areas, weights {A 30, B 5, C 5} in both -> `V2 dominant species ... A 75.0%`; {A 10, B 10, C 10} -> no V2 message; two species only -> `V2 fewer than 3 species`. Mixed shares are the mean of per-sub-area shares (a hand-computed case where pooled weights would give a different answer: sub-area 0 {A 1}, sub-area 1 {B 30, C 30, D 30} -> A 50.0%, not 1/91). `Variety`: a zone with 3 species -> `Variety fewer than 4 species`; Morning and Night sets equal -> `differ by 0`; every species on 3+ bands -> `no narrow-window species`. | pooled weights; specials counted; wrong symmetric difference |

`Tests/EditMode/Ecology/EcologyCoverageTests.cs` (class `EcologyCoverageTests`; real assets; `EcologyTuningItems` map at the top; each test prints its `FG ECO` lines):

| Test | Assertion |
|---|---|
| `Validation_Ecology_Pass` | `EcologyValidators.All()` is empty (M1 `AssertNoErrors` style). |
| `EcologyCoverage_V1_NoDeadSubArea` | no V1 message from `Coverage` on `registry.Ecology` (prints `FG ECO V1` per zone). |
| `EcologyCoverage_V2_ThreeSpeciesNoneAboveHalf` | no V2 message; prints the 144 context lines (2 zones x 4 x 6 x 3). |
| `EcologyCoverage_V3_EverySpeciesPassesSomewhereAtRodThree` | no V3 message; prints `FG ECO V3` per species with its passing-context count. |
| `EcologyVariety_EverySpotSeason_FourEligibleAndOneSingleBand` | no "fewer than 4" and no "narrow-window" message (CR-6); prints `FG ECO VARIETY` with the literal single-band count. |
| `EcologyVariety_MorningVsNight_DifferByTwoSpecies` | no "differ by" message. |

`Tests/EditMode/Tuning/EcologyFightabilityTests.cs`: `EcologyFightability_RhythmTwig_LandsEveryRodZeroSpeciesNinetyPercent` (4.3). Catches: a bot copy instead of R72's (the reviewer greps for `Decide` or `FightInput` in the file: none allowed), seeds or assets instead of the registry, a profile missed because a species appears only in one zone.

Each test carries a `// Catches:` comment naming the wrong implementation (C2 16.4).

### 4.6 Acceptance (reviewer checks)

- 1.7 rules all present with the message formats; `DataRules` never repeats `registry.Validate()` text; coverage contexts use neutral events/skills and `BaitMods.Neutral`.
- V2 is the mean of per-sub-area shares; specials excluded from V1/V2/variety, included in V3.
- No reference to `EcologySeedData`/`FishingSeedData` in the four files; no copy of bot logic; `FishingBots.cs` is untouched (no diff; CR-1 was applied by R72 F4).
- The `FG ECO`, `FG FIGHT` and `FG TARGET` blocks are in the report (skeleton 7: V1-V3 and variety reports printed); any tuning item has numbers and the controller's approval.
- Full `test-edit` PASS line and `FG TIME` pasted.

### 4.7 Commit

```
git add Assets/_Project/Editor/Validation/EcologyValidators.cs Assets/_Project/Editor/Validation/EcologyValidators.cs.meta Assets/_Project/Editor/Validation/ValidationRunner.cs Assets/_Project/Tests/EditMode/Ecology/EcologyValidatorTests.cs Assets/_Project/Tests/EditMode/Ecology/EcologyValidatorTests.cs.meta Assets/_Project/Tests/EditMode/Ecology/EcologyCoverageTests.cs Assets/_Project/Tests/EditMode/Ecology/EcologyCoverageTests.cs.meta Assets/_Project/Tests/EditMode/Tuning/EcologyFightabilityTests.cs Assets/_Project/Tests/EditMode/Tuning/EcologyFightabilityTests.cs.meta
git commit -m "m3(coverage): ecology data rules, V1-V3, 13.3 variety and fightability on the R72 Rhythm bot" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
`Tuning/FishingBots.cs` is never added to this commit (CR-1 was applied by R72 F4).

---

## 5. T12 - Balance reports

- **Goal:** the V4 expedition sim and the V5 economy report with the M4 quota-calibration print, on the real assets, the real resolver, the real `WeatherDirector` and the R72 Rhythm bot; misses retuned in data or recorded as tuning items.
- **Owned files (C 12):** `Tests/EditMode/Balance/EcologyBalanceReports.cs` (+ the `Balance` folder meta); retuned `.asset` values under `Content/Fishing/Species/*` and `Content/Ecology/Zones/*` with matching seeds in `EcologySeedData` and entries in `EcologyTunedDeviations` (`EcologyContentTests.cs`). No production code: the optional `Core/Ecology/ExpeditionSim.cs` is **not** created (both reports live in one file and share private helpers).
- **Depends on:** T11; R72 F8 committed (gate). **Implementer:** Sonnet. **Reviewer:** Sonnet (Opus high if a target needs more than data).
- **C 2.8 that bite:** #22; 0.4 assets not seeds; C 1.2 #1 (never retune R72 values); #21; test-local random streams only (`Rng.Stream(seed, "balance.v4.cast" | "balance.v4.catch", index)`), never an `EcologyStreams` id; neutral events/skills in every context.

### 5.1 Gate

R72 F8 committed (`git log --oneline -- docs/superpowers/plans/2026-10-12-m2-rework-fight.md` and the spec 9.6/9.12 measured table exist) and CR-1 in place (applied by R72 F4; confirmed in T11 step 0). Otherwise stop and report.

### 5.2 File layout

`[Category("Balance")] public class EcologyBalanceReports`, namespace `FishGame.Tests.EditMode`; `EcologyTuningItems` map and the private target helper at the top (0.5); `[OneTimeSetUp]` loads the registry asset once (`registry.Ecology`, `registry.Fishing`) and clears the static caches; a `Stopwatch` over the whole fixture prints `FG TIME EcologyBalanceReports <s>` in `[OneTimeTearDown]`.

### 5.3 V4 expedition sim (C 11.4, M3-D39, CR-7)

```
for each zone z, season s in Spring..Winter, run r in 0..399:
    runSeed  = unchecked(0x9E3779B97F4A7C15UL * (ulong)(r + 1))
    weather  = new WeatherDirector(zone.Weather, s, runSeed)          // season fixed per cell, not RollSeason
    castRng  = Rng.Stream(runSeed, "balance.v4.cast", 0)
    ledger   = new CatchLedger()                                       // catch limits are per run
    for day d in 1..7:  rodTier = d <= 2 ? 0 : d <= 4 ? 1 : d <= 6 ? 2 : 3   // Twig, Handmade, Carbon, Deep-sea
        for cast c in 0..59:
            minute = castRng.Range(0, 1200); sub = castRng.Range(0, zone.SubAreas.Count)   // int ranges, max exclusive
            ctx = { ZoneIndex z, SubAreaIndex sub, Depth 2, Minute minute, Season s, Weather weather.At(d, minute),
                    RodTier rodTier, Bait BaitMods.Neutral(1), InHotspot false, Luck 0, Events Neutral, Skills Neutral, PlayersInSession 1 }
            catchRng = Rng.Stream(runSeed, "balance.v4.catch", (ulong)(d * 100 + c))
            result = resolver.Resolve(ctx, ref catchRng, ledger, out _, out int ruleIndex)
            if fish and zone.Rules[ruleIndex].CatchLimitPerRun >= 0: ledger.Reserve(sp); ledger.Commit(sp)   // landed (the host's commit path)
            count per species, junk per day
```
(Depth 2 is a placeholder: every M3 rule has `minDepth` 0, so depth never gates; bite timing is V5's business.)

Bounds (means over the 400 runs of a (zone, season) cell):
- For each species with a rule in zone `z` whose `Seasons` contains `s` ("in season"; the rod path reaches tier 3 by day 7, so every in-season species is "at the required rod" for part of the run): mean catches per run >= Common 7, Uncommon 3.5, Rare 0.7, Legendary (special) 0.4 (by `FishSpeciesData.Rarity`).
- Junk: mean junk share over the run <= 20%, and for every day d the mean over runs of that day's junk share <= 20% (a single run-day may exceed it; 60 casts give a binomial spread of about 4.5 points).
- No species above 50% of a run's fish catches, in **every** run (print the maximum over runs).

Expected from a rough hand estimate (not data): every bound passes with neutral bait; the slimmest are sunny_trout in Spring (about 4.8 vs 3.5; Clear-only, two bands, open water only) and the specials (about 0.85-0.9 vs 0.4). Treat as a hint, never as data.

### 5.4 V5 economy (C 11.4, M3-D39, CR-5, CR-7)

**Fight medians (CR-5).** For each profile (5) x rod (4, by tier: `rod_twig`, `rod_handmade`, `rod_carbon`, `rod_deepsea`): 200 Rhythm fights `FishingBots.RunFight(Rhythm, profile, rod, tuning, i)`, i = 0..199; median `Elapsed` of landed fights (nearest rank) and land rate. Cache statically. Print `FG V5 MEDIAN` with F8's Handmade/Twig number for comparison (literal copy from spec 9.6/9.12 after F8; `n/a` for Carbon and Deep-sea). The gap to F8 is information only (the user may retune fight data after F8, and F8 publishes a condensed table); print it, never stop on it. Stop only if `FishingBots.cs` changed since T11's step-0 check (compare its `git log`).

**Per context** (zone, season, sub-area a, band b with `Minute = BandStart[b]`, weather w in {Clear, Rain, Fog}, rod):
```
j        = resolver.JunkShare(ctx)                                       // neutral bait: JunkMult 1
pool     = CollectEligible(ctx, null, specials: false); w_s = rule.Weight; p_s = (1 - j) * w_s / sum(w)   (empty pool: j = 1)
EV_s     = mean over u_k = (k + 0.5) / 20000, k < 20000, of CatchMath.Value(base_s, t, CatchMath.Quality(t)), t = CatchMath.SkewedT(u_k, CatchMath.Skew(skew_s, 0))
KG_s     = same mean of CatchMath.WeightG(min_s, max_s, t) / 1000
EVjunk   = sum over zone junk of weight * valueCoins / sum(weight)
wait_s   = max(tuning.MinBiteWait, 0.5 * (tuning.BiteWaitMin + tuning.BiteWaitMax) * BitePlanner.WaitMultiplier(1.0 (neutral bait speed), biteWaitMod_s (1 for junk), depth_a, 1, 1, false))
fight_s  = median(profile_s, rod); junk: (12 - tuning.LandDistance) / tuning.JunkDragSpeed
land_s   = landRate(profile_s, rod)
overhead = tuning.CastChargeTime + CastMath.FlightTime(12) + tuning.HookBeat + FishingConstants.FeedbackBeatSeconds + 10 (walking, M3-D39)
cycle    = overhead + j * (wait_junk + fight_junk) + sum_s p_s * (wait_s + fight_s)
coins    = sum_s p_s * land_s * EV_s + j * EVjunk;   kg = sum_s p_s * land_s * KG_s
coinsPerMin = 60 * coins / cycle;  kgPerMin = 60 * kg / cycle                // renewal-reward rate
```
`depth_a` = 0.75, 1.5, 3.0 m for sub-area index 0, 1, 2 (a printed report assumption; grids are not baked yet). Specials are excluded (catch-limited; CR-7). Use the exact `BitePlanner.WaitMultiplier` parameter order from its signature (spec 9.4 terms).

**Best-for-coins bound.** For each zone x season at rod 0: over the 18 (band, weather) contexts, the sub-area with the highest `coinsPerMin` wins (ties: lower sub-area index). Every sub-area's wins <= 85% of 18 (i.e. <= 15). Print `FG V5` per context and `FG V5 BEST` per sub-area.

**Quota calibration print (14.4 input).** For each zone x season: `pi` = the stationary distribution of the season's renormalised chain (`WeatherDirector.RowProbabilities`, 200 power iterations from Clear). Day d (1..7) on the rod path: `coins_d = sum_b sum_w pi(w) * (bandMinutes_b / DayClock.DefaultMinutesPerSecond) * mean_a coinsPerSec(a, b, w, rod_d)` (band minutes from `BandStart`: 120, 180, 180, 180, 180, 360); `kg_d` likewise; `bestCoins_d` uses the max over sub-areas instead of the mean. Plateau (day 8+) = the Deep-sea day value. Print `FG QUOTA` lines and `day4/day1`.

### 5.5 Tests (exact names, C 10.3)

| Test | Assertion | Catches |
|---|---|---|
| `EcologyBalance_V4_ExpeditionCatchesPerSpecies` | prints `FG V4` per (zone, season, in-season species); one target per line (5.3 rarity bounds). | rarity bounds read from the wrong field; out-of-season species asserted; limits not reserved (a special counted twice per run) |
| `EcologyBalance_V4_JunkAtMostTwentyPercentEveryDay` | prints `FG V4 JUNK`; targets: run mean and each day mean <= 20%. | bare-hook junk multiplier in the sim; per-run-day instead of mean |
| `EcologyBalance_V4_NoSpeciesAboveHalfOfARun` | prints `FG V4 TOP`; target: max share over all runs <= 50%. | junk counted as a species; a mean hiding one bad run |
| `EcologyBalance_V5_NoSubAreaBestForCoinsAboveEightyFivePercent` | prints `FG V5 MEDIAN`, the 432 context lines at rod 0 (2 zones x 4 seasons x 3 sub-areas x 6 bands x 3 weathers) and `FG V5 BEST`; targets per (zone, season, sub-area). | wins counted over sub-areas instead of contexts; specials in the economy; the junk drag ignored |
| `EcologyBalance_V5_PrintsQuotaCalibrationInputs` | prints `FG QUOTA`; fails only if a number is NaN or infinite, or a day value is <= 0. | a silent NaN from an empty pool or a zero cycle |

### 5.6 Retune procedure (data only)

1. Run `test-edit -Filter "FishGame.Tests.EditMode.EcologyBalanceReports" -TimeoutMinutes 15`; paste the first-run `FG` block.
2. For each failing target change **one** knob per iteration and rerun the filter (and `EcologyCoverageTests`, so V1-V3 stay green). Allowed knobs: a rule's `weight`, a special's `specialChance` (<= 0.5), a sub-area `junkChance` (<= 0.6), a zone's junk weights. Not allowed (design data; report to the controller): seasons, weather, bands, sub-areas, rod tiers, profiles, kg ranges, `baseValue`, anything R72-owned.
3. Each kept change edits, in the same commit: the `.asset` YAML value (Write/Edit only, LF), the seed in `EcologySeedData` (keep its `// 12.6 row N` comment and add `// T12 retune: <old> -> <new>`), and the `EcologyTunedDeviations` entry (0.5). Keep a table in the report: iteration, change, affected `FG` lines.
4. Time box: 6 iterations after the first run. A target still failing becomes an `EcologyTuningItems` entry with the controller's approval. A target that cannot move with the allowed knobs stops the task: report to the controller (Opus high review per skeleton).
5. Final: `build` (create-if-missing must leave the retuned assets untouched: `git diff` on them shows only your edits), then full `test-edit -TimeoutMinutes 15` in the background; paste the final `FG` block, `FG TIME` and the `FG RESULT` line.

### 5.7 Steps (TDD)

1. Gate (5.1).
2. Write the file with the five tests. `compile` PASS.
3. Mutation checks (red once, restore): V4 without `ledger.Reserve/Commit` -> the special counts exceed 1 per run (assert inside the sim: a limit-1 species never counted twice in a run); V5 using `p_s` without `(1 - j)` -> a hand-checked context's `coinsPerMin` changes (add a private self-check on one context with a hand-computed expectation, tolerance 1e-3).
4. Retune per 5.6.
5. Full `test-edit` green (tuning items skipped). `git status`: only the report file (+ metas), edited `.asset` files, `EcologySeedData.Roster.cs`/`EcologySeedData.cs`, `EcologyContentTests.cs`.
6. Commit.

### 5.8 Acceptance (reviewer checks)

- 5.3/5.4 procedures line by line (seeds, rod path, neutral bait, catch-limit reservation, the renewal-reward rate (expected coins over expected cycle), tie rule, depth table, F8 cross-check).
- No `EcologySeedData`/`FishingSeedData`/`UnityEngine.Random`/`System.Random`; data from the registry asset; bot via `FishingBots.RunFight` only.
- Every retuned value identical in asset, seed and `EcologyTunedDeviations`; only allowed knobs; V1-V3 still green.
- The V4/V5 tables and `FG QUOTA` block pasted (skeleton 7); tuning items with numbers and approval; `FG TIME` within 0.7.

### 5.9 Commit

```
git add Assets/_Project/Tests/EditMode/Balance Assets/_Project/Tests/EditMode/Balance.meta <each edited Content/**/*.asset> Assets/_Project/Editor/Builders/Ecology/EcologySeedData.Roster.cs Assets/_Project/Editor/Builders/Ecology/EcologySeedData.cs Assets/_Project/Tests/EditMode/Ecology/EcologyContentTests.cs
git commit -m "m3(balance): V4 expedition and V5 economy reports, quota calibration print; retuned <id.field old->new, ...>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
With no retune, drop the asset, seed and deviation paths and write "no retune needed".

---

## 6. Risks (section-level)

1. **R72 not built yet.** If F4.1 changes before it lands (bot names, `RunFight` signature), T11/T12 stop at their gate and this plan's 4.3/5.4 need a one-line update. CR-1 is folded into F4 (accepted 2026-10-12), so F4 must land with the five members internal.
2. **Roster-level misses that data knobs cannot fix.** CR-6 (single band) was certain and is ruled (M3-D65); V5's 85% bound may fail where one sub-area holds all high-value species (lake open_water: sunny_trout 30 and mudcat 22). Those go to the controller/user as roster questions, not silent retunes.
3. **Bluegill drift between sections A and C** turns `EcologyData_AssetValuesEqualSeeds` red; mitigated by cross-section question 1 and a reviewer diff.
4. **The R74 standard may change the species-model rule** (`IsFishModelMesh`, triangle budget). The CR-2 model test and `EcologyData_ModelFiles_ReuseFourM2Models` follow whatever T4 commits; whichever lands second adapts its selection only.
5. **Test runtime.** Tuning and Balance run in every later `test-edit`; budgets in 0.7, measured and reported, controller decides on overruns.
6. **V5 is a model, not play.** Cycle times use bot medians for a Rhythm player, an assumed 10 s walk, representative depths and the stationary weather; quota numbers printed here are calibration inputs for M4, to be retuned from `runlog.csv` (spec K3).
7. **`PlaceholderRoller` on 25 ungated species from T10 to T13.** Until T13 replaces it, the M2 roller draws over every species (all have `minBaitRating` 0 and `minDepth` 0), specials included. Checked: the EditMode tests that roll on `FishingSeedData.BuildDatabase()` (`FishingWireTests`, `FishingHostGuardTests`) are outcome-agnostic (round trip, sanity, "the sample contains a fish"), so T10 keeps them green. PlayMode is not run in T10-T12; any PlayMode test that pins a real-registry species meets the resolver in T13 anyway.
8. **Generated-asset volume.** T10 adds about 40 small `.asset` files; the two grid assets grow to about 150 KB each when T16/T17 bake them (`ushort[]` YAML lists). Not LFS; acceptable.
