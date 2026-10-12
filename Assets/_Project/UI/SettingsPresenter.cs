using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only view logic for the M1 settings (spec 11.6). Controls edit ISettings.Current and call NotifyChanged
    // (values apply live). UiFlowController saves when the screen is left; nothing here saves per tick.
    internal sealed class SettingsPresenter
    {
        private readonly ISettings _settings;
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly ScrollView _scroll;
        private readonly TextField _name;
        private VisualElement _firstControl;

        public event Action BackRequested;

        public SettingsPresenter(VisualElement root, ISettings settings)
        {
            _settings = settings;
            UiQuery.Req<Label>(root, "SettingsTitle").text = UiStrings.Get("settings.title");
            UiQuery.Req<Label>(root, "SectionAudio").text = UiStrings.Get("settings.section_audio");
            UiQuery.Req<Label>(root, "SectionGameplay").text = UiStrings.Get("settings.section_gameplay");
            UiQuery.Req<Label>(root, "SectionControls").text = UiStrings.Get("settings.section_controls");
            UiQuery.Req<Label>(root, "RebindHint").text = UiStrings.Get("settings.rebind_hint");

            _scroll = UiQuery.Req<ScrollView>(root, "SettingsScroll");
            _scroll.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);

            BindInt(root, "MasterVolume", "settings.master", 0, 100, d => d.masterVolume, (d, v) => d.masterVolume = v);
            BindInt(root, "EffectsVolume", "settings.sfx", 0, 100, d => d.sfxVolume, (d, v) => d.sfxVolume = v);
            BindInt(root, "AmbienceVolume", "settings.ambience", 0, 100, d => d.ambienceVolume, (d, v) => d.ambienceVolume = v);
            BindInt(root, "InterfaceVolume", "settings.ui", 0, 100, d => d.uiVolume, (d, v) => d.uiVolume = v);

            _name = UiQuery.Req<TextField>(root, "SettingsName");
            _name.label = UiStrings.Get("settings.name");
            _name.maxLength = DisplayNameRules.MaxLength;
            _name.isDelayed = true;
            _name.RegisterValueChangedCallback(_ => Commit());
            _refreshers.Add(() => _name.SetValueWithoutNotify(_settings.Current.displayName));

            BindFloat(root, "MouseSensitivity", "settings.mouse", 0.04f, 0.30f, d => d.mouseSensitivity, (d, v) => d.mouseSensitivity = v);
            BindFloat(root, "GamepadLookSpeed", "settings.pad_look", 0.2f, 3.0f, d => d.gamepadLookSpeed, (d, v) => d.gamepadLookSpeed = v);
            BindBool(root, "InvertY", "settings.invert_y", d => d.invertY, (d, v) => d.invertY = v);
            BindInt(root, "FieldOfView", "settings.fov", 80, 110, d => (int)Math.Round(d.fieldOfView), (d, v) => d.fieldOfView = v);
            BindFloat(root, "CameraMotion", "settings.cam_motion", 0f, 1f, d => d.cameraMotion, (d, v) => d.cameraMotion = v);
            var refreshGauge = BindChoice(root, "LineGauge", "settings.line_gauge",
                new[] { "settings.gauge_off", "settings.gauge_arc", "settings.gauge_bar" }, d => d.lineGauge, (d, v) => d.lineGauge = v);
            BindBool(root, "Captions", "settings.captions", d => d.captions, (d, v) => d.captions = v);
            BindAssist(root, refreshGauge);
            BindBool(root, "SprintToggle", "settings.sprint_toggle", d => d.sprintToggle, (d, v) => d.sprintToggle = v);
            BindBool(root, "AimToggle", "settings.aim_toggle", d => d.aimToggle, (d, v) => d.aimToggle = v);
            BindBool(root, "StreamerMode", "settings.streamer", d => d.streamerMode, (d, v) => d.streamerMode = v);
            BindInt(root, "UiScale", "settings.ui_scale", 80, 150, d => d.uiScalePercent, (d, v) => d.uiScalePercent = v);

            var back = UiQuery.Req<Button>(root, "SettingsBack");
            back.text = UiStrings.Get("settings.back");
            back.clicked += () => BackRequested?.Invoke();
        }

        public void Load()
        {
            foreach (var refresh in _refreshers) refresh();
        }

        public void FocusDefault() => _firstControl?.Focus();

        // Commits a typed but not yet committed display name into Current (no save; the caller saves).
        public void Commit()
        {
            var typed = _name.text ?? string.Empty;
            if (!string.Equals(typed, _settings.Current.displayName, StringComparison.Ordinal))
            {
                _settings.Current.displayName = typed;
                _settings.NotifyChanged();
            }
            _name.SetValueWithoutNotify(_settings.Current.displayName);
        }

        // Keeps the focused control visible while navigating with keyboard or gamepad (criterion 11).
        private void OnFocusIn(FocusInEvent evt)
        {
            if (evt.target is VisualElement element && element != _scroll && _scroll.contentContainer.Contains(element))
                _scroll.ScrollTo(element);
        }

        private void BindInt(VisualElement root, string name, string labelKey, int low, int high,
            Func<SettingsData, int> get, Action<SettingsData, int> set)
        {
            var slider = UiQuery.Req<SliderInt>(root, name);
            var label = UiStrings.Get(labelKey);
            slider.lowValue = low;
            slider.highValue = high;
            slider.RegisterValueChangedCallback(e =>
            {
                set(_settings.Current, e.newValue);
                _settings.NotifyChanged();
                slider.label = label + "  " + e.newValue.ToString(CultureInfo.InvariantCulture);
            });
            _refreshers.Add(() =>
            {
                var value = get(_settings.Current);
                slider.SetValueWithoutNotify(value);
                slider.label = label + "  " + value.ToString(CultureInfo.InvariantCulture);
            });
            _firstControl ??= slider;
        }

        // A SliderInt over a short list of named values (no DropdownField: its popup is unreliable with gamepad-only navigation).
        // Returns the refresh action so a related control can update this slider.
        private Action BindChoice(VisualElement root, string name, string labelKey, string[] valueKeys,
            Func<SettingsData, int> get, Action<SettingsData, int> set)
        {
            var slider = UiQuery.Req<SliderInt>(root, name);
            var label = UiStrings.Get(labelKey);
            int last = valueKeys.Length - 1;
            slider.lowValue = 0;
            slider.highValue = last;
            slider.RegisterValueChangedCallback(e =>
            {
                set(_settings.Current, e.newValue);
                _settings.NotifyChanged();
                slider.label = label + "  " + UiStrings.Get(valueKeys[Math.Max(0, Math.Min(last, e.newValue))]);
            });
            Action refresh = () =>
            {
                var value = Math.Max(0, Math.Min(last, get(_settings.Current)));
                slider.SetValueWithoutNotify(value);
                slider.label = label + "  " + UiStrings.Get(valueKeys[value]);
            };
            _refreshers.Add(refresh);
            return refresh;
        }

        // Fishing assist (ruling G-Q4): every false-to-true change with the gauge Off sets the gauge to Arc (FishingHudRules.OnAssistChanged).
        private void BindAssist(VisualElement root, Action refreshGauge)
        {
            var toggle = UiQuery.Req<Toggle>(root, "FishingAssist");
            toggle.label = UiStrings.Get("settings.assist");
            toggle.RegisterValueChangedCallback(e =>
            {
                var data = _settings.Current;
                bool gaugeChanged = FishingHudRules.OnAssistChanged(data, e.previousValue, e.newValue);
                data.fishingAssist = e.newValue;
                _settings.NotifyChanged();
                if (gaugeChanged) refreshGauge();
            });
            _refreshers.Add(() => toggle.SetValueWithoutNotify(_settings.Current.fishingAssist));
        }

        private void BindFloat(VisualElement root, string name, string labelKey, float low, float high,
            Func<SettingsData, float> get, Action<SettingsData, float> set)
        {
            var slider = UiQuery.Req<Slider>(root, name);
            var label = UiStrings.Get(labelKey);
            slider.lowValue = low;
            slider.highValue = high;
            slider.RegisterValueChangedCallback(e =>
            {
                set(_settings.Current, e.newValue);
                _settings.NotifyChanged();
                slider.label = label + "  " + e.newValue.ToString("0.00", CultureInfo.InvariantCulture);
            });
            _refreshers.Add(() =>
            {
                var value = get(_settings.Current);
                slider.SetValueWithoutNotify(value);
                slider.label = label + "  " + value.ToString("0.00", CultureInfo.InvariantCulture);
            });
            _firstControl ??= slider;
        }

        private void BindBool(VisualElement root, string name, string labelKey,
            Func<SettingsData, bool> get, Action<SettingsData, bool> set)
        {
            var toggle = UiQuery.Req<Toggle>(root, name);
            toggle.label = UiStrings.Get(labelKey);
            toggle.RegisterValueChangedCallback(e =>
            {
                set(_settings.Current, e.newValue);
                _settings.NotifyChanged();
            });
            _refreshers.Add(() => toggle.SetValueWithoutNotify(get(_settings.Current)));
            _firstControl ??= toggle;
        }
    }
}
