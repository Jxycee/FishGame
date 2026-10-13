using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ContentHashTests
    {
        [Test]
        public void ContentHash_InputOrder_DoesNotChangeHash()
        {
            var forward = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("spot", new[] { "lake_dock", "pond" }),
                new ContentCategoryIds("rod", new[] { "rod_handmade" })
            };
            var reversed = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("rod", new[] { "rod_handmade" }),
                new ContentCategoryIds("spot", new[] { "pond", "lake_dock" })
            };
            Assert.That(ContentHash.Compute(reversed, 1), Is.EqualTo(ContentHash.Compute(forward, 1)));
        }

        [Test]
        public void ContentHash_DataVersionChange_ChangesHash()
        {
            var cats = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("spot", new[] { "lake_dock" })
            };
            Assert.That(ContentHash.Compute(cats, 2), Is.Not.EqualTo(ContentHash.Compute(cats, 1)));
        }

        [Test]
        public void ContentHash_IdChange_ChangesHash()
        {
            var a = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("spot", new[] { "pond" })
            };
            var b = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("spot", new[] { "pond2" })
            };
            Assert.That(ContentHash.Compute(b, 1), Is.Not.EqualTo(ContentHash.Compute(a, 1)));
        }

        [Test]
        public void ContentHash_IdMovedBetweenCategories_ChangesHash()
        {
            // Moving id "y" from "spot" to "rod" must change the hash (the 0x1F / 0x1E separators mark category boundaries).
            var a = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("rod", new[] { "x" }),
                new ContentCategoryIds("spot", new[] { "y" })
            };
            var b = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("rod", new[] { "x", "y" }),
                new ContentCategoryIds("spot", new string[0])
            };
            Assert.That(ContentHash.Compute(b, 1), Is.Not.EqualTo(ContentHash.Compute(a, 1)));
        }

        // Catches: no duplicate check at all; a check that compares only neighbours in the INPUT order (before sorting), which misses these
        // non-adjacent duplicates; a case-insensitive compare, which would reject the distinct ordinal names "Species" and "species".
        [Test]
        public void ContentHash_DuplicateCategory_Throws()
        {
            var dup = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("species", new[] { "a" }),
                new ContentCategoryIds("rods", new[] { "r" }),
                new ContentCategoryIds("species", new[] { "b" })
            };
            var ex = Assert.Throws<System.ArgumentException>(() => ContentHash.Compute(dup, 1));
            Assert.That(ex.Message, Does.Contain("duplicate content category 'species'"));

            var caseDistinct = new List<ContentCategoryIds>
            {
                new ContentCategoryIds("Species", new[] { "a" }),
                new ContentCategoryIds("species", new[] { "a" })
            };
            Assert.DoesNotThrow(() => ContentHash.Compute(caseDistinct, 1), "category names compare ordinally");
        }
    }
}
