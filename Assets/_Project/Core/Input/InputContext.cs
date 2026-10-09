// Core, no engine references. Effective input context (spec 8.2). Set by gameplay code; Menu wins via IInputService.MenuOpen.
namespace FishGame.Core
{
    public enum InputContext : byte
    {
        Free = 0,
        RodReady = 1,
        RodAiming = 2,
        RodWaiting = 3,
        RodFighting = 4,
        Carrying = 5,
        Staggered = 6,
        Downed = 7,
        Carried = 8,
        Swimming = 9,
        Climbing = 10,
        Menu = 11,
        Seated = 12
    }
}
