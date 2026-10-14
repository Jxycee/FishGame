// Spot_HarbourPier numbers (plan maps T17.2, M3-D40 starting numbers adjusted per open question 3; T17-B: the visuals are Blender FBX). Owned by T17.
// World axes: +z is out to sea; the coast runs along x at z = -45; land is z < -45. Water surface y 0 (WaterSurface.FlatHeight).
// Everything the bake, the analytic test, CastProbe and the colliders need is here and in HarbourPierBuilder. The Blender generator
// (Art/Blender/fg_harbour.py) mirrors the pier numbers by hand; HarbourPier_ModelBoundsMatchLayout is what keeps the two in step.
// T17-B layout deviations accepted from the artist: the west-front T-head bollard sits at (-3.9, -5.4) (clear of the lamp at (-4.8, -5.3)), the deck
// stringers sit at x +-1.38, and the ladder grab rails rise to y 2.35 (inside the rail boxes' footprint, inside the ladder volumes' height).
//
// Hand check of reachability (the bake is the proof). Land cells are those with centre z <= -45.5, so a water cell's shore distance is z + 45 and
// its depth 0.25 x (z + 45). Castable needs depth >= 0.5, so z >= -43: castable shallows are the rows z -42.5, -41.5, -40.5 (shore 2.5, 3.5, 4.5 m);
// channel rows -39.5..-33.5; open sea from -32.5. From one beach point (z -46.5) the [4.9, 16.4] m ring covers about 85 shallows cells (row -42.5
// only beyond 2.9 m to the side), the whole channel band for about 30 m of coast, and about 30 open-sea cells; the pier middle, T-head and
// breakwater add far more channel and open sea. Each sub-area clears 25 with a wide margin. The breakwater is land, so the cells beside it are
// shallows too, which is intended (fish the wall).
using System.Collections.Generic;
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class HarbourPierLayout
    {
        // ---- seed and streams (new randomness uses new streams only; the rocks, planks and stones of the plan are Blender art now) ----
        public const ulong Seed = 0xF15ED0C5UL;   // the lake's is ...C4
        public const string TreeStream = "harbour_pier.trees";
        public const string CanopyStream = "harbour_pier.canopy";
        public const string FoliageStream = "harbour_pier.foliage";   // the round trees' balanced foliage variants (seasonal trees, as the lake)
        public const int MaxAttempts = 500;

        // ---- terrain (exact formulas: the bake, the analytic test, CastProbe and the posts depend on them) ----
        public const float CoastZ = -45f;
        public const float SeabedSlope = 0.25f, SeabedMaxDepth = 9f;
        public const float BeachSlope = 0.12f, BeachMinY = -0.06f, BeachMaxY = 1.2f;

        public static float SeabedY(float z) => -Mathf.Min(SeabedMaxDepth, SeabedSlope * Mathf.Max(0f, z - CoastZ));
        public static float BeachY(float z) => Mathf.Clamp(BeachSlope * (CoastZ - z), BeachMinY, BeachMaxY);
        public static float TerrainY(float x, float z) => z <= CoastZ ? BeachY(z) : SeabedY(z);

        // Seabed (Ground): x -400..400 (to the sea skirt's far edge, so the sea floor does not end at the water grid), z -47..121. Rows every 2 m in z up to -9 (so the
        // kinks at z -45 and z -9 are vertices and the surface is exactly the formula), then one row at 121 (flat; harbour round 1); columns every 20 m in x: every height-field attribute is independent
        // of x, so the surface is the same plane at any x step and the bake sees the same rays.
        public const float SeabedMinX = -400f, SeabedMaxX = 400f, SeabedMinZ = -47f, SeabedMaxZ = 121f, SeabedStepX = 20f, SeabedStepZ = 2f;
        // Round 1: from here (the -9 m kink) out the seabed is flat, so the mesh has no rows between it and SeabedMaxZ.
        public static float SeabedFlatFromZ => CoastZ + SeabedMaxDepth / SeabedSlope;   // -9
        // Beach and plateau (Shore): x -120..120 in 2 m steps; rows (z): the submerged lip -44.5, then -45 down to -55 every metre (the two kinks),
        // then -100 (flat plateau). Sand below BeachSandMaxY, Grass above (the slot blends across the band that crosses it).
        // The land runs to x +-400 (20 m columns, the heights do not depend on x) and z -400, the sea skirt's far edge, so the land has no visible edge.
        public const float BeachMinX = -400f, BeachMaxX = 400f, BeachStep = 20f, BeachLipZ = -44.5f, BeachFlatFromZ = -55f, BeachEndZ = -400f;
        public const float BeachSandMaxY = 0.55f;
        // Water visual: MeshBuilder.Grid(160, 160, 1.5) = 240 m, centred at the origin, y 0 (25,921 vertices, 16-bit safe).
        public const int WaterGridCells = 160;
        public const float WaterCellSize = 1.5f;
        // Water volume (the only Water collider): trigger box (120, 10, 120) centred (0, -5, 0).
        public static readonly Vector3 WaterVolumeSize = new Vector3(120f, 10f, 120f), WaterVolumeCentre = new Vector3(0f, -5f, 0f);

        // ---- asset paths ----
        public const string WaterMeshPath = AssetPaths.MeshesFolder + "/Harbour_Water.asset";
        public const string WaterSkirtMeshPath = AssetPaths.MeshesFolder + "/Harbour_WaterSkirt.asset";
        public const float SkirtOuterHalfExtent = 400f;   // MovementMath.FarClip: the sea reaches the far clip plane
        public const int SkirtOuterSegmentsPerSide = 8;
        public const string SeabedMeshPath = AssetPaths.MeshesFolder + "/Harbour_Seabed.asset";
        public const string BeachMeshPath = AssetPaths.MeshesFolder + "/Harbour_Beach.asset";
        public const string ModelsFolder = AssetPaths.ModelsFolder + "/Harbour";
        public const string ManifestPath = ModelsFolder + "/harbour_manifest.json";

        // ---- pier ----
        public const float DeckHalfWidth = 1.5f, DeckTopY = 1.5f, DeckThickness = 0.12f, DeckRootZ = -57f, DeckEndZ = -8f;
        public const float THeadHalfWidth = 5f, THeadNearZ = -8f, THeadFarZ = -5f;
        public static float DeckUndersideY => DeckTopY - DeckThickness;
        // Pier ramp: plateau 1.2 -> deck 1.5 without a step (the lake RampDeck construction: a box of this width and thickness along low -> high).
        public static readonly Vector3 PierRampLow = new Vector3(0f, 1.17f, -60.5f), PierRampHigh = new Vector3(0f, 1.45f, -57f);
        public const float PierRampWidth = 3f, PierRampThickness = 0.1f;
        // Posts (collider boxes, Structure), each from TerrainY to the deck underside: x +-1.4 every 3 m from z -56 to -8, plus the T-head corners.
        // The box is 0.2 wide (the art's post is a lathe of radius 0.09 with a foot flare, 0.198-0.212 across); the plan's 0.12 would let a player's
        // capsule overlap the visual by 4 cm.
        public const float PostX = 1.4f, PostFirstZ = -56f, PostSpacing = 3f, PostCollider = 0.2f;
        public const float THeadPostX = 4.6f, THeadPostNearZ = -7.6f, THeadPostFarZ = -5.4f;
        public static readonly Vector2[] Posts = BuildPosts();

        static Vector2[] BuildPosts()
        {
            var posts = new List<Vector2>();
            for (float z = PostFirstZ; z <= DeckEndZ + 0.001f; z += PostSpacing)
            {
                posts.Add(new Vector2(-PostX, z));
                posts.Add(new Vector2(PostX, z));
            }
            posts.Add(new Vector2(THeadPostX, THeadPostNearZ));
            posts.Add(new Vector2(-THeadPostX, THeadPostNearZ));
            posts.Add(new Vector2(THeadPostX, THeadPostFarZ));
            posts.Add(new Vector2(-THeadPostX, THeadPostFarZ));
            return posts.ToArray();
        }

        // ---- ladders (the lake's rule: volume top = deck + 1.0, bottom -1.4; volumes on Default, no rotation on the object or its parents) ----
        public const float LadderVolumeBottomY = -1.4f, LadderVolumeTopY = 2.5f, RailTopY = 2.35f;
        // Walk-in volumes (Bounds centre/size): mid ladder on the east side of the deck at z -25, head ladder on the T-head's seaward face.
        public static readonly Bounds LadderMidVolume = new Bounds(new Vector3(2.05f, 0.55f, -25f), new Vector3(1f, 3.9f, 1f));      // x 1.55..2.55, z -25.5..-24.5
        public static readonly Bounds LadderHeadVolume = new Bounds(new Vector3(2f, 0.55f, -4.45f), new Vector3(1f, 3.9f, 1f));      // x 1.5..2.5, z -4.95..-3.95
        // Collider-only rail boxes (Structure): the mid ladder's rails stand at x 1.58 (0.06 thick) across z -25.25..-24.75; the head ladder's rails
        // stand against the T-head face at z -4.92 (0.06 thick) across x 1.75..2.25. Bottom -1.4 (the ladder's foot), top = the deck top, as
        // LakeDockBuilder.AddLadder: a collider wall above the deck leaves the climber short of the lip (feet top out at about 1.6 against
        // StepOffset 0.35) and blocks climbing down. The grab rails of the VISUAL rise to RailTopY (2.35) above the collider, which is why the
        // bounds test checks the rails' x and z against these boxes and their height against RailTopY.
        public static readonly Bounds LadderMidRailBox = new Bounds(new Vector3(1.58f, (LadderVolumeBottomY + DeckTopY) * 0.5f, -25f),
                                                                     new Vector3(0.06f, DeckTopY - LadderVolumeBottomY, 0.5f));
        public static readonly Bounds LadderHeadRailBox = new Bounds(new Vector3(2f, (LadderVolumeBottomY + DeckTopY) * 0.5f, -4.92f),
                                                                      new Vector3(0.5f, DeckTopY - LadderVolumeBottomY, 0.06f));

        // ---- breakwater (Shore): the walkable box x 28..32, z -56..-25, y -6..1.5 (starts on the plateau at 1.2, a 0.3 m step with StepOffset 0.35) ----
        public static readonly Bounds BreakwaterBox = FromMinMax(new Vector3(28f, -6f, -56f), new Vector3(32f, DeckTopY, -25f));

        static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }
        public const float BreakwaterWalkMinX = 28f, BreakwaterWalkMaxX = 32f;

        // ---- spawns, bell, camera ----
        public const int SpawnCount = 4;
        public const float SpawnY = 1.55f, FirstSpawnZ = -55f, SpawnSpacing = 1.5f;
        public static readonly Vector3 BellAnchorPosition = new Vector3(1.1f, 1.5f, -47.5f);
        public static readonly Vector3 SceneCameraPosition = new Vector3(0f, 6f, -70f), SceneCameraLookAt = new Vector3(0f, 1.5f, -30f);

        // ---- invisible boundary walls (Default layer, so neither CastProbe nor the bake sees them; open question 2) ----
        // Round 1 (2026-10-15, map layout pass): the play area grew inland. The south wall moved from z -75.5 to -106.5 to take in the two
        // terraces behind the village (level 1 y 3.74: the upper lane and the cottages; level 2 y 6.74: the chapel and the overlook), and the
        // walls rise to y 16 so nobody walks over them from level 2. The terraces, their stairs and parapets are Blender land
        // (Harbour_Dunes_Col, Harbour_Core_Walls_Tri), all outside the bake grid (z >= -60), so the castable grid only changes by the jetty.
        public const float WallThickness = 1f, WallMinY = -12f, WallMaxY = 16f;
        public const float WallEastWestX = 60.5f, WallNorthZ = 60.5f, WallSouthZ = -106.5f;

        // ---- trees (Shore): lake pattern, reused lake meshes ----
        public const int TreeCount = 16;
        public const float TreeMinX = -58f, TreeMaxX = 58f, TreeMinZ = -73f, TreeMaxZ = -60f, TreeMinHeight = 0.8f, TreeMaxHeight = 1.3f;
        public const float TreeClearance = 6f;   // from x 0 (pier root), x 30 (breakwater) and the hut
        public static readonly Vector2 HutCentreXZ = new Vector2(-9f, -63f);

        public static bool TreeBlocked(float x, float z) =>
            Mathf.Abs(x) <= TreeClearance || Mathf.Abs(x - 30f) <= TreeClearance || (new Vector2(x, z) - HutCentreXZ).magnitude <= TreeClearance;

        // ---- stand points (x, y, z, facingDeg; this order) ----
        // 0-4 beach (x -48, -30, -12, 14, 48; z -46.5; BeachY + 0.05 = 0.23), 5-6 pier middle east/west, 7-8 T-head west/east end, 9 T-head front,
        // 10 breakwater tip. Facing is Unity yaw (0 = +z, 90 = +x).
        // Round 1 (2026-10-15): 11 the east-basin fishing jetty head (deck 0.95, facing north-west into the basin), 12 the lookout tower deck
        // (4.4 m up by the breakwater root, facing north-east over the basin): a second jetty and the first raised fishing spot. Both lanes clear
        // the tug, the moored boats and the other lanes (fg_harbour.py lane_hit / verify_layout).
        public const float JettyDeckTopY = 0.95f, LookoutDeckTopY = 4.4f;
        public const float StandBeachZ = -46.5f, StandLift = 0.05f;
        public static readonly Vector4[] StandPoints = BuildStandPoints();

        static Vector4[] BuildStandPoints()
        {
            float beachY = BeachY(StandBeachZ) + StandLift;
            float[] beachX = { -48f, -30f, -12f, 14f, 48f };
            var points = new List<Vector4>();
            foreach (float x in beachX) points.Add(new Vector4(x, beachY, StandBeachZ, 0f));
            points.Add(new Vector4(0f, 1.55f, -25f, 90f));      // pier middle, east
            points.Add(new Vector4(0f, 1.55f, -25f, 270f));     // pier middle, west
            points.Add(new Vector4(-4.5f, 1.55f, -6.5f, 270f)); // T-head west end
            points.Add(new Vector4(4.5f, 1.55f, -6.5f, 90f));   // T-head east end
            points.Add(new Vector4(0f, 1.55f, -5.5f, 0f));      // T-head front
            points.Add(new Vector4(30f, 1.55f, -26f, 0f));      // breakwater tip
            points.Add(new Vector4(55.7f, JettyDeckTopY + 0.05f, -35f, 330f));      // round 1: the east jetty head
            points.Add(new Vector4(36.4f, LookoutDeckTopY + 0.05f, -48.3f, 30f));   // round 1: the lookout tower deck
            return points.ToArray();
        }

        // ---- Blender visuals (Content/Models/Harbour): file, parent root, child name. Placed at the origin with an identity transform, no collider. ----
        public const string RootPier = "Pier", RootBreakwater = "Breakwater", RootBeach = "Beach", RootProps = "Props";
        // Pass 2 (harbour-art2 detail pass, round 0 2026-10-14). Parent tags of the pass-2 models:
        //  Land (Shore layer): land shape, backdrop and the river (water material, not the Water layer, no collider), the stone wall (Shore boxes)
        //    and the walkable dune collision mesh (Harbour_Dunes_Col: MeshCollider only).
        //  Village (Structure): buildings, yards, the village trees; their box items become Structure BoxColliders (trunk-only for the trees, the
        //    net shed's back half only, as the manifest's bounds give them).
        //  Detail (Default): every "detail" model (no colliders); AmbientLife hides the Detail root on the Low preset. Local detail models are
        //    instanced per placement (gulls, floats, flags, the windsock, the shop sign, nets, bunting, the weather vane) and animated by AmbientLife.
        //  CrewBoat: the tug, its collision mesh (Shore) and the ice chests, placed from the manifest (local models, the tug's frame).
        //  CodeTrees: the four local code-tree models, one under each Tree_NN (see PlaceTrees).
        //  Round 1: a local core model under Land (the windmill sails) is instanced per placement and turns; it is core, so Low keeps it.
        public const string RootLand = "Land", RootVillage = "Village", RootDetail = "Detail", RootCrewBoat = "CrewBoat", RootCodeTrees = "CodeTrees";
        public const string RootSmoke = "Smoke", RootLights = "HarbourLights", RootLife = "AmbientLife";

        public readonly struct VisualModel
        {
            public readonly string File, Parent, Name;
            public VisualModel(string file, string parent, string name) { File = file; Parent = parent; Name = name; }
            public string Path => ModelsFolder + "/" + File;
            // A "_Tri" model is drawn world-triplanar on M_Toon_Tri; everything else is uv0-mapped on M_Toon_Base.
            public bool Triplanar => File.EndsWith(HarbourModelPostprocessor.TriplanarSuffix + ".fbx", System.StringComparison.Ordinal);
            public string MeshName => System.IO.Path.GetFileNameWithoutExtension(File);
        }

        public static readonly VisualModel[] Visuals =
        {
            new VisualModel("Harbour_PierDeck.fbx", RootPier, "PierDeckVisual"),
            new VisualModel("Harbour_PierHead.fbx", RootPier, "PierHeadVisual"),
            new VisualModel("Harbour_PierPosts.fbx", RootPier, "PierPostsVisual"),
            new VisualModel("Harbour_Ladders.fbx", RootPier, "LaddersVisual"),
            new VisualModel("Harbour_Breakwater_Tri.fbx", RootBreakwater, "BreakwaterVisual"),
            new VisualModel("Harbour_BreakwaterBase_Tri.fbx", RootBreakwater, "BreakwaterBaseVisual"),
            new VisualModel("Harbour_Rocks_Tri.fbx", RootBeach, "RocksVisual"),
            new VisualModel("Harbour_Props.fbx", RootProps, "PropsVisual"),
            new VisualModel("Harbour_Dressing.fbx", RootProps, "DressingVisual"),
            new VisualModel("Harbour_Boats.fbx", RootProps, "BoatsVisual"),
            // ---- pass 2 ----
            new VisualModel("Harbour_Core_Dunes_Tri.fbx", RootLand, "DunesVisual"),
            new VisualModel("Harbour_Dunes_Col.fbx", RootLand, "DunesCollision"),
            new VisualModel("Harbour_Core_Hills_A_Tri.fbx", RootLand, "HillsAVisual"),
            new VisualModel("Harbour_Core_Hills_B_Tri.fbx", RootLand, "HillsBVisual"),
            new VisualModel("Harbour_Core_River.fbx", RootLand, "RiverVisual"),
            new VisualModel("Harbour_Core_Cliffs_West_Tri.fbx", RootLand, "CliffsWestVisual"),
            new VisualModel("Harbour_Core_Cliffs_East_Tri.fbx", RootLand, "CliffsEastVisual"),
            new VisualModel("Harbour_Core_Paths.fbx", RootLand, "PathsVisual"),
            new VisualModel("Harbour_Core_Walls_Tri.fbx", RootLand, "StoneWallVisual"),
            new VisualModel("Harbour_Core_Road.fbx", RootLand, "RoadVisual"),
            new VisualModel("Harbour_Core_Forest_Tri.fbx", RootLand, "ForestVisual"),
            new VisualModel("Harbour_Core_HeadlandTrees_Tri.fbx", RootLand, "HeadlandTreesVisual"),
            new VisualModel("Harbour_Core_Islands_Tri.fbx", RootLand, "IslandsVisual"),
            new VisualModel("Harbour_Core_Backdrop.fbx", RootLand, "BackdropVisual"),
            new VisualModel("Harbour_Core_Buildings.fbx", RootVillage, "BuildingsVisual"),
            new VisualModel("Harbour_Core_Yard_A.fbx", RootVillage, "YardAVisual"),
            new VisualModel("Harbour_Core_Yard_B.fbx", RootVillage, "YardBVisual"),
            new VisualModel("Harbour_Core_Trees_Tri.fbx", RootVillage, "VillageTreesVisual"),
            new VisualModel("Harbour_Core_CrewBoat.fbx", RootCrewBoat, "HullVisual"),
            new VisualModel("Harbour_Core_CrewBoat_Col.fbx", RootCrewBoat, "Collision"),
            new VisualModel("Harbour_Core_CrewBoat_IceChest.fbx", RootCrewBoat, "IceChest"),
            new VisualModel("Harbour_Detail_Lights.fbx", RootDetail, "StringLightsVisual"),
            new VisualModel("Harbour_Detail_Clutter.fbx", RootDetail, "ClutterVisual"),
            new VisualModel("Harbour_Detail_Plants_A.fbx", RootDetail, "PlantsAVisual"),
            new VisualModel("Harbour_Detail_Plants_B.fbx", RootDetail, "PlantsBVisual"),
            new VisualModel("Harbour_Detail_Shore.fbx", RootDetail, "ShoreDressingVisual"),
            new VisualModel("Harbour_Detail_TidePools_Tri.fbx", RootDetail, "TidePoolsVisual"),
            new VisualModel("Harbour_Detail_Reeds.fbx", RootDetail, "ReedsVisual"),
            new VisualModel("Harbour_Detail_Anim_Gull.fbx", RootDetail, "Gull"),
            new VisualModel("Harbour_Detail_Anim_Buoy.fbx", RootDetail, "Float"),
            new VisualModel("Harbour_Detail_Anim_Flag.fbx", RootDetail, "Flag"),
            new VisualModel("Harbour_Detail_Anim_WindSock.fbx", RootDetail, "WindSock"),
            new VisualModel("Harbour_Detail_Anim_SwingSign.fbx", RootDetail, "SwingSign"),
            new VisualModel("Harbour_Detail_Anim_HangingNet.fbx", RootDetail, "HangingNet"),
            new VisualModel("Harbour_Detail_Anim_Bunting_A.fbx", RootDetail, "Bunting"),
            new VisualModel("Harbour_Detail_Anim_Bunting_B.fbx", RootDetail, "Bunting"),
            new VisualModel("Harbour_Detail_Anim_Bunting_C.fbx", RootDetail, "Bunting"),
            new VisualModel("Harbour_Detail_Anim_WeatherVane.fbx", RootDetail, "WeatherVane"),
            new VisualModel("Harbour_Core_CodeTree_PineA_Tri.fbx", RootCodeTrees, "TreeVisual"),
            new VisualModel("Harbour_Core_CodeTree_PineB_Tri.fbx", RootCodeTrees, "TreeVisual"),
            new VisualModel("Harbour_Core_CodeTree_RoundA_Tri.fbx", RootCodeTrees, "TreeVisual"),
            new VisualModel("Harbour_Core_CodeTree_RoundB_Tri.fbx", RootCodeTrees, "TreeVisual"),
            // ---- round 1 (map layout pass) ----
            new VisualModel("Harbour_Core_Buildings_Upper.fbx", RootVillage, "UpperVillageVisual"),
            new VisualModel("Harbour_Core_Jetty.fbx", RootVillage, "JettyVisual"),
            new VisualModel("Harbour_Core_TerraceTrees_Tri.fbx", RootVillage, "TerraceTreesVisual"),
            new VisualModel("Harbour_Core_Anim_WindmillSails.fbx", RootLand, "WindmillSails"),
            new VisualModel("Harbour_Detail_Moorings.fbx", RootDetail, "MooringsVisual"),
            new VisualModel("Harbour_Detail_Terraces.fbx", RootDetail, "TerraceDressingVisual"),
            new VisualModel("Harbour_Detail_Anim_GullFly.fbx", RootDetail, "GullFly"),
        };

        // Pass-2 collider sources: box items of these models become BoxColliders (Land: Shore; Village: Structure). Round 1: the stone wall model
        // is the terrace retaining walls now (its box items are the parapets); the upper village (cottages, the chapel tower, lane lamps,
        // overlook benches) and the jetty with the lookout tower (deck, posts, rails, stair steps) are Village boxes.
        public static readonly string[] LandBoxModels = { "Harbour_Core_Walls_Tri" };
        public static readonly string[] VillageBoxModels = { "Harbour_Core_Buildings", "Harbour_Core_Yard_A", "Harbour_Core_Yard_B", "Harbour_Core_Trees_Tri",
                                                             "Harbour_Core_Buildings_Upper", "Harbour_Core_Jetty", "Harbour_Core_TerraceTrees_Tri" };
        // Round 1: the windmill sails turn (AmbientLife Rotor, degrees per second); the circling gulls (AmbientLife Circle).
        public const string WindmillSailsModel = "Harbour_Core_Anim_WindmillSails";
        public const float WindmillDegreesPerSecond = 24f;
        public const string CrewBoatModel = "Harbour_Core_CrewBoat", CrewBoatColModel = "Harbour_Core_CrewBoat_Col", IceChestModel = "Harbour_Core_CrewBoat_IceChest";
        public const string WeatherVaneModel = "Harbour_Detail_Anim_WeatherVane", LightsModel = "Harbour_Detail_Lights";
        public const float WeatherVaneSplitY = 0.25f;   // the manifest note: only the part above y 0.25 (local) turns
        public const string WeatherVaneHeadMeshPath = AssetPaths.MeshesFolder + "/Harbour_WeatherVane_Head.asset";
        public const string WeatherVaneBaseMeshPath = AssetPaths.MeshesFolder + "/Harbour_WeatherVane_Base.asset";
        public const string BeaconBeamMeshPath = AssetPaths.MeshesFolder + "/Harbour_BeaconBeam.asset";
        public const string GlowSphereMeshPath = AssetPaths.MeshesFolder + "/Harbour_GlowSphere.asset";   // round 1: the lamp glow spheres' low-poly mesh
        public const float TugBobMetres = 0.03f;         // the moored tug's hull visual heave (the collision mesh never moves)
        // Phase-1 pier lamps (Harbour_Props Lamp_0 / Lamp_1): the light sits in the lamp head, as the pass-2 lamp posts' (y 4.1 for a 4.53 top).
        public const float PierLampLightBelowTop = 0.43f;

        // Manifest item sources for the colliders (HarbourPierManifest): sphere items and box items per FBX mesh.
        public const string RocksModel = "Harbour_Rocks_Tri", BoulderModel = "Harbour_Breakwater_Tri", RubbleModel = "Harbour_BreakwaterBase_Tri";
        public const int ExpectedRocks = 12, ExpectedBoulders = 41, ExpectedRubble = 27;
        public static readonly string[] PropBoxModels = { "Harbour_Props", "Harbour_Dressing", "Harbour_Boats" };

        // The manifest's boulder spheres are loose bounding spheres around flattened stones: their centres sit up to 0.4 m low and their tops
        // reach 1.6-1.9 m, which would put invisible bumps along the walkable edge (x 28..32). The collider keeps the manifest radius but its
        // centre is lowered until the top is at most the walkable top, so no breakwater collider surface is above DeckTopY (1.5).
        public static float BoulderColliderCentreY(float manifestCentreY, float radius) => Mathf.Min(manifestCentreY, DeckTopY - radius);
    }
}
