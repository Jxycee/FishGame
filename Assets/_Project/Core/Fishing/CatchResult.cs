// Core/Fishing/CatchResult.cs: what a landed item is (spec 9.8, C 7.6). Plain data; no behaviour.
// Authority: produced by the host roll (C 7.6); the owner and creel only read it.
namespace FishGame.Core
{
    public struct CatchResult
    {
        public bool IsJunk;
        public ushort ItemIndex;      // species index, or junk index when IsJunk
        public int WeightG;           // 0 for junk
        public ushort LengthMm;       // 0 for junk
        public CatchQuality Quality;
        public int ValueCoins;
    }
}
