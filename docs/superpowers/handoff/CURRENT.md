# FishGame - current state (running handoff)

**Start any new conversation with:** `Read C:\Dev\FishGame\docs\superpowers\handoff\CURRENT.md and continue.`

This file lives on `main` only. The controller (Claude) rewrites it after every meaningful step (a task landed, a review, a user decision, a merge) and commits and pushes it straight to `main`. Feature branches never edit it.

_Last updated: 2026-10-14. ALL M3 tasks merged (m3-fish-ecology af95354; check PASS: EditMode 1378 incl. 1 skip, PlayMode 137; two-build ContentHash 54AA263BB84936CE identical). Acceptance doc: docs/superpowers/plans/2026-10-13-m3-acceptance.md. In flight: Opus xhigh branch review. Then merge M3 into main (no PR; merge commit). Board now has the M3 sign-off rows ME1-ME8, DC1-DC5, DN1-DN5, JE8-JE9 plus the re-checks._

## 1. Read these next (in order)
1. `C:\Dev\FishGame-m3\CLAUDE.md`: project rules (Git: no PRs; merge yourself when reviewed and green; PlayMode guard).
2. `C:\Dev\FishGame-m3\docs\superpowers\handoff\2026-10-13-m2-rework-done-handoff.md`, section 1b (detailed state).
3. `C:\Dev\FishGame-m3\docs\superpowers\plans\2026-10-13-m3-controller-notes.md`: review findings, rulings, and items queued for T15/T17/T20-T25.
4. `C:\Dev\FishGame-m3\docs\superpowers\handoff\2026-10-13-user-checks.md`: every hands-on check the user owes.

## 2. Where things are
- **main:** M2 complete (merged 235d460). That covers the fishing core, the Rust-faithful fight (R72), RV ladders (R70), smooth water (R73), the realistic-toon look (R74), seasonal trees, and the story R76 (mafia chase downriver, starfish god, 7 days = 7 river maps; planned for M4).
- **M3 (fish ecology):** worktree `C:\Dev\FishGame-m3`, branch `m3-fish-ecology` (pushed).
  - Done and reviewed: T0-T16, T18-T20, T23 (all 25 fish in game), plus the follow-ups. Tests green at 79b8f30: EditMode 1280 + 1 skip, PlayMode 103.
  - Also done: T17 (harbour; Blender art + code builder), fix batches A/B/C from the board.
  - Also done: T21 (journal UI + FishIconBaker atlas, M3-D96), T24 (EcologySoloTests).
  - Also done: T22 (dev panel + HUD clock).
  - Also done: T25 (acceptance doc + README).
  - Remaining: the Opus xhigh branch review (in flight), its fixes, then merge into main.
- **Idle worktree** `C:\Dev\FishGame-lookdev` (merged): delete only with the user's OK.

## 3. Next action
1. Point the user to the **M3 Checks board** https://claude.ai/artifact/LaUKFoiQZ3SYC24BCG1V1P (new 2026-10-14; same 50 checks and collections as the old Test Notes board, which had no results). Read their results with ArtifactData:
   - `results/{checkId}`: PASS/FAIL/SKIP plus a note;
   - `notes/general`.

   Summarise them, turn each FAIL or note into a task or a question, and reply under each check via its `reply` field.
2. If **W1** (two-window WorldState sync) passed, start **T15**. If it failed, do the M3-D18 prefab fallback first. If it isn't done yet, do what doesn't need it and remind the user.
   - **W1 PASSED (2026-10-13).** T15 is in flight in worktree `C:\Dev\FishGame-t15`, branch `m3-t15` (Sonnet; Opus high review next). T15 changes no scene and no file T20 owns.
   - **In flight (2026-10-14):** T20 (save + journal recorder) runs ahead of T15, in worktree `C:\Dev\FishGame-t20`, branch `m3-t20` (from m3-fish-ecology 070609d, copied Library), so the user can test W1 in `C:\Dev\FishGame-m3`. After Opus high review, merge `m3-t20` into `m3-fish-ecology`. T20 changes `Spot_LakeDock.unity`: whichever of T20/T15 lands second regenerates the scene from the builders, never a hand-merge of the .unity text.
3. As tasks land, add their new checks to the board (for example M6 after T17/T21/T22).

## 3b. Fix round pulled forward (user, 2026-10-14: "fix what you can according to the checks")
Board results so far: 23 checks done. FAIL: none. SKIP: F5 (no audio), S15 (no gamepad). Notes turned into fixes:
- **Batch B (in flight):** worktree `C:\Dev\FishGame-fixb`, branch `m3-fix-look`. F6 painted red/white bobber plus rod idle life; F9 a clearer bobber dive. Sonnet implements, Sonnet reviews.
- **Batch A (after T15 merges; T15 owns FishingRodController):** F1 reel stalls for ~1 s after the hook-set (check `HookBeat`; bug or design); F7 inactivity escape 30 s with no input (a new idle timer; `FightTimeout` 120 stays the overall cap; spec delta); S10 shorten the wait between a catch and the next cast; F10 shorter fights at the same difficulty (lower stamina/distance, same snap window; Rhythm-bot medians before/after; re-run EcologyBalanceReports V4/V5, cliff on minnow/snapper). The host `LandedFloorSeconds` is distance/reel speed, so it's safe unless reel speed changes; still test that an honest short catch is never rejected. Opus review (host validation).
- **Batch C (after T20 merges; T20 owns UiStrings):** S4 arcade ladder climb with momentum (R70 delta; tests pin ~1.25 s); S6 Graphics settings tab with quality presets (S3 minimum-spec target GTX 900 class, 60 fps at 1080p, can't be measured here). Sonnet review.
- **T17 Harbour Pier in Blender (user, 2026-10-14):** the user asked for the map's art to be modelled in Blender. Ruling T17-B is in the M3 controller notes (267f1df).
  - Phase 1 (in flight): Opus artist agent on worktree `C:\Dev\FishGame-harbour-art`, branch `m3-harbour-art`. It writes `Art/Blender/fg_harbour.py`, `fishgame_harbour.blend`, `Art/Export/Harbour/*.fbx` plus a manifest and preview images.
  - Phase 2 (after T15 merges): T17 builder in Unity. Terrain height fields, water, colliders, hooks, stand points and bake stay code. The builder places the FBX visuals. New FBX bounds test against HarbourPierLayout.
  - User look check: Unity screenshots after phase 2.
  - The user allows free addons/assets once verified safe (record source + licence). Characters will be done in Blender later, as the design says (RV There Yet style, REPO side-eye eyes, PEAK); not now.
- Design session: the ice chest (S10), the modern menu redesign (S6). Both are in board `notes/ideas`.
- After each batch: update the board check texts that quote changed numbers (F2, F4, F7, F10, S4, S11), add re-check rows, and post an F10 before/after duration table.

## 4. After M3
Merge M3 into main. Then the fix round from the user's board notes (including the friendlier fight tuning; re-run EcologyBalanceReports after any fight retune). Logged so far from the board:
- S3 performance: the frame is GPU-bound (CPU 0.2 ms of ~5 ms, 210 fps on the user's PC). Minimum-spec target: a GTX 900-series card (960/970 class) at a steady 60 fps, 1080p. Keep the load on the GPU, with quality presets.
- S4 ladders: faster, arcade-style climb with momentum and a small launch off the top; less realistic.
- S6 menus: add a Graphics tab (preset, resolution, window mode, vsync, frame cap, shadow/fog quality, render scale) in the fix round. A full modern-menu redesign goes to the design session (pick reference games first); some screens are hard to read.

Then a **design session with the user** (superpowers:brainstorming, about an hour). Input: `docs/superpowers/design/2026-10-13-fun-formula.md`, the user's reasons for loving Gamble With Your Friends, RV There Yet, REPO and PEAK, plus the board's "Ideas for the game" box (`notes/ideas`). Decide the core ingredients, items, gambling, the mafia, map size, failure, and the first vertical-slice day. Target: about 70 fish, a third of them with gameplay quirks.

Then write the M4 handoff: the R76 river run, starfish god camp, boat ride, mafia escalation, escape reward, cutscenes, and quota growth calibration (earnings grow only ~1.2-1.4x from day 1 to day 7; the spec assumes ~2.35x).

## 5. Boards
- Progress: https://claude.ai/artifact/SSE5uoZ7ELPyZP5A11qdXR. Keep `status/now`, `tasks`, `todos` and `agents` current.
- M3 Checks (the user writes here): https://claude.ai/artifact/LaUKFoiQZ3SYC24BCG1V1P. The old Test Notes board NAcGRrYJYuPs2Rw6SBa93C is retired (no results on it). Collections: `checks`, `results/{id}`, `notes/general`, and `notes/ideas` (game ideas; each has a `text` and a Claude `reply`).

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

## 10. Harbour detail pass (user, 2026-10-14) - in flight
- User: the harbour is "decent but very simple". Wants all of: shaped land, harbour life + clutter, shoreline + vegetation, backdrop/skyline; cozy-busy; "don't be afraid to add triangles" (user has an RTX 5070). Perf target revised: GTX 900-series at LOW-MEDIUM settings.
- Story (user): each map must feel like a stop on the downriver run ("they drove the boat to that area"): the crew's getaway boat moored as a hero prop (FBX Harbour_Core_CrewBoat, reused for the M4 boat ride), a river mouth in the backdrop, light mafia hints (wanted poster, dark boat on the horizon), the world continuing past the walls. Life from noise/clutter/details (REPO, PEAK) and realism + scale (RV There Yet).
- Blender artist (Opus) on worktree C:\Dev\FishGame-harbour-art, branch harbour-art2 (from af95354). Output: Harbour_Core_*, Harbour_Detail_*, *_Col FBX + manifest (animHint per object for Unity sway/bob/smoke/gull wiring). Screenshots 15+ go to the build log.
- Then a Unity task (post-M3, fix round): place the new meshes, colliders, a Low-preset Detail toggle, simple animation + ambient sound emitters from animHint; harbour grid must stay byte-identical unless re-baked on purpose.
- Carry the same map-feel rules into the M4 river maps and the design session.

## 11. M3 branch review (Opus xhigh, 2026-10-14): MERGE WITH FIXES
- Fix wave in flight: worktree C:\Dev\FishGame-t15, branch m3-final-fixes (acceptance-doc fixes + dev panel "Day over" button; host ignores rod/bait selection in release builds; __Temp test flake; FishIconBaker GPU guard; dev panel closes on a bite; contract M3-D97 for T17-B). Then full check, merge into m3-fish-ecology, merge M3 into main (merge commit, no PR).
- Deferred to M4 / post-merge round (from the review):
  - (Dropped 2026-10-14, user: no anti-cheat; mods/WeMod welcome) cancel-reroll and cast-origin-slack exploits; the rod/bait release-build guard was removed from the fix wave.
  - In-scene WorldState GlobalObjectIdHash changes on every Build All and is not in ContentHash: put each Spot's World hash in the approval payload (or pin file ids).
  - Newer-schema meta.json that fails to parse is treated as corrupt: read schemaVersion first.
  - Journal saves do a synchronous Flush per change on every peer: debounce / background.
  - Loadouts and pity streak reset on a map switch; quota calibration (earnings 1.14-1.38x over 7 days vs spec ~2.35x); journal level-1 sub-areas; Esc HandleBack test.

## 9. Live pages this round
- Harbour build log (screenshots as the map is built): https://claude.ai/artifact/TmqCpXrHJBXRZBhZMKkMhW (db `shots`, `meta/status`; images uploaded as assets).
