// Project-wide settings (spec 4.2 steps 2, 4, 5; spec 4.6 step 1; contract 7.15). Idempotent. Owned by T15.
using System.Reflection;
using FishGame.Core;
using Unity.Netcode.Editor.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FishGame.EditorTools
{
    public static class ProjectSettingsBuilder
    {
        public static void Apply()
        {
            // Netcode first. Every later asset import or delete runs NGO's NetworkPrefabProcessor, which recreates
            // Assets/DefaultNetworkPrefabs.asset while GenerateDefaultNetworkPrefabs is still on.
            ApplyNetcode();
            ApplyLayers();
            ApplyPhysicsMatrix();
            EditorSettings.enterPlayModeOptionsEnabled = false; // domain reload stays ON (spec 4.2 #4)
            ForceBool(AssetPaths.EditorSettingsFile, "m_EnterPlayModeOptionsEnabled", false); // the API setter is not persisted reliably
            PlayerSettings.runInBackground = true;
            ApplyUrp();

            // Spec 4.2 #2: clear the project-wide actions entry, then delete the template asset.
            UnityEngine.InputSystem.InputSystem.actions = null;
            EditorBuildSettings.RemoveConfigObject(BuilderUtil.ProjectWideActionsConfigKey); // the setter skips this when already null
            DeleteIfPresent(AssetPaths.TemplateInputActions);
            DeleteIfPresent(AssetPaths.DefaultNetworkPrefabs);

            // API setters do not always mark these settings objects dirty; make sure SaveAssets writes them.
            MarkDirty(AssetPaths.TagManagerFile);
            MarkDirty(AssetPaths.DynamicsManagerFile);
            MarkDirty(AssetPaths.EditorSettingsFile);
            MarkDirty(AssetPaths.ProjectSettingsFile);
            AssetDatabase.SaveAssets();
        }

        static void ApplyNetcode()
        {
            NetcodeForGameObjectsProjectSettings settings = NetcodeForGameObjectsProjectSettings.instance;
            settings.GenerateDefaultNetworkPrefabs = false;
            MethodInfo save = typeof(NetcodeForGameObjectsProjectSettings)
                .GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (save == null) throw new System.MissingMethodException("NetcodeForGameObjectsProjectSettings.SaveSettings not found (internal in NGO 2.13.3)");
            save.Invoke(settings, null); // writes ProjectSettings/NetcodeForGameObjects.asset
        }

        static void ApplyLayers()
        {
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(AssetPaths.TagManagerFile);
            if (objects.Length == 0) throw new System.InvalidOperationException("Cannot load " + AssetPaths.TagManagerFile);
            var tagManager = new SerializedObject(objects[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < Layers.UserLayerIndices.Length; i++)
                layers.GetArrayElementAtIndex(Layers.UserLayerIndices[i]).stringValue = Layers.UserLayerNames[i];
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        // Contract 5.3: Fish ignores Player and RemotePlayer; Ragdoll ignores Player, RemotePlayer and Fish. Everything else collides.
        static void ApplyPhysicsMatrix()
        {
            Physics.IgnoreLayerCollision(Layers.Fish, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Fish, Layers.RemotePlayer, true);
            Physics.IgnoreLayerCollision(Layers.Ragdoll, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Ragdoll, Layers.RemotePlayer, true);
            Physics.IgnoreLayerCollision(Layers.Ragdoll, Layers.Fish, true);
        }

        // Spec 4.2 #5: MSAA 4x, HDR on, soft shadows, 2 cascades, shadow distance 70 m, opaque texture off. The depth texture flag
        // (m_RequireDepthTexture) is owned by RenderingSetupBuilder (PC and Mobile), not set here.
        static void ApplyUrp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPaths.PcRenderPipelineAsset);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.PcRenderPipelineAsset);
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            asset.supportsCameraOpaqueTexture = false;
            asset.shadowCascadeCount = 2;
            asset.shadowDistance = 70f;
            var serialized = new SerializedObject(asset); // supportsSoftShadows has an internal setter in URP 17.6
            SerializedProperty soft = serialized.FindProperty("m_SoftShadowsSupported");
            if (soft == null) throw new System.InvalidOperationException("m_SoftShadowsSupported not found on " + AssetPaths.PcRenderPipelineAsset);
            soft.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            RepointVolumeProfile(asset);
        }

        // T1 deleted Assets/Settings/SampleSceneProfile.asset, which PC_RPAsset still referenced (a missing reference).
        // Point it at the template's DefaultVolumeProfile, the same profile the URP global settings use. Mobile assets stay as they are (spec 4.2 #5).
        static void RepointVolumeProfile(UniversalRenderPipelineAsset asset)
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(AssetPaths.DefaultVolumeProfile);
            if (profile == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.DefaultVolumeProfile);
            var serialized = new SerializedObject(asset);
            SerializedProperty volume = serialized.FindProperty("m_VolumeProfile");
            if (volume == null) throw new System.InvalidOperationException("m_VolumeProfile not found on " + AssetPaths.PcRenderPipelineAsset);
            if (volume.objectReferenceValue == profile) return;
            volume.objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void DeleteIfPresent(string assetPath)
        {
            bool known = AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
            if (!known && !System.IO.File.Exists(BuilderUtil.Abs(assetPath))) return;
            if (!AssetDatabase.DeleteAsset(assetPath)) throw new System.InvalidOperationException("Could not delete " + assetPath);
        }

        // Writes a bool straight into a ProjectSettings object, the same way ApplyLayers does.
        static void ForceBool(string settingsPath, string property, bool value)
        {
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
            if (objects.Length == 0) throw new System.InvalidOperationException("Cannot load " + settingsPath);
            var serialized = new SerializedObject(objects[0]);
            SerializedProperty prop = serialized.FindProperty(property);
            if (prop == null) throw new System.InvalidOperationException(property + " not found in " + settingsPath);
            prop.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MarkDirty(string settingsPath)
        {
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
            if (objects.Length == 0) throw new System.InvalidOperationException("Cannot load " + settingsPath);
            EditorUtility.SetDirty(objects[0]);
        }
    }
}
