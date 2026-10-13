// Authority: none (pure EditMode tests). The Graphics tab's numbers (FishGame.Core.GraphicsPresets) and their settings.json round trip
// (SettingsData / SettingsStore). No QualitySettings or URP asset is touched here: EditMode writes would reach the project files. The
// applied URP values are pinned in PlayMode (GraphicsApplierTests).
using System;
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class GraphicsSettingsTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fgtests-graphics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        // The main instance defaults to the project's full-screen window; a second local instance (a -dev-profile such as the test runs' fgtest,
        // or an MPPM clone) defaults to Windowed so two built instances do not cover each other.
        private static int ExpectedDefaultWindowMode()
        {
            return InstanceProfile.Current.Suffix.Length == 0 ? (int)WindowModeKind.Borderless : (int)WindowModeKind.Windowed;
        }

        // Catches: a table edit that silently changes what Low means (the GTX 900 class target) or the default for new installs.
        [Test]
        public void Presets_LowMediumHigh_HaveTheAgreedValues()
        {
            Assert.IsTrue(GraphicsPresets.TryGet((int)QualityPreset.Low, out GraphicsPresetValues low));
            Assert.AreEqual(75, low.RenderScalePercent);
            Assert.AreEqual((int)ShadowTier.Low, low.Shadows);
            Assert.AreEqual((int)AaMode.Fxaa, low.Aa, "Low: no MSAA, FXAA instead");

            Assert.IsTrue(GraphicsPresets.TryGet((int)QualityPreset.Medium, out GraphicsPresetValues med));
            Assert.AreEqual(100, med.RenderScalePercent);
            Assert.AreEqual((int)ShadowTier.Low, med.Shadows);
            Assert.AreEqual((int)AaMode.Msaa2x, med.Aa);

            Assert.IsTrue(GraphicsPresets.TryGet((int)QualityPreset.High, out GraphicsPresetValues high));
            Assert.AreEqual(100, high.RenderScalePercent);
            Assert.AreEqual((int)ShadowTier.High, high.Shadows);
            Assert.AreEqual((int)AaMode.Msaa4x, high.Aa);

            Assert.IsFalse(GraphicsPresets.TryGet((int)QualityPreset.Custom, out _), "Custom is not a table row");
            Assert.IsFalse(GraphicsPresets.TryGet(-1, out _));
            Assert.AreEqual(QualityPreset.High, GraphicsPresets.DefaultPreset);
        }

        [Test]
        public void Match_ReturnsThePresetOrCustom()
        {
            Assert.AreEqual((int)QualityPreset.Low, GraphicsPresets.Match(75, (int)ShadowTier.Low, (int)AaMode.Fxaa));
            Assert.AreEqual((int)QualityPreset.Medium, GraphicsPresets.Match(100, (int)ShadowTier.Low, (int)AaMode.Msaa2x));
            Assert.AreEqual((int)QualityPreset.High, GraphicsPresets.Match(100, (int)ShadowTier.High, (int)AaMode.Msaa4x));
            Assert.AreEqual((int)QualityPreset.Custom, GraphicsPresets.Match(80, (int)ShadowTier.Low, (int)AaMode.Fxaa), "one row off");
            Assert.AreEqual((int)QualityPreset.Custom, GraphicsPresets.Match(75, (int)ShadowTier.Off, (int)AaMode.Fxaa));
            Assert.AreEqual((int)QualityPreset.Custom, GraphicsPresets.Match(75, (int)ShadowTier.Low, (int)AaMode.Off));
        }

        // Catches: Low not being cheaper than High on every lever (the whole point of the preset), and a Low that still uses MSAA.
        [Test]
        public void Low_IsCheaperThanMediumIsCheaperThanHigh()
        {
            GraphicsPresets.TryGet(0, out GraphicsPresetValues low);
            GraphicsPresets.TryGet(1, out GraphicsPresetValues med);
            GraphicsPresets.TryGet(2, out GraphicsPresetValues high);
            Assert.LessOrEqual(low.RenderScalePercent, med.RenderScalePercent);
            Assert.LessOrEqual(med.RenderScalePercent, high.RenderScalePercent);
            Assert.Less(low.RenderScalePercent, high.RenderScalePercent);

            ShadowParams sLow = GraphicsPresets.ShadowFor(low.Shadows), sHigh = GraphicsPresets.ShadowFor(high.Shadows);
            Assert.Less(sLow.Distance, sHigh.Distance);
            Assert.Less(sLow.Resolution, sHigh.Resolution);
            Assert.LessOrEqual(sLow.Cascades, sHigh.Cascades);

            Assert.AreEqual(1, GraphicsPresets.MsaaSamples(low.Aa), "Low has no MSAA");
            Assert.IsTrue(GraphicsPresets.UsesFxaa(low.Aa));
            Assert.AreEqual(2, GraphicsPresets.MsaaSamples(med.Aa));
            Assert.AreEqual(4, GraphicsPresets.MsaaSamples(high.Aa));
            Assert.IsFalse(GraphicsPresets.UsesFxaa(high.Aa));
        }

        // Catches: High drifting from the authored project look (PC_RPAsset: 70 m, 2048, 2 cascades) and Off not disabling shadows.
        [Test]
        public void ShadowFor_MapsTiersToUrpValues()
        {
            ShadowParams off = GraphicsPresets.ShadowFor((int)ShadowTier.Off);
            ShadowParams low = GraphicsPresets.ShadowFor((int)ShadowTier.Low);
            ShadowParams high = GraphicsPresets.ShadowFor((int)ShadowTier.High);
            Assert.AreEqual(0f, off.Distance, "Off: distance 0 turns the shadow pass off in URP");
            Assert.AreEqual(25f, low.Distance);
            Assert.AreEqual(1024, low.Resolution);
            Assert.AreEqual(1, low.Cascades);
            Assert.AreEqual(70f, high.Distance);
            Assert.AreEqual(2048, high.Resolution);
            Assert.AreEqual(2, high.Cascades);
            Assert.AreEqual(high.Distance, GraphicsPresets.ShadowFor(99).Distance, "out of range clamps to the top tier");
            Assert.AreEqual(off.Distance, GraphicsPresets.ShadowFor(-5).Distance, "out of range clamps to the bottom tier");
        }

        [Test]
        public void RenderScaleRows_AreFiftyToHundredInFives_AndRoundTrip()
        {
            Assert.AreEqual(11, GraphicsPresets.RenderScaleCount);
            Assert.AreEqual(50, GraphicsPresets.RenderScaleFromIndex(0));
            Assert.AreEqual(75, GraphicsPresets.RenderScaleFromIndex(5));
            Assert.AreEqual(100, GraphicsPresets.RenderScaleFromIndex(10));
            Assert.AreEqual(100, GraphicsPresets.RenderScaleFromIndex(99));
            for (int i = 0; i < GraphicsPresets.RenderScaleCount; i++)
            {
                Assert.AreEqual(i, GraphicsPresets.RenderScaleToIndex(GraphicsPresets.RenderScaleFromIndex(i)));
            }

            Assert.AreEqual(5, GraphicsPresets.RenderScaleToIndex(76), "nearest row for a hand-edited value");
            Assert.AreEqual(0, GraphicsPresets.RenderScaleToIndex(10));
        }

        [Test]
        public void ResolutionList_IsNativeFirst_Distinct_Ascending_AndKeepsAStoredMode()
        {
            var modes = new List<(int, int)> { (1920, 1080), (1280, 720), (1920, 1080), (800, 600), (320, 240), (2560, 1440) };
            List<(int Width, int Height)> list = GraphicsPresets.BuildResolutionList(modes, 0, 0);
            Assert.AreEqual((0, 0), list[0], "index 0 is native");
            CollectionAssert.AreEqual(new[] { (0, 0), (800, 600), (1280, 720), (1920, 1080), (2560, 1440) }, list, "distinct, ascending, tiny modes dropped");

            // A stored mode the monitor does not list stays selectable.
            List<(int Width, int Height)> withStored = GraphicsPresets.BuildResolutionList(modes, 1600, 900);
            Assert.AreEqual(6, withStored.Count);
            Assert.AreEqual(3, GraphicsPresets.ResolutionIndex(withStored, 1600, 900), "native, 800x600, 1280x720, then the stored 1600x900");
            Assert.AreEqual(0, GraphicsPresets.ResolutionIndex(withStored, 0, 0));
            Assert.AreEqual(0, GraphicsPresets.ResolutionIndex(withStored, 1234, 567), "unknown falls back to native");
            Assert.AreEqual(0, GraphicsPresets.ResolutionIndex(null, 1, 1));
            Assert.AreEqual(1, GraphicsPresets.BuildResolutionList(null, 0, 0).Count, "no modes: only native");
        }

        [Test]
        public void NewSettings_DefaultToHigh_AndMatchTheHighPreset()
        {
            var d = new SettingsData();
            Assert.AreEqual((int)QualityPreset.High, d.graphicsPreset);
            GraphicsPresets.TryGet(d.graphicsPreset, out GraphicsPresetValues high);
            Assert.AreEqual(high.RenderScalePercent, d.renderScalePercent);
            Assert.AreEqual(high.Shadows, d.shadowQuality);
            Assert.AreEqual(high.Aa, d.antiAliasing);
            Assert.IsTrue(d.vsync);
            Assert.AreEqual(GraphicsPresets.DefaultFrameCapIndex, d.frameRateCap);
            Assert.AreEqual(0, GraphicsPresets.FrameCaps[d.frameRateCap], "unlimited");
            Assert.AreEqual(ExpectedDefaultWindowMode(), d.windowMode, "the project's full-screen window; Windowed for a secondary instance");
            Assert.AreEqual(0, d.resolutionWidth);
            Assert.AreEqual(0, d.resolutionHeight);
            d.Clamp();
            Assert.AreEqual((int)QualityPreset.High, d.graphicsPreset, "Clamp keeps a consistent preset");
        }

        // Catches (the user's saved settings must survive): an older settings.json without the Graphics keys losing its values or failing to load.
        [Test]
        public void OlderSettingsFile_WithoutGraphicsKeys_KeepsItsValuesAndGetsHighDefaults()
        {
            string path = Path.Combine(_dir, "old.json");
            File.WriteAllText(path,
                "{\"version\":1,\"masterVolume\":35,\"displayName\":\"Ann\",\"uiScalePercent\":125,\"fieldOfView\":100,\"lineGauge\":2," +
                "\"captions\":false,\"fishingAssist\":true,\"streamerMode\":true,\"localPlayerId\":\"0123456789abcdef0123456789abcdef\"}");
            var store = new SettingsStore(path);
            store.Load();
            SettingsData d = store.Current;
            Assert.AreEqual(35, d.masterVolume);
            Assert.AreEqual("Ann", d.displayName);
            Assert.AreEqual(125, d.uiScalePercent);
            Assert.AreEqual(100f, d.fieldOfView);
            Assert.AreEqual(2, d.lineGauge);
            Assert.IsFalse(d.captions);
            Assert.IsTrue(d.fishingAssist);
            Assert.IsTrue(d.streamerMode);
            Assert.AreEqual("0123456789abcdef0123456789abcdef", d.localPlayerId);
            Assert.AreEqual(1, d.version, "C-Q10: no version bump");

            Assert.AreEqual((int)QualityPreset.High, d.graphicsPreset);
            Assert.AreEqual(100, d.renderScalePercent);
            Assert.AreEqual((int)ShadowTier.High, d.shadowQuality);
            Assert.AreEqual((int)AaMode.Msaa4x, d.antiAliasing);
            Assert.IsTrue(d.vsync);
            Assert.AreEqual(ExpectedDefaultWindowMode(), d.windowMode);
            Assert.AreEqual(0, d.resolutionWidth);

            // And a save writes the new keys, so the next load reads them back.
            store.Save();
            string text = File.ReadAllText(path);
            StringAssert.Contains("\"graphicsPreset\"", text);
            StringAssert.Contains("\"renderScalePercent\"", text);
            StringAssert.Contains("\"masterVolume\": 35", text);
        }

        [Test]
        public void GraphicsFields_SaveThenLoad_RoundTrip()
        {
            string path = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(path);
            store.Load();
            SettingsData d = store.Current;
            d.graphicsPreset = (int)QualityPreset.Low;
            d.renderScalePercent = 75;
            d.shadowQuality = (int)ShadowTier.Low;
            d.antiAliasing = (int)AaMode.Fxaa;
            d.vsync = false;
            d.frameRateCap = 1;
            d.windowMode = (int)WindowModeKind.Windowed;
            d.resolutionWidth = 1600;
            d.resolutionHeight = 900;
            store.Save();

            var reloaded = new SettingsStore(path);
            reloaded.Load();
            SettingsData r = reloaded.Current;
            Assert.AreEqual((int)QualityPreset.Low, r.graphicsPreset);
            Assert.AreEqual(75, r.renderScalePercent);
            Assert.AreEqual((int)ShadowTier.Low, r.shadowQuality);
            Assert.AreEqual((int)AaMode.Fxaa, r.antiAliasing);
            Assert.IsFalse(r.vsync);
            Assert.AreEqual(1, r.frameRateCap);
            Assert.AreEqual((int)WindowModeKind.Windowed, r.windowMode);
            Assert.AreEqual(1600, r.resolutionWidth);
            Assert.AreEqual(900, r.resolutionHeight);

            SettingsData clone = r.Clone();
            Assert.AreEqual(75, clone.renderScalePercent, "Clone keeps the Graphics fields");
            Assert.AreEqual((int)QualityPreset.Low, clone.graphicsPreset);
        }

        // Catches: "changing any row switches to Custom" not holding (the Clamp that runs on every NotifyChanged owns the rule).
        [TestCase(0, 80, 1, 1)]
        [TestCase(0, 75, 2, 1)]
        [TestCase(0, 75, 1, 0)]
        [TestCase(1, 100, 1, 3)]
        [TestCase(2, 100, 2, 2)]
        public void Clamp_ARowThatNoLongerMatchesThePreset_SwitchesToCustom(int preset, int scale, int shadows, int aa)
        {
            var d = new SettingsData { graphicsPreset = preset, renderScalePercent = scale, shadowQuality = shadows, antiAliasing = aa };
            d.Clamp();
            Assert.AreEqual((int)QualityPreset.Custom, d.graphicsPreset);
            Assert.AreEqual(scale, d.renderScalePercent, "the rows are kept as set");
            Assert.AreEqual(shadows, d.shadowQuality);
            Assert.AreEqual(aa, d.antiAliasing);
        }

        [Test]
        public void Clamp_MatchingPresetStays_AndCustomIsNeverPromoted()
        {
            var low = new SettingsData { graphicsPreset = 0, renderScalePercent = 75, shadowQuality = 1, antiAliasing = 1 };
            low.Clamp();
            Assert.AreEqual((int)QualityPreset.Low, low.graphicsPreset);

            var custom = new SettingsData { graphicsPreset = (int)QualityPreset.Custom, renderScalePercent = 75, shadowQuality = 1, antiAliasing = 1 };
            custom.Clamp();
            Assert.AreEqual((int)QualityPreset.Custom, custom.graphicsPreset, "an explicit Custom stays Custom even when the rows equal Low");
        }

        [Test]
        public void Clamp_OutOfRangeGraphicsValues_AreClamped()
        {
            var d = new SettingsData
            {
                graphicsPreset = 9, renderScalePercent = 10, shadowQuality = 7, antiAliasing = -2, frameRateCap = 40, windowMode = 5,
                resolutionWidth = 100, resolutionHeight = 5000,
            };
            d.Clamp();
            Assert.AreEqual((int)QualityPreset.Custom, d.graphicsPreset);
            Assert.AreEqual(GraphicsPresets.MinRenderScalePercent, d.renderScalePercent);
            Assert.AreEqual((int)ShadowTier.High, d.shadowQuality);
            Assert.AreEqual((int)AaMode.Off, d.antiAliasing);
            Assert.AreEqual(GraphicsPresets.FrameCaps.Length - 1, d.frameRateCap);
            Assert.AreEqual((int)WindowModeKind.Windowed, d.windowMode);
            Assert.AreEqual(0, d.resolutionWidth, "a resolution under 640x480 means native");
            Assert.AreEqual(0, d.resolutionHeight);

            var big = new SettingsData { renderScalePercent = 400 };
            big.Clamp();
            Assert.AreEqual(100, big.renderScalePercent);
        }
    }
}
