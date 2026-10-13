# FishGame M3 - Interface Contract (Fish ecology)

- **Date:** 2026-10-13 (written 2026-10-12, while the M2 rework finishes). Branch `m3-fish-ecology`, cut from `m2-fishing-core` (or `main` after the M2 merge) once the entry gate in section 1.2 holds.
- **Scope:** Milestone M3 (spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` section 17.3, the M3 row of section 5, and every section it cites: 4.3, 4.6, 4.7 #8, 9.3, 9.4, 9.8, 9.9, 10.2-10.4, 11.1, 11.3, 11.4, 11.7, 12 (all), 13.3, 14.2 (clock replication), 18 V14, R1, R5, R17, R23, R32, R33, R50, R57, R67, R72, R73, R74).
- **Builds on:** the M2 contract `docs/superpowers/plans/2026-10-11-m2-interfaces.md` ("C2" below) in its end state after the M2 rework (R70 ladders, R72 fight, R73 water, R74 look-dev adoption). Every C2 name stays valid unless this file says otherwise. Section 13 here lists the rulings (M3-Dn) and the spec deltas; section 14 maps the M2 carry list.
- **Status:** binding for every M3 task (T1-T25 in `docs/superpowers/plans/2026-10-13-m3-plan-skeleton.md`; note the serial order runs T16 before T15). If a section plan disagrees with this file, this file wins. If this file disagrees with the spec, section 13 records why. **Fight fields are owned by R72:** where the final R72 text (spec 9.5-9.13 and C2 row R72) differs from anything here, R72 wins.
- **Amended 2026-10-12:** the section authors' contract change requests (A1-A7, B1-B2, C CR-1..CR-7, D1-D5) were all accepted as proposed and are applied in place below (sections 1.3, 2.6, 6.2, 7.1-7.3, 7.5, 7.9, 7.10, 7.12-7.15, 9, 10, 11, 12); the rulings are M3-D55..D77 in section 13, with a resolution note after the table.
- **Amended 2026-10-13:** the contract change requests of sections E, F and G (E-CCR-1..5, CCR-F1..F4, G-CR1..G-CR5) were all accepted as ruled and are applied in place (sections 1.2, 1.3, 2.6, 2.8 #27, 5.3, 6.2, 6.4, 7.4, 7.11, 7.16-7.23, 10.4, 11.3, 12, 13); the rulings are M3-D78..D91 in section 13, with a second resolution note after the first. M3-D23 is rewritten in place by CCR-F1 (M3-D78). The R74 look adoption landed on this branch (d660ee9): the fish skin sheet uses layers 0-5, so the M3 species layers are 6-25 (M3-D91).
- **Audience:** section authors (Opus) and implementers (Sonnet/Haiku). Each brief carries section 2.8 and its own task block.

Every type, file, asset, constant, event, NetworkVariable and test that two or more tasks use is defined here once, with an owner task in brackets. A task may add private or internal helpers freely. It must not add, rename or remove anything public listed here, and it must not edit a file owned by another task except for the shared edits in section 2.6.

---

## 0. Contents

1. Baseline, entry gate and dependencies on the M2 rework
2. Conventions and global constraints
3. Execution order and dependency graph
4. Assemblies
5. Constants
6. Assets, scenes and hierarchies
7. Types by task (public surface)
8. Network surface and authority
9. Runtime flows
10. Test inventory (exact names; owners)
11. Validators, build checks and balance reports
12. Per-task file manifest
13. Rulings (M3-D1..D91) and spec deltas
14. M2 carry list (C2 16.1 and the M2 handoff "Later" list) folded into M3 tasks
15. Risks (contract-level; the skeleton has the plan-level list)

---

## 1. Baseline, entry gate and dependencies on the M2 rework

### 1.1 What exists (checked in code on 2026-10-12)

| Id | Fact | Source |
|---|---|---|
| B1 | `Core/Random/Rng.cs` streams are string ids: `Rng.Stream(runSeed, streamId, index)`. Spec 12.2 `StreamId.Weather` maps to the string constant `EcologyStreams.Weather` (5.2). | code |
| B2 | `Core/Fishing/FishRarity.cs` is `{ Common, Uncommon, Rare }`. Spec 12.2 calls the type `Rarity` and adds `Legendary`. M3 keeps the name `FishRarity` and adds `Legendary = 3` (M3-D2). | code |
| B3 | `FishSpeciesDef` has `minBaitRating` and `minDepth` (M2 `PlaceholderRoller` gates) and no `tags`. Spec 12.3 puts the gates on `SpawnRule` and adds `tags`. | `Data/Fishing/FishSpeciesDef.cs` |
| B4 | `BaitDef`/`BaitStats` have no `affinityTags`, `affinityMult`, `specialMult`. A new float field on an existing asset deserializes as 0, and `FishingContentBuilder` is create-if-missing, so the migration (T3) must write them (M3-D1). | code, C2 6.2 |
| B5 | `ContentRegistry.cs` is 430 lines, a `sealed` (not partial) class, `dataVersion = 1`, categories spots, rods, baits, fight_profiles, species, junk. | code |
| B6 | `ContentHash.Compute` does not reject duplicate category names (M2 handoff "Later" item). | `Core/Hashing/ContentHash.cs` |
| B7 | `SpotDef` has only `id, displayName, sceneName`. `SceneNames.BuildOrder = { Bootstrap, Menu, Spot_LakeDock }`; `SceneFlow` (App) loads `Spot_LakeDock` only. | code |
| B8 | `ArtPaletteDef` already holds `seasons[4]` (Spring..Winter seeded) and `phases[4]` (`DayPhaseAmbient`, starts 0 / 0.10 / 0.55 / 0.70). `PaletteGlobals` pushes `m2Season` and phase index 1 once on enable. | code |
| B9 | Lake layers: `Lakebed` Ground, `Shore` mesh + rocks + trees Shore, `Dock` (posts, rails, ramp) Structure, reeds Default, `Ladders/LadderL|R` LadderVolumes Default (R70), `WaterVolume` trigger on Water (the only Water-layer collider), `WaterVisual` mesh on Water with no collider. The Shore/Structure split of spec 12.5 therefore already exists; T16 verifies and pins it with a validator. | `LakeDockBuilder.cs` |
| B10 | Lake bowl: bed y = -6 x clamp01((50 - r) / 40); the shore mesh crosses y 0 at r about 50.17. Depth at shore distance s is about 0.15 x s, so the 0-5 m `reeds` band is at most about 0.72 m deep, below `minLandDepth` 0.8. **No reeds cast is valid today** (Q1 in the skeleton; M3-D7). | `LakeDockLayout.cs`, spec 9.3 |
| B11 | `FishingHostLedger` lives on the per-scene `FishingHost`; `HostCastCounter` restarts at every Spot load. With one run seed a map switch would replay catch seeds (M3-D17). | code |
| B12 | `PlayerIntent` has no `JournalPressed`, and neither has `PlayerInputFrame` (the router builds the intent from the frame), although the input asset has `Player/Journal` (J / Select). T21 adds the field to both (M3-D87). | `Core/Input/PlayerIntent.cs`, `Core/Input/PlayerInputFrame.cs`, `FishGameInput.inputactions` |
| B13 | The four M2 species models `Fish_Perch/Carp/Trout/Catfish.fbx` exist in `Content/Models/`; their species assets are deleted by the migration. `SpeciesSeed.ModelFile` picks the file by name, so the M3 roster can reuse them (M3-D33). | code |
| B14 | `AtomicFile` (Runtime) already does the spec 12.10 atomic write (`.tmp`, `Flush(true)`, `File.Replace` with `.bak`, else `File.Move`). Settings use `JsonUtility`; Newtonsoft is auto-referenced by Runtime. | `Runtime/Settings/AtomicFile.cs` |
| B15 | No in-scene-placed NetworkObject exists yet. `WorldState` is the first (spec 4.6). | code |

### 1.2 Entry gate (M3 implementation starts only when all hold)

1. **R72 fight rework F0-F8 committed** (plan `docs/superpowers/plans/2026-10-12-m2-rework-fight.md`). M3 must not redefine any R72-owned surface: `FishFightProfile` and `FightProfileDef` fields, `FishingTuning`/`FishingTuningDef` fight fields, `FightSim`, `FishingTestData` fight values, the bots in `Tests/EditMode/Tuning/FishingBots.cs`, `CatchValidator.MaxReelSpeed` (it indexes the rolled species' profile, so the M3 roller must keep `FishingRoll.Catch.ItemIndex` a species index), `FishingConstants` fight constants, `landDistance` 1.0.
2. **R70 ladders L1-L9 committed** (plan `2026-10-12-m2-rework-ladders.md`; at the time of writing L1-L6 are in). In particular L7: `Exits/Ramp` (ramp foot) on layer Default and `RampAssist`.
3. **R73 water committed** (`9120898`, done).
4. **R74 look-dev approved and adopted (T4): DONE (adopted on `m2-fishing-core`, style guide commit d660ee9).** Only the art-dependent tasks (T17, T18, T19, T21, T23) waited for it (skeleton section 3). Core, data, host and the lake world/bake task T16 do not: the bake reads colliders only, and T4 preserves T16's hooks in `LakeDockBuilder` whichever lands second.
5. M2 acceptance rerun by the user, or explicitly deferred by the user.

### 1.3 Dependencies on the rework plans (what M3 reads from them)

| From | What M3 relies on | Used by |
|---|---|---|
| R72 | Species reference the five profile assets by id only; weight does not enter the fight; fight length depends on the profile, not the species. | T10 seeds, T11 fightability |
| R72 F4 | Bots `Greedy / Reckless / Pump / Rhythm`, realistic casts (10-14 m), game-like weight skew. M3 fightability uses the **Rhythm** bot with the Twig rod. The bot members `BotKind`, `FightRecord`, `SeedFor`, `CastFor` and `RunFight` are `internal` (applied by R72 F4, no behaviour change; M3-D60), so M3 tests in `FishGame.Tests.EditMode` call them and no M3 task edits `Tuning/FishingBots.cs`. | T11, T12 |
| R72 F8 | Measured fight medians per profile x rod for Handmade and Twig (the condensed bot table in spec 9.6/9.12). F8 covers only those two rods, so T12 measures the Rhythm-bot fight medians itself on all four rods and prints Handmade and Twig next to F8's numbers as a check; V5 cycle times use T12's own medians (M3-D39, M3-D64). | T12 |
| R72 F2 | Host Landed floor `CatchValidator.MaxReelSpeed(rod, profile, tuning)`; owner `FightTimeout` 120 s accepted only on the host clock. Unchanged by M3. | T13 |
| R70 | Ladders are `LadderVolume` triggers on Default; nothing but `WaterVolume` may carry a collider on layer Water (CastProbe accepts the first Water-layer hit as the surface: ladders plan F3). The harbour uses `LadderVolume` for its ladders and a walkable beach slope instead of a `WaterExit`. | T16, T17 |
| R73 | Water shader reads `_SkyTop`, `_SkyHorizon`, `_ToonAmbient`, fog. The DayNightController drives exactly these globals. | T18 |
| R74 | Material and texture standard, style guide `docs/superpowers/research/2026-10-12-art-direction-lookdev.md` (written by the look-dev branch `m2-lookdev`). All M3 art (25 species models, harbour props, journal icons) follows it. The seasonal tint and day-night lighting must apply ON TOP of textured albedo (M3-D23: under the adopted standard the palette season swap IS that tint, M3-D78). The fish skin array holds layers 0-5 after the adoption (0 white, 1 bluegill, 2-5 perch, carp, trout, catfish; style guide 2.2); M3 species take layers 6-25 (M3-D91). | T4, T17, T18, T21, T23 |

---

## 2. Conventions and global constraints

C2 sections 2.1-2.7 apply with these deltas.

### 2.1 Branch and commits
- Branch **`m3-fish-ecology`**. Commit at each task's commit step only. Never force-push. Never `git add -A` / `git add .`.
- Message: `git commit -m "m3(<area>): <summary>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"` (trailer names the model that wrote the change; Haiku tasks `Claude Haiku 5.5`; controller-made art the controller's model).
- `<area>`: `carry, migration, core, resolver, grid, weather, hotspot, journal, daynight, data, content, coverage, balance, ledger, world, net, fishing, lakedock, harbour, render, weatherfx, audio, save, ui, dev, models, integration, docs`.
- FBX under `Content/Models/` and any texture formats the R74 standard adds are LFS (check `git lfs ls-files` lists every new binary before committing; K17 LFS quota 1 GB - report the total size of new binaries in each art commit).

### 2.2 Namespaces and folders
No sub-namespaces (M1 rule). New folders:
```
Assets/_Project/
  Core/Ecology/            T3 (FishTags), T5-T8
  Core/Journal/            T8
  Core/Render/             (exists) T8 adds DayNightMath, SilhouetteMath
  Data/Ecology/            T9
  Content/Ecology/{Zones,Weather,ShoreGrids}/   T10 (generated), T16/T17 (grid bake writes values)
  Gameplay/World/          (exists) T14 WorldState, DevWorldDriver, wire structs; T16 StandPoint
  Fishing/Journal/         T20 JournalRecorder
  Presentation/World/      (exists) T18 DayNightController, T19 WeatherFx, HotspotFx
  Runtime/Save/            T20 SaveService
  UI/Journal/              T21
  UI/World/                T22 ClockHudPresenter
  UI/Dev/                  T22 DevPanelPresenter
  Editor/Builders/Ecology/ T3, T10, T16
  Editor/Builders/Harbour/ T17
  Tests/EditMode/Ecology/  T5-T12 (EditMode tests may live in this sub-folder; namespace stays FishGame.Tests.EditMode)
  Tests/EditMode/Balance/  T12
```

### 2.3 Hot files and the hook rule
C2 2.3 applies. M3 hot files: `ContentRegistry.cs`, `ContentBuilder.cs`, `ValidationRunner.cs`, `AssetPaths.cs`, `SceneBuilder.cs`, `SceneNames.cs`, `LakeDockBuilder.cs`, `FishingSceneObjects.cs`, `PresentationSceneObjects.cs`, `FishingSeedData.cs`, `FishingContentBuilder.cs`, `FishingValidators.cs`, `FishingHostLedger.cs`, `CatchValidator.cs`, `HostCastView.cs`, `FishingHost.cs`, `FishingRodController.cs`, `FishingDebugSettings.cs`, `BuildInfo.cs`, `AppBootstrap.cs`, `SceneFlow.cs`, `UiFlowController.cs`, `UiStrings.cs`, `AppRoot.uxml`, `App.uss`, `MainMenuPresenter.cs`, `DevOverlayPresenter.cs`, `PlayerIntent.cs`, `InputContextRouter.cs`, `PlayerInputAdapter.cs`, `DevKey.cs`, `FakeInputService.cs`, `PaletteGlobals.cs`, `AmbienceBed.cs`, `SfxIds.cs`, `FishingTestData.cs`, `Validators.cs`. New behaviour goes in new files; the hot file gets a hook listed in 2.6.

### 2.4 TDD cadence
C2 2.4 applies. Pure-Core tasks (T5-T8, Core parts of T3/T13) are red/green by `test-edit -Filter`. Integration tasks use `compile` + `build` + the named tests. Every task ends with the full `test-edit`, and with `test-play` from T13 on (`-TimeoutMinutes 15`). The mutation rule of C2 16.4 applies to every test added (name the wrong implementation it catches; break key formulas once and show red).

### 2.5 Tiers
Per task in the skeleton (C2 2.5 shape). Haiku implements only when the section plan's task body contains the complete code and says `Implementer: Haiku (complete code below)`.

### 2.6 Shared-file edits (the only cross-owner edits allowed; in execution order)

| File | Edits (task: what) |
|---|---|
| `Core/Hashing/ContentHash.cs` | T2 (duplicate category name -> `ArgumentException`). |
| `Core/Net/DisplayNameRules.cs`, `Gameplay/Player/NameWire.cs`, `Gameplay/Session/SessionText.cs` | T2 (one truncation helper, 7.2; `NameWire.ToFixed` delegates to `SessionText.Name`, M3-D72). |
| `Gameplay/AssemblyInfo.cs` | T2 (one line: `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]`, keeping the PlayMode line and the `AssemblyMarker`; the name-truncation EditMode test needs the `internal` `NameWire`/`SessionText` members; M3-D70). T14 finds it present and does not edit this file. |
| `UI/UiSessionActions.cs`, `UI/UiStrings.cs` (key `busy.cancelling`) | T2 (online cancel feedback). `UI/UiStrings.cs` also: T20 (the three keys `toast.journal_new`, `toast.journal_best`, `toast.journal_unlock` only; M3-D88), then T21, T22 (see the UI row below). |
| `App/AppBootstrap.cs` | T2 (test seam `OnlineSessionFactoryOverride`, 7.2; M3-D71), then T20 (register `SaveService` in `ServiceRoots.App` after settings; save on quit). |
| `Fishing/Owner/FishingRodController.cs` | T1 (marker after machine tick), T15 (owner marker M3 gates: grid water, casting open). |
| `Fishing/Host/FishingHost.cs` | T1 (F6/F7 no longer reset in `OnDisable`; `OnEnable` hooks `FishingDebugSettings.ClearOnSessionEnd`), T13 (interim compile fix, 7.13: new ledger ctor with a local run state and a fixed interim context so casts keep landing), T15 (host FishingContext from the world and grid, session run state, catch-limit reservations). |
| `Runtime/AssemblyInfo.cs` | T20 (`[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]` for the `SaveService` serializer seams; keep the `AssemblyMarker`). |
| `Tests/EditMode/Tuning/FishingBots.cs` | No M3 task edits it. Its five members `BotKind`, `FightRecord`, `SeedFor`, `CastFor`, `RunFight` are made `internal` by R72 F4 (M3-D60). |
| `Fishing/Debug/FishingDebugSettings.cs` | T1 (session-end reset: `ClearOnSessionEnd(INetworkSession)` hooks `INetworkSession.Ended`; also `SubsystemRegistration`, never a Spot unload; M3-D69), T15 (`Luck`, reset by the same handler). |
| `Core/Fishing/{FishRarity,FishSpeciesData,BaitStats}.cs`, `Data/Fishing/{FishSpeciesDef,BaitDef}.cs` | T3 (Legendary, `Tags`, bait affinity fields; the new `Configure` parameters are optional and trailing, M3-D75). T5 (`BaitStats.Mods`, M3-D68). T13 removes `MinBaitRating`/`MinDepth` from `FishSpeciesData` and `FishSpeciesDef` (M3-D3). |
| `Editor/Builders/Fishing/FishingSeedData.cs` | T3 (species list shrinks to `bluegill` with its 12.6 values; bait affinity seeds; `minLandDepth` per Q1). T10 moves the species AND junk lists to `EcologySeedData` (`FishingSeedData.Species` then returns `EcologySeedData.Species` and `FishingSeedData.Junk` returns `EcologySeedData.Junk`, so seed and registry counts agree; M3-D61). |
| `Editor/Builders/Fishing/FishingContentBuilder.cs` | T3 (bait and species `Configure` callers), T10 (its create-if-missing loops, reading the forwarded roster seeds, build every species and junk asset and fill the reused models: it stays the ONLY creator of species and junk assets, M3-D62), T13 (`Configure` callers lose the two species gate arguments), T19 (allowed in-place edit: append missing `SfxCueTable` rows by id, never modify existing rows). |
| `Editor/Builders/Ecology/{EcologyMigration,EcologySeedData,EcologySeedData.Roster}.cs` | T13 (mechanical: drop the `minBaitRating`/`minDepth` arguments and initializers where they name the species fields; M3-D55). `EcologyContentBuilder` creates no species, so it has no such caller. |
| `Editor/Validation/FishingValidators.cs` | T3 (bait affinity ranges, species `tags` allowed bits), T13 (drop species `minBaitRating`/`minDepth` reachability check; reachability moves to V3). |
| `Data/ContentRegistry.cs` | T9 only: add `partial`, one call each from `Validate`, `Build`, `ComputeContentHash`, `ClearCache` into `ContentRegistry.Ecology.cs`. `dataVersion` is bumped by T3's migration in the asset, not in code. |
| `Data/SpotDef.cs` | T9 (`mapOrder`, `zone`, `Configure` overload). |
| `Data/Art/ArtPaletteDef.cs` | T9 (`sunMoon`, `weatherLooks`, getters, `Configure` overload). |
| `Data/Audio/SfxCueTableDef.cs` | T9 (`seasonAmbience[4]`). |
| `Editor/Builders/ContentBuilder.cs` | T3 (hook `EcologyMigration.MigrateIfNeeded(registry)` before the fishing build), T10 (hook `EcologyContentBuilder.Build()` after `FishingContentBuilder.Build()`; T10 also creates the harbour `SpotDef` asset but appends it to `registry.spots` only when `SceneNames.BuildOrder` contains the string `"Spot_HarbourPier"`, checked at run time with `Array.IndexOf`, so `build` stays green until T17 (T10 used the literal; the cleanup round replaced it with the early-added constant `SceneNames.SpotHarbourPier`, same value, still not in `BuildOrder`, see the `SceneNames.cs` row); T17's first `build` then wires it in with no file edit; M3-D63). |
| `Core/Content/ContentIds.cs` | T10 (`HarbourPier`). |
| `Editor/Validation/ValidationRunner.cs` | T11 (hook `EcologyValidators.All()`), T16 (hook `EcologyValidators.MapChecks()` - reachability over the registered spots, the water-layer rule, the `WorldState` hash and the lake layer split; M3-D83). |
| `Editor/Validation/EcologyValidators.cs` | T11 creates it; T16 appends `MapChecks` and its private helpers (append only: T11's methods are never changed). |
| `Editor/Validation/Validators.cs` | T17 (the M1 `lake_dock` sceneName check gains the `harbour_pier` -> `Spot_HarbourPier` check). |
| `Editor/Builders/AssetPaths.cs` | T3, T10, T16, T17: constants only, each in its own commented block. |
| `Core/Game/SceneNames.cs` | T17 (`BuildOrder` appends `Spot_HarbourPier` at index 3). The constant `SpotHarbourPier = "Spot_HarbourPier"` was added EARLY by the M3 cleanup round (T9-T12 review #6) so `EcologyContentBuilder` uses it instead of a string literal; it is deliberately NOT in `BuildOrder` until T17, so the M3-D63 gate (the builder registers the harbour spot only once `BuildOrder` contains it) is unchanged. T17 adds only the `BuildOrder` entry. |
| `Editor/Builders/SceneBuilder.cs` | T17 (one hook `HarbourPierBuilder.Build()` after `LakeDockBuilder.Build()`). |
| `Editor/Builders/LakeDockBuilder.cs`, `LakeDockLayout.cs` | T16 and T4 in either order (T4 has landed, so T16 keeps T4's changes); T16 (hooks `WorldSceneObjects.AddTo(scene, ContentIds.LakeDock)` and `StandPoints` from `LakeDockLayout.StandPoints` before the save; after it the BAKE ORDER is **`BuilderUtil.SaveScene(...)` -> `WorldSceneObjects.FinalizeAfterSave(scene, AssetPaths.LakeDockScene)` -> `Physics.SyncTransforms()` -> `ShoreGridBaker.BakeOpenScene(...)`**: `FinalizeAfterSave` needs the saved scene for the `GlobalObjectIdHash` (M3-D82), and the bake runs on the SAVED scene (its name becomes `bakedFromScene`; an untitled scene has none) and writes only the grid asset, never the scene; review F1, Opus review of T13/T14/T16). T17's `HarbourPierBuilder` uses the same order from its own file. |
| `Editor/Builders/Fishing/FishingSceneObjects.cs` | T20 (`JournalRecorder` on the `Fishing` root). |
| `Editor/Builders/Render/PresentationSceneObjects.cs` | T18 (`DayNightController`), T19 (`WeatherFx`, `HotspotFx`). Order T18, T19. |
| `Presentation/World/PaletteGlobals.cs` | T18 (season from `IWorldContext`, phase globals handed to `DayNightController`, the `_FGSeasonTint` array pushed as a dormant hook; M3-D23, M3-D78). |
| `Core/Render/ShaderGlobals.cs` | T18 (`SeasonTint`, `SeasonTintLength = 16`, `DayFraction`). |
| Shaders (`Shaders/*.shader`, `FishGameCommon.hlsl`) | T4 (look-dev standard; done). T18: NO EDIT. Under the adopted standard the season tint on textured environment albedo IS the `_FGPalette` season swap (palette hue x value-only detail texture), so no shader term is added (CCR-F1; M3-D23, M3-D78). The rejected alternative (an opt-in `_FGSeasonTintWeight` term in `Toon.shader`) is not used (CCR accepted 2026-10-13). |
| `Runtime/Audio/AmbienceBed.cs` | T19 (season parameter sets, M3-D22). |
| `Core/Audio/SfxIds.cs`, new `SfxRecipes.M3.cs` | T19 (`Rain_Patter`). |
| `Core/Versioning/BuildInfo.cs`, `Tests/EditMode/BuildInfoTests.cs` | T14 (`ProtocolVersion = 4`; history line; R72 took 3). |
| `Gameplay/AssemblyInfo.cs` | Moved to T2 (see above). T14 does not edit it; the `InternalsVisibleTo("FishGame.Tests.EditMode")` line is already present for its wire and dev-world tests. |
| `Core/Fishing/{FishingHostLedger,CatchValidator,HostCastView}.cs` | T13 only. |
| `Core/Fishing/{BitePlanner,RollOverrides,FishingRoll}.cs` | T13 (comments only: reword `PlaceholderRoller` to `FishingRoller`; M3-D55). |
| `Core/Fishing/PlaceholderRoller.cs`, `RollRequest.cs`, `Tests/EditMode/PlaceholderRollerTests.cs` | T13 deletes them (`git rm`); `RollOverrides` stays. |
| `Tests/EditMode/FishingTestData.cs` | R72-owned. T13 removes ONLY the `MinBaitRating`/`MinDepth` initializers in its `Fish(...)` helper (the field removal breaks compile; no fight value changes; M3-D55). `EcologyTestData` is a NEW file `Tests/EditMode/Ecology/EcologyTestData.cs`. |
| `Tests/EditMode/FishingHostLedgerTests.cs` | T1 (positive control in the zero-alloc test), T13 (rewrite against `EcologyTestData`; the control is kept), T15 (deletes `FishingHostLedger_InterimLakeContext_BluegillEligible`). |
| `Tests/EditMode/CatchValidatorTests.cs` | T13. |
| `Tests/EditMode/FishingContentTests.cs` | T3 (species expectations), T10 (species and junk blocks of `FishingData_SeedsMatchSpecTables` become "same list as `EcologySeedData`" checks; M3-D61), T13 (drops the two species field rows and asserts; deletes `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth`, replaced by V3). |
| `Tests/EditMode/ContentAssetParityTests.cs` | T3 (seed species count 1), T10 (the species-model test checks only species whose model file exists; M3-D61). |
| `Tests/EditMode/FishingDefsTests.cs` | T3 (new fields in the csv and the `CheckBait`/`CheckSpecies` helpers), T13 (drops `minBaitRating,minDepth`). |
| `Tests/EditMode/FishingValidatorTests.cs` | T3 (re-point `perch` to `bluegill`; new range rows), T13 (drops the two species gate rows and arguments; deletes `FishingValidators_UnreachableSpecies_IsReported`, replaced by V3). |
| `Tests/EditMode/{FishingRollValidationTests,FishingWireTests}.cs` | T13 (point their `PlaceholderRoller`/`RollRequest` uses at `FishingRoller.Roll` over `EcologyTestData`; test names kept; M3-D55). |
| `Tests/EditMode/{FishingContentBuilderTests,FishFallbackMeshTests}.cs`, `Tests/PlayMode/FeedbackSoloTests.cs` | T3 (stop naming the deleted species). `FeedbackSoloTests.cs` also: T19, CONDITIONAL (only if T19 step 0 finds day 1 segment 0 of the fixed dev seed is Rain: `FindLoop` with no name skips `Loop_Rain_Patter`; no other edit; M3-D81). |
| `Tests/PlayMode/PresentationWorldTests.cs` | T18 (in `PresentationWorld_Palette_IsLinearAndAppliedOnce` only: pin the world first through `DevWorldDriver` - clock stopped, minute 300, weather override Clear, season = `M2Season` - then keep every existing assertion; name kept; M3-D80). |
| `Tests/EditMode/Ecology/DayNightMathTests.cs` | T18 (adds `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable` and the extra `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette`; T8's tests untouched). |
| `Tests/EditMode/SceneNamesTests.cs` | T17 (`SceneNames_BuildOrder_BootstrapFirst`: the expectation gains `"Spot_HarbourPier"` at index 3; the test name is kept; no other edit; M3-D85). The cleanup round already added `SceneNames_SpotHarbourPier_IsTheHarbourSceneName` (pins the constant's value; T17 leaves it). |
| `Core/Ecology/HotspotPlanner.cs`, `Tests/EditMode/Ecology/HotspotPlannerTests.cs` | T7's files. T14 edited them under a CONTROLLER-AUTHORISED exception (its brief; the T5-T8 review asked for it): `Advance` clamps its minute to the new `internal const MaxAdvanceMinute = DayClock.DayLengthMinutes * 2`, so `int.MaxValue` neither spins through millions of spawn cycles nor overflows `EndMinute`; one test added. No other T14 edit to T7's code (review F3). |
| `Tests/EditMode/{ContentRegistryTests,Ecology/EcologyDefsTests,Ecology/EcologyValidatorTests,Ecology/EcologyMigrationTests}.cs` | T13 (mechanical fallout of the species `MinBaitRating`/`MinDepth` removal, M3-D55: the two dropped `Configure` arguments in all four files; `EcologyMigrationTests` also drops its two `MinBaitRating`/`MinDepth` round-trip asserts, since the fields are gone). Listed so the manifest is complete (review F3). |
| `Tests/PlayMode/PlayerHooksTests.cs` | T13 (the slot-takeover test's `HostCastView` sets `GridWater = true` and `Context = FishingHost.InterimContext(...)`, because every view now carries them). **T15 re-points that line from `FishingHost.InterimContext` to the world-built context** (the same builder the host uses, over the real registry and lake grid) when it deletes the interim; no other edit (review F3). |
| `Tests/EditMode/{Ecology/EcologyMigrationTests,FishFallbackMeshTests,ContentAssetParityTests}.cs` | Also touched by T10 beyond the T3 rows above (roster-driven expectations only: species counts become "the roster", the fallback-mesh shape check uses `mirror_minnow` (Long) for the species case, the parity test skips species without a model file; no behaviour change). Of the other two files the review named, `FishingDefsTests` was touched by T9 (8487dff, six lines) and T13, and `FishingHostLedgerTests` by T13 (rows above), not by T10. One-line note so the manifest matches the commits (T9-T12 review #4). |
| `Tests/EditMode/FishingValidatorTests.cs` (T19 edit) | T19: the `maxDistance 0` cue-row count in the cue-table test goes 5 -> 6 (the five M1 2D/ambience rows plus the 2D `Rain_Patter` loop); no other edit (review F3). |
| `Content/Textures/T_FishSkinArray.png`, `Editor/Builders/Render/TextureArrayPostprocessor.cs`, `Tests/EditMode/SmoothMeshBuilderTests.cs` | T23 (T4's files; M3-D91). The sheet grows from 4 x 2 to 4 x 8 (2048 x 4096 px) with rows appended, so layers 0-5 keep their numbers; the `FishSkinArray` row of the postprocessor goes `Rows` 2 -> 8, the max texture size 2048 -> 4096 for that array only (the environment array stays 2048) and `GetVersion` +1, and its stale layer comment ("2..7 white") is corrected; `SmoothMeshBuilderTests.TextureArrays_ImportAsFlipbookArrays` (`skin.depth` 8 -> 32) is the pinned T4 test. |
| `Tests/EditMode/ShaderValidatorTests.cs`, `Tests/PlayMode/FishingSoloTests.cs` | T1. |
| `App/SceneFlow.cs` | T15 (host handles `DevLoadSpotRequested`: NetworkSceneManager single load of the spot's scene, dev builds only; default load stays `Spot_LakeDock`). |
| `Core/Input/{PlayerIntent,PlayerInputFrame,InputContextRouter}.cs`, `Runtime/Input/{PlayerInputAdapter,DevKey,IDevInput}.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs` | T21 (`JournalPressed` on `PlayerInputFrame` and `PlayerIntent`, routed in Free, RodReady, RodWaiting; `PlayerInputFrame.cs` is T21's, M3-D87), T22 (`DevKey.ScrubClock` = 6 = F8; `IDevInput` gains `bool IsHeld(DevKey key)` with the same gating as `WasPressed`, implemented by `PlayerInputAdapter` and `FakeInputService`; M3-D89). Order T21, T22. |
| `UI/UiFlowController.cs`, `UI/UiStrings.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`, `UI/MainMenuPresenter.cs` | T21 (journal modal, J key, main-menu Journal button and meta summary line; the internal HUD-modal seam inside `UiFlowController.cs` that T22's dev panel also uses, M3-D90), T22 (clock HUD and dev panel hooks). `UI/UiStrings.cs` is T2, T20 (three toast keys only), T21, T22. Order T21, T22. |
| `UI/Fishing/DevOverlayPresenter.cs` | T22 (F3 cycles Off -> Overlay -> Panel -> Off; one WORLD line). |
| Generated output (`Scenes/*.unity`, `Prefabs/*.prefab`, `Content/**`, `ProjectSettings/*`) | Any task may regenerate by `build`. Scene churn without a real change is restored with `git restore Assets/_Project/Scenes/*.unity` - EXCEPT that from T14 on a Spot scene's `WorldState` `GlobalObjectIdHash` follows the fileIDs, so restore the WHOLE scene file or commit the WHOLE scene file, never a partial hunk (2.8 #24). |
| Data assets under `Content/Fishing/**`, `Content/Ecology/**` | Created by T3/T10 (create-if-missing). T11/T12 may retune values in `.asset` files and must then update the seed and the `EcologyTunedDeviations` map in `EcologyContentTests.cs` (mirrors C2's `TunedDeviations`). Grid values are written only by `ShoreGridBaker`. |

**Integration-fix rule (T15, T16, T24, T25 only):** as C2 2.6 (fix defects in earlier M3 files, public surface frozen; each fix named `fix(<file>): <what>`; a signature change is a contract change: stop and report).

### 2.7 Reading rule
Section plans cite C2 and this contract by section number instead of restating. Implementers read the cited section when they touch that code.

### 2.8 Global constraints (every M3 brief carries these)
1-21. **C2 2.8 #1-#21 apply unchanged** (no `DEVELOPMENT_BUILD`/`#if DEBUG`; `\u` decoding check and ASCII docs; cancel convention; NetworkObject `SetDirty`; `SerializedObject` settings; scene churn; LF blobs; EditMode reach; PlayMode warning-free; late joiners; editor lock = HELD; no `rm -rf`; batchmode shader compile sync; builders load after `NewScene`; Core RNG; Presentation read-only; full-length shader arrays; commits; logs in the scratchpad). **Exception to C2 #15:** M3 adds the first in-scene-placed NetworkObject (`WorldState`, spec 4.6); see #24. **C2 #19 (vertex colours, `_JitterEncoded`) applies only to content the R74 standard has not replaced;** restyled content follows the style guide.
22. **Ecology tuning lives in assets** (`FishingZoneDef`, `SpawnRule`, `WeatherTableDef`, `EcologyTuningDef`, `ArtPaletteDef` M3 fields). Their seeds live only in `FishGame.EditorTools.EcologySeedData` [T10]. Core holds spec formula constants (`EcologyConstants`, `DayClock`) and no tuning.
23. **Host-authoritative ecology.** Season, weather, clock anchor, hotspot and every roll are host decisions. Clients compute only cosmetics and the journal from replicated state and announcements. No client value enters a roll except the cast request fields C2 already validates.
24. **In-scene NetworkObject rules.** `WorldState`'s `NetworkObject` is added once per scene build by `WorldSceneObjects.AddTo`; call `EditorUtility.SetDirty` after `OnValidate` so `GlobalObjectIdHash` is non-zero (validated, 11.2). Its hash follows the scene's local fileIDs, so a Spot scene file is committed or restored as a whole. Host and clients must run the same scene file (same build). Fallback if MPPM sync proves unstable (T14 check): `WorldState` becomes a network prefab spawned at a `NetSpawnAnchor` by `PlayerSpawner` (M2 DockBell path) with the SAME public API; no other task changes (M3-D18).
25. **Deterministic streams.** Every new random stream is `Rng.Stream(runSeed, EcologyStreams.<Name>, index)` with its own constant id; a draw added in one system never shifts another's rolls. Fixed draw orders are written in the owning task's plan and pinned by a test.
26. **Owner and host run the same cast gates.** Any cast check the host adds (grid water, casting open) is also run by the owner's marker so honest casts are never host-rejected (M3-D16).
27. **Textured materials (R74).** Seasonal swaps and day-night lighting tint on top of the albedo (texture x tint), never replace a textured material's colour with a flat palette colour (M3-D23). Under the adopted standard the environment albedo is palette hue x value-only detail texture, so the `_FGPalette` season swap IS the season tint and day-night drives light, sky and fog only (M3-D78).
28. **The procedural fallback fish mesh stays.** A species with no model (or a model the validators reject) reveals with `FishFallbackMesh`; missing art never blocks a build, a test or the milestone exit (Q2).

---

## 3. Execution order and dependency graph

Serial execution (one Unity lock, one git index); reviews pipeline per the skeleton. Spine:

| Task | Depends on (committed first) |
|---|---|
| T1 Carry: fishing minors (C2 16.1 final row) | gate 1.2 #1-#3 |
| T2 Carry: hash guard, name truncation, online cancel feedback | gate |
| T3 Migration: M2 placeholders -> roster start (+ FishTags, Legendary, species tags, bait M3 fields) | T1 |
| T4 Look-dev adoption and M2 restyle (owned by the R74 look-dev result) | user approval of `m2-lookdev` |
| T5 Core: ecology types, clock, database containers | T3 |
| T6 Core: `FishingResolver`, `FishingRoller`, `CatchLedger` | T5 |
| T7 Core: `DistanceField`, `ShoreGrid`, `WeatherDirector`, `HotspotPlanner` | T5 |
| T8 Core: journal rules and DTOs, `BitingNow`, `DayNightMath`, `SilhouetteMath` | T6 |
| T9 Data: ecology defs, registry partial, M3 fields on SpotDef/ArtPaletteDef/SfxCueTableDef | T7 |
| T10 Content: `EcologySeedData`, `EcologyContentBuilder`, generated assets | T9 |
| T11 Coverage: data validators, V1-V3, 13.3 variety, fightability | T10 |
| T12 Balance reports V4/V5 | T11, gate R72 F8 |
| T13 Host roll: ledger on the resolver, M3 cast checks, catch limits, run state; PlaceholderRoller removed | T6, T10 |
| T14 `WorldState`, wire structs, `DevWorldDriver`, ProtocolVersion 4 | T7, T10 |
| T16 Lake map: StandPoints, ShoreGrid bake, WorldState placement, map validators (runs BEFORE T15; no look-dev wait) | T11, T14 |
| T15 Fishing host/owner integration, dev map switch plumbing | T13, T14, T16 |
| T17 Harbour Pier map (waits for T4) | T4, T15, T16 |
| T18 DayNightController and seasonal tint | T4, T8, T14 |
| T19 WeatherFx, HotspotFx, seasonal ambience | T18 |
| T20 SaveService and JournalRecorder | T8, T15 |
| T21 Journal UI, IconFactory, journal input, menu summary | T4, T20 |
| T22 Dev panel, HUD clock, F8 scrub | T15, T20 (after T21 for the shared UI files unless look-dev is late; skeleton section 3) |
| T23 Fish models (controller, Blender MCP; off the critical path) | T4, T10 |
| T24 PlayMode ecology integration | T16, T17, T20 |
| T25 Acceptance, final `check`, branch review | all (T23 may be partial: Q2) |

Rule: no task references a type owned by a later task. `IWorldContext` (Core, T5) exists so Fishing, Presentation, Runtime and UI read world state without Gameplay knowing them.

---

## 4. Assemblies

**No asmdef changes are planned.** Core gains files only (`noEngineReferences` stays true). Runtime already auto-references Newtonsoft (`SaveService`). EditMode tests reach Core, Data, Runtime, Editor, Gameplay, Fishing (C2 4); they never call Newtonsoft types directly (tests use `SaveService` APIs and string checks), so `FishGame.Tests.EditMode.asmdef` needs no `Newtonsoft.Json.dll` (M3-D30). UI-only logic is tested through pure Core helpers (`SilhouetteMath`, `JournalRules`, `DayClock.FormatTime`), never by reflection into UI (the two M2 reflection exceptions stay the only ones). **PlayMode does not reference Presentation** (its asmdef lists Core, Data, Runtime, Networking, Gameplay, Fishing, App, UI): M3 PlayMode presentation tests (`DayNightTests`, `WeatherFxTests`) follow M2's `PresentationWorldTests` pattern - find scene roots by name and components by type name, drive the world through `WorldState`/`DevWorldDriver` (Gameplay, reachable), and observe shader globals, `RenderSettings`, the `Sun` light and particle/audio state; they never call Presentation types. `SaveService` serializer seams are reached through `InternalsVisibleTo` in `Runtime/AssemblyInfo.cs` (T20, C 2.6). If a task finds a missing reference, that is a contract bug: stop and report (integration tasks T15, T16, T24, T25 may add one under the integration-fix rule).

---

## 5. Constants

### 5.1 Tuning lives in assets
2.8 #22. Seeds: `EcologySeedData` [T10] for zones, rules, roster, junk, weather table, ecology tuning, sun/moon, weather looks, season ambience; `FishingSeedData` [T3] for the bait affinity fields and `minLandDepth`.

### 5.2 Core constants

```csharp
// Core/Ecology/DayClock.cs [T5] - spec 12.2, R1. Pure.
public static class DayClock
{
    public const int DayLengthMinutes = 1200;            // 06:00 -> 02:00, never wraps; valid casting 0..1199
    public const int LastCallMinute = 1140;              // 01:00
    public const int SegmentMinutes = 240, SegmentsPerDay = 5;
    public const int PlannedDays = 7;                    // 35 planned segments
    public const float DefaultMinutesPerSecond = 1200f / 900f;   // 1.333; M4 RunConfigDef.dayRealSec replaces it
    public const int DisplayStepMinutes = 10;
    public static readonly int[] BandStart = { 0, 120, 300, 480, 660, 840 };   // Dawn, Morning, Midday, Afternoon, Dusk, Night
    public static TimeBandMask BandAt(int minute);      // minute clamped to 0..1199; exactly one bit
    public static int SegmentAt(int minute);             // clamp, / 240 -> 0..4
    public static float DayFraction(float minute);       // clamp01(minute / 1200); NaN -> 0
    public static string FormatTime(int minute);         // "6:00 AM" .. "1:50 AM": floor to 10, 12-hour, ASCII, invariant
}

// Core/Ecology/EcologyConstants.cs [T5] - spec 12.4/12.7/12.9/12.10 formula numbers (not tuning).
public static class EcologyConstants
{
    public const float JunkShareCap = 0.6f;              // 12.4 step 1
    public const float SpecialChanceCap = 0.5f;          // 12.4 step 2, 12.9 specialChance in (0, 0.5]
    public const float HotspotJunkMult = 0.5f, HotspotSpecialMult = 2f;   // 12.7 effects (bite x0.6 stays in BitePlanner.WaitMultiplier)
    public const float EventBiteMultMin = 0.5f, EventBiteMultMax = 1.5f;  // 9.4 eventMult clamp
    public const ushort ShoreDmNotWater = 0xFFFF;        // 12.5
    public const ushort ShoreDmMax = 0xFFFE;
    public const byte AnySubArea = 0xFF;                 // FishingContext: sub-area gate passes (journal "biting now")
    public const int MaxSubAreas = 8;                    // SpawnRuleData.SubAreaMask is a byte
    public const int JournalCaughtForLevel3 = 3;         // 12.10 level 3
    public const float GridCellSizeM = 1f;               // 12.5 cellSizeM
    public const int MinReachableCellsPerSubArea = 25;   // 12.5 reachability
    public const float ReachMinM = FishingConstants.CastTipReach + 2.5f;   // 4.9 m (R67; M3-D6); 2.5 = castMinDist seed, checked against the asset by the validator
    public const float ReachMaxM = FishingConstants.CastTipReach + 14f;    // 16.4 m (Twig castMaxDist 14)
}

// Core/Ecology/EcologyStreams.cs [T5] - Rng stream ids (2.8 #25).
public static class EcologyStreams
{
    public const string Season = "season";     // index 0
    public const string Weather = "weather";   // index 0 for the 35 planned segments; index d for infinite day d >= 8
    public const string Hotspot = "hotspot";   // index = day
}
```

### 5.3 Other single-owner constants

| Constant | Value | Owner / file |
|---|---|---|
| `BuildInfo.ProtocolVersion` | `4` (R72 took 3; new in-scene `WorldState` NetworkObject and its NetworkVariables) | T14 |
| `ContentRegistry` `dataVersion` (asset) | `1 -> 2` (bumped once by the migration; spec 12.6) | T3 |
| `ContentRegistry.CategoryZones` | `"zones"` (hashed only when non-empty, like C2 D-Q1) | T9 |
| `ContentIds.HarbourPier` | `"harbour_pier"` (`ContentIds.LakeDock` exists) | T10 (it creates the harbour `SpotDef`) |
| `SceneNames.SpotHarbourPier` | `"Spot_HarbourPier"`; `BuildOrder = { Bootstrap, Menu, Spot_LakeDock, Spot_HarbourPier }` | the constant: cleanup round (before T17, not yet in `BuildOrder`); the `BuildOrder` entry: T17 |
| Meta save file | `Application.persistentDataPath/meta{InstanceProfile.Current.Suffix}.json` (+ `.tmp`, `.bak`) | T20 `SaveService` |
| `MetaSave.SchemaVersion` | `1` | T8 |
| Dev keys | F3 cycles overlay -> panel -> off (T22); F8 held = host dev clock x60 (`DevKey.ScrubClock = 6`, read through the new `IDevInput.IsHeld(DevKey)`, T22, M3-D89). F4-F7, F9 unchanged; F10 (`DevKey.CycleForcedSpecies = 7`, M2 final review I3) cycles `FishingDebugSettings.ForcedSpeciesId` and is unchanged too (value 6 stays free for `ScrubClock`) | T22 |
| `ShaderGlobals.SeasonTint` | `"_FGSeasonTint"` (Vector4[16], always full length; pushed as a DORMANT hook, no M3 shader reads it, M3-D78) and `ShaderGlobals.DayFraction` `"_FGDayFraction"` (float) | T18 |
| `SfxIds.RainPatter` | `"Rain_Patter"` (filtered noise loop, M3) | T19 |
| UiStrings keys | `busy.cancelling` (T2); `toast.journal_new`, `toast.journal_best`, `toast.journal_unlock` (T20: its `JournalRecorder` publishes the keys, and Fishing cannot reference UI; M3-D88); `journal.*`, `menu.journal`, `menu.meta_journal` (T21); `hud.clock`, `dev.*` (T22) - texts in the owning plan, ASCII only | T2, T20, T21, T22 |

---

## 6. Assets, scenes and hierarchies

### 6.1 Folder additions
Section 2.2.

### 6.2 Asset paths and ids (under `Assets/_Project/`)

| Asset | Path | Created by | Ids / notes |
|---|---|---|---|
| Species (25) | `Content/Fishing/Species/<id>.asset` | T3 (bluegill overwritten), T10 (24 new, create-if-missing, created by `FishingContentBuilder` from the forwarded roster seeds; M3-D62) | 23 regular + `gilded_carp`, `old_barnacle`; values spec 12.6 (+ M3-D35 presentation fields) |
| Junk (7) | `Content/Fishing/Junk/<id>.asset` | T10 (5 new, created by `FishingContentBuilder` from the forwarded seeds; `old_boot`, `tin_can` exist; M3-D62) | `old_boot, tin_can, soggy_newspaper, lost_sock, rope_scrap, seaweed_clump, empty_bait_tub` (M3-D34) |
| Zones (2) | `Content/Ecology/Zones/<id>.asset` | T10 | `lake_dock`, `harbour_pier` (zone id = spot id) |
| Weather table | `Content/Ecology/Weather/weather_temperate.asset` | T10 | shared by both zones (M3-D44); spec 12.2 matrix |
| Shore grids (2) | `Content/Ecology/ShoreGrids/<spot>_grid.asset` | T10 creates empty (width 0); T16/T17 bake in place | `lake_dock_grid`, `harbour_pier_grid`; GUIDs stable (written in place, never recreated) |
| Ecology tuning | `Content/Ecology/EcologyTuning.asset` | T10 | `EcologyTuningDef` (hotspot timings, radius) |
| Spots | `Content/Spots/lake_dock.asset` (exists), `Content/Spots/harbour_pier.asset` | T10 fills the null `zone` on lake_dock (allowed in-place edit; lake_dock's `mapOrder` is 0, the default); creates harbour_pier (asset only; listed in `registry.spots` once `SceneNames.BuildOrder` contains `Spot_HarbourPier`, M3-D63) | `lake_dock`: mapOrder 0; `harbour_pier`: mapOrder 1, scene `Spot_HarbourPier` |
| Fish models | `Content/Models/Fish_<Pascal>.fbx` | T4 (restyled M2 five, if the standard replaces them), T23 (20 new) | staged in `Art/Export/`, source `Art/Blender/fishgame_m3_species.blend` (and whatever the R74 standard adds, for example baked textures); one submesh unless the R74 standard says otherwise (then `FishingValidators.SpeciesModels` follows the standard, changed by T4) |
| Fish skin sheet | `Content/Textures/T_FishSkinArray.png` (imported as a `Texture2DArray`, 512 x 512 tiles, 4 columns) | T4 created it (4 x 2); T23 grows it to 4 x 8 (2048 x 4096 px, rows appended) | Layers 0-5 are used by the R74 adoption and never renumbered: 0 white, 1 bluegill, 2 perch, 3 carp, 4 trout, 5 catfish (style guide 2.2); layers 6 and 7 are white tiles of the current sheet. The 20 new species take layers 6-25, assigned in T23 batch order (the batches are 5 / 4 / 6 / 5: 6-10, 11-14, 15-20, 21-25); unused tiles (26-31) stay white. 26 used layers need a 32-layer sheet and the import size cap rises to 4096 for this array only (M3-D91). |
| Harbour meshes / props | per the R74 standard (`Content/Meshes/Harbour_*.asset` for procedural meshes; FBX + textures for Blender props) | T17 | |
| Scene | `Scenes/Spot_HarbourPier.unity` | T17 | build index 3 |

**Allowed in-place edits** (in addition to C2 6.2): the migration's writes (7.3); `EcologyContentBuilder` fills a null `SpotDef.zone`, a missing registry list entry and null singleton refs (it creates no species or junk asset: it throws `InvalidOperationException` naming any species or junk asset its zones reference that does not exist, M3-D62); `ShoreGridBaker` writes every field of an existing `ShoreGridDef`; `FishingContentBuilder` appends missing cue rows (T19). Nothing else overwrites an existing asset.

### 6.3 Scene objects added to each Spot scene

| Root | Components | Added by |
|---|---|---|
| `World` | `NetworkObject` (in-scene placed), `WorldState` (NetworkBehaviour), `DevWorldDriver` (MonoBehaviour, host-only behaviour); serialized `spotId` | `WorldSceneObjects.AddTo(scene, spotId)` [T16 for the lake, T17 for the harbour; the class is T16's] |
| `StandPoints/StandPoint_<n>` | `StandPoint` (no collider, layer Default), `facingDeg` | lake: T16 from `LakeDockLayout.StandPoints`; harbour: T17 |
| `Fishing` | `FishingHost`, `FightTelemetry`, `JournalRecorder` (T20) | `FishingSceneObjects.AddTo` |
| `Presentation` | `PaletteGlobals`, `WaterTimeDriver`, `RippleField`, `DayNightController` (T18), `WeatherFx`, `HotspotFx` (T19) | `PresentationSceneObjects.AddTo` |
| `Sun`, `WarmVolume`, `CoolVolume` | existing; `DayNightController` drives them | scene builders |

The harbour scene also needs every per-scene hook the lake has: `GameScope`, `PlayerSpawner` with spawn points (late-joiner `OnLoadComplete` path), `SceneCamera`, `Ambience`, `BellAnchor` (`NetSpawnAnchor` for the DockBell prefab), `WaterVisual` + `WaterVolume` (the only Water-layer collider), seabed on Ground, land on Shore, pier on Structure, ladders as `LadderVolume` on Default, `RenderingSetupBuilder.ApplySceneLighting()`, both Volumes, and the three AddTo hooks above.

### 6.4 Editor menu items
| Menu path | Owner | Method |
|---|---|---|
| `FishGame/Ecology/Migrate M2 Placeholders` | T3 | `EcologyMigration.MigrateMenu()` |
| `FishGame/Ecology/Bake Shore Grids` | T16 | `ShoreGridBaker.BakeAllMenu()` (offers to save modified scenes, opens each registered Spot scene SINGLY with `OpenSceneMode.Single` - two Spot scenes loaded together would stack two 120 m `WaterVolume` boxes at the origin and mix the bake rays - bakes, saves the grid asset, restores the scene setup; `BakeOpenScene` throws when another loaded scene holds a `WaterVolume`; M3-D86) |
| `FishGame/Ecology/Print Coverage Report` | T11 | `EcologyValidators.PrintCoverageMenu()` |

---

## 7. Types by task (public surface)

Everything listed is `public` unless marked. Paths relative to `Assets/_Project/`. Every networked class and every scene object with host-only behaviour opens with an authority comment.

### 7.1 T1 - Carry: fishing minors (C2 16.1 final row)
No new public surface. Behaviour:
- `FishingRodController` draws the marker AFTER the machine's `Tick` in the same frame (C2 carry "drawn marker trails one frame").
- `FishingHost.OnDisable` no longer resets `FishingDebugSettings` F6/F7; they reset when the session ends (`INetworkSession.Ended` only, hooked by the internal `FishingDebugSettings.ClearOnSessionEnd(INetworkSession)`) and on `SubsystemRegistration`, so a Spot change (M3 map switch) keeps them. `ServiceRoots.Game` teardown is NOT a reset trigger: every Spot scene has its own `GameScope`, so that teardown fires on every Spot load (M3-D69).
- Test and comment fixes: the landing-parity test comment no longer claims M-4 coverage; the ledger zero-alloc test gains a positive control (an allocating call is detected); `ShaderValidatorTests` gains the 3 extra cases from the polish review; the HUD creel lookup uses an explicit null check instead of Unity's fake-null; the `0.625 x reel` comment in `CatchValidator`/`FishingConstants` is corrected to R72's `MaxReelSpeed` wording (or removed if R72 already did).

### 7.2 T2 - Carry: content hash guard, name truncation, online cancel feedback
```csharp
// Core/Hashing/ContentHash.cs (edit): Compute throws ArgumentException("duplicate content category '<name>'") on a repeated category name.
// Core/Net/DisplayNameRules.cs (edit)
public static string Truncate(string clean, int maxChars);   // never splits a surrogate pair; the single truncation primitive used by NameWire, SessionText and Sanitize
// App/AppBootstrap.cs (edit): test seam for the PlayMode cancel test (precedent: AppBootstrap.SuppressDevAutoStart)
public static Func<INetworkSession> OnlineSessionFactoryOverride { get; set; }   // null in production; reset in ResetStatics; read lazily by the online backend factory
```
- `Truncate` is a CHAR-count primitive. `NameWire` and `SessionText` both fit a UTF-8 BYTE budget (`FixedString32Bytes`, 29 bytes), so `SessionText.FitBytes` keeps its byte loop and each step calls `DisplayNameRules.Truncate(s, s.Length - 1)`; `NameWire.ToFixed` delegates to `SessionText.Name`. Behaviour is identical for ASCII names. **Recorded behaviour change:** `SessionText.Name` now drops lone surrogates (as `NameWire` always did); before, it let them through and they encoded as U+FFFD. Tested on a surrogate pair at the boundary (M3-D72).
- `AppBootstrap.OnlineSessionFactoryOverride` (public, test-only by convention): the factory lambda becomes `() => OnlineSessionFactoryOverride != null ? OnlineSessionFactoryOverride() : new UgsNetworkSession(sessionConfig)`. `UiFlowController` captures the session on the first frame, so a test cannot swap it afterwards (M3-D71).
- T2 also adds `InternalsVisibleTo("FishGame.Tests.EditMode")` to `Gameplay/AssemblyInfo.cs` (2.6; M3-D70).
- Online cancel: `UiSessionActions` shows the Busy screen text `busy.cancelling` ("Cancelling...") at once and returns to MainMenu as soon as the session reports the cancel, never waiting on the SDK's up-to-30 s return (the M2 handoff item). The C2 cancel convention (fault with `UserLeft`) is unchanged.

### 7.3 T3 - Migration: M2 placeholders to the M3 roster start
```csharp
// Core/Ecology/FishTags.cs (new) - spec 12.2
[Flags] public enum FishTags : ushort { None = 0, Nocturnal = 1, Predator = 2, Bottom = 4, Schooling = 8, Surface = 16 }
// Core/Fishing/FishRarity.cs (edit): { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3 }   // display only (spec 12.2)
// Core/Fishing/FishSpeciesData.cs (edit): + public FishTags Tags;
// Core/Fishing/BaitStats.cs (edit): + public FishTags AffinityTags; public float AffinityMult, SpecialMult;
//     public BaitMods Mods(int loadedCount);   // added in T5 when BaitMods exists (T3 adds only the fields)
// Data/Fishing/FishSpeciesDef.cs (edit): + [SerializeField] FishTags tags (after `rarity`... appended at the END of the field list and of Configure, so YAML order is additive); getter Tags; ToCore maps it
//     Configure(..., Mesh model, FishTags tags = FishTags.None)   // optional trailing parameter (M3-D75)
// Data/Fishing/BaitDef.cs (edit): + affinityTags (FishTags), affinityMult, specialMult appended; getters; Configure appended; ToCore maps them
//     Configure(..., bool unlimited, FishTags affinityTags = FishTags.None, float affinityMult = 1f, float specialMult = 1f)   // optional trailing (M3-D75)
//     the two multiplier fields carry the field initializer `= 1f`

// Editor/Builders/Ecology/EcologyMigration.cs (new) - the one-off spec 12.6 step, idempotent
public static class EcologyMigration
{
    public const int MigratedDataVersion = 2;
    public static bool MigrateIfNeeded(ContentRegistry registry);   // true when it migrated; no-op when registry.DataVersion >= 2
        // In order: (1) remove perch, carp, trout, catfish from the registry species list, then AssetDatabase.DeleteAsset their .asset files
        //   (the FBX models stay); (2) overwrite bluegill with its 12.6 values (Configure with every field, tags Schooling, rarity Common,
        //   fight = steady, model kept); (3) write affinityTags/affinityMult/specialMult on all six existing bait assets from FishingSeedData;
        //   (4) registry dataVersion = 2 (SerializedObject); (5) SaveAssets. Logs one "FG MIGRATION" line per step.
    internal static bool MigrateAt(string contentRoot, ContentRegistry registry);   // test seam (Tests/__Temp, like C2 BuildAt)
    [MenuItem("FishGame/Ecology/Migrate M2 Placeholders")] public static void MigrateMenu();
}
```
- `FishingSeedData.Species` shrinks to `bluegill` (12.6 values: 0.2-0.6 kg, value 8, Common, Steady, tags Schooling) so the create-if-missing pass never recreates the deleted species. Bait seeds gain the M3-D4 affinity values. `minLandDepth` seed per Q1 (default 0.5; the asset is updated by T3 through the same SerializedObject write). 0.5 is a **spec value change** (spec 9.3/9.12, M3-D52, pending the user's Q1; the default applies), so the seed becomes 0.5, `FishingData_SeedsMatchSpecTables` expects 0.5 and there is NO `TunedDeviations` entry. If the user instead keeps 0.8 (lake-bed alternative), T3 skips its tuning write (M3-D74).
- **Neutral safety defaults (explicit exception to "no tuning initializers", C2 7.13 / 2.8 #22):** the `= 1f` initializers and the optional trailing `Configure` defaults on the new species/bait fields are neutral (tags None, multipliers 1), not tuning. A default of 1, never 0, cannot silently disable specials (B4), and about 20 in-memory test call sites keep compiling. The builder and the migration always pass explicit values (M3-D75).
- `ContentBuilder.Build` calls `EcologyMigration.MigrateIfNeeded(registry)` BEFORE `FishingContentBuilder.Build()`.
- Species-level `minBaitRating`/`minDepth` stay until T13 (the M2 roller still reads them); bluegill gets 0 / 0.

### 7.4 T4 - Look-dev adoption and M2 restyle (owned by the R74 look-dev result)
Not authored by an M3 section. The look-dev plan (branch `m2-lookdev`) defines the work: the style guide doc, the Toon shader changes (soft ramp, rim, AO, textured albedo), material and texture pipeline, the restyle of the dock, props, player body and the five M2 fish, and any validator changes (`RenderValidators`, `FishingValidators.SpeciesModels`, C2 #19). **The contract fixes only what M3 needs from it:**
- a written style guide at `docs/superpowers/research/2026-10-12-art-direction-lookdev.md` covering fish models (poly budget, smooth normals, texture size and format, colour/vertex-colour rule), props, and icons;
- a shader hook where the seasonal tint multiplies the albedo (M3-D23) - or an explicit statement that T18 adds it. **Resolved by the adoption (M3-D78):** the standard needs no extra term. In the merged `Shaders/Toon.shader` (checked 2026-10-13 against d660ee9) the albedo is `palette[uv1.x] x vertexFactor x _BaseColor x texFactor`; `M_Toon_Base` and `M_Toon_Tri` use the palette (`usePalette` true) with the env detail array at gain 2 (value-only detail centred on 0.5, hue from the palette), so swapping `_FGPalette` per season already tints textured environment albedo. Cloth, skin, fish and brass do not use the palette (`usePalette` false; `_BaseColor` or the full-colour skin array) and are season-invariant (style guide 2.3). No `_FGSeasonTint` appears under `Shaders/`;
- the T4 adoption is DONE (style guide commit d660ee9; the four reused fish were restyled by 3de9de1). The fish skin array holds layers 0-5 (0 white, 1 bluegill, 2-5 perch, carp, trout, catfish) on a 4 x 2 sheet, so the M3 species layers continue from 6 (M3-D91);
- `FishFallbackMesh` still works with the new fish material (2.8 #28);
- the lake rebuild keeps every collider, exit, ladder volume, spawn and the bell anchor where R70 left them (the grid bake reads colliders only), so the M3 map tasks can follow.

### 7.5 T5 - Core: ecology types, clock and database containers (`Core/Ecology/`)
```csharp
[Flags] public enum TimeBandMask : byte { None = 0, Dawn = 1, Morning = 2, Midday = 4, Afternoon = 8, Dusk = 16, Night = 32, All = 63 }
[Flags] public enum SeasonMask : byte { None = 0, Spring = 1, Summer = 2, Autumn = 4, Winter = 8, All = 15 }
public enum Season : byte { Spring = 0, Summer = 1, Autumn = 2, Winter = 3 }
[Flags] public enum WeatherMask : byte { None = 0, Clear = 1, Rain = 2, Fog = 4, Storm = 8, Heatwave = 16, All = 31 }
public static class EcologyMasks { public static SeasonMask Of(Season s); public static int WeatherColumn(WeatherMask single); }   // Clear 0, Rain 1, Fog 2; anything else throws

// DayClock, EcologyConstants, EcologyStreams: section 5.2.
public struct ClockAnchorData { public double AnchorServerTime; public float AnchorMinute; public float MinutesPerSecond; public bool Running; }
public static class ClockMath
{
    public static float MinuteAt(in ClockAnchorData a, double serverTime);
        // min(1200, AnchorMinute + (Running ? (serverTime - AnchorServerTime) * MinutesPerSecond : 0)); never below AnchorMinute;
        // a non-finite input returns AnchorMinute (spec 14.2 formula; NetworkTime is a double)
    public static ClockAnchorData Reanchor(in ClockAnchorData a, double serverTime, float minutesPerSecond, bool running);
        // continuity: MinuteAt(result, serverTime) == MinuteAt(a, serverTime)
    public static ClockAnchorData At(double serverTime, float minute, float minutesPerSecond, bool running);
}

public struct SubAreaBandData { public float MinShoreM, MaxShoreM, JunkChance; }   // last band MaxShoreM = +Infinity
public struct SpawnRuleData
{
    public ushort SpeciesIndex; public byte SubAreaMask;              // 0 = all sub-areas
    public SeasonMask Seasons; public WeatherMask Weather; public TimeBandMask Bands;
    public float Weight; public byte MinRodTier; public int MinBaitRating; public float MinDepth;
    public bool Special; public float SpecialChance; public int CatchLimitPerRun;   // -1 unlimited
    public int MinPlayersInSession;                                   // 0 in M3 (M3-D36)
}
public struct JunkEntry { public ushort JunkIndex; public float Weight; }
public struct WeatherTableData
{
    public float[] Base;               // 9, row-major: rows current Clear/Rain/Fog, columns next Clear/Rain/Fog (spec 12.2)
    public float[] SeasonColumnMult;   // 12: season * 3 + column (Spring Rain 1.5, Summer Fog 0.3, Winter Fog 1.3, others 1)
}
public struct BaitMods
{
    public int Rating; public FishTags AffinityTags; public float AffinityMult, JunkMult, SpecialMult;
    public static BaitMods Neutral(int rating);   // no affinity, all multipliers 1 (V1-V3 "neutral bait", M3-D5)
}
public struct EventMods                          // M4 daily modifiers, M5 hazards; neutral in M3
{
    public float JunkMult, RareChanceMult, BiteTimeMult;
    public float NocturnalMult, PredatorMult, BottomMult, SchoolingMult, SurfaceMult;
    public static readonly EventMods Neutral;    // every field 1
    public float WeightFor(FishTags tags);       // product of the multipliers of the set bits; None -> 1
    public bool IsValid { get; }                 // every field finite and > 0 (a default struct is NOT valid)
}
public struct SkillMods                          // M4 map skills; neutral in M2-M3
{
    public float UncommonRareMult;               // Rare Whisperer x1.15 in M4
    public static readonly SkillMods Neutral;    // 1
    public float RarityMult(FishRarity r);       // UncommonRareMult for Uncommon and Rare, else 1
    public bool IsValid { get; }
}
public struct FishingContext                     // spec 12.4 fields
{
    public int ZoneIndex; public byte SubAreaIndex; public float Depth;   // SubAreaIndex AnySubArea = gate passes; Depth +Infinity = gate passes
    public int Minute; public Season Season; public WeatherMask Weather;
    public int RodTier; public BaitMods Bait; public bool InHotspot;
    public float Luck;                           // -1..1, size skew only
    public EventMods Events; public SkillMods Skills;
    public int PlayersInSession;
}
public interface IWorldContext                   // implemented by WorldState (T14)
{
    Season Season { get; } WeatherMask Weather { get; } int Minute { get; }
    bool IsInHotspot(float x, float z);
    // M3 additions to the spec 12.4 interface (M3-D53):
    bool CastingOpen { get; }                    // exact (unclamped) minute < DayClock.DayLengthMinutes; the host and owner cast gates read THIS, never Minute
    bool HasState { get; }                       // false until the first NetworkVariable sync (consumers fall back to defaults)
    string SpotId { get; }
    event Action Changed;                        // season, weather or hotspot changed (not per minute)
}
public struct EcologyTuning
{
    public int HotspotFirstMinMin, HotspotFirstMinMax, HotspotGapMin, HotspotGapMax, HotspotDurationMin, HotspotDurationMax;   // minutes; seeds 60/120/60/120/45/60
    public float HotspotRadiusM;                 // 3
}
public sealed class ZoneData
{
    public ZoneData(SubAreaBandData[] subAreas, SpawnRuleData[] rules, JunkEntry[] junk, in WeatherTableData weather, ShoreGrid grid);
        // ArgumentException: no sub-areas, more than MaxSubAreas, non-contiguous bands, empty junk; rules may be empty; grid may be null (tests)
    public IReadOnlyList<SubAreaBandData> SubAreas { get; } public IReadOnlyList<SpawnRuleData> Rules { get; }
    public IReadOnlyList<JunkEntry> Junk { get; } public ref readonly WeatherTableData Weather { get; } public ShoreGrid Grid { get; }
    public byte SubAreaIndexFor(float shoreM);   // band with Min <= s < Max; s < 0 -> 0; NaN or +Infinity -> last band
}
public struct SpotEcologyData { public int ZoneIndex; public byte MapOrder; }
public sealed class EcologyDatabase              // index spaces: zones by ContentRegistry zone index; spots by spot index
{
    public EcologyDatabase(FishingDatabase fishing, ZoneData[] zones, SpotEcologyData[] spots, in EcologyTuning tuning);
        // ArgumentException: null fishing, a rule species index out of range, a junk index out of range, a spot zone index out of range
    public FishingDatabase Fishing { get; } public IReadOnlyList<ZoneData> Zones { get; } public IReadOnlyList<SpotEcologyData> Spots { get; }
    public ref readonly EcologyTuning Tuning { get; }
}
public sealed class CatchLedger                  // spec 12.3 catchLimitPerRun, crew-wide; reservation semantics M3-D14
{
    public int Caught(ushort speciesIndex); public int Reserved(ushort speciesIndex);
    public bool AtLimit(ushort speciesIndex, int limit);   // limit < 0 -> false; else Caught + Reserved >= limit
    public void Reserve(ushort speciesIndex); public void Commit(ushort speciesIndex); public void Release(ushort speciesIndex);   // Commit/Release on an unreserved index throw InvalidOperationException
    public void Clear();
}
public sealed class EcologyRunState              // host only, session-level (survives Spot loads; M3-D17)
{
    public EcologyRunState(ulong runSeed);       // Season starts as the ROLLED season (same formula as WeatherDirector.RollSeason), Day 1, HostCastCounter 0
    public ulong RunSeed { get; } public Season Season { get; set; } public int Day { get; set; }   // Day starts 1
    public uint HostCastCounter { get; set; }    // FishingHostLedger increments it (never restarts on a map switch)
    public CatchLedger Catches { get; }
    // No weather track here: the track is a pure function of (runSeed, season, table), so DevWorldDriver (T14) rebuilds its
    // WeatherDirector on every Spot load and gets the same segments.
}
public sealed class ShoreGrid                    // Core/Ecology/ShoreGrid.cs - T5 owns the lookup class (ZoneData exposes it); T7 builds the data
{
    public ShoreGrid(int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm, byte[] castableBits, byte[] reachableBits);
        // bit arrays: ceil(w*h/8) bytes, LSB-first by cell index (cz * width + cx); ArgumentException on any length mismatch
    public int Width { get; } public int Height { get; } public float CellSizeM { get; } public float OriginX { get; } public float OriginZ { get; }
    public bool TryCell(float x, float z, out int cx, out int cz);   // floor((x - OriginX) / cell); false outside
    public void CellCentre(int cx, int cz, out float x, out float z);
    public bool IsWater(float x, float z);       // inside and shoreDm != ShoreDmNotWater
    public float ShoreM(float x, float z);       // shoreDm / 10; +Infinity when not water
    public bool IsCastable(int cx, int cz); public bool IsReachable(int cx, int cz);
    public void CollectReachableWaterCells(List<int> cellIndicesInto);   // castable AND reachable; hotspot candidates
    public static bool GetBit(byte[] packed, int index);
}
```
`FishingContext` defaults: tests and callers build contexts through `EcologyTestData.Context(...)` (T13) or set `Events = EventMods.Neutral`, `Skills = SkillMods.Neutral`; the resolver throws `ArgumentException` on an invalid `Events`/`Skills` (catches a forgotten neutral).

**Test fixtures:** T5 also creates `Tests/EditMode/Ecology/EcologyCoreTestData.cs` (internal static, test assembly; read-only for T6-T8; M3-D67). It is a different data set from T13's `EcologyTestData` (which builds on `FishingTestData`'s five M2 species plus one special and keeps the M2 species indices for the ledger tests); neither edits the other.

### 7.6 T6 - Core: resolver, roller, catch ledger use (`Core/Ecology/`)
```csharp
public sealed class FishingResolver
{
    public FishingResolver(EcologyDatabase db);
    public CatchResult Resolve(in FishingContext ctx, ref Pcg32 rng, CatchLedger ledger);
    public CatchResult Resolve(in FishingContext ctx, ref Pcg32 rng, CatchLedger ledger, out float weightT, out int ruleIndex);   // M3-D12; ruleIndex -1 for junk
    public bool Passes(in SpawnRuleData rule, in FishingContext ctx, CatchLedger ledger);   // spec 12.4 gate; ledger null skips the limit
    public float JunkShare(in FishingContext ctx);                                          // step 1 value
    public int CollectEligible(in FishingContext ctx, CatchLedger ledger, bool specials, List<int> ruleIndicesInto);   // validators, journal
}
```
Gate `Passes` (spec 12.4): `SubAreaIndex == AnySubArea` or mask 0 or mask has the bit; season bit; any shared weather bit; band bit of `DayClock.BandAt(Minute)`; `RodTier >= MinRodTier`; `Bait.Rating >= MinBaitRating`; `Depth >= MinDepth` (+Infinity passes); `PlayersInSession >= MinPlayersInSession`; `!ledger.AtLimit(...)` when a ledger is given.

Algorithm (spec 12.4, verbatim order) with the fixed draw order:
1. **Junk share:** `junk = clamp(subArea.JunkChance x Bait.JunkMult x (InHotspot ? HotspotJunkMult : 1) x Events.JunkMult, 0, JunkShareCap)`; draw 1 `NextFloat`; `< junk` -> junk (go to 5).
2. **Specials:** eligible special rules in array order; for each, draw 1 `NextFloat` against `min(SpecialChanceCap, SpecialChance x Bait.SpecialMult x (InHotspot ? HotspotSpecialMult : 1) x Events.RareChanceMult)`; first success wins (go to 6).
3. **Pool:** eligible non-special rules, `w = Weight x (tags & Bait.AffinityTags != 0 ? Bait.AffinityMult : 1) x Events.WeightFor(tags) x Skills.RarityMult(rarity)`. Empty pool -> junk (go to 5).
4. **Draw:** 1 `NextFloat` x sum(w), walk in array order.
5. **Junk item:** 1 `NextFloat` x sum(junk weights), walk in order; `CatchMath.Junk`.
6. **Fish:** `CatchMath.RollFish(species, index, ctx.Luck, ref rng, out t)` (1 draw; weight, quality, value spec 9.8).

Rules array order = `FishingZoneDef.rules` sorted by `fish.id` ordinal at registry build (spec 12.3). Determinism is for tests and dev seeds; the resolver runs only on the host.

```csharp
public static class FishingRoller                // replaces PlaceholderRoller (M3-D13)
{
    public static FishingRoll Roll(EcologyDatabase db, FishingResolver resolver, in FishingContext ctx, CatchLedger ledger,
                                   in RollOverrides overrides, int baitIndex, ref Pcg32 rng, out int ruleIndex);
        // Order: (a) item: resolver.Resolve (draws above) - or, when overrides force, ForcedItemIndex -> CatchMath.RollFish (1 draw, gates
        //   ignored; a forced index >= species count throws, as C2) / ForceJunk -> junk item from the zone (1 draw);
        // (b) BitePlanner.Plan(tuning, WaitMultiplier(bait.BiteSpeedMod, junk ? 1 : species.BiteWaitMod, ctx.Depth (+Inf -> 6),
        //   clamp(ctx.Events.BiteTimeMult, 0.5, 1.5), 1, ctx.InHotspot), bait.MaxNibbles) - same draws as M2;
        //   ForcedBiteWait > 0 overwrites afterwards (no nibbles); Twitchy sets HookWindow 1.0 on a fish;
        // (c) FightSeed = NextUInt (0 -> 1). SizeClass from t. Pity is set later by the ledger (no draw).
}
```

### 7.7 T7 - Core: shore grid, distance field, weather, hotspot (`Core/Ecology/`)
```csharp
public static class DistanceField
{
    public static float[] Compute(bool[] land, int width, int height);
        // two-pass chamfer, weights 1 (axis) and sqrt(2) (diagonal), in CELL units, cell centre to nearest land cell centre;
        // +Infinity where no land cell exists; ArgumentException on size mismatch (spec 12.5)
}
// ShoreGrid (the lookup class) is T5's (7.5); T7 produces its arrays.
public static class ShoreGridMath
{
    public static ushort[] ShoreDecimetres(bool[] water, bool[] land, int width, int height, float cellSizeM);
        // water cells: round(max(0, DistanceField - 0.5) * cellSizeM * 10), clamped to ShoreDmMax; non-water ShoreDmNotWater (M3-D7)
    public static byte[] ReachableBits(int width, int height, float cellSizeM, float originX, float originZ, byte[] castableBits,
                                       IReadOnlyList<float> standXZ, float minM, float maxM);
        // a castable cell is reachable when its centre's horizontal distance to some stand point (x0, z0, x1, z1, ...) is in [minM, maxM] (M3-D6)
    public static int[] CountReachablePerSubArea(ShoreGrid grid, ZoneData zone);   // index = sub-area
    public static byte[] PackBits(bool[] bits);   // inverse of ShoreGrid.GetBit
}
public sealed class WeatherDirector                // spec 12.2, R50; host plain class
{
    public WeatherDirector(in WeatherTableData table, Season season, ulong runSeed);   // pre-rolls 35 segments at once
    public IReadOnlyList<WeatherMask> Track { get; }   // single-bit masks; grows by 5 when a day >= 8 is requested
    public WeatherMask At(int day, int minute);    // day >= 1; EnsureDay(day) first; segment DayClock.SegmentAt(minute)
    public void EnsureDay(int day);                // day 8+: continue from the previous segment with Rng.Stream(runSeed, Weather, day), one day at a time, in order
    public static void RowProbabilities(in WeatherTableData t, Season s, WeatherMask current, float[] into3);   // column multipliers then per-row renormalisation
    public static WeatherMask Next(in WeatherTableData t, Season s, WeatherMask current, ref Pcg32 rng);         // 1 NextFloat
    public static Season RollSeason(ulong runSeed); // Rng.Stream(runSeed, Season, 0).Range(0, 4) (one draw)
}
// Planned track: rng = Rng.Stream(runSeed, Weather, 0); previous = Clear; for 35 segments: previous = Next(..., previous, ref rng); store.
// M3 produces only Clear, Rain, Fog (M5 adds Storm with Rain and Heatwave with Clear as extra bits).

public struct HotspotData { public bool Active; public float X, Z, Radius; public int StartMinute, EndMinute; }
public sealed class HotspotPlanner                 // spec 12.7; host plain class, one per (day, spot)
{
    public HotspotPlanner(in EcologyTuning tuning, ShoreGrid grid, ulong runSeed, int day);
        // rng = Rng.Stream(runSeed, Hotspot, day); candidates = grid.CollectReachableWaterCells; none -> never spawns
    public HotspotData Current { get; }
    public int NextSpawnMinute { get; }            // int.MaxValue when none
    public bool Advance(int minute);               // true when Current changed; at most one hotspot; draw order per spawn: (1) delay
                                                   // Range(first or gap min, max + 1) when scheduling, (2) cell index, (3) duration Range(min, max + 1)
    public HotspotData ForceAt(float x, float z, int minute);   // dev "spawn hotspot here": replaces Current, duration = DurationMax, no draws
    public static bool Contains(in HotspotData h, float x, float z);   // Active && horizontal distance <= Radius
}
```

### 7.8 T8 - Core: journal, biting now, day-night and silhouette math
```csharp
// Core/Journal/MetaSave.cs - spec 12.10 shape (Newtonsoft in Runtime serializes it; no Unity types)
[Serializable] public sealed class MetaSave { public int SchemaVersion = 1; public Dictionary<string, JournalEntry> Journal = new Dictionary<string, JournalEntry>(); }
[Serializable] public sealed class JournalEntry
{
    public int Seen; public int CrewLanded; public int Caught;
    public int BestWeightG; public ushort BestLengthMm; public byte BestQuality;
    public List<string> SeenInSpots = new List<string>();
}
public struct JournalUpdate { public bool Changed; public bool FirstCatch; public bool PersonalBest; public int LevelBefore, LevelAfter; }
public static class JournalRules
{
    public static bool ShouldJournal(AnnounceKind kind, bool isJunk);   // Landed, Lost, ReleasedCreelFull and !isJunk; PityCast, None never (M3-D25)
    public static int HintLevel(JournalEntry e);   // null -> 0; Seen >= 1 -> 1; CrewLanded >= 1 -> 2; Caught >= 3 -> 3 (highest satisfied)
    public static JournalUpdate Apply(MetaSave save, string speciesId, string spotId, AnnounceKind kind, bool isJunk, bool localCatcher,
                                      int weightG, ushort lengthMm, CatchQuality quality);
        // fish Landed/ReleasedCreelFull/Lost: Seen++, add spotId once; Landed/ReleasedCreelFull: CrewLanded++; and localCatcher: Caught++
        // and bests (weight decides; length and quality follow the heavier fish). FirstCatch = Caught went 0 -> 1; PersonalBest = Caught >= 2
        // and weight beat the old best. Unknown entries in the dictionary are kept untouched.
    public static int CompletionCount(MetaSave save, IReadOnlyList<string> journalSpeciesIds);   // entries with Caught >= 1 (spec: of 25)
}
public static class BitingNow                    // spec 12.10 badge (level >= 2); every client
{
    public static bool IsBiting(FishingResolver resolver, ZoneData zone, ushort speciesIndex, in FishingContext ctx);
        // any rule of the species in this zone passes with ctx.SubAreaIndex forced to AnySubArea, Depth +Infinity, ledger null (M3-D26)
}
public static class JournalHints                 // level 2 icons: union over every rule of the species in every zone
{
    public static void Summarize(EcologyDatabase db, ushort speciesIndex, out SeasonMask seasons, out WeatherMask weather,
                                 out TimeBandMask bands, List<int> zoneIndicesInto);
}

// Core/Render/DayNightMath.cs - spec 10.3; pure
public struct PhaseBlend { public int From, To; public float T; }   // T 0..1 smoothstep weight of To
public static class DayNightMath
{
    public const float ReadabilityDistanceM = 25f, ReadabilityMinFactor = 0.5f;
    public static PhaseBlend Blend(float dayFraction, IReadOnlyList<float> phaseStarts, float window);
        // phase i holds from start[i]; the change into phase i runs over [start[i], start[i] + window] with smoothstep (M3-D24);
        // continuous in dayFraction (no pops), From == To away from boundaries
    public static void SunDirection(float dayFraction, float sunriseFraction, float sunsetFraction, float minElevationDeg,
                                    out float x, out float y, out float z);   // unit vector TO the light; east -> west arc; elevation >= min while up
    public static float SunWeight(float dayFraction, float sunriseFraction, float sunsetFraction, float flipWindow);   // 1 by day; 0 at the flip
    public static float MoonWeight(float dayFraction, float sunriseFraction, float sunsetFraction, float flipWindow);
    public static float FogFactor(float density, float distanceM);   // ExponentialSquared: exp(-(density * distance)^2)
    public static bool IsReadable(float density);  // FogFactor(density, 25) >= 0.5  <=>  density <= ~0.0333
}
// Core/Render/SilhouetteMath.cs - journal icon outline (IconFactory rasterises it; M3-D29)
public static class SilhouetteMath
{
    public static float HalfHeight(BodyShape shape, float lengthShapeK, float u01);   // body half-height at u along the length, 0 at both tips, > 0 inside
    public static float AspectRatio(BodyShape shape, float lengthShapeK);             // length / height of the icon
}
```

### 7.9 T9 - Data: ecology defs and registry (asm FishGame.Data)
Defs follow C2 7.13 rules (private `[SerializeField]` fields named as below, PascalCase getters, `Configure(...)` in field order, `ToCore()` where a Core mirror exists, no tuning initializers). **Serializable helper types** (`[Serializable]` structs and classes: `SubAreaBand`, `SpawnRule`, `JunkWeight`, `SunMoonSettings`, `WeatherAmbient`, `SeasonAmbience`) use PUBLIC camelCase fields exactly as named in the table (`band.id`, `rule.fish`, `weather.fogDensityMult`, `ambience.windGain`; the M2 `SfxCueRow` precedent) and have no PascalCase properties. Sections E (T16/T17 read sub-area ids), F (T18/T19 read looks and ambience) and G (T21 level 1) write these names (M3-D76).

| Def / type (file) | Fields (order) | Core |
|---|---|---|
| `Ecology/FishingZoneDef` | `id, displayName, subAreas (List<SubAreaBand>), rules (List<SpawnRule>), junk (List<JunkWeight>), weather (WeatherTableDef), shoreGrid (ShoreGridDef)` | `ZoneData ToCore(ContentRegistry registry)` (resolves species/junk indices; sub-area ids to bits; rules sorted by fish id ordinal) |
| `Ecology/SubAreaBand` (`[Serializable]` struct) | `id, minShoreM, maxShoreM, junkChance` (`maxShoreM <= 0` on the last band = open-ended) | `SubAreaBandData` |
| `Ecology/SpawnRule` (`[Serializable]` class) | `fish (FishSpeciesDef), subAreaIds (string[]), seasons (SeasonMask), weather (WeatherMask), bands (TimeBandMask), weight, minRodTier (byte), minBaitRating, minDepth, special, specialChance, catchLimitPerRun, minPlayersInSession` | `SpawnRuleData` |
| `Ecology/JunkWeight` (`[Serializable]` struct) | `junk (JunkDef), weight` | `JunkEntry` |
| `Ecology/WeatherTableDef` | `id, baseMatrix (float[9]), seasonColumnMult (float[12])` | `WeatherTableData ToCore()` |
| `Ecology/ShoreGridDef` | `id, width, height, cellSizeM, originX, originZ, shoreDm (ushort[]), castableBits (byte[]), reachableBits (byte[]), standPoints (Vector3[]), bakedFromScene (string)` | `ShoreGrid ToCore()` (null when width == 0: not baked) ; `void WriteBake(...)` (Editor-callable, used only by `ShoreGridBaker`) |
| `Ecology/EcologyTuningDef` | `hotspotFirstMinMin, hotspotFirstMinMax, hotspotGapMin, hotspotGapMax, hotspotDurationMin, hotspotDurationMax, hotspotRadiusM` | `EcologyTuning ToCore()` |
| `SpotDef` (edit) | appended `mapOrder (int), zone (FishingZoneDef)`; new `Configure(id, displayName, sceneName, mapOrder, zone)`; the M1 3-arg `Configure` stays | - |
| `Art/ArtPaletteDef` (edit) | appended `sunMoon (SunMoonSettings), weatherLooks (WeatherAmbient[3])` (index = Clear 0, Rain 1, Fog 2) + getters + `ConfigureM3(in SunMoonSettings, WeatherAmbient[])` | - |
| `Art/SunMoonSettings` (`[Serializable]` struct) | `sunriseFraction (0), sunsetFraction (0.70), minElevationDeg (12), flipWindow (0.02), moonIntensity (0.25), moonEulerDeg (Vector3), moonTint (Color), blendWindow (0.05)` | - |
| `Art/WeatherAmbient` (`[Serializable]` struct) | `fogDensityMult, fogDensityMin, lightMult, desaturate, rainAmount` (fog density = max(phase fog x fogDensityMult, fogDensityMin), capped readable; seeds Clear 1/0/1/0/0, Rain 1.2/0/0.85/0.1/1, Fog 1/0.03/0.9/0.15/0) | - |
| `Audio/SfxCueTableDef` (edit) | appended `seasonAmbience (SeasonAmbience[4])` | - |
| `Audio/SeasonAmbience` (`[Serializable]` struct) | `windGain, windLfoHz, lapIntervalMin, lapIntervalMax` | - |

```csharp
// Data/ContentRegistry.Ecology.cs (partial; T9)
public const string CategoryZones = "zones";
[SerializeField] List<FishingZoneDef> zones; [SerializeField] EcologyTuningDef ecologyTuning;
public IReadOnlyList<FishingZoneDef> Zones { get; } public EcologyTuningDef EcologyTuning { get; }
public void ConfigureEcology(IEnumerable<FishingZoneDef> zones, EcologyTuningDef tuning);   // builder + tests
public ushort ZoneIndex(string id); public FishingZoneDef ZoneAt(ushort index);
public bool TryGetSpotByScene(string sceneName, out SpotDef spot);   // ordinal; null -> false
public EcologyDatabase Ecology { get; }   // built in Build() when zones is non-empty and Fishing exists; null otherwise
```
`Validate()` adds: zone ids valid/unique; every zone has sub-areas, junk, a weather table; every rule `fish` non-null and in `species`; every junk entry in `junk`; spot `zone` non-null when zones exist and in `zones`; `ecologyTuning` present when zones exist. Data-quality rules (ranges, specials, contiguity) are `EcologyValidators` (T11), not registry validation. `ComputeContentHash` adds `zones` when non-empty.

### 7.10 T10 - Content: seeds and content builder (asm FishGame.Editor)
```csharp
public static class EcologySeedData            // Editor/Builders/Ecology/EcologySeedData.cs (+ partial EcologySeedData.Roster.cs)
{
    public static IReadOnlyList<SpeciesSeed> Species { get; }        // the 25 of spec 12.6 (C2 SpeciesSeed shape + Tags); model files per M3-D33
    public static IReadOnlyList<(string id, string name, int value, BodyShape shape)> Junk { get; }   // 7 (M3-D34)
    public static IReadOnlyList<ZoneSeed> Zones { get; }             // lake_dock, harbour_pier: sub-areas (12.5), junk lists (12.6), rules
    public static WeatherTableData Weather { get; }                  // 12.2
    public static EcologyTuning Tuning { get; }                      // 60/120/60/120/45/60, radius 3
    public static SunMoonSettings SunMoon { get; } public static WeatherAmbient[] WeatherLooks { get; } public static SeasonAmbience[] SeasonAmbience { get; }
    public static EcologyDatabase BuildDatabase();                   // test convenience straight from seeds (grids null)
}
public readonly struct ZoneSeed { public readonly string Id, Name; public readonly SubAreaBandData[] Bands; public readonly string[] BandIds;
                                  public readonly (string junkId, float weight)[] Junk; public readonly RuleSeed[] Rules; }
public readonly struct RuleSeed { /* species id, sub-area ids, masks, weight, minRodTier, special, specialChance, catchLimitPerRun */ }
public static class EcologyContentBuilder       // Editor/Builders/Ecology/EcologyContentBuilder.cs; ContentBuilder hook after FishingContentBuilder
{
    public static void Build();                 // create-if-missing: weather table, zones, empty grids, tuning, harbour SpotDef (M3-D63);
                                                // fills lake_dock zone/mapOrder; registry lists; ArtPalette/SfxCueTable M3 fields
                                                // when the M3 fields are still default (allowed in-place fill, logged).
                                                // It does NOT create species or junk assets: FishingContentBuilder stays their only creator
                                                // (it reads FishingSeedData.Species/Junk, which forward to this class's seeds). Build only
                                                // checks that every species and junk asset its zones reference exists, else
                                                // InvalidOperationException naming the missing asset (M3-D62).
    internal static void BuildAt(string contentRoot, ContentRegistry registry);   // test seam (Tests/__Temp)
}
```
Roster numbers are spec 12.6 (rows 1-23, specials, junk); seeds reference the table, never re-derive. Rows naming two zones (silver_mullet) become one rule per zone. "Any" weather = `WeatherMask.All`; specials: all gates (`All` masks, rod 0), `special` true, `catchLimitPerRun` 1, Bulldog, Legendary, `isSpecial` true.

### 7.11 T11 - Coverage (asm FishGame.Editor + EditMode tests)
```csharp
public static class EcologyValidators           // Editor/Validation/EcologyValidators.cs
{
    public static List<string> All();           // DataRules + Coverage (V1-V3) + Variety; prefixed "Ecology: "
    public static List<string> MapChecks();     // T16 adds: Reachability (the zone of each REGISTERED spot, M3-D83) + WaterLayerOnlyWater + WorldStateHashes + lake layer split; prefixed "EcologyMaps: "
    internal static List<string> DataRules(ContentRegistry registry);
    internal static List<string> Coverage(EcologyDatabase db, int neutralBaitRating, float neutralDepth);
    internal static List<string> Variety(EcologyDatabase db);
    [MenuItem("FishGame/Ecology/Print Coverage Report")] public static void PrintCoverageMenu();
}
```
Rules in section 11.

### 7.12 T12 - Balance reports (`Tests/EditMode/Balance/EcologyBalanceReports.cs`, `[Category("Balance")]`)
Section 11.4. No production code: the optional `Core/Ecology/ExpeditionSim.cs` is not created (both reports live in one test file and share private helpers; section C plan). T12 measures the Rhythm-bot fight medians itself on all four rods through the R72 bot (internal members, M3-D60/D64).

### 7.13 T13 - Host roll: ledger on the resolver (asm FishGame.Core)
```csharp
// Core/Fishing/HostCastView.cs (edit)
public struct HostCastView
{
    public float PlayerX, PlayerY, PlayerZ; public WaterProbe Probe;
    public bool CastingAllowed;                  // gameplay scene AND IWorldContext.CastingOpen (M3); M4 adds run phase Day
    public bool GridWater;                       // ShoreGrid.IsWater(landing) on the host (M3)
    public FishingContext Context;               // world part filled by FishingHost; the ledger overwrites RodTier, Bait and Depth (M3-D15)
}
// Core/Fishing/FishingHostLedger.cs (edit)
public FishingHostLedger(EcologyDatabase db, EcologyRunState run, ushort defaultRodIndex, byte defaultBaitIndex);   // replaces the M2 ctor
public EcologyRunState Run { get; }             // RunSeed == Run.RunSeed; HostCastCounter == Run.HostCastCounter
//   RequestCast: after validation, Run.HostCastCounter++, rng = Rng.Stream(Run.RunSeed, CatchStreamId, counter);
//   ctx = view.Context with RodTier = rod tier of the slot's host rod, Bait = slot bait Mods(1), Depth = view.Probe.Depth;
//   roll = FishingRoller.Roll(db, resolver, ctx, Run.Catches, overrides, baitIndex, ref rng, out ruleIndex);
//   a roll of a species whose rule has CatchLimitPerRun >= 0 -> Run.Catches.Reserve(species) and ActiveCast remembers it.
//   First resolution: Landed or ReleasedCreelFull -> Commit; any other outcome (incl. Cancelled, Invalid, Timeout, Disconnect) -> Release.
// Core/Fishing/ActiveCast.cs (edit): + public bool LimitReserved;
// Core/Fishing/CatchValidator.cs (edit): ValidateCast order: CastingAllowed (NotAllowedNow), ActiveCast, StaleCastId, BadCharge, charge accepted,
//   OriginTooFar, yaw, NoBait, CastMath.ProbeReason, then !GridWater -> NotWater (M3-D16).
```
- `PlaceholderRoller`, `RollRequest` and their tests are removed (`git rm`); `FishSpeciesData.MinBaitRating/MinDepth` and the matching `FishSpeciesDef` fields are removed (M3-D3); `FishingValidators` drops the species reachability-by-bait check (V3 replaces it).
- **Compile fallout (M3-D55; the primary option is accepted, the keep-the-fields fallback is not used):** the removals break the callers listed in 2.6 (`FishingTestData` two initializers, `FishingDefsTests`, `FishingValidatorTests`, `FishingContentTests`, `FishingRollValidationTests`, `FishingWireTests`, the `Configure` callers in `Editor/Builders/Ecology/*` and `FishingContentBuilder`, comments in `BitePlanner`/`RollOverrides`/`FishingRoll`). T13 fixes them mechanically with the public surface frozen: test names are kept, and two tests are DELETED because V3 (`EcologyCoverage_V3_EverySpeciesPassesSomewhereAtRodThree`) replaces them: `FishingValidators_UnreachableSpecies_IsReported` and `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth`. The species `.asset` files keep stale `minBaitRating:`/`minDepth:` YAML keys until Unity re-saves them (ignored; nobody hand-edits assets).
- `CastVerdict`, `CommitDecision`, `FishingRoll`, wire structs: unchanged.
- **Interim `FishingHost` (T13 compile fix, replaced by T15):** the ledger is built with `new EcologyRunState(RunSeed)` held by the `FishingHost` and the registry's `Ecology` database; every cast gets `GridWater = true`, `CastingAllowed = true` and an interim context: zone = `lake_dock`, `SubAreaIndex = 1` (open_water), Season Summer, Weather Clear, Minute 300 (Midday), `InHotspot` false, `Luck` 0, neutral events/skills, `PlayersInSession` 1. Every C2 PlayMode cast test keeps landing (bluegill is eligible in open_water, Summer, Midday) and `test-play` stays green until T15 replaces the interim with the world-built context.

### 7.14 T14 - World state, wire, dev world driver (asm FishGame.Gameplay)
```csharp
// Gameplay/World/ClockAnchor.cs - wire mirror (C2 G1)
public struct ClockAnchor : INetworkSerializeByMemcpy, IEquatable<ClockAnchor>
{
    public double AnchorServerTime; public float AnchorMinute; public float MinutesPerSecond; public byte Running;
    public static ClockAnchor From(in ClockAnchorData d); public ClockAnchorData ToCore();
    public bool Equals(ClockAnchor o); public override int GetHashCode();
}
// Gameplay/World/HotspotState.cs - spec 12.7 shape
public struct HotspotState : INetworkSerializeByMemcpy, IEquatable<HotspotState>
{
    public float X, Z, Radius; public int EndMinute; public byte Active;
    public static HotspotState From(in HotspotData d); public HotspotData ToCore();   // StartMinute is not replicated (0 on clients)
    public bool Equals(HotspotState o); public override int GetHashCode();
}
// Gameplay/World/WorldState.cs
// Authority: host writes every NetworkVariable (season, weather, clock anchor, hotspot); every peer reads. Scene-placed in each Spot scene
// (first in-scene NetworkObject, 2.8 #24). Registers itself as IWorldContext in ServiceRoots.Game (Replace) on spawn. ServiceRegistry has
// no remove method, so "unregisters on despawn" means: on despawn HasState becomes false and Current is cleared; the registry entry
// dies with the scene's GameScope (consumers check HasState; M3-D56).
public sealed class WorldState : NetworkBehaviour, IWorldContext
{
    [SerializeField] private string spotId;                     // the field NAME is fixed: WorldSceneObjects writes it with FindProperty("spotId") (M3-D59)
    public readonly NetworkVariable<byte> NetSeason;            // Season; Everyone / Server
    public readonly NetworkVariable<byte> NetWeather;           // current segment WeatherMask; Everyone / Server
    public readonly NetworkVariable<ClockAnchor> NetClock;      // Everyone / Server; written only on start, stop, pause, reset, rate change (14.2)
    public readonly NetworkVariable<HotspotState> NetHotspot;   // Everyone / Server
    public static WorldState Current { get; }
    public string SpotId { get; }                               // serialized; set by WorldSceneObjects
    public float MinuteF { get; }                               // ClockMath.MinuteAt(NetClock, NetworkManager.ServerTime.Time)
    // IWorldContext: Season, Weather, Minute (floor of MinuteF clamped 0..1199, for band and segment lookups), CastingOpen (MinuteF < 1200), IsInHotspot, HasState, SpotId, Changed
    public event Action ClockChanged;                           // anchor rewritten
    // Host-only writes (no-ops with a LogOnce on clients):
    public void ServerSetSeason(Season s); public void ServerSetWeather(WeatherMask w); public void ServerSetClock(in ClockAnchorData a);
    public void ServerSetHotspot(in HotspotData h);
    // OnNetworkSpawn (server) calls the internal DevWorldDriver.ServerInitialize(this) on the same GameObject, so WorldSceneObjects must add
    // WorldState and DevWorldDriver to the one "World" root (6.3; M3-D59).
}
// Gameplay/World/DevWorldDriver.cs
// Authority: host only (inert on clients). M3 dev clock + weather track + hotspot director; M4's RunDirector replaces it.
public sealed class DevWorldDriver : MonoBehaviour
{
    public EcologyRunState Run { get; }                         // the session's run state (created on first host spawn in a session; reused across Spot loads)
    public bool ClockRunning { get; } public WeatherMask WeatherOverride { get; }   // None = follow the track
    public WeatherDirector Weather { get; }                     // rebuilt from (Run.RunSeed, Run.Season, zone weather table) on every Spot load and season change
    public void DevSetSeason(Season s); public void DevSetMinute(int minute); public void DevSetWeatherOverride(WeatherMask single);   // minute 0..1200; None clears
    public void DevNewDay();                                    // Day++, minute 0, new HotspotPlanner(day), weather from the track
    public void DevSetClockRunning(bool running); public void DevSpawnHotspotAt(float x, float z);
    public void DevScrub(bool held);                            // F8 held: rate x60 while held (re-anchored on change)
}
// Gameplay/World/EcologySession.cs - host-side holder of the session's EcologyRunState in ServiceRoots.App (M3-D17)
public static class EcologySession { public static EcologyRunState GetOrCreate(ulong runSeed); public static EcologyRunState Current { get; } public static void Clear(); }
    // cleared when the session ends (INetworkSession.Ended) and on SubsystemRegistration.
    // GetOrCreate returns the existing run state and IGNORES the seed; otherwise it creates one (season rolled by RollSeason(seed), Day 1).
    // A dev seed change is Clear() + GetOrCreate(newSeed): FishingHost.SetRunSeed (allowed only while HostCastCounter == 0) does exactly that.
    // Gameplay does not reference Fishing, so DevWorldDriver cannot read FishingHost.RunSeed: it calls GetOrCreate(FishingConstants.DefaultDevRunSeed)
    // and adopts any new instance BY REFERENCE (season, track, clock at minute 0, planner). M3 keeps M2's fixed dev run seed (M3-D57).
```
Host behaviour (`DevWorldDriver`): on the first host spawn of a session `EcologySession.GetOrCreate(FishingConstants.DefaultDevRunSeed)` (or the instance `SetRunSeed` created); season = the run state's season (rolled by `WeatherDirector.RollSeason(runSeed)` at creation); `Weather = new WeatherDirector(zone weather, season, runSeed)` (rebuilt, not stored in the run state); clock anchor at minute 0, rate `DayClock.DefaultMinutesPerSecond`, running; each frame: minute -> segment -> `ServerSetWeather` on change; `HotspotPlanner.Advance(minute)` -> `ServerSetHotspot` on change; when the clock is RUNNING and reaches 1200, auto `DevNewDay()` (M3-D11); a clock stopped by the dev panel (or a test) at 1200 stays there with `CastingOpen` false; solo pause (`IPauseState.Paused`) re-anchors with `Running = false` and back on resume (R34). A Spot load keeps season, day, cast counter and catch ledger (run state), rebuilds the identical weather track and resumes the clock from minute 0 of the current day (M3-D31).

`BuildInfo.ProtocolVersion = 4` (R72 already moved it 2 -> 3).

### 7.15 T15 - Fishing host and owner integration (asm FishGame.Fishing, App)
- `FishingHost`: builds `HostCastView.Context` from `WorldState` (Season, Weather, Minute, `IsInHotspot(landing)`), the zone of `WorldState.SpotId` (`ZoneIndex`, `SubAreaIndex = zone.SubAreaIndexFor(grid.ShoreM(landing))`), `Luck = FishingDebugSettings.Luck`, `PlayersInSession` = connected roster count, `Events/Skills` neutral; `GridWater = grid.IsWater(landing)`; `CastingAllowed = WorldState.CastingOpen`. Creates its ledger with `EcologySession.GetOrCreate(RunSeed)` (cast counter persists; `SetRunSeed` before the first cast of the SESSION creates a fresh run state). No world, zone or grid (a non-ecology scene or a missing bake) -> logs once and rejects casts `NotAllowedNow` (never rolls without a context).
- `FishingRodController` (owner marker): valid = `CastProbe` valid AND `grid.IsWater(marker)` AND `WorldState.CastingOpen` (M3-D16). The owner reads the same `ShoreGridDef` asset and the replicated clock.
- `FishingDebugSettings.Luck { get; set; }` (-1..1, clamped; dev only; reset with the others).
- `Runtime/Services/DevLoadSpotRequested.cs`: `public readonly struct DevLoadSpotRequested { public readonly string SpotId; ... }` (EventBus message). `SceneFlow` (host, `Debug.isDebugBuild`) loads the spot's `sceneName` with the NetworkSceneManager (Single); clients ignore it. The late-joiner `OnLoadComplete` path is unchanged.

### 7.16 T16 - Lake map: stand points, shore-grid bake, world placement (asm FishGame.Editor, Gameplay)
```csharp
// Gameplay/World/StandPoint.cs - Authority: none (static marker read by the editor bake; no collider; layer Default)
public sealed class StandPoint : MonoBehaviour { public float FacingDeg { get; } public void Configure(float facingDeg); }
// Editor/Builders/Ecology/WorldSceneObjects.cs
public static class WorldSceneObjects
{
    public static void AddTo(Scene scene, string spotId);                      // root "World": NetworkObject + WorldState (spotId) + DevWorldDriver; SetDirty after OnValidate
    public static void AddStandPoints(Scene scene, IReadOnlyList<Vector4> xyzFacing);   // root "StandPoints"
    internal static void FinalizeAfterSave(Scene scene, string scenePath);              // M3-D82: NGO derives GlobalObjectIdHash from the saved scene, so after
        // BuilderUtil.SaveScene it runs NetworkObject.OnValidate by reflection on the World object, checks the hash is non-zero, SetDirty and saves the scene a
        // second time; LakeDockBuilder (a T16 hook line after SaveScene) and T17's HarbourPierBuilder both call it. AddTo keeps its signature and still calls OnValidate + SetDirty.
}
// Editor/Builders/Ecology/ShoreGridBaker.cs
public static class ShoreGridBaker
{
    public static void BakeOpenScene(Scene scene, ShoreGridDef target, float minLandDepth);   // M3-D7: Physics.SyncTransforms; per cell centre a down ray
        // from y 30 over 80 m against Water|Shore|Ground with QueryTriggerInteraction.Collide; nearest hit Water -> water, Shore/Ground -> land,
        // no hit -> void; castable = water AND CastProbe.Probe(cell centre) valid at minLandDepth; ShoreGridMath for shoreDm and reachable bits
        // (stand points from the scene's StandPoint markers, ring EcologyConstants.ReachMinM..ReachMaxM); target.WriteBake(...) in place; SetDirty; SaveAssets
    [MenuItem("FishGame/Ecology/Bake Shore Grids")] public static void BakeAllMenu();   // opens each registered Spot scene SINGLY, one at a time (M3-D86); BakeOpenScene throws if another loaded scene holds a WaterVolume
}
```
`LakeDockLayout.StandPoints` (T16): dock end (0, 0.65, -40.5) facing 0; dock middle (0, 0.65, -46) facing 90 and 270; ramp top (12, 0.45, -50.5) facing 0; six bank points at r 51.5, angles 30, 90, 150, 210, 290, 330 deg measured from +x toward +z (290 instead of 270 keeps clear of the dock at 270; ground y = `ShoreY(51.5) + 0.05`), facing the lake centre. The lake grid is 120 x 120 cells of 1 m, origin (-60, -60) (M3-D8).

**Bake order (review F1; supersedes any "bake before the save" wording).** In `LakeDockBuilder.Build()` and `HarbourPierBuilder.Build()` the sequence after the hooks is `BuilderUtil.SaveScene(...)` -> `WorldSceneObjects.FinalizeAfterSave(scene, scenePath)` -> `Physics.SyncTransforms()` -> `ShoreGridBaker.BakeOpenScene(scene, grid, minLandDepth)`. The bake runs on the SAVED scene (its name becomes `bakedFromScene`; an untitled scene has none) and writes only the grid asset, never the scene, so the scene needs no third save.

T16 also adds `Tests/PlayMode/EcologyMapPlayTests.cs` with `EcologyMap_LakeSolo_WorldStateSpawnsWithState` (M3-D84): a PlayMode proof that the first in-scene NetworkObject spawns with state before T15 relies on it (T24's `EcologySolo_WorldState_SyncedWithNonZeroHash` comes much later).

### 7.17 T17 - Harbour Pier map (asm FishGame.Editor)
```csharp
public static class HarbourPierBuilder { public static void Build(); }   // Editor/Builders/Harbour/; SceneBuilder hook; deterministic, seeded
internal static class HarbourPierLayout { /* numbers (M3-D40), stand points, seeds */ }
internal static class HarbourPierMeshes { /* procedural meshes per the R74 standard; saved in place (GUIDs kept) */ }
```
Every hook of 6.3. Layout M3-D40; art per R74. `HarbourPierBuilder.Build()` calls `WorldSceneObjects.FinalizeAfterSave(scene, AssetPaths.HarbourPierScene)` after its own `SaveScene` (M3-D82). T17 also appends `EcologyMap_HarbourDevLoad_SpawnsOnPierAndProbesEverySubArea` to `Tests/PlayMode/EcologyMapPlayTests.cs`, which publishes T15's `DevLoadSpotRequested` from the test (the harbour is otherwise unreachable in play mode until T22's button; M3-D84), and edits `Tests/EditMode/SceneNamesTests.cs` (M3-D85).

### 7.18 T18 - Day-night and seasonal tint (asm FishGame.Presentation)
```csharp
// Presentation/World/DayNightController.cs
// Authority: local only (every peer); reads IWorldContext (minute, season, weather), never writes gameplay state.
public sealed class DayNightController : MonoBehaviour
{
    public float DayFraction { get; }            // from WorldState minute; the editor/dev override wins when set
    public void SetPreviewMinute(float minute);  // editor and tests; NaN clears
    // Every frame (no allocation): DayNightMath.Blend over ArtPaletteDef.Phases (blendWindow from SunMoonSettings); sun/moon on the single
    // directional light (rotation, intensity, colour; the main light never swaps); _ToonShade, _ToonAmbient, sky globals, _FGDayFraction;
    // RenderSettings.fogColor/fogDensity (fog stays on; density = min(MaxReadableFogDensity 0.0333, max(phase fog x look.fogDensityMult, look.fogDensityMin))), the cap a safety net only);
    // WarmVolume / CoolVolume weights from the phase blend (warmWeight, coolWeight).
    // DayNightController also OWNS the weather look (CCR-F2, M3-D79): it reads IWorldContext.Weather (Fog bit -> look 2, else Rain bit -> 1, else Clear 0), cross-fades the
    // WeatherAmbient over 4 s (linear, from the current blended look; the first look after HasState snaps), and applies lightMult, desaturate and the fog terms.
    // It exposes the blended look to its assembly: internal WeatherAmbient ActiveLook and internal Color ParticleTint. No public surface changes.
}
```
`PaletteGlobals` (edit; CCR-F1, M3-D78): pushes `_FGPalette` for `IWorldContext.Season` on enable and on a real season change (`Changed` with a new season), never per frame, and pushes `_FGSeasonTint` (`SeasonTint[i] = season[i] / base[i]` per channel, linear, base = `ArtPaletteDef.M2Season` = Summer; clamped 0..2; length 16; exactly 1 when the season is the base) as a DORMANT hook that no M3 shader reads. **Under the adopted R74 standard the season tint on textured environment albedo IS the `_FGPalette` season swap** (palette hue x value-only detail texture, checked against the merged `Toon.shader`); full-colour painted albedo (fish skin array, cloth, skin) is season-invariant (style guide 2.3). T18 adds NO shader term, and an opt-in per-material `_FGSeasonTintWeight` term in `Toon.shader` is not used (CCR accepted 2026-10-13); the first task that needs a full-colour, season-following material adds that opt-in term and its test. With no world context (menus, edit mode) it uses `M2Season` and pushes phase 1 as today; in play mode, when an enabled `DayNightController` shares the root, the phase colours, sky colours and fog belong to it. **Seasonal trees (2026-10-13, after this contract):** `Apply()` already calls `SeasonalVisibility.SetSeason(season)` (Runtime) right after the palette push; T18 keeps that call on every push (including the `HandleWorldChanged` re-apply), so bare winter trees, spring blossoms and snowy pines follow the world season with no extra wiring.

### 7.19 T19 - Weather looks, hotspot VFX, seasonal ambience
```csharp
public sealed class WeatherFx : MonoBehaviour  // Presentation/World; local only. Rain: camera-local rain particles (URP particles) and the Rain_Patter loop
{ }                                             //   (2D; its follow is the WeatherFx transform, so a camera switch never kills it), driven by DayNightController.ActiveLook.rainAmount.
                                                //   The 4 s cross-fade and the light/fog parts of the weather look (lightMult, desaturate, fog terms) belong to
                                                //   DayNightController (T18, CCR-F2, M3-D79); WeatherFx only reads its blended look.
public sealed class HotspotFx : MonoBehaviour  // Presentation/World; local only. NetHotspot active: bubbling splash particles at (x, z) and a ripple
{ }                                             //   every 1.2 s through RippleField (strength 0.4); every client.
```
`AmbienceBed` (edit): wind gain / LFO and lap interval from `SfxCueTableDef.SeasonAmbience[(int)season]` (via `IWorldContext`, re-read on `Changed`); no world -> M1 values. `SfxRecipes.M3.cs`: `Rain_Patter` (filtered noise loop, 4 s, whole-period seam, crossfade 20 ms; spec 11.7 recipe rules).

### 7.20 T20 - Save service and journal recorder
```csharp
// Runtime/Save/SaveService.cs
public enum MetaLoadSource : byte { Defaults = 0, Main = 1, Backup = 2 }
public sealed class SaveService
{
    public const string MetaBaseName = "meta";
    public SaveService(string filePath);
    public static SaveService CreateForThisInstance();          // Application.persistentDataPath/meta{InstanceProfile.Current.Suffix}.json
    public string FilePath { get; } public MetaSave Current { get; } public MetaLoadSource LoadedFrom { get; }
    public event Action<MetaSave> Changed;
    public void Load();                                         // main -> .bak -> defaults; logs which (spec 12.10); a newer SchemaVersion than 1 loads read-only and is never overwritten (logged once)
    public void Save();                                         // AtomicFile.WriteAllText (spec 12.10); IO errors log a warning once, never throw into gameplay
    public void NotifyChanged();
    internal static string Serialize(MetaSave save);            // Newtonsoft; camelCase property names, dictionary keys unchanged (M3-D30)
    internal static MetaSave DeserializeOrNull(string json);    // null on malformed JSON
}
// Fishing/Journal/JournalRecorder.cs
// Authority: local only (every peer). Scene object on "Fishing". Applies every CatchAnnounced (App EventBus) to the local SaveService.
public sealed class JournalRecorder : MonoBehaviour { }
    // JournalRules.ShouldJournal -> species id from ContentRegistry, spot = IWorldContext.SpotId -> JournalRules.Apply -> Save when Changed;
    // toasts: FirstCatch "toast.journal_new", PersonalBest "toast.journal_best", LevelAfter > LevelBefore "toast.journal_unlock" (local only).
    // Saves on application quit as well (SaveService registered in App by AppBootstrap).
    // T20 also adds the three toast keys to UI/UiStrings.cs (Fishing cannot reference UI, so the recorder publishes keys; M3-D88) and the extra EditMode file
    // Tests/EditMode/JournalRecorderTests.cs (JournalRecorder_* through the internal pure seam JournalRecorder.Record; Fishing grants EditMode its internals; M3-D90).
```

### 7.21 T21 - Journal UI (asm FishGame.UI)
```csharp
internal sealed class JournalPresenter         // UI/Journal/; created by the UiFlowController hook; modal (cursor unlocked, Menu context)
{ }   // 25 cards in species-id order; level 0..3 per JournalRules.HintLevel (level 0: black silhouette + "???" + spot hint M3-D28);
      // level 2 icons from JournalHints; "biting now" badge (level >= 2) from BitingNow with WorldState values, the local rod tier
      // (NetRodIndex) and the local selected bait (M3-D26); filters: spot, biting now; completion "n/25". Opens with J in Free, RodReady,
      // RodWaiting and from the main menu (offline, from the save); closes on J/Esc and automatically on the local Bite cue.
internal static class IconFactory              // UI/Journal/IconFactory.cs; runtime Texture2D per species from SilhouetteMath (+ style per R74 guide),
{ }                                             //   cached, never saved as files; level 0 black, level >= 1 colorA/colorB
```
Main menu (11.3): meta summary line "Journal n/25" (`menu.meta_journal`) and a Journal button.

T21 also adds `PlayerInputFrame.JournalPressed` (M3-D87), the internal `UI/Journal/WorldGlyphs.cs` (painter2D glyphs for the 4 seasons, 3 M3 weathers and 6 bands, shape and not hue only; T22's clock HUD reuses it, read-only for T22) and an internal HUD-modal seam inside `UiFlowController.cs` that T22's dev panel uses; no new file for the seam (M3-D90).

### 7.22 T22 - Dev panel, HUD clock (asm FishGame.UI)
```csharp
internal sealed class ClockHudPresenter : IHudExtension   // UI/World/; top-left "2:20 PM" (DayClock.FormatTime) + season and weather glyphs (shape, not hue only)
{ }
internal sealed class DevPanelPresenter : IHudExtension   // UI/Dev/; Debug.isDebugBuild only; F3 cycles off -> overlay -> panel
{ }   // host: editable season, minute, weather override, run seed (before the first cast of the session), luck, local rod tier (SelectRodRpc to the
      // rod of that tier), "new day", "spawn hotspot here" (crosshair water point), dev clock on/off, load map (DevLoadSpotRequested);
      // clients: read-only view of the same values. No RPCs: the host calls DevWorldDriver / FishingHost / FishingDebugSettings directly.
```
`DevOverlayPresenter` gains one WORLD line (spot, day, minute, season, weather, hotspot, cast counter on the host).

"F8 held = x60" needs a held state: `Runtime/Input/IDevInput.cs` gains `bool IsHeld(DevKey key)` (same gating as `WasPressed`: dev build, window focus, not Menu), implemented by `PlayerInputAdapter` and `FakeInputService`. The alternative of overloading `WasPressed(DevKey.ScrubClock)` with the held state is not used (CCR accepted 2026-10-13; M3-D89). `DevKey.ScrubClock = 6` (F8); `DevKey.CycleForcedSpecies = 7` (F10) is unchanged.

### 7.23 T23 - Fish models (controller)
Not a subagent task. 20 new models (18 regular species not covered by M3-D33 + 2 specials) through Blender MCP following the R74 style guide (fish may be whacky or magical but stay recognisable and comic), staged in `Art/Export/`, imported to `Content/Models/`, filled by `FishingContentBuilder`'s model fill (C2 6.2 rule, as amended by T4), checked by `FishingValidators.SpeciesModels`. Batches of 4-6 (5 / 4 / 6 / 5) with a user look check per batch. Fallback: `FishFallbackMesh` (2.8 #28).

**Skin sheet (M3-D91):** the R74 pipeline keeps every fish skin as one 512 x 512 tile of `Content/Textures/T_FishSkinArray.png`. After the adoption it holds layers 0-5 (0 white, 1 bluegill, 2 perch, 3 carp, 4 trout, 5 catfish) on a 4 x 2 sheet, so the 20 new species take layers **6-25** in batch order (6-10, 11-14, 15-20, 21-25) and no used layer is ever renumbered (the FBX carries its layer in uv1). 26 used layers need a 4 x 8 sheet (32 layers, 2048 x 4096 px); today's `maxTextureSize` 2048 would silently halve every tile, so T23 (batch 1) raises the cap to 4096 for the fish array only. T23 therefore also owns `T_FishSkinArray.png` (+ `.meta`), the `FishSkinArray` row of `Editor/Builders/Render/TextureArrayPostprocessor.cs` (`Rows` 2 -> 8, cap 4096, `GetVersion` +1, layer comment corrected: it still says "2..7 white") and the pinned T4 test `SmoothMeshBuilderTests.TextureArrays_ImportAsFlipbookArrays` (`skin.depth` 8 -> 32). The fallback of a second fish array (`T_FishSkinArray2`, which needs a shader change) is not used (CCR accepted 2026-10-13).

### 7.24 T24 - PlayMode integration (asm FishGame.Tests.PlayMode)
`Tests/PlayMode/EcologySoloTests.cs` (section 10.4). Reuses the frozen C2 `FishingPlayModeDriver` surface (C2 7.19) unchanged.

### 7.25 T25 - Acceptance
`docs/superpowers/plans/2026-10-13-m3-acceptance.md`: every 17.3 bullet as a checklist line with an owner; the 12.9 MPPM list; the dev-panel direction checks (17.3 bullet 4); F8 scrub and readability at 25 m; balance tuning items; art status per species (model or fallback); README "Ecology dev panel" and "Journal / meta.json". Final `check` green; then the Opus xhigh branch review.

---

## 8. Network surface and authority

### 8.1 NetworkVariables (all new ones are on `WorldState`)

| Field | Type | Read / Write | Notes |
|---|---|---|---|
| `NetSeason` | `byte` (`Season`) | Everyone / Server | rolled per run (session in M3) |
| `NetWeather` | `byte` (`WeatherMask`) | Everyone / Server | current segment; changes at most 5 times a day |
| `NetClock` | `ClockAnchor` | Everyone / Server | written only on start, stop, pause, reset, rate change; peers compute the minute (14.2) |
| `NetHotspot` | `HotspotState` | Everyone / Server | at most one per spot |

No new RPCs. Dev panel writes are host-local calls; clients are read-only (spec 12.11, 17.3 "clients can't change world state"). `CastRequestRpc`, `CastResultRpc` and every other C2 8.2 payload are unchanged; the M3 rejection reasons reuse `NotWater` and `NotAllowedNow`.

### 8.2 Authority map (M3 additions to spec 6.8)

| System | Auth | Mechanism |
|---|---|---|
| Season, weather segment, clock anchor, hotspot | H | `WorldState` NetworkVariables written by `DevWorldDriver` (M4: RunDirector/DayDirector) |
| Run state (seed, season, day, cast counter, catch limits; the weather track is rebuilt from seed and season) | H, host memory | `EcologySession` / `EcologyRunState`; never sent (M4 puts it in the snapshot) |
| Catch roll context | H | `FishingHost` builds `FishingContext` from host values only; owner values are never trusted |
| Catch limits per run | H | `CatchLedger` reservations in the host ledger |
| Shore grid, stand points | static data | baked assets identical on every peer (covered by `ContentHash` only through ids; grids are build output of the same scene) |
| Journal, meta.json | L per peer | `JournalRecorder` from `CatchAnnounced`; each machine writes its own file |
| Day-night, palette tint, weather looks, hotspot VFX, ambience | L | derived from `WorldState` |

### 8.3 Protocol and content versions
- `BuildInfo.ProtocolVersion` 3 -> 4 (T14): a networked scene object and four NetworkVariables are added. The R72 rework already bumped it 2 -> 3 (FlagRight, FlagDive = back pull with a side, no run flags on a spent fish, host fight validation); the R70 rework does not bump it.
- `ContentRegistry.dataVersion` 1 -> 2 (T3). `ContentHash` also changes with the new species, junk and `zones` ids. MPPM check: an instance with a bumped `dataVersion` is rejected at approval with the version message (spec 12.9 manual).

---

## 9. Runtime flows

**Session start (host).** `DevWorldDriver` calls `EcologySession.GetOrCreate(FishingConstants.DefaultDevRunSeed)` (the first caller creates the run state with the rolled season; `FishingHost` later reuses it, and `SetRunSeed` before the first cast does `Clear()` + `GetOrCreate(newSeed)`, M3-D57) and adopts it by reference, then builds the `WeatherDirector` (35 segments), anchors the clock at minute 0 day 1 running, writes `NetSeason`, `NetWeather` (segment 0), `NetClock`, creates the day's `HotspotPlanner`. Clients receive all four on spawn (late joiners too; no per-tick traffic).

**Clock.** Every peer: `minute = ClockMath.MinuteAt(NetClock, ServerTime.Time)`. Host: segment change -> `NetWeather`; hotspot schedule -> `NetHotspot`; minute >= 1200 -> `DevNewDay()`; pause -> re-anchor stopped.

**Cast (M3 additions to C2 9).** Owner marker: `CastProbe` + grid water + `CastingOpen`. Host: `TryComputeLanding` -> `CastProbe` -> `GridWater` + `Context` from `WorldState` and the zone -> `Ledger.RequestCast` (validation incl. NotAllowedNow / NotWater) -> `FishingRoller.Roll` with the host context -> catch-limit reservation -> `CastResultRpc` as before. Bite wait uses `inHotspot` x0.6. The fight and commit path is C2 9 unchanged; the first resolution commits or releases the reservation.

**Announce and journal.** `CatchAnnouncedRpc` (C2) -> every peer's `JournalRecorder` -> `JournalRules.Apply` -> `SaveService.Save` when changed -> local toasts. The journal UI and "biting now" read the save and `WorldState`.

**Map switch (dev).** Host dev panel -> `DevLoadSpotRequested` -> `SceneFlow` NetworkSceneManager load -> new Spot scene: its `WorldState` spawns, `DevWorldDriver` reuses `EcologySession.Current` (season, day, cast counter, catch ledger), rebuilds the identical weather track, restarts the clock at minute 0 of the current day; `FishingHost` creates a new ledger on the same run state; `FishingDebugSettings` F6/F7/Luck survive (T1). Players respawn through `PlayerSpawner` (OnLoadComplete path).

**Session end.** `EcologySession.Clear()`, `FishingDebugSettings` reset, `SaveService.Save()` on quit.

---

## 10. Test inventory (exact names; owners)

EditMode files under `Tests/EditMode/` (ecology ones may sit in `Tests/EditMode/Ecology/`), namespace `FishGame.Tests.EditMode`, `Subject_Condition_Expected`. Listed tests are required; others may be added. Every C2 test keeps passing except those T3/T13 rewrite or delete (listed per task).

### 10.1 Carry and migration

| Owner | File | Required tests |
|---|---|---|
| T1 | `Tests/PlayMode/FishingSoloTests.cs` (add) | `FishingSolo_CastMarker_UpdatedAfterMachineTickSameFrame`, `FishingSolo_SceneReload_KeepsDevOverrides_SessionEndClears` |
| T1 | existing test files | positive control in the ledger zero-alloc test; the three `ShaderValidatorTests` extra cases (names chosen in the T1 plan) |
| T2 | `ContentHashTests.cs` (add) | `ContentHash_DuplicateCategory_Throws` |
| T2 | `DisplayNameRulesTests.cs` (add) | `DisplayNameRules_Truncate_NeverSplitsSurrogatePair`, `DisplayNameRules_Truncate_SharedByNameWireAndSessionText` |
| T2 | `Tests/PlayMode/UiFlowCancelTests.cs` | `UiFlow_OnlineCancel_ShowsCancellingImmediatelyAndReturnsToMenu` (fake `INetworkSession` whose cancel completes late, injected through `AppBootstrap.OnlineSessionFactoryOverride`, M3-D71) |
| T3 | `EcologyMigrationTests.cs` (temp folder `Tests/__Temp/`) | `EcologyMigration_RemovesM2PlaceholderSpeciesKeepsModels`, `EcologyMigration_OverwritesBluegillWithRosterValues`, `EcologyMigration_WritesBaitM3FieldsOnExistingAssets`, `EcologyMigration_BumpsDataVersionOnce_SecondRunIsNoOp` |
| T3 | `FishingContentTests.cs` (edit) | `FishingData_SeedsMatchSpecTables` drops the four M2 placeholder species (species count 1) and expects `minLandDepth` 0.5 (M3-D74); new `FishingData_BaitAffinitySeeds_MatchContract` |
| T3 | other existing test files (M3-D73) | `FishingDefsTests` (`FishingDefs_SerializedNames_MatchContract` csv, `CheckBait`/`CheckSpecies` round-trips, Legendary = 3), `FishingValidatorTests` (re-point `perch` to `bluegill`, bait range rows, new extra `FishingValidators_UndefinedTagBits_AreReported`), `FishingContentBuilderTests`, `ContentAssetParityTests` (seed species count 1), `FishFallbackMeshTests` (colour-only assert for the species case), `Tests/PlayMode/FeedbackSoloTests` (swap bluegill's model); each fix is listed in the section A plan T3.10 |

### 10.2 Core

| Owner | File | Required tests |
|---|---|---|
| T5 | `DayClockTests.cs` | `DayClock_BandAt_BoundariesMatchSpec` (0, 119, 120, 299, 300, ..., 840, 1199), `DayClock_SegmentAt_FiveBlocksOf240`, `DayClock_FormatTime_TenMinuteStepsTwelveHour` (0 "6:00 AM", 375 "12:10 PM", 1199 "1:50 AM"), `DayClock_DayFraction_ClampedAndNaNSafe` |
| T5 | `ClockMathTests.cs` | `ClockMath_MinuteAt_ClampsAt1200`, `ClockMath_MinuteAt_StopsWhenNotRunning`, `ClockMath_Reanchor_ContinuousAcrossRateAndRunChanges`, `ClockMath_NonFiniteTime_ReturnsAnchorMinute` |
| T5 | `EcologyTypesTests.cs` | `EventMods_DefaultIsInvalidNeutralIsValid`, `EventMods_WeightFor_MultipliesSetTags`, `SkillMods_RarityMult_OnlyUncommonAndRare`, `BaitMods_Neutral_HasUnitMultipliers`, `ZoneData_SubAreaIndexFor_ContiguousBandsAndEdges`, `ZoneData_NonContiguousBands_Throws`, `EcologyDatabase_OutOfRangeIndices_Throw`, `CatchLedger_ReserveCommitRelease_LimitsCrewWide`, `CatchLedger_CommitWithoutReserve_Throws` |
| T6 | `FishingResolverTests.cs` | `FishingResolver_SameSeedAndContext_IdenticalThousandCastSequence`, `FishingResolver_EachGate_ExcludesRule` (TestCase: subArea, season, weather, band, rodTier, baitRating, depth, players, catchLimit), `FishingResolver_WeatherGate_MatchesAnySharedBit`, `FishingResolver_AnySubAreaAndInfiniteDepth_PassGates`, `FishingResolver_JunkShare_WithinPoint015OverTenThousand`, `FishingResolver_JunkShare_HalvedInHotspot`, `FishingResolver_JunkShare_ClampedAtPointSix`, `FishingResolver_EmptyPool_AlwaysJunk`, `FishingResolver_SpeciesShares_WithinFifteenPercentRelativeOfWeights`, `FishingResolver_ForcedSpecial_WithinTwentyPercentOfExpected` (0.02 x (1 - junk), 50,000 casts), `FishingResolver_SpecialChance_ClampedAtHalfAndDoubledInHotspot`, `FishingResolver_AffinityTag_MultipliesWeight`, `FishingResolver_WeightDraws_InRangeMeanPoint3125TrophyRate4p7` (mean t 0.3125 +- 0.01, trophy 4.7% +- 0.7%), `FishingResolver_FishValue_AtLeastOneAndMinnowAtZeroIsThree`, `FishingResolver_InvalidEventOrSkillMods_Throws`, `FishingResolver_DrawOrder_HandReplayed` (Pcg32 replay of one junk path, one special path, one pool path) |
| T6 | `FishingRollerTests.cs` | `FishingRoller_Order_ItemThenBitePlanThenFightSeed`, `FishingRoller_Overrides_ForceSpeciesJunkBiteTwitchy`, `FishingRoller_ForcedIndexOutOfRange_Throws`, `FishingRoller_Hotspot_ShortensBiteWait`, `FishingRoller_EventBiteMult_ClampedToHalfAndOneAndHalf`, `FishingRoller_FightSeed_NeverZero` |
| T7 | `DistanceFieldTests.cs` | `DistanceField_SingleLandCell_WithinChamferErrorOfEuclidean` (<= 8.3% relative), `DistanceField_AxisDistances_Exact`, `DistanceField_NoLand_IsInfinite`, `DistanceField_SizeMismatch_Throws` |
| T5 | `ShoreGridTests.cs` | `ShoreGrid_WorldToCell_RoundTripsCellCentres`, `ShoreGrid_OutsideGrid_NotWaterInfiniteShore`, `ShoreGrid_BitArrayLengthMismatch_Throws` |
| T7 | `ShoreGridMathTests.cs` | `ShoreGridMath_ShoreDecimetres_NotWaterIsFFFFAndHalfCellOffset`, `ShoreGridMath_ReachableBits_RingFromStandPoints`, `ShoreGridMath_CountReachablePerSubArea_KnownGrid`, `ShoreGridMath_PackBits_RoundTrip` |
| T7 | `WeatherDirectorTests.cs` | `WeatherDirector_SameSeed_SameThirtyFiveSegments`, `WeatherDirector_FirstSegment_DrawnFromClearRow`, `WeatherDirector_RowProbabilities_SeasonMultipliersRenormalised` (Spring Rain x1.5, Summer Fog x0.3, Winter Fog x1.3), `WeatherDirector_TenThousandSteps_MatchMarkovRowsWithinTolerance`, `WeatherDirector_OnlyClearRainFog`, `WeatherDirector_InfiniteDay_ContinuesFromPreviousSegmentDeterministically`, `WeatherDirector_RollSeason_DeterministicAndRoughlyUniform`, `WeatherDirector_At_UsesSegmentOfMinute` |
| T7 | `HotspotPlannerTests.cs` | `HotspotPlanner_FirstSpawn_BetweenSixtyAndOneTwentyMinutes`, `HotspotPlanner_Duration_FortyFiveToSixty`, `HotspotPlanner_AtMostOneAndGapRespected`, `HotspotPlanner_Cell_IsReachableCastableWater`, `HotspotPlanner_NoReachableCells_NeverSpawns`, `HotspotPlanner_SameSeedAndDay_SameSchedule`, `HotspotPlanner_ForceAt_ReplacesCurrentWithoutDraws`, `HotspotPlanner_Contains_RadiusThree` |
| T8 | `JournalRulesTests.cs` | `JournalRules_LandedByMe_SeenCrewLandedCaughtAndBests`, `JournalRules_CrewLanded_NoCaughtNoBests`, `JournalRules_Lost_SeenOnly`, `JournalRules_ShouldJournal_IgnoresJunkPityCastAndNone` (a PityCast's default catch is item 0, a real species), `JournalRules_ReleasedCreelFull_JournaledLikeLanded`, `JournalRules_HintLevels_ZeroToThree`, `JournalRules_FirstCatchAndPersonalBest_Flags`, `JournalRules_SeenInSpots_NoDuplicates`, `JournalRules_UnknownIds_KeptUntouched`, `JournalRules_Completion_CountsCaughtAtLeastOne` |
| T8 | `BitingNowTests.cs` | `BitingNow_IgnoresSubAreaAndDepth`, `BitingNow_RespectsSeasonWeatherBandAndRodTier`, `JournalHints_Summarize_UnionOfAllRules` |
| T8 | `DayNightMathTests.cs` | `DayNightMath_Blend_ContinuousAcrossBoundaries` (sampled every 0.0005 of the day: no step above a bound), `DayNightMath_Blend_HoldsAwayFromBoundaries`, `DayNightMath_SunElevation_AtLeastMinWhileUp`, `DayNightMath_SunMoonFlip_BothWeightsZeroAtFlip`, `DayNightMath_FogFactor_ExponentialSquared`, `DayNightMath_IsReadable_DensityBoundAtPoint0333` |
| T8 | `SilhouetteMathTests.cs` | `SilhouetteMath_EveryBodyShape_ClosedAtTipsPositiveInside`, `SilhouetteMath_EelLongerThanPuffer` |

### 10.3 Data, content, coverage, balance

| Owner | File | Required tests |
|---|---|---|
| T9 | `EcologyDefsTests.cs` | `EcologyDefs_ToCore_MapsEveryField` (cases: zone, sub-area band, rule, junk weight, weather table, shore grid, ecology tuning), `FishingZoneDef_RulesSortedByFishIdOrdinal`, `SpotDef_M3Configure_KeepsM1Fields`, `ArtPaletteDef_M3Fields_RoundTrip`, `ShoreGridDef_Unbaked_ToCoreIsNull` |
| T9 | `ContentRegistryTests.cs` (add) | `ContentRegistry_Zones_IndexedOrdinalAndHashedWhenNonEmpty`, `ContentRegistry_Ecology_BuildsDatabase`, `ContentRegistry_TryGetSpotByScene_Resolves`, `ContentRegistry_RuleWithForeignSpecies_FailsValidate` |
| T10 | `EcologyContentTests.cs` | `EcologyData_SeedsMatchRoster` (spec 12.6 rows, specials, junk; deviations only through `EcologyTunedDeviations`), `EcologyData_SevenJunkOldBootShared`, `EcologyData_AssetsExistForEverySeed`, `EcologyData_ModelFiles_ReuseFourM2Models`, `EcologyData_AssetValuesEqualSeeds`, `EcologyData_SubAreasMatchSpec125` |
| T10 | `EcologyContentBuilderTests.cs` (temp folder) | `EcologyContentBuilder_BuildAt_CreatesMissingOnly`, `EcologyContentBuilder_FillsNullSpotZoneOnly`, `EcologyContentBuilder_WrongTypeAsset_Throws` |
| T10 | `FishingContentTests.cs`, `ContentAssetParityTests.cs` (edit, M3-D61) | species and junk blocks of `FishingData_SeedsMatchSpecTables` and the junk count of `FishingData_Ids_UniquePerCategory` become "same list as `EcologySeedData`" checks; `SeedSpecies_Models_AreFishModelMeshes_UnderTriangleBudget` checks only species whose model file exists (C 2.8 #28). No other edit |
| T11 | `EcologyValidatorTests.cs` | `EcologyValidators_DataRules_ReportEachViolation` (TestCase per 12.9 hard-fail rule), `EcologyValidators_Coverage_ReportsDeadSubArea`, `EcologyValidators_Variety_ReportsDominantSpecies` |
| T11 | `EcologyCoverageTests.cs` | `Validation_Ecology_Pass`, `EcologyCoverage_V1_NoDeadSubArea`, `EcologyCoverage_V2_ThreeSpeciesNoneAboveHalf`, `EcologyCoverage_V3_EverySpeciesPassesSomewhereAtRodThree`, `EcologyVariety_EverySpotSeason_FourEligibleAndOneSingleBand` (name kept; it checks the ruled "narrow window" rule of 11.2 and prints the literal single-band count, M3-D65), `EcologyVariety_MorningVsNight_DifferByTwoSpecies` |
| T11 | `Tuning/EcologyFightabilityTests.cs` (`[Category("Tuning")]`) | `EcologyFightability_RhythmTwig_LandsEveryRodZeroSpeciesNinetyPercent` (per rod-0 species' profile, R72 Rhythm bot, Twig, 200 seeds; a miss may pass only through an approved `EcologyTuningItems` entry, M3-D38) |
| T12 | `Balance/EcologyBalanceReports.cs` (`[Category("Balance")]`) | `EcologyBalance_V4_ExpeditionCatchesPerSpecies`, `EcologyBalance_V4_JunkAtMostTwentyPercentEveryDay`, `EcologyBalance_V4_NoSpeciesAboveHalfOfARun`, `EcologyBalance_V5_NoSubAreaBestForCoinsAboveEightyFivePercent`, `EcologyBalance_V5_PrintsQuotaCalibrationInputs` |

### 10.4 Host, world, maps, presentation, journal, PlayMode

| Owner | File | Required tests |
|---|---|---|
| T13 | `FishingHostLedgerTests.cs` (rewrite against `EcologyTestData`) | every C2 ledger test name kept (roll-content tests re-pointed at the resolver), plus `FishingHostLedger_Roll_UsesResolverWithHostContext`, `FishingHostLedger_HostRodTierAndBait_OverrideContext`, `FishingHostLedger_GridNotWater_RejectedNotWater`, `FishingHostLedger_DayOver_RejectedNotAllowedNow`, `FishingHostLedger_CastCounter_LivesInRunState`, `FishingHostLedger_LimitedSpecies_ReservedThenCommittedOnLanded`, `FishingHostLedger_LimitedSpecies_ReleasedOnLossCancelAndDisconnect`, `FishingHostLedger_RollSeed_DerivesFromRunSeedAndCounter` (kept name, run-state counter), and one interim test `FishingHostLedger_InterimLakeContext_BluegillEligible` (T15 deletes it together with the interim context). Deleted by T13 (V3 replaces them, M3-D55): `FishingValidators_UnreachableSpecies_IsReported`, `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth`; `PlaceholderRollerTests` is `git rm`ed. Re-pointed with names kept: `FishingRollValidationTests` (both tests), `FishingWireTests` (`FishingRollNet_RoundTrip_PreservesRoll`, `FishingRollNet_Flags_OnlyBitZeroIsPity`, `FishingRollNet_InsaneRoll_TryToCoreFalse`) |
| T13 | `CatchValidatorTests.cs` (add) | `CatchValidator_ValidateCast_GridCheckAfterProbe`, `CatchValidator_ValidateCast_CastingNotAllowedFirst` |
| T14 | `WorldWireTests.cs` | `ClockAnchor_FromToCore_RoundTripAndEquality`, `HotspotState_FromToCore_RoundTripAndEquality`, `ClockAnchor_Size_AtMostTwentyFourBytes` |
| T14 | `BuildInfoTests.cs` (edit) | expectation 3 -> 4 |
| T14 | `DevWorldTests.cs` (extra, M3-D58) | `DevClock_*` (pause freezes and resumes continuously, scrub x60 continuous, toggles while dev-stopped write nothing, auto new day only while effectively running at 1200, `StartAt` clamps, rate never zero), `EcologySession_*` (existing run state wins and ignores the seed, season from seed and Day 1, `Clear` gives a fresh instance, stale session dropped), `DevWorld_MapSwitchResume_RebuiltTrackIdentical`, `DevWorld_WeatherOverride_OnlyNoneOrSingleM3Weather` (exact names in the section D plan) |
| T15 | `SpotEcologyTests.cs` (extra, M3-D58) | `SpotEcology_OwnerAndHostGates_AgreeAcrossGridEdge`, `SpotEcology_WorldContext_MapsWorldValuesAndSubArea`, `SpotEcology_TryResolve_FalseWithoutStateSpotOrGrid`, `FishingDebugSettings_Luck_ClampedAndHonouredAsSet` |
| T15 | `Tests/PlayMode/FishingWorldTests.cs` (extra, M3-D58) | `FishingWorld_UnforcedCast_AcceptedWithWorldContext`, `FishingWorld_LakeReload_KeepsRunStateAndCastCounter` (a lake -> lake reload before the harbour exists; T24 repeats the switch with the harbour) |
| T16 | `EcologyMapTests.cs` | `Validation_EcologyMaps_Pass`, `ShoreGridBaker_LakeGrid_ShoreDistanceMatchesBowlGeometry` (sampled cells vs the analytic waterline r 50.17 within 1 cell), `ShoreGridBaker_UnderDock_IsWater`, `ShoreGridBaker_LakeEverySubArea_TwentyFiveReachableCastableCells`, `WaterLayer_OnlyWaterVolumeColliders`, `LakeDock_LayerSplit_ShoreStructureDefaultAsSpecified`, `WorldState_SceneObject_HashNonZeroAndSpotIdSet`; extra (the section E plan): `LakeDock_StandPoints_MatchLayout` |
| T16 | `Tests/PlayMode/EcologyMapPlayTests.cs` (new; M3-D84) | `EcologyMap_LakeSolo_WorldStateSpawnsWithState` |
| T17 | `EcologyMapTests.cs` (add) | `HarbourPier_SceneHasEveryPerSceneHook`, `ShoreGridBaker_HarbourEverySubArea_TwentyFiveReachableCastableCells`, `HarbourPier_Rebuild_ByteIdenticalMeshes` |
| T17 | `Tests/PlayMode/EcologyMapPlayTests.cs` (add; M3-D84) | `EcologyMap_HarbourDevLoad_SpawnsOnPierAndProbesEverySubArea` (publishes `DevLoadSpotRequested` from the test) |
| T17 | `SceneNamesTests.cs` (edit; M3-D85) | `SceneNames_BuildOrder_BootstrapFirst` expects `Bootstrap, Menu, Spot_LakeDock, Spot_HarbourPier` (name kept) |
| T18 | `Tests/PlayMode/DayNightTests.cs` | `DayNight_ScrubFullDay_NoLightOrFogPops` (sampled light intensity/colour/fog steps below bounds, incl. the sun/moon flip), `DayNight_SeasonChange_PushesTintOnceNotPerFrame`, `DayNight_FogReadableAt25m_EveryPhaseAndWeather` (EditMode-equivalent check on the real `ArtPalette.asset` lives in `DayNightMathTests`: `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable`, T18 adds it) |
| T18 | `Tests/EditMode/Ecology/DayNightMathTests.cs` (add) | `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable` and the extra `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette` (the CCR-F1 guard: `M_Toon_Base` and `M_Toon_Tri` both still take their hue from the palette; M3-D78) |
| T18 | `Tests/PlayMode/PresentationWorldTests.cs` (edit; M3-D80) | `PresentationWorld_Palette_IsLinearAndAppliedOnce` pins the world first (name kept; every existing assertion stays) |
| T19 | `SfxRecipesTests.cs` (add) | `SfxRecipes_RainPatter_SampleCountPeakNaNAndSeam`; `SfxRecipes_AllIds_Unique` expects 20 ids instead of 19 |
| T19 | `Tests/PlayMode/WeatherFxTests.cs` | `WeatherFx_RainSegment_StartsRainAndLoopStopsOnClear`, `HotspotFx_ActiveHotspot_SpawnsRipplesOnEveryPeerView`, extra `AmbienceBed_SeasonSets_WinterLouderSummerUnchanged` |
| T20 | `SaveServiceTests.cs` | `SaveService_RoundTrip_PreservesJournal`, `SaveService_CorruptMain_LoadsBak`, `SaveService_NoFiles_Defaults`, `SaveService_JsonKeys_CamelCasePropsIdsUnchanged`, `SaveService_UnknownIds_KeptOnRoundTrip`, `SaveService_NewerSchema_NotOverwritten` |
| T20 | `JournalRecorderTests.cs` (extra; M3-D90) | `JournalRecorder_*` through the internal pure seam `JournalRecorder.Record` (exact names in the section G plan) |
| T21 | `InputContextRouterTests.cs` (add) | `InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly` (needs `PlayerInputFrame.JournalPressed`, M3-D87) |
| T24 | `Tests/PlayMode/EcologySoloTests.cs` | `EcologySolo_LakeCastThroughResolver_LandsAnnouncesAndJournals`, `EcologySolo_OffGridCast_RejectedNotWater`, `EcologySolo_WorldClock_RunsAndStopsInSoloPause`, `EcologySolo_WorldState_SyncedWithNonZeroHash`, `EcologySolo_HarbourLoad_SpawnsPlayerAndLandsAFish`, `EcologySolo_DevMapSwitch_KeepsRunStateAndCastCounter`, `EcologySolo_DayOver_CastRejectedThenNewDayAccepts` (stops the dev clock, sets minute 1200: owner marker invalid and a forged request rejected `NotAllowedNow`; `DevNewDay` re-opens casting) |

T21 and T22 are manual-check tasks (C2 G-Q11 precedent) plus the Core helper tests above; T23 is a controller checklist; T25 runs the full suite.

---

## 11. Validators, build checks and balance reports

### 11.1 Data rules (hard fail, `EcologyValidators.DataRules`, spec 12.9 paragraph 1)
Ids unique per category; every reference resolves; `weightMinG < weightMaxG`; `baseValue > 0`; non-special `weight > 0`; specials `specialChance` in (0, 0.5] and `fish.isSpecial == rule.special`; sub-areas contiguous from 0 (last open-ended); `junkChance` in [0, 0.6]; every species has a fight profile. Added (M3): every species appears in at least one rule; at most `MaxSubAreas` sub-areas; masks non-zero; `catchLimitPerRun >= -1`; weather table rows positive and finite, season multipliers finite > 0; junk weights > 0; `castMinDist` in the tuning asset equals 2.5 (or `EcologyConstants.ReachMinM` is re-derived: the validator reports the mismatch).

### 11.2 Coverage and variety (`Coverage`, `Variety`)
- **V1 no dead sub-area:** every spot x season x band x {Clear, Rain, Fog} x sub-area at rod 0 with neutral bait (`BaitMods.Neutral(bare-hook effective rating 1)`) and depth = the tuning `minLandDepth` has >= 1 passing non-special rule.
- **V2 variety:** the same contexts with sub-areas mixed uniformly (expected shares over the eligible weights of all sub-areas) give >= 3 distinct species, none above 50%. Specials are excluded from the shares (catch-limited; M3-D66).
- **V3 reachability:** every species (specials included) passes in >= 1 context at rod 3 (any bait rating 10, depth 6 m).
- **13.3 variety (M3 part):** every (spot, season) has >= 4 eligible species and >= 1 species eligible in at most two adjacent bands (Night and Dawn are not adjacent: the day does not wrap, so band indices are never taken modulo 6); the eligible set differs by >= 2 species between Morning and Night. The literal spec text "eligible in exactly one band" fails for every spot and season of the spec 12.6 roster (every row has 2 or more bands), so this is the **ruled wording; the spec delta is pending (M3-D65)**. The test name `EcologyVariety_EverySpotSeason_FourEligibleAndOneSingleBand` is kept and it also prints the literal single-band count.
- **Definitions (M3-D66, with the spec 12.9/13.3 deltas pending):** "eligible" (13.3) = some non-special rule passes at rod 3, bait rating 10, depth 6 m, in any of Clear/Rain/Fog and any sub-area; "differs by >= 2" = the symmetric difference of the Morning and Night eligible sets has >= 2 species; specials are excluded from V2, the 13.3 counts and V5 and included in V3 and V4.
- **Fightability** (`[Category("Tuning")]`): Rhythm bot, Twig rod, every rod-0 species' profile lands >= 90% over 200 seeds (R72 bots; weight does not change an R72 fight, so species with the same profile share the result).

### 11.3 Map checks (`MapChecks`, build-failing, T16)
- **Reachability (12.5, M3-D6, M3-D83):** each sub-area of the zone of each REGISTERED spot (`registry.Spots`) has >= 25 water cells that are castable (water first and depth >= `minLandDepth`) and reachable (centre within [4.9, 16.4] m of a `StandPoint`). A registered spot's unbaked grid, or one baked from another scene, fails. The harbour is registered only once `SceneNames.BuildOrder` contains `Spot_HarbourPier` (M3-D63), which T17 adds in the same commit that bakes its grid, so `build` stays green between T16 and T17. Zones are built fresh from the assets (`ToCore`), never from the cached `registry.Ecology`.
- **Water layer:** in every build scene, the only colliders on layer Water belong to `WaterVolume` objects (R70 ladders plan F3; CastProbe treats the first Water-layer hit as the surface).
- **WorldState:** every Spot scene has exactly one `WorldState` with a non-zero `GlobalObjectIdHash` and a `spotId` that resolves to a `SpotDef` whose `sceneName` is that scene.
- **Layer split (lake, T16):** shore mesh, rocks, trees on Shore; dock, posts, rails, ramp on Structure; lakebed on Ground; reeds, ladders, exits, stand points on Default.

### 11.4 Balance reports (`[Category("Balance")]`, loose bounds, printed tables; spec 12.9)
- **V4 expedition sim:** solo, 7 days, ~60 casts/day, per spot x season, 400 runs, the real resolver and `WeatherDirector`, rod path Twig days 1-2, Handmade 3-4, Carbon 5-6, Deep-sea 7, neutral events (M4 adds escalation). Casts are spread over sub-areas and the day's minutes uniformly (M3-D39). V4 and V5 use neutral bait (`BaitMods.Neutral(1)`, M3-D5) with bite speed 1.0 (M3-D66). Bounds: in season and at the required rod each Common >= 7 per run, Uncommon >= 3.5, Rare >= 0.7, special >= 0.4 [tune]; junk <= 20% over the run and on every day; no species above 50% of a run's catches.
- **V5 economy:** expected coins and kg per real minute per sub-area x band x weather; cycle time = cast overhead + expected bite wait (spec 9.4 terms) + the Rhythm-bot fight median for the species' profile on the rod of the day + 10 s walking. T12 measures the fight medians itself with the R72 Rhythm bot (same seeds, same casts, real assets) on all four rods, because R72 F4/F8 measure only Handmade and Twig; for those two the numbers equal F8's by construction and the report prints them next to F8's table as a check (M3-D39, M3-D64). Economy inputs: neutral bait; specials excluded; best-for-coins ties go to the lower sub-area index (M3-D66). No sub-area best-for-coins in more than 85% of a season's contexts at rod 0. Prints the per-day expected sales on the default rod path (M4 quota calibration input, 14.4) and the day-8+ plateau.
- A failing bound passes only as an approved `EcologyTuningItems` entry (`Assert.Ignore` with numbers; controller approval; carried to T25), mirroring C2 D-Q6.

---

## 12. Per-task file manifest (paths under `Assets/_Project/` unless rooted; tests in section 10 belong to the same task)

| Task | Files created or edited |
|---|---|
| T1 | `Fishing/Owner/FishingRodController.cs`, `Fishing/Host/FishingHost.cs`, `Fishing/Debug/FishingDebugSettings.cs`, `Core/Fishing/{CatchValidator,FishingConstants}.cs` (comments only), `UI/Fishing/FishingHudPresenter.cs` (null check), tests: `Tests/PlayMode/FishingSoloTests.cs`, `Tests/EditMode/{FishingHostLedgerTests,ShaderValidatorTests}.cs` (M3-D73) |
| T2 | `Core/Hashing/ContentHash.cs`, `Core/Net/DisplayNameRules.cs`, `Gameplay/Player/NameWire.cs`, `Gameplay/Session/SessionText.cs`, `Gameplay/AssemblyInfo.cs` (`InternalsVisibleTo` EditMode), `App/AppBootstrap.cs` (`OnlineSessionFactoryOverride`), `UI/UiSessionActions.cs`, `UI/UiStrings.cs`, tests (`ContentHashTests`, `DisplayNameRulesTests`, `Tests/PlayMode/UiFlowCancelTests.cs`) |
| T3 | `Core/Ecology/FishTags.cs`, `Core/Fishing/{FishRarity,FishSpeciesData,BaitStats}.cs`, `Data/Fishing/{FishSpeciesDef,BaitDef}.cs`, `Editor/Builders/Ecology/EcologyMigration.cs`, `Editor/Builders/Fishing/{FishingSeedData,FishingContentBuilder}.cs`, `Editor/Validation/FishingValidators.cs`, hook in `ContentBuilder.cs`, `AssetPaths.cs` block; migrated `Content/Fishing/Species/*`, `Baits/*`, `ContentRegistry.asset`, `FishingTuning.asset` (`minLandDepth`), tests: new `Tests/EditMode/Ecology/EcologyMigrationTests.cs`; edits to `Tests/EditMode/{FishingContentTests,FishingDefsTests,FishingValidatorTests,FishingContentBuilderTests,ContentAssetParityTests,FishFallbackMeshTests}.cs` and `Tests/PlayMode/FeedbackSoloTests.cs` (M3-D73) |
| T4 | owned by the look-dev plan (`m2-lookdev`): style guide doc, shaders, materials, textures, LakeDock/props/body/fish restyle, render validators |
| T5 | `Core/Ecology/{TimeBandMask,SeasonMask,Season,WeatherMask,EcologyMasks,DayClock,ClockAnchorData,ClockMath,EcologyConstants,EcologyStreams,SubAreaBandData,SpawnRuleData,JunkEntry,WeatherTableData,BaitMods,EventMods,SkillMods,FishingContext,IWorldContext,EcologyTuning,ZoneData,ShoreGrid,SpotEcologyData,EcologyDatabase,CatchLedger,EcologyRunState}.cs`, `Core/Fishing/BaitStats.cs` (`Mods`, M3-D68), tests `Tests/EditMode/Ecology/{DayClockTests,ClockMathTests,EcologyTypesTests,ShoreGridTests}.cs` and the shared fixture `Tests/EditMode/Ecology/EcologyCoreTestData.cs` (M3-D67) |
| T6 | `Core/Ecology/{FishingResolver,FishingRoller}.cs`, tests |
| T7 | `Core/Ecology/{DistanceField,ShoreGridMath,WeatherDirector,HotspotData,HotspotPlanner}.cs`, tests |
| T8 | `Core/Journal/{MetaSave,JournalEntry,JournalUpdate,JournalRules,BitingNow,JournalHints}.cs`, `Core/Render/{DayNightMath,PhaseBlend,SilhouetteMath}.cs`, tests |
| T9 | `Data/Ecology/{FishingZoneDef,SubAreaBand,SpawnRule,JunkWeight,WeatherTableDef,ShoreGridDef,EcologyTuningDef}.cs`, `Data/ContentRegistry.Ecology.cs`, edits in `Data/ContentRegistry.cs`, `Data/SpotDef.cs`, `Data/Art/{ArtPaletteDef,SunMoonSettings,WeatherAmbient}.cs`, `Data/Audio/{SfxCueTableDef,SeasonAmbience}.cs`, tests |
| T10 | `Editor/Builders/Ecology/{EcologySeedData,EcologySeedData.Roster,EcologyContentBuilder}.cs`, edits in `FishingSeedData.cs` (species and junk forwarding, junk colours), `FishingContentBuilder.cs` (no logic change beyond what the forwarding gives), `ContentBuilder.cs` (hook), `AssetPaths.cs`, `Core/Content/ContentIds.cs` (`HarbourPier` if first); tests `Tests/EditMode/Ecology/{EcologyContentTests,EcologyContentBuilderTests}.cs` plus the edits to `Tests/EditMode/{FishingContentTests,ContentAssetParityTests}.cs` (M3-D61) and the roster-driven expectation edits to `Tests/EditMode/{Ecology/EcologyMigrationTests,FishFallbackMeshTests}.cs` (review #4, see 2.6); generated `Content/Fishing/{Species,Junk}/*` (created by `FishingContentBuilder`), `Content/Ecology/**`, `Content/Spots/harbour_pier.asset`, the in-place fills of `Content/Spots/lake_dock.asset`, `Content/Art/ArtPalette.asset` and `Content/Audio/SfxCueTable.asset`; `Tests/EditMode/Ecology/EcologyTestData.cs` is T13's - T10 does not create it |
| T11 | `Editor/Validation/EcologyValidators.cs`, hook in `ValidationRunner.cs`, tests (incl. `Tuning/EcologyFightabilityTests.cs`) |
| T12 | `Tests/EditMode/Balance/EcologyBalanceReports.cs` (no `ExpeditionSim.cs`: not created, see 7.12); retuned `.asset` + seed + `EcologyTunedDeviations` |
| T13 | `Core/Fishing/{FishingHostLedger,CatchValidator,HostCastView,ActiveCast,FishSpeciesData}.cs`, `Data/Fishing/FishSpeciesDef.cs` (field removal), `Editor/Validation/FishingValidators.cs`, `Editor/Builders/Fishing/{FishingSeedData,FishingContentBuilder}.cs` (callers), `git rm` of `Core/Fishing/{PlaceholderRoller,RollRequest}.cs` and `Tests/EditMode/PlaceholderRollerTests.cs`, `Tests/EditMode/Ecology/EcologyTestData.cs`, `Tests/EditMode/{FishingHostLedgerTests,CatchValidatorTests}.cs`; temporary compile fix in `FishingHost.cs` (new ctor with a dev run state; T15 completes it); **compile fallout of the field removal (M3-D55):** `Tests/EditMode/{FishingTestData (two initializers only),FishingDefsTests,FishingValidatorTests,FishingContentTests,FishingRollValidationTests,FishingWireTests}.cs`, plus (review F3) `Tests/EditMode/{ContentRegistryTests,Ecology/EcologyDefsTests,Ecology/EcologyValidatorTests,Ecology/EcologyMigrationTests}.cs` and `Tests/PlayMode/PlayerHooksTests.cs` (one `HostCastView` line), `Editor/Builders/Ecology/{EcologyMigration,EcologySeedData,EcologySeedData.Roster}.cs` (arguments and initializers only), `Core/Fishing/{BitePlanner,RollOverrides,FishingRoll}.cs` (comments only) |
| T14 | `Gameplay/World/{ClockAnchor,HotspotState,WorldState,DevWorldDriver,EcologySession,DevClock}.cs` (`DevClock` holds the internal `DevClock` and `DevWorldRules`, M3-D58), `Core/Versioning/BuildInfo.cs`, `Tests/EditMode/{WorldWireTests,BuildInfoTests,DevWorldTests}.cs` (no `Gameplay/AssemblyInfo.cs` edit: T2 did it); controller-authorised edit of T7's `Core/Ecology/HotspotPlanner.cs` (the `Advance` minute clamp) and `Tests/EditMode/Ecology/HotspotPlannerTests.cs` (review F3, see 2.6) |
| T15 | `Fishing/Host/FishingHost.cs`, `Fishing/Owner/FishingRodController.cs`, `Fishing/Debug/FishingDebugSettings.cs`, `Fishing/Host/SpotEcology.cs` (internal helper shared by the host view and the owner marker, M3-D58), `Runtime/Services/DevLoadSpotRequested.cs`, `App/SceneFlow.cs`, tests `Tests/EditMode/SpotEcologyTests.cs`, `Tests/PlayMode/FishingWorldTests.cs`, the edit to `FishingHostLedgerTests.cs` that deletes the interim test, and `Tests/PlayMode/PlayerHooksTests.cs` (re-point the slot-takeover test's view from `FishingHost.InterimContext` to the world-built context, review F3) |
| T16 | `Gameplay/World/StandPoint.cs`, `Editor/Builders/Ecology/{WorldSceneObjects,ShoreGridBaker}.cs`, `Editor/Builders/{LakeDockBuilder,LakeDockLayout}.cs` (hooks incl. the post-save `FinalizeAfterSave` line, stand points), `EcologyValidators.MapChecks` (new method in T11's file - shared edit allowed: T16 appends `MapChecks` and its helpers), hook in `ValidationRunner.cs`, `AssetPaths.cs`; baked `Content/Ecology/ShoreGrids/lake_dock_grid.asset`, regenerated `Scenes/Spot_LakeDock.unity` (committed whole), tests `Tests/EditMode/Ecology/EcologyMapTests.cs` and `Tests/PlayMode/EcologyMapPlayTests.cs` (M3-D84); `WorldSceneObjects` also holds the internal `FinalizeAfterSave` (M3-D82) |
| T17 | `Editor/Builders/Harbour/{HarbourPierBuilder,HarbourPierLayout,HarbourPierMeshes}.cs` (bake order `SaveScene` -> `FinalizeAfterSave` -> `Physics.SyncTransforms` -> `BakeOpenScene`, review F1), `Core/Game/SceneNames.cs` (the `BuildOrder` entry only: the `SpotHarbourPier` constant exists already), `Editor/Builders/SceneBuilder.cs` (hook), `Editor/Validation/Validators.cs` (spot scene check), `AssetPaths.cs`; generated `Scenes/Spot_HarbourPier.unity`, harbour meshes/props, baked `harbour_pier_grid.asset`, `ProjectSettings/EditorBuildSettings.asset`, tests (`Tests/EditMode/Ecology/EcologyMapTests.cs` and `Tests/PlayMode/EcologyMapPlayTests.cs` appended - M3-D84 - and `Tests/EditMode/SceneNamesTests.cs`, one expectation - M3-D85), `HarbourPierBuilder` calls `WorldSceneObjects.FinalizeAfterSave` (M3-D82) (no file edit wires the harbour `SpotDef` into the registry: T17's first `build` lists it once `BuildOrder` contains `Spot_HarbourPier`, M3-D63) |
| T18 | `Presentation/World/DayNightController.cs`, edits in `Presentation/World/PaletteGlobals.cs`, `Core/Render/ShaderGlobals.cs`, `Editor/Builders/Render/PresentationSceneObjects.cs`, `Tests/PlayMode/DayNightTests.cs`, `Tests/EditMode/Ecology/DayNightMathTests.cs` (add the real-palette case and `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette`), `Tests/PlayMode/PresentationWorldTests.cs` (one test, M3-D80), regenerated `Scenes/Spot_LakeDock.unity` and `Scenes/Spot_HarbourPier.unity` (whole files). **No shader edit** (M3-D78) |
| T19 | `Presentation/World/{WeatherFx,HotspotFx}.cs`, `Runtime/Audio/AmbienceBed.cs`, `Core/Audio/{SfxIds,SfxRecipes.M3}.cs` (+ `SfxRecipes.cs` `All` includes M3 and `Generate` routes the new id), `FishingContentBuilder.cs` (cue-row append), `PresentationSceneObjects.cs`, `Content/Audio/SfxCueTable.asset` (row appended by the builder), tests (`SfxRecipesTests.cs`, `Tests/PlayMode/WeatherFxTests.cs`, and the `Tests/EditMode/FishingValidatorTests.cs` cue-row count 5 -> 6, review F3); CONDITIONAL: `Tests/PlayMode/FeedbackSoloTests.cs` (M3-D81) |
| T20 | `Runtime/Save/{SaveService,MetaLoadSource}.cs`, `Fishing/Journal/JournalRecorder.cs`, `App/AppBootstrap.cs` (hook), `Runtime/AssemblyInfo.cs` (`InternalsVisibleTo` EditMode), `FishingSceneObjects.cs`, `UI/UiStrings.cs` (three toast keys only, M3-D88), regenerated `Scenes/Spot_LakeDock.unity` (committed whole: it gains the component), tests (`SaveServiceTests.cs` and the extra `Tests/EditMode/JournalRecorderTests.cs`, M3-D90) |
| T21 | `UI/Journal/{JournalPresenter,IconFactory,WorldGlyphs}.cs` (`WorldGlyphs` internal, M3-D90), edits in `UI/{UiFlowController,UiStrings,MainMenuPresenter}.cs` (incl. the internal HUD-modal seam), `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`, `Core/Input/{PlayerIntent,PlayerInputFrame,InputContextRouter}.cs` (`PlayerInputFrame.cs`: M3-D87), `Runtime/Input/PlayerInputAdapter.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs`, router tests (`Tests/EditMode/InputContextRouterTests.cs`: `InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly`) |
| T22 | `UI/World/ClockHudPresenter.cs`, `UI/Dev/DevPanelPresenter.cs`, edits in `UI/Fishing/DevOverlayPresenter.cs`, `UI/{UiFlowController,UiStrings}.cs`, `AppRoot.uxml`, `App.uss`, `Runtime/Input/{DevKey,IDevInput,PlayerInputAdapter}.cs` (`IDevInput.cs`: `IsHeld`, M3-D89), `FakeInputService.cs` |
| T23 | `Art/Blender/fishgame_m3_species.blend`, `Art/Export/Fish_*.fbx` (+ R74 textures), `Content/Models/Fish_*.fbx` (+ `.meta`), regenerated species model refs, `Content/Textures/T_FishSkinArray.png` (+ `.meta`; 4 x 2 -> 4 x 8, species layers 6-25), `Editor/Builders/Render/TextureArrayPostprocessor.cs` (fish row: `Rows` 8, cap 4096, `GetVersion` +1) and `Tests/EditMode/SmoothMeshBuilderTests.cs` (`TextureArrays_ImportAsFlipbookArrays`: `skin.depth` 32) (M3-D91) |
| T24 | `Tests/PlayMode/EcologySoloTests.cs` (+ integration fixes) |
| T25 | `docs/superpowers/plans/2026-10-13-m3-acceptance.md`, `README.md` |

---

## 13. Rulings (M3-Dn) and spec deltas

Each row is a ruling where the spec is silent or contradicts itself, marked [ruling]. Spec deltas the controller writes into the spec after the contract is approved are marked "spec delta".

| # | Topic | Ruling | Reason |
|---|---|---|---|
| M3-D1 | Migration owns its fields [ruling] | T3 adds `FishTags`, `FishRarity.Legendary`, `FishSpeciesDef.tags` and the three `BaitDef` M3 fields, deletes the four M2 species, overwrites bluegill, writes the bait fields onto the six existing bait assets (a default `specialMult` 0 would silently disable specials; create-if-missing never fixes it) and bumps `dataVersion`. It runs from `ContentBuilder` (idempotent on `dataVersion >= 2`) and from a menu item. | "Carry and migration first"; spec 12.6 step 1. |
| M3-D2 | Rarity type name [ruling] | Keep `FishRarity`; add `Legendary = 3`. | Avoid a rename across M2 code; spec 12.2 `Rarity` is the same type. Spec delta: 12.2 note. |
| M3-D3 | Species-level gates [ruling] | `FishSpeciesDef.minBaitRating/minDepth` and the `FishSpeciesData` mirrors are removed in T13 with `PlaceholderRoller`; gates live only on `SpawnRule`. Spec 12.6 has no bait column, so every M3 rule gates at bait rating 0: **the bare hook loses its M2 species restriction** (spec 9.7 "mostly brings junk and Steady/Bulldog fish"); it stays worse through `junkMult` 1.5 and `biteSpeedMod` 1.3 and no affinity. | Spec 12.3 lists neither field on the species. Spec delta: 9.7 bare-hook sentence. File fallout and the two deleted tests: M3-D55. |
| M3-D4 | Bait affinity seeds [ruling, tune] | `affinityTags / affinityMult / specialMult`: bare_hook None / 1 / 1; worm Bottom / 1.3 / 1; grub Surface / 1.3 / 1; minnow Predator / 1.4 / 1; shrimp Schooling / 1.3 / 1.2; raw_meat Predator|Nocturnal / 1.5 / 1.5. Validator ranges: affinityMult [1, 3], specialMult [0.5, 3]. | Spec 9.7/12.4 give the fields, no values. |
| M3-D5 | Neutral bait and depth for V1-V3 [ruling] | Neutral = `BaitMods.Neutral(1)` (the bare hook's effective rating, no affinity, multipliers 1); V1/V2 depth = the tuning `minLandDepth` (shallowest valid cast); V3 uses rating 10 and depth 6 m. | Spec says "neutral bait" without defining it; the bare hook is always available. V4/V5 use the same neutral bait and "eligible" is defined in M3-D66. |
| M3-D6 | Reachability ring after R67 [ruling] | A StandPoint reaches a cell when the horizontal distance to the cell centre is in [CastTipReach + castMinDist, CastTipReach + Twig castMaxDist] = [4.9, 16.4] m (any facing). Only castable cells count (water first and depth >= `minLandDepth`). | Spec 12.5 predates R67; counting plain water cells would pass a sub-area nobody can cast into (B10). Spec delta: 12.5. |
| M3-D7 | Bake rules [ruling] | Mask Water, Shore, Ground; Structure and Default ignored (under a pier is water; ladders and exits are invisible); `QueryTriggerInteraction.Collide`; nearest hit Water = water, Shore or Ground = land, no hit = void (neither water nor a distance seed, so the sea's far edge is not "shore"). Shore distance = chamfer distance to the nearest land cell centre minus half a cell, floored at 0. Castable bit from `CastProbe.Probe` at the cell centre. | Spec 12.5 names Water and Shore only; Ground above water and empty cells need an answer. |
| M3-D8 | Grid extents [ruling] | Both maps: 120 x 120 cells of 1 m, origin (-60, -60) (the 120 m water box). | Spec "~30 KB for 120 x 120 m". |
| M3-D9 | Spot of a scene [ruling] | `WorldState.spotId` is serialized by the builder; `ContentRegistry.TryGetSpotByScene` is the reverse lookup. No NetworkVariable. | Every peer loads the same scene. |
| M3-D10 | Season in M3 [ruling] | Rolled once per session from the run seed (`EcologyStreams.Season`); the dev panel overrides it. | Spec 12.2 rolls per run; M3 has no run. |
| M3-D11 | M3 dev clock [ruling] | Starts running at minute 0 of day 1 at session start; a RUNNING clock reaching minute 1200 auto-starts the next day (dev only); a clock stopped at 1200 stays there (casting closed); "new day" = day + 1 and minute 0; days 1-7 come from the pre-rolled 35 segments, day 8+ from the infinite continuation. | Without a run loop a 02:00 dead end would block casting; spec 12.2 "new day re-rolls a day". |
| M3-D12 | Resolver output [ruling] | Overload `Resolve(..., out float weightT, out int ruleIndex)`. | `FishingRoll.WeightT` and catch limits need them; spec signature returns only `CatchResult`. |
| M3-D13 | `FishingRoller` [ruling] | Replaces `PlaceholderRoller`; fixed order item -> bite plan -> fight seed (7.6). Debug overrides bypass gates. | Spec 17.3 "PlaceholderRoller replaced by FishingResolver"; the roll needs the bite plan and seed too. |
| M3-D14 | Catch limits [ruling] | Reserve on roll, commit on Landed or ReleasedCreelFull, release on anything else. | Two players can roll a limit-1 special at the same time; spec says "crew-wide". |
| M3-D15 | Host context [ruling] | `HostCastView.Context` carries the world part; the ledger overwrites rod tier, bait and depth from its own truth. | One host-built context, no client values. |
| M3-D16 | M3 cast checks [ruling] | Grid not water -> `NotWater` (after the probe); minute >= 1200 -> `NotAllowedNow` (through `CastingAllowed`). The owner marker runs the same two gates. No new reject reasons. | Spec 9.3/9.9 "from M3"; honest casts must not be host-rejected. |
| M3-D17 | Run state across Spot loads [ruling] | `EcologyRunState` (seed, season, day, cast counter, catch ledger; the weather track is rebuilt deterministically) is session-level in `EcologySession`; the per-scene ledger reads and increments its counter. | B11: a map switch would replay catch seeds. |
| M3-D18 | WorldState placement | Scene-placed (spec 4.6) with a separate host `DevWorldDriver`. Fallback: network prefab at a `NetSpawnAnchor`, same public API (2.8 #24). | First in-scene NetworkObject; fileID churn moves its hash. |
| M3-D19 | Day counter in M3 [ruling] | No `NetDay`. The HUD shows the time plus season and weather glyphs; "Day N/7" arrives with `RunDirector` in M4. | Spec 12.8 lists four NetworkVariables; a dev-only day var would be churn. Spec delta: 11.4 note. |
| M3-D20 | Hotspot numbers [ruling] | Timings and radius in `EcologyTuningDef` (seeds spec 12.7); the effects (junk x0.5, special x2, bite x0.6) stay formula constants. | Acceptance 9 "tuning in assets" without duplicating formula values. |
| M3-D21 | Weather looks in M3 [ruling] | Rain: camera-local rain particles, `Rain_Patter` loop, light x0.85, fog x1.2; Fog: fog density toward 0.03 (readability cap); Clear: none. M5 adds the Storm/Heatwave looks (spec 10.6). | Spec 17.3 tests "maximum fog at 25 m"; 4.3 lists `WeatherFx`; 10.6 is M5. |
| M3-D22 | Seasonal ambience [ruling] | `SfxCueTableDef.seasonAmbience[4]` (wind gain, wind LFO, lap interval); `AmbienceBed` reads it via `IWorldContext`. | Spec 11.7 "M3 seasonal ambience parameter sets", no home given. |
| M3-D23 | Tint on textured materials (R74) [ruling; rewritten 2026-10-13 by CCR-F1, see M3-D78] | **Under the adopted R74 standard the season tint on textured environment albedo IS the `_FGPalette` season swap** (palette hue x low-chroma value-only detail texture, centred on 0.5 at gain 2). Full-colour painted albedo (fish skin array, cloth, skin) is season-invariant (style guide 2.3). T18 adds NO shader term. `PaletteGlobals` still pushes `_FGSeasonTint` (Vector4[16], `season / Summer` per channel, linear, clamped 0..2, identity without a world) as a dormant hook that no M3 shader reads; the first task that needs a full-colour, season-following material adds an opt-in per-material term (`albedo *= lerp(1, _FGSeasonTint[slot], _FGSeasonTintWeight)`, weight default 0, non-palette path only) and its test. Day-night drives light, ambient, sky and fog only, never material colours. | R74 moved to painted textures; a flat colour swap would erase them (coordinator note 2026-10-12). Checked against the merged `Toon.shader`: a season / base multiply on the palette path would apply the season twice. |
| M3-D24 | Phase blend window [ruling] | The change into phase i runs over [start_i, start_i + 0.05] with smoothstep. | Spec "smoothstep-blended over a 0.05 window" without placement. |
| M3-D25 | Journal kinds [ruling] | Landed, Lost and ReleasedCreelFull journal (ReleasedCreelFull counts as landed: spec 9.10 "announced, journaled"); PityCast and None never (a PityCast's default catch is item 0). "New!" on the first Caught; "Personal best!" on a heavier fish with Caught >= 2; a level-up toast. | Spec 12.10 lists Landed/Lost only. |
| M3-D26 | Biting now inputs [ruling] | Local rod tier from `NetRodIndex`, local selected bait rating, depth ignored, any sub-area, null ledger, `PlayersInSession` from the roster. | Spec names rod tier and ledger only. |
| M3-D27 | Journal input [ruling] | `PlayerIntent.JournalPressed` routed in Free, RodReady, RodWaiting; the journal is a modal (Menu context); a local Bite cue closes it. | Spec 12.10 "can stay open while the bobber is out; a bite closes it". |
| M3-D28 | Level 0 hint [ruling] | "???" plus the display names of the spots whose zone has a rule for the species. | Spec "a vague spot hint". |
| M3-D29 | Icons [ruling] | `IconFactory` in UI rasterises `SilhouetteMath` (Core, tested) in the R74 icon style; cached runtime textures only. | Spec 11.1; EditMode cannot reach UI. |
| M3-D30 | Save format [ruling] | Newtonsoft in Runtime `SaveService` (V14 not attempted); camelCase property names, dictionary keys unchanged (ids are already snake_case); a newer `SchemaVersion` loads read-only. | Spec 12.10 JSON shape (14.10 example keys). |
| M3-D31 | Dev map switch [ruling] | Host dev panel button -> `DevLoadSpotRequested` -> `SceneFlow` NetworkSceneManager load; run state kept; clock restarts at minute 0 of the current day. The release path to the harbour is the M4 Hub map board. | R17: M1-M3 load `Spot_LakeDock` directly; the harbour must be playable in M3. |
| M3-D32 | Dev keys [ruling] | F3 cycles Off -> Overlay -> Panel; F8 held = host dev clock x60 ("F8 scrub", spec 17.3). | F8 is unbound in M2. |
| M3-D33 | Model reuse [ruling] | `yellow_perch` <- `Fish_Perch.fbx`, `grumpy_carp` <- `Fish_Carp.fbx`, `sunny_trout` <- `Fish_Trout.fbx`, `mudcat` <- `Fish_Catfish.fbx`, `bluegill` keeps `Fish_Bluegill.fbx` (all as restyled by T4). 20 new models remain. | Same animals; saves five models; the user may overturn. |
| M3-D34 | Junk values [ruling, tune] | `old_boot` 2, `tin_can` 2 (existing), `soggy_newspaper` 0, `lost_sock` 1, `rope_scrap` 1, `seaweed_clump` 0, `empty_bait_tub` 2; equal weights per spot (12.6). | Spec R51 range 0-5, no values. |
| M3-D35 | Species presentation fields [ruling, tune] | `weightSkew` 2.2, `biteWaitMod` 1.0, `hookWindow` 0 (auto-hook; the user can set a twitchy fish in the Inspector, spec 19 #4), `lengthShapeK` 1.0 except `glimmer_eel` 1.6, `grumpy_puffer` 0.8. Body shapes: Eel `glimmer_eel`; Squid `moon_squid`; Flat `sneaky_flounder`, `storm_ray`; Long `lake_pike`, `pier_sardine`, `ice_herring`, `grumble_mackerel`, `silver_mullet`, `lanternfish`, `frost_char`, `sunny_trout`, `sleepy_sturgeon`, `mirror_minnow`, `bruiser_tuna`; Round for the rest. Colours: two contrasting colours per species, chosen by T10 (comic, recognisable). | Spec 12.3 defaults only. |
| M3-D36 | `minPlayersInSession` now [ruling] | The `SpawnRule` field exists from M3 (value 0); the gate is live. | Avoids an M5 data migration. |
| M3-D37 | Versions | ProtocolVersion 4 (R72 took 3), dataVersion 2. | 8.3. |
| M3-D38 | Fightability under R72 [ruling] | A fightability miss on the Rust-faithful baseline is recorded as an `EcologyTuningItems` entry; M3 never retunes R72 fight numbers the user is playing. | R72 owns the fight; the user tunes it in the friendlier pass. |
| M3-D39 | Balance inputs [ruling] | V4 spreads casts uniformly over sub-areas and minutes; V5 cycle time = R72 F8 median cast-to-land for the profile on the day's rod + 10 s walking. | Spec gives the measures, not the sampling or the walking time. The fight medians come from T12's own Rhythm-bot runs on all four rods (M3-D64), not from F8 alone. |
| M3-D40 | Harbour layout (starting numbers) [ruling] | Water box 120 x 120 at y 0 (centre (0, 0)); coast along z = -45 (land z < -45: Shore, beach slope y -0.3 at z -43 to 1.2 at z -55); seabed Ground at depth 0.25 x shore distance, max 9 m; a 3 m wide pier (Structure, deck y 1.5) from the beach at x 0 out to z -5 with a 10 m T-head; a stone breakwater (Shore) at x 30 running 20 m out; ladders (`LadderVolume`, Default) at the T-head and mid-pier; spawns on the pier near land; bell anchor at the pier root. StandPoints: 5 beach points, pier middle (z -25, both sides), T-head (both sides, end), breakwater tip. Sub-areas 0-5 / 5-12 / 12+. The section author may move things while reachability holds. | Spec gives sub-areas only. |
| M3-D41 | Cast counter persistence | Lives in the run state (M3-D17). | B11. |
| M3-D42 | Registry partial [ruling] | `ContentRegistry` becomes `partial`; ecology members in `ContentRegistry.Ecology.cs`. | 430-line file, 300-line guideline. |
| M3-D43 | Rules sort | Rules sorted by `fish.id` ordinal at registry build; a species with rules in two zones has one rule per zone. | Spec 12.3. |
| M3-D44 | Weather table asset [ruling] | One `weather_temperate` asset shared by both zones. | Same numbers in spec 12.2. |
| M3-D45 | Zones in the hash | `zones` hashed when non-empty; weather tables and grids are referenced data, not hashed categories. | Spec 12.3 lists zones in the registry. |
| M3-D46 | Neutral mods | `EventMods`/`SkillMods` have `Neutral`; the resolver throws on an invalid (default) value. | A default struct would zero every weight silently. |
| M3-D47 | Lake stand points | 7.16 list; the dock end and pier reach far bands, banks reach near bands (spec 12.5). | Spec names no positions. |
| M3-D48 | Shore dm cap | `ShoreDmMax` 0xFFFE (6553.4 m) for water cells with no land in the grid. | Avoids colliding with the not-water marker. |
| M3-D49 | Hotspot draws | Delay, cell, duration per spawn in that order; `ForceAt` draws nothing. | Determinism (2.8 #25). |
| M3-D50 | Journal entry count | Journal = all species with `isSpecial` or a rule (25), in id order. | Spec "25 entries". |
| M3-D51 | Clients and dev panel | Read-only, no RPCs; the host calls drivers directly. | Spec 12.11, 17.3. |
| M3-D53 | `IWorldContext` additions [ruling] | Beyond spec 12.4's four members: `CastingOpen` (exact minute < 1200, the gate; `Minute` is clamped for lookups), `HasState`, `SpotId`, `Changed`. | The day-over gate cannot read a clamped minute; consumers need a change event and the spot. Spec delta: 12.4. |
| M3-D54 | Test bait data [ruling] | `EcologyTestData` (T13) builds its own `BaitStats` with explicit M3 fields (`AffinityMult` 1, `SpecialMult` 1 unless a test says otherwise); it never reuses `FishingTestData` baits, whose M3 fields would be 0 and silently disable specials. | B4. |
| M3-D52 | `minLandDepth` (Q1 default) | Lowered 0.8 -> 0.5. This is a **spec value change** (spec 9.3/9.12 delta, pending the user's Q1; the default applies), applied to the asset, the seed and the T3 expectation; it is NOT a `TunedDeviations` entry (M3-D74). With 0.8 the lake reeds band is uncastable (B10). Alternative if the user prefers: a near-shore shelf in the lake bed (lake layout change, T16). | Spec 12.5/12.6 need casts into the reeds. |
| M3-D55 | Species-gate removal file fallout (CCR-D1) [ruling] | Accepted as proposed (the primary option, not the keep-the-fields fallback). T13 owns the mechanical fixes to every caller of the removed `MinBaitRating`/`MinDepth` fields and of `PlaceholderRoller`/`RollRequest` (file list in 2.6 and 12), with the public surface frozen: `FishingTestData` loses only the two initializers (no fight value changes; the one exception to its R72 ownership), `FishingRollValidationTests` and `FishingWireTests` are re-pointed at `FishingRoller.Roll` over `EcologyTestData` with names kept, and two tests are deleted because V3 replaces them (`FishingValidators_UnreachableSpecies_IsReported`, `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth`). If R72 lands later and reshapes `FishingTestData`, T13 re-applies the same two-initializer removal. | The removals break compile outside the T13 list that C 2.6/12 gave; R72 lands first. |
| M3-D56 | `WorldState` registration lifetime (CCR-D2) [ruling] | `ServiceRegistry` has no remove method. `WorldState` calls `ServiceRoots.Game.Replace<IWorldContext>(this)` on spawn; on despawn `HasState` becomes false and `Current` is cleared; the entry dies with the scene's `GameScope`. No `TryRemove` is added. | "Unregisters on despawn" had no API; consumers already check `HasState`. |
| M3-D57 | Run seed source (CCR-D3) [ruling] | `EcologySession.GetOrCreate(seed)` returns the existing run state and ignores the seed; otherwise it creates one (season rolled from the seed, Day 1). `DevWorldDriver` calls `GetOrCreate(FishingConstants.DefaultDevRunSeed)`; `FishingHost.SetRunSeed` (only while `HostCastCounter == 0`) calls `Clear()` then `GetOrCreate(newSeed)`; the driver notices the new instance by reference and re-adopts it. M3 keeps M2's fixed dev run seed (see the resolution note). | Gameplay cannot reference Fishing, so the driver cannot read `FishingHost.RunSeed`. |
| M3-D58 | Extra helper and test files (CCR-D4) [ruling] | T14 adds internal `Gameplay/World/DevClock.cs` (`DevClock`, `DevWorldRules`) and `Tests/EditMode/DevWorldTests.cs`. T15 adds internal `Fishing/Host/SpotEcology.cs` and tests `Tests/EditMode/SpotEcologyTests.cs`, `Tests/PlayMode/FishingWorldTests.cs`. T13 adds one test `FishingHostLedger_InterimLakeContext_BluegillEligible`, which T15 deletes with the interim. Listed in 2.6, 10 and 12. | "Others may be added" (C 10); recorded so the manifests are complete. |
| M3-D59 | `WorldState` field and init hook for T16 (CCR-D5) [ruling] | The serialized spot field is named `spotId`; `WorldSceneObjects.AddTo` writes it with `SerializedObject.FindProperty("spotId")`. `WorldState.OnNetworkSpawn` (server) calls the internal `DevWorldDriver.ServerInitialize(this)` on the same GameObject, so both components sit on the one `World` root. | T16 and T14 must agree on the name; C 6.3 already puts both on one root. |
| M3-D60 | R72 bot visibility (CR-1) [applied by R72 F4] | The `FishingBots` members `BotKind`, `FightRecord` (and its fields), `SeedFor`, `CastFor`, `RunFight` become `internal`, no behaviour change. **Applied by R72 F4 (the fight builder's brief): FishingBots members internal.** No M3 task edits `Tests/EditMode/Tuning/FishingBots.cs`; T11 step 0 only confirms the members are internal and stops and reports if not. | T11 and T12 must call the R72 Rhythm bot and must not redefine it (1.2 #1). |
| M3-D61 | Seed routing and the M2 tests it breaks (CR-2) [ruling] | `FishingSeedData.Species` and `FishingSeedData.Junk` both return the `EcologySeedData` lists after T10 (so `FishingSeedData.BuildDatabase()` has every junk index the zones use). T10 edits `FishingContentTests` (species and junk blocks of `FishingData_SeedsMatchSpecTables` become "same list as `EcologySeedData`" checks; the junk count in `FishingData_Ids_UniquePerCategory`) and `ContentAssetParityTests` (the species-model test checks only species whose model file exists; C 2.8 #28). No other edit. | The count asserts T3 sets to 1 and the "every seed species has a model" rule contradict the 25-species roster and the fallback mesh. |
| M3-D62 | Who creates species and junk assets (CR-3) [ruling] | `FishingContentBuilder` stays the ONLY creator of species and junk assets (its create-if-missing loops read the forwarded seeds and fill the reused models in the same pass; T23 relies on that fill). `EcologyContentBuilder` only checks that every species and junk asset its zones reference exists, else `InvalidOperationException` naming it. 7.10 and 6.2 are amended. | Otherwise the first `build` would leave the reused models empty and the second would change assets. |
| M3-D63 | Harbour registration gate (CR-4) [ruling] | T10 creates `Content/Spots/harbour_pier.asset` but appends it to `registry.spots` only when `SceneNames.BuildOrder` contains the string `"Spot_HarbourPier"` (run-time `Array.IndexOf`; T10 never references T17's constant). T17's first `build` then lists it in the Content step, before the Validate step reads the build list. No file edit in T17. | `Validators.ContentIds` fails any registry spot whose scene is not in the build list, which would break `build` from T10 to T17. |
| M3-D64 | V5 cycle-time source (CR-5) [ruling] | T12 measures the Rhythm-bot fight medians itself on all four rods (`rod_twig`, `rod_handmade`, `rod_carbon`, `rod_deepsea`; same seeds, same casts, real assets); Handmade and Twig equal R72 F8's numbers by construction and are printed next to F8's table as a check. Cycle time = cast overhead + expected bite wait (spec 9.4 terms) + fight median + 10 s walking. | F4/F8 measure only Handmade and Twig, but the rod path needs Carbon and Deep-sea (M3-D39 amended). |
| M3-D65 | 13.3 variety rule (CR-6) [spec delta, PENDING] | The rule "a species eligible in exactly one band" fails for every spot and season of the 12.6 roster (every row has 2 or more bands; R1 split each old band in two). Ruled text, new spec 13.3 wording: **"a species eligible in at most two adjacent bands (Night and Dawn are not adjacent)"**. The controller applies it to the spec AFTER the R72 F0 spec edit lands (same file; not edited here). Until then 11.2 and the test carry the ruled wording; the test keeps its name and prints the literal single-band count. | Every (spot, season) passes the ruled rule (lake: `sunny_trout`, `glimmer_eel`, `frost_char`; harbour: `grumpy_puffer`, `bruiser_tuna`, `moon_squid`, `storm_ray`, `lanternfish`). |
| M3-D66 | Coverage, variety and balance inputs (CR-7) [ruling; spec 12.9/13.3 deltas pending] | V4 and V5 use neutral bait (`BaitMods.Neutral(1)`) with bite speed 1.0. 13.3 "eligible" = some non-special rule passes at rod 3, bait rating 10, depth 6 m, in any of Clear/Rain/Fog and any sub-area. "Differs by >= 2" = the symmetric difference of the Morning and Night eligible sets has >= 2 species. Specials are excluded from V2 shares, the 13.3 counts and V5 economy (catch-limited, they do not scale with time) and included in V3 and V4. Best-for-coins ties go to the lower sub-area index. | The contract left these undefined; the bare hook's `junkMult` 1.5 puts V4's mean junk near 21%, so neutral bait is the stated basis. |
| M3-D67 | Core test fixture (CR-B1) [ruling] | T5 owns `Tests/EditMode/Ecology/EcologyCoreTestData.cs` (internal static, test assembly), read-only for T6-T8. It is a different data set from T13's `EcologyTestData`; neither edits the other. | Shared Core fixture (M2 B-Q1 precedent). |
| M3-D68 | `BaitStats.Mods` owner (CR-B2) [ruling] | T5 also edits `Core/Fishing/BaitStats.cs` (`Mods(int loadedCount)`); 2.6 row amended. | C 7.3 and C 12 already assigned it to T5. |
| M3-D69 | F6/F7 reset trigger (CCR-A1) [ruling] | Reset on `INetworkSession.Ended` only (via `FishingDebugSettings.ClearOnSessionEnd`), plus `SubsystemRegistration`; never on `ServiceRoots.Game` teardown. T15's `Luck` is reset by the same handler. | `GameScope.OnDestroy` fires on every Spot single-load, which is exactly the map-switch case the carry item must survive. |
| M3-D70 | EditMode access to Gameplay internals (CCR-A2) [ruling] | T2 adds `InternalsVisibleTo("FishGame.Tests.EditMode")` to `Gameplay/AssemblyInfo.cs` (PlayMode line and marker kept); T14 finds it present. | `DisplayNameRules_Truncate_SharedByNameWireAndSessionText` is EditMode and calls `internal` Gameplay members. |
| M3-D71 | Online-session test seam (CCR-A3) [ruling] | `public static Func<INetworkSession> OnlineSessionFactoryOverride { get; set; }` on `AppBootstrap` (null in production, reset in `ResetStatics`, read lazily by the online backend factory). The fallback (InternalsVisibleTo on UI) is not used. | `UiFlowController` captures the session on the first frame; precedent `SuppressDevAutoStart`. |
| M3-D72 | Truncation helper shape (CCR-A4) [ruling] | `DisplayNameRules.Truncate(clean, maxChars)` stays a char-count primitive; `SessionText.FitBytes` keeps the UTF-8 byte loop and calls it per step; `NameWire.ToFixed` delegates to `SessionText.Name`. Recorded behaviour change: `SessionText.Name` now drops lone surrogates. | The 29-byte budget is bytes, not chars. |
| M3-D73 | T1/T3 test-file manifests (CCR-A5) [ruling] | The M2 test files T1 and T3 must edit are added to 2.6 and 12 (lists there). | They break or need the new fields; 2.6 named only `FishingContentTests`. |
| M3-D74 | `minLandDepth` 0.5 is a spec value (CCR-A6) [ruling; spec 9.3/9.12 delta pending] | The seed becomes 0.5 and `FishingData_SeedsMatchSpecTables` expects 0.5; there is no `TunedDeviations` entry. If the user keeps 0.8, T3 skips the write. | Removes the two readings of the old 7.3 sentence. |
| M3-D75 | `Configure` defaults (CCR-A7) [ruling] | New `FishSpeciesDef`/`BaitDef` `Configure` parameters are optional and trailing (`tags = FishTags.None`, `affinityTags = None`, `affinityMult = 1f`, `specialMult = 1f`); the two multiplier fields carry `= 1f`. These are neutral safety defaults, an explicit exception to "no tuning initializers"; builder and migration always pass explicit values. | About 20 in-memory call sites keep compiling; a default of 0 would silently disable specials (B4). |
| M3-D76 | Serializable helper naming [ruling] | `[Serializable]` helpers (`SubAreaBand`, `SpawnRule`, `JunkWeight`, `SunMoonSettings`, `WeatherAmbient`, `SeasonAmbience`) use public camelCase fields as named in 7.9, with no PascalCase properties; sections E, F and G use those names. | Section C's cross-section note, accepted; the M2 `SfxCueRow` precedent. |
| M3-D77 | Section B open questions [ruling] | Defaults kept: the day opens with the seed `sunriseFraction 0` (a sun fade-in of about 18 s under ambient light at minute 0; the alternative `-0.02` is data only), and a dev-forced species (F6) ignores catch limits and reserves nothing. | Both are data or dev-only behaviour with no contract effect. |
| M3-D78 | Season tint under R74 (CCR-F1) [ruling] | Accepted as proposed (the primary option). M3-D23 and 7.18 are rewritten in place: the season tint on textured environment albedo IS the `_FGPalette` season swap; T18 adds no shader term (the 2.6 shaders row says "no edit" for T18); `PaletteGlobals` pushes `_FGSeasonTint` as a dormant hook that no M3 shader reads; the first task that needs a full-colour, season-following material adds the opt-in `_FGSeasonTintWeight` term and its test. **Option B (T18 adds that opt-in term to `Toon.shader` now) is not used (CCR accepted 2026-10-13).** The section F plan keeps T18 step 0 (re-check the merged `Toon.shader`; stop if a season term exists) and the guard test `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette`; **step 0 and the guard cover BOTH textured env materials, `M_Toon_Base` (uv0) and `M_Toon_Tri` (world triplanar: rocks and canopies), the guard looping over the two (review fix 2026-10-13).** **Verified 2026-10-13 against the merged `Shaders/Toon.shader` (R74 adoption, d660ee9):** albedo = `(_UsePalette ? _FGPalette[round(uv1.x)] : 1) x vertexFactor x _BaseColor`, times `texFactor = lerp(1, sample(_FGAlbedoArray) x _FGArrayGain, _FGTexWeight)` in the fragment (header lines 2-3 and 12-16; code lines 193-195 and 228). `M_Toon_Base` and `M_Toon_Tri` are built with `usePalette` true and the env detail array at gain 2 (value-only detail centred on 0.5, hue from the palette), so the assumption holds. `M_Toon_Fish`, `M_Toon_FishFallback`, `M_Body_Slot*`, `M_Skin` and `M_Toon_Brass` are built with `usePalette` false (`_BaseColor`, or the full-colour fish skin array at gain 1) and are season-invariant. No `_FGSeasonTint` appears anywhere under `Shaders/` or in `RenderMaterialLibrary`. **Note 2026-10-13 (seasonal trees, spec 10.2):** palette slots 12-15 are now PineLight, PineDark, Blossom, FoliageAlt (the tint array stays 16 long), and `PaletteGlobals.Apply` hands each pushed season to `SeasonalVisibility.SetSeason`, which T18 must keep. | A `season / base` multiply on the palette path would apply the season twice; the standard already keeps the M3 season swap working (style guide 2.2, 2.3). |
| M3-D79 | Weather look owner (CCR-F2) [ruling] | Accepted. `DayNightController` (T18) owns the weather look: it reads `IWorldContext.Weather`, cross-fades the `WeatherAmbient` over 4 s, and applies `lightMult`, `desaturate` and the fog terms; it exposes the blended look to its assembly (`internal WeatherAmbient ActiveLook`, `internal Color ParticleTint`). `WeatherFx` (T19) owns the rain particles and the `Rain_Patter` loop (driven by `ActiveLook.rainAmount`; its `follow` is its own transform, not the camera). 7.18 and 7.19 are amended; no public surface changes. | T18 lands first and its required test `DayNight_FogReadableAt25m_EveryPhaseAndWeather` needs the weather looks on fog and light; otherwise T19 would edit T18's lighting. |
| M3-D80 | M2 palette test re-point (CCR-F3) [ruling] | Accepted. T18 edits `Tests/PlayMode/PresentationWorldTests.cs`, in `PresentationWorld_Palette_IsLinearAndAppliedOnce` only: it first pins the world through `DevWorldDriver` (clock stopped, minute 300, weather override Clear, season = `M2Season`), then keeps every existing assertion; the name is kept. 2.6 and 12 list the file. | From T18 the solo world starts at minute 0 (Dawn) in the rolled season, so the M2 test fails whatever season is rolled. |
| M3-D81 | Rain loop vs M2 feedback tests (CCR-F4) [ruling, CONDITIONAL] | Accepted as a conditional ruling. Only if T19 step 0 finds that day 1 segment 0 of the fixed dev seed (M3-D57) is Rain does T19 edit `Tests/PlayMode/FeedbackSoloTests.cs` (`FindLoop` with no name skips `Loop_Rain_Patter`; no other edit). If the segment is Clear or Fog, nothing changes and the request is withdrawn. | `FindLoop` matches any `AudioBus` child named `Loop_*`, and the rain ambience loop keeps playing while paused. |
| M3-D82 | `FinalizeAfterSave` (E-CCR-1) [ruling] | Accepted. NGO 2.13.3 derives `GlobalObjectIdHash` in the internal `NetworkObject.OnValidate` from `GlobalObjectId.GetGlobalObjectIdSlow`, which has no scene identity before the scene is saved. T16 adds `internal WorldSceneObjects.FinalizeAfterSave(Scene scene, string scenePath)` and one more `LakeDockBuilder.Build()` hook line right after `BuilderUtil.SaveScene(...)`: it runs `OnValidate` by reflection on the saved scene's `World` NetworkObject, checks the hash is non-zero, `SetDirty`s it and saves the scene a second time. T17's `HarbourPierBuilder` calls it the same way. `AddTo` keeps its signature and still calls `OnValidate` + `SetDirty` (harmless before the save). 2.6, 7.16 and 12 are amended. | The M2 helper `BuilderUtil.RefreshNetworkObjectHash` is prefab-only; an in-scene object needs a saved scene (T16 step 4 records the evidence: expected hash 0 before the save, non-zero after). |
| M3-D83 | Reachability walks the registered spots (E-CCR-2) [ruling] | Accepted. `MapChecks` reachability walks `registry.Spots`: for each spot its zone's grid must be baked from that spot's scene and pass reachability, with zones built fresh (`ToCore`), never from the cached `registry.Ecology`. The harbour spot is listed only once `SceneNames.BuildOrder` contains `Spot_HarbourPier` (M3-D63), which T17 adds in the commit that bakes the harbour grid. 11.3 reads "each sub-area of the zone of each registered spot". | `harbour_pier_grid.asset` exists unbaked from T10; walking every zone would turn `build` red from T16 to T17. Mirrors M3-D63. |
| M3-D84 | Map PlayMode tests (E-CCR-3) [ruling] | Accepted. One extra file `Tests/PlayMode/EcologyMapPlayTests.cs`: T16 creates it with `EcologyMap_LakeSolo_WorldStateSpawnsWithState`; T17 appends `EcologyMap_HarbourDevLoad_SpawnsOnPierAndProbesEverySubArea`, which publishes T15's `DevLoadSpotRequested` from the test. Added to 10.4 and to the 12 rows of T16 and T17. | T24 runs long after T15 depends on the lake `World`, and the harbour is not reachable in play mode before T22's button. |
| M3-D85 | `SceneNamesTests` (E-CCR-4) [ruling] | Accepted. T17 edits `Tests/EditMode/SceneNamesTests.cs` `SceneNames_BuildOrder_BootstrapFirst`: the expectation gains `"Spot_HarbourPier"` at index 3; the test name is kept; no other edit. 2.6 and 12 list the file. | The M2 test asserts `BuildOrder` equals exactly `{ Bootstrap, Menu, Spot_LakeDock }`; C 2.6 gave `SceneNames.cs` to T17 but not the test. |
| M3-D86 | `BakeAllMenu` opens scenes singly (E-CCR-5) [ruling] | Accepted (behaviour note, no public change). `BakeAllMenu` offers to save modified scenes, records the scene setup, opens each registered Spot scene alone (`OpenSceneMode.Single`), bakes it and restores the setup; `BakeOpenScene` throws when any other loaded scene holds a `WaterVolume`. 6.4 and 7.16 replace "additively". | Two Spot scenes loaded together stack two 120 m `WaterVolume` boxes at the origin of the physics scene and the bake rays would mix the maps. |
| M3-D87 | `PlayerInputFrame.JournalPressed` (G-CR1) [ruling] | Accepted. T21 adds `public bool JournalPressed;` to `Core/Input/PlayerInputFrame.cs` (no fallback: required) in addition to `PlayerIntent`; the file joins the 2.6 input row and the 12 T21 row; B12 is corrected. | `InputContextRouter.Route` builds `PlayerIntent` from `PlayerInputFrame`, which has no journal field, so `InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly` could not be written. |
| M3-D88 | Journal toast keys in T20 (G-CR2) [ruling] | Accepted: T20 adds the three keys `toast.journal_new`, `toast.journal_best`, `toast.journal_unlock` to `UI/UiStrings.cs` (the 2.6 UI-strings rows and the 5.3 owner column read T2, T20 (three keys only), T21, T22). **The fallback (T20 publishes the keys and the raw key shows until T21) is not used (CCR accepted 2026-10-13).** | `JournalRecorder` is in Fishing, which cannot reference UI, so it publishes keys; C 5.3 had given them to T21, which runs later. |
| M3-D89 | `IDevInput.IsHeld` (G-CR3) [ruling] | Accepted (the primary option). `Runtime/Input/IDevInput.cs` gains `bool IsHeld(DevKey key)` with the same gating as `WasPressed` (dev build, window focus, not Menu), implemented by `PlayerInputAdapter` and `FakeInputService` (held set, false by default). `DevKey.ScrubClock = 6` (F8) reads it; `DevKey.CycleForcedSpecies = 7` (F10) is unchanged. **The fallback (overload `WasPressed(DevKey.ScrubClock)` with the held state) is not used (CCR accepted 2026-10-13).** | "F8 held = x60" (M3-D32) needs a held state and `IDevInput` only had `WasPressed`. |
| M3-D90 | Journal manifest extras (G-CR4) [ruling] | Accepted. T20 adds the extra EditMode file `Tests/EditMode/JournalRecorderTests.cs` (through the internal pure seam `JournalRecorder.Record`). T21 adds the internal `UI/Journal/WorldGlyphs.cs` (season, weather and band glyphs, read-only for T22's clock HUD) and an internal HUD-modal seam inside `UiFlowController.cs` that T22's dev panel uses (no new file). Listed in 2.6, 7.20, 7.21 and 12 (the M3-D58 precedent). | Keeps the manifests complete. |
| M3-D91 | Fish skin sheet and species layers (G-CR5) [ruling] | Accepted. T23 owns `Content/Textures/T_FishSkinArray.png` (+ `.meta`), the `FishSkinArray` row of `Editor/Builders/Render/TextureArrayPostprocessor.cs` (`Rows` 2 -> 8, max texture size 2048 -> 4096 for that array only, `GetVersion` +1, the stale layer comment corrected) and the pinned T4 test `Tests/EditMode/SmoothMeshBuilderTests.cs` `TextureArrays_ImportAsFlipbookArrays` (`skin.depth` 8 -> 32). **Layer numbering after the R74 adoption (d660ee9, style guide 2.2, fish commit 3de9de1):** layers 0-5 are used (0 white, 1 bluegill, 2 perch, 3 carp, 4 trout, 5 catfish) and never renumbered; layers 6 and 7 are white tiles of the current 4 x 2 sheet. The 20 new species take layers **6-25** in T23 batch order (5 / 4 / 6 / 5 = 6-10, 11-14, 15-20, 21-25); 26-31 stay white. T4 did NOT grow the sheet, so T23 grows it to 4 x 8 (2048 x 4096 px) in batch 1. **The fallback (a second array `T_FishSkinArray2`, which needs a shader change) is not used (CCR accepted 2026-10-13).** | 26 used layers need 32 tiles; at today's 2048 cap Unity would silently halve every tile of a 2048 x 4096 sheet. |
| M3-D92 | Idle escape (user playtest 2026-10-13, F7; spec R77, 9.2, 9.5, 9.12) [user ruling] | `FishingTuning`/`FishingTuningDef`/`FishingSeedData`/`FishingTuning.asset` gain `idleEscapeTime` (seed 30 s; validator range 10-120 and `< fightTimeout`; `<= 0` = off in code, so a fixture that leaves the field unset never escapes at once). `FightState.IdleTimer` counts seconds with no reel and no raw steer press (`abs(steer) >= SteerDigitalThreshold`, NaN = none; the effective steer is NOT used, a dropped wrong-way steer still counts). At `IdleTimer >= idleEscapeTime` a FISH fight ends `Escaped(ThrewHook)` after the Landed, Snapped and Timeout checks. **No wire change:** the existing `EscapeReason.ThrewHook` is used; the host takes it on any hooked cast (no clock check) and announces it Lost, whereas an owner `Timeout` needs the host clock at `>= fightTimeout - 2 s`. `fightTimeout` 120 s stays the overall cap, `maxFightTime` 240 s the host fallback, junk keeps `slackEscapeTime` 12 s (reel only). Tests: `FightOpeningAndIdleTests`, `RodStateMachineTests`, `FishingHostLedgerTests` (`OwnerIdleEscape`). | Walked-away players lose the fish at 30 s instead of 120 s. |
| M3-D93 | Opening reel surge (F1; spec R77, 9.5, 9.12) [user ruling] | Cause checked first: no input gating, `HookBeat` is the pre-fight auto-hook delay, presentation eases at 20/s; the reel was x1 at tire 0 with an opening back pull cancelling it. New tuning `reelOpenMult` (seed 2.5; range 1-5) and `reelOpenSeconds` (seed 0.8; range 0.2-3). `FightSim`: reel multiplier `= max(tireReel, 1 + (reelOpenMult - 1) x (1 - clamp01(elapsed / reelOpenSeconds)))` (a floor under the tire curve, never a product) and the back pull is scaled by `clamp01(elapsed / reelOpenSeconds)`; strain, the RNG draw order and the first-run choice are untouched. `reelOpenMult <= 1` or `reelOpenSeconds <= 0` = off. **Validator rule `reelOpenMult <= tireReelMaxMult`** keeps `CatchValidator.MaxReelSpeed` (the host Landed floor) valid with no host change. `FishingTestData.Tuning()` ships the surge OFF (`reelOpenMult` 1) so the Rust hand checks keep their maths; `FightOpeningAndIdleTests` turns it on. | The user asked reeling to visibly take line within about 0.2 s of the hook-set. |
| M3-D94 | Recast timeline (S10; spec R77, 9.2) [user ruling] | `FishingConstants.FeedbackBeatSeconds` 1.5 -> 0.6 (shared by Landed, Snapped, Escaped and an unsolicited commit; it also feeds the V5 cycle overhead, so `EcologyBalanceReports` recomputes). `RodStateMachine`: when a terminal state ends (beat done and commit received, or `commitTimeout`) and Secondary is held, the machine enters Idle for that tick and Readying on the next (`_aimCarry`); it never skips Idle, so the controller's Idle bookkeeping (telemetry record, landing reset, rumble off) still runs. Idle still waits for the commit: `resultTimeout` 3 s and `commitTimeout` 3 s are unchanged, as is every host check. `CatchRevealView` listens to the rod's `Cue` and hides on `ChargeStart`/`CastRelease`, so the reveal never blocks a recast. **Review fix:** because the owner is in Idle for one frame only when aim is held, a remote usually observes `Escaped -> Readying`; `BobberView` now recalls on ANY exit from the line-out set (`FightViewMath.RecallOnLeavingLineOut`, tested), not only `-> Idle`; before, the remote bobber glided to world origin (`BobberPoint` is zero for no-line states). Earliest cast about 1.05 s after the landing (was 1.85 s), about 1.4 s with a 0.4 s charge. With aim-toggle ON the latched Secondary can survive the fight, so a catch then auto-readies the rod (reviewer: confirm that is acceptable). | The user found the gap between catches too long. |
| M3-D95 | Fight pace retune (F10; spec R77, 9.6, 9.12) [user ruling] | Rhythm-bot fights shortened with the strain path untouched. Seeds and assets (hand-edited YAML, parity-checked by `FishingContentTests.FishingData_AssetValuesEqualSeeds`): `tireRunFull` 20 -> 12, `tireSteerRate` 0.8 -> 1.2, `backMoveSpeed` 1.3 -> 1.0; profile `reelInMult` Darter 1.0 -> 1.3, Thrasher 1.0 -> 1.15, Bulldog 0.9 -> 1.0, Runner 0.9 -> 0.95 (`FishingContentTests.TunedDeviations` carries all seven). Unchanged: `strainBarFull`, `strainSnap`, every strain gain, `relaxRate`, `strainScale`, `tireCounterRate`, `tireReelFull`, `tireReelMaxMult`, the rods and `FishingTestData` (the Rust values the hand checks use). Rhythm medians (Handmade / Twig): Steady 13.7 / 13.9, Darter 13.2 / 15.5, Thrasher 13.8 / 17.2, Runner 20.4 / 21.6, Bulldog 19.3 / 20.5 (`FishingBots.RhythmBands` re-pinned). Greedy snap seconds identical; Reckless and Pump pins held (thin: Reckless Steady Handmade 55.5% vs 50%, Pump Runner Handmade 33% vs 30%). **Host:** `MaxReelSpeed` is the same formula with the new `reelInMult` values; `FightPaceHostTests` checks 8,943 bot landings against `LandedFloorSeconds` (worst margin 2.98 s) and that no tick beats `MaxReelSpeed`. V4/V5/13.3 re-run: all green (V5 worst best-sub-area share 12/18, unchanged). **Option B** (data-only, deeper cut, loosens the Reckless pins) is in spec 9.12 for the user to choose. | The user found fights too long for 15 minute days without wanting them easier. |

### Section-author CCR resolution (2026-10-12)

The controller ruled on every contract change request collected from the four section plans (A: `2026-10-13-m3-plan-carry-migration.md`, B: `...-core.md`, C: `...-data-coverage.md`, D: `...-host-world.md`): **ACCEPT ALL as proposed by their authors** (the primary options, never the fallbacks). Applied above and recorded as M3-D55..D77:
- D (CCR-D1..D5) -> M3-D55..D59; C (CR-1..CR-7) -> M3-D60..D66; B (CR-B1, CR-B2) -> M3-D67, D68; A (CCR-A1..A7) -> M3-D69..D75; field naming -> M3-D76; B open questions -> M3-D77.
- **CR-1 is applied by R72 F4** ("FishingBots members internal"); M3 records it and touches no file for it.
- **CR-6 is a pending spec delta** (M3-D65). Exact new text for spec 13.3: "a species eligible in at most two adjacent bands (Night and Dawn are not adjacent)". The spec is not edited by this amendment; the controller applies it after R72 F0 lands. M3-D66 (12.9/13.3 definitions) and M3-D74 (9.3/9.12 `minLandDepth`) are pending spec deltas of the same kind (the controller writes them after the contract is approved, per the intro of this section).
- Defaults that apply unless the user answers: Q1 `minLandDepth` 0.5 (M3-D52/D74); A-Q2 the three `ShaderValidatorTests` cases that T1 names; A-Q3 keep M1 behaviour (a cancel that loses the race to Connected lets the game load); B's two open questions (M3-D77); and **the user question from section D, not ruled here: M3 keeps M2's fixed dev run seed** (the same season and weather every session until the dev panel changes it) rather than a random seed per session.
- Cross-section mismatches fixed in the section plans: the run state starts at the rolled season (not Spring) and `HotspotPlanner.Advance` catches up in one call (section B answered section D); `ClearOnSessionEnd` is the name of T1's reset (section A answered section D); T14 no longer edits `Gameplay/AssemblyInfo.cs` (T2 does); section C's CR-6 text uses the ruled wording; fallback paths of the accepted CCRs are marked "(CCR accepted 2026-10-12)".

### Section E-G CCR resolution (2026-10-13)

The controller ruled on every contract change request collected from the three remaining section plans (E: `2026-10-13-m3-plan-maps.md`, F: `...-presentation.md`, G: `...-journal-ui.md`): **ACCEPT ALL as ruled** (the primary options, never the fallbacks). Applied above and recorded as M3-D78..D91:
- F (CCR-F1..F4) -> M3-D78..D81; E (E-CCR-1..5) -> M3-D82..D86; G (G-CR1..G-CR5) -> M3-D87..D91. M3-D23 is rewritten in place by M3-D78 (CCR-F1).
- **Rejected fallbacks, marked "not used (CCR accepted 2026-10-13)" in the contract and in the section plans:** Option B of CCR-F1 (a live opt-in season-tint term in `Toon.shader`), the G-CR2 fallback (raw toast keys until T21), the G-CR3 fallback (`WasPressed` returning the held state) and the G-CR5 fallback (a second fish skin array). The M3-D18 `WorldState` prefab fallback is NOT a rejected CCR fallback: it stays a live contingency if the T16 MPPM sync check fails. The lake-shelf alternative to `minLandDepth` 0.5 (M3-D52) and the invisible-wall versus headland alternatives for the harbour are user or art calls with defaults kept, not CCR fallbacks.
- **CCR-F4 is accepted conditionally** (M3-D81): it applies only if T19 step 0 finds Rain in day 1 segment 0 of the fixed dev seed.
- **R74 adoption checks (2026-10-13, after d660ee9):** section F's assumption that environment albedo = palette x detail texture holds against the merged `Toon.shader` (M3-D78 has the evidence); the fish skin sheet uses layers 0-5, so section G's batch table (layers 6-25) is correct once its "if T4 used 0-5" condition is replaced by the fact (M3-D91). The stale "2..7 white" comment in `TextureArrayPostprocessor.cs` was corrected in the review fixes (layers 2-5 are perch, carp, trout, catfish); `SmoothMeshBuilderTests` still pins `skin.depth == 8` and the 4 x 2 sheet cap, which is T23's to change.
- Defaults that apply unless the user answers (open questions of the three plans): E: the harbour play-through happens when T22's button exists (T22/T25 checklist), invisible boundary walls at the harbour's open edges with a 240 m water visual, and the small layout deviations from M3-D40 while reachability holds; F: rain 450 drops/s in a 24 m box, the M2 night data with the moon at 0.25, and the 18 s sunrise fade-in of M3-D77; G: journal level 1 lists each seen spot with the species' sub-areas in that spot's zone, T21's icon recipe with a user check at T21's look check, a gamepad B (UI Cancel) closes the journal, and T23 batches of 5 / 4 / 6 / 5.
- Risk to raise with the user if it bites: the lake reeds' castable ring is only about 1.4 m wide (shore distance 3.3-4.8 m); if the T16 bake gives fewer than 25 reachable castable cells in `reeds`, the lake-shelf alternative of M3-D52 is the user's call, not a code fix.
- Cross-section mismatches fixed in the section plans: the stale "if T4 used 0-5" skin-layer conditional (G) and the stale "being merged" T4 wording (E, F, G); the contract's own 7.19 camera-follow and 7.18 fog-formula wording (aligned to section F P8/P9); contract 6.4 "additively". Checked and consistent, no edit needed: `ProtocolVersion` (the contract says 4; the code is still 3 until T14), `DevKey` values (`ScrubClock = 6`, `CycleForcedSpecies = 7`, matching `Runtime/Input/DevKey.cs`), the `WorldState` field name `spotId` (E T16.3 and G match M3-D59) and the M3-D76 camelCase helper field names used by E, F and G.

---

## 14. M2 carry list folded into M3

| Source | Item | M3 task |
|---|---|---|
| C2 16.1 final row | Drawn marker trails one frame (UpdateMarker before Tick) | T1 |
| C2 16.1 final row | Parity test comment overclaims M-4 coverage | T1 |
| C2 16.1 final row | M-3 zero-alloc test lacks a positive control | T1 |
| C2 16.1 final row | F6/F7 reset on any Fishing `OnDisable` (spot changes mid-run from M3) | T1 (and relied on by T15's map switch) |
| C2 16.1 final row | Polish notes: ShaderValidatorTests 3 extra cases, HUD fake-null creel lookup, `0.625 x reel` comment | T1 |
| M2 handoff "Later (M3/M4)" | Guard `ContentHash` against duplicate category names | T2 |
| M2 handoff "Later (M3/M4)" | Unify `NameWire` and `SessionText` name truncation before the crew panel | T2 |
| M2 handoff "Later (M3/M4)" | Online Cancel keeps the Busy screen up until the SDK returns: show "Cancelling..." at once | T2 |
| C2 D-Q9 | `SfxCueTable` growth note for M3 | T19 (append-missing-rows rule) |
| C2 7.30 M4 note | `FishCaught` is published even without a stored fish | unchanged; M4 (journal uses `CatchAnnounced`, never `FishCaught`) |
| Ladders plan open questions (wading next to the bell, edge grabbiness) | M2 playtest items | not M3 (stay with the M2 rework) |

---

## 15. Risks (contract-level)

1. **Entry gate slips** (R72 F0-F8, L7-L9, look-dev approval). T1-T16, T20 and T22 run without look-dev; T17, T18, T19, T21 and T23 wait for it.
2. **First in-scene NetworkObject** (2.8 #24): hash churn on every build; MPPM sync must be verified in T16 (before T15 relies on it and before T17 copies the pattern). Fallback named (M3-D18).
3. **R74 changes the art pipeline mid-plan:** textures under LFS (quota K17), new material rules, possibly new validators; T18's tint hook depends on the standard. If the style guide lands late, the harbour and models slip but the fallback mesh keeps the game playable. **Status 2026-10-13:** the adoption landed (d660ee9) and T18's tint question is settled (M3-D78: the palette swap is the tint, no shader edit). The remaining art risk is the fish skin sheet outgrowing the import size cap (M3-D91, handled by T23's batch 1).
4. **Fightability on the R72 baseline** may miss 90% for Bulldog/Runner on Twig; recorded as a tuning item (M3-D38), not fixed in M3.
5. **Balance numbers rest on R72 F8 medians** (V5) and on a uniform cast-spread model (V4); misses become tuning items.
6. **Reeds castability (Q1)** changes a spec number (0.5) or the lake bed; either needs the user's nod.
7. **Bare hook loses its species restriction** (M3-D3): a design shift from 9.7 the user should know about at the M3 playtest.
