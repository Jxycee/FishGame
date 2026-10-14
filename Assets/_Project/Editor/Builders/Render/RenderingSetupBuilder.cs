// Rendering setup (C 7.21, plan T21.5). Owned by T21. Authority: none (editor).
// Build(): world materials (RenderMaterialLibrary.BuildWorldMaterials), the two volume profiles (ColorAdjustments + WhiteBalance only, spec 10.3: no
// tonemapping; each component is saved as a sub-asset of its profile, otherwise it is lost on reload) and the URP shadow tuning.
// ApplySceneLighting(): the ACTIVE scene's RenderSettings (skybox M_Sky, ExponentialSquared fog 0.008, flat ambient); scene builders call it after NewScene.
// Everything is ensured in place (GUIDs kept, SetDirty only on a change), so a second build changes nothing.
// This builder owns the PC_RPAsset / PC_Renderer / Mobile_RPAsset settings that the render look depends on (C 16 shared edit):
//   - the ScreenSpaceAmbientOcclusion renderer feature on PC_Renderer is ENABLED with the tuned contact-shadow settings below (quality-first round 1,
//     user 2026-10-14: spec Graphics tab note "Medium and High may use the full look: SSAO, ..."); FishGame/Toon samples it
//     (_SCREEN_SPACE_OCCLUSION). GraphicsApplier switches it off on the Low preset and puts the authored state back afterwards. SSAO asks for a
//     DepthNormals prepass, which Toon provides (its DepthNormals pass); this builder is the single owner of the flag;
//   - both pipeline assets keep m_RequireDepthTexture 1, so FishGame/Water's SampleSceneDepth depth fade and foam always have a depth texture;
//   - main-light shadows: 120 m, 4096, 4 cascades (GraphicsPresets.ShadowFor(High), the authored look), soft shadows on;
//   - VP_QualityPost: a global Bloom + Vignette profile (no tonemapping, spec 10.3) whose volume QualityPostVolume zeroes on the Low preset.
//
// Shadow acne (T20 look render): FishGame/Toon samples the shadow map at the raw world position (no receiver bias, the same as URP Lit), so the bias
// comes from the light/pipeline: PC_RPAsset had depth bias 0.1 and normal bias 0.5, which left diagonal hatching on grazing flat faces (the white
// torso). ShadowDepthBias / ShadowNormalBias below are the tuned pipeline values (the sun uses "Use Pipeline Settings").
using FishGame.Core;
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

            EnsureQualityPost(AssetPaths.QualityPostProfile);
            ApplyShadowTuning();
            EnableSsao();
            EnsureDepthTexture(AssetPaths.PcRenderPipelineAsset);
            EnsureDepthTexture(AssetPaths.MobileRenderPipelineAsset);
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

        // SSAO tuning (quality-first round 1): soft contact darkening under props, at wall feet and in corners, kept gentle so the toon ramp and
        // the baked vertex AO stay the main shading. Toon multiplies the lit side by the direct factor and the shade/sky terms by the full factor.
        // The settings are private serialized fields of the feature, so they are written through SerializedObject (m_Settings.*).
        internal const float SsaoIntensity = 0.9f, SsaoRadius = 0.45f, SsaoDirect = 0.3f, SsaoFalloff = 120f;

        static void EnableSsao()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetPaths.PcRenderer);
            if (renderer == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.PcRenderer);
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (!(feature is ScreenSpaceAmbientOcclusion)) continue;
                bool changed = false;
                if (!feature.isActive) { feature.SetActive(true); changed = true; }
                var so = new SerializedObject(feature);
                changed |= SetFloat(so, "m_Settings.Intensity", SsaoIntensity);
                changed |= SetFloat(so, "m_Settings.Radius", SsaoRadius);
                changed |= SetFloat(so, "m_Settings.DirectLightingStrength", SsaoDirect);
                changed |= SetFloat(so, "m_Settings.Falloff", SsaoFalloff);
                changed |= SetInt(so, "m_Settings.Downsample", 0);
                changed |= SetInt(so, "m_Settings.AfterOpaque", 0);
                changed |= SetInt(so, "m_Settings.Source", 1);          // DepthNormals (Toon has the pass)
                changed |= SetInt(so, "m_Settings.NormalSamples", 1);
                changed |= SetInt(so, "m_Settings.Samples", 0);         // High
                changed |= SetInt(so, "m_Settings.BlurQuality", 0);     // High
                so.ApplyModifiedPropertiesWithoutUndo();
                if (changed)
                {
                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        static bool SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) throw new System.InvalidOperationException("SSAO setting not found: " + path);
            if (Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        static bool SetInt(SerializedObject so, string path, int value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) throw new System.InvalidOperationException("SSAO setting not found: " + path);
            bool isEnum = p.propertyType == SerializedPropertyType.Enum, isBool = p.propertyType == SerializedPropertyType.Boolean;
            int now = isEnum ? p.enumValueIndex : (isBool ? (p.boolValue ? 1 : 0) : p.intValue);
            if (now == value) return false;
            if (isEnum) p.enumValueIndex = value;
            else if (isBool) p.boolValue = value != 0;
            else p.intValue = value;
            return true;
        }

        // Quality post (bloom + vignette). Bloom only catches the HDR highlights (threshold 1.05: the sun disc, lamp glows, the water's sun
        // glints) so the authored dusk grade is not lifted; the vignette is soft and warm-neutral.
        static void EnsureQualityPost(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            bool changed = false;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                changed = true;
            }

            Bloom bloom = EnsureComponent<Bloom>(profile, ref changed);
            changed |= Set(bloom.threshold, 1.05f);
            changed |= Set(bloom.intensity, 0.55f);
            changed |= Set(bloom.scatter, 0.72f);
            changed |= Set(bloom.tint, new Color(1.0f, 0.86f, 0.70f, 1f));
            if (!bloom.highQualityFiltering.overrideState || !bloom.highQualityFiltering.value) { bloom.highQualityFiltering.Override(true); changed = true; }

            Vignette vignette = EnsureComponent<Vignette>(profile, ref changed);
            changed |= Set(vignette.intensity, 0.2f);
            changed |= Set(vignette.smoothness, 0.45f);
            changed |= Set(vignette.color, new Color(0.16f, 0.10f, 0.14f, 1f));

            if (changed)
            {
                EditorUtility.SetDirty(bloom);
                EditorUtility.SetDirty(vignette);
                EditorUtility.SetDirty(profile);
            }
        }

        static void EnsureDepthTexture(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing " + assetPath);
            if (asset.supportsCameraDepthTexture) return;
            asset.supportsCameraDepthTexture = true;
            EditorUtility.SetDirty(asset);
        }

        static void ApplyShadowTuning()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPaths.PcRenderPipelineAsset);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.PcRenderPipelineAsset);
            bool changed = false;
            ShadowParams high = GraphicsPresets.ShadowFor((int)ShadowTier.High);
            if (asset.shadowDistance != high.Distance) { asset.shadowDistance = high.Distance; changed = true; }
            if (asset.mainLightShadowmapResolution != high.Resolution) { asset.mainLightShadowmapResolution = high.Resolution; changed = true; }
            if (asset.shadowCascadeCount != high.Cascades) { asset.shadowCascadeCount = high.Cascades; changed = true; }
            var split4 = new Vector3(0.067f, 0.2f, 0.46f);   // about 8 m, 24 m, 55 m, 120 m
            if (asset.cascade4Split != split4) { asset.cascade4Split = split4; changed = true; }
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
