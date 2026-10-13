// Authority: none (wire data). Unmanaged mirror of Core CatchResult (G1: Core structs cannot implement NGO interfaces); the host fills it,
// receivers convert it back with ToCore. Memcpy-serialized, so it must stay unmanaged and must not implement INetworkSerializable.
using FishGame.Core;
using Unity.Netcode;

namespace FishGame.Fishing
{
    public struct CatchResultNet : INetworkSerializeByMemcpy
    {
        public byte IsJunk;           // 0 / 1
        public ushort ItemIndex;
        public int WeightG;
        public ushort LengthMm;
        public byte Quality;          // CatchQuality
        public int ValueCoins;

        public static CatchResultNet From(in CatchResult c)
        {
            return new CatchResultNet
            {
                IsJunk = c.IsJunk ? (byte)1 : (byte)0,
                ItemIndex = c.ItemIndex,
                WeightG = c.WeightG,
                LengthMm = c.LengthMm,
                Quality = (byte)c.Quality,
                ValueCoins = c.ValueCoins,
            };
        }

        public CatchResult ToCore()
        {
            return new CatchResult
            {
                IsJunk = IsJunk != 0,
                ItemIndex = ItemIndex,
                WeightG = WeightG,
                LengthMm = LengthMm,
                Quality = (CatchQuality)Quality,
                ValueCoins = ValueCoins,
            };
        }
    }
}
