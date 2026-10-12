// Scene objects of the world presentation (contract 7.22). Called from LakeDockBuilder.Build right after FishingSceneObjects.AddTo.
// "Presentation" is a plain GameObject (C 2.8 #15: no in-scene NetworkObjects) holding the three scene-level drivers.
// Assets are loaded here, after NewScene (C 2.8 #14); a missing asset is a build error, not a silent null.
using FishGame.Core;
using FishGame.Data;
using FishGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class PresentationSceneObjects
    {
        private const string RootName = "Presentation";

        public static void AddTo(Scene scene)
        {
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            if (palette == null) throw new System.InvalidOperationException("Missing ArtPaletteDef at " + AssetPaths.ArtPaletteAsset);
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            if (water == null) throw new System.InvalidOperationException("Missing WaterSettingsDef at " + AssetPaths.WaterSettingsAsset);

            GameObject root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
            {
                if (candidate.name == RootName) { root = candidate; break; }
            }
            if (root == null)
            {
                root = new GameObject(RootName);
                root.layer = Layers.Default;
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            PaletteGlobals globals = BuilderUtil.GetOrAdd<PaletteGlobals>(root);
            WaterTimeDriver driver = BuilderUtil.GetOrAdd<WaterTimeDriver>(root);
            BuilderUtil.GetOrAdd<RippleField>(root);

            SetReference(globals, "palette", palette);
            SetReference(driver, "waterSettings", water);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException(target.GetType().Name + " has no serialized field '" + field + "'");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
