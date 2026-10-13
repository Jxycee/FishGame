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
        private readonly List<Action> _graphicsRefreshers = new List<Action>();
        private List<(int Width, int Height)> _resolutions = new List<(int Width, int Height)> { (0, 0) };
        private ScrollView _graphicsScroll;
        private Button _tabGeneral, _tabGraphics;
        private SliderInt _frameCap, _resolution, _qualityPreset;
        private bool _graphicsShown;
        private VisualElement _firstControl;

        private static readonly string[] WindowKeys = { "settings.win_fullscreen", "settings.win_borderless", "settings.win_windowed" };
        private static readonly string[] PresetKeys = { "settings.preset_low", "settings.preset_medium", "settings.preset_high", "settings.preset_custom" };
        private static readonly string[] ShadowKeys = { "settings.shadow_off", "settings.shadow_low", "settings.shadow_high" };
        private static readonly string[] AaKeys = { "settings.aa_off", "settings.aa_fxaa", "settings.aa_msaa2", "settings.aa_msaa4" };

        public event Action BackRequested;

        public SettingsPresenter(VisualElement root, ISettings settings)
        {
            _settings = settings;
            UiQuery.Req<Label>(root, "SettingsTitle").text = UiStrings.Get("settings.title");
            UiQuery.Req<Label>(root, "SectionDisplay").text = UiStrings.Get("settings.section_display");
            UiQuery.Req<Label>(root, "SectionAudio").text = UiStrings.Get("settings.section_audio");
            UiQuery.Req<Label>(root, "SectionGameplay").text = UiStrings.Get("settings.section_gameplay");
            UiQuery.Req<Label>(root, "SectionControls").text = UiStrings.Get("settings.section_controls");
            UiQuery.Req<Label>(root, "RebindHint").text = UiStrings.Get("settings.rebind_hint");

            _scroll = UiQuery.Req<ScrollView>(root, "SettingsScroll");
            _scroll.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _scroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;   // the viewport shrinks at 150% UI scale: the scroller must always be there

            // First row of the list (a Display row above Audio and Gameplay): at 150% the panel viewport shrinks live, and a row at the
            // end of the list fell below it with nothing to scroll to. After a change the slider is scrolled back into view once layout has run.
            BindInt(root, "UiScale", "settings.ui_scale", 80, 150, d => d.uiScalePercent, (d, v) => d.uiScalePercent = v);
            var uiScale = UiQuery.Req<SliderInt>(root, "UiScale");
            uiScale.RegisterValueChangedCallback(_ => _scroll.schedule.Execute(() => _scroll.ScrollTo(uiScale)).ExecuteLater(50));

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

            BindGraphicsTab(root);

            var back = UiQuery.Req<Button>(root, "SettingsBack");
            back.text = UiStrings.Get("settings.back");
            back.clicked += () => BackRequested?.Invoke();
        }

        public void Load()
        {
            foreach (var refresh in _refreshers) refresh();
            RebuildResolutions();
            RefreshGraphics();
            ShowTab(false);
        }

        // Graphics tab (playtest S6/S3). Two tab buttons switch between the General page (the original scroll list) and the Graphics page.
        // Everything is a SliderInt choice or a Toggle, like the existing rows, so keyboard and gamepad navigation need nothing special: a hidden
        // page is display:none and takes no focus. Rows write ISettings.Current + NotifyChanged (values apply live through GraphicsApplier);
        // SettingsData.Clamp turns a preset into Custom when a row stops matching it. Refreshing after a change uses SetValueWithoutNotify only,
        // so a preset change never re-enters the row callbacks.
        private void BindGraphicsTab(VisualElement root)
        {
            _tabGeneral = UiQuery.Req<Button>(root, "TabGeneral");
            _tabGraphics = UiQuery.Req<Button>(root, "TabGraphics");
            _tabGeneral.text = UiStrings.Get("settings.tab_general");
            _tabGraphics.text = UiStrings.Get("settings.tab_graphics");
            _tabGeneral.clicked += () => ShowTab(false);
            _tabGraphics.clicked += () => ShowTab(true);

            UiQuery.Req<Label>(root, "SectionScreen").text = UiStrings.Get("settings.section_screen");
            UiQuery.Req<Label>(root, "SectionQuality").text = UiStrings.Get("settings.section_quality");
            UiQuery.Req<Label>(root, "GraphicsHint").text = UiStrings.Get("settings.gfx_hint");

            _graphicsScroll = UiQuery.Req<ScrollView>(root, "GraphicsScroll");
            _graphicsScroll.RegisterCallback<FocusInEvent>(OnGraphicsFocusIn, TrickleDown.TrickleDown);
            _graphicsScroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            BindGfxChoice(root, "WindowMode", "settings.gfx_window", 3, i => UiStrings.Get(WindowKeys[i]),
                d => d.windowMode, (d, v) => d.windowMode = v);

            _resolution = BindGfxChoice(root, "Resolution", "settings.gfx_res", 1, ResolutionText,
                d => GraphicsPresets.ResolutionIndex(_resolutions, d.resolutionWidth, d.resolutionHeight),
                (d, v) =>
                {
                    var r = _resolutions[Math.Max(0, Math.Min(_resolutions.Count - 1, v))];
                    d.resolutionWidth = r.Width;
                    d.resolutionHeight = r.Height;
                });

            var vsync = UiQuery.Req<Toggle>(root, "VSync");
            vsync.label = UiStrings.Get("settings.gfx_vsync");
            vsync.RegisterValueChangedCallback(e =>
            {
                _settings.Current.vsync = e.newValue;
                _settings.NotifyChanged();
                RefreshGraphics();
            });
            _graphicsRefreshers.Add(() => vsync.SetValueWithoutNotify(_settings.Current.vsync));

            _frameCap = BindGfxChoice(root, "FrameCap", "settings.gfx_fps", GraphicsPresets.FrameCaps.Length,
                i => GraphicsPresets.FrameCaps[i] == 0
                    ? UiStrings.Get("settings.fps_unlimited")
                    : UiStrings.Format("settings.fps_value", GraphicsPresets.FrameCaps[i].ToString(CultureInfo.InvariantCulture)),
                d => d.frameRateCap, (d, v) => d.frameRateCap = v);
            _graphicsRefreshers.Add(() => _frameCap.SetEnabled(!_settings.Current.vsync));   // the cap only applies with VSync off

            // The preset: picking Low / Medium / High writes its three rows; Custom changes no row.
            _qualityPreset = BindGfxChoice(root, "QualityPreset", "settings.gfx_preset", PresetKeys.Length, i => UiStrings.Get(PresetKeys[i]),
                d => d.graphicsPreset,
                (d, v) =>
                {
                    d.graphicsPreset = v;
                    if (GraphicsPresets.TryGet(v, out GraphicsPresetValues preset))
                    {
                        d.renderScalePercent = preset.RenderScalePercent;
                        d.shadowQuality = preset.Shadows;
                        d.antiAliasing = preset.Aa;
                    }
                });

            BindGfxChoice(root, "RenderScale", "settings.gfx_scale", GraphicsPresets.RenderScaleCount,
                i => UiStrings.Format("settings.scale_value", GraphicsPresets.RenderScaleFromIndex(i).ToString(CultureInfo.InvariantCulture)),
                d => GraphicsPresets.RenderScaleToIndex(d.renderScalePercent),
                (d, v) => d.renderScalePercent = GraphicsPresets.RenderScaleFromIndex(v));
            BindGfxChoice(root, "ShadowQuality", "settings.gfx_shadows", ShadowKeys.Length, i => UiStrings.Get(ShadowKeys[i]),
                d => d.shadowQuality, (d, v) => d.shadowQuality = v);
            BindGfxChoice(root, "AntiAliasing", "settings.gfx_aa", AaKeys.Length, i => UiStrings.Get(AaKeys[i]),
                d => d.antiAliasing, (d, v) => d.antiAliasing = v);
        }

        private void ShowTab(bool graphics)
        {
            _graphicsShown = graphics;
            UiQuery.Show(_scroll, !graphics);
            UiQuery.Show(_graphicsScroll, graphics);
            _tabGeneral.EnableInClassList("active", !graphics);
            _tabGraphics.EnableInClassList("active", graphics);
        }

        private void RefreshGraphics()
        {
            foreach (var refresh in _graphicsRefreshers) refresh();
        }

        // The monitor's distinct modes plus the saved one; index 0 is native.
        private void RebuildResolutions()
        {
            var modes = new List<(int Width, int Height)>();
            foreach (var r in UnityEngine.Screen.resolutions) modes.Add((r.width, r.height));
            var data = _settings.Current;
            _resolutions = GraphicsPresets.BuildResolutionList(modes, data.resolutionWidth, data.resolutionHeight);
            _resolution.highValue = _resolutions.Count - 1;
        }

        private string ResolutionText(int index)
        {
            if (index <= 0 || index >= _resolutions.Count) return UiStrings.Get("settings.res_native");
            var r = _resolutions[index];
            return UiStrings.Format("settings.res_value", r.Width.ToString(CultureInfo.InvariantCulture), r.Height.ToString(CultureInfo.InvariantCulture));
        }

        // A SliderInt over `count` named values (index 0..count-1), for the Graphics rows. Returns the slider.
        private SliderInt BindGfxChoice(VisualElement root, string name, string labelKey, int count, Func<int, string> format,
            Func<SettingsData, int> getIndex, Action<SettingsData, int> setIndex)
        {
            var slider = UiQuery.Req<SliderInt>(root, name);
            var label = UiStrings.Get(labelKey);
            slider.lowValue = 0;
            slider.highValue = Math.Max(0, count - 1);
            slider.RegisterValueChangedCallback(e =>
            {
                setIndex(_settings.Current, e.newValue);
                _settings.NotifyChanged();
                RefreshGraphics();
            });
            _graphicsRefreshers.Add(() =>
            {
                var value = Math.Max(0, Math.Min(slider.highValue, getIndex(_settings.Current)));
                slider.SetValueWithoutNotify(value);
                slider.label = label + "  " + format(value);
            });
            return slider;
        }

        private void OnGraphicsFocusIn(FocusInEvent evt)
        {
            if (evt.target is VisualElement element && element != _graphicsScroll && _graphicsScroll.contentContainer.Contains(element))
                _graphicsScroll.ScrollTo(element);
        }

        // Focus the first row of the page that is showing (a lost gamepad focus lands here via UiFocusRouter.RefocusIfLost; the General page's
        // first row is hidden while the Graphics page shows, and a hidden control takes no focus).
        public void FocusDefault()
        {
            if (_graphicsShown && _qualityPreset != null) _qualityPreset.Focus();
            else _firstControl?.Focus();
        }

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
