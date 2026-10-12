// Core/Fishing/AnnounceKind.cs: crew announcement kind (C 7.5).
namespace FishGame.Core
{
    public enum AnnounceKind : byte
    {
        None = 0, Landed = 1, Lost = 2, ReleasedCreelFull = 3,
        PityCast = 4   // R65: toast-only, sent when a bad-luck-nudge cast is accepted; Catch is default, no reveal
    }
}
