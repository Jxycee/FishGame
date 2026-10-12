// Runtime. Dev-only keys read through IDevInput (F3-F7, F9; spec 17.2). Not part of the rebindable input asset.
namespace FishGame.Runtime
{
    public enum DevKey : byte
    {
        ToggleOverlay = 0,
        NextRod = 1,
        NextBait = 2,
        ToggleForcedBite = 3,
        ToggleTwitchy = 4,
        RippleAtCrosshair = 5
    }
}
