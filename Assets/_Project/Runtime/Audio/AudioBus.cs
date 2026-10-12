using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Client-side presentation audio only; never networked. Lives on the App root (Bootstrap, DontDestroyOnLoad).
    // Volume = cueBase * category^2 * master^2 (AudioMath.EffectiveVolume, settings 0..100); re-applied on ISettings.Changed.
    // Per play: pitch +/-7 % (times the caller multiplier), volume +/-1.5 dB (Pcg32); missing clip -> warning once per (reason, id), silent.
    // M2: loops (dedicated sources), ducking of Ambience/Music, per-play pitch multiplier and MinDistance, cue-table overrides (ApplyCueTable, C M2-D11).
    // Rate limit: Play2D per id, PlayAt per id + 2 m cell.
    [DisallowMultipleComponent]
    public sealed class AudioBus : MonoBehaviour
    {
        private const int PoolSize = 8;
        private const int MaxLoops = 16;
        private const int MaxRateEntries = 512;      // per-cell rate-limit keys are cleared past this
        private const float DefaultMaxDistance = 30f;
        private const float CellSize = 2f;           // PlayAt rate-limit cell edge, metres
        private const float DuckTarget = 0.5f, DuckSeconds = 0.3f, MaxDuckHoldSeconds = 60f;

        private struct Entry
        {
            public AudioCategory Category;
            public float CueBase;
        }

        private readonly Dictionary<AudioSource, Entry> _entries = new Dictionary<AudioSource, Entry>();
        private readonly List<AudioSource> _dead = new List<AudioSource>();
        private readonly Dictionary<(string, bool, int, int), float> _lastPlayed = new Dictionary<(string, bool, int, int), float>();
        private readonly Dictionary<string, SfxRecipe> _cueTable = new Dictionary<string, SfxRecipe>(StringComparer.Ordinal);
        private readonly List<AudioLoop> _loops = new List<AudioLoop>();
        private readonly LogOnce _warned = new LogOnce(); // per AudioBus lifetime; never reset
        private SfxGenerator _generator;
        private ISettings _settings;
        private AudioSource[] _pool;
        private int _poolCursor;
        private Pcg32 _jitter;
        private float _duckGain = 1f;                    // 1 = none; applied to Ambience and Music
        private float _duckHoldUntil;                    // Time.unscaledTime

        internal bool IsReady => _generator != null && _pool != null;

        public void Initialize(SfxGenerator generator, ISettings settings)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            _generator = generator;
            _settings = settings;
            _settings.Changed += OnSettingsChanged;
            _jitter = Rng.Stream(0UL, "audio.jitter", 0UL);
            BuildPool();
            ApplyVolumes();
        }

        public void Register(AudioSource source, AudioCategory category, float cueBase)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _entries[source] = new Entry { Category = category, CueBase = cueBase };
            source.volume = Effective(category, cueBase);
        }

        public void Unregister(AudioSource source)
        {
            if ((object)source != null) _entries.Remove(source);
        }

        public void PlayUi(string sfxId)
        {
            Play2D(sfxId, AudioCategory.Ui);
        }

        public void Play2D(string sfxId, AudioCategory category)
        {
            if (!TryPrepare(sfxId, false, Vector3.zero, out SfxRecipe recipe, out AudioClip clip)) return;
            Play(clip, recipe, category, false, Vector3.zero, 1f);
        }

        public void PlayAt(string sfxId, Vector3 position)
        {
            PlayAt(sfxId, position, 1f);
        }

        // pitchMult multiplies the +/-7 % jitter (clamped 0.1..3, NaN = 1). Rate limit per id + 2 m cell (floor(x/2), floor(z/2)).
        public void PlayAt(string sfxId, Vector3 position, float pitchMult)
        {
            if (!TryPrepare(sfxId, true, position, out SfxRecipe recipe, out AudioClip clip)) return;
            Play(clip, recipe, recipe.Category, true, position, pitchMult);
        }

        public void ApplyVolumes()
        {
            _dead.Clear();
            foreach (KeyValuePair<AudioSource, Entry> pair in _entries)
            {
                if (pair.Key == null)
                {
                    _dead.Add(pair.Key);
                    continue;
                }
                pair.Key.volume = Effective(pair.Value.Category, pair.Value.CueBase);
            }
            for (int i = 0; i < _dead.Count; i++)
            {
                _entries.Remove(_dead[i]);
            }
            _dead.Clear();
        }

        // Ambience and Music go to 50 % over 0.3 s, are held for `seconds` after that, then return over 0.3 s. Repeats extend the hold
        // (never below 50 %). Runs on unscaled time, so a paused game still ducks.
        public void Duck(float seconds)
        {
            float hold = float.IsNaN(seconds) || seconds < 0f ? 0f : Mathf.Min(seconds, MaxDuckHoldSeconds);
            _duckHoldUntil = Mathf.Max(_duckHoldUntil, Time.unscaledTime + DuckSeconds + hold);
        }

        // Mixing overrides from SfxCueTableDef by id (category, base, spatial, min/max distance, minInterval). Core Id, length and loop are
        // kept. A non-null call replaces the previous table; null restores the Core defaults. Takes effect from the next play; running
        // loops keep their values. Safe before or after Initialize.
        public void ApplyCueTable(IReadOnlyList<SfxRecipe> overrides)
        {
            _cueTable.Clear();
            if (overrides == null) return;
            for (int i = 0; i < overrides.Count; i++)
            {
                SfxRecipe o = overrides[i];
                if (!SfxRecipes.TryGet(o.Id, out SfxRecipe core))
                {
                    if (_warned.ShouldLog("cue-unknown|" + (o.Id ?? ""))) Debug.LogWarning($"Cue table override for unknown SFX id '{o.Id}' ignored.");
                    continue;
                }
                float baseVolume = float.IsNaN(o.BaseVolume) ? core.BaseVolume : Mathf.Clamp01(o.BaseVolume);
                float minDistance = o.MinDistance > 0f ? o.MinDistance : 1f;
                float maxDistance = o.MaxDistance > 0f ? o.MaxDistance : DefaultMaxDistance;
                if (maxDistance < minDistance) maxDistance = minDistance;
                float minInterval = o.MinInterval >= 0f ? o.MinInterval : 0f;
                _cueTable[core.Id] = new SfxRecipe(core.Id, core.LengthSeconds, core.Loop, o.Category, baseVolume, o.Spatial,
                    maxDistance, minInterval, minDistance);
            }
        }

        // A looped cue on its own AudioSource (a child "Loop_<id>"), never rate-limited, at most 16 at once. `follow` is tracked in
        // LateUpdate and the loop stops when it is destroyed; null follow = stays at the bus (2D loops). Returns null when the bus is not
        // ready, the recipe or clip is missing, or the cap is reached (each warns once).
        public AudioLoop StartLoop(string sfxId, Transform follow, bool spatial)
        {
            if (!Resolve(sfxId, out SfxRecipe recipe, out AudioClip clip)) return null;
            if (_loops.Count >= MaxLoops)
            {
                if (_warned.ShouldLog("loop-cap")) Debug.LogWarning($"AudioBus: {MaxLoops} loops are already running; '{sfxId}' loop not started.");
                return null;
            }
            var go = new GameObject("Loop_" + recipe.Id);
            go.transform.SetParent(transform, false);
            go.transform.position = follow != null ? follow.position : transform.position;
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.clip = clip;
            src.loop = true;
            src.dopplerLevel = 0f;
            src.spatialBlend = spatial ? 1f : 0f;
            src.minDistance = recipe.MinDistance > 0f ? recipe.MinDistance : 1f;
            src.maxDistance = recipe.MaxDistance > 0f ? recipe.MaxDistance : DefaultMaxDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.pitch = 1f;
            var loop = new AudioLoop(recipe.Id, src, follow, recipe.Category, recipe.BaseVolume);
            _loops.Add(loop);
            Register(src, recipe.Category, recipe.BaseVolume);
            src.Play();
            return loop;
        }

        // volume01 0..1 (NaN = 0) scales the recipe base volume and survives ApplyVolumes; pitch is clamped 0.1..3 (NaN = 1).
        public void SetLoop(AudioLoop loop, float volume01, float pitchMult)
        {
            if (loop == null || loop.Stopped || loop.Source == null) return;
            loop.Volume01 = float.IsNaN(volume01) ? 0f : Mathf.Clamp01(volume01);
            Register(loop.Source, loop.Category, loop.BaseVolume * loop.Volume01);
            loop.Source.pitch = float.IsNaN(pitchMult) ? 1f : Mathf.Clamp(pitchMult, 0.1f, 3f);
        }

        // Idempotent and null-safe.
        public void StopLoop(AudioLoop loop)
        {
            if (loop == null || loop.Stopped) return;
            loop.Stopped = true;
            _loops.Remove(loop);
            AudioSource src = loop.Source;
            if (src == null) return;
            src.Stop();
            Unregister(src);
            if (Application.isPlaying) Destroy(src.gameObject);
            else DestroyImmediate(src.gameObject);
        }

        internal bool TryGetClip(string sfxId, out AudioClip clip)
        {
            clip = null;
            return _generator != null && _generator.TryGetClip(sfxId, out clip);
        }

        private void Update()
        {
            float target = Time.unscaledTime < _duckHoldUntil ? DuckTarget : 1f;
            if (_duckGain == target) return;
            _duckGain = Mathf.MoveTowards(_duckGain, target, Time.unscaledDeltaTime * (1f - DuckTarget) / DuckSeconds);
            ApplyVolumes();
        }

        private void LateUpdate()
        {
            for (int i = _loops.Count - 1; i >= 0; i--)
            {
                AudioLoop loop = _loops[i];
                if (loop.Stopped || loop.Source == null)
                {
                    StopLoop(loop);
                    continue;
                }
                if ((object)loop.Follow == null) continue;
                if (loop.Follow == null) StopLoop(loop);
                else loop.Source.transform.position = loop.Follow.position;
            }
        }

        private void OnSettingsChanged(SettingsData _)
        {
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            for (int i = 0; i < _loops.Count; i++)
            {
                AudioLoop loop = _loops[i];
                loop.Stopped = true;
                if (loop.Source == null) continue;
                loop.Source.Stop();
                Unregister(loop.Source);
            }
            _loops.Clear();
        }

        private float Effective(AudioCategory category, float cueBase)
        {
            float v = AudioMath.EffectiveVolume(cueBase, CategoryPercent(category), MasterPercent());
            return category == AudioCategory.Ambience || category == AudioCategory.Music ? v * _duckGain : v;
        }

        private void BuildPool()
        {
            if (_pool != null) return;
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("SfxSource" + i);
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 1f;
                src.maxDistance = DefaultMaxDistance;
                src.dopplerLevel = 0f;
                src.spatialBlend = 0f;
                _pool[i] = src;
            }
        }

        // Round-robin; prefers an idle source. When all 8 are busy the oldest-in-order one is cut.
        private AudioSource AcquireSource()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                int idx = (_poolCursor + i) % _pool.Length;
                if (!_pool[idx].isPlaying)
                {
                    _poolCursor = (idx + 1) % _pool.Length;
                    return _pool[idx];
                }
            }
            AudioSource reuse = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            return reuse;
        }

        // The cue-table override for the id, else the Core recipe.
        private bool TryGetRecipe(string sfxId, out SfxRecipe recipe)
        {
            if (sfxId != null && _cueTable.TryGetValue(sfxId, out recipe)) return true;
            return SfxRecipes.TryGet(sfxId, out recipe);
        }

        private bool Resolve(string sfxId, out SfxRecipe recipe, out AudioClip clip)
        {
            recipe = default;
            clip = null;
            if (!IsReady)
            {
                if (_warned.ShouldLog("not-ready|" + (sfxId ?? ""))) Debug.LogWarning($"AudioBus not initialized; '{sfxId}' stays silent.");
                return false;
            }
            if (!TryGetRecipe(sfxId, out recipe))
            {
                if (_warned.ShouldLog("unknown|" + (sfxId ?? ""))) Debug.LogWarning($"Unknown SFX id '{sfxId}'; staying silent.");
                return false;
            }
            if (!TryGetClip(sfxId, out clip))
            {
                if (_warned.ShouldLog("missing|" + (sfxId ?? ""))) Debug.LogWarning($"SFX clip '{sfxId}' is missing; staying silent.");
                return false;
            }
            return true;
        }

        private bool TryPrepare(string sfxId, bool spatial, Vector3 position, out SfxRecipe recipe, out AudioClip clip)
        {
            return Resolve(sfxId, out recipe, out clip) && AllowPlay(recipe, spatial, position);
        }

        // Play2D: keyed by id. PlayAt: keyed by id + the 2 m cell, so two bobbers are not merged into one cue (spec 11.7, C-Q6).
        private bool AllowPlay(SfxRecipe recipe, bool spatial, Vector3 position)
        {
            float now = Time.unscaledTime;
            int cx = spatial ? Mathf.FloorToInt(position.x / CellSize) : 0;
            int cz = spatial ? Mathf.FloorToInt(position.z / CellSize) : 0;
            (string, bool, int, int) key = (recipe.Id, spatial, cx, cz);
            if (_lastPlayed.TryGetValue(key, out float last) && now - last < recipe.MinInterval) return false;
            if (_lastPlayed.Count >= MaxRateEntries) _lastPlayed.Clear();
            _lastPlayed[key] = now;
            return true;
        }

        // The pool is reused: every property a recipe can change is set on every play, so nothing sticks from the previous cue.
        private void Play(AudioClip clip, SfxRecipe recipe, AudioCategory category, bool spatial, Vector3 position, float pitchMult)
        {
            AudioSource src = AcquireSource();
            src.Stop();
            src.clip = clip;
            src.loop = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.minDistance = recipe.MinDistance > 0f ? recipe.MinDistance : 1f;
            src.maxDistance = recipe.MaxDistance > 0f ? recipe.MaxDistance : DefaultMaxDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.transform.position = position;
            float mult = float.IsNaN(pitchMult) ? 1f : Mathf.Clamp(pitchMult, 0.1f, 3f);
            src.pitch = (1f + SignedUnit() * AudioMath.PitchJitter) * mult;
            float jitterGain = AudioMath.DbToLinear(SignedUnit() * AudioMath.VolumeJitterDb);
            Register(src, category, recipe.BaseVolume * jitterGain);
            src.Play();
        }

        private float SignedUnit()
        {
            return _jitter.NextFloat() * 2f - 1f;
        }

        private int CategoryPercent(AudioCategory category)
        {
            if (_settings == null) return 100;
            SettingsData d = _settings.Current;
            switch (category)
            {
                case AudioCategory.Sfx: return d.sfxVolume;
                case AudioCategory.Ambience: return d.ambienceVolume;
                case AudioCategory.Music: return d.musicVolume;
                case AudioCategory.Ui: return d.uiVolume;
                case AudioCategory.Voice: return d.voiceVolume;
                default: return 100;
            }
        }

        private int MasterPercent()
        {
            return _settings == null ? 100 : _settings.Current.masterVolume;
        }
    }
}
