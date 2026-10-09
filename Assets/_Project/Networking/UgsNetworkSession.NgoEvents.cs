using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace FishGame.Networking
{
    // Part of UgsNetworkSession (see UgsNetworkSession.cs for the authority comment and recorded findings):
    // NGO callbacks. They run on the main thread. Split only to keep each file under ~300 lines.
    public sealed partial class UgsNetworkSession
    {
        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (_endTcs != null)
            {
                return; // ending: teardown clears the bridge and the seat cache
            }

            if (!_isHost)
            {
                if (_sm.State == ConnState.Connecting
                    && data.EventType == ConnectionEvent.ClientConnected
                    && data.ClientId == nm.LocalClientId)
                {
                    _sm.Fire(ConnEvent.Established);
                }

                return;
            }

            if (data.ClientId == NetworkManager.ServerClientId)
            {
                return;
            }

            if (data.EventType == ConnectionEvent.ClientConnected)
            {
                // Cache the UGS PlayerId now, so the seat can be freed even if SessionState forgets the client first.
                if (TryResolveKey(data.ClientId, out var key) && key.Kind == IdentityKind.Ugs)
                {
                    _ugsIdByClient[data.ClientId] = key.Id;
                }
            }
            else if (data.EventType == ConnectionEvent.ClientDisconnected)
            {
                if (!_ugsIdByClient.TryGetValue(data.ClientId, out var playerId))
                {
                    playerId = TryResolveKey(data.ClientId, out var key) && key.Kind == IdentityKind.Ugs ? key.Id : null;
                }

                _ugsIdByClient.Remove(data.ClientId);
                _config.Bridge.RemovePending(data.ClientId);

                // Free the Lobby seat, or a rejoin would fail as "full" (spec 6.3).
                var host = _host;
                if (host != null && !string.IsNullOrEmpty(playerId))
                {
                    _ = RemoveSeatAsync(host, playerId);
                }
            }
        }

        private bool TryResolveKey(ulong clientId, out PlayerKey key)
        {
            var bridge = _config.Bridge;
            var provider = bridge.Provider;
            if (provider != null && provider.TryGetKey(clientId, out key))
            {
                return true;
            }

            bridge.GetPending(_pendingScratch); // clears the list first
            foreach (var pending in _pendingScratch)
            {
                if (pending.ClientId == clientId)
                {
                    key = pending.Key;
                    return true;
                }
            }

            key = default;
            return false;
        }

        private static async Task RemoveSeatAsync(IHostSession host, string playerId)
        {
            try
            {
                await UgsAsync.WithTimeoutAsync(host.RemovePlayerAsync(playerId), CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] RemovePlayerAsync({playerId}) failed (ignored): {e}");
            }
        }

        // DisconnectClassifier's timing window counts from the NGO client start, not from sign-in.
        private void OnClientStarted()
        {
            if (!_isHost)
            {
                _clock = System.Diagnostics.Stopwatch.StartNew();
            }
        }

        private void OnServerStopped(bool wasClient)
        {
            OnNgoStopped(EndReason.TransportFailure);
        }

        // NGO 2.13.3 raises OnClientStopped before OnServerStopped, and a host is also a client.
        // The host ignores this one so it ends with TransportFailure from OnServerStopped, never HostLeft.
        private void OnClientStopped(bool wasHost)
        {
            if (wasHost || _isHost)
            {
                return;
            }

            var reason = DisconnectClassifier.Classify(
                false,
                _sm.State == ConnState.Connected,
                _config.NetworkManager.DisconnectReason,
                _clock == null ? 0.0 : _clock.Elapsed.TotalSeconds);
            OnNgoStopped(reason);
        }

        // NGO stopped on its own. Our own teardown is ignored (the end has started by then).
        // While Connecting only the reason is recorded: the running attempt fails with it.
        // While Connected the end runs one frame later, outside NGO's shutdown callback.
        private void OnNgoStopped(EndReason reason)
        {
            if (_endTcs != null)
            {
                return;
            }

            if (!_ngoStopReason.HasValue)
            {
                _ngoStopReason = reason;
            }

            if (_sm.State == ConnState.Connected)
            {
                _ = EndNextFrameAsync(_attemptId);
            }
        }

        private async Task EndNextFrameAsync(int attempt)
        {
            await Task.Yield();
            if (attempt == _attemptId && _ngoStopReason.HasValue)
            {
                await End(_ngoStopReason.Value, true);
            }
        }
    }
}
