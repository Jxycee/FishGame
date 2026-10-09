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
