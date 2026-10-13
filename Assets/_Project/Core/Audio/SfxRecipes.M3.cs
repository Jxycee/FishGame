using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    // M3 half of SfxRecipes (partial, ruling C-Q11): the M3 rows (Rain_Patter, section F plan T19.1, M3-D21) and their generators.
    // Same rules as the M1/M2 halves: pure, deterministic for (id, sampleRate, seed), noise only from the passed Pcg32 stream, exact sample
    // count, peak-normalized to 0.9 by Generate. Loops get a 20 ms crossfade of the stream's continuation into the head, then a tiny linear
    // seam correction so s[last] == s[0]. All maths in float with per-sample filters only (no accumulated float time step).
    // The mixing values in the table seed SfxCueTableDef (the cue table may retune everything except length and loop).
    public static partial class SfxRecipes
    {
        private const float RainBedHighPass = 0.13f;       // one-pole high-pass coefficient (low-pass state subtracted), about 900 Hz
        private const float RainBedLowPass = 0.35f;        // one-pole low-pass on the high-passed noise: a soft hiss around 1-4 kHz
        private const float RainBedGain = 0.35f;
        private const float RainDropGain = 0.65f;
        private const float RainDropsPerSecond = 60f;
        private const float RainDropDecaySeconds = 0.004f; // each drop's noise burst decays with this time constant
        private const float RainDropAmpMin = 0.3f, RainDropAmpRange = 0.7f;

        // Loop 4 s, Ambience, base 0.45, 2D, default max distance, no rate limit, min distance 1 (the cue validator needs minDistance > 0).
        private static readonly SfxRecipe[] M3Table =
        {
            new SfxRecipe(SfxIds.RainPatter, 4.0f, true, AudioCategory.Ambience, 0.45f, false, 0f, 0f, 1f),
        };

        private static float[] GenerateM3(string id, int n, int sampleRate, ref Pcg32 rng)
        {
            switch (id)
            {
                case SfxIds.RainPatter: return RainPatterGen(n, sampleRate, ref rng);
                default: throw new ArgumentException("No generator for SFX id: " + id, nameof(id));
            }
        }

        // Filtered noise bed plus random drop bursts. Draw order per sample k (fixed; pins determinism, warm-up samples included):
        //   (1) white = rng.NextFloat() * 2 - 1;  (2) roll = rng.NextFloat();  (3) only when roll < 60 / sampleRate: amp = 0.3 + 0.7 * rng.NextFloat().
        // The drop envelope carries into the crossfade region, so the tail continues the same stream.
        private static float[] RainPatterGen(int n, int sampleRate, ref Pcg32 rng)
        {
            int xf = Math.Min(n / 2, Math.Max(1, (int)Math.Round(CrossfadeSeconds * sampleRate)));
            int total = n + xf;
            var s = new float[total];
            float dropChance = RainDropsPerSecond / sampleRate;
            float dropDecay = MathF.Exp(-1f / (RainDropDecaySeconds * sampleRate));
            float lpHp = 0f, lp = 0f, dropEnv = 0f;
            for (int k = -NoiseWarmupSamples; k < total; k++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                float roll = rng.NextFloat();
                if (roll < dropChance)
                {
                    float amp = RainDropAmpMin + RainDropAmpRange * rng.NextFloat();
                    if (amp > dropEnv) dropEnv = amp;
                }
                float hp = white - lpHp;
                lpHp += RainBedHighPass * (white - lpHp);
                lp += RainBedLowPass * (hp - lp);
                float drop = white * dropEnv;
                dropEnv *= dropDecay;
                if (k >= 0) s[k] = RainBedGain * lp + RainDropGain * drop;
            }

            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < xf)
                {
                    float w = i / (float)xf;
                    o[i] = s[i] * w + s[n + i] * (1f - w);
                }
                else
                {
                    o[i] = s[i];
                }
            }

            float d = o[n - 1] - o[0];
            for (int i = 0; i < n; i++)
            {
                o[i] -= d * (i / (float)(n - 1));
            }
            return o;
        }
    }
}
