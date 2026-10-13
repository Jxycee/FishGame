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

        // ---- F9 (playtest 2026-10-13): the dive plunge must be unmistakable ----

        // Catches: a dive that only dips the float (old 0.35 m: part of the stem still showed), a dive that is not deeper than the Bite plunge
        // (0.25) and so reads as one, a dive that is not fully under at the largest distance scale, a spent fish that sinks, offsets that drifted.
        [Test]
        public void FightViewMath_HookedDepthOffset_DiveIsFullyUnderAndDeeperThanEverythingElse()
        {
            Assert.That(FightViewMath.HookedDepthOffset(false, false), Is.EqualTo(-0.10f).Within(1e-6f), "a plain hooked fish: the fish point at h - 0.1");
            Assert.That(FightViewMath.HookedDepthOffset(true, false), Is.EqualTo(-FightViewMath.DiveDepth).Within(1e-6f), "a back pull plunges to the dive depth");
            Assert.That(FightViewMath.HookedDepthOffset(false, true), Is.EqualTo(0.03f).Within(1e-6f), "a spent fish floats at h + 0.03");
            Assert.That(FightViewMath.HookedDepthOffset(true, true), Is.EqualTo(0.03f).Within(1e-6f), "spent wins over a stale dive flag");
            Assert.That(FightViewMath.DiveDepth, Is.GreaterThanOrEqualTo(0.6f), "well past the old 0.35 m and the 0.25 m Bite plunge");
            Assert.That(FightViewMath.DiveDepth, Is.GreaterThan(FightViewMath.StemTipHeight * FightViewMath.MaxDistanceScale + 0.15f),
                "even the largest, far-away bobber is fully under by more than a wave height");
        }

        // Catches: a plunge that is no faster than the normal easing, a resurface that is sped up too, a rate that changes when not diving.
        [Test]
        public void FightViewMath_YEaseRate_OnlyThePlungeIsFast()
        {
            Assert.That(FightViewMath.YEaseRate(true, 0f, -0.5f), Is.EqualTo(FightViewMath.PlungeEaseRate), "diving and going down: the fast plunge");
            Assert.That(FightViewMath.EaseRate, Is.EqualTo(20f), "the bobber's normal easing, 1 - exp(-20 dt), is unchanged");
            Assert.That(FightViewMath.PlungeEaseRate, Is.GreaterThanOrEqualTo(FightViewMath.EaseRate * 2f), "at least twice the normal easing");
            Assert.That(FightViewMath.YEaseRate(true, -0.5f, 0f), Is.EqualTo(FightViewMath.EaseRate), "coming back up uses the normal easing");
            Assert.That(FightViewMath.YEaseRate(false, 0f, -0.5f), Is.EqualTo(FightViewMath.EaseRate), "not diving: normal");
            Assert.That(FightViewMath.YEaseRate(true, 0f, 0f), Is.EqualTo(FightViewMath.EaseRate), "no movement: normal");
        }

        // Catches: a bobber that stays a 7 cm dot at cast distance (F6/F9: unreadable), a scale that shrinks it, one that grows without a limit,
        // a jump in the scale, NaN leaking into the transform.
        [Test]
        public void FightViewMath_DistanceScale_GrowsWithDistanceAndIsCapped()
        {
            Assert.That(FightViewMath.DistanceScale(0f), Is.EqualTo(1f));
            Assert.That(FightViewMath.DistanceScale(FightViewMath.FarScaleStart), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(FightViewMath.DistanceScale(FightViewMath.FarScaleStart * 2f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(FightViewMath.DistanceScale(1000f), Is.EqualTo(FightViewMath.MaxDistanceScale));
            Assert.That(FightViewMath.DistanceScale(float.NaN), Is.EqualTo(1f));
            Assert.That(FightViewMath.DistanceScale(-5f), Is.EqualTo(1f));
            float last = 1f;
            for (float d = 0f; d <= 30f; d += 0.25f)
            {
                float s = FightViewMath.DistanceScale(d);
                Assert.That(s, Is.GreaterThanOrEqualTo(last - 1e-6f), "non-decreasing at " + d);
                Assert.That(s, Is.InRange(1f, FightViewMath.MaxDistanceScale));
                last = s;
            }
        }
    }
}
