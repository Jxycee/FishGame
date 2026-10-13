// Core/Ecology/DistanceField.cs: spec 12.5 two-pass chamfer (1 axis, sqrt 2 diagonal), cell units, centre to nearest land centre.
// Authority: none (pure maths; the editor bake calls it). +Infinity where there is no land at all.
using System;

namespace FishGame.Core
{
    public static class DistanceField
    {
        private const float Diagonal = 1.41421356f;

        public static float[] Compute(bool[] land, int width, int height)
        {
            if (land == null) throw new ArgumentNullException(nameof(land));
            if (width <= 0 || height <= 0 || (long)width * height != land.Length)
                throw new ArgumentException("land.Length must equal width * height (both > 0).", nameof(land));
            var d = new float[land.Length];
            for (int i = 0; i < d.Length; i++) d[i] = land[i] ? 0f : float.PositiveInfinity;

            // forward pass: rows z = 0..h-1, columns x = 0..w-1; neighbours W, N, NW, NE
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    float v = d[i];
                    if (x > 0) v = Math.Min(v, d[i - 1] + 1f);
                    if (z > 0)
                    {
                        int up = i - width;
                        v = Math.Min(v, d[up] + 1f);
                        if (x > 0) v = Math.Min(v, d[up - 1] + Diagonal);
                        if (x < width - 1) v = Math.Min(v, d[up + 1] + Diagonal);
                    }
                    d[i] = v;
                }
            }

            // backward pass: reverse order; neighbours E, S, SE, SW
            for (int z = height - 1; z >= 0; z--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = z * width + x;
                    float v = d[i];
                    if (x < width - 1) v = Math.Min(v, d[i + 1] + 1f);
                    if (z < height - 1)
                    {
                        int down = i + width;
                        v = Math.Min(v, d[down] + 1f);
                        if (x < width - 1) v = Math.Min(v, d[down + 1] + Diagonal);
                        if (x > 0) v = Math.Min(v, d[down - 1] + Diagonal);
                    }
                    d[i] = v;
                }
            }
            return d;
        }
    }
}
