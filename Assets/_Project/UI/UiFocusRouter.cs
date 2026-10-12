using System;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: client-only (local UI). Menu focus: which control gets focus on each screen, the pending retry when the
    // target was disabled, gamepad focus recovery, and the menu-actions enable state. Split out of UiFlowController; plain C#.
    // Reads the controller's live screen and bound state through delegates.
    internal sealed class UiFocusRouter
    {
        private enum MenuFocus : byte { Default = 0, Solo = 1, JoinCode = 2 }

        private readonly VisualElement _root;
        private readonly VisualElement _mainMenu;
        private readonly MainMenuPresenter _menu;
        private readonly BusyPresenter _busyPresenter;
        private readonly SettingsPresenter _settingsPresenter;
        private readonly PausePresenter _pausePresenter;
        private readonly Func<UiScreen> _currentScreen;
        private readonly Func<bool> _isBound;

        private MenuFocus _menuFocus = MenuFocus.Default;
        private bool _menuActionsEnabled = true;
        private bool _menuFocusPending;          // a menu focus attempt failed (target disabled) and must be retried

        internal UiFocusRouter(VisualElement root, VisualElement mainMenu, MainMenuPresenter menu, BusyPresenter busy,
            SettingsPresenter settings, PausePresenter pause, Func<UiScreen> currentScreen, Func<bool> isBound)
        {
            _root = root;
            _mainMenu = mainMenu;
            _menu = menu;
            _busyPresenter = busy;
            _settingsPresenter = settings;
            _pausePresenter = pause;
            _currentScreen = currentScreen;
            _isBound = isBound;
        }

        internal void ClearPending()
        {
            _menuFocusPending = false;
        }

        internal void PreferFocusFor(EndReason reason)
        {
            if (reason == EndReason.ServicesUnavailable) _menuFocus = MenuFocus.Solo;        // "Play Solo instead"
            else if (reason == EndReason.InvalidCode) _menuFocus = MenuFocus.JoinCode;
        }

        internal void RefocusIfLost(UiScreen screen, InputDeviceKind lastDevice)
        {
            if (IsMenuScreen(screen) && lastDevice == InputDeviceKind.Gamepad && FocusedElement() == null)
                FocusScreen(screen);
        }

        // Enables or disables the menu actions. On a disabled -> enabled change while the menu is open, re-applies the
        // pending focus (the earlier attempt may have left focus on nothing because the target was still disabled).
        internal void SetMenuActions(bool enabled, bool refocus)
        {
            var wasEnabled = _menuActionsEnabled;
            _menuActionsEnabled = enabled;
            _menu.SetActionsEnabled(enabled);
            if (!refocus || !enabled || wasEnabled || _currentScreen() != UiScreen.MainMenu || !_menuFocusPending) return;
            // Never steal focus from an enabled control the user is already in inside the menu.
            if (FocusedElement() is VisualElement current && _mainMenu.Contains(current) && current.enabledInHierarchy)
            {
                _menuFocusPending = false;
                return;
            }
            FocusScreen(UiScreen.MainMenu);
        }

        internal void FocusScreen(UiScreen screen)
        {
            if (!_isBound() || _currentScreen() != screen) return;
            switch (screen)
            {
                case UiScreen.MainMenu:
                    // The pending focus is consumed only once its target could actually take focus.
                    bool focused;
                    if (_menuFocus == MenuFocus.Solo) focused = _menu.FocusSolo();
                    else if (_menuFocus == MenuFocus.JoinCode) focused = _menu.FocusJoinCode();
                    else focused = _menu.FocusDefault();
                    _menuFocusPending = !focused;
                    if (focused) _menuFocus = MenuFocus.Default;
                    break;
                case UiScreen.Busy: _busyPresenter.FocusDefault(); break;
                case UiScreen.Settings: _settingsPresenter.FocusDefault(); break;
                case UiScreen.Pause: _pausePresenter.FocusDefault(); break;
            }
        }

        private Focusable FocusedElement() => _root?.panel?.focusController?.focusedElement;

        internal void BlurFocus() => FocusedElement()?.Blur();

        internal static bool IsMenuScreen(UiScreen screen) =>
            screen == UiScreen.MainMenu || screen == UiScreen.Busy || screen == UiScreen.Settings || screen == UiScreen.Pause;
    }
}
