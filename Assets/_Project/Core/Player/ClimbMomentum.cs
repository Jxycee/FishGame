// Core/Player/ClimbMomentum.cs: the arcade ladder climb (spec 7.3, R70 delta "user playtest 2026-10-13"). Pure; PlayerLocomotion owns one per owner.
// Today's climb is a flat MovementMath.ClimbSpeed (2.4 m/s) along the look. Playtest note S4: "ladders should give the players more momentum
// when they go up them ... the physics should be less realistic". So while the climb heads UP, the speed starts with a small kick
// (KickSpeed) and ramps to TopSpeed (about 2.3x the old speed); descending and sideways stay at ClimbSpeed. Letting go still stops the
// climber at once (Reset). At the top, TopOutLaunch gives one small hop up and forward so the climber pops onto the deck.
using System;

namespace FishGame.Core
{
    public struct ClimbMomentum
    {
        public const float KickSpeed = 3.0f;       // m/s along the look on the first frame W is held [tune]
        public const float TopSpeed = 5.5f;        // m/s along the look at full charge [tune]: 2.3x ClimbSpeed
        public const float Accel = 20f;            // m/s per second from the kick to TopSpeed (about 0.125 s) [tune]
        public const float UpShareFull = 0.35f;    // an upward look share (sin of the climb angle) from here up gets the full boost (about 20 deg)
        public const float LaunchUp = 2.6f;        // m/s straight up at the top-out [tune]: a hop of about 0.15 m
        public const float LaunchForward = 3.0f;   // m/s along the flat climb direction at the top-out [tune]: about 0.8 m of travel in all
        public const float LaunchFlatFull = 0.5f;  // a flat share (cos of the climb angle) at or above this gets the full forward launch (pitch <= 60 deg)
        public const float LaunchForwardMin = 0.45f;   // a near-vertical look still gets this fraction of LaunchForward
        public const float LaunchChargeFloor = 0.6f;   // a climber who never charged (slow stick) gets this fraction of the launch

        private float _speed;

        // 0 while idle (hanging or not climbing); otherwise the current charged speed along the look, KickSpeed..TopSpeed.
        public float Speed => _speed;

        public void Reset()
        {
            _speed = 0f;
        }

        // One climb frame. holding = the climber is giving input; upShare = the unit wish direction's vertical component (-1..1).
        // Returns the factor to multiply the ClimbWish vector by: 1 when idle or not heading up, up to TopSpeed / ClimbSpeed when heading up
        // (weighted by how steeply: a level look gets none, so strafing and flat movement on the ladder stay controlled).
        // Releasing resets the charge, so the next press kicks again. dt <= 0, NaN or infinite adds no charge.
        public float Step(bool holding, float upShare, float dt)
        {
            if (!holding)
            {
                _speed = 0f;
                return 1f;
            }

            if (_speed < KickSpeed)
            {
                _speed = KickSpeed;
            }
            else if (dt > 0f && !float.IsInfinity(dt))
            {
                _speed = MathF.Min(TopSpeed, _speed + Accel * dt);
            }

            return Factor(_speed, upShare);
        }

        // Pure: the ClimbWish multiplier for a charged speed and an upward share. Never below 1.
        public static float Factor(float speed, float upShare)
        {
            if (float.IsNaN(speed) || float.IsNaN(upShare)) return 1f;
            float weight = MathF.Min(MathF.Max(upShare / UpShareFull, 0f), 1f);
            float boost = MathF.Max(speed / MovementMath.ClimbSpeed - 1f, 0f);
            return 1f + boost * weight;
        }

        // The velocity a climber gets on leaving through the top. wishX/Y/Z is the commanded climb velocity of the last climb frame (not the
        // CharacterController's, which spikes at high frame rates); fallbackX/Z is the flat facing used when the wish has no flat part.
        // Returns false (velocity zero) when the wish does not head up or any input is not finite: the caller keeps the plain carry then.
        // Up = LaunchUp. Forward = LaunchForward along the flat wish direction, scaled down for a near-vertical look so a climber who looks
        // straight up hops in place rather than being flung. Both scale with the charge (LaunchChargeFloor..1).
        public static bool TopOutLaunch(float speed, float wishX, float wishY, float wishZ, float fallbackX, float fallbackZ,
            out float vx, out float vy, out float vz)
        {
            vx = 0f;
            vy = 0f;
            vz = 0f;
            if (!IsFinite(speed) || !IsFinite(wishX) || !IsFinite(wishY) || !IsFinite(wishZ) || !IsFinite(fallbackX) || !IsFinite(fallbackZ)) return false;
            if (!(wishY > 0f)) return false;

            float flat = MathF.Sqrt(wishX * wishX + wishZ * wishZ);
            float len = MathF.Sqrt(flat * flat + wishY * wishY);
            float dirX, dirZ;
            if (flat > 1e-4f)
            {
                dirX = wishX / flat;
                dirZ = wishZ / flat;
            }
            else
            {
                float f = MathF.Sqrt(fallbackX * fallbackX + fallbackZ * fallbackZ);
                if (f <= 1e-6f) return false;
                dirX = fallbackX / f;
                dirZ = fallbackZ / f;
            }

            float flatShare = flat / len;
            float forwardScale = MathF.Min(MathF.Max(flatShare / LaunchFlatFull, LaunchForwardMin), 1f);
            float charge = MathF.Min(MathF.Max((speed - MovementMath.ClimbSpeed) / (TopSpeed - MovementMath.ClimbSpeed), 0f), 1f);
            float scale = LaunchChargeFloor + (1f - LaunchChargeFloor) * charge;

            vx = dirX * LaunchForward * forwardScale * scale;
            vz = dirZ * LaunchForward * forwardScale * scale;
            vy = LaunchUp * scale;
            return true;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
