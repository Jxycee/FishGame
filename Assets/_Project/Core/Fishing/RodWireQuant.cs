// Core/Fishing/RodWireQuant.cs: NetRodState quantization and flags (spec 9.9 RodStateData, C 7.5).
// NaN quantizes to 0; out-of-range values clamp.
using System;

namespace FishGame.Core
{
    public static class RodWireQuant
    {
        public const byte FlagBurst = 1, FlagReel = 2, FlagDive = 4, FlagExhausted = 8, FlagRecall = 16;
        public const byte FlagGrace = 32;                     // Hooked and the snap grace timer is running (tension at 1.0); remotes smooth Tension, so they read this, not Tension >= 0.999
        public const byte AnnounceFlagClean = 1;              // CatchAnnouncedRpc flags

        // round(clamp(-1, 1) * 127), away from zero.
        public static sbyte QuantizeLateral(float lateral)
        {
            if (float.IsNaN(lateral)) return 0;
            double c = lateral < -1f ? -1.0 : (lateral > 1f ? 1.0 : lateral);
            return (sbyte)Math.Round(c * 127.0, MidpointRounding.AwayFromZero);
        }

        // q / 127; -128 would dequantize below -1, so it clamps to -1.
        public static float DequantizeLateral(sbyte q)
        {
            float v = q / 127f;
            return v < -1f ? -1f : v;
        }

        // round(clamp01 * 255), away from zero.
        public static byte QuantizeTension(float tension)
        {
            if (float.IsNaN(tension)) return 0;
            double c = tension < 0f ? 0.0 : (tension > 1f ? 1.0 : tension);
            return (byte)Math.Round(c * 255.0, MidpointRounding.AwayFromZero);
        }

        public static float DequantizeTension(byte q)
        {
            return q / 255f;
        }
    }
}
