# FishGame M3 Fish Ecology - Manual Acceptance Checklist

- **Date written:** 2026-10-13 (task T25)
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` section 17.3 (M3 definition of done, 8 bullets, expanded here into criteria 1-12), 12.6-12.10 (roster, hotspots, validators, journal), 13.3 (variety), R74-R77.
- **Contract:** `docs/superpowers/plans/2026-10-13-m3-interfaces.md` (rows M3-D1 to M3-D96; section 10 holds the test inventory).
- **Section plans:** `2026-10-13-m3-plan-{carry-migration,core,data-coverage,host-world,maps,presentation,journal-ui}.md`; controller notes: `2026-10-13-m3-controller-notes.md`.
- **Who runs it:** the user does the manual rows (editor, Multiplayer Play Mode (MPPM) 3.0, the Network Simulator, a development player build, your own eyes). Claude ran the automated rows (T25) and pasted their numbers below.
- **How to record:** tick the box, then replace `______` with `PASS`, `FAIL` or `BLOCKED`, the date, and a short note (what you saw, or the error text). A `FAIL` needs enough detail to reproduce. The same items are mirrored, one per row, on the M3 Checks board (see "Item IDs" below).
- **Setup help:** `README.md`, sections "Local multiplayer testing", "Fishing dev keys", "Ecology dev panel" and "Journal / meta.json".
- **Measured on:** branch `m3-t25` cut from `m3-fish-ecology` at `eaab0aa` (T1-T24, fix batches A/B/C, T17 harbour, T21 journal and T22 dev panel all merged), Unity 6000.6.5f1, 2026-10-13. Raw logs and result files of that run are in the T25 implementer's scratchpad, not in the repo.

## Item IDs and the existing check list

`docs/superpowers/handoff/2026-10-13-user-checks.md` is the single list of everything the user owes, and the M3 Checks board mirrors it (IDs W1, S*, F*, L*, N1, D1, D2, M1-M6, P1-P6, B1, B2, X1, X2, G1, plus the re-check rows RL1, RS1, RF1, RL2, RG1, H1, J1, DP1). This document does **not** copy those. Where one of them covers an M3 item, the row below says "board M4" and so on. The IDs that are new here, and safe to add to the board, are:

| Prefix | Meaning | Where |
|---|---|---|
| ME1-ME8 | MPPM checks | section B |
| DC1-DC5 | dev-panel direction checks | section C |
| DN1-DN5 | day-night, weather and readability | section D |
| JE1-JE9 | journal and save | section E |
| DV1-DV9 | dev panel, F3 modes, F8 scrub, clock | section H.2 |
| HP1, HP2 | harbour play-through and the T15 marker check | section H.2 |
| KP1 | the real key paths (J, F3, F8, Esc) | section H.2 |
| AV1-AV3 | art verdicts (fish, harbour, journal icons) | section G |
| NB1 | non-development player build | section H.2 (board B2 is the same build) |

## Before you start

- [ ] All M3 tasks (T1-T24) are committed and `Tools\unity.ps1 check` prints `FG RESULT check PASS` (section A, criterion 5 holds the line).
- [ ] `FishGame > Build All` has run on this checkout. Do not hand-edit generated scenes or prefabs during these tests.
- [ ] Work in a worktree no agent is using, and close the editor before agents run Unity there (README, "Build and test from the command line").
- [ ] `FishGame > Dev > Auto Solo` is **off** for every multiplayer item (section B). Turn it on for the solo items.
- [ ] The MPPM scenario `Direct 4P` exists (README, "Set up a scenario"), and the `NetSim_150ms_2pct` preset exists (README, "Latency with the Network Simulator").
- [ ] A development player build exists for ME4 (the version-mismatch item) and NB1; a second, non-development build exists for NB1.
- [ ] The open M2 acceptance items (`2026-10-11-m2-acceptance.md`) are run or explicitly deferred by you (the M3 entry gate). Most of them are on the board as S*, F*, L*, N1 and P1.
- [ ] A solo session opens at dawn, in autumn, in fog. That is the fixed dev run seed (M3-D57, decided), not a bug.

## A. Criteria 1-12 (spec 17.3)

Spec 17.3 has eight bullets. Rows 1-4 split its first two bullets; row 5 is the validators/balance bullet; rows 6-9 are bullets 4-7; row 10 is the last bullet; rows 11 and 12 are the M2 carry list and the R74 art status (M3 scope, not a spec bullet).

| # | Criterion (17.3) | Owner | How to check (short) | Status 2026-10-13 |
|---|---|---|---|---|
| 1 | 23 species, 2 specials, 7 junk (`old_boot` shared), 2 spots, the weather table, 5 fight profiles and 2 baked grids with the 12.6 values | Claude | `test-edit` data rows | PASS (EditMode): 4 data tests + `Validation_EcologyMaps_Pass` passed |
| 2 | `WeatherDirector` pre-rolls 35 segments from one seed, deterministically, with the season held for the week | Claude + User | EditMode tests, then DC/DN panel checks | Tests PASS; manual open (user) |
| 3 | SceneBuilder builds `Spot_HarbourPier`; both grids re-baked in place (stable GUIDs); the lake passes reachability | Claude | two `build` runs, `git status`, reachability tests | PASS: GUIDs and grid bytes unchanged across two builds; counts below |
| 4 | `PlaceholderRoller` replaced by `FishingResolver`; bait has `BaitMods`; registry indices and `ContentHash` identical across two builds and sessions | Claude (T25.3) | `git grep`, two-build hash check | PASS: hash `54AA263BB84936CE` twice, index lists identical |
| 5 | 12.9 validators, fightability, resolver tests and 13.3 variety pass; balance reports pass or have recorded tuning items | Claude | `test-edit`, section F | PASS: every report passes; `EcologyTuningItems` empty |
| 6 | Dev panel changes to season, band, weather, rod tier or hotspot visibly change catches in the direction the data predicts | User | section C | Open (user) |
| 7 | `DayNightController` and the seasonal palette work on the dev clock with no pops (F8 scrub); bobber, marker and dock edge readable at night and in maximum fog at 25 m; the fog readability test passes | Claude + User | PlayMode and EditMode tests, then section D | Tests PASS; manual open (user) |
| 8 | MPPM: every `CatchAnnounced` shows identical species, weight, quality and value on all peers; journals right for catcher and witness; clients cannot change world state | User | section B | Open (user) |
| 9 | The journal persists in `meta.json` v1, recovers from a corrupt main file via `.bak`, and shows levels 0-3 and "biting now" correctly | Claude + User | `SaveService_*` tests, then section E | Tests PASS; manual open (user) |
| 10 | Core compiles with `noEngineReferences`; Fishing does not reference Networking or UI | Claude | asmdef read plus the compile | PASS (no dedicated test; see the block) |
| 11 | M2 carry list (contract 14 / 10.1) | Claude | T1/T2/T3 tests | PASS (list below) |
| 12 | R74 art: 25 fish models, harbour props, journal icons | User | section G | Models in game (25 of 25); verdicts open (user) |

### Criterion 1: Species, junk, spots, weather, profiles and grids exist with the 12.6 values
- [x] **Owner:** Claude (T25). **How to check:** `Tools\unity.ps1 test-edit -Filter "EcologyData_|Validation_EcologyMaps"`.
- **Automated rows (EditMode, all passed in the full run, 1378 tests, 1377 passed, 1 skipped):** `EcologyData_SeedsMatchRoster`, `EcologyData_AssetValuesEqualSeeds`, `EcologyData_SevenJunkOldBootShared`, `EcologyData_SubAreasMatchSpec125`, `Validation_EcologyMaps_Pass`.
- **Content counts (from the registry index lists, criterion 4):** 25 species (23 regular plus the specials `gilded_carp` and `old_barnacle`), 7 junk, 2 spots, 2 zones, 4 rods, 6 baits (the bare hook included), 5 fight profiles.
- **Skipped test:** `WaveGpuParity_FgWaveHeightMatchesWaveMath` needs a graphics device and skips itself under `-nographics`. It is not an M3 item.
- **Result:** PASS 2026-10-13.

### Criterion 2: The weather track
- [ ] **Owner:** Claude (tests) + User. **How to check:** EditMode, then the panel.
- **Automated (EditMode, passed):** `WeatherDirector_SameSeed_SameThirtyFiveSegments`, `WeatherDirector_RollSeason_DeterministicAndRoughlyUniform`, `DevWorld_MapSwitchResume_RebuiltTrackIdentical`.
- **Steps (user):** solo, F3 twice for the panel. Press **New day** 7 times, reading the WORLD line (F3 once) each time. Then leave to the menu, start Solo again and repeat.
- **Expected:** the season never changes across the 7 days; the weather per day and segment is the same sequence both times (the fixed seed); 35 segments cover days 1-7.
- **Record:** season: ______. Weather days 1-3, first run: ______. Second run: ______.
- **Result:** ______

### Criterion 3: Harbour Pier is built, both grids re-baked in place, the lake passes reachability
- [x] **Owner:** Claude (T25). **How to check:** `build` twice, then `git status`; the reachability tests.
- **Two builds:** the full `build` ran twice on the same checkout (PASS both times). Afterwards `git status` listed no `.asset` or `.meta` change for the grids or any other Content asset (only the usual scene renumbering, which was restored, plus an editor-written `.vscode/settings.json`). So the grid GUIDs are stable and the bake is byte-identical.
- **Grid files (first 16 hex digits of the SHA-256 of the committed asset, equal after both builds):** lake `52b0a39cc6cd7afe`, harbour `8c32acabb114b79c`.
- **Bake counts from the build log (castable cells per sub-area):**

| Spot | Water | Land | Castable | Reachable | Stand points | Sub-area castable cells |
|---|---|---|---|---|---|---|
| `Spot_LakeDock` | 7882 | 6518 | 6792 | 1858 | 10 | reeds 294, open_water 794, deep_water 770 |
| `Spot_HarbourPier` | 12451 | 1949 | 12046 | 3025 | 11 | shallows 507, channel 1016, open_sea 1502 |

- **Automated (EditMode, passed):** `ShoreGridBaker_LakeEverySubArea_TwentyFiveReachableCastableCells`, `ShoreGridBaker_HarbourEverySubArea_TwentyFiveReachableCastableCells`, `HarbourPier_SceneHasEveryPerSceneHook`, `HarbourPier_Rebuild_ByteIdenticalMeshes` (covers only the code-built meshes: water, skirt, seabed, beach), `HarbourPier_ModelBoundsMatchLayout` (the Blender FBX against the layout numbers), `WorldState_SceneObject_HashNonZeroAndSpotIdSet`.
- **Harbour build line:** `FG HARBOUR renderers=70 triangles=186844 meshes=22 materials=3` (the 10 Blender FBX plus the code meshes).
- **Result:** PASS 2026-10-13.

### Criterion 4: Resolver, BaitMods, and identical `ContentHash` across two builds
- [x] **Owner:** Claude (T25.3). **How to check:** `git grep`, then the two-build hash check.
- **`git grep -n PlaceholderRoller -- Assets`:** two hits, both comments (`Core/Ecology/FishingRoller.cs:1` "replaces PlaceholderRoller" and a note in `FishingRollValidationTests.cs`). The class and `RollRequest` no longer exist.
- **Two-build hash check:** `build`, then a separate batchmode session that loads `ContentRegistry.asset`, calls `Build()` and logs `ComputeContentHash()` plus every index list; then `build` again and a second new session. The temporary editor script was deleted afterwards (and its `.meta`); `git status` lists neither.

| Run | `FG HASH` | `dataVersion` |
|---|---|---|
| Build 1, session 1 | `54AA263BB84936CE` | 2 |
| Build 2, session 2 | `54AA263BB84936CE` | 2 |

Identical (a file compare of the two extracted log excerpts reported no differences). The index lists, both runs:
  - species: 0 bluegill, 1 bruiser_tuna, 2 bucketmouth_bass, 3 frost_char, 4 gawping_gurnard, 5 gilded_carp, 6 glimmer_eel, 7 grumble_mackerel, 8 grumpy_carp, 9 grumpy_puffer, 10 ice_herring, 11 lake_pike, 12 lanternfish, 13 mirror_minnow, 14 moon_squid, 15 mudcat, 16 old_barnacle, 17 pier_sardine, 18 red_snapper, 19 silver_mullet, 20 sleepy_sturgeon, 21 sneaky_flounder, 22 storm_ray, 23 sunny_trout, 24 yellow_perch
  - junk: 0 empty_bait_tub, 1 lost_sock, 2 old_boot, 3 rope_scrap, 4 seaweed_clump, 5 soggy_newspaper, 6 tin_can
  - zones: 0 harbour_pier, 1 lake_dock. spots: 0 harbour_pier, 1 lake_dock
  - rods: 0 rod_carbon, 1 rod_deepsea, 2 rod_handmade, 3 rod_twig
  - baits: 0 bait_bare_hook, 1 bait_grub, 2 bait_minnow, 3 bait_raw_meat, 4 bait_shrimp, 5 bait_worm
  - fight profiles: 0 bulldog, 1 darter, 2 runner, 3 steady, 4 thrasher
- **Also passed:** `ContentRegistry_Hash_StableAcrossInstances`, `ContentRegistry_Ecology_BuildsDatabase`, `FishingData_BaitAffinitySeeds_MatchContract`.
- **Result:** PASS 2026-10-13.

### Criterion 5: Validators, fightability, resolver tests, variety and balance
- [x] **Owner:** Claude (T25). **How to check:** the full `test-edit` (the Tuning and Balance categories run in it). The numbers are in section F.
- **Summary:** V1 (no dead sub-area), V2 (three species, none above half), V3 (every species passes somewhere at rod 3), the 13.3 variety rules, fightability (Rhythm bot on Twig), V4 expedition catches (70 cells), V4 junk and top-species shares, V5 best-for-coins (24 cells) and the quota calibration print all PASS. `EcologyTuningItems` is empty in all three report classes. Two data retunes are recorded as tuned deviations (section F.6).
- **Result:** PASS 2026-10-13. One note: the first full `test-edit` run of the session had 7 failures in `FishingContentBuilderTests` (`OneTimeSetUp`: "Cannot open file ... Fishing/Rods.meta for write" under `Tests/__Temp`). The rerun passed all 7 with no change, so it was a file-system flake on that folder, not a product defect (section H.4).

### Criterion 6: Dev-panel direction checks
- [ ] **Owner:** User. **How to check:** section C (DC1-DC5).
- **Result:** ______

### Criterion 7: Day-night, seasonal palette, no pops, readable at 25 m
- [ ] **Owner:** Claude (tests) + User. **How to check:** the automated rows, then section D.
- **Automated, EditMode (passed):** `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable`, `DayNightMath_IsReadable_DensityBoundAtPoint0333`.
- **Automated, PlayMode (passed, full run 137 of 137):** `DayNight_ScrubFullDay_NoLightOrFogPops` (5.2 s), `DayNight_FogReadableAt25m_EveryPhaseAndWeather` (27.6 s), both in `PlayMode/DayNightTests.cs`.
- **Result:** ______

### Criterion 8: MPPM agreement
- [ ] **Owner:** User. **How to check:** section B (ME1-ME8).
- **Result:** ______

### Criterion 9: Journal persistence, recovery and levels
- [ ] **Owner:** Claude (tests) + User. **How to check:** the tests below, then section E.
- **Automated (EditMode, passed):** the nine `SaveServiceTests` (the six required, `SaveService_RoundTrip_PreservesJournal`, `SaveService_CorruptMain_LoadsBak`, `SaveService_NoFiles_Defaults`, `SaveService_JsonKeys_CamelCasePropsIdsUnchanged`, `SaveService_UnknownIds_KeptOnRoundTrip`, `SaveService_NewerSchema_NotOverwritten`, plus `SaveService_BothCorrupt_DefaultsAndKeepsCorruptCopy`, `SaveService_CorruptMainGoodBak_KeepsCorruptAndBak`, `SaveService_WriteFailure_LogsOnceNeverThrows`); the four `JournalRecorderTests`; the twelve `JournalRulesTests` (`JournalRules_HintLevels_ZeroToThree` among them); `BitingNow_RespectsSeasonWeatherBandAndRodTier`, `BitingNow_SpecialsCountAsBiting`, `BitingNow_IgnoresSubAreaAndDepth`; `InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly`.
- **Automated, PlayMode:** `EcologySolo_LakeCastThroughResolver_LandsAnnouncesAndJournals` asserts the announce, Seen +1, CrewLanded +1, Caught +1, `SeenInSpots` and the on-disk `meta_fgtest.json` after a real resolver cast (passed, 9.1 s).
- **Result:** ______

### Criterion 10: Core has no engine references; Fishing does not reference Networking or UI
- [x] **Owner:** Claude (T25). **How to check:** read the two asmdefs.
- `Core/FishGame.Core.asmdef` has `"references": []` and `"noEngineReferences": true`; the compile enforces it (the build compiled). `Fishing/FishGame.Fishing.asmdef` references only `FishGame.Core`, `FishGame.Data`, `FishGame.Runtime`, `FishGame.Gameplay`, `Unity.Netcode.Runtime` and `Unity.Collections`: no `FishGame.Networking` and no `FishGame.UI`.
- **There is no dedicated test with that intent.** The nearest is `Architecture_GameplayFishingPresentationUi_DoNotUseServicesOrTransports` (passed), which only bans Unity Services and transports in Gameplay, Fishing, Presentation and UI. The asmdef rule itself is enforced by the compiler. A test that parses the asmdef reference lists would be a small M4 addition.
- **Result:** PASS 2026-10-13.

### Criterion 11: M2 carry list
- [x] **Owner:** Claude (T25). **How to check:** the T1/T2/T3 rows of contract 10.1.
- **EditMode (passed):** `ContentHash_DuplicateCategory_Throws`, `DisplayNameRules_Truncate_NeverSplitsSurrogatePair`, `DisplayNameRules_Truncate_SharedByNameWireAndSessionText`, `FishingHostLedger_Tick_OrderSurvivesDisconnectAndRejoin_AndDoesNotAllocate` (with its positive control), the 14 `ShaderValidatorTests` cases, `EcologyMigration_RemovesM2PlaceholderSpeciesKeepsModels`, `EcologyMigration_OverwritesBluegillWithRosterValues`, `EcologyMigration_WritesBaitM3FieldsOnExistingAssets`, `EcologyMigration_BumpsDataVersionOnce_SecondRunIsNoOp`, `FishingData_SeedsMatchSpecTables` (species count 1, `minLandDepth` 0.5).
- **PlayMode (passed):** `FishingSolo_CastMarker_UpdatedAfterMachineTickSameFrame`, `FishingSolo_SceneReload_KeepsDevOverrides_SessionEndClears`, `UiFlow_OnlineCancel_ShowsCancellingImmediatelyAndReturnsToMenu`.
- **Result:** PASS 2026-10-13.

### Criterion 12: R74 art
- [ ] **Owner:** User. **How to check:** section G, items AV1-AV3 (and board M1, M2, M6).
- **Status:** all 25 species have an R74 Blender model wired on their species asset (no fallback rows); the harbour props come from Blender (10 FBX); the journal icons are baked from the real models. Only your verdicts are open.
- **Result:** ______

## B. Manual MPPM list (spec 12.9 manual, 17.3 bullet 6)

Host plus 1-3 clients, dev-direct (README, "Local multiplayer testing"), Auto Solo off, the 150 ms / 2% preset on the clients only. Each peer should have its own display name. The older MP1-MP11 list is board P1 and stays separate.

- [ ] **ME1 - Same catch everywhere.** A client casts and lands a fish. The species, weight, quality and value read the same in the host's toast, the client's toast and the client's reveal, and in the owner's F3 `ROLL` line.
  - **Record:** species / weight / quality / value on each peer: ______
  - **Result:** ______
- [ ] **ME2 - Journals for catcher and witness.** The catcher's card goes to Caught +1. Every witness gets Seen and CrewLanded (level 2 on the card). A lost (snapped or escaped) fish gives Seen only, on every peer. Open the journal (J) on a witness while the catch happens: it updates live.
  - **Result:** ______
- [ ] **ME3 - Hotspot VFX on every peer.** The host spawns a hotspot with the panel button. Its bubbles and ripples show on every peer at the same place, and a late joiner sees it too. (Board P4 covers the join "snap".)
  - **Result:** ______
- [ ] **ME4 - Version mismatch.** MPPM clones share the project assets, so use a development player build: bump `dataVersion` in the editor's `ContentRegistry.asset` Inspector, host in the editor, and join from the player. Expect the refusal "Different game version" (the version message). Revert the asset afterwards.
  - **Result:** ______
- [ ] **ME5 - Late joiner.** After the host has run for a few minutes, a client joins. It sees the same minute (within one 10-minute display step), the same season and weather, and an active hotspot if there is one.
  - **Result:** ______
- [ ] **ME6 - Clients are read-only.** A client's dev panel (F3 twice) shows the host's values with the title "Dev panel (client: read-only)" and disabled rows. The client's F8 does nothing to the clock. Nothing a client does changes the world: the WORLD lines match the host's. (Known gap: the client side of this was only checked in code and the fake-input tests; this is its first real run.)
  - **Result:** ______
- [ ] **ME7 - Map switch with a client.** The host loads the harbour from the panel. The client follows, respawns, and its WORLD line matches the host's (day, season). Switch back to the lake.
  - **Result:** ______
- [ ] **ME8 - Day over.** On the host panel drag the time slider to its maximum (1:50 AM) and let the clock run past 2:00 AM (about 8 real seconds, or hold F8). The WORLD line shows `open=no`. Both peers' cast markers are invalid and a client cast is refused (the cast does nothing, bait unchanged). **New day** reopens casting on both and the markers turn valid again.
  - **Result:** ______

## C. Dev-panel direction checks (17.3 bullet 4)

For each lever: set the panel as shown (F3 twice; the other values fixed, the dev clock stopped), turn **F6 forced bite** on (host only; **no forced species**, F10 on None) and cast about 15 times per setting, writing down the species of each toast. Use the lake unless stated. The predictions were re-read from the roster seed (`EcologySeedData.Roster.cs`, which carries the T12 retunes) on 2026-10-13 and they hold.

| ID | Lever | Setting A -> B (other values fixed) | Predicted change (from the data) |
|---|---|---|---|
| DC1 | Season | open water, Midday, Clear: Summer -> Winter | Summer gives `mirror_minnow`, bluegill, `grumpy_carp` and `sunny_trout`. Winter gives `mirror_minnow` and `yellow_perch` only. |
| DC2 | Band | open water, Summer, Clear: Midday -> Night | Midday gives `mirror_minnow`, bluegill, `sunny_trout` and `grumpy_carp`. Night gives `grumpy_carp`, `bucketmouth_bass` and `silver_mullet`. |
| DC3 | Weather | Summer, Dusk, reeds or open water: Clear -> Rain | `mudcat` appears only in Rain (and Fog). Bluegill, `grumpy_carp`, `bucketmouth_bass` and `silver_mullet` stay. |
| DC4 | Rod tier | Summer, Dusk, Rain, deep water: tier 0 -> tier 1 | `glimmer_eel` (Rare, rod 1) appears only at tier 1 or more. |
| DC5 | Hotspot | same spot, 20 casts without and with a hotspot at the landing | Junk about halves (x0.5). The bite wait drops by about 40% (x0.6; read it on the F3 `BITE` line). |

- **Notes.** The Legendary lake special `gilded_carp` can turn up in any row at a low rate. That is not a miss. The rod-tier row depends on a Rare fish, so 15 casts may show no eel at all: use about 30 casts, or set **Luck** to 1.0 for bigger fish. A direct check for DC4 is the journal: open it (J) with the rod waiting, and the `glimmer_eel` card shows the "! Biting now" badge at tier 1 and loses it at tier 0 (toggle the rod tier in the panel while Idle).
- **Record per row:** species tally at A: ______. At B: ______. As predicted: yes / no.
- **DC1 Result:** ______ **DC2 Result:** ______ **DC3 Result:** ______ **DC4 Result:** ______ **DC5 Result:** ______

## D. Day-night, weather and readability (17.3 bullet 5)

The board's M4 (day-night and weather look) is the same area; these rows are the M3 sign-off items and may be recorded against it.

- [ ] **DN1 - Scrub with no pops.** At the lake, hold **F8** and watch a full day (about 15 seconds). No visible pop in light or fog at 8:00 AM, 5:00 PM (17:00) and 8:00 PM (phase starts at day fractions 0.10, 0.55 and 0.70) or at the sun/moon flip. Dusk is warm and night is cool. Known: rolling past minute 1200 (2:00 AM) pops once, and that is dev-only (section H.3).
  - **Record the pops you see (time and what):** ______
  - **Result:** ______
- [ ] **DN2 - Season re-tint.** In the panel switch Spring, Summer, Autumn and Winter. The trees, grass and props re-tint without flattening their texture (M3-D23). Spring shows blossoms, autumn mixed colours, winter bare trees with snowy pines.
  - **Result:** ______
- [ ] **DN3 - Night readability at 25 m.** Clock at night. From the bank, the bobber and the landing marker at about 25 m and the dock edge are all distinguishable. If not, the fix is data (Night `toonAmbient` or `moonIntensity`), not code.
  - **Result:** ______
- [ ] **DN4 - Maximum fog readability at 25 m.** Panel weather = Fog (daytime, then night). The same three things are distinguishable at 25 m.
  - **Result:** ______
- [ ] **DN5 - Rain.** Weather = Rain. It reads as rain at 1080p, the patter sits under the wind, and the 4 second fade in and out has no pop. Note the frame time under full rain (reduce `MaxRainRate` if rain costs more than about 10%).
  - **Record:** frame time clear / rain: ______
  - **Result:** ______

## E. Journal and save (17.3 bullet 7)

The journal path is `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\meta.json` (README, "Journal / meta.json"). The T21 manual list (plan `2026-10-13-m3-plan-journal-ui.md`, T21 step 8) and T20's three checks are folded in here.

- [ ] **JE1 - Menu journal.** The main menu shows "Journal n/25". The Journal button opens the journal with the cursor free. Gamepad navigation moves across the cards and filters only. B and Esc close it and focus returns to the Journal button.
  - **Result:** ______
- [ ] **JE2 - Levels 0, 1, 2 and 3 on one species.** Fresh profile (delete `meta.json`): all 25 cards are at level 0 (black silhouettes, "???", "Maybe at: Lake Dock" or "Harbour Pier"). Land a bluegill: a "New in the journal" toast, its card shows level 2 (name, "Seen at: Lake Dock (Reeds, Open Water)", the glyph row) and `meta.json` holds the species with Caught 1 and the bests. Land it two more times: level 3 with the weight range and your best.
  - **Result:** ______
- [ ] **JE3 - A lost fish.** Snap a fish. For that species only the card goes to level 1 (Seen +1 and nothing else).
  - **Result:** ______
- [ ] **JE4 - J with the rod waiting.** With the rod waiting press J: the journal opens, the bobber stays out and you cannot move. When the fish bites the journal closes itself and the fight starts normally (also with F7 twitchy hook, where "Click!" must stay reachable).
  - **Result:** ______
- [ ] **JE5 - J does nothing when it should not.** Press J while charging a cast, fighting a fish, swimming and climbing a ladder: nothing opens.
  - **Result:** ______
- [ ] **JE6 - Biting now.** With the panel change the season, weather and clock band and see the badges follow the rules. Switch rods with F4 and see a rod-gated species (for example `glimmer_eel`, rod 1, Summer Dusk Rain) appear and disappear. "Biting now only" shows only badged cards. The badge reads against the HUD clock and the WORLD line.
  - **Result:** ______
- [ ] **JE7 - Layout.** At a 1280 x 720 Game view with UI scale 150%: nothing clips and the grid scrolls. The filters, the completion count and the Close button are all reachable.
  - **Result:** ______
- [ ] **JE8 - Recovery from a corrupt file.** Quit Play. Truncate `meta.json` by hand and enter Play: the Console shows `FG SAVE loaded Backup` and the journal is intact.
  - **Result:** ______
- [ ] **JE9 - Newer schema.** Set `"schemaVersion": 2` by hand in `meta.json`. Enter Play: the Console warns that the journal is read-only this session. Land a fish and quit: the file is unchanged. Restore it to 1 afterwards.
  - **Result:** ______

## F. Balance and tuning items

All numbers were produced by the full `test-edit` run of 2026-10-13 on `eaab0aa`, after the batch A fight retune (section F.5). The raw prints are in `balance-output.txt`, kept in the implementer's scratchpad. The V5 cycle times use the Rhythm bot's fight medians, which the retune changed, so these tables replace every older V4/V5 number in the plans.

### F.1 Coverage and variety (T11)
- **V1:** dead contexts 0 of 216 for the lake and 0 of 216 for the harbour.
- **V2:** across the 144 (spot, season, band, weather) contexts each has 3 to 6 species (3 in 64 of them, 4 in 62, 5 in 16, 6 in 2) and the top species share never passes 47.2% (`yellow_perch`, lake Spring afternoon fog; the bound is 50%).
- **V3:** every species passes somewhere at rod 3. The lowest count is `sunny_trout` with 4 contexts; `bruiser_tuna`, `glimmer_eel`, `grumpy_puffer`, `lanternfish` and `storm_ray` have 8.
- **13.3 variety (the ruled wording: at least one species eligible in at most two adjacent bands; the literal "exactly one band" count is printed and is 0 everywhere):**

| Spot | Season | Eligible | Narrow-window species | Morning vs Night difference |
|---|---|---|---|---|
| harbour_pier | Spring | 7 | grumpy_puffer, moon_squid | 5 |
| harbour_pier | Summer | 8 | bruiser_tuna, grumpy_puffer, moon_squid | 6 |
| harbour_pier | Autumn | 10 | bruiser_tuna, lanternfish, moon_squid, storm_ray | 8 |
| harbour_pier | Winter | 7 | lanternfish, moon_squid, storm_ray | 5 |
| lake_dock | Spring | 8 | sunny_trout | 5 |
| lake_dock | Summer | 8 | glimmer_eel, sunny_trout | 6 |
| lake_dock | Autumn | 8 | glimmer_eel | 6 |
| lake_dock | Winter | 6 | frost_char | 3 |

### F.2 V4 expedition simulation (7 days, ~60 casts a day, 400 runs per cell, neutral bait)
- **Per-species catches per run:** 70 (spot, season, species) cells, all pass. Tightest cell per rarity (catches per run against the bound): Common 3.38 times its bound (`mirror_minnow` lake Autumn 23.69 against 7); Uncommon `sunny_trout` lake Spring 5.11 against 3.5 (1.46 times, the tightest cell in V4); Rare 3.63 times (`bruiser_tuna` harbour Autumn, 2.54 against 0.7); Legendary 2.15 times (`old_barnacle` 0.86 against 0.4).
- **Junk:** 14.0% of catches in every cell, worst single day 14.2% (bound 20%).
- **Top species share of a run:** harbour 28.8-32.6% (`grumble_mackerel`); lake Spring 39.2%, Summer 30.8%, Autumn 31.7% and Winter **45.1%** (`yellow_perch`; the bound is 50%, so lake Winter is the tightest share).

### F.3 V5 economy
- **Best-for-coins:** no sub-area wins more than 12 of 18 contexts (66.7%; the cap is 15 of 18, 85%). Harbour shallows win 0 of 18 in Spring, Summer and Autumn (9 in Winter); the lake's open_water wins 0 to 2 of 18.
- **Rhythm-bot fight medians (seconds; the 200-fight cells; `F8` = the pre-retune baseline of the M2 acceptance doc, section K, for the two rods it measured):**

| Profile | Twig | Handmade | Carbon | Deep-sea | Twig / Handmade before the retune |
|---|---|---|---|---|---|
| Steady | 13.9 | 13.7 | 13.1 | 13.0 | 14.4 / 14.0 |
| Darter | 15.5 | 13.2 | 12.2 | 12.1 | 21.6 / 19.1 |
| Thrasher | 17.2 | 13.8 | 12.5 | 12.4 | 21.7 / 17.8 |
| Runner | 21.6 | 20.4 | 16.5 | 16.4 | 30.5 / 26.2 |
| Bulldog | 20.5 | 19.3 | 15.0 | 14.1 | 33.7 / 30.9 |

All cells land 100% of fights.
- **Fightability (Rhythm bot on Twig, rod-0 species, bound 90%):** all five profiles land 100% (n = 200 each).

### F.4 Quota calibration input for M4 (per spot and season, coins per day on the default rod path: Twig days 1-2, Handmade 3-4, Carbon 5-6, Deep-sea 7)

| Spot | Season | Day 1 | Day 4 | Day 7 | Day 7 / day 1 |
|---|---|---|---|---|---|
| harbour_pier | Spring | 232.4 | 254.5 | 264.4 | 1.14 |
| harbour_pier | Summer | 228.2 | 257.8 | 294.5 | 1.29 |
| harbour_pier | Autumn | 221.5 | 271.8 | 306.2 | 1.38 |
| harbour_pier | Winter | 224.3 | 286.4 | 298.2 | 1.33 |
| lake_dock | Spring | 196.8 | 202.6 | 253.2 | 1.29 |
| lake_dock | Summer | 193.1 | 216.1 | 232.1 | 1.20 |
| lake_dock | Autumn | 213.3 | 235.1 | 247.7 | 1.16 |
| lake_dock | Winter | 241.5 | 250.2 | 287.7 | 1.19 |

Expected earnings grow only 1.14-1.38 times from day 1 to day 7 (the day 4 / day 1 ratio is 1.03-1.28). The spec's quota curve assumes about 2.35 times. **This is an M4 quota-calibration item, not an M3 failure** (it was already known; the print exists for it).

### F.5 The batch A fight retune (user playtest 2026-10-13; spec R77, 9.12 "User playtest")
- **Applied:** an idle escape after 30 s with no reel and no steer input (`idleEscapeTime`; `fightTimeout` 120 s stays the overall cap); an opening reel surge (`reelOpenMult` 2.5 falling to 1 over 0.8 s, `reelOpenSeconds`; the back pull ramps in over the same window); the terminal beat cut from 1.5 s to 0.6 s with a held aim carried into Readying; and shorter fights through `tireRunFull` 20 -> 12, `tireSteerRate` 0.8 -> 1.2, `backMoveSpeed` 1.3 -> 1.0 and `reelInMult` Darter 1.0 -> 1.3, Thrasher 1.0 -> 1.15, Bulldog 0.9 -> 1.0, Runner 0.9 -> 0.95. The strain path is untouched (the Greedy bot's snap times are identical).
- **Effect:** the cuts in section F.3 (Bulldog -38/-39%, Darter -31/-28%, Thrasher -22/-21%, Runner -22/-29%, Steady -2/-3%, Handmade / Twig).
- **The re-run V4/V5 and fightability tables above are the post-retune numbers**, and every report still passes. V5 had been sitting near a cliff on `mirror_minnow` and `red_snapper` (handoff note); it did not flip.
- **Option B (a deeper cut) is NOT shipped and waits for your decision (section I, Q1).** It changes `tireReelFull` 3.5, `tireReelMaxMult` 3.5, `tireCounterRate` 1.9, `backMoveSpeed` 1.1, Bulldog `reelInMult` 0.8 and a 3 x 1 s surge. Rhythm medians Handmade / Twig would be Steady 10.4 / 11.0, Darter 11.8 / 13.7, Thrasher 11.8 / 14.8, Runner 16.2 / 18.3 and Bulldog 17.9 / 19.4. It needs the Reckless bot's pins loosened to Steady Handmade 6%, Darter Handmade 67% and Thrasher Handmade 74%: a player who steers well and never eases would find it too easy.
- **Informational print, not asserted:** `FG FUN ... rec>=1` (near-snap recoveries by the Rhythm bot on Handmade) reads 0.0% for every profile against the fun-gate line of 40% for Bulldog, Thrasher and Runner. The Rhythm bot never lets the strain climb, so it never recovers; this has nothing to do with M3. The human fun gate (board G1) is where that is measured.

### F.6 Tuned deviations and tuning items
- **`EcologyTuningItems`:** empty in `EcologyBalanceReports`, `EcologyCoverageTests` and `EcologyFightabilityTests`. `FishingBots` `TuningItems` is empty too.
- **`EcologyTunedDeviations` (data retunes from the T12 balance reports, approved by the controller; spec 12.6 notes them):** lake `mirror_minnow` spawn weight 10 -> 4 (lake Winter deep_water won 18 of 18 on V5) and harbour `red_snapper` 12 -> 18.
- **`FishingContentTests.TunedDeviations`** (the batch A values above): `tuning.backMoveSpeed` 1.0, `tuning.tireSteerRate` 1.2, `tuning.tireRunFull` 12, and the four `reelInMult` values.
- **Value change, not a deviation:** `minLandDepth` is 0.5 m (was 0.8 m) so the reeds band is castable; spec R75 and 9.3/9.12 carry it.

### F.7 Skeleton section 8 risks and status

| # | Risk | Status |
|---|---|---|
| 1 | Look-dev dependency (R74) | Resolved: R74 adopted (T4), all 25 species and the harbour are in R74 style. |
| 2 | The fight rework | Resolved: R72 landed; batch A retuned it and the reports were re-run (F.5). |
| 3 | First in-scene NetworkObject | Resolved: the two-window check W1 passed on 2026-10-13; `GlobalObjectIdHash` is non-zero in both scenes. Scenes are restored whole (README). |
| 4 | Reeds depth conflict | Resolved: `minLandDepth` 0.5; the lake has 294 reeds cells. |
| 5 | Fightability and balance on the Rust-faithful baseline | Resolved: all pass, no tuning items. |
| 6 | Two players rolling one special | Covered by the catch-limit reservation tests; ME2 and board P4 check it live. |
| 7 | Map switching is dev-only | Still true. The slot loadout, bait stock, creel and pity streak reset on a switch; M4 must move them into session state (section H.3). |
| 8 | Scope / hotspot cut-line | Not needed: hotspots shipped. |

## G. Art status per species (R74)

All 25 species have a Blender model wired on their species asset (the `model` field resolves to an FBX under `Content/Models/`); there are **0 fallback rows**. The source column says where the model came from: "M2 restyled" = the M2 five, restyled in the R74 adoption (bluegill in the look-dev commit, the other four in `3de9de1`) and given an AO re-bake in `6ee38d3`; "T23 batch N" = made in `Art/Blender/fishgame_m3_fish.blend` (batches 1-4, commits `9f3dfd9`, `5567ffa`, `33b1bcf`, `bd1cf10`) and imported in `9ae35af`. Skin layers are layers of `T_FishSkinArray.png` (4 x 8 sheet, layer 0 white). The verdict column starts as pending (user) for all rows: no verdict has been recorded on the board for any fish yet.

| # | Species id | Model file | Source | Skin layer | Verdict |
|---|---|---|---|---|---|
| 1 | bluegill | Fish_Bluegill.fbx | M2 restyled | 1 | pending (user) |
| 2 | yellow_perch | Fish_Perch.fbx | M2 restyled | 2 | pending (user) |
| 3 | grumpy_carp | Fish_Carp.fbx | M2 restyled | 3 | pending (user) |
| 4 | sunny_trout | Fish_Trout.fbx | M2 restyled | 4 | pending (user) |
| 5 | mudcat | Fish_Catfish.fbx | M2 restyled | 5 | pending (user) |
| 6 | mirror_minnow | Fish_MirrorMinnow.fbx | T23 batch 1 | 6 | pending (user) |
| 7 | bucketmouth_bass | Fish_BucketmouthBass.fbx | T23 batch 1 | 7 | pending (user) |
| 8 | silver_mullet | Fish_SilverMullet.fbx | T23 batch 1 | 8 | pending (user); see the icon colour question, Q5 |
| 9 | lake_pike | Fish_LakePike.fbx | T23 batch 1 | 9 | pending (user) |
| 10 | frost_char | Fish_FrostChar.fbx | T23 batch 1 | 10 | pending (user) |
| 11 | glimmer_eel | Fish_GlimmerEel.fbx | T23 batch 2 | 11 | pending (user) |
| 12 | sleepy_sturgeon | Fish_SleepySturgeon.fbx | T23 batch 2 | 12 | pending (user) |
| 13 | gilded_carp | Fish_GildedCarp.fbx | T23 batch 2 | 13 | pending (user) |
| 14 | old_barnacle | Fish_OldBarnacle.fbx | T23 batch 2 | 14 | pending (user) |
| 15 | pier_sardine | Fish_PierSardine.fbx | T23 batch 3 | 15 | pending (user) |
| 16 | ice_herring | Fish_IceHerring.fbx | T23 batch 3 | 16 | pending (user) |
| 17 | grumble_mackerel | Fish_GrumbleMackerel.fbx | T23 batch 3 | 17 | pending (user) |
| 18 | gawping_gurnard | Fish_GawpingGurnard.fbx | T23 batch 3 | 18 | pending (user) |
| 19 | red_snapper | Fish_RedSnapper.fbx | T23 batch 3 | 19 | pending (user) |
| 20 | grumpy_puffer | Fish_GrumpyPuffer.fbx | T23 batch 3 | 20 | pending (user) |
| 21 | moon_squid | Fish_MoonSquid.fbx | T23 batch 4 | 21 | pending (user) |
| 22 | sneaky_flounder | Fish_SneakyFlounder.fbx | T23 batch 4 | 22 | pending (user) |
| 23 | bruiser_tuna | Fish_BruiserTuna.fbx | T23 batch 4 | 23 | pending (user) |
| 24 | storm_ray | Fish_StormRay.fbx | T23 batch 4 | 24 | pending (user) |
| 25 | lanternfish | Fish_Lanternfish.fbx | T23 batch 4 | 25 | pending (user) |

**Counts:** models 25, fallback 0. A fallback row would not be a failure (the procedural fish is kept for junk and missing art, contract 2.8 #28). The flat fish (flounder, ray) are side-flattened discs and the moon squid's arms point toward +Z (T23 notes).

- [ ] **AV1 - Fish models.** Pictures are in `docs/superpowers/handoff/img/2026-10-13-m3-fish-batch{1..4}.png` and `...-m3-fish-ingame.png`. Keep or adjust each fish (board M1). **Record per fish:** ______ **Result:** ______
- [ ] **AV2 - Harbour props and look (T17-B).** Ten Blender FBX under `Content/Models/Harbour` with `harbour_manifest.json`, generated by `Art/Blender/fg_harbour.py`: pier deck, T-head, posts, ladders, props, dressing (boats, buoys, nets), breakwater boulders and rocks. Judge them in the Scene view and in play (board M6, and HP1 below). Expected: smooth, bevelled pier and props with wood grain; sand beach rising into grass; boulder breakwater; no pink (missing shader) or black (missing globals) surfaces; the sea does not end abruptly inside the fog; it reads as the same game as the lake. Note the frame time at the pier end (the scene has 186,844 triangles). **Result:** ______
- [ ] **AV3 - Journal icons.** Baked from the real models by `FishIconBaker` (contract M3-D96). Approve the icon style or ask for changes, and answer the Silver Mullet belly colour (board M2). **Result:** ______

## H. Carried manual checks, rulings and known gaps

### H.1 Review rulings and merged-task notes (from the controller notes)

**Contract and plan rulings in force.**
- All collected change requests were accepted as proposed (the primary options): CCR-D1 to D5, CR-1 to CR-7, CR-B1, CR-B2, CCR-A1 to A7, CCR-F1 to F4, E-CCR-1 to E-CCR-5 and G-CR1 to G-CR5. Notable consequences: `PlaceholderRoller`, `RollRequest` and the species `MinBaitRating`/`MinDepth` fields are gone (the bare hook can catch any species); the seasonal tint is the palette swap on textured albedo, not a shader term (F1); the harbour is registered only once `Spot_HarbourPier` is in the build order; the skin sheet is 4 x 8 with a 4096 cap for that array only (G-CR5).
- Spec deltas M3-D2, D3, D6, D19, D53, D65, D66 and D74 are **applied** (spec R75). The earlier "pending" notes in the controller notes are stale. The user's fixed-dev-seed answer is also recorded there (M3-D57).
- User rulings: T17-B (2026-10-14: the harbour's art is modelled in Blender; code keeps terrain, water, colliders, hooks, stand points and the bake; free assets allowed once verified, source and licence recorded); "Full seasons" for trees (pines evergreen with snow, round trees spring blossoms, autumn mix, winter bare).
- The grid, hash and bake order: `SaveScene -> FinalizeAfterSave -> Physics.SyncTransforms -> BakeOpenScene` (Opus review of T13/T14/T16, F1 and F8 resolved). A Spot scene is restored or committed as a whole file.

**Review outcomes and their follow-ups.**

| Review | Outcome | Follow-ups |
|---|---|---|
| Opus on T0-T3 | approved with fixes | The online-cancel race (A-Q3) and the cancel test wording: applied (`afd3513`). Spec deltas D2/D3/D6/D19/D53: applied. |
| Sonnet on T5-T8, T9-T12 | approved with fixes | Applied in the cleanup round (`a362943`): `BuildEcology` names the missing zone or tuning, `SceneNames.SpotHarbourPier`, the shared asset-path guard, second `WorldState` inert, `SetPaused(Now)`, test hardening. The `EcologyBalanceReports` helpers are now private. |
| Opus on T13/T14/T16 | approved with fixes | F1 bake order: applied. F2 reservation release on `OnDisable`: applied with the T15 session switch. F4 to F7: applied. |
| Opus on T18/T19 | approved with fixes | Applied in the cleanup round (`0519828`): wind settles exactly, finished fade bit-exact, `FindLoop` skips `Loop_Rain_Patter`. The day wrap at minute 1200 still pops: dev-only in M3 (H.3). |
| Seasonal trees | safe to merge | Zero-normal root fix in `SmoothMeshBuilder.Radial` (`20bc0a7`), palette-globals TearDown and a lake collider-set guard (`702ce4f`): applied. |
| M2 merged to main | `235d460` | Pre-merge check: EditMode 976 (+1 skip), PlayMode 83. |

**T15 merged (Opus high review: approved with fixes `4caca5d`).**
- Owed manual checks: the cast marker turns red over a dock-shadowed cell the grid calls land (HP2); the F3 `HOST casts=` count keeps counting across a map switch (DV9).
- T24 follow-up (done): a PlayMode test that drives the host at a grid-land cell and asserts `NotWater` with the counter unchanged.
- M4: a map switch resets slot loadouts (rod, bait, stock) and the pity streak, which live in the per-scene `FishingHostLedger`. Real map travel must move them into session state.

**T17-B merged (harbour in Blender).**
- Blender phase 1 (`m3-harbour-art`): `Art/Blender/fg_harbour.py` (mirrored by hand against the layout numbers, with a comment pointing at them), `fishgame_harbour.blend`, 10 FBX in `Content/Models/Harbour` and `harbour_manifest.json`; encoding is the fish's (second UV map `Layer` = palette slot and texture layer, vertex colour = half-scale baked AO, no materials; rocks and boulders `_Tri` use `M_Toon_Tri`).
- Phase 2 (code builder): terrain, water and skirt, colliders, hooks, `World`, the 11 stand points and the bake. The harbour grid SHA starts `8c32acab`, the lake grid `52b0a39c`. Bake counts: shallows 507, channel 1016, open_sea 1502. `HarbourPier_Rebuild_ByteIdenticalMeshes` covers only the code meshes; `HarbourPier_ModelBoundsMatchLayout` keeps the FBX in step with `HarbourPierLayout`.
- Manifest note (follow-up): the breakwater boulders overhang the x 28..32 collider box by up to about 1.9 m at the waterline; check that swimmers cannot pass into rock (HP1).

**T24 merged (Sonnet review: approved).**
- `EcologySoloTests` has the seven plan tests plus `EcologySolo_GridLandCell_HostRejectsNotWater`, which uses a harbour breakwater sliver cell (the lake has none). Its discrimination was shown by reading the code only: a mutation run was refused by the permission system and not attempted again.
- Known gaps: the Esc edge detection in `UiFlowController.Update` (`HandleBack`) has no test (KP1); optional tightening of test 1 (snapshot eligibility for sub-area 2 only) and `FishingSoloTests.ResetDebugSettings` should also reset Luck.

### H.2 Carried manual checks (new IDs; board M1-M6, B2 and P1-P6 stay where they are)

The T20, T21 and T22 plan lists are folded into sections E (JE1-JE9), B and these rows.

- [ ] **DV1 - Clock.** Solo: the HUD clock reads 6:00 AM at the start, steps every 10 in-game minutes and shows the season and weather glyphs and names. Pause (Esc): time freezes; resume: it continues with no jump (R34). **Result:** ______
- [ ] **DV2 - F3 cycle.** F3 -> overlay (with the WORLD line) -> panel (cursor free, player frozen) -> F3 again: everything off. Esc and Close also close the panel. Opening the journal while the panel is open is refused, and the reverse. **Result:** ______
- [ ] **DV3 - Panel rows (host).** Each season button changes the HUD glyph, the WORLD line and the palette. The time slider jumps the clock and the sky. Weather Track/Clear/Rain/Fog changes the WORLD line and the look. **New day** resets to 6:00 AM with day + 1. The dev-clock toggle stops and restarts time. **Result:** ______
- [ ] **DV4 - F8.** Hold F8: the clock runs x60; release: normal. Alt-tab while holding: the scrub stops. **Result:** ______
- [ ] **DV5 - Hotspot button.** "Spawn hotspot at crosshair" over water shows the hotspot VFX there and on the WORLD line; over the dock it says "No water under crosshair". **Result:** ______
- [ ] **DV6 - Run seed.** Type a new seed (decimal or `0x`) and Apply before casting: the season and weather track change on the WORLD line. After one cast the row reads "Locked after the first cast". **Result:** ______
- [ ] **DV7 - Luck.** Luck 1.0: landed fish skew heavier over about 10 catches (F6 forced bite helps). **Result:** ______
- [ ] **DV8 - Rod tier.** Tier 3 while Idle: the LOADOUT line shows the Deep-sea rod. While Waiting: "Refused: cast active". **Result:** ______
- [ ] **DV9 - Map switch.** Load Map > Harbour Pier: the harbour loads, the player respawns, the WORLD line keeps day, season and cast counter, and `HOST casts=` keeps counting. Load Lake Dock back. **Result:** ______
- [ ] **HP1 - Harbour play-through.** Host solo, load the harbour from the panel: spawn on the pier; walk the ramp to the plateau and down to the beach; swim out, climb both ladders; walk onto the breakwater (and try to swim into it); cast from a beach point, the pier middle and the T-head (shallows, channel and open sea all give valid markers); ring the bell. Land a fish and check the journal's "Seen at" shows Harbour Pier. **Result:** ______
- [ ] **HP2 - Marker over a dock-shadowed cell.** At the lake, aim at water under or beside the dock edge where the grid says land: the marker turns red and a forced cast is refused with the bait unchanged (the T15 owed check). **Result:** ______
- [ ] **KP1 - Real key paths.** The automated tests drive the dev panel and journal through a fake input service; the real adapter paths are only manual. In a development player or the editor press J, gamepad Select, F3, F8 and Esc in each of: free walking, rod ready, rod waiting, a fight, a menu and the journal itself. Esc must close the journal or the panel first and must not open Pause underneath. **Result:** ______
- [ ] **NB1 - Non-development player build.** Build without "Development Build" (board B2 is the same build). The clock shows; F3, F8 and F4-F10 do nothing; no dev panel, no map switch, no exception in the log. J still opens the journal. **Result:** ______

### H.3 Known gaps and M4 items (accepted for M3)

- The Esc edge detection in `UiFlowController.Update` (T24 review) has no automated test.
- The real J, F3 and F8 key paths (KP1) are manual only.
- MPPM clients' read-only dev panel (ME6) is only exercised by code and fake-input tests.
- Non-development player build behaviour (NB1) is not exercised by any test.
- The harbour play-through (HP1) is manual; PlayMode only loads the map and lands a forced fish.
- Online cancel on a real join code shows "Cancelling..." for about a frame, then the menu with greyed buttons for up to 30 s (board P6).
- A join "snap" is really up to three snaps within about 0.5 s (light and fog, then palette and trees after at most 0.25 s, then wind), hidden behind the loading screen.
- The day wrap at minute 1200 pops (moon 0.25 -> 0, Night -> Dawn, cool -> warm, fog 0.014 -> 0.01). It is reachable only with the dev clock.
- **M4:** loadout (rod, bait stock) and the pity streak reset on a map switch, because the per-scene ledger owns them; the creel too. A real map travel needs them in session state.
- **M4:** quota calibration (section F.4: earnings grow only 1.14-1.38 times in 7 days, the spec assumes about 2.35 times).
- **M4:** the journal's level 1 shows the species' sub-areas in the spots seen, not the sub-areas actually seen; a v2 save could record the latter (G open question 1).
- **M4:** the R76 river run, starfish god camp, boat ride, mafia escalation, cutscenes and the Hub's map board.
- Default solo session look (dawn, autumn, fog for about 6 minutes, 18 s sun fade-in) is the fixed dev seed's; board M5.

### H.4 Test-environment note

The first full `test-edit` run after the builds failed 7 tests in `FishingContentBuilderTests` (`OneTimeSetUp`: "Cannot open file ... Tests/__Temp/FishingContentBuilderTests/Fishing/Rods.meta for write"), and the immediate rerun passed 1378 tests with nothing changed. If it returns, rerun; if it repeats, look at antivirus or an editor holding `Assets/_Project/Tests/__Temp`.

## I. User actions due and open questions

**Actions.**
1. Run this checklist: sections B, C, D, E, G and H.2.
2. The MPPM session (section B), with the Network Simulator on the clients.
3. Approve, adjust or defer the fish art (AV1), the harbour (AV2) and the journal icons (AV3).
4. Decide merging `m3-fish-ecology` into `main` after the final branch review (only with your consent).
5. Remaining older board checks: the M2 acceptance items and the re-check rows (RL1, RS1, RF1, RL2, RG1, H1, J1, DP1).

**Decided (no action):** the fixed dev run seed `0xF15F15F1` (M3-D57; spec R75).

**Open questions.**
1. **F10 option B.** Keep the shipped fight retune (option A, section F.5) or take the deeper option B? Decide after you have felt A on the new numbers (board F10).
2. **Menus protecting the fish in multiplayer.** Should an open menu (Pause, journal, dev panel) protect a hooked fish when other players are in the session? Today the world never stops online (R34), the journal closes itself when a bite arrives, and the owner is never left fighting behind a modal.
3. **Aim-toggle carry.** With the aim-toggle setting on, should the latched aim carry across the 0.6 s terminal beat the way a held aim does (R77, S10)?
4. **Top-out direction on ladders.** The arcade climb (batch C) leaves through the top with a small hop up (2.6 m/s) and forward (3.0 m/s) from the commanded climb direction, resting about 0.8 m inboard. Is that direction right, or should it follow the look direction?
5. **Silver Mullet icon.** The icon paints the belly in the accent colour (slate), though the real belly is white. Keep or change (board M2)?
6. From `docs/superpowers/handoff/CURRENT.md` on `main`, still open on the board: fish keep/adjust (M1); the fight feel verdict (K.3, F10); the bad-luck nudge keep/tune/remove (N1); deck-edge grab (L3); the default test-session look (M5); winter tree colliders (M3: bare winter trees keep their round collider, so you can bump into "air"); the online-cancel message (P6).

(Questions 2 to 4 are worded from the controller's one-line list and the shipped behaviour; correct the wording if the intent differs.)

## J. Definition of done (spec 17.3)

M3 is done when rows 1-12 of section A are met, or a miss is recorded as an approved tuning item (row 5; there are none) or a deferred art item (row 12; none are fallbacks). The branch then goes to the Opus (xhigh) whole-branch review (T25.6), one fix wave, the final `check`, your run of this document, and the merge into `main` with your consent.

### Branch-review hand-off (controller)

- **Scope:** `git diff <base>...m3-fish-ecology` and `git log <base>..m3-fish-ecology`, with `<base>` the commit the branch was cut from (`d5f980a` merged `m2-fishing-core`; M2 is now on `main` as `235d460`).
- **Inputs:** the contract, the skeleton, the seven section plans, this document, the controller notes (section H.1), the scripted checks (forbidden identifiers, BOM and non-ASCII scan, owned files vs contract 12 and 2.6, test names vs contract 10, LFS pointers for every binary) and the risk status (F.7).
- **Focus (T25.6):** contract conformance; host-only ecology and no client value in a roll; determinism through `EcologyStreams`; the in-scene `WorldState` (whole-scene commits, non-zero hashes, late-join state, despawn); owner and host gates agree and the day-over boundary; catch-limit reservations; the textured tint and no per-frame allocation; journal and save robustness (never throws into gameplay, never overwrites a newer or unreadable file, local only); UI never writes gameplay state except the dev-panel host calls (M3-D51) and dev surfaces gated by `Debug.isDebugBuild`; PlayMode warning-freedom, the fallback fish and the LFS footprint.
- **Output:** findings by severity with `path:line`, evidence and a proposed fix; then one fix wave (Sonnet; Fable escalates), re-reviews per the skeleton tiers, the final `check`, your run of this checklist, and the merge only with your consent.
