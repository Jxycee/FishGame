// Core/Player/MovementMath.cs: movement, camera and water numbers as pure functions (spec 7.3, contract 7.2).
// No engine references. The owner's PlayerLocomotion / PlayerCamera (T11) call these each frame. Constants are the M1 values.
using System;

namespace FishGame.Core
{
    public static class MovementMath
    {
        public const float WalkSpeed = 4.2f, SprintSpeed = 6.6f, AimSpeedMult = 0.6f, SwimSpeed = 2.2f;
        public const float GroundAccel = 14f, AirAccel = 4f, KnockbackDecayGround = 10f, KnockbackDecayAir = 2f;
        public const float Gravity = -22f, TerminalVelocity = -30f, GroundedStickVelocity = -2f;
        public const float JumpHeight = 1.1f, CoyoteTime = 0.1f, JumpBufferTime = 0.1f;
        public const float SwimGravity = -4f, SwimEnterDepth = 1.2f, SwimEyeAboveSurface = 0.15f, WadeDepth = 0.3f;
        public const float SwimBuoyancyStiffness = 12f, SwimBuoyancyDamping = 5f, SwimMaxVerticalSpeed = 3f;
        public const float ClimbDuration = 0.4f, WaterTimeoutSeconds = 60f, OutOfBoundsY = -20f;
        public const float EyeHeight = 1.62f, CapsuleHeight = 1.8f, CapsuleRadius = 0.35f, CapsuleCenterY = 0.9f;
        public const float StepOffset = 0.35f, SlopeLimit = 45f, SkinWidth = 0.08f;
        public const float PitchLimit = 85f, NearClip = 0.05f, FarClip = 400f;
        public const float DefaultHorizontalFov = 90f, SprintFovKickDeg = 4f, SprintFovKickTime = 0.25f;
        public const float HeadBobStride = 1.4f, HeadBobAmplitude = 0.03f;
        public const float TraumaDecayPerSecond = 1.2f, TraumaMaxAngleDeg = 2.5f, TraumaFrequency = 20f;
        public const float CarryMaxKg = 40f, CarryMinMult = 0.55f, CarryNoSprintKg = 15f;

        // sqrt(2 * |gravity| * jumpHeight); (1.1, -22) is about 6.957 m/s.
        public static float JumpVelocity(float jumpHeight, float gravity)
        {
            return MathF.Sqrt(2f * MathF.Abs(gravity) * jumpHeight);
        }

        // v^2 / (2 |g|)
        public static float JumpPeakHeight(float launchVelocity, float gravity)
        {
            return (launchVelocity * launchVelocity) / (2f * MathF.Abs(gravity));
        }

        // clamp(1 - kg / 40, 0.55, 1)
        public static float CarrySpeedMult(float kg)
        {
            float m = 1f - kg / CarryMaxKg;
            return MathF.Min(MathF.Max(m, CarryMinMult), 1f);
        }

        // Callers pass sprint = sprintHeld && grounded (no sprint in the air).
        public static float WishSpeed(bool sprint, bool aiming, bool swimming, float carryKg)
        {
            if (swimming) return SwimSpeed;
            float baseSpeed = (sprint && !aiming && carryKg <= CarryNoSprintKg) ? SprintSpeed : WalkSpeed;
            return baseSpeed * (aiming ? AimSpeedMult : 1f) * CarrySpeedMult(carryKg);
        }

        // Moves (vx, vz) toward (targetX, targetZ) by at most accel * dt (vector length); arrives exactly when closer.
        public static void Accelerate(ref float vx, ref float vz, float targetX, float targetZ, float accel, float dt)
        {
            float dx = targetX - vx;
            float dz = targetZ - vz;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            float maxStep = accel * dt;
            if (dist <= maxStep)
            {
                vx = targetX;
                vz = targetZ;
                return;
            }
            float k = maxStep / dist;
            vx += dx * k;
            vz += dz * k;
        }

        // Reduces the length of (kx, kz) by decay * dt, never below 0. Direction is kept.
        public static void DecayKnockback(ref float kx, ref float kz, float decay, float dt)
        {
            float len = MathF.Sqrt(kx * kx + kz * kz);
            if (len <= 0f) return;
            float newLen = MathF.Max(0f, len - decay * dt);
            float scale = newLen / len;
            kx *= scale;
            kz *= scale;
        }

        // Returns dy = vy*dt + 0.5*gravity*dt*dt, then sets vy = max(vy + gravity*dt, terminalVelocity).
        // Exact for constant gravity, so a JumpVelocity launch peaks at JumpHeight regardless of frame rate.
        public static float VerticalStep(ref float vy, float gravity, float terminalVelocity, float dt)
        {
            float dy = vy * dt + 0.5f * gravity * dt * dt;
            vy = MathF.Max(vy + gravity * dt, terminalVelocity);
            return dy;
        }

        // Root height that puts the eyes SwimEyeAboveSurface above the surface.
        public static float SwimTargetRootY(float surfaceY)
        {
            return surfaceY + SwimEyeAboveSurface - EyeHeight;
        }

        // Spring-damper toward SwimTargetRootY; SwimGravity applies only while above the target (contract D26).
        public static float SwimVerticalVelocity(float rootY, float surfaceY, float vy, float dt)
        {
            float target = SwimTargetRootY(surfaceY);
            float a = (target - rootY) * SwimBuoyancyStiffness - vy * SwimBuoyancyDamping
                      + (rootY > target ? SwimGravity : 0f);
            float next = vy + a * dt;
            return MathF.Min(MathF.Max(next, -SwimMaxVerticalSpeed), SwimMaxVerticalSpeed);
        }

        // Swimming when the root is more than SwimEnterDepth below the surface.
        public static bool IsSwimmingDepth(float rootY, float surfaceY)
        {
            return rootY < surfaceY - SwimEnterDepth;
        }

        // Wading or swimming (root more than WadeDepth below the surface); gates WaterExit climbs (contract D36).
        public static bool IsInWater(float rootY, float surfaceY)
        {
            return rootY < surfaceY - WadeDepth;
        }
    }
}
