using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). The baked shore-distance grid of one map (spec 12.5) as an asset: shore distance in decimetres per
    // cell (0xFFFF = not water), the castable and reachable bit arrays (LSB-first by cell index cz * width + cx) and the stand points the
    // reachability bake used. width 0 = not baked yet (ToCore gives null). Values are written only by the editor bake (WriteBake) and
    // stay in place across rebakes (stable GUID). No tuning initialisers. Owned by T9 (M3).
    public sealed class ShoreGridDef : ScriptableObject
    {
        private static readonly ushort[] NoShorts = new ushort[0];
        private static readonly byte[] NoBytes = new byte[0];
        private static readonly Vector3[] NoPoints = new Vector3[0];

        [SerializeField] private string id;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private float cellSizeM;
        [SerializeField] private float originX;
        [SerializeField] private float originZ;
        [SerializeField] private ushort[] shoreDm;
        [SerializeField] private byte[] castableBits;
        [SerializeField] private byte[] reachableBits;
        [SerializeField] private Vector3[] standPoints;
        [SerializeField] private string bakedFromScene;

        public string Id => id;
        public int Width => width;
        public int Height => height;
        public float CellSizeM => cellSizeM;
        public float OriginX => originX;
        public float OriginZ => originZ;
        public IReadOnlyList<ushort> ShoreDm => shoreDm ?? NoShorts;
        public IReadOnlyList<byte> CastableBits => castableBits ?? NoBytes;
        public IReadOnlyList<byte> ReachableBits => reachableBits ?? NoBytes;
        public IReadOnlyList<Vector3> StandPoints => standPoints ?? NoPoints;
        public string BakedFromScene => bakedFromScene;

        // Used by the content builder (the unbaked asset: width 0) and by tests. Copies everything, checks nothing.
        public void Configure(string id, int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm,
            byte[] castableBits, byte[] reachableBits, Vector3[] standPoints, string bakedFromScene)
        {
            this.id = id;
            Assign(width, height, cellSizeM, originX, originZ, shoreDm, castableBits, reachableBits, standPoints, bakedFromScene);
        }

        // Editor-callable; used only by ShoreGridBaker (T16). Replaces every field except id. ArgumentException (and no change) when
        // width or height < 1, cellSizeM <= 0 (or not finite), any array is null, shoreDm.Length != width * height, or a bit array's
        // length != (width * height + 7) / 8.
        public void WriteBake(int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm, byte[] castableBits,
            byte[] reachableBits, Vector3[] standPoints, string bakedFromScene)
        {
            if (width < 1 || height < 1) throw new ArgumentException("width and height must be >= 1.");
            if (float.IsNaN(cellSizeM) || float.IsInfinity(cellSizeM) || !(cellSizeM > 0f)) throw new ArgumentException("cellSizeM must be finite and > 0.", nameof(cellSizeM));
            if (shoreDm == null) throw new ArgumentException("shoreDm must not be null.", nameof(shoreDm));
            if (castableBits == null) throw new ArgumentException("castableBits must not be null.", nameof(castableBits));
            if (reachableBits == null) throw new ArgumentException("reachableBits must not be null.", nameof(reachableBits));
            if (standPoints == null) throw new ArgumentException("standPoints must not be null.", nameof(standPoints));
            long cells = (long)width * height;
            if (shoreDm.Length != cells) throw new ArgumentException("shoreDm.Length must equal width * height.", nameof(shoreDm));
            long bytes = (cells + 7) / 8;
            if (castableBits.Length != bytes) throw new ArgumentException("castableBits.Length must be ceil(w*h/8).", nameof(castableBits));
            if (reachableBits.Length != bytes) throw new ArgumentException("reachableBits.Length must be ceil(w*h/8).", nameof(reachableBits));
            Assign(width, height, cellSizeM, originX, originZ, shoreDm, castableBits, reachableBits, standPoints, bakedFromScene);
        }

        // null when width == 0 (not baked); otherwise a ShoreGrid over copies (its own length checks pass through).
        public ShoreGrid ToCore()
        {
            if (width == 0) return null;
            return new ShoreGrid(width, height, cellSizeM, originX, originZ, shoreDm ?? NoShorts, castableBits ?? NoBytes, reachableBits ?? NoBytes);
        }

        private void Assign(int width, int height, float cellSizeM, float originX, float originZ, ushort[] shoreDm, byte[] castableBits,
            byte[] reachableBits, Vector3[] standPoints, string bakedFromScene)
        {
            this.width = width;
            this.height = height;
            this.cellSizeM = cellSizeM;
            this.originX = originX;
            this.originZ = originZ;
            this.shoreDm = shoreDm == null ? null : (ushort[])shoreDm.Clone();
            this.castableBits = castableBits == null ? null : (byte[])castableBits.Clone();
            this.reachableBits = reachableBits == null ? null : (byte[])reachableBits.Clone();
            this.standPoints = standPoints == null ? null : (Vector3[])standPoints.Clone();
            this.bakedFromScene = bakedFromScene;
        }
    }
}
