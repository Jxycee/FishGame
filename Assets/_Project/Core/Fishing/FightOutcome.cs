// Core/Fishing/FightOutcome.cs: how a fight ended (spec 9.5, C 7.5).
namespace FishGame.Core
{
    public enum FightOutcome : byte { None = 0, Landed = 1, Snapped = 2, Escaped = 3, Cancelled = 4 }
}
