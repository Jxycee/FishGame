// Tests/EditMode/Ecology/DayNightMathTests.cs - T8: the spec 10.3 day-night maths. T18 later appends the real-palette readability case
// and SeasonTint_TexturedEnvMaterial_TakesHueFromPalette to this file; T8's tests stay as they are.
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DayNightMathTests
    {
        static readonly float[] Starts = { 0f, 0.10f, 0.55f, 0.70f };
        static readonly float[] P = { 0f, 10f, 20f, 30f };
        const float Window = 0.05f;

        static float V(float f, float[] starts, float[] values, float window)
        {
            PhaseBlend b = DayNightMath.Blend(f, starts, window);
            return values[b.From] + (values[b.To] - values[b.From]) * b.T;
        }

        [Test]
        public void DayNightMath_Blend_ContinuousAcrossBoundaries()
        {
            const float step = 0.0005f;
            double bound = 1.5 * 10 * step / Window + 1e-4;                           // max smoothstep slope 1.5 over the window
            for (int i = 0; i * step < 1f; i++)
            {
                float f = i * step;
                float a = V(f, Starts, P, Window), b = V(f + step, Starts, P, Window);
                Assert.LessOrEqual(Math.Abs(b - a), bound, "pop at f = " + f);
            }

            // A short phase: starts {0, 0.10, 0.12} cut the second window to 0.02 and must finish its blend before the next start.
            float[] cutStarts = { 0f, 0.10f, 0.12f };
            float[] cutValues = { 0f, 10f, 20f };
            double cutBound = 1.5 * 10 * step / 0.02 + 1e-4;
            for (int i = 0; i * step < 1f; i++)
            {
                float f = i * step;
                float a = V(f, cutStarts, cutValues, Window), b = V(f + step, cutStarts, cutValues, Window);
                Assert.LessOrEqual(Math.Abs(b - a), cutBound, "pop in the cut window at f = " + f);
            }

            PhaseBlend quarter = DayNightMath.Blend(0.10f + 0.0125f, Starts, Window);
            Assert.AreEqual(0, quarter.From);
            Assert.AreEqual(1, quarter.To);
            Assert.AreEqual(0.15625f, quarter.T, 1e-4f, "smoothstep(0.25), not linear");
        }

        [Test]
        public void DayNightMath_Blend_HoldsAwayFromBoundaries()
        {
            void Hold(float f, int phase)
            {
                PhaseBlend b = DayNightMath.Blend(f, Starts, Window);
                Assert.AreEqual(phase, b.From, "From at " + f);
                Assert.AreEqual(phase, b.To, "To at " + f);
                Assert.AreEqual(0f, b.T, 0f);
            }
            Hold(0.05f, 0);
            Hold(0.3f, 1);
            Hold(0.62f, 2);
            Hold(0.9f, 3);
            Hold(1.5f, 3);
            Hold(float.NaN, 0);
            Hold(-0.2f, 0);

            PhaseBlend mid = DayNightMath.Blend(0.125f, Starts, Window);
            Assert.AreEqual(0, mid.From);
            Assert.AreEqual(1, mid.To);
            Assert.AreEqual(0.5f, mid.T, 1e-5f);

            Assert.Throws<ArgumentException>(() => DayNightMath.Blend(0.3f, new[] { 0f, 0.5f, 0.5f }, Window));
            Assert.Throws<ArgumentException>(() => DayNightMath.Blend(0.3f, new[] { 0f, 0.6f, 0.5f }, Window));
            Assert.Throws<ArgumentException>(() => DayNightMath.Blend(0.3f, new float[0], Window));
            Assert.Throws<ArgumentException>(() => DayNightMath.Blend(0.3f, null, Window));
        }

        [Test]
        public void DayNightMath_SunElevation_AtLeastMinWhileUp()
        {
            const float rise = 0f, set = 0.70f, minDeg = 12f;
            double minY = Math.Sin(minDeg * Math.PI / 180.0) - 1e-5;
            float firstX = 0f, lastX = 0f;
            for (int i = 0; i <= 700; i++)
            {
                float f = i * 0.001f;
                DayNightMath.SunDirection(f, rise, set, minDeg, out float x, out float y, out float z);
                Assert.AreEqual(1.0, Math.Sqrt(x * x + y * y + z * z), 1e-5, "unit length at " + f);
                Assert.GreaterOrEqual(y, minY, "above the minimum elevation at " + f);
                Assert.LessOrEqual(z, 1e-5f, "south half at " + f);
                if (i == 0) firstX = x;
                lastX = x;
            }
            Assert.Greater(firstX, 0f, "east at sunrise");
            Assert.Less(lastX, 0f, "west at sunset");

            DayNightMath.SunDirection(0.35f, rise, set, minDeg, out _, out float peak, out _);
            Assert.AreEqual(Math.Sin(60.0 * Math.PI / 180.0), peak, 1e-4);

            DayNightMath.SunDirection(0.9f, rise, set, minDeg, out float ax, out float ay, out float az);
            DayNightMath.SunDirection(0.70f, rise, set, minDeg, out float bx, out float by, out float bz);
            Assert.AreEqual(bx, ax, 1e-6f);
            Assert.AreEqual(by, ay, 1e-6f);
            Assert.AreEqual(bz, az, 1e-6f);
        }

        [Test]
        public void DayNightMath_SunMoonFlip_BothWeightsZeroAtFlip()
        {
            const float rise = 0f, set = 0.70f, flip = 0.02f;
            Assert.AreEqual(0f, DayNightMath.SunWeight(0.70f, rise, set, flip));
            Assert.AreEqual(0f, DayNightMath.MoonWeight(0.70f, rise, set, flip));
            Assert.AreEqual(0f, DayNightMath.SunWeight(0f, rise, set, flip));
            Assert.AreEqual(0f, DayNightMath.MoonWeight(0f, rise, set, flip));
            Assert.AreEqual(1f, DayNightMath.SunWeight(0.68f, rise, set, flip), 1e-5f);
            Assert.AreEqual(0f, DayNightMath.MoonWeight(0.68f, rise, set, flip));
            Assert.AreEqual(1f, DayNightMath.MoonWeight(0.72f, rise, set, flip), 1e-5f);
            Assert.AreEqual(0f, DayNightMath.SunWeight(0.72f, rise, set, flip));
            Assert.AreEqual(0.5f, DayNightMath.SunWeight(0.69f, rise, set, flip), 1e-5f);
            Assert.AreEqual(1f, DayNightMath.SunWeight(0.02f, rise, set, flip), 1e-5f);

            float prevSun = DayNightMath.SunWeight(0f, rise, set, flip), prevMoon = DayNightMath.MoonWeight(0f, rise, set, flip);
            for (int i = 1; i <= 2000; i++)
            {
                float f = i * 0.0005f;
                float sun = DayNightMath.SunWeight(f, rise, set, flip), moon = DayNightMath.MoonWeight(f, rise, set, flip);
                Assert.LessOrEqual(Math.Abs(sun - prevSun), 0.04f, "sun pop at " + f);
                Assert.LessOrEqual(Math.Abs(moon - prevMoon), 0.04f, "moon pop at " + f);
                Assert.AreEqual(0f, sun * moon, 0f, "never both lights on, f = " + f);
                prevSun = sun; prevMoon = moon;
            }
        }

        [Test]
        public void DayNightMath_FogFactor_ExponentialSquared()
        {
            Assert.AreEqual(Math.Exp(-0.25), DayNightMath.FogFactor(0.02f, 25f), 1e-6);
            Assert.AreEqual(1f, DayNightMath.FogFactor(0f, 100f), 0f);
            float prev = 1f;
            for (float d = 1f; d <= 100f; d += 1f)
            {
                float v = DayNightMath.FogFactor(0.02f, d);
                Assert.Less(v, prev, "monotonic decreasing at " + d);
                prev = v;
            }
        }

        [Test]
        public void DayNightMath_IsReadable_DensityBoundAtPoint0333()
        {
            Assert.IsTrue(DayNightMath.IsReadable(0.0333f));
            Assert.IsFalse(DayNightMath.IsReadable(0.0334f));
            Assert.IsTrue(DayNightMath.IsReadable(0.008f));
            Assert.IsTrue(DayNightMath.IsReadable(0.014f));
            Assert.IsTrue(DayNightMath.IsReadable(0.03f));
            Assert.IsFalse(DayNightMath.IsReadable(float.NaN));
            Assert.AreEqual(25f, DayNightMath.ReadabilityDistanceM);
            Assert.AreEqual(0.5f, DayNightMath.ReadabilityMinFactor);
        }
    }
}
