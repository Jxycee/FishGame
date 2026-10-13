// Core/Ecology/ShoreGrid.cs: the baked shore-distance grid of a map (spec 12.5): shore distance in decimetres per cell (0xFFFF = not
// water) plus castable and reachable bit arrays (LSB-first by cell index cz * Width + cx). Immutable: arrays are copied.
// Authority: none (read-only data baked in the editor; the host and the owner's cast marker both read it, 2.8 #26).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class ShoreGrid
    {
        private readonly ushort[] _shoreDm;
        private readonly byte[] _castable;
        private readonly byte[] _reachable;

        public ShoreGrid(int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm, byte[] castableBits, byte[] reachableBits)
        {
            if (shoreDm == null) throw new ArgumentNullException(nameof(shoreDm));
            if (castableBits == null) throw new ArgumentNullException(nameof(castableBits));
            if (reachableBits == null) throw new ArgumentNullException(nameof(reachableBits));
            if (width < 1 || height < 1) throw new ArgumentException("width and height must be >= 1.");
            if (!IsFinite(cellSizeM) || !(cellSizeM > 0f)) throw new ArgumentException("cellSizeM must be finite and > 0.", nameof(cellSizeM));
            if (!IsFinite(originX) || !IsFinite(originZ)) throw new ArgumentException("The origin must be finite.");
            long cells = (long)width * height;
            if (cells > int.MaxValue) throw new ArgumentException("The grid is too large.");
            if (shoreDm.Length != cells) throw new ArgumentException("shoreDm.Length must equal width * height.", nameof(shoreDm));
            int bytes = (int)((cells + 7) / 8);
            if (castableBits.Length != bytes) throw new ArgumentException("castableBits.Length must be ceil(w*h/8).", nameof(castableBits));
            if (reachableBits.Length != bytes) throw new ArgumentException("reachableBits.Length must be ceil(w*h/8).", nameof(reachableBits));

            Width = width; Height = height; CellSizeM = cellSizeM; OriginX = originX; OriginZ = originZ;
            _shoreDm = (ushort[])shoreDm.Clone();
            _castable = (byte[])castableBits.Clone();
            _reachable = (byte[])reachableBits.Clone();
        }

        public int Width { get; }
        public int Height { get; }
        public float CellSizeM { get; }
        public float OriginX { get; }
        public float OriginZ { get; }

        // floor((x - OriginX) / cell) in double; false outside the grid or for non-finite input.
        public bool TryCell(float x, float z, out int cx, out int cz)
        {
            cx = 0; cz = 0;
            if (!IsFinite(x) || !IsFinite(z)) return false;
            double fx = ((double)x - OriginX) / CellSizeM;
            double fz = ((double)z - OriginZ) / CellSizeM;
            double ix = Math.Floor(fx), iz = Math.Floor(fz);
            if (!(ix >= 0.0 && ix < Width && iz >= 0.0 && iz < Height)) return false;
            cx = (int)ix; cz = (int)iz;
            return true;
        }

        public void CellCentre(int cx, int cz, out float x, out float z)
        {
            x = OriginX + (cx + 0.5f) * CellSizeM;
            z = OriginZ + (cz + 0.5f) * CellSizeM;
        }

        // inside the grid and shoreDm != ShoreDmNotWater
        public bool IsWater(float x, float z)
        {
            return TryCell(x, z, out int cx, out int cz) && _shoreDm[cz * Width + cx] != EcologyConstants.ShoreDmNotWater;
        }

        // shoreDm / 10; +Infinity when not water (including outside the grid)
        public float ShoreM(float x, float z)
        {
            if (!TryCell(x, z, out int cx, out int cz)) return float.PositiveInfinity;
            ushort dm = _shoreDm[cz * Width + cx];
            return dm == EcologyConstants.ShoreDmNotWater ? float.PositiveInfinity : dm / 10f;
        }

        public bool IsCastable(int cx, int cz)
        {
            return InGrid(cx, cz) && GetBit(_castable, cz * Width + cx);
        }

        public bool IsReachable(int cx, int cz)
        {
            return InGrid(cx, cz) && GetBit(_reachable, cz * Width + cx);
        }

        // Clears the list, then adds (ascending cell index) every cell that is water, castable AND reachable: the hotspot candidates.
        public void CollectReachableWaterCells(List<int> cellIndicesInto)
        {
            if (cellIndicesInto == null) throw new ArgumentNullException(nameof(cellIndicesInto));
            cellIndicesInto.Clear();
            int n = Width * Height;
            for (int i = 0; i < n; i++)
            {
                if (_shoreDm[i] != EcologyConstants.ShoreDmNotWater && GetBit(_castable, i) && GetBit(_reachable, i)) cellIndicesInto.Add(i);
            }
        }

        // LSB-first: bit i is (packed[i >> 3] >> (i & 7)) & 1.
        public static bool GetBit(byte[] packed, int index)
        {
            return ((packed[index >> 3] >> (index & 7)) & 1) != 0;
        }

        private bool InGrid(int cx, int cz)
        {
            return cx >= 0 && cx < Width && cz >= 0 && cz < Height;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
