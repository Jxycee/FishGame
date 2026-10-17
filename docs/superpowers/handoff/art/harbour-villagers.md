# harbour-villagers: the harbour's 11 villagers as PLACEHOLDER proxies + outfits + props

Branch `harbour-villagers` (from `origin/character-fisher` d153e17, then reworked on the orchestrator's 2026-10-17 scope change; the earlier fisher-style commits stay in history).
Script: `Art/Blender/fg_villagers.py` (Blender 5.2.2 headless). It no longer imports `fg_fisher.py`: the few generic metaball/lathe helpers are copied into it, and nothing of the fisher's head, face or body is used.

## What is a placeholder and what is not
The player-character look is unsettled (the fisher base was rejected; settled direction: faces painted into the head texture, toon shading with outlines, a PEAK-style body, Schedule 1 tone).
- **Placeholder: `<Name>_Proxy_Body`.** One smooth skin object (plain sphere head, torso, arms, mitts, legs, feet), no face, ears or nose. It fixes each villager's height, build and posture
  (heights 1.49-2.06 m incl. hair/hats; Doug tall and narrow, Marge short and wide, Lorraine thin, Fenwick bulky, Ambrose small and stooped), so the 11 read apart at 20 m. Replace it when the base is settled.
- **Refittable: `<Name>_Outfit_<Piece>`.** Clothes and head gear, each its own object. They are built from the proxy's parameters (`torso_profile`, `arm_geom`, head sphere), so changing a spec's
  height/width/belly/hunch regenerates a fitted set; for the settled base, refit the same builders to its measurements.
- **Kept as is: `<Name>_Prop_<Thing>`.** Signature props, separate objects. `ReadingGlasses` (Doug) and `LuckyHat` (Wendell) are the lost wish items (`lost: true` in the manifest), lying on the ground beside them: hide until found.
- Not in the files: eyes, pupils, brows, nose, ears, mouth (see the face briefs).

| Villager | File | Tris | Height m | Outfit and prop objects |
|---|---|---|---|---|
| Doug Pellow | Doug_Pellow.fbx | 8588 | 2.02 | Outfit_Hair, Outfit_Shirt, Outfit_HiVisVest, Outfit_Trousers, Outfit_Shoes, Prop_Lanyard, Prop_Clipboard, Prop_ReadingGlasses |
| Marge Tackleby | Marge_Tackleby.fbx | 7490 | 1.66 | Outfit_Hair, Outfit_Sweatband, Outfit_Tank, Outfit_Apron, Outfit_WristGuards, Outfit_Shorts, Outfit_Sneakers, Prop_KneePads |
| Gary Nettle (neat comb-over) | Gary_Nettle_Neat.fbx | 7322 | 1.75 | Outfit_Hair, Outfit_Jumper, Outfit_Trousers, Outfit_Shoes |
| Gary Nettle (wild hair) | Gary_Nettle_Wild.fbx | 7346 | 1.82 | Outfit_Hair, Outfit_Jumper, Outfit_Trousers, Outfit_Shoes |
| Wendell Krill | Wendell_Krill.fbx | 7226 | 1.58 | Outfit_Cap, Outfit_Shirt, Outfit_Apron, Outfit_Trousers, Outfit_Boots, Prop_LuckyHat |
| Ines Varga | Ines_Varga.fbx | 8048 | 1.76 | Outfit_Hair, Outfit_Tee, Outfit_Overalls, Outfit_Boots, Prop_WeldingGoggles |
| Dr. Penny Hollis | Penny_Hollis.fbx | 9942 | 1.84 | Outfit_Hair, Outfit_Blouse, Outfit_Waders, Outfit_Cardigan, Prop_Spectacles, Prop_Lanyard |
| Lorraine Bass | Lorraine_Bass.fbx | 7294 | 2.06 | Outfit_Hair, Outfit_Blouse, Outfit_Apron, Outfit_Slacks, Outfit_Shoes, Prop_CoffeePot |
| Reverend Gil | Reverend_Gil.fbx | 7142 | 1.80 | Outfit_Hair, Outfit_Clerical, Outfit_Collar, Outfit_Stole, Outfit_Trousers, Outfit_Shoes, Prop_BingoDauber, Prop_Sunglasses |
| Mr. Fenwick | Mr_Fenwick.fbx | 9157 | 1.73 | Outfit_Beard, Outfit_Souwester, Outfit_Jumper, Outfit_LongCoat, Outfit_Jacket, Outfit_Trousers, Outfit_Wellies, Prop_TinfoilBrim |
| Ambrose Lock | Ambrose_Lock.fbx | 6946 | 1.46 | Outfit_Hair, Outfit_Waistcoat, Outfit_Cardigan, Outfit_Trousers, Prop_Slippers, Prop_PocketWatch |

Budget 6-10k tris each, all within (props included). Per-object triangles and kinds are in `villagers_manifest.json` (`objects`).
Outfit pieces: Doug HiVisVest + lanyard + clipboard; Marge Apron, Tank, Shorts, WristGuards, Sweatband, KneePads (prop); Garys Jumper, Trousers, Hair (neat comb-over / wild spikes; jumper colours swapped);
Wendell Apron, Cap, Shirt; Ines Overalls, Tee, Boots, goggles (prop); Hollis Cardigan, Waders, Blouse, lanyard + spectacles (props); Lorraine Apron, Hair (beehive), coffee pot (prop);
Gil Clerical, Collar, Stole, dauber and sunglasses (props); Fenwick Beard, Souwester, LongCoat, Jacket, Wellies, tinfoil brim (prop); Ambrose Cardigan, Waistcoat, slippers + pocket watch (props).

## Shared palette (no per-model palettes, no character texture layers)
UV1 `Layer` = (palette slot, 0). Slots used: 17 Bronze, 18 Steel, 19 Iron, 20 GlassGreen, 23 GlassClear, 24 Gold, 25 Paper, 29 Khaki, 30 White, 31 Navy, 72 CharSkin, 73 CharCloth, 74 CharDark, 75 CharBright, 76 CharRubber, 77 CharHair, 78 CharLeather, 79 CharAccent.
- Fixed slots reused from harbour-props by name: Iron 19, Steel 18, Gold 24, Paper 25, Khaki 29, White 30, Navy 31, GlassClear 23, GlassGreen 20, Bronze 17 (use the palette colour).
- New slots in the villagers' range 72-79, all TINTABLE: 72 CharSkin, 73 CharCloth, 74 CharDark, 75 CharBright, 76 CharRubber, 77 CharHair, 78 CharLeather, 79 CharAccent.
  Eight slots cannot hold every skin tone and garment colour, so the colour of a 72-79 vertex = the slot colour x the per-key `tint` in `models[].palette` (`key`, `slot`, `slot_name`, `tint`), applied by the builder as a per-object colour/MaterialPropertyBlock. The manifest top level lists both groups.
- The character texture layers 20-27 were dropped.

## Encoding
UV0 `UVMap` in metres (box projection per face); UV1 `Layer` as above; vertex colour `Col` = 0.5 x (0.5 + 0.5 x baked AO); smooth normals; no materials exported; objects at the origin, identity transform, pivot at the base centre; facing +Z.
Export settings and `export_if_changed` as in `fg_harbour.py` (the unchanged-FBX check ignores Blender's unstable index-array order). Static A-pose, not rigged; the proxy topology is voxel-remesh + decimate.

## Previews (`Art/Previews/Villagers/`, Eevee on software GL)
`villagers_front.png`, `villagers_three_quarter.png` (dressed lineups) and `villagers_proxy_front.png` (placeholders only, the silhouette read). The old face close-up sheet is removed (faces no longer exist in the models).

## Face briefs
`docs/superpowers/handoff/art/harbour-villagers-face-briefs.md`: expression, eye shape, brows, mouth, wrinkles, stubble and one line of attitude per villager, for the painted-face texture.

## Unverified
Nothing was imported into Unity (FBX import, `uv2` slot read, the tint property, `Col` as AO). The tint mechanism needs a builder-side material/MPB. Outfit fit against the settled base is untested by definition.
The hair of Lorraine (beehive), Gary Wild and Ambrose is lathe/cone placeholder geometry.

## Exact steps for the PC
1. Merge the branch; import `Art/Export/Villagers/*.fbx` beside the harbour FBX (no materials in the files).
2. Read `uv2` = (slot, 0) for the shared palette; for slots 72-79 multiply by the per-key tint from `villagers_manifest.json`; `Col` x 2 is AO.
3. Hide `Prop_ReadingGlasses` and `Prop_LuckyHat` until their wish items are returned; `Pupil_*`, `Face`, `Head`, `Body` objects of the previous commit no longer exist.
4. Regenerate: `FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_villagers.py -- model` (optionally ids) and `... -- preview <outdir>`.
