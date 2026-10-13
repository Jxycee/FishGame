using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Client-side scene ambience on the Spot_LakeDock root "Ambience"; every peer runs its own copy. Never networked.
    // Wind_Ambient loop plus a Water_Lap every 0.6-1.4 s, both through AudioBus (category Ambience).
    // M3 (T19, M3-D22): season parameter sets from SfxCueTableDef.SeasonAmbience, read through IWorldContext (Runtime cannot see Gameplay; the world
    // is attached lazily like PaletteGlobals, P6). windGain multiplies the wind's base volume (ramped, so a season change from the dev panel takes
    // about a second), windLfoHz is a slow sine wobble on that gain, lapIntervalMin/Max bound the Water_Lap delay. No world (or no table) -> the
    // M1 values (gain 1, no wobble, laps 0.6-1.4), and Summer reproduces M1 exactly: same rng stream, one draw per lap, no re-register.
    // Disables itself (with a warning) if ServiceRoots.App or an initialized AudioBus is missing, e.g. when the scene is opened alone.
    [DisallowMultipleComponent]
    public sealed class AmbienceBed : MonoBehaviour
    {
        private const float MinLapInterval = 0.6f;
        private const float MaxLapInterval = 1.4f;
        private const float LfoDepth = 0.25f;            // the wind gain swings by +/-25 % at the season's LFO rate (section F plan T19.6)
        private const float GainRampPerSecond = 0.5f;
        private const float LookupInterval = 0.5f;       // unscaled seconds between world lookups while unattached
        private const float ReRegisterEpsilon = 1e-4f;

        private AudioBus _bus;
        private AudioSource _wind;
        private Pcg32 _rng;
        private float _nextLapIn;

        private SfxCueTableDef _table;
        private IWorldContext _world;
        private float _nextLookup;
        private int _appliedSeason = -1;
        private SeasonAmbience _target = new SeasonAmbience { windGain = 1f, windLfoHz = 0f, lapIntervalMin = MinLapInterval, lapIntervalMax = MaxLapInterval };
        private float _gain = 1f;
        private float _windBase;
        private float _lastCueBase;
        private float _lfoStart;

        private void Start()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app == null)
            {
                Disable("ServiceRoots.App is null");
                return;
            }
            if (!app.TryGet<AudioBus>(out AudioBus bus) || bus == null || !bus.IsReady)
            {
                Disable("AudioBus is missing or not initialized");
                return;
            }

            _bus = bus;
            if (app.TryGet(out ContentRegistry registry) && registry != null) _table = registry.SfxCueTable;
            _lfoStart = Time.unscaledTime;
            _rng = Rng.Stream(0UL, "ambience.bed", 0UL);
            _nextLapIn = NextLapDelay();

            if (SfxRecipes.TryGet(SfxIds.WindAmbient, out SfxRecipe windRecipe)
                && _bus.TryGetClip(SfxIds.WindAmbient, out AudioClip windClip))
            {
                var windGo = new GameObject("Wind");
                windGo.transform.SetParent(transform, false);
                _wind = windGo.AddComponent<AudioSource>();
                _wind.playOnAwake = false;
                _wind.loop = true;
                _wind.spatialBlend = 0f;
                _wind.dopplerLevel = 0f;
                _wind.clip = windClip;
                _windBase = windRecipe.BaseVolume;
                _lastCueBase = _windBase;
                _bus.Register(_wind, AudioCategory.Ambience, _windBase);
                _wind.Play();
            }
            else
            {
                Debug.LogWarning("AmbienceBed: Wind_Ambient clip missing; wind stays silent.");
            }
        }

        private void OnEnable()
        {
            if (_wind != null) _wind.UnPause();
        }

        private void OnDisable()
        {
            if (_wind != null) _wind.Pause();
        }

        private void Update()
        {
            if (_bus == null) return;
            UpdateWorld();
            UpdateWind();

            _nextLapIn -= Time.deltaTime;
            if (_nextLapIn > 0f) return;
            _bus.Play2D(SfxIds.WaterLap, AudioCategory.Ambience);
            _nextLapIn = NextLapDelay();
        }

        private void OnDestroy()
        {
            Detach();
            if (_bus != null && (object)_wind != null) _bus.Unregister(_wind);
        }

        // ------------------------------------------------------------------ world (P6: lazy attach, detach on a destroyed instance)

        private void UpdateWorld()
        {
            if (_world != null)
            {
                if (_world is Object o && o == null) Detach();   // Unity fake-null: the scene unloaded
                return;
            }
            if (Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + LookupInterval;

            ServiceRegistry game = ServiceRoots.Game;
            if (game == null || !game.TryGet(out IWorldContext w) || w == null) return;
            if (w is Object destroyed && destroyed == null) return;
            _world = w;
            _world.Changed += OnWorldChanged;
            OnWorldChanged();   // the state may already be there
        }

        private void Detach()
        {
            if (_world == null) return;
            _world.Changed -= OnWorldChanged;
            _world = null;
        }

        // Weather and hotspot changes raise Changed too: only a new season does anything.
        private void OnWorldChanged()
        {
            if (_world == null || !_world.HasState) return;
            int season = (int)_world.Season;
            if (season == _appliedSeason) return;
            _appliedSeason = season;
            IReadOnlyList<SeasonAmbience> sets = _table != null ? _table.SeasonAmbience : null;
            if (sets != null && sets.Count == 4 && season >= 0 && season < 4)
            {
                _target = sets[season];
            }
            else
            {
                _target = new SeasonAmbience { windGain = 1f, windLfoHz = 0f, lapIntervalMin = MinLapInterval, lapIntervalMax = MaxLapInterval };
            }
        }

        // The gain ramps toward the season's value; the LFO is a sine wobble of depth 0.25 on it. Summer (gain 1, no LFO) never re-registers.
        private void UpdateWind() => StepWind(Time.unscaledDeltaTime, Time.unscaledTime);

        // The wind step with the clock passed in (internal test seam: WeatherFxTests drives it with a fixed delta time).
        internal void StepWind(float unscaledDeltaTime, float unscaledTime)
        {
            if (_wind == null) return;
            _gain = Mathf.MoveTowards(_gain, _target.windGain, GainRampPerSecond * unscaledDeltaTime);
            float lfo = _target.windLfoHz > 0f ? 1f + LfoDepth * Mathf.Sin(2f * Mathf.PI * _target.windLfoHz * (unscaledTime - _lfoStart)) : 1f;
            float cueBase = _windBase * _gain * lfo;
            // While ramping or wobbling, tiny changes are not worth a re-register; once SETTLED (gain at target, no wobble) any difference is, so a
            // season change always ends on the exact target volume (Summer is exactly the M2 wind).
            bool settled = _gain == _target.windGain && lfo == 1f;
            if (settled ? cueBase != _lastCueBase : Mathf.Abs(cueBase - _lastCueBase) > ReRegisterEpsilon)
            {
                _lastCueBase = cueBase;
                _bus.Register(_wind, AudioCategory.Ambience, cueBase);
            }
        }

        private float NextLapDelay()
        {
            return _rng.Range(_target.lapIntervalMin, _target.lapIntervalMax);
        }

        private void Disable(string reason)
        {
            Debug.LogWarning($"AmbienceBed disabled: {reason}.");
            enabled = false;
        }
    }
}
