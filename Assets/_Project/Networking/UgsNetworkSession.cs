using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace FishGame.Networking
{
    // Authority: host owns the Sessions lobby (lock state, player removal). The SDK owns NGO start and stop on this path.
    //
    // Recorded findings (M1 criterion 13). Read from multiplayer 2.3.3 / authentication 3.8.0 source on 2026-10-09.
    // Each [pending] is filled in after the manual acceptance run (docs/superpowers/plans/2026-10-09-m1-acceptance.md), not by T16/T17.
    //  - The Sessions NGO handler calls StartHost/StartClient/Shutdown itself and calls UnityTransport.SetRelayServerData
    //    itself (F1). This class never calls StartHost, StartClient or SetConnectionData.
    //  - NetworkManager.Singleton must exist before CreateSessionAsync/JoinSessionByCodeAsync, else
    //    SessionException(NetworkManagerNotInitialized) (F1).
    //  - The inspector Protocol Type does not matter on the Sessions path: SetRelayServerData switches to Relay, and every
    //    local start calls SetConnectionData, which switches back to plain UnityTransport.
    //  - CreateSessionAsync returns only after NGO is listening. JoinSessionByCodeAsync returns only after the client is
    //    connected (IsConnectedClient, polled every 1 s). The SDK trims the code but does not change its case.
    //  - The SDK handler keeps its NGO session until ISession.LeaveAsync, and calling NetworkManager.Shutdown directly
    //    logs "Use ISession.LeaveAsync instead". Teardown therefore always leaves the session first, even after NGO
    //    stopped on its own; otherwise the next online start would skip starting NGO ("Session already started").
    //  - LobbyFull (16004), LobbyLocked (16005) and InvalidJoinCode (16010) arrive as SessionError.Unknown with message
    //    text only, and non-session exceptions are wrapped as Unknown with their message (F3). UgsErrorMap decides
    //    FullLobby and InvalidCode from the message.
    //  - If the network start fails inside a join (e.g. approval rejected), the SDK leaves the lobby itself.
    //  - MPPM clones get their auth profile from "-editor-mode -name <x>" (F4), so UgsBootstrap sets a profile only for
    //    -dev-profile instances. Distinct clone PlayerIds: [pending]. If not distinct: SwitchProfile while signed out.
    //  - Still to confirm at runtime: real code format/length/case (V3): [pending]; exact LobbyFull message: [pending]; exact LobbyLocked message text: [pending];
    //    Solo after a Sessions run in one process: [pending]; perf baseline (10.7): [pending].
    public sealed partial class UgsNetworkSession : INetworkSession
    {
        // Compile-time guard: error CS0221 on this line means UgsErrorMap's mirrored SessionError values are wrong.
        private const uint SessionErrorMirrorGuard =
            (uint)((int)SessionError.Unknown - UgsErrorMap.SessionErrorUnknown)
            + (uint)(UgsErrorMap.SessionErrorUnknown - (int)SessionError.Unknown)
            + (uint)((int)SessionError.SessionNotFound - UgsErrorMap.SessionErrorSessionNotFound)
            + (uint)(UgsErrorMap.SessionErrorSessionNotFound - (int)SessionError.SessionNotFound)
            + (uint)((int)SessionError.InvalidParameter - UgsErrorMap.SessionErrorInvalidParameter)
            + (uint)(UgsErrorMap.SessionErrorInvalidParameter - (int)SessionError.InvalidParameter);

        private readonly NetworkSessionConfig _config;
        private readonly ConnectionStateMachine _sm = new ConnectionStateMachine();
        private readonly Dictionary<ulong, string> _ugsIdByClient = new Dictionary<ulong, string>();
        private readonly List<ApprovedJoin> _pendingScratch = new List<ApprovedJoin>();

        private int _attemptId;
        private bool _isHost;
        private bool _subscribed;
        private bool _sessionReleased;
        private bool _disposed;
        private ConnectionApprover _approver;
        private ISession _session;
        private IHostSession _host;
        private string _joinCode;
        private Task _inFlight;
        private TaskCompletionSource<EndReason> _endTcs;
        private EndReason _endReason = EndReason.Unknown;
        private EndReason? _ngoStopReason;
        private System.Diagnostics.Stopwatch _clock;

        public event Action<ConnState, ConnState> StateChanged;
        public event Action<EndReason> Ended;

        // Must not touch UGS: Solo never constructs this class, and constructing it does nothing online.
        public UgsNetworkSession(NetworkSessionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sm.Changed += OnStateMachineChanged;
        }

        // Each subscriber is isolated (EventIsolation): a throwing StateChanged handler is logged, the others still run, and the state machine never sees the exception.
        private void OnStateMachineChanged(ConnState prev, ConnState next)
        {
            EventIsolation.Invoke(StateChanged, prev, next);
        }

        public ConnState State => _sm.State;
        public SessionMode Mode => _sm.State == ConnState.Offline ? SessionMode.None : SessionMode.Online;
        public bool IsHost => _isHost && _sm.State != ConnState.Offline;
        public string JoinCode => _joinCode;

        public Task<string> HostOnlineAsync(CancellationToken ct)
        {
            return RunAttemptAsync<string>(true, ct, HostBodyAsync);
        }

        public Task JoinOnlineAsync(string code, CancellationToken ct)
        {
            return RunAttemptAsync<bool>(false, ct, c => JoinBodyAsync(code, c));
        }

        public Task StartLocalAsync(bool allowRemote, CancellationToken ct)
        {
            throw new NotSupportedException("UgsNetworkSession does not run Solo or dev-direct sessions.");
        }

        public Task JoinDirectAsync(string ip, ushort port, CancellationToken ct)
        {
            throw new NotSupportedException("UgsNetworkSession does not run Solo or dev-direct sessions.");
        }

        // Mirrors joins-open into the Lobby lock (D35). Nice-to-have: approval is the guarantee, so failures are logged only.
        public async Task SetJoinableAsync(bool open)
        {
            var host = _host;
            if (host == null || _sm.State != ConnState.Connected)
            {
                return;
            }

            try
            {
                host.IsLocked = !open;
                await UgsAsync.WithTimeoutAsync(host.SavePropertiesAsync(), CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] SetJoinableAsync({open}) failed (ignored): {e}");
            }
        }

        // Idempotent: a no-op while Offline; while ending it returns the same in-flight end task.
        public Task LeaveAsync()
        {
            if (_sm.State == ConnState.Offline)
            {
                return Task.CompletedTask;
            }

            return End(EndReason.UserLeft, false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _ = LeaveAsync();
        }

        // ---- Attempts ------------------------------------------------------------------------------

        private async Task<T> RunAttemptAsync<T>(bool host, CancellationToken ct, Func<CancellationToken, Task<T>> body)
        {
            // Contract D8: a token already cancelled faults with UserLeft before any state change, event or UGS call.
            if (ct.IsCancellationRequested)
            {
                throw new NetworkSessionException(EndReason.UserLeft, "The online attempt was cancelled.");
            }

            StartAttempt(host);
            try
            {
                ct.ThrowIfCancellationRequested(); // backstop: cancelled between the check above and here
                return await body(ct);
            }
            catch (OperationCanceledException oce) when (ct.IsCancellationRequested)
            {
                var reason = await End(EndReason.UserLeft, false);
                throw new NetworkSessionException(reason, "The online attempt was cancelled.", oce);
            }
            catch (NetworkSessionException ex)
            {
                var reason = await End(ClientStopReasonOr(ex.Reason), true);
                if (ex.Reason == reason)
                {
                    throw;
                }

                throw new NetworkSessionException(reason, ex.Message, ex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UGS] Online {(host ? "host" : "join")} failed (raw error, never shown to the player): {ex}");
                var mapped = ex is SessionException se
                    ? UgsErrorMap.Map((int)se.Error, se.Message)
                    : EndReason.ServicesUnavailable;
                var reason = await End(ClientStopReasonOr(mapped), true);
                throw new NetworkSessionException(reason, "The online session failed.", ex);
            }
        }

        private void StartAttempt(bool host)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UgsNetworkSession));
            }

            if (_sm.State != ConnState.Offline)
            {
                throw new InvalidOperationException("A session attempt is already in progress.");
            }

            _attemptId++;
            _endTcs = null;
            _endReason = EndReason.Unknown;
            _ngoStopReason = null;
            _sessionReleased = false;
            _isHost = host;
            _joinCode = null;
            _session = null;
            _host = null;
            _inFlight = null;
            _ugsIdByClient.Clear();
            _clock = System.Diagnostics.Stopwatch.StartNew();
            _sm.Fire(ConnEvent.Begin);
        }

        private async Task<string> HostBodyAsync(CancellationToken ct)
        {
            var nm = _config.NetworkManager;
            await PrepareAsync(ct);

            var options = new SessionOptions { MaxPlayers = NetworkConstants.MaxPlayers, IsPrivate = true }.WithRelayNetwork();
            var call = MultiplayerService.Instance.CreateSessionAsync(options);
            _inFlight = TrackSessionAsync(call, _attemptId);
            var host = await UgsAsync.WithTimeoutAsync(call, ct);
            ThrowIfEnding();

            _host = host;
            _joinCode = host.Code; // exactly as given (V3)
            await WaitUntilAsync(() => nm.IsServer && nm.IsListening, ct);
            _sm.Fire(ConnEvent.Established); // after the SDK call returned, never inside an NGO callback (D37)
            return _joinCode;
        }

        private async Task<bool> JoinBodyAsync(string code, CancellationToken ct)
        {
            var normalized = FishGame.Core.JoinCode.Normalize(code);
            if (!FishGame.Core.JoinCode.IsPlausible(normalized))
            {
                throw new NetworkSessionException(EndReason.InvalidCode, "The join code is not valid."); // no UGS call
            }

            await PrepareAsync(ct);
            var call = MultiplayerService.Instance.JoinSessionByCodeAsync(normalized);
            _inFlight = TrackSessionAsync(call, _attemptId);
            var session = await UgsAsync.WithTimeoutAsync(call, ct);
            ThrowIfEnding();

            _joinCode = string.IsNullOrEmpty(session.Code) ? normalized : session.Code;
            await WaitUntilAsync(() => _sm.State == ConnState.Connected, ct); // Established fires in OnConnectionEvent
            return true;
        }

        // Shared setup: wait for NGO to be fully down, sign in, then set the payload, timeouts and approver before the SDK
        // starts NGO. Never calls SetConnectionData (the SDK sets relay data, F1).
        private async Task PrepareAsync(CancellationToken ct)
        {
            var nm = _config.NetworkManager;
            if (nm == null)
            {
                throw new NetworkSessionException(EndReason.Unknown, "NetworkManager is missing.");
            }

            await NetworkManagerAwait.WaitForShutdownAsync(nm, ct);
            await UgsAsync.WithTimeoutAsync(UgsBootstrap.EnsureSignedInAsync(ct), ct);
            ThrowIfEnding();

            var playerId = AuthenticationService.Instance.PlayerId;
            if (string.IsNullOrEmpty(playerId))
            {
                throw new NetworkSessionException(EndReason.ServicesUnavailable, "Signed in without a PlayerId.");
            }

            TransportSetup.ApplyTimeouts(TransportSetup.Get(nm), _config.LongTimeout);
            nm.NetworkConfig.ProtocolVersion = NetworkConstants.NgoProtocolVersion;
            nm.NetworkConfig.ConnectionData = ConnectPayloadCodec.Encode(new ConnectPayload(
                _config.ProtocolVersion, _config.ContentHash, new PlayerKey(IdentityKind.Ugs, playerId),
                _config.Settings.Current.displayName));

            _approver = new ConnectionApprover(_config, SessionMode.Online);
            _approver.Install();

            nm.OnConnectionEvent += OnConnectionEvent;
            nm.OnClientStarted += OnClientStarted;
            nm.OnServerStopped += OnServerStopped;
            nm.OnClientStopped += OnClientStopped;
            _subscribed = true;
        }

        private void ThrowIfEnding()
        {
            if (_endTcs != null)
            {
                throw new NetworkSessionException(_endReason, "The session ended while starting.");
            }
        }

        private async Task WaitUntilAsync(Func<bool> ready, CancellationToken ct)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!ready())
            {
                ThrowIfEnding();
                ct.ThrowIfCancellationRequested();
                if (_ngoStopReason.HasValue)
                {
                    throw new NetworkSessionException(_ngoStopReason.Value, "NGO stopped while starting.");
                }

                if (clock.Elapsed.TotalSeconds > NetworkConstants.AsyncTimeoutSeconds)
                {
                    throw new NetworkSessionException(EndReason.Timeout, "Timed out while starting the session.");
                }

                await Task.Yield(); // one frame on Unity's synchronization context
            }

            ThrowIfEnding();
        }

        private EndReason ClientStopReasonOr(EndReason fallback)
        {
            // A client rejected by approval stops NGO first; the SDK then fails with a generic error. Keep the real reason.
            return !_isHost && _ngoStopReason.HasValue ? _ngoStopReason.Value : fallback;
        }
    }
}
