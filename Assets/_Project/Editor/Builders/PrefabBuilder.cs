// PlayerFP, DockBell and SessionState network prefabs (contract 6.3, 6.4, 6.2). Owned by T15.
// Rebuilt in place with get-or-add (never destroy/re-add the root NetworkObject), then GlobalObjectIdHash is refreshed on the saved asset.
// This file deliberately has no "using UnityEditor;": UnityEditor.SessionState would clash with FishGame.Gameplay.SessionState.
using FishGame.Core;
using FishGame.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class PrefabBuilder
    {
        public static void Build()
        {
            BuildPlayer();
            BuildDockBell();
            BuildSessionState();
        }

        public static void BuildPlayer()
        {
            BuilderUtil.WritePrefab(AssetPaths.PlayerPrefab, BuildPlayerRoot);
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.PlayerPrefab);
        }

        public static void BuildDockBell()
        {
            BuilderUtil.WritePrefab(AssetPaths.DockBellPrefab, BuildDockBellRoot);
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.DockBellPrefab);
        }

        // Contract 6.2: root NetworkObject + SessionState only.
        public static void BuildSessionState()
        {
            BuilderUtil.WritePrefab(AssetPaths.SessionStatePrefab, root =>
            {
                BuilderUtil.GetOrAdd<NetworkObject>(root);
                BuilderUtil.GetOrAdd<FishGame.Gameplay.SessionState>(root);
            });
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.SessionStatePrefab);
        }

        static void BuildPlayerRoot(GameObject root)
        {
            var slotMaterials = new Material[SlotPalette.Count];
            for (int i = 0; i < slotMaterials.Length; i++) slotMaterials[i] = MaterialLibrary.Get("M_Body_Slot" + i);
            Material skin = MaterialLibrary.Get("M_Skin");
            Material body = slotMaterials[0];

            // Components in contract 6.3 order.
            BuilderUtil.GetOrAdd<NetworkObject>(root);

            NetworkTransform nt = BuilderUtil.GetOrAdd<NetworkTransform>(root);
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncPositionX = true; nt.SyncPositionY = true; nt.SyncPositionZ = true;
            nt.SyncRotAngleX = false; nt.SyncRotAngleY = true; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false; nt.SyncScaleY = false; nt.SyncScaleZ = false;
            nt.InLocalSpace = false;
            nt.Interpolate = true;
            nt.PositionInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            nt.RotationInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            nt.UseUnreliableDeltas = false;
            nt.SwitchTransformSpaceWhenParented = true;
            nt.enabled = true;

            CharacterController controller = BuilderUtil.GetOrAdd<CharacterController>(root);
            controller.height = MovementMath.CapsuleHeight;
            controller.radius = MovementMath.CapsuleRadius;
            controller.center = new Vector3(0f, MovementMath.CapsuleCenterY, 0f);
            controller.stepOffset = MovementMath.StepOffset;
            controller.slopeLimit = MovementMath.SlopeLimit;
            controller.skinWidth = MovementMath.SkinWidth;
            controller.minMoveDistance = 0f; // batchmode frame rates move < 1 mm per frame; the default 0.001 would drop those moves (T16)
            controller.enabled = false;

            CapsuleCollider capsule = BuilderUtil.GetOrAdd<CapsuleCollider>(root);
            capsule.height = MovementMath.CapsuleHeight;
            capsule.radius = MovementMath.CapsuleRadius;
            capsule.center = new Vector3(0f, MovementMath.CapsuleCenterY, 0f);
            capsule.direction = 1; // Y axis
            capsule.enabled = false;

            BuilderUtil.GetOrAdd<PlayerNetState>(root).enabled = true;
            BuilderUtil.GetOrAdd<PlayerActions>(root).enabled = true;
            BuilderUtil.GetOrAdd<PlayerLocomotion>(root).enabled = false;
            BuilderUtil.GetOrAdd<PlayerCamera>(root).enabled = false;
            PlayerBody playerBody = BuilderUtil.GetOrAdd<PlayerBody>(root);
            playerBody.Configure(slotMaterials, skin);
            playerBody.enabled = true;
            BuilderUtil.GetOrAdd<PlayerInteractor>(root).enabled = false;

            // Children (contract 6.3). Visual primitives have their colliders removed.
            Transform t = root.transform;
            Empty(t, PlayerRigPaths.HeadPivot, new Vector3(0f, MovementMath.EyeHeight, 0f));
            Empty(t, PlayerRigPaths.Shake, Vector3.zero);
            Transform cameraTransform = Empty(t, PlayerRigPaths.Camera, Vector3.zero);
            cameraTransform.gameObject.tag = "MainCamera";
            Camera camera = BuilderUtil.GetOrAdd<Camera>(cameraTransform.gameObject);
            camera.nearClipPlane = MovementMath.NearClip;
            camera.farClipPlane = MovementMath.FarClip;
            camera.enabled = false;
            BuilderUtil.GetOrAdd<AudioListener>(cameraTransform.gameObject).enabled = false;

            Prim(t, PlayerRigPaths.Head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.28f, 0.28f, 0.28f), skin);
            Prim(t, PlayerRigPaths.Nose, PrimitiveType.Cube, new Vector3(0f, 0f, 0.5f), new Vector3(0.18f, 0.18f, 0.3f), skin);

            Empty(t, PlayerRigPaths.Body, Vector3.zero);
            Prim(t, PlayerRigPaths.Torso, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.42f, 0.55f, 0.24f), body);
            Empty(t, PlayerRigPaths.ArmL, new Vector3(-0.27f, 1.42f, 0f));
            Empty(t, PlayerRigPaths.ArmR, new Vector3(0.27f, 1.42f, 0f));
            Prim(t, PlayerRigPaths.ArmLMesh, PrimitiveType.Capsule, new Vector3(0f, -0.25f, 0f), new Vector3(0.12f, 0.25f, 0.12f), body);
            Prim(t, PlayerRigPaths.ArmRMesh, PrimitiveType.Capsule, new Vector3(0f, -0.25f, 0f), new Vector3(0.12f, 0.25f, 0.12f), body);
            Empty(t, PlayerRigPaths.LegL, new Vector3(-0.11f, 0.8f, 0f));
            Empty(t, PlayerRigPaths.LegR, new Vector3(0.11f, 0.8f, 0f));
            Prim(t, PlayerRigPaths.LegLMesh, PrimitiveType.Capsule, new Vector3(0f, -0.4f, 0f), new Vector3(0.16f, 0.4f, 0.16f), body);
            Prim(t, PlayerRigPaths.LegRMesh, PrimitiveType.Capsule, new Vector3(0f, -0.4f, 0f), new Vector3(0.16f, 0.4f, 0.16f), body);
            Empty(t, PlayerRigPaths.NameplateAnchor, new Vector3(0f, 2.05f, 0f));

            // T19 hook (C 2.6): the fishing rig, its components after the M1 list, then the presentation hook. RefreshNetworkObjectHash follows in BuildPlayer.
            PlayerFishingRigBuilder.Build(root);
            PlayerFishingRigBuilder.AddPresentation(root);
        }

        // Contract 6.4.
        static void BuildDockBellRoot(GameObject root)
        {
            BuilderUtil.GetOrAdd<NetworkObject>(root);
            BuilderUtil.GetOrAdd<DockBell>(root);
            BoxCollider box = BuilderUtil.GetOrAdd<BoxCollider>(root);
            box.isTrigger = false;
            box.size = new Vector3(0.6f, 1.6f, 0.6f);
            box.center = new Vector3(0f, 0.8f, 0f);

            BuilderUtil.EnsurePrimitive(root.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f),
                Quaternion.identity, new Vector3(0.12f, 0.8f, 0.12f), MaterialLibrary.Get("M_Wood"), false);
            BuilderUtil.EnsurePrimitive(root.transform, "Bell", PrimitiveType.Sphere, new Vector3(0f, 1.45f, 0f),
                Quaternion.identity, new Vector3(0.35f, 0.4f, 0.35f), MaterialLibrary.Get("M_Brass"), false);
            BuilderUtil.SetLayerRecursive(root, Layers.Interactable);
        }

        static Transform Empty(Transform root, string path, Vector3 localPosition)
        {
            Transform t = BuilderUtil.EnsurePath(root, path);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        static GameObject Prim(Transform root, string path, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            int cut = path.LastIndexOf('/');
            Transform parent = cut < 0 ? root : BuilderUtil.EnsurePath(root, path.Substring(0, cut));
            string name = cut < 0 ? path : path.Substring(cut + 1);
            return BuilderUtil.EnsurePrimitive(parent, name, type, localPosition, Quaternion.identity, localScale, material, false);
        }
    }
}
