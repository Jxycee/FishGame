# Map identity review: Harbour Pier and Lake Dock (2026-10-15)

_Status: a proposal for the user; nothing here is decided until they say so. It re-checks the two built maps against the game's identity, because the harbour polish rounds drifted toward an exploration map. Sources: spec section 2 (pillars, variety rule), 2.1 (loops), 13.2 (beat cadence), R74, R76, R78; `docs/superpowers/design/2026-10-14-m4-design-inputs.md`; CURRENT.md section 10; `HarbourPierLayout.cs`; `LakeDockLayout.cs`; harbour-art3 bbf16d1._

## 1. The identity test: what a player does on a map

- **One map = one day per run** (R76). Day length by stage (M4 inputs): 15 min for stages 1-3 (Lake Dock is stage 2), 20 min for stages 4-5, 30 min for stages 6-7 (Harbour Pier is the stage-7 finale). The night camp plays on the same map's shore.
- **The day:** arrive on the tug, fish from the tug or a stand, move when the hotspot, a time band, the bounty fish or a beat says so (a new stimulus at least every ~3 real minutes, spec 13.2), then dock at the camp at dusk.
- **Where the eyes are:** on the bobber and the water through every wait (4-18 s per cast) and fight (20 s to ~2 min); on the stand around the player; on the view across the water. Land behind the beach is seen only while walking between stands, a few times a day.
- **Night is 30% of the clock** (20:00-02:00, about 4.5 real minutes of a 15-minute day). How a stand looks at night matters more than how the village looks at noon.

**So a map is its fishing water, its stands, the tug berth and the camp. Everything else is view.** This matches what the user already decided for M4: "the water and the boat are the focus, with detail off shore", "one main area per map", and hidden grottos are events, not map features.

## 2. What drifted in the harbour, and why

- **Facts (from `HarbourPierLayout.cs`):**
  - Polish round 1 moved the south boundary from z -75.5 to -106.5, so the play area could take in two terraces: the chapel, the overlook, cottages and an upper lane.
  - The walkable land behind the beach doubled. Land is now about a third of the walled area.
  - All 15 stand points sit at z >= -48.3. **The inland ~58 m of walkable land holds no fishing spot.**
  - Every stand is within about 60 m of the pier root in a straight line (under 10 s at sprint), so travel time was never the problem.
- **Cause:** the polish rubric scored "layout", "playerSpace" and a "world" lens on walk-through shots, and the artist was free to reshape areas. Each round therefore added walkable places: the terraced town, chapel, windmill, square, glen, waterfall and villagers.
  - Layout stayed the lowest score anyway (5.5-6.2, against a target of 8.5). A walk-through critic always wants more town, so that bar kept moving.
  - The workflow cost about 2.2M subagent tokens over 5 rounds.
- **Not wasted:** the village makes a good skyline, and lighting/dusk (7-7.6) and cohesion (7-7.5) are the strongest scores. The fix is to stop spending there, not to tear it down.

## 3. Map rules (proposed for every map)

1. **Build outward from the water.** Start from the fishing water, the stands and the tug berth; add land only to stand on, moor at, camp on or look at.
2. **Three rings of effort:**

   | Ring | What | Effort |
   |---|---|---|
   | 1. Hands | ~3 m around each stand, the tug deck, the camp | Highest: dense cheap props (REPO's way: a crate is ~100 triangles), safe readable footing, rails, a lamp for night |
   | 2. Lanes | The fishing water and its shoreline, 5-40 m out | High: water surface, foam lines, reeds, rocks, sandbars, wrecks, buoys, fish life (leaps, boils, diving birds) |
   | 3. Backdrop | Everything past the stands, inland or across the water | Silhouettes, skyline, smoke, lit windows at dusk, the river continuing, mafia hints on the horizon; cheap geometry, cards/LODs, not walkable |

3. **Every walkable place has a fishing reason** (spec section 2 variety rule). It changes which fish appear, when they bite or how they fight, or it decides where the crew goes. Otherwise it is backdrop.
4. **A map is a stop on the river** (R76, R78). It needs water in and out for the tug, a berth near the main stands, and a camp spot on the shore.
5. **The stage sets the wildness** (M4 inputs: maps get crazier and livelier as the run goes on). Early stages are plain and calm, which is also cheap; the finale is the most alive and absurd.
6. **Night counts.** Every stand is readable at night: a lamp, a visible bobber, moonlit water.
7. **Reviews judge from where players stand:**
   - Shots: each stand at eye height (looking out and looking back), the tug deck, the camp, and the arrival shot.
   - Score categories: water and fishing read, stand comfort and clarity, shoreline and life, backdrop and skyline, cohesion and look, performance.
   - Drop the walk-through, "layout" and "playerSpace" lenses.

## 4. Harbour Pier: revised plan

**Keep (on identity):**
- **Fishing structure:**
  - the pier, T-head, breakwater and ladders;
  - the 15 stands;
  - the tug as the hero prop (R78), with the chase damage (taped plank patch, bullet holes).
- **Water life:** sailboats, leaping fish with splash rings, gulls, floats.
- **harbour-art3 r1's water-side work:** the Gull Bar sandbar with its stilt shack (an off-shore destination that suits M4's free tug driving), the sea arch, the far mainland and the crag.
- **Mafia atmosphere:** the sedan searchlight, the thugs, the poster post.
- **The look:**
  - painted surfaces where they are seen from the water;
  - the lighting and dusk.

**Freeze (no more work; fix only what reads badly from a stand or the tug):**
- **Town layout:** the terraces, chapel, windmill and farm hills; the village square and lanes.
- **Town interiors and life:** the creek glen interior, the market, the villager crowds.
- **Building detail:** cottage dormers and flower boxes.

**Drop from the queue:**
- the village layout (cross-cut lane, building ring);
- wall and ground blending inside the town;
- resuming the harbour-best workflow (wf_8dc74d12-0fa) as configured. Its rubric and 8.5 bar push the wrong way, at roughly the cost of the last run.

**Promote, in order:**
1. **Per-zone fish tables** (reef, ledge, lagoon, creek marsh): the reason to walk to the West Horn or the East Ledge.
   - Today's sub-areas are shore-distance bands only (spec 12.5), so this is a spec delta.
   - Data and Core work, not Blender; it needs a short design pass first.
2. **Zones readable from the stands:** foam lines and darker water over the reef, kelp or weed patches, a visible drop-off, birds working a bait ball. This is how players decide where to go next.
3. **A reusable stand kit** (Blender): rod holder, bait bucket, crate seat, tackle box, lantern, rope coil. Cheap props, placed within ~3 m of every stand and on the tug, and reused on every later map. Every stand gets a lamp for night.
4. **Stand 14:** it is 2.4 m from the jetty edge; the rule is 3 m.
5. **Tug details seen all day:**
   - the LUCKY BREAK lettering hidden by the fender;
   - the jagged bow waterline stripe;
   - four ice-chest variants, so each player knows which chest is theirs (R78: one per player).
6. **Water:** the in-house toon water plan (CURRENT.md 6b). It is on screen almost all the time, so it is the biggest visual win per hour.
7. **Reserve now, build in M4:**
   - a camp spot on the beach near the tug (M4: night camp on each map's shore; the harbour hosts camps in Infinite mode);
   - a stretch of open sea in view of the T-head for the day-7 climax (`leviathan_tide`, spec 14.5).

**Decision for the user: the boundary.**
- **(a) Recommended:** pull the south wall back toward z -75.5. The town becomes view only, and only paths that join stands stay walkable. There is less to polish, test and render, and players read at once that the game is at the water.
- **(b)** Keep the town walkable but frozen.

**Note for the M4 design session: the finale.** As the stage-7 finale the harbour should be the craziest map, but it has been art-directed as a cozy working village. The wildness should live on the water and the horizon: the mafia boats closing in, the leviathan tide, signs of the monster. That is design-session work, not tonight's.

## 5. Lake Dock: revised plan

**State:**
- **Layout:** the M1 test bowl (shore radius ~50 m, land all round), one small dock and a ramp.
- **Stands:** 10 (dock end, dock middle on both sides, ramp top, six bank points).
- **Art:** code-built R74 meshes, with lathe/radial seasonal trees.
- **Run position:** stage 2 under R76, a 15-minute day.

**Gaps against the identity:**
1. **Not a stop on the river.** A closed bowl with no inlet or outlet and nowhere to moor the tug.
2. **Look mismatch.** Code meshes and blob trees next to the harbour's Blender pipeline and crown-card trees, and one run shows both maps. The style notes already flag cones and blobs as reading like placeholders.
3. **Zones you can't see.** The reeds / open water / deep water bands are shore distances; only the reeds show on screen.

**Plan:**
1. **Keep it small and calm** (an early stage is plain): no town. At most a boat-rental shack or a couple of cabins, with mist and a treeline.
2. **Short design session first** (CLAUDE.md: big features get one): where the river enters and leaves, the tug berth (likely beside the dock) and the camp spot.
   - This changes colliders the tests pin (the collider-count guard, the byte-identical lake colliders) and needs a grid rebake.
   - So it is a deliberate Unity task with test updates, not a look change.
3. **Then Blender, rings 1 and 2 only:**
   - Ring 1: the dock with the stand kit.
   - Ring 2, at the water: reeds and lily pads marking the reeds band; darker water and a drop-off over `deep_water`; shoreline rocks and logs.
   - Ring 2, around the shore: crown-card trees around the ring.
   - Further out: the far forest as cards or LODs.
   - The seasonal swap (R74 seasonal trees) must survive the move to crown cards.

If "the first map" means the stage-1 Misty Creek that M4 will build, these rules are its template.

## 6. Art direction: keep going, aim it

The direction is liked: realistic toon, greyscale painted layers with palette colour, the top tint, crown cards, warm light. The remaining work goes where players look:
1. **Surfaces seen from the water** (the critics' weakest category, 5.5): shoreline rock, the sand-to-water edge, pier timber, the hull. Not town walls.
2. **A recolour system:** one rock/tree/prop set recoloured per stage through the palette and tints. Seven stages with variants can't each get new meshes, and PEAK and RV There Yet do exactly this (reference style notes).
3. **The night look at the stands.**
4. **LODs and impostors** for the forest and the backdrop (performance).
5. **Characters later**, as planned.

## 7. Order of work

**Tonight (local, Blender, Sonnet, no critic loop):**
1. The stand kit (new, reusable).
2. Tug fixes plus the four ice-chest variants.
3. Harbour ring-1 dressing with the kit, and stand 14.

**Next (needs Unity or a design pass):**
4. **Unity integration** of the above.
5. **harbour-art3 r1:** keep it as the base, fix its two review findings, run `check`, merge. Alpha-to-coverage swells the crown cards on Low; the far mountains sit past the fog and the 400 m far clip yet still cast shadows.
6. **Short design pass:** per-zone fish tables, plus the Lake Dock river, berth and camp.
7. **The in-house toon water.**
8. **The Lake Dock Blender pass.**
