using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Host and clients build the same ordinal indices from the same ids,
    // and the ContentHash over those ids goes in the connect payload.
    // Categories: spots (M1), rods, baits, fight_profiles, species, junk (M2). Fishing database built in Build() when fishing content is present.
    // Changing a SpotDef's id after Build() is not tracked; call Configure or Build again.
    public sealed class ContentRegistry : ScriptableObject
    {
        public const string CategorySpots = "spots";
        public const string CategoryRods = "rods", CategoryBaits = "baits", CategoryFightProfiles = "fight_profiles",
                            CategorySpecies = "species", CategoryJunk = "junk";

        [SerializeField] private int dataVersion = 1;
        [SerializeField] private List<SpotDef> spots = new List<SpotDef>();
        [SerializeField] private List<RodDef> rods = new List<RodDef>();
        [SerializeField] private List<BaitDef> baits = new List<BaitDef>();
        [SerializeField] private List<FightProfileDef> fightProfiles = new List<FightProfileDef>();
        [SerializeField] private List<FishSpeciesDef> species = new List<FishSpeciesDef>();
        [SerializeField] private List<JunkDef> junk = new List<JunkDef>();
        [SerializeField] private FishingTuningDef fishingTuning;
        [SerializeField] private SfxCueTableDef sfxCueTable;
        [SerializeField] private ArtPaletteDef artPalette;
        [SerializeField] private WaterSettingsDef waterSettings;

        // Cache: never serialized. Build() rebuilds it; Configure() and ConfigureFishing() clear it.
        [NonSerialized] private bool built;
        [NonSerialized] private RegistryIndex spotsIndex;
        [NonSerialized] private Dictionary<string, SpotDef> spotsById;
        [NonSerialized] private RegistryIndex rodsIndex, baitsIndex, profilesIndex, speciesIndex, junkIndex;
        [NonSerialized] private Dictionary<string, RodDef> rodsById;
        [NonSerialized] private Dictionary<string, BaitDef> baitsById;
        [NonSerialized] private Dictionary<string, FightProfileDef> profilesById;
        [NonSerialized] private Dictionary<string, FishSpeciesDef> speciesById;
        [NonSerialized] private Dictionary<string, JunkDef> junkById;
        [NonSerialized] private FishingDatabase fishing;

        public int DataVersion => dataVersion;

        public IReadOnlyList<SpotDef> Spots => spots;
        public IReadOnlyList<RodDef> Rods => rods;
        public IReadOnlyList<BaitDef> Baits => baits;
        public IReadOnlyList<FightProfileDef> FightProfiles => fightProfiles;
        public IReadOnlyList<FishSpeciesDef> Species => species;
        public IReadOnlyList<JunkDef> Junk => junk;

        public FishingTuningDef FishingTuning => fishingTuning;
        public SfxCueTableDef SfxCueTable => sfxCueTable;
        public ArtPaletteDef ArtPalette => artPalette;
        public WaterSettingsDef WaterSettings => waterSettings;

        // The Core fishing database, built in Build() when any fishing list is non-empty; null otherwise.
        public FishingDatabase Fishing
        {
            get
            {
                EnsureBuilt();
                return fishing;
            }
        }

        // Used by the T15 ContentBuilder and by tests. Does not touch the fishing fields.
        public void Configure(int dataVersion, IEnumerable<SpotDef> spots)
        {
            if (spots == null)
            {
                throw new ArgumentNullException(nameof(spots));
            }

            this.dataVersion = dataVersion;
            this.spots = new List<SpotDef>(spots);
            ClearCache();
        }

        // Used by the fishing content builder and by tests. Does not touch the spots or dataVersion. The singletons may be null
        // (Validate then reports them when any list is non-empty).
        public void ConfigureFishing(IEnumerable<RodDef> rods, IEnumerable<BaitDef> baits, IEnumerable<FightProfileDef> profiles,
            IEnumerable<FishSpeciesDef> species, IEnumerable<JunkDef> junk, FishingTuningDef tuning,
            SfxCueTableDef cues, ArtPaletteDef palette, WaterSettingsDef water)
        {
            if (rods == null) throw new ArgumentNullException(nameof(rods));
            if (baits == null) throw new ArgumentNullException(nameof(baits));
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (junk == null) throw new ArgumentNullException(nameof(junk));

            this.rods = new List<RodDef>(rods);
            this.baits = new List<BaitDef>(baits);
            this.fightProfiles = new List<FightProfileDef>(profiles);
            this.species = new List<FishSpeciesDef>(species);
            this.junk = new List<JunkDef>(junk);
            fishingTuning = tuning;
            sfxCueTable = cues;
            artPalette = palette;
            waterSettings = water;
            ClearCache();
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < spots.Count; i++)
            {
                SpotDef def = spots[i];
                if (def == null)
                {
                    errors.Add("spots[" + i + "] is null.");
                    continue;
                }

                if (!ContentIds.IsValidId(def.Id))
                {
                    errors.Add("spots[" + i + "] has invalid id '" + def.Id + "'.");
                    continue;
                }

                if (!seen.Add(def.Id))
                {
                    errors.Add("spots[" + i + "] duplicate id '" + def.Id + "'.");
                }
            }

            ValidateIds("rods", rods, d => d.Id, errors);
            ValidateIds("baits", baits, d => d.Id, errors);
            ValidateIds("fightProfiles", fightProfiles, d => d.Id, errors);
            ValidateIds("species", species, d => d.Id, errors);
            ValidateSpeciesFights(errors);
            ValidateIds("junk", junk, d => d.Id, errors);

            if (HasFishingContent())
            {
                if (rods.Count == 0) errors.Add("rods is empty.");
                if (baits.Count == 0) errors.Add("baits is empty.");
                if (fightProfiles.Count == 0) errors.Add("fightProfiles is empty.");
                if (fishingTuning == null) errors.Add("fishingTuning is missing.");
                if (sfxCueTable == null) errors.Add("sfxCueTable is missing.");
                if (artPalette == null) errors.Add("artPalette is missing.");
                if (waterSettings == null) errors.Add("waterSettings is missing.");
                if (!HasUnlimitedBait()) errors.Add("baits has no unlimited bait.");
            }
            return errors;
        }

        public void Build()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("ContentRegistry is invalid: " + string.Join("; ", errors));
            }

            var ids = new List<string>(spots.Count);
            var byId = new Dictionary<string, SpotDef>(spots.Count, StringComparer.Ordinal);
            foreach (SpotDef def in spots)
            {
                ids.Add(def.Id);
                byId.Add(def.Id, def);
            }

            spotsIndex = new RegistryIndex(ids);
            spotsById = byId;

            rodsById = BuildIndex(rods, d => d.Id, out rodsIndex);
            baitsById = BuildIndex(baits, d => d.Id, out baitsIndex);
            profilesById = BuildIndex(fightProfiles, d => d.Id, out profilesIndex);
            speciesById = BuildIndex(species, d => d.Id, out speciesIndex);
            junkById = BuildIndex(junk, d => d.Id, out junkIndex);

            fishing = HasFishingContent() ? BuildDatabase() : null;
            built = true;
        }

        public ushort SpotIndex(string id)
        {
            EnsureBuilt();
            return spotsIndex.IndexOf(id);
        }

        public SpotDef SpotAt(ushort index)
        {
            EnsureBuilt();
            return spotsById[spotsIndex.IdAt(index)];
        }

        public bool TryGetSpot(string id, out SpotDef def)
        {
            EnsureBuilt();
            if (id == null)
            {
                def = null;
                return false;
            }
            return spotsById.TryGetValue(id, out def);
        }

        public ushort RodIndex(string id)
        {
            EnsureBuilt();
            return rodsIndex.IndexOf(id);
        }

        public RodDef RodAt(ushort index)
        {
            EnsureBuilt();
            return rodsById[rodsIndex.IdAt(index)];
        }

        public ushort BaitIndex(string id)
        {
            EnsureBuilt();
            return baitsIndex.IndexOf(id);
        }

        public BaitDef BaitAt(ushort index)
        {
            EnsureBuilt();
            return baitsById[baitsIndex.IdAt(index)];
        }

        public ushort FightProfileIndex(string id)
        {
            EnsureBuilt();
            return profilesIndex.IndexOf(id);
        }

        public FightProfileDef FightProfileAt(ushort index)
        {
            EnsureBuilt();
            return profilesById[profilesIndex.IdAt(index)];
        }

        public ushort SpeciesIndex(string id)
        {
            EnsureBuilt();
            return speciesIndex.IndexOf(id);
        }

        public FishSpeciesDef SpeciesAt(ushort index)
        {
            EnsureBuilt();
            return speciesById[speciesIndex.IdAt(index)];
        }

        public bool TryGetSpecies(string id, out FishSpeciesDef def)
        {
            EnsureBuilt();
            if (id == null)
            {
                def = null;
                return false;
            }
            return speciesById.TryGetValue(id, out def);
        }

        public ushort JunkIndex(string id)
        {
            EnsureBuilt();
            return junkIndex.IndexOf(id);
        }

        public JunkDef JunkAt(ushort index)
        {
            EnsureBuilt();
            return junkById[junkIndex.IdAt(index)];
        }

        // UI helper: the display name of a catch (junk or species) by its content index.
        public string DisplayNameOf(bool isJunk, ushort itemIndex)
        {
            return isJunk ? JunkAt(itemIndex).DisplayName : SpeciesAt(itemIndex).DisplayName;
        }

        // Spots are always hashed; each fishing category only when its list is non-empty, so a spots-only registry
        // hashes exactly as in M1 (ruling D-Q1).
        public ulong ComputeContentHash()
        {
            EnsureBuilt();
            var categories = new List<ContentCategoryIds>
            {
                new ContentCategoryIds(CategorySpots, spotsIndex.SortedIds),
            };
            if (rods.Count > 0) categories.Add(new ContentCategoryIds(CategoryRods, rodsIndex.SortedIds));
            if (baits.Count > 0) categories.Add(new ContentCategoryIds(CategoryBaits, baitsIndex.SortedIds));
            if (fightProfiles.Count > 0) categories.Add(new ContentCategoryIds(CategoryFightProfiles, profilesIndex.SortedIds));
            if (species.Count > 0) categories.Add(new ContentCategoryIds(CategorySpecies, speciesIndex.SortedIds));
            if (junk.Count > 0) categories.Add(new ContentCategoryIds(CategoryJunk, junkIndex.SortedIds));
            return ContentHash.Compute(categories, dataVersion);
        }

        private void EnsureBuilt()
        {
            if (!built)
            {
                Build();
            }
        }

        // SerializedObject edits (the content builder, the inspector) change the lists without resetting the [NonSerialized] caches.
        private void OnValidate()
        {
            ClearCache();
        }

        private void ClearCache()
        {
            built = false;
            spotsIndex = null;
            spotsById = null;
            rodsIndex = baitsIndex = profilesIndex = speciesIndex = junkIndex = null;
            rodsById = null;
            baitsById = null;
            profilesById = null;
            speciesById = null;
            junkById = null;
            fishing = null;
        }

        private bool HasFishingContent()
        {
            return rods.Count > 0 || baits.Count > 0 || fightProfiles.Count > 0 || species.Count > 0 || junk.Count > 0;
        }

        private bool HasUnlimitedBait()
        {
            for (int i = 0; i < baits.Count; i++)
            {
                if (baits[i] != null && baits[i].Unlimited) return true;
            }
            return false;
        }

        // "<list>[i] is null." / "has invalid id" / "duplicate id" (invalid ids skip the duplicate check). Same wording as spots.
        private static void ValidateIds<T>(string listName, List<T> list, Func<T, string> idOf, List<string> errors)
            where T : UnityEngine.Object
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                T def = list[i];
                if (def == null)
                {
                    errors.Add(listName + "[" + i + "] is null.");
                    continue;
                }

                string id = idOf(def);
                if (!ContentIds.IsValidId(id))
                {
                    errors.Add(listName + "[" + i + "] has invalid id '" + id + "'.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors.Add(listName + "[" + i + "] duplicate id '" + id + "'.");
                }
            }
        }

        // A species needs a fight profile that is one of fightProfiles (reference containment, not just a matching id).
        private void ValidateSpeciesFights(List<string> errors)
        {
            for (int i = 0; i < species.Count; i++)
            {
                FishSpeciesDef def = species[i];
                if (def == null) continue;
                if (def.Fight == null)
                {
                    errors.Add("species[" + i + "] '" + def.Id + "' has no fight profile.");
                    continue;
                }

                bool listed = false;
                for (int p = 0; p < fightProfiles.Count; p++)
                {
                    if (ReferenceEquals(fightProfiles[p], def.Fight))
                    {
                        listed = true;
                        break;
                    }
                }
                if (!listed)
                {
                    errors.Add("species[" + i + "] '" + def.Id + "' fight '" + def.Fight.Id + "' is not listed in fightProfiles.");
                }
            }
        }

        private static Dictionary<string, T> BuildIndex<T>(List<T> list, Func<T, string> idOf, out RegistryIndex index)
        {
            var ids = new List<string>(list.Count);
            var byId = new Dictionary<string, T>(list.Count, StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                string id = idOf(list[i]);
                ids.Add(id);
                byId.Add(id, list[i]);
            }
            index = new RegistryIndex(ids);
            return byId;
        }

        // Arrays in index (ordinal id) order. Species take the index of their fight profile in fightProfiles.
        private FishingDatabase BuildDatabase()
        {
            // Reads the dictionaries directly: this runs inside Build(), before `built` is set, so the *At methods (EnsureBuilt) must not be used.
            var rodStats = new RodStats[rodsIndex.Count];
            for (ushort i = 0; i < rodStats.Length; i++) rodStats[i] = rodsById[rodsIndex.IdAt(i)].ToCore();
            var baitStats = new BaitStats[baitsIndex.Count];
            for (ushort i = 0; i < baitStats.Length; i++) baitStats[i] = baitsById[baitsIndex.IdAt(i)].ToCore();
            var profileStats = new FishFightProfile[profilesIndex.Count];
            for (ushort i = 0; i < profileStats.Length; i++) profileStats[i] = profilesById[profilesIndex.IdAt(i)].ToCore();
            var speciesData = new FishSpeciesData[speciesIndex.Count];
            for (ushort i = 0; i < speciesData.Length; i++)
            {
                FishSpeciesDef def = speciesById[speciesIndex.IdAt(i)];
                speciesData[i] = def.ToCore(profilesIndex.IndexOf(def.Fight.Id));
            }
            var junkData = new JunkData[junkIndex.Count];
            for (ushort i = 0; i < junkData.Length; i++) junkData[i] = junkById[junkIndex.IdAt(i)].ToCore();
            var tuning = fishingTuning.ToCore();
            return new FishingDatabase(rodStats, baitStats, profileStats, speciesData, junkData, in tuning);
        }
    }
}
