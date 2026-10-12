# FishGame M2 - Plan Section G: UI, world, acceptance (T25, T26, T28, T29, T30)

- **Date:** 2026-10-11. Branch `m2-fishing-core`.
- **Contract ("C"):** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (binding). **Skeleton:** `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md`.
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` sections 9.11, 9.13, 10.1-10.7, 11.1-11.4, 11.6, 17.2 and 3.2 (R62-R64).
- **Scope:** T25 Fishing HUD, T26 Dev overlay, T28 Lake dock rebuild, T29 Blender species models (controller checklist), T30 Acceptance and final hand-off.
- **Shape:** behaviour, signatures, checks and acceptance. None of these tasks is on the skeleton 1.3 list of code-carrying bodies, so no task below contains complete code. No task below is Haiku-eligible.
- **Contract doubts** are listed under "Contract questions" at the end. Each task body picks a default, so implementation can start before the controller answers.

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
- **#11** Check `Temp/UnityLockfile` before any batchmode run. Never kill Unity. If a run prints `LOCKED`, stop and ask the user to close the editor.
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
3. `Test-Path C:\Dev\FishGame\Temp\UnityLockfile`: if true, stop and ask the user to close the editor.

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
- New `UiStrings` keys, exactly as C 7.25 gives them: `cap.bite` "Bite!", `cap.straining` "Line straining", `cap.snapped` "Snapped", `cap.escaped` "Got away", `hud.click` "Click!", `hud.creel` "{0}/{1}  {2} kg", `hud.creel_full` "Full - sell!", `toast.catch` "{0} caught {1} kg {2} (${3})", `toast.lost` "Snapped! It was a {0}", `toast.creel_full` "Creel full! {0} released", `toast.clean` "Clean catch!", `set.line_gauge` "Line gauge", `set.captions` "Captions", `set.assist` "Fishing assist".
- Additive keys this plan needs (contract questions Q1, Q2): `toast.raw` "{0}" (passthrough for pre-formatted toasts), `toast.junk` "{0} fished up {1} (${2})", `set.gauge_off` "Off", `set.gauge_arc` "Arc", `set.gauge_bar` "Bar".

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
  - Read `registry.FishingTuning?.ToCore()` for `CreelMaxEntries` and `CreelMaxWeightG`. If the registry has no fishing tuning, hide the creel and gauge and keep everything else working.
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
   - Text: if `CreelRules.IsFull(snapshot)`, then `hud.creel_full` with class `full`. Otherwise `UiStrings.Format("hud.creel", count, maxEntries, kg)`, where kg = `TotalWeightG / 1000f` formatted `"0.0"` with `CultureInfo.InvariantCulture`, plus class `warn` when `CreelRules.IsWarn(snapshot)`.
   - Recompute only on `PlayerCreel.Changed` or when the creel reference changes, not every frame.
   - `warn` = bold, `UiWarn`, and a 3 px left border. `full` = bold, `UiBad`, and the different text. The shape and text change carry the meaning, not hue alone.
6. **Catch toasts** (`CatchAnnounced`; App bus; every peer).
   - Let `a = msg.Announcement`, `local = msg.IsLocalCatcher`, `item = registry.DisplayNameOf(a.Catch.IsJunk, a.Catch.ItemIndex)`.
   - Catcher name: the `PlayerNetState` in `PlayerNetState.Active` whose `Slot == a.CatcherSlot`, using its `DisplayName`, the same lookup `NameplateOverlay` uses. If that player has gone (left before the announce arrived), use `"?"`.
   - **Rate limit (crew only, spec 11.4).** For `!local`, keep the last shown time per catcher slot (`Time.unscaledTime`). Drop a crew toast that arrives within `FishingConstants.CrewToastMinIntervalSeconds` (3 s) of the previous one for that slot; the in-world reveal still shows. Local toasts are never rate-limited.
   - By `a.Kind`:
     - `Landed`, fish: `toast.catch` with (name, kg `"0.0"`, item, `ValueCoins`). Landed junk: `toast.junk` with (name, item, `ValueCoins`). Show them for everyone (rate-limited for crew).
     - `Landed` with `a.CleanBonus && local`: also `toast.clean`. The clean toast is local only (crew see the reveal sparkle from T23).
     - `Lost`: `toast.lost` with (item), for everyone (rate-limited for crew). The text says "Snapped!" for every Lost, including a hooked escape; see Q3.
     - `ReleasedCreelFull`: `toast.creel_full` with (item), **local only** (it is about your own creel).
     - `None`: nothing.
   - **Delivery (Q1).** `ToastRequested` carries one `Arg`, and `ToastPresenter` (M1, not owned here) formats with one argument. So format the full text first with `UiStrings.Format(key, params args)`, then publish `new ToastRequested("toast.raw", text, 4f)`. Never publish `toast.catch` and its siblings directly: a single-argument format of a four-placeholder template throws `FormatException`, which is logged as an error and fails PlayMode.
   - All numbers are formatted with `CultureInfo.InvariantCulture`.
7. **Settings rows (SettingsPresenter).** Keep the M1 bind pattern (refreshers, `NotifyChanged`, no save per tick).
   - `LineGauge`: a `SliderInt` 0..2 bound to `lineGauge`. Its label is `set.line_gauge` + two spaces + the value name (`set.gauge_off` / `set.gauge_arc` / `set.gauge_bar`). Add a private `BindChoice` helper next to `BindInt`.
   - `Captions`: `BindBool` on `captions`, label `set.captions`.
   - `FishingAssist`: a `BindBool`-like bind on `fishingAssist`, label `set.assist`, with the C 7.10 rule: on the toggle's **false-to-true change**, if `lineGauge == 0`, set `lineGauge = 1` (Arc), call `NotifyChanged`, and refresh the gauge slider. Turning assist off never changes the gauge, and the player can then turn the gauge off separately. There is no persisted "already done once" flag, because `SettingsData` belongs to T10 (Q4).
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
7. Commit (G0.5): `m2(hud): fishing HUD - charge ring, Click prompt, captions, line gauge, creel, catch toasts, settings rows`. Paths: the new `.cs`, its `.meta`, the `UI/Fishing` folder `.meta` if new, and the five edited files.

### T25.6 Acceptance
**Reviewer (Opus high) checks:**
- The C 7.25 surface is exact: `internal sealed class FishingHudPresenter : IHudExtension`, the keys, and the `Format` overload with the old overload kept.
- The only `UiFlowController` change is one hook line. No file outside the T25 list is touched.
- Nothing writes gameplay state. Reads go through `Machine` getters, `PlayerCreel`, `ViewState` and events (C 2.8 #17).
- The lifecycle: re-resolution on `LocalChanged`, `PlayerCreel.Changed` unsubscribed on change and in `Unbind`, null-safe across leave and rejoin and a scene reload.
- The toast route is pre-formatted through `toast.raw`. The crew rate limit is keyed per slot, and local toasts are never limited. Clean and creel-full toasts are local only.
- The gauge shows the local line only, only while Hooked on a fish. It has the 0.8 tick, the warn thickening and the grace blink. The prompt and captions are off-centre.
- Assist-on sets the gauge to Arc only on the false-to-true edge, and only from Off.
- Invariant culture everywhere. No per-frame allocations in `Tick` beyond string changes on a value change.

**Controller manual checks** (editor, after the commit; record PASS/FAIL in the task report):
1. Solo with F6 forced bite (after T26) or a normal wait. Ready, then charge: the ring fills and ping-pongs. Hook a fish with the gauge on Arc: the tick sits at 0.8, the fill thickens past it, and it blinks in grace. Switch to Bar: same reads.
2. Captions on: "Bite!" appears bottom-centre. Captions off: none appears, and an active caption disappears.
3. With assist off, gauge Off and assist then switched on, the gauge becomes Arc. Turn the gauge Off again with assist still on: it stays Off. Toggle assist off and on again: it becomes Arc again (edge rule).
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
  - If `hit.collider.gameObject.layer == Layers.Water`: `RippleField.Current?.Spawn(hit.point.x, hit.point.z, RippleMath.Bite)`. Otherwise `dev.no_water`.
  - This is the spec 17.2 item 7 "ripple debug key".

**Overlay text.**
- Refresh at 10 Hz (accumulate `dt`) only while visible. Reuse one `StringBuilder`. Format with invariant culture, 2 decimals for 0..1 values and 1 decimal for seconds and metres.
- Lines, each skipped when its source is null:
  1. `ROD state=<RodState> t=<StateTime> castId=<CastId> assist=<on|off> recall=<Recalling>`, from `FishingRodController.Local.Machine`.
  2. `FIGHT phase=<Calm|Burst> dir=<BurstDir> tension=<..> stamina=<..> line=<LineLength>m lat=<Lateral> el=<Elapsed>s snap=<SnapTimer> pin=<PinnedTimer> slack=<SlackTimer> recov=<NearSnapRecoveries> max=<MaxTension>`, from `Machine.Fight.State`. For junk: `FIGHT junk line=<..> el=<..> slack=<..>`.
  3. `BITE timer=<BiteTimer>/<BiteWait>s nibbles=<Roll.NibbleCount> hookWindow=<Roll.HookWindow>` (when `HasResult`).
  4. `ROLL <item name> <WeightG> g <Quality> $<ValueCoins> t=<WeightT> size=<SizeClass> seed=0x<FightSeed:X8>` (when `HasResult`; this is the owner's own last roll, spec 9.11 "last roll").
  5. `LOADOUT rod=<RodAt(NetRodIndex).DisplayName> bait=<BaitAt(NetSelectedBait).DisplayName> x<count>`, where count 0xFFFF prints `inf`. Read from the local `FishingRodNetwork` NetVars, the host mirrors of the ledger (C 8.3).
  6. Host only: `HOST casts=<HostCastCounter> now=<Now>s catches=<CatchLog.Count> runSeed=0x<RunSeed:X16> forcedBite=<ForcedBiteWait> twitchy=<on|off>`, then one line per `FishingRodNetwork.Active` entry: `  slot <Player.Slot> client <OwnerClientId>: <Waiting|Hooked castId N | idle>` via `Ledger.TryGetActive(OwnerClientId, out cast)`. On a client the line reads `HOST n/a (client)`.
  7. `LAST <last action text>` (for 4 s after a key).
- `FishingRodNetwork.Spawned`/`Despawned` (C 8.1) may be used to keep the active list without polling. Polling `Active` at 10 Hz is also fine.
- **RunSeed** is displayed only. No key changes it (C 5.3 lists no key for it; see Q5).

**Unbind.** Unsubscribe everything and hide the label.

### T26.3 Steps
1. Pre-flight. Read T25's committed `FishingHudPresenter.cs` (same patterns), `Runtime/Input/IDevInput.cs`, `DevKey.cs`, `Fishing/Debug/FishingDebugSettings.cs`, `Fishing/Host/FishingHost.cs`, `Fishing/Host/CastProbe.cs`, and `Presentation/World/RippleField.cs`.
2. Edit the UXML, USS and UiStrings, write the presenter, and add the hook. Run `compile`.
3. Run `build`, the full `test-edit` and the full `test-play` (PlayMode's `FakeInputService` returns false for every `DevKey` unless a test sets one, so nothing fires).
4. Run the added-lines ASCII scan.
5. Commit: `m2(dev): F3 dev overlay and F4-F9 debug keys`.

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
  - Add a per-face colour `FaceJitter.Grey(meshSeed, faceIndex, 0.04f)` (grey, alpha 1; the same value on all 3 vertices of a face).
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
   - Message: `m2(lakedock): rebuild lake dock with MeshBuilder props, water grid, sky, fog and volumes`.
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
- The M1 baseline in `2026-10-09-m1-acceptance.md` may still be unrecorded (Q8). In that case T30 records the M2 numbers as the first baseline and notes it.

---

## T29 - Blender species models (controller checklist; not a subagent brief)

**Header**
- **Who:** the controller, in the user's single live Blender 5.2 instance through Blender MCP (`execute_blender_code` runs Python, `look` renders a view). There is no subagent and no worktree.
- **Goal (C 7.29, R62):** Make `Fish_Bluegill.fbx`, `Fish_Carp.fbx`, `Fish_Trout.fbx` and `Fish_Catfish.fbx`, and re-export `Fish_Perch.fbx` only if needed (step C). Each model: under 400 triangles (target 150-300), flat normals, nose +Z in Unity, origin at the body centre, identity transform, scale 1, per-face vertex colours from the species `colorA`/`colorB`, no materials or textures, **one submesh**.
- **Owned (C 12):** `Assets/_Project/Content/Models/Fish_{Bluegill,Carp,Trout,Catfish}.fbx` and their `.meta` (plus `Fish_Perch.fbx` only if re-exported), and the regenerated species assets (`model` refs only).
- **Depends on:** T20 (`FishModelPostprocessor`, `M_Toon_Fish`, `RenderValidators`), T23 (`FishFallbackMesh`, used for the colour comparison), T28 (committed or in review; skeleton section 5).
- **Review:** Sonnet (import settings, LFS, validators, asset diffs), then the **user look check**. The commit trailer names the controller's model.

### A. Inputs (read; do not copy values into this plan)
1. Species colours and shapes: read `colorA`, `colorB` and `bodyShape` from `Assets/_Project/Content/Fishing/Species/<id>.asset` (generated by T14 from `FishingSeedData`). The assets are the runtime truth.
2. Nominal model length = `CatchMath.LengthMm` at the mid weight, `400 x cbrt(midKg) x lengthShapeK (1.0)`. The reveal rescales to the rolled `LengthMm` anyway (C 7.23), so this only keeps the source files comparable:

| Species | Profile | Weight range kg | Mid kg | Nominal length |
|---|---|---|---|---|
| bluegill | Steady | 0.2-0.8 | 0.50 | 317 mm |
| perch | Darter | 0.3-1.2 | 0.75 | 363 mm |
| carp | Bulldog | 1.0-4.0 | 2.50 | 543 mm |
| trout | Thrasher | 0.6-2.5 | 1.55 | 463 mm |
| catfish | Runner | 2.0-7.0 | 4.50 | 660 mm |

3. Read `Presentation/Feedback/FishFallbackMesh.cs` (T23). Note how it writes `colorA`/`colorB` into vertex colours: the `Color` as stored (sRGB values), or `.linear`. The models use the same convention (step D4).

### B. Blender preflight
1. `get_addon_status` and `get_scene_info`. Record the Blender version.
2. Work in a fresh scene, or delete the default cube, light and camera. Exports are selection-only, mesh-only, either way.
3. Scene units: Metric, unit scale 1.0, length in metres.
4. Read the FBX exporter's RNA before relying on names (MCP rule: never hardcode enum identifiers):
   - list the `bpy.ops.export_scene.fbx` properties and the enum items of `axis_forward`, `axis_up`, `apply_scale_options`, `mesh_smooth_type` and `colors_type`;
   - confirm every name in step E exists;
   - if one is missing or renamed in 5.2, stop and record the new name in the T29 report.

### C. Perch audit (decide the re-export)
1. Make sure the working copy holds the real FBX, not an LFS pointer: the file is larger than 1 KB, and its first bytes are not `version https://git-lfs`. If it is a pointer, run `git lfs pull --include "Assets/_Project/Content/Models/Fish_Perch.fbx"`.
2. Import `Fish_Perch.fbx` into a scratch Blender collection and record:
   - the triangle count;
   - the number of material slots (the spike commit says "5 submeshes");
   - any colour attributes;
   - **the Blender axis the nose points along after import.** That axis is the modelling convention for every new fish, because the spike export (`axis_forward='-Z'`, `axis_up='Y'`, `bake_space_transform=True`) produced nose +Z in Unity (G6).
3. Unity-side facts after T20's reimport: the controller's lineup script (step G) prints `subMeshCount` and `HasVertexAttribute(VertexAttribute.Color)` for `Fish_Perch`. These read mesh metadata, so they work with `isReadable` off.
4. **Decision:** re-export the perch if it has no per-face colours **or** more than one submesh. With 5 submeshes, the reveal's single `M_Toon_Fish` material draws only submesh 0 (Q6). Re-export means rebuild per step D, reusing the spike geometry when it is good, and export over the same file path (the `.meta` and GUID are kept).
5. Delete the scratch import afterwards.

### D. Per-species modelling checklist (repeat for each fish)
1. **Names.** Object and mesh datablock both `Fish_<Pascal>`. The meta uses `fileIdsGeneration: 2` (fileIDs from names), so a stable name keeps the species asset's `model` reference valid across re-exports.
2. **Build by script** from bmesh primitives: lofted body rings, tail, dorsal and pectoral fins, and species extras. Triangulate in the script so the count is exact. Target 150-300 triangles, hard cap 399.
3. **Silhouette notes:**

| Species | Body | Fins and extras | Colour layout | Tri target |
|---|---|---|---|---|
| bluegill | deep, flat-sided disc, height about 55% of length, small mouth | one long dorsal, rounded tail, small pectorals | colorA back and flanks, colorB belly; a dark "ear" patch behind the gill | ~180 |
| perch (if re-exported) | torpedo, height about 28% | two dorsals (front one spiky), forked tail | colorA back with 5-6 vertical bars alternating colorA/colorB, colorB belly and fins | ~200 |
| carp | heavy, high-backed, height about 35%, blunt head | long dorsal, deep forked tail, two short barbels | colorA (bronze) back, colorB lighter belly | ~220 |
| trout | streamlined, height about 25% | small adipose fin, near-square tail | colorA back, a colorB lateral band, darker speckle faces | ~200 |
| catfish | broad flat head (wider than tall), tapering body | 4-6 long barbels (thin prisms), small dorsal, long anal fin, rounded tail | colorA dark back, colorB pale belly | ~260 |

4. **Per-face colours.**
   - One colour attribute only, domain `CORNER`. All three corners of a face get the same colour.
   - Use colorA, colorB, or a darker accent (colorA x 0.7, alpha 1) for fins, eyes and bars.
   - Colour space: match the `FishFallbackMesh` convention from A3. If it writes sRGB values, use a byte colour attribute with the sRGB values and export `colors_type` SRGB. If it writes `.linear`, use a float attribute with linear values and export LINEAR.
   - Step G's side-by-side render settles it; record the choice (Q7).
5. **Flat normals.** Every face has `use_smooth = False`. No custom split normals and no Auto Smooth modifier.
6. **Transform.**
   - Origin set to the bounds centre, object location at the world origin.
   - Rotation and scale applied, so the object transform is identity.
   - Nose along the axis recorded in C2.
7. **Size.** The length along the nose axis is the nominal length from A2, within 5%.
8. **No material slots** (remove all), so the FBX has one mesh and Unity imports one submesh. UV maps are not needed; remove them.
9. **Look.** Use `look` from the side, top and front. Fix bad faces (non-manifold fins are fine; inverted faces are not: recalculate normals outside before export).
10. **Record** the triangle count (from the evaluated mesh) for the report.

### E. Export (per fish; parameters that reproduced the spike)
Select only `Fish_<Pascal>` and export with:
- `filepath = C:/Dev/FishGame/Assets/_Project/Content/Models/Fish_<Pascal>.fbx`
- `use_selection = True`, `object_types = {'MESH'}`
- `axis_forward = '-Z'`, `axis_up = 'Y'`, `bake_space_transform = True`
- `apply_scale_options = 'FBX_SCALE_UNITS'`, `apply_unit_scale = True`, `global_scale = 1.0`
- `mesh_smooth_type = 'FACE'`
- `colors_type` per D4
- `use_mesh_modifiers = True`, `use_tspace = False`, `use_custom_props = False`
- `add_leaf_bones = False`, `bake_anim = False`
- `path_mode = 'STRIP'`, `embed_textures = False`

For the perch re-export, overwrite the same path. Never delete the `.meta`.

### F. Unity import and wiring
1. Check `Temp/UnityLockfile`: if the editor is open, ask the user to close it.
2. Run `build`. `FishModelPostprocessor` applies the import settings. `FishingContentBuilder` fills each null species `model` from `Content/Models/Fish_<Pascal>.fbx` (C 6.2 rule). A re-exported perch keeps its existing reference.
3. Check each new `.meta` (and the perch's):
   - `materialImportMode: 0`, `importCameras: 0`, `importLights: 0`
   - `globalScale: 1`, `useFileUnits: 1`, `isReadable: 0`, `meshCompression: 0`
   - `importAnimation: 0` (C 7.20)
4. `git diff Assets/_Project/Content/Fishing/Species` shows only `model:` lines changing (null to a fileID and guid).
5. Run the full `test-edit` (`Validation_SpeciesModels_Pass` and `Validation_ToonMeshesHaveVertexColours_Pass` must pass) and the full `test-play`.

### G. Visual verification (batchmode render, then the user)
1. **Use an existing tool first.** If T20 or T21 left a committed capture helper, use it. Search `Assets/_Project/Editor` for `allowAsyncCompilation`.
2. **Otherwise write a temporary editor script**, never committed: `Assets/_Project/Editor/Dev/TempFishLineup.cs`, namespace `FishGame.EditorTools`, `public static void Render()`. It must:
   - set `ShaderUtil.allowAsyncCompilation = false` (restore it in `finally`, C 2.8 #13);
   - open a new empty scene;
   - push the Toon globals the way `PaletteGlobals.Apply` does (add the component and call `Apply`, or set `_FGPalette`, `_ToonShade` and `_ToonAmbient` from the registry palette's Day phase);
   - add a directional light at (50, -30, 0);
   - add an orthographic camera with a side view, and a second pass with a top view;
   - for each species, place the model with `M_Toon_Fish` in a row, with the same species' `FishFallbackMesh.Get` mesh (rendered with `M_Toon_Fish`) under it for colour comparison, and a small marker cube at +Z of each fish (it shows the nose direction);
   - log `subMeshCount`, `HasVertexAttribute(VertexAttribute.Color)`, the triangle count and the bounds size per model (feeds steps C3 and D7);
   - render to a `RenderTexture`: `camera.Render()`, or `RenderPipeline.SubmitRenderRequest` with a `RenderPipeline.StandardRequest` if URP ignores `Render()` in batchmode;
   - read the pixels and write `$env:FG_OUT\t29\lineup_side.png` and `lineup_top.png`;
   - end with `EditorApplication.Exit(0)` (1 on an exception).
3. **Run it with a graphics device** (no `-nographics`):
   `& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -projectPath C:/Dev/FishGame -executeMethod FishGame.EditorTools.TempFishLineup.Render -logFile "$env:FG_OUT\t29\render.log"`
   Then grep the log for `error CS`, `Exception` and the per-model lines.
4. **Read both PNGs** with the Read tool. Check:
   - the nose is on the marker side for every fish;
   - the facets are visible (flat);
   - the colours match the fallback row;
   - nothing is pink or black;
   - the relative sizes are plausible.
   Fix in Blender and re-export as needed.
5. **Delete** `TempFishLineup.cs` and its `.meta`. `git status` must not list them.
6. **User look check.** Show the user the two PNGs. Then ask them to play Solo, rotate baits with F5 (raw meat reaches every species; cast long for catfish, depth 2.5 m or more), and land each species with F6 forced bite. The reveal must show the model at the right size and colours. The user decides; iterate in Blender until they approve.

### H. LFS check and commit
1. `git -C C:\Dev\FishGame check-attr filter -- Assets/_Project/Content/Models/Fish_Carp.fbx` prints `filter: lfs`. Repeat for each FBX.
2. Stage explicit paths: each new FBX and `.meta`, the perch FBX only if re-exported, and the changed species `.asset` files.
3. `git -C C:\Dev\FishGame lfs ls-files` lists all five `Fish_*.fbx`.
4. `git -C C:\Dev\FishGame show :Assets/_Project/Content/Models/Fish_Carp.fbx` starts with `version https://git-lfs.github.com/spec/v1` (the index holds a pointer, not the binary). Repeat for each FBX.
5. Commit: `m2(models): Blender species models - bluegill, carp, trout, catfish (perch re-exported: <yes|no>)`, with trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (the controller's model, C 2.1).
6. **Report** for the Sonnet reviewer:
   - triangle counts;
   - the perch audit result and decision;
   - the colour convention;
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
| 2 | Every 9.2 transition has a passing EditMode test | Claude (T30) | `test-edit -Filter RodStateMachine`: 44 test methods (38 row tests from C 10.2 + 6 rule tests). `RodStateMachine_IllegalEvents_IgnoredInEveryState` is a TestCaseSource over 11 states, so NUnit reports **54 results** (38 + 5 + 11), all passing. Paste the count line. |
| 3 | Co-op: cues on both peers; reveal for everyone after the host commits | User | MPPM 2-4 instances: the MPPM list items MP1 and MP4 below. |
| 4 | Tension and phase readable with no HUD and with sound off | User | Gauge Off, captions off, master volume 0. Fight 3 fish (one Bulldog). Name aloud: side run vs dive (splash column vs plunge and line down), creak band (rod bend), warn (shake plus the line dash and width pulse). A second person watches the screen and confirms. Then repeat with sound on and eyes on the rod only. |
| 5 | Rod back to Idle within 3 s in every 9.13 case; no stuck states; no rumble left | User + Claude | Section C below (the 9.13 walk). Gamepad connected, so rumble is observable. |
| 6 | Bait consumed on accept, refunded on cancel/recall, lost on snap/escape/miss; bare hook never runs out | Claude (tests) + User | EditMode `FishingHostLedger_Accept_ConsumesBaitAndRolls`, `FishingHostLedger_CancelInWaiting_RefundsBait`, `FishingHostLedger_SnapEscapeMiss_DoNotRefund`, `FishingHostLedger_BareHook_NeverRunsOut`; PlayMode `SoloHost_CancelInWaiting_RefundsBait`. Manual: F3 LOADOUT count 20 -> 19 on a cast, back to 20 after RMB cancel; a miss (F7 twitchy, don't click) keeps 19; bare hook shows `inf`. |
| 7 | Host rejects forged or out-of-order casts and hooks and impossibly fast catches; species and weight come from the host roll | Claude (tests) + Opus xhigh review | EditMode ledger tests (C 10.1 T9 rows: stale/duplicate id, charge range, origin > 3 m, no bait, early hook, landed below floor, implausible or self-contradictory clean claims). The branch review confirms that no owner-side path decides species, weight or quality (the owner only displays `CastResultRpc`'s roll; the creel and announce use `CommitDecision.Roll`). |
| 8 | Bots meet the 9.12 targets (a failure passes only with numbers recorded as a tuning item) | Claude (T30) | `test-edit -Filter "FishGame.Tests.EditMode.FishingBots"`. Paste the printed tables into section F. Each failing target is listed under "Open tuning items" with its numbers. |
| 9 | All tuning lives in assets; Core holds none except test defaults | Claude + User | Claude: the branch review's grep for 9.12 default literals in `Core/` outside `FishingConstants` (formula constants) and tests. User: in Play mode, change `Content/Fishing/FishingTuning.asset` `castChargeTime` 1.0 -> 0.6 in the Inspector; the ring fills faster on the next charge. Revert, or keep only if wanted (create-if-missing keeps Inspector edits). |
| 10 | Assist, line gauge, captions and Camera motion behave as 9.11 defines | User | Assist on: grace feels longer (snap later), and the gauge goes to Arc once (T25 rule). Gauge Arc/Bar/Off. Captions on/off ("Bite!", "Line straining", "Snapped", "Got away"). Camera motion 0: no shake, no FOV kicks, no rumble. |
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
- **Columns** (C 5.3): `profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean`.
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
     - the assist row count.
  5. Mark each target PASS/FAIL. A failed gate means **tuning data, not new mechanics** (17.2): retune the assets plus the matching `FishingSeedData` seeds (C 2.6 data row).
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
- Compare with the M1 baseline. If it was never recorded, write "No M1 baseline; these are the first numbers" (Q8).
- A regression above 10% gets a note.
- Also verify the V13 items here: fog variants survive in a player build (fog is visible in the dev build), the water facet lighting sign, and C#/HLSL wave parity (the bobber sits on the visible wave).

**F. Open tuning items.**
- T15's failing or marginal targets with numbers.
- The skeleton section 8 risks and their status: perch colours (T29), V11 `DeferLocal` (T19 test result), the clean-claim 2.0 s slack at 150 ms RTT (MPPM MP-run note), motion seeds, NetVar padding, behaviour indices, `-nographics` shader validation (T20 result), and gamepad.

**G. Definition of done (17.2).**
- Acceptance criteria 1-11 and the fun gate are met.
- Solo works offline (network adapter disabled, as in M1 criterion 2).
- 2-4 players fish together over dev-direct (and over Sessions once linked).
- The owner feels no latency at 150 ms RTT.

**H. User actions due.**
- Run this checklist.
- Run the fun gate with a friend (dev build).
- Approve the T29 look.
- Approve merging `m2-fishing-core` to `main` after the final review.

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
   - the acceptance doc contains `Criterion 1` through `Criterion 11`, all nine MPPM items (MP1-MP9) and every 9.13 case name from the spec table;
   - the README contains the headings `Fishing dev keys` and `Fight log`.
6. **Final `check`.** On FAIL, apply the integration-fix rule (C 2.6): fix the defect in the earlier task's file, keeping the public surface frozen, commit `fix(<file>): <what>`, and rerun until `FG RESULT check PASS`. A fix that needs a contract signature change: stop and report.
7. **Review the working tree** against the baseline. Only the two docs, any fix files and regenerated outputs may appear. Restore scene fileID churn.
8. **Commit:** `m2(docs): M2 acceptance checklist, fun-gate template, README fishing sections`.
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

## Contract questions

The default each task uses is in brackets. None blocks writing or starting the tasks.

1. **Q1 - Catch toast route.** C 7.25 gives multi-argument toast templates, but M1 `ToastRequested` carries one `Arg`, and `ToastPresenter.cs` (not in T25's C 12 files) formats with one argument. Publishing `toast.catch` directly would throw `FormatException` and fail PlayMode. [Default: T25 pre-formats the text and publishes `ToastRequested("toast.raw", text)`, adding the passthrough key `toast.raw` = "{0}". The alternative is to let T25 edit `ToastPresenter` or append labels to `ToastStack` directly.]
2. **Q2 - Extra UiStrings keys.** These additive keys aren't in C 7.25: `toast.raw`, `toast.junk` (a landed junk item reads badly as "caught 0.0 kg Old boot"), and `set.gauge_off`, `set.gauge_arc`, `set.gauge_bar` (value names for the line gauge row). Also, the C 7.25 settings keys use the prefix `set.*`, while every M1 settings key uses `settings.*`. [Default: add the extra keys; keep `set.*` exactly as in the contract.]
3. **Q3 - `toast.lost` text.** "Snapped! It was a {0}" is shown for every `AnnounceKind.Lost`, and Lost also covers hooked escapes (Pinned, ThrewHook, Dropped, Timeout; C 7.9 `AnnounceFor`). `CatchAnnouncement` has no outcome field to choose a different text. [Default: use the contract text for all Lost toasts. Better text would need the outcome in the announcement (a contract change), or a neutral text such as "Lost it! It was a {0}".]
4. **Q4 - "Assist sets the gauge once".** C 7.10 says "once when assist is first turned on". There is no persisted flag, and `SettingsData` belongs to T10. [Default: on every false-to-true change of the assist toggle, if the gauge is Off, set it to Arc; the player can turn it off again.]
5. **Q5 - RunSeed change key.** C 5.2's `DefaultDevRunSeed` comment says the dev overlay can change the seed, but C 5.3 and `DevKey` have no key for it (F8 is unused). [Default: T26 displays RunSeed only and invents no key.]
6. **Q6 - Fish submesh count.** The reveal uses one material (`M_Toon_Fish`). The spike perch has 5 submeshes (commit 52ce735), so only submesh 0 would draw. `FishingValidators.SpeciesModels` (C 7.14) checks only the origin and vertex colours. [Default: T29 exports one submesh per model and re-exports the perch if needed. Suggest adding `subMeshCount == 1` to `SpeciesModels` (T14 owner).]
7. **Q7 - Vertex colour space.** C 7.29 says "per-face vertex colours from colorA/colorB" but doesn't say whether they are stored as sRGB values or linear. The same goes for `FishFallbackMesh` (T23). [Default: T29 matches `FishFallbackMesh`'s convention and settles it with a side-by-side render. If they disagree visually, the controller picks one convention for both.]
8. **Q8 - M1 perf baseline.** The 10.7 baseline in the M1 checklist looks unrecorded (the multiplayer and perf parts were open on 2026-10-11), so "within 10% of M1" has no reference. [Default: T30 records the M2 numbers as the first baseline and notes it; the user may still record M1 from `main`.]
9. **Q9 - T29 capture tool ownership.** No task owns a committed batchmode render helper, so T29's lineup render uses a temporary, uncommitted editor script, and T28 uses a temporary, uncommitted EditMode fingerprint test. [Default: temporary files, deleted before commit and checked absent by the reviewers. If T20 or T21 committed a capture helper, T29 uses that instead.]
10. **Q10 - T28 and `AssetPaths`.** C 2.6 lets T14, T20 and T21 append constants to `AssetPaths.cs`, but not T28, which needs seven new mesh paths. [Default: T28 keeps its paths in `LakeDockLayout.cs`.]
11. **Q11 - T25/T26 automated coverage.** UI is unreachable from EditMode (C 2.8 #8), and C 10 has no PlayMode test for the HUD or overlay. A small PlayMode test (for example: forced catch, then exactly one `ToastRequested` with key `toast.raw`; crew rate limit) would need a new file in C 12. [Default: manual checks only, as written in T25.6 and T26.4.]
