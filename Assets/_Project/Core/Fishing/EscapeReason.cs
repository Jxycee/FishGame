// Core/Fishing/EscapeReason.cs: why a cast ended as Escaped (spec 9.2/9.5, C 7.5).
namespace FishGame.Core
{
    public enum EscapeReason : byte
    {
        None = 0, Missed = 1, Pinned = 2, ThrewHook = 3, Dropped = 4, Timeout = 5, Invalid = 6, Disconnected = 7
    }
}
