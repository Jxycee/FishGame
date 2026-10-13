using System;
using System.Collections.Generic;
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
        private JournalPresenter _journal;
        private ToastPresenter _toasts;
        private NameplateOverlay _nameplates;
        private IDisposable _toastSoundSubscription;

        private UiScreen _screen = UiScreen.None;
        private UiScreen _settingsReturn = UiScreen.MainMenu;
        private UiScreen _pendingScreen = UiScreen.None;
        private bool _hasPendingScreen;
        private bool _initialized;
        private bool _started;
        private bool _bound;
        private bool _inGame;
        private int _screenChangedFrame = -1;

        private ServiceRegistry _app;
        private UiFocusRouter _focus;
        private UiSessionActions _actions;
        private readonly List<IHudExtension> _hudExtensions = new List<IHudExtension>();
        private bool _extensionsBound;
        private bool _destroyed;

        // T21: one HUD-level modal at a time (the journal; T22's dev panel). Opening sets IInputService.MenuOpen, so the Menu context applies:
        // cursor free, gameplay input off, UI map on (spec 11.1). Closing restores the screen's own value. Show() and HandleBack close it first.
        // The game is never paused by a modal (spec 12.10: the journal may stay open while the bobber is out).
        private object _modalOwner;
        private Action _modalClosed;

        public UiScreen CurrentScreen => _screen;

        internal bool ModalOpen => _modalOwner != null;

        internal bool TryOpenModal(object owner, Action closed)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (_modalOwner != null) return ReferenceEquals(_modalOwner, owner);
            if (_screen != UiScreen.Hud && _screen != UiScreen.MainMenu) return false;
            _modalOwner = owner;
            _modalClosed = closed;
            _input.MenuOpen = true;
            return true;
        }

        internal void CloseModal(object owner)
        {
            if (_modalOwner == null || !ReferenceEquals(_modalOwner, owner)) return;
            Action closed = _modalClosed;
            _modalOwner = null;
            _modalClosed = null;
            _input.MenuOpen = UiFocusRouter.IsMenuScreen(_screen);
            closed?.Invoke();
        }

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
            _app = app;

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

            if (_modalOwner != null) CloseModal(_modalOwner);
            var previous = _screen;
            _focus.ClearPending();
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

            _input.MenuOpen = UiFocusRouter.IsMenuScreen(screen);
            SetSoloPaused(screen == UiScreen.Pause || settingsOverGame);
            if (screen != UiScreen.Busy) _busyPresenter.Hide();

            switch (screen)
            {
                case UiScreen.MainMenu:
                    _menu.Refresh();
                    _menu.SetMetaSummary(JournalPresenter.SummaryText(_app));
                    _focus.SetMenuActions(_session.State == ConnState.Offline && !_actions.HasAttempt, false);
                    break;
                case UiScreen.Settings:
                    _settingsPresenter.Load();
                    _rebind.Refresh();
                    break;
                case UiScreen.Pause:
                    _actions.ShowPauseData();
                    break;
                case UiScreen.Hud:
                    _hudPresenter.Refresh();
                    break;
            }

            if (!UiFocusRouter.IsMenuScreen(screen)) _focus.BlurFocus();
            var target = screen;
            _root.schedule.Execute(() => _focus.FocusScreen(target));
        }

        // T2.4 seam for T25/T26. Binds immediately once BindScreens has finished; otherwise BindScreens binds it.
        internal void AttachHudExtension(IHudExtension ext)
        {
            if (ext == null) throw new ArgumentNullException(nameof(ext));
            if (_destroyed || _hudExtensions.Contains(ext)) return;
            _hudExtensions.Add(ext);
            if (_extensionsBound) ext.Bind(_root, _app);
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
            _menu.SettingsRequested += () => OpenSettings(UiScreen.MainMenu);
            _menu.QuitRequested += QuitApplication;
            _menu.JournalRequested += OpenJournal;
            _journal = new JournalPresenter(_root, _app, () => CloseModal(_journal));

            _busyPresenter = new BusyPresenter(_busy);

            _settingsPresenter = new SettingsPresenter(_settingsScreen, _settings);
            _settingsPresenter.BackRequested += () => Show(_settingsReturn);
            _rebind = new RebindPresenter(_settingsScreen, _bindings, PlayClick);

            _pausePresenter = new PausePresenter(_pause, _settings);
            _pausePresenter.ResumeRequested += () => Show(UiScreen.Hud);
            _pausePresenter.SettingsRequested += () => OpenSettings(UiScreen.Pause);

            _hudPresenter = new HudPresenter(_hud, _bus, _input);
            AttachHudExtension(new FishingHudPresenter());
            if (Debug.isDebugBuild) AttachHudExtension(new DevOverlayPresenter());
            _toasts = new ToastPresenter(_root, _bus, _settings);
            _nameplates = new NameplateOverlay(nameplateLayer);

            _focus = new UiFocusRouter(_root, _mainMenu, _menu, _busyPresenter, _settingsPresenter, _pausePresenter,
                () => _screen, () => _bound);
            _actions = new UiSessionActions(_session, _bus, _menu, _busyPresenter, _pausePresenter,
                Show, () => _screen, _focus.SetMenuActions, () => _bound, () => _initialized && _bound);
            _actions.Wire();

            // Every UXML button plays Ui_Click on press (pointer, keyboard submit and gamepad submit all raise clicked).
            _root.Query<Button>().ForEach(button => button.clicked += PlayClick);
            _toastSoundSubscription = _bus.Subscribe<ToastRequested>(OnToastSound);

            for (int i = 0; i < _hudExtensions.Count; i++) _hudExtensions[i].Bind(_root, _app);
            _extensionsBound = true;
        }

        private void Update()
        {
            if (!_bound) return;
            if (_input.Intent.PausePressed && Time.frameCount != _screenChangedFrame) HandleBack();
            if (_screen == UiScreen.Hud && _modalOwner == null && _input.Intent.JournalPressed && Time.frameCount != _screenChangedFrame)
                OpenJournal();
            if (_journal.IsOpen) _journal.Tick(Time.unscaledDeltaTime);     // also over the main menu
            if (_screen == UiScreen.Pause) _pausePresenter.Tick();
            if (_inGame)
            {
                _hudPresenter.Tick();
                _nameplates.Tick(Camera.main);
                for (int i = 0; i < _hudExtensions.Count; i++) _hudExtensions[i].Tick(Time.unscaledDeltaTime);
            }
            if (_modalOwner != null) _journal.RecoverFocus(_input.LastDevice == InputDeviceKind.Gamepad);   // the menu behind a modal is hidden
            else _focus.RefocusIfLost(_screen, _input.LastDevice);
        }

        // T21.2: the journal over the HUD (J) or over the main menu (the Journal button). Refused during the local bite or fight.
        private void OpenJournal()
        {
            var inGame = _screen == UiScreen.Hud;
            if (!_journal.CanOpen(inGame)) return;
            if (!TryOpenModal(_journal, OnJournalClosed)) return;
            if (_screen == UiScreen.MainMenu) UiQuery.Show(_mainMenu, false);   // gamepad navigation must not reach the menu behind it
            _journal.Open(inGame);
        }

        private void OnJournalClosed()
        {
            _journal.Hide();
            if (_screen != UiScreen.MainMenu) return;
            UiQuery.Show(_mainMenu, true);
            _menu.SetMetaSummary(JournalPresenter.SummaryText(_app));
            _root.schedule.Execute(() =>
            {
                if (_screen == UiScreen.MainMenu && _modalOwner == null) _menu.FocusJournal();
            });
        }

        // Single handler for Escape / Start / UI Cancel (D27): no NavigationCancelEvent handlers exist.
        private void HandleBack()
        {
            if (_modalOwner != null)
            {
                CloseModal(_modalOwner);                        // Esc and gamepad B close the modal, never Pause behind it
                return;
            }
            switch (_screen)
            {
                case UiScreen.Hud: Show(UiScreen.Pause); break;
                case UiScreen.Pause: Show(UiScreen.Hud); break;
                case UiScreen.Settings:
                    if (!_rebind.IsRebinding) Show(_settingsReturn);
                    break;
                case UiScreen.Busy: _actions.CancelAttempt(); break;
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

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (!_bound) return;
            Show(ScreenForScene(next.name));
        }

        private void OnSessionEnded(EndReason reason)
        {
            if (!_bound) return;
            _focus.PreferFocusFor(reason);
            if (_screen == UiScreen.Busy) Show(UiScreen.MainMenu);
        }

        private void OnSessionStateChanged(ConnState previous, ConnState current)
        {
            if (!_bound) return;
            if (_screen == UiScreen.Busy && current == ConnState.Connected)
                _busyPresenter.SetMessage(UiStrings.Get("busy.loading"));
            _focus.SetMenuActions(current == ConnState.Offline && !_actions.HasAttempt, true);
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

        private void PlayClick() => _audio.PlayUi(SfxIds.UiClick);

        private void OnToastSound(ToastRequested toast)
        {
            if (toast.Key == null) return;
            if (toast.Key.StartsWith("err.", StringComparison.Ordinal)) _audio.PlayUi(SfxIds.UiError);
            else if (toast.Key == "toast.join_code") _audio.PlayUi(SfxIds.UiConfirm);
        }

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
            _destroyed = true;
            if (!_initialized) return;
            _initialized = false;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _settings.Changed -= OnSettingsChanged;
            _session.StateChanged -= OnSessionStateChanged;
            _session.Ended -= OnSessionEnded;
            _toastSoundSubscription?.Dispose();
            _toasts?.Dispose();
            _hudPresenter?.Dispose();
            if (_extensionsBound)
            {
                for (int i = _hudExtensions.Count - 1; i >= 0; i--) _hudExtensions[i].Unbind();
                _extensionsBound = false;
            }
            _rebind?.Dispose();
            _journal?.Dispose();
            _actions?.CancelForDestroy();
            _bound = false;
            if (_panelCopy != null) Destroy(_panelCopy);
        }
    }
}
