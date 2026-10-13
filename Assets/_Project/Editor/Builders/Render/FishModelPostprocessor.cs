// Import settings for the fish models under Content/Models (C 7.20, plan T20.7). Owned by T20. Authority: none (editor).
// Materials are never imported or assigned (the reveal renderer uses M_Toon_Fish). Bump GetVersion when Apply changes, so models reimport.
// The R74 fish models carry uv1 = (slot, species layer, mapping, 0) authored in Blender; the importer must never regenerate or swap UV channels
// (generateSecondaryUV / swapUVChannels pinned off), or FishGame/Toon would read a wrong texture-array layer. ContentAssetParityTests checks it.
// EnsureImportSettings logs tris, submeshes and colours per model.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.EditorTools
{
    public sealed class FishModelPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!IsFishModelPath(assetPath)) return;
            Apply((ModelImporter)assetImporter);
        }

        static bool IsFishModelPath(string path)
        {
            return path.StartsWith(AssetPaths.ModelsFolder + "/", System.StringComparison.Ordinal)
                && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }

        // Returns true when any setting changed. Never assigns materials.
        internal static bool Apply(ModelImporter importer)
        {
            bool changed = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (importer.importNormals != ModelImporterNormals.Import) { importer.importNormals = ModelImporterNormals.Import; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.importCameras) { importer.importCameras = false; changed = true; }
            if (importer.importLights) { importer.importLights = false; changed = true; }
            if (importer.globalScale != 1f) { importer.globalScale = 1f; changed = true; }
            if (!importer.useFileScale) { importer.useFileScale = true; changed = true; }
            if (importer.isReadable) { importer.isReadable = false; changed = true; }
            if (importer.meshCompression != ModelImporterMeshCompression.Off) { importer.meshCompression = ModelImporterMeshCompression.Off; changed = true; }
            if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; changed = true; }
            if (importer.generateSecondaryUV) { importer.generateSecondaryUV = false; changed = true; }   // would overwrite the authored uv1 (the texture layer)
            if (importer.swapUVChannels) { importer.swapUVChannels = false; changed = true; }
            return changed;
        }

        // Applies the settings to every model and reimports only the ones that changed. Returns the number reimported.
        internal static int EnsureImportSettings()
        {
            int reimported = 0;
            foreach (string path in ModelPaths())
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (Apply(importer))
                {
                    importer.SaveAndReimport();
                    reimported++;
                }
                LogModel(path);
            }
            return reimported;
        }

        [MenuItem("FishGame/Dev/Reimport Fish Models")]
        public static void ReimportAll()
        {
            foreach (string path in ModelPaths())
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                Apply(importer);
                importer.SaveAndReimport();
                LogModel(path);
            }
        }

        static System.Collections.Generic.List<string> ModelPaths()
        {
            var paths = new System.Collections.Generic.List<string>();
            if (!AssetDatabase.IsValidFolder(AssetPaths.ModelsFolder)) return paths;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AssetPaths.ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsFishModelPath(path)) paths.Add(path);
            }
            return paths;
        }

        static void LogModel(string path)
        {
            int tris = 0, submeshes = 0;
            bool colours = false;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var mesh = asset as Mesh;
                if (mesh == null) continue;
                submeshes += mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) tris += (int)(mesh.GetIndexCount(i) / 3);
                if (mesh.HasVertexAttribute(VertexAttribute.Color)) colours = true;
            }
            Debug.Log("FG MODELS " + System.IO.Path.GetFileName(path) + " tris=" + tris + " submeshes=" + submeshes + " colours=" + (colours ? "yes" : "no"));
        }
    }
}
