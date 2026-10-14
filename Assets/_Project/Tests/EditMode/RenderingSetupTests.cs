// T21: world materials, volume profiles, camera post-processing, URP shadow tuning and the water wave parity (plan T21 tests, ruling F-Q6).
// Reads the persisted assets built by `build`; nothing is written. EditMode does not reference the URP assemblies, so volume profiles,
// the camera data and the RP asset are inspected through SerializedObject and type names. Each test names the wrong build it catches.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using FishGame.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class RenderingSetupTests
    {
        static Material Load(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
            Assert.That(material, Is.Not.Null, "material missing: " + name);
            return material;
        }

        static string ReadProjectFile(string assetPath)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Assert.That(File.Exists(full), Is.True, "missing " + assetPath);
            return File.ReadAllText(full);
        }

        // Catches: a world material on the wrong shader, a water material that ignores WaterSettingsDef, a particle material that is not
        // alpha-blended transparent, or a marker queue that lets the transparent water tint or hide the marker (queue 3100, after 3000).
        [Test]
        public void RenderingSetup_WorldMaterials_UseExpectedShadersAndSettings()
        {
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            Assert.That(water, Is.Not.Null, "WaterSettings asset");

            Material surface = Load("M_Water_Surface");
            Assert.That(surface.shader.name, Is.EqualTo("FishGame/Water"));
            AssertColour(surface.GetColor("_ShallowColor"), water.ShallowColor, "_ShallowColor");
            AssertColour(surface.GetColor("_DeepColor"), water.DeepColor, "_DeepColor");
            Assert.That(surface.GetFloat("_DepthRange"), Is.EqualTo(water.DepthRange).Within(1e-4f), "_DepthRange");
            Assert.That(surface.GetFloat("_FoamWidth"), Is.EqualTo(water.FoamWidth).Within(1e-4f), "_FoamWidth");

            Material line = Load("M_Line");
            Assert.That(line.shader.name, Is.EqualTo("FishGame/Line"));
            Assert.That(line.GetFloat("_Tension"), Is.EqualTo(0f), "M_Line _Tension default");
            Assert.That(line.GetFloat("_DashCount"), Is.EqualTo(20f), "M_Line _DashCount default");

            Material sky = Load("M_Sky");
            Assert.That(sky.shader.name, Is.EqualTo("FishGame/GradientSky"));
            Assert.That(sky.GetFloat("_SunIntensity"), Is.EqualTo(1.5f), "M_Sky _SunIntensity default");

            Material particle = Load("M_Particle");
            Assert.That(particle.shader.name, Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
            Assert.That(particle.renderQueue, Is.EqualTo(3000), "M_Particle queue");
            Assert.That(particle.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True, "M_Particle transparent keyword");
            Assert.That(particle.GetFloat("_Surface"), Is.EqualTo(1f), "M_Particle _Surface");
            Assert.That(particle.GetFloat("_SrcBlend"), Is.EqualTo(5f), "M_Particle _SrcBlend (SrcAlpha)");
            Assert.That(particle.GetFloat("_DstBlend"), Is.EqualTo(10f), "M_Particle _DstBlend (OneMinusSrcAlpha)");
            Assert.That(particle.GetFloat("_ZWrite"), Is.EqualTo(0f), "M_Particle _ZWrite");
            Assert.That(particle.GetTag("RenderType", false), Is.EqualTo("Transparent"), "M_Particle RenderType tag");

            Material marker = Load("M_Marker");
            Assert.That(marker.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(marker.renderQueue, Is.EqualTo(3100), "M_Marker queue");
            Assert.That(marker.GetFloat("_QueueOffset"), Is.EqualTo(100f), "M_Marker _QueueOffset (URP recomputes the queue from surface type + offset on import)");
            Assert.That(marker.GetFloat("_Surface"), Is.EqualTo(1f), "M_Marker _Surface (transparent, so the queue survives the URP importer)");
            Assert.That(marker.GetFloat("_ZWrite"), Is.EqualTo(0f), "M_Marker _ZWrite");
            Assert.That(marker.GetFloat("_Cull"), Is.EqualTo(0f), "M_Marker _Cull (Off)");
        }

        static void AssertColour(Color actual, Color expected, string label)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), label + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), label + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), label + " b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-4f), label + " a");
        }

        // Catches: components added to the profile but not saved as sub-assets (the VolumeProfile pitfall: they vanish on reload),
        // a tonemapper (spec 10.3: tonemapping None), or a third component.
        [Test]
        public void RenderingSetup_VolumeProfiles_OnlyColorAdjustmentsAndWhiteBalance()
        {
            foreach (string path in new[] { AssetPaths.WarmVolumeProfile, AssetPaths.CoolVolumeProfile })
            {
                Object profile = AssetDatabase.LoadMainAssetAtPath(path);
                Assert.That(profile, Is.Not.Null, path);
                Assert.That(profile.GetType().Name, Is.EqualTo("VolumeProfile"), path + " type");

                var serialized = new SerializedObject(profile);
                SerializedProperty components = serialized.FindProperty("components");
                Assert.That(components, Is.Not.Null, path + " components property");
                Assert.That(components.arraySize, Is.EqualTo(2), path + " component count");

                var names = new List<string>();
                for (int i = 0; i < components.arraySize; i++)
                {
                    Object component = components.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(component, Is.Not.Null, path + " component " + i + " (not saved with the asset)");
                    // AssetDatabase.IsSubAsset reports false for these hidden components even though they are saved inside the profile file,
                    // so the sub-asset property is checked directly: persistent in this very file, and not the main asset.
                    Assert.That(AssetDatabase.GetAssetPath(component), Is.EqualTo(path), component.GetType().Name + " must be saved inside " + path);
                    Assert.That(AssetDatabase.IsMainAsset(component), Is.False, component.GetType().Name + " must be a sub-asset");
                    names.Add(component.GetType().Name);
                }
                names.Sort(System.StringComparer.Ordinal);
                Assert.That(string.Join(",", names), Is.EqualTo("ColorAdjustments,WhiteBalance"), path + " component types");

                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    Assert.That(sub.GetType().Name, Is.Not.EqualTo("Tonemapping"), path + " must not hold a tonemapper");
            }
        }

        // Catches: the post-processing flag lost on the PlayerFP camera (the volume profiles would do nothing).
        [Test]
        public void PlayerPrefab_Camera_RenderPostProcessingOn()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths.PlayerPrefab);
            Assert.That(prefab, Is.Not.Null);
            Transform camera = prefab.transform.Find(PlayerRigPaths.Camera);
            Assert.That(camera, Is.Not.Null, PlayerRigPaths.Camera);

            Component data = null;
            foreach (Component component in camera.GetComponents<Component>())
                if (component != null && component.GetType().Name == "UniversalAdditionalCameraData") data = component;
            Assert.That(data, Is.Not.Null, "UniversalAdditionalCameraData on the PlayerFP camera");

            SerializedProperty post = new SerializedObject(data).FindProperty("m_RenderPostProcessing");
            Assert.That(post, Is.Not.Null, "m_RenderPostProcessing");
            Assert.That(post.boolValue, Is.True, "renderPostProcessing");
        }

        // Catches: the pipeline shadow depth bias left at 0.1 (lit Toon surfaces show diagonal shadow-acne hatching, T20 look render).
        [Test]
        public void RenderingSetup_UrpShadows_TunedAgainstAcne()
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPaths.PcRenderPipelineAsset);
            Assert.That(asset, Is.Not.Null, AssetPaths.PcRenderPipelineAsset);
            var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("m_ShadowDepthBias").floatValue, Is.EqualTo(RenderingSetupBuilder.ShadowDepthBias).Within(1e-4f), "m_ShadowDepthBias");
            Assert.That(serialized.FindProperty("m_ShadowNormalBias").floatValue, Is.EqualTo(RenderingSetupBuilder.ShadowNormalBias).Within(1e-4f), "m_ShadowNormalBias");
            Assert.That(serialized.FindProperty("m_ShadowDepthBias").floatValue, Is.GreaterThanOrEqualTo(0.5f), "depth bias must not go back to the acne-prone 0.1");
        }

        static string StripLineComments(string source)
        {
            return Regex.Replace(source, @"//[^\r\n]*", "");
        }

        static string Normalise(string text)
        {
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        // Body of an HLSL function: from its signature to the matching closing brace.
        static string FunctionBody(string source, string signaturePrefix)
        {
            int start = source.IndexOf(signaturePrefix, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "missing " + signaturePrefix);
            int open = source.IndexOf('{', start);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open + 1, i - open - 1);
            }
            Assert.Fail("unbalanced braces after " + signaturePrefix);
            return null;
        }

        // C#/HLSL wave parity (F1, C-Q4, C 16.2). The GPU cannot run in EditMode (-nographics), so this pins the HLSL source to WaveMath term for term:
        // packing (x deg, y amplitude, z wavelength, w speed), 0 deg = +Z with dir (sin, cos), k = 2 pi / L, and the non-finite guard.
        // The guard is a bit test on the exponent field (NaN and +/-Inf all have it all ones) because fast-math compilers may fold isnan/isinf away; the GPU
        // behaviour itself is proven by WaveGpuParityTests (graphics run only).
        // Catches: swapped _Waves fields, a dropped non-finite guard (CPU and GPU heights diverge on NaN/Inf), a slot-3 term, or Water.shader
        // re-implementing the term instead of calling FG_WaveHeight.
        [Test]
        public void RenderingSetup_WaterShader_WaveTermMirrorsWaveMathAndComesOnlyFromCommonHlsl()
        {
            string common = StripLineComments(ReadProjectFile(AssetPaths.ShadersFolder + "/FishGameCommon.hlsl"));
            string term = Normalise(FunctionBody(common, "float FG_WaveTerm("));
            Assert.That(term, Is.EqualTo(
                "if (any((asuint(w) & 0x7F800000u) == 0x7F800000u)) return 0.0; " +
                "if (w.z <= 0.0 || w.y == 0.0) return 0.0; " +
                "float th = radians(w.x); " +
                "float k = 6.28318530718 / w.z; " +
                "return w.y * sin(k * (dot(float2(sin(th), cos(th)), xz) - w.w * t));"), "FG_WaveTerm body");

            string height = Normalise(FunctionBody(common, "float FG_WaveHeight("));
            Assert.That(height, Is.EqualTo(
                "return FG_WaveTerm(_Waves[0], xz, t) + FG_WaveTerm(_Waves[1], xz, t) + FG_WaveTerm(_Waves[2], xz, t);"), "FG_WaveHeight body (slot 3 unused)");
            Assert.That(common, Does.Contain("float4 _Waves[4];"), "_Waves is declared with 4 entries (C 2.8 #18)");

            string water = StripLineComments(ReadProjectFile(AssetPaths.ShadersFolder + "/Water.shader"));
            Assert.That(water, Does.Contain("FG_WaveHeight(positionWS.xz, _WaterTime)"), "Water.shader calls the shared wave height with _WaterTime");
            Assert.That(water, Does.Not.Contain("_Waves"), "Water.shader must not touch _Waves directly");
            Assert.That(water, Does.Not.Contain("isfinite"), "the guard lives in FG_WaveTerm only");
            Assert.That(water, Does.Not.Contain("asuint"), "the guard lives in FG_WaveTerm only");
            Assert.That(common, Does.Not.Contain("isfinite"), "isfinite may be folded away by fast-math: use the exponent bit test");
            Assert.That(water, Does.Not.Contain("sin("), "Water.shader must not re-implement the wave term");
        }

        // Catches: the foam hash time growing without bound (float precision in floor(_WaterTime * 1.5) makes the foam pattern stutter after hours of play).
        [Test]
        public void RenderingSetup_WaterShader_FoamHashTimeWraps()
        {
            string water = Normalise(StripLineComments(ReadProjectFile(AssetPaths.ShadersFolder + "/Water.shader")));
            Assert.That(water, Does.Contain("fmod(floor(_WaterTime * 1.5), 1024.0)"), "foam hash time must wrap");
            Assert.That(water, Does.Not.Contain("+ floor(_WaterTime * 1.5)"), "the unwrapped foam hash time is back");
        }

        // Quality-first round 1 (user 2026-10-14, spec Graphics tab note): SSAO is part of the Medium/High look. Catches: Toon losing its SSAO
        // variant or never sampling the AO, the DepthNormals pass (SSAO's prepass and the water's depth) going missing, the PC renderer's SSAO
        // feature switched off or turned into an after-opaque overlay (that would darken the water and the sky too), or its tuning drifting.
        [Test]
        public void RenderingSetup_Ssao_OnAndSampledByToon()
        {
            string toon = StripLineComments(ReadProjectFile(AssetPaths.ShadersFolder + "/Toon.shader"));
            Assert.That(toon, Does.Contain("multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION"), "Toon compiles an SSAO variant");
            Assert.That(toon, Does.Contain("GetScreenSpaceAmbientOcclusion"), "Toon samples the AO");
            Assert.That(toon, Does.Contain("LightMode\" = \"DepthNormals\""), "the DepthNormals pass stays (SSAO's prepass, the water depth)");

            var renderer = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetPaths.PcRenderer));
            SerializedProperty features = renderer.FindProperty("m_RendererFeatures");
            Assert.That(features, Is.Not.Null, "m_RendererFeatures");
            int found = 0;
            for (int i = 0; i < features.arraySize; i++)
            {
                Object feature = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                found++;
                var so = new SerializedObject(feature);
                Assert.That(so.FindProperty("m_Active").boolValue, Is.True, "PC_Renderer SSAO feature is on (GraphicsApplier turns it off on Low)");
                Assert.That(so.FindProperty("m_Settings.AfterOpaque").boolValue, Is.False, "Toon samples the AO; no after-opaque overlay");
                Assert.That(so.FindProperty("m_Settings.Intensity").floatValue, Is.EqualTo(RenderingSetupBuilder.SsaoIntensity).Within(1e-4f));
                Assert.That(so.FindProperty("m_Settings.Radius").floatValue, Is.EqualTo(RenderingSetupBuilder.SsaoRadius).Within(1e-4f));
            }
            Assert.That(found, Is.EqualTo(1), "one SSAO feature on PC_Renderer");
        }

        // Catches: a pipeline asset without the camera depth texture (FishGame/Water's SampleSceneDepth depth fade and foam read the CopyDepth result;
        // without it the whole surface is the deep colour with no foam). PC and Mobile both.
        [Test]
        public void RenderingSetup_PipelineAssets_RequireDepthTexture()
        {
            foreach (string path in new[] { AssetPaths.PcRenderPipelineAsset, AssetPaths.MobileRenderPipelineAsset })
            {
                var asset = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                Assert.That(asset.FindProperty("m_RequireDepthTexture").boolValue, Is.True, path + " m_RequireDepthTexture");
            }
        }
    }
}
