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
        const string MarkerValidMeshPath = "Assets/_Project/Content/Meshes/Marker_Valid.asset";      // T24
        const string MarkerInvalidMeshPath = "Assets/_Project/Content/Meshes/Marker_Invalid.asset";  // T24
        const int MarkerRingQuads = 24;
        const float MarkerInnerRadius = 0.42f, MarkerOuterRadius = 0.5f;
        const string ToonBaseMaterial = "M_Toon_Base";
        const string LineMaterial = "M_Line";

        // Rod rotation relative to Hand (ruling T19-R1). The arm's own frame is NOT identity at runtime: on the owner PlayerBody.LateUpdate poses
        // ArmR at camera * Euler(-90, 0, 0), which maps arm +Z to camera up, arm -Y (the way the arm mesh hangs) to camera forward, and arm +Y to
        // camera back. For the rod's +Z to be camera forward pitched UP by 35 degrees (0, sin35, cos35 in camera space) it must be
        // (0, -cos35, sin35) in arm space, i.e. +Z rotated toward -Y by 55 degrees = Euler(55, 0, 0) (90 - 35). On remotes the arm is
        // Euler(-90 - pitch, 0, 0) in Body space, so the rod elevation is 90 - 55 + pitch = 35 + pitch degrees (the owner's 35): forward-up when level, following the look.
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

        // T23's block first (ruling F-Q5: at most 12 lines, helper included; kept verbatim), then T24's (C 6.3 order: RodPresenter, LineView,
        // BobberView, SplashFx, CastMarkerView). Get-or-add only: rebuilds keep the component order and the root NetworkObject (hash unchanged).
        public static void AddPresentation(GameObject playerRoot)
        {
            FishingFeedback feedback = BuilderUtil.GetOrAdd<FishingFeedback>(playerRoot);
            CatchRevealView reveal = BuilderUtil.GetOrAdd<CatchRevealView>(playerRoot);
            SetObjectRef(feedback, "silhouetteMaterial", RenderMaterialLibrary.Get("M_Toon_FishFallback"));
            SetObjectRef(reveal, "fallbackMaterial", RenderMaterialLibrary.Get("M_Toon_FishFallback"));
            SetObjectRef(reveal, "fishMaterial", RenderMaterialLibrary.Get("M_Toon_Fish"));
            SetObjectRef(reveal, "sparkleMaterial", RenderMaterialLibrary.Get("M_Particle"));

            // T24. The rig materials (Seg*, Bobber, Line) are already final: Build() picked M_Toon_Base / M_Line (RigMaterial).
            BuilderUtil.GetOrAdd<RodPresenter>(playerRoot);
            BuilderUtil.GetOrAdd<LineView>(playerRoot);
            BuilderUtil.GetOrAdd<BobberView>(playerRoot);
            SplashFx splash = BuilderUtil.GetOrAdd<SplashFx>(playerRoot);
            SetObjectRef(splash, "particleMaterial", RenderMaterialLibrary.Get("M_Particle"));
            BuilderUtil.GetOrAdd<CastMarkerView>(playerRoot);
            BuildCastMarker(playerRoot.transform, RenderMaterialLibrary.Get("M_Marker"));
        }

        // CastMarker at the root with a Valid and an Invalid shape child (C 6.3): flat meshes in the XZ plane at y 0, M_Marker, shadows off, and the
        // renderers DISABLED (CastMarkerView enables them on the owner only, so a remote never flashes a marker).
        static void BuildCastMarker(Transform root, Material markerMaterial)
        {
            Mesh valid = SaveMesh(MarkerValidMeshPath, BuildMarkerValid());
            Mesh invalid = SaveMesh(MarkerInvalidMeshPath, BuildMarkerInvalid());
            AssetDatabase.SaveAssets();

            Empty(root, PlayerRigPaths.CastMarker, Vector3.zero, Quaternion.identity);
            MarkerShape(root, PlayerRigPaths.CastMarker + "/Valid", valid, markerMaterial);
            MarkerShape(root, PlayerRigPaths.CastMarker + "/Invalid", invalid, markerMaterial);
        }

        static void MarkerShape(Transform root, string path, Mesh mesh, Material material)
        {
            Transform shape = Empty(root, path, Vector3.zero, Quaternion.identity);
            BuilderUtil.GetOrAdd<MeshFilter>(shape.gameObject).sharedMesh = mesh;
            MeshRenderer renderer = BuilderUtil.GetOrAdd<MeshRenderer>(shape.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        // Valid: a solid ring of 24 quads (inner radius 0.42, outer 0.5) plus a centre dot.
        static Mesh BuildMarkerValid()
        {
            var builder = new MeshBuilder(0x4D4B0001u);
            for (int i = 0; i < MarkerRingQuads; i++) RingQuad(builder, i);
            builder.Prism(new Vector3(0f, 0.005f, 0f), 0.06f, 0.01f, 8, Quaternion.identity, SlotSnow);
            return builder.ToMesh("Marker_Valid");
        }

        // Invalid: a dashed ring (every other one of the 24 quads) plus a cross (two flat boxes, +-45 degrees about Y).
        static Mesh BuildMarkerInvalid()
        {
            var builder = new MeshBuilder(0x4D4B0002u);
            for (int i = 0; i < MarkerRingQuads; i += 2) RingQuad(builder, i);
            var size = new Vector3(0.5f, 0.01f, 0.06f);
            builder.Box(new Vector3(0f, 0.005f, 0f), size, Quaternion.Euler(0f, 45f, 0f), SlotSnow);
            builder.Box(new Vector3(0f, 0.005f, 0f), size, Quaternion.Euler(0f, -45f, 0f), SlotSnow);
            return builder.ToMesh("Marker_Invalid");
        }

        // One ring segment facing up (+Y): inner0, inner1, outer1, outer0 with the angle running from +X toward +Z.
        static void RingQuad(MeshBuilder builder, int index)
        {
            float a0 = 2f * Mathf.PI * index / MarkerRingQuads, a1 = 2f * Mathf.PI * (index + 1) / MarkerRingQuads;
            Vector3 inner0 = new Vector3(Mathf.Cos(a0) * MarkerInnerRadius, 0f, Mathf.Sin(a0) * MarkerInnerRadius);
            Vector3 inner1 = new Vector3(Mathf.Cos(a1) * MarkerInnerRadius, 0f, Mathf.Sin(a1) * MarkerInnerRadius);
            Vector3 outer1 = new Vector3(Mathf.Cos(a1) * MarkerOuterRadius, 0f, Mathf.Sin(a1) * MarkerOuterRadius);
            Vector3 outer0 = new Vector3(Mathf.Cos(a0) * MarkerOuterRadius, 0f, Mathf.Sin(a0) * MarkerOuterRadius);
            builder.Quad(inner0, inner1, outer1, outer0, SlotSnow);
        }

        static void SetObjectRef(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;   // a missing field throws: loud on purpose
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cylinder along local +Z from z 0 to 0.3, tapering from the base radius to the tip radius over the six segments.
        // R74 adoption: smooth (SmoothMeshBuilder lathe, 12 sides) with wood grain running along the rod. Both open ends of the rod are closed with a
        // rounded cap (a radius of 0 at the very end, a quarter ellipse over the end sixth): the butt of segment 0 and the tip of the last segment.
        // The end segments use 12 rings so the cap has real ring samples (0, half and full radius); middle segments stay at 2.
        static Mesh BuildSegment(int index)
        {
            float rBottom = Mathf.Lerp(RodRadiusBase, RodRadiusTip, index / 6f);
            float rTop = Mathf.Lerp(RodRadiusBase, RodRadiusTip, (index + 1) / 6f);
            bool butt = index == 0;
            bool tip = index == PlayerRigPaths.RodSegmentCount - 1;
            const float cap = 2f / 12f;
            var builder = new SmoothMeshBuilder();
            builder.Lathe(new Vector3(0f, 0f, SegmentLength * 0.5f), t =>
            {
                float r = Mathf.Lerp(rBottom, rTop, t);
                if (butt && t < cap) { float u = (cap - t) / cap; return r * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); }
                if (tip && t > 1f - cap) { float u = (t - (1f - cap)) / cap; return r * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); }
                return r;
            }, SegmentLength, 12, butt || tip ? 12 : 2, Quaternion.Euler(90f, 0f, 0f), SlotWoodDark, EnvTextureLayers.Wood, SmoothMeshBuilder.MappingUv,
                t => 0.92f + 0.08f * t, rBottom, true);
            return builder.ToMesh("Rod_Seg" + index);
        }

        // R74 adoption, playtest F6 revision (2026-10-13: "just an all white ball"): a classic fishing float, an orange (Accent) dome over a white (Snow)
        // dome (two smooth hemispheres, so the colour edge stays crisp) with a thin dark (WoodDark) waterline band between them so the split reads at a
        // distance, and a white stem topped by an orange ball. Radius 4.5 cm (was 3.5 cm); BobberView also scales it up with distance
        // (FightViewMath.DistanceScale). The stem ball's top is FightViewMath.StemTipHeight (0.135) above the centre. Palette slots only, no new material.
        static Mesh BuildBobber()
        {
            const float r = 0.045f;
            const float band = 0.012f, bandR = r * 1.04f;
            var builder = new SmoothMeshBuilder();
            System.Func<float, float> half = t => r * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            builder.Lathe(new Vector3(0f, r * 0.5f, 0f), t => half(t), r, 20, 8, Quaternion.identity, SlotAccent, EnvTextureLayers.None,
                SmoothMeshBuilder.MappingUv, t => 1f, r);
            builder.Lathe(new Vector3(0f, -r * 0.5f, 0f), t => half(1f - t), r, 20, 8, Quaternion.identity, SlotSnow, EnvTextureLayers.None,
                SmoothMeshBuilder.MappingUv, t => 0.82f + 0.18f * t, r);
            // The band is centred on the equator, a little proud of the domes; its rounded edges (radius 0.88 bandR at both ends) tuck inside them.
            builder.Lathe(Vector3.zero, t => bandR * (0.88f + 0.12f * Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * t - 1f) * (2f * t - 1f)))), band, 20, 4,
                Quaternion.identity, SlotWoodDark, EnvTextureLayers.None, SmoothMeshBuilder.MappingUv, t => 1f, bandR);
            // The float's top is y = r (0.045); the stem runs y 0.04..0.125 so it overlaps the float by 5 mm and ends inside the ball (centre 0.125, r 0.01).
            builder.Lathe(new Vector3(0f, 0.0825f, 0f), t => 0.0045f, 0.085f, 8, 1, Quaternion.identity, SlotSnow, EnvTextureLayers.None,
                SmoothMeshBuilder.MappingUv, t => 1f, 0.0045f);
            builder.Radial(new Vector3(0f, 0.125f, 0f), d => 0.01f, 3, Quaternion.identity, Vector3.one, SlotAccent, EnvTextureLayers.None,
                SmoothMeshBuilder.MappingUv, d => 1f, 0.01f);
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
