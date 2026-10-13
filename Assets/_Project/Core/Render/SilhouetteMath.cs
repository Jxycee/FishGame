// Core/Render/SilhouetteMath.cs: the journal icon outline (M3-D29, B-D16). Pure and allocation-free; IconFactory (UI) rasterises it.
// u runs from the head (0) to the tail (1). Half-height g(u) = max(0, sin(pi * u^a))^p scaled by H / k, where (H, a, p) is the shape's
// table row and k is the species' lengthShapeK clamped to [0.25, 4]. Authority: none.
using System;

namespace FishGame.Core
{
    public static class SilhouetteMath
    {
        private const double MinK = 0.25, MaxK = 4.0;

        // body half-height at u along the length (length 1): 0 at both tips and outside 0..1, > 0 inside
        public static float HalfHeight(BodyShape shape, float lengthShapeK, float u01)
        {
            if (float.IsNaN(u01) || u01 <= 0f || u01 >= 1f) return 0f;
            Row(shape, out double h, out double a, out double p);
            double g = Math.Sin(Math.PI * Math.Pow(u01, a));
            if (!(g > 0.0)) return 0f;                                                // guards the tiny negative sin near u = 1
            return (float)(h / ClampK(lengthShapeK) * Math.Pow(g, p));
        }

        // length / full height of the icon: k / (2 H). The peak of g is 1, so the full height is 2 H / k at length 1.
        public static float AspectRatio(BodyShape shape, float lengthShapeK)
        {
            Row(shape, out double h, out _, out _);
            return (float)(ClampK(lengthShapeK) / (2.0 * h));
        }

        // (H, a, p) per shape; an unknown enum value uses Round.
        private static void Row(BodyShape shape, out double h, out double a, out double p)
        {
            switch (shape)
            {
                case BodyShape.Long: h = 0.14; a = 0.80; p = 0.7; break;
                case BodyShape.Flat: h = 0.36; a = 1.0; p = 1.0; break;
                case BodyShape.Eel: h = 0.05; a = 1.0; p = 0.25; break;
                case BodyShape.Squid: h = 0.16; a = 0.6; p = 0.5; break;
                default: h = 0.30; a = 0.85; p = 0.6; break;                           // Round and unknown
            }
        }

        private static double ClampK(float k)
        {
            if (float.IsNaN(k)) return 1.0;
            return k < MinK ? MinK : (k > MaxK ? MaxK : k);
        }
    }
}
