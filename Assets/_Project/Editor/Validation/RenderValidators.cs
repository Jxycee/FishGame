// Render validators (C 7.20, plan T20.2; ValidationRunner hook). Owned by T20. Authority: none (editor-only, read-only checks).
// ToonMeshesHaveVertexColours: every renderer that uses a FishGame/Toon material, in every prefab under Prefabs/ and in every build scene,
// draws a mesh with a vertex colour channel (C 2.8 #19) and uses the right _JitterEncoded value (ruling BF-7: 0 for M_Toon_Fish, 1 for every
// other Toon material). Meshes are checked with HasVertexAttribute, never mesh.colors (FBX meshes are not readable).
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
        static readonly int JitterEncodedId = Shader.PropertyToID("_JitterEncoded");

        public static List<string> All()
        {
            var all = new List<string>();
            all.AddRange(ShaderValidator.Run());
            all.AddRange(ToonMeshesHaveVertexColours());
            return all;
        }

        public static List<string> ToonMeshesHaveVertexColours()
        {
            var errors = new List<string>();
            foreach (string sceneName in SceneNames.BuildOrder)
            {
                string scenePath = AssetPaths.ScenesFolder + "/" + sceneName + ".unity";
                if (!File.Exists(BuilderUtil.Abs(scenePath))) continue;   // Validators.BuildScenes reports a missing file
                ForEachSceneRoot(scenePath, errors, root => CheckRoot(scenePath, root, errors));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) CheckRoot(path, root, errors);
            }
            return errors;
        }

        static void CheckRoot(string where, GameObject root, List<string> errors)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var toon = new List<Material>();
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && material.shader != null && material.shader.name == ToonShaderName) toon.Add(material);
                if (toon.Count == 0) continue;

                string path = HierarchyPath(renderer.gameObject);
                Mesh mesh = null;
                if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }
                else if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
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
