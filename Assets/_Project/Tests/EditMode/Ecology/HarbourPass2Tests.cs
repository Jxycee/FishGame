// Tests/EditMode/Ecology/HarbourPass2Tests.cs - harbour round 0 (2026-10-14): the pass-2 Blender detail pass as the saved Spot_HarbourPier places it.
// Authority: test-only. Run the build first. The Presentation assembly is not referenced by the EditMode tests, so AmbientLife is reached by name.
using System;
using System.Collections.Generic;
using System.Reflection;
using FishGame.Core;
using FishGame.EditorTools;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.Tests.EditMode
{
    public class HarbourPass2Tests
    {
        static GameObject Root(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == name) return go;
            return null;
        }

        static Component Life(Scene scene)
        {
            GameObject root = Root(scene, HarbourPierLayout.RootLife);
            Assert.That(root, Is.Not.Null, "AmbientLife root");
            foreach (Component c in root.GetComponents<Component>()) if (c != null && c.GetType().Name == "AmbientLife") return c;
            Assert.Fail("no AmbientLife component");
            return null;
        }

        static List<GameObject> DetailObjects(Component life)
        {
            Type type = life.GetType();
            int count = (int)type.GetProperty("DetailCount").GetValue(life);
            var list = new List<GameObject>();
            for (int i = 0; i < count; i++) list.Add((GameObject)type.GetMethod("Detail").Invoke(life, new object[] { i }));
            return list;
        }

        static bool UnderAny(Transform t, List<GameObject> roots)
        {
            for (Transform u = t; u != null; u = u.parent) if (roots.Contains(u.gameObject)) return true;
            return false;
        }

        // Catches: a detail model that would keep a collider (Low would change gameplay), a detail mesh drawn outside the hidden set (Low would not hide
        // it), the hide not reaching the objects, a core model hidden on Low.
        [Test]
        public void HarbourPass2_DetailHasNoColliders_AndHidesOnLow()
        {
            HarbourPierManifest.Model[] manifest = HarbourPierManifest.Load();
            var detailMeshes = new HashSet<string>();
            foreach (HarbourPierManifest.Model m in manifest) if (HarbourPierManifest.IsDetail(m)) detailMeshes.Add(m.name);
            Assert.That(detailMeshes.Count, Is.GreaterThanOrEqualTo(17), "the manifest's detail models");

            EcologyMapTests.WithScene(AssetPaths.HarbourPierScene, scene =>
            {
                Component life = Life(scene);
                List<GameObject> detail = DetailObjects(life);
                Assert.That(detail.Count, Is.GreaterThanOrEqualTo(1));
                foreach (GameObject go in detail)
                {
                    Assert.That(go, Is.Not.Null);
                    Assert.That(go.GetComponentsInChildren<Collider>(true), Is.Empty, go.name + " (detail) carries a collider");
                    Assert.That(go.activeSelf, Is.True, go.name + " is saved visible (High is the default)");
                }

                int seen = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (f.sharedMesh == null) continue;
                        bool isDetail = detailMeshes.Contains(f.sharedMesh.name) || f.sharedMesh.name.StartsWith("Harbour_WeatherVane_", StringComparison.Ordinal);
                        bool hidden = UnderAny(f.transform, detail);
                        if (isDetail) { seen++; Assert.That(hidden, Is.True, f.name + " draws detail mesh " + f.sharedMesh.name + " outside the Low-hidden set"); }
                        else Assert.That(hidden, Is.False, f.name + " (" + f.sharedMesh.name + ") is a core mesh inside the Low-hidden set");
                    }
                Assert.That(seen, Is.GreaterThanOrEqualTo(30), "detail renderers in the scene");

                MethodInfo apply = life.GetType().GetMethod("ApplyDetail");
                try
                {
                    apply.Invoke(life, new object[] { false });
                    foreach (GameObject go in detail) Assert.That(go.activeSelf, Is.False, go.name + " hides on Low");
                }
                finally
                {
                    apply.Invoke(life, new object[] { true });
                }
            });
        }

        // Catches: the tug moved off its manifest mooring, a rotated tug (its ladder volumes must carry no rotation), the collision mesh on the wrong layer
        // or bobbing with the visual, a missing chest or ladder, the river on the Water layer or with a collider, the dune collision drawn, a code tree
        // that lost its colliders or still draws the old cone, a pass-2 box item without its collider.
        [Test]
        public void HarbourPass2_PlacementAndColliders_MatchManifest()
        {
            HarbourPierManifest.Model[] manifest = HarbourPierManifest.Load();
            HarbourPierManifest.Model boat = HarbourPierManifest.Find(manifest, HarbourPierLayout.CrewBoatModel);
            EcologyMapTests.WithScene(AssetPaths.HarbourPierScene, scene =>
            {
                GameObject tug = Root(scene, HarbourPierLayout.RootCrewBoat);
                Assert.That(tug, Is.Not.Null, "CrewBoat root");
                Assert.That((tug.transform.position - HarbourPierManifest.ToVector(boat.placements[0].position)).magnitude, Is.LessThan(1e-4f), "tug at its mooring");
                Assert.That(tug.transform.rotation, Is.EqualTo(Quaternion.identity), "the tug root carries no rotation");
                Transform col = tug.transform.Find("Collision");
                Assert.That(col, Is.Not.Null);
                Assert.That(col.gameObject.layer, Is.EqualTo(Layers.Shore), "the tug's collision mesh is Shore");
                Assert.That(col.GetComponent<MeshCollider>(), Is.Not.Null);
                Assert.That(col.GetComponent<Renderer>(), Is.Null, "collision only");
                Transform hull = tug.transform.Find("HullVisual");
                Assert.That(hull, Is.Not.Null);
                Assert.That(hull.GetComponentsInChildren<Collider>(true), Is.Empty, "nothing that moves with the hull visual has a collider");
                int ladders = 0, chests = 0;
                foreach (Transform t in tug.transform)
                {
                    if (t.GetComponent<LadderVolume>() != null)
                    {
                        ladders++;
                        Assert.That(t.gameObject.layer, Is.EqualTo(Layers.Default), t.name);
                        Assert.That(t.rotation, Is.EqualTo(Quaternion.identity), t.name + " no rotation");
                        Assert.That(t.GetComponent<BoxCollider>().isTrigger, Is.True, t.name);
                    }
                    if (t.name.StartsWith("CrewBoat_IceChest_", StringComparison.Ordinal) && t.GetComponent<BoxCollider>() != null) chests++;
                }
                Assert.That(ladders, Is.EqualTo(boat.ladders.Length), "tug ladders");
                Assert.That(chests, Is.EqualTo(4), "ice chest colliders");

                GameObject land = Root(scene, HarbourPierLayout.RootLand);
                Assert.That(land, Is.Not.Null);
                Transform river = land.transform.Find("RiverVisual");
                Assert.That(river, Is.Not.Null);
                Assert.That(river.gameObject.layer, Is.Not.EqualTo(Layers.Water), "the river is not on the Water layer");
                Assert.That(river.GetComponent<Collider>(), Is.Null, "the river has no collider");
                // round 1: the shallow-ribbon water (no shore-foam band; on M_Water_Surface the river drew as a white strip)
                Assert.That(river.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("M_Water_Creek"));
                Transform dunes = land.transform.Find("DunesCollision");
                Assert.That(dunes, Is.Not.Null);
                Assert.That(dunes.GetComponent<MeshCollider>(), Is.Not.Null);
                Assert.That(dunes.GetComponent<Renderer>(), Is.Null);
                Assert.That(dunes.gameObject.layer, Is.EqualTo(Layers.Shore));

                int expectedLand = 0, expectedVillage = 0;
                foreach (string m in HarbourPierLayout.LandBoxModels) expectedLand += Boxes(manifest, m);
                foreach (string m in HarbourPierLayout.VillageBoxModels) expectedVillage += Boxes(manifest, m);
                Assert.That(CountBoxes(land, Layers.Shore), Is.EqualTo(expectedLand), "Land boxes (stone wall, gate pillars) on Shore");
                Assert.That(CountBoxes(Root(scene, HarbourPierLayout.RootVillage), Layers.Structure), Is.EqualTo(expectedVillage), "Village boxes on Structure");

                GameObject beach = Root(scene, HarbourPierLayout.RootBeach);
                int trees = 0;
                foreach (Transform t in beach.transform)
                {
                    if (!t.name.StartsWith("Tree_", StringComparison.Ordinal)) continue;
                    trees++;
                    Assert.That(t.Find("Trunk").GetComponent<CapsuleCollider>(), Is.Not.Null, t.name + " trunk collider kept");
                    Assert.That(t.Find("Canopy").GetComponent<SphereCollider>(), Is.Not.Null, t.name + " canopy collider kept");
                    Assert.That(t.Find("Trunk").GetComponent<Renderer>(), Is.Null, t.name + " the old trunk draws nothing");
                    Assert.That(t.Find("Canopy").GetComponent<Renderer>(), Is.Null, t.name + " the old canopy draws nothing");
                    MeshFilter visual = t.Find("TreeVisual").GetComponent<MeshFilter>();
                    Assert.That(visual.sharedMesh.name, Does.StartWith("Harbour_Core_CodeTree_"), t.name);
                }
                Assert.That(trees, Is.EqualTo(HarbourPierLayout.TreeCount));
            });
        }

        static int Boxes(HarbourPierManifest.Model[] manifest, string model)
        {
            int n = 0;
            foreach (HarbourPierManifest.Item item in HarbourPierManifest.Find(manifest, model).items)
                if (item.collider != null && item.collider.StartsWith("box", StringComparison.Ordinal)) n++;
            return n;
        }

        static int CountBoxes(GameObject root, int layer)
        {
            int n = 0;
            foreach (BoxCollider b in root.GetComponentsInChildren<BoxCollider>(true))
            {
                Assert.That(b.gameObject.layer, Is.EqualTo(layer), b.name);
                Assert.That(b.isTrigger, Is.False, b.name);
                n++;
            }
            return n;
        }

        // Catches: Low no longer hiding the detail, a Custom tweak of Low keeping it, Medium or High losing it.
        [Test]
        public void GraphicsPresets_SceneDetail_HiddenOnLowOnly()
        {
            Assert.That(GraphicsPresets.ShowsSceneDetail((int)QualityPreset.Low, 75), Is.False);
            Assert.That(GraphicsPresets.ShowsSceneDetail((int)QualityPreset.Medium, 100), Is.True);
            Assert.That(GraphicsPresets.ShowsSceneDetail((int)QualityPreset.High, 100), Is.True);
            Assert.That(GraphicsPresets.ShowsSceneDetail((int)QualityPreset.Custom, 75), Is.False);
            Assert.That(GraphicsPresets.ShowsSceneDetail((int)QualityPreset.Custom, 80), Is.True);
        }

        // Catches: lamps lit at noon, dark at midnight, a jump at sunset.
        [Test]
        public void DayNightMath_LampGlow_RampsThroughDusk()
        {
            const float rise = 0.25f, set = 0.8f, w = 0.05f;
            Assert.That(DayNightMath.LampGlow(0.5f, rise, set, w), Is.EqualTo(0f));
            Assert.That(DayNightMath.LampGlow(0.9f, rise, set, w), Is.EqualTo(1f));
            Assert.That(DayNightMath.LampGlow(0.1f, rise, set, w), Is.EqualTo(1f));
            float mid = DayNightMath.LampGlow(set - w * 0.5f, rise, set, w);
            Assert.That(mid, Is.InRange(0.2f, 0.8f), "half way through the dusk ramp");
            Assert.That(DayNightMath.LampGlow(set - 1e-4f, rise, set, w), Is.GreaterThan(0.99f), "fully lit at the sunset flip");
            Assert.That(DayNightMath.LampGlow(float.NaN, rise, set, w), Is.EqualTo(0f));
        }
    }
}
