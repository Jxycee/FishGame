// Core/Fishing/CreelSnapshot.cs: entry count and total weight of one creel against its caps (C 7.9). Immutable.
namespace FishGame.Core
{
    public readonly struct CreelSnapshot
    {
        public readonly int Entries, TotalWeightG, MaxEntries, MaxWeightG;

        public CreelSnapshot(int entries, int totalWeightG, int maxEntries, int maxWeightG)
        {
            Entries = entries; TotalWeightG = totalWeightG; MaxEntries = maxEntries; MaxWeightG = maxWeightG;
        }
    }
}
