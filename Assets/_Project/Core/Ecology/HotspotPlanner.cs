// Core/Ecology/HotspotPlanner.cs: spec 12.7 hotspot schedule. Host plain class, one per (day, spot); DevWorldDriver owns it.
// rng = Rng.Stream(runSeed, "hotspot", day). Draws per spawn, in order (M3-D49): (1) the delay, drawn when the spawn is
// scheduled (first: Range(firstMin, firstMax + 1) from minute 0; later: Range(gapMin, gapMax + 1) after the previous end),
// (2) the cell index, (3) the duration Range(min, max + 1), both drawn when the spawn happens. ForceAt draws nothing.
// Schedule-driven (B-D10): a hotspot starts at its scheduled minute whatever the step size; Advance is monotonic.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class HotspotPlanner
    {
        private const int MaxTiming = 100000;

        // T14 hardening (T5-T8 review): Advance clamps its minute here, a day plus a day of margin, so a huge minute (int.MaxValue) can
        // neither spin through millions of spawn cycles nor overflow EndMinute. DevWorldDriver passes 0..1199.
        internal const int MaxAdvanceMinute = DayClock.DayLengthMinutes * 2;
        private readonly EcologyTuning _t;
        private readonly ShoreGrid _grid;
        private readonly List<int> _cells = new List<int>();
        private Pcg32 _rng;

        public HotspotPlanner(in EcologyTuning tuning, ShoreGrid grid, ulong runSeed, int day)
        {
            Validate(in tuning);
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            _t = tuning;
            _grid = grid;
            _rng = Rng.Stream(runSeed, EcologyStreams.Hotspot, (ulong)day);
            if (grid != null) grid.CollectReachableWaterCells(_cells);
            NextSpawnMinute = _cells.Count > 0 ? Draw(_t.HotspotFirstMinMin, _t.HotspotFirstMinMax) : int.MaxValue;   // (1)
        }

        public HotspotData Current { get; private set; }
        public int NextSpawnMinute { get; private set; }
        internal int DrawCount { get; private set; }

        public bool Advance(int minute)
        {
            if (minute > MaxAdvanceMinute) minute = MaxAdvanceMinute;   // T14 hardening: bounded loop, no EndMinute overflow
            HotspotData before = Current;
            while (true)
            {
                HotspotData c = Current;
                if (c.Active)
                {
                    if (minute < c.EndMinute) break;
                    Current = default;                                                     // ended at EndMinute
                    NextSpawnMinute = _cells.Count > 0 ? c.EndMinute + Draw(_t.HotspotGapMin, _t.HotspotGapMax) : int.MaxValue;   // (1)
                }
                else
                {
                    if (NextSpawnMinute == int.MaxValue || minute < NextSpawnMinute) break;
                    int cell = _cells[Draw(0, _cells.Count - 1)];                         // (2)
                    int duration = Draw(_t.HotspotDurationMin, _t.HotspotDurationMax);    // (3)
                    _grid.CellCentre(cell % _grid.Width, cell / _grid.Width, out float x, out float z);
                    Current = new HotspotData { Active = true, X = x, Z = z, Radius = _t.HotspotRadiusM,
                                                StartMinute = NextSpawnMinute, EndMinute = NextSpawnMinute + duration };
                    NextSpawnMinute = int.MaxValue;
                }
            }
            return !Same(in before, Current);
        }

        // Dev "spawn hotspot here": replaces Current (and any pending spawn), duration = DurationMax, no draws.
        public HotspotData ForceAt(float x, float z, int minute)
        {
            Current = new HotspotData { Active = true, X = x, Z = z, Radius = _t.HotspotRadiusM,
                                        StartMinute = minute, EndMinute = minute + _t.HotspotDurationMax };
            NextSpawnMinute = int.MaxValue;
            return Current;
        }

        public static bool Contains(in HotspotData h, float x, float z)
        {
            if (!h.Active) return false;
            double dx = (double)x - h.X, dz = (double)z - h.Z;
            return dx * dx + dz * dz <= (double)h.Radius * h.Radius;
        }

        private int Draw(int minInclusive, int maxInclusive)
        {
            DrawCount++;
            return _rng.Range(minInclusive, maxInclusive + 1);
        }

        private static bool Same(in HotspotData a, HotspotData b)
        {
            return a.Active == b.Active && a.X == b.X && a.Z == b.Z && a.Radius == b.Radius
                   && a.StartMinute == b.StartMinute && a.EndMinute == b.EndMinute;
        }

        private static void Validate(in EcologyTuning t)
        {
            bool ok = t.HotspotFirstMinMin >= 0 && t.HotspotFirstMinMax >= t.HotspotFirstMinMin && t.HotspotFirstMinMax <= MaxTiming
                      && t.HotspotGapMin >= 0 && t.HotspotGapMax >= t.HotspotGapMin && t.HotspotGapMax <= MaxTiming
                      && t.HotspotDurationMin >= 1 && t.HotspotDurationMax >= t.HotspotDurationMin && t.HotspotDurationMax <= MaxTiming
                      && t.HotspotRadiusM > 0f && !float.IsInfinity(t.HotspotRadiusM);
            if (!ok) throw new ArgumentException("EcologyTuning hotspot timings or radius are out of range.", nameof(t));
        }
    }
}
