// Core/Fishing/TensionCues.cs: tension -> feedback cue values (spec 9.11 table, C 7.7). Pure, stateless.
// Authority: none (owner-side presentation input). All camera-motion terms are scaled by cameraMotion01.
namespace FishGame.Core
{
    public static class TensionCues
    {
        public static TensionBand BandOf(float tension, bool inGrace)
        {
            if (inGrace) return TensionBand.Grace;
            if (tension < 0.3f) return TensionBand.Rest;
            if (tension < 0.6f) return TensionBand.Bend;
            if (tension < 0.8f) return TensionBand.Creak;
            return TensionBand.Warn;
        }

        // ReelClickRate = reelSpeed * 6 /s; CreakPitch 1..2.1 over 0.6..1.0; Warn: trauma +0.1/s, rumble ramp.
        public static TensionCueState Evaluate(float tension, bool inGrace, float reelSpeed, float cameraMotion01)
        {
            float t = Clamp01(tension);
            float m = Clamp01(cameraMotion01);
            TensionBand band = BandOf(t, inGrace);
            float creak = Clamp01((t - 0.6f) / 0.4f);
            bool warn = band >= TensionBand.Warn;

            float rumbleLow = 0f, rumbleHigh = 0f;
            if (band == TensionBand.Grace) { rumbleLow = 0.8f; rumbleHigh = 0.6f; }
            else if (band == TensionBand.Warn)
            {
                float r = Clamp01((t - 0.8f) / 0.2f);
                rumbleLow = 0.6f * r; rumbleHigh = 0.4f * r;
            }

            return new TensionCueState
            {
                Band = band,
                RodBend01 = t < 0.3f ? 0f : Clamp01((t - 0.3f) / 0.7f),
                ReelClickRate = reelSpeed * 6f,
                CreakVolume01 = creak,
                CreakPitch = 1f + 1.1f * creak,
                LineWarm01 = Clamp01((t - 0.6f) / 0.2f),
                LineWarn = warn,
                LineDash = warn,
                Shake = warn && m > 0f,
                CrackNow = band == TensionBand.Grace,
                RumbleLow = rumbleLow * m,
                RumbleHigh = rumbleHigh * m,
                TraumaPerSecond = warn ? 0.1f * m : 0f,
            };
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
