# FishGame M2 - Plan Section G: UI, world, acceptance (T25, T26, T28, T29, T30)

- **Date:** 2026-10-11. Branch `m2-fishing-core`.
- **Status (2026-10-12):** not started. Each task header below carries a "Carry-forward (2026-10-12)" block (T25, T26, T28, T29) and T30 has the criterion 7 wording, fun-gate metric notes and section J; contract C 16.2 lists them all.
- **Contract ("C"):** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (binding). **Skeleton:** `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md`.
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` sections 9.11, 9.13, 10.1-10.7, 11.1-11.4, 11.6, 17.2 and 3.2 (R62-R64).
- **Scope:** T25 Fishing HUD, T26 Dev overlay, T28 Lake dock rebuild, T29 Blender species models (controller checklist), T30 Acceptance and final hand-off.
- **Shape:** behaviour, signatures, checks and acceptance. None of these tasks is on the skeleton 1.3 list of code-carrying bodies, so no task below contains complete code. No task below is Haiku-eligible.
- **Contract doubts** were listed as questions; every one is now closed by a controller ruling ("Contract questions" at the end, C 15).

---

## G0. Rules shared by every task in this section

**The controller sends G0 with every T25, T26, T28 and T30 brief** (implementers see only their own task text plus C 2.8). The task bodies refer to G0 by number.

### G0.1 Read before starting
- `CLAUDE.md`; C 2.8 (global constraints, all 21 items); C 2.6 (shared-file edits); C 12 (your owned files); your task's C 7 entry.
- The spec sections cited in your task header.

### G0.2 Global constraints that bite here (C 2.8 by number)
- **#1** Dev-only surfaces (F3 overlay, F4-F9 keys) check `Debug.isDebugBuild` at runtime. Never `DEVELOPMENT_BUILD` or `#if DEBUG`.
- **#2** ASCII in code and docs. **Do not rewrite `UI/UiStrings.cs` wholesale.** It already holds two non-ASCII M1 lines (`pause.masked` and `err.version`). Edit it in place with the Edit tool and never "fix" those two lines. New keys and texts are ASCII only. The controller's non-ASCII scan for T25/T26 checks **added lines only**, for example `git diff -U0 main -- <file> | grep '^+' | grep -P '[^\x00-\x7F]'`.
- **#6** Scene churn: `git restore Assets/_Project/Scenes/*.unity` unless the scene really changed (T28 changes it on purpose).
- **#8** EditMode tests never reach UI or Presentation. **C 10 lists no tests for T25, T26, T28, T29 or T30.** Do not invent "required" test names. The gate for these tasks is `compile`, `build`, the full `test-edit` and `test-play`, the validators, and the manual checks written in each task. An extra committed test file needs the controller's OK first, because it is not in C 12. Temporary verification files that are deleted before the commit (T28's fingerprint test, T29's lineup script) are allowed, and the reviewer checks they are gone.
- **#9** PlayMode must stay warning-free. A HUD exception caught by `EventBus` is logged as an error and fails PlayMode.
- **#11** Never kill Unity. The wrapper tests whether the editor lock is HELD: if a run prints `LOCKED` (exit 2), stop and ask the user to close the editor. A leftover `Temp/UnityLockfile` that is not held (Unity leaves it after a batch run) is not a lock.
- **#13** Any batchmode render (T29) sets `ShaderUtil.allowAsyncCompilation = false` first and restores it after.
- **#14** Scene builders load assets **after** `EditorSceneManager.NewScene` (T28).
- **#15** No in-scene-placed NetworkObjects (T28 adds plain GameObjects only).
- **#17** UI never writes gameplay state. It reads `FishingRodNetwork.ViewState`, read-only `FishingRodController` members (including `Machine` getters), events and NetworkVariables. The only "writes" are the debug RPCs and debug statics C 7.26 names for T26.
- **#19** Every renderer with a `FishGame/Toon` material needs a mesh with vertex colours (T28, T29).
- **#20, #21** Explicit commit paths. Logs and results go in `$env:FG_OUT`, never in the repo.

### G0.3 Commands (Windows PowerShell; C 11)
```powershell
$env:FG_OUT = '<absolute session scratchpad>\fg'      # outside the repo, no '..'
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 check
```
The last line of every run is `FG RESULT <command> PASS|FAIL|LOCKED|TIMEOUT`. Exit 0 alone is not proof: read the `FG RESULT` line, and on failure the `FG LOG` / `FG FAILED` lines.

### G0.4 Pre-flight (every task)
1. `git -C C:\Dev\FishGame status --porcelain --untracked-files=all` and save it as the task baseline in the scratchpad.
2. `git -C C:\Dev\FishGame log --oneline -5`: the dependency commits from your header are present.
3. Lock: no separate check. The first `unity.ps1` run reports `LOCKED` (exit 2) if the user has the editor open: then stop and ask them to close it. A leftover `Temp/UnityLockfile` file alone is not a lock.

### G0.5 Commit shape (C 2.1)
`git -C C:\Dev\FishGame add <explicit paths>; git -C C:\Dev\FishGame commit -m "m2(<area>): <summary>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`. Areas used here: `hud`, `dev`, `lakedock`, `models`, `docs`. Integration fixes (T28, T30 only, C 2.6 rule) get their own commit: `fix(<file>): <what>`.

---

## T25 - Fishing HUD

**Header**
- **Goal:** The local player's fishing HUD (spec 11.4, 9.11 accessibility): charge ring, "Click!" prompt, captions, line gauge (Arc/Bar), creel count and kg, catch / lost / creel-full / clean toasts, and the three fishing settings rows.
- **Owned files (C 12):** `Assets/_Project/UI/Fishing/FishingHudPresenter.cs` (new). Edits: `UI/UiFlowController.cs` (one hook line), `UI/UiStrings.cs`, `UI/SettingsPresenter.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`.
- **Shared edits (C 2.6):** `UiFlowController.cs`: exactly one hook line that creates `FishingHudPresenter`. `UiStrings`, `AppRoot.uxml`, `App.uss`, `SettingsPresenter`: T25 first, then T26.
- **Depends on:** T2 (`IHudExtension`, `UiFlowController.AttachHudExtension`), T18 (`FishingRodController`), T24 (presentation landed; the HUD complements it). Reads: T9 `CreelRules`/`CreelSnapshot`, T10 `SettingsData.lineGauge/captions/fishingAssist`, T11 `CaptionRequested`, T13 `ContentRegistry` (`FishingTuning`, `ArtPalette`, `DisplayNameOf`), T16 `PlayerCreel`, T17 `CatchAnnounced`.
- **Implementer:** Sonnet. **Reviewer:** Opus high.
- **Tests:** none in C 10 (C 2.8 #8). Gate: `compile`, `build`, full `test-edit`, full `test-play`, then the manual checks below (controller).
- **Biggest trap (G0.2 #2):** `UiStrings.cs` already holds two non-ASCII M1 lines. Edit the file in place and leave those lines alone.
- **Spec:** 9.11 (accessibility block), 11.1 (UI Toolkit rules), 11.4 (HUD table), 11.6 (settings rows), 10.2 (shape-not-hue rule).

**Carry-forward (2026-10-12, C 16.2; from the T2, T10, T16 and T17 reviews). These are requirements of this task:**
- **`OnAssistChanged` pure helper + EditMode test.** Extract the G-Q4 rule (assist turned on while the gauge is Off sets Arc, every time; any other change leaves the gauge alone) into a pure static helper `OnAssistChanged(data, old, new)` in the presenter's file (or beside it), no `VisualElement` or `ISettings` dependency, and add an EditMode test for it (this task's only automated test; name the wrong implementation each case catches: "sets Arc only the first time", "changes the gauge when assist turns off", "overwrites Bar/Arc when the gauge is already on").
- **Creel HUD reads `PlayerCreel.Count` at spawn.** The initial NetworkList sync raises no `Changed`, so the HUD must read `Count` and `TotalWeightG` when the local `PlayerCreel` appears, not only on `Changed`.
- **Creel HUD must not reveal a species before `CatchAnnounced`.** On the host the creel `Changed` and `FishCaught` fire one frame BEFORE `CatchAnnounced`: the creel widget shows count and weight only (no species name or icon), or waits for the announce.
- **`IHudExtension.Bind` must never throw.** `UiFlowController`'s bind loop has no try/catch (T2 review): a throw blocks the whole UI. Guard everything inside `Bind` (missing services, null elements) and return quietly.
- **Extension-created buttons play their own `Ui_Click`.** The click-sound loop runs before the bind loop, so buttons the HUD creates after it must call `AudioBus.PlayUi(SfxIds.UiClick)` themselves.
- Manual smoke list (T2.7 in the carry plan) gained steps 3b, 5b, 6b, 7, 8 in the T2 review; the controller runs them again after T25 because the HUD attaches through the same hook.

### T25.1 Public surface (C 7.25, exact)
```csharp
internal sealed class FishingHudPresenter : IHudExtension   // UI/Fishing/FishingHudPresenter.cs, namespace FishGame.UI
{
    public void Bind(VisualElement root, ServiceRegistry app);
    public void Tick(float dt);
    public void Unbind();
}
```
- `UiStrings` (edit): add `public static string Format(string key, params string[] args)`. **Keep** the existing `Format(string key, string arg)`: M1 callers bind to it. Never pass a literal `null` as the only argument after the key, because that call would be ambiguous between the two overloads (C# CS0121).
- New `UiStrings` keys, exactly as C 7.25 gives them: `cap.bite` "Bite!", `cap.straining` "Line straining", `cap.snapped` "Snapped", `cap.escaped` "Got away", `hud.click` "Click!", `hud.creel` "{0}/{1}  {2} kg", `hud.creel_full` "Full - sell!", `toast.catch` "{0} caught {1} kg {2} (${3})", `toast.lost` "It got away!" (generic: covers snap and escape; ruling G-Q3), `toast.creel_full` "Creel full! {0} released", `toast.clean` "Clean catch!", `settings.line_gauge` "Line gauge", `settings.captions` "Captions", `settings.assist` "Fishing assist" (the M1 `settings.*` prefix, ruling G-Q2; C 7.25 amended).
- Additive keys (rulings G-Q1, G-Q2, R65): `toast.raw` "{0}" (passthrough for pre-formatted toasts), `toast.junk` "{0} fished up {1} (${2})", `toast.pity` "{0} has a lucky line!" (published through `toast.raw`), `settings.gauge_off` "Off", `settings.gauge_arc` "Arc", `settings.gauge_bar` "Bar".

### T25.2 The hook (UiFlowController, one line)
In `BindScreens` after the M1 HUD presenter is created (post-T2 layout): `AttachHudExtension(new FishingHudPresenter());`. Nothing else in `UiFlowController.cs` changes. T2 defines when `Bind`, `Tick` and `Unbind` run. Read T2's `AttachHudExtension` before coding, and confirm that `Tick` runs only while the HUD layer is visible (Hud, Pause, Settings-over-Pause) and that `Unbind` runs in `OnDestroy`.

### T25.3 UXML and USS
Add inside `ScreenHud` (all `picking-mode="Ignore"`, none focusable):

| Element name | Type | Class | Anchor |
|---|---|---|---|
| `FishingChargeRing` | VisualElement (custom drawn) | `fishing-ring` | centred on the crosshair, 72 x 72 px |
| `FishingLineGauge` | VisualElement (custom drawn) | `fishing-gauge` | just below the crosshair: Arc 140 x 70 px, Bar 160 x 8 px |
| `FishingHookPrompt` | Label | `fishing-hook` | bottom-centre, 120 px above the bottom edge |
| `FishingCaption` | Label | `fishing-caption` | bottom-centre, 72 px above the bottom edge |
| `FishingCreel` | Label | `fishing-creel` | bottom-right, `right: 16px; bottom: 16px` |

Add in `ScreenSettings`, directly after `CameraMotion` (9.11 groups these with Camera motion): `SliderInt name="LineGauge" class="setting" low-value="0" high-value="2"`, `Toggle name="Captions" class="setting"`, `Toggle name="FishingAssist" class="setting"`.

USS (`App.uss`):
- Add the five classes above, plus `fishing-creel.warn` and `fishing-creel.full`. Text uses the existing outline style of `.hud-prompt`.
- Change `.toast-stack` from `bottom: 16px` to `bottom: 48px`, so toasts stack above the creel line. Leave every other M1 rule unchanged.
- Spec 11.4: nothing over the screen centre except the crosshair and the charge ring, so the gauge sits below the crosshair and the prompt and captions sit bottom-centre.

### T25.4 Behaviour

**Bind(root, app).**
- Resolve the services: `EventBus` and `ISettings`, plus the concrete `ContentRegistry` (registered as its type by `AppBootstrap`).
- Query the elements with `UiQuery.Req`.
- Subscribe to the bus messages `CaptionRequested` and `CatchAnnounced` (App bus), to `ISettings.Changed`, and to the static `FishingRodController.LocalChanged`.
- Cache the creel caps and threshold colours:
  - Read `registry.FishingTuning` (explicit `!= null` check: it is a Unity object, never `?.`), then `.ToCore()` for `CreelMaxEntries` and `CreelMaxWeightG`. If the registry has no fishing tuning, hide the creel and gauge and keep everything else working.
  - Read the colours from `registry.ArtPalette` getters (`UiAccent`, `UiWarn`, `UiBad`, `UiText`, `UiPanel`, PascalCase per C 7.13). When the palette is missing, fall back to fixed USS-like colours.
- Hide every fishing element until `Tick` decides.

**Local references (lifecycle).**
- The presenter outlives sessions: `UiFlowController` lives in the App root. On `LocalChanged` and in `Tick` (a cheap reference compare), re-resolve these references:
  - `_controller = FishingRodController.Local`
  - `_rod` = its `FishingRodNetwork`, found with `GetComponent` on the same GameObject
  - `_creel` = `PlayerNetState.Local`'s `PlayerCreel`
- When the creel reference changes, unsubscribe the old `PlayerCreel.Changed` and subscribe the new one. A destroyed component (Unity null) counts as null.
- `Local == null` is a normal state (menu, loading, spawn gap). Hide every per-player element and never throw.

**Tick(dt).** It updates each element below. It repaints custom-drawn elements (`MarkDirtyRepaint`) only when the drawn value changes by more than 0.002 or the mode or visibility changes (spec 11.1: frequent HUD updates stay in their own subtree, without per-frame rebuilds).

1. **Charge ring.**
   - Visible iff `_controller != null && _controller.Machine.State == RodState.Charging`.
   - Draw it in `generateVisualContent` with `MeshGenerationContext.painter2D`: a dim full circle (2 px, `UiPanel` at 60% alpha), then an arc that starts at 12 o'clock and sweeps clockwise by `ChargeNorm * 360` degrees (4 px, `UiAccent`).
   - `ChargeNorm` ping-pongs (C 7.5 `CastMath.ChargeNorm`), so the arc shrinks again; that is the intended read.
   - Hidden in Ready, because the in-world marker (T24) covers Ready.
2. **"Click!" prompt.**
   - Visible iff `Machine.State == RodState.Bite && Machine.HasResult && Machine.Roll.HookWindow > 0`. Text `hud.click`.
   - It shows **independently of the captions setting**: it is the gameplay prompt of spec 11.4. When `HookWindow == 0` the bite auto-hooks and there is no prompt.
3. **Captions.**
   - On `CaptionRequested(Key, Seconds)`: when `settings.Current.captions` is false, ignore it. Otherwise set `FishingCaption.text = UiStrings.Get(Key)` and show it for `Seconds` (`<= 0` means 2 s). A newer caption replaces the current one and restarts the timer. Count the timer down with the `dt` passed to `Tick`.
   - Turning captions off while one is visible hides it at once (`ISettings.Changed`).
   - T23 already filters on the setting; this second check is intentional.
4. **Line gauge** (local line only, spec 9.11).
   - Mode = `(LineGaugeMode)settings.Current.lineGauge`. Visible iff the mode is not `Off`, `Machine.State == RodState.Hooked` and `Machine.Fight != null && !Machine.Fight.IsJunk`.
   - Value = `Machine.Fight.State.Tension`. Grace = `Machine.Fight.State.SnapTimer > 0` (tension has reached 1 and the snap grace timer is running).
   - **Arc:**
     - A 90-degree arc centred below the crosshair, spanning the bottom quarter of a circle of radius 60 px. In screen degrees with y down, that runs from 135 to 45, low end on the left.
     - The background arc is 3 px dim. The fill runs from the left end proportional to the value.
     - A radial tick crosses the arc at the 0.8 position (always drawn).
   - **Bar:** a 160 x 8 px track with a fill, and a 2 px vertical tick at 80% of the width.
   - **Readability without hue alone (10.2).**
     - Below 0.8: fill colour `UiAccent`.
     - At or above 0.8: fill `UiWarn` and the stroke thickens by 2 px.
     - In grace: the fill blinks at 6 Hz (on/off, `Time.unscaledTime`) in `UiBad`. The tick and the blink carry the meaning even with colour blindness.
   - Hidden in Bite (tension is 0) and in every terminal state.
5. **Creel.**
   - Visible iff `_creel != null` and the caps are known.
   - Snapshot = `_creel.Snapshot(maxEntries, maxWeightG)`.
   - Text: if `CreelRules.IsFull(snapshot)`, then `hud.creel_full` with class `full`. Otherwise `UiStrings.Format("hud.creel", ...)` with invariant-culture STRING arguments (`count.ToString(Invariant)`, `maxEntries.ToString(Invariant)`, kg; `Format` takes `params string[]`), where kg = `TotalWeightG / 1000f` formatted `"0.0"` with `CultureInfo.InvariantCulture`, plus class `warn` when `CreelRules.IsWarn(snapshot)`.
   - Recompute only on `PlayerCreel.Changed` or when the creel reference changes, not every frame.
   - `warn` = bold, `UiWarn`, and a 3 px left border. `full` = bold, `UiBad`, and the different text. The shape and text change carry the meaning, not hue alone.
6. **Catch toasts** (`CatchAnnounced`; App bus; every peer).
   - Let `a = msg.Announcement`, `local = msg.IsLocalCatcher`, `item = registry.DisplayNameOf(a.Catch.IsJunk, a.Catch.ItemIndex)`.
   - Catcher name: the `PlayerNetState` in `PlayerNetState.Active` whose `Slot == a.CatcherSlot`, using its `DisplayName`, the same lookup `NameplateOverlay` uses. If that player has gone (left before the announce arrived), use `"?"`.
   - **Rate limit (crew only, spec 11.4).** For `!local`, keep the last shown time per catcher slot (`Time.unscaledTime`). Drop a crew toast that arrives within `FishingConstants.CrewToastMinIntervalSeconds` (3 s) of the previous one for that slot; the in-world reveal still shows. Local toasts are never rate-limited.
   - By `a.Kind`:
     - `Landed`, fish: `toast.catch` with (name, kg `"0.0"`, item, `ValueCoins`). Landed junk: `toast.junk` with (name, item, `ValueCoins`). Show them for everyone (rate-limited for crew).
     - `Landed` with `a.CleanBonus && local`: also `toast.clean`. The clean toast is local only (crew see the reveal sparkle from T23).
     - `Lost`: `toast.lost` (the generic "It got away!", no argument; it covers snap and escape; ruling G-Q3, no wire change), for everyone (rate-limited for crew).
     - `PityCast` (R65 / M2-D23; announced when a bad-luck-nudge cast is accepted): `toast.pity` with (name), for everyone including the local player (not rate-limited: at most one per three failed casts). The announcement's `Catch` is `default`: do not index the registry with it.
     - `ReleasedCreelFull`: `toast.creel_full` with (item), **local only** (it is about your own creel).
     - `None`: nothing.
   - **Delivery (ruling G-Q1).** `ToastRequested` carries one `Arg`, and `ToastPresenter` (M1, not owned here) formats with one argument. So format the full text first with `UiStrings.Format(key, params args)`, then publish `new ToastRequested("toast.raw", text, 4f)`. Never publish `toast.catch` and its siblings directly: a single-argument format of a four-placeholder template throws `FormatException`, which is logged as an error and fails PlayMode.
   - All numbers are formatted with `CultureInfo.InvariantCulture`.
7. **Settings rows (SettingsPresenter).** Keep the M1 bind pattern (refreshers, `NotifyChanged`, no save per tick).
   - `LineGauge`: a `SliderInt` 0..2 bound to `lineGauge`. Its label is `settings.line_gauge` + two spaces + the value name (`settings.gauge_off` / `settings.gauge_arc` / `settings.gauge_bar`). Add a private `BindChoice` helper next to `BindInt`.
   - `Captions`: `BindBool` on `captions`, label `settings.captions`.
   - `FishingAssist`: a `BindBool`-like bind on `fishingAssist`, label `settings.assist`, with the C 7.10 rule (ruling G-Q4): on every **false-to-true change** of the toggle, if `lineGauge == 0`, set `lineGauge = 1` (Arc), call `NotifyChanged`, and refresh the gauge slider. Every time, not once: there is no persisted flag, because `SettingsData` belongs to T10. Turning assist off never changes the gauge, and the player can then turn the gauge off separately.
   - The slider and toggles are focusable `.setting` controls inside the existing `SettingsScroll`, so M1 keyboard and gamepad navigation and `OnFocusIn` scrolling cover them. Do not use `DropdownField`: its popup is not reliable with gamepad-only navigation (M1 criterion 11).

**Unbind().** Dispose the bus subscriptions, unsubscribe `ISettings.Changed`, `LocalChanged` and `PlayerCreel.Changed`, hide the elements, and drop the references. Calling it twice is safe.

**Errors.** Every handler tolerates missing data (null registry entries, an index out of range from `DisplayNameOf`; catch only `ArgumentOutOfRangeException` and use `"?"`). Never `Debug.LogError` on a normal path. A real defect may throw: the bus logs it, and PlayMode surfaces it.

### T25.5 Steps
1. Pre-flight (G0.4). Read `UI/UiFlowController.cs`, `UI/IHudExtension.cs`, `UI/UiSessionActions.cs` (T2), `UI/HudPresenter.cs`, `UI/ToastPresenter.cs`, `UI/SettingsPresenter.cs`, `UI/NameplateOverlay.cs`, `Fishing/Owner/FishingRodController.cs`, `Gameplay/Fishing/PlayerCreel.cs`, `Fishing/Net/CatchAnnounced.cs`, `Runtime/Services/CaptionRequested.cs`, and `Presentation/Feedback/FishingFeedback.cs`. Confirm which `EventBus` T23 publishes `CaptionRequested` on, and which one `FishingRodNetwork` publishes `CatchAnnounced` on (C 8.1 says the App bus). Subscribe on the same bus, or captions silently never appear. If the two disagree with C 8.1, report it.
2. Edit `UiStrings.cs` (keys and overload) with the Edit tool. Run `compile`.
3. Edit `AppRoot.uxml` and `App.uss`. Write `FishingHudPresenter.cs`. Add the hook line. Run `compile`.
4. Edit `SettingsPresenter.cs` (three rows, assist rule). Run `compile`.
5. Run `build`, the full `test-edit` and the full `test-play`. All must pass, with no new warnings in the PlayMode output.
6. Run the added-lines ASCII scan (G0.2 #2) on the six files.
7. Commit (G0.5), literally (add `Assets/_Project/UI/Fishing.meta` only if `git status` shows it as new):
```powershell
git -C C:\Dev\FishGame add Assets/_Project/UI/Fishing/FishingHudPresenter.cs Assets/_Project/UI/Fishing/FishingHudPresenter.cs.meta Assets/_Project/UI/UiFlowController.cs Assets/_Project/UI/UiStrings.cs Assets/_Project/UI/SettingsPresenter.cs Assets/_Project/UI/Uxml/AppRoot.uxml Assets/_Project/UI/Uss/App.uss; git -C C:\Dev\FishGame commit -m "m2(hud): fishing HUD - charge ring, Click prompt, captions, line gauge, creel, catch toasts, settings rows" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T25.6 Acceptance
**Reviewer (Opus high) checks:**
- The C 7.25 surface is exact: `internal sealed class FishingHudPresenter : IHudExtension`, the keys, and the `Format` overload with the old overload kept.
- The only `UiFlowController` change is one hook line. No file outside the T25 list is touched.
- Nothing writes gameplay state. Reads go through `Machine` getters, `PlayerCreel`, `ViewState` and events (C 2.8 #17).
- The lifecycle: re-resolution on `LocalChanged`, `PlayerCreel.Changed` unsubscribed on change and in `Unbind`, null-safe across leave and rejoin and a scene reload.
- The toast route is pre-formatted through `toast.raw` (also `toast.pity`). The crew rate limit is keyed per slot, and local toasts are never limited. Clean and creel-full toasts are local only.
- The gauge shows the local line only, only while Hooked on a fish. It has the 0.8 tick, the warn thickening and the grace blink. The prompt and captions are off-centre.
- Assist-on sets the gauge to Arc on every false-to-true edge, and only from Off (ruling G-Q4).
- Invariant culture everywhere. No per-frame allocations in `Tick` beyond string changes on a value change.

**Controller manual checks** (editor, after the commit; record PASS/FAIL in the task report):
1. Solo with F6 forced bite (after T26) or a normal wait. Ready, then charge: the ring fills and ping-pongs. Hook a fish with the gauge on Arc: the tick sits at 0.8, the fill thickens past it, and it blinks in grace. Switch to Bar: same reads.
2. Captions on: "Bite!" appears bottom-centre. Captions off: none appears, and an active caption disappears.
3. With assist off, gauge Off and assist then switched on, the gauge becomes Arc. Turn the gauge Off again with assist still on: it stays Off. Toggle assist off and on again: it becomes Arc again (every false-to-true edge, ruling G-Q4).
4. With F7 (twitchy, host), the bite shows "Click!" and a click hooks it. Without F7, no "Click!" appears.
5. Creel: land 10 fish (or until 80% of the cap): warn style appears. At the cap: "Full - sell!", and the next landed fish shows "Creel full! X released". Ring the bell (debug sell): the count resets.
6. Game view at 1280x720, UI scale 150%: the ring, gauge, prompt, caption, creel and toasts don't clip or overlap the crosshair.
7. Keyboard only, then gamepad only: reach and change the three new settings rows, then leave Settings.
8. MPPM with 2 instances (dev-direct): a client's catch shows a toast on the host. Two client catches within 3 s show one crew toast on the host, while the client sees both of its own.

---

## T26 - Dev overlay

**Header**
- **Goal:** The F3 dev overlay (spec 9.11 last bullet, 17.2 item 5) and the F4-F9 debug keys through `IDevInput` (C 7.26, C 5.3 dev keys, M2-D18).
- **Owned files (C 12):** `Assets/_Project/UI/Fishing/DevOverlayPresenter.cs` (new). Edits: `UI/UiFlowController.cs` (one hook line), `UI/UiStrings.cs` (`dev.*` keys), `UI/Uxml/AppRoot.uxml` (`DevOverlay` element), `UI/Uss/App.uss` (`.dev-overlay`).
- **Shared edits (C 2.6):** after T25 in all four shared files (serial; skeleton section 5).
- **Depends on:** T25. Reads: T10 `IDevInput`/`DevKey`, T12 `RippleMath`, T17 `FishingRodNetwork` (RPCs, NetVars, `Active`, `Spawned`/`Despawned`), `FishingHost`, `FishingDebugSettings`, `CastProbe`, T18 `FishingRodController`, T22 `RippleField`, T13 registry.
- **Implementer:** Sonnet (may share one dispatch with T25, as separate commits and reviews, skeleton section 5 batches). **Reviewer:** Opus high.
- **Tests:** none in C 10. The gate is as for T25.

**Carry-forward (2026-10-12, C 16.2; from the T8-fix, T10, T16 and T17 reviews). Requirements of this task:**
- **F4 / F5 rod and bait selection can race the owner's Readying/Charging.** The host answers `SelectRodRpc`/`SelectBaitRpc` and re-derives landing from the HOST rod's `castMaxDist`. On a valid `CastResult` the owner controller therefore adopts the host's landing x/z (keeping `FlatHeight`) or, at the very least, calls `LogOnce` when the host landing differs from the owner's by more than 0.05 m. The change is in `FishingRodController` (T18's file): do it under the integration-fix rule as a named `fix(FishingRodController.cs): ...` commit, or report it as a defect; do not leave the mismatch silent.
- **Manual F3 check (add to this task's manual list and to the T30 checklist):** F3 toggles the overlay in the editor, and in a NON-development player build F3 and F4-F9 do nothing (`Debug.isDebugBuild` is false; no exception, no overlay).
- The overlay and key handlers go through `IDevInput` only; the host-only selectors (F6, F7) stay static `FishingDebugSettings` writes on the host.

### T26.1 Surface (C 7.26)
```csharp
internal sealed class DevOverlayPresenter : IHudExtension   // UI/Fishing/DevOverlayPresenter.cs
{ public void Bind(VisualElement root, ServiceRegistry app); public void Tick(float dt); public void Unbind(); }
```
- **Hook:** one line in `UiFlowController.BindScreens` after T25's: `if (Debug.isDebugBuild) AttachHudExtension(new DevOverlayPresenter());` (use `UnityEngine.Debug` if ambiguous). The presenter also returns early from `Bind` when `!Debug.isDebugBuild` (belt and braces).
- **UXML:** `Label name="DevOverlay" class="dev-overlay hidden" picking-mode="Ignore"` inside `ScreenHud`, not focusable.
- **USS:** `.dev-overlay` sits top-left (`left: 16px; top: 16px`), font-size 13px, `white-space: normal`, a dark translucent background (`rgba(10, 18, 28, 0.75)`), padding 8px, max-width 46%. Top-left is free until the M3 clock.
- **Keys:** `dev.host_only` "Host only", `dev.refused_cast` "Refused: cast active", `dev.no_rod` "No local rod", `dev.no_water` "No water under crosshair". Overlay field labels (`state=`, `tension=`) are diagnostic code literals, not UiStrings keys.

### T26.2 Behaviour
**Input.**
- Resolve `app.Get<IInputService>() as IDevInput` once in `Bind`; it may be null, in which case the keys do nothing.
- Poll `WasPressed(key)` in `Tick`. `IDevInput` already returns false outside dev builds, without window focus, and in the Menu context (C 7.10), so keys never fire in menus or Pause.
- Keys work whether the overlay is shown or hidden.

**Keys (each sets a one-line "last action" text shown on the overlay's last line for 4 s).**
- **F3 `ToggleOverlay`:** shows or hides `DevOverlay`. The state is a presenter field, hidden at first, and survives scene loads because the presenter lives in App.
- **F4 `NextRod`:** local rod `r` (the `FishingRodNetwork` on `FishingRodController.Local`'s GameObject; none means `dev.no_rod`).
  - If `Local.Machine.State != RodState.Idle`, show `dev.refused_cast` and send nothing.
  - Otherwise send `r.SelectRodRpc((ushort)((r.NetRodIndex.Value + 1) % registry.Rods.Count))`.
  - The host refuses while a cast is active (`FishingHostLedger.SelectRod`); the NetVar mirror is the truth, and the overlay shows the new rod when `NetRodIndex` changes.
- **F5 `NextBait`:** same as F4, with `SelectBaitRpc((byte)((NetSelectedBait.Value + 1) % registry.Baits.Count))`.
- **F6 `ToggleForcedBite`:** host only (`NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer`; do not test `FishingHost.Current.Ledger`, which is null before the first server tick). Toggle `FishingDebugSettings.ForcedBiteWait` between `0.1f` and `0f`. On a client: `dev.host_only`, no change.
- **F7 `ToggleTwitchy`:** host only. Toggle `FishingDebugSettings.TwitchyHook`. On a client: `dev.host_only`.
- **F9 `RippleAtCrosshair`:** local only.
  - Cast a ray from `Camera.main` through the viewport centre (`ViewportPointToRay(0.5, 0.5)`) and call `Physics.Raycast(ray, out hit, 100f, CastProbe.CastMask, QueryTriggerInteraction.Collide)`. It returns the closest hit, so the dock above the water blocks it, the same rule as casting.
  - If `hit.collider.gameObject.layer == Layers.Water` and `RippleField.Current != null` (explicit check: Unity object): `RippleField.Current.Spawn(hit.point.x, hit.point.z, RippleMath.Bite)`. Otherwise `dev.no_water`.
  - This is the spec 17.2 item 7 "ripple debug key".

**Overlay text.**
- Refresh at 10 Hz (accumulate `dt`) only while visible. Reuse one `StringBuilder`. Format with invariant culture, 2 decimals for 0..1 values and 1 decimal for seconds and metres.
- Lines, each skipped when its source is null:
  1. `ROD state=<RodState> t=<StateTime> castId=<CastId> assist=<on|off> recall=<Recalling>`, from `FishingRodController.Local.Machine`.
  2. `FIGHT phase=<Calm|Burst> dir=<BurstDir> tension=<..> stamina=<..> line=<LineLength>m lat=<Lateral> el=<Elapsed>s snap=<SnapTimer> pin=<PinnedTimer> slack=<SlackTimer> recov=<NearSnapRecoveries> max=<MaxTension>`, from `Machine.Fight.State`. For junk: `FIGHT junk line=<..> el=<..> slack=<..>`.
  3. `BITE timer=<BiteTimer>/<BiteWait>s nibbles=<Roll.NibbleCount> hookWindow=<Roll.HookWindow>` (when `HasResult`).
  4. `ROLL <item name> <WeightG> g <Quality> $<ValueCoins> t=<WeightT> size=<SizeClass> pity=<0|1> seed=0x<FightSeed:X8>` (when `HasResult`; this is the owner's own last roll, spec 9.11 "last roll").
  5. `LOADOUT rod=<RodAt(NetRodIndex).DisplayName> bait=<BaitAt(NetSelectedBait).DisplayName> x<count>`, where count 0xFFFF prints `inf`. Read from the local `FishingRodNetwork` NetVars, the host mirrors of the ledger (C 8.3).
  6. Host only: `HOST casts=<HostCastCounter> now=<Now>s catches=<CatchLog.Count> runSeed=0x<RunSeed:X16> forcedBite=<ForcedBiteWait> twitchy=<on|off>`, then one line per `FishingRodNetwork.Active` entry: `  slot <Player.Slot> client <OwnerClientId>: <Waiting|Hooked castId N | idle>` via `Ledger.TryGetActive(OwnerClientId, out cast)`. On a client the line reads `HOST n/a (client)`.
  7. `LAST <last action text>` (for 4 s after a key).
- `FishingRodNetwork.Spawned`/`Despawned` (C 8.1) may be used to keep the active list without polling. Polling `Active` at 10 Hz is also fine.
- **RunSeed** is displayed only. No key changes it (ruling G-Q5; the misleading `DefaultDevRunSeed` comment in C 5.2 is corrected).

**Unbind.** Unsubscribe everything and hide the label.

### T26.3 Steps
1. Pre-flight. Read T25's committed `FishingHudPresenter.cs` (same patterns), `Runtime/Input/IDevInput.cs`, `DevKey.cs`, `Fishing/Debug/FishingDebugSettings.cs`, `Fishing/Host/FishingHost.cs`, `Fishing/Host/CastProbe.cs`, and `Presentation/World/RippleField.cs`.
2. Edit the UXML, USS and UiStrings, write the presenter, and add the hook. Run `compile`.
3. Run `build`, the full `test-edit` and the full `test-play` (PlayMode's `FakeInputService` returns false for every `DevKey` unless a test sets one, so nothing fires).
4. Run the added-lines ASCII scan.
5. Commit, literally (add `Assets/_Project/UI/Fishing.meta` only if it is still new):
```powershell
git -C C:\Dev\FishGame add Assets/_Project/UI/Fishing/DevOverlayPresenter.cs Assets/_Project/UI/Fishing/DevOverlayPresenter.cs.meta Assets/_Project/UI/UiFlowController.cs Assets/_Project/UI/UiStrings.cs Assets/_Project/UI/Uxml/AppRoot.uxml Assets/_Project/UI/Uss/App.uss; git -C C:\Dev\FishGame commit -m "m2(dev): F3 dev overlay and F4-F9 debug keys" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T26.4 Acceptance
**Reviewer (Opus high):**
- The hook is guarded by `Debug.isDebugBuild`. No `#if`.
- Keys go only through `IDevInput`; there is no bare `Input.` or `Keyboard.current` in UI.
- F6 and F7 are host-only (the host statics are honoured only on the host anyway, C 7.17).
- F4 and F5 compute the next index modulo the registry count and pre-check Idle.
- The F9 raycast includes triggers and takes the first hit.
- The overlay is non-focusable and picking-ignored, refreshes at 10 Hz, and is quiet when hidden.
- No gameplay writes except the C 7.26 RPCs and debug statics.

**Controller manual checks:**
1. Press F3 in solo: the overlay appears. Every line updates through a full cast, bite and fight.
2. F4 and F5 in Idle cycle the rods and baits, and the LOADOUT line follows. While Waiting, the overlay shows "Refused: cast active".
3. F6: the next cast bites after about 0.1 s. F7: the bite shows "Click!" (T25).
4. In MPPM, a client pressing F6 sees "Host only" and nothing changes.
5. F9 aimed at open water: a ripple ring appears there. Aimed at the dock: "No water under crosshair".
6. The pause menu is open: no dev key fires.

---

## T28 - Lake dock rebuild

**Header**
- **Goal:** Rebuild `Spot_LakeDock` in the M2 look (spec 10.1-10.4, 17.2 item 7; C 6.4, 7.28): MeshBuilder props on `M_Toon_Base` with palette slots, the `FishGame/Water` grid, the gradient sky, fog and the two volumes. Every M1 collider, exit, spawn, the bell anchor and the lakebed and shore numbers stay unchanged. Report the scene stats for the 10.7 numbers.
- **Owned files (C 12):** `Assets/_Project/Editor/Builders/LakeDockBuilder.cs`, `LakeDockLayout.cs`, `LakeDockMeshes.cs`. Generated: `Assets/_Project/Scenes/Spot_LakeDock.unity`, `Assets/_Project/Content/Meshes/LakeDock_*.asset`.
- **Shared edits (C 2.6):** T28 owns the M2 rebuild of `LakeDockBuilder.cs`. It keeps T4's load-after-`NewScene` ordering and both hooks, `FishingSceneObjects.AddTo(scene)` (T19, T27) and `PresentationSceneObjects.AddTo(scene)` (T22). The **integration-fix rule** applies: T28 may fix defects in earlier M2 files when the dock exposes them (for example the water facet-normal sign, V13), one `fix(<file>)` commit each. The public surface stays frozen.
- **No `AssetPaths.cs` edit:** T28 is not in C 2.6's `AssetPaths` row. Keep new mesh paths and stream ids as constants in `LakeDockLayout.cs`. Keep using the existing `AssetPaths.LakebedMesh`, `ShoreMesh` and `MeshesFolder`, plus the T20/T21 constants for materials and volume profiles.
- **Depends on:** T22, T24, T27 (and therefore T20 `M_Toon_Base`, T21 `M_Water_Surface`, `M_Sky`, the volume profiles and `RenderingSetupBuilder.ApplySceneLighting`, T12 `MeshBuilder`, `FaceJitter`).
- **Implementer:** Sonnet. **Reviewer:** Opus high.
- **Tests:** none in C 10. The gate is `build` run twice, the full `test-edit` (the validators include `MeshReferences`, `MissingScripts` and `ToonMeshesHaveVertexColours`) and the full `test-play` (the G9 cast fixture and the movement and teleport tests must stay green), plus the collider fingerprint (T28.4) and the user look check.

**Carry-forward (2026-10-12, C 16.2; rulings F-Q12 and the T16 note):**
- **Integration-fix duty for waves.** T22 reports any M1 swim or water-height test (EditMode `WaterSurfaceTests` M1 case, PlayMode swim and water-timeout tests) that waves break. T28 FIXES it under the integration-fix rule (named `fix(<file>): <what>`, public surface frozen): either the test's expectation is made wave-aware (sample `WaterSurface.HeightAt(x, z)` instead of a literal 0) or the production check uses the wave-aware height; decide per failure and record the decision in the task report. The full `test-play` must be green before the commit.
- **Swimming interact gate playtest.** T16 gated `PlayerInteractor` on `InputContextRouter.InteractLive(context) && !MenuOpen`. During the look check, swim to the dock ladder and the bell, with the rod out and stowed, and confirm what can and cannot be interacted with; record the result in the task report (it feeds the T30 playtest notes).

### T28.1 What must not change (the physics fingerprint)
- The root names and order of M1 contract 6.5, and every child name below them: `Rock_NN`, `Tree_NN/Trunk`, `Tree_NN/Canopy`, `Deck`, `Post_L0..`, `Ladder_L/R`, `RampDeck`, `Exits/*`, `Spawn0..3`.
- Every collider's type, layer, `isTrigger`, enabled state, size, centre, radius, height and direction, and every collider-bearing GameObject's world transform.
- The lakebed and shore mesh **positions and triangles**. They may gain vertex colours and uv1; the MeshCollider uses positions only.
- The M1 seeded streams `lake_dock.rocks` and `lake_dock.trees`: the same draw count and order. **All new randomness uses new stream ids** (T28.2). Never add a draw to an M1 stream, or every later rock or tree moves.
- `WaterVolume` (the trigger box), `Exits`, `BellAnchor`, `PlayerSpawner`, `GameScope`, `Ambience`, and the `SceneCamera` position.
- **No new colliders**, except the volumes' required ones (a global `Volume` needs none). New props (planks, reeds, dock visual, water grid) are visual only. A collider on a reed or a water-grid mesh would break `CastProbe`'s "first hit is water" rule (C 7.17) and walking on the deck.

### T28.2 LakeDockLayout additions (T28 constants)
- Mesh asset paths under `AssetPaths.MeshesFolder`: `LakeDock_Water.asset`, `LakeDock_Dock.asset`, `LakeDock_Rock.asset`, `LakeDock_Trunk.asset`, `LakeDock_CanopyRound.asset`, `LakeDock_CanopyPine.asset`, `LakeDock_Reeds.asset`. The M1 `LakeDock_Lakebed.asset` and `LakeDock_Shore.asset` are kept.
- New streams: `lake_dock.planks`, `lake_dock.canopy`, `lake_dock.reeds`. Mesh seeds for `MeshBuilder`: fixed `uint` constants, one per mesh.
- Water grid: `WaterGridCells = 96`, `WaterCellSize = 1.25f` (96 x 1.25 = 120 m, matching the M1 visual plane; spec 10.4 asks for wavelengths of at least 4 x the cell, and T14 seeds at least 6 m).
- Palette slots (spec 10.2 order). If a Core or Data constant set for slot indices exists by T28 (search for `WoodDark`), use it. Otherwise keep private constants here with a comment citing spec 10.2: Grass 0, Foliage 1, FoliageDark 2, Wood 3, WoodDark 4, Sand 5, Rock 6, Reed 7, Dock 8, Roof 9, Accent 10.
- Reeds: 24 clusters of 4-6 blades, radius band 46.5-49.5 m (bed about -0.45 to -0.08 m), blade height 0.9-1.6 m above the bed. Clearance uses the existing `Blocked(x, z, 4f)` (dock line and ramp).

### T28.3 Build changes (behaviour)
**Order inside `Build()`:**
1. Build and save every mesh asset (in place; GUIDs kept).
2. `NewScene`.
3. Load every asset after `NewScene` (C 2.8 #14): materials via `RenderMaterialLibrary.Get` / `MaterialLibrary.Get`, meshes via `AssetDatabase.LoadAssetAtPath`, volume profiles, the content registry for the palette.
4. Build the roots in M1 order.
5. Run `RenderingSetupBuilder.ApplySceneLighting()` (RenderSettings are per scene).
6. Add the volumes.
7. Run both hooks.
8. Save the scene.

**Meshes (`LakeDockMeshes` plus a small save helper).**
- **Save in place.** Each built mesh is copied into the existing asset when one exists (`EditorUtility.CopySerialized(built, existing)`, or clear and refill), then the temporary mesh is destroyed. Never delete and recreate the asset: the GUIDs referenced by the scene must stay.
- **Lakebed and shore (`Fill`).**
  - Keep the vertex and triangle order and `RecalculateNormals`.
  - Add a per-face colour `0.5f * FaceJitter.Grey(meshSeed, faceIndex, 0.04f)` (HALF-scale grey, alpha 1, matching `MeshBuilder`; `M_Toon_Base` has `_JitterEncoded` 1 and the shader doubles it; ruling BF-7; the same value on all 3 vertices of a face).
  - Add uv1 with `x = slot`. Lakebed: Sand. Shore: Sand for faces whose centre y is below 0.3 m, Grass above.
  - Both then render with `M_Toon_Base` (palette mode) and pass `ToonMeshesHaveVertexColours` (C 2.8 #19).
- **Water.** `MeshBuilder.Grid(96, 96, 1.25f, "LakeDock_Water")`: shared vertices, centred, y 0 (C 7.12). No colours needed (Water shader).
- **Rock.** `MeshBuilder.Icosphere(Vector3.zero, 0.5f, 1, Rock)`. Unit radius 0.5 matches the Unity sphere primitive, so the M1 transform (size, 0.7 x size, size) still applies.
- **Trunk.** `MeshBuilder.Cylinder(Vector3.zero, 0.5f, 0.42f, 2f, 7, identity, WoodDark)` (the Unity cylinder primitive is radius 0.5, height 2, centred).
- **CanopyRound.** Icosphere radius 0.5, subdivisions 1, Foliage.
- **CanopyPine.** Two stacked cones inside the unit sphere: `Cylinder` with `radiusTop = 0`. The lower cone uses FoliageDark and the upper Foliage.
- **Dock (one combined world-space mesh, `DockVisual`).**
  - Deck planks across the deck: 40 planks of 3.0 (x) by 0.26 (z) by 0.1 m, gap 0.04 m, filling z -52..-40.
  - **Every plank top is exactly y 0.6** (the deck collider top). Visual and collider tops agree within 0.01 m, so feet never float or sink.
  - Per plank, one draw from `lake_dock.planks` picks slot Wood or Dock. A second draw gives a yaw of +-0.8 degrees, with no height jitter.
  - Two WoodDark stringers under the deck at x +-1.2.
  - Posts: 6-sided prisms of radius 0.08 at the M1 post positions, from the bed to the deck underside, WoodDark.
  - Ladders: two rails and rungs every 0.3 m inside the M1 ladder collider volume, WoodDark, ending at the deck top.
  - Ramp: planks along the `RampDeck` direction, with their top surface on the `RampDeck` collider top.
  - Keep each combined mesh under 21,000 triangles (16-bit index safety; assert in the builder). The dock is well under that.
- **Reeds (one combined mesh).** Clusters from `lake_dock.reeds`: per cluster, angle, radius, blade count, then per blade offset, height and lean. Each blade is a 3-sided Prism of radius 0.025 from the bed up, with Reed slot and a +-8 degree lean.
- **Determinism.** Fixed mesh seeds, fixed draw order, and the `MeshBuilder` default face jitter 0.04. Do not read `ArtPaletteDef.faceJitter` for meshes: an Inspector edit must not change the generated meshes. Two consecutive builds leave `Content/Meshes/LakeDock_*.asset` byte-identical (`MeshBuilder_Rebuild_IsByteIdentical` covers the builder; T28 covers its own use).

**Scene objects.**
- **WaterVisual.**
  - Same name, layer Water, at the origin, with MeshFilter (`LakeDock_Water`) and MeshRenderer (`M_Water_Surface`).
  - Shadow casting Off, receive shadows off. **No collider.**
  - Built as a plain GameObject, not `CreatePrimitive`, so there is no collider to remove.
- **Lakebed / Shore.** As M1, with material `M_Toon_Base`.
- **Rocks / Trees.**
  - Keep `BuilderUtil.EnsurePrimitive(..., keepCollider: true)` exactly as M1: the same draws, names, transforms and colliders.
  - Then replace `MeshFilter.sharedMesh` with `LakeDock_Rock`, `LakeDock_Trunk` or the canopy mesh, and set `sharedMaterial = M_Toon_Base`.
  - Canopy choice: one draw per tree from `lake_dock.canopy`, indexed by tree number: 60% pine, 40% round.
  - The colliders stay those of the primitives, so physics is identical.
- **Dock.**
  - Keep `Deck`, `Post_*`, `Ladder_*` and `RampDeck` with their M1 transforms and BoxColliders. Remove their MeshRenderer and MeshFilter (`Object.DestroyImmediate`) so they are collider-only.
  - Add the child `DockVisual` (layer Structure, MeshFilter `LakeDock_Dock`, MeshRenderer `M_Toon_Base`, no collider).
  - `SetLayerRecursive(dock, Structure)` stays.
- **Reeds.** A new root `Reeds`, layer Default, MeshFilter and MeshRenderer (`M_Toon_Base`), shadow casting Off, no collider. Place it after `Dock` in the root order, so the M1 roots keep their order.
- **Sun.** Rotation unchanged (50, -30, 0). Colour and intensity come from the registry palette's Day phase (`phases[1]`: `lightColor`, `lightIntensity`) when present; otherwise M1 defaults. The registry loads after `NewScene`.
- **SceneCamera.** As M1, plus `UniversalAdditionalCameraData.renderPostProcessing = true` (C 7.21 does this for PlayerFP; the scene camera matches).
- **WarmVolume / CoolVolume.** New roots, each with a `Volume`: `isGlobal = true`, `weight = 0`, `priority = 0`, and `sharedProfile` set to `VP_WarmSunset` or `VP_CoolNight` (C 6.2, 6.4).
- **Lighting.** Call `RenderingSetupBuilder.ApplySceneLighting()`: skybox `M_Sky`, ExponentialSquared fog at density 0.008, ambient mode. Never toggle fog at runtime (spec 10.3).
- **Hooks.** `FishingSceneObjects.AddTo(scene)` and `PresentationSceneObjects.AddTo(scene)` stay exactly as T19 and T22 left them.
- **Stats line.** At the end of `Build()`, one `Debug.Log` line: `FG LAKEDOCK renderers=<n> triangles=<n> meshes=<n> materials=<n>` (counts over the scene's MeshRenderers). It feeds the 10.7 record in T30. Info level, never a warning.

### T28.4 Steps
1. **Pre-flight (G0.4).** Read `LakeDockBuilder.cs` (as T4, T19 and T22 left it), `BuilderUtil.cs` (`EnsurePrimitive` keeps the collider only when its last argument is true), `Runtime/World/MeshBuilder.cs`, `Editor/Builders/RenderMaterialLibrary.cs`, `Editor/Builders/Render/RenderingSetupBuilder.cs`, and `AssetPaths.cs` (the T20 and T21 blocks).
2. **Baseline fingerprint (before any edit).**
   - Run `build`.
   - Create a **temporary** EditMode test `Assets/_Project/Tests/EditMode/TempLakeDockFingerprint.cs`. It is never committed: delete it and its `.meta` in step 7, and the reviewer checks it is absent.
   - The test opens `Spot_LakeDock.unity` with `EditorSceneManager.OpenScene` and writes `$env:FG_OUT\t28\fingerprint-<label>.txt`, where the label comes from an environment variable.
   - Each line holds one Collider, sorted by hierarchy path. Use the collider's own serialized fields, not `Collider.bounds`, which can be zero or stale in an EditMode test right after `OpenScene`. Record:
     - the path, collider type, layer, `isTrigger` and `enabled`;
     - the owning Transform's `localToWorldMatrix` (16 values);
     - BoxCollider `size` and `center`; SphereCollider `radius` and `center`; CapsuleCollider `radius`, `height`, `direction` and `center`; MeshCollider mesh name and vertex count.
     - All numbers to 4 decimals, invariant culture.
   - Add one line per Transform under `PlayerSpawner`, `Exits` and `BellAnchor` (path and world position).
   - Run it with `test-edit -Filter TempLakeDockFingerprint` and label `before`.
   - **Sanity check on `before`:** it must list every M1 collider. That is the deck, 14 posts, 2 ladders, the ramp deck, 14 rocks, 30 trunks, 30 canopies, the lakebed, the shore, `WaterVolume`, 3 exits, plus any colliders the T19/T22 roots add. Every size and radius must be non-zero. If not, the fingerprint is broken: fix the test before going on.
3. **Implement** T28.2 and T28.3, then run `compile`.
4. **Run `build` twice.**
   - After the first run, record `Get-FileHash` of every `Assets/_Project/Content/Meshes/LakeDock_*.asset` into `$env:FG_OUT\t28\hash-1.txt`.
   - Run `build` again and record `hash-2.txt`. The two lists must be identical (byte-identical rebuild).
   - Check the log for the `FG LAKEDOCK` line.
5. **Fingerprint after.**
   - Run the temp test with label `after`, and `Compare-Object` before and after. They must be identical.
   - Any difference (a moved collider, a new collider, a renamed path) is a defect: fix it and rerun.
6. **Full `test-edit` and `test-play`.** All green: `Validation_ToonMeshesHaveVertexColours_Pass`, `Validation_MeshReferences_Pass`, `Validation_MissingScripts_Pass`, `SoloHost_CastBiteFight_Lands`, `RodFighting_MoveInput_DoesNotMovePlayer`, and the M1 teleport and movement tests.
7. **Clean up.** Delete `TempLakeDockFingerprint.cs` and its `.meta`. `git status` must not list them.
8. **Commit.**
   - Paths: the three builder files, `Assets/_Project/Scenes/Spot_LakeDock.unity` (a real change this time), each `Assets/_Project/Content/Meshes/LakeDock_*.asset` and its `.meta` (new ones included), and any integration fixes in their own commits first.
   - Literal command (new `LakeDock_*` assets and their `.meta` are picked up by the wildcard):
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Editor/Builders/LakeDockBuilder.cs Assets/_Project/Editor/Builders/LakeDockLayout.cs Assets/_Project/Editor/Builders/LakeDockMeshes.cs Assets/_Project/Scenes/Spot_LakeDock.unity "Assets/_Project/Content/Meshes/LakeDock_*"; git -C C:\Dev\FishGame commit -m "m2(lakedock): rebuild lake dock with MeshBuilder props, water grid, sky, fog and volumes" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
9. **Report:**
   - the `FG LAKEDOCK` stats line;
   - the fingerprint result;
   - the triangle count per mesh;
   - any V13 observation (water facets lit on the sun side: the sign is right).

### T28.5 Acceptance
**Reviewer (Opus high):**
- Only the three owned builder files (plus named `fix(...)` commits) changed in code.
- Both hooks and the load-after-`NewScene` order are kept.
- New randomness is on new streams only, and the M1 rock and tree loops have unchanged draws.
- The deck, posts, ladders and ramp are collider-only, with the same transforms.
- The water grid, reeds and dock visual have no colliders.
- Mesh assets are saved in place, with GUIDs kept.
- Every Toon renderer's mesh has colours.
- The fingerprint before and after is identical (pasted in the report), and two builds leave the meshes byte-identical.
- No temp test file is left in the commit.

**User look check** (controller asks; record the result):
- Open `Spot_LakeDock.unity` in the editor, then play Solo.
- **Expected:**
  - Flat-shaded props in palette colours, with no pink (missing-shader) or black (missing-globals) surfaces.
  - The water shows waves, depth fade near the shore and posts, and foam at the edges.
  - The sky has a gradient. Fog softens the far trees.
  - Reeds stand in the shallows. Walking the deck and ramp feels as in M1.
- If the water looks wrong (inverted facet lighting, no depth fade), check the URP depth texture and the facet-normal sign (V13). T28 may fix it under the integration-fix rule, or report it.

**Perf (spec 10.7):**
- Frame time, SRP batch count and the water pass GPU time need a development build and the Profiler. They are **user measurements** recorded in the T30 acceptance doc.
- T28 supplies the scene stats only.
- The M1 baseline in `2026-10-09-m1-acceptance.md` may still be unrecorded. In that case the M2 numbers become the first perf baseline (ruling G-Q8) and T30 notes it.

---

## T29 - Species models: import and verify the staged Blender fish (controller checklist; not a subagent brief)

**Header**
- **Who:** the controller. No subagent and no worktree. Blender MCP is needed only if a staged model fails verification and must be re-exported.
- **State at plan time:** the five species models already exist in the repo, committed in `8f327e5`: `Art/Export/Fish_{Bluegill,Perch,Carp,Trout,Catfish}.fbx`, source `Art/Blender/fishgame_m2_species.blend`. Each has ONE submesh and one material slot, per-face LINEAR vertex colours (ruling F-Q3: the controller re-exports the staged fish linear), 222-262 triangles, exported -Z forward / Y up / apply transform (nose +Z in Unity). The new perch REPLACES the spike perch `Content/Models/Fish_Perch.fbx` (5 submeshes, no vertex colours).
- **Goal (C 7.29, R62):** copy the five FBX into `Assets/_Project/Content/Models/` (perch overwrites the spike file and keeps its `.meta`), let `FishModelPostprocessor` import them, verify the properties above on the imported meshes, wire the species `model` refs through `build`, and get the user's look approval. T29 imports and verifies; it does not model from scratch.
- **Owned (C 12):** `Assets/_Project/Content/Models/Fish_{Bluegill,Perch,Carp,Trout,Catfish}.fbx` and their `.meta`, and the regenerated species assets (`model` refs only).
- **Depends on:** T20 (`FishModelPostprocessor`, `M_Toon_Fish`, `RenderValidators`), T14 (`Validation_SpeciesModels_Pass` incl. the one-submesh check), T23 (`FishFallbackMesh`, used for the colour comparison), T28 (committed or in review; skeleton section 5).
- **Review:** Sonnet (import settings, LFS, validators, asset diffs), then the **user look check**. The commit trailer names the controller's model.

**Carry-forward (2026-10-12, C 16.2; the T14-fix minors, to be done in the same small commit as the model fill because this task is the first to exercise it):**
1. `FishingContentBuilder.Rooted()` throws (`ArgumentException`/`InvalidOperationException`) when a path is not under `RealContentRoot` instead of silently mangling it with `Substring`; add a test.
2. A test covers the `ModelsFolder` re-root under `BuildAt` (assert the computed path, or use a temp FBX under `Tests/__Temp/`).
3. Rename the `FishingContentBuilder_Build_*` tests to `FishingContentBuilder_BuildAt_*` (`FishingContentBuilder_Build_NeverReconfiguresExistingAssets`, `FishingContentBuilder_Build_AppendsMissingRegistryEntriesOnce`), since they call `BuildAt`; the contract C 10 names are unchanged.
4. `FishingContentBuilderTests` `[OneTimeSetUp]`: wrap the temp build in try/catch and call the delete-temp-folders helper on failure, so a failed build never leaves `Tests/__Temp/` behind.
5. Move the `ValidationTests` temp prefab under `Assets/_Project/Tests/__Temp/` (git-ignored, deleted in TearDown).
6. The models are IMPORTED from the staged `Art/Export/` files; they are not rebuilt. Only the controller touches Blender, and only if a staged model fails verification.

### A. Inputs
1. The five staged FBX files and the source `.blend` (committed; LFS, `.gitattributes` tracks `*.fbx`).
2. Species colours: `colorA`, `colorB`, `bodyShape` from `Assets/_Project/Content/Fishing/Species/<id>.asset` (the runtime truth), to compare with the staged vertex colours in step D.
3. Nominal model length = `CatchMath.LengthMm` at the mid weight (bluegill 317 mm, perch 363, carp 543, trout 463, catfish 660). The reveal rescales to the rolled `LengthMm` anyway (C 7.23), so this only sanity-checks proportions.

### B. Pre-import checks on the staged files (no Unity needed)
1. Each file in `Art/Export/` is the real FBX, not an LFS pointer (size above 1 KB; first bytes not `version https://git-lfs`). If a pointer: `git lfs pull --include "Art/Export/*.fbx"`.
2. `git -C C:\Dev\FishGame check-attr filter -- Art/Export/Fish_Carp.fbx` prints `filter: lfs` (repeat per file).
3. Optional, in Blender (only if something looks wrong): import one file and confirm one material slot, one colour attribute, triangle count below 400 and the nose along the axis that gives +Z after the Unity import.

### C. Copy and import
1. The editor must not be open: run the first `unity.ps1` command and stop only if it reports LOCKED.
2. Copy `Art/Export/Fish_Bluegill.fbx`, `Fish_Carp.fbx`, `Fish_Trout.fbx`, `Fish_Catfish.fbx` to `Assets/_Project/Content/Models/`, and copy `Fish_Perch.fbx` over the existing spike file. Never delete `Fish_Perch.fbx.meta` (the GUID and fileIDs keep any reference valid).
3. Run `build`. `FishModelPostprocessor` applies the import settings and logs `FG MODELS <file> tris=<n> submeshes=<k> colours=<yes|no>` for each model. Expected: tris 222-262, `submeshes=1`, `colours=yes` for all five.
4. `FishingContentBuilder` fills each null species `model` from `Content/Models/Fish_<Pascal>.fbx` (C 6.2 rule: only when the mesh has a colour channel and one submesh). The perch's `model` is filled now too (T14 left it null).
5. Check each `.meta`: `materialImportMode: 0`, `importCameras: 0`, `importLights: 0`, `globalScale: 1`, `useFileUnits: 1`, `isReadable: 0`, `meshCompression: 0`, `importAnimation: 0` (C 7.20).
6. `git diff Assets/_Project/Content/Fishing/Species` shows only `model:` lines changing (null to a fileID and guid).
7. Run the full `test-edit` (`Validation_SpeciesModels_Pass`, including the one-submesh check, and `Validation_ToonMeshesHaveVertexColours_Pass` must pass) and the full `test-play`.

### D. Visual verification (batchmode render, then the user)
1. **Use an existing tool first.** If T20 or T21 left a committed capture helper, use it. Search `Assets/_Project/Editor` for `allowAsyncCompilation`.
2. **Otherwise write a temporary editor script**, never committed: `Assets/_Project/Editor/Dev/TempFishLineup.cs`, namespace `FishGame.EditorTools`, `public static void Render()`. It must:
   - set `ShaderUtil.allowAsyncCompilation = false` (restore it in `finally`, C 2.8 #13);
   - open a new empty scene;
   - push the Toon globals the way `PaletteGlobals.Apply` does (add the component and call `Apply`, or set `_FGPalette`, `_ToonShade` and `_ToonAmbient` from the registry palette's Day phase);
   - add a directional light at (50, -30, 0);
   - add an orthographic camera with a side view, and a second pass with a top view;
   - for each species, place the model with `M_Toon_Fish` in a row, with the same species' `FishFallbackMesh.Get` mesh (rendered with `M_Toon_FishFallback`) under it for colour comparison, and a small marker cube at +Z of each fish (it shows the nose direction);
   - log `subMeshCount`, `HasVertexAttribute(VertexAttribute.Color)`, the triangle count and the bounds size per model;
   - render to a `RenderTexture`: `camera.Render()`, or `RenderPipeline.SubmitRenderRequest` with a `RenderPipeline.StandardRequest` if URP ignores `Render()` in batchmode;
   - read the pixels and write `$env:FG_OUT\t29\lineup_side.png` and `lineup_top.png`;
   - end with `EditorApplication.Exit(0)` (1 on an exception).
3. **Run it with a graphics device** (no `-nographics`):
   `& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -projectPath C:/Dev/FishGame -executeMethod FishGame.EditorTools.TempFishLineup.Render -logFile "$env:FG_OUT\t29\render.log"`
   Then grep the log for `error CS`, `Exception` and the per-model lines.
4. **Read both PNGs** with the Read tool. Check:
   - the nose is on the marker side for every fish;
   - the facets are visible (flat);
   - the colours match the fallback row (both use LINEAR vertex colours; this render only confirms the one rule, F-Q3, it does not decide it);
   - nothing is pink or black;
   - the relative sizes are plausible.
   A defect means re-export from `Art/Blender/fishgame_m2_species.blend` with Blender MCP (`axis_forward='-Z'`, `axis_up='Y'`, `bake_space_transform=True`, `mesh_smooth_type='FACE'`, `colors_type='LINEAR'`, no materials, selection only), copy over the file, and repeat from C3.
5. **Delete** `TempFishLineup.cs` and its `.meta`. `git status` must not list them.
6. **User look check.** Show the user the two PNGs. Then ask them to play Solo, rotate baits with F5 (raw meat reaches every species; cast long for catfish, depth 2.5 m or more), and land each species with F6 forced bite. The reveal must show the model at the right size and colours. The user decides; iterate until they approve.

### E. LFS check and commit
1. `git -C C:\Dev\FishGame check-attr filter -- Assets/_Project/Content/Models/Fish_Carp.fbx` prints `filter: lfs`. Repeat for each FBX.
2. Stage explicit paths, literally:
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Content/Models/Fish_Bluegill.fbx Assets/_Project/Content/Models/Fish_Bluegill.fbx.meta Assets/_Project/Content/Models/Fish_Perch.fbx Assets/_Project/Content/Models/Fish_Perch.fbx.meta Assets/_Project/Content/Models/Fish_Carp.fbx Assets/_Project/Content/Models/Fish_Carp.fbx.meta Assets/_Project/Content/Models/Fish_Trout.fbx Assets/_Project/Content/Models/Fish_Trout.fbx.meta Assets/_Project/Content/Models/Fish_Catfish.fbx Assets/_Project/Content/Models/Fish_Catfish.fbx.meta Assets/_Project/Content/Fishing/Species/bluegill.asset Assets/_Project/Content/Fishing/Species/perch.asset Assets/_Project/Content/Fishing/Species/carp.asset Assets/_Project/Content/Fishing/Species/trout.asset Assets/_Project/Content/Fishing/Species/catfish.asset
git -C C:\Dev\FishGame commit -m "m2(models): import staged Blender species models (bluegill, perch, carp, trout, catfish)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
   (the trailer names the controller's model, C 2.1).
3. `git -C C:\Dev\FishGame lfs ls-files` lists all five `Fish_*.fbx` under `Content/Models/` (and the five under `Art/Export/`).
4. `git -C C:\Dev\FishGame show :Assets/_Project/Content/Models/Fish_Carp.fbx` starts with `version https://git-lfs.github.com/spec/v1` (the index holds a pointer, not the binary). Repeat for each FBX.
5. **Report** for the Sonnet reviewer:
   - triangle counts and submesh counts (from the `FG MODELS` lines);
   - the meta checks;
   - the validator results;
   - the PNG paths;
   - the user's verdict.

---

## T30 - Acceptance, README, final check, branch-review hand-off

**Header**
- **Goal (C 7.30):**
  - Write `docs/superpowers/plans/2026-10-11-m2-acceptance.md`, with every 17.2 criterion (1-11), each with an owner and a how-to-check, the 17.2 manual MPPM list, the 9.13 edge-case walk (criterion 5), the fun-gate log template with a stats recipe and a blind tell-apart sheet, the perf record (10.7), open tuning items, user actions and the definition of done.
  - Update `README.md`.
  - Run the final `check`.
  - Hand off to the Opus xhigh branch review.
- **Owned files (C 12):** `docs/superpowers/plans/2026-10-11-m2-acceptance.md` (new), `README.md`.
- **Integration-fix rule (C 2.6)** applies to the final `check`.
- **Depends on:** all of T1-T29 committed. Inputs:
  - T15's bot report (printed distributions and tuning items);
  - T28's `FG LAKEDOCK` line;
  - T29's report;
  - skeleton section 8 (open risks) and their status from the task reports.
- **Implementer:** Sonnet. **Reviewer:** Sonnet (docs), then **Opus xhigh** on the whole branch (T30.6).
- **Tests:** none new. The gate is the final `check`.

### T30.1 Acceptance doc: required structure
Follow the M1 doc's style (`2026-10-09-m1-acceptance.md`): checkbox per item, Steps / Expected / Record / `Result: ______`, plain ASCII. Sections in order:

**Header block.** Date, spec 17.2 reference, contract reference. Who runs it: the user (manual) and Claude (automated rows). How to record. Setup help: README "Local multiplayer testing", "Fishing dev keys", "Fight log".

**Before you start.**
- All M2 tasks are committed and `check` prints `FG RESULT check PASS`.
- `FishGame > Build All` has run.
- Auto Solo is off for the MPPM items.
- The `Direct4P` scenario exists, and the `NetSim_150ms_2pct` preset exists (README).
- A gamepad is connected for criterion 1 (skeleton risk 8).
- The M1 checklist's multiplayer criteria (3-9, 12) are run or scheduled: they were still open on 2026-10-11 (handoff).

**A. Criteria table, then one block per criterion.** Use this content (owner, how to check):

| # | Criterion (17.2) | Owner | How to check |
|---|---|---|---|
| 1 | Solo: Idle to landed fish, no errors, only the 8.3 bindings, keyboard/mouse **and** gamepad | User | Solo with Auto Solo on and the Console cleared. Keyboard/mouse: 1 (equip), hold RMB, hold and release LMB, wait, S/A/D during the fight, land. Then the same on gamepad only (North, LT, RT, left stick). Expected: the fish lands, the creel goes +1, the toast and reveal show, the rod returns to Idle, and the Console shows 0 errors. Record both devices. |
| 2 | Every 9.2 transition has a passing EditMode test | Claude (T30) | `test-edit -Filter RodStateMachine`: 45 test methods (38 row tests from C 10.2 + 6 rule tests + the extra `RodStateMachine_Assist_CapturedAtRelease`; BF-8). `RodStateMachine_IllegalEvents_IgnoredInEveryState` is a TestCaseSource over 11 states, so NUnit reports **55 results** (38 + 5 + 11 + 1), all passing. Paste the count line. |
| 3 | Co-op: cues on both peers; reveal for everyone after the host commits | User | MPPM 2-4 instances: the MPPM list items MP1 and MP4 below. |
| 4 | Tension and phase readable with no HUD and with sound off | User | Gauge Off, captions off, master volume 0. Fight 3 fish (one Bulldog). Name aloud: side run vs dive (splash column vs plunge and line down), creak band (rod bend), warn (shake plus the line dash and width pulse). A second person watches the screen and confirms. Then repeat with sound on and eyes on the rod only. |
| 5 | Rod back to Idle within 3 s in every 9.13 case; no stuck states; no rumble left | User + Claude | Section C below (the 9.13 walk). Gamepad connected, so rumble is observable. |
| 6 | Bait consumed on accept, refunded on cancel/recall, lost on snap/escape/miss; bare hook never runs out | Claude (tests) + User | EditMode `FishingHostLedger_Accept_ConsumesBaitAndRolls`, `FishingHostLedger_CancelInWaiting_RefundsBait`, `FishingHostLedger_SnapEscapeMiss_DoNotRefund`, `FishingHostLedger_BareHook_NeverRunsOut`; PlayMode `SoloHost_CancelInWaiting_RefundsBait`. Manual: F3 LOADOUT count 20 -> 19 on a cast, back to 20 after RMB cancel; a miss (F7 twitchy, don't click) keeps 19; bare hook shows `inf`. |
| 7 | Host rejects forged or out-of-order casts and hooks and impossibly fast catches; species and weight come from the host roll | Claude (tests) + Opus xhigh review | EditMode ledger tests (C 10.1 T9 rows: stale/duplicate id, charge range, origin > 3 m, no bait, early hook, landed below floor, implausible or self-contradictory clean claims). The branch review confirms that no owner-side path decides species, weight or quality (the owner only displays `CastResultRpc`'s roll; the creel and announce use `CommitDecision.Roll`). **Wording (2026-10-12, C 16.2):** the criterion is "the host rejects forged and out-of-order messages (stale or duplicate cast ids, non-finite or far origins and yaw, no bait, early hooks, landings faster than the floor, implausible or self-contradictory clean claims) and bounds the line start by what a player could have walked (T9-R1)"; position checks are bounded by the OWNER-WRITTEN transform (the player moves under owner authority), which is accepted for a friends-only game (spec 1.2); a bare-hook reroll by cancelling is accepted design. It does NOT claim protection against a skilled cheater who forges a plausible whole fight (spec 9.9: the host never replays the fight). |
| 8 | Bots meet the 9.12 targets (a failure passes only with numbers recorded as a tuning item) | Claude (T30) | `test-edit -Filter "FishGame.Tests.EditMode.FishingBots"`. Paste the printed tables into section F. Each failing target is listed under "Open tuning items" with its numbers. |
| 9 | All tuning lives in assets; Core holds none except test defaults | Claude + User | Claude: the branch review's grep for 9.12 default literals in `Core/` outside `FishingConstants` (formula constants) and tests. User: in Play mode, change `Content/Fishing/FishingTuning.asset` `castChargeTime` 1.0 -> 0.6 in the Inspector; the ring fills faster on the next charge. Revert, or keep only if wanted (create-if-missing keeps Inspector edits). |
| 10 | Assist, line gauge, captions and Camera motion behave as 9.11 defines | User | Assist on: grace feels longer (snap later), and the gauge goes to Arc each time assist is turned on while the gauge is Off (T25 rule, ruling G-Q4). Gauge Arc/Bar/Off. Captions on/off ("Bite!", "Line straining", "Snapped", "Got away"). Camera motion 0: no shake, no FOV kicks, no rumble. |
| 11 | `FightSim` costs < 0.05 ms per step | Claude + User | Record the **printed** per-step time from `FishingBots_FightSimStep_UnderFiftyMicroseconds`. That test fails only above 0.2 ms, so its pass is not the criterion. The number must be < 0.05 ms. User option: Profiler deep profile of a fight in a dev build. |

**B. Manual MPPM list (17.2).** Host plus 1-3 clients, dev-direct, Network Simulator 150 ms RTT and 2% loss on the clients (README section 5). Items:
- MP1: a client's cast lands, and the bite and fight cues play on both peers.
- MP2: remote rod bend, line and bobber trail the owner by no more than ~250 ms (side-by-side instances; slow-motion screen capture optional).
- MP3: the cast arc appears once on remotes (no replay; a late joiner sees no arc).
- MP4: the catch reveal shows on all screens after the host commits, plus the toast (crew toasts at most 1 per 3 s per catcher).
- MP5: a client disconnecting mid-fight (Leave, then a process kill) leaves no outstanding hook on the host (F3 HOST lines show no active cast for that client), and its rod vanishes within 1 s.
- MP6: a host alt-tab (unfocused for 10 s) doesn't stall the clients.
- MP7: late join mid-fight. Client B joins while client A is Hooked. B sees A's rod bent, the line and the bobber at the fish point, with no species until the reveal.
- MP8: the owner feels no latency at 150 ms RTT (definition of done). Charge, cast and reel respond on the frame.
- MP9: online over Sessions (join code), once linked. Repeat MP1 and MP4.

**C. 9.13 edge-case walk (criterion 5).** For every row: Steps, Expected (always "Idle within 3 s, no stuck state, rumble 0" plus the row's own expectation), Result. The rows and their recipes:

| 9.13 case | How to reproduce | Row-specific expectation |
|---|---|---|
| Invalid spot under the marker | Aim at the dock or the shore, charge, release | Thunk, marker flash, stay Ready, bait count unchanged |
| Owner/host disagree on validity | Hard to force by hand. Client under the simulator, strafing along the shore edge while releasing at a marginal spot. Covered by `RodStateMachine_Casting_InvalidResult_RecallsToIdle` / `_Waiting_InvalidResult_RecallsToIdle` | Recall, no bait used |
| Walking away while Waiting | Cast about 15 m, walk back along the dock to the shore until past `maxLineOut` (Handmade 18 + 12 = 30 m) | Auto-reel, bait refunded (F3 count) |
| Fish over land or dock | During a fight, let a side run swing the fish point toward the dock | The bobber holds the last water point; the fight is unaffected |
| Fish takes all the line | Twig rod (F4), raw meat (F5), cast long, hook a big fish and never reel | Spooled: the line clamps and tension rises (`spoolStrain`); tiring the fish first recovers it |
| Alt-tab mid-fight | Alt-tab for 5 s while Hooked, with a gamepad rumbling | Neutral input, tension relaxes, the session stays alive, rumble stops on focus loss |
| Owner hangs but stays connected | Client started with `-dev-long-timeout` (otherwise the 10 s disconnect fires first and tests MP5 instead). Cast, then press the client editor's Pause during Waiting for `BiteWait` + 30 s (check F3 on the host) | The host refunds (stuck-Waiting rule). Unpaused, the client plays the recall beat and is Idle within 3 s. The Hooked path (240 s) is covered by `FishingHostLedger_HookedPastMaxFightTime_TimesOutEscapedTimeout` |
| No `CommitRpc` | Pause the host editor (under 10 s, or the client has `-dev-long-timeout`) right as the client lands a fish | The client is back in Idle 3 s after the terminal state, with one warning in its Console. Also covered by `RodStateMachine_Terminal_NoCommit_ReturnsIdleAfterCommitTimeout` |
| Host disconnects | The host chooses Leave mid-fight | Clients return to the menu with "Host ended the session.", no exception, rumble 0 |
| Late joiner sees a rod mid-fight | Same as MPPM MP7 | Rod state, bobber and tension shown; species hidden |
| Two bobbers overlap | Two players cast to the same spot | Not handled (M5 tangle); record only that nothing breaks |
| Day end / Night Camp / Resume (M4 rows) | - | N/A in M2: mark them `N/A (M4)` |

**D. Fun gate (user plus at least one friend, at least 30 logged fights).**
- **Setup.**
  - Telemetry runs only when `Debug.isDebugBuild`. The friend plays a **development build** (`File > Build Profiles`, Development Build) over a join code, or dev-direct on a LAN.
  - Each player's log holds only their own casts: `Application.persistentDataPath/fight-log{Suffix}.csv` (C 5.3). On Windows that is `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\`; on 2026-10-11 `DefaultCompany\My project`, so check `ProjectSettings` if it changed.
  - MPPM clones and `-dev-profile` players write `fight-log_<name>.csv`.
  - The friend sends their CSV. Collect every `fight-log*.csv` into one scratch folder.
- **Coverage.** Worm and grub reach only bluegill, perch and carp (rating vs `minBaitRating`). Trout needs shrimp or raw meat. Catfish needs raw meat and a landing at least 2.5 m deep. Testers rotate baits with F5, cast long, and aim for **at least 5 hooked fights per profile**. Leave F6/F7 **off** during gate sessions (forced bites distort cast-to-bite).
- **Columns** (C 5.3): `profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity`.
- **Stats recipe** (the doc spells out a PowerShell 5.1 snippet; the T30 implementer writes it and tests it on a hand-made 6-row CSV in the scratchpad):
  1. Import every `fight-log*.csv` (`Import-Csv`). Parse numbers with `CultureInfo.InvariantCulture`.
  2. Read `outcome` and `reason` as enum names. If they are digits, map them through the C 7.5 enum order. Read `assist` and `clean` as `true`/`1` (case-insensitive).
  3. Define the row sets:
     - all rows = casts that reached Waiting;
     - bite rows = `castToBite > 0`;
     - hooked = outcome Landed or Snapped, or outcome Escaped with reason not Missed or Invalid;
     - landed = outcome Landed;
     - fish rows = profile not empty and not junk.
  4. Print:
     - the total casts, hooked fights (gate needs at least 30) and hooked fights per profile;
     - median and p90 of `castToBite` over bite rows (targets: median 6-12 s, p90 at most 20 s);
     - the cancel-or-timeout rate, (outcome Cancelled or reason Timeout) / all rows (target under 10%);
     - per profile, the median `fightSec` over landed fish rows (targets: Steady/Darter 15-30 s, Bulldog/Thrasher 35-55 s, Runner 60-110 s), plus the hooked median for information;
     - the overall median of `castToBite + fightSec` over landed fish rows (target 35-80 s);
     - the snap rate, Snapped / hooked (target 10-30%);
     - per Bulldog, Thrasher and Runner, the share of hooked fights with `nearSnapRecoveries >= 1` (target at least 40%);
     - the clean-catch rate, clean / landed fish, for information (R64);
     - the assist row count;
     - the pity row count and, for information, the landed share of pity casts versus the rest (R65).
  5. Mark each target PASS/FAIL. A failed gate means **tuning data, not new mechanics** (17.2): retune the assets plus the matching `FishingSeedData` seeds (C 2.6 data row).
  6. **Near-snap recovery >= 40% is a HUMAN fun-gate metric (2026-10-12, C 16.2).** The T15 bots only print it (Rhythm bot at `5a652bd`: bulldog 3%, thrasher 6%, runner 57.5%; bots do not model a human reacting to the warn cue), so the T30 acceptance row for it is read from the playtest CSVs, never from `FishingBots`. Tuning hints if the Bulldog or Thrasher share is low: `tuning.relaxWhileReeling` sits at 0.20, the TOP of the 9.12 range (no headroom upward). The ruling's hints are: lower `relaxWhileReeling` toward 0.15, or raise the Thrasher `pullStrength`; and, because relax is at its ceiling, prefer raising the Bulldog/Thrasher `pullStrength` or lowering their calm minimum over relax. Record the per-profile shares and the chosen change.
  7. **Duration notes for the playtest:** the Twig-rod Thrasher trophy (weightT 0.9) has a bot median of about 93 s, and the Bulldog Rhythm median is 37.4 s against the 35 s floor. Watch both in real fights; if testers find the 93 s fight tedious, retune in data (assets plus seeds plus `TunedDeviations`; `thrasher.staminaDrain` is 0.035 after T15) and let the controller pick the lever.
- **Blind tell-apart sheet.**
  - Before each reveal or loss toast, the tester writes their guess: Steady, Darter, Bulldog, Thrasher or Runner.
  - Columns: fight #, tester, guess, confidence (1-3), actual (from the CSV `profile`, matched by order among that tester's hooked rows), match.
  - Proposed bar (the user confirms): 70% or more correct overall and every profile at least 50%.
- **Play-style question.** Do the three play styles (greedy, counter-reel, rhythm) feel different? Free text.
- **Ten-minute check.** After 10 minutes, does each tester want to keep casting? Was conversation comfortable while waiting? Free text.

**E. Perf record (10.7).**
- Use the M1 doc's perf steps (dev build, 4 bodies, 1080p).
- Record the frame time (average and worst), the SRP batch count, Batches and SetPass, and the **water pass GPU time** (Profiler GPU module, "Render Transparents" or the Water draw in the Frame Debugger).
- Paste T28's `FG LAKEDOCK` stats line.
- Compare with the M1 baseline. If it was never recorded, write "No M1 baseline; these are the first numbers" (ruling G-Q8).
- A regression above 10% gets a note.
- Also verify the V13 items here: fog variants survive in a player build (fog is visible in the dev build), the water facet lighting sign, and C#/HLSL wave parity (the bobber sits on the visible wave).

**F. Open tuning items.**
- T15's failing or marginal targets with numbers.
- The skeleton section 8 risks and their status: the T29 import of the staged species models (the new single-submesh perch), V11 `DeferLocal` (T19 test result), the clean-claim 2.0 s slack at 150 ms RTT (MPPM MP-run note), motion seeds, NetVar padding, behaviour indices, `-nographics` shader validation (T20 result), and gamepad.

**G. Definition of done (17.2).**
- Acceptance criteria 1-11 and the fun gate are met.
- Solo works offline (network adapter disabled, as in M1 criterion 2).
- 2-4 players fish together over dev-direct (and over Sessions once linked).
- The owner feels no latency at 150 ms RTT.

**H. Bad-luck nudge (R65 / M2-D23) - USER.**
- Steps: in the Inspector set `Content/Fishing/FishingTuning.asset` `pityAfterFailures` to 3 (the seed). Fail three casts in a row (miss with F7 twitchy and no click, or let a fish snap the line). The next cast shows "<name> has a lucky line!" on every peer; that fight has a longer snap grace and lower strain; the fight log row has `pity` 1. A Landed catch or a cancel never shows the toast.
- Expected: the nudge is noticeable but not a free win (same species, weight and quality odds).
- **USER playtest line (2026-10-12):** after at least 10 nudged casts, write down whether the longer snap grace and the lucky-line toast are noticed, whether failures now feel less punishing, and whether anyone feels the game is helping too much.
- Record: **USER playtests the bad-luck nudge and decides keep / tune / remove.** `Result: ______`

**I. User actions due.**
- Run this checklist.
- Run the fun gate with a friend (dev build).
- Decide the bad-luck nudge: keep / tune / remove (section H).
- Approve the T29 look.
- Approve merging `m2-fishing-core` to `main` after the final review.

**J. Carried manual checks and later-milestone hand-offs (2026-10-12, C 16.2). The doc includes this section.**
- Manual checks carried from reviews:
  - **F3 overlay:** press F3 in the editor (overlay toggles and updates through a cast, bite and fight); in a NON-development player build F3 and F4-F9 are no-ops (T8-fix/T10 review).
  - **Swimming interact gate:** swim to the ladder and the bell with the rod out and stowed; confirm what can be interacted with (T16 note; recorded by the T28 look check).
  - **150 ms RTT clean-claim slack:** the 2.0 s `CleanClaimClockSlackSeconds` is a guess (skeleton risk 3); note the MPPM result.
  - **Gamepad:** acceptance 1 and rumble 0 on every terminal state need a controller.
  - **Rod geometry (ruling T19-R1):** with the T19-fix in, cast from the deck end at min and max charge at camera pitch -60, 0 and +60: the rod tip stays in front of the camera and the line never starts shorter than the host expects (no `Invalid` escapes from the early-hook or landed-floor checks on honest casts).
- **M4 hand-off note:** `FishCaught` is published even when the host's creel store was missing (the fish was not stored). Quota consumers (M4) must count creel data (`PlayerCreel` / `CreelLedger`), never `FishCaught` events.
- **M5 hand-off notes:** (1) knockback (`PlayerLocomotion.AddKnockback`) must END the cast, or the host walk bound (`CatchValidator.WalkBoundedLineLength0`, sprint speed) must be raised by the maximum knockback; otherwise a knocked-back player can look "teleported" to the line-start check. (2) The router's `Staggered` row must zero `Move` (M2 only guarantees RodFighting does; ruling C-Q7).

### T30.2 README edits
- **Top bullet list:** add `M2 manual acceptance checklist: docs/superpowers/plans/2026-10-11-m2-acceptance.md`.
- **New section "Fishing (M2)".**
  - A controls table from spec 8.3: equip 1 / wheel / North; ready RMB / LT (hold); charge and cast LMB / RT (hold, release); cancel RMB while Waiting; reel S or LMB / RT; steer A/D / left stick X.
  - The rule: you can walk with the rod out, but movement is locked while hooked (M2-D2).
- **New section "Fishing dev keys"** (editor and development builds only):
  - the table F3 overlay, F4 next rod, F5 next bait, F6 forced 0.1 s bite (host), F7 twitchy hook (host), F9 ripple at the crosshair;
  - the bell rings a debug "sell creel";
  - the debug loadout (Handmade rod, worm selected, 20 of each bait, bare hook unlimited) and that selection is refused while a cast is active.
- **New section "Fight log":**
  - the path and suffix rule (C 5.3, M2-D13), dev builds only;
  - one row per cast that reached Waiting (cancels and misses included);
  - the column list;
  - delete the file to start a fresh log;
  - the fun-gate stats recipe lives in the M2 acceptance doc.
- **"Generated scenes are disposable" table.**
  - Add the M2 rows. Rebuilt every build: `Content/Meshes/*.asset`, the generated materials, `Content/Rendering/*`.
  - Created only if missing (Inspector edits kept; this is where tuning lives): `Content/Fishing/**` (rods, baits, fight profiles, species, junk, `FishingTuning.asset`), `Content/Audio/SfxCueTable.asset`, `Content/Art/ArtPalette.asset`, `Content/Art/WaterSettings.asset`.
  - Confirm each row against the builders (`FishingContentBuilder` is create-if-missing per C 6.2; check `RenderingSetupBuilder` for the volume profiles) before writing it.
- **"Repository notes":** FBX models under `Content/Models/` are LFS. Run `git lfs pull` if a model imports as a tiny text file.
- ASCII only in new text. The README is plain ASCII today: keep it that way.

### T30.3 Steps
1. Pre-flight (G0.4). Collect the inputs listed in the header: ask the controller for the T15, T28 and T29 reports if they are not in the repo.
2. Run `test-edit -Filter RodStateMachine` (criterion 2 count) and `test-edit -Filter "FishGame.Tests.EditMode.FishingBots"` (criteria 8, 11). Keep the outputs in `$env:FG_OUT\t30\`.
3. Write the acceptance doc (T30.1) with the counts and bot tables pasted in. Leave every manual `Result:` as `______`.
4. Edit `README.md` (T30.2).
5. **Docs self-check** (a scratch PowerShell script in `$env:FG_OUT\t30\`, not committed):
   - no byte above 0x7F in either file;
   - the acceptance doc contains `Criterion 1` through `Criterion 11`, all nine MPPM items (MP1-MP9), every 9.13 case name from the spec table, the "Bad-luck nudge" block and section J ("Carried manual checks and later-milestone hand-offs": F3 editor and non-development check, M4 FishCaught note, M5 knockback and Staggered notes);
   - the README contains the headings `Fishing dev keys` and `Fight log`.
6. **Final `check`.** On FAIL, apply the integration-fix rule (C 2.6): fix the defect in the earlier task's file, keeping the public surface frozen, commit `fix(<file>): <what>`, and rerun until `FG RESULT check PASS`. A fix that needs a contract signature change: stop and report.
7. **Review the working tree** against the baseline. Only the two docs, any fix files and regenerated outputs may appear. Restore scene fileID churn.
8. **Commit**, literally:
```powershell
git -C C:\Dev\FishGame add docs/superpowers/plans/2026-10-11-m2-acceptance.md README.md; git -C C:\Dev\FishGame commit -m "m2(docs): M2 acceptance checklist, fun-gate template, README fishing sections" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
9. **Report:**
   - the `check` line;
   - the criterion 2 count;
   - the criterion 11 number;
   - the bot targets status;
   - the list of integration fixes.

### T30.4 Acceptance (Sonnet docs reviewer)
- Every 17.2 criterion has a line, an owner and a how-to-check.
- All MPPM items and every 9.13 row are present, with the M4 rows marked N/A.
- The fun-gate recipe definitions match T30.1 D (row sets, targets).
- The README keys match C 5.3. The CSV header matches `FightLogCsv.Header` exactly.
- ASCII only. No invented test names: every test cited exists in C 10 or in the M1 suite.

### T30.5 Hand-off to the final branch review (controller)
After T30's docs review passes, the controller dispatches **one Opus (effort xhigh) reviewer** on the whole branch:
- **Scope:** `git diff main...m2-fishing-core` (three dots), plus `git log main..m2-fishing-core`.
- **Inputs given to the reviewer:**
  - the contract, the skeleton, all seven section plans and the acceptance doc;
  - the controller's scripted-check results for the whole branch (C 2.8 forbidden identifiers, BOM / non-ASCII scan, owned files vs C 12 and C 2.6 via `git diff --name-only main...m2-fishing-core`, test counts vs C 10);
  - the status of skeleton section 8 risks 1-8.
- **Focus:**
  1. Contract conformance: public names, paths and test names.
  2. Authority comments and host-only behaviour (C 7 notation rule).
  3. Criterion 7 paths: forged and out-of-order casts and hooks, the duration floor, and clean claims that never upgrade blindly and never invalidate.
  4. Late join and disconnect (G2, G3, `ServerInitialized`, slot-keyed creel).
  5. Determinism: Pcg32 streams, the `FightSim` draw order, `MeshBuilder` and lake dock byte-identity.
  6. PlayMode warning-freedom and log hygiene.
  7. Cross-task seams: rumble 0 on every terminal state and on disable, the HUD and overlay lifecycle across sessions, and the arc played once with the first value snapped.
  8. Criterion 9: no tuning literals in Core.
  9. Presentation and UI never write gameplay state (C 2.8 #17).
- **Output:** findings by severity (Critical / Important / Minor), each with `path:line`, the evidence and a proposed fix.
- **Then:**
  - one fix wave (Sonnet; Fable escalates anything Sonnet cannot solve);
  - re-reviews per skeleton section 2: Sonnet for fixes of 50 lines or less in one area, Opus for ordering or concurrency;
  - the final `check`;
  - the user runs the acceptance doc and the fun gate;
  - merging to `main` only with the user's consent (handoff); pushing per the user's standing permission once the work is solid.

---

## Contract questions (all closed by controller rulings of 2026-10-11; C 15)

1. **Catch toast route (closed, G-Q1).** T25 formats the toast text itself and publishes `ToastRequested("toast.raw", text, 4f)` with the new passthrough key `toast.raw` = "{0}" (also used for `toast.pity`).
2. **Extra UiStrings keys (closed, G-Q2).** Added: `toast.raw`, `toast.junk`, `toast.pity`, `settings.gauge_off`, `settings.gauge_arc`, `settings.gauge_bar`. The settings keys use M1's `settings.*` prefix (not `set.*`); C 7.25 amended.
3. **`toast.lost` text (closed, G-Q3).** Generic "It got away!" (covers snap and escape); no wire change in M2.
4. **Assist and the gauge (closed, G-Q4).** Every false-to-true change of the assist toggle while the gauge is Off sets the gauge to Arc.
5. **RunSeed (closed, G-Q5).** Display only; the misleading constant comment in C 5.2 is fixed.
6. **Fish submesh count (closed, G-Q6).** Fish models are ONE submesh (single `M_Toon_Fish`, vertex colours); T14's `Validation_SpeciesModels_Pass` also checks `subMeshCount == 1`; T29 imports the staged models, including the new perch.
7. **Vertex colour space (closed, F-Q3; supersedes G-Q7).** Fish vertex colours are LINEAR (`colors_type='LINEAR'`); the Toon shader treats vertex colours as linear; `FishFallbackMesh` follows the same rule.
8. **M1 perf baseline (closed, G-Q8).** The M2 numbers become the first perf baseline.
9. **Temporary helpers (closed, G-Q9).** T28's fingerprint test and T29's lineup script stay uncommitted; the reviewer confirms deletion.
10. **T28 and `AssetPaths` (closed, G-Q10).** T28 mesh paths live in `LakeDockLayout.cs`.
11. **T25/T26 automated coverage (closed, G-Q11).** Manual checks only.
