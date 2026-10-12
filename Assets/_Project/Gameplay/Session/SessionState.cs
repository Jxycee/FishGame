// Authority: host writes Roster and NetJoinsOpen; every peer reads. Spawned by AppBootstrap on OnServerStarted with destroyWithScene: false.
// Roster entries are never removed in M1: a disconnected entry keeps its slot (Connected = false) so the same identity can reclaim it.
// M2 (T16): ServerCreels is the host-only, slot-keyed creel store (R46: a creel survives a disconnect and rejoin). It is a plain C# object,
// never networked: created in OnNetworkSpawn when IsServer, null on clients and after despawn. PlayerCreel (server) mirrors it to NetCreel.
// Slot takeover (T16-R1): Upsert clears that slot's creel and debug coins when the slot passes to a DIFFERENT persistent identity; the same
// identity rejoining keeps them (R46). The same takeover raises SlotTakenOver so FishingHost resets that slot's rod, bait, stock and pity streak.
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class SessionState : NetworkBehaviour, IJoinContextProvider
    {
        private static SessionState current;

        private JoinApprovalBridge bridge;
        private NetworkManager subscribedManager;

        public static SessionState Current => current;
        public static event Action<SessionState> CurrentChanged;

        // Host only (Upsert never runs elsewhere). Raised with the slot number after a DIFFERENT persistent identity took that slot over
        // (never for the same identity rejoining, never for a brand-new slot). Subscribers reset their per-slot host state (FishingHost).
        public static event Action<int> SlotTakenOver;

        public NetworkList<PlayerSlot> Roster;
        public readonly NetworkVariable<bool> NetJoinsOpen = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action RosterChanged;

        // Host only (null on clients). Created on server spawn with NetworkConstants.MaxPlayers slots.
        public CreelLedger ServerCreels { get; private set; }

        public bool JoinsOpen => NetJoinsOpen.Value;

        public int ConnectedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Roster.Count; i++)
                {
                    if (Roster[i].Connected) n++;
                }
                return n;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            CurrentChanged = null;
            SlotTakenOver = null;
        }

        private void Awake()
        {
            // Constructed here, not in a field initializer (contract F21).
            Roster = new NetworkList<PlayerSlot>(null,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            current = this;
            Roster.OnListChanged += OnRosterListChanged;
            NetJoinsOpen.OnValueChanged += OnJoinsOpenChanged;

            if (IsServer)
            {
                ServerCreels = new CreelLedger(NetworkConstants.MaxPlayers);

                var app = ServiceRoots.App;
                if (app != null && app.TryGet<JoinApprovalBridge>(out var found))
                {
                    bridge = found;
                    bridge.Provider = this;
                }
                else
                {
                    Debug.LogError("SessionState: JoinApprovalBridge is not registered in ServiceRoots.App; the roster will stay empty.");
                }

                subscribedManager = NetworkManager;
                subscribedManager.OnConnectionEvent += OnConnectionEvent;

                // Clients approved before this object spawned. Host self-approval runs inside StartHost, before OnServerStarted.
                var ids = new List<ulong>(subscribedManager.ConnectedClientsIds);
                for (int i = 0; i < ids.Count; i++)
                {
                    ServerAbsorbApproved(ids[i]);
                }
            }

            CurrentChanged?.Invoke(this);
            RosterChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            ServerCreels = null;
            if (subscribedManager != null)
            {
                subscribedManager.OnConnectionEvent -= OnConnectionEvent;
                subscribedManager = null;
            }

            if (bridge != null)
            {
                if (ReferenceEquals(bridge.Provider, this)) bridge.Provider = null;
                bridge = null;
            }

            Roster.OnListChanged -= OnRosterListChanged;
            NetJoinsOpen.OnValueChanged -= OnJoinsOpenChanged;

            if (ReferenceEquals(current, this))
            {
                current = null;
                CurrentChanged?.Invoke(null);
            }
        }

        // Clears `into`, then adds every entry (connected or not).
        public void GetRoster(List<RosterEntry> into)
        {
            into.Clear();
            for (int i = 0; i < Roster.Count; i++)
            {
                var e = Roster[i];
                into.Add(new RosterEntry(e.Id.ToKey(), e.Slot, e.Connected));
            }
        }

        // Connected or disconnected entries.
        public bool TryGetKey(ulong clientId, out PlayerKey key)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                var e = Roster[i];
                if (e.ClientId == clientId)
                {
                    key = e.Id.ToKey();
                    return true;
                }
            }
            key = default;
            return false;
        }

        // Connected entries only.
        public bool TryGetByClient(ulong clientId, out PlayerSlot slot)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                var e = Roster[i];
                if (e.Connected && e.ClientId == clientId)
                {
                    slot = e;
                    return true;
                }
            }
            slot = default;
            return false;
        }

        // Connected or disconnected entries (a disconnected slot is kept for reclaim).
        public bool TryGetBySlot(byte slot, out PlayerSlot entry)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                var e = Roster[i];
                if (e.Slot == slot)
                {
                    entry = e;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        // Dev toggle (T14 pause menu). Read by ConnectionApprover through IJoinContextProvider.JoinsOpen.
        public void ServerSetJoinsOpen(bool open)
        {
            if (!IsSpawned || !IsServer) throw new InvalidOperationException("ServerSetJoinsOpen is host-only.");
            NetJoinsOpen.Value = open;
        }

        // Called by PlayerActions.SubmitNameRpc (T11) with an already sanitized name.
        public void ServerRenameClient(ulong clientId, string sanitizedName)
        {
            if (!IsSpawned || !IsServer) throw new InvalidOperationException("ServerRenameClient is host-only.");
            for (int i = 0; i < Roster.Count; i++)
            {
                var e = Roster[i];
                if (!e.Connected || e.ClientId != clientId) continue;
                e.Name = SessionText.Name(sanitizedName);
                Roster[i] = e;
                return;
            }
        }

        // Host only. Moves this client's pending ApprovedJoin (if any) from the bridge into the roster.
        // NGO raises OnSynchronizeComplete BEFORE ClientConnected for a joining client, so PlayerSpawner calls this
        // before reading the slot; the later ClientConnected call then finds nothing pending and does nothing.
        internal bool ServerAbsorbApproved(ulong clientId)
        {
            if (!IsSpawned || !IsServer || bridge == null) return false;
            if (!bridge.TryTakeApproved(clientId, out var join)) return false;
            if (!Upsert(join))
            {
                // Defensive: the approver should never hand out a slot held by another connected client.
                var manager = NetworkManager;
                if (manager != null && join.ClientId != NetworkManager.ServerClientId
                    && manager.ConnectedClients.ContainsKey(join.ClientId))
                {
                    manager.DisconnectClient(join.ClientId, EndReason.FullLobby.ToString());
                }
                return false;
            }
            return true;
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (!IsSpawned || !IsServer) return;

            if (data.EventType == ConnectionEvent.ClientConnected)
            {
                ServerAbsorbApproved(data.ClientId);
            }
            else if (data.EventType == ConnectionEvent.ClientDisconnected)
            {
                MarkDisconnected(data.ClientId);
            }
        }

        // Host only. The entry holding join.Slot is replaced in place (reclaim, or taking over a disconnected slot); otherwise append.
        // Returns false (roster untouched) if the row holding join.Slot is connected under a different client.
        internal bool Upsert(ApprovedJoin join)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                var held = Roster[i];
                if (held.Slot == join.Slot && held.Connected && held.ClientId != join.ClientId)
                {
                    Debug.LogError($"SessionState: slot {join.Slot} is already held by connected client {held.ClientId}; refusing client {join.ClientId} ({join.Key.Id}).");
                    return false;
                }
            }

            var entry = new PlayerSlot
            {
                ClientId = join.ClientId,
                Id = NetPlayerId.From(join.Key),
                Name = SessionText.Name(DisplayNameRules.Sanitize(join.Name, join.Slot)),
                Slot = join.Slot,
                Connected = true,
                Ready = false,
            };

            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].Slot == join.Slot)
                {
                    // T16-R1: a different persistent identity taking the slot over starts with a fresh creel and debug wallet;
                    // the same identity rejoining keeps both (R46).
                    bool takeover = !Roster[i].Id.Equals(entry.Id);
                    if (takeover && ServerCreels != null)
                    {
                        ServerCreels.ClearSlot(join.Slot);
                    }

                    Roster[i] = entry;
                    // Host-side fishing state (rod, bait, bait stock, pity streak) is reset by FishingHost through this hook.
                    if (takeover) EventIsolation.Invoke(SlotTakenOver, (int)join.Slot);
                    return true;
                }
            }

            Roster.Add(entry);
            return true;
        }

        // Host only. Keeps the slot for reclaim; clears Connected and Ready.
        private void MarkDisconnected(ulong clientId)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                var e = Roster[i];
                if (!e.Connected || e.ClientId != clientId) continue;
                e.Connected = false;
                e.Ready = false;
                Roster[i] = e;
                return;
            }
        }

        private void OnRosterListChanged(NetworkListEvent<PlayerSlot> change)
        {
            RosterChanged?.Invoke();
        }

        private void OnJoinsOpenChanged(bool previous, bool next)
        {
            RosterChanged?.Invoke();
        }
    }
}
