// Core/Fishing/FishingRoll.cs: the host's roll for one cast (spec 9.8 plus HookWindow, C 7.6 / M2-D6).
// Authority: rolled by the host at cast accept; the owner receives it and runs the fight from FightSeed.
namespace FishGame.Core
{
    public struct FishingRoll
    {
        public CatchResult Catch;
        public float WeightT;
        public float BiteWait;
        public byte NibbleCount;
        public float Nibble0, Nibble1, Nibble2;
        public uint FightSeed;       // non-zero
        public byte SizeClass;       // 0..2 from WeightT thirds
        public float HookWindow;     // 0 = auto-hook
        public bool Pity;            // R65: set by the host ledger at accept (never by PlaceholderRoller; no extra RNG draw)
    }
}
