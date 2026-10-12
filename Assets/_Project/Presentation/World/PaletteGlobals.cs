// Authority: local only (every peer); never writes gameplay state.
// Pushes the art palette (16 slots, linearised) and the Day phase colours as shader globals (spec 10.3, C 7.22, ruling F3).
// ArtPaletteDef.PaletteVectors allocates, so Apply runs on enable and on editor validate only, never per frame (C 16.2).
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
        // M2 has no clock: the Day phase is index 1 of the four phases. M3's DayNightController replaces this.
        private const int DayPhaseIndex = 1;

        private static readonly int PaletteId = Shader.PropertyToID(ShaderGlobals.Palette);
        private static readonly int ToonShadeId = Shader.PropertyToID(ShaderGlobals.ToonShade);
        private static readonly int ToonAmbientId = Shader.PropertyToID(ShaderGlobals.ToonAmbient);
        private static readonly int SkyTopId = Shader.PropertyToID(ShaderGlobals.SkyTop);
        private static readonly int SkyHorizonId = Shader.PropertyToID(ShaderGlobals.SkyHorizon);
        private static readonly int SkyGroundId = Shader.PropertyToID(ShaderGlobals.SkyGround);

        [SerializeField] private ArtPaletteDef palette;

        private readonly Vector4[] _vectors = new Vector4[ShaderGlobals.PaletteLength];
        private bool _warnedSeason;

        // Diagnostic: how many times Apply pushed. A per-frame Apply (allocating) would make this climb every frame.
        internal int ApplyCount { get; private set; }

        private void OnEnable()
        {
            Apply();
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            Apply();
        }
#endif

        public void Apply()
        {
            ArtPaletteDef def = ResolvePalette();
            if (def == null) return;

            int season = def.M2Season;
            bool seasonOk = season >= 0 && season < ArtPaletteDef.SeasonCount && season < def.Seasons.Count;
            if (!seasonOk && !_warnedSeason)
            {
                _warnedSeason = true;   // once per component: Apply runs on enable and on validate only
                Debug.LogWarning("PaletteGlobals: palette season " + season + " is out of range or has no entry (" + def.Seasons.Count + " entries); the palette array is not pushed.", this);
            }
            if (seasonOk)
            {
                Vector4[] authored = def.PaletteVectors(season);
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                for (int i = 0; i < ShaderGlobals.PaletteLength; i++)
                {
                    Vector4 v = authored[i];
                    if (linear)
                    {
                        Color c = new Color(v.x, v.y, v.z, v.w).linear;
                        v = new Vector4(c.r, c.g, c.b, c.a);
                    }
                    _vectors[i] = v;
                }
                Shader.SetGlobalVectorArray(PaletteId, _vectors);
            }

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

            ApplyCount++;
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
