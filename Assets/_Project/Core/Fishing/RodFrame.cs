// Core/Fishing/RodFrame.cs: one owner frame of inputs and guards for RodStateMachine (spec 9.2, C 7.8).
// Authority: built by the owning client each frame; never sent over the network.
namespace FishGame.Core
{
    public struct RodFrame
    {
        public bool PrimaryPressed, PrimaryReleased, PrimaryHeld, SecondaryPressed, SecondaryReleased, SecondaryHeld;
        public bool MarkerValid;       // owner CastProbe at the current marker (Ready/Charging)
        public float PlayerToBobber;   // horizontal player-root to bobber (Waiting auto reel-in; the machine subtracts FishingConstants.CastTipReach, R67)
        public float TipToBobber;      // horizontal rod tip to bobber (FightSim start line at hook)
        public float HookDisplacement; // horizontal distance from the root position at hook time (Hooked leash)
        public bool InWater;           // player is in water (Hooked -> Dropped)
        public bool Paused;            // solo pause: nothing advances
    }
}
