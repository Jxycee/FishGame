# FishGenome art (branch `fish-genome`, 2026-10-17)

Art side of fish genetics (design 3.5): every fish look = genome -> shader parameters + blendshapes on the existing species mesh. Blender only; no C#, scenes or Unity meta files. Existing `Art/Export/Fish_*.fbx` and `T_FishSkinArray_m3.png` are untouched.

## 1. How the existing fish are encoded (UV layout convention)
- 25 species = `Art/Export/Fish_<Name>.fbx`; authoring meshes are `fishgame_m3_fish.blend` (`FG_Fish_<Name>`, 24 species) and `fishgame_lookdev.blend` (`Fish_Bluegill_LD`). `fishgame_m2_species.blend` holds only the superseded M2 low-poly fish. Generator source (`fg_fish.py`, `fg_textures.py`) lives only as text blocks in the lookdev blend; this branch needed only the meshes. Each fish is ONE mesh, 3.8-4.9k tris, vertex colour `Col` = 0.55 + 0.45 AO, no materials exported (Unity: `M_Toon_Fish`), nose at -Y in Blender, 0.31-0.50 m long.
- **UV0 "UVMap"** = the species skin tile (512 x 512), side projection of each part into a fixed box (v = 1 at the top): body box (0.01, 0.01)-(0.70, 0.72) at the bottom-left, uniformly scaled, nose at the left, belly at v = 0.01 (so the dorsal edge is at a species-dependent v); tail box (0.72, 0.01)-(0.99, 0.45); six boxes above/right for pectorals, pelvics, dorsal, anal, barbels; **eyes and pupils/glints map into the top strip v >= 0.93**: white texel at u 0.25, black texel at u 0.75 (both v 0.965); the strip also holds per-species swatch colours. Tile hole fill = (0.45, 0.45, 0.35) in linear, i.e. a dark olive in the stored bytes.
- **UV1 "Layer"** = (0, skin layer) for every vertex; `T_FishSkinArray_m3.png` is 4 columns x 8 rows of 512 tiles, layer = row * 4 + column, row 0 on top: 0 white, 1 Bluegill, 2 Perch, 3 Carp, 4 Trout, 5 Catfish, 6.. in `skins_batch1-4` order (MirrorMinnow 6 ... Lanternfish 25; the `FishSkin_Lxx_<Name>.png` files are the same tiles). Because UV0's body box is scaled per species (body height/length 0.11 eel .. 1.17 ray), a mask painted in UV0 cannot be shared: hence the third UV set below.

## 2. What was built
| Item | Where |
|---|---|
| 25 FBX with 4 blendshapes + UV set 3 | `Art/Export/FishGenome/Fish_<Name>.fbx`, `fishgenome_manifest.json`, `fishgenome_meshes.json` |
| 8 pattern masks + packed atlas | `Art/Textures/FishGenome/PatternMask_<i>_<Name>.png`, `T_FishPatternArray.png`, `SOURCES.md` |
| 50 colour-morph tiles + sheet | `Art/Export/FishGenome/Skins/<Species>_Morph<N>.png`, `T_FishMorphArray.png`, `fishgenome_morphs.json` |
| Previews (1920x1080, Eevee) | `Art/Previews/FishGenome/1_shapes_x_patterns.png`, `2_traits.png`, `3_morph1.png`, `4_morph2.png` |
| Scripts | `Art/Blender/fg_genome_common.py`, `fg_fishgenome.py` (Blender), `fg_fishgenome_masks.py`, `fg_fishgenome_morphs.py`, `fg_fishgenome_manifest.py` (python3 + numpy, Pillow optional), `fg_fishgenome_preview.py` (Blender) |

Triangle counts are unchanged from the committed models (3,796 MirrorMinnow .. 4,890 OldBarnacle, 25 values in the manifest). Topology, UV0, UV1, `Col`, pivot, axes, scale and FBX settings equal the committed files (verified by re-importing: vertex positions, UV0 and `Col` identical for 7 sampled species).

### Blendshapes (Basis + 4, Unity weights 0..100)
- `Fat` (belly sags, back grows 35% less, length x0.96), `Long` (length x1.34, girth -14%), `Stubby` (length x0.76, girth +4%, +12% bulge), `BigFin` (fin plates x1.55 about their base). Fin plates, eyes, barbels, spikes, barnacles and lures move with the body surface they sit on and keep their size; fins stretch 60% as much as the body. `Long` and `Stubby` are opposites: use one. `Fat` + `BigFin` and `Long` + `BigFin` look good (previews 1, 2).
- Per-key numeric checks (in the manifest `checks`): eye depth in the body surface changes < 2 mm, fin plates stay attached (gap change < 3 mm), no eye flips side, on all 25 species at weight 1.0.
- **Giant check**: Giant is a uniform x1.6 scale in Unity. Geometry is scale-invariant, and the worst combined case (Fat + BigFin) is stable (checks above); fin plates are 3.5-5 mm thick at x1, so nothing becomes hairline. Remember to scale the renderer bounds (see below).

### Third UV set "Pat" (Unity uv3 / `TEXCOORD2` / `Mesh.uv3`)
One species-independent frame so one mask atlas fits every mesh: body length (nose to end of the body island) maps to u 0.02..0.70, isotropic, body axis at v 0.50 (u = 0.02 + (y - nose)/len * 0.68, v = 0.5 + (z - axisZ)/len * 0.68, clamped to 0.005..0.93). Tail and fins use the same frame, so a pattern continues over them. **Eyes (white, pupil, glint) map to (0.975, 0.975), a texel every mask keeps at 0**: patterns never touch eyes, and `uv3.x > 0.95` is the shader's "is eye" flag (albino red eye, glow, ghost). All 25 species are side-projected (rays and the flounder are flat in X, so the side view is their dorsal outline).

### Pattern masks (greyscale, linear, ink 1 = pattern colour)
Index: 0 Stripes (horizontal lines, slightly broken), 1 Bars (7 vertical bars, fade out on the lower flank), 2 Spots, 3 Rosettes (irregular rings, some with a speck), 4 Marbled (thin veins), 5 Mottled (soft blotches), 6 Saddles (6 dorsal arches, one on the tail stalk), 7 TwoTone (dorsal half = ink, soft lateral-line edge). All are procedural (`fg_fishgenome_masks.py`); no CC0 photo source was needed, so SOURCES.md lists none. Atlas `T_FishPatternArray.png`: 2048 x 1024, 4 columns x 2 rows of 512, layer = row * 4 + column (same flipbook convention as the skin array).

### Colour morphs
Two per species, recolours of the committed tile in OKLab (painted detail, stripes, scales, fin rays kept; the top 36 rows = eye strip and swatches are copied untouched). Morph 1 = colour swap away from the natural hue (Sunburst, Ember, Crimson, Violet, Cobalt, Teal, Lime), morph 2 = special tone (Ink, Pearl, Koi, Dusk, Candy). Recipes per species in `fishgenome_morphs.json`. Sheet `T_FishMorphArray.png`: 4 columns x 13 rows, layer = (skinLayer - 1) * 2 + (N - 1). Intentionally not named albino/golden/melanistic (those are shader traits).

## 3. Shader contract (Unity side)
Material per fish (instances, not MaterialPropertyBlock, per the spec). Existing inputs stay: skin array index from UV1 (or `_MorphIndex` below), UV0 tile, vertex colour AO.

| Property | Type / range | Meaning |
|---|---|---|
| `_MorphLayer` | int, -1 = none, 0..49 | sample `T_FishMorphArray` (layer formula above) instead of the species tile; or append the 50 tiles as rows 8+ to `T_FishSkinArray` and write the index into `Layer` (Unity's call) |
| `_Hue` | float degrees -180..180 (natural 0 +-40; rare mutations to +-180) | hue rotation of the skin colour (HSV hue node equivalent); applied before the pattern |
| `_Sat` | float 0..2, default 1 (natural 0.7..1.3) | saturation multiplier |
| `_PatIndex` | int 0..7 | layer of `T_FishPatternArray` (linear, R channel) |
| `_PatStrength` | float 0..1 (0 = no pattern; natural 0.4..0.95) | mix factor: colour = lerp(skin, `_PatColor`, mask * strength) |
| `_PatColor` | RGB, sRGB | pattern ink colour; suggested: dark (L 0.05..0.3) for natural fish, bright accents for rare ones |
| `_Traits` | int bit flags | 1 albino, 2 golden, 4 melanistic, 8 glowing, 16 ghost, 32 two-tone |
| shape weights | `SetBlendShapeWeight` 0..100 | `Fat`, `Long`, `BigFin`, `Stubby` (see above) |
| Giant | uniform transform scale 1.6 | not a shader property |

Sampling: `skin = SAMPLE(skinArray, uv0, layer)`; `mask = SAMPLE(patArray, uv3 (Pat), _PatIndex).r`; `isEye = step(0.95, uv3.x)`. UV3 must be clamped (no wrap) on the mask sampler. Order: hue/sat -> pattern -> traits -> AO multiply. Trait recipes (as faked in preview 2; tune freely):
- **albino**: colour = lerp(c, float3(p, 0.88p, 0.86p), 0.92) with p = luma(c) * 0.45 + 0.62; eyes (isEye and luma > 0.35) -> (0.85, 0.05, 0.10).
- **golden**: g = luma(c) * 1.3 + 0.3; colour = lerp(c, float3(g, 0.70g, 0.16g), 0.88 on non-eye); metallic ~0.65, smoothness up.
- **melanistic**: colour = lerp(c * 0.14, (0.01, 0.012, 0.025), 0.45), slightly glossy.
- **glowing**: colour = c * 0.55; emission = (0.10, 1.0, 0.75) * 5 * max(mask * strength, isEye); pick mask 2 or 3 for the spots look, 0 for stripes.
- **ghost**: colour = lerp(c, (0.72, 0.90, 1.0), 0.6); alpha = 0.30 + 0.6 * fresnel(ior 1.6); transparent pass (or dithered).
- **two-tone**: force `_PatIndex` = 7, strength ~0.85, `_PatColor` = the dorsal colour (preview used (0.12, 0.10, 0.22)); combinable with glowing; exclusive of albino/golden/melanistic/ghost (colour traits are mutually exclusive, glowing and two-tone stack).
Previews: `2_traits.png`.

## 4. Unity steps for the PC
1. Import the 25 FBX from `Art/Export/FishGenome/` (do not overwrite the old ones until switched): Import BlendShapes on, Mesh Compression off, **Normals: Import** (blendshape normals), keep UV sets; the fish need a `SkinnedMeshRenderer` (no bones: blendshapes only) instead of `MeshRenderer`; set `localBounds` ~1.6x the base bounds (Long +34% length, BigFin fins x1.55) times the Giant scale 1.6.
2. Import `T_FishPatternArray.png` through `TextureArrayPostprocessor` as a Texture2DArray flipbook, sRGB OFF, 4 x 2 tiles of 512, bilinear, clamp. Import `T_FishMorphArray.png` the same way (sRGB ON, 4 x 13) or append its tiles to the skin sheet.
3. Extend `FishGame/Toon` fish variant per section 3 (read UV3, array, traits). `M_Toon_Fish` keeps working on the old meshes.
4. EditMode tests expected to change: triangle-count / UV-set tests if any iterate `Fish_*.fbx` (the new files have 3 UV sets and 5 blend shapes; tris unchanged).

## 5. Unverified / open
- Nothing was imported in Unity: blendshape normals import, SkinnedMeshRenderer behaviour and the shader are unverified. Previews are a Blender fake of the contract, not the Toon shader.
- FBX bytes come from Blender 5.2.2 (cloud). Re-running `fg_fishgenome.py` on the PC may rewrite them (only mesh content changes are skipped via a hash); harmless.
- Eyes of Flounder/StormRay (4 eyes on the ray) are treated per sphere; the ray's `Fat` key thickens the whole disc (7 cm at weight 100), consider capping Fat at 60 for rays.
- Morph recolours leave the swatch strip (top 36 rows) as in the base tile; if a tool reads those swatches for UI, recolour them on the PC.
- The Unity asset-store packs are not reachable from the cloud; none were needed here.
