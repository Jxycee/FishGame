// Authority: local only. On PlayerFP; reads its FishingRodNetwork (Announced). Never writes gameplay state (C 2.8 #17).
// The catch reveal on every peer: on Landed or ReleasedCreelFull the fish is held up at CatchHold (species model with M_Toon_Fish, or a
// FishFallbackMesh with M_Toon_FishFallback, one material per submesh). Lost and PityCast show nothing. ReleasedCreelFull drops the fish with a
// splash after the hold. A new announcement replaces a running reveal; OnDisable hides it; the owner's next cast (ChargeStart or CastRelease cue)
// dismisses it, so the reveal never blocks a recast (S10, user playtest 2026-10-13). All timing is polled in Update/LateUpdate (no
// coroutines), so a disabled component cannot finish a reveal.
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    // Order 210: after PlayerBody (200) has posed the arm, so CatchHold is this frame's pose (C 2.8 / F7 lists the default; a one-frame lag
    // would show as the fish trailing the hand while the camera turns).
    [DefaultExecutionOrder(210)]
    public sealed class CatchRevealView : MonoBehaviour
    {
        private const float ScaleInSeconds = 0.15f, DropSeconds = 0.5f, DropMeters = 1.5f, JunkLengthMeters = 0.3f, HoldRaise = 0.25f;
        private const string RevealName = "CatchReveal", SparklesName = "CatchSparkles";
        private static readonly Color GoldSparkle = new Color(1f, 0.82f, 0.25f, 1f);

        private enum Phase : byte { Hidden, Holding, Dropping }

        [SerializeField] private Material fishMaterial;       // M_Toon_Fish (Blender models, _JitterEncoded 0)
        [SerializeField] private Material fallbackMaterial;   // M_Toon_FishFallback (MeshBuilder meshes, _JitterEncoded 1; BF-7)
        [SerializeField] private Material sparkleMaterial;    // M_Particle

        private FishingRodNetwork _rod;
        private Transform _hold;
        private Transform _reveal;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private ParticleSystem _sparkles;

        private bool _live;
        private Phase _phase;
        private float _phaseStart;
        private bool _creelFull;
        private float _fullScale;
        private Vector3 _boundsCenter;
        private Vector3 _dropFrom, _dropTo;
        private Quaternion _dropRotation;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            _hold = transform.Find(PlayerRigPaths.CatchHold);

            var reveal = new GameObject(RevealName);
            _reveal = reveal.transform;
            _reveal.SetParent(transform, false);
            _filter = reveal.AddComponent<MeshFilter>();
            _renderer = reveal.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            reveal.SetActive(false);

            // Outside the reveal object, so hiding the fish does not cut in-flight sparkles short. No material, no particles (silent).
            if (sparkleMaterial != null) _sparkles = BuildSparkles();
        }

        private void OnEnable()
        {
            if (_rod == null) return;
            _rod.Announced += HandleAnnounced;
            _rod.Cue += HandleCue;
            _live = true;
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null)
            {
                _rod.Announced -= HandleAnnounced;
                _rod.Cue -= HandleCue;
            }
            Hide();
        }

        // S10 (user playtest 2026-10-13): the reveal never blocks casting, and the next cast dismisses it. The cue arrives on every peer for
        // this player's rod, so the fish also leaves the other screens when its owner casts again.
        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || _phase == Phase.Hidden) return;
            if (cue == RodCue.ChargeStart || cue == RodCue.CastRelease) Hide();
        }

        private ParticleSystem BuildSparkles()
        {
            var go = new GameObject(SparklesName);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // before any other change: Unity warns on duration edits while playing

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 0.3f;
            main.maxParticles = 128;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = sparkleMaterial;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ announcements

        private void HandleAnnounced(CatchAnnouncement a)
        {
            if (!_live || !isActiveAndEnabled) return;
            if (a.Kind != AnnounceKind.Landed && a.Kind != AnnounceKind.ReleasedCreelFull) return;   // Lost, PityCast: no reveal
            if (_hold == null || _filter == null) return;

            ContentRegistry registry = FeedbackServices.Get<ContentRegistry>();
            Mesh mesh = null;
            bool fallback = false;
            if (!a.Catch.IsJunk && registry != null && a.Catch.ItemIndex < registry.Species.Count)
            {
                FishSpeciesDef species = registry.SpeciesAt(a.Catch.ItemIndex);
                if (species != null) mesh = species.Model;
            }
            if (mesh == null)
            {
                mesh = FishFallbackMesh.Get(registry, a.Catch.IsJunk, a.Catch.ItemIndex);
                fallback = true;
            }
            if (mesh == null) return;

            Material source = fallback ? fallbackMaterial : fishMaterial;
            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = source;   // every submesh gets a material

            float length = a.Catch.IsJunk || a.Catch.LengthMm == 0 ? JunkLengthMeters : a.Catch.LengthMm / 1000f;
            float sizeZ = mesh.bounds.size.z;
            _fullScale = sizeZ > 1e-5f ? length / sizeZ : 1f;
            _boundsCenter = mesh.bounds.center;

            _filter.sharedMesh = mesh;
            _renderer.sharedMaterials = materials;
            _creelFull = a.Kind == AnnounceKind.ReleasedCreelFull;
            _phase = Phase.Holding;
            _phaseStart = Time.time;   // a new announcement replaces a running reveal
            _reveal.gameObject.SetActive(true);
            _renderer.enabled = source != null;   // no material (a builder skipped): stay invisible rather than magenta
            ApplyHoldPose(0f);

            Burst(a.Catch.Quality, a.CleanBonus);
        }

        private void Burst(CatchQuality quality, bool clean)
        {
            if (_sparkles == null) return;
            _sparkles.transform.position = _reveal.position;
            if (!_sparkles.isPlaying) _sparkles.Play();   // Emit alone does not simulate a stopped system
            _sparkles.Emit(quality == CatchQuality.Trophy ? 30 : 12);
            if (clean)
            {
                var gold = new ParticleSystem.EmitParams { startColor = GoldSparkle, applyShapeToPosition = true };
                _sparkles.Emit(gold, 20);
            }
        }

        // ------------------------------------------------------------------ playback

        private void LateUpdate()
        {
            if (!_live || _phase == Phase.Hidden) return;
            float t = Time.time - _phaseStart;

            if (_phase == Phase.Holding)
            {
                float holdEnd = ScaleInSeconds + FishingConstants.CatchHoldSeconds;
                if (t >= holdEnd)
                {
                    if (_creelFull) BeginDrop();
                    else Hide();
                    return;
                }
                ApplyHoldPose(t);
            }
            else
            {
                float u = t / DropSeconds;
                if (u >= 1f)
                {
                    LandDrop();
                    return;
                }
                Vector3 p = Vector3.Lerp(_dropFrom, _dropTo, u * u);   // accelerates under gravity
                PlaceFish(p, _dropRotation, _fullScale);
            }
        }

        private void ApplyHoldPose(float t)
        {
            float k = Mathf.Clamp01(t / ScaleInSeconds);
            float eased = k * k * (3f - 2f * k);
            Quaternion rot = Quaternion.LookRotation(transform.right, Vector3.up);   // nose to the player's right, belly down
            Vector3 pos = _hold.position + Vector3.up * HoldRaise;
            PlaceFish(pos, rot, _fullScale * eased);
            _dropRotation = rot;
        }

        private void PlaceFish(Vector3 center, Quaternion rotation, float scale)
        {
            _reveal.localScale = Vector3.one * scale;
            _reveal.SetPositionAndRotation(center - rotation * (_boundsCenter * scale), rotation);
        }

        private void BeginDrop()
        {
            _phase = Phase.Dropping;
            _phaseStart = Time.time;
            _dropFrom = _hold.position + Vector3.up * HoldRaise;
            float waterY = WaterSurface.HeightAt(_dropFrom.x, _dropFrom.z);
            float fall = Mathf.Clamp(_dropFrom.y - waterY, 0f, DropMeters);   // 1.5 m, or less when the water is closer
            _dropTo = _dropFrom + Vector3.down * fall;
        }

        private void LandDrop()
        {
            AudioBus bus = FeedbackServices.Get<AudioBus>();
            if (bus != null) bus.PlayAt(SfxIds.SplashSmall, _dropTo);
            RippleField field = RippleField.Current;
            if (field != null) field.Spawn(_dropTo.x, _dropTo.z, RippleMath.Landed);
            Hide();
        }

        private void Hide()
        {
            _phase = Phase.Hidden;
            if (_reveal != null) _reveal.gameObject.SetActive(false);
        }
    }
}
