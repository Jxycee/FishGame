// Authority: none (EditMode tests on a GameObject built in the test). FishGame.Gameplay.LadderVolume local-space containment (R70).
// In EditMode AddComponent does not run Awake/OnEnable, so these call Contains / IsAtOrAboveTop directly (FindContaining is PlayMode-tested).
using FishGame.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class LadderVolumeTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        // The lake-dock LadderR numbers: centre (2.05, 0.1, -41.5), size (1, 3, 1) -> x 1.55..2.55, y -1.4..1.6, z -42..-41.
        private LadderVolume Make(Vector3 boxCenter)
        {
            _go = new GameObject("LadderTest");
            _go.transform.position = new Vector3(2.05f, 0.1f, -41.5f);
            BoxCollider box = _go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = boxCenter;
            box.size = new Vector3(1f, 3f, 1f);
            return _go.AddComponent<LadderVolume>();
        }

        // Catches: using the full size as the half extent (a box twice as big), a box shifted off its transform, and a wrong face.
        [Test]
        public void LadderVolume_Contains_InsideAndOnFaces_NotJustOutside()
        {
            LadderVolume v = Make(Vector3.zero);
            Assert.IsTrue(v.Contains(new Vector3(2.05f, 0.1f, -41.5f)), "centre");
            Assert.IsTrue(v.Contains(new Vector3(1.551f, 1.599f, -41.001f)), "1 mm inside the deck-side top corner");   // exact-face points are float-fragile
            Assert.IsFalse(v.Contains(new Vector3(1.54f, 0.1f, -41.5f)), "1 cm inside the deck side");
            Assert.IsFalse(v.Contains(new Vector3(2.56f, 0.1f, -41.5f)), "1 cm past the outer face");
            Assert.IsFalse(v.Contains(new Vector3(2.05f, 1.61f, -41.5f)), "1 cm above the top");
            Assert.IsFalse(v.Contains(new Vector3(2.05f, -1.41f, -41.5f)), "1 cm below the bottom");
            Assert.IsFalse(v.Contains(new Vector3(2.05f, 0.1f, -40.99f)), "1 cm past max z");
        }

        // Catches: ignoring BoxCollider.center.
        [Test]
        public void LadderVolume_BoxCenterOffset_IsHonoured()
        {
            LadderVolume v = Make(new Vector3(0f, 1f, 0f));   // y -0.4..2.6
            Assert.IsTrue(v.Contains(new Vector3(2.05f, 2.5f, -41.5f)));
            Assert.IsFalse(v.Contains(new Vector3(2.05f, -0.5f, -41.5f)));
        }

        // Catches: the grace firing for side exits low on the ladder, or not at the top (TopEpsilon 0.05 below the face).
        [Test]
        public void LadderVolume_IsAtOrAboveTop_WithinEpsilonBelowTheFace()
        {
            LadderVolume v = Make(Vector3.zero);
            Assert.IsTrue(v.IsAtOrAboveTop(new Vector3(2.05f, 1.7f, -41.5f)), "above the top");
            Assert.IsTrue(v.IsAtOrAboveTop(new Vector3(1.4f, 1.56f, -41.5f)), "0.04 below the top, beside the box");
            Assert.IsFalse(v.IsAtOrAboveTop(new Vector3(2.05f, 1.54f, -41.5f)), "0.06 below the top");
        }

        // Catches: a NullReferenceException on a misbuilt volume.
        [Test]
        public void LadderVolume_WithoutBoxCollider_ContainsNothing()
        {
            _go = new GameObject("NoBox");
            LadderVolume v = _go.AddComponent<LadderVolume>();
            Assert.IsFalse(v.Contains(Vector3.zero));
            Assert.IsFalse(v.IsAtOrAboveTop(Vector3.zero));
        }
    }
}
