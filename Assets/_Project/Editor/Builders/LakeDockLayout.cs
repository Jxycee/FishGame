// Spot_LakeDock numbers (contract 6.5, spec 17.1 item 9, D34, D36). Owned by T15.
// Check: lakebed at (12, -44) -> r 45.61, d 4.39 -> y -0.66 m (matches contract 6.5).
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class LakeDockLayout
    {
        public const ulong Seed = 0xF15ED0C4UL; // contract 5.6 lake-dock layout seed
        public const string RockStream = "lake_dock.rocks";
        public const string TreeStream = "lake_dock.trees";

        // T28 additions. New randomness uses NEW streams only: a draw added to the M1 rock or tree streams would move every later prop.
        public const string PlankStream = "lake_dock.planks";
        public const string CanopyStream = "lake_dock.canopy";
        public const string ReedStream = "lake_dock.reeds";

        // T28 mesh asset paths (C 2.6: no AssetPaths edit; LakebedMesh, ShoreMesh and MeshesFolder stay in AssetPaths).
        public const string WaterMeshPath = AssetPaths.MeshesFolder + "/LakeDock_Water.asset";
        public const string DockMeshPath = AssetPaths.MeshesFolder + "/LakeDock_Dock.asset";
        public const string RockMeshPath = AssetPaths.MeshesFolder + "/LakeDock_Rock.asset";
        public const string TrunkMeshPath = AssetPaths.MeshesFolder + "/LakeDock_Trunk.asset";
        public const string CanopyRoundMeshPath = AssetPaths.MeshesFolder + "/LakeDock_CanopyRound.asset";
        public const string CanopyPineMeshPath = AssetPaths.MeshesFolder + "/LakeDock_CanopyPine.asset";
        public const string ReedsMeshPath = AssetPaths.MeshesFolder + "/LakeDock_Reeds.asset";

        // FaceJitter mesh seeds, one per mesh (fixed: an Inspector edit must never change a generated mesh).
        public const uint LakebedMeshSeed = 0x1A4E0B01u, ShoreMeshSeed = 0x1A4E0B02u, DockMeshSeed = 0x1A4E0B03u, RockMeshSeed = 0x1A4E0B04u;
        public const uint TrunkMeshSeed = 0x1A4E0B05u, CanopyRoundMeshSeed = 0x1A4E0B06u, CanopyPineMeshSeed = 0x1A4E0B07u, ReedsMeshSeed = 0x1A4E0B08u;

        // Water grid: 96 cells x 1.25 m = 120 m (the M1 visual plane). Spec 10.4 wants wavelengths >= 4 cells; T14 seeds at least 6 m.
        public const int WaterGridCells = 96;
        public const float WaterCellSize = 1.25f;

        // Palette slot indices (spec 10.2 slotNames order). No shared constant set exists in Core or Data, so they live here.
        public const byte SlotGrass = 0, SlotFoliage = 1, SlotFoliageDark = 2, SlotWood = 3, SlotWoodDark = 4, SlotSand = 5;
        public const byte SlotRock = 6, SlotReed = 7, SlotDock = 8, SlotRoof = 9, SlotAccent = 10;

        // Shore faces whose centre is below this height are Sand, above it Grass.
        public const float ShoreSandMaxY = 0.3f;

        // Dock visual (planks lie across the deck: 3.0 m in x, 0.26 m in z, 0.1 m thick, 0.04 m gap; 40 planks fill z -52..-40).
        public const int PlankCount = 40;
        public const float PlankLength = 3f, PlankWidth = 0.26f, PlankThickness = 0.1f, PlankGap = 0.04f, PlankYawDegrees = 0.8f;
        public const float StringerX = 1.2f, StringerSize = 0.12f;
        public const float PostRadius = 0.08f;
        public const int PostSides = 6;
        public const float RailSize = 0.05f, RungSpacing = 0.3f, RungThickness = 0.03f, RungDepth = 0.04f, RailInset = 0.2f;

        // Prop mesh shape (unit sizes match the Unity primitives the M1 transforms were authored for).
        public const float TrunkRadiusBottom = 0.5f, TrunkRadiusTop = 0.42f, TrunkHeight = 2f;
        public const int TrunkSides = 7;
        public const float PineChance = 0.6f;

        // Reeds: 24 clusters of 4-6 blades in the shallows (bed about -0.5 to -0.08 m).
        public const int ReedClusterCount = 24, ReedMinBlades = 4, ReedMaxBlades = 6;
        public const float ReedMinRadius = 46.5f, ReedMaxRadius = 49.5f, ReedClusterSpread = 0.35f, ReedClearance = 4f;
        public const float ReedMinHeight = 0.9f, ReedMaxHeight = 1.6f, ReedBladeRadius = 0.025f, ReedMaxLeanDegrees = 8f;

        // Index safety for a combined mesh (16-bit indices).
        public const int MaxCombinedTriangles = 21000;

        // Bowl: 0 m at the shoreline (r 50), -6 m at 40 m inward, flat -6 m inside r 10.
        public const float ShoreRadius = 50f, BowlRun = 40f, BowlDepth = 6f;
        public static readonly float[] LakebedRadii = { 0f, 4f, 8f, 10f, 14f, 18f, 22f, 26f, 30f, 34f, 38f, 42f, 46f, 50f };

        // Shore ring: y -0.2 at r 49 rising to y 1.0 at r 56, flat to r 90.
        public const float ShoreInnerRadius = 49f, ShoreInnerY = -0.2f, ShoreTopRadius = 56f, ShoreTopY = 1f;
        public static readonly float[] ShoreRadii = { 49f, 50f, 51f, 52f, 53f, 54f, 55f, 56f, 60f, 65f, 70f, 80f, 90f };

        // Dock: deck x in [-1.5, 1.5], z in [-52, -40], top y 0.6, thickness 0.1; posts every 2 m at x +-1.4.
        public const float DeckWidth = 3f, DeckNearZ = -52f, DeckFarZ = -40f, DeckTopY = 0.6f, DeckThickness = 0.1f;
        public const float PostX = 1.4f, PostSpacing = 2f, PostSize = 0.12f;
        public const float LadderZ = -41.5f, LadderX = 1.58f, LadderThickness = 0.06f, LadderWidth = 0.5f;

        // Ramp at x 12, from the lakebed to the shore.
        public const float RampX = 12f;
        public static readonly Vector3 RampLow = new Vector3(12f, -0.9f, -43f);
        public static readonly Vector3 RampHigh = new Vector3(12f, 0.4f, -51f);
        public const float RampWidth = 3f, RampThickness = 0.1f;

        // Spawns 1.5 m apart on the deck, facing +z.
        public const int SpawnCount = 4;
        public const float SpawnY = 0.65f, FirstSpawnZ = -50.5f, SpawnSpacing = 1.5f;

        // Seeded props. Placements near the dock/camera line (x 0) and the ramp (x 12) at z <= -38 are rejected.
        public const int RockCount = 14, TreeCount = 30, MaxAttempts = 500;
        public const float RockMinRadius = 48f, RockMaxRadius = 54f, TreeMinRadius = 58f, TreeMaxRadius = 85f;
        public const float RockClearance = 3f, TreeClearance = 5f, ClearanceMaxZ = -38f;

        public static float BedY(float r) =>
            r >= ShoreRadius ? 0f : -BowlDepth * Mathf.Clamp01((ShoreRadius - r) / BowlRun);

        public static float BedYAt(float x, float z) => BedY(Mathf.Sqrt(x * x + z * z));

        public static float ShoreY(float r) =>
            Mathf.Lerp(ShoreInnerY, ShoreTopY, Mathf.Clamp01((r - ShoreInnerRadius) / (ShoreTopRadius - ShoreInnerRadius)));

        public static bool Blocked(float x, float z, float clearance) =>
            z <= ClearanceMaxZ && (Mathf.Abs(x) <= clearance || Mathf.Abs(x - RampX) <= clearance);
    }
}
