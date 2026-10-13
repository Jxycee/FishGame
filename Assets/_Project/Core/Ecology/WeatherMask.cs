// Core/Ecology/WeatherMask.cs: weather states as bits (spec 12.2). M3 produces only Clear, Rain and Fog; Storm and Heatwave are M5 extra
// bits (a Storm is also Rain, a Heatwave also Clear, so a rule that allows Rain matches a Storm). Authority: none (plain data).
using System;

namespace FishGame.Core
{
    [Flags]
    public enum WeatherMask : byte { None = 0, Clear = 1, Rain = 2, Fog = 4, Storm = 8, Heatwave = 16, All = 31 }
}
