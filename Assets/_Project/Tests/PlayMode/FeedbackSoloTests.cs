// Authority: test-only. T23: the presentation feedback (FishingFeedback, CatchRevealView) is observed from outside, because this assembly does not
// reference Presentation (C 2.8 #8): shader globals (_Ripples), the EventBus (CaptionRequested), the fake input's last rumble, and the
// "CatchReveal" child's active state, mesh, materials and pose, and the AudioBus rate-limit table (which records every accepted play). The scripted
// cast reuses T19's FishingPlayModeDriver (ruling F-Q7). The T23-review tests drive the owner's view directly (FishingRodNetwork.OwnerSetView) with
// the rod controller disabled, so the result does not depend on a fight's RNG.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
    public class FeedbackSoloTests
    {
        private const string RevealName = "CatchReveal";

        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: no cast-landing ripple at the landing point (or one stamped elsewhere); no cap.bite caption at the bite (or never on the
        // owner); no catch reveal after the announcement, or one without a mesh, a material per submesh, or that never hides; rumble left on at
        // Idle; a warning anywhere in the loop.
        [UnityTest]
        public IEnumerator FeedbackSolo_CastBiteLand_RippleCaptionAndReveal()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "EventBus");
            Assert.That(ServiceRoots.App.TryGet(out ISettings settings), Is.True, "ISettings");
            Assert.That(settings.Current.captions, Is.True, "captions default on");

            var captions = new List<string>();
            Vector3 landing = Vector3.zero;
            bool haveLanding = false;
            rig.Net.CastResultReceived += (castId, valid, reason, point, roll) =>
            {
                if (!valid) return;
                landing = point;
                haveLanding = true;
            };

            using (bus.Subscribe<CaptionRequested>(c => captions.Add(c.Key)))
            {
                yield return FishingPlayModeDriver.EquipAndReady(rig);
                yield return FishingPlayModeDriver.ChargeAndRelease(rig);

                // (a) the cast-landing ripple, polled every frame until the fish is hooked (later ripples must not hide the first one).
                double start = Time.realtimeSinceStartupAsDouble;
                double seenWaiting = -1, seenRipple = -1;
                while (rig.Machine.State != RodState.Hooked)
                {
                    if (Time.realtimeSinceStartupAsDouble - start > 8.0) Assert.Fail("never hooked (state " + rig.Machine.State + ")");
                    if (seenWaiting < 0 && rig.Machine.State == RodState.Waiting) seenWaiting = Time.realtimeSinceStartupAsDouble;
                    if (seenRipple < 0 && haveLanding && HasRipple(RippleMath.CastLanding, landing, 0.5f)) seenRipple = Time.realtimeSinceStartupAsDouble;
                    yield return null;
                }
                if (seenRipple < 0 && haveLanding && HasRipple(RippleMath.CastLanding, landing, 0.5f)) seenRipple = Time.realtimeSinceStartupAsDouble;

                Assert.That(haveLanding, Is.True, "the host's cast result (landing point) never arrived");
                Assert.That(seenRipple, Is.GreaterThanOrEqualTo(0.0), "no _Ripples slot with strength CastLanding within 0.5 m of the landing " + landing);
                if (seenWaiting >= 0) Assert.That(seenRipple - seenWaiting, Is.LessThanOrEqualTo(1.5), "the landing ripple appeared too late");

                // (b) the bite caption (owner, settings.captions).
                Assert.That(captions, Does.Contain("cap.bite"), "CaptionRequested(cap.bite) at the bite");

                yield return FishingPlayModeDriver.FightRhythm(rig, 100f);
                Assert.That(rig.Machine.State, Is.EqualTo(RodState.Landed), "the scripted fight must land the fish");

                // (c) the reveal: active within 0.5 s of the announcement, with a mesh and one material per submesh, hidden again within 2.5 s.
                yield return PlayModeHarness.WaitUntil(() => rig.Announced.Count >= 1, 3f, "the catch was never announced");
                double announced = Time.realtimeSinceStartupAsDouble;
                Transform reveal = rig.Player.transform.Find(RevealName);
                Assert.That(reveal, Is.Not.Null, "PlayerFP has no '" + RevealName + "' child (CatchRevealView missing)");
                yield return PlayModeHarness.WaitUntil(() => reveal.gameObject.activeSelf, 0.5f, "the catch reveal did not show within 0.5 s of the announcement");
                AssertRevealRenders(reveal);


                // The terminal pulse (0.3 s at most) has ended and FishingFeedback itself zeroed the motors, well before the controller's Idle zero.
                yield return PlayModeHarness.WaitFrames(2, 0.6f - (float)(Time.realtimeSinceStartupAsDouble - announced));
                Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low after the terminal pulse");
                Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high after the terminal pulse");

                yield return PlayModeHarness.WaitUntil(() => !reveal.gameObject.activeSelf, 2.5f, "the catch reveal never hid");
                Assert.That(Time.realtimeSinceStartupAsDouble - announced, Is.LessThan(3.0));
            }

            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 5f);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);

            // (d) rumble is off at Idle.
            Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low at Idle");
            Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high at Idle");

            // No loop outlives the fight (AudioBus loops are children named Loop_<id>; a stopped loop's object is gone by the end of the frame).
            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Assert.That(HasLoop(audio), Is.False, "no Loop_ source may remain once the rod is back at Idle");

            rig.Log.AssertEmpty("feedback cast-bite-land loop");
        }

        // Catches: captions shown with settings.captions off; a cue still acting after the component is disabled (OnDisable must unsubscribe);
        // and, through the positive controls, that the silence is real and not a harness that cannot see captions or ripples.
        [UnityTest]
        public IEnumerator FeedbackSolo_CaptionsOffAndDisabled_StaySilent()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "EventBus");
            Assert.That(ServiceRoots.App.TryGet(out ISettings settings), Is.True, "ISettings");
            Behaviour feedback = FindBehaviour(rig.Player.gameObject, "FishingFeedback");

            var captions = new List<string>();
            using (bus.Subscribe<CaptionRequested>(c => captions.Add(c.Key)))
            {
                try
                {
                    // Positive control: enabled, captions on.
                    int nibbles = CountRipples(RippleMath.Nibble);
                    rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                    rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                    yield return null;
                    Assert.That(captions, Is.EqualTo(new[] { "cap.bite" }), "positive control: the owner's Bite cue raises cap.bite");
                    Assert.That(CountRipples(RippleMath.Nibble), Is.EqualTo(nibbles + 1), "positive control: the Nibble cue spawns a ripple");

                    // Captions off: the ripple still spawns, the caption does not.
                    captions.Clear();
                    settings.Current.captions = false;
                    settings.NotifyChanged();
                    rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                    rig.Net.RaiseLocalCue(RodCue.Snapped, 1);
                    yield return null;
                    Assert.That(captions, Is.Empty, "no caption may be requested while settings.captions is off");

                    // Disabled: nothing at all (captions on again, so only the disable can explain the silence).
                    settings.Current.captions = true;
                    settings.NotifyChanged();
                    feedback.enabled = false;
                    nibbles = CountRipples(RippleMath.Nibble);
                    int bites = CountRipples(RippleMath.Bite);
                    rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                    rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                    rig.Net.RaiseLocalCue(RodCue.Snapped, 2);
                    yield return PlayModeHarness.WaitFrames(2, 0.05f);
                    Assert.That(captions, Is.Empty, "a disabled FishingFeedback must not request captions");
                    Assert.That(CountRipples(RippleMath.Nibble), Is.EqualTo(nibbles), "a disabled FishingFeedback must not spawn ripples");
                    Assert.That(CountRipples(RippleMath.Bite), Is.EqualTo(bites), "a disabled FishingFeedback must not spawn ripples");
                    Assert.That(rig.Player.transform.Find("SnapSilhouette").gameObject.activeSelf, Is.False, "no snap silhouette while disabled");
                    Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f));
                    Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f));
                }
                finally
                {
                    settings.Current.captions = true;
                    settings.NotifyChanged();
                }
            }

            rig.Log.AssertEmpty("feedback captions and disable");
        }

        // Catches (T23 carry-forward and review): a loop started without a follow transform, or following the wrong one (Line_Creak must follow the rod
        // tip, Dive_Thrum the feedback anchor at the bobber, which is 5 m apart here); loops not stopped when the component is disabled or when the rod
        // leaves Hooked (the view keeps tension 0.7 and the dive/reel flags, so only the Hooked gate can silence them); loops not restarting from
        // polling when the component is enabled again mid-fight (F6). Deterministic: the controller is disabled, so nothing overwrites the view.
        [UnityTest]
        public IEnumerator FeedbackSolo_Loops_FollowStopWhenNotHookedAndRestartOnEnable()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Behaviour feedback = FindBehaviour(rig.Player.gameObject, "FishingFeedback");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Transform anchor = rig.Player.transform.Find("FeedbackAnchor");
            Assert.That(tip, Is.Not.Null, "RodTipAnchor");
            Assert.That(anchor, Is.Not.Null, "FishingFeedback creates a FeedbackAnchor child");

            try
            {
                rig.Controller.enabled = false;   // PublishView would overwrite the view every frame
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                rig.Log.AssertEmpty("disabling the rod controller at Idle");
                Assert.That(HasLoop(audio), Is.False, "no loop before the fight");

                Vector3 bobber = tip.position + new Vector3(0f, -1f, 5f);
                const byte diveAndReel = RodWireQuant.FlagDive | RodWireQuant.FlagReel;
                SetView(rig, RodState.Hooked, diveAndReel, 0.7f, bobber);

                Transform creak = null, thrum = null;
                yield return PlayModeHarness.WaitUntil(
                    () => (creak = FindLoop(audio, "Loop_Line_Creak")) != null && (thrum = FindLoop(audio, "Loop_Dive_Thrum")) != null,
                    2f, "Hooked at tension 0.7 with a dive must start both Line_Creak and Dive_Thrum");
                yield return PlayModeHarness.WaitFrames(3, 0.05f);   // AudioBus.LateUpdate moves a loop to its follow target; the order against ours is undefined

                Assert.That(Vector3.Distance(anchor.position, bobber), Is.LessThan(0.05f), "the feedback anchor sits at the bobber");
                Assert.That(Vector3.Distance(tip.position, anchor.position), Is.GreaterThan(3f), "the tip and the anchor must be far apart for this check to mean anything");
                Assert.That(Vector3.Distance(creak.position, tip.position), Is.LessThan(0.25f), "Line_Creak follows the rod tip");
                Assert.That(Vector3.Distance(thrum.position, anchor.position), Is.LessThan(0.25f), "Dive_Thrum follows the feedback anchor");

                feedback.enabled = false;
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                Assert.That(HasLoop(audio), Is.False, "OnDisable must stop every loop");
                Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low after disable");
                Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high after disable");

                feedback.enabled = true;
                yield return PlayModeHarness.WaitUntil(
                    () => FindLoop(audio, "Loop_Line_Creak") != null && FindLoop(audio, "Loop_Dive_Thrum") != null,
                    2f, "enabling the component mid-fight must restart both loops from the polled view (F6)");

                SetView(rig, RodState.Escaped, diveAndReel, 0.7f, bobber);   // not Hooked any more: same tension and flags
                yield return PlayModeHarness.WaitFrames(2, 0f);
                Assert.That(HasLoop(audio), Is.False, "no Loop_ child may remain within 2 frames of leaving Hooked");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("feedback loops");
        }

        // Catches (R72): the creak gated at the old 0.6 band instead of Rust's 10%, an instant stop when the tension falls (it fades over 0.25 s while
        // still hooked), a fade that never ends, or a creak that survives leaving Hooked. Deterministic: the controller is disabled and the test drives the view.
        [UnityTest]
        public IEnumerator FeedbackSolo_Creak_StartsAboveTenPercentAndFadesOut()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Assert.That(tip, Is.Not.Null, "RodTipAnchor");

            try
            {
                rig.Controller.enabled = false;
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                Vector3 bobber = tip.position + new Vector3(0f, -1f, 5f);

                SetView(rig, RodState.Hooked, 0, 0.05f, bobber);
                yield return PlayModeHarness.WaitFrames(3, 0.3f);
                Assert.That(FindLoop(audio, "Loop_Line_Creak"), Is.Null, "tension 0.05 is below the 10% start");

                SetView(rig, RodState.Hooked, 0, 0.2f, bobber);
                yield return PlayModeHarness.WaitUntil(() => FindLoop(audio, "Loop_Line_Creak") != null, 2f, "tension 0.2 must start the creak (Rust: from 0.10)");

                SetView(rig, RodState.Hooked, 0, 0.05f, bobber);   // still Hooked: fades instead of stopping dead
                yield return null;
                Assert.That(FindLoop(audio, "Loop_Line_Creak"), Is.Not.Null, "the creak is still there on the next frame (0.25 s fade)");
                yield return PlayModeHarness.WaitUntil(() => FindLoop(audio, "Loop_Line_Creak") == null, 0.6f, "the creak must be gone after the fade");

                SetView(rig, RodState.Hooked, 0, 0.2f, bobber);
                yield return PlayModeHarness.WaitUntil(() => FindLoop(audio, "Loop_Line_Creak") != null, 2f, "the creak restarts");
                SetView(rig, RodState.Escaped, 0, 0.2f, bobber);
                yield return PlayModeHarness.WaitFrames(2, 0f);
                Assert.That(HasLoop(audio), Is.False, "leaving Hooked stops the creak within 2 frames");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("creak fade");
        }

        // Catches (final review I-1): solo pause mid-fight leaving the rumble, the Line_Creak / Dive_Thrum loops, the reel clicks and the
        // cap.straining caption running. The view stays Hooked with frozen tension while paused (the controller still publishes it), so only the
        // pause gate can silence them. 3.3 s of pause outlasts the 3 s caption gap, so a caption leak would show. Unpausing restarts the loops.
        [UnityTest]
        public IEnumerator FeedbackSolo_SoloPause_SilencesRumbleLoopsClicksAndCaptions()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "EventBus");
            Assert.That(ServiceRoots.Game, Is.Not.Null, "GameScope");
            Assert.That(ServiceRoots.Game.TryGet(out IPauseState pause), Is.True, "IPauseState");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Vector3 bobber = tip.position + new Vector3(0f, -1f, 5f);
            Dictionary<(string, bool, int, int), float> lastPlayed = LastPlayed(audio);
            var clicks = new PlayCounter(lastPlayed, (SfxIds.ReelClick, false, 0, 0));
            var captions = new List<string>();

            try
            {
                rig.Controller.enabled = false;   // the view is whatever the test sets
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                rig.Log.AssertEmpty("disabling the rod controller at Idle");

                using (bus.Subscribe<CaptionRequested>(c => captions.Add(c.Key)))
                {
                    SetView(rig, RodState.Hooked, RodWireQuant.FlagDive | RodWireQuant.FlagReel, 0.9f, bobber);

                    // Positive controls: everything the pause must silence is running first.
                    yield return PlayModeHarness.WaitUntil(
                        () => FindLoop(audio, "Loop_Line_Creak") != null && FindLoop(audio, "Loop_Dive_Thrum") != null,
                        2f, "Hooked at tension 0.9 with a dive must start both loops");
                    yield return Hold(clicks, 0.4);
                    Assert.That(clicks.Count, Is.GreaterThan(0), "positive control: the reel ticks while reeling");
                    Assert.That(captions, Does.Contain("cap.straining"), "positive control: cap.straining at tension 0.9");
                    Assert.That(Mathf.Max(rig.Fake.LastRumbleLow, rig.Fake.LastRumbleHigh), Is.GreaterThan(0f), "positive control: rumble while fighting");

                    pause.SetPaused(true);
                    yield return PlayModeHarness.WaitFrames(3, 0.1f);
                    captions.Clear();
                    int clicksAtPause = clicks.Count;

                    // The view is still Hooked at 0.9 with the dive and reel flags latched; 3.3 s of it must stay silent.
                    double start = Time.realtimeSinceStartupAsDouble;
                    while (Time.realtimeSinceStartupAsDouble - start < 3.3)
                    {
                        yield return null;
                        clicks.Poll();
                        Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low while paused");
                        Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high while paused");
                        Assert.That(HasLoop(audio), Is.False, "no Loop_ child may run while paused");
                    }

                    Assert.That(clicks.Count, Is.EqualTo(clicksAtPause), "no reel click while paused");
                    Assert.That(captions, Is.Empty, "no caption while paused");

                    pause.SetPaused(false);
                    yield return PlayModeHarness.WaitUntil(
                        () => FindLoop(audio, "Loop_Line_Creak") != null && FindLoop(audio, "Loop_Dive_Thrum") != null,
                        2f, "unpausing mid-fight must restart both loops from the polled view");
                    yield return Hold(clicks, 0.4);
                    Assert.That(clicks.Count, Is.GreaterThan(clicksAtPause), "the reel ticks again after unpausing");
                }
            }
            finally
            {
                pause.SetPaused(false);
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("solo pause feedback");
        }

        // Catches (T23 review): HookSet's four clicks dropped by the Reel_Click rate limit (0.05 s against the last click that PLAYED) because the
        // later clicks were anchored to the first one: at a frame time of 40 ms the anchored clicks give three plays. Frames are slowed with a sleep so
        // the frame time is known; 8 ms is a fast-frame control that must also give four. A play is counted when the bus's last-played time for the
        // owner's Reel_Click changes.
        [UnityTest]
        public IEnumerator FeedbackSolo_HookSet_PlaysFourClicks_AtSlowAndFastFrames()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;
            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Dictionary<(string, bool, int, int), float> lastPlayed = LastPlayed(audio);

            foreach (int sleepMs in new[] { 40, 8 })
            {
                yield return PlayModeHarness.WaitFrames(2, 0.2f);   // the previous clicks are long past the rate limit
                var counter = new PlayCounter(lastPlayed, (SfxIds.ReelClick, false, 0, 0));

                rig.Net.RaiseLocalCue(RodCue.HookSet, 0);
                counter.Poll();   // click 1 plays inside the cue
                int frames = 0;
                double start = Time.realtimeSinceStartupAsDouble;
                // Poll until the fourth click plays (2 s deadline), then hold 0.2 s so a fifth, stray click would be seen: exactly 4.
                while (counter.Count < 4 && Time.realtimeSinceStartupAsDouble - start < 2.0)
                {
                    Thread.Sleep(sleepMs);
                    yield return null;
                    frames++;
                    counter.Poll();
                }
                double holdStart = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble - holdStart < 0.2)
                {
                    Thread.Sleep(sleepMs);
                    yield return null;
                    frames++;
                    counter.Poll();
                }

                double frameTime = (Time.realtimeSinceStartupAsDouble - start) / frames;
                Assert.That(frameTime, Is.GreaterThanOrEqualTo(sleepMs / 1000.0 * 0.95), "the frame time must be at least the sleep (" + sleepMs + " ms)");
                Assert.That(counter.Count, Is.EqualTo(4), "HookSet must play four Reel_Click at a " + (frameTime * 1000.0).ToString("F1") + " ms frame time");
            }

            rig.Log.AssertEmpty("hookset clicks");
        }

        // Catches (T23 review): the Line_Crack missing, level-triggered (a hold of 0.8 s is over five times its 0.15 s rate limit, so a level trigger
        // plays several), played twice for one rising edge, not played again on the next edge, replayed for a view that is already in grace when the
        // component is enabled (a late joiner, F6), or played spatially on the owner (the owner's is 2D: the Play2D key).
        [UnityTest]
        public IEnumerator FeedbackSolo_Grace_PlaysOneLineCrackPerRisingEdge()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;
            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Behaviour feedback = FindBehaviour(rig.Player.gameObject, "FishingFeedback");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Vector3 bobber = tip.position + new Vector3(0f, -1f, 5f);
            Dictionary<(string, bool, int, int), float> lastPlayed = LastPlayed(audio);
            var counter = new PlayCounter(lastPlayed, (SfxIds.LineCrack, false, 0, 0));

            try
            {
                rig.Controller.enabled = false;
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                rig.Log.AssertEmpty("disabling the rod controller at Idle");

                SetView(rig, RodState.Hooked, 0, 0.9f, bobber);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);   // the first poll only seeds the edge
                counter.Poll();
                Assert.That(counter.Count, Is.EqualTo(0), "no crack below grace");

                SetView(rig, RodState.Hooked, RodWireQuant.FlagGrace, 1f, bobber);
                yield return Hold(counter, 0.8);
                Assert.That(counter.Count, Is.EqualTo(1), "one Line_Crack for one rising edge, however long grace is held");

                SetView(rig, RodState.Hooked, 0, 0.9f, bobber);
                yield return Hold(counter, 0.3);
                Assert.That(counter.Count, Is.EqualTo(1), "leaving grace plays nothing");

                SetView(rig, RodState.Hooked, RodWireQuant.FlagGrace, 1f, bobber);
                yield return Hold(counter, 0.5);
                Assert.That(counter.Count, Is.EqualTo(2), "the next rising edge plays the second crack");

                foreach (var key in lastPlayed.Keys)
                    Assert.That(key.Item1 == SfxIds.LineCrack && key.Item2, Is.False, "the owner's Line_Crack is 2D, never positional");

                // A late joiner / re-enable mid-grace: the first poll seeds the edge, nothing replays.
                feedback.enabled = false;
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                feedback.enabled = true;
                yield return Hold(counter, 0.5);
                Assert.That(counter.Count, Is.EqualTo(2), "enabling the component while already in grace must not replay the crack");
            }
            finally
            {
                if (rig.Controller != null) rig.Controller.enabled = true;
            }

            rig.Log.AssertEmpty("grace crack");
        }

        // Catches (T23 review): the snap silhouette scaled or hidden by settings.cameraMotion. Camera motion is shake, FOV kicks, bob and rumble;
        // the jump is world feedback everyone sees and its height tells the fish size. With cameraMotion 0 the silhouette still shows and rises.
        [UnityTest]
        public IEnumerator FeedbackSolo_SnapSilhouette_IgnoresCameraMotion()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;
            Assert.That(ServiceRoots.App.TryGet(out ISettings settings), Is.True, "ISettings");
            Transform silhouette = rig.Player.transform.Find("SnapSilhouette");
            Assert.That(silhouette, Is.Not.Null, "SnapSilhouette child");

            float original = settings.Current.cameraMotion;
            try
            {
                settings.Current.cameraMotion = 0f;
                settings.NotifyChanged();

                rig.Net.RaiseLocalCue(RodCue.Snapped, 2);
                yield return null;
                Assert.That(silhouette.gameObject.activeSelf, Is.True, "the silhouette shows with cameraMotion 0");

                float minY = float.MaxValue, maxY = float.MinValue;
                double deadline = Time.realtimeSinceStartupAsDouble + 2.0;
                while (silhouette.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    minY = Mathf.Min(minY, silhouette.position.y);
                    maxY = Mathf.Max(maxY, silhouette.position.y);
                    yield return null;
                }

                Assert.That(silhouette.gameObject.activeSelf, Is.False, "the jump ends");
                Assert.That(maxY - minY, Is.GreaterThan(0.8f), "a size-2 jump rises about 1.4 m whatever cameraMotion is");
            }
            finally
            {
                settings.Current.cameraMotion = original;
                settings.NotifyChanged();
            }

            rig.Log.AssertEmpty("snap silhouette and camera motion");
        }

        // Catches: the reveal missing a material on a submesh (a multi-submesh species model draws invisible parts); the wrong material for a
        // fallback mesh or a model; the scale rule (+Z bounds length = LengthMm / 1000, junk 0.3 m); the pose not at CatchHold; ReleasedCreelFull
        // not dropping and splashing; Lost still showing a fish.
        [UnityTest]
        public IEnumerator FeedbackSolo_Reveal_ModelFallbackAndCreelFull_CoverEverySubmesh()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            // M3 T3: perch is deleted; the two-submesh model swap uses bluegill (restored in finally).
            ushort bluegill = rig.Registry.SpeciesIndex("bluegill");
            ushort boot = rig.Registry.JunkIndex("old_boot");
            FishSpeciesDef bluegillDef = rig.Registry.SpeciesAt(bluegill);
            FieldInfo modelField = typeof(FishSpeciesDef).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modelField, Is.Not.Null, "FishSpeciesDef.model");
            Mesh original = (Mesh)modelField.GetValue(bluegillDef);
            Mesh twoSubmeshes = MakeTwoSubmeshMesh();
            Transform reveal = rig.Player.transform.Find(RevealName);
            Transform hold = rig.Player.transform.Find(PlayerRigPaths.CatchHold);
            Assert.That(reveal, Is.Not.Null, "no CatchReveal child (CatchRevealView missing)");
            Assert.That(hold, Is.Not.Null, "no CatchHold");

            try
            {
                // A species with a two-submesh model: both submeshes get M_Toon_Fish; scaled so +Z bounds = 0.25 m; held at CatchHold.
                modelField.SetValue(bluegillDef, twoSubmeshes);
                Announce(rig, new CatchResult { IsJunk = false, ItemIndex = bluegill, LengthMm = 250, Quality = CatchQuality.Trophy }, AnnounceKind.Landed, true);
                yield return PlayModeHarness.WaitFrames(2, 0.4f);   // past the 0.15 s scale-in
                Assert.That(reveal.gameObject.activeSelf, Is.True, "the model reveal is shown");
                MeshFilter filter = reveal.GetComponent<MeshFilter>();
                MeshRenderer renderer = reveal.GetComponent<MeshRenderer>();
                Assert.That(filter.sharedMesh, Is.SameAs(twoSubmeshes), "the species model is used when it exists");
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2), "one material per submesh");
                foreach (Material m in renderer.sharedMaterials)
                {
                    Assert.That(m, Is.Not.Null, "a null submesh material");
                    Assert.That(m.name, Is.EqualTo("M_Toon_Fish"), "a species model is drawn with M_Toon_Fish");
                }
                Assert.That(twoSubmeshes.bounds.size.z * reveal.lossyScale.z, Is.EqualTo(0.25f).Within(0.01f), "+Z length = LengthMm / 1000");
                Vector3 expected = hold.position + Vector3.up * 0.25f;
                Assert.That(Vector3.Distance(renderer.bounds.center, expected), Is.LessThan(0.1f), "the fish is held at CatchHold");
                yield return PlayModeHarness.WaitUntil(() => !reveal.gameObject.activeSelf, 3f, "the model reveal never hid");

                // Lost and PityCast show nothing.
                Announce(rig, new CatchResult { IsJunk = false, ItemIndex = bluegill, LengthMm = 250 }, AnnounceKind.Lost, false);
                Announce(rig, default, AnnounceKind.PityCast, false);
                yield return PlayModeHarness.WaitFrames(2, 0.1f);
                Assert.That(reveal.gameObject.activeSelf, Is.False, "Lost and PityCast must not show a reveal");

                // Junk: the fallback mesh with M_Toon_FishFallback at 0.3 m.
                Announce(rig, new CatchResult { IsJunk = true, ItemIndex = boot }, AnnounceKind.Landed, false);
                yield return PlayModeHarness.WaitFrames(2, 0.4f);
                Assert.That(reveal.gameObject.activeSelf, Is.True, "the junk reveal is shown");
                Mesh junkMesh = reveal.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(junkMesh, Is.Not.Null.And.Not.SameAs(twoSubmeshes));
                Material[] junkMaterials = reveal.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.That(junkMaterials.Length, Is.EqualTo(junkMesh.subMeshCount));
                Assert.That(junkMaterials[0].name, Is.EqualTo("M_Toon_FishFallback"), "a fallback mesh is drawn with M_Toon_FishFallback");
                Assert.That(junkMesh.bounds.size.z * reveal.lossyScale.z, Is.EqualTo(0.3f).Within(0.01f), "junk is 0.3 m long");
                yield return PlayModeHarness.WaitUntil(() => !reveal.gameObject.activeSelf, 3f, "the junk reveal never hid");

                // ReleasedCreelFull (the same species, still on the swapped model): held, then falls and splashes with a Landed ripple, then hides.
                int landedRipples = CountRipples(RippleMath.Landed);
                Announce(rig, new CatchResult { IsJunk = false, ItemIndex = bluegill, LengthMm = 180 }, AnnounceKind.ReleasedCreelFull, false);
                yield return null;
                Assert.That(reveal.gameObject.activeSelf, Is.True, "the creel-full reveal is shown");
                float holdY = hold.position.y + 0.25f;
                float lowest = float.MaxValue;
                double deadline = Time.realtimeSinceStartupAsDouble + 4.0;
                while (reveal.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    lowest = Mathf.Min(lowest, reveal.GetComponent<MeshRenderer>().bounds.center.y);
                    yield return null;
                }
                Assert.That(reveal.gameObject.activeSelf, Is.False, "the creel-full reveal never hid");
                Assert.That(lowest, Is.LessThan(holdY - 0.3f), "ReleasedCreelFull must drop the fish after the hold");
                Assert.That(CountRipples(RippleMath.Landed), Is.GreaterThan(landedRipples), "the drop splashes with a Landed ripple");
            }
            finally
            {
                modelField.SetValue(rig.Registry.SpeciesAt(bluegill), original);
                UnityEngine.Object.Destroy(twoSubmeshes);
            }

            rig.Log.AssertEmpty("catch reveal variants");
        }

        // ------------------------------------------------------------------ helpers

        private static void AssertRevealRenders(Transform reveal)
        {
            MeshFilter filter = reveal.GetComponent<MeshFilter>();
            MeshRenderer renderer = reveal.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null, "reveal MeshRenderer");
            Assert.That(renderer.enabled, Is.True, "the reveal MeshRenderer must be enabled");
            Assert.That(filter.sharedMesh, Is.Not.Null, "the reveal needs a mesh");
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(filter.sharedMesh.subMeshCount), "one material per submesh");
            foreach (Material m in renderer.sharedMaterials) Assert.That(m, Is.Not.Null, "a null reveal material");
        }

        // The owner's view is whatever the test sets once the rod controller is disabled (OwnerSetView is internal to Fishing, visible to this assembly).
        private static void SetView(FishingRig rig, RodState state, byte flags, float tension, Vector3 bobber)
        {
            rig.Net.OwnerSetView(new RodViewState { State = state, Flags = flags, Tension = tension, BobberPos = bobber, CastId = 1, IsOwner = true, RodOut = true });
        }

        private static IEnumerator Hold(PlayCounter counter, double seconds)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < seconds)
            {
                yield return null;
                counter.Poll();
            }
        }

        // AudioBus's per-id rate-limit table: every accepted play stores its Time.unscaledTime under (id, spatial, cellX, cellZ).
        private static Dictionary<(string, bool, int, int), float> LastPlayed(AudioBus bus)
        {
            FieldInfo field = typeof(AudioBus).GetField("_lastPlayed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "AudioBus._lastPlayed");
            var table = field.GetValue(bus) as Dictionary<(string, bool, int, int), float>;
            Assert.That(table, Is.Not.Null, "AudioBus._lastPlayed is a Dictionary<(string, bool, int, int), float>");
            return table;
        }

        // Counts accepted plays for one rate-limit key: each accepted play sets the stored time, and two plays never share a frame here.
        private sealed class PlayCounter
        {
            private readonly Dictionary<(string, bool, int, int), float> _table;
            private readonly (string, bool, int, int) _key;
            private float _last = float.NaN;

            public int Count { get; private set; }

            public PlayCounter(Dictionary<(string, bool, int, int), float> table, (string, bool, int, int) key)
            {
                _table = table;
                _key = key;
                if (table.TryGetValue(key, out float existing)) _last = existing;
            }

            public void Poll()
            {
                if (!_table.TryGetValue(_key, out float now) || now == _last) return;
                _last = now;
                Count++;
            }
        }

        // AudioBus loops are children named Loop_<id>; a stopped loop's object is destroyed at the end of the frame.
        private static bool HasLoop(AudioBus bus)
        {
            return FindLoop(bus) != null;
        }

        // With no name, the weather's Loop_Rain_Patter does not count: rain is the world's loop (CCR-F4, applied defensively), not the rod's, and it
        // runs whenever the fixed dev seed's weather is Rain.
        private static Transform FindLoop(AudioBus bus, string name = null)
        {
            string rain = "Loop_" + SfxIds.RainPatter;
            foreach (Transform child in bus.transform)
            {
                if (name == null ? child.name.StartsWith("Loop_", StringComparison.Ordinal) && child.name != rain : child.name == name) return child;
            }
            return null;
        }

        private static bool HasRipple(float strength, Vector3 near, float radius)
        {
            foreach (Vector4 v in Shader.GetGlobalVectorArray(ShaderGlobals.Ripples))
            {
                if (Mathf.Abs(v.w - strength) > 1e-4f) continue;
                if (new Vector2(v.x - near.x, v.y - near.z).magnitude <= radius) return true;
            }
            return false;
        }

        private static int CountRipples(float strength)
        {
            int n = 0;
            foreach (Vector4 v in Shader.GetGlobalVectorArray(ShaderGlobals.Ripples))
            {
                if (Mathf.Abs(v.w - strength) <= 1e-4f) n++;
            }
            return n;
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

        // FishingRodNetwork.Announced is a field-like event with no public raiser (it is raised from CatchAnnouncedRpc); reflect its delegate.
        private static void Announce(FishingRig rig, CatchResult result, AnnounceKind kind, bool clean)
        {
            FieldInfo field = typeof(FishingRodNetwork).GetField("Announced", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "FishingRodNetwork.Announced backing field");
            var handler = (Action<CatchAnnouncement>)field.GetValue(rig.Net);
            Assert.That(handler, Is.Not.Null, "nothing is subscribed to Announced (CatchRevealView / FishingFeedback missing)");
            handler(new CatchAnnouncement(rig.Player.Slot, in result, kind, rig.Net.OwnerClientId, clean));
        }

        // Two quads in two submeshes, 0.5 m long on +Z, with vertex colours (a Toon mesh needs the channel).
        private static Mesh MakeTwoSubmeshMesh()
        {
            var mesh = new Mesh { name = "TestTwoSubmeshFish" };
            mesh.vertices = new[]
            {
                new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.25f), new Vector3(-0.1f, 0.1f, 0.25f),
                new Vector3(-0.1f, 0.1f, 0.25f), new Vector3(0.1f, 0.1f, 0.25f), new Vector3(0.1f, 0f, 0.5f), new Vector3(-0.1f, 0f, 0.5f),
            };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.SetTriangles(new[] { 4, 6, 5, 4, 7, 6 }, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
