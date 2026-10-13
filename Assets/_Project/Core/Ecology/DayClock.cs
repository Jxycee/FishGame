// Core/Ecology/DayClock.cs: the 20-minute day as a fixed minute count (spec 12.2, R1). Pure; authority: none.
// A day runs 06:00 -> 02:00 in 1200 game minutes and never wraps. Valid casting minutes are 0..1199; lookups clamp.
using System.Globalization;

namespace FishGame.Core
{
    public static class DayClock
    {
        public const int DayLengthMinutes = 1200;            // 06:00 -> 02:00, never wraps; valid casting 0..1199
        public const int LastCallMinute = 1140;              // 01:00
        public const int SegmentMinutes = 240, SegmentsPerDay = 5;
        public const int PlannedDays = 7;                    // 35 planned segments
        public const float DefaultMinutesPerSecond = 1200f / 900f;   // 1.333; M4 RunConfigDef.dayRealSec replaces it
        public const int DisplayStepMinutes = 10;
        public static readonly int[] BandStart = { 0, 120, 300, 480, 660, 840 };   // Dawn, Morning, Midday, Afternoon, Dusk, Night

        private static readonly TimeBandMask[] Bands =
        {
            TimeBandMask.Dawn, TimeBandMask.Morning, TimeBandMask.Midday, TimeBandMask.Afternoon, TimeBandMask.Dusk, TimeBandMask.Night,
        };

        // minute clamped to 0..1199; exactly one bit
        public static TimeBandMask BandAt(int minute)
        {
            int m = Clamp(minute);
            int band = 0;
            for (int i = 1; i < BandStart.Length; i++) if (BandStart[i] <= m) band = i;
            return Bands[band];
        }

        // clamp, / 240 -> 0..4
        public static int SegmentAt(int minute)
        {
            return Clamp(minute) / SegmentMinutes;
        }

        // clamp01(minute / 1200); NaN -> 0
        public static float DayFraction(float minute)
        {
            if (float.IsNaN(minute)) return 0f;
            float f = minute / DayLengthMinutes;
            return f < 0f ? 0f : (f > 1f ? 1f : f);
        }

        // "6:00 AM" .. "1:50 AM": floor to 10 minutes, 12-hour clock, ASCII, invariant culture (B-D17: minute clamped to 0..1199)
        public static string FormatTime(int minute)
        {
            int m = Clamp(minute);
            int total = 360 + (m / DisplayStepMinutes) * DisplayStepMinutes;
            int h24 = (total / 60) % 24;
            int h12 = h24 % 12 == 0 ? 12 : h24 % 12;
            return h12.ToString(CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture)
                   + (h24 < 12 ? " AM" : " PM");
        }

        private static int Clamp(int minute)
        {
            return minute < 0 ? 0 : (minute > DayLengthMinutes - 1 ? DayLengthMinutes - 1 : minute);
        }
    }
}
