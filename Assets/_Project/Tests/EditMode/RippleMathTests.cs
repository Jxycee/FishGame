using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RippleMathTests
    {
        [Test]
        public void RippleMath_Ring_PeaksOnTheFrontAndFadesByLifetime()
        {
            Assert.AreEqual(0.6f, RippleMath.Ring(2.2f, 1f, 1f), 1e-6f);
            Assert.AreEqual(0f, RippleMath.Ring(1.95f, 1f, 1f), 1e-5f);
            Assert.AreEqual(0f, RippleMath.Ring(2.45f, 1f, 1f), 1e-5f);
            Assert.AreEqual(0f, RippleMath.Ring(0f, 1f, 1f), 0f, "far inside the front");
            Assert.AreEqual(1f, RippleMath.Ring(0f, 0f, 1f), 1e-6f);
            Assert.AreEqual(0.7f, RippleMath.Ring(0f, 0f, 0.7f), 1e-6f, "strength scales the ring");

            float prev = float.MaxValue;
            foreach (float age in new[] { 0.5f, 1.0f, 1.5f, 2.0f })
            {
                float v = RippleMath.Ring(age * RippleMath.RingSpeed, age, 1f);
                Assert.Less(v, prev, "front value decreases at age " + age);
                prev = v;
            }

            foreach (float dist in new[] { 0f, 1f, 5.5f, 20f })
            {
                Assert.AreEqual(0f, RippleMath.Ring(dist, 2.5f, 1f), 0f, "age 2.5 dist " + dist);
                Assert.AreEqual(0f, RippleMath.Ring(dist, 3.0f, 1f), 0f, "age 3.0 dist " + dist);
                // C-Q5: a ripple stamped ahead of the water clock is invisible, even when the shifted front would overlap dist.
                Assert.AreEqual(0f, RippleMath.Ring(dist, -0.05f, 1f), 0f, "age -0.05 dist " + dist);
                Assert.AreEqual(0f, RippleMath.Ring(dist, -0.1f, 1f), 0f, "age -0.1 dist " + dist);
                Assert.AreEqual(0f, RippleMath.Ring(dist, -1f, 1f), 0f, "age -1 dist " + dist);
            }
            Assert.AreEqual(0f, RippleMath.Ring(float.NaN, 1f, 1f), 0f);
            Assert.AreEqual(0f, RippleMath.Ring(1f, float.NaN, 1f), 0f);

            Assert.AreEqual(8, RippleMath.Slots);
            Assert.AreEqual(2.2f, RippleMath.RingSpeed, 0f);
            Assert.AreEqual(0.25f, RippleMath.RingWidth, 0f);
            Assert.AreEqual(2.5f, RippleMath.Lifetime, 0f);
            Assert.AreEqual(0.6f, RippleMath.CastLanding, 0f);
            Assert.AreEqual(0.2f, RippleMath.Nibble, 0f);
            Assert.AreEqual(1.0f, RippleMath.Bite, 0f);
            Assert.AreEqual(0.3f, RippleMath.BiteFollow, 0f);
            Assert.AreEqual(0.8f, RippleMath.Snap, 0f);
            Assert.AreEqual(0.7f, RippleMath.Landed, 0f);
            Assert.AreEqual(1.0f, RippleMath.Shove, 0f);
        }

        [Test]
        public void RippleMath_NextSlot_RecyclesOldest()
        {
            int slot = -1;
            for (int i = 0; i < RippleMath.Slots; i++)
            {
                slot = RippleMath.NextSlot(slot);
                Assert.AreEqual(i, slot);
            }
            Assert.AreEqual(0, RippleMath.NextSlot(slot), "ninth call recycles the oldest (slot 0)");
            Assert.AreEqual(0, RippleMath.NextSlot(7));
            Assert.AreEqual(0, RippleMath.NextSlot(8));
            Assert.AreEqual(0, RippleMath.NextSlot(-5));
        }
    }
}
