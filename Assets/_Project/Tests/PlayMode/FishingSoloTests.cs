// Authority: test-only. T19 first end-to-end run of the fishing loop through the real solo host: owner FishingRodController ->
// FishingRodNetwork RPCs -> FishingHost ledger -> CommitRpc / CatchAnnouncedRpc -> creel. Input is scripted through FakeInputService.
// Fixture G9: the player is teleported to (0, 0.65, -41) facing +z, 1 m inside the dock end, so a 0.15 charge lands over open water.
// FishingPlayModeDriver is shared with later PlayMode tests; its member names are frozen (ruling F-Q7).
using System;
using System.Collections;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
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
            Assert.That(rig.Controller.MarkerValid, Is.True, "the cast marker (2.5 m ahead of the G9 spot) must be on water");
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

        // Counter-steers each burst, rests the reel above 0.8 tension and resumes below 0.5; holds the reel off during a dive-less pause.
        public static IEnumerator FightRhythm(FishingRig rig, float timeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            bool rest = false;
            while (rig.Machine.State == RodState.Hooked)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("the fight did not end within " + timeoutSeconds + " s");
                FightState s = rig.Machine.Fight.State;
                bool burst = s.Phase == FightPhase.Burst;
                float steer = burst && s.BurstDir != 0 ? -s.BurstDir : 0f;
                if (s.Tension >= 0.8f) rest = true;
                else if (s.Tension < 0.5f) rest = false;
                bool reel = !rest && !(burst && s.BurstDir == 0);
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

            yield return FishingPlayModeDriver.FightRhythm(rig, 60f);
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

        // Catches (T19-R1): the owner's start line (rod tip to bobber, FightSim's first LineLength) shorter than the host's LineLength0
        // (hostDistance - HookedLineSlack, floored at the landing distance): the host Landed floor would then reject an honest catch.
        // G9: the short 0.15 charge from the deck end.
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
    }
}
