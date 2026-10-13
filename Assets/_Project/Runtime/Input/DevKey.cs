// Runtime. Dev-only keys read through IDevInput (F3-F10; spec 17.2, 12.11). Not part of the rebindable input asset.
namespace FishGame.Runtime
{
    public enum DevKey : byte
    {
        ToggleOverlay = 0,
        NextRod = 1,
        NextBait = 2,
        ToggleForcedBite = 3,
        ToggleTwitchy = 4,
        RippleAtCrosshair = 5,
        ScrubClock = 6,             // F8, M3 (M3-D32): read through IDevInput.IsHeld (held = the host clock runs x60)
        CycleForcedSpecies = 7      // F10
    }
}
