// Core/Fishing/FightState.cs: the fight's state record (spec 9.5 as rewritten by R72). Plain data; FightSim owns the only writer.
// Authority: owner only (the host never replays the fight, spec 9.9).
namespace FishGame.Core
{
    public struct FightState
    {
        public float LineLength, Lateral;   // m from the rod tip; -1..+1 across the cone (+1 = right, |1| = LateralMaxDeg)
        public float Strain, Tension;       // Strain in Rust units (bar full at StrainBarFull, snap above the snap strain); Tension = Strain / StrainBarFull (display)
        public float Tire;                  // Rust TireAmount: rises while steering, falls without steer; no floor
        public FightPhase Phase;            // Calm = the fish rests (Rust state 0), Burst = a run
        public float PhaseTimer;            // seconds left in the phase; the switch happens at the start of the tick it reaches 0
        public sbyte BurstDir;              // run side: -1 left, +1 right (always set in a run); 0 at rest
        public bool Back;                   // the run also pulls back (Rust PullingBack); false at rest
        public float SnapTimer, SlackTimer, Elapsed;   // SnapTimer = seconds over the full bar (FlagGrace); SlackTimer = seconds not reeling (junk only)
        public float IdleTimer;             // seconds with no reel and no steer input (a fish only: IdleEscapeTime throws the hook at this; any input resets it)
        public int NearSnapRecoveries; public float MaxTension;
        public FightOutcome Outcome; public EscapeReason Reason;
    }
}
