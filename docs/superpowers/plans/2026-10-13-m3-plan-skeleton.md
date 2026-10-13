# FishGame M3 - Plan Skeleton (Fish ecology)

- **Date:** 2026-10-13 (written 2026-10-12). Branch `m3-fish-ecology`, cut from `m2-fishing-core` (or `main` after the M2 merge) when the entry gate holds.
- **Contract:** `docs/superpowers/plans/2026-10-13-m3-interfaces.md` (binding; "C" below). **Spec:** section 17.3 and the sections it cites (C header). **M2 contract:** `2026-10-11-m2-interfaces.md` ("C2").
- **Purpose:** task list, order, file ownership, tiers and the split for the section authors. Section authors write task bodies (behaviour, test cases, acceptance; code only for the tricky parts) in their own plan files. No author changes a public name, path or test name from the contract; a needed change goes to the controller as a contract change.

## 0. Open questions for the user (each has a default that applies if unanswered)

1. **Reeds are too shallow to cast into.** The lake bed slopes 0.15 m per metre from the shore. So the whole 0-5 m "reeds" band is at most about 0.7 m deep, below the 0.8 m minimum landing depth: today nobody can cast into the reeds. M3's "where you fish matters" needs them castable. **Default: lower the minimum landing depth from 0.8 to 0.5 m** (one tuning value). The alternative is a steeper first metre of lake bed near the shore (a lake layout change). (C M3-D52)
2. **Do the new fish models gate M3's exit?** M3 needs 20 new models (18 species plus 2 specials; the M2 bluegill, perch, carp, trout and catfish models are reused for bluegill, yellow_perch, grumpy_carp, sunny_trout and mudcat). They are built in the new R74 look after look-dev approval. **Default: no.** Models land in batches as a parallel controller track, you look at each batch, and any species without a model shows the procedural fallback fish. M3 exits on the gameplay criteria. (C 2.8 #28, T23)

Everything else is a ruling in C section 13. Two of those rulings change how the game plays, so you may want to look at them:
- M3-D3: the bare hook can now catch any species, but it still brings more junk and slower bites.
- M3-D31: Harbour Pier is reached in M3 through a dev-panel map switch; the Hub map board arrives in M4.

## 1. Entry gate and dependencies (C 1.2, 1.3)

- **R72 fight rework F0-F8 committed.** M3 never redefines R72 surfaces (profile and tuning fight fields, `FightSim`, `FishingTestData`, bots, `CatchValidator.MaxReelSpeed`). Fightability uses the R72 Rhythm bot, V5 uses the R72 F8 measured medians, and where the R72 text differs, R72 wins for fight fields.
- **R70 ladders L1-L9 committed.** L1-L6 are in at the time of writing; L7 moves the ramp foot and `Exits` to Default.
- **R73 water** committed (`9120898`).
- **R74 look-dev approved by the user.** It is the T4 slot, owned by the look-dev result (branch `m2-lookdev`, style guide `docs/superpowers/research/2026-10-12-art-direction-lookdev.md`).
  - Only the art-dependent tasks wait for it: T17-T19, T21, T23.
  - Core, data and host tasks (T1-T16, T20, T22) do not. T16 (the lake's world object, stand points and grid bake) reads colliders only; whichever of T4 and T16 lands second keeps the other's `LakeDockBuilder` hooks.
- **M2 acceptance** rerun by the user, or deferred by the user.

## 2. Rules for section authors

1. One Opus (effort high) author per section. Read first:
   - `CLAUDE.md`;
   - the M2 handoff `docs/superpowers/handoff/2026-10-10-m2-handoff.md` (tiers, global constraints) and the rework handoff `2026-10-12-m2-rework-handoff.md`;
   - C sections 1, 2, 13 and 14 in full;
   - the C 7, 10 and 12 entries of your tasks;
   - the spec sections your tasks cite. Reference the spec (for example the 12.6 roster); do not copy it.
2. Each task body has:
   - a header: goal, owned files (C 12), shared edits (C 2.6), depends on, implementer tier, reviewer tier;
   - steps in TDD order (C 2.4);
   - the exact test names from C 10 with their cases, and the mutation note per test (C2 16.4);
   - the commands (`Tools/unity.ps1` with `$env:FG_OUT`, PowerShell tool only, background for full runs);
   - acceptance (what the reviewer checks);
   - the commit command (C 2.1).
3. Code in a task body only for the tricky parts:
   - the resolver gate, algorithm and draw order, and the roller order (T6);
   - `DistanceField` and the reachability bits (T7);
   - the weather chain and the hotspot schedule (T7);
   - the `JournalRules.Apply` table (T8);
   - `DayNightMath.Blend` and the sun/moon weights (T8);
   - the migration steps (T3);
   - the ledger's reservation lifecycle (T13);
   - the dev clock re-anchoring (T14);
   - the bake loop (T16).

   Everything else is behaviour, signatures and tests. A task whose body holds complete code is marked `Implementer: Haiku (complete code below)`.
4. Every brief repeats C 2.8 by reference and spells out the constraints that bite in that task. In particular:
   - #24, in-scene NetworkObject: whole-scene commits;
   - #26, owner and host run the same gates;
   - #27, tints go on top of textured albedo;
   - #28, the fallback mesh stays.
5. Keep each plan file under about 1,200 lines.

## 3. Task list

Owned files are exactly C 12 (shared edits per C 2.6). "Deps" means the task must be committed first. "LD" means the task waits for the T4 look-dev adoption.

| # | Task | Goal (one line) | Deps | Impl | Review |
|---|---|---|---|---|---|
| T1 | Carry: fishing minors | C2 16.1 final-row carry list. The marker is drawn after the machine tick; F6/F7 reset only at session end (needed by map switching); test and comment fixes; HUD null check | gate | Sonnet | Sonnet |
| T2 | Carry: hash, names, cancel | `ContentHash` rejects duplicate categories; one name-truncation helper; online Cancel shows "Cancelling..." at once | gate | Sonnet | Opus high (cancel flow) |
| T3 | Migration | Spec 12.6 step 1: `FishTags`, `Legendary`, species `tags`, bait M3 fields; delete perch/carp/trout/catfish; overwrite bluegill; write bait fields on the 6 existing assets; `dataVersion` 2; `minLandDepth` per Q1 | T1 | Sonnet | Sonnet (+ controller asset diff) |
| T4 | Look-dev adoption and M2 restyle | Owned by the R74 look-dev result, not an M3 section. Style guide, shader/material/texture standard, dock/props/body/five fish restyled, validators adjusted, a season-tint hook. M3 fixes only what it needs (C 7.4) | user approves `m2-lookdev` | per look-dev plan | per look-dev plan |
| T5 | Core: ecology types and clock | Masks, `DayClock`, `ClockMath`, structs, `IWorldContext`, `ZoneData`, `EcologyDatabase`, `CatchLedger`, `EcologyRunState`, `BaitStats.Mods` | T3 | Haiku if complete code, else Sonnet | Sonnet |
| T6 | Core: resolver and roller | `FishingResolver` (12.4 gate and algorithm, fixed draws), `FishingRoller` (replaces `PlaceholderRoller`'s role) + the spec 12.9 resolver tests | T5 | Sonnet | Sonnet |
| T7 | Core: grid, weather, hotspot | `DistanceField`, `ShoreGrid`, `ShoreGridMath`, `WeatherDirector` (35-segment Markov, infinite continuation, season roll), `HotspotPlanner` | T5 | Sonnet | Sonnet |
| T8 | Core: journal and day-night maths | `MetaSave` DTOs, `JournalRules`, `BitingNow`, `JournalHints`, `DayNightMath`, `SilhouetteMath` | T6 | Sonnet | Sonnet |
| T9 | Data: ecology defs and registry | Zone/rule/weather/grid/tuning defs; registry partial (`zones`, `Ecology`, spot by scene); M3 fields on SpotDef, ArtPaletteDef, SfxCueTableDef | T7 | Sonnet | Sonnet |
| T10 | Content: seeds and builder | `EcologySeedData` (roster 12.6 by reference, 7 junk, rules, weather, tuning, sun/moon, weather looks, season ambience); `EcologyContentBuilder`; generated assets; harbour SpotDef | T9 | Sonnet (Haiku if complete) | Sonnet |
| T11 | Coverage | Data hard-fail rules, V1-V3, 13.3 variety (M3 part), fightability on the R72 Rhythm bot; `ValidationRunner` hook | T10 | Sonnet | Sonnet |
| T12 | Balance reports | V4 expedition sim, V5 economy and the M4 quota-calibration print; retune data or record tuning items | T11, R72 F8 | Sonnet | Sonnet (Opus high if a target needs more than data) |
| T13 | Host roll | `FishingHostLedger` on the resolver with a host-built context; M3 cast checks (grid water, day over); catch-limit reservations; cast counter in the run state; `PlaceholderRoller` and species-level gates removed | T6, T10 | Sonnet | Opus high |
| T14 | World state | `WorldState` (4 NetworkVariables, `IWorldContext`), `ClockAnchor`/`HotspotState` wire, `DevWorldDriver` (dev clock, weather track, hotspots, pause, auto new day), `EcologySession`, ProtocolVersion 4 (R72 took 3) | T7, T10 | Sonnet | Opus high |
| T15 | Fishing integration | `FishingHost` builds the context from `WorldState` and the zone grid (replacing T13's interim context); owner marker runs the same gates (`CastingOpen`, grid water); `FishingDebugSettings.Luck`; `DevLoadSpotRequested` -> `SceneFlow` map switch | T13, T14, T16 | Sonnet | Opus high |
| T16 | Lake world and bake (NOT LD; runs before T15) | `StandPoint`s, `ShoreGridBaker` (castable + reachable bits), `WorldSceneObjects` (first in-scene NetworkObject: MPPM sync check, fallback C M3-D18), map validators (reachability, water layer, layer split, WorldState hash) | T11, T14 | Sonnet | Opus high |
| T17 | Harbour Pier map (LD) | `HarbourPierBuilder/Layout/Meshes` in the R74 style (C M3-D40 starting layout), every per-scene hook (C 6.3), build order index 3, grid bake | T4, T15, T16 | Sonnet | Opus high (+ user look check) |
| T18 | Day-night and seasonal tint (LD) | `DayNightController` (phases, sun/moon flip, fog, volumes, sky globals); `PaletteGlobals` season from the world and the `_FGSeasonTint` tint on textured albedo (C M3-D23) | T4, T8, T14 | Sonnet | Opus high |
| T19 | Weather looks, hotspot VFX, ambience (LD) | `WeatherFx` (rain, fog), `HotspotFx` (every peer), seasonal `AmbienceBed` sets, `Rain_Patter` recipe + cue row | T18 | Sonnet | Opus high |
| T20 | Save and journal recorder | `SaveService` (meta.json v1, atomic, `.bak`, Newtonsoft); `JournalRecorder` on every peer; journal toasts; save on quit | T8, T15 | Sonnet | Opus high |
| T21 | Journal UI (LD) | 25-card journal, levels 0-3, biting now, filters, completion; `IconFactory` in the R74 icon style; J key routing; bite closes it; main-menu Journal button and summary line | T4, T20 | Sonnet | Opus high |
| T22 | Dev panel and HUD clock | F3 overlay -> panel cycle; host-editable world/dev values; client read-only; load-map button; F8 scrub; clock HUD with season and weather glyphs | T15, T20 (and T21 for the shared UI files, unless look-dev is late) | Sonnet | Opus high |
| T23 | Fish models (LD, controller) | 20 new models via Blender MCP in the R74 style, batches of 4-6, user look check per batch; fallback mesh until each lands. Off the critical path (Q2) | T4, T10 | Controller | Sonnet + user look |
| T24 | PlayMode integration | `EcologySoloTests` (resolver cast to land with journal, off-grid reject, clock and pause, WorldState sync, harbour load, map switch, day over) + integration fixes | T16, T17, T20 | Sonnet | Opus high |
| T25 | Acceptance | M3 acceptance checklist (17.3 bullets, 12.9 MPPM list, dev-panel direction checks, F8 scrub, readability, art status), README, final `check`; then the Opus xhigh branch review | all (T23 may be partial) | Sonnet | Sonnet, then Opus xhigh (branch) |

**Serial order:** T1 -> T2 -> T3 -> T5 -> T6 -> T7 -> T8 -> T9 -> T10 -> T11 -> T12 -> T13 -> T14 -> T16 -> T15 -> T20 -> (T4 lands here at the latest) -> T17 -> T18 -> T19 -> T21 -> T22 -> T24 -> T25.
- T16 runs before T15: T15's host and owner gates need the lake's `World` object and baked grid. T13 installs an interim fixed context (C 7.13) so every M2 cast test keeps landing until T15.
- T4 runs on its own track as soon as the user approves look-dev, and must land before T17.
- T23 runs in parallel with any implementer after T4 and T10. The controller models while implementers work; the Unity import waits for a free lock.
- If look-dev is late, T22 moves ahead of T17 (it needs T15 and T20 only; it then waits on nothing look-dev owns). The UI hooks are then made in the order T22, T21 (the C 2.6 rows for the UI files are re-ordered by the controller, with no contract change).

## 4. Coverage map (17.3 definition of done -> tasks)

| 17.3 item | Tasks |
|---|---|
| 23 species, 2 specials, 7 junk, 2 spots, weather table, 5 profiles, 2 baked grids with 12.6 values | T3, T9, T10, T16, T17 |
| `WeatherDirector` pre-rolls 35 segments deterministically, season held for the week | T7, T14 |
| SceneBuilder builds `Spot_HarbourPier`; both grids re-baked in place (stable GUIDs); lake passes reachability | T16, T17 |
| `PlaceholderRoller` replaced by `FishingResolver`; bait gains `BaitMods`; registry indices and `ContentHash` identical across two builds and editor sessions | T3, T5, T6, T9, T13; T25 checks the two-build hash |
| 12.9 validators, fightability, resolver tests, 13.3 variety; balance reports pass or have tuning items | T6, T7, T11, T12 |
| Dev panel changes to season, band, weather, rod tier or hotspot visibly change catches as the data predicts | T14, T15, T22; manual check in T25 |
| `DayNightController` and seasonal palette on the dev clock, with no pops (F8 scrub); readable at night and in max fog at 25 m; fog readability test | T8, T18, T19, T22 |
| MPPM: identical catch on all peers; journals right for catcher and witness; clients can't change world state | T14, T15, T20, T22; T25 manual |
| Journal in meta.json v1, `.bak` recovery, levels 0-3 and biting now | T8, T20, T21 |
| Core `noEngineReferences`; Fishing doesn't reference Networking or UI | every task (existing architecture test); T25 confirms |
| M2 carry list (C 14) | T1, T2 |
| R74 art (models, harbour props, icons) | T4, T17, T21, T23 |

## 5. Pipelining (implement N+1 while N is in review)

Only one implementer runs at a time (the Unity lock). Reviewers are read-only and run in parallel. A pair below is safe when the two tasks share no file (C 12 and C 2.6).

| While in review | Next implementer may start | Note |
|---|---|---|
| T1 | T2 | disjoint |
| T2 | T3 | disjoint |
| T3 | T5 | T5 needs `FishTags` committed (review may be pending) |
| T5 | T6, T7 | disjoint; batch-dispatch back to back |
| T6 / T7 | T8 (after T6), T9 (after T7) | disjoint files |
| T9 | T10 | T10 edits `FishingSeedData`/`FishingContentBuilder`, which T3 also touched: wait for T3's review fixes first |
| T10 | T13 | T11 and T13 are disjoint; run T13 first if T11's bot runs are slow |
| T11 (long bot runs) | T12 or T14 | T12 edits only tests and assets |
| T13 | T14 | disjoint |
| T14 | T16 | T16 places T14's `WorldState`; its public API is committed |
| T16 | - | T15 edits `FishingHost` (T13's interim) and relies on T16's grid and world object: wait for both reviews and the MPPM sync check |
| T15 | T20 | disjoint except `FishingSceneObjects.cs` (T20 only) |
| T20 | T17 (if T4 landed) or T22 | T17 copies T16's patterns; T22 is disjoint from T20 |
| T17 | T18 | disjoint except `PresentationSceneObjects.cs` (T18 adds one line; T17 calls it unchanged) |
| T18 | - | T19 edits the same presentation files: serial |
| T19 | T21 | disjoint |
| T21 | - | T22 edits the same UI files: serial |
| T22 | T24 | disjoint |

**Batches** (one dispatch, separate commits):
- T1 + T2 (small, disjoint);
- T5 + T6 + T7 for one Sonnet if T5 lands clean (serial commits, one review each);
- T18 + T19 when wall time is short.

## 6. Section split (7 sections, one Opus author each)

| Section | Plan file | Tasks | Notes for the author |
|---|---|---|---|
| A Carry and migration | `docs/superpowers/plans/2026-10-13-m3-plan-carry-migration.md` | T1, T2, T3 (T4 is listed but not authored: it points at the look-dev plan) | Use the C2 16.1 final-row details and the M2 handoff "Later" list. Migration code is complete, idempotent and tested in `Tests/__Temp`. List every M2 test that names a deleted species and its fix. |
| B Core ecology | `docs/superpowers/plans/2026-10-13-m3-plan-core.md` | T5, T6, T7, T8 | Owns the tricky code: the resolver gate, algorithm and draw table; the roller order; the chamfer; the reachability bits; the Markov row maths; the hotspot schedule; `JournalRules.Apply`; `DayNightMath`. The spec 12.9 numeric checks become test cases with their tolerances. Mark T5 Haiku-ready if you write it out in full. |
| C Data, content and coverage | `docs/superpowers/plans/2026-10-13-m3-plan-data-coverage.md` | T9, T10, T11, T12 | Seeds reference spec 12.6 rows, never re-derive them. Bots and reports load assets, never seeds. Fightability and V5 read R72's bots and F8 numbers. The `EcologyTunedDeviations` and `EcologyTuningItems` maps follow C2's D-Q2 and D-Q6. |
| D Host, world and network | `docs/superpowers/plans/2026-10-13-m3-plan-host-world.md` | T13, T14, T15 | Writes the reservation lifecycle, the `DevWorldDriver` re-anchoring (pause, scrub, auto new day, map switch) and the host context build as code. T15 is integration (the C 2.6 integration-fix rule). Each T14 class opens with its authority comment. |
| E Maps | `docs/superpowers/plans/2026-10-13-m3-plan-maps.md` | T16, T17 | T16 runs between section D's T14 and T15 and does not wait for look-dev; T17 does. The bake loop is code. The in-scene NetworkObject MPPM check is a T16 step with the C M3-D18 fallback. Harbour layout starts from C M3-D40 and follows the R74 style guide. Every per-scene hook (C 6.3) gets an assertion. |
| F Presentation | `docs/superpowers/plans/2026-10-13-m3-plan-presentation.md` | T18, T19 | The tint goes on textured albedo (C M3-D23; check the T4 shader hook first). No per-frame allocation. Fog density stays readable (`DayNightMath.IsReadable`). Every loop passes `follow` and stops in `OnDisable` (C2 16.2). |
| G Journal, UI, art and acceptance | `docs/superpowers/plans/2026-10-13-m3-plan-journal-ui.md` | T20, T21, T22, T23, T24, T25 | T23 is a controller checklist (Blender MCP steps per the R74 guide, batches, LFS size report, user look check), not a subagent brief. T21/T22 are manual-check UI tasks, built on Core helpers that have tests. T25 holds the acceptance doc and the branch-review hand-off. |

**Section status (2026-10-12):**

| Section | Status |
|---|---|
| A Carry and migration | **Written** (`2026-10-13-m3-plan-carry-migration.md`); contract change requests accepted and applied |
| B Core ecology | **Written** (`2026-10-13-m3-plan-core.md`); contract change requests accepted and applied |
| C Data, content and coverage | **Written** (`2026-10-13-m3-plan-data-coverage.md`); contract change requests accepted and applied |
| D Host, world and network | **Written** (`2026-10-13-m3-plan-host-world.md`); contract change requests accepted and applied |
| E Maps | Not written: waiting for the R74 look-dev approval (authoring waits so that T17 can follow the style guide; T16 itself is not look-dev-gated in the contract, 3 and section 1) |
| F Presentation | Not written: waiting for the R74 look-dev approval |
| G Journal, UI, art and acceptance | Not written: waiting for the R74 look-dev approval |

The contract change requests of A-D were collected and ruled on 2026-10-12 (all accepted as proposed): see the "Section-author CCR resolution (2026-10-12)" note and M3-D55..D77 in the contract section 13. Authors of E-G read the amended contract (in particular M3-D56..D59 for `WorldState`/`DevWorldDriver`/`EcologySession` and M3-D76 for the serializable helper field names).

Authors may run in parallel (each writes only its own plan file). Cross-section questions go to the controller, who answers against the contract.

## 7. Per-task acceptance highlights (for authors to expand)

- **T1-T2:** EditMode and PlayMode stay green. The marker is never a frame behind. F6/F7 survive a scene reload and clear at session end.
- **T3:** after `build` the registry has bluegill only among the old species, the four FBX files are untouched, and every bait asset has non-zero `affinityMult` and `specialMult`. `dataVersion` is 2, and a second `build` changes nothing (the controller diffs `Content/`).
- **T6:** every spec 12.9 resolver tolerance holds. A hand-replayed `Pcg32` pins the draw order. A default `EventMods` throws.
- **T7:** the weather frequencies over 10,000 steps match the renormalised rows. A grid with no reachable cell never spawns a hotspot.
- **T11:** the V1-V3 and variety reports are printed in the task report. Any fightability miss is an approved tuning item with numbers.
- **T12:** the V4/V5 tables are pasted in the report. Misses are retuned in data (asset + seed + deviation map) or listed for T25.
- **T13:** every C2 ledger test name still passes against the resolver. A limit-1 special can't be landed twice by two concurrent casts.
- **T14:** the late joiner sees the same minute within a frame of rounding. Solo pause stops the clock. The protocol is 4 (R72 took 3).
- **T15:** an honest cast at the grid edge is never host-rejected (owner and host gates agree). The map switch keeps the cast counter.
- **T16:** reachability passes for all three lake sub-areas (needs Q1). `WorldState` syncs under MPPM with a non-zero hash. The scene is committed whole.
- **T17:** the harbour plays end to end (spawn, swim, ladder, cast in all three sub-areas, bell); the user checks the look.
- **T18:** F8 scrub across a full day shows no pops, including at the sun/moon flip. A season change re-tints textured props without flattening them.
- **T20:** a corrupt `meta.json` recovers from `.bak`. A witness journals Seen/CrewLanded and the catcher Caught.
- **T21/T22:** manual checks in the editor and a non-development build (F3 is a no-op there). A bite closes the journal. Clients can't edit.
- **T23:** each batch passes `SpeciesModels` and the render validators. The LFS size is reported. The user approves the look.
- **T25:** every 17.3 bullet has a checklist line and an owner. The two-build `ContentHash` equality is checked. The MPPM list (12.9 manual) is ready for the user.

## 8. Risks (plan-level)

1. **The look-dev dependency (R74).** It is not approved yet, and the style guide may change the fish-model and material rules (one submesh, vertex colours vs textures), the validators and the LFS footprint.
   - Mitigation: T4 owns the adoption. Core, data and host work (T1-T15) doesn't wait. The fallback mesh keeps every species playable.
   - Art tasks re-read the guide before starting. Watch the GitHub LFS 1 GB quota (K17) once painted textures arrive; each art commit reports its size.
2. **The fight rework isn't built yet.** At the time of writing only the R72 plan exists. M3's fightability, V5 cycle times and the host Landed floor depend on its end state, so the entry gate waits for F8. If R72's final text moves a field, R72 wins and the contract is amended.
3. **The first in-scene NetworkObject.** Every build renumbers fileIDs, which changes `WorldState`'s `GlobalObjectIdHash`.
   - Host and client must run the same scene file. Scene churn can't be partially restored.
   - The fallback is a `NetSpawnAnchor` prefab with the same API (C M3-D18), verified in T16 before T17 copies it.
4. **The reeds depth conflict (Q1).** Without a decision, the lake's reachability check fails the build.
5. **Fightability and balance on a Rust-faithful baseline.** Bulldog/Runner on Twig may miss the 90% bar, and V4/V5 bounds may miss. These become recorded tuning items; M3 does not retune R72.
6. **Two players rolling one special.** Reservation semantics (C M3-D14) prevent double catches. A cancelled reservation makes the special briefly unavailable to the rest of the crew, which is accepted.
7. **Map switching is dev-only in M3.** The bait stock and creel are per scene (`FishingHost` ledger) and reset on a switch; the run state does not. This is acceptable in development; M4's Hub and run snapshot replace it.
8. **Scope.** There are 25 tasks, about the same as M2's 30, but the presentation, UI and art tasks depend on look-dev. Cut-line per spec 12.7: if M3 runs long, ship with `InHotspot = false` (keep T7's planner and tests; T14 does not spawn hotspots, and T19 skips `HotspotFx`).
