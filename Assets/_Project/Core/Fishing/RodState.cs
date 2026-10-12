// Core/Fishing/RodState.cs: the rod state machine states (spec 9.2, C 7.5). Values are wire bytes; never renumber.
namespace FishGame.Core
{
    public enum RodState : byte
    {
        Idle = 0, Readying = 1, Ready = 2, Charging = 3, Casting = 4, Waiting = 5,
        Bite = 6, Hooked = 7, Landed = 8, Snapped = 9, Escaped = 10
    }
}
