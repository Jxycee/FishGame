// T12 balance reports (spec 12.9, contract 11.4, plan data-coverage section 5): V4 expedition sim, V5 economy and the M4 quota-calibration print.
// Real assets (registry), the real FishingResolver, the real WeatherDirector and the R72 Rhythm bot (FishingBots.RunFight, internal). Reads no seeds.
// Test-local random streams only (Rng.Stream with "balance.*" ids), never an EcologyStreams id. A bound that data cannot meet is an
// EcologyTuningItems entry with the controller's approval; M3 never retunes R72 fight values (M3-D38).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace FishGame.Tests.EditMode
{
    [Category("Balance")]
    public class EcologyBalanceReports
    {
        // key = test name, value = one line with the measured numbers and the reason. Needs the controller's approval.
        static readonly Dictionary<string, string> EcologyTuningItems = new Dictionary<string, string>();

        const int Runs = 400, Days = 7, CastsPerDay = 60;
        const int FightsPerCell = 200;
        const int EvSamples = 20000;
        const float WalkSeconds = 10f;

        // F8 published medians (spec 9.6 / 9.12, "Measured (F4 bots ...)"): Handmade and Twig only; printed next to ours as a check.
        static readonly Dictionary<string, double> F8Medians = new Dictionary<string, double>
        {
            { "steady|1", 14.0 }, { "darter|1", 19.1 }, { "thrasher|1", 17.8 }, { "runner|1", 26.2 }, { "bulldog|1", 30.9 },
            { "steady|0", 14.4 }, { "darter|0", 21.6 }, { "thrasher|0", 21.7 }, { "runner|0", 30.5 }, { "bulldog|0", 33.7 },
        };

        static readonly string[] BandNames = { "Dawn", "Morning", "Midday", "Afternoon", "Dusk", "Night" };
        static readonly WeatherMask[] Weathers = { WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };
        static readonly string[] WeatherNames = { "Clear", "Rain", "Fog" };
        static readonly float[] DepthBySubArea = { 0.75f, 1.5f, 3.0f };

        sealed class Targets
        {
            readonly string _test;
            readonly List<string> _failures = new List<string>();
            public Targets(string test) { _test = test; }

            public void Add(bool pass, string measured, string target)
            {
                string status = pass ? "PASS" : (EcologyTuningItems.ContainsKey(_test) ? "TUNING-ITEM" : "FAIL");
                TestContext.WriteLine("FG TARGET " + _test + " " + status + " " + measured + " (target " + target + ")");
                if (!pass) _failures.Add(measured + " (target " + target + ")");
            }

            public void Complete()
            {
                if (_failures.Count == 0)
                {
                    if (EcologyTuningItems.ContainsKey(_test)) TestContext.WriteLine("FG TUNING STALE " + _test);
                    return;
                }
                if (EcologyTuningItems.TryGetValue(_test, out string note)) Assert.Ignore("FG TUNING ITEM " + note);
                Assert.Fail(_test + ": " + string.Join("; ", _failures));
            }
        }

        static string F1(double v) { return double.IsNaN(v) ? "n/a" : v.ToString("F1", CultureInfo.InvariantCulture); }
        static string F2(double v) { return double.IsNaN(v) ? "n/a" : v.ToString("F2", CultureInfo.InvariantCulture); }
        static string Pct(double v) { return F1(100.0 * v) + "%"; }

        ContentRegistry _registry;
        EcologyDatabase _db;
        readonly Stopwatch _clock = Stopwatch.StartNew();

        // Static caches shared by the tests of this fixture (the domain survives between runs: cleared in OneTimeSetUp).
        static V4Cell[] s_v4;
        static FightTable s_fights;

        [OneTimeSetUp]
        public void LoadRegistryAndClearCaches()
        {
            s_v4 = null;
            s_fights = null;
            _registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(_registry, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            _db = _registry.Ecology;
            Assert.That(_db, Is.Not.Null, "the registry has no ecology database");
        }

        [OneTimeTearDown]
        public void PrintTime()
        {
            TestContext.WriteLine("FG TIME EcologyBalanceReports " + F1(_clock.Elapsed.TotalSeconds) + "s");
        }

        string ZoneName(int z) { return _registry.ZoneAt((ushort)z).Id; }
        string SpeciesName(int i) { return _registry.SpeciesAt((ushort)i).Id; }
        string SubName(int z, int sub) { return _registry.ZoneAt((ushort)z).SubAreas[sub].id; }

        // ================================================================== V4

        sealed class V4Cell
        {
            public int Zone, Season;
            public double[] SpeciesPerRun;       // mean catches per run, by species index
            public double JunkRunShare;          // mean over runs of the run's junk share of all casts
            public double[] JunkDayShare = new double[Days + 1];   // index 1..7: mean over runs of that day's junk share
            public double MaxTopShare;           // max over runs of the top species' share of the run's fish
            public string TopSpecies = "";
        }

        static ulong RunSeed(int r) { return unchecked(0x9E3779B97F4A7C15UL * (ulong)(r + 1)); }
        static int RodTierForDay(int d) { return d <= 2 ? 0 : d <= 4 ? 1 : d <= 6 ? 2 : 3; }

        V4Cell[] V4()
        {
            if (s_v4 != null) return s_v4;
            var resolver = new FishingResolver(_db);
            var cells = new List<V4Cell>();
            int speciesCount = _db.Fishing.Species.Count;
            for (int z = 0; z < _db.Zones.Count; z++)
            {
                ZoneData zone = _db.Zones[z];
                for (int s = 0; s < 4; s++)
                {
                    var cell = new V4Cell { Zone = z, Season = s, SpeciesPerRun = new double[speciesCount] };
                    var runCounts = new int[speciesCount];
                    for (int r = 0; r < Runs; r++)
                    {
                        ulong runSeed = RunSeed(r);
                        var weather = new WeatherDirector(in zone.Weather, (Season)s, runSeed);
                        Pcg32 castRng = Rng.Stream(runSeed, "balance.v4.cast", 0);
                        var ledger = new CatchLedger();
                        Array.Clear(runCounts, 0, runCounts.Length);
                        int fish = 0, junkRun = 0;
                        for (int d = 1; d <= Days; d++)
                        {
                            int junkDay = 0;
                            for (int c = 0; c < CastsPerDay; c++)
                            {
                                int minute = castRng.Range(0, DayClock.DayLengthMinutes);
                                int sub = castRng.Range(0, zone.SubAreas.Count);
                                var ctx = new FishingContext
                                {
                                    ZoneIndex = z, SubAreaIndex = (byte)sub, Depth = 2f, Minute = minute, Season = (Season)s, Weather = weather.At(d, minute),
                                    RodTier = RodTierForDay(d), Bait = BaitMods.Neutral(1), InHotspot = false, Luck = 0f,
                                    Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = 1,
                                };
                                Pcg32 catchRng = Rng.Stream(runSeed, "balance.v4.catch", (ulong)(d * 100 + c));
                                CatchResult result = resolver.Resolve(in ctx, ref catchRng, ledger, out _, out int ruleIndex);
                                if (result.IsJunk) { junkDay++; junkRun++; continue; }
                                runCounts[result.ItemIndex]++;
                                fish++;
                                if (ruleIndex >= 0 && zone.Rules[ruleIndex].CatchLimitPerRun >= 0)
                                {
                                    ledger.Reserve(result.ItemIndex);
                                    ledger.Commit(result.ItemIndex);
                                    Assert.That(runCounts[result.ItemIndex], Is.LessThanOrEqualTo(zone.Rules[ruleIndex].CatchLimitPerRun),
                                        SpeciesName(result.ItemIndex) + " was caught more often than its catch limit in one run (" + ZoneName(z) + " " + (Season)s + " run " + r + ")");
                                }
                            }
                            cell.JunkDayShare[d] += (double)junkDay / CastsPerDay;
                        }
                        cell.JunkRunShare += (double)junkRun / (Days * CastsPerDay);
                        int top = 0;
                        for (int i = 0; i < speciesCount; i++)
                        {
                            cell.SpeciesPerRun[i] += runCounts[i];
                            if (runCounts[i] > runCounts[top]) top = i;
                        }
                        if (fish > 0)
                        {
                            double share = (double)runCounts[top] / fish;
                            if (share > cell.MaxTopShare) { cell.MaxTopShare = share; cell.TopSpecies = SpeciesName(top); }
                        }
                    }
                    for (int i = 0; i < speciesCount; i++) cell.SpeciesPerRun[i] /= Runs;
                    cell.JunkRunShare /= Runs;
                    for (int d = 1; d <= Days; d++) cell.JunkDayShare[d] /= Runs;
                    cells.Add(cell);
                }
            }
            s_v4 = cells.ToArray();
            return s_v4;
        }

        static double RarityBound(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Common: return 7.0;
                case FishRarity.Uncommon: return 3.5;
                case FishRarity.Rare: return 0.7;
                default: return 0.4;   // Legendary
            }
        }

        // Catches: rarity bounds read from the wrong field; out-of-season species asserted; limits not reserved (a special counted twice per run).
        [Test]
        public void EcologyBalance_V4_ExpeditionCatchesPerSpecies()
        {
            var t = new Targets(nameof(EcologyBalance_V4_ExpeditionCatchesPerSpecies));
            foreach (V4Cell cell in V4())
            {
                ZoneData zone = _db.Zones[cell.Zone];
                var seen = new HashSet<int>();
                foreach (SpawnRuleData rule in zone.Rules)
                {
                    if ((rule.Seasons & EcologyMasks.Of((Season)cell.Season)) == 0 || !seen.Add(rule.SpeciesIndex)) continue;   // in season only
                    FishSpeciesData sp = _db.Fishing.Species[rule.SpeciesIndex];
                    double bound = RarityBound(sp.Rarity);
                    double perRun = cell.SpeciesPerRun[rule.SpeciesIndex];
                    t.Add(perRun >= bound, "FG V4 " + ZoneName(cell.Zone) + " " + (Season)cell.Season + " " + SpeciesName(rule.SpeciesIndex) + " " + sp.Rarity
                                           + " rod=" + rule.MinRodTier + " perRun=" + F2(perRun) + " bound=" + F2(bound), "perRun >= " + F2(bound));
                }
            }
            t.Complete();
        }

        // Catches: the bare-hook junk multiplier in the sim (V4 is neutral bait); per-run-day instead of mean-over-runs.
        [Test]
        public void EcologyBalance_V4_JunkAtMostTwentyPercentEveryDay()
        {
            var t = new Targets(nameof(EcologyBalance_V4_JunkAtMostTwentyPercentEveryDay));
            foreach (V4Cell cell in V4())
            {
                string days = string.Join("/", Enumerable.Range(1, Days).Select(d => F1(100.0 * cell.JunkDayShare[d])));
                double maxDay = Enumerable.Range(1, Days).Max(d => cell.JunkDayShare[d]);
                TestContext.WriteLine("FG V4 JUNK " + ZoneName(cell.Zone) + " " + (Season)cell.Season + " run=" + Pct(cell.JunkRunShare) + " day1..7=" + days + "% maxDay=" + Pct(maxDay));
                t.Add(cell.JunkRunShare <= 0.20 && maxDay <= 0.20, ZoneName(cell.Zone) + " " + (Season)cell.Season + " run=" + Pct(cell.JunkRunShare) + " maxDay=" + Pct(maxDay), "run mean and every day mean <= 20.0%");
            }
            t.Complete();
        }

        // Catches: junk counted as a species; a mean hiding one bad run.
        [Test]
        public void EcologyBalance_V4_NoSpeciesAboveHalfOfARun()
        {
            var t = new Targets(nameof(EcologyBalance_V4_NoSpeciesAboveHalfOfARun));
            foreach (V4Cell cell in V4())
            {
                TestContext.WriteLine("FG V4 TOP " + ZoneName(cell.Zone) + " " + (Season)cell.Season + " maxShareInAnyRun=" + Pct(cell.MaxTopShare) + " species=" + cell.TopSpecies);
                t.Add(cell.MaxTopShare <= 0.50, ZoneName(cell.Zone) + " " + (Season)cell.Season + " max share in any run=" + Pct(cell.MaxTopShare) + " (" + cell.TopSpecies + ")", "<= 50.0%");
            }
            t.Complete();
        }

        // ================================================================== V5

        // Fight median / landed rate per (profile index, rod tier), measured with the R72 Rhythm bot on the real assets (M3-D64).
        sealed class FightTable
        {
            public readonly Dictionary<long, (double median, double land)> Cells = new Dictionary<long, (double, double)>();
            public static long Key(int profile, int tier) { return profile * 16L + tier; }
        }

        FightTable Fights()
        {
            if (s_fights != null) return s_fights;
            var table = new FightTable();
            FishingDatabase f = _db.Fishing;
            FishingTuning tuning = f.Tuning;
            for (int p = 0; p < f.Profiles.Count; p++)
                for (int tier = 0; tier < 4; tier++)
                {
                    RodStats rod = RodOfTier(f, tier);
                    FishFightProfile profile = f.Profiles[p];
                    var landed = new List<float>();
                    for (int i = 0; i < FightsPerCell; i++)
                    {
                        FishingBots.FightRecord rec = FishingBots.RunFight(FishingBots.BotKind.Rhythm, in profile, in rod, in tuning, i);
                        if (rec.Outcome == FightOutcome.Landed) landed.Add(rec.Elapsed);
                    }
                    landed.Sort();
                    double median = landed.Count == 0 ? double.NaN : landed[Math.Max(0, (int)Math.Ceiling(0.5 * landed.Count) - 1)];
                    table.Cells[FightTable.Key(p, tier)] = (median, (double)landed.Count / FightsPerCell);
                }
            s_fights = table;
            return table;
        }

        static RodStats RodOfTier(FishingDatabase f, int tier)
        {
            for (int i = 0; i < f.Rods.Count; i++) if (f.Rods[i].Tier == tier) return f.Rods[i];
            throw new InvalidOperationException("no rod of tier " + tier);
        }

        // Mean value and mean kilograms of one species over its weight distribution (M3 neutral luck): midpoint rule over u.
        internal static void MeanValue(in FishSpeciesData s, out double coins, out double kg)
        {
            double c = 0, w = 0;
            float skew = CatchMath.Skew(s.WeightSkew, 0f);
            for (int k = 0; k < EvSamples; k++)
            {
                float u = (k + 0.5f) / EvSamples;
                float t = CatchMath.SkewedT(u, skew);
                c += CatchMath.Value(s.BaseValue, t, CatchMath.Quality(t));
                w += CatchMath.WeightG(s.WeightMinG, s.WeightMaxG, t) / 1000.0;
            }
            coins = c / EvSamples;
            kg = w / EvSamples;
        }

        // Inputs of one economy evaluation that the real run measures and a unit check can stub.
        internal struct EconomyInputs
        {
            public Func<int, (double coins, double kg)> MeanValueOf;        // species index -> mean coins and kg of one landed fish
            public Func<int, int, (double median, double land)> FightOf;    // (profile index, rod tier) -> median seconds and landed fraction
        }

        // The renewal-reward rate of one context (plan 5.4): expected coins and kg per real minute at neutral bait.
        internal static void Economy(EcologyDatabase db, EconomyInputs inp, in FishingContext ctx, out double coinsPerMin, out double kgPerMin)
        {
            var resolver = new FishingResolver(db);
            ZoneData zone = db.Zones[ctx.ZoneIndex];
            FishingTuning t = db.Fishing.Tuning;
            float depth = ctx.Depth;
            double j = resolver.JunkShare(in ctx);
            var pool = new List<int>();
            int count = resolver.CollectEligible(in ctx, null, false, pool);
            double sumW = 0;
            for (int k = 0; k < count; k++) sumW += zone.Rules[pool[k]].Weight;
            if (count == 0) j = 1.0;

            double junkWeights = 0, evJunk = 0;
            foreach (JunkEntry e in zone.Junk) { junkWeights += e.Weight; evJunk += e.Weight * db.Fishing.Junk[e.JunkIndex].ValueCoins; }
            evJunk /= junkWeights;

            double overhead = t.CastChargeTime + CastMath.FlightTime(12f) + t.HookBeat + FishingConstants.FeedbackBeatSeconds + WalkSeconds;
            double waitMid = 0.5 * (t.BiteWaitMin + t.BiteWaitMax);
            double waitJunk = Math.Max(t.MinBiteWait, waitMid * BitePlanner.WaitMultiplier(1.0f, 1.0f, depth, 1f, 1f, false));
            double fightJunk = (12.0 - t.LandDistance) / t.JunkDragSpeed;
            double cycle = overhead + j * (waitJunk + fightJunk);
            double coins = j * evJunk, kg = 0;
            for (int k = 0; k < count; k++)
            {
                SpawnRuleData rule = zone.Rules[pool[k]];
                FishSpeciesData sp = db.Fishing.Species[rule.SpeciesIndex];
                double p = (1.0 - j) * rule.Weight / sumW;
                double wait = Math.Max(t.MinBiteWait, waitMid * BitePlanner.WaitMultiplier(1.0f, sp.BiteWaitMod, depth, 1f, 1f, false));
                (double median, double land) fight = inp.FightOf(sp.FightProfileIndex, ctx.RodTier);
                (double ev, double kgs) = inp.MeanValueOf(rule.SpeciesIndex);
                double fightTime = double.IsNaN(fight.median) ? t.FightTimeout : fight.median;
                cycle += p * (wait + fightTime);
                coins += p * fight.land * ev;
                kg += p * fight.land * kgs;
            }
            coinsPerMin = 60.0 * coins / cycle;
            kgPerMin = 60.0 * kg / cycle;
        }

        EconomyInputs RealInputs()
        {
            FightTable fights = Fights();
            var cache = new Dictionary<int, (double, double)>();
            return new EconomyInputs
            {
                MeanValueOf = i =>
                {
                    if (!cache.TryGetValue(i, out (double, double) v))
                    {
                        FishSpeciesData s = _db.Fishing.Species[i];
                        MeanValue(in s, out double c, out double kg);
                        v = (c, kg);
                        cache[i] = v;
                    }
                    return v;
                },
                FightOf = (p, tier) => fights.Cells[FightTable.Key(p, tier)],
            };
        }

        FishingContext V5Context(int z, int sub, int band, int w, int tier, Season season)
        {
            return new FishingContext
            {
                ZoneIndex = z, SubAreaIndex = (byte)sub, Depth = DepthBySubArea[Math.Min(sub, DepthBySubArea.Length - 1)], Minute = DayClock.BandStart[band],
                Season = season, Weather = Weathers[w], RodTier = tier, Bait = BaitMods.Neutral(1), InHotspot = false, Luck = 0f,
                Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = 1,
            };
        }

        void PrintMedians()
        {
            FightTable fights = Fights();
            for (int p = 0; p < _db.Fishing.Profiles.Count; p++)
            {
                string pid = _registry.FightProfileAt((ushort)p).Id;
                for (int tier = 0; tier < 4; tier++)
                {
                    var cell = fights.Cells[FightTable.Key(p, tier)];
                    string rodId = _registry.Rods.First(r => r.Tier == tier).Id;
                    string f8 = F8Medians.TryGetValue(pid + "|" + tier, out double m) ? F1(m) + "s" : "n/a";
                    TestContext.WriteLine("FG V5 MEDIAN " + pid + " " + rodId + " med=" + F1(cell.median) + "s land=" + Pct(cell.land) + " (F8 " + f8 + ")");
                }
            }
        }

        // Catches: wins counted over sub-areas instead of contexts; specials in the economy; the junk drag ignored.
        [Test]
        public void EcologyBalance_V5_NoSubAreaBestForCoinsAboveEightyFivePercent()
        {
            PrintMedians();
            EconomyInputs inp = RealInputs();
            var t = new Targets(nameof(EcologyBalance_V5_NoSubAreaBestForCoinsAboveEightyFivePercent));
            for (int z = 0; z < _db.Zones.Count; z++)
                for (int s = 0; s < 4; s++)
                {
                    int subs = _db.Zones[z].SubAreas.Count;
                    var wins = new int[subs];
                    for (int band = 0; band < 6; band++)
                        for (int w = 0; w < 3; w++)
                        {
                            int best = 0;
                            double bestCoins = double.NegativeInfinity;
                            for (int sub = 0; sub < subs; sub++)
                            {
                                FishingContext ctx = V5Context(z, sub, band, w, 0, (Season)s);
                                Economy(_db, inp, in ctx, out double cpm, out double kpm);
                                TestContext.WriteLine("FG V5 " + ZoneName(z) + " " + (Season)s + " " + SubName(z, sub) + " " + BandNames[band] + " " + WeatherNames[w]
                                                      + " coinsPerMin=" + F2(cpm) + " kgPerMin=" + F2(kpm));
                                if (cpm > bestCoins) { bestCoins = cpm; best = sub; }   // strict >: ties go to the lower index
                            }
                            wins[best]++;
                        }
                    for (int sub = 0; sub < subs; sub++)
                    {
                        TestContext.WriteLine("FG V5 BEST " + ZoneName(z) + " " + (Season)s + " " + SubName(z, sub) + " wins=" + wins[sub] + "/18 (" + Pct(wins[sub] / 18.0) + ")");
                        t.Add(wins[sub] <= 15, ZoneName(z) + " " + (Season)s + " " + SubName(z, sub) + " wins=" + wins[sub] + "/18", "wins <= 15 of 18 (85%)");
                    }
                }
            t.Complete();
        }

        // Catches: a silent NaN from an empty pool or a zero cycle.
        [Test]
        public void EcologyBalance_V5_PrintsQuotaCalibrationInputs()
        {
            EconomyInputs inp = RealInputs();
            var t = new Targets(nameof(EcologyBalance_V5_PrintsQuotaCalibrationInputs));
            for (int z = 0; z < _db.Zones.Count; z++)
                for (int s = 0; s < 4; s++)
                {
                    double[] pi = Stationary(_db.Zones[z], (Season)s);
                    TestContext.WriteLine("FG QUOTA " + ZoneName(z) + " " + (Season)s + " stationary clear/rain/fog=" + F2(pi[0]) + "/" + F2(pi[1]) + "/" + F2(pi[2]));
                    double coinsDay1 = double.NaN, coinsDay4 = double.NaN, lastCoins = double.NaN, lastKg = double.NaN;
                    for (int d = 1; d <= Days; d++)
                    {
                        int tier = RodTierForDay(d);
                        double coins = 0, kg = 0, bestCoins = 0;
                        for (int band = 0; band < 6; band++)
                        {
                            int end = band == 5 ? DayClock.DayLengthMinutes : DayClock.BandStart[band + 1];
                            double seconds = (end - DayClock.BandStart[band]) / (double)DayClock.DefaultMinutesPerSecond;
                            for (int w = 0; w < 3; w++)
                            {
                                double sumC = 0, sumK = 0, maxC = 0;
                                int subs = _db.Zones[z].SubAreas.Count;
                                for (int sub = 0; sub < subs; sub++)
                                {
                                    FishingContext ctx = V5Context(z, sub, band, w, tier, (Season)s);
                                    Economy(_db, inp, in ctx, out double cpm, out double kpm);
                                    sumC += cpm / 60.0; sumK += kpm / 60.0;
                                    if (cpm / 60.0 > maxC) maxC = cpm / 60.0;
                                }
                                coins += pi[w] * seconds * sumC / subs;
                                kg += pi[w] * seconds * sumK / subs;
                                bestCoins += pi[w] * seconds * maxC;
                            }
                        }
                        string rodId = _registry.Rods.First(r => r.Tier == tier).Id;
                        TestContext.WriteLine("FG QUOTA " + ZoneName(z) + " " + (Season)s + " day=" + d + " rod=" + rodId + " coins=" + F1(coins) + " kg=" + F2(kg) + " bestCoins=" + F1(bestCoins));
                        bool ok = !double.IsNaN(coins) && !double.IsInfinity(coins) && !double.IsNaN(kg) && !double.IsInfinity(kg) && coins > 0 && kg > 0;
                        t.Add(ok, ZoneName(z) + " " + (Season)s + " day " + d + " coins=" + F1(coins) + " kg=" + F2(kg), "finite and > 0");
                        if (d == 1) coinsDay1 = coins;
                        if (d == 4) coinsDay4 = coins;
                        lastCoins = coins; lastKg = kg;
                    }
                    // Plateau (day 8+) = the Deep-sea day value (day 7).
                    TestContext.WriteLine("FG QUOTA " + ZoneName(z) + " " + (Season)s + " plateau coins=" + F1(lastCoins) + " kg=" + F2(lastKg) + " day4/day1=" + F2(coinsDay4 / coinsDay1));
                }
            t.Complete();
        }

        // Catches (mutation target): p_s without the (1 - j) factor, a wrong cycle term, wins/rates swapped. One context, every term hand-computed.
        [Test]
        public void EcologyBalance_V5_EconomyFormula_HandChecked()
        {
            // One zone: sub-area 0 with junk chance 0.2, one species (bluegill, weight 10, all gates open), one junk item of value 2.
            WeatherTableData w = EcologyCoreTestData.Weather();
            var bands = new[] { new SubAreaBandData { MinShoreM = 0f, MaxShoreM = float.PositiveInfinity, JunkChance = 0.2f } };
            var rules = new[] { EcologyCoreTestData.Rule(EcologyCoreTestData.Bluegill, 0, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 10f) };
            var junk = new[] { new JunkEntry { JunkIndex = EcologyCoreTestData.TinCan, Weight = 1f } };   // TinCan is worth 2 coins in the fixture
            var zone = new ZoneData(bands, rules, junk, in w, null);
            EcologyTuning et = EcologyCoreTestData.Tuning();
            var db = new EcologyDatabase(EcologyCoreTestData.Fishing(), new[] { zone }, new SpotEcologyData[0], in et);

            var inp = new EconomyInputs { MeanValueOf = i => (10.0, 2.0), FightOf = (p, tier) => (10.0, 0.5) };
            var ctx = new FishingContext
            {
                ZoneIndex = 0, SubAreaIndex = 0, Depth = 0.75f, Minute = 300, Season = Season.Summer, Weather = WeatherMask.Clear, RodTier = 0,
                Bait = BaitMods.Neutral(1), Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = 1,
            };
            Economy(db, inp, in ctx, out double coinsPerMin, out double kgPerMin);

            // Hand computation (FishingTestData.Tuning: charge 1, hook beat 0.25, bite wait 4..18, min 1, land distance 1, junk drag 3):
            //   j = 0.2, p = 0.8; waitMid = 11; depth mult at 0.75 m = 1 - 0.2 * 0.125 = 0.975 -> wait = 10.725 (species and junk, biteWaitMod 1)
            //   overhead = 1 + (0.45 + 0.012 * 12) + 0.25 + 1.5 + 10 = 13.344; junk fight = (12 - 1) / 3; species fight 10 s, landed 50%.
            double wait = 11.0 * 0.975;
            double cycle = 13.344 + 0.2 * (wait + 11.0 / 3.0) + 0.8 * (wait + 10.0);
            double coins = 0.8 * 0.5 * 10.0 + 0.2 * 2.0;
            double kg = 0.8 * 0.5 * 2.0;
            Assert.That(coinsPerMin, Is.EqualTo(60.0 * coins / cycle).Within(1e-3));
            Assert.That(kgPerMin, Is.EqualTo(60.0 * kg / cycle).Within(1e-3));
            Assert.That(coinsPerMin, Is.EqualTo(8.0482).Within(1e-3), "literal value of the hand computation");
        }

        // The stationary distribution (Clear, Rain, Fog) of the season's renormalised weather chain: 200 power iterations from Clear.
        static double[] Stationary(ZoneData zone, Season season)
        {
            var rows = new float[3][];
            WeatherMask[] masks = { WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };
            for (int r = 0; r < 3; r++)
            {
                rows[r] = new float[3];
                WeatherDirector.RowProbabilities(in zone.Weather, season, masks[r], rows[r]);
            }
            var pi = new double[] { 1.0, 0.0, 0.0 };
            for (int it = 0; it < 200; it++)
            {
                var next = new double[3];
                for (int from = 0; from < 3; from++)
                    for (int to = 0; to < 3; to++) next[to] += pi[from] * rows[from][to];
                pi = next;
            }
            return pi;
        }
    }
}
