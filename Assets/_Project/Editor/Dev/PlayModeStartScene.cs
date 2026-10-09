// Play from any scene: the play-mode start scene is Bootstrap (spec 4.6). Never set in batchmode. Owned by T15.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FishGame.EditorTools
{
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        static PlayModeStartScene()
        {
            // The AssetDatabase is not ready inside an InitializeOnLoad constructor; wait one editor tick.
            EditorApplication.delayCall += Apply;
        }

        public static void Apply()
        {
            if (Application.isBatchMode) { Clear(); return; }
            var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetPaths.BootstrapScene);
            if (bootstrap != null && EditorSceneManager.playModeStartScene != bootstrap)
                EditorSceneManager.playModeStartScene = bootstrap;
        }

        public static void Clear()
        {
            if (EditorSceneManager.playModeStartScene != null) EditorSceneManager.playModeStartScene = null;
        }
    }
}
