namespace FishGame.Runtime
{
    // EventBus message. The UI (ToastPresenter) shows it as a bottom-right toast. Key is a UI string key, Arg an optional value.
    public readonly struct ToastRequested
    {
        public readonly string Key;
        public readonly string Arg;
        public readonly float Seconds;

        public ToastRequested(string key, string arg = null, float seconds = 4f)
        {
            Key = key;
            Arg = arg;
            Seconds = seconds;
        }
    }
}
