// Authority: host writes (through SessionState); every peer reads. Unmanaged wire form of FishGame.Core.PlayerKey.
using System;
using FishGame.Core;
using Unity.Collections;
using Unity.Netcode;

namespace FishGame.Gameplay
{
    public struct NetPlayerId : INetworkSerializable, IEquatable<NetPlayerId>
    {
        public byte Kind;
        public FixedString64Bytes Id;

        public static NetPlayerId From(PlayerKey key)
        {
            return new NetPlayerId
            {
                Kind = (byte)key.Kind,
                Id = SessionText.Id(key.Id),
            };
        }

        public PlayerKey ToKey()
        {
            return new PlayerKey((IdentityKind)Kind, Id.ToString());
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Id);
        }

        public bool Equals(NetPlayerId other)
        {
            return Kind == other.Kind && Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is NetPlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Kind * 397) ^ Id.GetHashCode();
        }
    }
}
