// Authority: test-only (client-side UI; no networked state). Drives the real Busy screen through UiFlowController with a fake online
// session installed through AppBootstrap.OnlineSessionFactoryOverride (T2, CCR-A3): an online Cancel shows "Cancelling..." at once and
// returns to the main menu as soon as the session reports the cancel, never waiting on the SDK's slow teardown.
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using FishGame.App;
using FishGame.Core;
using FishGame.Runtime;
using FishGame.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FishGame.Tests.PlayMode
{
    // An INetworkSession whose cancel completes late, like UgsNetworkSession.EndCoreAsync: it reports Connecting -> Failed -> ShuttingDown
    // right away (or when told), and only CompleteLate finishes the teardown (Offline, Ended, the start task faulting with UserLeft).
    internal sealed class LateCancelSession : INetworkSession
    {
        private TaskCompletionSource<string> _hosting;

        public bool ReportOnCancel;
        public bool CancelSeen;
        public int HostCalls;

        public ConnState State { get; private set; }
        public SessionMode Mode => State == ConnState.Offline ? SessionMode.None : SessionMode.Online;
        public bool IsHost => true;
        public string JoinCode => null;

        public event Action<ConnState, ConnState> StateChanged;
        public event Action<EndReason> Ended;

        public bool HostingPending => _hosting != null && !_hosting.Task.IsCompleted;

        public Task<string> HostOnlineAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)   // D8: an already-cancelled token faults before any state change
                return Task.FromException<string>(new NetworkSessionException(EndReason.UserLeft, "cancelled"));

            HostCalls++;
            _hosting = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetState(ConnState.Connecting);
            ct.Register(() =>
            {
                CancelSeen = true;
                if (ReportOnCancel) ReportCancel();
            });
            return _hosting.Task;
        }

        // What UgsNetworkSession.EndCoreAsync does synchronously before its up-to-30 s teardown.
        public void ReportCancel()
        {
            if (State != ConnState.Connecting) return;
            SetState(ConnState.Failed);
            SetState(ConnState.ShuttingDown);
        }

        // The teardown finishing: Offline, Ended(UserLeft), and the start task faulting.
        public void CompleteLate()
        {
            if (!HostingPending) return;
            ReportCancel();
            if (State != ConnState.Offline) SetState(ConnState.Offline);
            Ended?.Invoke(EndReason.UserLeft);
            _hosting.TrySetException(new NetworkSessionException(EndReason.UserLeft, "cancelled late"));
        }

        public Task LeaveAsync()
        {
            CompleteLate();
            if (State != ConnState.Offline) SetState(ConnState.Offline);
            return Task.CompletedTask;
        }

        public Task JoinOnlineAsync(string code, CancellationToken ct) => Task.CompletedTask;
        public Task StartLocalAsync(bool allowRemote, CancellationToken ct) => Task.CompletedTask;
        public Task JoinDirectAsync(string ip, ushort port, CancellationToken ct) => Task.CompletedTask;
        public Task SetJoinableAsync(bool open) => Task.CompletedTask;
        public void Dispose() { }

        private void SetState(ConnState next)
        {
            ConnState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }

    public class UiFlowCancelTests
    {
        private LateCancelSession _fake;
        private UiFlowController _ui;
        private VisualElement _root;
        private int _toasts;
        private IDisposable _toastSubscription;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AppBootstrap.OnlineSessionFactoryOverride = null;
            _toastSubscription?.Dispose();
            _toastSubscription = null;
            _fake?.CompleteLate();
            _fake = null;
            yield return PlayModeHarness.TearDownApp();
        }

        // Presses the Busy screen's Cancel and the Host button the way the UI does (a navigation submit on the button).
        private static void Click(Button b)
        {
            Assert.That(b, Is.Not.Null, "button");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        private IEnumerator OpenHostAttempt(bool reportOnCancel)
        {
            _fake = new LateCancelSession { ReportOnCancel = reportOnCancel };
            AppBootstrap.OnlineSessionFactoryOverride = () => _fake;
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();

            AppBootstrap app = AppBootstrap.Current;
            _ui = app.GetComponentInChildren<UiFlowController>(true);
            Assert.That(_ui, Is.Not.Null, "UiFlowController");
            _root = _ui.GetComponent<UIDocument>().rootVisualElement;
            _toasts = 0;
            _toastSubscription = app.Services.Get<EventBus>().Subscribe<ToastRequested>(t => _toasts++);

            Click(_root.Q<Button>("HostButton"));
            yield return PlayModeHarness.WaitFrames(2, 0f);
            Assert.That(_fake.HostCalls, Is.EqualTo(1), "the click reached StartAttempt (if this fails, the click helper is wrong)");
            Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.Busy));
            Assert.That(_root.Q<Label>("BusyLabel").text, Is.EqualTo("Creating session..."));
        }

        // Catches: the Busy screen held until the SDK returns (back to the menu only when the 30 s teardown ends); no "Cancelling..." text;
        // menu actions re-enabled before the session is Offline; a leaked or duplicate state handler (a second attempt or HostCalls); a toast
        // on a user cancel.
        [UnityTest]
        public IEnumerator UiFlow_OnlineCancel_ShowsCancellingImmediatelyAndReturnsToMenu()
        {
            yield return OpenHostAttempt(false);
            using (var capture = new PlayModeHarness.LogCapture())
            {
                Click(_root.Q<Button>("CancelButton"));   // no yield: everything below must already hold
                Assert.That(_fake.CancelSeen, Is.True, "the cancel reached the attempt's token");
                Assert.That(_root.Q<Label>("BusyLabel").text, Is.EqualTo("Cancelling..."), "shown at once");
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.Busy), "the session has not reported the cancel yet");

                _fake.ReportCancel();
                yield return PlayModeHarness.WaitFrames(1, 0f);
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.MainMenu), "back as soon as the session reports the cancel");
                Assert.That(_root.Q<Button>("HostButton").enabledSelf, Is.False, "menu actions stay off while the attempt is still pending");
                Assert.That(_fake.HostingPending, Is.True, "the start task is still pending");

                yield return PlayModeHarness.WaitFrames(10, 0.2f);
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.MainMenu));
                Assert.That(_fake.HostCalls, Is.EqualTo(1), "no second attempt");

                _fake.CompleteLate();
                yield return PlayModeHarness.WaitUntil(() => _root.Q<Button>("HostButton").enabledSelf, 5f,
                    "the menu actions never came back after the late completion");
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.MainMenu));
                Assert.That(_toasts, Is.EqualTo(0), "a user cancel shows no toast");
                capture.AssertNoWarnings("online cancel");
            }
        }

        // Catches: the cancel handler subscribed after _attempt.Cancel(): a session that reports the cancel inside Cancel() (LocalNetworkSession
        // style) would then never reach the handler and the Busy screen would stay up.
        [UnityTest]
        public IEnumerator UiFlow_OnlineCancel_ReportedInsideCancel_ReturnsToMenuSameFrame()
        {
            yield return OpenHostAttempt(true);
            using (var capture = new PlayModeHarness.LogCapture())
            {
                Click(_root.Q<Button>("CancelButton"));   // no yield
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.MainMenu), "the session reported inside Cancel(): the menu is back in the same frame");

                _fake.CompleteLate();
                yield return PlayModeHarness.WaitUntil(() => _root.Q<Button>("HostButton").enabledSelf, 5f,
                    "the menu actions never came back after the late completion");
                Assert.That(_toasts, Is.EqualTo(0));
                capture.AssertNoWarnings("online cancel inside Cancel");
            }
        }
    }
}
