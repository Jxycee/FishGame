// Project-level invariants from spec 4.7, checked in EditMode against persisted state. Owned by T15.
using System.Collections.Generic;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class ValidationTests
    {
        const string TempMissingMeshPrefab = "Assets/_Project/Prefabs/__TestMissingMesh.prefab";
        const string TempToonPrefab = "Assets/_Project/Prefabs/__TestToonMesh.prefab";
        const string TempFolder = "Assets/_Project/Tests/__Temp";   // gitignored
        const string TempToonMaterial = TempFolder + "/__TestToonJitter.mat";
        GameObject tempObject;

        // A crashed earlier run may have left its temp assets behind; they would be counted by the validators.
        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempMissingMeshPrefab);
            AssetDatabase.DeleteAsset(TempToonPrefab);
            AssetDatabase.DeleteAsset(TempToonMaterial);
            BuilderUtil.EnsureFolder(TempFolder);
        }

        [TearDown]
        public void TearDown()
        {
            if (tempObject != null) Object.DestroyImmediate(tempObject);
            tempObject = null;
            AssetDatabase.DeleteAsset(TempMissingMeshPrefab);
            AssetDatabase.DeleteAsset(TempToonPrefab);
            AssetDatabase.DeleteAsset(TempToonMaterial);
        }

        static void AssertNoErrors(List<string> errors)
        {
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test] public void Validation_ContentIds_Pass() => AssertNoErrors(Validators.ContentIds());
        [Test] public void Validation_NetworkPrefabs_Pass() => AssertNoErrors(Validators.NetworkPrefabs());
        [Test] public void Validation_BuildScenes_BootstrapFirst() => AssertNoErrors(Validators.BuildScenes());
        [Test] public void Validation_InputActions_Pass() => AssertNoErrors(Validators.InputActions());
        [Test] public void Validation_Manifest_HasPinsAndNoForbidden() => AssertNoErrors(Validators.Manifest());
        [Test] public void Validation_ProjectSettings_Pass() => AssertNoErrors(Validators.ProjectSettingsInvariants());
        [Test] public void Validation_AssetsLayout_OnlyProjectAndSettings() => AssertNoErrors(Validators.AssetsLayout());
        [Test] public void Validation_MeshReferences_Pass() => AssertNoErrors(Validators.MeshReferences());
        [Test]
        public void Validation_MeshReferences_ReportsMissingMesh()
        {
            tempObject = new GameObject("__TestMissingMesh");
            tempObject.AddComponent<MeshFilter>().sharedMesh = null;
            PrefabUtility.SaveAsPrefabAsset(tempObject, TempMissingMeshPrefab);

            List<string> errors = Validators.MeshReferences();

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            StringAssert.Contains(TempMissingMeshPrefab, errors[0]);
        }

        [Test] public void Validation_MissingScripts_Pass() => AssertNoErrors(Validators.MissingScripts());
        [Test] public void Validation_BootstrapReferences_Pass() => AssertNoErrors(Validators.BootstrapReferences());
        [Test] public void Validation_FishingContent_Pass() => AssertNoErrors(FishingValidators.FishingContent());
        [Test] public void Validation_SpeciesModels_Pass() => AssertNoErrors(FishingValidators.SpeciesModels());

        [Test]
        public void Validation_Shaders_Pass()
        {
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try { AssertNoErrors(ShaderValidator.Run()); }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
        }

        // Catches: a Toon shader without a DepthNormals pass. URP's DepthNormals prepass also writes _CameraDepthTexture, so without it Toon geometry
        // can be missing from the depth texture and FishGame/Water would lose its depth fade over it.
        [Test]
        public void Validation_ToonShader_HasDepthNormalsPass()
        {
            Shader toon = Shader.Find("FishGame/Toon");
            Assert.That(toon, Is.Not.Null);
            ShaderData data = ShaderUtil.GetShaderData(toon);
            Assert.That(data.SubshaderCount, Is.GreaterThan(0));
            var lightModes = new List<string>();
            ShaderData.Subshader own = data.GetSubshader(0);
            for (int i = 0; i < own.PassCount; i++)
                lightModes.Add(own.GetPass(i).FindTagValue(new UnityEngine.Rendering.ShaderTagId("LightMode")).name.ToLowerInvariant());   // ShadowCaster reports as SHADOWCASTER
            Assert.That(lightModes, Does.Contain("depthnormals"), "LightModes in subshader 0: " + string.Join(",", lightModes));
            Assert.That(lightModes, Does.Contain("depthonly"));
            Assert.That(lightModes, Does.Contain("shadowcaster"), string.Join(",", lightModes));
        }

        [Test] public void Validation_ToonMeshesHaveVertexColours_Pass() => AssertNoErrors(RenderValidators.ToonMeshesHaveVertexColours());

        // Catches: a validator that skips the mesh check (a Unity primitive mesh has no vertex colours and would draw black or white on Toon).
        [Test]
        public void Validation_ToonMeshes_ReportsMeshWithoutVertexColours()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh primitiveMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cube);
            tempObject = new GameObject("__TestToonMesh");
            tempObject.AddComponent<MeshFilter>().sharedMesh = primitiveMesh;
            tempObject.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get("M_Toon_Base");
            PrefabUtility.SaveAsPrefabAsset(tempObject, TempToonPrefab);

            List<string> errors = RenderValidators.ToonMeshesHaveVertexColours();

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            StringAssert.Contains(TempToonPrefab, errors[0]);
            StringAssert.Contains("without vertex colours", errors[0]);
        }

        // Catches: a validator that skips the BF-7 _JitterEncoded rule (a MeshBuilder mesh on a material with 0 renders at half brightness).
        [Test]
        public void Validation_ToonMeshes_ReportsWrongJitterEncoded()
        {
            Material wrong = new Material(Shader.Find("FishGame/Toon")) { name = "__TestToonJitter" };
            wrong.SetFloat("_JitterEncoded", 0f);
            AssetDatabase.CreateAsset(wrong, TempToonMaterial);
            tempObject = new GameObject("__TestToonMesh");
            tempObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPaths.BodyUnitBoxMesh);
            tempObject.AddComponent<MeshRenderer>().sharedMaterial = wrong;
            PrefabUtility.SaveAsPrefabAsset(tempObject, TempToonPrefab);

            List<string> errors = RenderValidators.ToonMeshesHaveVertexColours();

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            StringAssert.Contains("has the wrong _JitterEncoded", errors[0]);
        }
    }
}
