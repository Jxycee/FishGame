// Authority: test-only (client-side UI; no networked state). Drives the real Settings screen through UiFlowController in the Menu scene.
using System.Collections;
using FishGame.App;
using FishGame.Core;
using FishGame.Runtime;
using FishGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FishGame.Tests.PlayMode
{
    public class UiSettingsTests
    {
        private ISettings _settings;
        private UiFlowController _ui;
        private VisualElement _root;
        private int _savedScale, _savedGauge;
        private bool _savedAssist;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_settings != null)
            {
                _settings.Current.uiScalePercent = _savedScale;
                _settings.Current.lineGauge = _savedGauge;
                _settings.Current.fishingAssist = _savedAssist;
                _settings.NotifyChanged();
                _settings.Save();
            }
            _settings = null;
            yield return PlayModeHarness.TearDownApp();
        }

        private IEnumerator OpenSettings(int uiScale, int lineGauge, bool assist)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            var app = AppBootstrap.Current;
            _settings = app.Services.Get<ISettings>();
            _ui = app.GetComponentInChildren<UiFlowController>(true);
            Assert.That(_ui, Is.Not.Null, "UiFlowController");
            _root = _ui.GetComponent<UIDocument>().rootVisualElement;

            _savedScale = _settings.Current.uiScalePercent;
            _savedGauge = _settings.Current.lineGauge;
            _savedAssist = _settings.Current.fishingAssist;
            _settings.Current.uiScalePercent = uiScale;
            _settings.Current.lineGauge = lineGauge;
            _settings.Current.fishingAssist = assist;
            _settings.NotifyChanged();

            _ui.Show(UiScreen.Settings);
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
        }

        // Catches: at 150% UI scale the Settings viewport shrinks live and the UiScale row (once the last row of the list) fell below it with
        // nothing to scroll to, so the slider that caused it could not be reached. The row now sits at the top and is scrolled into view after a change.
        [UnityTest]
        public IEnumerator Settings_UiScale150_SliderStaysInsideScrollViewport()
        {
            yield return OpenSettings(100, 0, false);

            var slider = _root.Q<SliderInt>("UiScale");
            var scroll = _root.Q<ScrollView>("SettingsScroll");
            Assert.That(slider, Is.Not.Null, "UiScale slider");
            Assert.That(scroll, Is.Not.Null, "SettingsScroll");

            slider.value = 150;   // the real path: slider -> NotifyChanged -> UiFlowController.ApplyUiScale + the presenter's scroll-to
            yield return PlayModeHarness.WaitFrames(2, 0.2f);

            Assert.That(_settings.Current.uiScalePercent, Is.EqualTo(150), "uiScalePercent");
            Assert.That(_ui.GetComponent<UIDocument>().panelSettings.scale, Is.EqualTo(1.5f).Within(1e-4f), "panel scale applied live");

            Rect viewport = scroll.contentViewport.worldBound;
            Rect row = slider.worldBound;
            Assert.That(viewport.height, Is.GreaterThan(10f), "scroll viewport has a size");
            Assert.That(row.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - 0.5f), "UiScale row top is inside the viewport (viewport " + viewport + ", row " + row + ")");
            Assert.That(row.yMax, Is.LessThanOrEqualTo(viewport.yMax + 0.5f), "UiScale row bottom is inside the viewport (viewport " + viewport + ", row " + row + ")");
            Assert.That(scroll.verticalScrollerVisibility, Is.EqualTo(ScrollerVisibility.AlwaysVisible), "the scroller must always be visible");
        }

        // Wiring test (ruling G-Q4): the Settings toggle, not just FishingHudRules, turns the gauge to Arc when assist goes false -> true while the gauge is Off,
        // writes ISettings.Current, and refreshes the LineGauge slider; the rule re-applies every time, not once.
        [UnityTest]
        public IEnumerator Settings_AssistOn_WithGaugeOff_SetsGaugeToArc_EveryTime()
        {
            yield return OpenSettings(100, (int)LineGaugeMode.Off, false);

            var toggle = _root.Q<Toggle>("FishingAssist");
            var gauge = _root.Q<SliderInt>("LineGauge");
            Assert.That(toggle, Is.Not.Null, "FishingAssist toggle");
            Assert.That(gauge, Is.Not.Null, "LineGauge slider");
            Assert.That(toggle.panel, Is.Not.Null, "the toggle must be attached to a panel for ChangeEvent to fire");
            Assert.That(toggle.value, Is.False, "assist starts off");
            Assert.That(gauge.value, Is.EqualTo((int)LineGaugeMode.Off), "gauge starts Off");

            toggle.value = true;
            Assert.That(_settings.Current.fishingAssist, Is.True, "fishingAssist");
            Assert.That(_settings.Current.lineGauge, Is.EqualTo((int)LineGaugeMode.Arc), "assist on with the gauge Off sets the gauge to Arc");
            Assert.That(gauge.value, Is.EqualTo((int)LineGaugeMode.Arc), "the LineGauge slider shows Arc");
            Assert.That(gauge.label, Does.EndWith(UiStrings.Get("settings.gauge_arc")), "the LineGauge label shows Arc");

            // Gauge back to Off by hand, assist off, assist on again: Arc again.
            gauge.value = (int)LineGaugeMode.Off;
            Assert.That(_settings.Current.lineGauge, Is.EqualTo((int)LineGaugeMode.Off), "gauge set back to Off");
            toggle.value = false;
            Assert.That(_settings.Current.lineGauge, Is.EqualTo((int)LineGaugeMode.Off), "assist off leaves the gauge alone");
            toggle.value = true;
            Assert.That(_settings.Current.lineGauge, Is.EqualTo((int)LineGaugeMode.Arc), "assist on again re-applies Arc");
            Assert.That(gauge.value, Is.EqualTo((int)LineGaugeMode.Arc), "the LineGauge slider shows Arc again");

            // A gauge the player chose (Bar) is never overwritten.
            gauge.value = (int)LineGaugeMode.Bar;
            toggle.value = false;
            toggle.value = true;
            Assert.That(_settings.Current.lineGauge, Is.EqualTo((int)LineGaugeMode.Bar), "a chosen gauge is left alone");
        }
    }
}
