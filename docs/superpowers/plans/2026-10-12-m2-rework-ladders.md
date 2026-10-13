# FishGame M2 rework - Plan: RV-style ladders and the shore ramp (spec R70)

- **Date:** 2026-10-12. Branch `m2-fishing-core` (stay on it; never `main`).
- **Status:** not started. 9 tasks (L1-L9), one commit each.
- **Source of truth:** spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` R70 (amendment log 3.2), sections 7.3, 7.8, 8.2. Research `docs/superpowers/research/2026-10-12-rv-ladders.md`. Handoff `docs/superpowers/handoff/2026-10-12-m2-rework-handoff.md` section 3.2.
- **Contract ("C"):** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (DA7, section 13, section 16). D36 lives in the M1 contract `docs/superpowers/plans/2026-10-09-m1-interfaces.md` (see flag F1).
- **Shape:** every pure-logic piece has complete code; the integration pieces (`PlayerLocomotion`, `LakeDockBuilder`, `FishingRodController`, `RodPresenter`) have the complete new methods and an exact list of the lines that change. Tests come first in every task.
- **Agent roles (CLAUDE.md):** Sonnet implements; Haiku may implement L1-L3 (their briefs contain complete code). Sonnet reviews L1, L2, L3, L6, L9. Opus (high) reviews L4, L5, L7, L8 (integration). Fable is the escalation fixer.

---

## 0. Read this first

### 0.1 Flags: where the code or the docs contradict the handoff or the research

| # | Flag | What this plan does |
|---|---|---|
| F1 | **D36 is not in the M2 contract.** It is row D36 of the M1 contract (`2026-10-09-m1-interfaces.md`, "Water exits": a WaterExit climb triggers while wading or swimming). The M2 contract has only DA7 (section 14). | L9 adds a superseded note to the M1 D36 row and records both in the M2 contract (new M2-D29/D30, DA7 row, a section 16.1 row). D36's in-water gate survives: it now gates only the ramp foot assist. |
| F2 | **The T28 physics fingerprint test does not exist in the repo.** It was a temporary, never-committed EditMode test (ruling G-Q9, ui-world plan T28.4 step 2). | Appendix A gives its full code. L5 and L7 create it, take "before" and "after", diff, and delete it before the commit. Appendix B lists the exact expected differences. |
| F3 | **Layer trap.** The current exits are triggers on layer `Water`. `CastProbe.Probe` raycasts the `Water` layer with `QueryTriggerInteraction.Collide` and accepts the first Water-layer hit as the water surface. A ladder box on `Water` reaching y 1.6 would become "water at 1.6 m" for casts near the ladders. | `LadderVolume`s go on layer `Default` (outside `CastProbe.CastMask`). L7 also moves the `Exits` root (the ramp foot) to `Default`. Both are recorded as intentional fingerprint changes. |
| F4 | **"Edge-walking does not trigger the ladder" (research section 4) is not true.** A CharacterController stays grounded on a ledge with its centre up to about 0.35 m past the edge, and the ladder rail collider is flush with the deck top out to x 1.61. So a player standing right at the deck edge in front of a ladder (centre x 1.55 or more) starts climbing. | This is R70's "step off the deck edge" entry, so it stays. L5 pins both sides: standing at x 1.4 does not climb; walking off the edge does. The knob is `LakeDockLayout.LadderVolumeInnerX`. Open question Q7. |
| F5 | The research proposed `_vy = 0` when a climb ends. RV's flying-to-falling switch keeps the velocity, and the top-out needs it: at the top the capsule leaves the box about 0.45 m short of being over the deck. | The applied climb velocity carries into `_vx/_vz/_vy` on exit. The PlayMode climb-out tests at pitch 30/45/60 are the arbiter (L5). |
| F6 | The research kept the ramp's 0.4 s lerp. R70 (later, user-approved) removes it. | R70 wins (L7). |
| F7 | **The ramp has no lip.** The `RampDeck` box and the lakebed form one continuous ~9 deg slope (ramp top within 5 cm of the bed from z -43 to -48), so a swimmer can already walk out. The current ramp exit box (z -46..-42) starts where the bed is about -0.95 m, i.e. players there already wade. | The "light upward assist" is: inside the ramp foot you wade instead of swim, and a swimmer arriving there pushing toward the ramp gets one small upward kick. The foot box is extended seaward to z -39 (bed about -1.38 m) so the assist acts where swimmers find their feet. Open question Q3. |
| F8 | The handoff names `ClimbWish(yawRad, pitchDeg, ...)`. Core has no `Vector3`, and mixing radians and degrees in one signature invites bugs. | `ClimbWish(float yawDeg, float pitchDeg, float moveX, float moveY, out float x, out float y, out float z)`: degrees for both (`transform.eulerAngles.y` is degrees), out floats like `MovementMath.Accelerate`. |
| F9 | "`RouteClimbing` passes Move only." Taken literally (Look and Pause zeroed too) the player could not look up to climb or open the pause menu on a ladder. | Read as "Move is the only gameplay action that passes": Move, Look and Pause pass; everything else is zeroed (research section 4 says the same). |
| F10 | **The acceptance doc goes stale:** `docs/superpowers/plans/2026-10-11-m2-acceptance.md` lines ~554 and ~563 say "E climbs out at the ladder". | L9 rewrites both lines and adds a ladder/ramp feel check to section J. |
| F11 | `RodViewSoloTests` assumes the rod shows and hides instantly (`DriveView` waits 0.1 s, the stow check waits 0.05 s). A 0.2 s stow animation breaks both. | L8 lengthens those two waits. |
| F12 | `PlayerHooksTests.MakeLocomotionRig` builds locomotion rigs without a `PlayerCamera`. | `PlayerLocomotion` caches the camera in `Awake` and uses pitch 0 when it is missing. |
| F13 | The handoff lists `GameplayContextArbiter` as touched. Its precedence (Climbing > Swimming > request > Free) is already right. | No code change; its tests stay as they are. |
| F14 | `SwimInteractGateTests` positions sit at z -41.0, which is exactly the new ladder box's max-z face. | Rewritten tests use z -41.5 (the box's middle). |

### 0.2 Open questions for the user (defaults in brackets; the plan builds the default)

1. **Wading next to the bell.** A wader (in water but not swimming) is in context `Free`, so `InteractLive` is true and a wader standing in the shallows within 3 m of the bell (for example at (2.2, -1.2, -42.5), just outside the ladder box) can ring it. This was already true in M1 everywhere except inside the old exit box. Does "the bell is unringable from the water" include wading? [Default: keep M1: wading is Free. Only swimming and climbing are gated, and tests pin those two. An in-water gate can be added later as one router/locomotion change.]
2. **Spec 7.8 #3 wording.** At 2.4 m/s the climb-out takes about 1.5 s at a 45 deg look-up, about 2 s at 30 deg, and the ramp walk-out from the foot to dry ground about 2.5 s. [Default: "within 2 s of reaching a ladder while looking up toward the deck (about 45 deg); walks out up the ramp with no pull". L5's PlayMode test asserts 2 s at 45 deg.]
3. **Ramp assist shape.** [Default: wade-not-swim in the foot plus one 2.0 m/s upward kick (about a 9 cm step-up) for a swimmer arriving while pushing toward the ramp. `MovementMath.RampAssistKick` is the knob.]
4. **Re-entry grace.** R70 says the grace is "at the top". [Default: only an exit through the top face starts the 0.25 s grace; leaving sideways or from the bottom does not.]
5. **Stow animation scope.** [Default: every rod stow and equip animates (the E key too, swimming, ladders), and every peer sees it. One code path.]
6. **`NetClimbing`.** [Default: not added in M2 (decision D4 below). Remote players see a climber move up through the NetworkTransform, with no special pose.]
7. **Edge grabbiness (F4).** Standing at the deck edge in front of a ladder puts you on the ladder (no gravity, you hang), and a line out is reeled in (the rod stows). [Default: keep (R70). If it feels grabby at playtest, move `LadderVolumeInnerX` outward, from 1.55 to about 1.75.]

### 0.3 Decisions taken in this plan (the reviewer checks these, the user can overturn them)

- **D1 Climb model.** The climb is volume-driven. The owner climbs while the capsule centre (`transform.TransformPoint(cc.center)`) is inside any `LadderVolume`. There is no button, no snap and no lerp. The CharacterController stays enabled, so the rail, posts and deck collide. The wish is `ClimbWish * dt` through `cc.Move`: no gravity, no acceleration ramp, zero input = zero velocity (you hang).
- **D2 Entry.** Not paused, not locked (`MovementLocked`: RodFighting or any source reporting `LocksMovement`), not already climbing, the `ClimbGate` allows it, and the centre is inside a volume. Entry writes `IsSwimming = false` and `OwnerSetSwimming(false)`, and resets motion and `SecondsInWater`.
- **D3 Exit.** After the climb move, if the centre is outside every volume, the climb ends. The applied velocity (`cc.velocity`) carries into `_vx, _vz, _vy` (F5). If the centre is at or above the top face of the volume it was in (within `LadderVolume.TopEpsilon` 0.05 m), `ClimbGate` blocks entry for `MovementMath.ClimbReentryGrace` 0.25 s.
- **D4 No `NetClimbing` in M2.** No consumer needs it: there is no remote climb pose, no host rule reads climbing, and the spec's M5 pickup rule reads "not swimming". Adding a NetworkVariable changes a networked prefab, so `BuildInfo.ProtocolVersion` would have to go 2 -> 3 for a cosmetic. Revisit in M5 with body states. Authority is stated in the class comments of `LadderVolume` (none: static geometry) and `PlayerLocomotion` (owner).
- **D5 Router.** `RouteClimbing` passes Move, Look and Pause. `RouteSwimming` zeroes Interact, because there is no climb-at-exit press any more. `InteractLive` is unchanged (Free, RodReady, RodWaiting).
- **D6 Rod.** "Away" = `IsSwimming || IsClimbing` (`RodControllerRules.IsAway`). Entering away with the rod out stows it (remembered; a line out is recalled through `ForceEnd`). Once both flags clear, the rod is equipped again. The swim-to-climb hand-over never pops the rod out.
- **D7 Stow animation.** `RodPresenter` (every peer) animates every `NetTool` change with Core `ToolStowAnim`: 0.2 s, smoothstep. The Seg* chain and TipVisual swing 70 deg tip-down and slide 0.3 m back about Seg0, then the renderers hide. Equip plays it in reverse. `TipAnchor` is never touched (BF-3), so the gameplay tip, the reach tests and the host slack are unchanged. `PlayerBody` (ArmR carries TipAnchor) is not touched.
- **D8 Ramp.** The 0.4 s lerp, `TryBeginClimb` and `MovementMath.ClimbDuration` are deleted. `Exits/Ramp` stays a `WaterExit`; it becomes the ramp foot box: centre (12, -1.0, -42.5), size (3, 2.8, 7), layer Default, `Top` unchanged at (12, 0.45, -50.5). Core `RampAssist` implements the assist (F7). DA7's facing rule survives as `MovementMath.PushingToward`, used only for the kick.
- **D9 Geometry.** `Ladders/LadderL|R`: trigger boxes at x +-(1.55..2.55), y -1.4..1.6, z -42..-41 (centre (+-2.05, 0.1, -41.5), size (1, 3, 1)), layer Default, component `LadderVolume`, no `Top` child.

### 0.4 Risks

1. **Top-out feel.** The capsule leaves the box through the top about 0.45 m before it is over the deck. The carried velocity plus air control is what lands the player on the deck. The PlayMode tests at pitch 30/45/60 (L5) decide. Prescribed fallback if one fails: raise `LakeDockLayout.LadderVolumeTopY` from 1.6 to 1.9 and rerun; if it still fails, stop and escalate (Fable). Looking almost straight up (85 deg) and holding W bobs at the top: RV's own quirk, which the grace damps.
2. **Edge grabbiness (F4, Q7).**
3. **Swim-depth flicker near the dock.** The bed beside the ladders is about -1.26 m, right at the swim threshold (root < surface - 1.2), so the swell flips swim/wade there. This is M1 behaviour. Tests assert "in water", never "swimming", at those spots.
4. **Frame-rate sensitivity in PlayMode.** The climb is linear (`cc.Move(v * dt)`), so it is frame-rate independent. The top-out uses gravity integration, which is exact (`VerticalStep`). Time limits carry 25%+ headroom except the 2 s spec check (measured ~1.5 s).
5. **Remote readability.** Remotes see a player float up a ladder with legs still (the walk cycle reads horizontal speed only). This is accepted for M2 (D4).
6. **Scene churn.** Every Unity run renumbers fileIDs in all three scenes. Only L5 and L7 commit `Spot_LakeDock.unity`. Every other task restores all three scenes.

---

## L0. Rules shared by every task (send L0 with every brief)

### L0.1 Read before starting
- `CLAUDE.md`; contract C 2.8 (global constraints) and C 16.4 (process notes); this plan's section 0; your task.
- The files your task edits, in full.

### L0.2 Constraints that bite here
- **Tools.** Create and edit files ONLY with the Write and Edit tools: never heredocs, `sed`, `python` or PowerShell `Set-Content`. Run `Tools/unity.ps1` ONLY through the PowerShell tool (foreground, `timeout` 600000 ms). Full runs that may exceed 10 minutes run in the background (`run_in_background`); wait for the notification.
- **Lock.** The first `unity.ps1` run prints `LOCKED` (exit 2) if the editor or another agent holds the project. Then stop and report. Never kill Unity and never delete `Temp/UnityLockfile`. A leftover lockfile that is not held is not a lock.
- **ASCII** in code and comments. Spec tables already use the em dash in empty cells: when L9 edits a spec table row, copy the cell character from the neighbouring rows.
- **Scene churn (C 2.8 #6).** After every Unity run: `git -C C:\Dev\FishGame restore Assets/_Project/Scenes/Bootstrap.unity Assets/_Project/Scenes/Menu.unity`. Also restore `Assets/_Project/Scenes/Spot_LakeDock.unity`, except in L5 and L7 after their `build`. Delete a leftover `Assets/InitTestScene*.unity` and an untracked `dev/null/` folder if a run leaves them.
- **PlayMode stays warning-free (C 2.8 #9).** Tests log measurements with `TestContext.WriteLine`, never `Debug.LogWarning`.
- **Mutation rule (C 16.4).** Every new test names, in its comment, the plausible wrong implementation it catches. For the key formulas (ClimbWish, ClimbGate, RampAssist, AwayStep, ToolStowAnim), break the code once (for example flip the pitch sign), run the test, show it fails, and restore. Put the red run in the task report ("expected red").
- **Commits.** Explicit paths only (never `git add -A` or `.`). Conventional-commit subject as given in the task. Message body ends with the implementer's own trailer, for a Sonnet implementer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (C D33). Never push or merge unless the controller says so.
- **Unexpected changes.** If `git status` shows a modified file your task does not name (prefabs, assets), stop and report; do not commit it.

### L0.3 Commands (Windows PowerShell)
```powershell
$env:FG_OUT = '<absolute session scratchpad>\fg'     # outside the repo, no '..'
$u = 'C:\Dev\FishGame\Tools\unity.ps1'
powershell -NoProfile -ExecutionPolicy Bypass -File $u compile
powershell -NoProfile -ExecutionPolicy Bypass -File $u build
powershell -NoProfile -ExecutionPolicy Bypass -File $u test-edit -Filter "<regex>"
powershell -NoProfile -ExecutionPolicy Bypass -File $u test-play -Filter "<regex>"
powershell -NoProfile -ExecutionPolicy Bypass -File $u test-edit -TimeoutMinutes 20    # full EditMode (background)
powershell -NoProfile -ExecutionPolicy Bypass -File $u test-play -TimeoutMinutes 25    # full PlayMode (background)
```
The last line of every run is `FG RESULT <command> PASS|FAIL|LOCKED|TIMEOUT`. Exit 0 alone is not proof: read the `FG RESULT` line, and on failure the `FG LOG` / `FG FAILED` lines and the log file it names. The baseline before L1 is EditMode 869 pass + 1 skipped, PlayMode 64/64 (handoff section 1). Every task's report gives the new totals.

### L0.4 Pre-flight (every task)
1. `git -C C:\Dev\FishGame status --porcelain --untracked-files=all`, saved to the scratchpad as the task baseline. Other workstreams (the water look, the fight rework) may have uncommitted files in the tree: they are part of the baseline, are never staged by this plan, and must look the same after your task. If one of them overlaps a file your task edits, stop and report.
2. `git -C C:\Dev\FishGame log --oneline -12`: the commits of the tasks you depend on are present.

---

## L1 - Core: climb wish, climb speed and the re-entry gate

**Header**
- **Goal:** the pure maths of R70: `MovementMath.ClimbSpeed`, `ClimbReentryGrace`, `ClimbWish`, and the `ClimbGate` struct. Additive only; nothing calls them yet.
- **Files:** edit `Assets/_Project/Core/Player/MovementMath.cs`; new `Assets/_Project/Core/Player/ClimbGate.cs`; new tests `Assets/_Project/Tests/EditMode/ClimbWishTests.cs`, `Assets/_Project/Tests/EditMode/ClimbGateTests.cs`.
- **Depends on:** nothing. **Implementer:** Haiku or Sonnet. **Reviewer:** Sonnet.

### L1.1 Tests first

`Assets/_Project/Tests/EditMode/ClimbWishTests.cs`:
```csharp
// Authority: none (pure EditMode tests). FishGame.Core.MovementMath.ClimbWish (spec 7.3, R70): Move along the camera look at ClimbSpeed.
// Unity axes: +y up; yaw clockwise from +z, so forward(yaw, pitch) = (sin yaw cos pitch, sin pitch, cos yaw cos pitch), right = (cos yaw, 0, -sin yaw).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ClimbWishTests
    {
        private const float Tol = 1e-4f;

        private static void Wish(float yawDeg, float pitchDeg, float moveX, float moveY, out float x, out float y, out float z)
        {
            MovementMath.ClimbWish(yawDeg, pitchDeg, moveX, moveY, out x, out y, out z);
        }

        // Catches: a retuned seed slipping in unnoticed (spec R70: 2.4 m/s, 0.25 s).
        [Test]
        public void ClimbWish_Constants_MatchSpec()
        {
            Assert.AreEqual(2.4f, MovementMath.ClimbSpeed);
            Assert.AreEqual(0.25f, MovementMath.ClimbReentryGrace);
        }

        // Catches: gravity, drift or a decaying velocity on the ladder (R70: no input = hang, exactly).
        [Test]
        public void ClimbWish_NoInput_IsExactlyZero()
        {
            Wish(37f, 60f, 0f, 0f, out float x, out float y, out float z);
            Assert.AreEqual(0f, x, 0f);
            Assert.AreEqual(0f, y, 0f);
            Assert.AreEqual(0f, z, 0f);
        }

        // Catches: pitch ignored (y would be 0) or the pitch sign flipped (W while looking up would descend).
        [Test]
        public void ClimbWish_ForwardLookingUp45_ClimbsAlongTheLook()
        {
            Wish(0f, 45f, 0f, 1f, out float x, out float y, out float z);
            float c = 2.4f * 0.70710678f;
            Assert.AreEqual(0f, x, Tol);
            Assert.AreEqual(c, y, Tol);
            Assert.AreEqual(c, z, Tol);
        }

        // Catches: descending only through S (R70: look down + W descends).
        [Test]
        public void ClimbWish_ForwardLookingDown60_Descends()
        {
            Wish(0f, -60f, 0f, 1f, out float x, out float y, out float z);
            Assert.AreEqual(0f, x, Tol);
            Assert.AreEqual(-2.4f * 0.8660254f, y, Tol);
            Assert.AreEqual(2.4f * 0.5f, z, Tol);
        }

        // Catches: S not reversing the look direction.
        [Test]
        public void ClimbWish_BackwardLookingUp_DescendsAndBacksOff()
        {
            Wish(0f, 45f, 0f, -1f, out float x, out float y, out float z);
            float c = 2.4f * 0.70710678f;
            Assert.AreEqual(-c, y, Tol);
            Assert.AreEqual(-c, z, Tol);
        }

        // Catches: yaw ignored or applied with the wrong handedness (yaw 90 must face +x).
        [Test]
        public void ClimbWish_Yaw90Forward_PointsPlusX()
        {
            Wish(90f, 0f, 0f, 1f, out float x, out float y, out float z);
            Assert.AreEqual(2.4f, x, Tol);
            Assert.AreEqual(0f, y, Tol);
            Assert.AreEqual(0f, z, Tol);
        }

        // Catches: strafe using the pitched vector (A/D would climb) or the right vector with the wrong sign.
        [Test]
        public void ClimbWish_StrafeRight_IsFlatAlongTransformRight()
        {
            Wish(90f, 60f, 1f, 0f, out float x, out float y, out float z);   // right at yaw 90 = (0, 0, -1)
            Assert.AreEqual(0f, x, Tol);
            Assert.AreEqual(0f, y, Tol);
            Assert.AreEqual(-2.4f, z, Tol);
        }

        // Catches: no length clamp (a diagonal would climb at 3.39 m/s) and, the other way, normalising small stick input up to full speed.
        [Test]
        public void ClimbWish_InputLength_ClampedToOneButNotNormalisedUp()
        {
            Wish(0f, 0f, 1f, 1f, out float x, out float y, out float z);
            Assert.AreEqual(2.4f, (float)System.Math.Sqrt(x * x + y * y + z * z), Tol, "diagonal");

            Wish(0f, 0f, 0.5f, 0f, out x, out y, out z);
            Assert.AreEqual(1.2f, (float)System.Math.Sqrt(x * x + y * y + z * z), Tol, "half stick");
        }

        // Catches: no pitch clamp (the camera never exceeds PitchLimit 85, but a bad caller must not climb faster or flip).
        [Test]
        public void ClimbWish_PitchBeyondLimit_IsClampedTo85()
        {
            Wish(0f, 120f, 0f, 1f, out float _, out float y, out float _);
            Assert.AreEqual(2.4f * 0.9961947f, y, Tol);
        }

        // Catches: NaN or infinity reaching CharacterController.Move.
        [Test]
        public void ClimbWish_NonFiniteInput_IsZero()
        {
            float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float b in bad)
            {
                Wish(b, 0f, 0f, 1f, out float x, out float y, out float z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "yaw " + b);
                Wish(0f, b, 0f, 1f, out x, out y, out z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "pitch " + b);
                Wish(0f, 0f, b, 1f, out x, out y, out z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "moveX " + b);
                Wish(0f, 0f, 0f, b, out x, out y, out z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "moveY " + b);
            }
        }
    }
}
```

`Assets/_Project/Tests/EditMode/ClimbGateTests.cs`:
```csharp
// Authority: none (pure EditMode tests). FishGame.Core.ClimbGate: the 0.25 s ladder re-entry grace after topping out (spec 7.3, R70).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ClimbGateTests
    {
        // Catches: a gate that starts closed (the first ladder could never be entered).
        [Test]
        public void ClimbGate_Default_CanEnter()
        {
            var gate = new ClimbGate();
            Assert.IsTrue(gate.CanEnter);
            Assert.AreEqual(0f, gate.BlockedFor);
        }

        // Catches: no grace, a wrong length, or a grace that never ends.
        [Test]
        public void ClimbGate_ExitThroughTop_BlocksForTheGraceThenOpens()
        {
            var gate = new ClimbGate();
            gate.OnExit(throughTop: true);
            Assert.IsFalse(gate.CanEnter);
            gate.Tick(0.24f);
            Assert.IsFalse(gate.CanEnter, "0.24 s < 0.25 s");
            gate.Tick(0.011f);
            Assert.IsTrue(gate.CanEnter, "0.251 s");
        }

        // Catches: a grace after every exit (R70 puts it at the top only, open question 4).
        [Test]
        public void ClimbGate_ExitSidewaysOrBottom_NoGrace()
        {
            var gate = new ClimbGate();
            gate.OnExit(throughTop: false);
            Assert.IsTrue(gate.CanEnter);
        }

        // Catches: NaN poisoning (NaN would keep the gate shut forever) and a negative dt extending the grace.
        [Test]
        public void ClimbGate_BadDt_IsIgnored()
        {
            var gate = new ClimbGate();
            gate.OnExit(true);
            gate.Tick(float.NaN);
            gate.Tick(-1f);
            gate.Tick(float.PositiveInfinity);
            gate.Tick(0f);
            Assert.AreEqual(0.25f, gate.BlockedFor, 1e-6f);
        }

        // Catches: a second top exit inside the grace not restarting it, and Reset leaving it shut (teleports and respawns clear it).
        [Test]
        public void ClimbGate_SecondTopExitRestarts_ResetClears()
        {
            var gate = new ClimbGate();
            gate.OnExit(true);
            gate.Tick(0.2f);
            gate.OnExit(true);
            Assert.AreEqual(0.25f, gate.BlockedFor, 1e-6f);
            gate.Reset();
            Assert.IsTrue(gate.CanEnter);
        }
    }
}
```

### L1.2 Code

`MovementMath.cs`, three edits:
1. Below line 15 (`ClimbDuration` line, which stays until L7) add:
```csharp
        public const float ClimbSpeed = 2.4f, ClimbReentryGrace = 0.25f;   // R70 ladders [tune]
```
2. Add the method after `IsInWater`:
```csharp
        // R70 ladder climb: W/S along the camera look (yaw and pitch; pitch positive = look up), A/D along the flat right vector.
        // The input length is clamped to 1 (never normalised up), times ClimbSpeed. No gravity and no acceleration ramp: zero input gives
        // exactly zero (the climber hangs). Pitch is clamped to +-PitchLimit. Any non-finite input gives zero.
        // forward = (sin yaw cos pitch, sin pitch, cos yaw cos pitch); right = (cos yaw, 0, -sin yaw) (Unity: +y up, yaw clockwise from +z).
        public static void ClimbWish(float yawDeg, float pitchDeg, float moveX, float moveY, out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            if (!IsFinite(yawDeg) || !IsFinite(pitchDeg) || !IsFinite(moveX) || !IsFinite(moveY)) return;

            float len2 = moveX * moveX + moveY * moveY;
            if (len2 <= 0f) return;

            float scale = len2 > 1f ? ClimbSpeed / MathF.Sqrt(len2) : ClimbSpeed;
            float pitch = MathF.Min(MathF.Max(pitchDeg, -PitchLimit), PitchLimit) * (MathF.PI / 180f);
            float yaw = yawDeg * (MathF.PI / 180f);
            float sinYaw = MathF.Sin(yaw), cosYaw = MathF.Cos(yaw);
            float sinPitch = MathF.Sin(pitch), cosPitch = MathF.Cos(pitch);

            x = (sinYaw * cosPitch * moveY + cosYaw * moveX) * scale;
            y = sinPitch * moveY * scale;
            z = (cosYaw * cosPitch * moveY - sinYaw * moveX) * scale;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
```
3. Nothing else changes in L1.

New `Assets/_Project/Core/Player/ClimbGate.cs`:
```csharp
// Core/Player/ClimbGate.cs: the ladder re-entry grace (spec 7.3, R70; contract M2-D29). Pure; PlayerLocomotion owns one per owner.
// A climb that ends through the top of a LadderVolume blocks the next climb start for MovementMath.ClimbReentryGrace seconds, so a climber
// who tops out and drops a few centimetres is not caught by the ladder again. Any other exit starts no grace.
namespace FishGame.Core
{
    public struct ClimbGate
    {
        private float _blockedFor;

        public bool CanEnter => _blockedFor <= 0f;
        public float BlockedFor => _blockedFor;

        public void OnExit(bool throughTop)
        {
            if (throughTop) _blockedFor = MovementMath.ClimbReentryGrace;
        }

        // Counts the grace down. dt <= 0, NaN or infinite is ignored.
        public void Tick(float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            _blockedFor = _blockedFor > dt ? _blockedFor - dt : 0f;
        }

        public void Reset()
        {
            _blockedFor = 0f;
        }
    }
}
```

### L1.3 Verify
1. `compile` -> PASS.
2. `test-edit -Filter "ClimbWishTests|ClimbGateTests"` -> all pass (15 tests).
3. Mutation (L0.2): flip the sign of `y` in `ClimbWish`; `ClimbWish_ForwardLookingUp45_ClimbsAlongTheLook` and `..._ForwardLookingDown60_Descends` go red. In `ClimbGate.OnExit` drop the `if (throughTop)`; `ClimbGate_ExitSidewaysOrBottom_NoGrace` goes red. Restore both.
4. Full `test-edit` (background) -> 869 + 15 pass, 1 skipped.
5. Restore the three scenes (L0.2).

### L1.4 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Core/Player/MovementMath.cs Assets/_Project/Core/Player/ClimbGate.cs Assets/_Project/Core/Player/ClimbGate.cs.meta Assets/_Project/Tests/EditMode/ClimbWishTests.cs Assets/_Project/Tests/EditMode/ClimbWishTests.cs.meta Assets/_Project/Tests/EditMode/ClimbGateTests.cs Assets/_Project/Tests/EditMode/ClimbGateTests.cs.meta
git -C C:\Dev\FishGame commit -m "feat(core): ClimbWish, ClimbSpeed and the ladder re-entry gate (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(The `.meta` files are generated by the `compile` run; they must exist before the commit.)

---

## L2 - LadderVolume, and Climbing routes Move

**Header**
- **Goal:** the `LadderVolume` component (not placed in any scene yet) and the `RouteClimbing` row that passes Move. Safe now: the only climb today is the M1 exit lerp, which ignores the intent.
- **Files:** new `Assets/_Project/Gameplay/World/LadderVolume.cs`; edit `Assets/_Project/Core/Input/InputContextRouter.cs`; edit `Assets/_Project/Tests/EditMode/InputContextRouterTests.cs`; new `Assets/_Project/Tests/EditMode/LadderVolumeTests.cs`.
- **Depends on:** L1. **Implementer:** Haiku or Sonnet. **Reviewer:** Sonnet.

### L2.1 Tests first

`InputContextRouterTests.cs`: replace the whole method `InputContextRouter_Climbing_PassesOnlyLookAndPause` (lines 82-103) with:
```csharp
        // R70: Move climbs along the look; nothing else acts on a ladder. Catches: Move still zeroed (the climb could never move), and Jump,
        // Sprint, Interact or the rod buttons leaking onto a ladder (the bell could ring, a cast could start).
        [Test]
        public void InputContextRouter_Climbing_PassesMoveLookAndPauseOnly()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Climbing, AllActive());
            AssertRow(i, "Climbing", move: true, look: true, jump: false, sprint: false, primary: false, secondary: false,
                      interact: false, shove: false, drop: false, equip: false, pause: true);
        }

        // Catches: a ladder that reaches interactables (InteractLive) or drives a fight.
        [Test]
        public void InputContextRouter_Climbing_NeverInteractsOrFights()
        {
            Assert.IsFalse(InputContextRouter.InteractLive(InputContext.Climbing), "nothing can be interacted with from a ladder");
            FightInput fight = InputContextRouter.RouteFight(InputContext.Climbing, AllActive());
            Assert.AreEqual(0f, fight.Steer, 0f);
            Assert.IsFalse(fight.Reel);
        }
```

New `Assets/_Project/Tests/EditMode/LadderVolumeTests.cs`:
```csharp
// Authority: none (EditMode tests on a GameObject built in the test). FishGame.Gameplay.LadderVolume local-space containment (R70).
// In EditMode AddComponent does not run Awake/OnEnable, so these call Contains / IsAtOrAboveTop directly (FindContaining is PlayMode-tested).
using FishGame.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class LadderVolumeTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        // The lake-dock LadderR numbers: centre (2.05, 0.1, -41.5), size (1, 3, 1) -> x 1.55..2.55, y -1.4..1.6, z -42..-41.
        private LadderVolume Make(Vector3 boxCenter)
        {
            _go = new GameObject("LadderTest");
            _go.transform.position = new Vector3(2.05f, 0.1f, -41.5f);
            BoxCollider box = _go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = boxCenter;
            box.size = new Vector3(1f, 3f, 1f);
            return _go.AddComponent<LadderVolume>();
        }

        // Catches: using the full size as the half extent (a box twice as big), a box shifted off its transform, and a wrong face.
        [Test]
        public void LadderVolume_Contains_InsideAndOnFaces_NotJustOutside()
        {
            LadderVolume v = Make(Vector3.zero);
            Assert.IsTrue(v.Contains(new Vector3(2.05f, 0.1f, -41.5f)), "centre");
            Assert.IsTrue(v.Contains(new Vector3(1.551f, 1.599f, -41.001f)), "1 mm inside the deck-side top corner");   // exact-face points are float-fragile
            Assert.IsFalse(v.Contains(new Vector3(1.54f, 0.1f, -41.5f)), "1 cm inside the deck side");
            Assert.IsFalse(v.Contains(new Vector3(2.56f, 0.1f, -41.5f)), "1 cm past the outer face");
            Assert.IsFalse(v.Contains(new Vector3(2.05f, 1.61f, -41.5f)), "1 cm above the top");
            Assert.IsFalse(v.Contains(new Vector3(2.05f, -1.41f, -41.5f)), "1 cm below the bottom");
            Assert.IsFalse(v.Contains(new Vector3(2.05f, 0.1f, -40.99f)), "1 cm past max z");
        }

        // Catches: ignoring BoxCollider.center.
        [Test]
        public void LadderVolume_BoxCenterOffset_IsHonoured()
        {
            LadderVolume v = Make(new Vector3(0f, 1f, 0f));   // y -0.4..2.6
            Assert.IsTrue(v.Contains(new Vector3(2.05f, 2.5f, -41.5f)));
            Assert.IsFalse(v.Contains(new Vector3(2.05f, -0.5f, -41.5f)));
        }

        // Catches: the grace firing for side exits low on the ladder, or not at the top (TopEpsilon 0.05 below the face).
        [Test]
        public void LadderVolume_IsAtOrAboveTop_WithinEpsilonBelowTheFace()
        {
            LadderVolume v = Make(Vector3.zero);
            Assert.IsTrue(v.IsAtOrAboveTop(new Vector3(2.05f, 1.7f, -41.5f)), "above the top");
            Assert.IsTrue(v.IsAtOrAboveTop(new Vector3(1.4f, 1.56f, -41.5f)), "0.04 below the top, beside the box");
            Assert.IsFalse(v.IsAtOrAboveTop(new Vector3(2.05f, 1.54f, -41.5f)), "0.06 below the top");
        }

        // Catches: a NullReferenceException on a misbuilt volume.
        [Test]
        public void LadderVolume_WithoutBoxCollider_ContainsNothing()
        {
            _go = new GameObject("NoBox");
            LadderVolume v = _go.AddComponent<LadderVolume>();
            Assert.IsFalse(v.Contains(Vector3.zero));
            Assert.IsFalse(v.IsAtOrAboveTop(Vector3.zero));
        }
    }
}
```

### L2.2 Code

`InputContextRouter.cs`: replace the `RouteClimbing` method and its comment (lines 112-121) with:
```csharp
        // Climbing (ladder, R70): Move, Look, Pause pass; Move climbs along the look (PlayerLocomotion). Jump, Sprint, Primary*, Secondary*,
        // Interact, Shove, Drop, Equip zeroed: nothing on a ladder can act or interact.
        private static PlayerIntent RouteClimbing(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                MoveX = f.MoveX,
                MoveY = f.MoveY,
                LookYawDeg = f.LookYawDeg,
                LookPitchDeg = f.LookPitchDeg,
                PausePressed = f.PausePressed,
            };
        }
```
`RouteSwimming` is NOT touched in L2 (L5 changes it together with the scene).

New `Assets/_Project/Gameplay/World/LadderVolume.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: none (static world geometry, present on every peer). A walk-in ladder (spec 7.3, R70; contract M2-D29): while the owner's
    // capsule centre is inside this trigger box, PlayerLocomotion climbs. Nothing here is networked: the owner's movement replicates through
    // its owner-authority NetworkTransform like all locomotion, and no ladder state goes on the wire (no NetClimbing in M2, M2-D32).
    // Needs one BoxCollider (isTrigger) and no rotation on this object or its parents. Built by LakeDockBuilder on layer Default, so
    // CastProbe's water mask never sees it. No RequireComponent on purpose (see WaterVolume).
    public sealed class LadderVolume : MonoBehaviour
    {
        // A point no more than this far below the top face counts as "at the top" (the re-entry grace, R70).
        public const float TopEpsilon = 0.05f;

        private static readonly List<LadderVolume> s_all = new List<LadderVolume>();
        private BoxCollider _box;

        public static IReadOnlyList<LadderVolume> All => s_all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            if (_box == null)
            {
                Debug.LogWarning($"LadderVolume '{name}' has no BoxCollider.", this);
            }
        }

        private void OnEnable()
        {
            if (!s_all.Contains(this))
            {
                s_all.Add(this);
            }
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        // Local-space test against the BoxCollider's centre and size (not Collider.bounds, which can be stale right after a scene opens in an
        // EditMode test). Points on a face count as inside.
        public bool Contains(Vector3 worldPoint)
        {
            if (!TryLocal(worldPoint, out Vector3 d, out Vector3 half))
            {
                return false;
            }

            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && Mathf.Abs(d.z) <= half.z;
        }

        // True when the point is level with or above the top face, or within TopEpsilon below it. Read on the frame a climb ends.
        public bool IsAtOrAboveTop(Vector3 worldPoint)
        {
            return TryLocal(worldPoint, out Vector3 d, out Vector3 half) && d.y >= half.y - TopEpsilon;
        }

        public static LadderVolume FindContaining(Vector3 worldPoint)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                LadderVolume volume = s_all[i];
                if (volume != null && volume.Contains(worldPoint))
                {
                    return volume;
                }
            }

            return null;
        }

        private bool TryLocal(Vector3 worldPoint, out Vector3 delta, out Vector3 half)
        {
            if (_box == null)
            {
                _box = GetComponent<BoxCollider>();
            }

            if (_box == null)
            {
                delta = Vector3.zero;
                half = Vector3.zero;
                return false;
            }

            delta = transform.InverseTransformPoint(worldPoint) - _box.center;
            half = _box.size * 0.5f;
            return true;
        }
    }
}
```

### L2.3 Verify
1. `compile` -> PASS.
2. `test-edit -Filter "InputContextRouterTests|LadderVolumeTests"` -> all pass.
3. Mutation: in `Contains` compare against `_box.size` instead of `half`; `LadderVolume_Contains_InsideAndOnFaces_NotJustOutside` goes red. Restore.
4. Full `test-edit` (background) and full `test-play` (background): green (PlayMode unchanged at 64; nothing uses the new row yet).
5. Restore the three scenes.

### L2.4 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Gameplay/World/LadderVolume.cs Assets/_Project/Gameplay/World/LadderVolume.cs.meta Assets/_Project/Core/Input/InputContextRouter.cs Assets/_Project/Tests/EditMode/InputContextRouterTests.cs Assets/_Project/Tests/EditMode/LadderVolumeTests.cs Assets/_Project/Tests/EditMode/LadderVolumeTests.cs.meta
git -C C:\Dev\FishGame commit -m "feat(ladders): LadderVolume component; the Climbing context passes Move (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## L3 - Rod: swimming OR climbing puts the rod away

**Header**
- **Goal:** `HandleSwim` treats climbing like swimming (D6), before any ladder volume exists. Without this, once ladders land, `IsSwimming` clears on ladder entry and the rod would pop back out mid-ladder. Safe now: with the M1 lerp, `IsSwimming` stays true for a swimmer during the lerp. A wader's 0.4 s lerp now stows and re-equips the rod, which is harmless and goes away in L5.
- **Files:** edit `Assets/_Project/Fishing/Owner/RodControllerRules.cs`, `Assets/_Project/Fishing/Owner/FishingRodController.cs`; new test `Assets/_Project/Tests/EditMode/RodAwayStepTests.cs`.
- **Depends on:** L2. **Implementer:** Haiku or Sonnet. **Reviewer:** Sonnet.

### L3.1 Tests first

New `Assets/_Project/Tests/EditMode/RodAwayStepTests.cs`:
```csharp
// Authority: none (pure EditMode tests). FishGame.Fishing.RodControllerRules.IsAway / AwayStep (spec 7.3, R70, contract M2-D31):
// swimming or climbing puts the rod away (remembered) and brings it back once both are over.
using FishGame.Fishing;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RodAwayStepTests
    {
        private bool _wasAway, _stowed;

        private RodAwayAction Step(bool swimming, bool climbing, bool rodOut)
        {
            return RodControllerRules.AwayStep(ref _wasAway, ref _stowed, RodControllerRules.IsAway(swimming, climbing), rodOut);
        }

        [SetUp]
        public void SetUp()
        {
            _wasAway = false;
            _stowed = false;
        }

        // Catches: the M1 swim behaviour lost (stow on entering water, equip on leaving it).
        [Test]
        public void AwayStep_SwimWithRodOut_StowsThenEquipsOnLeaving()
        {
            Assert.AreEqual(RodAwayAction.Stow, Step(true, false, true));
            Assert.AreEqual(RodAwayAction.None, Step(true, false, false), "still swimming");
            Assert.AreEqual(RodAwayAction.Equip, Step(false, false, false));
        }

        // Catches (the research bug): away read from IsSwimming only, so the swim -> ladder hand-over (IsSwimming clears on ladder entry)
        // equips the rod mid-ladder.
        [Test]
        public void AwayStep_SwimThenClimb_StaysStowedAcrossTheHandOver()
        {
            Assert.AreEqual(RodAwayAction.Stow, Step(true, false, true));
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false), "swim -> climb: still away");
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false));
            Assert.AreEqual(RodAwayAction.Equip, Step(false, false, false), "off the ladder onto the deck");
        }

        // Catches: climbing not counted as away (the rod would stay out on a ladder).
        [Test]
        public void AwayStep_ClimbFromTheDeck_StowsThenEquips()
        {
            Assert.AreEqual(RodAwayAction.Stow, Step(false, true, true));
            Assert.AreEqual(RodAwayAction.Equip, Step(false, false, false));
        }

        // Catches: equipping a rod the player did not have out.
        [Test]
        public void AwayStep_RodNotOut_NeverEquipsAfterwards()
        {
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false));
            Assert.AreEqual(RodAwayAction.None, Step(true, false, false));
            Assert.AreEqual(RodAwayAction.None, Step(false, false, false));
        }

        // Catches: actions repeated every frame in a steady state.
        [Test]
        public void AwayStep_SteadyState_NoAction()
        {
            Assert.AreEqual(RodAwayAction.None, Step(false, false, true));
            Assert.AreEqual(RodAwayAction.None, Step(false, false, true));
            Assert.AreEqual(RodAwayAction.Stow, Step(false, true, true));
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false));
        }
    }
}
```

### L3.2 Code

`RodControllerRules.cs`: add above the class (namespace level, same file):
```csharp
    // What swimming or climbing asks the owner controller to do this frame (R70, M2-D31).
    internal enum RodAwayAction : byte { None = 0, Stow = 1, Equip = 2 }
```
and inside the class:
```csharp
        // Swimming or on a ladder: the rod is put away (spec 7.3, R70).
        public static bool IsAway(bool swimming, bool climbing)
        {
            return swimming || climbing;
        }

        // Edge-driven: Stow when going away with the rod out (remembered), Equip when coming back if it was put away by this rule.
        public static RodAwayAction AwayStep(ref bool wasAway, ref bool stowedByAway, bool away, bool rodOut)
        {
            if (away == wasAway) return RodAwayAction.None;
            wasAway = away;
            if (away)
            {
                if (!rodOut) return RodAwayAction.None;
                stowedByAway = true;
                return RodAwayAction.Stow;
            }

            if (!stowedByAway) return RodAwayAction.None;
            stowedByAway = false;
            return RodAwayAction.Equip;
        }
```

`FishingRodController.cs`:
1. Line 62: `private bool _wasSwimming, _swimStowed;` -> `private bool _wasAway, _awayStowed;`
2. Lines 114-115 (`OnEnable`) become:
```csharp
            _awayStowed = false;
            _wasAway = RodControllerRules.IsAway(_locomotion.IsSwimming, _locomotion.IsClimbing);
```
3. Replace `HandleSwim` (lines 236-254) with:
```csharp
        // Swimming or climbing a ladder (R70) puts the rod away (remembered; a line out is recalled by ForceEnd) and brings it back once both
        // are over (spec 7.3, contract M2-D31). Entry onto a ladder is blocked while RodFighting, so a fight never reaches this from a ladder.
        private void HandleSwim()
        {
            bool swimming = _locomotion.IsSwimming;
            bool away = RodControllerRules.IsAway(swimming, _locomotion.IsClimbing);
            switch (RodControllerRules.AwayStep(ref _wasAway, ref _awayStowed, away, RodOut))
            {
                case RodAwayAction.Stow:
                    Stow(swimming ? RodForceReason.Swim : RodForceReason.Stow);
                    break;
                case RodAwayAction.Equip:
                    Equip();
                    break;
            }
        }
```
No new `RodForceReason` value (it is unused by `ForceEnd` and would touch Core and tests for nothing).

### L3.3 Verify
1. `compile` -> PASS.
2. `test-edit -Filter "RodAwayStepTests|RodControllerHelperTests"` -> pass.
3. Mutation: make `IsAway` return `swimming` only; `AwayStep_SwimThenClimb_StaysStowedAcrossTheHandOver` and `AwayStep_ClimbFromTheDeck_StowsThenEquips` go red. Restore.
4. Full `test-edit` and full `test-play` (background): green.
5. Restore the three scenes.

### L3.4 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Fishing/Owner/RodControllerRules.cs Assets/_Project/Fishing/Owner/FishingRodController.cs Assets/_Project/Tests/EditMode/RodAwayStepTests.cs Assets/_Project/Tests/EditMode/RodAwayStepTests.cs.meta
git -C C:\Dev\FishGame commit -m "feat(rod): swimming or climbing puts the rod away and brings it back (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## L4 - Locomotion: the volume-driven ladder climb (inert until the dock has volumes)

**Header**
- **Goal:** `PlayerLocomotion` gains the R70 climb (D1-D3) next to the M1 WaterExit lerp, which keeps running unchanged. No scene has a `LadderVolume` yet, so behaviour is unchanged and every test stays green.
- **Files:** edit `Assets/_Project/Gameplay/Player/PlayerLocomotion.cs` only.
- **Depends on:** L1, L2, L3. **Implementer:** Sonnet. **Reviewer:** Opus (high).
- **Tests:** none new (the scene has no volumes; L5 adds the PlayMode tests with the volumes). Gate: compile, full EditMode and PlayMode green.

### L4.1 Edits (exact)

1. **Class comment** (lines 9-16): replace the first three lines (9-11) with:
```csharp
    // Authority: owner only. PlayerNetState enables this component (after the CharacterController) on the owning peer.
    // Moves the owner's CharacterController from the routed PlayerIntent; NetworkTransform (AuthorityMode Owner) replicates
    // the result to everyone. The host never moves players. Rules: spec 7.3; contract 7.12, D25, D26, D36, M2-D29.
    // Ladders (R70): the owner alone decides a climb from the static LadderVolumes; no RPC, no ladder state on the wire (no NetClimbing in M2,
    // M2-D32). Entering a ladder writes NetSwimming false. While climbing there is no gravity and Move goes along the camera look.
```
Keep lines 12-16 as they are, but in line 16 change "no sprint, jump or climb start" to "no sprint, jump or ladder entry".

2. **Fields:** after `private JumpAssist _jump;` add:
```csharp
        private PlayerCamera _camera;   // pitch for the ladder climb; null on test rigs (pitch 0 then)
        private ClimbGate _climbGate;
        private LadderVolume _ladder;   // the volume that held the capsule centre on the last climbing frame
        private bool _exitLerp;         // the M1 WaterExit lerp is running (only the ramp has an exit after L5); removed in L7
```
3. **Awake:** add `_camera = GetComponent<PlayerCamera>();` after `_net = ...`.
4. **OnEnable:** after `IsClimbing = false;` add:
```csharp
            _exitLerp = false;
            _ladder = null;
            _climbGate.Reset();
```
5. **OnDisable:** replace the first block (lines 77-80, `if (IsClimbing && _cc != null) { _cc.enabled = true; }`) with:
```csharp
            if (IsClimbing && _exitLerp && _cc != null)
            {
                _cc.enabled = true;
            }

            _exitLerp = false;
            _ladder = null;
            _climbGate.Reset();
```
6. **ApplyTeleport:** `bool restoreController = _cc.enabled || IsClimbing;` becomes `bool restoreController = _cc.enabled || (IsClimbing && _exitLerp);`. After `IsClimbing = false;` add `_exitLerp = false; _ladder = null; _climbGate.Reset();` (three statements on their own lines).
7. **Rename the M1 lerp** (behaviour unchanged): `BeginClimb(Vector3 target)` -> `BeginExitLerp(Vector3 target)`, with `_exitLerp = true;` added after `IsClimbing = true;`. `UpdateClimb(float dt)` -> `UpdateExitLerp(float dt)`, with `_exitLerp = false;` added after `IsClimbing = false;`. `TryBeginClimb` calls `BeginExitLerp(top)`. Put the comment `// M1 WaterExit lerp: the ramp only after L5; deleted in L7.` above `TryBeginClimb`.
8. **Update:** replace the whole method with:
```csharp
        private void Update()
        {
            _input = ResolveInput();
            if (_input == null || _cc == null)
            {
                return;
            }

            if (IsPaused())
            {
                Velocity = Vector3.zero;
                IsSprinting = false;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            PlayerIntent intent = _input.Intent;
            transform.Rotate(0f, intent.LookYawDeg, 0f);

            // M1 WaterExit lerp (the CharacterController is off while it runs). Deleted in L7.
            if (IsClimbing && _exitLerp)
            {
                UpdateExitLerp(dt);
                SetContext();
                return;
            }

            if (!_cc.enabled)
            {
                return;
            }

            // Resolved once per frame: the effective context (aiming speed) and whether movement is locked (no walk, jump or ladder entry).
            InputContext ctx = ResolveContext();
            bool locked = MovementLocked(ctx);
            bool aiming = GameplayContextArbiter.IsAiming(ctx);
            _climbGate.Tick(dt);

            if (IsClimbing)
            {
                UpdateLadderClimb(dt, in intent, locked);
                SetContext();
                return;
            }

            // R70: touching a ladder starts the climb (walking, wading, swimming or off the deck edge; no button). Not while locked
            // (RodFighting) and not during the re-entry grace after topping out.
            if (!locked && _climbGate.CanEnter)
            {
                LadderVolume ladder = LadderVolume.FindContaining(CapsuleCenter());
                if (ladder != null)
                {
                    BeginLadderClimb(ladder);
                    SetContext();
                    return;
                }
            }

            Vector3 pos = transform.position;
            float surfaceY = WaterSurface.HeightAt(pos.x, pos.z);
            bool inVolume = WaterVolume.FindContaining(pos) != null;
            bool inWater = inVolume && MovementMath.IsInWater(pos.y, surfaceY);
            bool swimming = inVolume && MovementMath.IsSwimmingDepth(pos.y, surfaceY);

            if (inWater && !locked && TryBeginClimb(pos, intent))
            {
                SetContext();
                return;
            }

            // ---- from here to the end of the method: the current code, unchanged (swim flag, grounded, sprint, wish, accelerate,
            // ---- knockback, the swim / gravity branch, cc.Move, Velocity, SecondsInWater, out-of-bounds, SetContext).
        }
```
   The implementer pastes the existing lines from `if (swimming != IsSwimming)` down to the final `SetContext();` of the old method in place of the marker comment, unchanged. The old `ctx` / `locked` / `aiming` declarations (and their comment) sat above `Vector3 pos` in the old method; the new method declares them once, above the climb branch, so they must not appear a second time (the compiler reports CS0128 if they do).
9. **New methods** (put them after `AddKnockback`):
```csharp
        // R70 ladder entry (contract M2-D29). No snap and no rotation change; the CharacterController stays on.
        private void BeginLadderClimb(LadderVolume ladder)
        {
            ResetMotion();
            _ladder = ladder;
            IsClimbing = true;
            IsSprinting = false;
            IsGrounded = false;
            SecondsInWater = 0f;
            if (IsSwimming)
            {
                IsSwimming = false;
                if (_net != null)
                {
                    _net.OwnerSetSwimming(false);
                }
            }
        }

        // R70: no gravity, no acceleration ramp. Move goes along the camera look (PlayerCamera.PitchDeg, 0 without a camera) at ClimbSpeed;
        // zero input hangs in place. Locked (a source reporting LocksMovement) also hangs. The rail, posts and deck still collide.
        private void UpdateLadderClimb(float dt, in PlayerIntent intent, bool locked)
        {
            float pitch = _camera != null ? _camera.PitchDeg : 0f;
            float moveX = locked ? 0f : intent.MoveX;
            float moveY = locked ? 0f : intent.MoveY;
            MovementMath.ClimbWish(transform.eulerAngles.y, pitch, moveX, moveY, out float wx, out float wy, out float wz);
            _cc.Move(new Vector3(wx, wy, wz) * dt);
            Velocity = _cc.velocity;
            SecondsInWater = 0f;

            Vector3 center = CapsuleCenter();
            LadderVolume ladder = LadderVolume.FindContaining(center);
            if (ladder != null)
            {
                _ladder = ladder;
                return;
            }

            EndLadderClimb(_ladder != null && _ladder.IsAtOrAboveTop(center));
        }

        // Leaving the volume ends the climb. The applied climb velocity carries on (RV's flying -> falling keeps it), so a climber who tops
        // out keeps moving onto the deck; next frame is ordinary walking, falling or swimming. Topping out starts the re-entry grace.
        private void EndLadderClimb(bool throughTop)
        {
            Vector3 v = Velocity;
            IsClimbing = false;
            _ladder = null;
            _vx = v.x;
            _vz = v.z;
            _vy = v.y;
            _kx = _kz = 0f;
            _jump = JumpAssist.Create();
            IsGrounded = false;
            _climbGate.OnExit(throughTop);
        }

        private Vector3 CapsuleCenter()
        {
            return transform.TransformPoint(_cc.center);
        }
```

### L4.2 Verify
1. `compile` -> PASS.
2. Full `test-edit` (background) -> green. Full `test-play` (background) -> 64/64 (`PlayerHooksTests`, `PlayerMovementTests`, `SwimInteractGateTests` and `FishingSoloTests` matter most: behaviour must be unchanged).
3. Grep check: `LadderVolume.All` is referenced nowhere in a scene yet (`git grep -n "LadderVolume" -- Assets/_Project/Scenes` prints nothing).
4. Restore the three scenes.

### L4.3 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Gameplay/Player/PlayerLocomotion.cs
git -C C:\Dev\FishGame commit -m "feat(locomotion): volume-driven ladder climb, inert until the dock has LadderVolumes (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

**Reviewer (Opus high) checks:** the old lerp path is byte-for-byte the old behaviour; the climb never disables the CharacterController; `ApplyTeleport` and `OnDisable` clear `_ladder`, `_exitLerp` and the gate; the entry check runs after the lock and the gate; pitch sign (positive = up) matches `PlayerCamera` (`_headPivot.localRotation = Euler(-PitchDeg, 0, 0)`).

---

## L5 - The switch: ladder volumes in the dock, ladder exits gone, Swimming loses Interact

**Header**
- **Goal:** one atomic commit, because either half alone breaks `SwimInteractGateTests`. The builder replaces the two ladder `WaterExit`s with `Ladders/LadderL|R` `LadderVolume`s; the scene is rebuilt; `RouteSwimming` zeroes Interact; the gate tests are rewritten; the PlayMode climb tests arrive; the fingerprint change is recorded. The ramp keeps its exit and its lerp until L7.
- **Files:** edit `Assets/_Project/Editor/Builders/LakeDockBuilder.cs`, `Assets/_Project/Editor/Builders/LakeDockLayout.cs`, `Assets/_Project/Core/Input/InputContextRouter.cs`, `Assets/_Project/Tests/EditMode/InputContextRouterTests.cs`, `Assets/_Project/Tests/PlayMode/SwimInteractGateTests.cs` (rewrite), `Assets/_Project/Gameplay/Player/PlayerInteractor.cs` (comment), `Assets/_Project/Tests/EditMode/PlayerUpdateOrderTests.cs` (comments), `Assets/_Project/Tests/PlayMode/PlayerHooksTests.cs` (one comment line); new `Assets/_Project/Tests/PlayMode/LadderClimbTests.cs`; generated `Assets/_Project/Scenes/Spot_LakeDock.unity`. Temporary (never committed): `Assets/_Project/Tests/EditMode/TempLakeDockFingerprint.cs` (Appendix A).
- **Depends on:** L4. **Implementer:** Sonnet. **Reviewer:** Opus (high).

### L5.1 Fingerprint "before" (first, before any edit)
1. Create `Assets/_Project/Tests/EditMode/TempLakeDockFingerprint.cs` from Appendix A.
2. `$env:FG_FP_LABEL = 'l5-before'; powershell ... $u test-edit -Filter "TempLakeDockFingerprint"` -> PASS; the file `$env:FG_OUT\ladders\fingerprint-l5-before.txt` exists.
3. Sanity: it lists `C Exits/LadderL`, `C Exits/LadderR`, `C Exits/Ramp`, the deck, 14 posts, `Dock/Ladder_L`, `Dock/Ladder_R`, `Dock/RampDeck`, the rocks, trunks, canopies, lakebed, shore and `WaterVolume`. If not, fix the temp test first.
4. Restore the three scenes (the test opened LakeDock).

### L5.2 Tests first

**`InputContextRouterTests.cs`:**
- In `InputContextRouter_Swimming_BlocksSprintAndShove` rename the method to `InputContextRouter_Swimming_BlocksSprintShoveAndInteract` and change `Assert.IsTrue(i.InteractPressed, "Interact");` to `Assert.IsFalse(i.InteractPressed, "Interact (R70: no climb-at-exit press)");`.
- Replace `InputContextRouter_Swimming_InteractClimbsButDoesNotReachInteractables` (and its comment) with:
```csharp
        // R70: no button climbs out of the water any more (ladders are walk-in, the ramp is a slope), so Swimming zeroes Interact and stays
        // dead for interactables. Catches: a swimmer's E reaching the bell through the router or through PlayerInteractor.
        [Test]
        public void InputContextRouter_Swimming_InteractIsZeroedAndNotLive()
        {
            Assert.IsFalse(InputContextRouter.InteractLive(InputContext.Swimming), "swimming must not drive PlayerInteractor");
            Assert.IsFalse(InputContextRouter.Route(InputContext.Swimming, AllActive()).InteractPressed, "Swimming zeroes Interact");
        }
```

**`SwimInteractGateTests.cs`:** rewrite the whole file:
```csharp
// Authority: test-only. Spec 8.2 (R70): nothing can be interacted with from the water or from a ladder, and a ladder is entered by walking or
// swimming into its LadderVolume, with no button. LadderR's volume (x 1.55..2.55, y -1.4..1.6, z -42..-41) is ~1.3 m from the bell
// (1.1, 0.6, -40.6), inside PlayerInteractor.CastRange. Input is scripted through FakeInputService; the real owner locomotion and interactor run.
using System.Collections;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class SwimInteractGateTests
    {
        private const float LadderZ = -41.5f;
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: a ladder that still needs Jump/Interact (the M1 exit), entry that forgets to write NetSwimming false, and a swimmer whose
        // forward input never reaches the volume. Only MoveY is ever set.
        [UnityTest]
        public IEnumerator Swimming_HoldForwardIntoLadderVolume_ClimbsWithNoButton()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            var root = new Vector3(3.0f, -1.2f, LadderZ);   // in the water 0.45 m outside LadderR's outer face, facing the dock (-x)
            rig.Locomotion.ApplyTeleport(root, -90f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(InWater(rig), Is.True, "precondition: in the water at " + rig.Player.transform.position);
            Assert.That(LadderVolume.FindContaining(Center(rig)), Is.Null, "precondition: outside every ladder volume");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "holding W into the ladder volume never started a climb");
            rig.Fake.Frame = default;
            yield return null;

            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Climbing));
            Assert.That(rig.Locomotion.IsSwimming, Is.False, "a climber is not swimming");
            yield return PlayModeHarness.WaitUntil(() => !rig.Player.IsSwimming, 1f, "NetSwimming was not written false on ladder entry");
            rig.Log.AssertEmpty("swim into the ladder");
        }

        // Catches: PlayerInteractor or the router live while Climbing: from inside LadderR's volume at wading depth, aimed at the bell, one E
        // press would ring it (and, in a debug build, sell the creel).
        [UnityTest]
        public IEnumerator Climbing_InteractAimedAtBell_DoesNotRingBell()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            DockBell bell = Object.FindAnyObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            PlayerInteractor interactor = rig.Player.GetComponent<PlayerInteractor>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(interactor, Is.Not.Null);

            var item = new CreelItem { ItemIndex = 1, WeightG = 300, ValueCoins = 7, Quality = CatchQuality.Normal };
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            int coinsBefore = rig.Creel.NetDebugCoins.Value;
            int ringsBefore = bell.LocalRingCount;
            int rangEvents = 0;
            bell.Rang += () => rangEvents++;

            var root = new Vector3(2.05f, -0.9f, LadderZ);   // centre of LadderR's volume at wading depth; climbing hangs here (no gravity)
            yield return AimAtBell(rig, camera, bell, root);
            rig.Locomotion.ApplyTeleport(root, YawTo(root, bell));
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing && rig.Fake.GameplayContext == InputContext.Climbing,
                PlayModeHarness.DefaultTimeoutSeconds, "the teleported player never started climbing at " + root);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            Assert.That(CastReachesBell(camera, bell), Is.True, "precondition: the aimed sphere cast must hit the bell from " + rig.Player.transform.position);
            Assert.That(bell.CanInteract(0UL), Is.True, "precondition: the bell is off cooldown");
            Assert.That(interactor.Current, Is.Null, "a climber has no current interactable even with the bell in cast range");

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { InteractPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);   // any stray InteractRpc would have landed by now

            Assert.That(rig.Locomotion.IsClimbing, Is.True, "E does nothing on a ladder: still hanging");
            Assert.That(bell.LocalRingCount, Is.EqualTo(ringsBefore), "the bell must not ring from a ladder");
            Assert.That(rangEvents, Is.EqualTo(0));
            Assert.That(rig.Creel.Count, Is.EqualTo(2), "the debug bell sell must not run");
            Assert.That(rig.Creel.NetDebugCoins.Value, Is.EqualTo(coinsBefore));
            rig.Log.AssertEmpty("interact on the ladder");
        }

        // Catches: the same gate in open water (no ladder, bell in range): the press must do nothing.
        [UnityTest]
        public IEnumerator Swimming_InteractInOpenWater_DoesNotRingBell()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            DockBell bell = Object.FindAnyObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            Assert.That(camera, Is.Not.Null, "PlayerCamera missing on PlayerFP");
            int ringsBefore = bell.LocalRingCount;

            // Deep enough (bed ~ -1.6 m) to keep swimming, outside every ladder volume and exit, bell about 2.5 m away.
            var root = new Vector3(2.6f, -1.45f, -39.0f);
            yield return AimAtBell(rig, camera, bell, root);
            rig.Locomotion.ApplyTeleport(root, YawTo(root, bell));
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsSwimming && rig.Fake.GameplayContext == InputContext.Swimming,
                PlayModeHarness.DefaultTimeoutSeconds, "the teleported player never reached the Swimming context at " + root);
            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Swimming), "precondition: still swimming at " + rig.Player.transform.position);
            Assert.That(LadderVolume.FindContaining(Center(rig)), Is.Null, "precondition: outside every ladder volume");
            Assert.That(WaterExit.FindContaining(rig.Player.transform.position), Is.Null, "precondition: outside every exit");
            Assert.That(CastReachesBell(camera, bell), Is.True, "precondition: the aimed sphere cast must hit the bell from " + rig.Player.transform.position);

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { InteractPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);

            Assert.That(rig.Locomotion.IsClimbing, Is.False, "no ladder here, nothing to climb");
            Assert.That(bell.LocalRingCount, Is.EqualTo(ringsBefore), "the bell must not ring from a swimmer's Interact press");
            rig.Log.AssertEmpty("swim interact in open water");
        }

        internal static Vector3 Center(FishingRig rig)
        {
            return rig.Player.transform.TransformPoint(rig.Player.GetComponent<CharacterController>().center);
        }

        internal static bool InWater(FishingRig rig)
        {
            Vector3 p = rig.Player.transform.position;
            return p.y < WaterSurface.HeightAt(p.x, p.z) - MovementMath.WadeDepth;
        }

        private static float YawTo(Vector3 root, DockBell bell)
        {
            Vector3 toBell = bell.transform.position - root;
            return Mathf.Atan2(toBell.x, toBell.z) * Mathf.Rad2Deg;
        }

        // Pitches the look (while the player still stands on the deck) so that, from `root`, the camera ray passes through the bell
        // collider's centre. The pitch survives the teleport.
        private static IEnumerator AimAtBell(FishingRig rig, PlayerCamera camera, DockBell bell, Vector3 root)
        {
            Collider collider = bell.GetComponentInChildren<Collider>();
            Assert.That(collider, Is.Not.Null, "the bell needs a collider on the Interactable layer");
            Vector3 eye = root + Vector3.up * MovementMath.EyeHeight;
            Vector3 to = collider.bounds.center - eye;
            float horizontal = new Vector2(to.x, to.z).magnitude;
            float pitch = Mathf.Atan2(to.y, horizontal) * Mathf.Rad2Deg;
            yield return FishingPlayModeDriver.SetPitch(rig, camera, pitch);
        }

        private static bool CastReachesBell(PlayerCamera camera, DockBell bell)
        {
            Transform origin = camera.Camera.transform;
            return Physics.SphereCast(origin.position, PlayerInteractor.CastRadius, origin.forward, out RaycastHit hit,
                       PlayerInteractor.CastRange, Layers.Mask(Layers.Interactable), QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<DockBell>() == bell;
        }
    }
}
```

**New `Assets/_Project/Tests/PlayMode/LadderClimbTests.cs`** (L6 and L7 add more tests to this file):
```csharp
// Authority: test-only. R70 ladders on the real Spot_LakeDock (spec 7.3, contract M2-D29): geometry, climb up from the water at three look
// pitches (the 7.8 #3 check at 45 deg), stepping off the deck edge to descend, hanging, the deck-edge boundary and the top-out grace.
// Input is scripted through FakeInputService; the real owner locomotion runs. Times are game time (Time.time).
using System.Collections;
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class LadderClimbTests
    {
        private const float LadderZ = -41.5f, DeckTopY = 0.6f, DeckEdgeX = 1.5f;
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: a ladder volume missing, misplaced, the wrong size, not a trigger, or on the Water layer (CastProbe would then read the box
        // top at y 1.6 as the water surface next to the ladder).
        [UnityTest]
        public IEnumerator LakeDock_LadderVolumes_TwoMirroredTriggersOffTheCastMask()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);

            Assert.That(LadderVolume.All.Count, Is.EqualTo(2), "two ladder volumes");
            bool left = false, right = false;
            foreach (LadderVolume v in LadderVolume.All)
            {
                BoxCollider box = v.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null, v.name);
                Assert.That(box.isTrigger, Is.True, v.name + " is a trigger");
                Assert.That(v.gameObject.layer, Is.EqualTo(Layers.Default), v.name + " layer");
                Assert.That(CastProbe.CastMask & Layers.Mask(v.gameObject.layer), Is.EqualTo(0), v.name + " is outside CastProbe.CastMask");
                Bounds b = box.bounds;
                float side = Mathf.Sign(b.center.x);
                left |= side < 0f;
                right |= side > 0f;
                Assert.That(Mathf.Abs(b.min.x * side - 1.55f) < 1e-3f || Mathf.Abs(b.max.x * side - 1.55f) < 1e-3f, Is.True, v.name + " inner face at |x| 1.55");
                Assert.That(b.size.x, Is.EqualTo(1f).Within(1e-3f), v.name + " width");
                Assert.That(b.min.y, Is.EqualTo(-1.4f).Within(1e-3f), v.name + " bottom");
                Assert.That(b.max.y, Is.EqualTo(1.6f).Within(1e-3f), v.name + " top (fallback 1.9, risk 1)");
                Assert.That(b.min.z, Is.EqualTo(-42f).Within(1e-3f), v.name + " min z");
                Assert.That(b.max.z, Is.EqualTo(-41f).Within(1e-3f), v.name + " max z");
            }

            Assert.That(left && right, Is.True, "one volume on each side of the dock");
            Assert.That(WaterExit.All.Count, Is.EqualTo(1), "only the ramp keeps a WaterExit");
            Assert.That(WaterExit.All[0].name, Is.EqualTo("Ramp"));

            WaterProbe probe = CastProbe.Probe(2.05f, LadderZ);
            Assert.That(probe.FirstHitIsWater, Is.True, "a cast beside the ladder still lands on water");
            Assert.That(probe.WaterY, Is.LessThan(0.3f), "the water height beside the ladder is the lake, not the ladder box top");
            _rig.Log.AssertEmpty("ladder geometry");
        }

        // Catches (spec 7.8 #3): the climb-out failing or taking longer than 2 s at a 45 deg look-up (pitch sign wrong, Move zeroed while
        // climbing, the volume top too low, the exit velocity dropped).
        [UnityTest]
        public IEnumerator Ladder_ClimbUpFromWater_Pitch45_LandsOnDeckWithin2s()
        {
            yield return ClimbUpFromWater(45f, 2.0f);
        }

        // Catches: a shallow look-up that cannot top out (the climber slides up the rail but never gets over the deck lip).
        [UnityTest]
        public IEnumerator Ladder_ClimbUpFromWater_Pitch30_LandsOnDeck()
        {
            yield return ClimbUpFromWater(30f, 3.0f);
        }

        // Catches: a steep look-up that leaves the box through the top short of the deck and falls back into the lake.
        [UnityTest]
        public IEnumerator Ladder_ClimbUpFromWater_Pitch60_LandsOnDeck()
        {
            yield return ClimbUpFromWater(60f, 3.0f);
        }

        // Catches: no entry from the deck edge (R70 "step off the deck edge"), free fall instead of a controlled descent, input that keeps
        // moving the climber after release (no hang), and a climber who cannot leave the ladder sideways into the water.
        [UnityTest]
        public IEnumerator Ladder_StepOffDeckEdgeLookingDown_DescendsHangsAndLeavesIntoWater()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            // -70 deg: W descends at 2.26 m/s and drifts outward at only 0.82 m/s, so the climber reaches the water well inside the box
            // (at -60 the 1.2 m/s outward drift would carry the capsule centre past the outer face, x 2.55, before y -0.5).
            yield return FishingPlayModeDriver.SetPitch(rig, camera, -70f);

            rig.Locomotion.ApplyTeleport(new Vector3(1.2f, 0.65f, LadderZ), 90f);   // on the deck, facing +x toward LadderR
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            Assert.That(rig.Locomotion.IsClimbing, Is.False, "precondition: not on the ladder yet");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "walking off the deck edge never started a climb");
            Assert.That(rig.Player.transform.position.y, Is.GreaterThan(DeckTopY - 0.15f), "caught at the deck edge, not after a fall");

            float maxDownSpeed = 0f;
            double until = Time.realtimeSinceStartupAsDouble + 5.0;
            while (rig.Player.transform.position.y > -0.5f)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble < until, Is.True, "never descended below y -0.5 (at " + rig.Player.transform.position + ")");
                Assert.That(rig.Locomotion.IsClimbing, Is.True, "fell off the ladder at " + rig.Player.transform.position);
                maxDownSpeed = Mathf.Max(maxDownSpeed, -rig.Locomotion.Velocity.y);
                yield return null;
            }

            Assert.That(maxDownSpeed, Is.LessThanOrEqualTo(MovementMath.ClimbSpeed + 0.05f), "descent faster than ClimbSpeed means gravity acted");
            Assert.That(maxDownSpeed, Is.GreaterThan(1.5f), "W while looking down 70 deg descends at about 2.26 m/s");

            // Release: instant stop and hang.
            yield return FishingPlayModeDriver.Step(rig.Fake, default);
            yield return null;
            Assert.That(rig.Locomotion.Velocity.magnitude, Is.LessThan(1e-3f), "release stops the climber at once");
            float hangY = rig.Player.transform.position.y;
            yield return PlayModeHarness.WaitGameSeconds(0.3f, PlayModeHarness.DefaultTimeoutSeconds, "hang");
            Assert.That(rig.Locomotion.IsClimbing, Is.True, "still on the ladder");
            Assert.That(Mathf.Abs(rig.Player.transform.position.y - hangY), Is.LessThan(0.005f), "hangs in place with no input");

            // Leave sideways: look level, W toward +x out of the box (outer face x 2.55) into the water.
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 0f);
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 2f, "never left the ladder through the outer face");
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(SwimInteractGateTests.InWater(rig), Is.True, "left the ladder into the water");
            Assert.That(rig.Fake.GameplayContext, Is.Not.EqualTo(InputContext.Climbing));
            rig.Log.AssertEmpty("descend the ladder");
        }

        // Catches: an inner face reaching onto the deck (walking along the deck beside a ladder would grab the player).
        [UnityTest]
        public IEnumerator Ladder_StandingOnDeckInsideTheEdge_DoesNotClimb()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            rig.Locomotion.ApplyTeleport(new Vector3(1.4f, 0.65f, LadderZ), 90f);   // capsule centre x 1.4 < inner face 1.55
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            double until = Time.realtimeSinceStartupAsDouble + 0.5;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                Assert.That(rig.Locomotion.IsClimbing, Is.False, "standing on the deck at x 1.4 must not climb");
                yield return null;
            }

            rig.Log.AssertEmpty("deck beside the ladder");
        }

        // Catches: no re-entry grace (the falling climber is caught again the moment the capsule centre drops back into the box, ~0.15 s
        // after topping out) and a grace that never ends (the climber would fall past the ladder into the lake).
        [UnityTest]
        public IEnumerator Ladder_TopOut_BlocksReentryFor025s_ThenCatchesTheFall()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 45f);

            // Inside LadderR near its top, facing away from the dock (+x) so the top-out is not over the deck.
            rig.Locomotion.ApplyTeleport(new Vector3(2.05f, 0.6f, LadderZ), 90f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 2f, "never started climbing near the top");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 2f, "never topped out");
            float exitTime = Time.time;
            rig.Fake.Frame = default;
            Assert.That(SwimInteractGateTests.Center(rig).y, Is.GreaterThan(1.55f), "precondition: left through the top face");

            int insideButFree = 0;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (!rig.Locomotion.IsClimbing && LadderVolume.FindContaining(SwimInteractGateTests.Center(rig)) != null) insideButFree++;
                return rig.Locomotion.IsClimbing;
            }, 2f, "the ladder never caught the falling climber after the grace");
            float gap = Time.time - exitTime;
            TestContext.WriteLine($"top-out re-entry after {gap:F3} s, {insideButFree} frames inside the box without climbing");

            Assert.That(gap, Is.GreaterThanOrEqualTo(MovementMath.ClimbReentryGrace - 0.01f), "re-entry inside the 0.25 s grace");
            Assert.That(gap, Is.LessThan(0.6f), "the grace never ended");
            Assert.That(insideButFree, Is.GreaterThanOrEqualTo(2), "the centre was back in the box but the grace held the climb off");
            Assert.That(rig.Player.transform.position.y, Is.GreaterThan(0f), "caught above the lake");
            rig.Log.AssertEmpty("top-out grace");
        }

        // Shared body of the three climb-out tests. Starts in the water 0.45 m outside LadderR, facing the dock, with the given look pitch.
        private IEnumerator ClimbUpFromWater(float pitchDeg, float limitSeconds)
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            yield return FishingPlayModeDriver.SetPitch(rig, camera, pitchDeg);   // on the deck, before the teleport; the pitch survives it

            rig.Locomotion.ApplyTeleport(new Vector3(3.0f, -1.2f, LadderZ), -90f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(SwimInteractGateTests.InWater(rig), Is.True, "precondition: in the water");
            Assert.That(LadderVolume.FindContaining(SwimInteractGateTests.Center(rig)), Is.Null, "precondition: outside the ladder volume");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never reached the ladder");
            float start = Time.time;
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Climbing));

            yield return PlayModeHarness.WaitUntil(() => OnDeck(rig), limitSeconds + 3f,
                "never got onto the deck at pitch " + pitchDeg + " (at " + rig.Player.transform.position + ")");
            float took = Time.time - start;
            rig.Fake.Frame = default;
            TestContext.WriteLine($"climb-out at pitch {pitchDeg}: {took:F2} s");
            Assert.That(took, Is.LessThanOrEqualTo(limitSeconds), "climb-out at pitch " + pitchDeg);

            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(rig.Locomotion.IsSwimming, Is.False);
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Free), "back to Free on the deck");
            rig.Log.AssertEmpty("climb out at pitch " + pitchDeg);
        }

        private static bool OnDeck(FishingRig rig)
        {
            Vector3 p = rig.Player.transform.position;
            return rig.Locomotion.IsGrounded && !rig.Locomotion.IsClimbing && p.x < DeckEdgeX && Mathf.Abs(p.y - DeckTopY) < 0.08f;
        }
    }
}
```
Notes for the implementer:
- `PlayModeHarness.WaitUntil(Func<bool>, float, string)` evaluates the lambda once per frame, so the counting lambda in the grace test is safe.
- If `WaterProbe` or `CastProbe` is not visible from the PlayMode assembly, add the missing `using` (it is in `FishGame.Fishing`); do not change production visibility.
- If `FishingRig`/`FishingPlayModeDriver` members you need are internal, they are already in this assembly.

### L5.3 Code

**`LakeDockLayout.cs`:** after the `LadderZ` line (line 74) add:
```csharp
        // R70 walk-in ladder volumes (contract M2-D29), one per ladder, mirrored in x. Tuning knobs for the climb-out: LadderVolumeTopY
        // (fallback 1.9 if a climb-out test fails, plan risk 1) and LadderVolumeInnerX (move out to ~1.75 if the deck edge feels grabby).
        public const float LadderVolumeInnerX = 1.55f, LadderVolumeOuterX = 2.55f, LadderVolumeBottomY = -1.4f, LadderVolumeTopY = 1.6f, LadderVolumeDepth = 1f;
```

**`LakeDockBuilder.cs`:**
1. Header comment, line 3: append the sentence: `R70 (ladders plan L5/L7) changes it on purpose: the ladder exits became Ladders/LadderL|R LadderVolumes (layer Default), and Exits/Ramp is the ramp foot (Appendix B of the plan).`
2. In `Build()`, after `AddExits();` insert `AddLadders();` (the new root goes after `Exits`, before `BellAnchor`).
3. Replace `AddExits()` with:
```csharp
        // Contract 6.5 exits. R70: only the shore ramp keeps a WaterExit; the ladders are LadderVolumes (AddLadders). L7 turns this into the
        // ramp foot.
        static void AddExits()
        {
            GameObject exits = Root("Exits", Layers.Water);
            AddExit(exits.transform, "Ramp", new Vector3(12f, -0.9f, -44f), new Vector3(3f, 2.4f, 4f), new Vector3(12f, 0.45f, -50.5f));
            BuilderUtil.SetLayerRecursive(exits, Layers.Water);
        }

        // R70 (contract M2-D29): one walk-in LadderVolume per ladder, mirrored in x, on layer Default so CastProbe's water mask never sees it.
        static void AddLadders()
        {
            GameObject root = Root("Ladders", Layers.Default);
            AddLadderVolume(root.transform, "LadderL", -1f);
            AddLadderVolume(root.transform, "LadderR", 1f);
        }

        static void AddLadderVolume(Transform parent, string name, float side)
        {
            var go = new GameObject(name);
            go.layer = Layers.Default;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(
                side * (LakeDockLayout.LadderVolumeInnerX + LakeDockLayout.LadderVolumeOuterX) * 0.5f,
                (LakeDockLayout.LadderVolumeBottomY + LakeDockLayout.LadderVolumeTopY) * 0.5f,
                LakeDockLayout.LadderZ);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(LakeDockLayout.LadderVolumeOuterX - LakeDockLayout.LadderVolumeInnerX,
                LakeDockLayout.LadderVolumeTopY - LakeDockLayout.LadderVolumeBottomY, LakeDockLayout.LadderVolumeDepth);
            go.AddComponent<LadderVolume>();
        }
```

**`InputContextRouter.cs`:**
1. In `RouteSwimming` add `i.InteractPressed = false;` after `i.SprintHeld = false;`, and change its comment to:
   `// Swimming: Move, Look, Jump, Pause pass. Sprint, Primary*, Secondary*, Interact, Shove, Drop, Equip zeroed (R70: no climb-at-exit press).`
2. Replace the `InteractLive` comment (lines 30-32) with:
```csharp
        // World interactables (PlayerInteractor): Free, RodReady, RodWaiting -> true (spec 8.2). Swimming and Climbing are false and their
        // routes zero Interact too (R70: nothing is interacted with from the water or a ladder). Carrying is false in M2 (ruling C-Q2; M5 revisits).
```
3. File header line 2: no change.

**`PlayerInteractor.cs`:** replace the comment line `// after PlayerLocomotion (0): a climb started this frame flips the context to Climbing first, so the same press never reaches an interactable` with:
`// after PlayerLocomotion (0): a ladder entered this frame flips the context to Climbing first, so a press on that frame never reaches an interactable (defensive since R70: no press starts a climb)`.

**`PlayerUpdateOrderTests.cs`:** line 2 comment becomes `// (no attribute = 0). Spec 8.2: a ladder entry must flip the context to Climbing before PlayerInteractor looks at it.`; the "Catches" comment of `PlayerInteractor_RunsAfterPlayerLocomotion` becomes:
`// Catches: PlayerInteractor updating at the same order as (or before) PlayerLocomotion: on the frame a player walks into a ladder the context`
`// is still Free when an early interactor runs, so an E press on that frame could reach the bell. Defensive since R70 (no press climbs).`
Test bodies unchanged.

**`PlayerHooksTests.cs`** line 104: `jump/climb gating dropped` -> `jump/ladder-entry gating dropped`.

### L5.4 Build, fingerprint "after", tests
1. `compile` -> PASS.
2. `build` -> PASS (rebuilds `Spot_LakeDock.unity`). Check the log has the `FG LAKEDOCK` line.
3. `$env:FG_FP_LABEL = 'l5-after'; ... test-edit -Filter "TempLakeDockFingerprint"`; then
   `Compare-Object (Get-Content "$env:FG_OUT\ladders\fingerprint-l5-before.txt") (Get-Content "$env:FG_OUT\ladders\fingerprint-l5-after.txt")`.
   The differences must be exactly the L5 set of Appendix B. Anything else is a defect: fix and rerun. Paste the diff in the report.
4. `test-edit -Filter "InputContextRouterTests|PlayerUpdateOrderTests"` -> pass.
5. `test-play -Filter "LadderClimbTests|SwimInteractGateTests"` -> pass. Paste the `TestContext` lines (climb-out times, top-out gap) from the results XML or log into the report.
   - If a `Ladder_ClimbUpFromWater_*` test fails, apply risk 1's fallback: `LadderVolumeTopY = 1.9f` and the geometry test's `1.6f` -> `1.9f`; rebuild and rerun. If it still fails, stop and report (escalation).
6. Mutations: (a) in `RouteSwimming` remove the new `i.InteractPressed = false;` -> `InputContextRouter_Swimming_InteractIsZeroedAndNotLive` red. (b) In `PlayerLocomotion.EndLadderClimb` replace the velocity carry with zeros -> at least one climb-out test red (record which). (c) In `EndLadderClimb` pass `false` to `OnExit` -> `Ladder_TopOut_BlocksReentryFor025s_ThenCatchesTheFall` red. Restore all three.
7. Delete `TempLakeDockFingerprint.cs` and its `.meta`. `git status` must not list them.
8. Full `test-edit` and full `test-play` (background) -> green (PlayMode 64 + 7 new, `SwimInteractGateTests` still 3).
9. Restore `Bootstrap.unity` and `Menu.unity` only. `Spot_LakeDock.unity` is a real change: keep it. If `build` also changed prefabs or other assets, stop and report.

### L5.5 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Editor/Builders/LakeDockBuilder.cs Assets/_Project/Editor/Builders/LakeDockLayout.cs Assets/_Project/Core/Input/InputContextRouter.cs Assets/_Project/Tests/EditMode/InputContextRouterTests.cs Assets/_Project/Tests/EditMode/PlayerUpdateOrderTests.cs Assets/_Project/Gameplay/Player/PlayerInteractor.cs Assets/_Project/Tests/PlayMode/PlayerHooksTests.cs Assets/_Project/Tests/PlayMode/SwimInteractGateTests.cs Assets/_Project/Tests/PlayMode/LadderClimbTests.cs Assets/_Project/Tests/PlayMode/LadderClimbTests.cs.meta Assets/_Project/Scenes/Spot_LakeDock.unity
git -C C:\Dev\FishGame commit -m "feat(lakedock): walk-in ladder volumes replace the ladder exits; Swimming no longer passes Interact (R70)" -m "Physics fingerprint changed on purpose (plan Appendix B, L5 set). Climb-out times: <paste>." -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

**Reviewer (Opus high) checks:** the fingerprint diff is exactly Appendix B's L5 set; the temp test is gone; the volumes are on Default; no other builder output changed; the gate tests cover swimming and climbing; the climb-out times are in the commit message.

---

## L6 - Rod on a ladder: PlayMode proof

**Header**
- **Goal:** prove D6 end to end: the rod stows on ladder entry and comes back after, and a line out is recalled (not escaped) when its owner steps onto a ladder.
- **Files:** edit `Assets/_Project/Tests/PlayMode/LadderClimbTests.cs` (add two tests).
- **Depends on:** L5. **Implementer:** Sonnet. **Reviewer:** Sonnet.

### L6.1 Tests (add to `LadderClimbTests`)
```csharp
        // Catches (R70, M2-D31): the rod staying out on a ladder, popping back out mid-ladder, or never coming back after the player steps
        // back onto the deck.
        [UnityTest]
        public IEnumerator Ladder_RodOut_StowsOnEntryAndComesBackOnTheDeck()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { EquipPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, 2f, "Equip never set the Rod tool");

            rig.Locomotion.ApplyTeleport(new Vector3(1.2f, 0.65f, LadderZ), 90f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            Assert.That(rig.Player.Tool, Is.EqualTo(ToolKind.Rod), "precondition: rod out");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };   // walk off the edge (pitch 0)
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never stepped onto the ladder");
            rig.Fake.Frame = default;                                // hang at the edge
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.None, 1f, "the rod was not put away on the ladder");
            yield return PlayModeHarness.WaitGameSeconds(0.3f, PlayModeHarness.DefaultTimeoutSeconds, "hang");
            Assert.That(rig.Player.Tool, Is.EqualTo(ToolKind.None), "the rod stays away while climbing");
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Idle));

            rig.Fake.Frame = new PlayerInputFrame { MoveY = -1f };  // S at pitch 0: back toward the deck, out through the inner face
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 2f, "never stepped back onto the deck");
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, 1f, "the rod never came back after the ladder");
            Assert.That(rig.Locomotion.IsSwimming, Is.False);
            rig.Log.AssertEmpty("rod on a ladder");
        }

        // Catches: a line left out while its owner climbs (the host cast would stay open), or the cast ending as Escaped instead of a recall
        // (the bait refund only happens on the cancel path).
        [UnityTest]
        public IEnumerator Ladder_LineOut_SteppingOntoTheLadderRecallsTheCast()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;   // no bite during the test
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);   // G9: (0, 0.65, -41) facing +z
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 5f);
            int baitAfterCast = rig.Net.NetSelectedBaitCount.Value;

            // Strafe right (+x) and back (-z) from z -41 so the edge is reached at z ~ -41.6, inside LadderR's z range, passing ~0.5 m
            // clear of the bell at (1.1, 0.6, -40.6).
            rig.Fake.Frame = new PlayerInputFrame { MoveX = 1f, MoveY = -0.4f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never stepped onto the ladder with the line out");
            rig.Fake.Frame = default;

            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.None, 1f, "the rod was not put away");
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 5f);
            yield return PlayModeHarness.WaitUntil(() => rig.Net.NetSelectedBaitCount.Value == baitAfterCast + 1, 3f,
                "the cast was not recalled with a bait refund (count " + rig.Net.NetSelectedBaitCount.Value + ")");
            rig.Log.AssertEmpty("line out onto a ladder");
        }
```
Note: if `SoloHost_CancelInWaiting_RefundsBait` shows the refund differently (read it first), assert the refund the same way it does. `ForcedBiteWait` is reset by `FishingPlayModeDriver.TearDown`.

### L6.2 Verify
1. `compile`; `test-play -Filter "LadderClimbTests"` -> all pass.
2. Mutation: in `FishingRodController.HandleSwim` use `IsAway(swimming, false)`; `Ladder_RodOut_StowsOnEntryAndComesBackOnTheDeck` goes red. Restore.
3. Full `test-play` (background) -> green. Restore the three scenes.

### L6.3 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Tests/PlayMode/LadderClimbTests.cs
git -C C:\Dev\FishGame commit -m "test(ladders): the rod stows on a ladder and comes back; a line out is recalled (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## L7 - The shore ramp: no lerp, a foot assist

**Header**
- **Goal:** D8. Delete the M1 exit lerp; the ramp exit becomes the ramp foot with the wade-not-swim rule and the one-shot kick; the `Exits` root moves to layer Default (F3).
- **Files:** edit `Assets/_Project/Core/Player/MovementMath.cs`, `Assets/_Project/Gameplay/Player/PlayerLocomotion.cs`, `Assets/_Project/Gameplay/World/WaterExit.cs` (comment), `Assets/_Project/Editor/Builders/LakeDockBuilder.cs`, `Assets/_Project/Editor/Builders/LakeDockLayout.cs`, `Assets/_Project/Tests/PlayMode/LadderClimbTests.cs` (geometry assert + ramp tests); new `Assets/_Project/Core/Player/RampAssist.cs`, `Assets/_Project/Tests/EditMode/RampAssistTests.cs`; generated `Spot_LakeDock.unity`. Temporary: `TempLakeDockFingerprint.cs` (Appendix A).
- **Depends on:** L6. **Implementer:** Sonnet. **Reviewer:** Opus (high).

### L7.1 Fingerprint "before"
Recreate Appendix A's temp test; `$env:FG_FP_LABEL = 'l7-before'`; run it; restore the three scenes.

### L7.2 Tests first

New `Assets/_Project/Tests/EditMode/RampAssistTests.cs`:
```csharp
// Authority: none (pure EditMode tests). FishGame.Core.RampAssist and MovementMath.PushingToward: the R70 shore-ramp foot (contract M2-D30).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RampAssistTests
    {
        // Catches: a retuned kick slipping in unnoticed.
        [Test]
        public void RampAssist_Kick_Is2()
        {
            Assert.AreEqual(2.0f, MovementMath.RampAssistKick);
        }

        // Catches: the assist leaking outside the foot (swimming suppressed everywhere, or kicks in open water).
        [Test]
        public void RampAssist_OutsideTheFoot_PassesSwimmingThrough()
        {
            RampAssist a = RampAssist.Create();
            Assert.IsTrue(a.Step(false, true, true, true, out float k1));
            Assert.AreEqual(0f, k1);
            Assert.IsFalse(a.Step(false, true, false, true, out float k2));
            Assert.AreEqual(0f, k2);
        }

        // Catches: no kick, a kick every frame (a launch, not a step), and still swimming inside the foot.
        [Test]
        public void RampAssist_SwimmerEntersPushingTowardTop_KicksOnceAndWades()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, true, out _);                          // swimming toward the ramp
            Assert.IsFalse(a.Step(true, true, true, true, out float kick), "wades in the foot even at swim depth");
            Assert.AreEqual(MovementMath.RampAssistKick, kick);
            Assert.IsFalse(a.Step(true, true, true, true, out float again));
            Assert.AreEqual(0f, again, "one kick per entry");
        }

        // Catches: kicking a wader who walks in (they are already on their feet).
        [Test]
        public void RampAssist_WaderEnters_NoKick()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, false, true, out _);
            a.Step(true, true, false, true, out float kick);
            Assert.AreEqual(0f, kick);
        }

        // Catches: a kick for a swimmer who drifts in without pushing, or a delayed kick when they push later (a random hop).
        [Test]
        public void RampAssist_SwimmerEntersNotPushing_NoKickEvenIfPushingLater()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, false, out _);
            a.Step(true, true, true, false, out float k1);
            a.Step(true, true, true, true, out float k2);
            Assert.AreEqual(0f, k1);
            Assert.AreEqual(0f, k2);
        }

        // Catches: a kick that can only ever fire once per session.
        [Test]
        public void RampAssist_LeaveAndReenterSwimming_KicksAgain()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, true, out _);
            a.Step(true, true, true, true, out float first);
            a.Step(false, true, true, false, out _);                         // back out into deep water, swimming
            a.Step(true, true, true, true, out float second);
            Assert.AreEqual(MovementMath.RampAssistKick, first);
            Assert.AreEqual(MovementMath.RampAssistKick, second);
        }

        // Catches: the foot acting on dry ground (the box top reaches y 0.4 over the ramp).
        [Test]
        public void RampAssist_InFootButOutOfWater_IsIgnored()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, true, out _);
            Assert.IsFalse(a.Step(true, false, false, true, out float kick));
            Assert.AreEqual(0f, kick);
        }

        // DA7's rule, now only for the kick. Catches: a sign error (walking down the ramp would kick), and the 0.5 stick threshold lost.
        [Test]
        public void PushingToward_ForwardFacingTheTop_Only()
        {
            Assert.IsTrue(MovementMath.PushingToward(1f, 0f, -1f, 0f, -8f), "facing the top");
            Assert.IsFalse(MovementMath.PushingToward(1f, 0f, 1f, 0f, -8f), "facing away");
            Assert.IsFalse(MovementMath.PushingToward(1f, 1f, 0f, 0f, -8f), "side-on (dot 0)");
            Assert.IsFalse(MovementMath.PushingToward(0.5f, 0f, -1f, 0f, -8f), "half stick is not pushing");
            Assert.IsTrue(MovementMath.PushingToward(1f, 1f, 0f, 0f, 0f), "standing on the top point");
        }
    }
}
```

Add to `LadderClimbTests.cs`:
1. In `LakeDock_LadderVolumes_TwoMirroredTriggersOffTheCastMask`, after the `WaterExit.All[0].name` assert, add:
```csharp
            WaterExit ramp = WaterExit.All[0];
            Assert.That(ramp.gameObject.layer, Is.EqualTo(Layers.Default), "the ramp foot is off the Water layer (CastProbe)");
            Bounds rb = ramp.GetComponent<BoxCollider>().bounds;
            Assert.That(rb.min.z, Is.EqualTo(-46f).Within(1e-3f), "ramp foot landward end");
            Assert.That(rb.max.z, Is.EqualTo(-39f).Within(1e-3f), "ramp foot seaward end");
```
2. Two tests:
```csharp
        // Catches (R70 ramp): a pull or lerp (the player must walk), no kick at the foot, swimming inside the foot, or a ramp that cannot be
        // walked out of the lake.
        [UnityTest]
        public IEnumerator Ramp_SwimmerHoldingForward_KicksAtTheFootAndWalksOut()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            rig.Locomotion.ApplyTeleport(new Vector3(12f, -1.45f, -37f), 180f);   // swimming 2 m seaward of the foot, facing the shore (-z)
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsSwimming, PlayModeHarness.DefaultTimeoutSeconds, "precondition: swimming");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            float maxUp = 0f;
            Vector3 prev = rig.Player.transform.position;
            double until = Time.realtimeSinceStartupAsDouble + 10.0;
            while (!(rig.Player.transform.position.z < -50f && rig.Player.transform.position.y > 0.2f && rig.Locomotion.IsGrounded))
            {
                Assert.That(Time.realtimeSinceStartupAsDouble < until, Is.True, "never walked out up the ramp (at " + rig.Player.transform.position + ")");
                yield return null;
                // IsSwimming was decided from the position at the START of this frame (= prev), not from where the move ended.
                bool prevInFootWater = WaterExit.FindContaining(prev) != null
                                       && prev.y < WaterSurface.HeightAt(prev.x, prev.z) - MovementMath.WadeDepth - 0.05f;
                if (prevInFootWater)
                    Assert.That(rig.Locomotion.IsSwimming, Is.False, "nobody swims in the ramp foot (from " + prev + ")");
                maxUp = Mathf.Max(maxUp, rig.Locomotion.Velocity.y);
                prev = rig.Player.transform.position;
            }

            rig.Fake.Frame = default;
            TestContext.WriteLine($"ramp walk-out: max upward speed {maxUp:F2} m/s");
            Assert.That(maxUp, Is.GreaterThan(1.5f), "the foot kick (2 m/s) fired; slope walking alone stays under ~0.7 m/s");
            Assert.That(rig.Locomotion.IsClimbing, Is.False, "no lerp: the ramp is walked");
            rig.Log.AssertEmpty("ramp walk-out");
        }

        // Catches: the assist pushing back a player who walks DOWN the ramp into the lake (the facing rule inverted, or a kick without push).
        [UnityTest]
        public IEnumerator Ramp_WalkingDownIntoTheLake_IsNotPushedBack()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            rig.Locomotion.ApplyTeleport(new Vector3(12f, 0.5f, -50.5f), 0f);   // on the ramp top, facing the lake (+z)
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the ramp");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            float lastZ = rig.Player.transform.position.z, maxUp = 0f;
            double until = Time.realtimeSinceStartupAsDouble + 10.0;
            while (rig.Player.transform.position.z < -38.5f)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble < until, Is.True, "never walked down past the foot (at " + rig.Player.transform.position + ")");
                float z = rig.Player.transform.position.z;
                Assert.That(z, Is.GreaterThan(lastZ - 0.02f), "pushed back toward the shore at z " + z);
                lastZ = z;
                maxUp = Mathf.Max(maxUp, rig.Locomotion.Velocity.y);
                yield return null;
            }

            rig.Fake.Frame = default;
            Assert.That(maxUp, Is.LessThan(1.0f), "no kick when walking down");
            Assert.That(SwimInteractGateTests.InWater(rig), Is.True, "ended in the lake");
            rig.Log.AssertEmpty("ramp walk-down");
        }
```

### L7.3 Code

**`MovementMath.cs`:**
1. Line 15 `public const float ClimbDuration = 0.4f, WaterTimeoutSeconds = 60f, OutOfBoundsY = -20f;` becomes `public const float WaterTimeoutSeconds = 60f, OutOfBoundsY = -20f;` (ClimbDuration deleted: it has no other user; `git grep -n ClimbDuration` must print nothing afterwards).
2. Next to `ClimbSpeed` add `public const float RampAssistKick = 2.0f;   // R70 ramp foot: one upward kick, m/s [tune] (~9 cm step-up)`.
3. `IsInWater` comment: `gates WaterExit climbs (contract D36)` -> `gates the ramp foot assist (contract D36, M2-D30)`.
4. Add:
```csharp
        // DA7's facing rule, kept only for the R70 ramp-foot kick: forward input (moveY > 0.5) while facing the target (flat dot > 0), or
        // standing on it (flat distance under 1 cm).
        public static bool PushingToward(float moveY, float forwardX, float forwardZ, float toX, float toZ)
        {
            if (!(moveY > 0.5f)) return false;
            return toX * toX + toZ * toZ < 0.0001f || forwardX * toX + forwardZ * toZ > 0f;
        }
```

New **`Assets/_Project/Core/Player/RampAssist.cs`**:
```csharp
// Core/Player/RampAssist.cs: the shore-ramp foot assist (spec 7.3, R70; contract M2-D30). Pure; PlayerLocomotion owns one per owner.
// Inside the ramp foot a player who is in water (wading or swimming depth, contract D36) wades instead of swimming: feet on the bed, walk
// speed, normal gravity. A swimmer who enters the foot while pushing toward the ramp top gets one upward kick (MovementMath.RampAssistKick)
// on that frame. Never a lerp, never a pull: without input nothing moves the player.
namespace FishGame.Core
{
    public struct RampAssist
    {
        private bool _wasInFoot;
        private bool _wasSwimming;

        public static RampAssist Create()
        {
            return default;
        }

        // Returns the swimming flag the locomotion uses this frame. kickVy is RampAssistKick on the entry frame described above, else 0.
        public bool Step(bool inRampFoot, bool inWater, bool swimmingDepth, bool pushingTowardTop, out float kickVy)
        {
            bool inFoot = inRampFoot && inWater;
            kickVy = inFoot && !_wasInFoot && _wasSwimming && pushingTowardTop ? MovementMath.RampAssistKick : 0f;
            bool swimming = !inFoot && swimmingDepth;
            _wasInFoot = inFoot;
            _wasSwimming = swimming;
            return swimming;
        }
    }
}
```

**`PlayerLocomotion.cs`** (final shape):
1. Delete: the fields `_climbT`, `_climbFrom`, `_climbTo`, `_exitLerp`; the methods `TryBeginClimb`, `BeginExitLerp`, `UpdateExitLerp`; in `Update` the whole `if (IsClimbing && _exitLerp) { ... }` block and the `if (inWater && !locked && TryBeginClimb(pos, intent)) { ... }` block; in `OnEnable`/`OnDisable`/`ApplyTeleport` every `_exitLerp` statement.
2. `OnDisable`: the first block becomes just `_ladder = null; _climbGate.Reset(); _rampAssist = RampAssist.Create();` (the CharacterController is never disabled by a climb any more).
3. `ApplyTeleport`: `bool restoreController = _cc.enabled;` and after `IsClimbing = false;`: `_ladder = null; _climbGate.Reset(); _rampAssist = RampAssist.Create();`.
4. Field: add `private RampAssist _rampAssist;` next to `_climbGate`. `OnEnable`: add `_rampAssist = RampAssist.Create();`. `BeginLadderClimb`: add `_rampAssist = RampAssist.Create();` as its last statement.
5. In `Update`, replace the part from `Vector3 pos = transform.position;` up to and including the `if (swimming != IsSwimming)` line with:
```csharp
            Vector3 pos = transform.position;
            float surfaceY = WaterSurface.HeightAt(pos.x, pos.z);
            bool inVolume = WaterVolume.FindContaining(pos) != null;
            bool inWater = inVolume && MovementMath.IsInWater(pos.y, surfaceY);
            bool swimDepth = inVolume && MovementMath.IsSwimmingDepth(pos.y, surfaceY);

            // R70 shore-ramp foot (the Ramp WaterExit; D36: wading or swimming): in water there the player wades instead of swimming, and a
            // swimmer arriving while pushing toward the ramp Top (DA7's facing rule) gets one small upward kick. No lerp, no pull.
            WaterExit foot = inWater ? WaterExit.FindContaining(pos) : null;
            bool towardTop = false;
            if (foot != null && !locked)
            {
                Vector3 toTop = foot.TopPoint - pos;
                Vector3 forward = transform.forward;
                towardTop = MovementMath.PushingToward(intent.MoveY, forward.x, forward.z, toTop.x, toTop.z);
            }

            bool swimming = _rampAssist.Step(foot != null, inWater, swimDepth, towardTop, out float kickVy);

            if (swimming != IsSwimming)
```
   (the body of that `if` and everything after stays.)
6. In the non-swimming branch, right after `if (grounded && _vy < 0f) { _vy = MovementMath.GroundedStickVelocity; }` add:
```csharp
                if (kickVy > 0f)
                {
                    _vy = Mathf.Max(_vy, kickVy);   // R70 ramp foot: one small upward help out of the water
                }
```
7. Class comment: `contract 7.12, D25, D26, D36, M2-D29.` -> `contract 7.12, D25, D26, D36 (ramp foot only since R70), M2-D29, M2-D30.`

**`WaterExit.cs`** class comment (lines 6-8) becomes:
```csharp
    // Authority: none (static world geometry). Since R70 only the shore ramp has one: the ramp foot (PlayerLocomotion, contract D36 and
    // M2-D30). Inside it a player in water wades instead of swimming, and a swimmer arriving while pushing toward TopPoint gets one small
    // upward kick (Core RampAssist). Needs exactly one trigger BoxCollider and a child named "Top" (added by LakeDockBuilder; no
    // RequireComponent on purpose, see WaterVolume). Ladders are LadderVolumes.
```

**`LakeDockLayout.cs`:** after the ramp constants (line 80) add:
```csharp
        // R70 shore-ramp foot (the only WaterExit, contract M2-D30): x 10.5..13.5, y -2.4..0.4, z -46..-39 (seaward to where swimmers find
        // their feet, bed about -1.38 m). Top is the old climb-out point, used only for "pushing toward the ramp".
        public static readonly Vector3 RampFootCenter = new Vector3(12f, -1.0f, -42.5f);
        public static readonly Vector3 RampFootSize = new Vector3(3f, 2.8f, 7f);
        public static readonly Vector3 RampTopPoint = new Vector3(12f, 0.45f, -50.5f);
```

**`LakeDockBuilder.cs`:** `AddExits()` becomes:
```csharp
        // Contract 6.5 exits since R70: only the shore-ramp foot (M2-D30). Layer Default, so CastProbe's water mask (Water layer, triggers
        // collide) never reads the box top as the water surface.
        static void AddExits()
        {
            GameObject exits = Root("Exits", Layers.Default);
            AddExit(exits.transform, "Ramp", LakeDockLayout.RampFootCenter, LakeDockLayout.RampFootSize, LakeDockLayout.RampTopPoint);
            BuilderUtil.SetLayerRecursive(exits, Layers.Default);
        }
```

### L7.4 Build, fingerprint, tests
1. `compile`; `git grep -n "ClimbDuration\|TryBeginClimb\|_exitLerp" -- Assets` prints nothing.
2. `build` -> PASS.
3. Fingerprint `l7-after`; `Compare-Object` with `l7-before`: exactly the L7 set of Appendix B. Paste the diff.
4. `test-edit -Filter "RampAssistTests|ClimbWishTests|ClimbGateTests"` -> pass.
5. `test-play -Filter "LadderClimbTests|SwimInteractGateTests|PlayerMovementTests|PlayerHooksTests"` -> pass.
6. Mutations: (a) `RampAssist.Step` drop `!_wasInFoot` -> `RampAssist_SwimmerEntersPushingTowardTop_KicksOnceAndWades` red. (b) Return `swimmingDepth` unconditionally -> the same test red, and `Ramp_SwimmerHoldingForward_KicksAtTheFootAndWalksOut` red. (c) `PushingToward` with `< 0f` -> `PushingToward_ForwardFacingTheTop_Only` red. Restore all.
7. Delete the temp fingerprint test and its `.meta`.
8. Full `test-edit` and full `test-play` (background) -> green.
9. Restore `Bootstrap.unity` and `Menu.unity`; keep `Spot_LakeDock.unity`.

### L7.5 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Core/Player/MovementMath.cs Assets/_Project/Core/Player/RampAssist.cs Assets/_Project/Core/Player/RampAssist.cs.meta Assets/_Project/Gameplay/Player/PlayerLocomotion.cs Assets/_Project/Gameplay/World/WaterExit.cs Assets/_Project/Editor/Builders/LakeDockBuilder.cs Assets/_Project/Editor/Builders/LakeDockLayout.cs Assets/_Project/Tests/EditMode/RampAssistTests.cs Assets/_Project/Tests/EditMode/RampAssistTests.cs.meta Assets/_Project/Tests/PlayMode/LadderClimbTests.cs Assets/_Project/Scenes/Spot_LakeDock.unity
git -C C:\Dev\FishGame commit -m "feat(locomotion): shore ramp foot assist replaces the 0.4 s exit lerp (R70)" -m "Physics fingerprint changed on purpose (plan Appendix B, L7 set)." -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

**Reviewer (Opus high) checks:** no lerp code left; the CharacterController is never disabled by locomotion; the fingerprint diff is exactly Appendix B's L7 set; walking down the ramp is untouched; the kick only fires on entry from swimming.

---

## L8 - The procedural rod stow / unstow animation

**Header**
- **Goal:** D7. Every `NetTool` change animates the rod on every peer, with no animation assets. The pure progress lives in Core so EditMode can test it (EditMode does not reference Presentation).
- **Files:** new `Assets/_Project/Core/Player/ToolStowAnim.cs`, `Assets/_Project/Tests/EditMode/ToolStowAnimTests.cs`; edit `Assets/_Project/Presentation/Rod/RodPresenter.cs`, `Assets/_Project/Tests/PlayMode/RodViewSoloTests.cs`.
- **Depends on:** L7. **Implementer:** Sonnet. **Reviewer:** Opus (high).

### L8.1 Tests first

New `Assets/_Project/Tests/EditMode/ToolStowAnimTests.cs`:
```csharp
// Authority: none (pure EditMode tests). FishGame.Core.ToolStowAnim: progress of the procedural rod stow / unstow (R70, contract M2-D31).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ToolStowAnimTests
    {
        // Catches: a retuned duration slipping in unnoticed.
        [Test]
        public void ToolStowAnim_Seconds_Is02()
        {
            Assert.AreEqual(0.2f, ToolStowAnim.Seconds);
        }

        // Catches: a spawn / late join that animates (Create must snap to the current tool).
        [Test]
        public void ToolStowAnim_Create_SnapsToTheCurrentTool()
        {
            ToolStowAnim shown = ToolStowAnim.Create(true);
            ToolStowAnim hidden = ToolStowAnim.Create(false);
            Assert.IsTrue(shown.Visible);
            Assert.AreEqual(0f, shown.Amount);
            Assert.IsFalse(hidden.Visible);
            Assert.AreEqual(1f, hidden.Amount);
        }

        // Catches: hiding at once (no stow animation) or never hiding.
        [Test]
        public void ToolStowAnim_Stow_StaysVisibleUntilTheEnd()
        {
            ToolStowAnim a = ToolStowAnim.Create(true);
            a.Tick(false, 0.1f);
            Assert.AreEqual(0.5f, a.Amount, 1e-6f);
            Assert.IsTrue(a.Visible, "half way: still visible");
            a.Tick(false, 0.1f);
            Assert.AreEqual(1f, a.Amount, 1e-6f);
            Assert.IsFalse(a.Visible, "fully stowed: hidden");
        }

        // Catches: an equip that waits for the end before showing the rod.
        [Test]
        public void ToolStowAnim_Equip_VisibleOnTheFirstTick()
        {
            ToolStowAnim a = ToolStowAnim.Create(false);
            a.Tick(true, 0.01f);
            Assert.IsTrue(a.Visible);
            Assert.AreEqual(0.95f, a.Amount, 1e-6f);
        }

        // Catches: a reversal that snaps to an end instead of continuing from where it is.
        [Test]
        public void ToolStowAnim_ReversalMidway_ContinuesFromTheCurrentAmount()
        {
            ToolStowAnim a = ToolStowAnim.Create(true);
            a.Tick(false, 0.1f);
            a.Tick(true, 0.05f);
            Assert.AreEqual(0.25f, a.Amount, 1e-6f);
        }

        // Catches: a non-smooth or non-monotonic curve, and endpoints off by a step.
        [Test]
        public void ToolStowAnim_Ease_SmoothstepEndpointsAndMidpoint()
        {
            Assert.AreEqual(0f, ToolStowAnim.Ease(0f));
            Assert.AreEqual(1f, ToolStowAnim.Ease(1f));
            Assert.AreEqual(0.5f, ToolStowAnim.Ease(0.5f), 1e-6f);
            Assert.AreEqual(0f, ToolStowAnim.Ease(float.NaN));
            float last = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float e = ToolStowAnim.Ease(i / 20f);
                Assert.Greater(e, last);
                last = e;
            }
        }

        // Catches: NaN or a negative dt corrupting the progress.
        [Test]
        public void ToolStowAnim_BadDt_IsIgnored()
        {
            ToolStowAnim a = ToolStowAnim.Create(true);
            a.Tick(false, float.NaN);
            a.Tick(false, -1f);
            a.Tick(false, float.PositiveInfinity);
            Assert.AreEqual(0f, a.Amount);
        }
    }
}
```

`RodViewSoloTests.cs` edits (F11):
1. `DriveView`: `yield return PlayModeHarness.WaitFrames(3, 0.1f);` -> `yield return PlayModeHarness.WaitFrames(3, ToolStowAnim.Seconds + 0.1f);   // the equip animation has settled`.
2. The stow check (around line 243): replace `yield return PlayModeHarness.WaitFrames(2, 0.05f);` with
   `yield return PlayModeHarness.WaitUntil(() => !Renderer(player, PlayerRigPaths.RodSegmentPrefix + "0").enabled, 1f, "the rod never hid after the stow animation");`
   and keep the per-segment asserts after it.
3. New test in the same class:
```csharp
        // Catches (R70 stow animation): the rod hiding at once on a stow or appearing straight at its rest pose on an equip (no animation),
        // the animation moving TipAnchor (BF-3: the gameplay tip, reach tests and host slack read it), and the rod not settling back exactly.
        [UnityTest]
        public IEnumerator RodViewSolo_StowAndEquip_AnimateThenSettle()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;
            try
            {
                yield return DriveView(rig);   // rod out, settled at rest
                Transform anchor = player.Find(PlayerRigPaths.RodTipAnchor);
                Transform visual = player.Find(PlayerRigPaths.RodTipVisual);
                MeshRenderer seg0 = Renderer(player, PlayerRigPaths.RodSegmentPrefix + "0");
                Vector3 restLocal = visual.localPosition;
                Vector3 anchorLocal = anchor.localPosition;
                Quaternion anchorRot = anchor.localRotation;

                rig.Player.OwnerSetTool(ToolKind.None);
                float t0 = Time.time, maxMove = 0f;
                bool visibleWhileMoving = false;
                while (Time.time - t0 < ToolStowAnim.Seconds * 0.9f)
                {
                    yield return null;
                    float move = Vector3.Distance(visual.localPosition, restLocal);
                    if (seg0.enabled && move > 0.01f) visibleWhileMoving = true;
                    maxMove = Mathf.Max(maxMove, move);
                }

                Assert.That(visibleWhileMoving, Is.True, "the rod is still visible while it swings away (max move " + maxMove + ")");
                yield return PlayModeHarness.WaitUntil(() => !seg0.enabled, 1f, "the rod never hid after the stow animation");
                Assert.That(Vector3.Distance(anchor.localPosition, anchorLocal), Is.LessThan(1e-6f), "TipAnchor never moves (BF-3)");
                Assert.That(Quaternion.Angle(anchor.localRotation, anchorRot), Is.LessThan(1e-4f), "TipAnchor never rotates (BF-3)");

                rig.Player.OwnerSetTool(ToolKind.Rod);
                t0 = Time.time;
                bool sawRising = false;
                while (Time.time - t0 < ToolStowAnim.Seconds * 0.9f)
                {
                    yield return null;
                    if (seg0.enabled && Vector3.Distance(visual.localPosition, restLocal) > 0.01f) sawRising = true;
                }

                Assert.That(sawRising, Is.True, "the equip shows the rod at once and swings it up into place");
                yield return PlayModeHarness.WaitFrames(3, ToolStowAnim.Seconds + 0.1f);
                Assert.That(seg0.enabled, Is.True);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.LessThan(1e-3f), "settled exactly at the rest pose");
                Assert.That(Vector3.Distance(anchor.localPosition, anchorLocal), Is.LessThan(1e-6f), "TipAnchor never moves (BF-3)");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("rod stow animation");
        }
```
(`DriveView` disables the controller; the `finally` re-enables it like the existing tests do. Add `using FishGame.Core;` if the file lacks it.)

### L8.2 Code

New **`Assets/_Project/Core/Player/ToolStowAnim.cs`**:
```csharp
// Core/Player/ToolStowAnim.cs: progress of the procedural rod stow / unstow (spec 7.3, R70; contract M2-D31). Pure, no assets.
// Amount 0 = out (rest pose), 1 = fully put away (hidden). Moves at 1 / Seconds per second toward the wanted end; Ease is a smoothstep.
// RodPresenter (every peer) owns one; Create snaps to the current tool, so a spawn or a late join never animates.
namespace FishGame.Core
{
    public struct ToolStowAnim
    {
        public const float Seconds = 0.2f;   // [tune]

        private float _amount;

        public static ToolStowAnim Create(bool shown)
        {
            return new ToolStowAnim { _amount = shown ? 0f : 1f };
        }

        public float Amount => _amount;
        public bool Visible => _amount < 1f;

        // Advances toward shown (0) or stowed (1); dt <= 0, NaN or infinite is ignored. Returns Ease(Amount).
        public float Tick(bool wantShown, float dt)
        {
            if (dt > 0f && !float.IsInfinity(dt))
            {
                float step = dt / Seconds;
                if (wantShown) _amount = _amount > step ? _amount - step : 0f;
                else _amount = _amount + step < 1f ? _amount + step : 1f;
            }

            return Ease(_amount);
        }

        public static float Ease(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
```

**`RodPresenter.cs`:**
1. Header comment: after "stowing the rod toggles the segment renderers only." add: ` Since R70 every NetTool change animates first (ToolStowAnim, 0.2 s): the chain swings tip-down and back about Seg0, then hides; equip reverses it. TipAnchor is still never touched.`
2. Constants: add
```csharp
        private const float StowPitchDeg = 70f;    // [tune] the tip swings down this far when fully stowed (about the rod's local X)
        private const float StowPullBack = 0.3f;   // [tune] metres the rod slides back along its own axis (-Z) when fully stowed
```
3. Fields: add `private ToolStowAnim _stow;` and `private bool _stowStarted;`.
4. `OnDisable`: add `_stowStarted = false;` and change the pose call to `ApplyPose(0f, Vector3.right, 0f, 0f, 0f, 0f);`.
5. `LateUpdate`: replace
```csharp
            bool wantShown = _rod.Player != null && _rod.Player.Tool == ToolKind.Rod;
            if (wantShown != _shown) SetSegmentsVisible(wantShown);
```
   with
```csharp
            bool wantShown = _rod.Player != null && _rod.Player.Tool == ToolKind.Rod;
            if (!_stowStarted)
            {
                _stow = ToolStowAnim.Create(wantShown);   // spawn / late join: no animation
                _stowStarted = true;
            }

            float stow = _stow.Tick(wantShown, Time.deltaTime);
            if (_stow.Visible != _shown) SetSegmentsVisible(_stow.Visible);
```
   and the final `ApplyPose(bendDeg, axis, extraX + shakeX, shakeY, 0f);` becomes `ApplyPose(bendDeg, axis, extraX + shakeX, shakeY, 0f, stow);`.
6. `ApplyPose`: new signature and body:
```csharp
        // The chain: link i is the rest pivot-to-pivot offset, segment i turns by the accumulated weighted angle a_i,
        // pos_{i+1} = pos_i + R_i * d_i, localRot_i = R_i * restRot_i; TipVisual (the end of the last link) gets the final position.
        // stow01 > 0 then moves the whole chain rigidly: rotate StowPitchDeg * stow01 about Rod-local X through Seg0's rest pivot and slide
        // StowPullBack * stow01 along -Z. At stow01 = 0 the pose is exactly the old one. Written every frame, so the rest pose is restored exactly.
        private void ApplyPose(float bendDeg, Vector3 axis, float extraXDeg, float extraYDeg, float extraZDeg, float stow01)
        {
            if (_tip == null) return;   // torn down with the player
            bool stowing = stow01 > 0f;
            Quaternion stowRot = stowing ? Quaternion.AngleAxis(StowPitchDeg * stow01, Vector3.right) : Quaternion.identity;
            Vector3 pivot = _restPosition[0];
            Vector3 shift = stowing ? Vector3.back * (StowPullBack * stow01) : Vector3.zero;

            Vector3 position = _restPosition[0];
            float accBend = 0f, accX = 0f, accY = 0f, accZ = 0f;
            Quaternion r = Quaternion.identity;
            for (int i = 0; i < SegmentCount; i++)
            {
                if (_segments[i] == null) return;
                accBend += bendDeg * Weights[i];
                accX += extraXDeg * Weights[i];
                accY += extraYDeg * Weights[i];
                accZ += extraZDeg * Weights[i];
                r = Quaternion.AngleAxis(accBend, axis) * Quaternion.Euler(accX, accY, accZ);

                _segments[i].localPosition = stowing ? pivot + stowRot * (position - pivot) + shift : position;
                _segments[i].localRotation = stowing ? stowRot * r * _restRotation[i] : r * _restRotation[i];
                position += r * (_restPosition[i + 1] - _restPosition[i]);
            }

            _tip.localPosition = stowing ? pivot + stowRot * (position - pivot) + shift : position;
            _tip.localRotation = stowing ? stowRot * r * _restRotation[SegmentCount] : r * _restRotation[SegmentCount];
        }
```
   (The `BendAxis` computation is untouched; with the rod hidden the line is never out, so a bend during a stow does not occur in practice.)

### L8.3 Verify
1. `compile`; `test-edit -Filter "ToolStowAnimTests"` -> pass.
2. `test-play -Filter "RodViewSoloTests|FishingSoloTests|FeedbackSoloTests|LadderClimbTests"` -> pass. `RodTip_*` reach tests must be unchanged (TipAnchor untouched).
3. Mutation: in `RodPresenter.LateUpdate` use `SetSegmentsVisible(wantShown)` (instant) -> `RodViewSolo_StowAndEquip_AnimateThenSettle` red. Restore.
4. Full `test-edit` and `test-play` (background) -> green. Restore the three scenes.
5. Manual (controller asks the user, record in L9's acceptance list): press 1 to equip and stow on the deck; swim; step onto a ladder. The rod swings down and away, then back up. Tune `StowPitchDeg`/`StowPullBack` if it clips the camera.

### L8.4 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Core/Player/ToolStowAnim.cs Assets/_Project/Core/Player/ToolStowAnim.cs.meta Assets/_Project/Tests/EditMode/ToolStowAnimTests.cs Assets/_Project/Tests/EditMode/ToolStowAnimTests.cs.meta Assets/_Project/Presentation/Rod/RodPresenter.cs Assets/_Project/Tests/PlayMode/RodViewSoloTests.cs
git -C C:\Dev\FishGame commit -m "feat(rod): procedural stow and unstow animation on every tool change (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## L9 - Spec, contract and acceptance text

**Header**
- **Goal:** the docs say what the code now does. No code. Fill the commit hashes of L1-L8 into the contract row.
- **Files:** `docs/superpowers/specs/2026-10-09-fishgame-design.md`, `docs/superpowers/plans/2026-10-11-m2-interfaces.md`, `docs/superpowers/plans/2026-10-09-m1-interfaces.md` (one cell), `docs/superpowers/plans/2026-10-11-m2-acceptance.md`.
- **Depends on:** L8. **Implementer:** Sonnet. **Reviewer:** Sonnet.
- If the user has answered any open question (0.2) by now, write the answer instead of the default and say so in the report.

### L9.1 Spec (`2026-10-09-fishgame-design.md`)
1. **7.3**, heading `**Water: swimming and exits**` -> `**Water: swimming, ladders and the ramp**`. Replace the bullet that starts `` `WaterExit` trigger volumes sit at every ladder `` (about line 673) with these three bullets:
```
- **Ladders (R70).** Each dock ladder has a walk-in `LadderVolume` (a trigger box; lake dock: x 1.55..2.55 on each side, 1.0 m along z around the ladder, y -1.4..1.6, layer Default). While the owner's capsule centre is inside one, the player is **Climbing**: no gravity; Move goes along the camera look (W toward where you look, so look up to climb and down to descend; S reverses; A/D strafe flat) at `climbSpeed` 2.4 m/s [tune]; with no input you hang (instant stop). Entering needs no button and works from walking, wading, swimming or stepping off the deck edge, but not while RodFighting. Leaving the box ends the climb and the climb velocity carries on into normal walking, falling or swimming; leaving through the top blocks re-entry for 0.25 s, so a climber who tops out is not caught again. Sprint, Jump, Interact and the rod inputs do nothing on a ladder. The rod is put away with a short stow animation on entry and comes back out after, as for swimming. The CharacterController stays on, so rails, posts and the deck still collide. Nothing about ladders is networked beyond the owner's NetworkTransform (`NetSwimming` is written false on entry; no `NetClimbing` in M2).
- **Shore ramp (R70).** A walkable slope: no lerp and no pull. Its foot (in the water) is a `WaterExit` box (lake dock: x 10.5..13.5, z -46..-39, layer Default): a player in water there always wades (feet on the bed, walk speed) instead of swimming, and a swimmer arriving there while pushing toward the ramp top gets one small upward kick (2.0 m/s [tune]).
- After 60 s in water the owner teleports to the dock spawn.
```
   Also in the bullet `Entering water stows the rod (remembered), ...` add after "stows the rod (remembered)": ` with the stow animation (a ladder does the same)`.
2. **7.8 #3** becomes: `3. A player gets out of the lake within 2 s of reaching a ladder while looking up toward the deck (about 45 deg), and walks out up the shore ramp with no pull (R70).`
3. **8.2** table: the `Swimming` row's E cell `climb at exits` -> the empty-cell dash used by the other rows. Add directly below the Swimming row (the `-` cells below stand for the em dash the table uses: copy that character from the neighbouring rows):
```
| `Climbing` (ladder, R70) | climb along the look (W toward it, S away; no jump, no sprint) | - | - | - | - | - | - | strafe / back |
```
4. Do not add an R row: R70 already covers this. Do not touch other sections.

### L9.2 M2 contract (`2026-10-11-m2-interfaces.md`)
1. **Section 13**, append four rows after M2-D28:
```
| M2-D29 | Ladders (R70, 2026-10-12) | `Gameplay/World/LadderVolume` (trigger box, layer Default, local-space `Contains`, `IsAtOrAboveTop` with `TopEpsilon` 0.05). Owner `PlayerLocomotion`: enter when the capsule centre is inside any volume, not locked (RodFighting or a source's `LocksMovement`), not in the `ClimbGate` grace; entry writes `NetSwimming` false. Climb: CharacterController stays enabled; `MovementMath.ClimbWish(yawDeg, pitchDeg = PlayerCamera.PitchDeg or 0, MoveX, MoveY)` at `ClimbSpeed` 2.4; no gravity; zero input = zero velocity. Exit: centre outside every volume; the applied climb velocity carries into `_vx/_vz/_vy`; an exit at or above the top face starts `ClimbReentryGrace` 0.25 s. `RouteClimbing` passes Move, Look, Pause; `RouteSwimming` zeroes Interact. Lake dock `Ladders/LadderL|R`: centre (+-2.05, 0.1, -41.5), size (1, 3, 1). | Spec R70 and the RV research. Velocity carry and the top-only grace come from the PlayMode climb-out tests; the Default layer keeps the boxes out of CastProbe's water mask. |
| M2-D30 | Shore-ramp foot assist (R70) | The 0.4 s exit lerp (`TryBeginClimb`, `MovementMath.ClimbDuration`) is deleted. `Exits/Ramp` (`WaterExit`; centre (12, -1.0, -42.5), size (3, 2.8, 7), layer Default; `Top` (12, 0.45, -50.5)) is the ramp foot. Core `RampAssist.Step`: in water inside the foot the player wades, not swims (D36's in-water gate); a swimmer entering it while `MovementMath.PushingToward` the Top (DA7's rule) gets `_vy = max(_vy, RampAssistKick 2.0)` once per entry. | R70 "a walkable slope with a light assist". The bed and ramp already form a continuous ~9 deg slope; the box reaches seaward to z -39 so the assist acts where swimmers find their feet. |
| M2-D31 | Rod on ladders; stow animation (R70) | `FishingRodController.HandleSwim`: away = `IsSwimming || IsClimbing` (`RodControllerRules.IsAway`, `AwayStep`); stow on entry (remembered; a line out is recalled; `RodForceReason.Stow` for a ladder, `Swim` for water), equip when both clear. `RodPresenter` animates every `NetTool` change on every peer: Core `ToolStowAnim` (0.2 s, smoothstep) swings the Seg* chain and TipVisual 70 deg tip-down and 0.3 m back about Seg0, then hides; equip reverses it. TipAnchor is never moved (BF-3). | R70's stow/unstow animation without assets; one code path for the equip key, water and ladders. |
| M2-D32 | No `NetClimbing` in M2 | Not added. Remotes see a climb through the NetworkTransform; no remote climb pose; no host rule reads climbing; `BuildInfo.ProtocolVersion` stays 2. Revisit in M5 with body states (a pickup rule may want "not climbing"). | A NetworkVariable for a cosmetic pose would bump the protocol with no M2 consumer. |
```
2. **Section 14**, DA7 row status cell `Adopted.` -> `Retired for ladders by R70 (M2-D29: walk-in, no press, no facing rule). Survives only as the ramp-foot kick's "pushing toward the Top" test (MovementMath.PushingToward, M2-D30).`
3. **Section 7.16** PlayerLocomotion block (around lines 1144-1146): `LocksMovement (any source or the context) -> wish 0, no jump,` / `no climb start; gravity and yaw continue.` -> `... -> wish 0, no jump, no ladder entry (R70, M2-D29); gravity and yaw continue; a climber who becomes locked hangs.`
4. **Section 6.4** table, add this row:
```
| `Ladders` | `LadderL`, `LadderR`: trigger BoxCollider + `LadderVolume`, layer Default (R70, M2-D29); `Exits` now holds only `Ramp` (the ramp foot, layer Default, M2-D30) | ladders plan L5, L7 |
```
5. **Section 10.5** table, add this row:
```
| Ladders plan (R70) | `ClimbWishTests.cs`, `ClimbGateTests.cs`, `LadderVolumeTests.cs`, `RodAwayStepTests.cs`, `RampAssistTests.cs`, `ToolStowAnimTests.cs` (EditMode); `LadderClimbTests.cs` (PlayMode, 11 tests); `SwimInteractGateTests.cs` rewritten (3 tests); `InputContextRouterTests` Climbing and Swimming rows; `RodViewSolo_StowAndEquip_AnimateThenSettle` | climb maths, re-entry grace, volume containment, rod away rule, ramp foot, stow animation; geometry, climb out at 30/45/60 deg, descend and hang, deck edge, top-out grace, rod and line on a ladder, ramp walk-out and walk-down |
```
6. **Section 16.1**, append a row:
```
| R70 ladders (plan `2026-10-12-m2-rework-ladders.md` L1-L8; commits <L1>..<L8>) | M2-D29..D32 above; DA7 retired for ladders; M1 D36 now gates only the ramp foot. Physics fingerprint changed on purpose (T28.1 superseded for these objects): `Exits/LadderL`, `Exits/LadderR` and their `Top` children removed; `Ladders` root added with `LadderL` / `LadderR` (BoxCollider trigger, layer 0, centre (+-2.05, 0.1, -41.5), size (1, 3, 1), `LadderVolume`); `Exits` root, `Exits/Ramp` and `Exits/Ramp/Top` layer 4 -> 0; `Exits/Ramp` centre (12, -0.9, -44) -> (12, -1.0, -42.5) and size (3, 2.4, 4) -> (3, 2.8, 7); every other collider, spawn and anchor identical. Measured climb-out times: <from L5>. | ladders plan; `PlayerLocomotion`, `MovementMath`, `ClimbGate`, `RampAssist`, `ToolStowAnim`, `LadderVolume`, `WaterExit`, `InputContextRouter`, `RodControllerRules`, `FishingRodController`, `RodPresenter`, `LakeDockBuilder`, `LakeDockLayout`, `Spot_LakeDock.unity` |
```
### L9.3 M1 contract (`2026-10-09-m1-interfaces.md`)
1. D36 row, reason cell: append ` Superseded in part by R70 (2026-10-12): ladders are walk-in LadderVolumes; this in-water gate now drives only the shore-ramp foot assist (M2 contract M2-D30).`
2. Section 6.5, the `Exits` row (about line 384): append to its description cell ` Superseded by R70 (2026-10-12; M2 contract M2-D29, M2-D30, 6.4): the ladder exits are now Ladders/LadderL|R LadderVolumes, and Exits/Ramp is the ramp foot.`
3. The other M1 contract hits (the `MovementMath` listing with `ClimbDuration` ~line 522, the `IsInWater` and `RouteSwimming` comments ~547/882, the locomotion pseudo-code ~1426-1427, the `WaterExit` block ~1479-1486, D25) are the M1 record and stay as written: the M2 contract rows above supersede them.

### L9.3b Stale-reference sweep (before committing)
Run `git -C C:\Dev\FishGame grep -n -e "WaterExit" -e "climb at exits" -e "ClimbDuration" -e "TryBeginClimb" -e "topPoint" -- docs CLAUDE.md`. Every hit must be one of: fixed by L9.1-L9.4; the historical M1 contract lines listed in L9.3 item 3; the spec's M1 roadmap line (17.1 item 9 area, ~line 2122) and roster line (~387), which describe what M1 shipped; old plans and handoffs under `docs/superpowers/plans/2026-10-09-*`, `2026-10-11-m2-plan-*` and `docs/superpowers/handoff/` (historical); this plan. List any other hit in the report and fix it if it describes current behaviour.

### L9.4 Acceptance (`2026-10-11-m2-acceptance.md`)
1. Line ~554, the item "Swim / wade at the ladder (`LadderR`): E climbs out, the bell is not rung." becomes:
```
  - [ ] Ladders (R70): swim to `LadderR`, look up at the deck and hold W: you climb out onto the deck with no button, in about 1.5 s; E never rings the bell from the water or a ladder.
```
2. Line ~563 (the "Swimming interact gate" item): replace its Expected text with: `Expected: **E** does nothing in the water or on a ladder (no bell, no creel sale); walking or swimming into a ladder volume starts the climb with no press (R70, ladders plan L5).`
3. Section J, "Manual checks carried from reviews": add
```
- [ ] **Ladder and ramp feel (R70, ladders plan):** (a) climb out of the water at a shallow (30 deg) and a steep (60 deg) look-up; (b) top out looking almost straight up and note the bob; (c) walk to the deck edge in front of a ladder: does it grab you too early? (open question Q7, knob `LadderVolumeInnerX`); (d) descend from the deck looking down, let go mid-ladder (you hang), leave sideways into the water; (e) swim to the ramp and walk out: the small kick at the foot; walk back down: no push; (f) the rod stow / unstow animation on 1, in water and on a ladder, rod out and line out; (g) MPPM: a second window sees a climber move smoothly. Result: ______
```

### L9.5 Verify and commit
1. `git -C C:\Dev\FishGame diff --stat` touches only the four docs.
2. Non-ASCII check on added lines: `git -C C:\Dev\FishGame diff -U0 | Select-String '^\+' | Select-String '[^\x00-\x7F]'` shows only the em-dash cells (U+2014) of the new 8.2 table row, copied from the neighbouring rows.
3. Commit:
```powershell
git -C C:\Dev\FishGame add docs/superpowers/specs/2026-10-09-fishgame-design.md docs/superpowers/plans/2026-10-11-m2-interfaces.md docs/superpowers/plans/2026-10-09-m1-interfaces.md docs/superpowers/plans/2026-10-11-m2-acceptance.md
git -C C:\Dev\FishGame commit -m "docs(ladders): spec 7.3/7.8/8.2, contract M2-D29..D32, DA7, D36, 6.4, 10.5 and 16.1, acceptance lines (R70)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
4. Controller: after L9, run the full `check` once in the background (`-TimeoutMinutes 40`), then put the L9.4 J item and the open questions on the user's "Waiting on you" board.

---

## Appendix A - Temporary fingerprint test (NEVER COMMIT)

`Assets/_Project/Tests/EditMode/TempLakeDockFingerprint.cs`. Create it at the start of L5 and of L7. Run it with
`$env:FG_FP_LABEL = '<label>'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "TempLakeDockFingerprint"`.
Delete it and its `.meta` before the commit. The reviewer checks it is absent.
```csharp
// TEMPORARY - NEVER COMMIT (ladders plan Appendix A; ruling G-Q9). Writes the Spot_LakeDock collider fingerprint to
// $FG_OUT/ladders/fingerprint-$FG_FP_LABEL.txt. Serialized collider fields only (Collider.bounds can be stale right after OpenScene).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FishGame.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.Tests.EditMode
{
    public class TempLakeDockFingerprint
    {
        private static readonly string[] TransformRoots = { "PlayerSpawner", "Exits", "Ladders", "BellAnchor" };

        [Test]
        public void TempLakeDockFingerprint_Write()
        {
            string outDir = Environment.GetEnvironmentVariable("FG_OUT");
            string label = Environment.GetEnvironmentVariable("FG_FP_LABEL");
            Assert.That(string.IsNullOrEmpty(outDir), Is.False, "set FG_OUT");
            Assert.That(string.IsNullOrEmpty(label), Is.False, "set FG_FP_LABEL");

            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Spot_LakeDock.unity", OpenSceneMode.Single);
            var lines = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    lines.Add(Describe(c));
                }

                if (Array.IndexOf(TransformRoots, root.name) >= 0)
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        lines.Add("T " + PathOf(t) + " pos=" + V(t.position) + " layer=" + t.gameObject.layer
                                  + (t.GetComponent<WaterExit>() != null ? " WaterExit" : "")
                                  + (t.GetComponent<LadderVolume>() != null ? " LadderVolume" : ""));
                    }
                }
            }

            lines.Sort(StringComparer.Ordinal);
            string dir = Path.Combine(outDir, "ladders");
            Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir, "fingerprint-" + label + ".txt"), lines);
            Assert.That(lines.Count, Is.GreaterThan(100), "the fingerprint looks empty");
        }

        private static string Describe(Collider c)
        {
            var s = new StringBuilder("C ").Append(PathOf(c.transform)).Append(' ').Append(c.GetType().Name)
                .Append(" layer=").Append(c.gameObject.layer).Append(" trigger=").Append(c.isTrigger).Append(" enabled=").Append(c.enabled)
                .Append(" m=");
            Matrix4x4 m = c.transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++)
            {
                s.Append(F(m[i])).Append(i < 15 ? "," : "");
            }

            switch (c)
            {
                case BoxCollider b: s.Append(" size=").Append(V(b.size)).Append(" center=").Append(V(b.center)); break;
                case SphereCollider sp: s.Append(" r=").Append(F(sp.radius)).Append(" center=").Append(V(sp.center)); break;
                case CapsuleCollider cap: s.Append(" r=").Append(F(cap.radius)).Append(" h=").Append(F(cap.height)).Append(" dir=").Append(cap.direction).Append(" center=").Append(V(cap.center)); break;
                case MeshCollider mc: s.Append(" mesh=").Append(mc.sharedMesh != null ? mc.sharedMesh.name + "/" + mc.sharedMesh.vertexCount : "null"); break;
            }

            return s.ToString();
        }

        private static string PathOf(Transform t)
        {
            return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        }

        private static string V(Vector3 v)
        {
            return F(v.x) + "," + F(v.y) + "," + F(v.z);
        }

        private static string F(float f)
        {
            return f.ToString("F4", CultureInfo.InvariantCulture);
        }
    }
}
```

## Appendix B - Expected fingerprint differences (anything else is a defect)

**L5 set** (`l5-before` vs `l5-after`):
- Removed: `C Exits/LadderL BoxCollider ...`, `C Exits/LadderR BoxCollider ...`, `T Exits/LadderL ...`, `T Exits/LadderL/Top ...`, `T Exits/LadderR ...`, `T Exits/LadderR/Top ...`.
- Added: `C Ladders/LadderL BoxCollider layer=0 trigger=True enabled=True m=<translation -2.05, 0.1, -41.5> size=1.0000,3.0000,1.0000 center=0.0000,0.0000,0.0000`; the same for `Ladders/LadderR` at x +2.05; `T Ladders pos=0.0000,0.0000,0.0000 layer=0`; `T Ladders/LadderL pos=-2.0500,0.1000,-41.5000 layer=0 LadderVolume`; `T Ladders/LadderR pos=2.0500,0.1000,-41.5000 layer=0 LadderVolume`.
- Unchanged: everything else, including `C Exits/Ramp`, `T Exits`, `T Exits/Ramp`, `T Exits/Ramp/Top`, `Dock/Ladder_L`, `Dock/Ladder_R`, `Dock/RampDeck`, `WaterVolume`, spawns and `BellAnchor`.

**L7 set** (`l7-before` vs `l7-after`):
- `C Exits/Ramp BoxCollider`: layer 4 -> 0; matrix translation (12, -0.9, -44) -> (12, -1.0, -42.5); size (3, 2.4, 4) -> (3, 2.8, 7).
- `T Exits`: layer 4 -> 0. `T Exits/Ramp`: pos -> (12, -1.0, -42.5), layer 4 -> 0. `T Exits/Ramp/Top`: layer 4 -> 0, pos unchanged (12, 0.45, -50.5).
- Unchanged: everything else, including both `Ladders/*` lines.

## Appendix C - Task order and why it stays green

| Task | Lands | Why the build and tests stay green |
|---|---|---|
| L1 | Core maths + tests | additive |
| L2 | `LadderVolume` (no scene), `RouteClimbing` passes Move | the M1 lerp ignores the intent while Climbing |
| L3 | rod away = swimming or climbing | with the M1 lerp a swimmer's `IsSwimming` stays true through the lerp; must land before ladders exist (otherwise the rod pops out mid-ladder) |
| L4 | volume climb in `PlayerLocomotion` | no scene has a `LadderVolume` yet |
| L5 | scene volumes + ladder exits removed + Swimming zeroes Interact + gate/climb tests | atomic: either half alone breaks `SwimInteractGateTests` |
| L6 | rod PlayMode proof | tests only |
| L7 | ramp foot, lerp deleted | the ramp lerp has no test; the new ramp tests arrive in the same commit |
| L8 | stow animation | `RodViewSoloTests` waits lengthened in the same commit |
| L9 | docs | no code |
