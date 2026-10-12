using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: client-only (local UI), read-only. Reads FishingRodController.Local (Machine getters), the local PlayerCreel mirror,
    // ISettings and the App EventBus; it never writes gameplay state (C 2.8 #17). Spec 11.4 (HUD table), 9.11 (accessibility), 10.2.
    // The presenter lives in the App root (UiFlowController), so it re-resolves the local rod and creel across leave, rejoin and scene reloads.
    // HUD buttons: this presenter creates none (the settings rows are a SliderInt and Toggles from AppRoot.uxml); a button added here later
    // must call AudioBus.PlayUi(SfxIds.UiClick) itself, because the click-sound loop runs before the extension bind loop.
    internal sealed class FishingHudPresenter : IHudExtension
    {
        private const float RepaintEpsilon = 0.002f;
        private const float WarnTension = 0.8f;
        private const float DefaultCaptionSeconds = 2f;
        private const float ToastSeconds = 4f;
        private const float BlinkHz = 6f;
        private const float RingRadius = 32f;
        private const float ArcRadius = 60f;
        private const float ArcStartDeg = 135f, ArcSpanDeg = 90f;
        private const float BarTrackHeight = 8f, BarWarnHeight = 10f;

        private static readonly Color FallbackAccent = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color FallbackWarn = new Color(1f, 0.78f, 0.27f, 1f);
        private static readonly Color FallbackBad = new Color(1f, 0.43f, 0.35f, 1f);
        private static readonly Color FallbackText = new Color(0.92f, 0.94f, 0.96f, 1f);
        private static readonly Color FallbackPanel = new Color(0.08f, 0.12f, 0.17f, 1f);

        private bool _bound;
        private EventBus _bus;
        private ISettings _settings;
        private ContentRegistry _registry;
        private IDisposable _captionSub, _catchSub;

        private VisualElement _ring, _gauge;
        private Label _hook, _caption, _creelLabel;

        private Color _accent = FallbackAccent, _warn = FallbackWarn, _bad = FallbackBad, _text = FallbackText, _panel = FallbackPanel;
        private bool _tuningKnown;
        private int _maxEntries, _maxWeightG;

        // Local references (re-resolved on LocalChanged and by a cheap compare in Tick).
        private FishingRodController _controller;
        private PlayerNetState _creelOwner;
        private PlayerCreel _creel;
        private bool _creelDirty;

        // Per-element state (visibility flags avoid redundant style writes; draw values gate repaints).
        private bool _ringShown, _gaugeShown, _hookShown, _captionShown, _creelShown;
        private float _ringValue;
        private LineGaugeMode _gaugeMode;
        private float _gaugeValue;
        private bool _gaugeWarn, _gaugeGrace, _gaugeBlinkOn;
        private float _captionTimer;
        private string _creelClass = string.Empty;
        private readonly Dictionary<byte, float> _lastCrewToast = new Dictionary<byte, float>();

        public void Bind(VisualElement root, ServiceRegistry app)
        {
            // Never throws: UiFlowController's bind loop has no try/catch (T2 review), so a throw would block the whole UI.
            try
            {
                if (_bound) Unbind();
                BindCore(root, app);
            }
            catch (Exception exception)
            {
                Unbind();
                Debug.LogException(exception);   // a real defect surfaces (PlayMode fails); the UI still comes up
            }
        }

        private void BindCore(VisualElement root, ServiceRegistry app)
        {
            if (root == null || app == null) return;
            if (!app.TryGet(out EventBus bus) || !app.TryGet(out ISettings settings)) return;
            // Required elements: UiQuery.Req throws on a renamed or missing one; Bind's try/catch logs it (PlayMode fails) and the UI still comes up.
            var ring = UiQuery.Req<VisualElement>(root, "FishingChargeRing");
            var gauge = UiQuery.Req<VisualElement>(root, "FishingLineGauge");
            var hook = UiQuery.Req<Label>(root, "FishingHookPrompt");
            var caption = UiQuery.Req<Label>(root, "FishingCaption");
            var creel = UiQuery.Req<Label>(root, "FishingCreel");

            _bus = bus;
            _settings = settings;
            _ring = ring;
            _gauge = gauge;
            _hook = hook;
            _caption = caption;
            _creelLabel = creel;
            _registry = app.TryGet(out ContentRegistry registry) ? registry : null;

            ReadRegistry();

            _hook.text = UiStrings.Get("hud.click");
            _ring.generateVisualContent += DrawRing;
            _gauge.generateVisualContent += DrawGauge;
            HideAll();

            _captionSub = _bus.Subscribe<CaptionRequested>(OnCaption);
            _catchSub = _bus.Subscribe<CatchAnnounced>(OnCatchAnnounced);
            _settings.Changed += OnSettingsChanged;
            FishingRodController.LocalChanged += OnLocalChanged;
            _bound = true;
            ResolveLocals();
        }

        public void Tick(float dt)
        {
            if (!_bound) return;
            ResolveLocals();
            TickRing();
            TickHook();
            TickCaption(dt);
            TickGauge();
            TickCreel();
        }

        public void Unbind()
        {
            if (_captionSub != null) { _captionSub.Dispose(); _captionSub = null; }
            if (_catchSub != null) { _catchSub.Dispose(); _catchSub = null; }
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            FishingRodController.LocalChanged -= OnLocalChanged;
            SetCreel(null, null);
            _creelClass = string.Empty;
            if (_ring != null) _ring.generateVisualContent -= DrawRing;
            if (_gauge != null) _gauge.generateVisualContent -= DrawGauge;
            if (_bound) HideAll();
            _bound = false;
            _controller = null;
            _bus = null;
            _settings = null;
            _registry = null;
            _ring = null;
            _gauge = null;
            _hook = null;
            _caption = null;
            _creelLabel = null;
            _lastCrewToast.Clear();
        }

        // ---- setup helpers ----

        private void ReadRegistry()
        {
            _tuningKnown = false;
            if (_registry == null) return;
            var tuningDef = _registry.FishingTuning;                       // a Unity object: explicit null check, never ?.
            if (tuningDef != null)
            {
                FishingTuning tuning = tuningDef.ToCore();
                _maxEntries = tuning.CreelMaxEntries;
                _maxWeightG = tuning.CreelMaxWeightG;
                _tuningKnown = _maxEntries > 0 && _maxWeightG > 0;
            }

            var palette = _registry.ArtPalette;
            if (palette != null)
            {
                _accent = Pick(palette.UiAccent, FallbackAccent);
                _warn = Pick(palette.UiWarn, FallbackWarn);
                _bad = Pick(palette.UiBad, FallbackBad);
                _text = Pick(palette.UiText, FallbackText);
                _panel = Pick(palette.UiPanel, FallbackPanel);
            }
        }

        private static Color Pick(Color color, Color fallback) => color.a > 0f ? color : fallback;   // an unassigned Color has alpha 0

        private void HideAll()
        {
            SetShown(_ring, ref _ringShown, false, true);
            SetShown(_gauge, ref _gaugeShown, false, true);
            SetShown(_hook, ref _hookShown, false, true);
            SetShown(_caption, ref _captionShown, false, true);
            SetShown(_creelLabel, ref _creelShown, false, true);
            _captionTimer = 0f;
        }

        // Writes the display style only when the visibility flag changes (or when forced), so Tick does no per-frame style churn.
        private static void SetShown(VisualElement element, ref bool flag, bool visible, bool force = false)
        {
            if (element == null) return;
            if (!force && flag == visible) return;
            flag = visible;
            UiQuery.Show(element, visible);
        }

        // ---- local references ----

        private void OnLocalChanged(FishingRodController controller) => SetController(controller);

        // Leaving the game (the controller goes from set to null) must not leave a caption on screen for the rest of its timer.
        private void SetController(FishingRodController controller)
        {
            bool left = !ReferenceEquals(_controller, null) && controller == null;
            _controller = controller;
            if (left)
            {
                _captionTimer = 0f;
                SetShown(_caption, ref _captionShown, false);
            }
        }

        private void ResolveLocals()
        {
            var controller = FishingRodController.Local;
            if (!ReferenceEquals(_controller, controller)) SetController(controller);

            var owner = PlayerNetState.Local;
            if (!ReferenceEquals(_creelOwner, owner) || (_creel == null && owner != null))
            {
                PlayerCreel creel = owner != null ? owner.GetComponent<PlayerCreel>() : null;
                SetCreel(owner, creel);
            }
        }

        // The creel reference changed: move the PlayerCreel.Changed subscription and re-read Count / TotalWeightG
        // (the initial NetworkList sync raises no Changed, so the first read happens here, not on an event).
        private void SetCreel(PlayerNetState owner, PlayerCreel creel)
        {
            _creelOwner = owner;
            if (!ReferenceEquals(_creel, creel))
            {
                if (!ReferenceEquals(_creel, null)) _creel.Changed -= OnCreelChanged;   // also for a destroyed component: a plain C# event
                _creel = creel;
                if (_creel != null) _creel.Changed += OnCreelChanged;
            }
            _creelDirty = true;
        }

        private void OnCreelChanged() => _creelDirty = true;

        // ---- per-element ticks ----

        private void TickRing()
        {
            var machine = _controller != null ? _controller.Machine : null;
            bool on = machine != null && machine.State == RodState.Charging;
            float value = on ? machine.ChargeNorm : 0f;
            bool changedVisibility = on != _ringShown;
            if (changedVisibility) SetShown(_ring, ref _ringShown, on);
            if (on && (changedVisibility || Mathf.Abs(value - _ringValue) > RepaintEpsilon))
            {
                _ringValue = value;
                _ring.MarkDirtyRepaint();
            }
        }

        private void TickHook()
        {
            var machine = _controller != null ? _controller.Machine : null;
            bool on = machine != null && machine.State == RodState.Bite && machine.HasResult && machine.Roll.HookWindow > 0f;
            SetShown(_hook, ref _hookShown, on);
        }

        private void TickCaption(float dt)
        {
            if (_captionTimer <= 0f) return;
            _captionTimer -= dt;
            if (_captionTimer <= 0f) SetShown(_caption, ref _captionShown, false);
        }

        private void TickGauge()
        {
            var machine = _controller != null ? _controller.Machine : null;
            LineGaugeMode mode = _settings != null ? ModeOf(_settings.Current.lineGauge) : LineGaugeMode.Off;
            FightSim fight = machine != null ? machine.Fight : null;
            bool on = _tuningKnown && mode != LineGaugeMode.Off && machine != null && machine.State == RodState.Hooked
                && fight != null && !fight.IsJunk;

            if (!on)
            {
                SetShown(_gauge, ref _gaugeShown, false);
                return;
            }

            float tension = Mathf.Clamp01(fight.State.Tension);
            bool grace = fight.State.SnapTimer > 0f;
            bool warn = tension >= WarnTension;
            bool blinkOn = grace && ((int)(Time.unscaledTime * BlinkHz * 2f) & 1) == 0;   // 6 Hz on/off

            bool becameVisible = !_gaugeShown;
            bool modeChanged = mode != _gaugeMode;
            if (becameVisible) SetShown(_gauge, ref _gaugeShown, true);
            if (modeChanged || becameVisible) _gauge.EnableInClassList("bar", mode == LineGaugeMode.Bar);

            if (becameVisible || modeChanged || Mathf.Abs(tension - _gaugeValue) > RepaintEpsilon
                || warn != _gaugeWarn || grace != _gaugeGrace || blinkOn != _gaugeBlinkOn)
            {
                _gaugeMode = mode;
                _gaugeValue = tension;
                _gaugeWarn = warn;
                _gaugeGrace = grace;
                _gaugeBlinkOn = blinkOn;
                _gauge.MarkDirtyRepaint();
            }
        }

        private static LineGaugeMode ModeOf(int raw) =>
            raw == (int)LineGaugeMode.Arc ? LineGaugeMode.Arc : raw == (int)LineGaugeMode.Bar ? LineGaugeMode.Bar : LineGaugeMode.Off;

        // Count and weight only. A species is never shown here: on the host the creel Changed (and FishCaught) fire one frame
        // BEFORE CatchAnnounced, so the species and the toast wait for the announce (OnCatchAnnounced).
        private void TickCreel()
        {
            if (!_creelDirty) return;
            _creelDirty = false;

            if (_creel == null || !_tuningKnown)
            {
                SetShown(_creelLabel, ref _creelShown, false);
                return;
            }

            // Built from the replicated mirror on every peer: PlayerCreel.Snapshot is host-only (RequireServer) and throws on a client.
            var snapshot = new CreelSnapshot(_creel.Count, _creel.TotalWeightG, _maxEntries, _maxWeightG);
            bool full = CreelRules.IsFull(in snapshot);
            bool warn = !full && CreelRules.IsWarn(in snapshot);

            _creelLabel.text = full
                ? UiStrings.Get("hud.creel_full")
                : UiStrings.Format("hud.creel",
                    snapshot.Entries.ToString(CultureInfo.InvariantCulture),
                    snapshot.MaxEntries.ToString(CultureInfo.InvariantCulture),
                    Kg(snapshot.TotalWeightG));

            string stateClass = full ? "full" : warn ? "warn" : string.Empty;
            if (!string.Equals(stateClass, _creelClass, StringComparison.Ordinal))
            {
                _creelClass = stateClass;
                _creelLabel.EnableInClassList("warn", warn);
                _creelLabel.EnableInClassList("full", full);
                _creelLabel.style.color = full ? _bad : warn ? _warn : _text;
            }
            SetShown(_creelLabel, ref _creelShown, true);
        }

        private static string Kg(int weightG) => (weightG / 1000f).ToString("0.0", CultureInfo.InvariantCulture);

        // ---- events ----

        private void OnCaption(CaptionRequested message)
        {
            if (!_bound || _settings == null || !_settings.Current.captions || string.IsNullOrEmpty(message.Key)) return;
            _caption.text = UiStrings.Get(message.Key);
            _captionTimer = message.Seconds > 0f ? message.Seconds : DefaultCaptionSeconds;
            SetShown(_caption, ref _captionShown, true);
        }

        private void OnSettingsChanged(SettingsData data)
        {
            if (!_bound || data == null) return;
            if (!data.captions)
            {
                _captionTimer = 0f;
                SetShown(_caption, ref _captionShown, false);
            }
        }

        private void OnCatchAnnounced(CatchAnnounced message)
        {
            if (!_bound) return;
            CatchAnnouncement a = message.Announcement;
            bool local = message.IsLocalCatcher;

            switch (a.Kind)
            {
                case AnnounceKind.Landed:
                    if (!local && CrewLimited(a.CatcherSlot)) return;
                    if (a.Catch.IsJunk)
                    {
                        Toast("toast.junk", CatcherName(a.CatcherSlot), ItemName(true, a.Catch.ItemIndex),
                            a.Catch.ValueCoins.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Toast("toast.catch", CatcherName(a.CatcherSlot), Kg(a.Catch.WeightG), ItemName(false, a.Catch.ItemIndex),
                            a.Catch.ValueCoins.ToString(CultureInfo.InvariantCulture));
                    }
                    if (a.CleanBonus && local) Toast("toast.clean");
                    break;
                case AnnounceKind.Lost:
                    if (!local && CrewLimited(a.CatcherSlot)) return;
                    Toast("toast.lost");
                    break;
                case AnnounceKind.PityCast:
                    // Not rate limited (at most one per three failed casts). a.Catch is default: the registry is not indexed with it.
                    Toast("toast.pity", CatcherName(a.CatcherSlot));
                    break;
                case AnnounceKind.ReleasedCreelFull:
                    if (local) Toast("toast.creel_full", ItemName(a.Catch.IsJunk, a.Catch.ItemIndex));
                    break;
            }
        }

        // Crew toasts (not the local catcher) are limited to one per CrewToastMinIntervalSeconds per catcher slot. A passing toast
        // stamps the slot; a dropped one does not extend the window. The in-world reveal (T23) still shows for dropped toasts.
        private bool CrewLimited(byte slot)
        {
            float now = Time.unscaledTime;
            if (_lastCrewToast.TryGetValue(slot, out float last) && now - last < FishingConstants.CrewToastMinIntervalSeconds) return true;
            _lastCrewToast[slot] = now;
            return false;
        }

        private void Toast(string key, params string[] args)
        {
            if (_bus == null) return;
            // ToastRequested carries one Arg, so the text is formatted here and sent through the passthrough key (ruling G-Q1).
            _bus.Publish(new ToastRequested("toast.raw", UiStrings.Format(key, args), ToastSeconds));
        }

        private static string CatcherName(byte slot)
        {
            var players = PlayerNetState.Active;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.Slot == slot)
                {
                    string name = player.DisplayName;
                    return string.IsNullOrEmpty(name) ? "?" : name;
                }
            }
            return "?";   // the catcher left before the announce arrived
        }

        private string ItemName(bool isJunk, ushort index)
        {
            if (_registry == null) return "?";
            try
            {
                string name = _registry.DisplayNameOf(isJunk, index);
                return string.IsNullOrEmpty(name) ? "?" : name;
            }
            catch (ArgumentOutOfRangeException)
            {
                return "?";
            }
        }

        // ---- drawing (Painter2D; angles in degrees, 0 = 3 o'clock, y down so positive is clockwise on screen) ----

        private void DrawRing(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            Rect rect = context.visualElement.contentRect;
            var centre = new Vector2(rect.width * 0.5f, rect.height * 0.5f);

            painter.lineCap = LineCap.Butt;
            painter.lineWidth = 2f;
            painter.strokeColor = new Color(_panel.r, _panel.g, _panel.b, 0.6f);
            painter.BeginPath();
            painter.Arc(centre, RingRadius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
            painter.Stroke();

            float sweep = Mathf.Clamp01(_ringValue) * 360f;
            if (sweep < 0.5f) return;
            painter.lineWidth = 4f;
            painter.strokeColor = _accent;
            painter.BeginPath();
            painter.Arc(centre, RingRadius, Angle.Degrees(-90f), Angle.Degrees(-90f + sweep), ArcDirection.Clockwise);
            painter.Stroke();
        }

        private void DrawGauge(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            Rect rect = context.visualElement.contentRect;
            Color fill = _gaugeWarn || _gaugeGrace ? _warn : _accent;
            bool drawFill = true;
            if (_gaugeGrace)
            {
                fill = _bad;
                drawFill = _gaugeBlinkOn;      // the blink (and the 0.8 tick) carry the meaning without hue
            }
            float value = Mathf.Clamp01(_gaugeValue);
            Color dim = new Color(_panel.r, _panel.g, _panel.b, 0.7f);
            bool thick = _gaugeWarn || _gaugeGrace;

            if (_gaugeMode == LineGaugeMode.Bar)
            {
                float w = rect.width, h = rect.height;
                float trackTop = (h - BarTrackHeight) * 0.5f;
                FillRect(painter, 0f, trackTop, w, BarTrackHeight, dim);
                if (drawFill && value > 0.002f)
                {
                    float height = thick ? BarWarnHeight : BarTrackHeight;
                    FillRect(painter, 0f, (h - height) * 0.5f, w * value, height, fill);
                }
                float tickX = w * WarnTension;
                FillRect(painter, tickX - 1f, 0f, 2f, h, _text);
                return;
            }

            // Arc: the bottom quarter of a circle of radius 60 centred on the top-middle of the element (the crosshair),
            // from 135 deg (left) to 45 deg (right). The fill grows from the left, so angles decrease: counter-clockwise.
            var centre = new Vector2(rect.width * 0.5f, 0f);
            painter.lineCap = LineCap.Butt;
            painter.lineWidth = 3f;
            painter.strokeColor = dim;
            painter.BeginPath();
            painter.Arc(centre, ArcRadius, Angle.Degrees(ArcStartDeg), Angle.Degrees(ArcStartDeg - ArcSpanDeg), ArcDirection.CounterClockwise);
            painter.Stroke();

            if (drawFill && value > 0.004f)
            {
                painter.lineWidth = thick ? 5f : 3f;
                painter.strokeColor = fill;
                painter.BeginPath();
                painter.Arc(centre, ArcRadius, Angle.Degrees(ArcStartDeg), Angle.Degrees(ArcStartDeg - ArcSpanDeg * value), ArcDirection.CounterClockwise);
                painter.Stroke();
            }

            float tickRad = (ArcStartDeg - ArcSpanDeg * WarnTension) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(tickRad), Mathf.Sin(tickRad));
            painter.lineWidth = 2f;
            painter.strokeColor = _text;
            painter.BeginPath();
            painter.MoveTo(centre + dir * (ArcRadius - 8f));
            painter.LineTo(centre + dir * (ArcRadius + 8f));
            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, float x, float y, float w, float h, Color color)
        {
            if (w <= 0f || h <= 0f) return;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x + w, y));
            painter.LineTo(new Vector2(x + w, y + h));
            painter.LineTo(new Vector2(x, y + h));
            painter.ClosePath();
            painter.Fill();
        }
    }

    // Pure settings rules for the fishing rows (no VisualElement, no ISettings): EditMode reaches this by reflection (C 2.8 #8, as T23's tests do).
    internal static class FishingHudRules
    {
        // Ruling G-Q4 (C 7.10): whenever the assist toggle changes from off to on while the line gauge is Off, the gauge becomes Arc, every time.
        // Any other change (assist turning off, on to on, off to off, a gauge already Arc or Bar) leaves the gauge alone.
        // Returns true when it wrote the gauge, so the caller can refresh the slider. Stateless: there is no "already done once" flag.
        internal static bool OnAssistChanged(SettingsData data, bool oldValue, bool newValue)
        {
            if (data == null) return false;
            if (oldValue || !newValue) return false;
            if (data.lineGauge != (int)LineGaugeMode.Off) return false;
            data.lineGauge = (int)LineGaugeMode.Arc;
            return true;
        }
    }
}
