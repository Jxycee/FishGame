# FishGame M2 - Section A plan: Carry-forwards (T1, T2, T3, T4)

- **Status (2026-10-12):** T1-T4 are implemented and committed (`1c8d78d`, `649fb88`, `ed20831` + `1c3b423`, `43178ee` + `4eb9edc`). Review notes: the T2 manual smoke list (T2.7) gained steps 3b, 5b, 6b, 7, 8 (text in the T2 review report); `UiFlowController` limit is < 400 lines (A-Q2); `IHudExtension.Bind` must never throw and extension buttons play their own `Ui_Click` (carried to T25); T4 validators loop all `MeshCollider`s, dispose the `SerializedObject` and keep unloaded scenes closed correctly (`4eb9edc`); T19 later made one integration fix in the T3 harness (original leak message logged before recovery; `PrepareResetScene` try/catch helper; C 2.6).
- **Date:** 2026-10-11. Branch `m2-fishing-core`. Code line numbers below refer to commit `f5e045a` (M1 code as it is today).
- **Binding contract:** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` ("C" below). Skeleton: `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md`. If this file disagrees with C, C wins; every tension raised is closed by a controller ruling, listed under "Contract questions" at the end.
- **Scope:** behaviour-preserving refactors plus the M1 review fixes (C 14: DA1, DA19-DA23). Nothing here adds gameplay.
- **Order:** T1 -> T2 -> T3 -> T4 (serial, one Unity lock). Pipelining (skeleton 5): while T1 is in review, T4 may run before T2; T2 must wait for T1's review because both edit `LocalNetworkSession.cs`.

---

## 0. Shared notes for all four tasks

### 0.1 Read first
Every implementer reads: `CLAUDE.md`, C 2.8 (global constraints, all of them apply), C 2.1 (commits), C 2.6 (shared-file edits), C 14, and the task below. The ones that bite in this section are repeated in each task.

### 0.2 Commands
The PowerShell tool does not keep variables between calls, so every command line sets `FG_OUT` itself. `<scratch>` is your session scratchpad directory (absolute, outside the repo).

```powershell
$env:FG_OUT = '<scratch>\m2-tN'; New-Item -ItemType Directory -Force $env:FG_OUT | Out-Null; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 <compile|build|test-edit|test-play|check> [-Filter "<regex>"] [-TimeoutMinutes 15]
```

- Result lines and exit codes: M1 contract section 11 (`FG RESULT ... PASS|FAIL|LOCKED|TIMEOUT`; exit 0 pass, 1 fail, 2 locked, 3 timeout, 4 usage).
- **Exit 2 (LOCKED):** the user has the editor open. Stop and ask the user to close it. Never kill Unity. Stop only when `unity.ps1` reports LOCKED (exit 2): Unity leaves `Temp/UnityLockfile` behind after a batch run (for example a compile-error abort), and a file that is merely present is not a lock; locked means the file is HELD (exclusive open fails), which the script tests.
- `check` = build -> test-edit -> test-play, stopping at the first failure. Use `-TimeoutMinutes 15` for `check` and `test-play`.
- Skeleton 7 requires EditMode **and** PlayMode green after each of T1-T4, so every task in this section ends with `check` (C 2.4 now says the same: PlayMode is green after each of T1-T4, ruling A-Q4; `test-play` is then routine from T19 on).

### 0.3 Scene identity check (used by T1, T2, T3, T4 after `check`)
Every `build` renumbers fileIDs (C 2.8 #6). This check tells fileID churn from a real change. Run it in Git Bash from the repo root:

```bash
OUT="<scratch>/scenecheck"; mkdir -p "$OUT"; cd /c/Dev/FishGame
norm() { tr -d '\r' | sed -E 's/&-?[0-9]+/\&N/g; s/fileID: -?[1-9][0-9]*/fileID: N/g' | sort; }
for s in Bootstrap Menu Spot_LakeDock; do
  f="Assets/_Project/Scenes/$s.unity"
  git show "HEAD:$f" | norm > "$OUT/$s.before"; norm < "$f" > "$OUT/$s.after"
  if cmp -s "$OUT/$s.before" "$OUT/$s.after"; then echo "SCENE SAME $s"; else echo "SCENE DIFF $s"; diff "$OUT/$s.before" "$OUT/$s.after" | head -40; fi
done
git status --porcelain -- Assets/_Project/Content Assets/_Project/Prefabs ProjectSettings
```

- All three `SCENE SAME` and an empty `git status` for Content/Prefabs/ProjectSettings (apart from paths on the baseline allow-list, 0.3a): run `git restore Assets/_Project/Scenes/*.unity` plus `git restore` of the allow-listed paths, and continue.
- Any other `SCENE DIFF`, or any other changed file under Content, Prefabs or ProjectSettings: stop and report the output. Do not commit generated files in this section.
- `fileID: 0` (a null reference) is deliberately not normalized, so a reference that became null shows up as a diff.

### 0.3a Baseline (once, before T1's first edit; whoever runs first of T1/T4)
Commits after the last scene commit (`e854365`) touched settings and slot colours, so a no-op `build` on HEAD may already change generated files. On the unmodified tree: `build`, then the 0.3 script, then `git status --porcelain`. Record every path that changes (and every `SCENE DIFF`) in `<scratch>/baseline-churn.txt` and paste it into the task report; then `git restore` those paths. That list is the allow-list for T1-T4. If the baseline shows a `SCENE DIFF` or a changed generated file, the controller decides whether to commit a regenerated baseline first (a separate `m2(carry): regenerate` commit) before any T1-T4 work; implementers do not decide this.
- The sorted compare covers every `m_LocalPosition`, `m_LocalRotation`, `m_LocalScale` and every `guid:` reference, so it is the proof of "LakeDock layout byte-identical" (skeleton 7, C G7).

### 0.4 Commits
Explicit paths only, including every new `.meta` (Unity writes them on the first compile). Never `git add -A` / `git add .`. Before committing, `git status --porcelain` shows nothing under `Library/ Temp/ Logs/ UserSettings/` and no `*.csproj`, `*.slnx`. Message shape (C 2.1): area `carry`, trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

### 0.5 Verbatim-move check (T2; reviewers may also use it on T1)
A "move" means the method body is byte-identical except for the call-site edits this plan lists. Check it as a sorted line multiset:

```bash
cd /c/Dev/FishGame; OUT="<scratch>/movecheck"; mkdir -p "$OUT"
strip() { tr -d '\r' | sed -E 's/^[[:space:]]+//' | grep -v '^$' | sort; }
git show HEAD:Assets/_Project/Networking/LocalNetworkSession.cs | strip > "$OUT/lns.before"
cat Assets/_Project/Networking/LocalNetworkSession.cs Assets/_Project/Networking/LocalNetworkSession.Start.cs Assets/_Project/Networking/LocalNetworkSession.NgoEvents.cs | strip > "$OUT/lns.after"
diff "$OUT/lns.before" "$OUT/lns.after"
```

The diff may only show: `using` lines, `namespace`/class declaration/brace lines of the new files, the new header comments, and the edits listed in the task. Use the same pattern for `UiFlowController.cs` against the four new UI files.

---

## Task T1 - Carry: hygiene (asmdef, Pcg32 float range, LogOnce, AudioBus warn limit, EventIsolation)

**Goal:** EditMode tests can reach Gameplay and Fishing; `Pcg32.Range(float, float)` never returns max and rejects `max <= min`; AudioBus warns once per `(reason, id)`; every M1 session event raise isolates each subscriber; the wrong comment at `UgsNetworkSession.cs:78` is fixed.

- **Status:** done (committed `1c8d78d`).
- **Owned files (C 12):** `Tests/EditMode/FishGame.Tests.EditMode.asmdef`, `Core/Random/Pcg32.cs`, `Core/Diagnostics/LogOnce.cs` (new; new folder `Core/Diagnostics/`), `Runtime/Services/EventIsolation.cs` (new), `Runtime/Audio/AudioBus.cs`, `Networking/LocalNetworkSession.cs`, `Networking/UgsNetworkSession.cs`, `Networking/UgsNetworkSession.Teardown.cs`, `Networking/CompositeNetworkSession.cs`.
- **Tests owned:** `Tests/EditMode/Pcg32Tests.cs` (add), `Tests/EditMode/LogOnceTests.cs` (new), `Tests/EditMode/EventIsolationTests.cs` (new), `Tests/EditMode/AudioBusTests.cs` (new; in the C 12 T1 manifest, ruling A-Q3). T11 later adds the `unknown|` and `missing|` warn-once cases to this file (T1 review carry-over; not T1's work).
- **Shared edits (C 2.6):** asmdef (T1 only); `Pcg32.cs` (T1); `AudioBus.cs` (T1 now, T11 later: keep your edit small and local to `TryPrepare` so T11 merges cleanly); `LocalNetworkSession.cs` (two raise sites; T2 splits the file next); `UgsNetworkSession.cs`, `UgsNetworkSession.Teardown.cs`, `CompositeNetworkSession.cs`; `Runtime/Services/` new file.
- **Depends on:** nothing.
- **Implementer:** Sonnet. **Reviewer:** Opus high.

### T1.1 Constraints that bite here (C 2.8)
- #2: no backslash-u escape sequences; ASCII only. #16: Core never uses `UnityEngine.Random` / `System.Random`; Core has `noEngineReferences: true`, so `LogOnce` uses only `System` and `System.Collections.Generic`.
- #8: EditMode reaches Core, Data, Runtime, Editor, Gameplay, Fishing (after this task). Never Networking. The six isolation sites are therefore proven by PlayMode only (`check`).
- #9: PlayMode fails on unexpected `Debug.LogError`. `Debug.LogException` (what EventIsolation uses) also fails a test when it fires unexpectedly: that is wanted.
- #6/#21: scene churn restored after the scene check; logs in `<scratch>`.

### T1.2 Current code (facts)
- `Pcg32.Range(float, float)` (`Pcg32.cs:54-57`) returns `minInclusive + NextFloat() * (maxExclusive - minInclusive)`. With `NextFloat() = 0.99999994f` (the largest value, `(2^24 - 1) / 2^24`) and range `[1, 2)` the sum rounds to exactly `2f`. No `max > min` check. Callers: `AmbienceBed.cs:84`, `LakeDockBuilder.cs:125-127, 148-150` (C G7).
- `AudioBus.TryPrepare` (`AudioBus.cs:150-170`) logs a warning on every call for: not ready (`:156`), unknown id (`:161`), missing clip (`:166`). The header comment `:10` says "missing clip -> warning, silent".
- Whole-event invokes (C G8): `LocalNetworkSession.OnStateMachineChanged` `:437-447` and `RaiseEnded` `:449-459`; `UgsNetworkSession.OnStateMachineChanged` `:79-89` (comment at `:78`); `UgsNetworkSession.Teardown.cs:77` (`Ended?.Invoke(reason)` inside a try that also guards `_sm.Fire(ConnEvent.ShutdownComplete, reason)` on `:74`); `CompositeNetworkSession.OnBackendStateChanged` `:106-109` and `OnBackendEnded` `:111-114` (no try at all).
- `EventBus.Publish` already isolates per handler (`EventBus.cs:37`), and `EventBusTests.EventBus_ThrowingHandler_DoesNotBlockOthers` shows the `LogAssert.Expect(LogType.Exception, new Regex("boom"))` pattern to copy.

### T1.3 Public surface (C 7.1, verbatim)
```csharp
// Core/Random/Pcg32.cs (edit)
public float Range(float minInclusive, float maxExclusive);
public static float MapUnitToRange(float unit01, float minInclusive, float maxExclusive);

// Core/Diagnostics/LogOnce.cs (namespace FishGame.Core)
public sealed class LogOnce
{
    public bool ShouldLog(string key);   // true the first time a key is seen; null key -> ""
    public void Reset();
    public int Count { get; }            // number of distinct keys seen since construction or Reset
}

// Runtime/Services/EventIsolation.cs (namespace FishGame.Runtime)
public static class EventIsolation
{
    public static void Invoke(Action evt);
    public static void Invoke<T>(Action<T> evt, T arg);
    public static void Invoke<T1, T2>(Action<T1, T2> evt, T1 a, T2 b);
}
```
No other public members. Namespaces: no sub-namespaces (C 2.2), so `LogOnce` is in `FishGame.Core` although its folder is `Core/Diagnostics/`.

### T1.4 The two subtle snippets

**Pcg32 float range.** Keep the arithmetic expression exactly as M1 wrote it (`min + unit * (max - min)`, same operand order), so every value that was below max before is bit-identical now (C G7). Guard with `!(max > min)` so NaN on either side throws. Throw **before** drawing, so a rejected call does not advance the stream.

```csharp
public float Range(float minInclusive, float maxExclusive)
{
    if (!(maxExclusive > minInclusive))
        throw new ArgumentException("maxExclusive must be greater than minInclusive.");
    return MapUnitToRange(NextFloat(), minInclusive, maxExclusive); // exactly one NextUInt, as in M1
}

// Pure. Maps unit01 in [0, 1) onto [min, max). Never returns max; never below min. No argument checks
// (Range checks); bounds whose difference overflows or is infinite are unsupported.
public static float MapUnitToRange(float unit01, float minInclusive, float maxExclusive)
{
    float r = minInclusive + unit01 * (maxExclusive - minInclusive);
    if (r >= maxExclusive) r = NextBelow(maxExclusive);
    if (r < minInclusive) r = minInclusive;
    return r;
}

// Largest float strictly below x (x finite). Bit step toward -infinity.
private static float NextBelow(float x)
{
    if (x == 0f) return -float.Epsilon;                       // +0 and -0: the step must go negative
    int bits = BitConverter.SingleToInt32Bits(x);
    return BitConverter.Int32BitsToSingle(x > 0f ? bits - 1 : bits + 1);
}
```
- `BitConverter.SingleToInt32Bits` / `Int32BitsToSingle` are in .NET Standard 2.1 (Unity 6 default). Do not use `MathF.BitDecrement` (not in .NET Standard 2.1). If CS0117 appears on `SingleToInt32Bits`, check `apiCompatibilityLevel` in `ProjectSettings/ProjectSettings.asset` and report; do not switch to `unsafe`.
- Leave `NextFloat`, `NextDouble`, `Range(int, int)` and the constructor untouched.

**EventIsolation per-subscriber invoke.** `GetInvocationList()` takes a snapshot, exactly like a multicast invoke, so subscribe/unsubscribe during the raise behaves as before. Each handler gets its own try/catch. It allocates an array per raise: fine for session events, not for per-frame use (say so in the class comment).

```csharp
public static void Invoke<T1, T2>(Action<T1, T2> evt, T1 a, T2 b)
{
    if (evt == null) return;
    Delegate[] handlers = evt.GetInvocationList();
    for (int i = 0; i < handlers.Length; i++)
    {
        try { ((Action<T1, T2>)handlers[i])(a, b); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
```
`Invoke(Action)` and `Invoke<T>(Action<T>, T)` are the same shape. Class comment: "Authority: none (local helper). Main thread only."

### T1.5 Call-site edits (the six G8 sites)
| File | Site | New body |
|---|---|---|
| `LocalNetworkSession.cs` | `OnStateMachineChanged(from, to)` `:437-447` | `EventIsolation.Invoke(StateChanged, from, to);` (try/catch removed) |
| `LocalNetworkSession.cs` | `RaiseEnded(reason)` `:449-459` | `EventIsolation.Invoke(Ended, reason);` (try/catch removed) |
| `UgsNetworkSession.cs` | `OnStateMachineChanged(prev, next)` `:79-89` | `EventIsolation.Invoke(StateChanged, prev, next);`; comment `:78` becomes `// Each subscriber is isolated (EventIsolation): a throwing StateChanged handler is logged, the others still run, and the state machine never sees the exception.` |
| `UgsNetworkSession.Teardown.cs` | `:77` | replace only `Ended?.Invoke(reason);` with `EventIsolation.Invoke(Ended, reason);` and keep its trailing comment. The surrounding try/catch stays (it still guards `_sm.Fire(ShutdownComplete)`), and `tcs.TrySetResult(reason)` still runs after it. Add `using FishGame.Runtime;`. |
| `CompositeNetworkSession.cs` | `OnBackendStateChanged` `:106-109` | `EventIsolation.Invoke(StateChanged, from, to);` |
| `CompositeNetworkSession.cs` | `OnBackendEnded` `:111-114` | `EventIsolation.Invoke(Ended, reason);` |

Effect: a throwing subscriber is logged once (by the innermost isolating raiser) and every other subscriber still runs. The exception count logged per throw is unchanged (one), because Composite's handler no longer lets anything reach the backend. No ordering changes: subscribers run in subscription order as before.

### T1.6 AudioBus warn limit (DA19)
- Add `private readonly LogOnce _warned = new LogOnce();` (per AudioBus lifetime; never reset; `Initialize` does not reset it).
- In `TryPrepare`, each of the three warnings is gated by `_warned.ShouldLog(<reason> + "|" + (sfxId ?? ""))` with reasons `not-ready`, `unknown`, `missing`. The warning text, the `return false` and every other line stay as they are.
- Header comment `:10`: "missing clip -> warning once per (reason, id), silent".
- Do not touch anything else in AudioBus (T11 edits it next).

### T1.7 asmdef (C 4)
`Tests/EditMode/FishGame.Tests.EditMode.asmdef` `references` becomes, in this order:
`UnityEngine.TestRunner, UnityEditor.TestRunner, FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Editor, Unity.InputSystem, Unity.Netcode.Runtime, FishGame.Gameplay, FishGame.Fishing, Unity.Collections`. Nothing else in the file changes. Proven by compile (no T1 test uses them; T16/T17 tests will).

### T1.8 Steps (TDD, C 2.4)
0. Baseline (0.3a) if it has not been recorded yet.
1. Edit the asmdef (T1.7).
2. Write all tests in T1.9. Create `Assets/_Project/Core/Diagnostics/` only when you add `LogOnce.cs` in step 4.
3. Red run: `test-edit -Filter "FishGame.Tests.EditMode.(Pcg32Tests|LogOnceTests|EventIsolationTests|AudioBusTests)"`. Expected: `FG RESULT test-edit FAIL total=0 (no tests matched or compile errors)` with `error CS0117` (`MapUnitToRange`) and `error CS0246` (`LogOnce`, `EventIsolation`) in the `FG LOG` lines.
4. Implement `Pcg32` (T1.4), `LogOnce`, `EventIsolation` (T1.4), the AudioBus gate (T1.6).
5. Green run: same filter. Expected `FG RESULT test-edit PASS total=13 passed=13 failed=0` (4 existing Pcg32 + 3 new Pcg32 + 2 LogOnce + 3 EventIsolation + 1 AudioBus; exact list in T1.9).
6. Edit the six sites and the comment (T1.5). `compile` -> PASS.
7. `check -TimeoutMinutes 15` -> `FG RESULT check PASS`.
8. Scene identity check (0.3): expect `SCENE SAME` x3 and no Content/Prefabs/ProjectSettings changes, then `git restore Assets/_Project/Scenes/*.unity`.
9. Byte scan of every file you touched: no byte > 0x7F, no BOM (`LC_ALL=C grep -nP '[^\x00-\x7F]' <files>` in Git Bash prints nothing; without `LC_ALL=C` it false-positives).
10. Commit (T1.12).

### T1.9 Tests (exact names; cases)
`Pcg32Tests.cs` (add three; the four M1 tests stay unchanged):
- `Pcg32_RangeFloat_NeverReturnsMax`
  - `MapUnitToRange(0.99999994f, 1f, 2f)` is `< 2f` and equals `BitConverter.Int32BitsToSingle(0x3FFFFFFF)` (1.9999999f). (The M1 formula gives exactly `2f` here.)
  - `MapUnitToRange(0.99999994f, -2f, -1f)` is `< -1f` (negative-max branch; equals `BitConverter.Int32BitsToSingle(unchecked((int)0xBF800001))`).
  - `MapUnitToRange(1f, -1f, 0f)` equals `-float.Epsilon` (zero-max branch; unit 1 is outside the domain but the clamp must hold).
  - `MapUnitToRange(0f, 1f, 2f)` equals `1f` (min inclusive).
- `Pcg32_RangeFloat_MaxNotAboveMin_Throws`
  - `Range(1f, 1f)`, `Range(2f, 1f)`, `Range(float.NaN, 1f)`, `Range(0f, float.NaN)` each throw `ArgumentException`.
  - The stream did not advance: after the four throws on `a = new Pcg32(5, 2)`, `a.NextUInt()` equals `new Pcg32(5, 2).NextUInt()`.
- `Pcg32_RangeFloat_DrawCountUnchanged`
  - `a = new Pcg32(99, 3)`, `b = new Pcg32(99, 3)`. For 10,000 draws: `float x = a.Range(-3f, 5f); float u = b.NextFloat(); float old = -3f + u * (5f - -3f);`. Assert `x == Pcg32.MapUnitToRange(u, -3f, 5f)`, and when `old < 5f` assert `x == old` (exact float equality: the M1 value is preserved bit for bit, C G7).
  - After the loop, `a.NextUInt() == b.NextUInt()` for 3 more draws (one draw per `Range` call).

`LogOnceTests.cs` (new):
- `LogOnce_SameKey_LogsOnce`: `ShouldLog("a")` true, then false, false; `ShouldLog("b")` true; `ShouldLog(null)` true then `ShouldLog("")` false (null maps to ""); `Count == 3`; keys are ordinal (`ShouldLog("A")` true afterwards, `Count == 4`).
- `LogOnce_Reset_AllowsAgain`: `ShouldLog("a")` true; `Reset()`; `Count == 0`; `ShouldLog("a")` true again; `Count == 1`.

`EventIsolationTests.cs` (new):
- `EventIsolation_ThrowingSubscriber_OthersStillRun` (required; one logged exception): an `Action<int, int>` with three subscribers in order: records `("first", a, b)`, throws `InvalidOperationException("isolation-boom")`, records `("third", a, b)`. `LogAssert.Expect(LogType.Exception, new Regex("isolation-boom"))`, then `EventIsolation.Invoke(evt, 4, 7)`. Assert the record list is exactly `first(4,7), third(4,7)` in that order.
- `EventIsolation_NullEvent_IsNoOp` (required): `Invoke((Action)null)`, `Invoke<int>(null, 1)`, `Invoke<int, int>(null, 1, 2)` do not throw; `LogAssert.NoUnexpectedReceived()`.
- `EventIsolation_ActionAndActionOfT_ThrowingSubscriber_OthersStillRun` (added): same three-subscriber pattern for `Action` and for `Action<string>`; two `LogAssert.Expect(LogType.Exception, ...)` with distinct messages; both survivors ran once each, in order, with the argument passed through.

`AudioBusTests.cs` (new; C 12 manifest, ruling A-Q3):
- `AudioBus_NotReady_WarnsOncePerId`: `var go = new GameObject("AudioBusTest"); var bus = go.AddComponent<AudioBus>();` (never initialized). Count messages through `Application.logMessageReceived` with `type == LogType.Warning` and text containing `"AudioBus not initialized"`. Calls: `PlayUi("x")`, `PlayUi("x")`, `PlayAt("x", Vector3.zero)`, `PlayUi("y")`. Assert count `== 2`. Unsubscribe and `Object.DestroyImmediate(go)` in `finally`. (Do not use `LogAssert` for warnings.)

**Existing tests that prove behaviour is unchanged:** `Pcg32_Seed42Stream54_MatchesReferenceVector`, `Pcg32_ZeroSeed_IsReplaced`, `Pcg32_RangeInt_StaysInBounds`, `Pcg32_FloatDraws_StayInHalfOpenRanges`, all of `RngTests`, `EventBusTests` (unchanged pattern), the full EditMode run; PlayMode `Bootstrap_Loads_Menu`, `Solo_Start_SpawnsOneOwnedPlayerWithCamera`, `Solo_LeaveThenRestart_Works`, `FakeInput_MoveForward_MovesPlayerMoreThanOneMeterInOneSecond`, `TeleportRpc_OnDeck_LandsWithinFiveCentimetersWithZeroVelocity` (these drive `LocalNetworkSession` and `CompositeNetworkSession` StateChanged/Ended through `SceneFlow` and `UiFlowController`); the scene identity check (rocks and trees from `Pcg32.Range`).

### T1.10 Acceptance (reviewer)
- `Range(float)` throws before drawing; expression order identical to M1; `NextBelow` handles 0 and negative max; exactly one `NextUInt` per call.
- `LogOnce` is pure (`noEngineReferences` Core; no `UnityEngine`); ordinal `HashSet<string>`.
- EventIsolation: snapshot via `GetInvocationList`, one try/catch per handler, `Debug.LogException`, null is a no-op; all three overloads.
- All six G8 sites use `EventIsolation.Invoke`; `grep -n "?.Invoke" Assets/_Project/Networking/*.cs` shows none for `StateChanged`/`Ended`; the Teardown try still guards `ShutdownComplete` and `TrySetResult` still follows; the `:78` comment is corrected.
- AudioBus: three warnings gated, texts unchanged, nothing else changed.
- asmdef references exactly as T1.7.
- `check` PASS; scene check `SCENE SAME` x3; scenes restored; test count matches T1.9 (13 in the filtered run).

### T1.11 Pitfalls
- Do not "simplify" the M1 expression to `Lerp` or reorder operands: G7 depends on identical rounding.
- `Debug` inside `FishGame.Runtime` may be ambiguous only if `System.Diagnostics` is imported; don't import it.
- Unity creates `Assets/_Project/Core/Diagnostics.meta` and the `.cs.meta` files on the first compile: commit them.

### T1.12 Commit
```powershell
git add Assets/_Project/Tests/EditMode/FishGame.Tests.EditMode.asmdef Assets/_Project/Core/Random/Pcg32.cs Assets/_Project/Core/Diagnostics.meta Assets/_Project/Core/Diagnostics/LogOnce.cs Assets/_Project/Core/Diagnostics/LogOnce.cs.meta Assets/_Project/Runtime/Services/EventIsolation.cs Assets/_Project/Runtime/Services/EventIsolation.cs.meta Assets/_Project/Runtime/Audio/AudioBus.cs Assets/_Project/Networking/LocalNetworkSession.cs Assets/_Project/Networking/UgsNetworkSession.cs Assets/_Project/Networking/UgsNetworkSession.Teardown.cs Assets/_Project/Networking/CompositeNetworkSession.cs Assets/_Project/Tests/EditMode/Pcg32Tests.cs Assets/_Project/Tests/EditMode/LogOnceTests.cs Assets/_Project/Tests/EditMode/LogOnceTests.cs.meta Assets/_Project/Tests/EditMode/EventIsolationTests.cs Assets/_Project/Tests/EditMode/EventIsolationTests.cs.meta Assets/_Project/Tests/EditMode/AudioBusTests.cs Assets/_Project/Tests/EditMode/AudioBusTests.cs.meta; git commit -m "m2(carry): Pcg32 float range fix, LogOnce, AudioBus warn limit, per-subscriber event isolation, EditMode asmdef refs" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task T2 - Carry: split `LocalNetworkSession` and `UiFlowController` (behaviour unchanged)

**Goal:** `LocalNetworkSession` becomes three partial files; `UiFlowController` becomes the screen router plus two internal helpers (`UiSessionActions`, `UiFocusRouter`) and gains the `IHudExtension` seam for T25/T26. Public surface and runtime behaviour are unchanged (C 7.2, DA21).

- **Status:** in progress (implementer has rulings A-Q1 and A-Q2).
- **Owned files (C 12):** `Networking/LocalNetworkSession.cs`, `Networking/LocalNetworkSession.Start.cs` (new), `Networking/LocalNetworkSession.NgoEvents.cs` (new), `UI/UiFlowController.cs`, `UI/UiSessionActions.cs` (new), `UI/UiFocusRouter.cs` (new), `UI/IHudExtension.cs` (new).
- **Shared edits (C 2.6):** `LocalNetworkSession.cs` (T1 then T2); `UiFlowController.cs` (T2 split; later T25 and T26 add one hook line each).
- **Depends on:** T1 committed **and reviewed** (both edit `LocalNetworkSession.cs`).
- **Implementer:** Sonnet. **Reviewer:** Opus high.

### T2.1 Constraints that bite here (C 2.8)
- Moves are verbatim. Never reformat, rename locals, reorder statements or cut comments to hit a line count; report the count instead (rulings A-Q1, A-Q2 set the targets).
- #3 cancel convention and the D37 ordering in `SetupAndStartAsync` ("after StartHost returned, never from inside OnServerStarted") are preserved by not editing those bodies.
- #9: PlayMode warning-free and LogError-free (the PlayMode tests are the behaviour proof).
- Every new file states authority in its class comment (CLAUDE.md): partial files point at the main file's comment, the UI helpers say "Authority: client-only (local UI)".
- DA10: after the split, `ToastRequested` is raised by `UiSessionActions` only (inside `UI/`, at T2; T25 later adds a publisher in `UI/Fishing/FishingHudPresenter.cs`, ruling G-Q1).

### T2.2 `LocalNetworkSession` member-to-file map
All three files declare `public sealed partial class LocalNetworkSession` in `namespace FishGame.Networking`; only the main file lists `: INetworkSession` and keeps the authority comment (lines 12-15). Each part file starts with the same style as `UgsNetworkSession.NgoEvents.cs`: `// Part of LocalNetworkSession (see LocalNetworkSession.cs for the authority comment): <what>. Split only to keep each file near ~300 lines.` Each file has only the `using`s it needs.

| File | Members (M1 line ranges, all verbatim; T1's two isolated raise sites included) |
|---|---|
| `LocalNetworkSession.cs` (responsibilities 1, 4, 6) | all fields `:18-33`; ctor `:35-39`; `Port`, `ActivePort`, `State`, `Mode`, `IsHost`, `JoinCode`, `StateChanged`, `Ended`, `Nm`, `IsLive`, `DevAllowed` `:41-64`; `HostOnlineAsync`, `JoinOnlineAsync`, `StartLocalAsync`, `JoinDirectAsync`, `SetJoinableAsync`, `LeaveAsync`, `Dispose` `:66-136`; `Established`, `Fail`, `RequestShutdown`, `TeardownAsync`, `OnStateMachineChanged`, `RaiseEnded` `:342-459`; `CancelAttemptToken`, `DisposeAttemptToken` `:470-494` |
| `LocalNetworkSession.Start.cs` (responsibilities 2, 3; ruling A-Q1) | `AttemptAsync`, `PostCancel`, `CancelAttempt` `:138-202`; `SetupAndStartAsync` `:204-294`; `StartNgo` `:296-309` (with its comment); `ShutdownNgoAsync` `:311-326`; `WatchTimeoutAsync` `:328-335`; `IsCurrent` `:337-340` (only `SetupAndStartAsync` calls it); `UninstallApprover` `:461-468` |
| `LocalNetworkSession.NgoEvents.cs` (responsibility 5) | `SubscribeNetwork` `:496-503`; `UnsubscribeNetwork` `:505-516` (with its comment); `OnConnectionEvent` `:518-534`; `OnClientStopped` `:536-547` (with its comment); `OnServerStopped` `:549-556`; `SecondsSinceBegin` `:558-561` |

Keep the member order inside each file as in M1. Expected sizes: main about 270 lines, Start about 230, NgoEvents about 85; every file is under 300 (C 7.2, ruling A-Q1: responsibility 2, the attempt lifecycle of about 75 lines, moves with the NGO start into `.Start.cs`). Do not move more than the table says; report the three counts.

### T2.3 `UiFlowController` split

**Stays in `UiFlowController.cs`** (public `CurrentScreen`, `Initialize`, `Show` unchanged in signature and behaviour; `[DefaultExecutionOrder(-500)]` kept): service fields; document/panel/screen element fields; presenter fields; `_screen`, `_settingsReturn`, `_pendingScreen`, `_hasPendingScreen`, `_initialized`, `_started`, `_bound`, `_inGame`, `_screenChangedFrame`; `Initialize` (adds `_app = app;`), `Show`, `Start`, `Bind`, `BindScreens`, `Update`, `HandleBack`, `OpenSettings`, `CloseSettings`, `OnActiveSceneChanged`, `OnSessionEnded`, `OnSessionStateChanged`, `OnSettingsChanged`, `ApplyUiScale`, `SetSoloPaused`, `PlayClick`, `OnToastSound`, `ScreenForScene`, `QuitApplication`, `OnApplicationQuit`, `OnDestroy`, and the new HUD-extension members (T2.4).

**Every subscription stays where it is:** `_settings.Changed`, `_session.StateChanged`, `_session.Ended`, `SceneManager.activeSceneChanged` in `Initialize` (same order), the click-sound loop and the toast-sound subscription at the end of `BindScreens` (after every presenter exists). Only handler bodies route into the helpers. This keeps invocation order relative to `SceneFlow` and other subscribers unchanged.

**`UI/UiSessionActions.cs`** - `internal sealed class UiSessionActions` (plain C#). Moved verbatim: `StartAttempt` (async void), `HostAsync`, `OnJoinRequested`, `CancelAttempt`, `LeaveSession` (async void), `ToggleJoins` (async void), `ShowPauseData`, field `_attempt` (the attempt CTS) and `_leaving`.
- Constructor arguments: `INetworkSession session, EventBus bus, MainMenuPresenter menu, BusyPresenter busy, PausePresenter pause, Action<UiScreen> show, Func<UiScreen> currentScreen, Action<bool, bool> setMenuActions, Func<bool> isBound, Func<bool> isLive`. The controller passes `Show`, `() => _screen`, `_focus.SetMenuActions`, `() => _bound`, `() => _initialized && _bound`.
- `internal void Wire()` subscribes exactly the M1 session wiring from `BindScreens`: `menu.HostRequested` (`StartAttempt("busy.creating", HostAsync)`), `menu.JoinRequested` (`OnJoinRequested`), `menu.SoloRequested`, `menu.DevHostRequested`, `menu.DevJoinRequested` (lambdas as in `:191-196`), `busy.CancelRequested` (`CancelAttempt`), `pause.LeaveRequested` (`LeaveSession`), `pause.JoinsToggleRequested` (`ToggleJoins`), `pause.Copied` (the `pause.copied` toast, `:212`). Each of these events has exactly one subscriber, so moving the subscription does not change any order. `menu.SettingsRequested`, `menu.QuitRequested`, `settingsPresenter.BackRequested`, `rebind`, `pause.ResumeRequested`, `pause.SettingsRequested` stay wired in the controller.
- `internal bool HasAttempt => _attempt != null;` `internal bool IsLeaving => _leaving;` `internal void CancelForDestroy() => _attempt?.Cancel();`
- Call-site substitutions inside the moved bodies (the only allowed edits): `_bus`/`_session`/`_menu`/`_busyPresenter`/`_pausePresenter` -> the ctor fields; `Show(x)` -> `_show(x)`; `_screen` -> `_currentScreen()`; `SetMenuActions(a, b)` -> `_setMenuActions(a, b)`; `if (!_initialized || !_bound) return;` -> `if (!_isLive()) return;`; `if (_bound)` in `LeaveSession` -> `if (_isBound())`; `if (_initialized && _bound)` in its `finally` -> `if (_isLive())`. The delegates read the controller's live fields at call time (after each `await`), never a copied bool.

**`UI/UiFocusRouter.cs`** - `internal sealed class UiFocusRouter`. Moved verbatim: `enum MenuFocus`, fields `_menuFocus`, `_menuActionsEnabled` (initial `true`), `_menuFocusPending`; `SetMenuActions`, `FocusScreen`, `FocusedElement`, `BlurFocus`, `IsMenuScreen` (becomes `internal static`, the controller calls `UiFocusRouter.IsMenuScreen`).
- Constructor arguments: `VisualElement root, VisualElement mainMenu, MainMenuPresenter menu, BusyPresenter busy, SettingsPresenter settings, PausePresenter pause, Func<UiScreen> currentScreen, Func<bool> isBound`.
- New one-line members that replace inline controller code: `internal void ClearPending()` (the `_menuFocusPending = false;` at the top of `Show`, `:96`, stays at that exact point as `_focus.ClearPending();`); `internal void PreferFocusFor(EndReason reason)` (the two lines `:390-391` of `OnSessionEnded`); `internal void RefocusIfLost(UiScreen screen, InputDeviceKind lastDevice)` (`:233-234` of `Update`, same condition including `IsMenuScreen(screen)` and `FocusedElement() == null`).
- `FocusScreen` keeps its guard `if (!_isBound() || _currentScreen() != screen) return;`. `Show` still schedules `_root.schedule.Execute(() => _focus.FocusScreen(target))`.

**Construction order in `BindScreens`:** after all presenters exist and before the click-sound loop: `_focus = new UiFocusRouter(...)`, `_actions = new UiSessionActions(...)`, `_actions.Wire()`. Both exist before `_bound = true` and before the first `Show`. Handlers that run before binding (`OnSessionEnded`, `OnSessionStateChanged`, `OnActiveSceneChanged`) already return on `!_bound`, so they never touch a null helper.

**Controller call sites after the split:** `Show(MainMenu)`: `_focus.SetMenuActions(_session.State == ConnState.Offline && !_actions.HasAttempt, false);` `Show(Pause)`: `_actions.ShowPauseData();` `HandleBack` Busy: `_actions.CancelAttempt();` `OnSessionStateChanged`: `_focus.SetMenuActions(current == ConnState.Offline && !_actions.HasAttempt, true);` `OnSessionEnded`: `_focus.PreferFocusFor(reason);` then the Busy check. `OnDestroy`: `_actions?.CancelForDestroy();` at the position of `_attempt?.Cancel();` (**not** `CancelAttempt()`, which would call `LeaveSession` when no attempt exists and change PlayMode teardown).

Expected sizes: `UiFlowController.cs` under 400 lines (about 350-365; C 7.2 target is < 400 with no fifth file, ruling A-Q2), `UiSessionActions.cs` about 150, `UiFocusRouter.cs` about 85, `IHudExtension.cs` about 15. Report the counts.

### T2.4 `IHudExtension` seam (C 7.2; used by T25 and T26)
```csharp
// UI/IHudExtension.cs
internal interface IHudExtension
{
    void Bind(VisualElement root, ServiceRegistry app);
    void Tick(float dt);
    void Unbind();
}
// UiFlowController
internal void AttachHudExtension(IHudExtension ext);
```
Behaviour (decided here; T25/T26 rely on it):
- `ext == null` -> `ArgumentNullException`. Attaching the same instance twice is ignored. After `OnDestroy` has run, attaching is ignored (a private `_destroyed` flag set at the top of `OnDestroy`, before its `_initialized` early return).
- Extensions are kept in attach order in a private `List<IHudExtension>`.
- A private flag `_extensionsBound` is set by the extension bind loop at the end of `BindScreens` (after the toast-sound subscription), which binds every extension attached so far, in attach order. `AttachHudExtension` binds immediately (`ext.Bind(_root, _app)`) whenever `_extensionsBound` is true, and otherwise only stores the extension. The trigger is this flag, not `_bound` (which becomes true only after `BindScreens` returns), so a T25/T26 hook placed anywhere in `Initialize`, `BindScreens` or later is bound exactly once. `_app` is the `ServiceRegistry` stored in `Initialize`.
- `Tick(Time.unscaledDeltaTime)` runs every `Update` inside the existing `if (_inGame)` block, after `_nameplates.Tick(...)`, in attach order (HUD visible: Hud, Pause, Settings-over-game). The game never changes `Time.timeScale` (`PauseState` comment), and unscaled time keeps UI animation independent of it anyway.
- `Unbind()` runs in `OnDestroy` for bound extensions in reverse attach order, right after `_hudPresenter?.Dispose()`.
- No try/catch around extension calls (same as the presenters).
- T2 attaches nothing. Behaviour with zero extensions is identical to M1. This seam is proven only by compile and review until T25.

### T2.5 Steps
1. Baseline into `<scratch>`: `grep -nE '^\s*(public|internal|protected)' Assets/_Project/Networking/LocalNetworkSession.cs Assets/_Project/UI/UiFlowController.cs > <scratch>/t2-public.before`.
2. Split `LocalNetworkSession` (T2.2). `compile` -> PASS.
3. Split `UiFlowController` (T2.3), add `IHudExtension` (T2.4). `compile` -> PASS.
4. Verbatim-move check (0.5) for both splits; the residue must be only what 0.5 and T2.3 allow. Save both diffs in `<scratch>` and paste them into your report.
5. Public-surface check: the same grep over the new files shows the same `public` members as the baseline (`UiFlowController`: `CurrentScreen`, `Initialize`, `Show`; `LocalNetworkSession`: ctor, `Port`, `ActivePort`, `State`, `Mode`, `IsHost`, `JoinCode`, `StateChanged`, `Ended`, the seven methods); the only new non-private members are those named in T2.3/T2.4.
6. `grep -n "new ToastRequested" Assets/_Project/UI/*.cs` -> hits only `UiSessionActions.cs` (4 hits).
7. `check -TimeoutMinutes 15` -> PASS. Scene identity check (0.3) -> `SCENE SAME` x3; restore scenes.
8. `wc -l` of the seven files into the report.
9. Byte scan (no byte > 0x7F). Commit (T2.9).

### T2.6 Tests
No new tests (C 10 has none for T2; the split is behaviour-preserving). **Existing tests that prove behaviour is unchanged:**
- PlayMode (all five): `Bootstrap_Loads_Menu` (UiFlowController sets `MenuOpen` on the main menu; Offline session), `Solo_Start_SpawnsOneOwnedPlayerWithCamera` and `Solo_LeaveThenRestart_Works` (LocalNetworkSession start, port loop, approver, NGO events, teardown, Ended; UiFlowController reacting to scene and session changes), `FakeInput_MoveForward_MovesPlayerMoreThanOneMeterInOneSecond`, `TeleportRpc_OnDeck_LandsWithinFiveCentimetersWithZeroVelocity` (HUD state, `MenuOpen == false` in game).
- EditMode full run (nothing there reaches Networking or UI; it guards compile and the rest of the project).
- Not covered by any automated test: menu buttons (`StartAttempt`), Busy cancel, Leave from Pause, join-code gate, focus. The controller runs the manual smoke list (T2.7) once T2 is committed.

### T2.7 Manual smoke list (controller, in the editor, about 5 minutes; not the implementer)
1. Play from Bootstrap: main menu shows, a button has focus with a gamepad (or keyboard Tab).
2. Play Solo: Busy "Loading" then HUD. Esc: Pause. Esc: HUD. Esc, Settings, change UI scale: scale applies live; Back: Pause; settings file timestamp updated.
3. Pause -> Leave: back on the main menu, buttons enabled, no toast, no errors in the Console.
4. Type `abc` in the join-code field, Join: toast "code not found", focus back in the field with the text kept.
5. Dev Host Direct, then Cancel on the Busy screen as fast as possible: main menu, no toast.
6. Quit button in the editor stops play mode; the Console shows no exceptions during any step.

### T2.8 Acceptance (reviewer)
- Member-to-file maps (T2.2, T2.3) followed; verbatim-move diffs contain only the allowed residue; no statement reordering; comments kept.
- Subscriptions unchanged in place and order; `OnDestroy` uses `CancelForDestroy`; `Show` clears the pending focus at the same point; live-state delegates used after awaits.
- Public members unchanged; new members internal and exactly those listed; `IHudExtension` behaviour as T2.4.
- At T2, `ToastRequested` is raised only in `UiSessionActions.cs` (`grep -n "new ToastRequested" Assets/_Project/UI/*.cs`, non-recursive). T25 later adds a publisher under `UI/Fishing/`; the final branch review must not flag that.
- `check` PASS; scenes restored; line counts reported (main `LocalNetworkSession.cs` < 300, `UiFlowController.cs` < 400).

### T2.9 Commit
```powershell
git add Assets/_Project/Networking/LocalNetworkSession.cs Assets/_Project/Networking/LocalNetworkSession.Start.cs Assets/_Project/Networking/LocalNetworkSession.Start.cs.meta Assets/_Project/Networking/LocalNetworkSession.NgoEvents.cs Assets/_Project/Networking/LocalNetworkSession.NgoEvents.cs.meta Assets/_Project/UI/UiFlowController.cs Assets/_Project/UI/UiSessionActions.cs Assets/_Project/UI/UiSessionActions.cs.meta Assets/_Project/UI/UiFocusRouter.cs Assets/_Project/UI/UiFocusRouter.cs.meta Assets/_Project/UI/IHudExtension.cs Assets/_Project/UI/IHudExtension.cs.meta; git commit -m "m2(carry): split LocalNetworkSession into partials and UiFlowController into router, session actions, focus router; add IHudExtension seam" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task T3 - Carry: PlayMode harness hardening + leave-after-Connected test

**Goal:** No fixed negative sleeps, no unguarded game-time waits, `Await` cancels its operation on timeout, teardown asserts both service roots are clear and resets `SuppressDevAutoStart`, and the missing "leave right after Connected" case is covered (C 7.3, DA22).

- **Owned files (C 12):** `Tests/PlayMode/PlayModeHarness.cs`, `Tests/PlayMode/SoloSessionTests.cs`, `Tests/PlayMode/PlayerMovementTests.cs`.
- **Shared edits (C 2.6):** these three (T3 hardening; T19 adds new PlayMode files only).
- **Depends on:** T2 committed.
- **Implementer:** Sonnet. **Reviewer:** Opus high.

### T3.1 Constraints that bite here (C 2.8)
- #9: PlayMode fails on any unexpected `Debug.LogError` or `LogException`; output warning-free. `IInputService` fakes are installed before the player spawns (keep `InstallFakeInput` before `StartSoloAndWaitForPlayer`).
- #3: a cancelled `StartLocalAsync` faults with `NetworkSessionException(EndReason.UserLeft)`, never a Canceled task.
- T3 owns test files only. If the new test fails because of a product defect, stop and report (do not edit Networking/App/UI).

### T3.2 Current code (facts)
- `PlayModeHarness.cs`: `LoadBootstrapAndWaitForMenu` `:23-38` (leak guard checks `AppBootstrap.Current` and `NetworkManager.Singleton` only; sets `SuppressDevAutoStart = true` at `:31`, never reset); `StartSoloAndWaitForPlayer` `:40-56` (`CancellationToken.None` at `:50`); `TearDownApp` `:78-149` (realtime loops, asserts `Current`, `Singleton`, `PlayerNetState.Active`; not `ServiceRoots`); `WaitUntil` `:151-162` (realtime); `Await` `:164-185` (no cancel on timeout).
- `SoloSessionTests.cs:31`: `yield return new WaitForSecondsRealtime(0.5f);` (fixed negative wait before counting players).
- `PlayerMovementTests.cs:36-43`: loops on `Time.deltaTime` for 1 s with no realtime guard; `:70`: `yield return new WaitForSeconds(0.4f);` (game time, no guard).
- `ServiceRoots.Game` is cleared only by `GameScope.OnDestroy` (scene unload), not by `DestroyForTests`. After a leave, `SceneFlow` loads Menu on the Offline transition (`SceneFlow.cs:63-65`) and returns to Menu if a gameplay scene activates after Offline (`:47-49`).

### T3.3 Harness API (C 7.3; M1 members kept)
```csharp
public static IEnumerator WaitFrames(int frames, float minRealtimeSeconds);
public static IEnumerator WaitGameSeconds(float seconds, float realtimeTimeoutSeconds, string failMessage);
public static IEnumerator Await(Task task, float timeoutSeconds, CancellationTokenSource cancelOnTimeout = null);
public static IEnumerator StartSoloAndWaitForPlayer();
public static IEnumerator TearDownApp();
public static IEnumerator LoadBootstrapAndWaitForMenu();
```
The M1 `Await(Task, float)` call sites keep compiling through the optional parameter. `DefaultTimeoutSeconds`, `LeaveAndWaitForMenu`, `WaitUntil` unchanged.

Behaviour:
- **`WaitFrames(frames, minRealtimeSeconds)`**: record `Time.frameCount` and `Time.realtimeSinceStartupAsDouble`; `yield return null` until **both** at least `frames` frames have passed **and** at least `minRealtimeSeconds` of realtime have elapsed. `frames <= 0` and `minRealtimeSeconds <= 0` returns without yielding. Always terminates (both conditions are monotonic).
- **`WaitGameSeconds(seconds, realtimeTimeoutSeconds, failMessage)`**: same loop shape as M1 `PlayerMovementTests:38-43` (check the realtime deadline, `yield return null`, then `elapsed += Time.deltaTime`) until `elapsed >= seconds`. If the realtime deadline passes first: `Assert.Fail($"{failMessage} (game time {elapsed:F3} of {seconds} s after {realtimeTimeoutSeconds} s realtime; timeScale {Time.timeScale})")`.
- **`Await(task, timeoutSeconds, cancelOnTimeout)`**: M1 loop. On timeout: `cancelOnTimeout?.Cancel()`; then observe a late fault so it cannot surface as an unobserved exception in a later test: `task.ContinueWith(t => { var unused = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);`; then `Assert.Fail($"Task did not complete within {timeoutSeconds} s")`. Faulted/Canceled handling unchanged.
- **`StartSoloAndWaitForPlayer`**: `var cts = new CancellationTokenSource();` pass `cts.Token` to `StartLocalAsync(false, ...)` and `cts` to `Await(..., SessionTimeoutSeconds, cts)`; after `Await` returns, `cts.Dispose()`. (On the timeout path the CTS is not disposed; it holds no timer, so that is harmless.)
- **`LoadBootstrapAndWaitForMenu`**: add a third leak guard before loading: `Assert.That(ServiceRoots.Game, Is.Null, "A previous test leaked ServiceRoots.Game (a gameplay scene's GameScope survived)")`. Everything else unchanged.
- **`TearDownApp`**: M1 body, with three changes:
  1. The realtime wait loop `:125` also waits while `ServiceRoots.App != null || ServiceRoots.Game != null` (same `TeardownTimeoutSeconds` deadline; this is the bounded wait for the Spot scene to unload).
  2. `AppBootstrap.SuppressDevAutoStart = false;` right after that loop and the two `yield return null`, before any problem is turned into `Assert.Fail` (nothing between the start of the method and this line can throw: `LeaveAsync` is already in try/catch).
  3. After the three M1 problem checks, two more in the same `problem == null && ...` style: `ServiceRoots.App` still set -> `"ServiceRoots.App is still set after DestroyForTests"`; `ServiceRoots.Game` still set -> `"ServiceRoots.Game is still set (gameplay scene not unloaded)"`.
  The comment at `:77` ("Never asserts before DestroyForTests ...") stays true.

### T3.4 Call-site changes
- `SoloSessionTests.cs:31` -> `yield return PlayModeHarness.WaitFrames(30, 0.5f);` (keeps at least the M1 0.5 s realtime window, and at least 30 frames for a duplicate spawn to show up). Keep the comment above it.
- `PlayerMovementTests.cs:35-43` -> keep the `fake.Frame = ...` lines; replace the loop with:
  `float t0 = Time.time; int f0 = Time.frameCount; yield return PlayModeHarness.WaitGameSeconds(1f, PlayModeHarness.DefaultTimeoutSeconds, "Forward walk never accumulated 1 s of game time"); float elapsed = Time.time - t0; int frames = Time.frameCount - f0;` so the `detail` message keeps both numbers.
- `PlayerMovementTests.cs:70` -> `yield return PlayModeHarness.WaitGameSeconds(0.4f, PlayModeHarness.DefaultTimeoutSeconds, "Walk precondition never accumulated 0.4 s of game time");`

### T3.5 New test (C 10.1, 10.4): `SoloSessionTests.Solo_LeaveRightAfterConnected_EndsOnMenu`
Purpose: leave on the first frame the session is Connected, while `SceneFlow`'s NGO load of `Spot_LakeDock` is still in flight. The session must end Offline on an idle Menu with no player and no `GameScope`, and stay there.

Steps (inline; the trigger frame must not pass through a nested enumerator):
1. `yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();`
2. `app`, `session` (`INetworkSession`), `local` (`LocalNetworkSession`, `local.Port = NetworkConstants.TestPort`), `nm = app.NetworkManager`. Assert `session.State == Offline`.
3. `var cts = new CancellationTokenSource(); Task start = session.StartLocalAsync(false, cts.Token);`
4. Inline loop with a realtime deadline of 20 s: `while (session.State != ConnState.Connected && !start.IsCompleted) { if (deadline passed) Assert.Fail("Solo start never reached Connected"); yield return null; }`
5. On that same frame, with no yield: `Assert.That(start.IsFaulted, Is.False, ...)`, `Assert.That(session.State, Is.EqualTo(ConnState.Connected), ...)`; record `string sceneAtLeave = SceneManager.GetActiveScene().name;` and `TestContext.WriteLine("scene at leave: " + sceneAtLeave);` (diagnostic only, not asserted: the load may or may not have activated yet); then `Task leave = session.LeaveAsync();`
6. `yield return PlayModeHarness.Await(leave, 20f);` then `yield return PlayModeHarness.Await(start, 20f);` (start already succeeded at Established; this only proves it did not fault). `cts.Dispose()`.
7. `yield return PlayModeHarness.WaitUntil(() => session.State == ConnState.Offline && !nm.ShutdownInProgress && !nm.IsListening && PlayerNetState.Local == null && SceneManager.GetActiveScene().name == SceneNames.Menu && ServiceRoots.Game == null, 20f, "Did not settle on an idle Menu after leaving right after Connected");`
8. Stability window: `yield return PlayModeHarness.WaitFrames(30, 1.0f);` (a late scene activation would show up here; `SceneFlow` must have sent it back).
9. Re-assert, each with its own message: active scene is `SceneNames.Menu`; `session.State == Offline`; `PlayerNetState.Local == null` and `PlayerNetState.Active.Count == 0`; `ServiceRoots.Game == null`; `nm.IsListening == false`, `nm.ShutdownInProgress == false`; `app.Services.Get<IInputService>().MenuOpen == true` (the main menu is showing).
10. TearDown is the class's existing `[UnityTearDown]`.

If the test fails at step 7 or 9 on a product path (for example the gameplay scene stays active or a player spawns after Offline), stop and report with the log; the fix belongs to the owner of `SceneFlow`/`LocalNetworkSession` (escalation), not to T3.

### T3.6 Steps (TDD)
1. Edit the call sites (T3.4) and add the new test (T3.5) first.
2. Red: `test-play -Filter "FishGame.Tests.PlayMode" -TimeoutMinutes 15` -> compile failure (`error CS0117` for `WaitFrames`, `WaitGameSeconds`).
3. Implement the harness (T3.3).
4. Green: same command -> `FG RESULT test-play PASS total=6 passed=6 failed=0`.
5. `check -TimeoutMinutes 15` -> PASS. Scene identity check (0.3) -> restore scenes.
6. Byte scan; commit (T3.9).

### T3.7 Tests
- New: `Solo_LeaveRightAfterConnected_EndsOnMenu` (above). Expected to pass on its first green run (regression guard for the leave-during-load path).
- **Existing tests that prove behaviour is unchanged (all must stay green, now 6 PlayMode tests):** `Bootstrap_Loads_Menu`, `Solo_Start_SpawnsOneOwnedPlayerWithCamera`, `Solo_LeaveThenRestart_Works`, `FakeInput_MoveForward_MovesPlayerMoreThanOneMeterInOneSecond`, `TeleportRpc_OnDeck_LandsWithinFiveCentimetersWithZeroVelocity`. Each test's `[UnityTearDown]` now also proves `ServiceRoots.App/Game` clear.
- The harness guards themselves (timeouts firing) cannot be unit-tested: NUnit records `Assert.Fail` even when caught. The reviewer checks them by reading.

### T3.8 Acceptance (reviewer)
- No `WaitForSeconds`/`WaitForSecondsRealtime` left in the three files; every game-time wait has a realtime guard.
- `Await` cancels then observes the fault then fails; `StartSoloAndWaitForPlayer` passes the CTS to both.
- `TearDownApp` waits on `ServiceRoots.App/Game`, resets `SuppressDevAutoStart` before any `Assert.Fail`, asserts both roots null.
- New test leaves on the detection frame (no yield between seeing Connected and `LeaveAsync`), has the stability window and all step-9 asserts.
- `check` PASS (6 PlayMode tests); scenes restored.

### T3.9 Commit
```powershell
git add Assets/_Project/Tests/PlayMode/PlayModeHarness.cs Assets/_Project/Tests/PlayMode/SoloSessionTests.cs Assets/_Project/Tests/PlayMode/PlayerMovementTests.cs; git commit -m "m2(carry): harden PlayMode harness (frame+time waits, realtime guards, cancel on timeout, service-root teardown asserts) and add leave-right-after-Connected test" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task T4 - Carry: builders load after `NewScene`; mesh, missing-script and Bootstrap-reference validators

**Goal:** `SceneBuilder.BuildBootstrap` and `LakeDockBuilder.Build` load every asset after `EditorSceneManager.NewScene` (C 2.8 #14, DA23); three new validators catch the failure class that bit M1 (destroyed references saved as null) and missing scripts in scenes.

- **Status:** done (committed `43178ee`).
- **Owned files (C 12):** `Editor/Builders/SceneBuilder.cs`, `Editor/Builders/LakeDockBuilder.cs`, `Editor/Validation/Validators.cs`, `Editor/Validation/ValidationRunner.cs`.
- **Tests owned:** `Tests/EditMode/ValidationTests.cs` (add three).
- **Shared edits (C 2.6):** `SceneBuilder.cs` (T4 only in M2); `LakeDockBuilder.cs` (T4 now; T19, T22 add hook lines and T28 rebuilds it later: keep the edit to the load block at the top of `Build`); `Validators.cs` (T4; later validators go in new files); `ValidationRunner.cs` (T4 adds three lines; T14 and T20 add hooks later). `AssetPaths.cs`: no change (C 2.6 "T4 (none needed)").
- **Depends on:** T1 committed (may run while T1 is in review; skeleton 5).
- **Implementer:** Sonnet. **Reviewer:** Opus high.

### T4.1 Constraints that bite here (C 2.8)
- #14 (the point of this task). #4: no NetworkObject is created or re-added here. #6: scene churn restored after the scene check. #11: check the lock (the script does). #21: logs in `<scratch>`.
- Output of `build` and the validators must stay warning-free; EditMode tests fail on an unexpected `Debug.LogError`.

### T4.2 Current code (facts)
- `SceneBuilder.BuildBootstrap` loads `sessionStatePrefab`, `inputActions`, `registry`, `prefabList`, `panelSettings`, `appRoot` at `:31-41`, calls `NewScene` at `:43`, reloads only `panelSettings` at `:45` (comment `:44`). `BuildMenu` loads nothing.
- `LakeDockBuilder.Build` loads `playerPrefab`, `bellPrefab` at `:20-21`, builds and saves the two meshes at `:22-24`, calls `NewScene` at `:26`, then uses all four after it (`:29`, `:34-35`, `:40`). Materials come from `MaterialLibrary.Get` after `NewScene` already.
- `Validators.cs` header (`:1-3`) says the validators "never open a scene". `NetworkPrefabs()` already checks missing scripts in prefabs under `Assets/_Project` (`:99-101`) and text-checks some Bootstrap/LakeDock references (`:106-111`); nothing checks meshes, scene missing scripts, or `AppBootstrap.inputActions`/`contentRegistry`.
- `ValidationRunner.Run` (`:10-21`) runs the seven M1 validators; `ProjectBuilder.RunAll` runs it as its last step during `build`.

### T4.3 Builder changes
**`SceneBuilder.BuildBootstrap`:** call `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` first, then load all six assets with the same null checks and the same `FileNotFoundException` messages, in the M1 order, then build the scene exactly as now. Delete the reload at `:44-45` (now redundant). Add one comment line above the loads: `// Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references (C 2.8 #14).` Nothing else changes.

**`LakeDockBuilder.Build`:** keep `LakeDockMeshes.BuildAsset(...)` x2 and `AssetDatabase.SaveAssets()` before `NewScene` (they write the mesh assets; keep the comment). After `NewScene`, load everything the scene uses:
```csharp
NetworkObject playerPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.PlayerPrefab);
NetworkObject bellPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.DockBellPrefab);
Mesh lakebedMesh = LoadMesh(AssetPaths.LakebedMesh);   // private static: LoadAssetAtPath<Mesh>, FileNotFoundException if null
Mesh shoreMesh = LoadMesh(AssetPaths.ShoreMesh);
```
The `BuildAsset` return values are discarded (do not keep the pre-`NewScene` references). The rest of `Build` (order of `Root`, `AddSpawner`, ..., `AddBellAnchor`, seeds, streams) is unchanged, so the layout and draw order are unchanged.

### T4.4 Validators (C 7.4)
Add to `Validators` (each `public static List<string>`, never throws for a missing file; a missing file is reported by `BuildScenes`/`NetworkPrefabs` already, so skip it here):

- **`MeshReferences()`**: for every build scene and every prefab under `Assets/_Project/Prefabs`, every `MeshFilter.sharedMesh` and every `MeshCollider.sharedMesh` (including inactive children) is non-null. Error: `"<asset path>: <hierarchy path> MeshFilter has no mesh"` (or `MeshCollider`). Hierarchy path = names from the root joined by `/`.
- **`MissingScripts()`**: for every GameObject (including inactive) in every build scene and every prefab under `Assets/_Project/Prefabs`, `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0`. Error: `"<asset path>: <hierarchy path> has <n> missing script(s)"`. (`NetworkPrefabs()` keeps its own prefab check unchanged.)
- **`BootstrapReferences()`**: in `Bootstrap.unity`, exactly one `AppBootstrap` (`FishGame.App`, already referenced by the Editor asm). Through `new SerializedObject(bootstrap)`: property `inputActions` has a non-null `objectReferenceValue` whose `AssetDatabase.GetAssetPath` is `InputActionNames.AssetPath`; property `contentRegistry` non-null with path `AssetPaths.ContentRegistryAsset`. Errors: `"Bootstrap AppBootstrap.inputActions is null"`, `"... references <path>, expected <path>"`, same for `contentRegistry`, and `"Bootstrap has <n> AppBootstrap components, expected 1"`.

Shared private helpers (in `Validators.cs`):
- `const string PrefabsFolder = "Assets/_Project/Prefabs";` (private; AssetPaths is not edited).
- Build scene paths = `AssetPaths.ScenesFolder + "/" + name + ".unity"` for each `SceneNames.BuildOrder` entry (same list as `BuildScenes()`); skip paths whose file does not exist.
- `ForEachSceneObject(string scenePath, List<string> errors, Action<GameObject> visit)`:
  - If `EditorApplication.isPlayingOrWillChangePlaymode`: add `"scene checks need edit mode"` once and return.
  - `Scene scene = SceneManager.GetSceneByPath(scenePath); bool opened = !(scene.IsValid() && scene.isLoaded);` if `opened`, `scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);`
  - In `try`: visit every `Transform` under every root (`GetComponentsInChildren<Transform>(true)`). In `finally`: if `opened`, `EditorSceneManager.CloseScene(scene, true)`. Never save; never close a scene that was already loaded (a scene the user has open, or the scene `BuildAll` just built).
  - One scene open at a time.
- Prefab visit: `AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder })` -> `LoadAssetAtPath<GameObject>` -> children with `true`.
- Update the file header comment: the M1 validators read persisted text; `MeshReferences`, `MissingScripts` and `BootstrapReferences` open build scenes additively (read-only, closed without saving, already-open scenes left alone). If a build scene is already open with unsaved edits, the in-memory state is what gets checked.

**`ValidationRunner.Run`** adds, after the seven M1 lines and in this order: `Add(all, "MeshReferences", Validators.MeshReferences());`, `Add(all, "MissingScripts", Validators.MissingScripts());`, `Add(all, "BootstrapReferences", Validators.BootstrapReferences());`. So `build` now fails (`FG BUILDALL FAIL`) on any of them.

If opening `Bootstrap.unity` additively in an EditMode run logs an error from a package (for example NGO's `NetworkManager` editor checks), the EditMode tests would fail on it: use the YAML text scan of `m_Mesh: {fileID: 0}` and `m_Script` GUIDs instead for the affected scene(s); no stop and no report needed (ruling A-Q5, C 7.4 allows either approach). Say in the task report which approach each validator uses.

### T4.5 Steps (TDD)
0. Baseline (0.3a) if T4 runs before T1 and it has not been recorded yet.
1. Add the three tests to `ValidationTests.cs` (T4.6).
2. Red: `test-edit -Filter "FishGame.Tests.EditMode.ValidationTests"` -> compile failure (`error CS0117`: `Validators` has no `MeshReferences`, ...).
3. Implement the validators and the runner lines (T4.4). `compile` -> PASS.
4. Fix the two builders (T4.3).
5. `build` -> `FG RESULT build PASS` (the build's own Validate step now runs the three new validators).
6. Green: `test-edit -Filter "FishGame.Tests.EditMode.ValidationTests"` -> `total=10 passed=10 failed=0` (7 M1 + 3 new).
7. Scene identity check (0.3) -> `SCENE SAME` x3 expected (the M1 scenes happened to survive the load order; the fix makes it guaranteed). Restore scenes. A `SCENE DIFF` that shows a reference changing from `{fileID: 0}` (null) to a guid reference is a real fix: stop and report it rather than restoring.
8. `check -TimeoutMinutes 15` -> PASS; scene check again; restore.
9. Byte scan; commit (T4.8).

### T4.6 Tests (exact names)
`ValidationTests.cs` (add three, same one-line style as the M1 seven):
- `Validation_MeshReferences_Pass` -> `AssertNoErrors(Validators.MeshReferences())`
- `Validation_MissingScripts_Pass` -> `AssertNoErrors(Validators.MissingScripts())`
- `Validation_BootstrapReferences_Pass` -> `AssertNoErrors(Validators.BootstrapReferences())`

They are regression guards: they pass on the first green run against the rebuilt assets. A negative case needs a deliberately broken asset in the project (the `AssetsLayout` validator and the build would object), and the Editor assembly exposes no internals to tests, so there are none; the reviewer checks the scan logic by reading (inactive children included, both components, both asset kinds, close-only-if-opened).

**Existing tests that prove behaviour is unchanged:** the seven M1 `ValidationTests` (in particular `Validation_NetworkPrefabs_Pass`, which text-checks the Bootstrap and LakeDock references), `ArchitectureTests`, the full EditMode run, all PlayMode tests in `check` (Bootstrap composition and the lake dock scene are exercised end to end), and the scene identity check (layout, references).

### T4.7 Acceptance (reviewer)
- No asset reference obtained before `NewScene` is used after it in either builder; the only pre-`NewScene` work in `LakeDockBuilder` is writing the meshes; `BuildMenu` unchanged.
- Validators: scenes opened additively one at a time, closed in `finally` only if this code opened them, never saved; play-mode guard; inactive objects included; prefab folder `Assets/_Project/Prefabs`; `BootstrapReferences` via `SerializedObject` property names `inputActions` and `contentRegistry`; header comment updated.
- `ValidationRunner` order: M1 seven, then the three.
- `build` and `check` PASS; scenes restored; `AssetPaths.cs` untouched.

### T4.8 Commit
```powershell
git add Assets/_Project/Editor/Builders/SceneBuilder.cs Assets/_Project/Editor/Builders/LakeDockBuilder.cs Assets/_Project/Editor/Validation/Validators.cs Assets/_Project/Editor/Validation/ValidationRunner.cs Assets/_Project/Tests/EditMode/ValidationTests.cs; git commit -m "m2(carry): builders load assets after NewScene; MeshReferences, MissingScripts, BootstrapReferences validators" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Summary of tests in this section

| Task | New tests | Count after the task (EditMode / PlayMode) |
|---|---|---|
| T1 | `Pcg32_RangeFloat_NeverReturnsMax`, `Pcg32_RangeFloat_MaxNotAboveMin_Throws`, `Pcg32_RangeFloat_DrawCountUnchanged`, `LogOnce_SameKey_LogsOnce`, `LogOnce_Reset_AllowsAgain`, `EventIsolation_ThrowingSubscriber_OthersStillRun`, `EventIsolation_NullEvent_IsNoOp`, `EventIsolation_ActionAndActionOfT_ThrowingSubscriber_OthersStillRun`, `AudioBus_NotReady_WarnsOncePerId` (9) | M1 EditMode + 9 / 5 |
| T2 | none | unchanged |
| T3 | `Solo_LeaveRightAfterConnected_EndsOnMenu` | unchanged / 6 |
| T4 | `Validation_MeshReferences_Pass`, `Validation_MissingScripts_Pass`, `Validation_BootstrapReferences_Pass` | + 3 / 6 |

Implementers report the `total=` numbers from `check`; the controller compares them with this table.

---

## Contract questions (all closed by controller rulings of 2026-10-11; C 15)

- **Q1 (closed, A-Q1).** `LocalNetworkSession` responsibility 2 (`AttemptAsync`, `PostCancel`, `CancelAttempt`, `WatchTimeoutAsync`, about 75 lines) moves into `LocalNetworkSession.Start.cs`; the main file lands at about 270 lines (< 300). See T2.2.
- **Q2 (closed, A-Q2).** No fifth file. C 7.2 now sets the `UiFlowController.cs` target to < 400 lines (about 350-365 expected). See T2.3.
- **Q3 (closed, A-Q3).** `Tests/EditMode/AudioBusTests.cs` is in the T1 manifest (C 12).
- **Q4 (closed, A-Q4).** PlayMode is green after each of T1-T4 (skeleton 7 wins); C 2.4 wording amended.
- **Q5 (closed, A-Q5).** The validators open build scenes additively; if opening Bootstrap logs errors in EditMode, the YAML text-scan fallback is allowed (C 7.4 wording amended). See T4.4.
