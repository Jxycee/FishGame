// Core/Journal/JournalHints.cs: journal level-2 hints: the union over every rule of a species in every zone (spec 12.10).
// Authority: none (pure read of the content database, evaluated locally).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public static class JournalHints
    {
        // ORs together the season, weather and band masks of every rule of the species; zonesInto gets each zone index that has at
        // least one such rule, ascending and once. A species with no rule gives None masks and an empty list.
        public static void Summarize(EcologyDatabase db, ushort speciesIndex, out SeasonMask seasons, out WeatherMask weather,
                                     out TimeBandMask bands, List<int> zoneIndicesInto)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (zoneIndicesInto == null) throw new ArgumentNullException(nameof(zoneIndicesInto));
            zoneIndicesInto.Clear();
            seasons = SeasonMask.None; weather = WeatherMask.None; bands = TimeBandMask.None;
            for (int z = 0; z < db.Zones.Count; z++)
            {
                var rules = db.Zones[z].Rules;
                bool any = false;
                for (int i = 0; i < rules.Count; i++)
                {
                    SpawnRuleData r = rules[i];
                    if (r.SpeciesIndex != speciesIndex) continue;
                    seasons |= r.Seasons; weather |= r.Weather; bands |= r.Bands;
                    any = true;
                }
                if (any) zoneIndicesInto.Add(z);
            }
        }
    }
}
