// Core, no engine references. Routed input: what gameplay is allowed to act on this frame (spec 8.1, 8.2).
namespace FishGame.Core
{
    public struct PlayerIntent
    {
        public float MoveX, MoveY, LookYawDeg, LookPitchDeg;
        public bool JumpPressed, SprintHeld;
        public bool PrimaryHeld, PrimaryPressed, PrimaryReleased;
        public bool SecondaryHeld, SecondaryPressed, SecondaryReleased;
        public bool InteractPressed, ShovePressed, DropPressed, EquipPressed, PausePressed;
        public bool JournalPressed;                // routed in Free, RodReady, RodWaiting only (M3-D27)

        public static readonly PlayerIntent None = default;
    }
}
