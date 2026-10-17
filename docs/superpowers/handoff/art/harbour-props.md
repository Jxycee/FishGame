# harbour-props: small hand-held and catchable props (design 3.2, 3.3, 3.4, 5.3)

Branch `harbour-props`, cut from `origin/harbour-art3`. Generator: `Art/Blender/fg_props.py` (Blender 5.2.2, headless; `fg_harbour.py` is untouched and imported for its primitives, `Group`, `make_object`, `export_if_changed` and the slot/layer tables; that import has no side effects).

Run: `FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_props.py` (env `FG_NOPREVIEW=1` skips the renders, `FG_NOEXPORT=1` the FBX, `FG_ONLY=Teapot,Shovel` limits the set). Deterministic: a second run keeps all 73 FBX byte-identical (`export_if_changed`: "kept").

## What was built (73 FBX, 46182 triangles in total)

* **Villager belongings (10, plain + fished-up):** Doug's reading glasses, Lorraine's teapot, the chapel bell (0.76 m body, bronze, on a carrying beam), the bait-shop sign (TACKLEBY'S / BAIT, TACKLE, OPINIONS, raised lettering, front faces +z), the lost net bundle, the brass propeller (0.6 m), the lock's crank handle, Wendell's smoking hat (fish pin and a corncob pipe in the band), Dr. Hollis's field notebook, Gil's bingo-ball message bottle (B7 ball as the stopper).
* **Collectibles:** message bottles A/B/C (green wine bottle with scroll, brown flask with wax seal and tag, long blue flask lying down); 8 shells (scallop, whelk, cowrie, clam, sand dollar, razor clam, moon snail, turret); sea glass in 4 colours (green, brown, blue, white = palette slots GlassGreen/GlassBrown/GlassBlue/GlassClear); treasure chest closed and open (coins, ruby, pearl string); dig mound; dug hole. Every one except the mound and the hole also has a `_Fished` variant.
* **Technique gear (13):** jig lure, trolling lure, cast net folded and spread, crab pot with buoy and rope (built on `fg_harbour.lobster_pot`, so it matches the harbour LobsterPot exactly, plus a funnel mouth, hatch, rope and buoy), lantern-fishing rig, heavy fighting rod, ice auger, ice-hole rim, fly rod and reel, metal detector, shovel, hand cart. `_Fished` variants only where the thing comes out of the water: crab pot, both cast nets, lantern rig.
* **Fished-up** = the plain mesh plus weed strands (Foliage slots, layer Foliage), barnacle clusters (ShellCream/Sand) and the vertex colour multiplied by 0.60 (wet tint). Same pivot and bounds as the plain model within the weed. Triangles are the plain count plus ~+30-45%.

## Conventions (also in each manifest entry)

* Unity metres, authored in Unity axes, FBX written exactly like the harbour's (each FBX object at the origin, identity transform). The **front of every prop faces +z**; a viewer standing in front of it looks toward -z (the sign and notebook text read for that viewer).
* Pivot = base centre (rests on y = 0) or the grip for held items; `pivot` and `held_axis` are in the manifest. Rods, lures and nets point along +z (or hang -y); **shovel, auger and jig hang along -y**; the cast net spread opens downward from its apex; **DugHole and IceHoleRim sink below y = 0** (the hole is 0.16 m deep, the ice water 0.05 m), place them with y = ground height.
* Encoding: UV0 "UVMap" metres, UV1 "Layer" = (palette slot, texture layer), vertex colour "Col" = 0.5 x per-vertex BVH AO (40 rays, 0.35 x size clamped 0.04-0.45 m, self occlusion plus a ground plane at y = 0 for ground props) x tint, smooth/analytic normals, no materials exported. **All props use M_Toon_Base** (none is `_Tri`, none is a collider).
* Palette: harbour slots 0-10 are reused. **New slots 16-31** (Brass, Bronze, Steel, Iron, GlassGreen, GlassBrown, GlassBlue, GlassClear, Gold, Paper, ShellPink, ShellCream, Ice, Khaki, White, Navy), preview colours in `props_manifest.json` (`palette`). Slots 11-15 (the harbour's preview-only extras) are not used. Layers: only existing ones (Wood, Cloth, Rock, Dirt, Foliage, none); nothing new.

## Triangles (plain / fished-up)

| model | group | size m (x,y,z) | plain | fished |
|---|---|---|---|---|
| Prop_ReadingGlasses | villager | 0.14 x 0.06 x 0.16 | 302 | 422 |
| Prop_Teapot | villager | 0.36 x 0.27 x 0.21 | 573 | 753 |
| Prop_ChapelBell | villager | 0.95 x 0.92 x 0.78 | 956 | 1316 |
| Prop_TacklebySign | villager | 1.61 x 0.83 x 0.20 | 1216 | 1636 |
| Prop_LostNetBundle | villager | 1.08 x 0.28 x 0.51 | 850 | 1210 |
| Prop_BrassPropeller | villager | 0.54 x 0.48 x 0.19 | 514 | 1024 |
| Prop_CrankHandle | villager | 0.07 x 0.37 x 0.40 | 302 | 482 |
| Prop_SmokingHat | villager | 0.43 x 0.15 x 0.43 | 514 | 664 |
| Prop_FieldNotebook | villager | 0.17 x 0.04 x 0.24 | 580 | 760 |
| Prop_BingoBottle | villager | 0.09 x 0.28 x 0.09 | 474 | 654 |
| Prop_Bottle_A | collectible | 0.09 x 0.34 x 0.09 | 340 | 520 |
| Prop_Bottle_B | collectible | 0.16 x 0.23 x 0.17 | 506 | 686 |
| Prop_Bottle_C | collectible | 0.10 x 0.10 x 0.39 | 446 | 626 |
| Prop_Shell_Scallop | collectible | 0.20 x 0.06 x 0.13 | 560 | 800 |
| Prop_Shell_Whelk | collectible | 0.16 x 0.12 x 0.11 | 400 | 640 |
| Prop_Shell_Cowrie | collectible | 0.06 x 0.07 x 0.11 | 280 | 430 |
| Prop_Shell_Clam | collectible | 0.14 x 0.09 x 0.17 | 532 | 772 |
| Prop_Shell_SandDollar | collectible | 0.12 x 0.03 x 0.12 | 246 | 396 |
| Prop_Shell_Razor | collectible | 0.15 x 0.02 x 0.08 | 448 | 598 |
| Prop_Shell_MoonSnail | collectible | 0.12 x 0.13 x 0.15 | 412 | 652 |
| Prop_Shell_Turret | collectible | 0.17 x 0.10 x 0.10 | 362 | 572 |
| Prop_SeaGlass_Green | collectible | 0.05 x 0.02 x 0.04 | 80 | 110 |
| Prop_SeaGlass_Brown | collectible | 0.07 x 0.02 x 0.04 | 80 | 110 |
| Prop_SeaGlass_Blue | collectible | 0.05 x 0.04 x 0.04 | 100 | 130 |
| Prop_SeaGlass_Clear | collectible | 0.07 x 0.02 x 0.06 | 164 | 194 |
| Prop_Chest_Closed | collectible | 0.58 x 0.38 x 0.37 | 652 | 1072 |
| Prop_Chest_Open | collectible | 0.58 x 0.57 x 0.58 | 1024 | 1444 |
| Prop_DigMound | collectible | 0.51 x 0.34 x 0.46 | 704 | - |
| Prop_DugHole | collectible | 0.82 x 0.21 x 0.82 | 324 | - |
| Prop_JigLure | gear | 0.09 x 0.17 x 0.09 | 428 | - |
| Prop_TrollingLure | gear | 0.08 x 0.09 x 0.30 | 520 | - |
| Prop_CastNet_Folded | gear | 0.25 x 0.26 x 0.59 | 650 | 920 |
| Prop_CastNet_Spread | gear | 2.65 x 0.59 x 2.65 | 968 | 1268 |
| Prop_CrabPot | gear | 1.15 x 0.48 x 0.72 | 1118 | 1538 |
| Prop_LanternRig | gear | 0.15 x 1.01 x 1.67 | 562 | 772 |
| Prop_FightingRod | gear | 0.17 x 0.27 x 1.85 | 921 | - |
| Prop_IceAuger | gear | 0.40 x 1.08 x 0.12 | 472 | - |
| Prop_IceHoleRim | gear | 1.19 x 0.17 x 1.22 | 368 | - |
| Prop_FlyRod | gear | 0.08 x 0.42 x 3.00 | 845 | - |
| Prop_MetalDetector | gear | 0.28 x 0.79 x 1.63 | 874 | - |
| Prop_Shovel | gear | 0.22 x 1.15 x 0.07 | 384 | - |
| Prop_HandCart | gear | 0.92 x 0.81 x 1.86 | 960 | - |

Most props are 250-650 triangles; the heavy ones are the chest open (1024), crab pot (1118, it embeds the harbour pot), sign (1216, ~800 of them are the lettering: if too heavy, paint the two lines as a texture) and hand cart (960). Budget is a guideline, not a Unity limit.

## Previews (Art/Previews/Props)

`props_villager.png`, `props_collectibles.png` (bottles, shells, sea glass, chest, dig site), `props_gear_small.png`, `props_gear_long.png`: flat-lays, plain on the left of each pair and fished-up on the right, Eevee 1280x720, neutral ground. **Not to scale**: each item is scaled to a common display size per image (labels name them; real sizes are in the manifest and the table above).

## Sources and licences

No external assets and no textures: everything is generated from code (no Poly Haven / ambientCG downloads, so no `Art/Textures/Props/SOURCES.md`). The sign lettering uses Blender's built-in Bfont (GPL/bundled with Blender; the extruded glyph geometry is baked into the mesh).

## Unverified / known weak spots

* **Not opened in Unity.** FBX files were re-imported in Blender to confirm the UV sets, the "Col" colour attribute and no materials; the Unity axis round-trip uses the harbour's exact export settings but was not tested for these files.
* Previews use a 32-colour preview palette (harbour table + my slots); the real Unity palette texture needs slots 16-31 (below).
* Layers (Wood, Cloth, Rock, Dirt, Foliage) are assigned but only judged by eye through the palette preview; the cast nets and the crab-pot netting rely on the Cloth layer to read as net.
* Look notes (after the redo): the whelk and moon snail now have crisp stepped ridges, the razor clam is two hinged valves, the teapot is taller and rounder, the dig mound has stones, a crab-hole dimple and a stick; the moon snail's mouth is still a simple pink disc.
* The shell/bottle/sea-glass sizes are real-world and tiny (1-12 cm): scale up in Unity for readability or give them a pickup glow.
* Unity palette strip is assumed to be extendable beyond 16 slots (the harbour only ever used 0-15).
* The Asset Store packs on the user's PC were not available; better hand-modelled villager props (sign, bell, propeller) could replace these if one of those packs fits.

## Unity steps (PC)

1. Extend the palette texture and the slot lookup to 32 slots and append the colours of slots 16-31 (see `palette` in `Art/Export/Props/props_manifest.json`; Preview colours are a starting point, keep the toon look: Brass/Bronze warm, Steel/Iron cool grey, glass slots slightly desaturated).
2. Import `Art/Export/Props/*.fbx` with the same import settings as `Art/Export/Harbour` (no materials, read/write per the harbour rule, vertex colours kept); assign **M_Toon_Base** to all.
3. For each model make a prefab with the FBX as a child at the origin; place pivots as in the manifest (`pivot`, `held_axis`, `rests_on_ground`). `_Fished` models are drop-in swaps for the plain mesh (same pivot) used when the item is pulled out of the water; the swap can be a mesh/renderer swap.
4. Held gear: parent under the hand socket with the pivot on the grip; +z is the forward axis of the item (shovel/auger/jig hang -y).
5. Colliders: add simple box/sphere/capsule colliders by hand from `bounds_min/max` (no `_Col` meshes were exported).
6. Optionally scale tiny collectibles (shells, sea glass, bottles) x2-3 in the prefab.
