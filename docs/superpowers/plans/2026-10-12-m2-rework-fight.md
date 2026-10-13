# FishGame M2 rework - Fight: Rust-faithful baseline (R72)

- **Date:** 2026-10-12. Branch `m2-fishing-core`. Author: fight-rework plan (Opus, effort high).
- **Source of the maths:** `docs/superpowers/research/2026-10-12-rust-fishing.md` section 8 (Rust Dedicated Server, decompiled). It supersedes the inferred formulas of sections 6 and 7.6. Only mechanics and numbers are used; no Rust code enters the repo.
- **User decisions (2026-10-12, final, not re-opened here):** (1) Rust-faithful baseline first, friendlier tuning later with the user; (2) countering drags the fish back toward centre and tires it fast, greedy reeling into a big fish's run snaps; (3) small fish can snap when careless (~7 s; see 0.7); (4) walking while waiting stays allowed, fight targets judged at 10-14 m casts and real weights; (5) authority unchanged (owner sim, host validation, spec 9.9).
- **Binding docs after F0:** spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` (9.1-9.13 as rewritten by F0, amendment R72), contract `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (C) with the R72 row in C 16.1. Where C 7.5, 7.7, 7.13, 7.15, 10.1 or 10.2 conflict with this plan, this plan wins.
- **Playtest gate:** after F6 the baseline is playable. F7 (cleanup) and F8 (docs) can run while the user plays.

## User answers (2026-10-12) and the questions still open

**Answered, and built by this plan:**
- **Q1, five styles:** keep all five now ("Keep 5 styles"). Each profile gets data-driven quirks on top of Rust's rules (section 0.3). Quirks change only how the fish behaves; the strain formula and the tier values are untouched. Each style must stay recognisable blind.
- **Q3, gauge:** red zone past the full mark, up to the snap point (as planned).
- **Q4, fight length:** keep the faithful baseline lengths, and keep the spec line "record baseline, retarget later". Players who never steer time out on large fish.

**Still open (defaults are built):**
- **Q2, careless small fish.** The default is Rust's Steady `strainMod` 0.8: a single unbroken reckless run snaps at 5.3 s, and the hand checks pin that.
  - With the Steady quirk (runs 3-5 s, few back pulls), no single run lasts 5.3 s, so the snap needs strain carried over from earlier runs.
  - Reckless reeling on a bluegill therefore snaps at a median of about 7.4 s (scratch model), close to the user's "about 7 s", with the strain rule still Rust's.
  - *Default: as built.*
- **Q5, AFK.** An unattended fish now clears only at the 120 s timeout (Rust has no slack escape); junk keeps the 12 s slack escape. *Default: accept.*
- **Q6, M4 skill Steady Nerves.** "Snap grace +0.15 s" has no grace left to extend, so F0 rewrites it as "snap headroom x1.15". The 1.15 is a placeholder. *Default: placeholder until M4.*
- **Q7, spec 17.2 fun gate.** F0 replaces the median fight-length targets with "record the baseline, retarget in the friendlier pass". *Default: as written.*

---

## 0. Mapping decisions (with numbers)

### 0.1 Units: strain, tension, lateral

- **Strain** is Rust's line load, in Rust units. The sim integrates `FightState.Strain`.
  - The bar is full at `strainBarFull` 6. The line snaps when strain > `strainSnap` 7.
- **Tension** = strain / 6 is the display value: 1.0 is a full bar, and the line snaps above 7/6 (1.1667). `FightState.Tension` keeps its name, so every existing consumer (cues, line colour, wire, R64) reads the display value.
  - The sim writes Tension every tick. `ForceState` derives it from Strain.
- **Lateral** stays normalized from -1 to +1. +-1 is the cone edge, at `lateralMaxDeg` from the cast line; the tuning value changes 75 -> 60 because Rust's cone is 60 deg.
  - Rust moves the bobber in metres per second, so sideways speed `v` becomes `dLateral = v * dt / (max(lineLength, 1.0) * lateralMaxRad)`. That is the angle a sideways move of `v * dt` metres sweeps at the current radius. The 1.0 m floor (`LateralMinRadius`) only guards the division near the landing.
  - Back pulls and the reel change only the line length (radial). Side pulls and steering change only the angle (tangential). This is the polar form of Rust's Cartesian moves; the second-order radius growth of a tangential step is ignored.
- **Cone edge.** Rust flips the fish's side flags when the next position would leave its 60 deg cone. Here a next lateral beyond +-1 is clamped to +-1 and `BurstDir` flips (no draw).
- **Steer frame.** Rust measures the steer against the rod-to-bobber direction. The sim has no camera, so it uses the cast line: a steer counts only when `sign(steer) == -sign(lateral)` (strictly). Steering can bring the fish back to the cast line but never push it further out. At exactly 0 lateral no steer counts.
- **Digital steer.** Rust's steer is a key. `|input.Steer| >= 0.5` (`SteerDigitalThreshold`) counts as a key press; magnitude is ignored. Holding A and D together is 0 from the input layer, so neither counts.

### 0.2 Landing distance: 1.0 m from the rod tip

- Rust lands the fish within 1.0 m (horizontal) of the cast origin, which is the player.
- FishGame measures casts from the rod tip (R67), so the tip is our cast origin. `landDistance` = 1.0 m of tip-to-fish line keeps Rust's reel-distance arithmetic: you reel (cast distance - 1 m).
- This is a choice, not an equivalence. The tip sits 2.4 m in front of the player (`CastTipReach`), so the fish lands about 3.4 m from your feet.
- The validator range becomes 0.5-3 m.

### 0.3 Profiles: Rust tiers plus per-fish quirks (user answer Q1)

**Tier values (unchanged; these are what enter the strain formula):**

| Profile (species) | Rust tier | StrainMod | MoveMult | ReelInMult |
|---|---|---|---|---|
| Steady (bluegill) | small (anchovy...) | 0.8 | 0.8 | 1.2 |
| Darter (perch) | medium (Rust's yellow perch) | 1.0 | 1.0 | 1.0 |
| Thrasher (trout) | medium (Rust's small trout) | 1.0 | 1.0 | 1.0 |
| Bulldog (carp) | large (heavy, strong) | 1.25 | 1.25 | 0.9 |
| Runner (catfish) | large (Rust's catfish) | 1.25 | 1.25 | 0.9 |

**Quirks (behaviour only, layered on the Rust tick).** They follow the R63 characters. Rust's own values are rest 2-4 s, run 3-7 s, back 0.39, no flips, no ramp, no drift, no jitter and a 4 s wave.

| Profile | rest `calmMin-calmMax` s | run `burstMin-burstMax` s | back `diveChance` | `flipRate` /s (`flipMinGap` s) | `burstRamp` s | `calmDrift` m/s (`calmDriftRamp` s) | `lateralJitter` | `wavePeriod` s | Character |
|---|---|---|---|---|---|---|---|---|---|
| Steady | 2-4 | 3-5 | 0.15 | 0 (0.5) | 0.6 | 0 (0) | 0 | 4 | Tutorial: short, plain side runs that ease in, rarely dives |
| Darter | 1-2 | 2-3.5 | 0.15 | 1.0 (0.3) | 0 | 0 (0) | 0 | 4 | Quick darts that switch sides mid-run: watch the splash |
| Thrasher | 1.5-3 | 3-5 | 0.20 | 0.4 (0.5) | 0 | 0 (0) | 0.3 | 1.5 | Jerky: fast surges (1.5 s wave), each run a different speed, sometimes flips |
| Bulldog | 2-4 | 2.5-4.5 | 0.55 | 0 (0.5) | 0 | 0.06 (2.5) | 0 | 4 | Dives a lot, and creeps line out even at rest |
| Runner | 2-3.5 | 5-9 | 0.30 | 0 (0.5) | 0.8 | 0 (0) | 0 | 4 | Long sideways runs that build up |

**Field meanings:**
- **Timing and back chance** reuse `calmMin/Max`, `burstMin/Max` and `diveChance`. Rust's tire scaling (`x (1 + tire01)` on rests, `x (1 - tire01)` on runs) and the back-pull gate apply unchanged.
- **`flipRate` / `flipMinGap`:** during a run, each tick has a `flipRate x dt` chance to flip the side, at least `flipMinGap` apart. A flip is allowed on the first tick of a run. This uses one draw per eligible tick.
- **`burstRamp`:** the fish's speed (side and back) rises from 0 to 1 over this many seconds from the run start.
- **`calmDrift` / `calmDriftRamp`:** at rest, the line creeps out at `calmDrift x driftRamp x (1 - tire01)` m/s, where `driftRamp` rises from 0 to 1 over `calmDriftRamp` s. No drift once the fish is exhausted.
- **`lateralJitter`:** at each run start one draw gives `runSpeedMult = Uniform(1 - j, 1 + j)`, which multiplies the run's side and back speed.
- **`wavePeriod` (new field):** the period of the pull wave. Rust's is 4 s. Same shape (clamped 0.2..2) and the same mean (1.01); a shorter period makes faster surges.
- **Speed this tick** = `p x MoveMult x ramp x runSpeedMult`. It drives only the side and back movement. Strain, tire, reel speed and the landing are Rust's.

**Fields:**
- `wavePeriod` is the only new quirk field. Every other quirk field already exists (R63), and from R72 the sim uses them.
- `pullStrength`, `lateralSpeed`, `runSpeed` and `staminaDrain` are superseded by the tier fields and are removed in F7.
- Bots and EditMode tests pin that the five styles are distinct (F2 quirk tests, the F4 signature test). The scratch-model numbers are in 0.10.

### 0.4 Rods

- **Handmade is Rust's only rod:** reel 0.6 m/s (`ReelInMoveSpeed`) and steer push 0.8 m/s (`SidewaysInputForce`).
- **The other rods keep their existing ratio to Handmade:**

| Rod | reelSpeed | steerRate | lineStrength (divides gains) | relaxMult (x the 1.5/s drain) | castMaxDist |
|---|---|---|---|---|---|
| Twig | 0.54 | 0.67 | 0.8 | 0.9 | 14 |
| Handmade | 0.6 | 0.8 | 1.0 | 1.0 | 18 |
| Carbon | 0.72 | 0.93 | 1.3 | 1.1 | 24 |
| Deep-sea | 0.78 | 1.07 | 1.6 | 1.2 | 30 |

- `angleWindow` is parked (unused; Rust has no out-of-window strain).
- The reelSpeed validator range becomes 0.3-2.

### 0.5 Weight: does not enter the fight

- Rust has no weight term: size lives in the tier.
- The baseline is meant to feel like Rust, and our tiers already carry size.
- R64 trophies already pay more. Re-adding a per-weight factor later is a one-line data knob in the friendlier pass.
- `weightT` stays on the `FightSim` constructor for API stability; `WeightFactor` and `EffectivePull` are removed.
- The bots still draw weights like the game (`t = u^2.2`). A test proves weightT cannot change a fight (F2 test 23), so the skew is recorded, not relied on.

### 0.6 Assist, Pity, snap grace, near-snap, R64

- **Snap grace** is gone. Rust snaps the moment strain > 7. The headroom band (strain 6-7, bar full but no snap) plays the grace's role. Reckless reeling crosses it in 0.43 s (large) to 0.76 s (small).
  - `SnapTimer` keeps its name and now counts seconds over the full bar (strain > 6). `FlagGrace` and the Line_Crack rising edge follow it unchanged.
- **Assist** multiplies the strain gains by 0.6. The data value is `strainScaleAssist` = 0.45 = 0.75 x 0.6, used in place of `strainScale`, as before.
  - The drain and the headroom are unchanged. A small fish reeled recklessly into a run snaps at 8.84 s instead of 5.32 s.
  - `snapGraceAssist` is retired.
- **Pity (R65)** keeps strain x0.85 on the gains. "The snap grace lasts longer (x1.5)" becomes **headroom x1.5**: the snap strain is 6 + 1 x 1.5 = 7.5 and `SnapTension` is 1.25. A small fish snaps at 6.70 s instead of 5.32 s. There is no draw.
- **Near-snap recovery** counts when tension goes over the full bar (above 1.0) and later falls below 0.5. The `NearSnapHigh` constant changes 0.8 -> 1.0.
- **R64 clean catch** keeps 0.8 of the bar (strain 4.8) as its seed, in display units.
  - `MaxTension` can now exceed 1 (up to 7/6, or 1.25 on a Pity cast).
  - `CleanClaimAccepted` is unchanged. A recovery still implies `MaxTension > 1.0 > 0.8`, so check (3) holds.

### 0.7 Small-fish snap time: Rust's strain rule, ~7 s in play

The baseline keeps Rust's Steady `StrainMod` 0.8. One unbroken reckless run into a side pull snaps at 7 / (2.2 x 0.8 x 0.75) = 5.30 s (tick 266), and the EditMode hand checks pin this on Rust-faithful (quirk-free) test profiles.

Two reasons:
1. Decision 1 asks for a faithful strain rule.
2. In play, Steady's runs last 3-5 s, so a single run never reaches 5.3 s. The snap needs strain carried over from earlier runs, so reckless reeling on a bluegill snaps at a median of about 7.4 s (scratch model: Greedy median 7.38 s, mean 7.29 s; Reckless snaps 74% of fights, median 7.44 s). That matches the user's "about 7 s" (decision 3).

Q2 stays open: if the user wants a ~7 s single run as well, Steady `strainMod` 0.6 gives 7.07 s.

### 0.8 Not copied from Rust, and FishGame-only rules kept

**Not copied:**
- **PlayerMoved:** walking while waiting stays allowed (decision 4).
- **BadAngle and Obstructed:** the sim has no camera or line-of-sight input; the steer frame is the cast line instead (0.1).
- **TooFarAway:** Rust fails the fight past 2x cast range. Here the line clamps at `maxLineOut` = castMaxDist + 12 with no strain, and back pulls stop inside castMaxDist - 2 m. Rust's own back gate (8 m of 10 m) makes TooFarAway rare there too.
- **Rod condition loss, the tea `FishingBoost`, and server authority** (decision 5).

**Kept from FishGame:**
- Junk: drag 3 m/s, no strain, slack escape 12 s.
- The hooked leash and the Dropped outcome.
- The host fallback timeout `maxFightTime` 240 s.
- Fixed 50 Hz steps.
- Seeded `Pcg32`.

**Removed from fish fights:**
- The pinned escape: Rust has no pin; the cone edge flips the run instead.
- The 12 s slack escape: Rust has none; the 120 s timeout replaces it.
- The 0.8 s calm lead-in: Rust's first run starts at once.

### 0.9 RNG draw order (fixed; seeds depend on it) - replaces the T7 table

All draws come from one `Pcg32(seed)` owned by the sim. `Next01()` = one `NextFloat()`. `Uniform(a, b)` = one `NextFloat()` mapped by `Pcg32.MapUnitToRange` when `b > a`, else `a` (the draw is still made).

| Event | Draws, in order |
|---|---|
| Constructor (fish) | 1 `Uniform(0, WavePeriod)` pull-wave phase (Rust uses global server time, so the phase is random per fight) |
| `ForJunk`, `ForcePhase`, `ForceState` | none |
| Run start (tick start, `PhaseTimer <= TimeEps`, at rest) | 1 `Next01` side (`< 0.5` left -1, else right +1); 2 `Next01` back (back when `< DiveChance` AND `LineLength < CastMaxDist - BackPullMargin`; always drawn); 3 `Uniform(BurstMin, BurstMax)`, then x `(1 - tire01)`; 4 **only if `LateralJitter > 0`**: `Uniform(1 - j, 1 + j)` run speed multiplier |
| Rest start (tick start, `PhaseTimer <= TimeEps`, in a run) | 1 `Uniform(CalmMin, CalmMax)`, then x `(1 + tire01)` |
| Each run tick with `FlipRate > 0` and `sinceFlip >= FlipMinGap - TimeEps`, after this tick's switch draws | 1 `Next01` flip check (`< FlipRate x dt` flips the side) |
| Cone-edge flip, tire, wave, ramp, drift, everything else | none |
| Junk sim | none, ever |

- `tire01 = clamp01(Tire / TireRunFull)`, read at the switch, before this tick's tire update.
- The first tick of every fight starts a run.
- After tick 1, `DrawCount == 4` for a profile with no jitter and no flips (all the quirk-free test profiles). Add 1 for jitter, and 1 for a flip check on that first run tick (`sinceFlip` starts at `FlipMinGap`, so the first run tick is eligible).
- A profile with `FlipRate` 0 and `LateralJitter` 0 draws exactly what Rust does.

### 0.10 Hand-worked numbers (research 8.7; Handmade, line strength 1, strain from 0)

The effective multiplier is StrainMod x 0.75: small 0.6, medium 0.75, large 0.9375. Snap ticks below are float32 replays of the sim's accumulation.

| Action | small | medium | large |
|---|---|---|---|
| Reel into a side run (k 2.2): per tick / snap tick / seconds | 0.0264 / 266 / 5.32 (exact 5.303) | 0.033 / 213 / 4.26 (4.242) | 0.04125 / 170 / 3.40 (3.394) |
| Reel into a back pull (k 2.5) | 0.030 / 234 / 4.68 (4.667) | 0.0375 / 187 / 3.74 (3.733) | 0.046875 / 150 / 3.00 (2.987) |
| Reel while resting (k 0.5) | 0.006 / 1167 / 23.34 (23.33) | 0.0075 / 934 / 18.68 (18.67) | 0.009375 / 747 / 14.94 (14.93) |
| Counter-steer only (k 0.8): rate per s; time to 7 at that rate | 0.48; 14.58 s | 0.60; 11.67 s | 0.75; 9.33 s |

Other numbers:
- **Release drain** 1.5/s: a full bar (6) reaches 0 in 200 ticks (4.0 s); 5.4 -> 1.8 (bar 0.9 -> 0.3) takes 120 ticks (2.4 s).
- **Assist, small, reel into a run:** 0.01584 per tick, snap tick 442 (8.84 s).
- **Pity, small, reel into a run:** 0.02244 per tick, snap at 7.5 on tick 335 (6.70 s).
- **Twig, small, reel into a run:** 0.033 per tick, snap tick 213 (4.26 s).
- **Pull wave:** over any 4 s window, sum(p) x dt = 4.04 exactly, whatever the phase. That makes these medium-fish checks phase-independent:
  - Countering drags the fish back: medium, line 12, lateral 0.5, 200 ticks.
    - No input: lateral 0.8858, tire -0.4.
    - Counter-steer only: lateral 0.4382, tire 5.0, strain 2.4.
  - A back pull takes line: medium, 200 ticks, line 12.
    - No reel: 12 + 1.3 x 4.04 = 17.252.
    - Reeling (tire < 0): 12 + 0.65 x 4.04 - 0.6 x 4 = 12.226.
- **Scratch-model bot predictions** (Python mirror of the F2 code, 400 fights per cell, casts Uniform(10, 14) m). The Pcg32 stream will differ, hence the wide F4 bands.

**Rust-faithful reference** (no quirks: every profile rests 2-4 s, runs 3-7 s, back 0.39). This is what the tiers alone give, and what the ±25% rule is measured against:

| Rod | Tier | Greedy snap% / median snap s | Reckless snap% | Pump land% / timeout% | Rhythm median / p90 s |
|---|---|---|---|---|---|
| Handmade | small | 100 / 5.34 | 86 | 99 / 1 | 17.6 / 28.4 |
| Handmade | medium | 100 / 4.26 | 100 | 71 / 29 | 22.9 / 37.5 |
| Handmade | large | 100 / 3.40 | 100 | 30 / 69 | 29.0 / 42.8 |
| Twig | small | 100 / 4.26 | 99 | 95 / 4 | 17.2 / 27.0 |
| Twig | medium | 100 / 3.40 | 100 | 46 / 54 | 25.1 / 36.8 |
| Twig | large | 100 / 2.72 | 100 | 20 / 79 | 33.1 / 44.3 |

**With the quirks (what ships).** Rhythm landed 100% with 0% snaps and 0% timeouts in every cell. "vs faithful" is the change from the tier reference above, and stays within ±25%.

Handmade:

| Profile | Rhythm median / p90 s (vs faithful) | Back-pull share of runs | Mean run s | Mid-run switches / run-s | Greedy snap% / median (mean) s | Reckless snap% | Pump land% / timeout% |
|---|---|---|---|---|---|---|---|
| Steady | 14.3 / 20.4 (-19%) | 15% | 3.66 | 0.00 | 100 / 7.38 (7.29) | 74 | 100 / 0 |
| Darter | 19.2 / 25.0 (-16%) | 14% | 2.21 | 0.78 | 100 / 5.32 | 100 | 100 / 0 |
| Thrasher | 18.6 / 28.3 (-19%) | 18% | 3.20 | 0.33 | 100 / 4.74 | 100 | 98 / 2 |
| Bulldog | 32.7 / 45.8 (+13%) | 52% | 2.60 | 0.00 | 100 / 3.40 | 100 | 8 / 92 |
| Runner | 29.7 / 45.2 (+2%) | 27% | 4.34 | 0.00 | 100 / 3.40 | 100 | 48 / 52 |

Twig:

| Profile | Rhythm median (vs faithful) | Back share | Mean run s | Switches / run-s | Greedy median s | Pump land% / timeout% |
|---|---|---|---|---|---|---|
| Steady | 14.7 (-15%) | 13% | 3.59 | 0.00 | 4.88 | 100 / 0 |
| Darter | 21.6 (-14%) | 12% | 2.13 | 0.78 | 4.34 | 90 / 10 |
| Thrasher | 22.8 (-9%) | 17% | 3.02 | 0.34 | 3.40 | 90 / 10 |
| Bulldog | 33.6 (+2%) | 44% | 2.44 | 0.00 | 2.72 | 3 / 97 |
| Runner | 32.8 (-1%) | 23% | 3.91 | 0.00 | 2.72 | 31 / 69 |

How each pair tells apart:
- **Darter vs Thrasher:** the switch rate (0.78 vs 0.33 per run-second), the run length (2.2 vs 3.2 s), and Thrasher's per-run speed jitter and 1.5 s surges.
- **Bulldog vs Runner:** the back-pull share (52% vs 27%), the run length (2.6 vs 4.3 s), Bulldog's line creep at rest and Runner's ramp-up.
- **Steady vs the rest:** small tier (slowest, lightest strain), no switches, 15% back pulls, 3.7 s runs that ease in.
- **Steady vs Runner:** tier (side speed x0.8 vs x1.25, strain x0.8 vs x1.25), run length (3.7 vs 4.3 s, nominal 3-5 vs 5-9) and back share (15% vs 27%).

Mean run length is measured under the Rhythm bot, so the tire shortens it from the nominal range.

---

## 1. Rules for every task

**Read first:** `CLAUDE.md`, contract C 2.8 (global constraints), section 0 of this file, and your task. For F2 also read research note section 8.

**Constraints that bite here:**
- **Core is pure C#.** `FishGame.Core` has `noEngineReferences`: no `UnityEngine`, no `Mathf`; use `System.Math`.
- **Randomness:** only the sim's `Pcg32`; never `UnityEngine.Random` or `System.Random`.
- **ASCII only** in code and comments. Scan before every commit: `grep -nP '[^\x00-\x7F]' <files>` must print nothing.
- **Tool discipline:**
  - Create and edit files ONLY with the Write and Edit tools, never with sed, python or heredocs. This applies to the `.asset` YAML files too.
  - Run `Tools/unity.ps1` ONLY through the PowerShell tool, in the foreground, with `timeout: 600000`.
  - A red run that is expected says "expected red" in its description.
- **Unity lock:** before any Unity run, the wrapper checks the editor lock. If it prints `LOCKED` (exit 2), stop and report. Never kill Unity and never delete the lockfile.
- **Logs and results** go in `$env:FG_OUT` (your scratchpad), never in the repo.
- **After every Unity run:** `git restore Assets/_Project/Scenes/*.unity` (scene churn). Delete any `Assets/InitTestScene*.unity` and any untracked `dev/null/` folder.
- **`.meta` files:** Unity generates the `.meta` of new files on the first batch run; commit them. A deleted script's `.meta` is deleted with `git rm`.
- **Commits:**
  - Explicit paths only. One conventional commit per task, with the message given in the task.
  - End the message with the Co-Authored-By line of the session's attribution reminder.
  - Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx`.
- **Mutation rule:** for each new test, name in a comment the wrong implementation it catches (`// Catches: ...`). For the key formulas (strain table, steer drop, tire, draw order), break the code once, show the test fails, then restore it. Tests that only assert "no exception", or that compare the code with itself, do not count.
- **Failing hand checks:** if a hand-check test fails, do not widen its tolerance. Recheck the tick order against the code block and report the numbers.
- **Failing bot targets (F4):** do not retune Rust values. Report the measured numbers; the controller decides.

**Commands** (`<scratch>` = your absolute scratchpad path):
```powershell
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play
```
- **Expected last lines:** red = `FG RESULT test-edit FAIL ...` (compile errors that name the missing members count as red); green = `FG RESULT ... PASS total=N passed=N failed=0 ...`. The GPU wave-parity test is skipped in batch, which is expected.
- **Full `check`** exceeds the 10-minute foreground limit. Run it with `run_in_background` only when a task says so, and wait for the notification.

**TDD cadence:**
1. Write the task's tests.
2. Do one red run with the task filter.
3. Implement.
4. Do one green run with the same filter.
5. Run the full `test-edit` once (plus `test-play` where the task says so) before committing.

**Index map (ordinal sort, unchanged):**
- Rods: `rod_carbon` 0, `rod_deepsea` 1, `rod_handmade` 2, `rod_twig` 3.
- Profiles: `bulldog` 0, `darter` 1, `runner` 2, `steady` 3, `thrasher` 4.
- Species: `bluegill` 0, `carp` 1, `catfish` 2, `perch` 3, `trout` 4.

## 2. Task list

| Task | Title | Implementer | Reviewer | Depends |
|---|---|---|---|---|
| F0 | Spec R72 + 9.1-9.13 rewrite, contract row | Sonnet | Sonnet | - |
| F1 | Data: add the Rust fight fields (additive) | Sonnet | Sonnet | F0 |
| F2 | Core: Rust-faithful `FightSim`, host floor and timeout | Sonnet | Opus (high) | F1 |
| F3 | Data: Rust values in the shipped content | Sonnet | Sonnet | F2 |
| F4 | Bots: realistic casts, Greedy / Reckless / Pump / Rhythm | Sonnet | Opus (high) | F3 |
| F5 | Core view math, creak curve, wire flags | Sonnet | Sonnet | F2 |
| F6 | Presentation: creak fade, bobber tilt, HUD gauge | Sonnet | Opus (high) | F5, F3 |
| F7 | Cleanup: remove retired fight data and constants | Sonnet | Sonnet | F4, F6 |
| F8 | Docs: measured numbers, contract commits, acceptance, handoff | Sonnet | Sonnet | F7 |

Every task leaves `test-edit` (and `test-play` from F2 on) green. F2 deletes the old bot target tests and F4 adds the new ones, so the bots never run against a half-switched model.

---

## Task F0 - Spec and contract text (docs only)

- **Goal:** the spec describes the R72 baseline before any code changes, so reviewers judge code against it.
- **Files:** `docs/superpowers/specs/2026-10-09-fishgame-design.md`, `docs/superpowers/plans/2026-10-11-m2-interfaces.md`.
- **No Unity run.** Edit with the Edit tool only. Spec text may use the spec's existing typography; keep the code blocks ASCII.

### F0.1 Amendment log row (spec 3.2; append after the R71 row)

```markdown
| R72 | Fight rework: Rust-faithful baseline (2026-10-12) | The fight is rebuilt on Rust's server maths (research note 2026-10-12 section 8) as a baseline the user plays before a friendlier tuning pass. **Strain is input-only:** with no input it drains at a flat 1.5/s; any input loads it at k x strainMod x 0.75 / lineStrength per second, with k = 0.5 while the fish rests, 2.2 reeling into a run, 2.5 reeling into a back pull, 0.8 counter-steering only, 0.5 other steering. The bar shows strain / 6; the line snaps above 7 (a headroom band above the full bar replaces the snap grace). **Fish:** a run starts at once on the hook and lasts Uniform(burstMin, burstMax) x (1 - tire01); a rest lasts Uniform(calmMin, calmMax) x (1 + tire01); a run goes left or right 50/50 and also pulls back with the profile's diveChance while the line is inside castMaxDist - 2 m; the side flips at the 60 deg cone edge; a pull wave (0.2..2, Rust period 4 s) scales its speed. **Five fight styles are kept (user, 2026-10-12) as data quirks on top of Rust's rules; they change only the fish's movement, never the strain formula:** Steady short plain runs that ease in (rest 2-4, run 3-5, back 0.15, ramp 0.6 s); Darter quick darts that switch sides mid-run (rest 1-2, run 2-3.5, back 0.15, flips 1.0/s at least 0.3 s apart); Thrasher jerky surges (rest 1.5-3, run 3-5, back 0.20, flips 0.4/s, per-run speed x0.7..1.3, 1.5 s wave); Bulldog dives and creeps (rest 2-4, run 2.5-4.5, back 0.55, line creeps out 0.06 m/s at rest); Runner long sideways runs that build up (rest 2-3.5, run 5-9, back 0.30, ramp 0.8 s). Rust's own values (rest 2-4, run 3-7, back 0.39, no quirk) remain the reference the hand checks use. **Steering:** digital, and only toward the cast line; a counter-steer halves the side run and pushes the fish back at the rod's steerRate (Handmade 0.8 m/s); steering tires the fish (+1.25/s countering a plain side run, +0.8/s otherwise, -0.1/s with no steer); reeling never tires; reel speed = reelSpeed x reelInMult x (1 rising to 3 as tire goes 0 to 5). **Endings:** landed at 1.0 m from the rod tip while reeling; snapped above strain 7; Escaped(Timeout) at 120 s (no pinned escape; the 12 s slack escape applies to junk only). Profiles take Rust's tiers (strainMod / moveMult / reelInMult): Steady 0.8/0.8/1.2; Darter and Thrasher 1/1/1; Bulldog and Runner 1.25/1.25/0.9. Weight does not enter the fight. Assist x0.6 on strain gains; the bad-luck nudge x0.85 on gains and x1.5 on the headroom (snap at 7.5). Handmade reels 0.6 m/s and steers 0.8 m/s; other rods keep their ratios. Feedback: creak from 0.10 of the bar on Rust's gain curve with a 0.25 s fade-out; HUD gauge smoothed at clamp01(dt x 10) and drawn to the snap point; bobber roll +-30 deg toward the pull, halved on a back pull. **Supersedes:** the 0.8 s calm lead-in, burst-only strain, relax while reeling, counter relief, steer/out-of-window/spool strain, the snap grace timer, the pinned and fish slack escapes, the weight factor, fish stamina and its regen, and the pre-R72 motion formulas (R63's characters live on as the quirks above). **Not copied:** PlayerMoved (walking while waiting stays), BadAngle, Obstructed, TooFarAway (the line clamps at maxLineOut), rod condition, tea boost, server authority. Authority is unchanged (owner sim, host validation, 9.9); the host Landed floor uses the fastest possible reel and the host accepts an owner Timeout only on its own clock. | 9.1, 9.2, 9.5-9.9, 9.11-9.13, 14.8, 17.2; R63, R64, R65 |
```

Under the 3.2 table, change the contract line to: `Contract details for R61-R65: ... (unchanged text) ... R72: plan docs/superpowers/plans/2026-10-12-m2-rework-fight.md and C 16.1 row R72.`

Append ` (R72: kept as per-profile quirks layered on Rust's rules - timing, back-pull chance, mid-run flips, ramp, rest creep, per-run speed jitter, wave period; spec 9.5/9.6.)` to the end of the R63 decision cell, and ` (R72: in display units, tension = strain / 6.)` to the R64 cell. In R65, change "the snap grace lasts longer (x1.5)" to "the snap grace lasts longer (x1.5; R72: the headroom above the full bar is x1.5, snap at strain 7.5)".

Change the 9.0 intro line "Facepunch publishes no fishing numbers. Every value here is a starting point;" to "The fight uses Rust's server numbers (R72, research note 2026-10-12 section 8); every other value is a starting point;".

### F0.2 Section 9.1 (targeted edits)

- **Goals:** replace "a forgiving rhythm (steer against the run, reel when the fish calms)" with "the Rust pump (counter-steer the runs, reel while the fish rests, let go to drain)".
- **Glossary row "Lateral":** `Fish position across the line, -1..+1, **+1 = right**. Angle off the cast line = lateral x lateralMaxDeg (60 deg, Rust's cone). The fish moves in m/s; a sideways speed v changes lateral by v x dt / (max(lineLength, 1 m) x lateralMaxDeg in radians)`.
- **Glossary row "Tension":** `Strain / tension | Strain is the line load (Rust units): the bar is full at strainBarFull 6 and the line snaps above strainSnap 7. Tension = strain / 6 is the displayed value (1.0 = full bar; snap above 7/6)`.
- **Glossary row "P":** `Pull wave p | 0.2..2: a 4 s triangle (Rust PingPong(t, 2) clamped) that scales the fish's speed; its phase is drawn per fight`.
- **Glossary row "Burst / Calm":** `Run / rest (Burst / Calm) | A run pulls left or right (burstDir +-1) and, 39% of the time, also back; at rest the fish does not move`.
- **New glossary row:** `Tire | Rust's TireAmount: +1.25/s countering a plain side run, +0.8/s any other steer, -0.1/s without steer (no floor). It shortens runs, lengthens rests (saturating at 20) and speeds the reel (x1..x3 over 0..5)`.

### F0.3 Section 9.2 table rows

- **Replace the Hooked snap row** with: `| Hooked | strain > snap strain (strainSnap 7; 7.5 on a Pity cast) | Snapped | ResultRpc(Snapped); RodEventRpc(Snapped, sizeClass) |`
- **Replace the pinned row** with: `| Hooked | elapsed >= fightTimeout (120 s) | Escaped (Timeout) | ResultRpc(Escaped, Timeout); the host checks it on its own clock (9.9) |`
- **Replace the slack row** with: `| Hooked (junk only) | slackTimer >= slackEscapeTime | Escaped (ThrewHook) | An AFK junk hook clears itself; a fish fight ends by fightTimeout instead (R72) |`

### F0.4 Section 9.5 (replace the whole section, from its heading to the line before "### 9.6")

````markdown
### 9.5 The fight (`FightSim`, FishGame.Core) - Rust-faithful baseline (R72)

The fight runs Rust's server maths (research note 2026-10-12 section 8: mechanics and numbers only). It is a baseline the user plays before a friendlier tuning pass. Mappings onto FishGame's world are marked [FG].

```csharp
public enum FightPhase : byte { Calm, Burst }        // Calm = the fish rests (Rust state 0); Burst = a run
public enum FightOutcome : byte { None, Landed, Snapped, Escaped, Cancelled }
public enum EscapeReason : byte { None, Missed, Pinned, ThrewHook, Dropped, Timeout, Invalid, Disconnected }   // Pinned is no longer produced
public struct FightInput { public float steer; public bool reel; }
public struct FightState {
    public float lineLength, lateral, strain, tension, tire;   // tension = strain / strainBarFull (display)
    public FightPhase phase; public float phaseTimer; public sbyte burstDir; public bool back;
    public float snapTimer, slackTimer, elapsed;                 // snapTimer = seconds over the full bar
    public int nearSnapRecoveries; public float maxTension;
    public FightOutcome outcome; public EscapeReason reason;
}
public sealed class FightSim {
    public const float Step = 0.02f;
    public FightSim(in FishFightProfile fish, in RodStats rod, in FishingTuning tuning, float weightT, uint seed, float startLineLength, bool assist, bool pity = false);
    public FightOutcome Tick(in FightInput input);
    public ref readonly FightState State { get; }
    public bool Exhausted { get; }       // tire >= tireRunFull
    public float SnapTension { get; }    // snap point in display units (7/6; 1.25 on a Pity cast)
    public float PullWave { get; }       // p of the last tick
}
```

- Own seeded `Pcg32` from `fightSeed`; never `UnityEngine.Random`. The draw order is fixed (plan rework-fight 0.9).
- **Initial state:** `lineLength` = horizontal rod-tip-to-bobber distance; lateral 0; strain 0; tire 0; phase Calm with `phaseTimer` 0, so the **first tick starts a run** (Rust). The pull-wave phase is drawn in the constructor.
- **Weight:** `weightT` does not enter the fight (Rust has no weight term; size is in the profile's tier). The parameter stays for API stability.

**Fish state machine** (at the start of each tick, when `phaseTimer <= 0`; `tire01 = clamp01(tire / tireRunFull)`, tireRunFull 20)
- Run -> rest: rest for `Uniform(calmMin, calmMax) x (1 + tire01)` (up to twice as long when tired).
- Rest -> run: side left or right 50/50; also a back pull when a draw `< diveChance` and `lineLength < rod.castMaxDist - backPullMargin` (2 m) [FG: Rust's gate is its 10 m max cast - 2]; run for `Uniform(burstMin, burstMax) x (1 - tire01)` (shrinking to nothing when fully tired); then, for a profile with `lateralJitter > 0`, `runSpeedMult = Uniform(1 - j, 1 + j)` for this run.
- Rust's values are rest 2-4 s, run 3-7 s, back 0.39; each profile sets its own (9.6).
- Cone edge: a next lateral beyond +-1 is clamped and the run's side flips (Rust flips the side flags at its 60 deg cone).

**Per tick** (quirk terms marked [Q]; with every quirk at its Rust value this is exactly Rust)
```
// [Q] mid-run side flips (Darter, Thrasher): each run tick, once flipMinGap has passed since the last flip (or the run start),
//     a draw < flipRate * dt flips burstDir
p       = clamp(pingpong-shaped wave of period fish.wavePeriod (Rust 4 s) peaking at 2, at wavePhase + elapsed, 0.2, 2)
ramp    = fish.burstRamp > 0 ? clamp01(secondsIntoRun / fish.burstRamp) : 1                // [Q] Steady, Runner
speed   = p * fish.moveMult * ramp * runSpeedMult                                        // [Q] ramp and runSpeedMult; behaviour only
steer   = |input.steer| >= 0.5 ? sign(input.steer) : 0;  if !(steer * lateral < 0) steer = 0   // digital, only toward the cast line [FG frame]
run     = phase == Burst;  back = run && back flag;  counter = run && steer == -burstDir

// sideways (m/s), then lateral at the current radius [FG polar mapping]
v  = run ? burstDir * sideMoveSpeed(1.2) * speed * (counter ? 0.5 : 1) : 0
v += (steer != 0 && !(run && steer == burstDir)) ? steer * rod.steerRate : 0           // the push (Handmade 0.8)
lateral += v * dt / (max(lineLength, 1) * lateralMaxRad);   at +-1: clamp, and a run flips its side

// line
tireReel = 1 + (tireReelMaxMult(3) - 1) * clamp01(tire / tireReelFull(5))
if reel: lineLength -= rod.reelSpeed * fish.reelInMult * tireReel * dt
if back: lineLength += backMoveSpeed(1.3) * speed * (reel ? 0.5 : 1) * dt
else if at rest and fish.calmDrift > 0 and not exhausted:                                // [Q] Bulldog creeps line out at rest
    lineLength += fish.calmDrift * clamp01(secondsIntoRest / fish.calmDriftRamp) * (1 - tire01) * dt
lineLength = clamp(lineLength, 0, rod.castMaxDist + spoolExtra)                         // no strain at the clamp

// tire (only steering tires the fish)
tire += steer != 0 ? (counter && !back ? 1.25 : 0.8) * dt : -0.1 * dt                   // no floor

// strain
if reel or steer != 0:
    k = !run ? 0.5 : reel ? (back ? 2.5 : 2.2) : counter ? 0.8 : 0.5
    strain += k * fish.strainMod * strainScale(0.75; assist 0.45) * (pity ? 0.85 : 1) / rod.lineStrength * dt
else:
    strain = max(0, strain - relaxRate(1.5) * rod.relaxMult * dt)                     // full release drains
tension = strain / strainBarFull(6)
```

**Endings** (same-tick priority Landed > Snapped > Timeout)
- **Landed:** `lineLength <= landDistance` (1.0 m from the rod tip) while reeling on that tick [FG: Rust lands within 1.0 m of its cast origin, the player; ours is the rod tip, R67].
- **Snapped:** `strain > strainSnap` (7). On a Pity cast the snap strain is `6 + (7 - 6) x 1.5 = 7.5`. There is no grace timer: the band from 6 to 7 (bar full, no snap yet) is the warning, and `snapTimer` counts the seconds spent in it (FlagGrace, crack).
- **Escaped(Timeout):** `elapsed >= fightTimeout` (120 s).
- There is no pinned or slack escape for a fish. Junk keeps its slack escape (9.4).
- `nearSnapRecoveries` counts tension going above 1.0 (over the full bar) and later below 0.5.

The lessons fall out of the rules:
- **Pump:** any input loads the line and only letting go drains it.
- **Counter the runs:** countering is cheaper than reeling into a run (0.8 vs 2.2). It halves the run, drags the fish back toward the cast line and tires it, which shortens its runs and triples the reel.
- **Let the back pulls go:** reeling into one is the most expensive input (2.5).
- **Greed snaps:** a large fish snaps in 3.4 s of reeling into a run, a small one in 5.3 s if a run lasts that long. Steady's runs are 3-5 s, so in play a bluegill needs about 7 s of reckless reeling, with strain carried across runs.

Quirks never touch the strain formula, the tire or the reel: they change how the fish moves, so each style reads differently while the Rust lessons hold.

**Hand-worked checks** (Handmade, strain from 0; effective multiplier strainMod x 0.75 = 0.6 / 0.75 / 0.9375; quirk-free Rust timing):

| Action | small (Steady) | medium (Darter, Thrasher) | large (Bulldog, Runner) |
|---|---|---|---|
| Reel into a side run (7 / rate) | 5.3 s | 4.2 s | 3.4 s |
| Reel into a back pull | 4.7 s | 3.7 s | 3.0 s |
| Reel while the fish rests | 23 s | 18.7 s | 14.9 s |
| Counter-steer only | 14.6 s | 11.7 s | 9.3 s |

- **Release:** a full bar drains to 0 in 4.0 s; 90% -> 30% of the bar takes 2.4 s.
- **Countering a medium fish for one 4 s wave at 12 m:** brings it from lateral 0.5 to 0.44 and tire to 5 (reel x3). Left alone, it reaches 0.89.
- **Assist** snaps a small fish's reckless run at 8.8 s; **Pity** at 6.7 s.
````

### F0.5 Section 9.6 (replace from its heading to the line before "### 9.7")

````markdown
### 9.6 Fight profiles and M2 placeholder species

Five shared `FightProfileDef` assets (R3). M2 has one placeholder species per profile; M3's roster references the same five assets. R72: each profile takes one of Rust's three fish tiers (the values that enter the strain formula), plus its own quirks that shape how it moves (user decision 2026-10-12: keep the five styles, each recognisable blind).

| Profile | Seeded from | Rust tier | strainMod | moveMult | reelInMult |
|---|---|---|---|---|---|
| Steady | Bluegill | small | 0.8 | 0.8 | 1.2 |
| Darter | Perch | medium | 1.0 | 1.0 | 1.0 |
| Thrasher | Trout | medium | 1.0 | 1.0 | 1.0 |
| Bulldog | Carp | large | 1.25 | 1.25 | 0.9 |
| Runner | Catfish | large | 1.25 | 1.25 | 0.9 |

**Quirks** (behaviour only; Rust's values are rest 2-4, run 3-7, back 0.39, no flip, no ramp, no drift, no jitter, wave 4 s):

| Profile | rest s | run s | back (diveChance) | flipRate /s (flipMinGap s) | burstRamp s | calmDrift m/s (ramp s) | lateralJitter | wavePeriod s | Character |
|---|---|---|---|---|---|---|---|---|---|
| Steady | 2-4 | 3-5 | 0.15 | 0 (0.5) | 0.6 | 0 | 0 | 4 | Tutorial: short plain side runs that ease in; rarely dives |
| Darter | 1-2 | 2-3.5 | 0.15 | 1.0 (0.3) | 0 | 0 | 0 | 4 | Quick darts that switch sides mid-run |
| Thrasher | 1.5-3 | 3-5 | 0.20 | 0.4 (0.5) | 0 | 0 | 0.3 | 1.5 | Jerky: fast surges, each run a different speed, sometimes flips |
| Bulldog | 2-4 | 2.5-4.5 | 0.55 | 0 (0.5) | 0 | 0.06 (2.5) | 0 | 4 | Dives a lot and creeps line out even at rest |
| Runner | 2-3.5 | 5-9 | 0.30 | 0 (0.5) | 0.8 | 0 | 0 | 4 | Long sideways runs that build up |

Signatures under good play (scratch model, Handmade): back-pull share of runs Steady 15%, Darter 14%, Thrasher 18%, Bulldog 52%, Runner 27%; mean run 3.7 / 2.2 / 3.2 / 2.6 / 4.3 s; mid-run side switches 0 / 0.78 / 0.33 / 0 / 0 per run-second. The tuning bots pin these (9.12).

**M2 placeholder species** (`FishSpeciesDef`):

| id | Profile | kg min-max | baseValue | biteWaitMod | minBaitRating | minDepth m |
|---|---|---|---|---|---|---|
| `bluegill` | Steady | 0.2-0.8 | 5 | 0.9 | 1 | 0.8 |
| `perch` | Darter | 0.3-1.2 | 8 | 1.0 | 3 | 1.2 |
| `carp` | Bulldog | 1.0-4.0 | 12 | 1.2 | 1 | 1.5 |
| `trout` | Thrasher | 0.6-2.5 | 20 | 0.9 | 5 | 2.0 |
| `catfish` | Runner | 2.0-7.0 | 35 | 1.4 | 6 | 2.5 |

Junk (`old_boot`, `tin_can`): 0 kg, 2 coins.

M2 species also fill the 12.3 presentation fields (`displayName`, `bodyShape`, `colorA`/`colorB`, `weightSkew` 2.2, `lengthShapeK` 1.0). The M2 catch reveal shows each species' Blender model (R62); the procedural fish mesh built from these fields is the fallback when a model is missing.

**Baseline fight lengths** (Rhythm bot, Handmade, realistic casts of 10-14 m from the tip, weights drawn like the game): median Steady about 14 s, Darter and Thrasher about 19 s, Runner about 30 s, Bulldog about 33 s. Each tier stays within ±25% of Rust's quirk-free lengths (17 / 23 / 29 s). Bands are in 9.12; plan rework-fight F8 records the measured numbers. These are Rust-like lengths, shorter than the pre-R72 targets; the user keeps them for the baseline, and the friendlier pass retargets them.
````

### F0.6 Section 9.7 (targeted edits)

- **First paragraph:** after "`relaxMult`," insert: "(R72: `reelSpeed` is the reel speed in m/s before the fish's `reelInMult` and the tire multiplier; `lineStrength` divides every strain gain; `relaxMult` multiplies the 1.5/s release drain; `steerRate` is the sideways push in m/s; `angleWindow` is parked, unused by the baseline)".
- **Replace the rod table rows:**

```markdown
| `rod_twig` | 0 | 14 | 0.54 | 0.8 | 0.50 (parked) | 0.67 | 0.9 | Starter kit (every run) |
| `rod_handmade` | 1 | 18 | 0.6 | 1.0 | 0.60 (parked) | 0.8 | 1.0 | 120 coins, from Night Camp 1 |
| `rod_carbon` | 2 | 24 | 0.72 | 1.3 | 0.70 (parked) | 0.93 | 1.1 | 300 coins, from Night Camp 2 |
| `rod_deepsea` | 3 | 30 | 0.78 | 1.6 | 0.75 (parked) | 1.07 | 1.2 | 600 coins, from Night Camp 4 |
```

- **Append to the paragraph after the table:** "R72: Handmade is Rust's rod (reel 0.6 m/s, steer push 0.8 m/s); the other rods keep their pre-R72 ratios to Handmade."

### F0.7 Section 9.8 (targeted edit)

Replace the clean-catch bonus bullet with:

```markdown
- **Clean-catch bonus (R64):** a Landed fish whose fight `maxTension < cleanCatchMaxTension` (`FishingTuningDef`, seed 0.8 of the bar, i.e. strain 4.8; R72 display units, `maxTension` may exceed 1) is raised one quality tier (cap Trophy) and its value recomputed with the new `qMult`. The owner's `ResultRpc` carries `maxTension`, `fightSeconds` and `nearSnapRecoveries` (a recovery means the bar went over full, so it is never clean); the host accepts the claim only within `CatchValidator.CleanClaimAccepted` bounds (9.9), and a rejected claim keeps the rolled quality without invalidating the catch.
```

### F0.8 Section 9.9 (targeted edits)

- **Result bullet:** replace "`Landed` requires `now − tHooked ≥ 0.8 × (lineLength0 − landDistance) / rod.reelSpeed − 0.5 s` (physical minimum on the host clock; junk uses `junkDragSpeed`;" with "`Landed` requires `now − tHooked ≥ 0.8 × (lineLength0 − landDistance) / maxReel − 0.5 s`, `maxReel = rod.reelSpeed × profile.reelInMult × tireReelMaxMult` (R72: the fastest the line can come in, a fully tired fish; physical minimum on the host clock; junk uses `junkDragSpeed`;".
- **Same bullet**, after the Landed sentence, insert: "An owner `Escaped(Timeout)` is accepted only when `now − tHooked ≥ fightTimeout − 2 s` (`HostTimeoutSlackSeconds`; the owner's sim ends a fish fight at 120 s), otherwise it resolves Escaped(Invalid)."
- **Timeouts bullet:** replace "stuck in Hooked after `maxFightTime` 240 s → Escaped(Timeout) and commit" with "stuck in Hooked after `maxFightTime` 240 s → Escaped(Timeout) and commit (a fallback: the owner reports Timeout at `fightTimeout` 120 s)".

### F0.9 Section 9.11 (targeted edits)

- **Before the tension table**, add the line: "Tension below is the display value, strain / 6 (1.0 = full bar; the line snaps above 7/6)."
- **Replace the "0.6-0.8" row** with: `| 0.6-0.8 | Line warms in colour; creak pitch rises from 0.6 |`
- **Insert after the "0.3-0.6" row:** `| > 0.10 | Creak loop (Rust's strainGainCurve): silent at <= 0.10, volume = CreakGain(tension) rising steeply to 1 at 0.95; stops with a 0.25 s fade when tension falls to 0.10 |`
- **Replace the ">= 1.0 (grace)" row** with: `| > 1.0 (over the full bar, strain > 6) | Crack SFX on entry, line shimmer, gauge blinks red; the line snaps above 7/6 (7.5/6 on a Pity cast) |`
- **Phase cues paragraph:** replace "dive = bobber plunges, line points down, deep thrum, no sideways splash; calm = ripples only; exhausted = the fish rolls on the surface." with "back pull (dive) = bobber plunges, line points down, deep thrum, no sideways splash; rest = ripples only; spent (tire >= 20) = the fish rolls on the surface. The bobber rolls +-30 deg toward the pull side (halved on a back pull), eased at clamp01(dt) per frame (Rust)."
- **Accessibility, Fishing assist:** replace "`snapGrace` 0.75 s, `strainScale` 0.6" with "strain gains x0.6 (`strainScaleAssist` 0.45 = 0.75 x 0.6; drain and headroom unchanged)", and replace "The assist flag is sent with each cast so the host validator uses the matching grace." with "The assist flag is sent with each cast (telemetry)."
- **Accessibility, Line gauge:** replace "HUD arc below the crosshair (90°, tick at 0.8) or a thin bar" with "HUD arc below the crosshair (90°) or a thin bar showing tension = strain / 6 smoothed by clamp01(dt x 10) per frame (Rust); the track runs to the snap point (7/6) with a full mark at 1.0, a tick at the clean threshold (0.8) and a red headroom zone above 1.0".

### F0.10 Section 9.12 (tuning table and bot targets)

**Tuning table:**
- **Replace these rows:**

```markdown
| `landDistance` | 1.0 m (from the rod tip; R72) | 0.5-3 |
| `relaxRate` | 1.5 strain/s (release drain; Rust) | 0.5-3 |
| `strainScale` | 0.75 (Rust GlobalStrainSpeedMultiplier; assist 0.45) | 0.3-1.5 |
| `lateralMaxDeg` | 60° (Rust cone) | 60-85 |
```

- **Delete these rows:** `reelStrainBurst`, `counterRelief`, `steerStrain`, `outOfWindowStrain`, `spoolStrain`, `relaxWhileReeling`, `reelBurstPenalty`, `diveRunMult`, `snapGrace`, `pinEscapeTime`, `fishStaminaRegen`, `calmCenterPull`.
- **Annotate these rows:** `spoolExtra` "12 m (maxLineOut only; no strain)"; `slackEscapeTime` "12 s (junk only, R72)"; `maxFightTime` "240 s (host fallback; owner Timeout at 120 s)".
- **Add these rows:**

```markdown
| `strainBarFull` / `strainSnap` | 6 / 7 (Rust) | 1-20 / 1-30, snap > bar |
| `strainRestInput` | 0.5 /s (any input while the fish rests) | 0-3 |
| `strainReelRun` / `strainReelBack` | 2.2 / 2.5 /s | 0-6 |
| `strainCounterSteer` / `strainOtherSteer` | 0.8 / 0.5 /s | 0-3 |
| `sideMoveSpeed` / `backMoveSpeed` | 1.2 / 1.3 m/s | 0-5 |
| `tireSteerRate` / `tireCounterRate` / `tireDecayRate` | 0.8 / 1.25 / 0.1 /s | 0-5 / 0-5 / 0-1 |
| `tireRunFull` / `tireReelFull` / `tireReelMaxMult` | 20 / 5 / 3 | 1-100 / 0.5-50 / 1-5 |
| `backPullMargin` | 2 m | 0-10 |
| `fightTimeout` | 120 s | 30-300, < maxFightTime |
```

**Bot targets.** Replace the "Bot targets" table and the "Also: ..." paragraph with:

```markdown
**Bot targets (R72 baseline; Handmade and Twig; 200 fights per cell; casts Uniform(10, 14) m from the tip; weightT = u^2.2):**

| Bot | Plays | Target |
|---|---|---|
| Greedy | always reels, never steers | snaps >= 95% in every cell; Handmade Steady median snap time 5.5-9.0 s (model 7.4; Steady's runs never reach Rust's 5.3 s single-run snap); Handmade Runner 2.8-3.8 s (Rust 3.4) |
| Reckless | always reels, counter-steers every run | snaps >= 95% for Darter, Thrasher, Bulldog and Runner; Steady >= 50% (model 74% Handmade, 100% Twig) |
| Pump | never steers; reels until tension 0.85, lets go until 0.30 | snaps <= 5% in every cell; Handmade Steady lands >= 85%; Handmade Bulldog times out >= 60%, Runner >= 30% (big fish need steering) |
| Rhythm | counter-steers runs without reeling, reels while the fish rests (steering it to the cast line), lets go at 0.85 until 0.30 | lands >= 95%, snaps <= 2%, times out <= 2% in every cell. Median Handmade: Steady 10-20 s, Darter 13-27, Thrasher 13-27, Runner 20-41, Bulldog 23-44. Twig: Steady 10-20, Darter 15-30, Thrasher 16-31, Runner 22-44, Bulldog 23-45 |
| Signatures (Rhythm, Handmade) | the five styles stay distinct | back-pull share of runs: Bulldog >= 40% and the highest; Runner 15-40%; Steady, Darter and Thrasher <= 25%. Mean run: Runner >= 3.8 s; Steady 3.0-4.4; Thrasher 2.6-3.8; Darter <= 2.7; Bulldog <= 3.1; ordered Runner > Steady > Thrasher > Darter. Mid-run side switches per run-second: Darter >= 0.5; Thrasher 0.15-0.55; Steady, Bulldog and Runner <= 0.05 (cone-edge flips only) |

The bands are wide because the scratch model behind them used a different RNG. A miss is reported with its numbers and not retuned away from Rust's values. Weight cannot change a baseline fight; the bots draw it only so that a later weight term is measured at real weights.
```

### F0.11 Section 9.13 (targeted edits)

- **"Fish takes all the line" row:** `| Line all out | Clamped at maxLineOut with no strain (strain is input-only, R72). Back pulls stop inside castMaxDist - 2 m, so this is rare (Rust fails at twice its cast range instead) |`
- **"Alt-tab mid-fight" row:** replace "Neutral input → tension relaxes" with "Neutral input → strain drains at 1.5/s while the fish keeps running; the fight times out at 120 s".

### F0.12 Other spec lines

- **14.8 skill table, Steady Nerves:** `| Steady Nerves | snap headroom x1.15 [tune] (R72: the snap grace became the headroom above the full bar) | FightSim snap strain | M4 |`.
- **17.2 "EditMode, fight (forced phases)" bullet:** "**EditMode, fight (R72, forced states):** Rust's hand-worked times for reeling into a run, into a back pull and at rest per tier; the counter-steer rate; the 1.5/s drain; the bar full at 6 and the snap above 7; run and rest durations scaled by tire; side 50/50 and back 39% inside the gate; the draw order replayed by hand; the pull wave; countering drags the fish back and tires it; the steer drop rule; the cone flip; the tire reel multiplier; back pulls take line; landing at 1.0 m while reeling; Timeout at 120 s; same-tick priority; weight never changes a fight; same seed and inputs give an identical state; assist and Pity numbers; near-snap recovery; junk."
- **17.2 bots bullet:** "Greedy, Reckless, Pump and Rhythm bots, 200 fights per cell at casts of 10-14 m, per profile and per rod (Twig and Handmade); assert the 9.12 targets; print fight-length, snap, timeout, clean and recovery distributions."
- **17.2 fun gate, "Median fight" line:** "Median fight: recorded for the R72 baseline (bots about 14 s Steady, 19 s Darter/Thrasher, 30-33 s Runner/Bulldog at 10-14 m); the user sets new targets in the friendlier pass."
- **17.2 fun gate, near-snap line:** replace "≥ 40% of Bulldog/Thrasher/Runner fights include a near-snap recovery" with "≥ 40% of Bulldog/Thrasher/Runner fights include a near-snap recovery (the bar went over full and came back under half)".
- **17.2 fun gate, "Testers can tell the five profiles apart blind":** unchanged. The R72 quirks (9.6) are what make this hold.

### F0.13 Contract (`2026-10-11-m2-interfaces.md`)

- **Directly under the heading `### 7.7 T7 - Fight`**, insert: `> **R72 (2026-10-12):** this block is superseded by plan `2026-10-12-m2-rework-fight.md` (F2 code, F5 view math). See C 16.1 row R72.`
- **Under `### 7.15 T15 - Tuning bots`**, insert: `> **R72:** bots and targets replaced by plan rework-fight F4.`
- **Append to the C 16.1 table:**

```markdown
| R72 fight rework (plan `2026-10-12-m2-rework-fight.md`, F0-F8) | `FightSim` rebuilt on Rust's server maths (spec 9.5, R72). Five fight styles kept as data quirks on the Rust tick (user, 2026-10-12): per-profile rest/run ranges and back chance, mid-run flips (`FlipRate`/`FlipMinGap`), `BurstRamp`, `CalmDrift`/`CalmDriftRamp` (scaled by 1 - tire01), `LateralJitter` (per-run speed multiplier, side and back), new `WavePeriod`. Public surface: `FishFightProfile` + `StrainMod`, `MoveMult`, `ReelInMult`, `WavePeriod`; `FishingTuning` + `StrainBarFull`, `StrainSnap`, `StrainRestInput`, `StrainReelRun`, `StrainReelBack`, `StrainCounterSteer`, `StrainOtherSteer`, `SideMoveSpeed`, `BackMoveSpeed`, `TireSteerRate`, `TireCounterRate`, `TireDecayRate`, `TireRunFull`, `TireReelFull`, `TireReelMaxMult`, `BackPullMargin`, `FightTimeout`, and - the 13 retired fields (F7); `FightState` - `FishStamina`, `PinnedTimer` + `Strain`, `Tire`, `Back` (`Tension` = Strain / StrainBarFull, `SnapTimer` = seconds over the full bar); `FightSim` - `WeightFactor`, `EffectivePull`, `Spooled`, `BurstLateralMult` + `SnapTension`, `PullWave`, internal static `Wave(t, period)`, internal `RunSpeedMult`, `ForcePhase(phase, burstDir, back, duration)`; `RodWireQuant.FlagRight = 64`; `RodControllerRules.HookedFlags(phase, burstDir, back, reeling, exhausted, snapTimer)`; `CatchValidator.MaxReelSpeed`, `CatchValidator.OwnerTimeoutPlausible`; `TensionCues.CreakGain` + `TensionCueState.CreakOn`; new Core `FightViewMath`; `FishingConstants` + `PullWavePeak`, `RustWavePeriod`, `PullWaveFloor`, `SteerDigitalThreshold`, `LateralMinRadius`, `CounterSideMult`, `ReelBackMult`, `SideRunLeftChance`, `HostTimeoutSlackSeconds`, `NearSnapHigh` 0.8 -> 1.0, - retired constants (F7). Ledger: the Landed floor and clean-claim (4) use `MaxReelSpeed`; an owner Escaped(Timeout) is accepted when host elapsed >= FightTimeout - 2 s. Tests: `FightSimTests` rewritten (research 8.7 numbers on quirk-free profiles, plus one `FightSim_Quirk_*` test per quirk), `FightMotionTests` deleted (its role moves to the quirk tests), `FightViewMathTests` added, `FishingBots` retargeted with a five-style signature test. Superseded contract text: C 7.5 (`FishFightProfile`, `FishingTuning`), C 7.7 (whole block), C 7.13 (`FightProfileDef`, `FishingTuningDef` field lists), C 7.15, C 10.1 T7 rows, C 10.2 pinned/slack rows (renamed tests), C 15 B-Q4, B-Q5, B-Q15; M2-D15 re-mapped (R63 motions become R72 quirks with the 9.6 values). B-Q7 (a flip may happen on a run's first tick) still holds. | F1-F7 commits (hashes filled in by F8) |
```

### Steps and commit
1. Apply F0.1-F0.13 with the Edit tool.
2. Read each edited section once and check the tables still render (pipes balanced).
3. Commit with explicit paths: `docs(spec): R72 Rust-faithful fight baseline - rewrite 9.5-9.8, tuning and bot targets; contract 16.1 row`.

### Acceptance (reviewer, Sonnet)
- Every number matches section 0 of this plan.
- R72 lists exactly what it supersedes and what is not copied.
- No other section of the spec changed.

---

## Task F1 - Data: add the Rust fight fields (additive, nothing behaves differently)

- **Goal:** add every new data field with its Rust value to Core, Defs, seeds, assets, test data and validators. Old fields and the old `FightSim` are untouched, so the game behaves exactly as before.
- **Files:**
  - Core: `Assets/_Project/Core/Fishing/FishFightProfile.cs`, `Assets/_Project/Core/Fishing/FishingTuning.cs`.
  - Data: `Assets/_Project/Data/Fishing/FightProfileDef.cs`, `Assets/_Project/Data/Fishing/FishingTuningDef.cs`.
  - Editor: `Assets/_Project/Editor/Builders/Fishing/FishingSeedData.cs`, `Assets/_Project/Editor/Builders/Fishing/FishingContentBuilder.cs`, `Assets/_Project/Editor/Validation/FishingValidators.cs`.
  - Assets: `Assets/_Project/Content/Fishing/FishingTuning.asset`, `Assets/_Project/Content/Fishing/FightProfiles/{steady,darter,bulldog,thrasher,runner}.asset`.
  - Tests: `Assets/_Project/Tests/EditMode/{FishingTestData,FishingDefsTests,FishingContentTests,FishingValidatorTests,ContentRegistryTests}.cs`.

### F1.1 Core (write exactly)

`FishFightProfile.cs`: append after `LateralJitter`:
```csharp
        // R72 Rust tiers (spec 9.6): small 0.8 / 0.8 / 1.2, medium 1 / 1 / 1, large 1.25 / 1.25 / 0.9.
        public float StrainMod;        // multiplies every strain gain (Rust StrainModifier)
        public float MoveMult;         // multiplies the side and back pull speeds (Rust MoveMultiplier)
        public float ReelInMult;       // multiplies the reel speed (Rust ReelInSpeedMultiplier)
        // R72 quirk (spec 9.6): pull-wave period in s (Rust 4); shape and mean unchanged, shorter = faster surges (Thrasher 1.5).
        public float WavePeriod;
```

`FishingTuning.cs`: append after `PityHookWindowBonus` (struct order matters: the Def mirrors it):
```csharp
        // R72 Rust fight baseline (spec 9.5, 9.12; research note 8). Strain is in Rust units: the bar is full at StrainBarFull and the
        // line snaps above StrainSnap. Per-second strain gains k by situation, before x StrainMod x StrainScale / LineStrength:
        public float StrainBarFull, StrainSnap;
        public float StrainRestInput, StrainReelRun, StrainReelBack, StrainCounterSteer, StrainOtherSteer;
        public float SideMoveSpeed, BackMoveSpeed;                                   // m/s at pull wave 1, x MoveMult
        public float TireSteerRate, TireCounterRate, TireDecayRate;                  // /s
        public float TireRunFull, TireReelFull, TireReelMaxMult;                     // run/rest scaling saturates at TireRunFull; reel x1..x3 over 0..TireReelFull
        public float BackPullMargin;                                                 // m: no back pull while lineLength >= castMaxDist - margin
        public float FightTimeout;                                                   // s: Escaped(Timeout)
```

### F1.2 Data Defs

**`FightProfileDef`:**
- Append `[SerializeField] private float strainMod; moveMult; reelInMult; wavePeriod;` (four fields, in that order) after `lateralJitter`.
- Add the four getters.
- Append four parameters `float strainMod, float moveMult, float reelInMult, float wavePeriod` to `Configure` (at the end), assign them, and map them in `ToCore()`.
- Fix every caller (the compile errors name them: `FishingContentBuilder`, `FishingValidatorTests.MakeSeedSet`, `FishingDefsTests`, `ContentRegistryTests`). Callers that only need a valid profile pass `1f, 1f, 1f, 4f`.

**`FishingTuningDef`:**
- Append 17 `[SerializeField] private float` fields after `pityHookWindowBonus`, in the struct's order: `strainBarFull`, `strainSnap`, `strainRestInput`, `strainReelRun`, `strainReelBack`, `strainCounterSteer`, `strainOtherSteer`, `sideMoveSpeed`, `backMoveSpeed`, `tireSteerRate`, `tireCounterRate`, `tireDecayRate`, `tireRunFull`, `tireReelFull`, `tireReelMaxMult`, `backPullMargin`, `fightTimeout`.
- Add the getters, the `Configure` assignments and the `ToCore()` mappings.

### F1.3 Seeds (`FishingSeedData`)

- **Tuning initializer:** append:
  `StrainBarFull = 6f, StrainSnap = 7f, StrainRestInput = 0.5f, StrainReelRun = 2.2f, StrainReelBack = 2.5f, StrainCounterSteer = 0.8f, StrainOtherSteer = 0.5f, SideMoveSpeed = 1.2f, BackMoveSpeed = 1.3f, TireSteerRate = 0.8f, TireCounterRate = 1.25f, TireDecayRate = 0.1f, TireRunFull = 20f, TireReelFull = 5f, TireReelMaxMult = 3f, BackPullMargin = 2f, FightTimeout = 120f,`
- **Profiles:** extend the private `Profile(...)` helper with four trailing parameters `strainMod, moveMult, reelInMult, wavePeriod`. Pass these per row (the old sim ignores all four, so the final quirk value of `wavePeriod` can ship now):
  - steady: `0.8f, 0.8f, 1.2f, 4f`
  - darter: `1.0f, 1.0f, 1.0f, 4f`
  - bulldog: `1.25f, 1.25f, 0.9f, 4f`
  - thrasher: `1.0f, 1.0f, 1.0f, 1.5f`
  - runner: `1.25f, 1.25f, 0.9f, 4f`
- **Content builder:** `FishingContentBuilder` passes `f.StrainMod, f.MoveMult, f.ReelInMult, f.WavePeriod` to `Configure`.

### F1.4 Assets (YAML; the builder never rewrites an existing asset, so these lines are the only way the game gets the values)

- **`FishingTuning.asset`:** append after `  pityHookWindowBonus: 0.3`, with two-space indent, one per line: `strainBarFull: 6`, `strainSnap: 7`, `strainRestInput: 0.5`, `strainReelRun: 2.2`, `strainReelBack: 2.5`, `strainCounterSteer: 0.8`, `strainOtherSteer: 0.5`, `sideMoveSpeed: 1.2`, `backMoveSpeed: 1.3`, `tireSteerRate: 0.8`, `tireCounterRate: 1.25`, `tireDecayRate: 0.1`, `tireRunFull: 20`, `tireReelFull: 5`, `tireReelMaxMult: 3`, `backPullMargin: 2`, `fightTimeout: 120`.
- **Each `FightProfiles/<id>.asset`:** append after `  lateralJitter: ...`:
  - steady: `strainMod: 0.8`, `moveMult: 0.8`, `reelInMult: 1.2`, `wavePeriod: 4`
  - darter: `strainMod: 1`, `moveMult: 1`, `reelInMult: 1`, `wavePeriod: 4`
  - thrasher: `strainMod: 1`, `moveMult: 1`, `reelInMult: 1`, `wavePeriod: 1.5`
  - bulldog and runner: `strainMod: 1.25`, `moveMult: 1.25`, `reelInMult: 0.9`, `wavePeriod: 4`

### F1.5 Validators (`FishingValidators`)

- **Tuning ranges:** `strainBarFull` [1, 20], `strainSnap` [1, 30], `strainRestInput` [0, 3], `strainReelRun` [0, 6], `strainReelBack` [0, 6], `strainCounterSteer` [0, 3], `strainOtherSteer` [0, 3], `sideMoveSpeed` [0, 5], `backMoveSpeed` [0, 5], `tireSteerRate` [0, 5], `tireCounterRate` [0, 5], `tireDecayRate` [0, 1], `tireRunFull` [1, 100], `tireReelFull` [0.5, 50], `tireReelMaxMult` [1, 5], `backPullMargin` [0, 10], `fightTimeout` [30, 300].
- **Tuning rules:** `Rule(e, L, t.StrainSnap > t.StrainBarFull, "strainSnap > strainBarFull")` and `Rule(e, L, t.MaxFightTime > t.FightTimeout, "maxFightTime > fightTimeout")`.
- **Profile ranges:** `strainMod` [0.1, 3], `moveMult` [0, 3], `reelInMult` [0.1, 5], `wavePeriod` [0.5, 8].

### F1.6 Tests (write first)

- **`FishingTestData`:**
  - `Tuning()` gets the 17 new fields (values as F1.3).
  - `BaseProfiles()` sets the tiers after building the array: steady `0.8/0.8/1.2`; darter and thrasher `1/1/1`; bulldog and runner `1.25/1.25/0.9`. Do it with a private helper `static void Tier(ref FishFightProfile p, float s, float m, float r)`. Every base profile gets `WavePeriod = 4f`.
  - `MotionProfiles()` inherits them and sets thrasher `WavePeriod = 1.5f`.
- **`FishingDefsTests`:**
  - In `FishingDefs_ToCore_MapsEveryField`, give the four profile fields and the 17 tuning fields distinct values and assert each maps.
  - In `FishingDefs_SerializedNames_MatchContract`, append `strainMod,moveMult,reelInMult,wavePeriod` to the FightProfileDef csv, append the 17 names to `TuningNames`, and change the count to 60 (assertion message "60 serialized members ...").
- **`FishingValidatorTests`:**
  - Add a `Rows` entry for every new range (tuning rows with id `""`, profile rows on `steady`).
  - Add `[TestCase("strainSnap")]` to `FishingValidators_CrossFieldRules_ReportRuleText`: set `strainBarFull` 6 and `strainSnap` 6.5, expect no errors; then `strainSnap` 6, expect `"tuning: strainSnap > strainBarFull"`.
  - Add `[TestCase("fightTimeout")]`: `fightTimeout` 120 and `maxFightTime` 180, no errors; then `fightTimeout` 180 and `maxFightTime` 180, expect `"tuning: maxFightTime > fightTimeout"`.
  - Update `MakeSeedSet` for the new `Configure` parameters.
- **`FishingContentTests`:**
  - In `FishingData_Ranges_Valid`, add the same ranges and the two rules.
  - In `FishingData_SeedsMatchSpecTables`, add a `Tn` line for each of the 17 tuning values, and extend the private `Profile(...)` assertion helper with four trailing arguments (tiers and `wavePeriod`, values as F1.3).
  - Add a new test (below).
- **`ContentRegistryTests`:** update the `Configure` calls only.

```csharp
        // Catches (R72): an asset whose new field was never written (the builder is create-if-missing, so a missing YAML line loads as 0 and
        // the fish would never strain or move), or any asset value drifting from its seed. Compares every public field of the Core structs.
        [Test]
        public void FishingData_AssetValuesEqualSeeds()
        {
            ContentRegistry reg = LoadRegistry();
            AssertFieldsEqual("tuning", FishingSeedData.Tuning, reg.FishingTuning.ToCore());
            foreach (var r in FishingSeedData.Rods)
                AssertFieldsEqual("rod " + r.id, r.stats, AssetDatabase.LoadAssetAtPath<RodDef>(AssetPaths.RodsFolder + "/" + r.id + ".asset").ToCore());
            foreach (var p in FishingSeedData.Profiles)
                AssertFieldsEqual("profile " + p.id, p.profile,
                    AssetDatabase.LoadAssetAtPath<FightProfileDef>(AssetPaths.FightProfilesFolder + "/" + p.id + ".asset").ToCore());
        }

        static void AssertFieldsEqual<T>(string what, T seed, T asset) where T : struct
        {
            foreach (System.Reflection.FieldInfo f in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                Assert.AreEqual(f.GetValue(seed), f.GetValue(asset), what + " " + f.Name + " (asset vs seed)");
        }
```

If `FishingData_AssetValuesEqualSeeds` fails on a field F1 does not touch (an existing asset-vs-seed mismatch), report the field and both values. Do not change either value.

**Filter:** `FishGame.Tests.EditMode.(FishingDefsTests|FishingContentTests|FishingValidatorTests|ContentRegistryTests|FishingDatabaseTests)`.

### Steps and commit
1. Write the tests and do a red run.
2. Implement F1.1-F1.5 and do a green run.
3. Run the full `test-edit`; everything else, including the old `FightSimTests` and the bots, is unchanged and green.
4. Do the ASCII scan.
5. Commit: `feat(fishing-data): R72 Rust fight fields (tiers, strain table, tire, timeout) in Core, Defs, seeds and assets`.

### Acceptance (reviewer, Sonnet)
- 17 tuning and 4 profile fields (tiers + `wavePeriod`) in struct, Def, seed, YAML, validator, validator-test row, content-test row, and `TuningNames` count 60.
- `FishingData_AssetValuesEqualSeeds` passes. Delete one YAML line locally to confirm it fails, then restore it.
- No existing value changed.

---

## Task F2 - Core: the Rust-faithful `FightSim`, host floor and owner timeout

- **Goal:** replace the fight model with section 0 and research note 8.7, switch the shared test data to Rust values, and keep every consumer compiling and green.
  - The shipped assets keep their old shared values until F3. The game is in a transitional state between F2 and F3; nobody plays it.
- **Files:**
  - Core: `Assets/_Project/Core/Fishing/{FightState,FightSim,FishingConstants,CatchValidator,FishingHostLedger,RodStateMachine}.cs`, `Assets/_Project/Core/AssemblyInfo.cs` (comment only).
  - UI: `Assets/_Project/UI/Fishing/DevOverlayPresenter.cs` (fight line).
  - Tests: `Assets/_Project/Tests/EditMode/{FightSimTests,FishingTestData,RodStateMachineTests,FishingHostLedgerTests,CatchValidatorTests,CastTipReachTests}.cs`, `Assets/_Project/Tests/EditMode/Tuning/FishingBots.cs` (strip only), `Assets/_Project/Tests/PlayMode/FishingSoloTests.cs` (driver `FightRhythm`), `Assets/_Project/Tests/PlayMode/FeedbackSoloTests.cs` (timeout argument only).
  - Delete: `Assets/_Project/Tests/EditMode/FightMotionTests.cs` and its `.meta` (`git rm`).

### F2.1 `FishingConstants` (edit)

Change `NearSnapHigh = 0.8f` to `NearSnapHigh = 1.0f` with the comment `// over the full bar (R72)`. Add:
```csharp
        // R72 Rust fight baseline (spec 9.5): structural constants, not tuning.
        public const float PullWavePeak = 2f;                // p = clamp(triangle peaking at 2, floor, 2); Rust: PingPong(t, 2)
        public const float RustWavePeriod = 4f;              // Rust's wave period; used when a profile's WavePeriod is not > 0
        public const float PullWaveFloor = 0.2f;
        public const float SteerDigitalThreshold = 0.5f;     // |steer| at or above counts as a key press (Rust steer is digital)
        public const float LateralMinRadius = 1.0f;          // m: floor of the radius when turning sideways m/s into lateral
        public const float CounterSideMult = 0.5f;           // a counter-steer halves the side run
        public const float ReelBackMult = 0.5f;              // reeling halves a back pull
        public const float SideRunLeftChance = 0.5f;         // a run goes left or right 50/50
        public const float HostTimeoutSlackSeconds = 2.0f;   // host accepts an owner Timeout when its own elapsed >= FightTimeout - this
```
Leave the now-unused constants (`CalmLeadInSeconds`, `ExhaustedStamina`, `SideRunAgainstChance`, `WeightFactorMin/Max`, `ReelRateFloor`) in place; F7 removes them.

### F2.2 `FightState.cs` (write exactly)

```csharp
// Core/Fishing/FightState.cs: the fight's state record (spec 9.5 as rewritten by R72). Plain data; FightSim owns the only writer.
// Authority: owner only (the host never replays the fight, spec 9.9).
namespace FishGame.Core
{
    public struct FightState
    {
        public float LineLength, Lateral;   // m from the rod tip; -1..+1 across the cone (+1 = right, |1| = LateralMaxDeg)
        public float Strain, Tension;       // Strain in Rust units (bar full at StrainBarFull, snap above the snap strain); Tension = Strain / StrainBarFull (display)
        public float Tire;                  // Rust TireAmount: rises while steering, falls without steer; no floor
        public FightPhase Phase;            // Calm = the fish rests (Rust state 0), Burst = a run
        public float PhaseTimer;            // seconds left in the phase; the switch happens at the start of the tick it reaches 0
        public sbyte BurstDir;              // run side: -1 left, +1 right (always set in a run); 0 at rest
        public bool Back;                   // the run also pulls back (Rust PullingBack); false at rest
        public float SnapTimer, SlackTimer, Elapsed;   // SnapTimer = seconds over the full bar (FlagGrace); SlackTimer = seconds not reeling (junk only)
        public int NearSnapRecoveries; public float MaxTension;
        public FightOutcome Outcome; public EscapeReason Reason;
    }
}
```

### F2.3 `FightSim.cs` (write exactly)

```csharp
// Core/Fishing/FightSim.cs - owner-simulated fight: the Rust-faithful baseline (spec 9.5, R72; maths from research note 2026-10-12 s8).
// Pure C#; seeded Pcg32 only. Authority: runs on the owner only; the host never replays it (spec 9.9). Deterministic for a seed and
// an input sequence. RNG draw order is fixed (plan rework-fight 0.9): constructor 1 (pull-wave phase), run start 3 (side, back,
// duration) + 1 jitter when LateralJitter > 0, rest start 1 (duration), 1 flip check per eligible run tick when FlipRate > 0.
// Per-profile quirks (R72, spec 9.6: timing, back chance, flips, ramp, rest creep, jitter, wave period) change only how the fish
// moves; the strain formula, tire, reel and endings are Rust's.
using System;

namespace FishGame.Core
{
    public sealed class FightSim
    {
        public const float Step = FishingConstants.FightStep;
        internal const float TimeEps = 1e-4f;

        private readonly FishFightProfile _fish;
        private readonly RodStats _rod;
        private readonly FishingTuning _t;
        private readonly float _gainScale;      // (StrainScale or StrainScaleAssist) x (PityStrainMult on a Pity cast) / LineStrength
        private readonly float _snapStrain;     // StrainBarFull + (StrainSnap - StrainBarFull) x (PityGraceMult on a Pity cast)
        private readonly float _lateralMaxRad;
        private readonly float _maxLineOut;
        private readonly float _wavePeriod;     // quirk: the profile's WavePeriod (Rust 4 s)
        private readonly float _wavePhase;
        private Pcg32 _rng;
        private FightState _s;
        private bool _nearSnapArmed;
        private float _phaseElapsed;            // seconds since the current run or rest began (ramp, rest creep)
        private float _sinceFlip;               // seconds since the last flip; set to FlipMinGap at a run start (a first-tick flip is allowed)
        private float _runSpeedMult = 1f;       // quirk: per-run speed multiplier (LateralJitter)

        // weightT is accepted for API stability and ignored: weight does not enter the R72 baseline (Rust has no weight term).
        public FightSim(in FishFightProfile fish, in RodStats rod, in FishingTuning tuning, float weightT, uint seed,
                        float startLineLength, bool assist, bool pity = false)
        {
            _fish = fish; _rod = rod; _t = tuning;
            _rng = new Pcg32(seed);
            _gainScale = (assist ? _t.StrainScaleAssist : _t.StrainScale) * (pity ? _t.PityStrainMult : 1f) / _rod.LineStrength;
            _snapStrain = _t.StrainBarFull + (_t.StrainSnap - _t.StrainBarFull) * (pity ? _t.PityGraceMult : 1f);
            _lateralMaxRad = _t.LateralMaxDeg * (float)(Math.PI / 180.0);
            _maxLineOut = _rod.MaxLineOut(_t.SpoolExtra);
            _s.LineLength = Clamp(startLineLength, 0f, _maxLineOut);
            _s.Phase = FightPhase.Calm;
            _s.PhaseTimer = 0f;                                                          // the first tick starts a run (Rust)
            _wavePeriod = _fish.WavePeriod > 0f ? _fish.WavePeriod : FishingConstants.RustWavePeriod;
            _sinceFlip = _fish.FlipMinGap;
            _wavePhase = Uniform(0f, _wavePeriod);                                       // draw 1: Rust's wave runs on global time
        }

        private FightSim(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength)
        {
            IsJunk = true; _rod = rod; _t = tuning;
            _rng = new Pcg32(seed);
            _s.LineLength = Math.Max(0f, startLineLength);
            _s.Phase = FightPhase.Calm;
        }

        public static FightSim ForJunk(in RodStats rod, in FishingTuning tuning, uint seed, float startLineLength)
            => new FightSim(rod, tuning, seed, startLineLength);

        public bool IsJunk { get; }
        public ref readonly FightState State => ref _s;
        public bool Exhausted => !IsJunk && _s.Tire >= _t.TireRunFull;          // tire01 = 1: runs have no length any more
        public float SnapTension => IsJunk ? 0f : _snapStrain / _t.StrainBarFull; // display units: 7/6 (1.25 on a Pity cast)
        public float PullWave { get; private set; }                               // p of the last tick, 0.2..2
        internal int DrawCount { get; private set; }
        internal float RunSpeedMult => _runSpeedMult;                             // test hook (Thrasher jitter)

        public FightOutcome Tick(in FightInput input)
        {
            if (_s.Outcome != FightOutcome.None) return _s.Outcome;
            const float dt = Step;
            _s.Elapsed += dt;
            bool reel = input.Reel;
            if (IsJunk) return TickJunk(reel, dt);

            // 1. fish state machine at the start of the tick (Rust: the first run starts at once)
            if (_s.PhaseTimer <= TimeEps)
            {
                if (_s.Phase == FightPhase.Burst) StartRest(); else StartRun();
            }
            bool run = _s.Phase == FightPhase.Burst;
            _phaseElapsed += dt;

            // 1b. quirk: mid-run side flips (Darter, Thrasher); one draw per eligible run tick, after the switch draws
            if (run && _fish.FlipRate > 0f)
            {
                _sinceFlip += dt;
                if (_sinceFlip >= _fish.FlipMinGap - TimeEps && Next01() < _fish.FlipRate * dt)
                {
                    _s.BurstDir = (sbyte)-_s.BurstDir;
                    _sinceFlip = 0f;
                }
            }
            bool back = run && _s.Back;

            // 2. steer: digital, dropped unless it pulls the line toward the cast line (Rust dot test; FG frame = cast line)
            int steer = SteerDir(input.Steer, _s.Lateral);
            bool counter = run && steer != 0 && steer == -_s.BurstDir;

            // 3. pull wave, and the fish's speed this tick (quirks: ramp-in, per-run multiplier); speed never touches strain
            float p = Wave(_wavePhase + _s.Elapsed, _wavePeriod);
            PullWave = p;
            float ramp = run && _fish.BurstRamp > 0f ? Clamp01(_phaseElapsed / _fish.BurstRamp) : 1f;
            float speed = p * _fish.MoveMult * ramp * _runSpeedMult;

            // 4. sideways in m/s, turned into lateral at the current radius; the cone edge flips the run's side
            float side = 0f;
            if (run) side += _s.BurstDir * _t.SideMoveSpeed * speed * (counter ? FishingConstants.CounterSideMult : 1f);
            if (steer != 0 && !(run && steer == _s.BurstDir)) side += steer * _rod.SteerRate;
            float radius = Math.Max(_s.LineLength, FishingConstants.LateralMinRadius);
            float lateral = _s.Lateral + side * dt / (radius * _lateralMaxRad);
            if (lateral > 1f || lateral < -1f)
            {
                lateral = Clamp(lateral, -1f, 1f);
                if (run) _s.BurstDir = (sbyte)-_s.BurstDir;
            }
            _s.Lateral = lateral;

            // 5. line: the reel speeds up as the fish tires; a back pull takes line, halved while reeling; the clamp adds no strain
            float tireReel = 1f + (_t.TireReelMaxMult - 1f) * Clamp01(_s.Tire / _t.TireReelFull);
            if (reel) _s.LineLength -= _rod.ReelSpeed * _fish.ReelInMult * tireReel * dt;
            if (back) _s.LineLength += _t.BackMoveSpeed * speed * (reel ? FishingConstants.ReelBackMult : 1f) * dt;
            else if (!run && _fish.CalmDrift > 0f && !Exhausted)                           // quirk: Bulldog creeps line out at rest
            {
                float driftRamp = _fish.CalmDriftRamp > 0f ? Clamp01(_phaseElapsed / _fish.CalmDriftRamp) : 1f;
                _s.LineLength += _fish.CalmDrift * driftRamp * (1f - Clamp01(_s.Tire / _t.TireRunFull)) * dt;
            }
            _s.LineLength = Clamp(_s.LineLength, 0f, _maxLineOut);

            // 6. tire: only steering tires the fish; no floor
            if (steer != 0) _s.Tire += (counter && !back ? _t.TireCounterRate : _t.TireSteerRate) * dt;
            else _s.Tire -= _t.TireDecayRate * dt;

            // 7. strain: any input loads the line, only a full release drains it
            if (reel || steer != 0)
            {
                float k = !run ? _t.StrainRestInput
                        : reel ? (back ? _t.StrainReelBack : _t.StrainReelRun)
                        : counter ? _t.StrainCounterSteer : _t.StrainOtherSteer;
                _s.Strain += k * _fish.StrainMod * _gainScale * dt;
            }
            else _s.Strain = Math.Max(0f, _s.Strain - _t.RelaxRate * _rod.RelaxMult * dt);
            _s.Tension = _s.Strain / _t.StrainBarFull;
            if (_s.Tension > _s.MaxTension) _s.MaxTension = _s.Tension;

            // 8. timers and near-snap recoveries (over the full bar, then back under half)
            _s.SnapTimer = _s.Strain > _t.StrainBarFull ? _s.SnapTimer + dt : 0f;
            _s.SlackTimer = reel ? 0f : _s.SlackTimer + dt;
            if (_s.Tension > FishingConstants.NearSnapHigh) _nearSnapArmed = true;
            else if (_nearSnapArmed && _s.Tension < FishingConstants.NearSnapLow) { _nearSnapArmed = false; _s.NearSnapRecoveries++; }

            // 9. endings (priority: Landed, Snapped, Timeout); a fish has no pinned or slack escape (Rust has neither)
            if (reel && _s.LineLength <= _t.LandDistance) return End(FightOutcome.Landed, EscapeReason.None);
            if (_s.Strain > _snapStrain) return End(FightOutcome.Snapped, EscapeReason.None);
            if (_s.Elapsed >= _t.FightTimeout - TimeEps) return End(FightOutcome.Escaped, EscapeReason.Timeout);

            // 10. phase clock
            _s.PhaseTimer -= dt;
            return FightOutcome.None;
        }

        private FightOutcome TickJunk(bool reel, float dt)
        {
            if (reel) _s.LineLength = Math.Max(0f, _s.LineLength - _t.JunkDragSpeed * dt);
            _s.SlackTimer = reel ? 0f : _s.SlackTimer + dt;
            if (reel && _s.LineLength <= _t.LandDistance) return End(FightOutcome.Landed, EscapeReason.None);
            if (_s.SlackTimer >= _t.SlackEscapeTime - TimeEps) return End(FightOutcome.Escaped, EscapeReason.ThrewHook);
            return FightOutcome.None;
        }

        private void StartRun()
        {
            float tire01 = Clamp01(_s.Tire / _t.TireRunFull);
            sbyte dir = Next01() < FishingConstants.SideRunLeftChance ? (sbyte)-1 : (sbyte)1;     // draw 1
            float backRoll = Next01();                                                              // draw 2 (always made)
            bool back = backRoll < _fish.DiveChance && _s.LineLength < _rod.CastMaxDist - _t.BackPullMargin;
            float duration = Uniform(_fish.BurstMin, _fish.BurstMax) * (1f - tire01);              // draw 3
            float j = _fish.LateralJitter;
            _runSpeedMult = j > 0f ? Uniform(1f - j, 1f + j) : 1f;                                  // draw 4 (jitter profiles only)
            _s.Phase = FightPhase.Burst; _s.BurstDir = dir; _s.Back = back; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap;
        }

        private void StartRest()
        {
            float tire01 = Clamp01(_s.Tire / _t.TireRunFull);
            float duration = Uniform(_fish.CalmMin, _fish.CalmMax) * (1f + tire01);                // draw 1
            _s.Phase = FightPhase.Calm; _s.BurstDir = 0; _s.Back = false; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _runSpeedMult = 1f;
        }

        // Rust: steering counts only while it pulls the line away from the bobber's side; here, toward the cast line (sign opposite to lateral).
        private static int SteerDir(float steer, float lateral)
        {
            if (float.IsNaN(steer)) return 0;
            int d = steer >= FishingConstants.SteerDigitalThreshold ? 1 : steer <= -FishingConstants.SteerDigitalThreshold ? -1 : 0;
            return d * lateral < 0f ? d : 0;
        }

        // Pull wave: a triangle of the given period peaking at 2, clamped to [0.2, 2]. With period 4 it is Rust's clamp(PingPong(t, 2), 0.2, 2).
        // The mean over one period is 1.01 for any period. A non-positive period falls back to Rust's 4 s.
        internal static float Wave(float t, float period)
        {
            if (!(period > 0f)) period = FishingConstants.RustWavePeriod;
            float half = 0.5f * period;
            float m = t - period * (float)Math.Floor(t / period);
            float pp = (m < half ? m : period - m) * (FishingConstants.PullWavePeak / half);
            return Clamp(pp, FishingConstants.PullWaveFloor, FishingConstants.PullWavePeak);
        }

        // Test seams (no draws). Both reset the quirk clocks (phase elapsed 0, flip gap satisfied, run speed x1). ForceState derives Tension from Strain.
        internal void ForcePhase(FightPhase phase, sbyte burstDir, bool back, float duration)
        {
            bool run = phase == FightPhase.Burst;
            _s.Phase = phase; _s.BurstDir = run ? burstDir : (sbyte)0; _s.Back = run && back; _s.PhaseTimer = duration;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _runSpeedMult = 1f;
        }

        internal void ForceState(in FightState state)
        {
            _s = state;
            _s.Tension = _s.Strain / _t.StrainBarFull;
            _nearSnapArmed = _s.Tension > FishingConstants.NearSnapHigh;
            _phaseElapsed = 0f; _sinceFlip = _fish.FlipMinGap; _runSpeedMult = 1f;
        }

        private FightOutcome End(FightOutcome o, EscapeReason r) { _s.Outcome = o; _s.Reason = r; return o; }
        private float Next01() { DrawCount++; return _rng.NextFloat(); }
        private float Uniform(float min, float max)
        {
            float u = Next01();
            return max > min ? Pcg32.MapUnitToRange(u, min, max) : min;
        }
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
```

`Core/AssemblyInfo.cs`: update the comment's seam list to `ForcePhase, ForceState, DrawCount, Wave, RunSpeedMult, TimeEps`.

Junk sims never read the profile fields (`_fish` is default), so the quirk fields cannot affect junk.

### F2.4 Host: `CatchValidator` and `FishingHostLedger` (edit)

`CatchValidator` gains:
```csharp
        // R72: the fastest the line can come in for this fish on this rod (a fully tired fish reels at TireReelMaxMult). Used for the Landed
        // floor and clean-claim check (4); junk keeps JunkDragSpeed. Host values only: the host's rod and the rolled species' profile.
        public static float MaxReelSpeed(in RodStats rod, in FishFightProfile fish, in FishingTuning t)
        {
            return rod.ReelSpeed * fish.ReelInMult * t.TireReelMaxMult;
        }

        // R72: the owner's FightSim ends a fish fight at FightTimeout. The host accepts that claim only when its own clock agrees within
        // HostTimeoutSlackSeconds (the hook reached the host one latency late; the result arrives one latency after the owner's tick).
        public static bool OwnerTimeoutPlausible(double now, in ActiveCast cast, float fightTimeout)
        {
            return now - cast.THooked >= fightTimeout - FishingConstants.HostTimeoutSlackSeconds;
        }
```
Fix the stale comment of `CleanClaimAccepted` check (3): `// (3) a recovery needs tension > 1.0 (over the full bar)`.

`FishingHostLedger.Result`:
```csharp
            float speed = junk ? t.JunkDragSpeed
                : CatchValidator.MaxReelSpeed(_db.Rods[a.RodIndex], _db.Profiles[_db.Species[a.Roll.Catch.ItemIndex].FightProfileIndex], in t);
            ...
                case FightOutcome.Escaped:
                    valid = reason == EscapeReason.Missed ? !wasHooked
                        : reason == EscapeReason.Timeout ? wasHooked && CatchValidator.OwnerTimeoutPlausible(Now, in a, t.FightTimeout)
                        : wasHooked && (reason == EscapeReason.Pinned || reason == EscapeReason.ThrewHook || reason == EscapeReason.Dropped);
                    break;
```
- Keep `Pinned` accepted: harmless, the owner no longer sends it.
- Update the `speed` comment: `// host rod and rolled profile, never an owner value (R72 MaxReelSpeed)`.
- **The network path needs no change.** `FishingRodNetwork.ResultRpc` -> `FishingHost.HandleResult` -> `Ledger.Result` passes the reason byte through unfiltered (checked 2026-10-12), so the owner's Timeout reaches the rule above.
- **`CastTipReachTests.MinChargeCast_Measurement_PrintsLineStartAndTimeToLandPerRod`** (print-only): replace the two `rod.ReelSpeed` arguments of `LandedFloorSeconds` and `CleanClaimAccepted` with `CatchValidator.MaxReelSpeed(rod, profile, t)`, computing the floor inside the profile loop, so the printed floor matches the host.

### F2.5 `RodStateMachine.TickFight` cues (edit the block after `Fight.Tick`)

```csharp
            if (o == FightOutcome.None)
            {
                if (Fight.IsJunk) return;
                FightPhase now = Fight.State.Phase;
                if (Fight.Exhausted && !wasExhausted) Cue(RodCue.Exhausted);
                else if (Fight.Exhausted) return;                                          // a spent fish's zero-length runs make no splash
                else if (now == FightPhase.Calm && phase == FightPhase.Burst) Cue(RodCue.Calm);
                else if (now == FightPhase.Burst && phase == FightPhase.Calm)
                    Cue(Fight.State.Back ? RodCue.Dive : RodCue.SideRun);                 // a new run: Dive when it pulls back
                else if (now == FightPhase.Burst && Fight.State.BurstDir != dir) Cue(RodCue.SideRun);   // the side flipped at the cone edge
                return;
            }
```
Change the `EndEscaped` comment `// rows: pinned, slack` to `// rows: timeout (fish), slack (junk)`.

### F2.6 Transitional consumer edits (compile and stay green)

**Not touched in F2:** `RodControllerRules.HookedFlags` (it still sets FlagDive from `burstDir == 0`, which a run no longer produces). Until F5 there is no dive thrum or plunge; this is accepted as transitional and fixed in F5.

**`DevOverlayPresenter.AppendFight`:** replace the fish line with:
`FIGHT phase=<Phase> dir=<BurstDir> back=<0|1> strain=<F2> tension=<F2> tire=<F2> p=<F2 PullWave> line=<F1>m lat=<F2> el=<F1>s over=<F2 SnapTimer> recov=<n> max=<F2>`
- Build it the same way as the existing `_sb.Append` chain.
- The junk line is unchanged.

**`FishingBots.cs`** (temporary strip; F4 rewrites it):
- Delete the six target tests:
  - `FishingBots_BulldogHandmade_MeetsGreedyCounterRhythmTargets`
  - `FishingBots_SteadyRhythm_MedianFifteenToThirtySeconds`
  - `FishingBots_DarterRhythm_MedianFifteenToThirtySeconds`
  - `FishingBots_ThrasherRhythm_MedianThirtyFiveToFiftyFiveSeconds`
  - `FishingBots_RunnerRhythm_MedianSixtyToOneTenSnapsAtMostFifteenPercent`
  - `FishingBots_TwigRod_RhythmLandsNinetyPercentAtHalfWeightSeventyFiveAtPointNine`
- Delete `MedianTarget`, the `SigAccum` class and its uses (the `sig` parameter of `RunFight` and `EnsureMatrix`, the `s_sig` cache and the `FG SIG` print block).
- Keep `FishingBots_RhythmDecide_PinsDiveEasingAndSteerRules`, `FishingBots_AllProfiles_PrintSignatureMotionAndDistributions` (rename the method to `FishingBots_AllProfiles_PrintDistributions`) and the perf test.
- Add at the top of the class: `// R72 (plan rework-fight F2): the pre-R72 targets are deleted; F4 adds the Rust baseline bots and targets.`

**PlayMode `FishingPlayModeDriver.FightRhythm`** (in `FishingSoloTests.cs`): replace the loop body with the Rust pump:
```csharp
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            bool easing = false;
            while (rig.Machine.State == RodState.Hooked)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("the fight did not end within " + timeoutSeconds + " s");
                FightState s = rig.Machine.Fight.State;
                bool run = s.Phase == FightPhase.Burst;
                if (s.Tension >= 0.85f) easing = true;
                else if (easing && s.Tension <= 0.30f) easing = false;
                float steer = 0f; bool reel = false;
                if (!easing)
                {
                    // R72 pump: counter a run without reeling (only while the steer counts: the fish is on its pull side); reel at rest,
                    // steering toward the cast line.
                    if (run) steer = s.BurstDir * s.Lateral > 0f ? -s.BurstDir : 0f;
                    else { reel = true; steer = s.Lateral > 0.05f ? -1f : s.Lateral < -0.05f ? 1f : 0f; }
                }
                yield return Step(rig.Fake, new PlayerInputFrame { Steer = steer, Reel = reel });
            }
            rig.Fake.Frame = default;
```
Raise both callers' timeout from `60f` to `100f` (`FishingSoloTests.SoloHost_CastBiteFight_Lands`, `FeedbackSoloTests` line ~86). A bluegill's Rhythm 90th percentile is about 28 s; 100 s stays under the 120 s timeout.

### F2.7 `FishingTestData` (shared values switch to Rust)

- **`Tuning()`:** `LandDistance = 1f`, `RelaxRate = 1.5f`, `StrainScale = 0.75f`, `StrainScaleAssist = 0.45f`, `LateralMaxDeg = 60f`. Every other value is unchanged.
- **`Rods()`:**
  - carbon `Rod(2, 24f, 0.72f, 1.3f, 0.70f, 0.93f, 1.1f)`
  - deepsea `Rod(3, 30f, 0.78f, 1.6f, 0.75f, 1.07f, 1.2f)`
  - handmade `Rod(1, 18f, 0.6f, 1.0f, 0.60f, 0.8f, 1.0f)`
  - twig `Rod(0, 14f, 0.54f, 0.8f, 0.50f, 0.67f, 0.9f)`
- **`BaseProfiles()`** = Rust-faithful, quirk-free (used by the hand checks). After the tiers, set every profile to `CalmMin = 2f, CalmMax = 4f, BurstMin = 3f, BurstMax = 7f, DiveChance = 0.39f, FlipRate = 0f, FlipMinGap = 0.5f, BurstRamp = 0f, CalmDrift = 0f, CalmDriftRamp = 0f, LateralJitter = 0f, WavePeriod = 4f`.
- **`MotionProfiles()`** = the R72 quirks (the shipped values, section 0.3). Start from `BaseProfiles()` and set:
  - steady: `BurstMin 3, BurstMax 5, DiveChance 0.15, BurstRamp 0.6`
  - darter: `CalmMin 1, CalmMax 2, BurstMin 2, BurstMax 3.5, DiveChance 0.15, FlipRate 1.0, FlipMinGap 0.3`
  - thrasher: `CalmMin 1.5, CalmMax 3, BurstMin 3, BurstMax 5, DiveChance 0.20, FlipRate 0.4, FlipMinGap 0.5, LateralJitter 0.3, WavePeriod 1.5`
  - bulldog: `BurstMin 2.5, BurstMax 4.5, DiveChance 0.55, CalmDrift 0.06, CalmDriftRamp 2.5`
  - runner: `CalmMin 2, CalmMax 3.5, BurstMin 5, BurstMax 9, DiveChance 0.30, BurstRamp 0.8`
- The machine and ledger tests keep using `Database(motion: true)`. Their forced-state cases are unaffected: Bulldog's rest creep in the landing tests is below 1e-5 m over 2 ticks, because the drift ramp starts at 0 after `ForceState`.
- **Comments:** update the file header to "R72 baseline values".

### F2.8 Tests (write first)

`FightSimTests.cs` is rewritten from scratch. Common setup:
- `Tun()` = `FishingTestData.Tuning()`; `Handmade()`; `P(i)` = `FishingTestData.BaseProfiles()[i]`; `In(steer, reel)`.
- `Small = ProfileSteady`, `Medium = ProfileDarter`, `Large = ProfileRunner`.
- `Make(profile, line = 18f, seed = 4242, assist = false, pity = false, rod = Handmade())`.
- `RunState(dir, back, lateral, line, strain = 0f, phaseTimer = 30f, tire = 0f)` returns a `FightState` with `Phase = Burst`.
- `RestState(line, strain = 0f, phaseTimer = 100f, lateral = 0f, tire = 0f)` returns a `FightState` with `Phase = Calm`.
- `RunUntilEnd(sim, input, maxTicks)` returns the 1-based end tick.

Tolerances: 1e-6 on single-tick strain or lateral deltas; 1e-5 on line deltas near 10 m; tick counts +-1 only where stated.

| # | Test | Setup and input | Expected |
|---|---|---|---|
| 1 | `FightSim_ReelIntoSideRun_SnapsAtRustTime` `[TestCase(Small, 0.8f, 266, 5.303f)] [TestCase(Medium, 1.0f, 213, 4.242f)] [TestCase(Large, 1.25f, 170, 3.394f)]` | `ForceState(RunState(+1, false, 0.5f, 18f))`; `In(0, true)` | first tick dStrain = 2.2 x mod x 0.75 x 0.02; Snapped on the given tick +-1; `Elapsed` within 0.03 of the given seconds; outcome never Landed |
| 2 | `FightSim_ReelIntoBackPull_SnapsAtRustTime` (same three cases: 234 / 4.667, 187 / 3.733, 150 / 2.987) | `RunState(+1, true, 0.5f, 18f)`; `In(0, true)` | dStrain = 2.5 x mod x 0.75 x 0.02; snap tick and seconds as given |
| 3 | `FightSim_ReelWhileResting_SnapsAtRustTime` (1167 / 23.33, 934 / 18.67, 747 / 14.93) | `RestState(29.9f)`; `In(0, true)` | dStrain = 0.5 x mod x 0.75 x 0.02; Snapped (not Landed) on the given tick +-1 |
| 4 | `FightSim_CounterSteerOnly_LoadsAtRustRate` (small 0.96, medium 1.2, large 1.5) | `RunState(+1, false, 0.5f, 18f)`; `In(-1, false)` x 100 | Strain == the given value (1e-4); Tire == 2.5 (1e-4); 0 < Lateral < 1 on every tick (the steer was never dropped); comment: time to 7 at this rate is 14.58 / 11.67 / 9.33 s (Rust 8.7) |
| 5 | `FightSim_FullRelease_DrainsOnePointFivePerSecond` | `RestState(10f, strain: 6f)`, `In(0, false)` | after 120 ticks Strain 2.4 (1e-3); a second sim from 5.4 after 120 ticks: 1.8 (1e-3, tension 0.3); from 6: 0.03 (1e-3) after 199 ticks, <= 1e-5 after 200, still 0 after 201; the same drain from `RunState(+1, true, 0.5f, 10f, strain: 6f)` (a back pull does not load the line by itself) |
| 6 | `FightSim_BarFullAtSix_SnapAboveSeven` (large) | A: `RestState(10f, strain: 6f)` | A: Tension == 1.0 (1e-6) before ticking; `SnapTension` == 7/6 (1e-6). B: `RunState(+1, false, 0.5f, 10f, strain: 6.95f)` one tick `In(0, true)`: None, Strain 6.99125 (1e-5), Tension > 1, SnapTimer 0.02. C: strain 6.99, one tick: Snapped |
| 7 | `FightSim_FirstTick_StartsARunAtOnce` | new medium sim, seed 4242 | before: Calm, PhaseTimer 0, `DrawCount` 1. After one `In(0, false)`: Burst, `DrawCount` 4, BurstDir is -1 or +1, PhaseTimer + 0.02 in [3, 7] (1e-5) |
| 8 | `FightSim_RunAndRestDurations_ScaleWithTire` | seeds 1..200; for each tire value one sim forced to `RestState(10f, phaseTimer: 0f, tire: x)` (run start) and one to `RunState(+1, false, 0.5f, 10f, phaseTimer: 0f, tire: x)` (rest start); one tick `In(0, false)` | run duration = PhaseTimer + 0.02: tire 0 in [3, 7]; tire 10 in [1.5, 3.5]; tire 20 <= 1e-4; tire -5 in [3, 7]. Rest: tire 0 in [2, 4]; tire 10 in [3, 6]; tire 20 in [4, 8]. Mean run at tire 0 is 5 +- 0.3. `DrawCount` +3 per run start and +1 per rest start |
| 9 | `FightSim_RunSide_FiftyFifty_BackThirtyNinePercentInsideTheGate` | seeds 1..1000, `RestState(10f, phaseTimer: 0f)`, one tick | left share 0.5 +- 0.05; back share 0.39 +- 0.05; with line 16.0 (= 18 - 2) back share 0 and `DrawCount` still +3; with 15.9 back share 0.39 +- 0.05 |
| 10 | `FightSim_DrawOrder_ReplaysByHand` | seed 77, medium, line 10; a hand `Pcg32(77)` | u0 -> wave phase; tick 1: BurstDir == (u1 < 0.5 ? -1 : 1), Back == (u2 < 0.39), PhaseTimer + 0.02 == Map(u3, 3, 7) (1e-5), `PullWave` == `FightSim.Wave(phase + 0.02f, 4f)` (1e-6). Then `ForceState` the same state with PhaseTimer 0 and tick: Calm, PhaseTimer + 0.02 == Map(u4, 2, 4) (the tire is -0.002, so tire01 = 0) |
| 11 | `FightSim_Wave_IsRustPingPongClamped` | `FightSim.Wave(t, 4f)` | Wave(0) 0.2, Wave(0.1) 0.2, Wave(1) 1, Wave(2) 2, Wave(3) 1, Wave(3.9) 0.2, Wave(4) 0.2, Wave(5) 1, Wave(-1) 1 (1e-6); `Wave(1, 0f)` == `Wave(1, 4f)` (fallback); for seeds 1..20 (base medium) the sum of `PullWave` x 0.02 over 200 ticks is 4.04 (1e-3) |
| 12 | `FightSim_Counter_DragsTheFishBackAndTiresIt` (user decision 2) | medium, two sims with seed 4242, both `RunState(+1, false, 0.5f, 12f)`; A `In(0, false)` x 200, B `In(-1, false)` x 200 | A.Lateral 0.8858 +- 0.005, A.Tire -0.4, A.Strain 0; B.Lateral 0.4382 +- 0.005, B.Tire 5.0 (1e-4), B.Strain 2.4 (1e-3), B.LineLength 12 (1e-5); B.Lateral < 0.5 < A.Lateral |
| 13 | `FightSim_Steer_CountsOnlyTowardTheCastLine` | medium, `RestState(10f, strain: 3f, lateral: 0.3f)` | `In(+1, false)` is dropped: Strain 2.97 (1e-5), Tire -0.002, Lateral 0.3. `In(-1, false)`: Lateral 0.3 - 0.8 x 0.02 / (10 x 1.0471976) = 0.2984721 (1e-6), Tire 0.016, Strain 3.0075 (1e-5). Lateral exactly 0 with `In(-1, false)`: dropped (drain). `In(-0.4, false)`: dropped; `In(-0.6, false)`: counts. NaN steer: dropped |
| 14 | `FightSim_SameWaySteer_NoPushHalfRateTire` | medium, two sims same seed, `RunState(+1, false, -0.5f, 10f)`; A `In(0, false)`, B `In(+1, false)` (toward the line, same side as the fish) | B.Lateral == A.Lateral (1e-7: no push); B dStrain 0.0075 (k 0.5); B.Tire 0.016 |
| 15 | `FightSim_CounterDuringBackPull_SlowerTireCounterStrain` | medium, `RunState(+1, true, 0.5f, 10f)`, `In(-1, false)` | Tire 0.016 (0.8/s, not 1.25); dStrain 0.012 (k 0.8) |
| 16 | `FightSim_ConeEdge_FlipsTheRunWithoutADraw` | medium, `RunState(+1, false, 0.9999f, 10f)`, `In(0, false)` | after one tick Lateral == 1, BurstDir == -1, `DrawCount` unchanged; after 10 more ticks Lateral < 1 |
| 17 | `FightSim_Tire_RaisesReelSpeedUpToThreeTimes` | `RestState(10f, tire: x)`, `In(0, true)`, one tick | medium dLine: tire 0 -0.012, 2.5 -0.024, 5 -0.036, 50 -0.036, -3 -0.012; small tire 0 -0.0144; large tire 0 -0.0108 (1e-5) |
| 18 | `FightSim_TireRates_SteerCounterDecayNoFloor` | medium | rest steer toward the line: +0.016 per tick; counter on a plain run: +0.025; no steer: -0.002; reel only: -0.002 (reeling never tires); from 0 with no input for 100 ticks: -0.2 (no floor) |
| 19 | `FightSim_BackPull_TakesLineHalvedWhileReeling` | medium, `RunState(+1, true, 0.5f, 12f)` x 200 ticks | `In(0, false)`: LineLength 17.252 +- 0.01; `In(0, true)` from the same state: 12.226 +- 0.01. **Built with a correction (review finding 8):** reeling into a medium back pull adds 0.0375 per tick and snaps at tick 187, before the 200 ticks, so the reeling half uses a Handmade copy with `LineStrength` 1000 (it must not snap; the strain rules are tested elsewhere) |
| 20 | `FightSim_LandsAtOneMetreWhileReeling` | medium `RestState(1.5f)`, `In(0, true)` | None after 41 ticks (1.008), Landed on tick 42; a sim at `RestState(0.5f)` with `In(0, false)` x 5 stays None |
| 21 | `FightSim_Timeout_EscapesAtOneTwentySeconds` | medium natural fight, seed 7, line 12, `In(0, false)` until end | Escaped / Timeout on tick 6000 +-1 (float accumulation of `Elapsed`), `Elapsed` 120 +- 0.021; never ThrewHook |
| 22 | `FightSim_SameTick_LandedThenSnappedThenTimeout` | large | `RunState(+1, false, 0.5f, 1.005f, strain: 6.99f)` + `In(0, true)`: Landed. `RunState(+1, false, 0.5f, 10f, strain: 6.99f)` with `Elapsed = 119.99f` + `In(0, true)`: Snapped. `RestState(10f)` with `Elapsed = 119.99f` + `In(0, false)`: Escaped / Timeout |
| 23 | `FightSim_WeightT_DoesNotChangeTheFight` | two runner sims, weightT 0 and 1, seed 99; input `In(sin(i x 0.05), i % 7 != 0)` x 3000 or until ended | every `FightState` field equal on every tick |
| 24 | `FightSim_SameSeedAndInputs_IdenticalState` | as the pre-R72 test, `SameState` comparing every new field | equal every tick; a seed-100 sim differs from seed 99 somewhere |
| 25 | `FightSim_Assist_ScalesGainsOnly` | small, assist, `RunState(+1, false, 0.5f, 18f)`, `In(0, true)` | first dStrain 0.01584 (1e-6); Snapped on tick 442 +-1; from `RestState(10f, strain: 3f)` with `In(0, false)`: 2.97 (drain unchanged); `SnapTension` 7/6 |
| 26 | `FightSim_Pity_LowerGainsAndLongerHeadroom` | small, two sims seed 4242, one with `pity: true`; `RunState(+1, false, 0.5f, 18f)`, `In(0, true)` | pity first dStrain 0.02244 (1e-6); `SnapTension` 1.25; pity Snapped on tick 335 +-1, normal on 266 +-1; `DrawCount` equal on every tick |
| 27 | `FightSim_NearSnapRecovery_OverTheFullBarThenUnderHalf` | `RestState(10f, strain: 6.1f)`, `In(0, false)` | `NearSnapRecoveries` 0 after 103 ticks and 1 after 104 (6.1 - 104 x 0.03 = 2.98 < 3.0); from strain 5.9 (never over full) it stays 0 |
| 28 | `FightSim_Junk_DragsWithoutStrainAndThrowsTheHookWhenSlack` | `ForJunk(handmade, tuning, 5, 10.03f)` | `In(0, true)` x 50: line 7.03 (1e-3), Strain and Tension 0, `DrawCount` 0; Landed on tick 151; a second junk sim with `In(0, false)`: ThrewHook on tick 600 |
| 29 | `FightSim_Exhausted_AtTireRunFull_NotLatched` | medium, `RestState(10f, lateral: 0.3f, tire: 19.99f)` | `Exhausted` false; one tick `In(-1, false)`: Tire 20.006, `Exhausted` true; 10 ticks `In(0, false)`: 19.986, false |

Tests 1-29 use `BaseProfiles()` (quirk-free Rust). The quirk tests below prove that each quirk changes behaviour, and that no quirk touches strain. `M(i)` = `FishingTestData.MotionProfiles()[i]`.

| # | Test | Setup and input | Expected |
|---|---|---|---|
| 30 | `FightSim_Quirk_BurstRampEasesTheRunIn` (Steady, Runner) | base medium copy A with `BurstRamp = 0.8f`, copy B without; same seed 4242; both `RunState(+1, false, 0.2f, 10f)`, `In(0, false)` x 60 | per-tick dLateral(A) / dLateral(B) == min(1, k x 0.02 / 0.8) at k = 1, 20, 40, 60 (1e-4). Same check on dLine with `RunState(+1, true, 0.2f, 10f)`. With `In(0, true)` the dStrain of A and B are equal on every tick (the ramp never touches strain) |
| 31 | `FightSim_Quirk_FlipRateSwitchesSidesMidRun` (Darter) | `M(ProfileDarter)`, seeds 1..20, `RunState(+1, false, 0.05f, 10f, phaseTimer: 1000f)`, 750 ticks of `In(Lateral > 0 ? -1 : 1, false)` | mid-run flips = BurstDir changes where `|Lateral| < 0.999` after the tick (cone flips excluded). Hazard = flips / (DrawCount delta x 0.02) in [0.8, 1.2] (the flip checks are the only draws in a forced long run). Every gap between consecutive flips is >= 0.3 - 1e-4 s. The same script on the base medium profile gives zero mid-run flips and a DrawCount delta of 0 |
| 32 | `FightSim_Quirk_LateralJitterSetsAPerRunSpeed` (Thrasher) | `M(ProfileThrasher)` copy with `FlipRate = 0f`; one sim; 60 times: `ForceState(RestState(16.5f, phaseTimer: 0f))` (line at or past the back gate, so no back pull moves the radius), one tick `In(0, false)`, read `RunSpeedMult`, then one more tick | every mult in [0.7, 1.3], max - min > 0.2, `DrawCount` +4 per run start. The second tick's dLateral == BurstDir x 1.2 x PullWave x 1.0 x mult x 0.02 / (16.5 x 1.0471976) (1e-6). A copy with `LateralJitter = 0f` draws +3 and `RunSpeedMult` == 1 |
| 33 | `FightSim_Quirk_WavePeriodSpeedsTheSurge` (Thrasher) | `FightSim.Wave(t, 1.5f)`; `M(ProfileThrasher)` sims, seeds 1..10 | Wave(0.375) 1, Wave(0.75) 2, Wave(1.125) 1, Wave(1.5) 0.2 (1e-5). The constructor makes 1 draw. Over 75 ticks (one 1.5 s period) the sum of `PullWave` x 0.02 is 1.515 (1e-2): same mean as Rust, faster surges. The base medium sim needs 200 ticks for one period |
| 34 | `FightSim_Quirk_CalmDriftCreepsLineOutAtRest` (Bulldog) | `M(ProfileBulldog)`, `RestState(10f)` (phaseTimer 100), `In(0, false)` x 250 | line grew 0.06 x (2.5 / 2 + 2.5) = 0.225 (+-0.005), with dLine at tick 1 < dLine at tick 125 (the ramp). From `RestState(10f, tire: 10f)`: 0.1139 (+-0.003, the drift scales with 1 - tire01). From tire 20.5 (exhausted): no growth (1e-6). In a run (`RunState(+1, false, 0.5f, 10f)`) there is no drift: dLine == 0 |
| 35 | `FightSim_Quirk_ProfileTimingAndBackChance` | each `M(i)`, seeds 1..500: a run start (`RestState(10f, phaseTimer: 0f)`, one tick) and a rest start (`RunState(+1, false, 0.5f, 10f, phaseTimer: 0f)`, one tick) | back share = profile `DiveChance` +- 0.05 (steady 0.15, darter 0.15, thrasher 0.20, bulldog 0.55, runner 0.30); every run duration (PhaseTimer + 0.02) in [BurstMin, BurstMax]; every rest in [CalmMin, CalmMax] (1e-5) |
| 36 | `FightSim_Quirks_NeverTouchTheStrainRules` | each `M(i)` against its quirk-free base copy, same seed, one tick each | dStrain identical (1e-7): side-run reel = 2.2 x mod x 0.75 x 0.02; back reel = 2.5; rest reel = 0.5; counter only = 0.8; release from strain 3 = -0.03. dTire identical. Rest reel dLine = -0.6 x ReelInMult x 0.02 (2e-5: Bulldog's first-tick creep is about 1e-5) |
| 37 | `FightSim_FiveStyles_SameSeedSameState` | each `M(i)`, two sims seed 555, script `In(sin(i x 0.05), i % 7 != 0)` x 3000 or until ended | identical `FightState` on every tick; a seed-556 sim differs somewhere |

Other F2 test edits:

**`RodStateMachineTests`:**
- `ForceSnapState(m)` (drop the `snapTimer` parameter): `new FightState { LineLength = 10f, Lateral = 0.7f, Strain = 6.999f, Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 5f }`. One `In(0f, true)` tick snaps: carp is large, +0.04125.
- `ToLanded`: `ToHooked(m, null, 1.02f)`, then `m.Fight.ForceState(new FightState { LineLength = 1.02f, Phase = FightPhase.Calm, PhaseTimer = 5f })`, then exactly 2 reel ticks (1.0092, 0.9984).
- `RodStateMachine_Hooked_LineAtLandDistanceWhileReeling_LandsWithFightReport`: same force. Landed on reel tick 2; `Report.MaxTension` = 2 x 0.009375 / 6 = 0.003125 (1e-6); `FightSeconds` 0.04 (1e-5).
- The FightReport echo case: `LineLength = 1.03f`. Landed on tick 3; drop `FishStamina`.
- `RodStateMachine_Hooked_PinnedTooLong_EscapesPinned` becomes `RodStateMachine_Hooked_FightTimeout_EscapesTimeout`: `ForceState { LineLength = 10f, Phase = Calm, PhaseTimer = 5f, Elapsed = 119.99f }`, `In(0f, false)`, Escaped / Timeout in the machine and in `SendResult`.
- `RodStateMachine_Hooked_SlackTooLong_EscapesThrewHook` becomes `RodStateMachine_Hooked_JunkSlackTooLong_EscapesThrewHook`: `ToHooked(m, FishRoll(junk: true))`, then `ForceState { LineLength = 10f, SlackTimer = 11.99f }`.
- New `RodStateMachine_Fight_NewRunCuesDiveOnlyWhenItPullsBack`: `ToHooked(m)` and drain. Then 40 times: `m.Fight.ForceState(new FightState { LineLength = 10f, Phase = FightPhase.Calm, PhaseTimer = 0f })`, one `TickFight(In(0f, false), Fr())`, drain. Each run start draws from the same stream, so the back flag varies. Every tick raises exactly one of SideRun or Dive, and it is Dive if and only if `Fight.State.Back`. Over the 40 runs both occur.
- New `RodStateMachine_Fight_ExhaustedRisingEdgeCuesOnce`: `ForceState { LineLength = 10f, Lateral = 0.3f, Tire = 19.995f, Phase = Calm, PhaseTimer = 5f }`. `TickFight(In(-1f, false))` raises Exhausted. 20 more ticks raise no Exhausted, SideRun or Dive.
- Remove every `FishStamina` and `PinnedTimer` initializer.

**`FishingHostLedgerTests`** (carp is large, handmade: `MaxReelSpeed` 0.6 x 0.9 x 3 = 1.62; LineLength0 8.5; landDistance 1):
- Fish floor = 0.8 x 7.5 / 1.62 - 0.5 = **3.2037 s**. Junk floor = 0.8 x 7.5 / 3 - 0.5 = **1.5 s**. Twig: 0.54 x 0.9 x 3 = 1.458, floor **3.6152 s**.
- `LandedBelowDurationFloor`: Tick 3.1 gives Invalid; Tick 3.3 gives Landed.
- `JunkLanded_UsesJunkDragSpeedFloor`: junk at 1.6 Landed; fish at 1.6 Invalid ("a fish needs 3.2 s"); junk at 1.4 Invalid ("below 1.5 s").
- `JunkCleanClaim_NoBonus`: Tick 1.6 and `Clean(1.6f)`.
- `ImplausibleCleanClaim_KeepsRolledQuality`: claims `{ 12.5f, 3.0f }`; update the comment ("may not undercut the 3.2 s floor").
- `LandedFloor_UsesTheSelectedRodReelSpeed`: `floor = CatchValidator.LandedFloorSeconds(8.5f, 1f, 0.54f * 0.9f * 3f)`, expect 3.6152 (1e-3); early at floor - 0.08 Invalid, floor + 0.05 Landed; update the comment.
- `LateHook_WalkBoundIsInactiveAndLineFloorsAtLandDistance`: expects 1f.
- `FishingHostLedger_ResultValidity_FollowsHookedState`: change the Timeout row's comment to "host-bounded: 10 s is far below FightTimeout"; the expected values are unchanged.
- New `FishingHostLedger_OwnerTimeout_AcceptedOnlyOnTheHostClock`: hook, `Tick(l, 118.5)`, then `Result(Escaped, Timeout)` gives Escaped / Timeout and Announce Lost. A second cast hooked and ticked 117.5 gives Escaped / Invalid and Announce None.

**`CatchValidatorTests`** (new):
- `CatchValidator_MaxReelSpeed_IsRodTimesReelInTimesTireMax`: handmade + Steady tier gives 2.16; twig + Runner tier gives 1.458; handmade + Darter gives 1.8 (1e-6).
- `CatchValidator_OwnerTimeoutPlausible_UsesTwoSecondSlack`: THooked 100; now 217.99 false, 218.0 true (fightTimeout 120).

**Filter:** `FishGame.Tests.EditMode.(FightSimTests|RodStateMachineTests|FishingHostLedgerTests|CatchValidatorTests|CastTipReachTests|FishingBots)`.

### Steps and commit
1. Write the tests and do a red run (expected red: missing members).
2. Write F2.1-F2.7 and do a green run with the filter.
3. Run the full `test-edit`, then `test-play`. `SoloHost_CastBiteFight_Lands` must land a bluegill with the new driver.
   - These PlayMode runs use the OLD shipped values (drain 0.45, Handmade reel 1.0, pre-R72 timing) until F3.
   - The bluegill should still land well within 100 s. If it is slow or fails, report the numbers; do not pull F3's data changes forward.
4. Delete `FightMotionTests.cs` and its `.meta` with `git rm`. Do the ASCII scan.
5. Commit: `feat(fight): Rust-faithful FightSim baseline (R72) with five per-profile quirks - input-only strain, run/rest flags, tire, 120 s timeout; host floor uses max reel, accepts owner Timeout`.

### Acceptance (reviewer, Opus high)
- The `FightSim` code matches F2.3 line for line in logic and order.
- The draw table (0.9) is pinned by tests 7-10 (Rust draws) and 31-32 (the flip check and jitter draws).
- Each quirk has a test that proves it changes behaviour (30-35), and test 36 proves no quirk touches the strain formula, tire or reel.
- Every research 8.7 number is asserted (tests 1-5).
- The host never trusts an owner value: the floor uses the host rod and the rolled profile, and the Timeout claim uses the host clock.
- `SnapTimer` keeps feeding `FlagGrace` (controller unchanged in F2).
- PlayMode green.
- The comments name Rust as the source and paste no code.

---

## Task F3 - Data: Rust values in the shipped content

- **Goal:** the assets the game and the bots load carry the R72 shared values from section 0. Same values as `FishingTestData` after F2.
- **Files:**
  - Editor: `Assets/_Project/Editor/Builders/Fishing/FishingSeedData.cs`, `Assets/_Project/Editor/Validation/FishingValidators.cs`.
  - Assets: `Assets/_Project/Content/Fishing/FishingTuning.asset`, `Assets/_Project/Content/Fishing/Rods/{rod_twig,rod_handmade,rod_carbon,rod_deepsea}.asset`, `Assets/_Project/Content/Fishing/FightProfiles/{steady,darter,bulldog,thrasher,runner}.asset`.
  - Tests: `Assets/_Project/Tests/EditMode/{FishingContentTests,FishingValidatorTests,ContentRegistryTests}.cs`.

### Value changes (seed, YAML, spec expectation in `FishingData_SeedsMatchSpecTables`)

- **Tuning:**
  - `landDistance` 2 -> 1
  - `relaxRate` 0.45 -> 1.5
  - `strainScale` 1 -> 0.75
  - `strainScaleAssist` 0.6 -> 0.45
  - `lateralMaxDeg` 75 -> 60
- **Rods** (`reelSpeed`, `steerRate`):
  - twig 0.54, 0.67
  - handmade 0.6, 0.8
  - carbon 0.72, 0.93
  - deepsea 0.78, 1.07
- **Profiles: the R72 quirks (section 0.3).** "was" is the current seed and YAML value. `wavePeriod` already landed in F1. Every field not listed is unchanged.

| Profile | calmMin / calmMax | burstMin / burstMax | diveChance | flipRate / flipMinGap | burstRamp | calmDrift / calmDriftRamp | lateralJitter |
|---|---|---|---|---|---|---|---|
| steady | 1.0 / 2.5 -> 2 / 4 | 1.5 / 2.5 -> 3 / 5 | 0 -> 0.15 | 0 / 0.5 (same) | 0.6 (same) | 0 / 0 (same) | 0 (same) |
| darter | 0.3 / 0.8 -> 1 / 2 | 0.4 / 0.9 -> 2 / 3.5 | 0 -> 0.15 | 1.0 / 0.3 (same) | 0 (same) | 0 / 0 (same) | 0 (same) |
| thrasher | 1.0 / 2.5 -> 1.5 / 3 | 1.5 / 3.0 -> 3 / 5 | 0 -> 0.20 | 0.5 -> 0.4 / 0.5 | 0 (same) | 0 / 0 (same) | 0.3 (same) |
| bulldog | 1.0 / 3.0 -> 2 / 4 | 2.0 / 4.0 -> 2.5 / 4.5 | 0.35 -> 0.55 | 0 / 0.5 (same) | 0 (same) | 0.12 -> 0.06 / 2.5 | 0 (same) |
| runner | 1.0 / 2.0 -> 2 / 3.5 | 2.5 / 4.0 -> 5 / 9 | 0.25 -> 0.30 | 0.1 -> 0 / 0.5 | 0.8 (same) | 0 / 0 (same) | 0 (same) |

- Update the comment above the `Profile` rows in the seed data and in the content test: "R72: Rust tiers plus the five per-profile quirks (spec 9.6); the quirks change only how the fish moves".

### Range changes (in all three places: `FishingValidators`, `FishingValidatorTests.Rows`, `FishingContentTests.FishingData_Ranges_Valid`)

- `landDistance` [1.5, 3] -> [0.5, 3]
- `relaxRate` [0.3, 0.7] -> [0.5, 3]
- `strainScale` [0.5, 1.5] -> [0.3, 1.5]
- `strainScaleAssist` [0.3, 1] -> [0.2, 1]
- rod `reelSpeed` [0.5, 2] -> [0.3, 2]
- profile `burstMax` [burstMin, 8] -> [burstMin, 10] (Runner runs up to 9 s)

### Other test edits

- `ContentRegistryTests` line ~537: bulldog `DiveChance` 0.35 -> 0.55.
- `FishingData_ThrasherCalm_AtLeastOneSecond` and its validator rule still hold (calm 1.5-3).
- `TunedDeviations` is unchanged. Its three keys belong to fields F7 removes.
- `CastTipReachTests` read the shipped assets and must stay green: the minimum cast still starts above 1.0 + 0.25.

**Filter:** `FishGame.Tests.EditMode.(FishingContentTests|FishingValidatorTests|ContentRegistryTests|CastTipReachTests|FishingDefsTests)`.

### Steps and commit
1. Update the test expectations and do a red run.
2. Change the seeds, the YAML and the validators, then do a green run.
3. Run the full `test-edit`; `FishingData_AssetValuesEqualSeeds` (F1) proves asset == seed.
4. Commit: `feat(fishing-data): R72 Rust values and the five profile quirks in shipped content - landDistance 1, drain 1.5, strain scale 0.75, rods, per-profile timing/back/flip/ramp/creep`.

### Acceptance (reviewer, Sonnet)
- Seed, YAML, validator and the three test tables agree with section 0.
- No field other than those listed changed.

---

## Task F4 - Bots: realistic casts, Greedy / Reckless / Pump / Rhythm

- **Goal:** the tuning bots measure the Rust baseline as players meet it (decision 4) and pin its signature behaviours (decisions 2 and 3).
- **Files:** `Assets/_Project/Tests/EditMode/Tuning/FishingBots.cs` (rewrite).
- **Keep from the current file:** `Loaded`/`LoadData`, `BatchStats`, `Compute`, `Nearest`, `Pct`, the formatting helpers, `Targets`, `In`, the `[OneTimeSetUp]` cache clear, `IsFinite*`, and the perf test with its `PerfDriver`. In `PerfDriver`, use Rhythm and a cast line of 12 m.

### F4.1 Bot code (write exactly)

```csharp
        // R72 bots (plan rework-fight F4). Casts are drawn like real play (decision 4): Uniform(10, 14) m from the tip, weightT = u^2.2.
        // Weight cannot change a baseline fight (FightSimTests 23); it is drawn so a later weight term is measured at real weights.
        enum BotKind { Greedy, Reckless, Pump, Rhythm }

        const float CastMin = 10f, CastMax = 14f, WeightSkew = 2.2f;
        const float EaseOn = 0.85f, EaseOff = 0.30f, RestSteerDeadband = 0.05f;
        const int FightsPerBatch = 200;
        static readonly BotKind[] Bots = { BotKind.Greedy, BotKind.Reckless, BotKind.Pump, BotKind.Rhythm };
        static readonly string[] ProfileIds = { "steady", "darter", "bulldog", "thrasher", "runner" };
        static readonly string[] RodIds = { "rod_handmade", "rod_twig" };

        static uint SeedFor(int i) { return unchecked(2654435761u * (uint)(i + 1)); }

        // Per-fight cast distance and weight from their own Pcg32 stream (stream 7), independent of the fight seed.
        static void CastFor(int i, out float line, out float weightT)
        {
            var r = new Pcg32(SeedFor(i), 7);
            line = CastMin + (CastMax - CastMin) * r.NextFloat();
            weightT = (float)Math.Pow(r.NextFloat(), WeightSkew);
        }

        static void Ease(float tension, ref bool easing)
        {
            if (tension >= EaseOn) easing = true;
            else if (easing && tension <= EaseOff) easing = false;
        }

        // What a player can see: the run and its side (splash), a back pull (plunge), tension (creak / gauge), the fish's side of the line.
        static FightInput Decide(BotKind kind, in FightState s, ref bool easing)
        {
            bool run = s.Phase == FightPhase.Burst;
            switch (kind)
            {
                case BotKind.Greedy:                                   // reels non-stop, never steers
                    return new FightInput(0f, true);
                case BotKind.Reckless:                                 // reels non-stop and counter-steers every run (decision 3: careless still snaps)
                    return new FightInput(run ? -s.BurstDir : 0f, true);
                case BotKind.Pump:                                     // never steers; reels and lets go on the creak
                    Ease(s.Tension, ref easing);
                    return new FightInput(0f, !easing);
                default:                                               // Rhythm: the Rust pump
                    Ease(s.Tension, ref easing);
                    if (easing) return new FightInput(0f, false);
                    if (run) return new FightInput(s.BurstDir * s.Lateral > 0f ? -s.BurstDir : 0f, false);
                    float toLine = s.Lateral > RestSteerDeadband ? -1f : s.Lateral < -RestSteerDeadband ? 1f : 0f;
                    return new FightInput(toLine, true);
            }
        }

        static FightRecord RunFight(BotKind kind, in FishFightProfile p, in RodStats rod, in FishingTuning t, int i)
        {
            CastFor(i, out float line, out float weightT);
            var sim = new FightSim(p, rod, t, weightT, SeedFor(i), line, false, false);
            bool easing = false;
            int maxTicks = (int)Math.Ceiling(t.MaxFightTime / FightSim.Step);   // the sim ends at FightTimeout first
            for (int k = 0; k < maxTicks; k++)
                if (sim.Tick(Decide(kind, sim.State, ref easing)) != FightOutcome.None) break;
            FightState f = sim.State;
            return new FightRecord { Outcome = f.Outcome, Reason = f.Reason, Elapsed = f.Elapsed, MaxTension = f.MaxTension, Recoveries = f.NearSnapRecoveries };
        }
```

### F4.2 `BatchStats` additions

- Add `TimeoutPct`: the count of `Escaped` with `Reason == Timeout`. The bot-side cap is no longer reachable: the sim times out at 120 s before `MaxFightTime` 240 s.
- `BotLine` prints `to=` and drops the pin/slack split.
- `EnsureMatrix` loops profiles x rods x bots; there is no weight dimension any more.
- Cache key: `bot|profile|rod`.
- **Signature stats** (a small `SigAccum`, collected for Rhythm on Handmade only). It compares the state before and after each tick:
  - A rest-to-run transition counts a run, plus a back run when `Back` is set on the new run. Runs of zero length (a spent fish) are not counted.
  - Run ticks are summed as run seconds.
  - A `BurstDir` change while the fish runs before and after the tick, with `|Lateral| < 0.999` after it, counts a mid-run switch. Cone flips are excluded.
  - Output: back share = back runs / runs; mean run = run seconds / runs; switch rate = switches / run seconds.

### F4.3 Tests and targets (spec 9.12 after F0)

| Test | Target (every assertion goes through `Targets.Add`) |
|---|---|
| `FishingBots_Decide_PinsTheFourBots` (pure) | Greedy `(0, true)` in any state. Reckless in a run +1: `(-1, true)`; at rest `(0, true)`. Pump: tension 0.85 starts easing (`reel false`), 0.6 still easing, 0.29 reels; never steers. Rhythm: run +1 at lateral +0.2 gives `(-1, false)`; run +1 at lateral -0.2 gives `(0, false)` (steer would be dropped); rest at lateral 0.3 gives `(-1, true)`; rest at 0.02 gives `(0, true)`; easing gives `(0, false)` |
| `FishingBots_Greedy_SnapsEveryCell` | snap >= 95% for 5 profiles x 2 rods; Handmade steady median snap time in [5.5, 9.0] s (model 7.4; decision 3); Handmade runner median in [2.8, 3.8] s (Rust 3.4) |
| `FishingBots_Reckless_StillSnaps` | snap >= 95% for darter, thrasher, bulldog and runner on both rods; steady >= 50% on both rods (model 74% Handmade, 100% Twig) |
| `FishingBots_Pump_NeverSnapsButBigFishTimeOut` | snap <= 5% in every cell; Handmade steady land >= 85%; Handmade bulldog timeout >= 60% (model 92%), Handmade runner timeout >= 30% (model 52%) |
| `FishingBots_Rhythm_LandsSafelyInBaselineTimes` | every cell: land >= 95%, snap <= 2%, timeout <= 2%. Median Handmade: steady [10, 20], darter [13, 27], thrasher [13, 27], runner [20, 41], bulldog [23, 44]. Twig: steady [10, 20], darter [15, 30], thrasher [16, 31], runner [22, 44], bulldog [23, 45]. Each band is the model median +- about 35% |
| `FishingBots_FiveStyles_AreDistinct` (user Q1: recognisable blind) | Rhythm, Handmade, signature stats. **Back share:** bulldog >= 40% and greater than every other profile; runner in [15%, 40%]; steady, darter and thrasher <= 25%. **Mean run:** runner >= 3.8 s; steady in [3.0, 4.4]; thrasher in [2.6, 3.8]; darter <= 2.7; bulldog <= 3.1; ordered runner > steady > thrasher > darter. **Switches per run-second:** darter >= 0.5; thrasher in [0.15, 0.55]; steady, bulldog and runner <= 0.05. Model: back 15 / 14 / 18 / 52 / 27%, mean run 3.66 / 2.21 / 3.20 / 2.60 / 4.34 s, switches 0 / 0.78 / 0.33 / 0 / 0 (steady / darter / thrasher / bulldog / runner) |
| `FishingBots_AllProfiles_PrintDistributions` | prints `FG DATA` (tuning: strain table, drain, timeout; rods; profiles: tier, timing, back, flip, ramp, creep, jitter, wave), every `FG BOT` line, an `FG SIG` line per profile (back share, mean run, switch rate), and `FG FUN` lines (Rhythm clean%, recovery%, overall snap per bot); asserts every number is finite or n/a |
| `FishingBots_FightSimStep_UnderFiftyMicroseconds` | unchanged |

Delete `FishingBots_RhythmDecide_PinsDiveEasingAndSteerRules` and the `CounterHold` constant.

**Filter:** `FishGame.Tests.EditMode.FishingBots`.

### Steps and commit
1. Write the file and run the filter.
2. If a target misses: do NOT change any Rust value or widen a band. Add a `TuningItems` entry for the test, as `"<test>": "<measured numbers>; R72 baseline, controller to decide"`, which turns it into an ignored test with its numbers printed. Report it.
3. Run the full `test-edit`.
4. Paste every `FG BOT`, `FG FUN` and `FG TARGET` line into the task report; F8 records them.
5. Commit: `test(bots): R72 bots at 10-14 m casts - Greedy, Reckless, Pump, Rhythm with baseline targets and a five-style signature test`.

### Acceptance (reviewer, Opus high)
- The bots only use what a player can see.
- Rhythm never reels into a run or a back pull, and never steers when the steer would be dropped.
- Targets match spec 9.12.
- Any `TuningItems` entry carries numbers.
- Decision 3 is visible: Greedy and Reckless still snap a Steady, with the median snap time printed (about 7 s).
- The five-style signature test passes without a `TuningItems` entry, or its miss is reported with numbers.

---

## Task F5 - Core view math, creak curve and wire flags

- **Goal:** the pure pieces every view needs: Rust's creak gain, the bar smoothing, the bobber tilt, the gauge geometry and the run side on the wire.
- **Files:**
  - Core: `Assets/_Project/Core/Fishing/{TensionCues,TensionCueState,TensionBand,RodWireQuant}.cs`, new `Assets/_Project/Core/Fishing/FightViewMath.cs`.
  - Fishing: `Assets/_Project/Fishing/Owner/{RodControllerRules,FishingRodController}.cs`.
  - Tests: `Assets/_Project/Tests/EditMode/{TensionCuesTests,RodControllerHelperTests,RodWireQuantTests}.cs`, new `Assets/_Project/Tests/EditMode/FightViewMathTests.cs`.

### F5.1 `TensionCues` (edit) and `TensionCueState` (+ `public bool CreakOn;`)

Add to `TensionCues`:
```csharp
        // Rust strainGainCurve (creak volume): keys (0.10, 0, flat) and (0.95, 1, end slope 2.66), clamped. Between two Unity curve keys the value is a
        // cubic Hermite: with out-tangent 0 and in-tangent 2.66 over the span s = 0.85 it is (1 - b) u^2 + b u^3, b = s * 2.66 - 2 = 0.261.
        public const float CreakStart = 0.10f, CreakFull = 0.95f, CreakEndSlope = 2.66f;

        public static float CreakGain(float tension)
        {
            if (!(tension > CreakStart)) return 0f;     // NaN -> 0
            if (tension >= CreakFull) return 1f;
            float span = CreakFull - CreakStart;
            float u = (tension - CreakStart) / span;
            float b = span * CreakEndSlope - 2f;
            return (1f - b) * u * u + b * u * u * u;
        }
```
In `Evaluate`:
- Set `CreakVolume01 = CreakGain(t)` and `CreakOn = inGrace || t > CreakStart`.
- `CreakPitch` is unchanged (`1 + 1.1 x clamp01((t - 0.6) / 0.4)`).
- Comments: `TensionBand.Grace` now means "over the full bar (strain > StrainBarFull; R72 headroom)". `BandOf` is unchanged.

### F5.2 `FightViewMath.cs` (write exactly)

```csharp
// Core/Fishing/FightViewMath.cs: pure presentation maths of the R72 fight (spec 9.11). Rust client values: UI bar smoothing clamp01(dt * 10),
// bobber roll +-30 deg toward the pull side, halved on a back pull, eased at clamp01(dt). Authority: none (every peer, view only).
namespace FishGame.Core
{
    public static class FightViewMath
    {
        public const float BarSmoothRate = 10f;
        public const float TiltMaxDeg = 30f;
        public const float CreakFadeSeconds = 0.25f;

        // shown += (target - shown) * clamp01(dt * 10). A non-finite or negative dt keeps shown.
        public static float SmoothBar(float shown, float target, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return shown;
            return shown + (target - shown) * Clamp01(dt * BarSmoothRate);
        }

        // 0 = pulling left, 1 = pulling right, 0.5 = at rest; a back pull moves the target halfway back to 0.5.
        public static float TiltTarget(bool run, bool right, bool back)
        {
            if (!run) return 0.5f;
            float t = right ? 1f : 0f;
            return back ? (t + 0.5f) * 0.5f : t;
        }

        public static float TiltStep(float tilt01, float target, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return tilt01;
            return tilt01 + (target - tilt01) * Clamp01(dt);
        }

        // +30 deg (leaning to the player's left) at tilt 0, -30 at tilt 1.
        public static float RollDeg(float tilt01) => TiltMaxDeg - 2f * TiltMaxDeg * tilt01;

        // Fraction of the gauge track for a display tension when the track ends at the snap point.
        public static float GaugeFraction(float tension, float snapTension)
        {
            if (float.IsNaN(tension)) return 0f;
            return snapTension > 0f ? Clamp01(tension / snapTension) : Clamp01(tension);
        }

        // Creak loop gate: 1 while wanted (and the fade is re-armed); then a linear fade over CreakFadeSeconds; stop = true when it has finished.
        public static float CreakFadeStep(bool wanted, ref float fadeLeft, float dt, out bool stop)
        {
            stop = false;
            if (wanted) { fadeLeft = CreakFadeSeconds; return 1f; }
            if (dt > 0f && !float.IsInfinity(dt)) fadeLeft -= dt;
            if (fadeLeft <= 0f) { fadeLeft = 0f; stop = true; return 0f; }
            return fadeLeft / CreakFadeSeconds;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
```

### F5.3 Wire flags

**`RodWireQuant`:** add `public const byte FlagRight = 64;   // R72: the run pulls right (with FlagBurst); bobber tilt on every peer`.

**`RodControllerRules.HookedFlags`:** new signature and body:
```csharp
        // R72: FlagBurst while the fish runs (not when spent), FlagRight for a right pull, FlagDive for a back pull; FlagGrace while the
        // strain is over the full bar (SnapTimer > 0).
        public static byte HookedFlags(FightPhase phase, sbyte burstDir, bool back, bool reeling, bool exhausted, float snapTimer)
        {
            byte flags = 0;
            if (phase == FightPhase.Burst && !exhausted)
            {
                flags |= RodWireQuant.FlagBurst;
                if (burstDir > 0) flags |= RodWireQuant.FlagRight;
                if (back) flags |= RodWireQuant.FlagDive;
            }
            if (reeling) flags |= RodWireQuant.FlagReel;
            if (exhausted) flags |= RodWireQuant.FlagExhausted;
            if (snapTimer > 0f) flags |= RodWireQuant.FlagGrace;
            return flags;
        }
```
**`FishingRodController.PublishView`:** `RodControllerRules.HookedFlags(fs.Phase, fs.BurstDir, fs.Back, _lastFightInput.Reel, fight.Exhausted, fs.SnapTimer)`.

`FishingRodNetwork`'s remote cue code is unchanged: the rising edge of `FlagBurst` picks Dive when `FlagDive` is set. `RodCueAudio` is unchanged.

### F5.4 Tests (write first)

**`TensionCuesTests`:**
- `TensionCues_ExactValues_AtSpecPoints`: the 0.8 creak line becomes `CreakVolume01 == 0.646964` (1e-5).
- New `TensionCues_CreakGain_FollowsRustCurve`:
  - gains: 0.05 -> 0, 0.10 -> 0, 0.2 -> 0.010653, 0.3 -> 0.044313, 0.525 -> 0.217375, 0.8 -> 0.646964, 0.9 -> 0.872213, 0.95 -> 1, 1.2 -> 1, NaN -> 0 (1e-5);
  - non-decreasing over 0..1 in steps of 0.01;
  - `CreakOn` false at 0.10, true at 0.11, true at 0 when `inGrace`.
  - Catches: a linear ramp, a curve starting at 0.6 (the pre-R72 band), a wrong tangent.

**`FightViewMathTests`:**
- `FightViewMath_SmoothBar_RustTenPerSecond`: (0, 1, 0.02) -> 0.2; (0.5, 1, 0.05) -> 0.75; (0, 1, 0.5) -> 1; dt 0 -> unchanged; dt NaN -> unchanged.
- `FightViewMath_Tilt_TowardThePullHalvedOnBackPull`:
  - `TiltTarget`: (false, any, any) 0.5; (true, false, false) 0; (true, true, false) 1; (true, false, true) 0.25; (true, true, true) 0.75.
  - `TiltStep`: (0, 1, 0.25) -> 0.25; (0, 1, 1.5) -> 1.
  - `RollDeg`: 0 -> 30, 1 -> -30, 0.5 -> 0, 0.25 -> 15, 0.75 -> -15.
- `FightViewMath_GaugeFraction_TrackEndsAtTheSnapPoint`: (1, 7/6) -> 0.857143; (7/6, 7/6) -> 1; (2, 7/6) -> 1; (-1, 7/6) -> 0; (0.5, 0) -> 0.5; NaN -> 0.
- `FightViewMath_CreakFade_QuarterSecondThenStop`: wanted gives 1 and fadeLeft 0.25; then not wanted with dt 0.1 gives 0.6, 0.2, then 0 and stop; wanted again re-arms.

**`RodControllerHelperTests`:** `RodControllerRules_HookedFlags_GraceFollowsSnapTimer_AndTheOtherBitsStay` becomes `RodControllerRules_HookedFlags_RunSideBackGraceAndSpent`:
- (Calm, 0, false, false, false, 0) -> 0
- (Burst, -1, false, false, false, 0) -> Burst
- (Burst, +1, false, false, false, 0) -> Burst | Right
- (Burst, +1, true, true, false, 0) -> Burst | Right | Dive | Reel
- (Burst, -1, true, false, false, 0) -> Burst | Dive
- (Burst, +1, true, false, true, 0) -> Exhausted only (a spent fish shows no run)
- (Calm, 0, false, false, false, 0.01) -> Grace
- all bits: (Burst, +1, true, true, false, 0.2) -> Burst | Right | Dive | Reel | Grace

**`RodWireQuantTests`:** `FlagRight == 64`, and it is distinct from every other flag bit.

**Filter:** `FishGame.Tests.EditMode.(TensionCuesTests|FightViewMathTests|RodControllerHelperTests|RodWireQuantTests)`.

### Steps and commit
1. Red run, implement, green run.
2. Full `test-edit`, then `test-play`: the feedback loop test still starts the creak at 0.7.
3. Commit: `feat(fight-view): Rust creak curve, bar smoothing, bobber tilt and gauge maths; FlagRight on the wire`.

### Acceptance (reviewer, Sonnet)
- The Hermite coefficients are derived in the comment, and the tests pin them to 1e-5.
- `HookedFlags` hides the run of a spent fish.
- No Unity type in Core.

---

## Task F6 - Presentation: creak fade, bobber tilt, HUD gauge

- **Goal:** the player sees and hears the R72 fight as specified in spec 9.11.
- **Files:**
  - `Assets/_Project/Presentation/Feedback/FishingFeedback.cs`
  - `Assets/_Project/Presentation/Rod/BobberView.cs`
  - `Assets/_Project/UI/Fishing/FishingHudPresenter.cs`
  - PlayMode tests: `Assets/_Project/Tests/PlayMode/{FeedbackSoloTests,RodViewSoloTests}.cs`

### F6.1 `FishingFeedback.UpdateLoops` (creak)

- Fields: `private float _creakFadeLeft; private float _creakVol, _creakPitch;`.
- `creak` = `hooked && cue.CreakOn`.
- **While hooked:**
  - If wanted: start the loop if null, set `_creakVol = cue.CreakVolume01` and `_creakPitch = cue.CreakPitch`, then `SetLoop(_creak, _creakVol, _creakPitch)`, after `FightViewMath.CreakFadeStep(true, ref _creakFadeLeft, dt, out _)`.
  - If not wanted and the loop exists: `float m = FightViewMath.CreakFadeStep(false, ref _creakFadeLeft, dt, out bool stop)`. If `stop`, call `StopLoop(ref _creak)`; otherwise `SetLoop(_creak, _creakVol * m, _creakPitch)`.
- **Not hooked:** stop at once, as today (the PlayMode test requires no loop within 2 frames of leaving Hooked).
- **`StopLoops()`** also resets `_creakFadeLeft = 0`.
- Pass `dt` (the caller's `Time.deltaTime`) into `UpdateLoops`.
- Update the file header comment: Grace = the strain is over the full bar (R72).

### F6.2 `BobberView` (tilt)

Steady phase, replacing the current roll block:
```csharp
                    bool hooked = vs.State == RodState.Hooked;
                    bool exhausted = hooked && (vs.Flags & RodWireQuant.FlagExhausted) != 0;
                    if (hooked && !exhausted)
                    {
                        bool run = (vs.Flags & RodWireQuant.FlagBurst) != 0;
                        bool right = (vs.Flags & RodWireQuant.FlagRight) != 0;
                        bool back = (vs.Flags & RodWireQuant.FlagDive) != 0;
                        _tilt01 = FightViewMath.TiltStep(_tilt01, FightViewMath.TiltTarget(run, right, back), dt);
                        _roll = FightViewMath.RollDeg(_tilt01);
                    }
                    else
                    {
                        _tilt01 = 0.5f;
                        float rollTarget = exhausted ? RollDeg * Mathf.Sin(RollRadPerSecond * now) : 0f;
                        _roll = Mathf.Lerp(_roll, rollTarget, 1f - Mathf.Exp(-10f * dt));
                    }
                    Place(_roll);
```
- Add the field `private float _tilt01 = 0.5f;` and reset it to 0.5 on baseline and on `OnEnable`.
- `Place(rollDeg)` rolls about the horizontal tip-to-bobber direction:
  - `Vector3 d = Position - TipPosition(); d.y = 0f;`
  - `Quaternion basis = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;`
  - `_bobber.SetPositionAndRotation(Position, basis * Quaternion.Euler(0f, 0f, rollDeg));`
  - A positive roll leans the top to the player's left, so a left pull gives +30.
- Rename the existing `RollDeg` constant to `ExhaustedRollDeg` to avoid the name clash.
- Header comment: add "Hooked: rolls +-30 deg toward the pull side (FlagRight), halved on a back pull (FlagDive), eased at clamp01(dt) (Rust, R72)".

### F6.3 `FishingHudPresenter` (gauge)

**Fields:**
- `_gaugeShown` (smoothed display tension).
- `_snapTension`.
- `_cleanTension` (from `tuning.CleanCatchMaxTension` in `ReadRegistry`; fallback 0.8).

**`TickGauge(float dt)`:**
- On hide, reset `_gaugeShown = 0`.
- `target = fight.State.Tension` (not clamped).
- `_gaugeShown = FightViewMath.SmoothBar(_gaugeShown, target, dt)` with the `Tick` dt.
- `_snapTension = fight.SnapTension`.
- `warn = _gaugeShown >= _cleanTension`.
- `grace = fight.State.SnapTimer > 0f` (over the full bar, unchanged).
- Repaint when the smoothed value moves by more than `RepaintEpsilon`. Remove `Mathf.Clamp01` on the target.

**`DrawGauge`** (Bar and Arc): the track spans `[0, _snapTension]`.
- Fill fraction = `FightViewMath.GaugeFraction(_gaugeShown, _snapTension)`.
- **Full mark** (2 px, `_text`) at `GaugeFraction(1f, _snapTension)`.
- **Clean tick** (the existing tick style) at `GaugeFraction(_cleanTension, _snapTension)`.
- **Headroom zone:** the part of the track from the full mark to the end, drawn in `_bad` at alpha 0.35 under the fill.
- Fill colour: `_accent` below the clean tick, `_warn` from it, `_bad` with the existing 6 Hz blink while `grace`.
- The Arc uses the same fractions over `ArcSpanDeg`.
- Replace the `WarnTension` constant with `_cleanTension`.

### F6.4 Tests

**PlayMode `FeedbackSoloTests.FeedbackSolo_Creak_StartsAboveTenPercentAndFadesOut`** (pattern of `FeedbackSolo_Loops_...`; the controller is disabled and the view is driven with `SetView`):
1. Hooked at tension 0.05: no `Loop_Line_Creak` after 0.3 s.
2. 0.2: the loop exists within 2 s.
3. Back to 0.05 (still Hooked): the loop still exists on the next frame, and is gone within 0.6 s.
4. 0.2 again, then Escaped: gone within 2 frames.

Catches: the creak gated at the old 0.6 band, an instant stop, or a fade that never stops.

**PlayMode `RodViewSoloTests.RodViewSolo_Bobber_TiltsTowardThePullHalvedOnBackPull`** (drive `OwnerSetView` like the T23-review tests; bobber at the tip + (0, -1, 6)):
- Hooked + `FlagBurst | FlagRight` for 4 s: the roll about the tip-to-bobber axis (`Vector3.SignedAngle` of `Vector3.up` and the bobber's up, around the flat axis) is -30 +- 2 deg.
- Add `FlagDive`: 4 s later, -15 +- 2.
- `FlagBurst` only (left): +30 +- 2.
- No `FlagBurst` (rest): 0 +- 2.

**HUD:** there is no automated HUD draw test (UI Toolkit screenshots are not allowed in batch). The geometry is pinned by `FightViewMathTests` (F5). Manual check M1 is in F8.

**Filters:**
- `test-play -Filter "FishGame.Tests.PlayMode.(FeedbackSoloTests|RodViewSoloTests)"`
- then the full `test-edit` and `test-play`.

### Steps and commit
1. Write the PlayMode tests and do a red run.
2. Implement F6.1-F6.3 and do a green run.
3. Run both full suites.
4. Commit: `feat(fight-feedback): creak from 10% with a 0.25 s fade, bobber tilt toward the pull, HUD gauge smoothed with the headroom to the snap point`.

### Acceptance (reviewer, Opus high)
- Remote peers tilt the bobber from the flags alone.
- Leaving Hooked stops the creak at once; the fade only runs while still hooked.
- The gauge never clamps the value before the headroom; the full mark sits at 1.0 and the clean tick at the tuning value.
- No per-frame allocation added.

---

## Task F7 - Cleanup: remove retired fight data and constants

- **Goal:** nothing in data or Core suggests a knob that no longer does anything. The quirk fields (R63 motion fields plus `WavePeriod`) are live and stay. Only `angleWindow` stays parked.
- **Remove from `FishingTuning`, `FishingTuningDef` (field, getter, `Configure`, `ToCore`), `FishingSeedData`, `FishingTuning.asset` (the YAML line), `FishingValidators`, `FishingValidatorTests.Rows`, `FishingContentTests` (ranges, `Tn`, rules), `FishingTestData` and `FishingDefsTests.TuningNames` (count 60 -> 47):**
  - `ReelStrainBurst`, `CounterRelief`, `SteerStrain`, `OutOfWindowStrain`, `SpoolStrain`, `RelaxWhileReeling`, `ReelBurstPenalty`, `DiveRunMult`, `SnapGrace`, `SnapGraceAssist`, `PinEscapeTime`, `FishStaminaRegen`, `CalmCenterPull`.
- **Remove from `FishFightProfile`, `FightProfileDef` (fields, getters, `Configure` parameters, `ToCore`), seeds, the 5 profile YAML files, validators, test rows, `FishingTestData`, the `FishingDefsTests` csv and every `Configure` caller:**
  - `PullStrength`, `LateralSpeed`, `RunSpeed`, `StaminaDrain`.
- **Remove from `FishingConstants`** (only when a grep finds no other reference):
  - `CalmLeadInSeconds`, `ExhaustedStamina`, `SideRunAgainstChance`, `CenterDeadzone`, `WeightFactorMin`, `WeightFactorMax`, `ReelRateFloor`.
  - Keep `PinnedLateral`: the parked `angleWindow` rule uses it. Change its comment to `// parked (R72): only the angleWindow validator rule reads it`.
- **Test edits:**
  - `FishingValidators_CrossFieldRules_ReportRuleText`: remove the `snapGrace` case.
  - `FishingValidators_NaNValue_IsReportedOutOfRange`: replace `"snapGrace"` with `"strainSnap"`.
  - `TunedDeviations`: remove all three keys.
  - `FishingData_SeedsMatchSpecTables`: drop the removed columns.
- **Comments:**
  - `FishFightProfile`: `// R72 quirks (spec 9.6): timing, back chance, flips, ramp, rest creep, per-run speed jitter, wave period; behaviour only, never strain`.
  - `RodStats`: `AngleWindow // parked by R72 (unused)`.
- **Filter:** the full `test-edit`, plus a grep that each removed name has no hit under `Assets/_Project` outside `.meta` files.
- **Commit:** `refactor(fishing): remove fight data retired by R72 (13 tuning fields, 4 profile fields, 7 constants)`.
- **Acceptance (reviewer, Sonnet):** `FishingData_AssetValuesEqualSeeds` and `FishingDefs_SerializedNames_MatchContract` pass; no YAML line refers to a removed field.

---

## Task F8 - Docs: measured numbers, contract commits, acceptance and handoff

- **Files:** spec 9.6 and 9.12 (measured medians next to the bands), contract C 16.1 row R72 (commit hashes F1-F7), `docs/superpowers/plans/2026-10-11-m2-acceptance.md` (steps 6 and 9: what to try in the R72 baseline), `docs/superpowers/handoff/2026-10-12-m2-rework-handoff.md` section 3.1 (status: built, awaiting the user's play).
- **Content:**
  - The `FG BOT` / `FG FUN` lines from F4, condensed to one table: profile x rod x bot, giving land / snap / timeout % and median seconds.
  - Any `TuningItems` entry with its numbers.
  - The open questions at the top of this plan, copied to the handoff for the user.
- **Manual check list for the user** (add to the acceptance doc, step 6):
  - **M1:** turn the line gauge on. The bar fills while reeling and drains only when everything is released. It shows a full mark, a red zone above it, and blinks red past the full mark.
  - **M2:** reel non-stop into a bluegill's run without steering: it should snap in about 5-8 s (decision 3, question 2).
  - **M3:** on a catfish, counter-steer a run without reeling. The fish visibly comes back toward the cast line, its runs get shorter, and the reel gets faster once it is tired.
  - **M4:** reel into a catfish run: it snaps in about 3.4 s.
  - **M5:** creak: silent at low tension, rising steeply near the top, fading out when you let go.
  - **M6:** the bobber leans toward the pull, less on a back pull.
  - **M9 (five styles, blind):** force each species in turn (F-keys) without looking at the reveal. Steady gives short plain runs that ease in. Darter darts and switches sides mid-run. Thrasher surges jerkily at varying speeds. Bulldog keeps diving and creeps line out at rest. Runner makes long sideways runs that build up. Name each one.
  - **M7:** an unattended fish escapes at 2 minutes.
  - **M8:** assist on: the same greedy reel snaps later.
- **Commit:** `docs(fight): R72 measured bot numbers, contract commits, acceptance steps and handoff status`.

---

## Risks

- **The bands come from a scratch model.** The F4 bands derive from a Python mirror with a different RNG. The bands are wide, and a miss is reported with numbers rather than retuned away from Rust.
- **Steer drop near the centre line.** A counter-steer that carries the fish across the line is dropped on the next tick, so steering alone alternates between loading (0.8) and draining (1.5/s). This is faithful to Rust's dot test, but it may feel jittery. Watch for it in M3.
- **The back-pull gate rarely binds.** It is `castMaxDist - 2` (16 m on Handmade, 12 m on Twig) against 10-14 m casts, where Rust's is 8 m of 10. Long back runs can carry a large fish toward `maxLineOut`. The line clamps there without strain (Rust would fail at twice its cast range).
- **The cone narrows (75 -> 60 deg).** Fish swing less widely on screen.
- **Hand-edited YAML.** A typo loads as 0. `FishingData_AssetValuesEqualSeeds` (F1) catches it.
- **The quirks are a FishGame layer, not Rust.** The strain rule is still Rust's (test 36), but fight lengths move by up to ±19% per profile from the quirk-free reference. Steady's in-play reckless snap moves from 5.3 s to about 7.4 s because its runs are shorter than 5.3 s.
- **Darter and Thrasher sit close on several stats** (back share 14% vs 18%, run 2.2 vs 3.2 s). Their distinctness rests on the switch rate, Thrasher's per-run speed and its 1.5 s surges. The F4 signature test and manual check M9 guard it.
- **Bulldog's dives and rest creep make it the longest fight** (about 33 s, +13% vs its tier), and a non-steerer almost always times out (92%). That matches Q4 ("non-steerers time out on large fish"), but Bulldog is the harshest of the five.
- **Runner's 5-9 s runs are long** for a Rhythm player: about 4.3 s on average once tired, sometimes near the cone edge.
- **New draws.** A flip check per eligible run tick (Darter, Thrasher) and a jitter draw per run start (Thrasher) change those profiles' seed streams. The quirk-free test profiles keep Rust's exact draw count.
- **The host Landed floor gets about 3x more permissive** because it assumes a fully tired fish. It is still a physical bound, not a replay.
- **PlayMode fight tests take longer.** The budget rises to 100 s; a typical bluegill lands in 15-30 s.
- **F2 is the largest task.** It changes 15 files. It is split from the data, bots and views so it can be reviewed against one code block and one test table.
