// Authority: local client only. Reads hardware input once per frame; nothing here is networked.
// The only reader of InputActions (spec 8.1). Single owner of the cursor lock policy (contract D28) and of binding overrides.
// Works on a runtime Instantiate copy of the serialized asset, so rebinding never dirties the asset (contract D15).
// Journal (J / Select) is read into PlayerInputFrame.JournalPressed; the router passes it in Free, RodReady and RodWaiting only (M3-D27).
// Dev keys F3-F7, F9 are read directly from Keyboard.current (not part of the rebindable asset); dev builds only.
using System;
using FishGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishGame.Runtime
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PlayerInputAdapter : MonoBehaviour, IInputService, IInputBindings, IDevInput
    {
        private InputActionAsset _actions;
        private ISettings _settings;

        private InputActionMap _playerMap, _fishingMap, _uiMap;
        private InputAction _move, _look, _jump, _sprint, _primary, _secondary, _interact, _shove, _drop, _equip, _journal, _pause;
        private InputAction _reel, _steer, _uiCancel;
        private InputAction[] _deviceProbe = Array.Empty<InputAction>();

        private InputContext _gameplayContext = InputContext.Free;
        private bool _menuOpen;
        private InputContext _appliedContext = InputContext.Free;

        private PlayerInputFrame _frame;
        private PlayerIntent _intent;
        private FightInput _fight;

        private bool _hasFocus = true;
        private InputDeviceKind _lastDevice = InputDeviceKind.KeyboardMouse;
        private bool _sprintLatched;
        private bool _aimLatched;
        private int _skipLookFrames;

        public InputContext Context => _menuOpen ? InputContext.Menu : _gameplayContext;

        public InputContext GameplayContext
        {
            get => _gameplayContext;
            set
            {
                if (_gameplayContext == value) return;
                _gameplayContext = value;
                ApplyContextIfChanged();
            }
        }

        public bool MenuOpen
        {
            get => _menuOpen;
            set
            {
                if (_menuOpen == value) return;
                _menuOpen = value;
                ApplyContextIfChanged();
            }
        }

        public PlayerIntent Intent => _intent;
        public FightInput Fight => _fight;
        public bool HasFocus => _hasFocus;
        public InputDeviceKind LastDevice => _lastDevice;
        public InputActionAsset Actions => _actions;

        public event Action<InputContext> ContextChanged;

        public void Initialize(InputActionAsset asset, ISettings settings)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ReleaseActions();
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            _settings = settings;
            _settings.Changed += OnSettingsChanged;

            _actions = Instantiate(asset);
            _actions.name = asset.name;

            string json = _settings.Current.bindingOverridesJson;
            if (!string.IsNullOrEmpty(json))
            {
                try { _actions.LoadBindingOverridesFromJson(json); }
                catch (Exception e) { Debug.LogWarning("[Input] Ignoring invalid binding overrides: " + e.Message); }
            }

            CacheActions();
            _appliedContext = Context;
            ApplyMaps(_appliedContext);
            ApplyCursor(_appliedContext);
        }

        private void Awake()
        {
            _hasFocus = Application.isFocused;
        }

        private void CacheActions()
        {
            _playerMap = _actions.FindActionMap(InputActionNames.PlayerMap, throwIfNotFound: true);
            _fishingMap = _actions.FindActionMap(InputActionNames.FishingMap, throwIfNotFound: true);
            _uiMap = _actions.FindActionMap(InputActionNames.UiMap, throwIfNotFound: true);

            _move = _actions.FindAction(InputActionNames.Move, throwIfNotFound: true);
            _look = _actions.FindAction(InputActionNames.Look, throwIfNotFound: true);
            _jump = _actions.FindAction(InputActionNames.Jump, throwIfNotFound: true);
            _sprint = _actions.FindAction(InputActionNames.Sprint, throwIfNotFound: true);
            _primary = _actions.FindAction(InputActionNames.Primary, throwIfNotFound: true);
            _secondary = _actions.FindAction(InputActionNames.Secondary, throwIfNotFound: true);
            _interact = _actions.FindAction(InputActionNames.Interact, throwIfNotFound: true);
            _shove = _actions.FindAction(InputActionNames.Shove, throwIfNotFound: true);
            _drop = _actions.FindAction(InputActionNames.Drop, throwIfNotFound: true);
            _equip = _actions.FindAction(InputActionNames.Equip, throwIfNotFound: true);
            _journal = _actions.FindAction(InputActionNames.Journal, throwIfNotFound: true);
            _pause = _actions.FindAction(InputActionNames.Pause, throwIfNotFound: true);
            _reel = _actions.FindAction(InputActionNames.Reel, throwIfNotFound: true);
            _steer = _actions.FindAction(InputActionNames.Steer, throwIfNotFound: true);
            _uiCancel = _actions.FindAction(InputActionNames.Cancel, throwIfNotFound: true);

            _deviceProbe = new[]
            {
                _move, _look, _jump, _sprint, _primary, _secondary, _interact, _shove, _drop, _equip, _pause, _reel, _steer,
                _actions.FindAction(InputActionNames.Navigate, throwIfNotFound: true),
                _actions.FindAction(InputActionNames.Submit, throwIfNotFound: true),
                _uiCancel,
                _actions.FindAction(InputActionNames.Point, throwIfNotFound: true),
                _actions.FindAction(InputActionNames.Click, throwIfNotFound: true),
            };
        }

        private void Update()
        {
            if (_actions == null || !_hasFocus)
            {
                ClearOutputs(); // spec 8.1: unfocused window -> neutral input
                return;
            }

            UpdateLastDevice();
            _frame = ReadFrame();
            InputContext ctx = Context;
            _intent = InputContextRouter.Route(ctx, in _frame);
            _fight = InputContextRouter.RouteFight(ctx, in _frame);
        }

        private PlayerInputFrame ReadFrame()
        {
            SettingsData s = _settings.Current;
            var f = new PlayerInputFrame();

            Vector2 move = _move.ReadValue<Vector2>();
            f.MoveX = move.x;
            f.MoveY = move.y;

            Vector2 look = _look.ReadValue<Vector2>();
            if (_skipLookFrames > 0)
            {
                _skipLookFrames--; // drop the first look delta after focus regain
            }
            else if (look.x != 0f || look.y != 0f)
            {
                float yaw, pitch;
                InputControl source = _look.activeControl;
                if (source != null && source.device is Gamepad)
                {
                    float dt = Time.unscaledDeltaTime;
                    yaw = LookMath.StickToDegrees(look.x, s.gamepadLookSpeed, dt);
                    pitch = LookMath.StickToDegrees(look.y, s.gamepadLookSpeed, dt);
                }
                else
                {
                    // Input System mouse delta: +x = right, +y = up, in counts per frame.
                    yaw = LookMath.MouseToDegrees(look.x, s.mouseSensitivity);
                    pitch = LookMath.MouseToDegrees(look.y, s.mouseSensitivity);
                }
                f.LookYawDeg = yaw;
                f.LookPitchDeg = s.invertY ? -pitch : pitch;
            }

            f.JumpPressed = _jump.WasPressedThisFrame();
            f.SprintHeld = Held(_sprint, s.sprintToggle, ref _sprintLatched);
            f.PrimaryHeld = _primary.IsPressed();
            f.PrimaryPressed = _primary.WasPressedThisFrame();
            f.PrimaryReleased = _primary.WasReleasedThisFrame();
            f.SecondaryHeld = Held(_secondary, s.aimToggle, ref _aimLatched);
            f.SecondaryPressed = _secondary.WasPressedThisFrame();
            f.SecondaryReleased = _secondary.WasReleasedThisFrame();
            f.InteractPressed = _interact.WasPressedThisFrame();
            f.ShovePressed = _shove.WasPressedThisFrame();
            f.DropPressed = _drop.WasPressedThisFrame();
            f.EquipPressed = _equip.WasPressedThisFrame();
            f.JournalPressed = _journal.WasPressedThisFrame();   // the Player map is disabled in the Menu context, so this reads false there
            f.PausePressed = Context ==InputContext.Menu ? _uiCancel.WasPressedThisFrame() : _pause.WasPressedThisFrame();
            f.Steer = _steer.ReadValue<float>();
            f.Reel = _reel.IsPressed();
            return f;
        }

        // Hold, or a latched toggle when the setting is on (sprint toggle, aim toggle).
        private static bool Held(InputAction action, bool toggle, ref bool latch)
        {
            if (!toggle)
            {
                latch = false;
                return action.IsPressed();
            }
            if (action.WasPressedThisFrame()) latch = !latch;
            return latch;
        }

        // The device of whichever action performed this frame wins. Never polls Gamepad.current.wasUpdatedThisFrame,
        // because HID pads stream reports every frame even when idle.
        private void UpdateLastDevice()
        {
            for (int i = 0; i < _deviceProbe.Length; i++)
            {
                InputAction action = _deviceProbe[i];
                if (!action.WasPerformedThisFrame()) continue;
                InputControl control = action.activeControl;
                if (control == null) continue;
                InputDevice device = control.device;
                if (device is Gamepad)
                {
                    _lastDevice = InputDeviceKind.Gamepad;
                    return;
                }
                if (device is Keyboard || device is Pointer)
                {
                    _lastDevice = InputDeviceKind.KeyboardMouse;
                    return;
                }
            }
        }

        private void ClearOutputs()
        {
            _frame = default;
            _intent = PlayerIntent.None;
            _fight = default;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            if (hasFocus)
            {
                _skipLookFrames = 1;
                ApplyCursor(Context);
            }
            else
            {
                SetRumble(0f, 0f);
                ClearOutputs();
            }
        }

        private void OnSettingsChanged(SettingsData data)
        {
            if (data == null) return;
            if (!data.sprintToggle) _sprintLatched = false;
            if (!data.aimToggle) _aimLatched = false;
        }

        private void OnDisable()
        {
            SetRumble(0f, 0f);
        }

        private void OnDestroy()
        {
            SetRumble(0f, 0f);
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            ReleaseActions();
        }

        private void ReleaseActions()
        {
            if (_actions == null) return;
            _actions.Disable();
            if (Application.isPlaying) Destroy(_actions);
            else DestroyImmediate(_actions);
            _actions = null;
            _deviceProbe = Array.Empty<InputAction>();
        }

        private void ApplyContextIfChanged()
        {
            InputContext ctx = Context;
            if (ctx == _appliedContext) return;
            _appliedContext = ctx;
            if (_actions != null) ApplyMaps(ctx);
            ApplyCursor(ctx);
            ContextChanged?.Invoke(ctx);
        }

        private void ApplyMaps(InputContext ctx)
        {
            InputMaps maps = InputContextRouter.EnabledMaps(ctx);
            SetMapEnabled(_playerMap, (maps & InputMaps.Player) != 0);
            SetMapEnabled(_fishingMap, (maps & InputMaps.Fishing) != 0);
            SetMapEnabled(_uiMap, (maps & InputMaps.UI) != 0);
        }

        private static void SetMapEnabled(InputActionMap map, bool enabled)
        {
            if (map == null) return;
            if (enabled) map.Enable();
            else map.Disable();
        }

        // Cursor policy (single owner): Menu -> unlocked + visible; every other context -> locked + hidden.
        private static void ApplyCursor(InputContext ctx)
        {
            bool menu = ctx == InputContext.Menu;
            Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menu;
        }

        // Stateless on purpose: no caching, so every reader in the frame sees the same edge.
        public bool WasPressed(DevKey key)
        {
            if (!Debug.isDebugBuild || !_hasFocus || Context == InputContext.Menu) return false;
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            Key k;
            switch (key)
            {
                case DevKey.ToggleOverlay: k = Key.F3; break;
                case DevKey.NextRod: k = Key.F4; break;
                case DevKey.NextBait: k = Key.F5; break;
                case DevKey.ToggleForcedBite: k = Key.F6; break;
                case DevKey.ToggleTwitchy: k = Key.F7; break;
                case DevKey.RippleAtCrosshair: k = Key.F9; break;
                case DevKey.CycleForcedSpecies: k = Key.F10; break;
                default: return false;
            }
            return kb[k].wasPressedThisFrame;
        }

        public void SetRumble(float low, float high)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return;
            pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
        }

        public string GetBindingDisplayString(string actionPath)
        {
            if (_actions == null || string.IsNullOrEmpty(actionPath)) return "";
            InputAction action = _actions.FindAction(actionPath);
            if (action == null) return "";
            string group = _lastDevice == InputDeviceKind.Gamepad
                ? InputActionNames.GamepadScheme
                : InputActionNames.KeyboardMouseScheme;
            return action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
        }

        public void SaveBindingOverrides()
        {
            if (_actions == null || _settings == null) return;
            _settings.Current.bindingOverridesJson = _actions.SaveBindingOverridesAsJson();
            _settings.Save();
        }

        public void ResetAllBindingOverrides()
        {
            if (_actions == null) return;
            _actions.RemoveAllBindingOverrides();
            SaveBindingOverrides();
        }
    }
}
