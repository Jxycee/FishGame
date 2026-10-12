// Data assets (spec 4.6 step 2). Create-if-missing only: the user tunes these in the Inspector, so existing values are never overwritten.
// The only edit to an existing asset is adding lake_dock to the registry list and filling an empty PanelSettings theme. Owned by T15.
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.EditorTools
{
    public static class ContentBuilder
    {
        public static void Build()
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(AssetPaths.LakeDockSpotAsset));
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(AssetPaths.PanelSettingsAsset));
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(AssetPaths.NetworkPrefabsListAsset));
            MaterialLibrary.BuildAll();
            SpotDef lakeDock = EnsureLakeDockSpot();
            EnsureRegistry(lakeDock);
            EnsurePanelSettings();
            FishingContentBuilder.Build();
            AssetDatabase.SaveAssets();
        }

        static SpotDef EnsureLakeDockSpot()
        {
            var spot = AssetDatabase.LoadAssetAtPath<SpotDef>(AssetPaths.LakeDockSpotAsset);
            if (spot != null) return spot;
            spot = ScriptableObject.CreateInstance<SpotDef>();
            spot.Configure(global::FishGame.Core.ContentIds.LakeDock, "Lake Dock", SceneNames.SpotLakeDock);
            AssetDatabase.CreateAsset(spot, AssetPaths.LakeDockSpotAsset);
            return spot;
        }

        static void EnsureRegistry(SpotDef lakeDock)
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ContentRegistry>();
                registry.Configure(1, new[] { lakeDock });
                AssetDatabase.CreateAsset(registry, AssetPaths.ContentRegistryAsset);
                return;
            }
            var serialized = new SerializedObject(registry);
            SerializedProperty spots = serialized.FindProperty("spots");
            if (spots == null) throw new System.InvalidOperationException("ContentRegistry has no serialized field 'spots'");
            for (int i = 0; i < spots.arraySize; i++)
                if (spots.GetArrayElementAtIndex(i).objectReferenceValue == lakeDock) return;
            spots.InsertArrayElementAtIndex(spots.arraySize);
            spots.GetArrayElementAtIndex(spots.arraySize - 1).objectReferenceValue = lakeDock;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Contract 6.2: Scale With Screen Size, 1920x1080, MatchWidthOrHeight 0.5, theme = FishGameTheme.tss.
        static void EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPaths.PanelSettingsAsset);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                AssetDatabase.CreateAsset(panel, AssetPaths.PanelSettingsAsset);
            }
            if (panel.themeStyleSheet == null)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetPaths.ThemeStyleSheet);
                if (theme == null) throw new System.IO.FileNotFoundException("Missing UI theme " + AssetPaths.ThemeStyleSheet + " (T14)");
                panel.themeStyleSheet = theme;
                EditorUtility.SetDirty(panel);
            }
        }
    }
}
