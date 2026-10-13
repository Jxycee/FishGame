using System;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only view logic for the main menu (spec 11.3). Raises intents; UiFlowController runs the session calls.
    internal sealed class MainMenuPresenter
    {
        private readonly ISettings _settings;
        private readonly Button _host;
        private readonly Button _join;
        private readonly Button _solo;
        private readonly Button _devHost;
        private readonly Button _devJoin;
        private readonly Button _journalButton;
        private readonly Label _metaSummary;
        private readonly TextField _codeField;
        private readonly TextField _nameField;
        private bool _actionsEnabled = true;

        public event Action HostRequested;
        public event Action<string> JoinRequested;
        public event Action SoloRequested;
        public event Action SettingsRequested;
        public event Action JournalRequested;
        public event Action QuitRequested;
        public event Action DevHostRequested;
        public event Action DevJoinRequested;

        public MainMenuPresenter(VisualElement root, ISettings settings)
        {
            _settings = settings;
            UiQuery.Req<Label>(root, "MenuTitle").text = UiStrings.Get("menu.title");
            UiQuery.Req<Label>(root, "JoinCodeLabel").text = UiStrings.Get("menu.join_code");
            _host = UiQuery.Req<Button>(root, "HostButton");
            _join = UiQuery.Req<Button>(root, "JoinButton");
            _solo = UiQuery.Req<Button>(root, "SoloButton");
            var settingsButton = UiQuery.Req<Button>(root, "SettingsButton");
            _journalButton = UiQuery.Req<Button>(root, "JournalButton");
            _metaSummary = UiQuery.Req<Label>(root, "MetaSummary");
            var quit = UiQuery.Req<Button>(root, "QuitButton");
            _devHost = UiQuery.Req<Button>(root, "DevHostButton");
            _devJoin = UiQuery.Req<Button>(root, "DevJoinButton");
            _codeField = UiQuery.Req<TextField>(root, "JoinCodeField");
            _nameField = UiQuery.Req<TextField>(root, "NameField");
            var devRow = UiQuery.Req<VisualElement>(root, "DevRow");

            _host.text = UiStrings.Get("menu.host");
            _join.text = UiStrings.Get("menu.join");
            _solo.text = UiStrings.Get("menu.solo");
            settingsButton.text = UiStrings.Get("menu.settings");
            _journalButton.text = UiStrings.Get("menu.journal");
            quit.text = UiStrings.Get("menu.quit");
            _devHost.text = UiStrings.Get("menu.dev_host");
            _devJoin.text = UiStrings.Get("menu.dev_join");

            _codeField.maxLength = JoinCode.MaxLength;
            _nameField.label = UiStrings.Get("menu.name");
            _nameField.maxLength = DisplayNameRules.MaxLength;
            _nameField.isDelayed = true;

            _host.clicked += () => HostRequested?.Invoke();
            _join.clicked += () => JoinRequested?.Invoke(JoinCode.Normalize(_codeField.text));
            _solo.clicked += () => SoloRequested?.Invoke();
            settingsButton.clicked += () => SettingsRequested?.Invoke();
            _journalButton.clicked += () => JournalRequested?.Invoke();
            quit.clicked += () => QuitRequested?.Invoke();
            _devHost.clicked += () => DevHostRequested?.Invoke();
            _devJoin.clicked += () => DevJoinRequested?.Invoke();

            _codeField.RegisterValueChangedCallback(_ => UpdateButtons());
            _nameField.RegisterValueChangedCallback(_ => CommitName());

            UiQuery.Show(devRow, UiQuery.IsDevBuild);
            Refresh();
        }

        public void Refresh()
        {
            _nameField.SetValueWithoutNotify(_settings.Current.displayName);
            UpdateButtons();
        }

        // "Journal n/25" under the title (spec 11.3); empty text hides the line. The Journal button reads only the local save, so it is
        // never disabled by a pending session operation (UpdateButtons leaves it alone).
        public void SetMetaSummary(string text)
        {
            var has = !string.IsNullOrEmpty(text);
            _metaSummary.text = has ? text : string.Empty;
            UiQuery.Show(_metaSummary, has);
        }

        public bool FocusJournal() => TryFocus(_journalButton);

        // Host, Join and Solo (and the dev buttons) are disabled while an operation is pending (spec 11.3).
        public void SetActionsEnabled(bool enabled)
        {
            _actionsEnabled = enabled;
            UpdateButtons();
        }

        // Each returns false (and does nothing) when the target cannot take focus yet, e.g. a disabled button.
        public bool FocusDefault() => TryFocus(_host);
        public bool FocusSolo() => TryFocus(_solo);
        public bool FocusJoinCode() => TryFocus(_codeField);

        private static bool TryFocus(VisualElement element)
        {
            if (!element.enabledInHierarchy) return false;
            element.Focus();
            return true;
        }

        // Commits the typed name (isDelayed fields only commit on Enter or focus loss) and saves it (spec 11.3).
        public void CommitName()
        {
            var typed = _nameField.text ?? string.Empty;
            if (!string.Equals(typed, _settings.Current.displayName, StringComparison.Ordinal))
            {
                _settings.Current.displayName = typed;
                _settings.NotifyChanged();
                _settings.Save();
            }
            _nameField.SetValueWithoutNotify(_settings.Current.displayName);
        }

        private void UpdateButtons()
        {
            _host.SetEnabled(_actionsEnabled);
            _solo.SetEnabled(_actionsEnabled);
            _devHost.SetEnabled(_actionsEnabled);
            _devJoin.SetEnabled(_actionsEnabled);
            _join.SetEnabled(_actionsEnabled && JoinCode.Normalize(_codeField.text).Length > 0);
        }
    }
}
