using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class CatchMathTests
    {
        static void AssertSameCatch(in CatchResult want, in CatchResult got)
        {
            Assert.That(got.IsJunk, Is.EqualTo(want.IsJunk));
            Assert.That(got.ItemIndex, Is.EqualTo(want.ItemIndex));
            Assert.That(got.WeightG, Is.EqualTo(want.WeightG));
            Assert.That(got.LengthMm, Is.EqualTo(want.LengthMm));
            Assert.That(got.Quality, Is.EqualTo(want.Quality));
            Assert.That(got.ValueCoins, Is.EqualTo(want.ValueCoins));
        }

        [Test]
        public void CatchMath_WeightDistribution_MeanAndTrophyRate()
        {
            FishSpeciesData carp = FishingTestData.Species()[FishingTestData.SpeciesCarp];
            var rng = new Pcg32(1234);
            const int n = 20000;
            int trophy = 0, good = 0;
            double sumT = 0;
            for (int i = 0; i < n; i++)
            {
                CatchResult r = CatchMath.RollFish(in carp, (ushort)FishingTestData.SpeciesCarp, 0f, ref rng, out float t);
                if (r.Quality == CatchQuality.Trophy) trophy++;
                else if (r.Quality == CatchQuality.Good) good++;
                sumT += t;
                if (r.IsJunk || r.ItemIndex != FishingTestData.SpeciesCarp) Assert.Fail("not a carp roll");
                if (r.WeightG < carp.WeightMinG || r.WeightG > carp.WeightMaxG) Assert.Fail("weight " + r.WeightG);
            }
            Assert.That(trophy / (double)n, Is.EqualTo(0.0468).Within(0.006), "Trophy rate");
            Assert.That(good / (double)n, Is.EqualTo(0.1604).Within(0.01), "Good rate");
            Assert.That(sumT / n, Is.EqualTo(0.3125).Within(0.01), "mean t");
        }

        [Test]
        public void CatchMath_Value_AwayFromZeroAndMinimumOne()
        {
            Assert.That(CatchMath.Value(3, 0.5f, CatchQuality.Trophy), Is.EqualTo(5));   // 4.5 rounds away from zero
            Assert.That(CatchMath.Value(0, 0f, CatchQuality.Normal), Is.EqualTo(1));
            Assert.That(CatchMath.Value(1, 0f, CatchQuality.Normal), Is.EqualTo(1));
            Assert.That(CatchMath.Value(12, 0.7f, CatchQuality.Good), Is.EqualTo(16));
            Assert.That(CatchMath.Value(12, 0.7f, CatchQuality.Trophy), Is.EqualTo(21));
        }

        [Test]
        public void CatchMath_LengthMm_FollowsCubeRoot()
        {
            Assert.That(CatchMath.LengthMm(1000, 1f), Is.EqualTo((ushort)400));
            Assert.That(CatchMath.LengthMm(8000, 1f), Is.EqualTo((ushort)800));
            Assert.That(CatchMath.LengthMm(125, 1f), Is.EqualTo((ushort)200));
            Assert.That(CatchMath.LengthMm(1000, 1.25f), Is.EqualTo((ushort)500));
        }

        [Test]
        public void CatchMath_SizeClass_FromWeightThirds()
        {
            Assert.That(CatchMath.SizeClass(0f), Is.EqualTo((byte)0));
            Assert.That(CatchMath.SizeClass(0.33f), Is.EqualTo((byte)0));
            Assert.That(CatchMath.SizeClass(0.34f), Is.EqualTo((byte)1));
            Assert.That(CatchMath.SizeClass(0.66f), Is.EqualTo((byte)1));
            Assert.That(CatchMath.SizeClass(0.67f), Is.EqualTo((byte)2));
            Assert.That(CatchMath.SizeClass(1f), Is.EqualTo((byte)2));
        }

        [Test]
        public void CatchMath_UpgradeOneTier_NormalGoodTrophyCapped()
        {
            Assert.That(CatchMath.UpgradeOneTier(CatchQuality.Normal), Is.EqualTo(CatchQuality.Good));
            Assert.That(CatchMath.UpgradeOneTier(CatchQuality.Good), Is.EqualTo(CatchQuality.Trophy));
            Assert.That(CatchMath.UpgradeOneTier(CatchQuality.Trophy), Is.EqualTo(CatchQuality.Trophy));
        }

        [Test]
        public void CatchMath_WithCleanBonus_RecomputesValueKeepsWeight()
        {
            FishSpeciesData[] species = FishingTestData.Species();
            FishSpeciesData carp = species[FishingTestData.SpeciesCarp];
            var good = new CatchResult { IsJunk = false, ItemIndex = (ushort)FishingTestData.SpeciesCarp, WeightG = 3100,
                                         LengthMm = CatchMath.LengthMm(3100, carp.LengthShapeK), Quality = CatchQuality.Good, ValueCoins = 16 };
            CatchResult up = CatchMath.WithCleanBonus(in good, in carp, 0.7f);
            Assert.That(up.Quality, Is.EqualTo(CatchQuality.Trophy));
            Assert.That(up.ValueCoins, Is.EqualTo(21));
            Assert.That(up.WeightG, Is.EqualTo(good.WeightG));
            Assert.That(up.LengthMm, Is.EqualTo(good.LengthMm));
            Assert.That(up.ItemIndex, Is.EqualTo(good.ItemIndex));
            Assert.That(up.IsJunk, Is.False);

            FishSpeciesData bluegill = species[FishingTestData.SpeciesBluegill];
            var normal = new CatchResult { IsJunk = false, ItemIndex = (ushort)FishingTestData.SpeciesBluegill, WeightG = 380,
                                           LengthMm = CatchMath.LengthMm(380, bluegill.LengthShapeK), Quality = CatchQuality.Normal, ValueCoins = 4 };
            CatchResult up2 = CatchMath.WithCleanBonus(in normal, in bluegill, 0.3f);
            Assert.That(up2.Quality, Is.EqualTo(CatchQuality.Good));
            Assert.That(up2.ValueCoins, Is.EqualTo(5));
            Assert.That(up2.WeightG, Is.EqualTo(380));

            // Trophy stays Trophy; the value is recomputed with the same tier.
            var trophy = new CatchResult { ItemIndex = (ushort)FishingTestData.SpeciesCarp, WeightG = 3900, Quality = CatchQuality.Trophy, ValueCoins = 21 };
            CatchResult same = CatchMath.WithCleanBonus(in trophy, in carp, 0.7f);
            Assert.That(same.Quality, Is.EqualTo(CatchQuality.Trophy));
            Assert.That(same.ValueCoins, Is.EqualTo(21));
        }

        [Test]
        public void CatchMath_WithCleanBonus_JunkUnchanged()
        {
            FishSpeciesData carp = FishingTestData.Species()[FishingTestData.SpeciesCarp];
            var junkData = new JunkData { ValueCoins = 2 };
            CatchResult junk = CatchMath.Junk(0, in junkData);
            Assert.That(junk.IsJunk, Is.True);
            Assert.That(junk.WeightG, Is.EqualTo(0));
            Assert.That(junk.LengthMm, Is.EqualTo((ushort)0));
            Assert.That(junk.Quality, Is.EqualTo(CatchQuality.Normal));
            Assert.That(junk.ValueCoins, Is.EqualTo(2));
            CatchResult after = CatchMath.WithCleanBonus(in junk, in carp, 0.9f);
            AssertSameCatch(in junk, in after);
        }
    }
}
