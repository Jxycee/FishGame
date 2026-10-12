using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class PlaceholderRollerTests
    {
        static RollRequest Req(int bait, float depth) =>
            new RollRequest { BaitIndex = bait, LoadedCount = 1, Depth = depth, Luck = 0f,
                              ForcedBiteWait = 0f, ForcedItemIndex = -1, ForceJunk = false, Twitchy = false };

        static void AssertSameRoll(in FishingRoll a, in FishingRoll b)
        {
            Assert.That(b.Catch.IsJunk, Is.EqualTo(a.Catch.IsJunk));
            Assert.That(b.Catch.ItemIndex, Is.EqualTo(a.Catch.ItemIndex));
            Assert.That(b.Catch.WeightG, Is.EqualTo(a.Catch.WeightG));
            Assert.That(b.Catch.LengthMm, Is.EqualTo(a.Catch.LengthMm));
            Assert.That(b.Catch.Quality, Is.EqualTo(a.Catch.Quality));
            Assert.That(b.Catch.ValueCoins, Is.EqualTo(a.Catch.ValueCoins));
            Assert.That(b.WeightT, Is.EqualTo(a.WeightT));
            Assert.That(b.BiteWait, Is.EqualTo(a.BiteWait));
            Assert.That(b.NibbleCount, Is.EqualTo(a.NibbleCount));
            Assert.That(b.Nibble0, Is.EqualTo(a.Nibble0));
            Assert.That(b.Nibble1, Is.EqualTo(a.Nibble1));
            Assert.That(b.Nibble2, Is.EqualTo(a.Nibble2));
            Assert.That(b.FightSeed, Is.EqualTo(a.FightSeed));
            Assert.That(b.SizeClass, Is.EqualTo(a.SizeClass));
            Assert.That(b.HookWindow, Is.EqualTo(a.HookWindow));
            Assert.That(b.Pity, Is.EqualTo(a.Pity));
        }

        [Test]
        public void PlaceholderRoller_TenThousandRolls_FrequenciesWithinTwoPercentOfWeights()
        {
            FishingDatabase db = FishingTestData.Database();
            RollRequest req = Req(FishingTestData.BaitWorm, 3f);   // eff 3: bluegill, carp, perch (1 each) + junk 0.2 x 1.0
            var rng = new Pcg32(1234);
            const int n = 10000;
            int bluegill = 0, carp = 0, perch = 0, junk = 0;
            for (int i = 0; i < n; i++)
            {
                FishingRoll r = PlaceholderRoller.Roll(db, in req, ref rng);
                if (r.Catch.IsJunk) { junk++; continue; }
                switch (r.Catch.ItemIndex)
                {
                    case FishingTestData.SpeciesBluegill: bluegill++; break;
                    case FishingTestData.SpeciesCarp: carp++; break;
                    case FishingTestData.SpeciesPerch: perch++; break;
                    default: Assert.Fail("ineligible species rolled: " + r.Catch.ItemIndex); break;
                }
            }
            Assert.That(bluegill / (double)n, Is.EqualTo(0.3125).Within(0.02), "bluegill");
            Assert.That(carp / (double)n, Is.EqualTo(0.3125).Within(0.02), "carp");
            Assert.That(perch / (double)n, Is.EqualTo(0.3125).Within(0.02), "perch");
            Assert.That(junk / (double)n, Is.EqualTo(0.0625).Within(0.02), "junk");
        }

        [Test]
        public void PlaceholderRoller_MinDepthAndMinBaitRating_Gate()
        {
            FishSpeciesData[] s = FishingTestData.Species();
            FishSpeciesData perch = s[FishingTestData.SpeciesPerch];
            FishSpeciesData trout = s[FishingTestData.SpeciesTrout];
            FishSpeciesData catfish = s[FishingTestData.SpeciesCatfish];
            Assert.That(PlaceholderRoller.IsEligible(in perch, 3, 1.2f), Is.True);
            Assert.That(PlaceholderRoller.IsEligible(in perch, 3, 1.19f), Is.False);
            Assert.That(PlaceholderRoller.IsEligible(in perch, 2, 5f), Is.False);
            Assert.That(PlaceholderRoller.IsEligible(in trout, 4, 5f), Is.False);
            Assert.That(PlaceholderRoller.IsEligible(in trout, 5, 2f), Is.True);
            Assert.That(PlaceholderRoller.IsEligible(in catfish, 6, 2.49f), Is.False);
            Assert.That(PlaceholderRoller.IsEligible(in catfish, 6, 2.5f), Is.True);

            Assert.That(PlaceholderRoller.JunkWeight(1), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(PlaceholderRoller.JunkWeight(2), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(PlaceholderRoller.JunkWeight(3), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(PlaceholderRoller.JunkWeight(5), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(PlaceholderRoller.JunkWeight(6), Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(PlaceholderRoller.JunkWeight(12), Is.EqualTo(0.05f).Within(1e-6f));
        }

        [Test]
        public void PlaceholderRoller_BareHookShallow_OnlySteadyBulldogOrJunk()
        {
            FishingDatabase db = FishingTestData.Database();
            RollRequest req = Req(FishingTestData.BaitBareHook, 1.6f);   // eff 1: bluegill + carp (1 each) + junk 0.6 x 1.5 = 0.9
            var rng = new Pcg32(1234);
            const int n = 5000;
            int junk = 0;
            for (int i = 0; i < n; i++)
            {
                FishingRoll r = PlaceholderRoller.Roll(db, in req, ref rng);
                if (r.Catch.IsJunk) { junk++; continue; }
                if (r.Catch.ItemIndex != FishingTestData.SpeciesBluegill && r.Catch.ItemIndex != FishingTestData.SpeciesCarp)
                    Assert.Fail("unexpected species " + r.Catch.ItemIndex);
            }
            Assert.That(junk / (double)n, Is.EqualTo(0.9 / 2.9).Within(0.03), "junk share");
        }

        [Test]
        public void PlaceholderRoller_NothingEligible_ReturnsJunk()
        {
            FishingDatabase db = FishingTestData.Database();
            RollRequest req = Req(FishingTestData.BaitBareHook, 0.5f);
            var rng = new Pcg32(1234);
            for (int i = 0; i < 200; i++)
            {
                FishingRoll r = PlaceholderRoller.Roll(db, in req, ref rng);
                if (!r.Catch.IsJunk) Assert.Fail("expected junk, got species " + r.Catch.ItemIndex);
                if (r.Catch.ItemIndex > 1) Assert.Fail("junk index " + r.Catch.ItemIndex);
                if (r.Catch.WeightG != 0 || r.Catch.ValueCoins != 2 || r.WeightT != 0f || r.SizeClass != 0)
                    Assert.Fail("junk fields wrong: weight " + r.Catch.WeightG + " value " + r.Catch.ValueCoins + " t " + r.WeightT + " size " + r.SizeClass);
            }
        }

        [Test]
        public void PlaceholderRoller_FightSeed_NeverZero()
        {
            FishingDatabase db = FishingTestData.Database();
            RollRequest req = Req(FishingTestData.BaitWorm, 3f);
            var rng = new Pcg32(1234);
            for (int i = 0; i < 10000; i++)
            {
                FishingRoll r = PlaceholderRoller.Roll(db, in req, ref rng);
                if (r.FightSeed == 0) Assert.Fail("FightSeed 0 at roll " + i);
            }
        }

        [Test]
        public void PlaceholderRoller_Overrides_ForceBiteSpeciesJunkTwitchy()
        {
            FishingDatabase db = FishingTestData.Database();

            // ForcedBiteWait: wait is exactly the forced value, no nibbles, and the draw count is unchanged.
            RollRequest plain = Req(FishingTestData.BaitWorm, 3f);
            RollRequest forced = plain; forced.ForcedBiteWait = 0.1f;
            var rngA = new Pcg32(21);
            var rngB = new Pcg32(21);
            for (int i = 0; i < 50; i++)
            {
                FishingRoll a = PlaceholderRoller.Roll(db, in plain, ref rngA);
                FishingRoll b = PlaceholderRoller.Roll(db, in forced, ref rngB);
                Assert.That(b.BiteWait, Is.EqualTo(0.1f));
                Assert.That(b.NibbleCount, Is.EqualTo((byte)0));
                Assert.That(b.Nibble0 + b.Nibble1 + b.Nibble2, Is.EqualTo(0f));
                Assert.That(b.Catch.ItemIndex, Is.EqualTo(a.Catch.ItemIndex));   // same item draw
                Assert.That(b.FightSeed, Is.EqualTo(a.FightSeed));               // same total draws before the seed
            }
            Assert.That(rngB.NextUInt(), Is.EqualTo(rngA.NextUInt()), "ForcedBiteWait must not shift the draw count");

            // ForcedItemIndex forces that species with no eligibility check (catfish needs rating 6 / depth 2.5).
            RollRequest cat = Req(FishingTestData.BaitWorm, 1f); cat.ForcedItemIndex = FishingTestData.SpeciesCatfish;
            var rng = new Pcg32(5);
            for (int i = 0; i < 20; i++)
            {
                FishingRoll r = PlaceholderRoller.Roll(db, in cat, ref rng);
                Assert.That(r.Catch.IsJunk, Is.False);
                Assert.That(r.Catch.ItemIndex, Is.EqualTo((ushort)FishingTestData.SpeciesCatfish));
            }

            // ForceJunk
            RollRequest junk = Req(FishingTestData.BaitWorm, 3f); junk.ForceJunk = true;
            for (int i = 0; i < 20; i++)
            {
                FishingRoll r = PlaceholderRoller.Roll(db, in junk, ref rng);
                Assert.That(r.Catch.IsJunk, Is.True);
                Assert.That(r.Catch.ItemIndex, Is.LessThan((ushort)2));
            }

            // Twitchy: fish HookWindow 1.0; without it the species value (0); junk stays 0 even when twitchy.
            RollRequest tw = cat; tw.Twitchy = true;
            Assert.That(PlaceholderRoller.Roll(db, in tw, ref rng).HookWindow, Is.EqualTo(1.0f));
            Assert.That(PlaceholderRoller.Roll(db, in cat, ref rng).HookWindow, Is.EqualTo(0f));
            RollRequest twJunk = junk; twJunk.Twitchy = true;
            Assert.That(PlaceholderRoller.Roll(db, in twJunk, ref rng).HookWindow, Is.EqualTo(0f));
        }

        [Test]
        public void PlaceholderRoller_SameSeed_SameRoll()
        {
            FishingDatabase db = FishingTestData.Database();
            RollRequest req = Req(FishingTestData.BaitWorm, 3f);
            var a = new Pcg32(7);
            var b = new Pcg32(7);
            for (int i = 0; i < 100; i++)
            {
                FishingRoll ra = PlaceholderRoller.Roll(db, in req, ref a);
                FishingRoll rb = PlaceholderRoller.Roll(db, in req, ref b);
                AssertSameRoll(in ra, in rb);
                Assert.That(ra.Pity, Is.False);
                Assert.That(rb.Pity, Is.False);
            }
            Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }
    }
}
