# Reference style notes: RV There Yet, PEAK, REPO (2026-10-14)

Viewed from the official Steam store screenshots: RV There Yet app 3949040, PEAK app 3527290, R.E.P.O. app 3241660. These notes record what makes those worlds feel alive, so the harbour pass and the later river maps can learn from them. Use them for study only: never copy their assets.

Local copies of the viewed screenshots (browser captures, 800x450, not in the repo): `C:\Users\jayce\.claude\projects\C--Dev-FishGame\0f9e3f02-3865-47a4-a947-b2e248975860\tool-results\mcp-Claude_Browser-blob-*.jpg`.

## RV There Yet (closest to our target look)
- **Trees are clusters, not cones.** Pines and leafy trees are built from many soft, layered leaf clumps (card-like tufts with painted strokes), so the silhouette is ragged and breaks against the sky. A cone or hexagon blob reads as a placeholder next to them.
- **Vegetation density is high everywhere.** Grass tufts, bushes and saplings line every path edge and slope. Bare flat ground is rare.
- **The ground has variety:** packed-dirt tracks with soft edges, grass patches blending into dirt, small rocks and sticks.
- **Rock is big, smooth and sculpted.** Canyon walls are large rounded masses with gentle painted strata and broad light planes, not boulder piles.
- **Lighting is warm and high-key.** A golden directional sun gives soft but clear shadows, the sky is saturated blue with puffy clouds, and there is a light distance haze.
- **Props are chunky and rounded**, with warm wood, plaid fabric and worn paint. Characters are soft and rounded with big hands.
- **The vehicle is always in frame and interacting** with the terrain: tilting on rocks, a moose on the roof. The world reacts to it.

## PEAK
- **Painterly textures.** Bark, rock and wood carry visible brush strokes and colour variation inside one material. Surfaces are rarely a flat fill.
- **Bold, complementary palettes:** purple fog against orange wood, lava orange against teal hands. Atmosphere colour sets the mood of each biome.
- **Strong atmospheric fog** gives depth and makes layers of the scene read separately.
- **Scale and verticality:** rope bridges, ladders and huge trunks. Traversal props are part of the scenery.
- Plush, characterful players with accessories (flower crowns, hats, packs).

## R.E.P.O.
- **Clutter density in interiors:** every shelf and floor is busy with junk, crates and furniture, so the eye always finds something.
- **Readable silhouettes and goofy big eyes** on characters. This informs the later character pass: REPO-style tracking side-eyes.
- Moody lighting and VHS-style post. That's not our daytime harbour look, but useful for night camp and spooky variants.

## Gaps in our current harbour pass, and fixes
1. **Trees.** The low-poly cones and hexagon blobs read as placeholders. Rebuild the canopies as clustered leaf clumps (Sapling Tree Gen is now installed, plus manual clumping), with ragged silhouettes and soft per-clump AO. Use wind-bent coastal pines, plus a couple of leafier trees near the village.
2. **Ground.** Add dirt paths with soft blended edges, grass tufts along every path and wall edge, flower clumps, small stones and sticks. Avoid large untouched flat green areas.
3. **Rock and cliffs.** Fewer, larger, smoother sculpted masses with broad planes, rather than many separate grey blobs. The A.N.T. Landscape add-on helps for the headlands.
4. **Surface breakup.** In Unity, our palette slot plus painted texture layers carry this. Make sure every mesh uses a texture layer (wood, rock, sand, grass), not layer 0, wherever it covers a large area.
5. **Lighting and atmosphere** (Unity side, later): a warmer sun, a stronger sky colour and a light distance haze, checked against the GTX 900 Low preset.
6. **The tug as the hero.** Like the RV, it should be in view from most of the map and look lived-in. The LUCKY BREAK tug already does this well.

## Tech they ship (from the installed games' managed assembly names, 2026-10-14)
Read from the `*_Data/Managed` file lists of the user's installed copies (names only; no code or assets opened).
- **PEAK:** URP; **HBAO** (horizon-based ambient occlusion: the soft contact shadows), **LightVolume** (baked light volumes); Photon PUN + **Photon Voice**; DOTween; Cinemachine; Animation Rigging; Addressables; Odin.
- **REPO:** Post-processing Stack v2 (built-in-pipeline style post: the VHS look); Photon PUN + **Photon Voice**; **Klattersynth** (speech synthesis, likely the robot voices); Splines; Timeline; Addressables.
- **Gamble With Your Friends:** URP; **Dissonance** voice (FMOD playback/recording); Mirror networking (Steam transport); FMOD audio; **Shapes** (vector UI/FX); **Febucci TextAnimator** (bouncy text); NiceVibrations; MoreMountains tools; UModeler; DOTween.
- **Rust:** IL2CPP build (no managed names to read); its tugboat is a reference for our R78 tug.
- **Takeaways for FishGame:**
  - A good screen-space AO is a big part of the "soft toon" depth. Check URP's built-in SSAO on our Medium/High presets before adding anything.
  - Bouncy text and tweening are cheap juice worth having: DOTween-style easing, and TextAnimator-like effects for toasts.
  - Voice: two of three use Photon Voice and one uses Dissonance. We plan Vivox (research 2026-10-14-voice-mimic-seagulls.md).
- **Limit:** REA analyses code, not Unity art files (meshes, textures, materials). Inspecting their art directly would need a Unity asset viewer (e.g. AssetStudio or AssetRipper), study only.

## How their art is built (asset study, 2026-10-14)
Study only. PEAK and REPO were opened read-only in AssetRipper (material properties, texture sizes, mesh counts). RV There Yet is an Unreal game (install folder `Ride`); its packages are not opened, so only the asset path list in its `Manifest_UFSFiles_Win64.txt` was read. A few PEAK textures were saved to the session scratchpad for viewing; nothing was copied into the repo.

### The shared trick: grey masks, colour from parameters
PEAK and RV There Yet both keep texture maps **greyscale** and set colour in the material.
- **PEAK foliage:** shader `GD/FoliageGD`.
  - **Textures:**
    - A 2048 top-down crown card (`T_Foliage TreeCrown`). It is a radial burst of painted brush strokes with branch-fork tips, alpha-clipped at 0.4.
    - A tiling 1024 "paint" mask (`A_Paint_Foliage`). It holds soft blobs plus doodled leaf-scallop strokes (w/m shapes), all in grey.
  - **Colour comes from parameters:**
    - `_BaseColor` (teal-green).
    - `_TopColor` and `_TopColorSecondary`, with a top-surface tightness. Upward faces get a lighter, yellower green.
    - `_BottomColor`, a dark blue-purple for shadowed undersides.
    - `_HueShifts` and `_HueStr`, which vary the hue per object.
    - Vertex-colour amount and vertex wind.
  - **Projection:** textures are **triplanar** (`_TRIPLANAR1..3`). That is why bark and rock never show UV seams.
- **PEAK rock:** a Shader Graph (`W/Peak_Rock`) with scripts `SetRockColors` and `RockMaterialSwapper`. The same rock meshes are recoloured per biome.
- **PEAK grass:** GPU grass (`GrassGeometryCompute`, `GrassChunking`, `GrassRenderer`). Grass is not hand-placed meshes.
- **RV There Yet rock:** rocks ship only `_Mask` and `_Nor` textures. Colour comes from a gradient atlas (`CurveAtlas_Rocks`: `Curve_Rock_BaseColor`, `_Grey`, `_Jungle`). Each rock has material instances per biome (`MI_CliffWall_02_Jungle`, `_Snow`, `_Grey`, `_Golf`).
- **RV There Yet master materials:** a handful (`MM_Asset`, `MM_Foliage`, `MM_TreeStem`, `MM_Trim`, `MM_Landscape`) drive hundreds of instances. Props have a `TX_ColorMask_Default`.
- **RV There Yet landscape:**
  - Material functions per surface: `MF_Dirt`, `MF_Grass`, `MF_Rock`, `MF_RockyDirt`, plus `_Jungle`/`_Snow`/`_Cold` variants.
  - Runtime virtual textures (`RVT_Landscape*`). Meshes sampling the RVT pick up the ground colour where they meet it, so rocks and foliage blend into the terrain.
- **RV There Yet trees:**
  - Spruce, fir, jungle and palm, each with a separate simple collision mesh (`_COL`).
  - Snow and no-moss variants.
  - **Impostor** billboards (`MI_Impostor_SM_Spruce_01`) for distance.
  - Grass is landscape grass types (scattered automatically from the ground material).
- **RV There Yet uses bought or free marketplace packs** (UltraDynamicSky, Stylized_Rocks_Free, Simple_Trees_Pack, MWLandscapeAutoMaterial, TrainPack). The look comes from one consistent recolouring system, not from bespoke art for every asset.
- **REPO is different.** Props are standard PBR from Substance Painter: `_BaseColor`/`_Normal`, plus metallic/smoothness on some. Textures are mostly 512 (1024 for hero props), and props are low poly:

  | REPO prop | Triangles |
  |---|---|
  | Crate | 108 |
  | Barrel | ~570 |
  | Bookshelf | ~320 |
  | Big valuable crate | ~860 |

  The REPO look is density of cheap props, not detail per prop.

### Mesh density (PEAK)
| Asset | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| Rock_Round | ~4.0k tris | 1.2k | 600 | 200 |
| Rock_Wall | ~3.3k | 1.3k | 500 | 250 |
| Fern | 112 | 55 | 28 | 10 |
| Dead shrub | 1.0k–3.2k | | | |

Hero rocks are a few thousand triangles with four LODs. Small plants are tiny, and colliders are separate low-poly meshes.

### What this means for FishGame
1. **Our palette-slot + texture-layer approach matches what both games do.** Push it further:
   - Keep the painted layers greyscale.
   - Add a **top colour** (world-up tint) and a **bottom/shadow colour** (cool blue-purple) to M_Toon_Base/M_Toon_Tri.
   - Add a small per-object hue shift.
   - This is one shader change that lifts every mesh, and it's cheap on a GTX 900.
2. **Trees:** build canopies from crossed/clustered **alpha-clipped crown cards** with a painted radial-stroke texture. Faceted clumps look more solid but less painterly. Keep a simple trunk collider.
3. **Biome variants by recolour:** one rock or tree set, recoloured per river stage (palette + top/bottom colours). This fits the 7-stage escalation without new meshes per map.
4. **LODs and impostors:** hero rocks ~3–4k triangles with LOD1–3. Far trees as billboards on Low. This gives triangle budget back (the harbour pass2 is ~22k over on High).
5. **Grass:** GPU-instanced or compute grass over the land beats hand-placed tuft meshes, both for density and for cost. Candidate for the art polish task, after M4 core.
6. **Ground blending:** an RVT-like effect (meshes tinted by the terrain colour near the ground) is a later nice-to-have. A cheap version is a world-height gradient in the toon shader.
