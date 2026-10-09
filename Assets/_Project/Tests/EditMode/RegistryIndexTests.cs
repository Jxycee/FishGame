using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RegistryIndexTests
    {
        // Ordinal order compares UTF-16 code units: 'Z' = 90 < '_' = 95 < 'a' = 97 < 'l' = 108.
        // So the expected order is "Zeta", "a_b", "alpha", "lake", "lake_dock".
        // A culture-aware sort orders "alpha" before "Zeta" (case-insensitive first pass), so this fails if the sort is culture-based.
        [Test]
        public void Registry_Index_IsOrdinalSortedAndStable()
        {
            var expected = new[] { "Zeta", "a_b", "alpha", "lake", "lake_dock" };
            var forward = new RegistryIndex(new[] { "lake_dock", "alpha", "Zeta", "a_b", "lake" });
            var reversed = new RegistryIndex(new[] { "lake", "a_b", "Zeta", "alpha", "lake_dock" });

            CollectionAssert.AreEqual(expected, forward.SortedIds);
            CollectionAssert.AreEqual(expected, reversed.SortedIds);
            Assert.AreEqual(5, forward.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual((ushort)i, forward.IndexOf(expected[i]));
                Assert.AreEqual((ushort)i, reversed.IndexOf(expected[i]));
                Assert.AreEqual(expected[i], forward.IdAt((ushort)i));
                Assert.IsTrue(reversed.TryGetIndex(expected[i], out ushort tryIndex));
                Assert.AreEqual((ushort)i, tryIndex);
            }
        }

        [Test]
        public void RegistryIndex_Duplicate_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RegistryIndex(new[] { "lake_dock", "lake_dock" }));
            Assert.Throws<ArgumentException>(() => new RegistryIndex(new[] { "b", "a", "b" }));
        }

        [Test]
        public void RegistryIndex_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RegistryIndex(new[] { "ok", "" }));
            Assert.Throws<ArgumentException>(() => new RegistryIndex(new string[] { null }));
            Assert.Throws<ArgumentException>(() => new RegistryIndex(null));
        }

        [Test]
        public void RegistryIndex_UnknownId_Throws_AndTryGetFalse()
        {
            var index = new RegistryIndex(new[] { "lake_dock" });
            Assert.Throws<KeyNotFoundException>(() => index.IndexOf("missing"));
            Assert.Throws<KeyNotFoundException>(() => index.IndexOf(null));
            Assert.IsFalse(index.TryGetIndex("missing", out _));
            Assert.IsFalse(index.TryGetIndex(null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => index.IdAt((ushort)1));
        }

        [Test]
        public void RegistryIndex_EmptyCollection_HasZeroCount()
        {
            var index = new RegistryIndex(new string[0]);
            Assert.AreEqual(0, index.Count);
            Assert.AreEqual(0, index.SortedIds.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => index.IdAt((ushort)0));
        }

        [Test]
        public void RegistryIndex_CountLimit_Is65535()
        {
            var max = new List<string>(65535);
            for (int i = 0; i < 65535; i++)
            {
                max.Add("id" + i.ToString("D5"));
            }

            var index = new RegistryIndex(max);
            Assert.AreEqual(65535, index.Count);
            Assert.AreEqual("id65534", index.IdAt((ushort)65534));

            max.Add("id65535");
            Assert.Throws<ArgumentException>(() => new RegistryIndex(max));
        }
    }
}
