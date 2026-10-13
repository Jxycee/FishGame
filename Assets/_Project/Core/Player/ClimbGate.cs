// Core/Player/ClimbGate.cs: the ladder re-entry grace (spec 7.3, R70; contract M2-D29). Pure; PlayerLocomotion owns one per owner.
// A climb that ends through the top of a LadderVolume blocks the next climb start for MovementMath.ClimbReentryGrace seconds, so a climber
// who tops out and drops a few centimetres is not caught by the ladder again. Any other exit starts no grace.
namespace FishGame.Core
{
    public struct ClimbGate
    {
        private float _blockedFor;

        public bool CanEnter => _blockedFor <= 0f;
        public float BlockedFor => _blockedFor;

        public void OnExit(bool throughTop)
        {
            if (throughTop) _blockedFor = MovementMath.ClimbReentryGrace;
        }

        // Counts the grace down. dt <= 0, NaN or infinite is ignored.
        public void Tick(float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            _blockedFor = _blockedFor > dt ? _blockedFor - dt : 0f;
        }

        public void Reset()
        {
            _blockedFor = 0f;
        }
    }
}
