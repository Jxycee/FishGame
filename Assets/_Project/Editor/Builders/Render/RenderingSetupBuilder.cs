// Rendering setup (C 7.21, plan T21.5). Owned by T21. Authority: none (editor).
// Build(): world materials (RenderMaterialLibrary.BuildWorldMaterials), the two volume profiles (ColorAdjustments + WhiteBalance only, spec 10.3: no
// tonemapping; each component is saved as a sub-asset of its profile, otherwise it is lost on reload) and the URP shadow tuning.
// ApplySceneLighting(): the ACTIVE scene's RenderSettings (skybox M_Sky, ExponentialSquared fog 0.008, flat ambient); scene builders call it after NewScene.
// Everything is ensured in place (GUIDs kept, SetDirty only on a change), so a second build changes nothing.
//
// Shadow acne (T20 look render): FishGame/Toon samples the shadow map at the raw world position (no receiver bias, the same as URP Lit), so the bias
// comes from the light/pipeline: PC_RPAsset had depth bias 0.1 and normal bias 0.5, which left diagonal hatching on grazing flat faces (the white
// torso). ShadowDepthBias / ShadowNormalBias below are the tuned pipeline values (the sun uses "Use Pipeline Settings").
using FishGame.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FishGame.EditorTools
{
    public static class RenderingSetupBuilder
    {
        // Tuned against the T21 look render (before: 0.1 / 0.5). Pipeline-wide: lights that use pipeline settings take these.
        internal const float ShadowDepthBias = 1.0f;
        internal const float ShadowNormalBias = 1.0f;

        public static void Build()
        {
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            if (water == null) throw new System.InvalidOperationException("WaterSettings asset missing: " + AssetPaths.WaterSettingsAsset);
            RenderMaterialLibrary.BuildWorldMaterials(water);

            BuilderUtil.EnsureFolder(AssetPaths.RenderingFolder);
            EnsureProfile(AssetPaths.WarmVolumeProfile, 18f, 4f, 0f, 8f, new Color(1.00f, 0.94f, 0.86f));
            EnsureProfile(AssetPaths.CoolVolumeProfile, -22f, -4f, -0.3f, -18f, new Color(0.82f, 0.88f, 1.00f));

            ApplyShadowTuning();
        }

        // C 6.4: skybox M_Sky, ExponentialSquared fog density 0.008 coloured by the Day phase, flat ambient = the Day toonAmbient.
        // Fog is never toggled at runtime. A missing asset throws.
        public static void ApplySceneLighting()
        {
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            if (palette == null) throw new System.InvalidOperationException("ArtPalette asset missing: " + AssetPaths.ArtPaletteAsset);
            if (palette.Phases.Count < 2) throw new System.InvalidOperationException("ArtPalette has no Day phase (phases[1]): " + AssetPaths.ArtPaletteAsset);
            DayPhaseAmbient day = palette.Phases[1];

            RenderSettings.skybox = RenderMaterialLibrary.Get("M_Sky");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = day.fogColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = day.toonAmbient;
        }

        static void ApplyShadowTuning()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPaths.PcRenderPipelineAsset);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.PcRenderPipelineAsset);
            bool changed = false;
            if (asset.shadowDepthBias != ShadowDepthBias) { asset.shadowDepthBias = ShadowDepthBias; changed = true; }
            if (asset.shadowNormalBias != ShadowNormalBias) { asset.shadowNormalBias = ShadowNormalBias; changed = true; }
            if (changed) EditorUtility.SetDirty(asset);
        }

        static void EnsureProfile(string path, float temperature, float tint, float postExposure, float saturation, Color colorFilter)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            bool changed = false;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                changed = true;
            }

            ColorAdjustments adjustments = EnsureComponent<ColorAdjustments>(profile, ref changed);
            changed |= Set(adjustments.postExposure, postExposure);
            changed |= Set(adjustments.saturation, saturation);
            changed |= Set(adjustments.colorFilter, colorFilter);

            WhiteBalance balance = EnsureComponent<WhiteBalance>(profile, ref changed);
            changed |= Set(balance.temperature, temperature);
            changed |= Set(balance.tint, tint);

            if (changed)
            {
                EditorUtility.SetDirty(adjustments);
                EditorUtility.SetDirty(balance);
                EditorUtility.SetDirty(profile);
            }
        }

        // A component that is only added to profile.components is not saved: AddObjectToAsset makes it a sub-asset.
        static T EnsureComponent<T>(VolumeProfile profile, ref bool changed) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T component = profile.Add<T>(true);
            if (string.IsNullOrEmpty(component.name)) component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            changed = true;
            return component;
        }

        static bool Set(VolumeParameter<float> parameter, float value)
        {
            if (parameter.overrideState && parameter.value == value) return false;
            parameter.Override(value);
            return true;
        }

        static bool Set(VolumeParameter<Color> parameter, Color value)
        {
            if (parameter.overrideState && parameter.value == value) return false;
            parameter.Override(value);
            return true;
        }
    }
}
