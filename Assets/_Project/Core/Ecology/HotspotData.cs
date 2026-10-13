// Core/Ecology/HotspotData.cs: one active hotspot circle (spec 12.7). default means "no hotspot".
// Authority: planned by the host (HotspotPlanner); replicated as HotspotState (T14) and read by every peer.
namespace FishGame.Core
{
    public struct HotspotData { public bool Active; public float X, Z, Radius; public int StartMinute, EndMinute; }
}
