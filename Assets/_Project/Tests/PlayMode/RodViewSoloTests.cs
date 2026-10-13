// Authority: test-only. T24: the rod presentation (RodPresenter, LineView, BobberView, SplashFx, CastMarkerView) is observed from outside, because
// this assembly does not reference Presentation (C 2.8 #8): child transforms, renderers, the Line material instance, the LineRenderer points and the
// splash ParticleSystem found by PlayerRigPaths. The scripted cast reuses T19's FishingPlayModeDriver (ruling F-Q7, C 7.19). The view-driven tests
// disable the rod controller and push the owner's view with FishingRodNetwork.OwnerSetView (T23 pattern), so every rule is exercised without a fight's RNG.
// The remote-only rules (arc once per CastId on a remote, the remote pitch from NetPitchQ, the marker never on a remote) need a second peer and are
// left to the reviewer and the MPPM check.
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
    public class RodViewSoloTests
    {
        private const string SplashName = "SplashFx";
        private static readonly int TensionId = Shader.PropertyToID("_Tension");
        private static readonly int DashCountId = Shader.PropertyToID("_DashCount");

        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: segments visible with no rod equipped (the builder leaves them enabled; only RodPresenter can hide them), segments that stay hidden
        // after Equip; the cast marker missing, not following the controller's MarkerPosition, left on after the cast; the bobber not arcing (peak
        // under 0.4 m above the water), not landing on BobberPos, not bobbing at the water height + 0.02; the line missing, not 24 points, not starting
        // at TipVisual or not ending at the bobber; the line or bobber hidden at once on a recall instead of the 0.4 s arc, or left on afterwards.
        [UnityTest]
        public IEnumerator RodViewSolo_CastToWaiting_RodBobberLineAndMarker()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;   // the bite cannot arrive during the checks
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;

            for (int i = 0; i < PlayerRigPaths.RodSegmentCount; i++)
                Assert.That(Renderer(player, PlayerRigPaths.RodSegmentPrefix + i).enabled, Is.False, "Seg" + i + " is hidden until the rod is equipped");
            Assert.That(Renderer(player, PlayerRigPaths.Bobber).enabled, Is.False, "no bobber before a cast");
            Assert.That(Line(player).enabled, Is.False, "no line before a cast");

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return PlayModeHarness.WaitFrames(2, 0f);

            for (int i = 0; i < PlayerRigPaths.RodSegmentCount; i++)
                Assert.That(Renderer(player, PlayerRigPaths.RodSegmentPrefix + i).enabled, Is.True, "Seg" + i + " after Equip");

            // Ready: the valid shape is shown on the water at the controller's marker position.
            MeshRenderer valid = Renderer(player, PlayerRigPaths.CastMarker + "/Valid");
            MeshRenderer invalid = Renderer(player, PlayerRigPaths.CastMarker + "/Invalid");
            Assert.That(valid.enabled, Is.True, "the Valid marker shape in Ready");
            Assert.That(invalid.enabled, Is.False, "the Invalid marker shape is off on valid water");
            Transform marker = player.Find(PlayerRigPaths.CastMarker);
            Assert.That(FishingPlayModeDriver.Flat(marker.position, rig.Controller.MarkerPosition), Is.LessThan(0.05f), "the marker sits at MarkerPosition");
            Assert.That(marker.position.y, Is.EqualTo(WaterSurface.HeightAt(marker.position.x, marker.position.z) + 0.03f).Within(0.05f), "the marker lies on the water");

            // Charge: the marker slides with the controller.
            Vector3 before = rig.Controller.MarkerPosition;
            yield return FishingPlayModeDriver.Charge(rig);
            yield return PlayModeHarness.WaitFrames(1, 0f);
            Assert.That(FishingPlayModeDriver.Flat(rig.Controller.MarkerPosition, before), Is.GreaterThan(0.05f), "the charge moves the marker");
            Assert.That(FishingPlayModeDriver.Flat(marker.position, rig.Controller.MarkerPosition), Is.LessThan(0.1f), "the marker follows the charge");

            yield return FishingPlayModeDriver.Release(rig);
            Transform bobber = player.Find(PlayerRigPaths.Bobber);
            Vector3 landing = rig.Net.ViewState.BobberPos;
            float peak = float.MinValue;
            bool shownInFlight = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 4.0;
            while (rig.Machine.State == RodState.Casting)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("the cast never landed (state " + rig.Machine.State + ")");
                landing = rig.Net.ViewState.BobberPos;
                shownInFlight |= bobber.GetComponent<MeshRenderer>().enabled;
                peak = Mathf.Max(peak, bobber.position.y);
                yield return null;
            }

            Assert.That(shownInFlight, Is.True, "the bobber is shown during the flight");
            float water = WaterSurface.HeightAt(landing.x, landing.z);
            Assert.That(peak, Is.GreaterThanOrEqualTo(water + 0.4f), "the arc peaks at least 0.4 m above the landing water height");

            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 3f);
            yield return PlayModeHarness.WaitFrames(2, 1.0f);   // the arc and the easing have settled

            Vector3 net = rig.Net.NetRodState.Value.BobberPos;
            Assert.That(FishingPlayModeDriver.Flat(bobber.position, net), Is.LessThan(0.25f), "the bobber floats at the landing point");
            Assert.That(bobber.position.y, Is.EqualTo(WaterSurface.HeightAt(net.x, net.z) + 0.02f).Within(0.1f), "the bobber bobs at the water height + 0.02");
            Assert.That(valid.enabled, Is.False, "the marker is off while the line is out (Valid)");
            Assert.That(invalid.enabled, Is.False, "the marker is off while the line is out (Invalid)");

            LineRenderer line = Line(player);
            Transform tipVisual = player.Find(PlayerRigPaths.RodTipVisual);
            Assert.That(line.enabled, Is.True, "the line is drawn while the bobber floats");
            Assert.That(line.positionCount, Is.EqualTo(24), "24 line points");
            Assert.That(Vector3.Distance(line.GetPosition(0), tipVisual.position), Is.LessThan(0.05f), "the line starts at TipVisual");
            Assert.That(Vector3.Distance(line.GetPosition(23), bobber.position), Is.LessThan(0.05f), "the line ends at the bobber");

            // Recall (Secondary): the bobber flies back to the tip in 0.4 s, then everything hides.
            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            rig.Fake.Frame = default;
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 1f);
            // The 0.4 s arc runs on Time.time and the waits on real time, so "still shown" is sampled over the window instead of at one instant:
            // shown on at least one frame after the view saw Idle (a recall that hid at once is never shown on any of them).
            MeshRenderer bobberRenderer = bobber.GetComponent<MeshRenderer>();
            yield return null;   // the LateUpdate of the frame that saw Idle has run
            bool bobberSeen = false, lineSeen = false;
            double recallUntil = Time.realtimeSinceStartupAsDouble + 1.5;
            while (bobberRenderer.enabled && Time.realtimeSinceStartupAsDouble < recallUntil)
            {
                bobberSeen = true;
                lineSeen |= line.enabled;
                yield return null;
            }
            Assert.That(bobberSeen, Is.True, "the recall arc still shows the bobber after Idle");
            Assert.That(lineSeen, Is.True, "and the line");
            yield return PlayModeHarness.WaitUntil(() => !bobberRenderer.enabled, 1.5f, "the bobber never hid after the recall arc");
            Assert.That(line.enabled, Is.False, "the line hides with the bobber");
            Assert.That(Vector3.Distance(bobber.position, tipVisual.position), Is.LessThan(0.6f), "the recall arc ended at the rod tip");

            rig.Log.AssertEmpty("rod view cast to waiting");
        }

        // Catches (BF-3): TipAnchor moved or re-parented by the bend (gameplay reads it); no bend, a constant bend, a bend toward the wrong side, or a
        // bend that vanishes at camera motion 0 (the bend is the no-HUD tension readout); a rest pose that is never restored; the Nibble wobble or the
        // HookSet jerk missing; segment visibility not following NetTool.
        [UnityTest]
        public IEnumerator RodViewSolo_ViewDriven_BendLeavesTipAnchorAloneAndReturnsToRest()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Assert.That(ServiceRoots.App.TryGet(out ISettings settings), Is.True, "ISettings");
            float originalMotion = settings.Current.cameraMotion;
            Transform player = rig.Player.transform;

            try
            {
                yield return DriveView(rig);
                Transform anchor = player.Find(PlayerRigPaths.RodTipAnchor);
                Transform visual = player.Find(PlayerRigPaths.RodTipVisual);
                Transform seg5 = player.Find(PlayerRigPaths.RodSegmentPrefix + 5);
                Vector3 bobber = Bobber(0f, -36f);
                Vector3 restLocal = visual.localPosition;
                Vector3 anchorLocal = anchor.localPosition;
                Quaternion anchorRot = anchor.localRotation;
                Transform parent = anchor.parent;

                for (int i = 0; i < PlayerRigPaths.RodSegmentCount; i++)
                    Assert.That(Renderer(player, PlayerRigPaths.RodSegmentPrefix + i).enabled, Is.True, "Seg" + i + " shown with the rod out");

                SetView(rig, RodState.Hooked, 0, 0.1f, bobber, 3);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.LessThan(1e-3f), "tension 0.1: no bend, TipVisual at its rest pose");
                Assert.That(Quaternion.Angle(seg5.localRotation, Quaternion.identity), Is.LessThan(0.5f), "tension 0.1: Seg5 at rest");

                SetView(rig, RodState.Hooked, 0, 0.5f, bobber, 3);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                float bend05 = Vector3.Distance(visual.localPosition, restLocal);

                SetView(rig, RodState.Hooked, 0, 0.9f, bobber, 3);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                float bend09 = Vector3.Distance(visual.localPosition, restLocal);
                Assert.That(bend05, Is.GreaterThan(0.03f), "tension 0.5 bends the rod");
                Assert.That(bend09, Is.GreaterThan(bend05 + 0.05f), "a harder pull bends it more");
                Assert.That(Quaternion.Angle(seg5.localRotation, Quaternion.identity), Is.GreaterThan(10f), "Seg5 turns toward the line");
                Vector3 toLine = (bobber - anchor.position).normalized;
                Assert.That(Vector3.Dot((visual.position - anchor.position).normalized, toLine), Is.GreaterThan(0.2f), "the tip bends TOWARD the line");

                Assert.That(anchor.parent, Is.SameAs(parent), "TipAnchor keeps its parent");
                Assert.That(Vector3.Distance(anchor.localPosition, anchorLocal), Is.LessThan(1e-6f), "TipAnchor is never moved (BF-3)");
                Assert.That(Quaternion.Angle(anchor.localRotation, anchorRot), Is.LessThan(1e-4f), "TipAnchor is never rotated (BF-3)");

                // Camera motion 0 removes shake, not the bend.
                settings.Current.cameraMotion = 0f;
                settings.NotifyChanged();
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.GreaterThan(bend05 + 0.05f), "the bend survives camera motion 0");
                // ... and with it the shake: once the bend has settled, the pose at tension 0.9 does not move at all. Sampled over 0.15 s of simulated
                // time (frames can be well under a millisecond, so two neighbouring frames differ by less than a shake would show).
                yield return PlayModeHarness.WaitFrames(3, 0.5f);
                Vector3 settledPos = visual.localPosition;
                Quaternion settledRot = seg5.localRotation;
                float maxMove = 0f, maxTurn = 0f;
                float shakeStart = Time.time;
                double shakeUntil = Time.realtimeSinceStartupAsDouble + 3.0;
                while (Time.time - shakeStart < 0.15f && Time.realtimeSinceStartupAsDouble < shakeUntil)
                {
                    yield return null;
                    maxMove = Mathf.Max(maxMove, Vector3.Distance(visual.localPosition, settledPos));
                    maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(seg5.localRotation, settledRot));
                }
                Assert.That(Time.time - shakeStart, Is.GreaterThanOrEqualTo(0.15f), "the shake window elapsed");
                Assert.That(maxMove, Is.LessThan(1e-4f), "camera motion 0: no shake, TipVisual holds its pose");
                Assert.That(maxTurn, Is.LessThan(0.01f), "camera motion 0: no shake, Seg5 holds its pose");

                // Not hooked any more: the rod relaxes to its rest pose.
                SetView(rig, RodState.Waiting, 0, 0.9f, bobber, 3);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.LessThan(1e-3f), "back to rest outside Hooked");
                Assert.That(Vector3.Distance(anchor.localPosition, anchorLocal), Is.LessThan(1e-6f), "TipAnchor still untouched");

                // HookSet: a 15 degree upward jerk easing back over 0.25 s.
                SetView(rig, RodState.Hooked, 0, 0.05f, bobber, 3);
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                rig.Net.RaiseLocalCue(RodCue.HookSet, 0);
                yield return null;
                float jerk = Vector3.Distance(visual.localPosition, restLocal);
                Assert.That(jerk, Is.GreaterThan(0.08f), "the HookSet jerk displaces the tip");
                Assert.That(visual.localPosition.y, Is.GreaterThan(restLocal.y), "the jerk is upward");
                yield return PlayModeHarness.WaitFrames(3, 0.5f);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.LessThan(1e-3f), "the jerk has eased back");

                // Nibble: a short 4 degree wobble.
                SetView(rig, RodState.Waiting, 0, 0f, bobber, 3);
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                float wobble = 0f;
                double until = Time.realtimeSinceStartupAsDouble + 0.25;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    yield return null;
                    wobble = Mathf.Max(wobble, Vector3.Distance(visual.localPosition, restLocal));
                }
                Assert.That(wobble, Is.GreaterThan(0.01f), "the Nibble cue wobbles the rod tip");
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.LessThan(1e-3f), "the wobble has ended");

                // Stowed: the segments follow NetTool.
                rig.Player.OwnerSetTool(ToolKind.None);
                yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.None, 2f, "the tool never changed");
                yield return PlayModeHarness.WaitUntil(() => !Renderer(player, PlayerRigPaths.RodSegmentPrefix + "0").enabled, 1f,
                    "the rod never hid after the stow animation");
                for (int i = 0; i < PlayerRigPaths.RodSegmentCount; i++)
                    Assert.That(Renderer(player, PlayerRigPaths.RodSegmentPrefix + i).enabled, Is.False, "Seg" + i + " hidden when stowed");
                Assert.That(anchor.parent, Is.SameAs(parent), "TipAnchor stays valid while the rod is stowed (never SetActive)");
            }
            finally
            {
                settings.Current.cameraMotion = originalMotion;
                settings.NotifyChanged();
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("rod view bend");
        }

        // Catches: the sag formula (1 - tension) * 0.15 * L, a wrong Bezier control point, the dash count not following the length, the width pulse
        // missing in Warn or present below it, _Tension not pushed, the grace shimmer keyed off the (smoothed) tension instead of FlagGrace (remote
        // tension never reaches 1.0), a tension reading outside Hooked, the line staying after Landed.
        [UnityTest]
        public IEnumerator RodViewSolo_Line_SagDashWidthAndGraceShimmer()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;

            try
            {
                yield return DriveView(rig);
                LineRenderer line = Line(player);
                Vector3 bobber = Bobber(0f, -35f);

                // Sag at two tensions (the points are re-derived here from the drawn end points).
                foreach (float tension in new[] { 0.1f, 0.5f })
                {
                    SetView(rig, RodState.Hooked, 0, tension, bobber, 4);
                    yield return PlayModeHarness.WaitFrames(3, 0.05f);
                    Vector3 p0 = line.GetPosition(0), p2 = line.GetPosition(23);
                    float length = Vector3.Distance(p0, p2);
                    float sag = (1f - tension) * 0.15f * length;
                    Vector3 p1 = (p0 + p2) * 0.5f + Vector3.down * 2f * sag;
                    for (int k = 1; k < 23; k++)
                    {
                        float u = k / 23f;
                        Vector3 expected = (1f - u) * (1f - u) * p0 + 2f * u * (1f - u) * p1 + u * u * p2;
                        Assert.That(Vector3.Distance(line.GetPosition(k), expected), Is.LessThan(0.03f), "tension " + tension + " point " + k);
                    }

                    Material material = line.sharedMaterial;
                    Assert.That(material.GetFloat(TensionId), Is.EqualTo(tension).Within(0.01f), "_Tension follows the tension at " + tension);
                    Assert.That(material.GetFloat(DashCountId), Is.EqualTo(Mathf.Max(1f, length / 0.3f)).Within(1.5f), "dash count follows the length");
                    Assert.That(Vector3.Distance(line.GetPosition(23), bobber), Is.LessThan(0.2f), "the line ends at the bobber");
                }

                // No width pulse below Warn.
                float min = float.MaxValue, max = float.MinValue;
                yield return Sample(() => line.startWidth, 0.5, (v) => { min = Mathf.Min(min, v); max = Mathf.Max(max, v); });
                Assert.That(max - min, Is.LessThan(1e-5f), "the width is constant at tension 0.5");
                float baseWidth = min;
                Assert.That(baseWidth, Is.EqualTo(0.012f).Within(0.002f), "base width");

                // Warn band: the width pulses.
                SetView(rig, RodState.Hooked, 0, 0.9f, bobber, 4);
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                min = float.MaxValue;
                max = float.MinValue;
                yield return Sample(() => line.startWidth, 0.6, (v) => { min = Mathf.Min(min, v); max = Mathf.Max(max, v); });
                Assert.That(max, Is.GreaterThan(baseWidth * 1.3f), "the width pulses up in Warn");
                Assert.That(min, Is.GreaterThanOrEqualTo(baseWidth * 0.99f), "and never below the base");
                Assert.That(line.sharedMaterial.GetFloat(TensionId), Is.LessThan(1f), "Warn alone is not the shimmer");

                // Grace: the flag, not the tension, turns the shimmer on (tension 0.9 here).
                SetView(rig, RodState.Hooked, RodWireQuant.FlagGrace, 0.9f, bobber, 4);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                Assert.That(line.sharedMaterial.GetFloat(TensionId), Is.GreaterThanOrEqualTo(1f), "FlagGrace pushes _Tension >= 1 (shimmer) at a smoothed tension of 0.9");

                // And the tension alone never does (a remote's smoothed tension can sit at 1.0 while the flag is clear).
                SetView(rig, RodState.Hooked, 0, 1f, bobber, 4);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                float t = line.sharedMaterial.GetFloat(TensionId);
                Assert.That(t, Is.LessThan(1f), "tension 1.0 without FlagGrace has no shimmer");
                Assert.That(t, Is.GreaterThan(0.9f), "but is still the warn colour");

                // Tension counts only while Hooked.
                SetView(rig, RodState.Waiting, 0, 0.9f, bobber, 4);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                Assert.That(line.sharedMaterial.GetFloat(TensionId), Is.EqualTo(0f).Within(0.001f), "no tension outside Hooked");
                Assert.That(line.enabled, Is.True, "Waiting still draws the line");

                // Landed: the reveal takes over; bobber and line hide at once.
                SetView(rig, RodState.Landed, 0, 0.2f, bobber, 4);
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                Assert.That(line.enabled, Is.False, "the line is hidden on Landed");
                Assert.That(Renderer(player, PlayerRigPaths.Bobber).enabled, Is.False, "the bobber is hidden on Landed");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("line view");
        }

        // Catches: no snap on spawn (a re-enabled or late-joined view eases in from wherever the bobber was, or replays the arc); an arc that is not
        // triggered by a change into Casting; an arc replayed for a re-observed CastId (a repeated or resent Casting value); an arc that is cut short
        // when the state moves on to Waiting; the bobber ignoring the water height.
        [UnityTest]
        public IEnumerator RodViewSolo_Bobber_SnapsOnEnable_ArcsOncePerCastId()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;

            try
            {
                yield return DriveView(rig);
                Behaviour bobberView = FindBehaviour(player.gameObject, "BobberView");
                Transform bobber = player.Find(PlayerRigPaths.Bobber);
                MeshRenderer bobberRenderer = bobber.GetComponent<MeshRenderer>();
                Vector3 spot = Bobber(0f, -36f);
                float water = WaterSurface.HeightAt(spot.x, spot.z);

                // 1. Late join / respawn: the view is already line-out (a Casting that is still in flight) when the component starts.
                // The bobber is parked far away first, and the CastId 7 was never seen by this component.
                SetView(rig, RodState.Idle, 0, 0f, spot, 6);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                bobberView.enabled = false;
                SetView(rig, RodState.Casting, 0, 0f, spot, 7);
                bobber.position = new Vector3(0f, 20f, 0f);
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                bobberView.enabled = true;
                yield return null;   // the LateUpdate of the frame in which it was enabled has run
                Assert.That(bobberRenderer.enabled, Is.True, "the bobber shows at once for a view that is already line-out");
                Assert.That(FishingPlayModeDriver.Flat(bobber.position, spot), Is.LessThan(0.05f), "snapped to the landing spot in one frame (no easing)");
                Assert.That(bobber.position.y, Is.EqualTo(WaterSurface.HeightAt(spot.x, spot.z) + 0.02f).Within(0.03f), "snapped to the water height + 0.02");
                float peak = float.MinValue;
                yield return Sample(() => bobber.position.y, 0.8, (v) => peak = Mathf.Max(peak, v));
                Assert.That(peak, Is.LessThan(water + 0.15f), "no arc is replayed for a view that was already in flight (F6)");

                // 2. A change into Casting with a new CastId arcs.
                SetView(rig, RodState.Waiting, 0, 0f, spot, 7);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                SetView(rig, RodState.Casting, 0, 0f, spot, 8);
                peak = float.MinValue;
                yield return Sample(() => bobber.position.y, 1.2, (v) => peak = Mathf.Max(peak, v));
                Assert.That(peak, Is.GreaterThanOrEqualTo(water + 0.4f), "a new cast arcs at least 0.4 m above the water");

                // 3. The same CastId observed again after leaving Casting does not arc.
                SetView(rig, RodState.Waiting, 0, 0f, spot, 8);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                SetView(rig, RodState.Casting, 0, 0f, spot, 8);
                peak = float.MinValue;
                yield return Sample(() => bobber.position.y, 1.0, (v) => peak = Mathf.Max(peak, v));
                Assert.That(peak, Is.LessThan(water + 0.15f), "a re-observed CastId never restarts the arc");

                // 4. The arc runs to completion although the state moves on to Waiting at once (Casting must be left first: a change INTO Casting).
                SetView(rig, RodState.Waiting, 0, 0f, spot, 9);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                SetView(rig, RodState.Casting, 0, 0f, spot, 10);
                float castTime = Time.time;   // the view runs on Time.time, the waits on real time: the bound below is derived from the former
                yield return null;
                float startGap = FishingPlayModeDriver.Flat(bobber.position, spot);   // the arc has just started at the rod tip
                SetView(rig, RodState.Waiting, 0, 0f, spot, 10);
                // Mid-flight: the bobber is still well short of the spot. An arc that was cut short would have eased onto the spot within 0.2 s
                // (about 5 % of the gap left after 0.15 s), and its peak alone would not show it because the arc starts at the rod tip, high above
                // the water. The horizontal travel is linear in the elapsed fraction of the flight, so the bound follows the simulated time that
                // has really passed (a hitch in real time only lowers it); half of the expected remainder keeps clear of the wobble.
                yield return PlayModeHarness.WaitFrames(1, 0.15f);
                float elapsed = Time.time - castTime;
                float expectedLeft = startGap * (1f - elapsed / CastMath.FlightTime(startGap));
                Assert.That(startGap, Is.GreaterThan(1.5f), "the test spot is far enough from the rod for the bound to mean something (gap " + startGap.ToString("0.00") + " m)");
                // Two hitches in a row can eat most of the flight before this sample; then the bound means nothing, so the mid-flight check is skipped
                // (the peak and the landing checks below still run).
                if (elapsed > 0.8f * CastMath.FlightTime(startGap))
                    TestContext.WriteLine("FG ARC mid-flight check skipped: elapsed " + elapsed.ToString("0.000") + " s of " + CastMath.FlightTime(startGap).ToString("0.000") + " s flight (hitch)");
                else
                    Assert.That(FishingPlayModeDriver.Flat(bobber.position, spot), Is.GreaterThan(Mathf.Max(0.1f, 0.5f * expectedLeft)),
                        "mid-flight (" + elapsed.ToString("0.000") + " s of simulated time) the bobber is still on its way");
                peak = float.MinValue;
                yield return Sample(() => bobber.position.y, 1.2, (v) => peak = Mathf.Max(peak, v));
                Assert.That(peak, Is.GreaterThanOrEqualTo(water + 0.4f), "the arc is not cut short when the state moves on");
                Assert.That(FishingPlayModeDriver.Flat(bobber.position, spot), Is.LessThan(0.1f), "and it ends at the landing spot");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("bobber snap and arc");
        }

        // Catches: the bobber steady positions (Bite plunge, Hooked fish point, dive depth, exhausted float), the nibble dip, and the hide rules:
        // Snapped hides the bobber at once and whips the line for 0.2 s, a recall flies the bobber back instead of hiding it.
        [UnityTest]
        public IEnumerator RodViewSolo_Bobber_StatePositionsSnapWhipAndRecall()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;

            try
            {
                yield return DriveView(rig);
                Transform bobber = player.Find(PlayerRigPaths.Bobber);
                MeshRenderer bobberRenderer = bobber.GetComponent<MeshRenderer>();
                LineRenderer line = Line(player);
                Transform tipVisual = player.Find(PlayerRigPaths.RodTipVisual);
                Vector3 spot = Bobber(0f, -36f);

                SetView(rig, RodState.Waiting, 0, 0f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                float h = WaterSurface.HeightAt(spot.x, spot.z);
                Assert.That(bobber.position.y, Is.EqualTo(h + 0.02f).Within(0.04f), "Waiting: float at the surface");

                SetView(rig, RodState.Bite, 0, 0f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                Assert.That(bobber.position.y, Is.EqualTo(h - 0.25f).Within(0.05f), "Bite: plunged to h - 0.25");

                SetView(rig, RodState.Hooked, 0, 0.5f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                Assert.That(bobber.position.y, Is.EqualTo(h - 0.1f).Within(0.05f), "Hooked: the fish point at h - 0.1");

                SetView(rig, RodState.Hooked, RodWireQuant.FlagDive, 0.5f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                Assert.That(bobber.position.y, Is.EqualTo(h - 0.35f).Within(0.05f), "Hooked + dive: h - 0.35");

                SetView(rig, RodState.Hooked, RodWireQuant.FlagExhausted, 0.2f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                Assert.That(bobber.position.y, Is.EqualTo(h + 0.03f).Within(0.05f), "Hooked + exhausted: rolling on the surface at h + 0.03");

                SetView(rig, RodState.Waiting, 0, 0f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);
                float lowest = float.MaxValue;
                rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                // Measured against the live water height (the waves move while the dip plays).
                yield return Sample(() => bobber.position.y - (WaterSurface.HeightAt(spot.x, spot.z) + 0.02f), 0.4, (v) => lowest = Mathf.Min(lowest, v));
                Assert.That(lowest, Is.LessThan(-0.02f), "the Nibble cue dips the bobber about 4 cm");
                Assert.That(bobber.position.y, Is.EqualTo(h + 0.02f).Within(0.04f), "and it comes back up");

                // Snapped: the bobber hides at once; the line whips toward the tip for 0.2 s, then hides.
                SetView(rig, RodState.Hooked, 0, 0.95f, spot, 11);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                Assert.That(line.enabled, Is.True);
                float startGap = Vector3.Distance(line.GetPosition(23), tipVisual.position);
                SetView(rig, RodState.Snapped, 0, 1f, spot, 11);
                rig.Net.RaiseLocalCue(RodCue.Snapped, 1);
                yield return null;   // the LateUpdate of the frame that saw Snapped has run
                Assert.That(bobberRenderer.enabled, Is.False, "Snapped hides the bobber at once");
                // The 0.2 s whip runs on Time.time and the waits on real time: sampled over the window, the line must be drawn on at least one frame
                // and its end must come well toward the tip on at least one drawn frame.
                bool whipDrawn = false;
                float closest = float.MaxValue;
                double whipUntil = Time.realtimeSinceStartupAsDouble + 1.0;
                while (line.enabled && Time.realtimeSinceStartupAsDouble < whipUntil)
                {
                    whipDrawn = true;
                    closest = Mathf.Min(closest, Vector3.Distance(line.GetPosition(23), tipVisual.position));
                    yield return null;
                }
                Assert.That(whipDrawn, Is.True, "the line is still drawn for the whip");
                Assert.That(closest, Is.LessThan(startGap * 0.7f), "the whip pulls the line end toward the tip");
                yield return PlayModeHarness.WaitUntil(() => !line.enabled, 1.0f, "the line never hid after the whip");

                // Recall: back to the tip along an arc, then hide.
                SetView(rig, RodState.Idle, 0, 0f, spot, 0);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                SetView(rig, RodState.Waiting, 0, 0f, spot, 12);
                yield return PlayModeHarness.WaitFrames(3, 0.4f);   // a change from Idle into Waiting (not Casting): shown at the steady spot, no arc
                Assert.That(bobberRenderer.enabled, Is.True);
                float startDistance = Vector3.Distance(bobber.position, tipVisual.position);
                SetView(rig, RodState.Idle, 0, 0f, spot, 0);
                rig.Net.RaiseLocalCue(RodCue.Recall, 0);
                yield return null;   // the LateUpdate of the frame that saw the recall has run
                // The 0.4 s recall runs on Time.time and the waits on real time: sampled over the window, the bobber is shown on at least one frame
                // and, on a frame where it is shown, it has come well back toward the tip.
                bool recallShown = false;
                float nearest = float.MaxValue;
                double recallUntil = Time.realtimeSinceStartupAsDouble + 1.0;
                while (bobberRenderer.enabled && Time.realtimeSinceStartupAsDouble < recallUntil)
                {
                    recallShown = true;
                    nearest = Mathf.Min(nearest, Vector3.Distance(bobber.position, tipVisual.position));
                    yield return null;
                }
                Assert.That(recallShown, Is.True, "mid-recall the bobber is still shown");
                Assert.That(nearest, Is.LessThan(startDistance * 0.8f), "and flying back to the tip");
                yield return PlayModeHarness.WaitUntil(() => !bobberRenderer.enabled, 1.0f, "the bobber never hid after the recall");
                Assert.That(line.enabled, Is.False, "the line hides with the bobber");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("bobber states");
        }

        // Catches: no splash for a cue; the side-run column emitted from a cue instead of polling, or also during a dive (spec 9.11: a dive has no
        // sideways splash), or not emitted at all; no slow bubbles for an exhausted fish; bubbles on a recall-escape; a missing particle material.
        [UnityTest]
        public IEnumerator RodViewSolo_Splash_CuesAndPolledSideRun()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;

            try
            {
                yield return DriveView(rig);
                Transform splash = player.Find(SplashName);
                Assert.That(splash, Is.Not.Null, "PlayerFP has a '" + SplashName + "' child (SplashFx missing)");
                ParticleSystem ps = splash.GetComponent<ParticleSystem>();
                Assert.That(ps, Is.Not.Null, "SplashFx has a ParticleSystem (particle material missing?)");
                Assert.That(splash.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null, "particle material");
                Vector3 spot = Bobber(0f, -36f);

                SetView(rig, RodState.Waiting, 0, 0f, spot, 13);
                yield return PlayModeHarness.WaitFrames(3, 0.2f);
                ps.Clear();
                rig.Net.RaiseLocalCue(RodCue.Landing, 0);
                yield return null;
                Assert.That(ps.particleCount, Is.GreaterThanOrEqualTo(10), "Landing: about 12 droplets");

                ps.Clear();
                rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                yield return null;
                Assert.That(ps.particleCount, Is.GreaterThanOrEqualTo(30), "Bite: a big splash (30 + 10)");

                ps.Clear();
                rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                yield return null;
                Assert.That(ps.particleCount, Is.InRange(3, 8), "Nibble: 4 small droplets");

                // A plain Hooked state emits nothing by polling.
                SetView(rig, RodState.Hooked, 0, 0.5f, spot, 13);
                yield return PlayModeHarness.WaitFrames(2, 0.1f);
                ps.Clear();
                yield return PlayModeHarness.WaitFrames(5, 0.4f);
                Assert.That(ps.particleCount, Is.EqualTo(0), "no splashes while the fish simply pulls");

                // Side run: polled, 20 per second, while Hooked + FlagBurst without FlagDive; the lateral swings.
                ps.Clear();
                int maxSide = 0;
                double until = Time.realtimeSinceStartupAsDouble + 0.6;
                float lateral = 0f;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    lateral += 0.05f;
                    rig.Net.OwnerSetView(new RodViewState { State = RodState.Hooked, Flags = RodWireQuant.FlagBurst, Lateral = lateral, Tension = 0.5f, BobberPos = spot, CastId = 13, IsOwner = true, RodOut = true });
                    yield return null;
                    maxSide = Mathf.Max(maxSide, ps.particleCount);
                }
                Assert.That(maxSide, Is.GreaterThanOrEqualTo(5), "the side-run column is emitted by polling");

                // Dive: Burst + Dive, no sideways splash.
                SetView(rig, RodState.Hooked, 0, 0.5f, spot, 13);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                ps.Clear();
                int maxDive = 0;
                until = Time.realtimeSinceStartupAsDouble + 0.6;
                lateral = 0f;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    lateral += 0.05f;
                    rig.Net.OwnerSetView(new RodViewState { State = RodState.Hooked, Flags = RodWireQuant.FlagBurst | RodWireQuant.FlagDive, Lateral = lateral, Tension = 0.5f, BobberPos = spot, CastId = 13, IsOwner = true, RodOut = true });
                    yield return null;
                    maxDive = Mathf.Max(maxDive, ps.particleCount);
                }
                Assert.That(maxDive, Is.EqualTo(0), "a dive has no sideways splash");

                // Exhausted: 3 particles every 0.6 s.
                SetView(rig, RodState.Hooked, 0, 0.2f, spot, 13);
                yield return PlayModeHarness.WaitFrames(3, 0.1f);
                ps.Clear();
                int maxExhausted = 0;
                SetView(rig, RodState.Hooked, RodWireQuant.FlagExhausted, 0.2f, spot, 13);
                yield return Sample(() => ps.particleCount, 1.4, (v) => maxExhausted = Mathf.Max(maxExhausted, (int)v));
                Assert.That(maxExhausted, Is.GreaterThanOrEqualTo(3), "an exhausted fish rolls: a few particles every 0.6 s");

                // Escaped: bubbles, unless it is the recall's own Escaped.
                SetView(rig, RodState.Idle, 0, 0f, spot, 0);
                yield return PlayModeHarness.WaitFrames(3, 0.3f);
                SetView(rig, RodState.Escaped, RodWireQuant.FlagRecall, 0f, spot, 13);
                ClearAll(splash);
                rig.Net.RaiseLocalCue(RodCue.Escaped, 0);
                yield return null;
                Assert.That(TotalParticles(splash), Is.EqualTo(0), "a recall is not an escape: no bubbles");
                SetView(rig, RodState.Escaped, 0, 0f, spot, 13);
                rig.Net.RaiseLocalCue(RodCue.Escaped, 0);
                yield return null;
                Assert.That(TotalParticles(splash), Is.GreaterThanOrEqualTo(8), "Escaped: about 10 slow bubbles (a child system with negative gravity)");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("splash fx");
        }

        // Catches: the Invalid shape not flashing on InvalidRelease (or flashing for good); the marker left on without a controller marker.
        [UnityTest]
        public IEnumerator RodViewSolo_CastMarker_FlashesOnInvalidReleaseThenStops()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;
            MeshRenderer invalid = Renderer(player, PlayerRigPaths.CastMarker + "/Invalid");
            MeshRenderer valid = Renderer(player, PlayerRigPaths.CastMarker + "/Valid");

            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(valid.enabled, Is.False, "no marker at Idle");
            Assert.That(invalid.enabled, Is.False, "no marker at Idle");

            rig.Net.RaiseLocalCue(RodCue.InvalidRelease, 0);
            bool on = false, off = false;
            double until = Time.realtimeSinceStartupAsDouble + 0.4;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
                if (invalid.enabled) on = true; else off = true;
            }
            Assert.That(on, Is.True, "the Invalid shape flashes on");
            Assert.That(off, Is.True, "and off (0.075 s steps)");
            yield return PlayModeHarness.WaitFrames(3, 0.4f);
            Assert.That(invalid.enabled, Is.False, "the flash ends after 0.45 s");
            Assert.That(valid.enabled, Is.False, "and the Valid shape never appears for it");

            rig.Log.AssertEmpty("marker flash");
        }

        // Catches (R70 stow animation): the rod hiding at once on a stow or appearing straight at its rest pose on an equip (no animation),
        // the animation moving TipAnchor (BF-3: the gameplay tip, reach tests and host slack read it), and the rod not settling back exactly.
        [UnityTest]
        public IEnumerator RodViewSolo_StowAndEquip_AnimateThenSettle()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Transform player = rig.Player.transform;
            try
            {
                yield return DriveView(rig);   // rod out, settled at rest
                Transform anchor = player.Find(PlayerRigPaths.RodTipAnchor);
                Transform visual = player.Find(PlayerRigPaths.RodTipVisual);
                MeshRenderer seg0 = Renderer(player, PlayerRigPaths.RodSegmentPrefix + "0");
                Vector3 restLocal = visual.localPosition;
                Vector3 anchorLocal = anchor.localPosition;
                Quaternion anchorRot = anchor.localRotation;

                rig.Player.OwnerSetTool(ToolKind.None);
                float t0 = Time.time, maxMove = 0f;
                bool visibleWhileMoving = false;
                while (Time.time - t0 < ToolStowAnim.Seconds * 0.9f)
                {
                    yield return null;
                    float move = Vector3.Distance(visual.localPosition, restLocal);
                    if (seg0.enabled && move > 0.01f) visibleWhileMoving = true;
                    maxMove = Mathf.Max(maxMove, move);
                }

                Assert.That(visibleWhileMoving, Is.True, "the rod is still visible while it swings away (max move " + maxMove + ")");
                yield return PlayModeHarness.WaitUntil(() => !seg0.enabled, 1f, "the rod never hid after the stow animation");
                Assert.That(Vector3.Distance(anchor.localPosition, anchorLocal), Is.LessThan(1e-6f), "TipAnchor never moves (BF-3)");
                Assert.That(Quaternion.Angle(anchor.localRotation, anchorRot), Is.LessThan(1e-4f), "TipAnchor never rotates (BF-3)");

                rig.Player.OwnerSetTool(ToolKind.Rod);
                t0 = Time.time;
                bool sawRising = false;
                while (Time.time - t0 < ToolStowAnim.Seconds * 0.9f)
                {
                    yield return null;
                    if (seg0.enabled && Vector3.Distance(visual.localPosition, restLocal) > 0.01f) sawRising = true;
                }

                Assert.That(sawRising, Is.True, "the equip shows the rod at once and swings it up into place");
                yield return PlayModeHarness.WaitFrames(3, ToolStowAnim.Seconds + 0.1f);
                Assert.That(seg0.enabled, Is.True);
                Assert.That(Vector3.Distance(visual.localPosition, restLocal), Is.LessThan(1e-3f), "settled exactly at the rest pose");
                Assert.That(Vector3.Distance(anchor.localPosition, anchorLocal), Is.LessThan(1e-6f), "TipAnchor never moves (BF-3)");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("rod stow animation");
        }

        // ------------------------------------------------------------------ helpers

        // Disables the rod controller (so nothing overwrites the pushed view), equips the rod through NetTool and waits for the pose.
        private static IEnumerator DriveView(FishingRig rig)
        {
            rig.Controller.enabled = false;
            yield return PlayModeHarness.WaitFrames(2, 0.05f);
            rig.Log.AssertEmpty("disabling the rod controller at Idle");
            rig.Player.OwnerSetTool(ToolKind.Rod);
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, 2f, "the Rod tool never applied");
            SetView(rig, RodState.Idle, 0, 0f, Bobber(0f, -36f), 0);
            yield return PlayModeHarness.WaitFrames(3, ToolStowAnim.Seconds + 0.1f);   // the equip animation has settled
        }

        private static void SetView(FishingRig rig, RodState state, byte flags, float tension, Vector3 bobber, uint castId)
        {
            rig.Net.OwnerSetView(new RodViewState { State = state, Flags = flags, Tension = tension, BobberPos = bobber, CastId = castId, IsOwner = true, RodOut = true });
        }

        // A point on the lake 5 m past the G9 deck end (the player stands at z -41 facing +z).
        private static Vector3 Bobber(float x, float z)
        {
            return new Vector3(x, WaterSurface.HeightAt(x, z), z);
        }

        private static MeshRenderer Renderer(Transform root, string path)
        {
            Transform t = root.Find(path);
            Assert.That(t, Is.Not.Null, "PlayerFP has no '" + path + "'");
            MeshRenderer r = t.GetComponent<MeshRenderer>();
            Assert.That(r, Is.Not.Null, path + " has no MeshRenderer");
            return r;
        }

        private static LineRenderer Line(Transform root)
        {
            Transform t = root.Find(PlayerRigPaths.Line);
            Assert.That(t, Is.Not.Null, "PlayerFP has no Line");
            return t.GetComponent<LineRenderer>();
        }

        private static int TotalParticles(Transform splash)
        {
            int n = 0;
            foreach (ParticleSystem system in splash.GetComponentsInChildren<ParticleSystem>()) n += system.particleCount;
            return n;
        }

        private static void ClearAll(Transform splash)
        {
            foreach (ParticleSystem system in splash.GetComponentsInChildren<ParticleSystem>()) system.Clear();
        }

        // Calls sink with the value once per frame for the given real-time seconds.
        private static IEnumerator Sample(Func<float> value, double seconds, Action<float> sink)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
                sink(value());
            }
        }

        private static Behaviour FindBehaviour(GameObject root, string typeName)
        {
            foreach (Behaviour b in root.GetComponents<Behaviour>())
            {
                if (b != null && b.GetType().Name == typeName) return b;
            }
            Assert.Fail("PlayerFP has no " + typeName);
            return null;
        }
    }
}
