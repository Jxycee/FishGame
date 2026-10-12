// Builds Spot_LakeDock.unity (contract 6.5, spec 17.1 item 9, D34, D36). Owned by T15.
// Deterministic: fixed build order, fixed names, seeded Pcg32 streams (Rng.Stream); the meshes are refilled in place.
// Never name SessionState in this file: with "using UnityEditor;" it would be ambiguous (CS0104).
using FishGame.App;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class LakeDockBuilder
    {
        public static void Build()
        {
            LakeDockMeshes.BuildAsset(AssetPaths.LakebedMesh, LakeDockLayout.LakebedRadii, LakeDockLayout.BedY);
            LakeDockMeshes.BuildAsset(AssetPaths.ShoreMesh, LakeDockLayout.ShoreRadii, LakeDockLayout.ShoreY);
            AssetDatabase.SaveAssets(); // meshes are on disk before the scene references them

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references (C 2.8 #14).
            NetworkObject playerPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.PlayerPrefab);
            NetworkObject bellPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.DockBellPrefab);
            Mesh lakebedMesh = LoadMesh(AssetPaths.LakebedMesh);
            Mesh shoreMesh = LoadMesh(AssetPaths.ShoreMesh);

            Root("GameScope", Layers.Default).AddComponent<GameScope>();
            AddSpawner(playerPrefab);
            AddSceneCamera();
            AddSun();
            Root("Ambience", Layers.Default).AddComponent<AmbienceBed>();
            AddWater();
            AddSurface("Lakebed", Layers.Ground, lakebedMesh, "M_Sand");
            GameObject shore = AddSurface("Shore", Layers.Shore, shoreMesh, "M_Grass");
            PlaceRocks(shore.transform);
            PlaceTrees(shore.transform);
            AddDock();
            AddExits();
            AddBellAnchor(bellPrefab);

            BuilderUtil.SaveScene(scene, AssetPaths.LakeDockScene);
        }

        static Mesh LoadMesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) throw new System.IO.FileNotFoundException("Missing mesh " + path);
            return mesh;
        }

        static GameObject Root(string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            return go;
        }

        // Spawn0..Spawn3 at (0, 0.65, -50.5 + 1.5 i), facing +z.
        static void AddSpawner(NetworkObject playerPrefab)
        {
            GameObject root = Root("PlayerSpawner", Layers.Default);
            var points = new Transform[LakeDockLayout.SpawnCount];
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("Spawn" + i);
                point.transform.SetParent(root.transform, false);
                point.transform.localPosition = new Vector3(0f, LakeDockLayout.SpawnY, LakeDockLayout.FirstSpawnZ + LakeDockLayout.SpawnSpacing * i);
                point.transform.localRotation = Quaternion.identity;
                points[i] = point.transform;
            }
            root.AddComponent<PlayerSpawner>().Configure(playerPrefab, points);
        }

        static void AddSceneCamera()
        {
            GameObject go = Root("SceneCamera", Layers.Default);
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 4f, -60f);
            go.transform.LookAt(new Vector3(0f, 0.6f, -46f));
            Camera camera = go.AddComponent<Camera>();
            camera.nearClipPlane = MovementMath.NearClip;
            camera.farClipPlane = MovementMath.FarClip;
            go.AddComponent<AudioListener>();
            go.AddComponent<SceneCameraSwitch>();
        }

        static void AddSun()
        {
            GameObject go = Root("Sun", Layers.Default);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Spec 10.4 M1 water: a flat 120 x 120 m visual plane plus a separate trigger box whose top face is y 0.
        static void AddWater()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
            visual.name = "WaterVisual";
            visual.layer = Layers.Water;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.position = Vector3.zero;
            visual.transform.localScale = new Vector3(12f, 1f, 12f); // the built-in plane is 10 x 10 m
            visual.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get("M_Water");

            GameObject volume = Root("WaterVolume", Layers.Water);
            BoxCollider box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(120f, 8f, 120f);
            box.center = new Vector3(0f, -4f, 0f);
            volume.AddComponent<WaterVolume>();
        }

        static GameObject AddSurface(string name, int layer, Mesh mesh, string material)
        {
            GameObject go = Root(name, layer);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(material);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static void PlaceRocks(Transform parent)
        {
            Material rockMaterial = MaterialLibrary.Get("M_Rock");
            Pcg32 rng = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.RockStream, 0UL);
            int placed = 0;
            for (int attempt = 0; attempt < LakeDockLayout.MaxAttempts && placed < LakeDockLayout.RockCount; attempt++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float r = rng.Range(LakeDockLayout.RockMinRadius, LakeDockLayout.RockMaxRadius);
                float size = rng.Range(0.6f, 1.6f);
                float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
                if (LakeDockLayout.Blocked(x, z, LakeDockLayout.RockClearance)) continue;
                GameObject rock = BuilderUtil.EnsurePrimitive(parent, "Rock_" + placed.ToString("00"), PrimitiveType.Sphere,
                    new Vector3(x, LakeDockLayout.ShoreY(r) + size * 0.25f, z), Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f),
                    new Vector3(size, size * 0.7f, size), rockMaterial, true);
                BuilderUtil.SetLayerRecursive(rock, Layers.Shore);
                placed++;
            }
            if (placed < LakeDockLayout.RockCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + LakeDockLayout.RockCount + " rocks");
        }

        static void PlaceTrees(Transform parent)
        {
            Material trunkMaterial = MaterialLibrary.Get("M_WoodDark");
            Material canopyMaterial = MaterialLibrary.Get("M_Foliage");
            Pcg32 rng = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.TreeStream, 0UL);
            int placed = 0;
            for (int attempt = 0; attempt < LakeDockLayout.MaxAttempts && placed < LakeDockLayout.TreeCount; attempt++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float r = rng.Range(LakeDockLayout.TreeMinRadius, LakeDockLayout.TreeMaxRadius);
                float height = rng.Range(0.8f, 1.3f);
                float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
                if (LakeDockLayout.Blocked(x, z, LakeDockLayout.TreeClearance)) continue;
                var tree = new GameObject("Tree_" + placed.ToString("00"));
                tree.transform.SetParent(parent, false);
                tree.transform.localPosition = new Vector3(x, LakeDockLayout.ShoreY(r), z);
                tree.transform.localScale = new Vector3(height, height, height);
                BuilderUtil.EnsurePrimitive(tree.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f),
                    Quaternion.identity, new Vector3(0.4f, 1f, 0.4f), trunkMaterial, true);
                BuilderUtil.EnsurePrimitive(tree.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 2.6f, 0f),
                    Quaternion.identity, new Vector3(2.4f, 2.4f, 2.4f), canopyMaterial, true);
                BuilderUtil.SetLayerRecursive(tree, Layers.Shore);
                placed++;
            }
            if (placed < LakeDockLayout.TreeCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + LakeDockLayout.TreeCount + " trees");
        }

        // Deck 3 x 12 m (x -1.5..1.5, z -52..-40), top y 0.6; posts every 2 m at x +-1.4 down to the bed; ladders at z -41.5; ramp at x 12.
        static void AddDock()
        {
            GameObject dock = Root("Dock", Layers.Structure);
            Transform t = dock.transform;
            Material wood = MaterialLibrary.Get("M_Wood");
            Material woodDark = MaterialLibrary.Get("M_WoodDark");

            float deckLength = LakeDockLayout.DeckFarZ - LakeDockLayout.DeckNearZ;
            float deckUnderside = LakeDockLayout.DeckTopY - LakeDockLayout.DeckThickness;
            BuilderUtil.EnsurePrimitive(t, "Deck", PrimitiveType.Cube,
                new Vector3(0f, LakeDockLayout.DeckTopY - LakeDockLayout.DeckThickness * 0.5f, (LakeDockLayout.DeckNearZ + LakeDockLayout.DeckFarZ) * 0.5f),
                Quaternion.identity, new Vector3(LakeDockLayout.DeckWidth, LakeDockLayout.DeckThickness, deckLength), wood, true);

            int postRows = Mathf.RoundToInt(deckLength / LakeDockLayout.PostSpacing) + 1; // 7 rows: z -52, -50, ..., -40
            for (int i = 0; i < postRows; i++)
            {
                float z = LakeDockLayout.DeckNearZ + LakeDockLayout.PostSpacing * i;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = LakeDockLayout.PostX * side;
                    float bed = LakeDockLayout.BedYAt(x, z);
                    BuilderUtil.EnsurePrimitive(t, "Post_" + (side < 0 ? "L" : "R") + i, PrimitiveType.Cube,
                        new Vector3(x, (deckUnderside + bed) * 0.5f, z), Quaternion.identity,
                        new Vector3(LakeDockLayout.PostSize, deckUnderside - bed, LakeDockLayout.PostSize), woodDark, true);
                }
            }

            AddLadder(t, "Ladder_L", -LakeDockLayout.LadderX, woodDark);
            AddLadder(t, "Ladder_R", LakeDockLayout.LadderX, woodDark);

            Vector3 low = LakeDockLayout.RampLow, high = LakeDockLayout.RampHigh;
            Vector3 along = high - low;
            BuilderUtil.EnsurePrimitive(t, "RampDeck", PrimitiveType.Cube, (low + high) * 0.5f,
                Quaternion.LookRotation(along.normalized, Vector3.up),
                new Vector3(LakeDockLayout.RampWidth, LakeDockLayout.RampThickness, along.magnitude), wood, true);

            BuilderUtil.SetLayerRecursive(dock, Layers.Structure);
        }

        static void AddLadder(Transform parent, string name, float x, Material material)
        {
            float bed = LakeDockLayout.BedYAt(x, LakeDockLayout.LadderZ);
            float top = LakeDockLayout.DeckTopY;
            BuilderUtil.EnsurePrimitive(parent, name, PrimitiveType.Cube, new Vector3(x, (top + bed) * 0.5f, LakeDockLayout.LadderZ),
                Quaternion.identity, new Vector3(LakeDockLayout.LadderThickness, top - bed, LakeDockLayout.LadderWidth), material, true);
        }

        // Contract 6.5 exits: trigger boxes plus a child "Top" at the climb-out point.
        static void AddExits()
        {
            GameObject exits = Root("Exits", Layers.Water);
            AddExit(exits.transform, "LadderL", new Vector3(-2.1f, -0.8f, -41.5f), new Vector3(1.2f, 2.6f, 1.2f), new Vector3(-0.9f, 0.65f, -41.5f));
            AddExit(exits.transform, "LadderR", new Vector3(2.1f, -0.8f, -41.5f), new Vector3(1.2f, 2.6f, 1.2f), new Vector3(0.9f, 0.65f, -41.5f));
            AddExit(exits.transform, "Ramp", new Vector3(12f, -0.9f, -44f), new Vector3(3f, 2.4f, 4f), new Vector3(12f, 0.45f, -50.5f));
            BuilderUtil.SetLayerRecursive(exits, Layers.Water);
        }

        static void AddExit(Transform parent, string name, Vector3 center, Vector3 size, Vector3 topWorld)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var top = new GameObject(WaterExit.TopChildName);
            top.transform.SetParent(go.transform, false);
            top.transform.position = topWorld;
            go.AddComponent<WaterExit>();
        }

        static void AddBellAnchor(NetworkObject bellPrefab)
        {
            GameObject go = Root("BellAnchor", Layers.Default);
            go.transform.position = new Vector3(1.1f, 0.6f, -40.6f);
            go.AddComponent<NetSpawnAnchor>().Configure(bellPrefab);
        }
    }
}
