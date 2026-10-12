// Core/Fishing/BaitStats.cs: bait numbers as Core sees them; mirrors BaitDef (C 7.5). No default tuning (C 5.1).
namespace FishGame.Core
{
    public struct BaitStats
    {
        public int Rating, StackMax, MaxRating, MaxNibbles;
        public float BiteSpeedMod, JunkMult;
        public bool Unlimited;

        // min(MaxRating, Rating * max(1, loadedCount)); M2 passes 1.
        public int EffRating(int loadedCount)
        {
            int n = loadedCount < 1 ? 1 : loadedCount;
            long r = (long)Rating * n;
            return r > MaxRating ? MaxRating : (int)r;
        }
    }
}
