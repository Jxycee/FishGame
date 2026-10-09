using System;

namespace FishGame.Runtime
{
    // Solo pause flag (registered in the Game scope by GameScope). Never touches Time.timeScale.
    public interface IPauseState
    {
        bool Paused { get; }
        event Action<bool> Changed;
        void SetPaused(bool paused);
    }
}
