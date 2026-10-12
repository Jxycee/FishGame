// T20: the Toon materials, the MeshBuilder body meshes on PlayerFP and the fish model import settings (plan T20 tests, ruling F-Q6).
// Reads the persisted assets built by `build`; nothing is written. Each test names the wrong build it catches in its comment.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.EditorTools;
using FishGame.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Tests.EditMode
{
    public class ToonMaterialTests
    {
        const string ToonShaderName = "FishGame/Toon";
        static readonly int JitterId = Shader.PropertyToID("_JitterEncoded");
        static readonly int PaletteId = Shader.PropertyToID("_UsePalette");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static Material Load(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
            Assert.That(material, Is.Not.Null, "material missing: " + name);
            return material;
        }

        static void AssertToon(Material material, float usePalette, float jitterEncoded)
        {
            Assert.That(material.shader.name, Is.EqualTo(ToonShaderName), material.name + " shader");
            Assert.That(material.GetFloat(PaletteId), Is.EqualTo(usePalette), material.name + " _UsePalette");
            Assert.That(material.GetFloat(JitterId), Is.EqualTo(jitterEncoded), material.name + " _JitterEncoded");
        }

        // Catches: a wrong _JitterEncoded (props too dark/bright by 2x, fish doubled), or the fish materials in palette mode.
        [Test]
        public void ToonMaterials_BaseAndFish_UseToonShaderAndColourModes()
        {
            Material toonBase = Load("M_Toon_Base");
            AssertToon(toonBase, 1f, 1f);
            Assert.That(toonBase.GetColor(BaseColorId), Is.EqualTo(Color.white), "M_Toon_Base _BaseColor");

            Material fish = Load("M_Toon_Fish");
            AssertToon(fish, 0f, 0f);
            Assert.That(fish.GetColor(BaseColorId), Is.EqualTo(Color.white), "M_Toon_Fish _BaseColor");

            Material fallback = Load("M_Toon_FishFallback");
            AssertToon(fallback, 0f, 1f);
            Assert.That(fallback.GetColor(BaseColorId), Is.EqualTo(Color.white), "M_Toon_FishFallback _BaseColor");

            int toonCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { AssetPaths.MaterialsFolder }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null || material.shader.name != ToonShaderName) continue;
                toonCount++;
                float expected = material.name == "M_Toon_Fish" ? 0f : 1f;
                Assert.That(material.GetFloat(JitterId), Is.EqualTo(expected), material.name + " _JitterEncoded (BF-7)");
            }
            Assert.That(toonCount, Is.GreaterThanOrEqualTo(8), "M_Toon_Base, Fish, FishFallback, four body slots and M_Skin");
        }

        // Catches: a body slot left on Lit (ping-pong with MaterialLibrary), a lost slot colour, or a missing x2 for the half-scale grey.
        [Test]
        public void ToonMaterials_BodySlotsAndSkin_UseToonWithSlotPaletteColours()
        {
            for (int i = 0; i < SlotPalette.Count; i++)
            {
                Material material = Load("M_Body_Slot" + i);
                AssertToon(material, 0f, 1f);
                SlotPalette.GetRgb(i, out float r, out float g, out float b);
                AssertColour(material.GetColor(BaseColorId), r, g, b, material.name);
            }

            Material skin = Load("M_Skin");
            AssertToon(skin, 0f, 1f);
            SlotPalette.GetSkinRgb(out float sr, out float sg, out float sb);
            AssertColour(skin.GetColor(BaseColorId), sr, sg, sb, "M_Skin");
        }

        static void AssertColour(Color actual, float r, float g, float b, string label)
        {
            Assert.That(actual.r, Is.EqualTo(r).Within(1e-4f), label + " r");
            Assert.That(actual.g, Is.EqualTo(g).Within(1e-4f), label + " g");
            Assert.That(actual.b, Is.EqualTo(b).Within(1e-4f), label + " b");
        }

        // Catches: body parts still on Unity primitive meshes (no vertex colours, not flat-shaded), or M1 sizes lost.
        [Test]
        public void PlayerPrefab_BodyMeshes_AreUnitMeshesWithVertexColours()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths.PlayerPrefab);
            Assert.That(prefab, Is.Not.Null);

            var expected = new Dictionary<string, string>
            {
                { PlayerRigPaths.Head, AssetPaths.BodyUnitSphereMesh },
                { PlayerRigPaths.Nose, AssetPaths.BodyUnitBoxMesh },
                { PlayerRigPaths.Torso, AssetPaths.BodyUnitBoxMesh },
                { PlayerRigPaths.ArmLMesh, AssetPaths.BodyUnitCapsuleMesh },
                { PlayerRigPaths.ArmRMesh, AssetPaths.BodyUnitCapsuleMesh },
                { PlayerRigPaths.LegLMesh, AssetPaths.BodyUnitCapsuleMesh },
                { PlayerRigPaths.LegRMesh, AssetPaths.BodyUnitCapsuleMesh },
            };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Transform part = prefab.transform.Find(pair.Key);
                Assert.That(part, Is.Not.Null, pair.Key);
                Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null, pair.Key + " mesh");
                Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(pair.Value), pair.Key + " mesh asset");
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.Color), Is.True, pair.Key + " vertex colours");
            }

            Vector3 head = prefab.transform.Find(PlayerRigPaths.Head).localScale;
            Vector3 torso = prefab.transform.Find(PlayerRigPaths.Torso).localScale;
            Assert.That(Vector3.Distance(head, new Vector3(0.28f, 0.28f, 0.28f)), Is.LessThan(1e-5f), "Head scale");
            Assert.That(Vector3.Distance(torso, new Vector3(0.42f, 0.55f, 0.24f)), Is.LessThan(1e-5f), "Torso scale");
        }

        // Catches: Unity re-importing the model materials (leftover Blender materials), or a changed postprocessor not reimporting.
        [Test]
        public void FishModels_ImportSettings_MatchPostprocessor()
        {
            int models = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AssetPaths.ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                models++;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None), path + " materialImportMode");
                Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import), path + " importNormals");
                Assert.That(importer.importAnimation, Is.False, path + " importAnimation");
                Assert.That(importer.importCameras, Is.False, path + " importCameras");
                Assert.That(importer.importLights, Is.False, path + " importLights");
                Assert.That(importer.globalScale, Is.EqualTo(1f), path + " globalScale");
                Assert.That(importer.useFileScale, Is.True, path + " useFileScale");
                Assert.That(importer.isReadable, Is.False, path + " isReadable");
                Assert.That(importer.meshCompression, Is.EqualTo(ModelImporterMeshCompression.Off), path + " meshCompression");
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None), path + " animationType");
            }
            Assert.That(models, Is.GreaterThan(0), "no .fbx under " + AssetPaths.ModelsFolder);
        }
    }
}
