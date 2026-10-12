// Builds Spot_LakeDock.unity (contract 6.5, spec 17.1 item 9, D34, D36; M2 look: contract 6.4 / 7.28, spec 10.1-10.4). Owned by T15; T28 owns the M2 rebuild.
// Deterministic: fixed build order, fixed names, seeded Pcg32 streams (Rng.Stream); the meshes are saved in place (GUIDs kept).
// Physics fingerprint (plan T28.1): every M1 collider, exit, spawn, the bell anchor and the lakebed and shore numbers are unchanged. The M2 look is
// MeshBuilder props on M_Toon_Base (collider-only primitives underneath), the FishGame/Water grid, ApplySceneLighting (sky + fog) and the two volumes.
// Order in Build(): meshes, NewScene, load assets (C 2.8 #14), roots in M1 order, ApplySceneLighting, volumes, both hooks, stats line, save.
// Never name SessionState in this file: with "using UnityEditor;" it would be ambiguous (CS0104).
using FishGame.App;
using FishGame.Core;
using FishGame.Data;
using FishGame.Gameplay;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class LakeDockBuilder
    {
        // Everything the scene needs from disk, loaded once after NewScene (C 2.8 #14).
        sealed class ArtAssets
        {
            public Material Toon, Water;
            public Mesh WaterMesh, Lakebed, Shore, Rock, Trunk, CanopyRound, CanopyPine, Dock, Reeds;
            public VolumeProfile WarmProfile, CoolProfile;
            public ArtPaletteDef Palette;
        }

        public static void Build()
        {
            LakeDockMeshes.BuildAll(); // saved in place, on disk before the scene references them

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references (C 2.8 #14).
            NetworkObject playerPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.PlayerPrefab);
            NetworkObject bellPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.DockBellPrefab);
            var art = new ArtAssets
            {
                Toon = RenderMaterialLibrary.Get("M_Toon_Base"),
                Water = RenderMaterialLibrary.Get("M_Water_Surface"),
                WaterMesh = LoadMesh(LakeDockLayout.WaterMeshPath),
                Lakebed = LoadMesh(AssetPaths.LakebedMesh),
                Shore = LoadMesh(AssetPaths.ShoreMesh),
                Rock = LoadMesh(LakeDockLayout.RockMeshPath),
                Trunk = LoadMesh(LakeDockLayout.TrunkMeshPath),
                CanopyRound = LoadMesh(LakeDockLayout.CanopyRoundMeshPath),
                CanopyPine = LoadMesh(LakeDockLayout.CanopyPineMeshPath),
                Dock = LoadMesh(LakeDockLayout.DockMeshPath),
                Reeds = LoadMesh(LakeDockLayout.ReedsMeshPath),
                WarmProfile = LoadProfile(AssetPaths.WarmVolumeProfile),
                CoolProfile = LoadProfile(AssetPaths.CoolVolumeProfile),
                Palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset),
            };

            Root("GameScope", Layers.Default).AddComponent<GameScope>();
            AddSpawner(playerPrefab);
            AddSceneCamera();
            AddSun(art.Palette);
            Root("Ambience", Layers.Default).AddComponent<AmbienceBed>();
            AddWater(art);
            AddSurface("Lakebed", Layers.Ground, art.Lakebed, art.Toon);
            GameObject shore = AddSurface("Shore", Layers.Shore, art.Shore, art.Toon);
            PlaceRocks(shore.transform, art);
            PlaceTrees(shore.transform, art);
            AddDock(art);
            AddReeds(art);
            AddExits();
            AddBellAnchor(bellPrefab);
            RenderingSetupBuilder.ApplySceneLighting(); // RenderSettings are per scene: skybox M_Sky, ExponentialSquared fog 0.008, flat ambient
            AddVolume("WarmVolume", art.WarmProfile);
            AddVolume("CoolVolume", art.CoolProfile);
            FishingSceneObjects.AddTo(scene);
            PresentationSceneObjects.AddTo(scene);
            LogStats(scene);

            BuilderUtil.SaveScene(scene, AssetPaths.LakeDockScene);
        }

        static Mesh LoadMesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) throw new System.IO.FileNotFoundException("Missing mesh " + path);
            return mesh;
        }

        static VolumeProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) throw new System.IO.FileNotFoundException("Missing volume profile " + path + " (run RenderingSetupBuilder.Build first)");
            return profile;
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
            // Matches PlayerFP (C 7.21): both cameras post-process, so the SceneCameraSwitch hand-over has no colour-grade pop.
            BuilderUtil.GetOrAdd<UniversalAdditionalCameraData>(go).renderPostProcessing = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<SceneCameraSwitch>();
        }

        // Rotation as M1. Colour and intensity come from the palette's Day phase (phases[1], the fixed M2 phase). A missing palette or Day phase throws
        // before anything is created, with the same exception type as RenderingSetupBuilder.ApplySceneLighting.
        static void AddSun(ArtPaletteDef palette)
        {
            if (palette == null) throw new System.InvalidOperationException("ArtPalette asset missing: " + AssetPaths.ArtPaletteAsset);
            if (palette.Phases.Count < 2) throw new System.InvalidOperationException("ArtPalette has no Day phase (phases[1]): " + AssetPaths.ArtPaletteAsset);
            GameObject go = Root("Sun", Layers.Default);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.color = palette.Phases[1].lightColor;
            light.intensity = palette.Phases[1].lightIntensity;
        }

        // Spec 10.4: a 96 x 96 cell grid over 120 m at y 0 (FishGame/Water) plus the M1 trigger box whose top face is y 0.
        // The visual is a plain GameObject, never a primitive: it has no collider, which would break CastProbe's "first hit is water" rule (C 7.17).
        static void AddWater(ArtAssets art)
        {
            GameObject visual = Root("WaterVisual", Layers.Water);
            visual.transform.position = Vector3.zero;
            visual.AddComponent<MeshFilter>().sharedMesh = art.WaterMesh;
            MeshRenderer water = visual.AddComponent<MeshRenderer>();
            water.sharedMaterial = art.Water;
            water.shadowCastingMode = ShadowCastingMode.Off;
            water.receiveShadows = false;

            GameObject volume = Root("WaterVolume", Layers.Water);
            BoxCollider box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(120f, 8f, 120f);
            box.center = new Vector3(0f, -4f, 0f);
            volume.AddComponent<WaterVolume>();
        }

        static GameObject AddSurface(string name, int layer, Mesh mesh, Material material)
        {
            GameObject go = Root(name, layer);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        // The M1 loop is untouched (same stream, same draws, same primitives and colliders); only the mesh is swapped afterwards.
        static void PlaceRocks(Transform parent, ArtAssets art)
        {
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
                    new Vector3(size, size * 0.7f, size), art.Toon, true);
                rock.GetComponent<MeshFilter>().sharedMesh = art.Rock;
                BuilderUtil.SetLayerRecursive(rock, Layers.Shore);
                placed++;
            }
            if (placed < LakeDockLayout.RockCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + LakeDockLayout.RockCount + " rocks");
        }

        // The M1 loop is untouched; the canopy shape is one draw per placed tree from the NEW stream lake_dock.canopy (60% pine, 40% round).
        static void PlaceTrees(Transform parent, ArtAssets art)
        {
            Pcg32 rng = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.TreeStream, 0UL);
            Pcg32 canopyRng = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.CanopyStream, 0UL);
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
                GameObject trunk = BuilderUtil.EnsurePrimitive(tree.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f),
                    Quaternion.identity, new Vector3(0.4f, 1f, 0.4f), art.Toon, true);
                GameObject canopy = BuilderUtil.EnsurePrimitive(tree.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 2.6f, 0f),
                    Quaternion.identity, new Vector3(2.4f, 2.4f, 2.4f), art.Toon, true);
                trunk.GetComponent<MeshFilter>().sharedMesh = art.Trunk;
                canopy.GetComponent<MeshFilter>().sharedMesh = canopyRng.NextFloat() < LakeDockLayout.PineChance ? art.CanopyPine : art.CanopyRound;
                BuilderUtil.SetLayerRecursive(tree, Layers.Shore);
                placed++;
            }
            if (placed < LakeDockLayout.TreeCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + LakeDockLayout.TreeCount + " trees");
        }

        // Deck 3 x 12 m (x -1.5..1.5, z -52..-40), top y 0.6; posts every 2 m at x +-1.4 down to the bed; ladders at z -41.5; ramp at x 12.
        // The M1 boxes stay as collider-only objects (same transforms and BoxColliders); the look is the combined child DockVisual (no collider).
        static void AddDock(ArtAssets art)
        {
            GameObject dock = Root("Dock", Layers.Structure);
            Transform t = dock.transform;
            Material material = art.Toon;

            float deckLength = LakeDockLayout.DeckFarZ - LakeDockLayout.DeckNearZ;
            float deckUnderside = LakeDockLayout.DeckTopY - LakeDockLayout.DeckThickness;
            BuilderUtil.EnsurePrimitive(t, "Deck", PrimitiveType.Cube,
                new Vector3(0f, LakeDockLayout.DeckTopY - LakeDockLayout.DeckThickness * 0.5f, (LakeDockLayout.DeckNearZ + LakeDockLayout.DeckFarZ) * 0.5f),
                Quaternion.identity, new Vector3(LakeDockLayout.DeckWidth, LakeDockLayout.DeckThickness, deckLength), material, true);

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
                        new Vector3(LakeDockLayout.PostSize, deckUnderside - bed, LakeDockLayout.PostSize), material, true);
                }
            }

            AddLadder(t, "Ladder_L", -LakeDockLayout.LadderX, material);
            AddLadder(t, "Ladder_R", LakeDockLayout.LadderX, material);

            Vector3 low = LakeDockLayout.RampLow, high = LakeDockLayout.RampHigh;
            Vector3 along = high - low;
            BuilderUtil.EnsurePrimitive(t, "RampDeck", PrimitiveType.Cube, (low + high) * 0.5f,
                Quaternion.LookRotation(along.normalized, Vector3.up),
                new Vector3(LakeDockLayout.RampWidth, LakeDockLayout.RampThickness, along.magnitude), material, true);

            foreach (Transform child in t) StripRenderer(child.gameObject); // collider-only from here on

            var visual = new GameObject("DockVisual");
            visual.transform.SetParent(t, false);
            visual.AddComponent<MeshFilter>().sharedMesh = art.Dock;
            visual.AddComponent<MeshRenderer>().sharedMaterial = art.Toon;

            BuilderUtil.SetLayerRecursive(dock, Layers.Structure);
        }

        static void AddLadder(Transform parent, string name, float x, Material material)
        {
            float bed = LakeDockLayout.BedYAt(x, LakeDockLayout.LadderZ);
            float top = LakeDockLayout.DeckTopY;
            BuilderUtil.EnsurePrimitive(parent, name, PrimitiveType.Cube, new Vector3(x, (top + bed) * 0.5f, LakeDockLayout.LadderZ),
                Quaternion.identity, new Vector3(LakeDockLayout.LadderThickness, top - bed, LakeDockLayout.LadderWidth), material, true);
        }

        // The primitive's MeshRenderer and MeshFilter go; the collider stays.
        static void StripRenderer(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        }

        // One combined reeds mesh in the shallows: visual only (a collider would break CastProbe's first-hit-is-water rule), no shadows.
        static void AddReeds(ArtAssets art)
        {
            GameObject reeds = Root("Reeds", Layers.Default);
            reeds.AddComponent<MeshFilter>().sharedMesh = art.Reeds;
            MeshRenderer renderer = reeds.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = art.Toon;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // C 6.2 / 6.4: global volume, weight 0 (a later milestone drives it), priority 0, profile built by RenderingSetupBuilder.
        static void AddVolume(string name, VolumeProfile profile)
        {
            GameObject go = Root(name, Layers.Default);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 0f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
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

        // One info line for the spec 10.7 record (T30): counts over every MeshRenderer in the scene (triangles are summed per renderer, i.e. per draw),
        // plus the triangle count of each LakeDock mesh.
        static void LogStats(Scene scene)
        {
            int renderers = 0, triangles = 0;
            var meshes = new System.Collections.Generic.List<Mesh>();
            var materials = new System.Collections.Generic.HashSet<Material>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderers++;
                    foreach (Material material in renderer.sharedMaterials) if (material != null) materials.Add(material);
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Mesh mesh = filter.sharedMesh;
                    if (!meshes.Contains(mesh)) meshes.Add(mesh);
                    for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += (int)(mesh.GetIndexCount(sub) / 3);
                }
            }
            Debug.Log("FG LAKEDOCK renderers=" + renderers + " triangles=" + triangles + " meshes=" + meshes.Count + " materials=" + materials.Count);
            var perMesh = new System.Text.StringBuilder("FG LAKEDOCK_MESHES");
            foreach (Mesh mesh in meshes)
                if (mesh.name.StartsWith("LakeDock_")) perMesh.Append(' ').Append(mesh.name).Append('=').Append(mesh.triangles.Length / 3);
            Debug.Log(perMesh.ToString());
        }
    }
}
