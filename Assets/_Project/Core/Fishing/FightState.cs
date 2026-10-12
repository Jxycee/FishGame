// Core/Fishing/FightState.cs: the fight's state record (spec 9.5, C 7.7). Plain data; FightSim owns the only writer.
// Authority: owner only (the host never replays the fight, spec 9.9).
namespace FishGame.Core
{
    public struct FightState
    {
        public float LineLength, Lateral, Tension, FishStamina;
        public FightPhase Phase; public float PhaseTimer; public sbyte BurstDir;   // +-1 side run, 0 dive
        public float SnapTimer, PinnedTimer, SlackTimer, Elapsed;
        public int NearSnapRecoveries; public float MaxTension;
        public FightOutcome Outcome; public EscapeReason Reason;
    }
}
