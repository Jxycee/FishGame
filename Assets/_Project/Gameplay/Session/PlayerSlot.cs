// Authority: host writes (through SessionState); every peer reads. One roster row; unmanaged so it can live in a NetworkList.
using System;
using Unity.Collections;
using Unity.Netcode;

namespace FishGame.Gameplay
{
    public struct PlayerSlot : INetworkSerializable, IEquatable<PlayerSlot>
    {
        public ulong ClientId;
        public NetPlayerId Id;
        public FixedString32Bytes Name;
        public byte Slot;
        public bool Connected;
        public bool Ready;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Connected);
            serializer.SerializeValue(ref Ready);
        }

        public bool Equals(PlayerSlot other)
        {
            return ClientId == other.ClientId
                && Id.Equals(other.Id)
                && Name.Equals(other.Name)
                && Slot == other.Slot
                && Connected == other.Connected
                && Ready == other.Ready;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, Id.GetHashCode(), Slot, Connected, Ready);
        }
    }
}
