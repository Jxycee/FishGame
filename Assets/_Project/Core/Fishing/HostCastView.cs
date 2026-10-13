// Core/Fishing/HostCastView.cs: host-side facts the Fishing adapter supplies with a cast request (C 7.9, C 7.13). Plain data.
// A default view rejects NotWater (GridWater false) and its Context throws in the resolver (default Events/Skills are invalid):
// every builder sets both (D-F3).
namespace FishGame.Core
{
    public struct HostCastView
    {
        public float PlayerX, PlayerY, PlayerZ;   // host view of the sender root (interpolated)
        public WaterProbe Probe;                  // CastProbe at the landing returned by FishingHostLedger.TryComputeLanding
        public bool CastingAllowed;               // gameplay scene AND IWorldContext.CastingOpen (M3); M4 adds run phase Day
        public bool GridWater;                    // ShoreGrid.IsWater(landing) on the host (M3-D16)
        public FishingContext Context;            // world part filled by FishingHost; the ledger overwrites RodTier, Bait and Depth (M3-D15)
    }
}
