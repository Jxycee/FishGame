// Runtime. Single source of action map/action/scheme names for FishGameInput.inputactions (spec 8.1). No engine types.
namespace FishGame.Runtime
{
    public static class InputActionNames
    {
        public const string AssetPath = "Assets/_Project/Runtime/Input/FishGameInput.inputactions";

        public const string PlayerMap = "Player", FishingMap = "Fishing", UiMap = "UI";

        public const string Move = "Player/Move", Look = "Player/Look", Jump = "Player/Jump", Sprint = "Player/Sprint",
            Primary = "Player/Primary", Secondary = "Player/Secondary", Interact = "Player/Interact", Shove = "Player/Shove",
            Drop = "Player/Drop", Equip = "Player/Equip", Journal = "Player/Journal", Pause = "Player/Pause";

        public const string Reel = "Fishing/Reel", Steer = "Fishing/Steer";

        public const string Navigate = "UI/Navigate", Submit = "UI/Submit", Cancel = "UI/Cancel", Point = "UI/Point", Click = "UI/Click";

        public const string KeyboardMouseScheme = "Keyboard&Mouse", GamepadScheme = "Gamepad";

        public static readonly string[] PlayerActions =
            { "Move", "Look", "Jump", "Sprint", "Primary", "Secondary", "Interact", "Shove", "Drop", "Equip", "Journal", "Pause" };
        public static readonly string[] FishingActions = { "Reel", "Steer" };
        public static readonly string[] UiActions = { "Navigate", "Submit", "Cancel", "Point", "Click" };
    }
}
