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
    }
}
