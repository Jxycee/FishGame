// Authority: owner writes it (FishingRodNetwork.NetRodState); every peer reads it. NGO wire struct (spec 9.9, C 7.17), 20 bytes, no padding.
// Unmanaged and INetworkSerializeByMemcpy, with a field-wise IEquatable so NetworkVariable change detection uses Equals, not memcmp (N3, V11).
using System;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Fishing
{
    public struct RodStateData : INetworkSerializeByMemcpy, IEquatable<RodStateData>
    {
        public byte State;            // FishGame.Core.RodState
        public byte Flags;            // RodWireQuant.Flag*
        public sbyte LateralQ;        // RodWireQuant.QuantizeLateral
        public byte TensionQ;         // RodWireQuant.QuantizeTension
        public Vector3 BobberPos;
        public uint CastId;

        public bool Equals(RodStateData other)
        {
            return State == other.State && Flags == other.Flags && LateralQ == other.LateralQ && TensionQ == other.TensionQ
                && BobberPos.x == other.BobberPos.x && BobberPos.y == other.BobberPos.y && BobberPos.z == other.BobberPos.z
                && CastId == other.CastId;
        }

        public override bool Equals(object obj)
        {
            return obj is RodStateData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + State;
                h = h * 31 + Flags;
                h = h * 31 + LateralQ;
                h = h * 31 + TensionQ;
                h = h * 31 + BobberPos.x.GetHashCode();
                h = h * 31 + BobberPos.y.GetHashCode();
                h = h * 31 + BobberPos.z.GetHashCode();
                h = h * 31 + (int)CastId;
                return h;
            }
        }
    }
}
