using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ContentIdsTests
    {
        [Test]
        public void ContentIds_IsValidId_AcceptsSnakeCaseOnly()
        {
            Assert.IsTrue(ContentIds.IsValidId(ContentIds.LakeDock));
            Assert.IsTrue(ContentIds.IsValidId("lake_dock"));
            Assert.IsTrue(ContentIds.IsValidId("rod2"));
            Assert.IsTrue(ContentIds.IsValidId("a_1_b"));
            Assert.IsFalse(ContentIds.IsValidId(""));
            Assert.IsFalse(ContentIds.IsValidId(null));
            Assert.IsFalse(ContentIds.IsValidId("Lake_dock"));
            Assert.IsFalse(ContentIds.IsValidId("_lake"));
            Assert.IsFalse(ContentIds.IsValidId("lake_"));
            Assert.IsFalse(ContentIds.IsValidId("lake__dock"));
            Assert.IsFalse(ContentIds.IsValidId("lake dock"));
            Assert.IsFalse(ContentIds.IsValidId("lake-dock"));
        }
    }
}
