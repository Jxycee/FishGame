# FishGame M2 - Plan Skeleton

- **Date:** 2026-10-11. Branch `m2-fishing-core`.
- **Contract:** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (binding; section numbers below refer to it as "C").
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` section 17.2 and the sections it cites. The user approved two design additions on 2026-10-11 (fight motions per profile, clean-catch bonus: C 13 M2-D15/D16); the spec is amended to match separately.
- **Amendments (2026-10-11):** controller rulings, the consistency-check fixes, the staged fish models and the bad-luck nudge (R65 / C 13 M2-D23) are listed in C 15 and applied below.
- **Status (2026-10-12):** T1-T19 are committed (T19 is in review); per-task commits and the review fixes are in the status table below. Review findings, rulings T8-R1/R2, T9-R1, T16-R1 and the process notes since the section plans were written are in C 16 and applied to the not-yet-implemented task texts as "Carry-forward (2026-10-12)" blocks.
- **Purpose:** task list, order, file ownership, tiers and the split for the section authors. Section authors write task bodies (behaviour, test cases, acceptance; code only for tricky parts: math, state machines, RPC sequences) in their own plan files. No section author changes a public name, path or test name from the contract; a needed change is reported to the controller as a contract change.

## 1. Rules for section authors

1. One Opus (effort high) author per section. Read: `CLAUDE.md`, the handoff `docs/superpowers/handoff/2026-10-10-m2-handoff.md`, C sections 1, 2, 3, 13, 14 in full, and the C section 7 entries, section 10 rows and section 12 rows of your tasks. Read the spec sections your tasks cite.
2. Each task body has: header (goal, owned files from C 12, shared edits from C 2.6, depends on, implementer tier, reviewer tier), steps in TDD order (C 2.4), the exact test names from C 10 with their cases, the commands to run (`Tools/unity.ps1` with `$env:FG_OUT`), acceptance (what the reviewer checks), commit command (C 2.1).
3. Code in a task body only for: the `RodStateMachine` transition code (T8), `FightSim` per-tick and motion math (T7), `FishingHostLedger.Result` / `CleanClaimAccepted` (T9), the cast/commit RPC sequences (T17, T18), shader lighting cores (T20, T21). Everything else is behaviour + signatures + tests. A task whose body contains the complete code is marked `Implementer: Haiku (complete code below)`.
4. Every brief repeats C 2.8 (global constraints) by reference and spells out the ones that bite in that task.
5. Keep each plan file under about 1,200 lines.

## 2. Tiers (from the handoff)

- **Implementer:** Sonnet for integration tasks (networking, gameplay, UI flow, builders, shaders, PlayMode); Sonnet for pure Core unless the body has complete code (then Haiku). Controller for Blender work (T29).
- **Reviewer:** Opus high for integration tasks; Sonnet for pure Core, data and docs after the controller's scripted checks pass. Exceptions (Opus high on pure Core): T8 and T9, because they are the protocol state machine and the host validator behind acceptance criteria 2 and 7.
- **Re-review of a fix:** Sonnet for fixes of 50 lines or less in one area; Opus when the fix touches ordering or concurrency.
- **Final branch review:** Opus xhigh after T30. **Escalation:** Fable.
- Controller scripted checks before every review (handoff): brief-vs-diff code blocks, BOM / non-ASCII scan, forbidden identifiers (`DEVELOPMENT_BUILD`, `#if DEBUG`, bare `Input.`, `Unity.Services` outside Networking, `UnityEngine.Random` / `System.Random` in Core), test count vs brief, owned-files check against C 12 and C 2.6.

## 3. Task list

Columns: goal; owned files are exactly C 12 for that task (shared edits per C 2.6); deps = must be committed first; Impl / Review tiers.

| # | Task | Goal (one line) | Deps | Impl | Review |
|---|---|---|---|---|---|
| T1 | Carry: hygiene | EditMode asmdef refs; `Pcg32.Range(float)` fix; `LogOnce` + AudioBus warn rate limit (+ `AudioBusTests`); `EventIsolation` at the six session raise sites + comment fix. Done (`1c8d78d`) | - | Sonnet | Opus high |
| T2 | Carry: splits | `LocalNetworkSession` into 3 partial files (main < 300 lines, the attempt lifecycle goes to `.Start.cs`); `UiFlowController` (< 400 lines) into router + `UiSessionActions` + `UiFocusRouter` + `IHudExtension` seam; behaviour unchanged. In progress | T1 | Sonnet | Opus high |
| T3 | Carry: PlayMode harness | Frames+time waits, realtime guards, cancel on `Await` timeout, `ServiceRoots` asserts, `SuppressDevAutoStart` reset; new leave-after-Connected test | T2 | Sonnet | Opus high |
| T4 | Carry: builders | Load assets after `NewScene` (Bootstrap, LakeDock); `MeshReferences`, `MissingScripts`, `BootstrapReferences` validators. Done (`43178ee`) | T1 | Sonnet | Opus high |
| T5 | Core: fishing model | Enums (incl. `AnnounceKind.PityCast`), `RodCue`, stats structs incl. motion fields, `FishingTuning` (incl. `CleanCatchMaxTension` and the four pity fields), shared test data, `FishingDatabase`, `CastMath`, `FishPointMath`, `RodWireQuant`, `FishingConstants`, `ToolKind` | T1 | Sonnet (Haiku if complete code) | Sonnet |
| T6 | Core: bite and roll | `BitePlanner`, `CatchMath` (incl. clean-bonus tier upgrade), `PlaceholderRoller`, `FishingRoll` (incl. `Pity`), `FishingRollValidation` | T5 | Sonnet | Sonnet |
| T7 | Core: fight | `FightSim` per spec 9.5 + junk mode + motion parameters (ramp, calm drift, flip gap, lateral jitter) + pity multipliers + forced-phase seams; `TensionCues` | T5 | Sonnet | Sonnet |
| T8 | Core: rod state machine | `RodStateMachine` with every 9.2 row, effects queue, `FightReport` on results; row-per-test map C 10.2 | T5, T6, T7 | Sonnet | Opus high |
| T9 | Core: host ledger | `FishingHostLedger` (incl. the per-slot bad-luck counter), `CatchValidator` (incl. clean-claim bounds), `CreelRules`, `CreelLedger`, slot/client keys | T5, T6, T8 | Sonnet | Opus high |
| T10 | Input and context | Router rod rows, `InteractLive`, `GameplayContextArbiter`, `IDevInput` (adapter + fake), settings fields | T1 | Sonnet | Opus high |
| T11 | Audio M2 | M2 recipes + `Rod_Thunk`, `SfxRecipes.All`, `MinDistance`, AudioBus loops/duck/cue overrides, `RodCueAudio`, `CaptionRequested`, audition menu | T5 | Sonnet | Opus high |
| T12 | Water math and meshes | `WaveMath`, `WaterSurface` time/wave seam, `MeshBuilder`, `RippleMath`, `FaceJitter`, `ShaderGlobals` | T1 | Sonnet | Sonnet |
| T13 | Data defs and registry | All Defs (C 7.13) incl. `FightProfileDef` motion fields and tuning `cleanCatchMaxTension`; `ContentRegistry` fishing categories, singletons, `Fishing` database | T5, T6, T11, T12 | Sonnet | Sonnet |
| T14 | Seeds and content builder | `FishingSeedData` (spec tables + motion seeds + pity seeds), `FishingContentBuilder` (create-if-missing), `FishingValidators` (incl. one-submesh models), ContentBuilder/ValidationRunner/AppBootstrap hooks; generate assets | T13, T4 | Sonnet | Sonnet |
| T15 | Tuning bots | Greedy/CounterReel/Rhythm bots on the real assets, every profile x Twig/Handmade, signature-motion and clean-rate prints; retune data until 9.12 targets pass or are recorded as tuning items | T7, T14 | Sonnet | Sonnet (Opus high if a target needs more than data) |
| T16 | Gameplay hooks | `NetTool`, `ServerInitialized`, `PlayerCreel` + `CreelEntry`, `SessionState.ServerCreels`, context arbitration + movement lock + aiming speed in `PlayerLocomotion`, `Teleported`, interactor gate, rig paths, `AddFovKick`, bell debug sell | T5, T9, T10 | Sonnet | Opus high |
| T17 | Fishing network and host | Wire structs, `FishingRodNetwork` (RPCs C 8.2, NetVars C 8.3), `FishingHost`, `CastProbe`, `FishingDebugSettings`, bus messages, `ProtocolVersion = 2` | T6, T9, T13, T16 | Sonnet | Opus high |
| T18 | Owner controller | `FishingRodController`: input -> machine -> RPCs/cues, 50 Hz accumulator, context request, equip/stow, swim/teleport ends, marker probe, `NetRodState` writes, `FightEnded` | T8, T10, T16, T17 | Sonnet | Opus high |
| T19 | Integration: cast to land | `PlayerFishingRigBuilder` + `PrefabBuilder` hook, `FishingSceneObjects` + LakeDock hook, build, PlayMode `SoloHost_CastBiteFight_Lands` and siblings (C 10.4) | T3, T12, T18 | Sonnet | Opus high |
| T20 | Toon and fish import | `FishGame/Toon`, common HLSL, `ShaderValidator`, `RenderValidators`, `RenderMaterialLibrary`, body meshes via MeshBuilder, `FishModelPostprocessor`, perch reimport | T12, T13, T19 | Sonnet | Opus high |
| T21 | Water, line, sky | `FishGame/Water`, `FishGame/Line`, `FishGame/GradientSky`, `RenderingSetupBuilder`, volume profiles, camera post-processing | T20 | Sonnet | Opus high |
| T22 | World presentation | `PaletteGlobals`, `WaterTimeDriver`, `RippleField`, `PresentationSceneObjects` + LakeDock hook | T12, T13, T21 | Sonnet | Opus high |
| T23 | Feedback and reveal | `FishingFeedback` (audio, loops, trauma, FOV kicks, rumble, captions, snap jump), `CatchRevealView`, `FishFallbackMesh` | T11, T19, T22 | Sonnet | Opus high |
| T24 | Rod presentation | `RodPresenter`, `LineView`, `BobberView` (arc once, wave bob), `SplashFx`, `CastMarkerView`; `AddPresentation` body | T22, T23 | Sonnet | Opus high |
| T25 | Fishing HUD | Charge ring, captions, line gauge, creel count, "Click!", catch/lost/clean toasts, settings rows, strings | T2, T18, T24 | Sonnet | Opus high |
| T26 | Dev overlay | F3 overlay and F4-F9 selectors through `IDevInput` | T25 | Sonnet | Opus high |
| T27 | Telemetry | `FightLogRow`, `FightLogCsv`, `FightTelemetry` on the scene `Fishing` root | T18, T19 | Sonnet | Sonnet |
| T28 | Lake dock rebuild | MeshBuilder props, water grid + shader, sky, fog, volumes, presentation and fishing roots; perf and water-pass numbers | T22, T24, T27 | Sonnet | Opus high |
| T29 | Species models | Import and verify the staged `Art/Export/Fish_{Bluegill,Perch,Carp,Trout,Catfish}.fbx` (one submesh, linear vertex colours; the new perch replaces the spike perch); build wires models | T20, T28 | Controller (Blender MCP only if a fix is needed) | Sonnet + user look check |
| T30 | Acceptance | M2 acceptance checklist, MPPM list, 9.13 walk, fun-gate template, README, final `check`; then Opus xhigh branch review | all | Sonnet | Sonnet, then Opus xhigh (branch) |

Serial order: T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8 -> T9 -> T10 -> T11 -> T12 -> T13 -> T14 -> T15 -> T16 -> T17 -> T18 -> T19 -> T20 -> T21 -> T22 -> T23 -> T24 -> T25 -> T26 -> T27 -> T28 -> T29 -> T30.

### 3.1 Status (2026-10-12; branch `m2-fishing-core`)

"Fix" = the review follow-up commit(s) already landed. Details of every review finding and ruling: C 16.

| # | Status | Commit(s) |
|---|---|---|
| T1 | done | `1c8d78d` |
| T2 | done | `649fb88` |
| T3 | done | `ed20831`, fix `1c3b423` |
| T4 | done | `43178ee`, fix `4eb9edc` |
| T5 | done | `7879ad1`, fix `339742f` (test strength, NaN/infinity guards) |
| T6 | done | `fdd8cde`; tests strengthened in `96a5f89` (with T7) |
| T7 | done | `f6b0495`, tests `96a5f89` |
| T8 | done | `7a809c8`, fix `95e31ee` (T8-R1, T8-R2) |
| T9 | done | `0d0c02b`, fix `abecb40` (I-1, I-2, T9-R1) |
| T10 | done | `27c076f` |
| T11 | done | `3f1c62c`, fix `2296e1a` |
| T12 | done | `c1e1291`, fix `4385084` |
| T13 | done | `8b34985`, tests `a51e072` |
| T14 | done | `1d50a04`, fix `03bb4a9` (`BuildAt`, create-if-missing guard) |
| T15 | done | `5a652bd` (tuned values below), tests `f1476c3`, spec tables `4a4028f` |
| T16 | done | `37f6723`, fix `d40534a` (T16-R1) |
| T17 | done | `b9f31ac`, fix `071dc2d` |
| T18 | done | `dea35f8` |
| T19 | done, in review | `0c070a0`, test tweak `d2d119a` |
| T20-T30 | not started | - |

**Mini-fix batch (after T19 review, before T20; Sonnet + Sonnet re-review):** slot takeover also resets the host ledger slot (`FishingHostLedger.ResetSlot`), `PlayerKey.MaxIdLength` 61, `PlayerCreel.cs:91` comment, `PlayerHooksTests` brace formatting, `FishingHost` UnknownPlayer reply sends `Vector3.zero`, cheap origin-vs-seen check before `CastProbe`, no local Recall/Escaped cues during despawn (C 16.3). In progress at the time of writing.

**T19-fix (T19 review, CHANGES REQUIRED; ruling T19-R1, C 13 M2-D28; after the mini-fix batch; Sonnet impl + Opus re-review):** rod rest rotation so the rod points camera-forward pitched up about 35 deg in the owner pose, `FishingConstants.HookedLineSlack` re-derived from the rod's measured forward reach (value TBD), the tip assertion in `SoloHost_CastBiteFight_Lands`, the shared `TearDown(rig)` driver helper and the amended driver surface (C 7.19), `PrepareResetScene` reuse. T24 must not move `TipAnchor`.

**T15 tuned values (recorded in `FishingSeedData`, the assets and `TunedDeviations`; spec 9.6/9.7/9.12 tables updated in `4a4028f`):** `tuning.relaxWhileReeling` 0.10 -> 0.20 (the TOP of the 9.12 range: no headroom), `profile.thrasher.staminaDrain` 0.09 -> 0.035, `profile.runner.staminaDrain` 0.05 -> 0.035. Near-snap recovery >= 40% (spec 17.2) is a human fun-gate metric, not a bot gate (bots, informational: Rhythm bulldog 3%, thrasher 6%, runner 57.5%); bulldog Rhythm median 37.4 s (floor 35 s); Twig thrasher trophy (weightT 0.9) median about 93 s, noted for playtest (C 16.2, T30).

## 4. Coverage map (17.2 build list and tests -> tasks)

| 17.2 item | Tasks |
|---|---|
| Core `RodStateMachine`, `CastMath`, `BitePlanner`, `FightSim`, `FishingHostLedger`, `CatchValidator`, `PlaceholderRoller`, weight/quality/value, `TensionCues`, router rod rows, M2 `SfxRecipes` | T5, T6, T7, T8, T9, T10, T11 |
| Data: RodDef x4, BaitDef x6, FightProfileDef x5, FishSpeciesDef x5 + 2 junk, FishingTuningDef, SfxCueTableDef, ArtPaletteDef, WaterSettingsDef | T13, T14 |
| `FishingRodNetwork`, `FishingRodController`, movement lock, `NetTool`, host creel, debug loadout and selectors | T16, T17, T18, T26 |
| Presentation: MeshBuilder props, 4 shaders + `ShaderValidator`, `RenderingSetupBuilder`, `WaterTimeDriver`, `RippleField`, `RodPresenter`, bobber, splashes, `FishingFeedback`, catch reveal, remote cast arc | T12, T20-T24, T28 |
| UI: charge ring, marker, captions, gauge, creel, toasts, F3 overlay, settings | T24 (marker), T25, T26 |
| Telemetry CSV | T27 |
| Lake dock rebuilt with props and water shader; ripple debug key | T28, T26 (F9) |
| Species models (17.2 user action; staged in `Art/Export/`) | T20 (import settings), T29 (import and verify) |
| EditMode state machine/cast, bite, fight, roll and ledger, data and input, Tuning bots; PlayMode; manual MPPM | C 10 (T5-T19, T27); T30 (manual) |
| User additions 2026-10-11: fight motions (C M2-D15), clean-catch bonus (C M2-D16), bad-luck nudge (C M2-D23) | T5, T6, T7, T8, T9, T13, T14, T15, T17, T18, T25, T26, T27, T30 |

## 5. Pipelining (implement N+1 while N is in review)

Only one implementer runs at a time (Unity lock). Reviewers are read-only and run in parallel. A pair below is safe when the two tasks share no file (C 12, C 2.6); fixes for N land as later commits.

| While in review | Next implementer may start | Note |
|---|---|---|
| T1 | T4 (then T2) | T2 waits for T1's review: both edit `LocalNetworkSession.cs`. Run T4 before T2 if T1's review is slow. |
| T2 | T5 | disjoint |
| T3 | T6 | disjoint (T3 is PlayMode test files only) |
| T5 | T6, T7 | T6 and T7 are disjoint; batch-dispatch them back to back |
| T7 | T10 | T8 needs T6 and T7 committed (review may still be pending) |
| T8 | T11 | T9 needs T8's `FightReport`; T11 is disjoint |
| T9 | T12 | disjoint |
| T10, T11, T12 | T13 | T13 edits only Data files |
| T14 | T16 | T15 depends on T14; T16 is disjoint from T14/T15 |
| T15 (long bot runs) | T17 | T15 only edits test files and `.asset` values |
| T16 | T17 | disjoint (T17 reads T16's public API, already committed) |
| T17 | T18 | disjoint |
| T18 | - | T19 is the first end-to-end run: wait for T16-T18 reviews to land fixes first |
| T19 | T27 | disjoint except `FishingSceneObjects.cs` (T27 edits it): start T27 only after T19 is approved |
| T20 | - | T21 edits `RenderMaterialLibrary`/`PrefabBuilder` after T20 |
| T21 | T22 | disjoint except the LakeDock hook line (T22 adds a new hook, T21 none) |
| T22 | T23 | disjoint |
| T23 | - | T24 owns `AddPresentation` where T23 added a line: serial |
| T24 | T27 (if not done) | disjoint |
| T25 | - | T26 edits the same UI files: serial |
| T26 | T28 | disjoint |
| T28 | T29 (controller) | the controller models in Blender while T28 is reviewed; import after approval |

**Batches (one dispatch, separate commits):** T6 + T7 (same area, disjoint files); T10 + T12 when short on wall time (disjoint); T25 + T26 for one Sonnet if the first lands clean (serial commits, one review each).

## 6. Section split (7 sections, one Opus author each)

| Section | Plan file | Tasks | Notes for the author |
|---|---|---|---|
| A Carry-forwards | `docs/superpowers/plans/2026-10-11-m2-plan-carry.md` | T1, T2, T3, T4 | Read drift reports `drift-net-gameplay.md` 5.1-5.2, `drift-ui-app-editor-tests.md` sections 2, 4, 5 (session scratchpad `m2plan/`). Behaviour-preserving refactors: list the existing tests that prove it. |
| B Core fishing | `docs/superpowers/plans/2026-10-11-m2-plan-core-fishing.md` | T5, T6, T7, T8, T9 | Owns the tricky code: FightSim per-tick + motion math (draw order fixed and written down), the 9.2 transition code, ledger `Result` + clean-claim bounds. Hand-worked checks from spec 9.5 become test cases. |
| C Core support | `docs/superpowers/plans/2026-10-11-m2-plan-core-support.md` | T10, T11, T12 | Router rows from C 7.10 verbatim; recipes per spec 11.7 rules; MeshBuilder determinism. |
| D Data and tuning | `docs/superpowers/plans/2026-10-11-m2-plan-data-tuning.md` | T13, T14, T15 | Seeds = spec 9.6/9.7/9.12 tables + motion seeds (C 7.7) + clean threshold 0.8. Bots load assets, never seeds. Optional input: `m2plan/research-stardew.md` recommendations 1 and 5 (background only; 9.4 bite wait is unchanged). |
| E Fishing net | `docs/superpowers/plans/2026-10-11-m2-plan-net.md` | T16, T17, T18, T19, T27 | Writes the RPC sequences (C 9) as code. T19 is integration (C 2.6 integration-fix rule). PlayMode fixture per C G9. |
| F Presentation | `docs/superpowers/plans/2026-10-11-m2-plan-presentation.md` | T20, T21, T22, T23, T24 | Shader cores per spec 10.3/10.4; batchmode `ShaderUtil.allowAsyncCompilation = false`; arc-once and snap-on-spawn rules; Toon meshes need vertex colours. |
| G UI, world, acceptance | `docs/superpowers/plans/2026-10-11-m2-plan-ui-world.md` | T25, T26, T28, T29, T30 | T29 is a controller task: write it as a checklist for the controller (Blender MCP steps, export settings, LFS check), not as a subagent brief. T30 includes the fun-gate log and the final branch review hand-off. |

Authors may run in parallel (they only write their own plan file). Cross-section questions go to the controller, who answers against the contract.

## 7. Per-task acceptance highlights (for authors to expand)

- **T1-T4:** EditMode and PlayMode stay green after each; `LakeDock` layout byte-identical after T1 and T4 (`git diff --stat Assets/_Project/Scenes` shows only fileID churn, then restore).
- **T7:** all spec 9.5 hand-worked checks hold with motion parameters at 0; each profile's signature test passes with its seeds; the pity run snaps later than the normal run for the same seed.
- **T8:** C 10.2 table complete (38 row tests + 6 rule tests).
- **T9:** every clean-claim rejection keeps the rolled quality and still lands the fish; three failed casts make the next cast Pity, a Landed resets, cancels neither count nor reset.
- **T15:** bot output pasted in the task report; any missed target either retuned (asset + seed) or listed as a tuning item for T30.
- **T19:** `check` green; manual solo cast-to-land on keyboard and gamepad by the controller (acceptance 1 dry run).
- **T20/T21:** `Validation_Shaders_Pass` green in `-nographics` batchmode; first screenshot check by the user in the editor.
- **T28:** lake dock looks complete (user check); frame time and SRP batches within 10% of the M1 baseline or noted.
- **T30:** every 17.2 acceptance criterion has a checklist line and an owner; the fun gate is a user activity (30+ logged fights with at least one friend) recorded after M2 code is done; a USER line for the bad-luck nudge (keep / tune / remove). Added 2026-10-12 (C 16): criterion 7 wording (host rejects forged and out-of-order messages; owner-written transform limits position checks; friends-only), near-snap recovery >= 40% as a human fun-gate metric with tuning hints, F3 overlay check in the editor and in a non-development build, the Twig thrasher trophy time, and the M4/M5 hand-off notes.

## 8. Open questions and risks (controller decides; not resolved by the spec)

1. Perch FBX vertex colours (C G6): resolved. The spike perch (5 submeshes, no vertex colours) is replaced by the staged single-submesh perch in T29; until then T14 leaves `perch.model` null and the reveal uses the fallback mesh.
2. V11: `DeferLocal` on a host-owned object is source-confirmed (net plan N1); T19's ordering test stays as the runtime check, and a failure needs a manual one-frame deferral in `FishingRodNetwork` (contract change). `CatchAnnouncedRpc` now also defers locally (ruling E-Q8).
3. Clean-claim bounds are one-sided on the clock (owner never ahead of host + 2.0 s) and cannot check the profile without a host replay (spec 9.9 forbids it); a skilled cheat is out of scope (friends-only, spec 1.2), and the 2.0 s slack is a guess to confirm under MPPM at 150 ms RTT.
4. Motion seeds (C 7.7) are first guesses; Darter micro-bursts at flipRate 1.0 and Bulldog calm drift may push medians out of the 9.12/17.2 targets. T15 decides by data.
5. `RodStateData` change detection: closed. NGO uses `Equals` for unmanaged `IEquatable` types (net plan N3), so padding cannot cause extra sends.
6. `PlayerFishingRigBuilder` and `PrefabBuilder` are edited by four tasks in sequence; a later rebuild that reorders root NetworkBehaviours would change NGO behaviour indices. Only T19 adds NetworkBehaviours (C 6.3).
7. `-nographics` shader validation (V13): if `ShaderUtil` reports nothing under `-nographics`, T20 owns a minimal `Tools/unity.ps1` change (graphics-capable mode for the validation run; ruling F-Q4); T20 must settle it before T21.
8. Gamepad verification (acceptance 1) needs a controller on the user's machine; schedule it with the T19/T30 manual checks.
