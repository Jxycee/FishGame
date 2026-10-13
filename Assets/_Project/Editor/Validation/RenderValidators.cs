// Render validators (C 7.20, plan T20.2; ValidationRunner hook). Owned by T20. Authority: none (editor-only, read-only checks).
// ToonMeshesHaveVertexColours: every renderer that uses a FishGame/Toon material, in every prefab under Prefabs/ and in every build scene,
// draws a mesh with a vertex colour channel (C 2.8 #19) and uses the right _JitterEncoded value (ruling BF-7: 0 for M_Toon_Fish, 1 for every
// other Toon material). Meshes are checked with HasVertexAttribute, never mesh.colors (FBX meshes are not readable).
// ToonMeshMappingMatchesMaterial: for a renderer whose Toon material is textured (_FGTexWeight > 0), the mesh's uv1.z (the mapping it was built for,
// SmoothMeshBuilder.MappingUv 0 / MappingTriplanar 1) must equal 1 when the material has the _FG_TRIPLANAR keyword enabled and 0 otherwise. The shader
// itself ignores uv1.z; this catches a rock or canopy mesh put on a uv0 material (stretched texture) or a plank put on the triplanar one.
// Scenes are opened additively one at a time like Validators.MeshReferences does (its helper is private, so this is a small local copy).
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class RenderValidators
    {
        const string PrefabsFolder = "Assets/_Project/Prefabs";
        const string ToonShaderName = "FishGame/Toon";
        const string FishMaterialName = "M_Toon_Fish";
        const string TriplanarKeyword = "_FG_TRIPLANAR";
        static readonly int JitterEncodedId = Shader.PropertyToID("_JitterEncoded");
        static readonly int TexWeightId = Shader.PropertyToID("_FGTexWeight");

        public static List<string> All()
        {
            var all = new List<string>();
            all.AddRange(ShaderValidator.Run());
            all.AddRange(ToonMeshesHaveVertexColours());
            all.AddRange(ToonMeshMappingMatchesMaterial());
            return all;
        }

        public static List<string> ToonMeshesHaveVertexColours() => ToonMeshesHaveVertexColours(PrefabsFolder);

        // Same check with the prefab folder given (tests point it at Tests/__Temp so no test prefab ever lands in the shipped Prefabs/ folder).
        internal static List<string> ToonMeshesHaveVertexColours(string prefabsFolder)
        {
            return Scan(prefabsFolder, CheckRoot);
        }

        public static List<string> ToonMeshMappingMatchesMaterial() => ToonMeshMappingMatchesMaterial(PrefabsFolder);

        internal static List<string> ToonMeshMappingMatchesMaterial(string prefabsFolder)
        {
            var zRanges = new Dictionary<Mesh, Vector2>();   // one uv1.z scan per mesh, however many renderers share it
            return Scan(prefabsFolder, (where, root, errors) => CheckMappingRoot(where, root, errors, zRanges));
        }

        // Visits every build scene (opened additively) and every prefab under the folder.
        static List<string> Scan(string prefabsFolder, System.Action<string, GameObject, List<string>> checkRoot)
        {
            var errors = new List<string>();
            foreach (string sceneName in SceneNames.BuildOrder)
            {
                string scenePath = AssetPaths.ScenesFolder + "/" + sceneName + ".unity";
                if (!File.Exists(BuilderUtil.Abs(scenePath))) continue;   // Validators.BuildScenes reports a missing file
                ForEachSceneRoot(scenePath, errors, root => checkRoot(scenePath, root, errors));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) checkRoot(path, root, errors);
            }
            return errors;
        }

        static List<Material> ToonMaterials(Renderer renderer)
        {
            var toon = new List<Material>();
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.shader != null && material.shader.name == ToonShaderName) toon.Add(material);
            return toon;
        }

        // The renderer's mesh (null for no mesh); false when the renderer type is not a mesh or skinned renderer.
        static bool TryGetMesh(Renderer renderer, out Mesh mesh)
        {
            mesh = null;
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                mesh = filter != null ? filter.sharedMesh : null;
                return true;
            }
            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
                return true;
            }
            return false;
        }

        static void CheckRoot(string where, GameObject root, List<string> errors)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                List<Material> toon = ToonMaterials(renderer);
                if (toon.Count == 0) continue;

                string path = HierarchyPath(renderer.gameObject);
                if (!TryGetMesh(renderer, out Mesh mesh))
                {
                    errors.Add(where + ": " + path + " unsupported renderer " + renderer.GetType().Name + " uses " + ToonShaderName);
                    continue;
                }

                if (mesh == null) errors.Add(where + ": " + path + " renders " + ToonShaderName + " with no mesh");
                else if (!mesh.HasVertexAttribute(VertexAttribute.Color))
                    errors.Add(where + ": " + path + " (" + mesh.name + ") renders " + ToonShaderName + " without vertex colours");

                foreach (Material material in toon)
                {
                    float expected = material.name == FishMaterialName ? 0f : 1f;
                    if (!material.HasProperty(JitterEncodedId) || !Mathf.Approximately(material.GetFloat(JitterEncodedId), expected))
                        errors.Add(where + ": " + path + " material " + material.name + " has the wrong _JitterEncoded");
                }
            }
        }

        // Renderers with no mesh, or of another type, are reported by CheckRoot; this check skips them.
        static void CheckMappingRoot(string where, GameObject root, List<string> errors, Dictionary<Mesh, Vector2> zRanges)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                List<Material> toon = ToonMaterials(renderer);
                if (toon.Count == 0 || !TryGetMesh(renderer, out Mesh mesh) || mesh == null) continue;

                foreach (Material material in toon)
                {
                    if (!material.HasProperty(TexWeightId) || material.GetFloat(TexWeightId) <= 0f) continue;   // untextured: the mapping is never used

                    bool triplanar = material.IsKeywordEnabled(TriplanarKeyword);
                    float expected = triplanar ? 1f : 0f;
                    if (!zRanges.TryGetValue(mesh, out Vector2 range))
                    {
                        range = Uv1ZRange(mesh);
                        zRanges[mesh] = range;
                    }
                    if (Mathf.Approximately(range.x, expected) && Mathf.Approximately(range.y, expected)) continue;

                    errors.Add(where + ": " + HierarchyPath(renderer.gameObject) + " mesh " + mesh.name + " has uv1.z in [" + range.x + ", " + range.y
                        + "] but textured material " + material.name + " is " + (triplanar ? "triplanar" : "uv0-mapped") + " (" + TriplanarKeyword + " "
                        + (triplanar ? "on" : "off") + "), which needs uv1.z == " + expected
                        + " (the mesh was built for the other mapping; put it on the matching material or rebuild it)");
                }
            }
        }

        // (min, max) of uv1.z over the mesh; a mesh without uv1 reads as 0 (what the shader sees), so (0, 0).
        static Vector2 Uv1ZRange(Mesh mesh)
        {
            if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) return Vector2.zero;
            var uv1 = new List<Vector4>();
            mesh.GetUVs(1, uv1);
            if (uv1.Count == 0) return Vector2.zero;
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector4 v in uv1)
            {
                if (v.z < min) min = v.z;
                if (v.z > max) max = v.z;
            }
            return new Vector2(min, max);
        }

        // Opens the scene additively unless it is already loaded and closes it (without saving) only if this call opened it.
        static void ForEachSceneRoot(string scenePath, List<string> errors, System.Action<GameObject> visitRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!errors.Contains("scene checks need edit mode")) errors.Add("scene checks need edit mode");
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasValid = scene.IsValid();
            bool opened = !(wasValid && scene.isLoaded);
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects()) visitRoot(root);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, !wasValid);
            }
        }

        static string HierarchyPath(GameObject go)
        {
            string path = go.name;
            for (Transform t = go.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return path;
        }
    }
}
