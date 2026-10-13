// Core/Fishing/RodEventType.cs: RodEventRpc payload kind (C 7.5).
namespace FishGame.Core
{
    public enum RodEventType : byte { Nibble = 0, Bite = 1, HookSet = 2, Snapped = 3, Escaped = 4 }
}
