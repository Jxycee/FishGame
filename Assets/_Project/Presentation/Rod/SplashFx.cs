// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.
// Splash particles (spec 9.11 phase cues, 10.5, C 7.24) on every peer.
//  - One-shot bursts come only from Cue, at the bobber's x/z on the water: Landing 12, Nibble 4 small, Bite 30 + 10 droplets, HookSet 10 droplets,
//    Snapped 25, LineLanded 15, Escaped (not the recall's own) 10 slow rising bubbles.
//  - State-driven effects are POLLED, so a late joiner sees them without a replay (F6): the side-run column (20/s at the fish point, up 2.5 m/s plus
//    1.5 m/s sideways, signed by the change of ViewState.Lateral) while Hooked and FlagBurst WITHOUT FlagDive (a dive has no sideways splash); and
//    3 particles every 0.6 s while FlagExhausted.
//  - Two runtime ParticleSystems with M_Particle: "SplashFx" (gravity 1, droplets) and its child "Bubbles" (gravity -0.2; one system cannot hold
//    both gravities). A null particleMaterial means no particles (silent).
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    public sealed class SplashFx : MonoBehaviour
    {
        private const string ChildName = "SplashFx", BubblesName = "Bubbles";
        private const float SideRunPerSecond = 20f, ExhaustedPeriod = 0.6f;
        private const int MaxEmitPerFrame = 8;
        private static readonly Color DropletColor = new Color(0.85f, 0.93f, 1f, 0.9f);

        [SerializeField] private Material particleMaterial;   // M_Particle

        private FishingRodNetwork _rod;
        private ParticleSystem _drops, _bubbles;
        private bool _live;

        private float _sideAcc, _lastLateral, _sideSign = 1f;
        private bool _haveLateral;
        private float _exhaustedAt = float.NegativeInfinity;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            if (particleMaterial == null) return;   // silent no-op

            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            _drops = Configure(go, 1f, 0.04f, 0.08f);
            ParticleSystem.ShapeModule shape = _drops.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;

            var bubbles = new GameObject(BubblesName);
            bubbles.transform.SetParent(go.transform, false);
            _bubbles = Configure(bubbles, -0.2f, 0.03f, 0.03f);
        }

        // Config order follows CatchRevealView: stop and clear first (Unity warns about edits to a playing system), then configure, then play.
        // Looping with emission off keeps the system alive for Emit calls without any per-frame cost worth measuring.
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
            main.startColor = DropletColor;
            main.gravityModifier = gravity;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = particleMaterial;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private void OnEnable()
        {
            if (_rod == null) return;
            _rod.Cue += HandleCue;
            _live = true;
            _sideAcc = 0f;
            _haveLateral = false;
            _exhaustedAt = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null) _rod.Cue -= HandleCue;
        }

        // ------------------------------------------------------------------ cues (one-shots)

        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || !isActiveAndEnabled || _drops == null) return;

            RodViewState vs = _rod.ViewState;
            Vector3 at = WaterPoint(vs.BobberPos);
            switch (cue)
            {
                case RodCue.Landing:
                    Drops(at, 12, 1.5f, 3f);
                    break;
                case RodCue.Nibble:
                    Drops(at, 4, 0.8f, 1.5f, 0.025f);   // 4 small drops
                    break;
                case RodCue.Bite:
                    Drops(at, 30, 1.5f, 3.2f);
                    Drops(at, 10, 2.5f, 4f);   // the higher droplets
                    break;
                case RodCue.HookSet:
                    Drops(at, 10, 2f, 3.5f);
                    break;
                case RodCue.Snapped:
                    Drops(at, 25, 1.5f, 3.5f);
                    break;
                case RodCue.LineLanded:
                    Drops(at, 15, 1.5f, 3f);
                    break;
                case RodCue.Escaped:
                    if ((vs.Flags & RodWireQuant.FlagRecall) == 0) Bubbles(at, 10);
                    break;
            }
        }

        // ------------------------------------------------------------------ polled effects (late-join safe)

        private void Update()
        {
            if (!_live || _drops == null || _rod == null || !_rod.IsSpawned) return;

            RodViewState vs = _rod.ViewState;
            float now = Time.time;
            float dt = Time.deltaTime;
            bool hooked = vs.State == RodState.Hooked;
            bool burst = hooked && (vs.Flags & RodWireQuant.FlagBurst) != 0;
            bool dive = (vs.Flags & RodWireQuant.FlagDive) != 0;

            // The sideways direction follows the frame-to-frame change of Lateral (positive lateral = right of the cast line, FishPointMath).
            if (hooked)
            {
                if (_haveLateral)
                {
                    float delta = vs.Lateral - _lastLateral;
                    if (Mathf.Abs(delta) > 1e-4f) _sideSign = delta > 0f ? 1f : -1f;
                }
                _lastLateral = vs.Lateral;
                _haveLateral = true;
            }
            else
            {
                _haveLateral = false;
            }

            if (burst && !dive && IsFinite(vs.BobberPos))
            {
                _sideAcc += SideRunPerSecond * dt;
                Vector3 at = WaterPoint(vs.BobberPos);
                Vector3 flat = at - transform.position;
                flat.y = 0f;
                Vector3 forward = flat.sqrMagnitude > 1e-6f ? flat.normalized : transform.forward;
                Vector3 right = new Vector3(forward.z, 0f, -forward.x);   // Unity: right = cross(up, forward)
                int emitted = 0;
                while (_sideAcc >= 1f && emitted < MaxEmitPerFrame)
                {
                    _sideAcc -= 1f;
                    emitted++;
                    Vector3 v = Vector3.up * 2.5f + right * (1.5f * _sideSign) + Random.insideUnitSphere * 0.3f;
                    Emit(_drops, at, v);
                }
                if (_sideAcc > 1f) _sideAcc = 1f;
            }
            else
            {
                _sideAcc = 0f;
            }

            if (hooked && (vs.Flags & RodWireQuant.FlagExhausted) != 0 && IsFinite(vs.BobberPos))
            {
                if (now - _exhaustedAt >= ExhaustedPeriod)
                {
                    _exhaustedAt = now;
                    Drops(WaterPoint(vs.BobberPos), 3, 0.4f, 0.9f);
                }
            }
            else
            {
                _exhaustedAt = float.NegativeInfinity;
            }
        }

        // ------------------------------------------------------------------ emit helpers

        private void Drops(Vector3 at, int count, float speedMin, float speedMax, float startSize = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = ConeUp(25f) * Random.Range(speedMin, speedMax);
                Emit(_drops, at, v, startSize);
            }
        }

        private void Bubbles(Vector3 at, int count)
        {
            if (_bubbles == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.3f, 0.6f), Random.Range(-0.15f, 0.15f));
                Emit(_bubbles, at + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f)), v);
            }
        }

        private static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float startSize = 0f)
        {
            if (!ps.isPlaying) ps.Play();
            var p = new ParticleSystem.EmitParams { position = position, velocity = velocity };
            if (startSize > 0f) p.startSize = startSize;   // 0 = the system's own size
            ps.Emit(p, 1);
        }

        private static Vector3 ConeUp(float halfAngleDeg)
        {
            float a = Random.Range(0f, halfAngleDeg) * Mathf.Deg2Rad;
            float phi = Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(phi), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(phi));
        }

        private static Vector3 WaterPoint(Vector3 bobberPos)
        {
            return new Vector3(bobberPos.x, WaterSurface.HeightAt(bobberPos.x, bobberPos.z), bobberPos.z);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
