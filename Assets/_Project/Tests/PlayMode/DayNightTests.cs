// Authority: test-only. T18 (section F plan): the DayNightController look and the season push, driven through the host's DevWorldDriver
// (Gameplay, reachable) and observed from outside: the scene root "Sun", shader globals, RenderSettings and PaletteGlobals.ApplyCount.
// This assembly does not reference Presentation (C 2.8 #8), so components are found by type name.
using System;
using System.Collections;
using System.Reflection;
using FishGame.Core;
using FishGame.Data;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class DayNightTests
    {
        private const string RootName = "Presentation";
        private const string FogProperty = "RenderSettings.fogDensity";
        private static readonly int DayFractionId = Shader.PropertyToID(ShaderGlobals.DayFraction);

        private DevWorldDriver _driver;
        private Light _sun;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
            EcologySession.Clear();   // DevSetSeason writes the session-level run state: never leak a test season into the next test
        }

        // Arrange (all three tests): solo on the lake, world state present, clock stopped, Clear weather with its fade finished.
        private IEnumerator Arrange()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10f,
                                                   "WorldState.Current did not spawn with state");
            _driver = WorldState.Current.GetComponent<DevWorldDriver>();
            Assert.That(_driver, Is.Not.Null, "the World root has a DevWorldDriver");
            _driver.DevSetClockRunning(false);
            _driver.DevSetWeatherOverride(WeatherMask.Clear);
            yield return PlayModeHarness.WaitFrames(3, 4.5f);   // any fade from the rolled weather has finished
            GameObject sunGo = GameObject.Find("Sun");
            Assert.That(sunGo, Is.Not.Null, "scene root 'Sun'");
            _sun = sunGo.GetComponent<Light>();
            Assert.That(_sun, Is.Not.Null, "Sun has a Light");
        }

        private static ArtPaletteDef Art()
        {
            Assert.That(ServiceRoots.App, Is.Not.Null, "ServiceRoots.App missing");
            Assert.That(ServiceRoots.App.TryGet(out ContentRegistry registry), Is.True, "ContentRegistry missing");
            Assert.That(registry.ArtPalette, Is.Not.Null, "registry ArtPalette");
            return registry.ArtPalette;
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

        private static int ApplyCount(Component globals)
        {
            PropertyInfo p = globals.GetType().GetProperty("ApplyCount", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(p, Is.Not.Null, "PaletteGlobals.ApplyCount (diagnostic counter)");
            return (int)p.GetValue(globals);
        }

        // ---- data-derived step bounds (scrub test) ----

        // Largest per-channel difference between two ADJACENT phases (the day never wraps inside one scrub).
        private static float MaxAdjacent(ArtPaletteDef art, Func<DayPhaseAmbient, Color> pick)
        {
            float max = 0f;
            for (int i = 1; i < art.Phases.Count; i++)
            {
                Color a = pick(art.Phases[i - 1]), b = pick(art.Phases[i]);
                max = Mathf.Max(max, Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
            }
            return max;
        }

        private static float MaxAdjacent(ArtPaletteDef art, Func<DayPhaseAmbient, float> pick)
        {
            float max = 0f;
            for (int i = 1; i < art.Phases.Count; i++) max = Mathf.Max(max, Mathf.Abs(pick(art.Phases[i]) - pick(art.Phases[i - 1])));
            return max;
        }

        private static float StepChannels(Color a, Color b)
        {
            return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
        }

        private static float ChannelMax(Color c)
        {
            return Mathf.Max(c.r, c.g, c.b);
        }

        // Catches: using phases[b.To] without lerping by b.T (a pop at every phase start); ignoring the sun/moon weights (full intensity on both
        // sides of the flip); LookRotation(+dir) (forward points up); a raw colour step that is visible (non-zero intensity at the flip).
        // Blending on the int minute is invisible here (the test steps whole minutes): a review item.
        [UnityTest]
        public IEnumerator DayNight_ScrubFullDay_NoLightOrFogPops()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return Arrange();
            ArtPaletteDef art = Art();
            SunMoonSettings sm = art.SunMoon;

            // smoothstep peaks at slope 1.5 per window: per 1-minute step a phase field moves at most 1.5 / (window x 1200) of its adjacent delta.
            const float slack = 1.2f, eps = 1e-4f;
            float phaseStep = 1.5f / (sm.blendWindow * DayClock.DayLengthMinutes);
            float weightStep = 1.5f / (sm.flipWindow * DayClock.DayLengthMinutes);
            float fogB = phaseStep * MaxAdjacent(art, p => p.fogDensity) * slack + eps;
            float fogColB = phaseStep * MaxAdjacent(art, p => p.fogColor) * slack + eps;
            float shadeB = phaseStep * MaxAdjacent(art, p => p.toonShade) * slack + eps;
            float ambB = phaseStep * MaxAdjacent(art, p => p.toonAmbient) * slack + eps;
            float topB = phaseStep * MaxAdjacent(art, p => p.skyTop) * slack + eps;
            float groundB = phaseStep * MaxAdjacent(art, p => p.skyGround) * slack + eps;
            float lightColB = phaseStep * MaxAdjacent(art, p => p.lightColor) * slack + eps;

            float maxIntensity = sm.moonIntensity, maxColor = ChannelMax(sm.moonTint);
            for (int i = 0; i < art.Phases.Count; i++)
            {
                maxIntensity = Mathf.Max(maxIntensity, art.Phases[i].lightIntensity);
                maxColor = Mathf.Max(maxColor, ChannelMax(art.Phases[i].lightColor));
            }
            float intensityB = (weightStep * maxIntensity + phaseStep * MaxAdjacent(art, p => p.lightIntensity)) * slack + eps;
            // d(c x i) <= dc x maxI + maxC x di (the raw colour jump at the flip is covered by di: the intensity before it is 0).
            float effB = (lightColB * maxIntensity + maxColor * intensityB) * slack + eps;
            const float invisibleIntensity = 0.005f;   // below this a raw colour step cannot be seen (about 1.3e-3 at the first minute of the moon)

            Color prevEff = default, prevRaw = default, prevShade = default, prevAmb = default, prevTop = default, prevGround = default, prevFogCol = default;
            float prevIntensity = 0f, prevFog = 0f;
            int rawJumps = 0;
            float flipMinute = Mathf.Round(sm.sunsetFraction * DayClock.DayLengthMinutes);

            for (int m = 0; m < 1200; m++)
            {
                _driver.DevSetMinute(m);
                yield return PlayModeHarness.WaitFrames(1, 0f);

                Color raw = _sun.color;
                float intensity = _sun.intensity;
                Color eff = new Color(raw.r * intensity, raw.g * intensity, raw.b * intensity, 1f);
                Color shade = Shader.GetGlobalColor(ShaderGlobals.ToonShade);
                Color amb = Shader.GetGlobalColor(ShaderGlobals.ToonAmbient);
                Color top = Shader.GetGlobalColor(ShaderGlobals.SkyTop);
                Color ground = Shader.GetGlobalColor(ShaderGlobals.SkyGround);
                Color fogCol = RenderSettings.fogColor;
                float fog = RenderSettings.fogDensity;

                Assert.That(_sun.enabled, Is.True, "the light is never disabled (m " + m + ")");
                Assert.That(Shader.GetGlobalFloat(DayFractionId), Is.EqualTo(m / 1200f).Within(1e-4f), "_FGDayFraction at m " + m);

                if (m > 0)
                {
                    Assert.That(Mathf.Abs(intensity - prevIntensity), Is.LessThanOrEqualTo(intensityB), "sun.intensity step at m " + m);
                    Assert.That(StepChannels(eff, prevEff), Is.LessThanOrEqualTo(effB), "effective light (colour x intensity) step at m " + m);
                    Assert.That(Mathf.Abs(fog - prevFog), Is.LessThanOrEqualTo(fogB), FogProperty + " step at m " + m);
                    Assert.That(StepChannels(fogCol, prevFogCol), Is.LessThanOrEqualTo(fogColB), "fogColor step at m " + m);
                    Assert.That(StepChannels(shade, prevShade), Is.LessThanOrEqualTo(shadeB), "_ToonShade step at m " + m);
                    Assert.That(StepChannels(amb, prevAmb), Is.LessThanOrEqualTo(ambB), "_ToonAmbient step at m " + m);
                    Assert.That(StepChannels(top, prevTop), Is.LessThanOrEqualTo(topB), "_SkyTop step at m " + m);
                    Assert.That(StepChannels(ground, prevGround), Is.LessThanOrEqualTo(groundB), "_SkyGround step at m " + m);

                    // The raw sun.color is NOT bounded: it switches from the dusk colour to moonTint in one step at the flip. That is invisible
                    // only because the intensity there is ~0, so every raw step above the phase bound must sit at ~0 intensity.
                    if (StepChannels(raw, prevRaw) > lightColB)
                    {
                        rawJumps++;
                        Assert.That(intensity, Is.LessThanOrEqualTo(invisibleIntensity), "raw light colour jumped at m " + m + " with a visible intensity");
                        Assert.That(prevIntensity, Is.LessThanOrEqualTo(invisibleIntensity), "raw light colour jumped at m " + m + " from a visible intensity");
                    }
                }

                if (m == 0 || m == (int)flipMinute)
                    Assert.That(intensity, Is.LessThanOrEqualTo(1e-3f), "the sun is dark at m " + m + " (both lights are 0 at the flips)");

                if (m == 420)   // noon: dayFraction 0.35, sun at its peak
                {
                    DayNightMath.SunDirection(0.35f, sm.sunriseFraction, sm.sunsetFraction, sm.minElevationDeg, out float x, out float y, out float z);
                    Vector3 fwd = _sun.transform.forward;
                    Assert.That(fwd.x, Is.EqualTo(-x).Within(1e-3f), "noon forward.x = -toSun.x");
                    Assert.That(fwd.y, Is.EqualTo(-y).Within(1e-3f), "noon forward.y = -toSun.y");
                    Assert.That(fwd.z, Is.EqualTo(-z).Within(1e-3f), "noon forward.z = -toSun.z");
                    Assert.That(fwd.y, Is.LessThan(-0.8f), "the noon sun shines downwards");
                }
                if (m == 1000)   // night: the fixed moon direction
                {
                    float angle = Quaternion.Angle(_sun.transform.rotation, Quaternion.Euler(sm.moonEulerDeg));
                    Assert.That(angle, Is.LessThan(0.1f), "the moon rotation at night");
                }

                prevRaw = raw; prevIntensity = intensity; prevEff = eff; prevShade = shade; prevAmb = amb;
                prevTop = top; prevGround = ground; prevFogCol = fogCol; prevFog = fog;
            }

            Assert.That(rawJumps, Is.GreaterThanOrEqualTo(1), "the sun-to-moon colour switch was seen (the raw-colour rule above was exercised)");
            logs.AssertNoWarnings("day-night scrub");
        }

        private static Vector4 Lin(Vector4 v)
        {
            Color c = new Color(v.x, v.y, v.z, v.w).linear;
            return new Vector4(c.r, c.g, c.b, c.a);
        }

        // P1: tint[i] = seasonLin[i] / baseLin[i] per rgb channel (alpha 1), a base channel below 1e-4 gives 1, clamped to [0, 2].
        private static Vector4[] ExpectedTint(ArtPaletteDef art, int season)
        {
            Vector4[] s = art.PaletteVectors(season), b = art.PaletteVectors(art.M2Season);
            var tint = new Vector4[ShaderGlobals.SeasonTintLength];
            for (int i = 0; i < tint.Length; i++)
            {
                Vector4 sl = Lin(s[i]), bl = Lin(b[i]);
                tint[i] = new Vector4(Ratio(sl.x, bl.x), Ratio(sl.y, bl.y), Ratio(sl.z, bl.z), 1f);
            }
            return tint;
        }

        private static float Ratio(float s, float b)
        {
            return b < 1e-4f ? 1f : Mathf.Clamp(s / b, 0f, 2f);
        }

        // Catches: Apply on every Changed (red after the weather change); Apply per frame; the tint pushed in gamma or not at all; the tint
        // computed inverted (base / season); the world season never reaching the palette or the seasonal trees.
        [UnityTest]
        public IEnumerator DayNight_SeasonChange_PushesTintOnceNotPerFrame()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return Arrange();
            ArtPaletteDef art = Art();
            Assert.That(art.M2Season, Is.EqualTo((int)Season.Summer), "the tint base is Summer");
            Component globals = FindComponent("PaletteGlobals");

            _driver.DevSetSeason(Season.Summer);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            int n0 = ApplyCount(globals);
            Vector4[] summerTint = Shader.GetGlobalVectorArray(ShaderGlobals.SeasonTint);
            Assert.That(summerTint, Is.Not.Null, "_FGSeasonTint was never pushed");
            Assert.That(summerTint.Length, Is.EqualTo(ShaderGlobals.SeasonTintLength), "_FGSeasonTint is pushed full length");
            for (int i = 0; i < summerTint.Length; i++)
                Assert.That(summerTint[i], Is.EqualTo(Vector4.one), "Summer is the base: the tint is exactly (1, 1, 1, 1) at slot " + i);
            Assert.That(SeasonalVisibility.CurrentSeason, Is.EqualTo((int)Season.Summer), "the seasonal trees follow the pushed season");

            _driver.DevSetSeason(Season.Autumn);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            Assert.That(ApplyCount(globals), Is.EqualTo(n0 + 1), "one Apply for one season change");
            Assert.That(SeasonalVisibility.CurrentSeason, Is.EqualTo((int)Season.Autumn), "SeasonalVisibility.SetSeason runs on the re-apply too");

            Vector4[] autumn = art.PaletteVectors((int)Season.Autumn);
            Vector4[] pushed = Shader.GetGlobalVectorArray(ShaderGlobals.Palette);
            Assert.That(pushed.Length, Is.EqualTo(ShaderGlobals.PaletteLength));
            for (int i = 0; i < ShaderGlobals.PaletteLength; i++)
            {
                Vector4 lin = Lin(autumn[i]);
                Assert.That(pushed[i].x, Is.EqualTo(lin.x).Within(1e-4f), "_FGPalette[" + i + "].r is Autumn, linear");
                Assert.That(pushed[i].y, Is.EqualTo(lin.y).Within(1e-4f), "_FGPalette[" + i + "].g");
                Assert.That(pushed[i].z, Is.EqualTo(lin.z).Within(1e-4f), "_FGPalette[" + i + "].b");
            }
            Vector4[] expected = ExpectedTint(art, (int)Season.Autumn);
            Vector4[] tint = Shader.GetGlobalVectorArray(ShaderGlobals.SeasonTint);
            Assert.That(tint, Is.Not.Null, "_FGSeasonTint was never pushed");
            Assert.That(tint.Length, Is.EqualTo(ShaderGlobals.SeasonTintLength), "_FGSeasonTint length");
            bool anyNotOne = false;
            for (int i = 0; i < tint.Length; i++)
            {
                Assert.That(tint[i].x, Is.EqualTo(expected[i].x).Within(1e-4f), "_FGSeasonTint[" + i + "].r = autumn / summer, linear");
                Assert.That(tint[i].y, Is.EqualTo(expected[i].y).Within(1e-4f), "_FGSeasonTint[" + i + "].g");
                Assert.That(tint[i].z, Is.EqualTo(expected[i].z).Within(1e-4f), "_FGSeasonTint[" + i + "].b");
                Assert.That(tint[i].w, Is.EqualTo(1f).Within(1e-4f), "_FGSeasonTint[" + i + "].a");
                if (Mathf.Abs(expected[i].x - 1f) > 0.05f || Mathf.Abs(expected[i].y - 1f) > 0.05f) anyNotOne = true;
            }
            Assert.That(anyNotOne, Is.True, "Autumn must differ from Summer in some slot, or the ratio check proves nothing");

            yield return PlayModeHarness.WaitFrames(10, 0.2f);
            Assert.That(ApplyCount(globals), Is.EqualTo(n0 + 1), "no Apply per frame");

            _driver.DevSetWeatherOverride(WeatherMask.Rain);   // raises Changed
            yield return PlayModeHarness.WaitFrames(10, 0.2f);
            Assert.That(ApplyCount(globals), Is.EqualTo(n0 + 1), "a weather change must not re-apply the palette");

            _driver.DevSetSeason(Season.Autumn);               // same value
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            Assert.That(ApplyCount(globals), Is.EqualTo(n0 + 1), "setting the same season again must not re-apply");

            logs.AssertNoWarnings("season change");
        }

        // Catches: weather looks ignored (the Fog density stays at the phase value); fogDensityMin applied as a multiplier; lightMult not
        // applied; the cap engaging (the density would then not equal the formula).
        [UnityTest]
        public IEnumerator DayNight_FogReadableAt25m_EveryPhaseAndWeather()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return Arrange();
            ArtPaletteDef art = Art();
            WeatherMask[] weathers = { WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };
            int[] minutes = { 60, 300, 760, 1000 };   // Dawn, Day, Dusk, Night (pure phases 0..3: all outside the blend windows)

            for (int w = 0; w < weathers.Length; w++)
            {
                WeatherAmbient look = art.WeatherLooks[w];
                _driver.DevSetWeatherOverride(weathers[w]);
                yield return PlayModeHarness.WaitFrames(3, 4.5f);   // the 4 s cross-fade is done

                for (int k = 0; k < minutes.Length; k++)
                {
                    _driver.DevSetMinute(minutes[k]);
                    yield return PlayModeHarness.WaitFrames(2, 0f);
                    DayPhaseAmbient phase = art.Phases[k];
                    string at = weathers[w] + " at m " + minutes[k];
                    Assert.That(RenderSettings.fog, Is.True, "fog stays on: " + at);
                    Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.ExponentialSquared), "fog mode: " + at);
                    float density = RenderSettings.fogDensity;
                    Assert.That(DayNightMath.IsReadable(density), Is.True, "fog " + density + " is not readable at 25 m: " + at);
                    float expected = Mathf.Max(phase.fogDensity * look.fogDensityMult, look.fogDensityMin);
                    Assert.That(density, Is.EqualTo(expected).Within(1e-5f), "fog density follows max(phase x mult, min): " + at);
                    if (minutes[k] == 300)
                        Assert.That(_sun.intensity, Is.EqualTo(phase.lightIntensity * look.lightMult).Within(1e-4f), "sun intensity x lightMult: " + at);
                }

                if (weathers[w] == WeatherMask.Fog)
                {
                    for (int m = 0; m < 1200; m += 10)
                    {
                        _driver.DevSetMinute(m);
                        yield return PlayModeHarness.WaitFrames(1, 0f);
                        Assert.That(DayNightMath.IsReadable(RenderSettings.fogDensity), Is.True, "max fog readable at 25 m, m " + m);
                    }
                }
            }
            logs.AssertNoWarnings("fog readability");
        }
    }
}
