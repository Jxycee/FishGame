// Core/Player/JumpAssist.cs: coyote time + jump buffer (spec 7.3). Pure struct, ticked by PlayerLocomotion (T11).
// Always create with JumpAssist.Create(); default(JumpAssist) has both timers at 0 and would allow an immediate jump.
// jumpPressed is the pressed-this-frame edge, not "held".
namespace FishGame.Core
{
    public struct JumpAssist
    {
        private float _sinceGrounded;
        private float _sinceJump;

        public static JumpAssist Create()
        {
            return new JumpAssist
            {
                _sinceGrounded = float.PositiveInfinity,
                _sinceJump = float.PositiveInfinity
            };
        }

        public float TimeSinceGrounded => _sinceGrounded;
        public float TimeSinceJumpPressed => _sinceJump;

        // Returns true on the tick the jump should fire, then resets both timers so it fires only once.
        public bool Tick(float dt, bool grounded, bool jumpPressed)
        {
            _sinceGrounded = grounded ? 0f : _sinceGrounded + dt;
            _sinceJump = jumpPressed ? 0f : _sinceJump + dt;
            if (_sinceJump <= MovementMath.JumpBufferTime && _sinceGrounded <= MovementMath.CoyoteTime)
            {
                _sinceGrounded = float.PositiveInfinity;
                _sinceJump = float.PositiveInfinity;
                return true;
            }
            return false;
        }
    }
}
