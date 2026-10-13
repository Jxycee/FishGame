// Core/Ecology/TimeBandMask.cs: the six time bands of the 20-minute day (spec 12.2, R1). Flags so a rule can allow several bands.
// Authority: none (plain data).
using System;

namespace FishGame.Core
{
    [Flags]
    public enum TimeBandMask : byte { None = 0, Dawn = 1, Morning = 2, Midday = 4, Afternoon = 8, Dusk = 16, Night = 32, All = 63 }
}
