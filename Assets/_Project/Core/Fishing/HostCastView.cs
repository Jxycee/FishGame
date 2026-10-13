// Core/Fishing/HostCastView.cs: host-side facts the Fishing adapter supplies with a cast request (C 7.9). Plain data.
namespace FishGame.Core
{
    public struct HostCastView
    {
        public float PlayerX, PlayerY, PlayerZ;   // host view of the sender root (interpolated)
        public WaterProbe Probe;                  // CastProbe at the landing returned by FishingHostLedger.TryComputeLanding
        public bool CastingAllowed;               // M2: true in gameplay scenes (M4: run phase Day)
    }
}
