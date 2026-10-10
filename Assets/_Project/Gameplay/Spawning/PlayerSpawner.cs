// Authority: host only. Spawns players and NetSpawnAnchor prefabs; every peer can query spawn poses.
using System.Collections.Generic;
using FishGame.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.Gameplay
{
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints; // index = slot; 4 entries (Spawn0..Spawn3), set by SceneBuilder (T15)

        private static PlayerSpawner current;

        private NetworkManager subscribedManager;
        private NetworkSceneManager subscribedScenes;
        private bool anchorsSpawned;

        public static PlayerSpawner Current => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
        }

        // Builder (T15).
        public void Configure(NetworkObject playerPrefab, Transform[] spawnPoints)
        {
            this.playerPrefab = playerPrefab;
            this.spawnPoints = spawnPoints;
        }

        public bool TryGetSpawnPose(byte slot, out Vector3 position, out Quaternion rotation)
        {
            if (spawnPoints != null && slot < spawnPoints.Length && spawnPoints[slot] != null)
            {
                var point = spawnPoints[slot];
                position = point.position;
                rotation = point.rotation;
                return true;
            }

            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        private void OnEnable()
        {
            current = this;

            var manager = NetworkManager.Singleton;
            if (manager == null || manager.SceneManager == null) return;

            subscribedManager = manager;
            subscribedScenes = manager.SceneManager;
            subscribedScenes.OnLoadEventCompleted += OnLoadEventCompleted;
            subscribedScenes.OnSynchronizeComplete += OnSynchronizeComplete;
            subscribedScenes.OnLoadComplete += OnClientLoadComplete;
        }

        private void OnDisable()
        {
            if (subscribedScenes != null)
            {
                subscribedScenes.OnLoadEventCompleted -= OnLoadEventCompleted;
                subscribedScenes.OnSynchronizeComplete -= OnSynchronizeComplete;
                subscribedScenes.OnLoadComplete -= OnClientLoadComplete;
            }
            subscribedScenes = null;
            subscribedManager = null;

            if (ReferenceEquals(current, this)) current = null;
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            var manager = subscribedManager;
            if (manager == null || !manager.IsServer) return;
            if (sceneName != gameObject.scene.name) return;

            // Spec 6.5: disconnect clients that timed out loading. The list can contain the host (load not done)
            // and clients that already left, so skip both.
            var timedOut = new List<ulong>(clientsTimedOut);
            for (int i = 0; i < timedOut.Count; i++)
            {
                var id = timedOut[i];
                if (id == NetworkManager.ServerClientId) continue;
                if (!manager.ConnectedClients.ContainsKey(id)) continue;
                manager.DisconnectClient(id, EndReason.Timeout.ToString());
            }

            // Spec 4.6: once per scene load, spawn every anchor's prefab in this scene.
            if (!anchorsSpawned)
            {
                anchorsSpawned = true;
                SpawnAnchors(manager);
            }

            // clientsCompleted contains the host once its own load is done; spawning the host explicitly is idempotent.
            if (manager.IsHost) SpawnPlayerIfMissing(manager, NetworkManager.ServerClientId);

            var completed = new List<ulong>(clientsCompleted);
            for (int i = 0; i < completed.Count; i++)
            {
                var id = completed[i];
                if (id == NetworkManager.ServerClientId) continue;
                if (!manager.ConnectedClients.ContainsKey(id)) continue;
                SpawnPlayerIfMissing(manager, id);
            }
        }

        // Late joiners: the server raises this when a joining client finishes its initial scene sync
        // (before the ClientConnected connection event for that client).
        private void OnSynchronizeComplete(ulong clientId)
        {
            var manager = subscribedManager;
            if (manager == null || !manager.IsServer) return;
            if (clientId == NetworkManager.ServerClientId) return;
            if (!gameObject.scene.isLoaded) return;

            SpawnPlayerIfMissing(manager, clientId);
        }

        // A client approved while the host's own scene load was still running syncs to the previous scene, then receives
        // this scene's load afterwards; it is never in OnLoadEventCompleted's clientsCompleted for that event. The server
        // sees every client's own LoadComplete here, so spawn that client's player (idempotent).
        private void OnClientLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            var manager = subscribedManager;
            if (manager == null || !manager.IsServer) return;
            if (clientId == NetworkManager.ServerClientId) return;
            if (sceneName != gameObject.scene.name) return;
            if (!gameObject.scene.isLoaded) return;
            if (!manager.ConnectedClients.ContainsKey(clientId)) return;

            SpawnPlayerIfMissing(manager, clientId);
        }

        private void SpawnAnchors(NetworkManager manager)
        {
            var anchors = new List<NetSpawnAnchor>(NetSpawnAnchor.All);
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null) continue;
                if (anchor.gameObject.scene != gameObject.scene) continue;
                if (anchor.Prefab == null)
                {
                    Debug.LogError($"PlayerSpawner: NetSpawnAnchor '{anchor.name}' has no prefab.", anchor);
                    continue;
                }

                var t = anchor.transform;
                var spawned = manager.SpawnManager.InstantiateAndSpawn(
                    anchor.Prefab,
                    ownerClientId: NetworkManager.ServerClientId,
                    destroyWithScene: true,
                    position: t.position,
                    rotation: t.rotation);
                if (spawned == null)
                {
                    Debug.LogError($"PlayerSpawner: failed to spawn '{anchor.Prefab.name}' at anchor '{anchor.name}'.", anchor);
                }
            }
        }

        // Idempotent: a client that already has a player object is skipped.
        private void SpawnPlayerIfMissing(NetworkManager manager, ulong clientId)
        {
            if (manager.SpawnManager.GetPlayerNetworkObject(clientId) != null) return;

            if (playerPrefab == null)
            {
                Debug.LogError("PlayerSpawner: playerPrefab is not configured.", this);
                return;
            }

            var session = SessionState.Current;
            if (session == null)
            {
                Debug.LogError($"PlayerSpawner: no SessionState is spawned; cannot spawn client {clientId}.", this);
                return;
            }

            // Pull the pending approval into the roster first (OnSynchronizeComplete runs before ClientConnected).
            session.ServerAbsorbApproved(clientId);

            if (!session.TryGetByClient(clientId, out var entry))
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    Debug.LogError("PlayerSpawner: the host has no connected roster slot.", this);
                    return;
                }

                Debug.LogWarning($"PlayerSpawner: client {clientId} has no connected roster slot. Disconnecting.", this);
                if (manager.ConnectedClients.ContainsKey(clientId))
                {
                    manager.DisconnectClient(clientId, EndReason.Unknown.ToString());
                }
                return;
            }

            if (!TryGetSpawnPose(entry.Slot, out var position, out var rotation))
            {
                Debug.LogError($"PlayerSpawner: no spawn point for slot {entry.Slot}.", this);
                return;
            }

            var playerObject = manager.SpawnManager.InstantiateAndSpawn(
                playerPrefab,
                ownerClientId: clientId,
                destroyWithScene: true,
                isPlayerObject: true,
                position: position,
                rotation: rotation);
            if (playerObject == null)
            {
                Debug.LogError($"PlayerSpawner: InstantiateAndSpawn returned null for client {clientId}.", this);
                return;
            }

            var init = playerObject.GetComponent<IServerPlayerInit>();
            if (init == null)
            {
                Debug.LogError($"PlayerSpawner: '{playerPrefab.name}' has no IServerPlayerInit component.", this);
                return;
            }

            init.ServerInit(entry.Slot, entry.Name.ToString());
        }
    }
}
