using System;

namespace FishGame.Data
{
    // Content data only (not networked). The look of one weather (index Clear 0, Rain 1, Fog 2 in ArtPaletteDef.weatherLooks): fog density =
    // max(phase fog x fogDensityMult, fogDensityMin), capped to stay readable at 25 m; light intensity x lightMult; colours desaturated by
    // desaturate; rainAmount drives the rain particles and the Rain_Patter loop. Public camelCase fields (M3-D76). Owned by T9 (M3); read by T18/T19.
    [Serializable]
    public struct WeatherAmbient
    {
        public float fogDensityMult, fogDensityMin, lightMult, desaturate, rainAmount;
    }
}
