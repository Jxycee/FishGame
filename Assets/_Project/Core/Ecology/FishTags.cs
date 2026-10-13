// Core/Ecology/FishTags.cs: species tags (spec 12.2). Bait affinity tags use the same flags (spec 9.7 / 12.4). A plain enum in FishGame.Core.
using System;

namespace FishGame.Core
{
    [Flags]
    public enum FishTags : ushort
    {
        None = 0,
        Nocturnal = 1,
        Predator = 2,
        Bottom = 4,
        Schooling = 8,
        Surface = 16,
    }
}
