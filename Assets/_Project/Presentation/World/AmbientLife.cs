// Authority: local only (every peer runs its own copy); purely visual. Never writes gameplay state, owns no collider and moves no collider.
// Harbour round 0 (2026-10-14): the map's ambient life from the Blender manifest's animHints, wired by HarbourPierBuilder:
//  - Motion: cheap script motion on visual-only transforms (bobbing floats and the moored tug's hull visual, swaying flags, bunting and nets,
//    the windsock, the swinging shop sign, the weather vane's head, perched gulls that look around and hop). Each entry stores its base pose
//    (captured by the builder) and Pose(t) writes base * offset, so nothing drifts and a capture can pose any time t.
//  - Lights: lamp point lights and glow spheres follow the _FGLampGlow global (DayNightMath.LampGlow, pushed by DayNightController); the
//    lighthouse beacon sweeps; the tug's mast light blinks.
//  - Detail: the collider-free detail objects are hidden while GraphicsApplier.SceneDetailVisible is false (the Low preset).
// Smoke is plain ParticleSystems built into the scene (they play on their own). Not [ExecuteAlways]: edit-mode captures call Pose directly.
using System;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class AmbientLife : MonoBehaviour
    {
        public enum MotionKind : byte
        {
            Bob = 0,        // floats: heave and a little tilt
            BoatBob = 1,    // the moored tug's hull visual: a few cm of heave, a slow roll and pitch
            Sway = 2,       // flags on a line, nets, bunting: rotation about Axis (local), two sines
            Flag = 3,       // a flag flying along +x: flutter about local y
            WindSock = 4,   // a windsock streaming along +x: slow yaw wander and droop
            Swing = 5,      // a hanging sign: swing about local x
            Spin = 6,       // a weather vane head: turns to a wandering wind direction
            Gull = 7,       // a perched gull: looks around in steps, an occasional hop
            Rotor = 8,      // round 1: windmill sails, a steady turn about local z (Amplitude = degrees per second)
            Circle = 9,     // round 1: a gliding gull circling its base position (Amplitude = radius, metres), banked into the turn
            Idle = 10,      // round 4: a standing villager: a slow weight shift and a look about (Amplitude = look, degrees)
            Walk = 11,      // round 4: a villager or the dog strolling back and forth along its base facing (Amplitude = path length, metres)
            Cruise = 12,    // quality-first round 1: a sailboat under way on a slow loop round its base position (Amplitude = radius, m), heeled
            Leap = 13,      // quality-first round 1: a fish jumping clear of the water in an arc, then gone under (Amplitude = arc length, m)
        }

        [Serializable]
        public struct Motion
        {
            public Transform Target;
            public MotionKind Kind;
            public float Phase;        // radians; spreads identical props apart
            public float Amplitude;    // metres (Bob, BoatBob) or degrees (the rest)
            public Vector3 Axis;       // Sway: the local axis; others ignore it
            public Vector3 BasePosition;
            public Quaternion BaseRotation;
        }

        [SerializeField] private Motion[] motions = new Motion[0];
        [SerializeField] private GameObject[] detail = new GameObject[0];
        [SerializeField] private Light[] lamps = new Light[0];
        [SerializeField] private float[] lampIntensity = new float[0];
        [SerializeField] private Renderer[] glowRenderers = new Renderer[0];
        [SerializeField] private Light beacon;
        [SerializeField] private float beaconIntensity;
        [SerializeField] private Light blink;
        [SerializeField] private Renderer blinkGlow;
        [SerializeField] private float blinkIntensity;
        [SerializeField] private ParticleSystem[] smoke = new ParticleSystem[0];
        [SerializeField] private Color[] smokeColor = new Color[0];

        private DayNightController _dayNight;

        private static readonly int LampGlowId = Shader.PropertyToID(ShaderGlobals.LampGlow);
        private const float GlowOn = 0.02f;

        public int MotionCount => motions.Length;
        public int DetailCount => detail.Length;
        public GameObject Detail(int i) => detail[i];

        // Builder only (edit time): everything is serialized into the scene.
        public void Configure(Motion[] motionList, GameObject[] detailObjects, Light[] lampLights, Renderer[] glows, Light beaconLight, Light blinkLight,
                              Renderer blinkGlowRenderer, ParticleSystem[] smokeSystems)
        {
            motions = motionList ?? new Motion[0];
            for (int i = 0; i < motions.Length; i++)
            {
                motions[i].BasePosition = motions[i].Target.localPosition;
                motions[i].BaseRotation = motions[i].Target.localRotation;
            }
            detail = detailObjects ?? new GameObject[0];
            lamps = lampLights ?? new Light[0];
            lampIntensity = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++) lampIntensity[i] = lamps[i].intensity;
            glowRenderers = glows ?? new Renderer[0];
            beacon = beaconLight;
            beaconIntensity = beaconLight != null ? beaconLight.intensity : 0f;
            blink = blinkLight;
            blinkIntensity = blinkLight != null ? blinkLight.intensity : 0f;
            blinkGlow = blinkGlowRenderer;
            smoke = smokeSystems ?? new ParticleSystem[0];
            smokeColor = new Color[smoke.Length];
            for (int i = 0; i < smoke.Length; i++) smokeColor[i] = smoke[i].main.startColor.color;
        }

        private const float WalkSpeed = 0.85f, WalkTurn = 1.4f;
        private const float LeapPeriod = 3.4f, LeapAir = 0.42f;   // quality-first round 1: a jump every 3.4 s, 42 % of it in the air   // round 4: a stroll (m/s) and the turn at each end (s)

        private void OnEnable()
        {
            GraphicsApplier.SceneDetailChanged += ApplyDetail;
            ApplyDetail(GraphicsApplier.SceneDetailVisible);
            _dayNight = FindAnyObjectByType<DayNightController>();
        }

        private void OnDisable()
        {
            GraphicsApplier.SceneDetailChanged -= ApplyDetail;
        }

        private void Update()
        {
            Pose(Time.time, Shader.GetGlobalFloat(LampGlowId), _dayNight != null ? _dayNight.ParticleTint : Color.white);
        }

        // Shows or hides every detail object (Low preset: hidden). Nothing with a collider is ever in the list (EditMode test).
        public void ApplyDetail(bool visible)
        {
            foreach (GameObject go in detail)
                if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }

        // Poses every moving part at time t (seconds), sets the lights for glow (0 day .. 1 night) and tints new smoke particles (the unlit smoke
        // would glow at night otherwise; DayNightController.ParticleTint, as the rain uses). Allocation-free.
        public void Pose(float t, float glow, Color smokeTint)
        {
            for (int i = 0; i < smoke.Length; i++)
            {
                if (smoke[i] == null) continue;
                ParticleSystem.MainModule main = smoke[i].main;
                Color c = smokeColor[i] * smokeTint;
                c.a = smokeColor[i].a;
                main.startColor = c;
            }
            for (int i = 0; i < motions.Length; i++) Apply(ref motions[i], t);

            bool on = glow > GlowOn;
            for (int i = 0; i < lamps.Length; i++)
            {
                Light l = lamps[i];
                if (l == null) continue;
                l.enabled = on;
                l.intensity = lampIntensity[i] * glow;
            }
            for (int i = 0; i < glowRenderers.Length; i++)
                if (glowRenderers[i] != null) glowRenderers[i].enabled = on;

            if (beacon != null)
            {
                beacon.enabled = on;
                beacon.intensity = beaconIntensity * glow;
                beacon.transform.localRotation = Quaternion.Euler(8f, t * 36f % 360f, 0f);   // one sweep every 10 s, tipped down toward the sea
            }
            if (blink != null)
            {
                bool lit = Mathf.Repeat(t, 2.4f) < 0.5f;
                blink.enabled = lit;
                blink.intensity = blinkIntensity * (0.35f + 0.65f * glow);
                if (blinkGlow != null) blinkGlow.enabled = lit && on;
            }
        }

        private static void Apply(ref Motion m, float t)
        {
            Transform tr = m.Target;
            if (tr == null) return;
            float p = m.Phase, a = m.Amplitude;
            switch (m.Kind)
            {
                case MotionKind.Bob:
                {
                    float h = a * Mathf.Sin(t * 1.3f + p) + a * 0.35f * Mathf.Sin(t * 2.9f + p * 1.7f);
                    tr.localPosition = m.BasePosition + new Vector3(0f, h, 0f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(7f * Mathf.Sin(t * 1.1f + p), 0f, 6f * Mathf.Sin(t * 1.4f + p * 0.6f));
                    break;
                }
                case MotionKind.BoatBob:
                {
                    float h = a * Mathf.Sin(t * 0.7f + p) + a * 0.3f * Mathf.Sin(t * 1.9f + p);
                    tr.localPosition = m.BasePosition + new Vector3(0f, h, 0f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0.45f * Mathf.Sin(t * 0.55f + p), 0f, 0.9f * Mathf.Sin(t * 0.8f + p * 1.3f));
                    break;
                }
                case MotionKind.Sway:
                {
                    float ang = a * Mathf.Sin(t * 1.2f + p) + a * 0.35f * Mathf.Sin(t * 2.7f + p * 1.9f);
                    tr.localRotation = Quaternion.AngleAxis(ang, m.Axis) * m.BaseRotation;
                    break;
                }
                case MotionKind.Flag:
                {
                    float ang = a * Mathf.Sin(t * 3.1f + p) + a * 0.5f * Mathf.Sin(t * 5.3f + p * 2.1f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, ang, 2f * Mathf.Sin(t * 4.2f + p));
                    break;
                }
                case MotionKind.WindSock:
                {
                    float yaw = a * Mathf.Sin(t * 0.35f + p) + a * 0.4f * Mathf.Sin(t * 1.3f + p);
                    float droop = 4f + 3f * Mathf.Sin(t * 0.5f + p * 0.7f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, yaw, -droop);
                    break;
                }
                case MotionKind.Swing:
                {
                    float ang = a * Mathf.Sin(t * 1.6f + p) + a * 0.25f * Mathf.Sin(t * 3.7f + p);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(ang, 0f, 0f);
                    break;
                }
                case MotionKind.Spin:
                {
                    float yaw = 40f * Mathf.Sin(t * 0.13f + p) + a * Mathf.Sin(t * 0.9f + p);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, yaw, 0f);
                    break;
                }
                case MotionKind.Gull:
                {
                    // Look around in held steps (a smoothed square wave), a hop now and then (a sharp sine peak).
                    float s = Mathf.Sin(t * 0.55f + p);
                    float step = Mathf.Clamp(s * 4f, -1f, 1f);
                    float hop = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.37f + p * 2.3f)), 24f) * 0.09f;
                    tr.localPosition = m.BasePosition + new Vector3(0f, hop, 0f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, a * step, 0f);
                    break;
                }
                case MotionKind.Rotor:
                {
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, 0f, (t * a + p * Mathf.Rad2Deg) % 360f);
                    break;
                }
                case MotionKind.Idle:
                {
                    float look = a * Mathf.Sin(t * 0.37f + p) + a * 0.35f * Mathf.Sin(t * 1.13f + p * 2.1f);
                    float sway = 2.2f * Mathf.Sin(t * 0.83f + p * 1.3f);
                    float breathe = 0.012f * Mathf.Sin(t * 1.7f + p);
                    tr.localPosition = m.BasePosition + new Vector3(0f, breathe, 0f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, look, sway);
                    break;
                }
                case MotionKind.Walk:
                {
                    // Out along the base facing at WalkSpeed, a turn on the spot, back, a turn: one loop per period; a step bounce and a hip roll
                    // while moving. The phase spreads the walkers along their loops.
                    float leg = Mathf.Max(0.5f, a) / WalkSpeed;
                    float period = 2f * (leg + WalkTurn);
                    float u = Mathf.Repeat(t + p / (2f * Mathf.PI) * period, period);
                    float s, yaw;
                    bool moving;
                    if (u < leg) { s = u / leg; yaw = 0f; moving = true; }
                    else if (u < leg + WalkTurn) { s = 1f; yaw = 180f * Mathf.SmoothStep(0f, 1f, (u - leg) / WalkTurn); moving = false; }
                    else if (u < 2f * leg + WalkTurn) { s = 1f - (u - leg - WalkTurn) / leg; yaw = 180f; moving = true; }
                    else { s = 0f; yaw = 180f + 180f * Mathf.SmoothStep(0f, 1f, (u - 2f * leg - WalkTurn) / WalkTurn); moving = false; }
                    float stride = u * 2f * Mathf.PI * 1.9f;
                    float bounce = moving ? 0.035f * Mathf.Abs(Mathf.Sin(stride)) : 0f;
                    Vector3 forward = m.BaseRotation * Vector3.forward;
                    tr.localPosition = m.BasePosition + forward * (s * a) + new Vector3(0f, bounce, 0f);
                    tr.localRotation = m.BaseRotation * Quaternion.Euler(0f, yaw, moving ? 3f * Mathf.Sin(stride) : 0f);
                    break;
                }
                case MotionKind.Cruise:
                {
                    // Counter-clockwise seen from above at ~2.2 m/s along the loop (a lap takes minutes on a big radius), heeled 7 degrees away
                    // from the turn with a slow pitch and heave on the swell; the hull faces along the path (its local +z is the bow).
                    float r = Mathf.Max(2f, a);
                    float ang = t * (2.2f / r) + p;
                    tr.localPosition = m.BasePosition + new Vector3(r * Mathf.Cos(ang), 0.06f * Mathf.Sin(t * 0.9f + p), r * Mathf.Sin(ang));
                    var heading = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    tr.localRotation = Quaternion.LookRotation(heading, Vector3.up)
                                       * Quaternion.Euler(1.5f * Mathf.Sin(t * 0.7f + p * 1.3f), 0f, -7f + 1.5f * Mathf.Sin(t * 0.55f + p));
                    break;
                }
                case MotionKind.Leap:
                {
                    // One jump per LeapPeriod: out of the water along the base facing in an arc over Amplitude metres, nose up then
                    // down (pitch follows the arc, 1.2 m high), a flick of the tail; the rest of the period it waits 2 m under the surface (hidden by the water).
                    float u = Mathf.Repeat(t / LeapPeriod + p / (2f * Mathf.PI), 1f);
                    if (u < LeapAir)
                    {
                        float s = u / LeapAir;                                   // 0..1 along the arc
                        float len = Mathf.Max(0.4f, a);
                        float y = 1.2f * Mathf.Sin(Mathf.PI * s) - 0.2f;
                        float dy = 1.2f * Mathf.PI * Mathf.Cos(Mathf.PI * s) / len;  // slope of the arc
                        tr.localPosition = m.BasePosition + m.BaseRotation * new Vector3(0f, y, (s - 0.5f) * len);
                        tr.localRotation = m.BaseRotation * Quaternion.Euler(-Mathf.Atan(dy) * Mathf.Rad2Deg, 18f * Mathf.Sin(s * 25f), 0f);
                    }
                    else
                    {
                        tr.localPosition = m.BasePosition + new Vector3(0f, -2f, 0f);
                        tr.localRotation = m.BaseRotation;
                    }
                    break;
                }
                case MotionKind.Circle:
                {
                    // Counter-clockwise seen from above at ~0.22 rad/s (a lap in ~28 s), a slow height wave; the body faces along the path
                    // and banks 18 degrees into the turn. The radius scales the angular speed down a little so big circles do not race.
                    float w = 0.22f * Mathf.Sqrt(6f / Mathf.Max(1f, a));
                    float ang = t * w + p;
                    var offset = new Vector3(a * Mathf.Cos(ang), 0.6f * Mathf.Sin(t * 0.31f + p * 1.7f), a * Mathf.Sin(ang));
                    tr.localPosition = m.BasePosition + offset;
                    var heading = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    tr.localRotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, 0f, 18f);
                    break;
                }
            }
        }
    }
}
