using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    // M2 half of SfxRecipes (partial, ruling C-Q11): the 12 spec 11.7 M2 rows plus Rod_Thunk (C M2-D10) and Line_Crack (spec 9.11 grace row,
    // T23 review; appended last so the pinned rows keep their index), and their generators.
    // Same rules as the M1 half: pure, deterministic for (id, sampleRate, seed), noise only from the passed Pcg32 stream, exact sample count,
    // peak-normalized to 0.9 by Generate. One-shots start from silence (attack) and end on an exact 0 (release); loops are whole numbers
    // of base periods, so they have no seam jump. Phase is computed per sample in double (2*pi*f*i/sampleRate) or integrated in a double
    // accumulator for sweeps; a float time step is never accumulated.
    // The mixing values in the table are the seeds for SfxCueTableDef (T14); the cue table may retune everything except length and loop.
    public static partial class SfxRecipes
    {
        private const double TwoPiD = 2.0 * Math.PI;

        private static readonly SfxRecipe[] M2Table =
        {
            new SfxRecipe(SfxIds.CastWhoosh, 0.35f, false, AudioCategory.Sfx, 0.70f, true, 30f, 0.10f, 1f),
            new SfxRecipe(SfxIds.SplashSmall, 0.25f, false, AudioCategory.Sfx, 0.70f, true, 30f, 0.08f, 1f),
            new SfxRecipe(SfxIds.SplashBig, 0.60f, false, AudioCategory.Sfx, 1.00f, true, 45f, 0.20f, 8f),
            new SfxRecipe(SfxIds.BitePlop, 0.20f, false, AudioCategory.Sfx, 0.70f, true, 30f, 0.10f, 1f),
            new SfxRecipe(SfxIds.NibbleTick, 0.03f, false, AudioCategory.Sfx, 0.50f, true, 20f, 0.30f, 1f),
            new SfxRecipe(SfxIds.ReelClick, 0.02f, false, AudioCategory.Sfx, 0.45f, true, 20f, 0.05f, 1f),
            new SfxRecipe(SfxIds.LineCreak, 0.50f, true, AudioCategory.Sfx, 0.60f, true, 25f, 0f, 1f),
            new SfxRecipe(SfxIds.DiveThrum, 1.00f, true, AudioCategory.Sfx, 0.70f, true, 30f, 0f, 1f),
            new SfxRecipe(SfxIds.Snap, 0.40f, false, AudioCategory.Sfx, 1.00f, true, 45f, 0.20f, 4f),
            new SfxRecipe(SfxIds.CatchFlop, 0.50f, false, AudioCategory.Sfx, 0.80f, true, 30f, 0.20f, 1f),
            new SfxRecipe(SfxIds.CatchCheer, 0.80f, false, AudioCategory.Sfx, 0.80f, true, 60f, 0.30f, 6f),
            new SfxRecipe(SfxIds.RodThunk, 0.15f, false, AudioCategory.Sfx, 0.60f, true, 20f, 0.10f, 1f),
            new SfxRecipe(SfxIds.LineCrack, 0.18f, false, AudioCategory.Sfx, 0.80f, true, 35f, 0.15f, 2f),
        };

        private static readonly IReadOnlyList<SfxRecipe> M2ReadOnly = Array.AsReadOnly(M2Table);

        public static IReadOnlyList<SfxRecipe> M2 => M2ReadOnly;

        private static float[] GenerateM2(string id, int n, int sampleRate, ref Pcg32 rng)
        {
            switch (id)
            {
                case SfxIds.CastWhoosh: return CastWhooshGen(n, sampleRate, ref rng);
                case SfxIds.SplashSmall: return SplashSmallGen(n, sampleRate, ref rng);
                case SfxIds.SplashBig: return SplashBigGen(n, sampleRate, ref rng);
                case SfxIds.BitePlop: return BitePlopGen(n, sampleRate, ref rng);
                case SfxIds.NibbleTick: return NibbleTickGen(n, sampleRate);
                case SfxIds.ReelClick: return ReelClickGen(n, sampleRate, ref rng);
                case SfxIds.LineCreak: return LineCreakGen(n, sampleRate);
                case SfxIds.DiveThrum: return DiveThrumGen(n, sampleRate);
                case SfxIds.Snap: return SnapGen(n, sampleRate, ref rng);
                case SfxIds.CatchFlop: return CatchFlopGen(n, sampleRate, ref rng);
                case SfxIds.CatchCheer: return CatchCheerGen(n, sampleRate);
                case SfxIds.RodThunk: return RodThunkGen(n, sampleRate, ref rng);
                case SfxIds.LineCrack: return LineCrackGen(n, sampleRate, ref rng);
                default: throw new ArgumentException("No generator for SFX id: " + id, nameof(id));
            }
        }

        private static float White(ref Pcg32 rng)
        {
            return rng.NextFloat() * 2f - 1f;
        }

        // Linear attack from 0 and a linear release to exactly 0 on the last sample. attackSeconds 0 = no attack ramp.
        private static float AttackRelease(int i, int n, int sampleRate, float attackSeconds, float releaseSeconds)
        {
            float t = i / (float)sampleRate;
            float attack = attackSeconds > 0f ? MathF.Min(1f, t / attackSeconds) : 1f;
            float releaseSamples = MathF.Max(1f, releaseSeconds * sampleRate);
            float release = MathF.Min(1f, (n - 1 - i) / releaseSamples);
            return attack * release;
        }

        // White noise through a 2-pole state-variable band-pass (Q about 2), centre sweeping 600 -> 200 Hz exponentially, sin^2-style hump
        // that peaks at 35 percent of the length (u^0.66 maps u = 0.35 to 0.5) and is zero at both ends.
        private static float[] CastWhooshGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            float low = 0f, band = 0f;
            const float damping = 0.5f; // 1 / Q
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float fc = 600f * MathF.Pow(200f / 600f, u);
                float f = 2f * MathF.Sin(MathF.PI * fc / sampleRate);
                float w = White(ref rng);
                low += f * band;
                float high = w - low - damping * band;
                band += f * high;
                float hump = MathF.Sin(MathF.PI * MathF.Pow(u, 0.66f));
                o[i] = band * hump * hump;
            }
            return o;
        }

        private static float[] SplashSmallGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                lp += 0.35f * (White(ref rng) - lp);
                o[i] = lp * MathF.Exp(-t / 0.08f) * AttackRelease(i, n, sampleRate, 0.002f, 0.010f);
            }
            return o;
        }

        // Low noise (two cascaded one-poles, about 0.05) plus a 90 -> 45 Hz sine sweep at about 0.6 relative level.
        private static float[] SplashBigGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            float lp1 = 0f, lp2 = 0f;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float u = i / (float)(n - 1);
                lp1 += 0.05f * (White(ref rng) - lp1);
                lp2 += 0.05f * (lp1 - lp2);
                double freq = 90.0 + (45.0 - 90.0) * u;
                phase += TwoPiD * freq / sampleRate;
                float body = (float)Math.Sin(phase);
                float env = MathF.Exp(-t / 0.25f) * AttackRelease(i, n, sampleRate, 0.003f, 0.020f);
                o[i] = (lp2 * 6f + 0.6f * body) * env;
            }
            return o;
        }

        private static float[] BitePlopGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float u = i / (float)(n - 1);
                phase += TwoPiD * (320.0 + (240.0 - 320.0) * u) / sampleRate;
                float tone = (float)Math.Sin(phase) * MathF.Exp(-t / 0.05f);
                float click = 0.3f * White(ref rng) * MathF.Exp(-t / 0.01f);
                o[i] = (tone + click) * AttackRelease(i, n, sampleRate, 0.001f, 0.010f);
            }
            return o;
        }

        private static float[] NibbleTickGen(int n, int sampleRate)
        {
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float tone = (float)Math.Sin(TwoPiD * 1800.0 * i / sampleRate);
                o[i] = tone * MathF.Exp(-t / 0.006f) * AttackRelease(i, n, sampleRate, 0.001f, 0.003f);
            }
            return o;
        }

        private static float[] ReelClickGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float tone = (float)Math.Sin(TwoPiD * 2600.0 * i / sampleRate) * MathF.Exp(-t / 0.003f);
                float tick = 0.4f * White(ref rng) * MathF.Exp(-t / 0.002f);
                o[i] = (tone + tick) * AttackRelease(i, n, sampleRate, 0.0005f, 0.003f);
            }
            return o;
        }

        // Band-limited saw at 200 Hz (0.5 s = exactly 100 periods) built from 20 harmonics with a low-pass weight, starting mid-ramp at
        // phase pi, with a 6 Hz wobble (3 whole cycles per loop). No IIR filter, so the loop is exactly periodic.
        private static float[] LineCreakGen(int n, int sampleRate)
        {
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                double phase = TwoPiD * 200.0 * i / sampleRate + Math.PI;
                double sum = 0.0;
                for (int k = 1; k <= 20; k++)
                {
                    double x = k * 200.0 / 1200.0;
                    sum += Math.Sin(k * phase) / k / (1.0 + x * x);
                }
                double wobble = 0.85 + 0.15 * Math.Sin(TwoPiD * 6.0 * i / sampleRate);
                o[i] = (float)(sum * wobble);
            }
            return o;
        }

        // 55 Hz + 0.35 x 110 Hz (55 whole periods per 1 s loop) with a 2 Hz AM (2 whole cycles per loop). No noise, no filter.
        private static float[] DiveThrumGen(int n, int sampleRate)
        {
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                double body = Math.Sin(TwoPiD * 55.0 * i / sampleRate) + 0.35 * Math.Sin(TwoPiD * 110.0 * i / sampleRate);
                double am = 0.6 + 0.4 * Math.Sin(TwoPiD * 2.0 * i / sampleRate);
                o[i] = (float)(body * am);
            }
            return o;
        }

        private static float[] SnapGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float w = White(ref rng);
                lp += 0.2f * (w - lp);
                float crack = (w - lp) * MathF.Exp(-t / 0.03f);
                float ping = 0.7f * (float)Math.Sin(TwoPiD * 2400.0 * i / sampleRate) * MathF.Exp(-t / 0.12f);
                o[i] = (crack + ping) * AttackRelease(i, n, sampleRate, 0.001f, 0.020f);
            }
            return o;
        }

        // Three thumps (0, 0.15, 0.30 s): a 120 -> 80 Hz sine glide (over 0.1 s) with a 40 ms decay and a small noise tick each.
        private static float[] CatchFlopGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            float[] starts = { 0f, 0.15f, 0.30f };
            const double glide = 0.1;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float w = White(ref rng);
                float sum = 0f;
                for (int j = 0; j < starts.Length; j++)
                {
                    float tau = t - starts[j];
                    if (tau < 0f) continue;
                    double cycles = tau <= glide ? 120.0 * tau - 20.0 * tau * tau / glide : 120.0 * glide - 20.0 * glide + 80.0 * (tau - glide);
                    float thump = (float)Math.Sin(TwoPiD * cycles) * MathF.Exp(-tau / 0.04f);
                    float tick = 0.25f * w * MathF.Exp(-tau / 0.004f);
                    sum += (thump + tick) * MathF.Min(1f, tau / 0.002f);
                }
                o[i] = sum * AttackRelease(i, n, sampleRate, 0f, 0.020f);
            }
            return o;
        }

        // Rising C5 / E5 / G5 chord entering at 0, 0.1 and 0.2 s; each has a 10 ms attack, a x0.97 -> x1.0 glide over 80 ms and a gentle decay.
        private static float[] CatchCheerGen(int n, int sampleRate)
        {
            var o = new float[n];
            double[] freqs = { 523.25, 659.25, 783.99 };
            float[] starts = { 0f, 0.1f, 0.2f };
            const double glide = 0.08;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float sum = 0f;
                for (int j = 0; j < freqs.Length; j++)
                {
                    float tau = t - starts[j];
                    if (tau < 0f) continue;
                    double cycles = freqs[j] * (tau <= glide
                        ? 0.97 * tau + 0.03 * tau * tau / (2.0 * glide)
                        : 0.97 * glide + 0.03 * glide / 2.0 + (tau - glide));
                    sum += (float)Math.Sin(TwoPiD * cycles) * MathF.Min(1f, tau / 0.010f) * MathF.Exp(-tau / 0.9f);
                }
                o[i] = sum * AttackRelease(i, n, sampleRate, 0f, 0.100f);
            }
            return o;
        }

        private static float[] RodThunkGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            double phase = 0.0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float u = i / (float)(n - 1);
                phase += TwoPiD * (140.0 + (70.0 - 140.0) * u) / sampleRate;
                lp += 0.08f * (White(ref rng) - lp);
                float body = (float)Math.Sin(phase) * MathF.Exp(-t / 0.03f);
                float thud = 0.5f * 3f * lp * MathF.Exp(-t / 0.02f);
                o[i] = (body + thud) * AttackRelease(i, n, sampleRate, 0.001f, 0.010f);
            }
            return o;
        }

        // Line_Crack (spec 9.11 grace row): a taut line about to go. Not the Snap (no ping, no long tail): a 4 ms noise "tick" through a high-pass
        // (the fibres letting go), then a stiff creak, a 3-harmonic tone gliding 900 -> 520 Hz with a rough 85 Hz stick-slip flutter, which dies
        // in about 45 ms. 0.18 s, 0.5 ms attack, 15 ms release to exactly 0.
        private static float[] LineCrackGen(int n, int sampleRate, ref Pcg32 rng)
        {
            var o = new float[n];
            float lp = 0f;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float u = i / (float)(n - 1);
                float w = White(ref rng);
                lp += 0.35f * (w - lp);
                float tick = (w - lp) * MathF.Exp(-t / 0.004f);
                phase += TwoPiD * (900.0 + (520.0 - 900.0) * u) / sampleRate;
                float tone = (float)(Math.Sin(phase) + 0.5 * Math.Sin(2.0 * phase) + 0.25 * Math.Sin(3.0 * phase));
                float flutter = 0.55f + 0.45f * (float)Math.Sin(TwoPiD * 85.0 * i / sampleRate);
                float creak = 0.45f * tone * flutter * MathF.Exp(-t / 0.045f);
                o[i] = (1.2f * tick + creak) * AttackRelease(i, n, sampleRate, 0.0005f, 0.015f);
            }
            return o;
        }
    }
}
