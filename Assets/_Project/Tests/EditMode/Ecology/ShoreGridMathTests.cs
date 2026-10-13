// Tests/EditMode/Ecology/ShoreGridMathTests.cs - T7: shore decimetres, reachability ring, per-sub-area counts and bit packing (spec 12.5).
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ShoreGridMathTests
    {
        [Test]
        public void ShoreGridMath_ShoreDecimetres_NotWaterIsFFFFAndHalfCellOffset()
        {
            // 5x1: land at x 0, water at x 1-3, x 4 is neither (void).
            bool[] land = { true, false, false, false, false };
            bool[] water = { false, true, true, true, false };
            Assert.AreEqual(new ushort[] { 0xFFFF, 5, 15, 25, 0xFFFF }, ShoreGridMath.ShoreDecimetres(water, land, 5, 1, 1f));
            Assert.AreEqual(new ushort[] { 0xFFFF, 10, 30, 50, 0xFFFF }, ShoreGridMath.ShoreDecimetres(water, land, 5, 1, 2f));

            // All water and no land anywhere: ShoreDmMax, never the not-water marker.
            var allWater = new bool[6];
            for (int i = 0; i < allWater.Length; i++) allWater[i] = true;
            ushort[] noLand = ShoreGridMath.ShoreDecimetres(allWater, new bool[6], 3, 2, 1f);
            foreach (ushort v in noLand) Assert.AreEqual((ushort)0xFFFE, v);

            Assert.Throws<ArgumentNullException>(() => ShoreGridMath.ShoreDecimetres(null, land, 5, 1, 1f));
            Assert.Throws<ArgumentException>(() => ShoreGridMath.ShoreDecimetres(new bool[4], land, 5, 1, 1f));
            Assert.Throws<ArgumentException>(() => ShoreGridMath.ShoreDecimetres(water, land, 5, 1, 0f));
        }

        const int N = 41;   // origin -20.5: cell 20 is centred on (0, 0)

        static bool[] AllTrue(int n)
        {
            var a = new bool[n];
            for (int i = 0; i < n; i++) a[i] = true;
            return a;
        }

        static bool Reach(byte[] bits, int cx, int cz) => ShoreGrid.GetBit(bits, cz * N + cx);

        [Test]
        public void ShoreGridMath_ReachableBits_RingFromStandPoints()
        {
            byte[] castable = ShoreGridMath.PackBits(AllTrue(N * N));
            byte[] bits = ShoreGridMath.ReachableBits(N, N, 1f, -20.5f, -20.5f, castable, new[] { 0f, 0f }, 4.9f, 16.4f);

            Assert.IsTrue(Reach(bits, 25, 20), "dx 5 is inside the ring");
            Assert.IsFalse(Reach(bits, 24, 20), "dx 4 is inside the dead zone");
            Assert.IsTrue(Reach(bits, 36, 20), "dx 16");
            Assert.IsFalse(Reach(bits, 37, 20), "dx 17");
            Assert.IsTrue(Reach(bits, 32, 31), "(12, 11) is 16.28 m");
            Assert.IsFalse(Reach(bits, 32, 32), "(12, 12) is 16.97 m");
            Assert.IsFalse(Reach(bits, 20, 20), "the stand cell itself");

            // A non-castable cell is never reachable.
            var notCastable = AllTrue(N * N);
            notCastable[20 * N + 25] = false;
            byte[] bits2 = ShoreGridMath.ReachableBits(N, N, 1f, -20.5f, -20.5f, ShoreGridMath.PackBits(notCastable), new[] { 0f, 0f }, 4.9f, 16.4f);
            Assert.IsFalse(Reach(bits2, 25, 20), "reachable needs castable");
            Assert.IsTrue(Reach(bits2, 26, 20));

            // A second stand at (10, 0) adds the cells only it reaches: the union.
            byte[] bits3 = ShoreGridMath.ReachableBits(N, N, 1f, -20.5f, -20.5f, castable, new[] { 0f, 0f, 10f, 0f }, 4.9f, 16.4f);
            Assert.IsTrue(Reach(bits3, 20 + 20, 20 + 5), "(20, 5): 20.6 m from stand 1, 11.2 m from stand 2");
            Assert.IsFalse(Reach(bits, 20 + 20, 20 + 5), "(20, 5) is out of reach of stand 1 alone");
            Assert.IsTrue(Reach(bits3, 20 + 16, 20), "(16, 0): 16 m from stand 1, 6 m from stand 2");
            for (int i = 0; i < N * N; i++)
                if (ShoreGrid.GetBit(bits, i)) Assert.IsTrue(ShoreGrid.GetBit(bits3, i), "the union keeps every stand-1 cell, cell " + i);

            Assert.Throws<ArgumentNullException>(() => ShoreGridMath.ReachableBits(N, N, 1f, 0f, 0f, null, new[] { 0f, 0f }, 4.9f, 16.4f));
            Assert.Throws<ArgumentException>(() => ShoreGridMath.ReachableBits(N, N, 1f, 0f, 0f, castable, new[] { 0f }, 4.9f, 16.4f));
            Assert.Throws<ArgumentException>(() => ShoreGridMath.ReachableBits(N, N, 1f, 0f, 0f, new byte[3], new[] { 0f, 0f }, 4.9f, 16.4f));
        }

        [Test]
        public void ShoreGridMath_CountReachablePerSubArea_KnownGrid()
        {
            ushort[] dm = { 0xFFFF, 5, 15, 25, 35, 45, 55, 65, 105, 125 };
            var castable = new bool[10];
            var reachable = new bool[10];
            for (int i = 1; i < 10; i++) { castable[i] = true; reachable[i] = i != 9; }
            var grid = new ShoreGrid(10, 1, 1f, 0f, 0f, dm, ShoreGridMath.PackBits(castable), ShoreGridMath.PackBits(reachable));

            var bands = new[]
            {
                new SubAreaBandData { MinShoreM = 0f, MaxShoreM = 3f, JunkChance = 0.1f },
                new SubAreaBandData { MinShoreM = 3f, MaxShoreM = 6f, JunkChance = 0.1f },
                new SubAreaBandData { MinShoreM = 6f, MaxShoreM = float.PositiveInfinity, JunkChance = 0.1f },
            };
            WeatherTableData w = EcologyCoreTestData.Weather();
            var zone = new ZoneData(bands, EcologyCoreTestData.LakeRules(), EcologyCoreTestData.LakeJunk(), in w, grid);
            Assert.AreEqual(new[] { 3, 3, 2 }, ShoreGridMath.CountReachablePerSubArea(grid, zone));
            Assert.Throws<ArgumentNullException>(() => ShoreGridMath.CountReachablePerSubArea(null, zone));
            Assert.Throws<ArgumentNullException>(() => ShoreGridMath.CountReachablePerSubArea(grid, null));
        }

        [Test]
        public void ShoreGridMath_PackBits_RoundTrip()
        {
            foreach (int n in new[] { 0, 1, 7, 8, 9, 14400 })
            {
                var bits = new bool[n];
                for (int i = 0; i < n; i++) bits[i] = i % 3 == 0;
                byte[] packed = ShoreGridMath.PackBits(bits);
                Assert.AreEqual((n + 7) / 8, packed.Length, "length for " + n);
                for (int i = 0; i < n; i++) Assert.AreEqual(bits[i], ShoreGrid.GetBit(packed, i), "bit " + i + " of " + n);
            }
            Assert.AreEqual(new byte[] { 0xFF, 0x01 }, ShoreGridMath.PackBits(AllTrue(9)));
            Assert.Throws<ArgumentNullException>(() => ShoreGridMath.PackBits(null));
        }
    }
}
