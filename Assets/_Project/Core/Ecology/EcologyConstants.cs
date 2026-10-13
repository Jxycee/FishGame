// Core/Ecology/EcologyConstants.cs: spec 12.4 / 12.5 / 12.7 / 12.9 / 12.10 formula numbers (not tuning; tuning lives in assets, 2.8 #22).
// Authority: none (constants).
namespace FishGame.Core
{
    public static class EcologyConstants
    {
        public const float JunkShareCap = 0.6f;              // 12.4 step 1
        public const float SpecialChanceCap = 0.5f;          // 12.4 step 2, 12.9 specialChance in (0, 0.5]
        public const float HotspotJunkMult = 0.5f, HotspotSpecialMult = 2f;   // 12.7 effects (bite x0.6 stays in BitePlanner.WaitMultiplier)
        public const float EventBiteMultMin = 0.5f, EventBiteMultMax = 1.5f;  // 9.4 eventMult clamp
        public const ushort ShoreDmNotWater = 0xFFFF;        // 12.5
        public const ushort ShoreDmMax = 0xFFFE;
        public const byte AnySubArea = 0xFF;                 // FishingContext: sub-area gate passes (journal "biting now")
        public const int MaxSubAreas = 8;                    // SpawnRuleData.SubAreaMask is a byte
        public const int JournalCaughtForLevel3 = 3;         // 12.10 level 3
        public const float GridCellSizeM = 1f;               // 12.5 cellSizeM
        public const int MinReachableCellsPerSubArea = 25;   // 12.5 reachability
        public const float ReachMinM = FishingConstants.CastTipReach + 2.5f;   // 4.9 m (R67; M3-D6); 2.5 = castMinDist seed, checked against the asset by the validator
        public const float ReachMaxM = FishingConstants.CastTipReach + 14f;    // 16.4 m (Twig castMaxDist 14)
    }
}
