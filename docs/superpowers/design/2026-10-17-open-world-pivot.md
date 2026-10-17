# Open-world pivot: a big, relaxed fishing game (design draft, 2026-10-17)

_Status: **draft for the user's review.** Nothing here is in the spec until the user approves it. Written from the user's new direction (2026-10-17): "a very big fishing game with lots of different aspects to it. Exploration/Relaxing/Still a lot of the same concepts as the last game minus the rush from the mafia chasing us / Lots of challenges needed to finish a map but a map can also offer near endless possibilities due to variations in fish, building, and other things." Then: start building up the Harbour Pier map._

_Sources: spec sections 2, 2.1, 5, 14, 16 and R57-R79; `2026-10-15-map-identity-review.md`; `2026-10-14-m4-design-inputs.md`; `2026-10-13-fun-formula.md`; `Art/Blender/fg_harbour.py` (main and `harbour-art3`); `HarbourPierLayout.cs`._

**What stays (not re-litigated):** co-op 1-4 friend-slop with a solo path for everything; host-authoritative NGO with UGS Relay and Lobby; the Rust-faithful fight (R72, R77); ecology, seasons, weather and the day clock (M3); the 25 fish and the journal; the realistic-toon look (R74); the crew tug (R78); the stand kit and the ring-1 rules for stands (R79's "every stand reads at night", "ring 1 gets the most detail").

---

## 1. Pillars and the core loop

**Pillars, in priority order** (replaces spec section 2's list):
1. **Moment-to-moment feel.** Unchanged: cast, wait, bite, fight and land are fun on their own.
2. **Always something to chase.** A map always offers a next goal: a challenge, a fish morph not yet seen, a secret, a build, a record weight. No goal is ever forced.
3. **Calm by default, chaos from friends.** No clock to beat and no failure state. Pressure comes from what players opt into (a derby, a legendary, a storm at sea) and from each other (shoves, thieves, physics), never from a deadline.
4. **Friend-slop readability.** Catches, finds and builds are shared moments. Failure is funny, never punishing.
5. **Every place has a reason.** A walkable place must offer at least one of: **fish, find, build or do** (extends the section 2 variety rule and R79 rule 3). Land with none of these stays backdrop.

**The loop:**
- **Micro (20 s to ~2 min):** unchanged (spec 2.1).
- **Session (an evening, 1-3 h):** load the crew's world on the tug at the current map → check the **challenge board** and today's weather, season and requests → split up or stick together to fish, explore, build and run activities → sell, buy and upgrade at the market and the starfish god's stall → save on quit (autosave every few minutes).
- **Map (several sessions):** earn **stars** from the map's challenge board. Enough stars open the next **tug route**. All stars **master** the map (a plaque, a cosmetic, a permanent skill).
- **Endless (after mastery):** fish variants and records, rare spawns and legendaries, rotating requests, derbies, treasure maps, and building. None of it needs new maps to stay fresh.

**What replaces the quota as the pacing:** the **challenge board** (stars) for structure, the **tug routes** for progression, the **rotating requests and derbies** for short-term goals, and the **collections** (journal, variants, trophies, discoveries, builds) for the long tail.

---

## 2. Proposed spec rows

Each row names what it replaces. "Host" = host-authoritative (host decides, validates and replicates); "owner" = the owning client writes it.

| # | Decision | Proposal | Supersedes |
|---|---|---|---|
| R80 | Direction: open-world cozy fishing (2026-10-17, user) | FishGame is a large, relaxed co-op fishing game: explore, fish, collect, build and complete challenges across several big maps. No quota, no deadline, no run loss. The pillars in this draft's section 1 replace spec section 2's list; section 2.1's meso, macro and meta loops are replaced by section 1 here. | Spec 2 pillar 3's "pressure from the clock and the quota"; 2.1 meso/macro/meta loops; R76 run structure (7 days = 7 maps, quota = fuel, mafia catch-up loss); R60 Infinite mode; the M4 Settle and run-loss rules in section 14 |
| R81 | Persistent crew world | One **world save per crew**, owned and written by the **host**: map states (buildings, challenge progress, discoveries, placed pots and chests, unlocked routes), the shared crew wallet, the tug and its upgrades, the world clock (day, season). Each player also keeps a **personal profile** on their own machine (journal, records, cosmetics, skills, their own tackle loadout), written by its **owner**. A joiner plays in the host's world and carries their profile in and out. Autosave every 5 min [tune] and on quit; versioned with a `schemaVersion` read first (the backlog fix). | R76/M4 nightly run snapshot and run save/resume; R60's per-map best-infinite-day; meta save v2 fields `pendingRunRef`, `skillByMap` (folded into the profile) |
| R82 | Challenge board and stars (host) | Each map has a **challenge board** of 15-25 hand-authored challenges in four kinds: Fishing, Exploration, Building, Social (co-op or crew-wide). Each gives 1 star (a few give 2-3). **Route threshold:** about 60% of the map's stars opens that map's next tug route [tune]. **Mastery:** all stars. Progress counters live on the host, fed by host-validated events (catches from the fishing ledger, discovery triggers, build placements); a challenge completes for the whole crew. Solo-completable: "co-op" challenges have a solo variant. | Section 14 quota curve, Settle and day-7 climax as the goal structure; R57 "beat map N to unlock N+1" (unlock is now by stars, not by a run win) |
| R83 | Time without a deadline | The M3 day clock keeps running: a day is ~24 real min [tune], night stays ~30% of it, the season advances every 3 in-game days [tune]. Nothing ends at day end. Players can **sleep** in any bed (tug bunk, camp, a built shack) to skip to dawn when everyone in the session is in a bed (host decides). The R78 day-end rule (a chest not aboard is lost) is dropped: placed chests persist in the world save. | R76/M4 day lengths by stage (15/20/30 min); R78's chest-loss rule; R54 daily escalation tiers as a pressure ramp |
| R84 | Economy without a quota (host) | One **shared crew wallet** (as before). Fish sell at the market (dock office) and the starfish god's stall, with the market board and freshness kept as price variety. Cash buys rods, bait, tackle, activity gear, **building materials and blueprints**, and tug upgrades. Cosmetic currency (R59) is folded into cash for cosmetics sold in-world; nothing is bought with real money. Requests (spec 15.4 "daily requests", brought forward) pay cash and sometimes a blueprint. | Section 14's quota-driven earnings calibration; R59's separate cosmetic currency (proposed merge) |
| R85 | Maps are open regions (supersedes parts of R79) | A map is a big region built out from the water, with walkable land wherever it has a reason (pillar 5). R79's three rings stay as the **effort** rule, with POIs (points of interest) added to ring 1 and the backdrop pushed further out. Art reviews judge shots from stands, the tug, POIs and the arrival; walk-through shots are allowed for POI paths only. **Harbour Pier:** the town is no longer frozen; the planned south-wall move to the stair feet (~z -72) and the stair-foot blockers are **cancelled**; the wall stays at z -106.5 for now and moves further out in phases (section 4). | R79's "everything else is view", the frozen harbour town, the south boundary at the terrace stair feet, the stair-foot blockers; R79 rule 4 "every map is a stop on the river" (now: every map has a tug berth and a route out) |
| R86 | Story in a gentle form | The crew "borrowed" a tug from the mafia after a big night out and set up as fishers at Harbour Pier, the **home map**. The **starfish god** is their goofy friend: he runs a stall, hands out the challenge board's tougher challenges and some requests, and reacts to big catches. The mafia is **comic flavour only**: wanted posters, the tug's old owner sulking on the horizon, the sedan on the hill, a few harmless optional encounters and challenges (e.g. hook a goon's hat). No chase, no catch-up, no loss. Intro cutscene kept; the ending cutscenes become per-map mastery stingers. | R76 story (on the run, quota as fuel); R66 cutscene outcomes; M4 inputs' mafia escalation and finale |
| R87 | Fish variants (host) | Every catch rolls on the host, in order: species (M3 resolver), **size class** (Tiny, Small, Normal, Big, Trophy, from the weight percentile), **colour morph** (common + 1-3 rare morphs per species), and a rare cross-species **mutation** (section 3.1). **Legendaries:** one per zone, with strict conditions. Variant ids replicate with the catch; the journal records morphs, mutations and per-variant records. Values: morphs and mutations multiply sell price. | Extends 12.2 (`FishSpeciesDef`) and the journal; R64 quality tiers remain separate |
| R88 | Building (host) | Players build on **build plots** (pre-authored areas per map) with a **1 m grid** and 90-degree turns for structural pieces, and freer decoration (0.25 m snap, 15-degree turns) inside a plot. The tug has its own small deck plot. Placement is requested by the owner and **validated by the host** (inside a plot, no overlap, paid for), then spawned as a host-owned `NetworkObject` and written to the world save. Any crew member can edit any build (friend-slop). Budgets in 3.2. | New; no spec row replaced |
| R89 | Activities (host for outcomes) | Fishing variants and side activities added in order: crab pots, cast net, treasure (metal detector and shovel; treasure maps), ice fishing in winter, boat fishing from the tug and a dinghy, spearfishing last (it needs underwater rendering). Each activity's rolls happen on the host; the owner drives its own animation and input. Details in 3.4. | Adds to 15.4 hazards/happenings; spec 16.2 boat becomes the dinghy plus the drivable tug |
| R90 | Travel by tug | Maps link by **tug routes**. A route opens by stars (R82); once open the crew can travel any time, both ways, from the tug's wheel (host starts it; all players aboard, or left-behind players are moved aboard). The trip is a short, skippable voyage scene (the old morning boat ride). Harbour Pier is map 1; the route goes upriver (Lake Dock and the R76 stage list are reused as the next maps: misty creek, lake, rapids mill town, bayou, canyon, glowing delta). | R76 fixed 7-stage order per run and per-run variants; R57 map board in a Hub scene (the harbour is the hub) |
| R91 | Opt-in pressure | Timed play exists only by choice: **derbies** (a 10-minute crew or rival contest started from the board), legendary hunts, storms at sea. A rogue-lite "rush" mode may come back later as a separate mode, not the main game. | Pillar 3's default clock pressure |
| R92 | Milestones re-planned | M4 = **Harbour open world**: world save, challenge board, market and stall, requests, tug driving inside the map, zone fish tables, fish variants v1, building v1, harbour phase A. M5 = friend slop (unchanged list) plus activities batch 1 (crab pots, cast net, treasure). M6 = second map and the tug route, harbour phase B, dinghy, ice fishing, voice. Later: more maps, spearfishing, phase C. | Section 5 rows M4 and M6, section 17's M4 DoD |

---

## 3. Near endless possibilities: systems and budgets

The rule for all of these: **variety is data and recolours, not new meshes**, so each map adds content cheaply.

### 3.1 Fish variants
| Layer | Per species | Rarity | Cost to make |
|---|---|---|---|
| Size class | 5 (Tiny to Trophy) | from the existing weight roll | none (scale + label) |
| Colour morph | 1 common + 1-3 rare | 1/15 to 1/200 [tune] | a texture-array layer each (the skin batches pipeline) |
| Mutation | ~8 shared: Albino, Golden, Melanistic, Glowing, Spotted, Giant (x1.6 scale), Stubby, Ghost (translucent) | 1/300 to 1/2000 | a shader keyword or scale each; one Unity shader pass covers all species |
| Legendary | 1 per zone (harbour: 6) | conditions + 1/50 when met | a unique model each |

**Scale:** 25 species now, ~70 target (fun formula). 70 x ~3 morphs = ~210 journal entries plus 8 mutation stamps per species as a long-tail collection; record weights per variant never end. Fish quirks (fun formula: 1 in 3 species) stay the gameplay layer on top.

### 3.2 Building
- **Where:** build plots per map (harbour: 7, section 4.3) plus the tug deck. Plots keep building off the fishing lanes and away from paths that tests pin.
- **What, v1 budget (~45 pieces):**
  - **Structure (~15):** floor, wall, wall with window, wall with door, half wall, post, beam, roof slope, roof ridge, stairs, railing, dock plank, dock post, pontoon, ladder.
  - **Functional (~12):** storage chest, bed or hammock (sleep, R83), smoker (raises sell value, takes time), aquarium tank (displays live catches; a tank collection challenge), trophy mount (shows a record fish), rod rack (loadout swap), bait vat (breeds bait overnight), crab pot station, workbench (crafts lures from junk catches), lantern, sign, campfire.
  - **Decoration (~18):** crates, barrels, plants, flower box, bunting, flags, net hang, buoy string, fish weathervane, bench, table, rug, lamp post, etc.
- **Colour:** each piece takes 2 palette slots players can repaint (cheap; the R74 palette system).
- **Scale:** each new map adds 5-10 themed pieces (bayou stilts, canyon rope bridge). Blueprints come from challenges, requests and treasure, so building is also a reward track.
- **Limits:** a per-plot piece cap (~150 [tune]) for performance and save size.

### 3.3 Discovery
| Kind | Harbour budget | Scales by |
|---|---|---|
| Message bottles (lore, hints, some start a treasure map) | 12 | fixed per map + treasure maps rolled per world seed |
| Collectible sets (shells, sea glass, mafia playing cards) | 3 sets x 8 | one new set per map |
| Secrets (hidden cove, cave behind the falls, chapel bell at midnight, sunken safe) | 8 | hand-made, 6-10 per map |
| NPCs with request chains | 4 (harbourmaster, lighthouse keeper, Gull Bar hermit, farmer) + the starfish god | 3-5 per map |
| Sunken things (hooked from the junk table: safe, anchor, figurehead, old rod) | 10 rare junk catches | junk table per zone |

### 3.4 Activities
| Activity | How it plays | Scales by | When |
|---|---|---|---|
| Crab pots | buy and drop a pot from a stand or the tug; it fills over real minutes; haul it for crabs, lobsters and junk | species per zone | M5 |
| Cast net | throw at a visible bait ball for bait and tiny fish | bait types per map | M5 |
| Treasure | metal detector on beaches; dig spots from treasure maps; magnet-fishing lure | spots rolled per world seed | M5 |
| Ice fishing | in winter the lagoon and creek freeze; drill a hole (auger), sit, fish winter species | any map with still water | M6 |
| Boat fishing | from the drivable tug and a dinghy; trolling lures; reach the reef, sea arch and islands | open-water zones | M4 (tug), M6 (dinghy) |
| Derbies | opt-in timed contests (biggest, most, rarest) at the board | rule cards (~10) | M5 |
| Smoking and aquarium | via buildings (3.2) | per species | M4-M5 |
| Spearfishing | snorkel in the shallows | needs underwater rendering | later |

---

## 4. Harbour Pier map plan

### 4.1 What opens up, in phases
The play area today (main and harbour-art3) is x -60.5..60.5, z -106.5..60.5. Beyond it the generator already models the West Head and its waterfall (x ~-92), the East Head with the lighthouse (92, 41), the windmill and farm hills (z -121..-138), the crag (44, -150) and the offshore islands (z 108-116).

- **Phase A (M4, first):** the current walls. The town, terraces, village square, chapel and overlook become explorable POIs. Water-side work from harbour-art3 (Gull Bar, sea arch, reef) is in reach by tug. No terrain change; cancel the stair-foot blockers.
- **Phase B (M6):** move the south wall to ~z -145 to take in the windmill, a **millpond** (new freshwater zone) and the farm; move the west wall to ~x -100 to take in the creek glen, the West Head top and the cave behind the waterfall. Needs a new castable-grid area (the millpond) and a rebake.
- **Phase C (later):** the East Head and the lighthouse (walkable path, keeper NPC), and the open sea to the islands by tug, with a soft sea edge (fog and current turn the tug back) instead of a wall.

### 4.2 Zones and their reasons (fish / find / build / do)
| Zone | Fish table | Find | Build | Do |
|---|---|---|---|---|
| Pier and T-head | pier (existing bands) | bottle under the pier | - | market, stall, board |
| Beach and harbour front | surf | sea glass, metal detector | plot 1 (beach shack) | bait shop, net shed, smokehouse, boatyard |
| Lagoon and creek marsh | **lagoon** (new) | shells, tide pools | plot 2 (lagoon dock) | ice fishing (winter), cast net |
| East basin, jetty, East Ledge, lookout | **ledge** (new) | lookout spyglass reveals bottles | plot 3 (jetty end) | crab pots |
| Village terraces, square, chapel | - | bell secret, playing cards, cottages' NPC | plot 4 (overlook garden) | requests, derby board |
| Gull Bar and stilt shack | **sandbar flats** (new) | hermit's chain | plot 5 (the bar) | crab pots, treasure |
| Reef, Skerries and wreck | **reef** (new) | the wreck's safe | - | tug fishing |
| Sea arch and open bay | **open sea** (new) | arch at high tide | - | trolling, legendary |
| (B) Windmill and millpond | **millpond** (freshwater) | farmer's chain | plot 6 (pond jetty) | ice fishing |
| (B) Creek glen and falls | **creek** (freshwater) | cave behind the falls | plot 7 (glen clearing) | - |
| Tug | follows where it is | - | deck plot | sleep, storage, travel |

Six new zone fish tables (lagoon, ledge, sandbar, reef, open sea, then millpond and creek) are the spec 12.5 delta the map review already promoted; the 25 fish are redistributed and new species fill gaps.

### 4.3 Points of interest (phase A)
1. **The challenge board** at the dock office notice (4.7, -60): the first thing seen from the spawn.
2. **The starfish god's stall** on the T-head (he floats beside it): sell, odd goods, his challenges.
3. **The market** in the dock office (10.5, -63.3): sell, market board, requests.
4. **The bait shop** (-18.5, -63): rods, bait, tackle, crab pots.
5. **The boatyard** (21.2, -66): tug upgrades, later the dinghy.
6. **The smokehouse** (-37.5, -66.8): smoking service until players build their own smoker.
7. **The village square** (-3, -88.6) and the harbour board: requests, derbies.
8. **The chapel bell tower** (4, -98.4): a midnight secret.
9. **The overlook**: spyglass to spot bait balls, bottles and the legendary's sign.
10. **Gull Bar's stilt shack** (37.4, 24.6): the hermit.
11. **The reef wreck** (-35.6, 9.2): sunken safe and treasure.
12. **The sea arch** (-8, 46): sail through at high tide.

### 4.4 Harbour challenge board (22 challenges, 24 stars; the route opens at 15)
| # | Kind | Challenge | Stars |
|---|---|---|---|
| 1 | Fishing | Catch 10 different species | 1 |
| 2 | Fishing | Catch a fish in every harbour zone | 1 |
| 3 | Fishing | Land a Trophy-size fish | 1 |
| 4 | Fishing | Catch a rare colour morph | 1 |
| 5 | Fishing | Catch a fish at night, in rain, and in a storm | 1 |
| 6 | Fishing | Fill the journal page for the reef | 1 |
| 7 | Fishing | Catch the harbour legendary (open sea zone) | 3 |
| 8 | Fishing | Hook something from the wreck's safe | 1 |
| 9 | Exploration | Find 6 message bottles | 1 |
| 10 | Exploration | Complete the shell collection | 1 |
| 11 | Exploration | Ring the chapel bell at midnight | 1 |
| 12 | Exploration | Sail the tug through the sea arch | 1 |
| 13 | Exploration | Dig up a treasure from a treasure map | 1 |
| 14 | Exploration | Visit all 12 POIs | 1 |
| 15 | Building | Build a shack with a roof and a bed on any plot | 1 |
| 16 | Building | Build and run a smoker | 1 |
| 17 | Building | Stock an aquarium tank with 5 species | 1 |
| 18 | Building | Mount a trophy fish | 1 |
| 19 | Social | Finish 5 requests for harbour NPCs | 1 |
| 20 | Social | Help the hermit (his 3-step chain) | 1 |
| 21 | Social | Win a derby (solo: beat the starfish god's score) | 1 |
| 22 | Social | Land a fish while a crewmate holds you by the belt (co-op; solo: land a fish while a seagull steals your hat) | 1 |

### 4.5 Art needed (prioritised)
Every item marked **Cloud** can be built headless in Blender 4.5 in a cloud container with an `fg_harbour.py`-style script (Unity axes, the FBX encoding: UV "Layer", AO in vertex colour, no materials; deterministic seeds; `export_if_changed`). **PC** needs Unity or a live editor.

| # | Asset | Where | Notes |
|---|---|---|---|
| 1 | **Stand and prop kit** (rod holder, bait bucket, crate seat, tackle box, lantern, rope coil) | Cloud | already queued; doubles as build decorations |
| 2 | **Building kit v1, structure** (~15 pieces on a 1 m grid, painted timber) | Cloud | new script `fg_buildkit.py`; pieces share one atlas and palette slots |
| 3 | **Challenge board and the starfish god's stall** | Cloud | the board must read from the spawn at 15 m |
| 4 | **Starfish god character** (floating, expressive, few bones) | Cloud | rig in Blender; face parts like `fg_fisher.py` |
| 5 | **Functional build pieces** (smoker, aquarium tank, trophy mount, chest, bed/hammock, rod rack, workbench, bait vat) | Cloud | aquarium glass and water need a Unity material later |
| 6 | **Collectibles** (message bottle, shells x8, sea glass, playing cards, treasure chest, mafia safe, dig mound) | Cloud | small; glint is a Unity effect |
| 7 | **Fish morph skins** for the 25 species (1-2 rare morphs each) | Cloud | the existing skin batch pipeline into `T_FishSkinArray` |
| 8 | **Activity gear** (crab pot buoy line, cast net, metal detector, shovel, ice auger, ice hole rim) | Cloud | crab pot model exists (LobsterPot); add a buoy and rope |
| 9 | **Build plot markers** (survey stakes, rope, a small sign) for 5 phase-A plots | Cloud | placed by the manifest |
| 10 | **Decoration kit** (~18 pieces: bunting, flags, plants, flower box, net hang, buoy string, weathervane, bench, table) | Cloud | reuse prop shapes, recolour |
| 11 | NPC set from the fisher base (harbourmaster, keeper, hermit, farmer) | Cloud | after `character-fisher` merges |
| 12 | Phase B land: millpond, the south hills walkable, glen path, cave behind the falls | Cloud (mesh) + PC (colliders, bake) | changes the generator's dune and cliff code |
| 13 | Winter ice sheets for the lagoon and creek | Cloud | swapped by season in Unity |
| 14 | Lighthouse approach and interior (phase C) | Cloud | later |
| 15 | Mutation look (shader keywords: albino, golden, glow, ghost) | PC | one shader pass |

### 4.6 Unity work (all PC)
1. **Cancel** the R79 boundary move and stair-foot blockers on harbour-art3; still fix r1's two review findings; merge it.
2. **World save** (R81) and **personal profile**: Opus review (save data).
3. **Challenge board** (R82): data (`ChallengeDef`), host counters, UI, the star threshold, a route stub.
4. **Market, stall and requests** (R84): sell anywhere at the two vendors, a shop UI, requests.
5. **Zone fish tables** (4.2) and **fish variants** (R87): Core and data, EditMode-tested.
6. **Tug driving inside the map** (R78 free driving) and its passengers: networking, Opus review.
7. **Building v1** (R88): plots, the grid, a placement ghost, host validation, save. Networking: Opus review.
8. **Discovery**: POI triggers, bottles and collectibles, the bell secret.
9. Place the kits and the plots from the manifest; review shots from stands, the tug, POIs and the arrival.

---

## 5. Open questions for the user

1. **Whose progress is it?**
   - **(a) Host-owned world save, plus a personal profile per player (recommended):** like Valheim or Stardew co-op. The host's world holds builds and the challenge board; each player keeps their journal, records and cosmetics everywhere.
   - (b) Every player has their own copy of everything, synced when they play together (more complex; merges are messy).
   - (c) One shared save only, no personal profiles.
2. **How free is building?**
   - **(a) Grid building on build plots, freer decorations inside them (recommended):** keeps the fishing lanes clear and the save small.
   - (b) Freeform anywhere on land and docks.
   - (c) No free building: restore and upgrade set buildings only (simplest).
3. **How hard is the map gate?**
   - **(a) About 60% of the stars opens the next route; the rest is optional mastery (recommended).**
   - (b) Every challenge is needed to move on.
   - (c) All maps open from the start; challenges are only for rewards.
4. **What happens to the mafia?**
   - **(a) Comic flavour only: posters, the tug's old owner on the horizon, harmless optional encounters (recommended).**
   - (b) Drop them entirely.
   - (c) Keep an opt-in "heat" event (a mafia boat visit you can trigger for rewards).
5. **Is any timed, rogue-lite mode still wanted?**
   - **(a) Only short opt-in derbies now; maybe a separate "rush week" mode much later (recommended).**
   - (b) No timed play at all.
   - (c) Keep the 7-day run as a second main mode alongside the open world.
6. **How big is the first harbour release?**
   - **(a) Phase A only (today's walls; town, terraces and the water by tug), then the second map before phase B (recommended).**
   - (b) Phase A and B together (windmill, millpond and falls) before anything else.
