// Tests for the T9 ecology Defs (Data): ToCore mappings, the rules sort, WriteBake, the M3 fields on SpotDef / ArtPaletteDef / SfxCueTableDef.
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class EcologyDefsTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            T o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        // A minimal valid fishing set (1 rod, the bare hook, 1 profile, the given species ids, 2 junk, the four singletons) plus the given zones.
        private sealed class Fixture
        {
            public ContentRegistry Registry;
            public FishSpeciesDef[] Species;
            public JunkDef[] Junk;
            public WeatherTableDef Weather;
            public EcologyTuningDef Tuning;
            public SpotDef Spot;
        }

        private Fixture MakeFixture(string[] speciesIds, Func<Fixture, FishingZoneDef[]> zones)
        {
            var f = new Fixture();
            RodDef twig = Make<RodDef>();
            twig.Configure("rod_twig", "Twig Rod", 0, 14f, 0.9f, 0.8f, 0.5f, 0.5f, 0.9f, 0, 0);
            BaitDef bare = Make<BaitDef>();
            bare.Configure("bait_bare_hook", "Bare Hook", 1, 1, 1, 1.3f, 1.5f, 1, true);
            FightProfileDef steady = Make<FightProfileDef>();
            steady.Configure("steady", "Steady", 1f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0.6f, 0f, 0f, 0.5f, 0f, 1f, 1f, 1f, 4f);

            f.Species = new FishSpeciesDef[speciesIds.Length];
            for (int i = 0; i < speciesIds.Length; i++)
            {
                f.Species[i] = Make<FishSpeciesDef>();
                f.Species[i].Configure(speciesIds[i], speciesIds[i], FishRarity.Common, 5, 200, 800, 2.2f, 1f, steady, BodyShape.Round,
                    Color.blue, Color.white, 1f, 0f, false, 0, 0f, null);
            }
            JunkDef boot = Make<JunkDef>();
            boot.Configure("old_boot", "Old Boot", 2, BodyShape.Long, Color.gray);
            JunkDef can = Make<JunkDef>();
            can.Configure("tin_can", "Tin Can", 2, BodyShape.Round, Color.gray);
            f.Junk = new[] { boot, can };

            FishingTuningDef ft = Make<FishingTuningDef>();
            FishingTuning t = FishingTestData.Tuning();
            ft.Configure(in t);

            f.Weather = Make<WeatherTableDef>();
            f.Weather.Configure("weather_temperate", new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, new float[] { 1, 1.5f, 1, 1, 1, 1, 1, 1, 0.3f, 1, 1, 1.3f });
            f.Tuning = Make<EcologyTuningDef>();
            f.Tuning.Configure(60, 120, 61, 121, 45, 60, 3f);

            f.Spot = Make<SpotDef>();
            f.Registry = Make<ContentRegistry>();
            f.Registry.Configure(1, new[] { f.Spot });
            f.Registry.ConfigureFishing(new[] { twig }, new[] { bare }, new[] { steady }, f.Species, f.Junk, ft, Make<SfxCueTableDef>(),
                Make<ArtPaletteDef>(), Make<WaterSettingsDef>());
            FishingZoneDef[] z = zones(f);
            f.Spot.Configure("spot_a", "Spot A", "Spot_A", 2, z[0]);
            f.Registry.ConfigureEcology(z, f.Tuning);
            return f;
        }

        private static SubAreaBand Band(string id, float min, float max, float junk)
        {
            return new SubAreaBand { id = id, minShoreM = min, maxShoreM = max, junkChance = junk };
        }

        // ---- EcologyDefs_ToCore_MapsEveryField --------------------------------------------------------------------------------

        // Catches: a field mapped to the wrong target (every value is distinct, so a swapped pair fails); a missing defensive copy;
        // WriteBake touching id or accepting bad lengths.
        [TestCase("zone")]
        [TestCase("sub_area_band")]
        [TestCase("rule")]
        [TestCase("junk_weight")]
        [TestCase("weather_table")]
        [TestCase("shore_grid")]
        [TestCase("ecology_tuning")]
        public void EcologyDefs_ToCore_MapsEveryField(string which)
        {
            switch (which)
            {
                case "zone": ZoneMapsCounts(); break;
                case "sub_area_band": BandMapsFields(); break;
                case "rule": RuleMapsFields(); break;
                case "junk_weight": JunkWeightMapsFields(); break;
                case "weather_table": WeatherTableMapsAndCopies(); break;
                case "shore_grid": ShoreGridMapsAndBakes(); break;
                case "ecology_tuning": TuningMapsFields(); break;
                default: Assert.Fail("unknown case " + which); break;
            }
        }

        private void ZoneMapsCounts()
        {
            FishingZoneDef zone = null;
            Fixture f = MakeFixture(new[] { "fish_b", "fish_a" }, fx =>
            {
                zone = Make<FishingZoneDef>();
                var rules = new List<SpawnRule> { new SpawnRule { fish = fx.Species[0], subAreaIds = new[] { "mid" }, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = 1f } };
                var bands = new List<SubAreaBand> { Band("near", 0f, 5f, 0.1f), Band("mid", 5f, 12f, 0.2f), Band("far", 12f, 0f, 0.3f) };
                var junk = new List<JunkWeight> { new JunkWeight { junk = fx.Junk[0], weight = 2f }, new JunkWeight { junk = fx.Junk[1], weight = 3f } };
                zone.Configure("zone_a", "Zone A", bands, rules, junk, fx.Weather, null);
                return new[] { zone };
            });
            ZoneData z = zone.ToCore(f.Registry);
            Assert.That(z.SubAreas.Count, Is.EqualTo(3));
            Assert.That(z.SubAreas[0].MaxShoreM, Is.EqualTo(5f));
            Assert.That(float.IsPositiveInfinity(z.SubAreas[2].MaxShoreM), Is.True, "the last band's maxShoreM 0 becomes +Infinity");
            Assert.That(z.SubAreas[1].JunkChance, Is.EqualTo(0.2f));
            Assert.That(z.Rules.Count, Is.EqualTo(1));
            Assert.That(z.Junk.Count, Is.EqualTo(2));
            Assert.That(z.Weather.Base.Length, Is.EqualTo(9));
            Assert.That(z.Weather.SeasonColumnMult.Length, Is.EqualTo(12));
            Assert.That(z.Grid, Is.Null);
        }

        private static void BandMapsFields()
        {
            var b = new SubAreaBand { id = "x", minShoreM = 1.1f, maxShoreM = 2.2f, junkChance = 3.3f };
            SubAreaBandData d = b.ToCore();
            Assert.That(d.MinShoreM, Is.EqualTo(1.1f));
            Assert.That(d.MaxShoreM, Is.EqualTo(2.2f));
            Assert.That(d.JunkChance, Is.EqualTo(3.3f));
            var open = new SubAreaBand { id = "y", minShoreM = 1f, maxShoreM = 0f, junkChance = 0.5f };
            Assert.That(open.ToCore().MaxShoreM, Is.EqualTo(0f), "the zone, not the band, decides open-endedness");
        }

        private void RuleMapsFields()
        {
            FishingZoneDef zone = null;
            SpawnRule withIds = null, withoutIds = null;
            Fixture f = MakeFixture(new[] { "fish_b", "fish_a" }, fx =>
            {
                zone = Make<FishingZoneDef>();
                withIds = new SpawnRule
                {
                    fish = fx.Species[0], subAreaIds = new[] { "near", "far" }, seasons = SeasonMask.Spring | SeasonMask.Winter,
                    weather = WeatherMask.Rain | WeatherMask.Fog, bands = TimeBandMask.Dawn | TimeBandMask.Night, weight = 1.1f, minRodTier = 2,
                    minBaitRating = 3, minDepth = 4.4f, special = true, specialChance = 0.25f, catchLimitPerRun = 5, minPlayersInSession = 2,
                };
                withoutIds = new SpawnRule
                {
                    fish = fx.Species[1], subAreaIds = new string[0], seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All,
                    weight = 6.6f, minRodTier = 0, special = false, catchLimitPerRun = -1,
                };
                var bands = new List<SubAreaBand> { Band("near", 0f, 5f, 0.1f), Band("mid", 5f, 12f, 0.2f), Band("far", 12f, 0f, 0.3f) };
                zone.Configure("zone_a", "Zone A", bands, new List<SpawnRule> { withIds, withoutIds }, new List<JunkWeight> { new JunkWeight { junk = fx.Junk[0], weight = 1f } }, fx.Weather, null);
                return new[] { zone };
            });
            ZoneData z = zone.ToCore(f.Registry);
            ushort aIndex = f.Registry.SpeciesIndex("fish_a"), bIndex = f.Registry.SpeciesIndex("fish_b");
            Assert.That(aIndex, Is.Not.EqualTo(bIndex));
            SpawnRuleData forB = z.Rules[1];   // sorted by fish id: fish_a first, fish_b second
            Assert.That(z.Rules[0].SpeciesIndex, Is.EqualTo(aIndex));
            Assert.That(forB.SpeciesIndex, Is.EqualTo(bIndex));
            Assert.That(forB.SubAreaMask, Is.EqualTo((byte)0b101), "ids {near, far} are bits 0 and 2");
            Assert.That(forB.Seasons, Is.EqualTo(SeasonMask.Spring | SeasonMask.Winter));
            Assert.That(forB.Weather, Is.EqualTo(WeatherMask.Rain | WeatherMask.Fog));
            Assert.That(forB.Bands, Is.EqualTo(TimeBandMask.Dawn | TimeBandMask.Night));
            Assert.That(forB.Weight, Is.EqualTo(1.1f));
            Assert.That(forB.MinRodTier, Is.EqualTo((byte)2));
            Assert.That(forB.MinBaitRating, Is.EqualTo(3));
            Assert.That(forB.MinDepth, Is.EqualTo(4.4f));
            Assert.That(forB.Special, Is.True);
            Assert.That(forB.SpecialChance, Is.EqualTo(0.25f));
            Assert.That(forB.CatchLimitPerRun, Is.EqualTo(5));
            Assert.That(forB.MinPlayersInSession, Is.EqualTo(2));
            Assert.That(z.Rules[0].SubAreaMask, Is.EqualTo((byte)0), "an empty id list means all sub-areas");
            Assert.That(z.Rules[0].Weight, Is.EqualTo(6.6f));
            Assert.That(z.Rules[0].CatchLimitPerRun, Is.EqualTo(-1));

            withIds.subAreaIds = new[] { "near", "nowhere" };
            ArgumentException ex = Assert.Throws<ArgumentException>(() => zone.ToCore(f.Registry));
            StringAssert.Contains("zone 'zone_a' rule 'fish_b' names unknown sub-area 'nowhere'", ex.Message);
        }

        private void JunkWeightMapsFields()
        {
            FishingZoneDef zone = null;
            Fixture f = MakeFixture(new[] { "fish_a" }, fx =>
            {
                zone = Make<FishingZoneDef>();
                var bands = new List<SubAreaBand> { Band("only", 0f, 0f, 0.1f) };
                var junk = new List<JunkWeight> { new JunkWeight { junk = fx.Junk[1], weight = 7.7f }, new JunkWeight { junk = fx.Junk[0], weight = 8.8f } };
                zone.Configure("zone_a", "Zone A", bands, new List<SpawnRule>(), junk, fx.Weather, null);
                return new[] { zone };
            });
            ZoneData z = zone.ToCore(f.Registry);
            Assert.That(z.Junk.Count, Is.EqualTo(2));
            Assert.That(z.Junk[0].JunkIndex, Is.EqualTo(f.Registry.JunkIndex("tin_can")), "list order is kept; the index is the registry junk index");
            Assert.That(z.Junk[0].Weight, Is.EqualTo(7.7f));
            Assert.That(z.Junk[1].JunkIndex, Is.EqualTo(f.Registry.JunkIndex("old_boot")));
            Assert.That(z.Junk[1].Weight, Is.EqualTo(8.8f));
        }

        private void WeatherTableMapsAndCopies()
        {
            var def = Make<WeatherTableDef>();
            var baseM = new float[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f, 6.6f, 7.7f, 8.8f, 9.9f };
            var mult = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            def.Configure("w", baseM, mult);
            baseM[0] = -1f; mult[0] = -1f;
            WeatherTableData t = def.ToCore();
            Assert.That(t.Base, Is.EqualTo(new float[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f, 6.6f, 7.7f, 8.8f, 9.9f }), "the Def copied its input");
            Assert.That(t.SeasonColumnMult, Is.EqualTo(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }));
            t.Base[1] = -5f;
            Assert.That(def.ToCore().Base[1], Is.EqualTo(2.2f), "mutating the result does not change the Def");
            Assert.That(def.Id, Is.EqualTo("w"));
        }

        private void ShoreGridMapsAndBakes()
        {
            var def = Make<ShoreGridDef>();
            var dm = new ushort[] { 0, 10, 20, 30, 40, 0xFFFF };
            var castable = new byte[] { 0b00101010 };
            var reachable = new byte[] { 0b00010100 };
            def.Configure("g", 3, 2, 1.5f, -4.5f, 7.25f, dm, castable, reachable, new[] { new Vector3(1, 2, 3) }, "Spot_X");
            ShoreGrid g = def.ToCore();
            Assert.That(g.Width, Is.EqualTo(3));
            Assert.That(g.Height, Is.EqualTo(2));
            Assert.That(g.CellSizeM, Is.EqualTo(1.5f));
            Assert.That(g.OriginX, Is.EqualTo(-4.5f));
            Assert.That(g.OriginZ, Is.EqualTo(7.25f));
            g.CellCentre(1, 1, out float cx, out float cz);
            Assert.That(g.ShoreM(cx, cz), Is.EqualTo(4f).Within(1e-4f), "cell (1,1) is index 4 = 40 dm");
            Assert.That(g.IsWater(cx, cz), Is.True);
            g.CellCentre(2, 1, out cx, out cz);
            Assert.That(g.IsWater(cx, cz), Is.False, "0xFFFF is not water");
            Assert.That(g.IsCastable(0, 0), Is.False);
            Assert.That(g.IsCastable(1, 0), Is.True);
            Assert.That(g.IsCastable(0, 1), Is.True, "bit 3 of 0b00101010");
            Assert.That(g.IsCastable(2, 1), Is.True, "bit 5");
            Assert.That(g.IsReachable(2, 0), Is.True, "bit 2 of 0b00010100");
            Assert.That(g.IsReachable(1, 1), Is.True, "bit 4");
            Assert.That(g.IsReachable(1, 0), Is.False);
            Assert.That(def.StandPoints.Count, Is.EqualTo(1));
            Assert.That(def.BakedFromScene, Is.EqualTo("Spot_X"));

            // WriteBake replaces every field except the id.
            def.WriteBake(4, 1, 2f, 10f, 20f, new ushort[] { 1, 2, 3, 4 }, new byte[] { 0x05 }, new byte[] { 0x0A }, new[] { new Vector3(4, 5, 6), new Vector3(7, 8, 9) }, "Spot_Y");
            Assert.That(def.Id, Is.EqualTo("g"), "WriteBake leaves the id alone");
            Assert.That(def.Width, Is.EqualTo(4));
            Assert.That(def.Height, Is.EqualTo(1));
            Assert.That(def.CellSizeM, Is.EqualTo(2f));
            Assert.That(def.OriginX, Is.EqualTo(10f));
            Assert.That(def.OriginZ, Is.EqualTo(20f));
            Assert.That(def.StandPoints.Count, Is.EqualTo(2));
            Assert.That(def.StandPoints[1], Is.EqualTo(new Vector3(7, 8, 9)));
            Assert.That(def.BakedFromScene, Is.EqualTo("Spot_Y"));
            ShoreGrid baked = def.ToCore();
            Assert.That(baked.Width, Is.EqualTo(4));
            Assert.That(baked.IsCastable(0, 0), Is.True);
            Assert.That(baked.IsReachable(1, 0), Is.True);

            // Bad lengths and values throw and change nothing.
            Assert.Throws<ArgumentException>(() => def.WriteBake(2, 2, 1f, 0f, 0f, new ushort[5], new byte[1], new byte[1], new Vector3[0], "s"), "5 entries for 2 x 2");
            Assert.Throws<ArgumentException>(() => def.WriteBake(2, 2, 1f, 0f, 0f, new ushort[4], new byte[2], new byte[1], new Vector3[0], "s"), "castable length");
            Assert.Throws<ArgumentException>(() => def.WriteBake(2, 2, 1f, 0f, 0f, new ushort[4], new byte[1], new byte[2], new Vector3[0], "s"), "reachable length");
            Assert.Throws<ArgumentException>(() => def.WriteBake(0, 2, 1f, 0f, 0f, new ushort[0], new byte[0], new byte[0], new Vector3[0], "s"), "width 0");
            Assert.Throws<ArgumentException>(() => def.WriteBake(2, 2, 0f, 0f, 0f, new ushort[4], new byte[1], new byte[1], new Vector3[0], "s"), "cell size 0");
            Assert.Throws<ArgumentException>(() => def.WriteBake(2, 2, 1f, 0f, 0f, null, new byte[1], new byte[1], new Vector3[0], "s"), "null array");
            Assert.That(def.Width, Is.EqualTo(4), "a rejected bake changed nothing");
        }

        private void TuningMapsFields()
        {
            var def = Make<EcologyTuningDef>();
            def.Configure(1, 2, 3, 4, 5, 6, 7.5f);
            EcologyTuning t = def.ToCore();
            Assert.That(t.HotspotFirstMinMin, Is.EqualTo(1));
            Assert.That(t.HotspotFirstMinMax, Is.EqualTo(2));
            Assert.That(t.HotspotGapMin, Is.EqualTo(3));
            Assert.That(t.HotspotGapMax, Is.EqualTo(4));
            Assert.That(t.HotspotDurationMin, Is.EqualTo(5));
            Assert.That(t.HotspotDurationMax, Is.EqualTo(6));
            Assert.That(t.HotspotRadiusM, Is.EqualTo(7.5f));
        }

        // ---- FishingZoneDef_RulesSortedByFishIdOrdinal ------------------------------------------------------------------------

        // Catches: culture-sensitive or unstable sorting (ordinal: '0' < '_' < 'a'; a culture compare puts fish_a before fish0 or ignores '_').
        [Test]
        public void FishingZoneDef_RulesSortedByFishIdOrdinal()
        {
            FishingZoneDef zone = null;
            Fixture f = MakeFixture(new[] { "fisha", "fish_a", "fish0" }, fx =>
            {
                zone = Make<FishingZoneDef>();
                FishSpeciesDef fisha = fx.Species[0], fishUnderscoreA = fx.Species[1], fish0 = fx.Species[2];
                var rules = new List<SpawnRule>
                {
                    new SpawnRule { fish = fisha, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = 1f },
                    new SpawnRule { fish = fishUnderscoreA, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = 2f },
                    new SpawnRule { fish = fish0, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = 3f },
                    new SpawnRule { fish = fish0, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = 4f },   // same fish twice: list order kept
                };
                zone.Configure("zone_a", "Zone A", new List<SubAreaBand> { Band("only", 0f, 0f, 0.1f) }, rules,
                    new List<JunkWeight> { new JunkWeight { junk = fx.Junk[0], weight = 1f } }, fx.Weather, null);
                return new[] { zone };
            });
            ZoneData z = zone.ToCore(f.Registry);
            var ids = new List<string>();
            var weights = new List<float>();
            for (int i = 0; i < z.Rules.Count; i++)
            {
                ids.Add(f.Registry.SpeciesAt(z.Rules[i].SpeciesIndex).Id);
                weights.Add(z.Rules[i].Weight);
            }
            Assert.That(ids, Is.EqualTo(new[] { "fish0", "fish0", "fish_a", "fisha" }));
            Assert.That(weights, Is.EqualTo(new[] { 3f, 4f, 2f, 1f }), "equal fish ids keep list order (stable)");
        }

        // ---- SpotDef / ArtPaletteDef / SfxCueTableDef -------------------------------------------------------------------------

        // Catches: the M1 overload resetting M3 fields (would wipe lake_dock's zone on every M1 EnsureLakeDockSpot).
        [Test]
        public void SpotDef_M3Configure_KeepsM1Fields()
        {
            var zone = Make<FishingZoneDef>();
            var spot = Make<SpotDef>();
            spot.Configure("lake_dock", "Lake Dock", "Spot_LakeDock", 3, zone);
            Assert.That(spot.Id, Is.EqualTo("lake_dock"));
            Assert.That(spot.DisplayName, Is.EqualTo("Lake Dock"));
            Assert.That(spot.SceneName, Is.EqualTo("Spot_LakeDock"));
            Assert.That(spot.MapOrder, Is.EqualTo(3));
            Assert.That(spot.Zone, Is.SameAs(zone));

            spot.Configure("other_id", "Other", "Spot_Other");
            Assert.That(spot.Id, Is.EqualTo("other_id"));
            Assert.That(spot.DisplayName, Is.EqualTo("Other"));
            Assert.That(spot.SceneName, Is.EqualTo("Spot_Other"));
            Assert.That(spot.MapOrder, Is.EqualTo(3), "the 3-arg Configure keeps mapOrder");
            Assert.That(spot.Zone, Is.SameAs(zone), "the 3-arg Configure keeps the zone");
        }

        // Catches: M3 fields not serialized, shared arrays, M2 configure clobbering M3 fields, a null WeatherLooks on a fresh Def.
        [Test]
        public void ArtPaletteDef_M3Fields_RoundTrip()
        {
            var palette = Make<ArtPaletteDef>();
            Assert.That(palette.WeatherLooks, Is.Not.Null);
            Assert.That(palette.WeatherLooks.Count, Is.EqualTo(0), "a fresh Def reads empty, not null");

            var sun = new SunMoonSettings
            {
                sunriseFraction = 0.01f, sunsetFraction = 0.71f, minElevationDeg = 13f, flipWindow = 0.03f, moonIntensity = 0.26f,
                moonEulerDeg = new Vector3(1, 2, 3), moonTint = new Color(0.1f, 0.2f, 0.3f, 0.4f), blendWindow = 0.06f,
            };
            var looks = new[]
            {
                new WeatherAmbient { fogDensityMult = 1.1f, fogDensityMin = 0.01f, lightMult = 0.9f, desaturate = 0.2f, rainAmount = 0.3f },
                new WeatherAmbient { fogDensityMult = 2.1f, fogDensityMin = 0.02f, lightMult = 0.8f, desaturate = 0.4f, rainAmount = 0.5f },
                new WeatherAmbient { fogDensityMult = 3.1f, fogDensityMin = 0.03f, lightMult = 0.7f, desaturate = 0.6f, rainAmount = 0.7f },
            };
            palette.ConfigureM3(in sun, looks);
            looks[0].rainAmount = 99f;   // the Def copied the array

            SunMoonSettings s = palette.SunMoon;
            Assert.That(s.sunriseFraction, Is.EqualTo(0.01f));
            Assert.That(s.sunsetFraction, Is.EqualTo(0.71f));
            Assert.That(s.minElevationDeg, Is.EqualTo(13f));
            Assert.That(s.flipWindow, Is.EqualTo(0.03f));
            Assert.That(s.moonIntensity, Is.EqualTo(0.26f));
            Assert.That(s.moonEulerDeg, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(s.moonTint, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(s.blendWindow, Is.EqualTo(0.06f));
            Assert.That(palette.WeatherLooks.Count, Is.EqualTo(3));
            WeatherAmbient w = palette.WeatherLooks[2];
            Assert.That(w.fogDensityMult, Is.EqualTo(3.1f));
            Assert.That(w.fogDensityMin, Is.EqualTo(0.03f));
            Assert.That(w.lightMult, Is.EqualTo(0.7f));
            Assert.That(w.desaturate, Is.EqualTo(0.6f));
            Assert.That(w.rainAmount, Is.EqualTo(0.7f));
            Assert.That(palette.WeatherLooks[0].rainAmount, Is.EqualTo(0.3f), "mutating the input array afterwards changes nothing");

            // The M2 Configure leaves the M3 fields alone.
            palette.Configure(new[] { "a" }, new PaletteSeason[0], 0.5f, Color.red, new DayPhaseAmbient[0], Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, 1);
            Assert.That(palette.SunMoon.sunsetFraction, Is.EqualTo(0.71f));
            Assert.That(palette.WeatherLooks.Count, Is.EqualTo(3));

            // SfxCueTableDef.seasonAmbience, written the way the builder writes it (SerializedObject), reads back through the getter.
            var cues = Make<SfxCueTableDef>();
            Assert.That(cues.SeasonAmbience.Count, Is.EqualTo(0));
            var so = new SerializedObject(cues);
            SerializedProperty arr = so.FindProperty("seasonAmbience");
            Assert.That(arr, Is.Not.Null, "the field is named seasonAmbience");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("windGain").floatValue = 1f + i;
                e.FindPropertyRelative("windLfoHz").floatValue = 0.1f + i;
                e.FindPropertyRelative("lapIntervalMin").floatValue = 0.5f + i;
                e.FindPropertyRelative("lapIntervalMax").floatValue = 1.5f + i;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(cues.SeasonAmbience.Count, Is.EqualTo(4));
            Assert.That(cues.SeasonAmbience[3].windGain, Is.EqualTo(4f));
            Assert.That(cues.SeasonAmbience[2].windLfoHz, Is.EqualTo(2.1f).Within(1e-5f));
            Assert.That(cues.SeasonAmbience[1].lapIntervalMin, Is.EqualTo(1.5f));
            Assert.That(cues.SeasonAmbience[0].lapIntervalMax, Is.EqualTo(1.5f));
            cues.Configure(new[] { new SfxCueRow { id = "x" } });
            Assert.That(cues.SeasonAmbience.Count, Is.EqualTo(4), "Configure(rows) keeps the season ambience");
            Assert.That(cues.Rows.Count, Is.EqualTo(1));
        }

        // Catches: an unbaked grid turning into a 0 x 0 ShoreGrid (would make every cast "not water" in T15).
        [Test]
        public void ShoreGridDef_Unbaked_ToCoreIsNull()
        {
            var grid = Make<ShoreGridDef>();
            grid.Configure("g", 0, 0, 1f, -60f, -60f, new ushort[0], new byte[0], new byte[0], new Vector3[0], "");
            Assert.That(grid.ToCore(), Is.Null);

            FishingZoneDef zone = null;
            Fixture f = MakeFixture(new[] { "fish_a" }, fx =>
            {
                zone = Make<FishingZoneDef>();
                zone.Configure("zone_a", "Zone A", new List<SubAreaBand> { Band("only", 0f, 0f, 0.1f) }, new List<SpawnRule>(),
                    new List<JunkWeight> { new JunkWeight { junk = fx.Junk[0], weight = 1f } }, fx.Weather, grid);
                return new[] { zone };
            });
            Assert.That(zone.ToCore(f.Registry).Grid, Is.Null, "an unbaked grid gives ZoneData.Grid == null");
            Assert.That(zone.ShoreGrid, Is.SameAs(grid));
        }
    }
}
