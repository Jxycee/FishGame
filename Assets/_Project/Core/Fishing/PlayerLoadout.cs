// Core/Fishing/PlayerLoadout.cs: a slot rod, selected bait and its stock (C 7.9). Plain data.
namespace FishGame.Core
{
    public struct PlayerLoadout
    {
        public ushort RodIndex;
        public byte BaitIndex;
        public ushort BaitCount;   // FishingConstants.UnlimitedBaitCount (0xFFFF) = unlimited
    }
}
