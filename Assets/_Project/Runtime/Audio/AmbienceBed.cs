using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Client-side scene ambience on the Spot_LakeDock root "Ambience"; every peer runs its own copy. Never networked.
    // Wind_Ambient loop plus a Water_Lap every 0.6-1.4 s, both through AudioBus (category Ambience).
    // Disables itself (with a warning) if ServiceRoots.App or an initialized AudioBus is missing, e.g. when the scene is opened alone.
    [DisallowMultipleComponent]
    public sealed class AmbienceBed : MonoBehaviour
    {
        private const float MinLapInterval = 0.6f;
        private const float MaxLapInterval = 1.4f;

        private AudioBus _bus;
        private AudioSource _wind;
        private Pcg32 _rng;
        private float _nextLapIn;

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
                _bus.Register(_wind, AudioCategory.Ambience, windRecipe.BaseVolume);
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
            _nextLapIn -= Time.deltaTime;
            if (_nextLapIn > 0f) return;
            _bus.Play2D(SfxIds.WaterLap, AudioCategory.Ambience);
            _nextLapIn = NextLapDelay();
        }

        private void OnDestroy()
        {
            if (_bus != null && (object)_wind != null) _bus.Unregister(_wind);
        }

        private float NextLapDelay()
        {
            return _rng.Range(MinLapInterval, MaxLapInterval);
        }

        private void Disable(string reason)
        {
            Debug.LogWarning($"AmbienceBed disabled: {reason}.");
            enabled = false;
        }
    }
}
