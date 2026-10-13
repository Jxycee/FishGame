// Tests/EditMode/Ecology/BitingNowTests.cs - T8: the journal's "biting now" badge (M3-D26) and the level-2 hint union.
using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;
using static FishGame.Tests.EditMode.EcologyCoreTestData;

namespace FishGame.Tests.EditMode
{
    public class BitingNowTests
    {
        // Reeds, night, summer, rain, rod tier 1, depth 0.5: the eel rule (deep water only, rod 1, Rain|Fog, Dusk|Night) fails on sub-area
        // and has no depth issue until the test changes it.
        static FishingContext EelCtx()
        {
            FishingContext c = Context(Reeds, MinuteNight, Season.Summer, WeatherMask.Rain, rodTier: 1);
            c.Depth = 0.5f;
            return c;
        }

        [Test]
        public void BitingNow_IgnoresSubAreaAndDepth()
        {
            SpawnRuleData[] rules = LakeRules();
            rules[GlimmerEel].MinDepth = 3f;
            EcologyDatabase db = Database(Lake(rules));
            var resolver = new FishingResolver(db);
            FishingContext ctx = EelCtx();

            Assert.IsTrue(BitingNow.IsBiting(resolver, db.Zones[ZoneLake], GlimmerEel, in ctx));
            Assert.IsFalse(resolver.Passes(db.Zones[ZoneLake].Rules[GlimmerEel], in ctx, null), "the honest gate fails on sub-area and depth");

            FishingContext any = ctx;
            any.SubAreaIndex = EcologyConstants.AnySubArea; any.Depth = float.PositiveInfinity;
            Assert.IsTrue(resolver.Passes(db.Zones[ZoneLake].Rules[GlimmerEel], in any, null), "control: only sub-area and depth differed");
            Assert.AreEqual(Reeds, ctx.SubAreaIndex, "the caller's context is not modified");
            Assert.AreEqual(0.5f, ctx.Depth);

            Assert.Throws<ArgumentNullException>(() => BitingNow.IsBiting(null, db.Zones[ZoneLake], GlimmerEel, in ctx));
            Assert.Throws<ArgumentNullException>(() => BitingNow.IsBiting(resolver, null, GlimmerEel, in ctx));
        }

        [Test]
        public void BitingNow_RespectsSeasonWeatherBandAndRodTier()
        {
            EcologyDatabase db = Database();
            var resolver = new FishingResolver(db);
            ZoneData lake = db.Zones[ZoneLake];
            FishingContext ctx = EelCtx();
            Assert.IsTrue(BitingNow.IsBiting(resolver, lake, GlimmerEel, in ctx), "baseline");

            ctx = EelCtx(); ctx.Season = Season.Spring;
            Assert.IsFalse(BitingNow.IsBiting(resolver, lake, GlimmerEel, in ctx), "spring");
            ctx = EelCtx(); ctx.Weather = WeatherMask.Clear;
            Assert.IsFalse(BitingNow.IsBiting(resolver, lake, GlimmerEel, in ctx), "clear");
            ctx = EelCtx(); ctx.Minute = MinuteMidday;
            Assert.IsFalse(BitingNow.IsBiting(resolver, lake, GlimmerEel, in ctx), "midday");
            ctx = EelCtx(); ctx.RodTier = 0;
            Assert.IsFalse(BitingNow.IsBiting(resolver, lake, GlimmerEel, in ctx), "rod 0");

            ctx = EelCtx();
            Assert.IsFalse(BitingNow.IsBiting(resolver, lake, SunnyTrout, in ctx), "sunny_trout needs clear weather at midday");
        }

        [Test]
        public void BitingNow_SpecialsCountAsBiting()
        {
            EcologyDatabase db = Database();
            var resolver = new FishingResolver(db);
            FishingContext ctx = EelCtx();
            Assert.IsTrue(BitingNow.IsBiting(resolver, db.Zones[ZoneLake], GildedCarp, in ctx), "all of the special's gates are open");
        }

        static SpawnRuleData MulletRule(SeasonMask s, WeatherMask w, TimeBandMask b) => Rule(SilverMullet, 0b011, s, w, b, 9f);

        [Test]
        public void JournalHints_Summarize_UnionOfAllRules()
        {
            SpawnRuleData[] lakeRules =
            {
                Rule(GlimmerEel, 0b100, SeasonMask.Summer | SeasonMask.Autumn, WeatherMask.Rain | WeatherMask.Fog, TimeBandMask.Dusk | TimeBandMask.Night, 8f, rod: 1),
                MulletRule(SeasonMask.Spring, WeatherMask.Clear, TimeBandMask.Dawn),
            };
            SpawnRuleData[] harbourRules = { MulletRule(SeasonMask.Winter, WeatherMask.Fog, TimeBandMask.Night) };
            EcologyDatabase db = Database(Lake(lakeRules), Harbour(harbourRules));

            var zones = new List<int> { 99, 98 };                                    // pre-filled: must be cleared
            JournalHints.Summarize(db, SilverMullet, out SeasonMask seasons, out WeatherMask weather, out TimeBandMask bands, zones);
            Assert.AreEqual(SeasonMask.Spring | SeasonMask.Winter, seasons);
            Assert.AreEqual(WeatherMask.Clear | WeatherMask.Fog, weather);
            Assert.AreEqual(TimeBandMask.Dawn | TimeBandMask.Night, bands);
            CollectionAssert.AreEqual(new[] { 0, 1 }, zones);

            JournalHints.Summarize(db, GlimmerEel, out seasons, out weather, out bands, zones);
            Assert.AreEqual(SeasonMask.Summer | SeasonMask.Autumn, seasons);
            Assert.AreEqual(WeatherMask.Rain | WeatherMask.Fog, weather);
            Assert.AreEqual(TimeBandMask.Dusk | TimeBandMask.Night, bands);
            CollectionAssert.AreEqual(new[] { 0 }, zones);

            JournalHints.Summarize(db, SunnyTrout, out seasons, out weather, out bands, zones);   // no rule anywhere in this database
            Assert.AreEqual(SeasonMask.None, seasons);
            Assert.AreEqual(WeatherMask.None, weather);
            Assert.AreEqual(TimeBandMask.None, bands);
            Assert.AreEqual(0, zones.Count);

            Assert.Throws<ArgumentNullException>(() => JournalHints.Summarize(null, SilverMullet, out _, out _, out _, zones));
            Assert.Throws<ArgumentNullException>(() => JournalHints.Summarize(db, SilverMullet, out _, out _, out _, null));
        }
    }
}
