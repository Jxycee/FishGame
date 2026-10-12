using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Networking
{
    // Authority: host. In Solo and in dev-direct host mode this process is the host: it owns join approval
    // (ConnectionApprover -> Core JoinRules) and the approval -> roster handoff (JoinApprovalBridge).
    // A dev-direct client only follows NGO. Plain UnityTransport on loopback; never touches UGS.
    // Main thread only. Dev-direct (host or join) is refused unless Debug.isDebugBuild.
    public sealed partial class LocalNetworkSession : INetworkSession
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
                return UnityEngine.Debug.isDebugBuild;
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
            EventIsolation.Invoke(StateChanged, from, to);
        }

        private void RaiseEnded(EndReason reason)
        {
            EventIsolation.Invoke(Ended, reason);
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
    }
}
