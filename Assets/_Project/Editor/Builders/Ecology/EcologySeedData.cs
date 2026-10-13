// The single M3 ecology seed source (C 2.8 #22): spec 12.2, 12.5, 12.6, 12.7 and plan data-coverage section 1. Used only to create missing assets and by tests.
// Owned by T10; T11/T12 update retuned values. The 25 species and the zone rules live in EcologySeedData.Roster.cs.
// Static initialisation: this class reads FishingSeedData.Profiles, so FishingSeedData's static constructor must never touch this class
// (FishingSeedData.Species/Junk forward here lazily, through getters).
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.EditorTools
{
    // One spawn rule of a zone seed (spec 12.3). minBaitRating, minDepth and minPlayersInSession are 0 for every M3 rule and are written as 0
    // by the builder, so they are not carried here.
    public readonly struct RuleSeed
    {
        public readonly string SpeciesId;
        public readonly string[] SubAreaIds;
        public readonly SeasonMask Seasons;
        public readonly WeatherMask Weather;
        public readonly TimeBandMask Bands;
        public readonly float Weight;
        public readonly byte MinRodTier;
        public readonly bool Special;
        public readonly float SpecialChance;
        public readonly int CatchLimitPerRun;

        public RuleSeed(string speciesId, string[] subAreaIds, SeasonMask seasons, WeatherMask weather, TimeBandMask bands, float weight,
            byte minRodTier, bool special, float specialChance, int catchLimitPerRun)
        {
            SpeciesId = speciesId;
            SubAreaIds = subAreaIds;
            Seasons = seasons;
            Weather = weather;
            Bands = bands;
            Weight = weight;
            MinRodTier = minRodTier;
            Special = special;
            SpecialChance = specialChance;
            CatchLimitPerRun = catchLimitPerRun;
        }
    }

    // One zone seed: sub-areas (last MaxShoreM = 0 = open-ended, exactly as the asset stores it), junk list, rules in roster row order.
    public readonly struct ZoneSeed
    {
        public readonly string Id, Name;
        public readonly SubAreaBandData[] Bands;
        public readonly string[] BandIds;
        public readonly (string junkId, float weight)[] Junk;
        public readonly RuleSeed[] Rules;

        public ZoneSeed(string id, string name, SubAreaBandData[] bands, string[] bandIds, (string junkId, float weight)[] junk, RuleSeed[] rules)
        {
            Id = id;
            Name = name;
            Bands = bands;
            BandIds = bandIds;
            Junk = junk;
            Rules = rules;
        }
    }

    public static partial class EcologySeedData
    {
        private static readonly ReadOnlyCollection<SpeciesSeed> s_species;
        private static readonly ReadOnlyCollection<(string id, string name, int value, BodyShape shape)> s_junk;
        private static readonly ReadOnlyCollection<ZoneSeed> s_zones;
        private static readonly WeatherTableData s_weather;
        private static readonly EcologyTuning s_tuning;
        private static readonly SunMoonSettings s_sunMoon;

        static EcologySeedData()
        {
            // Junk (plan 1.2, M3-D34): list order = table order.
            s_junk = new ReadOnlyCollection<(string, string, int, BodyShape)>(new List<(string id, string name, int value, BodyShape shape)>
            {
                ("old_boot", "Old Boot", 2, BodyShape.Long),
                ("tin_can", "Tin Can", 2, BodyShape.Round),
                ("soggy_newspaper", "Soggy Newspaper", 0, BodyShape.Flat),
                ("lost_sock", "Lost Sock", 1, BodyShape.Long),
                ("rope_scrap", "Rope Scrap", 1, BodyShape.Long),
                ("seaweed_clump", "Seaweed Clump", 0, BodyShape.Round),
                ("empty_bait_tub", "Empty Bait Tub", 2, BodyShape.Round),
            });

            // Species and the per-zone rules (the roster, spec 12.6).
            BuildRoster(out List<SpeciesSeed> species, out List<RuleSeed> lakeRules, out List<RuleSeed> harbourRules);
            s_species = new ReadOnlyCollection<SpeciesSeed>(species);

            // Zones (spec 12.5 sub-areas, 12.6 junk lists with equal weights). Last band MaxShoreM 0 = open-ended.
            s_zones = new ReadOnlyCollection<ZoneSeed>(new List<ZoneSeed>
            {
                new ZoneSeed("lake_dock", "Lake Dock",
                    new[]
                    {
                        new SubAreaBandData { MinShoreM = 0f, MaxShoreM = 5f, JunkChance = 0.20f },
                        new SubAreaBandData { MinShoreM = 5f, MaxShoreM = 10f, JunkChance = 0.12f },
                        new SubAreaBandData { MinShoreM = 10f, MaxShoreM = 0f, JunkChance = 0.10f },
                    },
                    new[] { "reeds", "open_water", "deep_water" },
                    new[] { ("old_boot", 1f), ("tin_can", 1f), ("soggy_newspaper", 1f), ("lost_sock", 1f) },
                    lakeRules.ToArray()),
                new ZoneSeed("harbour_pier", "Harbour Pier",
                    new[]
                    {
                        new SubAreaBandData { MinShoreM = 0f, MaxShoreM = 5f, JunkChance = 0.20f },
                        new SubAreaBandData { MinShoreM = 5f, MaxShoreM = 12f, JunkChance = 0.12f },
                        new SubAreaBandData { MinShoreM = 12f, MaxShoreM = 0f, JunkChance = 0.10f },
                    },
                    new[] { "shallows", "channel", "open_sea" },
                    new[] { ("old_boot", 1f), ("rope_scrap", 1f), ("seaweed_clump", 1f), ("empty_bait_tub", 1f) },
                    harbourRules.ToArray()),
            });

            // Weather table (spec 12.2): rows current Clear/Rain/Fog, columns next; season column multipliers (season * 3 + column).
            s_weather = new WeatherTableData
            {
                Base = new[] { 0.60f, 0.25f, 0.15f, 0.40f, 0.50f, 0.10f, 0.40f, 0.20f, 0.40f },
                SeasonColumnMult = new[]
                {
                    1f, 1.5f, 1f,    // Spring: Rain x1.5
                    1f, 1f, 0.3f,    // Summer: Fog x0.3
                    1f, 1f, 1f,      // Autumn: none
                    1f, 1f, 1.3f,    // Winter: Fog x1.3
                },
            };

            // Hotspots (spec 12.7, M3-D20): minutes and metres.
            s_tuning = new EcologyTuning
            {
                HotspotFirstMinMin = 60, HotspotFirstMinMax = 120, HotspotGapMin = 60, HotspotGapMax = 120,
                HotspotDurationMin = 45, HotspotDurationMax = 60, HotspotRadiusM = 3f,
            };

            // Sun and moon (plan 1.3). The lake sun is Euler(50, -30, 0); the moon comes from the opposite azimuth, lower.
            s_sunMoon = new SunMoonSettings
            {
                sunriseFraction = 0f, sunsetFraction = 0.70f, minElevationDeg = 12f, flipWindow = 0.02f, moonIntensity = 0.25f,
                moonEulerDeg = new Vector3(35f, 150f, 0f), moonTint = new Color(0.62f, 0.70f, 0.92f, 1f), blendWindow = 0.05f,
            };
        }

        public static IReadOnlyList<SpeciesSeed> Species { get { return s_species; } }

        public static IReadOnlyList<(string id, string name, int value, BodyShape shape)> Junk { get { return s_junk; } }

        public static IReadOnlyList<ZoneSeed> Zones { get { return s_zones; } }

        public static WeatherTableData Weather { get { return s_weather; } }

        public static EcologyTuning Tuning { get { return s_tuning; } }

        public static SunMoonSettings SunMoon { get { return s_sunMoon; } }

        // Index Clear 0, Rain 1, Fog 2 (plan 1.4). A new array per call.
        public static WeatherAmbient[] WeatherLooks
        {
            get
            {
                return new[]
                {
                    new WeatherAmbient { fogDensityMult = 1f, fogDensityMin = 0f, lightMult = 1f, desaturate = 0f, rainAmount = 0f },
                    new WeatherAmbient { fogDensityMult = 1.2f, fogDensityMin = 0f, lightMult = 0.85f, desaturate = 0.1f, rainAmount = 1f },
                    new WeatherAmbient { fogDensityMult = 1f, fogDensityMin = 0.03f, lightMult = 0.9f, desaturate = 0.15f, rainAmount = 0f },
                };
            }
        }

        // Index = Season (plan 1.5). Summer reproduces today's AmbienceBed exactly. A new array per call.
        public static SeasonAmbience[] SeasonAmbience
        {
            get
            {
                return new[]
                {
                    new SeasonAmbience { windGain = 1.1f, windLfoHz = 0.08f, lapIntervalMin = 0.6f, lapIntervalMax = 1.4f },   // Spring
                    new SeasonAmbience { windGain = 1.0f, windLfoHz = 0f, lapIntervalMin = 0.6f, lapIntervalMax = 1.4f },      // Summer
                    new SeasonAmbience { windGain = 1.3f, windLfoHz = 0.12f, lapIntervalMin = 0.5f, lapIntervalMax = 1.2f },   // Autumn
                    new SeasonAmbience { windGain = 1.5f, windLfoHz = 0.15f, lapIntervalMin = 1.0f, lapIntervalMax = 2.0f },   // Winter
                };
            }
        }

        // Junk colours (plan 1.2); the public Junk tuple carries no colour. old_boot and tin_can keep their M2 values exactly.
        internal static Color JunkColor(string id)
        {
            switch (id)
            {
                case "old_boot": return new Color(0.30f, 0.22f, 0.16f, 1f);
                case "tin_can": return new Color(0.68f, 0.70f, 0.72f, 1f);
                case "soggy_newspaper": return new Color(0.82f, 0.80f, 0.70f, 1f);
                case "lost_sock": return new Color(0.86f, 0.30f, 0.36f, 1f);
                case "rope_scrap": return new Color(0.72f, 0.60f, 0.40f, 1f);
                case "seaweed_clump": return new Color(0.22f, 0.42f, 0.22f, 1f);
                case "empty_bait_tub": return new Color(0.30f, 0.52f, 0.86f, 1f);
                default: throw new ArgumentException("no junk seed '" + id + "'", nameof(id));
            }
        }

        // Test convenience: the Core ecology database straight from the seeds (grids null). Zones and spots are in id order like ContentRegistry;
        // rules are stably sorted by species id (ordinal), sub-area ids become the mask, indices follow the sorted species and junk ids.
        public static EcologyDatabase BuildDatabase()
        {
            FishingDatabase fishing = FishingSeedData.BuildDatabase();

            var speciesIds = new List<string>();
            foreach (SpeciesSeed s in s_species) speciesIds.Add(s.Id);
            speciesIds.Sort(StringComparer.Ordinal);
            var junkIds = new List<string>();
            foreach (var j in s_junk) junkIds.Add(j.id);
            junkIds.Sort(StringComparer.Ordinal);

            var zoneList = new List<ZoneSeed>(s_zones);
            zoneList.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var zones = new ZoneData[zoneList.Count];
            for (int z = 0; z < zones.Length; z++)
            {
                ZoneSeed seed = zoneList[z];
                var bands = (SubAreaBandData[])seed.Bands.Clone();
                if (bands[bands.Length - 1].MaxShoreM <= 0f) bands[bands.Length - 1].MaxShoreM = float.PositiveInfinity;

                var order = new List<KeyValuePair<int, RuleSeed>>();
                for (int i = 0; i < seed.Rules.Length; i++) order.Add(new KeyValuePair<int, RuleSeed>(i, seed.Rules[i]));
                order.Sort((a, b) =>
                {
                    int c = string.CompareOrdinal(a.Value.SpeciesId, b.Value.SpeciesId);
                    return c != 0 ? c : a.Key.CompareTo(b.Key);
                });
                var rules = new SpawnRuleData[order.Count];
                for (int r = 0; r < rules.Length; r++)
                {
                    RuleSeed rs = order[r].Value;
                    int mask = 0;
                    if (rs.SubAreaIds != null)
                    {
                        foreach (string id in rs.SubAreaIds)
                        {
                            int bit = Array.IndexOf(seed.BandIds, id);
                            if (bit < 0) throw new InvalidOperationException("rule '" + rs.SpeciesId + "' names unknown sub-area '" + id + "'");
                            mask |= 1 << bit;
                        }
                    }
                    rules[r] = new SpawnRuleData
                    {
                        SpeciesIndex = (ushort)speciesIds.IndexOf(rs.SpeciesId), SubAreaMask = (byte)mask, Seasons = rs.Seasons, Weather = rs.Weather,
                        Bands = rs.Bands, Weight = rs.Weight, MinRodTier = rs.MinRodTier, Special = rs.Special, SpecialChance = rs.SpecialChance,
                        CatchLimitPerRun = rs.CatchLimitPerRun,
                    };
                }

                var junk = new JunkEntry[seed.Junk.Length];
                for (int j = 0; j < junk.Length; j++)
                    junk[j] = new JunkEntry { JunkIndex = (ushort)junkIds.IndexOf(seed.Junk[j].junkId), Weight = seed.Junk[j].weight };
                zones[z] = new ZoneData(bands, rules, junk, in s_weather, null);
            }

            // Spots in id order: harbour_pier (map 1), lake_dock (map 0).
            var spots = new[]
            {
                new SpotEcologyData { ZoneIndex = zoneList.FindIndex(s => s.Id == "harbour_pier"), MapOrder = 1 },
                new SpotEcologyData { ZoneIndex = zoneList.FindIndex(s => s.Id == "lake_dock"), MapOrder = 0 },
            };
            EcologyTuning tuning = s_tuning;
            return new EcologyDatabase(fishing, zones, spots, in tuning);
        }
    }
}
