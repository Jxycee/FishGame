// Authority: test-only. T19 (section F plan): the rain look and loop, the hotspot VFX and the seasonal wind, driven through the host's
// DevWorldDriver / WorldState (Gameplay, reachable) and observed from outside. This assembly does not reference Presentation (C 2.8 #8), so the
// scene objects are found by name: Presentation/RainFx, Presentation/HotspotFx (+ Bubbles), Ambience/Wind, and the AudioBus child Loop_Rain_Patter.
using System;
using System.Collections;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class WeatherFxTests
    {
        private static readonly int RipplesId = Shader.PropertyToID(ShaderGlobals.Ripples);

        private DevWorldDriver _driver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
            EcologySession.Clear();   // DevSetSeason writes the session-level run state
        }

        // Solo on the lake, world state present, clock stopped at `minute`, weather forced Clear and its fade finished.
        private IEnumerator Arrange(int minute)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10f,
                                                   "WorldState.Current did not spawn with state");
            _driver = WorldState.Current.GetComponent<DevWorldDriver>();
            Assert.That(_driver, Is.Not.Null, "the World root has a DevWorldDriver");
            _driver.DevSetClockRunning(false);
            _driver.DevSetMinute(minute);
            _driver.DevSetWeatherOverride(WeatherMask.Clear);
            yield return PlayModeHarness.WaitFrames(3, 4.5f);   // any fade from the rolled weather has finished
        }

        private static ParticleSystem RainFx()
        {
            GameObject root = GameObject.Find("Presentation");
            Assert.That(root, Is.Not.Null, "Presentation root");
            Transform t = root.transform.Find("RainFx");
            Assert.That(t, Is.Not.Null, "Presentation/RainFx");
            var ps = t.GetComponent<ParticleSystem>();
            Assert.That(ps, Is.Not.Null, "RainFx has a ParticleSystem");
            return ps;
        }

        private static Transform RainLoop()
        {
            Assert.That(ServiceRoots.App.TryGet(out AudioBus bus), Is.True, "AudioBus");
            return bus.transform.Find("Loop_" + SfxIds.RainPatter);
        }

        private static Vector4[] Ripples()
        {
            Vector4[] r = Shader.GetGlobalVectorArray(RipplesId);
            Assert.That(r, Is.Not.Null, "_Ripples pushed");
            return r;
        }

        // Distinct spawn stamps of ripples with strength 0.4 within `maxDist` of (cx, cz).
        private static int HotspotRipples(float cx, float cz, float maxDist, List<float> stamps)
        {
            foreach (Vector4 s in Ripples())
            {
                if (Mathf.Abs(s.w - 0.4f) > 1e-5f) continue;
                float d = Mathf.Sqrt((s.x - cx) * (s.x - cx) + (s.y - cz) * (s.y - cz));
                Assert.That(d, Is.LessThanOrEqualTo(maxDist), "a hotspot ripple outside 0.5 x radius of the hotspot");
                if (!stamps.Contains(s.z)) stamps.Add(s.z);
            }
            return stamps.Count;
        }

        // Catches: StopLoop never called (the loop outlives the rain); emission not tied to the blended look; rain playing while the look says
        // Clear; a 3D loop (spatialBlend must be 0).
        [UnityTest]
        public IEnumerator WeatherFx_RainSegment_StartsRainAndLoopStopsOnClear()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return Arrange(300);

            Assert.That(RainLoop(), Is.Null, "no Rain_Patter loop under Clear");
            Assert.That(RainFx().emission.rateOverTime.constant, Is.EqualTo(0f), "no rain emission under Clear");

            _driver.DevSetWeatherOverride(WeatherMask.Rain);
            ParticleSystem rain = RainFx();
            yield return PlayModeHarness.WaitUntil(() => rain.particleCount > 0, 6f, "no rain particle after switching to Rain");
            yield return PlayModeHarness.WaitUntil(() => RainLoop() != null, 6f, "no Loop_Rain_Patter after switching to Rain");
            var source = RainLoop().GetComponent<AudioSource>();
            Assert.That(source, Is.Not.Null, "the loop child has an AudioSource");
            Assert.That(source.spatialBlend, Is.EqualTo(0f), "Rain_Patter is a 2D loop");
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null, "Camera.main");
            Vector3 d = rain.transform.position - cam.transform.position;
            d.y = 0f;
            Assert.That(d.magnitude, Is.LessThan(15f), "the emitter follows the camera");
            Assert.That(rain.transform.position.y, Is.GreaterThan(cam.transform.position.y + 4f), "the emitter sits above the camera");

            yield return PlayModeHarness.WaitFrames(3, 4.5f);   // the fade to full rain is done
            Assert.That(rain.emission.rateOverTime.constant, Is.EqualTo(450f).Within(1e-3f), "full rain emits 450 drops/s");
            Assert.That(rain.isEmitting || rain.particleCount > 0, Is.True, "the rain system is live");

            _driver.DevSetWeatherOverride(WeatherMask.Clear);
            yield return PlayModeHarness.WaitUntil(() => RainLoop() == null, 6f, "the rain loop must stop once the look says Clear");
            yield return PlayModeHarness.WaitUntil(() => rain.emission.rateOverTime.constant == 0f, 6f, "the rain emission must reach 0 under Clear");
            yield return PlayModeHarness.WaitFrames(3, 2f);
            Assert.That(rain.particleCount, Is.EqualTo(0), "the last drops have fallen");

            logs.AssertNoWarnings("rain segment");
        }

        // Catches: ignoring Active (keeps spawning after the clear); ripple strength not 0.4; a ripple per frame (more than 4 new in 3 s).
        // HotspotFx reads only NetHotspot, which every client receives (late joiners on spawn): this is the "every peer view" claim; the MPPM
        // check in T25 covers a real client. The test writes the replicated variable directly, so it also runs under the 12.7 cut-line.
        [UnityTest]
        public IEnumerator HotspotFx_ActiveHotspot_SpawnsRipplesOnEveryPeerView()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return Arrange(10);   // minute 10: before the planner's first natural spawn at 60-120, so no natural hotspot is active

            Assert.That(HotspotRipples(0f, -30f, 1e6f, new List<float>()), Is.EqualTo(0), "precondition: no hotspot ripples yet");
            var stamps = new List<float>();
            WorldState.Current.ServerSetHotspot(new HotspotData { Active = true, X = 0f, Z = -30f, Radius = 3f, StartMinute = 10, EndMinute = 70 });

            float maxDist = 0.5f * 3f + 1e-3f;
            yield return PlayModeHarness.WaitUntil(() => HotspotRipples(0f, -30f, maxDist, stamps) >= 1, 2f, "no 0.4 ripple within 2 s of the hotspot");

            Transform fx = GameObject.Find("Presentation").transform.Find("HotspotFx");
            Assert.That(fx, Is.Not.Null, "Presentation/HotspotFx");
            ParticleSystem drops = fx.GetComponent<ParticleSystem>();
            ParticleSystem bubbles = fx.Find("Bubbles").GetComponent<ParticleSystem>();
            yield return PlayModeHarness.WaitUntil(() => drops.particleCount + bubbles.particleCount > 0, 2f, "no hotspot particles");

            int before = stamps.Count;
            double until = Time.realtimeSinceStartupAsDouble + 3.0;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                HotspotRipples(0f, -30f, maxDist, stamps);
                yield return null;
            }
            Assert.That(stamps.Count, Is.GreaterThan(before), "a second 0.4 ripple must appear");
            Assert.That(stamps.Count - before, Is.LessThanOrEqualTo(4), "one ripple every 1.2 s, never one per frame");

            // Cleared through Active alone: radius and end minute stay valid, so only the Active flag can switch the effect off.
            WorldState.Current.ServerSetHotspot(new HotspotData { Active = false, X = 0f, Z = -30f, Radius = 3f, StartMinute = 10, EndMinute = 70 });
            float cleared = (float)WaterSurface.Now;
            yield return PlayModeHarness.WaitFrames(3, 1.5f);
            foreach (Vector4 s in Ripples())
            {
                if (Mathf.Abs(s.w - 0.4f) > 1e-5f) continue;
                Assert.That(s.z, Is.LessThanOrEqualTo(cleared + 0.05f), "no new hotspot ripple after the hotspot was cleared");
            }
            yield return PlayModeHarness.WaitFrames(3, 3f);
            Assert.That(drops.particleCount + bubbles.particleCount, Is.EqualTo(0), "the hotspot particles have died");

            logs.AssertNoWarnings("hotspot fx");
        }

        // Catches: the season ignored by the bed; an LFO depth of 1 (the wind would dip to silence); Summer not reproducing M2 (a re-register
        // or a wobble in Summer).
        [UnityTest]
        public IEnumerator AmbienceBed_SeasonSets_WinterLouderSummerUnchanged()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return Arrange(300);

            GameObject bedGo = GameObject.Find("Ambience");
            Assert.That(bedGo, Is.Not.Null, "Ambience root");
            var bed = bedGo.GetComponent<AmbienceBed>();
            Assert.That(bed, Is.Not.Null, "AmbienceBed on the Ambience root");
            Assert.That(bed.enabled, Is.True, "AudioBus not ready: the AmbienceBed disabled itself");
            var wind = bedGo.transform.Find("Wind").GetComponent<AudioSource>();
            Assert.That(wind, Is.Not.Null, "Ambience/Wind");

            _driver.DevSetSeason(Season.Summer);
            yield return PlayModeHarness.WaitFrames(3, 2f);
            float vS = wind.volume;
            Assert.That(vS, Is.GreaterThan(0f), "the wind is audible in Summer");
            for (int i = 0; i < 10; i++)
            {
                yield return PlayModeHarness.WaitFrames(1, 0.2f);
                Assert.That(wind.volume, Is.EqualTo(vS).Within(1e-5f), "Summer wind is steady (sample " + i + ")");
            }

            _driver.DevSetSeason(Season.Winter);
            yield return PlayModeHarness.WaitFrames(3, 2.5f);   // the gain ramp (0.5 per second) is done
            var seen = new HashSet<float>();
            for (int i = 0; i < 20; i++)
            {
                yield return PlayModeHarness.WaitFrames(1, 0.2f);
                float ratio = wind.volume / vS;
                Assert.That(ratio, Is.InRange(1.5f * 0.75f - 0.01f, 1.5f * 1.25f + 0.01f), "Winter wind gain 1.5 x LFO(0.75..1.25), sample " + i);
                seen.Add(wind.volume);
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "the Winter wind wobbles (LFO 0.15 Hz)");

            _driver.DevSetSeason(Season.Summer);
            yield return PlayModeHarness.WaitFrames(3, 2f);
            Assert.That(wind.volume, Is.EqualTo(vS).Within(1e-5f), "back to Summer: the M2 wind");

            logs.AssertNoWarnings("seasonal ambience");
        }
    }
}
