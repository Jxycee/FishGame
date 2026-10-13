using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). The weather transition table (spec 12.2): a 3 x 3 base matrix (rows current Clear/Rain/Fog,
    // columns next) and 12 season column multipliers (season * 3 + column). No tuning initialisers: values come from EcologySeedData.
    // Owned by T9 (M3).
    public sealed class WeatherTableDef : ScriptableObject
    {
        private static readonly float[] NoFloats = new float[0];

        [SerializeField] private string id;
        [SerializeField] private float[] baseMatrix;
        [SerializeField] private float[] seasonColumnMult;

        public string Id => id;
        public IReadOnlyList<float> BaseMatrix => baseMatrix ?? NoFloats;
        public IReadOnlyList<float> SeasonColumnMult => seasonColumnMult ?? NoFloats;

        // Used by the content builder and by tests. The arrays are copied; null reads back as empty.
        public void Configure(string id, float[] baseMatrix, float[] seasonColumnMult)
        {
            this.id = id;
            this.baseMatrix = baseMatrix == null ? null : (float[])baseMatrix.Clone();
            this.seasonColumnMult = seasonColumnMult == null ? null : (float[])seasonColumnMult.Clone();
        }

        // New arrays every call (null reads as empty; the lengths are checked by ContentRegistry.Validate).
        public WeatherTableData ToCore()
        {
            return new WeatherTableData
            {
                Base = baseMatrix == null ? new float[0] : (float[])baseMatrix.Clone(),
                SeasonColumnMult = seasonColumnMult == null ? new float[0] : (float[])seasonColumnMult.Clone(),
            };
        }
    }
}
