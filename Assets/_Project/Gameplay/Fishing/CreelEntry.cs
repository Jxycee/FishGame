// Authority: none (wire data). Host writes PlayerCreel.NetCreel entries; every peer reads them.
// NGO wire mirror of Core CreelItem (C 7.16, G1): Core structs cannot implement NGO interfaces. All fields are unmanaged and
// the layout is plain memcpy, so INetworkSerializeByMemcpy applies. IsJunk is a byte (0/1) and Quality is the CatchQuality byte.
using System;
using FishGame.Core;
using Unity.Netcode;

namespace FishGame.Gameplay
{
    public struct CreelEntry : INetworkSerializeByMemcpy, IEquatable<CreelEntry>
    {
        public ushort EntryId, ItemIndex;
        public byte IsJunk;
        public int WeightG;
        public byte Quality;
        public int ValueCoins;
        public int CaughtAtRunMinute;

        public static CreelEntry From(in CreelItem item)
        {
            return new CreelEntry
            {
                EntryId = item.EntryId,
                ItemIndex = item.ItemIndex,
                IsJunk = item.IsJunk ? (byte)1 : (byte)0,
                WeightG = item.WeightG,
                Quality = (byte)item.Quality,
                ValueCoins = item.ValueCoins,
                CaughtAtRunMinute = item.CaughtAtRunMinute,
            };
        }

        public CreelItem ToCore()
        {
            return new CreelItem
            {
                EntryId = EntryId,
                ItemIndex = ItemIndex,
                IsJunk = IsJunk != 0,
                WeightG = WeightG,
                Quality = (CatchQuality)Quality,
                ValueCoins = ValueCoins,
                CaughtAtRunMinute = CaughtAtRunMinute,
            };
        }

        public bool Equals(CreelEntry other)
        {
            return EntryId == other.EntryId && ItemIndex == other.ItemIndex && IsJunk == other.IsJunk && WeightG == other.WeightG
                && Quality == other.Quality && ValueCoins == other.ValueCoins && CaughtAtRunMinute == other.CaughtAtRunMinute;
        }

        public override bool Equals(object obj)
        {
            return obj is CreelEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + EntryId;
                h = h * 31 + ItemIndex;
                h = h * 31 + IsJunk;
                h = h * 31 + WeightG;
                h = h * 31 + Quality;
                h = h * 31 + ValueCoins;
                h = h * 31 + CaughtAtRunMinute;
                return h;
            }
        }
    }
}
