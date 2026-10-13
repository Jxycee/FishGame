// Authority: local only (every peer); reads the blended weather look from DayNightController (CCR-F2), never writes gameplay state.
// M3-D21: camera-local rain particles and the 2D Rain_Patter loop while the look's rainAmount > 0. Light and fog belong to DayNightController.
// No ripples from rain: RippleField has 8 slots shared with the bite cues and rain must never push a bite ripple out. No collision (accepted for M3).
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class WeatherFx : MonoBehaviour
    {
        private const string ChildName = "RainFx";
        private const float MaxRainRate = 450f;          // drops per second at rainAmount 1, over the 24 x 24 m box
        private const float BoxSize = 24f;
        private const float EmitterHeight = 8f, EmitterForward = 4f;
        private const float RainOn = 0.001f, LoopStep = 0.01f, TintInterval = 0.25f;
        private static readonly Color RainColour = new Color(0.75f, 0.82f, 0.95f, 0.55f);

        [SerializeField] private Material particleMaterial;   // M_Particle; null = no particles (silent)

        private DayNightController _dayNight;
        private ParticleSystem _rain;
        private float _lastRate = -1f;
        private float _nextTint;

        private AudioBus _bus;
        private AudioLoop _loop;
        private float _lastLoopRain = -1f;

        private void Awake()
        {
            _dayNight = GetComponent<DayNightController>();
            if (particleMaterial == null) return;

            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            // The Box shape emits along its local +Z. Euler(90, 0, 0) turns +Z to world down (-Y) and local +Y to world +Z, so the 24 x 24 m
            // footprint lies in the world XZ plane and the drops fall straight down.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // Unity warns about edits to a playing system

            ParticleSystem.MainModule main = _rain.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.1f;
            main.startSpeed = 10f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.025f);
            main.startColor = RainColour;
            main.gravityModifier = 0.5f;
            main.maxParticles = 700;
            // Automatic culling can pause an off-screen system, and under -batchmode -nographics nothing is rendered: always simulate.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(BoxSize, BoxSize, 0.1f);

            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = particleMaterial;
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.06f;
            psr.lengthScale = 1f;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            _rain.Play();
        }

        private void LateUpdate()
        {
            float rain = 0f;
            if (_dayNight != null && _dayNight.isActiveAndEnabled) rain = Mathf.Clamp01(_dayNight.ActiveLook.rainAmount);

            UpdateParticles(rain);
            UpdateLoop(rain);
        }

        private void UpdateParticles(float rain)
        {
            if (_rain == null) return;

            Camera cam = Camera.main;
            if (cam == null) rain = 0f;   // no camera this frame: no rain
            else
            {
                Transform ct = cam.transform;
                Vector3 flat = ct.forward;
                flat.y = 0f;
                flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.zero;
                _rain.transform.position = ct.position + new Vector3(0f, EmitterHeight, 0f) + flat * EmitterForward;
            }

            float rate = MaxRainRate * rain;
            if (!Mathf.Approximately(rate, _lastRate))   // particles already falling finish their life: no pop when rain ends
            {
                _lastRate = rate;
                ParticleSystem.EmissionModule emission = _rain.emission;
                emission.rateOverTime = rate;
            }

            if (rate > 0f && Time.unscaledTime >= _nextTint && _dayNight != null)
            {
                _nextTint = Time.unscaledTime + TintInterval;
                ParticleSystem.MainModule main = _rain.main;
                main.startColor = RainColour * _dayNight.ParticleTint;   // unlit particles would glow at night: tint new particles by the light
            }
        }

        // The Rain_Patter loop is 2D, so its position is irrelevant: its follow is this transform (C2 16.2: the loop dies with its owner; the
        // camera is NOT used because a camera switch would kill the loop).
        private void UpdateLoop(float rain)
        {
            if ((object)_bus != null && _bus == null)   // Unity fake-null: the App was torn down, so its loops are gone
            {
                _bus = null;
                _loop = null;
            }
            if (rain > RainOn)
            {
                if (_bus == null)
                {
                    ServiceRegistry app = ServiceRoots.App;
                    if (app != null && app.TryGet(out AudioBus bus) && bus != null) _bus = bus;
                }
                if (_bus == null) return;
                if (_loop == null)
                {
                    _loop = _bus.StartLoop(SfxIds.RainPatter, transform, false);
                    _lastLoopRain = -1f;
                }
                if (_loop != null && Mathf.Abs(rain - _lastLoopRain) > LoopStep)
                {
                    _lastLoopRain = rain;
                    _bus.SetLoop(_loop, rain, 1f);
                }
            }
            else if (_loop != null)
            {
                StopLoop();
            }
        }

        private void StopLoop()
        {
            if (_loop == null) return;
            if (_bus != null) _bus.StopLoop(_loop);
            _loop = null;
            _lastLoopRain = -1f;
        }

        private void OnDisable()
        {
            StopLoop();
            if (_rain != null)
            {
                ParticleSystem.EmissionModule emission = _rain.emission;
                emission.rateOverTime = 0f;
                _lastRate = -1f;
            }
        }
    }
}
