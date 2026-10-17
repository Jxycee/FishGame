# harbour-berth-kit: the crew berth kit, stand kit, tacky decor and the berth site (2026-10-17)

Branch `harbour-berth-kit`, cut from `origin/harbour-art3`. Blender only; no C#, scenes, prefabs or meta files. `Art/Blender/fg_harbour.py` is untouched: `Art/Blender/fg_berthkit.py` imports it for the encoding helpers (it has no import side effects) and adds the kit.

## What was built
53 FBX models in `Art/Export/BerthKit/` plus `berthkit_manifest.json` (every model: file, tris, bounds, slots, pivot, footprint, notes; light points, emissive companions, the text surface and the whole site block). Total 21511 triangles. Material for every model: `M_Toon_Base` (no `_Tri` models). Emissive objects (`*_Glow`, `Decor_TikiTorch_Flame`) are own objects at the same pivot as their body: give them an emissive variant of the toon material.


**Stand kit**

| model | tris | footprint (cells) | notes |
|---|---|---|---|
| Stand_RodHolder | 328 | - |  |
| Stand_BaitBucket | 316 | - |  |
| Stand_CrateSeat | 200 | - |  |
| Stand_TackleBox | 192 | - |  |
| Stand_Lantern | 410 | - | +Stand_Lantern_Glow, 1 light pt |
| Stand_Lantern_Glow | 80 | - | emissive |
| Stand_RopeCoil | 590 | - |  |

**Berth kit (datum = walking surface)**

| model | tris | footprint (cells) | notes |
|---|---|---|---|
| Berth_Piling_2m | 208 | 1x1 |  |
| Berth_Piling_3m | 208 | 1x1 |  |
| Berth_Piling_4m | 208 | 1x1 |  |
| Berth_Piling_5m | 224 | 1x1 |  |
| Berth_Piling_6m | 224 | 1x1 |  |
| Berth_Piling_7m | 240 | 1x1 |  |
| Berth_DeckTile_1x1 | 128 | 1x1 |  |
| Berth_DeckTile_1x2 | 140 | 1x2 |  |
| Berth_DeckTrim_1m | 88 | 1x1 |  |
| Berth_Stair_1m | 172 | 1x2 |  |
| Berth_Ramp_1m | 388 | 1x4 |  |
| Berth_Rail_1m | 180 | 1x1 |  |
| Berth_RailPost | 44 | 1x1 |  |
| Berth_RoofPost | 144 | 1x1 |  |
| Berth_RoofPanel_Flat | 92 | 1x1 |  |
| Berth_RoofPanel_Pitched | 68 | 1x1 |  |
| Berth_RoofRidge_1m | 20 | 1x1 |  |
| Berth_Bench_2m | 284 | 2x1 |  |
| Berth_CleaningTable | 444 | 2x1 |  |
| Berth_Smoker | 608 | 1x1 |  |
| Berth_CrabPotRack | 534 | 1x1 |  |
| Berth_RodRack | 545 | 1x1 |  |
| Berth_NameSign | 384 | 2x1 |  |
| Berth_StringLights_2m | 220 | 2x1 | +Berth_StringLights_2m_Glow, 2 light pt |
| Berth_StringLights_2m_Glow | 240 | 1x1 | emissive |
| Berth_Bollard | 160 | 1x1 |  |
| Berth_Cleat | 80 | 1x1 |  |
| Berth_TyreFender | 236 | 1x1 |  |
| Berth_Pontoon_2x2 | 396 | 2x2 |  |
| Berth_Wall_1m | 204 | 1x1 |  |

**Tacky decor**

| model | tris | footprint (cells) | notes |
|---|---|---|---|
| Decor_Flamingo | 544 | - |  |
| Decor_BassPlaque | 636 | - |  |
| Decor_InflatableShark | 960 | - |  |
| Decor_TikiTorch | 384 | - | +Decor_TikiTorch_Flame, 1 light pt |
| Decor_TikiTorch_Flame | 70 | - | emissive |
| Decor_BeerCooler | 354 | - |  |
| Decor_PatioChair | 416 | - |  |
| Decor_PatioChairStack | 1248 | - |  |
| Decor_Hammock | 412 | - |  |
| Decor_NoFishingSign | 672 | - |  |
| Decor_NeonBait | 212 | - | +Decor_NeonBait_Glow, 1 light pt |
| Decor_NeonBait_Glow | 282 | - | emissive |
| Decor_BathtubPlanter | 870 | - |  |

**Fixed site models**

| model | tris | footprint (cells) | notes |
|---|---|---|---|
| Site_MooringLine | 4808 | - |  |
| Site_LaneBuoy_Port | 208 | - |  |
| Site_LaneBuoy_Starboard | 208 | - |  |

## Grid and snapping (manifest `grid`)
- 1 m cells, corners on integer Unity x/z. A piece pivots on the centre of its footprint (1x1 at x.5, z.5; a 1x2 on the middle of its two cells). Yaw in 90 degree steps; pieces face +z (stairs and ramps rise toward +z; rails, trim and roof posts sit inboard of the +z edge / the +x,+z corner).
- **y = 0 in a berth model is the walking surface.** Place at `y = datum_y`. Tiles hang 0.16 below it, pilings run from -N up to -0.17.
- **Levels:** `datum_y(n) = 1.25 + n x 1.0`. n = 0 (1.25) is the tug deck, so the gangway always lands; n = -1 (0.25) is the beach. Stair rise 1.0 over 2.0; ramp rise 1.0 over 4.0. Rail top 1.0; roof posts 2.5; flat roof panel +2.5 (built into the model); pitched panel +2.5..+3.0, a gable is two panels (the second yawed 180) plus the ridge cap at +3.0; string lights hook at +2.3; wall 2.4.
- **Pilings:** pivot at the datum, lengths 2-7 m, world base = `datum_y - N`; use the shortest N with `N >= datum_y - ground_y + 0.3`.
- Wall decor (`Decor_BassPlaque`, `Decor_NeonBait`) pivots at the back face, bottom centre. Put it at `wall z + 0.40` of a `Berth_Wall_1m` (its front face is at z -0.40 of the wall cell).
- `Berth_NameSign.text_surface` (centre, size, normal) is the blank painted board for the crew name.
- Camps use the same 1 m steps from their own base level.

## The berth site (manifest `site`)
- Plot `x 41..53, z -47..-28`. Ground: z -47 = 0.24 (beach), z -45 = 0 (the coast), z -30 = -3.75, z -28 = -4.25 (SeabedY). Water at y 0.
- **Mooring line (fixed):** `Site_MooringLine.fbx`, authored in WORLD coordinates (place at the origin): six pontoon sections `x 41..53, z -30..-28`, top 1.25, four bollards (x 41.5, 44.5, 47.5, 50.0 at z -28.45), seven tyre fenders along the north edge z -28.0, a steel gangway landing plate x 51..53. Free building stops at z -30 (`buildable`).
- **Tug berth:** position (45.5, 0, -25.2), **yaw 270** (bow west, PORT side to the pontoon: the gangway gap is on the port bulwark). Hull spans x 37.5..53.0, z -27.7..-22.7 (0.3 m fender gap); the gangway lands at x 52.5. Note: `fg_harbour.py` has `TUG_B = 5.0`, not the 4 m in the brief; I used the constant (hull about 15.5 x 5).
- **Approach lane (keep clear):** rect `x 33..57, z -28..-15` (it contains the tug envelope). No piece may intersect it; the host validates placements against it. Six lane buoys (`Site_LaneBuoy_Port` on x 33, `_Starboard` on x 57, z -16/-21/-26) mark it. Because the plot ends at z -28 and building stops at -30, only a piece pushed beyond the plot could block it.
- **Starter landing:** `site.starter_landing.placements` is a list of kit pieces (model, position, yaw): a 2-wide stair from the beach, a 2 m walkway of 1x2 tiles to the pontoon, a 3 x 4 side platform with a bench, rod rack, cleat and lanterns, a west rail, pilings sized from `land_y`, a name board on the shore and a rope coil. 63 placements, all removable build pieces.

## Previews (`Art/Previews/BerthKit/`, Eevee 1280x720)
`kit_berth_structure.png`, `kit_berth_furniture.png` (the berth kit on a 1 m checker grid; roof panels, stair, ramp and pilings are lifted or lowered so they read), `kit_stand_and_decor.png`, `starter_landing_tug_moored.png` (the landing, the mooring line, the lane buoys and the tug footprint as a translucent ghost), `example_crew_dock.png` (a crew build using only kit pieces: a roofed workshop with wall, table, smoker, hammock, planter and the rest of the decor). Five images rather than four: the kit would not read on fewer.

## Sources and licences
No external assets, textures or fonts: all geometry is procedural, the lettering uses Blender's built-in font. No `SOURCES.md`.

## Unverified / needs the PC
- Nothing was opened in Unity. FBXs were re-imported in Blender 5.2 headless (53 of 53: one mesh, UVMap + Layer + Col, triangle counts match) and a second run keeps every FBX byte-identical (`export_if_changed`).
- Palette: there is no pink slot. `Decor_Flamingo` uses slot 9 (Roof, brick red) and reads red-coral. A pink slot (or a per-material tint on the flamingo) would be better; Blossom (14) is pink only in spring. The Unity palette's slot 11 ("Snow") is white all year and is used for plastic and enamel. Reed (bamboo) and Foliage (bass, shrubs) follow the season on purpose.
- Triangle counts run higher than REPO's ~100-tri crate: stand props 80-590 (the crate seat is ~200), furniture 300-600, decor 350-1250 (three stacked chairs). Instance them; cut `Berth_Smoker`, `Berth_RodRack` or `Decor_InflatableShark` first if the budget bites.
- AO is baked per model with a ground plane at y 0 (props) or none (deck-family pieces), so a piece placed against another piece will not show contact shadow; the site model is baked against the real terrain.
- The pontoon is modelled as a static, tall-float section (waterline at y -1.25 for datum 1.25). A bobbing pontoon would need the tug deck to bob with it.
- `Berth_RoofPanel_*` have no collider; `Berth_Rail_1m` needs a thin box collider; stairs and ramps need a wedge collider (bounds in the manifest).

## Unity steps (PC)
1. Import `Art/Export/BerthKit/*.fbx` with the harbour importer settings (the same postprocessor; none of the names ends `_Tri`). Check `uv1` survives (palette slot, texture layer) and the `Col` vertex colour is read as `_JitterEncoded`.
2. Make a prefab per model on `M_Toon_Base`; for each model with `emissive: true` assign an emissive variant and add the Point Lights from `light_points` (positions are in the model frame). Add the colliders listed in the manifest notes.
3. Place `Site_MooringLine` at the origin, the lane buoys at `site.approach_lane.buoys`, and the tug at `site.tug` (position, yaw 270).
4. Instantiate `site.starter_landing.placements` as the default build state of the berth plot (each is a normal removable piece in the world save).
5. Berth validator (host): reject a piece whose footprint intersects `approach_lane.rect` or the tug envelope, or lies outside `buildable`; snap y to `datum_y(n)`, yaw to 90 degrees, x/z to the cell grid.
