// Gameplay/World/HotspotState.cs - wire mirror of Core HotspotData for WorldState.NetHotspot (spec 12.7 shape, C 7.14, C2 G1).
// Authority: none (wire data for WorldState.NetHotspot). StartMinute is not replicated (ToCore gives 0). Equality as ClockAnchor.
using System;
using FishGame.Core;
using Unity.Netcode;

namespace FishGame.Gameplay
{
    public struct HotspotState : INetworkSerializeByMemcpy, IEquatable<HotspotState>
    {
        public float X, Z, Radius;
        public int EndMinute;
        public byte Active;           // 0 / 1

        public static HotspotState From(in HotspotData d)
        {
            return new HotspotState { X = d.X, Z = d.Z, Radius = d.Radius, EndMinute = d.EndMinute, Active = d.Active ? (byte)1 : (byte)0 };
        }

        public HotspotData ToCore()
        {
            return new HotspotData { Active = Active != 0, X = X, Z = Z, Radius = Radius, StartMinute = 0, EndMinute = EndMinute };
        }

        public bool Equals(HotspotState o)
        {
            return X.Equals(o.X) && Z.Equals(o.Z) && Radius.Equals(o.Radius) && EndMinute == o.EndMinute && Active == o.Active;
        }

        public override bool Equals(object obj) => obj is HotspotState o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = X.GetHashCode();
                h = h * 31 + Z.GetHashCode();
                h = h * 31 + Radius.GetHashCode();
                h = h * 31 + EndMinute;
                h = h * 31 + Active;
                return h;
            }
        }
    }
}
