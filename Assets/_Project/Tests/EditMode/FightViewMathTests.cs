using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // R72 presentation maths (plan rework-fight F5; spec 9.11): Rust client values, pure Core.
    public class FightViewMathTests
    {
        // Catches: a smoothing rate other than 10 per second, an unclamped step that overshoots on a long frame, a bad dt moving the bar.
        [Test]
        public void FightViewMath_SmoothBar_RustTenPerSecond()
        {
            Assert.That(FightViewMath.SmoothBar(0f, 1f, 0.02f), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(FightViewMath.SmoothBar(0.5f, 1f, 0.05f), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(FightViewMath.SmoothBar(0f, 1f, 0.5f), Is.EqualTo(1f).Within(1e-6f), "clamped: never overshoots");
            Assert.That(FightViewMath.SmoothBar(0.3f, 1f, 0f), Is.EqualTo(0.3f));
            Assert.That(FightViewMath.SmoothBar(0.3f, 1f, float.NaN), Is.EqualTo(0.3f));
            Assert.That(FightViewMath.SmoothBar(0.3f, 1f, -1f), Is.EqualTo(0.3f));
        }

        // Catches: the tilt pointing the wrong way, a back pull not halved, a rest that does not centre, a roll of the wrong size or sign.
        [Test]
        public void FightViewMath_Tilt_TowardThePullHalvedOnBackPull()
        {
            Assert.That(FightViewMath.TiltTarget(false, true, true), Is.EqualTo(0.5f));
            Assert.That(FightViewMath.TiltTarget(false, false, false), Is.EqualTo(0.5f));
            Assert.That(FightViewMath.TiltTarget(true, false, false), Is.EqualTo(0f));
            Assert.That(FightViewMath.TiltTarget(true, true, false), Is.EqualTo(1f));
            Assert.That(FightViewMath.TiltTarget(true, false, true), Is.EqualTo(0.25f));
            Assert.That(FightViewMath.TiltTarget(true, true, true), Is.EqualTo(0.75f));
            Assert.That(FightViewMath.TiltStep(0f, 1f, 0.25f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(FightViewMath.TiltStep(0f, 1f, 1.5f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(FightViewMath.TiltStep(0.4f, 1f, float.NaN), Is.EqualTo(0.4f));
            Assert.That(FightViewMath.RollDeg(0f), Is.EqualTo(30f).Within(1e-5f));
            Assert.That(FightViewMath.RollDeg(1f), Is.EqualTo(-30f).Within(1e-5f));
            Assert.That(FightViewMath.RollDeg(0.5f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(FightViewMath.RollDeg(0.25f), Is.EqualTo(15f).Within(1e-5f));
            Assert.That(FightViewMath.RollDeg(0.75f), Is.EqualTo(-15f).Within(1e-5f));
        }

        // Catches: a gauge track that ends at 1.0 instead of the snap point (no headroom zone), an unclamped fill, NaN leaking into the layout.
        [Test]
        public void FightViewMath_GaugeFraction_TrackEndsAtTheSnapPoint()
        {
            float snap = 7f / 6f;
            Assert.That(FightViewMath.GaugeFraction(1f, snap), Is.EqualTo(0.857143f).Within(1e-5f), "the full mark sits at 6/7 of the track");
            Assert.That(FightViewMath.GaugeFraction(snap, snap), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(FightViewMath.GaugeFraction(2f, snap), Is.EqualTo(1f));
            Assert.That(FightViewMath.GaugeFraction(-1f, snap), Is.EqualTo(0f));
            Assert.That(FightViewMath.GaugeFraction(0.5f, 0f), Is.EqualTo(0.5f));
            Assert.That(FightViewMath.GaugeFraction(float.NaN, snap), Is.EqualTo(0f));
        }

        // Catches: a creak that stops dead, a fade of another length, a fade that never finishes, a fade that is not re-armed when wanted again.
        [Test]
        public void FightViewMath_CreakFade_QuarterSecondThenStop()
        {
            float left = 0f;
            Assert.That(FightViewMath.CreakFadeStep(true, ref left, 0.02f, out bool stop), Is.EqualTo(1f));
            Assert.That(left, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(stop, Is.False);
            Assert.That(FightViewMath.CreakFadeStep(false, ref left, 0.1f, out stop), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(stop, Is.False);
            Assert.That(FightViewMath.CreakFadeStep(false, ref left, 0.1f, out stop), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(stop, Is.False);
            Assert.That(FightViewMath.CreakFadeStep(false, ref left, 0.1f, out stop), Is.EqualTo(0f));
            Assert.That(stop, Is.True);
            Assert.That(FightViewMath.CreakFadeStep(true, ref left, 0.02f, out stop), Is.EqualTo(1f), "wanted again re-arms the fade");
            Assert.That(left, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(stop, Is.False);
        }
    }
}
