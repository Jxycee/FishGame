using System;
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

        // Bounds + ordering helper; counts nibbles sitting exactly on the low clamp (0.5) / high clamp (wait - 0.6).
        static void CheckNibbleBounds(float mult, int samples, ulong seed, out int clampedLow, out int clampedHigh, out int withNibbles)
        {
            FishingTuning t = FishingTestData.Tuning();
            var rng = new Pcg32(seed);
            clampedLow = 0; clampedHigh = 0; withNibbles = 0;
            for (int i = 0; i < samples; i++)
            {
                BitePlan p = BitePlanner.Plan(in t, mult, 3, ref rng);
                float[] n = { p.Nibble0, p.Nibble1, p.Nibble2 };
                float hi = p.BiteWait - 0.6f;
                if (p.BiteWait < 1.4f && p.NibbleCount != 0) Assert.Fail("mult " + mult + ": nibbles on a " + p.BiteWait + " s wait");
                for (int k = 0; k < p.NibbleCount; k++)
                {
                    if (n[k] < 0.5f || n[k] > hi)
                        Assert.Fail("mult " + mult + ": nibble " + k + " = " + n[k] + " outside [0.5, " + hi + "] for wait " + p.BiteWait);
                    if (k > 0 && n[k] < n[k - 1]) Assert.Fail("mult " + mult + ": nibbles not ascending: " + n[k - 1] + " then " + n[k]);
                    if (n[k] == 0.5f) clampedLow++;
                    if (n[k] == hi) clampedHigh++;
                }
                for (int k = p.NibbleCount; k < 3; k++)
                    if (n[k] != 0f) Assert.Fail("mult " + mult + ": unused nibble slot " + k + " = " + n[k]);
                if (p.NibbleCount > 0) withNibbles++;
            }
        }

        // Catches: removing either nibble clamp (unclamped jitter escapes [0.5, wait - 0.6] on short waits), a missing sort,
        // nibbles on a wait below 1.4 s. At mult 1 no clamp can bind (wait >= 4), so only small multipliers prove the clamps.
        [Test]
        public void BitePlanner_SmallMultipliers_NibblesStayInsideBoundsAndClampsBind()
        {
            foreach (float mult in new[] { 0.25f, 0.4f })
            {
                CheckNibbleBounds(mult, 10000, Seed, out int low, out int high, out int withNibbles);
                Assert.That(withNibbles, Is.GreaterThan(1000), "mult " + mult + ": nibbles must occur");
                Assert.That(low + high, Is.GreaterThan(0), "mult " + mult + ": at least one nibble must hit a clamp (0.5 or wait - 0.6)");
                Assert.That(low, Is.GreaterThan(0), "mult " + mult + ": low clamp (0.5) must bind");
                Assert.That(high, Is.GreaterThan(0), "mult " + mult + ": high clamp (wait - 0.6) must bind");
            }
            CheckNibbleBounds(1.0f, 10000, Seed, out int l1, out int h1, out int w1);
            Assert.That(l1 + h1, Is.EqualTo(0), "mult 1: waits are >= 4 s so no clamp can bind");
            Assert.That(w1, Is.GreaterThan(0));
        }

        // Catches: Range(0, maxNibbles) instead of Range(0, maxNibbles + 1) (never the top count), a cap that is not applied
        // (counts above 3), a skewed count draw.
        [Test]
        public void BitePlanner_NibbleCountHistogram_UniformOverZeroToMax()
        {
            FishingTuning t = FishingTestData.Tuning();
            const int n = 20000;
            var rng = new Pcg32(Seed);
            var counts = new int[4];
            for (int i = 0; i < n; i++) counts[BitePlanner.Plan(in t, 1f, 3, ref rng).NibbleCount]++;
            for (int c = 0; c < 4; c++)
                Assert.That(counts[c] / (double)n, Is.EqualTo(0.25).Within(0.03), "count " + c + " histogram " + string.Join(",", counts));

            var rng1 = new Pcg32(Seed + 1);
            var c1 = new int[2];
            for (int i = 0; i < n; i++) c1[BitePlanner.Plan(in t, 1f, 1, ref rng1).NibbleCount]++;
            Assert.That(c1[0] / (double)n, Is.EqualTo(0.5).Within(0.03), "maxNibbles 1: zero");
            Assert.That(c1[1] / (double)n, Is.EqualTo(0.5).Within(0.03), "maxNibbles 1: one");

            // maxNibbles above the slot count is capped at 3; 0 and negative give none.
            var rng7 = new Pcg32(Seed + 2);
            var c7 = new int[8];
            for (int i = 0; i < n; i++) c7[BitePlanner.Plan(in t, 1f, 7, ref rng7).NibbleCount]++;
            for (int c = 4; c < 8; c++) Assert.That(c7[c], Is.EqualTo(0), "maxNibbles 7 must cap at 3");
            for (int c = 0; c < 4; c++) Assert.That(c7[c] / (double)n, Is.EqualTo(0.25).Within(0.03), "maxNibbles 7, count " + c);
            var rng0 = new Pcg32(Seed + 3);
            for (int i = 0; i < 200; i++)
            {
                if (BitePlanner.Plan(in t, 1f, 0, ref rng0).NibbleCount != 0) Assert.Fail("maxNibbles 0 gave nibbles");
                if (BitePlanner.Plan(in t, 1f, -2, ref rng0).NibbleCount != 0) Assert.Fail("maxNibbles -2 gave nibbles");
            }
        }

        // Catches: skipping the count draw when maxNibbles is 0 (every later seeded value shifts), drawing the count before the wait.
        [Test]
        public void BitePlanner_MaxNibblesZero_ConsumesExactlyWaitThenCountDraw()
        {
            FishingTuning t = FishingTestData.Tuning();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var rng = new Pcg32(seed);
                Pcg32 hand = rng;                                       // value copy of the stream
                BitePlan p = BitePlanner.Plan(in t, 1.25f, 0, ref rng);
                float waitBase = hand.Range(4f, 18f);                   // draw 1 (float overload)
                int n = hand.Range(0, 1);                               // draw 2 (int overload, always made; one NextUInt)
                Assert.That(n, Is.EqualTo(0));
                Assert.That(p.BiteWait, Is.EqualTo(Math.Max(1f, waitBase * 1.25f)), "seed " + seed);
                Assert.That(p.NibbleCount, Is.EqualTo((byte)0));
                Assert.That(rng.NextUInt(), Is.EqualTo(hand.NextUInt()), "seed " + seed + ": exactly two draws consumed");
            }
        }

        // Catches: reordered draws (jitters before the count), a missing or extra jitter draw, a wrong jitter range, a wrong sort,
        // a wrong spacing formula wait * (k + 1) / (n + 1).
        [Test]
        public void BitePlanner_HandReplayOfDrawOrder_MatchesPlanAndLeavesSameRngState()
        {
            FishingTuning t = FishingTestData.Tuning();
            int seen3 = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var rng = new Pcg32(seed);
                Pcg32 hand = rng;
                BitePlan p = BitePlanner.Plan(in t, 1.1f, 3, ref rng);

                float waitBase = hand.Range(4f, 18f);
                float wait = Math.Max(1f, waitBase * 1.1f);
                int n = hand.Range(0, 4);
                var offs = new float[n];
                for (int k = 0; k < n; k++)
                {
                    float o = wait * (k + 1) / (n + 1) + hand.Range(-0.3f, 0.3f);
                    if (o > wait - 0.6f) o = wait - 0.6f;
                    if (o < 0.5f) o = 0.5f;
                    offs[k] = o;
                }
                Array.Sort(offs);
                if (n == 3) seen3++;

                Assert.That(p.BiteWait, Is.EqualTo(wait), "seed " + seed);
                Assert.That((int)p.NibbleCount, Is.EqualTo(n), "seed " + seed);
                float[] got = { p.Nibble0, p.Nibble1, p.Nibble2 };
                for (int k = 0; k < 3; k++)
                    Assert.That(got[k], Is.EqualTo(k < n ? offs[k] : 0f), "seed " + seed + " nibble " + k);
                Assert.That(rng.NextUInt(), Is.EqualTo(hand.NextUInt()), "seed " + seed + ": same draw count");
            }
            Assert.That(seen3, Is.GreaterThan(20), "the replay must cover three-nibble plans");
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
