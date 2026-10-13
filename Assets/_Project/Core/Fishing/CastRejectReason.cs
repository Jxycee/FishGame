// Core/Fishing/CastRejectReason.cs: why the host rejected a cast request (spec 9.3, C 7.5).
namespace FishGame.Core
{
    public enum CastRejectReason : byte
    {
        None = 0, ActiveCast = 1, StaleCastId = 2, BadCharge = 3, OriginTooFar = 4,
        NoBait = 5, NotWater = 6, TooShallow = 7, NotAllowedNow = 8, UnknownPlayer = 9
    }
}
