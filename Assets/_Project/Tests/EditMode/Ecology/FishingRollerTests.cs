// Tests/EditMode/Ecology/FishingRollerTests.cs - T6: the full cast roll on the resolver: item, then bite plan, then fight seed.
using System;
using FishGame.Core;
using NUnit.Framework;
using static FishGame.Tests.EditMode.EcologyCoreTestData;

namespace FishGame.Tests.EditMode
{
    public class FishingRollerTests
    {
        static EcologyDatabase Db() => Database();
        static FishingContext PoolCtx() => Context(OpenWater, MinuteMidday, Season.Summer, WeatherMask.Clear);

        // Three open_water rules: bluegill 1, mirror_minnow 3, sunny_trout 12.
        static EcologyDatabase Spread()
        {
            SpawnRuleData[] rules =
            {
                Rule(Bluegill, 0b010, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 1f),
                Rule(MirrorMinnow, 0b010, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 3f),
                Rule(SunnyTrout, 0b010, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 12f),
            };
            return Database(Lake(rules));
        }

        static void AssertSameRoll(FishingRoll a, FishingRoll b, string msg)
        {
            Assert.AreEqual(a.Catch.IsJunk, b.Catch.IsJunk, msg + " IsJunk");
            Assert.AreEqual(a.Catch.ItemIndex, b.Catch.ItemIndex, msg + " ItemIndex");
            Assert.AreEqual(a.Catch.WeightG, b.Catch.WeightG, msg + " WeightG");
            Assert.AreEqual(a.Catch.LengthMm, b.Catch.LengthMm, msg + " LengthMm");
            Assert.AreEqual(a.Catch.Quality, b.Catch.Quality, msg + " Quality");
            Assert.AreEqual(a.Catch.ValueCoins, b.Catch.ValueCoins, msg + " ValueCoins");
            Assert.AreEqual(a.WeightT, b.WeightT, msg + " WeightT");
            Assert.AreEqual(a.BiteWait, b.BiteWait, msg + " BiteWait");
            Assert.AreEqual(a.NibbleCount, b.NibbleCount, msg + " NibbleCount");
            Assert.AreEqual(a.Nibble0, b.Nibble0, msg + " Nibble0");
            Assert.AreEqual(a.Nibble1, b.Nibble1, msg + " Nibble1");
            Assert.AreEqual(a.Nibble2, b.Nibble2, msg + " Nibble2");
            Assert.AreEqual(a.FightSeed, b.FightSeed, msg + " FightSeed");
            Assert.AreEqual(a.SizeClass, b.SizeClass, msg + " SizeClass");
            Assert.AreEqual(a.HookWindow, b.HookWindow, msg + " HookWindow");
            Assert.AreEqual(a.Pity, b.Pity, msg + " Pity");
        }

        [Test]
        public void FishingRoller_Order_ItemThenBitePlanThenFightSeed()
        {
            EcologyDatabase db = Db();
            var resolver = new FishingResolver(db);
            FishingContext ctx = PoolCtx();
            FishingTuning tuning = db.Fishing.Tuning;
            BaitStats worm = db.Fishing.Baits[FishingTestData.BaitWorm];

            for (ulong seed = 1; seed <= 300; seed++)
            {
                // Replica: resolver, then the bite plan, then the fight seed.
                var r = new Pcg32(seed);
                CatchResult item = resolver.Resolve(in ctx, ref r, null, out float t, out int rule);
                float waitMod = item.IsJunk ? 1f : db.Fishing.Species[item.ItemIndex].BiteWaitMod;
                BitePlan plan = BitePlanner.Plan(in tuning, BitePlanner.WaitMultiplier(worm.BiteSpeedMod, waitMod, 3f, 1f, 1f, false), worm.MaxNibbles, ref r);
                uint fightSeed = r.NextUInt();
                if (fightSeed == 0) fightSeed = 1;

                var rng = new Pcg32(seed);
                FishingRoll roll = FishingRoller.Roll(db, resolver, in ctx, null, in RollOverrides.None, FishingTestData.BaitWorm, ref rng, out int ruleIndex);

                Assert.AreEqual(item.IsJunk, roll.Catch.IsJunk, "seed " + seed);
                Assert.AreEqual(item.ItemIndex, roll.Catch.ItemIndex, "seed " + seed);
                Assert.AreEqual(item.WeightG, roll.Catch.WeightG, "seed " + seed);
                Assert.AreEqual(rule, ruleIndex, "seed " + seed);
                Assert.AreEqual(item.IsJunk ? 0f : t, roll.WeightT, "seed " + seed);
                Assert.AreEqual(plan.BiteWait, roll.BiteWait, "seed " + seed);
                Assert.AreEqual(plan.NibbleCount, roll.NibbleCount, "seed " + seed);
                Assert.AreEqual(plan.Nibble0, roll.Nibble0, "seed " + seed);
                Assert.AreEqual(plan.Nibble1, roll.Nibble1, "seed " + seed);
                Assert.AreEqual(plan.Nibble2, roll.Nibble2, "seed " + seed);
                Assert.AreEqual(fightSeed, roll.FightSeed, "seed " + seed);
                Assert.IsFalse(roll.Pity, "the ledger sets Pity, not the roller");
                Assert.AreEqual(item.IsJunk ? (byte)0 : CatchMath.SizeClass(t), roll.SizeClass, "seed " + seed);
                Assert.AreEqual(r.NextUInt(), rng.NextUInt(), "stream position, seed " + seed);
            }

            // Forced species: one RollFish draw (no junk-share draw), then the plan, then the seed.
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var r = new Pcg32(seed);
                FishSpeciesData trout = db.Fishing.Species[SunnyTrout];
                CatchResult item = CatchMath.RollFish(trout, SunnyTrout, 0f, ref r, out float t);
                BitePlan plan = BitePlanner.Plan(in tuning, BitePlanner.WaitMultiplier(worm.BiteSpeedMod, trout.BiteWaitMod, 3f, 1f, 1f, false), worm.MaxNibbles, ref r);
                uint fightSeed = r.NextUInt();
                if (fightSeed == 0) fightSeed = 1;

                var rng = new Pcg32(seed);
                var ov = new RollOverrides { ForcedItemIndex = SunnyTrout };
                FishingRoll roll = FishingRoller.Roll(db, resolver, in ctx, null, in ov, FishingTestData.BaitWorm, ref rng, out int ruleIndex);
                Assert.AreEqual(SunnyTrout, roll.Catch.ItemIndex);
                Assert.AreEqual(item.WeightG, roll.Catch.WeightG);
                Assert.AreEqual(t, roll.WeightT);
                Assert.AreEqual(-1, ruleIndex);
                Assert.AreEqual(plan.BiteWait, roll.BiteWait);
                Assert.AreEqual(fightSeed, roll.FightSeed);
                Assert.AreEqual(r.NextUInt(), rng.NextUInt(), "forced stream position");
            }
        }

        [Test]
        public void FishingRoller_Overrides_ForceSpeciesJunkBiteTwitchy()
        {
            EcologyDatabase db = Db();
            var resolver = new FishingResolver(db);
            FishingContext everyGateFails = Context(Reeds, MinuteMidday, Season.Spring, WeatherMask.Clear);   // eel: wrong sub-area, season, weather, band, rod
            Assert.IsFalse(resolver.Passes(db.Zones[ZoneLake].Rules[GlimmerEel], in everyGateFails, null), "control: the eel rule fails here");

            var rng = new Pcg32(9);
            var forceEel = new RollOverrides { ForcedItemIndex = GlimmerEel };
            FishingRoll roll = FishingRoller.Roll(db, resolver, in everyGateFails, null, in forceEel, FishingTestData.BaitWorm, ref rng, out int ruleIndex);
            Assert.IsFalse(roll.Catch.IsJunk);
            Assert.AreEqual(GlimmerEel, roll.Catch.ItemIndex);
            Assert.AreEqual(-1, ruleIndex);

            // ForceJunk wins over ForcedItemIndex (M2 precedence, B-D5).
            var junkWins = new RollOverrides { ForceJunk = true, ForcedItemIndex = Bluegill };
            roll = FishingRoller.Roll(db, resolver, in everyGateFails, null, in junkWins, FishingTestData.BaitWorm, ref rng, out ruleIndex);
            Assert.IsTrue(roll.Catch.IsJunk);
            Assert.Less(roll.Catch.ItemIndex, LakeJunk().Length);
            Assert.AreEqual(-1, ruleIndex);

            // Forced bite wait: no nibbles.
            var wait = new RollOverrides { ForcedItemIndex = -1, ForcedBiteWait = 2.5f };
            FishingContext pool = PoolCtx();
            roll = FishingRoller.Roll(db, resolver, in pool, null, in wait, FishingTestData.BaitWorm, ref rng, out _);
            Assert.AreEqual(2.5f, roll.BiteWait);
            Assert.AreEqual(0, roll.NibbleCount);
            Assert.AreEqual(0f, roll.Nibble0);
            Assert.AreEqual(0f, roll.Nibble1);
            Assert.AreEqual(0f, roll.Nibble2);

            // Twitchy: a fish gets HookWindow 1.0; junk stays 0.
            var twitchy = new RollOverrides { ForcedItemIndex = Bluegill, Twitchy = true };
            roll = FishingRoller.Roll(db, resolver, in pool, null, in twitchy, FishingTestData.BaitWorm, ref rng, out _);
            Assert.AreEqual(1.0f, roll.HookWindow);
            var twitchyJunk = new RollOverrides { ForceJunk = true, ForcedItemIndex = -1, Twitchy = true };
            roll = FishingRoller.Roll(db, resolver, in pool, null, in twitchyJunk, FishingTestData.BaitWorm, ref rng, out _);
            Assert.AreEqual(0f, roll.HookWindow);
            var plain = new RollOverrides { ForcedItemIndex = Bluegill };
            roll = FishingRoller.Roll(db, resolver, in pool, null, in plain, FishingTestData.BaitWorm, ref rng, out _);
            Assert.AreEqual(0f, roll.HookWindow, "the species hook window (0 = auto-hook)");
        }

        [Test]
        public void FishingRoller_ForcedIndexOutOfRange_Throws()
        {
            EcologyDatabase db = Db();
            var resolver = new FishingResolver(db);
            FishingContext ctx = PoolCtx();
            var rng = new Pcg32(3);
            var tooHigh = new RollOverrides { ForcedItemIndex = 7 };                 // 7 species: index 7 is out
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FishingRoller.Roll(db, resolver, in ctx, null, in tooHigh, FishingTestData.BaitWorm, ref rng, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FishingRoller.Roll(db, resolver, in ctx, null, in RollOverrides.None, 6, ref rng, out _));    // 6 baits: index 6 is out
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FishingRoller.Roll(db, resolver, in ctx, null, in RollOverrides.None, -1, ref rng, out _));
        }

        [Test]
        public void FishingRoller_Hotspot_ShortensBiteWait()
        {
            EcologyDatabase db = Spread();
            var resolver = new FishingResolver(db);
            FishingContext cold = Context(OpenWater, MinuteMidday, Season.Summer, WeatherMask.Clear);
            cold.Depth = 0f;
            cold.Events.JunkMult = 1e-9f;                                            // no junk either way
            FishingContext hot = cold;
            hot.InHotspot = true;
            float floor = db.Fishing.Tuning.MinBiteWait;

            int scaled = 0;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var a = new Pcg32(seed); var b = new Pcg32(seed);
                FishingRoll rc = FishingRoller.Roll(db, resolver, in cold, null, in RollOverrides.None, FishingTestData.BaitWorm, ref a, out _);
                FishingRoll rh = FishingRoller.Roll(db, resolver, in hot, null, in RollOverrides.None, FishingTestData.BaitWorm, ref b, out _);
                Assert.AreEqual(rc.Catch.ItemIndex, rh.Catch.ItemIndex, "same item, seed " + seed);
                Assert.AreEqual(rc.Catch.WeightG, rh.Catch.WeightG, "same weight, seed " + seed);
                if (rc.BiteWait > floor && rh.BiteWait > floor)
                {
                    Assert.AreEqual(rc.BiteWait * 0.6f, rh.BiteWait, 1e-4f, "seed " + seed);
                    scaled++;
                }
                else
                {
                    Assert.LessOrEqual(rh.BiteWait, rc.BiteWait, "seed " + seed);
                }
            }
            Assert.Greater(scaled, 250, "most seeds are above the floor, so the x0.6 relation was really checked");
        }

        [Test]
        public void FishingRoller_EventBiteMult_ClampedToHalfAndOneAndHalf()
        {
            EcologyDatabase db = Spread();
            var resolver = new FishingResolver(db);
            FishingContext Make(float biteMult)
            {
                FishingContext c = Context(OpenWater, MinuteMidday, Season.Summer, WeatherMask.Clear);
                c.Events.JunkMult = 1e-9f;
                c.Events.BiteTimeMult = biteMult;
                return c;
            }
            FishingContext lowRaw = Make(0.1f), low = Make(0.5f), highRaw = Make(3.0f), high = Make(1.5f), one = Make(1.0f);

            int differs = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                FishingRoll Roll(FishingContext c)
                {
                    var rng = new Pcg32(seed);
                    return FishingRoller.Roll(db, resolver, in c, null, in RollOverrides.None, FishingTestData.BaitWorm, ref rng, out _);
                }
                AssertSameRoll(Roll(lowRaw), Roll(low), "0.1 clamps to 0.5, seed " + seed);
                AssertSameRoll(Roll(highRaw), Roll(high), "3.0 clamps to 1.5, seed " + seed);
                if (Roll(one).BiteWait != Roll(low).BiteWait) differs++;
            }
            Assert.Greater(differs, 100, "1.0 and 0.5 give different waits on most seeds");
        }

        [Test]
        public void FishingRoller_FightSeed_NeverZero()
        {
            EcologyDatabase db = Db();
            var resolver = new FishingResolver(db);
            FishingContext ctx = PoolCtx();
            var rng = new Pcg32(99);
            for (int i = 0; i < 10000; i++)
            {
                FishingRoll roll = FishingRoller.Roll(db, resolver, in ctx, null, in RollOverrides.None, FishingTestData.BaitWorm, ref rng, out _);
                Assert.AreNotEqual(0u, roll.FightSeed, "roll " + i);
            }
        }
    }
}
