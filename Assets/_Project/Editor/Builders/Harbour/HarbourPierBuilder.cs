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
    public static partial class HarbourPierBuilder
    {
        // Everything the scene needs from disk, loaded once after NewScene (C 2.8 #14).
        sealed class ArtAssets
        {
            public Material Toon, ToonTri, Water, Creek, Foliage;
            public Mesh WaterMesh, WaterSkirt, Seabed, Beach;
            public Mesh[] VisualMeshes;   // HarbourPierLayout.Visuals order
            public Mesh Branches;   // the lake's bare winter branches (read-only reuse; LakeDockBuilder builds it earlier in the same BuildAll)
            public VolumeProfile WarmProfile, CoolProfile;
            public ArtPaletteDef Palette;
            public HarbourPierManifest.Model[] Manifest;
            // pass 2 (HarbourPierBuilder.Pass2.cs)
            public Material Glow, GlowAll, Beam, Smoke;
            public Mesh Sphere, BeamMesh, SearchBeamMesh, VaneHead, VaneBase;
        }

        public static void Build()
        {
            HarbourPierMeshes.BuildAll();   // water, seabed, beach: on disk before the scene references them
            HarbourGroundCover.BuildMeshes();   // round 2b: the ground-cover tuft meshes
            HarbourBedMap.Build();              // round 3: T_HarbourBedMap on M_Water_Harbour (the water reads the natural bed)
            // Pass 2: the weather vane split (head turns) and the lighthouse beam, saved in place before the scene references them.
            BuildWeatherVaneMeshes(LoadModelMesh(HarbourPierLayout.WeatherVaneModel));
            HarbourPierMeshes.Save(HarbourPierLayout.BeaconBeamMeshPath, BuildBeaconBeam());
            HarbourPierMeshes.Save(HarbourPierLayout.SearchBeamMeshPath, BuildSearchBeam());   // round 3: the hill searchlight
            HarbourPierMeshes.Save(HarbourPierLayout.GlowSphereMeshPath, BuildGlowSphere());   // round 1: 112 triangles instead of Unity's 768
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references (C 2.8 #14).
            NetworkObject playerPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.PlayerPrefab);
            NetworkObject bellPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.DockBellPrefab);
            var art = new ArtAssets
            {
                Toon = RenderMaterialLibrary.Get("M_Toon_Base"),
                ToonTri = RenderMaterialLibrary.Get("M_Toon_Tri"),
                Foliage = RenderMaterialLibrary.Get("M_Toon_Foliage"),
                Water = RenderMaterialLibrary.Get(HarbourBedMap.MaterialName),   // round 3 (was M_Water_Surface: the same look plus the bed map)
                Creek = RenderMaterialLibrary.Get("M_Water_Creek"),
                WaterMesh = LoadMesh(HarbourPierLayout.WaterMeshPath),
                WaterSkirt = LoadMesh(HarbourPierLayout.WaterSkirtMeshPath),
                Seabed = LoadMesh(HarbourPierLayout.SeabedMeshPath),
                Beach = LoadMesh(HarbourPierLayout.BeachMeshPath),
                VisualMeshes = LoadVisualMeshes(),
                Branches = LoadMesh(LakeDockLayout.BranchesMeshPath),
                WarmProfile = LoadProfile(AssetPaths.WarmVolumeProfile),
                CoolProfile = LoadProfile(AssetPaths.CoolVolumeProfile),
                Palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset),
                Manifest = HarbourPierManifest.Load(),
                Glow = RenderMaterialLibrary.Get("M_LampGlow"),
                GlowAll = RenderMaterialLibrary.Get("M_LampGlow_All"),
                Beam = RenderMaterialLibrary.Get("M_BeaconBeam"),
                Smoke = RenderMaterialLibrary.Get("M_Smoke"),
                Sphere = LoadMesh(HarbourPierLayout.GlowSphereMeshPath),
                BeamMesh = LoadMesh(HarbourPierLayout.BeaconBeamMeshPath),
                SearchBeamMesh = LoadMesh(HarbourPierLayout.SearchBeamMeshPath),
                VaneHead = LoadMesh(HarbourPierLayout.WeatherVaneHeadMeshPath),
                VaneBase = LoadMesh(HarbourPierLayout.WeatherVaneBaseMeshPath),
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
            AddPass2(art);
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

        static Mesh LoadModelMesh(string meshName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(HarbourPierLayout.ModelsFolder + "/" + meshName + ".fbx"))
                if (asset is Mesh m) return m;
            throw new System.IO.FileNotFoundException("Missing model " + meshName + ".fbx (import it first)");
        }

        // The glow spheres' mesh (round 1): a UV sphere of radius 0.5 like Unity's built-in one (the glow objects scale it to their diameter), but
        // 8 sides x 8 rings = 112 triangles instead of 768. The glows are small and additive, so the facets never read; at dusk the overview sees
        // about 20 of them. Normals point out, uv0 = (longitude, latitude). Deterministic.
        static Mesh BuildGlowSphere()
        {
            const int sides = 8, rings = 8;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float v = (float)j / rings, lat = Mathf.PI * (v - 0.5f);
                for (int i = 0; i <= sides; i++)
                {
                    float u = (float)i / sides, lon = 2f * Mathf.PI * u;
                    var n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    verts.Add(n * 0.5f); normals.Add(n); uvs.Add(new Vector2(u, v));
                }
            }
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int a = j * (sides + 1) + i, b = a + sides + 1;
                    if (j > 0) tris.AddRange(new[] { a, b, a + 1 });                // clockwise seen from outside (Unity front faces)
                    if (j < rings - 1) tris.AddRange(new[] { a + 1, b, b + 1 });
                }
            }
            var mesh = new Mesh { name = "Harbour_GlowSphere" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
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
                // The rubble base lies under the water: it casts no shadow.
                AddVisual(root, visual.Name, art.VisualMeshes[i], visual.Triplanar ? art.ToonTri : art.Toon, Quaternion.identity, visual.Name != "BreakwaterBaseVisual");
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

            // The sea beyond the 240 m grid, out to the far clip plane (fog cannot hide the grid's edge: Water.shader blends only part of it and the
            // day-night controller owns the density). Same layer, material and flags as the grid; no collider; its inner edge is the grid's border.
            var skirt = new GameObject("WaterSkirt") { layer = Layers.Water };
            skirt.transform.SetParent(visual.transform, false);
            skirt.AddComponent<MeshFilter>().sharedMesh = art.WaterSkirt;
            MeshRenderer skirtRenderer = skirt.AddComponent<MeshRenderer>();
            skirtRenderer.sharedMaterial = art.Water;
            skirtRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skirtRenderer.receiveShadows = false;

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
            MeshRenderer seabed = go.AddComponent<MeshRenderer>();
            seabed.sharedMaterial = art.Toon;
            seabed.shadowCastingMode = ShadowCastingMode.Off;   // the sea floor casts nothing onto anything visible
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

        // mirrors LakeDockBuilder.PlaceTrees (Shore) with the harbour's numbers and streams: 16 trees at x [-58, 58], z [-73, -60], height [0.8, 1.3],
        // never within 6 m of the pier root (x 0), the breakwater (x 30) or the hut; pine or round from harbour_pier.canopy (60% pine).
        // Round 0 (2026-10-14): the Trunk (capsule) and Canopy (sphere) primitives keep their colliders exactly as before but draw nothing; the
        // visual is the painterly Blender code tree the manifest places at this Tree_NN (Harbour_Core_CodeTree_*, a local model under the tree root,
        // so the root's localScale, the Pcg32 height, scales it). The manifest's ported position and kind must match the Pcg32 draw (checked here).
        // Seasons: a round tree shows its leafy Blender crown in spring to autumn and the lake's bare Branches mesh in winter; a pine is evergreen.
        static void PlaceTrees(Transform parent, ArtAssets art)
        {
            Pcg32 rng = Rng.Stream(HarbourPierLayout.Seed, HarbourPierLayout.TreeStream, 0UL);
            Pcg32 canopyRng = Rng.Stream(HarbourPierLayout.Seed, HarbourPierLayout.CanopyStream, 0UL);
            Dictionary<string, (Mesh mesh, HarbourPierManifest.Placement at)> codeTrees = CodeTreePlacements(art, false);
            Dictionary<string, (Mesh mesh, HarbourPierManifest.Placement at)> codeCrowns = CodeTreePlacements(art, true);   // round 2b: crown cards
            int placed = 0;
            for (int attempt = 0; attempt < HarbourPierLayout.MaxAttempts && placed < HarbourPierLayout.TreeCount; attempt++)
            {
                float x = rng.Range(HarbourPierLayout.TreeMinX, HarbourPierLayout.TreeMaxX);
                float z = rng.Range(HarbourPierLayout.TreeMinZ, HarbourPierLayout.TreeMaxZ);
                float height = rng.Range(HarbourPierLayout.TreeMinHeight, HarbourPierLayout.TreeMaxHeight);
                if (HarbourPierLayout.TreeBlocked(x, z)) continue;
                string name = "Tree_" + placed.ToString("00");
                var tree = new GameObject(name);
                tree.transform.SetParent(parent, false);
                tree.transform.localPosition = new Vector3(x, HarbourPierLayout.BeachY(z), z);
                tree.transform.localScale = new Vector3(height, height, height);
                GameObject trunk = BuilderUtil.EnsurePrimitive(tree.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f),
                    Quaternion.identity, new Vector3(0.4f, 1f, 0.4f), null, true);
                GameObject canopy = BuilderUtil.EnsurePrimitive(tree.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 2.6f, 0f),
                    Quaternion.identity, new Vector3(2.4f, 2.4f, 2.4f), null, true);
                StripRenderer(trunk);
                StripRenderer(canopy);
                bool pine = canopyRng.NextFloat() < LakeDockLayout.PineChance;

                if (!codeTrees.TryGetValue(name, out var codeTree)) throw new System.InvalidOperationException("the manifest places no code tree at " + name);
                Vector3 ported = HarbourPierManifest.ToVector(codeTree.at.position);
                if (Mathf.Abs(ported.x - x) > 0.011f || Mathf.Abs(ported.z - z) > 0.011f || codeTree.at.pine != pine
                    || Mathf.Abs(codeTree.at.height_scale - height) > 0.011f)
                    throw new System.InvalidOperationException(name + ": the manifest's code tree (" + ported + ", pine " + codeTree.at.pine + ", h " + codeTree.at.height_scale
                                                               + ") differs from the Pcg32 draw (" + x + ", " + z + ", pine " + pine + ", h " + height + "): re-port CODE_TREES in fg_harbour.py");
                MeshRenderer visual = AddVisual(tree.transform, "TreeVisual", codeTree.mesh, art.ToonTri, Quaternion.Euler(0f, codeTree.at.yaw_deg, 0f), true);
                if (!codeCrowns.TryGetValue(name, out var crown)) throw new System.InvalidOperationException("the manifest places no crown cards at " + name);
                MeshRenderer crownVisual = AddVisual(tree.transform, "TreeCrowns", crown.mesh, art.Foliage, Quaternion.Euler(0f, crown.at.yaw_deg, 0f), true);
                if (!pine)
                {
                    MeshRenderer branches = AddVisual(tree.transform, "Branches", art.Branches, art.Toon,
                        Quaternion.Euler(0f, (placed * LakeDockLayout.BranchesYawStepDegrees) % 360f, 0f), true);
                    tree.AddComponent<SeasonalVisibility>().Configure(new[]
                    {
                        new SeasonalVisibility.Entry(visual, SeasonalLook.MaskLeafy),
                        new SeasonalVisibility.Entry(crownVisual, SeasonalLook.MaskLeafy),
                        new SeasonalVisibility.Entry(branches, SeasonalLook.MaskWinter),
                    });
                }
                BuilderUtil.SetLayerRecursive(tree, Layers.Shore);
                placed++;
            }
            if (placed < HarbourPierLayout.TreeCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + HarbourPierLayout.TreeCount + " trees");
        }

        static void StripRenderer(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        }

        // Tree_NN -> (its code-tree mesh, its manifest placement), from the four Harbour_Core_CodeTree_*_Tri models (cards: their four
        // Harbour_Core_CodeTree_*_Cards crown twins, round 2b).
        static Dictionary<string, (Mesh, HarbourPierManifest.Placement)> CodeTreePlacements(ArtAssets art, bool cards)
        {
            var map = new Dictionary<string, (Mesh, HarbourPierManifest.Placement)>();
            for (int i = 0; i < HarbourPierLayout.Visuals.Length; i++)
            {
                HarbourPierLayout.VisualModel visual = HarbourPierLayout.Visuals[i];
                if (visual.Parent != HarbourPierLayout.RootCodeTrees || visual.Cards != cards) continue;
                foreach (HarbourPierManifest.Placement p in HarbourPierManifest.Find(art.Manifest, visual.MeshName).placements)
                    map.Add(p.name, (art.VisualMeshes[i], p));
            }
            if (map.Count != HarbourPierLayout.TreeCount) throw new System.InvalidOperationException("the manifest places " + map.Count + " code trees, expected " + HarbourPierLayout.TreeCount);
            return map;
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
