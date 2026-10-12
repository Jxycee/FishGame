// Authority: test-only (no networked state). A standalone AudioBus (no Bootstrap, no scene load) for the behaviour EditMode cannot see:
// Update / LateUpdate and real elapsed time. T11 deviation: not in the C 10.1 list; added so ducking-on-unscaled-time, rate-limit expiry
// and loop follow are covered by a test that fails when the code is wrong.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class AudioBusPlayModeTests
    {
        private const ulong Seed = 1234UL;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
        }

        private AudioBus NewBus()
        {
            var go = new GameObject("AudioBusPlayModeTest");
            _spawned.Add(go);
            var bus = go.AddComponent<AudioBus>();
            var gen = new SfxGenerator();
            gen.GenerateAll(Seed);
            var settings = new SettingsStore(Path.Combine(Path.GetTempPath(), "fg_audiobus_playmode_settings.json")); // never loaded or saved
            bus.Initialize(gen, settings);
            return bus;
        }

        private AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<AudioSource>();
        }

        private static int PlayedCount(AudioBus bus)
        {
            int n = 0;
            foreach (AudioSource s in bus.GetComponentsInChildren<AudioSource>(true))
            {
                if (s.gameObject.name.StartsWith("SfxSource") && s.clip != null) n++;
            }
            return n;
        }

        // Catches: Update / Duck reading Time.deltaTime or Time.time (frozen at timeScale 0: the gain never moves, or never releases);
        // Duck affecting Sfx / Ui; Duck not covering an already registered Ambience source; the hold ending early or never.
        [UnityTest]
        public IEnumerator AudioBus_Duck_RampsOnUnscaledTimeAndReleases()
        {
            AudioBus bus = NewBus();
            AudioSource ambience = NewSource("Ambience");
            AudioSource sfx = NewSource("Sfx");
            bus.Register(ambience, AudioCategory.Ambience, 0.5f);
            bus.Register(sfx, AudioCategory.Sfx, 0.5f);
            float ambNormal = AudioMath.EffectiveVolume(0.5f, 70, 80);
            float sfxNormal = AudioMath.EffectiveVolume(0.5f, 80, 80);
            Assert.That(ambience.volume, Is.EqualTo(ambNormal).Within(1e-5f));

            Time.timeScale = 0f;
            bus.Duck(0.3f);                                   // ramp 0.3 s, hold 0.3 s, release 0.3 s
            yield return new WaitForSecondsRealtime(0.45f);   // inside the hold
            Assert.That(ambience.volume, Is.EqualTo(ambNormal * 0.5f).Within(0.003f), "ducked to 50 percent while Time.timeScale is 0");
            Assert.That(sfx.volume, Is.EqualTo(sfxNormal).Within(1e-5f), "Sfx is never ducked");

            yield return new WaitForSecondsRealtime(0.9f);    // hold over, release done
            Assert.That(ambience.volume, Is.EqualTo(ambNormal).Within(0.003f), "the duck releases back to full");

            bus.Duck(0.2f);
            yield return new WaitForSecondsRealtime(0.1f);    // mid-ramp: strictly between full and half
            Assert.That(ambience.volume, Is.LessThan(ambNormal - 0.005f));
            Assert.That(ambience.volume, Is.GreaterThan(ambNormal * 0.5f + 0.005f), "the gain ramps over 0.3 s, it does not jump");
            Time.timeScale = _timeScale;
        }

        // Catches: a rate limit that never expires, or expires before MinInterval (Bite_Plop 0.10 s), measured on real time.
        [UnityTest]
        public IEnumerator AudioBus_RateLimit_ExpiresAfterMinInterval()
        {
            AudioBus bus = NewBus();
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            Assert.That(PlayedCount(bus), Is.EqualTo(1), "second play inside 0.10 s is dropped");
            yield return new WaitForSecondsRealtime(0.25f);
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            Assert.That(PlayedCount(bus), Is.EqualTo(2), "after the interval it plays again");
        }

        // Catches: LateUpdate not copying the follow position; a loop that survives its destroyed follow; a loop that never starts
        // playing; StopLoop after the auto-release throwing.
        [UnityTest]
        public IEnumerator AudioBus_Loop_FollowsTransformAndStopsWhenItIsDestroyed()
        {
            AudioBus bus = NewBus();
            var followGo = new GameObject("Follow");
            _spawned.Add(followGo);
            AudioLoop loop = bus.StartLoop(SfxIds.LineCreak, followGo.transform, true);
            Assert.That(loop, Is.Not.Null);
            yield return null;
            Assert.That(loop.IsPlaying, Is.True, "the loop is playing after StartLoop");

            followGo.transform.position = new Vector3(9f, 0f, -4f);
            yield return null;
            yield return null;
            Transform src = bus.transform.Find("Loop_" + SfxIds.LineCreak);
            Assert.That(src, Is.Not.Null);
            Assert.That(src.position, Is.EqualTo(new Vector3(9f, 0f, -4f)), "LateUpdate copies the follow position");

            Object.Destroy(followGo);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(loop.IsPlaying, Is.False, "the loop stops when its follow is destroyed");
            Assert.That(bus.transform.Find("Loop_" + SfxIds.LineCreak), Is.Null, "and its source is released");
            Assert.DoesNotThrow(() => bus.StopLoop(loop));
        }
    }
}
