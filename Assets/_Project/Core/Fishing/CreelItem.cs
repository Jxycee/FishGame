// Core/Fishing/CreelItem.cs: one kept catch in a creel (C 7.9). Plain data.
namespace FishGame.Core
{
    public struct CreelItem
    {
        public ushort EntryId, ItemIndex;
        public bool IsJunk;
        public int WeightG;
        public CatchQuality Quality;
        public int ValueCoins;
        public int CaughtAtRunMinute;
    }
}
