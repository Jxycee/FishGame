// T13 (plan data-tuning 2.5): the fishing, audio and art Defs map to their Core structs field by field. Every case uses a distinct
// non-zero value per field so a swapped pair, a dropped field or a wrong default fails.
using System;
using System.Collections.Generic;
using System.Reflection;
using FishGame.Core;
using FishGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class FishingDefsTests
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

        // Catches: a ToCore or Configure that swaps two fields (every value differs), drops a field, or copies the wrong getter;
        // a species ToCore that ignores the supplied profile index or leaks presentation fields; a tuning Def that misses a
        // FishingTuning member (reflection walks all 47) or maps the kg field wrongly.
        [TestCase("rod")]
        [TestCase("bait")]
        [TestCase("fight_profile")]
        [TestCase("species")]
        [TestCase("junk")]
        [TestCase("tuning")]
        public void FishingDefs_ToCore_MapsEveryField(string kind)
        {
            switch (kind)
            {
                case "rod": CheckRod(); break;
                case "bait": CheckBait(); break;
                case "fight_profile": CheckFightProfile(); break;
                case "species": CheckSpecies(); break;
                case "junk": CheckJunk(); break;
                case "tuning": CheckTuning(); break;
                default: Assert.Fail("unknown case " + kind); break;
            }
        }

        private void CheckRod()
        {
            RodDef d = Make<RodDef>();
            d.Configure("rod_x", "Rod X", (byte)3, 1.1f, 2.2f, 3.3f, 4.4f, 5.5f, 6.6f, 77, 88);
            Assert.AreEqual("rod_x", d.Id);
            Assert.AreEqual("Rod X", d.DisplayName);
            Assert.AreEqual((byte)3, d.Tier);
            Assert.AreEqual(1.1f, d.CastMaxDist);
            Assert.AreEqual(2.2f, d.ReelSpeed);
            Assert.AreEqual(3.3f, d.LineStrength);
            Assert.AreEqual(4.4f, d.AngleWindow);
            Assert.AreEqual(5.5f, d.SteerRate);
            Assert.AreEqual(6.6f, d.RelaxMult);
            Assert.AreEqual(77, d.ShopCost);
            Assert.AreEqual(88, d.FirstCamp);
            RodStats s = d.ToCore();
            Assert.AreEqual((byte)3, s.Tier);
            Assert.AreEqual(1.1f, s.CastMaxDist);
            Assert.AreEqual(2.2f, s.ReelSpeed);
            Assert.AreEqual(3.3f, s.LineStrength);
            Assert.AreEqual(4.4f, s.AngleWindow);
            Assert.AreEqual(5.5f, s.SteerRate);
            Assert.AreEqual(6.6f, s.RelaxMult);
        }

        private void CheckBait()
        {
            BaitDef d = Make<BaitDef>();
            d.Configure("bait_x", "Bait X", 3, 4, 9, 1.25f, 0.75f, 2, true, FishTags.Bottom | FishTags.Surface, 1.7f, 0.6f);
            Assert.AreEqual("bait_x", d.Id);
            Assert.AreEqual("Bait X", d.DisplayName);
            Assert.AreEqual(3, d.Rating);
            Assert.AreEqual(4, d.StackMax);
            Assert.AreEqual(9, d.MaxRating);
            Assert.AreEqual(1.25f, d.BiteSpeedMod);
            Assert.AreEqual(0.75f, d.JunkMult);
            Assert.AreEqual(2, d.MaxNibbles);
            Assert.IsTrue(d.Unlimited);
            Assert.AreEqual(FishTags.Bottom | FishTags.Surface, d.AffinityTags);
            Assert.AreEqual(1.7f, d.AffinityMult);
            Assert.AreEqual(0.6f, d.SpecialMult);
            BaitStats s = d.ToCore();
            Assert.AreEqual(3, s.Rating);
            Assert.AreEqual(4, s.StackMax);
            Assert.AreEqual(9, s.MaxRating);
            Assert.AreEqual(1.25f, s.BiteSpeedMod);
            Assert.AreEqual(0.75f, s.JunkMult);
            Assert.AreEqual(2, s.MaxNibbles);
            Assert.IsTrue(s.Unlimited);
            // Catches: ToCore not mapping the M3 bait fields.
            Assert.AreEqual(FishTags.Bottom | FishTags.Surface, s.AffinityTags);
            Assert.AreEqual(1.7f, s.AffinityMult);
            Assert.AreEqual(0.6f, s.SpecialMult);

            // The M3 parameters are optional and trailing (M3-D75): an old call keeps compiling and gets the neutral defaults None / 1 / 1.
            d.Configure("bait_x", "Bait X", 3, 4, 9, 1.25f, 0.75f, 2, false);
            Assert.IsFalse(d.ToCore().Unlimited);
            Assert.AreEqual(FishTags.None, d.AffinityTags);
            Assert.AreEqual(1f, d.AffinityMult);
            Assert.AreEqual(1f, d.SpecialMult);
        }

        private void CheckFightProfile()
        {
            FightProfileDef d = Make<FightProfileDef>();
            d.Configure("prof_x", "Prof X", 0.55f, 0.66f, 0.77f, 0.88f, 0.99f, 0.12f, 0.23f, 0.34f, 0.45f, 0.56f, 0.67f,
                0.78f, 0.89f, 0.91f, 0.13f);
            Assert.AreEqual("prof_x", d.Id);
            Assert.AreEqual("Prof X", d.DisplayName);
            Assert.AreEqual(0.55f, d.CalmMin);
            Assert.AreEqual(0.66f, d.CalmMax);
            Assert.AreEqual(0.77f, d.BurstMin);
            Assert.AreEqual(0.88f, d.BurstMax);
            Assert.AreEqual(0.99f, d.FlipRate);
            Assert.AreEqual(0.12f, d.DiveChance);
            Assert.AreEqual(0.23f, d.BurstRamp);
            Assert.AreEqual(0.34f, d.CalmDrift);
            Assert.AreEqual(0.45f, d.CalmDriftRamp);
            Assert.AreEqual(0.56f, d.FlipMinGap);
            Assert.AreEqual(0.67f, d.LateralJitter);
            Assert.AreEqual(0.78f, d.StrainMod);
            Assert.AreEqual(0.89f, d.MoveMult);
            Assert.AreEqual(0.91f, d.ReelInMult);
            Assert.AreEqual(0.13f, d.WavePeriod);
            FishFightProfile p = d.ToCore();
            Assert.AreEqual(0.55f, p.CalmMin);
            Assert.AreEqual(0.66f, p.CalmMax);
            Assert.AreEqual(0.77f, p.BurstMin);
            Assert.AreEqual(0.88f, p.BurstMax);
            Assert.AreEqual(0.99f, p.FlipRate);
            Assert.AreEqual(0.12f, p.DiveChance);
            Assert.AreEqual(0.23f, p.BurstRamp);
            Assert.AreEqual(0.34f, p.CalmDrift);
            Assert.AreEqual(0.45f, p.CalmDriftRamp);
            Assert.AreEqual(0.56f, p.FlipMinGap);
            Assert.AreEqual(0.67f, p.LateralJitter);
            Assert.AreEqual(0.78f, p.StrainMod);
            Assert.AreEqual(0.89f, p.MoveMult);
            Assert.AreEqual(0.91f, p.ReelInMult);
            Assert.AreEqual(0.13f, p.WavePeriod);
        }

        private void CheckSpecies()
        {
            FightProfileDef fight = Make<FightProfileDef>();
            fight.Configure("prof_x", "Prof X", 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 4f);
            var colorA = new Color(0.1f, 0.2f, 0.3f, 1f);
            var colorB = new Color(0.4f, 0.5f, 0.6f, 1f);
            var mesh = new Mesh();
            created.Add(mesh);
            FishSpeciesDef d = Make<FishSpeciesDef>();
            d.Configure("fish_x", "Fish X", FishRarity.Uncommon, 11, 222, 333, 1.7f, 1.3f, fight, BodyShape.Flat, colorA, colorB,
                0.8f, 0.9f, true, mesh, FishTags.Predator | FishTags.Nocturnal);
            Assert.AreEqual("fish_x", d.Id);
            Assert.AreEqual("Fish X", d.DisplayName);
            Assert.AreEqual(FishRarity.Uncommon, d.Rarity);
            Assert.AreEqual(11, d.BaseValue);
            Assert.AreEqual(222, d.WeightMinG);
            Assert.AreEqual(333, d.WeightMaxG);
            Assert.AreEqual(1.7f, d.WeightSkew);
            Assert.AreEqual(1.3f, d.LengthShapeK);
            Assert.AreSame(fight, d.Fight);
            Assert.AreEqual(BodyShape.Flat, d.BodyShape);
            Assert.AreEqual(colorA, d.ColorA);
            Assert.AreEqual(colorB, d.ColorB);
            Assert.AreEqual(0.8f, d.BiteWaitMod);
            Assert.AreEqual(0.9f, d.HookWindow);
            Assert.IsTrue(d.IsSpecial);
            Assert.AreSame(mesh, d.Model);
            Assert.AreEqual(FishTags.Predator | FishTags.Nocturnal, d.Tags);

            FishSpeciesData s = d.ToCore(7);
            Assert.AreEqual(FishTags.Predator | FishTags.Nocturnal, s.Tags);   // Catches: ToCore not mapping tags
            Assert.AreEqual((ushort)7, s.FightProfileIndex);
            Assert.AreEqual(222, s.WeightMinG);
            Assert.AreEqual(333, s.WeightMaxG);
            Assert.AreEqual(11, s.BaseValue);
            Assert.AreEqual(1.7f, s.WeightSkew);
            Assert.AreEqual(1.3f, s.LengthShapeK);
            Assert.AreEqual(0.8f, s.BiteWaitMod);
            Assert.AreEqual(0.9f, s.HookWindow);
            Assert.IsTrue(s.IsSpecial);
            Assert.AreEqual(FishRarity.Uncommon, s.Rarity);
            Assert.AreEqual((ushort)65535, d.ToCore(65535).FightProfileIndex);

            d.Configure("fish_x", "Fish X", FishRarity.Rare, 11, 222, 333, 1.7f, 1.3f, null, BodyShape.Round, colorA, colorB,
                0.8f, 0.9f, false, null);
            Assert.IsNull(d.Fight);
            Assert.IsNull(d.Model);
            Assert.IsFalse(d.ToCore(0).IsSpecial);
            Assert.AreEqual(FishRarity.Rare, d.ToCore(0).Rarity);
            Assert.AreEqual(FishTags.None, d.Tags, "tags is an optional trailing parameter (M3-D75)");

            // Catches: Legendary misnumbered (spec 12.2 / M3-D2: Common 0, Uncommon 1, Rare 2, Legendary 3).
            d.Configure("fish_x", "Fish X", FishRarity.Legendary, 11, 222, 333, 1.7f, 1.3f, null, BodyShape.Round, colorA, colorB,
                0.8f, 0.9f, false, null);
            Assert.AreEqual((byte)3, (byte)d.Rarity);
            Assert.AreEqual(FishRarity.Legendary, d.ToCore(0).Rarity);
        }

        private void CheckJunk()
        {
            JunkDef d = Make<JunkDef>();
            var color = new Color(0.3f, 0.2f, 0.1f, 1f);
            d.Configure("junk_x", "Junk X", 4, BodyShape.Eel, color);
            Assert.AreEqual("junk_x", d.Id);
            Assert.AreEqual("Junk X", d.DisplayName);
            Assert.AreEqual(4, d.ValueCoins);
            Assert.AreEqual(BodyShape.Eel, d.BodyShape);
            Assert.AreEqual(color, d.Color);
            Assert.AreEqual(4, d.ToCore().ValueCoins);
        }

        // A FishingTuning with a distinct value in every member (bools differ so a swap shows), built by reflection so a member
        // added later is covered without editing this test.
        private static FishingTuning DistinctTuning(int creelGrams)
        {
            object boxed = new FishingTuning();
            FieldInfo[] fields = typeof(FishingTuning).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (f.FieldType == typeof(float)) f.SetValue(boxed, 1.5f + i * 0.37f);
                else if (f.FieldType == typeof(int)) f.SetValue(boxed, 100 + i);
                else if (f.FieldType == typeof(bool)) f.SetValue(boxed, f.Name == "ChargePingPong");
                else Assert.Fail("unexpected FishingTuning member type " + f.Name + " " + f.FieldType);
            }
            var t = (FishingTuning)boxed;
            t.CreelMaxWeightG = creelGrams;
            return t;
        }

        private void CheckTuning()
        {
            FieldInfo[] coreFields = typeof(FishingTuning).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(47, coreFields.Length, "FishingTuning has 47 members (43 + the 17 R72 fight members - the 13 R72 retired ones)");

            int serialized = 0;
            foreach (FieldInfo f in typeof(FishingTuningDef).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.GetCustomAttribute<SerializeField>() != null) serialized++;
            }
            Assert.AreEqual(47, serialized, "FishingTuningDef has 47 serialized fields (creelMaxKg replaces CreelMaxWeightG)");

            FishingTuning t = DistinctTuning(12345);
            FishingTuningDef d = Make<FishingTuningDef>();
            d.Configure(in t);

            Type defType = typeof(FishingTuningDef);
            foreach (FieldInfo f in coreFields)
            {
                if (f.Name == "CreelMaxWeightG") continue;
                PropertyInfo getter = defType.GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(getter, "FishingTuningDef has no getter " + f.Name);
                Assert.AreEqual(f.GetValue(t), getter.GetValue(d), "getter " + f.Name);
            }
            Assert.AreEqual(12.345f, d.CreelMaxKg, 1e-4f);

            FishingTuning back = d.ToCore();
            object boxedBack = back;
            foreach (FieldInfo f in coreFields)
            {
                Assert.AreEqual(f.GetValue(t), f.GetValue(boxedBack), "ToCore member " + f.Name);
            }
            Assert.AreEqual(12345, back.CreelMaxWeightG);
        }

        // Catches: ToCore truncating kg * 1000 instead of rounding (some grams / 1000f land just below the integer), or
        // Configure and ToCore using different factors.
        [Test]
        public void FishingTuningDef_CreelKg_RoundTripsEveryGram()
        {
            FishingTuningDef d = Make<FishingTuningDef>();
            for (int g = 0; g <= 100000; g++)
            {
                FishingTuning t = default;
                t.CreelMaxWeightG = g;
                d.Configure(in t);
                Assert.AreEqual(g, d.ToCore().CreelMaxWeightG, "grams " + g);
            }
        }

        private static readonly string[] TuningNames =
        {
            "castChargeTime", "chargePingPong", "castMinDist", "minLandDepth", "biteWaitMin", "biteWaitMax", "minBiteWait", "hookBeat",
            "landDistance", "spoolExtra", "relaxRate", "strainScale", "strainScaleAssist", "slackEscapeTime", "lateralMaxDeg", "hookedLeash",
            "junkDragSpeed", "resultTimeout", "commitTimeout", "hookTolerance", "maxFightTime", "rodStateHz", "cancelRefundsBait",
            "creelMaxEntries", "creelMaxKg", "cleanCatchMaxTension", "pityAfterFailures", "pityStrainMult", "pityGraceMult",
            "pityHookWindowBonus",
            "strainBarFull", "strainSnap", "strainRestInput", "strainReelRun", "strainReelBack", "strainCounterSteer", "strainOtherSteer",
            "sideMoveSpeed", "backMoveSpeed", "tireSteerRate", "tireCounterRate", "tireDecayRate", "tireRunFull", "tireReelFull",
            "tireReelMaxMult", "backPullMargin", "fightTimeout",
        };

        // Contract 7.13 serialized names. The builder, the T14 asset YAML and every saved asset depend on them: a renamed private field
        // silently drops the saved value. The property set must equal the list exactly (no extra field either).
        // Catches: a renamed or removed [SerializeField] field, a stray extra serialized field, slotTints / creelMaxWeightG reappearing.
        [TestCase("RodDef", "id,displayName,tier,castMaxDist,reelSpeed,lineStrength,angleWindow,steerRate,relaxMult,shopCost,firstCamp")]
        [TestCase("BaitDef", "id,displayName,rating,stackMax,maxRating,biteSpeedMod,junkMult,maxNibbles,unlimited,affinityTags,affinityMult,specialMult")]
        [TestCase("FightProfileDef", "id,displayName,calmMin,calmMax,burstMin,burstMax,flipRate,diveChance,burstRamp,calmDrift,calmDriftRamp,flipMinGap,lateralJitter,strainMod,moveMult,reelInMult,wavePeriod")]
        [TestCase("FishSpeciesDef", "id,displayName,rarity,baseValue,weightMinG,weightMaxG,weightSkew,lengthShapeK,fight,bodyShape,colorA,colorB,biteWaitMod,hookWindow,isSpecial,model,tags")]
        [TestCase("JunkDef", "id,displayName,valueCoins,bodyShape,color")]
        [TestCase("FishingTuningDef", "TUNING")]
        [TestCase("SfxCueTableDef", "rows,seasonAmbience")]   // M3 T9 appended seasonAmbience
        [TestCase("ArtPaletteDef", "slotNames,seasons,faceJitter,skinTone,phases,uiAccent,uiGood,uiWarn,uiBad,uiPanel,uiText,m2Season,sunMoon,weatherLooks")]   // M3 T9 appended sunMoon, weatherLooks
        [TestCase("WaterSettingsDef", "dirAmpLenSpeed,shallowColor,deepColor,depthRange,foamWidth")]
        [TestCase("ContentRegistry", "dataVersion,spots,rods,baits,fightProfiles,species,junk,fishingTuning,sfxCueTable,artPalette,waterSettings,zones,ecologyTuning")]   // M3 T9 appended zones, ecologyTuning
        public void FishingDefs_SerializedNames_MatchContract(string typeName, string csv)
        {
            ScriptableObject o;
            switch (typeName)
            {
                case "RodDef": o = Make<RodDef>(); break;
                case "BaitDef": o = Make<BaitDef>(); break;
                case "FightProfileDef": o = Make<FightProfileDef>(); break;
                case "FishSpeciesDef": o = Make<FishSpeciesDef>(); break;
                case "JunkDef": o = Make<JunkDef>(); break;
                case "FishingTuningDef": o = Make<FishingTuningDef>(); break;
                case "SfxCueTableDef": o = Make<SfxCueTableDef>(); break;
                case "ArtPaletteDef": o = Make<ArtPaletteDef>(); break;
                case "WaterSettingsDef": o = Make<WaterSettingsDef>(); break;
                case "ContentRegistry": o = Make<ContentRegistry>(); break;
                default: Assert.Fail("unknown Def " + typeName); return;
            }
            string[] expected = csv == "TUNING" ? TuningNames : csv.Split(',');
            if (csv == "TUNING") Assert.AreEqual(47, expected.Length, "47 serialized members (creelMaxKg replaces CreelMaxWeightG)");

            var so = new UnityEditor.SerializedObject(o);
            foreach (string name in expected)
                Assert.IsNotNull(so.FindProperty(name), typeName + " is missing serialized field '" + name + "'");

            var actual = new List<string>();
            UnityEditor.SerializedProperty it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.name != "m_Script") actual.Add(it.name);
            }
            var extra = new List<string>(actual);
            extra.RemoveAll(n => Array.IndexOf(expected, n) >= 0);
            Assert.IsEmpty(extra, typeName + " has serialized fields outside contract 7.13: " + string.Join(",", extra));
            Assert.AreEqual(expected.Length, actual.Count, typeName + " serialized field count");

            Assert.IsNull(so.FindProperty("slotTints"), "slotTints must not exist (M2-D3)");
            Assert.IsNull(so.FindProperty("creelMaxWeightG"), "creelMaxWeightG is stored as creelMaxKg");
        }

        // Catches: the m2Season initialiser dropped (default 0 = Spring).
        [Test]
        public void ArtPaletteDef_DefaultM2Season_IsSummer()
        {
            Assert.AreEqual(1, Make<ArtPaletteDef>().M2Season);
        }

        // Catches: GetSlotTint reading a stored copy or swapping channels, alpha not 1, an out-of-range slot returning a colour.
        [Test]
        public void ArtPaletteDef_SlotTint_EqualsSlotPalette()
        {
            ArtPaletteDef d = Make<ArtPaletteDef>();
            for (int slot = 0; slot < SlotPalette.Count; slot++)
            {
                SlotPalette.GetRgb(slot, out float r, out float g, out float b);
                Color c = d.GetSlotTint(slot);
                Assert.AreEqual(r, c.r, "slot " + slot + " r");
                Assert.AreEqual(g, c.g, "slot " + slot + " g");
                Assert.AreEqual(b, c.b, "slot " + slot + " b");
                Assert.AreEqual(1f, c.a, "slot " + slot + " a");
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => d.GetSlotTint(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => d.GetSlotTint(-1));

            var skin = new Color(0.1f, 0.2f, 0.3f, 1f);
            d.Configure(null, null, 0.04f, skin, null, default, default, default, default, default, default, 1);
            Assert.AreEqual(skin, d.GetSkinTone());
        }

        private static PaletteSeason Season(string name, int seasonIndex, int count)
        {
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                colors[i] = new Color(0.01f * (i + 1), 0.1f * (seasonIndex + 1), 0.02f * (i + 1), 0.5f + 0.01f * i);
            }
            return new PaletteSeason { name = name, colors = colors };
        }

        // Catches: a short Vector4[] (the global array must be full length), channel mix-ups, season index ignored, no magenta padding,
        // an out-of-range season returning data instead of throwing.
        [Test]
        public void ArtPaletteDef_PaletteVectors_LengthSixteen()
        {
            ArtPaletteDef d = Make<ArtPaletteDef>();
            var seasons = new[] { Season("Spring", 0, 16), Season("Summer", 1, 16), Season("Autumn", 2, 16), Season("Winter", 3, 16) };
            d.Configure(null, seasons, 0.04f, Color.white, null, default, default, default, default, default, default, 1);
            for (int s = 0; s < 4; s++)
            {
                Vector4[] v = d.PaletteVectors(s);
                Assert.AreEqual(16, v.Length);
                for (int i = 0; i < 16; i++)
                {
                    Color c = seasons[s].colors[i];
                    Assert.AreEqual(new Vector4(c.r, c.g, c.b, c.a), v[i], "season " + s + " slot " + i);
                }
            }
            Assert.AreNotSame(d.PaletteVectors(0), d.PaletteVectors(0), "a new array each call");

            d.Configure(null, new[] { Season("Spring", 0, 16), Season("Summer", 1, 3) }, 0.04f, Color.white, null, default, default, default, default, default, default, 1);
            Vector4[] padded = d.PaletteVectors(1);
            Assert.AreEqual(16, padded.Length);
            Assert.AreEqual(seasons[1].colors[2].r, padded[2].x);
            Assert.AreEqual(new Vector4(1f, 0f, 1f, 1f), padded[3]);
            Assert.AreEqual(new Vector4(1f, 0f, 1f, 1f), padded[15]);

            Assert.Throws<ArgumentOutOfRangeException>(() => d.PaletteVectors(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => d.PaletteVectors(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => d.PaletteVectors(2), "seasons shorter than season + 1");
        }

        // Catches: Configure keeping the caller's array/season objects (a later edit would change the Def).
        [Test]
        public void ArtPaletteDef_Configure_CopiesCollectionsDefensively()
        {
            ArtPaletteDef d = Make<ArtPaletteDef>();
            PaletteSeason season = Season("Spring", 0, 16);
            var seasons = new[] { season };
            var names = new[] { "A", "B" };
            d.Configure(names, seasons, 0.04f, Color.white, null, default, default, default, default, default, default, 1);
            names[0] = "changed";
            seasons[0] = null;
            season.colors[0] = Color.black;
            Assert.AreEqual("A", d.SlotNames[0]);
            Assert.IsNotNull(d.Seasons[0]);
            Assert.AreNotEqual(Color.black, d.Seasons[0].colors[0]);
            Assert.AreEqual(0, d.Phases.Count, "null collections read back as empty");
        }

        // Catches: components mapped in the wrong order (x/y/z/w -> direction/amplitude/wavelength/speed), a missing entry throwing.
        [Test]
        public void WaterSettingsDef_ToWaveSet_MapsComponents()
        {
            WaterSettingsDef d = Make<WaterSettingsDef>();
            d.Configure(new[] { new Vector4(20f, 0.05f, 9f, 1.2f), new Vector4(75f, 0.04f, 7f, 1.0f), new Vector4(320f, 0.025f, 6.5f, 0.8f) },
                new Color(0.1f, 0.2f, 0.3f, 0.4f), new Color(0.5f, 0.6f, 0.7f, 0.8f), 3.1f, 0.36f);
            WaveSet w = d.ToWaveSet();
            Assert.AreEqual(20f, w.W0.DirectionDeg);
            Assert.AreEqual(0.05f, w.W0.Amplitude);
            Assert.AreEqual(9f, w.W0.Wavelength);
            Assert.AreEqual(1.2f, w.W0.Speed);
            Assert.AreEqual(75f, w.W1.DirectionDeg);
            Assert.AreEqual(0.04f, w.W1.Amplitude);
            Assert.AreEqual(7f, w.W1.Wavelength);
            Assert.AreEqual(1.0f, w.W1.Speed);
            Assert.AreEqual(320f, w.W2.DirectionDeg);
            Assert.AreEqual(0.025f, w.W2.Amplitude);
            Assert.AreEqual(6.5f, w.W2.Wavelength);
            Assert.AreEqual(0.8f, w.W2.Speed);
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 0.4f), d.ShallowColor);
            Assert.AreEqual(new Color(0.5f, 0.6f, 0.7f, 0.8f), d.DeepColor);
            Assert.AreEqual(3.1f, d.DepthRange);
            Assert.AreEqual(0.36f, d.FoamWidth);

            d.Configure(new[] { new Vector4(20f, 0.05f, 9f, 1.2f), new Vector4(75f, 0.04f, 7f, 1.0f) }, default, default, 3f, 0.35f);
            WaveSet two = d.ToWaveSet();
            Assert.AreEqual(0f, two.W2.DirectionDeg);
            Assert.AreEqual(0f, two.W2.Amplitude);
            Assert.AreEqual(0f, two.W2.Wavelength);
            Assert.AreEqual(0f, two.W2.Speed);
            Assert.AreEqual(75f, two.W1.DirectionDeg);
        }

        // Catches: ToRecipes taking length or loop from the row (or defaulting them), the 9-arg ctor argument order (maxDistance,
        // minInterval, minDistance swapped), an unknown id not being skipped, row order lost, the result list being shared.
        [Test]
        public void SfxCueTableDef_ToRecipes_OverridesMixingKeepsLength()
        {
            SfxCueTableDef d = Make<SfxCueTableDef>();
            d.Configure(new[]
            {
                new SfxCueRow { id = SfxIds.SplashBig, category = AudioCategory.Ambience, baseVolume = 0.31f, minDistance = 2.5f, maxDistance = 41f, minInterval = 0.17f, spatial = false },
                new SfxCueRow { id = "no_such_cue", category = AudioCategory.Sfx, baseVolume = 1f, minDistance = 1f, maxDistance = 30f, minInterval = 0f, spatial = true },
                new SfxCueRow { id = SfxIds.LineCreak, category = AudioCategory.Voice, baseVolume = 0.2f, minDistance = 3.5f, maxDistance = 0f, minInterval = 0.05f, spatial = true },
            });

            IReadOnlyList<SfxRecipe> recipes = d.ToRecipes();
            Assert.AreEqual(2, recipes.Count, "the unknown id is skipped");

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.SplashBig, out SfxRecipe coreBig));
            SfxRecipe big = recipes[0];
            Assert.AreEqual(SfxIds.SplashBig, big.Id);
            Assert.AreEqual(coreBig.LengthSeconds, big.LengthSeconds);
            Assert.AreEqual(coreBig.Loop, big.Loop);
            Assert.AreEqual(AudioCategory.Ambience, big.Category);
            Assert.AreEqual(0.31f, big.BaseVolume);
            Assert.AreEqual(2.5f, big.MinDistance);
            Assert.AreEqual(41f, big.MaxDistance);
            Assert.AreEqual(0.17f, big.MinInterval);
            Assert.IsFalse(big.Spatial);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.LineCreak, out SfxRecipe coreCreak));
            Assert.IsTrue(coreCreak.Loop, "Line_Creak is a loop in Core");
            SfxRecipe creak = recipes[1];
            Assert.AreEqual(SfxIds.LineCreak, creak.Id);
            Assert.IsTrue(creak.Loop, "Loop comes from Core, not the row");
            Assert.AreEqual(coreCreak.LengthSeconds, creak.LengthSeconds);
            Assert.AreEqual(AudioCategory.Voice, creak.Category);
            Assert.AreEqual(0f, creak.MaxDistance, "MaxDistance 0 is kept (0 = the AudioBus default 30)");
            Assert.AreEqual(3.5f, creak.MinDistance);
            Assert.IsTrue(creak.Spatial);

            Assert.AreNotSame(recipes, d.ToRecipes(), "a new list each call");
            Assert.AreEqual(3, d.Rows.Count);
        }
    }
}
