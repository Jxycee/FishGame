// Runs every validator (spec 4.6 step 7) and provides FishGame/Validate. Owned by T15.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class ValidationRunner
    {
        public static List<string> Run()
        {
            var all = new List<string>();
            Add(all, "ContentIds", Validators.ContentIds());
            Add(all, "NetworkPrefabs", Validators.NetworkPrefabs());
            Add(all, "BuildScenes", Validators.BuildScenes());
            Add(all, "InputActions", Validators.InputActions());
            Add(all, "Manifest", Validators.Manifest());
            Add(all, "ProjectSettings", Validators.ProjectSettingsInvariants());
            Add(all, "AssetsLayout", Validators.AssetsLayout());
            return all;
        }

        [MenuItem("FishGame/Validate")]
        public static void RunMenu()
        {
            List<string> errors = Run();
            if (errors.Count == 0) { Debug.Log("FG VALIDATE PASS"); return; }
            Debug.Log("FG VALIDATE FAIL errors=" + errors.Count);
            foreach (string error in errors) Debug.Log("FG VALIDATE ERROR " + error);
        }

        static void Add(List<string> into, string label, List<string> errors)
        {
            foreach (string error in errors) into.Add(label + ": " + error);
        }
    }
}
