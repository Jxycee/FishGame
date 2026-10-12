namespace FishGame.Core
{
    public static class SfxIds
    {
        public const string UiClick = "Ui_Click", UiConfirm = "Ui_Confirm", UiError = "Ui_Error",
            WindAmbient = "Wind_Ambient", WaterLap = "Water_Lap", BellRing = "Bell_Ring";

        // M2 (spec 11.7 plus Rod_Thunk, C M2-D10, and Line_Crack for the spec 9.11 grace row, T23 review)
        public const string CastWhoosh = "Cast_Whoosh", SplashSmall = "Splash_Small", SplashBig = "Splash_Big", BitePlop = "Bite_Plop",
            NibbleTick = "Nibble_Tick", ReelClick = "Reel_Click", LineCreak = "Line_Creak", DiveThrum = "Dive_Thrum", Snap = "Snap",
            CatchFlop = "Catch_Flop", CatchCheer = "Catch_Cheer", RodThunk = "Rod_Thunk", LineCrack = "Line_Crack";
    }
}
