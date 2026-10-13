// Tests/EditMode/Ecology/ClockMathTests.cs - T5 (spec 14.2 clock replication): anchor maths, clamp, stop, re-anchor continuity.
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ClockMathTests
    {
        const float Rate = 1200f / 900f;

        [Test]
        public void ClockMath_MinuteAt_ClampsAt1200()
        {
            ClockAnchorData a = ClockMath.At(100.0, 0f, Rate, true);
            Assert.AreEqual(600f, ClockMath.MinuteAt(in a, 550.0), 1e-3f);

            ClockAnchorData b = ClockMath.At(100.0, 1190f, Rate, true);
            Assert.AreEqual(1200f, ClockMath.MinuteAt(in b, 120.0), 1e-3f);
            Assert.AreEqual(1200f, ClockMath.MinuteAt(in b, 1e9), 1e-3f);
        }

        [Test]
        public void ClockMath_MinuteAt_StopsWhenNotRunning()
        {
            ClockAnchorData stopped = ClockMath.At(10.0, 300f, Rate, false);
            Assert.AreEqual(300f, ClockMath.MinuteAt(in stopped, 10.0), 1e-4f);
            Assert.AreEqual(300f, ClockMath.MinuteAt(in stopped, 500.0), 1e-4f);
            Assert.AreEqual(300f, ClockMath.MinuteAt(in stopped, 1e6), 1e-4f);

            ClockAnchorData running = ClockMath.At(50.0, 200f, Rate, true);
            Assert.AreEqual(200f, ClockMath.MinuteAt(in running, 40.0), 1e-4f, "never below the anchor minute");
        }

        [Test]
        public void ClockMath_Reanchor_ContinuousAcrossRateAndRunChanges()
        {
            ClockAnchorData a0 = ClockMath.At(10.0, 0f, Rate, true);

            ClockAnchorData a1 = ClockMath.Reanchor(in a0, 40.0, 80f, true);       // x60 scrub at t 40
            Assert.AreEqual(ClockMath.MinuteAt(in a0, 40.0), ClockMath.MinuteAt(in a1, 40.0), 1e-4f);

            ClockAnchorData a2 = ClockMath.Reanchor(in a1, 41.0, 80f, false);      // stop at t 41
            Assert.AreEqual(ClockMath.MinuteAt(in a1, 41.0), ClockMath.MinuteAt(in a2, 41.0), 1e-4f);
            Assert.AreEqual(ClockMath.MinuteAt(in a2, 41.0), ClockMath.MinuteAt(in a2, 70.0), 1e-4f, "stopped clock holds");

            ClockAnchorData a3 = ClockMath.Reanchor(in a2, 100.0, Rate, true);     // resume at t 100
            float at100 = ClockMath.MinuteAt(in a3, 100.0);
            Assert.AreEqual(ClockMath.MinuteAt(in a2, 100.0), at100, 1e-4f);
            Assert.AreEqual(at100 + Rate, ClockMath.MinuteAt(in a3, 101.0), 1e-3f);
        }

        [Test]
        public void ClockMath_NonFiniteTime_ReturnsAnchorMinute()
        {
            ClockAnchorData a = ClockMath.At(10.0, 300f, Rate, true);
            Assert.AreEqual(300f, ClockMath.MinuteAt(in a, double.NaN), 1e-6f);
            Assert.AreEqual(300f, ClockMath.MinuteAt(in a, double.PositiveInfinity), 1e-6f);
            Assert.AreEqual(300f, ClockMath.MinuteAt(in a, double.NegativeInfinity), 1e-6f);

            var badAnchorTime = new ClockAnchorData { AnchorServerTime = double.NaN, AnchorMinute = 300f, MinutesPerSecond = Rate, Running = true };
            Assert.AreEqual(300f, ClockMath.MinuteAt(in badAnchorTime, 50.0), 1e-6f);

            var badRate = new ClockAnchorData { AnchorServerTime = 10.0, AnchorMinute = 300f, MinutesPerSecond = float.NaN, Running = true };
            Assert.AreEqual(300f, ClockMath.MinuteAt(in badRate, 50.0), 1e-6f);
        }

        [Test]
        public void ClockMath_At_SanitisesMinuteAndRate()
        {
            ClockAnchorData nan = ClockMath.At(5.0, float.NaN, Rate, true);
            Assert.AreEqual(0f, nan.AnchorMinute);
            Assert.AreEqual(1200f, ClockMath.At(5.0, 5000f, Rate, true).AnchorMinute);
            Assert.AreEqual(0f, ClockMath.At(5.0, -3f, Rate, true).AnchorMinute);
            Assert.AreEqual(0f, ClockMath.At(5.0, 10f, -2f, true).MinutesPerSecond);
            Assert.AreEqual(0f, ClockMath.At(5.0, 10f, float.NaN, true).MinutesPerSecond);
        }
    }
}
