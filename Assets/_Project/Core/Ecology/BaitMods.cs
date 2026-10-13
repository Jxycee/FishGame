// Core/Ecology/BaitMods.cs: the bait numbers the resolver reads (spec 12.4). Built from BaitStats.Mods(loadedCount) on the host.
// Authority: none (plain data).
namespace FishGame.Core
{
    public struct BaitMods
    {
        public int Rating; public FishTags AffinityTags; public float AffinityMult, JunkMult, SpecialMult;

        // No affinity, all multipliers 1 (V1-V3 "neutral bait", M3-D5).
        public static BaitMods Neutral(int rating)
        {
            return new BaitMods { Rating = rating, AffinityTags = FishTags.None, AffinityMult = 1f, JunkMult = 1f, SpecialMult = 1f };
        }
    }
}
