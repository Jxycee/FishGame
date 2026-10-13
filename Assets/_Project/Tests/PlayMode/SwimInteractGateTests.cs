// Authority: test-only. Spec 8.2 (R70): nothing can be interacted with from the water or from a ladder, and a ladder is entered by walking or
// swimming into its LadderVolume, with no button. LadderR's volume (x 1.55..2.55, y -1.4..1.6, z -42..-41) is ~1.3 m from the bell
// (1.1, 0.6, -40.6), inside PlayerInteractor.CastRange. Input is scripted through FakeInputService; the real owner locomotion and interactor run.
using System.Collections;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class SwimInteractGateTests
    {
        private const float LadderZ = -41.5f;
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: a ladder that still needs Jump/Interact (the M1 exit), entry that forgets to write NetSwimming false, and a swimmer whose
        // forward input never reaches the volume. Only MoveY is ever set.
        [UnityTest]
        public IEnumerator Swimming_HoldForwardIntoLadderVolume_ClimbsWithNoButton()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            var root = new Vector3(3.0f, -1.2f, LadderZ);   // in the water 0.45 m outside LadderR's outer face, facing the dock (-x)
            rig.Locomotion.ApplyTeleport(root, -90f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(InWater(rig), Is.True, "precondition: in the water at " + rig.Player.transform.position);
            Assert.That(LadderVolume.FindContaining(Center(rig)), Is.Null, "precondition: outside every ladder volume");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "holding W into the ladder volume never started a climb");
            rig.Fake.Frame = default;
            yield return null;

            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Climbing));
            Assert.That(rig.Locomotion.IsSwimming, Is.False, "a climber is not swimming");
            yield return PlayModeHarness.WaitUntil(() => !rig.Player.IsSwimming, 1f, "NetSwimming was not written false on ladder entry");
            rig.Log.AssertEmpty("swim into the ladder");
        }

        // Catches: PlayerInteractor or the router live while Climbing: from inside LadderR's volume at wading depth, aimed at the bell, one E
        // press would ring it (and, in a debug build, sell the creel).
        [UnityTest]
        public IEnumerator Climbing_InteractAimedAtBell_DoesNotRingBell()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            DockBell bell = Object.FindAnyObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            PlayerInteractor interactor = rig.Player.GetComponent<PlayerInteractor>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(interactor, Is.Not.Null);

            var item = new CreelItem { ItemIndex = 1, WeightG = 300, ValueCoins = 7, Quality = CatchQuality.Normal };
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            int coinsBefore = rig.Creel.NetDebugCoins.Value;
            int ringsBefore = bell.LocalRingCount;
            int rangEvents = 0;
            bell.Rang += () => rangEvents++;

            var root = new Vector3(2.05f, -0.9f, LadderZ);   // centre of LadderR's volume at wading depth; climbing hangs here (no gravity)
            yield return AimAtBell(rig, camera, bell, root);
            rig.Locomotion.ApplyTeleport(root, YawTo(root, bell));
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing && rig.Fake.GameplayContext == InputContext.Climbing,
                PlayModeHarness.DefaultTimeoutSeconds, "the teleported player never started climbing at " + root);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            Assert.That(CastReachesBell(camera, bell), Is.True, "precondition: the aimed sphere cast must hit the bell from " + rig.Player.transform.position);
            Assert.That(bell.CanInteract(0UL), Is.True, "precondition: the bell is off cooldown");
            Assert.That(interactor.Current, Is.Null, "a climber has no current interactable even with the bell in cast range");

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { InteractPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);   // any stray InteractRpc would have landed by now

            Assert.That(rig.Locomotion.IsClimbing, Is.True, "E does nothing on a ladder: still hanging");
            Assert.That(bell.LocalRingCount, Is.EqualTo(ringsBefore), "the bell must not ring from a ladder");
            Assert.That(rangEvents, Is.EqualTo(0));
            Assert.That(rig.Creel.Count, Is.EqualTo(2), "the debug bell sell must not run");
            Assert.That(rig.Creel.NetDebugCoins.Value, Is.EqualTo(coinsBefore));
            rig.Log.AssertEmpty("interact on the ladder");
        }

        // Catches: the same gate in open water (no ladder, bell in range): the press must do nothing.
        [UnityTest]
        public IEnumerator Swimming_InteractInOpenWater_DoesNotRingBell()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            DockBell bell = Object.FindAnyObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            Assert.That(camera, Is.Not.Null, "PlayerCamera missing on PlayerFP");
            int ringsBefore = bell.LocalRingCount;

            // Deep enough (bed ~ -1.6 m) to keep swimming, outside every ladder volume and exit, bell about 2.5 m away.
            var root = new Vector3(2.6f, -1.45f, -39.0f);
            yield return AimAtBell(rig, camera, bell, root);
            rig.Locomotion.ApplyTeleport(root, YawTo(root, bell));
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsSwimming && rig.Fake.GameplayContext == InputContext.Swimming,
                PlayModeHarness.DefaultTimeoutSeconds, "the teleported player never reached the Swimming context at " + root);
            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Swimming), "precondition: still swimming at " + rig.Player.transform.position);
            Assert.That(LadderVolume.FindContaining(Center(rig)), Is.Null, "precondition: outside every ladder volume");
            Assert.That(WaterExit.FindContaining(rig.Player.transform.position), Is.Null, "precondition: outside every exit");
            Assert.That(CastReachesBell(camera, bell), Is.True, "precondition: the aimed sphere cast must hit the bell from " + rig.Player.transform.position);

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { InteractPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);

            Assert.That(rig.Locomotion.IsClimbing, Is.False, "no ladder here, nothing to climb");
            Assert.That(bell.LocalRingCount, Is.EqualTo(ringsBefore), "the bell must not ring from a swimmer's Interact press");
            rig.Log.AssertEmpty("swim interact in open water");
        }

        internal static Vector3 Center(FishingRig rig)
        {
            return rig.Player.transform.TransformPoint(rig.Player.GetComponent<CharacterController>().center);
        }

        internal static bool InWater(FishingRig rig)
        {
            Vector3 p = rig.Player.transform.position;
            return p.y < WaterSurface.HeightAt(p.x, p.z) - MovementMath.WadeDepth;
        }

        private static float YawTo(Vector3 root, DockBell bell)
        {
            Vector3 toBell = bell.transform.position - root;
            return Mathf.Atan2(toBell.x, toBell.z) * Mathf.Rad2Deg;
        }

        // Pitches the look (while the player still stands on the deck) so that, from `root`, the camera ray passes through the bell
        // collider's centre. The pitch survives the teleport.
        private static IEnumerator AimAtBell(FishingRig rig, PlayerCamera camera, DockBell bell, Vector3 root)
        {
            Collider collider = bell.GetComponentInChildren<Collider>();
            Assert.That(collider, Is.Not.Null, "the bell needs a collider on the Interactable layer");
            Vector3 eye = root + Vector3.up * MovementMath.EyeHeight;
            Vector3 to = collider.bounds.center - eye;
            float horizontal = new Vector2(to.x, to.z).magnitude;
            float pitch = Mathf.Atan2(to.y, horizontal) * Mathf.Rad2Deg;
            yield return FishingPlayModeDriver.SetPitch(rig, camera, pitch);
        }

        private static bool CastReachesBell(PlayerCamera camera, DockBell bell)
        {
            Transform origin = camera.Camera.transform;
            return Physics.SphereCast(origin.position, PlayerInteractor.CastRadius, origin.forward, out RaycastHit hit,
                       PlayerInteractor.CastRange, Layers.Mask(Layers.Interactable), QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<DockBell>() == bell;
        }
    }
}
