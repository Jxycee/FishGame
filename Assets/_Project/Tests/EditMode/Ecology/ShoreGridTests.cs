// Tests/EditMode/Ecology/ShoreGridTests.cs - T5: the ShoreGrid lookup class (spec 12.5). Bits are packed by hand here
// (ShoreGridMath.PackBits belongs to T7).
using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ShoreGridTests
    {
        static byte[] Pack(int n, Func<int, bool> f)
        {
            var bytes = new byte[(n + 7) / 8];
            for (int i = 0; i < n; i++) if (f(i)) bytes[i >> 3] |= (byte)(1 << (i & 7));
            return bytes;
        }

        static ushort[] Dm(int n, ushort v)
        {
            var d = new ushort[n];
            for (int i = 0; i < n; i++) d[i] = v;
            return d;
        }

        [Test]
        public void ShoreGrid_WorldToCell_RoundTripsCellCentres()
        {
            var g = new ShoreGrid(7, 5, 2f, -3f, 4f, Dm(35, 10), Pack(35, i => true), Pack(35, i => true));
            for (int cz = 0; cz < 5; cz++)
            {
                for (int cx = 0; cx < 7; cx++)
                {
                    g.CellCentre(cx, cz, out float x, out float z);
                    Assert.IsTrue(g.TryCell(x, z, out int rx, out int rz), "centre of " + cx + "," + cz);
                    Assert.AreEqual(cx, rx);
                    Assert.AreEqual(cz, rz);
                }
            }
            Assert.IsTrue(g.TryCell(-3f, 4f, out int ox, out int oz));
            Assert.AreEqual(0, ox);
            Assert.AreEqual(0, oz);
            Assert.IsFalse(g.TryCell(-3f + 14f, 5f, out _, out _), "x = origin + width * cell is outside");
            Assert.IsFalse(g.TryCell(-3.001f, 4f, out _, out _));
            Assert.IsFalse(g.TryCell(-3f, 4f + 10f, out _, out _));
            g.CellCentre(0, 0, out float cx0, out float cz0);
            Assert.AreEqual(-2f, cx0, 1e-6f);
            Assert.AreEqual(5f, cz0, 1e-6f);
            Assert.AreEqual(7, g.Width);
            Assert.AreEqual(5, g.Height);
            Assert.AreEqual(2f, g.CellSizeM);
            Assert.AreEqual(-3f, g.OriginX);
            Assert.AreEqual(4f, g.OriginZ);
        }

        [Test]
        public void ShoreGrid_OutsideGrid_NotWaterInfiniteShore()
        {
            ushort[] dm = Dm(4, 10);
            dm[1] = 0xFFFF;
            dm[2] = 37;
            var g = new ShoreGrid(2, 2, 1f, 0f, 0f, dm, Pack(4, i => true), Pack(4, i => true));

            Assert.IsFalse(g.TryCell(-5f, 0.5f, out _, out _));
            Assert.IsFalse(g.IsWater(-5f, 0.5f));
            Assert.IsTrue(float.IsPositiveInfinity(g.ShoreM(-5f, 0.5f)));
            Assert.IsFalse(g.TryCell(float.NaN, 0.5f, out _, out _));
            Assert.IsFalse(g.TryCell(0.5f, float.NaN, out _, out _));
            Assert.IsFalse(g.TryCell(float.PositiveInfinity, 0.5f, out _, out _));
            Assert.IsFalse(g.IsWater(float.NaN, float.NaN));

            Assert.IsFalse(g.IsWater(1.5f, 0.5f), "cell 1 holds 0xFFFF");
            Assert.IsTrue(float.IsPositiveInfinity(g.ShoreM(1.5f, 0.5f)));
            Assert.IsTrue(g.IsWater(0.5f, 1.5f));
            Assert.AreEqual(3.7f, g.ShoreM(0.5f, 1.5f), 1e-6f);
            Assert.AreEqual(1.0f, g.ShoreM(0.5f, 0.5f), 1e-6f);

            Assert.IsFalse(g.IsCastable(-1, 0));
            Assert.IsFalse(g.IsCastable(0, -1));
            Assert.IsFalse(g.IsCastable(2, 0));
            Assert.IsFalse(g.IsReachable(0, 2));
            Assert.IsTrue(g.IsCastable(1, 1));
            Assert.IsTrue(g.IsReachable(0, 0));
        }

        [Test]
        public void ShoreGrid_BitArrayLengthMismatch_Throws()
        {
            Assert.DoesNotThrow(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), Pack(12, i => true), Pack(12, i => true)), "control");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(11, 5), Pack(12, i => true), Pack(12, i => true)), "shoreDm short");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), new byte[1], Pack(12, i => true)), "castable short");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), new byte[3], Pack(12, i => true)), "castable long");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), Pack(12, i => true), new byte[1]), "reachable short");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), Pack(12, i => true), new byte[3]), "reachable long");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(0, 3, 1f, 0f, 0f, new ushort[0], new byte[0], new byte[0]), "width 0");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 0, 1f, 0f, 0f, new ushort[0], new byte[0], new byte[0]), "height 0");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 0f, 0f, 0f, Dm(12, 5), Pack(12, i => true), Pack(12, i => true)), "cell 0");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, float.NaN, 0f, 0f, Dm(12, 5), Pack(12, i => true), Pack(12, i => true)), "cell NaN");
            Assert.Catch<ArgumentException>(() => new ShoreGrid(4, 3, 1f, float.NaN, 0f, Dm(12, 5), Pack(12, i => true), Pack(12, i => true)), "origin NaN");
            Assert.Throws<ArgumentNullException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, null, Pack(12, i => true), Pack(12, i => true)));
            Assert.Throws<ArgumentNullException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), null, Pack(12, i => true)));
            Assert.Throws<ArgumentNullException>(() => new ShoreGrid(4, 3, 1f, 0f, 0f, Dm(12, 5), Pack(12, i => true), null));
        }

        [Test]
        public void ShoreGrid_CollectReachableWaterCells_CastableAndReachableOnly()
        {
            ushort[] dm = Dm(8, 10);
            dm[3] = 0xFFFF;                                                      // cell 3 is not water
            var g = new ShoreGrid(4, 2, 1f, 0f, 0f, dm, Pack(8, i => i <= 5), Pack(8, i => i >= 2));
            var list = new List<int> { 99, 98 };
            g.CollectReachableWaterCells(list);
            CollectionAssert.AreEqual(new[] { 2, 4, 5 }, list);
            Assert.Throws<ArgumentNullException>(() => g.CollectReachableWaterCells(null));
        }

        [Test]
        public void ShoreGrid_GetBit_IsLsbFirst()
        {
            byte[] packed = { 0x01, 0x80, 0x02 };
            Assert.IsTrue(ShoreGrid.GetBit(packed, 0));
            Assert.IsFalse(ShoreGrid.GetBit(packed, 1));
            Assert.IsTrue(ShoreGrid.GetBit(packed, 15));
            Assert.IsFalse(ShoreGrid.GetBit(packed, 14));
            Assert.IsTrue(ShoreGrid.GetBit(packed, 17));
        }
    }
}
