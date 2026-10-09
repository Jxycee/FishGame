// Core/Player/PitchQuant.cs: pitch quantisation for NetPitchQ (degrees x 100, clamped to +-85; spec 7.3).
using System;

namespace FishGame.Core
{
    public static class PitchQuant
    {
        public const float LimitDeg = 85f;
        public const short SendThreshold = 50; // 0.5 degrees

        public static short Quantize(float pitchDeg)
        {
            if (float.IsNaN(pitchDeg)) return 0;
            float p = Math.Min(Math.Max(pitchDeg, -LimitDeg), LimitDeg);
            return (short)Math.Round(p * 100.0, MidpointRounding.AwayFromZero);
        }

        public static float Dequantize(short q)
        {
            return q / 100f;
        }

        // The owner writes NetPitchQ only when this is true (and at most 15 Hz; the rate cap lives in PlayerNetState, T11).
        public static bool ShouldSend(short lastSent, short current)
        {
            return Math.Abs(current - lastSent) >= SendThreshold;
        }
    }
}
