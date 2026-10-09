// Flat URP Lit placeholder materials (contract 6.2, spec 10.1). Updated in place, so GUIDs never change. Owned by T15.
using FishGame.Core;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class MaterialLibrary
    {
        const string LitShaderName = "Universal Render Pipeline/Lit";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void BuildAll()
        {
            BuilderUtil.EnsureFolder(AssetPaths.MaterialsFolder);
            Shader lit = Shader.Find(LitShaderName);
            if (lit == null) throw new System.InvalidOperationException("Shader not found: " + LitShaderName);

            for (int i = 0; i < SlotPalette.Count; i++)
            {
                SlotPalette.GetRgb(i, out float r, out float g, out float b);
                Ensure(lit, "M_Body_Slot" + i, new Color(r, g, b));
            }
            SlotPalette.GetSkinRgb(out float sr, out float sg, out float sb);
            Ensure(lit, "M_Skin", new Color(sr, sg, sb));
            Ensure(lit, "M_Water", new Color(0.12f, 0.36f, 0.62f));
            Ensure(lit, "M_Wood", new Color(0.55f, 0.38f, 0.22f));
            Ensure(lit, "M_WoodDark", new Color(0.36f, 0.24f, 0.14f));
            Ensure(lit, "M_Rock", new Color(0.46f, 0.46f, 0.48f));
            Ensure(lit, "M_Grass", new Color(0.32f, 0.56f, 0.26f));
            Ensure(lit, "M_Sand", new Color(0.84f, 0.77f, 0.56f));
            Ensure(lit, "M_Foliage", new Color(0.18f, 0.40f, 0.20f));
            Ensure(lit, "M_Brass", new Color(0.82f, 0.66f, 0.26f));
        }

        // name without extension, e.g. "M_Body_Slot0".
        public static Material Get(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
            if (material == null) throw new System.InvalidOperationException("Material not built: " + name + " (run MaterialLibrary.BuildAll first)");
            return material;
        }

        static void Ensure(Shader lit, string name, Color color)
        {
            string path = AssetPaths.MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material.shader != lit) { material.shader = lit; changed = true; }
            if (material.GetColor(BaseColorId) != color) { material.SetColor(BaseColorId, color); changed = true; }
            if (changed) EditorUtility.SetDirty(material);
        }
    }
}
