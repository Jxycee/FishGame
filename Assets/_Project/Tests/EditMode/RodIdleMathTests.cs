using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // F6 (playtest 2026-10-13): the rod has idle life (breathing, movement and look sway, a slight sag with the line out) and the strain bend stays
    // dominant (S12: a near-snap must read from the rod alone). Pure Core maths of RodPresenter.
    public class RodIdleMathTests
    {
        // Catches: idle motion that survives camera motion 0 (spec 9.11 removes bob), idle motion that fights the strain bend, a stowed rod still
        // swaying, NaN leaking into the pose.
        [Test]
        public void RodIdleMath_IdleWeight_FullAtRestGoneUnderStrainAndAtMotionZero()
        {
            Assert.That(RodIdleMath.IdleWeight(0f, 1f, 0f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(RodIdleMath.IdleWeight(0f, 0f, 0f), Is.EqualTo(0f), "camera motion 0: exactly none");
            Assert.That(RodIdleMath.IdleWeight(0f, 0.5f, 0f), Is.EqualTo(0.5f).Within(1e-6f), "scaled by camera motion");
            Assert.That(RodIdleMath.IdleWeight(RodIdleMath.FadeEndBend01, 1f, 0f), Is.EqualTo(0f).Within(1e-6f), "gone once the bend is clearly visible");
            Assert.That(RodIdleMath.IdleWeight(1f, 1f, 0f), Is.EqualTo(0f));
            Assert.That(RodIdleMath.IdleWeight(0f, 1f, 1f), Is.EqualTo(0f), "fully stowed: none");
            Assert.That(RodIdleMath.IdleWeight(0f, 1f, 0.25f), Is.EqualTo(0.75f).Within(1e-6f), "fades out with the stow");
            Assert.That(RodIdleMath.IdleWeight(float.NaN, 1f, 0f), Is.EqualTo(0f));
            Assert.That(RodIdleMath.IdleWeight(0f, float.NaN, 0f), Is.EqualTo(0f));
            float last = 2f;
            for (float b = 0f; b <= 1f; b += 0.02f)
            {
                float w = RodIdleMath.IdleWeight(b, 1f, 0f);
                Assert.That(w, Is.LessThanOrEqualTo(last + 1e-6f), "a harder bend never brings idle motion back, at " + b);
                Assert.That(w, Is.InRange(0f, 1f));
                last = w;
            }
        }

        // Catches: an idle pose that is as large as a real bend (S12 readability), a sag that stays when the line is tight, a bend that goes
        // DOWN as the tension rises (the blend must be monotone), a max bend that is no longer reached.
        [Test]
        public void RodIdleMath_TotalBend_SagFadesIntoTheStrainAndStrainStaysDominant()
        {
            const float max = 50f;
            Assert.That(RodIdleMath.TotalBendDeg(max, 0f, 0f), Is.EqualTo(0f), "no line out: no sag, no bend");
            Assert.That(RodIdleMath.TotalBendDeg(max, 0f, 1f), Is.EqualTo(RodIdleMath.SagDeg).Within(1e-5f), "line out, no tension: the slight sag");
            Assert.That(RodIdleMath.TotalBendDeg(max, 1f, 1f), Is.EqualTo(max).Within(1e-5f), "full strain: the full bend, no sag left");
            Assert.That(RodIdleMath.TotalBendDeg(max, 0.5f, 0f), Is.EqualTo(25f).Within(1e-5f), "no sag: exactly the old strain bend");
            float last = -1f;
            for (float b = 0f; b <= 1f; b += 0.01f)
            {
                float t = RodIdleMath.TotalBendDeg(max, b, 1f);
                Assert.That(t, Is.GreaterThanOrEqualTo(last - 1e-5f), "the bend never drops as strain rises, at " + b);
                last = t;
            }

            // The biggest idle pose (sag + breathing + lag) is well under the bend of a mid strain (tension 0.5): the strain bend dominates.
            float strain05 = max * TensionCues.Evaluate(0.5f, false, 1f, 1f).RodBend01;
            Assert.That(RodIdleMath.MaxIdleDeg, Is.LessThan(strain05 * 0.6f), "idle at most 60% of the bend at tension 0.5 (" + strain05 + " deg)");
            Assert.That(RodIdleMath.MaxIdleDeg, Is.EqualTo(RodIdleMath.SagDeg + RodIdleMath.BreathXDeg + RodIdleMath.LagMaxDeg).Within(1e-5f));
            Assert.That(RodIdleMath.SagDeg, Is.InRange(2f, 5f), "slight: visible, never a bend");
        }

        // Catches: the tip leaning the wrong way (it must trail the motion), no clamp (a flick throws the rod), NaN or a teleport spike moving it.
        [Test]
        public void RodIdleMath_Lag_TipTrailsTheMotionClampedAndTeleportSafe()
        {
            Assert.That(RodIdleMath.LagXDeg(0f), Is.EqualTo(0f));
            Assert.That(RodIdleMath.LagXDeg(1f), Is.EqualTo(RodIdleMath.LagDegPerMps).Within(1e-6f), "moving up: the tip lags DOWN (positive = tip down)");
            Assert.That(RodIdleMath.LagXDeg(-1f), Is.EqualTo(-RodIdleMath.LagDegPerMps).Within(1e-6f), "moving down: the tip lags up");
            Assert.That(RodIdleMath.LagYDeg(1f), Is.EqualTo(-RodIdleMath.LagDegPerMps).Within(1e-6f), "moving right: the tip lags LEFT (negative about Y)");
            Assert.That(RodIdleMath.LagYDeg(-1f), Is.EqualTo(RodIdleMath.LagDegPerMps).Within(1e-6f));
            Assert.That(RodIdleMath.LagXDeg(RodIdleMath.MaxSpeed), Is.EqualTo(RodIdleMath.LagMaxDeg).Within(1e-5f), "a fast move reaches, never passes, the clamp");
            Assert.That(RodIdleMath.LagXDeg(1000f), Is.EqualTo(0f), "a teleport (respawn, ladder snap) is ignored, not clamped to a full lean");
            Assert.That(RodIdleMath.LagYDeg(-1000f), Is.EqualTo(0f));
            Assert.That(RodIdleMath.LagXDeg(float.NaN), Is.EqualTo(0f));
            Assert.That(RodIdleMath.LagYDeg(float.PositiveInfinity), Is.EqualTo(0f));
            Assert.That(RodIdleMath.SanitizeSpeed(5f), Is.EqualTo(5f));
            Assert.That(RodIdleMath.SanitizeSpeed(-RodIdleMath.MaxSpeed - 0.01f), Is.EqualTo(0f));
        }

        // Catches: breathing that is constant (a static rod again), larger than its stated amplitude, or not smooth (a jump between frames).
        [Test]
        public void RodIdleMath_Breath_SlowSmallAndAlive()
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            float prevX = 0f, prevY = 0f;
            for (int i = 0; i <= 2000; i++)
            {
                float t = i * 0.01f;
                RodIdleMath.Breath(t, out float x, out float y);
                minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x);
                minY = System.Math.Min(minY, y); maxY = System.Math.Max(maxY, y);
                if (i > 0)
                {
                    Assert.That(System.Math.Abs(x - prevX), Is.LessThan(0.05f), "smooth in X at " + t);
                    Assert.That(System.Math.Abs(y - prevY), Is.LessThan(0.05f), "smooth in Y at " + t);
                }
                prevX = x; prevY = y;
            }

            Assert.That(maxX, Is.GreaterThan(RodIdleMath.BreathXDeg * 0.9f), "reaches its amplitude");
            Assert.That(minX, Is.LessThan(-RodIdleMath.BreathXDeg * 0.9f));
            Assert.That(maxX, Is.LessThanOrEqualTo(RodIdleMath.BreathXDeg + 1e-5f));
            Assert.That(maxY, Is.GreaterThan(RodIdleMath.BreathYDeg * 0.9f));
            Assert.That(minY, Is.LessThan(-RodIdleMath.BreathYDeg * 0.9f));
            Assert.That(maxY, Is.LessThanOrEqualTo(RodIdleMath.BreathYDeg + 1e-5f));
            RodIdleMath.Breath(float.NaN, out float nx, out float ny);
            Assert.That(nx, Is.EqualTo(0f));
            Assert.That(ny, Is.EqualTo(0f));
        }

        // Catches: a spring that never settles, one that overshoots wildly (a rubbery rod), one that blows up on a long frame or a bad dt, one that
        // does not move at all (no sway), and a rest value that is not exactly zero.
        [Test]
        public void RodIdleMath_Spring_SettlesWithASmallOvershootAndIsStable()
        {
            float pos = 0f, vel = 0f, peak = 0f;
            for (int i = 0; i < 180; i++)
            {
                RodIdleMath.SpringStep(ref pos, ref vel, 1f, 1f / 60f);
                peak = System.Math.Max(peak, pos);
                if (i == 5) Assert.That(pos, Is.GreaterThan(0.02f), "it moves at once");
            }

            Assert.That(pos, Is.EqualTo(1f).Within(0.01f), "settled on the target after 3 s");
            Assert.That(peak, Is.GreaterThan(1f), "a little overshoot: the rod has spring");
            Assert.That(peak, Is.LessThan(1.2f), "but not rubbery");

            pos = 0f; vel = 0f;
            RodIdleMath.SpringStep(ref pos, ref vel, 1f, 5f);   // a hitch
            Assert.That(float.IsNaN(pos) || float.IsInfinity(pos), Is.False);
            Assert.That(pos, Is.InRange(-0.5f, 1.5f), "a long frame cannot blow it up");
            pos = 0.3f; vel = 0.2f;
            RodIdleMath.SpringStep(ref pos, ref vel, 1f, float.NaN);
            Assert.That(pos, Is.EqualTo(0.3f), "NaN dt: unchanged");
            Assert.That(vel, Is.EqualTo(0.2f));
            RodIdleMath.SpringStep(ref pos, ref vel, 1f, -1f);
            Assert.That(pos, Is.EqualTo(0.3f), "negative dt: unchanged");

            pos = 0f; vel = 0f;
            for (int i = 0; i < 100; i++) RodIdleMath.SpringStep(ref pos, ref vel, 0f, 1f / 60f);
            Assert.That(pos, Is.EqualTo(0f), "no input: no motion");
            Assert.That(vel, Is.EqualTo(0f));
        }

        // Catches: a sag that snaps on instead of easing, a rate that overshoots, a bad dt moving the value.
        [Test]
        public void RodIdleMath_Ease_ExponentialAndGuarded()
        {
            Assert.That(RodIdleMath.Ease(0f, 1f, 4f, 0.25f), Is.EqualTo(1f - (float)System.Math.Exp(-1.0)).Within(1e-5f));
            Assert.That(RodIdleMath.Ease(0.5f, 0f, 4f, 100f), Is.EqualTo(0f).Within(1e-5f), "a long frame lands on the target, never past it");
            Assert.That(RodIdleMath.Ease(0.4f, 1f, 4f, 0f), Is.EqualTo(0.4f));
            Assert.That(RodIdleMath.Ease(0.4f, 1f, 4f, float.NaN), Is.EqualTo(0.4f));
            Assert.That(RodIdleMath.Ease(0.4f, 1f, 4f, -1f), Is.EqualTo(0.4f));
        }
    }
}
