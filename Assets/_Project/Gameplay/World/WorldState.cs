// Gameplay/World/WorldState.cs - the replicated M3 world state (spec 12.8, C 7.14, C 8.1).
// Authority: host writes every NetworkVariable (season, weather, clock anchor, hotspot); every peer reads. Scene-placed in each Spot scene
// (first in-scene NetworkObject, C 2.8 #24). Registers itself as IWorldContext in ServiceRoots.Game (Replace) on spawn. ServiceRegistry has
// no remove method, so "unregisters on despawn" means: on despawn HasState becomes false and Current is cleared; the registry entry
// dies with the scene's GameScope (consumers check HasState; M3-D56).
// Registers as IWorldContext with ServiceRoots.Game.Replace on spawn (no remove API, CCR-D2): after despawn HasState is false and Current
// is null. Spawn on the server calls the sibling DevWorldDriver.ServerInitialize synchronously, so the first sync never carries default values.
// Peers compute the minute from NetClock and NetworkManager.ServerTime (spec 14.2): no per-tick traffic.
using System;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class WorldState : NetworkBehaviour, IWorldContext
    {
        [SerializeField] private string spotId;   // the field NAME is fixed: WorldSceneObjects writes it with FindProperty("spotId") (M3-D59)

        public readonly NetworkVariable<byte> NetSeason = new NetworkVariable<byte>((byte)Season.Spring,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetWeather = new NetworkVariable<byte>((byte)WeatherMask.Clear,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<ClockAnchor> NetClock = new NetworkVariable<ClockAnchor>(default,   // rate 0: HasState false until written
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<HotspotState> NetHotspot = new NetworkVariable<HotspotState>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static WorldState s_current;
        private readonly LogOnce _log = new LogOnce();
        private bool _spawnedHere;
        private bool _subscribed;

        public static WorldState Current => s_current;

        public event Action Changed;          // season, weather or hotspot changed; also once at spawn and once at despawn
        public event Action ClockChanged;     // the anchor was rewritten; also once at spawn

        public string SpotId => spotId;

        // DevClock never writes rate 0 (R34: pause is Running = false), so a positive rate means "the host has written the anchor".
        public bool HasState => IsSpawned && _spawnedHere && NetClock.Value.MinutesPerSecond > 0f;

        public float MinuteF => HasState ? ClockMath.MinuteAt(NetClock.Value.ToCore(), NetworkManager.ServerTime.Time) : 0f;

        // Floor of MinuteF clamped to 0..1199, for band and segment lookups only.
        public int Minute => DevWorldRules.LookupMinute(MinuteF);

        // The exact minute (never Minute) below 1200: the host and owner day-over cast gate (M3-D53).
        public bool CastingOpen => HasState && MinuteF < DayClock.DayLengthMinutes;

        public Season Season => (Season)Math.Min(NetSeason.Value, (byte)Season.Winter);

        public WeatherMask Weather
        {
            get
            {
                if (!HasState) return WeatherMask.Clear;
                var w = (WeatherMask)NetWeather.Value;
                return w == WeatherMask.None ? WeatherMask.Clear : w;
            }
        }

        // The host clears expired hotspots through ServerSetHotspot; a client may show a stale one for a frame. Only the host's answer rolls.
        public bool IsInHotspot(float x, float z) => HasState && HotspotPlanner.Contains(NetHotspot.Value.ToCore(), x, z);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_current = null;
        }

        // ------------------------------------------------------------------ spawn / despawn

        public override void OnNetworkSpawn()
        {
            if (s_current != null && s_current != this)
            {
                // The second instance stays inert: no driver init (it would overwrite the first one's run state), no IWorldContext replace,
                // no subscription, and _spawnedHere stays false so HasState is false for it.
                Debug.LogError("WorldState: two WorldState objects in one scene; keeping the first.", this);
                return;
            }
            s_current = this;

            if (IsServer)
            {
                DevWorldDriver driver = GetComponent<DevWorldDriver>();
                if (driver != null) driver.ServerInitialize(this);   // writes all four values (spawn payload, D-F4)
                else if (_log.ShouldLog("no-driver")) Debug.LogError("WorldState: World has no DevWorldDriver; the clock never starts.", this);
            }

            Subscribe();   // after the init writes: the host does not raise Changed while HasState is still false
            _spawnedHere = true;
            ServiceRegistry game = ServiceRoots.Game;
            if (game != null) game.Replace<IWorldContext>(this);
            EventIsolation.Invoke(Changed);        // consumers that enabled before the spawn refresh now (seed from .Value)
            EventIsolation.Invoke(ClockChanged);
        }

        public override void OnNetworkDespawn()
        {
            Unsubscribe();
            _spawnedHere = false;
            if (s_current == this) s_current = null;
            EventIsolation.Invoke(Changed);
        }

        public override void OnDestroy()
        {
            Unsubscribe();
            if (s_current == this) s_current = null;
            base.OnDestroy();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            NetSeason.OnValueChanged += OnByteChanged;
            NetWeather.OnValueChanged += OnByteChanged;
            NetHotspot.OnValueChanged += OnHotspotChanged;
            NetClock.OnValueChanged += OnClockChanged;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            NetSeason.OnValueChanged -= OnByteChanged;
            NetWeather.OnValueChanged -= OnByteChanged;
            NetHotspot.OnValueChanged -= OnHotspotChanged;
            NetClock.OnValueChanged -= OnClockChanged;
        }

        private void OnByteChanged(byte previous, byte current) => EventIsolation.Invoke(Changed);
        private void OnHotspotChanged(HotspotState previous, HotspotState current) => EventIsolation.Invoke(Changed);
        private void OnClockChanged(ClockAnchor previous, ClockAnchor current) => EventIsolation.Invoke(ClockChanged);

        // ------------------------------------------------------------------ host-only writes (no-ops with a LogOnce elsewhere)

        public void ServerSetSeason(Season s)
        {
            if (!CanWrite("season")) return;
            if ((byte)s > (byte)Season.Winter)
            {
                if (_log.ShouldLog("bad-season")) Debug.LogWarning("WorldState: season " + (byte)s + " is not a season; ignored.", this);
                return;
            }
            if (NetSeason.Value != (byte)s) NetSeason.Value = (byte)s;
        }

        // None is ignored; masks with several bits are accepted (M5 Storm | Rain).
        public void ServerSetWeather(WeatherMask w)
        {
            if (!CanWrite("weather")) return;
            if (w == WeatherMask.None)
            {
                if (_log.ShouldLog("weather-none")) Debug.LogWarning("WorldState: weather None ignored.", this);
                return;
            }
            if (NetWeather.Value != (byte)w) NetWeather.Value = (byte)w;
        }

        // An anchor with a non-finite or non-positive rate is ignored: a positive rate is the HasState invariant.
        public void ServerSetClock(in ClockAnchorData a)
        {
            if (!CanWrite("clock")) return;
            if (float.IsNaN(a.MinutesPerSecond) || float.IsInfinity(a.MinutesPerSecond) || !(a.MinutesPerSecond > 0f))
            {
                if (_log.ShouldLog("clock-rate")) Debug.LogWarning("WorldState: clock anchor with a non-positive rate ignored.", this);
                return;
            }
            ClockAnchor wire = ClockAnchor.From(in a);
            if (!NetClock.Value.Equals(wire)) NetClock.Value = wire;
        }

        public void ServerSetHotspot(in HotspotData h)
        {
            if (!CanWrite("hotspot")) return;
            HotspotState wire = HotspotState.From(in h);
            if (!NetHotspot.Value.Equals(wire)) NetHotspot.Value = wire;
        }

        private bool CanWrite(string what)
        {
            if (IsServer && IsSpawned) return true;
            if (_log.ShouldLog("write-" + what))
                Debug.LogWarning("WorldState: " + what + " written while not the spawned server; ignored (clients are read-only).", this);
            return false;
        }
    }
}
