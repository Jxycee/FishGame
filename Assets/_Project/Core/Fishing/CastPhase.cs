// Core/Fishing/CastPhase.cs: host-side phase of an ActiveCast (spec 9.9, C 7.9).
namespace FishGame.Core
{
    public enum CastPhase : byte { Waiting = 0, Hooked = 1 }
}
