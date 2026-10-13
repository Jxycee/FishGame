// Core/Hashing/ContentHash.cs: order-independent FNV-1a 64 over registry ids plus a manually bumped data version (spec 4.5).
// Host and clients compare it in the connection payload to detect content mismatch. This file also holds ContentCategoryIds.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public readonly struct ContentCategoryIds
    {
        public readonly string Category;
        public readonly IReadOnlyList<string> Ids;

        public ContentCategoryIds(string category, IReadOnlyList<string> ids)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Ids = ids ?? throw new ArgumentNullException(nameof(ids));
        }
    }

    public static class ContentHash
    {
        public static ulong Compute(IReadOnlyList<ContentCategoryIds> categories, int dataVersion)
        {
            if (categories == null) throw new ArgumentNullException(nameof(categories));

            var sortedCategories = new List<ContentCategoryIds>(categories);
            sortedCategories.Sort((a, b) => StringComparer.Ordinal.Compare(a.Category, b.Category));

            // M3 T2: a repeated category name is a data bug (two lists under one name would hash as two blocks and hide a missing list).
            // Checked after the ordinal sort, so duplicates are adjacent wherever they were in the input; ordinal, so "Species" and "species" differ.
            for (int c = 1; c < sortedCategories.Count; c++)
            {
                if (StringComparer.Ordinal.Equals(sortedCategories[c].Category, sortedCategories[c - 1].Category))
                    throw new ArgumentException("duplicate content category '" + sortedCategories[c].Category + "'", nameof(categories));
            }

            var h = Fnv1a64.Create();
            for (int c = 0; c < sortedCategories.Count; c++)
            {
                ContentCategoryIds category = sortedCategories[c];
                if (category.Ids == null)
                    throw new ArgumentException("ContentCategoryIds at index " + c + " has null Ids (default struct?).", nameof(categories));

                h.Append(category.Category);
                h.Append((byte)0x1F);

                var ids = new List<string>(category.Ids);
                ids.Sort(StringComparer.Ordinal);
                for (int i = 0; i < ids.Count; i++)
                {
                    h.Append(ids[i]);
                    h.Append((byte)0x1E);
                }
            }

            h.Append((byte)0x1D);
            h.AppendInt32LE(dataVersion);
            return h.Value;
        }
    }
}
