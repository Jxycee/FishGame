// Tests/EditMode/DevWorldTests.cs - T14 extra (CCR-D4, M3-D58): the dev clock's re-anchoring decisions (DevClock, DevWorldRules) and the
// session-level run state holder (EcologySession). Nothing places a WorldState before T16, so these pure parts carry the T14 proof.
// Base rate DayClock.DefaultMinutesPerSecond (1200 / 900 = 1.3333 min/s); times are NetworkTime seconds.
using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DevWorldTests
    {
        const float Rate = DayClock.DefaultMinutesPerSecond;

        [TearDown]
        public void TearDown()
        {
            EcologySession.Clear();
            ServiceRoots.SetApp(null);
        }

        static DevClock Started(double t, float minute)
        {
            var c = new DevClock(Rate);
            c.StartAt(t, minute);
            return c;
        }

        static void AssertSameAnchor(in ClockAnchorData want, in ClockAnchorData got, string why)
        {
            Assert.That(got.AnchorServerTime, Is.EqualTo(want.AnchorServerTime), why + " time");
            Assert.That(got.AnchorMinute, Is.EqualTo(want.AnchorMinute), why + " minute");
            Assert.That(got.MinutesPerSecond, Is.EqualTo(want.MinutesPerSecond), why + " rate");
            Assert.That(got.Running, Is.EqualTo(want.Running), why + " running");
        }

        // Catches: pause written as rate 0 with Running true (the HasState invariant breaks), or a resume that jumps (not re-anchored).
        [Test]
        public void DevClock_Pause_FreezesMinuteAndResumeIsContinuous()
        {
            DevClock c = Started(100.0, 0f);
            Assert.That(c.SetPaused(130.0, true), Is.True, "pausing a running clock writes the anchor");
            Assert.That(c.MinuteAt(130.0), Is.EqualTo(40f).Within(1e-3f));
            Assert.That(c.Anchor.Running, Is.False, "R34: pause is Running = false");
            Assert.That(c.Anchor.MinutesPerSecond, Is.GreaterThan(0f), "the rate is never 0");
            Assert.That(c.MinuteAt(200.0), Is.EqualTo(40f).Within(1e-3f), "frozen while paused");
            Assert.That(c.SetPaused(200.0, false), Is.True);
            Assert.That(c.MinuteAt(200.0), Is.EqualTo(40f).Within(1e-3f), "resume is continuous");
            Assert.That(c.MinuteAt(230.0), Is.EqualTo(80f).Within(1e-3f));
            Assert.That(c.SetPaused(230.0, false), Is.False, "no change, no write");
        }

        // Catches: a scrub that jumps the minute on press or release, or a multiplier other than x60 (M3-D32).
        [Test]
        public void DevClock_Scrub_SixtyTimesRateAndContinuous()
        {
            DevClock c = Started(0.0, 0f);
            float before = c.MinuteAt(10.0);
            Assert.That(before, Is.EqualTo(13.333f).Within(1e-2f));
            Assert.That(c.SetScrub(10.0, true), Is.True);
            Assert.That(c.MinuteAt(10.0), Is.EqualTo(before).Within(1e-3f), "continuous on press");
            Assert.That(c.Anchor.MinutesPerSecond, Is.EqualTo(Rate * 60f).Within(1e-4f));
            Assert.That(c.EffectiveRate, Is.EqualTo(Rate * 60f).Within(1e-4f));
            float during = c.MinuteAt(11.0);
            Assert.That(during, Is.EqualTo(before + 80f).Within(1e-2f), "one second at x60 = 80 minutes");
            Assert.That(c.SetScrub(11.0, false), Is.True);
            Assert.That(c.MinuteAt(11.0), Is.EqualTo(during).Within(1e-3f), "continuous on release");
            Assert.That(c.Anchor.MinutesPerSecond, Is.EqualTo(Rate).Within(1e-6f));
        }

        // Catches: a toggle that leaves the clock stopped before and after still writing an anchor (wire churn while frozen).
        [Test]
        public void DevClock_ToggleWhileStopped_WritesNothing()
        {
            DevClock c = Started(0.0, 100f);
            Assert.That(c.SetRunning(5.0, false), Is.True, "stopping a running clock writes once");
            ClockAnchorData stopped = c.Anchor;
            Assert.That(c.SetPaused(6.0, true), Is.False, "pause while dev-stopped");
            Assert.That(c.SetScrub(7.0, true), Is.False, "scrub press while stopped");
            Assert.That(c.SetScrub(8.0, false), Is.False, "scrub release while stopped");
            AssertSameAnchor(in stopped, c.Anchor, "unchanged");
            Assert.That(c.MinuteAt(100.0), Is.EqualTo(100f + 5f * Rate).Within(1e-3f), "still the stopped minute");
        }

        // Catches: auto new day while stopped or paused at 1200 (M3-D11: a stopped clock at 1200 stays there, casting closed), or one minute early.
        [Test]
        public void DevClock_AutoNewDay_OnlyWhileEffectivelyRunningAt1200()
        {
            DevClock running = Started(0.0, 1190f);
            Assert.That(running.ShouldAutoNewDay(7.0), Is.False, "1199.3");
            Assert.That(running.ShouldAutoNewDay(8.0), Is.True, "1200 reached");

            var stopped = new DevClock(Rate);
            stopped.SetRunning(0.0, false);
            stopped.StartAt(0.0, 1200f);
            Assert.That(stopped.ShouldAutoNewDay(100.0), Is.False, "a stopped clock at 1200 stays there");

            var paused = new DevClock(Rate);
            paused.SetPaused(0.0, true);
            paused.StartAt(0.0, 1200f);
            Assert.That(paused.ShouldAutoNewDay(100.0), Is.False, "paused at 1200");
        }

        // Catches: StartAt not clamping (a NaN or 5000 minute on the wire) or resetting the run/pause/scrub flags.
        [Test]
        public void DevClock_StartAt_ClampsAndKeepsRunState()
        {
            var c = new DevClock(Rate);
            c.SetScrub(0.0, true);
            c.SetRunning(0.0, false);
            c.SetPaused(0.0, true);
            c.StartAt(1.0, -5f);
            Assert.That(c.MinuteAt(1.0), Is.EqualTo(0f));
            c.StartAt(1.0, 5000f);
            Assert.That(c.MinuteAt(1.0), Is.EqualTo(1200f));
            c.StartAt(1.0, float.NaN);
            Assert.That(c.MinuteAt(1.0), Is.EqualTo(0f));
            Assert.That(c.DevRunning, Is.False);
            Assert.That(c.Paused, Is.True);
            Assert.That(c.Scrub, Is.True);
            Assert.That(DevClock.ClampMinute(float.PositiveInfinity), Is.EqualTo(1200f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DevClock(0f), "a zero base rate is refused");
            Assert.Throws<ArgumentOutOfRangeException>(() => new DevClock(float.NaN));
        }

        // Catches: any operation writing a zero rate (WorldState.HasState reads a positive rate as "the host has written the anchor").
        [Test]
        public void DevClock_Rate_NeverZeroAfterAnyOperation()
        {
            var c = new DevClock(Rate);
            double t = 0.0;
            Action[] steps =
            {
                () => c.StartAt(t, 0f), () => c.SetPaused(t, true), () => c.SetScrub(t, true), () => c.SetPaused(t, false),
                () => c.SetRunning(t, false), () => c.SetScrub(t, false), () => c.StartAt(t, 1200f), () => c.SetRunning(t, true),
                () => c.SetScrub(t, true), () => c.StartAt(t, 600f), () => c.SetPaused(t, true), () => c.SetRunning(t, false),
                () => c.SetPaused(t, false), () => c.SetRunning(t, true), () => c.SetScrub(t, false), () => c.StartAt(t, float.NaN),
            };
            for (int i = 0; i < steps.Length; i++)
            {
                t += 3.5;
                steps[i]();
                Assert.That(c.Anchor.MinutesPerSecond, Is.GreaterThan(0f), "step " + i);
            }
        }

        // Catches: GetOrCreate replacing a live run state (a Spot load would reset the day, cast counter and catch ledger, M3-D17).
        [Test]
        public void EcologySession_GetOrCreate_ReturnsExistingAndIgnoresSeed()
        {
            EcologySession.Clear();
            EcologyRunState a = EcologySession.GetOrCreate(1);
            Assert.That(EcologySession.GetOrCreate(2), Is.SameAs(a));
            Assert.That(a.RunSeed, Is.EqualTo(1UL));
            Assert.That(EcologySession.Current, Is.SameAs(a));
        }

        // Catches: a run state that starts on another day or a fixed season instead of the rolled one (M3-D10).
        [Test]
        public void EcologySession_NewRunState_SeasonFromSeedDayOne()
        {
            foreach (ulong seed in new ulong[] { 3, 42, FishingConstants.DefaultDevRunSeed })
            {
                EcologySession.Clear();
                EcologyRunState r = EcologySession.GetOrCreate(seed);
                Assert.That(r.Season, Is.EqualTo(WeatherDirector.RollSeason(seed)), "seed " + seed);
                Assert.That(r.Day, Is.EqualTo(1));
                Assert.That(r.HostCastCounter, Is.EqualTo(0u));
            }
        }

        // Catches: Clear not dropping the instance (a dev seed change would keep the old run).
        [Test]
        public void EcologySession_Clear_NextGetOrCreateIsFresh()
        {
            EcologyRunState a = EcologySession.GetOrCreate(1);
            EcologySession.Clear();
            Assert.That(EcologySession.Current, Is.Null);
            EcologyRunState b = EcologySession.GetOrCreate(9);
            Assert.That(b, Is.Not.SameAs(a));
            Assert.That(b.RunSeed, Is.EqualTo(9UL));
        }

        // Catches: no stale-session guard (the next PlayMode test would inherit the previous counter, reservations and day), and no
        // unsubscribe/clear on the session's Ended.
        [Test]
        public void EcologySession_StaleSession_DroppedOnGetOrCreate()
        {
            try
            {
                ServiceRoots.SetApp(null);
                EcologySession.Clear();
                EcologyRunState a = EcologySession.GetOrCreate(1);   // no App: no session
                var app = new ServiceRegistry();
                var fake = new FakeSession();
                app.Register<INetworkSession>(fake);
                ServiceRoots.SetApp(app);
                EcologyRunState b = EcologySession.GetOrCreate(5);
                Assert.That(b, Is.Not.SameAs(a), "a run state from another (or no) session is dropped");
                Assert.That(b.RunSeed, Is.EqualTo(5UL));
                Assert.That(fake.EndedSubscribers, Is.EqualTo(1), "subscribed to the session's Ended");
                Assert.That(EcologySession.GetOrCreate(6), Is.SameAs(b), "same session: kept");
                fake.RaiseEnded();
                Assert.That(EcologySession.Current, Is.Null, "the session end clears the run state");
                Assert.That(fake.EndedSubscribers, Is.EqualTo(0), "and unsubscribes");
            }
            finally
            {
                ServiceRoots.SetApp(null);
            }
        }

        // Catches: a Spot load that rebuilds a different weather track (the track must be a pure function of seed, season and table).
        [Test]
        public void DevWorld_MapSwitchResume_RebuiltTrackIdentical()
        {
            WeatherTableData table = EcologyCoreTestData.Weather();
            var first = new WeatherDirector(in table, Season.Autumn, 7);
            first.EnsureDay(3);
            var rebuilt = new WeatherDirector(in table, Season.Autumn, 7);
            foreach (int day in new[] { 3, 9 })
                foreach (int m in new[] { 0, 240, 480, 720, 960 })
                    Assert.That(rebuilt.At(day, m), Is.EqualTo(first.At(day, m)), "day " + day + " minute " + m);
        }

        // Catches: the dev panel setting an M5 weather (Storm, Heatwave) or a multi-bit mask in M3.
        [Test]
        public void DevWorld_WeatherOverride_OnlyNoneOrSingleM3Weather()
        {
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.None), Is.True);
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.Clear), Is.True);
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.Rain), Is.True);
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.Fog), Is.True);
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.Storm), Is.False);
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.Heatwave), Is.False);
            Assert.That(DevWorldRules.IsValidOverride(WeatherMask.Rain | WeatherMask.Fog), Is.False);
            Assert.That(DevWorldRules.LookupMinute(-3f), Is.EqualTo(0));
            Assert.That(DevWorldRules.LookupMinute(1200f), Is.EqualTo(1199));
            Assert.That(DevWorldRules.LookupMinute(417.9f), Is.EqualTo(417));
        }

        // Minimal INetworkSession: only Ended matters here.
        private sealed class FakeSession : INetworkSession
        {
            private Action<EndReason> _ended;
            public int EndedSubscribers => _ended == null ? 0 : _ended.GetInvocationList().Length;
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
