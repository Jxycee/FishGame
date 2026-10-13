# FishGame - Handoff: M2 rework built, look-dev waiting, M3 planned (2026-10-13)

Supersedes `2026-10-12-m2-rework-handoff.md` (its sections 2, 3.2, 3.3 and 7 are done; see below).

## Paste this to start the next conversation

> Read `docs/superpowers/handoff/2026-10-13-m2-rework-done-handoff.md` and `CLAUDE.md`, then continue. Start with section 2 (the user's answers and check results), then section 3.

## 1. Where things stand

- **Branch `m2-fishing-core`** (pushed, NOT merged to `main`). The rework is built and reviewed: smooth water (R73), RV-style ladders (R70), the Rust-faithful fight with five styles kept as quirks (R72), network ProtocolVersion 3. Full `check` PASS on be8d7c5: build, EditMode 946 (945 pass, 1 skipped GPU test), PlayMode 83/83. The final Opus xhigh branch review is approved with fixes; that fix round landed in 975a6f0 (ladder top-out velocity clamp, latched spent-fish state), 63e8c9c (F10 cycles the forced species, host-only, dev builds), 15a374a and 2a39077 (docs). Full `check` PASS on 63e8c9c: build, EditMode 957 (956 pass, 1 skipped), PlayMode 83/83; bot numbers unchanged from acceptance K.1. Known minor: if the spent latch clears mid-run, the remote raises SideRun when FlagBurst reappears while the owner raises nothing (cosmetic).
- **Branch `m2-lookdev`** (worktree `C:\Dev\FishGame-lookdev`, own Library, not pushed, not merged): the R74 "realistic toon" look-dev. The soft toon shader (ramp, sky fill, rim, highlight), painted texture arrays generated in Blender (`Art/Blender/fishgame_lookdev.blend`, text block `fg_textures.py`), rounded dock/rock/trees, a pillowy player, a painted bluegill. Colliders are byte-identical. Style guide: `docs/superpowers/research/2026-10-12-art-direction-lookdev.md` (on that branch). Sheet: `docs/superpowers/handoff/img/2026-10-12-lookdev-realistic-toon.png`. **Waiting on the user's approval.** Adoption steps are in the look-dev report and style guide ("Adopting this across M2"): the other four fish, reeds/bell/rod/bobber, a shader keyword split for the mapping mode, a perf measure, a palette check, the spec 10 rewrite.
- **M3 (fish ecology) planned:** contract `docs/superpowers/plans/2026-10-13-m3-interfaces.md` (rulings to M3-D77), skeleton `2026-10-13-m3-plan-skeleton.md` (25 tasks, 7 sections), section plans A-D written (`-carry-migration`, `-core`, `-data-coverage`, `-host-world`). Sections E-G (maps, presentation, journal/UI/art) wait for the R74 look approval. M3 protocol bump = 4. When the M3 branch is cut, apply the pending spec deltas M3-D65 (13.3 "at most two adjacent bands"), M3-D66 (12.9/13.3 definitions) and M3-D74 (minLandDepth 0.5, unless the user picks a lake-bed shelf).
- **Progress board (this rework):** https://claude.ai/artifact/SSE5uoZ7ELPyZP5A11qdXR (db collections `status`, `phases`, `tasks` with `stream`/`code`, `todos`, `decisions`, `research`, `agents`). Old M2 board: https://claude.ai/artifact/JuQGKUKKWbK1eYdzmUBdkN (history).

## 1b. Current state (updated 2026-10-13, late)

- **M2 is merged into main:** 235d460 (PR #1), after a green `check` and all reviews. The look adoption (R74), the seasonal trees ("full seasons") and the story R76 are part of it. No PRs from now on: push and merge when reviewed and green (CLAUDE.md Git section).
- **M3** is in worktree `C:\Dev\FishGame-m3` on branch `m3-fish-ecology` (pushed; HEAD 702ce4f). Controller notes for every review and ruling: the session scratchpad m3-ccr.md, mirrored in the contract rows M3-D55..D91.
  - Done, reviewed and green (EditMode 1261 + 1 skip, PlayMode 97): T0 (spec deltas R75), T1-T3 (carry, migration), T5-T8 (core), T9-T12 (data, coverage, balance; mirror_minnow weight 4 and red_snapper 18 retuned), T13 (host roll, reservations), T14 (WorldState, dev clock, protocol 4), T16 (lake world object, grid bake: reeds 294 reachable), T18 (day-night), T19 (weather FX, ambience), T23 (all 20 new fish + 4 re-baked reused fish in game; sheet 4x8), plus the cleanup and follow-up rounds.
  - **BLOCKED on the user:** the MPPM two-window WorldState sync check (board todo u16; open `C:\Dev\FishGame-m3` in Unity Hub). After it passes: T15, then T17 (harbour), T20-T22, T24, T25, then an Opus xhigh branch review, then merge M3 into main.
  - The T15 and T17 brief items from the reviews are in the contract and the scratchpad notes: the T15 OnDisable reservation release guard; the PlayerHooksTests re-point; FishingWorldTests pins the world. For T17: the bake order SaveScene -> FinalizeAfterSave -> SyncTransforms -> Bake; the Sun/WarmVolume/CoolVolume/Ambience roots; fog; the whole-file scene commit.
- **After M3** (user plan, memory `post-m3-plan`): one checks handoff for all the user's manual and multiplayer checks (M2 rework + M2 acceptance + M3 acceptance), with a new db-backed notes board the user can write to. Then the fix round, then the M4 handoff (R76 river run).
- **User looks owed:** fish batches 1-4 (all imported already); the mullet journal-icon belly colour question; the M2 short check and the friendlier fight tuning (re-run EcologyBalanceReports after any fight retune, since V5 sits near a cliff).
- **Idle worktree** `C:\Dev\FishGame-lookdev` (branch m2-lookdev, merged): delete only with the user's OK.

## 2. Waiting on the user (all on the board's "Waiting on you")

1. **New look verdict:** approve or adjust; water shallows a touch greener/less neon (about (0.32, 0.80, 0.74, 0.66))? light RV-style haze (day fog about 0.012)?
2. **Short check before M3 code (about 30 min):** play the new fight (acceptance section K.2, M1-M9; F10 forces a species), the ladders and ramp, the water in motion plus frame time, and one Direct 4P two-window catch. Then say what feels too harsh: that starts the friendlier tuning pass.
3. **M3 questions** (defaults apply): reeds depth (minLandDepth 0.5 vs a lake shelf); the 20 new fish models do not gate M3's exit; fixed dev seed.
4. **Fight open questions** (acceptance K.3): Steady strainMod 0.8 vs 0.6; an unattended fish clears only at 120 s; the Steady Nerves headroom x1.15 placeholder.
5. **Later:** the full acceptance pass (the rest of Part A, Part B MPPM, Part C builds, Part D sounds and fallback fish), then the friend playtest (30+ fights), then the decision to merge M2 into `main`.

## 3. Next work

- After the user's look approval: adopt R74 on `m2-lookdev` (or rebase it onto `m2-fishing-core`; it predates the ladder/fight commits, so expect conflicts in `LakeDockBuilder`, `PrefabBuilder` and `RenderMaterialLibrary`), then merge it into `m2-fishing-core`, then write M3 sections E-G.
- After the short check: the friendlier fight tuning pass with the user (data only; R72 values are the baseline), then cut `m3-fish-ecology` and run M3 T1 onwards.
- Carry items: swim-depth hysteresis beside the ladders (the bed is about -1.26 m, right at the swim threshold; the rod stow animation makes the flicker visible) if the user's ladder check shows it; the tighter host Landed floor using the tire ramp (M5 anti-cheat); the far-distance water shimmer guard (R73 note) if seen in motion.

## 4. Process notes from this session

- Rust Dedicated Server: SteamCMD anonymous works (memory `reverse-engineering-tools`); the exact fight maths are in research section 8.
- Two worktrees can run Unity in parallel with a copied Library, but PlayMode runs collide on the UDP port: check for another `-testPlatform PlayMode` Unity first (memory `unity-worktree-playmode`).
- Agents still slipped in `sed` twice; restate "Write/Edit only" in every brief.
- Unity runs churn the three scenes: `git restore Assets/_Project/Scenes/*.unity`; delete the untracked `dev/` folder (git LFS hooks) after git commands.
- Agent roles unchanged (CLAUDE.md).
