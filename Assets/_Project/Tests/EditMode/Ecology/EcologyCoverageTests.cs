// T11: V1-V3 and the 13.3 variety checks over the GENERATED assets (registry.Ecology), printing the FG ECO report lines (plan 0.6).
// Reads assets only, never seeds (plan 0.4). A miss that data cannot meet becomes an EcologyTuningItems entry, with the controller's approval.
using System;
using System.Collections.Generic;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace FishGame.Tests.EditMode
{
    public class EcologyCoverageTests
    {
        // key = test name, value = one line with the measured numbers and the reason. An entry turns a missed target into an ignored test.
        static readonly Dictionary<string, string> EcologyTuningItems = new Dictionary<string, string>();

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

        static ContentRegistry LoadRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(reg, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            Assert.That(reg.Ecology, Is.Not.Null, "the registry has no ecology database");
            return reg;
        }

        static List<string> Coverage(ContentRegistry reg)
        {
            return EcologyValidators.Coverage(reg.Ecology, EcologyValidators.NeutralRating(reg), reg.Fishing.Tuning.MinLandDepth,
                EcologyValidators.ZoneIds(reg), EcologyValidators.SubAreaIds(reg), EcologyValidators.SpeciesIds(reg));
        }

        static void PrintReport(ContentRegistry reg, string prefix)
        {
            List<string> lines = EcologyValidators.CoverageReport(reg.Ecology, EcologyValidators.NeutralRating(reg), reg.Fishing.Tuning.MinLandDepth,
                EcologyValidators.ZoneIds(reg), EcologyValidators.SubAreaIds(reg), EcologyValidators.SpeciesIds(reg));
            foreach (string line in lines) if (line.StartsWith(prefix, StringComparison.Ordinal)) TestContext.WriteLine(line);
        }

        // Catches: a data rule violated by the generated roster; a validator that was never wired into the data.
        [Test]
        public void Validation_Ecology_Pass()
        {
            List<string> errors = EcologyValidators.All();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        // Catches (V1): a sub-area with no passing non-special rule at rod 0 in some season / band / weather.
        [Test]
        public void EcologyCoverage_V1_NoDeadSubArea()
        {
            ContentRegistry reg = LoadRegistry();
            PrintReport(reg, "FG ECO V1");
            List<string> dead = Coverage(reg).Where(m => m.StartsWith("V1")).ToList();
            var t = new Targets(nameof(EcologyCoverage_V1_NoDeadSubArea));
            t.Add(dead.Count == 0, "dead contexts=" + dead.Count + (dead.Count > 0 ? " first: " + dead[0] : ""), "none");
            t.Complete();
        }

        // Catches (V2): fewer than 3 species or a species above 50% (mean of per-sub-area shares) in any zone / season / band / weather.
        [Test]
        public void EcologyCoverage_V2_ThreeSpeciesNoneAboveHalf()
        {
            ContentRegistry reg = LoadRegistry();
            PrintReport(reg, "FG ECO V2");
            List<string> bad = Coverage(reg).Where(m => m.StartsWith("V2")).ToList();
            var t = new Targets(nameof(EcologyCoverage_V2_ThreeSpeciesNoneAboveHalf));
            t.Add(bad.Count == 0, "V2 misses=" + bad.Count + (bad.Count > 0 ? " first: " + bad[0] : ""), "none");
            t.Complete();
        }

        // Catches (V3): a species no rule can ever reach at rod 3 (specials included).
        [Test]
        public void EcologyCoverage_V3_EverySpeciesPassesSomewhereAtRodThree()
        {
            ContentRegistry reg = LoadRegistry();
            PrintReport(reg, "FG ECO V3");
            List<string> bad = Coverage(reg).Where(m => m.StartsWith("V3")).ToList();
            var t = new Targets(nameof(EcologyCoverage_V3_EverySpeciesPassesSomewhereAtRodThree));
            t.Add(bad.Count == 0, "unreachable species=" + bad.Count + (bad.Count > 0 ? " first: " + bad[0] : ""), "none");
            t.Complete();
        }

        // Catches (13.3, CR-6): fewer than 4 eligible species, or no narrow-window species (at most two adjacent bands), in some (spot, season).
        // The literal single-band count is printed in the FG ECO VARIETY line (single=<n>).
        [Test]
        public void EcologyVariety_EverySpotSeason_FourEligibleAndOneSingleBand()
        {
            ContentRegistry reg = LoadRegistry();
            PrintReport(reg, "FG ECO VARIETY");
            List<string> bad = EcologyValidators.Variety(reg.Ecology, EcologyValidators.ZoneIds(reg), EcologyValidators.SpeciesIds(reg))
                .Where(m => m.StartsWith("Variety fewer than 4") || m.StartsWith("Variety no narrow-window")).ToList();
            var t = new Targets(nameof(EcologyVariety_EverySpotSeason_FourEligibleAndOneSingleBand));
            t.Add(bad.Count == 0, "variety misses=" + bad.Count + (bad.Count > 0 ? " first: " + bad[0] : ""), "none");
            t.Complete();
        }

        // Catches (13.3): the eligible sets of Morning and Night differing by fewer than 2 species in some (spot, season).
        [Test]
        public void EcologyVariety_MorningVsNight_DifferByTwoSpecies()
        {
            ContentRegistry reg = LoadRegistry();
            List<string> bad = EcologyValidators.Variety(reg.Ecology, EcologyValidators.ZoneIds(reg), EcologyValidators.SpeciesIds(reg))
                .Where(m => m.StartsWith("Variety Morning and Night")).ToList();
            var t = new Targets(nameof(EcologyVariety_MorningVsNight_DifferByTwoSpecies));
            t.Add(bad.Count == 0, "morning/night misses=" + bad.Count + (bad.Count > 0 ? " first: " + bad[0] : ""), "none");
            t.Complete();
        }
    }
}
