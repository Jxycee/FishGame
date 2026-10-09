using System;

namespace FishGame.Runtime
{
    public interface ISettings
    {
        string FilePath { get; }
        SettingsData Current { get; }          // live, mutable; call NotifyChanged after editing
        event Action<SettingsData> Changed;
        void NotifyChanged();                  // Clamp + raise Changed (values apply live); never writes
        void Save();                           // Clamp + atomic write (panel close / quit; never per slider tick)
        string EnsureLocalPlayerId();          // creates + saves once if empty; returns the id
    }
}
