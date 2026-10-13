# FishGame - the fun formula (design input for the M4 design session)

_2026-10-13. Written from the user's own reasons for loving four reference games. This is not decided design: it is the input for a structured brainstorm before M4. Spec R76 (mafia chase downriver, the starfish god, 7 days = 7 river maps) is the frame it fits into. Earlier mechanics notes on these games live in `docs/superpowers/research/`._

## 1. What the user loves (their words, condensed)

| Game | Why it's fun |
|---|---|
| **Gamble With Your Friends** | Chaotic mini-games (literally gambling). Items that can completely **save or ruin your day**. |
| **RV There Yet** | A **large map** and **slightly janky physics**. Every run differs unless you take a preset path. **Not easy even once you know the mechanics.** |
| **REPO** | **Tense situations.** The chance of a **big loot haul in one run**. Funny details (eyes side-eyeing players). Many **items that change your game, protect you or upgrade you**. |
| **PEAK** | **Map length, item selection, map variety, and the number of situations you end up in.** It feels like a **real journey with ups and downs**: lots of loot or barely any. |

**The target feeling:** "a bit chaotic but more entertaining", not random noise. Fishing alone holds attention for a few runs. It needs to be the engine, not the whole game.

## 2. The common thread: six ingredients

1. **Journey** (PEAK, RV): you travel a long way, the route matters, and getting through it is the achievement.
2. **Swingy fortune** (REPO, PEAK, GWYF): some runs are rich and some are dry. Big wins are possible but never guaranteed.
3. **Items that change the run** (GWYF, REPO, PEAK): items that save you, ruin you, protect you or upgrade you. Choosing and using them is a big part of the skill.
4. **Tension spikes** (REPO): moments where something can go badly wrong right now.
5. **Chaos you can laugh at** (GWYF, RV): janky physics, gambling, friends making a mess of each other's plans.
6. **Never trivial** (RV): mastery helps, but the world still throws problems at you.

Funny small details (REPO's eyes) multiply all of these. Character beats cost little and get remembered.

## 3. How FishGame can hit each one (candidate mechanics; mix, not all)

**Journey: the river (R76).**
- 7 river stages in a fixed order. Each stage draws a variant, so the map changes and the route has forks: "take the rapids (fast, dangerous) or the backwater (slow, safe, odd fish)".
- The boat ride between maps is itself a short event: a snag, a mafia boat on the horizon, a floating crate to grab.
- Each map is big enough to explore. The best spots are hidden or risky: across the rapids, under the waterfall, inside the wreck.

**Swingy fortune: loot spikes.**
- Rare "jackpot" fish, and schools that boil up briefly (a frenzy).
- Treasure from the river: crates, chests, a sunken mafia safe.
- Bad days exist too: a dry stage, a storm, a stolen cooler. The starfish god's quota keeps them tense.

**Items: save or ruin your day.**
- Lures and bait with side effects: a "lucky" lure that attracts legendaries AND sharks; dynamite (big haul, and the mafia hears it); a cursed hook (double fish value, random snaps).
- Gear: net gun, harpoon, scuba mask, decoy duck, flare (signal or distraction).
- Consumables: an energy drink (reel super fast for 20 s, then shaky hands), the starfish god's "blessed" charm (fails sometimes).
- **Buying from a vendor who is also your drinking buddy** means some items are jokes or scams.

**Chaos: gambling and mini-games.**
- **The starfish god's camp games at night (the GWYF slot):** bet fish or cash on a shell game, a fish-gut reading, a dice drinking game, or "double or nothing" on a mystery crate. A great place for the comeback-or-ruin moments.
- Crew mini-games: a fish-weighing contest, a "hold the rod while the others bail water" panic.
- Janky physics: ragdolls, getting yanked off the dock by a big fish, fish flopping out of the cooler, the boat tipping.

**Tension spikes: the mafia and the river.**
- The mafia escalates through the week: rumours, then a patrol boat (hide, go quiet), then a camp raid (defend or hide the cooler), then a final chase to the river mouth.
- Dangerous fish and creatures: an eel shock, a snapping turtle stealing your catch, a legendary that drags you in, a night creature (REPO-style "don't look at it").

**Never trivial.**
- Each stage has a twist rule: fast current (casts drift), fog (the bobber is hard to see), low water (shallow snags), flooded banks.
- Fish fight styles keep scaling, and specials need teamwork (one steers, one nets).

**Funny details (REPO's eyes).**
- Fish with eyes that track the player who caught them in the cooler and the journal.
- The starfish god's reactions to your haul.
- Mafia goons with dumb dialogue.
- The comic fish models already point this way.

## 4. Where the ~70 fish fit
- **About 7 stages x 8 regular fish = 56**, each stage with its own signature fish. The roster now has 23 + 2 specials across 2 maps.
- **About 14 "anywhere" fish** tied to conditions: storm, night, frenzy, mafia day, cursed lure, a starfish god request.
- **About 1 in 3 fish gets a gameplay quirk**, not just a look: shocks, steals bait, inflates in the cooler, jumps onto the dock, splits into two, glows and lures others, bites back.
- **The art pipeline handles 70 cheaply:** about 0.2 MB and a batch of 4-6 per Blender session. The cost is in quirks and balance, so design quirks in groups.

## 5. Questions for the design session
1. Which **2-3 ingredients are the core**, and which are seasoning? (Proposal: Journey + Items + Mafia tension as the core; gambling and physics chaos as seasoning.)
2. **Gambling:** where does it live? Only the starfish god's camp at night, or also mid-day (bets between friends on who lands the biggest)?
3. **Items:** how many slots does a player carry? Can friends steal or swap items? How often does an item "ruin" vs "save"?
4. **Mafia:** do you fight back (water guns? fish as weapons?) or only hide and run? What does getting caught look like: run over, or a costly escape?
5. **Map size:** RV-style big maps take a lot of build time. Is a smaller map with secrets enough, or do you want true exploration?
6. **Death and failure:** can a player drown, get knocked out or be kidnapped mid-run, and how do friends bring them back (PEAK-style)?
7. **The first vertical slice:** which ONE wild day do we build first to prove the formula? (Proposal: a day 3 rapids stage with a mafia patrol, two save-or-ruin items, one quirky fish, and the starfish god's night gamble.)

## 6. Fit with what exists
- Already built and kept: the fight (R72), ecology rules (M3: when and where fish bite), seasons and weather, the realistic-toon look, ladders, the host-authoritative networking.
- Already planned and aligned: R76 (river, starfish god, mafia), R54 escalation and story beats, R68 self-hook gag, M5 friend-slop (thieves, monster fish, shove), M6 boats.
- Likely spec changes after the session: R57/R76 map count and variants; items and inventory (new); gambling mini-games (new); mafia encounters (expanding R54); fish quirks (new data on `FishSpeciesDef`).
