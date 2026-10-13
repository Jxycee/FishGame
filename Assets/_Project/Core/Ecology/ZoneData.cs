// Core/Ecology/ZoneData.cs: one fishing zone as the resolver reads it (spec 12.3): shore-distance sub-areas, spawn rules, junk list,
// weather table and the baked grid. Immutable: every array is copied. Authority: none (built identically on every peer from content).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class ZoneData
    {
        private readonly WeatherTableData _weather;

        // ArgumentNullException: null sub-areas, rules or junk. ArgumentException: no sub-areas, more than MaxSubAreas, a broken band chain
        // (B-D18: first band starts at 0, each starts where the previous ends, Max > Min, the last Max is +Infinity), empty junk.
        // Rules may be empty; the grid may be null (tests). Weather arrays are copied, not validated (WeatherDirector validates them).
        public ZoneData(SubAreaBandData[] subAreas, SpawnRuleData[] rules, JunkEntry[] junk, in WeatherTableData weather, ShoreGrid grid)
        {
            if (subAreas == null) throw new ArgumentNullException(nameof(subAreas));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (junk == null) throw new ArgumentNullException(nameof(junk));
            if (subAreas.Length == 0) throw new ArgumentException("A zone needs at least one sub-area.", nameof(subAreas));
            if (subAreas.Length > EcologyConstants.MaxSubAreas)
                throw new ArgumentException("A zone has at most " + EcologyConstants.MaxSubAreas + " sub-areas.", nameof(subAreas));
            if (junk.Length == 0) throw new ArgumentException("A zone needs at least one junk entry.", nameof(junk));

            if (subAreas[0].MinShoreM != 0f) throw new ArgumentException("The first sub-area must start at 0 m.", nameof(subAreas));
            for (int i = 0; i < subAreas.Length; i++)
            {
                SubAreaBandData b = subAreas[i];
                if (!(b.MaxShoreM > b.MinShoreM)) throw new ArgumentException("Sub-area " + i + " must have Max > Min.", nameof(subAreas));
                if (i > 0 && b.MinShoreM != subAreas[i - 1].MaxShoreM)
                    throw new ArgumentException("Sub-area " + i + " must start where sub-area " + (i - 1) + " ends.", nameof(subAreas));
            }
            if (!float.IsPositiveInfinity(subAreas[subAreas.Length - 1].MaxShoreM))
                throw new ArgumentException("The last sub-area must reach +Infinity.", nameof(subAreas));

            SubAreas = Array.AsReadOnly((SubAreaBandData[])subAreas.Clone());
            Rules = Array.AsReadOnly((SpawnRuleData[])rules.Clone());
            Junk = Array.AsReadOnly((JunkEntry[])junk.Clone());
            _weather = new WeatherTableData
            {
                Base = weather.Base == null ? null : (float[])weather.Base.Clone(),
                SeasonColumnMult = weather.SeasonColumnMult == null ? null : (float[])weather.SeasonColumnMult.Clone(),
            };
            Grid = grid;
        }

        public IReadOnlyList<SubAreaBandData> SubAreas { get; }
        public IReadOnlyList<SpawnRuleData> Rules { get; }
        public IReadOnlyList<JunkEntry> Junk { get; }
        public ref readonly WeatherTableData Weather => ref _weather;
        public ShoreGrid Grid { get; }

        // The band with Min <= s < Max; s < 0 gives 0; NaN or +Infinity gives the last band.
        public byte SubAreaIndexFor(float shoreM)
        {
            int last = SubAreas.Count - 1;
            if (float.IsNaN(shoreM) || float.IsPositiveInfinity(shoreM)) return (byte)last;
            if (shoreM < 0f) return 0;
            for (int i = 0; i < last; i++)
            {
                if (shoreM >= SubAreas[i].MinShoreM && shoreM < SubAreas[i].MaxShoreM) return (byte)i;
            }
            return (byte)last;
        }
    }
}
