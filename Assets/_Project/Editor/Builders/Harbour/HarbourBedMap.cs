// Harbour round 3 (2026-10-17): the water's bed map. Authority: none (editor builder; writes a texture asset and sets two properties on
// M_Water_Harbour). The Blender generator samples the NATURAL bed under the harbour water (the code beach and seabed, the coast landforms,
// the creek lagoon's and east basin's visual floors: smooth maxima, blurred) and a zone tint (1 = the creek lagoon) per 1 m texel centre
// (fg_harbour.py build_bedmap -> harbour_bedmap.json next to the manifest). This bakes it into T_HarbourBedMap (RGBA half: r = bed y in
// metres, g = zone; bilinear, clamped; fixed order, no random: byte-identical rebuilds) and points M_Water_Harbour at it (FishGame/Water
// item 13): the body colour and opacity follow the bed map, so boulders, hulls and piles near the surface stop drawing as turquoise ghosts.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class HarbourBedMap
    {
        public const string SourcePath = HarbourModelPostprocessor.HarbourModelsFolder + "/harbour_bedmap.json";
        public const string TexturePath = AssetPaths.TexturesFolder + "/T_HarbourBedMap.asset";
        public const string MaterialName = "M_Water_Harbour";

        [System.Serializable]
        sealed class Source
        {
            public float x0, z0, cell;
            public int width, height;
            public int[] bed_cm;
            public int[] zone;
        }

        static Source Load()
        {
            if (!File.Exists(SourcePath)) throw new System.InvalidOperationException("Missing " + SourcePath + " (run fg_harbour.py)");
            Source src = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
            int n = src.width * src.height;
            if (src.width < 2 || src.height < 2 || src.bed_cm == null || src.zone == null || src.bed_cm.Length != n || src.zone.Length != n || !(src.cell > 0f))
                throw new System.InvalidOperationException(SourcePath + " is malformed");
            return src;
        }

        // The texture, unsaved, and the rect (x0, z0, 1/width, 1/height in metres). Test seam: the caller destroys it.
        internal static Texture2D BuildInMemory(out Vector4 rect)
        {
            Source src = Load();
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBAHalf, false, true)
            {
                name = Path.GetFileNameWithoutExtension(TexturePath),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            var px = new Color[src.width * src.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(src.bed_cm[i] * 0.01f, src.zone[i] / 255f, 0f, 1f);
            tex.SetPixels(px);
            tex.Apply(false, false);
            // texel centres sit at x0 + i * cell: u = (x - (x0 - cell / 2)) / (width * cell)
            rect = new Vector4(src.x0 - 0.5f * src.cell, src.z0 - 0.5f * src.cell, 1f / (src.width * src.cell), 1f / (src.height * src.cell));
            return tex;
        }

        public static void Build()
        {
            Texture2D built = BuildInMemory(out Vector4 rect);
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(TexturePath));
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Texture2D tex;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, TexturePath);
                tex = built;
            }
            else
            {
                EditorUtility.CopySerialized(built, existing);
                existing.name = built.name;
                Object.DestroyImmediate(built);
                EditorUtility.SetDirty(existing);
                tex = existing;
            }
            Material water = RenderMaterialLibrary.Get(MaterialName);
            int texId = Shader.PropertyToID("_BedMap"), rectId = Shader.PropertyToID("_BedMapST");
            if (water.GetTexture(texId) != tex) { water.SetTexture(texId, tex); EditorUtility.SetDirty(water); }
            if (water.GetVector(rectId) != rect) { water.SetVector(rectId, rect); EditorUtility.SetDirty(water); }
            AssetDatabase.SaveAssets();
        }
    }
}
