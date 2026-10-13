// Gameplay/World/DevClock.cs - the M3 dev clock's re-anchoring decisions and the dev world rules (CCR-D4, M3-D58).
// Authority: host only (a plain helper owned by DevWorldDriver). Pure: no Unity calls; time comes in as NetworkTime seconds.
// Three independent inputs: dev-running (panel/test), paused (solo IPauseState), scrub (F8 held). Effective running = devRunning && !paused;
// the scrub sets the rate. Every change re-anchors through ClockMath.Reanchor so the minute is continuous (14.2). The rate is never 0 (R34).
using System;
using FishGame.Core;

namespace FishGame.Gameplay
{
    internal sealed class DevClock
    {
        internal const float ScrubMultiplier = 60f;          // M3-D32: F8 held = x60
        private readonly float _baseRate;

        public DevClock(float baseRate)
        {
            if (!(baseRate > 0f) || float.IsInfinity(baseRate)) throw new ArgumentOutOfRangeException(nameof(baseRate));
            _baseRate = baseRate;
        }

        public ClockAnchorData Anchor { get; private set; }
        public bool DevRunning { get; private set; } = true;
        public bool Paused { get; private set; }
        public bool Scrub { get; private set; }
        public bool EffectiveRunning => DevRunning && !Paused;
        public float EffectiveRate => Scrub ? _baseRate * ScrubMultiplier : _baseRate;

        public float MinuteAt(double now) => ClockMath.MinuteAt(Anchor, now);

        // Session start, map-switch resume and new day: minute m now; keeps DevRunning/Paused/Scrub as they are.
        public void StartAt(double now, float minute) { Anchor = ClockMath.At(now, ClampMinute(minute), EffectiveRate, EffectiveRunning); }

        public bool SetPaused(double now, bool paused) { if (Paused == paused) return false; Paused = paused; return Reanchor(now); }
        public bool SetRunning(double now, bool running) { if (DevRunning == running) return false; DevRunning = running; return Reanchor(now); }
        public bool SetScrub(double now, bool held) { if (Scrub == held) return false; Scrub = held; return Reanchor(now); }

        // A running clock that reached 1200 starts the next day (M3-D11). A clock stopped at 1200 stays there (casting closed).
        public bool ShouldAutoNewDay(double now) => EffectiveRunning && MinuteAt(now) >= DayClock.DayLengthMinutes;

        // True when the anchor on the wire must change. A toggle that leaves the clock stopped before and after (scrub while paused,
        // pause while dev-stopped) writes nothing: the frozen minute is identical and the next start re-anchors with the right rate.
        private bool Reanchor(double now)
        {
            ClockAnchorData next = ClockMath.Reanchor(Anchor, now, EffectiveRate, EffectiveRunning);
            if (!next.Running && !Anchor.Running) return false;
            Anchor = next;
            return true;
        }

        internal static float ClampMinute(float m) => float.IsNaN(m) ? 0f : (m < 0f ? 0f : (m > DayClock.DayLengthMinutes ? DayClock.DayLengthMinutes : m));
    }

    // Authority: none (pure rules used by the host's DevWorldDriver).
    internal static class DevWorldRules
    {
        // M3 override values: None (follow the track) or exactly Clear, Rain or Fog (the dev panel never sets Storm/Heatwave in M3).
        public static bool IsValidOverride(WeatherMask w) => w == WeatherMask.None || w == WeatherMask.Clear || w == WeatherMask.Rain || w == WeatherMask.Fog;

        // The minute used for band, segment and hotspot lookups: floor, clamped to 0..1199.
        public static int LookupMinute(float minuteF)
        {
            int m = (int)Math.Floor(minuteF);
            return m < 0 ? 0 : (m > DayClock.DayLengthMinutes - 1 ? DayClock.DayLengthMinutes - 1 : m);
        }
    }
}
