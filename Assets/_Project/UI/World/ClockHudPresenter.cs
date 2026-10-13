using System;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: local only (client-only UI, read-only). Reads IWorldContext from ServiceRoots.Game; it never writes. Spec 11.4 clock row
    // (top-left, 10-minute steps, M3-D19: no day counter), R34 (the solo pause stops the clock; nothing here touches timeScale).
    // Shown in every build: the clock is gameplay information, not a dev surface. M4 adds "Day n/7" next to it.
    // The day wrap at minute 1200 jumps the clock label from the day's last step back to 6:00 AM with the new day's light: dev-only in M3
    // and deliberately not hidden here (controller notes: RECORD FOR T22/T25/M4).
    internal sealed class ClockHudPresenter : IHudExtension
    {
        private readonly LogOnce _log = new LogOnce();

        private bool _bound;
        private VisualElement _hud;
        private Label _time, _seasonName, _weatherName;
        private GlyphElement _seasonGlyph, _weatherGlyph;
        private IWorldContext _world;
        private int _lastStep = -1;

        public void Bind(VisualElement root, ServiceRegistry app)
        {
            // Never throws: UiFlowController's bind loop has no try/catch (T2 review).
            try
            {
                if (_bound) Unbind();
                BindCore(root);
            }
            catch (Exception exception)
            {
                Unbind();
                Debug.LogException(exception);
            }
        }

        private void BindCore(VisualElement root)
        {
            if (root == null) return;
            _hud = UiQuery.Req<VisualElement>(root, "ClockHud");
            _time = UiQuery.Req<Label>(root, "ClockTime");

            // GlyphElement is a plain C# element (no UXML factory), so the season and weather pieces are added here after the UXML's time label.
            while (_hud.childCount > 1) _hud.RemoveAt(_hud.childCount - 1);
            _seasonGlyph = new GlyphElement { name = "ClockSeason", Lit = true };
            _seasonName = new Label { name = "ClockSeasonName", pickingMode = PickingMode.Ignore, focusable = false };
            _weatherGlyph = new GlyphElement { name = "ClockWeather", Lit = true };
            _weatherName = new Label { name = "ClockWeatherName", pickingMode = PickingMode.Ignore, focusable = false };
            _seasonGlyph.AddToClassList("clock-glyph");
            _weatherGlyph.AddToClassList("clock-glyph");
            _seasonName.AddToClassList("clock-name");
            _weatherName.AddToClassList("clock-name");
            _hud.Add(_seasonGlyph);
            _hud.Add(_seasonName);
            _hud.Add(_weatherGlyph);
            _hud.Add(_weatherName);

            _lastStep = -1;
            _bound = true;
            SetShown(false);
        }

        // Never throws: a despawned or destroyed world must not stop the later extensions from ticking.
        public void Tick(float dt)
        {
            try
            {
                TickCore();
            }
            catch (Exception exception)
            {
                if (_log.ShouldLog("tick:" + exception.GetType().Name)) Debug.LogException(exception);
            }
        }

        private void TickCore()
        {
            if (!_bound) return;

            IWorldContext world = Resolve();
            if (!ReferenceEquals(world, _world)) SwapWorld(world);
            if (world == null || !world.HasState)
            {
                SetShown(false);
                _lastStep = -1;
                return;
            }

            if (_hud.ClassListContains("hidden")) RefreshGlyphs(world);   // first frame after a hidden stretch (Changed fired while hidden)
            SetShown(true);

            // The label is rebuilt only when the 10-minute step changes: no string is built on other frames.
            int step = world.Minute / DayClock.DisplayStepMinutes;
            if (step == _lastStep) return;
            _lastStep = step;
            _time.text = UiStrings.Format("hud.clock", DayClock.FormatTime(world.Minute));
        }

        public void Unbind()
        {
            if (_world != null) _world.Changed -= OnWorldChanged;
            _world = null;
            if (_hud != null) _hud.EnableInClassList("hidden", true);
            _hud = null;
            _time = null;
            _seasonName = null;
            _weatherName = null;
            _seasonGlyph = null;
            _weatherGlyph = null;
            _lastStep = -1;
            _bound = false;
        }

        // ---- world ----

        private static IWorldContext Resolve()
        {
            ServiceRegistry game = ServiceRoots.Game;
            if (game == null || !game.TryGet(out IWorldContext world) || world == null) return null;
            // A despawned WorldState is a destroyed Unity object that the scope still references: read it as "none".
            if (world is UnityEngine.Object unityObject && unityObject == null) return null;
            return world;
        }

        private void SwapWorld(IWorldContext world)
        {
            if (_world != null) _world.Changed -= OnWorldChanged;
            _world = world;
            if (_world != null) _world.Changed += OnWorldChanged;
            _lastStep = -1;
            if (_world != null && _world.HasState) RefreshGlyphs(_world);
        }

        // Season, weather or hotspot changed (not per minute).
        private void OnWorldChanged()
        {
            if (!_bound || _world == null) return;
            try
            {
                if (_world.HasState) RefreshGlyphs(_world);
            }
            catch (Exception exception)
            {
                if (_log.ShouldLog("changed:" + exception.GetType().Name)) Debug.LogException(exception);
            }
        }

        // Shapes differ per value (WorldGlyphs), so the read never depends on colour (spec 10.2).
        private void RefreshGlyphs(IWorldContext world)
        {
            Season season = world.Season;
            _seasonGlyph.Glyph = WorldGlyphs.Of(season);
            _seasonName.text = UiStrings.Get(SeasonKey(season));

            Glyph weather = WorldGlyphs.Of(world.Weather);
            _weatherGlyph.Glyph = weather;
            _weatherName.text = UiStrings.Get(WeatherKey(weather));
        }

        private void SetShown(bool shown)
        {
            if (_hud != null) _hud.EnableInClassList("hidden", !shown);
        }

        internal static string SeasonKey(Season season)
        {
            switch (season)
            {
                case Season.Summer: return "journal.season_summer";
                case Season.Autumn: return "journal.season_autumn";
                case Season.Winter: return "journal.season_winter";
                default: return "journal.season_spring";
            }
        }

        internal static string WeatherKey(Glyph weather)
        {
            switch (weather)
            {
                case Glyph.Rain: return "journal.weather_rain";
                case Glyph.Fog: return "journal.weather_fog";
                default: return "journal.weather_clear";
            }
        }
    }
}
