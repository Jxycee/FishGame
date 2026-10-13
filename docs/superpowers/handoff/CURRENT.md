# FishGame - current state (running handoff)

**Start any new conversation with:** `Read C:\Dev\FishGame\docs\superpowers\handoff\CURRENT.md and continue.`

This file lives on `main` only. The controller (Claude) rewrites it after every meaningful step (a task landed, a review, a user decision, a merge) and commits and pushes it straight to `main`. Feature branches never edit it.

_Last updated: 2026-10-13, late. Fun-formula doc added, review tiers scaled to risk, Ideas box on the Test Notes board._

## 1. Read these next (in order)
1. `C:\Dev\FishGame-m3\CLAUDE.md`: project rules (Git: no PRs; merge yourself when reviewed and green; PlayMode guard).
2. `C:\Dev\FishGame-m3\docs\superpowers\handoff\2026-10-13-m2-rework-done-handoff.md`, section 1b (detailed state).
3. `C:\Dev\FishGame-m3\docs\superpowers\plans\2026-10-13-m3-controller-notes.md`: review findings, rulings, and items queued for T15/T17/T20-T25.
4. `C:\Dev\FishGame-m3\docs\superpowers\handoff\2026-10-13-user-checks.md`: every hands-on check the user owes.

## 2. Where things are
- **main:** M2 complete (merged 235d460). That covers the fishing core, the Rust-faithful fight (R72), RV ladders (R70), smooth water (R73), the realistic-toon look (R74), seasonal trees, and the story R76 (mafia chase downriver, starfish god, 7 days = 7 river maps; planned for M4).
- **M3 (fish ecology):** worktree `C:\Dev\FishGame-m3`, branch `m3-fish-ecology` (pushed).
  - Done and reviewed: T0-T14, T16, T18, T19, T23 (all 25 fish in game), plus the follow-ups. Tests green: EditMode 1261 + 1 skip, PlayMode 97.
  - Remaining: T15, T17 (Harbour Pier), T20-T22 (journal, UI, dev panel), T24, T25, then an Opus xhigh branch review, then merge into main.
- **Idle worktree** `C:\Dev\FishGame-lookdev` (merged): delete only with the user's OK.

## 3. Next action
1. Point the user to the **Test Notes board** https://claude.ai/artifact/NAcGRrYJYuPs2Rw6SBa93C. Read their results with ArtifactData:
   - `results/{checkId}`: PASS/FAIL/SKIP plus a note;
   - `notes/general`.

   Summarise them, turn each FAIL or note into a task or a question, and reply under each check via its `reply` field.
2. If **W1** (two-window WorldState sync) passed, start **T15**. If it failed, do the M3-D18 prefab fallback first. If it isn't done yet, do what doesn't need it and remind the user.
3. As tasks land, add their new checks to the board (for example M6 after T17/T21/T22).

## 4. After M3
Merge M3 into main. Then the fix round from the user's board notes (including the friendlier fight tuning; re-run EcologyBalanceReports after any fight retune).

Then a **design session with the user** (superpowers:brainstorming, about an hour). Input: `docs/superpowers/design/2026-10-13-fun-formula.md`, the user's reasons for loving Gamble With Your Friends, RV There Yet, REPO and PEAK, plus the board's "Ideas for the game" box (`notes/ideas`). Decide the core ingredients, items, gambling, the mafia, map size, failure, and the first vertical-slice day. Target: about 70 fish, a third of them with gameplay quirks.

Then write the M4 handoff: the R76 river run, starfish god camp, boat ride, mafia escalation, escape reward, cutscenes, and quota growth calibration (earnings grow only ~1.2-1.4x from day 1 to day 7; the spec assumes ~2.35x).

## 5. Boards
- Progress: https://claude.ai/artifact/SSE5uoZ7ELPyZP5A11qdXR. Keep `status/now`, `tasks`, `todos` and `agents` current.
- Test notes (the user writes here): https://claude.ai/artifact/NAcGRrYJYuPs2Rw6SBa93C. Collections: `checks`, `results/{id}`, `notes/general`, and `notes/ideas` (game ideas; each has a `text` and a Claude `reply`).

## 6. Working rules (short)
- **Roles:** the controller delegates. Sonnet implements; Opus high reviews integration work; Sonnet reviews Core/data/docs; Opus xhigh does the branch review.
- **Every brief says:** Write/Edit only (no sed or python edits), tests first with a real red run, explicit-path commits, push after each task, and the PlayMode guard. Run Unity in a worktree with its own -projectPath.
- **Save often:** the user's usage can run out mid-session. Update this file at each step.
- **Token hygiene:** start a fresh conversation at milestones or after a long break, using the line at the top of this file.

## 7. Open questions for the user
Also on the board:
- fish keep/adjust;
- the Silver Mullet icon belly colour;
- the fight feel verdict (K.3);
- the bad-luck nudge keep/tune/remove;
- deck-edge grab;
- the default test-session look;
- winter tree colliders;
- the online-cancel message.
