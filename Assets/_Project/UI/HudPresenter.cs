using System;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only. HUD placeholder (spec 11.4): static crosshair (UXML), interact prompt "[key] text" at the centre,
    // and the bottom-left prompt strip. Key names come from IInputService for the last-used device.
    internal sealed class HudPresenter : IDisposable
    {
        private readonly Label _prompt;
        private readonly Label _strip;
        private readonly IInputService _input;
        private readonly IDisposable _subscription;
        private InputDeviceKind _device;
        private string _promptText;
        private bool _promptCanInteract = true;

        public HudPresenter(VisualElement root, EventBus bus, IInputService input)
        {
            _input = input;
            _prompt = UiQuery.Req<Label>(root, "InteractPrompt");
            _strip = UiQuery.Req<Label>(root, "PromptStrip");
            _device = input.LastDevice;
            UiQuery.Show(_prompt, false);
            _subscription = bus.Subscribe<InteractPromptChanged>(OnPrompt);
        }

        public void Refresh()
        {
            _device = _input.LastDevice;
            _strip.text = UiStrings.Format("hud.strip_interact", KeyFor(InputActionNames.Interact))
                + "    " + UiStrings.Format("hud.strip_pause", KeyFor(InputActionNames.Pause));
            ApplyPrompt();
        }

        // Called every frame while the HUD is visible: re-labels prompts when the player switches device.
        public void Tick()
        {
            if (_input.LastDevice != _device) Refresh();
        }

        public void Dispose() => _subscription.Dispose();

        private void OnPrompt(InteractPromptChanged message)
        {
            _promptText = message.Prompt;
            _promptCanInteract = message.CanInteract;
            ApplyPrompt();
        }

        private void ApplyPrompt()
        {
            var show = !string.IsNullOrEmpty(_promptText);
            _prompt.text = show ? UiStrings.Format("hud.key", KeyFor(InputActionNames.Interact)) + " " + _promptText : string.Empty;
            UiQuery.Show(_prompt, show);
            _prompt.EnableInClassList("dim", !_promptCanInteract);
        }

        private string KeyFor(string actionPath)
        {
            var text = _input.GetBindingDisplayString(actionPath);
            return string.IsNullOrEmpty(text) ? "?" : text;
        }
    }
}
