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
