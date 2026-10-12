# Gamble With Your Friends (GWYF) - mechanics notes for FishGame

Method: ILSpy on Assembly-CSharp.dll (Unity 6000.3.6f1, Mono, Mirror + FizzySteamworks + Dissonance). Numbers are *local fallbacks*;
GameSettings has `useAPIUpdates=true`, so remote values may override. No web API contacted. Nothing copied; constants quoted for research only.
Provenance tags: *(asset)* = decoded from resources.assets (GameSettings, ItemPriceSettings, LobbySettings, reel chanceOfWin 0.65, Drink, Upgrade +0.5, Taser);
*(code)* = C# field initializer / literal in the decompiled class - the scene/prefab instance may differ and was not parsed (Crash, Plinko, Keno, Minesweeper, Blackjack decks, CrossyRoad, CoinFlip, DebtBag, Bat, TimeMachine, QuotaGun counts).

## 1. Core loop and economy
- Loop: HomeScene (hub: shop, challenge booth, cosmetics, organ shredder) -> elevator -> CasinoScene (one timed day) -> back. `GameManager.ProgressGameRoutine`.
  Day timer `dayDuration = 300 s` (per-save rule `roundTimerDurationSeconds`, timer can be disabled). `OnTimerEnd`: destroys all consumables, locks elevator, force-teleports crew home.
- **One shared crew wallet** (`MoneyManager`, host-only SyncVars, BigNumber mantissa+exponent). Bets are paid from it, payouts go back to it. Start money 1000.
  Second shared currency: **tickets** (start 15; items, rerolls, cosmetics). Money never converts to tickets directly.
- **Quota = threshold, not deducted** (same as our R7): `ProgressGameRoutine`: if `daysLeft<=0 && balance<currentQuota` -> lose; else `ServerPayDebt` (+tickets, next quota). Balance is kept.
  Quirk: `daysLeft` is local `daysBeforeQuota = 1` (class default 3), so each quota = exactly one 5-min day; with >1 the "else" branch would pay early.
- Target setting `GameSettings.GetQuota(i, prev, money, diffMult)`: `i==0: startingQuota 1200`; else `(prev + 0.75*(money-prev)) * quotas[i] * difficulty` (catchUpFactor 0.75),
  rounded to nearest 2 significant digits (`FathF.RoundByFirstNDigits`, AwayFromZero). So 75% of any surplus is folded into the next target (rich crews get chased).
  `quotas[]` (12 steps, local, asset): 1.2, 1.3, 1.5, 1.7, 1.9, 2.1, 2.3, 2.6, 3.0, 3.3, 3.6, 4.0 (index 0 unused). **Compounding**: each quota is ~prev x quotas[i], so Q12/Q1 ~ 11,700x (ratios between neighbouring multipliers are only 1.10-1.15, but the quota itself grows x1.3..x4.0 per day).
  If money == quota each time (rounded each step): 1200, 1600, 2400, 4100, 7800, 16k, 37k, 96k, 290k, 960k, 3.5M, 14M. If the crew doubles its money each quota (surplus 100%): ~6.4 billion by Q12 -> BigNumber needed.
  This only works because income compounds too: the stake cap scales with the quota (`MaxBet` below). Fishing sales do not compound.
  Past the array (`EndlessMode`): `GetQuotaMultiplier` extrapolates with a growing delta (`ComputeQuotaDeltaAcceleration`, min 0.05).
- Difficulty (`SaveGameRules`): quota mult Normal 1 / Hard 1.5 / Extreme 2. Other per-save rules: timer on/off + duration, startingMoney/Tickets, `restartSameDayOnLoss`, `endlessMode`, `seed`.
  `restartSameDayOnLoss` = forgiving mode: `ServerRetrySameDay` restores `dayStartBalance/dayStartTicketBalance` (snapshot taken in `GameInitializeRoutine`), refunds the day.
- **Floors**: 4 casino floors, access gated by quotas passed (`requiredQuotaToAccess` 0/3/6/9; `DayToFloor`). 12 quotas = 4 floors x 3. Floor sets ticket reward (9/11/13/15), shredder prices, reroll cost (2/3/4/5), item price step.
- Failure: any failed quota -> LoseStateScene -> `SaveManager.ResetCurrentSaveToDefaults()` = full run wipe (money, quota, items, upgrades, organs). Rogue-lite reset; only cosmetics + Steam achievements persist.
- Auxiliary money (`GetAuxiliaryMoney`, quota x pct[dayIdx]) exists as a mid-quota catch-up handout but local pct = [0,0,0] (unused at 1-day quotas).
- **DebtZone / MoneyPipe (the ending, not the loop)**: after quota 12 (`successfulQuota >= quotas.Length`, non-endless) -> WinStateScene. First player to press a button locks the choice (`WinSceneManager._isChoiceMade`):
  (a) Pay Debt: `DebtBagMachine` converts the *whole balance* into N physical `DebtBag` items (`balance/spawnAmount` each, last bag = remainder; money removed from wallet as each spawns);
  crew carries/throws bags into `DebtZone`; `CheckDebtBags` (host, every 0.1 s) sums `moneyInBag` of bags whose position is inside the collider; button enabled only when ALL bags are inside and sum >= `_totalDebtMoney` (balance at scene start);
  `ServerTryInitializeZone` then fires `MoneyPipe.ServerStartSucking` (bags lerp along pipe 1.5 s, accel 10, max speed 50, shrink 0.5 s, destroyed) -> cutscene "Debt_Paid".
  (b) Coin flip: `CoinFlip.ServerPlayCoinFlip` adds random up-force 10-20 and torque 2-4 to a physical coin (seeded), waits until it stops (max 10 s), `Angle(coin.up, up) > 90` = win. Lose -> pipe eats the bags -> "Coinflip_Lost".
  (c) Ending credits: spline camera 15 s + credits 20 s, Steam achievement, skip UI (hold 2 s).

## 2. Gambling games as content
Common flow (`GameBase.TryStartGame/Payout`, all [Server]): bet validated `MinBet<=bet<=MaxBet`, wallet debited, `StartGame`, `Payout(multiplier)` credits `bet*mult` through buffs, `GameResultsManager.RegisterResult`.
- **Bet bounds scale with run progress**: `MaxBet = max(5, baseMaxBet * currentQuota * 0.001 * 2^(casinoLevel - currentFloor - 1) * MaxBetOverrideMultiplier)`; `MinBet = max(1, baseMinBet * quota * 0.001 * 2^(...))`.
  Defaults baseMin 1 / baseMax 100 *(code)* => 0.1% .. 10% of quota at exponent 0; at the top floor exponent is `level - floor - 1` (= -1 if casinoLevel is 0-indexed like `currentFloor`; `casinoLevel` is a prefab SyncVar, not verified) => 0.05%..5%, so treat the real cap as **5-10% of quota**; each floor below halves it.
  `baseMin/baseMax/estimatedValue` are overridden per game by the remote API (see Not reachable).
  Taser raises the cap (see 3). One bad bet can never exceed a few % of quota => variance is bounded, losing is funny not fatal.
- **House edge lever = `EstimatedValue` (default 1, x floor `estimatedValueMultiplier`=1.0)**, multiplied into payouts of most games. Roulette and Crash do NOT use it (`Payout(_multiplier)`).
- Crash (`Crash.GetRandomCrashPoint`): `instantCrashChance 0.1` *(code)* -> 1.01x; else `crash = clamp(1/(r*0.9), 1.001, 100)` with r~U(0.001,1). => if defaults hold, P(reach m) = 1/m for m>=1.111, so cashout EV = 1.0 at any m (0% edge, 100x cap).
  Curve `mult = exp(0.15 t)`: 2x 4.6 s, 5x 10.7 s, 10x 15.4 s, 100x 30.7 s. Host drives the SyncVar each frame, 3-2-1 countdown, `Cashout()` is the player's button; crash point rolled before start.
- Plinko: physical puck, 9 pockets x `[0.2,0.5,1,2,5,2,1,0.5,0.2] * EstimatedValue`, host detects pocket trigger, drop cooldown. Edge depends on physics + EV (prefab/remote).
- Slots: 5 reels x 3 rows (`BoolGrid3x5` pattern assets, each with a multiplier); symbols uniform over atlas via hash; pattern wins when all cells equal; `mult = pattern.multiplier * (1 - (symIdx-1.5)*0.5)`, summed over patterns, x EV.
  Reels spin 5 s + 1 s stagger (anticipation theatre). Each reel's seed from `GetSeededRandom(i*10000)`.
- Roulette: single-zero 0-36 (`rng.Next(0,37)`), straight 36x, red/black/odd/even/low/high 2x, dozens/columns 3x. Multi-bet chips, per-spot cap = MaxBet; golden chip restricts to one spot. Edge 2.70%.
- Blackjack: 1 deck *(code)* (reshuffled when empty -> countable), dealer draws to 17, win 2x total return (1:1), **blackjack 3x total return (2:1, vs 1.5:1 normal)**, push 1x, double + one split. All x EV. Player-friendly rules, edge comes from EV.
- Others (all `x EstimatedValue`): HiLo = player picks win chance p on a slider, pays `EV/p` (fair risk dial); Minesweeper 25 tiles, 1-24 mines (default 3), multiplier = product of 1/P(safe) per reveal, cash out any time;
  DragonTower 4 buttons/floor, `1/0.75^floor`; CrossyRoad 10 steps, `stepMultipliers[]` ladder with per-step crash chance derived as `1 - m_k/m_{k+1}` (constant EV at every stop);
  Keno 10 diamonds, up to 10 picks, hypergeometric weights, `raw=hits^2` normalised so EV is exactly EV regardless of picks; Craps (physical dice, `maxPointPhaseThrows` cap) come-out 7/11 -> 2x, 2/3/12 -> lose, else point set; point hit -> 4x, 7 loses, throw cap -> refund (1x);
  DuckRace 4x total return on the winning duck; Poker (draw/replace) payout pair..straight flush = 1,2,3,4,6,9,25,50x (pair is a refund); Baccarat player/banker 2x (tie loses the bet), tie 10x total (9:1); MoneyWheel/WheelOfFortune segment lists (prefab).
- **Bad-beat / pity** (`CasinoOffersManager`, host): Blackjack lose 3 times with hand >=18 (not 21) -> +$1000; Slots lose 5 times in a row -> growing jackpot (`reward += bet + bet*loseStreak`). Losses feed a visible bonus.
- Fun vs punishing: capped stakes (% of quota), visible countdown/animation before outcomes, per-win NPC crowds (`GamblerBehavior.payoutThreshold 200`, watchRadius 30), refund items, bad-beat bonuses, shared wallet so the crew cheers/boos.

## 3. Shop and items (all tickets; all **one-day consumables**, destroyed at day end)
- Items spawn on fixed stamps in Home from per-floor loot tables (`StampManager`, `ItemStampManager`; unique loot per position; preview via `NextCasinoPredicter`: seeded shuffle of floor loot with `seed, successfulQuota, floor`).
  **Physical purchase**: drop items in `MerchantBuyZone`; total cost shown on a sign (`CalculatePrice` summed every 0.1 s); button pays tickets from the shared pool (host). Reroll all stamps: `(rerollCount+1) * floor.rerollCost` tickets.
- Price = `base + floorIdx * perFloor` tickets (floor = `DayToFloor(daysPassed-1)`); cosmetics capsule = `2 + floorIdx + rarityAdd[0,1,3,6,10]` (Common..Legendary). Local table (ItemPriceSettings, base/perFloor):
  Upgrade GamblersConfidence 7/3, Insurance 7/3, StakeHolder 6/3, BonusDraw 8/2; Coordinator 6/3; Cross (Immunity) 7/4; Microphone 6/3; Drink 5/3; Taser 4/2; GoldenChip 4/2; DevilsReel 3/2; AngelsReel 3/2;
  TimeMachine 6/3; MysteryBox 4/2; QuotaGun 5/3; Bat free; plush toys (Maxwell, Ratto) free. Income: 9-15 tickets per quota + excess bonus +1..+25 (money/quota >= 1.1/1.25/1.5/2/3/5 per floor table) + challenges.
- Effects (`UpgradeManager` per-player, per-day; defaults GamblersConfidence 1.0, Insurance 0, Stakeholder 1.0, BonusDraw 0):
  - GamblersConfidence (+0.5): multiplies the *profit part* of every win (`bet + profit*tipsy*(1+melody)*GC`). Stakeholder (+0.5) scales all item strengths (reel wins, taser, drink, timers).
  - Insurance: on a loss returns `loss*insurance` (stacks `1-(1-a)(1-b)`). BonusDraw: chance per profitable game to gain +1 ticket.
  - **Angel's Reel**: after a net-loss game, spin: `chanceOfWin 0.65` (asset) to refund the loss (x Stakeholder), else 0 => expected +0.65 x loss. **Devil's Reel**: after a net-win profit P, 0.65 to gain a further +2P (total 3P), else lose P => expected net +0.95 P (triple-or-nothing, positive EV).
    Design read: both are positive-EV variance toys sold for 3 tickets, i.e. gambling "feels" risky but is mildly player-favourable.
  - GoldenChip: apply to a machine's keypad (1 s hold) -> next play is free (no stake debit), consumed. Taser (asset: tick 0.5 s, use 0.1, increase 1.0): hold on a machine, +0.1 per tick to `MaxBetOverrideMultiplier`; full charge = 10 ticks = 5 s => cap x2.
  - Drink: `TipsyFortune` buff +1.0 (profit x2) for 45 s if drunk (drinkTime 2 s) or 22.5 s if thrown at a friend within radius 3. Microphone: shout (voice amplitude charges it) -> `InspiringMelody` area buff (range 10, 30 s): profit x(1+melody) and insurance on losses.
  - Cross: `Immunity` area buff, losses fully refunded, duration drains with time. TimeMachine: reverts the last 60 s of payouts (`PayoutTracker.RollbackLastSeconds`) and rewinds the day timer 60 s.
  - MysteryBox: weighted spawn from a list, seeded with a per-box counter. Coordinator: handheld camera; while pointed at *other* players in range it banks a share of their winning streak (charge per win, reset on loss);
  throw/smash to cash `bank * multipliers[charge]` to the holder = betting on friends. ClawMachine: prize machine (`grabSuccessChance` 0.4 default per prize, weighted). Gacha: 1 ticket capsule -> cosmetic.
  - QuotaGun: shoot a player; if wallet < quota and host-side `removableOrganCount 3` not used, removes a random organ (eye/mouth/body) and pays `quota/3` into the shared wallet (capped to the gap). BodyShreddingMachine buys organs for tickets (floor 0: eye 7, mouth 5, body 10, x price mult), 50% to appear daily.
  - Lost mouth = muted-muffled voice FX, lost eye = vision cut, lost body = ragdoll and *can be picked up and carried/thrown by teammates* (`PlayerCarry`). Organs reset at the ending.
- Luck/odds interaction: nothing changes RNG probabilities. Items change payout shape (multiply profit, refund losses, raise caps, free plays) - odds stay fixed, **EV and variance are shifted by items**. Dredge-style "flat boost" avoided except GC/Stakeholder.

## 4. Progression and pacing
- 12 quotas, one 300 s day each, 4 floors; new floor = higher stakes, better rewards, new games in the loot table. Estimate: ~1.5-2 h per full run (12 x 5 min + hub/summary), not measured.
- `BossAnnouncement` is only a slide-down panel; `bossMonologueDelay 5 s` after day start the boss/announcer voice plays; voice audio is streamed from a remote websocket (`WebSocketManager`, `voice_start/audio/stop`) -> remote-only content.
  `BossGame`: win `requiredConsecutiveWins = 3` games in a row at a boss machine.
- Escalation = quota curve + floor unlocks + bet cap rising with quota. No scripted per-day events locally.
- Day summary (`DaySummaryUI`, skippable per segment): day number, quota bar filling vs balance, start balance -> end balance, profit counter, 3D profit line graph (`ProfitLineGraph3D`/`PayoutTracker`), ticket sources list ("Quota (100%)", "Quota Excess Reward (n%)", completed challenges).
- Game over (`GameOverUI`): per-player contribution list sorted by net profit (hero/villain board), lifetime net total (`GetLifetimeNetTotal`), day reached, tickets. Victory uses the same screen with "game complete".
- Daily challenge booth: `ChallengeBooth` hands out `dailyAvailableChallengeCount` challenges per day, rerollable; conditions (win/loss counts, bet amount, blackjack hand value, craps sequence, duck win, poker hand, profit, payout multiplier, time) pay tickets and some link Steam achievements.

## 5. Cosmetics and meta
- Meta is *only* cosmetics + achievements. `CosmeticsUnlockManager` (MonoSingleton, local): `cosmetics_unlocks.json` in persistentDataPath: unlocked ids, equipped per slot, player colour hex, timestamp, version "1.0". `UnlockRandomCosmetic` = uniform over still-locked ones (ignores rarity).
- Cosmetics are obtained from in-run gacha capsules (`GachaSphere`, seeded cosmetic from `seed + quota idx + position`), paid in run tickets; so tickets are run-scoped, cosmetics permanent. 5 rarities. Default clothing always unlocked.
- Run save (host, `SaveData` JSON in `Saves/`): quota idx, days, money, tickets, floor, item ids, challenge progress, organ states, upgrade states, per-player profit history, lifetime wins/losses, `seed`, rules. Saved on every Home scene entry.

## 6. Co-op social mechanics
- Shared wallet: every bet risks the crew's money; anyone can walk up and gamble the whole bank (bounded by the % of quota cap). No personal money, no theft.
- Interference items: Bat (knockback, 15% break chance), QuotaGun (shoot a friend, sell their organs for quota), Drink throw (buff a friend), Taser (buff the machine), Coordinator (watch friends' streaks), Microphone (sing to buff the room),
  PlayerCarry (carry/throw ragdolled friends), PlayerPingManager, emote wheel, proximity voice (Dissonance) with voice manipulation items.
- Spectating is physical and spatial: crowd around a machine, NPCs (30) gather at big payouts, 3D profit graph, per-player result boards at the end. Win/lose screens rank players (blame/credit).
- Ending decision is one-press: first player to hit Pay Debt or Coin flip decides for everyone.
- Roles by ownership: bets, results, buffs and upgrades are tracked per `PlayerProfile` (steamId) even though money is shared (`GameResultsManager`, `UpgradeManager`).

## 7. Networking and authority (Mirror, FizzySteamworks, Steam lobbies)
- Host-authoritative: money, quota, timer, RNG, outcomes are all [Server]-only SyncVars/methods; clients only render. `ServerChangeScene` + a readiness barrier (`SceneReadyMessage`, `_sceneEpoch`, `_scenePlayReady`, `InitializeScene` runs when all spawned).
- RNG: deterministic `System.Random` from `save.seed` hashed with context (day, game turn, world position, per-feature counters: MysteryBox/Angel/Devil). Same seed + same actions => same outcomes (anti-reroll on reload, preview of tomorrow possible).
  Physics can supply spectacle (coin, plinko, dice) but the host reads the result.
- Lobby: Steam lobby, `maxPlayers 6` local, join code length 9, lobby joinable only in Home (`SetLobbyJoinable(false)` in casino). Late join handled (`OnPlayerLateJoined`).
- Weak spots (do not copy): `[Command(requiresAuthority=false)]` is used widely with client-supplied args: `MoneyManager.CmdTryChangeBalance(amount, changer, type)` (any client can ask for any amount),
  `InteractableBase.CmdOnHold(PlayerInteract)` (sender identity not checked). Bet validation, however, is correct (range + wallet checked on host in `TryStartGame`).
- Patterns worth translating: server-only mutation API + one `OnBalanceChanged(amount, who, ChangeType)` event feeding stats/UI; `ChangeType` enum (Bet, GameResult, Item, Save, Misc); rollback via a payout log; `GameResultsManager` per-player ledger;
  `restartSameDayOnLoss` via day-start snapshot; physical-state validation (`DebtZone`: host polls bag positions).

## Recommendations
- 14.4 quota (M4): GWYF compounds (quota ~prev x 1.3..4.0, 75% surplus carried) and survives only because stake caps scale with quota; our fishing sales don't compound, so do NOT copy the curve. Surplus-carry would conflict with the tuned 14.4/R60 pressure targets; noted only.
- 14.3/14.9 + 15.1 (M4 stub, M5 finish): a physical finale for the day-7 win payout: convert the haul into N carryable crates, host-poll a dock zone, button enabled only when all are inside and total >= target; one-press decision with a double-or-nothing coin option on the infinite vote.
- 14.7 shop (M4): physical basket pricing (items dropped in a buy zone, live total sign, one shared-wallet button) and escalating reroll `(rerolls+1) x base`; keep Shells separate (validates R59: GWYF tickets are run-scoped, cosmetics permanent).
- 14.7 / 9.7 items (M4): consumables that shift variance not odds, framed as risky but mildly positive-EV: refund-after-bad-cast (65% refund, "Angel"), triple-or-nothing on a good haul (65%, "Devil"), free-cast chip, cap-raising charger, area buff; fits the section 2 variety rule better than flat boosts.
- 15.4 happenings / 15.5 (M5): friend-slop items from GWYF: throwable buff drink (45 s drunk / 22.5 s thrown), shout-to-charge microphone area buff, "film your friend's streak" camera, and carrying incapacitated players; all host-validated.
- 9.8 catch roll (M2): add bad-beat/pity (GWYF `CasinoOffersManager`: lose N in a row -> growing reward) as an optional Luck nudge after consecutive snaps/misses, shown to the crew; tune in `FishingTuningDef` (9.12).
- 6.7 RPC conventions (M2): GWYF's `[Command(requiresAuthority=false)]` with client-sent amounts/identities (`CmdTryChangeBalance`, `CmdOnHold(PlayerInteract)`) is the anti-pattern; in NGO derive the actor from `RpcParams.Receive.SenderClientId`, never accept money amounts, validate range/phase on host (as GWYF does for bets).
- 11.5 day summary (M4): add a per-player contribution board sorted by cash/kg caught plus "day reached"/lifetime net on the Result screen; show ticket-style income breakdown (base, excess %, requests) with skippable segments.
- 14.7 / 14.8 (M4): wager-like systems (dock bets, triple-or-nothing) should cap stakes at a % of the current quota/wallet (GWYF 5-10% of quota, halving per tier below) so one gamble never ends the run.
- 14.9 / 6.8 (M4): keep GWYF's `restartSameDayOnLoss` idea as an optional forgiving setting (snapshot wallet at day start, restore on a failed Settle) for solo/learning runs; spec already has nightly snapshots (R56).

## Not reachable (remote API / prefab-only)
- Per-game `estimatedValue` (house edge), `baseMinBet`, `baseMaxBet`: set only via `RequestSettingsFromApi` (`GameSettings` private class: `estimatedValue`, `baseMinBet`, `baseMaxBet`) from `apiBaseUrl`/`gameId` (empty in code, apiEnabled flag in scene). Real edges for Plinko/Slots/Blackjack/etc. unknown.
- Slots atlas size and pattern multipliers (SlotPattern assets in scene/prefabs), MoneyWheel/WheelOfFortune segment tables, Keno button count, Plinko physics edge, Duck count - scene/prefab data not extracted.
- Remote overrides of `GameSettings` (quotas, floorData, auxiliaryMoneyPercentage), `ItemPriceSettings`, `ChallengeSettings` via `DynamicSettingsApplier` / `WebSocketManager.GameSettingsChangedMessage`; boss voice lines (`voice_start/audio` websocket) - local values above may be stale.
- Challenge list contents and ticket rewards (ScriptableObjects not decoded); cosmetics catalogue and gacha loot weights (MMLootTable assets); item loot tables per floor.
