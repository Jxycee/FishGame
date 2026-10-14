// Core/Render/DayNightMath.cs: spec 10.3 day-night maths (M3-D24, B-D12..B-D14). Pure; called every frame by the
// DayNightController (T18), so nothing here allocates.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public static class DayNightMath
    {
        public const float ReadabilityDistanceM = 25f, ReadabilityMinFactor = 0.5f;
        private const double SunPeakElevationDeg = 60.0;
        private const float MinFlipWindow = 1e-6f;

        // Phase i holds from start[i]; the change into phase i runs over [start[i], start[i] + w_i] with smoothstep, where
        // w_i = min(window, nextStart - start[i]) (nextStart = 1 after the last phase). Continuous in dayFraction.
        public static PhaseBlend Blend(float dayFraction, IReadOnlyList<float> phaseStarts, float window)
        {
            if (phaseStarts == null || phaseStarts.Count == 0) throw new ArgumentException("phaseStarts needs at least one entry.", nameof(phaseStarts));
            float f = float.IsNaN(dayFraction) ? 0f : (dayFraction < 0f ? 0f : (dayFraction > 1f ? 1f : dayFraction));
            int i = 0;
            for (int k = 1; k < phaseStarts.Count; k++)
            {
                if (!(phaseStarts[k] > phaseStarts[k - 1])) throw new ArgumentException("phaseStarts must be strictly increasing.", nameof(phaseStarts));
                if (f >= phaseStarts[k]) i = k;
            }
            if (i == 0) return new PhaseBlend { From = 0, To = 0, T = 0f };
            float next = i + 1 < phaseStarts.Count ? phaseStarts[i + 1] : 1f;
            float w = Math.Min(window, next - phaseStarts[i]);
            float x = f - phaseStarts[i];
            if (!(w > 0f) || x >= w) return new PhaseBlend { From = i, To = i, T = 0f };
            return new PhaseBlend { From = i - 1, To = i, T = Smooth01(x / w) };
        }

        // Unit vector TO the sun. East (+x) at sunrise, south (-z) at noon, west (-x) at sunset; elevation
        // min + (60 - min) * sin(pi * p), p = progress through daylight, clamped to 0..1 outside it (B-D13).
        public static void SunDirection(float dayFraction, float sunriseFraction, float sunsetFraction, float minElevationDeg,
                                        out float x, out float y, out float z)
        {
            double span = (double)sunsetFraction - sunriseFraction;
            double p = span > 0.0 && !float.IsNaN(dayFraction) ? (dayFraction - sunriseFraction) / span : 0.0;
            p = p < 0.0 ? 0.0 : (p > 1.0 ? 1.0 : p);
            double minE = float.IsNaN(minElevationDeg) ? 0.0 : Math.Max(0.0, Math.Min(SunPeakElevationDeg, minElevationDeg));
            double elev = (minE + (SunPeakElevationDeg - minE) * Math.Sin(Math.PI * p)) * (Math.PI / 180.0);
            double az = Math.PI * p;
            double c = Math.Cos(elev);
            x = (float)(c * Math.Cos(az));
            y = (float)Math.Sin(elev);
            z = (float)(-c * Math.Sin(az));
        }

        // Lamps and string lights (harbour round 0): 1 at night, 0 by day; smoothstep ramp over `window` (day fraction) before sunset and after
        // sunrise, so the lights come on through dusk and are fully lit at the sunset flip. Pure, no allocation.
        public static float LampGlow(float dayFraction, float sunriseFraction, float sunsetFraction, float window)
        {
            if (float.IsNaN(dayFraction)) return 0f;
            if (!(dayFraction > sunriseFraction) || !(dayFraction < sunsetFraction)) return 1f;
            float w = window > MinFlipWindow ? window : MinFlipWindow;
            float day = Math.Min(Smooth01((dayFraction - sunriseFraction) / w), Smooth01((sunsetFraction - dayFraction) / w));
            return 1f - day;
        }

        // 1 by day; smoothstep fade over flipWindow after sunrise and before sunset; 0 at and outside the flips (B-D14).
        public static float SunWeight(float dayFraction, float sunriseFraction, float sunsetFraction, float flipWindow)
        {
            if (float.IsNaN(dayFraction) || !(dayFraction > sunriseFraction) || !(dayFraction < sunsetFraction)) return 0f;
            float w = flipWindow > MinFlipWindow ? flipWindow : MinFlipWindow;
            return Math.Min(Smooth01((dayFraction - sunriseFraction) / w), Smooth01((sunsetFraction - dayFraction) / w));
        }

        // 0 while the sun is up; smoothstep fade-in over flipWindow after sunset and fade-out before sunrise; 0 at the flips.
        public static float MoonWeight(float dayFraction, float sunriseFraction, float sunsetFraction, float flipWindow)
        {
            if (float.IsNaN(dayFraction)) return 0f;
            float w = flipWindow > MinFlipWindow ? flipWindow : MinFlipWindow;
            if (dayFraction >= sunsetFraction) return Smooth01((dayFraction - sunsetFraction) / w);
            if (dayFraction <= sunriseFraction) return Smooth01((sunriseFraction - dayFraction) / w);
            return 0f;
        }

        // ExponentialSquared: exp(-(density * distance)^2).
        public static float FogFactor(float density, float distanceM)
        {
            double k = (double)density * distanceM;
            return (float)Math.Exp(-k * k);
        }

        // FogFactor(density, 25) >= 0.5, i.e. density <= sqrt(ln 2) / 25 = 0.0333; NaN is never readable.
        public static bool IsReadable(float density)
        {
            return FogFactor(density, ReadabilityDistanceM) >= ReadabilityMinFactor;
        }

        private static float Smooth01(float x)
        {
            if (!(x > 0f)) return 0f;
            if (x >= 1f) return 1f;
            return x * x * (3f - 2f * x);
        }
    }
}
