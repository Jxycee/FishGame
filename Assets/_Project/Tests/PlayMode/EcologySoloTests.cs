// Authority: test-only. T24: the M3 systems together in one solo-host process (spec 12.9 PlayMode, 17.3): a lake cast rolled through the resolver lands,
// announces and journals; an off-grid cast and a day-over cast are rejected by the host gates; the world clock runs and stops in solo pause;
// WorldState spawns with a non-zero hash; the harbour loads and lands a fish; the dev map switch keeps the run state; a grid-land cell that the
// physics probe calls water is rejected NotWater (T15 review follow-up). Reuses FishingPlayModeDriver unchanged.
// Counters and journal values are asserted RELATIVE (the fgtest meta file is deleted at boot, but a test never relies on it).
// Rejections are observed through the owner's CastResultReceived event, the host ledger (ActiveCast, HostCastCounter) and the bait mirror.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FishGame.App;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using FishGame.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class EcologySoloTests
    {
        private const float MapSwitchTimeoutSeconds = 20f;
        private const uint ForgedCastIdBase = 100000;   // far above any castId the owner's machine counts up to in a test

        private FishingRig _rig;
        private uint _forgedCastIds = ForgedCastIdBase;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            _forgedCastIds = ForgedCastIdBase;
            FishingPlayModeDriver.ResetDebugSettings();
            FishingDebugSettings.Luck = 0f;   // the shared driver's reset predates Luck
            EcologySession.Clear();           // SubsystemRegistration runs once per Play Mode entry, not per test
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // ---------------------------------------------------------------- helpers

        private static DevWorldDriver Driver()
        {
            Assert.That(WorldState.Current, Is.Not.Null, "no WorldState in the scene");
            DevWorldDriver driver = WorldState.Current.GetComponent<DevWorldDriver>();
            Assert.That(driver, Is.Not.Null, "the World root has a DevWorldDriver");
            return driver;
        }

        // Fixed world: clock stopped at the given minute of the current day, the given season, Clear weather (override).
        private static DevWorldDriver PinWorld(Season season, int minute)
        {
            DevWorldDriver driver = Driver();
            driver.DevSetSeason(season);
            driver.DevSetClockRunning(false);
            driver.DevSetWeatherOverride(WeatherMask.Clear);
            driver.DevSetMinute(minute);
            return driver;
        }

        private static IEnumerator ArrangeWorld(FishingRig rig, bool teleport = true)
        {
            yield return FishingPlayModeDriver.Arrange(rig, teleport);
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10f,
                "WorldState.Current did not spawn with state");
        }

        private static ZoneData ZoneOfWorld(FishingRig rig, out int zoneIndex)
        {
            Assert.That(SpotEcology.TryResolve(rig.Registry, WorldState.Current, out zoneIndex, out ZoneData zone), Is.True, "the world's zone and baked grid");
            return zone;
        }

        // The T15 map-switch wait: publish DevLoadSpotRequested, then wait for the new world (HasState, expected SpotId), host, local player and rod
        // controller, refresh the rig's references (never rig.Subscribe again) and wait for the loadout mirror and the grounded respawn.
        private static IEnumerator SwitchTo(FishingRig rig, string spotId)
        {
            WorldState oldWorld = WorldState.Current;
            FishingHost oldHost = FishingHost.Current;
            PlayerNetState oldPlayer = PlayerNetState.Local;
            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "no App EventBus");
            bus.Publish(new DevLoadSpotRequested(spotId));
            yield return null;

            yield return PlayModeHarness.WaitUntil(
                () => WorldState.Current != null && WorldState.Current != oldWorld && WorldState.Current.HasState && WorldState.Current.SpotId == spotId
                      && FishingHost.Current != null && FishingHost.Current != oldHost
                      && PlayerNetState.Local != null && PlayerNetState.Local != oldPlayer
                      && FishingRodController.Local != null,
                MapSwitchTimeoutSeconds, "the map switch to '" + spotId + "' never produced a new world, host, local player and rod controller");

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
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "the respawned player never became grounded");
        }

        private static IEnumerator TeleportToDeckEnd(FishingRig rig)
        {
            rig.Locomotion.ApplyTeleport(new Vector3(0f, 0.65f, FishingPlayModeDriver.DeckTeleportZ), 0f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "the player never became grounded after the G9 teleport");
        }

        // From Idle with the rod in hand: hold Secondary until Ready. Does NOT assert the cast marker (the caller decides what it expects).
        private static IEnumerator ReadyRod(FishingRig rig)
        {
            FakeInputService fake = rig.Fake;
            if (rig.Player.Tool != ToolKind.Rod)
            {
                yield return FishingPlayModeDriver.Step(fake, new PlayerInputFrame { EquipPressed = true });
                fake.Frame = default;
                yield return PlayModeHarness.WaitUntil(() => rig.Player.Tool == ToolKind.Rod, PlayModeHarness.DefaultTimeoutSeconds, "Equip never set the Rod tool");
                yield return PlayModeHarness.WaitFrames(2, 0f);
            }

            yield return FishingPlayModeDriver.Step(fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            double deadline = Time.realtimeSinceStartupAsDouble + 2.0;
            while (rig.Machine.State != RodState.Ready)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("the rod never reached Ready while Secondary was held (state " + rig.Machine.State + ")");
                yield return FishingPlayModeDriver.Step(fake, new PlayerInputFrame { SecondaryHeld = true });
            }
            yield return PlayModeHarness.WaitFrames(2, 0f);   // so the context is RodAiming and the marker refreshed
        }

        // Let go of everything, then wait (bounded) for the machine to be Idle; used between casts of one test.
        private static IEnumerator ToIdle(FishingRig rig, float timeoutSeconds)
        {
            rig.Fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => rig.Machine.State == RodState.Idle, timeoutSeconds,
                "the rod never returned to Idle (state " + rig.Machine.State + ")");
            yield return PlayModeHarness.WaitFrames(2, 0f);
        }

        private static IEnumerator CastAndWaitForWaiting(FishingRig rig)
        {
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
        }

        private static IEnumerator CancelToIdle(FishingRig rig)
        {
            yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { SecondaryPressed = true, SecondaryHeld = true });
            rig.Fake.Frame = default;
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 2f);
        }

        // One stand point of the scene whose cast marker is valid for a real cast: the rod is left Ready on it.
        private sealed class StandChoice
        {
            public string Name;
            public Vector3 Position;
            public float Facing;
        }

        // Harbour aim (no Editor types in PlayMode): the scene's StandPoint components in hierarchy-name order. A stand point is skipped when the
        // physics probe or the grid already refuses its landing at the charges a test cast uses (0 to 0.25), then each remaining one is teleported
        // to, readied, and kept when the real owner marker is valid. Fails with the list of tried points if none is.
        private static IEnumerator FindCastableStandPoint(FishingRig rig, StandChoice result)
        {
            StandPoint[] stands = UnityEngine.Object.FindObjectsByType<StandPoint>();
            Array.Sort(stands, (a, b) => string.CompareOrdinal(a.name, b.name));
            Assert.That(stands.Length, Is.GreaterThan(0), "the scene has no StandPoint");
            ZoneData zone = ZoneOfWorld(rig, out _);
            float minDist = rig.Registry.Fishing.Tuning.CastMinDist;
            float maxDist = rig.Controller.Rod.CastMaxDist;
            float minLandDepth = rig.Registry.FishingTuning.MinLandDepth;
            var tried = new StringBuilder();

            foreach (StandPoint sp in stands)
            {
                Vector3 p = sp.transform.position;
                bool landingsOk = true;
                foreach (float charge in new[] { 0f, 0.15f, 0.2f, 0.25f })
                {
                    CastMath.Landing(p.x, p.z, sp.FacingDeg, CastMath.Distance(charge, minDist, maxDist), out float lx, out float lz);
                    if (CastMath.ProbeReason(CastProbe.Probe(lx, lz), minLandDepth) != CastRejectReason.None || !SpotEcology.GridWater(zone, lx, lz))
                    {
                        landingsOk = false;
                        break;
                    }
                }
                if (!landingsOk)
                {
                    tried.Append(sp.name).Append(" (landing refused at charge 0..0.25); ");
                    continue;
                }

                rig.Locomotion.ApplyTeleport(p + new Vector3(0f, 0.1f, 0f), sp.FacingDeg);
                yield return PlayModeHarness.WaitFrames(3, 0.05f);
                yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded at " + sp.name);
                rig.Fake.Frame = default;
                yield return PlayModeHarness.WaitUntil(() => rig.Machine.State == RodState.Idle, 2f, "the rod never settled to Idle after the teleport to " + sp.name);
                yield return ReadyRod(rig);
                if (rig.Controller.MarkerValid)
                {
                    result.Name = sp.name;
                    result.Position = rig.Player.transform.position;
                    result.Facing = sp.FacingDeg;
                    TestContext.WriteLine("FG T24 stand point " + sp.name + " at " + p + " facing " + sp.FacingDeg + ": marker valid");
                    yield break;
                }
                tried.Append(sp.name).Append(" (marker invalid); ");
            }
            Assert.Fail("no StandPoint gave a valid cast marker; tried: " + tried);
        }

        // Forges one CastRequestRpc as the rod's owner and waits for the host's verdict as the owner receives it.
        private sealed class ForgedVerdict
        {
            public bool Received;
            public bool Valid;
            public CastRejectReason Reason;
            public uint CastId;
        }

        private IEnumerator ForgeCast(FishingRig rig, Vector3 origin, float yawDeg, float charge, ForgedVerdict verdict)
        {
            uint castId = ++_forgedCastIds;
            verdict.CastId = castId;
            verdict.Received = false;
            Action<uint, bool, CastRejectReason, Vector3, FishingRoll> handler = (id, valid, reason, landing, roll) =>
            {
                if (id != castId) return;
                verdict.Received = true;
                verdict.Valid = valid;
                verdict.Reason = reason;
            };
            rig.Net.CastResultReceived += handler;
            try
            {
                rig.Net.CastRequestRpc(castId, origin, yawDeg, charge, rig.Net.NetSelectedBait.Value, false);
                yield return PlayModeHarness.WaitUntil(() => verdict.Received, 3f, "the host never answered the forged cast " + castId);
            }
            finally
            {
                rig.Net.CastResultReceived -= handler;
            }
        }

        private static void AssertNoActiveCast(FishingRig rig, string why)
        {
            Assert.That(FishingHost.Current.Ledger.TryGetActive(rig.Player.OwnerClientId, out _), Is.False, "no active cast on the host: " + why);
        }

        // ---------------------------------------------------------------- tests

        // Catches: a roll that bypasses the resolver (forced species, a context the resolver is never given); the journal recorder not attached or not
        // saving; a journal entry keyed by the wrong spot; an announcement that disagrees with the creel; the world values (season, minute, weather)
        // not reaching the host's context.
        [UnityTest]
        public IEnumerator EcologySolo_LakeCastThroughResolver_LandsAnnouncesAndJournals()
        {
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig);   // G9
            FishingRig rig = _rig;
            ContentRegistry registry = rig.Registry;
            EcologyDatabase eco = registry.Ecology;
            FishingDatabase db = eco.Fishing;
            var resolver = new FishingResolver(eco);
            ZoneData zone = ZoneOfWorld(rig, out int zoneIndex);
            ushort handmade = registry.RodIndex("rod_handmade");
            ushort worm = registry.BaitIndex("bait_worm");
            EcologyRunState run = EcologySession.Current;
            Assert.That(run, Is.Not.Null, "the session run state");

            // The G9 short cast lands in deep_water (sub-area 2): checked from the zone grid at both ends of the charge the helper accepts (0.15 to 0.6).
            Vector3 p0 = rig.Player.transform.position;
            float minDist = db.Tuning.CastMinDist;
            float maxDist = rig.Controller.Rod.CastMaxDist;
            foreach (float charge in new[] { 0.15f, 0.6f })
            {
                CastMath.Landing(p0.x, p0.z, 0f, CastMath.Distance(charge, minDist, maxDist), out float gx, out float gz);
                Assert.That(zone.Grid.IsWater(gx, gz), Is.True, "G9 landing at charge " + charge + " is a grid water cell");
                Assert.That(zone.SubAreaIndexFor(zone.Grid.ShoreM(gx, gz)), Is.EqualTo((byte)2),
                    "G9 landing at charge " + charge + " is deep_water (shore " + zone.Grid.ShoreM(gx, gz) + " m)");
            }

            // World context: Spring / Midday / Clear if only Steady and Darter fish can bite there (a short fight); else the first season and minute
            // whose deep_water set is Steady/Darter only. The set is logged either way.
            Season season = Season.Spring;
            int minute = 360;
            if (!OnlyShortFighters(registry, db, resolver, zone, zoneIndex, season, minute, handmade, worm, run.Catches))
            {
                bool found = false;
                foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter })
                {
                    foreach (int m in new[] { 30, 200, 360, 560, 760, 950 })
                    {
                        if (found) break;
                        if (OnlyShortFighters(registry, db, resolver, zone, zoneIndex, s, m, handmade, worm, run.Catches))
                        {
                            season = s; minute = m; found = true;
                        }
                    }
                }
                Assert.That(found, Is.True, "no season/minute gives a Steady/Darter-only deep_water set at the Handmade rod (the T12 rules changed; retune this test)");
                TestContext.WriteLine("FG T24 Spring/360 holds a heavy fighter at deep_water; using " + season + " minute " + minute);
            }
            TestContext.WriteLine("FG T24 deep_water set " + season + " minute " + minute + ": " + DescribeEligible(registry, db, resolver, zone, zoneIndex, 2, season, minute, handmade, worm, run.Catches));

            var eligibleBeforeCasts = new HashSet<ushort>();
            for (byte sub = 0; sub < zone.SubAreas.Count; sub++)
            {
                foreach (bool specials in new[] { false, true })
                {
                    var rules = new List<int>();
                    FishingContext ctx = WorldCtx(db, zoneIndex, sub, season, minute, WeatherMask.Clear, handmade, worm);
                    resolver.CollectEligible(ctx, run.Catches, specials, rules);
                    foreach (int r in rules) eligibleBeforeCasts.Add(zone.Rules[r].SpeciesIndex);
                }
            }

            PinWorld(season, minute);
            yield return PlayModeHarness.WaitFrames(2, 0f);
            Assert.That(WorldState.Current.Season, Is.EqualTo(season));
            Assert.That(WorldState.Current.Minute, Is.EqualTo(minute));
            Assert.That(WorldState.Current.Weather, Is.EqualTo(WeatherMask.Clear));
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = null;

            Assert.That(ServiceRoots.App.TryGet(out SaveService save), Is.True, "no SaveService in the App services");
            int announcedAtStart = rig.Announced.Count;
            int creelAtStart = rig.Creel.Count;
            uint castsAtStart = run.HostCastCounter;
            Dictionary<string, int[]> journalBefore = SnapshotJournal(save);

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            CatchAnnounced landed = default;
            bool gotFish = false;
            int scanFrom = announcedAtStart;
            for (int attempt = 0; attempt < 4 && !gotFish; attempt++)
            {
                if (attempt > 0) yield return ReadyRod(rig);
                yield return FishingPlayModeDriver.ChargeAndRelease(rig);
                yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
                Assert.That(FishingHost.Current.Ledger.TryGetActive(rig.Player.OwnerClientId, out ActiveCast active), Is.True, "the host accepted cast " + attempt);
                byte sub = zone.SubAreaIndexFor(zone.Grid.ShoreM(active.LandingX, active.LandingZ));
                Assert.That(sub, Is.EqualTo((byte)2), "the cast landed in deep_water");

                yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 8f);
                yield return FishingPlayModeDriver.FightRhythm(rig, 150f);
                RodState end = rig.Machine.State;
                yield return ToIdle(rig, 6f);
                if (end == RodState.Landed)
                {
                    int sinceStart = scanFrom;
                    yield return PlayModeHarness.WaitUntil(() => LastNonPity(rig, sinceStart) != null, 3f, "the landed catch was never announced");
                    CatchAnnounced last = LastNonPity(rig, sinceStart).Value;
                    if (last.Announcement.Kind == AnnounceKind.Landed && !last.Announcement.Catch.IsJunk)
                    {
                        landed = last;
                        gotFish = true;
                    }
                    else
                    {
                        scanFrom = rig.Announced.Count;   // a junk landing: the next attempt looks at newer announcements only
                    }
                }
            }
            Assert.That(gotFish, Is.True, "no fish (only junk or losses) in 4 casts at the deep_water context");
            yield return PlayModeHarness.WaitFrames(3, 0.05f);   // the recorder handles the same bus message

            Assert.That(landed.IsLocalCatcher, Is.True, "the local catcher");
            Assert.That(landed.Announcement.CatcherSlot, Is.EqualTo(rig.Player.Slot));
            Assert.That(run.HostCastCounter, Is.GreaterThan(castsAtStart), "the host counted the casts");
            Assert.That(rig.Creel.Count, Is.GreaterThan(creelAtStart), "the landed fish is in the creel");

            ushort speciesIndex = landed.Announcement.Catch.ItemIndex;
            string speciesId = registry.SpeciesAt(speciesIndex).Id;
            // The species passes a lake rule in this world context for some sub-area (specials included); the set was taken before the casts,
            // because a landed limited special (gilded_carp, 1 per run) leaves the live set afterwards.
            Assert.That(eligibleBeforeCasts, Does.Contain(speciesIndex),
                        "the rolled species '" + speciesId + "' passes no lake rule at " + season + " minute " + minute + " (a roll that bypassed the resolver)");

            // Journal in memory: relative to the snapshot, counting exactly the announcements of this species the recorder saw.
            int[] before = journalBefore.TryGetValue(speciesId, out int[] b) ? b : new int[3];
            int seenDelta = 0, crewDelta = 0, caughtDelta = 0;
            for (int i = announcedAtStart; i < rig.Announced.Count; i++)
            {
                CatchAnnounced a = rig.Announced[i];
                AnnounceKind kind = a.Announcement.Kind;
                if (a.Announcement.Catch.IsJunk || a.Announcement.Catch.ItemIndex != speciesIndex) continue;
                if (kind != AnnounceKind.Landed && kind != AnnounceKind.Lost && kind != AnnounceKind.ReleasedCreelFull) continue;
                seenDelta++;
                if (kind == AnnounceKind.Landed || kind == AnnounceKind.ReleasedCreelFull)
                {
                    crewDelta++;
                    if (a.IsLocalCatcher) caughtDelta++;
                }
            }
            Assert.That(seenDelta, Is.GreaterThanOrEqualTo(1));
            Assert.That(crewDelta, Is.EqualTo(1), "the loop ends on exactly one landed fish");
            Assert.That(caughtDelta, Is.EqualTo(1), "and it is the local catcher's");
            Assert.That(save.Current.Journal.TryGetValue(speciesId, out JournalEntry entry), Is.True, "the journal has an entry for " + speciesId);
            Assert.That(entry.Seen, Is.EqualTo(before[0] + seenDelta), "Seen +" + seenDelta);
            Assert.That(entry.CrewLanded, Is.EqualTo(before[1] + crewDelta), "CrewLanded +" + crewDelta);
            Assert.That(entry.Caught, Is.EqualTo(before[2] + caughtDelta), "Caught +" + caughtDelta);
            Assert.That(entry.SeenInSpots, Does.Contain(ContentIds.LakeDock), "seen in lake_dock");

            // ... and on disk: a second SaveService on the same path holds the same Caught.
            var disk = new SaveService(save.FilePath);
            disk.Load();
            Assert.That(disk.Current.Journal.TryGetValue(speciesId, out JournalEntry onDisk), Is.True, "meta file on disk has the entry");
            Assert.That(onDisk.Caught, Is.EqualTo(entry.Caught), "Caught on disk");
            Assert.That(onDisk.Seen, Is.EqualTo(entry.Seen), "Seen on disk");
            Assert.That(onDisk.SeenInSpots, Does.Contain(ContentIds.LakeDock), "SeenInSpots on disk");
            TestContext.WriteLine("FG T24 landed " + speciesId + " sub-area 2; journal Seen " + entry.Seen + " Caught " + entry.Caught);
            rig.Log.AssertEmpty("resolver cast to journal");
        }

        private static CatchAnnounced? LastNonPity(FishingRig rig, int from)
        {
            for (int i = rig.Announced.Count - 1; i >= from; i--)
            {
                if (rig.Announced[i].Announcement.Kind != AnnounceKind.PityCast) return rig.Announced[i];
            }
            return null;
        }

        private static FishingContext WorldCtx(FishingDatabase db, int zoneIndex, byte sub, Season season, int minute, WeatherMask weather, ushort rodIndex, ushort baitIndex)
        {
            return new FishingContext
            {
                ZoneIndex = zoneIndex, SubAreaIndex = sub, Depth = float.PositiveInfinity,
                Minute = minute, Season = season, Weather = weather,
                RodTier = db.Rods[rodIndex].Tier, Bait = db.Baits[baitIndex].Mods(1), InHotspot = false,
                Luck = 0f, Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = 1,
            };
        }

        // True when the non-special deep_water set is not empty and holds only Steady and Darter species (a short fight).
        private static bool OnlyShortFighters(ContentRegistry registry, FishingDatabase db, FishingResolver resolver, ZoneData zone, int zoneIndex,
                                              Season season, int minute, ushort rodIndex, ushort baitIndex, CatchLedger ledger)
        {
            var rules = new List<int>();
            FishingContext ctx = WorldCtx(db, zoneIndex, 2, season, minute, WeatherMask.Clear, rodIndex, baitIndex);
            resolver.CollectEligible(ctx, ledger, false, rules);
            if (rules.Count == 0) return false;
            foreach (int r in rules)
            {
                string profile = registry.FightProfileAt(db.Species[zone.Rules[r].SpeciesIndex].FightProfileIndex).Id;
                if (profile != "steady" && profile != "darter") return false;
            }
            return true;
        }

        private static string DescribeEligible(ContentRegistry registry, FishingDatabase db, FishingResolver resolver, ZoneData zone, int zoneIndex, byte sub,
                                               Season season, int minute, ushort rodIndex, ushort baitIndex, CatchLedger ledger)
        {
            var sb = new StringBuilder();
            foreach (bool specials in new[] { false, true })
            {
                var rules = new List<int>();
                FishingContext ctx = WorldCtx(db, zoneIndex, sub, season, minute, WeatherMask.Clear, rodIndex, baitIndex);
                resolver.CollectEligible(ctx, ledger, specials, rules);
                foreach (int r in rules)
                {
                    ushort s = zone.Rules[r].SpeciesIndex;
                    sb.Append(registry.SpeciesAt(s).Id).Append('[').Append(registry.FightProfileAt(db.Species[s].FightProfileIndex).Id)
                      .Append(specials ? ",special" : "").Append("] ");
                }
            }
            return sb.ToString();
        }

        private static Dictionary<string, int[]> SnapshotJournal(SaveService save)
        {
            var map = new Dictionary<string, int[]>();
            foreach (KeyValuePair<string, JournalEntry> kv in save.Current.Journal) map[kv.Key] = new[] { kv.Value.Seen, kv.Value.CrewLanded, kv.Value.Caught };
            return map;
        }

        // Catches: a host that rolls without the grid/probe gate (a cast whose computed landing is past the shore and the grid's edge); an owner
        // left stuck by a rejection. The request is honest apart from the target: the origin is the player's, the charge is full.
        [UnityTest]
        public IEnumerator EcologySolo_OffGridCast_RejectedNotWater()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig);
            FishingRig rig = _rig;
            PinWorld(Season.Summer, 300);
            yield return PlayModeHarness.WaitFrames(2, 0f);

            // Face yaw 180 (toward the shore): re-teleport with that rotation, then equip and ready WITHOUT the driver's marker assert.
            rig.Locomotion.ApplyTeleport(new Vector3(0f, 0.65f, FishingPlayModeDriver.DeckTeleportZ), 180f);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "the player never became grounded after the yaw-180 teleport");
            yield return ReadyRod(rig);
            Assert.That(rig.Controller.MarkerValid, Is.False, "the owner's own marker is red toward the shore");

            FishingHost host = FishingHost.Current;
            EcologyRunState run = host.Ledger.Run;
            ZoneData zone = ZoneOfWorld(rig, out _);
            Vector3 origin = rig.Player.transform.position;
            float yaw = rig.Player.transform.eulerAngles.y;
            Assert.That(yaw, Is.EqualTo(180f).Within(1f), "facing yaw 180");
            const float fullCharge = 1f;
            var req = new CastRequestData { CastId = ForgedCastIdBase + 50, OriginX = origin.x, OriginY = origin.y, OriginZ = origin.z, YawDeg = yaw,
                                            ChargeNorm = fullCharge, BaitIndex = (byte)rig.Net.NetSelectedBait.Value };
            Assert.That(host.Ledger.TryComputeLanding(rig.Player.Slot, in req, out float lx, out float lz), Is.True, "the host computes a landing");
            TestContext.WriteLine("FG T24 off-grid landing (" + lx.ToString("F2") + ", " + lz.ToString("F2") + ")");
            Assert.That(zone.Grid.IsWater(lx, lz), Is.False, "precondition: the baked grid calls the landing not water");

            ushort baitBefore = rig.Net.NetSelectedBaitCount.Value;
            uint castsBefore = run.HostCastCounter;
            var verdict = new ForgedVerdict();
            yield return ForgeCast(rig, origin, yaw, fullCharge, verdict);

            Assert.That(verdict.Valid, Is.False, "the host refuses");
            Assert.That(verdict.Reason, Is.EqualTo(CastRejectReason.NotWater), "reason");
            AssertNoActiveCast(rig, "off-grid cast");
            Assert.That(rig.Net.NetSelectedBaitCount.Value, Is.EqualTo(baitBefore), "bait unchanged");
            Assert.That(run.HostCastCounter, Is.EqualTo(castsBefore), "the host cast counter did not move");

            // The owner is not stuck: its machine ignored the stray result and still takes Secondary (recall) back to Idle.
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Ready), "the stray rejection did not move the owner's machine");
            rig.Fake.Frame = default;
            yield return ToIdle(rig, 3f);
            rig.Log.AssertEmpty("off-grid cast");
        }

        // Catches: a clock that ignores solo pause; a pause done with timeScale; a jump on resume.
        // The pause goes through the real UI (UiFlowController.Show(Pause) is what Esc calls). The fake input is not used: UiFlowController
        // caches the IInputService it was initialised with, so a FakeInputService installed later never reaches its Esc handler.
        [UnityTest]
        public IEnumerator EcologySolo_WorldClock_RunsAndStopsInSoloPause()
        {
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig, teleport: false);
            FishingRig rig = _rig;
            WorldState world = WorldState.Current;
            float rate = DayClock.DefaultMinutesPerSecond;
            UiFlowController ui = AppBootstrap.Current.GetComponentInChildren<UiFlowController>(true);
            Assert.That(ui, Is.Not.Null, "UiFlowController");
            Assert.That(ui.CurrentScreen, Is.EqualTo(UiScreen.Hud), "in game");
            Assert.That(ServiceRoots.Game.TryGet(out IPauseState pause), Is.True, "IPauseState");
            Assert.That(world.NetClock.Value.Running, Is.EqualTo((byte)1), "a fresh world runs");

            try
            {
                float m0 = world.MinuteF;
                double t0 = Time.realtimeSinceStartupAsDouble;
                yield return PlayModeHarness.WaitFrames(1, 3f);
                float advanced = world.MinuteF - m0;
                double elapsed = Time.realtimeSinceStartupAsDouble - t0;
                TestContext.WriteLine("FG T24 clock advanced " + advanced.ToString("F3") + " minutes in " + elapsed.ToString("F3") + " s (rate " + rate.ToString("F3") + ")");
                // Only the relative check: an absolute "4 +- 0.5 minutes in 3 s" fails on a single long frame, since WaitFrames(1, 3f) can last longer.
                Assert.That(advanced, Is.EqualTo((float)elapsed * rate).Within(0.5f), "the clock runs at the dev rate");
                Assert.That(Time.timeScale, Is.EqualTo(1f), "timeScale while running");

                ui.Show(UiScreen.Pause);
                yield return PlayModeHarness.WaitFrames(2, 0.05f);
                Assert.That(pause.Paused, Is.True, "the pause screen paused the solo game");
                Assert.That(world.NetClock.Value.Running, Is.EqualTo((byte)0), "the clock anchor stopped");
                Assert.That(Time.timeScale, Is.EqualTo(1f), "R34: the pause never touches timeScale");
                float held = world.MinuteF;
                yield return PlayModeHarness.WaitFrames(1, 2f);
                Assert.That(world.MinuteF, Is.EqualTo(held).Within(0.01f), "the minute stays put for 2 s while paused");
                Assert.That(Time.timeScale, Is.EqualTo(1f), "timeScale while paused");

                float beforeResume = world.MinuteF;
                ui.Show(UiScreen.Hud);
                Assert.That(pause.Paused, Is.False, "resumed");
                Assert.That(world.NetClock.Value.Running, Is.EqualTo((byte)1), "the clock runs again");
                Assert.That(Mathf.Abs(world.MinuteF - beforeResume), Is.LessThan(0.2f), "no jump at resume");
                float afterResume = world.MinuteF;
                yield return PlayModeHarness.WaitFrames(1, 1f);
                Assert.That(world.MinuteF - afterResume, Is.GreaterThan(1f), "the clock advances again after resume");
                Assert.That(Time.timeScale, Is.EqualTo(1f), "timeScale after resume");
            }
            finally
            {
                if (ui != null) ui.Show(UiScreen.Hud);
            }
            rig.Log.AssertEmpty("clock and pause");
        }

        // Catches: an in-scene object with GlobalObjectIdHash 0 (not spawned through the scene manager); a world not registered as the context;
        // a spawn payload (season, clock rate, weather) that disagrees with the driver that wrote it.
        [UnityTest]
        public IEnumerator EcologySolo_WorldState_SyncedWithNonZeroHash()
        {
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig, teleport: false);
            FishingRig rig = _rig;
            WorldState world = WorldState.Current;
            Assert.That(world.IsSpawned, Is.True, "spawned");
            NetworkObject no = world.GetComponent<NetworkObject>();
            Assert.That(no, Is.Not.Null);
            Assert.That(no.PrefabIdHash, Is.Not.EqualTo(0u), "in-scene GlobalObjectIdHash (NGO exposes it as PrefabIdHash for a scene object)");
            Assert.That(world.HasState, Is.True);
            Assert.That(world.SpotId, Is.EqualTo(ContentIds.LakeDock));
            Assert.That(ServiceRoots.Game.TryGet(out IWorldContext context), Is.True, "an IWorldContext in ServiceRoots.Game");
            Assert.That(context, Is.SameAs(world), "the registered context is the spawned world");

            DevWorldDriver driver = Driver();
            Assert.That(driver.WeatherOverride, Is.EqualTo(WeatherMask.None), "unpinned: the rolled track decides the weather");
            Assert.That(world.Season, Is.EqualTo(EcologySession.Current.Season), "Season == the session run state's");
            Assert.That(world.NetClock.Value.MinutesPerSecond, Is.EqualTo(DayClock.DefaultMinutesPerSecond), "clock rate");
            WeatherMask track = driver.Weather.At(driver.Run.Day, world.Minute);
            Assert.That(world.NetWeather.Value, Is.EqualTo((byte)track), "NetWeather is the driver's track at the current day and segment");
            rig.Log.AssertEmpty("world state sync");
        }

        // Catches: a harbour scene missing a per-scene hook (spawn, rod, fishing host, recorder, grid), or a journal that keys a harbour catch to the
        // wrong spot. The forced species is for speed and determinism (the resolver path is the first test).
        [UnityTest]
        public IEnumerator EcologySolo_HarbourLoad_SpawnsPlayerAndLandsAFish()
        {
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig, teleport: false);
            FishingRig rig = _rig;
            yield return SwitchTo(rig, ContentIds.HarbourPier);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.SpotHarbourPier));
            PinWorld(Season.Summer, 300);
            yield return PlayModeHarness.WaitFrames(2, 0f);

            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "pier_sardine";
            var stand = new StandChoice();
            yield return FindCastableStandPoint(rig, stand);

            Assert.That(ServiceRoots.App.TryGet(out SaveService save), Is.True, "SaveService");
            ushort sardine = rig.Registry.SpeciesIndex("pier_sardine");
            Dictionary<string, int[]> journalBefore = SnapshotJournal(save);
            int creelBefore = rig.Creel.Count;
            int announcedBefore = rig.Announced.Count;

            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 4f);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 6f);
            yield return FishingPlayModeDriver.FightRhythm(rig, 100f);
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Landed), "the scripted fight lands the sardine (outcome " + rig.Machine.Outcome + "/" + rig.Machine.Reason + ")");
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 5f);
            yield return PlayModeHarness.WaitUntil(() => rig.Creel.Count > creelBefore && rig.Announced.Count > announcedBefore, 3f, "the commit never reached the creel and the bus");
            yield return PlayModeHarness.WaitFrames(3, 0.05f);

            Assert.That(rig.Creel.Count, Is.EqualTo(creelBefore + 1), "creel +1");
            CatchAnnounced a = rig.Announced[rig.Announced.Count - 1];
            Assert.That(a.Announcement.Kind, Is.EqualTo(AnnounceKind.Landed));
            Assert.That(a.IsLocalCatcher, Is.True);
            Assert.That(a.Announcement.Catch.ItemIndex, Is.EqualTo(sardine), "the announced fish is the forced pier_sardine");
            int[] before = journalBefore.TryGetValue("pier_sardine", out int[] b) ? b : new int[3];
            Assert.That(save.Current.Journal.TryGetValue("pier_sardine", out JournalEntry entry), Is.True, "journal entry for pier_sardine");
            Assert.That(entry.Caught, Is.EqualTo(before[2] + 1), "Caught +1");
            Assert.That(entry.Seen, Is.EqualTo(before[0] + 1), "Seen +1");
            Assert.That(entry.SeenInSpots, Does.Contain(ContentIds.HarbourPier), "seen in harbour_pier");
            rig.Log.AssertEmpty("harbour load and landing");
        }

        // Catches (B11, T1): a counter that restarts per scene (replayed catch seeds); dev overrides lost on a Spot load; a clock re-anchored
        // somewhere other than minute 0 (or not running) on the new map; a season or day dropped by the switch.
        [UnityTest]
        public IEnumerator EcologySolo_DevMapSwitch_KeepsRunStateAndCastCounter()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            FishingDebugSettings.Luck = 0.5f;
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig);
            FishingRig rig = _rig;
            PinWorld(Season.Summer, 300);
            yield return PlayModeHarness.WaitFrames(2, 0f);

            EcologyRunState run = EcologySession.Current;
            Assert.That(run, Is.Not.Null);
            uint start = run.HostCastCounter;
            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return CastAndWaitForWaiting(rig);
            yield return CancelToIdle(rig);
            uint n = run.HostCastCounter;
            Assert.That(n, Is.EqualTo(start + 1), "one accepted cast at the lake");
            Season season = run.Season;
            Driver().DevNewDay();   // day 2: "the same day" below is not just day 1
            int day = run.Day;
            Assert.That(day, Is.EqualTo(2));

            yield return SwitchTo(rig, ContentIds.HarbourPier);
            Assert.That(ReferenceEquals(EcologySession.Current, run), Is.True, "the session run state survives the switch");
            Assert.That(run.Season, Is.EqualTo(season));
            Assert.That(run.Day, Is.EqualTo(day));
            Assert.That(run.HostCastCounter, Is.EqualTo(n), "the counter is unchanged at the harbour");
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(0.5f), "Luck survives a Spot load");
            Assert.That(FishingDebugSettings.ForcedBiteWait, Is.EqualTo(30f), "F6 survives a Spot load");
            Assert.That(FishingHost.Current.Ledger.Run, Is.SameAs(run), "the new host's ledger runs on the same run state");
            ClockAnchor anchor = WorldState.Current.NetClock.Value;
            Assert.That(anchor.AnchorMinute, Is.EqualTo(0f), "the clock restarted at minute 0 on the new map");
            Assert.That(anchor.Running, Is.EqualTo((byte)1), "and runs");
            Assert.That(WorldState.Current.NetSeason.Value, Is.EqualTo((byte)season), "the harbour world shows the kept season");

            PinWorld(season, 300);   // the new driver started a running clock and a rolled weather track
            yield return PlayModeHarness.WaitFrames(2, 0f);
            var stand = new StandChoice();
            yield return FindCastableStandPoint(rig, stand);
            yield return CastAndWaitForWaiting(rig);
            yield return CancelToIdle(rig);
            Assert.That(run.HostCastCounter, Is.EqualTo(n + 1), "one more at the harbour");

            yield return SwitchTo(rig, ContentIds.LakeDock);
            Assert.That(run.HostCastCounter, Is.EqualTo(n + 1), "unchanged back at the lake");
            Assert.That(run.Day, Is.EqualTo(day));
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(0.5f));
            PinWorld(season, 300);
            yield return PlayModeHarness.WaitFrames(2, 0f);
            yield return TeleportToDeckEnd(rig);
            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return CastAndWaitForWaiting(rig);
            yield return CancelToIdle(rig);
            Assert.That(run.HostCastCounter, Is.EqualTo(n + 2), "the counter continues across both switches");
            rig.Log.AssertEmpty("dev map switch");
        }

        // Catches: an owner gate that disagrees with the host (M3-D16: the owner marker open while the host says NotAllowedNow); a day-over that
        // never reopens.
        [UnityTest]
        public IEnumerator EcologySolo_DayOver_CastRejectedThenNewDayAccepts()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig);   // G9
            FishingRig rig = _rig;
            DevWorldDriver driver = Driver();
            EcologyRunState run = EcologySession.Current;
            driver.DevSetClockRunning(false);
            driver.DevSetMinute(1200);
            yield return PlayModeHarness.WaitUntil(() => !WorldState.Current.CastingOpen, 3f, "the clock never closed casting");

            yield return ReadyRod(rig);   // WITHOUT the driver's marker assert: the marker is red over the water
            Assert.That(rig.Controller.MarkerVisible, Is.True, "the marker is shown");
            Assert.That(rig.Controller.MarkerValid, Is.False, "but red: casting is closed (M3-D16)");

            FishingHost host = FishingHost.Current;
            Vector3 origin = rig.Player.transform.position;
            float yaw = rig.Player.transform.eulerAngles.y;
            var req = new CastRequestData { CastId = ForgedCastIdBase + 60, OriginX = origin.x, OriginY = origin.y, OriginZ = origin.z, YawDeg = yaw,
                                            ChargeNorm = 0.2f, BaitIndex = (byte)rig.Net.NetSelectedBait.Value };
            Assert.That(host.Ledger.TryComputeLanding(rig.Player.Slot, in req, out float lx, out float lz), Is.True);
            ZoneData zone = ZoneOfWorld(rig, out _);
            Assert.That(zone.Grid.IsWater(lx, lz), Is.True, "the forged request is a valid G9 cast apart from the clock: grid water");
            Assert.That(CastMath.ProbeReason(CastProbe.Probe(lx, lz), rig.Registry.FishingTuning.MinLandDepth), Is.EqualTo(CastRejectReason.None), "and probe water");

            ushort baitBefore = rig.Net.NetSelectedBaitCount.Value;
            uint castsBefore = run.HostCastCounter;
            var verdict = new ForgedVerdict();
            yield return ForgeCast(rig, origin, yaw, 0.2f, verdict);
            Assert.That(verdict.Valid, Is.False);
            Assert.That(verdict.Reason, Is.EqualTo(CastRejectReason.NotAllowedNow), "day over");
            AssertNoActiveCast(rig, "day over");
            Assert.That(rig.Net.NetSelectedBaitCount.Value, Is.EqualTo(baitBefore), "bait unchanged");
            Assert.That(run.HostCastCounter, Is.EqualTo(castsBefore), "counter unchanged");

            int dayBefore = run.Day;
            driver.DevNewDay();
            Assert.That(WorldState.Current.CastingOpen, Is.True, "a new day re-opens casting");
            Assert.That(run.Day, Is.EqualTo(dayBefore + 1), "Run.Day +1");
            Assert.That(WorldState.Current.MinuteF, Is.LessThan(1f), "minute restarted");
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            Assert.That(rig.Controller.MarkerValid, Is.True, "the marker turns valid again");

            yield return CastAndWaitForWaiting(rig);
            Assert.That(run.HostCastCounter, Is.EqualTo(castsBefore + 1), "a normal cast is accepted on the new day");
            yield return CancelToIdle(rig);
            rig.Log.AssertEmpty("day over");
        }

        // Catches (T15 review follow-up): the host's GridWater wiring. A point the baked grid calls not water while the physics probe says valid
        // water must be refused NotWater by the host (the owner marker applies the same grid gate, so it is red there too). The baked lake grid has
        // no such point (the bake and the probe agree everywhere, 0 candidates); the harbour has them along the breakwater's edges, where a grid
        // cell that the bake called land still holds a sliver of probe water. The search runs on the lake first and falls back to the harbour.
        [UnityTest]
        public IEnumerator EcologySolo_GridLandCell_HostRejectsNotWater()
        {
            FishingDebugSettings.ForcedBiteWait = 30f;
            _rig = new FishingRig();
            yield return ArrangeWorld(_rig);
            FishingRig rig = _rig;
            float minLandDepth = rig.Registry.FishingTuning.MinLandDepth;
            float minDist = rig.Registry.Fishing.Tuning.CastMinDist;
            float tipReach = FishingConstants.CastTipReach;
            var searched = new StringBuilder();
            StandPoint chosenStand = null;
            Vector2 target = default;

            for (int scene = 0; scene < 2 && chosenStand == null; scene++)
            {
                if (scene == 1) yield return SwitchTo(rig, ContentIds.HarbourPier);
                PinWorld(Season.Summer, 300);
                yield return PlayModeHarness.WaitFrames(2, 0f);
                ZoneData zone = ZoneOfWorld(rig, out _);
                float maxDist = rig.Controller.Rod.CastMaxDist;
                List<Vector2> points = SearchGridLandPoints(zone.Grid, minLandDepth);
                searched.Append(WorldState.Current.SpotId).Append(": ").Append(points.Count).Append(" grid-land/probe-water points; ");
                TestContext.WriteLine("FG T24 " + WorldState.Current.SpotId + " grid-land search: " + points.Count + " points " + Describe(points));

                StandPoint[] stands = UnityEngine.Object.FindObjectsByType<StandPoint>();
                Array.Sort(stands, (a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (StandPoint sp in stands)
                {
                    foreach (Vector2 c in points)
                    {
                        float reach = Mathf.Sqrt((c.x - sp.transform.position.x) * (c.x - sp.transform.position.x)
                                                 + (c.y - sp.transform.position.z) * (c.y - sp.transform.position.z));
                        if (reach < tipReach + minDist + 0.5f || reach > tipReach + maxDist - 0.5f) continue;   // inside the rod's range with margin
                        if (!IsRobustGridLandPoint(zone.Grid, c, minLandDepth)) continue;
                        chosenStand = sp;
                        target = c;
                        break;
                    }
                    if (chosenStand != null) break;
                }
            }
            if (chosenStand == null) Assert.Fail("no point where the grid says not water and the probe says water is in range of a stand point; " + searched);

            // Stand on the stand point (the host takes the origin from its own view of the player, within 3 m) and aim at the point.
            string spot = WorldState.Current.SpotId;
            ZoneData zoneNow = ZoneOfWorld(rig, out _);
            float maxNow = rig.Controller.Rod.CastMaxDist;
            // The player faces the target (as the off-grid test does), so the request is honest apart from the target cell: the yaw is the player's.
            Vector3 standPos = chosenStand.transform.position;
            float aimYaw = Mathf.Atan2(target.x - standPos.x, target.y - standPos.z) * Mathf.Rad2Deg;
            rig.Locomotion.ApplyTeleport(standPos + new Vector3(0f, 0.1f, 0f), aimYaw);
            yield return PlayModeHarness.WaitFrames(3, 0.05f);
            yield return PlayModeHarness.WaitUntil(() => rig.Locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "never grounded at " + chosenStand.name);
            yield return ToIdle(rig, 2f);

            Vector3 origin = rig.Player.transform.position;
            float dx = target.x - origin.x, dz = target.y - origin.z;
            float total = Mathf.Sqrt(dx * dx + dz * dz);
            float charge = (total - tipReach - minDist) / (maxNow - minDist);
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            Assert.That(Mathf.DeltaAngle(rig.Player.transform.eulerAngles.y, yaw), Is.EqualTo(0f).Within(1f), "the player faces the forged yaw " + yaw.ToString("F1"));
            Assert.That(charge, Is.InRange(FishingConstants.MinChargeNorm + 0.01f, 0.99f), "the honest charge for a " + total.ToString("F2") + " m landing");

            FishingHost host = FishingHost.Current;
            EcologyRunState run = host.Ledger.Run;
            var req = new CastRequestData { CastId = ForgedCastIdBase + 70, OriginX = origin.x, OriginY = origin.y, OriginZ = origin.z, YawDeg = yaw,
                                            ChargeNorm = charge, BaitIndex = (byte)rig.Net.NetSelectedBait.Value };
            Assert.That(host.Ledger.TryComputeLanding(rig.Player.Slot, in req, out float lx, out float lz), Is.True, "the host computes a landing");
            TestContext.WriteLine("FG T24 " + spot + " forged landing (" + lx.ToString("F3") + ", " + lz.ToString("F3") + ") from stand " + chosenStand.name
                                  + " origin (" + origin.x.ToString("F2") + ", " + origin.z.ToString("F2") + ") yaw " + yaw.ToString("F1") + " charge " + charge.ToString("F3"));
            Assert.That(Mathf.Abs(lx - target.x) + Mathf.Abs(lz - target.y), Is.LessThan(0.02f), "the host's landing is the chosen point");
            Assert.That(zoneNow.Grid.IsWater(lx, lz), Is.False, "precondition: the baked grid calls the landing not water");
            Assert.That(CastMath.ProbeReason(CastProbe.Probe(lx, lz), minLandDepth), Is.EqualTo(CastRejectReason.None),
                        "precondition: the physics probe calls the landing valid water (so the grid gate is the only refusal)");
            Assert.That(SpotEcology.CastGatesOpen(WorldState.Current, zoneNow, lx, lz), Is.False, "the owner's world gates are red there too (same grid)");

            ushort baitBefore = rig.Net.NetSelectedBaitCount.Value;
            uint castsBefore = run.HostCastCounter;
            var verdict = new ForgedVerdict();
            yield return ForgeCast(rig, origin, yaw, charge, verdict);

            Assert.That(verdict.Valid, Is.False, "the host refuses");
            Assert.That(verdict.Reason, Is.EqualTo(CastRejectReason.NotWater), "reason");
            AssertNoActiveCast(rig, "grid-land cast");
            Assert.That(rig.Net.NetSelectedBaitCount.Value, Is.EqualTo(baitBefore), "bait unchanged");
            Assert.That(run.HostCastCounter, Is.EqualTo(castsBefore), "the host cast counter did not move");
            rig.Log.AssertEmpty("grid-land cast");
        }

        // A search hit is only used when the four points 5 cm around it agree (grid not water, probe valid): the host's landing is a float round trip.
        private static bool IsRobustGridLandPoint(ShoreGrid grid, Vector2 c, float minLandDepth)
        {
            foreach (Vector2 d in new[] { Vector2.zero, new Vector2(0.05f, 0.05f), new Vector2(-0.05f, 0.05f), new Vector2(0.05f, -0.05f), new Vector2(-0.05f, -0.05f) })
            {
                float x = c.x + d.x, z = c.y + d.y;
                if (grid.IsWater(x, z) || CastMath.ProbeReason(CastProbe.Probe(x, z), minLandDepth) != CastRejectReason.None) return false;
            }
            return true;
        }

        private static string Describe(List<Vector2> pts)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pts.Count && i < 30; i++) sb.Append('(').Append(pts[i].x.ToString("F2")).Append(',').Append(pts[i].y.ToString("F2")).Append(") ");
            return sb.ToString();
        }

        // Points the baked grid calls not water (inside the grid, or just outside it) at which the physics probe accepts a landing: every cell that
        // is not water gets 25 sub-cell samples when it touches a water cell, plus one centre sample otherwise.
        private static List<Vector2> SearchGridLandPoints(ShoreGrid grid, float minLandDepth)
        {
            var found = new List<Vector2>();
            float cs = grid.CellSizeM;
            float[] offsets = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };
            for (int cz = -2; cz < grid.Height + 2; cz++)
            {
                for (int cx = -2; cx < grid.Width + 2; cx++)
                {
                    float ox = grid.OriginX + cx * cs, oz = grid.OriginZ + cz * cs;
                    if (grid.IsWater(ox + 0.5f * cs, oz + 0.5f * cs)) continue;
                    bool edge = grid.IsWater(ox - 0.5f * cs, oz + 0.5f * cs) || grid.IsWater(ox + 1.5f * cs, oz + 0.5f * cs)
                                || grid.IsWater(ox + 0.5f * cs, oz - 0.5f * cs) || grid.IsWater(ox + 0.5f * cs, oz + 1.5f * cs);
                    foreach (float fz in edge ? offsets : new[] { 0.5f })
                    {
                        foreach (float fx in edge ? offsets : new[] { 0.5f })
                        {
                            float x = ox + fx * cs, z = oz + fz * cs;
                            if (grid.IsWater(x, z)) continue;
                            if (CastMath.ProbeReason(CastProbe.Probe(x, z), minLandDepth) == CastRejectReason.None) found.Add(new Vector2(x, z));
                        }
                    }
                }
            }
            return found;
        }
    }
}
