namespace FishGame.Core
{
    // Connection lifecycle state (spec 6.4). Failed carries its reason in ConnectionStateMachine.FailureReason.
    public enum ConnState : byte { Offline = 0, Connecting = 1, Connected = 2, ShuttingDown = 3, Failed = 4 }
}
