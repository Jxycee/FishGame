# M4 design inputs (user answers, 2026-10-14)

Collected before the M4 design session. These are the user's decisions; the design session turns them into spec rows (R76 run structure, R78 tugboat and ice chests).

## Map scale and layout
- **Medium maps.** RV There Yet scale is too large unless expeditions run about an hour. One main area per map, roughly harbour-sized on land.
- **The water and the boat are the focus**, with detail off shore as well as on land.
- **Hidden grottos / special off-shore spots** (later, not a map feature): events that can play on any map, themed by the map, each featuring a certain kind of fish or a modifier.
- Every map reads as a stop on the downriver run (the crew drove the boat there), never "another map we fish in". Life comes from noise, clutter and detail (REPO, PEAK) plus realism and a world that continues past the walls (RV There Yet).

## The tugboat (spec R78)
- Crew boat = a tugboat after Rust's tugs: wheelhouse, walkable interior storage, fits 4, several fishing spots aboard.
- **Free driving:** any player can take the wheel and move the tug around the map at any time.
- **Seats and the morning cutscene:**
  - The boat has 4 chairs; chair 1 is the driver's seat.
  - When the day ends, record each player's last saved chair. The cutscene places the players in those chairs as the tug drives into the next area, and then flows seamlessly into gameplay.
  - A player with no saved chair is slotted into an unsaved chair.
  - A player's saved chair clears only when they leave their chair after the cutscene, so tracking restarts properly.
- **Ice chests:** one per player, placeable anywhere, store fish until sold; a chest not brought back aboard when the boat leaves at day end is lost with its contents.

## The mafia
- **Yes:** pure atmosphere (wanted posters, dark boats on the horizon, getting closer each day).
- **Yes:** night-camp events (scout boat, threatening note, close calls), but **not as cutscenes**: they play out live.
- **Maybe:** day-time pressure (a patrol interrupts fishing), but only if players get a real way to combat it beyond slowly driving away or hiding.
- **Finale:** probably no chase. Instead a cutscene of the mafia dying in a dumb way as the crew's boat rolls on down the river.

## River stages
- The user leaves the stage list to Claude, with one rule: maps get **crazier and more lively** as the run goes on.

## Second round (2026-10-14)
- **Night camp:** on each map's own shore. At dusk the crew docks the tug at that map's riverbank camp spot; the starfish god appears there and the night-camp events play live on that map.
- **Day length by stage:** 15 min for stages 1-3, 20 min for stages 4-5, 30 min for stages 6-7 (later maps are bigger and crazier).
- **Stages (combined list; each later stage has variants, picked per run):**
  1. Misty creek (calm woods)
  2. Lake (Lake Dock)
  3. Rapids mill town
  4. Swamp / bayou with stilt shacks
  5. Canyon with waterfalls and cliff caves, or a flooded ghost town
  6. Glowing mangrove delta (bioluminescent, weird fish), or volcanic hot springs
  7. Harbour / open sea finale (Harbour Pier)
  The scenery itself must also be crazy and escalate stage by stage, not only the events and fish.
- **Day-time mafia counter-play:** the user asked for research first: how other friend-slop games (REPO, PEAK, Lethal Company, Content Warning, Gamble With Your Friends, RV There Yet and similar) let players deal with threats. Research note: `docs/superpowers/research/2026-10-14-friendslop-threat-counterplay.md` (in progress).
