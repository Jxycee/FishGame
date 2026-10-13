// Core/World/WaveMath.cs: water surface height (spec 10.4). Core, pure.
// One shared formula: T21's FishGame/Water vertex displacement must use the same direction convention and term:
//   dir = (sin(deg), cos(deg)) on (x, z) (0 deg = +Z), k = 2*pi / Wavelength, term = A * sin(k * (dot(dir, (x, z)) - Speed * t)),
//   and Wavelength <= 0, Amplitude == 0, or any non-finite field (NaN, +/-Infinity) gives term 0.
using System;

namespace FishGame.Core
{
    public static class WaveMath
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double TwoPi = 2.0 * Math.PI;

        // Phase and sine are computed in double (t grows large in long sessions); the sum is returned as float.
        public static float HeightAt(in WaveSet waves, float x, float z, double t)
        {
            double sum = Term(waves.W0, x, z, t) + Term(waves.W1, x, z, t) + Term(waves.W2, x, z, t);
            return (float)sum;
        }

        // Sum of |Amplitude| over the terms that contribute (Wavelength > 0).
        public static float AmplitudeSum(in WaveSet waves)
        {
            return Contribution(waves.W0) + Contribution(waves.W1) + Contribution(waves.W2);
        }

        // A wave with any non-finite field (NaN or +/-Infinity) is a 0 term, in the height and in the amplitude sum.
        private static bool IsUsable(in Wave w)
        {
            return w.Wavelength > 0f
                && !float.IsInfinity(w.Wavelength)
                && IsFinite(w.Amplitude) && IsFinite(w.Speed) && IsFinite(w.DirectionDeg);
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static float Contribution(in Wave w)
        {
            return IsUsable(w) ? Math.Abs(w.Amplitude) : 0f;
        }

        private static double Term(in Wave w, float x, float z, double t)
        {
            if (!IsUsable(w) || w.Amplitude == 0f) return 0.0;
            double th = w.DirectionDeg * DegToRad;
            double k = TwoPi / w.Wavelength;
            double along = Math.Sin(th) * x + Math.Cos(th) * z;
            return w.Amplitude * Math.Sin(k * (along - w.Speed * t));
        }
    }
}
