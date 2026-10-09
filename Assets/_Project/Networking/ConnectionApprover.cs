using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Networking
{
    // Authority: host. NGO calls Handle on the server only (each remote join, and the host's own client inside StartHost).
    // Every decision goes through Core JoinRules. Approved joins are parked in the JoinApprovalBridge for SessionState.
    public sealed class ConnectionApprover
    {
        private readonly NetworkSessionConfig _config;
        private readonly SessionMode _mode;
        private readonly Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> _callback;
        private readonly List<RosterEntry> _roster = new List<RosterEntry>();
        private readonly List<ApprovedJoin> _pending = new List<ApprovedJoin>();

        public ConnectionApprover(NetworkSessionConfig config, SessionMode mode)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mode = mode;
            _callback = Handle;
        }

        // Assignment, never +=. NGO throws on a multicast approval callback.
        // ConnectionApproval is part of NGO's config hash, so clients set it too.
        public void Install()
        {
            NetworkManager nm = _config.NetworkManager;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = _callback;
        }

        // Clears the callback only while it is still ours, so a stale approver never removes a newer one.
        public void Uninstall()
        {
            NetworkManager nm = _config.NetworkManager;
            if (ReferenceEquals(nm, null))
            {
                return;
            }
            if (nm.ConnectionApprovalCallback == _callback)
            {
                nm.ConnectionApprovalCallback = null;
            }
        }

        public void Handle(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            try
            {
                Evaluate(request, response);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Reject(response, EndReason.Unknown);
            }
        }

        private void Evaluate(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (!ConnectPayloadCodec.TryDecode(request.Payload, out ConnectPayload payload) || !payload.Key.IsValid)
            {
                Reject(response, EndReason.VersionMismatch);
                return;
            }

            ulong clientId = request.ClientNetworkId;
            JoinApprovalBridge bridge = _config.Bridge;
            IJoinContextProvider provider = bridge.Provider;

            // Roster = provider slots (empty if none) + pending approvals counted as connected (D4).
            _roster.Clear();
            if (provider != null)
            {
                provider.GetRoster(_roster);
            }
            bridge.GetPending(_pending);
            for (int i = 0; i < _pending.Count; i++)
            {
                _roster.Add(new RosterEntry(_pending[i].Key, _pending[i].Slot, true));
            }
            bool joinsOpen = provider == null || provider.JoinsOpen;

            var joinRequest = new JoinRequest(
                clientId,
                clientId == NetworkManager.ServerClientId,
                payload.ProtocolVersion,
                payload.ContentHash,
                payload.Key,
                payload.Name);
            var context = new JoinContext(
                _mode,
                _config.ProtocolVersion,
                _config.ContentHash,
                joinsOpen,
                NetworkConstants.MaxPlayers,
                _roster);

            JoinVerdict verdict = JoinRules.Evaluate(joinRequest, context);
            if (!verdict.Approved)
            {
                Reject(response, verdict.Reason);
                return;
            }

            string name = DisplayNameRules.Sanitize(payload.Name, verdict.Slot);
            bridge.AddApproved(new ApprovedJoin(clientId, payload.Key, name, verdict.Slot, verdict.Reclaimed));
            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;
        }

        // NGO sends Reason to the pending client before dropping it; the client parses it with DisconnectClassifier.
        private static void Reject(NetworkManager.ConnectionApprovalResponse response, EndReason reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason.ToString();
        }
    }
}
