# harbour-villagers: the harbour's 11 villager models (10 villagers, the Garys are two)

Branch `harbour-villagers`, cut from `origin/character-fisher` (d153e17, the redesigned fisher). Blender 5.2.2 headless, no Unity.
Script: `Art/Blender/fg_villagers.py` (imports `fg_fisher.py` for its builders; that file is untouched).

## What was built
Static A-pose adults in the fisher's family look: true-sphere head with flat painted face decals (big white eyes, tiny dot pupils, heavy brows,
big nose), chunky metaball body parts that are voxel-remeshed and decimated, the same landmark ladder (head / shoulder / chest / belly / hip /
knee / ankle) scaled per person, and the pupils as separate objects so a rig can slide them (REPO side-eye). Each has its own silhouette:
Doug tall and narrow, Marge short and wide, Lorraine thin with a beehive, Fenwick a bulky yellow oilskin with a wide hat brim, Ambrose small and
stooped with huge ears, and so on. Tone 2.1: grown-up goofy, worn and tacky, nothing cute. Not rigged; the topology is the fisher's (voxel remesh
+ decimate), which the fisher rig skins too. Heights 1.49-2.06 m including hair/hats; feet on y = 0, pivot at the base centre.

| Villager | File | Tris | Height m | Role | Signature props (separate objects) |
|---|---|---|---|---|---|
| Doug Pellow | Doug_Pellow.fbx | 8932 | 2.02 | Harbourmaster | Lanyard, Clipboard, ReadingGlasses (LOST) |
| Marge Tackleby | Marge_Tackleby.fbx | 7998 | 1.66 | Bait shop owner, retired roller-derby champion | - |
| Gary Nettle (neat comb-over) | Gary_Nettle_Neat.fbx | 7026 | 1.75 | Net-mender (twin) | - |
| Gary Nettle (wild hair) | Gary_Nettle_Wild.fbx | 7050 | 1.82 | Net-mender (twin) | - |
| Wendell Krill | Wendell_Krill.fbx | 7798 | 1.58 | Smokehouse | LuckyHat (LOST) |
| Ines Varga | Ines_Varga.fbx | 8422 | 1.76 | Boatwright | WeldingGoggles |
| Dr. Penny Hollis | Penny_Hollis.fbx | 9898 | 1.84 | Marine biologist "between universities" | Spectacles, Lanyard |
| Lorraine Bass | Lorraine_Bass.fbx | 7462 | 2.06 | Cafe owner | CoffeePot |
| Reverend Gil | Reverend_Gil.fbx | 6770 | 1.80 | Chapel | BingoDauber, Sunglasses |
| Mr. Fenwick | Mr_Fenwick.fbx | 8799 | 1.73 | Gull Bar hermit | TinfoilBrim |
| Ambrose Lock | Ambrose_Lock.fbx | 6858 | 1.49 | Lock-keeper | PocketWatch |

Budget 6-10k each: all within (6.8k-9.9k). `tris` is the sum over every object in the file (props included); `tris_by_object` is in the manifest.

Signature features: Doug (hi-vis vest + reflective bands, lanyard with badge, clipboard, squint), Marge (bait-stained apron with two stain colours,
knee pads, wrist guards, sweatband), the Garys (same body and jumpers in swapped colours: Neat = comb-over, red jumper/blue trim; Wild = spiky ginger hair,
blue jumper/red trim; their eyes look at each other), Wendell (flat cap, droopy moustache, long soot-smeared apron, stoop), Ines (denim overalls with a patch
pocket, thick arms, welding goggles on the forehead, scowl), Dr. Hollis (mustard cardigan with elbow patches, waders, lanyard with a crest badge, messy bun
with a pencil, round spectacles), Lorraine (beehive, half-lidded deadpan, cafe apron, coffee pot), Reverend Gil (clerical black, dog collar, purple stole,
sunglasses, bingo dauber in the breast pocket), Mr. Fenwick (beard, three layers of oilskin, sou'wester with a tinfoil-lined brim), Ambrose (cardigan over a
waistcoat, pocket watch on a chain, slippers, white tufts).

## Files
- `Art/Blender/fg_villagers.py` (generator), `Art/Export/Villagers/<Name>.fbx` x11 and `Art/Export/Villagers/villagers_manifest.json`.
- Previews (Eevee, software GL): `Art/Previews/Villagers/villagers_front.png`, `villagers_three_quarter.png` (two-row lineups, 1280x720), `villagers_faces.png` (11 face close-ups).
- No new textures (see Textures below), so no `Art/Textures/Villagers` and no SOURCES.md.

## FBX objects per file
`<Name>_Body` (clothes, limbs, hands, feet), `<Name>_Head` (head sphere, ears, nose, hair, hats), `<Name>_Face` (eye whites, lids, brows, mouth, wrinkles; decals lifted
1.5-4 mm off the head), `<Name>_Pupil_L` / `_Pupil_R`, and `<Name>_Prop_<Thing>`. Props named `LOST` in the table are the villager's missing wish items
(ReadingGlasses, LuckyHat): they are placed on the ground beside the villager, 0.55 m to the right (Doug) / left (Wendell); hide them until found, then show them
on the villager's forehead or head. Other props are worn or carried at their rest place (clipboard in the left hand, coffee pot in the right, goggles on the forehead).
`TinfoilBrim` is a separate ring of faceted foil on the sou'wester brim.

## Encoding (the harbour's, with two villager-specific notes)
UV0 `UVMap` in metres (box projection per face); UV1 `Layer` = (palette slot, texture layer) per corner; vertex colour `Col` = 0.5 x (0.5 + 0.5 x baked AO) (28 rays, 0.35 m, a ground plane
as occluder, back faces passed through); smooth normals; no materials exported; objects at the origin with an identity transform; export settings and `export_if_changed` as in `fg_harbour.py`
(the unchanged-FBX check ignores the index-array order that Blender does not emit stably: three repeat runs in a row kept all 11 files).
- **Palette slot** indexes the per-model `palette` list in the manifest (display colours, the same sRGB-ish values the fisher uses). It does NOT index the harbour's `S` table: a villager has far more
  colours than the harbour has slots. Gary Neat and Gary Wild use the same keys (`jumper`, `trim`, `hair`) with swapped colours.
- **Texture layer** is a new character range, `CHAR_LAYERS` (20 skin, 21 shirt, 22 vest, 23 hat, 24 cargo, 25 sock, 26 leather, 27 webbing, 0 none), mapping to `Art/Textures/Fisher/<name>_detail.png`.
  Nothing in the harbour's 0-14 layer range was renumbered.

## Textures
Not rebuilt: the fisher's greyscale detail maps (Poly Haven CC0, see `Art/Textures/Fisher/SOURCES.md` on `origin/character-fisher-tex`) are used as the layers above; colour comes from the palette
(colour = tint x detail x 2, as in the fisher). That folder is not on this branch, so merge `character-fisher-tex` first (or copy the eight `*_detail.png` files).
The fisher weave was tuned for one tile per 0.16 m; UV0 here is in metres, so set the tiling to about 6.25 per UV unit.

## Unverified
- Nothing was imported into Unity: the FBX import, the UV1 / `Col` read by `M_Toon_Base` (or a character material), the 6.25 tiling, and the layer-to-texture lookup are untested. The re-import into Blender shows 2 UV maps, a `Col` attribute and no materials on every object.
- AO is baked per vertex in the A-pose; clothes with the character in a different pose (arms down) will show some wrong occlusion.
- The topology is voxel-remeshed and decimated (triangulated, no edge loops): fine for the fisher rig's auto-weights, not hand-weighted deformation.
- Previews use Eevee on software GL with a flat colour per palette entry; the game's toon shader and the detail maps will look different.
- Where a Unity Asset Store pack on the user's PC would fit better: NPC-style clothes and hair are fine as built; a purchased/free character-hair pack could replace the lathe/cone hair of Lorraine (beehive), Gary Wild and Ambrose (tufts), which are the weakest-looking parts.

## Exact steps for the PC
1. Merge `harbour-villagers` (and `character-fisher-tex` for the detail maps) and let Unity import `Art/Export/Villagers/*.fbx`; move/copy them under the project content folder the harbour FBX use (the harbour copies live under `Assets/_Project/Content/Models/Harbour/`).
2. Import settings: scale factor 1, no material import (the FBX has none), generate colliders off, keep the objects as separate renderers, normals from the file, read UV1 as `uv2`, `Col` as vertex colour.
3. Build a villager material (or extend `M_Toon_Base`) that reads `uv2` = (palette slot, texture layer): colour = the villager's palette[slot] (from `villagers_manifest.json`; the builder can write it as a
   per-villager colour array / MaterialPropertyBlock), detail = the greyscale map for the layer (21 shirt ... 27 webbing), tiled about 6.25 per metre, vertex colour `Col` x 2 as AO.
4. Placement: stand each villager at its building (`building` in the manifest); the model faces +Z. `Pupil_L/R` rotate about the head centre (the `Head` object's bounds centre; head radius 0.23-0.27 m, see the manifest heights).
5. Hide `Prop_ReadingGlasses` and `Prop_LuckyHat` until the wish item is returned.
6. Regenerate with: `FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_villagers.py -- model` (add ids to build one) and `... -- preview <outdir>`.
