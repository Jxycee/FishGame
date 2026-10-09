using System;

namespace FishGame.Core
{
    // Spec 11.7: effective volume = cueBase * category^2 * master^2, settings are 0..100.
    public static class AudioMath
    {
        public const float PitchJitter = 0.07f, VolumeJitterDb = 1.5f;

        public static float EffectiveVolume(float cueBase, int categoryPercent, int masterPercent)
        {
            float c = Math.Clamp(categoryPercent, 0, 100) / 100f;
            float m = Math.Clamp(masterPercent, 0, 100) / 100f;
            return cueBase * c * c * m * m;
        }

        public static float DbToLinear(float db)
        {
            return (float)Math.Pow(10.0, db / 20.0);
        }
    }
}
