// Tests/EditMode/SpotEcologyTests.cs - T15 extra (CCR-D4, M3-D16): the shared grid-water / casting-open gates (SpotEcology), the world part of the
// host cast context, and the Luck debug setting. Pure parts only: a synthetic 6 x 6 grid and a fake IWorldContext; the real registry is used
// for the TryResolve and G9 deck-end checks.
using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class SpotEcologyTests
    {
        const float Cell = 1f;

        [TearDown]
        public void TearDown()
        {
            FishingDebugSettings.Luck = 0f;
            FishingDebugSettings.ForcedBiteWait = 0f;
            FishingDebugSettings.ClearOnSessionEnd(null);   // unhook a fake session left by the Luck test
        }

        // 6 x 6, cell 1 m, origin (0, 0): the left 3 columns are land, the right 3 water with shore distance 2, 7 and 12 m.
        static ShoreGrid Grid()
        {
            const int w = 6, h = 6;
            var dm = new ushort[w * h];
            for (int cz = 0; cz < h; cz++)
            {
                for (int cx = 0; cx < w; cx++)
                {
                    ushort v = EcologyConstants.ShoreDmNotWater;
                    if (cx == 3) v = 20;
                    else if (cx == 4) v = 70;
                    else if (cx == 5) v = 120;
                    dm[cz * w + cx] = v;
                }
            }
            int bytes = (w * h + 7) / 8;
            return new ShoreGrid(w, h, Cell, 0f, 0f, dm, new byte[bytes], new byte[bytes]);
        }

        static ZoneData Zone(ShoreGrid grid)
        {
            var bands = new[]
            {
                new SubAreaBandData { MinShoreM = 0f, MaxShoreM = 5f, JunkChance = 0.2f },
                new SubAreaBandData { MinShoreM = 5f, MaxShoreM = 10f, JunkChance = 0.12f },
                new SubAreaBandData { MinShoreM = 10f, MaxShoreM = float.PositiveInfinity, JunkChance = 0.1f },
            };
            WeatherTableData weather = EcologyCoreTestData.Weather();
            return new ZoneData(bands, new[] { EcologyTestData.Rule(0) }, EcologyTestData.JunkFirst(), in weather, grid);
        }

        sealed class FakeWorld : IWorldContext
        {
            public bool HasStateValue = true, CastingOpenValue = true;
            public int MinuteValue = 300;
            public Season SeasonValue = Season.Summer;
            public WeatherMask WeatherValue = WeatherMask.Clear;
            public string SpotIdValue = ContentIds.LakeDock;
            public float HotX = 4.5f, HotZ = 2.5f, HotRadius = 1f;

            public Season Season => SeasonValue;
            public WeatherMask Weather => WeatherValue;
            public int Minute => MinuteValue;
            public bool CastingOpen => HasStateValue && CastingOpenValue;
            public bool HasState => HasStateValue;
            public string SpotId => SpotIdValue;
            public bool IsInHotspot(float x, float z) => HasStateValue && (x - HotX) * (x - HotX) + (z - HotZ) * (z - HotZ) <= HotRadius * HotRadius;
            public event Action Changed { add { } remove { } }
        }

        // The host's view terms as FishingHost.HandleCastRequest builds them (CastingAllowed from the world clock, GridWater from SpotEcology),
        // run through the production validator.
        static CastRejectReason HostVerdict(FakeWorld world, ZoneData zone, float x, float z)
        {
            var req = new CastRequestData { CastId = 1, ChargeNorm = 0.5f, YawDeg = 0f, OriginX = 0f, OriginZ = 0f, BaitIndex = 0 };
            var view = new HostCastView
            {
                PlayerX = 0f, PlayerY = 0f, PlayerZ = 0f, CastingAllowed = world.CastingOpen,
                GridWater = SpotEcology.GridWater(zone, x, z),
                Probe = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 3f },
            };
            return CatchValidator.ValidateCast(in req, 0, false, in view, true, 0.5f);
        }

        // The owner's marker as FishingRodController.ProbeMarker combines it: the physics probe, then the production SpotEcology.CastGatesOpen
        // (the function OwnerWorldGates returns).
        static bool OwnerMarker(bool probeOk, FakeWorld world, ZoneData zone, float x, float z)
        {
            return probeOk && SpotEcology.CastGatesOpen(world, zone, x, z);
        }

        // Catches: SpotEcology.CastGatesOpen (the owner's gate function) dropping the grid-water or the casting-open term, or using a different
        // lookup than the host's GridWater (for example the cell centre or a rounded cell), so that an honest cast at a grid edge is
        // host-rejected (C 2.8 #26). It does not prove FishingRodController calls CastGatesOpen (the PlayMode day-over test covers the casting-open wiring only; the manual dock-shadow marker check covers the grid term).
        [Test]
        public void SpotEcology_OwnerAndHostGates_AgreeAcrossGridEdge()
        {
            ZoneData zone = Zone(Grid());
            var world = new FakeWorld();
            foreach (bool open in new[] { true, false })
            {
                world.CastingOpenValue = open;
                for (int i = 0; i <= 200; i++)
                {
                    float x = 2f + i / 100f;
                    bool owner = OwnerMarker(true, world, zone, x, 2.5f);
                    bool host = HostVerdict(world, zone, x, 2.5f) == CastRejectReason.None;
                    Assert.That(owner, Is.EqualTo(host), "x=" + x + " open=" + open);
                }
                Assert.That(OwnerMarker(true, world, zone, 2.999f, 2.5f), Is.False, "2.999 is the last land cell (open=" + open + ")");
                Assert.That(OwnerMarker(true, world, zone, 3.0f, 2.5f), Is.EqualTo(open), "3.0 is the first water cell (open=" + open + ")");
            }
            world.CastingOpenValue = true;
            Assert.That(HostVerdict(world, zone, 2.999f, 2.5f), Is.EqualTo(CastRejectReason.NotWater));
            Assert.That(HostVerdict(world, zone, 3.0f, 2.5f), Is.EqualTo(CastRejectReason.None));
            Assert.That(SpotEcology.CastGatesOpen(null, zone, 3.5f, 2.5f), Is.False, "no world: closed");
            Assert.That(SpotEcology.CastGatesOpen(world, null, 3.5f, 2.5f), Is.False, "no zone: closed");
            world.CastingOpenValue = false;
            Assert.That(HostVerdict(world, zone, 3.0f, 2.5f), Is.EqualTo(CastRejectReason.NotAllowedNow), "casting closed is checked first");
            Assert.That(HostVerdict(world, zone, 1f, 2.5f), Is.EqualTo(CastRejectReason.NotAllowedNow), "even over land");
            Assert.That(SpotEcology.GridWater(zone, 7f, 2.5f), Is.False, "outside the grid is not water");
            Assert.That(SpotEcology.GridWater(null, 3.5f, 2.5f), Is.False, "no zone is not water");
        }

        // Catches: a swapped field, a default (invalid) Events or Skills, an unclamped Luck, and a sub-area taken from the wrong distance.
        [Test]
        public void SpotEcology_WorldContext_MapsWorldValuesAndSubArea()
        {
            ZoneData zone = Zone(Grid());
            var world = new FakeWorld { MinuteValue = 777, SeasonValue = Season.Winter, WeatherValue = WeatherMask.Rain };

            FishingContext shallow = SpotEcology.WorldContext(world, zone, 4, 3.5f, 2.5f, 0.25f, 3);   // shore 2 m
            FishingContext mid = SpotEcology.WorldContext(world, zone, 4, 4.5f, 2.5f, 0.25f, 3);       // shore 7 m
            FishingContext far = SpotEcology.WorldContext(world, zone, 4, 5.5f, 2.5f, 0.25f, 3);       // shore 12 m
            Assert.That(shallow.SubAreaIndex, Is.EqualTo(0));
            Assert.That(mid.SubAreaIndex, Is.EqualTo(1));
            Assert.That(far.SubAreaIndex, Is.EqualTo(2));

            Assert.That(mid.ZoneIndex, Is.EqualTo(4));
            Assert.That(mid.Minute, Is.EqualTo(777));
            Assert.That(mid.Season, Is.EqualTo(Season.Winter));
            Assert.That(mid.Weather, Is.EqualTo(WeatherMask.Rain));
            Assert.That(mid.PlayersInSession, Is.EqualTo(3));
            Assert.That(mid.Luck, Is.EqualTo(0.25f));
            Assert.That(mid.Events.IsValid && mid.Skills.IsValid, Is.True, "a default Events or Skills makes the resolver throw");
            Assert.That(mid.InHotspot, Is.True, "(4.5, 2.5) is the hotspot centre");
            Assert.That(SpotEcology.WorldContext(world, zone, 4, 5.5f, 5.5f, 0f, 1).InHotspot, Is.False);

            Assert.That(SpotEcology.WorldContext(world, zone, 4, 4.5f, 2.5f, 3f, 1).Luck, Is.EqualTo(1f));
            Assert.That(SpotEcology.WorldContext(world, zone, 4, 4.5f, 2.5f, -3f, 1).Luck, Is.EqualTo(-1f));
            Assert.That(SpotEcology.WorldContext(world, zone, 4, 4.5f, 2.5f, float.NaN, 1).Luck, Is.EqualTo(0f));
            Assert.That(SpotEcology.WorldContext(world, zone, 4, 4.5f, 2.5f, 0f, 0).PlayersInSession, Is.EqualTo(1));
            Assert.That(SpotEcology.WorldContext(world, zone, 4, 4.5f, 2.5f, 0f, -2).PlayersInSession, Is.EqualTo(1));

            FishingContext land = SpotEcology.WorldContext(world, zone, 4, 1f, 2.5f, 0f, 1);   // not water: the last band, GridWater rejects anyway
            Assert.That(land.SubAreaIndex, Is.EqualTo(2));
        }

        static ContentRegistry LoadRegistry()
        {
            var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentRegistry>(FishGame.EditorTools.AssetPaths.ContentRegistryAsset);
            Assert.That(registry, Is.Not.Null, "missing " + FishGame.EditorTools.AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            return registry;
        }

        // Catches: resolving without HasState (a client marker valid before the first sync), an unknown spot resolving to the first zone,
        // and a null registry throwing instead of closing the gate.
        [Test]
        public void SpotEcology_TryResolve_FalseWithoutStateSpotOrGrid()
        {
            ContentRegistry registry = LoadRegistry();
            var world = new FakeWorld();
            Assert.That(SpotEcology.TryResolve(registry, world, out int zoneIndex, out ZoneData zone), Is.True, "lake_dock with state resolves");
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.Grid, Is.Not.Null, "the lake zone has its baked grid");
            Assert.That(zoneIndex, Is.EqualTo(registry.ZoneIndex(ContentIds.LakeDock)));

            world.HasStateValue = false;
            Assert.That(SpotEcology.TryResolve(registry, world, out zoneIndex, out zone), Is.False, "no state");
            Assert.That(zone, Is.Null);
            Assert.That(zoneIndex, Is.EqualTo(-1));

            world.HasStateValue = true;
            world.SpotIdValue = "no_such_spot";
            Assert.That(SpotEcology.TryResolve(registry, world, out _, out _), Is.False, "unknown spot id");
            world.SpotIdValue = "";
            Assert.That(SpotEcology.TryResolve(registry, world, out _, out _), Is.False, "empty spot id");
            world.SpotIdValue = ContentIds.LakeDock;
            Assert.That(SpotEcology.TryResolve(null, world, out _, out _), Is.False, "no registry");
            Assert.That(SpotEcology.TryResolve(registry, null, out _, out _), Is.False, "no world");
        }

        // Catches: an unbaked grid (ZoneData.Grid null), an out-of-range spot index and a missing database resolving to a zone, which would let
        // the owner marker and the host skip the grid gate instead of closing casting.
        [Test]
        public void SpotEcology_ResolveZone_FalseForNullGridAndBadIndex()
        {
            EcologyDatabase noGrids = EcologyTestData.Database();   // its zones are built with a null grid; spot 0 is the lake
            Assert.That(noGrids.Zones[noGrids.Spots[0].ZoneIndex].Grid, Is.Null, "precondition: the fixture zone has no grid");
            Assert.That(SpotEcology.ResolveZone(noGrids, 0, out int zoneIndex, out ZoneData zone), Is.False, "null grid");
            Assert.That(zone, Is.Null);
            Assert.That(zoneIndex, Is.EqualTo(-1));
            Assert.That(SpotEcology.ResolveZone(noGrids, 1, out _, out _), Is.False, "spot index past the end");
            Assert.That(SpotEcology.ResolveZone(noGrids, -1, out _, out _), Is.False, "negative spot index");
            Assert.That(SpotEcology.ResolveZone(null, 0, out _, out _), Is.False, "no database");

            ContentRegistry registry = LoadRegistry();
            Assert.That(SpotEcology.ResolveZone(registry.Ecology, registry.SpotIndex(ContentIds.LakeDock), out _, out ZoneData lake), Is.True, "the baked lake");
            Assert.That(lake.Grid, Is.Not.Null);
        }

        // Catches: the new world gates closing the M2 deck-end casts (the baked grid calling the dock shadow land): every PlayMode cast test
        // casts from (0, -41) facing +z with a short charge (G9) and must land on grid water, on the real baked asset.
        [Test]
        public void SpotEcology_G9DeckEndCasts_LandOnGridWater()
        {
            ContentRegistry registry = LoadRegistry();
            Assert.That(SpotEcology.TryResolve(registry, new FakeWorld(), out _, out ZoneData zone), Is.True);
            FishingDatabase db = registry.Fishing;
            RodStats rod = db.Rods[registry.RodIndex("rod_handmade")];
            for (int i = 0; i <= 10; i++)
            {
                float charge = 0.1f + i * 0.05f;   // the PlayMode Charge helper stops between 0.1 and 0.6
                float dist = CastMath.Distance(charge, db.Tuning.CastMinDist, rod.CastMaxDist);
                CastMath.Landing(0f, -41f, 0f, dist, out float lx, out float lz);
                Assert.That(SpotEcology.GridWater(zone, lx, lz), Is.True, "charge " + charge + " lands at (" + lx + ", " + lz + ")");
            }
        }

        // Catches: the clamp missing, NaN kept, and the session-end reset forgetting Luck (a dev override must not outlive its session).
        [Test]
        public void FishingDebugSettings_Luck_ClampedAndHonouredAsSet()
        {
            FishingDebugSettings.Luck = 0.5f;
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(0.5f));
            FishingDebugSettings.Luck = 2f;
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(1f));
            FishingDebugSettings.Luck = -7f;
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(-1f));
            FishingDebugSettings.Luck = float.NaN;
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(0f));

            var session = new FakeSession();
            FishingDebugSettings.ClearOnSessionEnd(session);
            FishingDebugSettings.Luck = 0.75f;
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            session.RaiseEnded();
            Assert.That(FishingDebugSettings.Luck, Is.EqualTo(0f), "the session end clears Luck");
            Assert.That(FishingDebugSettings.ForcedBiteWait, Is.EqualTo(0f), "and still F6");
        }

        sealed class FakeSession : INetworkSession
        {
            Action<EndReason> _ended;
            public void RaiseEnded() => _ended?.Invoke(EndReason.UserLeft);

            public ConnState State => default;
            public SessionMode Mode => default;
            public bool IsHost => true;
            public event Action<ConnState, ConnState> StateChanged { add { } remove { } }
            public event Action<EndReason> Ended { add { _ended += value; } remove { _ended -= value; } }
            public string JoinCode => null;
            public Task<string> HostOnlineAsync(CancellationToken ct) => Task.FromResult<string>(null);
            public Task JoinOnlineAsync(string code, CancellationToken ct) => Task.CompletedTask;
            public Task StartLocalAsync(bool allowRemote, CancellationToken ct) => Task.CompletedTask;
            public Task JoinDirectAsync(string ip, ushort port, CancellationToken ct) => Task.CompletedTask;
            public Task SetJoinableAsync(bool open) => Task.CompletedTask;
            public Task LeaveAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
