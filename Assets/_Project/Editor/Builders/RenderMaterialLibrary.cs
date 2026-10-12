// Toon materials (C 7.20, plan T20.5). Owned by T20; T21 adds BuildWorldMaterials (water, line, sky, particle, marker). Authority: none (editor).
// Ensure semantics (same as MaterialLibrary.Ensure): existing .mat files are updated in place so GUIDs never change; the shader is switched
// only when it differs (and then the Lit keyword leftovers are cleared); each property is set only when it differs; SetDirty only on a change.
// Ruling BF-7: _JitterEncoded is 1 for every material drawn on a MeshBuilder mesh (half-scale grey vertex colour, doubled by the shader) and
// 0 for M_Toon_Fish, whose Blender fish models carry full-scale linear albedo.
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

            // T21 adds RenderMaterialLibrary.BuildWorldMaterials(WaterSettingsDef) here (called from RenderingSetupBuilder.Build, ruling F5).

            FishModelPostprocessor.EnsureImportSettings();
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
