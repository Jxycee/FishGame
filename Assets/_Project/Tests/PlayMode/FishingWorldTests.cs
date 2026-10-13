// Authority: test-only. T15: the fishing host and the owner marker on the real M3 world (WorldState + the baked lake shore grid), the session run
// state across the dev map switch (lake -> lake, D-F5) and the reservation-leak guard (D-F6). Reuses FishingPlayModeDriver unchanged.
// Every test pins the world first (clock stopped, Summer, Clear, minute 300): a session otherwise starts in Autumn Fog from the fixed dev
// seed, and a natural hotspot appears 45-90 s in and takes 2-3 ripple slots, so long HasRipple checks would flake.
using System.Collections;
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
    public class FishingWorldTests
    {
        private const float ReloadTimeoutSeconds = 20f;

        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            FishingDebugSettings.Luck = 0f;   // the shared driver's ResetDebugSettings predates Luck
            EcologySession.Clear();           // SubsystemRegistration runs once per Play Mode entry, not per test
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Clock stopped at minute 300 of day 1, Summer, Clear. DevSetSeason writes the session run state (cleared in TearDown).
        private static DevWorldDriver Pin()
        {
            Assert.That(WorldState.Current, Is.Not.Null, "no WorldState in the scene");
            DevWorldDriver driver = WorldState.Current.GetComponent<DevWorldDriver>();
            Assert.That(driver, Is.Not.Null, "the World root has a DevWorldDriver");
            driver.DevSetSeason(Season.Summer);
            driver.DevSetClockRunning(false);
            driver.DevSetWeatherOverride(WeatherMask.Clear);
            driver.DevSetMinute(300);
            return driver;
        }

        private static IEnumerator ArrangePinned(FishingRig rig)
        {
            yield return FishingPlayModeDriver.Arrange(rig);
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10f,
                "WorldState.Current did not spawn with state");
            Pin();
            yield return PlayModeHarness.WaitFrames(2, 0f);
        }

        private static IEnumerator CastAndWaitForWaiting(FishingRig rig)
        {
            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
        }

        private static IEnumerator CancelToIdle(FishingRig rig)
        {
            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            rig.Fake.Frame = default;
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 2f);
        }

        // After a map switch every spawned object is new: refresh the rig's references (never rig.Subscribe again: it would overwrite the
        // bus subscription without disposing it) and wait for the new loadout mirror.
        private static IEnumerator WaitForReloadedRig(FishingRig rig, WorldState oldWorld, FishingHost oldHost, PlayerNetState oldPlayer)
        {
            yield return PlayModeHarness.WaitUntil(
                () => WorldState.Current != null && WorldState.Current != oldWorld && WorldState.Current.HasState
                      && FishingHost.Current != null && FishingHost.Current != oldHost
                      && PlayerNetState.Local != null && PlayerNetState.Local != oldPlayer
                      && FishingRodController.Local != null,
                ReloadTimeoutSeconds, "the map switch never produced a new world, host, local player and rod controller");

            rig.Player = PlayerNetState.Local;
            rig.Net = rig.Player.GetComponent<FishingRodNetwork>();
            rig.Creel = rig.Player.GetComponent<PlayerCreel>();
            rig.Locomotion = rig.Player.GetComponent<PlayerLocomotion>();
            rig.Controller = FishingRodController.Local;
            ushort handmade = rig.Registry.RodIndex("rod_handmade");
            ushort worm = rig.Registry.BaitIndex("bait_worm");
            yield return PlayModeHarness.WaitUntil(
                () => rig.Net.NetRodIndex.Value == handmade && rig.Net.NetSelectedBait.Value == worm && rig.Net.NetSelectedBaitCount.Value == 20,
                PlayModeHarness.DefaultTimeoutSeconds, "the loadout mirror (rod_handmade, bait_worm x20) never landed after the map switch");
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds,
                "the respawned player never became grounded");
        }

        private static IEnumerator TeleportToDeckEnd(FishingRig rig)
        {
            rig.Locomotion.ApplyTeleport(new Vector3(0f, 0.65f, FishingPlayModeDriver.DeckTeleportZ), 0f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds,
                "the player never became grounded after the G9 teleport");
        }

        private static IEnumerator PublishMapSwitch(string spotId)
        {
            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "no App EventBus");
            bus.Publish(new DevLoadSpotRequested(spotId));
            yield return null;
        }

        // Drives the ledger directly as the rod's owner client (the guard walks the live rods, so a stand-in client id would not be released)
        // until one honest cast holds a catch-limit reservation. The roll is a pure function of (seed, counter), so the search is deterministic;
        // only the real lake specials reserve (limit 1), forced rolls never do. The cast stays Waiting (forced 30 s bite) and is never settled
        // here: the caller proves the host releases it.
        private static IEnumerator HoldReservation(FishingRig rig, System.Action<ushort> species)
        {
            FishingHostLedger ledger = FishingHost.Current.Ledger;
            Assert.That(ledger, Is.Not.Null, "the host ledger");
            WorldState world = WorldState.Current;
            Assert.That(SpotEcology.TryResolve(rig.Registry, world, out int zoneIndex, out ZoneData zone), Is.True, "the lake zone");
            byte slot = rig.Player.Slot;
            ulong client = rig.Player.OwnerClientId;
            Vector3 p = rig.Player.transform.position;
            PlayerLoadout loadout = ledger.GetLoadout(slot);
            RollOverrides ov = RollOverrides.None;
            ov.ForcedBiteWait = 30f;
            EcologyRunState run = ledger.Run;

            for (uint id = 1000; id < 1000 + 4000; id++)
            {
                var req = new CastRequestData { CastId = id, OriginX = p.x, OriginY = p.y, OriginZ = p.z, YawDeg = 0f, ChargeNorm = 0.4f,
                                                BaitIndex = loadout.BaitIndex };
                Assert.That(ledger.TryComputeLanding(slot, in req, out float lx, out float lz), Is.True);
                var view = new HostCastView
                {
                    PlayerX = p.x, PlayerY = p.y, PlayerZ = p.z, CastingAllowed = true, GridWater = SpotEcology.GridWater(zone, lx, lz),
                    Probe = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 3f },
                    Context = SpotEcology.WorldContext(world, zone, zoneIndex, lx, lz, 0f, 1),
                };
                CastVerdict v = ledger.RequestCast(client, slot, in req, in view, in ov);
                Assert.That(v.Valid, Is.True, "direct cast " + id + ": " + v.Reason);
                Assert.That(ledger.TryGetActive(client, out ActiveCast active), Is.True);
                if (active.LimitReserved)
                {
                    ushort s = (ushort)v.Roll.Catch.ItemIndex;
                    Assert.That(run.Catches.Reserved(s), Is.EqualTo(1), "the cast holds one reservation");
                    TestContext.WriteLine("FG T15 reservation held after " + (id - 999) + " direct casts: species " + rig.Registry.SpeciesAt(s).Id);
                    species(s);
                    yield break;
                }
                ledger.Cancel(client, id);
            }
            Assert.Fail("no honest cast reserved a catch limit within 4000 rolls (does the lake still have a limited special?)");
        }

        // Catches (F5): a world whose replicated values disagree with the driver that wrote them (season, weather segment 0, running clock).
        [UnityTest]
        public IEnumerator FishingWorld_FreshWorld_ReplicatesDriverValues()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10f,
                "WorldState.Current did not spawn with state");

            WorldState world = WorldState.Current;
            DevWorldDriver driver = world.GetComponent<DevWorldDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.Run, Is.Not.Null, "the driver adopted the session run state");
            Assert.That(driver.WeatherOverride, Is.EqualTo(WeatherMask.None), "unpinned: the rolled track decides the weather");
            Assert.That(DayClock.SegmentAt(world.Minute), Is.EqualTo(0), "still in the day's first weather segment");

            Assert.That(world.NetSeason.Value, Is.EqualTo((byte)driver.Run.Season), "NetSeason == Run.Season");
            Assert.That(world.NetWeather.Value, Is.EqualTo((byte)driver.Weather.At(driver.Run.Day, 0)), "NetWeather == the track's segment 0");
            Assert.That(world.NetClock.Value.Running, Is.EqualTo((byte)1), "NetClock.Running == 1");
            Assert.That(driver.ClockRunning, Is.True);
            Assert.That(world.CastingOpen, Is.True);
            Assert.That(ReferenceEquals(EcologySession.Current, driver.Run), Is.True, "one session run state");
            Assert.That(FishingHost.Current.Ledger.Run, Is.SameAs(driver.Run), "the ledger runs on the session run state");
            _rig.Log.AssertEmpty("fresh world");
        }

        // Catches: a host that rejects an honest deck-end cast under the real gates (grid water, casting open), a context the resolver throws
        // on (default Events/Skills), and an owner marker that disagrees with the host at the G9 landing.
        [UnityTest]
        public IEnumerator FishingWorld_UnforcedCast_AcceptedWithWorldContext()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            FishingDebugSettings.ForcedSpeciesId = null;
            _rig = new FishingRig();
            yield return ArrangePinned(_rig);
            FishingRig rig = _rig;

            Assert.That(WorldState.Current.HasState, Is.True);
            Assert.That(WorldState.Current.CastingOpen, Is.True);
            uint before = FishingHost.Current.Ledger.HostCastCounter;

            yield return FishingPlayModeDriver.EquipAndReady(rig);   // asserts MarkerValid: the owner gates pass at the G9 landing
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);

            Assert.That(FishingHost.Current.Ledger.TryGetActive(rig.Player.OwnerClientId, out ActiveCast cast), Is.True,
                "the host accepted: the context was built and the resolver did not throw");
            Assert.That(FishingHost.Current.Ledger.HostCastCounter, Is.EqualTo(before + 1));
            Assert.That(SpotEcology.GridWater(WorldZone(rig), cast.LandingX, cast.LandingZ), Is.True, "the host landing is a grid water cell");

            yield return CancelToIdle(rig);
            rig.Log.AssertEmpty("unforced world cast");
        }

        // Catches: the owner marker ignoring the replicated clock (a cast the host will refuse NotAllowedNow), and a marker that stays red
        // after the clock is back (a stale gate cache).
        [UnityTest]
        public IEnumerator FishingWorld_DayOver_OwnerMarkerClosesAndReopens()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            _rig = new FishingRig();
            yield return ArrangePinned(_rig);
            FishingRig rig = _rig;
            DevWorldDriver driver = WorldState.Current.GetComponent<DevWorldDriver>();

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            Assert.That(rig.Controller.MarkerValid, Is.True);

            driver.DevSetMinute(1200);   // stopped clock: exactly the day-over minute
            yield return PlayModeHarness.WaitUntil(() => !WorldState.Current.CastingOpen, 3f, "the clock never closed casting");
            yield return PlayModeHarness.WaitFrames(3, 0f);
            Assert.That(rig.Controller.MarkerVisible, Is.True, "the marker is still shown");
            Assert.That(rig.Controller.MarkerValid, Is.False, "but red: casting is closed (M3-D16)");

            driver.DevSetMinute(300);
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current.CastingOpen, 3f, "the clock never reopened casting");
            yield return PlayModeHarness.WaitFrames(3, 0f);
            Assert.That(rig.Controller.MarkerValid, Is.True, "valid again");
            rig.Log.AssertEmpty("day over marker");
        }

        // Catches (CCR-D3, M3-D17, T1): the map switch dropping the session run state (cast counter, season), re-rolling it, wiping the dev
        // overrides, or restarting the clock somewhere other than minute 0; and any warning on the switch path (C2 #9).
        [UnityTest]
        public IEnumerator FishingWorld_LakeReload_KeepsRunStateAndCastCounter()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            FishingDebugSettings.Luck = 0.5f;
            _rig = new FishingRig();
            yield return ArrangePinned(_rig);
            FishingRig rig = _rig;

            EcologyRunState run = EcologySession.Current;
            Assert.That(run, Is.Not.Null);
            uint start = run.HostCastCounter;
            yield return CastAndWaitForWaiting(rig);
            yield return CancelToIdle(rig);

            Season season = run.Season;
            uint n = run.HostCastCounter;
            Assert.That(n, Is.EqualTo(start + 1), "one accepted cast so far");
            WorldState oldWorld = WorldState.Current;
            FishingHost oldHost = FishingHost.Current;
            PlayerNetState oldPlayer = PlayerNetState.Local;

            yield return PublishMapSwitch(ContentIds.LakeDock);
            yield return WaitForReloadedRig(rig, oldWorld, oldHost, oldPlayer);

            Assert.That(ReferenceEquals(EcologySession.Current, run), Is.True, "the session run state survives the switch");
            Assert.That(run.Season, Is.EqualTo(season));
            Assert.That(run.HostCastCounter, Is.EqualTo(n));
            Assert.That(FishingHost.Current.Ledger, Is.Not.Null);
            Assert.That(FishingHost.Current.Ledger.Run, Is.SameAs(run), "the new host's ledger runs on the same run state");
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(0.5f), "dev overrides survive a Spot load");
            Assert.That(WorldState.Current.Minute, Is.LessThan(30), "the clock restarted at minute 0 of the same day");
            Assert.That(WorldState.Current.NetSeason.Value, Is.EqualTo((byte)season), "the new world shows the kept season");

            Pin();   // the new driver starts a running clock, rolled weather and a hotspot planner
            yield return PlayModeHarness.WaitFrames(2, 0f);
            yield return TeleportToDeckEnd(rig);
            yield return CastAndWaitForWaiting(rig);
            yield return CancelToIdle(rig);
            Assert.That(run.HostCastCounter, Is.EqualTo(n + 1), "the counter continues across the switch");
            rig.Log.AssertEmpty("lake reload");
        }

        // Catches (F2, D-F6): FishingHost.OnDisable dropping the ledger while a catch-limit reservation is held. The run state outlives the
        // ledger (EcologySession), so a leaked reservation would block that species for the rest of the session. Disabling the host leaves the
        // rods alive, so only the OnDisable guard (not the rod-despawn path) can release it.
        [UnityTest]
        public IEnumerator FishingWorld_HostDisabled_ReleasesHeldReservation()
        {
            _rig = new FishingRig();
            yield return ArrangePinned(_rig);
            FishingRig rig = _rig;

            ushort held = 0;
            yield return HoldReservation(rig, s => held = s);
            EcologyRunState run = FishingHost.Current.Ledger.Run;
            Assert.That(run.Catches.Reserved(held), Is.EqualTo(1), "precondition: a reservation is held");

            FishingHost.Current.enabled = false;
            Assert.That(run.Catches.Reserved(held), Is.EqualTo(0), "the guard released the reservation before the ledger was dropped");
            Assert.That(FishingHost.Current, Is.Null, "disabled: no longer current");
            rig.Log.AssertEmpty("host disabled");
        }

        // Catches (F2 acceptance): a scene reload while a reservation is held leaving Reserved > 0 on the session run state.
        [UnityTest]
        public IEnumerator FishingWorld_LakeReload_WhileReservationHeld_LeavesNoReservation()
        {
            _rig = new FishingRig();
            yield return ArrangePinned(_rig);
            FishingRig rig = _rig;

            ushort held = 0;
            yield return HoldReservation(rig, s => held = s);
            EcologyRunState run = EcologySession.Current;
            Assert.That(run.Catches.Reserved(held), Is.EqualTo(1), "precondition: a reservation is held");
            WorldState oldWorld = WorldState.Current;
            FishingHost oldHost = FishingHost.Current;
            PlayerNetState oldPlayer = PlayerNetState.Local;

            yield return PublishMapSwitch(ContentIds.LakeDock);
            yield return WaitForReloadedRig(rig, oldWorld, oldHost, oldPlayer);

            Assert.That(run.Catches.Reserved(held), Is.EqualTo(0), "no reservation survives the scene reload");
            rig.Log.AssertEmpty("reload with a held reservation");
        }

        private static ZoneData WorldZone(FishingRig rig)
        {
            Assert.That(SpotEcology.TryResolve(rig.Registry, WorldState.Current, out _, out ZoneData zone), Is.True);
            return zone;
        }
    }
}
