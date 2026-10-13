// Authority: local only (every peer); reads the replicated WorldState.NetHotspot only (never DevWorldDriver or a planner), never writes gameplay state.
// Spec 12.7: bubbling splash particles and a ripple every 1.2 s at the active hotspot, on every peer (late joiners included: polled, not event-driven).
// Cut-line (skeleton risk 8): with no hotspot spawned (InHotspot off) this component simply stays idle.
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class HotspotFx : MonoBehaviour
    {
        private const string ChildName = "HotspotFx", BubblesName = "Bubbles";
        private const float BubblesPerSecond = 6f, PopPeriod = 0.5f, RipplePeriod = 1.2f;
        private const float BubbleSpread = 0.6f, RippleSpread = 0.5f, RippleStrength = 0.4f;
        private const int DropletsPerPop = 3, MaxEmitPerFrame = 8;
        private const float TintInterval = 0.25f;
        private static readonly Color DropletColour = new Color(0.85f, 0.93f, 1f, 0.9f);

        [SerializeField] private Material particleMaterial;   // M_Particle; null = no particles (ripples still run)

        private DayNightController _dayNight;
        private ParticleSystem _drops, _bubbles;
        private bool _wasActive;
        private float _bubbleAcc, _popTimer, _rippleTimer;
        private float _nextTint;

        private void Awake()
        {
            _dayNight = GetComponent<DayNightController>();
            if (particleMaterial == null) return;   // silent no-op for the particles

            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            _drops = Configure(go, 1f, 0.04f, 0.08f);

            var bubbles = new GameObject(BubblesName);
            bubbles.transform.SetParent(go.transform, false);
            _bubbles = Configure(bubbles, -0.2f, 0.03f, 0.03f);
        }

        // Config order follows SplashFx: stop and clear first, then configure, then play. Emission is off: particles come from Emit only.
        private ParticleSystem Configure(GameObject go, float gravity, float sizeMin, float sizeMax)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = DropletColour;
            main.gravityModifier = gravity;
            main.maxParticles = 200;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // emitted particles must also die when nothing renders (-nographics)

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = particleMaterial;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private void OnDisable()
        {
            _wasActive = false;
        }

        // Polled every frame from the replicated hotspot, so a client that joins mid-hotspot shows it without a replay. The client's own clock
        // hides a stale hotspot the host has not cleared yet (StartMinute is not replicated).
        private void Update()
        {
            WorldState w = WorldState.Current;
            bool active = false;
            HotspotState h = default;
            if (w != null && w.HasState)
            {
                h = w.NetHotspot.Value;
                active = h.Active != 0 && h.Radius > 0f && w.MinuteF < h.EndMinute;
            }
            if (!active)
            {
                _wasActive = false;
                _bubbleAcc = 0f;
                return;
            }

            float dt = Time.deltaTime;
            if (!_wasActive)
            {
                _wasActive = true;
                _bubbleAcc = 0f;
                _popTimer = PopPeriod;
                _rippleTimer = 0f;   // the first ripple lands on the frame the hotspot appears
            }

            _rippleTimer -= dt;
            if (_rippleTimer <= 0f)
            {
                _rippleTimer = RipplePeriod;
                Vector2 p = Random.insideUnitCircle * (RippleSpread * h.Radius);
                RippleField field = RippleField.Current;
                if (field != null) field.Spawn(h.X + p.x, h.Z + p.y, RippleStrength);
            }

            if (_drops == null) return;

            if (_dayNight != null && Time.unscaledTime >= _nextTint)
            {
                _nextTint = Time.unscaledTime + TintInterval;
                Color tint = DropletColour * _dayNight.ParticleTint;
                ParticleSystem.MainModule dm = _drops.main;
                dm.startColor = tint;
                ParticleSystem.MainModule bm = _bubbles.main;
                bm.startColor = tint;
            }

            _bubbleAcc += BubblesPerSecond * dt;
            int emitted = 0;
            while (_bubbleAcc >= 1f && emitted < MaxEmitPerFrame)
            {
                _bubbleAcc -= 1f;
                emitted++;
                Vector2 p = Random.insideUnitCircle * (BubbleSpread * h.Radius);
                float x = h.X + p.x, z = h.Z + p.y;
                Vector3 v = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.3f, 0.6f), Random.Range(-0.1f, 0.1f));
                Emit(_bubbles, new Vector3(x, WaterSurface.HeightAt(x, z), z), v);
            }
            if (_bubbleAcc > 1f) _bubbleAcc = 1f;

            _popTimer -= dt;
            if (_popTimer <= 0f)
            {
                _popTimer = PopPeriod;
                Vector2 p = Random.insideUnitCircle * (BubbleSpread * h.Radius);
                float x = h.X + p.x, z = h.Z + p.y;
                Vector3 at = new Vector3(x, WaterSurface.HeightAt(x, z), z);
                for (int i = 0; i < DropletsPerPop; i++) Emit(_drops, at, ConeUp(25f) * Random.Range(1f, 2f));
            }
        }

        private static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity)
        {
            if (!ps.isPlaying) ps.Play();
            var p = new ParticleSystem.EmitParams { position = position, velocity = velocity };
            ps.Emit(p, 1);
        }

        private static Vector3 ConeUp(float halfAngleDeg)
        {
            float a = Random.Range(0f, halfAngleDeg) * Mathf.Deg2Rad;
            float phi = Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(phi), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(phi));
        }
    }
}
