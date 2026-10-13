// Scene objects of the world presentation (contract 7.22). Called from LakeDockBuilder.Build right after FishingSceneObjects.AddTo.
// "Presentation" is a plain GameObject (C 2.8 #15: the only in-scene NetworkObject is the World root, #24) holding the six scene-level drivers
// (PaletteGlobals, WaterTimeDriver, RippleField, T18's DayNightController, and T19's WeatherFx and HotspotFx).
// Assets are loaded here, after NewScene (C 2.8 #14); a missing asset is a build error, not a silent null.
using FishGame.Core;
using FishGame.Data;
using FishGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
            DayNightController dayNight = BuilderUtil.GetOrAdd<DayNightController>(root);
            WeatherFx weatherFx = BuilderUtil.GetOrAdd<WeatherFx>(root);
            HotspotFx hotspotFx = BuilderUtil.GetOrAdd<HotspotFx>(root);
            Material particle = RenderMaterialLibrary.Get("M_Particle");   // throws InvalidOperationException naming the material when it is not built

            SetReference(globals, "palette", palette);
            SetReference(driver, "waterSettings", water);
            SetReference(dayNight, "palette", palette);
            SetReference(dayNight, "sun", FindRoot<Light>(scene, "Sun"));
            SetReference(dayNight, "warmVolume", FindRoot<Volume>(scene, "WarmVolume"));
            SetReference(dayNight, "coolVolume", FindRoot<Volume>(scene, "CoolVolume"));
            SetReference(weatherFx, "particleMaterial", particle);
            SetReference(hotspotFx, "particleMaterial", particle);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // The component of the scene root with that name (C 6.3: every Spot scene has Sun, WarmVolume and CoolVolume before this runs).
        private static T FindRoot<T>(Scene scene, string name) where T : Component
        {
            foreach (GameObject candidate in scene.GetRootGameObjects())
            {
                if (candidate.name != name) continue;
                T component = candidate.GetComponent<T>();
                if (component != null) return component;
            }
            throw new System.InvalidOperationException("Spot scene '" + scene.name + "' has no root '" + name + "' with a " + typeof(T).Name + " (C 6.3)");
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
