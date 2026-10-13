// Core/Ecology/IWorldContext.cs: read-only view of the replicated world state (spec 12.4 plus the M3-D53 additions).
// Authority: implemented by WorldState (T14), which the host drives and every peer reads. Consumers in Fishing, Presentation, Runtime and UI
// read it through the service registry and fall back to defaults while HasState is false.
using System;

namespace FishGame.Core
{
    public interface IWorldContext                   // implemented by WorldState (T14)
    {
        Season Season { get; } WeatherMask Weather { get; } int Minute { get; }
        bool IsInHotspot(float x, float z);
        // M3 additions to the spec 12.4 interface (M3-D53):
        bool CastingOpen { get; }                    // exact (unclamped) minute < DayClock.DayLengthMinutes; the host and owner cast gates read THIS, never Minute
        bool HasState { get; }                       // false until the first NetworkVariable sync (consumers fall back to defaults)
        string SpotId { get; }
        event Action Changed;                        // season, weather or hotspot changed (not per minute)
    }
}
