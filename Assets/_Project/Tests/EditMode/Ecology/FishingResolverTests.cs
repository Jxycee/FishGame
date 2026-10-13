// Tests/EditMode/Ecology/FishingResolverTests.cs - T6: the spec 12.4 gate and algorithm, the spec 12.9 resolver checks with their
// tolerances, and the hand-replayed draw order. Statistical rows use fixed Pcg32 seeds, so they are deterministic.
using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;
using static FishGame.Tests.EditMode.EcologyCoreTestData;

namespace FishGame.Tests.EditMode
{
    public class FishingResolverTests
    {
        static EcologyDatabase Db() => Database();
        static FishingResolver Res() => new FishingResolver(Db());

        // OpenWater, midday, summer, clear: pool bluegill 10, grumpy_carp 9, mirror_minnow 10, sunny_trout 12 (total 41), junk 0.12.
        static FishingContext PoolCtx() => Context(OpenWater, MinuteMidday, Season.Summer, WeatherMask.Clear);
        // DeepWater, night, autumn, rain, rod 0: special gilded_carp (0.02), pool grumpy_carp only, junk 0.10.
        static FishingContext DeepCtx() => Context(DeepWater, MinuteNight, Season.Autumn, WeatherMask.Rain);
        static FishingContext ReedsCtx() => Context(Reeds, MinuteMidday, Season.Summer, WeatherMask.Clear);

        static EcologyDatabase OneRule(SpawnRuleData r) => Database(Lake(new[] { r }));

        // Three open_water rules: bluegill 1, mirror_minnow 3, sunny_trout 12 (shares 1/16, 3/16, 12/16).
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

        static void AssertSameCatch(CatchResult a, CatchResult b, string msg = "")
        {
            Assert.AreEqual(a.IsJunk, b.IsJunk, msg + " IsJunk");
            Assert.AreEqual(a.ItemIndex, b.ItemIndex, msg + " ItemIndex");
            Assert.AreEqual(a.WeightG, b.WeightG, msg + " WeightG");
            Assert.AreEqual(a.LengthMm, b.LengthMm, msg + " LengthMm");
            Assert.AreEqual(a.Quality, b.Quality, msg + " Quality");
            Assert.AreEqual(a.ValueCoins, b.ValueCoins, msg + " ValueCoins");
        }

        // ---- determinism ----

        [Test]
        public void FishingResolver_SameSeedAndContext_IdenticalThousandCastSequence()
        {
            FishingResolver res = Res();
            FishingContext ctx = PoolCtx();
            var a = new Pcg32(77); var b = new Pcg32(77); var c = new Pcg32(78);
            bool differs = false;
            for (int i = 0; i < 1000; i++)
            {
                CatchResult ra = res.Resolve(in ctx, ref a, null, out float ta, out int ia);
                CatchResult rb = res.Resolve(in ctx, ref b, null, out float tb, out int ib);
                CatchResult rc = res.Resolve(in ctx, ref c, null, out float tc, out int ic);
                AssertSameCatch(ra, rb, "cast " + i);
                Assert.AreEqual(ta, tb, "t " + i);
                Assert.AreEqual(ia, ib, "rule " + i);
                if (ra.IsJunk != rc.IsJunk || ra.ItemIndex != rc.ItemIndex || ra.WeightG != rc.WeightG || ta != tc || ia != ic) differs = true;
            }
            Assert.IsTrue(differs, "seed 78 must differ from seed 77 somewhere");
        }

        // ---- the gate ----

        [TestCase("subArea")]
        [TestCase("season")]
        [TestCase("weather")]
        [TestCase("band")]
        [TestCase("rodTier")]
        [TestCase("baitRating")]
        [TestCase("depth")]
        [TestCase("players")]
        [TestCase("catchLimit")]
        public void FishingResolver_EachGate_ExcludesRule(string gate)
        {
            SpawnRuleData rule = Rule(Bluegill, 0b010, SeasonMask.Summer, WeatherMask.Clear, TimeBandMask.Midday, 10f, rod: 1);
            rule.MinBaitRating = 3; rule.MinDepth = 2f; rule.MinPlayersInSession = 2; rule.CatchLimitPerRun = 1;
            FishingContext ctx = Context(OpenWater, MinuteMidday, Season.Summer, WeatherMask.Clear, rodTier: 1);
            ctx.Bait = BaitMods.Neutral(3); ctx.Depth = 2.0f; ctx.PlayersInSession = 2;

            FishingResolver res = new FishingResolver(OneRule(rule));
            var ledger = new CatchLedger();
            var list = new List<int>();
            Assert.IsTrue(res.Passes(in rule, in ctx, ledger), "positive control: the base rule passes");
            Assert.AreEqual(1, res.CollectEligible(in ctx, ledger, false, list), "positive control: CollectEligible finds it");

            switch (gate)
            {
                case "subArea": ctx.SubAreaIndex = Reeds; break;
                case "season": ctx.Season = Season.Winter; break;
                case "weather": ctx.Weather = WeatherMask.Rain; break;
                case "band": ctx.Minute = MinuteNight; break;
                case "rodTier": ctx.RodTier = 0; break;
                case "baitRating": ctx.Bait = BaitMods.Neutral(2); break;
                case "depth": ctx.Depth = 1.99f; break;
                case "players": ctx.PlayersInSession = 1; break;
                case "catchLimit": ledger.Reserve(Bluegill); break;
                default: Assert.Fail("unknown gate " + gate); break;
            }
            Assert.IsFalse(res.Passes(in rule, in ctx, ledger), gate + ": Passes");
            Assert.AreEqual(0, res.CollectEligible(in ctx, ledger, false, list), gate + ": CollectEligible");
            Assert.AreEqual(0, list.Count);
            if (gate == "catchLimit") Assert.IsTrue(res.Passes(in rule, in ctx, null), "a null ledger skips the catch limit");
        }

        [Test]
        public void FishingResolver_WeatherGate_MatchesAnySharedBit()
        {
            FishingResolver res = Res();
            SpawnRuleData rainFog = Rule(Bluegill, 0, SeasonMask.All, WeatherMask.Rain | WeatherMask.Fog, TimeBandMask.All, 1f);
            SpawnRuleData clear = Rule(Bluegill, 0, SeasonMask.All, WeatherMask.Clear, TimeBandMask.All, 1f);
            SpawnRuleData all = Rule(Bluegill, 0, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 1f);
            FishingContext ctx = PoolCtx();

            ctx.Weather = WeatherMask.Rain; Assert.IsTrue(res.Passes(in rainFog, in ctx, null), "rain on Rain|Fog");
            ctx.Weather = WeatherMask.Fog; Assert.IsTrue(res.Passes(in rainFog, in ctx, null), "fog on Rain|Fog");
            ctx.Weather = WeatherMask.Clear; Assert.IsFalse(res.Passes(in rainFog, in ctx, null), "clear on Rain|Fog");
            ctx.Weather = WeatherMask.Rain | WeatherMask.Storm; Assert.IsTrue(res.Passes(in rainFog, in ctx, null), "rain|storm on Rain|Fog");
            ctx.Weather = WeatherMask.Clear; Assert.IsTrue(res.Passes(in clear, in ctx, null), "clear on Clear");
            ctx.Weather = WeatherMask.Clear | WeatherMask.Heatwave; Assert.IsTrue(res.Passes(in clear, in ctx, null), "clear|heatwave on Clear");
            ctx.Weather = WeatherMask.Rain; Assert.IsFalse(res.Passes(in clear, in ctx, null), "rain on Clear");
            ctx.Weather = WeatherMask.Fog; Assert.IsTrue(res.Passes(in all, in ctx, null), "fog on All");
        }

        [Test]
        public void FishingResolver_AnySubAreaAndInfiniteDepth_PassGates()
        {
            SpawnRuleData rule = Rule(GlimmerEel, 0b100, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 1f);
            rule.MinDepth = 5f;
            FishingResolver res = Res();
            FishingContext ctx = Context(EcologyConstants.AnySubArea, MinuteMidday, Season.Summer, WeatherMask.Clear);

            ctx.Depth = float.PositiveInfinity;
            Assert.IsTrue(res.Passes(in rule, in ctx, null), "AnySubArea + infinite depth");
            ctx.SubAreaIndex = Reeds; ctx.Depth = float.PositiveInfinity;
            Assert.IsFalse(res.Passes(in rule, in ctx, null), "sub-area 0 is outside the 0b100 mask");
            ctx.SubAreaIndex = DeepWater; ctx.Depth = 2f;
            Assert.IsFalse(res.Passes(in rule, in ctx, null), "deep water but too shallow");
            ctx.SubAreaIndex = EcologyConstants.AnySubArea; ctx.Depth = float.NaN;
            Assert.IsFalse(res.Passes(in rule, in ctx, null), "NaN depth fails (B-D7)");
            ctx.SubAreaIndex = 9; ctx.Depth = float.PositiveInfinity;
            Assert.IsFalse(res.Passes(in rule, in ctx, null), "sub-area 9 is outside the mask range (B-D7)");

            FishingContext any = PoolCtx();
            any.SubAreaIndex = EcologyConstants.AnySubArea;
            var rng = new Pcg32(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => res.Resolve(in any, ref rng, null), "Resolve needs a real sub-area (B-D2)");
        }

        // ---- junk share ----

        [Test]
        public void FishingResolver_JunkShare_WithinPoint015OverTenThousand()
        {
            FishingResolver res = Res();
            FishingContext ctx = ReedsCtx();
            Assert.AreEqual(0.20f, res.JunkShare(in ctx), 0f);
            Assert.AreEqual(0.20, JunkFraction(res, ctx, 10000, 11), 0.015);

            ctx.Bait = Baits()[FishingTestData.BaitBareHook].Mods(1);                // JunkMult 1.5
            Assert.AreEqual(0.30f, res.JunkShare(in ctx), 1e-6f);
            Assert.AreEqual(0.30, JunkFraction(res, ctx, 10000, 12), 0.015);
        }

        static double JunkFraction(FishingResolver res, FishingContext ctx, int casts, ulong seed)
        {
            var rng = new Pcg32(seed);
            int junk = 0;
            for (int i = 0; i < casts; i++) if (res.Resolve(in ctx, ref rng, null).IsJunk) junk++;
            return junk / (double)casts;
        }

        [Test]
        public void FishingResolver_JunkShare_HalvedInHotspot()
        {
            FishingResolver res = Res();
            FishingContext ctx = ReedsCtx();
            ctx.InHotspot = true;
            Assert.AreEqual(0.10f, res.JunkShare(in ctx), 1e-6f);
            Assert.AreEqual(0.10, JunkFraction(res, ctx, 10000, 13), 0.015);
        }

        [Test]
        public void FishingResolver_JunkShare_ClampedAtPointSix()
        {
            FishingResolver res = Res();
            FishingContext ctx = ReedsCtx();
            ctx.Bait = Baits()[FishingTestData.BaitBareHook].Mods(1);                // JunkMult 1.5
            ctx.Events.JunkMult = 3f;                                                // 0.2 x 1.5 x 3 = 0.9 -> capped
            Assert.AreEqual(0.6f, res.JunkShare(in ctx), 1e-6f);

            ctx.Events.JunkMult = 1e-9f;
            float tiny = res.JunkShare(in ctx);
            Assert.Greater(tiny, 0f);
            Assert.Less(tiny, 1e-8f);
        }

        [Test]
        public void FishingResolver_EmptyPool_AlwaysJunk()
        {
            FishingResolver res = Res();
            FishingContext ctx = Context(DeepWater, MinuteMidday, Season.Winter, WeatherMask.Clear);
            var limited = new CatchLedger();
            limited.Reserve(GildedCarp);                                             // limit 1 reached: the special is blocked
            var rng = new Pcg32(5);
            for (int i = 0; i < 2000; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, limited, out float t, out int rule);
                Assert.IsTrue(r.IsJunk, "cast " + i);
                Assert.AreEqual(-1, rule);
                Assert.AreEqual(0f, t);
            }
            var rng2 = new Pcg32(5);
            int gilded = 0;
            for (int i = 0; i < 20000; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng2, null);
                if (!r.IsJunk && r.ItemIndex == GildedCarp) gilded++;
            }
            Assert.Greater(gilded, 0, "without the ledger the special rolls");
        }

        // ---- pool shares ----

        [Test]
        public void FishingResolver_SpeciesShares_WithinFifteenPercentRelativeOfWeights()
        {
            FishingResolver res = new FishingResolver(Spread());
            FishingContext ctx = Context(OpenWater, MinuteMidday, Season.Summer, WeatherMask.Clear);
            ctx.Events.JunkMult = 1e-9f;                                             // no junk
            var rng = new Pcg32(21);
            const int n = 20000;
            int blue = 0, minnow = 0, trout = 0;
            for (int i = 0; i < n; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, null);
                Assert.IsFalse(r.IsJunk);
                if (r.ItemIndex == Bluegill) blue++; else if (r.ItemIndex == MirrorMinnow) minnow++; else if (r.ItemIndex == SunnyTrout) trout++;
            }
            Assert.AreEqual(n, blue + minnow + trout);
            Assert.AreEqual(1.0 / 16, blue / (double)n, 1.0 / 16 * 0.15, "bluegill");
            Assert.AreEqual(3.0 / 16, minnow / (double)n, 3.0 / 16 * 0.15, "mirror_minnow");
            Assert.AreEqual(12.0 / 16, trout / (double)n, 12.0 / 16 * 0.15, "sunny_trout");
        }

        [Test]
        public void FishingResolver_ForcedSpecial_WithinTwentyPercentOfExpected()
        {
            FishingResolver res = Res();
            FishingContext ctx = Context(DeepWater, MinuteMidday, Season.Summer, WeatherMask.Clear);   // pool grumpy_carp, special gilded_carp
            ctx.Bait = Baits()[FishingTestData.BaitBareHook].Mods(1);
            ctx.Bait.JunkMult = 6f;                                                  // 0.10 x 6 caps the junk share at 0.6
            Assert.AreEqual(0.6f, res.JunkShare(in ctx), 1e-6f);
            var rng = new Pcg32(31);
            int specials = 0;
            for (int i = 0; i < 50000; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, null);
                if (!r.IsJunk && r.ItemIndex == GildedCarp) specials++;
            }
            // 0.02 x (1 - 0.6) x 50,000 = 400. A resolver that rolls specials before the junk draw would give about 1,000.
            Assert.GreaterOrEqual(specials, 320);
            Assert.LessOrEqual(specials, 480);
        }

        static int CountSpecials(FishingResolver res, FishingContext ctx, int casts, ulong seed)
        {
            var rng = new Pcg32(seed);
            int specials = 0;
            for (int i = 0; i < casts; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, null);
                if (!r.IsJunk && r.ItemIndex == GildedCarp) specials++;
            }
            return specials;
        }

        [Test]
        public void FishingResolver_SpecialChance_ClampedAtHalfAndDoubledInHotspot()
        {
            FishingResolver res = Res();
            FishingContext ctx = DeepCtx();

            ctx.Bait.SpecialMult = 50f;                                              // 0.02 x 50 = 1 -> capped at 0.5
            Assert.AreEqual(0.45, CountSpecials(res, ctx, 20000, 41) / 20000.0, 0.015, "cap 0.5 x (1 - 0.10)");

            FishingContext hot = DeepCtx();
            hot.InHotspot = true;                                                    // chance x2 = 0.04, junk 0.05
            Assert.AreEqual(0.038, CountSpecials(res, hot, 50000, 42) / 50000.0, 0.038 * 0.15, "hotspot");

            Assert.AreEqual(0.018, CountSpecials(res, DeepCtx(), 50000, 43) / 50000.0, 0.018 * 0.15, "no hotspot");
        }

        [Test]
        public void FishingResolver_AffinityTag_MultipliesWeight()
        {
            FishingResolver res = Res();
            FishingContext ctx = PoolCtx();
            ctx.Bait = new BaitMods { Rating = 1, AffinityTags = FishTags.Schooling, AffinityMult = 3f, JunkMult = 1f, SpecialMult = 1f };
            // weights: bluegill 30, grumpy_carp 9, mirror_minnow 30, sunny_trout 12 (81)
            FishShares(res, ctx, 20000, 51, out double blue, out double trout);
            Assert.AreEqual(30.0 / 81, blue, 30.0 / 81 * 0.10, "bluegill with affinity");
            Assert.AreEqual(12.0 / 81, trout, 12.0 / 81 * 0.10, "sunny_trout with affinity");

            FishingContext neutral = PoolCtx();
            FishShares(res, neutral, 20000, 52, out double blueN, out _);
            Assert.AreEqual(10.0 / 41, blueN, 10.0 / 41 * 0.10, "bluegill neutral");
        }

        // Share of bluegill and sunny_trout among the FISH results (junk excluded).
        static void FishShares(FishingResolver res, FishingContext ctx, int casts, ulong seed, out double blue, out double trout)
        {
            var rng = new Pcg32(seed);
            int fish = 0, b = 0, t = 0;
            for (int i = 0; i < casts; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, null);
                if (r.IsJunk) continue;
                fish++;
                if (r.ItemIndex == Bluegill) b++; else if (r.ItemIndex == SunnyTrout) t++;
            }
            blue = b / (double)fish; trout = t / (double)fish;
        }

        // ---- weights, quality, value ----

        [Test]
        public void FishingResolver_WeightDraws_InRangeMeanPoint3125TrophyRate4p7()
        {
            FishingResolver res = Res();
            FishingContext ctx = PoolCtx();
            FishSpeciesData[] species = Species();
            var rng = new Pcg32(61);
            double sumT = 0; int fish = 0, trophy = 0;
            for (int i = 0; i < 50000; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, null, out float t, out _);
                if (r.IsJunk) { Assert.AreEqual(0f, t, "junk weightT"); continue; }
                FishSpeciesData s = species[r.ItemIndex];
                Assert.GreaterOrEqual(r.WeightG, s.WeightMinG);
                Assert.LessOrEqual(r.WeightG, s.WeightMaxG);
                sumT += t; fish++;
                if (r.Quality == CatchQuality.Trophy) trophy++;
            }
            Assert.AreEqual(0.3125, sumT / fish, 0.01, "mean t");
            Assert.AreEqual(0.047, trophy / (double)fish, 0.007, "trophy share");
        }

        [Test]
        public void FishingResolver_FishValue_AtLeastOneAndMinnowAtZeroIsThree()
        {
            Assert.AreEqual(3, CatchMath.Value(5, 0f, CatchQuality.Normal));         // mirror_minnow base 5
            FishingResolver res = Res();
            FishingContext ctx = PoolCtx();
            var rng = new Pcg32(71);
            for (int i = 0; i < 10000; i++)
            {
                CatchResult r = res.Resolve(in ctx, ref rng, null);
                if (r.IsJunk) Assert.That(r.ValueCoins, Is.InRange(0, 2));
                else Assert.GreaterOrEqual(r.ValueCoins, 1);
            }
        }

        // ---- validation ----

        [Test]
        public void FishingResolver_InvalidEventOrSkillMods_Throws()
        {
            FishingResolver res = Res();
            FishingContext badEvents = PoolCtx();
            badEvents.Events = default;
            FishingContext badSkills = PoolCtx();
            badSkills.Skills = default;

            var rng = new Pcg32(5);
            var fresh = new Pcg32(5);
            Assert.Throws<ArgumentException>(() => res.Resolve(in badEvents, ref rng, null));
            Assert.Throws<ArgumentException>(() => res.JunkShare(in badEvents));
            Assert.Throws<ArgumentException>(() => res.Resolve(in badSkills, ref rng, null));
            Assert.AreEqual(fresh.NextUInt(), rng.NextUInt(), "no draw happened before the throw");
        }

        // ---- the draw order, replayed by hand ----

        static int Walk(float target, params float[] weights)
        {
            float acc = 0f; int pick = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (!(weights[i] > 0f)) continue;
                pick = i; acc += weights[i];
                if (target < acc) break;
            }
            return pick;
        }

        [Test]
        public void FishingResolver_DrawOrder_HandReplayed()
        {
            FishingResolver res = Res();
            FishSpeciesData[] species = Species();

            // Junk path: junk share draw succeeds, then one junk item draw over the 4 unit weights.
            FishingContext reeds = ReedsCtx();
            float junkShare = res.JunkShare(in reeds);
            ulong seed = FindSeed(r => r.NextFloat() < junkShare);
            var rng = new Pcg32(seed); var replica = new Pcg32(seed);
            CatchResult got = res.Resolve(in reeds, ref rng, null, out float gotT, out int gotRule);
            replica.NextFloat();
            int junkPick = Walk(replica.NextFloat() * 4f, 1f, 1f, 1f, 1f);
            Assert.IsTrue(got.IsJunk);
            Assert.AreEqual(LakeJunk()[junkPick].JunkIndex, got.ItemIndex);
            Assert.AreEqual(-1, gotRule);
            Assert.AreEqual(0f, gotT);
            Assert.AreEqual(replica.NextUInt(), rng.NextUInt(), "junk path stream position");

            // Special path: junk draw fails (>= 0.10), special draw succeeds (< 0.02): gilded_carp.
            FishingContext deep = DeepCtx();
            seed = FindSeed(r => { float u1 = r.NextFloat(), u2 = r.NextFloat(); return u1 >= 0.10f && u2 < 0.02f; });
            rng = new Pcg32(seed); replica = new Pcg32(seed);
            got = res.Resolve(in deep, ref rng, null, out gotT, out gotRule);
            replica.NextFloat(); replica.NextFloat();
            CatchResult want = CatchMath.RollFish(species[GildedCarp], GildedCarp, 0f, ref replica, out float wantT);
            AssertSameCatch(want, got, "special");
            Assert.AreEqual(wantT, gotT);
            Assert.AreEqual(1, gotRule);
            Assert.AreEqual(replica.NextUInt(), rng.NextUInt(), "special path stream position");

            // Special-fail path: the special draw fails, one pool draw picks the only pool rule (grumpy_carp, rule 3), then RollFish.
            seed = FindSeed(r => { float u1 = r.NextFloat(), u2 = r.NextFloat(); return u1 >= 0.10f && u2 >= 0.02f; });
            rng = new Pcg32(seed); replica = new Pcg32(seed);
            got = res.Resolve(in deep, ref rng, null, out gotT, out gotRule);
            replica.NextFloat(); replica.NextFloat();
            int poolPick = Walk(replica.NextFloat() * 9f, 9f);
            Assert.AreEqual(0, poolPick);
            want = CatchMath.RollFish(species[GrumpyCarp], GrumpyCarp, 0f, ref replica, out wantT);
            AssertSameCatch(want, got, "special-fail");
            Assert.AreEqual(wantT, gotT);
            Assert.AreEqual(3, gotRule);
            Assert.AreEqual(replica.NextUInt(), rng.NextUInt(), "special-fail path stream position");

            // Pool path: no eligible special, so no special draw. Junk draw fails (>= 0.12), pool walk over 10/9/10/12, then RollFish.
            FishingContext pool = PoolCtx();
            // The pool draw must land past the first two rules (u2 x 41 >= 19), so a dropped "x total" cannot hide behind rule 0.
            seed = FindSeed(r => r.NextFloat() >= 0.12f && r.NextFloat() * 41f >= 19f);
            rng = new Pcg32(seed); replica = new Pcg32(seed);
            got = res.Resolve(in pool, ref rng, null, out gotT, out gotRule);
            replica.NextFloat();
            int p = Walk(replica.NextFloat() * 41f, 10f, 9f, 10f, 12f);
            ushort[] poolSpecies = { Bluegill, GrumpyCarp, MirrorMinnow, SunnyTrout };
            int[] poolRules = { 0, 3, 4, 6 };
            want = CatchMath.RollFish(species[poolSpecies[p]], poolSpecies[p], 0f, ref replica, out wantT);
            AssertSameCatch(want, got, "pool");
            Assert.AreEqual(wantT, gotT);
            Assert.AreEqual(poolRules[p], gotRule);
            Assert.AreEqual(replica.NextUInt(), rng.NextUInt(), "pool path stream position");
        }

        static ulong FindSeed(Func<Pcg32, bool> accept)
        {
            for (ulong s = 1; s <= 10000; s++)
            {
                var r = new Pcg32(s);
                if (accept(r)) return s;
            }
            Assert.Fail("no seed in 1..10000 matches the wanted path");
            return 0;
        }
    }
}
