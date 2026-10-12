using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace FishGame.Networking
{
    // Part of LocalNetworkSession (see LocalNetworkSession.cs for the authority comment): attempt lifecycle (AttemptAsync,
    // cancel, timeout watch) and the NGO start (setup, port loop, StartNgo, ShutdownNgoAsync, approver helper). Split only to
    // keep each file near ~300 lines.
    public sealed partial class LocalNetworkSession
    {
        private async Task AttemptAsync(SessionMode mode, bool asHost, string address, ushort firstPort, int portAttempts, CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LocalNetworkSession));
            }
            if (ct.IsCancellationRequested)
            {
                throw new NetworkSessionException(EndReason.UserLeft);
            }
            if (_sm.State != ConnState.Offline)
            {
                throw new InvalidOperationException("Cannot start a session while " + _sm.State + ".");
            }
            if (Nm == null)
            {
                throw new NetworkSessionException(EndReason.TransportFailure, "NetworkManager is missing.");
            }

            _attemptId++;
            int attempt = _attemptId;
            _mode = mode;
            _isHost = asHost;
            _endReason = EndReason.UserLeft;
            _endedRaised = false;
            _suppressStopCallbacks = false;
            _beginTime = Time.realtimeSinceStartupAsDouble;
            ActivePort = 0;
            _startTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _attemptCts = new CancellationTokenSource();
            Task startTask = _startTcs.Task;
            CancellationToken attemptToken = _attemptCts.Token;

            SubscribeNetwork();
            _sm.Fire(ConnEvent.Begin);

            SynchronizationContext context = SynchronizationContext.Current;
            using (ct.Register(() => PostCancel(context, attempt)))
            {
                _ = WatchTimeoutAsync(attempt);
                _ = SetupAndStartAsync(attempt, address, firstPort, portAttempts, attemptToken);
                await startTask;
            }
        }

        // Cancel callbacks can run on any thread (e.g. CancelAfter); hop back to the main thread first.
        private void PostCancel(SynchronizationContext context, int attempt)
        {
            if (context != null)
            {
                context.Post(_ => CancelAttempt(attempt), null);
            }
            else
            {
                CancelAttempt(attempt);
            }
        }

        private void CancelAttempt(int attempt)
        {
            if (attempt == _attemptId && _sm.State == ConnState.Connecting)
            {
                Fail(EndReason.UserLeft);
            }
        }

        private async Task SetupAndStartAsync(int attempt, string address, ushort firstPort, int portAttempts, CancellationToken token)
        {
            try
            {
                NetworkManager nm = Nm;
                await NetworkManagerAwait.WaitForShutdownAsync(nm, token);
                if (!IsCurrent(attempt))
                {
                    return;
                }
                if (nm.IsListening)
                {
                    Debug.LogWarning("[LocalNetworkSession] NetworkManager was still running; shutting it down before starting.");
                    await ShutdownNgoAsync();
                    if (!IsCurrent(attempt))
                    {
                        return;
                    }
                }

                UnityTransport utp = TransportSetup.Get(nm);
                TransportSetup.ApplyTimeouts(utp, _config.LongTimeout);
                nm.NetworkConfig.ProtocolVersion = NetworkConstants.NgoProtocolVersion;
                nm.NetworkConfig.ConnectionData = ConnectPayloadCodec.Encode(new ConnectPayload(
                    _config.ProtocolVersion,
                    _config.ContentHash,
                    new PlayerKey(IdentityKind.Local, _config.Settings.EnsureLocalPlayerId()),
                    _config.Settings.Current.displayName));

                UninstallApprover();
                _approver = new ConnectionApprover(_config, _mode);
                _approver.Install();

                bool started = false;
                int attempts = _isHost ? Math.Max(1, portAttempts) : 1;
                for (int i = 0; i < attempts; i++)
                {
                    int candidate = firstPort + i;
                    if (candidate > ushort.MaxValue)
                    {
                        break;
                    }
                    ushort port = (ushort)candidate;
                    ActivePort = port;
                    TransportSetup.ConfigureDirect(utp, address, port, _isHost ? NetworkConstants.LoopbackAddress : null);

                    started = StartNgo(nm);
                    if (started)
                    {
                        break;
                    }
                    if (_isHost && i < attempts - 1)
                    {
                        Debug.LogWarning("[LocalNetworkSession] StartHost failed on port " + port + "; retrying on the next port.");
                    }
                    await ShutdownNgoAsync();
                    if (!IsCurrent(attempt))
                    {
                        return;
                    }
                }

                if (!IsCurrent(attempt))
                {
                    return;
                }
                if (!started)
                {
                    Fail(EndReason.TransportFailure);
                    return;
                }
                if (_isHost)
                {
                    // D37: after StartHost returned, never from inside OnServerStarted.
                    Established();
                }
                // Client: Established comes from OnConnectionEvent ClientConnected for the local id.
            }
            catch (OperationCanceledException)
            {
                // The attempt ended (timeout, cancel or leave) while waiting for the previous shutdown.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (IsCurrent(attempt))
                {
                    Fail(EndReason.TransportFailure);
                }
            }
        }

        // An exception inside NGO's Initialize runs its internal shutdown with roles still set, which raises the stop
        // callbacks while StartHost/StartClient is on the stack. Those are part of a failed start, not an unexpected stop.
        private bool StartNgo(NetworkManager nm)
        {
            _suppressStopCallbacks = true;
            try
            {
                return _isHost ? nm.StartHost() : nm.StartClient();
            }
            finally
            {
                _suppressStopCallbacks = false;
            }
        }

        private async Task ShutdownNgoAsync()
        {
            _suppressStopCallbacks = true;
            try
            {
                await NetworkManagerAwait.ShutdownAsync(Nm);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _suppressStopCallbacks = false;
            }
        }

        private async Task WatchTimeoutAsync(int attempt)
        {
            await Task.Delay(TimeSpan.FromSeconds(NetworkConstants.AsyncTimeoutSeconds));
            if (attempt == _attemptId && _sm.State == ConnState.Connecting)
            {
                Fail(EndReason.Timeout);
            }
        }

        private bool IsCurrent(int attempt)
        {
            return attempt == _attemptId && _sm.State == ConnState.Connecting;
        }

        private void UninstallApprover()
        {
            if (_approver != null)
            {
                _approver.Uninstall();
                _approver = null;
            }
        }
    }
}
