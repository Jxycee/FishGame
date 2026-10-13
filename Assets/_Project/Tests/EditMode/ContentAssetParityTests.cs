// Committed content assets against their sources (M2 pre-acceptance polish): the cue table asset against FishingSeedData.CueRows (T13/T14 review leftover),
// and the five seed species' models (T21 review leftover). Run Tools/unity.ps1 build first if the assets were never generated.
using System;
using System.Collections.Generic;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class ContentAssetParityTests
    {
        // The budget for one species model. R74 (realistic toon, look-dev 2026-10-12): smooth subdivided fish with big eyes; the look-dev bluegill is
        // ~3.9k triangles. Was 400 under the low-poly look.
        const int MaxFishModelTriangles = 5000;

        // Catches: the committed Content/Audio/SfxCueTable.asset drifting from the seed rows (a retuned recipe or a hand-edited asset that the
        // builder's create-only rule would never refresh), a missing or extra row, a changed field. Rows are matched by id, not by position.
        [Test]
        public void SfxCueTableAsset_RowsEqualSeedCueRows_ByIdAndFields()
        {
            var table = AssetDatabase.LoadAssetAtPath<SfxCueTableDef>(AssetPaths.SfxCueTableAsset);
            Assert.IsNotNull(table, "missing " + AssetPaths.SfxCueTableAsset + " (run Tools/unity.ps1 build)");

            List<SfxCueRow> seeds = FishingSeedData.CueRows();
            Assert.That(seeds.Count, Is.GreaterThan(0), "no seed cue rows");
            Assert.That(table.Rows.Count, Is.EqualTo(seeds.Count), "cue table row count vs FishingSeedData.CueRows");

            var byId = new Dictionary<string, SfxCueRow>(StringComparer.Ordinal);
            foreach (SfxCueRow row in table.Rows)
                Assert.That(byId.TryAdd(row.id, row), Is.True, "duplicate cue id in the asset: " + row.id);

            foreach (SfxCueRow seed in seeds)
            {
                Assert.That(byId.TryGetValue(seed.id, out SfxCueRow asset), Is.True, "the asset has no row for seed cue " + seed.id);
                string l = "cue " + seed.id + " ";
                Assert.That(asset.category, Is.EqualTo(seed.category), l + "category");
                Assert.That(asset.baseVolume, Is.EqualTo(seed.baseVolume), l + "baseVolume");
                Assert.That(asset.minDistance, Is.EqualTo(seed.minDistance), l + "minDistance");
                Assert.That(asset.maxDistance, Is.EqualTo(seed.maxDistance), l + "maxDistance");
                Assert.That(asset.minInterval, Is.EqualTo(seed.minInterval), l + "minInterval");
                Assert.That(asset.spatial, Is.EqualTo(seed.spatial), l + "spatial");
            }
        }

        // Catches: a species whose Model was never filled (the fish would render the fallback mesh), a model without vertex colours or with several
        // submeshes (IsFishModelMesh), or a model over the triangle budget.
        [Test]
        public void SeedSpecies_Models_AreFishModelMeshes_UnderTriangleBudget()
        {
            Assert.That(FishingSeedData.Species.Count, Is.EqualTo(5), "the five M2 seed species");
            foreach (SpeciesSeed seed in FishingSeedData.Species)
            {
                string path = AssetPaths.SpeciesFolder + "/" + seed.Id + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                Assert.IsNotNull(def, "missing species asset " + path);
                Assert.IsNotNull(def.Model, seed.Id + ": Model is null");
                Assert.That(FishingContentBuilder.IsFishModelMesh(def.Model), Is.True, seed.Id + ": Model needs vertex colours and exactly one submesh");

                long triangles = 0;
                for (int s = 0; s < def.Model.subMeshCount; s++) triangles += def.Model.GetIndexCount(s) / 3;
                Assert.That(triangles, Is.LessThan(MaxFishModelTriangles), seed.Id + ": triangle count");
                Assert.That(triangles, Is.GreaterThan(0), seed.Id + ": triangle count");
            }
        }

        // Catches: the model importer regenerating or swapping UV channels (FishModelPostprocessor pins generateSecondaryUV / swapUVChannels off), which would
        // replace the authored uv1 = (slot, layer, mapping, 0) and make FishGame/Toon read the wrong T_FishSkinArray layer (the fish would be skinned as another
        // species, or white). Each species model must carry uv1, and uv1.y is its skin layer on every vertex.
        [Test]
        public void SeedSpecies_Models_CarryUv1_WithTheirSkinArrayLayer()
        {
            var expectedLayer = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                { "bluegill", 1f }, { "perch", 2f }, { "carp", 3f }, { "trout", 4f }, { "catfish", 5f },
            };
            Assert.That(FishingSeedData.Species.Count, Is.EqualTo(expectedLayer.Count), "every seed species needs an expected skin layer here");
            foreach (SpeciesSeed seed in FishingSeedData.Species)
            {
                Assert.That(expectedLayer.TryGetValue(seed.Id, out float layer), Is.True, seed.Id + ": no expected skin layer in this test");
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetPaths.SpeciesFolder + "/" + seed.Id + ".asset");
                Assert.IsNotNull(def, seed.Id + ": missing species asset");
                Mesh mesh = def.Model;
                Assert.IsNotNull(mesh, seed.Id + ": Model is null");
                Assert.That(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1), Is.True, seed.Id + ": the model has no uv1 (TexCoord1)");

                var uv1 = new List<Vector4>();
                mesh.GetUVs(1, uv1);
                Assert.That(uv1.Count, Is.EqualTo(mesh.vertexCount), seed.Id + ": uv1 count vs vertex count");
                for (int i = 0; i < uv1.Count; i++)
                    Assert.That(uv1[i].y, Is.EqualTo(layer), seed.Id + ": uv1.y (skin layer) at vertex " + i);
            }
        }
    }
}
