// Core/Fishing/RodCue.cs: every feedback moment of spec 9.11 (C 7.5).
// Owner raises from RodStateMachine effects; remotes from RodEventRpc and NetRodState transitions.
namespace FishGame.Core
{
    public enum RodCue : byte
    {
        None = 0, Equip = 1, Stow = 2, Ready = 3, ChargeStart = 4, CastRelease = 5, InvalidRelease = 6,
        Landing = 7, Recall = 8, Nibble = 9, Bite = 10, HookSet = 11, SideRun = 12, Dive = 13, Calm = 14,
        Exhausted = 15, Snapped = 16, Escaped = 17, LineLanded = 18
    }
}
