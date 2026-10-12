// Authority: host produces it (from the ledger's roll) and sends it to the cast's owner only (CastResultRpc); the owner converts it with
// TryToCore, which re-runs the sanity check (spec 9.9). Species stay hidden from everyone else until the reveal (CatchAnnouncedRpc).
// Unmanaged mirror of Core FishingRoll (G1), memcpy-serialized; it must not implement INetworkSerializable.
using FishGame.Core;
using Unity.Netcode;

namespace FishGame.Fishing
{
    public struct FishingRollNet : INetworkSerializeByMemcpy
    {
        public const byte FlagPity = 1;   // R65 / M2-D23: bit 0 = FishingRoll.Pity; other bits are ignored on read

        public CatchResultNet Catch;
        public float WeightT, BiteWait;
        public byte NibbleCount;
        public float Nibble0, Nibble1, Nibble2;
        public uint FightSeed;
        public byte SizeClass;
        public float HookWindow;
        public byte Flags;

        public static FishingRollNet From(in FishingRoll r)
        {
            return new FishingRollNet
            {
                Catch = CatchResultNet.From(in r.Catch),
                WeightT = r.WeightT,
                BiteWait = r.BiteWait,
                NibbleCount = r.NibbleCount,
                Nibble0 = r.Nibble0,
                Nibble1 = r.Nibble1,
                Nibble2 = r.Nibble2,
                FightSeed = r.FightSeed,
                SizeClass = r.SizeClass,
                HookWindow = r.HookWindow,
                Flags = r.Pity ? FlagPity : (byte)0,
            };
        }

        // Converts, then returns FishingRollValidation.IsSane. A null database is never sane. False means: the caller logs once and drops it.
        public bool TryToCore(FishingDatabase db, out FishingRoll roll)
        {
            roll = new FishingRoll
            {
                Catch = Catch.ToCore(),
                WeightT = WeightT,
                BiteWait = BiteWait,
                NibbleCount = NibbleCount,
                Nibble0 = Nibble0,
                Nibble1 = Nibble1,
                Nibble2 = Nibble2,
                FightSeed = FightSeed,
                SizeClass = SizeClass,
                HookWindow = HookWindow,
                Pity = (Flags & FlagPity) != 0,
            };
            if (db == null) return false;
            return FishingRollValidation.IsSane(in roll, db);
        }
    }
}
