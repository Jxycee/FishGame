using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.UI
{
    // Authority: client-only (local UI). Session actions behind the menu, Busy and Pause buttons: start and cancel an
    // attempt, host, join, leave, dev join toggle. Split out of UiFlowController; plain C#, no MonoBehaviour. It reads the
    // controller's live state through delegates (never a copied bool), because each method resumes after an await.
    internal sealed class UiSessionActions
    {
        private readonly INetworkSession _session;
        private readonly EventBus _bus;
        private readonly MainMenuPresenter _menu;
        private readonly BusyPresenter _busyPresenter;
        private readonly PausePresenter _pausePresenter;
        private readonly Action<UiScreen> _show;
        private readonly Func<UiScreen> _currentScreen;
        private readonly Action<bool, bool> _setMenuActions;
        private readonly Func<bool> _isBound;
        private readonly Func<bool> _isLive;

        private CancellationTokenSource _attempt;
        private bool _leaving;
        private bool _cancelPending;   // a Cancel was pressed for the current attempt and the session has not reported it yet (M3 T2)

        internal UiSessionActions(INetworkSession session, EventBus bus, MainMenuPresenter menu, BusyPresenter busy,
            PausePresenter pause, Action<UiScreen> show, Func<UiScreen> currentScreen, Action<bool, bool> setMenuActions,
            Func<bool> isBound, Func<bool> isLive)
        {
            _session = session;
            _bus = bus;
            _menu = menu;
            _busyPresenter = busy;
            _pausePresenter = pause;
            _show = show;
            _currentScreen = currentScreen;
            _setMenuActions = setMenuActions;
            _isBound = isBound;
            _isLive = isLive;
        }

        internal bool HasAttempt => _attempt != null;

        internal bool IsLeaving => _leaving;

        internal void CancelForDestroy() => _attempt?.Cancel();

        // The M1 session wiring from BindScreens. Each of these events has exactly one subscriber.
        internal void Wire()
        {
            _menu.HostRequested += () => StartAttempt("busy.creating", HostAsync);
            _menu.JoinRequested += OnJoinRequested;
            _menu.SoloRequested += () => StartAttempt("busy.loading", ct => _session.StartLocalAsync(false, ct));
            _menu.DevHostRequested += () => StartAttempt("busy.creating", ct => _session.StartLocalAsync(true, ct));
            _menu.DevJoinRequested += () => StartAttempt("busy.joining",
                ct => _session.JoinDirectAsync(NetworkConstants.LoopbackAddress, NetworkConstants.DefaultPort, ct));

            _busyPresenter.CancelRequested += CancelAttempt;

            _pausePresenter.LeaveRequested += LeaveSession;
            _pausePresenter.JoinsToggleRequested += ToggleJoins;
            _pausePresenter.Copied += () => _bus.Publish(new ToastRequested("pause.copied", null, 2f));
        }

        internal void ShowPauseData()
        {
            var state = SessionState.Current;
            var code = _session.JoinCode;
            var showCode = _session.Mode == SessionMode.Online && !string.IsNullOrEmpty(code);
            var showJoins = UiQuery.IsDevBuild && _session.IsHost && _session.Mode != SessionMode.Solo && state != null;
            _pausePresenter.Show(showCode, code, showJoins, state == null || state.JoinsOpen);
            _pausePresenter.SetLeaveEnabled(!_leaving);
        }

        private async void StartAttempt(string messageKey, Func<CancellationToken, Task> operation)
        {
            if (_attempt != null || _session.State != ConnState.Offline) return;
            _menu.CommitName();                                  // the connect payload reads Current.displayName
            var cts = new CancellationTokenSource();
            _attempt = cts;
            _busyPresenter.Show(UiStrings.Get(messageKey));
            _show(UiScreen.Busy);

            var succeeded = false;
            try
            {
                await operation(cts.Token);
                succeeded = true;
            }
            catch (OperationCanceledException)
            {
                // Cancel ends the attempt with UserLeft: no toast.
            }
            catch (NetworkSessionException ex)
            {
                // Ended(reason) already fired; SceneFlow (T13) publishes the toast (D8). Raw text is logged only.
                Debug.Log("FishGame UI: session attempt ended: " + ex.Reason);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _bus.Publish(new ToastRequested("err.generic"));
            }
            finally
            {
                // An attempt that ended without a reporting state change (for example the D8 already-cancelled throw) still leaves the Busy screen.
                // A cancel that lost the race to Connected changes nothing (A-Q3): the attempt succeeded, Busy stays up and the game loads.
                if (_cancelPending && _attempt == cts)
                {
                    if (succeeded)
                    {
                        StopCancelWatch();
                        _busyPresenter.SetMessage(UiStrings.Get("busy.loading"));
                    }
                    else ReturnToMenuAfterCancel();
                }
                if (_attempt == cts) _attempt = null;
                cts.Dispose();
            }

            if (!_isLive()) return;
            if (!succeeded && _currentScreen() == UiScreen.Busy) _show(UiScreen.MainMenu);
            else _setMenuActions(_session.State == ConnState.Offline, true);
        }

        private async Task HostAsync(CancellationToken ct)
        {
            var code = await _session.HostOnlineAsync(ct);
            if (!string.IsNullOrEmpty(code)) _bus.Publish(new ToastRequested("toast.join_code", code, 10f));
        }

        private void OnJoinRequested(string code)
        {
            if (!JoinCode.IsPlausible(code))
            {
                _bus.Publish(new ToastRequested("err.code_not_found"));
                _menu.FocusJoinCode();                           // text is kept (spec 11.2)
                return;
            }
            StartAttempt("busy.joining", ct => _session.JoinOnlineAsync(code, ct));
        }

        internal void CancelAttempt()
        {
            if (_attempt != null)
            {
                if (_cancelPending) return;                                  // a second press while cancelling: nothing more to do
                _cancelPending = true;
                _busyPresenter.SetMessage(UiStrings.Get("busy.cancelling")); // at once, before the SDK returns (M2 handoff item)
                _session.StateChanged += OnStateAfterCancel;                 // subscribe BEFORE Cancel: a session may report inside Cancel()
                _attempt.Cancel();
                if (_cancelPending && CancelReported(_session.State)) ReturnToMenuAfterCancel();
                return;
            }
            // Already connected and waiting for the scene load: Cancel leaves instead.
            if (_session.State != ConnState.Offline)
            {
                _busyPresenter.SetMessage(UiStrings.Get("busy.cancelling"));
                LeaveSession();
            }
        }

        // The session has acted on the cancel once it leaves Connecting/Connected (UGS: Failed then ShuttingDown fire before its up-to-30 s teardown).
        private static bool CancelReported(ConnState s) => s == ConnState.Failed || s == ConnState.ShuttingDown || s == ConnState.Offline;

        private void OnStateAfterCancel(ConnState previous, ConnState current)
        {
            if (CancelReported(current)) ReturnToMenuAfterCancel();
        }

        // Clears the pending flag and stops watching the session for the cancel report (nothing else).
        private void StopCancelWatch()
        {
            _cancelPending = false;
            _session.StateChanged -= OnStateAfterCancel;
        }

        private void ReturnToMenuAfterCancel()
        {
            if (!_cancelPending) return;
            StopCancelWatch();
            if (_isLive() && _currentScreen() == UiScreen.Busy) _show(UiScreen.MainMenu);   // menu actions stay off: HasAttempt is still true
        }

        private async void LeaveSession()
        {
            if (_leaving) return;
            _leaving = true;
            if (_isBound()) _pausePresenter.SetLeaveEnabled(false);
            try
            {
                await _session.LeaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _leaving = false;
                if (_isLive()) _pausePresenter.SetLeaveEnabled(true);
            }
        }

        // Dev-only host command (criterion 6): SessionState.NetJoinsOpen + Sessions lock.
        private async void ToggleJoins()
        {
            var state = SessionState.Current;
            if (state == null || !state.IsServer || !_session.IsHost) return;
            var open = !state.JoinsOpen;
            state.ServerSetJoinsOpen(open);
            _pausePresenter.SetJoinsOpen(open);
            try
            {
                await _session.SetJoinableAsync(open);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
