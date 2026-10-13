# FishGame M3 - Plan Section F: Presentation (T18, T19)

- **Date:** 2026-10-13. **Branch:** `m3-fish-ecology`.
- **Status:** written 2026-10-13, after the user approved the R74 look (spec R74). T18 and T19 wait for T4 (the look-dev adoption: DONE, d660ee9), T8, T14 and T17 (serial order in the skeleton section 3). **CCR-F1..F4 were ACCEPTED as ruled (2026-10-13; F4 conditionally) and are applied to the contract as M3-D78..D81.**
- **Contract (binding, "C" below):** `docs/superpowers/plans/2026-10-13-m3-interfaces.md`, as amended to M3-D91. The M2 contract is "C2". If this file disagrees with C, C wins; the changes this section needed are listed under "Contract change requests" and are now applied (M3-D78..D81).
- **Skeleton:** `docs/superpowers/plans/2026-10-13-m3-plan-skeleton.md` (rows T18 and T19, section 6 row F, section 7).
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` 10.1-10.4 (palette, `DayNightController`, fog readability), 10.6 (M5 event looks; M3 ships only the M3-D21 subset), 11.7 (audio recipe rules), 12.7 (hotspot), 12.8 (`WorldState`), 17.3 bullet 5, R1, R34, R73 (water, unchanged), R74 (look).
- **Look-dev standard (R74):** branch `m2-lookdev` (worktree `C:\Dev\FishGame-lookdev`), merged as T4 (done, d660ee9): `Assets/_Project/Shaders/Toon.shader`, `Editor/Builders/RenderMaterialLibrary.cs`, style guide `docs/superpowers/research/2026-10-12-art-direction-lookdev.md` (sections 2.1-2.3). Water and fog values stay as approved (R74 note: no haze added).
- **Sibling plans used:** section B `2026-10-13-m3-plan-core.md` (T8 `DayNightMath` code, B-D12..B-D14), section C `2026-10-13-m3-plan-data-coverage.md` (1.3 `SunMoonSettings`, 1.4 `WeatherAmbient`, 1.5 `SeasonAmbience` seeds and meanings, 1.7 ranges), section D `2026-10-13-m3-plan-host-world.md` (T14 `WorldState` members, `DevWorldDriver` dev calls), M2 section F `2026-10-11-m2-plan-presentation.md` (format, T22 `PaletteGlobals`, T24 `SplashFx` particle pattern).
- **Audience:** the Sonnet implementer of each task (reads section 0 plus its own task) and the Opus (high) reviewer.

---

## Contract change requests

Each row names the problem, the change asked for, and the controller's ruling (2026-10-13: all four accepted as requested, F4 conditionally; contract rows M3-D78..D81). None changes a public name, path or test name.

| # | Problem | Requested change | Ruling (2026-10-13) |
|---|---|---|---|
| CCR-F1 | **The R74 standard already puts the season on top of textured albedo, through the palette (the T4 hook check).** In the look-dev `Toon.shader` the albedo is `_FGPalette[uv1.x] x vertexFactor x _BaseColor x textureFactor`. Every textured environment material (`M_Toon_Base`, uv0-mapped, and `M_Toon_Tri`, world-triplanar for rocks and canopies; both: `_UsePalette` 1, the env detail array at `_FGArrayGain` 2) gets its hue from the palette slot; the texture is a low-chroma value map centred on 0.5 (style guide 2.2: "Hue comes from the palette slot or `_BaseColor`, which keeps the M3 season swap ... working"; 2.3: env textures are "value and texture, low chroma"). So swapping `_FGPalette` per season already re-tints textured env surfaces without flattening them, which is exactly 2.8 #27. The full-colour textures (fish skin array, player cloth and skin through `_BaseColor`) are season-invariant by style guide 2.3. A `_FGSeasonTint = season / base` multiply on the palette path would apply the season twice. | Amend M3-D23 and 7.18: "Under R74 the season tint on textured environment albedo IS the `_FGPalette` season swap (palette hue x value-only detail texture). Full-colour painted albedo is season-invariant (style guide 2.3). T18 adds no shader term. `PaletteGlobals` still pushes `_FGSeasonTint` (Vector4[16], `season / Summer` per channel, clamped 0..2, identity without a world) as a dormant hook: no M3 shader reads it. The first task that needs a full-colour, season-following material adds an opt-in per-material term (`albedo *= lerp(1, _FGSeasonTint[slot], _FGSeasonTintWeight)`, weight default 0, non-palette path only) and its test." The 2.6 shader row for T18 becomes "no edit". **Option B (not used; CCR accepted 2026-10-13)** (had the controller wanted the term live in M3): T18 adds that opt-in term to `Toon.shader` now (one `UnityPerMaterial` float `_FGSeasonTintWeight` default 0, the global declared in `FishGameCommon.hlsl`), no material sets it, and the identity push in every path becomes mandatory (an unpushed global array reads as zeros and would render black once any material opts in). | ACCEPTED, primary option (no shader edit; the global is pushed): M3-D78, M3-D23 rewritten. **Verified 2026-10-13 against the merged `Toon.shader` (d660ee9):** albedo = `(_UsePalette ? palette[uv1.x] : 1) x vertexFactor x _BaseColor x texFactor` (header lines 2-3 and 12-16, code 193-195 and 228); `M_Toon_Base` and `M_Toon_Tri` are built with `usePalette` true and the env array at gain 2; the fish, cloth, skin and brass materials use `usePalette` false; no `_FGSeasonTint` anywhere under `Shaders/`. T18 step 0 still re-checks the merged `Toon.shader` and stops if a season term appeared in the meantime. |
| CCR-F2 | **The weather cross-fade sits in the wrong task for T18's own test.** C 7.19 gives `WeatherFx` the 4 s cross-fade, and says "`DayNightController` reads the active `WeatherAmbient`". But T18 lands first, and its required test `DayNight_FogReadableAt25m_EveryPhaseAndWeather` needs the weather looks applied to fog and light. If the blend lived in `WeatherFx`, T18 would have to read it before it exists, and T19 would have to edit T18's lighting. | Amend the 7.18/7.19 comments: `DayNightController` (T18) owns the weather look: it reads `IWorldContext.Weather`, cross-fades the `WeatherAmbient` over 4 s, and applies `lightMult`, `desaturate` and the fog terms. It exposes the blended look to its assembly (`internal WeatherAmbient ActiveLook`, plus `internal Color ParticleTint`). `WeatherFx` (T19) owns the rain particles and the `Rain_Patter` loop, driven by `ActiveLook.rainAmount`. No public surface changes. | ACCEPTED as requested (M3-D79). |
| CCR-F3 | **An M2 PlayMode test breaks once the clock drives the look.** `Tests/PlayMode/PresentationWorldTests.cs` `PresentationWorld_Palette_IsLinearAndAppliedOnce` asserts the `M2Season` palette and the Day phase (`phases[1]`) shade, ambient, sky and fog. From T18 the solo world starts at minute 0 (Dawn) in the season rolled from the fixed dev seed, so the test fails whatever season is rolled. The file is not in T18's manifest. | Add a 2.6 row and a 12 entry: "`Tests/PlayMode/PresentationWorldTests.cs`: T18 (in `PresentationWorld_Palette_IsLinearAndAppliedOnce` only: pin the world first through `DevWorldDriver` - clock stopped, minute 300, weather override Clear, season = `M2Season` - then keep every existing assertion; name kept)." | ACCEPTED as requested (M3-D80; T18.6). |
| CCR-F4 (conditional) | **Rain at session start would break M2 PlayMode tests.** `Tests/PlayMode/FeedbackSoloTests.cs` asserts `HasLoop(audio)` is false at several points (before the fight, after leaving Hooked, while paused); its `FindLoop(bus)` matches ANY `AudioBus` child named `Loop_*`. Every PlayMode test runs inside day 1, segment 0 (minutes 0-239, 3 real minutes) of the fixed dev seed (M3-D57). If that segment is Rain, the `Loop_Rain_Patter` ambience loop exists throughout and those asserts fail. The rain loop deliberately keeps playing while paused (ambience, like the wind). | Only if T19 step 0 finds that day 1 segment 0 of the fixed dev seed is Rain: add a 2.6 row "`Tests/PlayMode/FeedbackSoloTests.cs`: T19 (`FindLoop` with no name skips `Loop_Rain_Patter`: the helper means fishing feedback loops; no other edit)". If the segment is Clear or Fog, nothing changes and the CCR is withdrawn. | ACCEPTED as a CONDITIONAL ruling (M3-D81): it applies only if T19 step 0 finds Rain. |

**For the record (no ruling needed; inside the owning task's files):**
- T19 changes one assertion of an existing test in its own file `Tests/EditMode/SfxRecipesTests.cs`: `SfxRecipes_AllIds_Unique` expects `All.Count` 19; it becomes 20 with the M3 row after M2 (T19.2).
- T19 routes the new id in `SfxRecipes.Generate` (`SfxRecipes.cs`, in T19's manifest). Today unknown ids fall through to `GenerateM2`, which throws.
- T18 adds one extra EditMode test to its own `DayNightMathTests.cs` (`SeasonTint_TexturedEnvMaterial_TakesHueFromPalette`, the CCR-F1 guard) and T19 one extra PlayMode test to its own `WeatherFxTests.cs` (`AmbienceBed_SeasonSets_WinterLouderSummerUnchanged`). C 10: "others may be added".

---

## 0. Read first (every T18-T19 implementer)

### 0.1 Global constraints

Every brief carries C 2.8 by reference (C2 2.8 #1-#21 plus M3 #22-#28). These bite here:
- **C2 #2 ASCII:** C#, HLSL and docs are ASCII. After writing a file, `Select-String -Path <file> -Pattern '[^\x00-\x7F]'` prints nothing.
- **C2 #6 and M3 #24 scenes:** both tasks change both Spot scenes (a new component on the `Presentation` root). From T14 on, a Spot scene's `WorldState` hash follows the scene's fileIDs: **commit each Spot scene file whole, or restore it whole, never a hunk.** `Spot_LakeDock.unity` and `Spot_HarbourPier.unity` are both rebuilt by `build`.
- **C2 #8 reach and C 4:** EditMode tests reach Core, Data, Runtime, Editor, Gameplay and Fishing, never Presentation. PlayMode does not reference Presentation either. See 0.4.
- **C2 #9:** PlayMode fails on an unexpected `Debug.LogError` and must be warning-free. A missing optional reference is a silent no-op (or one `Debug.Log`).
- **C2 #11:** `Tools/unity.ps1` tests the lock. `LOCKED` (exit 2) means stop and report. Never kill Unity, never delete the lockfile.
- **C2 #14:** builders load assets after `NewScene`; `PresentationSceneObjects.AddTo` loads its own assets and finds the scene roots it needs inside the call.
- **C2 #15:** the `Presentation` root stays a plain GameObject (the only in-scene NetworkObject is `World`, 2.8 #24).
- **C2 #17:** Presentation never writes gameplay state. T18/T19 read `WorldState` (`MinuteF`, `HasState`, `Season`, `Weather`, `NetHotspot.Value`) and `IWorldContext`. They never call `DevWorldDriver`, `ServerSet*`, a planner or an RPC.
- **C2 #18:** global arrays are always pushed full length: `_FGPalette` 16, `_FGSeasonTint` 16 (`ShaderGlobals.SeasonTintLength`), `_Ripples` 8.
- **C2 16.2 carry-forwards:** no per-frame allocation (cached arrays and property ids, no LINQ, no string building, no boxing in `Update`); the palette push (`PaletteVectors` allocates) runs on enable, on validate and on a real season change only, never per frame; every `AudioBus` loop passes a `follow` and is stopped in `OnDisable`.
- **M3 #23 host-authoritative ecology:** the look is derived on every peer from replicated state; nothing here decides season, weather, clock or hotspot.
- **M3 #27 textured materials:** seasonal swaps and day-night lighting go on top of the albedo. Day-night drives light, ambient, sky, fog and the two Volumes only, never a material colour (M3-D23; CCR-F1 for the R74 form).
- **Statics:** this section adds none. If one is added, reset it on `SubsystemRegistration` (project pattern).
- **Edits:** Write/Edit tools only. **No `sed`, no Python, no PowerShell text rewriting** (agents slipped `sed` in twice during M2).

### 0.2 Commands

```
$env:FG_OUT = '<your session scratchpad>\fg'
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play -Filter "<regex>" -TimeoutMinutes 15
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play -TimeoutMinutes 15
```
Run them with the PowerShell tool. Run the full `test-edit` and `test-play` in the background (`run_in_background`) and wait for the notification. Expected lines are `FG RESULT <cmd> PASS ...` (M1 contract 11); `build` ends with `ValidationRunner.Run`, so a failing validator prints `FG BUILDALL FAIL`. Logs and results go in `$env:FG_OUT`, never in the repo. Before any PlayMode run, check that no other Unity runs `-testPlatform PlayMode` (the UDP port collides across worktrees). After git commands, delete an untracked `dev/` folder if one appears (LFS hooks).

### 0.3 Section decisions (P1-P10; pinned for T18-T19)

- **P1 Season hook (CCR-F1).** The season reaches textured env albedo through `_FGPalette` (R74). `_FGSeasonTint` is pushed with it, as a dormant hook: `tint[i] = seasonLin[i] / summerLin[i]` per rgb channel (alpha 1), a summer channel below 1e-4 gives 1, clamped to [0, 2]; when the season equals the base (Summer, `ArtPaletteDef.M2Season`) every entry is exactly (1, 1, 1, 1). Both arrays are linearised first (F3 of the M2 plan), so the ratio multiplies linear albedo correctly. With no world (menus, edit mode, before the first sync) the season is `M2Season`, so the push is the identity.
- **P2 Weather look (CCR-F2).** `DayNightController` owns the `WeatherAmbient` blend. Index from the weather mask: Fog bit -> 2, else Rain bit -> 1, else 0 (Clear). This never throws, so an M5 Storm (Rain bit) reads as Rain until M5 adds its look; `EcologyMasks.WeatherColumn` is not used here because it throws on multi-bit masks. A change of index starts a 4 s linear cross-fade of all five fields from the CURRENT blended look (so a change during a fade never pops). The first look after `HasState` becomes true snaps (no fade; a late joiner never watches a 4 s fog roll-in; M2 F6 precedent).
- **P3 `desaturate` meaning.** CPU-side, no new global: the light colour, `_ToonShade`, `_ToonAmbient`, the three sky colours and the fog colour are each lerped toward their own Rec.709 luma by `desaturate` before they are pushed. `desaturate` 0 returns the colour unchanged (branch, not a lerp by 0, so the Clear look is bit-exact). Section C 1.4 pinned the other four fields; this completes the five.
- **P4 One writer per frame.** In play mode, when an enabled `DayNightController` sits on the same root, `PaletteGlobals` pushes only the palette and tint arrays; the phase colours, sky colours and fog belong to `DayNightController`. In edit mode `PaletteGlobals` still pushes the Day phase (scene view), and it never writes `RenderSettings` in edit mode (M2 rule). `_SkyGround` is part of the set: the R74 sky fill reads it, so a Day bounce colour left in place would light shaded sides at night.
- **P5 Clock input.** `DayFraction = DayClock.DayFraction(WorldState.Current.MinuteF)` (the continuous minute; the int `Minute` would step the sun and its shadows every 0.75 s). No world or `HasState` false: hold the Day look at `DayHoldFraction` 0.25 (minute 300: pure Day phase, sun up), which is what M2 showed. `SetPreviewMinute` (play mode only) overrides both until it is cleared with NaN.
- **P6 Late attach.** The `Presentation` root enables before `WorldState` spawns (and a client syncs later still). `PaletteGlobals`, `DayNightController` and `AmbienceBed` therefore attach lazily: while unattached they look the world up at most every 0.25 s (unscaled), and they detach when the instance is destroyed (Unity fake-null on the `UnityEngine.Object`) or a different instance registers. `DayNightController` and `HotspotFx` read `WorldState.Current` (Presentation references Gameplay); `PaletteGlobals` and `AmbienceBed` read `IWorldContext` from `ServiceRoots.Game` (Runtime cannot see Gameplay; C 7.18 names `IWorldContext` for the palette). `Changed` also fires for weather and hotspot changes: consumers act only when the value they use changed.
- **P7 Light rules.** One directional light, the scene root `Sun`; it is never disabled or swapped (URP main light); intensity 0 is how it goes dark. Sun side: rotation `Quaternion.LookRotation(-toSun)`, where `toSun` is `DayNightMath.SunDirection` (the unit vector TOWARD the sun; the light's forward points away from it). Moon side (`MoonWeight > 0`): rotation `Quaternion.Euler(sunMoon.moonEulerDeg)`, colour `moonTint`, intensity `moonIntensity x moonWeight`. The two weights are never both non-zero and are both 0 at the flips (B-D14), so the rotation switch happens at intensity 0 and cannot be seen. `RenderSettings.ambientLight` is not touched: Toon and Water read `_ToonAmbient`, and the particles are unlit.
- **P8 Fog.** Fog stays on (never toggled, spec 10.3). `fog = min(MaxReadableFogDensity, max(phaseFog x look.fogDensityMult, look.fogDensityMin))` with `MaxReadableFogDensity = 0.0333f` (`DayNightMath.IsReadable(0.0333f)` is true, T8 test). The cap is a safety net only: the T18 tests require that the shipped data never reaches it. Phase blends and look blends are convex combinations, so checking the endpoints (every phase x every look) covers every in-between value.
- **P9 Particles and audio.** `WeatherFx` and `HotspotFx` follow the M2 `SplashFx` pattern (runtime `ParticleSystem`s on child objects, `M_Particle`, stop-and-clear before configuring, a null material = silent no-op). Unlit particles would glow at night, so both tint new particles by `DayNightController.ParticleTint`, refreshed at most 4 times a second. The `Rain_Patter` loop is 2D (`spatial` false), so its position is irrelevant: its `follow` is the `WeatherFx` transform itself (C2 16.2: the loop dies with its owner; the camera is NOT used as `follow`, because a camera switch would kill the loop). `WeatherFx.OnDisable` calls `StopLoop`.
- **P10 Execution order.** `WaterTimeDriver` -200 (M2), `DayNightController` -150 (`Update`), everything else default. `WeatherFx` positions its emitter in `LateUpdate` (after the camera moves).

### 0.4 Verification strategy

EditMode cannot reach Presentation, and PlayMode does not reference it (C 4). The tasks are verified by:
1. `compile` and `build` (builders and validators run; `AddTo` throws if a scene root is missing).
2. EditMode data tests on the real assets (`ArtPalette.asset`, `M_Toon_Base.mat`, the recipes).
3. PlayMode tests that drive the world through `WorldState` / `DevWorldDriver` (Gameplay, reachable) and observe only effects: shader globals (`Shader.GetGlobalVectorArray/Color/Float`), `RenderSettings`, the scene root `Sun` (`Light`), the Volumes, child `ParticleSystem`s found by name, the `AudioBus` child `Loop_Rain_Patter`, and `PaletteGlobals.ApplyCount` by reflection (the M2 precedent). Driving pattern (host, solo): `var driver = WorldState.Current.GetComponent<DevWorldDriver>(); driver.DevSetClockRunning(false); driver.DevSetMinute(m); driver.DevSetWeatherOverride(w); driver.DevSetSeason(s);`. Stop the clock before setting minutes: a running clock at 1200 starts a new day (M3-D11).
4. Manual checks by the controller and the user (the F8 scrub key and the dev panel arrive in T22; T25 holds the look checks).

---

## T18 - Day-night and seasonal tint

| | |
|---|---|
| Goal | `DayNightController` (spec 10.3): phases blended on the world clock, the sun/moon flip on the one directional light, `_ToonShade`/`_ToonAmbient`/sky globals, fog with the weather looks and the readability rule, the warm/cool Volume weights, `_FGDayFraction`. `PaletteGlobals` takes the season from the world and pushes `_FGPalette` and `_FGSeasonTint` once per season change (M3-D23, CCR-F1). |
| Owned files (C 12) | `Presentation/World/DayNightController.cs` (new); `Tests/PlayMode/DayNightTests.cs` (new); `Tests/EditMode/Ecology/DayNightMathTests.cs` (add `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable` and the extra `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette`); regenerated `Scenes/Spot_LakeDock.unity` and `Scenes/Spot_HarbourPier.unity` (whole files). No shader edit (CCR-F1). |
| Shared edits (C 2.6) | `Presentation/World/PaletteGlobals.cs` (season from `IWorldContext`, tint array, phase hand-over); `Core/Render/ShaderGlobals.cs` (`SeasonTint`, `SeasonTintLength`, `DayFraction`); `Editor/Builders/Render/PresentationSceneObjects.cs` (`DayNightController` and its references; T19 adds after it); `Tests/PlayMode/PresentationWorldTests.cs` (CCR-F3, one test). |
| Depends on | C 3: T4 (R74 adopted and merged: `Toon.shader` texture arrays, `M_Toon_Base` env look), T8 (`DayNightMath`, `PhaseBlend`), T14 (`WorldState`, `DevWorldDriver`); and T9/T10 data (`ArtPaletteDef.SunMoon`/`WeatherLooks` filled in `ArtPalette.asset`). Note: in the serial order T17 lands first, so the harbour scene already exists and `AddTo` needs its `Sun`, `WarmVolume` and `CoolVolume` roots (C 6.3); if the harbour is not built yet, only the lake scene changes. |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: C2 #2, #6, #8, #9, #11, #14, #15, #17, #18, C2 16.2 (no per-frame allocation, palette push not per frame); M3 #23, #24 (whole scenes), #27 (tint on textured albedo; CCR-F1). Section 0.3 P1-P8, P10.

### T18.1 `ShaderGlobals` (C 5.3; `Core/Render/ShaderGlobals.cs`)

Append, keeping the existing members and comment style (the header comment's list of full-length arrays gains `_FGSeasonTint 16`):
```csharp
public const int SeasonTintLength = 16;                 // == PaletteLength; always pushed full length (C2 2.8 #18)
public const string SeasonTint = "_FGSeasonTint";       // Vector4[16]: season / base per palette slot (M3-D23, CCR-F1)
public const string DayFraction = "_FGDayFraction";     // float 0..1, DayClock.DayFraction of the world minute
```

### T18.2 `PaletteGlobals` (shared edit; `Presentation/World/PaletteGlobals.cs`)

Keep `[ExecuteAlways]`, the authority comment, the serialized `palette`, `Apply()`, `ApplyCount` (internal diagnostic; tests read it by reflection) and the M2 linearisation. Changes:
- **Season source.** `SeasonToPush()`: the attached `IWorldContext` with `HasState` true -> `(int)world.Season`; otherwise `def.M2Season` (menus, edit mode, before the first sync: M2 behaviour). `Apply()` pushes the palette of that season and records it in `_pushedSeason`.
- **Tint array (P1).** A second cached `Vector4[SeasonTintLength]`. In `Apply()`, after linearising the season's palette, linearise the base season's palette (`M2Season`, Summer) and fill `tint[i]` per P1; when the season equals the base, write exactly (1, 1, 1, 1). `Shader.SetGlobalVectorArray(SeasonTintId, _tint)` right after the palette push, in edit mode too (so a later opt-in material is never black; Option B is not used, CCR accepted 2026-10-13). A season with a magenta (missing) entry follows the palette's M2 rule: the ratio is computed from what was pushed.
- **Phase hand-over (P4).** Phase colours and fog are pushed only when NOT (`Application.isPlaying` and `TryGetComponent(out DayNightController dn)` and `dn.isActiveAndEnabled`). Edit mode keeps the M2 Day push (and still never writes `RenderSettings`).
- **World attach (P6), play mode only.** `Update`: if unattached and `Time.unscaledTime >= _nextLookup` (every 0.25 s): `ServiceRoots.Game` non-null and `TryGet(out IWorldContext w)` -> attach: store it, `w.Changed += HandleWorldChanged`, then call `HandleWorldChanged()` once (state may already be there). If attached but the instance is gone (`w is UnityEngine.Object o && o == null`), detach (unsubscribe through the stored reference, keep the last palette: no pop at unload). `OnDisable` detaches.
- **`HandleWorldChanged`:** `if (world.HasState && (int)world.Season != _pushedSeason) Apply();`. Weather and hotspot changes, and `HasState` turning false at despawn, never re-apply. This is the "once per season change, never per frame" rule; `DayNight_SeasonChange_PushesTintOnceNotPerFrame` pins it.
- `Update` does nothing else and allocates nothing. `OnValidate` still calls `Apply()`.

### T18.3 `DayNightController` (C 7.18; `Presentation/World/DayNightController.cs`)

Public surface exactly C 7.18: `public float DayFraction { get; }` and `public void SetPreviewMinute(float minute)`. Everything else is private or internal. First lines:
```csharp
// Authority: local only (every peer); reads IWorldContext (minute, season, weather), never writes gameplay state.
// Spec 10.3 day-night look on the world clock (M3-D21, D23, D24; section F plan T18, P2-P8). Owns the weather look blend (CCR-F2).
```
- `[DisallowMultipleComponent]`, `[DefaultExecutionOrder(-150)]`, NOT `[ExecuteAlways]` (edit mode belongs to `PaletteGlobals`; this class would otherwise rewrite the active scene's `RenderSettings` during builds and validators).
- Serialized (set by the builder with `SerializedObject`, M2 F8): `ArtPaletteDef palette`, `Light sun`, `Volume warmVolume`, `Volume coolVolume`. A null `palette` falls back to `ServiceRoots.App` `ContentRegistry.ArtPalette` (M2 pattern).
- Constants: `internal const float MaxReadableFogDensity = 0.0333f`, `WeatherFadeSeconds = 4f`, `DayHoldFraction = 0.25f`.
- `OnEnable`: resolve the palette. Cache `float[] _starts` from `Phases[i].startFraction` and the `SunMoonSettings` (by value) and the three `WeatherAmbient` looks (copied into a fixed `WeatherAmbient[3]`). If the palette, the phases (< 1 or not strictly increasing) or the light are missing, or `WeatherLooks.Count != 3`: `Debug.Log` once (not a warning: T9/T11 validators make this unreachable with shipped data) and use what is safe: no light -> skip the light; looks missing -> the neutral look (1, 0, 1, 0, 0); phases missing -> `enabled = false`. Cache every property id.
- `internal WeatherAmbient ActiveLook` (the blended look, for `WeatherFx`) and `internal Color ParticleTint` (computed each frame: the desaturated `lerp(toonAmbient, lightColour, saturate(lightIntensity))`, alpha 1; T19 multiplies particle colours by it).
- `SetPreviewMinute(float minute)`: NaN clears; otherwise `_preview = Mathf.Clamp(minute, 0, 1200)`. Play mode only (the class does not run in edit mode); tests use the world instead (0.4).
- `DayFraction` returns the fraction used by the last `Update`.

**Per-frame evaluation (`Update`; the order is the no-pop contract, write it this way):**
```csharp
WorldState world = WorldState.Current;
bool state = world != null && world.HasState;
float f = !float.IsNaN(_preview) ? DayClock.DayFraction(_preview)
        : state ? DayClock.DayFraction(world.MinuteF) : DayHoldFraction;      // P5
_dayFraction = f;

// Weather look (P2): snap on the first state, else a 4 s fade from the CURRENT blend.
int idx = state ? LookIndex(world.Weather) : 0;
if (!_lookValid || (state && !_hadState)) { _from = _to = _looks[idx]; _fade = 1f; _lookValid = true; }
else if (idx != _lookIdx) { _from = _active; _to = _looks[idx]; _fade = 0f; }
_lookIdx = idx; _hadState = state;
_fade = Mathf.Min(1f, _fade + Time.deltaTime / WeatherFadeSeconds);
_active = LerpLook(_from, _to, _fade);                                        // field-wise linear, no allocation

// Phase blend (M3-D24, B-D12).
PhaseBlend b = DayNightMath.Blend(f, _starts, _sunMoon.blendWindow);
DayPhaseAmbient a = _phases[b.From], c = _phases[b.To]; float t = b.T;
// lerp: lightColor, lightIntensity, skyTop, skyHorizon, skyGround, toonShade, toonAmbient, fogColor, fogDensity, warmWeight, coolWeight

// Sun / moon (P7, B-D14).
float sunW = DayNightMath.SunWeight(f, _sunMoon.sunriseFraction, _sunMoon.sunsetFraction, _sunMoon.flipWindow);
float moonW = DayNightMath.MoonWeight(f, _sunMoon.sunriseFraction, _sunMoon.sunsetFraction, _sunMoon.flipWindow);
Quaternion rot; Color lightCol; float intensity;
if (moonW > 0f) { rot = Quaternion.Euler(_sunMoon.moonEulerDeg); lightCol = _sunMoon.moonTint; intensity = _sunMoon.moonIntensity * moonW; }
else
{
    DayNightMath.SunDirection(f, _sunMoon.sunriseFraction, _sunMoon.sunsetFraction, _sunMoon.minElevationDeg, out float x, out float y, out float z);
    rot = Quaternion.LookRotation(new Vector3(-x, -y, -z), Vector3.up);       // forward points AWAY from the sun
    lightCol = phaseLightColor; intensity = phaseLightIntensity * sunW;
}
intensity *= _active.lightMult;

// Desaturate (P3), fog (P8), then push.
// colours: lightCol, shade, ambient, skyTop, skyHorizon, skyGround, fogColor = Desat(colour, _active.desaturate)
float fog = Mathf.Min(MaxReadableFogDensity, Mathf.Max(phaseFog * _active.fogDensityMult, _active.fogDensityMin));
```
Push: `sun.transform.rotation`, `sun.color`, `sun.intensity` (never `sun.enabled`); `Shader.SetGlobalColor` for `ToonShade`, `ToonAmbient`, `SkyTop`, `SkyHorizon`, `SkyGround`; `Shader.SetGlobalFloat(DayFractionId, f)`; `RenderSettings.fogColor`, `RenderSettings.fogDensity` (never `fog` or `fogMode`); `warmVolume.weight`, `coolVolume.weight` (null-safe). Colours are lerped as authored (sRGB) and pushed with `SetGlobalColor` (Unity converts), the same path as M2's `PaletteGlobals`; the PlayMode checks compare authored values.

**Notes for the implementer:**
- `_starts` is a `float[]` passed as `IReadOnlyList<float>` (no allocation). `Blend` throws on non-increasing starts: that is checked once in `OnEnable`.
- `SunDirection` is non-vertical (elevation <= 60 deg) and `moonEulerDeg` is data, so `LookRotation` with `Vector3.up` is safe.
- `LerpLook` and `Desat` are private static helpers on structs; no `new` of reference types after `OnEnable`.
- When the controller is disabled it pushes nothing; `PaletteGlobals` then owns the phase globals again only after its next `Apply` (enable), which is acceptable (disabling the controller is a debugging action).

### T18.4 `PresentationSceneObjects` (shared edit; `Editor/Builders/Render/PresentationSceneObjects.cs`)

- After `RippleField`: `DayNightController dayNight = BuilderUtil.GetOrAdd<DayNightController>(root);` and `SetReference(dayNight, "palette", palette)`.
- A private helper `T FindRoot<T>(Scene scene, string name) where T : Component`: the root GameObject with that name in `scene.GetRootGameObjects()` and its component `T`; missing -> `InvalidOperationException("Spot scene '<scene.name>' has no root '<name>' with a <T> (C 6.3)")`.
- `SetReference(dayNight, "sun", FindRoot<Light>(scene, "Sun"))`, `"warmVolume"` -> `FindRoot<Volume>(scene, "WarmVolume")`, `"coolVolume"` -> `FindRoot<Volume>(scene, "CoolVolume")`. `SetReference` already handles scene objects (`objectReferenceValue` within one scene).
- The lake builder adds `Sun` and both Volumes before `AddTo` (verified in `LakeDockBuilder.Build`). The harbour builder (T17) must do the same (C 6.3); if it does not, `build` fails with the message above: report it to the controller (a T17 defect fixed under the integration-fix rule by T24), do not edit T17's files.
- Update the class comment: the root now holds four drivers (T19 adds two more).

### T18.5 Tests

**EditMode, `Tests/EditMode/Ecology/DayNightMathTests.cs` (add to T8's file; class and namespace unchanged).**

| Test | Cases | Catches (mutation) |
|---|---|---|
| `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable` (C 10.4) | Load `Content/Art/ArtPalette.asset` (`AssetPaths.ArtPaletteAsset`). Exactly 4 phases with strictly increasing starts; `WeatherLooks.Count == 3`; `SunMoon.blendWindow` in (0, 0.1]. For every phase p and look w: `uncapped = max(p.fogDensity x w.fogDensityMult, w.fogDensityMin)` and `DayNightMath.IsReadable(uncapped)` (the cap must never be what keeps the data readable). Also `DayNightMath.IsReadable(0.0333f)` (the controller's cap constant value) and the Fog look's `fogDensityMin >= 0.025` (M3-D21 "toward 0.03": a fog weather that is not visibly foggy fails). A comment states that phase and look blends are convex, so the 12 endpoint pairs cover every blended value. | Fog look min 0.04 (unreadable) -> red; Night fog 0.03 x Rain 1.2 = 0.036 -> red; a look list of 2 -> red. |
| `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette` (extra; CCR-F1 guard) | Loop over BOTH `M_Toon_Base.mat` and `M_Toon_Tri.mat` (the uv0 and the world-triplanar env materials), loading each through `RenderMaterialLibrary.Get` (Editor) or `AssetDatabase`: shader `FishGame/Toon`; `_UsePalette` == 1; `_FGTexWeight` > 0; `_FGArrayGain` == 2; `_FGAlbedoArray` non-null (name the material in every assertion message). And the real palette's Summer and Autumn colours differ by more than 0.05 in some channel for the Grass (0) and Foliage (1) slots (the swap is visible). | A T4 drift that bakes hue into env textures and turns `_UsePalette` off on EITHER env material (the season would stop reaching textured env props, or only the triplanar rocks and canopies); identical season palettes. |

**PlayMode, `Tests/PlayMode/DayNightTests.cs` (new; class `DayNightTests`; `PlayModeHarness`, `[UnityTearDown]` `TearDownApp`; uses the `LogWatch` helper pattern of `FishingSoloTests` to stay warning-free).** Arrange for every test: `LoadBootstrapAndWaitForMenu`, `StartSoloAndWaitForPlayer`, `WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10 s)`, get the driver (0.4), `DevSetClockRunning(false)`, `DevSetWeatherOverride(Clear)`, wait 4.5 s realtime once so any fade from the rolled weather has finished. Read the palette from the registry (`ArtPalette`). Find the light: `GameObject.Find("Sun").GetComponent<Light>()`.

| Test | Steps and asserts | Catches (mutation) |
|---|---|---|
| `DayNight_ScrubFullDay_NoLightOrFogPops` | For m = 0..1199: `DevSetMinute(m)`, `WaitFrames(1, 0)`, sample the effective light `sun.color x sun.intensity` (per channel), `sun.intensity`, `RenderSettings.fogDensity`, `RenderSettings.fogColor`, `_ToonShade`, `_ToonAmbient`, `_SkyTop`, `_SkyGround`. Every step between consecutive samples is below a bound derived from the data: smoothstep's peak slope is 1.5 per window, so per 1-minute step a phase field moves at most `1.5 / (blendWindow x 1200) x maxAdjacentPhaseDelta(field)` (0.025 x delta with the 0.05 seed) and a weight at most `1.5 / (flipWindow x 1200)` (0.0625); the effective-light and intensity bounds use the product rule (`d(c x i) <= dc x maxI + maxC x di`, with `di <= 0.0625 x maxIntensity + 0.025 x maxIntensityDelta`; the moon side likewise with `moonIntensity`). Each bound gets x1.2 slack + 1e-4. The raw `sun.color` is NOT bounded: it switches from the Dusk colour to `moonTint` in one step at the flip, which is invisible because the intensity is 0 there; the test asserts instead that every raw colour step above the phase bound happens at a sample whose intensity (and the previous one's) is <= 1e-3. Also: `sun.enabled` is true at every sample; `sun.intensity <= 1e-3` at m 0 and at the sunset flip (`m = round(sunsetFraction x 1200)` = 840); at m 420 (noon) `sun.transform.forward` equals `-SunDirection(0.35, ...)` within 1e-3 and `forward.y < -0.8`; at m 1000 `Quaternion.Angle(sun.transform.rotation, Quaternion.Euler(moonEulerDeg)) < 0.1`; `Shader.GetGlobalFloat("_FGDayFraction")` equals `m / 1200` within 1e-4 at every sample. | Using `phases[b.To]` without lerping by `b.T` -> red at every phase start; ignoring the weights (full intensity on both sides of the flip) -> red at 840; `LookRotation(+dir)` -> red (forward points up). Blending on the int minute is invisible here (the test steps whole minutes): a review item. |
| `DayNight_SeasonChange_PushesTintOnceNotPerFrame` | Set a known start: `DevSetSeason(Summer)`, wait 3 frames. Read `ApplyCount` (reflection, the M2 test's helper) as n0; `_FGSeasonTint` all exactly (1, 1, 1, 1). `DevSetSeason(Autumn)`, wait 3 frames: `ApplyCount == n0 + 1`; `_FGPalette` equals Autumn linearised (1e-4); `_FGSeasonTint[i]` equals the P1 ratio of Autumn over Summer, linear (1e-4), length 16. Wait 10 frames: count unchanged. `DevSetWeatherOverride(Rain)` (raises `Changed`), wait 10 frames: count still `n0 + 1`. `DevSetSeason(Autumn)` again (same value): unchanged. | `Apply` on every `Changed` -> red after the weather change; `Apply` per frame -> red; tint pushed in gamma or not at all -> red; tint computed as base / season (inverted) -> red. |
| `DayNight_FogReadableAt25m_EveryPhaseAndWeather` (C 10.4) | For w in {Clear, Rain, Fog}: `DevSetWeatherOverride(w)`, wait 4.5 s realtime (fade done). For m in {60 (Dawn), 300 (Day), 760 (Dusk), 1000 (Night)}: `DevSetMinute(m)`, wait 2 frames; assert `RenderSettings.fog` true, `fogMode == ExponentialSquared`, `DayNightMath.IsReadable(RenderSettings.fogDensity)`, and `fogDensity == max(phase.fogDensity x look.fogDensityMult, look.fogDensityMin)` within 1e-5 (the look is really applied and the cap did not engage). Under Fog also sweep m = 0..1190 step 10: readable at every sample. Finally Clear at m 300: `sun.intensity` equals `day.lightIntensity` (1e-4, lightMult 1) and under Rain at m 300 it is `x 0.85` (1e-4). | Weather looks ignored (Fog density stays 0.008) -> red; `fogDensityMin` applied as a multiplier -> red; `lightMult` not applied -> red. |

**`Tests/PlayMode/PresentationWorldTests.cs` (CCR-F3).** In `PresentationWorld_Palette_IsLinearAndAppliedOnce` only, right after `StartSoloAndWaitForPlayer`: wait for `WorldState.Current.HasState` (10 s), then `DevSetClockRunning(false)`, `DevSetMinute(300)`, `DevSetWeatherOverride(Clear)`, `DevSetSeason((Season)art.M2Season)`, then wait 4.5 s realtime (any weather fade) and 3 frames. Every existing assertion then holds unchanged: minute 300 is the pure Day phase (T = 0), Clear is the neutral look (fog x1, min 0, no desaturation, so fog = `day.fogDensity` exactly), and the season equals `M2Season`. The `ApplyCount` check starts after the pinning (the season set may add one apply), so it still proves "not per frame". Update the test's comment to say why the world is pinned. No other test in the file changes.

### T18.6 Steps (TDD order)

0. **Preconditions and the T4 hook check.** `git log` shows T4, T8 and T14 committed (and T17, in the serial order). In the merged `Assets/_Project/Shaders/Toon.shader`: the albedo is `palette x vertexFactor x _BaseColor x textureFactor`, `_FGTexWeight`/`_FGAlbedoArray` exist, and there is **no** `_FGSeasonTint` term. In `RenderMaterialLibrary.BuildAll`, both `M_Toon_Base` and `M_Toon_Tri` use `usePalette: true` and the env array at gain 2. If T4 added a season term, or env materials no longer take hue from the palette, STOP and report (CCR-F1 then needs a new ruling). Also check `ArtPalette.asset` has `sunMoon` and 3 `weatherLooks` (T10 filled them; `EcologyData_AssetValuesEqualSeeds` is green).
1. Add the two EditMode tests (T18.5). `test-edit -Filter "DayNightMathTests"`. Expected: PASS on the shipped data (they are data guards). Show each red once by a temporary edit of the in-memory copy in the test (do not edit assets): a Fog look `fogDensityMin` of 0.04, and `_UsePalette` 0 on an instantiated copy of the material; restore.
2. Add the `ShaderGlobals` constants. Write `DayNightTests.cs` and the `PresentationWorldTests` edit. `compile`, then `test-play -Filter "DayNightTests|PresentationWorldTests" -TimeoutMinutes 15`. Expected: FAIL (no controller: the sun never moves; no tint array).
3. Write `DayNightController`, the `PaletteGlobals` edit and the `PresentationSceneObjects` edit. `build`. Expected: `FG BUILDALL PASS`. Rerun the filter. Expected: PASS.
4. Mutations (each red once, then restore): `LookRotation(new Vector3(x, y, z))` -> the scrub test; `PaletteGlobals` applying on every `Changed` -> the tint test; drop `fogDensityMin` from the fog formula -> the fog test.
5. `git status`: `Spot_LakeDock.unity` and `Spot_HarbourPier.unity` (when T17 has landed) changed (the new component and its references). Keep them WHOLE (2.8 #24). Restore `Bootstrap`/`Menu` if only churned. Drop the harbour scene from the `git add` below if it does not exist yet.
6. Full `test-edit`, then full `test-play -TimeoutMinutes 15` (background). Every test green and warning-free.
7. ASCII check on every touched file. Commit:
```
git add Assets/_Project/Presentation/World/DayNightController.cs Assets/_Project/Presentation/World/DayNightController.cs.meta Assets/_Project/Presentation/World/PaletteGlobals.cs Assets/_Project/Core/Render/ShaderGlobals.cs Assets/_Project/Editor/Builders/Render/PresentationSceneObjects.cs Assets/_Project/Tests/PlayMode/DayNightTests.cs Assets/_Project/Tests/PlayMode/DayNightTests.cs.meta Assets/_Project/Tests/PlayMode/PresentationWorldTests.cs Assets/_Project/Tests/EditMode/Ecology/DayNightMathTests.cs Assets/_Project/Scenes/Spot_LakeDock.unity Assets/_Project/Scenes/Spot_HarbourPier.unity
git commit -m "m3(daynight): DayNightController on the world clock, sun/moon flip, weather looks, season palette and tint globals" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T18.7 Acceptance (reviewer)

- The public surface is exactly C 7.18 (`DayFraction`, `SetPreviewMinute`); the authority comment is present; `internal` members only for `WeatherFx` (`ActiveLook`, `ParticleTint`).
- `Update` allocates nothing (no `new` of reference types, no LINQ, no string building, no boxing, no `GetComponent`); the palette push happens only on enable, validate and a real season change.
- The minute is `MinuteF` (continuous), not `Minute`. No world -> the Day hold. The first look after `HasState` snaps.
- The light is never disabled; the rotation switches only where both weights are 0; `LookRotation(-toSun)`.
- One writer per frame for phase globals and fog (P4); `_SkyGround` is driven; `RenderSettings.fog` and `fogMode` are never written.
- Fog = `min(cap, max(phase x mult, min))`; the EditMode test proves the shipped data never needs the cap.
- No shader edit (CCR-F1); `_FGSeasonTint` is pushed full length with the identity when the season is the base.
- `PresentationWorldTests`: only the one test changed, name kept, its old assertions intact.
- Both Spot scenes committed whole. All suites green and warning-free.

### T18.8 Manual check (controller, after the commit)

The F8 scrub key, the dev panel and its map switch land in T22, so the full scrub check and the harbour look check are part of T22/T25. Now: enter Play solo on the lake. The day opens at Dawn (warm low light; the sun fades in over about 18 s from minute 0, M3-D77) and blends into Day after about 1.5 min of real time; shadows move smoothly (no steps). Note for T25: night readability of the bobber, the cast marker and the dock edge at 25 m, and the max-fog readability, are user look checks (17.3 bullet 5).

---

## T19 - Weather looks, hotspot VFX, seasonal ambience

| | |
|---|---|
| Goal | The M3 weather looks that are not lighting (M3-D21): camera-local rain particles and the `Rain_Patter` loop, driven by T18's blended look (CCR-F2). `HotspotFx` on every peer from the replicated hotspot (spec 12.7). Seasonal `AmbienceBed` sets (M3-D22). The `Rain_Patter` recipe and its cue row. |
| Owned files (C 12) | `Presentation/World/WeatherFx.cs`, `Presentation/World/HotspotFx.cs`, `Core/Audio/SfxRecipes.M3.cs` (new); `Tests/PlayMode/WeatherFxTests.cs` (new); regenerated `Content/Audio/SfxCueTable.asset` (one row appended by the builder), `Scenes/Spot_LakeDock.unity`, `Scenes/Spot_HarbourPier.unity` (whole files). |
| Shared edits (C 2.6) | `Core/Audio/SfxIds.cs` (`RainPatter`); `Core/Audio/SfxRecipes.cs` (`All` includes M3; `Generate` routes the M3 id); `Runtime/Audio/AmbienceBed.cs` (season parameter sets); `Editor/Builders/Fishing/FishingContentBuilder.cs` (append missing cue rows by id; never modify a row); `Editor/Builders/Render/PresentationSceneObjects.cs` (`WeatherFx`, `HotspotFx` after T18's `DayNightController`); `Tests/EditMode/SfxRecipesTests.cs` (add the Rain_Patter test; the `All` count in `SfxRecipes_AllIds_Unique` 19 -> 20). |
| Depends on | T18 committed (`DayNightController.ActiveLook`, `ParticleTint`); T14 (`WorldState.NetHotspot`, `ServerSetHotspot`, `DevWorldDriver`); T9/T10 (`SfxCueTableDef.SeasonAmbience` with 4 seeded entries in `SfxCueTable.asset`). |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: C2 #2, #6, #8, #9, #11, #14, #15, #17, #18 (`_Ripples` stays 8), C2 16.2 (no per-frame allocation; loops pass `follow` and stop in `OnDisable`; no cue during despawn); M3 #23, #24 (whole scenes); C2 D-Q9 (the cue table grows by appending missing rows). Section 0.3 P2, P6, P9, P10.

### T19.1 `Rain_Patter` recipe (spec 11.7 rules; `Core/Audio/SfxIds.cs`, `Core/Audio/SfxRecipes.M3.cs`)

- `SfxIds`: `public const string RainPatter = "Rain_Patter";` in a new `// M3` line after the M2 block (C 5.3).
- `SfxRecipes.M3.cs`: `public static partial class SfxRecipes` (same namespace, no `UnityEngine`), header comment in the M2 file's style ("M3 half ... ruling C-Q11; pure, deterministic for (id, sampleRate, seed) ..."). It holds:
  - `private static readonly SfxRecipe[] M3Table = { new SfxRecipe(SfxIds.RainPatter, 4.0f, true, AudioCategory.Ambience, 0.45f, false, 0f, 0f, 1f) };` (loop 4 s, Ambience, base 0.45, 2D, default max distance, no rate limit, min distance 1 so the cue validator's `minDistance > 0` holds). No public `M3` list is added (the tests use `All` and `TryGet`).
  - `private static float[] GenerateM3(string id, int n, int sampleRate, ref Pcg32 rng)`: a switch with `RainPatter` and a default that throws `ArgumentException("No generator for SFX id: " + id)`.
  - `RainPatterGen(n, sampleRate, ref rng)`, behaviour:
    - Work over `total = n + xf` samples, `xf = min(n / 2, max(1, round(0.02 x sampleRate)))` (the 20 ms crossfade, as `WindLoop`), after `NoiseWarmupSamples` warm-up samples so the filters start settled.
    - **Draw order per sample k (fixed; pins determinism):** (1) `white = rng.NextFloat() * 2 - 1`; (2) `roll = rng.NextFloat()`; (3) only when `roll < DropRatePerSecond / sampleRate` (60 drops/s): `amp = 0.3 + 0.7 * rng.NextFloat()`.
    - **Bed:** `white` through a one-pole high-pass (`hp = white - lpHp`, `lpHp += 0.13 * (white - lpHp)`) then a one-pole low-pass (`lp += 0.35 * (hp - lp)`): a soft hiss around 1-4 kHz, gain 0.35.
    - **Drops:** a drop sets `dropEnv = max(dropEnv, amp)`; each sample `drop = white * dropEnv` and `dropEnv *= exp(-1 / (0.004 * sampleRate))` (4 ms decay). The drop envelope carries into the crossfade region, so the tail continues the same stream.
    - `s[k] = 0.35 * lp + 0.65 * drop`.
    - **Seam:** crossfade the tail `[n, n + xf)` into the head exactly as `WindLoop` does (`o[i] = s[i] * w + s[n + i] * (1 - w)`, `w = i / xf`), then the linear seam correction so `o[n - 1] == o[0]`. `Generate` normalises the peak to 0.9.
  - All maths in float with no accumulated float time step (per-sample filters only); constants are private `const`s with comments.
- `SfxRecipes.cs`: `BuildAll` concatenates `M1Table`, `M2Table`, `M3Table` in that order; its comment becomes "M1 rows, then M2 (13), then M3 (1): 20". In `Generate`, add `case SfxIds.RainPatter: samples = GenerateM3(id, n, sampleRate, ref rng); break;` before `default` (today every other id falls to `GenerateM2`, which throws for an M3 id).
- `SfxGenerator` builds every `All` clip at Bootstrap: the 4 s mono clip adds about 0.7 MB. No `SfxGenerator` change.

### T19.2 `SfxRecipesTests` (EditMode; `Tests/EditMode/SfxRecipesTests.cs`)

| Test | Cases | Catches (mutation) |
|---|---|---|
| `SfxRecipes_RainPatter_SampleCountPeakNaNAndSeam` (C 10.4, add) | `TryGet(RainPatter)`: loop true, category Ambience, spatial false, base 0.45, length 4.0. `Generate(RainPatter, 44100, Seed)`: length `SampleCount` = 176400; peak in [0.5, 0.9 + 1e-6]; no NaN or Inf; seam `abs(s[n-1] - s[0]) < 0.05` (the M1/M2 loop bound); the same seed gives identical samples and a different seed gives a different first 1000 samples; the mean of `abs(s)` over the first and the last second differ by less than 25% (no fade-in or fade-out on a loop). | Seam correction dropped -> red; length 3.9 s -> red; a draw-order change does not fail this test by design (determinism is per build); a fade envelope copied from a one-shot -> red. |
| `SfxRecipes_AllIds_Unique` (existing; one edit) | `All.Count` 20; M1 indices 0-5, M2 6-18 as today, and `All[19].Id == SfxIds.RainPatter`. Everything else unchanged. | - |

The other existing recipe tests iterate `SfxRecipes.M1` or `SfxRecipes.M2` only and stay as they are. `FishingContentTests` (cue rows follow `All`) and `ContentAssetParityTests.SfxCueTableAsset_RowsEqualSeedCueRows_ByIdAndFields` pick up the new row automatically once `build` appended it (T19.3).

### T19.3 Cue-row append (`FishingContentBuilder.cs`, C 2.6 T19 row, C2 D-Q9)

- After the existing `Ensure<SfxCueTableDef>(...)` line, call a new private `AppendMissingCueRows(SfxCueTableDef cues)`:
  - `var so = new SerializedObject(cues); SerializedProperty rows = so.FindProperty("rows");` (null -> `InvalidOperationException` naming the field).
  - Collect the existing ids (ordinal). For each row of `FishingSeedData.CueRows()` in seed order whose id is missing: `rows.InsertArrayElementAtIndex(rows.arraySize)` and set `id`, `category` (`enumValueIndex` by the enum's index), `baseVolume`, `minDistance`, `maxDistance`, `minInterval`, `spatial` with `FindPropertyRelative`; log `FG CONTENT cue row appended: <id>`.
  - When anything was appended: `ApplyModifiedPropertiesWithoutUndo()` and `EditorUtility.SetDirty(cues)`. Otherwise touch nothing (a second `build` leaves the asset byte-identical).
  - Existing rows are never read for change and never rewritten (a tuned row keeps its values).
- `category` is an `AudioCategory` enum in a struct field: if `enumValueIndex` does not match the declared order, use `intValue` (state which in the report).

### T19.4 `WeatherFx` (C 7.19; `Presentation/World/WeatherFx.cs`)

`public sealed class WeatherFx : MonoBehaviour` with no public members (C 7.19 `{ }`). First lines:
```csharp
// Authority: local only (every peer); reads the blended weather look from DayNightController (CCR-F2), never writes gameplay state.
// M3-D21: camera-local rain particles and the 2D Rain_Patter loop while the look's rainAmount > 0. Light and fog belong to DayNightController.
```
- Serialized `Material particleMaterial` (`M_Particle`; null -> no particles, silent). `[DisallowMultipleComponent]`.
- `Awake`: `_dayNight = GetComponent<DayNightController>()` (same root, T18). Create the child `RainFx` with a `ParticleSystem`, configured in the `SplashFx` order (stop and clear, configure, play):
  - main: `loop` true, `playOnAwake` false, simulation space World, start lifetime 1.1 s, start speed 10 m/s, start size 0.015-0.025, start colour (0.75, 0.82, 0.95, 0.55), gravity 0.5, `maxParticles` 700, `cullingMode = ParticleSystemCullingMode.AlwaysSimulate` (the default Automatic mode can pause an off-screen system, and under `-batchmode -nographics` nothing is rendered: the PlayMode test would never see a drop);
  - shape: Box 24 x 24 m footprint, emitting straight down (rotate the child `Euler(90, 0, 0)` so the shape's +Z points down; state the final orientation in a comment);
  - emission: rate over time set each frame (below), starting at 0;
  - renderer: `ParticleSystemRenderMode.Stretch`, velocity scale 0.06, length scale 1, `M_Particle`, no shadows.
- `LateUpdate` (P10):
  - `rain = _dayNight != null ? _dayNight.ActiveLook.rainAmount : 0` (clamped 0..1).
  - Emitter: `Camera cam = Camera.main` (null -> no rain this frame); place `RainFx` at `cam.position + (0, 8, 0) + flat(cam.forward) x 4` (rain falls in front of and around the view, never inside the head).
  - `emission.rateOverTime = MaxRainRate (450) x rain`. Particles already falling finish their life (no pop when rain ends).
  - Tint: at most every 0.25 s, `main.startColor = RainColour x _dayNight.ParticleTint` (new particles only).
  - Loop: bus = `ServiceRoots.App` `TryGet<AudioBus>` (lazy; missing -> silent, the bus warns on its own). `rain > 0.001` and no loop -> `_loop = bus.StartLoop(SfxIds.RainPatter, transform, false)` (`follow` = this transform, P9). Loop running -> `bus.SetLoop(_loop, rain, 1f)` only when `rain` changed by more than 0.01 since the last set. `rain <= 0.001` and a loop -> `bus.StopLoop(_loop)`, null it. A loop the bus stopped (`!_loop.IsPlaying`) is dropped and restarted on the next frame if it still rains.
- `OnDisable`: `StopLoop` (idempotent, null-safe) and set the emission rate to 0. `OnEnable` does nothing (the next `LateUpdate` restarts what is needed).
- No ripples from rain: `RippleField` has 8 slots shared with the bite cues, and rain must never push a bite ripple out (gameplay readability). No collision (drops may pass through a roof; accepted for M3, spec 10.6 lists no more).

### T19.5 `HotspotFx` (C 7.19, spec 12.7; `Presentation/World/HotspotFx.cs`)

`public sealed class HotspotFx : MonoBehaviour`, no public members. First lines:
```csharp
// Authority: local only (every peer); reads the replicated WorldState.NetHotspot only (never DevWorldDriver or a planner), never writes gameplay state.
// Spec 12.7: bubbling splash particles and a ripple every 1.2 s at the active hotspot, on every peer (late joiners included: polled, not event-driven).
```
- Serialized `Material particleMaterial` (`M_Particle`). `Awake`: `_dayNight = GetComponent<DayNightController>()`; create the child `HotspotFx` with two systems in the `SplashFx` way (emission off, `Emit` only): droplets (gravity 1) and the child `Bubbles` (gravity -0.2), both with `cullingMode = AlwaysSimulate` (as `RainFx`: emitted particles must also die when nothing renders). Null material -> silent.
- `Update`:
  - `WorldState w = WorldState.Current`; `HotspotData h = (w != null && w.HasState) ? w.NetHotspot.Value.ToCore() : default`; `active = h.Active && h.Radius > 0 && w.MinuteF < h.EndMinute` (the client's own clock hides a stale hotspot the host has not cleared yet; `StartMinute` is not replicated).
  - Inactive: reset the timers; nothing else.
  - Active, in accumulators (no allocation; `UnityEngine.Random` is fine for cosmetics, as `SplashFx`):
    - bubbles 6/s at a random point within 0.6 x `Radius` of (X, Z), y = `WaterSurface.HeightAt(x, z)`, rising 0.3-0.6 m/s;
    - a pop every 0.5 s: 3 droplets at 1-2 m/s up in a 25 deg cone;
    - a ripple every 1.2 s (the first one on the frame the hotspot appears): `RippleField.Current?.Spawn(px, pz, 0.4f)` at a random point within 0.5 x `Radius`.
  - Tint new particles by `_dayNight.ParticleTint` (at most every 0.25 s), as `WeatherFx`.
- Cut-line (skeleton risk 8): if M3 ships with `InHotspot = false`, the host never spawns a hotspot and this component simply stays idle; it is still added (no code path differs).

### T19.6 `AmbienceBed` season sets (shared edit; `Runtime/Audio/AmbienceBed.cs`, M3-D22)

Meanings are section C 1.5: `windGain` multiplies the `Wind_Ambient` base volume; `windLfoHz` is a slow sine wobble on that gain; `lapIntervalMin/Max` bound the `Water_Lap` delay. **Summer reproduces M2 exactly.**
- Keep: the authority comment (extend it: "M3: season parameter sets from SfxCueTableDef.SeasonAmbience via IWorldContext (Runtime cannot see Gameplay); no world -> M1 values"), `Start` (bus checks, the `ambience.bed` rng stream, the wind source), `OnEnable`/`OnDisable` pause behaviour, `OnDestroy`.
- New private state: the attached `IWorldContext`, the `SfxCueTableDef` (from `ServiceRoots.App` `ContentRegistry.SfxCueTable`, resolved in `Start`), the target `SeasonAmbience` (initialised to the M1 values: gain 1, LFO 0, laps 0.6 / 1.4 - the existing constants), the smoothed `_gain` (1), `_lastCueBase`, the LFO start time.
- **Attach (P6):** in `Update`, while unattached, every 0.5 s (unscaled): `ServiceRoots.Game` non-null and `TryGet(out IWorldContext w)` -> subscribe `w.Changed += OnWorldChanged`, call it once. Detach on a destroyed instance and in `OnDestroy`.
- **`OnWorldChanged`:** when `HasState` and the season differs from the last applied one: target = `table.SeasonAmbience[(int)season]` if the table has 4 entries, else the M1 values. Weather and hotspot changes do nothing.
- **Wind (per frame, no allocation):** `_gain = Mathf.MoveTowards(_gain, target.windGain, 0.5f x Time.unscaledDeltaTime)` (a season change from the dev panel ramps over about a second instead of jumping); `lfo = target.windLfoHz > 0 ? 1 + 0.25 x sin(2 pi x windLfoHz x (Time.unscaledTime - _lfoStart)) : 1` (depth 0.25, the "T19 picks the wobble depth" item of section C 1.5); `cueBase = windRecipe.BaseVolume x _gain x lfo`; call `_bus.Register(_wind, AudioCategory.Ambience, cueBase)` only when `abs(cueBase - _lastCueBase) > 1e-4`. In Summer (gain 1, LFO 0) it never re-registers, so the wind is exactly M2's.
- **Laps:** `NextLapDelay()` returns `_rng.Range(target.lapIntervalMin, target.lapIntervalMax)`: the same stream and one draw per lap, so the Summer sequence equals M2's.
- Keep the wind `AudioSource` as it is (a child source owned by the bed, not an `AudioBus` loop; it dies with the scene).

### T19.7 `PresentationSceneObjects` (shared edit)

After T18's `DayNightController`: `BuilderUtil.GetOrAdd<WeatherFx>(root)` and `BuilderUtil.GetOrAdd<HotspotFx>(root)`; `Material particle = RenderMaterialLibrary.Get("M_Particle")` (null -> `InvalidOperationException` naming it); `SetReference(weatherFx, "particleMaterial", particle)` and the same for `HotspotFx`. Update the class comment (six drivers).

### T19.8 Tests

**PlayMode, `Tests/PlayMode/WeatherFxTests.cs` (new; class `WeatherFxTests`; same harness and Arrange as `DayNightTests`: solo, `HasState`, driver, clock stopped at minute 300, then the steps below; `LogWatch` empty at the end).** Helpers: `RainFx()` = the `ParticleSystem` on `Presentation/RainFx` (found by name under the `Presentation` root); `RainLoop()` = the `AudioBus` child `Loop_Rain_Patter` (`ServiceRoots.App.TryGet(out AudioBus bus)`, `bus.transform.Find("Loop_" + SfxIds.RainPatter)`), null when none.

| Test | Steps and asserts | Catches (mutation) |
|---|---|---|
| `WeatherFx_RainSegment_StartsRainAndLoopStopsOnClear` (C 10.4) | `DevSetWeatherOverride(Clear)`, wait 4.5 s: no `RainLoop()`; `RainFx().emission.rateOverTime.constant == 0`. `DevSetWeatherOverride(Rain)`: `WaitUntil` (6 s) `RainFx().particleCount > 0` and `RainLoop() != null` with an `AudioSource` whose `spatialBlend == 0` (the loop child's existence is the signal, as in `FeedbackSoloTests`; `isPlaying` is not asserted in batchmode); the emitter is within 15 m horizontally of `Camera.main`. After 4.5 s the emission rate equals 450 (1e-3; rainAmount 1). `DevSetWeatherOverride(Clear)`: `WaitUntil` (6 s) `RainLoop() == null` and the emission rate is 0; 2 s later `particleCount == 0`. If `particleCount` stays 0 under `-nographics` despite `AlwaysSimulate`, assert `isEmitting` and the rate instead and report it. | `StopLoop` never called -> red; emission not tied to the look -> red; rain playing while the look says Clear -> red. |
| `HotspotFx_ActiveHotspot_SpawnsRipplesOnEveryPeerView` (C 10.4) | Arrange at minute 10, not 300 (`DevSetMinute(10)`: before the planner's first natural spawn at 60-120, so no natural hotspot is active). Read `_Ripples` (8). Write the hotspot straight into the replicated variable, the only thing `HotspotFx` reads: `WorldState.Current.ServerSetHotspot(new HotspotData { Active = true, X = 0, Z = -30, Radius = 3, StartMinute = 10, EndMinute = 70 })` (lake water off the dock end; no planner or grid needed, so the test also runs under the 12.7 cut-line). Within 2 s: some `_Ripples` slot has strength 0.4 (1e-5) and lies within `0.5 x Radius + 1e-3` of (0, -30); the `Presentation/HotspotFx` droplet or bubble system has `particleCount > 0`. Within 3 more s a second 0.4 ripple appears (a different spawn time). Then clear through the replicated variable alone: `WorldState.Current.ServerSetHotspot(default)`; after 1.5 s no slot has a spawn time newer than the clear (no new 0.4 ripple), and 3 s later the hotspot systems hold no particles. The comment states the "every peer view" claim: `HotspotFx` reads only `NetHotspot`, which every client receives (late joiners on spawn); the MPPM check in T25 covers a real client. | Ignoring `Active` (keeps spawning after the clear) -> red; ripple strength not 0.4 -> red; a ripple per frame -> visible as more than 4 new slots in 3 s (asserted: at most 4). |
| `AmbienceBed_SeasonSets_WinterLouderSummerUnchanged` (extra) | Find `Ambience/Wind` (`AudioSource`); the `AmbienceBed` must be enabled (else fail with "AudioBus not ready"). `DevSetSeason(Summer)`, wait 2 s: `vS = volume`; sample 10 times over 2 s: every sample equals `vS` (1e-5; no LFO in Summer). `DevSetSeason(Winter)`, wait 2.5 s (gain ramp): sample 20 times over 4 s; every ratio `v / vS` lies in [1.5 x 0.75 - 0.01, 1.5 x 1.25 + 0.01] and the samples are not all equal (the 0.15 Hz LFO is live). `DevSetSeason(Summer)`, wait 2 s: `volume == vS` (1e-5). | Season ignored -> red; LFO depth 1 (silence dips) -> red; Summer not reproducing M2 -> red. |

**EditMode:** T19.2.

### T19.9 Steps (TDD order)

0. **Preconditions.** T18 committed (`DayNightController` has `internal ActiveLook` and `ParticleTint`). `SfxCueTable.asset` has 4 `seasonAmbience` entries and `ArtPalette.asset` 3 `weatherLooks` (T10). `SfxRecipesTests.SfxRecipes_AllIds_Unique` expects 19 today. **CCR-F4 check:** find the weather of day 1, segment 0 for the fixed dev seed (the session's run state: season `WeatherDirector.RollSeason(FishingConstants.DefaultDevRunSeed)`, then `new WeatherDirector(<lake weather table>, season, seed).At(1, 0)`; read it from a temporary EditMode print or from an existing T14 `DevWorldTests` log, and do not commit the probe). Clear or Fog: CCR-F4 is withdrawn. Rain: the conditional ruling (M3-D81) applies, so make the `FeedbackSoloTests.FindLoop` edit named in the CCR-F4 row (no other edit to that file) and list it in the T19 report; no further controller ruling is needed. Also grep `Tests/` for literal loop or clip counts (`19`, `childCount` on the bus) that the new row or loop would change; report any hit.
1. Write the Rain_Patter test and the count edit (T19.2). `compile` fails (no `SfxIds.RainPatter`): add the constant only, `compile`, then `test-edit -Filter "SfxRecipesTests"`. Expected: FAIL (unknown id; count 19).
2. Write `SfxRecipes.M3.cs` and the `SfxRecipes.cs` edits. Rerun the filter. Expected: PASS. Mutation: remove the seam correction -> the seam case red; restore.
3. Add `AppendMissingCueRows`. `build`. Expected: `FG BUILDALL PASS` and one `FG CONTENT cue row appended: Rain_Patter` line. `git diff Assets/_Project/Content/Audio/SfxCueTable.asset`: exactly one appended row. Run `build` again: no log line and no diff. `test-edit -Filter "ContentAssetParityTests|FishingContentTests|SfxRecipesTests"`. Expected: PASS.
4. Write `WeatherFxTests.cs`. `compile`, `test-play -Filter "WeatherFxTests" -TimeoutMinutes 15`. Expected: FAIL (no `RainFx`, no hotspot systems, wind unchanged by season).
5. Write `WeatherFx`, `HotspotFx`, the `AmbienceBed` edit and the `PresentationSceneObjects` edit. `build`, rerun the filter. Expected: PASS.
6. Mutations (each red once, then restore): no `StopLoop` on Clear; `HotspotFx` ignoring `Active`; `AmbienceBed` ignoring the season.
7. `git status`: both Spot scenes changed: keep them WHOLE (2.8 #24). Full `test-edit`, then full `test-play -TimeoutMinutes 15` (background). Green and warning-free.
8. ASCII check. Commit:
```
git add Assets/_Project/Core/Audio/SfxIds.cs Assets/_Project/Core/Audio/SfxRecipes.cs Assets/_Project/Core/Audio/SfxRecipes.M3.cs Assets/_Project/Core/Audio/SfxRecipes.M3.cs.meta Assets/_Project/Presentation/World/WeatherFx.cs Assets/_Project/Presentation/World/WeatherFx.cs.meta Assets/_Project/Presentation/World/HotspotFx.cs Assets/_Project/Presentation/World/HotspotFx.cs.meta Assets/_Project/Runtime/Audio/AmbienceBed.cs Assets/_Project/Editor/Builders/Fishing/FishingContentBuilder.cs Assets/_Project/Editor/Builders/Render/PresentationSceneObjects.cs Assets/_Project/Content/Audio/SfxCueTable.asset Assets/_Project/Tests/EditMode/SfxRecipesTests.cs Assets/_Project/Tests/PlayMode/WeatherFxTests.cs Assets/_Project/Tests/PlayMode/WeatherFxTests.cs.meta Assets/_Project/Scenes/Spot_LakeDock.unity Assets/_Project/Scenes/Spot_HarbourPier.unity
git commit -m "m3(weatherfx): rain particles and Rain_Patter loop, hotspot VFX, seasonal ambience sets" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T19.10 Acceptance (reviewer)

- `WeatherFx` and `HotspotFx` have no public members; authority comments present; `HotspotFx` reads only `WorldState.NetHotspot` (no `DevWorldDriver`, no planner, no host-only API).
- No per-frame allocation in `LateUpdate`/`Update` (accumulators, cached systems, `EmitParams` structs; no `GetComponent` per frame).
- The rain loop passes `follow` = its own transform, is stopped in `OnDisable` and when the look's rain reaches 0, and is 2D. No ripples from rain.
- `Rain_Patter`: 4 s loop, the fixed draw order, the 20 ms crossfade plus seam correction, peak 0.9; `All` is M1, M2, M3 in that order.
- The cue-table append adds missing rows only; a second `build` leaves the asset unchanged.
- `AmbienceBed`: Summer reproduces M2 exactly (no re-register, same rng stream and draws); `Runtime` still does not reference Gameplay.
- Both Spot scenes committed whole. All suites green and warning-free.

### T19.11 Manual check (controller, after the commit)

Solo on the lake. The weather track of the fixed dev seed decides what shows first; the dev panel that forces weather lands in T22, so the rain and fog look checks go to T22/T25 with the user (rain reads as rain around the camera at 1080p, the patter sits under the wind, nothing pops at the 4 s fade; fog at its maximum keeps the dock edge and the bobber readable at 25 m). Now: confirm the wind sounds as in M2 when the rolled season is Summer, and audibly windier with a slow swell otherwise. MPPM (2 players, T25 list): a hotspot spawned by the host shows bubbles and ripples on the client too.

---

## Open questions (each has a default that applies if unanswered; the controller kept the defaults of 2-4 on 2026-10-13)

1. **CCR-F1, primary or Option B** (controller). **RULED 2026-10-13: primary; Option B is not used (CCR accepted).** Primary: The season reaches textured env albedo through the palette, as the R74 standard designed it; `_FGSeasonTint` is pushed but no shader reads it in M3. (Option B, not used: it would have put an opt-in, default-off term into `Toon.shader` now, with no M3 material using it.)
2. **Rain look numbers** (user, at the T22/T25 look check). Default: 450 drops/s at full rain in a 24 x 24 m box, streaks 0.015-0.025 m, light x0.85, fog x1.2 and desaturate 0.1 (section C seeds). Retunes are data for the looks (seed + asset + `EcologyTunedDeviations`) and constants in `WeatherFx` for the particles.
3. **Night and max-fog readability** (user, T25; 17.3 bullet 5). Default: the M2 Night phase (`toonAmbient`, fog 0.014) and the moon at 0.25. If the bobber, marker or dock edge do not read at 25 m at night, the fix is data (Night `toonAmbient` or `moonIntensity`), not code.
4. **Dawn sun fade-in** (M3-D77, unchanged). Default: `sunriseFraction` 0, so the day opens with about 18 s of ambient light only while the sun fades in. Data-only alternative: -0.02.

## Risks

1. **T4 drift (CCR-F1).** If the adoption changes how env materials get their hue (for example painted full-colour env textures with `_UsePalette` 0), the season would stop reaching them. Guards: T18 step 0 (stop and report) and the EditMode test `SeasonTint_TexturedEnvMaterial_TakesHueFromPalette`, which fails the suite on such a change.
2. **The M2 test re-point (CCR-F3).** Accepted (M3-D80), so T18 makes the re-point itself; without it T18 could not keep `test-play` green: `PresentationWorld_Palette_IsLinearAndAppliedOnce` fails as soon as the clock drives the look. Other M2 tests that read light, fog or palette values were searched: only `PresentationWorldTests` does in PlayMode. The EditMode ones read assets and the saved scene's `RenderSettings`, which the builder still writes (fog 0.008, Day colours).
3. **Harbour scene hooks.** T18's `AddTo` needs the harbour's `Sun`, `WarmVolume` and `CoolVolume` roots (C 6.3). A T17 miss fails `build` in T18 with a named error; the fix belongs to T17 (or T24 under the integration-fix rule), not to this section.
4. **Ripple slot contention.** Hotspot ripples share `RippleField`'s 8 slots with the bite cues. At one ripple every 1.2 s with a 2.5 s life, a hotspot holds about 2-3 slots, so a bite's 1.0 + 0.3 + 0.3 ripples near a hotspot can recycle older hotspot ripples, never the reverse within the bite's own 2.5 s. Rain spawns no ripples. If a playtest shows a lost bite ripple, the fix is a lower hotspot rate, not more slots (the array length is fixed at 8 on the GPU).
5. **Clients before the first sync.** Until `HasState`, a client shows the Day hold and the base season; on the first state the look snaps once (a visible change during the join, never a pop in play). Acceptable: the join happens behind the scene-load transition.
6. **PlayMode run time.** `DayNight_ScrubFullDay_NoLightOrFogPops` steps 1200 frames, and the fog and weather tests wait out 4 s fades: about 60-90 s added to `test-play`, inside the 15-minute timeout. If it grows, step the scrub by 2 minutes and double the bounds (they are derived per step).
7. **Particle cost.** Rain caps at 700 particles plus the hotspot's few. The frame-time baseline (spec 10.7) is re-measured at T25. If rain costs more than 10%, lower `MaxRainRate` (a constant).
8. **World state leaking into M2 PlayMode tests.** Rain at session start (CCR-F4) is the known case. A natural hotspot spawns 60-120 game minutes after day start (45-90 real seconds); a single M2 PlayMode test that runs that long could see hotspot ripples recycle the bite ripples its `HasRipple` checks look for. No current test is known to run that long; if one goes flaky, T24 pins the world (clock stopped early in the day) under the integration-fix rule.
9. **Shader globals outlive the Spot scene.** `DayNightController` leaves the last phase's `_ToonShade`/`_ToonAmbient`/sky globals set when the scene unloads. The Menu scene holds only a solid-colour camera, a light and the UI (no Toon geometry; `SceneBuilder.BuildMenu`), and the next Spot scene's controller overwrites them on its first frame, so nothing reads stale values today. If a later milestone puts Toon geometry in the Menu, its scene needs a `PaletteGlobals` (which pushes the Day look on enable).
10. **Cut-line (skeleton risk 8).** With `InHotspot = false` the host spawns no hotspot (section D: no planner); `HotspotFx` stays idle, and `HotspotFx_ActiveHotspot_SpawnsRipplesOnEveryPeerView` still runs because it writes `NetHotspot` through `WorldState.ServerSetHotspot` directly.
