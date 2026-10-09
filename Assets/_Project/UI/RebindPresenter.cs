using System;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only. Interactive rebinding (spec 8.3 / 11.6) for M1 single-binding actions, one button per control scheme.
    // Overrides persist via IInputBindings.SaveBindingOverrides (settings.json). Move/Look composites and Pause are M2+.
    internal sealed class RebindPresenter
    {
        private const float TimeoutSeconds = 6f;
        private const long StartDelayMs = 150;
        private static readonly string[] ActionPaths = { InputActionNames.Jump, InputActionNames.Sprint, InputActionNames.Interact };
        private static readonly string[] ActionLabelKeys = { "settings.action_jump", "settings.action_sprint", "settings.action_interact" };
        private static readonly string[] KeyboardMousePaths = { "<Keyboard>", "<Mouse>" };
        private static readonly string[] GamepadPaths = { "<Gamepad>" };

        private readonly IInputBindings _bindings;
        private readonly Action _onClick;
        private readonly VisualElement _list;
        private InputActionRebindingExtensions.RebindingOperation _operation;
        private int _endedFrame = -10;

        public event Action BindingsChanged;

        // True while waiting for input and for one frame after, so the Escape that cancels is not also treated as Back.
        public bool IsRebinding => _operation != null || Time.frameCount <= _endedFrame + 1;

        public RebindPresenter(VisualElement root, IInputBindings bindings, Action onClick)
        {
            _bindings = bindings;
            _onClick = onClick;
            _list = UiQuery.Req<VisualElement>(root, "RebindList");
            var reset = UiQuery.Req<Button>(root, "RebindReset");
            reset.text = UiStrings.Get("settings.rebind_reset");
            reset.clicked += ResetAll;
        }

        public void Refresh()
        {
            _list.Clear();
            var asset = _bindings.Actions;
            if (asset == null) return;
            for (var i = 0; i < ActionPaths.Length; i++)
            {
                var action = asset.FindAction(ActionPaths[i], false);
                if (action == null) continue;
                AddButton(action, ActionLabelKeys[i], InputActionNames.KeyboardMouseScheme, "settings.scheme_kbm", KeyboardMousePaths);
                AddButton(action, ActionLabelKeys[i], InputActionNames.GamepadScheme, "settings.scheme_pad", GamepadPaths);
            }
        }

        // Cancels a running rebind (screen closed). Safe to call when none is running.
        public void CancelActive()
        {
            var op = _operation;
            if (op == null) return;
            if (op.started) op.Cancel();           // raises OnCancel -> Finish
            else if (_operation == op) Finish(null, false, false);
        }

        // Teardown without touching the UI.
        public void Dispose()
        {
            _operation?.Dispose();
            _operation = null;
        }

        private void AddButton(InputAction action, string labelKey, string scheme, string schemeKey, string[] devicePaths)
        {
            var index = action.GetBindingIndex(group: scheme);
            if (index < 0) return;
            var button = new Button();
            button.AddToClassList("menu-button");
            button.AddToClassList("rebind-button");
            button.text = UiStrings.Get(labelKey) + " (" + UiStrings.Get(schemeKey) + "): " + action.GetBindingDisplayString(index);
            button.SetEnabled(_operation == null);
            button.clicked += () =>
            {
                _onClick?.Invoke();
                BeginRebind(action, index, devicePaths, button);
            };
            _list.Add(button);
        }

        private void BeginRebind(InputAction action, int bindingIndex, string[] devicePaths, Button button)
        {
            if (_operation != null) return;
            var wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();   // WithAction throws while the action is enabled (Player map is normally off in Menu).

            button.text = UiStrings.Get("settings.rebind_wait");
            foreach (var child in _list.Children()) child.SetEnabled(child == button);

            var op = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(TimeoutSeconds);
            foreach (var path in devicePaths) op.WithControlsHavingToMatchPath(path);
            op.OnComplete(_ => Finish(action, wasEnabled, true))
              .OnCancel(_ => Finish(action, wasEnabled, false));
            _operation = op;

            // Start slightly later so the Submit/click that opened the rebind is not captured as the new binding.
            _list.schedule.Execute(() =>
            {
                if (_operation == op) op.Start();
            }).StartingIn(StartDelayMs);
        }

        private void Finish(InputAction action, bool wasEnabled, bool completed)
        {
            var op = _operation;
            _operation = null;
            _endedFrame = Time.frameCount;
            op?.Dispose();
            if (action != null && wasEnabled) action.Enable();
            if (completed)
            {
                _bindings.SaveBindingOverrides();
                BindingsChanged?.Invoke();
            }
            Refresh();
        }

        private void ResetAll()
        {
            if (_operation != null) return;
            _bindings.ResetAllBindingOverrides();
            BindingsChanged?.Invoke();
            Refresh();
        }
    }
}
