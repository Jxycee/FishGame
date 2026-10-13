using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    // Pure procedural audio (spec 11.7). No UnityEngine. Deterministic for a given (id, sampleRate, seed).
    // Loops are a whole number of base periods: the wind gain LFO completes exactly one cycle per loop.
    // Noise loops get a 20 ms crossfade, then a tiny linear seam correction so s[last] == s[0].
    // Every recipe is peak-normalized to 0.9.
    // Partial (ruling C-Q11): this file holds M1, All, TryGet, Generate dispatch and the M1 generators; SfxRecipes.M2.cs holds the M2 table
    // and generators; SfxRecipes.M3.cs the M3 table (Rain_Patter) and its generator. Static initializers of a partial class run in an unspecified file order, so All is built lazily, never from a field
    // initializer that reads the other file.
    public static partial class SfxRecipes
    {
        public const int SampleRate = 44100;

        private const float TwoPi = 2f * MathF.PI;
        private const float PeakTarget = 0.9f;
        private const float CrossfadeSeconds = 0.02f;
        private const float ToneAttackSeconds = 0.003f;
        private const float ToneReleaseSeconds = 0.005f;
        private const float BellAttackSeconds = 0.002f;
        private const float BellReleaseSeconds = 0.02f;
        private const int NoiseWarmupSamples = 4096;
        private const float WindLowPass = 0.12f;   // one-pole coefficient, about 850 Hz at 44.1 kHz
        private const float WindLfoDepth = 0.225f; // gain swings between 0.55 and 1.0
        private const float LapLowPass = 0.06f;    // two cascaded one-pole stages
        private const float BellFundamentalHz = 660f;

        private static readonly float[] BellRatio = { 1f, 2.01f, 2.76f, 5.4f };
        private static readonly float[] BellAmp = { 1f, 0.5f, 0.3f, 0.15f };
        private static readonly float[] BellDecay = { 2.5f, 4f, 6f, 9f };

        private static readonly SfxRecipe[] M1Table =
        {
            new SfxRecipe(SfxIds.UiClick, 0.08f, false, AudioCategory.Ui, 0.6f, false, 0f, 0.03f),
            new SfxRecipe(SfxIds.UiConfirm, 0.08f, false, AudioCategory.Ui, 0.6f, false, 0f, 0.03f),
            new SfxRecipe(SfxIds.UiError, 0.08f, false, AudioCategory.Ui, 0.6f, false, 0f, 0.03f),
            new SfxRecipe(SfxIds.WindAmbient, 6.0f, true, AudioCategory.Ambience, 0.5f, false, 0f, 0f),
            new SfxRecipe(SfxIds.WaterLap, 0.4f, false, AudioCategory.Ambience, 0.5f, false, 0f, 0.3f),
            new SfxRecipe(SfxIds.BellRing, 1.0f, false, AudioCategory.Sfx, 0.9f, true, 30f, 0.25f),
        };

        private static readonly IReadOnlyList<SfxRecipe> M1ReadOnly = Array.AsReadOnly(M1Table);

        public static IReadOnlyList<SfxRecipe> M1 => M1ReadOnly;

        private static SfxRecipe[] _allTable;
        private static IReadOnlyList<SfxRecipe> _allReadOnly;

        // M1 rows, then M2 (13), then M3 (1): 20 rows, one cached read-only list. TryGet and Generate cover every row.
        public static IReadOnlyList<SfxRecipe> All
        {
            get
            {
                if (_allReadOnly == null) BuildAll();
                return _allReadOnly;
            }
        }

        private static void BuildAll()
        {
            var table = new SfxRecipe[M1Table.Length + M2Table.Length + M3Table.Length];
            Array.Copy(M1Table, 0, table, 0, M1Table.Length);
            Array.Copy(M2Table, 0, table, M1Table.Length, M2Table.Length);
            Array.Copy(M3Table, 0, table, M1Table.Length + M2Table.Length, M3Table.Length);
            _allTable = table;
            _allReadOnly = Array.AsReadOnly(table);
        }

        public static bool TryGet(string id, out SfxRecipe recipe)
        {
            if (_allTable == null) BuildAll();
            SfxRecipe[] table = _allTable;
            for (int i = 0; i < table.Length; i++)
            {
                if (string.Equals(table[i].Id, id, StringComparison.Ordinal))
                {
                    recipe = table[i];
                    return true;
                }
            }
            recipe = default;
            return false;
        }

        public static int SampleCount(in SfxRecipe recipe, int sampleRate)
        {
            return (int)Math.Round(recipe.LengthSeconds * sampleRate);
        }

        public static float[] Generate(string id, int sampleRate, ulong seed)
        {
            if (!TryGet(id, out SfxRecipe recipe))
                throw new ArgumentException("Unknown SFX id: " + (id ?? "<null>"), nameof(id));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");

            int n = SampleCount(recipe, sampleRate);
            if (n < 2)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate too low for recipe " + id + ".");

            Pcg32 rng = Rng.Stream(seed, "sfx." + id, 0UL);
            float[] samples;
            switch (id)
            {
                case SfxIds.UiClick: samples = Tone(n, sampleRate, 1400f, 2100f, 60f); break;
                case SfxIds.UiConfirm: samples = Tone(n, sampleRate, 880f, 1320f, 30f); break;
                case SfxIds.UiError: samples = Tone(n, sampleRate, 220f, 233f, 20f); break;
                case SfxIds.WindAmbient: samples = WindLoop(n, sampleRate, ref rng); break;
                case SfxIds.WaterLap: samples = LapBurst(n, ref rng); break;
                case SfxIds.BellRing: samples = Bell(n, sampleRate); break;
                case SfxIds.RainPatter: samples = GenerateM3(id, n, sampleRate, ref rng); break;
                default: samples = GenerateM2(id, n, sampleRate, ref rng); break;
            }

            NormalizePeak(samples, PeakTarget);
            return samples;
        }

        // Sine pair with a 3 ms attack, exponential decay and a 5 ms release to zero (no click at the end).
        private static float[] Tone(int n, int sampleRate, float f1, float f2, float decayPerSecond)
        {
            var o = new float[n];
            float releaseSamples = MathF.Max(1f, ToneReleaseSeconds * sampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float attack = MathF.Min(1f, t / ToneAttackSeconds);
                float release = MathF.Min(1f, (n - 1 - i) / releaseSamples);
                float env = attack * release * MathF.Exp(-decayPerSecond * t);
                o[i] = (MathF.Sin(TwoPi * f1 * t) + 0.6f * MathF.Sin(TwoPi * f2 * t)) * env;
            }
            return o;
        }

        // Pink noise (Paul Kellet's economy filter), one-pole low-passed, with a gain LFO of exactly one cycle per loop.
        // The tail (n .. n+xf) is crossfaded into the head, so o[n-1] -> o[0] continues the same noise stream.
        private static float[] WindLoop(int n, int sampleRate, ref Pcg32 rng)
        {
            int xf = Math.Min(n / 2, Math.Max(1, (int)Math.Round(CrossfadeSeconds * sampleRate)));
            int total = n + xf;
            var noise = new float[total];
            float b0 = 0f, b1 = 0f, b2 = 0f, lp = 0f;
            for (int k = -NoiseWarmupSamples; k < total; k++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                b0 = 0.99765f * b0 + white * 0.0990460f;
                b1 = 0.96300f * b1 + white * 0.2965164f;
                b2 = 0.57000f * b2 + white * 1.0526913f;
                float pink = b0 + b1 + b2 + white * 0.1848f;
                lp += WindLowPass * (pink - lp);
                if (k >= 0) noise[k] = lp;
            }

            for (int k = 0; k < total; k++)
            {
                float lfo = 1f - WindLfoDepth + WindLfoDepth * MathF.Sin(TwoPi * k / n);
                noise[k] *= lfo;
            }

            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < xf)
                {
                    float w = i / (float)xf;
                    o[i] = noise[i] * w + noise[n + i] * (1f - w);
                }
                else
                {
                    o[i] = noise[i];
                }
            }

            float d = o[n - 1] - o[0];
            for (int i = 0; i < n; i++)
            {
                o[i] -= d * (i / (float)(n - 1));
            }
            return o;
        }

        // Two-pole low-passed white noise with a sin^2 envelope (zero at both ends).
        private static float[] LapBurst(int n, ref Pcg32 rng)
        {
            var o = new float[n];
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                lp1 += LapLowPass * (white - lp1);
                lp2 += LapLowPass * (lp1 - lp2);
                float env = MathF.Sin(MathF.PI * i / (n - 1));
                o[i] = lp2 * env * env;
            }
            return o;
        }

        // Struck-bell sine partials with per-partial exponential decay, 2 ms attack, 20 ms release to zero.
        private static float[] Bell(int n, int sampleRate)
        {
            var o = new float[n];
            float releaseSamples = MathF.Max(1f, BellReleaseSeconds * sampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float attack = MathF.Min(1f, t / BellAttackSeconds);
                float release = MathF.Min(1f, (n - 1 - i) / releaseSamples);
                float sum = 0f;
                for (int k = 0; k < BellRatio.Length; k++)
                {
                    sum += BellAmp[k] * MathF.Sin(TwoPi * BellFundamentalHz * BellRatio[k] * t) * MathF.Exp(-BellDecay[k] * t);
                }
                o[i] = sum * attack * release;
            }
            return o;
        }

        private static void NormalizePeak(float[] s, float target)
        {
            float peak = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float a = MathF.Abs(s[i]);
                if (a > peak) peak = a;
            }
            if (peak <= 1e-9f) return;
            float g = target / peak;
            for (int i = 0; i < s.Length; i++)
            {
                s[i] *= g;
            }
        }
    }
}
