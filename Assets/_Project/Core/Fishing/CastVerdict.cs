// Core/Fishing/CastVerdict.cs: the host answer to a cast request (spec 9.3, C 7.9). Plain data.
namespace FishGame.Core
{
    public struct CastVerdict
    {
        public bool Valid;
        public CastRejectReason Reason;
        public FishingRoll Roll;          // default when rejected
        public float LandingX, LandingZ;  // zero when rejected
        // True only for the first accepted Pity cast of a streak: the host announces PityCast once, not on every cancel-and-recast of the same
        // streak (Cancelled keeps FailStreak by contract, so the next cast is Pity again). The flag clears when the streak resets.
        public bool AnnouncePity;
    }
}
