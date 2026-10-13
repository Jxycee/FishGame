// Authority: test-only (client-side UI + local rendering settings; no networked state). Drives the real Graphics tab of the Settings screen
// through UiFlowController in the Menu scene, and checks that GraphicsApplier puts the values on a RUNTIME COPY of the URP asset (never on
// the project's asset) and puts everything back when the app goes away. The user's real settings.json is saved/restored by TearDown.
using System.Collections;
using FishGame.App;
using FishGame.Core;
using FishGame.Presentation;
using FishGame.Runtime;
using FishGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FishGame.Tests.PlayMode
{
    public class GraphicsTabTests
    {
        private static readonly string[] RowNames =
        {
            "WindowMode", "Resolution", "FrameCap", "QualityPreset", "RenderScale", "ShadowQuality", "AntiAliasing",
        };

        private ISettings _settings;
        private UiFlowController _ui;
        private VisualElement _root;
        private GraphicsApplier _graphics;
        private SettingsData _saved;
        private RenderPipelineAsset _qualityBefore;
        private float _sourceScaleBefore, _sourceShadowBefore;
        private int _sourceMsaaBefore;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_settings != null && _saved != null)
            {
                SettingsData d = _settings.Current;
                d.graphicsPreset = _saved.graphicsPreset;
                d.renderScalePercent = _saved.renderScalePercent;
                d.shadowQuality = _saved.shadowQuality;
                d.antiAliasing = _saved.antiAliasing;
                d.vsync = _saved.vsync;
                d.frameRateCap = _saved.frameRateCap;
                d.windowMode = _saved.windowMode;
                d.resolutionWidth = _saved.resolutionWidth;
                d.resolutionHeight = _saved.resolutionHeight;
                _settings.NotifyChanged();
                _settings.Save();
            }

            _settings = null;
            _saved = null;
            _graphics = null;
            yield return PlayModeHarness.TearDownApp();
        }

        private IEnumerator OpenSettings(int preset)
        {
            _qualityBefore = QualitySettings.renderPipeline;   // what the app should put back when it goes away
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            var app = AppBootstrap.Current;
            _settings = app.Services.Get<ISettings>();
            _graphics = app.Services.Get<GraphicsApplier>();
            _ui = app.GetComponentInChildren<UiFlowController>(true);
            Assert.That(_ui, Is.Not.Null, "UiFlowController");
            _root = _ui.GetComponent<UIDocument>().rootVisualElement;
            _saved = _settings.Current.Clone();

            if (_graphics.SourceAsset != null)
            {
                _sourceScaleBefore = _graphics.SourceAsset.renderScale;
                _sourceShadowBefore = _graphics.SourceAsset.shadowDistance;
                _sourceMsaaBefore = _graphics.SourceAsset.msaaSampleCount;
            }

            // Start every test from a known preset through the real settings path (the applier follows Changed).
            ApplyPreset(preset);
            _ui.Show(UiScreen.Settings);
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
        }

        private void ApplyPreset(int preset)
        {
            Assert.IsTrue(GraphicsPresets.TryGet(preset, out GraphicsPresetValues v));
            SettingsData d = _settings.Current;
            d.graphicsPreset = preset;
            d.renderScalePercent = v.RenderScalePercent;
            d.shadowQuality = v.Shadows;
            d.antiAliasing = v.Aa;
            _settings.NotifyChanged();
        }

        private void OpenGraphicsTab()
        {
            var tab = _root.Q<Button>("TabGraphics");
            Assert.That(tab, Is.Not.Null, "TabGraphics");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = tab;
                tab.SendEvent(e);
            }
        }

        // Catches: the Graphics tab missing, or its rows not built/translated (a missing UXML name throws at bind time), the two pages showing at
        // once, or a label that is not plain ASCII (UiStrings keys are ASCII by project rule).
        [UnityTest]
        public IEnumerator GraphicsTab_Exists_ShowsItsRows_AndHidesTheGeneralPage()
        {
            yield return OpenSettings((int)QualityPreset.High);

            var general = _root.Q<ScrollView>("SettingsScroll");
            var graphics = _root.Q<ScrollView>("GraphicsScroll");
            Assert.That(general, Is.Not.Null);
            Assert.That(graphics, Is.Not.Null);
            Assert.That(general.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "General is the page that opens");
            Assert.That(graphics.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(_root.Q<Button>("TabGeneral").text, Is.EqualTo("General"));
            Assert.That(_root.Q<Button>("TabGraphics").text, Is.EqualTo("Graphics"));

            OpenGraphicsTab();
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(graphics.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the Graphics page is showing");
            Assert.That(general.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the General page is hidden");
            Assert.That(_root.Q<Button>("TabGraphics").ClassListContains("active"), Is.True);

            foreach (string name in RowNames)
            {
                var row = _root.Q<SliderInt>(name);
                Assert.That(row, Is.Not.Null, name);
                Assert.That(graphics.Contains(row), Is.True, name + " lives on the Graphics page");
                Assert.That(string.IsNullOrEmpty(row.label), Is.False, name + " has a label");
                foreach (char c in row.label) Assert.That(c, Is.LessThan('\u0080'), name + " label is ASCII: " + row.label);
            }

            Assert.That(_root.Q<Toggle>("VSync"), Is.Not.Null);
            Assert.That(graphics.Contains(_root.Q<Toggle>("VSync")), Is.True);
            Assert.That(_root.Q<SliderInt>("QualityPreset").label, Does.EndWith("High"));
            Assert.That(_root.Q<SliderInt>("AntiAliasing").label, Does.EndWith("MSAA 4x"));
            Assert.That(_root.Q<SliderInt>("RenderScale").label, Does.EndWith("100%"));

            // Rows are focusable (keyboard / gamepad), and the tab buttons are reachable too.
            Assert.That(_root.Q<SliderInt>("QualityPreset").focusable, Is.True);
            Assert.That(_root.Q<Button>("TabGraphics").focusable, Is.True);
            Assert.That(_root.Q<Button>("TabGeneral").focusable, Is.True);

            // Back to General: the original page returns.
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                var tab = _root.Q<Button>("TabGeneral");
                e.target = tab;
                tab.SendEvent(e);
            }

            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(general.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(graphics.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        // Catches: a preset that does not reach the render pipeline (render scale, shadows, MSAA, FXAA), High drifting from the project's authored
        // asset values, or the project's own URP asset being written instead of the runtime copy.
        [UnityTest]
        public IEnumerator Preset_LowThenHigh_ChangesTheRuntimeUrpCopy_NotTheProjectAsset()
        {
            yield return OpenSettings((int)QualityPreset.High);
            UniversalRenderPipelineAsset runtime = _graphics.RuntimeAsset;
            UniversalRenderPipelineAsset source = _graphics.SourceAsset;
            Assert.That(runtime, Is.Not.Null, "the test project renders with URP");
            Assert.That(source, Is.Not.Null);
            Assert.That(runtime, Is.Not.SameAs(source), "values go on a copy");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(runtime), "the copy is the active pipeline asset");

            Assert.That(runtime.renderScale, Is.EqualTo(1f).Within(1e-4f), "High: full render scale");
            Assert.That(runtime.shadowDistance, Is.EqualTo(70f), "High: the authored 70 m");
            Assert.That(runtime.shadowCascadeCount, Is.EqualTo(2));
            Assert.That(runtime.mainLightShadowmapResolution, Is.EqualTo(2048));
            Assert.That(runtime.msaaSampleCount, Is.EqualTo(4));
            Assert.That(_graphics.FxaaSelected, Is.False);

            OpenGraphicsTab();
            var preset = _root.Q<SliderInt>("QualityPreset");
            preset.value = (int)QualityPreset.Low;   // the real path: slider -> setIndex -> NotifyChanged -> GraphicsApplier

            Assert.That(_settings.Current.graphicsPreset, Is.EqualTo((int)QualityPreset.Low));
            Assert.That(_settings.Current.renderScalePercent, Is.EqualTo(75));
            Assert.That(_settings.Current.shadowQuality, Is.EqualTo((int)ShadowTier.Low));
            Assert.That(_settings.Current.antiAliasing, Is.EqualTo((int)AaMode.Fxaa));
            Assert.That(runtime.renderScale, Is.EqualTo(0.75f).Within(1e-4f), "Low: 75 percent render scale");
            Assert.That(runtime.shadowDistance, Is.EqualTo(25f), "Low: short shadow distance");
            Assert.That(runtime.shadowCascadeCount, Is.EqualTo(1));
            Assert.That(runtime.mainLightShadowmapResolution, Is.EqualTo(1024));
            Assert.That(runtime.msaaSampleCount, Is.EqualTo(1), "Low: no MSAA");
            Assert.That(_graphics.FxaaSelected, Is.True, "Low: FXAA instead");

            // The rows follow the preset (SetValueWithoutNotify), and the preset still reads Low.
            Assert.That(_root.Q<SliderInt>("RenderScale").value, Is.EqualTo(GraphicsPresets.RenderScaleToIndex(75)));
            Assert.That(_root.Q<SliderInt>("RenderScale").label, Does.EndWith("75%"));
            Assert.That(_root.Q<SliderInt>("ShadowQuality").value, Is.EqualTo((int)ShadowTier.Low));
            Assert.That(_root.Q<SliderInt>("AntiAliasing").value, Is.EqualTo((int)AaMode.Fxaa));
            Assert.That(preset.value, Is.EqualTo((int)QualityPreset.Low));
            Assert.That(preset.label, Does.EndWith("Low"));

            // The project's asset is untouched.
            Assert.That(source.renderScale, Is.EqualTo(_sourceScaleBefore).Within(1e-6f), "project URP asset render scale");
            Assert.That(source.shadowDistance, Is.EqualTo(_sourceShadowBefore).Within(1e-6f), "project URP asset shadow distance");
            Assert.That(source.msaaSampleCount, Is.EqualTo(_sourceMsaaBefore), "project URP asset MSAA");

            preset.value = (int)QualityPreset.High;
            Assert.That(runtime.renderScale, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(runtime.shadowDistance, Is.EqualTo(70f));
            Assert.That(runtime.shadowCascadeCount, Is.EqualTo(2));
            Assert.That(runtime.mainLightShadowmapResolution, Is.EqualTo(2048));
            Assert.That(runtime.msaaSampleCount, Is.EqualTo(4));
            Assert.That(_graphics.FxaaSelected, Is.False);
        }

        // Catches: "changing any row switches to Custom" not holding in the UI, a Custom preset row that rewrites the rows, and shadows Off not
        // zeroing the shadow distance.
        [UnityTest]
        public IEnumerator ChangingARow_SwitchesToCustom_AndShadowsOffZeroTheDistance()
        {
            yield return OpenSettings((int)QualityPreset.High);
            OpenGraphicsTab();
            var preset = _root.Q<SliderInt>("QualityPreset");
            Assert.That(preset.value, Is.EqualTo((int)QualityPreset.High));

            var scale = _root.Q<SliderInt>("RenderScale");
            scale.value = GraphicsPresets.RenderScaleToIndex(80);
            Assert.That(_settings.Current.renderScalePercent, Is.EqualTo(80));
            Assert.That(_settings.Current.graphicsPreset, Is.EqualTo((int)QualityPreset.Custom), "a row change switches the preset to Custom");
            Assert.That(preset.value, Is.EqualTo((int)QualityPreset.Custom), "the preset row shows Custom");
            Assert.That(preset.label, Does.EndWith("Custom"));
            Assert.That(_graphics.RuntimeAsset.renderScale, Is.EqualTo(0.8f).Within(1e-4f));

            // Picking Custom by hand changes no row.
            preset.value = (int)QualityPreset.Custom;
            Assert.That(_settings.Current.renderScalePercent, Is.EqualTo(80));

            var shadows = _root.Q<SliderInt>("ShadowQuality");
            shadows.value = (int)ShadowTier.Off;
            Assert.That(_graphics.RuntimeAsset.shadowDistance, Is.EqualTo(0f), "Off: URP skips the main-light shadow pass at distance 0");
            shadows.value = (int)ShadowTier.High;
            Assert.That(_graphics.RuntimeAsset.shadowDistance, Is.EqualTo(70f), "back on");
            Assert.That(_graphics.RuntimeAsset.mainLightShadowmapResolution, Is.EqualTo(2048));

            var aa = _root.Q<SliderInt>("AntiAliasing");
            aa.value = (int)AaMode.Msaa2x;
            Assert.That(_graphics.RuntimeAsset.msaaSampleCount, Is.EqualTo(2));
            Assert.That(_graphics.FxaaSelected, Is.False);
            aa.value = (int)AaMode.Fxaa;
            Assert.That(_graphics.RuntimeAsset.msaaSampleCount, Is.EqualTo(1));
            Assert.That(_graphics.FxaaSelected, Is.True);

            // An unrelated settings change (a volume slider) leaves the applied values alone.
            _settings.Current.masterVolume = _saved.masterVolume;
            _settings.NotifyChanged();
            Assert.That(_graphics.RuntimeAsset.renderScale, Is.EqualTo(0.8f).Within(1e-4f));
        }

        // Catches: FXAA reaching the settings but not the cameras (the player camera spawns later, so the applier must reach any Game camera),
        // a non-FXAA mode leaving FXAA on, and the applier stomping an authored mode on a camera it never touched.
        [UnityTest]
        public IEnumerator Fxaa_IsPutOnGameCameras_AndTakenOffAgain()
        {
            yield return OpenSettings((int)QualityPreset.Low);   // FXAA
            var go = new GameObject("GraphicsTabTests camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.antialiasing = AntialiasingMode.None;

                _graphics.ApplyToCamera(cam);
                Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.FastApproximateAntialiasing));

                ApplyPreset((int)QualityPreset.High);   // MSAA 4x
                _graphics.ApplyToCamera(cam);
                Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.None), "FXAA is taken off when another mode is chosen");

                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                _graphics.ApplyToCamera(cam);
                Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing), "an authored mode is not overwritten");
            }
            finally
            {
                Object.Destroy(go);
            }
        }

        // Catches: the app leaving the runtime copy installed (a leak into the next test or, in the editor, into the project settings), or the
        // project's asset being modified. Runs after the app is gone, so it does its own teardown first.
        [UnityTest]
        public IEnumerator AppTeardown_PutsTheOriginalPipelineAssetBack()
        {
            yield return OpenSettings((int)QualityPreset.Low);
            UniversalRenderPipelineAsset source = _graphics.SourceAsset;
            Assert.That(source, Is.Not.Null);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(_graphics.RuntimeAsset), "the copy is installed while the app runs");
            RenderPipelineAsset before = _qualityBefore;
            float scale = _sourceScaleBefore;

            // TearDown (UnityTearDown) restores the settings and destroys the app; mirror the order here to check the result.
            SettingsData d = _settings.Current;
            d.graphicsPreset = _saved.graphicsPreset;
            d.renderScalePercent = _saved.renderScalePercent;
            d.shadowQuality = _saved.shadowQuality;
            d.antiAliasing = _saved.antiAliasing;
            _settings.NotifyChanged();
            _settings.Save();
            _saved = null;   // TearDown has nothing more to restore
            yield return PlayModeHarness.TearDownApp();

            Assert.That(QualitySettings.renderPipeline, Is.SameAs(before), "the original quality-level pipeline asset is back");
            Assert.That(source.renderScale, Is.EqualTo(scale).Within(1e-6f), "the project asset was never modified");
            Assert.That(source.shadowDistance, Is.EqualTo(_sourceShadowBefore).Within(1e-6f));
        }
    }
}
