using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class Pcg32Tests
    {
        [Test]
        public void Pcg32_Seed42Stream54_MatchesReferenceVector()
        {
            // Reference output of O'Neill's pcg32-demo: pcg32_srandom_r(&rng, 42u, 54u).
            var rng = new Pcg32(42, 54);
            uint[] expected =
            {
                0xa15c02b7u, 0x7b47f409u, 0xba1d3330u,
                0x83d2f293u, 0xbfa4784bu, 0xcbed606eu
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(rng.NextUInt(), Is.EqualTo(expected[i]), "draw " + i);
            }
        }

        [Test]
        public void Pcg32_ZeroSeed_IsReplaced()
        {
            var zero = new Pcg32(0, 54);
            var replaced = new Pcg32(Pcg32.ZeroSeedReplacement, 54);
            for (int i = 0; i < 5; i++)
            {
                Assert.That(zero.NextUInt(), Is.EqualTo(replaced.NextUInt()), "draw " + i);
            }
        }

        [Test]
        public void Pcg32_RangeInt_StaysInBounds()
        {
            var rng = new Pcg32(7, 1);
            bool sawMin = false, sawMaxMinusOne = false;
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(-3, 7);
                Assert.That(v, Is.GreaterThanOrEqualTo(-3));
                Assert.That(v, Is.LessThan(7));
                if (v == -3) sawMin = true;
                if (v == 6) sawMaxMinusOne = true;
            }
            Assert.That(sawMin, Is.True, "min value never drawn");
            Assert.That(sawMaxMinusOne, Is.True, "max-1 value never drawn");
            Assert.Throws<ArgumentException>(() => rng.Range(5, 5));
            Assert.Throws<ArgumentException>(() => rng.Range(5, 4));
        }

        [Test]
        public void Pcg32_FloatDraws_StayInHalfOpenRanges()
        {
            var rng = new Pcg32(123, 0);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f));
                Assert.That(f, Is.LessThan(1f));

                double d = rng.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(d, Is.LessThan(1.0));

                float r = rng.Range(-2f, 3f);
                Assert.That(r, Is.GreaterThanOrEqualTo(-2f));
                Assert.That(r, Is.LessThan(3f));
            }
        }

        [Test]
        public void Pcg32_RangeFloat_NeverReturnsMax()
        {
            float top = 0.99999994f; // largest NextFloat value
            float r1 = Pcg32.MapUnitToRange(top, 1f, 2f);
            Assert.That(r1, Is.LessThan(2f));
            Assert.That(r1, Is.EqualTo(BitConverter.Int32BitsToSingle(0x3FFFFFFF)));

            float r2 = Pcg32.MapUnitToRange(top, -2f, -1f);
            Assert.That(r2, Is.LessThan(-1f));
            Assert.That(r2, Is.EqualTo(BitConverter.Int32BitsToSingle(unchecked((int)0xBF800001))));

            Assert.That(Pcg32.MapUnitToRange(1f, -1f, 0f), Is.EqualTo(-float.Epsilon));
            Assert.That(Pcg32.MapUnitToRange(0f, 1f, 2f), Is.EqualTo(1f));
        }

        [Test]
        public void Pcg32_RangeFloat_MaxNotAboveMin_Throws()
        {
            var a = new Pcg32(5, 2);
            Assert.Throws<ArgumentException>(() => a.Range(1f, 1f));
            Assert.Throws<ArgumentException>(() => a.Range(2f, 1f));
            Assert.Throws<ArgumentException>(() => a.Range(float.NaN, 1f));
            Assert.Throws<ArgumentException>(() => a.Range(0f, float.NaN));
            var fresh = new Pcg32(5, 2);
            Assert.That(a.NextUInt(), Is.EqualTo(fresh.NextUInt()), "a rejected call must not advance the stream");
        }

        [Test]
        public void Pcg32_RangeFloat_DrawCountUnchanged()
        {
            var a = new Pcg32(99, 3);
            var b = new Pcg32(99, 3);
            for (int i = 0; i < 10000; i++)
            {
                float x = a.Range(-3f, 5f);
                float u = b.NextFloat();
                float old = -3f + u * (5f - -3f);
                Assert.That(x, Is.EqualTo(Pcg32.MapUnitToRange(u, -3f, 5f)), "draw " + i);
                if (old < 5f) Assert.That(x, Is.EqualTo(old), "M1 value preserved, draw " + i);
            }
            for (int i = 0; i < 3; i++)
            {
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()), "tail " + i);
            }
        }
    }
}
