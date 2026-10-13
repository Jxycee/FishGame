// T11 fightability (spec 12.9, M3-D38): the R72 Rhythm bot lands every rod-0 species with the Twig rod at >= 90%. The bot is R72's
// (FishingBots.RunFight, internal); this file only groups the species by fight profile and counts. Data comes from the generated assets (registry),
// never the seeds. A miss on the Rust-faithful baseline is recorded as an EcologyTuningItems entry (M3 never retunes R72 fight numbers).
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
    [Category("Tuning")]
    public class EcologyFightabilityTests
    {
        // key = test name, value = measured numbers and the reason. Needs the controller's approval.
        static readonly Dictionary<string, string> EcologyTuningItems = new Dictionary<string, string>();

        const int Fights = 200;
        const double LandTarget = 90.0;

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

        // Catches: a copy of the bot instead of R72's, seeds or hand-built data instead of the registry, a profile missed because its species
        // appears only in a second zone, specials left out of the rod-0 set.
        [Test]
        public void EcologyFightability_RhythmTwig_LandsEveryRodZeroSpeciesNinetyPercent()
        {
            var sw = Stopwatch.StartNew();
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(registry, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            EcologyDatabase db = registry.Ecology;
            Assert.That(db, Is.Not.Null, "the registry has no ecology database");

            // Rod-0 species: every species of a rule with minRodTier 0 in any zone (specials included), grouped by fight profile id.
            var byProfile = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (ZoneData zone in db.Zones)
                foreach (SpawnRuleData rule in zone.Rules)
                {
                    if (rule.MinRodTier != 0) continue;
                    FishSpeciesDef species = registry.SpeciesAt(rule.SpeciesIndex);
                    if (!byProfile.TryGetValue(species.Fight.Id, out SortedSet<string> set)) byProfile[species.Fight.Id] = set = new SortedSet<string>(StringComparer.Ordinal);
                    set.Add(species.Id);
                }
            Assert.That(byProfile.Count, Is.GreaterThan(0), "no rod-0 species");

            RodDef twigDef = registry.Rods.First(r => r.Tier == 0);
            RodStats twig = twigDef.ToCore();
            FishingTuning tuning = registry.FishingTuning.ToCore();

            var t = new Targets(nameof(EcologyFightability_RhythmTwig_LandsEveryRodZeroSpeciesNinetyPercent));
            foreach (KeyValuePair<string, SortedSet<string>> kv in byProfile)
            {
                FishFightProfile profile = registry.FightProfileAt(registry.FightProfileIndex(kv.Key)).ToCore();
                int landed = 0, snapped = 0, timeouts = 0;
                var landedTimes = new List<float>();
                for (int i = 0; i < Fights; i++)
                {
                    FishingBots.FightRecord rec = FishingBots.RunFight(FishingBots.BotKind.Rhythm, in profile, in twig, in tuning, i);
                    if (rec.Outcome == FightOutcome.Landed) { landed++; landedTimes.Add(rec.Elapsed); }
                    else if (rec.Outcome == FightOutcome.Snapped) snapped++;
                    else if (rec.Outcome == FightOutcome.Escaped && rec.Reason == EscapeReason.Timeout) timeouts++;
                }
                landedTimes.Sort();
                double median = landedTimes.Count == 0 ? double.NaN : landedTimes[Math.Max(0, (int)Math.Ceiling(0.5 * landedTimes.Count) - 1)];
                double landPct = 100.0 * landed / Fights;
                TestContext.WriteLine("FG FIGHT " + kv.Key + " " + twigDef.Id + " rhythm n=" + Fights + " land=" + F1(landPct) + "% snap=" + F1(100.0 * snapped / Fights)
                                      + "% to=" + F1(100.0 * timeouts / Fights) + "% med=" + F1(median) + "s species=" + string.Join(",", kv.Value));
                t.Add(landPct >= LandTarget, kv.Key + " land=" + F1(landPct) + "% (species " + string.Join(",", kv.Value) + ")", "land >= " + F1(LandTarget) + "%");
            }
            sw.Stop();
            TestContext.WriteLine("FG TIME EcologyFightabilityTests " + F1(sw.Elapsed.TotalSeconds) + "s");
            t.Complete();
        }
    }
}
