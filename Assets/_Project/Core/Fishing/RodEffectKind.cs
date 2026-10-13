// Core/Fishing/RodEffectKind.cs: what a RodEffect asks the owner controller to do (C 7.8).
namespace FishGame.Core
{
    public enum RodEffectKind : byte
    {
        Cue = 0, SendCastRequest = 1, SendCancel = 2, SendHooked = 3, SendResult = 4,
        SendRodEvent = 5, StartFight = 6, CommitTimedOut = 7
    }
}
