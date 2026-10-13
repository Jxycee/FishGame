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

        // Exact cue values (spec 9.11 table + C 7.7 rumble ramp: low 0.6 r, high 0.4 r with r = (t - 0.8) / 0.2; Grace 0.8 / 0.6).
        // Catches: wrong ramp slope or start, wrong Grace rumble constants, camera motion not scaling rumble/trauma, trauma 0.1 changed,
        // Shake/LineDash set in the Creak band or missing in Grace, wrong LineWarm / Creak / RodBend windows.
        [Test]
        public void TensionCues_ExactValues_AtSpecPoints()
        {
            TensionCueState w = TensionCues.Evaluate(0.9f, false, 1f, 1f);
            Assert.That(w.Band, Is.EqualTo(TensionBand.Warn));
            Assert.That(w.RumbleLow, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(w.RumbleHigh, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(w.TraumaPerSecond, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(w.Shake, Is.True);
            Assert.That(w.LineDash, Is.True);
            Assert.That(w.LineWarn, Is.True);
            Assert.That(w.CrackNow, Is.False);

            TensionCueState wHalf = TensionCues.Evaluate(0.9f, false, 1f, 0.5f);
            Assert.That(wHalf.RumbleLow, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(wHalf.RumbleHigh, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(wHalf.TraumaPerSecond, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(wHalf.Shake, Is.True, "any non-zero camera motion keeps the shake flag");

            TensionCueState start = TensionCues.Evaluate(0.8f, false, 1f, 1f);
            Assert.That(start.Band, Is.EqualTo(TensionBand.Warn));
            Assert.That(start.RumbleLow, Is.EqualTo(0f).Within(1e-6f), "the ramp starts at 0.8");
            Assert.That(start.RumbleHigh, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(start.Shake, Is.True);
            Assert.That(start.LineDash, Is.True);
            TensionCueState top = TensionCues.Evaluate(1.0f, false, 1f, 1f);
            Assert.That(top.RumbleLow, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(top.RumbleHigh, Is.EqualTo(0.4f).Within(1e-5f));

            TensionCueState g = TensionCues.Evaluate(1.0f, true, 1f, 1f);
            Assert.That(g.Band, Is.EqualTo(TensionBand.Grace));
            Assert.That(g.RumbleLow, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(g.RumbleHigh, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(g.LineDash, Is.True, "line dash stays on in Grace");
            Assert.That(g.Shake, Is.True);
            Assert.That(g.CrackNow, Is.True);
            Assert.That(g.TraumaPerSecond, Is.EqualTo(0.1f).Within(1e-6f));
            TensionCueState gHalf = TensionCues.Evaluate(1.0f, true, 1f, 0.5f);
            Assert.That(gHalf.RumbleLow, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(gHalf.RumbleHigh, Is.EqualTo(0.3f).Within(1e-5f));

            Assert.That(TensionCues.Evaluate(0.7f, false, 1f, 1f).LineWarm01, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(TensionCues.Evaluate(0.8f, false, 1f, 1f).CreakVolume01, Is.EqualTo(0.646964f).Within(1e-5f), "R72: Rust strainGainCurve");
            Assert.That(TensionCues.Evaluate(0.65f, false, 1f, 1f).RodBend01, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(TensionCues.Evaluate(0.6f, false, 1f, 1f).LineWarm01, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(TensionCues.Evaluate(0.8f, false, 1f, 1f).LineWarm01, Is.EqualTo(1f).Within(1e-6f));

            TensionCueState creak = TensionCues.Evaluate(0.79f, false, 1f, 1f);
            Assert.That(creak.Band, Is.EqualTo(TensionBand.Creak));
            Assert.That(creak.Shake, Is.False);
            Assert.That(creak.LineDash, Is.False);
            Assert.That(creak.LineWarn, Is.False);
            Assert.That(creak.TraumaPerSecond, Is.EqualTo(0f));
            Assert.That(creak.RumbleLow, Is.EqualTo(0f));
            Assert.That(creak.RumbleHigh, Is.EqualTo(0f));

            // Camera motion is clamped to 0..1.
            TensionCueState over = TensionCues.Evaluate(0.9f, false, 1f, 3f);
            Assert.That(over.RumbleLow, Is.EqualTo(0.3f).Within(1e-5f));
            TensionCueState under = TensionCues.Evaluate(0.9f, false, 1f, -1f);
            Assert.That(under.RumbleLow, Is.EqualTo(0f));
            Assert.That(under.Shake, Is.False);
        }

        // Catches (R72): a linear ramp, a curve that still starts at the pre-R72 0.6 band, a wrong end tangent (the Hermite coefficients are
        // derived in TensionCues: keys (0.10, 0, flat) and (0.95, 1, slope 2.66)), a creak that stays on at or below 0.10.
        [Test]
        public void TensionCues_CreakGain_FollowsRustCurve()
        {
            float[] t = { 0.05f, 0.10f, 0.2f, 0.3f, 0.525f, 0.8f, 0.9f, 0.95f, 1.2f, float.NaN };
            float[] want = { 0f, 0f, 0.010653f, 0.044313f, 0.217375f, 0.646964f, 0.872213f, 1f, 1f, 0f };
            for (int i = 0; i < t.Length; i++)
                Assert.That(TensionCues.CreakGain(t[i]), Is.EqualTo(want[i]).Within(1e-5f), "tension " + t[i]);
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float g = TensionCues.CreakGain(i * 0.01f);
                Assert.That(g, Is.GreaterThanOrEqualTo(prev), "non-decreasing at " + i * 0.01f);
                prev = g;
            }
            Assert.That(TensionCues.Evaluate(0.10f, false, 1f, 1f).CreakOn, Is.False);
            Assert.That(TensionCues.Evaluate(0.11f, false, 1f, 1f).CreakOn, Is.True);
            Assert.That(TensionCues.Evaluate(0f, true, 1f, 1f).CreakOn, Is.True, "in grace the creak is on regardless");
            Assert.That(TensionCues.Evaluate(0.2f, false, 1f, 1f).CreakVolume01, Is.EqualTo(0.010653f).Within(1e-5f));
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
