# Research: Rust (Facepunch) fishing minigame - mechanics and numbers

Date: 2026-10-12. Scope: game-design reference only. No Rust code or assets go into the FishGame repo; everything below is paraphrased.
Tags: [recovered] = read from Rust's shipped files (copies in scratchpad); [public knowledge] = from memory of the 2021 fishing update / wiki / devblog, not verified here; [inferred] = my reading of names plus recovered values, not confirmed by code.

## 1. Method and limits

- Copied to `scratchpad/re/rust/`: `global-metadata.dat` (IL2CPP metadata v39), `items.preload.bundle`, `Bundles` (manifest), the first 20 MB of `content.bundle` (header and object table only), and the two `AssetScene-prefabs` serialized files cut out of `assetscenes.bundle`. Nothing was run against the live install; Rust was never launched.
- Tools: small stdlib Python parsers written for this task (`scratchpad/re/scripts/`: metadata reader, UnityFS reader with LZ4 block decoder, SerializedFile v22 + type-tree reader). REA, Ghidra and ILSpy were not needed. No downloads.
- The bundles ship **with type trees**, so serialized field VALUES of the rod, bobber and every fish item were read exactly.
- **Code was NOT recovered.** All 208 `BaseFishingRod` methods (and the bobber/UI methods) have hash-obfuscated names (`%15e9bf...`), and string literals are not useful for xrefs. Mapping method tokens to RVAs means locating CodeRegistration in the 300 MB `GameAssembly.dll`, which did not fit the time-box. So every formula below is [inferred] or [public knowledge]; only names and constants are [recovered]. (Pass 1. Pass 2 did decompile the client code: see section 7. The fight simulation turned out to be server-only.)
- Optional deeper path (pass 1 note, now done in section 7): Il2CppDumper or Cpp2IL would give method RVAs plus field offsets. Then the methods that read the `GlobalStrainSpeedMultiplier` / `StrainModifier` field offsets could be decompiled one at a time with Ghidra, which would recover the actual strain formula. Even then, the method names stay obfuscated.

## 2. Recovered names (metadata) [recovered]

- `BaseFishingRod` serialized fields: FishingBobberRef, FishCatchDistance, ReelLineRenderer, LineRendererWorldStartPos, ConditionLossOnSuccess, ConditionLossOnFail, GlobalStrainSpeedMultiplier, MaxCastDistance, ForceFish, BobberPreview, onLineSoundDef, strainSoundDef, strainGainCurve, tensionBreakSoundDef, OverfishedAreaPrefab (plus ~17 obfuscated private fields).
- Nested enums of BaseFishingRod:
  - `CatchState`: None, Aiming, Waiting, Catching, Caught.
  - `FishState` (what the hooked fish is doing): **PullingLeft, PullingRight, PullingBack**.
  - `FailReason`: UserRequested, BadAngle, TensionBreak, Unequipped, TimeOut, Success, NoWaterFound, Obstructed, NoLure, TooShallow, TooClose, TooFarAway, PlayerMoved.
- `FishingBobber`: HorizontalMoveSpeed, PullAwayMoveSpeed, SidewaysInputForce, ReelInMoveSpeed (+ rigidbody / attach refs).
- `ItemModFishable` (on each catchable item): CanBeFished, IsJunk, StrainModifier, MoveMultiplier, ReelInSpeedMultiplier, CatchWaitTimeMultiplier, MinimumBaitLevel, MaximumBaitLevel, MinimumWaterDepth, MaximumWaterDepth, RequiredTag, Chance, SteamStatName, CanBeMounted, FishMountIndex.
- `FishingTag` (water types): MoonPool, River, Ocean, Swamp, DeepSea.
- UI and feel: `UIFishing` (TensionLine, FillImage, FillGradient); `FishingRodViewmodel` (YawLerpSpeed, PitchLerpSpeed, StrainParticles, ShakeMaxScale, CatchRoot, FishViewmodels).
- No `fishing.*` convars were found among the identifier names or string literals.

## 3. Recovered values [recovered]

### Rod: `fishing_rod.entity.prefab` (BaseFishingRod; Rust has one rod, `fishingrod.handmade`)

| Field | Value |
|---|---|
| MaxCastDistance | 10 m |
| FishCatchDistance | 1.0 m |
| GlobalStrainSpeedMultiplier | 0.75 |
| ConditionLossOnSuccess / OnFail | 1 / 2 (item condition max 100, so roughly 100 catches or 50 snaps) |
| strainGainCurve | key (0.10, 0.0, flat) -> key (0.95, 1.0, slope 2.66); clamped both ends (pre/post infinity = clamp) |
| Viewmodel | YawLerpSpeed 2, PitchLerpSpeed 2, ShakeMaxScale 0.007 |
| Crafting | 10 s, 100 scrap to research |

### Bobber: `bobber.entity.prefab` (FishingBobber)

| Field | Value |
|---|---|
| HorizontalMoveSpeed | 1.2 |
| PullAwayMoveSpeed | 1.3 |
| SidewaysInputForce | 0.8 |
| ReelInMoveSpeed | 0.6 |

### Fish items (ItemModFishable)

There are three tiers. Each tier's StrainModifier, MoveMultiplier and ReelInSpeedMultiplier move together.

| Item | Strain | Move | ReelIn | CatchWait | MinBait | MaxBait | MinDepth | Chance |
|---|---|---|---|---|---|---|---|---|
| anchovy, herring, sardine (small) | 0.8 | 0.8 | 1.2 | 0.9 | 0 | 5 | - | 1.0 |
| yellowperch, smalltrout (medium) | 1.0 | 1.0 | 1.0 | 1.0 | 3 | - | - | 1.0 |
| catfish, orangeroughy, salmon, smallshark (large) | 1.25 | 1.25 | 0.9 | 0.8 | 5 | - | salmon 4.5, shark 5.5 | 1.0 |
| junk (bucket, fins, skull, tarp, bottles) | 0 | 0 | 4.0 | 0.3 | 0 | 2.5 | - | 0.2 |

Derived speeds (base x multiplier) [inferred units: m/s]:
- Pull-away: small 1.04, medium 1.3, large 1.63.
- Sideways: small 0.96, medium 1.2, large 1.5.
- Reel-in: small 0.72, medium 0.6, large 0.54, junk 2.4.

Junk has zero strain and zero movement: you just wind it in fast.

## 4. How the fight works

**Flow** [public knowledge + recovered states]:
- Aim (with a bobber preview), cast, then wait (Waiting) until the bobber dips on a bite. A bite is hooked automatically, after which you are Catching.
- While Catching, the fish is always in one of three states: pulling left, pulling right, or pulling back (away).
- You hold primary fire to reel. You hold the strafe key opposite to the fish's side run (fish goes left, hold D).
- A tension/strain bar (UIFishing fill with a colour gradient) rises and falls. The rod shakes and the line strains near the top.
- The fish is caught when the bobber reaches the player (FishCatchDistance 1 m).

**Tension build and drain** [public knowledge for the rules; inferred for the shape]:
- Strain rises while you reel. It rises much faster when you reel while the fish runs and you are not countering it, and fastest when the fish pulls straight back, which cannot be countered sideways.
- It falls when you stop reeling. "Pumping" is the core rhythm: reel, release, reel.
- At full strain the line snaps (FailReason.TensionBreak) and the rod loses 2 condition. A catch costs 1.
- Inferred rate: `strainRate = base * GlobalStrainSpeedMultiplier(0.75) * fish.StrainModifier(0.8 / 1.0 / 1.25) * situation`, where situation is roughly 0 when idle, small when countered, and large when uncountered or the fish is pulling back. So a large fish loads the line 1.56x faster than a small one (1.25 / 0.8).
- The base rate and the situation factors are in code and were NOT recovered.

**strainGainCurve** [inferred]: the field sits between `strainSoundDef` and `tensionBreakSoundDef`. It is most likely the strain-to-volume curve for the creak sound: silent below 10% strain, rising steeply to full volume by 95%. A less likely reading is a gain shaper on the strain rate itself. Either way it describes the feel: nothing audible early, then a sharp warning ramp in the last third.

**Fish movement** [inferred from the bobber fields]:
- The fish is a physical bobber. Pulling left or right moves it sideways at about 1.2 x Move. Pulling back moves it away at about 1.3 x Move.
- Reeling moves it toward you at 0.6 x ReelIn. Your A/D input pushes it sideways with SidewaysInputForce 0.8.
- [inferred] So on a pull-back you LOSE line even while reeling: 0.54 in against 1.63 out for a large fish.
- On a side run, reeling still gains line, and your counter-force (0.8) slows the fish's drift (0.96 to 1.5) but cannot fully cancel it. Big fish drag you sideways regardless.
- How often the fish changes state, and whether it tires (an explicit stamina), is not recovered.
  - [public knowledge, weak] Fish change direction every few seconds and give up after a while.
  - There is a `TimeOut` fail reason, so a fight or wait time limit exists; its value is not recovered.

**Countering** [public knowledge]:
- Hold the strafe key opposite the fish's run direction while reeling.
- Correct counter keeps strain low and lets you gain line.
- Wrong or no counter spikes strain.
- Against PullingBack, the right play is to stop reeling and let the strain drain.

**Other fail rules** [recovered names; thresholds not recovered]:
- BadAngle: you look or aim too far from the line.
- PlayerMoved: you walk away mid-fight.
- TooClose / TooFarAway / TooShallow / NoWaterFound / Obstructed: cast validation.
- NoLure: no bait loaded.
- Unequipped: you holster the rod.

**Snap** [public knowledge + recovered name]: the line breaks the moment strain hits max. No grace period is known. The fish and the bait are lost, and the rod loses 2 condition.

**Catch** [recovered value 1.0 m; meaning inferred]: the fish is landed when the bobber is within 1.0 m (FishCatchDistance). Junk reels in at 4x speed with no strain.

## 5. Mapping onto FishGame (spec 9.5-9.7, FightSim.cs)

| Rust | FishGame now |
|---|---|
| FishState PullingLeft/Right | Burst side run, `burstDir = +-1` |
| FishState PullingBack | dive, `burstDir = 0` |
| (no explicit calm state found) | Calm phase |
| StrainModifier 0.8 / 1.0 / 1.25 | no per-fish strain multiplier; heavier fish load the line only via `pb` in burst |
| GlobalStrainSpeedMultiplier | `StrainScale` |
| ReelInSpeedMultiplier | `rod.ReelSpeed`; no per-fish multiplier |
| SidewaysInputForce < fish sideways speed | `rod.SteerRate` vs `pb * LateralSpeed` |
| FishCatchDistance 1 m | `LandDistance` 2 m |
| strainGainCurve (creak volume) | TensionCues / RodCueAudio |

Key difference: in FishGame, tension gains only in Burst while reeling, and Calm reeling DRAINS it (`RelaxWhileReeling` 0.20/s). In Rust, every reel adds strain and only releasing drains it.

## 6. Proposal: FightSim changes for a Rust feel

User choices to hit:
1. Tension builds whenever reeling: heavier fish and uncountered runs load it much faster. It drains when not reeling.
2. Correct counter-steer visibly drags the fish back and tires it fast.
3. Greedy reeling snaps big fish.

All numbers are [tune] seeds for the M2 bots.

### New data

- **`FishFightProfile.StrainMod`**: Steady 0.8, Darter 0.9, Bulldog 1.1, Thrasher 1.0, Runner 1.25. This mirrors Rust's three tiers.
- **`FishingTuning`** additions:
  - `ReelStrainBase` 0.07 /s
  - `UncounteredMult` 1.6
  - `DiveStrainMult` 1.6
  - `CounterSteerMult` 2.0
  - `CounterTireMult` 3.0
  - `CounterReelRelief` 0.7
- **Change** `CounterRelief` from 0.5 to 0.8, and `RelaxWhileReeling` from 0.20 to 0. Remove `RelaxWhileReeling`, or keep it at 0 so old data still loads.

### Step 3 (lateral): counter-steer drags the fish back

```
steerGain = against > 0 ? rod.SteerRate * CounterSteerMult : rod.SteerRate
lateral  += steer * steerGain * dt
```

Handmade gives 1.2/s, which beats a fresh trophy Runner run (`pb * LateralSpeed`, about 0.85/s), so the fish visibly comes back toward centre. Twig gives 1.0/s against 0.85/s: it barely holds.

### Step 4 (stamina): countering tires fast

```
side: FishStamina -= StaminaDrain * against * CounterTireMult * dt
```

Runner at 0.035 x 3 = 0.105/s needs about 9 s of full counter to exhaust. Keep the dive rule (resting tires it) and the calm regen.

### Step 5 (line): a countered run gives line back

```
reelRate = ReelSpeed * max(floor, 1 - ReelBurstPenalty * pb * (1 - CounterReelRelief * against))
```

This makes "drags the fish back" true in distance too. Like Rust, a dive while reeling still loses line, because `RunSpeed * DiveRunMult` beats the reel.

### Step 6 (tension): Rust rule, any reel builds strain and only release drains it

```
w = WeightFactor * fish.StrainMod                                   // heavier = faster load
// NOTE: pb already contains WeightFactor, so burst terms scale ~ PullStrength * WF^2 * StrainMod (deliberate:
// trophies punish greed hard). Simpler alternative: use StrainMod alone on the burst terms and keep w for the base;
// then trophy Runner greedy = ~0.75/s, snaps ~1.3 s, still meets choice 3.
if reel:
    strain += ReelStrainBase * w                                    // calm reeling builds slowly
    if side:  strain += ReelStrainBurst * pb * w * UncounteredMult * (1 - CounterRelief * against)
    if dive:  strain += ReelStrainBurst * pb * w * DiveStrainMult   // no counter possible: let it run
strain += SteerStrain*|steer| + OutOfWindowStrain(if out) + SpoolStrain*pb(if spooled)   // unchanged
relax = reel ? 0 : RelaxRate * rod.RelaxMult                         // 0.45/s, maybe 0.55 for snappier pumping
tension = clamp01(tension + (strain * scale / rod.LineStrength - relax) * dt)
```

Keep `SnapGrace`, assist and pity unchanged (pity still scales strain).

### Hand-worked checks (Handmade, `LineStrength` 1)

| Case | Rate | Time to full |
|---|---|---|
| Bluegill, Calm reel | 0.07 x 0.85 x 0.8 = 0.048/s | about 21 s (never threatening) |
| Bluegill, uncountered run | about 0.13/s | about 7.5 s (forgiving) |
| Trophy Runner (w = 1.25 x 1.25 = 1.56, pb about 1.06), Calm reel | 0.11/s | about 9 s, so you must pump even in Calm |
| Trophy Runner, greedy uncountered run | (0.07 + 0.30 x 1.06 x 1.6) x 1.56 = 0.90/s | about 1.1 s + 0.25 s grace: **snaps** (choice 3) |
| Trophy Runner, same run fully countered | (0.07 + 0.30 x 1.06 x 1.6 x 0.2) x 1.56 = 0.27/s | about 3.7 s from 0, so a typical 2.5-4 s run can be reeled through if you start low |
| Trophy Runner, dive while reeling | about 0.90/s, like greedy | snaps; must release |
| Release from 0.9 | 0.45/s drain | under 0.3 in about 1.3 s, which matches the existing "rest from 0.9" check |

### Knock-on effects

- `TensionCues` / creak audio: map volume with a Rust-like curve, silent below about 0.1 tension and ramping steeply to full by about 0.95.
- The spec 9.5 "two lessons" text, the hand-worked checks, the clean-catch threshold (0.8, R64), and the bot fight-length targets all need a re-run.
- Calm-phase reeling now costs tension, so fights will lengthen. Expect the bots to lower `ReelStrainBase` or raise `RelaxRate`.
- `CounterSteerMult` 2.0 roughly doubles steer authority while countering, so players can overshoot past centre into the far side of `angleWindow` (out-of-window strain) or the pin edge (5 s escape). This could be good skill pressure; the bots should watch for it.
- No new RNG draws are added, so the T7 draw-order table is unaffected.
- Not proposed: Rust's 1 m catch distance, 10 m cast, and rod condition loss. FishGame's own values and rules stay.

## 7. Recovered formulas (pass 2, 2026-10-12: client code decompiled)

### 7.1 Method

- Inputs: copies only, in `scratchpad/re/rust/`. `GameAssembly.dll` (300 MB) was copied there from the install for this pass. Rust was never launched. The Unity version, read from `globalgamemanagers`, is `6000.3.15x1`.
- Cpp2IL 2022.1.0-pre.21 failed on this build in both modes:
  - `diffable-cs` crashed with a NullReferenceException in ParameterAnalysisContext.
  - `dll_empty` crashed with an IndexOutOfRange on property types in Fleck.dll / PickAFriend.
  - It did locate CodeRegistration at 0x18ED0BA70 and MetadataRegistration at 0x18FB5D3B0.
- Replacement: own stdlib Python mapper (`re/scripts/il2map.py`, `pe.py`).
  - Per-module method-pointer tables give an RVA for every method (687k rows in `cpp2il-out/methods_all.tsv`).
  - The field-offset table gives the field offsets (`cpp2il-out/fishing_map.txt`).
  - Mapping checked: the BaseFishingRod constructor writes the expected defaults at the expected offsets.
- Ghidra 12.1.4 headless with `-noanalysis`. No auto-analysis was run. Only the 215 fishing methods were disassembled and decompiled, by a small script (`re/ghidra/scripts/DecompileList.java`). Output is in `cpp2il-out/decomp_fishing*.c`.
- Obfuscation:
  - Method names are hashed.
  - Most real methods have several decoy clones with junk constants (for example 0.4467, 2.449, 51.84). Facts below come only from the clone whose constants are sane and are shared across all of its clones. The UI-bar and bobber-tilt helpers were picked this way, and their sibling clones agree on the structure. These values are cosmetic.

### 7.2 Authority: the fight is simulated on the SERVER [recovered]

- The client has one handler (plus its decoy clones) for a server->client RPC. It reads (int fishState, float strain) from the message, stores them in the rod, and sets CatchState = Catching. The client never computes strain.
- None of the decompiled client classes (BaseFishingRod, FishingBobber, UIFishing, FishingRodViewmodel, ItemModFishable; nested and compiler-generated types were not decompiled) reads the bobber speed fields (HorizontalMoveSpeed, PullAwayMoveSpeed, SidewaysInputForce, ReelInMoveSpeed) or ItemModFishable.StrainModifier at runtime. Only constructors touch them; the bobber's code defaults are all 1.0 and the prefab overrides them.
- No client code rolls the fish direction, moves the bobber, integrates strain, or checks TimeOut.
- Therefore these are NOT in the client binary:
  - the base strain rate and the per-tick strain update;
  - the drain rule;
  - how strafe input is compared to fish direction;
  - direction-switch timing;
  - the TimeOut value;
  - the catch test itself.
  They live in the dedicated server's Assembly-CSharp.
- [inferred] The server reads the player's normal input state (reel = FIRE_PRIMARY, strafe = LEFT/RIGHT) and pushes (fishState, strain) back to the client.

### 7.3 What the client does recover [recovered unless tagged]

- **CatchState values**: None 0, Aiming 1, Waiting 2, Catching 3, Caught 4. Strain is forced to 0 while Waiting.
- **FishState is a bit-flags enum**: PullingLeft = 1, PullingRight = 2, PullingBack = 4.
  - The client tests each bit independently, so a fish can pull back AND to one side at the same time. This corrects the pass-1 reading of three exclusive states.
  - [inferred] The server may also send 0, meaning no pull.
- **BUTTON values** (metadata defaults): LEFT 8, RIGHT 16, FIRE_PRIMARY 1024, FIRE_SECONDARY 2048.
  - The client uses FIRE_PRIMARY to cast while Aiming.
  - FIRE_SECONDARY plus a cooldown timestamp sends a cancel RPC.
- **Strain domain**: 0..1. This is [inferred] from the creak threshold, the curve domain (0.10..0.95) and the gradient-filled UI bar.
- **Snap**: [inferred] when strain reaches 1, the server ends the fight with TensionBreak. No grace value exists client-side.
- **strainGainCurve is audio, which settles the pass-1 question.**
  - The creak (strainSoundDef) starts when strain > 0.10 and stops (0.25 s fade) when strain <= 0.10.
  - While playing, its gain modulation = strainGainCurve(strain). The keys (0.10 -> 0) and (0.95 -> 1, end slope 2.66) give a silent start and a steep ramp in the last third.
  - The curve does not shape the strain rate.
- **UI tension bar**:
  - `shown += (strain - shown) * clamp01(dt * 10)` per frame, which is about 0.1 s of smoothing.
  - Slider value = shown.
  - Fill colour = FillGradient(shown).
- **Bobber visual tilt** (client only, cosmetic):
  - Target t = 0 if PullingLeft, else 1 if PullingRight, else 0.5.
  - If PullingBack is set, t moves halfway back toward 0.5.
  - `t += (target - t) * clamp01(dt)` per frame.
  - Roll angle = 30 - 60*t degrees, so +-30 degrees.
- **Code defaults vs prefab** (the prefab wins in game):

  | Field | Code default | Prefab |
  |---|---|---|
  | FishCatchDistance | 0.5 | 1.0 |
  | GlobalStrainSpeedMultiplier | 1.0 | 0.75 |
  | MaxCastDistance | 10 | 10 |
  | ConditionLossOnSuccess / OnFail | 0.02 / 0.04 | see section 3 |
  | Bobber speeds | 1.0 each | 1.2 / 1.3 / 0.8 / 0.6 |

### 7.4 Status of each requested item

| Item | Status |
|---|---|
| Per-tick strain update (reel, strafe vs fish, tier StrainModifier, GlobalStrainSpeedMultiplier, pull-back) | SERVER-ONLY. The inputs are known (sections 3 and 7.3); the combining formula is not in the client. |
| Drain rule | SERVER-ONLY. |
| Snap threshold | strain 1.0 [inferred]; the domain is 0..1 [recovered from client use]. |
| Direction switching and timing | SERVER-ONLY. The state is flags, so back can combine with a side pull [recovered]. |
| Fish speeds vs reel | Prefab speeds x item multipliers (section 3) [recovered]; how they combine is SERVER-ONLY. |
| Countering effect on position and strain | SERVER-ONLY. |
| Catch condition | Bobber within FishCatchDistance 1.0 m [field recovered; the test is server-side, inferred]. |
| TimeOut value | SERVER-ONLY. |
| Curves | strainGainCurve = creak volume (above); no other curve on the rod. |

### 7.5 Follow-up path to get the exact maths (needs the user's OK: it is a download)

- Rust Dedicated Server (Steam app 258550, free, anonymous SteamCMD login, several GB) ships `RustDedicated_Data/Managed/Assembly-CSharp.dll`.
  - [public knowledge] The server build is Mono (managed IL, not IL2CPP); the Oxide and Carbon modding frameworks patch this exact DLL.
  - The existing `scratchpad/tools/ilspycmd` can decompile BaseFishingRod's server methods directly.
  - [public knowledge, unverified here] The server DLL is believed not to be name-obfuscated.
- That download would resolve every SERVER-ONLY row above. Nothing else on this machine contains that code.

### 7.6 Mapping onto FightSim.cs (Rust baseline, what can be fixed now)

Can be set now from recovered data:
1. **Land**: `LandDistance` 2 -> 1.0 (FishCatchDistance). [inferred: equivalent] Rust measures bobber-to-player distance; FightSim compares line length.
2. **Snap**: tension is already 0..1 with a snap at 1, which matches Rust's domain.
3. **Per-fish strain**: add `FishFightProfile.StrainMod` (Rust tiers 0.8 / 1.0 / 1.25) as a multiplier on every strain term.
4. **Global strain**: fold GlobalStrainSpeedMultiplier 0.75 into `StrainScale`.
5. **Fish direction**: model it as flags {left, right, back}, not as `BurstDir` in {-1, 0, +1}.
   - A dive can carry a side component. This changes `FightState` (add a `Diving` bool alongside `BurstDir`).
   - [inferred] Lateral drift applies whenever BurstDir != 0, including during a dive. How the server moves the bobber is unknown.
6. **Speeds** (FightSim works in normalized lateral; W = lateral half-width in metres). [inferred units] The bobber has a rigidbody and one field is named SidewaysInputForce, so the values may be forces, not m/s:

   | FightSim field | Rust baseline |
   |---|---|
   | `LateralSpeed` | 1.2 * MoveMult / W |
   | `SteerRate` | 0.8 / W |
   | `RunSpeed` | 1.3 * MoveMult |
   | `ReelSpeed` | 0.6 * ReelInMult |

   - Add a per-fish `ReelInMult` (Rust 1.2 / 1.0 / 0.9).
7. **Feedback** (TensionCues / RodCueAudio / HUD):
   - Creak silent at <= 0.10, gain from the curve (0.10 -> 0, 0.95 -> 1, end slope 2.66).
   - Bar smoothing lerp factor dt*10.
   - Bobber roll +-30 deg toward the pull side, halved on a back-pull.

Must wait for the server DLL:
- `SnapGrace` (a missing client grace says nothing about the server).
- Whether reel speed depends on pull (`ReelBurstPenalty`, `DiveRunMult`); until then these are [placeholder, not Rust].
- The rest: the strain gain and drain formula (ReelStrainBurst / RelaxRate / CounterRelief / RelaxWhileReeling shape), the direction-switch timing (FlipRate / FlipMinGap / BurstMin..Max / CalmMin..Max and whether Calm exists at all), and TimeOut. FightSim has no total fight limit today; add `MaxFightSeconds` -> Escaped when the value is known.

Rules that stay FishGame's:
- Authority stays owner-simulated per spec 9.9. Do not copy Rust's server authority.
- If switching moves to Rust-style random intervals, map each interval to a `Uniform` draw. Note that this changes the T7 draw-order table.

## 8. Exact server maths (pass 3, 2026-10-12: Rust Dedicated Server decompiled)

### 8.1 Method

- The user approved the download. SteamCMD (Valve-signed) installed Steam app 258550 with an anonymous login: 5.7 GB, into the session scratchpad.
- Windows Defender found no threats in `RustDedicated_Data/Managed`.
- `Assembly-CSharp.dll`: 10,508,112 bytes, SHA-256 `13687C7D90785D1DE7C01258432D62068B6FF048D22F2F7AD3804C7AF8709F8F`. It is managed (Mono) and not obfuscated.
- A copy was decompiled with ilspycmd 11.1.0.9782: `BaseFishingRod`, `FishingBobber`, `ItemModFishable` and `ConVar.Fishing`. `RustDedicated.exe` was never run.
- Only the mechanics and numbers are recorded here. No Rust code is in the repo.
- Everything in this section is [recovered] unless tagged otherwise. It supersedes the SERVER-ONLY rows in 7.4 and the [inferred] formulas in sections 6 and 7.6.
- Prefab values override the code defaults:
  - FishCatchDistance: code 0.5, prefab 1.0.
  - GlobalStrainSpeedMultiplier: code 1, prefab 0.75.
  - Bobber speeds: code 1, prefab per section 3.

### 8.2 Tick (server, every frame while Catching)

Inputs come from the server's input state:
- `reel` = Backward (S) or Fire (LMB) held.
- `steerL` = Left held, `steerR` = Right held. Holding both cancels both.
- A steer input is dropped unless it pulls the line away from the bobber side (dot test on the rod-to-bobber direction).

Strain rate `k`, before any multipliers:

| Situation | k |
|---|---|
| No input at all | none: strain drains instead (below) |
| Any input while the fish is resting (state 0) | 0.5 |
| Reeling while the fish pulls back (with or without a side pull) | 1.5 + 1 = **2.5** |
| Reeling while the fish pulls sideways | 1.2 + 1 = **2.2** |
| Counter-steering only (Right vs a Left pull, or Left vs a Right pull) | **0.8** |
| Steering the same way as the fish, or steering during a back-only pull | 0.5 |

- Reeling takes precedence: reel + counter-steer during a run is still 2.2 (or 2.5 on a back-pull).
- Then `k *= StrainModifier(fish) * GlobalStrainSpeedMultiplier(0.75)`, and `k -= (FishingBoost - 1)` (a tea buff).
- With any input, `strain += dt * k`. With no input, `strain = MoveTowards(strain, 0, 1.5 * dt)`, a flat drain of **1.5 per second**.
- **Snap**: `strain > 7` -> TensionBreak.
- **Display**: the bar is `strain / 6`, so the bar is full at 6 and the line snaps at 7 (bar 1.17). The "straining" flag (for audio and anims) is set when there is input and the display value is above 0.25.
- So **the fish never builds strain on its own.** Only player input loads the line, and fully letting go always drains it. This is the Rust "pump".

### 8.3 Fish state machine

- The state is a flag set {PullingLeft 1, PullingRight 2, PullingBack 4}. 0 = resting.
- When the timer `nextFishStateChange` expires (it starts at 0, so the first run starts at once):
  - `tire01 = clamp01(TireAmount / 20)`.
  - **Run -> rest**: the state becomes 0 for `Uniform(2, 4) * (1 + tire01)` s. Rests get up to twice as long as the fish tires.
  - **Rest -> run**: the run lasts `Uniform(3, 7) * (1 - tire01)` s. Runs shrink to nothing when the fish is fully tired.
    - Left or Right, 50/50 (`Range(0,100) < 50`).
    - Plus PullingBack when `Range(0,100) > 60` (39%) and the bobber is closer than `MaxCastDistance - 2` (8 m).
- If the next bobber position is invalid, the side flags flip (Left <-> Right). Invalid means more than 60 deg off the initial cast direction (horizontal, from the player), or blocked by geometry.
- **TimeOut**: fight time (since the hook) > **120 s** -> TimeOut. The bobber entity also self-destructs 150 s after the cast.

### 8.4 Tire (FishingBobber.TireAmount)

- Any steer input: `+0.8/s`. A counter-steer against an exactly single-direction side pull (no back flag): `+1.25/s`.
- No steer input: `-0.1/s`. There is no explicit floor in code [recovered]; it can go negative, which lengthens runs slightly.
- **Reeling does not tire the fish.** Only steering does.
- Effects: it scales run/rest durations (above, saturating at 20) and multiplies reel speed by `remap(TireAmount, 0..5 -> 1..3)` (clamped). So after about 5 s of steering, reeling is 3x faster.

### 8.5 Bobber movement (per tick, metres)

- `p = clamp(PingPong(time, 2), 0.2, 2)`. This is a global 4 s triangle wave, so fish pull strength surges and fades.
- Side pull: `HorizontalMoveSpeed(1.2) * p * MoveMultiplier`, halved while the player counter-steers.
  - The code assigns Left and then Right, so if both flags were set, Right wins. They never are.
- Back pull: `PullAwayMoveSpeed(1.3) * p * MoveMultiplier` away from the player, halved while reeling.
- Player steer: when the fish is not pulling in that same direction, add `SidewaysInputForce(0.8) * FishingBoost` sideways.
  - A counter-steer therefore both halves the fish's side speed AND pushes the bobber back at 0.8 m/s.
- Reel: `ReelInMoveSpeed(0.6) * ReelInSpeedMultiplier * tireRemap(1..3) * FishingBoost` toward the player.
- Units are metres per second applied to the transform. They are not forces; the rigidbody is only for visuals.

### 8.6 Other server rules

- **Catch**: horizontal player-to-bobber distance <= FishCatchDistance (1.0 m) -> Caught.
- **BadAngle**: the bobber is more than 60 deg off the head-forward direction (horizontal) while more than 1.2 m away.
- **Obstructed**: line-of-sight checks every 0.4 s.
- **TooFarAway**: distance > 2 x MaxCastDistance (20 m).
- **PlayerMoved**: the player is more than 1 m from where they cast.
- **Cast validation**: TooClose under 5 m; TooShallow if the depth is under 0.3 m at the bobber or under 0.1 m at 95% of the way back.
- **Bite wait**: `Uniform(10, 20) * CatchWaitTimeMultiplier - remap(baitValue 0..20 -> 1..10)`, clamped to 3..20 s. The bite auto-hooks.
- **Rod condition loss**: 2% on success, 4% on failure (the code values; the prefab values in section 3 are 1 and 2).

### 8.7 Worked numbers (prefab values, strain from 0)

The effective multiplier is `StrainModifier x 0.75`: small 0.6, medium 0.75, large 0.9375.

| Action | small | medium | large |
|---|---|---|---|
| Reel into a side run, seconds to snap (7 / rate) | 7 / 1.32 = **5.3 s** | 4.2 s | 3.4 s |
| Reel into a back-pull | 4.7 s | 3.7 s | 3.0 s |
| Reel while the fish rests | 23 s | 18.7 s | 14.9 s |
| Counter-steer only | 14.6 s | 11.7 s | 9.3 s |

- Release drain: a full bar (6) to 0 takes 4.0 s. 90% -> 30% of the bar (5.4 -> 1.8) takes 2.4 s.
- Runs last 3-7 s, so reckless reeling into one run snaps a small fish only if the run lasts more than 5.3 s and nothing was drained before. With carry-over strain from earlier pumps, it is sooner.
- [inferred] The user-confirmed target of "about 7 s of careless reeling snaps a small fish" sits close to Rust's 5.3 s. FishGame can hit 7 s with the small-fish StrainModifier at about 0.6 (vs Rust's 0.8), or keep Rust's 5.3 s for the baseline.

### 8.8 What this changes vs the section 6 proposal

- There is no "reel strain base x weight" term. Strain depends on input type, fish state and the per-fish StrainModifier only. Weight does not enter Rust's maths at all.
- Calm reeling still costs strain (0.5 x mods), and only a full release drains. This confirms section 6's `RelaxWhileReeling` 0.
- There is no CounterRelief term. Countering helps because counter-steer alone (0.8) is cheaper than reeling into a run (2.2), it halves the run, it pushes the bobber back, and it tires the fish (+1.25/s), which shortens runs and triples reel speed.
- A dive (PullingBack) combines with a side pull and only happens inside 8 m. It is the most expensive thing to reel into (2.5).
- TimeOut is 120 s. There is no near-snap grace: the snap is instant at strain > 7, but the bar shows full from 6, a ~1/6 headroom band.
