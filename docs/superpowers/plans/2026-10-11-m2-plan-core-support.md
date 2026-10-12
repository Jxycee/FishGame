# FishGame M2 - Plan Section C: Core support (T10, T11, T12)

- **Date:** 2026-10-11. Branch `m2-fishing-core`.
- **Binding contract:** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` ("C" below). If this file and C disagree, C wins; open doubts are listed under "Contract questions" at the end.
- **Skeleton:** `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md` (rows T10, T11, T12; section 6 row C).
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` sections 8 (input), 9.11 (feedback), 10.2-10.4 (palette, meshes, water), 11.6 (settings), 11.7 (audio).
- **Shape:** behaviour, signatures, test names and cases, commands and acceptance. No complete code (skeleton rule 3): none of T10-T12 is on the "code in the brief" list, so every task is `Implementer: Sonnet`.

---

## 0. Common to T10, T11, T12

### 0.1 Global constraints (C 2.8, by reference)
Every brief below carries C 2.8 items 1-21 by reference. The implementer reads C 2.8 before starting. The items that bite in a task are spelled out in that task's "Constraints that bite" list.

### 0.2 Commands
Every Unity run goes through the wrapper. Each run starts by setting `FG_OUT`, because PowerShell state does not persist between calls. Call the PowerShell tool with `timeout: 600000`. `<absolute scratchpad path>` is the only placeholder: substitute your own session scratchpad.

```powershell
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
```

- `compile`, `test-edit`, `test-play` as in M1 contract section 11 (C 11). Expected last lines: `FG RESULT test-edit PASS total=N passed=N failed=0 skipped=K log=<path>`, and so on.
- **Red** means one of two things. Either a compile error naming the missing type (`FG RESULT test-edit FAIL total=0 (no tests matched or compile errors)`, with `error CS0246`/`CS0103` in the `FG LOG` lines), or failing assertions.
- **LOCKED (exit 2):** stop the task and report it. The user has the editor open. Never kill Unity (C 2.8 #11).
- `-Filter` is a regex over full test names, for example `"FishGame.Tests.EditMode.(GameplayContextArbiterTests|InputContextRouterTests)"`.

### 0.3 Commits (C 2.1)
- At the commit step only. Use explicit paths, including the `.meta` files Unity generated for new files and folders. Never `git add -A` or `git add .`.
- Before committing, `git status --porcelain` shows nothing under `Library/ Temp/ Logs/ UserSettings/` and no `*.csproj` or `*.slnx` (C 2.8 #20).
- Shape: `git add <paths>; git commit -m "m2(<area>): <summary>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`.

### 0.4 Pipelining (skeleton section 5)
- T10 may start while T7 is in review.
- T11 may start while T8 is in review: it needs T5 for `RodCue` and T1 for `LogOnce`.
- T12 may start while T9 is in review.
- The three tasks share no file, so T10 + T12 can be batch-dispatched (separate commits, separate reviews).

---

## T10 - Input rod rows, context arbiter, dev input, settings fields

| Field | Value |
|---|---|
| Goal | Spec 8.2 rod rows in `InputContextRouter`, `InteractLive`, the pure `GameplayContextArbiter`, the `IDevInput` seam (adapter + fake) and the three M2 settings fields. |
| Owned files (C 12) | `Core/Input/InputContextRouter.cs` (edit), `Core/Input/GameplayContextArbiter.cs` (new), `Runtime/Input/DevKey.cs` (new), `Runtime/Input/IDevInput.cs` (new), `Runtime/Input/PlayerInputAdapter.cs` (edit), `Runtime/Settings/SettingsData.cs` (edit), `Tests/PlayMode/Fakes/FakeInputService.cs` (edit). Tests: `Tests/EditMode/InputContextRouterTests.cs` (edit), `InputActionsAssetTests.cs` (edit), `GameplayContextArbiterTests.cs` (new), `SettingsStoreTests.cs` (edit). All paths under `Assets/_Project/`. |
| Shared edits (C 2.6) | `SettingsData.cs` (three fields + Clamp), `PlayerInputAdapter.cs` (`IDevInput`; aiming context unchanged), `FakeInputService.cs` (implements `IDevInput`), `InputContextRouter.cs` (rod rows, `InteractLive`). |
| Depends on | T1 committed. |
| Implementer | Sonnet |
| Reviewer | Opus high |
| Commit area | `input` |
| Test count | 14 new tests (13 required + 1 extra), and 1 existing test re-cased (`InputContextRouter_NonM1Contexts_RouteLikeFree`). |

### T10.1 Constraints that bite
- **#1:** `IDevInput` is gated at runtime by `UnityEngine.Debug.isDebugBuild`. Never `#if DEBUG` or `DEVELOPMENT_BUILD`.
- **#2:** ASCII only, in code and comments.
- **#8:** the new tests reach Core and Runtime only. No Gameplay types are needed.
- **#9:** the PlayMode fake must stay warning-free.
- **M1 contract 2.1:** never write a bare `Input.` identifier. Read keys through `UnityEngine.InputSystem.Keyboard.current`, never the legacy `UnityEngine.Input`.
- **#11, #20, #21:** editor lock, commit hygiene, logs in the scratchpad.

### T10.2 Behaviour

#### Router rows
`Core/Input/InputContextRouter.cs` gets these rows, copied verbatim from C 7.10:

```
// InputContextRouter (edit): new rows (spec 8.2). Carrying/Staggered/Downed/Carried/Seated still route like Free (D18 kept for M5/M6).
// RodReady:    Move, Look, Jump, Sprint, Secondary*, Interact, Shove, Equip, Pause pass. Primary*, Drop zeroed.
// RodAiming:   Move, Look, Jump, Primary*, Secondary*, Equip, Pause pass. Sprint, Interact, Shove, Drop zeroed.
// RodWaiting:  Move, Look, Jump, Sprint, Secondary*, Interact, Shove, Pause pass. Primary*, Drop, Equip zeroed.
// RodFighting: Look, Primary*, Pause pass. Move, Jump, Sprint, Secondary*, Interact, Shove, Drop, Equip zeroed.
// RouteFight unchanged: RodFighting/Staggered -> (Steer, Reel || PrimaryHeld). EnabledMaps unchanged.
public static bool InteractLive(InputContext context);   // Free, RodReady, RodWaiting, Swimming -> true
```

- **Glossary.** "Move" is `MoveX` and `MoveY`. "Look" is `LookYawDeg` and `LookPitchDeg`. `Primary*` is `PrimaryHeld/Pressed/Released`. `Secondary*` is the three Secondary fields. "Zeroed" means `0f` or `false`.
- **Implementation pattern.** Use the M1 pattern: `CopyAll` and then zero the listed fields, one private method per row (`RouteRodReady`, `RouteRodAiming`, `RouteRodWaiting`, `RouteRodFighting`), plus four new `case` lines in `Route`. `default:` still goes to `RouteFree`.
- **Rows the router does not touch.** It does not apply the x0.6 aiming speed or the sprint ban, and it does not lock movement. That is `PlayerLocomotion` (T16) reading `GameplayContextArbiter`. The router only zeroes fields.
- **Unchanged.** `RouteFight` and `EnabledMaps` keep their M1 bodies.
- **`InteractLive`.** It is a switch that returns `true` for exactly `Free`, `RodReady`, `RodWaiting` and `Swimming`, and `false` for every other context, including `Carrying` (see Contract questions 2).
- **Header comment.** Update it to: "M1 rows Free, Swimming, Climbing, Menu; M2 rod rows RodReady, RodAiming, RodWaiting, RodFighting; Carrying, Staggered, Downed, Carried, Seated route like Free until M5/M6 (contract D18)".

#### `GameplayContextArbiter`
New file `Core/Input/GameplayContextArbiter.cs`. It is a pure static class with no engine references (C 7.10, C 13 M2-D1).
- `Resolve(bool climbing, bool swimming, InputContext requested)`:
  - `climbing` -> `Climbing`.
  - Else `swimming` -> `Swimming`.
  - Else, if `requested` is anything except `Menu`, `Climbing` or `Swimming` -> `requested`. That includes `Free`, the four rod contexts, and the M5/M6 contexts.
  - Else -> `Free`.
  - `Menu` is never produced. `MenuOpen` stays the adapter's overlay.
- `IsRodContext(c)`: true for `RodReady`, `RodAiming`, `RodWaiting`, `RodFighting` (byte values 1..4).
- `LocksMovement(c)`: true only for `RodFighting`. Add a comment: "M5 adds Staggered/Downed/Carried".
- `IsAiming(c)`: true only for `RodAiming`. Its consumer (T16) applies x0.6 speed and no sprint.
- Class comment: "Core, no engine references. Precedence of the effective gameplay context (C M2-D1): Climbing > Swimming > requested > Free. PlayerLocomotion (T16) is the only caller that writes the result."

#### `DevKey` and `IDevInput`
These are Runtime files, with signatures exactly as in C 7.10:
- `Runtime/Input/DevKey.cs`: `public enum DevKey : byte { ToggleOverlay = 0, NextRod = 1, NextBait = 2, ToggleForcedBite = 3, ToggleTwitchy = 4, RippleAtCrosshair = 5 }`.
- `Runtime/Input/IDevInput.cs`: `public interface IDevInput { bool WasPressed(DevKey key); }`, with a comment that repeats the C 7.10 gate.

#### `PlayerInputAdapter` (edit)
- Class declaration adds `IDevInput`: `MonoBehaviour, IInputService, IInputBindings, IDevInput`.
- `public bool WasPressed(DevKey key)` returns `false` unless all of these hold:
  - `UnityEngine.Debug.isDebugBuild`;
  - `_hasFocus`;
  - `Context != InputContext.Menu`;
  - `Keyboard.current != null`.
- When the gate passes, it returns `Keyboard.current[k].wasPressedThisFrame`, using this key map:
  - `ToggleOverlay` -> `Key.F3`
  - `NextRod` -> `Key.F4`
  - `NextBait` -> `Key.F5`
  - `ToggleForcedBite` -> `Key.F6`
  - `ToggleTwitchy` -> `Key.F7`
  - `RippleAtCrosshair` -> `Key.F9`
  - An unknown enum value -> `false`.
- It is stateless: no caching, so every reader in the frame sees the same edge.
- Add one line to the class header comment: "Dev keys F3-F7, F9 are read directly from Keyboard.current (not part of the rebindable asset); dev builds only."
- Nothing else in the adapter changes. Unchanged: routing, `EnabledMaps`, the cursor policy, aim and sprint latches, and the "aiming context unchanged" rule (C 2.6). Do not add Fishing-map behaviour.

#### `FakeInputService` (edit)
- Implements `IDevInput`.
- Adds `public readonly HashSet<DevKey> DevPressed = new HashSet<DevKey>();`.
- `WasPressed(key)` returns `Context != InputContext.Menu && DevPressed.Contains(key)`.
- Class comment: "tests add a key for one frame and remove it themselves". The `isDebugBuild` gate is not applied, because PlayMode in the editor is a debug build anyway.

#### `SettingsData` (edit; spec 11.6, C 7.10)
- Add, after `uiScalePercent`:
  - `public int lineGauge = 0;` with comment `// 0..2 = FishGame.Core.LineGaugeMode (Off, Arc, Bar)`
  - `public bool captions = true;`
  - `public bool fishingAssist = false;`
- `Clamp()` adds `lineGauge = ClampInt(lineGauge, 0, 2);`.
- `version` stays `1`. `JsonUtility` keeps the field defaults for a file written before M2, so no migration is needed.
- Update the class comment "M1 settings" to "M1 + M2 settings (spec 11.6)".
- **Do not** implement "assist on sets the gauge to Arc once" here. That is a UI rule owned by T25 (C 7.10). `Clamp` never couples the two fields.
- `LineGaugeMode` is the T5 Core enum. `SettingsData` stores an `int`, so it needs no `using`.

### T10.3 Tests (exact names from C 10.1)

Helpers in `InputContextRouterTests`:
- Reuse `AllActive()`. Every field is set, including `Steer 0.3`, `Reel true`, `MoveX 0.5`, `MoveY -1`.
- Add one private helper that checks all 17 intent fields of a row, for example `AssertRow(PlayerIntent i, string ctx, bool move, bool look, bool jump, bool sprint, bool primary, bool secondary, bool interact, bool shove, bool drop, bool equip, bool pause)`.
  - "true" means the field equals the `AllActive` value.
  - "false" means it is `0f` or `false`.
  - `primary` covers all three Primary fields, and `secondary` all three Secondary fields.

`InputContextRouterTests.cs` (add):

| Test | Cases |
|---|---|
| `InputContextRouter_RodReady_PassesSecondaryAndMovement` | `Route(RodReady, AllActive())`: move, look, jump, sprint, secondary, interact, shove, equip, pause pass; primary, drop zeroed. |
| `InputContextRouter_RodAiming_PassesChargeBlocksSprintAndInteract` | `RodAiming`: move, look, jump, primary, secondary, equip, pause pass; sprint, interact, shove, drop zeroed. |
| `InputContextRouter_RodWaiting_PassesCancelAndMovementBlocksPrimary` | `RodWaiting`: move, look, jump, sprint, secondary, interact, shove, pause pass; primary, drop, equip zeroed. |
| `InputContextRouter_RodFighting_BlocksMovementPassesLookAndPrimary` | `RodFighting`: look, primary, pause pass; move (both axes exactly 0), jump, sprint, secondary, interact, shove, drop, equip zeroed. Also `RouteFight(RodFighting, AllActive())` = (0.3, true). |
| `InputContextRouter_RodContexts_SameKeyNeverMovesAndReels` | Frame "S held + D held + stick": `MoveX 1, MoveY -1, Reel true, Steer 1, PrimaryHeld false`. For each of `RodReady, RodAiming, RodWaiting, RodFighting`, let `moving = intent.MoveX != 0 \|\| intent.MoveY != 0` and `fighting = fight.Reel \|\| fight.Steer != 0`. Assert `!(moving && fighting)`. Non-vacuous checks: `RodFighting` has fighting true and moving false; the other three have moving true and fighting false. Free is checked too (moving true, fighting false). The sweep is limited to these five contexts; see Contract questions 7. |
| `InputContextRouter_InteractLive_MatchesSpecRows` | Over every `InputContext` value (`Enum.GetValues`), `InteractLive` is true exactly for `{Free, RodReady, RodWaiting, Swimming}` (4 true, 9 false). Consistency check: for each context where it is true, `Route(ctx, AllActive()).InteractPressed` is true. |

- Existing `InputContextRouter_NonM1Contexts_RouteLikeFree` keeps its name. Its array becomes `Carrying, Staggered, Downed, Carried, Seated`; the four rod contexts are removed.
- Update the file header comment to "spec 8.2 M1 rows + M2 rod rows + contract D18".
- Every other M1 test in the file is unchanged and must stay green.

`InputActionsAssetTests.cs` (add): `InputActions_FishingReelAndSteer_Exist`. This is the 17.2 data-and-input bullet and the M2-D2 binding rule. Load with the existing `LoadFromJson()` and assert:
- `Fishing/Reel` exists and has type Button.
- Reel has exactly one non-composite binding, `<Keyboard>/s` in group `Keyboard&Mouse`. It has no `<Mouse>/leftButton` and no `<Gamepad>/rightTrigger` binding: reel-by-Primary is the router's `Reel || PrimaryHeld`, never a duplicate binding.
- `Fishing/Steer` exists with type Value and control type Axis.
- Steer has a `1DAxis` composite with negative `<Keyboard>/a` and positive `<Keyboard>/d`, plus `<Gamepad>/leftStick/x` in group `Gamepad`.
- `DestroyImmediate` the asset in `finally`.

This test is green on its first run, because the asset already satisfies it (DA13). It is a guard, not a red step.

`GameplayContextArbiterTests.cs` (new; namespace `FishGame.Tests.EditMode`):

| Test | Cases |
|---|---|
| `GameplayContextArbiter_ClimbingBeatsEverything` | `Resolve(true, swimming, requested) == Climbing` for both values of `swimming` and every `InputContext` as `requested`. |
| `GameplayContextArbiter_SwimmingBeatsRodRequest` | `Resolve(false, true, r) == Swimming` for `r` in `Free, RodReady, RodAiming, RodWaiting, RodFighting, Menu`. |
| `GameplayContextArbiter_RodRequest_UsedWhenOnLand` | `Resolve(false, false, r) == r` for the four rod contexts, `Free` and `Carrying`. |
| `GameplayContextArbiter_MenuRequest_TreatedAsFree` | `Resolve(false, false, Menu) == Free`. Also `Climbing` and `Swimming` requests while neither flag is set -> `Free`. |
| `GameplayContextArbiter_LocksMovement_OnlyRodFighting` | Over every `InputContext` value, `LocksMovement` is true only for `RodFighting`. |
| `GameplayContextArbiter_RodContextAndAiming_MatchRows` (extra) | `IsRodContext` is true exactly for the four rod contexts. `IsAiming` is true exactly for `RodAiming`. |

`SettingsStoreTests.cs` (add): `SettingsStore_FishingFields_RoundTripAndClamp`. Use the file's existing temp-dir `SetUp`/`TearDown`.
1. A missing file loads defaults: `lineGauge 0`, `captions true`, `fishingAssist false`.
2. Set `lineGauge 2`, `captions false`, `fishingAssist true`. `Save`, then reload into a new `SettingsStore`: the values are equal.
3. Set `lineGauge 7`, then `NotifyChanged()` -> `2`. Set `-3`, then `NotifyChanged()` -> `0`.
4. Write an M1-era JSON file by hand (for example `{"version":1,"masterVolume":50}`) and load it: `captions` is still `true`, `lineGauge 0`, `fishingAssist false`, `masterVolume 50`.
5. Setting `fishingAssist = true` and calling `NotifyChanged()` leaves `lineGauge` at 0. This proves `Clamp` does not implement the T25 rule.

### T10.4 Steps (TDD, C 2.4)
1. **Tests first.**
   - Add the six router tests and re-case `NonM1Contexts_RouteLikeFree`.
   - Add `InputActions_FishingReelAndSteer_Exist`.
   - Create `GameplayContextArbiterTests.cs`.
   - Add `SettingsStore_FishingFields_RoundTripAndClamp`.
2. **Red.** Run `test-edit -Filter "FishGame.Tests.EditMode.(InputContextRouterTests|GameplayContextArbiterTests|InputActionsAssetTests|SettingsStoreTests)"`. Expected: a compile failure naming `GameplayContextArbiter`, `InteractLive`, `lineGauge`/`captions`/`fishingAssist` (total=0).
3. **Implement.** Write the router rows + `InteractLive`, `GameplayContextArbiter`, and the `SettingsData` fields + `Clamp`.
4. **Green.** Rerun the same filtered command. Expected `FG RESULT test-edit PASS`.
5. **Dev input.** Add `DevKey.cs`, `IDevInput.cs`, the adapter `WasPressed`, and the fake `DevPressed`. Gate with `compile` (expected `FG RESULT compile PASS`). Then grep the diff for a bare `Input.` and for `#if`: there must be none.
6. **Full suites.**
   - Run `test-edit` with no filter: all green, including the M1 tests.
   - Run `test-play` once, because the PlayMode fake changed: all green, and no new warnings in the log.
7. **Commit.**

### T10.5 Acceptance (reviewer checks)
- **Router.**
  - The router rows match the C 7.10 text field for field. `RouteFight` and `EnabledMaps` are untouched.
  - `InteractLive` returns true for exactly the four contexts.
  - `RouteFree` still serves the five M5/M6 contexts.
- **Arbiter.** `GameplayContextArbiter` has no `UnityEngine` reference and no state. Its precedence is exactly Climbing > Swimming > request (except Menu/Climbing/Swimming) > Free.
- **Adapter.** `WasPressed` is gated by `UnityEngine.Debug.isDebugBuild`, focus and not-Menu. `Keyboard.current` is null-checked. There is no `#if`. No action map changed and no binding was added.
- **Assets.** `FishGameInput.inputactions` and the generated `FishGameInput.cs` are unchanged (the test is read-only).
- **Settings.** `SettingsData` has the three fields with the spec 11.6 defaults. `Clamp` clamps `lineGauge` to 0..2, `version` is still 1, and there is no assist-to-gauge coupling.
- **Test counts.** The 14 new tests are present by exact name, and the re-cased test keeps its name. EditMode and PlayMode are fully green.

### T10.6 Commit
```powershell
git add Assets/_Project/Core/Input/InputContextRouter.cs Assets/_Project/Core/Input/GameplayContextArbiter.cs Assets/_Project/Core/Input/GameplayContextArbiter.cs.meta Assets/_Project/Runtime/Input/DevKey.cs Assets/_Project/Runtime/Input/DevKey.cs.meta Assets/_Project/Runtime/Input/IDevInput.cs Assets/_Project/Runtime/Input/IDevInput.cs.meta Assets/_Project/Runtime/Input/PlayerInputAdapter.cs Assets/_Project/Runtime/Settings/SettingsData.cs Assets/_Project/Tests/PlayMode/Fakes/FakeInputService.cs Assets/_Project/Tests/EditMode/InputContextRouterTests.cs Assets/_Project/Tests/EditMode/InputActionsAssetTests.cs Assets/_Project/Tests/EditMode/GameplayContextArbiterTests.cs Assets/_Project/Tests/EditMode/GameplayContextArbiterTests.cs.meta Assets/_Project/Tests/EditMode/SettingsStoreTests.cs; git commit -m "m2(input): rod router rows, InteractLive, context arbiter, IDevInput, fishing settings" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## T11 - Audio M2

| Field | Value |
|---|---|
| Goal | The 11 spec 11.7 M2 recipes plus `Rod_Thunk`; `SfxRecipes.All`; `SfxRecipe.MinDistance`; `AudioBus` loops, ducking, pitch multiplier, minDistance and cue-table overrides; `RodCueAudio`; `CaptionRequested`; the audition menu. |
| Owned files (C 12) | `Core/Audio/SfxIds.cs`, `Core/Audio/SfxRecipe.cs`, `Core/Audio/SfxRecipes.cs` (edits); `Core/Fishing/RodCueAudio.cs` (new); `Runtime/Audio/AudioBus.cs` (edit), `Runtime/Audio/AudioLoop.cs` (new), `Runtime/Audio/SfxGenerator.cs` (edit); `Runtime/Services/CaptionRequested.cs` (new); `Editor/Dev/AuditionCuesMenu.cs` (new). Tests: `Tests/EditMode/SfxRecipesTests.cs` (edit), `Tests/EditMode/RodCueAudioTests.cs` (new). |
| Shared edits (C 2.6) | `AudioBus.cs` (after T1's `LogOnce` edit), `SfxIds.cs`, `SfxRecipes.cs`, `SfxGenerator.cs`, `Runtime/Services/` (new file `CaptionRequested.cs` only). |
| Depends on | T5 (`RodCue`), T1 (`LogOnce`, post-T1 `AudioBus`). |
| Implementer | Sonnet |
| Reviewer | Opus high |
| Commit area | `audio` |
| Test count | 9 new tests (7 required + 2 extra). M1 audio tests unchanged and green. |

### T11.1 Constraints that bite
- **#16 and Core rules:** Core has no `UnityEngine`, `UnityEngine.Random` or `System.Random`. Recipe noise comes only from the existing `Rng.Stream(seed, "sfx." + id, 0UL)`.
- **#9:** PlayMode stays warning-free. Every M2 recipe must generate. `AudioBus` warnings go through T1's `LogOnce`, at most once per (reason, id).
- **#2:** ASCII only.
- **#1:** the audition menu is an editor tool and needs no build gate.
- **Base file:** start from the post-T1 `AudioBus.cs` (its `TryPrepare` already uses a private `LogOnce`), not the M1 text.
- **#11, #20, #21.**

### T11.2 Behaviour - Core

#### `SfxIds` (edit)
Add exactly the C 7.11 constants: `CastWhoosh`, `SplashSmall`, `SplashBig`, `BitePlop`, `NibbleTick`, `ReelClick`, `LineCreak`, `DiveThrum`, `Snap`, `CatchFlop`, `CatchCheer`, `RodThunk`, with the string values listed there. The M1 constants are unchanged.

#### `SfxRecipe` (edit)
- Add the field `public readonly float MinDistance;`.
- Add a 9-arg constructor: `(string id, float lengthSeconds, bool loop, AudioCategory category, float baseVolume, bool spatial, float maxDistance, float minInterval, float minDistance)`.
  - `minDistance` is appended **last**, so the first eight parameters mean the same in both constructors. See Contract questions 1: T13's `SfxCueTableDef.ToRecipes` uses this order.
- The 8-arg M1 constructor stays and chains with `minDistance: 1f`.

#### `SfxRecipes` (edit)
- Add a private `M2Table` (12 rows, order below) and `public static IReadOnlyList<SfxRecipe> M2 { get; }`.
- Add `public static IReadOnlyList<SfxRecipe> All { get; }`: the M1 rows and then the M2 rows, 18 in total, as one cached read-only array.
- `TryGet` searches `All`. `Generate` dispatches every `All` id. `M1` is unchanged (the M1 test `SfxRecipes_M1Table_MatchesContract` checks 6 rows).
- **File size.** With 12 more generators the file grows to roughly 450 lines, over the ~300-line rule. C 12 lists one file, so keep it as one file and say so in the task report. The exception is if the controller approves a partial `Core/Audio/SfxRecipes.M2.cs` first (Contract questions 11). The private M2 generators would move there.

**M2 table.** These are the seeds for T14's `SfxCueTable.asset`. Length and loop come from spec 11.7, except `Rod_Thunk`, which is ours (C 13 M2-D10). The values are the M2 mixing defaults; the cue table may retune everything except length and loop.

| # | Id | Length s | Loop | Category | Base | Spatial | MaxDist | MinInterval | MinDist |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `Cast_Whoosh` | 0.35 | no | Sfx | 0.70 | yes | 30 | 0.10 | 1 |
| 2 | `Splash_Small` | 0.25 | no | Sfx | 0.70 | yes | 30 | 0.08 | 1 |
| 3 | `Splash_Big` | 0.60 | no | Sfx | 1.00 | yes | 45 | 0.20 | 8 |
| 4 | `Bite_Plop` | 0.20 | no | Sfx | 0.70 | yes | 30 | 0.10 | 1 |
| 5 | `Nibble_Tick` | 0.03 | no | Sfx | 0.50 | yes | 20 | 0.30 | 1 |
| 6 | `Reel_Click` | 0.02 | no | Sfx | 0.45 | yes | 20 | 0.05 | 1 |
| 7 | `Line_Creak` | 0.50 | yes | Sfx | 0.60 | yes | 25 | 0 | 1 |
| 8 | `Dive_Thrum` | 1.00 | yes | Sfx | 0.70 | yes | 30 | 0 | 1 |
| 9 | `Snap` | 0.40 | no | Sfx | 1.00 | yes | 45 | 0.20 | 4 |
| 10 | `Catch_Flop` | 0.50 | no | Sfx | 0.80 | yes | 30 | 0.20 | 1 |
| 11 | `Catch_Cheer` | 0.80 | no | Sfx | 0.80 | yes | 60 | 0.30 | 6 |
| 12 | `Rod_Thunk` | 0.15 | no | Sfx | 0.60 | yes | 20 | 0.10 | 1 |

Notes on the table:
- **Spatial is a default flag.** The call site decides 2D or 3D: `PlayAt` is 3D, `Play2D` is 2D, and `StartLoop(..., spatial)` takes it as a parameter. Spec 11.7: everyone hears spatial sound at the source; only the owner's own reel/creak loops and UI are 2D.
- **`Splash_Big`.** MinDist 8 is the spec 9.4 "large minDistance" for the bite.
- **`Nibble_Tick`.** MinInterval 0.3 is the spec 11.7 example.
- **`Reel_Click`.** MinInterval 0.05 is below the fastest scheduled tick. `ReelClickRate = reelSpeed x 6`/s, and with `rod_deepsea` 1.3 m/s the tick period is 0.128 s.

**Generators.** Behaviour only. Exact coefficients are the implementer's choice, within the rules below.
- **Common rules (spec 11.7 and the M1 rules in the file header).**
  - Deterministic for `(id, sampleRate, seed)`. Noise comes only from the `ref Pcg32` stream.
  - The sample count is exactly `SampleCount(recipe, sampleRate)`.
  - Peak-normalized to 0.9.
  - One-shots start from silence (attack of 1-5 ms) and end with a release to exactly 0, so there are no clicks.
  - Loops are a whole number of base periods and have no DC jump at the seam.
- **Phase per sample.** Compute phase as `2*pi*f*i/sampleRate`. For sweeps, integrate phase in a double accumulator. Never accumulate a float time step.

| Id | Recipe (spec 11.7 column, made concrete) |
|---|---|
| `Cast_Whoosh` | White noise through a 2-pole band-pass (state-variable filter, Q about 2). The centre sweeps exponentially from 600 to 200 Hz over the length. The envelope is a smooth hump (`sin^2`, peak at about 35% of the length). |
| `Splash_Small` | One-pole low-passed white noise (coefficient about 0.35) x `exp(-t / 0.08)`, 2 ms attack, release to 0 over the last 10 ms. |
| `Splash_Big` | Low noise (two cascaded one-poles, about 0.05) x `exp(-t / 0.25)`, plus a sine sweeping 90 -> 45 Hz x `exp(-t / 0.25)` at about 0.6 relative level. 3 ms attack, release over the last 20 ms. |
| `Bite_Plop` | Sine gliding 320 -> 240 Hz x `exp(-t / 0.05)`, plus a short noise transient x `exp(-t / 0.01)` at about 0.3. 1 ms attack, release over the last 10 ms. |
| `Nibble_Tick` | 1800 Hz sine x `exp(-t / 0.006)`. 1 ms attack, release over the last 3 ms. |
| `Reel_Click` | 2600 Hz sine x `exp(-t / 0.003)`, plus a short noise tick at about 0.4. 0.5 ms attack, release over the last 3 ms. |
| `Line_Creak` (loop) | Band-limited saw at 200 Hz, so 0.5 s = exactly 100 periods. Additive: harmonics `k = 1..20`, each `sin(k * phase) / k` x a low-pass weight `1 / (1 + (k * 200 / 1200)^2)`. Start at phase `pi` (mid-ramp, where the slope is gentle), not at the wrap. Add a slight 6 Hz amplitude wobble (`0.85 + 0.15 sin`, 3 whole cycles per loop). No IIR filter, so the loop is exactly periodic. If an IIR filter is used anyway, pre-roll it by whole periods first. |
| `Dive_Thrum` (loop) | 55 Hz sine + 0.35 x 110 Hz sine (55 whole periods per 1 s loop), with slow AM `0.6 + 0.4 sin(2*pi*2*t)` (2 whole cycles per loop). No noise, no filter. |
| `Snap` | High-passed white noise (`white - onePole(white, 0.2)`) x `exp(-t / 0.03)`, plus a 2400 Hz ping x `exp(-t / 0.12)` at about 0.7. 1 ms attack, release over the last 20 ms. |
| `Catch_Flop` | Three low thumps starting at 0, 0.15 and 0.30 s. Each is a sine gliding 120 -> 80 Hz x `exp(-tau / 0.04)` plus a small noise tick, where tau is the time since that thump started. Release over the last 20 ms. |
| `Catch_Cheer` | Rising chord: C5, E5 and G5 (523.25, 659.25, 783.99 Hz) enter at 0, 0.1 and 0.2 s. Each has a 10 ms attack and a slight upward glide (x0.97 -> x1.0 over 80 ms), held with a gentle decay `exp(-t / 0.9)`. Release over the last 100 ms. |
| `Rod_Thunk` | Sine gliding 140 -> 70 Hz x `exp(-t / 0.03)`, plus low-passed noise (one-pole about 0.08) x `exp(-t / 0.02)` at about 0.5. 1 ms attack, release over the last 10 ms. |

#### `Core/Fishing/RodCueAudio.cs` (new; C 7.11, spec 9.11)
- `SfxFor(RodCue cue)` implements exactly this map:
  - `Equip`, `Stow` -> `Rod_Thunk`
  - `Ready` -> `Reel_Click`
  - `ChargeStart` -> `Cast_Whoosh`
  - `CastRelease` -> `Cast_Whoosh`
  - `InvalidRelease` -> `Rod_Thunk`
  - `Landing` -> `Bite_Plop`
  - `Recall` -> `Rod_Thunk`
  - `Nibble` -> `Nibble_Tick`
  - `Bite` -> `Splash_Big`
  - `HookSet` -> `Reel_Click`
  - `SideRun` -> `Splash_Small`
  - `Dive` -> `Dive_Thrum` (the consumer starts a loop)
  - `Calm` -> `null`
  - `Exhausted` -> `Splash_Small`
  - `Snapped` -> `Snap`
  - `Escaped` -> `Splash_Small`
  - `LineLanded` -> `Catch_Flop`
  - `None` and any unknown value -> `null`
- Use the `SfxIds` constants, not string literals.
- `IsStinger(RodCue cue)`: true for `Bite` and `Snapped` only. Its consumer (T23) calls `AudioBus.Duck`.
- Class comment:
  - "Core, pure. RodCue -> SFX id (spec 9.11 sounds mapped onto 11.7 ids, C M2-D10)."
  - "Pitch-follows-charge and loop handling belong to FishingFeedback (T23)."
  - "The catch announce plays Catch_Flop + Catch_Cheer on every peer; that is not a RodCue."

### T11.3 Behaviour - Runtime

#### `AudioBus` (edit; C 7.11)
Members are listed by contract name. Everything not listed keeps its M1 and T1 behaviour.

- **Recipe lookup.** A private `TryGetRecipe(id, out SfxRecipe r)` returns the cue-table override for `id` if one exists, else `SfxRecipes.TryGet`. Use it from `TryPrepare` and `StartLoop`, never `SfxRecipes.TryGet` directly.
- **`ApplyCueTable(IReadOnlyList<SfxRecipe> overrides)`.**
  - `null` clears all overrides (back to the Core defaults).
  - Otherwise, for each override whose `Id` is known to `SfxRecipes.TryGet`, store a merged recipe: Core `Id`, `LengthSeconds` and `Loop`; override `Category`, `BaseVolume`, `Spatial`, `MaxDistance`, `MinInterval`, `MinDistance`.
  - Sanitize the merged values:
    - `BaseVolume`: NaN -> Core value, else clamp 0..1.
    - `MinDistance`: `<= 0` or NaN -> 1.
    - `MaxDistance`: `<= 0` or NaN -> 30. Then, if it is still below `MinDistance`, set it to `MinDistance`.
    - `MinInterval`: `< 0` or NaN -> 0.
  - An unknown id gets one `LogOnce` warning and is skipped.
  - The method works whether or not `Initialize` has run, because T14's hook runs after `Initialize` (C 2.6). It takes effect from the next play; running loops keep their values.
- **Play path.**
  - `PlayAt(string sfxId, Vector3 position, float pitchMult)` is new. The existing `PlayAt(string, Vector3)` stays and forwards with `1f`.
  - Every pooled play sets all of these: `minDistance = recipe.MinDistance` (1 if `<= 0`), `maxDistance` as in M1, `rolloffMode = Logarithmic`, `spatialBlend`, and `pitch = (1 + jitter) * clamp(pitchMult, 0.1, 3)`. The pool is reused, so a value left from `Splash_Big` (8 m) must never stick to the next play.
- **Rate limit per source (spec 11.7).** `Play2D` keeps the M1 key (`id`). `PlayAt` keys by `id` plus a coarse position cell (`floor(x / 2), floor(z / 2)`), so two friends' nibbles at different bobbers are not merged into one. Loops are never rate-limited. See Contract questions 6.
- **Duck gain.**
  - Private state: `_duckGain` (1 = none), `_duckHoldUntil` (unscaled time).
  - `Duck(float seconds)` sets `_duckHoldUntil = max(_duckHoldUntil, now + 0.3 + max(0, seconds))`. NaN `seconds` is treated as 0. A repeat call extends the hold; ducks never stack below 50%.
  - `Update()` (unscaled delta): the target is `0.5` while `now < _duckHoldUntil`, else `1`. `_duckGain` moves toward the target at `0.5 / 0.3` per second (0.3 s each way). When it changes, call `ApplyVolumes()`.
  - Effective volume = `AudioMath.EffectiveVolume(cueBase, cat, master) * (category is Ambience or Music ? _duckGain : 1)`. Apply it in **both** `Register` and `ApplyVolumes`. This covers `AmbienceBed`'s registered wind, new one-shots, and loops.
- **Loops: `AudioLoop StartLoop(string sfxId, Transform follow, bool spatial)`.**
  - `TryGetRecipe` + clip. A missing recipe or clip -> one `LogOnce` warning and return `null`. `null` is also returned when the bus is not ready.
  - Each loop gets a **dedicated** `AudioSource` on a child GameObject of the AudioBus (`"Loop_<id>"`), never a pool source and never parented under `follow`.
  - Source settings: `loop = true`, `playOnAwake = false`, `dopplerLevel 0`, `spatialBlend = spatial ? 1 : 0`, min and max distance from the recipe, `rolloffMode` Logarithmic, `pitch 1`.
  - Register it with category = recipe category and cueBase = `recipe.BaseVolume * volume01`, with `volume01` starting at 1. Then `Play()`.
  - Cap: 16 live loops. Over the cap -> one `LogOnce` warning and `null`.
  - `LateUpdate()` copies `follow.position` to each loop source. If `follow` was given and has since been destroyed (`(object)follow != null && follow == null`), stop and release that loop. A `null` follow (a 2D loop) stays at the bus.
- **`SetLoop(AudioLoop loop, float volume01, float pitchMult)`.**
  - Null-safe and stopped-safe (no-op).
  - Stores `volume01` (clamped 0..1, NaN -> 0) on the loop and re-registers with `cueBase = recipe.BaseVolume * volume01`. The stored cueBase is what `ApplyVolumes` re-applies, so a settings change never reverts the loop volume.
  - Sets `pitch = clamp(pitchMult, 0.1, 3)`.
- **`StopLoop(AudioLoop loop)`.** Idempotent and null-safe. It stops the source, calls `Unregister`, destroys the child GameObject (or keeps it in a small free list; implementer's choice), and marks the loop stopped.
- **Teardown.** `OnDestroy` stops every live loop (and keeps the M1 settings unsubscribe).
- **`GenerateAll`.** Covered by `SfxGenerator` below; the bus code needs no change for it.
- **Header comment.** Replace "Ducking and SfxCueTableDef are later milestones" with:
  - "M2: loops (dedicated sources), ducking of Ambience/Music, per-play pitch multiplier and MinDistance, cue-table overrides (ApplyCueTable, C M2-D11)."
  - "Rate limit: Play2D per id, PlayAt per id + 2 m cell."
- **Size.** Keep the file at or below about 300 lines. If it grows past that, move the per-loop state (source, follow, cueBase, volume01, stopped) into `AudioLoop` as `internal` members. `AudioLoop.cs` is a T11 file.

#### `Runtime/Audio/AudioLoop.cs` (new)
- Declaration: `public sealed class AudioLoop { public string SfxId { get; } public bool IsPlaying { get; } }`, exactly as in C 7.11.
- `IsPlaying` = not stopped, the source is alive, and `source.isPlaying`.
- The constructor and all state are `internal`. Only `AudioBus` creates loops.
- Authority comment: "Authority: none. Client-side presentation audio handle."

#### `SfxGenerator` (edit)
`GenerateAll` iterates `SfxRecipes.All` instead of `M1` (G10). Nothing else changes.

#### `Runtime/Services/CaptionRequested.cs` (new)
- Signature exactly as in C 7.11: `public readonly struct CaptionRequested { public readonly string Key; public readonly float Seconds; public CaptionRequested(string key, float seconds = 2f); }`.
- Comment: "EventBus message (C 8.1). Raised by FishingFeedback (T23) when settings.captions; shown bottom-centre by FishingHudPresenter (T25). Key is a UiStrings key."
- Same shape as `ToastRequested`.

#### `Editor/Dev/AuditionCuesMenu.cs` (new; C 6.5)
- Declaration: `public static class AuditionCuesMenu` in namespace `FishGame.EditorTools`, with `[MenuItem("FishGame/Dev/Audition All Cues")] public static void Audition()`.
- **Edit mode** (`!Application.isPlaying`):
  - For every recipe in `SfxRecipes.All`, call `SfxRecipes.Generate(id, SfxRecipes.SampleRate, seed)` with any fixed seed.
  - Check the 11.7 rules: sample count, peak < 1, no NaN or Inf, and loop seam < 0.05.
  - Log one `Debug.LogError` per failing id, then a final summary line: `FG AUDITION PASS count=18` or `FG AUDITION FAIL failed=<n>`.
  - Exceptions are caught per id and counted as failures.
- **Play mode:**
  - Get the `AudioBus` from `ServiceRoots.App`. If it is missing, log one warning and return.
  - Play every id once on a wall-clock schedule (0.8 s apart, driven by `EditorApplication.update` and `EditorApplication.timeSinceStartup`).
  - One-shots use `Play2D(id, recipe.Category)`. Loops use `StartLoop(id, null, false)` and then `StopLoop` after 1.5 s.
  - Wrap each play in try/catch and log any exception with its id. End with `FG AUDITION PLAYED count=18`.
  - Unsubscribe from `EditorApplication.update` when done, or when play mode exits (`EditorApplication.playModeStateChanged`).

### T11.4 Tests (exact names from C 10.1; extras marked)

`SfxRecipesTests.cs` (add; M1 tests unchanged):

| Test | Cases |
|---|---|
| `SfxRecipes_EveryM2Recipe_HasCorrectSampleCount` | For each `SfxRecipes.M2` row: `Generate(...).Length == SampleCount(recipe, 44100) == round(length * 44100)`. |
| `SfxRecipes_EveryM2Recipe_PeakBelowOne` | Peak < 1.0 and > 0.5, so no recipe is almost silent (same bounds as M1). |
| `SfxRecipes_EveryM2Recipe_HasNoNaNOrInf` | No NaN or Inf sample. |
| `SfxRecipes_M2Loops_HaveSmoothSeam` | For each M2 row with `Loop`, `abs(s[n-1] - s[0]) < 0.05`. Assert exactly 2 loops (`Line_Creak`, `Dive_Thrum`). |
| `SfxRecipes_AllIds_Unique` | `All.Count == 18`; the first 6 entries equal `M1` in order and the next 12 equal `M2` in order; ids are unique (ordinal); `TryGet` finds every `All` id; `M2.Count == 12`. |
| `SfxRecipes_M2Table_MatchesPlan` (extra) | The 12 M2 rows in the T11.2 table order. Each row's length, loop, category, base, spatial, maxDistance, minInterval and minDistance equal the table (tolerance 1e-6). Also the M1 rows report `MinDistance == 1` (8-arg constructor default). |
| `SfxRecipes_M2OneShots_StartAndEndNearZero` (extra) | For each non-loop M2 row, `abs(s[0]) < 0.05` and `abs(s[n-1]) < 0.05`, so there is no click. |

`RodCueAudioTests.cs` (new):

| Test | Cases |
|---|---|
| `RodCueAudio_EveryMappedCue_HasKnownSfxId` | Over every `RodCue` value: `None` and `Calm` -> `null`. Every other cue -> a non-null id with `SfxRecipes.TryGet` true. Plus an explicit table assertion of the full C 7.11 map (17 rows). |
| `RodCueAudio_BiteAndSnap_AreStingers` | Over every `RodCue` value, `IsStinger` is true exactly for `Bite` and `Snapped`. |

### T11.5 Steps (TDD)
1. **Core tests first.** Write the seven `SfxRecipesTests` additions and `RodCueAudioTests.cs`.
2. **Red.** Run `test-edit -Filter "FishGame.Tests.EditMode.(SfxRecipesTests|RodCueAudioTests)"`. Expected: a compile failure on `SfxRecipes.M2`/`All`, `MinDistance`, `RodCueAudio` and the new `SfxIds`.
3. **Implement Core.** Write `SfxIds`, `SfxRecipe`, `SfxRecipes` (table + 12 generators), and `RodCueAudio`.
4. **Green.** Rerun the same filter. If a seam or click test fails, fix the generator (phase start, whole periods, release), never the threshold.
5. **Runtime.** Write `AudioLoop`, the `AudioBus` edits, `SfxGenerator`, `CaptionRequested`, and `AuditionCuesMenu`. Gate with `compile`.
6. **Audition check (edit mode, batchmode).** Check `Temp/UnityLockfile` first. Then run:
   ```powershell
   $out = '<absolute scratchpad path>\fg'; $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -ArgumentList @('-batchmode','-nographics','-quit','-projectPath','C:\Dev\FishGame','-executeMethod','FishGame.EditorTools.AuditionCuesMenu.Audition','-logFile',"$out\audition.log") -PassThru -NoNewWindow; $null = $p.Handle; $p.WaitForExit(600000); Select-String -Path "$out\audition.log" -Pattern 'FG AUDITION|error CS|Exiting batchmode successfully'
   ```
   Expected: `FG AUDITION PASS count=18` and `Exiting batchmode successfully`.
7. **Full suites.** Run `test-edit` (all green) and `test-play`. `test-play` must stay green: `AudioBus` now has `Update`/`LateUpdate`, and the bootstrap generates 18 clips. Check its log for no new `AudioBus`/SFX warnings.
8. **Commit.**

Manual (the controller, at the T19/T23 manual checks, not a blocker for T11): in Play mode, run `FishGame/Dev/Audition All Cues` once and listen.

### T11.6 Acceptance (reviewer checks)
- **Ids and recipes.**
  - `SfxIds` has exactly the 12 C 7.11 constants.
  - `SfxRecipe` has `MinDistance`, and the 9-arg constructor has `minDistance` last.
  - The 8-arg constructor gives 1.
- **Recipe lists.** `All` is M1 then M2. `TryGet`, `Generate` and `SfxGenerator.GenerateAll` cover `All`.
- **Generators.** All 12 use only the seeded `Pcg32` (no `System.Random`). Loops are whole periods with no seam jump. One-shots end at 0.
- **`RodCueAudio`.** The map matches C 7.11 entry for entry. Stingers are exactly Bite and Snapped.
- **`AudioBus`.**
  - Every pooled play sets `minDistance` and `pitch`.
  - Loops use dedicated sources, follow in `LateUpdate`, stop when `follow` dies, are capped at 16, and are not rate-limited.
  - `SetLoop` survives `ApplyVolumes`.
  - Ducking runs on unscaled time, extends rather than stacks, and covers both `Register` and `ApplyVolumes`.
  - `ApplyCueTable(null)` resets the overrides, and merged overrides keep Core length and loop.
  - Warnings go through `LogOnce`.
- **Unchanged public members.** The existing members keep their signatures: `Register`, `Unregister`, `PlayUi`, `Play2D`, `PlayAt(id, pos)` and `ApplyVolumes`.
- **Other surfaces.** `CaptionRequested` and `AudioLoop` match the C 7.11 signatures. The menu path and method match C 6.5.
- **Results.** Audition PASS, EditMode and PlayMode green, and 9 new tests by exact name.

### T11.7 Commit
```powershell
git add Assets/_Project/Core/Audio/SfxIds.cs Assets/_Project/Core/Audio/SfxRecipe.cs Assets/_Project/Core/Audio/SfxRecipes.cs Assets/_Project/Core/Fishing/RodCueAudio.cs Assets/_Project/Core/Fishing/RodCueAudio.cs.meta Assets/_Project/Runtime/Audio/AudioBus.cs Assets/_Project/Runtime/Audio/AudioLoop.cs Assets/_Project/Runtime/Audio/AudioLoop.cs.meta Assets/_Project/Runtime/Audio/SfxGenerator.cs Assets/_Project/Runtime/Services/CaptionRequested.cs Assets/_Project/Runtime/Services/CaptionRequested.cs.meta Assets/_Project/Editor/Dev/AuditionCuesMenu.cs Assets/_Project/Editor/Dev/AuditionCuesMenu.cs.meta Assets/_Project/Tests/EditMode/SfxRecipesTests.cs Assets/_Project/Tests/EditMode/RodCueAudioTests.cs Assets/_Project/Tests/EditMode/RodCueAudioTests.cs.meta; git commit -m "m2(audio): M2 recipes and Rod_Thunk, All, MinDistance, AudioBus loops/duck/cue table, RodCueAudio, captions message, audition menu" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## T12 - Water math, mesh builder, ripples

| Field | Value |
|---|---|
| Goal | `WaveSet`/`WaveMath` (spec 10.4), the `WaterSurface` time/wave seam (C M2-D12), a deterministic flat-shaded `MeshBuilder` (spec 10.2), `RippleMath`, `FaceJitter`, `ShaderGlobals`. |
| Owned files (C 12) | `Core/World/WaveSet.cs`, `Core/World/WaveMath.cs`, `Core/World/RippleMath.cs`, `Core/Render/FaceJitter.cs`, `Core/Render/ShaderGlobals.cs` (all new, in new folders); `Runtime/World/WaterSurface.cs` (edit), `Runtime/World/MeshBuilder.cs` (new). Tests: `Tests/EditMode/WaveMathTests.cs` (new), `WaterSurfaceTests.cs` (edit), `MeshBuilderTests.cs` (new), `RippleMathTests.cs` (new). |
| Shared edits (C 2.6) | `Runtime/World/WaterSurface.cs` (wave seam). |
| Depends on | T1 committed. |
| Implementer | Sonnet |
| Reviewer | Sonnet (pure Core + one Runtime builder; scripted checks first) |
| Commit area | `water` |
| Test count | 13 new tests (12 required + 1 extra). The M1 `WaterSurface_HeightAt_ReturnsFlatHeightEverywhere` is kept and stays green. |

### T12.1 Constraints that bite
- **#16:** no `UnityEngine.Random` or `System.Random`. Face jitter comes from a hash.
- **Core:** no `UnityEngine` (`noEngineReferences`).
  - `WaveSet` and `WaveMath` use `float` and `double` fields, never vectors (M1 contract 2.1).
  - Use `System.Math`/`MathF` in Core.
- **#18:** `_Waves` is pushed with length 4 and `_Ripples` with length 8. This task defines the names and the slot count; T22 pushes the arrays.
- **#19:** every `MeshBuilder.ToMesh` mesh carries vertex colours (`FishGame/Toon` needs them).
- **#2:** ASCII only.
- **#11, #20, #21.**
- **Static state:** `WaterSurface` is static. Every test that configures it resets it in `TearDown`; otherwise it leaks into the M1 test and into PlayMode.

### T12.2 Behaviour - Core

#### `Core/World/WaveSet.cs`
- Declarations exactly as in C 7.12:
  - `public struct Wave { public float DirectionDeg, Amplitude, Wavelength, Speed; }`
  - `public struct WaveSet { public Wave W0, W1, W2; public static readonly WaveSet Flat; }`
- `Flat` is `default` (all zero).
- Comment the units: degrees, metres, metres, metres per second.
- Comment the direction convention: **0 deg = +Z, 90 deg = +X**, the same as the `CastMath` yaw. A wave travels along `dir = (sin(deg), cos(deg))` in (x, z).

#### `Core/World/WaveMath.cs`
- **`HeightAt(in WaveSet w, float x, float z, double t)`** = term(W0) + term(W1) + term(W2), where:
  - `term = 0` when `!(Wavelength > 0)` (also catches NaN) or `Amplitude == 0`.
  - Else, with `th = DirectionDeg * pi / 180`, `k = 2*pi / Wavelength`:
    `term = Amplitude * sin(k * ((sin(th) * x + cos(th) * z) - Speed * t))`.
  - Compute the phase and the sine in `double` (t grows large in long sessions), then return the sum as `float`.
- **`AmplitudeSum(in WaveSet w)`** = the sum of `abs(Amplitude)` over the terms with `Wavelength > 0`.
- Class comment: "Core, pure (spec 10.4). One shared formula: T21's FishGame/Water vertex displacement must use the same direction convention and term."

#### `Core/World/RippleMath.cs`
- Constants exactly as in C 7.12: `Slots = 8`, `RingSpeed 2.2`, `RingWidth 0.25`, `Lifetime 2.5`, and the seven strengths `CastLanding 0.6`, `Nibble 0.2`, `Bite 1.0`, `BiteFollow 0.3`, `Snap 0.8`, `Landed 0.7`, `Shove 1.0`.
- **`Ring(float dist, float age, float strength)`** = `(1 - sat(abs(dist - age * RingSpeed) / RingWidth)) * sat(1 - age / Lifetime) * strength`, where `sat` clamps to 0..1.
  - Refinement: `age < 0` returns 0. A ripple stamped ahead of the water clock is invisible. See Contract questions 5.
  - NaN inputs return 0.
- **`NextSlot(int lastSlot)`** = `(lastSlot + 1) % Slots` for `lastSlot` in `0..Slots-1`. Any other value returns 0, which also covers -1 for "none yet". With round-robin, the next slot is always the oldest.

#### `Core/Render/FaceJitter.cs`
- **`Grey(uint meshSeed, int faceIndex, float jitter)`:**
  - `h = Rng.SplitMix64(((ulong)meshSeed << 32) | (uint)faceIndex)`.
  - `u = (float)(h >> 40) * (1f / 16777216f)`, so u is in [0, 1) and exact.
  - Return `1f + jitter * (2f * u - 1f)`.
- `jitter <= 0` or NaN -> exactly `1f`.
- Pure and deterministic. `Rng.SplitMix64` is the existing Core hash.

#### `Core/Render/ShaderGlobals.cs`
- `public static class ShaderGlobals` with `public const string` members for the C 5.3 names:
  - `Palette = "_FGPalette"`
  - `Ripples = "_Ripples"`
  - `Waves = "_Waves"`
  - `WaterTime = "_WaterTime"`
  - `ToonShade = "_ToonShade"`
  - `ToonAmbient = "_ToonAmbient"`
  - `SkyTop = "_SkyTop"`
  - `SkyHorizon = "_SkyHorizon"`
  - `SkyGround = "_SkyGround"`
- These C# identifiers are chosen here; see Contract questions 3.
- Class comment: "Global shader property names (C 5.3). Arrays are always pushed full length: _FGPalette 16, _Ripples 8 (RippleMath.Slots), _Waves 4 (C 2.8 #18)."

### T12.3 Behaviour - Runtime

#### `Runtime/World/WaterSurface.cs` (edit; C 7.12, C M2-D12)
- **Kept.** `public const float FlatHeight = 0f;` and the M1 `HeightAt(float x, float z)` signature. T17's `CastProbe` uses `FlatHeight + 20`.
- **New private statics.** `s_waves` (`WaveSet`, default `Flat`) and `s_now` (`Func<double>`, default `null`).
- **Methods:**
  - `Configure(in WaveSet waves)` stores the waves.
  - `SetTimeSource(Func<double> now)` stores the source; `null` restores the default.
  - `Now` returns `s_now != null ? s_now() : Time.timeSinceLevelLoad`.
  - `HeightAt(x, z)`:
    - When the stored set is flat (`WaveMath.AmplitudeSum(s_waves) == 0`), return `FlatHeight` **without** reading `Now`.
    - Otherwise return `HeightAt(x, z, Now)`.
  - `HeightAt(x, z, t)` returns `FlatHeight + WaveMath.HeightAt(s_waves, x, z, t)`.
  - `ResetToFlat()` sets the waves to `Flat` and the time source to `null`.
- **Reset on play.** A private `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => ResetToFlat();`. Enter Play Mode options are on in this project, so statics survive play sessions.
- **Class comment:**
  - "Authority: none. Pure shared water height for every peer (spec 10.4)."
  - "Waves come from WaterTimeDriver (T22) via Configure; the clock via SetTimeSource (ServerTime in sessions)."
  - "Gameplay never reads _WaterTime."

#### `Runtime/World/MeshBuilder.cs` (new; C 7.12, spec 10.2)
The public surface is exactly as in C 7.12. The class comment states the rules below.

- **Constructor.** `MeshBuilder(uint meshSeed, float faceJitter = 0.04f)`. `faceJitter` must be in 0..0.5, else `ArgumentOutOfRangeException` (NaN too).
- **Geometry.**
  - Every primitive appends unshared vertices: 3 per triangle, in the order emitted.
  - Indices are `0, 1, 2, ...`. There is no welding.
  - Positions are `center + rotation * local`.
- **Winding.** Each triangle `(a, b, c)` is clockwise seen from outside (the Unity front face). Its normal is `normalize(Cross(b - a, c - a))` and points outward. A primitive whose source table is counter-clockwise is flipped when emitted. The face test proves the result.
- **Faces.** A face is one `Triangle` call, one `Quad` call (two triangles `(a, b, c)`, `(a, c, d)`), or one polygon cap. All triangles of a face share the face's grey, `FaceJitter.Grey(meshSeed, faceIndex, faceJitter)`, and the slot. `faceIndex` counts faces from 0 in emission order. Each triangle gets its own computed normal, so planar quads and caps get identical normals.
- **Slot.** `slot` must be 0..15 (`ArgumentOutOfRangeException` otherwise). It is stored in **UV channel 1** as `(slot, 0)` via `mesh.SetUVs(1, List<Vector2>)`, which is shader `TEXCOORD1`, the spec's `uv1.x`. UV0 is not written. See Contract questions 4.
- **Colour.** Each vertex gets `new Color(g, g, g, 1)` as float `Color` via `SetColors(List<Color>)`. Never use `Color32`: `g` reaches `1 + jitter` (> 1), and `Color32` would clamp it.
- **Primitives.** All are centred on `center` and use a local Y axis. Angles are computed as `2*pi*k / sides` per index; never accumulated. `sides >= 3`, `rings >= 1`, `subdivisions` 0..4, and every radius, height and size component must be > 0, else `ArgumentOutOfRangeException` (exceptions noted per primitive). In the counts below, n = `sides` and r = `rings`.
  - **`Box(center, size, rotation, slot)`:** 6 quad faces in the order +X, -X, +Y, -Y, +Z, -Z = 12 triangles.
  - **`Cylinder(center, radiusBottom, radiusTop, height, sides, rotation, slot)`:**
    - Spans `y` from `-height/2` to `+height/2`. The ring vertex is at local `(radius * sin(theta), y, radius * cos(theta))`.
    - Side: n quads when both radii are > 0. When one radius is 0 (a cone), the side is n triangles to the apex.
    - Caps: one polygon face per non-zero radius, fan-triangulated from ring vertex 0, so n - 2 triangles each.
    - Both radii 0 -> exception.
    - Counts: both radii > 0 -> `2n + 2(n - 2)`; one radius 0 -> `n + (n - 2)`.
  - **`Prism(center, radius, height, sides, rotation, slot)`** = `Cylinder(center, radius, radius, height, sides, rotation, slot)`.
  - **`Capsule(center, radius, height, sides, rings, rotation, slot)`:**
    - `height` is the total height, as in Unity's `CapsuleCollider`. The body half-length is `max(0, height/2 - radius)`.
    - Each hemisphere has r latitude bands from the equator (`phi_b = (pi/2) * b / r`, b = 0..r).
    - In each hemisphere, the band touching the pole is n triangles; the others are n quads each.
    - The body is n quads when the body half-length is > 0, else it is omitted.
    - Count: `2 * (n + 2n(r - 1)) + (body ? 2n : 0)`.
  - **`Icosphere(center, radius, subdivisions, slot)`:**
    - Base: the 12-vertex, 20-face icosahedron (`t = (1 + sqrt 5) / 2`), as a fixed array in the file. **Normalize the 12 base vertices to unit length first.** The raw `(+-1, +-t, 0)` has length about 1.9, so skipping this makes subdivision 0 the wrong size and mixes two radii at higher levels.
    - Subdivide each base face depth-first in table order into 4 children, in order `(a, ab, ca)`, `(b, bc, ab)`, `(c, ca, bc)`, `(ab, bc, ca)`. Midpoints are `normalize(p + q)`.
    - Scale by `radius`; no vertex cache or Dictionary is needed because vertices are unshared.
    - Each final triangle is one face. Count: `20 * 4^subdivisions`.
  - **`Triangle(a, b, c, slot)`** and **`Quad(a, b, c, d, slot)`** are in world space as given, one face each.
- **`TriangleCount`** is the number of triangles appended so far.
- **`ToMesh(string name)`:**
  - Throws `InvalidOperationException` when `TriangleCount == 0`.
  - Creates a new `Mesh` with `name`.
  - Sets `indexFormat` explicitly: `UInt16` when the vertex count is at most 65535, else `UInt32`. This happens before the triangles are set.
  - Calls `SetVertices`, `SetNormals`, `SetColors`, `SetUVs(1, ...)`, then `SetTriangles(indices, 0, calculateBounds: true)`.
  - Never calls `RecalculateNormals`, `RecalculateTangents`, `Optimize` or `MarkDynamic`, and writes no tangents.
  - Leaves the builder unchanged, so a second call gives identical bytes.
- **`static Grid(int cellsX, int cellsZ, float cellSize, string name)`:**
  - Shared vertices `(cellsX + 1) * (cellsZ + 1)`, row-major (index `j * (cellsX + 1) + i`), at `(-cellsX * cellSize / 2 + i * cellSize, 0, -cellsZ * cellSize / 2 + j * cellSize)`.
  - Normals `(0, 1, 0)`. UV0 `(i / cellsX, j / cellsZ)`. No colours (water is not Toon).
  - Two triangles per cell, wound so their geometric normal points +Y.
  - `indexFormat` is set explicitly as in `ToMesh`.
  - `bounds` is set after the triangles: centre 0, size `(cellsX * cellSize, 2, cellsZ * cellSize)`. The extra 1 m above and below covers shader displacement.
  - `cellsX`, `cellsZ` >= 1 and `cellSize` > 0, else `ArgumentOutOfRangeException`.
- **Determinism (spec 10.2).**
  - Iterate only arrays and lists in emission order; never a `Dictionary` or `HashSet`.
  - No time, no `UnityEngine.Random`, and no reliance on Unity's import-time processing.
  - The same calls with the same seed give bit-identical vertex, normal, colour, uv1 and index arrays and identical bounds.
- **Size.** Aim to keep the file at or below about 350 lines. If it cannot fit, stop and ask the controller before adding a partial file (C 12 lists one file).

### T12.4 Tests (exact names from C 10.1; extra marked)

`WaveMathTests.cs` (new). Shared fixture `Sample`, a 3-wave set:
- `(30 deg, 0.05 m, 8 m, 1.2 m/s)`
- `(160 deg, 0.03 m, 6 m, 0.9 m/s)`
- `(275 deg, 0.02 m, 11 m, 1.6 m/s)`

| Test | Cases |
|---|---|
| `WaveMath_Height_BoundedBySumOfAmplitudes` | 1,000 sample points `(x, z, t)` from a fixed `Pcg32` (x, z in -60..60; t in 0..10,000 s): `abs(h) <= AmplitudeSum(Sample) + 1e-5`. Also `AmplitudeSum(Sample) == 0.10` within 1e-6. |
| `WaveMath_Height_ContinuousInTime` | Rate bound `R = sum over waves of abs(Amplitude) * (2*pi / Wavelength) * abs(Speed)`. For 200 points and `dt = 1e-3`: `abs(h(t + dt) - h(t)) <= R * dt + 1e-5`, including t around 3,600 s. |
| `WaveMath_SameInputs_Deterministic` | Two calls with identical inputs return bit-identical floats (`BitConverter.SingleToInt32Bits`) at 50 points. |
| `WaveMath_Flat_IsZero` | `WaveSet.Flat` -> exactly 0 everywhere (5 points, several t). A set with `Amplitude 1, Wavelength 0` and one with `Amplitude 0, Wavelength 5` -> 0. Their `AmplitudeSum` is 0 in the first case. |

`WaterSurfaceTests.cs` (edit). Add a `[TearDown]` that calls `WaterSurface.ResetToFlat()`. Keep the M1 test unchanged.

| Test | Cases |
|---|---|
| `WaterSurface_Configured_FollowsWaveMath` | `Configure(Sample)`, then `SetTimeSource(() => 12.5)`. `Now == 12.5`. `HeightAt(x, z) == WaveMath.HeightAt(Sample, x, z, 12.5)` exactly, and `HeightAt(x, z, 99.0) == WaveMath.HeightAt(Sample, x, z, 99.0)` exactly, at 5 points. |
| `WaterSurface_ResetToFlat_ReturnsZero` | Configure plus a counting time source, then `ResetToFlat()`. `HeightAt` is exactly 0 at 3 points, and the counting source is not called afterwards: `HeightAt` and `Now` no longer use it. |

`MeshBuilderTests.cs` (new). Every created `Mesh` is destroyed with `Object.DestroyImmediate` in `TearDown` (keep a list).

| Test | Cases |
|---|---|
| `MeshBuilder_FlatPrimitives_HaveThreeVerticesPerTriangle` | One builder per primitive, then `ToMesh`. `vertexCount == 3 * TriangleCount == triangles.Length`. Expected `TriangleCount`: `Triangle` 1, `Quad` 2, `Box` 12, `Prism(sides 6)` 20, `Cylinder(8 sides, top radius 0)` 14, `Capsule(sides 8, rings 3, height 2, radius 0.4)` 96, `Icosphere(1)` 80. Also, for `Icosphere(subdivisions 0 and 2, radius 0.7)`, every vertex is within 1e-5 of 0.7 from the centre. |
| `MeshBuilder_FaceVertices_ShareNormalSlotAndColour` | One builder (seed 7, jitter 0.04): `Box` slot 3 at the origin and `Icosphere(subdiv 1)` slot 7 centred at (5, 0, 0). For every triangle: its 3 vertices have identical normals, uv1 and colours. The normal is within 1e-5 of `normalize(Cross(b - a, c - a))`. `dot(normal, centroid - shapeCentre) > 0` (outward). uv1.x is the slot. The colour has r == g == b, a == 1, and the grey is in [0.96 - 1e-6, 1.04 + 1e-6]. The two triangles of each Box side share one colour. At least one face has grey > 1, which proves float colours. |
| `MeshBuilder_Rebuild_IsByteIdentical` | Helper `BuildSample(seed)`: every primitive with non-trivial rotations, positions and slots. Two fresh builds with seed 42 compare `vertices`, `normals`, `colors`, `GetUVs(1)` and `triangles` element-wise by bits, plus equal `indexFormat` and `bounds`. A build with seed 43 differs in at least one colour and in no position. `Grid(8, 6, 1.5f)` built twice is identical too. |
| `MeshBuilder_Grid_SharesVertices` | `Grid(4, 3, 2f)`: `vertexCount 20`, `triangles.Length 72`, every `y == 0`, x in [-4, 4] and z in [-3, 3] with both extremes present, every normal `(0, 1, 0)`, every vertex index referenced, and the first triangle's geometric normal has y > 0. `Grid(96, 96, 1.25f)`: `vertexCount 9409`, `indexFormat UInt16`, bounds size `(120, 2, 120)`. |
| `FaceJitter_Grey_DeterministicAndWithinJitter` (extra; lives in `MeshBuilderTests.cs`) | Same inputs give the same value. Over faces 0..999: all values in [0.96 - 1e-6, 1.04 + 1e-6] and the mean is within 0.005 of 1. A different seed changes at least one value. `jitter 0` -> exactly 1. |

`RippleMathTests.cs` (new):

| Test | Cases |
|---|---|
| `RippleMath_Ring_PeaksOnTheFrontAndFadesByLifetime` | At age 1 and strength 1, the ring is `0.6` at dist 2.2 (within 1e-6) and 0 at dist 1.95 and 2.45. At age 0 and dist 0, it equals the strength. The front value decreases strictly with age at 0.5, 1.0, 1.5 and 2.0. Age 2.5 and 3.0 give 0 at any distance. Age -0.1 gives 0. The constants equal C 7.12. |
| `RippleMath_NextSlot_RecyclesOldest` | Starting from -1, eight calls give 0..7 and the ninth gives 0 (the oldest). Inputs 7 -> 0, 8 -> 0, -5 -> 0. |

### T12.5 Steps (TDD)
1. **Tests first.** Write the four test files with the 12 required tests and the 1 extra.
2. **Red.** Run `test-edit -Filter "FishGame.Tests.EditMode.(WaveMathTests|WaterSurfaceTests|MeshBuilderTests|RippleMathTests)"`. Expected: a compile failure on `WaveSet`, `WaveMath`, `RippleMath`, `FaceJitter`, `MeshBuilder` and `WaterSurface.Configure`.
3. **Core.** Write `WaveSet`, `WaveMath`, `RippleMath`, `FaceJitter`, `ShaderGlobals`. Then the `WaterSurface` edit.
4. **Green, part 1.** Rerun the filter. The Wave, Water and Ripple tests pass.
5. **MeshBuilder.** Write `MeshBuilder`, then rerun the filter until everything is green. If the face test reports inward normals for a primitive, flip that primitive's emission order. Never weaken the test.
6. **Full suite.** Run `test-edit` with no filter: all green, including M1 `WaterSurface_HeightAt_ReturnsFlatHeightEverywhere`.
7. **PlayMode.** Run `test-play` once: `WaterSurface` changed and the swim code reads it. Expect green; the waves stay flat until T22 configures them.
8. **Commit.**

### T12.6 Acceptance (reviewer checks)
- **Core.** No `UnityEngine` and no RNG in Core. `WaveMath` handles `Wavelength <= 0` or `Amplitude == 0` as 0, computes the phase in `double`, and pins the direction convention in a comment.
- **`WaterSurface`.**
  - The M1 members are kept.
  - A flat set never reads `Now`.
  - `ResetToFlat` runs on `SubsystemRegistration`.
  - Every test that configures it resets in `TearDown`.
- **`MeshBuilder` determinism.**
  - Float colours, slot in UV channel 1, explicit `indexFormat`.
  - No `Recalculate*` or `Optimize`, and no hash-container iteration.
  - Angles are computed per index.
- **`MeshBuilder` geometry.** Outward clockwise winding. Triangle counts match T12.4. Grid bounds are inflated by +-1 m.
- **Constants.** The `RippleMath` constants and formula match C 7.12, plus the `age < 0` refinement. The `ShaderGlobals` strings match C 5.3.
- **Results.** 13 new tests by exact name; EditMode and PlayMode green.

### T12.7 Commit
```powershell
git add Assets/_Project/Core/World.meta Assets/_Project/Core/World/WaveSet.cs Assets/_Project/Core/World/WaveSet.cs.meta Assets/_Project/Core/World/WaveMath.cs Assets/_Project/Core/World/WaveMath.cs.meta Assets/_Project/Core/World/RippleMath.cs Assets/_Project/Core/World/RippleMath.cs.meta Assets/_Project/Core/Render.meta Assets/_Project/Core/Render/FaceJitter.cs Assets/_Project/Core/Render/FaceJitter.cs.meta Assets/_Project/Core/Render/ShaderGlobals.cs Assets/_Project/Core/Render/ShaderGlobals.cs.meta Assets/_Project/Runtime/World/WaterSurface.cs Assets/_Project/Runtime/World/MeshBuilder.cs Assets/_Project/Runtime/World/MeshBuilder.cs.meta Assets/_Project/Tests/EditMode/WaveMathTests.cs Assets/_Project/Tests/EditMode/WaveMathTests.cs.meta Assets/_Project/Tests/EditMode/WaterSurfaceTests.cs Assets/_Project/Tests/EditMode/MeshBuilderTests.cs Assets/_Project/Tests/EditMode/MeshBuilderTests.cs.meta Assets/_Project/Tests/EditMode/RippleMathTests.cs Assets/_Project/Tests/EditMode/RippleMathTests.cs.meta; git commit -m "m2(water): WaveMath, WaterSurface wave/time seam, MeshBuilder, RippleMath, FaceJitter, ShaderGlobals" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Notes for later sections (read-only; consumers of T10-T12)

- **T13** (`SfxCueTableDef.ToRecipes`): uses the 9-arg `SfxRecipe` constructor with `minDistance` **last**, and takes length and loop from `SfxRecipes.TryGet`. `WaterSettingsDef.ToWaveSet` maps `dirAmpLenSpeed[i]` (x deg, y amplitude, z wavelength, w speed) to `W0..W2` in that order.
- **T14:** seeds the cue table from `SfxRecipes.All` (18 rows, the T11.2 mixing values). The AppBootstrap hook `audioBus.ApplyCueTable(registry.SfxCueTable?.ToRecipes())` runs after `Initialize`, which works (T11.3).
- **T16:**
  - `PlayerLocomotion.SetContext` uses `GameplayContextArbiter.Resolve`.
  - `IsAiming` -> x0.6 speed, no sprint. `LocksMovement` -> no wish, no jump.
  - `PlayerInteractor` uses `InputContextRouter.InteractLive(input.GameplayContext)`.
- **T18:** reads `settings.fishingAssist` at release. It never reads `IDevInput` (that is T26).
- **T20:** the Toon shader reads the palette slot from `TEXCOORD1.x` (`mesh.SetUVs(1, ...)`) and the grey from the vertex colour (float, may exceed 1). Body meshes built with `MeshBuilder` already carry both.
- **T21 / T22:**
  - The HLSL wave term must equal `WaveMath`'s: direction `(sin deg, cos deg)` on (x, z), `k = 2*pi/L`, `A * sin(k * (dot - S * t))`, and `L <= 0` or `A == 0` -> 0.
  - `_Waves` packing is not fixed by C (Contract questions 4).
  - The ripple ring uses `RippleMath.Ring`, including `age < 0 -> 0`.
  - Property names come from `ShaderGlobals`.
  - `WaterTimeDriver` calls `WaterSurface.Configure` and `SetTimeSource`, and calls `ResetToFlat` on destroy.
- **T23:**
  - Sounds come from `RodCueAudio.SfxFor`. A stinger calls `Duck(seconds)`.
  - `Dive_Thrum` and `Line_Creak` use `StartLoop`/`SetLoop`/`StopLoop`: `spatial: false` for the owner and `true` (following the bobber or player) for remotes.
  - `Reel_Click` ticks: `Play2D` for the owner, `PlayAt` for remotes.
  - The `ChargeStart` whoosh pitch follows the charge through `PlayAt(..., pitchMult)` at the owner's rod position. `Play2D` has no pitch overload (Contract questions 8).
  - Captions are sent as `CaptionRequested`.
- **T25:** the "assist on sets the gauge to Arc once" rule lives in the settings UI, not in `SettingsData.Clamp`.
- **T26:** resolves `ServiceRoots.App.Get<IInputService>() as IDevInput`, null-safe. The PlayMode fake exposes `DevPressed`.

---

## Contract questions

1. **`SfxRecipe` 9-arg constructor order (T11, T13).**
   - C 7.11 says "a 9-arg ctor" without the parameter order.
   - This plan appends `minDistance` last: `(id, length, loop, category, baseVolume, spatial, maxDistance, minInterval, minDistance)`. The first eight positions then keep their M1 meaning, and no float can silently shift.
   - T13's `ToRecipes` must use this order. Section D (`2026-10-11-m2-plan-data-tuning.md`) agrees: it tells T13 to read the order from `SfxRecipe.cs`, so this is a confirmation request, not a conflict. Please confirm, or pin another order in C.
   - **Related conflict with Section D's cue-table validator.** D validates `maxDistance > minDistance` and seeds every row from the Core recipe. The M1 2D rows (`Ui_*`, `Wind_Ambient`, `Water_Lap`) have `MaxDistance 0`, which `AudioBus` reads as "default 30", and `MinDistance 1`, so the rule would reject them. The controller should pick one fix: (a) T14 seeds `maxDistance 30` when the Core value is 0; (b) D's validator treats 0 as "default"; or (c) T11 changes the M1 rows to `30`. Option (c) would break the M1 test `SfxRecipes_M1Table_MatchesContract` only if it asserted those values. It does not for UI, but it is an M1 value change, so this plan does not do it.
2. **`InteractLive(Carrying)` (T10).**
   - C 7.10 lists Free, RodReady, RodWaiting and Swimming.
   - Spec 8.2 gives Carrying "Interact", and the router still passes Interact for Carrying (D18).
   - This plan follows C (Carrying -> false). Carrying is unreachable in M2, so M5 should revisit it.
3. **`ShaderGlobals` identifiers (T12).**
   - C 5.3 fixes the property strings but not the C# constant names.
   - This plan uses `Palette, Ripples, Waves, WaterTime, ToonShade, ToonAmbient, SkyTop, SkyHorizon, SkyGround`.
   - T21 and T22 should use these. Please add them to C, or name others.
4. **Water and mesh conventions that C leaves open (T12 -> T20, T21, T22).**
   - (a) Wave direction: `0 deg = +Z`, `dir = (sin, cos)` on (x, z), the same as the `CastMath` yaw.
   - (b) The `_Waves` Vector4 packing is not in C. Suggested: `_Waves[i] = (DirectionDeg, Amplitude, Wavelength, Speed)` for i = 0..2, with `_Waves[3] = 0`, mirroring `WaterSettingsDef.dirAmpLenSpeed`. T22 is the owner.
   - (c) The palette slot is in mesh UV channel 1 (`SetUVs(1, ...)`, TEXCOORD1), which is the spec's `uv1.x`, not `mesh.uv`.
   - (d) `_WaterTime` is a float in HLSL. After hours of `ServerTime`, the C# (double) and GPU phases can drift by a few hundredths of a radian, a centimetre-scale visual offset at 0.1 m amplitude. If that matters, T22 may push a wrapped time. That needs a shared wrap period, which C does not define.
5. **`RippleMath.Ring` with negative age (T12 -> T21).** C gives the formula for age >= 0 only. This plan returns 0 for `age < 0` (a clock jump when `WaterTimeDriver` switches to `ServerTime`). T21's shader should match with `step(0, age)`.
6. **Rate limit per source (T11).**
   - Spec 11.7 says "rate-limited per source by minInterval". The M1 `AudioBus` limits per id.
   - Without a change, two friends' nibbles or splashes within the minInterval would merge in co-op.
   - This plan keys `PlayAt` by id plus a 2 m position cell, with no public API change. `Play2D` stays per id. Please confirm.
7. **Staggered moves and reels at once (M5, informational).**
   - Under D18 Staggered routes like Free (Move passes), yet `RouteFight` is live in Staggered, so S would both move and reel.
   - It is unreachable in M2. `InputContextRouter_RodContexts_SameKeyNeverMovesAndReels` is therefore scoped to Free plus the four rod contexts.
   - M5's Staggered row (Move zeroed, spec 8.2) fixes it.
8. **Pitched owner-only 2D sound (T11 -> T23).**
   - C 7.11 adds `PlayAt(id, pos, pitchMult)` but no pitched 2D play.
   - Spec 9.11 wants the charge whoosh pitch to follow the charge, and the owner hears its own rod. T23 can use `PlayAt` at the camera/rod position, which is effectively 2D for the owner.
   - If a true 2D pitched play is wanted, C needs `Play2D(id, category, pitchMult)`. This plan does not add it.
9. **`AmbienceBed` and the cue table (informational).** `AmbienceBed` (an M1 file not owned by any M2 task) reads `SfxRecipes.TryGet` directly for the wind base volume, so a cue-table override of `Wind_Ambient` will not reach the wind loop. M2 tunes only fishing cues, so this is harmless. It can be fixed when M3 touches ambience.
10. **`SettingsData.version` (T10).** It stays 1. `JsonUtility` keeps the defaults for the missing M2 fields, so no migration is needed. Confirm that no version bump is wanted.
11. **`SfxRecipes.cs` size (T11).** The 12 new generators take the file to about 450 lines, over the ~300-line rule. C 12 lists one file. Should a partial `Core/Audio/SfxRecipes.M2.cs` (holding the private M2 generators) be added to T11's manifest, or is one long file accepted? Without an answer, T11 keeps one file.
