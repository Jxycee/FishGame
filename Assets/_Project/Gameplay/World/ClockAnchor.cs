// Gameplay/World/ClockAnchor.cs - wire mirror of Core ClockAnchorData for WorldState.NetClock (C 7.14, C2 G1).
// Authority: none (wire data for WorldState.NetClock). Unmanaged and memcpy-serialized: never INetworkSerializable.
// Equals compares with double.Equals / float.Equals, so NaN equals NaN and a NaN can never cause endless re-sends.
using System;
using FishGame.Core;
using Unity.Netcode;

namespace FishGame.Gameplay
{
    public struct ClockAnchor : INetworkSerializeByMemcpy, IEquatable<ClockAnchor>
    {
        public double AnchorServerTime;
        public float AnchorMinute;
        public float MinutesPerSecond;
        public byte Running;          // 0 / 1

        public static ClockAnchor From(in ClockAnchorData d)
        {
            return new ClockAnchor
            {
                AnchorServerTime = d.AnchorServerTime, AnchorMinute = d.AnchorMinute, MinutesPerSecond = d.MinutesPerSecond,
                Running = d.Running ? (byte)1 : (byte)0,
            };
        }

        public ClockAnchorData ToCore()
        {
            return new ClockAnchorData
            {
                AnchorServerTime = AnchorServerTime, AnchorMinute = AnchorMinute, MinutesPerSecond = MinutesPerSecond, Running = Running != 0,
            };
        }

        public bool Equals(ClockAnchor o)
        {
            return AnchorServerTime.Equals(o.AnchorServerTime) && AnchorMinute.Equals(o.AnchorMinute)
                   && MinutesPerSecond.Equals(o.MinutesPerSecond) && Running == o.Running;
        }

        public override bool Equals(object obj) => obj is ClockAnchor o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = AnchorServerTime.GetHashCode();
                h = h * 31 + AnchorMinute.GetHashCode();
                h = h * 31 + MinutesPerSecond.GetHashCode();
                h = h * 31 + Running;
                return h;
            }
        }
    }
}
