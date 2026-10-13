// Editor/Builders/Ecology/WorldSceneObjects.cs - the per-Spot-scene M3 world objects (C 6.3, C 7.16; T16).
// Authority: none (editor; builds scene objects). "World" is the first in-scene NetworkObject (C 2.8 #24): NetworkObject + WorldState
// (spotId) + DevWorldDriver on the one root (M3-D59). "StandPoints" holds the StandPoint markers the shore-grid bake reads.
// E-CCR-1 / M3-D82: NGO 2.13.3 computes GlobalObjectIdHash in the internal NetworkObject.OnValidate from GlobalObjectId.GetGlobalObjectIdSlow,
// which has no scene identity until the scene is saved; FinalizeAfterSave refreshes it on the saved scene and saves again.
using System;
using System.Collections.Generic;
using System.Reflection;
using FishGame.Core;
using FishGame.Gameplay;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class WorldSceneObjects
    {
        public const string WorldRootName = "World";
        public const string StandPointsRootName = "StandPoints";
        public const string StandPointPrefix = "StandPoint_";

        private static readonly MethodInfo s_onValidate =
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Root "World" (layer Default, identity, active) with NetworkObject, WorldState (spotId) and DevWorldDriver, in that order.
        public static void AddTo(Scene scene, string spotId)
        {
            if (string.IsNullOrEmpty(spotId)) throw new ArgumentException("spotId must be a spot id.", nameof(spotId));
            if (!scene.IsValid()) throw new ArgumentException("scene is not valid.", nameof(scene));
            if (FindRoot(scene, WorldRootName) != null) throw new InvalidOperationException("one World per scene (" + scene.name + ")");

            var go = new GameObject(WorldRootName);
            go.layer = Layers.Default;
            SceneManager.MoveGameObjectToScene(go, scene);
            NetworkObject networkObject = go.AddComponent<NetworkObject>();
            WorldState world = go.AddComponent<WorldState>();
            go.AddComponent<DevWorldDriver>();

            using (var so = new SerializedObject(world))
            {
                SerializedProperty p = so.FindProperty("spotId");
                if (p == null) throw new InvalidOperationException("WorldState has no serialized field 'spotId' (M3-D59)");
                p.stringValue = spotId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            RefreshHash(networkObject);   // a no-op for the hash before the save (E-CCR-1); FinalizeAfterSave sets it on the saved scene
        }

        // Root "StandPoints" with StandPoint_0..n-1 in list order: world position (x, y, z), yaw w; no collider, no renderer.
        public static void AddStandPoints(Scene scene, IReadOnlyList<Vector4> xyzFacing)
        {
            if (xyzFacing == null) throw new ArgumentNullException(nameof(xyzFacing));
            if (xyzFacing.Count == 0) throw new ArgumentException("a map without stand points can never pass reachability.", nameof(xyzFacing));
            if (!scene.IsValid()) throw new ArgumentException("scene is not valid.", nameof(scene));
            if (FindRoot(scene, StandPointsRootName) != null) throw new InvalidOperationException("one StandPoints root per scene (" + scene.name + ")");

            var root = new GameObject(StandPointsRootName);
            root.layer = Layers.Default;
            SceneManager.MoveGameObjectToScene(root, scene);
            for (int i = 0; i < xyzFacing.Count; i++)
            {
                Vector4 p = xyzFacing[i];
                var go = new GameObject(StandPointPrefix + i);
                go.layer = Layers.Default;
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(p.x, p.y, p.z);
                go.transform.rotation = Quaternion.Euler(0f, p.w, 0f);
                go.AddComponent<StandPoint>().Configure(p.w);
            }
        }

        // E-CCR-1 (M3-D82): after BuilderUtil.SaveScene, run OnValidate on the saved scene's World NetworkObject, check the hash is non-zero,
        // SetDirty and save the scene a second time (file ids are unchanged within one build).
        internal static void FinalizeAfterSave(Scene scene, string scenePath)
        {
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("save the scene before FinalizeAfterSave");
            GameObject world = FindRoot(scene, WorldRootName);
            if (world == null) throw new InvalidOperationException("no World root in " + scenePath);
            NetworkObject networkObject = world.GetComponent<NetworkObject>();
            if (networkObject == null) throw new InvalidOperationException("World has no NetworkObject in " + scenePath);
            RefreshHash(networkObject);
            if (HashOf(networkObject) == 0u)
                throw new InvalidOperationException("World GlobalObjectIdHash is still 0 after OnValidate in " + scenePath);
            BuilderUtil.SaveScene(scene, scenePath);
        }

        internal static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        // The serialized field, as the validator reads it.
        internal static uint HashOf(NetworkObject networkObject)
        {
            using (var so = new SerializedObject(networkObject))
            {
                SerializedProperty p = so.FindProperty("GlobalObjectIdHash");
                if (p == null) throw new InvalidOperationException("NetworkObject.GlobalObjectIdHash property not found");
                return p.uintValue;
            }
        }

        private static void RefreshHash(NetworkObject networkObject)
        {
            if (s_onValidate == null) throw new MissingMethodException("NetworkObject.OnValidate not found (expected internal in NGO 2.13.3)");
            s_onValidate.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);   // C2 #4: OnValidate dirties only on a change in memory
            EditorSceneManager.MarkSceneDirty(networkObject.gameObject.scene);
        }
    }
}
