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
        GameObject tempObject;

        [TearDown]
        public void TearDown()
        {
            if (tempObject != null) Object.DestroyImmediate(tempObject);
            tempObject = null;
            AssetDatabase.DeleteAsset(TempMissingMeshPrefab);
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
    }
}
