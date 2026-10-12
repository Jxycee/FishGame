using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class WaveMathTests
    {
        // Gentle 3-wave set (deg, amplitude m, wavelength m, speed m/s).
        public static WaveSet Sample()
        {
            WaveSet s = default;
            s.W0 = new Wave { DirectionDeg = 30f, Amplitude = 0.05f, Wavelength = 8f, Speed = 1.2f };
            s.W1 = new Wave { DirectionDeg = 160f, Amplitude = 0.03f, Wavelength = 6f, Speed = 0.9f };
            s.W2 = new Wave { DirectionDeg = 275f, Amplitude = 0.02f, Wavelength = 11f, Speed = 1.6f };
            return s;
        }

        static WaveSet Single(float deg, float amp, float len, float speed)
        {
            WaveSet s = default;
            s.W0 = new Wave { DirectionDeg = deg, Amplitude = amp, Wavelength = len, Speed = speed };
            return s;
        }

        [Test]
        public void WaveMath_Height_BoundedBySumOfAmplitudes()
        {
            WaveSet s = Sample();
            float bound = WaveMath.AmplitudeSum(s);
            Assert.AreEqual(0.10f, bound, 1e-6f);
            var rng = new Pcg32(12345);
            for (int i = 0; i < 1000; i++)
            {
                float x = rng.Range(-60f, 60f);
                float z = rng.Range(-60f, 60f);
                double t = rng.Range(0f, 10000f);
                float h = WaveMath.HeightAt(s, x, z, t);
                Assert.LessOrEqual(Math.Abs(h), bound + 1e-5f, "point " + i);
            }
        }

        [Test]
        public void WaveMath_Height_ContinuousInTime()
        {
            WaveSet s = Sample();
            double rate = 0;
            foreach (Wave w in new[] { s.W0, s.W1, s.W2 })
                rate += Math.Abs(w.Amplitude) * (2.0 * Math.PI / w.Wavelength) * Math.Abs(w.Speed);
            const double dt = 1e-3;
            var rng = new Pcg32(777);
            for (int i = 0; i < 200; i++)
            {
                float x = rng.Range(-60f, 60f);
                float z = rng.Range(-60f, 60f);
                double t = i < 100 ? rng.Range(0f, 100f) : 3600.0 + rng.Range(0f, 10f);
                float a = WaveMath.HeightAt(s, x, z, t);
                float b = WaveMath.HeightAt(s, x, z, t + dt);
                Assert.LessOrEqual(Math.Abs(b - a), rate * dt + 1e-5, "point " + i);
            }
        }

        [Test]
        public void WaveMath_SameInputs_Deterministic()
        {
            WaveSet s = Sample();
            var rng = new Pcg32(99);
            for (int i = 0; i < 50; i++)
            {
                float x = rng.Range(-60f, 60f);
                float z = rng.Range(-60f, 60f);
                double t = rng.Range(0f, 10000f);
                float a = WaveMath.HeightAt(s, x, z, t);
                float b = WaveMath.HeightAt(s, x, z, t);
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a), BitConverter.SingleToInt32Bits(b), "point " + i);
            }
        }

        [Test]
        public void WaveMath_Flat_IsZero()
        {
            float[] xs = { 0f, 3.5f, -17f, 42.25f, -60f };
            float[] zs = { 0f, -8f, 11f, 5.5f, 60f };
            double[] ts = { 0.0, 1.5, 123.456, 3600.0 };
            WaveSet noLength = Single(45f, 1f, 0f, 1f);
            WaveSet noAmp = Single(45f, 0f, 5f, 1f);
            WaveSet negLength = Single(45f, 1f, -4f, 1f);
            WaveSet nanLength = Single(45f, 1f, float.NaN, 1f);
            for (int i = 0; i < xs.Length; i++)
            {
                foreach (double t in ts)
                {
                    Assert.AreEqual(0f, WaveMath.HeightAt(WaveSet.Flat, xs[i], zs[i], t), 0f);
                    Assert.AreEqual(0f, WaveMath.HeightAt(noLength, xs[i], zs[i], t), 0f);
                    Assert.AreEqual(0f, WaveMath.HeightAt(noAmp, xs[i], zs[i], t), 0f);
                    Assert.AreEqual(0f, WaveMath.HeightAt(negLength, xs[i], zs[i], t), 0f);
                    Assert.AreEqual(0f, WaveMath.HeightAt(nanLength, xs[i], zs[i], t), 0f);
                }
            }
            Assert.AreEqual(0f, WaveMath.AmplitudeSum(WaveSet.Flat), 0f);
            Assert.AreEqual(0f, WaveMath.AmplitudeSum(noLength), 0f);
            Assert.AreEqual(0f, WaveMath.AmplitudeSum(negLength), 0f);
        }

        // extra: pins the direction convention (0 deg = +Z, dir = (sin, cos) on (x, z)) and travel direction.
        [Test]
        public void WaveMath_Direction_ZeroDegIsPlusZ_AndWaveTravelsForward()
        {
            // L = 4 m so k*d = (pi/2)*d: a crest (sin = 1) sits at d = 1 m when t = 0.
            WaveSet toZ = Single(0f, 1f, 4f, 1f);
            Assert.AreEqual(1f, WaveMath.HeightAt(toZ, 0f, 1f, 0.0), 1e-5f, "0 deg: crest along +Z");
            Assert.AreEqual(0f, WaveMath.HeightAt(toZ, 1f, 0f, 0.0), 1e-5f, "0 deg: x does not matter");
            Assert.AreEqual(1f, WaveMath.HeightAt(toZ, 0f, 2f, 1.0), 1e-5f, "crest moved from z=1 to z=2 after 1 s at 1 m/s");

            WaveSet toX = Single(90f, 1f, 4f, 1f);
            Assert.AreEqual(1f, WaveMath.HeightAt(toX, 1f, 0f, 0.0), 1e-5f, "90 deg: crest along +X");
            Assert.AreEqual(0f, WaveMath.HeightAt(toX, 0f, 1f, 0.0), 1e-5f, "90 deg: z does not matter");

            WaveSet toMinusZ = Single(180f, 1f, 4f, 1f);
            Assert.AreEqual(1f, WaveMath.HeightAt(toMinusZ, 0f, -1f, 0.0), 1e-5f, "180 deg: crest along -Z");

            WaveSet toMinusX = Single(270f, 1f, 4f, 1f);
            Assert.AreEqual(1f, WaveMath.HeightAt(toMinusX, -1f, 0f, 0.0), 1e-5f, "270 deg: crest along -X");
        }

        // extra: phase is computed in double, so long sessions stay accurate.
        [Test]
        public void WaveMath_LargeTime_PhaseComputedInDouble()
        {
            WaveSet s = Single(30f, 1f, 8f, 1.2f);
            double t = 100000.37;
            float x = 3.25f, z = -7.5f;
            double th = 30.0 * Math.PI / 180.0;
            double k = 2.0 * Math.PI / 8.0;
            double phase = k * ((Math.Sin(th) * x + Math.Cos(th) * z) - 1.2f * t);
            float expected = (float)Math.Sin(phase);
            Assert.AreEqual(expected, WaveMath.HeightAt(s, x, z, t), 1e-6f);
        }
    }
}
