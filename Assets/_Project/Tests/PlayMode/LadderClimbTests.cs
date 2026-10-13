// Authority: test-only. R70 ladders on the real Spot_LakeDock (spec 7.3, contract M2-D29): geometry, climb up from the water at three look
// pitches (the 7.8 #3 check at 45 deg), stepping off the deck edge to descend, hanging, the deck-edge boundary and the top-out grace.
// Input is scripted through FakeInputService; the real owner locomotion runs. Times are game time (Time.time).
using System.Collections;
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class LadderClimbTests
    {
        private const float LadderZ = -41.5f, DeckTopY = 0.6f, DeckEdgeX = 1.5f;
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: a ladder volume missing, misplaced, the wrong size, not a trigger, or on the Water layer (CastProbe would then read the box
        // top at y 1.6 as the water surface next to the ladder).
        [UnityTest]
        public IEnumerator LakeDock_LadderVolumes_TwoMirroredTriggersOffTheCastMask()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);

            Assert.That(LadderVolume.All.Count, Is.EqualTo(2), "two ladder volumes");
            bool left = false, right = false;
            foreach (LadderVolume v in LadderVolume.All)
            {
                BoxCollider box = v.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null, v.name);
                Assert.That(box.isTrigger, Is.True, v.name + " is a trigger");
                Assert.That(v.gameObject.layer, Is.EqualTo(Layers.Default), v.name + " layer");
                Assert.That(CastProbe.CastMask & Layers.Mask(v.gameObject.layer), Is.EqualTo(0), v.name + " is outside CastProbe.CastMask");
                Bounds b = box.bounds;
                float side = Mathf.Sign(b.center.x);
                left |= side < 0f;
                right |= side > 0f;
                Assert.That(Mathf.Abs(b.min.x * side - 1.55f) < 1e-3f || Mathf.Abs(b.max.x * side - 1.55f) < 1e-3f, Is.True, v.name + " inner face at |x| 1.55");
                Assert.That(b.size.x, Is.EqualTo(1f).Within(1e-3f), v.name + " width");
                Assert.That(b.min.y, Is.EqualTo(-1.4f).Within(1e-3f), v.name + " bottom");
                Assert.That(b.max.y, Is.EqualTo(1.6f).Within(1e-3f), v.name + " top (fallback 1.9, risk 1)");
                Assert.That(b.min.z, Is.EqualTo(-42f).Within(1e-3f), v.name + " min z");
                Assert.That(b.max.z, Is.EqualTo(-41f).Within(1e-3f), v.name + " max z");
            }

            Assert.That(left && right, Is.True, "one volume on each side of the dock");
            Assert.That(WaterExit.All.Count, Is.EqualTo(1), "only the ramp keeps a WaterExit");
            Assert.That(WaterExit.All[0].name, Is.EqualTo("Ramp"));

            WaterProbe probe = CastProbe.Probe(2.05f, LadderZ);
            Assert.That(probe.FirstHitIsWater, Is.True, "a cast beside the ladder still lands on water");
            Assert.That(probe.WaterY, Is.LessThan(0.3f), "the water height beside the ladder is the lake, not the ladder box top");
            _rig.Log.AssertEmpty("ladder geometry");
        }

        // Catches (spec 7.8 #3): the climb-out failing or taking longer than 2 s at a 45 deg look-up (pitch sign wrong, Move zeroed while
        // climbing, the volume top too low, the exit velocity dropped).
        [UnityTest]
        public IEnumerator Ladder_ClimbUpFromWater_Pitch45_LandsOnDeckWithin2s()
        {
            yield return ClimbUpFromWater(45f, 2.0f);
        }

        // Catches: a shallow look-up that cannot top out (the climber slides up the rail but never gets over the deck lip).
        [UnityTest]
        public IEnumerator Ladder_ClimbUpFromWater_Pitch30_LandsOnDeck()
        {
            yield return ClimbUpFromWater(30f, 3.0f);
        }

        // Catches: a steep look-up that leaves the box through the top short of the deck and falls back into the lake.
        [UnityTest]
        public IEnumerator Ladder_ClimbUpFromWater_Pitch60_LandsOnDeck()
        {
            yield return ClimbUpFromWater(60f, 3.0f);
        }

        // Catches: no entry from the deck edge (R70 "step off the deck edge"), free fall instead of a controlled descent, input that keeps
        // moving the climber after release (no hang), and a climber who cannot leave the ladder sideways into the water.
        [UnityTest]
        public IEnumerator Ladder_StepOffDeckEdgeLookingDown_DescendsHangsAndLeavesIntoWater()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            // -70 deg: W descends at 2.26 m/s and drifts outward at only 0.82 m/s, so the climber reaches the water well inside the box
            // (at -60 the 1.2 m/s outward drift would carry the capsule centre past the outer face, x 2.55, before y -0.5).
            yield return FishingPlayModeDriver.SetPitch(rig, camera, -70f);

            rig.Locomotion.ApplyTeleport(new Vector3(1.2f, 0.65f, LadderZ), 90f);   // on the deck, facing +x toward LadderR
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            Assert.That(rig.Locomotion.IsClimbing, Is.False, "precondition: not on the ladder yet");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "walking off the deck edge never started a climb");
            Assert.That(rig.Player.transform.position.y, Is.GreaterThan(DeckTopY - 0.15f), "caught at the deck edge, not after a fall");

            float maxDownSpeed = 0f;
            double until = Time.realtimeSinceStartupAsDouble + 5.0;
            while (rig.Player.transform.position.y > -0.5f)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble < until, Is.True, "never descended below y -0.5 (at " + rig.Player.transform.position + ")");
                Assert.That(rig.Locomotion.IsClimbing, Is.True, "fell off the ladder at " + rig.Player.transform.position);
                maxDownSpeed = Mathf.Max(maxDownSpeed, -rig.Locomotion.Velocity.y);
                yield return null;
            }

            Assert.That(maxDownSpeed, Is.LessThanOrEqualTo(MovementMath.ClimbSpeed + 0.05f), "descent faster than ClimbSpeed means gravity acted");
            Assert.That(maxDownSpeed, Is.GreaterThan(1.5f), "W while looking down 70 deg descends at about 2.26 m/s");

            // Release: instant stop and hang.
            yield return FishingPlayModeDriver.Step(rig.Fake, default);
            yield return null;
            Assert.That(rig.Locomotion.Velocity.magnitude, Is.LessThan(1e-3f), "release stops the climber at once");
            float hangY = rig.Player.transform.position.y;
            yield return PlayModeHarness.WaitGameSeconds(0.3f, PlayModeHarness.DefaultTimeoutSeconds, "hang");
            Assert.That(rig.Locomotion.IsClimbing, Is.True, "still on the ladder");
            Assert.That(Mathf.Abs(rig.Player.transform.position.y - hangY), Is.LessThan(0.005f), "hangs in place with no input");

            // Leave sideways: look level, W toward +x out of the box (outer face x 2.55) into the water.
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 0f);
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 2f, "never left the ladder through the outer face");
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(SwimInteractGateTests.InWater(rig), Is.True, "left the ladder into the water");
            Assert.That(rig.Fake.GameplayContext, Is.Not.EqualTo(InputContext.Climbing));
            rig.Log.AssertEmpty("descend the ladder");
        }

        // Catches: an inner face reaching onto the deck (walking along the deck beside a ladder would grab the player).
        [UnityTest]
        public IEnumerator Ladder_StandingOnDeckInsideTheEdge_DoesNotClimb()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            rig.Locomotion.ApplyTeleport(new Vector3(1.4f, 0.65f, LadderZ), 90f);   // capsule centre x 1.4 < inner face 1.55
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            double until = Time.realtimeSinceStartupAsDouble + 0.5;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                Assert.That(rig.Locomotion.IsClimbing, Is.False, "standing on the deck at x 1.4 must not climb");
                yield return null;
            }

            rig.Log.AssertEmpty("deck beside the ladder");
        }

        // Catches: no re-entry grace (the falling climber is caught again the moment the capsule centre drops back into the box, ~0.15 s
        // after topping out) and a grace that never ends (the climber would fall past the ladder into the lake).
        [UnityTest]
        public IEnumerator Ladder_TopOut_BlocksReentryFor025s_ThenCatchesTheFall()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 45f);

            // Inside LadderR near its top, facing away from the dock (+x) so the top-out is not over the deck.
            rig.Locomotion.ApplyTeleport(new Vector3(2.05f, 0.6f, LadderZ), 90f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 2f, "never started climbing near the top");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 2f, "never topped out");
            float exitTime = Time.time;
            rig.Fake.Frame = default;
            Assert.That(SwimInteractGateTests.Center(rig).y, Is.GreaterThan(1.55f), "precondition: left through the top face");

            int insideButFree = 0;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (!rig.Locomotion.IsClimbing && LadderVolume.FindContaining(SwimInteractGateTests.Center(rig)) != null) insideButFree++;
                return rig.Locomotion.IsClimbing;
            }, 2f, "the ladder never caught the falling climber after the grace");
            float gap = Time.time - exitTime;
            TestContext.WriteLine($"top-out re-entry after {gap:F3} s, {insideButFree} frames inside the box without climbing");

            Assert.That(gap, Is.GreaterThanOrEqualTo(MovementMath.ClimbReentryGrace - 0.01f), "re-entry inside the 0.25 s grace");
            Assert.That(gap, Is.LessThan(0.6f), "the grace never ended");
            Assert.That(insideButFree, Is.GreaterThanOrEqualTo(2), "the centre was back in the box but the grace held the climb off");
            Assert.That(rig.Player.transform.position.y, Is.GreaterThan(0f), "caught above the lake");
            rig.Log.AssertEmpty("top-out grace");
        }

        // Shared body of the three climb-out tests. Starts in the water 0.45 m outside LadderR, facing the dock, with the given look pitch.
        private IEnumerator ClimbUpFromWater(float pitchDeg, float limitSeconds)
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            yield return FishingPlayModeDriver.SetPitch(rig, camera, pitchDeg);   // on the deck, before the teleport; the pitch survives it

            rig.Locomotion.ApplyTeleport(new Vector3(3.0f, -1.2f, LadderZ), -90f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(SwimInteractGateTests.InWater(rig), Is.True, "precondition: in the water");
            Assert.That(LadderVolume.FindContaining(SwimInteractGateTests.Center(rig)), Is.Null, "precondition: outside the ladder volume");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never reached the ladder");
            float start = Time.time;
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Climbing));

            yield return PlayModeHarness.WaitUntil(() => OnDeck(rig), limitSeconds + 3f,
                "never got onto the deck at pitch " + pitchDeg + " (at " + rig.Player.transform.position + ")");
            float took = Time.time - start;
            rig.Fake.Frame = default;
            TestContext.WriteLine($"climb-out at pitch {pitchDeg}: {took:F2} s");
            Assert.That(took, Is.LessThanOrEqualTo(limitSeconds), "climb-out at pitch " + pitchDeg);

            yield return PlayModeHarness.WaitFrames(5, 0.1f);
            Assert.That(rig.Locomotion.IsSwimming, Is.False);
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.Free), "back to Free on the deck");
            rig.Log.AssertEmpty("climb out at pitch " + pitchDeg);
        }

        private static bool OnDeck(FishingRig rig)
        {
            Vector3 p = rig.Player.transform.position;
            return rig.Locomotion.IsGrounded && !rig.Locomotion.IsClimbing && p.x < DeckEdgeX && Mathf.Abs(p.y - DeckTopY) < 0.08f;
        }
    }
}
