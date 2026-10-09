using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class MovementMathTests
    {
        [Test]
        public void MovementMath_JumpPeak_IsOnePointOneMeters()
        {
            const float dt = 1f / 240f;
            float g = MovementMath.Gravity;
            float vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, g);
            float y = 0f;
            float peak = 0f;
            for (int i = 0; i < 240 * 2; i++)
            {
                y += MovementMath.VerticalStep(ref vy, g, MovementMath.TerminalVelocity, dt);
                if (y > peak) peak = y;
            }
            Assert.That(peak, Is.InRange(1.09f, 1.11f));
        }

        [Test]
        public void MovementMath_JumpVelocity_MatchesSpecFormula()
        {
            float v = MovementMath.JumpVelocity(1.1f, -22f);
            Assert.That(v, Is.EqualTo(6.957f).Within(1e-3f));
            Assert.That(MovementMath.JumpPeakHeight(v, -22f), Is.EqualTo(1.1f).Within(1e-4f));
        }

        [Test]
        public void MovementMath_CarrySpeedMult_ZeroKg_IsOne()
        {
            Assert.That(MovementMath.CarrySpeedMult(0f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void MovementMath_CarrySpeedMult_FortyKg_IsMinimum()
        {
            Assert.That(MovementMath.CarrySpeedMult(40f), Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(MovementMath.CarrySpeedMult(100f), Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(MovementMath.CarrySpeedMult(10f), Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void MovementMath_WishSpeed_SwimAndSprintRules()
        {
            // swimming ignores sprint
            Assert.That(MovementMath.WishSpeed(true, false, true, 0f), Is.EqualTo(2.2f).Within(1e-4f));
            // sprint
            Assert.That(MovementMath.WishSpeed(true, false, false, 0f), Is.EqualTo(6.6f).Within(1e-4f));
            // no sprint above 15 kg: walk 4.2 * (1 - 16/40) = 2.52
            Assert.That(MovementMath.WishSpeed(true, false, false, 16f), Is.EqualTo(2.52f).Within(1e-4f));
            // aiming: no sprint, x0.6
            Assert.That(MovementMath.WishSpeed(true, true, false, 0f), Is.EqualTo(2.52f).Within(1e-4f));
            // walk
            Assert.That(MovementMath.WishSpeed(false, false, false, 0f), Is.EqualTo(4.2f).Within(1e-4f));
        }

        [Test]
        public void MovementMath_Accelerate_StepsByAccelAndArrivesExactly()
        {
            float vx = 0f, vz = 0f;
            MovementMath.Accelerate(ref vx, ref vz, 10f, 0f, MovementMath.GroundAccel, 0.1f);
            Assert.That(vx, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(vz, Is.EqualTo(0f).Within(1e-6f));

            vx = 0f; vz = 0f;
            MovementMath.Accelerate(ref vx, ref vz, 3f, 4f, 10f, 0.1f); // step length 1 along (0.6, 0.8)
            Assert.That(vx, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(vz, Is.EqualTo(0.8f).Within(1e-4f));

            vx = 0f; vz = 0f;
            MovementMath.Accelerate(ref vx, ref vz, 0.5f, 0f, MovementMath.GroundAccel, 0.1f);
            Assert.That(vx, Is.EqualTo(0.5f));
            Assert.That(vz, Is.EqualTo(0f));
        }

        [Test]
        public void MovementMath_DecayKnockback_ReducesLengthNotBelowZero()
        {
            float kx = 3f, kz = 4f;
            MovementMath.DecayKnockback(ref kx, ref kz, MovementMath.KnockbackDecayGround, 0.1f); // length 5 -> 4
            Assert.That(kx, Is.EqualTo(2.4f).Within(1e-4f));
            Assert.That(kz, Is.EqualTo(3.2f).Within(1e-4f));

            MovementMath.DecayKnockback(ref kx, ref kz, MovementMath.KnockbackDecayGround, 10f);
            Assert.That(kx, Is.EqualTo(0f));
            Assert.That(kz, Is.EqualTo(0f));

            MovementMath.DecayKnockback(ref kx, ref kz, MovementMath.KnockbackDecayGround, 0.1f); // zero stays zero, no NaN
            Assert.That(kx, Is.EqualTo(0f));
            Assert.That(kz, Is.EqualTo(0f));
        }

        [Test]
        public void MovementMath_VerticalStep_ClampsAtTerminalVelocity()
        {
            float vy = -29.9f;
            float dy = MovementMath.VerticalStep(ref vy, MovementMath.Gravity, MovementMath.TerminalVelocity, 0.1f);
            Assert.That(vy, Is.EqualTo(MovementMath.TerminalVelocity));
            Assert.That(dy, Is.EqualTo(-3.1f).Within(1e-4f)); // -29.9*0.1 + 0.5*-22*0.01
        }

        [Test]
        public void MovementMath_SwimVerticalVelocity_PushesTowardTargetAndClamps()
        {
            float target = MovementMath.SwimTargetRootY(0f);
            Assert.That(target, Is.EqualTo(-1.47f).Within(1e-4f));

            // at rest on the target: no acceleration
            Assert.That(MovementMath.SwimVerticalVelocity(target, 0f, 0f, 0.1f), Is.EqualTo(0f).Within(1e-5f));
            // far below: pushed up, clamped to +3
            Assert.That(MovementMath.SwimVerticalVelocity(-6f, 0f, 0f, 1f), Is.EqualTo(MovementMath.SwimMaxVerticalSpeed));
            // 0.1 above target: a = -0.1*12 + SwimGravity(-4) = -5.2 -> vy = -0.52 after 0.1 s
            Assert.That(MovementMath.SwimVerticalVelocity(target + 0.1f, 0f, 0f, 0.1f), Is.EqualTo(-0.52f).Within(1e-3f));
            // fast fall is clamped to -3
            Assert.That(MovementMath.SwimVerticalVelocity(target + 3f, 0f, -10f, 0.1f), Is.EqualTo(-MovementMath.SwimMaxVerticalSpeed));
        }

        [Test]
        public void MovementMath_WaterDepthChecks_UseThresholds()
        {
            Assert.That(MovementMath.IsSwimmingDepth(-1.3f, 0f), Is.True);
            Assert.That(MovementMath.IsSwimmingDepth(-1.1f, 0f), Is.False);
            Assert.That(MovementMath.IsInWater(-0.31f, 0f), Is.True);
            Assert.That(MovementMath.IsInWater(-0.29f, 0f), Is.False);
            Assert.That(MovementMath.IsInWater(0.5f, 1f), Is.True); // surface at y 1, root 0.5 below it
        }
    }
}
