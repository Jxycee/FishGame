namespace FishGame.Core
{
    // How the current session was started. None while Offline.
    public enum SessionMode : byte { None = 0, Solo = 1, DevDirect = 2, Online = 3 }
}
