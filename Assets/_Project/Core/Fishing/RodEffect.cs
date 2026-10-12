// Core/Fishing/RodEffect.cs: one queued output of RodStateMachine, drained by the owner controller (C 7.8).
// Authority: owner only. Fields not used by a Kind stay at default.
namespace FishGame.Core
{
    public struct RodEffect
    {
        public RodEffectKind Kind;
        public RodCue Cue;
        public uint CastId;
        public float ChargeNorm;
        public FightOutcome Outcome;
        public EscapeReason Reason;
        public RodEventType EventType;
        public byte SizeClass;
        public FightReport Report;     // SendResult only (zeroes when no fight ran)
    }
}
