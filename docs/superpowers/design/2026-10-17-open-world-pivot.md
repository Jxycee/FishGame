# The Watershed: an open-world fishing game (design draft, revision 2, 2026-10-17)

_Status: **draft for the user's review**; nothing here is in the spec until approved. Revision 2 follows the user's corrections (2026-10-17): "We are basically scraping the entire story and classic friend slop genre. It should be more of a large mostly open world fishing game. Still in the friend slop category, but more along the lines of PEAK and RV There Yet rather than quick paced friend slop games." Then: "The game revolves around fishing rather than a tough journey. Be ambitious and think outside the box." Revision 1 (challenge board, starfish god, gentle mafia) is withdrawn._

_Sources: spec sections 2, 12, 14, 15, 16 and R57-R79; `2026-10-13-fun-formula.md`; `2026-10-15-map-identity-review.md`; `Art/Blender/fg_harbour.py` (main and `harbour-art3`)._

**Kept:** co-op 1-4 with a solo path for everything; host-authoritative NGO with Relay and Lobby; the Rust-faithful fight (R72, R77); the M3 ecology, seasons, weather and clock; the 25 fish and the journal; the realistic-toon look (R74); the crew tug (R78); the stand kit and the "every stand reads at night" rule.
**Dropped:** the story (mafia, starfish god, the stolen tug, cutscenes); the quota, the days-per-run structure, Settle and run loss; scheduled pressure (thieves on a timer, daily escalation, the seagull carry); the "maps are only water" rule.

---

## 1. The idea in one paragraph

The world is one **watershed**: a river system that runs from mountain tarns, through lakes, marsh and a mill town, down to **Harbour Pier** at the river mouth and out to the open sea. Every system in the game is a fishing system. The world opens up by **what you catch and how you fish**, not by how hard the road is. The water is **alive**: fish populations, migrations, tides and seasons move on their own and react to the crew. The towns along the river have **run down because the fishing went quiet**; bringing fish back to them restores them, and that restoration is how a region is finished. The crew's tug is their **moving home**, like the RV in RV There Yet: they upgrade it, live on it and anchor wherever the fishing is good. Friend-slop comes from PEAK and RV There Yet: slow, co-operative, physical and janky, with big moments the crew pulls off together.

## 2. Pillars

1. **Moment-to-moment feel.** Unchanged: cast, wait, bite, fight and land are fun on their own.
2. **Fishing is the verb for everything.** Progress, exploration, building and co-op all run through catching fish. A system that does not touch fishing does not ship.
3. **A living water.** The same place is never quite the same twice: stocks, migrations, tides, weather, seasons and the crew's own past actions change what bites.
4. **Big moments, made together.** Co-op is roles and physics, not timers: one drives, one spots, one fights, one gaffs. Huge fish have to be carried by two. Things go wrong in funny ways.
5. **Calm by default.** No deadline and no failure state. Tension comes from a fight, a storm or a legendary, and the crew chooses to go after it.

**Design rule (replaces the section 2 variety rule):** every feature must change which fish appear, when or how they bite, how they fight, or what the crew does with them.

---

## 3. Core systems

### 3.1 The watershed world
- **Regions, mouth to source:** Harbour Pier (river mouth, open sea), the Marsh and Creek, the Mill Town Rapids, Lake Dock, the Bayou, the Canyon, the Glowing Delta (a side branch), the Mountain Tarns. The old R76 stage list becomes geography, not a run order.
- **Mostly open:** each region is a large scene. The tug crosses between regions through a **passage** (a river lock, a gorge, a fog bank at sea) that plays as a short sail, not a menu. Once a passage is open the crew can go anywhere at any time.
- **Water connects the world:** fish migrate along the river (eels down to the sea in autumn, salmon up the creek, herring into the harbour in spring), so a region's fishing changes with what happens up- and downstream.

### 3.2 Restoration: how a region is finished
- Each region has a **run-down town**. Its buildings are closed, boarded up or broken because the fish went quiet.
- Each building is a **restoration project** made of **bundles**: sets of fish and finds to bring in. A bundle names a reef fish, a storm catch, a trophy-size fish, a rare morph, three crab-pot species or a sunken bell (in the style of Stardew's Community Center and Animal Crossing's museum).
- **A restored building does something:**
  - the market buys more kinds of fish;
  - the bait shop stocks a new bait;
  - the boatyard fits a tug upgrade;
  - the aquarium displays live catches;
  - the lighthouse lights the night sea.
- **The region's final project opens the passage upstream:** for the harbour, it repairs the river lock.
- **Building is visible:** the town physically changes, with scaffolding, then fresh paint, lit windows and people back on the quay.

### 3.3 Techniques and the bait ladder: the world's keys
- **Techniques open water, not doors:**
  - float fishing (start);
  - bottom fishing (deep holes);
  - jigging (reef);
  - trolling from the moving tug (open sea);
  - fly fishing (rivers and rapids);
  - cast net (bait balls);
  - crab and lobster pots (passive);
  - ice fishing (winter);
  - lantern fishing (night squid);
  - noodling (hands only, in the bayou's mud holes: pure friend-slop);
  - big-game fighting chair (legendaries).
  Each technique is a new rod or gear item with its own handling on top of the R72 fight.
- **The bait ladder:** the best bait for a big fish is a smaller fish. Worms catch minnows, minnows catch mackerel, live mackerel catch tuna, a tuna head chums for sharks. Kept alive in the tug's live well, bait becomes a planning game: what the crew catches this morning decides what it can chase tonight.

### 3.4 A living ecosystem (host)
- **Stocks:** each zone holds a coarse stock per species: a number that recovers with time and drops as fish are kept. Fishing one spot hard thins it, and released fish help it recover.
- **Food chain:** predators follow prey. A bait ball brings in mackerel, the mackerel bring in tuna, and diving birds show it.
- **Calendar events** (seasonal, a few in-game days each):
  - the herring run;
  - the salmon run up the creek;
  - the eel migration;
  - a red tide (the reef shuts and odd fish come in);
  - a king tide;
  - the spawning aggregation at the reef;
  - a whale passing through.
- **Tides:** a real tide cycle. Low tide exposes flats, pools and the wreck's deck; high tide floods the marsh and lets the tug through the sea arch.
- **The crew leaves a mark:** restoring the smokehouse brings back gulls and herring; stocking the millpond from the aquarium gives it new fish.
- **Implementation:** pure C# in Core, host-ticked a few times per in-game hour and EditMode-tested. It feeds the M3 `FishingResolver` as a weight multiplier, so the existing ecology rules stay the base.

### 3.5 Fish genetics: where "near endless" comes from (host)
- **A genome per fish:** every caught fish carries a small genome: size, body shape (fat or long, blendshapes), hue shift, pattern (a mask from an atlas), pattern colour, fin length and a rare trait (albino, golden, glowing, ghost, melanistic, giant, stubby, two-tone).
- **Rendering:** a genome is a handful of shader parameters plus blendshape weights on the existing species mesh, so a new look costs no new model.
- **Regional strains per world:** each crew's world seed rolls each region's local strains. Harbour mackerel in one world are striped; in another they are bronze. Two crews never share a journal.
- **Breeding:** with the aquarium restored, two fish of a species in a breeding tank produce offspring overnight with mixed genes, with a small mutation chance. Bred fish can be released into any water and **become catchable there**, so a crew can seed the wild with its own strains. The collection goal never runs out.
- **Journal:** tracks species, then strains, then traits. Record weights are kept per strain.

### 3.6 Legendary hunts (host)
- One legendary per region. Each is a hunt in the style of Monster Hunter, but every step is fishing:
  1. **Find the signs:** wrecked nets, a stripped bait ball, a sonar ghost, old-timers' talk.
  2. **Learn its conditions:** tide, moon, weather and bait.
  3. **Set it up:** chum line, the right live bait, the tug in position.
  4. **Fight it as a crew:** one angler in the fighting chair, one at the wheel following the fish, one on the winch, one with the gaff (spec 15.3's co-op monster fight, extended with the boat). Solo uses the tug's autopilot and a rod holder.
- **The landed legendary is physical:** too heavy for one player, it hangs on the quay's scale for the whole town to see.

### 3.7 The tug as a moving home (RV There Yet)
- **Upgrades from the boatyard:** live well, sonar, crane and davit, fighting chair, bunks (sleep anywhere), galley (cook catches for buffs), aquarium tank, smoker, bait freezer, outriggers, searchlight, a towed dinghy for shallows.
- **Anchoring anywhere is a camp:** drop anchor, hang a lantern, fish through the night.
- **Driving:** host-simulated with floppy handling (spec 16.2), so docking badly, running aground at low tide and towing a stuck friend are part of the fun.

### 3.8 Building
- **Directed building:** town restoration (3.2), where the crew chooses paint and some fixtures.
- **Free building:** the crew builds **fishing camps** on camp plots along the watershed: a shack, a jetty, drying racks, a smoker, pots, an aquarium pond, trophy walls. Structural pieces snap to a 1 m grid; decoration is freer inside a plot. A camp is a fishing tool, not just decoration: a jetty reaches deeper water, a pond holds bred stock, and a smoker multiplies value.
- **Placement:** the owner requests a placement and the host validates and spawns it (inside a plot, no overlap, paid for). Every build goes into the world save.

### 3.9 Co-op and jank (PEAK and RV There Yet, not quick-paced)
- **Physical fish** (spec 15.1): big catches must be carried, sometimes by two; dropped fish flop back to the water; and the sell scale is a physical platform.
- **A big fish can pull its angler off the tug.** Friends can hook a swimmer and reel them back in. The rod is a rescue tool.
- **Crew roles on the tug:** the wheel, the winch, the spotter on the mast with binoculars, and the net.
- **Gulls and otters:** they still steal an unattended fish, as ambient mischief, not on a timer.
- **Dropped from spec 15:** daily escalation, the seagull carry and shove as counterplay.

### 3.10 Linked worlds (later, ambitious)
- **Bottles between worlds:** a bottle thrown into the sea washes up in another crew's world, carrying a note, a lure or a fertilised egg of the sender's strain. This needs UGS Cloud Save or Cloud Code, so it comes later and is opt-in.
- **A global record board:** records per species and per strain.

---

## 4. Proposed spec rows

"Host" = host-authoritative (decides, validates, replicates); "owner" = the owning client writes it.

| # | Decision | Proposal | Supersedes |
|---|---|---|---|
| R80 | Direction (2026-10-17, user) | An open-world co-op fishing game: no story, no quota, no runs, no failure state. Pillars and design rule as in section 2 of this draft. Friend-slop in the PEAK and RV There Yet sense: slow, physical, co-operative. | Spec 2 pillars and variety rule, 2.1 loops, R54 escalation, R57 map unlock, R60 Infinite, R66 cutscenes, R76 story and run, section 14's quota, Settle and run loss |
| R81 | The watershed (3.1) | One world of regions along a river system, each a large scene joined by tug passages. Harbour Pier is the river mouth and the first region. | R76's 7-stage order and per-run variants; R79 "a map is only its water"; the Hub map board |
| R82 | Restoration completes a region (host) | Projects made of bundles restore each region's town; restored buildings add shops, upgrades and fishing features; the final project opens the next passage. Progress is counted on the host from host-validated catches and deliveries. | Quota and Settle as the goal structure; R57 |
| R83 | Living ecosystem (host) | Per-zone species stocks, a food-chain link, migrations, tides and calendar events, as a pure-C# Core model feeding `FishingResolver` weights. | Extends 12.x; R52/R54's daily modifiers become calendar events |
| R84 | Techniques and the bait ladder | Techniques (3.3) are the keys to water types; live fish are bait for bigger fish via the tug's live well. Each technique's handling builds on the R72 fight. | Extends 9.x rods and baits |
| R85 | Fish genetics (host) | Every catch carries a genome rolled on the host (3.5); strains are regional per world seed; breeding and release. Rendered with shader parameters and blendshapes per species. | Extends 12.2 `FishSpeciesDef`, the journal; R64 quality tiers stay separate |
| R86 | Legendary hunts (host) | One per region: signs, conditions, set-up, a crew fight with boat roles (extends 15.3); solo via autopilot and a rod holder. | 15.3's day-7 boss framing |
| R87 | The tug as home | R78 extended: upgrades (3.7), sleep aboard, anchoring anywhere, host-simulated driving. The ice chests persist where placed. | R78's day-end chest-loss rule; 16.2's boat as a separate vehicle (the dinghy becomes a tug upgrade) |
| R88 | Building (host) | Restoration (directed) plus camp plots (free, grid-snapped), owner-requested and host-validated, saved in the world. | New |
| R89 | Persistent world | A host-owned world save (regions, stocks, builds, restorations, the tug, the wallet, the clock, the world seed) and an owner-written personal profile (journal, records, cosmetics, own tackle). `schemaVersion` is read first; autosave every 5 min [tune] and on quit. | The R76/M4 run snapshot and resume; the run-related fields of meta save v2 |
| R90 | Time without a deadline | The M3 clock keeps running (a day of about 24 real min [tune]; night ~30%; a season every 3 in-game days [tune]); sleep in a bunk or bed skips to dawn when everyone present is in one. | R76 day lengths per stage |
| R91 | Co-op and jank | Section 3.9: keep physical fish, co-op monster fights, falling in and rescuing by rod; gulls and otters as ambient thieves. Drop daily escalation, the seagull carry and shove as counterplay. | Section 15's scheduled thieves (15.2 scaling), 15.6, R68 (to review) |
| R92 | Milestones re-planned | **M4 "Harbour alive":** world save, ecosystem v1, genetics v1, restoration v1 (3 harbour projects), tug driving inside the region, the market. **M5 "The crew":** physical fish, co-op big fights, the bait ladder, techniques batch 1 (bottom, jig, troll, pots, cast net), the aquarium and breeding, camp building. **M6 "Upstream":** the river lock passage, the Marsh and Creek region, fly fishing, ice fishing, voice. **Later:** more regions, legendaries in each, linked worlds. | Section 5 rows M4-M6 and section 17's M4 DoD |

---

## 5. Harbour Pier under this direction

### 5.1 Role and play area
- **Role:** the river mouth and the first region; home port; the open sea starts here.
- **Phase A (M4):** today's walls (x ±60.5, z -106.5..60.5). The town and terraces are walkable because they are now the restoration hub. The planned R79 south-wall move to ~z -72 and the stair-foot blockers on harbour-art3 are cancelled.
- **Phase B (M6):** the south wall goes out to ~z -145 for the windmill and a new millpond, and the west wall to ~x -100 for the creek glen and the cave behind the falls. This needs a castable-grid rebake.
- **Phase C (later):** the lighthouse on the East Head, and the open sea out to the islands (z 108-116) by tug, with a fog-and-current edge instead of a wall.

### 5.2 Fishing zones
- **Zone fish tables:** six, building on the 12.5 delta the map review already promoted: pier, lagoon and creek marsh, East Ledge, Gull Bar flats, the reef and its wreck, and the open bay and sea arch. Phase B adds the millpond and the creek.
- **Tide states:**
  - the Gull Bar is a walkable sandbar at low tide and a fishing flat at high tide;
  - the wreck's deck shows at low tide;
  - the sea arch is passable by tug only at high tide.
- **Events:** the spring herring run in the harbour basin, the autumn eel run out of the creek, and a summer bait ball off the T-head.

### 5.3 Restoration projects: the harbour's completion list
The modelled town already has the buildings, so each one becomes a project. There are 9 projects and 22 bundles (target 15-25).

| Project (building, position) | Bundles | Unlocks |
|---|---|---|
| Dock office market (10.5, -63.3) | Pier fish (4 species); a fish over 5 kg | full market, the market board, requests |
| Bait shop (-18.5, -63) | Worms and minnows (dig 10, catch 5 minnows) | live bait and the bait ladder, jig lures |
| Net shed (-30.5, -62.9) | Crab-pot catch (3 species); a lost net (junk catch) | crab pots, the cast net |
| Smokehouse (-37.5, -66.8) | 10 kg of herring during the run; driftwood | smoking (value x1.5), the gulls and herring come back |
| Boatyard (21.2, -66) | A trophy fish; sunken brass (wreck); a storm catch | tug upgrades: live well, sonar, then the fighting chair |
| Aquarium (new, on the beach plot) | Reef set (4 species); a night set (3); a rare morph | display tanks, breeding (3.5) |
| Chapel bell tower (4, -98.4) | The sunken bell (hooked from the reef wreck) | the calendar board: shows upcoming events and tides |
| Lighthouse (phase C; keeper's hut first) | Lantern-fished squid (5); a fish caught in fog | night sea fishing, a safe light in storms |
| **River lock** (creek mouth, new) | The legendary's sign; a 20 kg fish; one bundle from every other project | **the passage to the Marsh and Creek region** |

- **Harbour legendary:** a giant in the open bay. Its signs are torn nets off Gull Bar and boils past the sea arch. It needs a king tide, a new moon and live mackerel, and it is fought from the tug's fighting chair.
- **Camp plots (free building):** the beach by the aquarium, the lagoon dock, the jetty end, Gull Bar (high and dry only at low tide: build stilts) and the overlook garden.

### 5.4 Art: what cloud sessions can build headless in Blender
**Cloud** = a script in the style of `fg_harbour.py` (Unity axes, the FBX encoding: UV "Layer", AO in vertex colour, no materials, deterministic seeds, `export_if_changed`), run in Blender 4.5 headless. **PC** = needs Unity or the live editor.

| # | Asset | Where | Why first |
|---|---|---|---|
| 1 | **Run-down and restored variants of the town buildings** (boarded windows, tarps, broken signs, scaffolding → fresh) via a `STATE` flag in `fg_harbour.py` | Cloud | restoration is the region's spine and the town already exists |
| 2 | **Fish genome support:** per-species blendshapes (fat, long, big-fin) and a pattern-mask atlas (stripes, spots, bars, marbled, mottled, saddles) | Cloud (mesh and textures) + PC (shader) | the biggest content multiplier in the game |
| 3 | **Stand and prop kit** (already queued) | Cloud | every stand, camp and the tug |
| 4 | **Tug upgrade modules:** live well, sonar dome, davit/crane, fighting chair, bunks, galley, outriggers, searchlight, a towable dinghy | Cloud | the moving home; bait ladder and hunts depend on it |
| 5 | **Aquarium building and tank set** (display tank, breeding tank, touch pool) | Cloud | collection and breeding |
| 6 | **Ecosystem tells:** bait ball, diving-bird flock, fin and boil decals, kelp and weed patches, herring and eel schools | Cloud (meshes) + PC (VFX) | players read the living water |
| 7 | **Project and bundle board** plus the quay's big hanging scale | Cloud | the completion UI in the world |
| 8 | **Technique gear:** jig and trolling lures, cast net, crab pot with buoy line, lantern rig, fighting rod, ice auger and hole rim, fly rod | Cloud | each technique needs its look |
| 9 | **Tide set:** low-tide flats, exposed rocks and the wreck's deck as a ring the tide reveals | Cloud (meshes) + PC (tide driver) | daily variety for free |
| 10 | **The river lock** (broken → repaired) at the creek mouth, plus the passage view upstream | Cloud | the region's finish line |
| 11 | Camp building kit v1 (~30 structural and functional pieces) | Cloud | M5 |
| 12 | The harbour legendary model and its signs (torn nets, boil rings) | Cloud | M5-M6 |
| 13 | Phase B land: millpond, south hills, glen path, the cave behind the falls | Cloud (mesh) + PC (colliders, rebake) | M6 |

### 5.5 Unity work (all PC)
1. **harbour-art3:** cancel the boundary move and the blockers, fix r1's two findings, run `check` and merge.
2. **World save and personal profile** (R89). Opus review (save data).
3. **Ecosystem v1** (stocks, tides, the calendar) and **genetics v1** (genome roll, shader parameters, blendshapes, the journal by strain). Core and EditMode first.
4. **Restoration v1:** project and bundle data, host counting, the board and building-state swaps.
5. **Zone fish tables** for the six harbour zones.
6. **Tug driving and passengers** inside the region (16.2). Networking, Opus review.
7. **The market and selling** without a quota.

---

## 6. Open questions for the user

1. **How "open" is the world?**
   - **(a) Large regions as separate scenes, joined by a short tug passage you sail through (recommended):** it works with today's scenes and NGO, and each region can be big.
   - (b) One seamless streamed world (no transitions; a large engine and networking cost, probably a year more).
   - (c) Regions picked from a map screen (simplest, least open).
2. **Is restoring the towns the right way to "finish" a region?**
   - **(a) Yes: projects and bundles restore the town, and the last one opens the next passage (recommended).**
   - (b) A plain challenge list per region instead.
   - (c) No finish line: everything is open from the start.
3. **How far should the living ecosystem go?**
   - **(a) Stocks recover and thin, migrations, tides and events, with no permanent damage (recommended).**
   - (b) Full consequences: overfished species can vanish from a region until the crew restocks it.
   - (c) Only events and tides, with fixed stocks.
4. **Breeding and releasing your own strains:**
   - **(a) In, from M5 (recommended):** it is the strongest "near endless" system and builds on the aquarium.
   - (b) Strains and morphs as catch-only (no breeding).
   - (c) Later, after more regions exist.
5. **Whose world is it?**
   - **(a) A host-owned world plus a personal profile per player (recommended):** like Valheim or Stardew co-op.
   - (b) One shared save only.
   - (c) Each player keeps a full copy, synced when they play together.
6. **The first playable goal:**
   - **(a) "Harbour alive" (M4 in R92): restoration of three buildings, the ecosystem, genetics and the tug driving in one region (recommended).**
   - (b) Harbour plus the second region together before anything is playtested.
