using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class TensionCuesTests
    {
        [Test]
        public void TensionCues_BandBoundaries_MatchSpecTable()
        {
            (float tension, bool grace, TensionBand band)[] rows =
            {
                (0.29f, false, TensionBand.Rest), (0.3f, false, TensionBand.Bend), (0.59f, false, TensionBand.Bend),
                (0.6f, false, TensionBand.Creak), (0.79f, false, TensionBand.Creak), (0.8f, false, TensionBand.Warn),
                (1.0f, false, TensionBand.Warn), (1.0f, true, TensionBand.Grace),
            };
            foreach (var r in rows)
            {
                Assert.That(TensionCues.BandOf(r.tension, r.grace), Is.EqualTo(r.band), r.tension + " grace " + r.grace);
                TensionCueState c = TensionCues.Evaluate(r.tension, r.grace, 1.0f, 1f);
                Assert.That(c.Band, Is.EqualTo(r.band));
                Assert.That(c.CrackNow, Is.EqualTo(r.band == TensionBand.Grace), "CrackNow " + r.tension);
                Assert.That(c.LineWarn, Is.EqualTo(r.band >= TensionBand.Warn), "LineWarn " + r.tension);
            }
            TensionCueState rest = TensionCues.Evaluate(0.1f, false, 1.3f, 1f);
            Assert.That(rest.RodBend01, Is.EqualTo(0f));
            Assert.That(rest.ReelClickRate, Is.EqualTo(1.3f * 6f).Within(1e-5f));
            Assert.That(TensionCues.Evaluate(1.0f, false, 1f, 1f).RodBend01, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TensionCues_CreakPitch_RisesToTwoPointOne()
        {
            float[] tension = { 0.5f, 0.6f, 0.8f, 1.0f };
            float[] want = { 1.0f, 1.0f, 1.55f, 2.1f };
            float prev = 0f;
            for (int i = 0; i < tension.Length; i++)
            {
                float pitch = TensionCues.Evaluate(tension[i], false, 1f, 1f).CreakPitch;
                Assert.That(pitch, Is.EqualTo(want[i]).Within(1e-5f), "tension " + tension[i]);
                Assert.That(pitch, Is.GreaterThanOrEqualTo(prev));
                prev = pitch;
            }
        }

        [Test]
        public void TensionCues_CameraMotionZero_RemovesShakeTraumaAndRumble()
        {
            foreach (var row in new[] { (0.9f, false), (1.0f, true) })
            {
                TensionCueState off = TensionCues.Evaluate(row.Item1, row.Item2, 1f, 0f);
                TensionCueState on = TensionCues.Evaluate(row.Item1, row.Item2, 1f, 1f);
                Assert.That(on.Shake, Is.True);
                Assert.That(on.TraumaPerSecond, Is.GreaterThan(0f));
                Assert.That(on.RumbleLow, Is.GreaterThan(0f));
                Assert.That(off.Shake, Is.False);
                Assert.That(off.TraumaPerSecond, Is.EqualTo(0f));
                Assert.That(off.RumbleLow, Is.EqualTo(0f));
                Assert.That(off.RumbleHigh, Is.EqualTo(0f));
                Assert.That(off.CreakVolume01, Is.EqualTo(on.CreakVolume01));
                Assert.That(off.LineWarn, Is.EqualTo(on.LineWarn));
            }
        }
    }
}
