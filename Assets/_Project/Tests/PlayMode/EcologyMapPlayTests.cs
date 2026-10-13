// Authority: test-only. T16 (M3-D84, E-CCR-3): the first in-scene NetworkObject (the lake's World: WorldState + DevWorldDriver) spawns with
// state in a solo session before T15 relies on it. T17 appends the harbour dev-load test here.
using System.Collections;
using FishGame.Core;
using FishGame.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
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

            float before = world.MinuteF;
            yield return PlayModeHarness.WaitGameSeconds(1f, 10f, "game time did not advance");
            float after = world.MinuteF;
            Assert.That(after - before, Is.GreaterThan(0.5f), "the clock runs (" + before + " -> " + after + ")");

            logs.AssertNoWarnings("lake solo with World");
        }
    }
}
