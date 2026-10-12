using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Client-side presentation audio only; never networked. Lives on the App root (Bootstrap, DontDestroyOnLoad).
    // Volume = cueBase * category^2 * master^2 (AudioMath.EffectiveVolume, settings 0..100); re-applied on ISettings.Changed.
    // Per play: pitch +/-7 %, volume +/-1.5 dB (Pcg32); rate limit per id by the recipe MinInterval; missing clip -> warning once per (reason, id), silent.
    // Ducking and SfxCueTableDef (spec 11.7) are later milestones; M1 cue data comes from SfxRecipes.M1.
    [DisallowMultipleComponent]
    public sealed class AudioBus : MonoBehaviour
    {
        private const int PoolSize = 8;
        private const float DefaultMaxDistance = 30f;

        private struct Entry
        {
            public AudioCategory Category;
            public float CueBase;
        }

        private readonly Dictionary<AudioSource, Entry> _entries = new Dictionary<AudioSource, Entry>();
        private readonly List<AudioSource> _dead = new List<AudioSource>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly LogOnce _warned = new LogOnce(); // per AudioBus lifetime; never reset
        private SfxGenerator _generator;
        private ISettings _settings;
        private AudioSource[] _pool;
        private int _poolCursor;
        private Pcg32 _jitter;

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
            source.volume = AudioMath.EffectiveVolume(cueBase, CategoryPercent(category), MasterPercent());
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
            if (!TryPrepare(sfxId, out SfxRecipe recipe, out AudioClip clip)) return;
            Play(clip, recipe, category, false, Vector3.zero);
        }

        public void PlayAt(string sfxId, Vector3 position)
        {
            if (!TryPrepare(sfxId, out SfxRecipe recipe, out AudioClip clip)) return;
            Play(clip, recipe, recipe.Category, true, position);
        }

        public void ApplyVolumes()
        {
            int master = MasterPercent();
            _dead.Clear();
            foreach (KeyValuePair<AudioSource, Entry> pair in _entries)
            {
                if (pair.Key == null)
                {
                    _dead.Add(pair.Key);
                    continue;
                }
                pair.Key.volume = AudioMath.EffectiveVolume(pair.Value.CueBase, CategoryPercent(pair.Value.Category), master);
            }
            for (int i = 0; i < _dead.Count; i++)
            {
                _entries.Remove(_dead[i]);
            }
            _dead.Clear();
        }

        internal bool TryGetClip(string sfxId, out AudioClip clip)
        {
            clip = null;
            return _generator != null && _generator.TryGetClip(sfxId, out clip);
        }

        private void OnSettingsChanged(SettingsData _)
        {
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
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

        private bool TryPrepare(string sfxId, out SfxRecipe recipe, out AudioClip clip)
        {
            recipe = default;
            clip = null;
            if (!IsReady)
            {
                if (_warned.ShouldLog("not-ready|" + (sfxId ?? ""))) Debug.LogWarning($"AudioBus not initialized; '{sfxId}' stays silent.");
                return false;
            }
            if (!SfxRecipes.TryGet(sfxId, out recipe))
            {
                if (_warned.ShouldLog("unknown|" + (sfxId ?? ""))) Debug.LogWarning($"Unknown SFX id '{sfxId}'; staying silent.");
                return false;
            }
            if (!TryGetClip(sfxId, out clip))
            {
                if (_warned.ShouldLog("missing|" + (sfxId ?? ""))) Debug.LogWarning($"SFX clip '{sfxId}' is missing; staying silent.");
                return false;
            }
            return AllowPlay(recipe);
        }

        private bool AllowPlay(SfxRecipe recipe)
        {
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(recipe.Id, out float last) && now - last < recipe.MinInterval) return false;
            _lastPlayed[recipe.Id] = now;
            return true;
        }

        private void Play(AudioClip clip, SfxRecipe recipe, AudioCategory category, bool spatial, Vector3 position)
        {
            AudioSource src = AcquireSource();
            src.Stop();
            src.clip = clip;
            src.loop = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.maxDistance = recipe.MaxDistance > 0f ? recipe.MaxDistance : DefaultMaxDistance;
            src.transform.position = position;
            src.pitch = 1f + SignedUnit() * AudioMath.PitchJitter;
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
