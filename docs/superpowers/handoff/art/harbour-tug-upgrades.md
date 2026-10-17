# Tug upgrades (branch `harbour-tug-upgrades`, 2026-10-17)

Bolt-on upgrade modules for the crew tug "Lucky Break" (design 3.7, the tug as a moving home), built in Blender from `Art/Blender/fg_tugmods.py`. Cut from `origin/harbour-art3`. `fg_harbour.py` is untouched (the new script imports it for the tug, the geometry helpers, the palette and the FBX writer).

## What was built
24 FBX models in **tug-local** space (origin = the tug's waterline centre, +z bow, +x starboard, y up; `TUG_WORLD` places the tug), 19494 triangles in total (the tug itself is 19.4k). Each module has a mount point and a footprint in the manifest and is clearance-checked against the deckhouse, the wheelhouse, the bulwarks, the four ice chests, the six fishing spots and the gangway gap.

| Upgrade (design 3.7) | Models |
|---|---|
| live well | `Tug_LiveWell` (raised planked fish box on the aft deck), `Tug_LiveWell_Lid` (hinged), `Tug_LiveWell_Water` (its own water object) |
| sonar | `Tug_SonarDome` (under the hull), `Tug_SonarCable` (duct-taped run from the dome to the screen), `Tug_SonarScreen` (swing-arm monitor in the wheelhouse) |
| crane and davit | `Tug_Davit` (aft starboard quarter), `Tug_Davit_Hook` (hook block, own object) |
| fighting chair | `Tug_FightingChair` (stern), `Tug_RodGimbal` (separate, pivoting) |
| bunks | `Tug_Bunks` (two stacked, deckhouse port wall) |
| galley | `Tug_Galley` (counter, 2-burner stove, hood + vent cowl, sink, chalkboard, extinguisher), `Tug_Galley_Kettle` (own object) |
| aquarium tank | `Tug_AquariumTank`, `Tug_AquariumTank_Water` |
| bait freezer | `Tug_BaitFreezer` (port foredeck) |
| outriggers | `Tug_Outriggers_Folded`, `Tug_Outriggers_Deployed` (swap pair, each with both bracket bases) |
| searchlight | `Tug_Searchlight` (wheelhouse roof), `Tug_Searchlight_Head` (pan/tilt, light point) |
| towed dinghy | `Tug_Dinghy` (free body, own origin = keel centre), `Tug_TowFairlead` (tug end of the tow line) |
| wheelhouse dash | `Tug_Dash_Stock` / `Tug_Dash_Upgraded` (swap pair, same slot on the console) |

Not built: the smoker (design 3.7 lists one; the task did not ask for it) and a tow-rope mesh (its length is variable: draw a rope between `Tug_TowFairlead.tow_point` and `Tug_Dinghy.tow_point`).

Tone: worn and slightly mismatched, grown-up goofy. The sonar cable is held on with grey duct tape (and is a trip hazard across the starboard walkway); the dash has a replacement panel in a different orange ("NOT SINKING"); one door each on the galley and the aquarium cabinet, one plank of the live well, the back cushion of the chair, the two bunk mattresses, the two outrigger poles and the two oars are from different sets; the live well says "NOT A SINK"; the bunk footboards are swapped (the lower bunk says UPPER); the galley menu is "SOUP: FISH / MAIN: FISH"; the aquarium is called KEITH; the dinghy is "BREAK II"; the stock dash has a label-maker strip that says "FUEL?".

## Files
- `Art/Blender/fg_tugmods.py`: the generator. Run: `FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_tugmods.py` (Blender 5.2.2, empty file, about 5 s). `FG_TUG_SHOT=<name> FG_TUG_OUT=<dir>` renders one preview shot instead; `FG_TUG_COMPOSE=<dir>` stitches the shots into `Art/Previews/TugUpgrades`. Shots available: `stock`, `upgraded`, `aft`, `galley`, `bunks`, `dash_stock`, `dash_upgraded`, `hull` (the last two extra ones are not committed). Rendering needs a GL library on a headless Linux box (`apt-get install libegl1 libgl1 libgl1-mesa-dri`) and is software Eevee: 3 to 12 minutes per shot on 4 cores.
- `Art/Export/TugUpgrades/*.fbx` (24) and `tugupgrades_manifest.json`.
- `Art/Previews/TugUpgrades/`: `tug_stock_vs_upgraded.png`, `aft_deck_closeup.png`, `galley.png`, `wheelhouse_dash_stock_vs_upgraded.png`.

## Models
Position/yaw are the mount in tug-local metres; footprint is the model's bounding box (x, y, z). Pivot = the hinge/pin for animated parts (see below). Min clear = the smallest gap (m) to any hard obstacle that is not an intended contact.

| Model | Tris | Mount position | Yaw | Footprint | Pivot | Min clear |
|---|---|---|---|---|---|---|
| `Tug_LiveWell` | 774 | (0.92, 1.25, -5.45) | 0.0 | 0.748x0.435x0.8 | - | 0.10 |
| `Tug_LiveWell_Lid` | 320 | (0.92, 1.615, -5.1) | 0.0 | 0.78x0.148x0.722 | (0.92, 1.615, -5.1) | 0.08 |
| `Tug_LiveWell_Water` | 152 | (0.92, 1.51, -5.45) | 0.0 | 0.61x0.046x0.51 | - | - |
| `Tug_FightingChair` | 1022 | (0, 1.25, -5.4) | 180.0 | 0.827x1.32x1.04 | - | 0.10 |
| `Tug_RodGimbal` | 266 | (0, 1.97, -5.76) | 180.0 | 0.24x0.336x0.413 | (0, 1.97, -5.76) | 0.17 |
| `Tug_Davit` | 969 | (1.05, 1.25, -6.95) | 90.0 | 2.515x2.661x0.5 | - | 0.07 |
| `Tug_Davit_Hook` | 344 | (3.275, 2.569, -6.95) | 0.0 | 0.219x0.754x0.212 | (3.275, 2.569, -6.95) | - |
| `Tug_TowFairlead` | 312 | (0.72, 2.17, -7.4) | 180.0 | 0.38x0.23x0.381 | - | 0.28 |
| `Tug_SonarDome` | 568 | (0, -1.5, 1.2) | 0.0 | 0.72x0.35x1.32 | - | - |
| `Tug_SonarCable` | 1618 | (0, 0, 0) | 0.0 | 2.827x6.204x0.931 | - | 0.14 |
| `Tug_SonarScreen` | 647 | (1, 4.95, 2.27) | 249.0 | 0.492x0.555x0.447 | - | 0.03 |
| `Tug_Dash_Stock` | 914 | (0.1, 4.6, 3.07) | 180.0 | 0.9x0.55x0.271 | - | 0.02 |
| `Tug_Dash_Upgraded` | 859 | (0.1, 4.6, 3.07) | 180.0 | 0.9x0.547x0.35 | - | 0.02 |
| `Tug_Searchlight` | 504 | (-0.55, 5.85, 1.25) | 0.0 | 0.775x0.81x0.515 | - | 0.20 |
| `Tug_Searchlight_Head` | 408 | (-0.55, 6.49, 1.25) | 0.0 | 0.42x0.539x0.505 | (-0.55, 6.49, 1.25) | 0.36 |
| `Tug_Outriggers_Folded` | 840 | (0, 4.05, 1.433) | 0.0 | 3.816x4.732x1.071 | - | 0.09 |
| `Tug_Outriggers_Deployed` | 1036 | (0, 4.05, 1.433) | 0.0 | 11.394x2.949x4.033 | - | 0.09 |
| `Tug_Bunks` | 1259 | (-1.05, 1.25, -1) | 0.0 | 0.822x1.721x1.818 | - | 0.14 |
| `Tug_Galley` | 1842 | (0.25, 1.25, 3.005) | 180.0 | 1.44x2.15x1.035 | - | 0.08 |
| `Tug_Galley_Kettle` | 332 | (-0.01, 2.29, 3) | 180.0 | 0.25x0.32x0.371 | (-0.01, 2.29, 3) | 0.15 |
| `Tug_AquariumTank` | 1377 | (1.21, 1.25, 0.55) | 270.0 | 0.466x1.245x1.225 | - | 0.13 |
| `Tug_AquariumTank_Water` | 128 | (1.21, 2.33, 0.55) | 270.0 | 0.385x0.363x1.07 | - | - |
| `Tug_BaitFreezer` | 627 | (-1.2, 1.55, 4.45) | 90.0 | 0.822x1.236x1.515 | - | 0.06 |
| `Tug_Dinghy` | 2376 | (0.3, -0.17, -15.02) | 0.0 | 1.473x1.214x3.915 | - | - |

The manifest also carries, per model: `colliders` (box colliders in tug-local space), `clearance` (the nearest obstacles and gaps), `requires` / `replaces`, `animHint`, and the extras the builder needs (`light_point`, `sleep_points`, `interaction_points`, `tow_point`, `steam_point`, `bubble_point`, `screen_quad`, `front_glass`, `fish_swim_volume`, `open_envelope`, `pole_pivots`).

## Install sets and swaps
`install_sets.stock` = the stock dash only; `install_sets.upgraded` = everything except `Tug_Dash_Stock` and `Tug_Outriggers_Folded`. Swap pairs: `Tug_Dash_Stock` / `Tug_Dash_Upgraded`, `Tug_Outriggers_Folded` / `Tug_Outriggers_Deployed`. `requires` lists the parent (lid needs the live well, gimbal needs the chair, hook needs the davit, kettle needs the galley, water needs its tank, head needs the searchlight base, screen needs the dome and the cable).

## Conventions
- Encoding is the harbour's: UV0 `UVMap` in metres, UV1 `Layer` = (palette slot, texture layer), vertex colour `Col` = 0.5 x baked AO, smooth normals, no materials in the FBX, palette slots `S` and texture layers `L` reused from `fg_harbour.py` (no new layer). Material `M_Toon_Base` for everything except the two water objects (`Tug_LiveWell_Water`, `Tug_AquariumTank_Water`: water material, no toon lighting, no foam). No `_Tri` or `_Col` models were needed.
- AO is baked against the stock tug mesh and every other module, so the bunks and the galley are darkened by the deckhouse walls. The Unity side applies the tug's own AO as before.
- Deterministic: all random draws come from `random.Random(SEED ^ crc32(stream))`; `export_if_changed` keeps unchanged FBX files byte-identical.
- Lettering is front faces only (a quarter of the triangles of a full extrusion).

## Design decisions the builder should know
- **The live well is raised**, not a hatch in the deck: the stock deck is an unbroken surface, so a recessed tank would be invisible. Box top deck + 0.34, water at deck + 0.26. `LiveWell_Lid` hinges at the forward top edge and opens up and over (about 100 degrees); its open envelope is in the manifest and is clear of ice chest 2 by 0.09 m.
- **The fighting chair sits 0.1 m clear of the stern fishing spot circle**, so the spot stays valid. When the chair is installed, `mount.seat_point` (0, 1.25, -5.4, facing 180) can be used as the angler's spot instead.
- **The davit boom overhangs the water** (reach about 1.6 m past the starboard quarter, tip at (3.35, 3.9, -6.95)); it is above head height (y > 2.9) so nothing on deck is under its swing. `Tug_Davit_Hook` rests 1.3 m below the tip; the builder draws the wire from `Tug_Davit.sheave` down to the hook pivot.
- **The sonar dome hangs 0.34 m below the keel**: the draft goes from 1.5 to 1.84 m (`draft_added`). It is underwater; the hull view (`FG_TUG_SHOT=hull`) is not one of the committed previews.
- **The sonar cable passes over the starboard fender ring and the bulwark cap by design** (marked allowed in the clearance report), crosses the walkway at z 1.85 between the nameboard and the porthole, goes under the upper-deck rail and through a drilled hole in the wheelhouse wall (a sealant blob marks it).
- **Outrigger bases clamp to the outside of the upper-deck rail posts** at z 1.43, x +-1.82, y 4.05; the deployed poles overhang the water up to x +-5.8 (swept volume), folded tips are at y 8.65.
- **The dinghy** floats with its keel 0.17 m under the waterline; `placement_tug_local` puts it at (0.3, -0.17, -15.02) with its bow eye 5.9 m behind `Tug_TowFairlead.tow_point`. The fairlead is on the transom cap at x 0.72, clear of the stern spot and of the gangway landing.
- **Interior furniture was fitted around the stock tug**: the bunks take the free port wall z -1.9..-0.1 (cot at z >= 1.0, lockers and the aft door clear), the aquarium the starboard wall z 0..1.1 (between lockers and shelves), the galley the forward wall (0.08 m from the cot and the shelves). The stock deck clutter (duffels, jerry cans, coils, tarp pile) is baked into the tug mesh and cannot be hidden, so every module clears it too (soft obstacles in the report).

## Clearance check (in the manifest, `clearance_problems` is empty)
Each part is an axis-aligned box (the boom, the cable and the outrigger poles are split into chunks) tested against: deckhouse and wheelhouse walls, roofs, door openings, ladder volumes, upper-deck rails, roof rail, mast, console, helm wheel, compass, the 4 ice chests, the 6 fishing spots (0.5 m standing circle, 1.9 m tall), the gangway landing (x -2.4..-0.8, z -7.55..-6.4), the bulwark inner face (0.17 m inside the outline), the stock deckhouse furniture and the baked-in deck clutter. Every module is also tested against every other (parent/child and swap pairs excepted). Result: no overlaps, smallest free gap 0.02 m (the two dashes against the rolled map on the console), every deck item at least 0.06 m from the bulwark, the live well and its open lid at least 0.08 m from the ice chests, the galley 0.08 m from the cot and the shelves.

## Sources and licences
No external assets: everything is procedural Blender geometry. No textures were made or downloaded (`Art/Textures/TugUpgrades` and a SOURCES.md are not needed). The user's Unity Asset Store packs were not reachable; a ready-made ship's-galley or aquarium pack would be a possible replacement for `Tug_Galley` / `Tug_AquariumTank` if the art direction ever wants more detail.

## Not verified
- Nothing was imported into Unity. The FBX files were verified by re-importing them in Blender (bounds, triangle count, UV0 `UVMap`, UV1 `Layer`, colour `Col` all match the manifest) and exported with the harbour's settings.
- Built with Blender 5.2.2 LTS on Linux; the exports are the same version family the PC runs (5.2). The preview renders are software Eevee with the harbour's toon preview, not Unity.
- The colliders in `colliders` are boxes derived from the geometry; no physics test was run.
- The clearance check is box-based (conservative): it is not a mesh-against-mesh test.
- Preview-only extras (not in any FBX): the interior point lights, the water and tank-water materials, the tow rope and the davit wire, the opened live-well lid, and the removal of the helm wheel faces in the dash shots.

## Unity steps (on the PC)
1. `git lfs pull`, merge or check out `harbour-tug-upgrades`.
2. Copy `Art/Export/TugUpgrades/*.fbx` to `Assets/_Project/Content/Models/TugUpgrades/` the way the Harbour FBX are copied (same import settings: scale 1, no materials, shared importer settings of the Models folder; none of these are `_Tri`, so `HarbourModelPostprocessor` does nothing to them). Check each bounds against the manifest `bounds_min` / `bounds_max` (a test like `HarbourPierModelTests` fits).
3. Add a builder step (`HarbourPierBuilder.Pass2` style) that reads `tugupgrades_manifest.json`, creates one child per model under the placed tug root `CrewBoat` at local position 0 and identity rotation (the FBX are already in tug-local space), and enables the models in the chosen install set. `M_Toon_Base` for all; the two water models get the water material (builder: translucent, ripple, no foam).
4. Pivot parts (`Tug_LiveWell_Lid`, `Tug_Davit_Hook`, `Tug_RodGimbal`, `Tug_Searchlight_Head`, `Tug_Galley_Kettle`): create an empty at `pivot.position` under the tug, parent the mesh under it with localPosition = -pivot.position, animate the empty (axis and limits in `pivot`).
5. Colliders from each model's `colliders` boxes (Structure layer), parented to the tug. The aft deck, bunks, galley counter, aquarium cabinet and freezer are solid; the lid, hook, outriggers and cable are not.
6. Light points: `Tug_Searchlight.light_point` (spot, aim along `direction`, pan/tilt with the head). Points of interest: `sleep_points` (bunks), `interaction_points` (galley), `bait_freezer.interaction_point`, `steam_point` / `bubble_point` for the particles, `fish_swim_volume` for the aquarium, `screen_quad` for the sonar and chartplotter screens.
7. The dinghy: spawn `Tug_Dinghy` at `placement_tug_local` as a floating Rigidbody, joined by a rope (LineRenderer + joint) from `Tug_TowFairlead.tow_point` to `Tug_Dinghy.tow_point` (the dinghy-local bow eye).
8. Swap the dash and the outriggers with their `replaces` partner; show the sonar cable only with the dome. When the chair is installed use its `seat_point` as the stern spot.
9. Run `Tools/unity.ps1 check`; no existing test should change (nothing in `Assets/` was touched).
