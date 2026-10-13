// Import fix-up for the Blender harbour models (T17-B, plan T17.3). Owned by T17. Authority: none (editor).
// The FBX carries the second UV map "Layer" = (palette slot, texture layer) and no mapping channel: the mapping comes from the material the
// builder picks (a "_Tri" model goes on M_Toon_Tri, world-triplanar; everything else on M_Toon_Base, uv0). RenderValidators.ToonMeshMappingMatchesMaterial
// wants uv1.z == 1 on a mesh drawn with a triplanar material (SmoothMeshBuilder.MappingTriplanar), and FBX uv channels are two-component, so this
// postprocessor writes uv1 as (slot, layer, 1, 0) on every vertex of every Content/Models/Harbour/*_Tri.fbx mesh. FishModelPostprocessor
// (which applies to the whole Models folder, this subfolder included) keeps the shared importer settings and stays unchanged.
using System.Collections.Generic;
using FishGame.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.EditorTools
{
    public sealed class HarbourModelPostprocessor : AssetPostprocessor
    {
        public const string HarbourModelsFolder = AssetPaths.ModelsFolder + "/Harbour";
        public const string TriplanarSuffix = "_Tri";

        public override uint GetVersion() => 1;

        // True for Models/Harbour/<name>_Tri.fbx.
        public static bool IsTriplanarHarbourModel(string path)
        {
            if (path == null) return false;
            if (!path.StartsWith(HarbourModelsFolder + "/", System.StringComparison.Ordinal)) return false;
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name.EndsWith(TriplanarSuffix, System.StringComparison.Ordinal);
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsTriplanarHarbourModel(assetPath)) return;
            var uv1 = new List<Vector4>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) continue;   // a missing uv1 is reported by the bounds test
                uv1.Clear();
                mesh.GetUVs(1, uv1);
                for (int i = 0; i < uv1.Count; i++)
                {
                    Vector4 v = uv1[i];
                    uv1[i] = new Vector4(v.x, v.y, SmoothMeshBuilder.MappingTriplanar, 0f);
                }
                mesh.SetUVs(1, uv1);
            }
        }
    }
}
