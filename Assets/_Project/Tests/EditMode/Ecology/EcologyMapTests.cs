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
using FishGame.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;

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

        static void WithLakeScene(Action<Scene> visit) => WithScene(AssetPaths.LakeDockScene, visit);

        static void WithHarbourScene(Action<Scene> visit) => WithScene(AssetPaths.HarbourPierScene, visit);

        internal static void WithScene(string scenePath, Action<Scene> visit)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null,
                        "missing " + scenePath + ": run Tools/unity.ps1 build first");
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasValid = scene.IsValid();
            bool opened = !(wasValid && scene.isLoaded);
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
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

        // Catches: a missing half-cell offset; the bake treating void as land; metres and decimetres swapped; Shore removed from the mask
        // (the wet rocks sit on the Shore layer, so without it they would read as water).
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

        // ================================================================================================================================
        // T17: Spot_HarbourPier (plan maps T17.8, T17-B). The Blender visuals are checked in HarbourPierModelTests.
        // ================================================================================================================================

        static uint WorldHashOf(Scene scene)
        {
            GameObject world = Root(scene, "World");
            Assert.That(world, Is.Not.Null, scene.name + " has a World root");
            using (var so = new SerializedObject(world.GetComponent<NetworkObject>())) return so.FindProperty("GlobalObjectIdHash").uintValue;
        }

        // Types in the App and Presentation assemblies are not referenced by the EditMode tests (C 4): checked by name.
        static bool HasComponentNamed(GameObject go, string typeName)
        {
            foreach (Component c in go.GetComponents<Component>()) if (c != null && c.GetType().Name == typeName) return true;
            return false;
        }

        static Component ComponentNamed(GameObject go, string typeName)
        {
            foreach (Component c in go.GetComponents<Component>()) if (c != null && c.GetType().Name == typeName) return c;
            return null;
        }

        // A public property or field (the URP Volume exposes isGlobal / sharedProfile / weight either way; the test asmdef has no URP reference).
        static object Member(Component c, string name)
        {
            System.Reflection.PropertyInfo p = c.GetType().GetProperty(name);
            if (p != null) return p.GetValue(c);
            System.Reflection.FieldInfo f = c.GetType().GetField(name);
            Assert.That(f, Is.Not.Null, c.GetType().Name + "." + name);
            return f.GetValue(c);
        }

        static void AssertLayerTree(GameObject root, int layer, string label)
        {
            Assert.That(root, Is.Not.Null, label);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                Assert.That(t.gameObject.layer, Is.EqualTo(layer), label + "/" + t.name + " layer");
        }

        // Catches: a forgotten hook (no GameScope, no Ambience, no spawner), a Water-layer collider on a prop, World without its driver, a ladder
        // rotated or on the wrong layer, boundary walls that CastProbe or the bake would see, fog settings lost, JournalRecorder missing.
        [Test]
        public void HarbourPier_SceneHasEveryPerSceneHook()
        {
            ContentRegistry registry = Registry();
            uint lakeHash = 0u;
            WithLakeScene(scene => lakeHash = WorldHashOf(scene));
            uint harbourHash = 0u;
            WithHarbourScene(scene =>
            {
                GameObject gameScope = Root(scene, "GameScope");
                Assert.That(gameScope, Is.Not.Null, "GameScope root");
                Assert.That(HasComponentNamed(gameScope, "GameScope"), Is.True, "GameScope component (App assembly: by name)");

                GameObject spawnerRoot = Root(scene, "PlayerSpawner");
                Assert.That(spawnerRoot, Is.Not.Null, "PlayerSpawner root");
                var spawner = spawnerRoot.GetComponent<PlayerSpawner>();
                Assert.That(spawner, Is.Not.Null, "PlayerSpawner component");
                using (var so = new SerializedObject(spawner))
                {
                    UnityEngine.Object prefab = so.FindProperty("playerPrefab").objectReferenceValue;
                    Assert.That(prefab, Is.Not.Null, "playerPrefab");
                    Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(AssetPaths.PlayerPrefab));
                    SerializedProperty points = so.FindProperty("spawnPoints");
                    Assert.That(points.arraySize, Is.EqualTo(4), "four spawn points");
                    for (int i = 0; i < 4; i++)
                    {
                        var point = (Transform)points.GetArrayElementAtIndex(i).objectReferenceValue;
                        Assert.That(point, Is.Not.Null, "Spawn" + i);
                        Vector3 p = point.position;
                        Assert.That(p.y, Is.GreaterThanOrEqualTo(HarbourPierLayout.DeckTopY), "Spawn" + i + " is above the deck");
                        Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(HarbourPierLayout.DeckHalfWidth), "Spawn" + i + " is inside the deck footprint (x)");
                        Assert.That(p.z, Is.InRange(HarbourPierLayout.DeckRootZ, HarbourPierLayout.DeckEndZ), "Spawn" + i + " is inside the deck footprint (z)");
                    }
                }

                GameObject cameraRoot = Root(scene, "SceneCamera");
                Assert.That(cameraRoot, Is.Not.Null, "SceneCamera root");
                Assert.That(cameraRoot.GetComponent<Camera>(), Is.Not.Null, "Camera");
                Assert.That(cameraRoot.GetComponent<AudioListener>(), Is.Not.Null, "AudioListener");
                Assert.That(cameraRoot.GetComponent<SceneCameraSwitch>(), Is.Not.Null, "SceneCameraSwitch");
                Assert.That(cameraRoot.tag, Is.EqualTo("MainCamera"));

                GameObject ambience = Root(scene, "Ambience");
                Assert.That(ambience, Is.Not.Null, "Ambience root");
                Assert.That(ambience.GetComponent<AmbienceBed>(), Is.Not.Null, "AmbienceBed");

                GameObject anchor = Root(scene, "BellAnchor");
                Assert.That(anchor, Is.Not.Null, "BellAnchor root");
                var netAnchor = anchor.GetComponent<NetSpawnAnchor>();
                Assert.That(netAnchor, Is.Not.Null, "NetSpawnAnchor");
                Assert.That(AssetDatabase.GetAssetPath(netAnchor.Prefab), Is.EqualTo(AssetPaths.DockBellPrefab));

                GameObject waterVisual = Root(scene, "WaterVisual");
                Assert.That(waterVisual, Is.Not.Null, "WaterVisual root");
                Assert.That(waterVisual.layer, Is.EqualTo(Layers.Water));
                Assert.That(waterVisual.GetComponentsInChildren<Collider>(true), Is.Empty, "WaterVisual has no collider");
                GameObject waterVolume = Root(scene, "WaterVolume");
                Assert.That(waterVolume, Is.Not.Null, "WaterVolume root");
                Assert.That(waterVolume.layer, Is.EqualTo(Layers.Water));
                Assert.That(waterVolume.GetComponent<WaterVolume>(), Is.Not.Null);
                var waterBox = waterVolume.GetComponent<BoxCollider>();
                Assert.That(waterBox, Is.Not.Null);
                Assert.That(waterBox.isTrigger, Is.True, "WaterVolume is a trigger");
                Assert.That(waterBox.size, Is.EqualTo(HarbourPierLayout.WaterVolumeSize).Using(Vector3EqualityComparer.Instance), "WaterVolume size");
                Assert.That(waterVolume.transform.position + waterBox.center, Is.EqualTo(HarbourPierLayout.WaterVolumeCentre).Using(Vector3EqualityComparer.Instance), "WaterVolume centre");
                AssertNoErrors(EcologyValidators.WaterLayerErrors(scene, "harbour"));

                GameObject seabed = Root(scene, "Seabed");
                Assert.That(seabed, Is.Not.Null, "Seabed root");
                Assert.That(seabed.layer, Is.EqualTo(Layers.Ground));
                Assert.That(seabed.GetComponent<MeshCollider>(), Is.Not.Null, "Seabed MeshCollider");
                AssertLayerTree(Root(scene, "Beach"), Layers.Shore, "Beach");
                AssertLayerTree(Root(scene, "Breakwater"), Layers.Shore, "Breakwater");
                AssertLayerTree(Root(scene, "Pier"), Layers.Structure, "Pier");
                AssertLayerTree(Root(scene, "Props"), Layers.Structure, "Props");

                GameObject ladders = Root(scene, "Ladders");
                Assert.That(ladders, Is.Not.Null, "Ladders root");
                Assert.That(ladders.transform.rotation, Is.EqualTo(Quaternion.identity), "Ladders root has no rotation");
                foreach (string name in new[] { "LadderMid", "LadderHead" })
                {
                    Transform ladder = ladders.transform.Find(name);
                    Assert.That(ladder, Is.Not.Null, name);
                    Assert.That(ladder.gameObject.layer, Is.EqualTo(Layers.Default), name + " is on Default (CastProbe never sees it)");
                    Assert.That(ladder.rotation, Is.EqualTo(Quaternion.identity), name + " has no rotation");
                    var box = ladder.GetComponent<BoxCollider>();
                    Assert.That(box, Is.Not.Null, name);
                    Assert.That(box.isTrigger, Is.True, name + " is a trigger");
                    Assert.That(ladder.GetComponent<LadderVolume>(), Is.Not.Null, name);
                }
                Assert.That(Root(scene, "Exits"), Is.Null, "no WaterExit in the harbour: swimmers use the beach and the ladders (R70)");

                GameObject bounds = Root(scene, "SeaBounds");
                Assert.That(bounds, Is.Not.Null, "SeaBounds root");
                Assert.That(bounds.transform.childCount, Is.EqualTo(4), "four boundary walls");
                foreach (Transform wall in bounds.transform)
                {
                    Assert.That(wall.gameObject.layer, Is.EqualTo(Layers.Default), wall.name + " is on Default (invisible to CastProbe and the bake)");
                    var box = wall.GetComponent<BoxCollider>();
                    Assert.That(box, Is.Not.Null, wall.name);
                    Assert.That(box.isTrigger, Is.False, wall.name + " is solid");
                    Assert.That(wall.GetComponentsInChildren<Renderer>(true), Is.Empty, wall.name + " has no renderer");
                }

                GameObject sun = Root(scene, "Sun");
                Assert.That(sun, Is.Not.Null, "Sun root");
                Assert.That(sun.GetComponent<Light>(), Is.Not.Null);
                Assert.That(sun.GetComponent<Light>().type, Is.EqualTo(LightType.Directional));
                foreach (string name in new[] { "WarmVolume", "CoolVolume" })
                {
                    GameObject go = Root(scene, name);
                    Assert.That(go, Is.Not.Null, name);
                    Component volume = ComponentNamed(go, "Volume");
                    Assert.That(volume, Is.Not.Null, name + " has a Volume");
                    Assert.That((bool)Member(volume, "isGlobal"), Is.True, name + " is global");
                    Assert.That(Member(volume, "sharedProfile"), Is.Not.Null, name + " has a profile");
                }

                GameObject world = Root(scene, "World");
                Assert.That(world, Is.Not.Null, "World root");
                var no = world.GetComponent<NetworkObject>();
                var ws = world.GetComponent<WorldState>();
                Assert.That(no, Is.Not.Null, "NetworkObject");
                Assert.That(ws, Is.Not.Null, "WorldState");
                Assert.That(world.GetComponent<DevWorldDriver>(), Is.Not.Null, "DevWorldDriver");
                Assert.That(ws.SpotId, Is.EqualTo(ContentIds.HarbourPier));
                harbourHash = WorldHashOf(scene);
                Assert.That(harbourHash, Is.Not.EqualTo(0u), "GlobalObjectIdHash");
                AssertNoErrors(EcologyValidators.WorldStateErrors(scene, "harbour", registry));

                GameObject stands = Root(scene, "StandPoints");
                Assert.That(stands, Is.Not.Null, "StandPoints root");
                IReadOnlyList<Vector4> layout = HarbourPierLayout.StandPoints;
                Assert.That(layout.Count, Is.EqualTo(11));
                Assert.That(stands.transform.childCount, Is.EqualTo(11), "StandPoints children");
                for (int i = 0; i < layout.Count; i++)
                {
                    Transform t = stands.transform.Find("StandPoint_" + i);
                    Assert.That(t, Is.Not.Null, "StandPoint_" + i);
                    Assert.That((t.position - new Vector3(layout[i].x, layout[i].y, layout[i].z)).magnitude, Is.LessThan(1e-4f), "position " + i);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(t.GetComponent<StandPoint>().FacingDeg, layout[i].w)), Is.LessThan(1e-3f), "facing " + i);
                    Assert.That(t.GetComponentsInChildren<Collider>(true), Is.Empty, t.name + " has no collider");
                }

                GameObject fishing = Root(scene, "Fishing");
                Assert.That(fishing, Is.Not.Null, "Fishing root");
                Assert.That(fishing.GetComponent<FishGame.Fishing.FishingHost>(), Is.Not.Null, "FishingHost");
                Assert.That(fishing.GetComponent<FishGame.Fishing.FightTelemetry>(), Is.Not.Null, "FightTelemetry");
                Assert.That(fishing.GetComponent<FishGame.Fishing.JournalRecorder>(), Is.Not.Null, "JournalRecorder");

                GameObject presentation = Root(scene, "Presentation");
                Assert.That(presentation, Is.Not.Null, "Presentation root");
                foreach (string name in new[] { "PaletteGlobals", "WaterTimeDriver", "RippleField" })
                    Assert.That(HasComponentNamed(presentation, name), Is.True, "Presentation/" + name + " (Presentation assembly: by name)");
            });
            Assert.That(harbourHash, Is.Not.EqualTo(lakeHash), "the harbour's World hash differs from the lake's");

            // RenderSettings report the ACTIVE scene, so the saved per-scene settings are read from the scene file.
            string text = File.ReadAllText(AssetPaths.HarbourPierScene);
            Assert.That(text, Does.Match(@"(?m)^\s+m_Fog: 1\s*$"), "fog is on");
            Assert.That(text, Does.Match(@"(?m)^\s+m_FogMode: 3\s*$"), "fog is ExponentialSquared");
            string skyGuid = AssetDatabase.AssetPathToGUID(AssetPaths.MaterialsFolder + "/M_Sky.mat");
            Assert.That(skyGuid, Is.Not.Empty);
            Assert.That(text, Does.Match(@"m_SkyboxMaterial: \{fileID: 2100000, guid: " + skyGuid + ","), "the skybox is M_Sky");
            MatchCollection hashes = Regex.Matches(text, @"(?m)^\s+GlobalObjectIdHash: (\d+)\s*$");
            Assert.That(hashes.Count, Is.EqualTo(1), "one NetworkObject in the harbour scene file");
            Assert.That(uint.Parse(hashes[0].Groups[1].Value), Is.EqualTo(harbourHash), "the scene file holds the same hash");
            TestContext.WriteLine("FG MAP harbour_pier World GlobalObjectIdHash=" + harbourHash);
        }

        // Catches: the bake reading the beach as water; a seabed that disagrees with the layout; Structure in the bake mask; a stand point that
        // moved without a re-bake; a sub-area nobody can reach.
        [Test]
        public void ShoreGridBaker_HarbourEverySubArea_TwentyFiveReachableCastableCells()
        {
            ContentRegistry registry = Registry();
            var spot = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.HarbourPierSpotAsset);
            Assert.That(spot, Is.Not.Null, "harbour_pier spot asset");
            Assert.That(spot.Zone, Is.Not.Null, "harbour_pier has a zone");
            ZoneData zone = spot.Zone.ToCore(registry);
            Assert.That(zone.Grid, Is.Not.Null, "the harbour zone grid is baked (run build)");
            var def = AssetDatabase.LoadAssetAtPath<ShoreGridDef>(AssetPaths.HarbourPierGridAsset);
            Assert.That(def, Is.Not.Null);
            Assert.That(def.BakedFromScene, Is.EqualTo(SceneNames.SpotHarbourPier), "baked from the harbour scene");

            int[] counts = ShoreGridMath.CountReachablePerSubArea(zone.Grid, zone);
            for (int i = 0; i < counts.Length; i++)
            {
                TestContext.WriteLine("FG MAP harbour_pier " + spot.Zone.SubAreas[i].id + "=" + counts[i]);
                Assert.That(counts[i], Is.GreaterThanOrEqualTo(EcologyConstants.MinReachableCellsPerSubArea), spot.Zone.SubAreas[i].id);
            }

            ShoreGrid g = zone.Grid;
            // Straight out from the coast, at least 35 m from the breakwater (land), the coast is the nearest land for the whole z range (the pier is
            // Structure and the walls are Default, so neither counts). Axis-aligned chamfer distance is exact: shore = z + 45.
            foreach (float x in new[] { -50.5f, -40.5f, -20.5f, -10.5f })
            {
                for (float z = -44.5f; z <= -10.5f; z += 1f)
                {
                    Assert.That(g.IsWater(x, z), Is.True, "water at (" + x + ", " + z + ")");
                    Assert.That(Mathf.Abs(g.ShoreM(x, z) - (z + 45f)), Is.LessThanOrEqualTo(0.05f), "shore distance at (" + x + ", " + z + "): " + g.ShoreM(x, z));
                }
                for (float z = -45.5f; z >= -59.5f; z -= 1f)
                    Assert.That(g.IsWater(x, z), Is.False, "the beach is land at (" + x + ", " + z + ")");
            }
            Assert.That(g.TryCell(0.5f, -30.5f, out int ux, out int uz), Is.True);
            Assert.That(g.IsWater(0.5f, -30.5f), Is.True, "under the pier is water");
            Assert.That(g.IsCastable(ux, uz), Is.False, "the deck is CastProbe's first hit");
            Assert.That(g.TryCell(5.5f, -30.5f, out int ox, out int oz), Is.True);
            Assert.That(g.IsCastable(ox, oz), Is.True, "beside the pier is castable");

            for (int cz = 0; cz < g.Height; cz++)
            {
                for (int cx = 0; cx < g.Width; cx++)
                {
                    g.CellCentre(cx, cz, out float x, out float z);
                    if (g.IsReachable(cx, cz)) Assert.That(g.IsCastable(cx, cz), Is.True, "reachable implies castable at " + cx + "," + cz);
                    if (g.IsCastable(cx, cz)) Assert.That(g.IsWater(x, z), Is.True, "castable implies water at " + cx + "," + cz);
                }
            }
            Assert.That(def.StandPoints.Count, Is.EqualTo(HarbourPierLayout.StandPoints.Length), "the grid baked every stand point");
            for (int i = 0; i < def.StandPoints.Count; i++)
            {
                Vector4 p = HarbourPierLayout.StandPoints[i];
                Assert.That((def.StandPoints[i] - new Vector3(p.x, p.y, p.z)).magnitude, Is.LessThan(1e-4f), "grid stand " + i);
            }
        }

        // Catches: a time or UnityEngine.Random seed, a hash-container iteration order, a missing uv1 channel (untextured, unpaletted mesh) in the
        // meshes the harbour still generates in code (water, seabed, beach); the Blender meshes are checked by HarbourPierModelTests.
        [Test]
        public void HarbourPier_Rebuild_ByteIdenticalMeshes()
        {
            List<Mesh> a = HarbourPierMeshes.BuildAllInMemory();
            List<Mesh> b = HarbourPierMeshes.BuildAllInMemory();
            try
            {
                Assert.That(a.Count, Is.EqualTo(4), "water, water skirt, seabed, beach");
                AssertSkirtMatchesWaterBorder(a[0], a[1]);
                Assert.That(b.Count, Is.EqualTo(a.Count));
                for (int m = 0; m < a.Count; m++)
                {
                    Mesh x = a[m], y = b[m];
                    Assert.That(y.name, Is.EqualTo(x.name), "order");
                    Assert.That(y.indexFormat, Is.EqualTo(x.indexFormat), x.name + " index format");
                    Assert.That(x.vertexCount, Is.LessThan(65535), x.name + " stays 16-bit safe");
                    Assert.That(y.vertices, Is.EqualTo(x.vertices), x.name + " vertices");
                    Assert.That(y.normals, Is.EqualTo(x.normals), x.name + " normals");
                    Assert.That(y.uv, Is.EqualTo(x.uv), x.name + " uv0");
                    Assert.That(y.colors, Is.EqualTo(x.colors), x.name + " colours");
                    Assert.That(y.triangles, Is.EqualTo(x.triangles), x.name + " triangles");
                    var ux = new List<Vector4>();
                    var uy = new List<Vector4>();
                    x.GetUVs(1, ux);
                    y.GetUVs(1, uy);
                    Assert.That(uy, Is.EqualTo(ux), x.name + " uv1");

                    if (x.name == "Harbour_Water" || x.name == "Harbour_WaterSkirt") continue;   // the water meshes (51,200 and 672 triangles, 16-bit) are not Toon: no colours or uv1
                    Assert.That(x.triangles.Length / 3, Is.LessThanOrEqualTo(LakeDockLayout.MaxCombinedTriangles), x.name + " triangle limit");
                    Assert.That(x.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color), Is.True, x.name + " has vertex colours");
                    Assert.That(ux.Count, Is.EqualTo(x.vertexCount), x.name + " has uv1 on every vertex");
                    foreach (Vector4 uv in ux)
                    {
                        Assert.That(uv.y, Is.EqualTo(Mathf.Round(uv.y)), x.name + " uv1.y is a whole number");
                        Assert.That(uv.y, Is.InRange(0f, (float)EnvTextureLayers.Face), x.name + " uv1.y is a defined texture layer");
                        Assert.That(uv.x, Is.EqualTo(Mathf.Round(uv.x)), x.name + " uv1.x is a whole number");
                        Assert.That(uv.x, Is.InRange(0f, 15f), x.name + " uv1.x is a palette slot");
                        Assert.That(uv.z, Is.EqualTo((float)SmoothMeshBuilder.MappingUv), x.name + " is uv0-mapped (M_Toon_Base)");
                    }
                }
            }
            finally
            {
                foreach (Mesh mesh in a) UnityEngine.Object.DestroyImmediate(mesh);
                foreach (Mesh mesh in b) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // Catches: a crack or a T-junction between the water grid and the sea skirt (the sea would show a hairline of sky or seabed at +-120 m), a skirt
        // that does not reach the far clip plane, a skirt facing down, a skirt off the water plane.
        static void AssertSkirtMatchesWaterBorder(Mesh water, Mesh skirt)
        {
            Assert.That(skirt.name, Is.EqualTo("Harbour_WaterSkirt"));
            var skirtVertices = new HashSet<Vector3>(skirt.vertices);
            float edge = HarbourPierLayout.WaterGridCells * HarbourPierLayout.WaterCellSize * 0.5f;
            int border = 0;
            foreach (Vector3 v in water.vertices)
            {
                if (Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z)) != edge) continue;
                border++;
                Assert.That(skirtVertices.Contains(v), Is.True, "the water grid's border vertex " + v + " is not a skirt vertex (bit for bit)");
            }
            Assert.That(border, Is.EqualTo(4 * HarbourPierLayout.WaterGridCells), "the grid's border has 640 vertices");
            Vector3[] vs = skirt.vertices;
            Vector3[] ns = skirt.normals;
            float far = 0f;
            for (int i = 0; i < vs.Length; i++)
            {
                Assert.That(vs[i].y, Is.EqualTo(0f), "skirt vertex " + i + " is on the water plane");
                Assert.That(ns[i], Is.EqualTo(Vector3.up), "skirt normal " + i + " is up");
                far = Mathf.Max(far, Mathf.Abs(vs[i].x), Mathf.Abs(vs[i].z));
            }
            Assert.That(far, Is.EqualTo(HarbourPierLayout.SkirtOuterHalfExtent), "the skirt reaches the far clip plane");
            int[] tris = skirt.triangles;
            float area = 0f;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 c = Vector3.Cross(vs[tris[t + 1]] - vs[tris[t]], vs[tris[t + 2]] - vs[tris[t]]);
                Assert.That(c.y, Is.GreaterThan(0f), "skirt triangle " + t / 3 + " faces up");
                area += c.y * 0.5f;
            }
            float want = 4f * HarbourPierLayout.SkirtOuterHalfExtent * HarbourPierLayout.SkirtOuterHalfExtent - 4f * edge * edge;
            Assert.That(area, Is.EqualTo(want).Within(want * 1e-3f), "the skirt covers exactly the ring between the grid and the far square (no gap, no overlap)");
        }

        // Catches: a collider moved off the layout or the manifest; a breakwater collider above the walkable top (an invisible bump on the path edge);
        // the rocks redrawn instead of taken from the manifest; props without colliders; a ladder rail box off its ladder.
        [Test]
        public void HarbourPier_Colliders_MatchLayoutAndManifest()
        {
            HarbourPierManifest.Model[] manifest = HarbourPierManifest.Load();
            WithHarbourScene(scene =>
            {
                // Pier: Deck, THead, PierRamp, 38 posts, two rail boxes. Collider-only, Structure.
                GameObject pier = Root(scene, "Pier");
                Assert.That(pier, Is.Not.Null);
                Bounds deck = pier.transform.Find("Deck").GetComponent<BoxCollider>().bounds;
                Assert.That(deck.min.x, Is.EqualTo(-HarbourPierLayout.DeckHalfWidth).Within(1e-4f));
                Assert.That(deck.max.x, Is.EqualTo(HarbourPierLayout.DeckHalfWidth).Within(1e-4f));
                Assert.That(deck.max.y, Is.EqualTo(HarbourPierLayout.DeckTopY).Within(1e-4f), "deck top");
                Assert.That(deck.min.z, Is.EqualTo(HarbourPierLayout.DeckRootZ).Within(1e-4f));
                Assert.That(deck.max.z, Is.EqualTo(HarbourPierLayout.DeckEndZ).Within(1e-4f));
                Bounds head = pier.transform.Find("THead").GetComponent<BoxCollider>().bounds;
                Assert.That(head.min.x, Is.EqualTo(-HarbourPierLayout.THeadHalfWidth).Within(1e-4f));
                Assert.That(head.max.x, Is.EqualTo(HarbourPierLayout.THeadHalfWidth).Within(1e-4f));
                Assert.That(head.min.z, Is.EqualTo(HarbourPierLayout.THeadNearZ).Within(1e-4f));
                Assert.That(head.max.z, Is.EqualTo(HarbourPierLayout.THeadFarZ).Within(1e-4f));
                Assert.That(head.max.y, Is.EqualTo(HarbourPierLayout.DeckTopY).Within(1e-4f), "T-head top");
                Assert.That(pier.transform.Find("PierRamp").GetComponent<BoxCollider>(), Is.Not.Null, "PierRamp");

                int posts = 0;
                foreach (Transform t in pier.transform)
                {
                    if (!t.name.StartsWith("Post_", StringComparison.Ordinal)) continue;
                    posts++;
                    Bounds b = t.GetComponent<BoxCollider>().bounds;
                    Vector3 c = b.center;
                    float terrain = HarbourPierLayout.TerrainY(c.x, c.z);
                    Assert.That(b.min.y, Is.EqualTo(terrain).Within(1e-3f), t.name + " stands on the terrain");
                    Assert.That(b.max.y, Is.EqualTo(HarbourPierLayout.DeckUndersideY).Within(1e-3f), t.name + " reaches the deck underside");
                }
                Assert.That(posts, Is.EqualTo(HarbourPierLayout.Posts.Length), "post colliders");
                Assert.That(HarbourPierLayout.Posts.Length, Is.EqualTo(38));

                Bounds mid = pier.transform.Find("Ladder_Mid").GetComponent<BoxCollider>().bounds;
                Assert.That(mid.center, Is.EqualTo(HarbourPierLayout.LadderMidRailBox.center).Using(Vector3EqualityComparer.Instance));
                Bounds railHead = pier.transform.Find("Ladder_Head").GetComponent<BoxCollider>().bounds;
                Assert.That(railHead.center, Is.EqualTo(HarbourPierLayout.LadderHeadRailBox.center).Using(Vector3EqualityComparer.Instance));
                // The rail colliders end at the deck top (a wall above it makes the ladders unclimbable; the visual rails rise higher).
                Assert.That(mid.max.y, Is.EqualTo(HarbourPierLayout.DeckTopY).Within(1e-4f), "mid rail collider top is the deck top");
                Assert.That(railHead.max.y, Is.EqualTo(HarbourPierLayout.DeckTopY).Within(1e-4f), "head rail collider top is the deck top");
                Assert.That(mid.min.y, Is.EqualTo(HarbourPierLayout.LadderVolumeBottomY).Within(1e-4f), "mid rail collider foot");
                Assert.That(railHead.min.y, Is.EqualTo(HarbourPierLayout.LadderVolumeBottomY).Within(1e-4f), "head rail collider foot");
                foreach (Collider c in pier.GetComponentsInChildren<Collider>(true)) Assert.That(c.isTrigger, Is.False, c.name);

                // Ladder volumes.
                GameObject ladders = Root(scene, "Ladders");
                Bounds lm = ladders.transform.Find("LadderMid").GetComponent<BoxCollider>().bounds;
                Bounds lh = ladders.transform.Find("LadderHead").GetComponent<BoxCollider>().bounds;
                Assert.That(lm.center, Is.EqualTo(HarbourPierLayout.LadderMidVolume.center).Using(Vector3EqualityComparer.Instance));
                Assert.That(lm.size, Is.EqualTo(HarbourPierLayout.LadderMidVolume.size).Using(Vector3EqualityComparer.Instance));
                Assert.That(lh.center, Is.EqualTo(HarbourPierLayout.LadderHeadVolume.center).Using(Vector3EqualityComparer.Instance));
                Assert.That(lh.size, Is.EqualTo(HarbourPierLayout.LadderHeadVolume.size).Using(Vector3EqualityComparer.Instance));

                // Breakwater: the walkable box plus one sphere per boulder and per rubble stone, none above the walkable top.
                GameObject breakwater = Root(scene, "Breakwater");
                Bounds box = breakwater.GetComponent<BoxCollider>().bounds;
                Assert.That(box.min, Is.EqualTo(HarbourPierLayout.BreakwaterBox.min).Using(Vector3EqualityComparer.Instance));
                Assert.That(box.max, Is.EqualTo(HarbourPierLayout.BreakwaterBox.max).Using(Vector3EqualityComparer.Instance));
                List<HarbourPierManifest.Item> boulders = HarbourPierManifest.Spheres(manifest, HarbourPierLayout.BoulderModel, "sphere", HarbourPierLayout.ExpectedBoulders);
                List<HarbourPierManifest.Item> rubble = HarbourPierManifest.Spheres(manifest, HarbourPierLayout.RubbleModel, "none (underwater", HarbourPierLayout.ExpectedRubble);
                AssertSpheres(breakwater.transform, "Boulder_", boulders, true);
                AssertSpheres(breakwater.transform, "Rubble_", rubble, false);

                // Beach: the 12 rocks from the manifest, the 16 trees.
                GameObject beach = Root(scene, "Beach");
                AssertSpheres(beach.transform, "Rock_", HarbourPierManifest.Spheres(manifest, HarbourPierLayout.RocksModel, "sphere", HarbourPierLayout.ExpectedRocks), false);
                int trees = 0;
                foreach (Transform t in beach.transform) if (t.name.StartsWith("Tree_", StringComparison.Ordinal)) trees++;
                Assert.That(trees, Is.EqualTo(HarbourPierLayout.TreeCount), "Tree_ count");

                // Props: a box per manifest box item (props, dressing, boats), equal to its bounds.
                GameObject props = Root(scene, "Props");
                int expectedBoxes = 0;
                foreach (string modelName in HarbourPierLayout.PropBoxModels)
                {
                    foreach (HarbourPierManifest.Item item in HarbourPierManifest.Find(manifest, modelName).items)
                    {
                        if (item.collider == null || !item.collider.StartsWith("box", StringComparison.Ordinal)) continue;
                        expectedBoxes++;
                        Transform t = props.transform.Find(item.name);
                        Assert.That(t, Is.Not.Null, "Props/" + item.name);
                        Bounds b = t.GetComponent<BoxCollider>().bounds;
                        Vector3 min = HarbourPierManifest.ToVector(item.bounds_min), max = HarbourPierManifest.ToVector(item.bounds_max);
                        Assert.That(b.min, Is.EqualTo(min).Using(Vector3EqualityComparer.Instance), item.name + " min");
                        Assert.That(b.max, Is.EqualTo(max).Using(Vector3EqualityComparer.Instance), item.name + " max");
                    }
                }
                Assert.That(props.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(expectedBoxes), "Props colliders: the manifest box items and nothing else");
                Assert.That(expectedBoxes, Is.GreaterThanOrEqualTo(15));
            });
        }

        static void AssertSpheres(Transform parent, string prefix, List<HarbourPierManifest.Item> items, bool clampToWalkableTop)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Transform t = parent.Find(prefix + i.ToString("00"));
                Assert.That(t, Is.Not.Null, prefix + i.ToString("00"));
                var sphere = t.GetComponent<SphereCollider>();
                Assert.That(sphere, Is.Not.Null, t.name + " SphereCollider");
                Vector3 c = HarbourPierManifest.ToVector(items[i].position);
                Assert.That(sphere.radius * t.lossyScale.x, Is.EqualTo(items[i].radius).Within(1e-4f), t.name + " radius");
                Vector3 centre = sphere.transform.TransformPoint(sphere.center);
                Assert.That(centre.x, Is.EqualTo(c.x).Within(1e-4f), t.name + " x");
                Assert.That(centre.z, Is.EqualTo(c.z).Within(1e-4f), t.name + " z");
                float wantY = clampToWalkableTop ? HarbourPierLayout.BoulderColliderCentreY(c.y, items[i].radius) : c.y;
                Assert.That(centre.y, Is.EqualTo(wantY).Within(1e-4f), t.name + " y");
                if (clampToWalkableTop)
                    Assert.That(centre.y + items[i].radius, Is.LessThanOrEqualTo(HarbourPierLayout.DeckTopY + 1e-4f), t.name + " stays below the walkable top");
            }
        }
    }
}
