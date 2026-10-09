using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RngTests
    {
        [Test]
        public void Rng_SplitMix64_ZeroInput_MatchesReferenceVector()
        {
            // First output of Vigna's splitmix64 with state 0.
            Assert.That(Rng.SplitMix64(0UL), Is.EqualTo(0xE220A8397B1DCDAFUL));
        }

        [Test]
        public void Rng_Derive_IsDeterministicAndStreamIndependent()
        {
            ulong a = Rng.Derive(1, "fish", 0);
            Assert.That(Rng.Derive(1, "fish", 0), Is.EqualTo(a));
            Assert.That(Rng.Derive(1, "bait", 0), Is.Not.EqualTo(a));
            Assert.That(Rng.Derive(1, "fish", 1), Is.Not.EqualTo(a));
            Assert.That(Rng.Derive(2, "fish", 0), Is.Not.EqualTo(a));
        }

        [Test]
        public void Rng_Derive_NeverReturnsZero()
        {
            for (ulong i = 0; i < 10000; i++)
            {
                Assert.That(Rng.Derive(0, "", i), Is.Not.EqualTo(0UL), "index " + i);
            }
        }

        [Test]
        public void Rng_Stream_EqualsPcg32SeededWithDerive()
        {
            var fromStream = Rng.Stream(99, "weather", 3);
            var manual = new Pcg32(Rng.Derive(99, "weather", 3));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(fromStream.NextUInt(), Is.EqualTo(manual.NextUInt()), "draw " + i);
            }
        }
    }
}
