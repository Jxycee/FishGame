// Authority: test-only. T19 first end-to-end run of the fishing loop through the real solo host: owner FishingRodController ->
// FishingRodNetwork RPCs -> FishingHost ledger -> CommitRpc / CatchAnnouncedRpc -> creel. Input is scripted through FakeInputService.
// Fixture G9: the player is teleported to (0, 0.65, -41) facing +z, 1 m inside the dock end, so a 0.15 charge lands over open water.
// FishingPlayModeDriver is shared with later PlayMode tests; its member names are frozen (ruling F-Q7).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishGame.App;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    // Wraps PlayModeHarness.LogCapture (Warning/Error/Exception/Assert, minus the licensing noise) under the frozen name.
    internal sealed class LogWatch : IDisposable
    {
        private readonly PlayModeHarness.LogCapture _capture = new PlayModeHarness.LogCapture();

        public int Count => _capture.Count;

        public void AssertEmpty(string context)
        {
            _capture.AssertNoWarnings(context);
        }

        public void Dispose()
        {
            _capture.Dispose();
        }
    }

    // Everything Arrange resolves, plus what the tests collect while they run.
    internal sealed class FishingRig : IDisposable
    {
        public FakeInputService Fake;
        public PlayerNetState Player;
        public FishingRodNetwork Net;
        public FishingRodController Controller;
        public PlayerLocomotion Locomotion;
        public PlayerCreel Creel;
        public ContentRegistry Registry;
        public LogWatch Log;

        public readonly List<CatchAnnounced> Announced = new List<CatchAnnounced>();
        public readonly List<string> Order = new List<string>();   // "commit" / "announce" in arrival order

        private IDisposable _busSubscription;
        private Action<uint, bool, FightOutcome, EscapeReason> _commitHandler;

        public RodStateMachine Machine => Controller.Machine;

        public void Subscribe()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out EventBus bus))
            {
                _busSubscription = bus.Subscribe<CatchAnnounced>(a =>
                {
                    Announced.Add(a);
                    Order.Add("announce");
                });
            }

            _commitHandler = (castId, accepted, outcome, reason) => Order.Add("commit");
            Net.CommitReceived += _commitHandler;
        }

        public void Dispose()
        {
            if (_busSubscription != null)
            {
                _busSubscription.Dispose();
                _busSubscription = null;
            }

            if (Net != null && _commitHandler != null)
            {
                Net.CommitReceived -= _commitHandler;
                _commitHandler = null;
            }

            if (Log != null)
            {
                Log.Dispose();
                Log = null;
            }
        }
    }

    internal static class FishingPlayModeDriver
    {
        public const float DeckTeleportZ = -41f;   // G9

        // Same as PlayerMovementTests: must run before StartSoloAndWaitForPlayer (C 2.8 #9).
        public static FakeInputService InstallFakeInput()
        {
            Assert.That(ServiceRoots.App, Is.Not.Null, "ServiceRoots.App missing; load Bootstrap first");
            var fake = new FakeInputService { MenuOpen = false, GameplayContext = InputContext.Free, Frame = default };
            ServiceRoots.App.Replace<IInputService>(fake);
            return fake;
        }

        // Sets the frame, then waits one frame: a coroutine resumes after every Update, so exactly one controller Update reads it.
        // Edge flags (*Pressed, *Released) must be set for exactly one Step.
        public static IEnumerator Step(FakeInputService fake, PlayerInputFrame frame)
        {
            fake.Frame = frame;
            yield return null;
        }

        public static void ResetDebugSettings()
        {
            FishingDebugSettings.ForcedBiteWait = 0f;
            FishingDebugSettings.ForcedSpeciesId = null;
            FishingDebugSettings.TwitchyHook = false;
            FishingDebugSettings.ForceJunk = false;
        }

        // Bootstrap -> Menu -> solo host -> the Handmade loadout mirrored (rod_handmade, 20 worms) -> optionally the G9 teleport.
        public static IEnumerator Arrange(FishingRig rig, bool teleport = true)
        {
            rig.Log = new LogWatch();
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            rig.Fake = InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            rig.Player = PlayerNetState.Local;
            Assert.That(rig.Player, Is.Not.Null, "no local player");
            rig.Net = rig.Player.GetComponent<FishingRodNetwork>();
            rig.Creel = rig.Player.GetComponent<PlayerCreel>();
            rig.Locomotion = rig.Player.GetComponent<PlayerLocomotion>();
            Assert.That(rig.Net, Is.Not.Null, "FishingRodNetwork missing on PlayerFP");
            Assert.That(rig.Creel, Is.Not.Null, "PlayerCreel missing on PlayerFP");
            Assert.That(rig.Locomotion, Is.Not.Null, "PlayerLocomotion missing on PlayerFP");
            Assert.That(ServiceRoots.App.TryGet(out rig.Registry), Is.True, "ContentRegistry missing from the App services");

            ushort handmade = rig.Registry.RodIndex("rod_handmade");
            ushort worm = rig.Registry.BaitIndex("bait_worm");
            FishingRig captured = rig;
            yield return PlayModeHarness.WaitUntil(
                () => FishingRodController.Local != null
                      && captured.Net.NetRodIndex.Value == handmade
                      && captured.Net.NetSelectedBait.Value == worm
                      && captured.Net.NetSelectedBaitCount.Value == 20,
                PlayModeHarness.DefaultTimeoutSeconds,
                "the owner's rod controller never enabled or the host loadout mirror (rod_handmade, bait_worm x20) never landed");
            rig.Controller = FishingRodController.Local;
            rig.Subscribe();

            yield return PlayModeHarness.WaitUntil(() => captured.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds,
                "the player never became grounded on the dock");
            if (teleport)
            {
                rig.Locomotion.ApplyTeleport(new Vector3(0f, 0.65f, DeckTeleportZ), 0f);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                yield return PlayModeHarness.WaitUntil(() => captured.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds,
                    "the player never became grounded after the G9 teleport");
            }
        }

        // Equip (1 / North), ready (RMB held): leaves the rod Ready with the cast marker on valid water.
        public static IEnumerator EquipAndReady(FishingRig rig)
        {
            FakeInputService fake = rig.Fake;
            yield return Step(fake, new PlayerInputFrame { EquipPressed = true });
            fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, PlayModeHarness.DefaultTimeoutSeconds, "Equip never set the Rod tool");
            yield return PlayModeHarness.WaitFrames(2, 0f);   // the context needs a frame to become RodReady

            yield return Step(fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            double deadline = Time.realtimeSinceStartupAsDouble + 2.0;
            while (rig.Machine.State != RodState.Ready)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("the rod never reached Ready while Secondary was held (state " + rig.Machine.State + ")");
                yield return Step(fake, new PlayerInputFrame { SecondaryHeld = true });
            }

            yield return PlayModeHarness.WaitFrames(2, 0f);   // so the context is RodAiming
            Assert.That(rig.Controller.MarkerValid, Is.True, "the cast marker (castTipReach + 2.5 m ahead of the G9 spot, R67) must be on water");
        }

        // Press Primary and hold until ChargeNorm >= min. Leaves Secondary and Primary held.
        public static IEnumerator Charge(FishingRig rig, float min = 0.15f)
        {
            FakeInputService fake = rig.Fake;
            yield return Step(fake, new PlayerInputFrame { SecondaryHeld = true, PrimaryPressed = true, PrimaryHeld = true });
            double deadline = Time.realtimeSinceStartupAsDouble + 3.0;
            while (rig.Machine.ChargeNorm < min)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("ChargeNorm never reached " + min + " (state " + rig.Machine.State + ")");
                yield return Step(fake, new PlayerInputFrame { SecondaryHeld = true, PrimaryHeld = true });
            }

            Assert.That(rig.Machine.ChargeNorm, Is.InRange(0.1f, 0.6f), "the charge must be a short cast so it lands over the water, not on the shore");
        }

        // Release Primary with Secondary still held (Secondary released while Charging means Idle), then clear the frame.
        public static IEnumerator Release(FishingRig rig)
        {
            yield return Step(rig.Fake, new PlayerInputFrame { SecondaryHeld = true, PrimaryReleased = true });
            rig.Fake.Frame = default;
        }

        public static IEnumerator ChargeAndRelease(FishingRig rig, float min = 0.15f)
        {
            yield return Charge(rig, min);
            yield return Release(rig);
        }

        public static IEnumerator WaitForState(FishingRig rig, RodState state, float timeoutSeconds)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.Machine.State == state, timeoutSeconds,
                "the rod never reached " + state + " (state " + rig.Machine.State + ")");
        }

        // R72 Rust pump: counter a run without reeling (only while the steer counts, i.e. the fish is on its pull side), reel at rest steering
        // toward the cast line, and let go above 0.85 tension until it falls below 0.30.
        public static IEnumerator FightRhythm(FishingRig rig, float timeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            bool easing = false;
            while (rig.Machine.State == RodState.Hooked)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("the fight did not end within " + timeoutSeconds + " s");
                FightState s = rig.Machine.Fight.State;
                bool run = s.Phase == FightPhase.Burst;
                if (s.Tension >= 0.85f) easing = true;
                else if (easing && s.Tension <= 0.30f) easing = false;
                float steer = 0f; bool reel = false;
                if (!easing)
                {
                    if (run) steer = s.BurstDir * s.Lateral > 0f ? -s.BurstDir : 0f;
                    else { reel = true; steer = s.Lateral > 0.05f ? -1f : s.Lateral < -0.05f ? 1f : 0f; }
                }
                yield return Step(rig.Fake, new PlayerInputFrame { Steer = steer, Reel = reel });
            }

            rig.Fake.Frame = default;
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        // Like Charge but for a (near) full-power cast: holds until ChargeNorm >= min. Leaves Secondary and Primary held.
        public static IEnumerator ChargeToMax(FishingRig rig, float min = 0.95f)
        {
            FakeInputService fake = rig.Fake;
            yield return Step(fake, new PlayerInputFrame { SecondaryHeld = true, PrimaryPressed = true, PrimaryHeld = true });
            double deadline = Time.realtimeSinceStartupAsDouble + 3.0;
            while (rig.Machine.ChargeNorm < min)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("ChargeNorm never reached " + min + " (state " + rig.Machine.State + ")");
                yield return Step(fake, new PlayerInputFrame { SecondaryHeld = true, PrimaryHeld = true });
            }
        }

        // Shared TearDown body (ruling T19-R1): debug settings first (SubsystemRegistration resets only on entering Play Mode),
        // then the log watch and the bus / commit subscriptions (teardown has its own checks), then the App.
        public static IEnumerator TearDown(FishingRig rig)
        {
            ResetDebugSettings();
            if (rig != null)
            {
                rig.Dispose();
            }

            yield return PlayModeHarness.TearDownApp();
        }

        // Moves the owner's look pitch to the given angle (positive = up) and waits until PlayerBody.LateUpdate has posed the arm for it.
        // A coroutine resumes after Update and before LateUpdate, so the pose from the look Step exists only after a second frame.
        public static IEnumerator SetPitch(FishingRig rig, PlayerCamera camera, float pitchDeg)
        {
            yield return Step(rig.Fake, new PlayerInputFrame { LookPitchDeg = pitchDeg - camera.PitchDeg });
            yield return Step(rig.Fake, default);
            Assert.That(camera.PitchDeg, Is.EqualTo(pitchDeg).Within(0.01f), "the look pitch did not reach the requested angle");
        }
    }

    public class FishingSoloTests
    {
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: any link of cast -> result -> bite -> hook -> fight -> commit -> creel -> announce being broken or mis-wired in the
        // prefab or scene (missing component, controller never enabled, host absent); the host not decrementing bait; the announcement
        // sent before the commit; a creel entry that disagrees with the announced catch; rumble left on after the terminal beat;
        // the movement lock not being requested on Hooked; a warning anywhere in the loop (V12 water-first probe runs here too).
        [UnityTest]
        public IEnumerator SoloHost_CastBiteFight_Lands()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            // V12: the nearest hit decides. Over the deck the dock is hit first (probe ray starts 20 m up); off the deck end it is water.
            WaterProbe overDeck = CastProbe.Probe(0f, -45f);
            Assert.That(overDeck.FirstHitIsWater, Is.False, "over the deck the dock must be hit before the water trigger");
            WaterProbe offDeck = CastProbe.Probe(0f, -36f);
            Assert.That(offDeck.FirstHitIsWater, Is.True, "off the deck end the first hit must be the water trigger");
            Assert.That(offDeck.HasBed, Is.True, "the lakebed must be found below the water hit");
            Assert.That(offDeck.Depth, Is.InRange(1.5f, 3.5f), "depth off the deck end");
            TestContext.WriteLine($"V12: Probe(0,-45) water={overDeck.FirstHitIsWater}; Probe(0,-36) water={offDeck.FirstHitIsWater} waterY={offDeck.WaterY:F3} depth={offDeck.Depth:F3}");

            yield return FishingPlayModeDriver.EquipAndReady(rig);

            // T19-R1: in the owner arm pose the rod tip is in front of the eye and only slightly above it (the rod points forward-up, not up/back).
            yield return PlayModeHarness.WaitFrames(2, 0f);   // PlayerBody.LateUpdate has posed the arm
            Transform cam = rig.Player.transform.Find(PlayerRigPaths.Camera);
            Assert.That(cam, Is.Not.Null, "the player camera transform");
            Vector3 tipPos = rig.Controller.RodTipPosition;
            float tipForwardDot = Vector3.Dot((tipPos - cam.position).normalized, cam.forward);
            TestContext.WriteLine($"FG T19R1 tip dot={tipForwardDot:F3} tipAboveEye={tipPos.y - cam.position.y:F3}");
            Assert.That(tipForwardDot, Is.GreaterThan(0.8f), "the rod tip must lie in front of the camera (rod points forward-up)");
            Assert.That(tipPos.y, Is.LessThan(cam.position.y + 1.0f), "the rod tip must be below 1 m above the eye line");

            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Casting), "state right after the release");

            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 6f);
            yield return PlayModeHarness.WaitFrames(2, 0f);
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.RodFighting), "PlayerLocomotion writes RodFighting from the controller's request");

            yield return FishingPlayModeDriver.FightRhythm(rig, 100f);
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Landed), "the scripted fight must land the fish (outcome " + rig.Machine.Outcome + "/" + rig.Machine.Reason + ")");

            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 5f);
            yield return PlayModeHarness.WaitUntil(() => rig.Creel.Count >= 1 && rig.Announced.Count >= 1, 3f, "the commit never reached the creel and the bus");

            ushort bluegill = rig.Registry.SpeciesIndex("bluegill");
            Assert.That(rig.Creel.Count, Is.EqualTo(1), "creel count");
            CreelEntry entry = rig.Creel.NetCreel[0];
            Assert.That(entry.IsJunk, Is.EqualTo((byte)0), "forced species, not junk");
            Assert.That(entry.ItemIndex, Is.EqualTo(bluegill), "creel item is the forced bluegill");

            Assert.That(rig.Announced.Count, Is.EqualTo(1), "exactly one CatchAnnounced");
            CatchAnnounced a = rig.Announced[0];
            Assert.That(a.Announcement.Kind, Is.EqualTo(AnnounceKind.Landed));
            Assert.That(a.IsLocalCatcher, Is.True);
            Assert.That(a.Announcement.CatcherSlot, Is.EqualTo(rig.Player.Slot));
            Assert.That(a.Announcement.Catch.ItemIndex, Is.EqualTo(entry.ItemIndex), "announced item matches the creel entry");
            Assert.That((byte)a.Announcement.Catch.Quality, Is.EqualTo(entry.Quality), "announced quality matches the creel entry");

            Assert.That(rig.Order, Is.EqualTo(new[] { "commit", "announce" }), "commit-then-announce (ruling E-Q8)");
            Assert.That(rig.Net.NetSelectedBaitCount.Value, Is.EqualTo((ushort)19), "one worm spent");
            Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f));
            Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f));
            rig.Log.AssertEmpty("cast-bite-fight-land loop");
        }

        // Catches (V11, spec 17.2): CastResultRpc running inside the caller's stack on a host-owned rod (DeferLocal missing): the machine
        // would already have its result when the CastRequestRpc call returns, which re-enters the owner state change.
        [UnityTest]
        public IEnumerator SoloHost_CastResult_ArrivesAfterCastRequestReturns()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            int count = 0;
            int receivedFrame = -1;
            rig.Net.CastResultReceived += (castId, valid, reason, landing, roll) =>
            {
                receivedFrame = Time.frameCount;
                count++;
            };

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.Charge(rig);

            // The release Step inline: the frame after this yield is the one whose Update read PrimaryReleased and sent the request.
            rig.Fake.Frame = new PlayerInputFrame { SecondaryHeld = true, PrimaryReleased = true };
            yield return null;
            int sendFrame = Time.frameCount;
            rig.Fake.Frame = default;   // before the next frame, or PrimaryReleased would be read twice

            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Casting), "state right after CastRequestRpc returned");
            Assert.That(rig.Machine.HasResult, Is.False, "no result inside the caller's stack");
            Assert.That(count, Is.EqualTo(0), "the host handled CastRequestRpc inline (N2), but its reply must not have run yet");

            yield return null;
            Assert.That(count, Is.EqualTo(1), "the deferred reply arrives on the next frame");
            Assert.That(receivedFrame, Is.EqualTo(sendFrame + 1), "N1: dispatched at the next frame's EarlyUpdate");
            Assert.That(rig.Machine.HasResult, Is.True);
            rig.Log.AssertEmpty("cast result timing");
        }

        // Catches (R67 follow-up, end to end): the owner's own landing prediction (the bobber it publishes the frame it sends the request,
        // before the host's reply can be adopted) differing from the host's ActiveCast landing by even one ulp: the cast-tip reach applied on one
        // side only, a different heading or origin. (A release-frame charge advance, M-4, is not visible here: owner and host derive from the
        // same Machine.CastDistance; the RodStateMachineTests M-4 cases cover it.) No rod switch here, so adoption cannot hide a mismatch.
        [UnityTest]
        public IEnumerator SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            FishingHost host = FishingHost.Current;
            Assert.That(host, Is.Not.Null, "the solo host's FishingHost");

            rig.Locomotion.ApplyTeleport(new Vector3(0f, 0.65f, FishingPlayModeDriver.DeckTeleportZ), 25f);   // a non-zero yaw so x discriminates the heading
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "the player never became grounded after the teleport");

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.Charge(rig);

            rig.Fake.Frame = new PlayerInputFrame { SecondaryHeld = true, PrimaryReleased = true };
            yield return null;   // the owner's Update of this frame sent the request; the host handled it inline; the reply is deferred
            rig.Fake.Frame = default;
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Casting));
            Assert.That(rig.Machine.HasResult, Is.False, "the host's reply has not been adopted yet: the view is the owner's own prediction");

            Assert.That(host.Ledger.TryGetActive(rig.Player.OwnerClientId, out ActiveCast cast), Is.True, "the host must have accepted the cast");
            Vector3 predicted = rig.Net.ViewState.BobberPos;
            TestContext.WriteLine($"FG LANDPARITY: owner=({predicted.x:R},{predicted.z:R}) host=({cast.LandingX:R},{cast.LandingZ:R}) charge={rig.Machine.ChargeNorm:R}");
            Assert.That(predicted.x, Is.EqualTo(cast.LandingX), "owner landing x == ActiveCast.LandingX (bit-identical)");
            Assert.That(predicted.z, Is.EqualTo(cast.LandingZ), "owner landing z == ActiveCast.LandingZ (bit-identical)");
            Assert.That(Mathf.Abs(predicted.x), Is.GreaterThan(1f), "yaw 25: the landing is off the x = 0 line, so a wrong heading would show");

            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
            rig.Log.AssertEmpty("landing parity");
        }

        // Catches: a Recall in Waiting that sends no CancelRpc, or a host that does not refund the bait on a cancelled Waiting cast
        // (the stock would stay 19), or a refund that over-counts or leaves a creel entry / announcement behind.
        [UnityTest]
        public IEnumerator SoloHost_CancelInWaiting_RefundsBait()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;   // the bite cannot beat the cancel
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
            Assert.That(rig.Net.NetSelectedBaitCount.Value, Is.EqualTo((ushort)19), "the bait is spent while the line is out");

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            rig.Fake.Frame = default;
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 1f);
            yield return PlayModeHarness.WaitUntil(() => rig.Net.NetSelectedBaitCount.Value == 20, 2f, "the cancelled cast's bait was never refunded");

            Assert.That(rig.Creel.Count, Is.EqualTo(0));
            Assert.That(rig.Announced, Is.Empty, "a cancel announces nothing");
            rig.Log.AssertEmpty("cancel in waiting");
        }

        // Catches (final review M-5): the F6 forced bite and the F7 twitchy hook surviving the session (they are statics, so leaving to the
        // menu and starting a new run would keep them on). INetworkSession.Ended (the session ending) clears both.
        [UnityTest]
        public IEnumerator SoloHost_LeavingTheSession_ClearsF6AndF7()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingDebugSettings.ForcedBiteWait = 0.1f;   // as F6 / F7 set them mid-session (after the scene loaded)
            FishingDebugSettings.TwitchyHook = true;
            _rig.Dispose();

            yield return PlayModeHarness.TearDownApp();   // leaves the session and unloads the gameplay scene
            Assert.That(FishingDebugSettings.ForcedBiteWait, Is.EqualTo(0f), "F6 must not survive leaving the session");
            Assert.That(FishingDebugSettings.TwitchyHook, Is.False, "F7 must not survive leaving the session");
        }

        // Catches (C2 16.1 carry): the drawn cast marker written before Machine.Tick, so it shows the previous frame's charge (about 0.5 m
        // behind at 30 fps on Handmade) and is still shown on the release frame. It also catches a fix that moved the whole probe after the
        // tick: SoloHost_OwnerPreAdoptionLanding_IsBitIdenticalToTheHostActiveCast and the RodViewSoloTests marker asserts guard the gate side.
        [UnityTest]
        public IEnumerator FishingSolo_CastMarker_UpdatedAfterMachineTickSameFrame()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            yield return FishingPlayModeDriver.EquipAndReady(rig);

            Vector3 p0 = rig.Player.transform.position;
            float yaw = rig.Player.transform.eulerAngles.y;
            float minDist = rig.Registry.Fishing.Tuning.CastMinDist;
            float maxDist = rig.Controller.Rod.CastMaxDist;
            Time.captureFramerate = 30;   // every frame's charge step is 1/30 s, so the old one-frame lag is about 0.5 m on Handmade
            try
            {
                float cPrev = 0f;
                for (int i = 0; i < 9; i++)
                {
                    PlayerInputFrame frame = i == 0
                        ? new PlayerInputFrame { SecondaryHeld = true, PrimaryPressed = true, PrimaryHeld = true }
                        : new PlayerInputFrame { SecondaryHeld = true, PrimaryHeld = true };
                    yield return FishingPlayModeDriver.Step(rig.Fake, frame);

                    float c = rig.Machine.ChargeNorm;
                    Vector3 expected = LandingAt(p0, yaw, c, minDist, maxDist);
                    Assert.That(rig.Controller.MarkerVisible, Is.True, "the marker is shown while charging (sample " + i + ")");
                    Assert.That(FishingPlayModeDriver.Flat(rig.Controller.MarkerPosition, expected), Is.LessThan(1e-3f),
                        "the drawn marker uses the post-tick charge (sample " + i + ", charge " + c + ")");
                    if (i >= 2)
                    {
                        Vector3 previous = LandingAt(p0, yaw, cPrev, minDist, maxDist);
                        Assert.That(FishingPlayModeDriver.Flat(expected, previous), Is.GreaterThan(0.1f),
                            "the window can tell this frame's charge from the previous frame's (sample " + i + ")");
                    }

                    cPrev = c;
                }

                Assert.That(FishingPlayModeDriver.Flat(rig.Player.transform.position, p0), Is.LessThan(1e-3f), "precondition: the player must stand still");

                yield return FishingPlayModeDriver.Release(rig);
                Assert.That(rig.Machine.State, Is.EqualTo(RodState.Casting), "the release frame casts");
                Assert.That(rig.Controller.MarkerVisible, Is.False, "the marker hides on the release frame");
            }
            finally
            {
                Time.captureFramerate = 0;
            }

            rig.Log.AssertEmpty("marker after tick");
        }

        private static Vector3 LandingAt(Vector3 origin, float yawDeg, float charge, float minDist, float maxDist)
        {
            float dist = CastMath.Distance(charge, minDist, maxDist);
            CastMath.Landing(origin.x, origin.z, yawDeg, dist, out float lx, out float lz);
            return new Vector3(lx, 0f, lz);
        }

        // Catches (C2 16.1 carry, C 7.1, CCR-A1): F6/F7 cleared in FishingHost.OnDisable (the old reset, which a Spot load fires), or on the
        // ServiceRoots.Game teardown (GameScope.OnDestroy, which every Spot single-load fires), or only on SubsystemRegistration / App destroy
        // (the session ending would then not clear them). The M3 dev map switch is a Spot single-load, so F6/F7 must survive it.
        [UnityTest]
        public IEnumerator FishingSolo_SceneReload_KeepsDevOverrides_SessionEndClears()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingHost oldHost = FishingHost.Current;
            ServiceRegistry oldGame = ServiceRoots.Game;
            PlayerNetState oldPlayer = PlayerNetState.Local;
            var capture = new PlayModeHarness.LogCapture();
            try
            {
                FishingDebugSettings.ForcedBiteWait = 0.1f;   // as F6 / F7 set them mid-session
                FishingDebugSettings.TwitchyHook = true;
                _rig.Dispose();   // the rig's references die with the scene

                SceneEventProgressStatus status = NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.SpotLakeDock, LoadSceneMode.Single);
                Assert.That(status, Is.EqualTo(SceneEventProgressStatus.Started), "the Spot reload must start");
                yield return PlayModeHarness.WaitUntil(
                    () => FishingHost.Current != null && !ReferenceEquals(FishingHost.Current, oldHost)
                          && ServiceRoots.Game != null && !ReferenceEquals(ServiceRoots.Game, oldGame)
                          && PlayerNetState.Local != null && !ReferenceEquals(PlayerNetState.Local, oldPlayer),
                    20f, "the Spot reload never produced a new FishingHost, GameScope and local player");

                Assert.That(FishingDebugSettings.ForcedBiteWait, Is.EqualTo(0.1f), "F6 survives the Spot reload");
                Assert.That(FishingDebugSettings.TwitchyHook, Is.True, "F7 survives the Spot reload");

                // A plain leave, not TearDownApp: the clear must come from the session ending, not from destroying the App.
                INetworkSession session = AppBootstrap.Current.Services.Get<INetworkSession>();
                Task leave = session.LeaveAsync();
                yield return PlayModeHarness.Await(leave, 20f);
                yield return PlayModeHarness.WaitUntil(
                    () => session.State == ConnState.Offline && SceneManager.GetActiveScene().name == SceneNames.Menu,
                    20f, "the session never returned to the menu after the leave");

                Assert.That(FishingDebugSettings.ForcedBiteWait, Is.EqualTo(0f), "F6 is cleared at session end");
                Assert.That(FishingDebugSettings.TwitchyHook, Is.False, "F7 is cleared at session end");
                capture.AssertNoWarnings("scene reload");
            }
            finally
            {
                capture.Dispose();
            }
        }

        // Catches (F4/F5 select race call site): HandleCastResult not adopting the host's landing when the rod was switched mid-charge. The host
        // accepts the select (no ActiveCast yet) and casts with the NEW rod's castMaxDist; the owner's ApplyRod refuses (not Idle) and charged
        // with the OLD rod. The published bobber (ViewState.BobberPos) must be the ledger's landing, not the owner-derived one; and the
        // telemetry cast distance (FightEnded.CastDistance) must be origin -> the adopted landing (minus castTipReach, R67), not Machine.CastDistance.
        // Rods: handmade (18 m) held, twig (14 m) selected; charge >= 0.15 -> about 0.5 m apart at the shortest, both well inside the lake.
        [UnityTest]
        public IEnumerator SoloHost_SelectRodMidCharge_OwnerAdoptsHostLanding_AndTelemetryUsesIt()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;   // no bite: the cast stays in Waiting while it is inspected
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            FishingHost host = FishingHost.Current;
            Assert.That(host, Is.Not.Null, "the solo host's FishingHost");
            ulong client = rig.Player.OwnerClientId;
            ushort handmade = rig.Registry.RodIndex("rod_handmade");
            ushort twig = rig.Registry.RodIndex("rod_twig");
            Assert.That(twig, Is.Not.EqualTo(handmade));

            var ended = new List<FightEnded>();
            rig.Controller.FightEnded += e => ended.Add(e);

            // A non-zero yaw so the landing x (and the separation) also discriminates a wrong heading; yaw 0 puts every landing at x = 0.
            rig.Locomotion.ApplyTeleport(new Vector3(0f, 0.65f, FishingPlayModeDriver.DeckTeleportZ), 25f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "the player never became grounded after the yaw-25 teleport");

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.Charge(rig);
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Charging));
            Assert.That(host.Ledger.TryGetActive(client, out _), Is.False, "no ActiveCast yet, so the ledger accepts the select");

            // The race: the host switches rods while the owner is Charging; the owner's ApplyRod refuses (not Idle) and keeps the old rod.
            host.HandleSelectRod(rig.Net, twig);
            Assert.That(rig.Net.NetRodIndex.Value, Is.EqualTo(twig), "the host mirrored the new rod");
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Charging), "the select must not disturb the charge");

            yield return FishingPlayModeDriver.Release(rig);
            Vector3 origin = rig.Player.transform.position;
            float yaw = rig.Player.transform.eulerAngles.y;
            Assert.That(yaw, Is.EqualTo(25f).Within(1f), "the player must still face the yaw-25 heading (else the x assert is back to yaw 0)");
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
            yield return PlayModeHarness.WaitFrames(2, 0f);   // the published view is written in the owner's Update

            Assert.That(host.Ledger.TryGetActive(client, out ActiveCast cast), Is.True, "the host must have accepted the cast (else this test proves nothing)");
            Assert.That(cast.RodIndex, Is.EqualTo(twig), "the host cast with the newly selected rod");

            CastMath.Landing(origin.x, origin.z, yaw, rig.Machine.CastDistance, out float ownerX, out float ownerZ);   // what the owner predicted (old rod)
            Vector3 ownerLanding = new Vector3(ownerX, 0f, ownerZ);
            Vector3 hostLanding = new Vector3(cast.LandingX, 0f, cast.LandingZ);
            float separation = FishingPlayModeDriver.Flat(ownerLanding, hostLanding);
            Assert.That(CastProbe.Probe(ownerX, ownerZ).FirstHitIsWater, Is.True, "the owner-derived (old rod) landing is on water too");
            Assert.That(CastProbe.Probe(cast.LandingX, cast.LandingZ).FirstHitIsWater, Is.True, "the host landing is on water");
            TestContext.WriteLine($"FG LANDRACE: charge={rig.Machine.ChargeNorm:F3} handmade/owner distance={rig.Machine.CastDistance:F3} twig/host distance={cast.CastDistance:F3} separation={separation:F3}");
            Assert.That(separation, Is.GreaterThan(0.05f), "the two landings must differ or the test cannot tell adoption from no adoption");

            Vector3 bobber = rig.Net.ViewState.BobberPos;
            Assert.That(bobber.x, Is.EqualTo(cast.LandingX).Within(1e-3f), "the bobber x is the host landing");
            Assert.That(bobber.z, Is.EqualTo(cast.LandingZ).Within(1e-3f), "the bobber z is the host landing");
            Assert.That(FishingPlayModeDriver.Flat(bobber, ownerLanding), Is.GreaterThan(0.05f), "the bobber is not the owner-derived landing");

            // Recall: Idle ends the recording, which publishes FightEnded.
            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            rig.Fake.Frame = default;
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 1f);
            Assert.That(ended.Count, Is.EqualTo(1), "one FightEnded for the cast");
            // R67: the telemetry distance is on the rod-tip basis: origin -> the adopted landing minus castTipReach.
            float expectedDistance = FishingPlayModeDriver.Flat(origin, hostLanding) - FishingConstants.CastTipReach;
            TestContext.WriteLine($"FG LANDRACE: telemetry castDistance={ended[0].CastDistance:F3} origin->hostLanding-castTipReach={expectedDistance:F3} machine={rig.Machine.CastDistance:F3}");
            Assert.That(ended[0].CastDistance, Is.EqualTo(expectedDistance).Within(1e-3f), "the recorded cast distance is origin -> the adopted landing, minus castTipReach");
            Assert.That(ended[0].CastDistance, Is.EqualTo(cast.CastDistance).Within(1e-3f), "...which is the host's distance from the tip for the cast it adopted");
            rig.Log.AssertEmpty("select rod mid-charge");
        }

        // Catches (T19-R1): HookedLineSlack smaller than the rig's real tip reach + margin. TipAnchor is posed in camera space by
        // PlayerBody, so its horizontal distance from the player root depends on the look pitch. The Core constant must be that
        // maximum (over pitch -60..+60) + 0.5 m, rounded up to 0.1 m, or an honest owner start line could undercut the host's LineLength0.
        [UnityTest]
        public IEnumerator RodTip_MaxHorizontalReachOverPitch_FitsHookedLineSlack()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            Assert.That(camera, Is.Not.Null, "PlayerCamera missing");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Assert.That(tip, Is.Not.Null, "TipAnchor missing");

            float maxFlat = 0f, maxFlatPitch = 0f, maxForward = 0f;
            for (int pitch = -60; pitch <= 60; pitch++)
            {
                yield return FishingPlayModeDriver.SetPitch(rig, camera, pitch);
                Vector3 root = rig.Player.transform.position;
                Vector3 d = tip.position - root;
                Vector3 flatForward = Vector3.ProjectOnPlane(rig.Player.transform.forward, Vector3.up).normalized;
                float flat = new Vector2(d.x, d.z).magnitude;
                float forward = Vector3.Dot(new Vector3(d.x, 0f, d.z), flatForward);
                if (flat > maxFlat) { maxFlat = flat; maxFlatPitch = pitch; }
                if (forward > maxForward) maxForward = forward;
            }

            float expectedSlack = Mathf.Ceil((maxFlat + 0.5f) * 10f - 1e-3f) / 10f;
            TestContext.WriteLine($"FG T19R1 reach maxFlat={maxFlat:F4} at pitch {maxFlatPitch} maxForward={maxForward:F4} expectedSlack={expectedSlack:F1} constant={FishingConstants.HookedLineSlack:F1}");
            Assert.That(FishingConstants.HookedLineSlack, Is.GreaterThanOrEqualTo(maxFlat + 0.5f),
                "HookedLineSlack must cover the measured max tip reach (" + maxFlat.ToString("F3") + ") + 0.5 m");
            Assert.That(FishingConstants.HookedLineSlack, Is.EqualTo(expectedSlack).Within(0.01f),
                "HookedLineSlack must equal the measured reach + 0.5 m rounded up to 0.1 m (re-derive it after changing the rod rig)");
            rig.Log.AssertEmpty("rod tip reach sweep");
        }

        // Catches (R67): FishingConstants.CastTipReach drifting from the rig. It is the TipAnchor's horizontal forward reach from the player root
        // in the owner rest pose at look pitch 0 (rounded to 0.1 m); the owner and the host both add it to the landing, so a rig change that moves
        // the tip without re-deriving the constant would silently shift every landing relative to the real rod tip (and the owner's start line).
        [UnityTest]
        public IEnumerator RodTip_ForwardReachAtPitchZero_MatchesCastTipReach()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            PlayerCamera camera = rig.Player.GetComponent<PlayerCamera>();
            Assert.That(camera, Is.Not.Null, "PlayerCamera missing");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Assert.That(tip, Is.Not.Null, "TipAnchor missing");

            yield return FishingPlayModeDriver.SetPitch(rig, camera, 0f);
            float forwardPitch0 = ForwardReach(rig.Player.transform, tip);
            Vector3 d = tip.position - rig.Player.transform.position;
            float flatPitch0 = new Vector2(d.x, d.z).magnitude;

            // The rod in hand (equipped) must not move the anchor: the arm pose is camera-space, independent of the tool.
            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { EquipPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, PlayModeHarness.DefaultTimeoutSeconds, "Equip never set the Rod tool");
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            float forwardEquipped = ForwardReach(rig.Player.transform, tip);

            float rounded = Mathf.Round(forwardPitch0 * 10f) / 10f;
            TestContext.WriteLine($"FG R67 castTipReach: forwardReach(pitch 0)={forwardPitch0:F4} flatReach={flatPitch0:F4} equipped={forwardEquipped:F4} rounded={rounded:F1} constant={FishingConstants.CastTipReach:F1}");
            Assert.That(forwardEquipped, Is.EqualTo(forwardPitch0).Within(0.01f), "equipping the rod must not move the TipAnchor reach");
            Assert.That(FishingConstants.CastTipReach, Is.EqualTo(forwardPitch0).Within(0.05f + 1e-3f),
                "CastTipReach must be the measured pitch-0 forward reach (" + forwardPitch0.ToString("F4") + ") to within 0.05 m");
            Assert.That(FishingConstants.CastTipReach, Is.EqualTo(rounded).Within(0.01f),
                "CastTipReach must equal the measured forward reach rounded to 0.1 m (re-derive it after changing the rod rig)");
            rig.Log.AssertEmpty("cast tip reach at pitch 0");
        }

        // Horizontal forward reach of the tip from the root, along the player's flat forward.
        private static float ForwardReach(Transform player, Transform tip)
        {
            Vector3 d = tip.position - player.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            return Vector3.Dot(new Vector3(d.x, 0f, d.z), flatForward);
        }

        // Catches (T19-R1): the owner's start line (rod tip to bobber, FightSim's first LineLength) shorter than the host's LineLength0
        // (hostDistance - HookedLineSlack, floored at the landing distance): the host Landed floor would then reject an honest catch.
        // G9: the short 0.15 charge from the deck end. Note: this short cast cannot discriminate the slack (a wrong HookedLineSlack still passes here);
        // only the max-cast test below and the reach sweep (RodTip_MaxHorizontalReachOverPitch_FitsHookedLineSlack) can tell a too-small one.
        [UnityTest]
        public IEnumerator SoloHost_OwnerStartLine_CoversHostLineLength0_AtG9Cast()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 6f);
            yield return AssertOwnerLineCoversHost(rig, "G9");
            rig.Log.AssertEmpty("owner start line at the G9 cast");
        }

        // Same, at a (near) full-power cast: the longest distance the rod can throw, where a slack smaller than the tip reach shows most.
        [UnityTest]
        public IEnumerator SoloHost_OwnerStartLine_CoversHostLineLength0_AtMaxCast()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeToMax(rig);
            yield return FishingPlayModeDriver.Release(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 8f);
            ActiveCast cast = default;
            yield return AssertOwnerLineCoversHost(rig, "max", c => cast = c);
            float rodMax = rig.Registry.Fishing.Rods[rig.Registry.RodIndex("rod_handmade")].CastMaxDist;
            Assert.That(cast.CastDistance, Is.GreaterThan(0.85f * rodMax), "this must be a max-distance cast (rod max " + rodMax + ")");
            rig.Log.AssertEmpty("owner start line at the max cast");
        }

        // Waits for the host to record Hooked, then compares the owner's tip-to-landing distance (what BuildFrame feeds FightSim as the
        // start line; the player is still, so it equals the hook-frame value) with the host's LineLength0.
        private static IEnumerator AssertOwnerLineCoversHost(FishingRig rig, string label, Action<ActiveCast> capture = null)
        {
            FishingHost host = FishingHost.Current;
            Assert.That(host, Is.Not.Null, "the solo host's FishingHost");
            ulong client = rig.Player.OwnerClientId;
            ActiveCast cast = default;
            yield return PlayModeHarness.WaitUntil(
                () => host.Ledger.TryGetActive(client, out cast) && cast.Phase == CastPhase.Hooked,
                3f, "the host never recorded the Hooked phase");
            capture?.Invoke(cast);

            float ownerLine = FishingPlayModeDriver.Flat(rig.Controller.RodTipPosition, new Vector3(cast.LandingX, 0f, cast.LandingZ));
            float hostDistance = FishingPlayModeDriver.Flat(rig.Player.transform.position, new Vector3(cast.LandingX, 0f, cast.LandingZ));
            float hostLine = cast.LineLength0;
            TestContext.WriteLine($"FG T19R1 {label}: castDistance={cast.CastDistance:F3} hostDistance={hostDistance:F3} hostLineLength0={hostLine:F3} ownerStartLine={ownerLine:F3} fightLine={rig.Machine.Fight.State.LineLength:F3} slack={FishingConstants.HookedLineSlack:F1}");
            Assert.That(ownerLine, Is.GreaterThanOrEqualTo(hostLine),
                label + ": the owner's start line (" + ownerLine.ToString("F3") + ") must not undercut the host's LineLength0 (" + hostLine.ToString("F3") + ")");
        }

        // Catches: the movement lock leaking into RodReady (a rod in hand must not freeze the walk).
        [UnityTest]
        public IEnumerator RodOut_MoveInput_StillWalks()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { EquipPressed = true });
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Fake.GameplayContext == InputContext.RodReady, PlayModeHarness.DefaultTimeoutSeconds,
                "the context never became RodReady after Equip");

            Vector3 start = rig.Player.transform.position;
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitGameSeconds(1f, PlayModeHarness.DefaultTimeoutSeconds, "the walk never accumulated 1 s of game time");
            rig.Fake.Frame = default;

            float flat = FishingPlayModeDriver.Flat(rig.Player.transform.position, start);
            Assert.That(flat, Is.GreaterThan(1f), "RodReady must still walk (moved " + flat.ToString("F3") + " m)");
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.RodReady));
            rig.Log.AssertEmpty("rod out, walking");
        }

        // Catches: the Hooked fight not locking movement (unlocked forward input would walk off the deck end into the lake and trip the
        // leash), the jump or sprint slipping through the lock, and the context not being RodFighting while hooked.
        [UnityTest]
        public IEnumerator RodFighting_MoveInput_DoesNotMovePlayer()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 6f);
            yield return PlayModeHarness.WaitFrames(2, 0f);

            Vector3 start = rig.Player.transform.position;
            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { MoveY = 1f, MoveX = 1f, SprintHeld = true, JumpPressed = true });
            rig.Fake.Frame = new PlayerInputFrame { MoveY = 1f, MoveX = 1f, SprintHeld = true };
            yield return PlayModeHarness.WaitGameSeconds(0.5f, PlayModeHarness.DefaultTimeoutSeconds, "the locked hold never accumulated 0.5 s of game time");
            rig.Fake.Frame = default;

            float flat = FishingPlayModeDriver.Flat(rig.Player.transform.position, start);
            Assert.That(flat, Is.LessThan(0.05f), "movement is locked while Hooked (moved " + flat.ToString("F3") + " m)");
            Assert.That(rig.Fake.GameplayContext, Is.EqualTo(InputContext.RodFighting));
            rig.Log.AssertEmpty("rod fighting, movement locked");
            // Mid-fight teardown is fine: OnDisable suppresses sends and the host clears the cast through Disconnect.
        }

        // ---- PlayerCreel runtime checks (T16 review carry-overs) ----

        // Catches: ServerTryAdd storing past the entry cap (the host would let a full creel keep growing), or a refused add still
        // consuming an EntryId / mirroring a ghost entry.
        [UnityTest]
        public IEnumerator PlayerCreel_ServerTryAdd_RefusedWhenFull()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            var item = new CreelItem { ItemIndex = 1, WeightG = 300, ValueCoins = 7, Quality = CatchQuality.Normal };
            Assert.That(rig.Creel.ServerTryAdd(in item, 2, 100000, out CreelItem first), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 2, 100000, out CreelItem second), Is.True);
            Assert.That(rig.Creel.Count, Is.EqualTo(2));
            Assert.That(second.EntryId, Is.Not.EqualTo(first.EntryId), "the ledger assigns distinct entry ids");

            Assert.That(rig.Creel.ServerTryAdd(in item, 2, 100000, out CreelItem refused), Is.False, "entries == max refuses");
            Assert.That(refused.WeightG, Is.EqualTo(0), "a refused add stores nothing");
            Assert.That(rig.Creel.Count, Is.EqualTo(2), "the replicated list did not grow");

            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 600, out _), Is.False, "total weight == max refuses too");
            Assert.That(rig.Creel.Count, Is.EqualTo(2));
            rig.Log.AssertEmpty("creel full");
        }

        // Catches: the debug sell not emptying the replicated list, not paying into NetDebugCoins, or the ledger and the mirror disagreeing.
        [UnityTest]
        public IEnumerator PlayerCreel_DebugBellSell_EmptiesCreelAndAddsCoins()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            Assert.That(Debug.isDebugBuild, Is.True, "the bell sell is a debug-build feature; PlayMode in the editor is one");
            DockBell bell = UnityEngine.Object.FindFirstObjectByType<DockBell>();
            Assert.That(bell, Is.Not.Null, "the DockBell anchor must have spawned in Spot_LakeDock");

            var item = new CreelItem { ItemIndex = 1, WeightG = 300, ValueCoins = 7, Quality = CatchQuality.Normal };
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            int before = rig.Creel.NetDebugCoins.Value;

            bell.ServerInteract(NetworkManager.ServerClientId);

            Assert.That(rig.Creel.Count, Is.EqualTo(0), "the creel is empty after the sell");
            Assert.That(rig.Creel.NetDebugCoins.Value, Is.EqualTo(before + 14), "coins = the two items' value");
            rig.Log.AssertEmpty("debug bell sell");
        }

        // Catches: a respawned player object (same slot) not mirroring the ledger creel and wallet (R46), or the mirror reading a slot
        // before NetSlot is written (it would show the wrong slot's items).
        [UnityTest]
        public IEnumerator PlayerCreel_MirrorsLedger_AfterRespawn()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            var item = new CreelItem { ItemIndex = 2, WeightG = 450, ValueCoins = 9, Quality = CatchQuality.Normal };
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerSellAll(), Is.EqualTo(18), "precondition: sell once so the wallet is non-zero");
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            Assert.That(rig.Creel.ServerTryAdd(in item, 10, 100000, out _), Is.True);
            byte slot = rig.Player.Slot;

            // A real respawn: despawn the host's player, then run PlayerSpawner's idempotent entry (internal) as OnLoadEventCompleted would.
            NetworkObject old = rig.Player.GetComponent<NetworkObject>();
            old.Despawn(true);
            yield return PlayModeHarness.WaitUntil(() => PlayerNetState.Local == null, PlayModeHarness.DefaultTimeoutSeconds, "the old player never despawned");

            PlayerSpawner spawner = PlayerSpawner.Current;
            Assert.That(spawner, Is.Not.Null, "PlayerSpawner missing");
            spawner.SpawnPlayerIfMissing(NetworkManager.Singleton, NetworkManager.ServerClientId);

            yield return PlayModeHarness.WaitUntil(() => PlayerNetState.Local != null, PlayModeHarness.DefaultTimeoutSeconds, "the respawned player never appeared");
            PlayerCreel creel = PlayerNetState.Local.GetComponent<PlayerCreel>();
            yield return PlayModeHarness.WaitUntil(() => creel.Count == 2, 3f, "the respawned creel never mirrored the ledger");
            Assert.That(PlayerNetState.Local.Slot, Is.EqualTo(slot), "same slot");
            Assert.That(creel.NetCreel[0].ItemIndex, Is.EqualTo(item.ItemIndex));
            Assert.That(creel.NetCreel[0].WeightG, Is.EqualTo(item.WeightG));
            Assert.That(creel.NetDebugCoins.Value, Is.EqualTo(18), "the wallet survives the respawn");
            rig.Log.AssertEmpty("creel mirror after respawn");
        }

        // Catches (T27 review): FightTelemetry writing the CSV header on every row (or not at all), dropping a row, or not appending. The scene's
        // FightTelemetry is attached to the local controller; the event is raised through its backing field, so the real subscription runs.
        // Its file is persistentDataPath/fight-log<suffix>.csv; the run must use the _fgtest profile (Tools/unity.ps1 passes -dev-profile fgtest),
        // otherwise the test is ignored rather than touching the developer's own fight log. The file is deleted before and after.
        [UnityTest]
        public IEnumerator SoloHost_FightTelemetry_TwoFightEnded_OneHeaderTwoRows()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            FightTelemetry telemetry = UnityEngine.Object.FindFirstObjectByType<FightTelemetry>();
            Assert.That(telemetry, Is.Not.Null, "the Fishing root's FightTelemetry");
            string path = telemetry.FilePath;
            if (!path.EndsWith("_fgtest.csv", StringComparison.Ordinal))
                Assert.Ignore("FightTelemetry writes " + path + " (not the _fgtest profile); run through Tools/unity.ps1 (-dev-profile fgtest).");

            System.Reflection.FieldInfo field = typeof(FishingRodController).GetField("FightEnded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "the FightEnded event's backing field");
            var raise = (Action<FightEnded>)field.GetValue(rig.Controller);
            Assert.That(raise, Is.Not.Null, "FightTelemetry subscribed to the local controller's FightEnded");

            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            try
            {
                raise(new FightEnded("steady", "bluegill", "rod_handmade", 250, 7.5f, 3.2f, 11.1f, FightOutcome.Landed, EscapeReason.None, 1, 0.62f, false, true, false));
                raise(new FightEnded("darter", "perch", "rod_twig", 400, 9.25f, 4.5f, 6.75f, FightOutcome.Escaped, EscapeReason.ThrewHook, 0, 0.91f, true, false, true));

                string[] lines = System.IO.File.ReadAllLines(path);
                Assert.That(lines.Length, Is.EqualTo(3), "one header and two rows:\n" + string.Join("\n", lines));
                Assert.That(lines[0], Is.EqualTo(FightLogCsv.Header), "the header is the first line");
                Assert.That(lines[1], Does.StartWith("steady,bluegill,250,"), "row 1");
                Assert.That(lines[2], Does.StartWith("darter,perch,400,"), "row 2");
                int headers = 0;
                foreach (string line in lines) if (line == FightLogCsv.Header) headers++;
                Assert.That(headers, Is.EqualTo(1), "exactly one header");
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            rig.Log.AssertEmpty("fight telemetry");
        }
    }
}
