# FishGame M3 - Section A plan: Carry and migration (T1, T2, T3; T4 pointer)

- **Date:** 2026-10-13 (written 2026-10-12). Branch `m3-fish-ecology`.
- **Binding contract:** `docs/superpowers/plans/2026-10-13-m3-interfaces.md` ("C" below). M2 contract: `2026-10-11-m2-interfaces.md` ("C2"). Skeleton: `2026-10-13-m3-plan-skeleton.md`. If this file disagrees with C, C wins, except where a contract change request below has been approved by the controller.
- **Code facts** were checked at commit `a91e1ad` (plus the uncommitted R70 work in the tree on 2026-10-12). Before T1 starts, R72 F0-F8 and R70 L7-L9 land (C 1.2), and they edit several files read here: `FishingSeedData` tuning and profiles, the `FishingContentTests` `Tn(...)` lines, the `FishingValidators` tuning ranges, the `CatchValidator` comments, `landDistance`, and `FishingRodController.cs`/`RodViewSoloTests.cs` (uncommitted R70 edits on 2026-10-12). This plan therefore cites **symbols, not line numbers**. Implementers start from the committed end state of the rework.
- **Scope:** the C 14 carry list (T1, T2) and the spec 12.6 step 1 migration (T3). There are no new gameplay features. T4 is a pointer only.
- **Order:** T1 -> T2 -> T3, serial (one Unity lock). Pipelining (skeleton 5): T2 may start while T1 is in review (the two share no file), and T3 may start while T2 is in review.

---

## Contract change requests (for the controller; none renames or removes a contract name)

**Status: CCR-A1..A7 are all ACCEPTED as proposed (CCR accepted 2026-10-12) and applied in the contract** (2.6, 7.1-7.3, 10.1, 12; rulings M3-D69 = A1, D70 = A2, D71 = A3, D72 = A4, D73 = A5, D74 = A6, D75 = A7). The primary option of each row below is the one to build; the fallback of CCR-A3 is not used.

| # | Where in C | Request | Why |
|---|---|---|---|
| CCR-A1 | 7.1 (second bullet), 14 row "F6/F7 reset" | Reset F6/F7 on **`INetworkSession.Ended` only**, plus `SubsystemRegistration`. Drop "/ `ServiceRoots.Game` teardown". | `ServiceRoots.Game` is cleared by `GameScope.OnDestroy` (App), and every Spot scene has its own `GameScope`. That teardown therefore fires on every Spot single-load, including the M3 map switch (T15), which is exactly the case the carry item must survive. Wording only, no public name. |
| CCR-A2 | 2.6 row `Gameplay/AssemblyInfo.cs`, 12 row T2 | **T2** adds `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]` to `Gameplay/AssemblyInfo.cs` (one line, keeping the PlayMode line and the marker). T14's "if needed" row then finds it already present. | The required test `DisplayNameRules_Truncate_SharedByNameWireAndSessionText` (C 10.1, EditMode) must call `NameWire.ToFixed` and `SessionText.Name`, which are `internal` to Gameplay. Today Gameplay grants internals to PlayMode only. |
| CCR-A3 | 2.6 (new row `App/AppBootstrap.cs` for T2, before T20's), 12 row T2, 7.2 | **T2** adds one test seam to `AppBootstrap`: `public static Func<INetworkSession> OnlineSessionFactoryOverride { get; set; }`. It is null in production and reset in `ResetStatics`. The online backend factory reads it lazily: `() => OnlineSessionFactoryOverride != null ? OnlineSessionFactoryOverride() : new UgsNetworkSession(sessionConfig)`. | The required PlayMode test `UiFlow_OnlineCancel_ShowsCancellingImmediatelyAndReturnsToMenu` needs "a fake `INetworkSession` whose cancel completes late". `UiFlowController` captures the session in `Initialize`, which `AppBootstrap.Start` calls on the first frame, so the session cannot be swapped afterwards. The UI assembly has no InternalsVisibleTo, so `UiSessionActions` cannot be built directly either. `CompositeNetworkSession` creates its online backend lazily through the factory, so the override reaches the real UI flow end to end. Precedent: `AppBootstrap.SuppressDevAutoStart` (public static test hook). **Fallback if rejected (not used: seam CCR accepted 2026-10-12):** add `InternalsVisibleTo("FishGame.Tests.PlayMode")` to `UI/AssemblyInfo.cs` and test `UiSessionActions` with presenters bound to the real `AppRoot` document. That fallback is more fragile and still touches a non-T2 file. |
| CCR-A4 | 7.2 (wording) | `Truncate(clean, maxChars)` stays the single truncation primitive, as the contract names it. Both callers fit a **UTF-8 byte** budget (`FixedString32Bytes` = 29 bytes), not a char budget, so T2 keeps the byte loop in `SessionText.FitBytes`, makes each step of that loop call `DisplayNameRules.Truncate(s, s.Length - 1)`, and makes `NameWire.ToFixed` delegate to `SessionText.Name`. Record the one behaviour change: `SessionText.Name` now drops lone surrogates, as `NameWire` always did. Before, `SessionText.Name` let them through, where they encode as U+FFFD. | A char-count helper cannot serve a byte budget by itself. This keeps the contract's public surface unchanged, with no new public name. |
| CCR-A5 | 2.6 row `Tests/EditMode/{...}`, 12 rows T1 and T3 | Add the test files that T1 and T3 must edit but that 2.6 does not list. **T1:** `Tests/EditMode/FishingHostLedgerTests.cs` (positive control; T13 rewrites the file later and must keep it), `Tests/EditMode/ShaderValidatorTests.cs`, `Tests/PlayMode/FishingSoloTests.cs`. **T3:** `Tests/EditMode/{FishingDefsTests,FishingValidatorTests,FishingContentBuilderTests,ContentAssetParityTests,FishFallbackMeshTests}.cs` and `Tests/PlayMode/FeedbackSoloTests.cs`. Each is listed in T3.10 with its fix. | C 12 says "the named test files" (T1) and "tests" (T3). The 2.6 table names only `FishingContentTests` for T3. These files break or need the new fields once the migration runs. |
| CCR-A6 | 7.3 (`minLandDepth` sentence) | Read Q1's default as a **spec value change**. The seed becomes 0.5, `FishingData_SeedsMatchSpecTables` expects 0.5, and there is no `TunedDeviations` entry. The controller writes the spec 9.3/9.12 delta (M3-D52). If the user instead keeps 0.8 (lake-bed alternative), T3 skips its tuning write and nothing changes. | The C 7.3 sentence ("recorded in `TunedDeviations` only if the user keeps 0.8") can be read two ways. |
| CCR-A7 | 7.3 (`Configure` notes) | The new `Configure` parameters are **optional trailing parameters**. `FishSpeciesDef.Configure(..., Mesh model, FishTags tags = FishTags.None)` and `BaitDef.Configure(..., bool unlimited, FishTags affinityTags = FishTags.None, float affinityMult = 1f, float specialMult = 1f)`. The two bait multiplier fields get the field initializer `= 1f`. The builder and the migration always pass explicit values. | Existing callers keep compiling unchanged. About 20 in-memory test call sites stay untouched, and T13 changes the same signatures again anyway. A default of 1, never 0, cannot silently disable specials (B4). The order stays "appended at the end", as the contract asks. |

**Cross-section notes (for the controller to pass on; no contract change):**
- **Section C (T10):** the `bluegill` row in `EcologySeedData` must equal what T3 writes: 200-600 g, value 8, Common, `steady`, tags `Schooling`, weightSkew 2.2, lengthShapeK 1.0, biteWaitMod 1.0, hookWindow 0, Round, **colours kept from M2** (A `(0.32, 0.50, 0.62, 1)`, B `(0.95, 0.62, 0.22, 1)`), model file `Fish_Bluegill.fbx`, `minBaitRating` 0, `minDepth` 0. Create-if-missing will never fix a mismatch, so a different row would fail `EcologyData_AssetValuesEqualSeeds`.
- **Section C (T10):** once Long-bodied species exist again, `FishFallbackMeshTests` may go back to a Long species for its shape check. T3 drops that check to a colour-only assert for the species case (T3.10).
- **Section D (T13):** keep T1's positive control in the ledger zero-alloc test when `FishingHostLedgerTests` is rewritten against the resolver.
- **Section D (T15):** the map switch relies on T1's F6/F7 rule. Overrides survive a Spot load and clear on `Ended`.

---

## 0. Shared notes for all three tasks

### 0.1 Read first
Every implementer reads:
- `CLAUDE.md`;
- C 2.1 (commits), C 2.6 (shared edits, with the CCRs above), C 2.8 (global constraints, all of them) and C 14;
- C2 16.4 (mutation rule, tool discipline);
- the task below.

The constraints that bite in each task are repeated there.

### 0.2 Tool discipline (C2 16.4)
- Run `Tools/unity.ps1` only through the **PowerShell tool**, never through Bash.
- Create and edit files only with the **Write/Edit tools**. No heredocs, no sed, no Python splicing.
- When a run is expected to be red, say "expected red" in its description.
- **C 2.8 #2:** the Write tool may decode `\uXXXX` in C# source. Never write `\u` escapes in new code. Build test strings with `char.ConvertFromUtf32(0x1F41F)` or `((char)0xD83D).ToString()`, then byte-scan every touched file (0.6).

### 0.3 Commands
The PowerShell tool does not keep variables between calls, so every command line sets `FG_OUT` itself. `<scratch>` is your session scratchpad (absolute path, outside the repo).

```powershell
$env:FG_OUT = '<scratch>\m3-tN'; New-Item -ItemType Directory -Force $env:FG_OUT | Out-Null; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 <compile|build|test-edit|test-play|check> [-Filter "<regex>"] [-TimeoutMinutes 15]
```

- Result lines are `FG RESULT ... PASS|FAIL|LOCKED|TIMEOUT`. Exit codes: 0 pass, 1 fail, 2 locked, 3 timeout, 4 usage.
- **Stop only on exit 2 (LOCKED).** Unity often leaves `Temp/UnityLockfile` behind after a batch run. A file that is merely present is not a lock. LOCKED means held, and the script tests that. When LOCKED, ask the user to close the editor. Never kill Unity and never delete the lockfile.
- Filtered `compile` and `test-edit` runs go in the **foreground** (timeout 600000 ms).
- Full `test-edit`, `test-play` and `check` go in the **background** (`run_in_background`), then wait for the notification. A full run can exceed the 10-minute foreground limit.
- `check` runs build -> test-edit -> test-play and stops at the first failure.
- Every task ends with the full `test-edit` and the full `test-play` (skeleton 7: EditMode and PlayMode stay green after T1-T2; C 2.4).

### 0.4 Scene churn (C 2.8 #6)
`build` renumbers fileIDs in every `.unity` file, and none of T1-T3 changes a scene.
- After each `build`/`check`, run `git restore Assets/_Project/Scenes/*.unity`.
- Delete a stray `Assets/InitTestScene*.unity` or an untracked `dev/null/` folder if an interrupted run left one (rework handoff 5).
- T1-T3 run before T14, so no Spot scene holds an in-scene NetworkObject yet, and restoring is safe (C 2.8 #24 does not apply yet).

### 0.5 Commits
- Explicit paths only, including every new `.meta`. Never `git add -A` or `git add .`.
- Before committing, `git status --porcelain` must show nothing under `Library/ Temp/ Logs/ UserSettings/` and no `*.csproj` or `*.slnx`.
- Message (C 2.1): `m3(<area>): <summary>` with the trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (the model that wrote the change).
- Areas: `carry` for T1/T2, `migration` for T3.

### 0.6 Byte scan
In Git Bash: `LC_ALL=C grep -nP '[^\x00-\x7F]' <touched files>` must print nothing (no BOM, no decoded escapes). Without `LC_ALL=C` it gives false positives.

### 0.7 Mutation rule (C2 16.4)
- Every test added names, in its `// Catches:` comment, the plausible wrong implementation it catches.
- For the key orders and formulas (T1 marker, T2 truncation, T3 migration steps), the implementer breaks the code once, shows the red run in the report, then restores it.
- A test that only asserts "no exception", or compares the same code run twice, does not count.

---

## Task T1 - Carry: fishing minors (C2 16.1 final row)

**Goal:** close the five M2 carry items:
- the drawn cast marker is never a frame behind the machine;
- F6/F7 survive a Spot scene load and clear when the session ends;
- the parity test comment is honest;
- the zero-alloc test has a positive control;
- three `ShaderValidator` cases are added;
- the HUD creel lookup uses an explicit null check;
- the `0.625 x reel` comment is fixed.

- **Owned files (C 12):** `Fishing/Owner/FishingRodController.cs`, `Fishing/Host/FishingHost.cs`, `Fishing/Debug/FishingDebugSettings.cs`, `Core/Fishing/{CatchValidator,FishingConstants}.cs` (comments only), `UI/Fishing/FishingHudPresenter.cs` (null check).
- **Tests owned:** `Tests/PlayMode/FishingSoloTests.cs` (add 2), `Tests/EditMode/FishingHostLedgerTests.cs` (positive control), `Tests/EditMode/ShaderValidatorTests.cs` (3 cases) (CCR-A5).
- **Shared edits (C 2.6):**
  - `FishingRodController.cs`: T1, later T15 (owner M3 gates). Keep the marker edit local to `Update`/`BuildFrame`/the marker methods.
  - `FishingHost.cs`: T1, then T13, then T15. Keep the edit to `OnEnable`/`OnDisable`.
  - `FishingDebugSettings.cs`: T1, then T15 (`Luck`).
- **Depends on:** entry gate (C 1.2 #1-#3).
- **Implementer:** Sonnet. **Reviewer:** Sonnet.

### T1.1 Constraints that bite here (C 2.8)
- **#26: owner and host run the same cast gates.** The marker change must not change which probe validates a cast (T1.3). Owner landing, validated marker and host `ActiveCast` landing stay bit-identical. `SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast` is the guard.
- **#9:** PlayMode is warning-free and fails on an unexpected `LogError`.
- **#1:** no `DEVELOPMENT_BUILD` and no `#if DEBUG`.
- **#17:** Presentation is read-only. `CastMarkerView` (order 270, `LateUpdate`) only reads the controller's properties.
- **#24, #27, #28:** not touched by this task (no NetworkObject, no material, no fish mesh).

### T1.2 Current code (facts, by symbol)
- `FishingRodController.Update`: `BuildFrame(...)` -> `Machine.Tick(dt, in frame)` -> `ProcessEffects()` -> 50 Hz `TickFight` loop -> `PublishView()`.
  - `BuildFrame` sets `f.MarkerValid = UpdateMarker()`.
  - `UpdateMarker()` writes the public `MarkerVisible`, `MarkerValid` and `MarkerPosition` from `Machine.MarkerOn`, `Machine.ChargeNorm` and the current transform.
  - So the published marker shows the **pre-tick** charge: one frame behind the charge the machine holds after `Tick`, and still visible on the release frame.
- `RodStateMachine.TickCharging` (M-4, `e484abb`): the release frame does not advance the charge, and a release casts only if `f.MarkerValid` (the pre-tick probe at this frame's pose).
  - That probe is the cast gate and must stay pre-tick and at this frame's transform. `PlayerLocomotion` (order 0) moves the player after the controller (order -100), so a probe cached from last frame would be at a stale position.
- `FishingHost.OnDisable` sets `FishingDebugSettings.ForcedBiteWait = 0f` and `TwitchyHook = false` (final review M-5). It runs on every Spot unload.
- `FishingDebugSettings.ResetStatics` (`SubsystemRegistration`) clears all four overrides.
- `GameScope` (App) is per scene. `ServiceRoots.Game` is cleared in its `OnDestroy`, so it is unsuitable as a session-end signal (CCR-A1).
- `INetworkSession` lives in Runtime, which Fishing references. `Ended` fires exactly once per attempt, after the Offline `StateChanged`.
- Comment of `FishingSoloTests.SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast`: lists "a stale charge (M-4)" among what it catches. It cannot catch that: owner and host both derive from the same `Machine.CastDistance`. M-4 is covered by the EditMode `RodStateMachineTests` cases tagged "M-4".
- `SoloHost_LeavingTheSession_ClearsF6AndF7`: its comment says `FishingHost.OnDisable` clears them.
- `FishingHostLedgerTests.FishingHostLedger_Tick_OrderSurvivesDisconnectAndRejoin_AndDoesNotAllocate` measures with `System.GC.GetAllocatedBytesForCurrentThread()` and asserts `== 0` with no proof that the counter works on Unity's Mono.
- `ShaderValidatorTests.CountSubShaders_Cases` has 11 `TestCase`s.
  - `ShaderValidator.CountSubShaders` skips `//`, `/* */` and string literals.
  - An unterminated `/*` swallows the rest (`i = n`).
  - A string literal ends at a newline.
  - `SubShaderKeyword` is `(?<![A-Za-z0-9_])SubShader\s*\{` with `RegexOptions.IgnoreCase`.
- `FishingHudPresenter.ResolveLocals`:
  - The condition is `!ReferenceEquals(_creelOwner, owner) || (_creel == null && owner != null && (!_creelLookedUp || !ReferenceEquals(_creel, null)))`, and the lookup is `owner.GetComponent<PlayerCreel>()`.
  - In the editor, `GetComponent` for a missing component returns Unity's fake-null object, not a real null. `!ReferenceEquals(_creel, null)` is then true, so a player without a creel is looked up again on every Tick in the editor. That is the polish-review "fake-null" item.
- `CatchValidator.LandedFloorSeconds` and `FishingConstants.LandedFloorFactor` carry a comment "(delta <= 0.625 * reel distance)" (`9ecb85f`). The number was never derived in any doc, and R72 changes the floor's speed to `CatchValidator.MaxReelSpeed`.

### T1.3 Marker after the machine tick (the tricky part)
**Rule:** the cast gate keeps its pre-tick probe, and the published (drawn) marker is refreshed after the tick. Two probes per frame while Ready or Charging: one feeds `RodFrame.MarkerValid` (the gate), and one writes the public properties.

```csharp
// BuildFrame: the gate. Probes at THIS frame's pose and the PRE-tick charge, writes no public property (M-4: marker == cast == host landing).
f.MarkerValid = ProbeMarker(out _);

// Update, after the fight-step loop and before PublishView():
RefreshDrawnMarker();   // C2 16.1 carry: the drawn marker shows the post-tick charge (and hides on the release frame), never a frame behind

// Pure probe: same maths as the old UpdateMarker; no field writes.
private bool ProbeMarker(out Vector3 position)
{
    position = Vector3.zero;
    if (!(Machine.MarkerOn && RodOut)) return false;
    float charge = Machine.State == RodState.Charging ? Machine.ChargeNorm : 0f;
    float dist = CastMath.Distance(charge, _tuning.CastMinDist, Rod.CastMaxDist);
    Vector3 root = transform.position;
    CastMath.Landing(root.x, root.z, transform.eulerAngles.y, dist, out float lx, out float lz);
    WaterProbe probe = CastProbe.Probe(lx, lz);
    position = new Vector3(lx, probe.FirstHitIsWater ? probe.WaterY : WaterSurface.HeightAt(lx, lz), lz);
    return CastMath.IsValidLanding(in probe, _tuning.MinLandDepth);
}

// The only writer of MarkerVisible / MarkerValid / MarkerPosition besides OnDisable.
private void RefreshDrawnMarker()
{
    MarkerVisible = Machine.MarkerOn && RodOut;
    MarkerValid = ProbeMarker(out Vector3 p);   // false when the marker is off
    if (MarkerVisible) MarkerPosition = p;
}
```

- Delete `UpdateMarker()`. Its comment (spec 9.3, R67) moves onto `ProbeMarker`.
- `OnDisable` still clears `MarkerVisible` and `MarkerValid`. A paused frame still refreshes, and the machine is frozen, so the marker holds still.
- Do not reorder anything else in `Update`.
- T15 later adds the M3 gates (grid water, casting open) inside `ProbeMarker`, so gate and drawn marker share them (C 2.8 #26). Leave a one-line comment there: `// T15: M3 owner gates go here (C 2.8 #26)`.

### T1.4 F6/F7: cleared at session end, kept across Spot loads (CCR-A1)

```csharp
// FishingDebugSettings (add; internal, no new public surface - C 7.1)
private static INetworkSession s_session;

// F6/F7 are SESSION toggles: cleared when the session ends (INetworkSession.Ended, once per attempt), never when a Spot scene unloads,
// so the M3 dev map switch keeps them (C 7.1, CCR-A1). Idempotent; re-hooks when the app's session instance changes (a new App in tests).
internal static void ClearOnSessionEnd(INetworkSession session)
{
    if (session == null || ReferenceEquals(s_session, session)) return;
    if (s_session != null) s_session.Ended -= OnSessionEnded;
    s_session = session;
    session.Ended += OnSessionEnded;
}

private static void OnSessionEnded(EndReason reason)
{
    ForcedBiteWait = 0f;
    TwitchyHook = false;
}
// ResetStatics additionally: if (s_session != null) s_session.Ended -= OnSessionEnded; s_session = null;
```

- `FishingDebugSettings.cs` gains `using FishGame.Runtime;` (for `INetworkSession`; `EndReason` is in Core).
- `FishingHost.OnEnable`, after `s_current = this;`, adds: `ServiceRegistry app = ServiceRoots.App; if (app != null && app.TryGet(out INetworkSession session)) FishingDebugSettings.ClearOnSessionEnd(session);`.
- `FishingHost.OnDisable`: delete the two reset lines and their M-5 comment, and replace them with `// F6/F7 clear on INetworkSession.Ended (FishingDebugSettings.ClearOnSessionEnd), not here: a Spot load must keep them (M3 map switch).`
- Update the file header of `FishingDebugSettings`: "F6/F7 reset when the session ends and on SubsystemRegistration; ForcedSpeciesId (cycled by F10 since the M2 final review) and ForceJunk only on SubsystemRegistration". `ForcedSpeciesId` and `ForceJunk` keep their M2 behaviour.
- Fix the comment of `SoloHost_LeavingTheSession_ClearsF6AndF7`: "`INetworkSession.Ended` (the session ending) clears both". The test body is unchanged and still passes, because `TearDownApp` leaves first.

### T1.5 Small fixes
- **Parity comment** (`SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast`): replace "a stale charge (M-4)" with "(a release-frame charge advance, M-4, is not visible here: owner and host derive from the same `Machine.CastDistance`; `RodStateMachineTests` M-4 cases cover it)".
- **HUD creel lookup** (`FishingHudPresenter.ResolveLocals`). Behaviour stays identical except that the editor no longer looks up every Tick:

```csharp
var owner = PlayerNetState.Local;
bool ownerChanged = !ReferenceEquals(_creelOwner, owner);
bool neverFound = ReferenceEquals(_creel, null) && !_creelLookedUp;   // a missing creel is looked up at most once per owner
bool destroyed = !ReferenceEquals(_creel, null) && _creel == null;    // a Unity-destroyed component: look it up again
if (ownerChanged || (owner != null && (neverFound || destroyed)))
{
    PlayerCreel creel = null;
    if (owner != null) owner.TryGetComponent(out creel);   // a real null when missing, never the editor's fake-null object
    SetCreel(owner, creel);
}
```
  Replace the old comment line with one sentence saying why `TryGetComponent` is used. `SetCreel` is unchanged.
- **`0.625` comments:**
  - Run `grep -n "0.625" Assets/_Project/Core/Fishing/*.cs`.
  - Rewrite each hit as: `// Lag when the player walks toward the bobber is absorbed by the 0.8 factor plus the 0.5 s slack; the speed is CatchValidator.MaxReelSpeed(rod, profile, tuning) for a fish (R72) and JunkDragSpeed for junk. Do not shave either constant.` Use the shorter form on the `FishingConstants` line.
  - If R72 already removed the comment, change nothing and say so in the report.
  - After the edit, the grep must print nothing.

### T1.6 Steps (TDD)
1. Add the tests in T1.7: two PlayMode tests, the positive control and three `TestCase`s.
2. Expected red: `test-play -Filter "FishGame.Tests.PlayMode.FishingSoloTests" -TimeoutMinutes 15`, in the background.
   - `FishingSolo_CastMarker_UpdatedAfterMachineTickSameFrame` fails.
   - `FishingSolo_SceneReload_KeepsDevOverrides_SessionEndClears` fails at "F6 survives the Spot reload" (the old `OnDisable` reset).
3. `test-edit -Filter "FishGame.Tests.EditMode.(ShaderValidatorTests|FishingHostLedgerTests)"`.
   - Expected green: the new shader cases pin current behaviour.
   - The positive control **must pass**. If it fails, the counter is inert on this runtime, so apply T1.7's fallback and report it.
4. Implement T1.3 and T1.4, then T1.5.
5. Green: the step 2 filter.
6. Mutation, shown red once each:
   - put back the single pre-tick `UpdateMarker` write;
   - put back the `OnDisable` reset;
   - in the positive control, make the loop write to a local the JIT can drop (the control must go red, or it proves nothing).
7. Full `test-edit` and full `test-play` (background, `-TimeoutMinutes 15`). Restore the scenes (0.4). Byte scan (0.6). Commit (T1.10).

### T1.7 Tests (exact names; cases)
**`FishingSoloTests.FishingSolo_CastMarker_UpdatedAfterMachineTickSameFrame`** (required, C 10.1)
- **Setup:** `Arrange` (G9 teleport), then `EquipAndReady`. Set `Time.captureFramerate = 30` inside `try`, so each frame's charge step is 1/30 and the old one-frame lag is about 0.5 m on Handmade (18 - 2.5 m x 1/30). Restore it to 0 in `finally`.
- **Precondition:** record `p0 = rig.Player.transform.position` and `yaw = rig.Player.transform.eulerAngles.y`.
- **Charge window:**
  - Step `{SecondaryHeld, PrimaryPressed, PrimaryHeld}`, then 8 Steps of `{SecondaryHeld, PrimaryHeld}`.
  - After each Step (the coroutine resumes after every `Update` of that frame), read `c = rig.Machine.ChargeNorm` and the previous frame's `cPrev`.
  - Compute `L(c)` with `CastMath.Distance(c, rig.Registry.Fishing.Tuning.CastMinDist, rig.Controller.Rod.CastMaxDist)` and `CastMath.Landing(p0.x, p0.z, yaw, dist, ...)`.
  - Assert `rig.Controller.MarkerVisible`, and `Flat(MarkerPosition, L(c)) < 1e-3` ("the drawn marker uses the post-tick charge").
  - From the second sample on, also assert `Flat(L(c), L(cPrev)) > 0.1` (the window can tell the two apart).
- **Precondition check:** the player moved less than 1e-3 m over the window. If not, fail with "precondition: the player must stand still".
- **Release frame:** `Release(rig)`. On that same resumed frame, `rig.Machine.State == Casting` and `rig.Controller.MarkerVisible == false` ("the marker hides on the release frame").
- `rig.Log.AssertEmpty("marker after tick")`.
- **Catches:** the drawn marker written before `Machine.Tick` (one frame behind, still shown on the release frame). It also catches a fix that moved the whole probe after the tick: `SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast` and the `RodViewSoloTests` marker-follow asserts stay as guards for the gate side.

**`FishingSoloTests.FishingSolo_SceneReload_KeepsDevOverrides_SessionEndClears`** (required, C 10.1)
1. `Arrange(rig, teleport: false)`. Record `oldHost = FishingHost.Current`, `oldGame = ServiceRoots.Game` and `oldPlayer = PlayerNetState.Local`.
2. Set `FishingDebugSettings.ForcedBiteWait = 0.1f` and `TwitchyHook = true`, as F6/F7 do mid-session. Call `rig.Dispose()`; the rig's references die with the scene.
3. **Reload (primary path):** `NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.SpotLakeDock, LoadSceneMode.Single)`. Assert the status is `Started`. Then `WaitUntil` (20 s) for:
   - `FishingHost.Current != null && !ReferenceEquals(FishingHost.Current, oldHost)`;
   - `ServiceRoots.Game != null && !ReferenceEquals(ServiceRoots.Game, oldGame)`, which proves a real Spot reload and the `GameScope` teardown;
   - `PlayerNetState.Local != null && !ReferenceEquals(PlayerNetState.Local, oldPlayer)`.
4. Assert `ForcedBiteWait == 0.1f` ("F6 survives the Spot reload") and `TwitchyHook` ("F7 survives the Spot reload").
5. **Session end:** `Task leave = AppBootstrap.Current.Services.Get<INetworkSession>().LeaveAsync()`, then `PlayModeHarness.Await(leave, 20f)`, then `WaitUntil(session.State == Offline && active scene == Menu, 20f)`. This is a plain leave, not `TearDownApp`, so the clear is proven to come from the session ending and not from destroying the App.
6. Assert `ForcedBiteWait == 0f` and `TwitchyHook == false` ("cleared at session end").
7. The capture from `new PlayModeHarness.LogCapture()`, opened before step 3, `AssertNoWarnings("scene reload")`.

- **Catches:**
  - the old reset in `FishingHost.OnDisable` (step 4 fails);
  - a reset hooked to `GameScope`/`ServiceRoots.Game` teardown (step 4 fails);
  - a reset only on `SubsystemRegistration` or App destroy (step 6 fails).
- **Fallback (allowed without asking; record it as a deviation in the report):** if the NGO single-load of the already-active `Spot_LakeDock` logs warnings or errors from code outside T1's files, replace step 3 with `FishingHost.Current.gameObject.SetActive(false)` then `SetActive(true)` on the `Fishing` root. That runs exactly `FishingHost.OnDisable`/`OnEnable`, the same mutation for the first catch. Name the GameScope case as not covered (T24's `EcologySolo_DevMapSwitch_KeepsRunStateAndCastCounter` then covers the real switch), and paste the reload's log lines into the report as a T15 risk.

**Positive control in `FishingHostLedger_Tick_OrderSurvivesDisconnectAndRejoin_AndDoesNotAllocate`** (C 10.1 "positive control"; the test name is unchanged)
Right after the `allocated == 0` assert, add:
```csharp
// Positive control (C2 16.1 carry): the same counter must see 100 small array allocations, or "allocated == 0" above proves nothing here.
long controlBefore = System.GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 100; i++) s_allocSink = new object[4];   // a static sink the JIT cannot drop
long controlAllocated = System.GC.GetAllocatedBytesForCurrentThread() - controlBefore;
Assert.That(controlAllocated, Is.GreaterThan(0L), "positive control: the allocation counter must detect allocations on this runtime");
```
- Add `private static object s_allocSink;` to the class.
- **Fallback if the control reads 0** (the counter is inert on Unity's Mono): replace both measurements with Unity Test Framework constraints (`using UnityEngine.TestTools.Constraints;`, aliasing `Is` as needed):
  - `Assert.That(() => { for (int i = 0; i < 100; i++) l.Tick(0.01, false, sink); }, Is.Not.AllocatingGCMemory())`;
  - the control `Assert.That(() => { s_allocSink = new object[4]; }, Is.AllocatingGCMemory())`.
  - Report which probe is in use. Never ship a control that cannot fail.
- **Catches:** an inert counter (an always-zero probe passes "no allocation" for any code).

**`ShaderValidatorTests.CountSubShaders_Cases`**, three new `TestCase`s (names chosen here, as C 10.1 asks; all pass on the current scanner and pin it):
- `[TestCase("Shader \"A\" { SubShader { } /* SubShader { }", 1, TestName = "unterminated block comment swallows the rest")]`. Catches a scanner that resumes after an unterminated `/*` (for example `IndexOf` returning -1 treated as `i + 2`), which counts 2.
- `[TestCase("Shader \"A\" { subshader { } }", 1, TestName = "keyword is case-insensitive")]`. Catches dropping `RegexOptions.IgnoreCase`. ShaderLab keywords are case-insensitive, so a lowercase second block would slip past the "exactly 1" rule.
- `[TestCase("Shader \"A\" { SubShader\n\t{ } }", 1, TestName = "newline and tab before the brace")]`. Catches a regex narrowed to `SubShader ?\{`, which counts 0.

The C# string literals above contain `\n` and `\t` escapes, never `\u`.

### T1.8 Not tested automatically
- The HUD lookup is UI (EditMode cannot reach it; the reflection exceptions are capped at two, C 4). The reviewer checks it by reading against the truth table in T1.5.
- The comment edits.

### T1.9 Acceptance (reviewer)
- `RodFrame.MarkerValid` still comes from a probe in `BuildFrame` at the frame's pose and pre-tick charge, and `ProbeMarker` writes no field.
- The public marker properties are written only in `RefreshDrawnMarker` (after the fight loop, before `PublishView`) and in `OnDisable`.
- `FishingHost.OnDisable` no longer touches `FishingDebugSettings`.
- `ClearOnSessionEnd` is idempotent, unhooks the old session, and is reset in `ResetStatics`. Only F6/F7 clear on `Ended`.
- The positive control can fail (shown red in the report).
- The three shader cases are present. `grep 0.625` over `Core/Fishing` is empty. The HUD condition matches T1.5 and uses `TryGetComponent`.
- Full EditMode and PlayMode green, scenes restored, byte scan clean.

### T1.10 Commit
```powershell
git add Assets/_Project/Fishing/Owner/FishingRodController.cs Assets/_Project/Fishing/Host/FishingHost.cs Assets/_Project/Fishing/Debug/FishingDebugSettings.cs Assets/_Project/Core/Fishing/CatchValidator.cs Assets/_Project/Core/Fishing/FishingConstants.cs Assets/_Project/UI/Fishing/FishingHudPresenter.cs Assets/_Project/Tests/PlayMode/FishingSoloTests.cs Assets/_Project/Tests/EditMode/FishingHostLedgerTests.cs Assets/_Project/Tests/EditMode/ShaderValidatorTests.cs; git commit -m "m3(carry): drawn cast marker after the machine tick; F6/F7 clear on session end, kept across Spot loads; zero-alloc positive control; shader scanner cases; HUD creel lookup without fake-null; landed-floor comment" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
Leave out any path that did not change (for example `FishingConstants.cs` if R72 already removed the comment).

---

## Task T2 - Carry: content hash guard, one name truncation, online cancel feedback

**Goal:**
- `ContentHash.Compute` rejects a repeated category;
- `DisplayNameRules.Truncate` is the single truncation primitive, used by `Sanitize`, `SessionText` and (through `SessionText`) `NameWire`;
- an online Cancel shows "Cancelling..." at once and returns to the main menu as soon as the session reports the cancel, never waiting up to 30 s on the SDK.

- **Owned files (C 12):** `Core/Hashing/ContentHash.cs`, `Core/Net/DisplayNameRules.cs`, `Gameplay/Player/NameWire.cs`, `Gameplay/Session/SessionText.cs`, `UI/UiSessionActions.cs`, `UI/UiStrings.cs`. Added by accepted CCRs (CCR accepted 2026-10-12): `Gameplay/AssemblyInfo.cs` (CCR-A2) and `App/AppBootstrap.cs` (CCR-A3).
- **Tests owned:** `Tests/EditMode/ContentHashTests.cs` (add), `Tests/EditMode/DisplayNameRulesTests.cs` (add), `Tests/PlayMode/UiFlowCancelTests.cs` (new).
- **Shared edits (C 2.6):**
  - `UiStrings.cs`: T2 adds `busy.cancelling`; T21 and T22 add their keys later.
  - `AppBootstrap.cs`: T2 seam, then T20's `SaveService` hook.
  - `Gameplay/AssemblyInfo.cs`: T2 now; T14 finds the line present.
  - `UiFlowController.cs` is **not** edited. Every change lives in `UiSessionActions`.
- **Depends on:** entry gate. Disjoint from T1.
- **Implementer:** Sonnet. **Reviewer:** Opus high (cancel flow ordering).

### T2.1 Constraints that bite here (C 2.8)
- **#3 cancel convention unchanged:** any cancel, including an already-cancelled token, faults with `NetworkSessionException(EndReason.UserLeft)` before any state change. T2 changes only what the UI shows while waiting.
- **#2:** ASCII only, and no `\u` escapes in new code (0.2). New UiStrings text: `"Cancelling..."` (three ASCII dots).
- **#9:** PlayMode is warning-free. **#1:** no `#if DEBUG`.
- `UiSessionActions` keeps its authority comment ("client-only (local UI)").

### T2.2 Current code (facts)
- `ContentHash.Compute` sorts the categories ordinally and hashes each in turn, with no duplicate check (B6).
- `DisplayNameRules.Sanitize` truncates to `MaxLength` chars inline, with its own surrogate-pair guard.
- `NameWire.ToFixed`:
  - drops lone surrogates;
  - then `FixedString32Bytes.CopyFromTruncated` cuts at a code-point boundary within 29 bytes.
- `SessionText.FitBytes(text, maxBytes)`:
  - trims one char at a time with its own pair guard until the UTF-8 byte count fits;
  - `Name` and `Id` wrap it.
  - `Name` does **not** drop lone surrogates, so the two paths can disagree on malformed text.
- `UiSessionActions.CancelAttempt`:
  - with an attempt, it calls `_attempt.Cancel()` and nothing else;
  - otherwise, if not Offline, it calls `LeaveSession()`.
  - The Busy screen stays until `StartAttempt`'s `await` returns.
  - On UGS that return waits for `EndCoreAsync` -> `TeardownAsync`, which waits up to `AsyncTimeoutSeconds` (30 s) for the in-flight SDK call.
- Before the wait, `EndCoreAsync` fires `Fail` (Connecting -> Failed) and `Leave` (Failed -> ShuttingDown) synchronously. Those `StateChanged` events are "the session reports the cancel". `Ended` comes only after the teardown.
- `UiFlowController`:
  - `OnSessionStateChanged` sets menu actions to `current == Offline && !HasAttempt`;
  - `Show(MainMenu)` does the same;
  - `OnSessionEnded` returns to MainMenu if Busy.
  - `CompositeNetworkSession` creates its online backend lazily through the factory passed by `AppBootstrap.Awake`, and forwards its events through `EventIsolation`.

### T2.3 ContentHash
- In `Compute`, after sorting, `for c >= 1: if (StringComparer.Ordinal.Equals(sorted[c].Category, sorted[c - 1].Category)) throw new ArgumentException("duplicate content category '" + sorted[c].Category + "'", nameof(categories));`.
- Ordinal, so `"Species"` and `"species"` are distinct.
- The throw happens before any hashing. The null-`Ids` check keeps its place.

### T2.4 One truncation (CCR-A4)
```csharp
// DisplayNameRules (add). The single truncation primitive: cut to at most maxChars UTF-16 chars, never between a high and a low surrogate
// (the cut moves one char left instead). Does not clean: callers pass text that already went through their own cleaning.
public static string Truncate(string clean, int maxChars)
{
    if (maxChars < 0) throw new ArgumentOutOfRangeException(nameof(maxChars));
    if (string.IsNullOrEmpty(clean) || maxChars == 0) return string.Empty;
    if (clean.Length <= maxChars) return clean;
    int cut = maxChars;
    if (char.IsHighSurrogate(clean[cut - 1]) && char.IsLowSurrogate(clean[cut])) cut--;
    return clean.Substring(0, cut);
}
```

- **`Sanitize`:** the inline cut block becomes `if (s.Length > MaxLength) s = Truncate(s, MaxLength).TrimEnd();`. The behaviour is identical, and the existing `DisplayNameRules_Sanitize_*` tests prove it.
- **`SessionText.FitBytes`:** the loop body becomes `s = DisplayNameRules.Truncate(s, s.Length - 1);` and the old inline pair guard is deleted. The byte loop stays.
- **`SessionText.Name(text)`:** first drops lone surrogates (the cleaning moved from `NameWire`, as a private helper `DropLoneSurrogates`), then `new FixedString32Bytes(FitBytes(cleaned, NameCapacityBytes))`. `Id` is unchanged apart from the shared loop.
- **`NameWire.ToFixed(text)`:** `return SessionText.Name(text);`. Its class comment points at `SessionText` as the one place that fits names to the wire.
- Update both class comments.

### T2.5 Online cancel feedback (the tricky part: ordering)
Add to `UiSessionActions` (all private except what is shown):

```csharp
private bool _cancelPending;   // a Cancel was pressed for the current attempt and the session has not reported it yet

internal void CancelAttempt()
{
    if (_attempt != null)
    {
        if (_cancelPending) return;                                  // a second press while cancelling: nothing more to do
        _cancelPending = true;
        _busyPresenter.SetMessage(UiStrings.Get("busy.cancelling"));   // at once, before the SDK returns (M2 handoff item)
        _session.StateChanged += OnStateAfterCancel;                 // subscribe BEFORE Cancel: LocalNetworkSession may report inside Cancel()
        _attempt.Cancel();
        if (_cancelPending && CancelReported(_session.State)) ReturnToMenuAfterCancel();
        return;
    }
    if (_session.State != ConnState.Offline)                         // connected, waiting for the scene: Cancel leaves (M1)
    {
        _busyPresenter.SetMessage(UiStrings.Get("busy.cancelling"));
        LeaveSession();
    }
}

// The session has acted on the cancel once it leaves Connecting/Connected (UGS: Failed then ShuttingDown fire before its up-to-30 s teardown).
private static bool CancelReported(ConnState s) => s == ConnState.Failed || s == ConnState.ShuttingDown || s == ConnState.Offline;

private void OnStateAfterCancel(ConnState previous, ConnState current)
{
    if (CancelReported(current)) ReturnToMenuAfterCancel();
}

private void ReturnToMenuAfterCancel()
{
    if (!_cancelPending) return;
    _cancelPending = false;
    _session.StateChanged -= OnStateAfterCancel;
    if (_isLive() && _currentScreen() == UiScreen.Busy) _show(UiScreen.MainMenu);   // menu actions stay off: HasAttempt is still true
}
```

- In `StartAttempt`'s `finally`, before clearing `_attempt`, call `ReturnToMenuAfterCancel()` if `_cancelPending` is still set. That covers an attempt that ended without a reporting state change (for example the D8 already-cancelled throw). The existing tail then re-enables the menu (`_setMenuActions(Offline, true)`) once the attempt really ended. Nothing else in `StartAttempt` changes.
- Ordering facts the reviewer checks:
  - `UiFlowController.OnSessionStateChanged` is subscribed in `Initialize`, before this handler, so it runs first and disables the menu actions.
  - Our handler then shows MainMenu, and `Show(MainMenu)` keeps the actions disabled because `_actions.HasAttempt` is true.
  - When the attempt finally completes, `StartAttempt`'s tail finds `_currentScreen() == MainMenu` and enables the actions if Offline.
  - `OnSessionEnded` finds MainMenu, not Busy, and does nothing.
  - The temporary handler is removed on first use or at the attempt's end, so it never leaks across attempts.
- Do not touch `HostAsync`, `OnJoinRequested`, `LeaveSession` or `ToggleJoins`.
- `UiStrings`: add `["busy.cancelling"] = "Cancelling...",` after `busy.cancel`.
- **Kept on purpose (M1 behaviour):** if the cancel loses the race to `Connected` (Established fired just before), the attempt succeeds and the game loads. Open question A-Q3.

**`AppBootstrap` seam (CCR-A3):**
- `public static Func<INetworkSession> OnlineSessionFactoryOverride { get; set; }` with the comment `// Test seam (PlayMode UiFlowCancelTests): null in production; read lazily when the first online attempt creates the backend.`
- `ResetStatics` sets it to null.
- The factory lambda becomes `() => OnlineSessionFactoryOverride != null ? OnlineSessionFactoryOverride() : new UgsNetworkSession(sessionConfig)`.
- Nothing else changes.

### T2.6 Steps (TDD)
1. Apply the CCR-A2 line in `Gameplay/AssemblyInfo.cs` (CCR accepted 2026-10-12; it is needed for compile).
2. Add the tests in T2.7.
3. Expected red: `test-edit -Filter "FishGame.Tests.EditMode.(ContentHashTests|DisplayNameRulesTests)"`. The result is a compile failure (`CS0117` `Truncate`). After stubbing `Truncate` to `throw new NotImplementedException()`, `ContentHash_DuplicateCategory_Throws` fails.
4. Implement T2.3 and T2.4. Green with the same filter.
5. Implement T2.5 and the seam. Then `test-play -Filter "FishGame.Tests.PlayMode.UiFlowCancelTests" -TimeoutMinutes 15` (background) goes green.
6. Mutation, shown red once each:
   - `Truncate` as a plain `Substring`;
   - `SessionText.Name` without the lone-surrogate drop;
   - the cancel handler subscribed after `_attempt.Cancel()` with a fake that reports inside Cancel (the extra case below);
   - `ReturnToMenuAfterCancel` removed (the test waits for the late completion).
7. Full `test-edit` and full `test-play` (background). Restore the scenes. Byte scan. Commit (T2.9).

### T2.7 Tests (exact names; cases)
**`ContentHashTests.ContentHash_DuplicateCategory_Throws`** (required)
- `[("species", {"a"}), ("rods", {"r"}), ("species", {"b"})]`. The two duplicates are **not adjacent** in the input. `Assert.Throws<ArgumentException>`, with the message containing `duplicate content category 'species'`.
- `[("Species", {"a"}), ("species", {"a"})]` does not throw (ordinal).
- **Catches:** no check; a check of adjacent input entries only (before sorting); a case-insensitive compare.

**`DisplayNameRulesTests.DisplayNameRules_Truncate_NeverSplitsSurrogatePair`** (required)
- Let `fish = char.ConvertFromUtf32(0x1F41F)` (2 chars).
- Cases:
  - `Truncate("abc", 2) == "ab"`;
  - `Truncate("a" + fish + "b", 2) == "a"` (the pair at index 1-2 is not split);
  - `Truncate("a" + fish + "b", 3) == "a" + fish`;
  - `Truncate(fish, 1) == ""` (a leading pair with max 1);
  - `Truncate("ab", 5) == "ab"`;
  - `Truncate(null, 4) == ""` and `Truncate("abc", 0) == ""`;
  - `Assert.Throws<ArgumentOutOfRangeException>(() => DisplayNameRules.Truncate("a", -1))`.
- **Catches:** a plain `Substring(0, max)` (splits the pair); an off-by-one that keeps the high surrogate alone; null handling.

**`DisplayNameRulesTests.DisplayNameRules_Truncate_SharedByNameWireAndSessionText`** (required; needs CCR-A2)
- For each input, assert `NameWire.ToFixed(x).ToString()` equals `SessionText.Name(x).ToString()`, and both equal the expected string:
  - 16 ASCII chars -> unchanged;
  - 8 x `fish` (32 bytes) -> 7 x `fish` (28 bytes);
  - 10 x `((char)0x65E5).ToString()` (3 bytes each, 30) -> 9 of them;
  - `"ab" + ((char)0xD83D).ToString() + "c"` (lone high surrogate) -> `"abc"`;
  - `""` -> `""`.
- Also `SessionText.FitBytes("a" + fish, 4) == "a"` (5 bytes, so the pair goes as a unit).
- **Catches:** the two paths drifting apart again (old `SessionText.Name` kept the lone surrogate and encoded U+FFFD, a 3-byte replacement); `FitBytes` splitting a pair.

**`UiFlowCancelTests.UiFlow_OnlineCancel_ShowsCancellingImmediatelyAndReturnsToMenu`** (required, PlayMode, new file)
- **Fake** (internal sealed class `LateCancelSession : INetworkSession` in the same file):
  - `State` starts Offline. `Mode` is Online while not Offline. `IsHost` true. `JoinCode` null.
  - `HostOnlineAsync(ct)`:
    - if `ct` is already cancelled, return `Task.FromException<string>(new NetworkSessionException(EndReason.UserLeft, "cancelled"))` (D8);
    - otherwise `HostCalls++`, create a `TaskCompletionSource<string>(RunContinuationsAsynchronously)`, set Connecting (raising `StateChanged(Offline, Connecting)`), and register `ct.Register(() => { CancelSeen = true; if (ReportOnCancel) ReportCancel(); })`.
  - `ReportCancel()`: if Connecting, raise Connecting -> Failed, then Failed -> ShuttingDown (what `UgsNetworkSession.EndCoreAsync` does before its teardown).
  - `CompleteLate()`: if the task is pending, `ReportCancel()`, then ShuttingDown -> Offline, then `Ended(UserLeft)`, then `TrySetException(new NetworkSessionException(EndReason.UserLeft, "cancelled late"))`.
  - `LeaveAsync()`: `CompleteLate()`, set Offline if not already, return `Task.CompletedTask`.
  - The other members are no-ops returning completed tasks. `Dispose` is a no-op.
- **Test (fake with `ReportOnCancel = false`):**
  1. `AppBootstrap.OnlineSessionFactoryOverride = () => fake;`, then `LoadBootstrapAndWaitForMenu`.
  2. `ui = AppBootstrap.Current.GetComponentInChildren<UiFlowController>(true)` and `root = ui.GetComponent<UIDocument>().rootVisualElement`. Open a `PlayModeHarness.LogCapture`. Subscribe to `ToastRequested` on the App `EventBus` and count.
  3. Click `HostButton` with `Click(root.Q<Button>("HostButton"))`, where `Click` does `using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }`. Wait 2 frames. Assert:
     - `fake.HostCalls == 1` (the click reached `StartAttempt`; if this fails, the click helper is wrong, so fix the helper, not the product);
     - `ui.CurrentScreen == UiScreen.Busy`;
     - `root.Q<Label>("BusyLabel").text == "Creating session..."`.
  4. `Click(root.Q<Button>("CancelButton"))`, then **no yield**. Assert:
     - `fake.CancelSeen`;
     - `BusyLabel.text == "Cancelling..."` ("shown at once");
     - `ui.CurrentScreen == UiScreen.Busy` (the session has not reported yet).
  5. `fake.ReportCancel()`, then wait 1 frame. Assert:
     - `ui.CurrentScreen == UiScreen.MainMenu` ("back as soon as the session reports the cancel");
     - `root.Q<Button>("HostButton").enabledSelf == false` (actions stay off while the attempt is still pending);
     - the fake's task is still pending.
  6. `WaitFrames(10, 0.2f)`. Still MainMenu, and no second `HostCalls`.
  7. `fake.CompleteLate()`, then `WaitUntil(HostButton.enabledSelf, 5f)`. Assert:
     - `ui.CurrentScreen == UiScreen.MainMenu`;
     - toast count 0 (UserLeft has no toast);
     - `capture.AssertNoWarnings("online cancel")`.
- **Extra case** (allowed, C 10: "others may be added"), `UiFlow_OnlineCancel_ReportedInsideCancel_ReturnsToMenuSameFrame`: the same flow with `ReportOnCancel = true`. Right after the Cancel click, with no yield, `ui.CurrentScreen == MainMenu`. This catches a handler subscribed after `Cancel()`.
- **TearDown** (`[UnityTearDown]`): `AppBootstrap.OnlineSessionFactoryOverride = null;`, then `fake?.CompleteLate();`, then `yield return PlayModeHarness.TearDownApp();`.
- **Catches:**
  - the Busy screen held until the SDK returns (step 5 fails while the task is pending);
  - no "Cancelling..." text (step 4);
  - menu actions re-enabled before Offline (step 5);
  - a leaked or duplicate handler (step 6);
  - a toast on a user cancel (step 7).

### T2.8 Acceptance (reviewer, Opus high)
- `Truncate` matches T2.4 exactly. `Sanitize`, `SessionText.FitBytes` and (through `SessionText.Name`) `NameWire.ToFixed` use it. `NameWire` contains no truncation of its own. The behaviour change for lone surrogates is recorded in the `SessionText` comment.
- `ContentHash` duplicate check: after the sort, ordinal, message as C 7.2.
- Cancel flow:
  - the handler is subscribed before `Cancel()` and removed on first report or at the attempt's end;
  - MainMenu is shown only from Busy;
  - menu actions stay disabled until the attempt completes Offline;
  - `UiFlowController.cs` is untouched;
  - the D8 path (an already-cancelled token) and the connected-leave path still work.
- The `AppBootstrap` seam is null in production, read lazily and reset in `ResetStatics`.
- Full EditMode and PlayMode green. ASCII scan clean.

### T2.9 Commit
```powershell
git add Assets/_Project/Core/Hashing/ContentHash.cs Assets/_Project/Core/Net/DisplayNameRules.cs Assets/_Project/Gameplay/Player/NameWire.cs Assets/_Project/Gameplay/Session/SessionText.cs Assets/_Project/Gameplay/AssemblyInfo.cs Assets/_Project/UI/UiSessionActions.cs Assets/_Project/UI/UiStrings.cs Assets/_Project/App/AppBootstrap.cs Assets/_Project/Tests/EditMode/ContentHashTests.cs Assets/_Project/Tests/EditMode/DisplayNameRulesTests.cs Assets/_Project/Tests/PlayMode/UiFlowCancelTests.cs Assets/_Project/Tests/PlayMode/UiFlowCancelTests.cs.meta; git commit -m "m3(carry): ContentHash rejects duplicate categories; one surrogate-safe name truncation for Sanitize, SessionText and NameWire; online Cancel shows Cancelling... and returns to the menu when the session reports it" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task T3 - Migration: M2 placeholders to the M3 roster start (spec 12.6 step 1)

**Goal:**
- `FishTags`, `FishRarity.Legendary`, species `tags` and the three bait M3 fields exist end to end (Def -> Core).
- A one-off, idempotent `EcologyMigration`:
  - deletes `perch`, `carp`, `trout` and `catfish` (registry entries first, then the files; the FBX models stay);
  - overwrites `bluegill` with its roster values;
  - writes the bait M3 fields onto the six existing bait assets;
  - writes `minLandDepth` 0.5 (Q1 default);
  - bumps `dataVersion` to 2.
- `ContentBuilder` runs the migration before the fishing build.
- The seeds shrink to `bluegill`, so create-if-missing never recreates the deleted species.

- **Owned files (C 12):**
  - new: `Core/Ecology/FishTags.cs` (new folder `Core/Ecology/`) and `Editor/Builders/Ecology/EcologyMigration.cs` (new folder);
  - edited: `Core/Fishing/{FishRarity,FishSpeciesData,BaitStats}.cs`, `Data/Fishing/{FishSpeciesDef,BaitDef}.cs`, `Editor/Builders/Fishing/{FishingSeedData,FishingContentBuilder}.cs`, `Editor/Validation/FishingValidators.cs`, `Editor/Builders/ContentBuilder.cs` (hook);
  - `AssetPaths.cs`: no constant is needed. If one turns out to be needed, add it in a block commented `// M3 T3`.
  - migrated assets: `Content/ContentRegistry.asset`, `Content/Fishing/Species/bluegill.asset`, `Content/Fishing/Baits/*.asset` (6), `Content/Fishing/FishingTuning.asset`, and the four deleted species assets with their `.meta` files.
- **Tests owned:**
  - new `Tests/EditMode/Ecology/EcologyMigrationTests.cs` (new folder `Tests/EditMode/Ecology/`);
  - edits per T3.10 (CCR-A5).
- **Shared edits (C 2.6):**
  - the Core/Data fishing files: T3, then T13 removes `minBaitRating`/`minDepth`;
  - `FishingSeedData.cs` and `FishingContentBuilder.cs`: T3, then T10, T13, T19;
  - `FishingValidators.cs`: T3, then T13;
  - `ContentBuilder.cs`: T3 hook, then T10 hook.
- **Depends on:** T1 committed (C 3), and R72 F0-F8 committed (the seeds and tests T3 edits are R72's end state).
- **Implementer:** Sonnet. The migration code is complete below; the test and seed edits need judgement. **Reviewer:** Sonnet, plus the controller's diff of `Content/` after two builds.

### T3.1 Constraints that bite here (C 2.8)
- **#22:** tuning lives in assets. Seeds live in `FishingSeedData` (bait affinity, `minLandDepth`, the `bluegill` row) until T10 moves the species list.
- **#28: the fallback fish mesh stays.** T3 deletes species assets only. `Content/Models/Fish_{Perch,Carp,Trout,Catfish}.fbx` and their `.meta` files must remain (M3-D33 reuses them). `FishFallbackMesh` is untouched.
- **C 6.2 allowed in-place edits:** only the migration's writes (C 7.3) modify existing assets. `FishingContentBuilder` stays create-if-missing.
- **#16:** Core has no engine references (`FishTags` is a plain enum in `FishGame.Core`; no sub-namespace, C 2.2).
- **#6:** restore scene churn after `build`. Commit the migrated Content files, but no scene.
- **#24, #26, #27:** not touched.
- Tests never write committed Content. Every migration test runs only under `Assets/_Project/Tests/__Temp/EcologyMigrationTests` (gitignored, deleted in TearDown), as the M2 `FishingContentBuilderTests` do.

### T3.2 Current code (facts)
- `FishSpeciesDef`:
  - fields end with `minBaitRating, minDepth, model`;
  - `Configure(..., float minDepth, Mesh model)`;
  - `ToCore(ushort)` maps every gameplay field.
- `FishSpeciesData` has no `Tags`. `FishRarity` is `{ Common, Uncommon, Rare }`.
- `BaitDef` fields end with `unlimited`. `BaitStats` has `Rating, StackMax, MaxRating, MaxNibbles, BiteSpeedMod, JunkMult, Unlimited, EffRating(int)`.
- `FishingSeedData`:
  - `Species` has 5 rows (`MakeSpecies(...)`);
  - `Baits` has 6 rows (`Bait(...)` helper);
  - `Tuning.MinLandDepth = 0.8f`;
  - `BuildDatabase()` builds a Core database from the seeds (used by `FishingHostGuardTests` and `FishingWireTests`).
- `FishingContentBuilder.BuildAt(contentRoot, registry)`:
  - create-if-missing via `Ensure<T>`;
  - `RootedPath(contentRoot, realPath)` is internal;
  - `WireRegistry` appends missing list entries.
- `ContentBuilder.Build`: `EnsureLakeDockSpot` -> `EnsureRegistry` (creates the registry with `Configure(1, ...)` when missing) -> `EnsurePanelSettings` -> `FishingContentBuilder.Build()` -> `RenderingSetupBuilder.Build()` -> `SaveAssets`.
- `ContentRegistry`:
  - `[SerializeField] private int dataVersion = 1;` with `DataVersion`;
  - an `OnValidate` that calls `ClearCache()`, so `SerializedObject.ApplyModifiedProperties*` edits reset the index caches.
- The real registry lists the 5 species, 6 baits and 5 profiles. `dataVersion: 1`. The `bluegill.asset` model references `Fish_Bluegill.fbx`.
- The validator range for `minLandDepth` is [0.5, 1.5], so 0.5 is legal.

### T3.3 Public surface (C 7.3, verbatim intent)
```csharp
// Core/Ecology/FishTags.cs (new) - spec 12.2
[Flags] public enum FishTags : ushort { None = 0, Nocturnal = 1, Predator = 2, Bottom = 4, Schooling = 8, Surface = 16 }
// Core/Fishing/FishRarity.cs: public enum FishRarity : byte { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3 }   // display only (spec 12.2)
// Core/Fishing/FishSpeciesData.cs: + public FishTags Tags;
// Core/Fishing/BaitStats.cs: + public FishTags AffinityTags; public float AffinityMult, SpecialMult;   (Mods() comes in T5)
// Data/Fishing/FishSpeciesDef.cs: + [SerializeField] private FishTags tags;  (appended after `model`)  public FishTags Tags => tags;
//     Configure(..., Mesh model, FishTags tags = FishTags.None)   (CCR-A7); ToCore: Tags = tags
// Data/Fishing/BaitDef.cs: + [SerializeField] private FishTags affinityTags; [SerializeField] private float affinityMult = 1f; [SerializeField] private float specialMult = 1f;
//     (appended after `unlimited`) getters AffinityTags, AffinityMult, SpecialMult;
//     Configure(..., bool unlimited, FishTags affinityTags = FishTags.None, float affinityMult = 1f, float specialMult = 1f); ToCore maps all three
```
- Class comments: `FishSpeciesDef` "tags (M3, T3)"; `BaitDef` "affinity fields (M3, T3; spec 9.7/12.4; the migration wrote them onto the M2 assets)".
- The `= 1f` initializers are belt and braces (CCR-A7). The migration still writes explicit values, and the migration tests force the fields to 0 first to prove it.

### T3.4 Seeds and builder
**`FishingSeedData`:**
- `Bait(...)` gains `FishTags affinityTags, float affinityMult, float specialMult` and sets them on `BaitStats`. The six rows follow M3-D4:

| id | affinityTags | affinityMult | specialMult |
|---|---|---|---|
| `bait_bare_hook` | None | 1.0 | 1.0 |
| `bait_worm` | Bottom | 1.3 | 1.0 |
| `bait_grub` | Surface | 1.3 | 1.0 |
| `bait_minnow` | Predator | 1.4 | 1.0 |
| `bait_shrimp` | Schooling | 1.3 | 1.2 |
| `bait_raw_meat` | Predator, Nocturnal | 1.5 | 1.5 |

- `MakeSpecies(...)` gains a trailing `FishTags tags` and sets `data.Tags`.
- The species list becomes **one row**: `MakeSpecies("bluegill", "Bluegill", "steady", sortedProfileIds, 200, 600, 8, 1.0f, 0, 0f, FishRarity.Common, BodyShape.Round, new Color(0.32f, 0.50f, 0.62f, 1f), new Color(0.95f, 0.62f, 0.22f, 1f), "Fish_Bluegill.fbx", FishTags.Schooling)`. The sources are spec 12.6 row 2, M3-D35 (`biteWaitMod` 1.0, skew 2.2, K 1.0, hookWindow 0, Round), C 7.3 (gates 0/0) and the M2 colours kept.
- Comment above the list: `// M3 T3: only bluegill remains (12.6 row 2); perch/carp/trout/catfish were deleted by EcologyMigration. T10 moves the list to EcologySeedData.`
- `Tuning.MinLandDepth = 0.5f` with the comment `// M3-D52 (Q1 default): reeds castable`. If the user chose the lake-bed alternative instead, keep 0.8 and skip the tuning write in the migration (CCR-A6).

**`FishingContentBuilder.BuildAt`:**
- The bait `Configure` call passes `s.AffinityTags, s.AffinityMult, s.SpecialMult`.
- The species `Configure` call passes `x.Tags` after `null` (model).
- Header comment: add "existing assets are migrated by `EcologyMigration` (M3 T3), never here".

**`FishingValidators`:**
- In `CheckBaits`, add:
  - `Range(e, l, "affinityMult", b.AffinityMult, 1f, 3f);`
  - `Range(e, l, "specialMult", b.SpecialMult, 0.5f, 3f);`
  - `Rule(e, l, (b.AffinityTags & ~DefinedTags) == 0, "affinityTags has undefined bits");`
- In `CheckSpecies`, add `Rule(e, l, (s.Tags & ~DefinedTags) == 0, "tags has undefined bits");`.
- Add a private `const FishTags DefinedTags = FishTags.Nocturnal | FishTags.Predator | FishTags.Bottom | FishTags.Schooling | FishTags.Surface;`.
- The ranges come from M3-D4.

**`ContentBuilder.Build`:** after `EnsurePanelSettings();`:
```csharp
var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
EcologyMigration.MigrateIfNeeded(registry);   // M3 T3 (C 2.6): BEFORE the fishing build, so create-if-missing never sees the M2 placeholders
FishingContentBuilder.Build();
```
- Header comment: add "and the one-off M3 migration (`EcologyMigration`, spec 12.6 step 1)".
- `EnsureRegistry` keeps `Configure(1, ...)`. On a fresh project the migration then runs once over empty folders: every step skips, the version bumps, and `FishingContentBuilder` creates `bluegill` and the baits from the new seeds.

### T3.5 `EcologyMigration` (complete code)
```csharp
// Editor/Builders/Ecology/EcologyMigration.cs - the one-off spec 12.6 step 1 (C 7.3, M3-D1): M2 placeholder species -> the M3 roster start.
// Authority: none (editor-only build step; runs from ContentBuilder.Build before FishingContentBuilder, and from a menu item).
// Gate: a registry at dataVersion >= 2 is never touched. Every step is also idempotent on its own and dataVersion is written last, so a run
// interrupted between steps is finished by the next build. Allowed in-place edits only (C 6.2): registry species list + dataVersion,
// bluegill (every field, model kept), the three bait M3 fields, FishingTuning.minLandDepth. The FBX models are never touched.
using System;
using System.IO;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class EcologyMigration
    {
        public const int MigratedDataVersion = 2;

        private const string RealContentRoot = "Assets/_Project/Content";
        private const string BluegillId = "bluegill";
        private static readonly string[] DeletedSpeciesIds = { "perch", "carp", "trout", "catfish" };   // spec 12.6 step 1

        public static bool MigrateIfNeeded(ContentRegistry registry)
        {
            return MigrateAt(RealContentRoot, registry);
        }

        // Test seam (Tests/__Temp, like FishingContentBuilder.BuildAt): every AssetPaths constant is re-rooted under contentRoot.
        internal static bool MigrateAt(string contentRoot, ContentRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (string.IsNullOrEmpty(contentRoot) || !contentRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("contentRoot must be a project-relative path under Assets/: " + contentRoot, nameof(contentRoot));
            if (registry.DataVersion >= MigratedDataVersion) return false;

            string Rooted(string path) => FishingContentBuilder.RootedPath(contentRoot, path);
            RemovePlaceholderSpecies(registry, Rooted(AssetPaths.SpeciesFolder));
            OverwriteBluegill(Rooted(AssetPaths.SpeciesFolder), Rooted(AssetPaths.FightProfilesFolder));
            WriteBaitFields(Rooted(AssetPaths.BaitsFolder));
            WriteMinLandDepth(Rooted(AssetPaths.FishingTuningAsset));
            SetDataVersion(registry);
            AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("FishGame/Ecology/Migrate M2 Placeholders")]
        public static void MigrateMenu()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
            {
                Debug.LogError("FG MIGRATION: no ContentRegistry at " + AssetPaths.ContentRegistryAsset + " (run the build first)");
                return;
            }
            bool migrated = MigrateIfNeeded(registry);
            Debug.Log(migrated ? "FG MIGRATION: migrated to dataVersion " + MigratedDataVersion
                               : "FG MIGRATION: nothing to do (dataVersion " + registry.DataVersion + ")");
        }

        // (1) Registry entries first, so no list ever points at a deleted file; then the files. Null entries are dropped too: an interrupted run
        // (files deleted, registry not yet saved) leaves missing references, and a null species fails ContentRegistry.Validate.
        private static void RemovePlaceholderSpecies(ContentRegistry registry, string speciesFolder)
        {
            int removed = 0;
            using (var so = new SerializedObject(registry))
            {
                SerializedProperty list = so.FindProperty("species");
                if (list == null) throw new InvalidOperationException("ContentRegistry has no serialized field 'species'");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty element = list.GetArrayElementAtIndex(i);
                    var def = element.objectReferenceValue as FishSpeciesDef;
                    if (def != null && Array.IndexOf(DeletedSpeciesIds, def.Id) < 0) continue;
                    int before = list.arraySize;
                    element.objectReferenceValue = null;   // on object-reference arrays the first delete may only null the element
                    list.DeleteArrayElementAtIndex(i);
                    if (list.arraySize != before - 1)
                        throw new InvalidOperationException("registry species entry " + i + " was not removed (arraySize " + list.arraySize + ")");
                    removed++;
                }
                if (removed > 0)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();   // ContentRegistry.OnValidate clears the index caches
                    EditorUtility.SetDirty(registry);
                }
            }

            int deleted = 0;
            foreach (string id in DeletedSpeciesIds)
            {
                string path = speciesFolder + "/" + id + ".asset";
                if (!File.Exists(BuilderUtil.Abs(path))) continue;
                if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("could not delete " + path);
                deleted++;
            }
            Debug.Log("FG MIGRATION step 1: removed " + removed + " registry species entries, deleted " + deleted + " placeholder species assets (models kept)");
        }

        // (2) bluegill gets every field from its seed (spec 12.6 row 2 + M3-D35, gates 0/0); its model reference is kept.
        private static void OverwriteBluegill(string speciesFolder, string profilesFolder)
        {
            string path = speciesFolder + "/" + BluegillId + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
            if (def == null)
            {
                Debug.Log("FG MIGRATION step 2: no " + path + "; the fishing build creates it from the seed");
                return;
            }
            SpeciesSeed seed = Seed(BluegillId);
            string profilePath = profilesFolder + "/" + seed.ProfileId + ".asset";
            var fight = AssetDatabase.LoadAssetAtPath<FightProfileDef>(profilePath);
            if (fight == null) throw new InvalidOperationException("bluegill: missing fight profile asset " + profilePath);
            FishSpeciesData x = seed.Data;
            def.Configure(seed.Id, seed.Name, x.Rarity, x.BaseValue, x.WeightMinG, x.WeightMaxG, x.WeightSkew, x.LengthShapeK, fight, seed.BodyShape,
                seed.ColorA, seed.ColorB, x.BiteWaitMod, x.HookWindow, x.IsSpecial, x.MinBaitRating, x.MinDepth, def.Model, x.Tags);
            EditorUtility.SetDirty(def);
            Debug.Log("FG MIGRATION step 2: bluegill overwritten with its roster values (model kept)");
        }

        // (3) The three M3 bait fields on every existing seed bait (B4: a missing float deserializes as 0 and specialMult 0 would disable specials).
        private static void WriteBaitFields(string baitsFolder)
        {
            int written = 0;
            foreach (var b in FishingSeedData.Baits)
            {
                var def = AssetDatabase.LoadAssetAtPath<BaitDef>(baitsFolder + "/" + b.id + ".asset");
                if (def == null) continue;   // created later from the seed by FishingContentBuilder, with the fields
                using (var so = new SerializedObject(def))
                {
                    Prop(so, "affinityTags").intValue = (int)b.stats.AffinityTags;   // flags: intValue, never enumValueIndex
                    Prop(so, "affinityMult").floatValue = b.stats.AffinityMult;
                    Prop(so, "specialMult").floatValue = b.stats.SpecialMult;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(def);
                written++;
            }
            Debug.Log("FG MIGRATION step 3: wrote affinityTags/affinityMult/specialMult on " + written + " bait assets");
        }

        // (3b) M3-D52 / Q1: the tuning asset takes the seed minLandDepth (0.5 by default; equal values make this a no-op).
        private static void WriteMinLandDepth(string tuningPath)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<FishingTuningDef>(tuningPath);
            if (tuning == null)
            {
                Debug.Log("FG MIGRATION step 3b: no " + tuningPath + "; the fishing build creates it from the seed");
                return;
            }
            float seed = FishingSeedData.Tuning.MinLandDepth;
            using (var so = new SerializedObject(tuning))
            {
                Prop(so, "minLandDepth").floatValue = seed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(tuning);
            Debug.Log("FG MIGRATION step 3b: minLandDepth = " + seed.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        }

        // (4) Written last: only a complete run ends the migration.
        private static void SetDataVersion(ContentRegistry registry)
        {
            using (var so = new SerializedObject(registry))
            {
                Prop(so, "dataVersion").intValue = MigratedDataVersion;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(registry);
            Debug.Log("FG MIGRATION step 4: dataVersion = " + MigratedDataVersion);
        }

        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) throw new InvalidOperationException(so.targetObject.GetType().Name + " has no serialized field '" + name + "'");
            return p;
        }

        private static SpeciesSeed Seed(string id)
        {
            foreach (SpeciesSeed s in FishingSeedData.Species) if (s.Id == id) return s;
            throw new InvalidOperationException("no species seed '" + id + "' in FishingSeedData");
        }
    }
}
```
Notes for the implementer:
- `SerializedObject` is `IDisposable` in Unity 6. That fixes the M2 T4 review item about disposing it.
- If R72 renamed the `minLandDepth` serialized field, use R72's name and report it. `FishingDefsTests` lists the field names.
- The migration logs with `Debug.Log` only. A `LogError` would fail EditMode tests, so it is used only in the menu path (registry missing).
- If `DeleteArrayElementAtIndex` removes the element on the first call in this Unity version, nulling it first is harmless. The `arraySize` check guards both behaviours.

### T3.6 Steps (TDD)
0. **Re-check the break list after R72:**
   - `grep -rn -E "\"(perch|carp|trout|catfish)\"|SpeciesIndex\(\"(perch|carp|trout|catfish)|perch\.asset" Assets/_Project/Tests`;
   - `grep -rln "FishingSeedData\|ContentRegistryAsset\|SpeciesFolder" Assets/_Project/Tests`.
   - Compare with T3.10. A new hit gets the same treatment (re-point to `bluegill` or to an in-memory def). Report it.
1. Add `FishTags` and the Core/Data fields and accessors (T3.3). `compile` -> PASS.
2. Write `EcologyMigrationTests` (T3.9) and the edits to `FishingContentTests` (T3.10). Expected red: `test-edit -Filter "FishGame.Tests.EditMode.EcologyMigrationTests"` fails to compile (`CS0103 EcologyMigration`).
3. Seeds, builder and validators (T3.4), then `EcologyMigration` and the `ContentBuilder` hook (T3.5).
4. Green: `test-edit -Filter "FishGame.Tests.EditMode.(EcologyMigrationTests|FishingDefsTests|FishingValidatorTests|FishingContentBuilderTests)"`. These are temp or in-memory and need no build.
5. Mutation, shown red once each in the migration tests:
   - skip the registry-list removal (DeleteAsset only);
   - write `dataVersion` first and return before step 3 (simulating a crash; the resume extra test must still pass after the fix);
   - write the bait fields with `enumValueIndex`.
6. `build` (foreground; about 3 minutes). The log must show the four `FG MIGRATION step` lines plus 3b, exactly once each. Then the second-build check:
   - `git status --porcelain -- Assets/_Project/Content > <scratch>/content-1.txt`;
   - `build` again;
   - `git status --porcelain -- Assets/_Project/Content > <scratch>/content-2.txt`;
   - `git diff --stat -- Assets/_Project/Content > <scratch>/content-diff-2.txt`;
   - the two status files are equal, the second log has **no** `FG MIGRATION` line, and the only Content changes are the T3.11 list. Paste both into the report; the controller diffs `Content/` (skeleton 7).
7. `git status -- Assets/_Project/Content/Models` shows nothing: the four FBX files and their metas are untouched.
8. Apply the remaining T3.10 test fixes. Full `test-edit`, then full `test-play` (background, `-TimeoutMinutes 15`).
9. Restore the scenes (0.4). Byte scan. Commit (T3.11).

### T3.7 What the migrated assets must look like (acceptance values)
- `ContentRegistry.asset`: `dataVersion: 2`. `species` has exactly one entry (bluegill's GUID, unchanged). Rods, baits, profiles, junk and singletons are unchanged.
- `bluegill.asset`:
  - `baseValue: 8`, `weightMinG: 200`, `weightMaxG: 600`, `biteWaitMod: 1`, `minBaitRating: 0`, `minDepth: 0`, `tags: 8`;
  - `fight` = the steady GUID, and `model` = the same `Fish_Bluegill.fbx` reference as before;
  - colours unchanged, `rarity: 0`, `bodyShape: 0`.
- Each bait asset gains `affinityTags`, `affinityMult` and `specialMult` per the T3.4 table. Every other field is byte-identical.
- `FishingTuning.asset`: `minLandDepth: 0.5`. No other change.
- `perch`, `carp`, `trout` and `catfish` `.asset` and `.meta` are deleted. `Content/Models/*` is untouched.

### T3.8 Behaviour after T3 (for the reviewer)
Until T13, the M2 `PlaceholderRoller` rolls only bluegill or junk: bluegill's gates are 0/0, so every bait reaches it. That is expected; T10 adds the roster and T13 the resolver. PlayMode tests force `bluegill` already. `ContentHash` changes because `dataVersion` is now 2. Old builds cannot join new ones, which is expected; `ProtocolVersion` moves 3 -> 4 in T14 (R72 took 2 -> 3).

### T3.9 Tests: `Tests/EditMode/Ecology/EcologyMigrationTests.cs` (exact names; cases)
**Fixture** (`[SetUp]`, fresh per test, all under `Root = "Assets/_Project/Tests/__Temp/EcologyMigrationTests"`):
1. `DeleteTempFolders()`, using the `FishingContentBuilderTests` helper pattern. Then `BuilderUtil.EnsureFolder(Root)`. Create `registry = CreateInstance<ContentRegistry>()` with `CreateAsset(registry, Root + "/ContentRegistry.asset")`; its `dataVersion` defaults to 1.
2. `FishingContentBuilder.BuildAt(Root, registry)`. This gives a complete temp content (profiles, rods, baits, bluegill, tuning, art) from the current seeds, independent of any R72 `Configure` signature.
3. **Age it to M2:**
   - Create `Root/Models/TempFish.asset` (a small `Mesh`) with `EnsureFolder(FishingContentBuilder.ModelsFolderAt(Root))`.
   - Create four `FishSpeciesDef` assets `perch`, `carp`, `trout` and `catfish` at `Rooted(SpeciesFolder)/<id>.asset`, with `Configure` using their M2 values (spec 9.6 table), fight = the temp profile asset, and model = the temp mesh. Append them to the registry `species` list with `SerializedObject`.
   - Overwrite the temp bluegill with its M2 values: 200-800 g, value 5, biteWaitMod 0.9, gates 1/0.8, tags None, model = the temp mesh.
   - Set each bait's `affinityTags`/`affinityMult`/`specialMult` to 0 with `SerializedObject`, as an M2 asset deserializes.
   - Set tuning `minLandDepth` to 0.8.
   - `AssetDatabase.SaveAssets()`.
4. Keep `TearDown` (`DeleteTempFolders()`) and an `OneTimeSetUp` cleanup on failure, like `FishingContentBuilderTests`.

**`EcologyMigration_RemovesM2PlaceholderSpeciesKeepsModels`** (required)
- `MigrateAt(Root, registry)` is true.
- For each of the four ids: `File.Exists(BuilderUtil.Abs(path))` is false and `LoadAssetAtPath` is null.
- The registry `species` serialized list (`SerializedObject`) has `arraySize == 1`, its element is the bluegill asset, and no element is null.
- `registry.TryGetSpecies("perch", out _)` is false, which proves the cache was cleared.
- `registry.Species.Count == 1`.
- The temp mesh asset still exists and the bluegill's `Model` is still that mesh.
- The real `Assets/_Project/Content/Models/Fish_{Perch,Carp,Trout,Catfish}.fbx` exist (`File.Exists`).
- **Catches:**
  - `DeleteAsset` without removing the list entry (a null entry);
  - list removal without the file delete;
  - `DeleteArrayElementAtIndex` only nulling the element (`arraySize` stays 5);
  - a migration that deletes model files;
  - a stale index cache.

**`EcologyMigration_OverwritesBluegillWithRosterValues`** (required)
- After the migration, the temp bluegill equals the seed in **every** field: id, displayName, rarity, baseValue, weights, skew, K, fight (`AreSame` as the temp `steady` asset), bodyShape, colorA/B, biteWaitMod, hookWindow, isSpecial, minBaitRating, minDepth and tags.
- It also matches the literal roster values: `WeightMinG == 200`, `WeightMaxG == 600`, `BaseValue == 8`, `Rarity == Common`, `Tags == FishTags.Schooling`, `BiteWaitMod == 1.0f`, `MinBaitRating == 0`, `MinDepth == 0f`, `Fight.Id == "steady"`.
- `Model` is `AreSame` as the temp mesh (kept).
- **Catches:** a partial overwrite (biteWaitMod 0.9 kept); a wrong seed (the literal checks); the model nulled by passing `null` to `Configure`; a fight resolved from the real Content folder instead of the root.

**`EcologyMigration_WritesBaitM3FieldsOnExistingAssets`** (required)
- Take a pre-migration snapshot of each bait's other fields: rating, stackMax, maxRating, biteSpeedMod, junkMult, maxNibbles, unlimited.
- After the migration, each of the six temp baits has the T3.4 table values (a literal table in the test, not the seeds), and every snapshot field is unchanged.
- The temp tuning `MinLandDepth == FishingSeedData.Tuning.MinLandDepth` (0.5 under the Q1 default).
- **Catches:**
  - a bait skipped;
  - tags written via `enumValueIndex` (`Predator | Nocturnal` = 3 would be read as index 3);
  - other bait fields reconfigured (create-if-missing values lost);
  - the tuning write missing.

**`EcologyMigration_BumpsDataVersionOnce_SecondRunIsNoOp`** (required)
- The first `MigrateAt` is true and `registry.DataVersion == 2`.
- Snapshot `File.ReadAllText` of every file under `Root` (after `SaveAssets`). The second `MigrateAt` is false, and the snapshot is byte-identical.
- **Gate case:** plant a new `perch` species asset (not listed) and a bait with `affinityMult` 0, and call `MigrateAt` again. It returns false, `perch.asset` still exists, and the bait is still 0. The version gate, not step idempotency, prevents the run.
- **Catches:** no version gate; a gate on `> 2`; a version bump that happens on every run; a second run that rewrites assets.

**Extra test** (allowed): `EcologyMigration_InterruptedRun_ResumesCleanly`
- Before migrating, delete `perch.asset` directly with `AssetDatabase.DeleteAsset`, leaving a missing reference in the list.
- `MigrateAt` is true, the list has one non-null entry, and the other three files are gone.
- **Catches:** a migration that throws or keeps the null entry after a crash between steps.

### T3.10 Edits to M2 tests (every hit of the deleted species, with its fix)
| File (test) | Why it breaks | Fix |
|---|---|---|
| `FishingContentTests.FishingData_SeedsMatchSpecTables` | expects 5 species and `minLandDepth` 0.8 | `Tn("minLandDepth", 0.5f, ...)` with the comment `// M3-D52 (spec 9.3/9.12 delta)`. Species count 1. One `Species("bluegill", "Bluegill", "steady", 200, 600, 8, 1.0f, 0, 0f, FishRarity.Common, BodyShape.Round, <M2 colours>, "Fish_Bluegill.fbx")`, plus `Assert.AreEqual(FishTags.Schooling, SeedSpecies("bluegill").Data.Tags)`. Delete the four placeholder rows. Comment: "spec 12.6 row 2 + M3-D35; T10 moves the species seeds" |
| `FishingContentTests.FishingData_BaitAffinitySeeds_MatchContract` (new, required) | - | For each of the six bait seeds, `AffinityTags`, `AffinityMult` and `SpecialMult` equal the M3-D4 literal table (exact floats). Then, for each real bait asset (`LoadRegistry().Baits`), the same three values equal the seed. Message `"bait asset <id> was never migrated (B4)"` when `AffinityMult == 0`. **Catches:** a seed typo or swapped columns; real assets never migrated (the 0 B4 failure) |
| `FishingDefsTests.FishingDefs_SerializedNames_MatchContract` | pins the field lists | `BaitDef` row: append `,affinityTags,affinityMult,specialMult`. `FishSpeciesDef` row: append `,tags` after `model` |
| `FishingDefsTests` `CheckBait` / `CheckSpecies` helpers | new fields are not round-tripped | `CheckBait`: `Configure(..., true, FishTags.Bottom \| FishTags.Surface, 1.7f, 0.6f)`, then assert the getters and `ToCore()` (`AffinityTags`, `AffinityMult`, `SpecialMult`). Keep a second `Configure` with no new arguments and assert the defaults `None/1/1`. `CheckSpecies`: pass `FishTags.Predator \| FishTags.Nocturnal`, then assert `Tags` and `ToCore(7).Tags`. Add one `Configure` with `FishRarity.Legendary` asserting `(byte)d.Rarity == 3`. **Catches:** `ToCore` not mapping the new fields; Legendary misnumbered |
| `FishingValidatorTests` (range table, `MakeSeedSet`, and the `weightMaxG`/`hookWindow`/unreachable tests) | `set.ById["perch"]` is missing because the seed set comes from `FishingSeedData` | Re-point every `"perch"` key and range-table row to `"bluegill"`. The weight test uses `weightMaxG` 200 (== `weightMinG`, expect the error) and 201 (ok). Add range rows `("bait", "bait_worm", "affinityMult", 1f, 3f, false)` and `("bait", "bait_worm", "specialMult", 0.5f, 3f, false)`. `SetNum` gains `case SerializedPropertyType.Enum: p.intValue = (int)value; break;`. `MakeSeedSet` passes the seed affinity values and `x.Tags`. The `CheckSpeciesModel("perch", ...)` string-only cases stay: they test path strings, and `Fish_Perch.fbx` still exists |
| `FishingValidatorTests.FishingValidators_UndefinedTagBits_AreReported` (new extra) | - | `SetNum(set.ById["bait_worm"], "affinityTags", 32)` -> `"bait bait_worm: affinityTags has undefined bits"`. `SetNum(set.ById["bluegill"], "tags", 64)` -> `"species bluegill: tags has undefined bits"`. Value 31 is clean |
| `FishingContentBuilderTests` (`BuildAt_ReRootsEveryAsset`, `BuildAt_NeverReconfiguresExistingAssets`) | load `perch.asset` from the temp build | load `bluegill.asset` instead (`baseValue` original read from the asset, so nothing else changes) |
| `ContentAssetParityTests.SeedSpecies_Models_AreFishModelMeshes_UnderTriangleBudget` | asserts 5 seed species | `Is.EqualTo(1)` with the message `"after the M3 migration only bluegill is seeded here (T10 moves the roster)"` |
| `FishFallbackMeshTests.FishFallbackMesh_MissingRegistryResult_DoesNotPoisonTheCache(false)` | `SpeciesIndex("perch")` is missing, and perch was the non-Round shape discriminator | species case uses `bluegill`. Assert `vertexCount` differs **only when `isJunk`** (bluegill is Round, like the plain fish), and keep the colour assert for both. Comment the reason; section C may restore a Long species after T10 |
| `Tests/PlayMode/FeedbackSoloTests.FeedbackSolo_Reveal_ModelFallbackAndCreelFull_CoverEverySubmesh` | `SpeciesIndex("perch")` for the two-submesh model swap | swap bluegill's model instead (`fish = SpeciesIndex("bluegill")`, restored in `finally`). The ReleasedCreelFull step keeps `bluegill`; its drop and splash asserts do not depend on the mesh. Fix the comment "(a species without a model)" |
| Safe, no edit | - | `ContentRegistryTests` (in-memory defs named carp/perch; optional `Configure` parameters keep them compiling). `FishingTestData`-backed tests (`PlaceholderRollerTests`, `FishingHostLedgerTests`, `FishingRollValidationTests`; in-memory, R72-owned). `FishingHostGuardTests` and `FishingWireTests` (they use `db.Species[0]` = bluegill and sample fish from the seed database; bluegill's 0/0 gates keep the fish samples). `RodControllerHelperTests` and `FishingSoloTests` telemetry (species ids as plain strings). `Tuning/FishingBots.cs` (loads profiles, rods and tuning, no species; re-check after R72 in step 0) |

### T3.11 Commit
```powershell
git rm -q Assets/_Project/Content/Fishing/Species/perch.asset Assets/_Project/Content/Fishing/Species/perch.asset.meta Assets/_Project/Content/Fishing/Species/carp.asset Assets/_Project/Content/Fishing/Species/carp.asset.meta Assets/_Project/Content/Fishing/Species/trout.asset Assets/_Project/Content/Fishing/Species/trout.asset.meta Assets/_Project/Content/Fishing/Species/catfish.asset Assets/_Project/Content/Fishing/Species/catfish.asset.meta
git add Assets/_Project/Core/Ecology.meta Assets/_Project/Core/Ecology/FishTags.cs Assets/_Project/Core/Ecology/FishTags.cs.meta Assets/_Project/Core/Fishing/FishRarity.cs Assets/_Project/Core/Fishing/FishSpeciesData.cs Assets/_Project/Core/Fishing/BaitStats.cs Assets/_Project/Data/Fishing/FishSpeciesDef.cs Assets/_Project/Data/Fishing/BaitDef.cs Assets/_Project/Editor/Builders/Ecology.meta Assets/_Project/Editor/Builders/Ecology/EcologyMigration.cs Assets/_Project/Editor/Builders/Ecology/EcologyMigration.cs.meta Assets/_Project/Editor/Builders/Fishing/FishingSeedData.cs Assets/_Project/Editor/Builders/Fishing/FishingContentBuilder.cs Assets/_Project/Editor/Validation/FishingValidators.cs Assets/_Project/Editor/Builders/ContentBuilder.cs Assets/_Project/Content/ContentRegistry.asset Assets/_Project/Content/Fishing/Species/bluegill.asset Assets/_Project/Content/Fishing/Baits/bait_bare_hook.asset Assets/_Project/Content/Fishing/Baits/bait_worm.asset Assets/_Project/Content/Fishing/Baits/bait_grub.asset Assets/_Project/Content/Fishing/Baits/bait_minnow.asset Assets/_Project/Content/Fishing/Baits/bait_shrimp.asset Assets/_Project/Content/Fishing/Baits/bait_raw_meat.asset Assets/_Project/Content/Fishing/FishingTuning.asset Assets/_Project/Tests/EditMode/Ecology.meta Assets/_Project/Tests/EditMode/Ecology/EcologyMigrationTests.cs Assets/_Project/Tests/EditMode/Ecology/EcologyMigrationTests.cs.meta Assets/_Project/Tests/EditMode/FishingContentTests.cs Assets/_Project/Tests/EditMode/FishingDefsTests.cs Assets/_Project/Tests/EditMode/FishingValidatorTests.cs Assets/_Project/Tests/EditMode/FishingContentBuilderTests.cs Assets/_Project/Tests/EditMode/ContentAssetParityTests.cs Assets/_Project/Tests/EditMode/FishFallbackMeshTests.cs Assets/_Project/Tests/PlayMode/FeedbackSoloTests.cs
git commit -m "m3(migration): FishTags, Legendary, species tags and bait affinity fields; EcologyMigration deletes the M2 placeholder species, overwrites bluegill, writes bait M3 fields and minLandDepth 0.5, dataVersion 2" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
- The `git rm` stages deletions that `AssetDatabase.DeleteAsset` already made on disk.
- If the controller wants the asset diff reviewed before the commit, stop after step 9 and hand over the step 6 files.

### T3.12 Acceptance (reviewer, Sonnet; plus the controller's Content diff)
- C 7.3 surface present, appended at the end with optional trailing parameters (CCR-A7). The `ToCore` mappings are tested.
- `EcologyMigration` matches T3.5:
  - gate on `>= 2`;
  - list removal before file deletion, with the `arraySize` check;
  - null entries dropped;
  - bluegill model kept;
  - bait flags written via `intValue`;
  - `dataVersion` last;
  - one log line per step.
- `ContentBuilder` calls it before `FishingContentBuilder.Build()`. `FishingContentBuilder` is still create-if-missing.
- Seeds: one species row, the M3-D4 bait table, `minLandDepth` 0.5.
- Validators: the M3-D4 ranges and the defined-bits rules.
- After two builds: the second makes no Content change and logs no `FG MIGRATION`. The registry has bluegill only among the old species. Every bait asset has non-zero `affinityMult` and `specialMult`. `dataVersion` is 2. The four FBX files are untouched (skeleton 7).
- Every T3.10 row is applied. Full EditMode and PlayMode green. Scenes restored. ASCII scan clean.

---

## Task T4 - Look-dev adoption and M2 restyle (pointer only)

Not authored here (C 7.4, skeleton 3). The R74 look-dev result on branch `m2-lookdev` owns this work, with the style guide `docs/superpowers/research/2026-10-12-art-direction-lookdev.md` and the look-dev plan. M3 relies only on the four points in C 7.4:
- the style guide;
- the season-tint shader hook, or a statement that T18 adds it;
- `FishFallbackMesh` working with the new fish material;
- the lake rebuild keeping every collider, exit, ladder volume, spawn and the bell anchor.

T1-T3 do not depend on T4. If T4 lands before T3 and restyles `Fish_Bluegill.fbx`, the migration keeps whatever `model` reference bluegill has.

---

## Summary of tests in this section

| Task | New tests (required names from C 10.1 in bold) | Edited tests |
|---|---|---|
| T1 | **`FishingSolo_CastMarker_UpdatedAfterMachineTickSameFrame`**, **`FishingSolo_SceneReload_KeepsDevOverrides_SessionEndClears`**; 3 `CountSubShaders_Cases` rows; positive control inside the ledger zero-alloc test | comment of `SoloHost_LeavingTheSession_ClearsF6AndF7`, comment of `SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast` |
| T2 | **`ContentHash_DuplicateCategory_Throws`**, **`DisplayNameRules_Truncate_NeverSplitsSurrogatePair`**, **`DisplayNameRules_Truncate_SharedByNameWireAndSessionText`**, **`UiFlow_OnlineCancel_ShowsCancellingImmediatelyAndReturnsToMenu`**, extra `UiFlow_OnlineCancel_ReportedInsideCancel_ReturnsToMenuSameFrame` | - |
| T3 | **`EcologyMigration_RemovesM2PlaceholderSpeciesKeepsModels`**, **`EcologyMigration_OverwritesBluegillWithRosterValues`**, **`EcologyMigration_WritesBaitM3FieldsOnExistingAssets`**, **`EcologyMigration_BumpsDataVersionOnce_SecondRunIsNoOp`**, **`FishingData_BaitAffinitySeeds_MatchContract`**, extras `EcologyMigration_InterruptedRun_ResumesCleanly`, `FishingValidators_UndefinedTagBits_AreReported` | **`FishingData_SeedsMatchSpecTables`** and the T3.10 rows |

Implementers report the `total=` numbers of the full runs. The controller compares them with the previous green run plus this table (R72 changes the base counts).

---

## Open questions (each has a default that applies if unanswered)

- **A-Q1 (skeleton Q1): reeds depth.** Default: `minLandDepth` 0.5 (seed, asset through the migration, spec delta; CCR-A6, accepted 2026-10-12 as a spec value change pending the user's Q1). The alternative (a lake-bed shelf, T16) leaves 0.8, and T3 then skips step 3b and the test edit.
- **A-Q2: the "3 extra `ShaderValidatorTests` cases" from the M2 polish review.** The review text is not in the repo (it was in a gone scratchpad). Default: the three cases in T1.7, which pin the current scanner (default confirmed by the controller 2026-10-12). The controller may substitute names if it has the original list. A case that needs a scanner fix would touch `ShaderValidator.cs`, outside T1's manifest.
- **A-Q3: a cancel that loses the race to Connected.** If the session reaches Connected just before Cancel takes effect, the attempt succeeds and the game loads. That is M1 behaviour. Default: keep it (default confirmed by the controller 2026-10-12). The alternative, where `StartAttempt` leaves when `_cancelPending` is still set after success, is a small `UiSessionActions` change and would need a test.
- **A-Q4: CCR-A3 seam vs fallback.** Default: the `AppBootstrap.OnlineSessionFactoryOverride` seam (CCR accepted 2026-10-12).

## Risks

1. **T1 reload path.** An NGO single-load of the already-active `Spot_LakeDock` in solo is new territory. M3 T15 depends on the same path, and `PlayerSpawner` spawns players with `destroyWithScene: true` and respawns them on `OnLoadEventCompleted`. If it warns, T1 uses the allowed fallback (T1.7), and the reload log becomes an early T15 risk report. That is useful either way.
2. **Inert allocation counter.** `GC.GetAllocatedBytesForCurrentThread` may read 0 on Unity's Mono. In that case the M2 zero-alloc assert was vacuous, and T1 switches to the Unity Test Framework constraints (T1.7). If the real probe then shows the ledger `Tick` allocating, T1 stops and reports: `FishingHostLedger.cs` is not in T1's manifest, so the fix is a controller decision (likely folded into T13).
3. **T3 deletes committed assets.** The migration's real run happens only inside `build` on the real registry. All tests use `__Temp`. The controller diffs `Content/` after two builds before the commit is accepted. A wrong registry cannot be migrated twice (version gate).
4. **R72 end state.** T3 edits files R72 also edits (seeds, `FishingContentTests`, `FishingValidators`). Symbols, not lines, are cited, and step 0 re-greps the break list. If R72 renamed `minLandDepth` or changed `Configure` shapes, the implementer follows R72 and reports it.
5. **T2 UI click simulation.** `NavigationSubmitEvent` triggers `Button.clicked` in Unity 6 UI Toolkit. The test asserts `fake.HostCalls == 1` right after the Host click, so a silent no-op click fails loudly. It does not pass vacuously.
6. **Between T3 and T13 the game catches only bluegill (or junk).** This is intended, and must not be mistaken for a bug in playtests. The dataVersion 2 hash change blocks old builds from joining.
