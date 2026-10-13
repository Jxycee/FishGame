// T14 extra: negative tests for FishingValidators. The seed data is valid, so Validation_FishingContent_Pass alone cannot see a weakened or
// deleted rule. Each case builds the seed content in memory, breaks exactly one value through a SerializedObject and asserts the message.
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
    public class FishingValidatorTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            T o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        // The seed content in memory: the same objects the builder creates, wired into a registry.
        private sealed class SeedSet
        {
            public ContentRegistry Registry;
            public FishingTuningDef Tuning;
            public Dictionary<string, UnityEngine.Object> ById = new Dictionary<string, UnityEngine.Object>();
            public SfxCueTableDef Cues;
            public ArtPaletteDef Palette;
            public WaterSettingsDef Water;
        }

        private SeedSet MakeSeedSet()
        {
            var set = new SeedSet();
            var rods = new List<RodDef>();
            foreach (var r in FishingSeedData.Rods)
            {
                RodDef d = Make<RodDef>();
                d.Configure(r.id, r.name, r.stats.Tier, r.stats.CastMaxDist, r.stats.ReelSpeed, r.stats.LineStrength, r.stats.AngleWindow,
                    r.stats.SteerRate, r.stats.RelaxMult, r.shopCost, r.firstCamp);
                rods.Add(d);
                set.ById[r.id] = d;
            }
            var baits = new List<BaitDef>();
            foreach (var b in FishingSeedData.Baits)
            {
                BaitDef d = Make<BaitDef>();
                d.Configure(b.id, b.name, b.stats.Rating, b.stats.StackMax, b.stats.MaxRating, b.stats.BiteSpeedMod, b.stats.JunkMult,
                    b.stats.MaxNibbles, b.stats.Unlimited);
                baits.Add(d);
                set.ById[b.id] = d;
            }
            var profiles = new List<FightProfileDef>();
            var profileById = new Dictionary<string, FightProfileDef>();
            foreach (var p in FishingSeedData.Profiles)
            {
                FightProfileDef d = Make<FightProfileDef>();
                FishFightProfile f = p.profile;
                d.Configure(p.id, p.name, f.CalmMin, f.CalmMax, f.BurstMin, f.BurstMax,
                    f.FlipRate, f.DiveChance, f.BurstRamp, f.CalmDrift, f.CalmDriftRamp, f.FlipMinGap, f.LateralJitter,
                    f.StrainMod, f.MoveMult, f.ReelInMult, f.WavePeriod);
                profiles.Add(d);
                profileById[p.id] = d;
                set.ById[p.id] = d;
            }
            var species = new List<FishSpeciesDef>();
            foreach (SpeciesSeed s in FishingSeedData.Species)
            {
                FishSpeciesDef d = Make<FishSpeciesDef>();
                FishSpeciesData x = s.Data;
                d.Configure(s.Id, s.Name, x.Rarity, x.BaseValue, x.WeightMinG, x.WeightMaxG, x.WeightSkew, x.LengthShapeK, profileById[s.ProfileId],
                    s.BodyShape, s.ColorA, s.ColorB, x.BiteWaitMod, x.HookWindow, x.IsSpecial, x.MinBaitRating, x.MinDepth, null);
                species.Add(d);
                set.ById[s.Id] = d;
            }
            var junk = new List<JunkDef>();
            foreach (var j in FishingSeedData.Junk)
            {
                JunkDef d = Make<JunkDef>();
                d.Configure(j.id, j.name, j.value, j.shape, Color.gray);
                junk.Add(d);
                set.ById[j.id] = d;
            }

            set.Tuning = Make<FishingTuningDef>();
            FishingTuning t = FishingSeedData.Tuning;
            set.Tuning.Configure(in t);

            set.Cues = Make<SfxCueTableDef>();
            set.Cues.Configure(FishingSeedData.CueRows());

            set.Palette = Make<ArtPaletteDef>();
            set.Palette.Configure(FishingSeedData.PaletteSlotNames, FishingSeedData.PaletteSeasons(), FishingSeedData.PaletteFaceJitter,
                FishingSeedData.PaletteSkinTone, FishingSeedData.PalettePhases(), FishingSeedData.PaletteUiAccent, FishingSeedData.PaletteUiGood,
                FishingSeedData.PaletteUiWarn, FishingSeedData.PaletteUiBad, FishingSeedData.PaletteUiPanel, FishingSeedData.PaletteUiText,
                FishingSeedData.PaletteM2Season);

            set.Water = Make<WaterSettingsDef>();
            WaveSet w = FishingSeedData.Waves;
            set.Water.Configure(new[]
            {
                new Vector4(w.W0.DirectionDeg, w.W0.Amplitude, w.W0.Wavelength, w.W0.Speed),
                new Vector4(w.W1.DirectionDeg, w.W1.Amplitude, w.W1.Wavelength, w.W1.Speed),
                new Vector4(w.W2.DirectionDeg, w.W2.Amplitude, w.W2.Wavelength, w.W2.Speed),
            }, FishingSeedData.WaterShallowColor, FishingSeedData.WaterDeepColor, FishingSeedData.WaterDepthRange, FishingSeedData.WaterFoamWidth);

            set.Registry = Make<ContentRegistry>();
            set.Registry.ConfigureFishing(rods, baits, profiles, species, junk, set.Tuning, set.Cues, set.Palette, set.Water);
            return set;
        }

        // ---- editing helpers ----

        private static void Edit(UnityEngine.Object target, Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty Prop(SerializedObject so, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            Assert.IsNotNull(p, "no serialized property '" + path + "'");
            return p;
        }

        private static void SetNum(SerializedObject so, string path, double value)
        {
            SerializedProperty p = Prop(so, path);
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: p.floatValue = (float)value; break;
                case SerializedPropertyType.Integer: p.intValue = (int)value; break;
                case SerializedPropertyType.Boolean: p.boolValue = value != 0.0; break;
                default: Assert.Fail("property '" + path + "' is not numeric"); break;
            }
        }

        private static void SetNum(UnityEngine.Object target, string path, double value)
        {
            Edit(target, so => SetNum(so, path, value));
        }

        private static string Fmt(float v)
        {
            return v.ToString("G", CultureInfo.InvariantCulture);
        }

        private static List<string> Check(SeedSet set)
        {
            return FishingValidators.CheckRegistry(set.Registry);
        }

        private static void AssertContains(List<string> errors, string expected)
        {
            Assert.IsTrue(errors.Contains(expected), "expected message '" + expected + "' in:\n" + string.Join("\n", errors));
        }

        private static void AssertAnyStartsWith(List<string> errors, string prefix, string suffix)
        {
            Assert.IsTrue(errors.Any(e => e.StartsWith(prefix, StringComparison.Ordinal) && e.EndsWith(suffix, StringComparison.Ordinal)),
                "expected a message '" + prefix + "...' ending '" + suffix + "' in:\n" + string.Join("\n", errors));
        }

        // ---- baseline ----

        // Catches: a validator that is too strict for the seed data (every other test here starts from this baseline).
        [Test]
        public void FishingValidators_SeedContent_HasNoErrors()
        {
            SeedSet set = MakeSeedSet();
            CollectionAssert.IsEmpty(Check(set), "the seed data must pass CheckRegistry");
        }

        // ---- range table: every range of plan section 1, independent of the production tables ----

        private const float Open = float.PositiveInfinity;   // no upper bound

        // kind, id, field, min, max, integer-valued
        private static readonly (string kind, string id, string field, float min, float max, bool isInt)[] Rows =
        {
            ("tuning", "", "castChargeTime", 0.6f, 1.5f, false), ("tuning", "", "castMinDist", 2f, 4f, false),
            ("tuning", "", "minLandDepth", 0.5f, 1.5f, false), ("tuning", "", "biteWaitMin", 2f, 8f, false),
            ("tuning", "", "biteWaitMax", 10f, 30f, false), ("tuning", "", "minBiteWait", 0.5f, 2f, false),
            ("tuning", "", "hookBeat", 0.15f, 0.4f, false), ("tuning", "", "landDistance", 0.5f, 3f, false),
            ("tuning", "", "spoolExtra", 6f, 20f, false), ("tuning", "", "relaxRate", 0.5f, 3f, false),
            ("tuning", "", "strainScale", 0.3f, 1.5f, false), ("tuning", "", "strainScaleAssist", 0.2f, 1f, false),
            ("tuning", "", "slackEscapeTime", 8f, 20f, false),
            ("tuning", "", "lateralMaxDeg", 60f, 85f, false), ("tuning", "", "hookedLeash", 2f, 5f, false),
            ("tuning", "", "junkDragSpeed", 2f, 5f, false), ("tuning", "", "resultTimeout", 1f, 10f, false),
            ("tuning", "", "commitTimeout", 1f, 10f, false), ("tuning", "", "hookTolerance", 0.5f, 1.5f, false),
            ("tuning", "", "maxFightTime", 180f, 400f, false), ("tuning", "", "rodStateHz", 10f, 20f, true),
            ("tuning", "", "creelMaxEntries", 1f, 99f, true), ("tuning", "", "creelMaxKg", 1f, 100f, false),
            ("tuning", "", "cleanCatchMaxTension", 0.5f, 0.95f, false), ("tuning", "", "pityAfterFailures", 0f, 10f, true),
            ("tuning", "", "pityStrainMult", 0.5f, 1f, false), ("tuning", "", "pityGraceMult", 1f, 3f, false),
            ("tuning", "", "pityHookWindowBonus", 0f, 1f, false),
            // R72 Rust fight baseline
            ("tuning", "", "strainBarFull", 1f, 20f, false), ("tuning", "", "strainSnap", 1f, 30f, false),
            ("tuning", "", "strainRestInput", 0f, 3f, false), ("tuning", "", "strainReelRun", 0f, 6f, false),
            ("tuning", "", "strainReelBack", 0f, 6f, false), ("tuning", "", "strainCounterSteer", 0f, 3f, false),
            ("tuning", "", "strainOtherSteer", 0f, 3f, false), ("tuning", "", "sideMoveSpeed", 0f, 5f, false),
            ("tuning", "", "backMoveSpeed", 0f, 5f, false), ("tuning", "", "tireSteerRate", 0f, 5f, false),
            ("tuning", "", "tireCounterRate", 0f, 5f, false), ("tuning", "", "tireDecayRate", 0f, 1f, false),
            ("tuning", "", "tireRunFull", 1f, 100f, false), ("tuning", "", "tireReelFull", 0.5f, 50f, false),
            ("tuning", "", "tireReelMaxMult", 1f, 5f, false), ("tuning", "", "backPullMargin", 0f, 10f, false),
            ("tuning", "", "fightTimeout", 30f, 300f, false),

            ("rod", "rod_handmade", "tier", 0f, 3f, true), ("rod", "rod_handmade", "castMaxDist", 8f, 40f, false),
            ("rod", "rod_handmade", "reelSpeed", 0.3f, 2f, false), ("rod", "rod_handmade", "lineStrength", 0.5f, 2.5f, false),
            ("rod", "rod_handmade", "angleWindow", 0.3f, 0.9f, false), ("rod", "rod_handmade", "steerRate", 0.3f, 1.2f, false),
            ("rod", "rod_handmade", "relaxMult", 0.5f, 1.5f, false), ("rod", "rod_handmade", "shopCost", 0f, Open, true),
            ("rod", "rod_handmade", "firstCamp", 0f, 6f, true),

            ("bait", "bait_worm", "rating", 1f, 10f, true), ("bait", "bait_worm", "stackMax", 1f, 5f, true),
            ("bait", "bait_worm", "biteSpeedMod", 0.5f, 1.5f, false), ("bait", "bait_worm", "junkMult", 0f, 2f, false),
            ("bait", "bait_worm", "maxNibbles", 0f, 3f, true),

            ("profile", "steady", "calmMin", 0.2f, 5f, false), ("profile", "steady", "burstMin", 0.3f, 6f, false),
            ("profile", "steady", "flipRate", 0f, 2f, false), ("profile", "steady", "diveChance", 0f, 1f, false),
            ("profile", "steady", "burstRamp", 0f, 2f, false), ("profile", "steady", "calmDrift", 0f, 0.5f, false),
            ("profile", "steady", "calmDriftRamp", 0f, 5f, false), ("profile", "steady", "flipMinGap", 0.2f, 2f, false),
            ("profile", "steady", "lateralJitter", 0f, 0.5f, false),
            ("profile", "steady", "strainMod", 0.1f, 3f, false), ("profile", "steady", "moveMult", 0f, 3f, false),
            ("profile", "steady", "reelInMult", 0.1f, 5f, false), ("profile", "steady", "wavePeriod", 0.5f, 8f, false),

            ("species", "perch", "baseValue", 1f, Open, true), ("species", "perch", "weightMinG", 1f, Open, true),
            ("species", "perch", "weightSkew", 1.2f, 3f, false), ("species", "perch", "lengthShapeK", 0.5f, 2f, false),
            ("species", "perch", "biteWaitMod", 0.5f, 2f, false), ("species", "perch", "minBaitRating", 0f, 20f, true),
            ("species", "perch", "minDepth", 0f, 6f, false),

            ("junk", "old_boot", "valueCoins", 0f, 5f, true),

            ("palette", "", "faceJitter", 0f, 0.1f, false), ("palette", "", "m2Season", 0f, 3f, true),
        };

        private static string Label(string kind, string id)
        {
            return id.Length == 0 ? kind : kind + " " + id;
        }

        private static UnityEngine.Object Target(SeedSet set, string kind, string id)
        {
            switch (kind)
            {
                case "tuning": return set.Tuning;
                case "palette": return set.Palette;
                case "water": return set.Water;
                default: return set.ById[id];
            }
        }

        public static IEnumerable<object[]> RangeCases()
        {
            foreach (var r in Rows)
            {
                // A byte field cannot hold a negative value in a SerializedObject (it wraps), so tier is only tested above its range.
                if (r.field != "tier") yield return new object[] { r.kind, r.id, r.field, r.min, r.max, r.isInt, true };
                if (!float.IsPositiveInfinity(r.max)) yield return new object[] { r.kind, r.id, r.field, r.min, r.max, r.isInt, false };
            }
        }

        // Catches: a deleted or loosened range check for any listed field (both bounds), a swapped min/max, a check on the wrong field, and a
        // non-conforming message format ("<category> <id>: <field> = <value> outside [<min>, <max>]").
        [TestCaseSource(nameof(RangeCases))]
        public void FishingValidators_OutOfRange_ReportsRangeMessage(string kind, string id, string field, float min, float max, bool isInt, bool low)
        {
            SeedSet set = MakeSeedSet();
            float delta = isInt ? 1f : Math.Max((float.IsPositiveInfinity(max) ? 1f : (max - min)) * 0.25f, 0.01f);
            float bad = low ? min - delta : max + delta;
            SetNum(Target(set, kind, id), field, bad);

            string prefix = Label(kind, id) + ": " + field + " = " + Fmt(isInt ? (float)(int)bad : bad);
            string suffix = float.IsPositiveInfinity(max) ? " below minimum " + Fmt(min) : " outside [" + Fmt(min) + ", " + Fmt(max) + "]";
            AssertAnyStartsWith(Check(set), prefix, suffix);
        }

        // Catches: NaN slipping through a comparison written as (v < min || v > max).
        [Test]
        public void FishingValidators_NaNValue_IsReportedOutOfRange([Values("castChargeTime", "strainSnap", "cleanCatchMaxTension", "creelMaxKg", "pityGraceMult")] string field)
        {
            SeedSet set = MakeSeedSet();
            SetNum(set.Tuning, field, double.NaN);
            Assert.IsTrue(Check(set).Any(e => e.StartsWith("tuning: " + field + " = ", StringComparison.Ordinal)), field + " NaN must be reported");
        }

        // ---- cross-field and structural rules ----

        // Catches: each cross-field rule removed or inverted: the exact message appears only when its rule breaks.
        // Most pairs can only break together with a range violation (the ranges do not overlap); the rule text must still appear, and
        // the equal case must pass. One case per rule, so a deleted rule fails exactly its own case.
        [TestCase("biteWait")]
        [TestCase("minBiteWait")]
        [TestCase("strainScale")]
        [TestCase("strainSnap")]
        [TestCase("fightTimeout")]
        [TestCase("castMaxDist")]
        [TestCase("angleWindow")]
        [TestCase("weightMax")]
        [TestCase("hookWindow")]
        public void FishingValidators_CrossFieldRules_ReportRuleText(string rule)
        {
            SeedSet set = MakeSeedSet();
            switch (rule)
            {
                case "biteWait":
                    SetNum(set.Tuning, "biteWaitMin", 8);
                    SetNum(set.Tuning, "biteWaitMax", 10);
                    CollectionAssert.IsEmpty(Check(set), "8 < 10");
                    SetNum(set.Tuning, "biteWaitMin", 5);
                    SetNum(set.Tuning, "biteWaitMax", 5);
                    AssertContains(Check(set), "tuning: biteWaitMin < biteWaitMax");
                    break;
                case "minBiteWait":
                    SetNum(set.Tuning, "biteWaitMin", 2);
                    SetNum(set.Tuning, "minBiteWait", 2);
                    CollectionAssert.IsEmpty(Check(set), "minBiteWait == biteWaitMin passes");
                    SetNum(set.Tuning, "biteWaitMin", 1.9);
                    AssertContains(Check(set), "tuning: minBiteWait <= biteWaitMin");
                    break;
                case "strainScale":
                    SetNum(set.Tuning, "strainScale", 0.9);
                    SetNum(set.Tuning, "strainScaleAssist", 0.9);
                    CollectionAssert.IsEmpty(Check(set), "assist == base passes");
                    SetNum(set.Tuning, "strainScaleAssist", 0.95);
                    AssertContains(Check(set), "tuning: strainScaleAssist <= strainScale");
                    break;
                case "strainSnap":
                    SetNum(set.Tuning, "strainBarFull", 6);
                    SetNum(set.Tuning, "strainSnap", 6.5);
                    CollectionAssert.IsEmpty(Check(set), "snap above the full bar passes");
                    SetNum(set.Tuning, "strainSnap", 6);
                    AssertContains(Check(set), "tuning: strainSnap > strainBarFull");
                    break;
                case "fightTimeout":
                    SetNum(set.Tuning, "fightTimeout", 120);
                    SetNum(set.Tuning, "maxFightTime", 180);
                    CollectionAssert.IsEmpty(Check(set), "120 < 180 passes");
                    SetNum(set.Tuning, "fightTimeout", 180);
                    AssertContains(Check(set), "tuning: maxFightTime > fightTimeout");
                    break;
                case "castMaxDist":
                    SetNum(set.ById["rod_handmade"], "castMaxDist", 2);   // below castMinDist 2.5 (and outside [8, 40])
                    AssertContains(Check(set), "rod rod_handmade: castMaxDist > castMinDist");
                    break;
                case "angleWindow":
                    SetNum(set.ById["rod_handmade"], "angleWindow", 0.97);   // not below PinnedLateral 0.95 (and outside [0.3, 0.9])
                    AssertContains(Check(set), "rod rod_handmade: angleWindow < PinnedLateral");
                    break;
                case "weightMax":
                    SetNum(set.ById["perch"], "weightMaxG", 300);   // == weightMinG 300
                    AssertContains(Check(set), "species perch: weightMaxG > weightMinG");
                    SetNum(set.ById["perch"], "weightMaxG", 301);
                    CollectionAssert.IsEmpty(Check(set), "301 > 300");
                    break;
                case "hookWindow":
                    foreach (double bad in new[] { 0.5, 0.79, 1.3, double.NaN })
                    {
                        SetNum(set.ById["perch"], "hookWindow", bad);
                        AssertContains(Check(set), "species perch: hookWindow is 0 or in [0.8, 1.2]");
                    }
                    foreach (double ok in new[] { 0.8, 1.0, 1.2, 0.0 })
                    {
                        SetNum(set.ById["perch"], "hookWindow", ok);
                        CollectionAssert.IsEmpty(Check(set), "hookWindow " + ok + " is allowed");
                    }
                    break;
                default:
                    Assert.Fail("unknown rule " + rule);
                    break;
            }
        }

        // Catches: the dynamic-bound rules (maxRating >= rating, calmMax >= calmMin, burstMax >= burstMin) written against a fixed bound.
        [Test]
        public void FishingValidators_DynamicBounds_UseTheSiblingField()
        {
            SeedSet set = MakeSeedSet();
            SetNum(set.ById["bait_worm"], "maxRating", 2);       // rating 3
            AssertContains(Check(set), "bait bait_worm: maxRating = 2 outside [3, 20]");
            SetNum(set.ById["bait_worm"], "maxRating", 21);
            AssertContains(Check(set), "bait bait_worm: maxRating = 21 outside [3, 20]");
            SetNum(set.ById["bait_worm"], "maxRating", 3);       // == rating: allowed
            CollectionAssert.IsEmpty(Check(set));
            SetNum(set.ById["bait_worm"], "maxRating", 9);

            SetNum(set.ById["steady"], "calmMax", 1.9);          // calmMin 2.0 (R72)
            AssertContains(Check(set), "profile steady: calmMax = 1.9 outside [2, 6]");
            SetNum(set.ById["steady"], "calmMax", 6.5);
            AssertContains(Check(set), "profile steady: calmMax = 6.5 outside [2, 6]");
            SetNum(set.ById["steady"], "calmMax", 4);
            SetNum(set.ById["steady"], "burstMax", 2.9);         // burstMin 3.0 (R72)
            AssertContains(Check(set), "profile steady: burstMax = 2.9 outside [3, 10]");
            SetNum(set.ById["steady"], "burstMax", 10.5);
            AssertContains(Check(set), "profile steady: burstMax = 10.5 outside [3, 10]");
            SetNum(set.ById["steady"], "burstMax", 5);
            CollectionAssert.IsEmpty(Check(set));
        }

        // Catches: the exactly-one-unlimited-bait rule missing or accepting zero or two.
        [Test]
        public void FishingValidators_UnlimitedBaitCount_MustBeOne()
        {
            SeedSet set = MakeSeedSet();
            SetNum(set.ById["bait_bare_hook"], "unlimited", 0);
            AssertContains(Check(set), "baits: exactly one bait must be unlimited (found 0)");
            SetNum(set.ById["bait_bare_hook"], "unlimited", 1);
            SetNum(set.ById["bait_worm"], "unlimited", 1);
            AssertContains(Check(set), "baits: exactly one bait must be unlimited (found 2)");
        }

        // Catches: the Thrasher rule (C 7.14) removed or applied to every profile; boundary 1.0 accepted.
        [Test]
        public void FishingValidators_ThrasherCalmMin_AtLeastOneSecond()
        {
            SeedSet set = MakeSeedSet();
            SetNum(set.ById["thrasher"], "calmMin", 0.9);
            AssertContains(Check(set), "profile thrasher: calmMin >= 1.0");
            SetNum(set.ById["thrasher"], "calmMin", 1.0);
            CollectionAssert.IsEmpty(Check(set), "calmMin exactly 1.0 passes");
            SetNum(set.ById["darter"], "calmMin", 0.3);          // below 1.0 on Darter (seed 1.0): only Thrasher is bound by the rule
            CollectionAssert.IsEmpty(Check(set));
            SetNum(set.ById["steady"], "calmMin", 0.5);
            CollectionAssert.IsEmpty(Check(set), "other profiles may have a short calm");
        }

        // Catches: reachability computed with the wrong rating or never evaluated; a species that no bait can reach.
        [Test]
        public void FishingValidators_UnreachableSpecies_IsReported()
        {
            SeedSet set = MakeSeedSet();
            SetNum(set.ById["perch"], "minBaitRating", 10);      // raw meat (10) reaches it
            CollectionAssert.IsEmpty(Check(set));
            SetNum(set.ById["perch"], "minBaitRating", 11);      // above every bait's EffRating(1)
            AssertContains(Check(set), "species perch: not reachable with any bait at depth <= 6 m");
        }

        // Catches: a missing singleton throwing (registry.Validate already reports it) or duplicating that message.
        [Test]
        public void FishingValidators_MissingSingleton_IsNotRepeatedHere()
        {
            SeedSet set = MakeSeedSet();
            set.Registry.ConfigureFishing(set.Registry.Rods, set.Registry.Baits, set.Registry.FightProfiles, set.Registry.Species, set.Registry.Junk,
                null, null, null, null);
            List<string> errors = null;
            Assert.DoesNotThrow(() => errors = FishingValidators.CheckRegistry(set.Registry));
            CollectionAssert.IsEmpty(errors);
        }

        // ---- palette ----

        // Catches: each palette structure rule removed (16 names, 4 seasons x 16 colours, 4 phases, ascending starts from 0, fog density).
        [TestCase("slotNames")]
        [TestCase("seasons")]
        [TestCase("seasonColours")]
        [TestCase("phases")]
        [TestCase("phaseZero")]
        [TestCase("phaseOrder")]
        [TestCase("phaseBelowOne")]
        [TestCase("fogDensity")]
        public void FishingValidators_PaletteStructure_ReportsRuleText(string rule)
        {
            SeedSet set = MakeSeedSet();
            switch (rule)
            {
                case "slotNames":
                    Edit(set.Palette, so => Prop(so, "slotNames").arraySize = 15);
                    AssertContains(Check(set), "palette: 16 slot names (found 15)");
                    break;
                case "seasons":
                    Edit(set.Palette, so => Prop(so, "seasons").arraySize = 3);
                    AssertContains(Check(set), "palette: 4 seasons (found 3)");
                    break;
                case "seasonColours":
                    Edit(set.Palette, so => Prop(so, "seasons.Array.data[2].colors").arraySize = 15);
                    AssertContains(Check(set), "palette season 2: 16 colours (found 15)");
                    break;
                case "phases":
                    Edit(set.Palette, so => Prop(so, "phases").arraySize = 3);
                    AssertContains(Check(set), "palette: 4 phases (found 3)");
                    break;
                case "phaseZero":
                    SetNum(set.Palette, "phases.Array.data[0].startFraction", 0.05);
                    AssertContains(Check(set), "palette phase 0: startFraction must be 0");
                    break;
                case "phaseOrder":
                    SetNum(set.Palette, "phases.Array.data[2].startFraction", 0.10);   // == phase 1
                    AssertContains(Check(set), "palette phase 2: startFraction must be greater than the previous phase");
                    break;
                case "phaseBelowOne":
                    SetNum(set.Palette, "phases.Array.data[3].startFraction", 1.0);
                    AssertContains(Check(set), "palette phase 3: startFraction must be below 1");
                    break;
                case "fogDensity":
                    foreach (double bad in new[] { 0.0, 0.034, double.NaN, double.PositiveInfinity, -0.01 })
                    {
                        set = MakeSeedSet();
                        SetNum(set.Palette, "phases.Array.data[1].fogDensity", bad);
                        AssertAnyStartsWith(Check(set), "palette phase 1: fogDensity = ", " outside (0, 0.033]");
                    }
                    set = MakeSeedSet();
                    SetNum(set.Palette, "phases.Array.data[1].fogDensity", 0.033);
                    CollectionAssert.IsEmpty(Check(set), "fogDensity 0.033 is the upper bound");
                    break;
                default:
                    Assert.Fail("unknown rule " + rule);
                    break;
            }
        }

        // ---- water ----

        // Catches: each water rule removed (3 waves, per-wave amplitude / wavelength / speed, amplitude sum, depthRange, foamWidth).
        [TestCase("waveCount")]
        [TestCase("amplitude")]
        [TestCase("amplitudeNaN")]
        [TestCase("amplitudeSum")]
        [TestCase("wavelength")]
        [TestCase("speed")]
        [TestCase("depthRange")]
        [TestCase("foamWidth")]
        public void FishingValidators_Water_ReportsRuleText(string rule)
        {
            SeedSet set = MakeSeedSet();
            switch (rule)
            {
                case "waveCount":
                    Edit(set.Water, so => Prop(so, "dirAmpLenSpeed").arraySize = 2);
                    AssertContains(Check(set), "water: exactly 3 waves (found 2)");
                    break;
                case "amplitude":
                    Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[1]").vector4Value = new Vector4(75f, 0.09f, 7f, 1f));
                    AssertAnyStartsWith(Check(set), "water wave 1: amplitude = 0.09", " outside [0, 0.08]");
                    break;
                case "amplitudeNaN":
                    Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[0]").vector4Value = new Vector4(20f, float.NaN, 9f, 1.2f));
                    Assert.IsTrue(Check(set).Any(e => e.StartsWith("water wave 0: amplitude = ", StringComparison.Ordinal)), "NaN amplitude");
                    break;
                case "amplitudeSum":
                    Edit(set.Water, so =>
                    {
                        Prop(so, "dirAmpLenSpeed.Array.data[0]").vector4Value = new Vector4(20f, 0.05f, 9f, 1.2f);
                        Prop(so, "dirAmpLenSpeed.Array.data[1]").vector4Value = new Vector4(75f, 0.05f, 7f, 1f);
                        Prop(so, "dirAmpLenSpeed.Array.data[2]").vector4Value = new Vector4(320f, 0.05f, 6.5f, 0.8f);
                    });
                    AssertAnyStartsWith(Check(set), "water: amplitude sum = 0.15", " outside [0, 0.12]");
                    break;
                case "wavelength":
                    Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[2]").vector4Value = new Vector4(320f, 0.025f, 4.9f, 0.8f));
                    AssertAnyStartsWith(Check(set), "water wave 2: wavelength = 4.9", " below minimum 5");
                    Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[2]").vector4Value = new Vector4(320f, 0.025f, float.PositiveInfinity, 0.8f));
                    AssertAnyStartsWith(Check(set), "water wave 2: wavelength = ", " is not finite");
                    break;
                case "speed":
                    Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[0]").vector4Value = new Vector4(20f, 0.05f, 9f, 3.1f));
                    AssertAnyStartsWith(Check(set), "water wave 0: speed = 3.1", " outside [0, 3]");
                    break;
                case "depthRange":
                case "foamWidth":
                    foreach (double bad in new[] { 0.0, -1.0 })
                    {
                        set = MakeSeedSet();
                        SetNum(set.Water, rule, bad);
                        AssertAnyStartsWith(Check(set), "water: " + rule + " = ", " must be greater than 0");
                    }
                    foreach (double bad in new[] { double.NaN, double.PositiveInfinity })
                    {
                        set = MakeSeedSet();
                        SetNum(set.Water, rule, bad);
                        AssertAnyStartsWith(Check(set), "water: " + rule + " = ", " is not finite");
                    }
                    break;
                default:
                    Assert.Fail("unknown rule " + rule);
                    break;
            }
        }

        // ---- cue rows ----

        private static void SetCue(SeedSet set, int row, Action<SerializedProperty> edit)
        {
            Edit(set.Cues, so => edit(Prop(so, "rows.Array.data[" + row + "]")));
        }

        private static string CueId(SeedSet set, int row)
        {
            return set.Cues.Rows[row].id;
        }

        private static int RowOf(SeedSet set, string id)
        {
            for (int i = 0; i < set.Cues.Rows.Count; i++) if (set.Cues.Rows[i].id == id) return i;
            Assert.Fail("no cue row " + id);
            return -1;
        }

        // Catches: the seed cue rows (five with maxDistance 0) failing because the raw maxDistance was compared with minDistance.
        [Test]
        public void FishingValidators_CueRowsWithZeroMaxDistance_UseTheDefault30()
        {
            SeedSet set = MakeSeedSet();
            int zero = 0;
            foreach (SfxCueRow r in set.Cues.Rows) if (r.maxDistance == 0f) zero++;
            Assert.AreEqual(5, zero, "the five M1 2D/ambience rows have maxDistance 0");
            CollectionAssert.IsEmpty(Check(set));
        }

        // Catches: "0 means skip the check" (effective 30 must still exceed minDistance), a raw compare, negative maxDistance accepted,
        // equal max/min accepted, a minDistance of 0.
        [Test]
        public void FishingValidators_CueDistance_EffectiveMaxMustExceedMin()
        {
            SeedSet set = MakeSeedSet();
            int ui = RowOf(set, SfxIds.UiClick);            // maxDistance 0
            SetCue(set, ui, p => p.FindPropertyRelative("minDistance").floatValue = 31f);
            AssertContains(Check(set), "cue Ui_Click: effective maxDistance (30) must be greater than minDistance (31)");

            set = MakeSeedSet();
            SetCue(set, ui, p => p.FindPropertyRelative("minDistance").floatValue = 30f);   // eff 30 == min 30
            AssertContains(Check(set), "cue Ui_Click: effective maxDistance (30) must be greater than minDistance (30)");

            set = MakeSeedSet();
            SetCue(set, ui, p => p.FindPropertyRelative("minDistance").floatValue = 29f);
            CollectionAssert.IsEmpty(Check(set), "29 < 30 passes");

            set = MakeSeedSet();
            int bell = RowOf(set, SfxIds.BellRing);          // max 30
            SetCue(set, bell, p => p.FindPropertyRelative("maxDistance").floatValue = -5f);
            AssertContains(Check(set), "cue Bell_Ring: maxDistance = -5 must be 0 (default) or greater than minDistance (1)");

            set = MakeSeedSet();
            SetCue(set, bell, p => { p.FindPropertyRelative("minDistance").floatValue = 10f; p.FindPropertyRelative("maxDistance").floatValue = 10f; });
            AssertContains(Check(set), "cue Bell_Ring: effective maxDistance (10) must be greater than minDistance (10)");

            set = MakeSeedSet();
            SetCue(set, bell, p => p.FindPropertyRelative("minDistance").floatValue = 0f);
            AssertContains(Check(set), "cue Bell_Ring: minDistance = 0 must be greater than 0");
        }

        // Catches: a non-finite cue value slipping through a one-sided comparison (+Inf passes "> 0", "> min" and ">= 0").
        [Test]
        public void FishingValidators_CueValues_MustBeFinite(
            [Values("minDistance", "maxDistance", "minInterval")] string field,
            [Values(double.NaN, double.PositiveInfinity)] double bad)
        {
            SeedSet set = MakeSeedSet();
            int bell = RowOf(set, SfxIds.BellRing);
            SetCue(set, bell, p => p.FindPropertyRelative(field).floatValue = (float)bad);
            AssertAnyStartsWith(Check(set), "cue Bell_Ring: " + field + " = ", " is not finite");
        }

        // Catches: baseVolume outside 0..1 or non-finite accepted (the range check already rejects NaN and Inf).
        [Test]
        public void FishingValidators_CueBaseVolume_MustBeInUnitRange([Values(-0.1, 1.1, double.NaN, double.PositiveInfinity)] double bad)
        {
            SeedSet set = MakeSeedSet();
            SetCue(set, RowOf(set, SfxIds.BellRing), p => p.FindPropertyRelative("baseVolume").floatValue = (float)bad);
            AssertAnyStartsWith(Check(set), "cue Bell_Ring: baseVolume = ", " outside [0, 1]");
            SetCue(set, RowOf(set, SfxIds.BellRing), p => p.FindPropertyRelative("baseVolume").floatValue = 1f);
            CollectionAssert.IsEmpty(Check(set), "1 is allowed");
        }

        // Catches: minInterval below 0 accepted.
        [Test]
        public void FishingValidators_CueMinInterval_NotNegative()
        {
            SeedSet set = MakeSeedSet();
            SetCue(set, RowOf(set, SfxIds.BellRing), p => p.FindPropertyRelative("minInterval").floatValue = -0.01f);
            AssertAnyStartsWith(Check(set), "cue Bell_Ring: minInterval = -0.01", " must be >= 0");
            SetCue(set, RowOf(set, SfxIds.BellRing), p => p.FindPropertyRelative("minInterval").floatValue = 0f);
            CollectionAssert.IsEmpty(Check(set), "0 is allowed");
        }

        // Catches: duplicate ids and ids unknown to SfxRecipes accepted.
        [Test]
        public void FishingValidators_CueIds_UniqueAndKnown()
        {
            SeedSet set = MakeSeedSet();
            int bell = RowOf(set, SfxIds.BellRing);
            int click = RowOf(set, SfxIds.UiClick);
            SetCue(set, click, p => p.FindPropertyRelative("id").stringValue = SfxIds.BellRing);
            AssertContains(Check(set), "cue Bell_Ring: duplicate id");

            set = MakeSeedSet();
            SetCue(set, bell, p => p.FindPropertyRelative("id").stringValue = "no_such_cue");
            AssertContains(Check(set), "cue no_such_cue: unknown to SfxRecipes");
        }

        // ---- seed assets ----

        // Catches: the registry-listing rule or the missing-asset rule absent. A removed registry entry and an unreachable path are reported.
        [Test]
        public void FishingValidators_SeedAssets_MustExistAndBeListed()
        {
            ContentRegistry reg = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.IsNotNull(reg, "run Tools/unity.ps1 build first");
            Func<string, UnityEngine.Object> load = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>;
            CollectionAssert.IsEmpty(FishingValidators.CheckSeedAssets(reg, load), "the generated assets satisfy the rule");

            string missing = AssetPaths.BaitsFolder + "/bait_worm.asset";
            List<string> e1 = FishingValidators.CheckSeedAssets(reg, path => path == missing ? null : load(path));
            CollectionAssert.AreEqual(new[] { "missing asset " + missing }, e1);

            // a registry that lacks the Twig rod and the cue table (in-memory copy of the real registry)
            ContentRegistry copy = Make<ContentRegistry>();
            var rods = new List<RodDef>(reg.Rods);
            rods.RemoveAll(r => r.Id == "rod_twig");
            copy.ConfigureFishing(rods, reg.Baits, reg.FightProfiles, reg.Species, reg.Junk, reg.FishingTuning, null, reg.ArtPalette, reg.WaterSettings);
            List<string> e2 = FishingValidators.CheckSeedAssets(copy, load);
            CollectionAssert.AreEqual(new[]
            {
                AssetPaths.RodsFolder + "/rod_twig.asset is not listed in ContentRegistry.rods",
                AssetPaths.SfxCueTableAsset + " is not listed in ContentRegistry.sfxCueTable",
            }, e2);
        }

        // ---- species models ----

        private Mesh MakeMesh(bool colours, int subMeshes)
        {
            var m = new Mesh();
            created.Add(m);
            m.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right };
            if (colours) m.colors = new[] { Color.white, Color.white, Color.white };
            m.subMeshCount = subMeshes;
            for (int i = 0; i < subMeshes; i++) m.SetTriangles(new[] { 0, 1, 2 }, i);
            return m;
        }

        // Catches: a model from outside Content/Models or not an .fbx accepted, a missing colour channel accepted, subMeshCount != 1 accepted;
        // the extension compare being case sensitive; null rejected.
        [Test]
        public void FishingValidators_SpeciesModel_Rules()
        {
            string good = AssetPaths.ModelsFolder + "/Fish_Perch.fbx";
            CollectionAssert.IsEmpty(FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 1), good));
            CollectionAssert.IsEmpty(FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 1), AssetPaths.ModelsFolder + "/Fish_Perch.FBX"),
                "extension compare ignores case");
            CollectionAssert.IsEmpty(FishingValidators.CheckSpeciesModel("perch", null, null), "a null model is allowed (fallback mesh)");

            CollectionAssert.AreEqual(new[] { "species perch: model is not from " + AssetPaths.ModelsFolder + "/*.fbx (Assets/_Project/Content/Meshes/x.asset)" },
                FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 1), "Assets/_Project/Content/Meshes/x.asset"));
            CollectionAssert.AreEqual(new[] { "species perch: model is not from " + AssetPaths.ModelsFolder + "/*.fbx (" + AssetPaths.ModelsFolder + "/Fish_Perch.obj)" },
                FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 1), AssetPaths.ModelsFolder + "/Fish_Perch.obj"));
            CollectionAssert.AreEqual(new[] { "species perch: model is not from " + AssetPaths.ModelsFolder + "/*.fbx (" + AssetPaths.ModelsFolder + "X/Fish_Perch.fbx)" },
                FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 1), AssetPaths.ModelsFolder + "X/Fish_Perch.fbx"));
            CollectionAssert.AreEqual(new[] { "species perch: model " + good + " has no vertex colours" },
                FishingValidators.CheckSpeciesModel("perch", MakeMesh(false, 1), good));
            CollectionAssert.AreEqual(new[] { "species perch: model " + good + " has 5 submeshes, expected 1" },
                FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 5), good));
            CollectionAssert.AreEqual(new[]
            {
                "species perch: model " + good + " has no vertex colours",
                "species perch: model " + good + " has 2 submeshes, expected 1",
            }, FishingValidators.CheckSpeciesModel("perch", MakeMesh(false, 2), good));
        }

        // Catches: the builder and the validator disagreeing on which meshes are usable fish models (colour channel AND exactly one submesh).
        [Test]
        public void FishingContentBuilder_IsFishModelMesh_RequiresColoursAndOneSubmesh()
        {
            Assert.IsTrue(FishingContentBuilder.IsFishModelMesh(MakeMesh(true, 1)));
            Assert.IsFalse(FishingContentBuilder.IsFishModelMesh(MakeMesh(false, 1)), "no colours");
            Assert.IsFalse(FishingContentBuilder.IsFishModelMesh(MakeMesh(true, 5)), "five submeshes");
            Assert.IsFalse(FishingContentBuilder.IsFishModelMesh(MakeMesh(false, 5)), "the spike perch: neither");
            Assert.IsFalse(FishingContentBuilder.IsFishModelMesh(null));
        }

        // ---- entry points on the real assets ----

        // Catches: the prefix step dropping a part, swapping the two prefixes, or leaving one part unprefixed. In memory only: the real
        // assets are never edited by a test (a failing test must not be able to leave a committed asset changed).
        [Test]
        public void FishingValidators_Prefixed_PrefixesBothParts_InMemory()
        {
            SeedSet set = MakeSeedSet();
            SetNum(set.Tuning, "castChargeTime", 5.0);
            List<string> content = FishingValidators.CheckRegistry(set.Registry);
            List<string> models = FishingValidators.CheckSpeciesModel("perch", MakeMesh(true, 1), AssetPaths.MeshesFolder + "/LakeDock_Shore.asset");
            Assert.AreEqual(1, content.Count, "exactly the tuning error: " + string.Join("\n", content));
            Assert.AreEqual(1, models.Count, "exactly the model-path error: " + string.Join("\n", models));

            List<string> all = FishingValidators.Prefixed(content, models);
            CollectionAssert.AreEqual(new[]
            {
                "FishingContent: tuning: castChargeTime = 5 outside [0.6, 1.5]",
                "SpeciesModels: species perch: model is not from " + AssetPaths.ModelsFolder + "/*.fbx (" + AssetPaths.MeshesFolder + "/LakeDock_Shore.asset)",
            }, all);

            CollectionAssert.IsEmpty(FishingValidators.Prefixed(new List<string>(), new List<string>()), "no errors, no prefix noise");
            CollectionAssert.AreEqual(new[] { "SpeciesModels: m" }, FishingValidators.Prefixed(new List<string>(), new List<string> { "m" }));
            CollectionAssert.AreEqual(new[] { "FishingContent: c" }, FishingValidators.Prefixed(new List<string> { "c" }, new List<string>()));
        }

        // Catches: All() / FishingContent() / SpeciesModels() reporting a problem on the generated assets (READ ONLY: nothing is written).
        [Test]
        public void FishingValidators_EntryPoints_PassOnTheRealAssets()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset), "run Tools/unity.ps1 build first");
            CollectionAssert.IsEmpty(FishingValidators.All(), "the generated assets pass");
            CollectionAssert.IsEmpty(FishingValidators.FishingContent());
            CollectionAssert.IsEmpty(FishingValidators.SpeciesModels());
        }

        // ---- non-finite water and palette values ----

        // Catches: a NaN / Infinity wave direction or colour channel accepted (they poison the water shader and the ambient blend).
        [Test]
        public void FishingValidators_NonFiniteWaterAndPaletteValues_AreReported()
        {
            SeedSet set = MakeSeedSet();
            Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[1]").vector4Value = new Vector4(float.NaN, 0.025f, 7f, 1f));
            AssertContains(Check(set), "water wave 1: direction = NaN is not finite");

            set = MakeSeedSet();
            Edit(set.Water, so => Prop(so, "dirAmpLenSpeed.Array.data[2]").vector4Value = new Vector4(float.PositiveInfinity, 0.025f, 6.5f, 0.8f));
            AssertContains(Check(set), "water wave 2: direction = Infinity is not finite");

            set = MakeSeedSet();
            Edit(set.Water, so => Prop(so, "shallowColor").colorValue = new Color(0.1f, float.NaN, 0.3f, 1f));
            AssertContains(Check(set), "water: shallowColor has a non-finite channel (0.1, NaN, 0.3, 1)");

            set = MakeSeedSet();
            Edit(set.Water, so => Prop(so, "deepColor").colorValue = new Color(0.1f, 0.2f, 0.3f, float.PositiveInfinity));
            AssertContains(Check(set), "water: deepColor has a non-finite channel (0.1, 0.2, 0.3, Infinity)");

            set = MakeSeedSet();
            Edit(set.Palette, so => Prop(so, "seasons.Array.data[1].colors.Array.data[4]").colorValue = new Color(float.NaN, 0f, 0f, 1f));
            AssertAnyStartsWith(Check(set), "palette season 1: colour 4 has a non-finite channel", ")");

            set = MakeSeedSet();
            Edit(set.Palette, so => Prop(so, "skinTone").colorValue = new Color(0f, float.PositiveInfinity, 0f, 1f));
            AssertAnyStartsWith(Check(set), "palette: skinTone has a non-finite channel", ")");

            set = MakeSeedSet();
            Edit(set.Palette, so => Prop(so, "uiText").colorValue = new Color(0f, 0f, float.NaN, 1f));
            AssertAnyStartsWith(Check(set), "palette: uiText has a non-finite channel", ")");

            CollectionAssert.IsEmpty(Check(MakeSeedSet()), "finite seed values pass");
        }
    }
}
