// Core, no engine references. Which action maps are enabled for a context (spec 8.1). Boat is reserved for M6.
using System;

namespace FishGame.Core
{
    [Flags]
    public enum InputMaps : byte
    {
        None = 0,
        Player = 1,
        Fishing = 2,
        Boat = 4,
        UI = 8
    }
}
