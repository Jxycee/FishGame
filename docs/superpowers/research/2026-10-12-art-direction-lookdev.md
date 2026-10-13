# Art direction look-dev: realistic toon (R74), 2026-10-12

Branch `m2-lookdev`. The before/after sheet is `docs/superpowers/handoff/img/2026-10-12-lookdev-realistic-toon.png`. The left column is the M2 low-poly look and the right column is the look-dev. The rows, top to bottom, are: dock end, shore, player close-up, bluegill reveal close-up, dusk. The shots come from `FishGame.EditorTools.LookDevCapture.Capture`.

## 1. Reference study (short)

### RV There Yet? (Nuggets Entertainment, UE5)
From general knowledge only. Not checked against screenshots in this pass (one web search gave no visual description), and the pak files were not read.

- **Shading:** physically based lighting pushed toward stylised. It has broad soft light-to-shadow transitions, warm sun, cool sky-filled shadows, and strong ambient occlusion in creases (Lumen-style GI). There is no hard cel band.
- **Outlines:** none.
- **Textures:** hand-painted-feel albedo at a medium texel density, with low-frequency colour variation, soft brushy wood and rock, and grass that is mostly colour and value with little detail. Normal maps are subtle.
- **Colour:** saturated but natural greens and oranges. The value range is wide: bright sunlit tops and fairly deep shadows.
- **Atmosphere:** heavy atmospheric haze and fog that layers the mountains, and warm golden-hour grading.
- **Shapes:** chunky, rounded and slightly exaggerated (the RV, rocks, trees). Bevelled edges everywhere, and no visible faceting.

### Gamble With Your Friends (Unity URP, Mono)
Read-only string scan of a scratch copy of its `*.assets` files. Mechanics and numbers only; no assets were copied.

- **Shading:** stock `Universal Render Pipeline/Lit` with `_NORMALMAP` (137 references), `_EMISSION` and some `_SPECULAR_SETUP`. A `ScreenSpaceAmbientOcclusion` renderer feature is in use (`_SCREEN_SPACE_OCCLUSION`). There are a few custom Shader Graphs (`ShortHikeLikeUnlit`, `S_Fresnel_shader`, a `_ColorRamp*` gradient shader, and a grass wind shader).
- **Outlines:** only for interaction highlight (`Custom/Outline Fill`, `Custom/Outline Mask`). No art outline.
- **Textures:** modular-kit naming (`SM_Bld_*`, `SM_Env_*`, `SM_Prop_*`, trim sheets such as `TrimArches` and `Wall_Trim`). Colour comes mostly from gradient and trim textures (`FirstFloorGradient_*`, `gradient`, `toonSeamlessNoise`) plus a few painted materials (`Wood_Planks`, `Casino_WoodTexture`, `M_SandMarble*`). Detail stays low and colour reads first.
- **Colour, light and post:** bright and saturated casino palette. Post is `Bloom`, `Vignette`, `Tonemapping` and `ColorAdjustments`.
- **Shapes:** smooth bevelled props and big readable silhouettes.

**Takeaway for FishGame.** Both games get "realistic toon" from four things rather than from outlines:

- smooth bevelled shapes;
- soft lighting with occlusion;
- low-frequency painted colour variation;
- atmosphere.

Both keep texture detail secondary to colour and value.

## 2. FishGame style guide (proposed standard once approved)

### 2.1 Shading model: `FishGame/Toon`, extended in place
One shader for everything opaque. Every new property defaults to off, so older materials render as before.

- **Ramp:** `band = smoothstep(mid - soft, mid + soft, n.l * shadow)`. The standard is `mid 0.30` and `soft 0.28`, a gentle 0.02 to 0.58 ramp. Shadow tint comes from `_ToonShade` (the day-phase global) and lit tint from the light colour.
- **Sky fill:** `lerp(_SkyGround, _SkyTop, n.y) * 0.20 to 0.22`. Shaded sides pick up sky blue from above and ground bounce from below. This gives the subtle colour gradient across forms.
- **Rim:** `pow(1 - n.v, 3 to 3.5) * 0.18 to 0.30`, tinted from the sky horizon (shaded side) to the light (lit side). It separates silhouettes from the water.
- **Toon highlight:** smoothstepped Blinn-Phong on the lit side only. It is off for the environment, 0.10 for skin, and 0.30 at power 180 for fish (the wet look).
- **Ambient occlusion:** baked per vertex by the builders, and by the Blender AO bake for fish. SSAO is not used: in this pipeline it forces a DepthNormals prepass, which changes where the R73 water gets its depth texture (CopyDepth). The test `RenderingSetup_NoSsao_ToonHasNoOcclusionKeyword` keeps it out.
- **Outlines:** none. Both references go without them, and the rim plus value contrast already separates forms.
- **Normal maps:** optional, behind `_NORMALMAP` (`shader_feature_local_fragment`, `_FGNormalArray` at the same uv/layer, uv0 mapping only, needs tangents). No material uses it yet. The painted albedo carries the detail.
- **SRP Batcher:** one `UnityPerMaterial` CBUFFER holds every property, and the textures sit outside it. The shader validator compiles the `_NORMALMAP` variant (keyword set 1).

### 2.2 Textures: what, how big, where
- **Two Texture2DArrays.** Each is imported from one flipbook PNG of 512 x 512 tiles by `TextureArrayPostprocessor`: Repeat, Trilinear, aniso 4, CompressedHQ (BC7 on PC), mipmaps, max size 2048.
  - `Content/Textures/T_EnvDetailArray.png`: 4 x 4 tiles, linear. It holds detail maps centred on 0.5 and is multiplied by `_FGArrayGain 2`, so 0.5 means no change. Hue comes from the palette slot or `_BaseColor`, which keeps the M3 season swap and the "slots differ in lightness" rule working. Layers: 0 neutral, 1 grass, 2 foliage, 3 wood (grain along u), 4 bark (ridges along v), 5 sand, 6 rock, 7 cloth, 8 player face decal, 9 to 15 reserved (neutral).
  - `Content/Textures/T_FishSkinArray.png`: 4 x 2 tiles, sRGB, full-colour albedo at gain 1. Layer 0 is white and layer 1 is the bluegill.
- **Layer selection:** the layer is `round(uv1.y)` and the mapping is `uv1.z`, where 0 is uv0 and 1 is world triplanar. A mesh without uv1.y reads layer 0 and renders untextured: the reeds, the rod and the four other M2 fish.
- **Texel density:**
  - Environment: `_FGTiling 0.6` repeats per metre, so one 512 tile covers 1.67 m, about 300 px/m. A macro breakup (`_FGMacro 0.7`, the same layer at 1/11 scale in world xz) hides the repeat on the shore and lakebed.
  - Cloth: `_FGTiling 2.5`.
  - Fish skin: 512 px over about 0.3 m, roughly 1,500 px/m on the body, sized for the reveal close-up.
- **Storage:** the PNGs are Git LFS through the existing `*.png lfs` rule in `.gitattributes`. The generator lives in `Art/Blender/fishgame_lookdev.blend`, text block `fg_textures.py`. It bakes procedural nodes on a 4D torus mapping, so every tile wraps seamlessly. Regenerate by running it in Blender; never hand-edit the PNG.

### 2.3 Palette rules
- Keep `ArtPaletteDef` as the single source of hue for the environment, so seasons still swap. Painted env textures are value and texture, low chroma, with a mean of about 0.5.
- **Albedo value range:** keep lit albedo between about 0.15 and 0.85 linear. Reserve near-white for foam and highlights, and near-black for pupils and eye-like details.
- Fish, characters and props that never change with the season may carry full-colour painted albedo (fish array, gain 1).
- Gameplay meaning is never carried by texture alone (spec 10.2 still applies).

### 2.4 Model smoothing rules
- **No faceting.** Smooth (shared or analytic) normals everywhere. `MeshBuilder` (flat) remains only for legacy props until restyled.
- **Bevels:** every box-like prop is a `SmoothMeshBuilder.RoundedBox`. Use a radius of about 15 to 20% of the smallest dimension for planks (0.02 m on a 0.1 m plank) and up to 85% for soft character shapes (body box 0.42). Use `bandSteps 1` for props seen at range and `bandSteps 2` for close-ups.
- **Round props:** posts and trunks use `Lathe` (12 to 16 sides, profile-based normals, root flare and taper). Blobs (rocks, canopies, heads) use `Radial` (cube sphere, res 6 to 8, finite-difference normals that agree across seams).
- **Blender models:** subdivision surface level 1 over a low cage, plus solidified fins (3.5 to 4.5 mm). Budget about 4k triangles per species: the look-dev bluegill is 3,876, and the test budget is under 5,000.
- **Colliders never change for a look change.** Visual twins are allowed (lakebed and shore). Check with `LookDevCapture`'s collider dump.

### 2.5 Recipes per asset type
- **Dock and wood props:** rounded boxes, grain along the long axis (wood layer 3, uv0 in metres), per-plank palette slot (Wood or Dock), ao 0.45 toward the bottom face. Posts are lathes with grain running up (`swapUv`), darker toward the bed and just under the deck.
- **Ground (shore, lakebed, banks):** the collider mesh stays as built. The visual twin uses the same positions with shared vertices, slope normals, uv0 = world xz, the sand or grass layer per band, and macro breakup.
- **Rocks:** `Radial` with two noise octaves plus a rounded-box bias (soft planes) and a flattened base. Rock layer 6, triplanar. ao from 0.62 at the bottom to 1.0 at the top.
- **Trees:** the trunk is a lathe with root flare and the bark layer along v, ao 0.6 at the foot. Round canopies are lumpy `Radial` shapes. Pines are two tiered lathes with a closed rounded lip. Both use the foliage layer (triplanar), with ao of about 0.55 underneath. All of it stays inside the canopy collider sphere.
- **Characters:** pillowy rounded-box torso, smooth capsules (arms and legs overlap the torso and shoulders, so there are no gaps), sphere head with a face decal (layer 8 on the head's +Z cube face: dot eyes, glint, smile), cloth layer on clothes and no texture on skin.
- **Fish:**
  - **Model:** a Blender loft of superellipse rings plus subdivision, fins as solidified plates, and big comic eyes (white sclera, black pupil, glint).
  - **UVs:** UV0 is a side projection per part into fixed boxes. Eyes go to the top strip of the tile (white left, black right). UV "Layer" = (0, species layer).
  - **Skin:** painted procedurally in object space (back-to-belly gradient, bars, cheek, ear flap, scales, mouth) and baked to the species tile.
  - **Vertex colour:** baked AO.
  - **Export:** one submesh, no materials, `bake_space_transform` on. Script: `fg_bluegill.py` + `fg_bluegill_paint.py`.

## 3. What the spike changed (summary)
- **Shader:** `FishGame/Toon` extension, `TextureArrayPostprocessor`, `RenderMaterialLibrary` `ToonLook` presets, and the validator keyword.
- **Meshes:** `SmoothMeshBuilder` and `EnvTextureLayers` (Runtime).
- **Lake dock:** smooth dock, rock, trunk and canopies; lakebed and shore visual twins.
- **Player:** smooth body meshes, face decal, and limb mesh proportions. The pivots are unchanged.
- **Fish:** the bluegill FBX is replaced, in both `Art/Export` and `Content/Models`. The other four species still use the M2 low-poly models with layer 0.
- **Not changed:**
  - **Water:** not changed. R73 water code and colours are untouched. With the new look it still reads well.
  - **Fog:** not changed. Day density stays 0.008.
- **Recommended follow-ups:** a slightly greener, less neon shallow colour, and day fog of about 0.012 for RV-style haze. Both need a seed-data and test edit, so they are left to the user.
