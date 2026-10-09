using System;
using FishGame.Runtime;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only. Bottom-right toast queue (spec 11.4): at most 4 visible (oldest dropped), each removed after its Seconds.
    // The host join-code toast is masked in streamer mode (spec 11.3).
    internal sealed class ToastPresenter : IDisposable
    {
        private const int MaxVisible = 4;
        private const float DefaultSeconds = 4f;
        private const string JoinCodeKey = "toast.join_code";
        private readonly VisualElement _stack;
        private readonly ISettings _settings;
        private readonly IDisposable _subscription;

        public ToastPresenter(VisualElement root, EventBus bus, ISettings settings)
        {
            _stack = UiQuery.Req<VisualElement>(root, "ToastStack");
            _settings = settings;
            _subscription = bus.Subscribe<ToastRequested>(Show);
        }

        public void Dispose() => _subscription.Dispose();

        private void Show(ToastRequested toast)
        {
            if (string.IsNullOrEmpty(toast.Key)) return;
            var arg = toast.Arg;
            if (arg != null && toast.Key == JoinCodeKey && _settings.Current.streamerMode) arg = UiStrings.Get("pause.masked");
            var text = arg != null ? UiStrings.Format(toast.Key, arg) : UiStrings.Get(toast.Key);
            while (_stack.childCount >= MaxVisible) _stack[0].RemoveFromHierarchy();
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("toast");
            _stack.Add(label);
            var seconds = toast.Seconds > 0f ? toast.Seconds : DefaultSeconds;
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000f));
        }
    }
}
