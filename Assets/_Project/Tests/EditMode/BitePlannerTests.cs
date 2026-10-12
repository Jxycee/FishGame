using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class BitePlannerTests
    {
        const ulong Seed = 1234;

        [Test]
        public void BitePlanner_TenThousandSamples_WithinBoundsAndFloor()
        {
            FishingTuning t = FishingTestData.Tuning();
            foreach (float mult in new[] { 0.2f, 1.0f, 1.7f })
            {
                var rng = new Pcg32(Seed);
                int atFloor = 0;
                float maxWait = 0f;
                for (int i = 0; i < 10000; i++)
                {
                    BitePlan p = BitePlanner.Plan(in t, mult, 3, ref rng);
                    if (p.BiteWait < 1.0f) Assert.Fail("BiteWait below the 1.0 floor: " + p.BiteWait + " (mult " + mult + ")");
                    if (p.NibbleCount > 3) Assert.Fail("NibbleCount " + p.NibbleCount);
                    if (p.BiteWait == 1.0f) atFloor++;
                    if (p.BiteWait > maxWait) maxWait = p.BiteWait;
                    if (mult >= 1.0f)
                    {
                        if (p.BiteWait < 4f * mult || p.BiteWait >= 18f * mult)
                            Assert.Fail("BiteWait " + p.BiteWait + " outside [" + 4f * mult + ", " + 18f * mult + ") for mult " + mult);
                    }
                }
                if (mult < 1.0f)
                {
                    Assert.That(atFloor, Is.GreaterThan(0), "the floor must bind for mult " + mult);
                    Assert.That(maxWait, Is.LessThanOrEqualTo(3.6f + 1e-4f), "max wait for mult " + mult);
                }
            }
        }

        [Test]
        public void BitePlanner_Means_FollowBaitSpeciesAndDepthMultipliers()
        {
            Assert.That(BitePlanner.DepthMult(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BitePlanner.DepthMult(3f), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(BitePlanner.DepthMult(6f), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(BitePlanner.DepthMult(12f), Is.EqualTo(0.8f).Within(1e-5f));

            float m1 = BitePlanner.WaitMultiplier(0.9f, 1.4f, 6f);
            float m2 = BitePlanner.WaitMultiplier(1.3f, 0.9f, 0f);
            Assert.That(m1, Is.EqualTo(1.008f).Within(1e-5f));
            Assert.That(m2, Is.EqualTo(1.17f).Within(1e-5f));
            Assert.That(BitePlanner.WaitMultiplier(0.9f, 1.4f, 6f, inHotspot: true), Is.EqualTo(1.008f * 0.6f).Within(1e-5f));
            Assert.That(BitePlanner.WaitMultiplier(1f, 1f, 0f, eventMult: 2f, skillMult: 0.5f), Is.EqualTo(1f).Within(1e-5f));

            FishingTuning t = FishingTestData.Tuning();
            var rng = new Pcg32(Seed);
            double sum1 = 0, sum2 = 0;
            for (int i = 0; i < 10000; i++) sum1 += BitePlanner.Plan(in t, m1, 3, ref rng).BiteWait;
            for (int i = 0; i < 10000; i++) sum2 += BitePlanner.Plan(in t, m2, 3, ref rng).BiteWait;
            Assert.That(sum1 / 10000.0, Is.EqualTo(11.088).Within(0.15));
            Assert.That(sum2 / 10000.0, Is.EqualTo(12.87).Within(0.15));
        }

        [Test]
        public void BitePlanner_Nibbles_OrderedInsideWaitAndClearOfBite()
        {
            FishingTuning t = FishingTestData.Tuning();
            var rng = new Pcg32(Seed);
            int withNibbles = 0;
            for (int i = 0; i < 10000; i++)
            {
                BitePlan p = BitePlanner.Plan(in t, 1f, 3, ref rng);
                float[] n = { p.Nibble0, p.Nibble1, p.Nibble2 };
                for (int k = 0; k < p.NibbleCount; k++)
                {
                    if (n[k] < 0.5f || n[k] > p.BiteWait - 0.6f)
                        Assert.Fail("nibble " + k + " = " + n[k] + " outside [0.5, " + (p.BiteWait - 0.6f) + "] for wait " + p.BiteWait);
                    if (k > 0 && n[k] < n[k - 1]) Assert.Fail("nibbles not ascending: " + n[k - 1] + " then " + n[k]);
                }
                for (int k = p.NibbleCount; k < 3; k++)
                    if (n[k] != 0f) Assert.Fail("unused nibble slot " + k + " = " + n[k]);
                if (p.NibbleCount > 0) withNibbles++;
            }
            Assert.That(withNibbles, Is.GreaterThan(0));
        }

        [Test]
        public void BitePlanner_ShortWait_ForcesZeroNibbles()
        {
            FishingTuning t = FishingTestData.Tuning();
            t.BiteWaitMin = 1.0f; t.BiteWaitMax = 1.3f; t.MinBiteWait = 1.0f;
            var rng = new Pcg32(Seed);
            for (int i = 0; i < 1000; i++)
            {
                BitePlan p = BitePlanner.Plan(in t, 1f, 3, ref rng);
                if (p.NibbleCount != 0) Assert.Fail("NibbleCount " + p.NibbleCount + " for wait " + p.BiteWait);
                if (!(p.BiteWait < 1.4f)) Assert.Fail("BiteWait " + p.BiteWait);
            }
        }

        [Test]
        public void BitePlanner_SameSeed_SamePlan()
        {
            FishingTuning t = FishingTestData.Tuning();
            var a = new Pcg32(99);
            var b = new Pcg32(99);
            for (int i = 0; i < 50; i++)
            {
                BitePlan pa = BitePlanner.Plan(in t, 1.1f, 3, ref a);
                BitePlan pb = BitePlanner.Plan(in t, 1.1f, 3, ref b);
                Assert.That(pa.BiteWait, Is.EqualTo(pb.BiteWait));
                Assert.That(pa.NibbleCount, Is.EqualTo(pb.NibbleCount));
                Assert.That(pa.Nibble0, Is.EqualTo(pb.Nibble0));
                Assert.That(pa.Nibble1, Is.EqualTo(pb.Nibble1));
                Assert.That(pa.Nibble2, Is.EqualTo(pb.Nibble2));
            }
            Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()), "same draw count");
        }
    }
}
