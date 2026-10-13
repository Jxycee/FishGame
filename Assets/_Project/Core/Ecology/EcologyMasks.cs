// Core/Ecology/EcologyMasks.cs: enum-to-mask helpers (C 7.5). Pure; authority: none.
using System;

namespace FishGame.Core
{
    public static class EcologyMasks
    {
        // Spring..Winter -> 1, 2, 4, 8; anything else throws.
        public static SeasonMask Of(Season s)
        {
            if ((byte)s > (byte)Season.Winter) throw new ArgumentOutOfRangeException(nameof(s));
            return (SeasonMask)(1 << (int)s);
        }

        // Markov matrix column of a single weather bit: Clear 0, Rain 1, Fog 2; combined bits, None, Storm and Heatwave throw.
        public static int WeatherColumn(WeatherMask single)
        {
            switch (single)
            {
                case WeatherMask.Clear: return 0;
                case WeatherMask.Rain: return 1;
                case WeatherMask.Fog: return 2;
                default: throw new ArgumentException("WeatherColumn needs exactly Clear, Rain or Fog, got " + single + ".", nameof(single));
            }
        }
    }
}
