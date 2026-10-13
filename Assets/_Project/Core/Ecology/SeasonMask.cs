// Core/Ecology/SeasonMask.cs: which seasons a spawn rule allows (spec 12.3). Authority: none (plain data).
using System;

namespace FishGame.Core
{
    [Flags]
    public enum SeasonMask : byte { None = 0, Spring = 1, Summer = 2, Autumn = 4, Winter = 8, All = 15 }
}
