using System;

namespace FishGame.Data
{
    // Content data only (not networked). The ambience parameter set of one season (index = Season; spec 11.7, M3-D22). windGain multiplies the
    // Wind_Ambient loop's base volume (1 = the M2 value); windLfoHz is the rate of a slow sine wobble on that gain (0 = steady); lapIntervalMin
    // and lapIntervalMax are the Water_Lap interval bounds in seconds (the M2 AmbienceBed constants are 0.6 and 1.4). Public camelCase
    // fields (M3-D76). Owned by T9 (M3); read by T19.
    [Serializable]
    public struct SeasonAmbience
    {
        public float windGain, windLfoHz, lapIntervalMin, lapIntervalMax;
    }
}
