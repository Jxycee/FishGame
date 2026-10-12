// T25 carry-forward (C 16.2, ruling G-Q4). FishingHudRules lives in the UI assembly, which EditMode tests never reference (C 2.8 #8), so the
// pure helper is reached by reflection (the UI assembly is loaded in the editor domain), the same exception T23's FishFallbackMeshTests use.
using System;
using System.Reflection;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class FishingAssistRuleTests
    {
        private static readonly Type RulesType = Type.GetType("FishGame.UI.FishingHudRules, FishGame.UI", true);

        private static bool OnAssistChanged(SettingsData data, bool oldValue, bool newValue)
        {
            MethodInfo method = RulesType.GetMethod("OnAssistChanged", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "FishingHudRules.OnAssistChanged");
            return (bool)method.Invoke(null, new object[] { data, oldValue, newValue });
        }

        private static SettingsData WithGauge(LineGaugeMode mode) => new SettingsData { lineGauge = (int)mode };

        // Catches: "sets Arc only the first time" (a static or persisted once-flag). The same helper runs three times in one test method,
        // each time from gauge Off, so a flag that survives the first call leaves the later gauges at Off.
        [Test]
        public void OnAssistChanged_FalseToTrue_WhileGaugeOff_SetsArcEveryTime()
        {
            for (int i = 0; i < 3; i++)
            {
                SettingsData data = WithGauge(LineGaugeMode.Off);
                bool changed = OnAssistChanged(data, false, true);
                Assert.That(data.lineGauge, Is.EqualTo((int)LineGaugeMode.Arc), "call " + i);
                Assert.That(changed, Is.True, "reports the write so the slider is refreshed (call " + i + ")");
            }
        }

        // Catches: "changes the gauge when assist turns off" (any change, or the false edge, writes the gauge) and a rule that fires on a
        // non-edge (off to off, on to on) while the gauge is Off.
        [TestCase(LineGaugeMode.Off)]
        [TestCase(LineGaugeMode.Arc)]
        [TestCase(LineGaugeMode.Bar)]
        public void OnAssistChanged_TurningOffOrNoEdge_LeavesGaugeAlone(LineGaugeMode start)
        {
            foreach (bool[] change in new[] { new[] { true, false }, new[] { false, false }, new[] { true, true } })
            {
                SettingsData data = WithGauge(start);
                bool changed = OnAssistChanged(data, change[0], change[1]);
                Assert.That(data.lineGauge, Is.EqualTo((int)start), "old=" + change[0] + " new=" + change[1]);
                Assert.That(changed, Is.False);
            }
        }

        // Catches: "overwrites Bar/Arc when the gauge is already on" (assist on forces Arc regardless of the gauge, so Bar becomes Arc).
        [TestCase(LineGaugeMode.Arc)]
        [TestCase(LineGaugeMode.Bar)]
        public void OnAssistChanged_FalseToTrue_WhileGaugeAlreadyOn_KeepsTheChosenGauge(LineGaugeMode start)
        {
            SettingsData data = WithGauge(start);
            bool changed = OnAssistChanged(data, false, true);
            Assert.That(data.lineGauge, Is.EqualTo((int)start));
            Assert.That(changed, Is.False);
        }

        // The player can turn the gauge off separately while assist stays on: no edge, no rewrite; a fresh false-to-true edge sets Arc again.
        [Test]
        public void OnAssistChanged_GaugeTurnedOffWhileAssistOn_StaysOffUntilTheNextEdge()
        {
            SettingsData data = WithGauge(LineGaugeMode.Off);
            Assert.That(OnAssistChanged(data, false, true), Is.True);
            data.lineGauge = (int)LineGaugeMode.Off;
            Assert.That(OnAssistChanged(data, true, true), Is.False);
            Assert.That(data.lineGauge, Is.EqualTo((int)LineGaugeMode.Off));
            Assert.That(OnAssistChanged(data, true, false), Is.False);
            Assert.That(OnAssistChanged(data, false, true), Is.True);
            Assert.That(data.lineGauge, Is.EqualTo((int)LineGaugeMode.Arc));
        }

        [Test]
        public void OnAssistChanged_NullData_DoesNothing()
        {
            Assert.That(OnAssistChanged(null, false, true), Is.False);
        }
    }
}
