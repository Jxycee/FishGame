// Builds Spot_HarbourPier.unity (plan maps T17, T17-B; C 6.3, 7.17; M3-D40). Owned by T17. Authority: none (editor; builds scene objects).
// Open sea with a sloping seabed, a beach rising to a grassy plateau, a 3 m pier with a T-head, a stone breakwater, ladders and props, every per-scene
// hook of C 6.3, the World object, 11 stand points and the baked harbour_pier_grid.asset.
// T17-B split: the numbers, the height-field meshes (water, seabed, beach), every collider, the hooks and the bake are code; the visual meshes of the
// pier, breakwater, rocks and props are the Blender FBX of Content/Models/Harbour, placed at the origin with an identity transform as visual-only
// children (no collider). Collider data for the rocks, boulders and props comes from the artist's manifest (HarbourPierManifest), never redrawn here.
// Private helpers marked "mirrors LakeDockBuilder.<Name>" copy the lake's on purpose: this task edits no lake file.
// Deterministic: fixed build order, fixed names, seeded Pcg32 streams (Rng.Stream); the generated meshes are saved in place (GUIDs kept).
// Order in Build(): meshes, NewScene, load assets (C 2.8 #14), roots, ApplySceneLighting, volumes, hooks, stats line, save, FinalizeAfterSave,
// SyncTransforms, bake (F1: the bake runs on the SAVED scene).
// Never name SessionState in this file: with "using UnityEditor;" it would be ambiguous (CS0104).
using System.Collections.Generic;
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
    public static class HarbourPierBuilder
    {
        // Everything the scene needs from disk, loaded once after NewScene (C 2.8 #14).
        sealed class ArtAssets
        {
            public Material Toon, ToonTri, Water;
            public Mesh WaterMesh, Seabed, Beach;
            public Mesh[] VisualMeshes;   // HarbourPierLayout.Visuals order
            // The lake's tree meshes (read-only reuse; LakeDockBuilder builds them earlier in the same BuildAll).
            public Mesh Trunk, CanopyPine, CanopyPineSnow, Blossoms, Branches;
            public Mesh[] CanopyRound;
            public VolumeProfile WarmProfile, CoolProfile;
            public ArtPaletteDef Palette;
            public HarbourPierManifest.Model[] Manifest;
        }

        public static void Build()
        {
            HarbourPierMeshes.BuildAll();   // water, seabed, beach: on disk before the scene references them

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references (C 2.8 #14).
            NetworkObject playerPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.PlayerPrefab);
            NetworkObject bellPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.DockBellPrefab);
            var art = new ArtAssets
            {
                Toon = RenderMaterialLibrary.Get("M_Toon_Base"),
                ToonTri = RenderMaterialLibrary.Get("M_Toon_Tri"),
                Water = RenderMaterialLibrary.Get("M_Water_Surface"),
                WaterMesh = LoadMesh(HarbourPierLayout.WaterMeshPath),
                Seabed = LoadMesh(HarbourPierLayout.SeabedMeshPath),
                Beach = LoadMesh(HarbourPierLayout.BeachMeshPath),
                VisualMeshes = LoadVisualMeshes(),
                Trunk = LoadMesh(LakeDockLayout.TrunkMeshPath),
                CanopyPine = LoadMesh(LakeDockLayout.CanopyPineMeshPath),
                CanopyPineSnow = LoadMesh(LakeDockLayout.CanopyPineSnowMeshPath),
                Blossoms = LoadMesh(LakeDockLayout.BlossomsMeshPath),
                Branches = LoadMesh(LakeDockLayout.BranchesMeshPath),
                CanopyRound = System.Array.ConvertAll(LakeDockLayout.RoundCanopyMeshPaths, LoadMesh),
                WarmProfile = LoadProfile(AssetPaths.WarmVolumeProfile),
                CoolProfile = LoadProfile(AssetPaths.CoolVolumeProfile),
                Palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset),
                Manifest = HarbourPierManifest.Load(),
            };
            // The harbour zone's grid (baked in place below) and the cast gate's minLandDepth.
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null) throw new System.InvalidOperationException("Missing " + AssetPaths.ContentRegistryAsset);
            var harbourSpot = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.HarbourPierSpotAsset);
            if (harbourSpot == null) throw new System.InvalidOperationException("Missing " + AssetPaths.HarbourPierSpotAsset + " (run EcologyContentBuilder)");
            if (harbourSpot.Zone == null) throw new System.InvalidOperationException("harbour_pier spot has no zone (run EcologyContentBuilder)");
            ShoreGridDef harbourGrid = harbourSpot.Zone.ShoreGrid;
            if (harbourGrid == null) throw new System.InvalidOperationException("harbour_pier zone has no shoreGrid");
            float minLandDepth = registry.FishingTuning.MinLandDepth;

            Root("GameScope", Layers.Default).AddComponent<GameScope>();
            AddSpawner(playerPrefab);
            AddSceneCamera();
            AddSun(art.Palette);                                            // roots named exactly Sun, WarmVolume, CoolVolume exist before PresentationSceneObjects.AddTo
            Root("Ambience", Layers.Default).AddComponent<AmbienceBed>();
            AddWater(art);
            AddSeabed(art);
            GameObject beach = AddBeach(art);
            PlaceRocks(beach.transform, art);
            PlaceTrees(beach.transform, art);
            BuilderUtil.SetLayerRecursive(beach, Layers.Shore);   // the Rock_NN colliders and RocksVisual were created on Default
            AddBreakwater(art);
            AddPier(art);
            AddProps(art);
            AddLadders();
            AddSeaBounds();
            AddBellAnchor(bellPrefab);
            RenderingSetupBuilder.ApplySceneLighting();   // RenderSettings are per scene: skybox M_Sky, ExponentialSquared fog, flat ambient
            AddVolume("WarmVolume", art.WarmProfile);
            AddVolume("CoolVolume", art.CoolProfile);
            FishingSceneObjects.AddTo(scene);
            PresentationSceneObjects.AddTo(scene);
            WorldSceneObjects.AddTo(scene, ContentIds.HarbourPier);
            WorldSceneObjects.AddStandPoints(scene, HarbourPierLayout.StandPoints);
            LogStats(scene);

            BuilderUtil.SaveScene(scene, AssetPaths.HarbourPierScene);
            WorldSceneObjects.FinalizeAfterSave(scene, AssetPaths.HarbourPierScene);   // E-CCR-1: GlobalObjectIdHash needs the saved scene
            // The bake runs on the SAVED scene, whose name it records as bakedFromScene. It writes only the grid asset, never the scene.
            Physics.SyncTransforms();                                                    // colliders where the transforms say before any bake ray
            ShoreGridBaker.BakeOpenScene(scene, harbourGrid, minLandDepth);
        }

        // ---- loading ----

        static Mesh LoadMesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) throw new System.IO.FileNotFoundException("Missing mesh " + path);
            return mesh;
        }

        // The one mesh of each harbour FBX (the artist exports one mesh object per FBX at the origin with an identity transform).
        static Mesh[] LoadVisualMeshes()
        {
            var meshes = new Mesh[HarbourPierLayout.Visuals.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                string path = HarbourPierLayout.Visuals[i].Path;
                Mesh found = null;
                int count = 0;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Mesh m) { found = m; count++; }
                }
                if (count != 1) throw new System.InvalidOperationException(path + " holds " + count + " meshes, expected exactly 1 (import it first)");
                meshes[i] = found;
            }
            return meshes;
        }

        static VolumeProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) throw new System.IO.FileNotFoundException("Missing volume profile " + path + " (run RenderingSetupBuilder.Build first)");
            return profile;
        }

        // ---- helpers ----

        // mirrors LakeDockBuilder.Root
        static GameObject Root(string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            return go;
        }

        // A collider-only child: an empty object with one BoxCollider (world position and optional rotation, solid).
        static GameObject AddBox(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = rotation;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static GameObject AddSphere(Transform parent, string name, Vector3 centre, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.AddComponent<SphereCollider>().radius = radius;
            return go;
        }

        // mirrors LakeDockBuilder.AddVisual: a visual-only child (MeshFilter + MeshRenderer, never a collider) at the parent's origin.
        static MeshRenderer AddVisual(Transform parent, string name, Mesh mesh, Material material, Quaternion localRotation, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return renderer;
        }

        // The Blender models of one root, each at the origin with an identity transform: _Tri models on M_Toon_Tri, the rest on M_Toon_Base.
        static void AddModelVisuals(Transform root, string rootName, ArtAssets art)
        {
            for (int i = 0; i < HarbourPierLayout.Visuals.Length; i++)
            {
                HarbourPierLayout.VisualModel visual = HarbourPierLayout.Visuals[i];
                if (visual.Parent != rootName) continue;
                AddVisual(root, visual.Name, art.VisualMeshes[i], visual.Triplanar ? art.ToonTri : art.Toon, Quaternion.identity, true);
            }
        }

        // ---- roots, in the plan's order (T17.4 step 4) ----

        // mirrors LakeDockBuilder.AddSpawner: Spawn0..Spawn3 at (0, 1.55, -55 + 1.5 i), facing +z.
        static void AddSpawner(NetworkObject playerPrefab)
        {
            GameObject root = Root("PlayerSpawner", Layers.Default);
            var points = new Transform[HarbourPierLayout.SpawnCount];
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("Spawn" + i);
                point.transform.SetParent(root.transform, false);
                point.transform.localPosition = new Vector3(0f, HarbourPierLayout.SpawnY, HarbourPierLayout.FirstSpawnZ + HarbourPierLayout.SpawnSpacing * i);
                point.transform.localRotation = Quaternion.identity;
                points[i] = point.transform;
            }
            root.AddComponent<PlayerSpawner>().Configure(playerPrefab, points);
        }

        // mirrors LakeDockBuilder.AddSceneCamera: (0, 6, -70) looking at (0, 1.5, -30), inside the walls.
        static void AddSceneCamera()
        {
            GameObject go = Root("SceneCamera", Layers.Default);
            go.tag = "MainCamera";
            go.transform.position = HarbourPierLayout.SceneCameraPosition;
            go.transform.LookAt(HarbourPierLayout.SceneCameraLookAt);
            Camera camera = go.AddComponent<Camera>();
            camera.nearClipPlane = MovementMath.NearClip;
            camera.farClipPlane = MovementMath.FarClip;
            // Matches PlayerFP (C 7.21): both cameras post-process, so the SceneCameraSwitch hand-over has no colour-grade pop.
            BuilderUtil.GetOrAdd<UniversalAdditionalCameraData>(go).renderPostProcessing = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<SceneCameraSwitch>();
        }

        // mirrors LakeDockBuilder.AddSun: colour and intensity from the palette's Day phase (phases[1]).
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

        // mirrors LakeDockBuilder.AddWater: the visual is a plain GameObject (no collider: CastProbe's first hit must be the WaterVolume), the volume the
        // only Water collider. 240 m visual so the surface does not end at the walls; trigger box (120, 10, 120) centred (0, -5, 0).
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
            box.size = HarbourPierLayout.WaterVolumeSize;
            box.center = HarbourPierLayout.WaterVolumeCentre;
            volume.AddComponent<WaterVolume>();
        }

        // Ground: one mesh serves the renderer and the collider.
        static void AddSeabed(ArtAssets art)
        {
            GameObject go = Root("Seabed", Layers.Ground);
            go.AddComponent<MeshFilter>().sharedMesh = art.Seabed;
            go.AddComponent<MeshRenderer>().sharedMaterial = art.Toon;
            go.AddComponent<MeshCollider>().sharedMesh = art.Seabed;
        }

        // Shore: the beach and plateau (one mesh for renderer and collider) with the Rock_NN, Tree_NN and RocksVisual children.
        static GameObject AddBeach(ArtAssets art)
        {
            GameObject go = Root("Beach", Layers.Shore);
            go.AddComponent<MeshFilter>().sharedMesh = art.Beach;
            go.AddComponent<MeshRenderer>().sharedMaterial = art.Toon;
            go.AddComponent<MeshCollider>().sharedMesh = art.Beach;
            AddModelVisuals(go.transform, HarbourPierLayout.RootBeach, art);
            return go;
        }

        // The 12 rocks: sphere colliders from the manifest (centre, radius). The visual is the one Blender mesh Harbour_Rocks_Tri.
        static void PlaceRocks(Transform beach, ArtAssets art)
        {
            List<HarbourPierManifest.Item> rocks = HarbourPierManifest.Spheres(art.Manifest, HarbourPierLayout.RocksModel, "sphere", HarbourPierLayout.ExpectedRocks);
            for (int i = 0; i < rocks.Count; i++)
                AddSphere(beach, "Rock_" + i.ToString("00"), HarbourPierManifest.ToVector(rocks[i].position), rocks[i].radius);
        }

        // mirrors LakeDockBuilder.PlaceTrees (Shore, reused lake meshes, seasonal visibility), with the harbour's numbers and streams:
        // 16 trees at x [-58, 58], z [-73, -60], height [0.8, 1.3], never within 6 m of the pier root (x 0), the breakwater (x 30) or the hut;
        // canopy shape from harbour_pier.canopy (60% pine), the round trees' foliage variants a balanced shuffle from harbour_pier.foliage.
        static void PlaceTrees(Transform parent, ArtAssets art)
        {
            Pcg32 rng = Rng.Stream(HarbourPierLayout.Seed, HarbourPierLayout.TreeStream, 0UL);
            Pcg32 canopyRng = Rng.Stream(HarbourPierLayout.Seed, HarbourPierLayout.CanopyStream, 0UL);
            var trees = new List<GameObject>();
            var isPine = new List<bool>();
            int placed = 0;
            for (int attempt = 0; attempt < HarbourPierLayout.MaxAttempts && placed < HarbourPierLayout.TreeCount; attempt++)
            {
                float x = rng.Range(HarbourPierLayout.TreeMinX, HarbourPierLayout.TreeMaxX);
                float z = rng.Range(HarbourPierLayout.TreeMinZ, HarbourPierLayout.TreeMaxZ);
                float height = rng.Range(HarbourPierLayout.TreeMinHeight, HarbourPierLayout.TreeMaxHeight);
                if (HarbourPierLayout.TreeBlocked(x, z)) continue;
                var tree = new GameObject("Tree_" + placed.ToString("00"));
                tree.transform.SetParent(parent, false);
                tree.transform.localPosition = new Vector3(x, HarbourPierLayout.BeachY(z), z);
                tree.transform.localScale = new Vector3(height, height, height);
                GameObject trunk = BuilderUtil.EnsurePrimitive(tree.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f),
                    Quaternion.identity, new Vector3(0.4f, 1f, 0.4f), art.Toon, true);
                GameObject canopy = BuilderUtil.EnsurePrimitive(tree.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 2.6f, 0f),
                    Quaternion.identity, new Vector3(2.4f, 2.4f, 2.4f), art.ToonTri, true);
                trunk.GetComponent<MeshFilter>().sharedMesh = art.Trunk;
                bool pine = canopyRng.NextFloat() < LakeDockLayout.PineChance;
                canopy.GetComponent<MeshFilter>().sharedMesh = pine ? art.CanopyPine : art.CanopyRound[0];
                trees.Add(tree);
                isPine.Add(pine);
                placed++;
            }
            if (placed < HarbourPierLayout.TreeCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + HarbourPierLayout.TreeCount + " trees");

            int roundCount = 0;
            foreach (bool pine in isPine) if (!pine) roundCount++;
            Pcg32 foliageRng = Rng.Stream(HarbourPierLayout.Seed, HarbourPierLayout.FoliageStream, 0UL);
            int[] variants = SeasonalLook.BalancedVariants(ref foliageRng, roundCount, LakeDockLayout.RoundFoliageSlots.Length);
            int round = 0;
            for (int i = 0; i < trees.Count; i++)
            {
                if (isPine[i]) AddPineSeasons(trees[i], art);
                else AddRoundSeasons(trees[i], i, variants[round++], art);
                BuilderUtil.SetLayerRecursive(trees[i], Layers.Shore);
            }
        }

        // mirrors LakeDockBuilder.AddRoundSeasons
        static void AddRoundSeasons(GameObject tree, int index, int variant, ArtAssets art)
        {
            Transform canopy = tree.transform.Find("Canopy");
            canopy.GetComponent<MeshFilter>().sharedMesh = art.CanopyRound[variant];
            MeshRenderer blossoms = AddVisual(canopy, "Blossoms", art.Blossoms, art.ToonTri, Quaternion.identity, false);
            MeshRenderer branches = AddVisual(tree.transform, "Branches", art.Branches, art.Toon,
                Quaternion.Euler(0f, (index * LakeDockLayout.BranchesYawStepDegrees) % 360f, 0f), true);
            tree.AddComponent<SeasonalVisibility>().Configure(new[]
            {
                new SeasonalVisibility.Entry(canopy.GetComponent<MeshRenderer>(), SeasonalLook.MaskLeafy),
                new SeasonalVisibility.Entry(blossoms, SeasonalLook.MaskSpring),
                new SeasonalVisibility.Entry(branches, SeasonalLook.MaskWinter),
            });
        }

        // mirrors LakeDockBuilder.AddPineSeasons
        static void AddPineSeasons(GameObject tree, ArtAssets art)
        {
            Transform canopy = tree.transform.Find("Canopy");
            MeshRenderer snowy = AddVisual(canopy, "CanopyWinter", art.CanopyPineSnow, art.ToonTri, Quaternion.identity, true);
            tree.AddComponent<SeasonalVisibility>().Configure(new[]
            {
                new SeasonalVisibility.Entry(canopy.GetComponent<MeshRenderer>(), SeasonalLook.MaskLeafy),
                new SeasonalVisibility.Entry(snowy, SeasonalLook.MaskWinter),
            });
        }

        // Shore: the walkable box (x 28..32, z -56..-25, y -6..1.5) on the root, one sphere per boulder and per rubble stone (manifest centres and radii;
        // the boulder spheres' centres are lowered so no collider surface is above the walkable top, see HarbourPierLayout.BoulderColliderCentreY),
        // and the Blender visuals (no collider).
        static void AddBreakwater(ArtAssets art)
        {
            GameObject root = Root(HarbourPierLayout.RootBreakwater, Layers.Shore);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = HarbourPierLayout.BreakwaterBox.center;
            box.size = HarbourPierLayout.BreakwaterBox.size;

            List<HarbourPierManifest.Item> boulders = HarbourPierManifest.Spheres(art.Manifest, HarbourPierLayout.BoulderModel, "sphere", HarbourPierLayout.ExpectedBoulders);
            for (int i = 0; i < boulders.Count; i++)
            {
                Vector3 c = HarbourPierManifest.ToVector(boulders[i].position);
                c.y = HarbourPierLayout.BoulderColliderCentreY(c.y, boulders[i].radius);
                AddSphere(root.transform, "Boulder_" + i.ToString("00"), c, boulders[i].radius);
            }
            List<HarbourPierManifest.Item> rubble = HarbourPierManifest.Spheres(art.Manifest, HarbourPierLayout.RubbleModel, "none (underwater", HarbourPierLayout.ExpectedRubble);
            for (int i = 0; i < rubble.Count; i++)
                AddSphere(root.transform, "Rubble_" + i.ToString("00"), HarbourPierManifest.ToVector(rubble[i].position), rubble[i].radius);

            AddModelVisuals(root.transform, HarbourPierLayout.RootBreakwater, art);
            BuilderUtil.SetLayerRecursive(root, Layers.Shore);
        }

        // Structure: collider-only Deck, THead, PierRamp, Post_NN, Ladder_Mid, Ladder_Head, plus the Blender visuals (no collider).
        static void AddPier(ArtAssets art)
        {
            GameObject root = Root(HarbourPierLayout.RootPier, Layers.Structure);
            Transform t = root.transform;
            float topY = HarbourPierLayout.DeckTopY, thick = HarbourPierLayout.DeckThickness;
            float deckLength = HarbourPierLayout.DeckEndZ - HarbourPierLayout.DeckRootZ;
            AddBox(t, "Deck", new Vector3(0f, topY - thick * 0.5f, (HarbourPierLayout.DeckRootZ + HarbourPierLayout.DeckEndZ) * 0.5f),
                new Vector3(HarbourPierLayout.DeckHalfWidth * 2f, thick, deckLength), Quaternion.identity);
            AddBox(t, "THead", new Vector3(0f, topY - thick * 0.5f, (HarbourPierLayout.THeadNearZ + HarbourPierLayout.THeadFarZ) * 0.5f),
                new Vector3(HarbourPierLayout.THeadHalfWidth * 2f, thick, HarbourPierLayout.THeadFarZ - HarbourPierLayout.THeadNearZ), Quaternion.identity);

            // mirrors the lake's RampDeck: a box of the ramp width and thickness along low -> high.
            Vector3 low = HarbourPierLayout.PierRampLow, high = HarbourPierLayout.PierRampHigh;
            Vector3 along = high - low;
            AddBox(t, "PierRamp", (low + high) * 0.5f, new Vector3(HarbourPierLayout.PierRampWidth, HarbourPierLayout.PierRampThickness, along.magnitude),
                Quaternion.LookRotation(along.normalized, Vector3.up));

            float underside = HarbourPierLayout.DeckUndersideY;
            for (int i = 0; i < HarbourPierLayout.Posts.Length; i++)
            {
                Vector2 p = HarbourPierLayout.Posts[i];
                float terrain = HarbourPierLayout.TerrainY(p.x, p.y);
                AddBox(t, "Post_" + i.ToString("00"), new Vector3(p.x, (terrain + underside) * 0.5f, p.y),
                    new Vector3(HarbourPierLayout.PostCollider, underside - terrain, HarbourPierLayout.PostCollider), Quaternion.identity);
            }

            AddBox(t, "Ladder_Mid", HarbourPierLayout.LadderMidRailBox.center, HarbourPierLayout.LadderMidRailBox.size, Quaternion.identity);
            AddBox(t, "Ladder_Head", HarbourPierLayout.LadderHeadRailBox.center, HarbourPierLayout.LadderHeadRailBox.size, Quaternion.identity);

            AddModelVisuals(t, HarbourPierLayout.RootPier, art);
            BuilderUtil.SetLayerRecursive(root, Layers.Structure);
        }

        // Structure: one solid box per manifest box item (hut, bollards, crates, barrels, lamps, lobster pots, the pier sign, the three boats); buoys, rope
        // coils, the hut net and the life ring have none. The Blender props, dressing and boats are visual-only children.
        static void AddProps(ArtAssets art)
        {
            GameObject root = Root(HarbourPierLayout.RootProps, Layers.Structure);
            foreach (string modelName in HarbourPierLayout.PropBoxModels)
            {
                foreach (HarbourPierManifest.Item item in HarbourPierManifest.Find(art.Manifest, modelName).items)
                {
                    if (item.collider == null || !item.collider.StartsWith("box", System.StringComparison.Ordinal)) continue;
                    Vector3 min = HarbourPierManifest.ToVector(item.bounds_min), max = HarbourPierManifest.ToVector(item.bounds_max);
                    AddBox(root.transform, item.name, (min + max) * 0.5f, max - min, Quaternion.identity);
                }
            }
            AddModelVisuals(root.transform, HarbourPierLayout.RootProps, art);
            BuilderUtil.SetLayerRecursive(root, Layers.Structure);
        }

        // R70: walk-in LadderVolume triggers on Default (CastProbe's mask never sees them), no rotation on the object or its parents; no WaterExit here.
        static void AddLadders()
        {
            GameObject root = Root("Ladders", Layers.Default);
            AddLadderVolume(root.transform, "LadderMid", HarbourPierLayout.LadderMidVolume);
            AddLadderVolume(root.transform, "LadderHead", HarbourPierLayout.LadderHeadVolume);
        }

        // mirrors LakeDockBuilder.AddLadderVolume
        static void AddLadderVolume(Transform parent, string name, Bounds volume)
        {
            var go = new GameObject(name);
            go.layer = Layers.Default;
            go.transform.SetParent(parent, false);
            go.transform.position = volume.center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = volume.size;
            go.AddComponent<LadderVolume>();
        }

        // Invisible boundary walls on Default at x +-60.5 and z 60.5 / -75.5 (open question 2): boxes 1 m thick, y -12..6, no renderer. Default means
        // neither CastProbe (Water | Ground | Shore | Structure) nor the bake (Water | Shore | Ground) sees them.
        static void AddSeaBounds()
        {
            GameObject root = Root("SeaBounds", Layers.Default);
            float h = HarbourPierLayout.WallMaxY - HarbourPierLayout.WallMinY, y = (HarbourPierLayout.WallMaxY + HarbourPierLayout.WallMinY) * 0.5f;
            float ew = HarbourPierLayout.WallEastWestX, n = HarbourPierLayout.WallNorthZ, s = HarbourPierLayout.WallSouthZ, w = HarbourPierLayout.WallThickness;
            float zLength = (n + 0.5f) - (s + 0.5f), zCentre = ((n + 0.5f) + (s + 0.5f)) * 0.5f;   // East and West: z -75.5..61
            float xLength = 2f * (ew + 0.5f);                                                       // North and South: x -61..61
            AddBox(root.transform, "West", new Vector3(-ew, y, zCentre), new Vector3(w, h, zLength), Quaternion.identity);
            AddBox(root.transform, "East", new Vector3(ew, y, zCentre), new Vector3(w, h, zLength), Quaternion.identity);
            AddBox(root.transform, "North", new Vector3(0f, y, n), new Vector3(xLength, h, w), Quaternion.identity);
            AddBox(root.transform, "South", new Vector3(0f, y, s), new Vector3(xLength, h, w), Quaternion.identity);
            BuilderUtil.SetLayerRecursive(root, Layers.Default);
        }

        // mirrors LakeDockBuilder.AddVolume (C 6.2 / 6.4): global volume, weight 0, priority 0.
        static void AddVolume(string name, VolumeProfile profile)
        {
            GameObject go = Root(name, Layers.Default);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 0f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        // mirrors LakeDockBuilder.AddBellAnchor
        static void AddBellAnchor(NetworkObject bellPrefab)
        {
            GameObject go = Root("BellAnchor", Layers.Default);
            go.transform.position = HarbourPierLayout.BellAnchorPosition;
            go.AddComponent<NetSpawnAnchor>().Configure(bellPrefab);
        }

        // mirrors LakeDockBuilder.LogStats: counts over every MeshRenderer in the scene (triangles summed per renderer, i.e. per draw), plus the
        // triangle count of each Harbour mesh (the generated ones and the Blender models, which carry the same Harbour_ names).
        static void LogStats(Scene scene)
        {
            int renderers = 0, triangles = 0;
            var meshes = new List<Mesh>();
            var materials = new HashSet<Material>();
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
            Debug.Log("FG HARBOUR renderers=" + renderers + " triangles=" + triangles + " meshes=" + meshes.Count + " materials=" + materials.Count);
            var perMesh = new System.Text.StringBuilder("FG HARBOUR_MESHES");
            foreach (Mesh mesh in meshes)
            {
                if (!mesh.name.StartsWith("Harbour_", System.StringComparison.Ordinal)) continue;
                int tris = 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) tris += (int)(mesh.GetIndexCount(sub) / 3);
                perMesh.Append(' ').Append(mesh.name).Append('=').Append(tris);
            }
            Debug.Log(perMesh.ToString());
        }
    }
}
