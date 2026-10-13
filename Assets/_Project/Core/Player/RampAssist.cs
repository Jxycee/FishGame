// Core/Player/RampAssist.cs: the shore-ramp foot assist (spec 7.3, R70; contract M2-D30). Pure; PlayerLocomotion owns one per owner.
// Inside the ramp foot a player who is in water (wading or swimming depth, contract D36) wades instead of swimming: feet on the bed, walk
// speed, normal gravity. A swimmer who enters the foot while pushing toward the ramp top gets one upward kick (MovementMath.RampAssistKick)
// on that frame. Never a lerp, never a pull: without input nothing moves the player.
namespace FishGame.Core
{
    public struct RampAssist
    {
        private bool _wasInFoot;
        private bool _wasSwimming;

        public static RampAssist Create()
        {
            return default;
        }

        // Returns the swimming flag the locomotion uses this frame. kickVy is RampAssistKick on the entry frame described above, else 0.
        public bool Step(bool inRampFoot, bool inWater, bool swimmingDepth, bool pushingTowardTop, out float kickVy)
        {
            bool inFoot = inRampFoot && inWater;
            kickVy = inFoot && !_wasInFoot && _wasSwimming && pushingTowardTop ? MovementMath.RampAssistKick : 0f;
            bool swimming = !inFoot && swimmingDepth;
            _wasInFoot = inFoot;
            _wasSwimming = swimming;
            return swimming;
        }
    }
}
