using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Host and clients build the same ordinal indices from the same ids,
    // and the ContentHash over those ids goes in the connect payload.
    // M1 holds one category (spots). Later milestones add categories.
    // Changing a SpotDef's id after Build() is not tracked; call Configure or Build again.
    public sealed class ContentRegistry : ScriptableObject
    {
        public const string CategorySpots = "spots";

        [SerializeField] private int dataVersion = 1;
        [SerializeField] private List<SpotDef> spots = new List<SpotDef>();

        // Cache: never serialized. Build() rebuilds it; Configure() clears it.
        [NonSerialized] private RegistryIndex spotsIndex;
        [NonSerialized] private Dictionary<string, SpotDef> spotsById;

        public int DataVersion => dataVersion;

        public IReadOnlyList<SpotDef> Spots => spots;

        // Used by the T15 ContentBuilder and by tests.
        public void Configure(int dataVersion, IEnumerable<SpotDef> spots)
        {
            if (spots == null)
            {
                throw new ArgumentNullException(nameof(spots));
            }

            this.dataVersion = dataVersion;
            this.spots = new List<SpotDef>(spots);
            spotsIndex = null;
            spotsById = null;
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

        public ulong ComputeContentHash()
        {
            EnsureBuilt();
            return ContentHash.Compute(
                new[] { new ContentCategoryIds(CategorySpots, spotsIndex.SortedIds) },
                dataVersion);
        }

        private void EnsureBuilt()
        {
            if (spotsIndex == null)
            {
                Build();
            }
        }
    }
}
