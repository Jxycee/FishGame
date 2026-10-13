// Core/Fishing/BaitStats.cs: bait numbers as Core sees them; mirrors BaitDef (C 7.5). No default tuning (C 5.1).
namespace FishGame.Core
{
    public struct BaitStats
    {
        public int Rating, StackMax, MaxRating, MaxNibbles;
        public float BiteSpeedMod, JunkMult;
        public bool Unlimited;
        public FishTags AffinityTags;            // M3 T3 (spec 9.7 / 12.4); BaitStats.Mods comes in T5
        public float AffinityMult, SpecialMult;

        // min(MaxRating, Rating * max(1, loadedCount)); M2 passes 1.
        public int EffRating(int loadedCount)
        {
            int n = loadedCount < 1 ? 1 : loadedCount;
            long r = (long)Rating * n;
            return r > MaxRating ? MaxRating : (int)r;
        }
    }
}
