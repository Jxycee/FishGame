// Authority: test-only. T16 (M3-D84, E-CCR-3): the first in-scene NetworkObject (the lake's World: WorldState + DevWorldDriver) spawns with
// state in a solo session before T15 relies on it. T17 appends the harbour dev-load test here.
using System.Collections;
using System.Text.RegularExpressions;
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
    public class EcologyMapPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
            EcologySession.Clear();
        }

        // Catches: the in-scene object never spawning (scene not loaded through the NetworkSceneManager, hash 0, NetworkObject disabled);
        // DevWorldDriver not initialised (no clock anchor, no run state); a warning from the world objects at spawn.
        [UnityTest]
        public IEnumerator EcologyMap_LakeSolo_WorldStateSpawnsWithState()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 5f,
                                                   "WorldState.Current did not spawn with state within 5 s");

            WorldState world = WorldState.Current;
            Assert.That(world.IsSpawned, Is.True, "spawned");
            Assert.That(world.HasState, Is.True, "the host wrote the clock anchor at spawn");
            Assert.That(world.SpotId, Is.EqualTo(ContentIds.LakeDock));
            NetworkObject no = world.GetComponent<NetworkObject>();
            Assert.That(no, Is.Not.Null);
            Assert.That(no.PrefabIdHash, Is.Not.EqualTo(0u), "in-scene GlobalObjectIdHash");
            DevWorldDriver driver = world.GetComponent<DevWorldDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.Run, Is.Not.Null, "the driver adopted the session run state");
            Assert.That(driver.Run.Day, Is.EqualTo(1));
            Assert.That(driver.Run, Is.SameAs(EcologySession.Current));
            Assert.That(world.CastingOpen, Is.True, "minute 0 of day 1: casting open");
            // The four spawn-payload values the driver wrote (D-F4): season, weather of segment 0, a running anchor.
            Assert.That(world.NetSeason.Value, Is.EqualTo((byte)driver.Run.Season), "NetSeason is the run's season");
            Assert.That(world.NetWeather.Value, Is.EqualTo((byte)driver.Weather.At(driver.Run.Day, 0)), "NetWeather is the day's segment-0 weather");
            Assert.That(world.NetClock.Value.Running, Is.EqualTo((byte)1), "the clock anchor is running");

            float before = world.MinuteF;
            yield return PlayModeHarness.WaitGameSeconds(1f, 10f, "game time did not advance");
            float after = world.MinuteF;
            Assert.That(after - before, Is.GreaterThan(0.5f), "the clock runs (" + before + " -> " + after + ")");

            logs.AssertNoWarnings("lake solo with World");
        }

        // Catches (F4): a second WorldState running the full spawn path after logging the error: it would ServerInitialize its own driver
        // (rewriting the run state), Replace the first one as IWorldContext and flip its own _spawnedHere so HasState turns true.
        [UnityTest]
        public IEnumerator EcologyMap_SecondWorldState_LogsAndStaysInert()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 5f,
                                                   "WorldState.Current did not spawn with state within 5 s");
            WorldState first = WorldState.Current;
            Assert.That(ServiceRoots.Game.TryGet(out IWorldContext before), Is.True, "the first WorldState registered");
            Assert.That(before, Is.SameAs(first));

            GameObject copy = Object.Instantiate(first.gameObject);
            WorldState second = copy.GetComponent<WorldState>();
            LogAssert.Expect(LogType.Error, new Regex("two WorldState objects"));
            copy.GetComponent<NetworkObject>().Spawn();
            yield return PlayModeHarness.WaitFrames(2, 0f);

            Assert.That(second.IsSpawned, Is.True, "the copy spawned");
            Assert.That(WorldState.Current, Is.SameAs(first), "the first instance is kept");
            Assert.That(ServiceRoots.Game.TryGet(out IWorldContext after), Is.True);
            Assert.That(after, Is.SameAs(first), "the second instance did not Replace the registered context");
            Assert.That(second.HasState, Is.False, "the second instance never reports state");
            Assert.That(second.GetComponent<DevWorldDriver>().Run, Is.Null, "the second instance's driver was never initialised");
            Assert.That(EcologySession.Current, Is.SameAs(first.GetComponent<DevWorldDriver>().Run), "the session run state is untouched");
            Assert.That(first.HasState, Is.True);
        }

        // Catches (T17, E-CCR-3): the harbour missing from the build list; a broken spawn path on the switch (late OnLoadComplete: the player on the plateau,
        // under the pier, or at the lake's coordinates); the in-scene World not spawning in the second scene; the run state dropped across the switch;
        // a baked grid that disagrees with the scene's physics (the owner/host gate pair of M3-D16 refuses a cell the bake marked castable).
        [UnityTest]
        public IEnumerator EcologyMap_HarbourDevLoad_SpawnsOnPierAndProbesEverySubArea()
        {
            using var logs = new PlayModeHarness.LogCapture();
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 5f,
                                                   "WorldState.Current did not spawn with state within 5 s");

            WorldState lakeWorld = WorldState.Current;
            PlayerNetState lakePlayer = PlayerNetState.Local;
            EcologyRunState run = EcologySession.Current;
            Assert.That(run, Is.Not.Null, "the session run state exists on the lake");
            uint casts = run.HostCastCounter;

            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "no App EventBus");
            Assert.That(ServiceRoots.App.TryGet(out ContentRegistry registry), Is.True, "no ContentRegistry");
            bus.Publish(new DevLoadSpotRequested(ContentIds.HarbourPier));

            yield return PlayModeHarness.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.SpotHarbourPier
                      && WorldState.Current != null && WorldState.Current != lakeWorld && WorldState.Current.HasState
                      && PlayerNetState.Local != null && PlayerNetState.Local != lakePlayer,
                20f, "the dev load never produced the harbour scene, its World and a new local player within 20 s");
            yield return PlayModeHarness.WaitFrames(5, 0.2f);   // physics settles; the player is grounded on the deck

            WorldState world = WorldState.Current;
            Assert.That(world.SpotId, Is.EqualTo(ContentIds.HarbourPier));
            Assert.That(world.IsSpawned, Is.True);
            Assert.That(ReferenceEquals(EcologySession.Current, run), Is.True, "the session run state survives the switch");
            Assert.That(run.HostCastCounter, Is.EqualTo(casts), "the cast counter is unchanged");

            PlayerNetState player = PlayerNetState.Local;
            Vector3 position = player.transform.position;
            Assert.That(position.y, Is.GreaterThan(1.4f), "the player stands on the deck (y " + position.y + ")");
            Assert.That(PlayerSpawner.Current, Is.Not.Null, "the harbour PlayerSpawner");
            Assert.That(PlayerSpawner.Current.TryGetSpawnPose(player.Slot, out Vector3 spawn, out _), Is.True, "spawn pose of slot " + player.Slot);
            Assert.That((position - spawn).magnitude, Is.LessThan(2f), "the player is within 2 m of its spawn point " + spawn + " (at " + position + ")");
            Assert.That(UnityEngine.Object.FindFirstObjectByType<DockBell>(), Is.Not.Null, "the DockBell spawned at the BellAnchor");

            // One reachable castable cell per sub-area, picked from the baked grid: the cast gate agrees with the grid.
            Assert.That(SpotEcology.TryResolve(registry, world, out _, out ZoneData zone), Is.True, "the harbour zone");
            ShoreGrid grid = zone.Grid;
            Assert.That(grid, Is.Not.Null, "the harbour grid is baked");
            float minLandDepth = registry.FishingTuning.MinLandDepth;
            var picked = new Vector2?[zone.SubAreas.Count];
            for (int cz = 0; cz < grid.Height; cz++)
            {
                for (int cx = 0; cx < grid.Width; cx++)
                {
                    if (!grid.IsCastable(cx, cz) || !grid.IsReachable(cx, cz)) continue;
                    grid.CellCentre(cx, cz, out float x, out float z);
                    if (!grid.IsWater(x, z)) continue;
                    int sub = zone.SubAreaIndexFor(grid.ShoreM(x, z));
                    if (picked[sub] == null) picked[sub] = new Vector2(x, z);
                }
            }
            for (int sub = 0; sub < picked.Length; sub++)
            {
                Assert.That(picked[sub].HasValue, Is.True, "a reachable castable cell in sub-area " + sub);
                Vector2 p = picked[sub].Value;
                Assert.That(grid.IsWater(p.x, p.y), Is.True, "the grid says water at " + p);
                Assert.That(CastMath.ProbeReason(CastProbe.Probe(p.x, p.y), minLandDepth), Is.EqualTo(CastRejectReason.None),
                            "the cast gate agrees with the grid in sub-area " + sub + " at " + p);
            }
            logs.AssertNoWarnings("harbour dev load");
        }
    }
}
