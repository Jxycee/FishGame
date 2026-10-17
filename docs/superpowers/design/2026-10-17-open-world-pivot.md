# The Watershed: an open-world fishing game (design draft, revision 3, 2026-10-17)

_Status: **draft for the user's review**; nothing here is in the spec until approved. Revision 3 adds the user's two ideas (2026-10-17): village restoration in the style of **Dark Cloud's Georama** ("a specific set of fish for specific people along with found items + other quests to restore the village"), and a **free-build deck plot** it unlocks "so you and friends can make a cool dock/deck for landing the tug there". It also sets the tone: "goofy, but not childish; Schedule 1 is a good example" (section 2.1). Revision 2 followed the user's corrections (2026-10-17): "We are basically scraping the entire story and classic friend slop genre. It should be more of a large mostly open world fishing game. Still in the friend slop category, but more along the lines of PEAK and RV There Yet rather than quick paced friend slop games." Then: "The game revolves around fishing rather than a tough journey. Be ambitious and think outside the box." Revision 1 (challenge board, starfish god, gentle mafia) is withdrawn._

_Sources: spec sections 2, 12, 14, 15, 16 and R57-R79; `2026-10-13-fun-formula.md`; `2026-10-15-map-identity-review.md`; `Art/Blender/fg_harbour.py` (main and `harbour-art3`)._

**Kept:** co-op 1-4 with a solo path for everything; host-authoritative NGO with Relay and Lobby; the Rust-faithful fight (R72, R77); the M3 ecology, seasons, weather and clock; the 25 fish and the journal; the realistic-toon look (R74); the crew tug (R78); the stand kit and the "every stand reads at night" rule.
**Dropped:** the story (mafia, starfish god, the stolen tug, cutscenes); the quota, the days-per-run structure, Settle and run loss; scheduled pressure (thieves on a timer, daily escalation, the seagull carry); the "maps are only water" rule.

---

## 1. The idea in one paragraph

The world is one **watershed**: a river system that runs from mountain tarns, through lakes, marsh and a mill town, down to **Harbour Pier** at the river mouth and out to the open sea. Every system in the game is a fishing system. The world opens up by **what you catch and how you fish**, not by how hard the road is. The water is **alive**: fish populations, migrations, tides and seasons move on their own and react to the crew. The towns along the river have **run down because the fishing went quiet**; bringing their people home with the fish and belongings they ask for (in the style of Dark Cloud) is how a region is finished, and it unlocks the crew's own free-build berth. The crew's tug is their **moving home**, like the RV in RV There Yet: they upgrade it, live on it and anchor wherever the fishing is good. Friend-slop comes from PEAK and RV There Yet: slow, co-operative, physical and janky, with big moments the crew pulls off together.

## 2. Pillars

1. **Moment-to-moment feel.** Unchanged: cast, wait, bite, fight and land are fun on their own.
2. **Fishing is the verb for everything.** Progress, exploration, building and co-op all run through catching fish. A system that does not touch fishing does not ship.
3. **A living water.** The same place is never quite the same twice: stocks, migrations, tides, weather, seasons and the crew's own past actions change what bites.
4. **Big moments, made together.** Co-op is roles and physics, not timers: one drives, one spots, one fights, one gaffs. Huge fish have to be carried by two. Things go wrong in funny ways.
5. **Calm by default.** No deadline and no failure state. Tension comes from a fight, a storm or a legendary, and the crew chooses to go after it.

**Design rule (replaces the section 2 variety rule):** every feature must change which fish appear, when or how they bite, how they fight, or what the crew does with them.

### 2.1 Tone: goofy, not childish
- **Reference:** Schedule 1. An adult cast, dry satirical humour and a clean toon look.
- **People:** the locals are odd grown-ups with grudges, hobbies and bad ideas, not cute mascots: a harbourmaster who treats a lost pair of reading glasses as a maritime incident, twin net-menders who haven't agreed on anything since 1989.
- **Writing:** short, deadpan lines and dad humour. The joke is in the people and the situation, never in pratfall sound effects or baby talk.
- **Comedy:** physics mishaps carry it: a fish that slaps a friend off the jetty, a tug that docks "firmly", a deck someone built one plank too long.
- **Building and challenges:** follow the same rule. Requests are oddly specific ("a mackerel caught in the rain, for reasons I won't go into"). Decorations lean practical and a bit tacky (plastic flamingos, a mounted bass that sings), not pastel and cute.

---

## 3. Core systems

### 3.1 The watershed world
- **Regions, mouth to source:** Harbour Pier (river mouth, open sea), the Marsh and Creek, the Mill Town Rapids, Lake Dock, the Bayou, the Canyon, the Glowing Delta (a side branch), the Mountain Tarns. The old R76 stage list becomes geography, not a run order.
- **Mostly open:** each region is a large scene. The tug crosses between regions through a **passage** (a river lock, a gorge, a fog bank at sea) that plays as a short sail, not a menu. Once a passage is open the crew can go anywhere at any time.
- **Water connects the world:** fish migrate along the river (eels down to the sea in autumn, salmon up the creek, herring into the harbour in spring), so a region's fishing changes with what happens up- and downstream.

### 3.2 Bringing the village home (after Dark Cloud's Georama)
- **The setup:** when the fishing went quiet, the locals left. Their houses are boarded up, and their belongings ended up in the water.
- **Bringing someone home:** every villager has a **wish list** of 2-4 items, which the crew must fill to bring them back.
  - **Fish:** specific ones, usually with a condition: a species, a size, a strain or trait, a weather, a time, or a zone. For example, "a Big perch caught at dawn" or "anything golden".
  - **Found items:** their belongings, mostly **fished up** from a named zone (glasses, a teapot, the chapel bell, a shop sign), plus a few found ashore.
  - **One quest:** a small job with a fishing or physics twist, such as "get my dinghy off the sandbar before the tide does".
  - **Feedback:** each villager hints where and when to look, and the journal tracks their list.
- **Fishing up the village:** the zone junk tables gain these items.
  - Each item has conditions (zone, tide, time), and a guaranteed drop after N qualifying casts [tune ~15], so nobody grinds 500 casts for a doorknob.
  - This turns junk catches from filler into progress.
- **Placement (Georama):**
  - **Fixed landmarks:** these restore in place (market, bait shop, net shed, smokehouse, boatyard, chapel, lighthouse, lock).
  - **Placeable buildings:** cottages and small shops come back as buildings the crew places on **village lots**.
  - **Placement wishes:** each placeable villager has 1-2, for example "facing the sea", "next to the café" or "as far from the other Gary as possible".
  - **Reward:** meeting a villager's placement wishes earns a bonus gift. A host-side checker scores the wishes; the crew can move buildings any time.
  - **Co-op:** arguing over the town plan is the co-op.
- **Rewards:** a villager back home gives something to fish with: a shop, a technique, a lure, a tug upgrade, a blueprint, or a new area. The town changes visibly: scaffolding, fresh paint, lit windows, the villager on the quay commenting on your catches.
- **Completion:**
  - **Harbour home:** a region is "home" when every villager is back. The lock-keeper's list includes "everyone else is home", so the passage upstream opens last.
  - **100%:** every placement wish is also met (Dark Cloud's "future" check). That gives a mastery reward.
- **Budget:** 8-11 villagers per region, about 3 wish-list items each, 4-6 placeable buildings, and 6-10 village lots. That makes 25-35 tasks per region, which covers "lots of challenges needed to finish a map".

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
- **Directed building:** the village (3.2): restored landmarks plus placeable villager buildings on lots, with the crew choosing paint and some fixtures.
- **The crew berth (deck plot), one per region:** a free-build area at the water's edge where the crew builds its own dock and deck to land the tug. A villager unlocks it (in the harbour, the boatwright: "I'll sell you the pilings. The rest is your problem").
  - **Fixed:** a **mooring line** (bollards and a depth that always fits the tug) and an **approach lane** that building can never block, so the tug can always berth. This rule is checked by the host.
  - **Free:** decks, jetties, stairs, rails, roofs, a fish-cleaning table, a smoker, pots, a sign with the crew's name, the tacky decor.
  - **Deck height:** pieces snap to the tug's deck height near the mooring line, so the gangway always lands.
  - **A fishing spot:** a jetty pushed out reaches deeper water (with a reach limit), so the berth is a fishing tool too.
  - **The arrival shot:** it is the first thing the crew sees when the tug sails into a region.
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
| R82 | Bringing the village home completes a region (host) | Georama-style (3.2): each villager's wish list (specific fish, fished-up and found items, one quest) brings them home. Landmarks restore in place; villager buildings are placed by the crew on village lots, with placement wishes scored by the host. A region is home when all villagers are back; the lock-keeper's list opens the next passage. Deliveries, wish progress and placements are host-validated (catches come from the host fishing ledger) and saved in the world. | Quota and Settle as the goal structure; R57 |
| R83 | Living ecosystem (host) | Per-zone species stocks, a food-chain link, migrations, tides and calendar events, as a pure-C# Core model feeding `FishingResolver` weights. | Extends 12.x; R52/R54's daily modifiers become calendar events |
| R84 | Techniques and the bait ladder | Techniques (3.3) are the keys to water types; live fish are bait for bigger fish via the tug's live well. Each technique's handling builds on the R72 fight. | Extends 9.x rods and baits |
| R85 | Fish genetics (host) | Every catch carries a genome rolled on the host (3.5); strains are regional per world seed; breeding and release. Rendered with shader parameters and blendshapes per species. | Extends 12.2 `FishSpeciesDef`, the journal; R64 quality tiers stay separate |
| R86 | Legendary hunts (host) | One per region: signs, conditions, set-up, a crew fight with boat roles (extends 15.3); solo via autopilot and a rod holder. | 15.3's day-7 boss framing |
| R87 | The tug as home | R78 extended: upgrades (3.7), sleep aboard, anchoring anywhere, host-simulated driving. The ice chests persist where placed. | R78's day-end chest-loss rule; 16.2's boat as a separate vehicle (the dinghy becomes a tug upgrade) |
| R88 | Building (host) | The village (directed, R82), the **crew berth** deck plot per region (free build; a fixed mooring line and approach lane; snaps to the tug's deck height), and camp plots (free, grid-snapped). Every placement is owner-requested, host-validated, spawned by the host and saved in the world. | New |
| R89 | Persistent world | A host-owned world save (regions, stocks, builds, villagers and placements, the tug, the wallet, the clock, the world seed) and an owner-written personal profile (journal, records, cosmetics, own tackle). `schemaVersion` is read first; autosave every 5 min [tune] and on quit. | The R76/M4 run snapshot and resume; the run-related fields of meta save v2 |
| R90 | Time without a deadline | The M3 clock keeps running (a day of about 24 real min [tune]; night ~30%; a season every 3 in-game days [tune]); sleep in a bunk or bed skips to dawn when everyone present is in one. | R76 day lengths per stage |
| R91 | Co-op and jank | Section 3.9: keep physical fish, co-op monster fights, falling in and rescuing by rod; gulls and otters as ambient thieves. Drop daily escalation, the seagull carry and shove as counterplay. | Section 15's scheduled thieves (15.2 scaling), 15.6, R68 (to review) |
| R92 | Milestones re-planned | **M4 "Harbour alive":** world save, ecosystem v1, genetics v1, village v1 (the first 4 villagers, the lots and the crew berth), tug driving inside the region, the market. **M5 "The crew":** physical fish, co-op big fights, the bait ladder, techniques batch 1 (bottom, jig, troll, pots, cast net), the aquarium and breeding, camp building. **M6 "Upstream":** the river lock passage, the Marsh and Creek region, fly fishing, ice fishing, voice. **Later:** more regions, legendaries in each, linked worlds. | Section 5 rows M4-M6 and section 17's M4 DoD |

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

### 5.3 The harbour's villagers: its completion list
The modelled town already has the buildings. There are 10 villagers, with 3 wish-list items each (30 tasks), plus 7 placement wishes. Placeable buildings go on **village lots**: 4 on the level-1 upper lane (the existing cottages), 2 on the overlook terrace, 1 by the square, and the beach lot for the aquarium.

| Villager (building) | Wants: fish | Wants: item (where it is fished up or found) | Quest | Placement wish | Gives |
|---|---|---|---|---|---|
| **Doug Pellow**, harbourmaster; treats everything as a maritime incident (dock office, fixed) | 4 pier species | his reading glasses (under the T-head) | re-hang the channel buoy line | - | the full market, the market board, requests |
| **Marge Tackleby**, bait shop; retired roller-derby champion, still competitive (bait shop, fixed) | 5 minnows on live bait | the shop sign (lagoon) | dig 10 worms at low tide | - | live bait and the bait ladder, jig lures |
| **Gary and Gary Nettle**, twin net-menders who disagree on principle (net shed, fixed; two cottages, placeable) | 3 crab-pot species | the lost net (reef) | settle their argument: catch the fish each swears is bigger and weigh both | each cottage as far from the other as the lots allow | crab pots, the cast net |
| **Wendell Krill**, smokehouse; communicates mainly in fish puns (smokehouse, fixed) | 10 kg of herring during the run | his lucky smoking hat (gull nest on the sea arch) | stack driftwood from the beach | - | smoking (value x1.5); the gulls and herring come back |
| **Ines Varga**, boatwright; blunt, sees the crew's tug as a personal insult (boatyard, fixed) | a Trophy-size fish | sunken brass propeller (the wreck, low tide) | get her dinghy off Gull Bar before the tide does | - | tug upgrades, and **the crew berth** (5.3.1) |
| **Dr. Penny Hollis**, marine biologist "between universities" (aquarium, placeable) | a reef set of 4 species | her field notebook (lagoon) | a rare morph, alive | the beach lot, "somewhere children can't tap the glass" | display tanks, breeding |
| **Lorraine Bass**, café owner; dry as a cracker (café, placeable) | a fish caught in fog | her grandmother's teapot (pier) | a dawn catch of three fish, cooked on the tug's galley | facing the harbour; next to the chapel "for the funeral trade" | galley recipes (cooking buffs) |
| **Reverend Gil**, runs bingo far too seriously (chapel, fixed) | a fish caught at midnight | the chapel bell (reef wreck, needs the crane upgrade) | find the bingo balls (bottles around the harbour) | - | the calendar board (upcoming events and tides) |
| **Mr. Fenwick**, Gull Bar hermit; believes the gulls report to someone (stilt shack, stays on the bar) | a fish caught from Gull Bar | his "associate", a one-legged gull, lured back with fresh sardine | dig 5 clams on the bar at low tide | refuses town; his shack stays on the bar | treasure maps |
| **Ambrose Lock**, lock-keeper; has waited 11 years for anything to happen (river lock, fixed) | a fish over 20 kg | the lock's crank handle (creek mouth) | everyone else is home | - | **the passage upstream** (the region's finish) |

- **Placeable villager houses:** the remaining 4 lots take returning minor villagers in later passes (more wishes, same pattern). Mastery is every list filled plus every placement wish met.
- **The lighthouse keeper** joins in phase C with her own list.
- **Harbour legendary:** a giant in the open bay. Its signs are torn nets off Gull Bar and boils past the sea arch. It needs a king tide, a new moon and live mackerel, and it is fought from the tug's fighting chair. It is a mastery item, not on the lock's list.

#### 5.3.1 The crew berth
- **Where:** the south shore of the east basin, between the lookout (x 34.7-39.5) and the East Ledge jetty (x 54.4): about x 41-53, from the beach (z -47) out to about z -28.
- **Fixed parts:** the **mooring line** along its north edge, with bollards and a guaranteed depth, and an **approach lane** from the open basin to the north. Building can't block either, which the host checks.
- **The tug's spot:** the tug moves from its current spot (41, -19) to this berth once the crew builds a landing; until then it keeps its breakwater gangway.
- **Camp plots (free building, smaller):** the lagoon dock, the jetty end, Gull Bar (stilts, dry only at low tide) and the overlook garden.

### 5.4 Art: what cloud sessions can build headless in Blender
**Cloud** = a script in the style of `fg_harbour.py` (Unity axes, the FBX encoding: UV "Layer", AO in vertex colour, no materials, deterministic seeds, `export_if_changed`), run in Blender 4.5 headless. **PC** = needs Unity or the live editor.

| # | Asset | Where | Why first |
|---|---|---|---|
| 1 | **Run-down and restored variants of the town buildings** (boarded windows, tarps, broken signs, scaffolding → fresh) via a `STATE` flag in `fg_harbour.py` | Cloud | the village is the region's spine and the town already exists |
| 2 | **The 10 harbour villagers**, built on the `fg_fisher.py` base with face parts, outfits and props (Doug's lanyard, Marge's knee pads, the Garys' matching jumpers in different colours) | Cloud | the people are the content; adult proportions, Schedule 1-style |
| 3 | **Placeable villager buildings:** the cottages, café and aquarium split out of `fg_harbour.py` into standalone models with a lot footprint, plus empty-lot markers | Cloud | Georama placement |
| 4 | **Crew berth kit:** pilings, deck planks, stairs, rails, a roof, bollards, a fish-cleaning table, a name sign, and tacky decor (flamingos, singing bass, string lights) | Cloud | the free build the user asked for; also the camp kit |
| 5 | **Fished-up belongings:** glasses, teapot, chapel bell, shop sign, lost net, brass propeller, crank handle, smoking hat, notebook, bingo-ball bottles | Cloud | the wish lists |
| 6 | **Fish genome support:** per-species blendshapes (fat, long, big-fin) and a pattern-mask atlas (stripes, spots, bars, marbled, mottled, saddles) | Cloud (mesh and textures) + PC (shader) | the biggest content multiplier |
| 7 | **Stand and prop kit** (already queued) | Cloud | every stand and the tug |
| 8 | **Tug upgrade modules:** live well, sonar dome, davit/crane, fighting chair, bunks, galley, outriggers, searchlight, a towable dinghy | Cloud | the moving home; the bait ladder and the bell need it |
| 9 | **Aquarium tank set** (display tank, breeding tank, touch pool) | Cloud | collection and breeding |
| 10 | **Ecosystem tells:** bait ball, diving-bird flock, fin and boil decals, kelp and weed patches, herring and eel schools | Cloud (meshes) + PC (VFX) | players read the living water |
| 11 | **Technique gear:** jig and trolling lures, cast net, crab pot with buoy line, lantern rig, fighting rod, ice auger and hole rim, fly rod | Cloud | each technique needs its look |
| 12 | **Tide set:** low-tide flats, exposed rocks and the wreck's deck as a ring the tide reveals | Cloud (meshes) + PC (tide driver) | daily variety for free |
| 13 | **The river lock** (broken → repaired) at the creek mouth, plus the passage view upstream | Cloud | the region's finish line |
| 14 | The harbour legendary model and its signs (torn nets, boil rings) | Cloud | M5-M6 |
| 15 | Phase B land: millpond, south hills, glen path, the cave behind the falls | Cloud (mesh) + PC (colliders, rebake) | M6 |

### 5.5 Unity work (all PC)
1. **harbour-art3:** cancel the boundary move and the blockers, fix r1's two findings, run `check` and merge.
2. **World save and personal profile** (R89). Opus review (save data).
3. **Ecosystem v1** (stocks, tides, the calendar) and **genetics v1** (genome roll, shader parameters, blendshapes, the journal by strain). Core and EditMode first.
4. **Village v1:** villager and wish-list data, host counting of deliveries, the fished-up item drops with their guarantee, building-state swaps, village lots with the placement checker, and the crew berth (mooring line and approach-lane rules). Networking parts get an Opus review.
5. **Zone fish tables** for the six harbour zones.
6. **Tug driving and passengers** inside the region (16.2). Networking, Opus review.
7. **The market and selling** without a quota.

---

## 6. Open questions for the user

1. **How "open" is the world?**
   - **(a) Large regions as separate scenes, joined by a short tug passage you sail through (recommended):** it works with today's scenes and NGO, and each region can be big.
   - (b) One seamless streamed world (no transitions; a large engine and networking cost, probably a year more).
   - (c) Regions picked from a map screen (simplest, least open).
2. **How free is village placement?**
   - **(a) Landmarks fixed, villager houses placed on village lots (recommended):** keeps the modelled town, gives the Georama puzzle.
   - (b) Full Georama: every building placed anywhere in the village area (needs the town rebuilt as separate pieces and more collider work).
   - (c) Everything restores in place (no placement wishes).
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
   - **(a) "Harbour alive" (M4 in R92): the first four villagers home, the crew berth, the ecosystem, genetics and the tug driving in one region (recommended).**
   - (b) Harbour plus the second region together before anything is playtested.
