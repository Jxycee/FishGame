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
    }
}
