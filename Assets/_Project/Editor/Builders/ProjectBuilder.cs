// The single build entry point (spec 4.6; contract 7.15, D40) and FishGame/Build All. Owned by T15.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class ProjectBuilder
    {
        [MenuItem("FishGame/Build All")]
        public static void BuildAllMenu()
        {
            // NewScene(Single) discards the open scene, so offer to save it first. Cancel aborts the build.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("FG BUILDALL CANCELLED");
                return;
            }
            Execute();
            PlayModeStartScene.Apply(); // a first build in the editor arms the play-from-any-scene hook
        }

        // Batchmode entry: -executeMethod FishGame.EditorTools.ProjectBuilder.BuildAll, always together with -quit.
        // Success returns normally, so -quit prints "Exiting batchmode successfully" (D40). Failure exits 1 in batchmode only.
        public static void BuildAll()
        {
            bool ok = Execute();
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        // Runs every step even after a failure and collects the errors. Saves before validating (the validators read disk).
        public static bool RunAll(List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Step(errors, "ProjectSettings", ProjectSettingsBuilder.Apply);
            Step(errors, "Content", ContentBuilder.Build);
            Step(errors, "Prefabs", PrefabBuilder.Build);
            Step(errors, "NetworkPrefabs", NetworkPrefabsBuilder.Build);
            Step(errors, "Scenes", SceneBuilder.BuildAll);
            Step(errors, "BuildSettings", SceneBuilder.ApplyBuildSettings);
            Step(errors, "Save", AssetDatabase.SaveAssets);
            Step(errors, "Validate", () => errors.AddRange(ValidationRunner.Run()));
            return errors.Count == 0;
        }

        static bool Execute()
        {
            var errors = new List<string>();
            if (RunAll(errors))
            {
                Debug.Log("FG BUILDALL PASS");
                return true;
            }
            Debug.Log("FG BUILDALL FAIL errors=" + errors.Count);
            foreach (string error in errors) Debug.Log("FG BUILDALL ERROR " + error);
            return false;
        }

        static void Step(List<string> errors, string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                errors.Add(label + ": " + e.GetType().Name + ": " + e.Message);
                Debug.LogException(e);
            }
        }
    }
}
