// Authority: test-only. T22: the scene-level presentation drivers (PaletteGlobals, WaterTimeDriver, RippleField) are observed from outside.
// This assembly does not reference Presentation (C 2.8 #8), so the "Presentation" root is found by name, its components by type name,
// and the effects are read from shader globals, WaterSurface, RenderSettings and the registry's data.
using System;
using System.Collections;
using System.Reflection;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class PresentationWorldTests
    {
        private const string RootName = "Presentation";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        private static Component FindComponent(string typeName)
        {
            GameObject root = GameObject.Find(RootName);
            Assert.That(root, Is.Not.Null, "no GameObject named '" + RootName + "' in the active scene");
            foreach (Component c in root.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName) return c;
            }
            Assert.Fail("the '" + RootName + "' root has no " + typeName);
            return null;
        }

        private static ContentRegistry Registry()
        {
            Assert.That(ServiceRoots.App, Is.Not.Null, "ServiceRoots.App missing");
            Assert.That(ServiceRoots.App.TryGet(out ContentRegistry registry), Is.True, "ContentRegistry missing from the App services");
            return registry;
        }

        // Catches: a driver component missing from the scene root; an array pushed short (palette, ripples, waves); _Waves not matching
        // WaterSettings (wrong packing or index); slot 3 not zeroed; WaterSurface never configured (C# height flat while the GPU is wavy);
        // _WaterTime frozen or on another clock than WaterSurface.Now.
        [UnityTest]
        public IEnumerator PresentationWorld_LakeDock_PushesGlobalsAndConfiguresWaves()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            Assert.That(GameObject.Find(RootName), Is.Not.Null, "a GameObject named 'Presentation' must exist in the active scene");

            Vector4[] palette = Shader.GetGlobalVectorArray(ShaderGlobals.Palette);
            Vector4[] ripples = Shader.GetGlobalVectorArray(ShaderGlobals.Ripples);
            Vector4[] waves = Shader.GetGlobalVectorArray(ShaderGlobals.Waves);
            Assert.That(palette.Length, Is.EqualTo(ShaderGlobals.PaletteLength), "_FGPalette length");
            Assert.That(ripples.Length, Is.EqualTo(RippleMath.Slots), "_Ripples length");
            Assert.That(waves.Length, Is.EqualTo(ShaderGlobals.WavesLength), "_Waves length");

            WaterSettingsDef water = Registry().WaterSettings;
            Assert.That(water, Is.Not.Null, "registry WaterSettings");
            Assert.That(water.DirAmpLenSpeed.Count, Is.GreaterThanOrEqualTo(3), "the seed has three waves");
            for (int i = 0; i < 3; i++)
            {
                Vector4 expected = water.DirAmpLenSpeed[i];
                Assert.That(waves[i].x, Is.EqualTo(expected.x).Within(1e-5f), "_Waves[" + i + "].x (direction)");
                Assert.That(waves[i].y, Is.EqualTo(expected.y).Within(1e-5f), "_Waves[" + i + "].y (amplitude)");
                Assert.That(waves[i].z, Is.EqualTo(expected.z).Within(1e-5f), "_Waves[" + i + "].z (wavelength)");
                Assert.That(waves[i].w, Is.EqualTo(expected.w).Within(1e-5f), "_Waves[" + i + "].w (speed)");
            }
            Assert.That(waves[3], Is.EqualTo(Vector4.zero), "_Waves[3] must be zero");

            // The C# surface is configured with the same set, so HeightAt equals the pure WaveMath value.
            WaveSet set = water.ToWaveSet();
            (float x, float z, double t)[] samples = { (3f, 7f, 1.0), (-12.5f, 4f, 2.75), (20f, -30f, 0.4) };
            bool anyNonZero = false;
            foreach ((float x, float z, double t) s in samples)
            {
                float expected = WaveMath.HeightAt(set, s.x, s.z, s.t);
                float actual = WaterSurface.HeightAt(s.x, s.z, s.t);
                Assert.That(actual, Is.EqualTo(expected).Within(1e-5f), "WaterSurface.HeightAt(" + s.x + ", " + s.z + ", " + s.t + ")");
                if (Mathf.Abs(expected) > 1e-4f) anyNonZero = true;
            }
            Assert.That(anyNonZero, Is.True, "the sample points must hit a non-flat surface, or the equality above proves nothing");

            // _WaterTime advances and tracks WaterSurface.Now.
            float previous = Shader.GetGlobalFloat(ShaderGlobals.WaterTime);
            for (int i = 0; i < 10; i++)
            {
                yield return PlayModeHarness.WaitFrames(1, 0.02f);
            }
            float current = Shader.GetGlobalFloat(ShaderGlobals.WaterTime);
            Assert.That(current, Is.GreaterThan(previous), "_WaterTime must advance over 10 frames");
            Assert.That(current, Is.EqualTo((float)WaterSurface.Now).Within(0.05f), "_WaterTime must track WaterSurface.Now");
        }

        // Catches: the C# time source returning the raw double (session ServerTime) while the GPU gets the float: the two clocks drift
        // apart as the session ages (ruling C-Q4). The float-cast time is exactly representable as a float, a raw ServerTime is not.
        [UnityTest]
        public IEnumerator PresentationWorld_WaterClock_IsFloatCastAndMatchesGlobal()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitFrames(5, 0.1f);

            for (int i = 0; i < 6; i++)
            {
                yield return PlayModeHarness.WaitFrames(1, 0.013f);
                double now = WaterSurface.Now;
                Assert.That(now, Is.EqualTo((double)(float)now), "WaterSurface.Now must be a float-cast value (frame " + i + ")");
                // Same frame: Update has pushed this frame's clock, so the global is the float of Now (within one frame's drift).
                Assert.That(Shader.GetGlobalFloat(ShaderGlobals.WaterTime), Is.EqualTo((float)now).Within(0.05f), "_WaterTime vs Now");
            }
        }

        // Catches: the palette pushed in gamma space (the Toon shader treats the array as linear: ruling F3); the day phase colours or the
        // fog not applied in play; the palette rebuilt every frame (PaletteVectors allocates; carry-forward C 16.2: enable/validate only).
        [UnityTest]
        public IEnumerator PresentationWorld_Palette_IsLinearAndAppliedOnce()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            ArtPaletteDef art = Registry().ArtPalette;
            Assert.That(art, Is.Not.Null, "registry ArtPalette");
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear), "the project is Linear (F3)");

            Vector4[] authored = art.PaletteVectors(art.M2Season);
            Vector4[] pushed = Shader.GetGlobalVectorArray(ShaderGlobals.Palette);
            Assert.That(pushed.Length, Is.EqualTo(ShaderGlobals.PaletteLength), "_FGPalette length");
            bool anyDiffers = false;
            for (int i = 0; i < ShaderGlobals.PaletteLength; i++)
            {
                Color lin = new Color(authored[i].x, authored[i].y, authored[i].z, authored[i].w).linear;
                Assert.That(pushed[i].x, Is.EqualTo(lin.r).Within(1e-4f), "_FGPalette[" + i + "].r (linear)");
                Assert.That(pushed[i].y, Is.EqualTo(lin.g).Within(1e-4f), "_FGPalette[" + i + "].g (linear)");
                Assert.That(pushed[i].z, Is.EqualTo(lin.b).Within(1e-4f), "_FGPalette[" + i + "].b (linear)");
                Assert.That(pushed[i].w, Is.EqualTo(lin.a).Within(1e-4f), "_FGPalette[" + i + "].a");
                if (Mathf.Abs(authored[i].x - lin.r) > 1e-3f) anyDiffers = true;
            }
            Assert.That(anyDiffers, Is.True, "the authored palette must contain non-trivial colours, or the linear check proves nothing");

            DayPhaseAmbient day = art.Phases[1];
            AssertColor(Shader.GetGlobalColor(ShaderGlobals.ToonShade), day.toonShade, "_ToonShade");
            AssertColor(Shader.GetGlobalColor(ShaderGlobals.ToonAmbient), day.toonAmbient, "_ToonAmbient");
            AssertColor(Shader.GetGlobalColor(ShaderGlobals.SkyTop), day.skyTop, "_SkyTop");
            AssertColor(Shader.GetGlobalColor(ShaderGlobals.SkyHorizon), day.skyHorizon, "_SkyHorizon");
            AssertColor(Shader.GetGlobalColor(ShaderGlobals.SkyGround), day.skyGround, "_SkyGround");
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(day.fogDensity).Within(1e-6f), "fog density");
            AssertColor(RenderSettings.fogColor, day.fogColor, "fog colour");

            Component globals = FindComponent("PaletteGlobals");
            PropertyInfo applyCount = globals.GetType().GetProperty("ApplyCount", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(applyCount, Is.Not.Null, "PaletteGlobals.ApplyCount (diagnostic counter)");
            int before = (int)applyCount.GetValue(globals);
            Assert.That(before, Is.GreaterThanOrEqualTo(1), "Apply must have run at enable");
            yield return PlayModeHarness.WaitFrames(10, 0.1f);
            Assert.That((int)applyCount.GetValue(globals), Is.EqualTo(before), "Apply (which allocates via PaletteVectors) must not run per frame");
        }

        // Measured in this Linear project (T23 step 0): Shader.GetGlobalColor and RenderSettings.fogColor read back the AUTHORED (gamma) colour
        // that was set; Unity converts it for the GPU at upload, so the read-back is never the linear form. Asserting only that form catches a
        // double conversion (pushing .linear through SetGlobalColor reads back linear). Pushing the gamma colour through SetGlobalVector reads
        // back identically, so that mutation is not observable from the CPU (the GPU value needs a graphics run).
        private static void AssertColor(Color actual, Color expected, string what)
        {
            Color lin = expected.linear;
            Assert.That(Mathf.Abs(expected.g - lin.g), Is.GreaterThan(1e-2f), what + ": the authored colour must differ from its linear form, or this check proves nothing");
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), what + ".r (read-back is the authored colour, got " + actual + ")");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), what + ".g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), what + ".b");
        }

        // Catches: a short ripple array; a spawn that does not stamp (x, z, water-clock time, strength); a non-round-robin slot choice;
        // non-finite or non-positive strength accepted; slots not cleared when the field is disabled.
        [UnityTest]
        public IEnumerator PresentationWorld_RippleField_SpawnsRoundRobinAndIgnoresBadInput()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            Component field = FindComponent("RippleField");
            MethodInfo spawn = field.GetType().GetMethod("Spawn", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(spawn, Is.Not.Null, "RippleField.Spawn(float, float, float)");

            Vector4[] idle = Shader.GetGlobalVectorArray(ShaderGlobals.Ripples);
            Assert.That(idle.Length, Is.EqualTo(RippleMath.Slots));
            foreach (Vector4 v in idle) Assert.That(v, Is.EqualTo(Vector4.zero), "no ripples at rest");

            spawn.Invoke(field, new object[] { 1f, 2f, 0f });                 // strength 0: ignored
            spawn.Invoke(field, new object[] { 1f, 2f, -1f });                // negative: ignored
            spawn.Invoke(field, new object[] { float.NaN, 2f, 1f });          // non-finite: ignored
            spawn.Invoke(field, new object[] { 1f, float.PositiveInfinity, 1f });
            spawn.Invoke(field, new object[] { 1f, 2f, float.NaN });
            foreach (Vector4 v in Shader.GetGlobalVectorArray(ShaderGlobals.Ripples)) Assert.That(v, Is.EqualTo(Vector4.zero), "bad input must be ignored");

            spawn.Invoke(field, new object[] { 3f, 4f, RippleMath.CastLanding });
            Vector4[] one = Shader.GetGlobalVectorArray(ShaderGlobals.Ripples);
            Assert.That(one.Length, Is.EqualTo(RippleMath.Slots), "the full array is pushed after a spawn");
            Assert.That(one[0].x, Is.EqualTo(3f), "slot 0 x");
            Assert.That(one[0].y, Is.EqualTo(4f), "slot 0 z");
            Assert.That(one[0].w, Is.EqualTo(RippleMath.CastLanding), "slot 0 strength");
            Assert.That(one[0].z, Is.EqualTo((float)WaterSurface.Now).Within(0.05f), "slot 0 stamp is the water clock");
            Assert.That(one[1], Is.EqualTo(Vector4.zero), "slot 1 untouched");

            for (int i = 1; i < RippleMath.Slots; i++) spawn.Invoke(field, new object[] { 10f + i, 20f + i, 0.5f });
            Vector4[] full = Shader.GetGlobalVectorArray(ShaderGlobals.Ripples);
            for (int i = 1; i < RippleMath.Slots; i++)
            {
                Assert.That(full[i].x, Is.EqualTo(10f + i), "slot " + i + " x (round robin order)");
            }

            spawn.Invoke(field, new object[] { 99f, 98f, 0.9f });             // the ninth replaces the oldest: slot 0
            Vector4[] wrapped = Shader.GetGlobalVectorArray(ShaderGlobals.Ripples);
            Assert.That(wrapped[0].x, Is.EqualTo(99f), "the ninth spawn must reuse slot 0");
            Assert.That(wrapped[1].x, Is.EqualTo(11f), "slot 1 keeps its ripple");

            ((Behaviour)field).enabled = false;
            yield return null;
            foreach (Vector4 v in Shader.GetGlobalVectorArray(ShaderGlobals.Ripples)) Assert.That(v, Is.EqualTo(Vector4.zero), "disable clears the slots");
            ((Behaviour)field).enabled = true;
        }

        // Catches (T22 review): an older scene's WaterTimeDriver / RippleField tearing down AFTER a newer one enabled and flattening the new
        // water or wiping the new ripples (additive load or reload: the old objects are destroyed late). Both statics are owner-guarded.
        [UnityTest]
        public IEnumerator PresentationWorld_OlderTeardown_DoesNotWipeNewerDriverOrField()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            Component oldDriver = FindComponent("WaterTimeDriver");
            Component oldField = FindComponent("RippleField");
            var newer = new GameObject("PresentationNewer");
            Component newDriver = newer.AddComponent(oldDriver.GetType());
            Component newField = newer.AddComponent(oldField.GetType());
            yield return null;
            Assert.That(newDriver, Is.Not.Null);

            MethodInfo spawn = newField.GetType().GetMethod("Spawn", BindingFlags.Public | BindingFlags.Instance);
            spawn.Invoke(newField, new object[] { 5f, 6f, RippleMath.Bite });

            UnityEngine.Object.Destroy(oldDriver);
            UnityEngine.Object.Destroy(oldField);
            yield return null;
            yield return null;

            Assert.That(Mathf.Abs(WaterSurface.HeightAt(3f, 7f, 1.0)), Is.GreaterThan(1e-4f), "the older driver's teardown must not flatten the newer driver's water");
            Assert.That(Shader.GetGlobalVectorArray(ShaderGlobals.Waves)[0].y, Is.GreaterThan(0f), "_Waves must stay pushed");
            Vector4 slot = Shader.GetGlobalVectorArray(ShaderGlobals.Ripples)[0];
            Assert.That(slot.x, Is.EqualTo(5f), "the older field's disable must not wipe the newer field's ripples");

            UnityEngine.Object.Destroy(newer);
            yield return null;
        }

        // Catches: WaterTimeDriver leaving the waves configured after the scene unloads (a later scene would rock on stale waves), or
        // leaving the GPU waves non-zero while the C# surface is flat.
        [UnityTest]
        public IEnumerator PresentationWorld_LeaveSession_ResetsWaterToFlat()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(Mathf.Abs(WaterSurface.HeightAt(3f, 7f, 1.0)), Is.GreaterThan(1e-4f), "precondition: the lake is wavy while in the session");

            yield return PlayModeHarness.TearDownApp();

            Assert.That(WaterSurface.HeightAt(3f, 7f, 1.0), Is.EqualTo(0f), "WaterSurface must be flat after the scene unloads");
            Vector4[] waves = Shader.GetGlobalVectorArray(ShaderGlobals.Waves);
            Assert.That(waves.Length, Is.EqualTo(ShaderGlobals.WavesLength), "_Waves stays full length");
            foreach (Vector4 w in waves) Assert.That(w, Is.EqualTo(Vector4.zero), "_Waves must be all zero after the scene unloads");
            Assert.That(Shader.GetGlobalFloat(ShaderGlobals.WaterTime), Is.EqualTo(0f), "_WaterTime reset");
        }
    }
}
