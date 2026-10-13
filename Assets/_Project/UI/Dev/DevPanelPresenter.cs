using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: the HOST edits, through direct host calls (M3-D51, C 7.22): DevWorldDriver.Dev* (season, minute, weather override, clock, new
    // day, hotspot, F8 scrub), FishingHost.SetRunSeed and FishingDebugSettings.Luck. No RPC is added and no client ever writes: every
    // call sits behind the host check (NetworkManager.IsServer). The one RPC is the existing owner RPC FishingRodNetwork.SelectRodRpc on the
    // host's OWN rod (rod tier). Clients see the replicated season, minute, weather and hotspot read-only; the rows are disabled.
    // Dev builds only (Debug.isDebugBuild, checked at runtime; never #if): the hook, Bind and every key path check it.
    // F3 mode: DevOverlayPresenter.Mode (Off -> Overlay -> Panel -> Off). Entering Panel opens T21's HUD modal seam (cursor free, gameplay
    // input off); leaving it, Esc/B and a screen change close it. The journal and the panel exclude each other through the same seam.
    internal sealed class DevPanelPresenter : IHudExtension
    {
        private const float RefreshSeconds = 0.25f;          // 4 Hz, and only while the panel is shown
        private const float MinuteApplyDelay = 0.2f;         // one NetClock write per slider drag
        private const float LastActionSeconds = 3f;
        private const float RayLength = 100f;
        private const int RodTierCount = 4;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly WeatherMask[] WeatherChoices = { WeatherMask.None, WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };

        private readonly UiFlowController _flow;
        private readonly DevOverlayPresenter _overlay;
        private readonly LogOnce _log = new LogOnce();
        private readonly List<Action> _unhook = new List<Action>();
        private readonly List<Button> _seasonButtons = new List<Button>(4);
        private readonly List<Button> _weatherButtons = new List<Button>(4);
        private readonly List<Button> _rodButtons = new List<Button>(RodTierCount);
        private readonly List<KeyValuePair<Button, string>> _mapButtons = new List<KeyValuePair<Button, string>>(4);
        private readonly List<VisualElement> _worldRows = new List<VisualElement>(24);

        private bool _bound;
        private VisualElement _root, _panel, _mapRow;
        private Label _title, _minuteLabel, _luckLabel, _hotspotStatus, _seedStatus, _lastLabel;
        private SliderInt _minute;
        private Slider _luck;
        private Toggle _clockToggle;
        private TextField _seedField;
        private Button _seedApply;
        private ServiceRegistry _app;
        private IDevInput _dev;
        private IInputService _input;
        private ContentRegistry _registry;
        private EventBus _bus;
        private AudioBus _audio;

        // World lookups, cached per WorldState instance (a map switch replaces the world and its driver together).
        private WorldState _cachedWorld;
        private DevWorldDriver _driver;
        private bool _scrubSent;

        private float _refreshTimer;
        private float _lastActionTimer;
        private bool _minutePending;
        private float _minuteTimer;
        private int _minuteValue;
        private bool _seedFocused;
        private bool _seedDirty;               // the user typed in the seed field: a refresh never overwrites it (until applied, locked or reopened)
        private string _shownSeedText;         // what the refresh last wrote into the field; null forces the next refresh to write
        private bool _f3Armed;                // F3 closes the panel only after the F3 that opened it was released (no repeat closes it)
        private int _openedFrame = -1;
        private bool _rowsEnabled = true;
        private bool _rowsEnabledKnown;

        internal DevPanelPresenter(UiFlowController flow, DevOverlayPresenter overlay)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        internal bool PanelShown => _panel != null && !_panel.ClassListContains("hidden");

        // ---------------------------------------------------------------------------------------------------------- bind

        public void Bind(VisualElement root, ServiceRegistry app)
        {
            // Never throws: UiFlowController's bind loop has no try/catch (T2 review).
            try
            {
                if (_bound) Unbind();
                if (!Debug.isDebugBuild) return;   // belt and braces: the hook is guarded too
                BindCore(root, app);
            }
            catch (Exception exception)
            {
                Unbind();
                Debug.LogException(exception);
            }
        }

        private void BindCore(VisualElement root, ServiceRegistry app)
        {
            if (root == null || app == null) return;
            _root = root;
            _app = app;
            _panel = UiQuery.Req<VisualElement>(root, "DevPanel");
            _title = UiQuery.Req<Label>(root, "DevPanelTitle");
            _minuteLabel = UiQuery.Req<Label>(root, "DevMinuteLabel");
            _luckLabel = UiQuery.Req<Label>(root, "DevLuckLabel");
            _hotspotStatus = UiQuery.Req<Label>(root, "DevHotspotStatus");
            _seedStatus = UiQuery.Req<Label>(root, "DevSeedStatus");
            _lastLabel = UiQuery.Req<Label>(root, "DevLastAction");
            _minute = UiQuery.Req<SliderInt>(root, "DevMinute");
            _luck = UiQuery.Req<Slider>(root, "DevLuck");
            _clockToggle = UiQuery.Req<Toggle>(root, "DevClockToggle");
            _seedField = UiQuery.Req<TextField>(root, "DevSeedField");
            _seedApply = UiQuery.Req<Button>(root, "DevSeedApply");
            _mapRow = UiQuery.Req<VisualElement>(root, "DevMapRow");

            _input = app.TryGet(out IInputService input) ? input : null;
            _dev = _input as IDevInput;                   // may be null: the F8 scrub then does nothing
            _registry = app.TryGet(out ContentRegistry registry) ? registry : null;
            _bus = app.TryGet(out EventBus bus) ? bus : null;
            _audio = app.TryGet(out AudioBus audio) ? audio : null;

            UiQuery.Req<Label>(root, "DevSeasonLabel").text = UiStrings.Get("dev.season");
            UiQuery.Req<Label>(root, "DevWeatherLabel").text = UiStrings.Get("dev.weather");
            UiQuery.Req<Label>(root, "DevSeedLabel").text = UiStrings.Get("dev.run_seed");
            UiQuery.Req<Label>(root, "DevRodLabel").text = UiStrings.Get("dev.rod_tier");
            UiQuery.Req<Label>(root, "DevMapLabel").text = UiStrings.Get("dev.load_map");
            UiQuery.Req<Label>(root, "DevScrubHint").text = UiStrings.Get("dev.scrub_hint");
            _clockToggle.label = UiStrings.Get("dev.clock_running");
            _seedApply.text = UiStrings.Get("dev.apply_seed");

            // The root rows, in the order the disabled state is applied to them.
            _worldRows.Clear();
            _seasonButtons.Clear();
            _weatherButtons.Clear();
            _rodButtons.Clear();
            for (int i = 0; i < 4; i++)
            {
                Button season = UiQuery.Req<Button>(root, "DevSeason" + i.ToString(Inv));
                season.text = UiStrings.Get(ClockHudPresenter.SeasonKey((Season)i));
                int s = i;
                Hook(season, () => OnSeason((Season)s));
                _seasonButtons.Add(season);
                _worldRows.Add(season);

                Button weather = UiQuery.Req<Button>(root, "DevWeather" + i.ToString(Inv));
                weather.text = i == 0 ? UiStrings.Get("dev.weather_track")
                    : UiStrings.Get(ClockHudPresenter.WeatherKey(WorldGlyphs.Of(WeatherChoices[i])));
                int w = i;
                Hook(weather, () => OnWeather(WeatherChoices[w]));
                _weatherButtons.Add(weather);
                _worldRows.Add(weather);

                Button rod = UiQuery.Req<Button>(root, "DevRod" + i.ToString(Inv));
                rod.text = i.ToString(Inv);
                int t = i;
                Hook(rod, () => OnRodTier(t));
                _rodButtons.Add(rod);
                _worldRows.Add(rod);
            }

            Button newDay = UiQuery.Req<Button>(root, "DevNewDay");
            newDay.text = UiStrings.Get("dev.new_day");
            Hook(newDay, OnNewDay);
            Button dayOver = UiQuery.Req<Button>(root, "DevDayOver");
            dayOver.text = UiStrings.Get("dev.day_over");
            Hook(dayOver, OnDayOver);
            Button hotspot = UiQuery.Req<Button>(root, "DevHotspotButton");
            hotspot.text = UiStrings.Get("dev.spawn_hotspot");
            Hook(hotspot, OnHotspot);
            Button close = UiQuery.Req<Button>(root, "DevClose");
            close.text = UiStrings.Get("dev.close");
            Hook(close, OnClose);
            Hook(_seedApply, OnApplySeed);
            _worldRows.Add(newDay);
            _worldRows.Add(dayOver);
            _worldRows.Add(hotspot);
            _worldRows.Add(_minute);
            _worldRows.Add(_clockToggle);
            _worldRows.Add(_seedField);
            _worldRows.Add(_seedApply);
            _worldRows.Add(_luck);

            HookChange(_minute, OnMinuteChanged);
            HookChange(_luck, OnLuckChanged);
            HookChange(_clockToggle, OnClockToggle);
            HookChange(_seedField, OnSeedChanged);
            HookFocus(_seedField);
            // With nothing focused (a click on the panel background) key events target the panel's visualTree, above the document root.
            VisualElement keyTarget = root.panel != null ? root.panel.visualTree : root;
            Hook(keyTarget, KeyDown);
            Hook(keyTarget, KeyUp);
            BuildMapButtons();

            _overlay.ModeChanged += OnModeChanged;
            _unhook.Add(() => _overlay.ModeChanged -= OnModeChanged);

            _bound = true;
            _rowsEnabledKnown = false;
            _refreshTimer = RefreshSeconds;
            _lastLabel.text = string.Empty;
            _panel.EnableInClassList("hidden", true);
            if (_overlay.Mode == DevHudMode.Panel) OnModeChanged(DevHudMode.Panel);
        }

        public void Unbind()
        {
            // The flow is being destroyed (or this is a re-bind): the modal seam is not touched here, only this presenter's own state.
            ReleaseScrub();
            for (int i = _unhook.Count - 1; i >= 0; i--) _unhook[i]();
            _unhook.Clear();
            _mapButtons.Clear();
            if (_mapRow != null) _mapRow.Clear();
            if (_panel != null) _panel.EnableInClassList("hidden", true);
            _bound = false;
            _root = null;
            _panel = null;
            _title = null;
            _minuteLabel = null;
            _luckLabel = null;
            _hotspotStatus = null;
            _seedStatus = null;
            _lastLabel = null;
            _minute = null;
            _luck = null;
            _clockToggle = null;
            _seedField = null;
            _seedApply = null;
            _mapRow = null;
            _app = null;
            _dev = null;
            _input = null;
            _registry = null;
            _bus = null;
            _audio = null;
            _cachedWorld = null;
            _driver = null;
            _minutePending = false;
            _seedFocused = false;
            _seedDirty = false;
            _shownSeedText = null;
            _worldRows.Clear();
            _seasonButtons.Clear();
            _weatherButtons.Clear();
            _rodButtons.Clear();
        }

        private void Hook(Button button, Action action)
        {
            button.clicked += action;
            _unhook.Add(() => button.clicked -= action);
        }

        private void Hook(VisualElement target, EventCallback<KeyDownEvent> callback)
        {
            target.RegisterCallback(callback, TrickleDown.TrickleDown);
            _unhook.Add(() => target.UnregisterCallback(callback, TrickleDown.TrickleDown));
        }

        private void Hook(VisualElement target, EventCallback<KeyUpEvent> callback)
        {
            target.RegisterCallback(callback, TrickleDown.TrickleDown);
            _unhook.Add(() => target.UnregisterCallback(callback, TrickleDown.TrickleDown));
        }

        private void HookChange<T>(INotifyValueChanged<T> field, EventCallback<ChangeEvent<T>> callback)
        {
            field.RegisterValueChangedCallback(callback);
            _unhook.Add(() => field.UnregisterValueChangedCallback(callback));
        }

        private void HookFocus(VisualElement field)
        {
            EventCallback<FocusInEvent> focusIn = e => _seedFocused = true;
            EventCallback<FocusOutEvent> focusOut = e => _seedFocused = false;
            field.RegisterCallback(focusIn);
            field.RegisterCallback(focusOut);
            _unhook.Add(() =>
            {
                field.UnregisterCallback(focusIn);
                field.UnregisterCallback(focusOut);
            });
        }

        // One button per registry spot, MapOrder order, created at runtime (they play Ui_Click themselves: the UXML click loop ran earlier).
        private void BuildMapButtons()
        {
            _mapButtons.Clear();
            _mapRow.Clear();
            if (_registry == null) return;
            var spots = new List<SpotDef>(_registry.Spots);
            spots.Sort((a, b) => a.MapOrder != b.MapOrder ? a.MapOrder.CompareTo(b.MapOrder) : string.CompareOrdinal(a.Id, b.Id));
            for (int i = 0; i < spots.Count; i++)
            {
                SpotDef spot = spots[i];
                if (spot == null) continue;
                string spotId = spot.Id;
                var button = new Button { name = "DevMap_" + spotId, text = string.IsNullOrEmpty(spot.DisplayName) ? spotId : spot.DisplayName };
                button.AddToClassList("dev-btn");
                button.clicked += PlayClick;
                button.clicked += () => OnLoadMap(spotId);
                _mapRow.Add(button);
                _mapButtons.Add(new KeyValuePair<Button, string>(button, spotId));
            }
        }

        // ---------------------------------------------------------------------------------------------------------- tick

        // Never throws: UiFlowController's extension loop would skip the later extensions (an RPC during shutdown can throw here).
        public void Tick(float dt)
        {
            try
            {
                TickCore(dt);
            }
            catch (Exception exception)
            {
                if (_log.ShouldLog("tick:" + exception.GetType().Name)) Debug.LogException(exception);   // once per exception type
            }
        }

        private void TickCore(float dt)
        {
            if (!_bound) return;
            ResolveWorld();
            TickScrub();
            if (!Application.isFocused) _f3Armed = true;      // a KeyUp that arrived while the window was unfocused is lost: never leave F3 unarmed

            if (_lastActionTimer > 0f)
            {
                _lastActionTimer -= dt;
                if (_lastActionTimer <= 0f) _lastLabel.text = string.Empty;
            }

            if (_overlay.Mode != DevHudMode.Panel || !PanelShown) return;

            // Like the journal: a local bite or fight closes the panel (the owner is never left fighting behind a modal).
            if (JournalPresenter.LocalBiteOrHooked())
            {
                _overlay.SetMode(DevHudMode.Off);
                return;
            }

            if (_minutePending)
            {
                _minuteTimer -= dt;
                if (_minuteTimer <= 0f)
                {
                    _minutePending = false;
                    if (CanEdit()) _driver.DevSetMinute(_minuteValue);
                }
            }

            _refreshTimer += dt;
            if (_refreshTimer < RefreshSeconds) return;
            _refreshTimer = 0f;
            Refresh();
        }

        // Cached per WorldState instance. A new instance (map switch) means a new driver, which has never been told about the scrub.
        private void ResolveWorld()
        {
            WorldState world = WorldState.Current;
            if (ReferenceEquals(world, _cachedWorld)) return;
            _cachedWorld = world;
            _driver = world != null ? world.GetComponent<DevWorldDriver>() : null;
            _scrubSent = false;
            _minutePending = false;
        }

        // F8 held scrubs the host clock x60. IDevInput.IsHeld is false without focus and in the Menu context, so alt-tab or any open menu
        // (this panel included) releases it. DevScrub is sent only when the held state differs from the last value sent to this driver.
        private void TickScrub()
        {
            bool held = _dev != null && IsHost() && _driver != null && _dev.IsHeld(DevKey.ScrubClock);
            if (held == _scrubSent) return;
            if (_driver != null) _driver.DevScrub(held);
            _scrubSent = held;
        }

        private void ReleaseScrub()
        {
            if (!_scrubSent) return;
            _scrubSent = false;
            if (_driver != null && IsHost()) _driver.DevScrub(false);
        }

        // ---------------------------------------------------------------------------------------------------------- mode

        private void OnModeChanged(DevHudMode mode)
        {
            if (!_bound) return;
            if (mode == DevHudMode.Panel)
            {
                if (!_flow.TryOpenModal(this, OnModalClosed))
                {
                    _overlay.SetMode(DevHudMode.Overlay);     // refused (the journal is open)
                    return;
                }
                ShowPanel();
            }
            else
            {
                _flow.CloseModal(this);                         // no-op unless this panel holds the seam
                HidePanel();
            }
        }

        // Esc, B and a screen change come through the seam's close callback.
        private void OnModalClosed()
        {
            HidePanel();
            if (_overlay.Mode == DevHudMode.Panel) _overlay.SetMode(DevHudMode.Off);
        }

        private void ShowPanel()
        {
            _openedFrame = Time.frameCount;
            _f3Armed = false;
            _seedDirty = false;
            _shownSeedText = null;
            _refreshTimer = RefreshSeconds;
            _minutePending = false;
            _rowsEnabledKnown = false;
            _panel.EnableInClassList("hidden", false);
            ResolveWorld();
            Refresh();
            _panel.schedule.Execute(FocusFirst);
        }

        private void HidePanel()
        {
            _minutePending = false;
            if (_panel != null) _panel.EnableInClassList("hidden", true);
        }

        private void FocusFirst()
        {
            if (!_bound || !PanelShown) return;
            VisualElement target = _seasonButtons.Count > 0 && _seasonButtons[0].enabledSelf
                ? _seasonButtons[0]
                : _panel.Q<Button>("DevClose");
            target?.Focus();
        }

        // Called by UiFlowController while a modal is up: a gamepad user whose focus was lost lands back on the panel.
        internal void RecoverFocus()
        {
            if (!_bound || _overlay.Mode != DevHudMode.Panel || !PanelShown) return;
            if (_input == null || _input.LastDevice != InputDeviceKind.Gamepad) return;
            if (_panel.panel?.focusController?.focusedElement == null) FocusFirst();
        }

        private void OnClose() => _overlay.SetMode(DevHudMode.Off);

        // F3 while the panel is open (the Menu context blocks IDevInput, so the panel listens itself). It acts only after the F3 that opened it
        // was released (key repeat and the opening frame cannot close it) and on the first press, not a repeat.
        private void KeyDown(KeyDownEvent evt)
        {
            if (!_bound || evt.keyCode != KeyCode.F3 || _overlay.Mode != DevHudMode.Panel) return;
            if (Time.frameCount == _openedFrame || !_f3Armed) return;
            evt.StopPropagation();
            _f3Armed = false;
            _overlay.SetMode(DevHudMode.Off);
        }

        private void KeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode == KeyCode.F3) _f3Armed = true;
        }

        // ---------------------------------------------------------------------------------------------------------- host helpers

        private static bool IsHost()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }

        private bool HasWorld() => _cachedWorld != null && _cachedWorld.HasState;

        // Every world-changing call goes through this: this peer is the server, the world is up and the driver exists.
        private bool CanEdit() => IsHost() && _driver != null && HasWorld();

        private void SetLast(string text)
        {
            if (_lastLabel == null) return;
            _lastLabel.text = text ?? string.Empty;
            _lastActionTimer = LastActionSeconds;
        }

        private void PlayClick()
        {
            if (_audio != null) _audio.PlayUi(SfxIds.UiClick);
        }

        // ---------------------------------------------------------------------------------------------------------- actions (host only)

        private void OnSeason(Season season)
        {
            if (!CanEdit()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            _driver.DevSetSeason(season);
            _refreshTimer = RefreshSeconds;
        }

        private void OnWeather(WeatherMask choice)
        {
            if (!CanEdit()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            _driver.DevSetWeatherOverride(choice);
            _refreshTimer = RefreshSeconds;
        }

        private void OnNewDay()
        {
            if (!CanEdit()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            _driver.DevNewDay();
            _refreshTimer = RefreshSeconds;
        }

        // The slider stops at 1190 and a running clock that reaches 1200 starts a new day, so the closed-casting state needs this: stop, then 1200.
        private void OnDayOver()
        {
            if (!CanEdit()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            _driver.DevSetClockRunning(false);
            _driver.DevSetMinute(1200);
            _refreshTimer = RefreshSeconds;
        }

        private void OnClockToggle(ChangeEvent<bool> evt)
        {
            if (!CanEdit()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            _driver.DevSetClockRunning(evt.newValue);
        }

        // Debounced: the label follows the drag at once, the host gets one DevSetMinute 0.2 s after the last change.
        private void OnMinuteChanged(ChangeEvent<int> evt)
        {
            _minuteLabel.text = UiStrings.Format("dev.time", DayClock.FormatTime(evt.newValue));
            if (!CanEdit()) return;
            _minuteValue = evt.newValue;
            _minuteTimer = MinuteApplyDelay;
            _minutePending = true;
        }

        private void OnSeedChanged(ChangeEvent<string> evt)
        {
            if (evt.newValue != _shownSeedText) _seedDirty = true;   // only a user edit raises this (the refresh uses SetValueWithoutNotify)
        }

        private void OnLuckChanged(ChangeEvent<float> evt)
        {
            if (!IsHost()) return;
            FishingDebugSettings.Luck = evt.newValue;
            _luckLabel.text = UiStrings.Format("dev.luck", FishingDebugSettings.Luck.ToString("0.00", Inv));
        }

        // The F9 rule: the first hit of the cast mask wins (triggers included), so a dock above the water blocks it.
        private void OnHotspot()
        {
            if (!CanEdit()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, RayLength, CastProbe.CastMask, QueryTriggerInteraction.Collide)
                    && hit.collider.gameObject.layer == Layers.Water)
                {
                    _driver.DevSpawnHotspotAt(hit.point.x, hit.point.z);
                    SetLast("hotspot at " + hit.point.x.ToString("0.0", Inv) + ", " + hit.point.z.ToString("0.0", Inv));
                    _refreshTimer = RefreshSeconds;
                    return;
                }
            }
            SetLast(UiStrings.Get("dev.no_water"));
        }

        private void OnApplySeed()
        {
            if (!IsHost()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            EcologyRunState run = EcologySession.Current;
            if (run != null && run.HostCastCounter > 0)
            {
                _seedStatus.text = UiStrings.Get("dev.seed_locked");
                return;
            }
            if (!TryParseSeed(_seedField.value, out ulong seed))
            {
                _seedStatus.text = UiStrings.Get("dev.bad_seed");
                return;
            }
            FishingHost host = FishingHost.Current;
            if (host == null)
            {
                SetLast(UiStrings.Get("dev.no_world"));
                return;
            }
            host.SetRunSeed(seed);
            _seedStatus.text = string.Empty;
            SetLast("run seed 0x" + seed.ToString("X16", Inv));
            _seedDirty = false;
            _shownSeedText = null;                            // the next refresh shows the applied seed
            _refreshTimer = RefreshSeconds;
        }

        // Decimal or 0x hex, invariant. Whitespace is trimmed; anything else (empty, negative, overflow) is rejected.
        internal static bool TryParseSeed(string text, out ulong seed)
        {
            seed = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return t.Length > 2 && ulong.TryParse(t.Substring(2), NumberStyles.AllowHexSpecifier, Inv, out seed);
            return ulong.TryParse(t, NumberStyles.None, Inv, out seed);
        }

        // The lowest rod index of that tier. The owner pre-checks Idle (the host refuses a change during a cast), like F4.
        private void OnRodTier(int tier)
        {
            if (!IsHost()) { SetLast(UiStrings.Get("dev.host_only")); return; }
            FishingRodController controller = FishingRodController.Local;
            FishingRodNetwork net = controller != null ? controller.GetComponent<FishingRodNetwork>() : null;
            if (net == null || !net.IsSpawned || controller.Machine == null) { SetLast(UiStrings.Get("dev.no_rod")); return; }
            if (controller.Machine.State != RodState.Idle) { SetLast(UiStrings.Get("dev.refused_cast")); return; }
            int index = LowestRodOfTier(_registry, tier);
            if (index < 0) { SetLast("no rod of tier " + tier.ToString(Inv)); return; }
            net.SelectRodRpc((ushort)index);
            SetLast("rod request: " + _registry.RodAt((ushort)index).DisplayName);
        }

        internal static int LowestRodOfTier(ContentRegistry registry, int tier)
        {
            if (registry == null) return -1;
            int count = registry.Rods.Count;
            for (int i = 0; i < count; i++)
            {
                if (registry.RodAt((ushort)i).Tier == tier) return i;
            }
            return -1;
        }

        private void OnLoadMap(string spotId)
        {
            if (!IsHost() || _bus == null) { SetLast(UiStrings.Get("dev.host_only")); return; }
            _bus.Publish(new DevLoadSpotRequested(spotId));   // SceneFlow loads it (host only, debug builds only; M3-D31)
            SetLast("load " + spotId);
        }

        // ---------------------------------------------------------------------------------------------------------- refresh (4 Hz, panel shown)

        // SetValueWithoutNotify everywhere, so a refresh never triggers an action. Selected buttons carry the "selected" class (bold plus a
        // border: shape, not hue only). Clients see the replicated values, and "n/a (client)" for the run state.
        private void Refresh()
        {
            bool host = IsHost();
            WorldState world = _cachedWorld;
            bool hasWorld = world != null && world.HasState;
            bool editable = host && hasWorld && _driver != null;
            SetRowsEnabled(editable);

            // No world, no state or (host) no driver: the "no world" header and disabled rows. Otherwise host or client title.
            string titleKey = !hasWorld || (host && _driver == null) ? "dev.no_world" : host ? "dev.panel_title_host" : "dev.panel_title_client";
            _title.text = UiStrings.Get(titleKey);

            int minute = hasWorld ? world.Minute : 0;
            if (!_minutePending)
            {
                _minute.SetValueWithoutNotify(minute);
                _minuteLabel.text = UiStrings.Format("dev.time", hasWorld ? DayClock.FormatTime(minute) : "-");
            }

            Season season = hasWorld ? world.Season : Season.Spring;
            for (int i = 0; i < _seasonButtons.Count; i++) _seasonButtons[i].EnableInClassList("selected", hasWorld && (int)season == i);

            // The override is driver-local, so a client cannot know it: it sees the weather that is actually in effect.
            WeatherMask overrideMask = host && _driver != null ? _driver.WeatherOverride : WeatherMask.None;
            WeatherMask effective = hasWorld ? world.Weather : WeatherMask.None;
            for (int i = 0; i < _weatherButtons.Count; i++)
            {
                bool selected = host ? WeatherChoices[i] == overrideMask : i > 0 && WeatherChoices[i] == effective;
                _weatherButtons[i].EnableInClassList("selected", selected);
            }

            bool running = host && _driver != null ? _driver.ClockRunning : hasWorld && world.NetClock.Value.Running != 0;
            _clockToggle.SetValueWithoutNotify(running);

            _hotspotStatus.text = HotspotText(world, hasWorld);

            RefreshSeed(host);
            RefreshLuck(host);
            RefreshRod();
            RefreshMaps(host, hasWorld ? world.SpotId : null);
        }

        private static string HotspotText(WorldState world, bool hasWorld)
        {
            if (!hasWorld) return "hotspot: -";
            HotspotData h = world.NetHotspot.Value.ToCore();
            if (!h.Active) return "hotspot: none";
            return "hotspot: " + h.X.ToString("0.0", Inv) + ", " + h.Z.ToString("0.0", Inv) + " r=" + h.Radius.ToString("0.0", Inv)
                + " until " + h.EndMinute.ToString(Inv);
        }

        private void RefreshSeed(bool host)
        {
            EcologyRunState run = EcologySession.Current;
            bool locked = host && run != null && run.HostCastCounter > 0;
            if (locked) _seedDirty = false;
            // The field is written only when the shown value is stale and the user is not editing: typed text survives losing focus (Tab or a
            // click on Apply) until it is applied, the row locks or the panel is reopened.
            if (!_seedDirty && !_seedFocused)
            {
                string desired = host ? (run != null ? "0x" + run.RunSeed.ToString("X16", Inv) : string.Empty) : "n/a (client)";
                if (desired != _shownSeedText)
                {
                    _seedField.SetValueWithoutNotify(desired);
                    _shownSeedText = desired;
                }
            }
            if (host)
            {
                // The row is writable only before the session's first cast; a bad-parse message stays until the next apply.
                _seedField.SetEnabled(!locked && _rowsEnabled);
                _seedApply.SetEnabled(!locked && _rowsEnabled);
                if (locked) _seedStatus.text = UiStrings.Get("dev.seed_locked");
                else if (_seedStatus.text == UiStrings.Get("dev.seed_locked")) _seedStatus.text = string.Empty;
            }
            else
            {
                _seedStatus.text = string.Empty;
            }
        }

        private void RefreshLuck(bool host)
        {
            if (!host)
            {
                _luckLabel.text = UiStrings.Format("dev.luck", "n/a (client)");
                return;
            }
            _luck.SetValueWithoutNotify(FishingDebugSettings.Luck);
            _luckLabel.text = UiStrings.Format("dev.luck", FishingDebugSettings.Luck.ToString("0.00", Inv));
        }

        private void RefreshRod()
        {
            FishingRodController controller = FishingRodController.Local;
            FishingRodNetwork net = controller != null ? controller.GetComponent<FishingRodNetwork>() : null;
            int tier = -1;
            if (net != null && net.IsSpawned && _registry != null)
            {
                ushort index = net.NetRodIndex.Value;
                if (index < _registry.Rods.Count) tier = _registry.RodAt(index).Tier;
            }
            for (int i = 0; i < _rodButtons.Count; i++) _rodButtons[i].EnableInClassList("selected", tier == i);
        }

        // The current spot's button is disabled; the whole row is the host's only (Load map stays usable on the host without a world, to leave
        // a broken scene).
        private void RefreshMaps(bool host, string currentSpot)
        {
            for (int i = 0; i < _mapButtons.Count; i++)
            {
                KeyValuePair<Button, string> map = _mapButtons[i];
                bool current = !string.IsNullOrEmpty(currentSpot) && map.Value == currentSpot;
                map.Key.SetEnabled(host && !current);
                map.Key.EnableInClassList("selected", current);
            }
        }

        private void SetRowsEnabled(bool enabled)
        {
            if (_rowsEnabledKnown && _rowsEnabled == enabled) return;
            _rowsEnabled = enabled;
            _rowsEnabledKnown = true;
            for (int i = 0; i < _worldRows.Count; i++) _worldRows[i].SetEnabled(enabled);
        }
    }
}
