// Project-level invariants from spec 4.7, checked in EditMode against persisted state. Owned by T15.
using System.Collections.Generic;
using FishGame.EditorTools;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class ValidationTests
    {
        const string TempFolder = "Assets/_Project/Tests/__Temp/Validation";   // gitignored; own subfolder so other fixtures' temp prefabs under __Temp cannot change the validators' counts
        const string TempMissingMeshPrefab = TempFolder + "/__TestMissingMesh.prefab";
        const string TempToonPrefab = TempFolder + "/__TestToonMesh.prefab";
        const string TempToonMaterial = TempFolder + "/__TestToonJitter.mat";
        const string TempToonMesh = TempFolder + "/__TestToonMapping.asset";
        GameObject tempObject;

        // A crashed earlier run may have left its temp assets behind; they would be counted by the validators.
        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempMissingMeshPrefab);
            AssetDatabase.DeleteAsset(TempToonPrefab);
            AssetDatabase.DeleteAsset(TempToonMaterial);
            AssetDatabase.DeleteAsset(TempToonMesh);
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
            AssetDatabase.DeleteAsset(TempToonMesh);
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
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

            List<string> errors = Validators.MeshReferences(TempFolder);   // scans the temp prefab folder, not Prefabs/

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

        // Catches: a Toon shader without its depth passes. With SSAO off, water depth comes from CopyDepth after the opaques; when !CanCopyDepth the
        // depth prepass uses Toon's DepthOnly pass (asserted below). DepthNormals is kept for future normals requests (SSAO or a renderer feature),
        // which would otherwise find no pass to draw Toon geometry with.
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

            List<string> errors = RenderValidators.ToonMeshesHaveVertexColours(TempFolder);   // scans the temp prefab folder, not Prefabs/

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

            List<string> errors = RenderValidators.ToonMeshesHaveVertexColours(TempFolder);   // scans the temp prefab folder, not Prefabs/

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            StringAssert.Contains("has the wrong _JitterEncoded", errors[0]);
        }

        [Test] public void Validation_ToonMeshMapping_Pass() => AssertNoErrors(RenderValidators.ToonMeshMappingMatchesMaterial());

        // Builds a temp prefab: a SmoothMeshBuilder radial mesh built for `meshMapping` on a textured (or untextured) Toon material with the
        // _FG_TRIPLANAR keyword `triplanarMaterial`, then runs the mapping check over the temp folder.
        List<string> RunMappingCheck(byte meshMapping, bool triplanarMaterial, float texWeight)
        {
            var builder = new SmoothMeshBuilder();
            builder.Radial(Vector3.zero, d => 0.5f, 2, Quaternion.identity, Vector3.one, 0, 3, meshMapping, d => 1f, 1f);
            Mesh mesh = builder.ToMesh("__TestToonMapping");
            AssetDatabase.CreateAsset(mesh, TempToonMesh);

            var material = new Material(Shader.Find("FishGame/Toon")) { name = "__TestToonJitter" };
            material.SetFloat("_FGTexWeight", texWeight);
            if (triplanarMaterial) material.EnableKeyword("_FG_TRIPLANAR");
            AssetDatabase.CreateAsset(material, TempToonMaterial);

            tempObject = new GameObject("__TestToonMesh");
            tempObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TempToonMesh);
            tempObject.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TempToonMaterial);
            PrefabUtility.SaveAsPrefabAsset(tempObject, TempToonPrefab);
            return RenderValidators.ToonMeshMappingMatchesMaterial(TempFolder);   // scans the temp prefab folder, not Prefabs/
        }

        // Catches: a validator that skips the mapping rule (a rock built for world triplanar on a uv0 material gets a stretched texture, and the
        // reverse puts uv-metre coordinates through the triplanar blend). Both mismatches must be reported, with a message that says what to do.
        [Test]
        public void Validation_ToonMeshMapping_ReportsTriplanarMeshOnUv0Material()
        {
            List<string> errors = RunMappingCheck(SmoothMeshBuilder.MappingTriplanar, false, 1f);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            StringAssert.Contains(TempToonPrefab, errors[0]);
            StringAssert.Contains("uv1.z", errors[0]);
            StringAssert.Contains("uv0-mapped", errors[0]);
        }

        [Test]
        public void Validation_ToonMeshMapping_ReportsUv0MeshOnTriplanarMaterial()
        {
            List<string> errors = RunMappingCheck(SmoothMeshBuilder.MappingUv, true, 1f);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            StringAssert.Contains("uv1.z", errors[0]);
            StringAssert.Contains("triplanar", errors[0]);
        }

        // Catches: a validator that is simply always red, or that fires on materials that never sample the array (_FGTexWeight 0).
        [Test]
        public void Validation_ToonMeshMapping_AcceptsMatchingPairsAndUntexturedMaterials()
        {
            Assert.That(RunMappingCheck(SmoothMeshBuilder.MappingUv, false, 1f), Is.Empty, "uv0 mesh on a uv0 material");
            AssetDatabase.DeleteAsset(TempToonPrefab);
            AssetDatabase.DeleteAsset(TempToonMesh);
            AssetDatabase.DeleteAsset(TempToonMaterial);
            Object.DestroyImmediate(tempObject);
            Assert.That(RunMappingCheck(SmoothMeshBuilder.MappingTriplanar, true, 1f), Is.Empty, "triplanar mesh on a triplanar material");
            AssetDatabase.DeleteAsset(TempToonPrefab);
            AssetDatabase.DeleteAsset(TempToonMesh);
            AssetDatabase.DeleteAsset(TempToonMaterial);
            Object.DestroyImmediate(tempObject);
            Assert.That(RunMappingCheck(SmoothMeshBuilder.MappingTriplanar, false, 0f), Is.Empty, "mismatch on an untextured material is never sampled");
        }
    }
}
