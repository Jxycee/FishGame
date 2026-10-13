// Core/Fishing/RodForceReason.cs: why the rod is forced back to idle by the world (C 7.5).
namespace FishGame.Core
{
    public enum RodForceReason : byte { Teleport = 0, Respawn = 1, Stow = 2, Swim = 3, Disconnect = 4 }
}
