using System;
using FishGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Networking
{
    // Part of LocalNetworkSession (see LocalNetworkSession.cs for the authority comment): NGO subscribe/unsubscribe and the
    // three NGO callbacks. They run on the main thread. Split only to keep each file near ~300 lines.
    public sealed partial class LocalNetworkSession
    {
        private void SubscribeNetwork()
        {
            UnsubscribeNetwork();
            NetworkManager nm = Nm;
            nm.OnConnectionEvent += OnConnectionEvent;
            nm.OnClientStopped += OnClientStopped;
            nm.OnServerStopped += OnServerStopped;
        }

        // Managed-only unsubscribe; safe even after the NetworkManager GameObject is destroyed.
        private void UnsubscribeNetwork()
        {
            NetworkManager nm = Nm;
            if (ReferenceEquals(nm, null))
            {
                return;
            }
            nm.OnConnectionEvent -= OnConnectionEvent;
            nm.OnClientStopped -= OnClientStopped;
            nm.OnServerStopped -= OnServerStopped;
        }

        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (data.EventType == ConnectionEvent.ClientConnected)
            {
                if (!_isHost && _sm.State == ConnState.Connecting && data.ClientId == nm.LocalClientId)
                {
                    Established();
                }
            }
            else if (data.EventType == ConnectionEvent.ClientDisconnected)
            {
                if (_isHost)
                {
                    _config.Bridge.RemovePending(data.ClientId);
                }
            }
        }

        // Client path. The host's own client also reports here, so the host is skipped (it uses OnServerStopped).
        private void OnClientStopped(bool wasHostClient)
        {
            if (_isHost || _suppressStopCallbacks || !IsLive)
            {
                return;
            }
            bool wasConnected = _sm.State == ConnState.Connected;
            NetworkManager nm = Nm;
            string disconnectReason = ReferenceEquals(nm, null) ? null : nm.DisconnectReason;
            Fail(DisconnectClassifier.Classify(false, wasConnected, disconnectReason, SecondsSinceBegin()));
        }

        private void OnServerStopped(bool wasHost)
        {
            if (!_isHost || _suppressStopCallbacks || !IsLive)
            {
                return;
            }
            Fail(EndReason.TransportFailure);
        }

        private double SecondsSinceBegin()
        {
            return Time.realtimeSinceStartupAsDouble - _beginTime;
        }
    }
}
