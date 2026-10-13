// Tests/EditMode/Ecology/EcologyMapTests.cs - T16 (C 10.4, plan maps T16.6): the lake's World object, stand points, shore-grid bake and the
// map validators. Scene-reading tests open Spot_LakeDock additively and read-only (the Validators.ForEachSceneObject pattern) and close it
// without saving; grids are read through ShoreGridDef.ToCore() from AssetPaths.LakeDockGridAsset. Run Tools/unity.ps1 build first.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using FishGame.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.Tests.EditMode
{
    public class EcologyMapTests
    {
        const float Waterline = 49f + 7f * 0.2f / 1.2f;   // ShoreY crosses 0: r = 49 + 7 x 0.2 / 1.2 = 50.1667

        static ContentRegistry Registry()
        {
            var r = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(r, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            return r;
        }

        static ShoreGrid Grid()
        {
            var def = AssetDatabase.LoadAssetAtPath<ShoreGridDef>(AssetPaths.LakeDockGridAsset);
            Assert.That(def, Is.Not.Null, "missing " + AssetPaths.LakeDockGridAsset + ": run Tools/unity.ps1 build first");
            ShoreGrid g = def.ToCore();
            Assert.That(g, Is.Not.Null, AssetPaths.LakeDockGridAsset + " is not baked: run Tools/unity.ps1 build first");
            return g;
        }

        static void WithLakeScene(Action<Scene> visit)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetPaths.LakeDockScene), Is.Not.Null,
                        "missing " + AssetPaths.LakeDockScene + ": run Tools/unity.ps1 build first");
            Scene scene = SceneManager.GetSceneByPath(AssetPaths.LakeDockScene);
            bool wasValid = scene.IsValid();
            bool opened = !(wasValid && scene.isLoaded);
            if (opened) scene = EditorSceneManager.OpenScene(AssetPaths.LakeDockScene, OpenSceneMode.Additive);
            try { visit(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, !wasValid); }
        }

        static GameObject Root(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
            return null;
        }

        // In-memory scene for the negative cases (an additive NewScene is refused while the runner's untitled scene is unsaved).
        static Scene TempScene() => EditorSceneManager.NewPreviewScene();

        static void AssertNoErrors(List<string> errors) => Assert.That(errors, Is.Empty, string.Join("\n", errors));

        // Catches: any map rule broken by a later builder change.
        [Test]
        public void Validation_EcologyMaps_Pass() => AssertNoErrors(EcologyValidators.MapChecks());

        // Catches: a missing half-cell offset; the bake treating void as land; metres and decimetres swapped; Ground left out of the mask.
        [Test]
        public void ShoreGridBaker_LakeGrid_ShoreDistanceMatchesBowlGeometry()
        {
            ShoreGrid g = Grid();
            var wetRocks = new List<Vector2>();
            WithLakeScene(scene =>
            {
                GameObject shore = Root(scene, "Shore");
                Assert.That(shore, Is.Not.Null, "Shore root");
                foreach (Transform t in shore.transform)
                {
                    if (!t.name.StartsWith("Rock_", StringComparison.Ordinal)) continue;
                    Vector3 p = t.position;
                    if (new Vector2(p.x, p.z).magnitude < 50.5f) wetRocks.Add(new Vector2(p.x, p.z));
                }
            });

            int kept = 0;
            for (int step = 0; step < 24; step++)
            {
                float deg = step * 15f;
                if (deg >= 255f && deg <= 300f) continue;                      // dock and ramp sector
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 waterlinePoint = dir * Waterline;
                bool nearRock = false;
                foreach (Vector2 rock in wetRocks) if ((rock - waterlinePoint).magnitude <= 5f) nearRock = true;
                if (nearRock) continue;
                kept++;
                for (int r = 20; r <= 48; r++)
                {
                    Vector2 p = dir * r;
                    Assert.That(g.TryCell(p.x, p.y, out int cx, out int cz), Is.True);
                    g.CellCentre(cx, cz, out float x, out float z);
                    float cr = new Vector2(x, z).magnitude;
                    Assert.That(g.IsWater(x, z), Is.True, "ray " + deg + " r " + r + " is water");
                    float want = Waterline - cr;
                    Assert.That(Mathf.Abs(g.ShoreM(x, z) - want), Is.LessThanOrEqualTo(1.0f + 0.083f * want),
                                "ray " + deg + " r " + r + ": shore " + g.ShoreM(x, z) + " vs " + want);
                }
                for (int r = 51; r <= 59; r++)
                {
                    Vector2 p = dir * r;
                    if (!g.TryCell(p.x, p.y, out int cx, out int cz)) continue;
                    g.CellCentre(cx, cz, out float x, out float z);
                    if (new Vector2(x, z).magnitude < 51f) continue;
                    Assert.That(g.IsWater(x, z), Is.False, "ray " + deg + " r " + r + " is land");
                }
            }
            TestContext.WriteLine("FG MAP lake_dock shore rays kept=" + kept + " wetRocks=" + wetRocks.Count);
            Assert.That(kept, Is.GreaterThanOrEqualTo(12), "enough rays clear of rocks standing in the water");
            Assert.That(g.ShoreM(0.5f, 0.5f), Is.GreaterThanOrEqualTo(40f), "the lake centre is about 50 m from the shore");
        }

        // Catches: Structure in the bake mask (under-dock cells become land); castable without the cast gate (C 2.8 #26).
        [Test]
        public void ShoreGridBaker_UnderDock_IsWater()
        {
            ShoreGrid g = Grid();
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                foreach (float z in new[] { -45.5f, -43.5f, -41.5f })
                {
                    Assert.That(g.TryCell(x, z, out int cx, out int cz), Is.True);
                    Assert.That(g.IsWater(x, z), Is.True, "under the deck is water (" + x + ", " + z + ")");
                    Assert.That(g.IsCastable(cx, cz), Is.False, "the deck is CastProbe's first hit (" + x + ", " + z + ")");
                }
            }
            Assert.That(g.TryCell(0.5f, -38.5f, out int ex, out int ez), Is.True);
            Assert.That(g.IsWater(0.5f, -38.5f), Is.True, "just past the dock end");
            Assert.That(g.IsCastable(ex, ez), Is.True, "just past the dock end is castable");
        }

        // Catches: reachable without castable; the ring measured from the cell corner; the wrong stand points; a reeds band nobody can cast into.
        [Test]
        public void ShoreGridBaker_LakeEverySubArea_TwentyFiveReachableCastableCells()
        {
            ContentRegistry registry = Registry();
            var spot = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.LakeDockSpotAsset);
            Assert.That(spot, Is.Not.Null);
            Assert.That(spot.Zone, Is.Not.Null, "lake_dock has a zone");
            ZoneData zone = spot.Zone.ToCore(registry);
            Assert.That(zone.Grid, Is.Not.Null, "the lake zone grid is baked");
            int[] counts = ShoreGridMath.CountReachablePerSubArea(zone.Grid, zone);
            for (int i = 0; i < counts.Length; i++)
            {
                TestContext.WriteLine("FG MAP lake_dock " + spot.Zone.SubAreas[i].id + "=" + counts[i]);
                Assert.That(counts[i], Is.GreaterThanOrEqualTo(EcologyConstants.MinReachableCellsPerSubArea), spot.Zone.SubAreas[i].id);
            }
            ShoreGrid g = zone.Grid;
            for (int cz = 0; cz < g.Height; cz++)
            {
                for (int cx = 0; cx < g.Width; cx++)
                {
                    g.CellCentre(cx, cz, out float x, out float z);
                    if (g.IsReachable(cx, cz)) Assert.That(g.IsCastable(cx, cz), Is.True, "reachable implies castable at " + cx + "," + cz);
                    if (g.IsCastable(cx, cz)) Assert.That(g.IsWater(x, z), Is.True, "castable implies water at " + cx + "," + cz);
                }
            }
        }

        // Catches: a validator that never looks at non-WaterVolume colliders (CastProbe takes the first Water-layer hit as the surface).
        [Test]
        public void WaterLayer_OnlyWaterVolumeColliders()
        {
            WithLakeScene(scene => AssertNoErrors(EcologyValidators.WaterLayerErrors(scene, "lake")));

            Scene temp = TempScene();
            try
            {
                var volume = new GameObject("WaterVolume") { layer = Layers.Water };
                SceneManager.MoveGameObjectToScene(volume, temp);
                var box = volume.AddComponent<BoxCollider>();
                box.isTrigger = true;
                volume.AddComponent<WaterVolume>();
                var intruder = new GameObject("Intruder") { layer = Layers.Water };
                SceneManager.MoveGameObjectToScene(intruder, temp);
                intruder.AddComponent<BoxCollider>();
                var ladder = new GameObject("Ladder") { layer = Layers.Default };
                SceneManager.MoveGameObjectToScene(ladder, temp);
                ladder.AddComponent<BoxCollider>().isTrigger = true;
                ladder.AddComponent<LadderVolume>();

                List<string> errors = EcologyValidators.WaterLayerErrors(temp, "temp");
                Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
                Assert.That(errors[0], Does.Contain("Intruder"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(temp);
            }
        }

        // Catches: a restyle (T4) moving reeds or a prop onto Shore or Structure (it would change the bake or CastProbe).
        [Test]
        public void LakeDock_LayerSplit_ShoreStructureDefaultAsSpecified()
        {
            WithLakeScene(scene =>
            {
                GameObject lakebed = Root(scene, "Lakebed");
                Assert.That(lakebed, Is.Not.Null);
                Assert.That(lakebed.layer, Is.EqualTo(Layers.Ground), "Lakebed");

                GameObject shore = Root(scene, "Shore");
                Assert.That(shore, Is.Not.Null);
                int rocks = 0, trees = 0;
                foreach (Transform t in shore.transform)
                {
                    if (t.name.StartsWith("Rock_", StringComparison.Ordinal)) rocks++;
                    if (t.name.StartsWith("Tree_", StringComparison.Ordinal)) trees++;
                }
                Assert.That(rocks, Is.EqualTo(14), "Rock_ count");
                Assert.That(trees, Is.EqualTo(30), "Tree_ count");
                foreach (Transform t in shore.GetComponentsInChildren<Transform>(true))
                    Assert.That(t.gameObject.layer, Is.EqualTo(Layers.Shore), "Shore/" + t.name);

                foreach (string name in new[] { "Dock" })
                {
                    GameObject root = Root(scene, name);
                    Assert.That(root, Is.Not.Null, name);
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(t.gameObject.layer, Is.EqualTo(Layers.Structure), name + "/" + t.name);
                }
                foreach (string name in new[] { "Reeds", "Exits", "Ladders", "StandPoints", "World" })
                {
                    GameObject root = Root(scene, name);
                    Assert.That(root, Is.Not.Null, name);
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(t.gameObject.layer, Is.EqualTo(Layers.Default), name + "/" + t.name);
                }
                foreach (string name in new[] { "WaterVolume", "WaterVisual" })
                {
                    GameObject root = Root(scene, name);
                    Assert.That(root, Is.Not.Null, name);
                    Assert.That(root.layer, Is.EqualTo(Layers.Water), name);
                }

                GameObject stands = Root(scene, "StandPoints");
                Assert.That(stands.transform.childCount, Is.EqualTo(10), "StandPoints children");
                foreach (Transform t in stands.transform)
                {
                    Assert.That(t.GetComponentsInChildren<Collider>(true), Is.Empty, t.name + " has no collider");
                    Assert.That(t.GetComponentsInChildren<Renderer>(true), Is.Empty, t.name + " has no renderer");
                }
            });
        }

        // Catches: AddTo without the post-save refresh (hash 0); a spot id typo; components split across roots; a hash in memory that the
        // scene file does not hold.
        [Test]
        public void WorldState_SceneObject_HashNonZeroAndSpotIdSet()
        {
            ContentRegistry registry = Registry();
            uint hash = 0u;
            WithLakeScene(scene =>
            {
                int worlds = 0;
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "World") worlds++;
                Assert.That(worlds, Is.EqualTo(1), "exactly one World root");
                GameObject world = Root(scene, "World");
                var no = world.GetComponent<NetworkObject>();
                var ws = world.GetComponent<WorldState>();
                Assert.That(no, Is.Not.Null, "NetworkObject");
                Assert.That(ws, Is.Not.Null, "WorldState");
                Assert.That(world.GetComponent<DevWorldDriver>(), Is.Not.Null, "DevWorldDriver");
                Assert.That(ws.SpotId, Is.EqualTo(ContentIds.LakeDock));
                SpotDef spot = null;
                foreach (SpotDef s in registry.Spots) if (s != null && s.Id == ws.SpotId) spot = s;
                Assert.That(spot, Is.Not.Null, "the spot id resolves");
                Assert.That(spot.SceneName, Is.EqualTo(SceneNames.SpotLakeDock));
                using (var so = new SerializedObject(no)) hash = so.FindProperty("GlobalObjectIdHash").uintValue;
                Assert.That(hash, Is.Not.EqualTo(0u), "GlobalObjectIdHash");
                AssertNoErrors(EcologyValidators.WorldStateErrors(scene, "lake", registry));
            });
            string text = File.ReadAllText(AssetPaths.LakeDockScene);
            // Anchored: InScenePlacedSourceGlobalObjectIdHash and PrefabGlobalObjectIdHash must not match.
            MatchCollection m = Regex.Matches(text, @"(?m)^\s+GlobalObjectIdHash: (\d+)\s*$");
            Assert.That(m.Count, Is.EqualTo(1), "one NetworkObject in the lake scene file");
            Assert.That(uint.Parse(m[0].Groups[1].Value), Is.EqualTo(hash), "the scene file holds the same hash");
            TestContext.WriteLine("FG MAP lake_dock World GlobalObjectIdHash=" + hash);

            Scene temp = TempScene();
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject("World");
                    SceneManager.MoveGameObjectToScene(go, temp);
                    go.AddComponent<NetworkObject>();
                    go.AddComponent<WorldState>();
                    go.AddComponent<DevWorldDriver>();
                }
                List<string> errors = EcologyValidators.WorldStateErrors(temp, "temp", null);
                Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
                Assert.That(errors[0], Does.Contain("2 WorldState"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(temp);
            }
        }

        // extra: Catches a stand point moved in the layout but not re-baked, or a facing convention flip.
        [Test]
        public void LakeDock_StandPoints_MatchLayout()
        {
            IReadOnlyList<Vector4> layout = LakeDockLayout.StandPoints;
            Assert.That(layout.Count, Is.EqualTo(10));
            WithLakeScene(scene =>
            {
                GameObject root = Root(scene, "StandPoints");
                Assert.That(root, Is.Not.Null);
                for (int i = 0; i < layout.Count; i++)
                {
                    Transform t = root.transform.Find("StandPoint_" + i);
                    Assert.That(t, Is.Not.Null, "StandPoint_" + i);
                    var sp = t.GetComponent<StandPoint>();
                    Assert.That(sp, Is.Not.Null);
                    Assert.That((t.position - new Vector3(layout[i].x, layout[i].y, layout[i].z)).magnitude, Is.LessThan(1e-4f), "position " + i);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(sp.FacingDeg, layout[i].w)), Is.LessThan(1e-3f), "facing " + i);
                }
            });
            var def = AssetDatabase.LoadAssetAtPath<ShoreGridDef>(AssetPaths.LakeDockGridAsset);
            Assert.That(def.StandPoints.Count, Is.EqualTo(layout.Count), "the grid baked every stand point");
            for (int i = 0; i < layout.Count; i++)
                Assert.That((def.StandPoints[i] - new Vector3(layout[i].x, layout[i].y, layout[i].z)).magnitude, Is.LessThan(1e-4f), "grid stand " + i);
            for (int i = 4; i < layout.Count; i++)   // the six bank points face the lake centre
            {
                float f = layout[i].w * Mathf.Deg2Rad;
                var facing = new Vector2(Mathf.Sin(f), Mathf.Cos(f));
                Vector2 toCentre = new Vector2(-layout[i].x, -layout[i].z).normalized;
                Assert.That(Vector2.Dot(facing, toCentre), Is.GreaterThan(0.999f), "bank " + i + " faces the centre");
            }
        }
    }
}
