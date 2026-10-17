# harbour-village-buildings: placeable village buildings, aquarium tank set, village lots, lot marker

Branch `harbour-village-buildings`, cut from `origin/harbour-art3` (area name VillageBuildings). Blender 5.2.2 on Linux, no Unity. Generator: `Art/Blender/fg_village_buildings.py` (imports `Art/Blender/fg_harbour.py` as a module: the import has no side effects, so nothing in `fg_harbour.py` was touched). Run it with `FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_village_buildings.py` (env switches: `FG_VB_EXPORT=0`, `FG_VB_PREVIEW=0`, `FG_VB_VERIFY=0`). A full run takes about 4 minutes (25 s of it is the harbour build for the lot checks, the rest is four software-rendered previews).

## What was built

Every model is a LOCAL model in the harbour's encoding (UV0 metres, UV1 = palette slot + texture layer, vertex colour = 0.5 x baked AO, smooth normals, no materials, deterministic streams `village.<job>`). Buildings: origin = centre of the footprint at ground level, yaw 0, front and door on +z, a worn-earth **ground pad** inside the mesh (top y +0.03, buried to y -0.45). All on `M_Toon_Base` (no triplanar objects), except `*_Glass` (a glass material) and `*_Water` (a water material).

| Model | File | Tris | Extent x by z (m) | Fits lot class | Notes |
|---|---|---|---|---|---|
| `Village_Cottage_A` | `Village_Cottage_A.fbx` | 5254 | 7.7 x 5.8 | SML | Fisher's Row: plastered, red roof, green trim, dormer, porch hood, log lean-to. Sign "MIND THE GULL". |
| `Village_Cottage_A_Rundown` | `Village_Cottage_A_Rundown.fbx` | 3240 | 7.9 x 5.8 | SML | Run-down: boarded openings, tarp, weeds, a fallen part.  |
| `Village_Cottage_B` | `Village_Cottage_B.fbx` | 4080 | 7.1 x 6.0 | SML | Boat-loft cottage: weatherboard, slate roof, front gable with hoist beam and buoy, porch. Sign "FISH ALREADY.". |
| `Village_Cottage_B_Rundown` | `Village_Cottage_B_Rundown.fbx` | 2124 | 6.8 x 6.0 | SML | Run-down: boarded openings, tarp, weeds, a fallen part.  |
| `Village_Cottage_C` | `Village_Cottage_C.fbx` | 2612 | 7.4 x 5.6 | SML | Stone cottage: rubble walls, mossy roof, porthole, gable chimney, bench. Sign "KNOCK. THEN LEAVE.". |
| `Village_Cottage_C_Rundown` | `Village_Cottage_C_Rundown.fbx` | 1652 | 7.4 x 5.7 | SML | Run-down: boarded openings, tarp, weeds, a fallen part.  |
| `Village_Cottage_D` | `Village_Cottage_D.fbx` | 3260 | 6.4 x 6.0 | SML | Narrow two-storey: balcony, bay window, hoist arm, flag. Sign "NOT A PUB". |
| `Village_Cottage_D_Rundown` | `Village_Cottage_D_Rundown.fbx` | 2670 | 6.4 x 6.0 | SML | Run-down: boarded openings, tarp, weeds, a fallen part.  |
| `Village_Gary_Tidy` | `Village_Gary_Tidy.fbx` | 4662 | 7.5 x 6.0 | SML | Gary (tidy): orange trim, red door and shutters; matching buoys, folded nets, square crates, two jumpers on a line. Plaque "GARY. OLDER.". |
| `Village_Gary_Chaos` | `Village_Gary_Chaos.fbx` | 5424 | 7.5 x 6.0 | SML | Gary (chaos): red trim, orange door and shutters; a dinghy tied on the roof, nets over everything, buoys and crates everywhere, jumpers snagged. Plaque "GARY. CORRECT.". |
| `Village_Gary_Rundown` | `Village_Gary_Rundown.fbx` | 2404 | 7.5 x 6.0 | SML | One run-down model for both Garys (identical plan, paint gone). |
| `Village_Cafe` | `Village_Cafe.fbx` | 7744 | 9.4 x 6.3 | ML | Lorraine's cafe: striped awning, three tables (one with a parasol), chalkboard "SOUP OF THE DAY: / YESTERDAY'S / (SMALL OR NONE)", sign LORRAINE'S, "OPEN-ISH", "NO REFUNDS ON GRAVY". |
| `Village_Cafe_Rundown` | `Village_Cafe_Rundown.fbx` | 4242 | 9.4 x 6.3 | ML | Run-down: boarded openings, tarp, weeds, a fallen part. Awning half collapsed, tables stacked, chalkboard "BACK IN 5 MIN.". |
| `Village_Aquarium` | `Village_Aquarium.fbx` | 7373 | 10.8 x 7.8 | L | Dr. Hollis's aquarium: glass frontage (glass is a separate object), hand-painted arched sign "DR. HOLLIS'S AQUARIUM (INSTITUTE PENDING)", orange fish mascot, open doorway, interior with three tank platforms and a breeding platform. |
| `Village_Aquarium_Rundown` | `Village_Aquarium_Rundown.fbx` | 3786 | 10.8 x 7.8 | L | Run-down: boarded openings, tarp, weeds, a fallen part. Frontage boarded (no glass object), sign faded, mascot fallen, "CLOSED (FISH ESCAPED)". |

Footprint = the whole mesh at yaw 0 (roofs, awnings, props, pad included); `fits lot class` is checked at yaw and yaw + 180 against the lot sizes below. Pad sizes and door offsets are in the manifest (`buildings[].footprint`).

**Garys:** one shared plan (`gary_plan()`: walls, roof, chimney, door, windows, the lean-to net store are the same code), their jumpers are brick red (palette slot 9, `Roof`) and orange (slot 10, `Accent`); the two houses wear the same two colours swapped (trim vs door and shutters).

**Run-down states:** done for every building (a flag in the same builders: boards, tarp, weeds, a dropped part): cost 1.6k-4.2k tris each. Gary has one shared run-down model, the aquarium run-down has no glass object.

### Aquarium tank set (separate props)

| Prop | Files (same origin: base centre on the floor, front +z) | Tris | Footprint | Fish mounts |
|---|---|---|---|---|
| `Village_DisplayTank` | body, `_Glass`, `_Water` | 1406 | 2.7 x 1.0 m, 2.08 m tall | 8 (`fish_mounts` in the manifest, tank-local Unity metres, yaw, max length; `swim_volume`) |
| `Village_BreedingTank` | body, `_Glass` (incl. divider), `_Water` (two boxes) | 2042 | 1.9 x 0.9 m (+0.4 m pump), 1.72 m tall | 4 (`fish_mounts` in the manifest, tank-local Unity metres, yaw, max length; `swim_volume`) |
| `Village_TouchPool` | body, `_Water` | 1786 | 3.0 x 1.8 m basin, rim 0.62 m, water at y 0.5 | 6 (`fish_mounts` in the manifest, tank-local Unity metres, yaw, max length; `swim_volume`) |

Mount points for the tanks in the aquarium (building frame, manifest `Village_Aquarium.tank_mounts`): three display tanks on the back platforms (-3.6 / 0 / 3.6, y 0.43, z -3.1), the breeding tank at (4.4, 0.43, -1.0) yaw 270, the touch pool in the forecourt at (3.2, 0.03, 2.55). `aquarium_with_tanks.png` shows them in place.

### Lot marker

`Village_LotMarker_S/M/L` (one per lot class, origin = lot centre at ground height, front +z): four corner stakes (plus mid stakes on sides over 7 m) with hi-vis ribbons and flags, a sagging rope, and the sign "LOT AVAILABLE. / ENQUIRE WITHIN. / DON'T.". Stakes go 0.3 m into the ground, so lots must be flat within ~0.1 m (all are). The manifest lists each marker's placements (lot, position, yaw).

## Village lots (design 3.2, 5.3)

Lot classes (width x by depth z, in the lot frame): **S 9.0 x 6.0**, **M 9.6 x 6.4**, **L 14.0 x 8.0** m. A lot accepts any building whose class is not larger; the building origin goes on the lot centre at ground height. Each lot allows its yaw and yaw + 180 (the level-1 lots are only 6 m deep between the lane and wall 1, so no 90 degree turns). Yaw is Unity yaw (0 = front toward +z, the sea).

| Lot | Centre x, y, z | Yaw | Class | Ground y (min-max) | Replaces / removes | Min gap to a harbour solid | Path edge | Lane edge |
|---|---|---|---|---|---|---|---|---|
| **Lot1** Upper lane west | -54.0, 3.74, -82.0 | 180 | S | 3.74-3.74 | `UPPER_HOUSES[0]` (cottage -54.0, -82.0) | 0.36 m (LaundryPole_0) | 1.65 m | 1.41 m |
| **Lot2** Upper lane west-centre | -17.5, 3.74, -79.7 | 180 | S | 3.74-3.74 | `UPPER_HOUSES[2]` (cottage -17.5, -80.4) | 0.78 m (Parapet1) | 10.74 m | 0.34 m |
| **Lot3** Upper lane east-centre | 23.8, 3.74, -79.5 | 180 | S | 3.74-3.74 | `UPPER_HOUSES[7]` (cottage 23.8, -80.4) | 0.51 m (Stall_nets) | 3.85 m | 0.28 m |
| **Lot4** Upper lane east | 47.4, 3.74, -81.8 | 180 | S | 3.74-3.74 | `UPPER_HOUSES[9]` (cottage 46.6, -81.8) | 0.56 m (Cottage_8) | 9.05 m | 0.66 m |
| **Lot5** Overlook west | -4.2, 6.74, -101.2 | 0 | M | 6.74-6.74 | `Tree2_14` | 0.27 m (ChapelTower) | 0.19 m | 6.85 m |
| **Lot6** Overlook east | 15.5, 6.74, -101.2 | 0 | M | 6.74-6.74 | `Tree2_15` | 1.17 m (OverlookBench_E) | 0.48 m | 8.97 m |
| **Lot7** Square lot | 12.8, 3.74, -79.3 | 180 | S | 3.74-3.74 |  | 0.38 m (Parapet1) | 3.05 m | 0.30 m |
| **Lot8** Beach lot | 47.0, 1.20, -59.0 | 0 | L | 1.20-1.26 |  | 0.50 m (LookoutStep) | 9.05 m | n/a |

Checks run by the generator on the real harbour layout (`build_pass2()` + phase 1, every solid item minus the cottages and trees the lots replace): no lot overlaps any solid, clears every stand point circle (STAND_R 3 m) and cast lane by 1 m, every stair corridor by 0.5 m, every path edge, the pier/T-head/breakwater, the code trees (nearest 3.7 m, Lot8), and the other lots; the ground is flat (Lot8 0.06 m, the rest 0.0). "Lane edge" is the distance to the lane's dirt edge (road_dist - 2.35): the lane-side lots were moved up to 1.1 m off the old cottage positions so none dips into it.

### Which four cottages the lots replace: UPPER_HOUSES[0], [2], [7], [9]

All four are on the north row (the lane's sea side), spread over the whole lane (x -54, -17.5, 23.8, 46.6): the Garys can sit about 100 m apart (Lot1 and Lot4) as their wish asks, and the remaining six cottages keep their two clusters. These four are in no washing-line pair (3,4) / (5,6) and have no vegetable plot (1,3,6,8), so nothing else dangles. The lots take the cottages' places: Lot1 is the old cottage 0 spot, Lot2/Lot3 are 0.7 / 0.9 m toward wall 1 from cottages 2 and 7 (off the lane edge), Lot7 is 1.1 m toward wall 1 from the row, Lot4 is 0.8 m east of cottage 9 (clear of cottage 8's garden fence).

Other lots: **Lot5/Lot6** on the overlook terrace (y 6.74), west and east of the chapel: both face the harbour, Lot5 is next to the chapel (Lorraine's "next to the chapel"), and the café (class M) fits them. **Lot7** is the "by the square" lot: the nearest free ground is east of stair A's head (the square, its tree and the market fill the west side), 11 m from the cobbles. **Lot8** is the beach lot: the only flat, keep-out-free ground on the plateau (y 1.2) big enough, behind the east beach and the crew berth, off every path and lane (the aquarium's "somewhere children can't tap the glass").

### What the harbour builder (owner of `fg_harbour.py`) must drop for these lots

Use a skip set, not list surgery, so every remaining cottage stays byte-identical (the dressing loops key on indices):
- cottages `UPPER_HOUSES[0, 2, 7, 9]`: skip the `cottage()` call (and its chimney smoke point), the `GardenFence_*` / `GardenFenceSide_*` for 0, 2, 9 (7 has none), and in `build_terrace_dressing` the flower-box loop, the yard clutter (`YardCrates_0`, `YardCrates_9`, `YardPots_7`, the log pile for 2) and `CottageBench_0/7/9`;
- the two overlook shade trees `TREES2[14]`, `TREES2[15]` (`Tree2_14`, `Tree2_15` in `Harbour_Core_TerraceTrees_Tri`);
- every detail item inside a lot's `rect_world` (tufts, flowers, ground cover, the 0.8 m hedge row at z -104.8 touches Lot5/Lot6's south edge, the 0.55 m hedge strips behind wall 1 touch Lot2/Lot3/Lot7's north edge).
The manifest `lots[].clearances.harbour_items_to_remove` lists the exact item names.

## Files

- `Art/Blender/fg_village_buildings.py`
- `Art/Export/VillageBuildings/*.fbx` (27 files, 73239 triangles in all; the biggest model is 7744) and `villagebuildings_manifest.json` (buildings, props, lot markers, lots with clearances, per-model items = colliders, smoke points, fish mounts, tank mounts)
- `Art/Previews/VillageBuildings/`: `lineup_cottages.png` (four cottages, built in front and run-down behind; east is to the left), `lineup_garys_cafe_aquarium.png` (Gary tidy, Gary chaos, cafe, aquarium; run-down behind), `aquarium_with_tanks.png` (the tanks on their mounts, glass and water shown, the touch pool in the forecourt), `lots_above_square.png` (the eight lots numbered, the six remaining cottages, the square, the wall stairs; camera above the square looking south, east on the left).

## Sources and licences

Nothing external: all geometry is procedural, no textures were made or downloaded (so no `Art/Textures/VillageBuildings` and no SOURCES.md), existing texture layers only (Plaster, StoneWall, Wood, RoofTile, Dirt, Cloth ... from `fg_envtextures.py`). Where the user's Unity Asset Store packs would fit better (not reachable from the cloud): the cafe's tables, chairs and parasol, the aquarium's interior props (specimen jars, counter, fish skeleton) and the fish tank decor could be swapped for a pack's models; the meshes here are placeholders in the project's style.

## Unverified

- Nothing was imported into Unity: FBX axes, scale and the `*_Glass` / `*_Water` material look are untested (the encoding is the harbour's, so the same import settings apply).
- Previews are Eevee on software GL (Blender's toon preview look, not the Unity shader); sky and grass are Blender stand-ins.
- AO is baked per model against itself only (the pad is in the mesh, no terrain occluder): expect slightly lighter plinths than the harbour's buildings.
- Windows are dark panes, not emissive: "lit windows" for returned villagers needs the Unity builder to swap them or add lights (not modelled).
- Colliders: `Body` boxes per building (an AABB of walls + roof), table / chalkboard / planter boxes for the cafe, the aquarium's hall as separate wall boxes with a 1.3 m doorway (`WallBack`, `WallWest`, `WallEast`, `FrontWest`, `FrontEast`, `RoofAndFalseFront`); nothing else is walkable.

## Unity steps (PC)

1. Import `Art/Export/VillageBuildings/*.fbx` with the harbour's settings. Material `M_Toon_Base` on every model, a glass material on `*_Glass`, a water material on `*_Water`. Slots / layers are the harbour's (slots 0-10 + S2 11-15, layers 1-14).
2. Make a prefab per building (pair each with its `_Rundown`; a tank = body + `_Glass` + `_Water` at the same transform; the aquarium = `Village_Aquarium` + `Village_Aquarium_Glass`). Add colliders from the manifest `items`, chimney smoke from `smoke_points`, fish parents from `fish_mounts` and `tank_mounts`.
3. Read `lots[]` from the manifest into a lot table (centre, yaw, class, rect_world, allowed yaws), place `Village_LotMarker_<class>` at each lot (the marker's `placements`), swap it for the run-down building when a villager's list is done and for the built one when placed. A lot is free ground (no collider needed): the host-side wish checker uses centre, yaw and the neighbours.
4. In the harbour build apply the removals above (cottages 0, 2, 7, 9, the two overlook trees, detail inside the lot rects).
5. Run `Tools/unity.ps1 check`; no C# or scenes were changed here, so no existing test should change. A new EditMode test could assert the lot rects against `HarbourPierLayout` stand points (the generator already checks this in Python).

## Open points for the orchestrator

- Lot7 is 11 m from the square's edge, not on it: nothing nearer is free without moving the market or the shade tree. Say if you would rather give up a market stall.
- The level-1 lots are 6.0 m deep (lane to wall 1): buildings there cannot be turned 90 degrees; every cottage, both Garys and the cafe's class-M lot respect this (see `fits`).
- The beach lot is 14 x 8 m because the plateau is only flat for z -55..-63 there; a deeper lot needs the dunes flattened (add a `YARDS` rect in `fg_harbour.py`).
- Gary run-down is one model for both; the chaos house's roof boat and nets add 700 tris over the tidy one.
