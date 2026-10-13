// T14 (plan data-tuning 3.7): the fishing seeds equal the spec 9.6/9.7/9.12 tables (plus C 7.7 motion seeds, clean threshold 0.8, pity seeds),
// and the generated data assets (Content/Fishing, Content/Art, Content/Audio, the registry) are complete, unique, in range and reachable.
// "Assets" means the assets produced by the builder (run Tools/unity.ps1 build first); "seeds" means FishingSeedData.
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
    public class FishingContentTests
    {
        // Values T15 retuned away from the spec tables. Key "<category>.<id>.<field>" (profile.thrasher.calmMin, rod.rod_twig.lineStrength,
        // bait.bait_worm.rating, species.carp.baseValue, junk.old_boot.valueCoins) or "tuning.<field>" (tuning.relaxRate). Value = the tuned seed.
        // Empty since R72 (the three pre-R72 entries belonged to fields F7 removed); T15 may edit only this map (ruling D-Q2).
        static readonly Dictionary<string, float> TunedDeviations = new Dictionary<string, float>
        {
        };

        static float E(string key, float spec)
        {
            return TunedDeviations.TryGetValue(key, out float tuned) ? tuned : spec;
        }

        static void F(string what, float expected, float actual)
        {
            Assert.AreEqual(expected, actual, 0f, what);
        }

        static void C(string what, Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0f, what + " r");
            Assert.AreEqual(expected.g, actual.g, 0f, what + " g");
            Assert.AreEqual(expected.b, actual.b, 0f, what + " b");
            Assert.AreEqual(expected.a, actual.a, 0f, what + " a");
        }

        static ContentRegistry LoadRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.IsNotNull(reg, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            return reg;
        }

        static string Fmt(float v)
        {
            return v.ToString("G", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- ids

        // Catches: a seed id that is not a valid content id or is duplicated within a category; a registry list that is empty or has a
        // different number of entries than the seeds (a category not wired into the registry).
        [Test]
        public void FishingData_Ids_UniquePerCategory()
        {
            var cats = new List<(string name, List<string> ids)>
            {
                ("rods", new List<string>()), ("baits", new List<string>()), ("fightProfiles", new List<string>()),
                ("species", new List<string>()), ("junk", new List<string>()),
            };
            foreach (var r in FishingSeedData.Rods) cats[0].ids.Add(r.id);
            foreach (var b in FishingSeedData.Baits) cats[1].ids.Add(b.id);
            foreach (var p in FishingSeedData.Profiles) cats[2].ids.Add(p.id);
            foreach (SpeciesSeed s in FishingSeedData.Species) cats[3].ids.Add(s.Id);
            foreach (var j in FishingSeedData.Junk) cats[4].ids.Add(j.id);
            foreach (var c in cats)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in c.ids)
                {
                    Assert.IsTrue(ContentIds.IsValidId(id), c.name + " seed id '" + id + "' is not a valid content id");
                    Assert.IsTrue(seen.Add(id), c.name + " seed id '" + id + "' is duplicated");
                }
            }

            ContentRegistry reg = LoadRegistry();
            CollectionAssert.IsEmpty(reg.Validate(), "registry.Validate() reports errors");
            Assert.AreEqual(cats[0].ids.Count, reg.Rods.Count, "rods");
            Assert.AreEqual(cats[1].ids.Count, reg.Baits.Count, "baits");
            Assert.AreEqual(cats[2].ids.Count, reg.FightProfiles.Count, "fightProfiles");
            Assert.AreEqual(cats[3].ids.Count, reg.Species.Count, "species");
            Assert.AreEqual(cats[4].ids.Count, reg.Junk.Count, "junk");
        }

        // ---------------------------------------------------------------- ranges (test-local copy of plan section 1 ranges)

        static void R(List<string> v, string label, float value, float min, float max)
        {
            if (!(value >= min && value <= max))
                v.Add(label + " = " + Fmt(value) + " outside [" + Fmt(min) + ", " + Fmt(max) + "]");
        }

        static void Rule(List<string> v, string label, bool ok, string rule)
        {
            if (!ok) v.Add(label + ": " + rule);
        }

        // Catches: any seed or asset value outside the section 1 ranges or breaking a cross-field rule (a typo in a seed, a hand edit).
        [Test]
        public void FishingData_Ranges_Valid()
        {
            ContentRegistry reg = LoadRegistry();
            var v = new List<string>();

            FishingTuningDef t = reg.FishingTuning;
            Assert.IsNotNull(t, "fishingTuning missing");
            R(v, "tuning castChargeTime", t.CastChargeTime, 0.6f, 1.5f);
            R(v, "tuning castMinDist", t.CastMinDist, 2f, 4f);
            R(v, "tuning minLandDepth", t.MinLandDepth, 0.5f, 1.5f);
            R(v, "tuning biteWaitMin", t.BiteWaitMin, 2f, 8f);
            R(v, "tuning biteWaitMax", t.BiteWaitMax, 10f, 30f);
            Rule(v, "tuning", t.BiteWaitMin < t.BiteWaitMax, "biteWaitMin < biteWaitMax");
            R(v, "tuning minBiteWait", t.MinBiteWait, 0.5f, 2f);
            Rule(v, "tuning", t.MinBiteWait <= t.BiteWaitMin, "minBiteWait <= biteWaitMin");
            R(v, "tuning hookBeat", t.HookBeat, 0.15f, 0.4f);
            R(v, "tuning landDistance", t.LandDistance, 0.5f, 3f);
            R(v, "tuning spoolExtra", t.SpoolExtra, 6f, 20f);
            R(v, "tuning relaxRate", t.RelaxRate, 0.5f, 3f);
            R(v, "tuning strainScale", t.StrainScale, 0.3f, 1.5f);
            R(v, "tuning strainScaleAssist", t.StrainScaleAssist, 0.2f, 1f);
            Rule(v, "tuning", t.StrainScaleAssist <= t.StrainScale, "strainScaleAssist <= strainScale");
            R(v, "tuning slackEscapeTime", t.SlackEscapeTime, 8f, 20f);
            R(v, "tuning lateralMaxDeg", t.LateralMaxDeg, 60f, 85f);
            R(v, "tuning hookedLeash", t.HookedLeash, 2f, 5f);
            R(v, "tuning junkDragSpeed", t.JunkDragSpeed, 2f, 5f);
            R(v, "tuning resultTimeout", t.ResultTimeout, 1f, 10f);
            R(v, "tuning commitTimeout", t.CommitTimeout, 1f, 10f);
            R(v, "tuning hookTolerance", t.HookTolerance, 0.5f, 1.5f);
            R(v, "tuning maxFightTime", t.MaxFightTime, 180f, 400f);
            R(v, "tuning rodStateHz", t.RodStateHz, 10f, 20f);
            R(v, "tuning creelMaxEntries", t.CreelMaxEntries, 1f, 99f);
            R(v, "tuning creelMaxKg", t.CreelMaxKg, 1f, 100f);
            R(v, "tuning cleanCatchMaxTension", t.CleanCatchMaxTension, 0.5f, 0.95f);
            R(v, "tuning pityAfterFailures", t.PityAfterFailures, 0f, 10f);
            R(v, "tuning pityStrainMult", t.PityStrainMult, 0.5f, 1f);
            R(v, "tuning pityGraceMult", t.PityGraceMult, 1f, 3f);
            R(v, "tuning pityHookWindowBonus", t.PityHookWindowBonus, 0f, 1f);
            R(v, "tuning strainBarFull", t.StrainBarFull, 1f, 20f);
            R(v, "tuning strainSnap", t.StrainSnap, 1f, 30f);
            Rule(v, "tuning", t.StrainSnap > t.StrainBarFull, "strainSnap > strainBarFull");
            R(v, "tuning strainRestInput", t.StrainRestInput, 0f, 3f);
            R(v, "tuning strainReelRun", t.StrainReelRun, 0f, 6f);
            R(v, "tuning strainReelBack", t.StrainReelBack, 0f, 6f);
            R(v, "tuning strainCounterSteer", t.StrainCounterSteer, 0f, 3f);
            R(v, "tuning strainOtherSteer", t.StrainOtherSteer, 0f, 3f);
            R(v, "tuning sideMoveSpeed", t.SideMoveSpeed, 0f, 5f);
            R(v, "tuning backMoveSpeed", t.BackMoveSpeed, 0f, 5f);
            R(v, "tuning tireSteerRate", t.TireSteerRate, 0f, 5f);
            R(v, "tuning tireCounterRate", t.TireCounterRate, 0f, 5f);
            R(v, "tuning tireDecayRate", t.TireDecayRate, 0f, 1f);
            R(v, "tuning tireRunFull", t.TireRunFull, 1f, 100f);
            R(v, "tuning tireReelFull", t.TireReelFull, 0.5f, 50f);
            R(v, "tuning tireReelMaxMult", t.TireReelMaxMult, 1f, 5f);
            R(v, "tuning backPullMargin", t.BackPullMargin, 0f, 10f);
            R(v, "tuning fightTimeout", t.FightTimeout, 30f, 300f);
            Rule(v, "tuning", t.MaxFightTime > t.FightTimeout, "maxFightTime > fightTimeout");

            foreach (RodDef r in reg.Rods)
            {
                string l = "rod " + r.Id;
                R(v, l + " tier", r.Tier, 0f, 3f);
                R(v, l + " castMaxDist", r.CastMaxDist, 8f, 40f);
                Rule(v, l, r.CastMaxDist > t.CastMinDist, "castMaxDist > castMinDist");
                R(v, l + " reelSpeed", r.ReelSpeed, 0.3f, 2f);
                R(v, l + " lineStrength", r.LineStrength, 0.5f, 2.5f);
                R(v, l + " angleWindow", r.AngleWindow, 0.3f, 0.9f);
                Rule(v, l, r.AngleWindow < FishingConstants.PinnedLateral, "angleWindow < PinnedLateral");   // parked (R72), mirrors the validator
                R(v, l + " steerRate", r.SteerRate, 0.3f, 1.2f);
                R(v, l + " relaxMult", r.RelaxMult, 0.5f, 1.5f);
                R(v, l + " shopCost", r.ShopCost, 0f, 1000000f);
                R(v, l + " firstCamp", r.FirstCamp, 0f, 6f);
            }

            int unlimited = 0;
            foreach (BaitDef b in reg.Baits)
            {
                string l = "bait " + b.Id;
                R(v, l + " rating", b.Rating, 1f, 10f);
                R(v, l + " stackMax", b.StackMax, 1f, 5f);
                R(v, l + " maxRating", b.MaxRating, b.Rating, 20f);
                R(v, l + " biteSpeedMod", b.BiteSpeedMod, 0.5f, 1.5f);
                R(v, l + " junkMult", b.JunkMult, 0f, 2f);
                R(v, l + " maxNibbles", b.MaxNibbles, 0f, FishingConstants.MaxNibbleSlots);
                if (b.Unlimited) unlimited++;
            }
            Rule(v, "baits", unlimited == 1, "exactly one unlimited bait (found " + unlimited + ")");

            foreach (FightProfileDef p in reg.FightProfiles)
            {
                string l = "profile " + p.Id;
                R(v, l + " calmMin", p.CalmMin, 0.2f, 5f);
                R(v, l + " calmMax", p.CalmMax, p.CalmMin, 6f);
                R(v, l + " burstMin", p.BurstMin, 0.3f, 6f);
                R(v, l + " burstMax", p.BurstMax, p.BurstMin, 10f);
                R(v, l + " flipRate", p.FlipRate, 0f, 2f);
                R(v, l + " diveChance", p.DiveChance, 0f, 1f);
                R(v, l + " burstRamp", p.BurstRamp, 0f, 2f);
                R(v, l + " calmDrift", p.CalmDrift, 0f, 0.5f);
                R(v, l + " calmDriftRamp", p.CalmDriftRamp, 0f, 5f);
                R(v, l + " flipMinGap", p.FlipMinGap, 0.2f, 2f);
                R(v, l + " lateralJitter", p.LateralJitter, 0f, 0.5f);
                R(v, l + " strainMod", p.StrainMod, 0.1f, 3f);
                R(v, l + " moveMult", p.MoveMult, 0f, 3f);
                R(v, l + " reelInMult", p.ReelInMult, 0.1f, 5f);
                R(v, l + " wavePeriod", p.WavePeriod, 0.5f, 8f);
                if (p.Id == "thrasher") Rule(v, l, p.CalmMin >= 1.0f, "calmMin >= 1.0");
            }

            foreach (FishSpeciesDef s in reg.Species)
            {
                string l = "species " + s.Id;
                R(v, l + " baseValue", s.BaseValue, 1f, int.MaxValue);
                R(v, l + " weightMinG", s.WeightMinG, 1f, int.MaxValue);
                Rule(v, l, s.WeightMaxG > s.WeightMinG, "weightMaxG > weightMinG");
                R(v, l + " weightSkew", s.WeightSkew, FishingConstants.SkewMin, FishingConstants.SkewMax);
                R(v, l + " lengthShapeK", s.LengthShapeK, 0.5f, 2f);
                R(v, l + " biteWaitMod", s.BiteWaitMod, 0.5f, 2f);
                Rule(v, l, s.HookWindow == 0f || (s.HookWindow >= 0.8f && s.HookWindow <= 1.2f), "hookWindow is 0 or in [0.8, 1.2]");
                R(v, l + " minBaitRating", s.MinBaitRating, 0f, 20f);
                R(v, l + " minDepth", s.MinDepth, 0f, FishingConstants.DepthMultFullDepth);
                Rule(v, l, s.Fight != null && reg.FightProfiles.Contains(s.Fight), "fight is non-null and listed in fightProfiles");
            }

            foreach (JunkDef j in reg.Junk)
                R(v, "junk " + j.Id + " valueCoins", j.ValueCoins, 0f, 5f);

            ArtPaletteDef art = reg.ArtPalette;
            Assert.IsNotNull(art, "artPalette missing");
            Rule(v, "palette", art.SlotNames.Count == 16, "16 slot names");
            Rule(v, "palette", art.Seasons.Count == 4, "4 seasons");
            for (int s = 0; s < art.Seasons.Count; s++)
                Rule(v, "palette season " + s, art.Seasons[s] != null && art.Seasons[s].colors != null && art.Seasons[s].colors.Length == 16, "16 colours");
            Rule(v, "palette", art.Phases.Count == 4, "4 phases");
            for (int p = 0; p < art.Phases.Count; p++)
            {
                DayPhaseAmbient ph = art.Phases[p];
                if (p == 0) Rule(v, "palette phase 0", ph.startFraction == 0f, "startFraction is 0");
                else Rule(v, "palette phase " + p, ph.startFraction > art.Phases[p - 1].startFraction, "startFraction ascends");
                Rule(v, "palette phase " + p, ph.startFraction < 1f, "startFraction < 1");
                R(v, "palette phase " + p + " fogDensity", ph.fogDensity, 1e-6f, 0.033f);
            }
            R(v, "palette faceJitter", art.FaceJitter, 0f, 0.1f);
            R(v, "palette m2Season", art.M2Season, 0f, 3f);

            WaterSettingsDef water = reg.WaterSettings;
            Assert.IsNotNull(water, "waterSettings missing");
            Rule(v, "water", water.DirAmpLenSpeed.Count == 3, "exactly 3 waves");
            float ampSum = 0f;
            for (int w = 0; w < water.DirAmpLenSpeed.Count; w++)
            {
                Vector4 wv = water.DirAmpLenSpeed[w];
                R(v, "water wave " + w + " amplitude", wv.y, 0f, 0.08f);
                R(v, "water wave " + w + " wavelength", wv.z, 5f, 1000f);
                R(v, "water wave " + w + " speed", wv.w, 0f, 3f);
                ampSum += wv.y;
            }
            R(v, "water amplitude sum", ampSum, 0f, 0.12f);
            Rule(v, "water", water.DepthRange > 0f, "depthRange > 0");
            Rule(v, "water", water.FoamWidth > 0f, "foamWidth > 0");

            SfxCueTableDef cues = reg.SfxCueTable;
            Assert.IsNotNull(cues, "sfxCueTable missing");
            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SfxCueRow row in cues.Rows)
            {
                string l = "cue " + row.id;
                Rule(v, l, cueIds.Add(row.id), "duplicate id");
                Rule(v, l, SfxRecipes.TryGet(row.id, out _), "unknown to SfxRecipes");
                R(v, l + " baseVolume", row.baseVolume, 0f, 1f);
                Rule(v, l, row.minDistance > 0f, "minDistance > 0");
                float effMax = row.maxDistance == 0f ? 30f : row.maxDistance;   // 0 means the AudioBus default
                Rule(v, l, effMax > row.minDistance, "effective maxDistance (" + Fmt(effMax) + ") > minDistance (" + Fmt(row.minDistance) + ")");
                Rule(v, l, row.minInterval >= 0f, "minInterval >= 0");
            }

            CollectionAssert.IsEmpty(v, string.Join("\n", v));
            Assert.DoesNotThrow(() => FishingSeedData.BuildDatabase(), "FishingSeedData.BuildDatabase");
        }

        // Catches (R72): an asset whose new field was never written (the builder is create-if-missing, so a missing YAML line loads as 0 and
        // the fish would never strain or move), or any asset value drifting from its seed. Compares every public field of the Core structs.
        [Test]
        public void FishingData_AssetValuesEqualSeeds()
        {
            ContentRegistry reg = LoadRegistry();
            AssertFieldsEqual("tuning", FishingSeedData.Tuning, reg.FishingTuning.ToCore());
            foreach (var r in FishingSeedData.Rods)
                AssertFieldsEqual("rod " + r.id, r.stats, AssetDatabase.LoadAssetAtPath<RodDef>(AssetPaths.RodsFolder + "/" + r.id + ".asset").ToCore());
            foreach (var p in FishingSeedData.Profiles)
                AssertFieldsEqual("profile " + p.id, p.profile,
                    AssetDatabase.LoadAssetAtPath<FightProfileDef>(AssetPaths.FightProfilesFolder + "/" + p.id + ".asset").ToCore());
        }

        static void AssertFieldsEqual<T>(string what, T seed, T asset) where T : struct
        {
            foreach (System.Reflection.FieldInfo f in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                Assert.AreEqual(f.GetValue(seed), f.GetValue(asset), what + " " + f.Name + " (asset vs seed)");
        }

        // ---------------------------------------------------------------- reachability

        // Catches: a species no bait can ever reach (its minBaitRating above every EffRating, or minDepth beyond the 6 m full depth).
        [Test]
        public void FishingData_EverySpecies_ReachableWithSomeBaitAndDepth()
        {
            FishingDatabase db = LoadRegistry().Fishing;
            Assert.IsNotNull(db, "registry has no fishing database");
            for (int s = 0; s < db.Species.Count; s++)
            {
                FishSpeciesData sp = db.Species[s];
                bool reachable = false;
                for (int b = 0; b < db.Baits.Count && !reachable; b++)
                    reachable = PlaceholderRoller.IsEligible(in sp, db.Baits[b].EffRating(1), 6f);
                Assert.IsTrue(reachable, "species index " + s + " is not reachable with any bait at depth 6 m");
            }
            // The M2 debug loadout holds every bait (C M2-D14), so "some bait" is also "some bait in the loadout".
        }

        // ---------------------------------------------------------------- seeds vs spec tables

        static (string id, string name, RodStats stats, int shopCost, int firstCamp) SeedRod(string id)
        {
            foreach (var r in FishingSeedData.Rods) if (r.id == id) return r;
            Assert.Fail("no rod seed '" + id + "'");
            return default;
        }

        static (string id, string name, BaitStats stats) SeedBait(string id)
        {
            foreach (var b in FishingSeedData.Baits) if (b.id == id) return b;
            Assert.Fail("no bait seed '" + id + "'");
            return default;
        }

        static (string id, string name, FishFightProfile profile) SeedProfile(string id)
        {
            foreach (var p in FishingSeedData.Profiles) if (p.id == id) return p;
            Assert.Fail("no profile seed '" + id + "'");
            return default;
        }

        static SpeciesSeed SeedSpecies(string id)
        {
            foreach (SpeciesSeed s in FishingSeedData.Species) if (s.Id == id) return s;
            Assert.Fail("no species seed '" + id + "'");
            return default;
        }

        static (string id, string name, int value, BodyShape shape) SeedJunk(string id)
        {
            foreach (var j in FishingSeedData.Junk) if (j.id == id) return j;
            Assert.Fail("no junk seed '" + id + "'");
            return default;
        }

        static void Tn(string field, float spec, float actual)
        {
            F("tuning." + field, E("tuning." + field, spec), actual);
        }

        static void Rod(string id, string name, int tier, float cast, float reel, float strength, float window, float steer, float relax, int shop, int camp)
        {
            var r = SeedRod(id);
            string k = "rod." + id + ".";
            Assert.AreEqual(name, r.name, id + " name");
            F(k + "tier", E(k + "tier", tier), r.stats.Tier);
            F(k + "castMaxDist", E(k + "castMaxDist", cast), r.stats.CastMaxDist);
            F(k + "reelSpeed", E(k + "reelSpeed", reel), r.stats.ReelSpeed);
            F(k + "lineStrength", E(k + "lineStrength", strength), r.stats.LineStrength);
            F(k + "angleWindow", E(k + "angleWindow", window), r.stats.AngleWindow);
            F(k + "steerRate", E(k + "steerRate", steer), r.stats.SteerRate);
            F(k + "relaxMult", E(k + "relaxMult", relax), r.stats.RelaxMult);
            F(k + "shopCost", E(k + "shopCost", shop), r.shopCost);
            F(k + "firstCamp", E(k + "firstCamp", camp), r.firstCamp);
        }

        static void Bait(string id, string name, int rating, int stack, int maxRating, float speed, float junk, int nibbles, bool unlimited)
        {
            var b = SeedBait(id);
            string k = "bait." + id + ".";
            Assert.AreEqual(name, b.name, id + " name");
            F(k + "rating", E(k + "rating", rating), b.stats.Rating);
            F(k + "stackMax", E(k + "stackMax", stack), b.stats.StackMax);
            F(k + "maxRating", E(k + "maxRating", maxRating), b.stats.MaxRating);
            F(k + "biteSpeedMod", E(k + "biteSpeedMod", speed), b.stats.BiteSpeedMod);
            F(k + "junkMult", E(k + "junkMult", junk), b.stats.JunkMult);
            F(k + "maxNibbles", E(k + "maxNibbles", nibbles), b.stats.MaxNibbles);
            Assert.AreEqual(unlimited, b.stats.Unlimited, id + " unlimited");
        }

        // Catches (M3-D4, B4): a seed typo or swapped columns in the bait affinity table; real bait assets that were never migrated (the
        // silent B4 failure: a field missing from an M2 asset loads as 0, and specialMult 0 would disable every special).
        [Test]
        public void FishingData_BaitAffinitySeeds_MatchContract()
        {
            var table = new (string id, FishTags tags, float affinity, float special)[]
            {
                ("bait_bare_hook", FishTags.None, 1.0f, 1.0f),
                ("bait_worm", FishTags.Bottom, 1.3f, 1.0f),
                ("bait_grub", FishTags.Surface, 1.3f, 1.0f),
                ("bait_minnow", FishTags.Predator, 1.4f, 1.0f),
                ("bait_shrimp", FishTags.Schooling, 1.3f, 1.2f),
                ("bait_raw_meat", FishTags.Predator | FishTags.Nocturnal, 1.5f, 1.5f),
            };
            Assert.AreEqual(table.Length, FishingSeedData.Baits.Count, "bait count");
            ContentRegistry reg = LoadRegistry();
            foreach (var row in table)
            {
                BaitStats seed = SeedBait(row.id).stats;
                Assert.AreEqual(row.tags, seed.AffinityTags, row.id + " seed affinityTags");
                F(row.id + " seed affinityMult", row.affinity, seed.AffinityMult);
                F(row.id + " seed specialMult", row.special, seed.SpecialMult);

                var asset = AssetDatabase.LoadAssetAtPath<BaitDef>(AssetPaths.BaitsFolder + "/" + row.id + ".asset");
                Assert.IsNotNull(asset, "missing bait asset " + row.id);
                Assert.AreNotEqual(0f, asset.AffinityMult, "bait asset " + row.id + " was never migrated (B4)");
                Assert.AreEqual(row.tags, asset.AffinityTags, row.id + " asset affinityTags");
                F(row.id + " asset affinityMult", row.affinity, asset.AffinityMult);
                F(row.id + " asset specialMult", row.special, asset.SpecialMult);
                Assert.IsTrue(reg.Baits.Contains(asset), row.id + " is listed in the registry");
            }
        }

        // calmMin, calmMax, burstMin, burstMax, flipRate, dive, burstRamp, calmDrift, calmDriftRamp, flipMinGap, lateralJitter,
        // then the R72 tier (strainMod, moveMult, reelInMult) and wavePeriod
        static void Profile(string id, string name, float calmMin, float calmMax,
            float burstMin, float burstMax, float flip, float dive, float ramp, float drift, float driftRamp, float gap, float jitter,
            float strainMod, float moveMult, float reelInMult, float wavePeriod)
        {
            var p = SeedProfile(id);
            string k = "profile." + id + ".";
            Assert.AreEqual(name, p.name, id + " name");
            F(k + "calmMin", E(k + "calmMin", calmMin), p.profile.CalmMin);
            F(k + "calmMax", E(k + "calmMax", calmMax), p.profile.CalmMax);
            F(k + "burstMin", E(k + "burstMin", burstMin), p.profile.BurstMin);
            F(k + "burstMax", E(k + "burstMax", burstMax), p.profile.BurstMax);
            F(k + "flipRate", E(k + "flipRate", flip), p.profile.FlipRate);
            F(k + "diveChance", E(k + "diveChance", dive), p.profile.DiveChance);
            F(k + "burstRamp", E(k + "burstRamp", ramp), p.profile.BurstRamp);
            F(k + "calmDrift", E(k + "calmDrift", drift), p.profile.CalmDrift);
            F(k + "calmDriftRamp", E(k + "calmDriftRamp", driftRamp), p.profile.CalmDriftRamp);
            F(k + "flipMinGap", E(k + "flipMinGap", gap), p.profile.FlipMinGap);
            F(k + "lateralJitter", E(k + "lateralJitter", jitter), p.profile.LateralJitter);
            F(k + "strainMod", E(k + "strainMod", strainMod), p.profile.StrainMod);
            F(k + "moveMult", E(k + "moveMult", moveMult), p.profile.MoveMult);
            F(k + "reelInMult", E(k + "reelInMult", reelInMult), p.profile.ReelInMult);
            F(k + "wavePeriod", E(k + "wavePeriod", wavePeriod), p.profile.WavePeriod);
        }

        static void Species(string id, string name, string profileId, int gMin, int gMax, int baseValue, float biteWaitMod, int minBait, float minDepth,
            FishRarity rarity, BodyShape shape, Color colorA, Color colorB, string modelFile)
        {
            SpeciesSeed s = SeedSpecies(id);
            string k = "species." + id + ".";
            Assert.AreEqual(name, s.Name, id + " name");
            Assert.AreEqual(profileId, s.ProfileId, id + " profile");
            F(k + "weightMinG", E(k + "weightMinG", gMin), s.Data.WeightMinG);
            F(k + "weightMaxG", E(k + "weightMaxG", gMax), s.Data.WeightMaxG);
            F(k + "baseValue", E(k + "baseValue", baseValue), s.Data.BaseValue);
            F(k + "biteWaitMod", E(k + "biteWaitMod", biteWaitMod), s.Data.BiteWaitMod);
            F(k + "minBaitRating", E(k + "minBaitRating", minBait), s.Data.MinBaitRating);
            F(k + "minDepth", E(k + "minDepth", minDepth), s.Data.MinDepth);
            F(k + "weightSkew", E(k + "weightSkew", 2.2f), s.Data.WeightSkew);
            F(k + "lengthShapeK", E(k + "lengthShapeK", 1.0f), s.Data.LengthShapeK);
            F(k + "hookWindow", E(k + "hookWindow", 0f), s.Data.HookWindow);
            Assert.IsFalse(s.Data.IsSpecial, id + " isSpecial");
            Assert.AreEqual(rarity, s.Data.Rarity, id + " rarity");
            Assert.AreEqual(shape, s.BodyShape, id + " bodyShape");
            C(id + " colorA", colorA, s.ColorA);
            C(id + " colorB", colorB, s.ColorB);
            Assert.AreEqual(modelFile, s.ModelFile, id + " modelFile");
        }

        // Catches: a seed that drifts from spec 9.12 / 9.7 / 9.6 / 9.10, the C 7.7 motion seeds (including Darter's calm, burst, flip rate and gap
        // that override the spec 9.6 row), the clean threshold 0.8 or the R65 pity seeds; a swapped pair of columns; a wrong profile index.
        // Values T15 retunes are read through TunedDeviations.
        [Test]
        public void FishingData_SeedsMatchSpecTables()
        {
            FishingTuning t = FishingSeedData.Tuning;
            Tn("castChargeTime", 1.0f, t.CastChargeTime);
            Assert.AreEqual(true, t.ChargePingPong, "tuning.chargePingPong");
            Tn("castMinDist", 2.5f, t.CastMinDist);
            Tn("minLandDepth", 0.5f, t.MinLandDepth);   // M3-D52 (spec 9.3/9.12 delta, R75)
            Tn("biteWaitMin", 4f, t.BiteWaitMin);
            Tn("biteWaitMax", 18f, t.BiteWaitMax);
            Tn("minBiteWait", 1.0f, t.MinBiteWait);
            Tn("hookBeat", 0.25f, t.HookBeat);
            Tn("landDistance", 1.0f, t.LandDistance);
            Tn("spoolExtra", 12f, t.SpoolExtra);
            Tn("relaxRate", 1.5f, t.RelaxRate);
            Tn("strainScale", 0.75f, t.StrainScale);
            Tn("strainScaleAssist", 0.45f, t.StrainScaleAssist);
            Tn("slackEscapeTime", 12f, t.SlackEscapeTime);
            Tn("lateralMaxDeg", 60f, t.LateralMaxDeg);
            Tn("hookedLeash", 3f, t.HookedLeash);
            Tn("junkDragSpeed", 3f, t.JunkDragSpeed);
            Tn("resultTimeout", 3f, t.ResultTimeout);
            Tn("commitTimeout", 3f, t.CommitTimeout);
            Tn("hookTolerance", 0.75f, t.HookTolerance);
            Tn("maxFightTime", 240f, t.MaxFightTime);
            Tn("rodStateHz", 15f, t.RodStateHz);
            Assert.AreEqual(true, t.CancelRefundsBait, "tuning.cancelRefundsBait");
            Tn("creelMaxEntries", 12f, t.CreelMaxEntries);
            Tn("creelMaxWeightG", 15000f, t.CreelMaxWeightG);
            Tn("cleanCatchMaxTension", 0.8f, t.CleanCatchMaxTension);
            Tn("pityAfterFailures", 3f, t.PityAfterFailures);
            Tn("pityStrainMult", 0.85f, t.PityStrainMult);
            Tn("pityGraceMult", 1.5f, t.PityGraceMult);
            Tn("pityHookWindowBonus", 0.3f, t.PityHookWindowBonus);
            // R72 Rust fight baseline (spec 9.12)
            Tn("strainBarFull", 6f, t.StrainBarFull);
            Tn("strainSnap", 7f, t.StrainSnap);
            Tn("strainRestInput", 0.5f, t.StrainRestInput);
            Tn("strainReelRun", 2.2f, t.StrainReelRun);
            Tn("strainReelBack", 2.5f, t.StrainReelBack);
            Tn("strainCounterSteer", 0.8f, t.StrainCounterSteer);
            Tn("strainOtherSteer", 0.5f, t.StrainOtherSteer);
            Tn("sideMoveSpeed", 1.2f, t.SideMoveSpeed);
            Tn("backMoveSpeed", 1.3f, t.BackMoveSpeed);
            Tn("tireSteerRate", 0.8f, t.TireSteerRate);
            Tn("tireCounterRate", 1.25f, t.TireCounterRate);
            Tn("tireDecayRate", 0.1f, t.TireDecayRate);
            Tn("tireRunFull", 20f, t.TireRunFull);
            Tn("tireReelFull", 5f, t.TireReelFull);
            Tn("tireReelMaxMult", 3f, t.TireReelMaxMult);
            Tn("backPullMargin", 2f, t.BackPullMargin);
            Tn("fightTimeout", 120f, t.FightTimeout);

            Assert.AreEqual(4, FishingSeedData.Rods.Count, "rod count");
            // R72: reelSpeed and steerRate follow Rust's rod (Handmade 0.6 / 0.8) with the pre-R72 ratios between rods.
            Rod("rod_twig", "Twig Rod", 0, 14f, 0.54f, 0.8f, 0.50f, 0.67f, 0.9f, 0, 0);
            Rod("rod_handmade", "Handmade Rod", 1, 18f, 0.6f, 1.0f, 0.60f, 0.8f, 1.0f, 120, 1);
            Rod("rod_carbon", "Carbon Rod", 2, 24f, 0.72f, 1.3f, 0.70f, 0.93f, 1.1f, 300, 2);
            Rod("rod_deepsea", "Deep-Sea Rod", 3, 30f, 0.78f, 1.6f, 0.75f, 1.07f, 1.2f, 600, 4);

            Assert.AreEqual(6, FishingSeedData.Baits.Count, "bait count");
            Bait("bait_bare_hook", "Bare Hook", 1, 1, 1, 1.30f, 1.5f, 1, true);
            Bait("bait_worm", "Worm", 3, 3, 9, 0.90f, 1.0f, 2, false);
            Bait("bait_grub", "Grub", 4, 3, 12, 0.85f, 0.9f, 2, false);
            Bait("bait_minnow", "Minnow", 2, 2, 4, 1.00f, 1.1f, 1, false);
            Bait("bait_shrimp", "Shrimp", 5, 2, 10, 0.80f, 0.8f, 1, false);
            Bait("bait_raw_meat", "Raw Meat", 10, 1, 10, 0.70f, 0.5f, 0, false);

            // R72: Rust tiers plus the five per-profile quirks (spec 9.6); the quirks change only how the fish moves.
            Assert.AreEqual(5, FishingSeedData.Profiles.Count, "profile count");
            // R72: Rust tiers (spec 9.6: small 0.8 / 0.8 / 1.2, medium 1 / 1 / 1, large 1.25 / 1.25 / 0.9) plus the five per-profile quirks
            // (rest, run, back chance, flips, ramp, creep, jitter, wave 4 s; Thrasher 1.5 s); the quirks change only how the fish moves.
            // The pull/lateral/run/drain columns are retired (F7 removes them) and keep their pre-R72 values.
            Profile("steady", "Steady", 2f, 4f, 3f, 5f, 0f, 0.15f, 0.6f, 0f, 0f, 0.5f, 0f, 0.8f, 0.8f, 1.2f, 4f);
            Profile("darter", "Darter", 1f, 2f, 2f, 3.5f, 1.0f, 0.15f, 0f, 0f, 0f, 0.3f, 0f, 1.0f, 1.0f, 1.0f, 4f);
            Profile("bulldog", "Bulldog", 2f, 4f, 2.5f, 4.5f, 0f, 0.55f, 0f, 0.06f, 2.5f, 0.5f, 0f, 1.25f, 1.25f, 0.9f, 4f);
            Profile("thrasher", "Thrasher", 1.5f, 3f, 3f, 5f, 0.4f, 0.20f, 0f, 0f, 0f, 0.5f, 0.3f, 1.0f, 1.0f, 1.0f, 1.5f);
            Profile("runner", "Runner", 2f, 3.5f, 5f, 9f, 0f, 0.30f, 0.8f, 0f, 0f, 0.5f, 0f, 1.25f, 1.25f, 0.9f, 4f);

            // M3 T3: only bluegill is seeded here (spec 12.6 row 2 + M3-D35, gates 0/0, colours kept from M2); T10 moves the species seeds.
            Assert.AreEqual(1, FishingSeedData.Species.Count, "species count");
            Species("bluegill", "Bluegill", "steady", 200, 600, 8, 1.0f, 0, 0f, FishRarity.Common, BodyShape.Round,
                new Color(0.32f, 0.50f, 0.62f, 1f), new Color(0.95f, 0.62f, 0.22f, 1f), "Fish_Bluegill.fbx");
            Assert.AreEqual(FishTags.Schooling, SeedSpecies("bluegill").Data.Tags, "bluegill tags");

            // The profile index in each species seed is the ordinal-sorted index of its profile id (0.2: bulldog, darter, runner, steady, thrasher).
            string[] sortedProfiles = { "bulldog", "darter", "runner", "steady", "thrasher" };
            foreach (SpeciesSeed s in FishingSeedData.Species)
                Assert.AreEqual(sortedProfiles[s.Data.FightProfileIndex], s.ProfileId, s.Id + " FightProfileIndex");

            Assert.AreEqual(2, FishingSeedData.Junk.Count, "junk count");
            var boot = SeedJunk("old_boot");
            Assert.AreEqual("Old Boot", boot.name);
            F("junk.old_boot.valueCoins", E("junk.old_boot.valueCoins", 2), boot.value);
            Assert.AreEqual(BodyShape.Long, boot.shape);
            var can = SeedJunk("tin_can");
            Assert.AreEqual("Tin Can", can.name);
            F("junk.tin_can.valueCoins", E("junk.tin_can.valueCoins", 2), can.value);
            Assert.AreEqual(BodyShape.Round, can.shape);
            C("old_boot color", new Color(0.30f, 0.22f, 0.16f, 1f), FishingSeedData.JunkColor("old_boot"));
            C("tin_can color", new Color(0.68f, 0.70f, 0.72f, 1f), FishingSeedData.JunkColor("tin_can"));
        }

        static void CheckWave(string what, Wave w, float dir, float amp, float len, float speed)
        {
            F(what + " direction", dir, w.DirectionDeg);
            F(what + " amplitude", amp, w.Amplitude);
            F(what + " wavelength", len, w.Wavelength);
            F(what + " speed", speed, w.Speed);
        }

        // Catches: a transcription error in the art, water or cue seeds (the validators only check ranges), a season that does not copy
        // Summer outside slots 0, 1, 2, 7, cue rows that drift from SfxRecipes.All (order, category, mixing values).
        [Test]
        public void FishingData_ArtWaterAndCueSeeds_MatchPlanTables()
        {
            string[] names = { "Grass", "Foliage", "FoliageDark", "Wood", "WoodDark", "Sand", "Rock", "Reed", "Dock", "Roof", "Accent", "Snow",
                               "Reserved12", "Reserved13", "Reserved14", "Reserved15" };
            CollectionAssert.AreEqual(names, FishingSeedData.PaletteSlotNames);

            Color[] summer =
            {
                new Color(0.42f, 0.66f, 0.30f, 1f), new Color(0.30f, 0.52f, 0.24f, 1f), new Color(0.17f, 0.34f, 0.17f, 1f),
                new Color(0.66f, 0.48f, 0.30f, 1f), new Color(0.40f, 0.27f, 0.17f, 1f), new Color(0.88f, 0.80f, 0.58f, 1f),
                new Color(0.55f, 0.55f, 0.53f, 1f), new Color(0.62f, 0.68f, 0.36f, 1f), new Color(0.58f, 0.44f, 0.30f, 1f),
                new Color(0.70f, 0.30f, 0.24f, 1f), new Color(0.95f, 0.45f, 0.15f, 1f), new Color(0.95f, 0.96f, 0.98f, 1f),
                new Color(0.5f, 0.5f, 0.5f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f),
            };
            // Spring, Summer, Autumn, Winter: slots 0, 1, 2, 7 differ; every other slot copies Summer.
            Color[][] overrides =
            {
                new[] { new Color(0.50f, 0.74f, 0.34f, 1f), new Color(0.40f, 0.62f, 0.30f, 1f), new Color(0.22f, 0.42f, 0.20f, 1f), new Color(0.56f, 0.70f, 0.34f, 1f) },
                new[] { summer[0], summer[1], summer[2], summer[7] },
                new[] { new Color(0.62f, 0.60f, 0.30f, 1f), new Color(0.85f, 0.50f, 0.18f, 1f), new Color(0.55f, 0.26f, 0.12f, 1f), new Color(0.72f, 0.62f, 0.32f, 1f) },
                new[] { new Color(0.80f, 0.84f, 0.86f, 1f), new Color(0.32f, 0.44f, 0.36f, 1f), new Color(0.18f, 0.28f, 0.24f, 1f), new Color(0.66f, 0.62f, 0.48f, 1f) },
            };
            string[] seasonNames = { "Spring", "Summer", "Autumn", "Winter" };
            PaletteSeason[] seasons = FishingSeedData.PaletteSeasons();
            Assert.AreEqual(4, seasons.Length);
            int[] varying = { 0, 1, 2, 7 };
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(seasonNames[s], seasons[s].name);
                Assert.AreEqual(16, seasons[s].colors.Length);
                for (int i = 0; i < 16; i++)
                {
                    Color expected = summer[i];
                    int slot = Array.IndexOf(varying, i);
                    if (slot >= 0) expected = overrides[s][slot];
                    C(seasonNames[s] + " slot " + i, expected, seasons[s].colors[i]);
                }
            }

            Assert.AreEqual(0.04f, FishingSeedData.PaletteFaceJitter, 0f);
            Assert.AreEqual(1, FishingSeedData.PaletteM2Season);
            SlotPalette.GetSkinRgb(out float sr, out float sg, out float sb);
            C("skin", new Color(sr, sg, sb, 1f), FishingSeedData.PaletteSkinTone);
            C("uiAccent", new Color(0.95f, 0.62f, 0.20f, 1f), FishingSeedData.PaletteUiAccent);
            C("uiGood", new Color(0.40f, 0.80f, 0.40f, 1f), FishingSeedData.PaletteUiGood);
            C("uiWarn", new Color(0.98f, 0.78f, 0.25f, 1f), FishingSeedData.PaletteUiWarn);
            C("uiBad", new Color(0.90f, 0.30f, 0.25f, 1f), FishingSeedData.PaletteUiBad);
            C("uiPanel", new Color(0.08f, 0.10f, 0.14f, 0.85f), FishingSeedData.PaletteUiPanel);
            C("uiText", new Color(0.95f, 0.95f, 0.92f, 1f), FishingSeedData.PaletteUiText);

            DayPhaseAmbient[] phases = FishingSeedData.PalettePhases();
            Assert.AreEqual(4, phases.Length);
            float[] start = { 0f, 0.10f, 0.55f, 0.70f };
            Color[] light = { new Color(1.00f, 0.78f, 0.60f, 1f), new Color(1.00f, 0.96f, 0.88f, 1f), new Color(1.00f, 0.62f, 0.40f, 1f), new Color(0.62f, 0.70f, 0.92f, 1f) };
            float[] intensity = { 0.8f, 1.1f, 0.8f, 0.25f };
            Color[] top = { new Color(0.38f, 0.52f, 0.78f, 1f), new Color(0.33f, 0.58f, 0.92f, 1f), new Color(0.30f, 0.30f, 0.58f, 1f), new Color(0.04f, 0.06f, 0.16f, 1f) };
            Color[] horizon = { new Color(0.98f, 0.74f, 0.56f, 1f), new Color(0.74f, 0.86f, 0.96f, 1f), new Color(0.98f, 0.56f, 0.36f, 1f), new Color(0.12f, 0.16f, 0.30f, 1f) };
            Color[] ground = { new Color(0.36f, 0.34f, 0.36f, 1f), new Color(0.40f, 0.40f, 0.36f, 1f), new Color(0.30f, 0.26f, 0.30f, 1f), new Color(0.05f, 0.05f, 0.08f, 1f) };
            Color[] shade = { new Color(0.42f, 0.40f, 0.52f, 1f), new Color(0.46f, 0.50f, 0.62f, 1f), new Color(0.40f, 0.32f, 0.46f, 1f), new Color(0.12f, 0.14f, 0.26f, 1f) };
            Color[] ambient = { new Color(0.70f, 0.62f, 0.66f, 1f), new Color(0.80f, 0.86f, 0.92f, 1f), new Color(0.66f, 0.52f, 0.56f, 1f), new Color(0.30f, 0.36f, 0.52f, 1f) };
            Color[] fog = { new Color(0.86f, 0.74f, 0.66f, 1f), new Color(0.74f, 0.84f, 0.92f, 1f), new Color(0.80f, 0.56f, 0.46f, 1f), new Color(0.10f, 0.13f, 0.22f, 1f) };
            float[] fogDensity = { 0.010f, 0.008f, 0.010f, 0.014f };
            float[] warm = { 0.6f, 0f, 1f, 0f };
            float[] cool = { 0f, 0f, 0f, 1f };
            for (int p = 0; p < 4; p++)
            {
                string l = "phase " + p;
                F(l + " startFraction", start[p], phases[p].startFraction);
                C(l + " lightColor", light[p], phases[p].lightColor);
                F(l + " lightIntensity", intensity[p], phases[p].lightIntensity);
                C(l + " skyTop", top[p], phases[p].skyTop);
                C(l + " skyHorizon", horizon[p], phases[p].skyHorizon);
                C(l + " skyGround", ground[p], phases[p].skyGround);
                C(l + " toonShade", shade[p], phases[p].toonShade);
                C(l + " toonAmbient", ambient[p], phases[p].toonAmbient);
                C(l + " fogColor", fog[p], phases[p].fogColor);
                F(l + " fogDensity", fogDensity[p], phases[p].fogDensity);
                F(l + " warmWeight", warm[p], phases[p].warmWeight);
                F(l + " coolWeight", cool[p], phases[p].coolWeight);
            }

            WaveSet w = FishingSeedData.Waves;
            CheckWave("W0", w.W0, 20f, 0.050f, 9.0f, 1.2f);
            CheckWave("W1", w.W1, 75f, 0.040f, 7.0f, 1.0f);
            CheckWave("W2", w.W2, 320f, 0.025f, 6.5f, 0.8f);
            C("shallow", new Color(0.30f, 0.86f, 0.80f, 0.68f), FishingSeedData.WaterShallowColor);
            C("deep", new Color(0.03f, 0.36f, 0.62f, 0.97f), FishingSeedData.WaterDeepColor);
            Assert.AreEqual(1.6f, FishingSeedData.WaterDepthRange, 0f);
            Assert.AreEqual(0.35f, FishingSeedData.WaterFoamWidth, 0f);

            IReadOnlyList<SfxCueRow> rows = FishingSeedData.CueRows();
            Assert.AreEqual(SfxRecipes.All.Count, rows.Count, "one cue row per recipe");
            for (int i = 0; i < rows.Count; i++)
            {
                SfxRecipe r = SfxRecipes.All[i];
                Assert.AreEqual(r.Id, rows[i].id, "row " + i + " id");
                Assert.AreEqual(r.Category, rows[i].category, r.Id + " category");
                F(r.Id + " baseVolume", r.BaseVolume, rows[i].baseVolume);
                F(r.Id + " minDistance", r.MinDistance, rows[i].minDistance);
                F(r.Id + " maxDistance", r.MaxDistance, rows[i].maxDistance);
                F(r.Id + " minInterval", r.MinInterval, rows[i].minInterval);
                Assert.AreEqual(r.Spatial, rows[i].spatial, r.Id + " spatial");
            }
        }

        // ---------------------------------------------------------------- Thrasher calm

        // Catches: a Thrasher whose calm phase can fall under one second (C 7.7: its jitter needs a readable calm); an inverted calm range.
        [Test]
        public void FishingData_ThrasherCalm_AtLeastOneSecond()
        {
            var seed = SeedProfile("thrasher");
            Assert.GreaterOrEqual(seed.profile.CalmMin, 1.0f, "seed calmMin");
            Assert.GreaterOrEqual(seed.profile.CalmMax, seed.profile.CalmMin, "seed calmMax");

            var asset = AssetDatabase.LoadAssetAtPath<FightProfileDef>(AssetPaths.FightProfilesFolder + "/thrasher.asset");
            Assert.IsNotNull(asset, "missing thrasher.asset: run Tools/unity.ps1 build first");
            Assert.GreaterOrEqual(asset.CalmMin, 1.0f, "asset calmMin");
            Assert.GreaterOrEqual(asset.CalmMax, asset.CalmMin, "asset calmMax");
        }

        // ---------------------------------------------------------------- assets exist for every seed

        static void AssertListed<T>(IReadOnlyList<T> list, T asset, string what) where T : UnityEngine.Object
        {
            foreach (T item in list) if (ReferenceEquals(item, asset)) return;
            Assert.Fail(what + " is not listed in the registry");
        }

        // Catches: a seed without an asset (or an asset whose Id differs from its file name), an asset the registry does not list, a singleton
        // that is missing or not the registry's reference, a species whose fight profile differs from its seed.
        [Test]
        public void FishingData_AssetsExistForEverySeed()
        {
            ContentRegistry reg = LoadRegistry();
            foreach (var r in FishingSeedData.Rods)
            {
                var a = AssetDatabase.LoadAssetAtPath<RodDef>(AssetPaths.RodsFolder + "/" + r.id + ".asset");
                Assert.IsNotNull(a, "missing rod asset " + r.id);
                Assert.AreEqual(r.id, a.Id);
                AssertListed(reg.Rods, a, "rod " + r.id);
            }
            foreach (var b in FishingSeedData.Baits)
            {
                var a = AssetDatabase.LoadAssetAtPath<BaitDef>(AssetPaths.BaitsFolder + "/" + b.id + ".asset");
                Assert.IsNotNull(a, "missing bait asset " + b.id);
                Assert.AreEqual(b.id, a.Id);
                AssertListed(reg.Baits, a, "bait " + b.id);
            }
            foreach (var p in FishingSeedData.Profiles)
            {
                var a = AssetDatabase.LoadAssetAtPath<FightProfileDef>(AssetPaths.FightProfilesFolder + "/" + p.id + ".asset");
                Assert.IsNotNull(a, "missing profile asset " + p.id);
                Assert.AreEqual(p.id, a.Id);
                AssertListed(reg.FightProfiles, a, "profile " + p.id);
            }
            foreach (SpeciesSeed s in FishingSeedData.Species)
            {
                var a = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetPaths.SpeciesFolder + "/" + s.Id + ".asset");
                Assert.IsNotNull(a, "missing species asset " + s.Id);
                Assert.AreEqual(s.Id, a.Id);
                AssertListed(reg.Species, a, "species " + s.Id);
                Assert.IsNotNull(a.Fight, "species " + s.Id + " has no fight profile");
                Assert.AreEqual(s.ProfileId, a.Fight.Id, "species " + s.Id + " fight profile");
            }
            foreach (var j in FishingSeedData.Junk)
            {
                var a = AssetDatabase.LoadAssetAtPath<JunkDef>(AssetPaths.JunkFolder + "/" + j.id + ".asset");
                Assert.IsNotNull(a, "missing junk asset " + j.id);
                Assert.AreEqual(j.id, a.Id);
                AssertListed(reg.Junk, a, "junk " + j.id);
            }

            var tuning = AssetDatabase.LoadAssetAtPath<FishingTuningDef>(AssetPaths.FishingTuningAsset);
            Assert.IsNotNull(tuning, "missing " + AssetPaths.FishingTuningAsset);
            Assert.AreSame(tuning, reg.FishingTuning, "registry fishingTuning");
            var cues = AssetDatabase.LoadAssetAtPath<SfxCueTableDef>(AssetPaths.SfxCueTableAsset);
            Assert.IsNotNull(cues, "missing " + AssetPaths.SfxCueTableAsset);
            Assert.AreSame(cues, reg.SfxCueTable, "registry sfxCueTable");
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            Assert.IsNotNull(palette, "missing " + AssetPaths.ArtPaletteAsset);
            Assert.AreSame(palette, reg.ArtPalette, "registry artPalette");
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            Assert.IsNotNull(water, "missing " + AssetPaths.WaterSettingsAsset);
            Assert.AreSame(water, reg.WaterSettings, "registry waterSettings");
        }
    }
}
