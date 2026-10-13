# FishGame - Handoff: M2 rework after the user's first manual checks (2026-10-12)

## Paste this to start the next conversation

> Read `docs/superpowers/handoff/2026-10-12-m2-rework-handoff.md` and `CLAUDE.md`, then continue the M2 rework on branch `m2-fishing-core`. Start with the open decisions in section 2, then plan and run the work in section 3 with the agent roles in section 6.

## 1. Where things stand

- Branch `m2-fishing-core` (pushed to GitHub, NOT merged to `main`). M2 tasks T1-T30 are done and reviewed; the Opus xhigh branch review and its fix wave were approved. Last full `check` green: EditMode 869 + 1 skipped (GPU parity test, graphics-only), PlayMode 64/64.
- Progress board the user watches: https://claude.ai/artifact/JuQGKUKKWbK1eYdzmUBdkN (db collections `tasks`, `agents`, `todos`; the user's to-dos go on its "Waiting on you" list, collection `todos`).
- Source of truth: spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` (amendment log 3.2 now runs to R71); contract `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (section 16 = every implementation delta and ruling, incl. the M3 carry list in the "Final branch review + fix wave" row); acceptance `docs/superpowers/plans/2026-10-11-m2-acceptance.md`.
- User decisions made 2026-10-12 (all in the spec): R67 cast measured from the rod tip (built); no lead-in gate / no host minimum catch time; R68 self-hook gag (M5); R69 graphical overhaul before full release; R70 RV-style ladders (to build); R71 boat docking assist (M6).

## 2. Open decisions for the user (ask first)

1. **Water look.** The user rejected the dark water and then the "washed-out grey-blue" fix (`256a00e`). An Opus look-dev pass produced variants (see section 3.3 for status and image paths). The user picks one.
2. **Rust Dedicated Server download (optional, for the exact fight formula).** Rust's client does NOT contain the fight maths - the fight runs on Rust's server (see research notes). The exact strain/drain/timing formulas are in Rust Dedicated Server (Steam app 258550, free, anonymous SteamCMD, several GB): `RustDedicated_Data/Managed/Assembly-CSharp.dll` is believed to be plain .NET (Mono), readable with the ILSpy CLI. Downloading needs the user's explicit OK (state source and size; scan it like Cpp2IL was: hash + Windows Defender). Without it, build the Rust-baseline from the recovered tunables + the inferred formula in the research notes and tune by feel.
3. **Settings default focus.** `0c0211d` moved UI scale to the top of Settings, so gamepad focus now lands on the UI scale slider on open (left/right changes the scale). Recommend moving default focus back to Master volume - confirm.

## 3. The work (all M2, before the user reruns acceptance)

### 3.1 Fight rework - Rust-faithful first, then tune friendlier (user decision)

Diagnosis (fight felt "super easy, zero resistance, gauge never reaches 80%; catfish impossible"): NOT a bug. Live game == spec 9.5 model == bot data. Causes: strain only builds during bursts and in proportion to pull (spec 9.5 even says "bluegill effectively can't snap"); calm reeling relaxes tension; assist (x0.6) was on in 5 of 9 logged fights; the T15 bots test unrepresentative cells (start line = castMaxDist 18 m, weightT 0.5/0.9 while real weights skew to ~0.22, a Greedy bot that never steers); catfish: reel floor 0.13 m/s vs runner run 0.81 m/s and stamina regen in calm > drain, so a ~35 s stall.

User choices:
- Fight model: **Rust style** - reeling always loads the line (heavier fish faster, un-countered runs and dives much faster), tension drains only when you stop reeling (the Rust "pump" rhythm). Build a Rust-faithful baseline first, then tune more friend-slop with the user.
- Countering: **visibly drags the fish back toward centre and tires it fast**; greedy reeling into a big fish's run snaps.
- Walking while waiting for a bite stays allowed; judge fight-length targets at realistic cast distances and weights.
- Small fish can snap if careless (proposed ~7 s of reckless reeling) - confirm with the user when presenting the baseline.

Research: `docs/superpowers/research/2026-10-12-rust-fishing.md`. Recovered from Rust's files: fish direction is a FLAG set (PullingLeft 1, PullingRight 2, PullingBack 4 - a fish can pull back and sideways at once); rod max cast 10 m, catch distance 1.0 m, global strain multiplier 0.75; bobber sideways 1.2 / pull-away 1.3 / player sideways input 0.8 / reel-in 0.6 (units maybe forces); fish tiers strain 0.8 / 1.0 / 1.25, move 0.8 / 1.0 / 1.25, reel-in 1.2 / 1.0 / 0.9; strainGainCurve is ONLY the creak volume (silent <= 0.10); tension bar display smooths toward strain at clamp01(dt*10); bobber rolls up to 30 deg toward the pull side; server-authoritative; fail reasons TensionBreak, BadAngle, TimeOut, PlayerMoved, TooClose, TooFarAway, TooShallow, NoLure. Not recovered (server-only): the per-tick strain formula, drain, direction-switch timing, TimeOut value.

Proposed FightSim changes (research notes sections 6-7): per-profile `StrainMod` (Steady 0.8, Darter 0.9, Bulldog 1.1, Thrasher 1.0, Runner 1.25) and `ReelInMult`; `ReelStrainBase` (~0.07/s, any reeling loads the line x weight x StrainMod); un-countered run x1.6, dive x1.6; `CounterRelief` 0.5 -> 0.8; `RelaxWhileReeling` 0.20 -> 0 (drain only when not reeling); counter-steer x2 (fish visibly comes back) + stamina drain x3 while countering + reel-penalty relief 0.7; dive becomes a flag that can combine with a side run; `LandDistance` re-think (Rust 1.0 m bobber-to-player vs our line length); `MaxFightSeconds` -> Escaped(Timeout); creak curve silent below 0.1 and steep to 0.95; HUD bar smoothing dt*10; bobber roll +-30 deg. Hand-checked rates in the notes (bluegill un-countered run ~7.5 s to snap; trophy runner greedy ~1.35 s; release from 0.9 drains below 0.3 in ~1.3 s).

Follow-on work: rewrite spec 9.5-9.8 (+ amendment row); bots must cast at realistic distances (10-14 m), draw weights like the game (skew), add a reckless bot that still steers; re-tune fight-length targets, snap rates, near-snap recovery and the R64 clean threshold (calm reeling now costs tension, fights lengthen); T7 draw-order table changes if direction switching gets new random draws; TensionCues/creak; FishingFeedback and the HUD gauge read the new state; owner sim + host validation (spec 9.9) unchanged in authority - update the host's Landed floor if reel speeds change.

### 3.2 Ladders like RV There Yet (spec R70)

Research: `docs/superpowers/research/2026-10-12-rv-ladders.md` (RV ladder = trigger box -> flying mode; W/S along camera forward incl. pitch; hang with no input; exit by leaving the box; no interact, snap or mantle).

Build: walk-in `LadderVolume` (new `Gameplay/World/LadderVolume.cs`); entering (walk, wade, swim, step off the deck edge; blocked while RodFighting) starts Climbing; no gravity; Move travels along camera look at `climbSpeed` 2.4 m/s [tune] with instant stop; leaving ends it; 0.25 s re-entry grace at the top; Sprint/Jump/Interact/rod inputs zeroed (`InputContextRouter.RouteClimbing` passes Move only); rod stowed with a short stow animation on entry and unstowed (animation) after (procedural first-person animation - no animation assets; `FishingRodController.HandleSwim` treats climbing like swimming). Owner-write movement via the existing NetworkTransform; optional `NetClimbing` bool for a remote arms-up pose. Replace the two ladder `WaterExit`s in `LakeDockBuilder.AddExits` with LadderVolumes (~x 1.55..2.55, 1.0 m wide, y -1.4..1.6, mirrored) - the T28 physics fingerprint changes intentionally (record it). Shore ramp: loses the 0.4 s auto-lerp; becomes a walkable slope with a light assist (small upward help at its foot when leaving the water). Code touched: `PlayerLocomotion` (replace TryBeginClimb/BeginClimb/UpdateClimb with volume-driven climb), `MovementMath` (`ClimbSpeed`, pure `ClimbWish`), `InputContextRouter`, `GameplayContextArbiter` (precedence unchanged), `LakeDockBuilder`/`LakeDockLayout`. Tests: InputContextRouter Climbing cases, `SwimInteractGateTests` rewrite (bell must stay unringable from water and ladders), ClimbWish EditMode, rod stows while climbing, re-entry grace, PlayMode climb up and down. Spec text to update: 7.3 exits bullet (line ~671), 7.8 #3 ("climb out within 2 s"), 8.2 Swimming row + new Climbing row; contract D36/DA7.

### 3.3 Water look (pending the user's pick)

See section 7 (filled in at the end of the session that wrote this handoff) for the variant status, images and what is committed.

### 3.4 Small leftovers

- Settings default focus back to Master volume (decision 2.3).
- Docs still carrying old water values: `docs/superpowers/plans/2026-10-11-m2-plan-data-tuning.md` ~200; `2026-10-11-m2-plan-presentation.md` ~572-577 and ~653-656; contract ~1397; spec ~1267 (Water shader description).
- M3 carry list: contract section 16.1, row "Final branch review + fix wave".
- Scratchpad research copies (Rust GameAssembly ~624 MB, Ghidra project, Cpp2IL) were in the previous session's scratchpad and are gone with it; the notes in `docs/superpowers/research/` are the record.

## 4. The user's manual check results so far (2026-10-12, solo, editor)

Numbered list from that session (steps 1-17 solo editor, 18-22 MPPM two windows, 23-24 builds, 25-26 anytime):
1 PASS (UI scale slider vanished at 150% - fixed `0c0211d`); 2 PASS; 3 FAIL water dark (frame time 2.8-3.0 ms global, CPU 0.2 ms - record as perf baseline); 4 ladders -> R70 rework; 5 PASS; 6 FAIL fight too easy (rework 3.1) and "assist doesn't switch gauge to Arc" (no bug: the user's settings already had assist on; the rule fires on assist off->on while the gauge is Off; PlayMode test `653ea4e` proves it - ask the user to retry assist OFF, gauge Off, assist ON); 7 PASS (wind ambience continuing during pause is fine); 8 PASS; 9 models all good, catfish impossible (rework 3.1).
Still to run after the rework: steps 6, 9, 11 (edge cases), 12 (readability), 10, 13-17, then Part B (MPPM 2 windows, solo is fine), Part C (dev + non-dev builds), Part D (sounds pack, fallback fish shape). The playtest (30+ fights) needs a friend.

## 5. Process notes from this session

- Implementers sometimes edited with scripts (python/sed) instead of Write/Edit; restate the rule.
- Every Unity run churns the three scenes (fileID renumbering): `git restore Assets/_Project/Scenes/*.unity`. Interrupted runs can leave a gitignored `Assets/InitTestScene*.unity` (delete it) and git LFS can create an untracked `dev/null/` folder (delete it).
- A batchmode UI Toolkit screenshot attempt froze Unity once - never try HUD screenshots in batchmode. Mesh/scene renders without `-nographics` are fine.
- Full `check` exceeds the 10-minute foreground tool limit: run it in the background and wait.
- The auto-mode classifier once blocked `git push`; the user then re-confirmed push permission. The user has standing approval to push when work is solid. Never merge to `main` without asking.
- Never kill Unity processes; the user's "kill it" approval on 2026-10-12 was for one stuck batch run only.

## 6. Agent roles (user preference, unchanged)

One Opus (high) author per plan section; Sonnet implements; Haiku only for complete-code briefs and mechanical scans; Opus (high) reviews integration work, Sonnet reviews pure-Core/data/docs and small fixes; Opus (xhigh) final branch review; Fable escalation. For the fight rework: Opus (high) writes the FightSim design + spec rewrite, Sonnet implements, Opus re-reviews, bots retuned and the user plays the baseline before the friendlier tuning pass.

## 7. Water look status (end of session)

- Committed `be51da9` with **variant C as the default** (pending the user's pick). Comparison sheet: `docs/superpowers/handoff/img/2026-10-12-water-variants.png` - rows A tropical teal, B deep lake blue, C recommended (dock | shore), then C at dusk | C at night.
- Why it was grey: depth measured along the view ray (always "deep" at low angles), `_ToonShade` darkening most facets, full-strength fog, low opacity showing the beige bed, and a ramp tuned to one sun height.
- New `Water.shader` (displacement and `FishGameCommon.hlsl` untouched): vertical depth from reconstructed bed position with an exp curve blended in sqrt space; exaggerated shading-only facet normal (`_FacetBoost` 7, `_FacetContrast`, fades past `_FacetFade` 55 m) with a sun-height-independent split; light brightness with only `_LightTint` of its colour plus `_ToonAmbient * _AmbientStrength`; crest tint (`_CrestHeight` 0.11, `_CrestAmount`); hard Blinn-Phong glints (`_GlintPower` 96, threshold 0.5, `_GlintFlatten` 0.7, fade-in past `_GlintNear` 6 m); fresnel sky reflection toward horizon/zenith colours (`_ReflectStrength`, `_ReflectZenith`); `_Saturation` before fog; fog at `_FogAmount` 0.35; foam band on vertical depth with smooth value-noise wobble (wrapped time kept).
- Variant values (sRGB, alpha = opacity): A shallow (0.22,0.95,0.84,0.55) deep (0.00,0.58,0.72,0.96) depth 2.2; B shallow (0.28,0.74,0.76,0.65) deep (0.02,0.20,0.48,0.98) depth 1.4; **C shallow (0.30,0.86,0.80,0.68) deep (0.03,0.36,0.62,0.97) depth 1.6**, light tint 0.35, facet contrast 0.14, crest 0.35, reflect 0.4/0.6, saturation 1.15, fog 0.35. Material pins live in `RenderMaterialLibrary` (`WaterLook` table); seeds in `FishingSeedData` + `Content/Art/WaterSettings.asset`.
- Tests run: build PASS; EditMode `Validation|Validator|Shader|Wave|FishingContent|RenderingSetup` 288 pass + 1 skip; GPU wave parity PASS (graphics run). **Still to do:** full `check`, an Opus review of the shader, and spec 10.4 text (depth fade formula, fog amount, glints/reflection/crest/facet additions, value-noise foam) plus the stale water lines listed in 3.4.
- Known caveats: few daytime glints (lower `_GlintThreshold`/`_GlintPower`/`_GlintFlatten` for more sparkle); foam cross-fade might pulse in motion and the foam line is slightly thinner (shader wobble base 0.35); water receives no shadows. The user should judge it in Play mode with moving waves.
