// Gameplay/World/DevWorldDriver.cs - the M3 dev world: clock, weather track and hotspot director (C 7.14, M3-D11, M3-D31, M3-D57).
// Authority: HOST ONLY (inert on clients: ServerInitialize runs only from WorldState.OnNetworkSpawn on the server, and Update returns
// unless the sibling WorldState is spawned as server). M3 dev clock, weather track and hotspot director on the scene root "World"
// (C 6.3); reads and writes the session run state (EcologySession). M4's RunDirector/DayDirector replace it. Writes go only through
// WorldState.ServerSet*; NetClock is written only on start, stop, pause, scrub, minute set and new day (14.2), never per frame.
// No Awake work: in-scene objects wake before NGO spawns them.
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class DevWorldDriver : MonoBehaviour
    {
        private readonly DevClock _clock = new DevClock(DayClock.DefaultMinutesPerSecond);
        private readonly LogOnce _log = new LogOnce();
        private WorldState _world;
        private EcologyRunState _run;
        private WeatherDirector _weather;
        private HotspotPlanner _hotspots;                  // null without a baked grid (or at the 12.7 cut-line)
        private WeatherTableData _table;
        private ShoreGrid _grid;
        private EcologyTuning _tuning;
        private bool _hasZone;
        private IPauseState _pause;
        private WeatherMask _override = WeatherMask.None;  // driver-local: a map switch resets it (new driver instance)

        public EcologyRunState Run => _run;
        public bool ClockRunning => _clock.DevRunning;
        public WeatherMask WeatherOverride => _override;
        public WeatherDirector Weather => _weather;

        private bool Active => _world != null && _world.IsSpawned && _world.IsServer;
        private double Now => _world.NetworkManager.ServerTime.Time;

        // WorldState.OnNetworkSpawn (server), before HasState.
        internal void ServerInitialize(WorldState world)
        {
            _world = world;
            ResolveZone();                                  // registry.Ecology -> Spots[SpotIndex(world.SpotId)].ZoneIndex -> Zones[i]: Weather, Grid; eco.Tuning
            HookPause();                                    // ServiceRoots.Game IPauseState: read Paused now, subscribe Changed
            _clock.SetPaused(Now, _pause != null && _pause.Paused);   // anchor not yet written: the return value is ignored
            Adopt(EcologySession.GetOrCreate(FishingConstants.DefaultDevRunSeed));   // CCR-D3: M3 keeps the fixed dev run seed (M3-D57)
        }

        // Session start, map-switch resume (M3-D31) and dev seed change all land here: the run's season, the identical rebuilt
        // track, the clock at minute 0 of the run's current day running, a fresh planner for that day, weather of segment 0.
        private void Adopt(EcologyRunState run)
        {
            double now = Now;
            _run = run;
            _world.ServerSetSeason(run.Season);
            RebuildWeather();
            _clock.SetRunning(now, true);
            _clock.StartAt(now, 0f);
            _world.ServerSetClock(_clock.Anchor);
            _hotspots = NewPlanner(run.Day);
            _world.ServerSetHotspot(default);
            PushWeather(0);
        }

        private void Update()
        {
            if (!Active || _run == null) return;
            EcologyRunState current = EcologySession.Current;
            if (current != null && !ReferenceEquals(current, _run)) { Adopt(current); return; }   // FishingHost.SetRunSeed replaced it
            double now = Now;
            if (_clock.ShouldAutoNewDay(now)) { DevNewDay(); return; }
            int minute = DevWorldRules.LookupMinute(_clock.MinuteAt(now));
            PushWeather(minute);
            if (_hotspots != null && _hotspots.Advance(minute)) _world.ServerSetHotspot(_hotspots.Current);
        }

        private void OnDisable() { UnhookPause(); }
        private void OnDestroy() { UnhookPause(); }

        // ------------------------------------------------------------------ dev controls (host only; T22's panel and tests call these)

        private void HandlePauseChanged(bool paused)       // solo pause (R34): stop the clock, never timeScale; resume is continuous
        {
            if (!Active) return;
            if (_clock.SetPaused(Now, paused)) _world.ServerSetClock(_clock.Anchor);
        }

        public void DevScrub(bool held)                     // F8 (T22): x60 while held; re-anchored on press and on release only
        {
            if (!Active) return;
            if (_clock.SetScrub(Now, held)) _world.ServerSetClock(_clock.Anchor);
        }

        public void DevSetClockRunning(bool running)
        {
            if (!Active) return;
            if (_clock.SetRunning(Now, running)) _world.ServerSetClock(_clock.Anchor);
        }

        // 0..1200. 1200 with a RUNNING clock means a new day on the next frame (auto new day); tests that need "day over" stop the clock first.
        public void DevSetMinute(int minute)
        {
            if (!Active) return;
            _clock.StartAt(Now, minute);
            _world.ServerSetClock(_clock.Anchor);
            int m = DevWorldRules.LookupMinute(_clock.MinuteAt(Now));
            _hotspots = NewPlanner(_run.Day);               // rebuild for the day, then catch up: a backward jump would otherwise keep a stale
            if (_hotspots != null) _hotspots.Advance(m);    // future hotspot; forward jumps give the same state (one-call catch-up, T7)
            _world.ServerSetHotspot(_hotspots != null ? _hotspots.Current : default);
            PushWeather(m);
        }

        public void DevNewDay()                             // Day++, minute 0, the day's planner and track weather (M3-D11); run/scrub/pause kept
        {
            if (!Active) return;
            _run.Day++;
            _weather?.EnsureDay(_run.Day);                  // day 8+: infinite continuation, one day at a time (T7)
            _clock.StartAt(Now, 0f);
            _world.ServerSetClock(_clock.Anchor);
            _hotspots = NewPlanner(_run.Day);
            _world.ServerSetHotspot(default);
            PushWeather(0);
        }

        public void DevSetSeason(Season s)                  // the track depends on the season: rebuild it (same seed)
        {
            if (!Active) return;
            _run.Season = s;
            _world.ServerSetSeason(s);
            RebuildWeather();
            PushWeather(DevWorldRules.LookupMinute(_clock.MinuteAt(Now)));
        }

        public void DevSetWeatherOverride(WeatherMask single)
        {
            if (!Active) return;
            if (!DevWorldRules.IsValidOverride(single))
            {
                if (_log.ShouldLog("bad-override")) Debug.LogWarning("DevWorldDriver: weather override must be None, Clear, Rain or Fog.", this);
                return;
            }
            _override = single;
            PushWeather(DevWorldRules.LookupMinute(_clock.MinuteAt(Now)));
        }

        public void DevSpawnHotspotAt(float x, float z)
        {
            if (!Active) return;
            if (_hotspots == null)
            {
                if (_log.ShouldLog("no-planner")) Debug.LogWarning("DevWorldDriver: no hotspot planner (grid not baked).", this);
                return;
            }
            _world.ServerSetHotspot(_hotspots.ForceAt(x, z, DevWorldRules.LookupMinute(_clock.MinuteAt(Now))));
        }

        // ------------------------------------------------------------------ internals

        private void PushWeather(int minute)                // WorldState skips unchanged values: no per-frame traffic
        {
            WeatherMask w = _override != WeatherMask.None ? _override : (_weather != null ? _weather.At(_run.Day, minute) : WeatherMask.Clear);
            _world.ServerSetWeather(w);
        }

        private void RebuildWeather()
        {
            _weather = _hasZone ? new WeatherDirector(in _table, _run.Season, _run.RunSeed) : null;
            _weather?.EnsureDay(_run.Day);
        }

        // 12.7 cut-line: if the controller calls it, return null here always (one line). Nothing else changes.
        private HotspotPlanner NewPlanner(int day)
        {
            if (!_hasZone) return null;
            if (_grid == null)
            {
                if (_log.ShouldLog("no-grid")) Debug.LogWarning("DevWorldDriver: spot '" + _world.SpotId + "' has no baked shore grid; no hotspots.", this);
                return null;
            }
            return new HotspotPlanner(in _tuning, _grid, _run.RunSeed, day);
        }

        // Failure (no registry or ecology database, unknown spot): LogWarning once. The clock still runs, the weather stays Clear, no hotspots.
        // C2 #9 makes a PlayMode run with that warning fail, which is intended.
        private void ResolveZone()
        {
            _hasZone = false;
            _grid = null;
            ServiceRegistry app = ServiceRoots.App;
            ContentRegistry registry = null;
            if (app == null || !app.TryGet(out registry) || registry == null)
            {
                Warn("no-registry", "DevWorldDriver: no ContentRegistry; weather stays Clear and there are no hotspots.");
                return;
            }
            EcologyDatabase eco = registry.Ecology;
            if (eco == null)
            {
                Warn("no-ecology", "DevWorldDriver: the registry has no ecology database (run FishGame > Build All).");
                return;
            }
            string spot = _world.SpotId;
            int spotIndex;
            try { spotIndex = string.IsNullOrEmpty(spot) ? -1 : registry.SpotIndex(spot); }
            catch (System.Exception) { spotIndex = -1; }
            if (spotIndex < 0 || spotIndex >= eco.Spots.Count)
            {
                Warn("no-spot", "DevWorldDriver: WorldState spotId '" + spot + "' is not a registered spot.");
                return;
            }
            ZoneData zone = eco.Zones[eco.Spots[spotIndex].ZoneIndex];
            _table = zone.Weather;
            _grid = zone.Grid;
            _tuning = eco.Tuning;
            _hasZone = true;
        }

        private void Warn(string key, string message)
        {
            if (_log.ShouldLog(key)) Debug.LogWarning(message, this);
        }

        private void HookPause()
        {
            if (_pause != null) return;
            ServiceRegistry game = ServiceRoots.Game;
            if (game != null && game.TryGet(out IPauseState pause))
            {
                _pause = pause;
                _pause.Changed += HandlePauseChanged;
            }
        }

        private void UnhookPause()
        {
            if (_pause == null) return;
            _pause.Changed -= HandlePauseChanged;
            _pause = null;
        }
    }
}
