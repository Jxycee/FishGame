using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). The M3 ecology half of the registry: the zones category, the ecology tuning and the Core
    // EcologyDatabase built in Build() when zones exist. Owned by T9 (M3). ContentRegistry.cs calls the four private hooks below.
    public sealed partial class ContentRegistry
    {
        public const string CategoryZones = "zones";

        [SerializeField] private List<FishingZoneDef> zones = new List<FishingZoneDef>();
        [SerializeField] private EcologyTuningDef ecologyTuning;

        [NonSerialized] private RegistryIndex zonesIndex;
        [NonSerialized] private Dictionary<string, FishingZoneDef> zonesById;
        [NonSerialized] private EcologyDatabase ecology;

        public IReadOnlyList<FishingZoneDef> Zones => zones;
        public EcologyTuningDef EcologyTuning => ecologyTuning;

        // The Core ecology database, built in Build() when zones is non-empty and the fishing database exists; null otherwise.
        public EcologyDatabase Ecology
        {
            get
            {
                EnsureBuilt();
                return ecology;
            }
        }

        // Used by the ecology content builder and by tests. Does not touch the spots, the fishing fields or dataVersion.
        public void ConfigureEcology(IEnumerable<FishingZoneDef> zones, EcologyTuningDef tuning)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            this.zones = new List<FishingZoneDef>(zones);
            ecologyTuning = tuning;
            ClearCache();
        }

        public ushort ZoneIndex(string id)
        {
            EnsureBuilt();
            return zonesIndex.IndexOf(id);
        }

        public FishingZoneDef ZoneAt(ushort index)
        {
            EnsureBuilt();
            return zonesById[zonesIndex.IdAt(index)];
        }

        // The first spot, in index order, whose scene name equals sceneName ordinally. False for null or an unknown scene.
        public bool TryGetSpotByScene(string sceneName, out SpotDef spot)
        {
            EnsureBuilt();
            spot = null;
            if (sceneName == null) return false;
            for (int i = 0; i < spotsIndex.Count; i++)
            {
                SpotDef candidate = spotsById[spotsIndex.IdAt((ushort)i)];
                if (string.Equals(candidate.SceneName, sceneName, StringComparison.Ordinal))
                {
                    spot = candidate;
                    return true;
                }
            }
            return false;
        }

        // ---- hooks called from ContentRegistry.cs ----------------------------------------------------------------------------------

        private void ValidateEcology(List<string> errors)
        {
            ValidateIds("zones", zones, d => d.Id, errors);
            for (int i = 0; i < zones.Count; i++)
            {
                FishingZoneDef zone = zones[i];
                if (zone == null || !ContentIds.IsValidId(zone.Id)) continue;
                string z = "zones[" + i + "] '" + zone.Id + "'";
                if (zone.SubAreas.Count == 0) errors.Add(z + " has no sub-areas.");
                if (zone.Junk.Count == 0) errors.Add(z + " has no junk.");
                if (zone.Weather == null) errors.Add(z + " has no weather table.");
                else if (zone.Weather.BaseMatrix.Count != 9 || zone.Weather.SeasonColumnMult.Count != 12)
                {
                    errors.Add(z + " weather '" + zone.Weather.Id + "' needs 9 base and 12 season values.");
                }

                for (int j = 0; j < zone.Rules.Count; j++)
                {
                    SpawnRule rule = zone.Rules[j];
                    string r = z + " rules[" + j + "]";
                    if (rule == null || rule.fish == null)
                    {
                        errors.Add(r + " has no fish.");
                        continue;
                    }
                    if (!ListedSpecies(rule.fish))
                    {
                        errors.Add(r + " fish '" + rule.fish.Id + "' is not listed in species.");
                        continue;
                    }
                    if (rule.subAreaIds == null) continue;
                    for (int k = 0; k < rule.subAreaIds.Length; k++)
                    {
                        if (!HasSubArea(zone, rule.subAreaIds[k]))
                        {
                            errors.Add(r + " fish '" + rule.fish.Id + "' names unknown sub-area '" + rule.subAreaIds[k] + "'.");
                        }
                    }
                }

                for (int k = 0; k < zone.Junk.Count; k++)
                {
                    JunkDef item = zone.Junk[k].junk;
                    string jn = z + " junk[" + k + "]";
                    if (item == null) errors.Add(jn + " has no junk item.");
                    else if (!ListedJunk(item)) errors.Add(jn + " '" + item.Id + "' is not listed in junk.");
                }
            }

            if (zones.Count == 0) return;
            for (int i = 0; i < spots.Count; i++)
            {
                SpotDef spot = spots[i];
                if (spot == null || !ContentIds.IsValidId(spot.Id)) continue;
                if (spot.Zone == null) errors.Add("spots[" + i + "] '" + spot.Id + "' has no zone.");
                else if (!ListedZone(spot.Zone)) errors.Add("spots[" + i + "] '" + spot.Id + "' zone '" + spot.Zone.Id + "' is not listed in zones.");
            }
            if (ecologyTuning == null) errors.Add("ecologyTuning is missing.");
        }

        // Runs after built = true: FishingZoneDef.ToCore calls SpeciesIndex/JunkIndex, which EnsureBuilt; before built = true that would
        // re-enter Build() forever. A failure leaves no half-built cache: the next access rebuilds and throws the same error.
        private void BuildEcology()
        {
            try
            {
                zonesById = BuildIndex(zones, d => d.Id, out zonesIndex);
                ecology = null;
                if (zones.Count == 0 || fishing == null) return;
                var zoneData = new ZoneData[zonesIndex.Count];
                for (ushort i = 0; i < zoneData.Length; i++) zoneData[i] = zonesById[zonesIndex.IdAt(i)].ToCore(this);
                var spotData = new SpotEcologyData[spotsIndex.Count];
                for (ushort i = 0; i < spotData.Length; i++)
                {
                    SpotDef spot = spotsById[spotsIndex.IdAt(i)];
                    spotData[i] = new SpotEcologyData { ZoneIndex = zonesIndex.IndexOf(spot.Zone.Id), MapOrder = (byte)spot.MapOrder };
                }
                EcologyTuning tuning = ecologyTuning.ToCore();
                ecology = new EcologyDatabase(fishing, zoneData, spotData, in tuning);
            }
            catch
            {
                ClearCache();   // built = false again
                throw;
            }
        }

        private void AddEcologyCategories(List<ContentCategoryIds> categories)
        {
            if (zones.Count > 0) categories.Add(new ContentCategoryIds(CategoryZones, zonesIndex.SortedIds));
        }

        private void ClearEcologyCache()
        {
            zonesIndex = null;
            zonesById = null;
            ecology = null;
        }

        // ---- helpers ---------------------------------------------------------------------------------------------------------------

        private bool ListedSpecies(FishSpeciesDef def)
        {
            for (int i = 0; i < species.Count; i++) if (ReferenceEquals(species[i], def)) return true;
            return false;
        }

        private bool ListedJunk(JunkDef def)
        {
            for (int i = 0; i < junk.Count; i++) if (ReferenceEquals(junk[i], def)) return true;
            return false;
        }

        private bool ListedZone(FishingZoneDef def)
        {
            for (int i = 0; i < zones.Count; i++) if (ReferenceEquals(zones[i], def)) return true;
            return false;
        }

        private static bool HasSubArea(FishingZoneDef zone, string id)
        {
            for (int i = 0; i < zone.SubAreas.Count; i++)
            {
                if (string.Equals(zone.SubAreas[i].id, id, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
