// T23 review. FishFallbackMesh lives in Presentation, which EditMode tests never reference (C 2.8 #8), so it is reached by reflection
// (the Presentation assembly is loaded in the editor domain). Uses the generated ContentRegistry asset (run Tools/unity.ps1 build first).
using System;
using System.Reflection;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class FishFallbackMeshTests
    {
        private static readonly Type MeshType = Type.GetType("FishGame.Presentation.FishFallbackMesh, FishGame.Presentation", true);

        private ContentRegistry _real;
        private ContentRegistry _empty;

        [SetUp]
        public void SetUp()
        {
            ResetStatics();
            _real = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(_real, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            _empty = ScriptableObject.CreateInstance<ContentRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            ResetStatics();
            if (_empty != null) UnityEngine.Object.DestroyImmediate(_empty);
        }

        private static Mesh Get(ContentRegistry registry, bool isJunk, ushort index)
        {
            MethodInfo get = MeshType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            Assert.That(get, Is.Not.Null, "FishFallbackMesh.Get");
            return (Mesh)get.Invoke(null, new object[] { registry, isJunk, index });
        }

        // Clears the mesh cache (and destroys the meshes), as entering Play Mode does.
        private static void ResetStatics()
        {
            MethodInfo reset = MeshType.GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(reset, Is.Not.Null, "FishFallbackMesh.ResetStatics");
            reset.Invoke(null, null);
        }

        // Catches: a plain grey Round fish built for a null / empty registry being cached under the item's key, so the real, coloured, shaped
        // mesh is never built once any caller asked too early (the lookup runs before the registry is read).
        [TestCase(false)]
        [TestCase(true)]
        public void FishFallbackMesh_MissingRegistryResult_DoesNotPoisonTheCache(bool isJunk)
        {
            ushort index = isJunk ? _real.JunkIndex("old_boot") : _real.SpeciesIndex("perch");   // both are Long: not the Round default

            Mesh plainNull = Get(null, isJunk, index);
            Assert.That(plainNull, Is.Not.Null, "a null registry still gives a mesh");
            Mesh plainEmpty = Get(_empty, isJunk, index);
            Assert.That(plainEmpty, Is.Not.Null, "an empty registry (index out of range) still gives a mesh");

            Mesh real = Get(_real, isJunk, index);
            Assert.That(real, Is.Not.Null);
            Assert.That(real, Is.Not.SameAs(plainNull), "the real registry must build the real mesh, not get the plain one back");
            Assert.That(real, Is.Not.SameAs(plainEmpty));
            Assert.That(real.vertexCount, Is.Not.EqualTo(plainNull.vertexCount), "the Long shape differs from the plain Round fish / can");
            Assert.That(real.colors, Is.Not.EqualTo(plainNull.colors), "the species / junk colour differs from the plain grey");
            Assert.That(Get(_real, isJunk, index), Is.SameAs(real), "a resolved item is cached");
        }

        // Catches: a bad index being cached (so a registry that later has the item cannot fix it), and a null mesh or an error for it.
        [Test]
        public void FishFallbackMesh_BadIndex_GivesAPlainMeshAndIsNotCached()
        {
            Mesh a = Get(_real, false, 60000);
            Mesh b = Get(_real, false, 60000);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(b, Is.Not.SameAs(a), "an unresolved item is not cached");
            Assert.That(a.vertexCount, Is.EqualTo(Get(null, false, 60000).vertexCount), "the same plain Round fish as the null registry");

            Mesh junk = Get(_real, true, 60000);
            Assert.That(junk, Is.Not.Null);
            Assert.That(Get(_real, true, 60000), Is.Not.SameAs(junk));
        }

        // Catches: the uncached ring (UncachedCapacity 16) never destroying a plain mesh (a leak with every bad lookup), or destroying the wrong one
        // (the newest, or one still inside the ring). The meshes are Unity objects: a destroyed one compares equal to null.
        [Test]
        public void FishFallbackMesh_UnresolvedRing_DestroysTheOldestOnThe17thCall()
        {
            const int capacity = 16;   // FishFallbackMesh.UncachedCapacity
            var meshes = new Mesh[capacity + 1];
            for (int i = 0; i < capacity; i++)
            {
                meshes[i] = Get(_real, false, (ushort)(60000 + i));
                Assert.That(meshes[i], Is.Not.Null, "unresolved call " + (i + 1));
            }

            for (int i = 0; i < capacity; i++) Assert.That(meshes[i] != null, Is.True, "the ring holds " + capacity + " meshes: #" + (i + 1) + " is still alive");

            meshes[capacity] = Get(_real, false, (ushort)(60000 + capacity));
            Assert.That(meshes[capacity] != null, Is.True, "the 17th mesh is alive");
            Assert.That(meshes[0] == null, Is.True, "the 17th unresolved call destroys the oldest mesh");
            for (int i = 1; i <= capacity; i++) Assert.That(meshes[i] != null, Is.True, "mesh #" + (i + 1) + " is still alive after the first eviction");

            Mesh next = Get(_real, true, 60000);   // the 18th: evicts the second oldest
            Assert.That(next != null, Is.True);
            Assert.That(meshes[1] == null, Is.True, "the 18th call destroys the next oldest");
            Assert.That(meshes[2] != null, Is.True);
        }
    }
}
