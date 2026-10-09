namespace FishGame.Core
{
    // Inputs to ConnectionStateMachine (spec 6.4, contract 7.3).
    public enum ConnEvent : byte { Begin = 0, Established = 1, Fail = 2, UnexpectedStop = 3, Leave = 4, ShutdownComplete = 5 }
}
