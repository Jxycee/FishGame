// Flat URP Lit placeholder materials (contract 6.2, spec 10.1). Updated in place, so GUIDs never change. Owned by T15.
// Body slot and skin materials are FishGame/Toon, built by RenderMaterialLibrary (T20); the props stay Lit until T28.
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

            Ensure(lit, "M_Water", new Color(0.12f, 0.36f, 0.62f));
            // M_Wood and M_Brass: since the R74 adoption no scene or prefab uses them (the props are M_Toon_Base / M_Toon_Brass), but they stay. This builder
            // recreates every entry here on each build, so deleting only the assets would leave them reappearing; and PlayerFishingRigBuilder falls back
            // to M_WoodDark and M_Brass for the rod and bobber when M_Toon_Base is missing (a rig built before the Toon materials exist).
            Ensure(lit, "M_Wood", new Color(0.55f, 0.38f, 0.22f));
            Ensure(lit, "M_WoodDark", new Color(0.36f, 0.24f, 0.14f));
            Ensure(lit, "M_Rock", new Color(0.46f, 0.46f, 0.48f));
            Ensure(lit, "M_Grass", new Color(0.32f, 0.56f, 0.26f));
            Ensure(lit, "M_Sand", new Color(0.84f, 0.77f, 0.56f));
            Ensure(lit, "M_Foliage", new Color(0.18f, 0.40f, 0.20f));
            Ensure(lit, "M_Brass", new Color(0.82f, 0.66f, 0.26f));

            RenderMaterialLibrary.BuildAll();   // T20: the Toon materials, including the body slots and skin
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
