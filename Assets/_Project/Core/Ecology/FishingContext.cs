// Core/Ecology/FishingContext.cs: everything the resolver reads for one cast (spec 12.4 fields). Plain data.
// Authority: built by the host (FishingHostLedger via HostCastView) from replicated world state and its own truth; also built on every
// peer for the journal's "biting now" badge. Callers set Events = EventMods.Neutral and Skills = SkillMods.Neutral (a default is invalid).
namespace FishGame.Core
{
    public struct FishingContext
    {
        public int ZoneIndex; public byte SubAreaIndex; public float Depth;   // SubAreaIndex AnySubArea = gate passes; Depth +Infinity = gate passes
        public int Minute; public Season Season; public WeatherMask Weather;
        public int RodTier; public BaitMods Bait; public bool InHotspot;
        public float Luck;                           // -1..1, size skew only
        public EventMods Events; public SkillMods Skills;
        public int PlayersInSession;
    }
}
