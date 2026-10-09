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
    // Authority: host. In Solo and in dev-direct host mode this process is the host: it owns join approval
    // (ConnectionApprover -> Core JoinRules) and the approval -> roster handoff (JoinApprovalBridge).
    // A dev-direct client only follows NGO. Plain UnityTransport on loopback; never touches UGS.
    // Main thread only. Dev-direct (host or join) is refused unless UNITY_EDITOR || DEVELOPMENT_BUILD.
    public sealed class LocalNetworkSession : INetworkSession
    {
        private const string DevOnlyMessage = "Dev-direct is only available in the editor or in a development build.";

        private readonly NetworkSessionConfig _config;
        private readonly ConnectionStateMachine _sm = new ConnectionStateMachine();
        private ConnectionApprover _approver;
        private SessionMode _mode = SessionMode.None;
        private bool _isHost;
        private EndReason _endReason = EndReason.UserLeft;
        private bool _endedRaised = true;
        private bool _suppressStopCallbacks;
        private bool _disposed;
        private int _attemptId;
        private double _beginTime;
        private TaskCompletionSource<bool> _startTcs;
        private CancellationTokenSource _attemptCts;
        private Task _teardown;

        public LocalNetworkSession(NetworkSessionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sm.Changed += OnStateMachineChanged;
        }

        // First port tried. PlayMode tests set NetworkConstants.TestPort.
        public ushort Port { get; set; } = NetworkConstants.DefaultPort;

        // The port actually bound (host) or joined (client) by the current or last attempt; 0 before the first.
        public ushort ActivePort { get; private set; }

        public ConnState State => _sm.State;
        public SessionMode Mode => _sm.State == ConnState.Offline ? SessionMode.None : _mode;
        public bool IsHost => _sm.State != ConnState.Offline && _isHost;
        public string JoinCode => null;

        public event Action<ConnState, ConnState> StateChanged;
        public event Action<EndReason> Ended;

        private NetworkManager Nm => _config.NetworkManager;
        private bool IsLive => _sm.State == ConnState.Connecting || _sm.State == ConnState.Connected;

        private static bool DevAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public Task<string> HostOnlineAsync(CancellationToken ct)
        {
            throw new NotSupportedException("LocalNetworkSession cannot host online sessions.");
        }

        public Task JoinOnlineAsync(string code, CancellationToken ct)
        {
            throw new NotSupportedException("LocalNetworkSession cannot join online sessions.");
        }

        public async Task StartLocalAsync(bool allowRemote, CancellationToken ct)
        {
            if (allowRemote)
            {
                if (!DevAllowed)
                {
                    throw new NetworkSessionException(EndReason.Unknown, DevOnlyMessage);
                }
                // Dev-direct host: 127.0.0.1, exactly Port, no retry (D22, D23).
                await AttemptAsync(SessionMode.DevDirect, true, NetworkConstants.LoopbackAddress, Port, 1, ct);
                return;
            }
            // Solo: 127.0.0.1, Port .. Port + SoloPortRetryCount - 1.
            await AttemptAsync(SessionMode.Solo, true, NetworkConstants.LoopbackAddress, Port, NetworkConstants.SoloPortRetryCount, ct);
        }

        public async Task JoinDirectAsync(string ip, ushort port, CancellationToken ct)
        {
            if (!DevAllowed)
            {
                throw new NetworkSessionException(EndReason.Unknown, DevOnlyMessage);
            }
            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new ArgumentException("An IPv4 address is required.", nameof(ip));
            }
            await AttemptAsync(SessionMode.DevDirect, false, ip.Trim(), port, 1, ct);
        }

        // Local sessions have no Lobby lock; approval (rule 5) is the guarantee.
        public Task SetJoinableAsync(bool open)
        {
            return Task.CompletedTask;
        }

        public async Task LeaveAsync()
        {
            if (_sm.State == ConnState.Connecting)
            {
                Fail(EndReason.UserLeft);
            }
            RequestShutdown();
            Task pending = _teardown;
            if (pending != null)
            {
                await pending;
            }
        }

        // Does not shut NGO down. Callers use LeaveAsync first.
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            UnsubscribeNetwork();
            UninstallApprover();
            DisposeAttemptToken();
        }

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

        private void Established()
        {
            if (_sm.State != ConnState.Connecting)
            {
                return;
            }
            _sm.Fire(ConnEvent.Established);
            TaskCompletionSource<bool> tcs = _startTcs;
            if (tcs != null)
            {
                tcs.TrySetResult(true);
            }
        }

        // Connecting -> Fail; Connected -> UnexpectedStop. Start task: faulted with NetworkSessionException(reason) (UserLeft for a cancel). Then shut down.
        private void Fail(EndReason reason)
        {
            if (!IsLive)
            {
                return;
            }

            _endReason = reason;
            CancelAttemptToken();
            if (_sm.State == ConnState.Connecting)
            {
                _sm.Fire(ConnEvent.Fail, reason);
            }
            else
            {
                _sm.Fire(ConnEvent.UnexpectedStop, reason);
            }

            TaskCompletionSource<bool> tcs = _startTcs;
            if (tcs != null)
            {
                tcs.TrySetException(new NetworkSessionException(reason));
            }
            RequestShutdown();
        }

        private void RequestShutdown()
        {
            if (_teardown != null || _sm.State == ConnState.Offline)
            {
                return;
            }
            _teardown = TeardownAsync();
        }

        // Leave -> NGO shutdown -> cleanup -> ShutdownComplete (Offline) -> Ended(reason), exactly once.
        private async Task TeardownAsync()
        {
            await Task.Yield(); // never shut NGO down from inside one of its own callbacks
            try
            {
                if (_sm.State == ConnState.Connecting)
                {
                    Fail(EndReason.UserLeft);
                }
                if (_sm.State == ConnState.Connected || _sm.State == ConnState.Failed)
                {
                    _sm.Fire(ConnEvent.Leave, _endReason);
                }
                CancelAttemptToken();
                await ShutdownNgoAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                UnsubscribeNetwork();
                UninstallApprover();
                _config.Bridge.Clear();
                DisposeAttemptToken();

                // Capture and reset first: an Offline/Ended subscriber may start a new attempt synchronously.
                EndReason reason = _endReason;
                bool raise = !_endedRaised;
                _endedRaised = true;
                _teardown = null;

                if (_sm.State == ConnState.ShuttingDown || _sm.State == ConnState.Failed)
                {
                    _sm.Fire(ConnEvent.ShutdownComplete);
                }
                if (raise)
                {
                    RaiseEnded(reason);
                }
            }
        }

        private void OnStateMachineChanged(ConnState from, ConnState to)
        {
            try
            {
                StateChanged?.Invoke(from, to);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RaiseEnded(EndReason reason)
        {
            try
            {
                Ended?.Invoke(reason);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void UninstallApprover()
        {
            if (_approver != null)
            {
                _approver.Uninstall();
                _approver = null;
            }
        }

        private void CancelAttemptToken()
        {
            CancellationTokenSource cts = _attemptCts;
            if (cts == null)
            {
                return;
            }
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void DisposeAttemptToken()
        {
            CancellationTokenSource cts = _attemptCts;
            _attemptCts = null;
            if (cts != null)
            {
                cts.Dispose();
            }
        }

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
