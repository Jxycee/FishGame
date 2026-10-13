// Core/Ecology/WeatherDirector.cs: spec 12.2 weather chain (R50). Host plain class; the track is a pure function of
// (table, season, runSeed), so DevWorldDriver rebuilds it on every Spot load (C 7.14).
// Planned track: rng = Rng.Stream(runSeed, "weather", 0); previous = Clear; 35 times previous = Next(previous) - so segment 0
// of day 1 is DRAWN from the Clear row. Day d >= 8: rng = Rng.Stream(runSeed, "weather", d), continuing from the previous segment.
// M3 produces only Clear, Rain, Fog (single bits).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class WeatherDirector
    {
        private const int MaxDay = 10000;
        private readonly WeatherTableData _table;
        private readonly Season _season;
        private readonly ulong _runSeed;
        private readonly List<WeatherMask> _track;

        public WeatherDirector(in WeatherTableData table, Season season, ulong runSeed)
        {
            Validate(in table);
            if ((byte)season > (byte)Season.Winter) throw new ArgumentOutOfRangeException(nameof(season));
            _table = new WeatherTableData { Base = (float[])table.Base.Clone(), SeasonColumnMult = (float[])table.SeasonColumnMult.Clone() };
            _season = season;
            _runSeed = runSeed;
            int planned = DayClock.PlannedDays * DayClock.SegmentsPerDay;
            _track = new List<WeatherMask>(planned);
            Pcg32 rng = Rng.Stream(runSeed, EcologyStreams.Weather, 0);
            WeatherMask previous = WeatherMask.Clear;
            for (int i = 0; i < planned; i++)
            {
                previous = Next(in _table, season, previous, ref rng);
                _track.Add(previous);
            }
            Track = _track.AsReadOnly();
        }

        public IReadOnlyList<WeatherMask> Track { get; }

        public WeatherMask At(int day, int minute)
        {
            EnsureDay(day);
            return _track[(day - 1) * DayClock.SegmentsPerDay + DayClock.SegmentAt(minute)];
        }

        public void EnsureDay(int day)
        {
            if (day < 1 || day > MaxDay) throw new ArgumentOutOfRangeException(nameof(day));
            while (_track.Count < day * DayClock.SegmentsPerDay)
            {
                int next = _track.Count / DayClock.SegmentsPerDay + 1;                 // the first missing day (>= 8), in order
                Pcg32 rng = Rng.Stream(_runSeed, EcologyStreams.Weather, (ulong)next);
                WeatherMask previous = _track[_track.Count - 1];
                for (int s = 0; s < DayClock.SegmentsPerDay; s++)
                {
                    previous = Next(in _table, _season, previous, ref rng);
                    _track.Add(previous);
                }
            }
        }

        public static void RowProbabilities(in WeatherTableData t, Season s, WeatherMask current, float[] into3)
        {
            if (into3 == null || into3.Length < 3) throw new ArgumentException("into3 needs 3 slots.", nameof(into3));
            Row(in t, s, current, out into3[0], out into3[1], out into3[2]);
        }

        // One NextFloat: [0, pClear) Clear, [pClear, pClear + pRain) Rain, else Fog.
        public static WeatherMask Next(in WeatherTableData t, Season s, WeatherMask current, ref Pcg32 rng)
        {
            Row(in t, s, current, out float pClear, out float pRain, out float _);
            float u = rng.NextFloat();
            if (u < pClear) return WeatherMask.Clear;
            if (u < pClear + pRain) return WeatherMask.Rain;
            return WeatherMask.Fog;
        }

        // Same formula as the EcologyRunState constructor (B-D8): one draw.
        public static Season RollSeason(ulong runSeed)
        {
            Pcg32 rng = Rng.Stream(runSeed, EcologyStreams.Season, 0);
            return (Season)rng.Range(0, 4);
        }

        // Row of the current weather: base x the season's column multipliers, then renormalised (spec 12.2). No allocation.
        private static void Row(in WeatherTableData t, Season s, WeatherMask current, out float clear, out float rain, out float fog)
        {
            if (t.Base == null || t.Base.Length != 9 || t.SeasonColumnMult == null || t.SeasonColumnMult.Length != 12)
                throw new ArgumentException("The weather table needs Base[9] and SeasonColumnMult[12].");
            if ((byte)s > (byte)Season.Winter) throw new ArgumentOutOfRangeException(nameof(s));
            int row = EcologyMasks.WeatherColumn(current) * 3, col = (int)s * 3;
            double a = (double)t.Base[row] * t.SeasonColumnMult[col];
            double b = (double)t.Base[row + 1] * t.SeasonColumnMult[col + 1];
            double c = (double)t.Base[row + 2] * t.SeasonColumnMult[col + 2];
            double sum = a + b + c;
            if (!(sum > 0.0) || double.IsInfinity(sum)) throw new ArgumentException("A weather row must sum to a finite value > 0.");
            clear = (float)(a / sum); rain = (float)(b / sum); fog = (float)(c / sum);
        }

        // B-D11: Base 9 finite entries >= 0 with every row sum > 0; SeasonColumnMult 12 finite entries > 0.
        private static void Validate(in WeatherTableData t)
        {
            if (t.Base == null || t.Base.Length != 9 || t.SeasonColumnMult == null || t.SeasonColumnMult.Length != 12)
                throw new ArgumentException("The weather table needs Base[9] and SeasonColumnMult[12].");
            for (int i = 0; i < 9; i++)
                if (!(t.Base[i] >= 0f) || float.IsInfinity(t.Base[i])) throw new ArgumentException("Base entries must be finite and >= 0.");
            for (int r = 0; r < 3; r++)
                if (!(t.Base[r * 3] + t.Base[r * 3 + 1] + t.Base[r * 3 + 2] > 0f)) throw new ArgumentException("Every Base row must sum to > 0.");
            for (int i = 0; i < 12; i++)
                if (!(t.SeasonColumnMult[i] > 0f) || float.IsInfinity(t.SeasonColumnMult[i])) throw new ArgumentException("Season multipliers must be finite and > 0.");
        }
    }
}
