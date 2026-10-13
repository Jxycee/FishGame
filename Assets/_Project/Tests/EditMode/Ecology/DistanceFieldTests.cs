// Tests/EditMode/Ecology/DistanceFieldTests.cs - T7: the spec 12.5 two-pass chamfer distance transform.
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DistanceFieldTests
    {
        const int N = 41, C = 20;

        static float[] SingleLand()
        {
            var land = new bool[N * N];
            land[C * N + C] = true;
            return DistanceField.Compute(land, N, N);
        }

        [Test]
        public void DistanceField_SingleLandCell_WithinChamferErrorOfEuclidean()
        {
            float[] d = SingleLand();
            for (int z = 0; z < N; z++)
            {
                for (int x = 0; x < N; x++)
                {
                    double e = Math.Sqrt((x - C) * (x - C) + (z - C) * (z - C));
                    float v = d[z * N + x];
                    Assert.GreaterOrEqual(v, e - 1e-4, "never below Euclidean at " + x + "," + z);
                    if (e > 0) Assert.LessOrEqual((v - e) / e, 0.083, "relative error at " + x + "," + z);
                }
            }
            Assert.AreEqual(0f, d[C * N + C]);
            Assert.AreEqual(5f + 2f * 0.41421356f, d[22 * N + 25], 1e-4f, "(5, 2) offset is 3 axis + 2 diagonal steps");
        }

        [Test]
        public void DistanceField_AxisDistances_Exact()
        {
            float[] d = SingleLand();
            for (int k = 1; k <= 20; k++)
            {
                Assert.AreEqual((float)k, d[C * N + C + k], 0f, "east " + k);
                Assert.AreEqual((float)k, d[C * N + C - k], 0f, "west " + k);
                Assert.AreEqual((float)k, d[(C - k) * N + C], 0f, "north (z lower) " + k);
                Assert.AreEqual((float)k, d[(C + k) * N + C], 0f, "south (z higher) " + k);
                Assert.AreEqual(k * 1.41421356f, d[(C + k) * N + C + k], 1e-4f, "diagonal " + k);
                Assert.AreEqual(k * 1.41421356f, d[(C - k) * N + C - k], 1e-4f, "diagonal back " + k);
            }
        }

        [Test]
        public void DistanceField_NoLand_IsInfinite()
        {
            var none = new bool[20];
            float[] d = DistanceField.Compute(none, 5, 4);
            Assert.AreEqual(20, d.Length);
            foreach (float v in d) Assert.IsTrue(float.IsPositiveInfinity(v));

            var corner = new bool[20];
            corner[0] = true;
            float[] d2 = DistanceField.Compute(corner, 5, 4);
            Assert.AreEqual(0f, d2[0]);
            Assert.IsFalse(float.IsInfinity(d2[19]), "the far corner is finite");
            Assert.Greater(d2[19], 4f);
        }

        [Test]
        public void DistanceField_SizeMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => DistanceField.Compute(new bool[19], 5, 4));
            Assert.Throws<ArgumentException>(() => DistanceField.Compute(new bool[20], 0, 20));
            Assert.Throws<ArgumentException>(() => DistanceField.Compute(new bool[20], 5, 0));
            Assert.Throws<ArgumentNullException>(() => DistanceField.Compute(null, 5, 4));
            Assert.DoesNotThrow(() => DistanceField.Compute(new bool[20], 5, 4), "control");
        }
    }
}
