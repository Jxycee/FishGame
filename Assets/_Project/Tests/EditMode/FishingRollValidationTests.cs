using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class FishingRollValidationTests
    {
        static RollRequest Req(int bait, float depth) =>
            new RollRequest { BaitIndex = bait, LoadedCount = 1, Depth = depth, Luck = 0f,
                              ForcedBiteWait = 0f, ForcedItemIndex = -1, ForceJunk = false, Twitchy = false };

        [Test]
        public void FishingRollValidation_SaneRoll_Passes()
        {
            FishingDatabase db = FishingTestData.Database();
            RollRequest deep = Req(FishingTestData.BaitWorm, 3f);
            RollRequest shallow = Req(FishingTestData.BaitBareHook, 0.5f);   // all junk
            var rng = new Pcg32(1234);
            int junk = 0;
            for (int i = 0; i < 1000; i++)
            {
                RollRequest req = (i % 4 == 3) ? shallow : deep;
                FishingRoll r = PlaceholderRoller.Roll(db, in req, ref rng);
                if (r.Catch.IsJunk) junk++;
                if (!FishingRollValidation.IsSane(in r, db)) Assert.Fail("roll " + i + " rejected");
            }
            Assert.That(junk, Is.GreaterThan(0));
        }

        [Test]
        public void FishingRollValidation_OutOfRangeFields_Rejected()
        {
            FishingDatabase db = FishingTestData.Database();
            var rng = new Pcg32(3);

            RollRequest fishReq = Req(FishingTestData.BaitWorm, 3f); fishReq.ForcedItemIndex = FishingTestData.SpeciesCarp;
            FishingRoll fish = PlaceholderRoller.Roll(db, in fishReq, ref rng);
            RollRequest junkReq = Req(FishingTestData.BaitWorm, 3f); junkReq.ForceJunk = true;
            FishingRoll junk = PlaceholderRoller.Roll(db, in junkReq, ref rng);
            Assert.That(fish.Catch.IsJunk, Is.False);
            Assert.That(junk.Catch.IsJunk, Is.True);
            Assert.That(FishingRollValidation.IsSane(in fish, db), Is.True, "base fish roll");
            Assert.That(FishingRollValidation.IsSane(in junk, db), Is.True, "base junk roll");

            FishSpeciesData carp = db.Species[FishingTestData.SpeciesCarp];

            FishingRoll m = fish; m.Catch.ItemIndex = 5;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "species index 5");
            m = junk; m.Catch.ItemIndex = 2;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "junk index 2");
            m = fish; m.Catch.WeightG = carp.WeightMaxG + 1;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "weight max + 1");
            m = fish; m.Catch.WeightG = carp.WeightMinG - 1;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "weight min - 1");
            m = fish; m.Catch.Quality = (CatchQuality)3;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "quality 3");
            m = fish; m.FightSeed = 0;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "FightSeed 0");
            m = fish; m.BiteWait = float.NaN;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "BiteWait NaN");
            m = fish; m.BiteWait = float.PositiveInfinity;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "BiteWait +inf");
            m = fish; m.BiteWait = -1f;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "BiteWait -1");
            m = fish; m.NibbleCount = 4;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "NibbleCount 4");
            m = fish; m.SizeClass = 3;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "SizeClass 3");
            m = fish; m.HookWindow = float.NaN;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "HookWindow NaN");
            m = fish; m.HookWindow = -0.1f;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "HookWindow negative");
            m = junk; m.Catch.WeightG = 1;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "junk with weight 1");

            // WeightT must be finite and inside [0, 1]; the bounds themselves are fine (junk rolls carry 0).
            // Catches: a missing WeightT check, a check written as (< 0 || > 1) that lets NaN through, or an off-by-one on the bounds.
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f, 1.1f, -0.0001f, 1.0001f })
            {
                m = fish; m.WeightT = bad;
                Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "WeightT " + bad);
            }
            m = fish; m.WeightT = 0f;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.True, "WeightT 0 is allowed");
            m = fish; m.WeightT = 1f;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.True, "WeightT 1 is allowed");

            // Nibble times must be finite (all three slots, used or not).
            // Catches: validating only Nibble0, or only the used slots.
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                m = fish; m.Nibble0 = bad;
                Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "Nibble0 " + bad);
                m = fish; m.Nibble1 = bad;
                Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "Nibble1 " + bad);
                m = fish; m.Nibble2 = bad;
                Assert.That(FishingRollValidation.IsSane(in m, db), Is.False, "Nibble2 " + bad);
            }

            // Pity is ignored by the check.
            m = fish; m.Pity = true;
            Assert.That(FishingRollValidation.IsSane(in m, db), Is.True, "Pity does not affect sanity");
        }
    }
}
