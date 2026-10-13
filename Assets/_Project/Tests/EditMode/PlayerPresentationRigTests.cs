// T24: the rod presentation rig on the PlayerFP prefab (plan T24 tests, ruling F-Q6). EditMode cannot reference Presentation (C 2.8 #8), so
// components are compared by GetType().Name and their serialized fields are read through SerializedObject. Reads the persisted prefab built by
// `build`; nothing is written. Each test names the wrong build it catches.
using System.Collections.Generic;
using FishGame.EditorTools;
using FishGame.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Tests.EditMode
{
    public class PlayerPresentationRigTests
    {
        const string MarkerValidMesh = "Assets/_Project/Content/Meshes/Marker_Valid.asset";
        const string MarkerInvalidMesh = "Assets/_Project/Content/Meshes/Marker_Invalid.asset";

        static GameObject LoadPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths.PlayerPrefab);
            Assert.That(prefab, Is.Not.Null, "PlayerFP prefab");
            return prefab;
        }

        // Catches: a presentation component missing, duplicated or out of the C 6.3 order (the order is also the NGO behaviour order contract:
        // nothing after FishingRodController may be a NetworkBehaviour).
        [Test]
        public void PlayerPrefab_PresentationComponents_InContractOrder()
        {
            GameObject prefab = LoadPrefab();
            Component[] all = prefab.GetComponents<Component>();
            int controller = -1;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetType().Name == "FishingRodController") controller = i;
            }
            Assert.That(controller, Is.GreaterThanOrEqualTo(0), "FishingRodController on the root");

            var after = new List<string>();
            for (int i = controller + 1; i < all.Length; i++)
            {
                Assert.That(all[i], Is.Not.Null, "a missing script after FishingRodController (index " + i + ")");
                Assert.That(all[i], Is.Not.InstanceOf<NetworkBehaviour>(), all[i].GetType().Name + " must not be a NetworkBehaviour (C 6.3)");
                after.Add(all[i].GetType().Name);
            }

            Assert.That(after, Is.EqualTo(new[]
            {
                "FishingFeedback", "CatchRevealView", "RodPresenter", "LineView", "BobberView", "SplashFx", "CastMarkerView",
            }), "root components after FishingRodController");
        }

        // Catches: the rig falling back to the M1 materials, or the line losing its 24 world-space points.
        // (Passes in the red run already: T19/T21 built these before T24.)
        [Test]
        public void PlayerPrefab_RodBobberAndLine_UseFinalMaterials()
        {
            GameObject prefab = LoadPrefab();
            for (int i = 0; i < PlayerRigPaths.RodSegmentCount; i++)
            {
                MeshRenderer renderer = prefab.transform.Find(PlayerRigPaths.RodSegmentPrefix + i).GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo("M_Toon_Base"), "Seg" + i + " material");
            }

            Assert.That(prefab.transform.Find(PlayerRigPaths.Bobber).GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("M_Toon_Base"), "Bobber material");
            LineRenderer line = prefab.transform.Find(PlayerRigPaths.Line).GetComponent<LineRenderer>();
            Assert.That(line.sharedMaterial.name, Is.EqualTo("M_Line"), "Line material");
            Assert.That(line.positionCount, Is.EqualTo(24), "Line points");
            Assert.That(line.useWorldSpace, Is.True, "Line world space");
        }

        // Catches: no CastMarker, one shape only (hue-only coding), a marker drawn by default (a remote would flash one), the wrong material
        // (the marker must be the transparent M_Marker, queue 3100), or the two shapes sharing one mesh.
        [Test]
        public void PlayerPrefab_CastMarker_HasValidAndInvalidShapesDisabled()
        {
            GameObject prefab = LoadPrefab();
            Transform marker = prefab.transform.Find(PlayerRigPaths.CastMarker);
            Assert.That(marker, Is.Not.Null, "CastMarker child");
            Assert.That(marker.parent, Is.SameAs(prefab.transform), "CastMarker sits at the root");

            Mesh valid = AssertMarkerShape(marker, "Valid", MarkerValidMesh);
            Mesh invalid = AssertMarkerShape(marker, "Invalid", MarkerInvalidMesh);
            Assert.That(valid, Is.Not.SameAs(invalid), "the two shapes are separate meshes");
            Assert.That(valid.triangles.Length, Is.Not.EqualTo(invalid.triangles.Length), "the shapes differ (solid ring + dot vs dashed ring + cross)");

            // Flat in the XZ plane at y 0 (CastMarkerView places the transform on the water).
            Assert.That(valid.bounds.size.y, Is.LessThan(0.05f), "Valid is flat");
            Assert.That(invalid.bounds.size.y, Is.LessThan(0.05f), "Invalid is flat");
            Assert.That(valid.bounds.size.x, Is.EqualTo(1.0f).Within(0.02f), "Valid outer diameter");
        }

        static Mesh AssertMarkerShape(Transform marker, string child, string meshPath)
        {
            Transform t = marker.Find(child);
            Assert.That(t, Is.Not.Null, "CastMarker/" + child);
            MeshFilter filter = t.GetComponent<MeshFilter>();
            MeshRenderer renderer = t.GetComponent<MeshRenderer>();
            Assert.That(filter, Is.Not.Null, child + " MeshFilter");
            Assert.That(renderer, Is.Not.Null, child + " MeshRenderer");
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo(meshPath), child + " mesh asset");
            Assert.That(renderer.sharedMaterial, Is.Not.Null, child + " material");
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo("M_Marker"), child + " material");
            Assert.That(renderer.enabled, Is.False, child + " renderer is disabled in the prefab (CastMarkerView enables it on the owner only)");
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), child + " casts no shadow");
            return filter.sharedMesh;
        }

        // Catches: SplashFx without its particle material (no splashes, silently), or the reference pointing at another material.
        [Test]
        public void PlayerPrefab_SplashFx_HasParticleMaterial()
        {
            GameObject prefab = LoadPrefab();
            Component splash = null;
            foreach (Component c in prefab.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "SplashFx") splash = c;
            }

            Assert.That(splash, Is.Not.Null, "SplashFx on the root");
            var so = new SerializedObject(splash);
            SerializedProperty property = so.FindProperty("particleMaterial");
            Assert.That(property, Is.Not.Null, "SplashFx.particleMaterial");
            Assert.That(property.objectReferenceValue, Is.Not.Null, "particleMaterial assigned");
            Assert.That(property.objectReferenceValue.name, Is.EqualTo("M_Particle"));
            so.Dispose();
        }

        // Catches: the rod pose moved by the presentation pass (BF-3): TipAnchor and TipVisual share one rest pose and the segments are
        // siblings under Rod at 0.3 m steps (RodPresenter's chain maths assumes exactly this layout).
        [Test]
        public void PlayerPrefab_RodRestPose_AnchorAndVisualShareOnePose()
        {
            GameObject prefab = LoadPrefab();
            Transform anchor = prefab.transform.Find(PlayerRigPaths.RodTipAnchor);
            Transform visual = prefab.transform.Find(PlayerRigPaths.RodTipVisual);
            Transform rod = prefab.transform.Find(PlayerRigPaths.Rod);
            Assert.That(anchor.parent, Is.SameAs(rod), "TipAnchor under Rod");
            Assert.That(visual.parent, Is.SameAs(rod), "TipVisual under Rod");
            Assert.That(Vector3.Distance(anchor.localPosition, visual.localPosition), Is.LessThan(1e-5f), "same rest position");
            Assert.That(anchor.localPosition.z, Is.EqualTo(1.8f).Within(1e-4f), "TipAnchor at the end of Seg5");
            for (int i = 0; i < PlayerRigPaths.RodSegmentCount; i++)
            {
                Transform seg = prefab.transform.Find(PlayerRigPaths.RodSegmentPrefix + i);
                Assert.That(seg.parent, Is.SameAs(rod), "Seg" + i + " under Rod");
                Assert.That(seg.localPosition.z, Is.EqualTo(0.3f * i).Within(1e-4f), "Seg" + i + " rest z");
            }
        }
    }
}
