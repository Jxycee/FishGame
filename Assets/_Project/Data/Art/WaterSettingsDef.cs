using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Water look and the three waves: dirAmpLenSpeed entries are
    // (x direction deg, y amplitude m, z wavelength m, w speed m/s). Seeds come from the content builder, not from initialisers.
    // Owned by T13.
    public sealed class WaterSettingsDef : ScriptableObject
    {
        private static readonly Vector4[] NoWaves = new Vector4[0];

        [SerializeField] private Vector4[] dirAmpLenSpeed;
        [SerializeField] private Color shallowColor;
        [SerializeField] private Color deepColor;
        [SerializeField] private float depthRange;
        [SerializeField] private float foamWidth;

        public IReadOnlyList<Vector4> DirAmpLenSpeed => dirAmpLenSpeed ?? NoWaves;
        public Color ShallowColor => shallowColor;
        public Color DeepColor => deepColor;
        public float DepthRange => depthRange;
        public float FoamWidth => foamWidth;

        // Used by the content builder and by tests. The array is copied; null reads back as empty.
        public void Configure(IEnumerable<Vector4> dirAmpLenSpeed, Color shallowColor, Color deepColor, float depthRange, float foamWidth)
        {
            this.dirAmpLenSpeed = dirAmpLenSpeed == null ? null : new List<Vector4>(dirAmpLenSpeed).ToArray();
            this.shallowColor = shallowColor;
            this.deepColor = deepColor;
            this.depthRange = depthRange;
            this.foamWidth = foamWidth;
        }

        // Entries 0..2 become W0..W2; a missing entry is a zero Wave (a flat term).
        public WaveSet ToWaveSet()
        {
            return new WaveSet { W0 = WaveAt(0), W1 = WaveAt(1), W2 = WaveAt(2) };
        }

        private Wave WaveAt(int index)
        {
            Vector4[] v = dirAmpLenSpeed;
            if (v == null || index >= v.Length) return default;
            return new Wave { DirectionDeg = v[index].x, Amplitude = v[index].y, Wavelength = v[index].z, Speed = v[index].w };
        }
    }
}
