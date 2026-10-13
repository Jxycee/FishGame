// Core/Ecology/ShoreGridMath.cs: shore decimetres and reachability bits for the grid bake (spec 12.5, M3-D6, M3-D7).
// Authority: none (pure; ShoreGridBaker in the editor and the map validators call it).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public static class ShoreGridMath
    {
        // water cells: round(max(0, chamfer - 0.5) * cellSizeM * 10), capped at ShoreDmMax (no land at all: ShoreDmMax);
        // other cells: ShoreDmNotWater.
        public static ushort[] ShoreDecimetres(bool[] water, bool[] land, int width, int height, float cellSizeM)
        {
            if (water == null) throw new ArgumentNullException(nameof(water));
            if (!(cellSizeM > 0f) || float.IsInfinity(cellSizeM)) throw new ArgumentException("cellSizeM must be finite and > 0.", nameof(cellSizeM));
            float[] dist = DistanceField.Compute(land, width, height);
            if (water.Length != dist.Length) throw new ArgumentException("water.Length must equal width * height.", nameof(water));
            var dm = new ushort[dist.Length];
            for (int i = 0; i < dm.Length; i++)
            {
                if (!water[i]) { dm[i] = EcologyConstants.ShoreDmNotWater; continue; }
                float cells = dist[i];
                if (float.IsPositiveInfinity(cells)) { dm[i] = EcologyConstants.ShoreDmMax; continue; }
                double v = Math.Round(Math.Max(0.0, cells - 0.5) * cellSizeM * 10.0, MidpointRounding.AwayFromZero);
                dm[i] = v >= EcologyConstants.ShoreDmMax ? EcologyConstants.ShoreDmMax : (ushort)v;
            }
            return dm;
        }

        // A castable cell is reachable when its centre's horizontal distance to some stand point is in [minM, maxM] (M3-D6, any facing).
        public static byte[] ReachableBits(int width, int height, float cellSizeM, float originX, float originZ, byte[] castableBits,
                                           IReadOnlyList<float> standXZ, float minM, float maxM)
        {
            if (castableBits == null) throw new ArgumentNullException(nameof(castableBits));
            if (standXZ == null) throw new ArgumentNullException(nameof(standXZ));
            if (width <= 0 || height <= 0) throw new ArgumentException("width and height must be > 0.");
            int n = width * height;
            if (castableBits.Length != (n + 7) / 8) throw new ArgumentException("castableBits length must be ceil(w*h/8).", nameof(castableBits));
            if ((standXZ.Count & 1) != 0) throw new ArgumentException("standXZ holds x, z pairs.", nameof(standXZ));
            if (!(cellSizeM > 0f) || !(minM >= 0f) || !(maxM >= minM)) throw new ArgumentException("bad cell size or ring.");

            var bits = new byte[castableBits.Length];
            double min2 = (double)minM * minM, max2 = (double)maxM * maxM;
            int stands = standXZ.Count / 2;
            for (int cz = 0; cz < height; cz++)
            {
                double z = originZ + (cz + 0.5) * cellSizeM;
                for (int cx = 0; cx < width; cx++)
                {
                    int i = cz * width + cx;
                    if (!ShoreGrid.GetBit(castableBits, i)) continue;
                    double x = originX + (cx + 0.5) * cellSizeM;
                    for (int s = 0; s < stands; s++)
                    {
                        double dx = x - standXZ[2 * s], dz = z - standXZ[2 * s + 1];
                        double d2 = dx * dx + dz * dz;
                        if (d2 >= min2 && d2 <= max2) { bits[i >> 3] |= (byte)(1 << (i & 7)); break; }
                    }
                }
            }
            return bits;
        }

        // Index = sub-area. Counts every cell that is water, castable and reachable, by the band of its shore distance.
        public static int[] CountReachablePerSubArea(ShoreGrid grid, ZoneData zone)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            var counts = new int[zone.SubAreas.Count];
            for (int cz = 0; cz < grid.Height; cz++)
            {
                for (int cx = 0; cx < grid.Width; cx++)
                {
                    if (!grid.IsCastable(cx, cz) || !grid.IsReachable(cx, cz)) continue;
                    grid.CellCentre(cx, cz, out float x, out float z);
                    if (!grid.IsWater(x, z)) continue;
                    counts[zone.SubAreaIndexFor(grid.ShoreM(x, z))]++;
                }
            }
            return counts;
        }

        // Inverse of ShoreGrid.GetBit: bit i goes to byte[i >> 3] at 1 << (i & 7).
        public static byte[] PackBits(bool[] bits)
        {
            if (bits == null) throw new ArgumentNullException(nameof(bits));
            var packed = new byte[(bits.Length + 7) / 8];
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i]) packed[i >> 3] |= (byte)(1 << (i & 7));
            }
            return packed;
        }
    }
}
