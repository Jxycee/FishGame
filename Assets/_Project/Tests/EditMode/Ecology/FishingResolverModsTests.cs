// Tests/EditMode/Ecology/FishingResolverModsTests.cs - T13 extras asked by the T5-T8 review: the event and skill multipliers the first
// real context builder (FishingHostLedger) passes as Neutral are pinned by shifted shares, and the B-D4/B-D6 sanitisers are pinned
// (NaN luck = 0, |luck| > 1 clamped; +Infinity depth = 6 m and NaN depth = 0 m for the bite plan). Uses EcologyCoreTestData's species
// (tags and rarities) with gate-free rules from EcologyTestData.Rule and a junk share of 0, so every draw lands on a rule.
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class FishingResolverModsTests
    {
        const int N = 20000;

        static EcologyDatabase Db(params SpawnRuleData[] rules) =>
            EcologyCoreTestData.Database(EcologyTestData.Zone(EcologyTestData.OneBand(0f), rules));

        static FishingContext Ctx() =>
            EcologyCoreTestData.Context(0, EcologyCoreTestData.MinuteMidday, Season.Summer, WeatherMask.Clear);

        // Item counts per species over N resolutions; the same N streams for every call, so two contexts compare draw for draw.
        static int[] Counts(EcologyDatabase db, in FishingContext ctx)
        {
            var resolver = new FishingResolver(db);
            var counts = new int[db.Fishing.Species.Count];
            for (int i = 0; i < N; i++)
            {
                Pcg32 rng = Rng.Stream(77, "mods", (ulong)i);
                CatchResult c = resolver.Resolve(in ctx, ref rng, null);
                Assert.That(c.IsJunk, Is.False, "junk share is 0 here");
                counts[c.ItemIndex]++;
            }
            return counts;
        }

        static float Share(int[] counts, ushort species) => counts[species] / (float)N;

        // Catches: Events.RareChanceMult dropped from the special chance (FishingResolver step 2).
        [Test]
        public void FishingResolver_EventsRareChanceMult_ScalesSpecialShare()
        {
            EcologyDatabase db = Db(EcologyTestData.Rule(EcologyCoreTestData.Bluegill),
                                    EcologyTestData.Rule(EcologyCoreTestData.GildedCarp, 0f, -1, true, 0.05f));
            FishingContext ctx = Ctx();
            float neutral = Share(Counts(db, in ctx), EcologyCoreTestData.GildedCarp);
            EventMods events = EventMods.Neutral;
            events.RareChanceMult = 3f;
            ctx.Events = events;
            float boosted = Share(Counts(db, in ctx), EcologyCoreTestData.GildedCarp);
            TestContext.WriteLine("FG MODS special share neutral=" + neutral + " rareChance x3=" + boosted);
            Assert.That(neutral, Is.EqualTo(0.05f).Within(0.01f), "special chance 0.05");
            Assert.That(boosted, Is.EqualTo(0.15f).Within(0.015f), "RareChanceMult 3 triples it");
        }

        // Catches: Events.WeightFor(tags) dropped from the pool weight (FishingResolver PoolWeight).
        [Test]
        public void FishingResolver_EventsWeightFor_ShiftsTaggedShare()
        {
            EcologyDatabase db = Db(EcologyTestData.Rule(EcologyCoreTestData.Bluegill),       // Schooling
                                    EcologyTestData.Rule(EcologyCoreTestData.GrumpyCarp));    // Bottom
            FishingContext ctx = Ctx();
            float neutral = Share(Counts(db, in ctx), EcologyCoreTestData.GrumpyCarp);
            EventMods bottom = EventMods.Neutral;
            bottom.BottomMult = 3f;
            ctx.Events = bottom;
            float bottomUp = Share(Counts(db, in ctx), EcologyCoreTestData.GrumpyCarp);
            EventMods schooling = EventMods.Neutral;
            schooling.SchoolingMult = 3f;
            ctx.Events = schooling;
            float schoolingUp = Share(Counts(db, in ctx), EcologyCoreTestData.GrumpyCarp);
            TestContext.WriteLine("FG MODS bottom share neutral=" + neutral + " bottom x3=" + bottomUp + " schooling x3=" + schoolingUp);
            Assert.That(neutral, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(bottomUp, Is.EqualTo(0.75f).Within(0.02f), "the Bottom carp weighs 3 against 1");
            Assert.That(schoolingUp, Is.EqualTo(0.25f).Within(0.02f), "the Schooling bluegill weighs 3 against 1");
        }

        // Catches: Skills.RarityMult dropped from the pool weight, or applied to Common.
        [Test]
        public void FishingResolver_SkillsRarityMult_ShiftsUncommonAndRareShare()
        {
            EcologyDatabase db = Db(EcologyTestData.Rule(EcologyCoreTestData.Bluegill),      // Common
                                    EcologyTestData.Rule(EcologyCoreTestData.SunnyTrout),    // Uncommon
                                    EcologyTestData.Rule(EcologyCoreTestData.GlimmerEel));   // Rare
            FishingContext ctx = Ctx();
            int[] neutral = Counts(db, in ctx);
            ctx.Skills = new SkillMods { UncommonRareMult = 3f };
            int[] boosted = Counts(db, in ctx);
            TestContext.WriteLine("FG MODS rarity neutral common/uncommon/rare=" + Share(neutral, EcologyCoreTestData.Bluegill) + "/"
                                  + Share(neutral, EcologyCoreTestData.SunnyTrout) + "/" + Share(neutral, EcologyCoreTestData.GlimmerEel)
                                  + " x3=" + Share(boosted, EcologyCoreTestData.Bluegill) + "/" + Share(boosted, EcologyCoreTestData.SunnyTrout)
                                  + "/" + Share(boosted, EcologyCoreTestData.GlimmerEel));
            Assert.That(Share(neutral, EcologyCoreTestData.Bluegill), Is.EqualTo(1f / 3f).Within(0.02f));
            Assert.That(Share(boosted, EcologyCoreTestData.Bluegill), Is.EqualTo(1f / 7f).Within(0.02f), "Common is never boosted");
            Assert.That(Share(boosted, EcologyCoreTestData.SunnyTrout), Is.EqualTo(3f / 7f).Within(0.02f), "Uncommon x3");
            Assert.That(Share(boosted, EcologyCoreTestData.GlimmerEel), Is.EqualTo(3f / 7f).Within(0.02f), "Rare x3");
        }

        static void AssertSameCatch(in CatchResult want, in CatchResult got, string why)
        {
            Assert.That(got.IsJunk, Is.EqualTo(want.IsJunk), why);
            Assert.That(got.ItemIndex, Is.EqualTo(want.ItemIndex), why);
            Assert.That(got.WeightG, Is.EqualTo(want.WeightG), why);
            Assert.That(got.LengthMm, Is.EqualTo(want.LengthMm), why);
            Assert.That(got.Quality, Is.EqualTo(want.Quality), why);
            Assert.That(got.ValueCoins, Is.EqualTo(want.ValueCoins), why);
        }

        static CatchResult Resolve(FishingResolver r, in FishingContext ctx, float luck, int i, out float t)
        {
            FishingContext c = ctx;
            c.Luck = luck;
            Pcg32 rng = Rng.Stream(78, "luck", (ulong)i);
            return r.Resolve(in c, ref rng, null, out t, out _);
        }

        static FishingRoll Roll(EcologyDatabase db, FishingResolver r, in FishingContext ctx, float luck, float depth, int i, in RollOverrides ov)
        {
            FishingContext c = ctx;
            c.Luck = luck;
            c.Depth = depth;
            Pcg32 rng = Rng.Stream(79, "roll", (ulong)i);
            return FishingRoller.Roll(db, r, in c, null, in ov, FishingTestData.BaitWorm, ref rng, out _);
        }

        // Catches (B-D4): luck not sanitised before the size skew (NaN or |luck| > 1 reaching CatchMath), on the resolver path and the
        // roller's forced-species path. Non-vacuous: luck 1 changes some weights against luck 0.
        [Test]
        public void FishingResolver_Luck_NaNIsZeroAndOutOfRangeClamped()
        {
            EcologyDatabase db = Db(EcologyTestData.Rule(EcologyCoreTestData.Bluegill), EcologyTestData.Rule(EcologyCoreTestData.GrumpyCarp));
            var resolver = new FishingResolver(db);
            FishingContext ctx = Ctx();
            var forced = new RollOverrides { ForcedItemIndex = EcologyCoreTestData.GrumpyCarp };
            int differs = 0;
            for (int i = 0; i < 300; i++)
            {
                CatchResult zero = Resolve(resolver, in ctx, 0f, i, out float t0);
                CatchResult nan = Resolve(resolver, in ctx, float.NaN, i, out float tn);
                AssertSameCatch(in zero, in nan, "NaN luck = 0, stream " + i);
                Assert.That(tn, Is.EqualTo(t0), "NaN luck weightT, stream " + i);
                CatchResult one = Resolve(resolver, in ctx, 1f, i, out _);
                CatchResult five = Resolve(resolver, in ctx, 5f, i, out _);
                AssertSameCatch(in one, in five, "luck 5 = 1, stream " + i);
                CatchResult minusOne = Resolve(resolver, in ctx, -1f, i, out _);
                CatchResult minusFive = Resolve(resolver, in ctx, -5f, i, out _);
                AssertSameCatch(in minusOne, in minusFive, "luck -5 = -1, stream " + i);
                if (one.WeightG != zero.WeightG) differs++;

                FishingRoll fZero = Roll(db, resolver, in ctx, 0f, 3f, i, in forced);
                FishingRoll fNan = Roll(db, resolver, in ctx, float.NaN, 3f, i, in forced);
                AssertSameCatch(in fZero.Catch, in fNan.Catch, "forced path NaN luck = 0, stream " + i);
                FishingRoll fOne = Roll(db, resolver, in ctx, 1f, 3f, i, in forced);
                FishingRoll fFive = Roll(db, resolver, in ctx, 5f, 3f, i, in forced);
                AssertSameCatch(in fOne.Catch, in fFive.Catch, "forced path luck 5 = 1, stream " + i);
            }
            Assert.That(differs, Is.GreaterThan(0), "control: luck 1 skews some weights");
        }

        // Catches (B-D6): the roller's depth sanitiser for the bite plan mapping +Infinity to anything but 6 m (DepthMultFullDepth) or NaN to
        // anything but 0 (a NaN depth would give a NaN bite wait). Forced species, so the resolver's depth gate is not in play.
        // Non-vacuous: depth 0 and depth 6 give different bite waits.
        [Test]
        public void FishingRoller_Depth_InfiniteIsSixAndNaNIsZero()
        {
            EcologyDatabase db = Db(EcologyTestData.Rule(EcologyCoreTestData.Bluegill));
            var resolver = new FishingResolver(db);
            FishingContext ctx = Ctx();
            var forced = new RollOverrides { ForcedItemIndex = EcologyCoreTestData.Bluegill };
            int differs = 0;
            for (int i = 0; i < 300; i++)
            {
                FishingRoll six = Roll(db, resolver, in ctx, 0f, 6f, i, in forced);
                FishingRoll inf = Roll(db, resolver, in ctx, 0f, float.PositiveInfinity, i, in forced);
                Assert.That(inf.BiteWait, Is.EqualTo(six.BiteWait), "+Inf depth = 6 m, stream " + i);
                Assert.That(inf.NibbleCount, Is.EqualTo(six.NibbleCount), "+Inf nibbles, stream " + i);
                Assert.That(inf.Nibble0, Is.EqualTo(six.Nibble0), "+Inf nibble 0, stream " + i);
                FishingRoll zero = Roll(db, resolver, in ctx, 0f, 0f, i, in forced);
                FishingRoll nan = Roll(db, resolver, in ctx, 0f, float.NaN, i, in forced);
                Assert.That(float.IsNaN(nan.BiteWait), Is.False, "NaN depth never gives a NaN bite wait, stream " + i);
                Assert.That(nan.BiteWait, Is.EqualTo(zero.BiteWait), "NaN depth = 0 m, stream " + i);
                if (zero.BiteWait != six.BiteWait) differs++;
            }
            Assert.That(differs, Is.GreaterThan(0), "control: depth changes the bite wait");
        }
    }
}
