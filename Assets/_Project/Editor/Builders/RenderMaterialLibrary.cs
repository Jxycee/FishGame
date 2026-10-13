// Toon materials (C 7.20, plan T20.5). Owned by T20; T21 adds BuildWorldMaterials (water, line, sky, particle, marker). Authority: none (editor).
// Ensure semantics (same as MaterialLibrary.Ensure): existing .mat files are updated in place so GUIDs never change; the shader is switched
// only when it differs (and then the Lit keyword leftovers are cleared); each property is set only when it differs; SetDirty only on a change.
// Ruling BF-7: _JitterEncoded is 1 for every material drawn on a MeshBuilder mesh (half-scale grey vertex colour, doubled by the shader) and
// 0 for M_Toon_Fish, whose Blender fish models carry full-scale linear albedo.
using System.Collections.Generic;
using FishGame.Core;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class RenderMaterialLibrary
    {
        const string ToonShaderName = "FishGame/Toon";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int UsePaletteId = Shader.PropertyToID("_UsePalette");
        static readonly int JitterEncodedId = Shader.PropertyToID("_JitterEncoded");
        static readonly int RampMidId = Shader.PropertyToID("_RampMid");
        static readonly int RampSoftId = Shader.PropertyToID("_RampSoft");

        // FishGame/Water look parameters (variant C of the 2026-10-12 water look-dev); keep equal to the shader property defaults.
        static readonly KeyValuePair<string, float>[] WaterLook =
        {
            new KeyValuePair<string, float>("_LightTint", 0.35f),
            new KeyValuePair<string, float>("_AmbientStrength", 0.35f),
            new KeyValuePair<string, float>("_FacetBoost", 7f),
            new KeyValuePair<string, float>("_FacetContrast", 0.14f),
            new KeyValuePair<string, float>("_FacetSoft", 0.02f),
            new KeyValuePair<string, float>("_FacetFade", 55f),
            new KeyValuePair<string, float>("_CrestAmount", 0.35f),
            new KeyValuePair<string, float>("_CrestHeight", 0.11f),
            new KeyValuePair<string, float>("_GlintPower", 96f),
            new KeyValuePair<string, float>("_GlintThreshold", 0.5f),
            new KeyValuePair<string, float>("_GlintStrength", 1f),
            new KeyValuePair<string, float>("_GlintFlatten", 0.7f),
            new KeyValuePair<string, float>("_GlintNear", 6f),
            new KeyValuePair<string, float>("_ReflectStrength", 0.4f),
            new KeyValuePair<string, float>("_ReflectZenith", 0.6f),
            new KeyValuePair<string, float>("_FresnelPower", 4f),
            new KeyValuePair<string, float>("_Saturation", 1.15f),
            new KeyValuePair<string, float>("_FogAmount", 0.35f),
        };
        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        static readonly int RimPowId = Shader.PropertyToID("_RimPow");
        static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");

        public static void BuildAll()
        {
            Shader toon = Shader.Find(ToonShaderName);
            if (toon == null) throw new System.InvalidOperationException("Shader not found: " + ToonShaderName);
            BuilderUtil.EnsureFolder(AssetPaths.MaterialsFolder);

            EnsureToon(toon, "M_Toon_Base", true, true, Color.white);
            EnsureToon(toon, "M_Toon_Fish", false, false, Color.white);
            EnsureToon(toon, "M_Toon_FishFallback", false, true, Color.white);
            for (int i = 0; i < SlotPalette.Count; i++)
            {
                SlotPalette.GetRgb(i, out float r, out float g, out float b);
                EnsureToon(toon, "M_Body_Slot" + i, false, true, new Color(r, g, b));
            }
            SlotPalette.GetSkinRgb(out float sr, out float sg, out float sb);
            EnsureToon(toon, "M_Skin", false, true, new Color(sr, sg, sb));

            // BuildWorldMaterials(WaterSettingsDef) (T21) is called from RenderingSetupBuilder.Build, after T14's WaterSettings asset exists (ruling F5).

            FishModelPostprocessor.EnsureImportSettings();
        }

        // T21 (ruling F5): water, line, sky, particle and marker materials. Same ensure semantics as BuildAll.
        // M_Water_Surface copies colours and ranges from the WaterSettingsDef asset (a designer retune needs a build).
        internal static void BuildWorldMaterials(FishGame.Data.WaterSettingsDef water)
        {
            if (water == null) throw new System.ArgumentNullException(nameof(water));
            BuilderUtil.EnsureFolder(AssetPaths.MaterialsFolder);

            Material surface = EnsureMaterial("M_Water_Surface", RequireShader("FishGame/Water"), out bool surfaceChanged);
            surfaceChanged |= SetColor(surface, Shader.PropertyToID("_ShallowColor"), water.ShallowColor);
            surfaceChanged |= SetColor(surface, Shader.PropertyToID("_DeepColor"), water.DeepColor);
            surfaceChanged |= SetFloat(surface, Shader.PropertyToID("_DepthRange"), water.DepthRange);
            surfaceChanged |= SetFloat(surface, Shader.PropertyToID("_FoamWidth"), water.FoamWidth);
            // The stylised look parameters (FishGame/Water header) are pinned to the shader defaults so an existing material follows a shader retune.
            foreach (var p in WaterLook) surfaceChanged |= SetFloat(surface, Shader.PropertyToID(p.Key), p.Value);
            surfaceChanged |= SetColor(surface, Shader.PropertyToID("_FoamColor"), new Color(0.96f, 0.99f, 1.0f, 1.0f));
            if (surfaceChanged) EditorUtility.SetDirty(surface);

            Material line = EnsureMaterial("M_Line", RequireShader("FishGame/Line"), out bool lineChanged);   // shader defaults
            if (lineChanged) EditorUtility.SetDirty(line);
            Material sky = EnsureMaterial("M_Sky", RequireShader("FishGame/GradientSky"), out bool skyChanged);   // shader defaults
            if (skyChanged) EditorUtility.SetDirty(sky);

            Material particle = EnsureMaterial("M_Particle", RequireShader("Universal Render Pipeline/Particles/Unlit"), out bool particleChanged);
            particleChanged |= SetTransparentAlphaBlend(particle, 3000);
            particleChanged |= SetColor(particle, BaseColorId, Color.white);
            // The URP importer writes (0, +Infinity, 0, 0) here while camera fading is off; match it so the build and the import agree.
            particleChanged |= SetVector(particle, Shader.PropertyToID("_CameraFadeParams"), new Vector4(0f, float.PositiveInfinity, 0f, 0f));
            if (particleChanged) EditorUtility.SetDirty(particle);

            // Queue 3100 draws after the transparent water (ZWrite Off), so the marker is never tinted or hidden by it. Cull Off: marker winding never matters.
            // URP's material importer recomputes the queue of every URP/Unlit material (opaque = 2000 + _QueueOffset, transparent = 3000 + _QueueOffset) and
            // there is no queue override on this shader, so an opaque marker would be reset to 2000 on the next import. The marker is therefore an alpha-blend
            // transparent surface with _QueueOffset 100, which URP itself resolves to 3100 (a stable fixed point; _BaseColor alpha 1 keeps it solid).
            Material marker = EnsureMaterial("M_Marker", RequireShader("Universal Render Pipeline/Unlit"), out bool markerChanged);
            markerChanged |= SetColor(marker, BaseColorId, Color.white);
            markerChanged |= SetFloat(marker, Shader.PropertyToID("_Cull"), 0f);
            markerChanged |= SetFloat(marker, Shader.PropertyToID("_QueueOffset"), 100f);
            markerChanged |= SetTransparentAlphaBlend(marker, 3100);
            markerChanged |= SetPassEnabled(marker, "MotionVectors", false);   // what the URP importer settles on for a double-sided (Cull Off) Unlit material
            if (!marker.doubleSidedGI) { marker.doubleSidedGI = true; markerChanged = true; }
            if (markerChanged) EditorUtility.SetDirty(marker);
        }

        // URP transparent surface, alpha blend, ZWrite off: exactly what BaseShaderGUI.SetupMaterialBlendMode would write (so an import changes nothing).
        static bool SetTransparentAlphaBlend(Material material, int queue)
        {
            bool changed = false;
            changed |= SetFloat(material, Shader.PropertyToID("_Surface"), 1f);
            changed |= SetFloat(material, Shader.PropertyToID("_Blend"), 0f);
            changed |= SetFloat(material, Shader.PropertyToID("_SrcBlend"), 5f);          // SrcAlpha
            changed |= SetFloat(material, Shader.PropertyToID("_DstBlend"), 10f);         // OneMinusSrcAlpha
            changed |= SetFloat(material, Shader.PropertyToID("_SrcBlendAlpha"), 1f);
            changed |= SetFloat(material, Shader.PropertyToID("_DstBlendAlpha"), 10f);
            changed |= SetFloat(material, Shader.PropertyToID("_ZWrite"), 0f);
            changed |= SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
            if (material.GetTag("RenderType", false) != "Transparent") { material.SetOverrideTag("RenderType", "Transparent"); changed = true; }
            changed |= SetQueue(material, queue);
            changed |= SetPassEnabled(material, "DepthOnly", false);       // a transparent surface does not write depth or cast shadows (URP importer does the same)
            changed |= SetPassEnabled(material, "ShadowCaster", false);
            return changed;
        }

        // Exact component compare (Vector4 == is approximate and never equal for Infinity).
        static bool SetVector(Material material, int id, Vector4 value)
        {
            if (material.HasProperty(id))
            {
                Vector4 current = material.GetVector(id);
                if (current.x == value.x && current.y == value.y && current.z == value.z && current.w == value.w) return false;
            }
            material.SetVector(id, value);
            return true;
        }

        static bool SetPassEnabled(Material material, string pass, bool enabled)
        {
            if (material.GetShaderPassEnabled(pass) == enabled) return false;
            material.SetShaderPassEnabled(pass, enabled);
            return true;
        }

        static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + name);
            return shader;
        }

        // Loads or creates MaterialsFolder/<name>.mat. The shader is switched only when it differs (then the keywords are cleared).
        static Material EnsureMaterial(string name, Shader shader, out bool changed)
        {
            string path = AssetPaths.MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            changed = false;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material.shader != shader)
            {
                material.shader = shader;
                material.shaderKeywords = new string[0];
                changed = true;
            }
            return material;
        }

        static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled) return false;
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
            return true;
        }

        static bool SetQueue(Material material, int queue)
        {
            if (material.renderQueue == queue) return false;
            material.renderQueue = queue;
            return true;
        }

        // name without extension, e.g. "M_Toon_Base".
        public static Material Get(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
            if (material == null) throw new System.InvalidOperationException("Material not built: " + name + " (run RenderMaterialLibrary.BuildAll first)");
            return material;
        }

        static void EnsureToon(Shader toon, string name, bool usePalette, bool jitterEncoded, Color baseColor)
        {
            string path = AssetPaths.MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(toon);
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material.shader != toon)
            {
                material.shader = toon;
                material.shaderKeywords = new string[0];   // drops the Lit leftovers (_NORMALMAP and friends)
                changed = true;
            }
            changed |= SetColor(material, BaseColorId, baseColor);
            changed |= SetFloat(material, UsePaletteId, usePalette ? 1f : 0f);
            changed |= SetFloat(material, JitterEncodedId, jitterEncoded ? 1f : 0f);
            changed |= SetFloat(material, RampMidId, 0.5f);
            changed |= SetFloat(material, RampSoftId, 0.04f);
            changed |= SetFloat(material, DesaturateId, 0f);
            changed |= SetFloat(material, RimPowId, 4f);
            changed |= SetFloat(material, RimStrengthId, 0f);
            if (changed) EditorUtility.SetDirty(material);
        }

        static bool SetFloat(Material material, int id, float value)
        {
            if (material.HasProperty(id) && material.GetFloat(id) == value) return false;
            material.SetFloat(id, value);
            return true;
        }

        static bool SetColor(Material material, int id, Color value)
        {
            if (material.HasProperty(id) && material.GetColor(id) == value) return false;
            material.SetColor(id, value);
            return true;
        }
    }
}
