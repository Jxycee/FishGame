// T10: the M3 ecology seeds against spec 12.6 / 12.5 / 12.2 (a test-local literal copy), and the generated assets against the seeds.
// "assets" = loaded through the generated ContentRegistry asset (run Tools/unity.ps1 build first); "seeds" = EcologySeedData.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class EcologyContentTests
    {
        // Retuned values (T11/T12 only, with approval): key "<kind>.<owner>.<field>" -> the value the seed and asset carry instead of the spec's.
        // species.<id>.<field> | rule.<zone>.<speciesId>.<field> | band.<zone>.<subAreaId>.junkChance | junk.<zone>.<junkId>.weight
        internal static readonly Dictionary<string, float> EcologyTunedDeviations = new Dictionary<string, float>();

        private static float E(string key, float specValue)
        {
            return EcologyTunedDeviations.TryGetValue(key, out float tuned) ? tuned : specValue;
        }

        private static ContentRegistry LoadRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(reg, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            return reg;
        }

        // ---- the spec 12.6 literal (rows 1-23), kept in the spec's own shorthand and parsed here --------------------------------------

        // id | zones: sub-areas (";" between zones) | seasons | weather | bands | kg min | kg max | baseValue | Sw | rarity | rod | profile | tags
        private static readonly string[] Roster =
        {
            "mirror_minnow|lake: reeds, open_water|All|Any|Da Mo Mi Af|0.05|0.2|5|10|Common|0|Darter|Schooling",
            "bluegill|lake: reeds, open_water|Sp Su Au|Any|Mo Mi Af Du|0.2|0.6|8|10|Common|0|Steady|Schooling",
            "yellow_perch|lake: open_water, deep_water|Sp Au Wi|Any|Da Mo Mi Af Du Ni|0.2|1.2|9|10|Common|0|Darter|Schooling",
            "grumpy_carp|lake: open_water, deep_water|Su Au|Any|Mi Af Du Ni|2|12|10|9|Common|0|Bulldog|Bottom",
            "bucketmouth_bass|lake: deep_water, open_water|Sp Su|Any|Da Mo Du Ni|0.5|4|11|9|Common|0|Bulldog|Predator",
            "silver_mullet|lake: reeds, open_water; harbour: shallows, channel|All|Any|Du Ni Da|0.3|2|8|9|Common|0|Steady|Schooling",
            "lake_pike|lake: deep_water, reeds|Au Wi|Any|Da Mo Du Ni|1.5|9|20|9|Common|0|Runner|Predator",
            "sunny_trout|lake: open_water|Sp Su|Clear|Mi Af|0.5|3|30|12|Uncommon|0|Runner|Surface",
            "mudcat|lake: reeds, open_water|Sp Su Au|Rain, Fog|Du Ni Da|0.8|5|22|12|Uncommon|0|Steady|Bottom, Nocturnal",
            "frost_char|lake: deep_water, reeds|Wi|Any|Mi Af|0.4|2.5|26|10|Uncommon|0|Runner|-",
            "glimmer_eel|lake: deep_water|Su Au|Rain, Fog|Du Ni|0.5|2.5|60|8|Rare|1|Thrasher|Nocturnal",
            "sleepy_sturgeon|lake: deep_water|Sp Wi|Any|Mo Mi Af|6|25|75|3|Rare|2|Bulldog|Bottom",
            "pier_sardine|harbour: shallows, channel|Sp Su Au|Any|Da Mo Mi Af|0.1|0.3|5|10|Common|0|Darter|Schooling",
            "ice_herring|harbour: shallows, channel|Au Wi|Any|Da Mo Mi Af Du Ni|0.2|0.6|8|7|Common|0|Darter|Schooling",
            "grumble_mackerel|harbour: channel, open_sea|All|Any|Da Mo Mi Af|0.4|1.5|12|10|Common|0|Runner|Schooling",
            "moon_squid|harbour: open_sea, channel|All|Any|Du Ni|0.5|3|16|9|Common|0|Thrasher|Nocturnal",
            "gawping_gurnard|harbour: channel, shallows|Sp Su|Any|Mo Mi Af Du Ni|0.3|1.8|14|8|Common|0|Steady|Bottom",
            "sneaky_flounder|harbour: shallows, channel|Sp Au Wi|Any|Da Mo Mi Af|0.5|2|15|10|Common|0|Steady|Bottom",
            "red_snapper|harbour: channel|Su Au|Rain, Fog|Af Du Ni|1|4|28|12|Uncommon|0|Bulldog|Predator",
            "grumpy_puffer|harbour: channel, open_sea|Sp Su|Clear|Mi Af|0.3|1|32|12|Uncommon|1|Thrasher|Surface",
            "bruiser_tuna|harbour: open_sea|Su Au|Clear, Rain|Da Mo|8|40|80|3|Rare|2|Bulldog|Predator",
            "storm_ray|harbour: open_sea|Au Wi|Rain, Fog|Af Du|3|15|65|7|Rare|1|Thrasher|Bottom",
            "lanternfish|harbour: open_sea, channel|Au Wi|Fog|Du Ni|0.1|0.4|70|10|Rare|1|Darter|Nocturnal",
        };

        // id | shape | colour A | colour B | model file (plan 1.1; bluegill is T3's committed row; models per M3-D33)
        private static readonly string[] Presentation =
        {
            "mirror_minnow|Long|0.80,0.84,0.88|0.20,0.62,0.66|Fish_MirrorMinnow.fbx",
            "bluegill|Round|0.32,0.50,0.62|0.95,0.62,0.22|Fish_Bluegill.fbx",
            "yellow_perch|Round|0.62,0.68,0.30|0.20,0.24,0.16|Fish_Perch.fbx",
            "grumpy_carp|Round|0.72,0.58,0.30|0.45,0.34,0.18|Fish_Carp.fbx",
            "bucketmouth_bass|Round|0.36,0.50,0.26|0.88,0.86,0.70|Fish_BucketmouthBass.fbx",
            "silver_mullet|Long|0.70,0.74,0.78|0.30,0.36,0.44|Fish_SilverMullet.fbx",
            "lake_pike|Long|0.40,0.52,0.30|0.86,0.84,0.52|Fish_LakePike.fbx",
            "sunny_trout|Long|0.55,0.62,0.55|0.88,0.45,0.48|Fish_Trout.fbx",
            "mudcat|Round|0.36,0.34,0.30|0.70,0.66,0.56|Fish_Catfish.fbx",
            "frost_char|Long|0.62,0.76,0.90|0.92,0.40,0.30|Fish_FrostChar.fbx",
            "glimmer_eel|Eel|0.16,0.22,0.40|0.40,0.95,0.85|Fish_GlimmerEel.fbx",
            "sleepy_sturgeon|Long|0.42,0.40,0.38|0.78,0.72,0.60|Fish_SleepySturgeon.fbx",
            "pier_sardine|Long|0.52,0.66,0.80|0.92,0.94,0.96|Fish_PierSardine.fbx",
            "ice_herring|Long|0.78,0.88,0.96|0.30,0.44,0.62|Fish_IceHerring.fbx",
            "grumble_mackerel|Long|0.20,0.48,0.46|0.10,0.14,0.20|Fish_GrumbleMackerel.fbx",
            "moon_squid|Squid|0.88,0.86,0.96|0.58,0.40,0.80|Fish_MoonSquid.fbx",
            "gawping_gurnard|Round|0.86,0.36,0.28|0.98,0.84,0.62|Fish_GawpingGurnard.fbx",
            "sneaky_flounder|Flat|0.62,0.54,0.40|0.30,0.26,0.20|Fish_SneakyFlounder.fbx",
            "red_snapper|Round|0.86,0.24,0.22|0.98,0.70,0.66|Fish_RedSnapper.fbx",
            "grumpy_puffer|Round|0.92,0.82,0.40|0.40,0.30,0.20|Fish_GrumpyPuffer.fbx",
            "bruiser_tuna|Long|0.16,0.24,0.44|0.80,0.84,0.88|Fish_BruiserTuna.fbx",
            "storm_ray|Flat|0.32,0.36,0.44|0.84,0.88,0.94|Fish_StormRay.fbx",
            "lanternfish|Long|0.10,0.12,0.22|1.00,0.90,0.40|Fish_Lanternfish.fbx",
            "gilded_carp|Round|0.95,0.76,0.25|0.70,0.46,0.12|Fish_GildedCarp.fbx",
            "old_barnacle|Round|0.40,0.44,0.40|0.86,0.82,0.74|Fish_OldBarnacle.fbx",
        };

        private static SeasonMask ParseSeasons(string s)
        {
            if (s == "All") return SeasonMask.All;
            SeasonMask m = SeasonMask.None;
            foreach (string t in s.Split(' '))
            {
                switch (t)
                {
                    case "Sp": m |= SeasonMask.Spring; break;
                    case "Su": m |= SeasonMask.Summer; break;
                    case "Au": m |= SeasonMask.Autumn; break;
                    case "Wi": m |= SeasonMask.Winter; break;
                    default: throw new ArgumentException("season " + t);
                }
            }
            return m;
        }

        private static WeatherMask ParseWeather(string s)
        {
            if (s == "Any") return WeatherMask.All;
            WeatherMask m = WeatherMask.None;
            foreach (string t in s.Split(new[] { ", " }, StringSplitOptions.None))
            {
                switch (t)
                {
                    case "Clear": m |= WeatherMask.Clear; break;
                    case "Rain": m |= WeatherMask.Rain; break;
                    case "Fog": m |= WeatherMask.Fog; break;
                    default: throw new ArgumentException("weather " + t);
                }
            }
            return m;
        }

        private static TimeBandMask ParseBands(string s)
        {
            TimeBandMask m = TimeBandMask.None;
            foreach (string t in s.Split(' '))
            {
                switch (t)
                {
                    case "Da": m |= TimeBandMask.Dawn; break;
                    case "Mo": m |= TimeBandMask.Morning; break;
                    case "Mi": m |= TimeBandMask.Midday; break;
                    case "Af": m |= TimeBandMask.Afternoon; break;
                    case "Du": m |= TimeBandMask.Dusk; break;
                    case "Ni": m |= TimeBandMask.Night; break;
                    default: throw new ArgumentException("band " + t);
                }
            }
            return m;
        }

        private static FishTags ParseTags(string s)
        {
            if (s == "-") return FishTags.None;
            FishTags m = FishTags.None;
            foreach (string t in s.Split(new[] { ", " }, StringSplitOptions.None))
                m |= (FishTags)Enum.Parse(typeof(FishTags), t);
            return m;
        }

        private static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

        private static Color ParseColor(string s)
        {
            string[] p = s.Split(',');
            return new Color(F(p[0]), F(p[1]), F(p[2]), 1f);
        }

        private static void AssertColor(Color expected, Color actual, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r), what + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g), what + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b), what + " b");
            Assert.That(actual.a, Is.EqualTo(expected.a), what + " a");
        }

        private static SpeciesSeed SeedSpecies(string id)
        {
            foreach (SpeciesSeed s in EcologySeedData.Species) if (s.Id == id) return s;
            Assert.Fail("no species seed " + id);
            return default;
        }

        private static ZoneSeed SeedZone(string id)
        {
            foreach (ZoneSeed z in EcologySeedData.Zones) if (z.Id == id) return z;
            Assert.Fail("no zone seed " + id);
            return default;
        }

        private static RuleSeed[] RulesOf(ZoneSeed zone, string speciesId)
        {
            return zone.Rules.Where(r => r.SpeciesId == speciesId).ToArray();
        }

        private static string ZoneId(string shorthand)
        {
            return shorthand == "lake" ? "lake_dock" : "harbour_pier";
        }

        // Catches: a transcription error; a re-derived number; a missing second-zone rule; the bluegill row drifting from T3's committed row.
        [Test]
        public void EcologyData_SeedsMatchRoster()
        {
            Assert.That(EcologySeedData.Species.Count, Is.EqualTo(25), "23 regular + 2 specials");
            var expectedRulesPerZone = new Dictionary<string, int> { { "lake_dock", 0 }, { "harbour_pier", 0 } };

            foreach (string line in Roster)
            {
                string[] c = line.Split('|');
                string id = c[0];
                SpeciesSeed s = SeedSpecies(id);
                string k = "species." + id + ".";
                Assert.That(s.Data.WeightMinG, Is.EqualTo((int)E(k + "weightMinG", (int)Math.Round(F(c[5]) * 1000))), id + " weightMinG");
                Assert.That(s.Data.WeightMaxG, Is.EqualTo((int)E(k + "weightMaxG", (int)Math.Round(F(c[6]) * 1000))), id + " weightMaxG");
                Assert.That(s.Data.BaseValue, Is.EqualTo((int)E(k + "baseValue", int.Parse(c[7]))), id + " baseValue");
                Assert.That(s.Data.Rarity, Is.EqualTo((FishRarity)Enum.Parse(typeof(FishRarity), c[9])), id + " rarity");
                Assert.That(s.ProfileId, Is.EqualTo(c[11].ToLowerInvariant()), id + " profile");
                Assert.That(s.Data.Tags, Is.EqualTo(ParseTags(c[12])), id + " tags");
                Assert.That(s.Data.IsSpecial, Is.False, id + " isSpecial");
                // M3-D35 constants (bluegill is T3's committed row: 2.2 / 1.0 / 0 / K 1.0 as well).
                Assert.That(s.Data.WeightSkew, Is.EqualTo(2.2f), id + " weightSkew");
                Assert.That(s.Data.BiteWaitMod, Is.EqualTo(1.0f), id + " biteWaitMod");
                Assert.That(s.Data.HookWindow, Is.EqualTo(0f), id + " hookWindow");
                Assert.That(s.Data.LengthShapeK, Is.EqualTo(id == "glimmer_eel" ? 1.6f : id == "grumpy_puffer" ? 0.8f : 1.0f), id + " lengthShapeK");

                string seasons = c[2], weather = c[3], bands = c[4];
                foreach (string zonePart in c[1].Split(new[] { "; " }, StringSplitOptions.None))
                {
                    string[] zp = zonePart.Split(new[] { ": " }, StringSplitOptions.None);
                    string zoneId = ZoneId(zp[0]);
                    RuleSeed[] rules = RulesOf(SeedZone(zoneId), id);
                    Assert.That(rules.Length, Is.EqualTo(1), id + " has exactly one rule in " + zoneId);
                    RuleSeed r = rules[0];
                    expectedRulesPerZone[zoneId]++;
                    string rk = "rule." + zoneId + "." + id + ".";
                    Assert.That(new HashSet<string>(r.SubAreaIds), Is.EquivalentTo(zp[1].Split(new[] { ", " }, StringSplitOptions.None)), id + " sub-areas in " + zoneId);
                    Assert.That(r.Seasons, Is.EqualTo(ParseSeasons(seasons)), id + " seasons");
                    Assert.That(r.Weather, Is.EqualTo(ParseWeather(weather)), id + " weather");
                    Assert.That(r.Bands, Is.EqualTo(ParseBands(bands)), id + " bands");
                    Assert.That(r.Weight, Is.EqualTo(E(rk + "weight", F(c[8]))), id + " weight (Sw)");
                    Assert.That(r.MinRodTier, Is.EqualTo(byte.Parse(c[10])), id + " minRodTier");
                    Assert.That(r.Special, Is.False, id + " special");
                    Assert.That(r.SpecialChance, Is.EqualTo(0f), id + " specialChance");
                    Assert.That(r.CatchLimitPerRun, Is.EqualTo(-1), id + " catchLimitPerRun");
                }
            }

            // The two specials (spec 12.6): independent roll, limit 1, Legendary, Bulldog, value 200 / 250, chance 0.02 / 0.015.
            AssertSpecial("gilded_carp", "lake_dock", "deep_water", E("rule.lake_dock.gilded_carp.specialChance", 0.02f), 3000, 15000, 200);
            AssertSpecial("old_barnacle", "harbour_pier", "open_sea", E("rule.harbour_pier.old_barnacle.specialChance", 0.015f), 5000, 20000, 250);
            expectedRulesPerZone["lake_dock"]++;
            expectedRulesPerZone["harbour_pier"]++;
            Assert.That(SeedZone("lake_dock").Rules.Length, Is.EqualTo(expectedRulesPerZone["lake_dock"]), "no extra lake rule");
            Assert.That(SeedZone("harbour_pier").Rules.Length, Is.EqualTo(expectedRulesPerZone["harbour_pier"]), "no extra harbour rule");

            // Presentation (plan 1.1): shape, colours, model file.
            foreach (string line in Presentation)
            {
                string[] c = line.Split('|');
                SpeciesSeed s = SeedSpecies(c[0]);
                Assert.That(s.BodyShape, Is.EqualTo((BodyShape)Enum.Parse(typeof(BodyShape), c[1])), c[0] + " body shape");
                AssertColor(ParseColor(c[2]), s.ColorA, c[0] + " colorA");
                AssertColor(ParseColor(c[3]), s.ColorB, c[0] + " colorB");
                Assert.That(s.ModelFile, Is.EqualTo(c[4]), c[0] + " model file");
            }

            // The bluegill row equals T3's committed row: 200-600 g, value 8, steady, biteWaitMod 1.0, Schooling, gates 0/0, M2 colours, Fish_Bluegill.fbx.
            SpeciesSeed bg = SeedSpecies("bluegill");
            Assert.That(bg.Data.WeightMinG, Is.EqualTo(200));
            Assert.That(bg.Data.WeightMaxG, Is.EqualTo(600));
            Assert.That(bg.Data.BaseValue, Is.EqualTo(8));
            Assert.That(bg.Data.BiteWaitMod, Is.EqualTo(1.0f));
            Assert.That(bg.Data.Tags, Is.EqualTo(FishTags.Schooling));

            // Seed order = plan 1.1 order: rows 1-23, then the specials.
            Assert.That(EcologySeedData.Species.Select(x => x.Id).ToArray(), Is.EqualTo(Presentation.Select(p => p.Split('|')[0]).ToArray()));
        }

        private static void AssertSpecial(string id, string zoneId, string subArea, float chance, int gMin, int gMax, int value)
        {
            SpeciesSeed s = SeedSpecies(id);
            Assert.That(s.Data.IsSpecial, Is.True, id + " isSpecial");
            Assert.That(s.Data.Rarity, Is.EqualTo(FishRarity.Legendary), id + " rarity");
            Assert.That(s.ProfileId, Is.EqualTo("bulldog"), id + " profile");
            Assert.That(s.Data.WeightMinG, Is.EqualTo(gMin), id + " weightMinG");
            Assert.That(s.Data.WeightMaxG, Is.EqualTo(gMax), id + " weightMaxG");
            Assert.That(s.Data.BaseValue, Is.EqualTo((int)E("species." + id + ".baseValue", value)), id + " baseValue");
            RuleSeed[] rules = RulesOf(SeedZone(zoneId), id);
            Assert.That(rules.Length, Is.EqualTo(1), id + " has one rule in " + zoneId);
            RuleSeed r = rules[0];
            Assert.That(r.SubAreaIds, Is.EqualTo(new[] { subArea }), id + " sub-area");
            Assert.That(r.Special, Is.True, id + " special");
            Assert.That(r.SpecialChance, Is.EqualTo(chance), id + " specialChance");
            Assert.That(r.CatchLimitPerRun, Is.EqualTo(1), id + " catchLimitPerRun");
            Assert.That(r.Seasons, Is.EqualTo(SeasonMask.All), id + " seasons");
            Assert.That(r.Weather, Is.EqualTo(WeatherMask.All), id + " weather");
            Assert.That(r.Bands, Is.EqualTo(TimeBandMask.All), id + " bands");
            Assert.That(r.MinRodTier, Is.EqualTo((byte)0), id + " rod");
            Assert.That(r.Weight, Is.EqualTo(0f), id + " weight is 0 (ignored for specials)");
            string other = zoneId == "lake_dock" ? "harbour_pier" : "lake_dock";
            Assert.That(RulesOf(SeedZone(other), id).Length, Is.EqualTo(0), id + " is in one zone only");
        }

        // Catches: a duplicated boot asset; unequal weights; a wrong junk list per spot.
        [Test]
        public void EcologyData_SevenJunkOldBootShared()
        {
            var expected = new (string id, string name, int value, BodyShape shape)[]
            {
                ("old_boot", "Old Boot", 2, BodyShape.Long), ("tin_can", "Tin Can", 2, BodyShape.Round),
                ("soggy_newspaper", "Soggy Newspaper", 0, BodyShape.Flat), ("lost_sock", "Lost Sock", 1, BodyShape.Long),
                ("rope_scrap", "Rope Scrap", 1, BodyShape.Long), ("seaweed_clump", "Seaweed Clump", 0, BodyShape.Round),
                ("empty_bait_tub", "Empty Bait Tub", 2, BodyShape.Round),
            };
            Assert.That(EcologySeedData.Junk.Count, Is.EqualTo(7));
            foreach (var e in expected)
            {
                var j = EcologySeedData.Junk.First(x => x.id == e.id);
                Assert.That(j.name, Is.EqualTo(e.name), e.id + " name");
                Assert.That(j.value, Is.EqualTo((int)E("junk." + e.id + ".valueCoins", e.value)), e.id + " value");
                Assert.That(j.shape, Is.EqualTo(e.shape), e.id + " shape");
            }
            AssertColor(new Color(0.30f, 0.22f, 0.16f, 1f), EcologySeedData.JunkColor("old_boot"), "old_boot (M2 value)");
            AssertColor(new Color(0.68f, 0.70f, 0.72f, 1f), EcologySeedData.JunkColor("tin_can"), "tin_can (M2 value)");

            Assert.That(SeedZone("lake_dock").Junk.Select(j => j.junkId).ToArray(), Is.EqualTo(new[] { "old_boot", "tin_can", "soggy_newspaper", "lost_sock" }));
            Assert.That(SeedZone("harbour_pier").Junk.Select(j => j.junkId).ToArray(), Is.EqualTo(new[] { "old_boot", "rope_scrap", "seaweed_clump", "empty_bait_tub" }));
            foreach (ZoneSeed z in EcologySeedData.Zones)
            {
                float first = z.Junk[0].weight;
                foreach (var j in z.Junk) Assert.That(j.weight, Is.EqualTo(E("junk." + z.Id + "." + j.junkId + ".weight", first)).Or.EqualTo(first), z.Id + " " + j.junkId + " weight");
            }

            // Assets: both zones reference the SAME old_boot JunkDef object.
            ContentRegistry reg = LoadRegistry();
            JunkDef lakeBoot = reg.Zones.First(z => z.Id == "lake_dock").Junk.First(j => j.junk.Id == "old_boot").junk;
            JunkDef harbourBoot = reg.Zones.First(z => z.Id == "harbour_pier").Junk.First(j => j.junk.Id == "old_boot").junk;
            Assert.That(lakeBoot, Is.SameAs(harbourBoot), "old_boot is shared by both spots");
            Assert.That(reg.Junk.Count(j => j.Id == "old_boot"), Is.EqualTo(1), "one boot asset");
        }

        // Catches: an orphan asset; a seed without its asset; wiring the harbour spot too early (it would fail the build-list check of Validators.ContentIds).
        [Test]
        public void EcologyData_AssetsExistForEverySeed()
        {
            ContentRegistry reg = LoadRegistry();
            foreach (SpeciesSeed s in EcologySeedData.Species)
            {
                var a = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetPaths.SpeciesFolder + "/" + s.Id + ".asset");
                Assert.That(a, Is.Not.Null, "missing species asset " + s.Id);
                Assert.That(a.Id, Is.EqualTo(s.Id));
                Assert.That(reg.Species, Contains.Item(a), "species " + s.Id + " is listed in the registry");
            }
            foreach (var j in EcologySeedData.Junk)
            {
                var a = AssetDatabase.LoadAssetAtPath<JunkDef>(AssetPaths.JunkFolder + "/" + j.id + ".asset");
                Assert.That(a, Is.Not.Null, "missing junk asset " + j.id);
                Assert.That(a.Id, Is.EqualTo(j.id));
                Assert.That(reg.Junk, Contains.Item(a), "junk " + j.id + " is listed in the registry");
            }

            Assert.That(reg.Zones.Count, Is.EqualTo(2), "registry zones: lake_dock and harbour_pier");
            foreach (ZoneSeed z in EcologySeedData.Zones)
            {
                var zone = AssetDatabase.LoadAssetAtPath<FishingZoneDef>(AssetPaths.ZonesFolder + "/" + z.Id + ".asset");
                Assert.That(zone, Is.Not.Null, "missing zone asset " + z.Id);
                Assert.That(zone.Id, Is.EqualTo(z.Id));
                Assert.That(reg.Zones, Contains.Item(zone));
                var grid = AssetDatabase.LoadAssetAtPath<ShoreGridDef>(AssetPaths.ShoreGridsFolder + "/" + z.Id + "_grid.asset");
                Assert.That(grid, Is.Not.Null, "missing shore grid asset " + z.Id);
                Assert.That(grid.Id, Is.EqualTo(z.Id + "_grid"));
                Assert.That(zone.ShoreGrid, Is.SameAs(grid), z.Id + " uses its grid asset");
            }
            var weather = AssetDatabase.LoadAssetAtPath<WeatherTableDef>(AssetPaths.WeatherTableAsset);
            Assert.That(weather, Is.Not.Null, "missing " + AssetPaths.WeatherTableAsset);
            foreach (FishingZoneDef zone in reg.Zones) Assert.That(zone.Weather, Is.SameAs(weather), zone.Id + " shares the weather table (M3-D44)");
            var tuning = AssetDatabase.LoadAssetAtPath<EcologyTuningDef>(AssetPaths.EcologyTuningAsset);
            Assert.That(tuning, Is.Not.Null, "missing " + AssetPaths.EcologyTuningAsset);
            Assert.That(reg.EcologyTuning, Is.SameAs(tuning));

            var lakeSpot = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.LakeDockSpotAsset);
            Assert.That(lakeSpot.Zone, Is.SameAs(reg.Zones.First(z => z.Id == "lake_dock")), "lake_dock spot zone");
            Assert.That(lakeSpot.MapOrder, Is.EqualTo(0));
            var harbour = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.HarbourPierSpotAsset);
            Assert.That(harbour, Is.Not.Null, "missing " + AssetPaths.HarbourPierSpotAsset);
            Assert.That(harbour.Id, Is.EqualTo("harbour_pier"));
            Assert.That(harbour.DisplayName, Is.EqualTo("Harbour Pier"));
            Assert.That(harbour.SceneName, Is.EqualTo("Spot_HarbourPier"));
            Assert.That(harbour.MapOrder, Is.EqualTo(1));
            Assert.That(harbour.Zone, Is.SameAs(reg.Zones.First(z => z.Id == "harbour_pier")));
            bool inBuildOrder = Array.IndexOf(SceneNames.BuildOrder, "Spot_HarbourPier") >= 0;
            Assert.That(reg.Spots.Contains(harbour), Is.EqualTo(inBuildOrder), "the harbour spot is registered iff its scene is in the build order (CR-4)");
        }

        // Catches: wrong reuse mapping; models not filled on the first build.
        [Test]
        public void EcologyData_ModelFiles_ReuseFourM2Models()
        {
            var reused = new Dictionary<string, string>
            {
                { "yellow_perch", "Fish_Perch.fbx" }, { "grumpy_carp", "Fish_Carp.fbx" }, { "sunny_trout", "Fish_Trout.fbx" },
                { "mudcat", "Fish_Catfish.fbx" }, { "bluegill", "Fish_Bluegill.fbx" },
            };
            var files = new HashSet<string>();
            foreach (SpeciesSeed s in EcologySeedData.Species)
            {
                if (reused.TryGetValue(s.Id, out string file)) Assert.That(s.ModelFile, Is.EqualTo(file), s.Id + " reuses " + file);
                else Assert.That(s.ModelFile, Is.EqualTo("Fish_" + Pascal(s.Id) + ".fbx"), s.Id + " model file");
                Assert.That(files.Add(s.ModelFile), Is.True, "model file " + s.ModelFile + " is used by one species only");
            }
            Assert.That(files.Count, Is.EqualTo(25));

            ContentRegistry reg = LoadRegistry();
            foreach (KeyValuePair<string, string> kv in reused)
            {
                Mesh mesh = null;
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(AssetPaths.ModelsFolder + "/" + kv.Value))
                    if (o is Mesh m) { mesh = m; break; }
                if (mesh == null || !FishingContentBuilder.IsFishModelMesh(mesh)) continue;   // a missing or rejected model leaves the fallback fish (C 2.8 #28)
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetPaths.SpeciesFolder + "/" + kv.Key + ".asset");
                Assert.That(def.Model, Is.SameAs(mesh), kv.Key + " has the reused model " + kv.Value);
            }
            Assert.That(reg.Species.Count, Is.EqualTo(25));
        }

        private static string Pascal(string id)
        {
            return string.Concat(id.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        // Catches: a hand-edited YAML typo; a retune applied to only one of asset and seed.
        [Test]
        public void EcologyData_AssetValuesEqualSeeds()
        {
            ContentRegistry reg = LoadRegistry();
            foreach (SpeciesSeed s in EcologySeedData.Species)
            {
                var a = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetPaths.SpeciesFolder + "/" + s.Id + ".asset");
                Assert.That(a, Is.Not.Null, s.Id);
                string l = "species " + s.Id + " ";
                Assert.That(a.DisplayName, Is.EqualTo(s.Name), l + "name");
                Assert.That(a.Rarity, Is.EqualTo(s.Data.Rarity), l + "rarity");
                Assert.That(a.BaseValue, Is.EqualTo(s.Data.BaseValue), l + "baseValue");
                Assert.That(a.WeightMinG, Is.EqualTo(s.Data.WeightMinG), l + "weightMinG");
                Assert.That(a.WeightMaxG, Is.EqualTo(s.Data.WeightMaxG), l + "weightMaxG");
                Assert.That(a.WeightSkew, Is.EqualTo(s.Data.WeightSkew), l + "weightSkew");
                Assert.That(a.LengthShapeK, Is.EqualTo(s.Data.LengthShapeK), l + "lengthShapeK");
                Assert.That(a.Fight.Id, Is.EqualTo(s.ProfileId), l + "fight");
                Assert.That(a.BodyShape, Is.EqualTo(s.BodyShape), l + "body shape");
                AssertColor(s.ColorA, a.ColorA, l + "colorA");
                AssertColor(s.ColorB, a.ColorB, l + "colorB");
                Assert.That(a.BiteWaitMod, Is.EqualTo(s.Data.BiteWaitMod), l + "biteWaitMod");
                Assert.That(a.HookWindow, Is.EqualTo(s.Data.HookWindow), l + "hookWindow");
                Assert.That(a.IsSpecial, Is.EqualTo(s.Data.IsSpecial), l + "isSpecial");
                Assert.That(a.Tags, Is.EqualTo(s.Data.Tags), l + "tags");
            }
            foreach (var j in EcologySeedData.Junk)
            {
                var a = AssetDatabase.LoadAssetAtPath<JunkDef>(AssetPaths.JunkFolder + "/" + j.id + ".asset");
                Assert.That(a, Is.Not.Null, j.id);
                Assert.That(a.DisplayName, Is.EqualTo(j.name), "junk " + j.id + " name");
                Assert.That(a.ValueCoins, Is.EqualTo(j.value), "junk " + j.id + " value");
                Assert.That(a.BodyShape, Is.EqualTo(j.shape), "junk " + j.id + " shape");
                AssertColor(EcologySeedData.JunkColor(j.id), a.Color, "junk " + j.id + " colour");
            }

            foreach (ZoneSeed seed in EcologySeedData.Zones)
            {
                FishingZoneDef zone = reg.Zones.First(z => z.Id == seed.Id);
                string l = "zone " + seed.Id + " ";
                Assert.That(zone.DisplayName, Is.EqualTo(seed.Name), l + "name");
                Assert.That(zone.SubAreas.Count, Is.EqualTo(seed.Bands.Length), l + "sub-area count");
                for (int i = 0; i < seed.Bands.Length; i++)
                {
                    Assert.That(zone.SubAreas[i].id, Is.EqualTo(seed.BandIds[i]), l + "band id " + i);
                    Assert.That(zone.SubAreas[i].minShoreM, Is.EqualTo(seed.Bands[i].MinShoreM), l + "band min " + i);
                    Assert.That(zone.SubAreas[i].maxShoreM, Is.EqualTo(seed.Bands[i].MaxShoreM), l + "band max " + i);
                    Assert.That(zone.SubAreas[i].junkChance, Is.EqualTo(seed.Bands[i].JunkChance), l + "band junkChance " + i);
                }
                Assert.That(zone.Rules.Count, Is.EqualTo(seed.Rules.Length), l + "rule count");
                for (int i = 0; i < seed.Rules.Length; i++)
                {
                    RuleSeed rs = seed.Rules[i];
                    SpawnRule r = zone.Rules[i];
                    string rl = l + "rule " + i + " (" + rs.SpeciesId + ") ";
                    Assert.That(r.fish.Id, Is.EqualTo(rs.SpeciesId), rl + "fish");
                    Assert.That(r.subAreaIds, Is.EqualTo(rs.SubAreaIds), rl + "sub-areas");
                    Assert.That(r.seasons, Is.EqualTo(rs.Seasons), rl + "seasons");
                    Assert.That(r.weather, Is.EqualTo(rs.Weather), rl + "weather");
                    Assert.That(r.bands, Is.EqualTo(rs.Bands), rl + "bands");
                    Assert.That(r.weight, Is.EqualTo(rs.Weight), rl + "weight");
                    Assert.That(r.minRodTier, Is.EqualTo(rs.MinRodTier), rl + "minRodTier");
                    Assert.That(r.minBaitRating, Is.EqualTo(0), rl + "minBaitRating");
                    Assert.That(r.minDepth, Is.EqualTo(0f), rl + "minDepth");
                    Assert.That(r.special, Is.EqualTo(rs.Special), rl + "special");
                    Assert.That(r.specialChance, Is.EqualTo(rs.SpecialChance), rl + "specialChance");
                    Assert.That(r.catchLimitPerRun, Is.EqualTo(rs.CatchLimitPerRun), rl + "catchLimitPerRun");
                    Assert.That(r.minPlayersInSession, Is.EqualTo(0), rl + "minPlayersInSession");
                }
                Assert.That(zone.Junk.Count, Is.EqualTo(seed.Junk.Length), l + "junk count");
                for (int i = 0; i < seed.Junk.Length; i++)
                {
                    Assert.That(zone.Junk[i].junk.Id, Is.EqualTo(seed.Junk[i].junkId), l + "junk " + i);
                    Assert.That(zone.Junk[i].weight, Is.EqualTo(seed.Junk[i].weight), l + "junk weight " + i);
                }
            }

            WeatherTableData w = EcologySeedData.Weather;
            var table = AssetDatabase.LoadAssetAtPath<WeatherTableDef>(AssetPaths.WeatherTableAsset);
            Assert.That(table.Id, Is.EqualTo("weather_temperate"));
            Assert.That(table.BaseMatrix.ToArray(), Is.EqualTo(w.Base), "weather base matrix");
            Assert.That(table.SeasonColumnMult.ToArray(), Is.EqualTo(w.SeasonColumnMult), "weather season multipliers");

            EcologyTuning t = EcologySeedData.Tuning;
            EcologyTuning at = AssetDatabase.LoadAssetAtPath<EcologyTuningDef>(AssetPaths.EcologyTuningAsset).ToCore();
            Assert.That(at.HotspotFirstMinMin, Is.EqualTo(t.HotspotFirstMinMin));
            Assert.That(at.HotspotFirstMinMax, Is.EqualTo(t.HotspotFirstMinMax));
            Assert.That(at.HotspotGapMin, Is.EqualTo(t.HotspotGapMin));
            Assert.That(at.HotspotGapMax, Is.EqualTo(t.HotspotGapMax));
            Assert.That(at.HotspotDurationMin, Is.EqualTo(t.HotspotDurationMin));
            Assert.That(at.HotspotDurationMax, Is.EqualTo(t.HotspotDurationMax));
            Assert.That(at.HotspotRadiusM, Is.EqualTo(t.HotspotRadiusM));

            var harbour = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.HarbourPierSpotAsset);
            Assert.That(harbour.MapOrder, Is.EqualTo(1));

            SunMoonSettings sm = EcologySeedData.SunMoon, asm = reg.ArtPalette.SunMoon;
            Assert.That(asm.sunriseFraction, Is.EqualTo(sm.sunriseFraction));
            Assert.That(asm.sunsetFraction, Is.EqualTo(sm.sunsetFraction));
            Assert.That(asm.minElevationDeg, Is.EqualTo(sm.minElevationDeg));
            Assert.That(asm.flipWindow, Is.EqualTo(sm.flipWindow));
            Assert.That(asm.moonIntensity, Is.EqualTo(sm.moonIntensity));
            Assert.That(asm.moonEulerDeg, Is.EqualTo(sm.moonEulerDeg));
            AssertColor(sm.moonTint, asm.moonTint, "moonTint");
            Assert.That(asm.blendWindow, Is.EqualTo(sm.blendWindow));
            WeatherAmbient[] looks = EcologySeedData.WeatherLooks;
            Assert.That(reg.ArtPalette.WeatherLooks.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                WeatherAmbient a = reg.ArtPalette.WeatherLooks[i];
                Assert.That(a.fogDensityMult, Is.EqualTo(looks[i].fogDensityMult), "look " + i + " fogDensityMult");
                Assert.That(a.fogDensityMin, Is.EqualTo(looks[i].fogDensityMin), "look " + i + " fogDensityMin");
                Assert.That(a.lightMult, Is.EqualTo(looks[i].lightMult), "look " + i + " lightMult");
                Assert.That(a.desaturate, Is.EqualTo(looks[i].desaturate), "look " + i + " desaturate");
                Assert.That(a.rainAmount, Is.EqualTo(looks[i].rainAmount), "look " + i + " rainAmount");
            }
            SeasonAmbience[] amb = EcologySeedData.SeasonAmbience;
            Assert.That(reg.SfxCueTable.SeasonAmbience.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                SeasonAmbience a = reg.SfxCueTable.SeasonAmbience[i];
                Assert.That(a.windGain, Is.EqualTo(amb[i].windGain), "season " + i + " windGain");
                Assert.That(a.windLfoHz, Is.EqualTo(amb[i].windLfoHz), "season " + i + " windLfoHz");
                Assert.That(a.lapIntervalMin, Is.EqualTo(amb[i].lapIntervalMin), "season " + i + " lapIntervalMin");
                Assert.That(a.lapIntervalMax, Is.EqualTo(amb[i].lapIntervalMax), "season " + i + " lapIntervalMax");
            }

            // The seed-built database gives the same zones as the registry's (grids aside).
            EcologyDatabase seedDb = EcologySeedData.BuildDatabase();
            EcologyDatabase assetDb = reg.Ecology;
            Assert.That(assetDb, Is.Not.Null);
            Assert.That(assetDb.Zones.Count, Is.EqualTo(seedDb.Zones.Count));
            for (int z = 0; z < seedDb.Zones.Count; z++)
            {
                ZoneData a = assetDb.Zones[z], b = seedDb.Zones[z];
                string l = "zone index " + z + " ";
                Assert.That(a.SubAreas.Count, Is.EqualTo(b.SubAreas.Count), l + "sub-areas");
                for (int i = 0; i < a.SubAreas.Count; i++)
                {
                    Assert.That(a.SubAreas[i].MinShoreM, Is.EqualTo(b.SubAreas[i].MinShoreM), l + "min " + i);
                    Assert.That(a.SubAreas[i].MaxShoreM, Is.EqualTo(b.SubAreas[i].MaxShoreM), l + "max " + i);
                    Assert.That(a.SubAreas[i].JunkChance, Is.EqualTo(b.SubAreas[i].JunkChance), l + "junk chance " + i);
                }
                Assert.That(a.Rules.Count, Is.EqualTo(b.Rules.Count), l + "rules");
                for (int i = 0; i < a.Rules.Count; i++)
                {
                    SpawnRuleData x = a.Rules[i], y = b.Rules[i];
                    string rl = l + "rule " + i + " ";
                    Assert.That(x.SpeciesIndex, Is.EqualTo(y.SpeciesIndex), rl + "species");
                    Assert.That(x.SubAreaMask, Is.EqualTo(y.SubAreaMask), rl + "mask");
                    Assert.That(x.Seasons, Is.EqualTo(y.Seasons), rl + "seasons");
                    Assert.That(x.Weather, Is.EqualTo(y.Weather), rl + "weather");
                    Assert.That(x.Bands, Is.EqualTo(y.Bands), rl + "bands");
                    Assert.That(x.Weight, Is.EqualTo(y.Weight), rl + "weight");
                    Assert.That(x.MinRodTier, Is.EqualTo(y.MinRodTier), rl + "rod");
                    Assert.That(x.Special, Is.EqualTo(y.Special), rl + "special");
                    Assert.That(x.SpecialChance, Is.EqualTo(y.SpecialChance), rl + "chance");
                    Assert.That(x.CatchLimitPerRun, Is.EqualTo(y.CatchLimitPerRun), rl + "limit");
                }
                Assert.That(a.Junk.Count, Is.EqualTo(b.Junk.Count), l + "junk");
                for (int i = 0; i < a.Junk.Count; i++)
                {
                    Assert.That(a.Junk[i].JunkIndex, Is.EqualTo(b.Junk[i].JunkIndex), l + "junk index " + i);
                    Assert.That(a.Junk[i].Weight, Is.EqualTo(b.Junk[i].Weight), l + "junk weight " + i);
                }
                Assert.That(a.Weather.Base, Is.EqualTo(b.Weather.Base), l + "weather base");
                Assert.That(a.Weather.SeasonColumnMult, Is.EqualTo(b.Weather.SeasonColumnMult), l + "weather mult");
            }
        }

        // Catches: a band edge typo that shifts the reeds / open boundary; junk chances off the spec table.
        [Test]
        public void EcologyData_SubAreasMatchSpec125()
        {
            ContentRegistry reg = LoadRegistry();
            // spec 12.5: lake reeds / open_water / deep_water 0-5 / 5-10 / 10+ with 0.20 / 0.12 / 0.10; harbour shallows / channel / open_sea 0-5 / 5-12 / 12+.
            CheckZone(reg, "lake_dock", new[] { "reeds", "open_water", "deep_water" }, new[] { 0f, 5f, 10f }, new[] { 5f, 10f, 0f }, new[] { 0.20f, 0.12f, 0.10f });
            CheckZone(reg, "harbour_pier", new[] { "shallows", "channel", "open_sea" }, new[] { 0f, 5f, 12f }, new[] { 5f, 12f, 0f }, new[] { 0.20f, 0.12f, 0.10f });
        }

        private static void CheckZone(ContentRegistry reg, string zoneId, string[] ids, float[] min, float[] max, float[] junk)
        {
            FishingZoneDef zone = reg.Zones.First(z => z.Id == zoneId);
            Assert.That(zone.SubAreas.Count, Is.EqualTo(3), zoneId + " sub-area count");
            for (int i = 0; i < 3; i++)
            {
                SubAreaBand b = zone.SubAreas[i];
                Assert.That(b.id, Is.EqualTo(ids[i]), zoneId + " id " + i);
                Assert.That(b.minShoreM, Is.EqualTo(min[i]), zoneId + " " + ids[i] + " minShoreM");
                Assert.That(b.maxShoreM, Is.EqualTo(max[i]), zoneId + " " + ids[i] + " maxShoreM (last band 0 = open-ended)");
                Assert.That(b.junkChance, Is.EqualTo(E("band." + zoneId + "." + ids[i] + ".junkChance", junk[i])), zoneId + " " + ids[i] + " junkChance");
                if (i > 0) Assert.That(b.minShoreM, Is.EqualTo(zone.SubAreas[i - 1].maxShoreM), zoneId + " contiguous at " + i);
            }
            Assert.That(zone.SubAreas[0].minShoreM, Is.EqualTo(0f), zoneId + " starts at 0");
        }
    }
}
