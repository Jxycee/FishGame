// Ecology validators (spec 12.9, contract 7.11 / 11.1 / 11.2; plan data-coverage section 4). Owned by T11 (T16 appends MapChecks; append only).
//   DataRules: hard-fail data rules over the Defs (ranges, specials, contiguity, masks, weights, the cast-distance assumption).
//   Coverage:  V1 (no dead sub-area), V2 (variety: >= 3 species, none above 50%) and V3 (every species reachable at rod 3) over the Core
//              EcologyDatabase, plus two structural checks the resolver trusts (rules sorted by species, junk weights > 0).
//   Variety:   the 13.3 variety rules (M3-D65 / M3-D66 wording).
// All messages are InvariantCulture and ASCII. Reads the generated assets only; never the seeds (plan 0.4). Allocation is not a concern here.
using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class EcologyValidators
    {
        private const int BandCount = 6;
        private const float MaxFogMin = 0.0333f;          // DayNightMath.IsReadable: exp(-(d * 25)^2) >= 0.5 <=> d <= ~0.0333
        private const int V3Rod = 3, V3Rating = 10;
        private const float V3Depth = 6f;
        private static readonly string[] BandNames = { "Dawn", "Morning", "Midday", "Afternoon", "Dusk", "Night" };
        private static readonly WeatherMask[] Weathers = { WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };
        private static readonly string[] WeatherNames = { "Clear", "Rain", "Fog" };

        // ---- entry points ---------------------------------------------------------------------------------------------------------

        public static List<string> All()
        {
            var errors = new List<string>();
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
            {
                errors.Add("Ecology: ContentRegistry missing at " + AssetPaths.ContentRegistryAsset);
                return errors;
            }
            List<string> data = DataRules(registry);
            foreach (string e in data) errors.Add("Ecology: " + e);
            if (data.Count > 0) return errors;
            if (registry.Zones.Count == 0 || registry.Ecology == null)
            {
                errors.Add("Ecology: no ecology database (registry has no zones)");
                return errors;
            }

            EcologyDatabase db = registry.Ecology;
            int rating = NeutralRating(registry);
            float depth = registry.Fishing.Tuning.MinLandDepth;
            string[] zoneIds = ZoneIds(registry);
            string[][] subIds = SubAreaIds(registry);
            string[] speciesIds = SpeciesIds(registry);
            foreach (string e in Coverage(db, rating, depth, zoneIds, subIds, speciesIds)) errors.Add("Ecology: " + e);
            foreach (string e in Variety(db, zoneIds, speciesIds)) errors.Add("Ecology: " + e);
            return errors;
        }

        [MenuItem("FishGame/Ecology/Print Coverage Report")]
        public static void PrintCoverageMenu()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null || registry.Ecology == null)
            {
                Debug.Log("FG ECO no ecology database (run FishGame/Build first)");
                return;
            }
            List<string> lines = CoverageReport(registry.Ecology, NeutralRating(registry), registry.Fishing.Tuning.MinLandDepth,
                ZoneIds(registry), SubAreaIds(registry), SpeciesIds(registry));
            foreach (string line in lines) Debug.Log(line);
            List<string> all = All();
            Debug.Log(all.Count == 0 ? "FG ECO VALIDATE PASS" : "FG ECO VALIDATE FAIL " + string.Join(" | ", all));
        }

        // The bare hook's effective rating with one bait loaded (M3-D5 neutral bait).
        internal static int NeutralRating(ContentRegistry registry)
        {
            FishingDatabase f = registry.Fishing;
            return f.Baits[f.UnlimitedBaitIndex].EffRating(1);
        }

        internal static string[] ZoneIds(ContentRegistry registry)
        {
            var ids = new string[registry.Ecology.Zones.Count];
            for (ushort i = 0; i < ids.Length; i++) ids[i] = registry.ZoneAt(i).Id;
            return ids;
        }

        internal static string[][] SubAreaIds(ContentRegistry registry)
        {
            var result = new string[registry.Ecology.Zones.Count][];
            for (ushort i = 0; i < result.Length; i++)
            {
                FishingZoneDef zone = registry.ZoneAt(i);
                result[i] = new string[zone.SubAreas.Count];
                for (int s = 0; s < result[i].Length; s++) result[i][s] = zone.SubAreas[s].id;
            }
            return result;
        }

        internal static string[] SpeciesIds(ContentRegistry registry)
        {
            var ids = new string[registry.Species.Count];
            for (ushort i = 0; i < ids.Length; i++) ids[i] = registry.SpeciesAt(i).Id;
            return ids;
        }

        // ---- data rules (spec 12.9 paragraph 1, plan 1.7) ----------------------------------------------------------------------------

        internal static List<string> DataRules(ContentRegistry registry)
        {
            var e = new List<string>();
            if (registry.Validate().Count > 0)
            {
                e.Add("registry invalid (see ContentIds); ecology data rules skipped");
                return e;
            }

            int highestRod = 0;
            foreach (RodDef rod in registry.Rods) if (rod != null && rod.Tier > highestRod) highestRod = rod.Tier;
            var usedSpecies = new HashSet<FishSpeciesDef>();

            foreach (FishingZoneDef zone in registry.Zones)
            {
                string zs = "zone " + zone.Id;
                CheckSubAreas(e, zone, zs);
                foreach (SpawnRule rule in zone.Rules)
                {
                    usedSpecies.Add(rule.fish);
                    CheckRule(e, zs + " rule " + rule.fish.Id, rule, highestRod);
                }
                foreach (JunkWeight j in zone.Junk)
                {
                    if (!(j.weight > 0f) || float.IsInfinity(j.weight))
                        e.Add(zs + " junk " + j.junk.Id + ": weight = " + Fmt(j.weight) + " must be finite and > 0");
                }
                CheckWeather(e, zone.Weather);
            }

            foreach (FishSpeciesDef s in registry.Species)
            {
                string ss = "species " + s.Id;
                if (!(s.WeightMinG < s.WeightMaxG)) e.Add(ss + ": weightMinG = " + s.WeightMinG.ToString(CultureInfo.InvariantCulture) + " must be < weightMaxG = " + s.WeightMaxG.ToString(CultureInfo.InvariantCulture));
                if (!(s.BaseValue > 0)) e.Add(ss + ": baseValue = " + s.BaseValue.ToString(CultureInfo.InvariantCulture) + " must be > 0");
                if (!usedSpecies.Contains(s)) e.Add(ss + ": is in no spawn rule of any zone");
            }

            FishingTuningDef tuning = registry.FishingTuning;
            if (tuning != null && tuning.CastMinDist != 2.5f)
                e.Add("tuning: castMinDist = " + Fmt(tuning.CastMinDist) + " must equal 2.5 (EcologyConstants.ReachMinM assumes it; re-derive ReachMinM and the reachability bake)");

            var mapOrders = new Dictionary<int, string>();
            foreach (SpotDef spot in registry.Spots)
            {
                string sp = "spot " + spot.Id;
                if (spot.MapOrder < 0 || spot.MapOrder > 4) e.Add(sp + ": mapOrder = " + spot.MapOrder.ToString(CultureInfo.InvariantCulture) + " outside [0, 4]");
                else if (mapOrders.TryGetValue(spot.MapOrder, out string first))
                    e.Add(sp + ": mapOrder = " + spot.MapOrder.ToString(CultureInfo.InvariantCulture) + " is also used by spot " + first);
                else mapOrders.Add(spot.MapOrder, spot.Id);
            }

            if (registry.EcologyTuning != null) CheckHotspotTuning(e, registry.EcologyTuning);
            if (registry.ArtPalette != null && registry.Zones.Count > 0) CheckPalette(e, registry.ArtPalette);
            if (registry.SfxCueTable != null && registry.Zones.Count > 0) CheckSeasonAmbience(e, registry.SfxCueTable);
            return e;
        }

        private static void CheckSubAreas(List<string> e, FishingZoneDef zone, string zs)
        {
            int n = zone.SubAreas.Count;
            if (n < 1 || n > EcologyConstants.MaxSubAreas)
                e.Add(zs + ": sub-area count = " + n.ToString(CultureInfo.InvariantCulture) + " outside [1, " + EcologyConstants.MaxSubAreas.ToString(CultureInfo.InvariantCulture) + "]");
            for (int i = 0; i < n; i++)
            {
                SubAreaBand b = zone.SubAreas[i];
                string bs = zs + " sub-area " + b.id;
                if (i == 0 && b.minShoreM != 0f) e.Add(bs + ": minShoreM = " + Fmt(b.minShoreM) + ", the first sub-area must start at 0 m (contiguous from 0)");
                if (i > 0 && b.minShoreM != zone.SubAreas[i - 1].maxShoreM)
                    e.Add(bs + ": minShoreM = " + Fmt(b.minShoreM) + " must equal the previous maxShoreM = " + Fmt(zone.SubAreas[i - 1].maxShoreM) + " (contiguous)");
                if (i < n - 1 && !(b.maxShoreM > b.minShoreM))
                    e.Add(bs + ": maxShoreM = " + Fmt(b.maxShoreM) + " must be > minShoreM = " + Fmt(b.minShoreM));
                if (i == n - 1 && b.maxShoreM > 0f)
                    e.Add(bs + ": maxShoreM = " + Fmt(b.maxShoreM) + " must be <= 0 on the last sub-area (open-ended, contiguous to infinity)");
                Range(e, bs, "junkChance", b.junkChance, 0f, EcologyConstants.JunkShareCap);
            }
        }

        private static void CheckRule(List<string> e, string rs, SpawnRule rule, int highestRod)
        {
            if (rule.seasons == SeasonMask.None) e.Add(rs + ": seasons mask is empty (the rule can never pass)");
            if (rule.weather == WeatherMask.None) e.Add(rs + ": weather mask is empty (the rule can never pass)");
            if (rule.bands == TimeBandMask.None) e.Add(rs + ": bands mask is empty (the rule can never pass)");
            if (rule.special)
            {
                if (!(rule.specialChance > 0f && rule.specialChance <= EcologyConstants.SpecialChanceCap))
                    e.Add(rs + ": specialChance = " + Fmt(rule.specialChance) + " outside (0, 0.5]");
            }
            else if (!(rule.weight > 0f))
            {
                e.Add(rs + ": weight = " + Fmt(rule.weight) + " must be > 0 for a non-special rule");
            }
            if (rule.special != rule.fish.IsSpecial)
                e.Add(rs + ": rule special = " + rule.special + " but species isSpecial = " + rule.fish.IsSpecial + " (the flags must match)");
            if (rule.minRodTier > highestRod)
                e.Add(rs + ": minRodTier = " + rule.minRodTier.ToString(CultureInfo.InvariantCulture) + " is above the highest rod tier " + highestRod.ToString(CultureInfo.InvariantCulture));
            if (rule.minBaitRating < 0) e.Add(rs + ": minBaitRating = " + rule.minBaitRating.ToString(CultureInfo.InvariantCulture) + " must be >= 0");
            Range(e, rs, "minDepth", rule.minDepth, 0f, 6f);
            if (rule.catchLimitPerRun < -1)
                e.Add(rs + ": catchLimitPerRun = " + rule.catchLimitPerRun.ToString(CultureInfo.InvariantCulture) + " must be >= -1");
            if (rule.minPlayersInSession < 0 || rule.minPlayersInSession > 4)
                e.Add(rs + ": minPlayersInSession = " + rule.minPlayersInSession.ToString(CultureInfo.InvariantCulture) + " outside [0, 4]");
        }

        private static void CheckWeather(List<string> e, WeatherTableDef w)
        {
            string ws = "weather " + w.Id;
            for (int i = 0; i < w.BaseMatrix.Count; i++)
                if (!(w.BaseMatrix[i] > 0f) || float.IsInfinity(w.BaseMatrix[i])) e.Add(ws + ": baseMatrix[" + i + "] = " + Fmt(w.BaseMatrix[i]) + " must be finite and > 0");
            for (int i = 0; i < w.SeasonColumnMult.Count; i++)
                if (!(w.SeasonColumnMult[i] > 0f) || float.IsInfinity(w.SeasonColumnMult[i])) e.Add(ws + ": seasonColumnMult[" + i + "] = " + Fmt(w.SeasonColumnMult[i]) + " must be finite and > 0");
        }

        private static void CheckHotspotTuning(List<string> e, EcologyTuningDef t)
        {
            const string s = "ecologyTuning";
            IntRange(e, s, "hotspotFirstMinMin", t.HotspotFirstMinMin, 0, 1200);
            IntRange(e, s, "hotspotFirstMinMax", t.HotspotFirstMinMax, 0, 1200);
            if (t.HotspotFirstMinMin > t.HotspotFirstMinMax) e.Add(s + ": hotspotFirstMinMin = " + t.HotspotFirstMinMin + " must be <= hotspotFirstMinMax = " + t.HotspotFirstMinMax);
            IntRange(e, s, "hotspotGapMin", t.HotspotGapMin, 1, 1200);
            IntRange(e, s, "hotspotGapMax", t.HotspotGapMax, 1, 1200);
            if (t.HotspotGapMin > t.HotspotGapMax) e.Add(s + ": hotspotGapMin = " + t.HotspotGapMin + " must be <= hotspotGapMax = " + t.HotspotGapMax);
            IntRange(e, s, "hotspotDurationMin", t.HotspotDurationMin, 1, 1200);
            IntRange(e, s, "hotspotDurationMax", t.HotspotDurationMax, 1, 1200);
            if (t.HotspotDurationMin > t.HotspotDurationMax) e.Add(s + ": hotspotDurationMin = " + t.HotspotDurationMin + " must be <= hotspotDurationMax = " + t.HotspotDurationMax);
            if (!(t.HotspotRadiusM > 0f && t.HotspotRadiusM <= 10f)) e.Add(s + ": hotspotRadiusM = " + Fmt(t.HotspotRadiusM) + " outside (0, 10]");
        }

        private static void CheckPalette(List<string> e, ArtPaletteDef palette)
        {
            const string p = "palette";
            SunMoonSettings sm = palette.SunMoon;
            if (!(sm.sunriseFraction >= 0f && sm.sunriseFraction < sm.sunsetFraction && sm.sunsetFraction <= 1f))
                e.Add(p + ": sunMoon sunriseFraction = " + Fmt(sm.sunriseFraction) + " must be < sunsetFraction = " + Fmt(sm.sunsetFraction) + " (both within [0, 1])");
            Range(e, p, "sunMoon minElevationDeg", sm.minElevationDeg, 0f, 45f);
            if (!(sm.flipWindow > 0f && sm.flipWindow <= 0.1f)) e.Add(p + ": sunMoon flipWindow = " + Fmt(sm.flipWindow) + " outside (0, 0.1]");
            Range(e, p, "sunMoon moonIntensity", sm.moonIntensity, 0f, 1f);
            if (!(sm.blendWindow > 0f && sm.blendWindow <= 0.1f)) e.Add(p + ": sunMoon blendWindow = " + Fmt(sm.blendWindow) + " outside (0, 0.1]");
            if (palette.WeatherLooks.Count != 3)
            {
                e.Add(p + ": weatherLooks must have exactly 3 entries (has " + palette.WeatherLooks.Count + ")");
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                WeatherAmbient a = palette.WeatherLooks[i];
                string ls = p + " weatherLooks[" + i + "]";
                Range(e, ls, "fogDensityMult", a.fogDensityMult, 0.5f, 2f);
                Range(e, ls, "fogDensityMin", a.fogDensityMin, 0f, MaxFogMin);
                Range(e, ls, "lightMult", a.lightMult, 0.3f, 1.5f);
                Range(e, ls, "desaturate", a.desaturate, 0f, 1f);
                Range(e, ls, "rainAmount", a.rainAmount, 0f, 1f);
            }
        }

        private static void CheckSeasonAmbience(List<string> e, SfxCueTableDef cues)
        {
            const string c = "cueTable";
            if (cues.SeasonAmbience.Count != 4)
            {
                e.Add(c + ": seasonAmbience must have exactly 4 entries (has " + cues.SeasonAmbience.Count + ")");
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                SeasonAmbience a = cues.SeasonAmbience[i];
                string ss = c + " seasonAmbience[" + i + "]";
                Range(e, ss, "windGain", a.windGain, 0f, 2f);
                Range(e, ss, "windLfoHz", a.windLfoHz, 0f, 1f);
                Range(e, ss, "lapIntervalMin", a.lapIntervalMin, 0.2f, 5f);
                if (!(a.lapIntervalMax >= a.lapIntervalMin && a.lapIntervalMax <= 10f))
                    e.Add(ss + ": lapIntervalMax = " + Fmt(a.lapIntervalMax) + " outside [lapIntervalMin = " + Fmt(a.lapIntervalMin) + ", 10]");
            }
        }

        // ---- coverage V1-V3 -----------------------------------------------------------------------------------------------------------

        // The contract surface: indices in messages.
        internal static List<string> Coverage(EcologyDatabase db, int neutralBaitRating, float neutralDepth)
        {
            return Coverage(db, neutralBaitRating, neutralDepth, null, null, null);
        }

        // Readable messages: zoneIds[z], subAreaIds[z][s] and speciesIds[i] replace the indices (any of them may be null).
        internal static List<string> Coverage(EcologyDatabase db, int neutralBaitRating, float neutralDepth, string[] zoneIds, string[][] subAreaIds,
            string[] speciesIds)
        {
            var errors = new List<string>();
            var resolver = new FishingResolver(db);
            var buf = new List<int>();

            for (int z = 0; z < db.Zones.Count; z++)
            {
                ZoneData zone = db.Zones[z];
                string zn = Name(zoneIds, z);
                // Structural facts the resolver trusts.
                for (int r = 1; r < zone.Rules.Count; r++)
                    if (zone.Rules[r].SpeciesIndex < zone.Rules[r - 1].SpeciesIndex)
                        errors.Add("Rules not sorted by species: " + zn + " rule " + r.ToString(CultureInfo.InvariantCulture));
                for (int j = 0; j < zone.Junk.Count; j++)
                    if (!(zone.Junk[j].Weight > 0f) || float.IsInfinity(zone.Junk[j].Weight))
                        errors.Add("Junk weight not positive: " + zn + " junk " + j.ToString(CultureInfo.InvariantCulture) + " (" + Fmt(zone.Junk[j].Weight) + ")");

                for (int season = 0; season < 4; season++)
                    for (int band = 0; band < BandCount; band++)
                        for (int w = 0; w < Weathers.Length; w++)
                        {
                            var shareSum = new Dictionary<int, double>();
                            for (int sub = 0; sub < zone.SubAreas.Count; sub++)
                            {
                                FishingContext ctx = Ctx(z, sub, DayClock.BandStart[band], (Season)season, Weathers[w], 0, neutralBaitRating, neutralDepth);
                                int count = resolver.CollectEligible(in ctx, null, false, buf);
                                if (count == 0)
                                {
                                    errors.Add("V1 dead sub-area: " + zn + " " + Name(subAreaIds == null ? null : subAreaIds[z], sub) + " " + ((Season)season) + " "
                                               + BandNames[band] + " " + WeatherNames[w]);
                                    continue;
                                }
                                double total = 0;
                                for (int k = 0; k < count; k++) total += zone.Rules[buf[k]].Weight;
                                for (int k = 0; k < count; k++)
                                {
                                    int species = zone.Rules[buf[k]].SpeciesIndex;
                                    shareSum.TryGetValue(species, out double acc);
                                    shareSum[species] = acc + zone.Rules[buf[k]].Weight / total;
                                }
                            }
                            // V2: the mixed share of a species is the mean of its per-sub-area shares (an empty sub-area contributes 0).
                            int distinct = 0, top = -1;
                            double topShare = 0;
                            foreach (KeyValuePair<int, double> kv in shareSum)
                            {
                                double share = kv.Value / zone.SubAreas.Count;
                                if (share > 0) distinct++;
                                if (share > topShare || (share == topShare && kv.Key < top)) { topShare = share; top = kv.Key; }
                            }
                            string ctxName = zn + " " + ((Season)season) + " " + BandNames[band] + " " + WeatherNames[w];
                            if (distinct < 3) errors.Add("V2 fewer than 3 species: " + ctxName + " (" + distinct.ToString(CultureInfo.InvariantCulture) + ")");
                            if (topShare > 0.5) errors.Add("V2 dominant species: " + ctxName + " " + Name(speciesIds, top) + " " + Pct(topShare));
                        }
            }

            // V3: every species (specials included) passes somewhere at rod 3, bait rating 10, depth 6 m.
            int[] reached = V3Counts(db, resolver);
            for (int i = 0; i < reached.Length; i++)
                if (reached[i] == 0) errors.Add("V3 unreachable species: " + Name(speciesIds, i));
            return errors;
        }

        // Number of V3 contexts in which each species passes (specials included).
        private static int[] V3Counts(EcologyDatabase db, FishingResolver resolver)
        {
            var reached = new int[db.Fishing.Species.Count];
            for (int z = 0; z < db.Zones.Count; z++)
            {
                ZoneData zone = db.Zones[z];
                for (int season = 0; season < 4; season++)
                    for (int band = 0; band < BandCount; band++)
                        for (int w = 0; w < Weathers.Length; w++)
                            for (int sub = 0; sub < zone.SubAreas.Count; sub++)
                            {
                                FishingContext ctx = Ctx(z, sub, DayClock.BandStart[band], (Season)season, Weathers[w], V3Rod, V3Rating, V3Depth);
                                var counted = new HashSet<int>();
                                for (int r = 0; r < zone.Rules.Count; r++)
                                {
                                    SpawnRuleData rule = zone.Rules[r];
                                    if (resolver.Passes(in rule, in ctx, null) && counted.Add(rule.SpeciesIndex)) reached[rule.SpeciesIndex]++;
                                }
                            }
            }
            return reached;
        }

        // ---- variety (spec 13.3, M3-D65 / M3-D66) -------------------------------------------------------------------------------------

        internal static List<string> Variety(EcologyDatabase db)
        {
            return Variety(db, null, null);
        }

        internal static List<string> Variety(EcologyDatabase db, string[] zoneIds, string[] speciesIds)
        {
            var errors = new List<string>();
            var resolver = new FishingResolver(db);
            for (int z = 0; z < db.Zones.Count; z++)
                for (int season = 0; season < 4; season++)
                {
                    bool[,] el = Eligible(db, resolver, z, (Season)season);
                    int species = db.Fishing.Species.Count;
                    string where = Name(zoneIds, z) + " " + ((Season)season);
                    int union = 0;
                    bool narrow = false;
                    for (int s = 0; s < species; s++)
                    {
                        int bands = 0, first = -1, last = -1;
                        for (int b = 0; b < BandCount; b++)
                            if (el[b, s]) { bands++; if (first < 0) first = b; last = b; }
                        if (bands > 0) union++;
                        if (bands == 1 || (bands == 2 && last == first + 1)) narrow = true;   // Night (5) and Dawn (0) are NOT adjacent
                    }
                    if (union < 4) errors.Add("Variety fewer than 4 species: " + where + " (" + union.ToString(CultureInfo.InvariantCulture) + ")");
                    if (!narrow) errors.Add("Variety no narrow-window species: " + where);
                    int diff = MorningNightDifference(el, species);
                    if (diff < 2) errors.Add("Variety Morning and Night differ by " + diff.ToString(CultureInfo.InvariantCulture) + ": " + where);
                }
            return errors;
        }

        // Species eligible in (zone, season, band): some non-special rule passes at rod 3, rating 10, depth 6 in any weather and sub-area.
        private static bool[,] Eligible(EcologyDatabase db, FishingResolver resolver, int zone, Season season)
        {
            var el = new bool[BandCount, db.Fishing.Species.Count];
            var buf = new List<int>();
            ZoneData zd = db.Zones[zone];
            for (int band = 0; band < BandCount; band++)
                for (int w = 0; w < Weathers.Length; w++)
                    for (int sub = 0; sub < zd.SubAreas.Count; sub++)
                    {
                        FishingContext ctx = Ctx(zone, sub, DayClock.BandStart[band], season, Weathers[w], V3Rod, V3Rating, V3Depth);
                        resolver.CollectEligible(in ctx, null, false, buf);
                        foreach (int ruleIndex in buf) el[band, zd.Rules[ruleIndex].SpeciesIndex] = true;
                    }
            return el;
        }

        private static int MorningNightDifference(bool[,] el, int species)
        {
            int diff = 0;
            for (int s = 0; s < species; s++) if (el[1, s] != el[5, s]) diff++;
            return diff;
        }

        // ---- the printed report (plan 0.6) --------------------------------------------------------------------------------------------

        internal static List<string> CoverageReport(EcologyDatabase db, int neutralBaitRating, float neutralDepth, string[] zoneIds,
            string[][] subAreaIds, string[] speciesIds)
        {
            var lines = new List<string>();
            var resolver = new FishingResolver(db);
            var buf = new List<int>();
            for (int z = 0; z < db.Zones.Count; z++)
            {
                ZoneData zone = db.Zones[z];
                string zn = Name(zoneIds, z);
                int dead = 0, contexts = 0;
                var v2 = new List<string>();
                for (int season = 0; season < 4; season++)
                    for (int band = 0; band < BandCount; band++)
                        for (int w = 0; w < Weathers.Length; w++)
                        {
                            var shareSum = new Dictionary<int, double>();
                            for (int sub = 0; sub < zone.SubAreas.Count; sub++)
                            {
                                contexts++;
                                FishingContext ctx = Ctx(z, sub, DayClock.BandStart[band], (Season)season, Weathers[w], 0, neutralBaitRating, neutralDepth);
                                int count = resolver.CollectEligible(in ctx, null, false, buf);
                                if (count == 0) { dead++; continue; }
                                double total = 0;
                                for (int k = 0; k < count; k++) total += zone.Rules[buf[k]].Weight;
                                for (int k = 0; k < count; k++)
                                {
                                    int species = zone.Rules[buf[k]].SpeciesIndex;
                                    shareSum.TryGetValue(species, out double acc);
                                    shareSum[species] = acc + zone.Rules[buf[k]].Weight / total;
                                }
                            }
                            int distinct = 0, top = -1;
                            double topShare = 0;
                            foreach (KeyValuePair<int, double> kv in shareSum)
                            {
                                double share = kv.Value / zone.SubAreas.Count;
                                if (share > 0) distinct++;
                                if (share > topShare || (share == topShare && kv.Key < top)) { topShare = share; top = kv.Key; }
                            }
                            v2.Add("FG ECO V2 " + zn + " " + ((Season)season) + " " + BandNames[band] + " " + WeatherNames[w] + " species="
                                   + distinct.ToString(CultureInfo.InvariantCulture) + " top=" + (top < 0 ? "none" : Name(speciesIds, top)) + " " + Pct(topShare));
                        }
                lines.Add("FG ECO V1 " + zn + " dead=" + dead.ToString(CultureInfo.InvariantCulture) + " of " + contexts.ToString(CultureInfo.InvariantCulture));
                lines.AddRange(v2);
            }

            int[] reached = V3Counts(db, resolver);
            for (int i = 0; i < reached.Length; i++)
                lines.Add("FG ECO V3 " + Name(speciesIds, i) + " contexts=" + reached[i].ToString(CultureInfo.InvariantCulture));

            for (int z = 0; z < db.Zones.Count; z++)
                for (int season = 0; season < 4; season++)
                {
                    bool[,] el = Eligible(db, resolver, z, (Season)season);
                    int species = db.Fishing.Species.Count, union = 0, single = 0;
                    var narrow = new List<string>();
                    for (int s = 0; s < species; s++)
                    {
                        int bands = 0, first = -1, last = -1;
                        for (int b = 0; b < BandCount; b++)
                            if (el[b, s]) { bands++; if (first < 0) first = b; last = b; }
                        if (bands > 0) union++;
                        if (bands == 1) single++;
                        if (bands == 1 || (bands == 2 && last == first + 1)) narrow.Add(Name(speciesIds, s));
                    }
                    lines.Add("FG ECO VARIETY " + Name(zoneIds, z) + " " + ((Season)season) + " eligible=" + union.ToString(CultureInfo.InvariantCulture)
                              + " narrow=" + (narrow.Count == 0 ? "none" : string.Join(",", narrow)) + " single=" + single.ToString(CultureInfo.InvariantCulture)
                              + " morningNightDiff=" + MorningNightDifference(el, species).ToString(CultureInfo.InvariantCulture));
                }
            return lines;
        }

        // ---- helpers ------------------------------------------------------------------------------------------------------------------

        // Neutral context (M3-D5): the given bait rating without affinity, neutral events and skills, no hotspot, luck 0, one player.
        private static FishingContext Ctx(int zone, int sub, int minute, Season season, WeatherMask weather, int rod, int rating, float depth)
        {
            return new FishingContext
            {
                ZoneIndex = zone, SubAreaIndex = (byte)sub, Depth = depth, Minute = minute, Season = season, Weather = weather, RodTier = rod,
                Bait = BaitMods.Neutral(rating), InHotspot = false, Luck = 0f, Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = 1,
            };
        }

        private static string Name(string[] names, int index)
        {
            if (names != null && index >= 0 && index < names.Length && names[index] != null) return names[index];
            return index.ToString(CultureInfo.InvariantCulture);
        }

        private static string Pct(double share)
        {
            return (share * 100.0).ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        private static string Fmt(float v)
        {
            return v.ToString("G", CultureInfo.InvariantCulture);
        }

        private static void Range(List<string> e, string subject, string field, float v, float min, float max)
        {
            if (!(v >= min && v <= max)) e.Add(subject + ": " + field + " = " + Fmt(v) + " outside [" + Fmt(min) + ", " + Fmt(max) + "]");
        }

        private static void IntRange(List<string> e, string subject, string field, int v, int min, int max)
        {
            if (v < min || v > max)
                e.Add(subject + ": " + field + " = " + v.ToString(CultureInfo.InvariantCulture) + " outside [" + min.ToString(CultureInfo.InvariantCulture) + ", "
                      + max.ToString(CultureInfo.InvariantCulture) + "]");
        }
    }
}
