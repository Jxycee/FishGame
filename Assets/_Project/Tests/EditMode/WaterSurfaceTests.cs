using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class WaterSurfaceTests
    {
        [Test]
        public void WaterSurface_HeightAt_ReturnsFlatHeightEverywhere()
        {
            Assert.AreEqual(0f, WaterSurface.FlatHeight, 0f);
            Assert.AreEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(0f, 0f), 0f);
            Assert.AreEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(-60f, 60f), 0f);
            Assert.AreEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(12f, -44f), 0f);
        }
    }
}
