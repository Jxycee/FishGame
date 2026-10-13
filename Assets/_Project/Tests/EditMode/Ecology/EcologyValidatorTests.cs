// T11: EcologyValidators over in-memory Defs (DataRules) and hand-built Core data (Coverage, Variety). Nothing here reads a committed asset.
// Coverage fixtures use EcologyCoreTestData (call it by class name: the FishGame.Fishing namespace shadows a `using static` import).
using System;
using System.Collections.Generic;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class EcologyValidatorTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            T o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        // ---- DataRules rig: a minimal VALID registry (one zone, two spots, three species, a special) ----------------------------------

        private sealed class Rig
        {
            public ContentRegistry Registry;
            public FishSpeciesDef FishA, FishB, FishS;
            public SpawnRule RuleA, RuleB, RuleS;
            public FishingZoneDef Zone;
            public WeatherTableDef Weather;
            public EcologyTuningDef Hotspots;
            public SpotDef SpotA, SpotB;
            public ArtPaletteDef Palette;
            public SfxCueTableDef Cues;
            public FishingTuningDef Tuning;
            public List<SubAreaBand> Bands;
            public List<JunkWeight> Junk;
            public RodDef[] Rods;
            public BaitDef[] Baits;
            public FightProfileDef[] Profiles;
            public JunkDef[] JunkDefs;
            public List<FishSpeciesDef> Species;

            public void Rezone() { Zone.Configure("zone_a", "Zone A", Bands, new List<SpawnRule> { RuleA, RuleB, RuleS }, Junk, Weather, null); }
        }

        private static SpawnRule Rule(FishSpeciesDef fish, string[] ids, float weight, byte rod = 0, bool special = false, float chance = 0f, int limit = -1)
        {
            return new SpawnRule
            {
                fish = fish, subAreaIds = ids, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = weight, minRodTier = rod,
                special = special, specialChance = chance, catchLimitPerRun = limit,
            };
        }

        private static void FillSpecies(FishSpeciesDef d, string id, int minG, int maxG, int value, FightProfileDef fight, bool special)
        {
            d.Configure(id, id, special ? FishRarity.Legendary : FishRarity.Common, value, minG, maxG, 2.2f, 1f, fight, BodyShape.Round, Color.blue, Color.white,
                1f, 0f, special, null);
        }

        private Rig MakeRig()
        {
            var r = new Rig();
            RodDef twig = Make<RodDef>();
            twig.Configure("rod_twig", "Twig", 0, 14f, 0.9f, 0.8f, 0.5f, 0.5f, 0.9f, 0, 0);
            RodDef deep = Make<RodDef>();
            deep.Configure("rod_deepsea", "Deep", 3, 30f, 1.2f, 1.6f, 0.7f, 0.7f, 1.1f, 600, 4);
            r.Rods = new[] { twig, deep };
            BaitDef bare = Make<BaitDef>();
            bare.Configure("bait_bare_hook", "Bare Hook", 1, 1, 1, 1.3f, 1.5f, 1, true);
            r.Baits = new[] { bare };
            FightProfileDef steady = Make<FightProfileDef>();
            steady.Configure("steady", "Steady", 1f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0.6f, 0f, 0f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            r.Profiles = new[] { steady };

            r.FishA = Make<FishSpeciesDef>(); FillSpecies(r.FishA, "fish_a", 200, 800, 5, steady, false);
            r.FishB = Make<FishSpeciesDef>(); FillSpecies(r.FishB, "fish_b", 300, 900, 6, steady, false);
            r.FishS = Make<FishSpeciesDef>(); FillSpecies(r.FishS, "fish_s", 3000, 9000, 200, steady, true);
            r.Species = new List<FishSpeciesDef> { r.FishA, r.FishB, r.FishS };
            JunkDef boot = Make<JunkDef>(); boot.Configure("old_boot", "Old Boot", 2, BodyShape.Long, Color.gray);
            JunkDef can = Make<JunkDef>(); can.Configure("tin_can", "Tin Can", 2, BodyShape.Round, Color.gray);
            r.JunkDefs = new[] { boot, can };

            r.Tuning = Make<FishingTuningDef>();
            FishingTuning t = FishingTestData.Tuning();
            r.Tuning.Configure(in t);

            r.Palette = Make<ArtPaletteDef>();
            SetPalette(r, ValidSunMoon(), ValidLooks());
            r.Cues = Make<SfxCueTableDef>();
            SetAmbience(r.Cues, ValidAmbience());

            r.Weather = Make<WeatherTableDef>();
            r.Weather.Configure("weather_temperate", new float[] { 0.6f, 0.25f, 0.15f, 0.4f, 0.5f, 0.1f, 0.4f, 0.2f, 0.4f },
                new float[] { 1, 1.5f, 1, 1, 1, 0.3f, 1, 1, 1, 1, 1, 1.3f });
            r.Hotspots = Make<EcologyTuningDef>();
            r.Hotspots.Configure(60, 120, 60, 120, 45, 60, 3f);

            r.Bands = new List<SubAreaBand>
            {
                new SubAreaBand { id = "near", minShoreM = 0f, maxShoreM = 5f, junkChance = 0.2f },
                new SubAreaBand { id = "far", minShoreM = 5f, maxShoreM = 0f, junkChance = 0.1f },
            };
            r.Junk = new List<JunkWeight> { new JunkWeight { junk = boot, weight = 1f }, new JunkWeight { junk = can, weight = 1f } };
            r.RuleA = Rule(r.FishA, new[] { "near", "far" }, 10f);
            r.RuleB = Rule(r.FishB, new[] { "far" }, 5f, rod: 1);
            r.RuleS = Rule(r.FishS, new[] { "far" }, 0f, special: true, chance: 0.02f, limit: 1);
            r.Zone = Make<FishingZoneDef>();
            r.Rezone();

            r.SpotA = Make<SpotDef>(); r.SpotA.Configure("spot_a", "Spot A", "Spot_A", 0, r.Zone);
            r.SpotB = Make<SpotDef>(); r.SpotB.Configure("spot_b", "Spot B", "Spot_B", 1, r.Zone);
            r.Registry = Make<ContentRegistry>();
            r.Registry.Configure(2, new[] { r.SpotA, r.SpotB });
            Reconfigure(r);
            r.Registry.ConfigureEcology(new[] { r.Zone }, r.Hotspots);
            return r;
        }

        private void Reconfigure(Rig r)
        {
            r.Registry.ConfigureFishing(r.Rods, r.Baits, r.Profiles, r.Species, r.JunkDefs, r.Tuning, r.Cues, r.Palette, Make<WaterSettingsDef>());
        }

        private static SunMoonSettings ValidSunMoon()
        {
            return new SunMoonSettings
            {
                sunriseFraction = 0f, sunsetFraction = 0.7f, minElevationDeg = 12f, flipWindow = 0.02f, moonIntensity = 0.25f,
                moonEulerDeg = new Vector3(35, 150, 0), moonTint = Color.white, blendWindow = 0.05f,
            };
        }

        private static WeatherAmbient[] ValidLooks()
        {
            return new[]
            {
                new WeatherAmbient { fogDensityMult = 1f, fogDensityMin = 0f, lightMult = 1f, desaturate = 0f, rainAmount = 0f },
                new WeatherAmbient { fogDensityMult = 1.2f, fogDensityMin = 0f, lightMult = 0.85f, desaturate = 0.1f, rainAmount = 1f },
                new WeatherAmbient { fogDensityMult = 1f, fogDensityMin = 0.03f, lightMult = 0.9f, desaturate = 0.15f, rainAmount = 0f },
            };
        }

        private static SeasonAmbience[] ValidAmbience()
        {
            return new[]
            {
                new SeasonAmbience { windGain = 1.1f, windLfoHz = 0.08f, lapIntervalMin = 0.6f, lapIntervalMax = 1.4f },
                new SeasonAmbience { windGain = 1f, windLfoHz = 0f, lapIntervalMin = 0.6f, lapIntervalMax = 1.4f },
                new SeasonAmbience { windGain = 1.3f, windLfoHz = 0.12f, lapIntervalMin = 0.5f, lapIntervalMax = 1.2f },
                new SeasonAmbience { windGain = 1.5f, windLfoHz = 0.15f, lapIntervalMin = 1f, lapIntervalMax = 2f },
            };
        }

        private static void SetPalette(Rig r, SunMoonSettings sun, WeatherAmbient[] looks)
        {
            r.Palette.ConfigureM3(in sun, looks);
        }

        private static void SetAmbience(SfxCueTableDef cues, SeasonAmbience[] values)
        {
            var so = new SerializedObject(cues);
            SerializedProperty arr = so.FindProperty("seasonAmbience");
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("windGain").floatValue = values[i].windGain;
                e.FindPropertyRelative("windLfoHz").floatValue = values[i].windLfoHz;
                e.FindPropertyRelative("lapIntervalMin").floatValue = values[i].lapIntervalMin;
                e.FindPropertyRelative("lapIntervalMax").floatValue = values[i].lapIntervalMax;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<SubAreaBand> NineBands()
        {
            var bands = new List<SubAreaBand> { new SubAreaBand { id = "near", minShoreM = 0f, maxShoreM = 1f, junkChance = 0.1f } };
            for (int i = 1; i <= 7; i++) bands.Add(new SubAreaBand { id = "s" + i, minShoreM = i, maxShoreM = i + 1, junkChance = 0.1f });
            bands.Add(new SubAreaBand { id = "far", minShoreM = 8f, maxShoreM = 0f, junkChance = 0.1f });
            return bands;
        }

        // key -> (mutation, expected substring). A mutation breaks exactly one thing on an otherwise valid rig.
        private void Mutate(string key, Rig r)
        {
            switch (key)
            {
                case "duplicate_id": r.SpotB.Configure("spot_a", "Spot B", "Spot_B", 1, r.Zone); break;
                case "unresolved_reference":
                    r.RuleA.fish = Make<FishSpeciesDef>();
                    FillSpecies(r.RuleA.fish, "stray", 200, 800, 5, r.Profiles[0], false);
                    r.Rezone();
                    break;
                case "weight_min_not_below_max": FillSpecies(r.FishA, "fish_a", 800, 800, 5, r.Profiles[0], false); break;
                case "base_value_not_positive": FillSpecies(r.FishA, "fish_a", 200, 800, 0, r.Profiles[0], false); break;
                case "nonspecial_weight_not_positive": r.RuleA.weight = 0f; break;
                case "special_chance_zero": r.RuleS.specialChance = 0f; break;
                case "special_chance_above_half": r.RuleS.specialChance = 0.51f; break;
                case "special_chance_at_half": r.RuleS.specialChance = 0.5f; break;
                case "special_flag_mismatch_rule": r.RuleA.special = true; r.RuleA.specialChance = 0.1f; break;
                case "special_flag_mismatch_species": r.RuleS.special = false; r.RuleS.weight = 5f; break;
                case "subareas_not_contiguous_first": r.Bands[0] = new SubAreaBand { id = "near", minShoreM = 1f, maxShoreM = 5f, junkChance = 0.2f }; break;
                case "subareas_not_contiguous_gap": r.Bands[1] = new SubAreaBand { id = "far", minShoreM = 6f, maxShoreM = 0f, junkChance = 0.1f }; break;
                case "subareas_not_contiguous_closed": r.Bands[1] = new SubAreaBand { id = "far", minShoreM = 5f, maxShoreM = 20f, junkChance = 0.1f }; break;
                case "junk_chance_out_of_range_high": r.Bands[0] = new SubAreaBand { id = "near", minShoreM = 0f, maxShoreM = 5f, junkChance = 0.61f }; break;
                case "junk_chance_out_of_range_low": r.Bands[0] = new SubAreaBand { id = "near", minShoreM = 0f, maxShoreM = 5f, junkChance = -0.01f }; break;
                case "species_in_no_rule":
                    var extra = Make<FishSpeciesDef>();
                    FillSpecies(extra, "fish_x", 200, 800, 5, r.Profiles[0], false);
                    r.Species.Add(extra);
                    Reconfigure(r);
                    break;
                case "too_many_subareas":
                    r.Bands = NineBands();
                    break;
                case "mask_zero_seasons": r.RuleA.seasons = SeasonMask.None; break;
                case "mask_zero_weather": r.RuleA.weather = WeatherMask.None; break;
                case "mask_zero_bands": r.RuleA.bands = TimeBandMask.None; break;
                case "catch_limit_below_minus_one": r.RuleS.catchLimitPerRun = -2; break;
                case "weather_rows_invalid_zero": r.Weather.Configure("weather_temperate", new float[] { 0.6f, 0.25f, 0.15f, 0f, 0.5f, 0.1f, 0.4f, 0.2f, 0.4f }, SeasonMult()); break;
                case "weather_rows_invalid_nan": r.Weather.Configure("weather_temperate", new float[] { 0.6f, 0.25f, float.NaN, 0.4f, 0.5f, 0.1f, 0.4f, 0.2f, 0.4f }, SeasonMult()); break;
                case "weather_rows_invalid_season_zero":
                    var mult = SeasonMult(); mult[5] = 0f;
                    r.Weather.Configure("weather_temperate", new float[] { 0.6f, 0.25f, 0.15f, 0.4f, 0.5f, 0.1f, 0.4f, 0.2f, 0.4f }, mult);
                    break;
                case "junk_weight_not_positive": r.Junk[1] = new JunkWeight { junk = r.JunkDefs[1], weight = 0f }; break;
                case "cast_min_dist_mismatch":
                    FishingTuning t = FishingTestData.Tuning(); t.CastMinDist = 3f;
                    r.Tuning.Configure(in t);
                    break;
                case "min_rod_tier_above_rods": r.RuleB.minRodTier = 4; break;
                case "map_order_invalid_range": r.SpotB.Configure("spot_b", "Spot B", "Spot_B", 5, r.Zone); break;
                case "map_order_invalid_duplicate": r.SpotB.Configure("spot_b", "Spot B", "Spot_B", 0, r.Zone); break;
                case "hotspot_tuning_invalid_order": r.Hotspots.Configure(130, 120, 60, 120, 45, 60, 3f); break;
                case "hotspot_tuning_invalid_radius": r.Hotspots.Configure(60, 120, 60, 120, 45, 60, 0f); break;
                case "sun_moon_invalid": { SunMoonSettings s = ValidSunMoon(); s.sunriseFraction = 0.8f; SetPalette(r, s, ValidLooks()); break; }
                case "weather_looks_invalid_count": SetPalette(r, ValidSunMoon(), ValidLooks().Take(2).ToArray()); break;
                case "weather_looks_invalid_fog": { WeatherAmbient[] l = ValidLooks(); l[2].fogDensityMin = 0.04f; SetPalette(r, ValidSunMoon(), l); break; }
                case "season_ambience_invalid_count": SetAmbience(r.Cues, ValidAmbience().Take(3).ToArray()); break;
                case "season_ambience_invalid_lap": { SeasonAmbience[] a = ValidAmbience(); a[1].lapIntervalMax = 0.5f; SetAmbience(r.Cues, a); break; }
                default: Assert.Fail("unknown case " + key); break;
            }
            if (key == "too_many_subareas" || key.StartsWith("subareas_") || key.StartsWith("junk_chance") || key == "junk_weight_not_positive" ||
                key.StartsWith("nonspecial") || key.StartsWith("special_") || key.StartsWith("mask_zero") || key == "catch_limit_below_minus_one" ||
                key == "min_rod_tier_above_rods")
            {
                r.Rezone();
            }
        }

        private static float[] SeasonMult() { return new float[] { 1, 1.5f, 1, 1, 1, 0.3f, 1, 1, 1, 1, 1, 1.3f }; }

        // Catches: a missing rule; a boundary off by one (0.5 passes, 0.51 fails); a rule reported twice or by two messages.
        [TestCase("duplicate_id", "registry invalid")]
        [TestCase("unresolved_reference", "registry invalid")]
        [TestCase("weight_min_not_below_max", "weightMinG")]
        [TestCase("base_value_not_positive", "baseValue")]
        [TestCase("nonspecial_weight_not_positive", "must be > 0 for a non-special rule")]
        [TestCase("special_chance_zero", "specialChance = 0 outside (0, 0.5]")]
        [TestCase("special_chance_above_half", "specialChance = 0.51 outside (0, 0.5]")]
        [TestCase("special_chance_at_half", null)]
        [TestCase("special_flag_mismatch_rule", "the flags must match")]
        [TestCase("special_flag_mismatch_species", "the flags must match")]
        [TestCase("subareas_not_contiguous_first", "contiguous from 0")]
        [TestCase("subareas_not_contiguous_gap", "contiguous")]
        [TestCase("subareas_not_contiguous_closed", "contiguous to infinity")]
        [TestCase("junk_chance_out_of_range_high", "junkChance = 0.61 outside [0, 0.6]")]
        [TestCase("junk_chance_out_of_range_low", "junkChance = -0.01 outside [0, 0.6]")]
        [TestCase("species_in_no_rule", "is in no spawn rule of any zone")]
        [TestCase("too_many_subareas", "sub-area count = 9 outside [1, 8]")]
        [TestCase("mask_zero_seasons", "seasons mask is empty")]
        [TestCase("mask_zero_weather", "weather mask is empty")]
        [TestCase("mask_zero_bands", "bands mask is empty")]
        [TestCase("catch_limit_below_minus_one", "catchLimitPerRun = -2 must be >= -1")]
        [TestCase("weather_rows_invalid_zero", "baseMatrix[3] = 0 must be finite and > 0")]
        [TestCase("weather_rows_invalid_nan", "baseMatrix[2] = NaN must be finite and > 0")]
        [TestCase("weather_rows_invalid_season_zero", "seasonColumnMult[5] = 0 must be finite and > 0")]
        [TestCase("junk_weight_not_positive", "weight = 0 must be finite and > 0")]
        [TestCase("cast_min_dist_mismatch", "castMinDist = 3 must equal 2.5")]
        [TestCase("min_rod_tier_above_rods", "minRodTier = 4 is above the highest rod tier 3")]
        [TestCase("map_order_invalid_range", "mapOrder = 5 outside [0, 4]")]
        [TestCase("map_order_invalid_duplicate", "mapOrder = 0 is also used by spot spot_a")]
        [TestCase("hotspot_tuning_invalid_order", "hotspotFirstMinMin = 130 must be <= hotspotFirstMinMax = 120")]
        [TestCase("hotspot_tuning_invalid_radius", "hotspotRadiusM = 0 outside (0, 10]")]
        [TestCase("sun_moon_invalid", "sunriseFraction = 0.8 must be < sunsetFraction = 0.7")]
        [TestCase("weather_looks_invalid_count", "weatherLooks must have exactly 3 entries (has 2)")]
        [TestCase("weather_looks_invalid_fog", "fogDensityMin = 0.04 outside [0, 0.0333]")]
        [TestCase("season_ambience_invalid_count", "seasonAmbience must have exactly 4 entries (has 3)")]
        [TestCase("season_ambience_invalid_lap", "lapIntervalMax = 0.5 outside [lapIntervalMin = 0.6, 10]")]
        public void EcologyValidators_DataRules_ReportEachViolation(string key, string expected)
        {
            Rig r = MakeRig();
            Assert.That(r.Registry.Validate(), Is.Empty, "the rig is a valid registry");
            Assert.That(EcologyValidators.DataRules(r.Registry), Is.Empty, "the rig passes every data rule");

            Mutate(key, r);
            List<string> messages = EcologyValidators.DataRules(r.Registry);
            if (expected == null)
            {
                Assert.That(messages, Is.Empty, key + ": this value is valid");
                return;
            }
            Assert.That(messages.Count, Is.EqualTo(1), key + " -> " + string.Join(" | ", messages));
            StringAssert.Contains(expected, messages[0]);
            if (expected == "registry invalid")
            {
                // The specific text is the registry's own (no duplicate reporting).
                Assert.That(r.Registry.Validate().Count, Is.GreaterThan(0));
                StringAssert.Contains(key == "duplicate_id" ? "duplicate id" : "is not listed in species", r.Registry.Validate()[0]);
            }
        }

        // ---- Coverage fixtures ----------------------------------------------------------------------------------------------------------

        private const ushort A = EcologyCoreTestData.Bluegill, B = EcologyCoreTestData.MirrorMinnow, C = EcologyCoreTestData.SilverMullet, D = EcologyCoreTestData.SunnyTrout;
        private const TimeBandMask Night = TimeBandMask.Night;

        private static SpawnRuleData R(ushort species, byte mask, TimeBandMask bands, float weight, byte rod = 0, bool special = false,
            SeasonMask seasons = SeasonMask.All, WeatherMask weather = WeatherMask.All)
        {
            return EcologyCoreTestData.Rule(species, mask, seasons, weather, bands, weight, rod, special, special ? 0.02f : 0f, special ? 1 : -1);
        }

        private static SubAreaBandData[] TwoBands()
        {
            return new[]
            {
                new SubAreaBandData { MinShoreM = 0f, MaxShoreM = 5f, JunkChance = 0.2f },
                new SubAreaBandData { MinShoreM = 5f, MaxShoreM = float.PositiveInfinity, JunkChance = 0.1f },
            };
        }

        private static EcologyDatabase Db(SpawnRuleData[] rules, JunkEntry[] junk = null)
        {
            WeatherTableData w = EcologyCoreTestData.Weather();
            var zone = new ZoneData(TwoBands(), rules, junk ?? EcologyCoreTestData.LakeJunk(), in w, null);
            EcologyTuning t = EcologyCoreTestData.Tuning();
            return new EcologyDatabase(EcologyCoreTestData.Fishing(), new[] { zone }, new SpotEcologyData[0], in t);
        }

        private static List<string> V1(EcologyDatabase db)
        {
            return EcologyValidators.Coverage(db, 1, 0.5f).Where(m => m.StartsWith("V1")).ToList();
        }

        // Catches: ignoring sub-areas (a dead sub-area hidden behind a covered one), ignoring the rod tier, counting specials.
        [Test]
        public void EcologyValidators_Coverage_ReportsDeadSubArea()
        {
            const TimeBandMask NotNight = TimeBandMask.Dawn | TimeBandMask.Morning | TimeBandMask.Midday | TimeBandMask.Afternoon | TimeBandMask.Dusk;
            SpawnRuleData ruleA = R(A, 0b01, TimeBandMask.All, 10f);                  // species A: every band in sub-area 0 only
            SpawnRuleData ruleB = R(B, 0b10, NotNight, 10f);                          // species B: sub-area 1, every band except Night

            List<string> dead = V1(Db(new[] { ruleA, ruleB }));
            Assert.That(dead.Count, Is.EqualTo(12), "sub-area 1 at Night, 4 seasons x 3 weathers: " + string.Join(" | ", dead));
            Assert.That(dead.All(m => m.Contains(" 1 ") && m.Contains("Night")), Is.True);
            Assert.That(dead[0], Is.EqualTo("V1 dead sub-area: 0 1 Spring Night Clear"));

            Assert.That(V1(Db(new[] { ruleA, ruleB, R(B, 0b10, Night, 10f) })), Is.Empty, "a Night rule for sub-area 1 clears them");
            Assert.That(V1(Db(new[] { ruleA, ruleB, R(B, 0b10, Night, 10f, rod: 1) })).Count, Is.EqualTo(12), "a rod-1-only rule does not count at rod 0");
            Assert.That(V1(Db(new[] { ruleA, ruleB, R(B, 0b10, Night, 10f, special: true) })).Count, Is.EqualTo(12), "a special does not count");
        }

        private static List<string> V2(EcologyDatabase db)
        {
            return EcologyValidators.Coverage(db, 1, 0.5f).Where(m => m.StartsWith("V2")).ToList();
        }

        // Catches: pooled weights instead of the mean of per-sub-area shares; specials counted; a wrong dominance threshold.
        [Test]
        public void EcologyValidators_Variety_ReportsDominantSpecies()
        {
            // {A 30, B 5, C 5} in both sub-areas: A holds 75% in all 72 contexts (4 seasons x 6 bands x 3 weathers).
            List<string> dominant = V2(Db(new[] { R(A, 0, TimeBandMask.All, 30f), R(B, 0, TimeBandMask.All, 5f), R(C, 0, TimeBandMask.All, 5f) }));
            Assert.That(dominant.Count, Is.EqualTo(72));
            Assert.That(dominant.All(m => m.StartsWith("V2 dominant species:") && m.EndsWith(" 0 75.0%")), Is.True, dominant[0]);

            Assert.That(V2(Db(new[] { R(A, 0, TimeBandMask.All, 10f), R(B, 0, TimeBandMask.All, 10f), R(C, 0, TimeBandMask.All, 10f) })), Is.Empty);

            List<string> few = V2(Db(new[] { R(A, 0, TimeBandMask.All, 10f), R(B, 0, TimeBandMask.All, 10f) }));
            Assert.That(few.Count, Is.EqualTo(72));
            Assert.That(few[0], Does.StartWith("V2 fewer than 3 species:"));
            Assert.That(few[0], Does.EndWith("(2)"));

            // Mixed shares are the MEAN of the per-sub-area shares: sub-area 0 {A 1}, sub-area 1 {B 30, C 30, D 30} -> A 50.0% (pooled weights would say 1/91).
            EcologyDatabase mixed = Db(new[] { R(A, 0b01, TimeBandMask.All, 1f), R(B, 0b10, TimeBandMask.All, 30f), R(C, 0b10, TimeBandMask.All, 30f), R(D, 0b10, TimeBandMask.All, 30f) });
            Assert.That(V2(mixed), Is.Empty, "A holds exactly 50%, which is not above 50%");
            List<string> report = EcologyValidators.CoverageReport(mixed, 1, 0.5f, null, null, null).Where(l => l.StartsWith("FG ECO V2")).ToList();
            Assert.That(report.Count, Is.EqualTo(72));
            Assert.That(report[0], Is.EqualTo("FG ECO V2 0 Spring Dawn Clear species=4 top=" + A + " 50.0%"));

            // Variety (13.3): three species -> fewer than 4; Morning and Night sets equal -> differ by 0; every species on 3+ bands -> no narrow window.
            List<string> variety = EcologyValidators.Variety(Db(new[] { R(A, 0, TimeBandMask.All, 10f), R(B, 0, TimeBandMask.All, 10f), R(C, 0, TimeBandMask.All, 10f) }));
            Assert.That(variety.Count(m => m.StartsWith("Variety fewer than 4 species:")), Is.EqualTo(4));
            Assert.That(variety.Count(m => m.StartsWith("Variety Morning and Night differ by 0:")), Is.EqualTo(4));
            Assert.That(variety.Count(m => m.StartsWith("Variety no narrow-window species:")), Is.EqualTo(4));

            // A good zone: A (Da Mo), B (Mi Af), C (Du Ni), D (all bands): 4 species, narrow windows, Morning {A, D} vs Night {C, D} differ by 2.
            EcologyDatabase good = Db(new[]
            {
                R(A, 0, TimeBandMask.Dawn | TimeBandMask.Morning, 10f), R(B, 0, TimeBandMask.Midday | TimeBandMask.Afternoon, 10f),
                R(C, 0, TimeBandMask.Dusk | Night, 10f), R(D, 0, TimeBandMask.All, 10f),
            });
            Assert.That(EcologyValidators.Variety(good), Is.Empty);

            // Night (5) and Dawn (0) are NOT adjacent: a Night + Dawn species is not a narrow window by itself.
            EcologyDatabase wrap = Db(new[]
            {
                R(A, 0, Night | TimeBandMask.Dawn, 10f), R(B, 0, TimeBandMask.All, 10f), R(C, 0, TimeBandMask.All, 10f), R(D, 0, TimeBandMask.All, 10f),
            });
            Assert.That(EcologyValidators.Variety(wrap).Any(m => m.StartsWith("Variety no narrow-window species:")), Is.True);
        }

        // Catches: the resolver's trust in sorted rules and positive junk weights (FishingResolver.JunkItem silently returns junk[0] otherwise).
        [Test]
        public void EcologyValidators_Coverage_ReportsUnsortedRulesAndBadJunkWeights()
        {
            var sorted = new[] { R(A, 0, TimeBandMask.All, 10f), R(B, 0, TimeBandMask.All, 10f) };
            Assert.That(EcologyValidators.Coverage(Db(sorted), 1, 0.5f).Where(m => m.StartsWith("Rules not sorted") || m.StartsWith("Junk weight")), Is.Empty);

            var unsorted = new[] { R(B, 0, TimeBandMask.All, 10f), R(A, 0, TimeBandMask.All, 10f) };
            List<string> msgs = EcologyValidators.Coverage(Db(unsorted), 1, 0.5f);
            Assert.That(msgs.Count(m => m == "Rules not sorted by species: 0 rule 1"), Is.EqualTo(1));

            JunkEntry[] junk = EcologyCoreTestData.LakeJunk();
            junk[2].Weight = 0f;
            List<string> junkMsgs = EcologyValidators.Coverage(Db(sorted, junk), 1, 0.5f);
            Assert.That(junkMsgs.Count(m => m == "Junk weight not positive: 0 junk 2 (0)"), Is.EqualTo(1));
        }

        // Catches: V3 skipping specials or the rod-3 context (a rod-2-only species must pass).
        [Test]
        public void EcologyValidators_Coverage_V3ReportsUnreachableSpecies()
        {
            // 7 species in the fixture; only A (rod 3 needed) and a special are in rules, the other five are unreachable.
            var rules = new[] { R(A, 0, TimeBandMask.All, 10f, rod: 3), R(EcologyCoreTestData.GildedCarp, 0b10, TimeBandMask.All, 0f, special: true) };
            List<string> v3 = EcologyValidators.Coverage(Db(rules), 1, 0.5f).Where(m => m.StartsWith("V3")).ToList();
            Assert.That(v3.Count, Is.EqualTo(5), string.Join(" | ", v3));
            Assert.That(v3.Contains("V3 unreachable species: " + A), Is.False, "a rod-3 rule is reached by the rod-3 context");
            Assert.That(v3.Contains("V3 unreachable species: " + EcologyCoreTestData.GildedCarp), Is.False, "specials are included in V3");
            Assert.That(v3.Contains("V3 unreachable species: " + B), Is.True);
        }
    }
}
