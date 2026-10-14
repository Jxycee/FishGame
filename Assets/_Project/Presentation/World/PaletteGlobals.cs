// Authority: local only (every peer); never writes gameplay state.
// Pushes the art palette (16 slots, linearised), the season's toon colour grade (_FGTopTint, _FGBottomTint, _FGGradeParams) and the _FGSeasonTint hook for the world's season (spec 10.2/10.3, C 7.18, ruling F3, M3-D78),
// and in edit mode / without a DayNightController the Day phase colours. ArtPaletteDef.PaletteVectors allocates, so Apply runs on enable, on
// editor validate and on a real season change only (HandleWorldChanged), never per frame (C 16.2).
// M3 (T18): the season comes from IWorldContext (attached lazily in play mode, P6); with no world state it is ArtPaletteDef.M2Season (Summer).
// The palette swap IS the season tint on textured environment albedo (palette hue x value-only detail texture); _FGSeasonTint (season / base per
// slot, linear, clamped 0..2, exactly 1 for the base season) is a DORMANT hook that no M3 shader reads (CCR-F1). In play mode, when an enabled
// DayNightController shares this root, the phase colours, sky colours and fog belong to it (P4).
// Every successful palette push also hands the season to SeasonalVisibility (Runtime), which switches the seasonal scenery renderers; that call
// stays on EVERY push, including the re-apply when the world season changes.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    [ExecuteAlways]
    public sealed class PaletteGlobals : MonoBehaviour
    {
        // Edit mode (and a scene without a DayNightController) keeps the M2 look: the Day phase is index 1 of the four phases.
        private const int DayPhaseIndex = 1;
        private const float LookupInterval = 0.25f;   // P6: how often an unattached component looks for the world (unscaled seconds)
        private const float TintMax = 2f, TintBaseEpsilon = 1e-4f;

        private static readonly int PaletteId = Shader.PropertyToID(ShaderGlobals.Palette);
        private static readonly int SeasonTintId = Shader.PropertyToID(ShaderGlobals.SeasonTint);
        private static readonly int ToonShadeId = Shader.PropertyToID(ShaderGlobals.ToonShade);
        private static readonly int ToonAmbientId = Shader.PropertyToID(ShaderGlobals.ToonAmbient);
        private static readonly int SkyTopId = Shader.PropertyToID(ShaderGlobals.SkyTop);
        private static readonly int SkyHorizonId = Shader.PropertyToID(ShaderGlobals.SkyHorizon);
        private static readonly int SkyGroundId = Shader.PropertyToID(ShaderGlobals.SkyGround);
        private static readonly int TopTintId = Shader.PropertyToID(ShaderGlobals.TopTint);
        private static readonly int BottomTintId = Shader.PropertyToID(ShaderGlobals.BottomTint);
        private static readonly int GradeParamsId = Shader.PropertyToID(ShaderGlobals.GradeParams);

        [SerializeField] private ArtPaletteDef palette;

        private readonly Vector4[] _vectors = new Vector4[ShaderGlobals.PaletteLength];
        private readonly Vector4[] _baseVectors = new Vector4[ShaderGlobals.PaletteLength];
        private readonly Vector4[] _tint = new Vector4[ShaderGlobals.SeasonTintLength];
        private bool _warnedSeason;

        private IWorldContext _world;
        private float _nextLookup;
        private int _pushedSeason = -1;

        // Diagnostic: how many times Apply pushed. A per-frame Apply (allocating) would make this climb every frame.
        internal int ApplyCount { get; private set; }

        private void OnEnable()
        {
            _nextLookup = 0f;
            Apply();
        }

        private void OnDisable()
        {
            Detach();
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            Apply();
        }
#endif

        // Play mode only: attach to the world lazily (the Presentation root enables before WorldState spawns; a client syncs later still) and
        // detach when the instance is destroyed. Allocation-free: a timer, one registry lookup every 0.25 s while unattached.
        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_world != null)
            {
                if (_world is Object o && o == null) Detach();   // Unity fake-null: the scene unloaded; keep the last palette (no pop)
                return;
            }
            if (Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + LookupInterval;

            ServiceRegistry game = ServiceRoots.Game;
            if (game == null || !game.TryGet(out IWorldContext w) || w == null) return;
            if (w is Object destroyed && destroyed == null) return;
            _world = w;
            _world.Changed += HandleWorldChanged;
            HandleWorldChanged();   // the state may already be there
        }

        private void Detach()
        {
            if (_world == null) return;
            _world.Changed -= HandleWorldChanged;
            _world = null;
        }

        // Season, weather and hotspot changes all raise Changed: re-apply only when the season really changed. HasState turning false at
        // despawn never re-applies either.
        private void HandleWorldChanged()
        {
            if (_world != null && _world.HasState && (int)_world.Season != _pushedSeason) Apply();
        }

        public void Apply()
        {
            ArtPaletteDef def = ResolvePalette();
            if (def == null) return;

            int season = SeasonToPush(def);
            _pushedSeason = season;
            bool seasonOk = season >= 0 && season < ArtPaletteDef.SeasonCount && season < def.Seasons.Count;
            if (!seasonOk && !_warnedSeason)
            {
                _warnedSeason = true;   // once per component: Apply runs on enable, on validate and on a real season change only
                Debug.LogWarning("PaletteGlobals: palette season " + season + " is out of range or has no entry (" + def.Seasons.Count + " entries); the palette array is not pushed.", this);
            }
            if (seasonOk)
            {
                Linearise(def.PaletteVectors(season), _vectors);
                Shader.SetGlobalVectorArray(PaletteId, _vectors);
                PushSeasonTint(def, season);
                PushToonGrade(def, season);
                // Seasonal scenery (bare winter trees, spring blossoms, snowy pines) follows the pushed season. M3 T18 keeps this call on
                // every push, so a runtime season change re-applies the palette AND switches the trees in one place.
                SeasonalVisibility.SetSeason(season);
            }

            if (!PhasesBelongToDayNight())
            {
                IReadOnlyList<DayPhaseAmbient> phases = def.Phases;
                if (phases.Count > DayPhaseIndex)
                {
                    DayPhaseAmbient day = phases[DayPhaseIndex];
                    Shader.SetGlobalColor(ToonShadeId, day.toonShade);
                    Shader.SetGlobalColor(ToonAmbientId, day.toonAmbient);
                    Shader.SetGlobalColor(SkyTopId, day.skyTop);
                    Shader.SetGlobalColor(SkyHorizonId, day.skyHorizon);
                    Shader.SetGlobalColor(SkyGroundId, day.skyGround);

                    // Play mode only: in edit mode these writes would rewrite the fog of whatever scene is active (validators open the lake
                    // scene and OnValidate fires during builds). The fog switch itself is never toggled (C 6.4).
                    if (Application.isPlaying)
                    {
                        RenderSettings.fogColor = day.fogColor;
                        RenderSettings.fogDensity = day.fogDensity;
                    }
                }
            }

            ApplyCount++;
        }

        // The toon colour grade of a season (asset study 2026-10-14): raw vectors, no sRGB conversion (ToonGrade: the tints are gains, 1 = neutral).
        // Allocation-free. Public static so the editor captures push exactly what the runtime pushes.
        public static void PushToonGrade(ArtPaletteDef def, int season)
        {
            ToonGrade g = def.ToonGradeFor(season);
            Shader.SetGlobalVector(TopTintId, g.TopTintVector);
            Shader.SetGlobalVector(BottomTintId, g.BottomTintVector);
            Shader.SetGlobalVector(GradeParamsId, g.ParamsVector);
        }

        // The attached world's season when it has state, otherwise the M2 season (menus, edit mode, before the first sync).
        private int SeasonToPush(ArtPaletteDef def)
        {
            if (Application.isPlaying && _world != null && _world.HasState) return (int)_world.Season;
            return def.M2Season;
        }

        // P4: in play mode, an enabled DayNightController on this root owns the phase colours, the sky colours and the fog.
        private bool PhasesBelongToDayNight()
        {
            return Application.isPlaying && TryGetComponent(out DayNightController dayNight) && dayNight.isActiveAndEnabled;
        }

        // P1: tint[i] = seasonLin[i] / baseLin[i] per rgb channel (alpha 1); a base channel below 1e-4 gives 1; clamped to [0, TintMax]. The
        // base season is ArtPaletteDef.M2Season; the base season itself is written as exactly (1, 1, 1, 1). Always pushed full length (C2 #18),
        // in edit mode too, so a later opt-in material is never black.
        private void PushSeasonTint(ArtPaletteDef def, int season)
        {
            int baseSeason = def.M2Season;
            bool baseOk = baseSeason >= 0 && baseSeason < ArtPaletteDef.SeasonCount && baseSeason < def.Seasons.Count;
            if (!baseOk || baseSeason == season)
            {
                for (int i = 0; i < _tint.Length; i++) _tint[i] = Vector4.one;
            }
            else
            {
                Linearise(def.PaletteVectors(baseSeason), _baseVectors);
                for (int i = 0; i < _tint.Length; i++)
                {
                    Vector4 s = _vectors[i], b = _baseVectors[i];
                    _tint[i] = new Vector4(Ratio(s.x, b.x), Ratio(s.y, b.y), Ratio(s.z, b.z), 1f);
                }
            }
            Shader.SetGlobalVectorArray(SeasonTintId, _tint);
        }

        private static float Ratio(float season, float baseValue)
        {
            return baseValue < TintBaseEpsilon ? 1f : Mathf.Clamp(season / baseValue, 0f, TintMax);
        }

        private static void Linearise(Vector4[] authored, Vector4[] into)
        {
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < ShaderGlobals.PaletteLength; i++)
            {
                Vector4 v = authored[i];
                if (linear)
                {
                    Color c = new Color(v.x, v.y, v.z, v.w).linear;
                    v = new Vector4(c.r, c.g, c.b, c.a);
                }
                into[i] = v;
            }
        }

        private ArtPaletteDef ResolvePalette()
        {
            if (palette != null) return palette;
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out ContentRegistry registry)) return registry.ArtPalette;
            return null;
        }
    }
}
