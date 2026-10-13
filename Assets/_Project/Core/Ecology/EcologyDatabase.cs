// Core/Ecology/EcologyDatabase.cs: immutable bundle of the fishing database, the zones, the spots' ecology facts and the tuning (C 7.5).
// Index spaces: zones by ContentRegistry zone index; spots by spot index. Arrays are copied. Authority: none (read-only data built
// identically on every peer from the same content).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class EcologyDatabase
    {
        private readonly EcologyTuning _tuning;

        // ArgumentNullException (an ArgumentException) for null fishing, zones or spots. ArgumentException for a null zone, a rule species
        // index >= the species count, a junk index >= the junk count, or a spot zone index outside the zones.
        public EcologyDatabase(FishingDatabase fishing, ZoneData[] zones, SpotEcologyData[] spots, in EcologyTuning tuning)
        {
            if (fishing == null) throw new ArgumentNullException(nameof(fishing));
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            if (spots == null) throw new ArgumentNullException(nameof(spots));

            for (int z = 0; z < zones.Length; z++)
            {
                ZoneData zone = zones[z];
                if (zone == null) throw new ArgumentException("zone " + z + " is null.", nameof(zones));
                for (int r = 0; r < zone.Rules.Count; r++)
                {
                    if (zone.Rules[r].SpeciesIndex >= fishing.Species.Count)
                        throw new ArgumentException("zone " + z + " rule " + r + " has species index " + zone.Rules[r].SpeciesIndex
                                                    + " but there are only " + fishing.Species.Count + " species.", nameof(zones));
                }
                for (int j = 0; j < zone.Junk.Count; j++)
                {
                    if (zone.Junk[j].JunkIndex >= fishing.Junk.Count)
                        throw new ArgumentException("zone " + z + " junk " + j + " has junk index " + zone.Junk[j].JunkIndex
                                                    + " but there are only " + fishing.Junk.Count + " junk items.", nameof(zones));
                }
            }
            for (int s = 0; s < spots.Length; s++)
            {
                if ((uint)spots[s].ZoneIndex >= (uint)zones.Length)
                    throw new ArgumentException("spot " + s + " has zone index " + spots[s].ZoneIndex + " but there are only " + zones.Length + " zones.", nameof(spots));
            }

            Fishing = fishing;
            Zones = Array.AsReadOnly((ZoneData[])zones.Clone());
            Spots = Array.AsReadOnly((SpotEcologyData[])spots.Clone());
            _tuning = tuning;
        }

        public FishingDatabase Fishing { get; }
        public IReadOnlyList<ZoneData> Zones { get; }
        public IReadOnlyList<SpotEcologyData> Spots { get; }
        public ref readonly EcologyTuning Tuning => ref _tuning;
    }
}
