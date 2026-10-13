// Core/Journal/JournalUpdate.cs: what one JournalRules.Apply changed, for toasts. default means "nothing changed". Authority: local.
namespace FishGame.Core
{
    public struct JournalUpdate { public bool Changed; public bool FirstCatch; public bool PersonalBest; public int LevelBefore, LevelAfter; }
}
