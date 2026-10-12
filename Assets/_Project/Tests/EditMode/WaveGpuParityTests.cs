// GPU parity of the water wave term (F1, C-Q4, C 16.2): runs the REAL FG_WaveHeight (Shaders/FishGameCommon.hlsl, through Compute/WaveParity.compute)
// and compares it with FishGame.Core.WaveMath.HeightAt. The source-text pin in RenderingSetupTests only proves what the HLSL says; this proves what the GPU does.
// Needs a graphics device, so it is Ignored under -nographics (GraphicsDeviceType.Null, the normal test runs). Run it once in a graphics batchmode run:
//   Unity.exe -batchmode -projectPath <repo> -runTests -testPlatform EditMode -testFilter WaveGpuParity -testResults <xml> -logFile <log>   (no -nographics)
// and read the "FG GPU PARITY maxErr=..." log line.
// Catches: a swapped direction convention (dir = (sin, cos), 0 deg = +Z), a wrong k or speed term, a dropped or fast-math-folded non-finite guard
// (NaN / Infinity fields must give a zero term on the GPU exactly as WaveMath gives), and float-cast time drift at a long-session t (about 3600 s).
using System;
using System.Collections.Generic;
using System.Text;
using FishGame.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Tests.EditMode
{
    public class WaveGpuParityTests
    {
        const string ComputePath = "Assets/_Project/Tests/EditMode/Compute/WaveParity.compute";
        const float Tolerance = 1e-4f;
        const int SampleCount = 240;

        // Not 45 degrees, not symmetric in x/z: a swapped sin/cos or a mirrored axis changes the height at almost every sample.
        static readonly Vector4 ValidA = new Vector4(20f, 0.12f, 8.0f, 1.3f);       // (directionDeg, amplitude, wavelength, speed)
        static readonly Vector4 ValidB = new Vector4(137f, 0.06f, 3.1f, 0.9f);
        static readonly Vector4 ValidC = new Vector4(-71f, 0.03f, 1.7f, 1.6f);

        static readonly float[] Times = { 0f, 1.7f, 12.5f, 123.456f, 3600f, 3600.37f };

        struct Case
        {
            public string Name;
            public Vector4 W0, W1, W2;
        }

        [Test]
        public void WaveGpuParity_FgWaveHeightMatchesWaveMath()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("WaveGpuParity needs a graphics device (this is a -nographics run); run it in a graphics batchmode run, see the file header.");
            Assert.That(SystemInfo.supportsComputeShaders, Is.True, "compute shaders unsupported on " + SystemInfo.graphicsDeviceType);

            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            Assert.That(shader, Is.Not.Null, "missing " + ComputePath);
            AssertComputeCompiled(shader);
            int kernel = shader.FindKernel("CSMain");

            Vector3[] samples = BuildSamples();
            List<Case> cases = BuildCases();

            var sampleBuffer = new ComputeBuffer(samples.Length, sizeof(float) * 3);
            var heightBuffer = new ComputeBuffer(samples.Length, sizeof(float));
            var echoBuffer = new ComputeBuffer(4, sizeof(float) * 4);
            try
            {
                sampleBuffer.SetData(samples);
                var heights = new float[samples.Length];
                var echo = new Vector4[4];
                float maxErr = 0f, maxErrLongT = 0f, maxErrShortT = 0f;
                int compared = 0, nonZeroExpected = 0;

                foreach (Case c in cases)
                {
                    var waves = new[] { c.W0, c.W1, c.W2, Vector4.zero };
                    shader.SetVectorArray("_Waves", waves);
                    shader.SetInt("_Count", samples.Length);
                    shader.SetBuffer(kernel, "_Samples", sampleBuffer);
                    shader.SetBuffer(kernel, "_Heights", heightBuffer);
                    shader.SetBuffer(kernel, "_WavesEcho", echoBuffer);
                    shader.Dispatch(kernel, (samples.Length + 63) / 64, 1, 1);
                    heightBuffer.GetData(heights);
                    echoBuffer.GetData(echo);

                    // The bad value must have reached the shader: otherwise a NaN / Infinity case would test nothing.
                    for (int i = 0; i < 3; i++) AssertSameBits(waves[i], echo[i], c.Name + " echo of _Waves[" + i + "]");

                    var set = new WaveSet { W0 = ToWave(c.W0), W1 = ToWave(c.W1), W2 = ToWave(c.W2) };
                    for (int i = 0; i < samples.Length; i++)
                    {
                        Vector3 s = samples[i];
                        float cpu = WaveMath.HeightAt(in set, s.x, s.y, (double)s.z);   // t is already the float the GPU receives
                        float gpu = heights[i];
                        Assert.That(float.IsNaN(gpu) || float.IsInfinity(gpu), Is.False,
                            c.Name + ": GPU height is non-finite (" + gpu + ") at x=" + s.x + " z=" + s.y + " t=" + s.z + " (cpu " + cpu + ")");
                        float err = Mathf.Abs(gpu - cpu);
                        if (Mathf.Abs(cpu) > 1e-3f) nonZeroExpected++;
                        compared++;
                        if (err > maxErr) maxErr = err;
                        if (s.z >= 3600f) { if (err > maxErrLongT) maxErrLongT = err; }
                        else if (err > maxErrShortT) maxErrShortT = err;
                        Assert.That(err, Is.LessThanOrEqualTo(Tolerance),
                            c.Name + ": |gpu - cpu| = " + err + " at x=" + s.x + " z=" + s.y + " t=" + s.z + " (gpu " + gpu + ", cpu " + cpu + ")");
                    }
                }

                Assert.That(nonZeroExpected, Is.GreaterThan(compared / 2), "the cases must mostly expect a non-zero height, otherwise the comparison is vacuous");
                Debug.Log("FG GPU PARITY maxErr=" + maxErr.ToString("E3") + " maxErr(t<100)=" + maxErrShortT.ToString("E3") +
                          " maxErr(t>=3600)=" + maxErrLongT.ToString("E3") + " cases=" + cases.Count + " samples=" + compared +
                          " device=" + SystemInfo.graphicsDeviceType + " tolerance=" + Tolerance.ToString("E1"));
            }
            finally
            {
                sampleBuffer.Release();
                heightBuffer.Release();
                echoBuffer.Release();
            }
        }

        static void AssertComputeCompiled(ComputeShader shader)
        {
            ShaderMessage[] messages = ShaderUtil.GetComputeShaderMessages(shader);
            var errors = new StringBuilder();
            if (messages != null)
                foreach (ShaderMessage m in messages)
                    if (m.severity == ShaderCompilerMessageSeverity.Error) errors.AppendLine(m.message + " (" + m.file + ":" + m.line + ")");
            Assert.That(errors.ToString(), Is.Empty, "WaveParity.compute failed to compile");
        }

        static Wave ToWave(Vector4 v)
        {
            return new Wave { DirectionDeg = v.x, Amplitude = v.y, Wavelength = v.z, Speed = v.w };
        }

        static void AssertSameBits(Vector4 expected, Vector4 actual, string label)
        {
            for (int i = 0; i < 4; i++)
            {
                float e = expected[i], a = actual[i];
                if (float.IsNaN(e)) Assert.That(float.IsNaN(a), Is.True, label + "[" + i + "]: NaN did not reach the shader (got " + a + ")");
                else Assert.That(BitConverter.SingleToInt32Bits(a), Is.EqualTo(BitConverter.SingleToInt32Bits(e)), label + "[" + i + "] (" + e + " vs " + a + ")");
            }
        }

        // (x, z, t) with x != z and a deterministic spread over the lake, cycling through the time list (the last two are the long-session float times).
        static Vector3[] BuildSamples()
        {
            var samples = new Vector3[SampleCount];
            uint state = 0x1234567u;
            for (int i = 0; i < SampleCount; i++)
            {
                state = state * 1664525u + 1013904223u;
                float x = (state >> 8) / (float)(1 << 24) * 120f - 60f;
                state = state * 1664525u + 1013904223u;
                float z = (state >> 8) / (float)(1 << 24) * 120f - 60f;
                samples[i] = new Vector3(x, z, Times[i % Times.Length]);
            }
            return samples;
        }

        static List<Case> BuildCases()
        {
            var cases = new List<Case>
            {
                new Case { Name = "valid", W0 = ValidA, W1 = ValidB, W2 = ValidC },
                new Case { Name = "valid-A-only", W0 = ValidA, W1 = Vector4.zero, W2 = Vector4.zero },
                new Case { Name = "valid-C-only", W0 = Vector4.zero, W1 = Vector4.zero, W2 = ValidC },
                new Case { Name = "amplitude0+negativeWavelength", W0 = new Vector4(20f, 0f, 8f, 1.3f), W1 = new Vector4(137f, 0.06f, -3.1f, 0.9f), W2 = ValidC },
            };

            float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            string[] badNames = { "NaN", "+Inf", "-Inf" };
            string[] fields = { "dir", "amp", "len", "speed" };
            Vector4[] valid = { ValidA, ValidB, ValidC };
            for (int slot = 0; slot < 3; slot++)
                for (int field = 0; field < 4; field++)
                    for (int b = 0; b < bad.Length; b++)
                    {
                        var waves = (Vector4[])valid.Clone();
                        Vector4 w = waves[slot];
                        w[field] = bad[b];
                        waves[slot] = w;
                        cases.Add(new Case { Name = "slot" + slot + "." + fields[field] + "=" + badNames[b], W0 = waves[0], W1 = waves[1], W2 = waves[2] });
                    }
            return cases;
        }
    }
}
