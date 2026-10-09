// Authority: test-only. Solo start/leave/restart on the real stack: host-authoritative solo session on 127.0.0.1, port 7790 (plain UnityTransport).
using System.Collections;
using FishGame.App;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Networking;
using FishGame.Runtime;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class SoloSessionTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest]
        public IEnumerator Solo_Start_SpawnsOneOwnedPlayerWithCamera()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            // Give a duplicate spawn (OnLoadEventCompleted plus OnSynchronizeComplete) time to show up before counting.
            yield return new WaitForSecondsRealtime(0.5f);

            var app = AppBootstrap.Current;
            var session = app.Services.Get<INetworkSession>();
            Assert.That(session.State, Is.EqualTo(ConnState.Connected), "Solo session must be Connected");
            Assert.That(session.Mode, Is.EqualTo(SessionMode.Solo), "Session mode must be Solo");
            Assert.That(app.NetworkManager.IsHost, Is.True, "Solo runs as host");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.SpotLakeDock), "Solo must load Spot_LakeDock");
            Assert.That(ServiceRoots.Game, Is.Not.Null, "GameScope must register ServiceRoots.Game in the gameplay scene");

            Assert.That(PlayerNetState.Active.Count, Is.EqualTo(1), "Exactly one PlayerNetState expected");
            var player = PlayerNetState.Local;
            Assert.That(player, Is.Not.Null, "No local player");
            Assert.That(PlayerNetState.Active[0], Is.SameAs(player), "The only active player must be the local one");
            Assert.That(player.IsOwner, Is.True, "Local player must be owned by this peer");
            Assert.That(player.gameObject.layer, Is.EqualTo(Layers.Player), "Owner root must be on layer Player");

            var cameraTransform = player.transform.Find(PlayerRigPaths.Camera);
            Assert.That(cameraTransform, Is.Not.Null, "Player rig is missing " + PlayerRigPaths.Camera);
            var playerCamera = cameraTransform.GetComponent<Camera>();
            var listener = cameraTransform.GetComponent<AudioListener>();
            Assert.That(playerCamera, Is.Not.Null, "Player camera component missing");
            Assert.That(listener, Is.Not.Null, "Player AudioListener component missing");
            Assert.That(playerCamera.enabled, Is.True, "Owner Camera must be enabled");
            Assert.That(listener.enabled, Is.True, "Owner AudioListener must be enabled");

            var capsule = player.GetComponent<CapsuleCollider>();
            var controller = player.GetComponent<CharacterController>();
            Assert.That(capsule, Is.Not.Null, "PlayerFP root must have a CapsuleCollider");
            Assert.That(controller, Is.Not.Null, "PlayerFP root must have a CharacterController");
            Assert.That(capsule.enabled, Is.False, "Remote-only CapsuleCollider must be disabled on the owner");
            Assert.That(controller.enabled, Is.True, "Owner CharacterController must be enabled");

            var sceneCameraObject = GameObject.Find("SceneCamera");
            Assert.That(sceneCameraObject, Is.Not.Null, "SceneCamera missing in Spot_LakeDock");
            var sceneCamera = sceneCameraObject.GetComponent<Camera>();
            var sceneListener = sceneCameraObject.GetComponent<AudioListener>();
            Assert.That(sceneCamera, Is.Not.Null, "SceneCamera has no Camera");
            Assert.That(sceneListener, Is.Not.Null, "SceneCamera has no AudioListener");
            Assert.That(sceneCamera.enabled, Is.False, "SceneCamera Camera must be off while a local player exists");
            Assert.That(sceneListener.enabled, Is.False, "SceneCamera AudioListener must be off while a local player exists");
        }

        [UnityTest]
        public IEnumerator Solo_LeaveThenRestart_Works()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();

            var app = AppBootstrap.Current;
            var nm = app.NetworkManager;
            var session = app.Services.Get<INetworkSession>();
            var local = app.Services.Get<LocalNetworkSession>();

            AssertNgoIdle(nm, "before the first start");
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            Assert.That(PlayerNetState.Local, Is.Not.Null, "First solo start did not spawn a local player");
            AssertSoloTransport(nm, local, "first start");

            yield return PlayModeHarness.LeaveAndWaitForMenu();
            Assert.That(session.State, Is.EqualTo(ConnState.Offline), "Session must be Offline after leave");
            Assert.That(PlayerNetState.Local, Is.Null, "Local player must be gone after leave");
            Assert.That(PlayerNetState.Active.Count, Is.EqualTo(0), "No players may remain after leave");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Menu), "Leave must return to Menu");

            AssertNgoIdle(nm, "before the restart");
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            Assert.That(session.State, Is.EqualTo(ConnState.Connected), "Restart must reach Connected");
            Assert.That(PlayerNetState.Local, Is.Not.Null, "Restart after leave did not spawn a local player");
            Assert.That(PlayerNetState.Active.Count, Is.EqualTo(1), "Exactly one player after restart");
            AssertSoloTransport(nm, local, "restart");
        }

        private static void AssertNgoIdle(Unity.Netcode.NetworkManager nm, string when)
        {
            Assert.That(nm, Is.Not.Null, "NetworkManager missing " + when);
            Assert.That(nm.ShutdownInProgress, Is.False, "NGO ShutdownInProgress must be false " + when);
            Assert.That(nm.IsListening, Is.False, "NGO must not be listening " + when);
        }

        private static void AssertSoloTransport(Unity.Netcode.NetworkManager nm, LocalNetworkSession local, string when)
        {
            UnityTransport transport = TransportSetup.Get(nm);
            Assert.That(transport, Is.Not.Null, "No UnityTransport (" + when + ")");
            Assert.That(transport.Protocol, Is.EqualTo(UnityTransport.ProtocolType.UnityTransport),
                "Solo must use the plain UnityTransport protocol, not Relay (" + when + ")");
            Assert.That(transport.ConnectionData.Address, Is.EqualTo(NetworkConstants.LoopbackAddress),
                "Solo must connect to 127.0.0.1 (" + when + ")");
            Assert.That(transport.ConnectionData.ServerListenAddress, Is.EqualTo(NetworkConstants.LoopbackAddress),
                "Solo must listen on 127.0.0.1 (" + when + ")");

            int first = NetworkConstants.TestPort;
            int last = NetworkConstants.TestPort + NetworkConstants.SoloPortRetryCount - 1;
            Assert.That((int)local.ActivePort, Is.InRange(first, last),
                "ActivePort must be TestPort..TestPort+9 (" + when + ")");
            Assert.That((int)transport.ConnectionData.Port, Is.EqualTo((int)local.ActivePort),
                "Transport port must equal LocalNetworkSession.ActivePort (" + when + ")");
        }
    }
}
