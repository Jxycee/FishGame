# Story and run loop design (2026-10-15)

_Status: draft for the user's review. Agreed with the user in the 2026-10-15 design session. Once approved, it becomes spec row R80 and replaces the quota, economy and map-choice parts of the main spec (see section 8). Builds on R79 (maps built out from the water)._

## 1. The game in one line
Get the stolen tug up the river with your friends. Every stretch of river is an extraction job (REPO, Lethal Company) laid along a journey (PEAK, RV There Yet), and fishing is how you do everything.

## 2. Story (loose, simple, Hangover-styled)
- **Last night:** the crew went drinking in the harbour town with a goofy floating starfish god. He wanted to show them his home "down on the bedrock", so they stole the nicest boat on the pier: the mafia boss's beloved tug, LUCKY BREAK. Nobody could breathe underwater, so they drank more instead and blacked out aboard.
- **Intro cutscene:** hazy flashes of the night, then the crew wakes up adrift in the bay with an empty tank. The god pops out of the bilge: "Good news: I remember where I live. Bad news: it's upstream. Worse news: we're out of fuel."
- **The run:** upriver from the harbour into ever wilder country, toward his real home at the river's source. The mafia follow.
- **Win:** at the source, the crew finally visits his home (fishbowls on their heads this time). The boss dives in after his tug without one; his goons go over the falls in an overloaded dinghy. Credits roll over photos from last night.
- **Lose:** the mafia catch the crew. The boss takes his tug back and the crew scrubs its deck forever. Slapstick, never violent.
- **Characters:** the crew are the players (no names, no lines). The starfish god is overconfident, forgetful and means well. The boss loves his tug more than people; the goons are dumb, hungover comic relief. Gatekeepers are the god's critter relatives (a pelican, a heron, an otter...). Voices are babble plus subtitles.
- **The mafia appear only in cutscenes and as scripted atmosphere** (posters, horizon boats, searchlights, a horn at dusk). No mafia AI in play.
- **Cutscenes (short, skippable):** intro; day 1 dusk discovery ("MY TUG!", played live without taking the camera); first camp; the morning tug ride (one template); day 7 arrival; win and lose endings.

## 3. Run structure
- **Seven stretches of river**, one per day, each wilder than the last. A run is about 2-3 hours over one or more evenings; the host saves at every camp.
- **Stretch 1 is Harbour Pier**, hand-made: adrift in the bay, out through the river mouth. Lake Dock stays a test map and is not in the run.
- **Stretches 2-7 are assembled each run from modular river pieces per biome** (bends, pools, rapids runs, backwaters, side caves, village banks, sandbar flats, a gate piece), picked and joined by the host's run seed, the way REPO assembles levels from rooms. Biomes get stranger toward the source.
- **Forks:** on some nights the river splits and the crew picks a branch (for example fast rapids or a slow swamp). Each branch is its own assembled stretch.
- **Stretch 7 is the source**, the god's home, and the last and biggest gate.

## 4. One stretch (one day)
1. **Morning:** the tug-ride cutscene drops the crew into the stretch with players in their chairs.
2. **Travel:** anyone can drive the tug. Calm water is short; tricky water (rapids, narrow channels) is where driving matters. Obstacles sit between fishing grounds and are solved with the rod: cast onto a tree and reel the tug off a sandbar, hook logs out of a logjam. Big catches can tow too.
3. **Fish:**
   - **Ambient (the calm):** cast anywhere, wait, and the M3 ecology roll gives a bite. Works from the drifting tug.
   - **Sighted (the money):** each stretch holds a rolled stock of visible fish (surface cues past ~10 m: fins, wakes, boils, diving birds; shadows up close or from raised spots; glows, rings and splash sounds at night). A fish notices bait that lands a few metres ahead of it, follows, then strikes. Landing the bobber on its head, or casting your shadow over it, spooks it: it dives and comes back wary about a minute later, and only leaves after repeated spooks. While a sighted fish is interested in your bait, ambient bites are off for that cast.
4. **Land it:** fish over a weight threshold need a second player with the net or gaff; the tired fish wallows at the surface for about 20 s while you shout. A basic net is always on the tug. Solo players get a rod clamp and a longer window.
5. **Haul:** catches go hand to chest. Chests can be placed anywhere and ride in the tug's hold. Dropped fish flop toward the water; gulls raid open chests; rough water and hits spill some fish (never the whole hold); a sinking tug is slow and can be bailed and patched; chests float and can be fished back out.
6. **Explore:** leaving the tug pays in its own currency: sunken crates (items that save or ruin), rare fish for the journal and the tug's trophy wall, shortcuts and hidden grottos.
7. **The gate:** the stretch's gatekeeper wants a **weight** of fish, set from a share of that stretch's sighted stock (always reachable; jackpots and rares are not counted). Bring their **bonus fish** and the weight halves; the weight alone always works. The crew carries chests onto the gatekeeper's scale (REPO's extraction point) and the gate opens when the needle passes the mark. A costly bypass always exists (for example a side channel that batters the tug).
8. **Dusk deadline:** daytime is calm. At a set late hour the mafia reach the stretch: a horn, searchlights downriver, and a short last-chance window. Still not through the gate when it closes: caught. Each stretch is its own deadline; there is no run-wide chase debt.
9. **Early finish:** clear the gate before dusk and you can make camp early for a rest buff, or keep exploring.

## 5. Camp (night)
- Past the gate, on the riverbank.
- **Cook** surplus fish for buffs for the next day (warmth, night vision, stamina...).
- **Ice:** uncooked surplus stays fresh for about a day and counts at the next gate, so the hold is the crew's buffer. Fish spoil after that, and thieves like full chests.
- **Gift pick:** the god coughs up three gifts and the crew picks one: a better rod, a bigger chest, a gadget. Rods come only from gifts, so progression is a choice, never a random lock. Gifts stack into a run "build".
- **Crates** fished up during the day are opened.
- **The tip:** the god names tomorrow's gatekeeper, their bonus fish and the main fish to look for, in rough areas only, never the rares.
- Save and quit here.

## 6. Bait and gear (no hard locks)
- Bait helps, never locks (as in M3: any bait catches any species). The right bait means faster, bolder bites and fewer spooks.
- The map provides bait: dig worms, grab crabs, use small ambient catches as live bait, and chum by throwing a cheap fish to pull a school closer.
- Rods and bait are physical and shareable: one bait bucket on the tug; hand the good rod to whoever is next to the big fish.
- No money, no shop. Fish pay the gates and feed the crew; gatekeepers make the odd one-off trade for surplus.

## 7. What varies each run
The assembled stretches and forks; season and weather (which change which fish appear where and when); where the sighted fish turn up; gatekeeper wants and bonus fish; obstacle placement; events (storm, flood, salmon run, frenzy); sunken loot; and the camp gifts, which make each run's build different.

## 8. What this replaces in the main spec (after approval)
- **Dropped:** the daily cash/weight quota and its curve (14.3-14.4), the money and Night Camp shop (14.7), the Hub map board and map unlocks (R57), the per-map climax and day-7 boss as written in 14.5, the lead meter, the 15/20/30-minute fixed day per stage. Infinite mode does not fit the story ending: drop it or rethink it later (open).
- **Changed:** R66 and R76 story (section 2 here), R78 chests (they ride in the hold and feed the gates), M5 thieves, carrying and monster fish (now part of the core loop), M6 tug driving and boat damage (pulled forward to the first prototype).
- **Kept:** the fight (R72 and later tuning), the ecology and journal, all 25 fish, the art direction (R74) and map rules (R79), host-authoritative networking.

## 9. Prototype order
1. **Tug driving feel:** buoyancy, current, grounding on a sandbar, winching with a cast line. If driving feels bad, stop and fix before anything else.
2. **One greybox stretch with a gate:** ambient fishing, a placeable chest, the scale gate and the dusk deadline.
3. **Sighted fish:** three species with surface cues, interest, strike and spook, plus the net landing.
4. Then the modular piece kit for one biome, camp and gifts, and Harbour Pier as stretch 1.

## 10. Open questions
- Exact numbers: gate share of the sighted stock, landing weight threshold, ice freshness length, dusk hour.
- How often forks appear, and how many pieces a biome kit needs before runs feel different.
- Infinite mode: drop or rethink.
- Solo details: anchor and throttle lock, scaled weights.
- Sighted fish visibility on our water shader (prototype 3 answers it).
