using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: client-only (local UI). Owns the screen state and IInputService.MenuOpen, starts and cancels session
    // attempts, and drives Pause, Settings and the HUD. Services: INetworkSession, ISettings, IInputService, IInputBindings,
    // EventBus, AudioBus. AppBootstrap.Start calls Initialize (Awake would also work); the UIDocument tree is bound in Start (it may not exist yet).
    [DefaultExecutionOrder(-500)]
    public sealed class UiFlowController : MonoBehaviour
    {
        private enum MenuFocus : byte { Default = 0, Solo = 1, JoinCode = 2 }

        private const int MinUiScalePercent = 80;
        private const int MaxUiScalePercent = 150;

        private ISettings _settings;
        private IInputService _input;
        private IInputBindings _bindings;
        private INetworkSession _session;
        private EventBus _bus;
        private AudioBus _audio;

        private UIDocument _document;
        private PanelSettings _panelCopy;
        private VisualElement _root;
        private VisualElement _mainMenu;
        private VisualElement _busy;
        private VisualElement _settingsScreen;
        private VisualElement _pause;
        private VisualElement _hud;

        private MainMenuPresenter _menu;
        private BusyPresenter _busyPresenter;
        private SettingsPresenter _settingsPresenter;
        private RebindPresenter _rebind;
        private PausePresenter _pausePresenter;
        private HudPresenter _hudPresenter;
        private ToastPresenter _toasts;
        private NameplateOverlay _nameplates;
        private IDisposable _toastSoundSubscription;

        private CancellationTokenSource _attempt;
        private UiScreen _screen = UiScreen.None;
        private UiScreen _settingsReturn = UiScreen.MainMenu;
        private UiScreen _pendingScreen = UiScreen.None;
        private bool _hasPendingScreen;
        private MenuFocus _menuFocus = MenuFocus.Default;
        private bool _initialized;
        private bool _started;
        private bool _bound;
        private bool _leaving;
        private bool _inGame;
        private int _screenChangedFrame = -1;

        public UiScreen CurrentScreen => _screen;

        public void Initialize(ServiceRegistry app)
        {
            if (_initialized) return;
            if (app == null) throw new ArgumentNullException(nameof(app));
            _settings = app.Get<ISettings>();
            _input = app.Get<IInputService>();
            _bindings = app.Get<IInputBindings>();
            _session = app.Get<INetworkSession>();
            _bus = app.Get<EventBus>();
            _audio = app.Get<AudioBus>();

            _settings.Changed += OnSettingsChanged;
            _session.StateChanged += OnSessionStateChanged;
            _session.Ended += OnSessionEnded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            _initialized = true;
            if (_started) Bind();
        }

        public void Show(UiScreen screen)
        {
            if (!_bound)
            {
                _pendingScreen = screen;
                _hasPendingScreen = true;
                return;
            }

            var previous = _screen;
            if (previous == UiScreen.Settings && screen != UiScreen.Settings) CloseSettings();

            _screen = screen;
            _screenChangedFrame = Time.frameCount;
            var settingsOverGame = screen == UiScreen.Settings && _settingsReturn == UiScreen.Pause;
            _inGame = screen == UiScreen.Hud || screen == UiScreen.Pause || settingsOverGame;

            UiQuery.Show(_mainMenu, screen == UiScreen.MainMenu);
            UiQuery.Show(_busy, screen == UiScreen.Busy);
            UiQuery.Show(_settingsScreen, screen == UiScreen.Settings);
            UiQuery.Show(_pause, screen == UiScreen.Pause);
            UiQuery.Show(_hud, _inGame);
            _nameplates.SetActive(_inGame);

            _input.MenuOpen = IsMenuScreen(screen);
            SetSoloPaused(screen == UiScreen.Pause || settingsOverGame);
            if (screen != UiScreen.Busy) _busyPresenter.Hide();

            switch (screen)
            {
                case UiScreen.MainMenu:
                    _menu.Refresh();
                    _menu.SetActionsEnabled(_session.State == ConnState.Offline && _attempt == null);
                    break;
                case UiScreen.Settings:
                    _settingsPresenter.Load();
                    _rebind.Refresh();
                    break;
                case UiScreen.Pause:
                    ShowPauseData();
                    break;
                case UiScreen.Hud:
                    _hudPresenter.Refresh();
                    break;
            }

            if (!IsMenuScreen(screen)) BlurFocus();
            var target = screen;
            _root.schedule.Execute(() => FocusScreen(target));
        }

        private void Start()
        {
            _started = true;
            if (_initialized) Bind();
        }

        private void Bind()
        {
            if (_bound) return;
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                Debug.LogError("UiFlowController needs a UIDocument on the same GameObject.");
                return;
            }

            var original = _document.panelSettings;
            if (original != null)
            {
                _panelCopy = Instantiate(original);           // D29: never modify the asset at runtime
                _panelCopy.name = original.name + " (Runtime)";
                _document.panelSettings = _panelCopy;
            }
            else
            {
                Debug.LogError("UiFlowController: the UIDocument has no PanelSettings; UI scale is disabled.");
            }

            _root = _document.rootVisualElement;
            if (_root == null)
            {
                Debug.LogError("UiFlowController: the UIDocument has no root visual element.");
                return;
            }

            BindScreens();
            _bound = true;
            ApplyUiScale();
            var first = _hasPendingScreen ? _pendingScreen : ScreenForScene(SceneManager.GetActiveScene().name);
            _hasPendingScreen = false;
            Show(first);
        }

        private void BindScreens()
        {
            _mainMenu = UiQuery.Req<VisualElement>(_root, "ScreenMainMenu");
            _busy = UiQuery.Req<VisualElement>(_root, "ScreenBusy");
            _settingsScreen = UiQuery.Req<VisualElement>(_root, "ScreenSettings");
            _pause = UiQuery.Req<VisualElement>(_root, "ScreenPause");
            _hud = UiQuery.Req<VisualElement>(_root, "ScreenHud");
            var nameplateLayer = UiQuery.Req<VisualElement>(_root, "NameplateLayer");

            _menu = new MainMenuPresenter(_mainMenu, _settings);
            _menu.HostRequested += () => StartAttempt("busy.creating", HostAsync);
            _menu.JoinRequested += OnJoinRequested;
            _menu.SoloRequested += () => StartAttempt("busy.loading", ct => _session.StartLocalAsync(false, ct));
            _menu.DevHostRequested += () => StartAttempt("busy.creating", ct => _session.StartLocalAsync(true, ct));
            _menu.DevJoinRequested += () => StartAttempt("busy.joining",
                ct => _session.JoinDirectAsync(NetworkConstants.LoopbackAddress, NetworkConstants.DefaultPort, ct));
            _menu.SettingsRequested += () => OpenSettings(UiScreen.MainMenu);
            _menu.QuitRequested += QuitApplication;

            _busyPresenter = new BusyPresenter(_busy);
            _busyPresenter.CancelRequested += CancelAttempt;

            _settingsPresenter = new SettingsPresenter(_settingsScreen, _settings);
            _settingsPresenter.BackRequested += () => Show(_settingsReturn);
            _rebind = new RebindPresenter(_settingsScreen, _bindings, PlayClick);

            _pausePresenter = new PausePresenter(_pause, _settings);
            _pausePresenter.ResumeRequested += () => Show(UiScreen.Hud);
            _pausePresenter.SettingsRequested += () => OpenSettings(UiScreen.Pause);
            _pausePresenter.LeaveRequested += LeaveSession;
            _pausePresenter.JoinsToggleRequested += ToggleJoins;
            _pausePresenter.Copied += () => _bus.Publish(new ToastRequested("pause.copied", null, 2f));

            _hudPresenter = new HudPresenter(_hud, _bus, _input);
            _toasts = new ToastPresenter(_root, _bus, _settings);
            _nameplates = new NameplateOverlay(nameplateLayer);

            // Every UXML button plays Ui_Click on press (pointer, keyboard submit and gamepad submit all raise clicked).
            _root.Query<Button>().ForEach(button => button.clicked += PlayClick);
            _toastSoundSubscription = _bus.Subscribe<ToastRequested>(OnToastSound);
        }

        private void Update()
        {
            if (!_bound) return;
            if (_input.Intent.PausePressed && Time.frameCount != _screenChangedFrame) HandleBack();
            if (_screen == UiScreen.Pause) _pausePresenter.Tick();
            if (_inGame)
            {
                _hudPresenter.Tick();
                _nameplates.Tick(Camera.main);
            }
            if (IsMenuScreen(_screen) && _input.LastDevice == InputDeviceKind.Gamepad && FocusedElement() == null)
                FocusScreen(_screen);
        }

        // Single handler for Escape / Start / UI Cancel (D27): no NavigationCancelEvent handlers exist.
        private void HandleBack()
        {
            switch (_screen)
            {
                case UiScreen.Hud: Show(UiScreen.Pause); break;
                case UiScreen.Pause: Show(UiScreen.Hud); break;
                case UiScreen.Settings:
                    if (!_rebind.IsRebinding) Show(_settingsReturn);
                    break;
                case UiScreen.Busy: CancelAttempt(); break;
            }
        }

        private void OpenSettings(UiScreen returnTo)
        {
            _settingsReturn = returnTo;
            Show(UiScreen.Settings);
        }

        // Spec 11.6: save when the panel closes, whichever way it closes.
        private void CloseSettings()
        {
            _rebind.CancelActive();
            _settingsPresenter.Commit();
            _settings.Save();
        }

        private void ShowPauseData()
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
            Show(UiScreen.Busy);

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
                if (_attempt == cts) _attempt = null;
                cts.Dispose();
            }

            if (!_initialized || !_bound) return;
            if (!succeeded && _screen == UiScreen.Busy) Show(UiScreen.MainMenu);
            else _menu.SetActionsEnabled(_session.State == ConnState.Offline);
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

        private void CancelAttempt()
        {
            if (_attempt != null)
            {
                _attempt.Cancel();
                return;
            }
            // Already connected and waiting for the scene load: Cancel leaves instead.
            if (_session.State != ConnState.Offline) LeaveSession();
        }

        private async void LeaveSession()
        {
            if (_leaving) return;
            _leaving = true;
            if (_bound) _pausePresenter.SetLeaveEnabled(false);
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
                if (_initialized && _bound) _pausePresenter.SetLeaveEnabled(true);
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

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (!_bound) return;
            Show(ScreenForScene(next.name));
        }

        private void OnSessionEnded(EndReason reason)
        {
            if (!_bound) return;
            if (reason == EndReason.ServicesUnavailable) _menuFocus = MenuFocus.Solo;        // "Play Solo instead"
            else if (reason == EndReason.InvalidCode) _menuFocus = MenuFocus.JoinCode;
            if (_screen == UiScreen.Busy) Show(UiScreen.MainMenu);
        }

        private void OnSessionStateChanged(ConnState previous, ConnState current)
        {
            if (!_bound) return;
            if (_screen == UiScreen.Busy && current == ConnState.Connected)
                _busyPresenter.SetMessage(UiStrings.Get("busy.loading"));
            _menu.SetActionsEnabled(current == ConnState.Offline && _attempt == null);
        }

        private void OnSettingsChanged(SettingsData data) => ApplyUiScale();

        private void ApplyUiScale()
        {
            if (_panelCopy == null || _settings == null) return;
            var percent = Mathf.Clamp(_settings.Current.uiScalePercent, MinUiScalePercent, MaxUiScalePercent);
            _panelCopy.scale = percent / 100f;
        }

        private void SetSoloPaused(bool paused)
        {
            if (_session.Mode != SessionMode.Solo) return;
            var game = ServiceRoots.Game;
            if (game != null && game.TryGet<IPauseState>(out var pause)) pause.SetPaused(paused);
        }

        private void FocusScreen(UiScreen screen)
        {
            if (!_bound || _screen != screen) return;
            switch (screen)
            {
                case UiScreen.MainMenu:
                    var focus = _menuFocus;
                    _menuFocus = MenuFocus.Default;
                    if (focus == MenuFocus.Solo) _menu.FocusSolo();
                    else if (focus == MenuFocus.JoinCode) _menu.FocusJoinCode();
                    else _menu.FocusDefault();
                    break;
                case UiScreen.Busy: _busyPresenter.FocusDefault(); break;
                case UiScreen.Settings: _settingsPresenter.FocusDefault(); break;
                case UiScreen.Pause: _pausePresenter.FocusDefault(); break;
            }
        }

        private Focusable FocusedElement() => _root?.panel?.focusController?.focusedElement;

        private void BlurFocus() => FocusedElement()?.Blur();

        private void PlayClick() => _audio.PlayUi(SfxIds.UiClick);

        private void OnToastSound(ToastRequested toast)
        {
            if (toast.Key == null) return;
            if (toast.Key.StartsWith("err.", StringComparison.Ordinal)) _audio.PlayUi(SfxIds.UiError);
            else if (toast.Key == "toast.join_code") _audio.PlayUi(SfxIds.UiConfirm);
        }

        private static bool IsMenuScreen(UiScreen screen) =>
            screen == UiScreen.MainMenu || screen == UiScreen.Busy || screen == UiScreen.Settings || screen == UiScreen.Pause;

        private static UiScreen ScreenForScene(string sceneName)
        {
            if (sceneName == SceneNames.Menu) return UiScreen.MainMenu;
            if (SceneNames.IsGameplay(sceneName)) return UiScreen.Hud;
            return UiScreen.None;   // Bootstrap
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationQuit()
        {
            if (!_initialized) return;
            if (_bound && _screen == UiScreen.Settings) _settingsPresenter.Commit();
            _settings.Save();                                   // spec 11.6: saved on quit
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            _initialized = false;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _settings.Changed -= OnSettingsChanged;
            _session.StateChanged -= OnSessionStateChanged;
            _session.Ended -= OnSessionEnded;
            _toastSoundSubscription?.Dispose();
            _toasts?.Dispose();
            _hudPresenter?.Dispose();
            _rebind?.Dispose();
            _attempt?.Cancel();
            _bound = false;
            if (_panelCopy != null) Destroy(_panelCopy);
        }
    }
}
