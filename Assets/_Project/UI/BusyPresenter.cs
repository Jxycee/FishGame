using System;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only view logic for the Creating / Joining / Loading screen: text spinner plus Cancel (spec 11.2).
    internal sealed class BusyPresenter
    {
        private static readonly string[] Frames = { "|", "/", "-", "\\" };
        private readonly Label _message;
        private readonly Label _spinner;
        private readonly Button _cancel;
        private readonly IVisualElementScheduledItem _tick;
        private int _frame;

        public event Action CancelRequested;

        public BusyPresenter(VisualElement root)
        {
            _message = UiQuery.Req<Label>(root, "BusyLabel");
            _spinner = UiQuery.Req<Label>(root, "BusySpinner");
            _cancel = UiQuery.Req<Button>(root, "CancelButton");
            _cancel.text = UiStrings.Get("busy.cancel");
            _cancel.clicked += () => CancelRequested?.Invoke();
            _tick = root.schedule.Execute(() =>
            {
                _frame = (_frame + 1) % Frames.Length;
                _spinner.text = Frames[_frame];
            }).Every(150);
            _tick.Pause();
        }

        public void Show(string message)
        {
            SetMessage(message);
            _spinner.text = Frames[_frame];
            _tick.Resume();
        }

        public void SetMessage(string message) => _message.text = message ?? string.Empty;

        public void Hide() => _tick.Pause();

        public void FocusDefault() => _cancel.Focus();
    }
}
