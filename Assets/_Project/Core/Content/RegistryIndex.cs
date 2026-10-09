using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FishGame.Core
{
    // Maps stable string ids to ushort indices for one content category.
    // Indices follow the ordinal sort (StringComparer.Ordinal), so host and clients agree (spec 4.5). Pure C#.
    public sealed class RegistryIndex
    {
        private const int MaxCount = 65535;

        private readonly string[] sortedIds;
        private readonly ReadOnlyCollection<string> sortedIdsView;
        private readonly Dictionary<string, ushort> indexById;

        public RegistryIndex(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                throw new ArgumentException("ids must not be null.", nameof(ids));
            }

            var list = new List<string>(ids);
            if (list.Count > MaxCount)
            {
                throw new ArgumentException("Too many ids: " + list.Count + " (max " + MaxCount + ").", nameof(ids));
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrEmpty(list[i]))
                {
                    throw new ArgumentException("Ids must be non-null and non-empty (position " + i + ").", nameof(ids));
                }
            }

            list.Sort(StringComparer.Ordinal);

            indexById = new Dictionary<string, ushort>(list.Count, StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0 && string.Equals(list[i], list[i - 1], StringComparison.Ordinal))
                {
                    throw new ArgumentException("Duplicate id: " + list[i], nameof(ids));
                }
                indexById.Add(list[i], (ushort)i);
            }

            sortedIds = list.ToArray();
            sortedIdsView = new ReadOnlyCollection<string>(sortedIds);
        }

        public int Count => sortedIds.Length;

        public IReadOnlyList<string> SortedIds => sortedIdsView;

        public ushort IndexOf(string id)
        {
            if (!TryGetIndex(id, out ushort index))
            {
                throw new KeyNotFoundException("Unknown id: " + (id ?? "<null>"));
            }
            return index;
        }

        public bool TryGetIndex(string id, out ushort index)
        {
            if (id == null)
            {
                index = 0;
                return false;
            }
            return indexById.TryGetValue(id, out index);
        }

        public string IdAt(ushort index)
        {
            if (index >= sortedIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "Index " + index + " is outside the registry (count " + sortedIds.Length + ").");
            }
            return sortedIds[index];
        }
    }
}
