// Core/Fishing/RollOverrides.cs: debug overrides for a cast roll; map 1:1 onto RollRequest (C 7.9).
// Note: default(RollOverrides) has ForcedItemIndex 0 (which would force species 0); start from RollOverrides.None.
namespace FishGame.Core
{
    public struct RollOverrides
    {
        public float ForcedBiteWait;
        public int ForcedItemIndex;      // -1 off
        public bool ForceJunk;
        public bool Twitchy;

        public static readonly RollOverrides None = new RollOverrides { ForcedItemIndex = -1 };
    }
}
