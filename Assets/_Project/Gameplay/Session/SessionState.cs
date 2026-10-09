// Authority: host writes Roster and NetJoinsOpen; every peer reads. Spawned by AppBootstrap on OnServerStarted with destroyWithScene: false.
// Roster entries are never removed in M1: a disconnected entry keeps its slot (Connected = false) so the same identity can reclaim it.
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

        public NetworkList<PlayerSlot> Roster;
        public readonly NetworkVariable<bool> NetJoinsOpen = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action RosterChanged;

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
            Upsert(join);
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
        private void Upsert(ApprovedJoin join)
        {
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
                    Roster[i] = entry;
                    return;
                }
            }

            Roster.Add(entry);
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
