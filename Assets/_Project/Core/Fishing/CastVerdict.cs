// Core/Fishing/CastVerdict.cs: the host answer to a cast request (spec 9.3, C 7.9). Plain data.
namespace FishGame.Core
{
    public struct CastVerdict
    {
        public bool Valid;
        public CastRejectReason Reason;
        public FishingRoll Roll;          // default when rejected
        public float LandingX, LandingZ;  // zero when rejected
    }
}
