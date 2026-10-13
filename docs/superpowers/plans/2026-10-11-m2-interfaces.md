# FishGame M2 - Interface Contract

- **Date:** 2026-10-11
- **Scope:** Milestone M2 Fishing core (spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` section 17.2 and the sections it cites: 4.3-4.9, 6.7-6.9, 7.4-7.6, 8, 9, 10.1-10.5, 11.1-11.4, 11.6-11.7, 18, 19).
- **Builds on:** the M1 contract `docs/superpowers/plans/2026-10-09-m1-interfaces.md` ("M1 contract" below). Every M1 name stays valid unless section 14 here ("Drift adopted") or a row in section 13 says otherwise. Where M1 code and the M1 contract disagree, the code is what exists, and section 14 records it.
- **Status:** binding for every M2 task (T1-T30, listed in `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md`). Amended 2026-10-11 after section planning: controller rulings and the bad-luck nudge are in section 15 and applied inline. Amended 2026-10-12 after T1-T19 were implemented: review findings, the rulings T8-R1/R2, T9-R1 and T16-R1, the added public and internal surface and the process notes are in section 16 and applied inline in sections 2.6, 7, 9, 10.4, 12 and 13. If a section plan disagrees with this file, this file wins. If this file disagrees with the spec, section 13 records why.
- **Audience:** section authors (Opus) and implementers (Sonnet/Haiku) who see their own task text, the global constraints (section 2.8) and this contract.

Every type, file, asset, constant, event, RPC and test that two or more tasks use is defined here once, with an owner task in brackets. A task may add private or internal helpers freely. It must not add, rename or remove anything public listed here, and it must not edit a file owned by another task, except for the shared edits in section 2.6.

---

## 0. Contents

1. Verified facts (M2)
2. Shared conventions (deltas from M1 contract section 2) and global constraints
3. Execution order and dependency graph
4. Assemblies (the one asmdef change)
5. Constants
6. Assets, prefabs, scenes and hierarchies
7. Types by task (public surface)
8. Event, RPC and NetworkVariable catalogue
9. Runtime flows (cast, bite, fight, commit, cancel, timeouts, late join, disconnect)
10. Test inventory (exact names; owners)
11. Commands reference (unchanged)
12. Per-task file manifest
13. Resolved ambiguities and spec deviations (M2)
14. Drift adopted (M1 code vs M1 contract)
15. Amendments after section planning (2026-10-11)
16. Implementation deltas (T1-T19, 2026-10-12)

---

## 1. Verified facts (M2; checked in code or package source on 2026-10-11)

| Id | Fact | Source |
|---|---|---|
| G1 | `NetworkList<T>` requires `T : unmanaged, IEquatable<T>`. NGO's ILPP serializes a user struct in a NetworkVariable, NetworkList or RPC parameter only if it implements `INetworkSerializeByMemcpy` (unmanaged) or `INetworkSerializable` (never both). Core structs cannot implement NGO interfaces, so every wire struct is a Gameplay/Fishing mirror of a Core struct. | `NetworkList.cs:14`; `INetworkSerializableILPP.cs:71-83` |
| G2 | `PlayerSpawner` calls `InstantiateAndSpawn(...)` and only **then** `GetComponent<IServerPlayerInit>().ServerInit(slot, name)` (singular `GetComponent`). So `OnNetworkSpawn` on the server runs before `NetSlot` is set, and only one `IServerPlayerInit` implementation on the prefab ever runs. M2 server-side per-player init hangs off `PlayerNetState.ServerInitialized` (T16). | `PlayerSpawner.cs:173-236` |
| G3 | Both late-joiner entries (`OnSynchronizeComplete` and `OnClientLoadComplete`) end in the same idempotent `SpawnPlayerIfMissing`, which calls `ServerInit`. Anything initialised from `ServerInitialized` is therefore correct for late joiners on either path. | `PlayerSpawner.cs:119-142` |
| G4 | `PlayerLocomotion.SetContext` (lines 332-346) writes `GameplayContext` from `IsClimbing`/`IsSwimming` only, every frame. No per-player movement lock exists. T16 replaces this with the arbiter in 7.13. | code |
| G5 | `PlayerInteractor` only scans while `GameplayContext == Free && !MenuOpen` (line 48). Spec 8.2 also allows Interact in RodReady and RodWaiting. T16 changes the gate. | code |
| G6 | `Fish_Perch.fbx.meta`: `materialImportMode: 2`, `globalScale: 1`, `useFileScale: 1`, `bakeAxisConversion: 0`. The spike import is identity transform, scale 1, nose +Z, flat normals. T20's postprocessor sets material import to None. The spike perch has 5 submeshes and no vertex colours (ruling G-Q6); the staged `Art/Export/Fish_Perch.fbx` (one submesh, per-face LINEAR vertex colours) replaces it in T29. | `Content/Models/Fish_Perch.fbx.meta` |
| G7 | `Pcg32.Range(float, float)` callers today: `AmbienceBed` (lap interval) and `LakeDockBuilder` (rocks, trees). The T1 fix keeps the draw count and every value except the exact-max edge, so the lake layout is byte-identical. | grep |
| G8 | Isolation today: `LocalNetworkSession.OnStateMachineChanged`/`RaiseEnded`, `UgsNetworkSession.OnStateMachineChanged` and the `Teardown.cs` Ended raise each wrap the **whole** invoke in one try/catch. `CompositeNetworkSession.OnBackendStateChanged`/`OnBackendEnded` have no try at all. The comment at `UgsNetworkSession.cs:78` ("Subscribers are isolated") is wrong. | code |
| G9 | Lake geometry for the PlayMode cast fixture (M1 contract 6.5, D34): dock deck x in [-1.5, 1.5], z in [-52, -40], top y 0.6; water trigger top y 0; lakebed depth at the dock end (z -40) is 1.5 m and deepens outward. A player at (0, 0.65, -41) facing +z (yaw 0) with chargeNorm 0.15 (Handmade: 2.5 + 15.5 x 0.15 = 4.83 m) lands (R67: the distance is from the rod tip, so the landing is `CastTipReach` 2.4 + 4.83 = 7.23 m from the player) at z about -33.8, over deeper water than the pre-R67 spot (z about -36.2, about 2 m deep). The host `lineLength0` = max(landDistance, 7.23 - `HookedLineSlack` 3.1) = 4.13 m, so the host's Landed floor is 0.8 x (4.13 - 2.0) / 1.0 - 0.5 = 1.2 s. The G9 minimum marker (charge 0, `castMinDist` 2.5) is `CastTipReach` + 2.5 = 4.9 m from the player, z -36.1. | M1 contract 6.5; spec 9.9 |
| G10 | `SfxGenerator.GenerateAll` and `SfxRecipes.TryGet` iterate only `M1Table`. `AudioBus` plays one-shots only (`loop = false`, no minDistance control, no ducking). | code |

Closed by package-source reading (section 15): V11 `DeferLocal` defers on a host-owned object (net plan N1; the T19 PlayMode test stays as the runtime check) and NetworkVariable change detection uses `IEquatable` (N3). Still to verify at runtime (owners in brackets): V12 trigger-water raycast with `QueryTriggerInteraction.Collide`, water first when a dock is above [T19]; V13 water facet-normal sign (moot since R73: the facet normal is gone), C#/HLSL wave parity, fog variants in a player build, `ShaderUtil` messages in `-nographics` batchmode [T21, T28, T30].

---

## 2. Shared conventions (deltas from M1 contract section 2) and global constraints

M1 contract sections 2.1-2.5 and 2.7 apply unchanged (paths, encoding, namespaces, `$env:FG_OUT`, TDD cadence, editor lock), with these deltas.

### 2.1 Branch and commits
- Branch **`m2-fishing-core`** (not `main`). Commit at each task's commit step only. Never force-push. Never `git add -A` or `git add .`.
- Message shape: `git commit -m "m2(<area>): <summary>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"` (trailer names the model that wrote the change; Haiku tasks use `Claude Haiku 5.5`, controller-made Blender assets use the controller's model).
- `<area>`: `carry, core, fight, rod, ledger, input, audio, water, data, tuning, gameplay, net, rodctl, integration, shaders, render, world, rodview, feedback, hud, dev, telemetry, lakedock, models, docs`.
- FBX files under `Assets/_Project/Content/Models/` are LFS (`.gitattributes` tracks `*.fbx`). `git lfs install --local` is already done. Check `git lfs ls-files` lists every new FBX before committing.

### 2.2 Namespaces
Unchanged (M1 contract 2.2). No sub-namespaces. New folders: `Core/Fishing/`, `Core/Diagnostics/`, `Core/World/`, `Core/Render/`, `Data/Fishing/`, `Data/Art/`, `Data/Audio/`, `Gameplay/Fishing/` (creel, context source), `Fishing/{Net,Host,Owner,Debug}/`, `Presentation/{World,Rod,Feedback}/`, `UI/Fishing/`, `Editor/Builders/Fishing/`, `Editor/Builders/Render/`, `Editor/Validation/`, `Shaders/`.

### 2.3 Hot files and the hook rule
These M1 files are touched by several M2 tasks: `PrefabBuilder`, `ContentBuilder`, `LakeDockBuilder`, `SceneBuilder`, `MaterialLibrary`, `Validators`, `ValidationRunner`, `AssetPaths`, `PlayerNetState`, `PlayerLocomotion`, `PlayerCamera`, `PlayerInteractor`, `InputContextRouter`, `AppBootstrap`, `UiStrings`, `AppRoot.uxml`, `App.uss`, `SettingsData`, `SfxRecipes`, `SfxIds`, `ContentRegistry`, `BuildInfo`, `SessionState`, `DockBell`.
- **Rule:** new behaviour goes in **new files** (`FishingContentBuilder`, `FishingValidators`, `PlayerFishingRigBuilder`, `FishingSceneObjects`, `RenderMaterialLibrary`, ...). The hot file gets a one-line call (a "hook"). Every hook and every other hot-file edit is listed in section 2.6 with its task and order.

### 2.4 TDD cadence
M1 contract 2.4 applies. Pure-Core tasks (T5-T9, T12, parts of T10/T11/T13/T27) are red/green by `test-edit -Filter`. Integration tasks use `compile` + `build` + the named PlayMode or EditMode tests. Every task ends with the full `test-edit` before its commit, and with `test-play` too from T19 on. T1-T4 also end with PlayMode green (the Networking and UI changes in T1/T2 are only reachable from PlayMode; ruling A-Q4). From T22 on, `test-play` runs use `-TimeoutMinutes 15` (ruling F-Q6).

### 2.5 Tiers
Per task in the skeleton. Haiku implements only when the section plan's task body contains the complete code. Section plans say so explicitly in the task header (`Implementer: Haiku (complete code below)`).

### 2.6 Shared-file edits (the only cross-owner edits allowed; in execution order)

| File | Edits (task: what) |
|---|---|
| `Tests/EditMode/FishGame.Tests.EditMode.asmdef` | T1 only (section 4). No other asmdef changes in M2. |
| `Core/Random/Pcg32.cs` | T1 (float `Range` fix + `MapUnitToRange`). |
| `Runtime/Audio/AudioBus.cs` | T1 (warn rate limit via `LogOnce`), T11 (loops, minDistance, ducking, cue overrides, generate all). |
| `Tests/EditMode/AudioBusTests.cs` | T1 creates it (`AudioBus_NotReady_WarnsOncePerId`; ruling A-Q3); T11 adds the unknown-id and missing-clip warn-once cases (T1 review carry-over). |
| `Networking/LocalNetworkSession.cs` | T1 (`EventIsolation` at two raise sites), T2 (split into partial files). |
| `Networking/UgsNetworkSession.cs`, `UgsNetworkSession.Teardown.cs` | T1 (`EventIsolation`, comment fix at :78). |
| `Networking/CompositeNetworkSession.cs` | T1 (`EventIsolation` in both forwarders). |
| `UI/UiFlowController.cs` | T2 (split), T25 (one hook: create `FishingHudPresenter`), T26 (one hook: create `DevOverlayPresenter`). |
| `Tests/PlayMode/{PlayModeHarness,SoloSessionTests,PlayerMovementTests}.cs` | T3 (hardening; follow-up `1c3b423`). T19 adds new PlayMode files, and under the integration-fix rule edited `PlayModeHarness.cs` once (`0c070a0`: the original leak or problem message is written with `TestContext.WriteLine` before `RecoverFromLeakedScenes`, and the reset-scene creation and unload-list building moved into the try/catching helper `PrepareResetScene`, so a recovery exception never replaces the original failure; carry-over from the T3 follow-up re-review). |
| `Editor/Builders/SceneBuilder.cs` | T4 (load after `NewScene`). |
| `Editor/Builders/LakeDockBuilder.cs` | T4 (load after `NewScene`), T19 (hook `FishingSceneObjects.AddTo(scene)`), T22 (hook `PresentationSceneObjects.AddTo(scene)`), T28 (owner of the M2 rebuild; keeps both hooks). |
| `Editor/Builders/LakeDockLayout.cs`, `LakeDockMeshes.cs` | T28 only. |
| `Editor/Validation/Validators.cs` | T4 (`MeshReferences`, `MissingScripts`, `BootstrapReferences`). Later tasks add validators in **new** files (`FishingValidators`, `RenderValidators`). |
| `Editor/Validation/ValidationRunner.cs` | T4 (adds its three), T14 (hook `FishingValidators.All()`), T20 (hook `RenderValidators.All()`). |
| `Editor/Builders/ContentBuilder.cs` | T14 (hook `FishingContentBuilder.Build()`), T21 (hook `RenderingSetupBuilder.Build()`). |
| `Editor/Builders/MaterialLibrary.cs` | T20 only (body slot materials move to `FishGame/Toon`; hook `RenderMaterialLibrary.BuildAll()`). |
| `Editor/Builders/RenderMaterialLibrary.cs` (new, T20) | T20 (Toon materials), T21 (water, line, sky, particle, marker materials). |
| `Editor/Builders/PrefabBuilder.cs` | T19 (hook `PlayerFishingRigBuilder.Build(root)` + PlayerFP components in order 6.3), T20 (body meshes from `MeshBuilder`), T21 (camera post-processing on). |
| `Editor/Builders/Fishing/PlayerFishingRigBuilder.cs` (new, T19) | T19 (rig + Fishing components), T23 (feedback + reveal components plus their serialized material references: a block of up to 12 lines in `AddPresentation`; ruling F-Q5), T24 (owner of `AddPresentation`: rod, line, bobber, splash, marker components). Order: T19, T23, T24. |
| `Editor/Builders/Fishing/FishingSceneObjects.cs` (new, T19) | T19 (`FishingHost`), T27 (`FightTelemetry` on the same root). |
| `Editor/Builders/AssetPaths.cs` | T4 (none needed), T14, T20, T21: constants only, appended in their own commented block. T14 declares `ModelsFolder`; T20, T21 and T29 reuse it (ruling D-Q8). T28 adds none (its mesh paths live in `LakeDockLayout.cs`, ruling G-Q10). |
| `Runtime/World/WaterSurface.cs` | T12 (wave seam). |
| `Runtime/Settings/SettingsData.cs` | T10 (three fields + Clamp). |
| `Runtime/Input/PlayerInputAdapter.cs` | T10 (`IDevInput`, aiming context unchanged). |
| `Tests/PlayMode/Fakes/FakeInputService.cs` | T10 (implements `IDevInput`). |
| `Core/Input/InputContextRouter.cs` | T10 (rod rows, `InteractLive`). |
| `Core/Audio/SfxIds.cs`, `SfxRecipes.cs`, `SfxRecipes.M2.cs` (new partial file holding the M2 table and generators; ruling C-Q11) | T11. |
| `Runtime/Audio/SfxGenerator.cs` | T11 (generate `SfxRecipes.All`). |
| `Runtime/Services/` (new files only) | T1 (`EventIsolation`), T11 (`CaptionRequested`). |
| `Data/ContentRegistry.cs` | T13 (fishing categories, singleton refs, `Fishing` database). |
| `App/AppBootstrap.cs` | T14 (one hook after the registry build: `audioBus.ApplyCueTable(registry.SfxCueTable?.ToRecipes())`). |
| `Core/Versioning/BuildInfo.cs` | T17 (`ProtocolVersion = 2`). |
| `Tests/EditMode/BuildInfoTests.cs` | T17 (expectation 1 -> 2; ruling E-Q1). Done in `b9f31ac`. |
| `Fishing/AssemblyInfo.cs` | T17 (the file exists with an `AssemblyMarker` class: keep it and add `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]`; ruling E-Q2). Done in `b9f31ac` (reaches `CatchAnnouncement.FromWire` and the other internal wire and controller seams). The mini-fix batch after T19 adds `[assembly: InternalsVisibleTo("FishGame.Tests.PlayMode")]` (the takeover PlayMode test drives `FishingHost.HandleSelectRod/HandleSelectBait`). |
| `Core/AssemblyInfo.cs` | T7: `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]` (the `FightSim` seams `ForcePhase`, `ForceState`, `DrawCount`, `BurstLateralMult`, `TimeEps`; ruling B-Q2). |
| `Editor/AssemblyInfo.cs` | T14: `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]` (the internal `FishingSeedData` members, `FishingContentBuilder.BuildAt`, `FishingValidators.CheckRegistry/CheckSeedAssets/CheckSpeciesModel/Prefixed`). Recorded here per the T14 review. |
| `Gameplay/AssemblyInfo.cs` | T16 follow-up (`d40534a`): `[assembly: InternalsVisibleTo("FishGame.Tests.PlayMode")]` (PlayMode tests reach `PlayerNetState.HandleToolChanged` and `SessionState.Upsert`). |
| `.gitignore` | T14 follow-up (`03bb4a9`): `/Assets/_Project/Tests/__Temp*` (EditMode scratch content that `FishingContentBuilderTests` builds and deletes; never committed). |
| `Core/Fishing/CreelLedger.cs` (T9 file) | T16 follow-up (`d40534a`, ruling T16-R1): `ClearSlot(byte slot)`. |
| `Core/Fishing/{CatchValidator,FishingRollValidation}.cs` (T9, T6 files) | T17 follow-up (`071dc2d`): `CatchValidator.IsOriginFinite`; `FishingRollValidation.IsSaneCatch` made the shared catch check (used by `FishingRodNetwork` for announcements). |
| `Core/Net/PlayerKey.cs` | Mini-fix batch after T19 (C 16.3): `MaxIdLength` 64 -> 61 to match the connect-payload truncation. |
| `Core/Fishing/{FishingHostLedger,CatchValidator}.cs` | Mini-fix batch after T19 (C 16.3): `ResetSlot`, `OriginWithinSlack`. |
| `Tools/unity.ps1` | T20 only, and only if the `-nographics` shader-validation check fails: a minimal change running `Validation_Shaders_Pass` in a graphics-capable batchmode (ruling F-Q4). |
| `Gameplay/Player/{PlayerNetState,PlayerLocomotion,PlayerCamera,PlayerInteractor,PlayerRigPaths}.cs`, `Gameplay/Session/SessionState.cs`, `Gameplay/Interaction/DockBell.cs` | T16 only (done `37f6723`, fix `d40534a`). The mini-fix batch after T19 edits `SessionState.cs` once more (`SlotTakenOver` event, C 16.3) and `PlayerCreel.cs` (a comment). |
| `UI/UiStrings.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`, `UI/SettingsPresenter.cs` | T25 (fishing HUD, toasts, captions, settings rows), T26 (dev overlay keys and element). Order T25, T26. |
| Generated output (`Scenes/*.unity`, `Prefabs/*.prefab`, `Content/**`, `ProjectSettings/*`) | Any task may regenerate by running `build`. Scene churn without a real change is restored with `git restore Assets/_Project/Scenes/*.unity`. |
| Data assets under `Content/Fishing/**`, `Content/Art/**`, `Content/Audio/**` | Created by T14 (create-if-missing). T15 may retune values **in the `.asset` files** (and must then update the seed in `FishingSeedData` to match, so a fresh build reproduces the tuned values, and the `TunedDeviations` entry below). |
| `Tests/EditMode/FishingContentTests.cs` | T14 creates it, including an empty `TunedDeviations` map; T15 may edit only that map (ruling D-Q2). The controller amends the spec 9.6/9.7/9.12 tables after T15. |

**Integration-fix rule (T19, T28, T30 only).** Like M1 D39: these tasks may fix defects in files owned by earlier M2 tasks, with the public surface in this contract frozen. Each fix is named in the commit message (`fix(<file>): <what>`). A fix that needs a listed signature to change is a contract change: stop and report.

### 2.7 Reading rule for M1 behaviour
Section plans reference M1 contract sections by number instead of restating them. Implementers read the cited M1 section when they touch that code.

### 2.8 Global constraints (every M2 brief carries these)
1. Never `DEVELOPMENT_BUILD` or `#if DEBUG` (UAC0009). Use runtime `Debug.isDebugBuild` (`UnityEngine.Debug` if ambiguous). Dev-only surfaces (selectors, forced bite, F3 overlay, telemetry) check it at runtime.
2. The Write tool may decode `\uXXXX` escapes. After writing a file whose intended text contains `\u`, check no byte > 0x7F unless intended. Docs and code use ASCII punctuation.
3. Cancel convention: any cancel, including an already-cancelled token, faults with `NetworkSessionException(EndReason.UserLeft)` before any state change; never a Canceled task.
4. NetworkObject hashes: builders call `EditorUtility.SetDirty` after `OnValidate`, or `GlobalObjectIdHash` saves as 0. Never destroy and re-add a root `NetworkObject`.
5. Project settings via `SerializedObject` when an API setter doesn't persist.
6. Scene churn: every `BuildAll` renumbers fileIDs. `git restore Assets/_Project/Scenes/*.unity` unless a scene really changed.
7. `core.autocrlf` is true; committed blobs are LF.
8. EditMode tests reach Core, Data, Runtime, Editor, **Gameplay and Fishing** (after T1). They never reach Networking, Presentation, UI or App: put testable rules in Core, or test via PlayMode.
9. PlayMode fails on any unexpected `Debug.LogError`, and output must be warning-free. Register `ISettings`/`IInputService` before a player spawns. File IO in PlayMode (telemetry) must not warn.
10. Late joiners: player objects come from `PlayerSpawner`'s two idempotent entries. Server-side per-player init uses `PlayerNetState.ServerInitialized` (G2, G3). Remote-visible rod state is derivable from NetworkVariables alone; RPC-only state is never needed to draw a rod.
11. Editor lock: locked means `Temp/UnityLockfile` is HELD (an exclusive open fails); a leftover file that is merely present (Unity leaves it after a batch run, for example a compile-error abort) is not a lock. `Tools/unity.ps1` tests this: stop only if it reports LOCKED (exit 2), then ask the user to close the editor. Never kill Unity. Briefs say "stop only if unity.ps1 reports LOCKED".
12. The sandbox refuses `rm -rf` on project paths: use `git rm` / `git restore`, or ask.
13. **Batchmode rendering:** any editor or test code that renders, captures or compiles shaders for capture in batchmode sets `ShaderUtil.allowAsyncCompilation = false` first (restore it after), or it captures placeholder shading.
14. **Builders load assets after `EditorSceneManager.NewScene`** (it unloads unused assets). Load every asset a scene builder uses after the `NewScene` call.
15. No in-scene-placed NetworkObjects in M2 (spec 4.6). Scene objects added in M2 (`FishingHost`, `PaletteGlobals`, `WaterTimeDriver`, `RippleField`, `FightTelemetry`) are plain MonoBehaviours.
16. Core never uses `UnityEngine.Random` or `System.Random`. `FightSim`, rolls and bite plans use their own seeded `Pcg32`.
17. Presentation never writes gameplay state (spec 10.1). UI and Presentation read rod state through `FishingRodNetwork.ViewState`, `FishingRodController` read-only members and events.
18. Global shader arrays are always pushed full length: `_FGPalette` 16, `_Ripples` 8, `_Waves` 4.
19. Every mesh rendered with a `FishGame/Toon` material has vertex colours (MeshBuilder meshes and the fish FBX models do). `RenderValidators` enforces it. Vertex colours are never colour-space converted, so the Toon shader treats them as LINEAR (ruling F-Q3): the fish FBX models are exported with `colors_type='LINEAR'` and `FishFallbackMesh` writes `colorA.linear` / `colorB.linear`; `PaletteGlobals` linearises `_FGPalette` for the same reason. Fish models are ONE submesh drawn with the single `M_Toon_Fish` (ruling G-Q6). Vertex-colour scale (ruling BF-7): `MeshBuilder` stores a HALF-scale grey `0.5 * (1 +/- faceJitter)` (a new Mesh stores colours as UNorm8, which clamps to 0..1); the Toon material float `_JitterEncoded` decides how the vertex colour is read: 1 for every MeshBuilder-built mesh (props, lakebed/shore, rod, bobber, body slots, skin, `FishFallbackMesh`, the silhouette), where the shader uses `2 * vc.rgb` (grey meshes have r == g == b); 0 for the Blender fish models on `M_Toon_Fish`, where the vertex colour is the linear albedo as stored. No colour is clamped or rewritten after `ToMesh`. Only `M_Toon_Fish` has 0, and `RenderValidators` and `ToonMaterialTests` check it. T20 builds one extra asset material `M_Toon_FishFallback` (white `_BaseColor`, no palette, `_JitterEncoded` 1) for MeshBuilder-built fish: `CatchRevealView` uses it for `FishFallbackMesh` meshes and `FishingFeedback` for the snap silhouette. There are no runtime material instances for this; the silhouette tint is baked into its vertex colours.
20. Commits: explicit paths; trailer per 2.1; never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx`.
21. Logs and test results go in the session scratchpad (`$env:FG_OUT`), never in the repo.

---

## 3. Execution order and dependency graph

Execution is serial (one Unity project lock, one git index). Reviews run in parallel with the next implementer where the skeleton's pipelining notes allow it. The full table with goals, tiers and pipelining is in the skeleton; this is the dependency spine.

| Task | Depends on (committed first) |
|---|---|
| T1 Carry: hygiene (asmdef, Pcg32, LogOnce, AudioBus warn, EventIsolation) | - |
| T2 Carry: split `LocalNetworkSession`, `UiFlowController` | T1 |
| T3 Carry: PlayMode harness hardening | T2 |
| T4 Carry: builder NewScene ordering + validators | T1 |
| T5 Core fishing types, `CastMath`, `FishingDatabase`, `RodCue`, wire quantization | T1 |
| T6 Core `BitePlanner`, `CatchMath`, `PlaceholderRoller` | T5 |
| T7 Core `FightSim` (+ junk, motion parameters), `TensionCues` | T5 |
| T8 Core `RodStateMachine` | T5, T6, T7 |
| T9 Core `FishingHostLedger`, `CatchValidator`, `CreelRules`, `CreelLedger` | T5, T6, T8 (`FightReport`) |
| T10 Input rod rows, `GameplayContextArbiter`, dev input, settings fields | T1 |
| T11 Audio M2 (recipes, loops, ducking, cue map, captions message) | T5 |
| T12 `WaveMath`, `WaterSurface` seam, `MeshBuilder`, `RippleMath`, `FaceJitter` | T1 |
| T13 Data defs, `ContentRegistry` fishing categories | T5, T6, T11, T12 |
| T14 `FishingSeedData`, `FishingContentBuilder`, `FishingValidators`, cue-table hook | T13, T4 |
| T15 Tuning bots + retune | T7, T14 |
| T16 Gameplay hooks (NetTool, creel, context arbitration, rig paths, FOV kick, bell sell) | T5, T9, T10 |
| T17 Fishing wire types, `FishingRodNetwork`, `FishingHost`, `CastProbe`, protocol bump | T6, T9, T13, T16 |
| T18 `FishingRodController` (owner) | T8, T10, T16, T17 |
| T19 Integration: player rig + components, scene host, PlayMode cast-to-land | T3, T12, T18 |
| T20 Toon shader, `ShaderValidator`, Toon materials, body meshes, FBX postprocessor | T12, T13, T19 |
| T21 Water/Line/GradientSky shaders, `RenderingSetupBuilder` | T20 |
| T22 World presentation (`PaletteGlobals`, `WaterTimeDriver`, `RippleField`) | T12, T13, T21 |
| T23 Feedback + catch reveal (`FishingFeedback`, `CatchRevealView`, fallback mesh) | T11, T19, T22 |
| T24 Rod presentation (`RodPresenter`, `LineView`, `BobberView`, `SplashFx`, `CastMarkerView`) | T22, T23 |
| T25 HUD (charge ring, captions, gauge, creel, toasts, settings rows) | T2, T18, T24 |
| T26 Dev overlay F3 + debug selectors | T25 |
| T27 Fight telemetry CSV | T18, T19 |
| T28 Lake dock rebuild | T22, T24, T27 |
| T29 Blender species models (controller, Blender MCP) | T20, T28 |
| T30 Acceptance checklist, README, final `check` | all |

Rule: no task references a type owned by a later task. `IPlayerContextSource` (T16) and `IDevInput` (T10) exist to keep that rule.

---

## 4. Assemblies

**One change in M2 (T1):** `Assets/_Project/Tests/EditMode/FishGame.Tests.EditMode.asmdef` adds references `FishGame.Gameplay`, `FishGame.Fishing` and `Unity.Collections` (after `Unity.Netcode.Runtime`). Everything else in M1 contract section 4 is unchanged.

Why: wire-struct round trips (`RodStateData`, `FishingRollNet`, `CreelEntry`) and `ArtPaletteDef`/Def-to-Core mapping tests run in EditMode without a NetworkManager. EditMode tests never instantiate NetworkBehaviours.

No other asmdef needs a change: Fishing already references Core, Data, Runtime, Gameplay, Netcode, Collections; Presentation already references URP Runtime and RP Core; UI references Fishing, Presentation and InputSystem; Editor references everything; PlayMode tests reference Fishing and Gameplay. If a task finds a missing reference, that is a contract bug: stop and report (integration tasks T19, T28, T30 may add one under 2.6's integration-fix rule).

`Shaders/` holds `.shader`/`.hlsl` only and has no asmdef.

---

## 5. Constants (single source; owners in brackets)

### 5.1 Tuning lives in assets
All spec 9.12 parameters, rod, bait, fight-profile, species and junk numbers, creel caps, cue mixing and water waves live in data assets (section 6.2). Their **seed values** (used only to create missing assets and by pure unit tests) live in one Editor class, `FishGame.EditorTools.FishingSeedData` [T14] (spec tables 9.6, 9.7, 9.12 and 9.10 creel cap). Core holds formula constants (below) and no tuning. Core structs have no default tuning values.

### 5.2 `FishGame.Core.FishingConstants` [T5] - `Core/Fishing/FishingConstants.cs`
Formula constants from spec section 9 that are not in the 9.12 tuning table.
```csharp
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
    public const float LandedFloorFactor = 0.8f, LandedFloorSlackSeconds = 0.5f;
    public const float HookedLineSlack = 3.1f;           // lineLength0 = hostDistance - 3.1, floor landDistance. T19-R1 re-derived it from the rod's measured forward reach (max horizontal TipAnchor reach 2.52 m over camera pitch -60..+60 at the owner pose, + 0.5 m margin, rounded up to 0.1). It also feeds WalkBoundedLineLength0 (T9-R1). Unchanged by R67 (hostDistance is root -> landing, the owner line is tip -> landing, both to the same landing)
    public const float CastTipReach = 2.4f;              // R67 (spec 3.2 / 9.3): the rod tip's horizontal forward reach from the player root in the owner rest pose at look pitch 0, measured from TipAnchor (PlayMode FishingSoloTests.RodTip_ForwardReachAtPitchZero_MatchesCastTipReach), rounded to 0.1 m. Rig geometry, not tuning: fixed on owner AND host, pitch never changes it. Re-derive after changing the rod rig. Added ONLY inside CastMath.Landing (subtracted again for the maxLineOut check and the telemetry castDist)
    public const float JunkWeightLowRating = 0.6f;       // effRating 1-2
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
```

### 5.3 Other single-owner constants

| Constant | Value | Owner / file |
|---|---|---|
| `BuildInfo.ProtocolVersion` | `2` (was 1; new RPCs and `PlayerFP` components) | T17 `Core/Versioning/BuildInfo.cs` |
| `ContentRegistry` category names | `"rods"`, `"baits"`, `"fight_profiles"`, `"species"`, `"junk"` (plus M1 `"spots"`) | T13 |
| `ContentRegistry` `dataVersion` | stays `1` (new categories already change `ContentHash`) | T13 |
| Fight log file | `Application.persistentDataPath/fight-log{InstanceProfile.Current.Suffix}.csv` | T27 |
| `FightLogCsv.Header` | `profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity` (`pity` added by R65 / M2-D23) | T27 |
| Dev keys | F3 overlay, F4 next rod, F5 next bait, F6 forced bite (host), F7 twitchy hook (host), F9 ripple at crosshair | T10 (`DevKey`), T26 (behaviour) |
| `SfxIds` M2 ids | section 7.11 | T11 |
| Default M2 loadout | rod `rod_handmade`, bait `bait_worm`, 20 of each bait, bare hook unlimited | T9 (ledger), T14 (ids) |
| Shader names | `FishGame/Toon`, `FishGame/Water`, `FishGame/Line`, `FishGame/GradientSky` | T20, T21 |
| Global shader property names | `_FGPalette` (Vector4[16]), `_Ripples` (Vector4[8]), `_Waves` (Vector4[4]), `_WaterTime` (float), `_ToonShade`, `_ToonAmbient` (Color), `_SkyTop`, `_SkyHorizon`, `_SkyGround` (Color) | `FishGame.Core.ShaderGlobals` [T12] `Core/Render/ShaderGlobals.cs` (string consts named `Palette, Ripples, Waves, WaterTime, ToonShade, ToonAmbient, SkyTop, SkyHorizon, SkyGround`; ruling C-Q3) |
| Water conventions | Wave direction 0 deg = +Z, `dir = (sin, cos)` on (x, z) (same as the `CastMath` yaw). `_Waves[i] = (DirectionDeg, Amplitude, Wavelength, Speed)` for i = 0..2, `_Waves[3] = (0,0,0,0)`, mirroring `WaterSettingsDef.dirAmpLenSpeed`. The palette slot is in mesh UV channel 1 (`SetUVs(1, ...)`, `TEXCOORD1`). Parity rule: the C# wave height is evaluated with the same float-cast time value that is pushed to `_WaterTime` (rulings C-Q4, F-Q1/Q2). | T12 (C#), T22 (push), T20/T21 (HLSL) |

---

## 6. Assets, prefabs, scenes and hierarchies

### 6.1 Folder additions
```
Assets/_Project/
  Core/{Fishing,Diagnostics,World,Render}/        T1,T5-T9,T11,T12,T27
  Data/{Fishing,Art,Audio}/                       T13
  Content/Fishing/{Rods,Baits,FightProfiles,Species,Junk}/  T14 (generated, create-if-missing)
  Content/{Art,Audio,Rendering}/                  T14, T21 (generated)
  Content/Models/                                 T29 (controller; LFS FBX)
  Gameplay/Fishing/                               T16
  Fishing/{Net,Host,Owner,Debug}/                 T17, T18, T27
  Presentation/{World,Rod,Feedback}/              T22-T24
  UI/Fishing/                                     T25, T26
  Shaders/                                        T20, T21
  Editor/Builders/{Fishing,Render}/               T14, T19, T20, T21
```

### 6.2 Asset paths and ids (all under `Assets/_Project/`)

| Asset | Path | Created by | Ids / notes |
|---|---|---|---|
| Rods (4) | `Content/Fishing/Rods/<id>.asset` | T14 | `rod_twig`, `rod_handmade`, `rod_carbon`, `rod_deepsea` (spec 9.7 table) |
| Baits (6) | `Content/Fishing/Baits/<id>.asset` | T14 | `bait_bare_hook`, `bait_worm`, `bait_grub`, `bait_minnow`, `bait_shrimp`, `bait_raw_meat` (spec 9.7 table; bare hook is unlimited) |
| Fight profiles (5) | `Content/Fishing/FightProfiles/<id>.asset` | T14 | `steady`, `darter`, `bulldog`, `thrasher`, `runner` (spec 9.6 table) |
| Species (5) | `Content/Fishing/Species/<id>.asset` | T14 | `bluegill`, `perch`, `carp`, `trout`, `catfish` (spec 9.6; weightSkew 2.2, lengthShapeK 1.0, hookWindow 0) |
| Junk (2) | `Content/Fishing/Junk/<id>.asset` | T14 | `old_boot`, `tin_can`; value 2, 0 kg |
| Fishing tuning | `Content/Fishing/FishingTuning.asset` | T14 | `FishingTuningDef` (spec 9.12 + creel caps 12 / 15 kg) |
| Cue table | `Content/Audio/SfxCueTable.asset` | T14 | `SfxCueTableDef` seeded from `SfxRecipes.All` mixing fields |
| Art palette | `Content/Art/ArtPalette.asset` | T14 | `ArtPaletteDef` (spec 10.2 path); no `slotTints` field (13 M2-D3) |
| Water settings | `Content/Art/WaterSettings.asset` | T14 | `WaterSettingsDef` (3 waves) |
| Fish models | `Content/Models/Fish_Bluegill.fbx`, `Fish_Perch.fbx` (the spike file is replaced), `Fish_Carp.fbx`, `Fish_Trout.fbx`, `Fish_Catfish.fbx` | T29 (controller imports the staged `Art/Export/Fish_*.fbx`, source `Art/Blender/fishgame_m2_species.blend`) | ONE submesh, one material slot (no materials imported), per-face LINEAR vertex colours, 222-262 triangles, exported -Z forward / Y up / apply transform (identity transform, scale 1, nose +Z in Unity), flat normals |
| Shaders | `Shaders/FishGameCommon.hlsl`, `Shaders/Toon.shader` | T20 | |
| Shaders | `Shaders/Water.shader`, `Shaders/Line.shader`, `Shaders/GradientSky.shader` | T21 | |
| Toon materials | `Content/Materials/M_Toon_Base.mat` (palette mode), `M_Toon_Fish.mat` (vertex-colour mode for the Blender fish models, white `_BaseColor`, `_JitterEncoded` 0), `M_Toon_FishFallback.mat` (ruling BF-7: vertex-colour mode for MeshBuilder-built fish and the snap silhouette, white `_BaseColor`, no palette, `_JitterEncoded` 1), `M_Body_Slot0..3.mat` and `M_Skin.mat` re-pointed to `FishGame/Toon` in place (GUIDs kept) | T20 | |
| Other materials | `Content/Materials/M_Water_Surface.mat` (`FishGame/Water`), `M_Line.mat` (`FishGame/Line`), `M_Sky.mat` (`FishGame/GradientSky`), `M_Particle.mat` (URP `Particles/Unlit`), `M_Marker.mat` (URP `Unlit`) | T21 | M1 `M_Water.mat` stays (unused after T28) |
| Volume profiles | `Content/Rendering/VP_WarmSunset.asset`, `VP_CoolNight.asset` | T21 | `ColorAdjustments` + `WhiteBalance` only |
| Generated meshes | `Content/Meshes/Body_*.asset` (T20), `Rod_*.asset`, `Bobber.asset` (T19), `Marker_*.asset` (T24), `LakeDock_*.asset` (T28, M1 lakebed/shore kept) | builders | rebuilt in place (GUIDs kept) |

Every Def asset is **create-if-missing**: `FishingContentBuilder` never overwrites an existing asset's values. Allowed in-place edits to existing assets: adding a missing entry to a `ContentRegistry` list, filling a null `FishSpeciesDef.model` when `Content/Models/Fish_<Pascal>.fbx` exists AND its mesh has a colour channel AND `subMeshCount == 1` (otherwise left null with a log line; rulings D-Q4, G-Q6), and filling null singleton refs on the registry.

### 6.3 `PlayerFP` prefab additions (T19 `PlayerFishingRigBuilder`, called from `PrefabBuilder.BuildPlayer`)

Root components, after the M1 list (M1 contract 6.3), in this order:

| Component | Prefab state | Owner task |
|---|---|---|
| `PlayerCreel` (Gameplay, NetworkBehaviour) | enabled | T16 class, T19 adds |
| `FishingRodNetwork` (Fishing, NetworkBehaviour) | enabled | T17 class, T19 adds |
| `FishingRodController` (Fishing) | **disabled** (enabled by `FishingRodNetwork.OnNetworkSpawn` on the owner) | T18 class, T19 adds |
| `FishingFeedback` (Presentation) | enabled | T23 |
| `CatchRevealView` (Presentation) | enabled | T23 |
| `RodPresenter`, `LineView`, `BobberView`, `SplashFx`, `CastMarkerView` (Presentation) | enabled (`CastMarkerView` renders on the owner only) | T24 |

NetworkBehaviour order on the root is fixed once T19 commits (adding NetworkBehaviours later would change NGO's behaviour indices; no later task adds one).

Children (paths are `PlayerRigPaths` constants, T16):

| Path | Kind | Notes |
|---|---|---|
| `Body/ArmR/Hand` | empty at the right-hand end of the arm (local (0, -0.5, 0) under `ArmR`) | M5 turns it into the `HoldNode` |
| `Body/ArmR/Hand/Rod` | empty, rotated so +Z points forward-up 35 degrees | shown when `NetTool == Rod` |
| `Body/ArmR/Hand/Rod/Seg0..Seg5` | 6 MeshBuilder cylinder segments, `M_Toon_Base` (palette WoodDark) | `RodPresenter` bends them |
| `Body/ArmR/Hand/Rod/TipAnchor` | empty at the end of Seg5 (local (0, 0, 1.8) under `Rod`); STATIC, never moved by presentation | gameplay tip: `FishingRodController` reads it for `RodTipPosition`, the fish point and `TipToBobber` (ruling BF-3) |
| `Body/ArmR/Hand/Rod/TipVisual` | empty, same rest pose as `TipAnchor`; `RodPresenter` bends it with the segments | visual tip: `LineView` draws the line from it; `RodPresenter.Tip` returns it (ruling BF-3) |
| `Body/ArmR/Hand/CatchHold` | empty | `CatchRevealView` holds the fish at CatchHold (world pose read in LateUpdate, order 210, after `PlayerBody` 200; not parented; T23 review) |
| `Line` | `LineRenderer` (world space, `M_Line`), 24 points | `LineView` |
| `Bobber` | MeshFilter + MeshRenderer (`Bobber.asset`, palette Accent + white) | world-positioned every frame by `BobberView` |
| `CastMarker` | ring meshes (valid: solid ring + centre dot; invalid: dashed grey ring + cross), `M_Marker` | `CastMarkerView`, owner only (T24 adds) |

### 6.4 Scene objects added to `Spot_LakeDock`

| Root | Components | Added by |
|---|---|---|
| `Fishing` | `FishingHost` (T17), `FightTelemetry` (T27) | `FishingSceneObjects.AddTo` [T19, T27] |
| `Presentation` | `PaletteGlobals`, `WaterTimeDriver`, `RippleField` (T22) | `PresentationSceneObjects.AddTo` [T22] |
| `WarmVolume`, `CoolVolume` | global `Volume`, weight 0, profiles from 6.2 | T28 |
| `WaterVisual` | replaced: `MeshBuilder.Grid` 96 x 96 cells over 120 m at y 0, `M_Water_Surface`, no collider | T28 |
| `WaterVolume` | unchanged M1 trigger box (cast raycast target and swim volume) | - |
| props | MeshBuilder dock planks, posts, ladders, rocks, trees, reeds on `M_Toon_Base` (palette slots) | T28 |
| `Ladders` | `LadderL`, `LadderR`: trigger BoxCollider + `LadderVolume`, layer Default (R70, M2-D29); `Exits` now holds only `Ramp` (the ramp foot, layer Default, M2-D30) | ladders plan L5, L7 |

`RenderSettings`: skybox `M_Sky`, fog on (ExponentialSquared, density 0.008, never toggled at runtime), ambient mode set once by `RenderingSetupBuilder.ApplySceneLighting()` (T21, called by T28's builder).

### 6.5 Editor menu items (single owners)

| Menu path | Owner | Method |
|---|---|---|
| `FishGame/Validate Shaders` | T20 | `ShaderValidator.RunMenu()` |
| `FishGame/Dev/Audition All Cues` | T11 | `AuditionCuesMenu.Audition()` (Play mode: plays every id through `AudioBus`; edit mode: generates every recipe and checks the 11.7 rules) |
| `FishGame/Dev/Reimport Fish Models` | T20 | `FishModelPostprocessor.ReimportAll()` |

---

## 7. Types by task (public surface)

Notation: everything listed is `public` unless marked. Paths are relative to `Assets/_Project/`. "Authority" comments are mandatory on every networked class and every scene object with host-only behaviour.

### 7.1 T1 - Carry-forward hygiene

```csharp
// Core/Random/Pcg32.cs (edit)
public float Range(float minInclusive, float maxExclusive);
    // ArgumentException unless max > min (NaN fails the check). Returns MapUnitToRange(NextFloat(), min, max).
    // Exactly one NextUInt per call (draw count unchanged, G7).
public static float MapUnitToRange(float unit01, float minInclusive, float maxExclusive);
    // r = min + unit01 * (max - min); if (r >= max) r = the largest float below max (BitConverter.SingleToInt32Bits step);
    // if (r < min) r = min. Pure; tested with unit01 = 0.99999994f.

// Core/Diagnostics/LogOnce.cs - pure dedupe gate (no logging itself)
public sealed class LogOnce
{
    public bool ShouldLog(string key);   // true the first time a key is seen; null key -> ""
    public void Reset();
    public int Count { get; }
}

// Runtime/Services/EventIsolation.cs - per-subscriber invoke
public static class EventIsolation
{
    // Iterates evt.GetInvocationList(); each handler in its own try/catch -> Debug.LogException(e). Null evt is a no-op.
    public static void Invoke(Action evt);
    public static void Invoke<T>(Action<T> evt, T arg);
    public static void Invoke<T1, T2>(Action<T1, T2> evt, T1 a, T2 b);
}
```
- `AudioBus.TryPrepare` warns at most once per `(reason, id)` per AudioBus lifetime through a private `LogOnce` (not-ready, unknown id, missing clip). Behaviour otherwise unchanged.
- `EventIsolation.Invoke` replaces the whole-event invokes at the six G8 sites (Local x2, Ugs x2, Composite x2). The comment at `UgsNetworkSession.cs:78` is corrected.

### 7.2 T2 - Splits (behaviour-preserving; public surface unchanged)
- `Networking/LocalNetworkSession.cs` becomes `public sealed partial class LocalNetworkSession` in three files: `LocalNetworkSession.cs` (public API, state and end, tokens; drift responsibilities 1, 4, 6; about 270 lines), `LocalNetworkSession.Start.cs` (the attempt lifecycle `AttemptAsync`, `PostCancel`, `CancelAttempt`, `WatchTimeoutAsync` plus the NGO start: setup, port loop, `StartNgo`, `ShutdownNgoAsync`, approver helper; responsibilities 2 and 3; about 230 lines; ruling A-Q1), `LocalNetworkSession.NgoEvents.cs` (subscribe/unsubscribe and the three NGO callbacks; responsibility 5). Same pattern as `UgsNetworkSession` (M1 A9). Each file < 300 lines.
- `UI/UiFlowController.cs` keeps the public members (M1 contract 7.13) and the screen router, `Initialize`, `Show`, `Update`/back handling, scale and solo pause. New internal classes: `UI/UiSessionActions.cs` (`internal sealed class UiSessionActions`: start attempt, host, join, cancel, leave, toggle joins, attempt CTS, toast publishing) and `UI/UiFocusRouter.cs` (`internal sealed class UiFocusRouter`: menu focus, gamepad focus, `SetMenuActions`, `FocusScreen`). `UiFlowController.cs` < 400 lines (about 350-365; no fifth file; ruling A-Q2).
- Internal hook points reserved for T25/T26: `UiFlowController` gets `internal void AttachHudExtension(IHudExtension ext)` (internal interface `IHudExtension { void Bind(VisualElement root, ServiceRegistry app); void Tick(float dt); void Unbind(); }` in `UI/IHudExtension.cs`), called by T25/T26 hooks; the HUD screen ticks attached extensions.

### 7.3 T3 - PlayMode harness (asm FishGame.Tests.PlayMode)
```csharp
public static class PlayModeHarness  // M1 members kept; changes:
{
    public static IEnumerator WaitFrames(int frames, float minRealtimeSeconds); // both conditions, replaces fixed negative waits
    public static IEnumerator WaitGameSeconds(float seconds, float realtimeTimeoutSeconds, string failMessage);
        // accumulates Time.deltaTime with a realtime guard (fails instead of spinning when timeScale is 0)
    public static IEnumerator Await(Task task, float timeoutSeconds, CancellationTokenSource cancelOnTimeout = null);
        // on timeout: cancelOnTimeout?.Cancel(), then Assert.Fail
    public static IEnumerator StartSoloAndWaitForPlayer();   // now passes a CTS to StartLocalAsync and to Await
    public static IEnumerator TearDownApp();
        // M1 behaviour, then asserts ServiceRoots.App == null && ServiceRoots.Game == null and resets
        // AppBootstrap.SuppressDevAutoStart = false (bounded wait for Game to clear on scene unload)
    public static IEnumerator LoadBootstrapAndWaitForMenu(); // leak guard also asserts ServiceRoots.Game == null
}
```
`SoloSessionTests.cs:31` (fixed 0.5 s wait) uses `WaitFrames`; `PlayerMovementTests` game-time waits use `WaitGameSeconds`.

### 7.4 T4 - Builders and validators (asm FishGame.Editor)
- `SceneBuilder.BuildBootstrap` and `LakeDockBuilder.Build` load every asset **after** `EditorSceneManager.NewScene` (2.8 #14).
- `Validators` (edit) adds, each returning `List<string>`:
  - `MeshReferences()`: every `MeshFilter.sharedMesh` and `MeshCollider.sharedMesh` in build scenes and in prefabs under `Assets/_Project/Prefabs` is non-null.
  - `MissingScripts()`: no missing `MonoBehaviour` in build scenes or prefabs (`GameObjectUtility.GetMonoBehavioursWithMissingScriptCount`).
  - `BootstrapReferences()`: the Bootstrap `AppBootstrap` has non-null `inputActions` and `contentRegistry`.
- The new validators open build scenes additively, read-only, closed without saving (already-open scenes are left alone). If opening `Bootstrap.unity` logs errors in EditMode, a YAML text scan of `m_Mesh: {fileID: 0}` and `m_Script` GUIDs is an allowed alternative (ruling A-Q5).
- `ValidationRunner.Run` runs them after the M1 seven.

### 7.5 T5 - Core fishing model (asm FishGame.Core, folder `Core/Fishing/`, one type per file)

```csharp
public enum RodState : byte { Idle = 0, Readying = 1, Ready = 2, Charging = 3, Casting = 4, Waiting = 5,
                              Bite = 6, Hooked = 7, Landed = 8, Snapped = 9, Escaped = 10 }
public enum FightPhase : byte { Calm = 0, Burst = 1 }
public enum FightOutcome : byte { None = 0, Landed = 1, Snapped = 2, Escaped = 3, Cancelled = 4 }
public enum EscapeReason : byte { None = 0, Missed = 1, Pinned = 2, ThrewHook = 3, Dropped = 4, Timeout = 5, Invalid = 6, Disconnected = 7 }
public enum CatchQuality : byte { Normal = 0, Good = 1, Trophy = 2 }
public enum FishRarity : byte { Common = 0, Uncommon = 1, Rare = 2 }
public enum BodyShape : byte { Round = 0, Long = 1, Flat = 2, Eel = 3, Squid = 4 }
public enum CastRejectReason : byte { None = 0, ActiveCast = 1, StaleCastId = 2, BadCharge = 3, OriginTooFar = 4,
                                      NoBait = 5, NotWater = 6, TooShallow = 7, NotAllowedNow = 8, UnknownPlayer = 9 }
public enum RodEventType : byte { Nibble = 0, Bite = 1, HookSet = 2, Snapped = 3, Escaped = 4 }   // RodEventRpc payload
public enum AnnounceKind : byte { None = 0, Landed = 1, Lost = 2, ReleasedCreelFull = 3,
                                  PityCast = 4 }   // R65: toast-only, sent when a bad-luck-nudge cast is accepted; Catch is default, no reveal
public enum ToolKind : byte { None = 0, Rod = 1, Bucket = 2 }      // file Core/Player/ToolKind.cs
public enum LineGaugeMode : byte { Off = 0, Arc = 1, Bar = 2 }
public enum RodForceReason : byte { Teleport = 0, Respawn = 1, Stow = 2, Swim = 3, Disconnect = 4 }

// Every feedback moment of spec 9.11. Owner raises from RodStateMachine effects; remotes from RodEventRpc and NetRodState transitions.
public enum RodCue : byte { None = 0, Equip = 1, Stow = 2, Ready = 3, ChargeStart = 4, CastRelease = 5, InvalidRelease = 6,
                            Landing = 7, Recall = 8, Nibble = 9, Bite = 10, HookSet = 11, SideRun = 12, Dive = 13, Calm = 14,
                            Exhausted = 15, Snapped = 16, Escaped = 17, LineLanded = 18 }

public struct RodStats            // mirrors RodDef
{
    public byte Tier; public float CastMaxDist, ReelSpeed, LineStrength, AngleWindow, SteerRate, RelaxMult;
    public float MaxLineOut(float spoolExtra) => CastMaxDist + spoolExtra;
}
public struct BaitStats           // mirrors BaitDef
{
    public int Rating, StackMax, MaxRating, MaxNibbles; public float BiteSpeedMod, JunkMult; public bool Unlimited;
    public int EffRating(int loadedCount);   // min(MaxRating, Rating * max(1, loadedCount)); M2 passes 1
}
public struct FishFightProfile    // mirrors FightProfileDef (spec 9.6 columns + motion parameters, 13 M2-D15)
{
    public float PullStrength, LateralSpeed, RunSpeed, StaminaDrain, CalmMin, CalmMax, BurstMin, BurstMax, FlipRate, DiveChance;
    // Motion parameters (all 0 = the base spec 9.5 model, except FlipMinGap which must be > 0):
    public float BurstRamp;        // s; burst force ramps 0 -> 1 over this time from burst start (Steady, Runner)
    public float CalmDrift;        // m/s; line creeps out during Calm (Bulldog), x FishStamina
    public float CalmDriftRamp;    // s; CalmDrift ramps 0 -> 1 from Calm start (0 = immediate)
    public float FlipMinGap;       // s; minimum time between flips (spec default 0.5; Darter 0.3)
    public float LateralJitter;    // 0..0.5; per-burst lateralSpeed multiplier drawn from [1 - j, 1 + j] (Thrasher)
}
public struct FishSpeciesData     // mirrors FishSpeciesDef (gameplay fields only)
{
    public ushort FightProfileIndex; public int WeightMinG, WeightMaxG, BaseValue, MinBaitRating;
    public float WeightSkew, LengthShapeK, BiteWaitMod, HookWindow, MinDepth; public bool IsSpecial; public FishRarity Rarity;
}
public struct JunkData { public int ValueCoins; }

// Every spec 9.12 parameter plus creel caps. No defaults here (5.1).
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

// Index spaces follow ContentRegistry ordinal-sorted ids (rods, baits, fight_profiles, species, junk).
public sealed class FishingDatabase
{
    public FishingDatabase(RodStats[] rods, BaitStats[] baits, FishFightProfile[] profiles, FishSpeciesData[] species,
                           JunkData[] junk, in FishingTuning tuning);  // ArgumentException: empty rods/baits/profiles, bad profile index, no Unlimited bait
    public IReadOnlyList<RodStats> Rods { get; } public IReadOnlyList<BaitStats> Baits { get; }
    public IReadOnlyList<FishFightProfile> Profiles { get; } public IReadOnlyList<FishSpeciesData> Species { get; }
    public IReadOnlyList<JunkData> Junk { get; }
    public ref readonly FishingTuning Tuning { get; }
    public int UnlimitedBaitIndex { get; }   // first bait with Unlimited (bare hook)
}

// Result of the two cast raycasts (spec 9.3), filled by the Fishing adapter CastProbe.
public struct WaterProbe { public bool FirstHitIsWater; public float WaterY; public bool HasBed; public float Depth; }

public static class CastMath
{
    public static float ChargeNorm(float heldSeconds, float castChargeTime, bool pingPong);
        // u = held / chargeTime; pingPong ? 1 - |(u mod 2) - 1| : clamp01(u). held <= 0, NaN held, chargeTime <= 0 or NaN -> 0;
        // infinite u -> 0 (ping-pong) or 1 (T5 review fix `339742f`).
    public static float Distance(float chargeNorm, float castMinDist, float castMaxDist);   // lerp, chargeNorm clamped 0..1; NaN chargeNorm counts as 0
    public static float FlightTime(float distance);                                         // 0.45 + 0.012 * distance
    public static float PitchApexMult(float pitchDeg);                                      // 0.7..1.3, linear over -30..45 deg, clamped
    public static float Apex(float distance, float pitchApexMult);                          // max(0.6, 0.12 * distance) * mult
    public static void Landing(float originX, float originZ, float yawDeg, float distance, out float x, out float z);
        // R67: distance is measured FROM THE ROD TIP; the fixed FishingConstants.CastTipReach is added INSIDE, the one place it is added (owner marker, cast request and host TryComputeLanding stay bit-identical).
        // flat forward from yaw only (x += sin(yaw) * d, z += cos(yaw) * d) with d = CastTipReach + distance
    public static void ArcPoint(float t01, float startX, float startY, float startZ, float endX, float endY, float endZ,
                                float apex, out float x, out float y, out float z);         // parabola, apex above the higher end
    public static bool IsValidLanding(in WaterProbe probe, float minLandDepth);             // FirstHitIsWater && HasBed && Depth >= minLandDepth
    public static CastRejectReason ProbeReason(in WaterProbe probe, float minLandDepth);    // None | NotWater | TooShallow; written as !(Depth >= minLandDepth), so a NaN depth is TooShallow
}

// Fish point during a fight (spec 9.1 glossary): angle off the cast line = lateral * lateralMaxDeg.
public static class FishPointMath
{
    public static void FishPoint(float tipX, float tipZ, float castYawDeg, float lateral, float lateralMaxDeg,
                                 float lineLength, out float x, out float z);
}

// NetRodState quantization and flags (spec 9.9 RodStateData).
public static class RodWireQuant
{
    public const byte FlagBurst = 1, FlagReel = 2, FlagDive = 4, FlagExhausted = 8, FlagRecall = 16;
    public const byte FlagGrace = 32;                     // owner sets while Hooked && FightSim SnapTimer > 0 (tension 1.0 last tick); presentation only, host ignores (T23-fix d4483c3)
    public const byte AnnounceFlagClean = 1;              // CatchAnnouncedRpc flags
    public static sbyte QuantizeLateral(float lateral);    // round(clamp(-1,1) * 127), AwayFromZero
    public static float DequantizeLateral(sbyte q);
    public static byte QuantizeTension(float tension);     // round(clamp01 * 255)
    public static float DequantizeTension(byte q);
}
```

### 7.6 T6 - Bite, catch and placeholder roll (asm FishGame.Core, `Core/Fishing/`)

```csharp
public struct CatchResult        // spec 9.8
{
    public bool IsJunk; public ushort ItemIndex; public int WeightG; public ushort LengthMm; public CatchQuality Quality; public int ValueCoins;
}
public struct FishingRoll        // spec 9.8 plus HookWindow (13 M2-D6)
{
    public CatchResult Catch; public float WeightT;
    public float BiteWait; public byte NibbleCount; public float Nibble0, Nibble1, Nibble2;
    public uint FightSeed;       // non-zero
    public byte SizeClass;       // 0..2 from WeightT thirds
    public float HookWindow;     // 0 = auto-hook
    public bool Pity;            // R65: set by the host ledger at accept (never by PlaceholderRoller; no extra RNG draw); owner applies the Pity effects
}
public struct BitePlan { public float BiteWait; public byte NibbleCount; public float Nibble0, Nibble1, Nibble2; }

public static class BitePlanner
{
    public static float DepthMult(float depth);       // lerp(1.0, 0.8, clamp01(depth / 6))
    public static float WaitMultiplier(float baitBiteSpeedMod, float fishBiteWaitMod, float depth,
                                       float eventMult = 1f, float skillMult = 1f, bool inHotspot = false);
    public static BitePlan Plan(in FishingTuning tuning, float waitMultiplier, int maxNibbles, ref Pcg32 rng);
        // waitBase = Range(BiteWaitMin, BiteWaitMax); biteWait = max(MinBiteWait, waitBase * mult);
        // n = Range(0, maxNibbles + 1) (forced 0 when biteWait < 1.4); offsets biteWait*(k+1)/(n+1) +- 0.3,
        // clamped [0.5, biteWait - 0.6], then sorted ascending. Fixed draw order: waitBase, n, then one jitter per nibble.
}

public static class CatchMath    // shared with M3
{
    public static float Skew(float weightSkew, float luck);           // clamp(weightSkew - 0.4 * luck, 1.2, 3.0)
    public static float SkewedT(float u, float skew);                 // pow(u, skew)
    public static int WeightG(int minG, int maxG, float t);           // round(min + (max - min) * t), AwayFromZero
    public static CatchQuality Quality(float t);                      // >= 0.9 Trophy, >= 0.6 Good, else Normal
    public static float QualityMult(CatchQuality q);
    public static int Value(int baseValue, float t, CatchQuality q);  // max(1, round(base * (0.6 + 0.8 t) * qMult)), AwayFromZero
    public static ushort LengthMm(int weightG, float lengthShapeK);   // round(400 * cbrt(weightG / 1000.0) * k)
    public static byte SizeClass(float t);                            // t < 1/3 -> 0, < 2/3 -> 1, else 2
    public static CatchResult RollFish(in FishSpeciesData s, ushort speciesIndex, float luck, ref Pcg32 rng, out float t);
    public static CatchResult Junk(ushort junkIndex, in JunkData junk); // weight 0, length 0, Normal, value = junk value
    public static CatchQuality UpgradeOneTier(CatchQuality q);        // Normal -> Good -> Trophy -> Trophy
    public static CatchResult WithCleanBonus(in CatchResult fish, in FishSpeciesData s, float t);
        // quality +1 tier (capped at Trophy), ValueCoins recomputed with Value(base, t, upgraded); weight and length unchanged; junk unchanged
}

public struct RollRequest
{
    public int BaitIndex; public int LoadedCount; public float Depth; public float Luck;
    public float ForcedBiteWait;    // > 0: BiteWait = this, no floor, no nibbles (debug)
    public int ForcedItemIndex;     // -1 off; >= 0 forces that species index (debug)
    public bool ForceJunk;          // debug
    public bool Twitchy;            // debug: HookWindow = 1.0 for a fish
}
public static class PlaceholderRoller   // M2 only; replaced by M3 FishingResolver
{
    public static float JunkWeight(int effRating);                     // 0.6 / 0.2 / 0.05, before bait.JunkMult
    public static bool IsEligible(in FishSpeciesData s, int effRating, float depth);  // effRating >= MinBaitRating && depth >= MinDepth
    public static FishingRoll Roll(FishingDatabase db, in RollRequest req, ref Pcg32 rng);
        // Order: item draw (species weight 1 each eligible, junk JunkWeight * JunkMult; nothing eligible -> junk; junk item uniform),
        // then CatchMath, then BitePlanner (species biteWaitMod; junk uses 1.0), then FightSeed = rng.NextUInt() (0 -> 1).
        // A ForcedItemIndex >= the species count THROWS (T6/T7 review; tested): the debug force must name a real species.
}

// Client-side sanity check of a received roll (spec 9.9). Pure; used by FishingRollNet.TryToCore.
public static class FishingRollValidation
{
    public static bool IsSane(in FishingRoll roll, FishingDatabase db);
        // index in range for the junk/species flag; species weight within [min, max]; junk weight 0; quality 0..2;
        // BiteWait finite and >= 0; NibbleCount <= 3; FightSeed != 0; SizeClass <= 2; HookWindow finite and >= 0;
        // WeightT finite and in [0, 1]; Nibble0..2 finite (T6/T7 review)
    public static bool IsSaneCatch(in CatchResult c, FishingDatabase db);
        // The catch part of IsSane (public since the T17 follow-up `071dc2d`): quality 0..2; item inside the database; species weight within
        // [min, max]; junk weight 0; a null database returns false. `FishingRodNetwork.IsPlausible(in CatchAnnouncement, FishingDatabase)` (internal static)
        // vets crew announcements with it for kinds 1..3 (kind 4 PityCast carries a default catch and is not checked; when no database exists the
        // announcement is let through, because a client without content cannot judge it).
}
```

### 7.7 T7 - Fight (asm FishGame.Core, `Core/Fishing/`)

> **R72 (2026-10-12):** this block is superseded by plan `2026-10-12-m2-rework-fight.md` (F2 code, F5 view math). See C 16.1 row R72.

```csharp
public struct FightState   // spec 9.5
{
    public float LineLength, Lateral, Tension, FishStamina;
    public FightPhase Phase; public float PhaseTimer; public sbyte BurstDir;   // +-1 side run, 0 dive
    public float SnapTimer, PinnedTimer, SlackTimer, Elapsed;
    public int NearSnapRecoveries; public float MaxTension;
    public FightOutcome Outcome; public EscapeReason Reason;
}
public sealed class FightSim
{
    public const float Step = FishingConstants.FightStep;
    public FightSim(in FishFightProfile fish, in RodStats rod, in FishingTuning tuning, float weightT, uint seed,
                    float startLineLength, bool assist, bool pity = false);
        // pity (R65): strain scale x PityStrainMult and snap grace x PityGraceMult; deterministic, no RNG draw
    public static FightSim ForJunk(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength);
        // spec 9.4 junk: no phases, tension 0, line -= JunkDragSpeed * dt while reeling, slack escape applies, lands at LandDistance while reeling
    public bool IsJunk { get; }
    public FightOutcome Tick(in FightInput input);      // one fixed step; returns Outcome (None while running); no-op once ended
    public ref readonly FightState State { get; }
    public float WeightFactor { get; }                  // lerp(0.85, 1.25, weightT)
    public float EffectivePull { get; }                 // P of the last tick
    public bool Spooled { get; }                        // last tick
    public bool Exhausted { get; }                      // stamina <= 0.05 reached
    internal void ForcePhase(FightPhase phase, sbyte burstDir, float duration);   // test seam (InternalsVisibleTo FishGame.Tests.EditMode)
    internal void ForceState(in FightState state);                                 // test seam
    internal int DrawCount { get; }  internal float BurstLateralMult { get; }  internal const float TimeEps = 1e-4f;   // test hooks (ruling B-Q2)
}
```
`Core/AssemblyInfo.cs` gains `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]` (T7). Rules: spec 9.5 verbatim (per-tick formulas, phase schedule, timers and endings); `FishingConstants` for the non-tuning numbers. Draw order per phase change is fixed and documented in the T7 plan so seeds stay stable. Rulings applied (section 15): same-tick endings resolve Landed > Snapped > Pinned > ThrewHook (B-Q6); a flip is allowed on the first burst tick and the minimum gap applies only between flips (B-Q7); a new side run is away from the current side with probability 0.75 and otherwise a random side, so 87.5% away overall, with ONE draw (the literal spec 9.5 reading, B-Q4); an exhausted fish stays Calm with no drift and no draws (B-Q15); the reused M1 type `Core/Input/FightInput.cs` is the input (B-Q3).

**Motion parameters (13 M2-D15; user-approved 2026-10-11).** With every motion parameter at 0 (and `FlipMinGap` 0.5) the sim is exactly spec 9.5. The fight stays Rust-style (steer against side runs, reel when calm, tension and snap); these only shape the motion:
- `ramp = BurstRamp > 0 ? clamp01(burstElapsed / BurstRamp) : 1`, where `burstElapsed` resets at each burst start. During Burst every burst term of 9.5 uses `Pb = P * ramp` in place of `P` (lateral push, run line-out, reel penalty, `reelStrainBurst` strain, spool strain). Calm terms are unchanged.
- During Calm: `lineLength += CalmDrift * driftRamp * FishStamina * dt`, `driftRamp = CalmDriftRamp > 0 ? clamp01(calmElapsed / CalmDriftRamp) : 1` (no drift once Exhausted). The line then still clamps to `maxLineOut`; drift never sets `spooled`.
- Flips use `FlipMinGap` instead of the 0.5 s constant. Darter's micro-bursts are its calm/burst ranges plus `FlipRate` 0.8-1.2 /s.
- At each burst start, when `LateralJitter > 0`, one extra draw `burstLateralMult = Range(1 - j, 1 + j)` (after the existing burst-start draws); the side-run lateral push uses `LateralSpeed * burstLateralMult`. No draw when `j == 0`, so profiles without jitter keep their seed streams.
- **Test data:** `FightSimTests` (spec 9.5 hand-worked checks) use the spec 9.6 columns with every motion field 0 and `FlipMinGap` 0.5 (the Runner/Bulldog/Thrasher hand-check copies also set `FlipRate` 0 and use `weightT` 0.375, ruling B-Q5); `FightMotionTests` and the T15 bots use the seeded motions (bots via the assets). `FishingData_SeedsMatchSpecTables` compares seeds to the spec tables except the motion fields and Darter's calm/burst/flipRate overrides listed here.
- Everything stays deterministic for a given seed and input sequence. Seeds (5.1, `FishingSeedData`): Steady `BurstRamp` 0.6; Darter calm 0.3-0.8 s, burst 0.4-0.9 s, `FlipRate` 1.0, `FlipMinGap` 0.3; Bulldog `CalmDrift` 0.12 m/s, `CalmDriftRamp` 2.5 s; Thrasher `LateralJitter` 0.3 with calm kept at 1.0-2.5 s (`CalmMin >= 1.0` is validated, T14); Runner `BurstRamp` 0.8; every other motion field 0 and `FlipMinGap` 0.5. The bots (T15) are the judge; failing targets are retuned in data.

```csharp
public enum TensionBand : byte { Rest = 0, Bend = 1, Creak = 2, Warn = 3, Grace = 4 }  // <0.3, 0.3-0.6, 0.6-0.8, 0.8-1.0, >=1.0 in grace
public struct TensionCueState
{
    public TensionBand Band; public float RodBend01, ReelClickRate, CreakVolume01, CreakPitch, LineWarm01;
    public bool LineWarn, LineDash, Shake, CrackNow; public float RumbleLow, RumbleHigh, TraumaPerSecond;
}
public static class TensionCues   // spec 9.11 table; pure
{
    public static TensionBand BandOf(float tension, bool inGrace);
    public static TensionCueState Evaluate(float tension, bool inGrace, float reelSpeed, float cameraMotion01);
        // ReelClickRate = reelSpeed * 6 /s; CreakPitch 1..2.1 over 0.6..1.0; Warn: trauma +0.1/s, rumble ramp; all motion terms x cameraMotion01
}
```

### 7.8 T8 - Rod state machine (asm FishGame.Core, `Core/Fishing/`; owner-run)

```csharp
public struct RodFrame            // one owner frame of inputs and guards
{
    public bool PrimaryPressed, PrimaryReleased, PrimaryHeld, SecondaryPressed, SecondaryReleased, SecondaryHeld;
    public bool MarkerValid;      // owner CastProbe at the current marker (Ready/Charging)
    public float PlayerToBobber;  // horizontal player-root to bobber (Waiting auto reel-in; the machine subtracts FishingConstants.CastTipReach so maxLineOut is measured from the rod tip, R67)
    public float TipToBobber;     // horizontal rod tip to bobber (FightSim start line at hook)
    public float HookDisplacement;// horizontal distance from the root position at hook time (Hooked leash)
    public bool InWater;          // player is in water (Hooked -> Dropped)
    public bool Paused;           // solo pause: nothing advances
}
public enum RodEffectKind : byte { Cue = 0, SendCastRequest = 1, SendCancel = 2, SendHooked = 3, SendResult = 4,
                                   SendRodEvent = 5, StartFight = 6, CommitTimedOut = 7 }
public struct FightReport        // owner fight stats carried by ResultRpc (clean-catch claim, telemetry)
{
    public float MaxTension; public float FightSeconds; public int NearSnapRecoveries;   // from FightState (Elapsed for FightSeconds)
}
public struct RodEffect
{
    public RodEffectKind Kind; public RodCue Cue; public uint CastId; public float ChargeNorm;
    public FightOutcome Outcome; public EscapeReason Reason; public RodEventType EventType; public byte SizeClass;
    public FightReport Report;    // SendResult only (zeroes when no fight ran)
}
public sealed class RodStateMachine
{
    public RodStateMachine(in FishingTuning tuning);
    public RodState State { get; }
    public float StateTime { get; }             // seconds in the current state (unpaused)
    public uint CastId { get; }                 // last issued cast id (starts 0; each cast request uses ++CastId)
    public float ChargeNorm { get; }            // live in Charging; frozen at release
    public float CastDistance { get; }          // at release; R67: the cast distance FROM THE ROD TIP (flight time uses it as is; the landing is CastTipReach further from the root)
    public bool MarkerOn { get; }               // Ready or Charging
    public bool HasResult { get; }              // valid CastResult held for CastId
    public FishingRoll Roll { get; }            // owner-only roll (valid when HasResult)
    public float BiteTimer { get; } public float BiteWait { get; }
    public FightSim Fight { get; }              // non-null from Hooked until the next Idle
    public FightOutcome Outcome { get; } public EscapeReason Reason { get; }
    public bool Recalling { get; }              // Escaped state used as the recall beat of an unsolicited cancel (13 M2-D5)
    public bool Assist { get; set; }            // captured at release
    public int IgnoredEventCount { get; }       // public read-only (ruling E-Q5): every ignored call; the owner controller logs growth in debug builds
    public void SetRod(in RodStats rod);        // applied only in Idle (else ignored, logged once)
    public void Tick(float dt, in RodFrame frame);              // all non-fight transitions of spec 9.2
    public void TickFight(in FightInput input, in RodFrame frame); // one 0.02 s step while Hooked; applies Landed/Snapped/Escaped rows
    public void OnCastResult(uint castId, bool valid, in FishingRoll roll, in FishFightProfile profile);
    public void OnCommit(uint castId, bool accepted, FightOutcome finalOutcome, EscapeReason finalReason);
    public void ForceEnd(RodForceReason reason); // "any line-out state" row
    public int DrainEffects(List<RodEffect> into); // appends then clears the internal queue; returns count
}
```
Contract rules (the T8 plan carries the full transition code, since it is a tricky state machine):
- Every row of spec 9.2 maps to exactly one code path and to the tests in 10.2 (row map in 10.2).
- Edge triggers per spec 9.2 rules. A Primary held through a catch never starts a cast: Charging needs a `PrimaryPressed` seen while `State == Ready`.
- Any `castId` that differs from `CastId` is ignored. Illegal events (wrong state) are ignored and counted in the public `IgnoredEventCount` (no Unity logging in Core). The controller (T18) compares the counter after each machine call and, when it grew and `Debug.isDebugBuild`, `Debug.Log`s once per state (not a warning). The reply (commit or late result) to the owner's own cancel is consumed silently and never counted, so ordinary cancels stay warning-free.
- `Casting -> Waiting` when flight time (`CastMath.FlightTime(CastDistance)`) has elapsed **and** `HasResult`; the bite timer starts at `max(landing, resultReceived)`.
- Unsolicited commit in Casting/Waiting/Bite/Hooked: no `SendResult`; `Cancelled` -> `Escaped` with `Recalling = true` and cue `Recall`; otherwise `Escaped(finalReason)` with cue `Escaped`; 1.5 s beat then Idle. **T8-R1:** the beat applies in Casting too, and a commit while Casting is no longer an illegal event (spec 9.13 "LastCast" intent wins over the 9.2 row list; the spec carries a 9.2 note). A duplicate commit in a terminal state is ignored and counted; an unsolicited `Cancelled` commit emits no `Escaped` event; `finalReason None` on a non-cancel commit is treated as `Invalid`.
- **T8-R2:** ANY invalid cast result (`OnCastResult(valid:false)`, Casting or Waiting) sets `_cancelledCastId = castId`, so the host's `Cancelled` commit for that cast is consumed silently (no `IgnoredEventCount`, no beat). `_cancelledCastId` is never cleared by `ResetCast`. Late replies to the owner's own cancel are consumed the same way.
- While `RodFrame.Paused`: `Tick` and `TickFight` do not advance anything (a paused `TickFight` with no fight is not an illegal event); `OnCastResult` and `OnCommit` are still accepted (they carry no frame) but the flight, bite and beat clocks do not move until unpaused. `ForceEnd` in a terminal state or Idle is a no-op. The sequence for a cast is "cue first, then `SendCastRequest`" (cue effect, then the send effect; decision 9 of the T8 plan).
- Telemetry note (T18, done as `FightRecorder`): `ResetCast` wipes `Roll`, `Outcome` and `Fight` on entering Idle and a recall goes Waiting/Bite -> Idle in one call, so the owner must capture the roll on entering Waiting and the outcome on entering a terminal state; a cast that reaches Idle with no captured terminal state is recorded as `Cancelled`. Commit in Landed/Snapped/Escaped: sets the commit flag; Idle once the 1.5 s beat is done and (commit or `CommitTimeout` since entering the terminal state; the latter emits `CommitTimedOut`).
- `ForceEnd`: Readying/Ready/Charging -> Idle; Casting/Waiting/Bite -> Idle + `SendCancel` + cue `Recall`; Hooked -> Escaped(Dropped) + `SendResult(Escaped, Dropped)`; Idle and terminal states: no-op.
- Effects for every transition: `SendRodEvent` for Nibble, Bite, HookSet, Snapped (with `SizeClass`; only Snapped carries the size class, ruling B-Q13), Escaped. `Cue` for every `RodCue` the state change implies; the Snapped `Cue` effect carries `SizeClass = Roll.SizeClass` too (ruling BF-1: the owner's local snap cue reads it), every other cue effect carries 0. A host-rejected or insane cast result never makes the machine emit `SendCancel`; the single `CancelRpc` after an insane roll comes from `FishingRodNetwork` (ruling BF-5).
- Bad-luck nudge (R65): on Bite, a real hook window (`Roll.HookWindow > 0`) is extended by `PityHookWindowBonus` when `Roll.Pity`; an auto-hook (window 0) is unchanged. The fight is built with `new FightSim(..., Roll.Pity)`.

### 7.9 T9 - Host ledger (asm FishGame.Core, `Core/Fishing/`)

```csharp
public enum CastPhase : byte { Waiting = 0, Hooked = 1 }
public struct ActiveCast   // spec 9.9 plus host bookkeeping
{
    public uint CastId; public FishingRoll Roll; public CastPhase Phase; public double TAccepted, THooked;
    public float LineLength0, TFlight; public bool Assist; public ushort RodIndex; public byte BaitIndex;
    public float LandingX, LandingZ;
    public float CastDistance;     // R67: cast distance FROM THE ROD TIP, host-derived at accept (origin -> landing is CastTipReach + this; anti-teleport bound T9-R1, TFlight)
}
public struct CastRequestData { public uint CastId; public float OriginX, OriginY, OriginZ, YawDeg, ChargeNorm; public byte BaitIndex; public bool Assist; }
public struct HostCastView    // host-side facts the Fishing adapter supplies
{
    public float PlayerX, PlayerY, PlayerZ;   // host's interpolated view of the sender's root
    public WaterProbe Probe;                  // CastProbe at the landing returned by TryComputeLanding
    public bool CastingAllowed;               // M2: true in gameplay scenes (M4: run phase Day)
}
public struct CastVerdict { public bool Valid; public CastRejectReason Reason; public FishingRoll Roll; public float LandingX, LandingZ; }
public struct CommitDecision
{
    public ulong ClientId; public byte Slot; public uint CastId;
    public bool Accepted;      // false ONLY for orphan repeats (a stored resolution replayed); every FIRST resolution has Accepted = true (ruling E-Q6). Consumers act on Announce / AddToCreel / Outcome.
    public FightOutcome Outcome; public EscapeReason Reason;
    public bool Refunded; public AnnounceKind Announce; public FishingRoll Roll; public bool AddToCreel;   // Roll = the cast's roll on EVERY non-orphan decision, incl. Cancel and Tick timeouts; default on orphans (BF-4)
    public bool CleanBonus;    // Roll.Catch already carries the upgraded quality and value
}
public struct PlayerLoadout { public ushort RodIndex; public byte BaitIndex; public ushort BaitCount; }  // BaitCount 0xFFFF = unlimited
public readonly struct CreelSnapshot
{
    public readonly int Entries, TotalWeightG, MaxEntries, MaxWeightG;
    public CreelSnapshot(int entries, int totalWeightG, int maxEntries, int maxWeightG);
}
public struct CreelItem { public ushort EntryId, ItemIndex; public bool IsJunk; public int WeightG; public CatchQuality Quality; public int ValueCoins; public int CaughtAtRunMinute; }

public static class CreelRules   // spec 9.10 overweight rule
{
    public static bool CanAccept(in CreelSnapshot c);   // Entries < MaxEntries && TotalWeightG < MaxWeightG
    public static bool IsWarn(in CreelSnapshot c);      // Entries >= 0.8 * MaxEntries || TotalWeightG >= 0.8 * MaxWeightG
    public static bool IsFull(in CreelSnapshot c);      // !CanAccept
}

// Host slot-keyed creel store (R46: survives disconnect and rejoin within the session). Host only; lives on SessionState (T16).
public sealed class CreelLedger
{
    public CreelLedger(int slotCount);
    public IReadOnlyList<CreelItem> Items(byte slot);
    public CreelSnapshot Snapshot(byte slot, int maxEntries, int maxWeightG);
    public CreelItem Add(byte slot, CreelItem item);      // assigns EntryId (per slot, monotonic, starts 1); returns stored item
    public int SellAll(byte slot);                         // sum of ValueCoins; clears the slot
    public void ClearSlot(byte slot);                      // T16-R1 slot takeover: empties the creel AND the debug wallet; the EntryId counter keeps counting
    public int DebugCoins(byte slot);                      // M2 debug wallet (sum of sales)
    // ctor(0) throws ArgumentOutOfRangeException; any slot >= slotCount throws ArgumentOutOfRangeException (the last slot, 3 of 4, works).
}

public static class CatchValidator   // pure host checks (spec 9.9)
{
    public static CastRejectReason ValidateCast(in CastRequestData req, uint lastAcceptedCastId, bool hasActive,
                                                in HostCastView view, bool baitAvailable, float minLandDepth);
        // order: CastingAllowed, ActiveCast, StaleCastId (castId <= last), BadCharge (outside [0,1] or NaN),
        // OriginTooFar (> 3 m horizontal; a NaN origin counts as too far), OriginTooFar again for a non-finite YawDeg (I-1; NaN and +Inf tested),
        // NoBait, then CastMath.ProbeReason
    public static bool OriginWithinSlack(float originX, float originZ, float playerX, float playerZ);   // <= HostOriginSlack (3 m) inclusive; NaN/Inf fail
    public static bool IsFinite(float v);                                   // not NaN, not +/-Inf
    public static bool IsOriginFinite(in CastRequestData req);              // OriginX/Y/Z and YawDeg finite; FishingHost checks it BEFORE the physics probe (T17 follow-up)
    public static float WalkBoundedLineLength0(float hostLineLength0, float castDistance, double elapsedSeconds, float landDistance);
        // T9-R1: max(hostLineLength0, castDistance - MovementMath.SprintSpeed * elapsed - HookedLineSlack 3.1), floored at landDistance; a NaN bound is ignored.
        // R67: castDistance here is the horizontal ORIGIN -> LANDING distance; the ledger passes FishingConstants.CastTipReach + ActiveCast.CastDistance.
        // No new tuning: MaxWalkSpeed is the Core sprint speed. A teleport-adjacent player still gets a real floor (tested).
    public static bool HookTooEarly(double now, in ActiveCast cast, float hookTolerance);
        // now - TAccepted < TFlight + Roll.BiteWait - hookTolerance
    public static float LandedFloorSeconds(float lineLength0, float landDistance, float speed);
        // max(0, 0.8 * (lineLength0 - landDistance) / speed - 0.5)
    public static bool LandedTooFast(double now, in ActiveCast cast, float landDistance, float speed);
    public static float HostLineLength0(float hostDistance, float landDistance);  // max(landDistance, hostDistance - HookedLineSlack 3.1); a NaN distance gives landDistance (safe default only: Hooked fails closed on NaN, I-2)
    public static AnnounceKind AnnounceFor(FightOutcome outcome, EscapeReason reason, bool wasHooked);
        // Landed -> Landed; Snapped -> Lost; Escaped after hook with Pinned/ThrewHook/Dropped/Timeout -> Lost; everything else None
    public static bool CleanClaimAccepted(in FightReport report, double now, in ActiveCast cast, float landDistance, float reelSpeed,
                                          float cleanMaxTension);
        // The host only ever downgrades: true only when ALL hold, else the catch keeps its rolled quality (never Invalid):
        //  (1) not junk; (2) MaxTension finite, 0 <= MaxTension < cleanMaxTension; (3) NearSnapRecoveries == 0 (a recovery needs
        //  tension > 0.8, so a claim with one contradicts itself); (4) FightSeconds finite and >= LandedFloorSeconds(LineLength0, landDistance, reelSpeed);
        //  (5) FightSeconds <= (now - THooked) + FishingConstants.CleanClaimClockSlackSeconds (asymmetric: the owner's fixed-step clock drops
        //      time on frame hitches, so it may run well behind the host but never ahead of it plus the slack).
        // The host never replays the fight (spec 9.9), so these are the physical, clock and self-consistency bounds.
}

public struct RollOverrides { public float ForcedBiteWait; public int ForcedItemIndex; public bool ForceJunk; public bool Twitchy; public static readonly RollOverrides None; }
    // None has ForcedItemIndex = -1 (a default(RollOverrides) would force species 0); T17's override builder starts from None (B-Q14).

public sealed class FishingHostLedger
{
    public FishingHostLedger(FishingDatabase db, ulong runSeed, ushort defaultRodIndex, byte defaultBaitIndex);
    public ulong RunSeed { get; }
    public uint HostCastCounter { get; }
    public double Now { get; }                                  // pause-aware ledger clock
    public void EnsureSlot(byte slot);                          // debug loadout once per slot: 20 of each bait, bare hook unlimited, defaults; RequestCast for a slot never ensured -> UnknownPlayer, so T17 calls EnsureSlot before any RequestCast (B-Q10)
    public PlayerLoadout GetLoadout(byte slot);
    public bool SelectBait(ulong clientId, byte slot, byte baitIndex);  // refused while that client has an ActiveCast or index invalid
    public bool SelectRod(ulong clientId, byte slot, ushort rodIndex);
    public bool TryComputeLanding(byte slot, in CastRequestData req, out float x, out float z); // host rod castMaxDist; false for an unknown slot, an invalid charge (NaN or outside [0,1]) or a non-finite YawDeg (I-1); R67: the distance is from the rod tip and the shared CastMath.Landing adds CastTipReach
    public CastVerdict RequestCast(ulong clientId, byte slot, in CastRequestData req, in HostCastView view, in RollOverrides overrides);
        // accept: consume bait (not unlimited), HostCastCounter++, rng = Rng.Stream(RunSeed, FishingConstants.CatchStreamId, HostCastCounter),
        // PlaceholderRoller.Roll, then Roll.Pity = (PityAfterFailures > 0 && slot FailStreak >= PityAfterFailures) (R65, no RNG),
        // ActiveCast { TAccepted = Now, TFlight = CastMath.FlightTime(distance) }
    public CommitDecision Cancel(ulong clientId, uint castId);
        // Waiting + match -> Cancelled, refund (CancelRefundsBait), cleared. Hooked + match -> Escaped(Dropped), announced Lost, no refund
        // (B-Q9; the owner never sends Cancel while Hooked). Orphan -> Accepted = false, repeat stored resolution.
    public bool Hooked(ulong clientId, uint castId, float hostDistanceToLanding, out CommitDecision commit);
        // true = a commit must be sent (too early -> Escaped(Invalid); a NON-FINITE hostDistanceToLanding -> Escaped(Invalid), fail closed (I-2); orphan).
        // false = accepted (Phase Hooked, THooked = Now, LineLength0 = WalkBoundedLineLength0(HostLineLength0(hostDistance, landDistance), CastTipReach + CastDistance, Now - TAccepted, landDistance); T9-R1, R67: ActiveCast.CastDistance is from the rod tip, so the reach is added for the origin -> landing bound).
    public CommitDecision Result(ulong clientId, uint castId, FightOutcome outcome, EscapeReason reason, in FightReport report,
                                 in CreelSnapshot creel);
        // validation per spec 9.9; Landed: if CatchValidator.CleanClaimAccepted -> Roll.Catch = CatchMath.WithCleanBonus(...), CleanBonus = true;
        // then CanAccept -> AddToCreel, Announce Landed; full -> ReleasedCreelFull; failure -> Escaped(Invalid).
        // Always cleared afterwards; resolution stored for orphans.
    public void Tick(double dt, bool paused, List<CommitDecision> unsolicited);
        // advances Now unless paused; a NaN, negative, zero or infinite dt is ignored; stuck Waiting (Now - TAccepted > TFlight + BiteWait + 30) -> refunded Cancelled;
        // Hooked past MaxFightTime -> Escaped(Timeout), Announce Lost
    public bool Disconnect(ulong clientId);                     // clears that client's ActiveCast and cast-id state, no commit, no refund of a Waiting cast's bait (B-Q11)
    public bool TryGetActive(ulong clientId, out ActiveCast cast);
    public IReadOnlyList<CatchResult> CatchLog { get; }         // landed catches this session (dev overlay)
}
```
Bad-luck nudge (R65 / M2-D23, ledger side; all host, deterministic, no RNG): per slot a `FailStreak` (survives rejoin like bait stock). When a FIRST resolution is stored: `Landed` -> 0; `Snapped` or `Escaped` (any reason, including Missed, Invalid and Timeout) -> +1; if the resolved cast was a Pity cast the streak becomes 0 whatever the outcome; `Cancelled` (owner cancel, stuck-Waiting refund, recall) neither counts nor resets, so a cancelled Pity cast leaves the next accepted cast Pity again; `Disconnect` changes nothing. Orphans never count.

Keys: bait stock, rod and selected bait by **slot** (survive rejoin); `lastAcceptedCastId`, `ActiveCast` and the stored resolution by **clientId** (reset on disconnect, since castIds restart per connection).

Added after the T9 review (C 16): the fail-streak counts `Escaped(Timeout)` and `Escaped(Dropped)` like any other Escaped (pity rows tested); the landing floor holds for a non-Handmade rod (host rod stats, never owner values). **Mini-fix batch after T19 (C 16.3; in the working tree at the time of writing, committed by the controller):** `public bool ResetSlot(int slot)` on `FishingHostLedger` (Core; tested) puts that slot's rod, selected bait, bait stock and pity `FailStreak` back to the fresh-slot loadout that `EnsureSlot` gives (a slot never ensured is left alone; returns true when a slot was reset; client state - cast ids, `ActiveCast`, stored resolutions - is untouched). `SessionState` raises `public static event Action<int> SlotTakenOver` (host only, after a DIFFERENT persistent identity takes the slot over, JoinRules takeover rule c; never for the same identity rejoining, R46, nor for a brand-new slot; raised through `EventIsolation`), and `FishingHost` subscribes and calls `ResetSlot`, re-mirroring the loadout NetVars, next to the `CreelLedger.ClearSlot` of T16-R1. `CatchValidator.OriginWithinSlack` (7.9 above) is the shared 3 m rule the adapter runs before the probe. `Fishing/AssemblyInfo.cs` also gains `InternalsVisibleTo("FishGame.Tests.PlayMode")` for the takeover PlayMode test.

### 7.10 T10 - Input rows, context arbiter, dev input, settings

Core (`Core/Input/`):
```csharp
// InputContextRouter (edit): new rows (spec 8.2). Carrying/Staggered/Downed/Carried/Seated still route like Free (D18 kept for M5/M6).
// RodReady:    Move, Look, Jump, Sprint, Secondary*, Interact, Shove, Equip, Pause pass. Primary*, Drop zeroed.
// RodAiming:   Move, Look, Jump, Primary*, Secondary*, Equip, Pause pass. Sprint, Interact, Shove, Drop zeroed.
// RodWaiting:  Move, Look, Jump, Sprint, Secondary*, Interact, Shove, Pause pass. Primary*, Drop, Equip zeroed.
// RodFighting: Look, Primary*, Pause pass. Move, Jump, Sprint, Secondary*, Interact, Shove, Drop, Equip zeroed.
// RouteFight unchanged: RodFighting/Staggered -> (Steer, Reel || PrimaryHeld). EnabledMaps unchanged.
public static bool InteractLive(InputContext context);   // Free, RodReady, RodWaiting, Swimming -> true; Carrying false in M2 (unused context; M5 revisits spec 8.2; ruling C-Q2)

// Core/Input/GameplayContextArbiter.cs
public static class GameplayContextArbiter
{
    // Climbing > Swimming > requested (any gameplay context except Menu/Climbing/Swimming) > Free.
    public static InputContext Resolve(bool climbing, bool swimming, InputContext requested);
    public static bool IsRodContext(InputContext context);           // RodReady..RodFighting
    public static bool LocksMovement(InputContext context);          // RodFighting (M5 adds Staggered/Downed/Carried)
    public static bool IsAiming(InputContext context);               // RodAiming (x0.6 speed, no sprint)
}
```
Runtime:
```csharp
// Runtime/Input/DevKey.cs
public enum DevKey : byte { ToggleOverlay = 0, NextRod = 1, NextBait = 2, ToggleForcedBite = 3, ToggleTwitchy = 4, RippleAtCrosshair = 5 }
// Runtime/Input/IDevInput.cs - implemented by PlayerInputAdapter (reads Keyboard.current F3,F4,F5,F6,F7,F9) and FakeInputService.
// Always false unless Debug.isDebugBuild, the window has focus and the context is not Menu.
public interface IDevInput { bool WasPressed(DevKey key); }
```
Consumers resolve it null-safely as `ServiceRoots.App.Get<IInputService>() as IDevInput` (no App edit).

`SettingsData` (edit): `public int lineGauge = 0;` (0..2, `LineGaugeMode`), `public bool captions = true;`, `public bool fishingAssist = false;`; `Clamp` clamps `lineGauge`. Whenever the assist toggle changes from off to on while the line gauge is Off, the gauge is set to Arc, every time (UI rule, T25; ruling G-Q4). `SettingsData.version` stays 1 (the new fields are additive with defaults; ruling C-Q10).

### 7.11 T11 - Audio M2

Core (`Core/Audio/`):
```csharp
// SfxIds (edit): M2 ids
public const string CastWhoosh = "Cast_Whoosh", SplashSmall = "Splash_Small", SplashBig = "Splash_Big", BitePlop = "Bite_Plop",
    NibbleTick = "Nibble_Tick", ReelClick = "Reel_Click", LineCreak = "Line_Creak", DiveThrum = "Dive_Thrum", Snap = "Snap",
    CatchFlop = "Catch_Flop", CatchCheer = "Catch_Cheer", RodThunk = "Rod_Thunk", LineCrack = "Line_Crack"; // LineCrack: T23-fix d4483c3
// SfxRecipes (edit)
public static IReadOnlyList<SfxRecipe> M2 { get; }     // spec 11.7 M2 rows + Rod_Thunk + Line_Crack (14, M2-D10; Line_Crack appended last, T23-fix); Line_Creak and Dive_Thrum loop
public static IReadOnlyList<SfxRecipe> All { get; }    // M1 then M2; TryGet and Generate cover All
// SfxRecipe (edit): adds `public readonly float MinDistance;` (default 1) with a 9-arg ctor; the 8-arg M1 ctor stays (MinDistance 1).
//   9-arg order (ruling C-Q1): (id, lengthSeconds, loop, category, baseVolume, spatial, maxDistance, minInterval, minDistance), minDistance LAST.
//   MaxDistance 0 keeps meaning "default 30"; the T14 cue-table validator compares the EFFECTIVE max (0 -> 30) with MinDistance. No M1 value changes.
//   SfxRecipes becomes a partial class: the M2 table and generators live in Core/Audio/SfxRecipes.M2.cs (ruling C-Q11).
//   Bite (Splash_Big) uses MinDistance 8 (spec 9.4 "large minDistance").

// Core/Fishing/RodCueAudio.cs - cue -> sfx map (spec 9.11)
public static class RodCueAudio
{
    public static string SfxFor(RodCue cue);       // null = no sound; map below
    public static bool IsStinger(RodCue cue);      // Bite, Snapped -> duck ambience
}
```
Cue map (T11 implements exactly): Equip/Stow -> `Rod_Thunk`; Ready -> `Reel_Click`; ChargeStart -> `Cast_Whoosh` (pitch follows charge, presentation); CastRelease -> `Cast_Whoosh`; InvalidRelease -> `Rod_Thunk`; Landing -> `Bite_Plop`; Recall -> `Rod_Thunk`; Nibble -> `Nibble_Tick`; Bite -> `Splash_Big`; HookSet -> `Reel_Click`; SideRun -> `Splash_Small`; Dive -> `Dive_Thrum` (loop start); Calm -> none; Exhausted -> `Splash_Small`; Snapped -> `Snap`; Escaped -> `Splash_Small`; LineLanded -> `Catch_Flop`. The catch announce (every peer) plays `Catch_Flop` + `Catch_Cheer`.

Runtime (`Runtime/Audio/AudioBus.cs`, edit):
```csharp
public void PlayAt(string sfxId, Vector3 position, float pitchMult);     // pitch multiplier on top of jitter (clamped 0.1..3, NaN = 1); rate limit keyed by id + 2 m cell (floor(x/2), floor(z/2)); Play2D keyed by id (rulings C-Q6, C-Q8)
public bool Play2D(string sfxId, AudioCategory category);                 // returns TRUE when the cue started, FALSE when dropped (rate limit, unknown id, missing clip, bus not ready); T11 follow-up `2296e1a`: the audition menu counts only real plays
public AudioLoop StartLoop(string sfxId, Transform follow, bool spatial); // looped source on its own child AudioSource; null when the bus is not ready, the recipe or clip is missing, or 16 loops already run (each warns once). `follow` is tracked in LateUpdate and the loop stops when it is destroyed; follow == null keeps the loop at the bus. Callers that start 2D (owner) loops still pass a `follow` so the loop dies with its owner (T23, C 16.2)
public void SetLoop(AudioLoop loop, float volume01, float pitchMult);
public void StopLoop(AudioLoop loop);                                     // idempotent; null-safe
public void Duck(float seconds);       // Ambience and Music to 50% over 0.3 s, held `seconds`, then back over 0.3 s
public void ApplyCueTable(IReadOnlyList<SfxRecipe> overrides);           // mixing fields by id (category, base, spatial, min/max distance, minInterval); a non-null call REPLACES the previous table (it does not merge); null = Core defaults; unknown ids are ignored with a warn-once. Sanitising (T11 follow-up): a non-finite BaseVolume falls back to the Core value, non-finite or non-positive MinDistance -> 1, MaxDistance -> 30 (default) and never below MinDistance, a non-finite or negative MinInterval -> 0; takes effect from the next play, running loops keep their values
// Runtime/Audio/AudioLoop.cs
public sealed class AudioLoop { public string SfxId { get; } public bool IsPlaying { get; } }   // IsPlaying = !Stopped && Source != null && Source.isPlaying (a destroyed source is false, no exception); Source/Follow/Category/BaseVolume/Volume01/Stopped are internal (only AudioBus creates loops)
```
`SfxGenerator.GenerateAll` iterates `SfxRecipes.All`. `PlayAt` uses the recipe's `MinDistance`.

Runtime (`Runtime/Services/CaptionRequested.cs`): `public readonly struct CaptionRequested { public readonly string Key; public readonly float Seconds; public CaptionRequested(string key, float seconds = 2f); }` (EventBus message; UI shows bottom-centre).

Editor: `Editor/Dev/AuditionCuesMenu.cs` (section 6.5).

### 7.12 T12 - Water math, mesh builder, ripples

Core:
```csharp
// Core/World/WaveSet.cs   (direction convention: 0 deg = +Z, dir = (sin, cos) on (x, z); rulings C-Q4, F-Q1/Q2)
public struct Wave { public float DirectionDeg, Amplitude, Wavelength, Speed; }
public struct WaveSet { public Wave W0, W1, W2; public static readonly WaveSet Flat; }
// Core/World/WaveMath.cs
public static class WaveMath
{
    // sum over 3: A * sin(k * (dot(dir, (x, z)) - Speed * t)), k = 2*pi / Wavelength; Wavelength <= 0 or A == 0 -> term 0.
    // Any NON-FINITE field (NaN or +/-Inf in Amplitude, Speed, DirectionDeg or Wavelength) is also a 0 term, in HeightAt and in AmplitudeSum
    // (T12 follow-up `4385084`). The T21 Water shader must match this exactly or CPU and GPU heights diverge (C 16.2).
    public static float HeightAt(in WaveSet waves, float x, float z, double t);
    public static float AmplitudeSum(in WaveSet waves);
}
// Core/World/RippleMath.cs
public static class RippleMath
{
    public const int Slots = 8; public const float RingSpeed = 2.2f, RingWidth = 0.25f, Lifetime = 2.5f;
    public const float CastLanding = 0.6f, Nibble = 0.2f, Bite = 1.0f, BiteFollow = 0.3f, Snap = 0.8f, Landed = 0.7f, Shove = 1.0f;
    public static float Ring(float dist, float age, float strength);  // (1 - sat(|dist - age*2.2| / 0.25)) * sat(1 - age/2.5) * strength; age < 0 -> 0 (T21 shader matches; ruling C-Q5)
    public static int NextSlot(int lastSlot);                          // round robin, oldest recycled
}
// Core/Render/FaceJitter.cs
public static class FaceJitter { public static float Grey(uint meshSeed, int faceIndex, float jitter); }  // 1 +- jitter from FNV/SplitMix hash; deterministic. MeshBuilder STORES 0.5 * Grey (BF-7)
// Core/Render/ShaderGlobals.cs - property name constants (5.3) plus the full array lengths of C 2.8 #18:
//   public const int PaletteLength = 16, WavesLength = 4;   (the ripple array length is RippleMath.Slots = 8)
```
Runtime:
```csharp
// Runtime/World/WaterSurface.cs (edit; M1 members kept)
public static void Configure(in WaveSet waves);         // default WaveSet.Flat
public static void SetTimeSource(Func<double> now);     // null -> Time.timeSinceLevelLoad; WaterTimeDriver's source returns (double)(float)time so C# and HLSL use one float value (parity rule, C-Q4)
public static double Now { get; }
public static float HeightAt(float x, float z);         // HeightAt(x, z, Now)  (M1 signature, now wave-aware). Main-thread only unless a time source is set (the default clock reads Time.timeSinceLevelLoad); class comment says so
public static float HeightAt(float x, float z, double t);
public static void ResetToFlat();                       // also on SubsystemRegistration
// Runtime/World/MeshBuilder.cs - flat-shaded primitives (spec 10.2)
public sealed class MeshBuilder
{
    public MeshBuilder(uint meshSeed, float faceJitter = 0.04f);
    public int TriangleCount { get; }
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, byte slot);
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, byte slot);
    public void Box(Vector3 center, Vector3 size, Quaternion rotation, byte slot);
    public void Prism(Vector3 center, float radius, float height, int sides, Quaternion rotation, byte slot);
    public void Cylinder(Vector3 center, float radiusBottom, float radiusTop, float height, int sides, Quaternion rotation, byte slot);
    public void Capsule(Vector3 center, float radius, float height, int sides, int rings, Quaternion rotation, byte slot);
    public void Icosphere(Vector3 center, float radius, int subdivisions, byte slot);
    public Mesh ToMesh(string name);  // unshared verts (3 per tri), face normal, uv1.x = slot (UV channel 1 = TEXCOORD1), vertex colour = HALF-scale grey 0.5 * FaceJitter.Grey = 0.5 * (1 +/- faceJitter) so it fits UNorm8 storage (BF-7); deterministic bytes
    public static Mesh Grid(int cellsX, int cellsZ, float cellSize, string name); // shared vertices, centred, y 0 (water)
}
// Winding (T12 follow-up `4385084`): every primitive is wound outward. MeshBuilderTests asserts, per triangle, dot(faceNormal, centroid - centre) > 0
// for box, prism and frustum sides, cone (top radius 0 and bottom radius 0), capsule (body and no-body) hemispheres and the icosphere, and
// caps against +/-(rotation * up), including a rotated cylinder; the Grid test asserts upward normals.
```

### 7.13 T13 - Data defs and registry (asm FishGame.Data)

Every Def: `public sealed class XDef : ScriptableObject`, private `[SerializeField]` fields named as below, one getter per field (PascalCase), `public void Configure(...)` taking every field in the listed order (builder and tests), and `ToCore()` where a Core mirror exists. No field initializers carry tuning values (5.1).

| Def (file `Data/<folder>/<Name>.cs`) | Fields (order) | Core |
|---|---|---|
| `Fishing/RodDef` | `id, displayName, tier (byte), castMaxDist, reelSpeed, lineStrength, angleWindow, steerRate, relaxMult, shopCost (int), firstCamp (int)` | `RodStats ToCore()` |
| `Fishing/BaitDef` | `id, displayName, rating, stackMax, maxRating, biteSpeedMod, junkMult, maxNibbles, unlimited (bool)` | `BaitStats ToCore()` |
| `Fishing/FightProfileDef` | `id, displayName, pullStrength, lateralSpeed, runSpeed, staminaDrain, calmMin, calmMax, burstMin, burstMax, flipRate, diveChance, burstRamp, calmDrift, calmDriftRamp, flipMinGap, lateralJitter` | `FishFightProfile ToCore()` |
| `Fishing/FishSpeciesDef` | `id, displayName, rarity, baseValue, weightMinG, weightMaxG, weightSkew, lengthShapeK, fight (FightProfileDef), bodyShape, colorA (Color), colorB (Color), biteWaitMod, hookWindow, isSpecial, minBaitRating, minDepth, model (Mesh; may be null)` | `FishSpeciesData ToCore(ushort fightProfileIndex)` |
| `Fishing/JunkDef` | `id, displayName, valueCoins, bodyShape, color (Color)` | `JunkData ToCore()` |
| `Fishing/FishingTuningDef` | one field per `FishingTuning` member (including the four R65 pity members `pityAfterFailures, pityStrainMult, pityGraceMult, pityHookWindowBonus`), same names camelCase, with `creelMaxKg (float)` stored as kg instead of `creelMaxWeightG` | `FishingTuning ToCore()` (kg -> g) and `Configure(in FishingTuning t)` |
| `Audio/SfxCueTableDef` | `rows (List<SfxCueRow>)`; `[Serializable] public struct SfxCueRow { public string id; public AudioCategory category; public float baseVolume, minDistance, maxDistance, minInterval; public bool spatial; }` | `IReadOnlyList<SfxRecipe> ToRecipes()` (length and loop from Core; uses the 9-arg `SfxRecipe` ctor, order in 7.11) |
| `Art/ArtPaletteDef` | `slotNames (string[16]), seasons (PaletteSeason[4]), faceJitter, skinTone (Color), phases (DayPhaseAmbient[4]), uiAccent, uiGood, uiWarn, uiBad, uiPanel, uiText, m2Season (int, default index 1 Summer)` | `Vector4[] PaletteVectors(int season)` (length 16); `Color GetSlotTint(int slot)` reads **`SlotPalette`** (no stored copy); `Color GetSkinTone()` |
| `Art/PaletteSeason` (`[Serializable]` class) | `name, colors (Color[16])` | - |
| `Art/DayPhaseAmbient` (`[Serializable]` struct) | `startFraction, lightColor, lightIntensity, skyTop, skyHorizon, skyGround, toonShade, toonAmbient, fogColor, fogDensity, warmWeight, coolWeight` (spec 10.3) | - |
| `Art/WaterSettingsDef` | `dirAmpLenSpeed (Vector4[3])` (x direction deg, y amplitude m, z wavelength m, w speed m/s), `shallowColor, deepColor, depthRange (1.6, R73), foamWidth (0.35)` | `WaveSet ToWaveSet()` |

`ContentRegistry` (edit):
```csharp
public const string CategoryRods = "rods", CategoryBaits = "baits", CategoryFightProfiles = "fight_profiles",
                    CategorySpecies = "species", CategoryJunk = "junk";
[SerializeField] List<RodDef> rods; List<BaitDef> baits; List<FightProfileDef> fightProfiles; List<FishSpeciesDef> species; List<JunkDef> junk;
[SerializeField] FishingTuningDef fishingTuning; SfxCueTableDef sfxCueTable; ArtPaletteDef artPalette; WaterSettingsDef waterSettings;
public IReadOnlyList<RodDef> Rods { get; } /* Baits, FightProfiles, Species, Junk likewise */
public FishingTuningDef FishingTuning { get; } public SfxCueTableDef SfxCueTable { get; } public ArtPaletteDef ArtPalette { get; } public WaterSettingsDef WaterSettings { get; }
public void ConfigureFishing(IEnumerable<RodDef> rods, IEnumerable<BaitDef> baits, IEnumerable<FightProfileDef> profiles,
                             IEnumerable<FishSpeciesDef> species, IEnumerable<JunkDef> junk, FishingTuningDef tuning,
                             SfxCueTableDef cues, ArtPaletteDef palette, WaterSettingsDef water);   // builder + tests
public ushort RodIndex(string id); public RodDef RodAt(ushort index);              // and BaitIndex/BaitAt, FightProfileIndex/FightProfileAt,
public ushort SpeciesIndex(string id); public FishSpeciesDef SpeciesAt(ushort i);  // SpeciesIndex/SpeciesAt, JunkIndex/JunkAt (same shapes;
public bool TryGetSpecies(string id, out FishSpeciesDef def);                      //  KeyNotFoundException / ArgumentOutOfRangeException)
public FishingDatabase Fishing { get; }   // built in Build() when fishing content is present; null otherwise
public string DisplayNameOf(bool isJunk, ushort itemIndex);  // UI helper
```
`Validate()` adds: invalid/duplicate ids per category, null entries, species with a null `fight` or a fight not in `fightProfiles`, missing singletons when any fishing list is non-empty, no unlimited bait. `ComputeContentHash()` includes spots always and each fishing category only when its list is non-empty (a spots-only registry hashes exactly as in M1; ruling D-Q1).

### 7.14 T14 - Seeds, content builder, fishing validators (asm FishGame.Editor)
```csharp
// Editor/Builders/Fishing/FishingSeedData.cs - the single seed source (5.1); spec tables 9.6, 9.7, 9.12, 9.10
public static class FishingSeedData
{
    public static FishingTuning Tuning { get; }
    public static IReadOnlyList<(string id, string name, RodStats stats, int shopCost, int firstCamp)> Rods { get; }
    public static IReadOnlyList<(string id, string name, BaitStats stats)> Baits { get; }
    public static IReadOnlyList<(string id, string name, FishFightProfile profile)> Profiles { get; }
    public static IReadOnlyList<SpeciesSeed> Species { get; }   // SpeciesSeed: id, name, profileId, data fields, bodyShape, colorA, colorB, modelFile
    public static IReadOnlyList<(string id, string name, int value, BodyShape shape)> Junk { get; }
    public static WaveSet Waves { get; }                        // 3 gentle waves, amplitude sum <= 0.12 m, wavelengths >= 6 m
    public static FishingDatabase BuildDatabase();              // test convenience: Core database straight from the seeds
}
public static class FishingContentBuilder   // Editor/Builders/Fishing/FishingContentBuilder.cs; called from ContentBuilder.Build (hook)
{
    public static void Build();   // create-if-missing every 6.2 Def asset; registry lists + singleton refs; fill null species models (6.2 rule). Calls BuildAt(RealContentRoot, loaded registry)
    internal static void BuildAt(string contentRoot, ContentRegistry registry);
        // T14 follow-up `03bb4a9` (review F1): the same build rooted at `contentRoot` (must start with "Assets/") and wired into the GIVEN registry;
        // every AssetPaths constant is re-rooted from "Assets/_Project/Content". Builder tests run it under `Assets/_Project/Tests/__Temp/<Fixture>`
        // (OneTimeSetUp deletes a stale folder, creates the folder and a fresh registry asset, calls BuildAt; OneTimeTearDown deletes the folder;
        // `.gitignore` has `/Assets/_Project/Tests/__Temp*`). No test may write to committed Content assets.
        // F2: the private Ensure<T> THROWS InvalidOperationException("asset exists but is not a T: <path>") when the file or GUID exists but
        // LoadAssetAtPath returns null (an unloadable asset is never overwritten); tested.
}
public static class FishingValidators       // Editor/Validation/FishingValidators.cs; ValidationRunner hook
{
    public static List<string> All();               // Prefixed(FishingContent(), SpeciesModels()): "FishingContent: ..." and "SpeciesModels: ..."
    internal static List<string> Prefixed(List<string> contentErrors, List<string> modelErrors);   // prefixing seam; tests feed in-memory lists
    internal static List<string> CheckRegistry(ContentRegistry registry);                          // in-memory registry checks (tests build bad registries)
    internal static List<string> CheckSpeciesModel(string speciesId, Mesh model, string assetPath);
    internal static List<string> CheckSeedAssets(ContentRegistry registry, Func<string, UnityEngine.Object> load);
    public static List<string> FishingContent();   // ids unique, ranges valid (9.7/9.6/9.12 ranges), each species reachable with some bait and depth <= 6 m;
                                                   // motion ranges: burstRamp 0..2, calmDrift 0..0.5, calmDriftRamp 0..5, flipMinGap 0.2..2, lateralJitter 0..0.5;
                                                   // thrasher calmMin >= 1.0; cleanCatchMaxTension 0.5..0.95;
                                                   // pity: pityAfterFailures 0..10 (0 = off), pityStrainMult 0.5..1, pityGraceMult 1..3, pityHookWindowBonus 0..1;
                                                   // cue rows: the EFFECTIVE max distance (0 -> 30) > minDistance (ruling C-Q1); cue rows reject non-finite values;
                                                   // bait maxNibbles 0..FishingConstants.MaxNibbleSlots (3); water/palette colours and wave direction finite
    public static List<string> SpeciesModels();    // a non-null model must come from Content/Models/*.fbx, have vertex colours AND subMeshCount == 1
                                                   // ("species <id>: model <path> has <n> submeshes, expected 1"; ruling G-Q6); null is allowed (fallback mesh)
}
```

### 7.15 T15 - Tuning bots (asm FishGame.Tests.EditMode, `[Category("Tuning")]`)
> **R72:** bots and targets replaced by plan rework-fight F4.

`Tests/EditMode/Tuning/FishingBots.cs`: Greedy (always reels, never steers), CounterReel (always reels, steers against side runs), Rhythm (counters side runs, stops reeling in dives and when tension >= 0.8, resumes below 0.5). Bots load the **real assets** through `AssetDatabase.LoadAssetAtPath` and the Defs' `ToCore()` (never the seeds), run 200 seeds per profile per rod (Twig, Handmade) at weightT 0.5 and 0.9 from an 18 m line (Twig: 14 m), and print fight-length, snap-rate, near-snap-recovery and clean-catch-rate distributions (`TestContext.WriteLine`). They also print each profile's signature-motion stats (flips per second, line crept out per Calm second, time to full burst force, per-burst lateral-speed spread), so the five profiles are told apart in numbers before the blind playtest. A failing target is fixed by retuning data (2.6 data-asset row), never by changing `FightSim`. Rulings (section 15): the bots' counter-steer holds until the fish is `CounterHold = 0.1` (= `CenterDeadzone`) past centre (D-Q3); targets assert at weightT 0.5, and the Twig check at 0.5 and 0.9 (D-Q5); a target that cannot be met may be listed in a `TuningItems` map (`Assert.Ignore` with the numbers; each entry needs the controller's approval and is carried into T30; D-Q6); T14 creates the `TunedDeviations` map in `FishingContentTests.cs` and T15 may edit only that map (D-Q2). The bots never use Pity.

Delivered (T15, `5a652bd`; test follow-up `f1476c3`; spec tables `4a4028f`): `TunedDeviations` key format is `tuning.<field>` (creel `tuning.creelMaxWeightG` in grams) or `<category>.<id>.<field>`. Retuned: `tuning.relaxWhileReeling` 0.10 -> 0.20, `profile.thrasher.staminaDrain` 0.09 -> 0.035, `profile.runner.staminaDrain` 0.05 -> 0.035 (seeds, assets and the map agree). Near-snap recovery >= 40% (spec 17.2) is a HUMAN fun-gate metric, not a bot gate: the bots print it for information only (Rhythm bulldog 3%, thrasher 6%, runner 57.5%); T30 watches it in the playtest fight logs (C 16.2). A unit test `FishingBots_RhythmDecide_PinsDiveEasingAndSteerRules` pins `Decide` over hand-built `FightState`s (dive: no reel and no steer; burst counter-steer; past-centre hold; calm reel; easing hysteresis 0.8/0.5; Greedy (0, true); CounterReel in a dive (0, true); `CounterHold == FishingConstants.CenterDeadzone`), and the bots clear their static caches in `[OneTimeSetUp]`. The Creep gate uses `exhaustedBefore` (accepted, informational).

### 7.16 T16 - Gameplay hooks (asm FishGame.Gameplay)

```csharp
// Gameplay/Player/PlayerNetState.cs (edit)
public readonly NetworkVariable<byte> NetTool = new NetworkVariable<byte>(0, Everyone, Owner);   // ToolKind
public ToolKind Tool { get; }
public event Action<ToolKind> ToolChanged;                 // every peer
public void OwnerSetTool(ToolKind tool);                   // owner only; ignored when unchanged
public bool IsServerInitialized { get; }                   // true after ServerInit (server only)
public event Action<PlayerNetState> ServerInitialized;     // server only; raised at the end of ServerInit (G2)

// Gameplay/Player/PlayerRigPaths.cs (edit)
public const string Hand = "Body/ArmR/Hand", Rod = "Body/ArmR/Hand/Rod", RodTipAnchor = "Body/ArmR/Hand/Rod/TipAnchor", RodTipVisual = "Body/ArmR/Hand/Rod/TipVisual",
    RodSegmentPrefix = "Body/ArmR/Hand/Rod/Seg", CatchHold = "Body/ArmR/Hand/CatchHold", Line = "Line", Bobber = "Bobber",
    CastMarker = "CastMarker";
public const int RodSegmentCount = 6;

// Gameplay/Player/IPlayerContextSource.cs - Fishing (and M5 carry) request a context without Gameplay referencing them.
public interface IPlayerContextSource
{
    InputContext RequestedContext { get; }   // Free = no request
    bool LocksMovement { get; }              // belt and braces next to the router's RodFighting row
}

// Gameplay/Player/PlayerLocomotion.cs (edit) - stays the ONLY writer of IInputService.GameplayContext.
// Awake: caches GetComponents<IPlayerContextSource>(). SetContext: GameplayContextArbiter.Resolve(IsClimbing, IsSwimming, first non-Free request).
// Movement: wish speed uses aiming = GameplayContextArbiter.IsAiming(context); LocksMovement (any source or the context) -> wish 0, no jump,
//   no ladder entry (R70, M2-D29); gravity and yaw continue; a climber who becomes locked hangs.
public event Action Teleported;   // raised at the end of ApplyTeleport (also out-of-bounds and water-timeout recovery)

// Gameplay/Player/PlayerCamera.cs (edit)
public void AddFovKick(float degrees, float seconds);   // additive vertical-equivalent kick that decays to 0 over `seconds`; x cameraMotion

// Gameplay/Player/PlayerInteractor.cs (edit): scans when InputContextRouter.InteractLive(input.GameplayContext) && !MenuOpen.

// Gameplay/Fishing/CreelEntry.cs - wire mirror of Core CreelItem (G1)
public struct CreelEntry : INetworkSerializeByMemcpy, IEquatable<CreelEntry>
{
    public ushort EntryId, ItemIndex; public byte IsJunk; public int WeightG; public byte Quality; public int ValueCoins; public int CaughtAtRunMinute;
    public static CreelEntry From(in CreelItem item); public CreelItem ToCore();
    public bool Equals(CreelEntry other); public override int GetHashCode();
}

// Gameplay/Fishing/PlayerCreel.cs
// Authority: host writes NetCreel and NetDebugCoins from SessionState.ServerCreels (slot-keyed, survives rejoin); every peer reads.
public sealed class PlayerCreel : NetworkBehaviour
{
    public NetworkList<CreelEntry> NetCreel;                       // Awake: new NetworkList<CreelEntry>(null, Everyone, Server)
    public readonly NetworkVariable<int> NetDebugCoins;            // Everyone / Server
    public int Count { get; } public int TotalWeightG { get; }
    public event Action Changed;                                   // every peer
    public CreelSnapshot Snapshot(int maxEntries, int maxWeightG);
    public bool ServerTryAdd(in CreelItem item, int maxEntries, int maxWeightG, out CreelItem stored); // host; CreelRules; ledger then mirror
    public int ServerSellAll();                                    // host; ledger SellAll, mirror, NetDebugCoins
    // Server: on PlayerNetState.ServerInitialized (or at spawn if already initialised) mirror ServerCreels.Items(slot) into NetCreel.
}

// Gameplay/Session/SessionState.cs (edit)
public CreelLedger ServerCreels { get; }   // host only (null on clients); created on server spawn with NetworkConstants.MaxPlayers slots

// Gameplay/Interaction/DockBell.cs (edit): ServerInteract also calls the interacting player's PlayerCreel.ServerSellAll()
//   when Debug.isDebugBuild (M2 debug "sell creel"); the ring is unchanged.
//   Follow-up (`d40534a`, M-4): a missing SessionState or ServerCreels is a silent no-op (no exception).
```
Follow-up deltas from the T16 review (`d40534a`; C 16.1): `PlayerLocomotion` resets the jump buffer while the movement lock is on (`_jump = JumpAssist.Create()`, as swimming does) so a jump pressed airborne never fires on landing while locked (I-1), and refreshes its context-source cache in `OnEnable` as well as `Awake` (M-5); `PlayerNetState.HandleToolChanged(byte previous, byte current)` is `internal` and returns when `previous == current`, so `ToolChanged` fires once per real change (M-2); `PlayerCreel.Snapshot/ServerTryAdd/ServerSellAll` throw when `!IsSpawned || !IsServer` and return false/0 while `!_player.IsServerInitialized` (M-3); `SessionState.Upsert` is `internal` (PlayMode tests) and **slot takeover (ruling T16-R1)**: when a slot passes to a DIFFERENT persistent identity (JoinRules takeover rule c) `Upsert` calls `ServerCreels.ClearSlot(slot)` (creel and debug coins); the same identity rejoining keeps both (R46). A subscriber of `PlayerNetState.ServerInitialized` already sees `Slot` written (M-7, tested by `PlayerNetState_ServerInit_SlotIsAlreadyWrittenInsideTheEvent`). `Gameplay/AssemblyInfo.cs` has `InternalsVisibleTo("FishGame.Tests.PlayMode")` (2.6). Notes carried to later tasks: `FishingRodController` must sit on the PlayerFP root (T19, done); the creel HUD (T25) reads `PlayerCreel.Count` at spawn because the initial NetworkList sync raises no `Changed`.

### 7.17 T17 - Fishing networking and host (asm FishGame.Fishing)

```csharp
// Fishing/Net/RodStateData.cs (spec 9.9; ~20 bytes)
public struct RodStateData : INetworkSerializeByMemcpy, IEquatable<RodStateData>
{
    public byte State; public byte Flags; public sbyte LateralQ; public byte TensionQ; public Vector3 BobberPos; public uint CastId;
    public bool Equals(RodStateData other);   // field-wise (V11: implement both memcpy and IEquatable)
    public override int GetHashCode();
}
// Fishing/Net/CatchResultNet.cs, FishingRollNet.cs - unmanaged mirrors (G1)
public struct CatchResultNet : INetworkSerializeByMemcpy
{
    public byte IsJunk; public ushort ItemIndex; public int WeightG; public ushort LengthMm; public byte Quality; public int ValueCoins;
    public static CatchResultNet From(in CatchResult c); public CatchResult ToCore();
}
public struct FishingRollNet : INetworkSerializeByMemcpy
{
    public CatchResultNet Catch; public float WeightT, BiteWait; public byte NibbleCount; public float Nibble0, Nibble1, Nibble2;
    public uint FightSeed; public byte SizeClass; public float HookWindow;
    public byte Flags; public const byte FlagPity = 1;   // R65: bit 0 = FishingRoll.Pity (From sets it, TryToCore restores it; other bits ignored)
    public static FishingRollNet From(in FishingRoll r);
    public bool TryToCore(FishingDatabase db, out FishingRoll roll);   // FishingRollValidation.IsSane; false -> caller logs and drops
}
// Fishing/Net/CatchAnnouncement.cs - local event payload (every peer)
public readonly struct CatchAnnouncement
{
    public readonly byte CatcherSlot; public readonly CatchResult Catch; public readonly AnnounceKind Kind; public readonly ulong CatcherClientId;
    public readonly bool CleanBonus;   // from CommitDecision.CleanBonus (host-accepted claim); drives toast.clean and the reveal sparkle
    public CatchAnnouncement(byte catcherSlot, in CatchResult c, AnnounceKind kind, ulong catcherClientId, bool cleanBonus);
}
// Fishing/Net/CatchAnnounced.cs and FishCaught.cs - EventBus messages
public readonly struct CatchAnnounced { public readonly CatchAnnouncement Announcement; public readonly bool IsLocalCatcher; public CatchAnnounced(in CatchAnnouncement a, bool isLocalCatcher); }
public readonly struct FishCaught { public readonly byte Slot; public readonly CatchResult Catch; public FishCaught(byte slot, in CatchResult c); } // published only on the host, iff Outcome == Landed && AddToCreel

// Fishing/Net/RodViewState.cs - what Presentation and UI draw, on every peer
public struct RodViewState
{
    public RodState State; public byte Flags; public float Lateral, Tension; public Vector3 BobberPos; public uint CastId;
    public bool IsOwner; public bool RodOut;
}

// Fishing/Net/FishingRodNetwork.cs
// Authority: owner writes NetRodState and sends intent RPCs (Server, Owner); host owns bait, rolls, ActiveCast, outcomes, the creel
// and writes NetSelectedBait, NetSelectedBaitCount, NetRodIndex (mirrors of FishingHostLedger). Remotes display only.
public sealed class FishingRodNetwork : NetworkBehaviour
{
    public readonly NetworkVariable<RodStateData> NetRodState;          // Everyone / Owner; traits MinSecondsBetweenUpdates = 1 / tuning.RodStateHz
    public readonly NetworkVariable<byte> NetSelectedBait;              // Everyone / Server
    public readonly NetworkVariable<ushort> NetSelectedBaitCount;       // Everyone / Server (0xFFFF unlimited)
    public readonly NetworkVariable<ushort> NetRodIndex;                // Everyone / Server
    public static IReadOnlyList<FishingRodNetwork> Active { get; }
    public static event Action<FishingRodNetwork> Spawned, Despawned;
    public PlayerNetState Player { get; }
    public RodViewState ViewState { get; }        // owner: live from FishingRodController; remotes: NetRodState smoothed (lerp, 1 - exp(-12 dt));
                                                  // the first value after spawn snaps (no cast arc replay)
    public event Action<RodCue, byte> Cue;        // every peer; arg = sizeClass (Snapped) or 0
    public event Action<CatchAnnouncement> Announced;   // every peer, for this rod's catcher
    public static event Action<CatchAnnouncement> AnyAnnounced;
    // Owner-side receive events (raised from the deferred owner RPCs):
    public event Action<uint, bool, CastRejectReason, Vector3, FishingRoll> CastResultReceived;   // roll already sanity-checked
    public event Action<uint, bool, FightOutcome, EscapeReason> CommitReceived;
    public void OwnerWriteState(in RodStateData data);   // owner; writes only when !Equals(current)
    public void RaiseLocalCue(RodCue cue, byte arg);     // owner path into Cue
    // RPCs: section 8.2 (public methods, called by FishingRodController and FishingHost).
}

// Fishing/Host/FishingHost.cs
// Authority: host only (inert on clients). Scene object "Fishing". Owns the session's FishingHostLedger, ticks its pause-aware clock
// (IPauseState), resolves timeouts and routes every host decision back through the player's FishingRodNetwork.
public sealed class FishingHost : MonoBehaviour
{
    public static FishingHost Current { get; }
    public FishingHostLedger Ledger { get; }      // null on clients and before the first server tick
    public ulong RunSeed { get; }                 // FishingConstants.DefaultDevRunSeed unless SetRunSeed was called
    public void SetRunSeed(ulong seed);           // dev; only before the first accepted cast (else ignored, logged once)
    internal void OnPlayerReady(FishingRodNetwork rod);       // server: EnsureSlot, mirror loadout NetVars
    internal void HandleCastRequest(FishingRodNetwork rod, in CastRequestData req);
    internal void HandleCancel(FishingRodNetwork rod, uint castId);
    internal void HandleHooked(FishingRodNetwork rod, uint castId);
    internal void HandleResult(FishingRodNetwork rod, uint castId, FightOutcome outcome, EscapeReason reason, in FightReport report);
    internal void HandleSelectBait(FishingRodNetwork rod, byte baitIndex);
    internal void HandleSelectRod(FishingRodNetwork rod, ushort rodIndex);
    // Commit path: CommitRpc to the owner -> creel add (PlayerCreel.ServerTryAdd, iff AddToCreel) -> CatchAnnouncedRpc (iff Announce != None)
    //   -> EventBus FishCaught (iff Outcome == Landed && AddToCreel) -> CatchLog. Effects are driven by the decision fields, never by
    //   Accepted (false only for orphans; ruling E-Q6). On an accepted cast with Roll.Pity the host also sends CatchAnnouncedRpc(slot, default, PityCast, 0) (R65). Disconnect: NetworkManager.OnConnectionEvent ClientDisconnected -> Ledger.Disconnect.
}

// Fishing/Host/CastProbe.cs - Physics adapter for CastMath validity (same function on owner and host, spec 9.3)
public static class CastProbe
{
    public static WaterProbe Probe(float x, float z);
        // ray 1 down from (x, WaterSurface.FlatHeight + 20, z), mask Water|Ground|Shore|Structure, QueryTriggerInteraction.Collide,
        // first hit (RaycastNonAlloc sorted by distance) must be layer Water; ray 2 down from that hit, mask Ground, 50 m
    public static int CastMask { get; } public static int BedMask { get; }
}
// T17 follow-up (`071dc2d`, C 16.1) - behaviour, no new public names:
//  - FishingHost.HandleCastRequest checks CatchValidator.IsOriginFinite BEFORE any physics probe and replies CastResultRpc(castId, false, OriginTooFar, Vector3.zero, default).
//  - FishingHost resolves the NetworkManager lazily (the scene object can enable before the singleton exists); a failing ledger construction of ANY kind
//    (missing default id, bad database) leaves Ledger null, logs once ("ledger-create-failed") and the handlers use their fallback replies.
//  - FishingHost.Commit with no SessionState.ServerCreels: skips the creel add, still announces, publishes FishCaught and mirrors the loadout, logs once
//    ("no-creel-ledger"). M4 NOTE: FishCaught is therefore published even when the creel store was missing (the fish was NOT stored): quota consumers must
//    count creel data, never FishCaught events.
//  - FishingRodNetwork.IsPlausible(in CatchAnnouncement, FishingDatabase) is internal static (7.6): kinds 1..4 only; kinds 1..3 vetted with FishingRollValidation.IsSaneCatch.
//  - Tests: FishingHostGuardTests.cs (EditMode: the finite-origin guard and the plausibility vetting), FishingWireTests.cs (wire structs and RPC payload mirrors).
//  - Mini-fix batch after T19 (C 16.3): the UnknownPlayer reply sends Vector3.zero as landing (never the raw client origin); `CatchValidator.OriginWithinSlack`
//    (the ledger's 3 m rule) runs against the host's seen position BEFORE any CastProbe raycast; FishingHost subscribes to `SessionState.SlotTakenOver` and calls
//    `FishingHostLedger.ResetSlot` (7.9).

// Fishing/Debug/FishingDebugSettings.cs - host-side dev overrides; honoured only when Debug.isDebugBuild; reset on SubsystemRegistration
public static class FishingDebugSettings
{
    public static float ForcedBiteWait { get; set; }   // <= 0 off (F6 toggles 0.1)
    public static bool TwitchyHook { get; set; }       // F7
    public static string ForcedSpeciesId { get; set; } // null off (PlayMode fixture)
    public static bool ForceJunk { get; set; }
    public static RollOverrides ToOverrides(ContentRegistry registry);   // starts from RollOverrides.None (ForcedItemIndex -1; ruling B-Q14)
}
```
`BuildInfo.ProtocolVersion = 2` (T17).

### 7.18 T18 - Owner controller (asm FishGame.Fishing, `Fishing/Owner/`)

```csharp
// Authority: owner only. Disabled on the prefab; FishingRodNetwork.OnNetworkSpawn enables it when IsOwner.
// Input -> RodStateMachine (Tick each frame, TickFight from a 50 Hz accumulator, max 5 steps) -> RPCs and cues.
// Requests its InputContext through IPlayerContextSource; PlayerLocomotion writes it.
[DefaultExecutionOrder(-100)]   // after PlayerInputAdapter (-1000), before PlayerLocomotion (0)
public sealed class FishingRodController : MonoBehaviour, IPlayerContextSource
{
    public static FishingRodController Local { get; }
    public static event Action<FishingRodController> LocalChanged;
    public RodStateMachine Machine { get; }       // read-only use by UI/Presentation
    public bool RodOut { get; }
    public bool MarkerVisible { get; } public bool MarkerValid { get; } public Vector3 MarkerPosition { get; }
    public float ChargeNorm { get; }
    public RodStats Rod { get; }
    public Vector3 RodTipPosition { get; }        // world position of the STATIC TipAnchor (BF-3), never the bent TipVisual
    public InputContext RequestedContext { get; } // Free (stowed), RodReady (Idle), RodAiming (Readying..Charging), RodWaiting (Casting, Waiting, terminal beats), RodFighting (Bite, Hooked)
    public bool LocksMovement { get; }            // State is Bite or Hooked
    public event Action<FightEnded> FightEnded;   // owner; one per cast that reached Waiting (telemetry)
}
// Fishing/Owner/FightEnded.cs - telemetry payload (also published on the App EventBus)
public readonly struct FightEnded
{
    public readonly string ProfileId, ItemId, RodId; public readonly int WeightG; public readonly float CastDistance, CastToBite, FightSeconds;   // R67: CastDistance = cast distance FROM THE ROD TIP (fight-log castDist): origin -> adopted landing minus CastTipReach (floored at 0) when a landing was adopted, else Machine.CastDistance
    public readonly FightOutcome Outcome; public readonly EscapeReason Reason; public readonly int NearSnapRecoveries; public readonly float MaxTension;
    public readonly bool Assist; public readonly bool CleanClaimed;   // owner-side claim (maxTension < threshold on a landed fish)
    public readonly bool Pity;                                        // R65: Machine.Roll.Pity of that cast (false without a result)
    public FightEnded(string profileId, string itemId, string rodId, int weightG, float castDistance, float castToBite, float fightSeconds,
                      FightOutcome outcome, EscapeReason reason, int nearSnapRecoveries, float maxTension, bool assist, bool cleanClaimed, bool pity);
}
```
Behaviour the other tasks rely on:
- Equip: `Intent.EquipPressed` toggles `PlayerNetState.OwnerSetTool(Rod/None)` when the context allows (Free/RodReady/RodAiming); stowing with a line out uses `ForceEnd(Stow)`.
- Swim: when `PlayerLocomotion.IsSwimming` turns on, `ForceEnd(Swim)`, stow and remember; restore on leaving water. Teleport/respawn: `PlayerLocomotion.Teleported` -> `ForceEnd(Teleport)`.
- Marker: in Ready/Charging, every frame `CastMath.Landing` from the root, camera yaw and the live distance (R67: the distance is from the rod tip and `CastMath.Landing` adds `CastTipReach`, so the minimum marker is `CastTipReach` + `castMinDist` = 4.9 m from the player); `CastProbe` colours it.
- `NetRodState`: written every frame through `OwnerWriteState` (quantized; the NetworkVariable throttles to 15 Hz). `BobberPos` = landing target while Casting, then the bobber / fish point (`FishPointMath` + water sampling every 0.2 s, holding the last valid water point).
- Pause (`IPauseState.Paused`, solo): `RodFrame.Paused = true`; the fight accumulator does not advance.
- Assist from `settings.fishingAssist`, captured at release by the machine.
- Rumble (ruling BF-2): the continuous rumble is off on every terminal state, the terminal one-shot pulses (Snapped, Escaped, Landed double pulse) play to completion first, and everything is 0 at Idle, on focus loss and on disable. `FishingFeedback` (T23) owns the pulses; the controller itself calls `IInputService.SetRumble(0, 0)` only on entering Idle and in `OnDisable`.
- Ignored events: after each machine call the controller compares `Machine.IgnoredEventCount` and, in debug builds, `Debug.Log`s once per state when it grew (ruling E-Q5).
- Internal helpers (all `internal`, namespace `FishGame.Fishing`, reached by EditMode through the `Fishing/AssemblyInfo.cs` InternalsVisibleTo; tests in `RodControllerHelperTests.cs`): `Fishing/Owner/RodControllerRules.cs` (static: `ContextFor(RodState, rodOut)`, `LocksMovement`, `EquipAllowed(InputContext)`, `IsFinite(Vector3)`, `FiniteOr(candidate, fallback)` - the owner never writes a non-finite `BobberPos`, because `RodStateData.Equals` compares floats with `==` and a NaN would resend every frame); `Fishing/Owner/FightRecorder.cs` (plain C#, per-cast telemetry: `OnCastRequested(clock, assist)`, `OnWaiting(roll, castDistance)` captured BEFORE `ResetCast` wipes the roll, `OnBite`, `OnTerminal(outcome, reason, maxTension, fightSeconds, nearSnap)`, `TryFinish(...)` -> `FightEnded`; a cast that reached Idle with no terminal state is recorded as `Cancelled`; a cast that never reached Waiting produces no `FightEnded`); `Fishing/Owner/FishPointSampler.cs` (`Begin(landing)`, `Tick(...)`: the candidate from `FishPointMath` each frame, a water probe every `FishingConstants.FishPointSampleSeconds`, a non-water candidate holds the last water point, a non-finite candidate returns the last point; the tip is the STATIC `TipAnchor`).
- Despawn: while the controller is being disabled or despawned (`_suppressSends`) it sends nothing and raises no local Recall/Escaped cue (mini-fix batch after T19, C 16.3).
- `FightEnded` is published on the App `EventBus` and as the C# event; `Roll`, `Outcome` and `Pity` of each cast come from `FightRecorder`, not from the machine after Idle (T8 note, 7.8).

### 7.19 T19 - Integration (asm FishGame.Editor + PlayMode tests)
```csharp
// Editor/Builders/Fishing/PlayerFishingRigBuilder.cs - called from PrefabBuilder.BuildPlayer (hook); reuses existing children/components
public static class PlayerFishingRigBuilder
{
    public static void Build(GameObject playerRoot);           // 6.3 rig (Hand, Rod, Seg0..5, TipAnchor, TipVisual, CatchHold, Line, Bobber) + PlayerCreel,
                                                               // FishingRodNetwork, FishingRodController (disabled); meshes Rod_*.asset, Bobber.asset
    public static void AddPresentation(GameObject playerRoot); // T24 owns the body; T23 adds its line (2.6)
}
// Editor/Builders/Fishing/FishingSceneObjects.cs - called from LakeDockBuilder (hook)
public static class FishingSceneObjects { public static void AddTo(Scene scene); }  // root "Fishing" with FishingHost (T27 adds FightTelemetry)
```
PlayMode (`Tests/PlayMode/FishingSoloTests.cs`): fixture per G9. Solo start; `FishingDebugSettings.ForcedBiteWait = 0.1f`, `ForcedSpeciesId = "bluegill"`; owner `ApplyTeleport((0, 0.65, -41), 0)`; `FakeInputService` frames (Equip, hold Secondary 0.3 s, Primary press, hold about 0.15 s, release, then `Reel = true` with counter-steer until Landed). Teardown resets `FishingDebugSettings`. The two movement tests `RodOut_MoveInput_StillWalks` and `RodFighting_MoveInput_DoesNotMovePlayer` also live in `FishingSoloTests.cs`; `PlayerMovementTests.cs` stays T3-only (ruling E-Q3). The `FishingPlayModeDriver` member names are frozen after T19 and T23/T24 tests reuse them (ruling F-Q7). Real surface (internal types in `FishingSoloTests.cs`, assembly `FishGame.Tests.PlayMode`; amended by ruling T19-R1): `internal sealed class FishingRig : IDisposable` holds `Fake, Player, Net, Controller, Locomotion, Creel, Registry, Log, Announced, Order, Machine` and `Subscribe()`; `LogWatch` is a TOP-LEVEL internal type (not a driver member) reached through `rig.Log` (`AssertEmpty(context)`); every driver method takes the rig first: `static class FishingPlayModeDriver { const float DeckTeleportZ = -41f; FakeInputService InstallFakeInput(); IEnumerator Step(FakeInputService fake, PlayerInputFrame frame); void ResetDebugSettings(); IEnumerator Arrange(FishingRig rig, bool teleport = true); IEnumerator EquipAndReady(FishingRig rig); IEnumerator Charge(FishingRig rig, float min = 0.15f); IEnumerator Release(FishingRig rig); IEnumerator ChargeAndRelease(FishingRig rig, float min = 0.15f); IEnumerator WaitForState(FishingRig rig, RodState state, float timeoutSeconds); IEnumerator FightRhythm(FishingRig rig, float timeoutSeconds); float Flat(Vector3 a, Vector3 b); }`. The T19-fix adds a shared `TearDown(FishingRig rig)` helper (disposes the rig, resets `FishingDebugSettings`, then `PlayModeHarness.TearDownApp`) so later fixtures do not copy the teardown order; briefs for T23/T24 must call these signatures as written so they compile. T19-fix also: `PrepareResetScene` reuses an existing `FG_TestReset` via `GetSceneByName` and scans in its own try; optional internal `SpawnPlayerIfMissing` plus an `InternalsVisibleTo` instead of reflection. Rig materials: prefer the new `M_Toon_Base` / `M_Line`, else the M1 fallback; palette slots WoodDark 4, Accent 10, Snow 11 (rulings E-Q4, F-Q8).
**T19-R1 (T19 review, CHANGES REQUIRED; executor: T19-fix after the mini-fix batch, Sonnet impl + Opus re-review):** the rod geometry must make the owner's start line honest against the host slack. (1) The rod local rotation is chosen so that in the OWNER arm pose (`PlayerBody.LateUpdate`: `ArmR = camera * Euler(-90)`) the rod points camera-forward pitched UP about 35 deg; the remote pose (`Euler(-70 - pitch)`) must give a plausible forward-up rod too (derive from the real transforms; test both). (2) `FishingConstants.HookedLineSlack` (5.2) is re-derived by the fix as the measured maximum horizontal forward reach of `TipAnchor` from the player root over camera pitch -60..+60 at that pose, plus 0.5 m, rounded up to 0.1 (value TBD until measured). The host stays conservative: the owner's start line is >= the host's `LineLength0` for every honest cast (proved at the G9 spot and at max cast). (3) `SoloHost_CastBiteFight_Lands` gains the tip assertion `Dot((RodTipPosition - cam.position).normalized, cam.forward) > 0.8` and `tip.y < cam.y + 1.0`. (4) Presentation (T24) must not move `TipAnchor` (7.24). Spec 9.9 carries a note: the slack is derived from the rig's reach.
Delivered (T19, `0c070a0`, test tweak `d2d119a`): `FishingSoloTests.cs` has EIGHT tests: the five above plus the three T16 review carry-overs `PlayerCreel_ServerTryAdd_RefusedWhenFull`, `PlayerCreel_DebugBellSell_EmptiesCreelAndAddsCoins`, `PlayerCreel_MirrorsLedger_AfterRespawn`. The file also holds the internal test helpers `LogWatch`, `FishingRig` and `FishingPlayModeDriver` (member names frozen, F-Q7); `RodFighting_MoveInput_DoesNotMovePlayer` asserts the log watch is empty. `PlayerFishingRigBuilder.cs` (148 lines) and `FishingSceneObjects.cs` (21 lines) are the two new Editor files; generated `Content/Meshes/{Rod_Seg0..5,Bobber}.asset`. One integration fix was made (`PlayModeHarness.cs`, 2.6); no other integration fixes were needed in T1-T18 files. Manual carry-overs: the swimming interact gate check (T16 note) goes to the T28 playtest; the F3 overlay checks (editor and non-development build) go to T26/T30 (C 16.2).

### 7.20 T20 - Toon shading, fish import (asm FishGame.Editor; `Shaders/`)
- `Shaders/FishGameCommon.hlsl`: the 5.3 globals; shared fog and wave helpers (the HLSL wave function mirrors `WaveMath.HeightAt` term for term).
- `Shaders/Toon.shader` (`FishGame/Toon`): spec 10.3 exactly (UnityPerMaterial `_BaseColor, _RampMid, _RampSoft, _UsePalette, _JitterEncoded, _Desaturate, _RimPow, _RimStrength`; passes UniversalForward with the SimpleLit 17.6 keyword set incl. `_CLUSTER_LIGHT_LOOP` and `_ADDITIONAL_LIGHTS`, ShadowCaster, DepthOnly). UnityPerMaterial also holds `_JitterEncoded` (ToggleUI float, default 1; SRP Batcher compatible). Albedo `vertexFactor = lerp(vertexColor.rgb, 2 * vertexColor.rgb, _JitterEncoded)`, then `albedo = (_UsePalette > 0.5 ? _FGPalette[round(uv1.x)] : 1) * vertexFactor * _BaseColor` (BF-7: MeshBuilder stores a half-scale grey so its materials set `_JitterEncoded` 1; `M_Toon_Fish` sets 0 and reads the Blender linear albedo as stored). Material values: `M_Toon_Base`, `M_Body_Slot0..3`, `M_Skin`, `M_Toon_FishFallback` `_JitterEncoded` 1; `M_Toon_Fish` 0. Vertex colour is treated as LINEAR and never converted (one rule for MeshBuilder meshes and the fish models; ruling F-Q3). If the `-nographics` shader-validation check fails, T20 owns a minimal `Tools/unity.ps1` change (graphics-capable mode for the validation run; ruling F-Q4).
```csharp
public static class ShaderValidator       // Editor/Validation/ShaderValidator.cs
{
    public static List<string> Run();      // every FishGame/* shader: ShaderUtil.ShaderHasError / GetShaderMessages (errors fail; warnings logged)
    [MenuItem("FishGame/Validate Shaders")] public static void RunMenu();
}
public static class RenderValidators      // Editor/Validation/RenderValidators.cs; ValidationRunner hook
{
    public static List<string> All();                          // ShaderValidator.Run() + ToonMeshesHaveVertexColours()
    public static List<string> ToonMeshesHaveVertexColours();  // every renderer with a FishGame/Toon material in prefabs and build scenes
}
public static class RenderMaterialLibrary  // Editor/Builders/RenderMaterialLibrary.cs; MaterialLibrary.BuildAll hook
{
    public static void BuildAll();         // T20: M_Toon_Base, M_Toon_Fish, M_Toon_FishFallback; re-points M_Body_Slot0..3 (_BaseColor = SlotPalette) and M_Skin to Toon
    public static Material Get(string name);
}
public sealed class FishModelPostprocessor : AssetPostprocessor   // Editor/Builders/Render/FishModelPostprocessor.cs
{
    // OnPreprocessModel for Assets/_Project/Content/Models/*.fbx: materialImportMode None, importNormals Import, importAnimation false,
    // importCameras/Lights false, globalScale 1, useFileScale true, isReadable false, meshCompression Off. Never assigns materials:
    // the reveal renderer uses M_Toon_Fish.
    [MenuItem("FishGame/Dev/Reimport Fish Models")] public static void ReimportAll();
}
```
- `PrefabBuilder` body meshes (head, nose, torso, arms, legs) become `MeshBuilder` meshes saved as `Content/Meshes/Body_*.asset` (same dimensions as M1 contract 6.3), so they carry vertex colours.

### 7.21 T21 - Water, line, sky, rendering setup
- `Shaders/Water.shader` (`FishGame/Water`, spec 10.4: vertex-displaced grid via `_Waves`/`_WaterTime` (through `FG_WaveHeight` only), smooth per-pixel normal from `FG_WaveHeight` finite differences plus drifting ripples (R73; no flat facet normal), vertical depth fade `1 - exp(-depth / _DepthRange)` on `_ShallowColor`/`_DeepColor`, shallow bed cover and caustics, crest tint, fresnel, sparkle glints, foam band `_FoamWidth` plus `_Ripples`, Transparent, ZWrite Off, one UniversalForward pass, fog at `_FogAmount`; full description in spec 10.4). `Shaders/Line.shader` (`FishGame/Line`, unlit; `_Tension` drives warm/warn colour and the moving dash `frac(uv.x * _DashCount - _Time.y * _DashSpeed)`; the width pulse is in `LineView`). `Shaders/GradientSky.shader` (`FishGame/GradientSky`, Background queue, ZWrite Off, `_SkyTop/_SkyHorizon/_SkyGround` and a sun disc).
```csharp
public static class RenderingSetupBuilder  // Editor/Builders/Render/RenderingSetupBuilder.cs; ContentBuilder hook
{
    public static void Build();                // volume profiles (6.2); RenderMaterialLibrary additions (water, line, sky, particle, marker)
    public static void ApplySceneLighting();   // RenderSettings: skybox M_Sky, fog ExponentialSquared on, density 0.008, ambient mode; scene builders call it
}
```
- `PrefabBuilder`: the PlayerFP camera gets `UniversalAdditionalCameraData.renderPostProcessing = true`.

Carry-forward (C 16.2): the Water shader must treat ANY non-finite wave field (NaN or +/-Inf in direction, amplitude, wavelength or speed) as a zero term, exactly like `WaveMath`, or the CPU and GPU heights diverge. `ArtPaletteDef.PaletteVectors(season)` allocates a `Vector4[16]`: the palette push happens on enable and validate only, never per frame (T22 `PaletteGlobals` reuses one cached array).

### 7.22 T22 - World presentation (asm FishGame.Presentation, `Presentation/World/`)
```csharp
// Authority: local only (every peer); never writes gameplay state.
public sealed class PaletteGlobals : MonoBehaviour   // pushes _FGPalette (ArtPaletteDef.m2Season, length 16) and the Day phase globals
{ public void Apply(); }                              // (_ToonShade, _ToonAmbient, sky colours, fog colour/density) on enable
public sealed class WaterTimeDriver : MonoBehaviour  // WaterSurface.Configure(WaterSettings.ToWaveSet()); SetTimeSource(ServerTime.Time while a
{ }                                                   // session is listening, else Time.timeSinceLevelLoad), float-cast so C# and HLSL share one value (C-Q4); pushes _Waves (4), _WaterTime; ResetToFlat on destroy
public sealed class RippleField : MonoBehaviour      // fixed Vector4[8] (x, z, spawnWaterTime, strength) -> _Ripples; oldest slot recycled
{
    public static RippleField Current { get; }
    public void Spawn(float x, float z, float strength);   // strengths from RippleMath constants
}
// Editor/Builders/Render/PresentationSceneObjects.cs (asm Editor) - LakeDockBuilder hook
public static class PresentationSceneObjects { public static void AddTo(Scene scene); }  // root "Presentation" with the three components
```

### 7.23 T23 - Feedback and catch reveal (asm FishGame.Presentation, `Presentation/Feedback/`)
```csharp
// Authority: local only. On PlayerFP; reads its FishingRodNetwork (Cue, ViewState, Announced).
public sealed class FishingFeedback : MonoBehaviour
{
    // Every peer: RodCueAudio sounds at the bobber (spatial; the owner also hears Bite 2D), ripples, duck on stingers, loops
    //   (Line_Creak pitch/volume from TensionCues while Hooked, Dive_Thrum during dives, Reel_Click at ReelClickRate while reeling).
    // Owner only: camera trauma (PlayerCamera.AddTrauma), FOV kicks (AddFovKick: cast +2 deg over 0.15 s, catch -3 deg),
    //   rumble (IInputService.SetRumble from TensionCues and cue pulses; continuous part 0 on every terminal state, the terminal pulses play out first; all 0 at Idle, focus loss and disable; BF-2),
    //   captions (EventBus CaptionRequested "cap.bite", "cap.straining", "cap.snapped", "cap.escaped" when settings.captions).
    // Snap: a grey silhouette jump at BobberPos scaled by sizeClass, every peer, drawn with the asset material M_Toon_FishFallback (grey tint baked
    //   into FishFallbackMesh.Silhouette() vertex colours; no material instance; BF-7). Camera motion only (trauma, FOV kicks, rumble) x settings.cameraMotion;
    //   the silhouette jump, splash and ripple are world feedback seen by everyone and are NOT scaled or hidden by cameraMotion (spec 9.11; T23 review).
    //   FlagGrace reaches remotes through the 15 Hz NetRodState throttle: grace touches shorter than ~67 ms may not crack on remotes (same as burst flags; accepted).
    // Grace: `Line_Crack` one-shot (rod tip; owner 2D) on the rising edge of `RodWireQuant.FlagGrace` (bit 32, set by the owner's PublishView while in Grace) on every peer (T23 review).
}
public sealed class CatchRevealView : MonoBehaviour
{
    // Announced (every peer): Landed or ReleasedCreelFull -> fish held up at CatchHold for 1.5 s: the species model with M_Toon_Fish,
    //   scaled so its +Z bounds length = LengthMm / 1000 m; junk or a null model -> FishFallbackMesh with M_Toon_FishFallback (BF-7). Trophy: bigger burst.
    //   ReleasedCreelFull then drops it with a splash. Lost and PityCast: no reveal (the HUD toast covers them). Clean bonus: sparkle.
}
public static class FishFallbackMesh       // MeshBuilder fish from BodyShape + colorA/colorB written LINEAR (cached per item; the same vertex-colour rule as the fish models, F-Q3; drawn with M_Toon_FishFallback, `_JitterEncoded` 1, BF-7); junk: box boot or cylinder can
{ public static Mesh Get(ContentRegistry registry, bool isJunk, ushort itemIndex); }
```
`PlayerFishingRigBuilder.AddPresentation` gets a block of up to 12 lines from T23: GetOrAdd of both components plus their serialized material references (ruling F-Q5, 2.6).

Carry-forward (C 16.2): every loop `FishingFeedback` starts passes a `follow` transform, 2D (owner) loops included, and all loops are stopped in `OnDisable` (a 2D loop with no follow would play forever after a scene change); no cues are played while the rod is despawning or disabled (the controller already suppresses its own local Recall/Escaped cues, 7.18).

### 7.24 T24 - Rod presentation (asm FishGame.Presentation, `Presentation/Rod/`)
All read `FishingRodNetwork.ViewState` (and `FishingRodController.Local` for the owner's marker); none write gameplay state.
```csharp
public sealed class RodPresenter : MonoBehaviour   // rod shown iff Player.Tool == Rod; 6 segments bend toward the line by bend * weight[i],
{ public Transform Tip { get; } }                  //   bend = TensionCues.RodBend01; nibble wobble 4 deg; shake in Warn. Tip = the BENT TipVisual (BF-3);
                                                   //   RodPresenter bends TipVisual only and never touches TipAnchor (gameplay reads TipAnchor)
public sealed class LineView : MonoBehaviour       // quadratic Bezier TipVisual (RodPresenter.Tip) -> bobber, sag (1 - tension) * 0.15 * length, 24 points, M_Line _Tension;
{ }                                                //   width pulse in Warn; hidden while no line is out
public sealed class BobberView : MonoBehaviour     // cast arc on a CHANGE into Casting (CastMath.ArcPoint, FlightTime, PitchApexMult; remotes use NetPitchQ);
{ public Vector3 Position { get; } }               //   wave bob WaterSurface.HeightAt; nibble dip, bite plunge, recall bounce; the first value after spawn snaps
public sealed class SplashFx : MonoBehaviour       // URP Particles/Unlit (M_Particle) bursts: landing, nibble, bite, sideways side-run column, snap, catch
{ }
public sealed class CastMarkerView : MonoBehaviour // owner only, in world at MarkerPosition: valid = solid ring + centre dot; invalid = dashed grey ring
{ }                                                //   + cross (shape, not hue only); slides with the charge; flashes on InvalidRelease
```

Carry-forward (C 16.2, ruling T19-R1): `RodPresenter` never moves or re-parents `TipAnchor` (only `TipVisual` and the `Seg*` bend); the rod rest pose comes from the T19-fix in `PlayerFishingRigBuilder`, and T24 adds bend on top of it. T24 PlayMode tests use the driver exactly as in 7.19.

### 7.25 T25 - Fishing HUD (asm FishGame.UI, `UI/Fishing/`)
```csharp
internal sealed class FishingHudPresenter : IHudExtension   // created by the UiFlowController hook (2.6)
{
    // Charge ring around the crosshair (FishingRodController.Local.ChargeNorm, Charging only); "Click!" when Bite && HookWindow > 0;
    // captions bottom-centre (CaptionRequested, settings.captions); line gauge (Arc: 90 deg arc below the crosshair with a tick at 0.8;
    //   Bar: thin bar) for the local line only; creel count/kg bottom-right (local PlayerCreel; warn style at CreelRules.IsWarn,
    //   "Full - sell!" at IsFull); catch toasts from CatchAnnounced (crew toasts at most 1 per 3 s per player).
}
```
- `UiStrings` (edit): `Format(string key, params string[] args)` overload; keys `cap.bite` "Bite!", `cap.straining` "Line straining", `cap.snapped` "Snapped", `cap.escaped` "Got away", `hud.click` "Click!", `hud.creel` "{0}/{1}  {2} kg", `hud.creel_full` "Full - sell!", `toast.catch` "{0} caught {1} kg {2} (${3})", `toast.lost` "It got away!" (generic: covers snap and escape; ruling G-Q3), `toast.creel_full` "Creel full! {0} released", `toast.clean` "Clean catch!", settings labels `settings.line_gauge`, `settings.captions`, `settings.assist` (M1 `settings.*` prefix, not `set.*`; ruling G-Q2). Additive keys: `toast.raw` "{0}" (passthrough), `toast.junk` "{0} fished up {1} (${2})", `toast.pity` "{0} has a lucky line!" (R65), `settings.gauge_off` "Off", `settings.gauge_arc` "Arc", `settings.gauge_bar` "Bar". T25 formats toast text itself and publishes `ToastRequested("toast.raw", text, 4f)` (ruling G-Q1). An `AnnounceKind.PityCast` announcement shows `toast.pity` (name) for everyone. ASCII only.
- `SettingsPresenter` rows: Line gauge (Off/Arc/Bar), Captions, Fishing assist (turning assist on while the gauge is Off sets the gauge to Arc, every time; ruling G-Q4).

Carry-forward (C 16.2): extract the pure helper `OnAssistChanged(data, old, new)` (the G-Q4 rule: assist turned on while the gauge is Off sets Arc, every time) with an EditMode test; the creel HUD reads `PlayerCreel.Count` at spawn (the initial NetworkList sync raises no `Changed`) and must not show a species ahead of `CatchAnnounced` (host-side creel `Changed` and `FishCaught` fire one frame BEFORE `CatchAnnounced`: show count and weight only, or wait for the announce); `IHudExtension.Bind` implementations must never throw (the bind loop has no try/catch: a throw blocks the whole UI) and extension-created buttons play their own `Ui_Click` (the click-sound loop runs before the bind loop).

### 7.26 T26 - Dev overlay (asm FishGame.UI)
```csharp
internal sealed class DevOverlayPresenter : IHudExtension   // only when Debug.isDebugBuild; F3 toggles
{
    // Shows state, phase, tension, stamina, line length, timers, last roll (owner); loadout (rod, bait, count); host ledger counters and RunSeed.
    // RunSeed is displayed only; no key changes it in M2 (ruling G-Q5). The ROLL line also shows the roll's Pity flag.
    // IDevInput keys: F4 SelectRodRpc(next), F5 SelectBaitRpc(next), F6 FishingDebugSettings.ForcedBiteWait 0.1 <-> 0 (host),
    // F7 TwitchyHook (host), F9 RippleField.Spawn at the crosshair water point (local).
}
```

Carry-forward (C 16.2): F4/F5 rod and bait selection (`SelectRodRpc`/`SelectBaitRpc`) can race the owner's Readying/Charging: on a valid `CastResult` the controller adopts the host's landing x/z (keeping `FlatHeight`) or at least `LogOnce`s when they differ by more than 0.05 m. Manual check: F3 in the editor shows the overlay; in a non-development player build it is a no-op.

### 7.27 T27 - Fight telemetry
```csharp
public struct FightLogRow   // Core/Fishing/FightLogRow.cs
{
    public string Profile, Species, Rod; public int WeightG; public float CastDist, CastToBite, FightSec, MaxTension;
    public FightOutcome Outcome; public EscapeReason Reason; public int NearSnapRecoveries; public bool Assist, Clean, Pity;   // Pity: R65, last column
}
public static class FightLogCsv { public const string Header = "..."; /* 5.3 */ public static string Format(in FightLogRow row); }  // invariant culture, 3 decimals
// Fishing/Owner/FightTelemetry.cs - Authority: local only. Scene object on "Fishing". Debug.isDebugBuild only.
// Subscribes FishingRodController.LocalChanged / FightEnded; appends one row per cast that reached Waiting (cancels and misses included,
// so the fun gate's cancel rate is computable); writes the header when the file is new; an IO error logs once as a warning.
public sealed class FightTelemetry : MonoBehaviour { public string FilePath { get; } }
```

Carry-forward (C 16.2): `FightEnded` arrives from `FightRecorder` (7.18), which already captures the roll on entering Waiting and records `Cancelled` when a cast drains to Idle with no terminal state; T27 only formats and appends. `FishCaught` is NOT the source of any row.

### 7.28 T28 - Lake dock rebuild (asm FishGame.Editor)
`LakeDockBuilder`, `LakeDockLayout` and `LakeDockMeshes` are rebuilt per 6.4: MeshBuilder props (dock planks, posts, ladders, rocks, trees, reeds) on `M_Toon_Base` with palette slots; water grid on `M_Water_Surface`; M1 colliders, spawns, bell anchor, lakebed and shore numbers unchanged (M1 contract 6.5, D34) except the ladder exits and the ramp exit, which R70 changed on purpose (M2-D29, M2-D30, 6.4, 16.1); `RenderingSetupBuilder.ApplySceneLighting()`; both volumes; both hooks kept. Seeded and byte-identical across rebuilds. Frame time, SRP batch count and the water pass GPU time go in the acceptance doc (spec 10.7).

Carry-forward (C 16.2): any M1 swim or water-height test broken by the waves is fixed here under the integration-fix rule (T22 reports it, ruling F-Q12). Playtest line: check the swimming interact gate (T16 added the `InteractLive` gate to `PlayerInteractor`; confirm what a swimming player can and cannot interact with, and with the rod out).

### 7.29 T29 - Species models: import and verify the staged Blender fish (controller)
Controller work, not a subagent task. The five models already exist in the repo (commit `8f327e5`): `Art/Export/Fish_{Bluegill,Perch,Carp,Trout,Catfish}.fbx`, source `Art/Blender/fishgame_m2_species.blend`; each has ONE submesh, one material slot, per-face LINEAR vertex colours (`colors_type='LINEAR'`), 222-262 triangles, exported -Z forward / Y up / apply transform (nose +Z in Unity). T29 copies them to `Content/Models/` (the new perch replaces the spike `Fish_Perch.fbx`, keeping its `.meta`), lets `FishModelPostprocessor` import them, and verifies subMeshCount 1, vertex colours, triangle count below 400 and the nose direction; it does not model from scratch (Blender MCP only for a fix). Then `build`: `FishingContentBuilder` fills the null `model` refs (rule in 6.2); `FishingValidators.SpeciesModels` (incl. `subMeshCount == 1`) and `RenderValidators` pass. A Sonnet reviewer checks import settings, LFS and validators; the user judges the look. The Toon shader and `FishFallbackMesh` treat vertex colours as LINEAR, so the side-by-side render only confirms the one rule (ruling F-Q3).

Carry-forward (C 16.2): T29 also lands the T14 follow-up minors (small commit, exercised by the model fill): `FishingContentBuilder.Rooted()` throws when a path is not under `RealContentRoot`; a test covers the `ModelsFolder` re-root; the `Build_` tests are renamed `BuildAt_`; the fixture `OneTimeSetUp` runs `DeleteTempFolders` on failure (try/catch); the `ValidationTests` temp prefab moves under `Tests/__Temp/`. The staged models in `Art/Export/` are imported, not rebuilt.

### 7.30 T30 - Acceptance
`docs/superpowers/plans/2026-10-11-m2-acceptance.md`: 17.2 acceptance criteria 1-11 as a manual checklist; the 17.2 MPPM list; the 9.13 edge-case walk (criterion 5); the fun-gate log template (30+ fights, CSV columns, the 17.2 thresholds, clean-catch rate, a per-profile blind tell-apart sheet); perf numbers; open tuning items. `README.md` gains "Fishing dev keys" and "Fight log". Final `check` green. The checklist carries T15's `TuningItems` (ruling D-Q6), the M2 perf numbers as the first baseline (G-Q8), and a USER line: "User playtests the bad-luck nudge and decides keep / tune / remove" (R65).
Added 2026-10-12 (C 16.2): **criterion 7 wording** - "the host rejects forged and out-of-order messages (stale/duplicate castIds, impossible hook or landing timing, non-finite or far origins, bait it does not have, creel add over the cap); position checks are bounded by the owner-written transform (friends-only trust, spec 1.2); a bare-hook reroll by cancelling is accepted design". **Fun gate:** near-snap recovery >= 40% (spec 17.2) is a HUMAN metric read from the fight logs (bots only print it; bulldog and thrasher Rhythm recovery were 3% and 6% in `5a652bd`); tuning hints if low: lower `tuning.relaxWhileReeling` toward 0.15 (it sits at 0.20, the TOP of the 9.12 range, so there is no headroom upward), or raise bulldog/thrasher `pullStrength` or lower their calm minimum rather than touching relax; note the Twig thrasher trophy (weightT 0.9) median of about 93 s and bulldog Rhythm median 37.4 s (floor 35 s). **Manual list:** F3 in the editor shows the overlay; in a non-development player build F3 is a no-op. **M4 handoff:** `FishCaught` is published even when the creel store was missing (the fish was not stored): quota consumers use creel data, not `FishCaught` counts. **M5 handoff:** knockback (`PlayerLocomotion.AddKnockback`) must end the cast, or the host walk bound (`WalkBoundedLineLength0`, sprint speed) must add the maximum knockback; the Staggered router row must zero Move.

---

## 8. Event, RPC and NetworkVariable catalogue (additions to M1 contract section 8)

### 8.1 Events

| Name | Kind | Defined in (owner) | Raised by | Consumed by |
|---|---|---|---|---|
| `CaptionRequested` | EventBus message | Runtime (T11) | FishingFeedback (T23) | FishingHudPresenter (T25) |
| `CatchAnnounced` | EventBus message (App bus); kinds Landed, Lost, ReleasedCreelFull and the toast-only PityCast (R65) | Fishing (T17) | FishingRodNetwork, every peer | FishingHudPresenter (T25) |
| `FishCaught` | EventBus message (host only, iff Outcome == Landed && AddToCreel) | Fishing (T17) | FishingHost | none in M2 (M3 journal, M4 run stats) |
| `FightEnded` | C# event + EventBus message | Fishing (T18) | FishingRodController | FightTelemetry (T27) |
| `FishingRodNetwork.Cue` | C# event (instance) | Fishing (T17) | owner: controller via `RaiseLocalCue`; remotes: RodEventRpc + NetRodState transitions | FishingFeedback (T23), RodPresenter/BobberView/SplashFx (T24) |
| `FishingRodNetwork.Announced` / `AnyAnnounced` | C# events | Fishing (T17) | CatchAnnouncedRpc | CatchRevealView (T23) |
| `FishingRodNetwork.CastResultReceived` / `CommitReceived` | C# events (owner) | Fishing (T17) | CastResultRpc / CommitRpc | FishingRodController (T18) |
| `FishingRodNetwork.Spawned` / `Despawned` | static C# events | Fishing (T17) | OnNetworkSpawn / Despawn | FishingHost (T17), dev overlay (T26) |
| `FishingRodController.LocalChanged` | static C# event | Fishing (T18) | controller enable / disable | HUD (T25), dev overlay (T26), telemetry (T27) |
| `PlayerNetState.ToolChanged` | C# event | Gameplay (T16) | NetTool change | FishingRodController (T18), RodPresenter (T24) |
| `PlayerNetState.ServerInitialized` | C# event (server) | Gameplay (T16) | ServerInit | PlayerCreel (T16), FishingRodNetwork (T17) |
| `PlayerCreel.Changed` | C# event | Gameplay (T16) | NetCreel change | FishingHudPresenter (T25) |
| `PlayerLocomotion.Teleported` | C# event (owner) | Gameplay (T16) | ApplyTeleport | FishingRodController (T18) |
| `INetworkSession.StateChanged` / `Ended` | M1 events, now raised per subscriber (`EventIsolation`) | Runtime (T1) | Local / Ugs / Composite | unchanged |

### 8.2 RPCs (all on `FishingRodNetwork`; every attribute sets `InvokePermission` explicitly)

| RPC | `SendTo` / `InvokePermission` | Parameters | Purpose |
|---|---|---|---|
| `CastRequestRpc` | Server / Owner | `uint castId, Vector3 origin, float yawDeg, float chargeNorm, byte baitIndex, bool assist, RpcParams p = default` | Request a cast (origin = owner root position) |
| `CastResultRpc` | Owner / Server, `DeferLocal = true` | `uint castId, bool valid, byte reason, Vector3 landing, FishingRollNet roll` | Accept or reject; roll to the owner only |
| `CancelRpc` | Server / Owner | `uint castId, RpcParams p = default` | Refund in Waiting; recall; force end |
| `HookedRpc` | Server / Owner | `uint castId, RpcParams p = default` | Host mirror to Hooked |
| `ResultRpc` | Server / Owner | `uint castId, byte outcome, byte reason, float maxTension, float fightSeconds, ushort nearSnapRecoveries, RpcParams p = default` | Outcome + fight stats (clean-catch claim, 13 M2-D16) |
| `CommitRpc` | Owner / Server, `DeferLocal = true` | `uint castId, bool accepted, byte finalOutcome, byte finalReason` | Unlocks the return to Idle (finalReason added, 13 M2-D7) |
| `CatchAnnouncedRpc` | ClientsAndHost / Server, `DeferLocal = true` (ruling E-Q8) | `byte catcherSlot, CatchResultNet catchResult, byte announceKind, byte flags` | Reveal and toast (Landed, Lost, ReleasedCreelFull) and the toast-only PityCast (R65; `catchResult` is default); `flags` bit `RodWireQuant.AnnounceFlagClean` = clean bonus |
| `RodEventRpc` | NotMe / Owner (reliable) | `byte eventType, byte sizeClass` | One-shot remote cues (`RodEventType`) |
| `SelectBaitRpc` | Server / Owner | `byte baitIndex, RpcParams p = default` | Debug bait selection (refused while a cast is active) |
| `SelectRodRpc` | Server / Owner | `ushort rodIndex, RpcParams p = default` | Debug rod selection (refused while a cast is active) |

Server handlers forward to `FishingHost`. The owner drops a `CastResultRpc` whose roll fails `FishingRollNet.TryToCore` with one warning, raises `CastResultReceived(valid: false)` immediately and sends `CancelRpc` once (ruling E-Q7; this handler is the sole sender, ruling BF-5). Because `CommitRpc` and `CatchAnnouncedRpc` both defer local invocation (host-owned rod) and are sent in that order, commit-then-announce holds on every peer, the host included.

### 8.3 NetworkVariables

| Field | Type | Read / Write | Class | Owner |
|---|---|---|---|---|
| `NetTool` | `byte` (`ToolKind`) | Everyone / Owner | PlayerNetState | T16 |
| `NetCreel` | `NetworkList<CreelEntry>` | Everyone / Server | PlayerCreel | T16 |
| `NetDebugCoins` | `int` | Everyone / Server | PlayerCreel | T16 |
| `NetRodState` | `RodStateData` | Everyone / Owner (traits 1 / RodStateHz) | FishingRodNetwork | T17 |
| `NetSelectedBait` | `byte` | Everyone / Server | FishingRodNetwork | T17 |
| `NetSelectedBaitCount` | `ushort` (0xFFFF unlimited) | Everyone / Server | FishingRodNetwork | T17 |
| `NetRodIndex` | `ushort` | Everyone / Server | FishingRodNetwork | T17 |

---

## 9. Runtime flows

**Equip and ready.** Free: `1` -> controller `OwnerSetTool(Rod)` -> `NetTool` -> the rod shows everywhere; requested context RodReady. Hold RMB 0.25 s -> Ready (marker; RodAiming: x0.6 speed, no sprint).

**Cast (owner, then host; solo is identical with the deferred reply).**
1. Owner: Primary released in Charging with `chargeNorm >= 0.1` and a valid marker -> `++CastId`, cue CastRelease FIRST, then the `SendCastRequest` effect (the machine queues them in that order; T8 decision 9) -> `CastRequestRpc(castId, rootPos, cameraYaw, chargeNorm, NetSelectedBait, assist)`; `NetRodState {Casting, BobberPos = landing}`; the bobber flies at once.
2. Host (`FishingHost.HandleCastRequest`): `Ledger.TryComputeLanding` (host rod) -> `CastProbe.Probe(landing)` -> `Ledger.RequestCast(clientId, slot, req, view, FishingDebugSettings.ToOverrides(registry))`. Valid: bait consumed, `HostCastCounter++`, roll from `Rng.Stream(runSeed, "catch", counter)`, ActiveCast; mirror `NetSelectedBaitCount`. Reply `CastResultRpc` (DeferLocal: in solo it runs on the next tick, never inside `CastRequestRpc`'s stack). A Pity accept (R65) also sends `CatchAnnouncedRpc(slot, default, PityCast, 0)` so every peer shows the lucky-line toast.
3. Owner: `CastResultReceived` -> `TryToCore` -> `Machine.OnCastResult`. Invalid -> recall to Idle, no bait used. Valid -> Waiting once the flight is over; the bite timer starts at `max(landing, result)`.

**Wait, nibble, bite, hook.** The bite timer is the owner's. Nibbles: cue + `RodEventRpc(Nibble)`. At `BiteWait`: Bite cue + `RodEventRpc(Bite)`; context RodFighting (movement locked). Auto-hook after `HookBeat` (or Primary within `HookWindow`) -> `HookedRpc(castId)` + `RodEventRpc(HookSet)` + `FightSim` from `FightSeed`, start line = `TipToBobber`. Host `Ledger.Hooked` (too early -> commit Escaped(Invalid)); `LineLength0` from the host's view of the player.

**Fight and result.** Owner: 50 Hz `TickFight`; `NetRodState` carries phase flags, lateral, tension and the fish point. End -> `ResultRpc(castId, outcome, reason, maxTension, fightSeconds, recoveries)` (+ `RodEventRpc(Snapped|Escaped)` on a loss). Host `Ledger.Result`: spec 9.9 validation (Landed floor on the host clock, host rod or junk speed), clean-catch claim (only ever downgrades), creel check -> `CommitRpc` -> creel add (`PlayerCreel.ServerTryAdd`, iff `AddToCreel`) -> `CatchAnnouncedRpc` (Landed / Lost / ReleasedCreelFull, iff `Announce != None`) -> `FishCaught` (iff Landed and added). Every FIRST resolution commits and announces per its decision fields; only orphan repeats are silent (ruling E-Q6). Owner: 1.5 s beat, then Idle on the commit (or after 3 s with a warning).

**Cancel and recall.** Waiting + RMB, line past `maxLineOut` (R67: measured from the rod tip, root-to-bobber minus `CastTipReach`), teleport, stow, swim -> `CancelRpc`; the host refunds (Waiting only) and commits. Unsolicited commits (stuck-Waiting refund, `MaxFightTime`) arrive in Waiting/Bite/Hooked and run the 1.5 s beat with no `ResultRpc`.

**Orphans.** A Hooked, Result or Cancel for a cleared cast -> immediate `CommitRpc(castId, false, stored outcome, stored reason)`.

**Remote view and late join.** Remotes draw from `NetRodState`, `NetTool` and `NetRodIndex` only (all synced on spawn). The first value snaps (no arc replay). Species stay hidden until `CatchAnnouncedRpc`; announcements are not replayed to late joiners. Server init for a joiner runs from `PlayerNetState.ServerInitialized` on either `PlayerSpawner` entry: `FishingHost.OnPlayerReady` (EnsureSlot, loadout NetVars) and `PlayerCreel` (mirrors the slot's creel, which survived any disconnect).

**Disconnect.** Host `ClientDisconnected` and server-side `FishingRodNetwork.OnNetworkDespawn` -> `Ledger.Disconnect(clientId)` (no commit, no announce). Bait, rod and creel stay with the slot. The rod vanishes with the player object.

**Pause (solo).** `IPauseState.Paused`: the owner's rod tick, bite timer and fight accumulator stop; `FishingHost` passes `paused` to `Ledger.Tick`, so the ledger clock stops too.

---

## 10. Test inventory (exact names; owners)

EditMode files live in `Assets/_Project/Tests/EditMode/` (namespace `FishGame.Tests.EditMode`), one class per file named `<Subject>Tests`; tuning bots in `Tests/EditMode/Tuning/`. Names follow `Subject_Condition_Expected`. Listed tests are required; others may be added. M1 tests keep passing, except the one rename noted for T10.

### 10.1 Carry-forwards and Core

| Owner | File | Required tests |
|---|---|---|
| T1 | `Pcg32Tests.cs` (add) | `Pcg32_RangeFloat_NeverReturnsMax` (MapUnitToRange(0.99999994f, 1, 2) < 2), `Pcg32_RangeFloat_MaxNotAboveMin_Throws`, `Pcg32_RangeFloat_DrawCountUnchanged` |
| T1 | `LogOnceTests.cs` | `LogOnce_SameKey_LogsOnce`, `LogOnce_Reset_AllowsAgain` |
| T1 | `EventIsolationTests.cs` | `EventIsolation_ThrowingSubscriber_OthersStillRun` (expects one logged exception), `EventIsolation_NullEvent_IsNoOp` |
| T1 | `AudioBusTests.cs` | `AudioBus_NotReady_WarnsOncePerId` |
| T3 | `SoloSessionTests.cs` (PlayMode, add) | `Solo_LeaveRightAfterConnected_EndsOnMenu` |
| T4 | `ValidationTests.cs` (add) | `Validation_MeshReferences_Pass`, `Validation_MissingScripts_Pass`, `Validation_BootstrapReferences_Pass` |
| T5 | `CastMathTests.cs` | `CastMath_ChargeNorm_PingPongAtSampleTimes` (0/0.5/1/1.5/2 s -> 0/0.5/1/0.5/0), `CastMath_ChargeNorm_NoPingPong_Clamps`, `CastMath_Distance_MonotonicAndWithinHalfMeterOfFormula` (charge 0, 0.25, 0.5, 0.75, 1), `CastMath_FlightTimeAndApex_MatchFormulas`, `CastMath_Apex_NeverBelowPointSixMeters`, `CastMath_PitchApexMult_ClampedPointSevenToOnePointThree`, `CastMath_Landing_UsesYawOnly`, `CastMath_IsValidLanding_RequiresWaterFirstAndMinDepth` |
| T5 | `FishPointMathTests.cs` | `FishPointMath_PositiveLateral_IsRightOfCastLine`, `FishPointMath_LateralOne_IsAtMaxAngle` |
| T5 | `RodWireQuantTests.cs` | `RodWireQuant_LateralAndTension_RoundTripWithinOneStep`, `RodWireQuant_OutOfRange_Clamps` |
| T5 | `FishingDatabaseTests.cs` | `FishingDatabase_BadProfileIndex_Throws`, `FishingDatabase_NoUnlimitedBait_Throws`, `FishingDatabase_UnlimitedBaitIndex_IsBareHook` |
| T6 | `BitePlannerTests.cs` | `BitePlanner_TenThousandSamples_WithinBoundsAndFloor`, `BitePlanner_Means_FollowBaitSpeciesAndDepthMultipliers`, `BitePlanner_Nibbles_OrderedInsideWaitAndClearOfBite`, `BitePlanner_ShortWait_ForcesZeroNibbles`, `BitePlanner_SameSeed_SamePlan` |
| T6 | `CatchMathTests.cs` | `CatchMath_WeightDistribution_MeanAndTrophyRate` (about 4.7% Trophy, 16% Good at skew 2.2), `CatchMath_Value_AwayFromZeroAndMinimumOne`, `CatchMath_LengthMm_FollowsCubeRoot`, `CatchMath_SizeClass_FromWeightThirds`, `CatchMath_UpgradeOneTier_NormalGoodTrophyCapped`, `CatchMath_WithCleanBonus_RecomputesValueKeepsWeight`, `CatchMath_WithCleanBonus_JunkUnchanged` |
| T6 | `PlaceholderRollerTests.cs` | `PlaceholderRoller_TenThousandRolls_FrequenciesWithinTwoPercentOfWeights`, `PlaceholderRoller_MinDepthAndMinBaitRating_Gate`, `PlaceholderRoller_BareHookShallow_OnlySteadyBulldogOrJunk`, `PlaceholderRoller_NothingEligible_ReturnsJunk`, `PlaceholderRoller_FightSeed_NeverZero`, `PlaceholderRoller_Overrides_ForceBiteSpeciesJunkTwitchy`, `PlaceholderRoller_SameSeed_SameRoll` |
| T6 | `FishingRollValidationTests.cs` | `FishingRollValidation_SaneRoll_Passes`, `FishingRollValidation_OutOfRangeFields_Rejected` (index, weight, quality, seed 0, NaN wait, nibble count) |
| T7 | `FightSimTests.cs` | `FightSim_FirstPhase_IsCalmLeadIn`, `FightSim_RunnerReelNoSteerSideRun_SnapsBetween2p5And3p2Seconds`, `FightSim_RunnerFullCounterFourSecondRun_NoSnapTensionPointFiveToPointSeven`, `FightSim_SteadyReelNoSteerRun_StaysBelowPointThreeTensionAndPointSixLateral`, `FightSim_RestFromPointNine_BelowPointThreeAfterOnePointFiveSeconds`, `FightSim_PositiveBurstDirWithLeftSteer_ReducesLateralAndDrainsStamina`, `FightSim_Dive_DrainsStaminaOnlyWhileNotReeling`, `FightSim_Exhausted_StopsNewBursts`, `FightSim_Pinned_EscapesAtFiveSeconds`, `FightSim_NoReel_EscapesThrewHookAtTwelveSeconds`, `FightSim_Spooled_ClampsLineAndAddsStrain`, `FightSim_TensionTouchesOneShorterThanGrace_DoesNotSnap`, `FightSim_LineAtLandDistanceWhileReeling_Lands`, `FightSim_NeverReeling_EndsBySlackEscape`, `FightSim_SameSeedAndInputs_IdenticalState`, `FightSim_Assist_UsesLongGraceAndLowerStrain`, `FightSim_NearSnapRecovery_Counted`, `FightSim_Junk_DragsAtJunkSpeedWithoutTension` |
| T7 | `FightMotionTests.cs` (forced phases; motion parameters) | `FightMotion_ZeroParameters_MatchBaseModel`, `FightMotion_SteadyBurstRamp_LateralPushRampsOverRampTime`, `FightMotion_RunnerBurstRamp_LineOutRateRampsUp`, `FightMotion_BulldogCalmDrift_LineCreepsOutDuringCalm`, `FightMotion_BulldogCalmDrift_StopsWhenExhausted`, `FightMotion_DarterMicroBursts_FlipRateInRangeAndGapRespected`, `FightMotion_ThrasherJitter_LateralSpeedVariesPerBurstWithinBounds`, `FightMotion_Pity_SnapsLaterThanNormalSameSeed` (R65), `FightMotion_AllProfiles_SameSeedSameState` |
| T7 | `TensionCuesTests.cs` | `TensionCues_BandBoundaries_MatchSpecTable`, `TensionCues_CreakPitch_RisesToTwoPointOne`, `TensionCues_CameraMotionZero_RemovesShakeTraumaAndRumble` |
| T9 | `FishingHostLedgerTests.cs` | `FishingHostLedger_CastWhileActive_Rejected`, `FishingHostLedger_StaleOrDuplicateCastId_Rejected`, `FishingHostLedger_ChargeOutsideUnitRange_Rejected`, `FishingHostLedger_OriginBeyondThreeMeters_Rejected`, `FishingHostLedger_NoBait_Rejected`, `FishingHostLedger_InvalidWater_Rejected`, `FishingHostLedger_Accept_ConsumesBaitAndRolls`, `FishingHostLedger_BareHook_NeverRunsOut`, `FishingHostLedger_CancelInWaiting_RefundsBait`, `FishingHostLedger_SnapEscapeMiss_DoNotRefund`, `FishingHostLedger_EarlyHook_ResolvesEscapedInvalid`, `FishingHostLedger_LandedBelowDurationFloor_ResolvesEscapedInvalid`, `FishingHostLedger_JunkLanded_UsesJunkDragSpeedFloor`, `FishingHostLedger_StuckWaiting_TimesOutAsRefundedCancel`, `FishingHostLedger_HookedPastMaxFightTime_TimesOutEscapedTimeout`, `FishingHostLedger_PausedClock_DoesNotTimeOut`, `FishingHostLedger_Disconnect_ClearsWithoutCommit`, `FishingHostLedger_Rejoin_KeepsSlotBaitAndRestartsCastIds`, `FishingHostLedger_FullCreel_ReleasesFishAndAnnounces`, `FishingHostLedger_OrphanMessage_RepeatsStoredResolution`, `FishingHostLedger_SnapAfterHook_AnnouncedLost`, `FishingHostLedger_Missed_NotAnnounced`, `FishingHostLedger_SelectWhileActive_Refused`, `FishingHostLedger_RollSeed_DerivesFromRunSeedAndCounter`, `FishingHostLedger_CleanCatch_UpgradesQualityOneTier`, `FishingHostLedger_CleanCatchAtTrophy_StaysTrophy`, `FishingHostLedger_ImplausibleCleanClaim_KeepsRolledQuality` (fight seconds ahead of the host clock plus slack, and below the duration floor), `FishingHostLedger_SelfContradictoryCleanClaim_KeepsRolledQuality` (recoveries > 0), `FishingHostLedger_NonCleanCatch_KeepsRolledQuality`, `FishingHostLedger_JunkCleanClaim_NoBonus`, `FishingHostLedger_ThreeFailures_NextCastPity`, `FishingHostLedger_Landed_ResetsPityCount`, `FishingHostLedger_CancelDoesNotCountTowardPity` (R65) |
| T9 | `CreelRulesTests.cs` | `CreelRules_OverweightRule_AcceptsUntilAlreadyFull`, `CreelRules_Warn_AtEightyPercent`, `CreelLedger_SellAll_ClearsSlotAndAddsCoins`, `CreelLedger_EntryIds_MonotonicPerSlot` |
| T10 | `InputContextRouterTests.cs` (add) | `InputContextRouter_RodReady_PassesSecondaryAndMovement`, `InputContextRouter_RodAiming_PassesChargeBlocksSprintAndInteract`, `InputContextRouter_RodWaiting_PassesCancelAndMovementBlocksPrimary`, `InputContextRouter_RodFighting_BlocksMovementPassesLookAndPrimary`, `InputContextRouter_RodContexts_SameKeyNeverMovesAndReels` (S / stick X: Move zeroed whenever RouteFight is live), `InputContextRouter_InteractLive_MatchesSpecRows`; `InputActionsAssetTests.cs` (add) `InputActions_FishingReelAndSteer_Exist` (17.2 data-and-input bullet). Existing `InputContextRouter_NonM1Contexts_RouteLikeFree` keeps its name but its cases become Carrying, Staggered, Downed, Carried, Seated |
| T10 | `GameplayContextArbiterTests.cs` | `GameplayContextArbiter_ClimbingBeatsEverything`, `GameplayContextArbiter_SwimmingBeatsRodRequest`, `GameplayContextArbiter_RodRequest_UsedWhenOnLand`, `GameplayContextArbiter_MenuRequest_TreatedAsFree`, `GameplayContextArbiter_LocksMovement_OnlyRodFighting` |
| T10 | `SettingsStoreTests.cs` (add) | `SettingsStore_FishingFields_RoundTripAndClamp` |
| T11 | `SfxRecipesTests.cs` (add) | `SfxRecipes_EveryM2Recipe_HasCorrectSampleCount`, `SfxRecipes_EveryM2Recipe_PeakBelowOne`, `SfxRecipes_EveryM2Recipe_HasNoNaNOrInf`, `SfxRecipes_M2Loops_HaveSmoothSeam`, `SfxRecipes_AllIds_Unique` |
| T11 | `AudioBusTests.cs` (add; T1 review carry-over, extras) | `AudioBus_UnknownId_WarnsOncePerId`, `AudioBus_MissingClip_WarnsOncePerId` |
| T11 | `RodCueAudioTests.cs` | `RodCueAudio_EveryMappedCue_HasKnownSfxId`, `RodCueAudio_BiteAndSnap_AreStingers` |
| T12 | `WaveMathTests.cs` | `WaveMath_Height_BoundedBySumOfAmplitudes`, `WaveMath_Height_ContinuousInTime`, `WaveMath_SameInputs_Deterministic`, `WaveMath_Flat_IsZero` |
| T12 | `WaterSurfaceTests.cs` (add; M1 test kept) | `WaterSurface_Configured_FollowsWaveMath`, `WaterSurface_ResetToFlat_ReturnsZero` |
| T12 | `MeshBuilderTests.cs` | `MeshBuilder_FlatPrimitives_HaveThreeVerticesPerTriangle`, `MeshBuilder_FaceVertices_ShareNormalSlotAndColour`, `MeshBuilder_Rebuild_IsByteIdentical`, `MeshBuilder_Grid_SharesVertices` |
| T12 | `RippleMathTests.cs` | `RippleMath_Ring_PeaksOnTheFrontAndFadesByLifetime`, `RippleMath_NextSlot_RecyclesOldest` |

### 10.2 Rod state machine (T8, `RodStateMachineTests.cs`) - one test per spec 9.2 row (acceptance criterion 2)

| 9.2 row (From / event) | Test |
|---|---|
| Idle / Secondary down | `RodStateMachine_Idle_SecondaryPressed_EntersReadying` |
| Readying / held 0.25 s | `RodStateMachine_Readying_SecondaryHeldQuarterSecond_EntersReadyWithMarker` |
| Readying / released | `RodStateMachine_Readying_SecondaryReleased_ReturnsIdle` |
| Ready / released | `RodStateMachine_Ready_SecondaryReleased_ReturnsIdleMarkerOff` |
| Ready / fresh Primary | `RodStateMachine_Ready_FreshPrimaryPress_EntersChargingAtZero` |
| Charging / Secondary released | `RodStateMachine_Charging_SecondaryReleased_ReturnsIdleWithoutCast` |
| Charging / valid release | `RodStateMachine_Charging_ValidRelease_EntersCastingAndRequestsCast` |
| Charging / short release | `RodStateMachine_Charging_ShortRelease_ThunksAndStaysReady` |
| Charging / invalid marker | `RodStateMachine_Charging_InvalidMarkerRelease_ThunksAndStaysReady` |
| Casting / flight over + result | `RodStateMachine_Casting_FlightEndedWithResult_EntersWaiting` |
| Casting / flight over, no result | `RodStateMachine_Casting_FlightEndedWithoutResult_StaysCastingTimerStopped` |
| (timer start rule) | `RodStateMachine_Casting_ResultAfterLanding_BiteTimerStartsAtResult` |
| Casting / invalid result | `RodStateMachine_Casting_InvalidResult_RecallsToIdle` |
| Waiting / invalid result | `RodStateMachine_Waiting_InvalidResult_RecallsToIdle` |
| Casting / no result in 3 s | `RodStateMachine_Casting_ResultTimeout_RecallsAndSendsCancel` |
| Waiting / Secondary pressed | `RodStateMachine_Waiting_SecondaryPressed_CancelsToIdle` |
| Waiting / beyond maxLineOut (R67: `PlayerToBobber - CastTipReach > MaxLineOut`) | `RodStateMachine_Waiting_BeyondMaxLineOut_AutoReelsAndCancels` |
| Waiting / bite timer | `RodStateMachine_Waiting_BiteTimerElapsed_EntersBiteAndSendsBiteEvent` |
| Bite / auto-hook | `RodStateMachine_Bite_AutoHookAfterHookBeat_EntersHookedAndStartsFight` |
| Bite / Primary in window | `RodStateMachine_Bite_PrimaryInsideHookWindow_EntersHooked` |
| Bite / window elapsed | `RodStateMachine_Bite_HookWindowElapsed_EscapesMissed` |
| Hooked / land | `RodStateMachine_Hooked_LineAtLandDistanceWhileReeling_LandsWithFightReport` |
| Hooked / snap | `RodStateMachine_Hooked_SnapTimerReachesGrace_Snaps` |
| Hooked / pinned | `RodStateMachine_Hooked_PinnedTooLong_EscapesPinned` |
| Hooked / slack | `RodStateMachine_Hooked_SlackTooLong_EscapesThrewHook` |
| Hooked / leash | `RodStateMachine_Hooked_DisplacedBeyondLeash_EscapesDropped` |
| Hooked / entered water | `RodStateMachine_Hooked_EnteredWater_EscapesDropped` |
| Any line-out / teleport | `RodStateMachine_Waiting_ForceEndTeleport_CancelsToIdle` |
| Any line-out / respawn | `RodStateMachine_Casting_ForceEndRespawn_CancelsToIdle` |
| Any line-out / stow | `RodStateMachine_Bite_ForceEndStow_CancelsToIdle` |
| Any line-out / swim | `RodStateMachine_Waiting_ForceEndSwim_CancelsToIdle` |
| Any line-out / disconnect | `RodStateMachine_Hooked_ForceEndDisconnect_EscapesDropped` |
| Waiting / unsolicited commit | `RodStateMachine_Waiting_UnsolicitedCancelCommit_RecallBeatThenIdle` |
| Bite / unsolicited commit | `RodStateMachine_Bite_UnsolicitedCommit_BeatThenIdleNoResult` |
| Hooked / unsolicited commit | `RodStateMachine_Hooked_UnsolicitedEscapeCommit_SlackBeatThenIdle` |
| Terminal / beat + commit | `RodStateMachine_Terminal_BeatDoneAndCommit_ReturnsIdle` |
| Terminal / commit before beat | `RodStateMachine_Terminal_CommitBeforeBeatEnds_WaitsForBeat` |
| Terminal / no commit | `RodStateMachine_Terminal_NoCommit_ReturnsIdleAfterCommitTimeout` |

Rules: `RodStateMachine_IllegalEvents_IgnoredInEveryState` (TestCaseSource over all 11 states), `RodStateMachine_HeldPrimaryThroughCatch_DoesNotStartNewCast`, `RodStateMachine_MismatchedCastId_Ignored`, `RodStateMachine_Paused_StopsBiteTimerAndBeat`, `RodStateMachine_NibbleInput_NeverHooksOrCancels`, `RodStateMachine_SetRod_IgnoredOutsideIdle`.

### 10.3 Data, wire, validation, tuning

| Owner | File | Required tests |
|---|---|---|
| T13 | `FishingDefsTests.cs` | `FishingDefs_ToCore_MapsEveryField` (one case per Def), `ArtPaletteDef_SlotTint_EqualsSlotPalette`, `ArtPaletteDef_PaletteVectors_LengthSixteen`, `WaterSettingsDef_ToWaveSet_MapsComponents`, `SfxCueTableDef_ToRecipes_OverridesMixingKeepsLength` |
| T13 | `ContentRegistryTests.cs` (add) | `ContentRegistry_FishingCategories_IndexedOrdinal`, `ContentRegistry_FishingCategories_ChangeHash`, `ContentRegistry_SpeciesWithForeignProfile_FailsValidate`, `ContentRegistry_Fishing_BuildsDatabase` |
| T14 | `FishingContentTests.cs` | `FishingData_Ids_UniquePerCategory`, `FishingData_Ranges_Valid`, `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth`, `FishingData_SeedsMatchSpecTables` (seed values = spec 9.6/9.7/9.12, motion seeds per 7.7, pity seeds 3 / 0.85 / 1.5 / 0.3; values T15 retunes come from the `TunedDeviations` map), `FishingData_ThrasherCalm_AtLeastOneSecond`, `FishingData_AssetsExistForEverySeed` |
| T14 | `ValidationTests.cs` (add) | `Validation_FishingContent_Pass`, `Validation_SpeciesModels_Pass` (also asserts `subMeshCount == 1`; ruling G-Q6) |
| T15 | `Tuning/FishingBots.cs` (`[Category("Tuning")]`) | `FishingBots_BulldogHandmade_MeetsGreedyCounterRhythmTargets`, `FishingBots_SteadyRhythm_MedianFifteenToThirtySeconds`, `FishingBots_DarterRhythm_MedianFifteenToThirtySeconds`, `FishingBots_ThrasherRhythm_MedianThirtyFiveToFiftyFiveSeconds`, `FishingBots_RunnerRhythm_MedianSixtyToOneTenSnapsAtMostFifteenPercent`, `FishingBots_TwigRod_RhythmLandsNinetyPercentAtHalfWeightSeventyFiveAtPointNine`, `FishingBots_AllProfiles_PrintSignatureMotionAndDistributions`, `FishingBots_FightSimStep_UnderFiftyMicroseconds` (prints; fails only above 0.2 ms) |
| T16 | `CreelEntryTests.cs` | `CreelEntry_FromAndToCore_RoundTrip`, `CreelEntry_Equality_FieldWise` |
| T17 | `FishingWireTests.cs` | `RodStateData_Equality_FieldWise`, `RodStateData_Size_AtMostTwentyFourBytes`, `CatchAnnouncement_CleanFlag_SetsCleanBonus`, `FishingRollNet_RoundTrip_PreservesRoll`, `FishingRollNet_InsaneRoll_TryToCoreFalse`, `CatchResultNet_RoundTrip` |
| T20 | `ValidationTests.cs` (add) | `Validation_Shaders_Pass` (sets `ShaderUtil.allowAsyncCompilation = false` first), `Validation_ToonMeshesHaveVertexColours_Pass` |
| T27 | `FightLogCsvTests.cs` | `FightLogCsv_Header_MatchesContract`, `FightLogCsv_Format_InvariantCultureAndColumnCount` |

### 10.4 PlayMode (`Tests/PlayMode/`, port 7790)

| Owner | File | Tests |
|---|---|---|
| T3 | `SoloSessionTests.cs` | (add) `Solo_LeaveRightAfterConnected_EndsOnMenu` |
| T19 | `FishingSoloTests.cs` | (8 tests: the 5 planned below + the 3 `PlayerCreel_*` T16 carry-overs listed after them; ruling T19-R1 adds a rod-tip assertion to the first) `SoloHost_CastBiteFight_Lands` (forced 0.1 s bite, forced bluegill, scripted input; asserts a creel entry and the CatchAnnounced on the bus, rod back to Idle, rumble 0); `SoloHost_CastResult_ArrivesAfterCastRequestReturns` (machine still Casting with no result right after `CastRequestRpc` returns; the result lands on a later frame, V11); `SoloHost_CancelInWaiting_RefundsBait` (count back to 20); `RodOut_MoveInput_StillWalks`; `RodFighting_MoveInput_DoesNotMovePlayer` (both movement tests live here, ruling E-Q3; `PlayerMovementTests.cs` stays T3-only). Delivered with three more: `PlayerCreel_ServerTryAdd_RefusedWhenFull`, `PlayerCreel_DebugBellSell_EmptiesCreelAndAddsCoins`, `PlayerCreel_MirrorsLedger_AfterRespawn` (T16 carry-overs); 8 tests in all |
| T16 (added) | `PlayerHooksTests.cs` | `Locomotion_LockingSource_BlocksWalkAndJump_ButGravityAndYawContinue`, `Locomotion_JumpBufferedBeforeLock_DoesNotFireOnLandingWhileLocked`, `Locomotion_RodFightingRequest_IsWrittenAndLocksMovement`, `Locomotion_RodAimingRequest_WalksAtSixtyPercent`, `Locomotion_FirstEnabledNonFreeSourceWins_DisabledIgnored`, `Locomotion_ApplyTeleport_RaisesTeleportedAfterThePositionIsApplied`, `Camera_AddFovKick_AddsDecaysScalesWithCameraMotionAndClamps`, `PlayerNetState_ServerInitialized_RaisedOnceAfterInit_AndThrowingSubscriberIsIsolated`, `PlayerNetState_ServerInit_SlotIsAlreadyWrittenInsideTheEvent`, `PlayerNetState_ToolChangedHandler_IgnoresEqualValues`, `SessionState_SlotTakeover_ClearsCreelForNewIdentity_KeepsItForSameIdentity`, `PlayerNetState_OwnerSetTool_RaisesToolChangedOncePerChange` (the file is not in the original C 12; it carries the T16 hook tests, reaching internals through `Gameplay/AssemblyInfo.cs`) |
| T11 (added) | `AudioBusPlayModeTests.cs` | `AudioBus_Duck_RampsOnUnscaledTimeAndReleases`, `AudioBus_RateLimit_ExpiresAfterMinInterval`, `AudioBus_Loop_FollowsTransformAndStopsWhenItIsDestroyed`, `AudioLoop_IsPlaying_FalseWithoutThrowingWhenSourceObjectIsDestroyed` (real audio sources; added by T11 and its follow-up) |

---

### 10.5 Test files added beyond the lists above (T1-T19; 2026-10-12)

Owners keep them; the required names in 10.1-10.4 are unchanged. Mutation rule for every later brief: for each test added, name the plausible wrong implementation it catches; for key formulas and orders, break the code once and show the test fails; tests that only assert no-exception or run the same code twice do not count.

| Owner | File | Covers |
|---|---|---|
| T9 | `CatchValidatorTests.cs` (13 tests) | cast-rule order, stale/origin boundaries, non-finite yaw, hook-too-early boundary, landed floor, `HostLineLength0`, `WalkBoundedLineLength0` (T9-R1), `AnnounceFor`, every clean-claim bound |
| T9 | `FishingHostLedgerTests.cs` (extended after review) | NaN host distance, yaw rule, dt guard, pity rows for Escaped(Timeout) and Escaped(Dropped), landing floor with a non-Handmade rod, `ResetSlot` (mini-fix batch) |
| T9 | `CreelRulesTests.cs` (extended) | `(8, 0, 10, 15000)` warns, last slot (3 of 4), `ctor(0)` throws, `ClearSlot` (T16-R1) |
| T12 | `MeshBuilderTests.cs`: `MeshBuilder_AllPrimitives_FaceOutward`, `MeshBuilder_InvalidArguments_ThrowAndLeaveBuilderUnchanged`, `FaceJitter_Grey_DeterministicAndWithinJitter`; `WaveMathTests.cs`: non-finite fields are zero terms | winding for every primitive, argument guards |
| T14 | `FishingValidatorTests.cs` (44 tests), `FishingContentBuilderTests.cs` (5 tests, temp folder `Tests/__Temp/`), `FishingContentTests.cs` (`TunedDeviations`) | validator messages and ranges, `Prefixed` seam, `BuildAt` re-rooting, create-if-missing, wrong-type refusal |
| T15 | `Tuning/FishingBots.cs`: `FishingBots_RhythmDecide_PinsDiveEasingAndSteerRules` | pins bot `Decide` rules (7.15) |
| T16 | `PlayerHooksTests.cs` (PlayMode, 12 tests, 10.4), `CreelEntryTests.cs` | context arbitration, movement lock, jump buffer, `Teleported`, FOV kick, `ServerInitialized`, tool-change dedupe, slot takeover |
| T17 | `FishingHostGuardTests.cs` (4 tests), `FishingWireTests.cs` | `IsOriginFinite`, `OriginWithinSlack`, `IsSaneCatch`, `FishingRodNetwork.IsPlausible`; wire struct round trips |
| T18 | `RodControllerHelperTests.cs` (10 tests) | `FightRecorder` (captured roll, cancelled cast, no-Waiting, clean claim, first bite only), `FishPointSampler` (cadence, hold, non-finite), `RodControllerRules` (context table, equip, `FiniteOr`) |
| T19 | `FishingSoloTests.cs` (8 tests, 10.4) | cast to land, V11 ordering, cancel refund, movement, creel runtime |
| T11 | `AudioBusPlayModeTests.cs` (4 tests, 10.4) | duck, rate limit, loop follow, `AudioLoop.IsPlaying` |

## 11. Commands reference

Unchanged: M1 contract section 11 (`Tools/unity.ps1 compile | build | test-edit | test-play | check`, `-Filter`, `$env:FG_OUT`, result lines, exit codes). Tuning bots run with `-Filter "FishGame.Tests.EditMode.FishingBots"`; the full `test-edit` includes them (they fail only on gross regressions plus the 9.12 targets).

---

## 12. Per-task file manifest (owned files; paths under `Assets/_Project/` unless rooted; tests listed in section 10 are owned by the same task)

| Task | Files created or edited |
|---|---|
| T1 | `Tests/EditMode/FishGame.Tests.EditMode.asmdef`, `Core/Random/Pcg32.cs`, `Core/Diagnostics/LogOnce.cs`, `Runtime/Services/EventIsolation.cs`, `Runtime/Audio/AudioBus.cs`, `Networking/{LocalNetworkSession,UgsNetworkSession,UgsNetworkSession.Teardown,CompositeNetworkSession}.cs`, `Tests/EditMode/AudioBusTests.cs` (ruling A-Q3) |
| T2 | `Networking/LocalNetworkSession{,.Start,.NgoEvents}.cs` (main < 300 lines: the attempt lifecycle moves to `.Start.cs`, A-Q1), `UI/{UiFlowController (< 400 lines, A-Q2),UiSessionActions,UiFocusRouter,IHudExtension}.cs` |
| T3 | `Tests/PlayMode/{PlayModeHarness,SoloSessionTests,PlayerMovementTests}.cs` |
| T4 | `Editor/Builders/{SceneBuilder,LakeDockBuilder}.cs`, `Editor/Validation/{Validators,ValidationRunner}.cs` |
| T5 | `Core/Fishing/{RodState,FightPhase,FightOutcome,EscapeReason,CatchQuality,FishRarity,BodyShape,CastRejectReason,RodEventType,AnnounceKind,LineGaugeMode,RodForceReason,RodCue,RodStats,BaitStats,FishFightProfile,FishSpeciesData,JunkData,FishingTuning,FishingDatabase,WaterProbe,CastMath,FishPointMath,RodWireQuant,FishingConstants}.cs`, `Core/Player/ToolKind.cs`, `Tests/EditMode/FishingTestData.cs` (shared test data, read-only for T6-T9; ruling B-Q1) |
| T6 | `Core/Fishing/{CatchResult,FishingRoll,BitePlan,BitePlanner,CatchMath,RollRequest,PlaceholderRoller,FishingRollValidation}.cs` |
| T7 | `Core/Fishing/{FightState,FightSim,TensionBand,TensionCueState,TensionCues}.cs`, `Core/AssemblyInfo.cs` (InternalsVisibleTo) |
| T8 | `Core/Fishing/{RodFrame,FightReport,RodEffectKind,RodEffect,RodStateMachine}.cs` |
| T9 | `Core/Fishing/{CastPhase,ActiveCast,CastRequestData,HostCastView,CastVerdict,CommitDecision,PlayerLoadout,CreelSnapshot,CreelItem,CreelRules,CreelLedger,CatchValidator,RollOverrides,FishingHostLedger}.cs` |
| T10 | `Core/Input/{InputContextRouter,GameplayContextArbiter}.cs`, `Runtime/Input/{DevKey,IDevInput,PlayerInputAdapter}.cs`, `Runtime/Settings/SettingsData.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs` (implements `IDevInput`) |
| T11 | `Core/Audio/{SfxIds,SfxRecipe,SfxRecipes,SfxRecipes.M2}.cs` (ruling C-Q11), `Tests/EditMode/AudioBusTests.cs` (edit), `Core/Fishing/RodCueAudio.cs`, `Runtime/Audio/{AudioBus,AudioLoop,SfxGenerator}.cs`, `Runtime/Services/CaptionRequested.cs`, `Editor/Dev/AuditionCuesMenu.cs` |
| T12 | `Core/World/{WaveSet,WaveMath,RippleMath}.cs`, `Core/Render/{FaceJitter,ShaderGlobals}.cs`, `Runtime/World/{WaterSurface,MeshBuilder}.cs` |
| T13 | `Data/Fishing/{RodDef,BaitDef,FightProfileDef,FishSpeciesDef,JunkDef,FishingTuningDef}.cs`, `Data/Audio/SfxCueTableDef.cs`, `Data/Art/{ArtPaletteDef,PaletteSeason,DayPhaseAmbient,WaterSettingsDef}.cs`, `Data/ContentRegistry.cs` |
| T14 | `Editor/Builders/Fishing/{FishingSeedData,FishingContentBuilder}.cs`, `Editor/Validation/FishingValidators.cs`, hooks in `ContentBuilder.cs`, `ValidationRunner.cs`, `App/AppBootstrap.cs`, constants in `AssetPaths.cs`; generated `Content/{Fishing,Art,Audio}/**` |
| T15 | `Tests/EditMode/Tuning/FishingBots.cs`; retuned `.asset` values + matching `FishingSeedData` seeds (2.6) + the `TunedDeviations` map in `Tests/EditMode/FishingContentTests.cs` (only that map; ruling D-Q2) |
| T16 | `Gameplay/Player/{PlayerNetState,PlayerLocomotion,PlayerCamera,PlayerInteractor,PlayerRigPaths,IPlayerContextSource}.cs`, `Gameplay/Fishing/{CreelEntry,PlayerCreel}.cs`, `Gameplay/Session/SessionState.cs`, `Gameplay/Interaction/DockBell.cs` |
| T17 | `Fishing/Net/{RodStateData,CatchResultNet,FishingRollNet,CatchAnnouncement,CatchAnnounced,FishCaught,RodViewState,FishingRodNetwork}.cs`, `Fishing/Host/{FishingHost,CastProbe}.cs`, `Fishing/Debug/FishingDebugSettings.cs`, `Core/Versioning/BuildInfo.cs`, `Fishing/AssemblyInfo.cs` (edit), `Tests/EditMode/BuildInfoTests.cs` (edit) |
| T18 | `Fishing/Owner/{FishingRodController,FightEnded}.cs` (+ internal helpers such as `FishPointSampler`) |
| T19 | `Editor/Builders/Fishing/{PlayerFishingRigBuilder,FishingSceneObjects}.cs`, hooks in `PrefabBuilder.cs`, `LakeDockBuilder.cs`; `Tests/PlayMode/FishingSoloTests.cs` (8 tests: 5 planned incl. the two movement tests + 3 `PlayerCreel_*`); generated prefab, scene, meshes |
| T20 | `Shaders/{FishGameCommon.hlsl,Toon.shader}`, `Editor/Validation/{ShaderValidator,RenderValidators}.cs`, `Editor/Builders/RenderMaterialLibrary.cs`, `Editor/Builders/Render/FishModelPostprocessor.cs`, edits in `MaterialLibrary.cs`, `PrefabBuilder.cs`, `ValidationRunner.cs`, `AssetPaths.cs`; reimported `Content/Models/Fish_Perch.fbx.meta`, `Tests/EditMode/ToonMaterialTests.cs`, conditionally `Tools/unity.ps1` (ruling F-Q4) |
| T21 | `Shaders/{Water,Line,GradientSky}.shader`, `Editor/Builders/Render/RenderingSetupBuilder.cs`, edits in `RenderMaterialLibrary.cs`, `ContentBuilder.cs`, `PrefabBuilder.cs`, `AssetPaths.cs`; `Tests/EditMode/RenderingSetupTests.cs`; generated `Content/Rendering/**`, materials |
| T22 | `Presentation/World/{PaletteGlobals,WaterTimeDriver,RippleField}.cs`, `Editor/Builders/Render/PresentationSceneObjects.cs`, hook in `LakeDockBuilder.cs`, `Tests/PlayMode/PresentationWorldTests.cs` |
| T23 | `Presentation/Feedback/{FishingFeedback,CatchRevealView,FishFallbackMesh}.cs`, a block of up to 12 lines in `PlayerFishingRigBuilder.cs`, `Tests/PlayMode/FeedbackSoloTests.cs` |
| T24 | `Presentation/Rod/{RodPresenter,LineView,BobberView,SplashFx,CastMarkerView}.cs`, `PlayerFishingRigBuilder.AddPresentation` body, `Tests/EditMode/PlayerPresentationRigTests.cs`, `Tests/PlayMode/RodViewSoloTests.cs` |
| T25 | `UI/Fishing/FishingHudPresenter.cs`, edits in `UI/{UiFlowController,UiStrings,SettingsPresenter}.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss` |
| T26 | `UI/Fishing/DevOverlayPresenter.cs`, edits in `UI/{UiFlowController,UiStrings}.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss` |
| T27 | `Core/Fishing/{FightLogRow,FightLogCsv}.cs`, `Fishing/Owner/FightTelemetry.cs`, edit in `FishingSceneObjects.cs` |
| T28 | `Editor/Builders/{LakeDockBuilder,LakeDockLayout,LakeDockMeshes}.cs`; generated `Scenes/Spot_LakeDock.unity`, `Content/Meshes/LakeDock_*.asset` |
| T29 | `Content/Models/Fish_{Bluegill,Perch,Carp,Trout,Catfish}.fbx` (+ `.meta`; copied from the staged `Art/Export/`; the perch file is replaced, its `.meta` kept); regenerated species assets (model refs) |
| T30 | `docs/superpowers/plans/2026-10-11-m2-acceptance.md`, `README.md` |
| Ladders plan (R70) | `ClimbWishTests.cs`, `ClimbGateTests.cs`, `LadderVolumeTests.cs`, `RodAwayStepTests.cs`, `RampAssistTests.cs`, `ToolStowAnimTests.cs` (EditMode); `LadderClimbTests.cs` (PlayMode, 15 tests); `SwimInteractGateTests.cs` rewritten (3 tests); `InputContextRouterTests` Climbing and Swimming rows; `RodViewSolo_StowAndEquip_AnimateThenSettle` | climb maths, re-entry grace, volume containment, rod away rule (incl. the hold until grounded), ramp foot, stow animation; geometry, climb out at 30/45/60 deg, descend and hang, deck edge and side-exit re-entry, top-out grace and release-at-the-top carry, locked entry, swim-exit no-churn, top-out bob, rod and line on a ladder, ramp walk-out and walk-down |

**Files added or edited beyond the plan (T1-T19, 2026-10-12; paths under `Assets/_Project/`, all committed unless marked mini-fix):**

| Task | Extra files |
|---|---|
| T7 | `Core/AssemblyInfo.cs` (InternalsVisibleTo `FishGame.Tests.EditMode`; was already listed above) |
| T9 | `Tests/EditMode/{CatchValidatorTests,FishingHostLedgerTests}.cs`; `Core/Fishing/CatchValidator.cs` gains `OriginWithinSlack`, `IsFinite`, `IsOriginFinite`, `WalkBoundedLineLength0` (T17 follow-up and mini-fix: `IsOriginFinite`, `OriginWithinSlack`); `ActiveCast.CastDistance` (from the rod tip since R67) |
| T11 | `Tests/PlayMode/AudioBusPlayModeTests.cs` (+`.meta`), `Runtime/Audio/AudioLoop.cs` (listed above) |
| T12 | `Tests/EditMode/{MeshBuilderTests,RippleMathTests,WaveMathTests,WaterSurfaceTests}.cs` |
| T13 | `Tests/EditMode/{FishingDefsTests,ContentRegistryTests}.cs` (registry tests extended) |
| T14 | `Editor/AssemblyInfo.cs` (InternalsVisibleTo), `.gitignore` (`/Assets/_Project/Tests/__Temp*`), `Tests/EditMode/{FishingContentBuilderTests,FishingValidatorTests,FishingContentTests}.cs`; `FishingContentBuilder.BuildAt` (internal), `FishingValidators.Prefixed/CheckRegistry/CheckSeedAssets/CheckSpeciesModel` (internal) |
| T15 | `Tests/EditMode/FishingContentTests.cs` (`TunedDeviations` map), spec 9.6/9.7/9.12 tables |
| T16 | `Gameplay/AssemblyInfo.cs` (InternalsVisibleTo `FishGame.Tests.PlayMode`), `Tests/PlayMode/PlayerHooksTests.cs`, `Tests/EditMode/CreelEntryTests.cs`, `Core/Fishing/CreelLedger.cs` (`ClearSlot`, T9 file) |
| T17 | `Tests/EditMode/{FishingWireTests,FishingHostGuardTests}.cs`; `Fishing/AssemblyInfo.cs`; `Core/Fishing/FishingRollValidation.cs` (`IsSaneCatch`, T6 file) |
| T18 | `Fishing/Owner/{FightRecorder,FishPointSampler,RodControllerRules}.cs` (internal helpers), `Tests/EditMode/RodControllerHelperTests.cs`; small edit in `Fishing/Net/FishingRodNetwork.cs` (29 lines in `dea35f8`) |
| T19 | `Tests/PlayMode/FishingSoloTests.cs` (8 tests), `Tests/PlayMode/PlayModeHarness.cs` (integration fix); `Content/Meshes/{Rod_Seg0..5,Bobber}.asset`; regenerated `Prefabs/PlayerFP.prefab` and `Scenes/Spot_LakeDock.unity` |
| mini-fix (after T19) | `Core/Net/PlayerKey.cs` + `Tests/EditMode/PlayerKeyTests.cs` (`MaxIdLength` 61), `Core/Fishing/{FishingHostLedger,CatchValidator}.cs`, `Gameplay/Session/SessionState.cs`, `Gameplay/Fishing/PlayerCreel.cs`, `Fishing/Host/FishingHost.cs`, `Fishing/Owner/FishingRodController.cs`, `Fishing/AssemblyInfo.cs`, `Tests/EditMode/{FishingHostGuardTests,FishingHostLedgerTests}.cs`, `Tests/PlayMode/PlayerHooksTests.cs` (working tree; the controller commits) |

---

## 13. Resolved ambiguities and spec deviations (M2)

| # | Topic | Decision | Reason |
|---|---|---|---|
| M2-D1 | Context ownership | `PlayerLocomotion` stays the only writer of `GameplayContext`; Fishing requests through `IPlayerContextSource`; precedence by `GameplayContextArbiter` (Climbing > Swimming > request > Free). The movement lock is the RodFighting router row plus `LocksMovement`. | Gameplay can't reference Fishing (spec 4.3); fixes G4. |
| M2-D2 | Input ruling | Movement suppressed only while RodFighting (Bite/Hooked); with the rod merely out, WASD walks. Reel/Steer act only in RodFighting (RouteFight). `Fishing/Reel` keeps its single S binding: "S (and Primary)" / "RT via Primary" (spec 8.3) is the router's `Reel || PrimaryHeld`. No duplicate bindings, so rebinding Primary carries reel with it. | Spec 8.2 feel rule, 19 #7; drift-runtime finding 1. |
| M2-D3 | `ArtPaletteDef.slotTints` | Not stored. `GetSlotTint(slot)` reads `SlotPalette`. | One source for slot colours (blue host, white, green, red). |
| M2-D4 | Tuning single source | Seeds in Editor `FishingSeedData`; assets are the runtime truth; bots load the assets; Core structs have no defaults. | Acceptance 9; user tunes in the Inspector. |
| M2-D5 | Unsolicited cancel beat | Reuses state `Escaped` with `Outcome = Cancelled` and `Recalling = true` (wire flag `FlagRecall`). | Spec 9.2's state list has no Cancelled state but asks for a 1.5 s beat. |
| M2-D6 | `FishingRoll.HookWindow` | Added (host fills from species data or the twitchy debug toggle). | The owner needs the window at Bite; spec 9.8's struct lacks it. |
| M2-D7 | `CommitRpc` payload | Adds `finalReason` (`EscapeReason`). | The unsolicited row needs `Escaped(reason)`. |
| M2-D8 | Creel home | `NetCreel` lives on a new `PlayerCreel` NetworkBehaviour (not `PlayerNetState`, already ~340 lines); host truth is the slot-keyed `CreelLedger` on `SessionState` so creels survive a rejoin (R46). | File size; R46. Authority unchanged (server write). |
| M2-D9 | Per-player server init | `PlayerNetState.ServerInitialized` event, not a second `IServerPlayerInit`. | G2: `PlayerSpawner` calls `GetComponent<IServerPlayerInit>()` once. |
| M2-D10 | Extra SFX recipe | `Rod_Thunk` (invalid release, recall, equip/stow) and `Line_Crack` (grace rising edge, spec 9.11 grace row; T23-fix); spec 9.11 sounds map onto the 11.7 ids via `RodCueAudio`. `SfxRecipe` gains `MinDistance`. | 9.11 names a thunk and a thud that 11.7 lacks; the bite needs a large minDistance (9.4). |
| M2-D11 | Cue mixing source | `SfxCueTableDef` at runtime (`AudioBus.ApplyCueTable`); Core `SfxRecipes` mixing values are its seeds. | Avoids two runtime sources; acceptance 9. |
| M2-D12 | Water clock seam | `WaterSurface.SetTimeSource` (set by Presentation `WaterTimeDriver`); M1 `HeightAt(x, z)` is now wave-aware; `HeightAt(x, z, t)` added. | Runtime has no Netcode reference; spec 10.4. |
| M2-D13 | Fight log | `fight-log{Suffix}.csv` and one row per cast that reached Waiting (cancels/misses included); extra `clean` column. | MPPM clones share persistentDataPath; the fun gate needs the cancel rate. |
| M2-D14 | Default debug loadout | `rod_handmade` + `bait_worm`, 20 of each bait, bare hook unlimited; selectors dev-only. | Handmade is the M2 reference; worms reach most M2 species. |
| M2-D15 | Fight motions (user-approved 2026-10-11) | `FightProfileDef` gains `burstRamp`, `calmDrift`, `calmDriftRamp`, `flipMinGap`, `lateralJitter` (7.7); Darter micro-bursts via its ranges and `flipRate` 0.8-1.2. All in assets; deterministic. Not adopted: a single `fightIntensity` knob, tackle-style assist levers, the Stardew bite-wait change (9.4 unchanged). | Each profile fights differently, not just harder (9.1 goal). The spec is amended to match after this contract. |
| M2-D16 | Clean-catch bonus (user-approved 2026-10-11) | Landed with fight `MaxTension < cleanCatchMaxTension` (seed 0.8) -> +1 quality tier (cap Trophy), value recomputed. `ResultRpc` carries `maxTension, fightSeconds, nearSnapRecoveries`; the host accepts the claim only within `CatchValidator.CleanClaimAccepted` bounds and otherwise keeps the rolled quality (never upgrades blindly, never invalidates the catch). | Rewards the rhythm the fight teaches; owner-simulated fight (N6) means the host can only bound-check. The spec is amended to match. |
| M2-D17 | HoldNode / AttachableNode | Stay M5 (V16). M2 adds the plain `Body/ArmR/Hand` transform M5 converts. | Not needed for a rod. |
| M2-D18 | Rod and bait selection keys | F4/F5 dev keys plus `SelectRodRpc`/`SelectBaitRpc`; forced bite / twitchy (F6/F7) are host-only statics. | Spec 17.2 debug selectors; the host owns rolls. |
| M2-D19 | Cast origin | The owner's root position (feet), not the rod tip; the line and fight start use the rod tip. R67 (2026-10-12): the cast DISTANCE is measured from the rod tip (landing = origin + yaw forward x (`CastTipReach` + distance)); the origin itself stays the root. | Simple, identical on owner and host. |
| M2-D20 | Ledger keys | Bait, rod, selected bait by slot; cast ids, ActiveCast and stored resolution by clientId. | castIds restart per connection; R46 needs slot persistence. |
| M2-D21 | EditMode reach | EditMode tests reference Gameplay and Fishing (wire structs). | G1: wire structs can't live in Core. |
| M2-D22 | Pitch to apex | Linear map -30..45 deg -> x0.7..x1.3, clamped. | Spec 9.3 gives the range, not the mapping. |
| M2-D23 | Bad-luck nudge (R65, user-approved 2026-10-11) | The host counts consecutive failed casts per slot (Snapped, or Escaped with any reason; a Landed resets; cancels and recalls neither count nor reset; kept in the slot data, survives rejoin). At `pityAfterFailures` (seed 3, 0 = off) the next accepted cast gets `FishingRoll.Pity = true`, set by the ledger at accept with no extra RNG draw. A Pity cast resolving (other than Cancelled) resets the count. Owner-side effects, deterministic: `FightSim` strain scale x `pityStrainMult` (0.85) and snap grace x `pityGraceMult` (1.5), a real bite hook window plus `pityHookWindowBonus` (0.3 s). No change to species, weight or quality odds; the host's Landed-floor validation is unchanged. Visible to the crew: the host sends `CatchAnnouncedRpc` with the new `AnnounceKind.PityCast = 4` (smallest contract change: no new RPC; the wire roll carries the bit in `FishingRollNet.Flags`) and every peer shows `toast.pity` "{0} has a lucky line!" through `toast.raw`; the fight log gets a `pity` column. All numbers are `FishingTuningDef` seeds; Core holds none. | Fishing should not punish a streak of snaps without the user noticing why; the user decides keep / tune / remove at playtest (T30). The spec section 3.2 is amended by the controller. |
| M2-D24 | Unsolicited commit while Casting (T8-R1, T8 review, 2026-10-12) | `RodStateMachine.OnCommit` while `Casting` is a legal event: it takes the same path as Waiting/Bite/Hooked (a `Cancelled` commit -> `Escaped` with `Recalling = true` and cue `Recall`, the 1.5 s cancel beat per M2-D5, then Idle; any other commit -> `Escaped(finalReason)`, `None` -> `Invalid`). Casting is removed from the IllegalEvents expectation for commits. | Spec 9.13 "LastCast" intent (a host cancel/timeout during the flight must recall the line) wins over the 9.2 row list. Spec 9.2 carries a note. |
| M2-D25 | Invalid cast result remembers the cast (T8-R2, T8 review) | Any invalid `OnCastResult` (Casting or Waiting) sets `_cancelledCastId = castId` before the recall, so the host's `Cancelled` commit for it is consumed silently (no `IgnoredEventCount`, no beat). Only the owner-side `CastResultRpc` handler sends the one `CancelRpc` (BF-5). | The host always answers a rejected cast with a cancel commit; without the flag it would be counted as an illegal event or replay a beat. |
| M2-D26 | Walk-bounded hook line (T9-R1, T9 review; hardening) | At accept the ledger stores `ActiveCast.CastDistance` (R67: the distance from the rod tip; origin -> landing is `CastTipReach` + this); at `Hooked`, `LineLength0 = max(HostLineLength0(hostDistance), CastTipReach + CastDistance - SprintSpeed * (THooked - TAccepted) - HookedLineSlack 3.1)`, floored at `landDistance` (`CatchValidator.WalkBoundedLineLength0`). No new tuning: `SprintSpeed` is the Core `MovementMath` constant. Non-finite `hostDistanceToLanding` -> `Escaped(Invalid)` (I-2); non-finite `YawDeg` -> `OriginTooFar` and `TryComputeLanding` false (I-1). | A client teleported next to the landing could shrink the Landed floor to zero; the player cannot have walked faster than sprint since the cast was accepted. Knockback is an M5 concern (C 16.2). |
| M2-D27 | Slot takeover resets the creel (T16-R1, T16 review) | `CreelLedger.ClearSlot(slot)` (Core, T9 file, allowed in the T16 fix). `SessionState.Upsert` clears that slot's creel and debug coins when the slot is assigned to a DIFFERENT persistent identity (JoinRules takeover rule c); the same identity rejoining keeps both (R46). The mini-fix batch after T19 extends the same trigger (`SessionState.SlotTakenOver`) to the host ledger slot (`FishingHostLedger.ResetSlot`). | A new player must not inherit the previous holder's fish or coins; a returning player must (R46). |
| M2-D28 | Rod geometry vs host slack (T19-R1, T19 review) | The rod's local rotation makes it point camera-forward pitched up about 35 deg in the owner arm pose (and plausibly in the remote pose); `FishingConstants.HookedLineSlack` is re-derived from the measured maximum forward reach of `TipAnchor` over pitch -60..+60 plus 0.5 m (rounded up to 0.1; value TBD, set by the T19-fix); the host stays conservative (owner start line >= host `LineLength0` for every honest cast). `SoloHost_CastBiteFight_Lands` asserts the tip is in front of the camera and below head height + 1 m. The T19-fix also amends the PlayMode driver surface (C 7.19) and the harness `PrepareResetScene`. | The T19 review found the host slack was not tied to the real rod reach, so an honest cast could start with a line shorter than the host's floor. |
| M2-D29 | Ladders (R70, 2026-10-12) | `Gameplay/World/LadderVolume` (trigger box, layer Default, local-space `Contains`, `IsAtOrAboveTop` with `TopEpsilon` 0.05). Owner `PlayerLocomotion`: enter when the capsule centre is inside any volume, not locked (RodFighting or a source's `LocksMovement`), not in the `ClimbGate` grace; entry writes `NetSwimming` false. Climb: CharacterController stays enabled; `MovementMath.ClimbWish(yawDeg, pitchDeg = PlayerCamera.PitchDeg or 0, MoveX, MoveY)` at `ClimbSpeed` 2.4; no gravity; zero input = zero velocity. Exit: centre outside every volume; the applied climb velocity carries into `_vx/_vz/_vy`; an exit at or above the top face starts `ClimbReentryGrace` 0.25 s; a swimmer who exits at swim depth is swimming again on the same frame (`SwimmingAt`). `RouteClimbing` passes Move, Look, Pause; `RouteSwimming` zeroes Interact. Lake dock `Ladders/LadderL|R`: centre (+-2.05, 0.1, -41.5), size (1, 3, 1). | Spec R70 and the RV research. The top-only grace comes from the PlayMode climb-out tests; the Default layer keeps the boxes out of CastProbe's water mask. The velocity carry is kept for continuity and for release-at-the-top (`Ladder_TopOutThenRelease_CarriedVelocityLandsOnDeckAndStaysOff` pins it); it is not required while W is held, because air control alone lands the player then. |
| M2-D30 | Shore-ramp foot assist (R70) | The 0.4 s exit lerp (`TryBeginClimb`, `MovementMath.ClimbDuration`) is deleted. `Exits/Ramp` (`WaterExit`; centre (12, -1.0, -42.5), size (3, 2.8, 7), layer Default; `Top` (12, 0.45, -50.5)) is the ramp foot. Core `RampAssist.Step`: in water inside the foot the player wades, not swims (D36's in-water gate); a swimmer entering it while `MovementMath.PushingToward` the Top (DA7's rule) gets `_vy = max(_vy, RampAssistKick 2.0)` once per entry. | R70 "a walkable slope with a light assist". The bed and ramp already form a continuous ~9 deg slope; the box reaches seaward to z -39 so the assist acts where swimmers find their feet. |
| M2-D31 | Rod on ladders; stow animation (R70) | `FishingRodController.HandleSwim`: away = `IsSwimming || IsClimbing` (`RodControllerRules.IsAway`, `AwayStep`), and coming back is held while airborne or while `PlayerLocomotion.LadderCatchPending` (`RodControllerRules.EffectiveAway`); stow on entry (remembered; a line out is recalled; `RodForceReason.Stow` for a ladder, `Swim` for water), equip when both clear. `RodPresenter` animates every `NetTool` change on every peer: Core `ToolStowAnim` (0.2 s, smoothstep) swings the Seg* chain and TipVisual 70 deg tip-down and 0.3 m back about Seg0, then hides; equip reverses it. TipAnchor is never moved (BF-3). | R70's stow/unstow animation without assets; one code path for the equip key, water and ladders. The hold avoids an equip/stow flicker at a top-out bob. |
| M2-D32 | No `NetClimbing` in M2 | Not added. Remotes see a climb through the NetworkTransform; no remote climb pose; no host rule reads climbing; `BuildInfo.ProtocolVersion` stays 2. Revisit in M5 with body states (a pickup rule may want "not climbing"). | A NetworkVariable for a cosmetic pose would bump the protocol with no M2 consumer. |

---

## 14. Drift adopted (M1 code vs M1 contract; from the 2026-10-11 scans)

| # | Drift | Status in M2 |
|---|---|---|
| DA1 | `Pcg32.Range(float)` can return max; no max > min check | **Fixed** by T1 (7.1). |
| DA2 | `JoinRules.ChooseSlot` rules a) and c) also require that no connected entry holds the slot | Adopted: contract text now includes the `!SlotConnected` guard (it prevents reissuing a slot during a pending takeover; tests exist). |
| DA3 | `UgsErrorMap` rule 2b: message contains "locked" -> `JoinsClosed`, after rule 2, before the relay rule | Adopted (test `UgsErrorMap_LockedMessage_IsJoinsClosed`). LobbyLocked text still unverified (M1 criterion 12). |
| DA4 | `UgsNetworkSession` rejects implausible codes (`JoinCode.IsPlausible`) before any UGS call | Adopted. |
| DA5 | `DockBell.RingRpc` writes the cooldown time on every peer | Adopted (local prompt greying); the host still decides in `CanInteract`. |
| DA6 | `PlayerSpawner.OnClientLoadComplete` is a second late-joiner entry; a roster miss disconnects with `Unknown` | Adopted; M2 per-player server init hangs off `ServerInitialized`, which both entries reach (G3). |
| DA7 | Forward climb needs facing the exit Top (dot > 0) | Retired for ladders by R70 (M2-D29: walk-in, no press, no facing rule). Survives only as the ramp-foot kick's "pushing toward the Top" test (MovementMath.PushingToward, M2-D30). |
| DA8 | Locomotion zeroes `Velocity` while paused | Adopted. |
| DA9 | `PlayerCamera` polls settings and `PlayerLocomotion` polls pause instead of subscribing to `Changed` | Adopted; catalogue updated (8.1). `PlayerNetState` also consumes `ISettings.Changed`. |
| DA10 | `ToastRequested` raised by `UiFlowController` (after T2: `UiSessionActions`), not `MainMenuPresenter` | Adopted. True at T2; T25 adds a second publisher, `UI/Fishing/FishingHudPresenter.cs` (`toast.raw`, ruling G-Q1). |
| DA11 | Settings also saved on quit and on a name commit in the main menu | Adopted. |
| DA12 | `EnabledMaps(Seated)` returns `Player|Boat` with no Boat map | Adopted; the adapter ignores missing maps; revisit in M6. |
| DA13 | `Fishing/Reel` bound to S only | Adopted as the implementation of spec 8.3 (M2-D2). `Fishing/Steer` matches (A/D composite + left stick X). |
| DA14 | `SessionState` disconnects a joiner whose slot is held (`FullLobby`) | Adopted. |
| DA15 | Extra Bootstrap root `BootstrapListener`; `SceneFlow` activeSceneChanged guard; `CharacterController.minMoveDistance = 0`; `ProjectSettingsBuilder.RepointVolumeProfile`; `PlayModeStartScene` static ctor; `TestRunStartSceneGuard` ExitingEditMode hook; `TestMenu` result-path consts | Adopted as-is. |
| DA16 | Minor internals: UGS seat cache, fire-and-forget `LeaveAsync` in `Dispose`, `PlayerInteractor` `IsSpawned` filter, `PlayerCamera` FOV clamp 80-110, `PlayerBody` execution order 200, `NameWire` round trip in `SubmitNameRpc`, `AppBootstrap.ResetStatics`, `SfxGenerator` not destroying a failed clip | Adopted as-is. |
| DA17 | `HoldNode`, `AttachableNode`, `NetTool` absent | `NetTool` added by T16; `HoldNode`/`AttachableNode` deferred to M5 (M2-D17). |
| DA18 | `PlayerLocomotion.SetContext` clobbers any other context every frame; no movement lock | **Fixed** by T16 (M2-D1). |
| DA19 | `AudioBus.TryPrepare` warns on every call | **Fixed** by T1. |
| DA20 | Whole-event isolation (Local, Ugs, Composite); wrong comment at `UgsNetworkSession.cs:78` | **Fixed** by T1 (G8). |
| DA21 | `LocalNetworkSession` 563 lines; `UiFlowController` 512 lines | **Split** by T2. |
| DA22 | Harness: no `ServiceRoots` asserts, `SuppressDevAutoStart` never reset, `Await` doesn't cancel, fixed and unguarded waits | **Fixed** by T3. |
| DA23 | Builders load assets before `NewScene`; no mesh-reference or scene missing-script validators; Bootstrap input/registry refs unvalidated | **Fixed** by T4. |
| DA24 | `Fish_Perch.fbx` imported with `materialImportMode: 2` | Changed by T20's postprocessor; the file itself is replaced by the staged single-submesh perch in T29. |
| DA25 | M1 router routes rod contexts like Free | Replaced by the T10 rod rows; D18 still applies to Carrying, Staggered, Downed, Carried, Seated. |

---

## 15. Amendments after section planning (2026-10-11)

Controller rulings on the section authors' contract questions, the consistency-check fixes, the staged fish models and the bad-luck nudge. Each row gives the ruling id, a one-line summary, and where it was applied: "C x" = this contract, otherwise the section plan (carry, core-fishing, core-support, data-tuning, net, presentation, ui-world). The section plans' "Contract questions" lists are all closed and point here.

### 15.1 Rulings

| Id | Summary | Applied in |
|---|---|---|
| A-Q1 | Attempt lifecycle (`AttemptAsync`, `PostCancel`, `CancelAttempt`, `WatchTimeoutAsync`, about 75 lines) moves into `LocalNetworkSession.Start.cs`; main file about 270 lines | C 7.2, C 12; carry T2.2 |
| A-Q2 | No fifth UI file; `UiFlowController.cs` target < 400 lines | C 7.2, C 12; carry T2.3, T2.8 |
| A-Q3 | `Tests/EditMode/AudioBusTests.cs` added to T1's manifest | C 2.6, C 12, C 10.1; carry T1 |
| A-Q4 | PlayMode green after each of T1-T4 | C 2.4; carry 0.2 |
| A-Q5 | Validators open scenes additively; the YAML text scan is an allowed alternative | C 7.4; carry T4.4 |
| B-Q1 | `Tests/EditMode/FishingTestData.cs` added to T5's manifest (read-only for T6-T9) | C 12; core-fishing T5 |
| B-Q2 | Internal test hooks `DrawCount`, `BurstLateralMult`, `TimeEps` | C 7.7; core-fishing T7 |
| B-Q3 | Reuse the M1 `Core/Input/FightInput.cs` | C 7.7; core-fishing T7 |
| B-Q4 | Literal spec 9.5 side-run choice: 75% away, else a random side, so 87.5% away / 12.5% toward, one draw | C 5.2, C 7.7; core-fishing T7 decision 3 and code |
| B-Q5 | Hand-check test data: FlipRate 0 on Runner/Bulldog/Thrasher copies, weightT 0.375 | C 7.7; core-fishing T7 |
| B-Q6 | Same-tick ending order Landed > Snapped > Pinned > ThrewHook | C 7.7; core-fishing T7 |
| B-Q7 | A flip is allowed on the first burst tick; the minimum gap applies only between flips | C 7.7; core-fishing T7 |
| B-Q8 | Spec-literal apex (floor before the pitch multiplier); the test fixes the multiplier at 1 | core-fishing T5 test; unchanged in C 7.5 |
| B-Q9 | Host Cancel in Hooked resolves Escaped(Dropped), announced Lost, no refund | C 7.9; core-fishing T9 |
| B-Q10 | T17 calls `EnsureSlot` before any `RequestCast` (unknown slot -> `UnknownPlayer`) | C 7.9; net T17 (`OnPlayerReady`) |
| B-Q11 | Disconnect does not refund a Waiting cast's bait | C 7.9; core-fishing T9 |
| B-Q12 | SUPERSEDED by E-Q5 | - |
| B-Q13 | Size class only on Snapped | C 7.8; core-fishing T8 |
| B-Q14 | T17's override builder starts from `RollOverrides.None` (`ForcedItemIndex` -1) | C 7.9, C 7.17; net T17 |
| B-Q15 | An exhausted fish stays Calm: no drift, no draws | C 7.7; core-fishing T7 |
| C-Q1 | `SfxRecipe` 9-arg order with `minDistance` last; `MaxDistance` 0 still means default 30; the cue-table validator compares the EFFECTIVE max (0 -> 30) with `MinDistance`; no M1 value changes | C 7.11, C 7.14; core-support T11; data-tuning 1.9, 2.2, 3.4, 3.7 |
| C-Q2 | `InteractLive(Carrying)` is false in M2 (context unused); M5 revisits spec 8.2 | C 7.10; core-support T10 |
| C-Q3 | `ShaderGlobals` constant names `Palette, Ripples, Waves, WaterTime, ToonShade, ToonAmbient, SkyTop, SkyHorizon, SkyGround` | C 5.3; core-support T12 |
| C-Q4 | Wave direction 0 deg = +Z, dir (sin, cos); `_Waves` = (deg, amplitude, wavelength, speed), slot 3 zero; palette slot in UV1; parity: C# uses the same float-cast time that is pushed to `_WaterTime` | C 5.3, C 7.12, C 7.22; core-support T12 and notes; presentation T22.2 |
| C-Q5 | `RippleMath.Ring(age < 0) = 0`; the T21 shader matches | C 7.12; core-support T12 |
| C-Q6 | `PlayAt` rate limit keyed by id + 2 m cell | C 7.11; core-support T11 |
| C-Q7 | Staggered move+reel overlap is an M5 concern | core-support (note) |
| C-Q8 | `PlayAt` with `pitchMult` serves the charge whoosh | C 7.11; core-support T11, presentation T23 |
| C-Q9 | `AmbienceBed` ignores the cue table; harmless in M2 | core-support (note) |
| C-Q10 | `SettingsData.version` stays 1 | C 7.10; core-support T10 |
| C-Q11 | Partial file `Core/Audio/SfxRecipes.M2.cs` added to T11's owned files | C 2.6, C 12; core-support T11 |
| D-Q1 | `ContentHash` includes a fishing category only when its list is non-empty | C 7.13; data-tuning 2.3 |
| D-Q2 | T14 creates the `TunedDeviations` map; T15 may edit only that map; the controller amends the spec tables after T15 | C 2.6, C 7.15, C 12; data-tuning T14, T15 |
| D-Q3 | Bot `CounterHold = 0.1` (= `CenterDeadzone`) defines CounterReel and Rhythm steering | C 7.15; data-tuning 4.4 |
| D-Q4 | A species model is filled only when the FBX mesh has a colour channel (and, by G-Q6, one submesh) | C 6.2; data-tuning 3.3 |
| D-Q5 | Targets assert at weightT 0.5; the Twig check at 0.5 and 0.9 | C 7.15; data-tuning 4.8 |
| D-Q6 | `TuningItems` map: a listed miss becomes `Assert.Ignore` with numbers; each entry needs controller approval; carried to T30 | C 7.15, C 7.30; data-tuning 4.8; ui-world T30 |
| D-Q7 | `creelMaxKg` only (no grams field) | C 7.13; data-tuning 2.2 |
| D-Q8 | `AssetPaths.ModelsFolder` declared by T14, reused by T20/T29 | C 2.6; data-tuning 3.1 |
| D-Q9 | SfxCueTable growth: OK for M2, note for M3 | data-tuning Q9 |
| D-Q10 | T15 reports `FG TIME`; the controller decides timeout versus seeds above 90 s | data-tuning 4.12 |
| E-Q1 | T17 edits `BuildInfoTests.cs` (expectation 1 -> 2) | C 2.6; net T17 |
| E-Q2 | T17 owns `Fishing/AssemblyInfo.cs` (exists with `AssemblyMarker`; add `InternalsVisibleTo`) | C 2.6, C 12; net T17 |
| E-Q3 | The two movement tests go in T19's `FishingSoloTests.cs`; `PlayerMovementTests.cs` stays T3-only | C 7.19, C 10.4, C 12; net T19 |
| E-Q4 | Rig materials: prefer the new, else the M1 fallback; palette slots WoodDark 4, Accent 10, Snow 11 | C 7.19; net T19 |
| E-Q5 | `RodStateMachine.IgnoredEventCount` is a public getter (T8); the controller logs ignored events in debug builds | C 7.8, C 7.18; core-fishing T8; net T18 |
| E-Q6 | `Accepted` is false only for orphans; T17 acts on `Announce`, `AddToCreel`, `Outcome` (announce iff `Announce != None`; creel add iff `AddToCreel`; `FishCaught` iff Landed and added) | C 7.9, C 7.17, C 8.1, C 9; core-fishing T9; net T17 `Commit()` |
| E-Q7 | Insane roll: the owner raises `CastResultReceived(valid:false)` immediately and sends `CancelRpc` once | C 8.2; net T17 |
| E-Q8 | `CatchAnnouncedRpc` gets `DeferLocal = true` (commit-then-announce on every peer incl. the host) | C 8.2; net T17 code, N2, acceptance |
| F-Q1/Q2 | Wave direction, `_Waves` packing and palette UV1 recorded in the contract | C 5.3, C 7.12; presentation F1, F2 |
| F-Q3 | Toon shader treats vertex colours as LINEAR (supersedes G-Q7); fish FBX exported `colors_type='LINEAR'`; `FishFallbackMesh` linear | C 2.8 #19, C 7.20, C 7.23, C 7.29; presentation F3, T23; ui-world T29 |
| F-Q4 | If the `-nographics` shader check fails, T20 owns a minimal `Tools/unity.ps1` change | C 2.6, C 7.20, C 12; presentation T20 |
| F-Q5 | T23 may add a block of up to 12 lines in `AddPresentation` | C 2.6, C 7.23; presentation T23 |
| F-Q6 | Whitelisted test files: `ToonMaterialTests.cs` (T20), `RenderingSetupTests.cs` (T21), `PresentationWorldTests.cs` (T22), `FeedbackSoloTests.cs` (T23), `PlayerPresentationRigTests.cs` and `RodViewSoloTests.cs` (T24); `test-play` from T22 on uses `-TimeoutMinutes 15` | C 2.4, C 12; presentation 0.4 and task headers |
| F-Q7/Q8 | `FishingPlayModeDriver` member names frozen after T19; `RigMaterial` fallback confirmed | C 7.19; net T19; presentation |
| F-Q9 | Toon keyword subset accepted | presentation T20.4 |
| F-Q10 | Unspecified looks accepted; the user judges them at T28 | presentation |
| F-Q11 | `_WaterTime` float drift accepted (parity rule above) | C 5.3; presentation T22 |
| F-Q12 | T22 reports any M1 swim or water-height test broken by waves; T28 fixes it under the integration-fix rule | presentation T22 |
| G-Q1 | T25 formats toast text itself and publishes via the passthrough key `toast.raw` = "{0}" | C 7.25, C 14 DA10; ui-world T25 |
| G-Q2 | Extra keys added, with M1's `settings.*` prefix instead of `set.*` | C 7.25; ui-world T25 |
| G-Q3 | `toast.lost` is the generic "It got away!" (covers snap and escape); no wire change | C 7.23, C 7.25; ui-world T25 |
| G-Q4 | Assist turned on while the gauge is Off sets Arc, every time | C 7.10, C 7.25; core-support T10; ui-world T25, T30 |
| G-Q5 | RunSeed is display-only; the misleading constant comment is fixed | C 5.2, C 7.26; ui-world T26 |
| G-Q6 | Fish models are ONE submesh; `Validation_SpeciesModels_Pass` also checks `subMeshCount == 1`; the perch is replaced in T29 | C 2.8 #19, C 6.2, C 7.14, C 10.3, C 7.29; data-tuning T14; ui-world T29 |
| G-Q7 | SUPERSEDED by F-Q3 | - |
| G-Q8 | The M2 perf numbers become the first baseline | C 7.30; ui-world T28, T30 |
| G-Q9 | T28's fingerprint test and T29's lineup script are temporary and uncommitted | ui-world |
| G-Q10 | T28's mesh paths live in `LakeDockLayout.cs` | C 2.6; ui-world T28 |
| G-Q11 | Manual checks only for T25/T26 | ui-world |
| Carry-over (T1 review) | T11 adds AudioBus tests for the unknown-id and missing-clip warn-once gates | C 2.6, C 10.1, C 12; core-support T11 |
| Process (lockfile) | Locked means HELD; stop only if `unity.ps1` reports LOCKED; a leftover `Temp/UnityLockfile` is not a lock (CLAUDE.md update needs the user's OK) | C 2.8 #11; every section plan's lock note |
| Fish models staged | `Art/Export/Fish_*.fbx` (source `Art/Blender/fishgame_m2_species.blend`): T29 imports and verifies; the new perch replaces the spike perch | C 6.2, C 7.29, C 12; ui-world T29; data-tuning T14; presentation T20.7 |
| R65 / M2-D23 | Bad-luck nudge (section 13) | C 5.3, C 7.5, C 7.6, C 7.7, C 7.8, C 7.9, C 7.17, C 7.18, C 7.25, C 7.27, C 8.2, C 9, C 10.1, C 13; core-fishing T5-T9; data-tuning 1.1; net T17, T18, T27; ui-world T25, T26, T30; presentation T23 |
| BF-1 | The machine's `Cue()` helper sets `SizeClass` on the Snapped cue from `Roll.SizeClass` (only Snapped, consistent with B-Q13), so the owner's local snap silhouette scales like the remotes' | C 7.8; core-fishing T8 (code, decisions 7 and 13, test); net T18 (comment); presentation T23 |
| BF-2 | Terminal states (Snapped, Escaped, Landed) play their one-shot rumble pulse; continuous rumble is 0 on terminal states and the output is forced to 0 only after the pulse ends; Idle, focus loss and disable zero at once. The controller no longer calls `SetRumble(0, 0)` on terminal states | C 7.18, C 7.23; net T18; presentation T23 |
| BF-3 | The rod rig has two tip transforms: static `TipAnchor` (read by `FishingRodController` for `RodTipPosition`, the fish point, `TipToBobber`) and `TipVisual` (bent by `RodPresenter`, `RodPresenter.Tip`, the line start of `LineView`). `PlayerRigPaths.RodTip` is replaced by `RodTipAnchor` and `RodTipVisual` | C 6.3, C 7.16, C 7.18, C 7.19, C 7.24; net T16, T18, T19; presentation T23, T24 |
| BF-4 | Every non-orphan `CommitDecision` carries `Roll = ActiveCast.Roll` (Cancel and Tick-timeout decisions too, so T17's Lost announcement reads `d.Roll.Catch`); orphans carry default | C 7.9; core-fishing T9; net T17 |
| BF-5 | Exactly one sender of `CancelRpc` after an insane roll: the owner-side `CastResultRpc` handler in `FishingRodNetwork` (T17), which also raises `CastResultReceived(valid:false)`. The machine and T18 only react (`OnCastResult(false)`) | C 7.8, C 8.2; core-fishing T8 decision 6; net T17, T18 |
| BF-6 | T5 gains three extra tests (`CastMath_ArcPoint_EndpointsAndApex`, `BaitStats_EffRating_ClampsAndTreatsZeroAsOne`, `RodStats_MaxLineOut_AddsSpoolExtra`); T5 is 18 tests. Given to the T5 implementer directly | core-fishing T5 (not edited here) |
| BF-7 | `MeshBuilder` stores a HALF-scale grey `0.5 * (1 +/- faceJitter)` so it fits UNorm8 vertex-colour storage (`FaceJitter.Grey` is unchanged); the T12 test asserts a grey > 0.5 instead of > 1. The Toon shader gets a per-material float `_JitterEncoded` (in UnityPerMaterial): 1 for every MeshBuilder-built mesh (props, lakebed/shore, rod, bobber, body slots, skin, and `FishFallbackMesh` and the snap silhouette, which use the new asset material `M_Toon_FishFallback`; no runtime material instances) gives `vertexFactor = 2 * vc.rgb`; 0 (only `M_Toon_Fish`, the Blender fish models) gives `vc.rgb` as linear albedo. No scale correction or clamping after `ToMesh` (the fallback colour pass and the silhouette tint only multiply the half-scale grey by a colour). `RenderValidators` and `ToonMaterialTests` check that every Toon material except `M_Toon_Fish` has 1 | C 2.8 #19, C 6.2, C 7.12, C 7.20, C 7.23; core-support T12; presentation T20, T20.5, T20.6, T23.1-T23.3; ui-world T28 |
| BF-8 | T30 counts `RodStateMachineTests` as 45 test methods and 55 NUnit results (38 row + 6 rule + `RodStateMachine_Assist_CapturedAtRelease`) | ui-world T30 |

### 15.2 R65 / M2-D23 details that cross tasks
- **Wire:** `FishingRollNet.Flags` bit 0 (`FlagPity`) carries `FishingRoll.Pity` to the owner; the crew announcement is `CatchAnnouncedRpc(slot, default, PityCast, 0)` sent by `FishingHost` when an accepted cast has `Roll.Pity` (new `AnnounceKind.PityCast = 4`; consumers other than the HUD ignore it; `CatchRevealView` does not reveal it).
- **Tuning:** `FishingTuning` and `FishingTuningDef` gain `pityAfterFailures` (seed 3, validator 0..10, 0 = off), `pityStrainMult` (0.85, 0.5..1), `pityGraceMult` (1.5, 1..3), `pityHookWindowBonus` (0.3, 0..1).
- **Telemetry:** `FightEnded.Pity`, `FightLogRow.Pity`, CSV column `pity` (last).
- **Interpretation to confirm:** the rulings say both that a Pity cast resets the count "whatever its outcome" and that cancels "neither count nor reset". The plans let the cancel win: a cancelled (refunded) Pity cast does not consume the pity.
- **Spec:** the controller adds the line to spec section 3.2 (not part of this amendment).

---

## 16. Implementation deltas (T1-T19, 2026-10-12)

What changed between the plan and the code after T1-T19 were implemented and reviewed. Sections 2.6, 5.2, 7, 9, 10.4-10.5, 12 and 13 already carry the inline edits; this section is the index. "Applied" names the task, the commit and the file. Review verdicts: APPROVE unless stated. Commit hashes are on branch `m2-fishing-core`.

### 16.1 Findings and rulings applied

| Id | Summary | Applied (task, commit, file) |
|---|---|---|
| T1 review carry-over | `AudioBus` tests for the `unknown|` and `missing|` warn-once gates (unknown id twice -> one warning) | T11 `AudioBusTests.cs` (`AudioBus_UnknownId_WarnsOncePerId`, `AudioBus_MissingClip_WarnsOncePerId`) |
| T2 review | `UiFlowController` limit < 400 (A-Q2); `IHudExtension.Bind` must never throw (no try/catch in the bind loop) and extension buttons play their own `Ui_Click`; manual smoke list gains steps 3b, 5b, 6b, 7, 8 | C 7.2; carried to T25 (16.2); carry plan T2.7 note |
| T3 follow-up (`1c3b423`) | Harness: reset dev-autostart in `finally`, recover from leaked scenes, capture warnings. Carry to T19: log the original problem BEFORE recovery; recovery scene setup in a catching helper | T19 `0c070a0` `PlayModeHarness.cs` (`PrepareResetScene`; C 2.6). T19-R1 minor: `PrepareResetScene` reuses an existing `FG_TestReset` via `GetSceneByName` (T19-fix) |
| T4 review (`4eb9edc`) | Validators loop all `MeshCollider`s, dispose the `SerializedObject`, keep unloaded scenes via `wasValid` / `removeScene`, check skinned meshes for null meshes | T4 `Validators.cs` |
| T5 review (`339742f`) | `CastMath` NaN/infinity guards (`ChargeNorm`, `Distance`), `ProbeReason` as `!(Depth >= minLandDepth)`; stronger `FishPointMath`, `RodWireQuant` (exact values) and `CastMath` (apex 0.42) tests | T5 `CastMath.cs`, `CastMathTests.cs`, `FishPointMathTests.cs`, `RodWireQuantTests.cs`; C 7.5 |
| T6/T7 review (`96a5f89`) | Test strength: nibble clamp, hand-replayed `Pcg32` draw order, nibble histogram, `CatchMath` skew values, `ForcedItemIndex` throw, side-run direction stats, same-tick ending priority, `TensionCues` exact values. Production: `FishingRollValidation` rejects non-finite or out-of-[0,1] `WeightT` and non-finite nibbles; T14 validator `MaxNibbles <= 3` | T6/T7 tests; C 7.6, C 7.14 |
| T8-R1 | Unsolicited commit while Casting plays the cancel beat (recall), no longer illegal; spec 9.13 LastCast intent wins over the 9.2 row list | T8 `RodStateMachine.cs` (`95e31ee`); C 7.8, C 13 M2-D24; spec 9.2 note (controller) |
| T8-R2 | Any invalid cast result sets `_cancelledCastId = castId` so the host's Cancelled commit is consumed silently | T8 `RodStateMachine.cs` (`95e31ee`); C 7.8, C 13 M2-D25 |
| T8 review tests (`95e31ee`) | Flight-time lower bound, `SendResult` before `SendRodEvent(Escaped)`, `FightReport` echo, pity window at 1.3 s, duplicate commit in a terminal state, unsolicited Cancelled commit emits no Escaped event, `finalReason None` -> Invalid, paused `TickFight`/`OnCastResult`, `ForceEnd` no-op in terminal states | T8 `RodStateMachineTests.cs` (BF-8's counts of 45 methods and 55 NUnit results were written before these tests: T30 recounts from the file) |
| T8 telemetry note | The machine's `ResetCast` wipes Roll/Outcome on Idle: telemetry captures the roll on entering Waiting and records Cancelled when SendCancel/Recall drains to Idle | T18 `FightRecorder.cs`; C 7.8, C 7.18 |
| T8 docs wording | Effect order is "cue first, then SendCastRequest" (decision 9) | C 9 flow 1; core-fishing status note |
| T9 review I-1 | `ValidateCast` rejects a non-finite `YawDeg` as `OriginTooFar`; `TryComputeLanding` false for non-finite yaw; NaN and +Inf tested | T9 `abecb40` `CatchValidator.cs`, `FishingHostLedger.cs`; C 7.9 |
| T9 review I-2 | Fail closed on a non-finite `hostDistanceToLanding`: `Hooked` resolves `Escaped(Invalid)` | T9 `abecb40` `FishingHostLedger.cs`; C 7.9 |
| T9-R1 | Walk-bounded hook line: `ActiveCast.CastDistance` (from the rod tip since R67), `CatchValidator.WalkBoundedLineLength0` (sprint speed, no new tuning; takes the origin -> landing distance `CastTipReach` + `CastDistance`) | T9 `abecb40`; C 7.9, C 13 M2-D26 |
| T9 review minors | `Tick` ignores NaN/negative/infinite dt; `CreelRules` (8, 0, 10, 15000) warns; `CreelLedger` last slot and `ctor(0)` throws; pity rows for Escaped(Timeout)/(Dropped); landing floor with a non-Handmade rod | T9 `abecb40` tests; C 7.9 |
| T9 notes for T30 | Criterion 7 holds against forged messages; owner-written transform limits position checks (friends-only, spec 1.2); a bare-hook reroll by cancelling is accepted design | T30 text (16.2) |
| T8-fix / T10 review | Press F3 in the editor (overlay) and in a non-development build (no-op); `OnAssistChanged` pure helper with an EditMode test (G-Q4); M5: Staggered row must zero Move; optional comment wording at `RodStateMachine.cs:20` | T25, T26, T30 (16.2) |
| T9-fix / T11 review | Docs: ActiveCast.CastDistance (from the tip since R67), `IsFinite`/`WalkBoundedLineLength0`, yaw rule order, Hooked NaN, dt guard, `ApplyCueTable` replaces. M5: knockback must end the cast or the walk bound must add max knockback. T23 brief MUST pass `follow` for 2D loops and `StopLoop` in `OnDisable`. T14 validator rejects non-finite cue values | C 7.9, C 7.11; T23, T30 (16.2); `FishingValidators` |
| T11 follow-up (`2296e1a`) | `AudioBus` rejects non-finite cue values (`BaseVolume`, `MinDistance`, `MaxDistance`, `MinInterval`); `AudioLoop.IsPlaying` uses `Source != null`; `AuditionCuesMenu` counts only real plays (`Play2D` returns bool); optional PlayMode duck test widening; the optional walk-bound latency allowance (SprintSpeed * 0.25) was REJECTED | T11 `AudioBus.cs`, `AudioLoop.cs`, `AuditionCuesMenu.cs`, `AudioBusPlayModeTests.cs`; C 7.11 |
| T12 review (`4385084`) | Winding test for every primitive; `WaveMath` non-finite fields are zero terms (Term and AmplitudeSum); `ShaderGlobals.PaletteLength = 16`, `WavesLength = 4`; `WaterSurface` class comment (HeightAt main-thread only unless a time source is set) | T12 `MeshBuilder`/`WaveMath`/`ShaderGlobals`/`WaterSurface` and tests; C 7.12; T21 (16.2) |
| T14 implementer notes | T21 Water shader must treat ANY non-finite wave field as a zero term; `Editor/AssemblyInfo.cs` InternalsVisibleTo added; `TunedDeviations` key format `tuning.<field>` (creel `tuning.creelMaxWeightG` in grams), `<category>.<id>.<field>` | C 2.6, C 7.15; T21 (16.2) |
| T14 review F1 (`03bb4a9`) | `FishingContentBuilder.BuildAt(contentRoot, registry)` internal; builder tests under `Tests/__Temp/<Fixture>`; `.gitignore`; validator `All()` prefix test uses in-memory sets; no test writes committed Content | T14 `FishingContentBuilder.cs`, `FishingContentBuilderTests.cs`, `FishingValidatorTests.cs`; C 2.6, C 7.14 |
| T14 review F2 (`03bb4a9`) | `Ensure<T>` throws `InvalidOperationException("asset exists but is not a T: path")` when the file or GUID exists but cannot be loaded; tested. Optional: `CheckWater` direction and colour finiteness (done) | same |
| T14-fix re-review minors | `Rooted()` guard; `ModelsFolder` re-root test; rename `Build_` tests to `BuildAt_`; `OneTimeSetUp` cleanup on failure; `ValidationTests` temp prefab under `Tests/__Temp/` | NOT done yet: carried to T29 (16.2) |
| T15 rulings (`5a652bd`, `4a4028f`) | Retuned `tuning.relaxWhileReeling` 0.10 -> 0.20, `profile.thrasher.staminaDrain` 0.09 -> 0.035, `profile.runner.staminaDrain` 0.05 -> 0.035; near-snap recovery >= 40% is a HUMAN fun-gate metric (bots informational: Rhythm bulldog 3%, thrasher 6%, runner 57.5%); T30 fun-gate watches bulldog/thrasher recovery and the hints below | T15 assets, `FishingSeedData`, `TunedDeviations`; spec tables; C 7.15; T30 (16.2) |
| T15 review (`f1476c3`) | `FishingBots_RhythmDecide_PinsDiveEasingAndSteerRules` over `Decide`; `CounterHold == CenterDeadzone`; clear static caches in `[OneTimeSetUp]`; creep gate on `exhaustedBefore` accepted. Fun-gate: relax 0.20 is the TOP of the 9.12 range; bulldog Rhythm median 37.4 s (floor 35 s); prefer raising bulldog/thrasher `pullStrength` or lowering calm min over relax | `Tuning/FishingBots.cs`; C 7.15; T30 (16.2) |
| T16 review I-1 (`d40534a`) | `PlayerLocomotion` resets the jump buffer while locked; PlayMode test (press jump airborne, lock, land: no jump) | T16 `PlayerLocomotion.cs`, `PlayerHooksTests.cs`; C 7.16 |
| T16 review M-2..M-7 (`d40534a`) | `HandleToolChanged` returns on `previous == current`; `PlayerCreel` throws when `!IsSpawned || !IsServer`, returns false/0 when `!_player.IsServerInitialized`; `DockBell.DebugSellCreel` guards a missing ledger; `PlayerLocomotion` refreshes the context-source cache in `OnEnable`; PlayMode test that `ServerInitialized` subscribers see `Slot` | same; C 7.16 |
| T16-R1 | Slot takeover: `CreelLedger.ClearSlot(slot)`; `SessionState` clears the slot's creel and debug coins when the slot goes to a DIFFERENT persistent identity; the same identity keeps it (R46); both tested | T16 `d40534a` `CreelLedger.cs`, `SessionState.cs`, `PlayerHooksTests.cs`, `CreelRulesTests.cs`; C 7.9, C 13 M2-D27 |
| T16 notes | T19: `FishingRodController` on the PlayerFP root; PlayerCreel runtime tests (mirror after respawn, `ServerTryAdd` refused when full, bell sell); T25: HUD reads `Count` at spawn; swimming interact gate checked in T19/T28 playtest | T19 `FishingSoloTests.cs` (3 tests); T25, T28 (16.2) |
| T17 review (APPROVE) | T25: host-side creel `Changed` and `FishCaught` fire one frame BEFORE `CatchAnnounced`, so the creel HUD must not show species ahead of the announce. T17 minors went to the T18 agent as Job 3 | T25 (16.2) |
| Review of `d40534a`, `dea35f8`, `071dc2d` (all APPROVE) | Mini-fix batch (Sonnet + Sonnet re-review): slot takeover also resets the host ledger slot (`FishingHostLedger.ResetSlot`); `PlayerKey.MaxIdLength` 61; `PlayerCreel.cs:91` comment; `PlayerHooksTests` brace formatting; `FishingHost` UnknownPlayer reply sends `Vector3.zero`; cheap origin-vs-seen distance check before `CastProbe`; controller raises no local Recall/Escaped cue during despawn. T26 brief MUST adopt the host landing x/z after F4/F5. M4 note: `FishCaught` is published even when the creel store was missing | 16.3 (in the working tree); T26, M4 (16.2) |
| T17 follow-up (`071dc2d`) | `CatchValidator.IsOriginFinite` before the probe; `FishingRollValidation.IsSaneCatch` public and used to vet announcements; lazy NetworkManager; ledger-creation failure handling; no-creel-ledger path still announces | T17 `FishingHost.cs`, `FishingRodNetwork.cs`; C 7.6, C 7.17 |
| T18 (`dea35f8`) | Internal helpers `FightRecorder`, `FishPointSampler`, `RodControllerRules` and `RodControllerHelperTests.cs`; `FightEnded` carries `Pity` | C 7.18, C 12 |
| T19-R1 (T19 review, CHANGES REQUIRED) | Rod geometry vs host slack: rod rest rotation points camera-forward pitched up ~35 deg in the owner pose; `HookedLineSlack` re-derived from the measured forward reach of `TipAnchor` (pitch -60..+60) + 0.5 m, rounded up to 0.1 (value TBD); PlayMode tip assertion (`Dot > 0.8`, `tip.y < cam.y + 1.0`); minors: `PrepareResetScene` reuse of `FG_TestReset`; driver surface amended in C 7.19; C 10.4 is 8 tests; dry run walks to the deck end first; optional internal `SpawnPlayerIfMissing` + `InternalsVisibleTo` instead of reflection | T19-fix (after the mini-fix batch; Sonnet + Opus re-review); C 5.2, C 7.19, C 10.4, C 13 M2-D28; net T19 text; T24 (16.2); spec 9.9 note (controller) |
| R67 (`9278b2e`) | Cast distance measured from the rod tip (user decision A, spec R67, 3.2, 9.3): new `FishingConstants.CastTipReach = 2.4` (TipAnchor forward reach at pitch 0, PlayMode-checked); `CastMath.Landing` = origin + yawForward x (`CastTipReach` + distance), the only place the reach is added (owner and host bit-identical); `RodStateMachine.CastDistance`, `ActiveCast.CastDistance` and `FightEnded.CastDistance` (fight-log `castDist`) are distances FROM THE TIP; owner maxLineOut check is `PlayerToBobber - CastTipReach > MaxLineOut` (limit from the tip, full 12 m spool slack kept: Handmade 30 m from the tip, 32.4 m from the player); host `WalkBoundedLineLength0` takes the origin -> landing distance `CastTipReach + CastDistance`; `HookedLineSlack` is 3.1 (stale 1.5 mentions fixed); `BobberView` arc uses tip -> landing; G9 minimum marker is 4.9 m from the player | `9278b2e` `CastMath.cs`, `FishingConstants.cs`, `RodStateMachine.cs`, `FishingHostLedger.cs`, `CatchValidator.cs`, `FishingRodController.cs`, `BobberView.cs`, `CastTipReachTests.cs`; C 5.2, C 7.5, C 7.9, C 7.18, C 13 M2-D19 and M2-D26, spec R67 |
| Final branch review + fix wave (`bc69e55`, `e484abb`, `2c0a787`, `f25c646`, `42ae12b`; Opus xhigh then Opus re-review APPROVE, 2026-10-12) | `FishingFeedback` honours `IPauseState` (solo pause: loops stopped, rumble 0, no clicks/captions/trauma; restart on resume); `RodStateMachine.TickCharging` does not advance the charge on the release frame (validated marker == cast == host landing); host `CatchValidator.IsChargeAccepted` (charge >= `MinChargeNorm` 0.1, NaN rejected) in `ValidateCast` and `TryComputeLanding`; `CastVerdict.AnnouncePity` (first Pity cast of a streak; per-slot `PityAnnounced` cleared where `FailStreak` resets); ledger `Tick` iterates a sorted id `List` (no per-frame allocation); no-host fallback sends `Vector3.zero`; `FishingHost.OnDisable` resets F6/F7. New public: `CastVerdict.AnnouncePity`, `CatchValidator.IsChargeAccepted`. M3 carry (Minor): the drawn marker trails one frame (UpdateMarker before Tick); parity test comment overclaims M-4 coverage; M-3 zero-alloc test lacks a positive control; F6/F7 reset on any Fishing OnDisable (spot changes mid-run in M3+); polish-review notes (ShaderValidatorTests 3 extra cases, HUD fake-null creel lookup, `0.625 x reel` comment in CatchValidator/FishingConstants) | final fix wave; M3 |
| R70 ladders (plan `2026-10-12-m2-rework-ladders.md` L1-L8 plus review fixes; commits `8d46f9c`, `6c52e4b`, `e4b99e3`, `7cf076c`, `40b3e55`, `fb8ae56`, `869d887`, `a0cb2e0`, `6414cd3`, `4c62893`) | M2-D29..D32 above; DA7 retired for ladders; M1 D36 now gates only the ramp foot. Physics fingerprint changed on purpose (T28.1 superseded for these objects): `Exits/LadderL`, `Exits/LadderR` and their `Top` children removed; `Ladders` root added with `LadderL` / `LadderR` (BoxCollider trigger, layer 0, centre (+-2.05, 0.1, -41.5), size (1, 3, 1), `LadderVolume`); `Exits` root, `Exits/Ramp` and `Exits/Ramp/Top` layer 4 -> 0; `Exits/Ramp` centre (12, -0.9, -44) -> (12, -1.0, -42.5) and size (3, 2.4, 4) -> (3, 2.8, 7); every other collider, spawn and anchor identical. Measured climb-out times (headless): pitch 30 1.62 s, pitch 45 1.25 s, pitch 60 1.10 s; top-out re-entry after 0.250 s. Review fixes: a swimmer leaving a volume at swim depth swims on the same frame (`SwimmingAt`); the rod is held away while airborne or `LadderCatchPending` (`EffectiveAway`). Notes: `CharacterController.velocity` reads hundreds of m/s at the headless frame rate (ramp tests sample root height instead); the bed beside the dock ladders (about -1.28 m) sits at the swim threshold, so swim-depth tests use a test volume in deep water. | ladders plan; `PlayerLocomotion`, `MovementMath`, `ClimbGate`, `RampAssist`, `ToolStowAnim`, `LadderVolume`, `WaterExit`, `InputContextRouter`, `RodControllerRules`, `FishingRodController`, `RodPresenter`, `LakeDockBuilder`, `LakeDockLayout`, `Spot_LakeDock.unity` |
| R72 fight rework (plan `2026-10-12-m2-rework-fight.md`, F0-F8) | `FightSim` rebuilt on Rust's server maths (spec 9.5, R72). Five fight styles kept as data quirks on the Rust tick (user, 2026-10-12): per-profile rest/run ranges and back chance, mid-run flips (`FlipRate`/`FlipMinGap`), `BurstRamp`, `CalmDrift`/`CalmDriftRamp` (scaled by 1 - tire01), `LateralJitter` (per-run speed multiplier, side and back), new `WavePeriod`. Public surface: `FishFightProfile` + `StrainMod`, `MoveMult`, `ReelInMult`, `WavePeriod`; `FishingTuning` + `StrainBarFull`, `StrainSnap`, `StrainRestInput`, `StrainReelRun`, `StrainReelBack`, `StrainCounterSteer`, `StrainOtherSteer`, `SideMoveSpeed`, `BackMoveSpeed`, `TireSteerRate`, `TireCounterRate`, `TireDecayRate`, `TireRunFull`, `TireReelFull`, `TireReelMaxMult`, `BackPullMargin`, `FightTimeout`, and - the 13 retired fields (F7); `FightState` - `FishStamina`, `PinnedTimer` + `Strain`, `Tire`, `Back` (`Tension` = Strain / StrainBarFull, `SnapTimer` = seconds over the full bar); `FightSim` - `WeightFactor`, `EffectivePull`, `Spooled`, `BurstLateralMult` + `SnapTension`, `PullWave`, internal static `Wave(t, period)`, internal `RunSpeedMult`, `ForcePhase(phase, burstDir, back, duration)`; `RodWireQuant.FlagRight = 64`; `RodControllerRules.HookedFlags(phase, burstDir, back, reeling, exhausted, snapTimer)`; `CatchValidator.MaxReelSpeed`, `CatchValidator.OwnerTimeoutPlausible`; `TensionCues.CreakGain` + `TensionCueState.CreakOn`; new Core `FightViewMath`; `FishingConstants` + `PullWavePeak`, `RustWavePeriod`, `PullWaveFloor`, `SteerDigitalThreshold`, `LateralMinRadius`, `CounterSideMult`, `ReelBackMult`, `SideRunLeftChance`, `HostTimeoutSlackSeconds`, `NearSnapHigh` 0.8 -> 1.0, - retired constants (F7). Ledger: the Landed floor and clean-claim (4) use `MaxReelSpeed`; an owner Escaped(Timeout) is accepted when host elapsed >= FightTimeout - 2 s. Tests: `FightSimTests` rewritten (research 8.7 numbers on quirk-free profiles, plus one `FightSim_Quirk_*` test per quirk), `FightMotionTests` deleted (its role moves to the quirk tests), `FightViewMathTests` added, `FishingBots` retargeted with a five-style signature test. Superseded contract text: C 7.5 (`FishFightProfile`, `FishingTuning`), C 7.7 (whole block), C 7.13 (`FightProfileDef`, `FishingTuningDef` field lists), C 7.15, C 10.1 T7 rows, C 10.2 pinned/slack rows (renamed tests), C 15 B-Q4, B-Q5, B-Q15; M2-D15 re-mapped (R63 motions become R72 quirks with the 9.6 values). B-Q7 (a flip may happen on a run's first tick) still holds. | F1-F7 commits (hashes filled in by F8) |

### 16.2 Carry-forward into the not-yet-implemented tasks (also written into the task texts)

| Task | Carry-forward | Where |
|---|---|---|
| T21 | The Water shader treats ANY non-finite wave field as a zero term (the guard lives in `FG_WaveTerm`, T20.3); `PaletteVectors(season)` allocates, never called per frame | presentation T20.3, T21 block, T22.1 |
| T23 | Every loop passes `follow` (2D loops too) and all loops `StopLoop` in `OnDisable`; no cues while despawning or disabled | presentation T23 block |
| T23 review (CHANGES REQUIRED 752ddf9/7860a34; fix `d4483c3` APPROVE, 2026-10-12) | HookSet clicks chained +0.055 s from the previous fired click (Reel_Click minInterval 0.05 kept); deterministic loops test via `OwnerSetView` incl. leave-Hooked; silhouette pose in LateUpdate and not scaled by cameraMotion; `FishFallbackMesh` does not cache registry-null / bad-index results (16-entry ring for uncached meshes); contract aligned to the existing CatchReveal world pose at order 210 (code from 752ddf9, no change); crack: `RodWireQuant.FlagGrace` = 32 + `Line_Crack` SFX id/recipe consumed by `FishingFeedback` on the rising edge, flag also feeds `TensionCues` `inGrace`. Shared edits outside T23's row: T19 `RodControllerRules.HookedFlags` + `FishingRodController.PublishView`; T11 `SfxIds`, `SfxRecipes.M2.cs`; `Content/Audio/SfxCueTable.asset` row appended (equals `CueRows()` output); EditMode tests RodWireQuant, RodControllerHelper, SfxRecipes, AudioBus. Exception to C 2.8 #8 / F-Q6: new `Tests/EditMode/FishFallbackMeshTests.cs` reaches Presentation by `Type.GetType` (no asmdef reference). T24 `LineView` shimmer reads `FlagGrace` (remote tension is smoothed) | T23-fix; C 5.x RodWireQuant, C 6.3, C 7.11 SfxIds/SfxRecipes, C 7.23, M2-D10; T24 (16.2) |
| T24 review (`063bb35`, CHANGES REQUIRED: tests only; production approved, 2026-10-12) | Deltas accepted: PlayMode test `RodViewSolo_CastToLand_*` is `RodViewSolo_CastToWaiting_*` (ForcedBiteWait 30 + recall; Landed-hide covered in the Line test); `BobberView` re-baselines on every `OnEnable` (fresh snap, never a replay) and has a steady-spot fallback that places (never arcs) the bobber when a line-out state arrives while hidden; Escaped (non-recall) line sag 0.25*L (spec 9.11 "line goes slack") instead of 0.15; rod shake is on/off by cameraMotion > 0 (plan). Fix batch: mid-flight arc assert threshold 0.5 m (or Time.time-derived), window asserts sample "shown on at least one frame in the window", Nibble burst startSize ~0.025, optional shake-off test. Controller shared edit: `PlayerBody.RemoteRightArmRaiseDeg` 70 -> 90 (M1/T16 file; remote rod at 35 + pitch like the owner; purely visual) + `PlayerFishingRigBuilder.cs:33` comment | T24-fix; C 7.24; T16 `PlayerBody.cs`; MPPM check u09 |
| T25 review (`73a7ca6`, APPROVE pending gate re-run, 2026-10-12) | HUD builds `new CreelSnapshot(Count, TotalWeightG, maxEntries, maxWeightG)` from the replicated mirror because `PlayerCreel.Snapshot` is host-only (`RequireServer`): plan T25.4 item 5 (`_creel.Snapshot(...)`) is wrong for clients; `_rod` unused (gauge reads the owner `Machine.Fight`, same-frame, grace from `SnapTimer > 0` like `FlagGrace`); bar gauge 160x12 with an 8 px track; Landed and Lost share the per-slot crew toast limit (1 per 3 s, unscaledTime); `FishingHudRules.OnAssistChanged` internal static in the presenter file; exception to C 2.8 #8 / C 12: `Tests/EditMode/FishingAssistRuleTests.cs` reaches the UI assembly by reflection. Fix batch minors: `ReferenceEquals` creel-owner compare; `UiQuery.Req` for required elements (logged via the Bind try/catch); clear caption on leaving the game; reset `_creelClass` in `Unbind` | T25-fix (with T24-fix); C 7.25; plan T25.4 |
| T26 + landing race (`a8ebe2d` CHANGES REQUIRED tests only, production approved; `50569f3` APPROVE, 2026-10-12) | `FishingRodController.HandleCastResult` adopts the host landing x/z (keeps `FlatHeight`) when the machine will accept the result; pure `RodControllerRules.AdoptHostLanding` + `LandingMismatchLogMeters` (0.05 m, `LogOnce` in debug builds). Fix batch: solo PlayMode race test (force `FishingHost.HandleSelectRod` mid-Charging, assert `ViewState.BobberPos` == ledger landing); `FightEnded.CastDistance` from the adopted landing. T26 delivered as planned: overlay 7 lines, 10 Hz while visible, hook `if (Debug.isDebugBuild) AttachHudExtension(...)`, keys via `IDevInput` (gated off while menus are open), F8 unbound | `a8ebe2d`, `50569f3`; landing-fix follow-up; C 7.26 |
| T27 (`1fed9fa`) | `FightLogRow`/`FightLogCsv` (Core) + `FightTelemetry` on the `Fishing` root; extra test `FightLogCsv_Format_NullAndControlCharsInStrings`; real `fight-log_fgtest.csv` verified (header + Landed/Escaped/Cancelled rows) | T27 |
| T28 (`4096335` APPROVE, `47b4083` APPROVE, 2026-10-12) | Menu scene camera `renderPostProcessing` (integration fix in `SceneBuilder.cs`); LakeDock rebuilt with the physics fingerprint identical to M1; `LakeDockMeshes` saves every mesh via `CopySerialized` in place (GUIDs kept, lakebed/shore vertex stride 24 -> 48); water grid 96 x 1.25 m, shadows off; fog ExponentialSquared 0.008 constant from `RenderingSetupBuilder`; WarmVolume/CoolVolume global weight 0 (driven by M3 DayNightController). Ruling (spec 8.2 Swimming row: E = climb at exits only): `PlayerInteractor` must not be live while Swimming (T16 code made it live; a ladder climb could also ring the bell and, in debug builds, sell the creel) - fix batch after T29 with T28 minors (pine canopy inside its 0.5 sphere, dead palette null-guard, unused `PlankThickness`) | T28; swim-gate fix (`PlayerInteractor`/`InputContextRouter`, T16 files, integration-fix rule) |
| Swim interact gate (`a0eb655`, `8320ad5`; T28 minors `93b7637`, `f5b06fa`; T29 `d570c9f`, `c67d0ba`, 2026-10-12) | `InputContextRouter.InteractLive` is Free, RodReady, RodWaiting only (Swimming dropped; `Route(Swimming)` still passes Interact so `PlayerLocomotion.TryBeginClimb` climbs); `PlayerInteractor` `[DefaultExecutionOrder(100)]` after `PlayerLocomotion` (0) so a wading ladder-climb press never reaches the bell; order chain -1000 adapter / -100 rod / 0 locomotion / 100 interactor / 200 body pinned by `PlayerUpdateOrderTests`; `SwimInteractGateTests` (3 PlayMode). T29: models imported, `FishingContentBuilder.RootedPath`/`ModelsFolderAt`, internal folder overloads on `Validators.MeshReferences` and `RenderValidators.ToonMeshesHaveVertexColours` | M1 `InputContextRouter.cs`, `PlayerInteractor.cs` (integration fixes); T28; T29 |
| T24 | `RodPresenter` never moves `TipAnchor`; the rod rest pose is the T19-fix's | presentation T24 block, C 7.24 |
| T25 | `OnAssistChanged(data, old, new)` pure helper + EditMode test; HUD reads creel `Count` at spawn; creel HUD shows no species before `CatchAnnounced`; `IHudExtension.Bind` never throws; extension buttons play their own `Ui_Click` | ui-world T25 block, C 7.25 |
| T26 | F4/F5 rod/bait switch: the controller adopts the host landing x/z on a valid result (or `LogOnce` when > 0.05 m); F3 manual check in the editor and a non-development build | ui-world T26 block, C 7.26 |
| T27 | Consume `FightEnded` from `FightRecorder`; never derive rows from `FishCaught` | net T27 block, C 7.27 |
| T28 | Fix any M1 swim/water-height test broken by waves (integration-fix rule, F-Q12); swimming interact gate playtest | ui-world T28 block, C 7.28 |
| T29 | T14-fix minors (`Rooted()` guard, `ModelsFolder` re-root test, `BuildAt_` renames, `OneTimeSetUp` cleanup, `ValidationTests` temp prefab under `Tests/__Temp/`); import the staged `Art/Export/` models | ui-world T29 block, C 7.29 |
| T30 | Criterion 7 wording; bad-luck nudge USER playtest line; near-snap recovery >= 40% is a human fun-gate metric with tuning hints; Twig thrasher trophy median about 93 s; F3 manual check; M4 note (`FishCaught` without a stored fish); M5 notes (knockback vs walk bound; Staggered row must zero Move) | ui-world T30 (criterion 7 row, fun-gate steps 6-7, H, section J), C 7.30 |
| R70 rod hold (ladders plan, review fixes; M5) | `PlayerLocomotion.OnDisable` never clears `IsGrounded`, and `RodControllerRules.EffectiveAway` holds the rod away while `LadderCatchPending` (grace running or capsule centre inside a `LadderVolume`). A future `LocksMovement` source (M5 carry, Staggered) on a player who stands inside a volume would keep the rod away until they step out; check this when M5 adds sources. | M2-D31, ladders plan L8 |

### 16.3 Pending work that changes code the contract describes

- **Mini-fix batch after T19 (before T20; in the working tree at the time of writing, the controller commits):** `FishingHostLedger.ResetSlot(int) -> bool`, `SessionState.SlotTakenOver`, `FishingHost` subscription and re-mirror, `CatchValidator.OriginWithinSlack` before the probe, UnknownPlayer reply with `Vector3.zero`, `PlayerKey.MaxIdLength = 61`, no local Recall/Escaped cues in the controller's despawn path, `PlayerCreel` comment, `PlayerHooksTests` formatting, `Fishing/AssemblyInfo.cs` PlayMode InternalsVisibleTo. Documented inline in C 2.6, 7.9, 7.17, 7.18.
- **T19-fix (after the mini-fix batch; ruling T19-R1, C 13 M2-D28):** rod rest rotation, `HookedLineSlack` re-derivation (value TBD), tip assertion, shared `TearDown(rig)` driver helper, `PrepareResetScene` reuse. C 5.2 and C 7.9 follow the new constant.
- **Dev-key rod switch mid-cast (residual, dev keys only, M2-D18; for T30 notes):** only the landing is reconciled; the owner machine keeps the OLD rod for the cast. Slower rod (deepsea -> twig): the host `LandedTooFast` floor uses the new rod's `ReelSpeed`, so an honest long-line land can come back Escaped(Invalid). Longer rod (twig -> deepsea): old `MaxLineOut` < host landing, so the rod recalls at Waiting entry with a refund (clean host-wins). Bobber may visibly slide after adoption and the owner's landing splash/ripple can sit at the old spot when the round trip exceeds the flight. Accepted for M2 (dev only).
- **Contract changes that need the user:** none. CLAUDE.md "Before any batchmode run" line (held, not merely present; `unity.ps1` exit 2 = LOCKED) needs the user's OK because it is their file.

### 16.4 Process notes (for the handoff and CLAUDE.md update)

- **Lockfile:** Unity leaves `Temp/UnityLockfile` behind after a batch run (for example a compile-error abort). Locked means HELD (an exclusive open fails), not merely present. `Tools/unity.ps1` already tests held-ness (exit 2 = LOCKED). Implementer briefs say "stop only if `unity.ps1` reports LOCKED". The CLAUDE.md line needs updating (user's file: ask first).
- **Mutation rule (every future implementer brief):** for each test added, name the plausible wrong implementation it catches; for key formulas and orders, temporarily break the code once and show the test fails. Tests that only assert no exception or compare the same code run twice do not count.
- **Tool discipline (every future implementer brief):** run `Tools/unity.ps1` ONLY through the PowerShell tool (foreground, timeout 600000 ms), never through Bash; create and edit files ONLY with the Write/Edit tools, never heredocs or Python splicing; a red run that is expected says "expected red" in the description.
- **Review follow-up cadence:** small fixes (50 lines or less in one area) get a Sonnet re-review; fixes touching ordering or networking (the T8-fix, T9-fix, T16-fix, T19-fix) get Opus. Follow-ups are separate `fix(<file>): <what>` commits (skeleton section 2).
