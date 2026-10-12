// Core/Fishing/ActiveCast.cs: the host's record of one accepted cast (spec 9.9 plus host bookkeeping, C 7.9). Plain data.
// Authority: host only (owned by FishingHostLedger).
namespace FishGame.Core
{
    public struct ActiveCast
    {
        public uint CastId;
        public FishingRoll Roll;
        public CastPhase Phase;
        public double TAccepted, THooked;      // ledger clock (Now) at accept / at the accepted Hooked message
        public float LineLength0, TFlight;     // LineLength0 is set on Hooked (WalkBoundedLineLength0); TFlight from the cast distance
        public float CastDistance;             // horizontal origin -> landing, host-derived at accept (anti-teleport bound)
        public bool Assist;
        public ushort RodIndex;
        public byte BaitIndex;
        public float LandingX, LandingZ;
    }
}
