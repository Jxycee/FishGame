// Authority: test-only. Spec 8.2 Swimming row: Interact = "climb at exits" only. The LadderR exit (2.1, -0.8, -41.5) is ~1.5 m from the bell
// (1.1, 0.6, -40.6), inside PlayerInteractor.CastRange, so a swimmer's Interact press must climb and must never ring the bell
// (in a debug build a ring also sells the creel). Input is scripted through FakeInputService; the real owner locomotion and interactor run.
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
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: PlayerInteractor staying live while Swimming, so one Interact press at the ladder both climbs and rings the bell
        // (and, in a debug build, sells the creel). The climb itself must still start from the same press.
        [UnityTest]
        public IEnumerator Swimming_InteractAtLadderExit_ClimbsAndDoesNotRingBell()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            DockBell bell = UnityEngine.Object.FindAnyObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            Assert.That(camera, Is.Not.Null, "PlayerCamera missing on PlayerFP");

            var item = new CreelItem { ItemIndex = 1, WeightG = 300, ValueCoins = 7, Quality = CatchQuality.Normal };
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            int coinsBefore = rig.Creel.NetDebugCoins.Value;
            int ringsBefore = bell.LocalRingCount;
            int rangEvents = 0;
            bell.Rang += () => rangEvents++;

            // Inside the LadderR trigger box (x 1.5..2.7, y -2.1..0.5, z -42.1..-40.9) on its deeper (z -41.0) side. The bed there is only ~ -1.25 m,
            // just under the swim threshold (root < -1.2), so the swimmer can flicker out with the swell: the press goes in on the very
            // frame the Swimming context is seen (no yield between the check and the press), pitch and yaw are set before the teleport.
            var root = new Vector3(2.1f, -1.4f, -41.0f);
            yield return AimAtBell(rig, camera, bell, root);
            rig.Locomotion.ApplyTeleport(root, YawTo(root, bell));
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsSwimming && rig.Fake.GameplayContext == InputContext.Swimming,
                PlayModeHarness.DefaultTimeoutSeconds, "the teleported player never reached the Swimming context at " + root);
            Assert.That(WaterExit.FindContaining(rig.Player.transform.position), Is.Not.Null, "precondition: the swimmer is inside a WaterExit");

            // Precondition: the owner's own sphere cast would reach the bell from here, so only the context gate can stop the ring.
            Assert.That(CastReachesBell(camera, bell), Is.True, "precondition: the aimed sphere cast must hit the bell from " + rig.Player.transform.position);
            Assert.That(bell.CanInteract(0UL), Is.True, "precondition: the bell is off cooldown");

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { InteractPressed = true });
            rig.Fake.Frame = default;
            Assert.That(rig.Locomotion.IsClimbing, Is.True, "the Interact press inside a WaterExit must start the climb");
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Climbing));

            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, PlayModeHarness.DefaultTimeoutSeconds, "the climb never finished");
            yield return PlayModeHarness.WaitFrames(5, 0.1f);   // any stray InteractRpc would have landed by now

            Assert.That(bell.LocalRingCount, Is.EqualTo(ringsBefore), "the bell must not ring from a swimmer's Interact press");
            Assert.That(rangEvents, Is.EqualTo(0));
            Assert.That(rig.Creel.Count, Is.EqualTo(2), "the debug bell sell must not run");
            Assert.That(rig.Creel.NetDebugCoins.Value, Is.EqualTo(coinsBefore));
            rig.Log.AssertEmpty("swim interact at the ladder");
        }

        // Catches: the same gate when there is no exit to climb (open water just off the ladder, bell in range): the press must do nothing.
        // Unlike the ladder case this does not depend on whether the locomotion or the interactor updates first in the frame.
        [UnityTest]
        public IEnumerator Swimming_InteractInOpenWater_DoesNotRingBell()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            DockBell bell = UnityEngine.Object.FindAnyObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            Assert.That(camera, Is.Not.Null, "PlayerCamera missing on PlayerFP");
            int ringsBefore = bell.LocalRingCount;

            // Deep enough (bed ~ -1.6 m) to keep swimming, outside every WaterExit, bell about 2.5 m away.
            var root = new Vector3(2.6f, -1.45f, -39.0f);
            yield return AimAtBell(rig, camera, bell, root);
            rig.Locomotion.ApplyTeleport(root, YawTo(root, bell));
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsSwimming && rig.Fake.GameplayContext == InputContext.Swimming,
                PlayModeHarness.DefaultTimeoutSeconds, "the teleported player never reached the Swimming context at " + root);
            yield return PlayModeHarness.WaitFrames(5, 0.1f);   // let the buoyancy settle near the surface
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Swimming), "precondition: still swimming at " + rig.Player.transform.position);
            Assert.That(WaterExit.FindContaining(rig.Player.transform.position), Is.Null, "precondition: the swimmer is outside every WaterExit");
            Assert.That(CastReachesBell(camera, bell), Is.True, "precondition: the aimed sphere cast must hit the bell from " + rig.Player.transform.position);

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { InteractPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);

            Assert.That(rig.Locomotion.IsClimbing, Is.False, "no exit here, nothing to climb");
            Assert.That(bell.LocalRingCount, Is.EqualTo(ringsBefore), "the bell must not ring from a swimmer's Interact press");
            rig.Log.AssertEmpty("swim interact in open water");
        }

        private static float YawTo(Vector3 root, DockBell bell)
        {
            Vector3 toBell = bell.transform.position - root;
            return Mathf.Atan2(toBell.x, toBell.z) * Mathf.Rad2Deg;
        }

        // Pitches the look up (while the player still stands on the deck) so that, from `root`, the camera ray passes through the bell
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
