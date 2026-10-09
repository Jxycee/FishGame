using System;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only view logic for the Pause menu (spec 11.3). The join code row is shown online only. Streamer mode masks it;
    // a pointer hold reveals it, and a keyboard/gamepad submit on Reveal shows it for 5 s (UI Toolkit has no submit release).
    internal sealed class PausePresenter
    {
        private const float SubmitRevealSeconds = 5f;
        private readonly ISettings _settings;
        private readonly Button _resume;
        private readonly Button _leave;
        private readonly Button _joins;
        private readonly VisualElement _codeRow;
        private readonly Label _codeText;
        private string _code = string.Empty;
        private bool _showCode;
        private bool _holding;
        private float _revealUntil;

        public event Action ResumeRequested;
        public event Action SettingsRequested;
        public event Action LeaveRequested;
        public event Action JoinsToggleRequested;
        public event Action Copied;

        public PausePresenter(VisualElement root, ISettings settings)
        {
            _settings = settings;
            UiQuery.Req<Label>(root, "PauseTitle").text = UiStrings.Get("pause.title");
            UiQuery.Req<Label>(root, "CodeLabel").text = UiStrings.Get("pause.code_label");
            _codeRow = UiQuery.Req<VisualElement>(root, "CodeRow");
            _codeText = UiQuery.Req<Label>(root, "CodeText");

            _resume = UiQuery.Req<Button>(root, "ResumeButton");
            _resume.text = UiStrings.Get("pause.resume");
            _resume.clicked += () => ResumeRequested?.Invoke();

            var settingsButton = UiQuery.Req<Button>(root, "PauseSettingsButton");
            settingsButton.text = UiStrings.Get("pause.settings");
            settingsButton.clicked += () => SettingsRequested?.Invoke();

            _leave = UiQuery.Req<Button>(root, "LeaveButton");
            _leave.text = UiStrings.Get("pause.leave");
            _leave.clicked += () => LeaveRequested?.Invoke();

            var copy = UiQuery.Req<Button>(root, "CopyButton");
            copy.text = UiStrings.Get("pause.copy");
            copy.clicked += () =>
            {
                if (_code.Length == 0) return;
                GUIUtility.systemCopyBuffer = _code;   // exactly as session.Code, never the mask
                Copied?.Invoke();
            };

            var reveal = UiQuery.Req<Button>(root, "RevealButton");
            reveal.text = UiStrings.Get("pause.reveal");
            reveal.RegisterCallback<PointerDownEvent>(_ => _holding = true, TrickleDown.TrickleDown);
            reveal.RegisterCallback<PointerUpEvent>(_ => _holding = false, TrickleDown.TrickleDown);
            reveal.RegisterCallback<PointerCaptureOutEvent>(_ => _holding = false);
            reveal.RegisterCallback<PointerLeaveEvent>(_ => _holding = false);
            reveal.RegisterCallback<NavigationSubmitEvent>(_ => _revealUntil = Time.unscaledTime + SubmitRevealSeconds, TrickleDown.TrickleDown);

            _joins = UiQuery.Req<Button>(root, "JoinsButton");
            _joins.clicked += () => JoinsToggleRequested?.Invoke();
        }

        public void Show(bool showCode, string code, bool showJoinsToggle, bool joinsOpen)
        {
            _showCode = showCode;
            _code = code ?? string.Empty;
            _holding = false;
            _revealUntil = 0f;
            UiQuery.Show(_codeRow, showCode);
            UiQuery.Show(_joins, showJoinsToggle);
            SetJoinsOpen(joinsOpen);
            Tick();
        }

        public void SetJoinsOpen(bool open) => _joins.text = UiStrings.Get(open ? "pause.joins_close" : "pause.joins_open");

        public void SetLeaveEnabled(bool enabled) => _leave.SetEnabled(enabled);

        public void Tick()
        {
            if (!_showCode) return;
            var reveal = !_settings.Current.streamerMode || _holding || Time.unscaledTime < _revealUntil;
            var text = reveal ? _code : UiStrings.Get("pause.masked");
            if (!string.Equals(_codeText.text, text, StringComparison.Ordinal)) _codeText.text = text;
        }

        public void FocusDefault() => _resume.Focus();
    }
}
