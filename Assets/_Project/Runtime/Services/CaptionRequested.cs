namespace FishGame.Runtime
{
    // EventBus message (C 8.1). Raised by FishingFeedback (T23) when settings.captions; shown bottom-centre by FishingHudPresenter (T25).
    // Key is a UiStrings key.
    public readonly struct CaptionRequested
    {
        public readonly string Key;
        public readonly float Seconds;

        public CaptionRequested(string key, float seconds = 2f)
        {
            Key = key;
            Seconds = seconds;
        }
    }
}
