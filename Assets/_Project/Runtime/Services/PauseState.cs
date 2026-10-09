using System;

namespace FishGame.Runtime
{
    // Raises Changed only on a real change. Never touches Time.timeScale.
    public sealed class PauseState : IPauseState
    {
        public bool Paused { get; private set; }

        public event Action<bool> Changed;

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            Action<bool> handler = Changed;
            if (handler != null) handler(paused);
        }
    }
}
