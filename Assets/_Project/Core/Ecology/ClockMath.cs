// Core/Ecology/ClockMath.cs: day-clock maths over a ClockAnchorData (spec 14.2, B-D9). Pure; authority: none.
// Computed in double (NetworkTime is a double). Never throws; hostile input collapses to the anchor minute.
namespace FishGame.Core
{
    public static class ClockMath
    {
        // min(1200, AnchorMinute + (Running ? (serverTime - AnchorServerTime) * MinutesPerSecond : 0)); never below AnchorMinute;
        // a non-finite input returns AnchorMinute.
        public static float MinuteAt(in ClockAnchorData a, double serverTime)
        {
            double m0 = ClampMinute(a.AnchorMinute);
            if (!a.Running) return (float)m0;
            float rate = a.MinutesPerSecond;
            if (float.IsNaN(rate) || float.IsInfinity(rate) || !(rate > 0f)) return (float)m0;
            if (!IsFinite(serverTime) || !IsFinite(a.AnchorServerTime)) return (float)m0;
            double elapsed = serverTime - a.AnchorServerTime;
            if (elapsed < 0.0) elapsed = 0.0;
            double m = m0 + elapsed * (double)rate;
            return (float)(m > DayClock.DayLengthMinutes ? DayClock.DayLengthMinutes : m);
        }

        // continuity: MinuteAt(result, serverTime) == MinuteAt(a, serverTime)
        public static ClockAnchorData Reanchor(in ClockAnchorData a, double serverTime, float minutesPerSecond, bool running)
        {
            return At(serverTime, MinuteAt(in a, serverTime), minutesPerSecond, running);
        }

        public static ClockAnchorData At(double serverTime, float minute, float minutesPerSecond, bool running)
        {
            float rate = float.IsNaN(minutesPerSecond) || float.IsInfinity(minutesPerSecond) || minutesPerSecond < 0f ? 0f : minutesPerSecond;
            return new ClockAnchorData
            {
                AnchorServerTime = serverTime,
                AnchorMinute = (float)ClampMinute(minute),
                MinutesPerSecond = rate,
                Running = running,
            };
        }

        private static double ClampMinute(float minute)
        {
            if (float.IsNaN(minute)) return 0.0;
            return minute < 0f ? 0.0 : (minute > DayClock.DayLengthMinutes ? DayClock.DayLengthMinutes : (double)minute);
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
