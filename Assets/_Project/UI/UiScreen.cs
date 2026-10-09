namespace FishGame.UI
{
    // Screens shown by UiFlowController. Hud is the in-game overlay; Pause and Settings-from-Pause keep it visible underneath.
    public enum UiScreen : byte { None = 0, MainMenu = 1, Busy = 2, Settings = 3, Pause = 4, Hud = 5 }
}
