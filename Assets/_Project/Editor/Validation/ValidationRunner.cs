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
            Add(all, "MeshReferences", Validators.MeshReferences());
            Add(all, "MissingScripts", Validators.MissingScripts());
            Add(all, "BootstrapReferences", Validators.BootstrapReferences());
            all.AddRange(FishingValidators.All());
            Add(all, "Render", RenderValidators.All());
            all.AddRange(EcologyValidators.All());   // M3 T11 (already prefixed "Ecology: "); T16 adds its MapChecks line after this one
            all.AddRange(EcologyValidators.MapChecks());   // M3 T16 (already prefixed "EcologyMaps: "; C 11.3, M3-D83)
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
