// Core/Fishing/CommitDecision.cs: the host resolution of one cast (spec 9.9, C 7.9). Plain data.
// Authority: produced by FishingHostLedger (host only); the Fishing adapter turns it into CommitRpc, announcements and creel entries.
namespace FishGame.Core
{
    public struct CommitDecision
    {
        public ulong ClientId;
        public byte Slot;
        public uint CastId;
        public bool Accepted;      // false ONLY for orphan repeats (a stored resolution replayed); every FIRST resolution has true (E-Q6)
        public FightOutcome Outcome;
        public EscapeReason Reason;
        public bool Refunded;
        public AnnounceKind Announce;
        public FishingRoll Roll;   // the cast roll on EVERY non-orphan decision (BF-4); default on orphans
        public bool AddToCreel;
        public bool CleanBonus;    // Roll.Catch already carries the upgraded quality and value
    }
}
