using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class WaterSurfaceTests
    {
        // WaterSurface is static: reset so nothing leaks into the M1 test or PlayMode.
        [TearDown]
        public void TearDown()
        {
            WaterSurface.ResetToFlat();
        }

        [Test]
        public void WaterSurface_HeightAt_ReturnsFlatHeightEverywhere()
        {
            Assert.AreEqual(0f, WaterSurface.FlatHeight, 0f);
            Assert.AreEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(0f, 0f), 0f);
            Assert.AreEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(-60f, 60f), 0f);
            Assert.AreEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(12f, -44f), 0f);
        }

        [Test]
        public void WaterSurface_Configured_FollowsWaveMath()
        {
            WaveSet sample = WaveMathTests.Sample();
            WaterSurface.Configure(sample);
            WaterSurface.SetTimeSource(() => 12.5);
            Assert.AreEqual(12.5, WaterSurface.Now, 0.0);
            float[] xs = { 0f, 3.5f, -17f, 42.25f, -60f };
            float[] zs = { 0f, -8f, 11f, 5.5f, 60f };
            for (int i = 0; i < xs.Length; i++)
            {
                Assert.AreEqual(WaveMath.HeightAt(sample, xs[i], zs[i], 12.5), WaterSurface.HeightAt(xs[i], zs[i]), 0f, "now " + i);
                Assert.AreEqual(WaveMath.HeightAt(sample, xs[i], zs[i], 99.0), WaterSurface.HeightAt(xs[i], zs[i], 99.0), 0f, "t=99 " + i);
            }
            Assert.AreNotEqual(WaterSurface.FlatHeight, WaterSurface.HeightAt(3.5f, -8f, 12.5), "sample waves must actually move the surface");
        }

        [Test]
        public void WaterSurface_ResetToFlat_ReturnsZero()
        {
            int calls = 0;
            WaterSurface.Configure(WaveMathTests.Sample());
            WaterSurface.SetTimeSource(() => { calls++; return 7.0; });
            Assert.AreNotEqual(0f, WaterSurface.HeightAt(3.5f, -8f), "configured surface is not flat");
            Assert.Greater(calls, 0);

            WaterSurface.ResetToFlat();
            calls = 0;
            Assert.AreEqual(0f, WaterSurface.HeightAt(3.5f, -8f), 0f);
            Assert.AreEqual(0f, WaterSurface.HeightAt(-20f, 14f), 0f);
            Assert.AreEqual(0f, WaterSurface.HeightAt(0f, 0f), 0f);
            Assert.AreEqual(0f, WaterSurface.HeightAt(3.5f, -8f, 7.0), 0f);
            Assert.AreNotEqual(7.0, WaterSurface.Now, "the old time source is gone");
            Assert.AreEqual(0, calls, "the counting source is not called after reset");
        }

        // extra: a flat set never reads the clock (swim code calls HeightAt every frame).
        [Test]
        public void WaterSurface_FlatSet_NeverReadsTheClock()
        {
            int calls = 0;
            WaterSurface.SetTimeSource(() => { calls++; return 1.0; });
            Assert.AreEqual(0f, WaterSurface.HeightAt(5f, 5f), 0f);
            Assert.AreEqual(0, calls);
            WaterSurface.Configure(WaveSet.Flat);
            Assert.AreEqual(0f, WaterSurface.HeightAt(5f, 5f), 0f);
            Assert.AreEqual(0, calls);
        }

        // extra: the seam passes the source value through unrounded. The float-cast parity with _WaterTime is the
        // time source's job (WaterTimeDriver, T22), not WaterSurface's.
        [Test]
        public void WaterSurface_TimeSource_PassedThroughUnrounded()
        {
            WaveSet sample = WaveMathTests.Sample();
            const double t = 1234567.891;
            WaterSurface.Configure(sample);
            WaterSurface.SetTimeSource(() => t);
            Assert.AreEqual(t, WaterSurface.Now, 0.0);
            Assert.AreEqual(WaveMath.HeightAt(sample, 2f, 3f, t), WaterSurface.HeightAt(2f, 3f), 0f);
        }
    }
}
