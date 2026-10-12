using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace FishGame.Networking
{
    // Part of UgsNetworkSession (see UgsNetworkSession.cs for the authority comment and recorded findings):
    // ending, teardown and session-handle tracking. Split only to keep each file under ~300 lines.
    public sealed partial class UgsNetworkSession
    {
        // ---- Ending --------------------------------------------------------------------------------

        // Starts the end of the current attempt exactly once; later calls get the same task. Never faults.
        // The result is the reason Ended was (or will be) raised with.
        private Task<EndReason> End(EndReason reason, bool failed)
        {
            if (_endTcs != null)
            {
                return _endTcs.Task;
            }

            if (_sm.State == ConnState.Offline)
            {
                return Task.FromResult(_endReason);
            }

            var tcs = new TaskCompletionSource<EndReason>(TaskCreationOptions.RunContinuationsAsynchronously);
            _endTcs = tcs;
            _endReason = reason;
            _ = EndCoreAsync(tcs, reason, failed);
            return tcs.Task;
        }

        private async Task EndCoreAsync(TaskCompletionSource<EndReason> tcs, EndReason reason, bool failed)
        {
            try
            {
                if (_sm.State == ConnState.Connecting)
                {
                    _sm.Fire(ConnEvent.Fail, reason);
                }
                else if (_sm.State == ConnState.Connected)
                {
                    _sm.Fire(failed ? ConnEvent.UnexpectedStop : ConnEvent.Leave, reason);
                }

                if (_sm.State == ConnState.Failed)
                {
                    _sm.Fire(ConnEvent.Leave, reason);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e); // a throwing StateChanged handler must not skip teardown
            }

            try
            {
                await TeardownAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                if (_sm.State == ConnState.ShuttingDown || _sm.State == ConnState.Failed)
                {
                    _sm.Fire(ConnEvent.ShutdownComplete, reason);
                }

                EventIsolation.Invoke(Ended, reason); // exactly once per attempt; SceneFlow turns it into a toast (D8)
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            tcs.TrySetResult(reason);
        }

        // Order: let the SDK call finish (bounded), leave the session (bounded, errors ignored), then shut NGO down.
        private async Task TeardownAsync()
        {
            var nm = _config.NetworkManager;

            var inFlight = _inFlight;
            _inFlight = null;
            if (inFlight != null
                && !await UgsAsync.WaitAtMostAsync(inFlight, NetworkConstants.AsyncTimeoutSeconds))
            {
                Debug.LogWarning("[UGS] The SDK call is still running; its session will be left when it finishes.");
            }

            _sessionReleased = true;
            var session = _session;
            _session = null;
            _host = null;
            _joinCode = null;
            if (session != null)
            {
                await LeaveQuietlyAsync(session);
            }

            if (nm != null)
            {
                if (_subscribed)
                {
                    nm.OnConnectionEvent -= OnConnectionEvent;
                    nm.OnClientStarted -= OnClientStarted;
                    nm.OnServerStopped -= OnServerStopped;
                    nm.OnClientStopped -= OnClientStopped;
                    _subscribed = false;
                }

                await NetworkManagerAwait.ShutdownAsync(nm);
            }

            _approver?.Uninstall();
            _approver = null;
            _config.Bridge.Clear();
            _ugsIdByClient.Clear();
        }

        private static async Task LeaveQuietlyAsync(ISession session)
        {
            try
            {
                await UgsAsync.WithTimeoutAsync(session.LeaveAsync(), CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] ISession.LeaveAsync failed (ignored): {e}");
            }
        }

        // Keeps the session handle even when the caller timed out or cancelled, so teardown can leave it.
        // A call that finishes after its attempt was torn down is left at once.
        private async Task TrackSessionAsync<T>(Task<T> call, int attempt) where T : ISession
        {
            ISession session;
            try
            {
                session = await call;
            }
            catch (Exception e)
            {
                if (attempt != _attemptId || _sessionReleased)
                {
                    Debug.LogWarning($"[UGS] Late session call failed after its attempt ended (ignored): {e}");
                }

                return; // the attempt itself logs and maps the error
            }

            if (session == null)
            {
                return;
            }

            if (attempt == _attemptId && !_sessionReleased)
            {
                _session = session;
                return;
            }

            Debug.LogWarning("[UGS] A session call finished after its attempt ended. Leaving that session.");
            await LeaveQuietlyAsync(session);
        }
    }
}
