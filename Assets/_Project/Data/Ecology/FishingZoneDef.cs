using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One fishing zone (spec 12.3): sub-areas by shore distance, spawn rules, a junk list, a weather
    // table and the baked shore grid. A SpotDef points at its zone. No tuning initialisers: values come from EcologySeedData.
    // Owned by T9 (M3).
    public sealed class FishingZoneDef : ScriptableObject
    {
        private static readonly SubAreaBand[] NoBands = new SubAreaBand[0];
        private static readonly SpawnRule[] NoRules = new SpawnRule[0];
        private static readonly JunkWeight[] NoJunk = new JunkWeight[0];

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<SubAreaBand> subAreas = new List<SubAreaBand>();
        [SerializeField] private List<SpawnRule> rules = new List<SpawnRule>();
        [SerializeField] private List<JunkWeight> junk = new List<JunkWeight>();
        [SerializeField] private WeatherTableDef weather;
        [SerializeField] private ShoreGridDef shoreGrid;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<SubAreaBand> SubAreas => subAreas;
        public IReadOnlyList<SpawnRule> Rules => rules;
        public IReadOnlyList<JunkWeight> Junk => junk;
        public WeatherTableDef Weather => weather;
        public ShoreGridDef ShoreGrid => shoreGrid;

        // Used by the content builder and by tests. The lists are copied (the SpawnRule objects are shared); null reads as empty.
        public void Configure(string id, string displayName, IEnumerable<SubAreaBand> subAreas, IEnumerable<SpawnRule> rules,
            IEnumerable<JunkWeight> junk, WeatherTableDef weather, ShoreGridDef shoreGrid)
        {
            this.id = id;
            this.displayName = displayName;
            this.subAreas = subAreas == null ? new List<SubAreaBand>() : new List<SubAreaBand>(subAreas);
            this.rules = rules == null ? new List<SpawnRule>() : new List<SpawnRule>(rules);
            this.junk = junk == null ? new List<JunkWeight>() : new List<JunkWeight>(junk);
            this.weather = weather;
            this.shoreGrid = shoreGrid;
        }

        // The Core zone, with registry indices. Rules are STABLY sorted by fish id (ordinal) because the resolver trusts the array order
        // (spec 12.3, M3-D43). The last band with maxShoreM <= 0 becomes open-ended. ArgumentException for a rule naming an unknown
        // sub-area; ZoneData's own ArgumentExceptions pass through. Call only on a built registry (it reads SpeciesIndex/JunkIndex).
        public ZoneData ToCore(ContentRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (weather == null) throw new ArgumentException("zone '" + id + "' has no weather table.");

            var bands = new SubAreaBandData[subAreas.Count];
            for (int i = 0; i < bands.Length; i++) bands[i] = subAreas[i].ToCore();
            if (bands.Length > 0 && bands[bands.Length - 1].MaxShoreM <= 0f) bands[bands.Length - 1].MaxShoreM = float.PositiveInfinity;

            var order = new List<KeyValuePair<int, SpawnRule>>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null || rules[i].fish == null) throw new ArgumentException("zone '" + id + "' rule " + i + " has no fish.");
                order.Add(new KeyValuePair<int, SpawnRule>(i, rules[i]));
            }
            // Sort (rule, original index) pairs: List.Sort is unstable, so the index breaks ties.
            order.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Value.fish.Id, b.Value.fish.Id);
                return c != 0 ? c : a.Key.CompareTo(b.Key);
            });

            var ruleData = new SpawnRuleData[order.Count];
            for (int r = 0; r < ruleData.Length; r++)
            {
                SpawnRule rule = order[r].Value;
                ruleData[r] = new SpawnRuleData
                {
                    SpeciesIndex = registry.SpeciesIndex(rule.fish.Id),
                    SubAreaMask = SubAreaMaskOf(rule),
                    Seasons = rule.seasons,
                    Weather = rule.weather,
                    Bands = rule.bands,
                    Weight = rule.weight,
                    MinRodTier = rule.minRodTier,
                    MinBaitRating = rule.minBaitRating,
                    MinDepth = rule.minDepth,
                    Special = rule.special,
                    SpecialChance = rule.specialChance,
                    CatchLimitPerRun = rule.catchLimitPerRun,
                    MinPlayersInSession = rule.minPlayersInSession,
                };
            }

            var junkData = new JunkEntry[junk.Count];
            for (int j = 0; j < junkData.Length; j++)
            {
                if (junk[j].junk == null) throw new ArgumentException("zone '" + id + "' junk " + j + " has no junk item.");
                junkData[j] = new JunkEntry { JunkIndex = registry.JunkIndex(junk[j].junk.Id), Weight = junk[j].weight };
            }

            WeatherTableData table = weather.ToCore();
            return new ZoneData(bands, ruleData, junkData, in table, shoreGrid != null ? shoreGrid.ToCore() : null);
        }

        private byte SubAreaMaskOf(SpawnRule rule)
        {
            if (rule.subAreaIds == null || rule.subAreaIds.Length == 0) return 0;
            int mask = 0;
            for (int k = 0; k < rule.subAreaIds.Length; k++)
            {
                int found = -1;
                for (int i = 0; i < subAreas.Count; i++)
                {
                    if (string.Equals(subAreas[i].id, rule.subAreaIds[k], StringComparison.Ordinal)) { found = i; break; }
                }
                if (found < 0)
                {
                    throw new ArgumentException("zone '" + id + "' rule '" + rule.fish.Id + "' names unknown sub-area '" + rule.subAreaIds[k] + "'");
                }
                mask |= 1 << found;
            }
            return (byte)mask;
        }
    }
}
