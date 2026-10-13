// Core/Fishing/FightReport.cs: owner fight stats carried by ResultRpc (clean-catch claim, telemetry; C 7.8).
// Authority: produced by the owner's RodStateMachine; the host only bounds-checks the claim (spec 9.9).
namespace FishGame.Core
{
    public struct FightReport
    {
        public float MaxTension;
        public float FightSeconds;       // FightState.Elapsed
        public int NearSnapRecoveries;
    }
}
