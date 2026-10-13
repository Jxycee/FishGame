// Authority: test-only. Fake input drives the owner's real PlayerLocomotion; TeleportRpc (host -> owner) is invoked by the host on its own player.
using System.Collections;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class PlayerMovementTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest]
        public IEnumerator FakeInput_MoveForward_MovesPlayerMoreThanOneMeterInOneSecond()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            var fake = InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            var player = PlayerNetState.Local;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Assert.That(locomotion, Is.Not.Null, "PlayerLocomotion missing on PlayerFP");
            Assert.That(locomotion.enabled, Is.True, "PlayerLocomotion must be enabled on the owner");
            yield return PlayModeHarness.WaitUntil(() => locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds,
                "Player never became grounded on the dock");

            Vector3 start = player.transform.position;
            fake.Frame = new PlayerInputFrame { MoveY = 1f };
            float t0 = Time.time;
            int f0 = Time.frameCount;
            yield return PlayModeHarness.WaitGameSeconds(1f, PlayModeHarness.DefaultTimeoutSeconds, "Forward walk never accumulated 1 s of game time");
            float elapsed = Time.time - t0;
            int frames = Time.frameCount - f0;
            fake.Frame = default;

            Vector3 delta = player.transform.position - start;
            float flat = new Vector2(delta.x, delta.z).magnitude;
            string detail = $"moved {flat:F3} m (dz {delta.z:F3}) over {elapsed:F3} s of game time in {frames} frames";
            Assert.That(flat, Is.GreaterThan(1f), "Forward input must move the player more than 1 m in 1 s; " + detail);
            Assert.That(delta.z, Is.GreaterThan(1f), "Spawn faces +z (yaw 0), so forward input must move toward +z; " + detail);
        }

        [UnityTest]
        public IEnumerator TeleportRpc_OnDeck_LandsWithinFiveCentimetersWithZeroVelocity()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            var fake = InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            var player = PlayerNetState.Local;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var actions = player.GetComponent<PlayerActions>();
            Assert.That(locomotion, Is.Not.Null, "PlayerLocomotion missing on PlayerFP");
            Assert.That(actions, Is.Not.Null, "PlayerActions missing on PlayerFP");
            yield return PlayModeHarness.WaitUntil(() => locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds,
                "Player never became grounded on the dock");

            // Walk first, so that "zero velocity after teleport" is not trivially true.
            fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitGameSeconds(0.4f, PlayModeHarness.DefaultTimeoutSeconds, "Walk precondition never accumulated 0.4 s of game time");
            float walkSpeed = new Vector2(locomotion.Velocity.x, locomotion.Velocity.z).magnitude;
            Assert.That(walkSpeed, Is.GreaterThan(1f), $"Precondition: the player must be walking before the teleport (speed {walkSpeed:F3})");
            fake.Frame = default;

            // Deck: x in [-1.5, 1.5], z in [-52, -40], top at y 0.6. Root y 0.65 matches the spawn points.
            var target = new Vector3(0f, 0.65f, -44f);
            const float yaw = 90f;

            // Host calling a SendTo.Owner RPC on its own player: NGO runs it locally and synchronously (DeferLocal is false).
            actions.TeleportRpc(target, yaw);

            float landed = Vector3.Distance(player.transform.position, target);
            Assert.That(landed, Is.LessThanOrEqualTo(0.05f), $"Teleport must land within 0.05 m of the target (was {landed:F4} m)");
            Assert.That(locomotion.Velocity.magnitude, Is.LessThan(1e-4f),
                $"ApplyTeleport must zero velocity (was {locomotion.Velocity})");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw)), Is.LessThan(1f),
                $"Yaw must match the teleport yaw (was {player.transform.eulerAngles.y:F2})");

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Vector3 after = player.transform.position;
            float drift = new Vector2(after.x - target.x, after.z - target.z).magnitude;
            Assert.That(drift, Is.LessThanOrEqualTo(0.05f),
                $"Player must stay within 0.05 m (XZ) of the teleport target a few frames later (drifted {drift:F4} m)");
            Assert.That(Mathf.Abs(after.y - target.y), Is.LessThan(0.3f), $"Player must stay on the deck after teleport (y {after.y:F3})");
            float residual = new Vector2(locomotion.Velocity.x, locomotion.Velocity.z).magnitude;
            Assert.That(residual, Is.LessThan(0.05f), $"No horizontal momentum may survive the teleport (speed {residual:F4})");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw)), Is.LessThan(1f),
                "Yaw must stay at the teleport yaw with no look input");
        }

        // Must run before StartSoloAndWaitForPlayer: PlayerLocomotion/PlayerCamera/PlayerInteractor read IInputService in OnEnable.
        private static FakeInputService InstallFakeInput()
        {
            Assert.That(ServiceRoots.App, Is.Not.Null, "ServiceRoots.App missing; load Bootstrap first");
            var fake = new FakeInputService();
            fake.MenuOpen = false;
            fake.GameplayContext = InputContext.Free;
            fake.Frame = default;
            ServiceRoots.App.Replace<IInputService>(fake);
            return fake;
        }
    }
}
