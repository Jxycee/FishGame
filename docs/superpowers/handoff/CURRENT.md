# FishGame - current state (running handoff)

**Start any new conversation with:** `Read C:\Dev\FishGame\docs\superpowers\handoff\CURRENT.md and continue.`

This file lives on `main` only. The controller (Claude) rewrites it after every meaningful step (a task landed, a review, a user decision, a merge) and commits and pushes it straight to `main`. Feature branches never edit it.

_Last updated: 2026-10-14. **M3 MERGED INTO MAIN** (7af8041; check on main PASS: EditMode 1378 incl. 1 skip, PlayMode 139). Harbour detail pass + 4 polish rounds merged into main (36f8abb). Reference asset study done (research/2026-10-14-reference-style-notes.md: grey masks + colour parameters, top/bottom tint, crown cards, LODs). In flight: research notes (mafia counter-play; voice-mimic seagulls). Next: user's board checks, harbour pass into Unity, then the M4 design session (inputs: docs/superpowers/design/2026-10-14-m4-design-inputs.md)._

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

## 6b. AI tooling (installed 2026-10-14; a new session is needed to load it)
- **Unity MCP** (CoplayDev MCP for Unity, MIT): the package `com.coplaydev.unity-mcp` is pinned to `#v10.3.0` in `Packages/manifest.json`, and the server `unity` is in `.mcp.json` (`uvx mcpforunityserver==10.3.0`, stdio, `DISABLE_TELEMETRY=true`). It only works while the editor is open. An open editor holds `Temp/UnityLockfile`, so batchmode compile and test runs need the editor closed. Use the MCP for scene, GameObject and console work in the live editor, and batchmode for `check`. On the first editor open, run `Window > MCP for Unity`, check that telemetry shows as disabled, and do not let it rewrite the client config.
- **Blender:** the server is `blender` (`uvx blender-mcp`, ahujasid). User-scope skills are in `~/.claude/skills`, taken from RobLe3/cc-blender-skill (MIT): `text-to-blender` (the router), `blender-modeling`, `-materials`, `-uv-texturing`, `-lighting`, `-rendering`, `-cameras`, `-animation`, `-export`, `-pro-workflow` and `reference-to-3d`. The other 19 skills in that pack were skipped (niche reconstruction loops) to save listing tokens.
- **game-developer** skill (Jeffallan/claude-skills v0.4.18, MIT, user scope): generic Unity patterns. Its "60+ FPS on all platforms" rule is overridden by the spec's quality-first performance target.
- **Asset Store (free; Standard Unity Asset Store EULA, Extension Asset)**: these were added to the user's account on 2026-10-14 to target the critics' weakest category, surfaces (5.5). They are not imported yet. Import them through Package Manager > My Assets, under `Assets/ThirdParty/<pack>/`, and use them as painting and blending references or bases behind the look-dev, not as drop-in replacements.
  - 25+ Free Stylized Textures - Grass, Ground, Floors, Walls & More, by Game Buffs (241895)
  - FREE Stylized Textures - RPG Environment, by Blink (204187)
  - Free Stylized Textures, by LowlyPoly (204742)
  - Round 2, the same day (targets the critics' asks on crown-card trees, forest LODs, coast rock and splash VFX):
    - Cartoon FX Remaster Free, by Jean Moreno (109565, 460 reviews, URP): splashes and hit pops.
    - Fantasy Worlds: Forest FREE (282610, URP): stylised trees, possibly with LODs; check them.
    - Free Pack - Rocks Stylized (316250, URP): coast and breakwater rocks.
    - Free Stylized Nature Environment (96371, 39 reviews, built-in RP, so its materials need converting to URP).
    - Skipped Free Toon Water (387614): the page didn't clearly show it as free.
  - Round 3: 5 free character packs for reference (Synty Sidekick 336970, ithappy Creative Characters 304841, OlyPoly Party Game Characters 342650, Supercyan 79870, Kevin Iglesias Human Basic Motions 154271).
- **Planned: in-house toon water (user, 2026-10-14). Build this instead of buying Stylized Water 3.** That asset is $50, and the extensions we'd need cost extra: Dynamic Effects $20 (ripples/wakes) and Underwater $25. Asset page for comparison: https://assetstore.unity.com/packages/package/287769 (Staggart Creations, v3.3.2, Unity 6000.0.68, URP, 315 files). It's a feature reference only; don't copy its code. Scope for a short design session before any code:
  1. URP Shader Graph water: depth-based shallow/deep colour, intersection foam (from scene depth), scrolling normals, fresnel, cheap fake reflection or sky probe, and a projected caustics texture.
  2. Rivers: a flowmap or spline-UV flow direction, so the 7 river maps can share one material with a speed per section.
  3. Waves: Gerstner waves on the GPU with the same function in pure C# (Core assembly, EditMode-tested), so buoyancy for bobbers, the tug and props matches the visual surface on host and clients.
  4. Dynamic ripples: a render texture stamped by casts, bobbers, fish strikes and boats (the extension's job). Skip underwater rendering unless the design needs it.
  5. Shoreline foam and blending against the coast, shingle and jetty edges (the critics' surfaces ask). Lake Dock shares the material.
  - The forest is handled separately and free: Blender-baked LOD meshes and billboard cards, and thinner crown cards on Low (the critics' forest asks). Flat Kit (143368, $39.90) was the only one-pack option that touched both water and forest, but it doesn't help forest performance.

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

## 10. Harbour detail pass (user, 2026-10-14) - DONE, merged into main 36f8abb
- **Quality-first run (user, 2026-10-14):** "stop focusing purely on GTX 900 cards and just make the best map possible". The spec Graphics tab note was revised (53732a7): no per-view cap, Low is a best-effort fallback.
  - **PAUSED 2026-10-14 (PC restart + weekly usage at 97%, resets 2026-10-18 ~02:00 CDT).**
- **Cloud delegation (2026-10-14):** Agent isolation "remote" fell back to local worktrees and used the weekly plan (97% to 98%), so it was stopped. Only the Sonnet review of harbour-art3 r1 finished: sound to build on, 2 medium findings (foliage alpha-to-coverage swells cards on Low; far mountains lost in fog and past the 400 m far clip while casting shadows). The findings are in scratchpad harbour-best/r1-review.md, and the r2 artist brief points to them.
  - The save-fix review and the M4 design draft are ready-to-paste briefs for cloud sessions the USER starts: docs/superpowers/handoff/cloud-briefs.md. The fix/* branches are pushed.
    - Round 1: critics 6.6 / 6.8 / 6.7. Artist r1 bbf16d1 pushed on harbour-art3 (SSAO, bloom, height fog, painted rock/stone/plaster/roof, lawn carpet, bay destinations, life on the water). EditMode 1389/1390 (1 skipped), PlayMode 139/139.
    - Round 2: critics 6.8 / 6.7 / 7.2 (cached in the run). The r2 artist was cut off before writing anything; the worktree is clean.
    - Resume: Workflow({scriptPath: ~/.claude/projects/C--Dev-FishGame/0f9e3f02-.../workflows/scripts/harbour-best-wf_8dc74d12-0fa.js, resumeFromRunId: 'wf_8dc74d12-0fa', args as in the run}). r1 and the r2 critics return cached.
    - This work needs the PC (Unity + live Blender) and cannot run in the cloud. Cloud-session notes: docs/superpowers/handoff/cloud-environment.md.
  - Run wf_8dc74d12-0fa (script ~/.claude/projects/C--Dev-FishGame/0f9e3f02-.../workflows/scripts/harbour-best-*.js) on branch harbour-art3, worktree C:\Dev\FishGame-harbour-art.
  - Up to 4 rounds of 3 critics + an Opus artist, pass at >= 8.5 with no category < 7.5, then a final gate. Shots: scratchpad/harbour-best/r<N>. The controller merges into main after it finishes.
  - Never SendMessage workflow agents (memory: workflow-agent-messaging).
- **Polish workflow RUNNING again (run wf_f8fbbf2a-074, resumed 2026-10-14; user: rounds cover map creation + the player environment, not only details: third critic "world" lens, layout + playerSpace scores, macro-first punch list, artist may reshape terrain/areas).** Integration finished by the controller: harbour-art2 5f8ffd5 (pass-2 FBX in Unity, AmbientLife motion/lamps, Low detail toggle, grid rebaked for the tug, HarbourCapture tool; EditMode 1381/1382, PlayMode 139/139). Round-0 shots: scratchpad/harbour-polish/r0 (baseline: fog too heavy, big flat grass, sparse trees, plastic cyan water). Resume = run Workflow with scriptPath ~/.claude/projects/C--Dev-FishGame/0f9e3f02-.../workflows/scripts/harbour-polish-wf_f18c5f35-033.js (integration step replaced by its result; next: toon shader top/bottom tint, then up to 5 critic+artist rounds, pass = overall >= 8.5, no category < 7.5; final check + review; controller merges into main). Worktree C:\Dev\FishGame-harbour-art (Library copied). Unity rewrites .vscode/settings.json there: never commit it.
- **Polish rounds (2026-10-14):**
  - **Scores:** r1 critics 5.8 / 6.2 / 5.5 (average 5.8). After artist r1 (897c31b, 51523e9: terraced town, chapel, windmill, lookout tower stand point 12, east-basin jetty stand point 11, creek, farm hills, mafia hints), r2 critics scored 6.0 / 5.8 / 6.3.
  - **Blockers:** scores stall on items the artist had skipped as out of bounds: water look, crown-card trees, coast/breakwater reshape, ground cover.
  - **User decision (2026-10-14):** all four unlocked, including restyling the Lake Dock water (shared material); "best creativity".
  - **How the unlocks reach the artist:** artist r2 was told by message; the script adds UNLOCK to artist prompts from r3. The controller restarts the run with resumeFromRunId after r2, so r1/r2 come back cached.
  - **Round 2 restart (controller error):** a SendMessage to the running r2 artist cloned it. Both copies then edited the worktree, and stopping one stopped both.
    - Recovery: their work was parked in scratchpad/r2-copy-work (patches and files). The run resumed from the cache (wf_f8fbbf2a-074), and a fresh r2 artist got the unlocks, the user's dusk note and the parked work. HEAD has the local WIP commit 5086384.
    - Lesson saved to memory: never message workflow agents.
  - **Round 2 done:** 20b9c88 + 3fd741c, pushed.
    - Added: crown-card trees everywhere, ~8k GPU ground tufts (detail, hidden on Low), coast spit/shingle/mole/shelf, tug moved 4.2 m east and 3 m north with a 7 m gangway, creek shelter and market corner. The water is calmer (Lake Dock too; collider dump identical) and the day warmer; dusk untouched.
    - Tests: EditMode 1388/1389 (1 skipped), PlayMode 139/139. Worst views: Low 325k, High 495k.
    - r3 critics: 6.5 / 6.8 / 6.8, average 6.7 (up from 6.0). Layout is stuck at 5.5.
    - Run resumed again so artist r3 gets PERF3.
  - **Workflow finished** (5 rounds, 21 agents, ~2.2M subagent tokens): critic averages 5.8 → 6.0 → 6.7 → 6.9 → 6.7. The 8.5 target was not reached; scores plateaued at about 6.8.
    - Weakest: layout 5.5-6.2, surfaces 5.5. Strongest: lighting and dusk 7-7.6, cohesion 7-7.5.
    - r3 (bb37300): West Horn and lagoon, bent lane and village square, bed-mapped water.
    - r4 (8a390f0, 35d66b1): creek glen, West Head waterfall, reef and Skerries, East Ledge stand 14, villagers/dog/ducks, toon clouds.
    - Final gate (Sonnet) fixed the generator ROOT path (27cc5d6). Check on the branch: EditMode 1389/1389, PlayMode 139/139 with -dev-profile fgtest. Worst view: Low 384,592 (under 500k), High 534,204.
  - **Next (harbour):**
    - the presets change from the perf research (Medium shadow tier, Low 35 m, High 120 m with 3 cascades, extra lights 3/4/8, FSR on Low, LOD bias);
    - LODs or billboards for the forest, walls, hedges and ivy;
    - thin the crown cards on Low (alpha cover up to 2.8 screens in eye views);
    - village layout (cross-cut, building ring) and wall/ground surface blending, the critics' remaining asks;
    - per-zone fish tables (reef, ledge, lagoon, creek marsh);
    - stand 14 is 2.4 m from the jetty edge (no test enforces 3 m).
  - Unused leftovers: the parked r2-copy-work patches (folded in by r2) and worktree C:\Dev\FishGame-harbour-art (can be removed). Tests run without -dev-profile fgtest touch the user's real LocalLow settings; always pass the profile.
  - **Perf research** (docs/superpowers/research/2026-10-14-friendslop-graphics-settings.md, user: follow the average):
    - Low budget 500k triangles per view, plus renderer, shadow-caster and alpha-coverage audits. This is PERF3 in the script from r3; the controller restarts the run after r2 to apply it.
    - Presets change to make after the workflow, on harbour-art2: Medium shadow tier 50 m/2048/2; Low 35 m; High 120 m/2048/3 cascades; extra lights 3/4/8; FSR on Low; LOD bias 0.7/1.0/1.5.
- **Status (2026-10-14):** branch `harbour-art2` pushed. Commits 3bfd603 (detail pass) and dbeaf30 (fixes). Phase-1 FBX are byte-identical; the rebuild from an empty Blender is deterministic. 37 new FBX (core 149.7k tris, detail 43.2k hidden on Low) plus a `pass2` manifest block (placements, animHint, smoke/light points, ladders).
- **Triangles (worst view):**
  - High: ~372k (22k over the old 350k aim).
  - Low: ~327k.
  - The perf target is a GTX 900 at Low-Medium, so High may stay over. Trim with LODs/impostors in the Unity task (see the asset study in research/2026-10-14-reference-style-notes.md).
- **Tug placement:** moored on the EAST (outer) face of the breakwater at (36.8, 0, -22). The inner face clipped the x=14 beach cast lane.
  - The tug hull covers ~70 castable cells and the gangway ~6, so a grid rebake is needed in the Unity task.
- **Open items:**
  - The 16 code village trees are still cones/blobs. Replace them in the Unity task, at ported positions.
  - The LUCKY BREAK lettering is partly hidden by the fender.
  - The bow waterline stripe is jagged.
  - Ice chests are one model with 4 placements.
  - Sapling/A.N.T./Poly Haven were not used (determinism).
- **Unity builder checklist:** in the agent's report (task a0dad2601da7be3ce); summary in the next Unity brief. The key points:
  - Copy the FBX + manifest + `HarbourPierLayout.Visuals` together.
  - `_Tri` models use M_Toon_Tri.
  - `Harbour_Core_River` uses the water material, with no collider and not on the Water layer.
  - Col models: MeshCollider only. Wall/pillars/dunes go on the Shore layer.
  - Low hides group "detail".
  - Rebake the grid.
- User: the harbour is "decent but very simple". Wants all of: shaped land, harbour life + clutter, shoreline + vegetation, backdrop/skyline; cozy-busy; "don't be afraid to add triangles" (user has an RTX 5070). Perf target revised: GTX 900-series at LOW-MEDIUM settings.
- Story (user): each map must feel like a stop on the downriver run ("they drove the boat to that area"): the crew's getaway boat moored as a hero prop (FBX Harbour_Core_CrewBoat, reused for the M4 boat ride), a river mouth in the backdrop, light mafia hints (wanted poster, dark boat on the horizon), the world continuing past the walls. Life from noise/clutter/details (REPO, PEAK) and realism + scale (RV There Yet).
- Blender artist (Opus) on worktree C:\Dev\FishGame-harbour-art, branch harbour-art2 (from af95354). Output: Harbour_Core_*, Harbour_Detail_*, *_Col FBX + manifest (animHint per object for Unity sway/bob/smoke/gull wiring). Screenshots 15+ go to the build log.
- Then a Unity task (post-M3, fix round): place the new meshes, colliders, a Low-preset Detail toggle, simple animation + ambient sound emitters from animHint; harbour grid must stay byte-identical unless re-baked on purpose.
- Carry the same map-feel rules into the M4 river maps and the design session.
- Crew boat = TUGBOAT (user, 2026-10-14; spec R78 on m3-fish-ecology fa09d49): Rust tug silhouette reference, walkable interior storage, fits 4, several fishing spots aboard; per-player placeable ice chests that store fish until sold, lost if left behind at day end. Harbour pass places a moored tug (inside the breakwater) with 4 placeholder chests; driving and chest rules are M4 design-session items.

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

## 12. Ideas queued for the design session
- Seagulls that steal voices (user, 2026-10-14): like the Lethal Company voice-mimic mods (Skinwalkers/Mirage), gulls record short clips of players and squawk them back positionally. Needs voice chat first (Vivox likely). Research: docs/superpowers/research/2026-10-14-voice-mimic-seagulls.md (in progress). Must have an opt-out setting.
- Token efficiency (user, 2026-10-14):
  - **Ponytail benchmark:** 2 tasks with and without its rules (Sonnet, no Unity). Diffs were 37-60% smaller, but tokens only 1-4% lower, and the save fix lost tests and the read-only .bak view. Not adopted.
  - **Code navigation:** installed the .NET 10 SDK, `csharp-ls` 0.28.0 (global dotnet tool) and the csharp-lsp plugin (project scope). `Tools/sync-csproj.ps1` gives worktrees Unity's project files.
  - **Turned off for this project** (.claude/settings.json):
    - plugins: playwright, typescript-lsp, security-guidance, superdesign, yapui;
    - synced plugins: brightdata, data, design, engineering, cowork-plugin-management, pdf-viewer;
    - the `rea` MCP server (~/.claude.json project entry; it timed out every session).
  - **Account default:** the claude.ai connectors Vercel, Supabase and Gmail are now off by default for new sessions. Re-enable them from the + menu, Connectors.
  - **Benchmark branches kept** (worktree-agent-*): the two without Ponytail are real backlog fixes (newer-schema meta.json read-only; journal write debounce). They need a Unity test run plus an Opus review (save data) before merging.
  - The REA study is finished.
