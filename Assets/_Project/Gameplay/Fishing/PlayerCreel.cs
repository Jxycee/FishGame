// Authority: host writes NetCreel and NetDebugCoins from SessionState.ServerCreels (the slot-keyed CreelLedger, which survives a rejoin, R46);
// every peer reads. A client never writes either one. Truth lives in the ledger on the host; NetCreel is its replicated mirror.
// Server init hangs off PlayerNetState.ServerInitialized (G2): this component's OnNetworkSpawn runs inside InstantiateAndSpawn, BEFORE
// PlayerSpawner calls ServerInit (which writes NetSlot), so the mirror waits for that event unless the player is already initialised.
// Both late-joiner spawn entries end in the same ServerInit (G3), so a rejoining player gets the right creel on either path.
using System;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class PlayerCreel : NetworkBehaviour
    {
        public NetworkList<CreelEntry> NetCreel;
        public readonly NetworkVariable<int> NetDebugCoins = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerNetState _player;
        private bool _subscribed;
        private bool _waitingForServerInit;
        private bool _warnedNoLedger;

        public int Count => NetCreel != null ? NetCreel.Count : 0;

        public int TotalWeightG
        {
            get
            {
                int sum = 0;
                if (NetCreel != null)
                {
                    for (int i = 0; i < NetCreel.Count; i++) sum += NetCreel[i].WeightG;
                }
                return sum;
            }
        }

        // Every peer. Raised on any NetCreel or NetDebugCoins change (the list change also fires on the host for its own writes).
        public event Action Changed;

        private void Awake()
        {
            // Constructed here, not in a field initializer (contract F21).
            NetCreel = new NetworkList<CreelEntry>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            _player = GetComponent<PlayerNetState>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            NetCreel.OnListChanged += HandleListChanged;
            NetDebugCoins.OnValueChanged += HandleCoinsChanged;
            _subscribed = true;

            if (!IsServer)
            {
                return;
            }

            if (_player == null)
            {
                Debug.LogError("PlayerCreel: the player root has no PlayerNetState; the creel cannot be mirrored.", this);
                return;
            }

            if (_player.IsServerInitialized)
            {
                ServerMirror();
            }
            else
            {
                _player.ServerInitialized += HandleServerInitialized;
                _waitingForServerInit = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            Unsubscribe();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        // Host only. Reads the ledger (the truth); elsewhere it is built from the replicated list.
        // Throws when not spawned on the server. Before the player's ServerInit (slot not written yet) the ledger is not touched:
        // the replicated list is the answer.
        public CreelSnapshot Snapshot(int maxEntries, int maxWeightG)
        {
            RequireServer(nameof(Snapshot));
            CreelLedger ledger = _player != null && _player.IsServerInitialized ? Ledger() : null;
            if (ledger != null)
            {
                return ledger.Snapshot(_player.Slot, maxEntries, maxWeightG);
            }

            return new CreelSnapshot(Count, TotalWeightG, maxEntries, maxWeightG);
        }

        // Host only. CreelRules decides first; then the ledger stores (assigning the EntryId) and the replicated list mirrors the stored item.
        public bool ServerTryAdd(in CreelItem item, int maxEntries, int maxWeightG, out CreelItem stored)
        {
            RequireServer(nameof(ServerTryAdd));
            stored = default;
            if (_player == null || !_player.IsServerInitialized)
            {
                return false;   // slot not written yet: no ledger access
            }

            CreelLedger ledger = RequireLedger();
            byte slot = _player.Slot;
            CreelSnapshot snapshot = ledger.Snapshot(slot, maxEntries, maxWeightG);
            if (!CreelRules.CanAccept(in snapshot))
            {
                stored = default;
                return false;
            }

            stored = ledger.Add(slot, item);
            NetCreel.Add(CreelEntry.From(in stored));
            return true;
        }

        // Host only. Sells the whole creel through the ledger and mirrors the empty creel and the new debug wallet.
        public int ServerSellAll()
        {
            RequireServer(nameof(ServerSellAll));
            if (_player == null || !_player.IsServerInitialized)
            {
                return 0;   // slot not written yet: no ledger access
            }

            CreelLedger ledger = RequireLedger();
            byte slot = _player.Slot;
            int coins = ledger.SellAll(slot);
            NetCreel.Clear();
            NetDebugCoins.Value = ledger.DebugCoins(slot);
            return coins;
        }

        private void HandleServerInitialized(PlayerNetState player)
        {
            if (_waitingForServerInit)
            {
                _player.ServerInitialized -= HandleServerInitialized;
                _waitingForServerInit = false;
            }

            if (IsSpawned && IsServer)
            {
                ServerMirror();
            }
        }

        // Rebuilds the replicated creel and wallet from the slot ledger (the rejoin path: R46 creels survive in the slot).
        private void ServerMirror()
        {
            CreelLedger ledger = Ledger();
            if (ledger == null)
            {
                if (!_warnedNoLedger)
                {
                    _warnedNoLedger = true;
                    Debug.LogError("PlayerCreel: SessionState.ServerCreels is not available; the creel is not mirrored.", this);
                }
                return;
            }

            byte slot = _player.Slot;
            NetCreel.Clear();
            var items = ledger.Items(slot);
            for (int i = 0; i < items.Count; i++)
            {
                CreelItem item = items[i];
                NetCreel.Add(CreelEntry.From(in item));
            }

            NetDebugCoins.Value = ledger.DebugCoins(slot);
        }

        private static CreelLedger Ledger()
        {
            SessionState session = SessionState.Current;
            return session != null ? session.ServerCreels : null;
        }

        private void RequireServer(string member)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("PlayerCreel." + member + " is host-only and needs a spawned creel.");
            }
        }

        private CreelLedger RequireLedger()
        {
            CreelLedger ledger = Ledger();
            if (ledger == null || _player == null)
            {
                throw new InvalidOperationException("PlayerCreel: SessionState.ServerCreels or PlayerNetState is not available.");
            }

            return ledger;
        }

        private void HandleListChanged(NetworkListEvent<CreelEntry> change)
        {
            EventIsolation.Invoke(Changed);
        }

        private void HandleCoinsChanged(int previous, int current)
        {
            EventIsolation.Invoke(Changed);
        }

        private void Unsubscribe()
        {
            if (_waitingForServerInit)
            {
                if (_player != null)
                {
                    _player.ServerInitialized -= HandleServerInitialized;
                }
                _waitingForServerInit = false;
            }

            if (!_subscribed)
            {
                return;
            }

            NetCreel.OnListChanged -= HandleListChanged;
            NetDebugCoins.OnValueChanged -= HandleCoinsChanged;
            _subscribed = false;
        }
    }
}
