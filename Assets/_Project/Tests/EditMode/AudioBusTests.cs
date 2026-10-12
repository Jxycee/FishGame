using System;
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FishGame.Tests.EditMode
{
    // AudioBus (T1 warn-once gate, T11 warn-once carry-over, rate limit, per-play settings, cue table, loops). Everything runs in
    // EditMode through the public surface: the pool sources are inspected on the bus children. Update and LateUpdate never run in
    // EditMode, so ducking and loop-follow are covered by Tests/PlayMode/AudioBusPlayModeTests.cs. Time.unscaledTime does not advance
    // inside one synchronous test, so "same cell, same frame" is always inside the rate-limit window here.
    public class AudioBusTests
    {
        private const ulong Seed = 1234UL;
        private SfxGenerator _clips;               // all 18 recipes generated once for the fixture
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _clips = new SfxGenerator();
            _clips.GenerateAll(Seed);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            foreach (SfxRecipe r in SfxRecipes.All)
            {
                if (_clips.TryGetClip(r.Id, out AudioClip c)) Object.DestroyImmediate(c);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------------------------------------

        private sealed class WarningCounter : IDisposable
        {
            public readonly List<string> Messages = new List<string>();
            public WarningCounter() { Application.logMessageReceived += OnLog; }
            public void Dispose() { Application.logMessageReceived -= OnLog; }
            private void OnLog(string text, string stack, LogType type)
            {
                if (type == LogType.Warning) Messages.Add(text);
            }
            public int Count(string contains)
            {
                int n = 0;
                foreach (string m in Messages) if (m.Contains(contains)) n++;
                return n;
            }
        }

        private AudioBus NewBus(out SettingsStore settings, bool withClips = true, bool initialize = true)
        {
            var go = new GameObject("AudioBusTest");
            _spawned.Add(go);
            var bus = go.AddComponent<AudioBus>();
            settings = new SettingsStore(Path.Combine(Path.GetTempPath(), "fg_audiobus_test_settings.json")); // never loaded or saved
            if (initialize) bus.Initialize(withClips ? _clips : new SfxGenerator(), settings);
            return bus;
        }

        private static List<AudioSource> PoolSources(AudioBus bus)
        {
            var list = new List<AudioSource>();
            foreach (AudioSource s in bus.GetComponentsInChildren<AudioSource>(true))
            {
                if (s.gameObject.name.StartsWith("SfxSource", StringComparison.Ordinal)) list.Add(s);
            }
            return list;
        }

        private static int PlayedCount(AudioBus bus)
        {
            int n = 0;
            foreach (AudioSource s in PoolSources(bus)) if (s.clip != null) n++;
            return n;
        }

        private static AudioSource LastPlayedWith(AudioBus bus, string clipName)
        {
            AudioSource found = null;
            foreach (AudioSource s in PoolSources(bus)) if (s.clip != null && s.clip.name == clipName) found = s;
            return found;
        }

        private static float Eff(float cueBase, int categoryPercent, int masterPercent)
        {
            return AudioMath.EffectiveVolume(cueBase, categoryPercent, masterPercent);
        }

        // ---- warn-once gates (T1, T11 carry-over) -------------------------------------------------------------------------

        [Test]
        public void AudioBus_NotReady_WarnsOncePerId()
        {
            var go = new GameObject("AudioBusTest");
            int warnings = 0;
            void OnLog(string text, string stack, LogType type)
            {
                if (type == LogType.Warning && text.Contains("AudioBus not initialized")) warnings++;
            }
            Application.logMessageReceived += OnLog;
            try
            {
                var bus = go.AddComponent<AudioBus>();
                bus.PlayUi("x");
                bus.PlayUi("x");
                bus.PlayAt("x", Vector3.zero);
                bus.PlayUi("y");
                Assert.That(warnings, Is.EqualTo(2));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Object.DestroyImmediate(go);
            }
        }

        // Catches: the "unknown|<id>" ShouldLog guard removed (one warning per call), a key shared across ids, an unknown id that
        // does not reach any play path (PlayUi, PlayAt, StartLoop all share the gate).
        [Test]
        public void AudioBus_UnknownId_WarnsOncePerId()
        {
            AudioBus bus = NewBus(out _);
            using (var w = new WarningCounter())
            {
                bus.PlayUi("no_such_id");
                bus.PlayUi("no_such_id");
                bus.PlayAt("no_such_id", Vector3.zero);
                Assert.That(w.Count("no_such_id"), Is.EqualTo(1), "one warning for the first unknown id, whatever the entry point");
                Assert.That(w.Messages[0], Does.Contain("Unknown SFX id"));
                bus.PlayUi("other_id");
                bus.PlayUi("other_id");
                Assert.That(w.Count("other_id"), Is.EqualTo(1), "a second unknown id warns once more");
                Assert.That(bus.StartLoop("no_such_id", null, false), Is.Null);
                Assert.That(w.Count("no_such_id"), Is.EqualTo(1), "StartLoop shares the gate and stays silent the second time");
                Assert.That(w.Messages.Count, Is.EqualTo(2));
            }
        }

        // Catches: the "missing|<id>" ShouldLog guard removed, a missing clip that is not skipped, a gate shared between ids.
        [Test]
        public void AudioBus_MissingClip_WarnsOncePerId()
        {
            AudioBus bus = NewBus(out _, withClips: false);
            using (var w = new WarningCounter())
            {
                bus.PlayUi(SfxIds.UiClick);
                bus.PlayUi(SfxIds.UiClick);
                bus.PlayAt(SfxIds.BellRing, Vector3.zero);
                bus.PlayAt(SfxIds.BellRing, new Vector3(50f, 0f, 50f));
                Assert.That(w.Count("'" + SfxIds.UiClick + "'"), Is.EqualTo(1));
                Assert.That(w.Count("'" + SfxIds.BellRing + "'"), Is.EqualTo(1));
                Assert.That(w.Messages[0], Does.Contain("is missing"));
                Assert.That(bus.StartLoop(SfxIds.LineCreak, null, false), Is.Null, "a loop with no clip returns null");
                Assert.That(bus.StartLoop(SfxIds.LineCreak, null, false), Is.Null);
                Assert.That(w.Count("'" + SfxIds.LineCreak + "'"), Is.EqualTo(1));
                Assert.That(w.Messages.Count, Is.EqualTo(3));
            }
            Assert.That(PlayedCount(bus), Is.EqualTo(0), "nothing was played");
        }

        // ---- rate limit (spec 11.7, C-Q6) ---------------------------------------------------------------------------------

        // Catches: PlayAt keyed by id only (two friends' bobbers merged); a truncating (int) cell instead of floor, which puts
        // x = -0.5 and x = +0.5 in one cell; a cell size other than 2 m; Play2D not limited by id; the limit shared between ids.
        [Test]
        public void AudioBus_PlayAt_RateLimitIsPerIdAndTwoMeterCell()
        {
            AudioBus bus = NewBus(out _);
            string id = SfxIds.BitePlop;                         // MinInterval 0.10
            bus.PlayAt(id, new Vector3(0.5f, 0f, 0.5f));
            Assert.That(PlayedCount(bus), Is.EqualTo(1));
            bus.PlayAt(id, new Vector3(0.5f, 0f, 0.5f));
            bus.PlayAt(id, new Vector3(1.9f, 0f, 1.9f));         // same 2 m cell (0, 0)
            Assert.That(PlayedCount(bus), Is.EqualTo(1), "same cell inside the interval is dropped");
            bus.PlayAt(id, new Vector3(2.1f, 0f, 0.5f));         // cell (1, 0)
            Assert.That(PlayedCount(bus), Is.EqualTo(2), "the next cell on x plays");
            bus.PlayAt(id, new Vector3(0.5f, 0f, 2.1f));         // cell (0, 1)
            Assert.That(PlayedCount(bus), Is.EqualTo(3), "the next cell on z plays");
            bus.PlayAt(id, new Vector3(-0.5f, 0f, 0.5f));        // cell (-1, 0): floor, not truncation
            Assert.That(PlayedCount(bus), Is.EqualTo(4), "x = -0.5 is cell -1, not cell 0");
            bus.PlayAt(id, new Vector3(0.5f, 0f, -0.5f));        // cell (0, -1)
            Assert.That(PlayedCount(bus), Is.EqualTo(5), "z = -0.5 is cell -1, not cell 0");
            bus.PlayAt(SfxIds.SplashSmall, new Vector3(0.5f, 0f, 0.5f));
            Assert.That(PlayedCount(bus), Is.EqualTo(6), "another id in the same cell is independent");

            bus.PlayUi(SfxIds.UiClick);
            bus.PlayUi(SfxIds.UiClick);
            Assert.That(PlayedCount(bus), Is.EqualTo(7), "Play2D is limited by id (0.03 s)");
            bus.PlayUi(SfxIds.UiConfirm);
            Assert.That(PlayedCount(bus), Is.EqualTo(8), "a different 2D id plays");
        }

        // Catches: a MinInterval of 0 still limiting (recipes with no interval must never be dropped).
        [Test]
        public void AudioBus_ZeroMinInterval_IsNeverRateLimited()
        {
            AudioBus bus = NewBus(out _);
            bus.ApplyCueTable(new[] { new SfxRecipe(SfxIds.BitePlop, 0.2f, false, AudioCategory.Sfx, 0.7f, true, 30f, 0f, 1f) });
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            Assert.That(PlayedCount(bus), Is.EqualTo(3));
        }

        // ---- per-play settings (pooled sources are reused) ----------------------------------------------------------------

        // Catches: minDistance / maxDistance / rolloff / spatialBlend / position left from the previous user of a pooled source
        // (Splash_Big leaves 8 m); the recipe MinDistance ignored; the pitch multiplier missing, unclamped or NaN-unsafe.
        [Test]
        public void AudioBus_PlayAt_SetsDistancesPitchAndPositionOnEveryPlay()
        {
            AudioBus bus = NewBus(out _);
            bus.PlayAt(SfxIds.SplashBig, new Vector3(5f, 1f, 7f));
            AudioSource big = LastPlayedWith(bus, SfxIds.SplashBig);
            Assert.That(big, Is.Not.Null);
            Assert.That(big.minDistance, Is.EqualTo(8f));
            Assert.That(big.maxDistance, Is.EqualTo(45f));
            Assert.That(big.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
            Assert.That(big.spatialBlend, Is.EqualTo(1f));
            Assert.That(big.loop, Is.False);
            Assert.That(big.transform.position, Is.EqualTo(new Vector3(5f, 1f, 7f)));
            Assert.That(big.pitch, Is.InRange(0.93f, 1.07f), "jitter only when no multiplier");

            // Eight more plays at far apart cells make the ninth play reuse the first pooled source.
            for (int k = 0; k < 8; k++) bus.PlayAt(SfxIds.NibbleTick, new Vector3(10f * (k + 1), 0f, 0f));
            Assert.That(big.clip.name, Is.EqualTo(SfxIds.NibbleTick), "the first pooled source was reused");
            Assert.That(big.minDistance, Is.EqualTo(1f), "the 8 m minDistance of Splash_Big must not stick");
            Assert.That(big.maxDistance, Is.EqualTo(20f));
            Assert.That(big.transform.position, Is.EqualTo(new Vector3(80f, 0f, 0f)));

            AudioBus pitchBus = NewBus(out _);
            pitchBus.PlayAt(SfxIds.RodThunk, new Vector3(0f, 0f, 0f), 2f);
            pitchBus.PlayAt(SfxIds.RodThunk, new Vector3(10f, 0f, 0f), 0f);
            pitchBus.PlayAt(SfxIds.RodThunk, new Vector3(20f, 0f, 0f), 100f);
            pitchBus.PlayAt(SfxIds.RodThunk, new Vector3(30f, 0f, 0f), float.NaN);
            pitchBus.PlayAt(SfxIds.RodThunk, new Vector3(40f, 0f, 0f));
            var pitches = new Dictionary<float, float>();
            foreach (AudioSource s in PoolSources(pitchBus)) if (s.clip != null) pitches[s.transform.position.x] = s.pitch;
            Assert.That(pitches[0f], Is.InRange(2f * 0.93f, 2f * 1.07f), "multiplier 2");
            Assert.That(pitches[10f], Is.InRange(0.1f * 0.93f, 0.1f * 1.07f), "multiplier 0 clamps to 0.1");
            Assert.That(pitches[20f], Is.InRange(3f * 0.93f, 3f * 1.07f), "multiplier 100 clamps to 3");
            Assert.That(pitches[30f], Is.InRange(0.93f, 1.07f), "a NaN multiplier plays at normal pitch");
            Assert.That(pitches[40f], Is.InRange(0.93f, 1.07f), "the two-argument PlayAt forwards 1");
        }

        // Catches: Register applying a zero duck gain before the first Update (an Ambience or Music source registered at start-up
        // stays silent, and in EditMode Update never runs).
        [Test]
        public void AudioBus_Register_AmbienceIsNotSilentBeforeAnyUpdate()
        {
            AudioBus bus = NewBus(out _);
            var go = new GameObject("AmbienceProbe");
            _spawned.Add(go);
            AudioSource src = go.AddComponent<AudioSource>();
            bus.Register(src, AudioCategory.Ambience, 0.5f);
            Assert.That(src.volume, Is.EqualTo(Eff(0.5f, 70, 80)).Within(1e-5f));
            bus.Duck(1f);
            bus.ApplyVolumes();
            Assert.That(src.volume, Is.EqualTo(Eff(0.5f, 70, 80)).Within(1e-5f), "a Duck request alone changes nothing until Update ramps the gain");
        }

        // ---- cue table (C 7.11, M2-D11) -----------------------------------------------------------------------------------

        // Catches: TryPrepare reading SfxRecipes.TryGet directly (override ignored); an override replacing Core length / loop;
        // BaseVolume not applied; MinInterval override ignored; ApplyCueTable(null) not restoring the Core values.
        [Test]
        public void AudioBus_CueTable_OverridesMixingFieldsAndNullRestoresCore()
        {
            AudioBus bus = NewBus(out _);
            bus.PlayAt(SfxIds.SplashBig, new Vector3(0f, 0f, 0f));
            Assert.That(LastPlayedWith(bus, SfxIds.SplashBig).minDistance, Is.EqualTo(8f), "Core default");

            var overrides = new List<SfxRecipe>
            {
                new SfxRecipe(SfxIds.SplashBig, 99f, true, AudioCategory.Sfx, 0.2f, false, 12f, 0.5f, 3f),
                new SfxRecipe(SfxIds.BitePlop, 99f, true, AudioCategory.Sfx, 0.7f, true, 30f, 0f, 1f),
            };
            bus.ApplyCueTable(overrides);
            bus.PlayAt(SfxIds.SplashBig, new Vector3(20f, 0f, 0f));
            AudioSource big = LastPlayedWith(bus, SfxIds.SplashBig);
            Assert.That(big.transform.position.x, Is.EqualTo(20f));
            Assert.That(big.minDistance, Is.EqualTo(3f));
            Assert.That(big.maxDistance, Is.EqualTo(12f));
            Assert.That(big.loop, Is.False, "Core Loop (false) wins over the override (true)");
            Assert.That(big.clip.length, Is.EqualTo(0.6f).Within(0.001f), "Core length wins over the override (99 s)");
            Assert.That(big.volume, Is.InRange(Eff(0.2f, 80, 80) * 0.83f, Eff(0.2f, 80, 80) * 1.2f), "override BaseVolume 0.2 (jitter +/-1.5 dB), not Core 1.0");

            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            bus.PlayAt(SfxIds.BitePlop, Vector3.zero);
            int plops = 0;
            foreach (AudioSource s in PoolSources(bus)) if (s.clip != null && s.clip.name == SfxIds.BitePlop) plops++;
            Assert.That(plops, Is.EqualTo(2), "override MinInterval 0 lets a repeat through (Core 0.10 would drop it)");

            bus.ApplyCueTable(null);
            bus.PlayAt(SfxIds.SplashBig, new Vector3(40f, 0f, 0f));
            AudioSource back = LastPlayedWith(bus, SfxIds.SplashBig);
            Assert.That(back.transform.position.x, Is.EqualTo(40f));
            Assert.That(back.minDistance, Is.EqualTo(8f), "null clears the overrides");
            Assert.That(back.maxDistance, Is.EqualTo(45f));
            bus.PlayAt(SfxIds.BitePlop, new Vector3(60f, 0f, 0f));
            bus.PlayAt(SfxIds.BitePlop, new Vector3(60f, 0f, 0f));
            plops = 0;
            foreach (AudioSource s in PoolSources(bus)) if (s.clip != null && s.clip.name == SfxIds.BitePlop && s.transform.position.x == 60f) plops++;
            Assert.That(plops, Is.EqualTo(1), "Core MinInterval is back");
        }

        // Catches: a sanitize rule dropped: NaN BaseVolume, MinDistance <= 0 / NaN / +Inf, MaxDistance <= 0 (means 30) / +Inf or below
        // MinDistance, negative / NaN / +Inf MinInterval (never plays again), BaseVolume above 1.
        [Test]
        public void AudioBus_CueTable_SanitizesMergedValues()
        {
            AudioBus bus = NewBus(out _);
            bus.ApplyCueTable(new[]
            {
                new SfxRecipe(SfxIds.SplashBig, 1f, false, AudioCategory.Sfx, float.NaN, true, 0f, float.NaN, -2f),
                new SfxRecipe(SfxIds.Snap, 1f, false, AudioCategory.Sfx, 5f, true, 2f, -1f, 5f),
                new SfxRecipe(SfxIds.CatchCheer, 1f, false, AudioCategory.Sfx, 0.8f, true, 40f, 0.3f, float.NaN),
                new SfxRecipe(SfxIds.CastWhoosh, 1f, false, AudioCategory.Sfx, 0.8f, true, 40f, 0.3f, float.PositiveInfinity),
                new SfxRecipe(SfxIds.SplashSmall, 1f, false, AudioCategory.Sfx, 0.8f, true, float.PositiveInfinity, 0.3f, 2f),
                new SfxRecipe(SfxIds.NibbleTick, 1f, false, AudioCategory.Sfx, 0.8f, true, 40f, float.PositiveInfinity, 2f),
            });
            bus.PlayAt(SfxIds.CastWhoosh, new Vector3(30f, 0f, 0f));
            bus.PlayAt(SfxIds.SplashSmall, new Vector3(40f, 0f, 0f));
            bus.PlayAt(SfxIds.NibbleTick, new Vector3(50f, 0f, 0f));
            bus.PlayAt(SfxIds.NibbleTick, new Vector3(50f, 0f, 0f));
            AudioSource whoosh = LastPlayedWith(bus, SfxIds.CastWhoosh);
            Assert.That(whoosh.minDistance, Is.EqualTo(1f), "MinDistance +Inf -> 1, not infinite");
            Assert.That(whoosh.maxDistance, Is.EqualTo(40f));
            AudioSource small = LastPlayedWith(bus, SfxIds.SplashSmall);
            Assert.That(small.maxDistance, Is.EqualTo(30f), "MaxDistance +Inf -> default 30, not infinite");
            Assert.That(small.minDistance, Is.EqualTo(2f));
            int tickCount = 0;
            foreach (AudioSource s in PoolSources(bus)) if (s.clip != null && s.clip.name == SfxIds.NibbleTick) tickCount++;
            Assert.That(tickCount, Is.EqualTo(2), "MinInterval +Inf -> 0: the repeat is not dropped forever");
            bus.PlayAt(SfxIds.SplashBig, new Vector3(0f, 0f, 0f));
            bus.PlayAt(SfxIds.SplashBig, new Vector3(0f, 0f, 0f));
            bus.PlayAt(SfxIds.Snap, new Vector3(10f, 0f, 0f));
            bus.PlayAt(SfxIds.CatchCheer, new Vector3(20f, 0f, 0f));
            AudioSource big = LastPlayedWith(bus, SfxIds.SplashBig);
            Assert.That(big.minDistance, Is.EqualTo(1f), "MinDistance <= 0 -> 1");
            Assert.That(big.maxDistance, Is.EqualTo(30f), "MaxDistance 0 -> default 30");
            Assert.That(big.volume, Is.InRange(Eff(1f, 80, 80) * 0.83f, Eff(1f, 80, 80) * 1.2f), "NaN BaseVolume falls back to the Core value 1.0");
            int bigCount = 0;
            foreach (AudioSource s in PoolSources(bus)) if (s.clip != null && s.clip.name == SfxIds.SplashBig) bigCount++;
            Assert.That(bigCount, Is.EqualTo(2), "NaN MinInterval -> 0: the repeat is not dropped");

            AudioSource snap = LastPlayedWith(bus, SfxIds.Snap);
            Assert.That(snap.minDistance, Is.EqualTo(5f));
            Assert.That(snap.maxDistance, Is.EqualTo(5f), "MaxDistance below MinDistance is raised to MinDistance");
            Assert.That(snap.volume, Is.LessThanOrEqualTo(Eff(1f, 80, 80) * 1.2f), "BaseVolume 5 is clamped to 1");

            AudioSource cheer = LastPlayedWith(bus, SfxIds.CatchCheer);
            Assert.That(cheer.minDistance, Is.EqualTo(1f), "NaN MinDistance -> 1");
            Assert.That(cheer.maxDistance, Is.EqualTo(40f));
        }

        // Catches: Play2D reporting true for a cue that was dropped (rate limit, unknown id, bus not ready), which made the audition
        // menu count silent cues as played; or reporting false for a cue that did play.
        [Test]
        public void AudioBus_Play2D_ReturnsWhetherTheCuePlayed()
        {
            AudioBus bus = NewBus(out _);
            Assert.That(bus.Play2D(SfxIds.UiClick, AudioCategory.Ui), Is.True, "first play");
            Assert.That(bus.Play2D(SfxIds.UiClick, AudioCategory.Ui), Is.False, "inside MinInterval: dropped");
            using (new WarningCounter())
            {
                Assert.That(bus.Play2D("Nope_Cue", AudioCategory.Sfx), Is.False, "unknown id");
            }
            AudioBus unready = NewBus(out _, initialize: false);
            using (new WarningCounter())
            {
                Assert.That(unready.Play2D(SfxIds.UiClick, AudioCategory.Ui), Is.False, "bus not initialized");
            }
        }

        // Catches: an unknown override id throwing or warning every call; a bad id aborting the rest of the table; overrides set
        // before Initialize being lost (T14 applies the table after Initialize, but order must not matter).
        [Test]
        public void AudioBus_CueTable_UnknownIdWarnsOnceAndOrderWithInitializeDoesNotMatter()
        {
            AudioBus bus = NewBus(out SettingsStore settings, initialize: false);
            var table = new[]
            {
                new SfxRecipe("Nope_Cue", 1f, false, AudioCategory.Sfx, 0.5f, true, 30f, 0f, 1f),
                new SfxRecipe(SfxIds.SplashBig, 0.6f, false, AudioCategory.Sfx, 1f, true, 33f, 0.2f, 2f),
            };
            using (var w = new WarningCounter())
            {
                bus.ApplyCueTable(table);       // before Initialize
                bus.ApplyCueTable(table);
                Assert.That(w.Count("Nope_Cue"), Is.EqualTo(1), "one warning for the unknown id");
            }
            bus.Initialize(_clips, settings);
            bus.PlayAt(SfxIds.SplashBig, Vector3.zero);
            AudioSource big = LastPlayedWith(bus, SfxIds.SplashBig);
            Assert.That(big.minDistance, Is.EqualTo(2f), "the known row after the unknown one still applies");
            Assert.That(big.maxDistance, Is.EqualTo(33f));
        }

        // ---- loops (C 7.11) -----------------------------------------------------------------------------------------------

        private static List<AudioSource> LoopSources(AudioBus bus)
        {
            var list = new List<AudioSource>();
            foreach (AudioSource s in bus.GetComponentsInChildren<AudioSource>(true))
            {
                if (s.gameObject.name.StartsWith("Loop_", StringComparison.Ordinal)) list.Add(s);
            }
            return list;
        }

        // Catches: a loop that is not looping; played from a pool source; parented under the follow transform; never registered
        // (volume stays 1); spatial flag ignored; recipe distances ignored; loops rate-limited (the second start must work).
        [Test]
        public void AudioBus_StartLoop_CreatesADedicatedLoopingSource()
        {
            AudioBus bus = NewBus(out _);
            var followGo = new GameObject("Follow");
            _spawned.Add(followGo);
            followGo.transform.position = new Vector3(3f, 0f, 4f);

            AudioLoop loop = bus.StartLoop(SfxIds.LineCreak, followGo.transform, true);
            Assert.That(loop, Is.Not.Null);
            Assert.That(loop.SfxId, Is.EqualTo(SfxIds.LineCreak));
            List<AudioSource> loops = LoopSources(bus);
            Assert.That(loops.Count, Is.EqualTo(1));
            AudioSource src = loops[0];
            Assert.That(src.gameObject.name, Is.EqualTo("Loop_" + SfxIds.LineCreak));
            Assert.That(src.transform.parent, Is.SameAs(bus.transform), "a child of the bus, never of follow");
            Assert.That(src.loop, Is.True);
            Assert.That(src.clip.name, Is.EqualTo(SfxIds.LineCreak));
            Assert.That(src.spatialBlend, Is.EqualTo(1f));
            Assert.That(src.minDistance, Is.EqualTo(1f));
            Assert.That(src.maxDistance, Is.EqualTo(25f));
            Assert.That(src.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
            Assert.That(src.pitch, Is.EqualTo(1f));
            Assert.That(src.dopplerLevel, Is.EqualTo(0f));
            Assert.That(src.transform.position, Is.EqualTo(new Vector3(3f, 0f, 4f)));
            Assert.That(src.volume, Is.EqualTo(Eff(0.6f, 80, 80)).Within(1e-5f), "registered with the recipe base volume, no jitter");

            AudioLoop twoD = bus.StartLoop(SfxIds.DiveThrum, null, false);
            AudioLoop again = bus.StartLoop(SfxIds.DiveThrum, null, false);
            Assert.That(twoD, Is.Not.Null);
            Assert.That(again, Is.Not.Null, "loops are never rate-limited");
            Assert.That(LoopSources(bus).Count, Is.EqualTo(3));
            Assert.That(LastPlayedWith(bus, SfxIds.DiveThrum), Is.Null, "loops do not use the pool");
            foreach (AudioSource s in LoopSources(bus))
            {
                if (s.clip.name == SfxIds.DiveThrum) Assert.That(s.spatialBlend, Is.EqualTo(0f), "2D loop");
            }
        }

        // Catches: SetLoop not re-registering (volume unchanged), the volume reverting on the next ApplyVolumes (a settings change),
        // the pitch not clamped, NaN volume / pitch not handled, stopped / null loops throwing.
        [Test]
        public void AudioBus_SetLoop_SurvivesApplyVolumesAndClampsPitch()
        {
            AudioBus bus = NewBus(out SettingsStore settings);
            AudioLoop loop = bus.StartLoop(SfxIds.LineCreak, null, false);
            AudioSource src = LoopSources(bus)[0];

            bus.SetLoop(loop, 0.5f, 2f);
            Assert.That(src.volume, Is.EqualTo(Eff(0.6f * 0.5f, 80, 80)).Within(1e-5f));
            Assert.That(src.pitch, Is.EqualTo(2f));

            settings.Current.masterVolume = 50;
            settings.NotifyChanged();
            Assert.That(src.volume, Is.EqualTo(Eff(0.6f * 0.5f, 80, 50)).Within(1e-5f), "a settings change keeps the loop volume01");

            bus.SetLoop(loop, 3f, 100f);
            Assert.That(src.volume, Is.EqualTo(Eff(0.6f, 80, 50)).Within(1e-5f), "volume01 clamps to 1");
            Assert.That(src.pitch, Is.EqualTo(3f), "pitch clamps to 3");
            bus.SetLoop(loop, 1f, 0f);
            Assert.That(src.pitch, Is.EqualTo(0.1f).Within(1e-6f), "pitch clamps to 0.1");
            bus.SetLoop(loop, float.NaN, float.NaN);
            Assert.That(src.volume, Is.EqualTo(0f), "NaN volume01 is 0");
            Assert.That(src.pitch, Is.EqualTo(1f), "NaN pitch is 1");

            Assert.DoesNotThrow(() => bus.SetLoop(null, 1f, 1f));
            bus.StopLoop(loop);
            Assert.DoesNotThrow(() => bus.SetLoop(loop, 1f, 1f), "a stopped loop is a no-op");
        }

        // Catches: StopLoop that leaves the child source alive, is not idempotent, throws on null, leaves IsPlaying true, or does not
        // free the slot in the 16-loop cap; a cap that is off by one; a missing warn-once on the cap.
        [Test]
        public void AudioBus_StopLoop_ReleasesTheSourceAndTheCapIsSixteen()
        {
            AudioBus bus = NewBus(out _);
            AudioLoop first = bus.StartLoop(SfxIds.LineCreak, null, false);
            Assert.That(LoopSources(bus).Count, Is.EqualTo(1));
            bus.StopLoop(first);
            Assert.That(first.IsPlaying, Is.False);
            Assert.That(LoopSources(bus).Count, Is.EqualTo(0), "the child source is gone");
            Assert.DoesNotThrow(() => bus.StopLoop(first), "idempotent");
            Assert.DoesNotThrow(() => bus.StopLoop(null), "null-safe");

            var live = new List<AudioLoop>();
            for (int i = 0; i < 16; i++)
            {
                AudioLoop l = bus.StartLoop(SfxIds.LineCreak, null, false);
                Assert.That(l, Is.Not.Null, "loop " + (i + 1) + " of 16");
                live.Add(l);
            }
            using (var w = new WarningCounter())
            {
                Assert.That(bus.StartLoop(SfxIds.LineCreak, null, false), Is.Null, "the 17th loop is refused");
                Assert.That(bus.StartLoop(SfxIds.DiveThrum, null, false), Is.Null);
                Assert.That(w.Messages.Count, Is.EqualTo(1), "the cap warns once");
                Assert.That(w.Messages[0], Does.Contain("16"));
            }
            bus.StopLoop(live[3]);
            Assert.That(bus.StartLoop(SfxIds.LineCreak, null, false), Is.Not.Null, "stopping one frees a slot");
            Assert.That(LoopSources(bus).Count, Is.EqualTo(16));
        }
    }
}
