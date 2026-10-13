// Authority: test-only. R70 ladders on the real Spot_LakeDock (spec 7.3, contract M2-D29): geometry, climb up from the water at three look
// pitches (the 7.8 #3 check at 45 deg), stepping off the deck edge to descend, hanging, the deck-edge boundary and the top-out grace.
// Input is scripted through FakeInputService; the real owner locomotion runs. Times are game time (Time.time).
using System;
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

        // A movement lock the test can switch (as FishingRodController does while RodFighting): PlayerLocomotion caches the sources in OnEnable.
        private sealed class LockSource : MonoBehaviour, IPlayerContextSource
        {
            public bool Locks;

            public InputContext RequestedContext => InputContext.Free;
            public bool LocksMovement => Locks;
        }

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
        // climbing, the volume top too low). It does NOT pin the top-out velocity carry: W stays held, so air control lands the player anyway;
        // Ladder_TopOutThenRelease_CarriedVelocityLandsOnDeckAndStaysOff pins the carry.
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

            // Leave sideways slowly (a 0.2 stick is 0.48 m/s): look level, W toward +x out of the box (outer face x 2.55) into the water.
            // A side exit starts NO re-entry grace, so steering straight back in (S) must catch the climber again at once. Catches an exit that
            // always starts the grace (finding 5: the climber would be held off for 0.25 s with the centre back inside the box).
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 0f);
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 0.2f };
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 5f, "never left the ladder through the outer face");
            float sideExitTime = Time.time;
            rig.Fake.Frame = new PlayerInputFrame { MoveY = -1f };
            int heldOff = 0;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (!rig.Locomotion.IsClimbing && LadderVolume.FindContaining(SwimInteractGateTests.Center(rig)) != null) heldOff++;
                return rig.Locomotion.IsClimbing;
            }, 2f, "steering straight back in never caught the climber after a side exit");
            float sideGap = Time.time - sideExitTime;
            TestContext.WriteLine($"side-exit re-entry after {sideGap:F3} s, {heldOff} frames inside the box without climbing");
            // One frame is the normal entry latency (the move that brings the centre in happens after that frame's entry check); a 0.25 s grace
            // would show as hundreds of frames in a headless run.
            Assert.That(heldOff, Is.LessThanOrEqualTo(2), "a side exit starts no grace: the centre is never held inside the box without climbing");

            // Now leave for good at full speed.
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 3f, "never left the ladder through the outer face (second time)");
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

        // Catches (finding 3): the climb velocity not carried out of the box. The input is cleared on the first frame the climb is over, so only
        // the carried velocity can move the climber from there: one frame later the flat speed must still be well above zero (a zero carry
        // reads 0), the climber must land on the deck within a second, and the ladder must not catch it again once the grace is over.
        [UnityTest]
        public IEnumerator Ladder_TopOutThenRelease_CarriedVelocityLandsOnDeckAndStaysOff()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            yield return FishingPlayModeDriver.SetPitch(rig, camera, 45f);

            rig.Locomotion.ApplyTeleport(new Vector3(3.0f, -1.2f, LadderZ), -90f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never reached the ladder");
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 4f, "never topped out");
            rig.Fake.Frame = default;   // released on the first frame the climb is over
            float exitTime = Time.time;
            yield return null;

            var flat = new Vector2(rig.Locomotion.Velocity.x, rig.Locomotion.Velocity.z);
            TestContext.WriteLine($"top-out release: flat speed one frame after the exit {flat.magnitude:F2} m/s");
            Assert.That(flat.magnitude, Is.GreaterThan(1f), "the climb velocity carried out of the box (input already cleared)");

            yield return PlayModeHarness.WaitUntil(() => OnDeck(rig), 1f, "never landed on the deck from the carried velocity (at " + rig.Player.transform.position + ")");
            TestContext.WriteLine($"top-out release: on the deck {Time.time - exitTime:F2} s after the exit");

            // Past the grace, then 0.5 s more: the climber must stay off the ladder.
            yield return PlayModeHarness.WaitUntil(() => Time.time - exitTime >= MovementMath.ClimbReentryGrace, 2f, "grace wait");
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                Assert.That(rig.Locomotion.IsClimbing, Is.False, "caught by the ladder again at " + rig.Player.transform.position);
                yield return null;
            }

            rig.Log.AssertEmpty("top-out release");
        }

        // Catches (finding 1, regression for the one-frame IsSwimming gap): a swimmer who climbs into a ladder volume and then steps back out
        // into swim depth. The rod is stowed before the climb, so any ToolChanged afterwards is churn (an Equip on the frame IsSwimming is
        // false, then a Stow on the next). Deviation from the brief ("swim into LadderR"): the bed beside the dock ladders (about -1.28 m) sits
        // right at the swim threshold (surface - 1.2 with the swell), so the test builds its own volume in open deep water (bed ~ -1.6 m).
        [UnityTest]
        public IEnumerator Ladder_SwimmerLeavingTheVolumeAtSwimDepth_CausesNoRodEquipChurn()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            // x 2.1..3.1, y -2..1, z -39.5..-38.5: clear of the dock ladders and of the ramp foot.
            var volumeGo = new GameObject("TestLadderDeepWater");
            volumeGo.transform.position = new Vector3(2.6f, -0.5f, -39.0f);
            BoxCollider box = volumeGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f, 3f, 1f);
            volumeGo.AddComponent<LadderVolume>();

            Action<ToolKind> onTool = null;
            try
            {
                yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { EquipPressed = true });
                rig.Fake.Frame = default;
                yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, 2f, "Equip never set the Rod tool");

                rig.Locomotion.ApplyTeleport(new Vector3(2.6f, -1.45f, -37.8f), 180f);   // swimming, facing the shore (-z)
                yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsSwimming && rig.Player.Tool == ToolKind.None,
                    PlayModeHarness.DefaultTimeoutSeconds, "never swimming with the rod stowed");
                yield return PlayModeHarness.WaitFrames(3, 0.1f);

                int toolEvents = 0;
                bool sawRod = false;
                onTool = t =>
                {
                    toolEvents++;
                    if (t == ToolKind.Rod) sawRod = true;
                };
                rig.Player.ToolChanged += onTool;

                rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
                yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "swimming into the volume never started a climb");
                Assert.That(rig.Locomotion.IsSwimming, Is.False, "a climber is not swimming");
                rig.Fake.Frame = new PlayerInputFrame { MoveY = -1f };   // back out along z, toward the lake
                yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 3f, "never left the volume along z");
                Vector3 exitPos = rig.Player.transform.position;
                Assert.That(rig.Locomotion.IsSwimming, Is.True, "a swimmer leaving the volume at swim depth swims on the same frame (root " + exitPos
                    + ", surface " + WaterSurface.HeightAt(exitPos.x, exitPos.z) + ")");
                rig.Fake.Frame = default;
                yield return PlayModeHarness.WaitFrames(10, 0.3f);

                Assert.That(toolEvents, Is.EqualTo(0), "no NetTool write while climbing in and out of the water");
                Assert.That(sawRod, Is.False, "the rod never pops out");
            }
            finally
            {
                if (onTool != null) rig.Player.ToolChanged -= onTool;
                UnityEngine.Object.Destroy(volumeGo);
            }

            Assert.That(rig.Player.Tool, Is.EqualTo(ToolKind.None));
            rig.Log.AssertEmpty("swim in and out of the ladder volume");
        }

        // Catches (finding 2): ladder entry while movement is locked (RodFighting or any source's LocksMovement): the player would be pulled
        // onto a ladder and hang mid-fight. Entry must wait for the lock to end.
        [UnityTest]
        public IEnumerator Ladder_EntryWhileMovementLocked_WaitsForTheLockToEnd()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            LockSource source = rig.Player.gameObject.AddComponent<LockSource>();
            source.Locks = true;
            rig.Locomotion.enabled = false;   // PlayerLocomotion caches its context sources in OnEnable
            rig.Locomotion.enabled = true;

            rig.Locomotion.ApplyTeleport(new Vector3(2.05f, -0.9f, LadderZ), 90f);   // centre of LadderR's volume at wading depth
            float until = Time.time + 0.3f;
            while (Time.time < until)
            {
                Assert.That(rig.Locomotion.IsClimbing, Is.False, "a locked player must not enter the ladder (at " + rig.Player.transform.position + ")");
                yield return null;
            }

            Assert.That(LadderVolume.FindContaining(SwimInteractGateTests.Center(rig)), Is.Not.Null, "precondition: the capsule centre is inside the volume");

            source.Locks = false;
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 2f, "the climb never started once the lock ended");
            rig.Log.AssertEmpty("ladder entry while locked");
        }

        // Catches (R70, M2-D31): the rod staying out on a ladder, popping back out mid-ladder, or never coming back after the player steps
        // back onto the deck.
        [UnityTest]
        public IEnumerator Ladder_RodOut_StowsOnEntryAndComesBackOnTheDeck()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { EquipPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, 2f, "Equip never set the Rod tool");

            rig.Locomotion.ApplyTeleport(new Vector3(1.2f, 0.65f, LadderZ), 90f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded on the deck");
            Assert.That(rig.Player.Tool, Is.EqualTo(ToolKind.Rod), "precondition: rod out");

            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };   // walk off the edge (pitch 0)
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never stepped onto the ladder");
            rig.Fake.Frame = default;                                // hang at the edge
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.None, 1f, "the rod was not put away on the ladder");
            yield return PlayModeHarness.WaitGameSeconds(0.3f, PlayModeHarness.DefaultTimeoutSeconds, "hang");
            Assert.That(rig.Player.Tool, Is.EqualTo(ToolKind.None), "the rod stays away while climbing");
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Idle));

            rig.Fake.Frame = new PlayerInputFrame { MoveY = -1f };  // S at pitch 0: back toward the deck, out through the inner face
            yield return PlayModeHarness.WaitUntil(() => !rig.Locomotion.IsClimbing, 2f, "never stepped back onto the deck");
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, 1f, "the rod never came back after the ladder");
            Assert.That(rig.Locomotion.IsSwimming, Is.False);
            rig.Log.AssertEmpty("rod on a ladder");
        }

        // Catches: a line left out while its owner climbs (the host cast would stay open), or the cast ending as Escaped instead of a recall
        // (the bait refund only happens on the cancel path).
        [UnityTest]
        public IEnumerator Ladder_LineOut_SteppingOntoTheLadderRecallsTheCast()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;   // no bite during the test
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);   // G9: (0, 0.65, -41) facing +z
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 5f);
            int baitAfterCast = rig.Net.NetSelectedBaitCount.Value;

            // Strafe right (+x) and back (-z) from z -41 so the edge is reached at z ~ -41.6, inside LadderR's z range, passing ~0.5 m
            // clear of the bell at (1.1, 0.6, -40.6).
            rig.Fake.Frame = new PlayerInputFrame { MoveX = 1f, MoveY = -0.4f };
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsClimbing, 3f, "never stepped onto the ladder with the line out");
            rig.Fake.Frame = default;

            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.None, 1f, "the rod was not put away");
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 5f);
            yield return PlayModeHarness.WaitUntil(() => rig.Net.NetSelectedBaitCount.Value == baitAfterCast + 1, 3f,
                "the cast was not recalled with a bait refund (count " + rig.Net.NetSelectedBaitCount.Value + ")");
            rig.Log.AssertEmpty("line out onto a ladder");
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
