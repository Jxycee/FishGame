// Tests/EditMode/Ecology/DayClockTests.cs - T5 (spec 12.2, R1): band table, segments, 12-hour text, day fraction.
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DayClockTests
    {
        [TestCase(0, TimeBandMask.Dawn)]
        [TestCase(119, TimeBandMask.Dawn)]
        [TestCase(120, TimeBandMask.Morning)]
        [TestCase(299, TimeBandMask.Morning)]
        [TestCase(300, TimeBandMask.Midday)]
        [TestCase(479, TimeBandMask.Midday)]
        [TestCase(480, TimeBandMask.Afternoon)]
        [TestCase(659, TimeBandMask.Afternoon)]
        [TestCase(660, TimeBandMask.Dusk)]
        [TestCase(839, TimeBandMask.Dusk)]
        [TestCase(840, TimeBandMask.Night)]
        [TestCase(1199, TimeBandMask.Night)]
        [TestCase(-5, TimeBandMask.Dawn)]
        [TestCase(1200, TimeBandMask.Night)]
        [TestCase(int.MaxValue, TimeBandMask.Night)]
        [TestCase(int.MinValue, TimeBandMask.Dawn)]
        public void DayClock_BandAt_BoundariesMatchSpec(int minute, TimeBandMask expected)
        {
            TimeBandMask band = DayClock.BandAt(minute);
            Assert.AreEqual(expected, band, "minute " + minute);
            int bits = 0;
            for (int b = (int)band; b != 0; b >>= 1) bits += b & 1;
            Assert.AreEqual(1, bits, "exactly one band bit for minute " + minute);
        }

        [TestCase(0, 0)]
        [TestCase(239, 0)]
        [TestCase(240, 1)]
        [TestCase(959, 3)]
        [TestCase(960, 4)]
        [TestCase(1199, 4)]
        [TestCase(1200, 4)]
        [TestCase(-1, 0)]
        public void DayClock_SegmentAt_FiveBlocksOf240(int minute, int expected)
        {
            Assert.AreEqual(expected, DayClock.SegmentAt(minute), "minute " + minute);
        }

        [TestCase(0, "6:00 AM")]
        [TestCase(9, "6:00 AM")]
        [TestCase(10, "6:10 AM")]
        [TestCase(359, "11:50 AM")]
        [TestCase(360, "12:00 PM")]
        [TestCase(375, "12:10 PM")]
        [TestCase(1080, "12:00 AM")]
        [TestCase(1199, "1:50 AM")]
        [TestCase(1200, "1:50 AM")]
        [TestCase(-30, "6:00 AM")]
        public void DayClock_FormatTime_TenMinuteStepsTwelveHour(int minute, string expected)
        {
            Assert.AreEqual(expected, DayClock.FormatTime(minute), "minute " + minute);
        }

        [Test]
        public void DayClock_DayFraction_ClampedAndNaNSafe()
        {
            Assert.AreEqual(0f, DayClock.DayFraction(0f), 1e-6f);
            Assert.AreEqual(0.5f, DayClock.DayFraction(600f), 1e-6f);
            Assert.AreEqual(1f, DayClock.DayFraction(1200f), 1e-6f);
            Assert.AreEqual(1f, DayClock.DayFraction(1500f), 1e-6f);
            Assert.AreEqual(0f, DayClock.DayFraction(-10f), 1e-6f);
            Assert.AreEqual(0f, DayClock.DayFraction(float.NaN), 1e-6f);
            Assert.AreEqual(1f, DayClock.DayFraction(float.PositiveInfinity), 1e-6f);
        }

        [Test]
        public void DayClock_Constants_MatchContract()
        {
            Assert.AreEqual(1200, DayClock.DayLengthMinutes);
            Assert.AreEqual(1140, DayClock.LastCallMinute);
            Assert.AreEqual(240, DayClock.SegmentMinutes);
            Assert.AreEqual(5, DayClock.SegmentsPerDay);
            Assert.AreEqual(7, DayClock.PlannedDays);
            Assert.AreEqual(10, DayClock.DisplayStepMinutes);
            Assert.AreEqual(new[] { 0, 120, 300, 480, 660, 840 }, DayClock.BandStart);
        }
    }
}
