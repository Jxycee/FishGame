// Spec 4.7 invariants for M1 (#1-#7; #8 arrives in M3, and #9 has no FishGame shaders before M2). Owned by T15.
// Reads persisted state only (asset files, ProjectSettings text, Packages/manifest.json) and never opens a scene.
// The separate test-edit process therefore also proves that BuildAll saved everything.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishGame.EditorTools
{
    public static class Validators
    {
        static readonly string[] M1NetworkPrefabs = { AssetPaths.PlayerPrefab, AssetPaths.DockBellPrefab, AssetPaths.SessionStatePrefab };

        // GUID of Assets/Settings/SampleSceneProfile.asset, deleted by T1 (spec 4.2 #1).
        const string DeletedSampleSceneProfileGuid = "10fc4df2da32a41aaa32d77bc913491c";

        // Spec 4.4 pins ("name@version"). Rows marked "as in template" in the spec (timeline, ide.*) are not pinned here.
        static readonly string[] ManifestPins =
        {
            "com.unity.netcode.gameobjects@2.13.3", "com.unity.services.multiplayer@2.3.3",
            "com.unity.services.authentication@3.8.0", "com.unity.services.core@1.18.0",
            "com.unity.multiplayer.playmode@3.0.0", "com.unity.multiplayer.tools@2.2.12",
            "com.unity.nuget.newtonsoft-json@3.2.2", "com.unity.inputsystem@1.20.0",
            "com.unity.render-pipelines.universal@17.6.0", "com.unity.test-framework@1.8.0",
            "com.unity.ugui@2.6.0", "com.unity.ai.navigation@2.0.15", "com.unity.pipeline@0.8.0-exp.1",
        };

        // Spec 4.2 #3 removals, the 4.4 "never add" list, and com.unity.transport (transitive only, never listed).
        static readonly string[] ForbiddenPackages =
        {
            "com.unity.visualscripting", "com.unity.collab-proxy", "com.unity.services.relay", "com.unity.services.lobby",
            "com.unity.services.matchmaker", "com.unity.services.multiplay", "com.unity.multiplayer.widgets",
            "com.unity.textmeshpro", "com.unity.cinemachine", "com.unity.transport",
        };

        // 4.7 #1. Inside this class the method name shadows FishGame.Core.ContentIds, so the type is written with global::.
        public static List<string> ContentIds()
        {
            var errors = new List<string>();
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null) { errors.Add("ContentRegistry missing at " + AssetPaths.ContentRegistryAsset); return errors; }
            foreach (string error in registry.Validate()) errors.Add(error);

            var buildSceneNames = new HashSet<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                buildSceneNames.Add(Path.GetFileNameWithoutExtension(scene.path));

            bool lakeDockListed = false;
            foreach (SpotDef spot in registry.Spots)
            {
                if (spot == null) continue; // reported by Validate()
                if (spot.Id == global::FishGame.Core.ContentIds.LakeDock)
                {
                    lakeDockListed = true;
                    if (spot.SceneName != SceneNames.SpotLakeDock)
                        errors.Add("lake_dock sceneName is '" + spot.SceneName + "', expected " + SceneNames.SpotLakeDock);
                }
                if (!buildSceneNames.Contains(spot.SceneName))
                    errors.Add("spot " + spot.Id + " references scene '" + spot.SceneName + "', which is not in the build list");
            }
            if (!lakeDockListed) errors.Add("lake_dock is not listed in ContentRegistry");
            return errors;
        }

        // 4.7 #2 (plus #1 for prefab and scene references).
        public static List<string> NetworkPrefabs()
        {
            var errors = new List<string>();
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetPaths.NetworkPrefabsListAsset);
            if (list == null) { errors.Add("network prefab list missing at " + AssetPaths.NetworkPrefabsListAsset); return errors; }

            foreach (NetworkPrefab entry in list.PrefabList)
                if (entry == null || entry.Prefab == null) errors.Add("network prefab list has an empty entry");

            var seenHashes = new Dictionary<string, string>();
            foreach (string path in M1NetworkPrefabs)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { errors.Add("missing prefab " + path); continue; }
                if (go.GetComponent<NetworkObject>() == null) errors.Add(path + " has no root NetworkObject");
                string hash = ValidationText.FirstGroup(path, @"^\s*GlobalObjectIdHash:\s*(\d+)\s*$");
                if (hash == null || hash == "0") errors.Add(path + " has no GlobalObjectIdHash on disk");
                else if (seenHashes.ContainsKey(hash)) errors.Add(path + " shares GlobalObjectIdHash " + hash + " with " + seenHashes[hash]);
                else seenHashes.Add(hash, path);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { AssetPaths.ProjectFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        errors.Add(path + " has a missing script on " + t.name);
                if (go.GetComponent<NetworkObject>() != null && !list.Contains(go))
                    errors.Add("networked prefab not in the network prefab list: " + path);
            }

            RequireReference(AssetPaths.BootstrapScene, AssetPaths.NetworkPrefabsListAsset, "Bootstrap NetworkManager does not reference FishGameNetworkPrefabs.asset", errors);
            RequireReference(AssetPaths.BootstrapScene, AssetPaths.SessionStatePrefab, "Bootstrap AppBootstrap does not reference SessionState.prefab", errors);
            RequireReference(AssetPaths.LakeDockScene, AssetPaths.PlayerPrefab, "Spot_LakeDock PlayerSpawner does not reference PlayerFP.prefab", errors);
            RequireReference(AssetPaths.LakeDockScene, AssetPaths.DockBellPrefab, "Spot_LakeDock has no NetSpawnAnchor referencing DockBell.prefab", errors);
            return errors;
        }

        // 4.7 #3
        public static List<string> BuildScenes()
        {
            var errors = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            string[] names = SceneNames.BuildOrder;
            if (scenes.Length != names.Length)
                errors.Add("expected " + names.Length + " build scenes, found " + scenes.Length);
            for (int i = 0; i < names.Length; i++)
            {
                string expected = AssetPaths.ScenesFolder + "/" + names[i] + ".unity";
                if (!File.Exists(BuilderUtil.Abs(expected))) errors.Add("missing scene file " + expected);
                if (i >= scenes.Length) continue;
                if (scenes[i].path != expected) errors.Add("build index " + i + " is '" + scenes[i].path + "', expected " + expected);
                else if (!scenes[i].enabled) errors.Add(expected + " is disabled in the build list");
            }
            return errors;
        }

        // 4.7 #4. Types are asserted for Player and Fishing only; UI by name (contract D17).
        public static List<string> InputActions()
        {
            var errors = new List<string>();
            string json = ValidationText.Read(InputActionNames.AssetPath);
            if (json == null) { errors.Add("missing " + InputActionNames.AssetPath); return errors; }
            InputActionAsset asset;
            try { asset = InputActionAsset.FromJson(json); }
            catch (System.Exception e) { errors.Add("inputactions parse failed: " + e.Message); return errors; }

            try
            {
                foreach (string map in new[] { InputActionNames.PlayerMap, InputActionNames.FishingMap, InputActionNames.UiMap })
                    if (asset.FindActionMap(map) == null) errors.Add("missing action map " + map);
                if (asset.FindActionMap("FishGameInput") != null) errors.Add("no action map may be named FishGameInput");
                foreach (string action in InputActionNames.PlayerActions) CheckType(asset, InputActionNames.PlayerMap + "/" + action, errors);
                foreach (string action in InputActionNames.FishingActions) CheckType(asset, InputActionNames.FishingMap + "/" + action, errors);
                foreach (string action in InputActionNames.UiActions)
                    if (asset.FindAction(InputActionNames.UiMap + "/" + action) == null) errors.Add("missing action " + InputActionNames.UiMap + "/" + action);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
            return errors;
        }

        static void CheckType(InputActionAsset asset, string path, List<string> errors)
        {
            InputAction action = asset.FindAction(path);
            if (action == null) { errors.Add("missing action " + path); return; }
            bool ok;
            if (path == InputActionNames.Move || path == InputActionNames.Look)
                ok = action.type == InputActionType.Value && action.expectedControlType == "Vector2";
            else if (path == InputActionNames.Steer)
                ok = action.type == InputActionType.Value && action.expectedControlType == "Axis";
            else
                ok = action.type == InputActionType.Button;
            if (!ok) errors.Add("unexpected type for " + path + ": " + action.type + "/" + action.expectedControlType);
        }

        // 4.7 #5
        public static List<string> Manifest()
        {
            var errors = new List<string>();
            string text = ValidationText.Read(AssetPaths.ManifestFile);
            if (text == null) { errors.Add("missing " + AssetPaths.ManifestFile); return errors; }
            if (text.Length > 0 && text[0] == '\uFEFF') errors.Add("manifest.json starts with a BOM");
            foreach (string pin in ManifestPins)
            {
                int at = pin.IndexOf('@');
                string name = pin.Substring(0, at), version = pin.Substring(at + 1);
                string pattern = "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"" + Regex.Escape(version) + "\"";
                if (!Regex.IsMatch(text, pattern)) errors.Add("pin missing or wrong: " + name + " " + version);
            }
            foreach (string name in ForbiddenPackages)
                if (Regex.IsMatch(text, "\"" + Regex.Escape(name) + "\"\\s*:")) errors.Add("forbidden package listed: " + name);
            return errors;
        }

        // 4.7 #6 plus the spec 4.2 steps 2, 4, 5 outputs of ProjectSettingsBuilder.
        public static List<string> ProjectSettingsInvariants()
        {
            var errors = new List<string>();
            ValidationText.ExpectLines(AssetPaths.EditorSettingsFile, errors, "m_SerializationMode: 2", "m_EnterPlayModeOptionsEnabled: 0");
            ValidationText.ExpectLines(AssetPaths.ProjectSettingsFile, errors, "activeInputHandler: 1", "runInBackground: 1");
            ValidationText.ExpectLines(AssetPaths.PcRenderPipelineAsset, errors,
                "m_MSAA: 4", "m_RequireDepthTexture: 1", "m_SupportsHDR: 1", "m_RequireOpaqueTexture: 0",
                "m_SoftShadowsSupported: 1", "m_ShadowCascadeCount: 2", "m_ShadowDistance: 70");
            ValidationText.ExpectLines(AssetPaths.PcRenderer, errors, "m_RenderingMode: 2", "m_CopyDepthMode: 0");
            string pcRp = ValidationText.Read(AssetPaths.PcRenderPipelineAsset);
            if (pcRp != null && pcRp.Contains(DeletedSampleSceneProfileGuid))
                errors.Add(AssetPaths.PcRenderPipelineAsset + " still references the deleted SampleSceneProfile (m_VolumeProfile)");
            ValidationText.ExpectLines(AssetPaths.NetcodeSettingsFile, errors, "GenerateDefaultNetworkPrefabs: 0");

            string buildSettings = ValidationText.Read(AssetPaths.EditorBuildSettingsFile);
            if (buildSettings != null && buildSettings.Contains(BuilderUtil.ProjectWideActionsConfigKey))
                errors.Add("project-wide input actions entry (" + BuilderUtil.ProjectWideActionsConfigKey + ") is still configured");
            if (UnityEngine.InputSystem.InputSystem.actions != null)
                errors.Add("InputSystem.actions must be null (project-wide actions cleared)");

            CheckIgnored(Layers.Fish, Layers.Player, errors);
            CheckIgnored(Layers.Fish, Layers.RemotePlayer, errors);
            CheckIgnored(Layers.Ragdoll, Layers.Player, errors);
            CheckIgnored(Layers.Ragdoll, Layers.RemotePlayer, errors);
            CheckIgnored(Layers.Ragdoll, Layers.Fish, errors);

            if (LayerMask.LayerToName(Layers.Water) != "Water") errors.Add("layer " + Layers.Water + " must be Water");
            for (int i = 0; i < Layers.UserLayerIndices.Length; i++)
            {
                int index = Layers.UserLayerIndices[i];
                if (LayerMask.LayerToName(index) != Layers.UserLayerNames[i])
                    errors.Add("layer " + index + " must be " + Layers.UserLayerNames[i] + ", found '" + LayerMask.LayerToName(index) + "'");
            }
            return errors;
        }

        // 4.7 #7: nothing under Assets/ except _Project and Settings (and their two folder metas).
        public static List<string> AssetsLayout()
        {
            var errors = new List<string>();
            string assets = Application.dataPath;
            foreach (string dir in Directory.GetDirectories(assets))
            {
                string name = Path.GetFileName(dir);
                if (name != "_Project" && name != "Settings") errors.Add("unexpected folder Assets/" + name);
            }
            foreach (string file in Directory.GetFiles(assets))
            {
                string name = Path.GetFileName(file);
                if (name == "_Project.meta" || name == "Settings.meta") continue;
                errors.Add("unexpected file Assets/" + name);
            }
            return errors;
        }

        static void CheckIgnored(int a, int b, List<string> errors)
        {
            if (!Physics.GetIgnoreLayerCollision(a, b))
                errors.Add("physics matrix: layers " + LayerMask.LayerToName(a) + " and " + LayerMask.LayerToName(b) + " must not collide");
        }

        static void RequireReference(string fileAssetPath, string referencedAssetPath, string message, List<string> errors)
        {
            string guid = AssetDatabase.AssetPathToGUID(referencedAssetPath);
            string text = ValidationText.Read(fileAssetPath);
            if (string.IsNullOrEmpty(guid) || text == null || !text.Contains("guid: " + guid)) errors.Add(message);
        }
    }
}
