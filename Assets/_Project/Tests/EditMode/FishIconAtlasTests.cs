// Authority: none (EditMode, reads committed assets). The baked journal icon atlas (Content/Textures/T_FishIcons.png, written by
// Editor/Dev/FishIconBaker with a graphics device): every registry species has a tile, the atlas has the expected size, the tiles are inside it
// and do not overlap, and each tile holds a model filling its width or height. Run the baker (see FishIconBaker) after species or models change.
using System;
using System.Collections.Generic;
using System.IO;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class FishIconAtlasTests
    {
        private static ArtPaletteDef Palette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            Assert.That(palette, Is.Not.Null, "missing " + AssetPaths.ArtPaletteAsset + " (run Tools/unity.ps1 build)");
            return palette;
        }

        // Catches: a species added or renamed without re-baking (no tile, the journal silently falling back to the silhouette), an atlas that was
        // never baked or assigned, a wrong size (rows = ceil(species / 5), 256 x 128 tiles), and tiles that leave the atlas or overlap.
        [Test]
        public void FishIconAtlas_EverySpeciesHasATile_InsideAnAtlasOfTheExpectedSize()
        {
            ArtPaletteDef palette = Palette();
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(registry, Is.Not.Null, "ContentRegistry asset");
            Texture2D atlas = palette.FishIconAtlas;
            Assert.That(atlas, Is.Not.Null, "ArtPalette.fishIconAtlas is not assigned: run FishGame > Bake Fish Icons and commit T_FishIcons.png + ArtPalette.asset");
            Assert.That(AssetDatabase.GetAssetPath(atlas), Is.EqualTo(AssetPaths.FishIconAtlas));

            int count = registry.Species.Count;
            int rows = (count + FishIconBaker.Columns - 1) / FishIconBaker.Columns;
            Assert.That(atlas.width, Is.EqualTo(FishIconBaker.Columns * FishIconBaker.TileWidth), "atlas width");
            Assert.That(atlas.height, Is.EqualTo(rows * FishIconBaker.TileHeight), "atlas height for " + count + " species");

            var seen = new List<FishIconTile>();
            for (int i = 0; i < count; i++)
            {
                string id = registry.SpeciesAt((ushort)i).Id;
                Assert.That(palette.TryGetFishIconTile(id, out FishIconTile tile), Is.True, "no icon tile for species " + id);
                Assert.That(tile.w, Is.EqualTo(FishIconBaker.TileWidth), id + " tile width");
                Assert.That(tile.h, Is.EqualTo(FishIconBaker.TileHeight), id + " tile height");
                Assert.That(tile.x, Is.InRange(0, atlas.width - tile.w), id + " tile x inside the atlas");
                Assert.That(tile.y, Is.InRange(0, atlas.height - tile.h), id + " tile y inside the atlas");
                foreach (FishIconTile other in seen)
                {
                    bool overlap = tile.x < other.x + other.w && other.x < tile.x + tile.w && tile.y < other.y + other.h && other.y < tile.y + tile.h;
                    Assert.That(overlap, Is.False, id + " overlaps " + other.id);
                }
                seen.Add(tile);
            }
            Assert.That(palette.FishIconTiles.Count, Is.EqualTo(count), "no stale tiles for removed species");
        }

        // Catches: a blank or half-baked tile (no coverage), a model that does not fill its tile (a wrong framing: the fit is by the projected
        // vertices, so the model must span about 90% of the tile width or height), and an atlas without transparency (a solid background).
        [Test]
        public void FishIconAtlas_EveryTile_HoldsATransparentBackedModelFillingItsTile()
        {
            ArtPaletteDef palette = Palette();
            Assert.That(palette.FishIconAtlas, Is.Not.Null, "atlas not assigned");
            string disk = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPaths.FishIconAtlas));
            Assert.That(File.Exists(disk), Is.True, disk);

            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(pixels, File.ReadAllBytes(disk)), Is.True, "the atlas PNG decodes");
                Color32[] all = pixels.GetPixels32();
                int w = pixels.width;
                foreach (FishIconTile tile in palette.FishIconTiles)
                {
                    int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1, covered = 0;
                    for (int y = 0; y < tile.h; y++)
                    {
                        for (int x = 0; x < tile.w; x++)
                        {
                            if (all[(tile.y + y) * w + tile.x + x].a < 128) continue;
                            covered++;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                    int total = tile.w * tile.h;
                    Assert.That(covered, Is.GreaterThan(total / 40), tile.id + " has a model in its tile (covered " + covered + " of " + total + ")");
                    Assert.That(covered, Is.LessThan(total * 9 / 10), tile.id + " has a transparent background (covered " + covered + " of " + total + ")");
                    float spanX = (maxX - minX + 1) / (float)tile.w, spanY = (maxY - minY + 1) / (float)tile.h;
                    Assert.That(Mathf.Max(spanX, spanY), Is.GreaterThan(0.88f), tile.id + " fills its tile (span " + spanX.ToString("0.00") + " x " + spanY.ToString("0.00") + ")");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
