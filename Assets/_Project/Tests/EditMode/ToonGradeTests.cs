// Authority: test-only. The toon colour grade (asset study 2026-10-14): ArtPaletteDef.ToonGradeFor and its fallback, the seed values, the committed
// ArtPalette asset against the seeds, and the PaletteGlobals push of the three grade globals.
// This assembly does not reference Presentation, so PaletteGlobals is reached by type name (as in SeasonalTreesTests).
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class ToonGradeTests
    {
        readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        bool _pushed;

        [TearDown]
        public void TearDown()
        {
            // A push leaves its grade in the shader globals: put the real asset's M2 season back before anything else renders in this session.
            if (_pushed) PushThroughPaletteGlobals(AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset), track: false);
            _pushed = false;
            SeasonalVisibility.SetSeason(SeasonalVisibility.DefaultSeason);
            foreach (UnityEngine.Object o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
        }

        // Catches: a palette built before the grades existed (or a short grade array) throwing or rendering ungraded instead of the default grade.
        [Test]
        public void ToonGradeFor_FallsBackToDefault_WhenMissingOrOutOfRange()
        {
            var palette = Track(ScriptableObject.CreateInstance<ArtPaletteDef>());
            Assert.AreEqual(0, palette.ToonGrades.Count);
            for (int s = -1; s <= ArtPaletteDef.SeasonCount; s++) AssertGrade(ToonGrade.Default, palette.ToonGradeFor(s), "no grades, season " + s);

            palette.ConfigureToonGrades(new[] { FishingSeedData.ToonGrades()[2] });
            AssertGrade(FishingSeedData.ToonGrades()[2], palette.ToonGradeFor(0), "configured season 0");
            AssertGrade(ToonGrade.Default, palette.ToonGradeFor(1), "beyond the configured array");
        }

        // Catches: ConfigureToonGrades keeping the caller's array (a later edit of it would change the asset).
        [Test]
        public void ConfigureToonGrades_CopiesTheArray()
        {
            var palette = Track(ScriptableObject.CreateInstance<ArtPaletteDef>());
            ToonGrade[] grades = FishingSeedData.ToonGrades();
            palette.ConfigureToonGrades(grades);
            grades[0].topStrength = 0.123f;
            Assert.AreNotEqual(0.123f, palette.ToonGradeFor(0).topStrength);
            palette.ConfigureToonGrades(null);
            Assert.AreEqual(0, palette.ToonGrades.Count);
        }

        // Catches: a seed that inverts the look (a cool top or a warm bottom), lightens the shade side, a strength outside 0..1, a tightness that
        // would make the smoothstep degenerate, a hue shift big enough to turn grass blue, or a Summer grade that is not the fallback.
        [Test]
        public void Seeds_AreWarmTopCoolBottom_InRange()
        {
            ToonGrade[] seeds = FishingSeedData.ToonGrades();
            Assert.AreEqual(ArtPaletteDef.SeasonCount, seeds.Length);
            AssertGrade(ToonGrade.Default, seeds[SeasonalLook.Summer], "Summer is ToonGrade.Default");
            for (int s = 0; s < seeds.Length; s++)
            {
                ToonGrade g = seeds[s];
                string l = "season " + s + " ";
                Assert.That(g.topGain.x, Is.GreaterThanOrEqualTo(g.topGain.z), l + "top is warm (r >= b)");
                Assert.That(g.topGain.x + g.topGain.y + g.topGain.z, Is.GreaterThan(3f), l + "top lightens");
                Assert.That(g.bottomGain.z, Is.GreaterThan(g.bottomGain.x), l + "bottom is cool (b > r)");
                Assert.That(g.bottomGain.x, Is.LessThan(1f), l + "bottom darkens red");
                Assert.That(g.topStrength, Is.InRange(0f, 1f), l + "topStrength");
                Assert.That(g.bottomStrength, Is.InRange(0f, 1f), l + "bottomStrength");
                Assert.That(g.shadeWeight, Is.InRange(0f, 1f), l + "shadeWeight");
                Assert.That(g.topTightness, Is.InRange(0f, 0.95f), l + "topTightness");
                Assert.That(g.hueShift, Is.InRange(0f, 0.05f), l + "hueShift");
            }
        }

        // Catches: the committed ArtPalette asset drifting from the seeds (the builder fills the grades only when the asset has none).
        [Test]
        public void ArtPaletteAsset_ToonGrades_EqualSeeds()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            Assert.IsNotNull(asset, "missing " + AssetPaths.ArtPaletteAsset + " (run the build)");
            ToonGrade[] seeds = FishingSeedData.ToonGrades();
            Assert.AreEqual(seeds.Length, asset.ToonGrades.Count, "toon grade count vs FishingSeedData.ToonGrades");
            for (int s = 0; s < seeds.Length; s++) AssertGrade(seeds[s], asset.ToonGrades[s], "asset season " + s);
        }

        // Catches: PaletteGlobals.Apply not pushing the grade, pushing another season's grade, or converting the gains as sRGB colours.
        [Test]
        public void PaletteGlobalsApply_PushesTheSeasonsGradeRaw()
        {
            var palette = Track(ScriptableObject.CreateInstance<ArtPaletteDef>());
            palette.Configure(FishingSeedData.PaletteSlotNames, FishingSeedData.PaletteSeasons(), FishingSeedData.PaletteFaceJitter,
                FishingSeedData.PaletteSkinTone, FishingSeedData.PalettePhases(), FishingSeedData.PaletteUiAccent, FishingSeedData.PaletteUiGood,
                FishingSeedData.PaletteUiWarn, FishingSeedData.PaletteUiBad, FishingSeedData.PaletteUiPanel, FishingSeedData.PaletteUiText,
                SeasonalLook.Autumn);
            palette.ConfigureToonGrades(FishingSeedData.ToonGrades());
            PushThroughPaletteGlobals(palette, track: true);

            ToonGrade autumn = FishingSeedData.ToonGrades()[SeasonalLook.Autumn];
            AssertVector(autumn.TopTintVector, Shader.GetGlobalVector(ShaderGlobals.TopTint), "_FGTopTint");
            AssertVector(autumn.BottomTintVector, Shader.GetGlobalVector(ShaderGlobals.BottomTint), "_FGBottomTint");
            AssertVector(autumn.ParamsVector, Shader.GetGlobalVector(ShaderGlobals.GradeParams), "_FGGradeParams");
        }

        // The two environment materials carry the full grade; the fish, clothes, skin and brass are ungraded (they render exactly as before).
        [Test]
        public void ToonMaterials_GradeOnlyOnTheEnvironment()
        {
            foreach (string name in new[] { "M_Toon_Base", "M_Toon_Tri" })
                Assert.AreEqual(1f, LoadMaterial(name).GetFloat("_FGGrade"), name + " _FGGrade");
            foreach (string name in new[] { "M_Toon_Fish", "M_Toon_FishFallback", "M_Toon_Brass", "M_Skin", "M_Body_Slot0" })
                Assert.AreEqual(0f, LoadMaterial(name).GetFloat("_FGGrade"), name + " _FGGrade");
        }

        static Material LoadMaterial(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
            Assert.IsNotNull(m, name);
            return m;
        }

        void PushThroughPaletteGlobals(ArtPaletteDef palette, bool track)
        {
            Assert.IsNotNull(palette, "palette");
            Type globalsType = Type.GetType("FishGame.Presentation.PaletteGlobals, FishGame.Presentation");
            Assert.IsNotNull(globalsType, "FishGame.Presentation.PaletteGlobals");
            if (track) _pushed = true;
            var host = new GameObject("PaletteGrade");
            try
            {
                host.SetActive(false);
                Component globals = host.AddComponent(globalsType);
                var so = new SerializedObject(globals);
                so.FindProperty("palette").objectReferenceValue = palette;
                so.ApplyModifiedPropertiesWithoutUndo();
                host.SetActive(true);   // OnEnable -> Apply
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static void AssertGrade(ToonGrade expected, ToonGrade actual, string label)
        {
            AssertVector(expected.TopTintVector, actual.TopTintVector, label + " top");
            AssertVector(expected.BottomTintVector, actual.BottomTintVector, label + " bottom");
            AssertVector(expected.ParamsVector, actual.ParamsVector, label + " params");
        }

        static void AssertVector(Vector4 expected, Vector4 actual, string label)
        {
            for (int i = 0; i < 4; i++) Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-5f), label + "[" + i + "]");
        }

        T Track<T>(T o) where T : UnityEngine.Object
        {
            _created.Add(o);
            return o;
        }
    }
}
