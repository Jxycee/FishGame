// Authority: local only (every peer); reads IWorldContext (minute, season, weather), never writes gameplay state.
// Spec 10.3 day-night look on the world clock (M3-D21, D23, D24; section F plan T18, P2-P8). Owns the weather look blend (CCR-F2, M3-D79).
// Drives the single directional light (the scene root "Sun": never disabled or swapped), _ToonShade/_ToonAmbient/the three sky globals,
// _FGDayFraction, the fog colour and density (fog stays on) and the two Volume weights. Never touches a material colour (M3 #27): the season
// reaches textured albedo through the _FGPalette swap in PaletteGlobals (M3-D78).
// Not [ExecuteAlways]: edit mode belongs to PaletteGlobals (this class would otherwise rewrite the active scene's RenderSettings in builds).
using FishGame.Core;
using FishGame.Data;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class DayNightController : MonoBehaviour
    {
        // The fog cap is a safety net only: the T18 EditMode test proves the shipped data never reaches it (DayNightMath.IsReadable(0.0333f)).
        internal const float MaxReadableFogDensity = 0.0333f;
        internal const float WeatherFadeSeconds = 4f;
        internal const float DayHoldFraction = 0.25f;   // minute 300: pure Day phase, sun up (what M2 showed) while there is no world state

        private static readonly int ToonShadeId = Shader.PropertyToID(ShaderGlobals.ToonShade);
        private static readonly int ToonAmbientId = Shader.PropertyToID(ShaderGlobals.ToonAmbient);
        private static readonly int SkyTopId = Shader.PropertyToID(ShaderGlobals.SkyTop);
        private static readonly int SkyHorizonId = Shader.PropertyToID(ShaderGlobals.SkyHorizon);
        private static readonly int SkyGroundId = Shader.PropertyToID(ShaderGlobals.SkyGround);
        private static readonly int DayFractionId = Shader.PropertyToID(ShaderGlobals.DayFraction);

        private const int LookCount = 3;   // Clear 0, Rain 1, Fog 2 (ArtPaletteDef.weatherLooks)

        [SerializeField] private ArtPaletteDef palette;
        [SerializeField] private Light sun;
        [SerializeField] private Volume warmVolume;
        [SerializeField] private Volume coolVolume;

        private DayPhaseAmbient[] _phases;
        private float[] _starts;
        private SunMoonSettings _sunMoon;
        private readonly WeatherAmbient[] _looks = new WeatherAmbient[LookCount];

        private float _preview = float.NaN;
        private float _dayFraction = DayHoldFraction;

        private bool _lookValid, _hadState;
        private int _lookIdx;
        private WeatherAmbient _from, _to, _active;
        private float _fade = 1f;

        // The blended weather look (for WeatherFx, CCR-F2) and the colour new particles are multiplied by so unlit rain does not glow at night.
        internal WeatherAmbient ActiveLook => _active;
        internal Color ParticleTint { get; private set; } = Color.white;

        public float DayFraction => _dayFraction;

        // Editor and tests; NaN clears. Play mode only (the class does not run in edit mode).
        public void SetPreviewMinute(float minute)
        {
            _preview = float.IsNaN(minute) ? float.NaN : Mathf.Clamp(minute, 0f, DayClock.DayLengthMinutes);
        }

        private void OnEnable()
        {
            ArtPaletteDef def = palette;
            if (def == null)
            {
                ServiceRegistry app = ServiceRoots.App;
                if (app != null && app.TryGet(out ContentRegistry registry)) def = registry.ArtPalette;
            }

            _phases = null;
            if (def == null)
            {
                Debug.Log("DayNightController: no ArtPaletteDef; the day-night look is off.");
                enabled = false;
                return;
            }

            var phases = new DayPhaseAmbient[def.Phases.Count];
            var starts = new float[phases.Length];
            bool ok = phases.Length >= 1;
            for (int i = 0; i < phases.Length; i++)
            {
                phases[i] = def.Phases[i];
                starts[i] = phases[i].startFraction;
                if (i > 0 && !(starts[i] > starts[i - 1])) ok = false;
            }
            if (!ok)
            {
                Debug.Log("DayNightController: the palette needs at least one phase with strictly increasing starts; the day-night look is off.");
                enabled = false;
                return;
            }
            _phases = phases;
            _starts = starts;
            _sunMoon = def.SunMoon;

            var neutral = new WeatherAmbient { fogDensityMult = 1f, fogDensityMin = 0f, lightMult = 1f, desaturate = 0f, rainAmount = 0f };
            bool looksOk = def.WeatherLooks.Count == LookCount;
            if (!looksOk) Debug.Log("DayNightController: the palette has " + def.WeatherLooks.Count + " weather looks, expected " + LookCount + "; using the neutral look.");
            for (int i = 0; i < LookCount; i++) _looks[i] = looksOk ? def.WeatherLooks[i] : neutral;

            if (sun == null) Debug.Log("DayNightController: no Sun light; only the globals, fog and volumes are driven.");

            _lookValid = false;
            _hadState = false;
            _fade = 1f;
            _from = _to = _active = _looks[0];
            _lookIdx = 0;
        }

        private void Update()
        {
            if (_phases == null) return;

            WorldState world = WorldState.Current;
            bool state = world != null && world.HasState;
            float f = !float.IsNaN(_preview) ? DayClock.DayFraction(_preview)
                    : state ? DayClock.DayFraction(world.MinuteF) : DayHoldFraction;      // P5: the continuous minute, never the int one
            _dayFraction = f;

            // Weather look (P2): snap on the first state, else a 4 s linear fade from the CURRENT blend (no pop when it changes mid-fade).
            int idx = state ? LookIndex(world.Weather) : 0;
            if (!_lookValid || (state && !_hadState))
            {
                _from = _to = _looks[idx];
                _fade = 1f;
                _lookValid = true;
            }
            else if (idx != _lookIdx)
            {
                _from = _active;
                _to = _looks[idx];
                _fade = 0f;
            }
            _lookIdx = idx;
            _hadState = state;
            _fade = Mathf.Min(1f, _fade + Time.deltaTime / WeatherFadeSeconds);
            _active = LerpLook(_from, _to, _fade);

            // Phase blend (M3-D24, B-D12).
            PhaseBlend b = DayNightMath.Blend(f, _starts, _sunMoon.blendWindow);
            DayPhaseAmbient a = _phases[b.From], c = _phases[b.To];
            float t = b.T;
            Color phaseLight = Color.Lerp(a.lightColor, c.lightColor, t);
            float phaseIntensity = Mathf.Lerp(a.lightIntensity, c.lightIntensity, t);
            Color shade = Color.Lerp(a.toonShade, c.toonShade, t);
            Color ambient = Color.Lerp(a.toonAmbient, c.toonAmbient, t);
            Color skyTop = Color.Lerp(a.skyTop, c.skyTop, t);
            Color skyHorizon = Color.Lerp(a.skyHorizon, c.skyHorizon, t);
            Color skyGround = Color.Lerp(a.skyGround, c.skyGround, t);
            Color fogColor = Color.Lerp(a.fogColor, c.fogColor, t);
            float phaseFog = Mathf.Lerp(a.fogDensity, c.fogDensity, t);
            float warm = Mathf.Lerp(a.warmWeight, c.warmWeight, t);
            float cool = Mathf.Lerp(a.coolWeight, c.coolWeight, t);

            // Sun / moon (P7, B-D14): the two weights are never both non-zero and are both 0 at the flips, so the switch is invisible.
            float sunW = DayNightMath.SunWeight(f, _sunMoon.sunriseFraction, _sunMoon.sunsetFraction, _sunMoon.flipWindow);
            float moonW = DayNightMath.MoonWeight(f, _sunMoon.sunriseFraction, _sunMoon.sunsetFraction, _sunMoon.flipWindow);
            Quaternion rot;
            Color lightColor;
            float intensity;
            if (moonW > 0f)
            {
                rot = Quaternion.Euler(_sunMoon.moonEulerDeg);
                lightColor = _sunMoon.moonTint;
                intensity = _sunMoon.moonIntensity * moonW;
            }
            else
            {
                DayNightMath.SunDirection(f, _sunMoon.sunriseFraction, _sunMoon.sunsetFraction, _sunMoon.minElevationDeg, out float x, out float y, out float z);
                rot = Quaternion.LookRotation(new Vector3(-x, -y, -z), Vector3.up);   // forward points AWAY from the sun (the vector is toward it)
                lightColor = phaseLight;
                intensity = phaseIntensity * sunW;
            }
            intensity *= _active.lightMult;

            // Desaturate (P3) and fog (P8), then push.
            float d = _active.desaturate;
            lightColor = Desat(lightColor, d);
            shade = Desat(shade, d);
            ambient = Desat(ambient, d);
            skyTop = Desat(skyTop, d);
            skyHorizon = Desat(skyHorizon, d);
            skyGround = Desat(skyGround, d);
            fogColor = Desat(fogColor, d);
            float fog = Mathf.Min(MaxReadableFogDensity, Mathf.Max(phaseFog * _active.fogDensityMult, _active.fogDensityMin));

            if (sun != null)
            {
                sun.transform.rotation = rot;   // never sun.enabled: URP needs the main light; intensity 0 is how it goes dark
                sun.color = lightColor;
                sun.intensity = intensity;
            }
            Shader.SetGlobalColor(ToonShadeId, shade);
            Shader.SetGlobalColor(ToonAmbientId, ambient);
            Shader.SetGlobalColor(SkyTopId, skyTop);
            Shader.SetGlobalColor(SkyHorizonId, skyHorizon);
            Shader.SetGlobalColor(SkyGroundId, skyGround);
            Shader.SetGlobalFloat(DayFractionId, f);
            RenderSettings.fogColor = fogColor;       // fog and fogMode are never written: the scene builder switched fog on (spec 10.3)
            RenderSettings.fogDensity = fog;
            if (warmVolume != null) warmVolume.weight = warm;
            if (coolVolume != null) coolVolume.weight = cool;

            float s = Mathf.Clamp01(intensity);
            Color tint = Color.Lerp(ambient, lightColor, s);
            tint.a = 1f;
            ParticleTint = tint;
        }

        // Fog bit -> Fog look (2), else the Rain bit -> Rain (1), else Clear (0). Never throws, so an M5 Storm (a Rain bit) reads as Rain until M5
        // adds its look; EcologyMasks.WeatherColumn is not used here because it throws on multi-bit masks.
        private static int LookIndex(WeatherMask w)
        {
            if ((w & WeatherMask.Fog) != 0) return 2;
            if ((w & WeatherMask.Rain) != 0) return 1;
            return 0;
        }

        private static WeatherAmbient LerpLook(in WeatherAmbient a, in WeatherAmbient b, float t)
        {
            return new WeatherAmbient
            {
                fogDensityMult = Mathf.Lerp(a.fogDensityMult, b.fogDensityMult, t),
                fogDensityMin = Mathf.Lerp(a.fogDensityMin, b.fogDensityMin, t),
                lightMult = Mathf.Lerp(a.lightMult, b.lightMult, t),
                desaturate = Mathf.Lerp(a.desaturate, b.desaturate, t),
                rainAmount = Mathf.Lerp(a.rainAmount, b.rainAmount, t),
            };
        }

        // Each channel toward the colour's own Rec.709 luma by `amount`; 0 returns the colour untouched (a branch, not a lerp by 0, so the Clear
        // look is bit-exact).
        private static Color Desat(Color c, float amount)
        {
            if (amount <= 0f) return c;
            float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return new Color(Mathf.Lerp(c.r, luma, amount), Mathf.Lerp(c.g, luma, amount), Mathf.Lerp(c.b, luma, amount), c.a);
        }
    }
}
