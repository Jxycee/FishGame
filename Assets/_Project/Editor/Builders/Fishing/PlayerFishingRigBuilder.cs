// Builds the fishing rig on the PlayerFP prefab (contract 6.3, 7.19). Called from PrefabBuilder.BuildPlayerRoot (the last two statements).
// Get-or-add everything, never destroy: rebuilds keep GUIDs, the root NetworkObject (and so its GlobalObjectIdHash) and the mesh assets' GUIDs.
// Root component order is fixed from here on (C 6.3): PlayerCreel, FishingRodNetwork, FishingRodController (disabled; the owner's
// FishingRodNetwork.OnNetworkSpawn enables it). NetworkBehaviour order must never change after this task, or NGO's behaviour indices shift.
// No "using UnityEditor.SessionState" clash here: this file never names SessionState.
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Presentation;
using FishGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class PlayerFishingRigBuilder
    {
        // Spec 10.2 slotNames: WoodDark 4, Accent 10, Snow 11 (ruling E-Q4).
        const byte SlotWoodDark = 4, SlotAccent = 10, SlotSnow = 11;

        const string RodSegmentMeshPrefix = "Assets/_Project/Content/Meshes/Rod_Seg";   // + i + ".asset"
        const string BobberMeshPath = "Assets/_Project/Content/Meshes/Bobber.asset";
        const string ToonBaseMaterial = "M_Toon_Base";
        const string LineMaterial = "M_Line";

        // Rod rotation relative to Hand (ruling T19-R1). The arm's own frame is NOT identity at runtime: on the owner PlayerBody.LateUpdate poses
        // ArmR at camera * Euler(-90, 0, 0), which maps arm +Z to camera up, arm -Y (the way the arm mesh hangs) to camera forward, and arm +Y to
        // camera back. For the rod's +Z to be camera forward pitched UP by 35 degrees (0, sin35, cos35 in camera space) it must be
        // (0, -cos35, sin35) in arm space, i.e. +Z rotated toward -Y by 55 degrees = Euler(55, 0, 0) (90 - 35). On remotes the arm is
        // Euler(-70 - pitch, 0, 0) in Body space, so the rod elevation is 70 - 55 + pitch = 15 + pitch degrees: forward-up when level, following the look.
        const float RodLocalPitchDeg = 55f;

        const float SegmentLength = 0.3f;
        const float RodRadiusBase = 0.018f, RodRadiusTip = 0.006f;
        const int LinePointCount = 24;
        const float LineWidth = 0.008f;

        public static void Build(GameObject playerRoot)
        {
            // 1. Meshes first: saved before the prefab references them.
            Mesh[] segments = new Mesh[PlayerRigPaths.RodSegmentCount];
            for (int i = 0; i < segments.Length; i++) segments[i] = SaveMesh(RodSegmentMeshPrefix + i + ".asset", BuildSegment(i));
            Mesh bobber = SaveMesh(BobberMeshPath, BuildBobber());
            AssetDatabase.SaveAssets();

            // 2. Materials: the new Toon material when it exists (T20/T21 re-point the rig on their next build), else the M1 fallback.
            Material rodMaterial = RigMaterial(ToonBaseMaterial, "M_WoodDark");
            Material bobberMaterial = RigMaterial(ToonBaseMaterial, "M_Brass");
            Material lineMaterial = RigMaterialOrNull(LineMaterial);

            // 3. Children.
            Transform root = playerRoot.transform;
            Empty(root, PlayerRigPaths.Hand, new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            Empty(root, PlayerRigPaths.Rod, Vector3.zero, Quaternion.Euler(RodLocalPitchDeg, 0f, 0f));
            for (int i = 0; i < segments.Length; i++)
            {
                Transform seg = Empty(root, PlayerRigPaths.RodSegmentPrefix + i, new Vector3(0f, 0f, SegmentLength * i), Quaternion.identity);
                MeshFilter filter = BuilderUtil.GetOrAdd<MeshFilter>(seg.gameObject);
                filter.sharedMesh = segments[i];
                MeshRenderer renderer = BuilderUtil.GetOrAdd<MeshRenderer>(seg.gameObject);
                renderer.sharedMaterial = rodMaterial;
            }

            // BF-3: TipAnchor stays static (the controller reads it); TipVisual is bent by RodPresenter (T24) and is where the line starts.
            Vector3 tip = new Vector3(0f, 0f, SegmentLength * PlayerRigPaths.RodSegmentCount);
            Empty(root, PlayerRigPaths.RodTipAnchor, tip, Quaternion.identity);
            Empty(root, PlayerRigPaths.RodTipVisual, tip, Quaternion.identity);
            Empty(root, PlayerRigPaths.CatchHold, new Vector3(0f, 0.05f, 0.12f), Quaternion.identity);

            Transform line = Empty(root, PlayerRigPaths.Line, Vector3.zero, Quaternion.identity);
            LineRenderer lineRenderer = BuilderUtil.GetOrAdd<LineRenderer>(line.gameObject);
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = LinePointCount;
            lineRenderer.startWidth = LineWidth;
            lineRenderer.endWidth = LineWidth;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            if (lineMaterial != null) lineRenderer.sharedMaterial = lineMaterial;
            lineRenderer.enabled = false;   // LineView owns visibility

            Transform bobberTransform = Empty(root, PlayerRigPaths.Bobber, Vector3.zero, Quaternion.identity);
            BuilderUtil.GetOrAdd<MeshFilter>(bobberTransform.gameObject).sharedMesh = bobber;
            MeshRenderer bobberRenderer = BuilderUtil.GetOrAdd<MeshRenderer>(bobberTransform.gameObject);
            bobberRenderer.sharedMaterial = bobberMaterial;
            bobberRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bobberRenderer.receiveShadows = false;
            bobberRenderer.enabled = false;   // BobberView owns visibility and position

            // 4. Root components, appended after the M1 list in this order (C 6.3).
            BuilderUtil.GetOrAdd<PlayerCreel>(playerRoot).enabled = true;
            BuilderUtil.GetOrAdd<FishingRodNetwork>(playerRoot).enabled = true;
            BuilderUtil.GetOrAdd<FishingRodController>(playerRoot).enabled = false;   // every build, not only the first
        }

        // T23's block (ruling F-Q5: at most 12 lines, helper included); T24 owns the rest of this body (C 2.6).
        public static void AddPresentation(GameObject playerRoot)
        {
            FishingFeedback feedback = BuilderUtil.GetOrAdd<FishingFeedback>(playerRoot);
            CatchRevealView reveal = BuilderUtil.GetOrAdd<CatchRevealView>(playerRoot);
            SetObjectRef(feedback, "silhouetteMaterial", RenderMaterialLibrary.Get("M_Toon_FishFallback"));
            SetObjectRef(reveal, "fallbackMaterial", RenderMaterialLibrary.Get("M_Toon_FishFallback"));
            SetObjectRef(reveal, "fishMaterial", RenderMaterialLibrary.Get("M_Toon_Fish"));
            SetObjectRef(reveal, "sparkleMaterial", RenderMaterialLibrary.Get("M_Particle"));
        }

        static void SetObjectRef(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;   // a missing field throws: loud on purpose
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cylinder along local +Z from z 0 to 0.3, tapering from the base radius to the tip radius over the six segments.
        static Mesh BuildSegment(int index)
        {
            float rBottom = Mathf.Lerp(RodRadiusBase, RodRadiusTip, index / 6f);
            float rTop = Mathf.Lerp(RodRadiusBase, RodRadiusTip, (index + 1) / 6f);
            var builder = new MeshBuilder(unchecked((uint)(0x524F4400u + index)));
            builder.Cylinder(new Vector3(0f, 0f, SegmentLength * 0.5f), rBottom, rTop, SegmentLength, 6, Quaternion.Euler(90f, 0f, 0f), SlotWoodDark);
            return builder.ToMesh("Rod_Seg" + index);
        }

        // Float: red top, white bottom, a thin red stem.
        static Mesh BuildBobber()
        {
            var builder = new MeshBuilder(0x424F4242u);
            builder.Cylinder(new Vector3(0f, 0.025f, 0f), 0.035f, 0.035f, 0.05f, 8, Quaternion.identity, SlotAccent);
            builder.Cylinder(new Vector3(0f, -0.025f, 0f), 0.035f, 0.035f, 0.05f, 8, Quaternion.identity, SlotSnow);
            builder.Cylinder(new Vector3(0f, 0.07f, 0f), 0.004f, 0.004f, 0.04f, 4, Quaternion.identity, SlotAccent);
            return builder.ToMesh("Bobber");
        }

        // Existing asset: copy the built data into it (GUID and fileID stay, rebuilds are byte-identical) and return the asset.
        static Mesh SaveMesh(string assetPath, Mesh built)
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(assetPath));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(built, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(built);
                return existing;
            }

            AssetDatabase.CreateAsset(built, assetPath);
            return built;
        }

        static Material RigMaterial(string preferred, string fallback)
        {
            Material material = RigMaterialOrNull(preferred);
            return material != null ? material : MaterialLibrary.Get(fallback);
        }

        static Material RigMaterialOrNull(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
        }

        static Transform Empty(Transform root, string path, Vector3 localPosition, Quaternion localRotation)
        {
            Transform t = BuilderUtil.EnsurePath(root, path);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = Vector3.one;
            return t;
        }
    }
}
