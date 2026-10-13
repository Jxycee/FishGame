// Authority: test-only. T17 review fix: both harbour ladders can actually be climbed (up from the water onto the deck, and down off the deck edge).
// Modelled on LadderClimbTests (Ladder_ClimbUpFromWater_Pitch45_LandsOnDeckWithin2s and the step-off-deck-edge test) on the real Spot_HarbourPier,
// reached through the dev map switch. The rail collider boxes must end at the deck top (as LakeDockBuilder.AddLadder): a rail wall above the deck
// leaves the climber short of the deck lip (the feet top out at about 1.6 against StepOffset 0.35), so the climb loops and climbing down is blocked.
// Input is scripted through FakeInputService; the real owner locomotion runs. Times are game time (Time.time).
using System.Collections;
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class HarbourLadderTests
    {
        private const float DeckTopY = 1.5f;
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            EcologySession.Clear();
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Starts a solo session on the lake, then dev-loads the harbour and refreshes the rig (every spawned object is new after the switch).
        private IEnumerator ArriveAtHarbour()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;
            WorldState oldWorld = WorldState.Current;
            PlayerNetState oldPlayer = PlayerNetState.Local;
            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "no App EventBus");
            bus.Publish(new DevLoadSpotRequested(ContentIds.HarbourPier));
            yield return PlayModeHarness.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.SpotHarbourPier
                      && WorldState.Current != null && WorldState.Current != oldWorld && WorldState.Current.HasState
                      && PlayerNetState.Local != null && PlayerNetState.Local != oldPlayer && FishingRodController.Local != null,
                20f, "the dev load never produced the harbour scene, its World and a new local player");
            rig.Player = PlayerNetState.Local;
            rig.Net = rig.Player.GetComponent<FishingRodNetwork>();
            rig.Creel = rig.Player.GetComponent<PlayerCreel>();
            rig.Locomotion = rig.Player.GetComponent<PlayerLocomotion>();
            rig.Controller = FishingRodController.Local;
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the harbour deck");
            Assert.That(LadderVolume.All.Count, Is.EqualTo(2), "the harbour's two ladder volumes");
        }

        private static bool OnDeck(FishingRig rig, bool head)
        {
            Vector3 p = rig.Player.transform.position;
            bool inFootprint = head ? p.z < -5f && Mathf.Abs(p.x) < 5f : p.x < 1.5f;   // T-head, or the main deck beside the mid ladder
            return rig.Locomotion.IsGrounded && !rig.Locomotion.IsClimbing && inFootprint && Mathf.Abs(p.y - DeckTopY) < 0.08f;
        }

        // Starts inside the ladder volume in the water, facing the pier, looking up 45 deg, W held.
        private IEnumerator ClimbUp(Vector3 start, float yaw, bool head)
        {
            yield return ArriveAtHarbour();
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 45f);
            rig.Locomotion.ApplyTeleport(start, yaw);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(SwimInteractGateTests.InWater(rig), Is.True, "precondition: in the water at " + rig.Player.transform.position);

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never reached the ladder");
            float t0 = Time.time;
            yield return PlayModeHarness.WaitUntil(() => OnDeck(rig, head), 5f, "never got onto the deck (at " + rig.Player.transform.position + ")");
            float took = Time.time - t0;
            rig.Fake.Frame = default;
            TestContext.WriteLine("FG HARBOUR_LADDER " + (head ? "head" : "mid") + " climb-out " + took.ToString("F2") + " s");
            Assert.That(took, Is.LessThanOrEqualTo(2f), "climb-out within 2 s");
            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(rig.Locomotion.IsSwimming, Is.False);
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Free), "back to Free on the deck");
            rig.Log.AssertEmpty("harbour ladder climb up");
        }

        // Starts on the deck beside the ladder, steps off the edge looking down 70 deg and must climb down to y -0.5 without falling.
        private IEnumerator ClimbDown(Vector3 start, float yaw)
        {
            yield return ArriveAtHarbour();
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            // -85 deg (the pitch limit): the deck is 0.9 m higher than the lake's, so the descent is longer and the outward drift of a -70 deg descent
            // (0.82 m/s) would carry the capsule out of the 1 m volume through its outer face before it reaches the water.
            yield return FishingPlayModeDriver.SetPitch(rig, camera, -85f);
            rig.Locomotion.ApplyTeleport(start, yaw);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            Assert.That(rig.Locomotion.IsClimbing, Is.False, "precondition: not on the ladder yet");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "walking off the deck edge never started a climb");
            Assert.That(rig.Player.transform.position.y, Is.GreaterThan(DeckTopY - 0.15f), "caught at the deck edge, not after a fall");

            double until = Time.realtimeSinceStartupAsDouble + 6.0;
            while (rig.Player.transform.position.y > -0.5f)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble < until, Is.True, "never descended below y -0.5 (at " + rig.Player.transform.position + ")");
                Assert.That(rig.Locomotion.IsClimbing, Is.True, "fell off the ladder at " + rig.Player.transform.position);
                yield return null;
            }
            rig.Fake.Frame = default;
            yield return null;
            rig.Log.AssertEmpty("harbour ladder climb down");
        }

        // Catches: a rail collider wall above the deck (0.85 m at the old 2.35 top): the climber cannot top out onto the pier.
        [UnityTest]
        public IEnumerator HarbourLadder_Mid_ClimbUpFromWater_LandsOnDeckWithin2s()
        {
            yield return ClimbUp(new Vector3(2.05f, -1.2f, -25f), 270f, false);
        }

        [UnityTest]
        public IEnumerator HarbourLadder_Head_ClimbUpFromWater_LandsOnTHeadWithin2s()
        {
            yield return ClimbUp(new Vector3(2.0f, -1.2f, -4.45f), 180f, true);
        }

        // Catches: the same wall blocking the way down (the climber is stopped at the deck edge or pushed back).
        [UnityTest]
        public IEnumerator HarbourLadder_Mid_StepOffDeckEdge_ClimbsDown()
        {
            yield return ClimbDown(new Vector3(1.2f, 1.55f, -25f), 90f);
        }

        [UnityTest]
        public IEnumerator HarbourLadder_Head_StepOffTHeadEdge_ClimbsDown()
        {
            yield return ClimbDown(new Vector3(2.0f, 1.55f, -5.4f), 0f);
        }
    }
}
