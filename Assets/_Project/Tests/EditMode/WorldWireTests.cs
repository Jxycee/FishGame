// Tests/EditMode/WorldWireTests.cs - T14 (C 10.4): the WorldState wire mirrors ClockAnchor and HotspotState (C2 G1 unmanaged memcpy).
using FishGame.Core;
using FishGame.Gameplay;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace FishGame.Tests.EditMode
{
    public class WorldWireTests
    {
        // Catches: From dropping Running (or any field); float == in Equals (two NaN anchors would never compare equal, so NGO would
        // re-send the variable forever).
        [Test]
        public void ClockAnchor_FromToCore_RoundTripAndEquality()
        {
            foreach (bool running in new[] { true, false })
            {
                var d = new ClockAnchorData { AnchorServerTime = 12345.678901234, AnchorMinute = 417.25f, MinutesPerSecond = 1.3333334f, Running = running };
                ClockAnchor wire = ClockAnchor.From(in d);
                ClockAnchorData back = wire.ToCore();
                Assert.That(back.AnchorServerTime, Is.EqualTo(d.AnchorServerTime), "the double survives exactly");
                Assert.That(back.AnchorMinute, Is.EqualTo(d.AnchorMinute));
                Assert.That(back.MinutesPerSecond, Is.EqualTo(d.MinutesPerSecond));
                Assert.That(back.Running, Is.EqualTo(running));
                Assert.That(wire.Running, Is.EqualTo(running ? (byte)1 : (byte)0));
            }

            var a = ClockAnchor.From(new ClockAnchorData { AnchorServerTime = 10.5, AnchorMinute = 12f, MinutesPerSecond = 2f, Running = true });
            ClockAnchor same = a;
            Assert.That(a.Equals(same), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            ClockAnchor b = a; b.AnchorServerTime = 10.25;
            Assert.That(a.Equals(b), Is.False, "AnchorServerTime");
            b = a; b.AnchorMinute = 13f;
            Assert.That(a.Equals(b), Is.False, "AnchorMinute");
            b = a; b.MinutesPerSecond = 3f;
            Assert.That(a.Equals(b), Is.False, "MinutesPerSecond");
            b = a; b.Running = 0;
            Assert.That(a.Equals(b), Is.False, "Running");

            ClockAnchor n1 = a; n1.AnchorMinute = float.NaN;
            ClockAnchor n2 = a; n2.AnchorMinute = float.NaN;
            Assert.That(n1.Equals(n2), Is.True, "NaN equals NaN: no endless re-send");
        }

        // Catches: a field dropped or swapped in From/ToCore; StartMinute replicated (it is not on the wire); Active dropped.
        [Test]
        public void HotspotState_FromToCore_RoundTripAndEquality()
        {
            var h = new HotspotData { Active = true, X = 3.5f, Z = -12.25f, Radius = 3f, StartMinute = 200, EndMinute = 250 };
            HotspotState wire = HotspotState.From(in h);
            HotspotData back = wire.ToCore();
            Assert.That(back.Active, Is.True);
            Assert.That(back.X, Is.EqualTo(3.5f));
            Assert.That(back.Z, Is.EqualTo(-12.25f));
            Assert.That(back.Radius, Is.EqualTo(3f));
            Assert.That(back.EndMinute, Is.EqualTo(250));
            Assert.That(back.StartMinute, Is.EqualTo(0), "StartMinute is not replicated");

            HotspotState same = wire;
            Assert.That(wire.Equals(same), Is.True);
            Assert.That(wire.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            HotspotState c = wire; c.X = 4f;
            Assert.That(wire.Equals(c), Is.False, "X");
            c = wire; c.Z = 0f;
            Assert.That(wire.Equals(c), Is.False, "Z");
            c = wire; c.Radius = 2f;
            Assert.That(wire.Equals(c), Is.False, "Radius");
            c = wire; c.EndMinute = 251;
            Assert.That(wire.Equals(c), Is.False, "EndMinute");
            c = wire; c.Active = 0;
            Assert.That(wire.Equals(c), Is.False, "Active");

            HotspotData off = HotspotState.From(new HotspotData { Active = false, X = 1f }).ToCore();
            Assert.That(off.Active, Is.False);
        }

        // Catches: an added field or a wider running flag growing the per-change payload.
        [Test]
        public void ClockAnchor_Size_AtMostTwentyFourBytes()
        {
            int anchor = UnsafeUtility.SizeOf<ClockAnchor>();
            int hotspot = UnsafeUtility.SizeOf<HotspotState>();
            TestContext.WriteLine("FG WIRE ClockAnchor=" + anchor + " HotspotState=" + hotspot);
            Assert.That(anchor, Is.LessThanOrEqualTo(24));
            Assert.That(hotspot, Is.LessThanOrEqualTo(20));
        }
    }
}
