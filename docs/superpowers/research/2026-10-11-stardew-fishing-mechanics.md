# Stardew Valley 1.6.15 fishing: transferable mechanics (RE notes, numbers only)

Sources: ILSpy of `Stardew Valley.dll` / `StardewValley.GameData.dll`; `Data/Fish.xnb` and `Data/Locations.xnb` decoded by me (own LZX port + schema parser in `scratchpad/re/stardew/tools/`, output `re/stardew/fish.txt`, `locations.json`). No wiki values used. All BobberBar constants are per frame at 60 Hz (x60 for /s). Timers in FishingRod/BobberBar use real ms.

## 1. Bite timing (`FishingRod.calculateTimeUntilFishingBite`, `tickUpdate`, `DoFunction`)

- Constants: `minFishingBiteTime=600`, `maxFishingBiteTime=30000`, `maxTimeToNibble=800` (ms).
- `wait = Random(600, max(600, 30000 - 250*FishingLevel - tackleCut))`, uniform. `tackleCut`: Spinner (686) 5000 each, Dressed Spinner (687) 10000 each (they lower the upper bound, not the mean multiplier).
- `if firstCast: wait *= 0.75`. `if any bait: wait *= 0.5` (Wild Bait 774 and Challenge Bait: another x0.75; Deluxe Bait: x0.66). Final `max(500, wait)`.
- Level 0, no bait: mean ~15.3 s (first cast 11.5 s). Level 10 + plain bait + first cast: mean ~5.2 s. Wild bait + first cast at level 0 ~4.3 s.
- Hotspot ("fish splash point", `GameLocation.performTenMinuteUpdate`): bobber overlapping it => first `wait /= 4` (re-rolls after a missed bite are recomputed without it), `waterDepth + 1`, bait potency +0.4. Frenzy (named fish) `/= 2` and forces that fish within 2 tiles. Spawn: every 10 in-game min, if none active, 50% per roll, random open-water tile 2-4 tiles from shore; despawn chance `0.1 + minutesActive/1800` per tick once active >60 min (>120 for frenzy). One per map. Frenzy: 1% per spawn in 4 maps.
- Missed-bite loop: single event, no multi-nibble. At `wait` expiry `isNibbling=true`: chime, controller rumble (0.75 for 250 ms), "!" popup, bobber bob speed up. Player has **800 ms** (`timeUntilFishingNibbleDone`); click inside it => hooked. Miss it => nibble ends silently, new wait rolled (no 0.75 first-cast factor), line stays out, bait not consumed. Clicking while NOT nibbling reels in empty (no penalty).
- Hook beat: on click `hit=true`, a 1500 ms "!" sprite plays, then `startMinigameEndFunction` opens BobberBar. Fish is rolled at the click (`getFish`), not at the bite. Junk/trash/non-fish items skip the minigame and are landed instantly.
- Auto-hook enchant: skips the 800 ms window. Bait is consumed when the minigame ends, win or lose (`doneFishing(consume:true)`), not on missed bites or early reel-ins. Tackle: +1 use per non-junk catch, breaks at 20.
- Cast: `distance = castPower(0..1, ping-pong) * (4 + addedDist) tiles`, min 2 tiles; `addedDist` = 0/1/2/3/4 at fishing level 0/1/4/8/15. Rod skill extends range.

## 2. Fish behaviour types (`BobberBar.update`, fish `difficulty` d, `Data/Fish` field 2)

Track is 548 px (pos 0 = top); fish clamped to [0, 532]. Per frame, all types:
```
P_new   = d * (smooth ? 20 : 1) / 4000            // smooth only rolls when target == -1
if roll < P_new: s = min(99, d + U[10,44]) / 100
                 target = pos + U[-pos, 548-pos] * s          // offset scaled by s (0.2-0.99): low-d fish move only part of the way, high-d fish roam the whole track
if |pos-target| > 3 and target != -1:
    a = (target-pos) / (U[10,29] + 100 - min(100,d))           // response lag in frames
    speed += (a - speed) / 5                                   // 5-frame low-pass
elif type != smooth and roll < d/2000: target = pos + (U[-100,-51] or U[50,100])   // idle twitch
else: target = -1                                              // idle; speed is NOT damped, keeps coasting
dart only (extra, overrides target): if roll < d/1000: target = pos + (-U[51,100+2d] or +U[50,100+2d])
floater: biasAcc = max(biasAcc-0.01, -1.5)   (ramps 2.5 s to -90 px/s, never resets)  | sinker: min(+0.01, 1.5) (+90 px/s)
pos += speed + biasAcc
```
Difficulty scales three things at once: event rate (about `0.015*d` retargets/s), response lag (`U[10,29]+100-d` frames: d=15 about 1.6-1.9 s to arrive, d=95 about 0.25-0.55 s), and jump amplitude (`s` = d+10..45 %). Data range d=5 (algae) to 110 (Legend); catalogue: mixed 25 fish (avg d 67), dart 15 (56), smooth 10 (59), sinker 9 (66), floater 5 (34).

Sim of those rules (my port, 40 seeds x 30 s; px/s, rev/s = direction reversals per s, still = share of time <15 px/s):

| d | type | mean spd | p95 spd | rev/s | still |
|---|---|---|---|---|---|
| 30 | mixed / dart / smooth | 28 / 49 / 20 | 77 / 88 / 68 | 0.22 / 0.92 / 0.10 | .41 / .08 / .59 |
| 50 | mixed / dart / smooth | 49 / 75 / 34 | 142 / 141 / 123 | 0.37 / 1.50 / 0.15 | .27 / .05 / .47 |
| 75 | mixed / dart / smooth | 98 / 130 / 67 | 287 / 259 / 253 | 0.62 / 2.10 / 0.25 | .15 / .03 / .32 |
| 95 | mixed / dart / smooth | 174 / 236 / 123 | 554 / 512 / 484 | 0.91 / 2.61 / 0.43 | .12 / .03 / .28 |

Feel per type: **mixed** = baseline stop/go with idle twitches. **dart** = same plus constant 50-290 px snap targets (about 2.5-3x the reversals of mixed, almost never still): you cannot lead it, you chase. **smooth** = 20x retarget rate but only after arriving, no twitches: long fluid glides, few reversals, predictable and rest-heavy even at high d (lag is a low-pass, so it looks lazy). **sinker/floater** = mixed plus a constant +/-90 px/s pull to bottom/top (floater spends 90% of time at top at d=15, 20% at d=95 as chase overpowers bias): the force is steady pressure, not events.
Quirk: target clamps to 548 but pos clamps at 532, so a smooth fish that picks a target >535 can never "arrive" and parks at the bottom forever (sim: 22% of d=85 smooth fish within 60 s). Bug-as-feature, don't copy.

Closest-profile mapping and parameter ideas (our fields from spec 9.6: calm/burst s, flipRate, diveChance, lateralSpeed, runSpeed, staminaDrain):
- **Steady <- smooth (and mixed, low d).** Ideas: (a) add `burstRamp` 0.5-0.8 s ease-in on lateral/run speed (Stardew's 5-frame low-pass + 10-30 frame lag) so runs are readable; (b) never flip, never dive (already), calm 1.0-2.5 -> 1.5-3 with real rest (smooth is still 47-59%); (c) 0.4 s pre-run telegraph (line twitch) before every burst: tutorial friendliness.
- **Darter <- dart.** (a) micro-bursts: burst 0.4-0.9 s, calm 0.3-0.8 s (dart never rests, `still` 5%; P 0.45 and no dives, so this survives the 9.5 reel-economy check at ~24 s); (b) `flipRate` 0.2 -> 0.8-1.2/s with min flip gap 0.3 s (dart reversals 1.5-2.6/s vs mixed 0.4-0.9, i.e. 3x); (c) burstDir picks "opposite of lateral" at 0.5 not 0.75 so you can't pre-counter. Lower `staminaDrain` payoff (steering has to be spent on feints).
- **Bulldog <- sinker.** The sinker is a constant pull, not an event. (a) `calmDrift`: during Calm line creeps out at `runSpeed*0.25`, ramping over 2.5 s (0.01/frame to 1.5 px/frame) so resting has a cost and the player must reel/pause rhythmically; (b) pullStrength ramps 0->1 across the burst (`diveRamp` 1.5 s) instead of a step, so the "let it run" window is felt; (c) keep diveChance 0.35 but lengthen Calm after a dive (tired fish).
- **Thrasher <- mixed/dart at d 80-95** (p95 ~550 px/s = whole track in 1 s, retarget 1.5/s, lag 0.25-0.55 s). (a) keep calm >=1.0 s and take the frantic feel from flips and speed jitter: shortening calm to 0.3-0.8 s cuts the calm fraction from ~0.44 to ~0.35 and, with P~0.7 (reel rate 0.3 m/s vs run-out 0.42 m/s in Burst), pushes a 16 m fight to ~60 s vs the 35-55 s mid target, so any timing change needs re-validation against the 9.12 bot targets; (b) per-burst `lateralSpeed` jitter x[0.7,1.3] (Stardew's U[10,29] lag); (c) flips triggered by player lateral crossing 0 rather than pure timer, so it feels like it answers your steering. Keep staminaDrain 0.09 so it tires fast: frantic but short.
- **Runner <- smooth at d 70-95 plus floater bias.** Long glides across the whole track with few reversals (0.25-0.43/s) and real rests (28-32%). (a) burst 3-5 s, flipRate 0.05, near-zero flips; (b) travel is the threat: runSpeed up (0.8->1.0) but lateralSpeed down so it is a line-out problem, not a steering one; (c) mandatory 2-3 s exhausted "gasp" calm at stamina <0.3 (smooth idles after each target) to cue the reel window.
Cross-cut: derive per-species intensity from one scalar (Stardew's d) driving rate+speed+amplitude together, rather than tuning each number: `FishSpeciesDef.fightIntensity` 0.5-1.5 scaling calm length (/), lateralSpeed (x), flipRate (x). Species already scale by weight; add this for within-profile spread (carp d=15 vs catfish d=75 are both mixed).

## 3. Catch progress and player-assist levers (`BobberBar` ctor + update)

- Progress `distanceFromCatching` starts **0.3**. Win at 1.0, lose at 0.0. **First catch of a save is unfailable**: start 0.1, d floored to 50 (looks hard), but the whole drain branch is guarded by `fishCaught.Length != 0 || currentMinigame != null`, so progress never drains and only fills (showy tutorial fight that cannot be lost).
- In bar: **+0.002/frame (0.12/s)**. Out of bar: **-0.003/frame (0.18/s)** (-0.002 with the Training Rod). Win from 0.3 takes at least 5.8 s; losing from 0.3 takes 1.7 s of continuous miss. Fish difficulty does not change these rates, only how often you are in the bar. Drain:fill = 1.5:1.
- Bar: height `96 + 8*FishingLevel` px of a 568 px track (17%->31% at level 10); gravity/lift +/-0.25 px/frame^2 each frame (button = up); in-bar gravity multiplier x0.6; wall bounce x(-2/3); pressing at floor/ceiling zeroes speed. Training Rod (`upgradeLevel==1`, "beginner"): bar +40 -8*level while level<5, drain 0.002, quality forced 0, size forced min; and its spawn filter only allows species with d<50 (chance x1.1): training wheels that cap reward and pool.
- Perfect: `perfect=false` the first frame fish leaves the bar after having been in it (contact lost once). Perfect upgrades quality in `doPullFishFromWater`: silver->gold, gold->iridium (normal stays normal); XP x2.4 ("Perfect!" sparkle).
- Treasure (rolled at hook): `0.15 + 0.005*luck + dailyLuck/2` (+0.15 Magnet bait, +0.15 Pirate profession, +0.05 per Treasure Hunter tackle), only after 2+ catches. Chest appears 1-3 s into the fight at a random height away from the bar; holding the bar on it fills `+0.0135/frame` (about 1.2 s dwell), decays `-0.01/frame` off it. Optional second objective that competes with chasing the fish; fish progress still drains while you detour.
- Tackle levers (bobber ids): **Cork Bobber 695** bar +24 px. **Trap Bobber 694** drain 0.003 -> 0.002, 0.0015, 0.00125, floor 0.001 per extra copy (diminishing). **Lead Bobber 692** bottom-wall bounce x(0.1*count) (bar settles on the floor instead of rebounding: stability). **Barbed Hook 691** in-bar gravity x0.3 (vs 0.6) and a +/-0.2 (then 0.05) speed nudge toward the fish centre (soft auto-aim). **Treasure Hunter 693** while the treasure is in-bar the fish being out of bar does not drain or break "perfect" (a protected greed window). **Quality Bobber 877** +1 quality tier (cap iridium). Bait: **Deluxe** bar +12 px; **Challenge** gives 3 lives (each lost contact -1; at 0 the fish escapes) and you land the remaining count as fish; **Wild** 25%+luck/2 chance of 2 fish. Blessing of the Waters: d>20 halves (boss x0.75), drain x0.5.
- Failure feedback: bar shake + "tinyWhip" the frame it loses contact; screen shakes 500 ms on end; fish size shrinks by 1 per 800 ms out of bar (see 4).
- Takeaway as levers we can mirror with rods/baits: (1) window size (`angleWindow`), (2) leak rate (`strainScale`/`relaxMult`), (3) overshoot damping (lateral clamp / `calmCenterPull`), (4) soft aim assist (`steerRate` toward run direction), (5) protected greed window, (6) reward tier (quality), (7) starter-rod gentle mode with capped reward, (8) unfailable first-catch tutorial fight.

## 4. Size / quality roll (`startMinigameEndFunction`, `BobberBar` ctor)

```
f = clearWaterDistance(0..5 tiles from shore)/5
f *= U{1+lvl/2 .. max(6,1+lvl/2)-1} / 5          // level 0: 1..5 -> 0.2..1.0; level >=10: 6 -> 1.2
if favBait (bait scale==1 flag): f *= 1.2
f *= 1 + U[-10,10]/100;  f = clamp(f, 0, 1)
size  = (int)(minSize + (maxSize-minSize)*f) + 1             // inches
quality = f < 0.33 ? Normal : f < 0.66 ? Silver : Gold       // Gold=2, Iridium=4 only via perfect or Quality Bobber
```
- Size loss during the fight: `fishSize -= 1` per 800 ms spent out of bar (floor = minSize). Winning non-perfect at exactly maxSize gives `max-1` (the `+1` after the roll makes f=1.0 hit max+1 before this trim).
- Quality Bobber +1 tier per copy (3 => iridium). Training Rod: quality 0 and minSize regardless. Luck/skill: size roll depends on **skill and shore distance (cast distance)**, not on difficulty; difficulty only affects XP (`(q+1)*3 + d/3`, treasure x2.2, perfect x2.4, boss x5). Bait barely moves size (favBait x1.2).
- Value uses quality multiplier elsewhere; irrelevant here.

## 5. Spawn selection (`GameLocation.GetFishFromLocationData`, `CheckGenericFishRequirements`, `SpawnFishData`)

- Candidate list = `Data/Locations["Default"].Fish` + `location.Fish`, **sorted by `Precedence` ascending, ties shuffled freshly per cast**. Default list: Qi beans (-1000, 15%), secret note/item (+1000, 8%), trash (+2000, chance 1.0, random of 6 items). Legendaries -100, area/position specials -90..-10, ordinary fish 0.
- Walk the list; per entry (first full pass wins):
  1. Hard gates: `FishAreaId` equals the bobber tile's fish area (named rectangles per map), `Season` field, `PlayerPosition` / `BobberPosition` rectangles, `MinFishingLevel`, `MinDistanceFromShore <= depth <= MaxDistanceFromShore` (depth = `distanceToLand`, 0-5 tiles), `RequireMagicBait`.
  2. Bernoulli on `GetChance`: `Chance` (+CuriosityLureBuff, +dailyLuck if `ApplyDailyLuck`, QuantityModifiers) then `x SpecificBaitMultiplier (1.66) + SpecificBaitBuff` if the bait targets this fish, `+ ChanceBoostPerLuckLevel*luck`.
  3. `Condition` game-state query (almost all seasons are `LOCATION_SEASON Here spring fall`, plus `WEATHER`, `TIME`, festival, mail flags).
  4. Item resolved (random-item queries pick one), then `CatchLimit` per player, then `Data/Fish` per-species requirements: time spans (`600 1900`, multi-span like `600 1100 1800 2600`), weather `sunny|rainy|both` (skipped under Magic Bait, as are time spans), `minFishingLevel`, then a second Bernoulli:
     `p = chance - max(0, maxDepth - waterDepth) * (depthMult*chance) + FishingLevel/50; p = min(p, 0.9)`.
     Curiosity Lure lifts `p<0.25` via `p' = 0.68*p + 0.085` (floor lift: p=0 -> 8.5%); target bait `p *= 1.66`.
     Example: Largemouth Bass chance .4, maxDepth 3, depthMult .2: at depth 0 `0.4 - 3*0.08 = 0.16`, depth 3+ 0.40 (+0.02/level).
  5. First entry to pass all returns. Trash (+2000, chance 1) guarantees a result, so **junk is emergent**: more eligible fish => less junk. Specific bait (`SpecificBait`) skips the first two non-target successes (up to 3 rerolls) before accepting a non-target. Non-plain bait enables a second pass only if the first returned nothing (practically unused).
- Per-species spawn data (`Data/Fish`): `name/difficulty/behavior/minSize/maxSize/timeSpans/seasons/weather/(bait table, unused)/maxDepth/chance/depthMult/minLevel/tutorialFlag`. Typical chances .05-.65 (legendaries .05-.1 with level 3-10 gates), maxDepth 1-5, depthMult 0.1-0.55.
- Maps share no weighted pool: probability of fish i is roughly `p_i * prod(1-p_j for j earlier in the shuffled order)`, so rarity comes from low `p` and tier order, not weights; shuffle order makes it symmetric among equals.
- Frenzy/hotspot override: within 2 tiles, forced species, bypasses everything else.

## Recommendations for M2 tuning

1. `FishingTuningDef.biteWaitMax` / `RodDef` + `bait.biteSpeedMod` (spec 9.4, 9.12): Stardew tackle lowers the **upper bound** (-5 s / -10 s of 30 s) and bait is a flat x0.5 (x0.375 wild); keeps `biteWaitMin` as the floor so minimum surprise stays. Consider modelling M2 baits as `biteWaitMax -= X` rather than x`biteSpeedMod` so skilled setups compress the long tail (dead time) and don't produce instant bites. Also first-cast x0.75.
2. `hookWindow` (spec 9.4 / 12.3 `FishSpeciesDef.hookWindow`): Stardew's mandatory window is 0.8 s with a silent re-roll on a miss and no bait loss, plus a 1.5 s hook beat. Keep auto-hook default; make the 0.8 s window the twitchy-species value (already 0.8-1.2); a miss should re-roll `biteWait` without refund concerns.
3. `FightProfileDef` (spec 9.6 table): add `burstRamp` (ease-in 0.5-0.8 s, Steady/Runner), `calmDrift` (sinker-style constant line creep, Bulldog), micro-burst timings + `flipRate` 0.8-1.2 with 0.3 s gap (Darter), per-burst `lateralSpeed` jitter (Thrasher), and an explicit exhausted-rest cue (Runner). These are the ideas that make smooth/dart/sinker/mixed feel distinct in Stardew.
4. `FishSpeciesDef.fightIntensity` (new, 12.3) driving calm length, lateralSpeed and flipRate together (Stardew's single d scales rate + speed + amplitude); lets 25 species spread within 5 profiles without per-species hand tuning.
5. `CatchResult.Quality` (spec 9.8, but needs 9.9: the fight runs on the owner, so the owner's result RPC must carry `maxTension`/out-of-window time and the host must apply and bound-check it): add a "clean fight" bonus mirroring Perfect: quality +1 tier (Normal->Good->Trophy) if `State.maxTension < 0.8`, no pin timer, never out of `angleWindow` >0.5 s. Uses existing `FightState.maxTension`. Stardew perfect = loss of contact never happened.
6. Weight roll (9.8, also 9.9 for the same owner-reported-stats reason, and it touches the catch reveal): add cast-distance term `skew = clamp(weightSkew - 0.4*Luck - 0.3*(castDist/castMaxDist), 1.2, 3.0)`; Stardew size scales directly with shore distance (0..5 tiles) and skill. Also let `weightT` shrink during the fight (-1 unit per 0.8 s out of bar in Stardew; mirror with -0.02*weightT per second above tension 0.8 or out of window), so sloppy fights land smaller fish.
7. `RodDef` assist levers (9.7): map Cork -> `angleWindow` +, Trap -> `relaxMult`/`strainScale` -, Lead -> lateral damping (`calmCenterPull` or soft clamp), Barbed -> `steerAssist` (small push toward the fish's current run side), Treasure Hunter -> bonus objective, Quality Bobber -> +1 quality tier. Starter Twig rod should be the "beginner" variant: generous window, capped reward (quality Normal, weight <= midpoint) per 9.12 starter check; consider an unfailable first fight of a save/run (Steady, no snap) as Stardew's tutorial does.
8. (M3) `SpawnRule.minDepth` (spec 12.3/12.4 `Passes`): replace hard cutoff with Stardew's ramp `avail = clamp01((depth - minDepth)/depthRamp + ...)` multiplying `weight` (or add `depthRamp` m) so deeper casts gradually raise a species' share (`chance - (maxDepth-depth)*depthMult*chance`) instead of a binary gate.
9. (M3) `BaitMods` (12.4): add `IgnoresGates` (Magic Bait: skips season/time/weather in `Passes`), `RareLift` (Curiosity Lure: `w' = 0.68*w + floor`) and set `AffinityMult` default 1.66 with "reroll twice" semantic for targeted bait (draw up to 3 times, accept first matching species/tag). Keeps our weighted pool but borrows the best Stardew bait ideas.
10. (M3/M4) Hotspot (`inHotspot` multiplier, 9.4 and 12.7; Stardew applies it to the first wait only): Stardew uses /4 bite time, +1 depth tier, one active per map, 50%/10 min spawn, >=60 min lifetime with rising despawn chance, 2-4 tiles from shore. Our 0.6 is far weaker; try 0.4-0.5 plus +1 sub-area depth bump, and cap to one active hotspot per spot.
