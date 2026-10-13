// Authority: none (pure EditMode tests). FishGame.Core.ClimbMomentum: the arcade ladder climb (spec 7.3, R70 delta "user playtest 2026-10-13").
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ClimbMomentumTests
    {
        private const float Tol = 1e-4f;
        private const float DeckY = 0.6f, VolumeTopCenterY = 1.6f, EntryCenterY = -0.3f, RootToCenter = 0.9f;

        [Test]
        public void Constants_MatchTheSpecDelta()
        {
            Assert.AreEqual(2.4f, MovementMath.ClimbSpeed, "the old speed stays the base and the descent speed");
            Assert.AreEqual(3.0f, ClimbMomentum.KickSpeed);
            Assert.AreEqual(5.5f, ClimbMomentum.TopSpeed);
            Assert.AreEqual(20f, ClimbMomentum.Accel);
            Assert.AreEqual(2.6f, ClimbMomentum.LaunchUp);
            Assert.AreEqual(3.0f, ClimbMomentum.LaunchForward);
            float ratio = ClimbMomentum.TopSpeed / MovementMath.ClimbSpeed;
            Assert.That(ratio, Is.InRange(2.0f, 2.5f), "top speed is 2-2.5x the old climb speed");
        }

        // Catches: a ramp that runs on idle input, or a hang that keeps its charge (the release must stop at once: factor 1, charge 0).
        [Test]
        public void Step_NotHolding_IsIdleAndResetsTheCharge()
        {
            var m = new ClimbMomentum();
            Assert.AreEqual(1f, m.Step(false, 0.7f, 0.016f), Tol);
            Assert.AreEqual(0f, m.Speed);

            m.Step(true, 0.7f, 0.016f);
            m.Step(true, 0.7f, 0.016f);
            Assert.Greater(m.Speed, 0f);
            Assert.AreEqual(1f, m.Step(false, 0.7f, 0.016f), Tol);
            Assert.AreEqual(0f, m.Speed, "release drops the charge");
        }

        // Catches: no kick (the climb would start at the old speed and crawl up) or a kick that is applied again every frame.
        [Test]
        public void Step_FirstHeldFrame_KicksThenRamps()
        {
            var m = new ClimbMomentum();
            float f = m.Step(true, 0.7f, 0.016f);
            Assert.AreEqual(ClimbMomentum.KickSpeed, m.Speed, Tol, "first held frame is the kick");
            Assert.AreEqual(ClimbMomentum.KickSpeed / MovementMath.ClimbSpeed, f, Tol, "a 45 deg look gets the full boost");

            m.Step(true, 0.7f, 0.016f);
            Assert.AreEqual(ClimbMomentum.KickSpeed + ClimbMomentum.Accel * 0.016f, m.Speed, Tol, "then it ramps");
        }

        // Catches: a ramp that depends on the frame rate (the headless tests run over 1000 fps, players at 30-144).
        [TestCase(0.001f)]
        [TestCase(0.004f)]
        [TestCase(0.0167f)]
        [TestCase(0.033f)]
        [TestCase(0.1f)]
        public void Step_ReachesTopSpeedAtTheSameTimeAtAnyFrameRate(float dt)
        {
            var m = new ClimbMomentum();
            float t = 0f;
            m.Step(true, 1f, dt);   // the kick frame
            while (m.Speed < ClimbMomentum.TopSpeed && t < 1f)
            {
                m.Step(true, 1f, dt);
                t += dt;
            }

            float expected = (ClimbMomentum.TopSpeed - ClimbMomentum.KickSpeed) / ClimbMomentum.Accel;
            Assert.AreEqual(expected, t, dt + 1e-3f, "kick to top speed time at dt " + dt);
            m.Step(true, 1f, dt);
            Assert.AreEqual(ClimbMomentum.TopSpeed, m.Speed, Tol, "never above the top speed");
        }

        // Catches: a boost on a flat or descending look (strafing and climbing down must stay at the controlled old speed) and a cliff
        // between a level look and a 1 degree look-up.
        [Test]
        public void Factor_IsOneUnlessHeadingUp_AndGrowsSmoothlyWithTheUpShare()
        {
            float top = ClimbMomentum.TopSpeed;
            Assert.AreEqual(1f, ClimbMomentum.Factor(top, 0f), Tol, "level look");
            Assert.AreEqual(1f, ClimbMomentum.Factor(top, -0.7f), Tol, "descending");
            Assert.AreEqual(1f, ClimbMomentum.Factor(top, -1f), Tol, "straight down");
            Assert.AreEqual(top / MovementMath.ClimbSpeed, ClimbMomentum.Factor(top, 0.35f), Tol, "20 deg up is already full");
            Assert.AreEqual(top / MovementMath.ClimbSpeed, ClimbMomentum.Factor(top, 1f), Tol, "straight up");

            float tiny = ClimbMomentum.Factor(top, 0.0175f);   // 1 degree up
            Assert.That(tiny, Is.LessThan(1.1f), "1 degree up is barely boosted: " + tiny);
            Assert.That(ClimbMomentum.Factor(top, 0.1f), Is.GreaterThan(tiny));
            Assert.AreEqual(1f, ClimbMomentum.Factor(float.NaN, 0.7f));
            Assert.AreEqual(1f, ClimbMomentum.Factor(top, float.NaN));
            Assert.AreEqual(1f, ClimbMomentum.Factor(1f, 0.7f), "a speed under the old speed never slows the climb");
        }

        // The headline number: the climb out takes about 0.5-0.6 s at a 45 deg look (it was about 1.25 s). Vertical travel is entry (capsule
        // centre enters the box, root -1.2 + 0.9) to the top face (centre y 1.6); the deck wall blocks the flat part, so the climb is the
        // vertical component of the boosted wish. Checked at four frame rates.
        [TestCase(45f, 0.001f, 0.45f, 0.65f)]
        [TestCase(45f, 0.004f, 0.45f, 0.65f)]
        [TestCase(45f, 0.0167f, 0.45f, 0.65f)]
        [TestCase(45f, 0.033f, 0.45f, 0.70f)]
        [TestCase(30f, 0.0167f, 0.60f, 0.90f)]
        [TestCase(60f, 0.0167f, 0.40f, 0.60f)]
        public void ClimbOut_FromEntryToTheTopFace_TakesAboutHalfASecond(float pitchDeg, float dt, float minS, float maxS)
        {
            var m = new ClimbMomentum();
            float y = EntryCenterY;
            float t = 0f;
            while (y < VolumeTopCenterY && t < 5f)
            {
                MovementMath.ClimbWish(0f, pitchDeg, 0f, 1f, out float wx, out float wy, out float wz);
                float len = (float)System.Math.Sqrt(wx * wx + wy * wy + wz * wz);
                float k = m.Step(true, wy / len, dt);
                y += wy * k * dt;
                t += dt;
            }

            TestContext.WriteLine($"pitch {pitchDeg} dt {dt}: entry to top face {t:F3} s");
            Assert.That(t, Is.InRange(minS, maxS), "climb time at pitch " + pitchDeg);
            if (pitchDeg >= 45f) Assert.That(t, Is.LessThan(1.25f / 2f + 0.05f), "at least twice as fast as the old 1.25 s climb");
        }

        // Catches: a descent that picks up the up-boost (the descent stays the controlled old speed).
        [Test]
        public void Descent_NeverExceedsTheOldClimbSpeed()
        {
            var m = new ClimbMomentum();
            for (int i = 0; i < 100; i++)
            {
                MovementMath.ClimbWish(0f, -70f, 0f, 1f, out float wx, out float wy, out float wz);
                float len = (float)System.Math.Sqrt(wx * wx + wy * wy + wz * wz);
                float k = m.Step(true, wy / len, 0.016f);
                Assert.LessOrEqual(wy * k, 0f);
                Assert.LessOrEqual(-wy * k, MovementMath.ClimbSpeed + Tol, "descent speed");
            }
        }

        [Test]
        public void TopOutLaunch_45DegClimb_HopsUpAndForwardAlongTheFlatWish()
        {
            MovementMath.ClimbWish(-90f, 45f, 0f, 1f, out float wx, out float wy, out float wz);   // facing -x
            wx *= 2.29f; wy *= 2.29f; wz *= 2.29f;
            bool ok = ClimbMomentum.TopOutLaunch(ClimbMomentum.TopSpeed, wx, wy, wz, -1f, 0f, out float vx, out float vy, out float vz);
            Assert.IsTrue(ok);
            Assert.AreEqual(ClimbMomentum.LaunchUp, vy, Tol);
            Assert.AreEqual(-ClimbMomentum.LaunchForward, vx, 1e-3f, "forward is the flat wish direction (-x)");
            Assert.AreEqual(0f, vz, 1e-3f);
        }

        // Catches: a climber who looks (almost) straight up being flung across the deck or into the water: the forward launch shrinks to
        // LaunchForwardMin x LaunchForward and the direction falls back to the facing.
        [Test]
        public void TopOutLaunch_NearVerticalLook_HopsInPlaceMostly()
        {
            MovementMath.ClimbWish(90f, 85f, 0f, 1f, out float wx, out float wy, out float wz);
            ClimbMomentum.TopOutLaunch(ClimbMomentum.TopSpeed, wx, wy, wz, 1f, 0f, out float vx, out float vy, out float vz);
            float flat = (float)System.Math.Sqrt(vx * vx + vz * vz);
            Assert.AreEqual(ClimbMomentum.LaunchForward * ClimbMomentum.LaunchForwardMin, flat, 1e-3f);
            Assert.Greater(vx, 0f, "still the facing direction (+x)");
            Assert.AreEqual(ClimbMomentum.LaunchUp, vy, Tol);

            // Exactly vertical: no flat part at all, so the fallback facing decides.
            ClimbMomentum.TopOutLaunch(ClimbMomentum.TopSpeed, 0f, 5f, 0f, 0f, -1f, out vx, out vy, out vz);
            Assert.AreEqual(0f, vx, Tol);
            Assert.AreEqual(-ClimbMomentum.LaunchForward * ClimbMomentum.LaunchForwardMin, vz, 1e-3f);
        }

        [Test]
        public void TopOutLaunch_UnchargedClimber_GetsTheFloorFraction()
        {
            ClimbMomentum.TopOutLaunch(MovementMath.ClimbSpeed, 0f, 1.7f, -1.7f, 0f, -1f, out float vx, out float vy, out float vz);
            Assert.AreEqual(ClimbMomentum.LaunchUp * ClimbMomentum.LaunchChargeFloor, vy, Tol);
            Assert.AreEqual(-ClimbMomentum.LaunchForward * ClimbMomentum.LaunchChargeFloor, vz, 1e-3f);
            Assert.AreEqual(0f, vx, 1e-3f);
        }

        [Test]
        public void TopOutLaunch_NotHeadingUpOrBadInput_ReturnsFalseAndZero()
        {
            Assert.IsFalse(ClimbMomentum.TopOutLaunch(5f, 1f, 0f, 0f, 1f, 0f, out float vx, out float vy, out float vz));
            Assert.AreEqual(0f, vx); Assert.AreEqual(0f, vy); Assert.AreEqual(0f, vz);
            Assert.IsFalse(ClimbMomentum.TopOutLaunch(5f, 1f, -1f, 0f, 1f, 0f, out vx, out vy, out vz), "descending");
            Assert.IsFalse(ClimbMomentum.TopOutLaunch(5f, float.NaN, 1f, 0f, 1f, 0f, out vx, out vy, out vz));
            Assert.IsFalse(ClimbMomentum.TopOutLaunch(5f, 0f, float.PositiveInfinity, 0f, 1f, 0f, out vx, out vy, out vz));
            Assert.IsFalse(ClimbMomentum.TopOutLaunch(5f, 0f, 1f, 0f, 0f, 0f, out vx, out vy, out vz), "no flat wish and no facing");
            Assert.AreEqual(0f, vx); Assert.AreEqual(0f, vy); Assert.AreEqual(0f, vz);
        }

        // Catches (user note: "no floaty overshoot past about 1 m", and no launch into the water on the far side of a 3 m deck): simulates the
        // top-out with the real MovementMath integrators. Exit at the top face (root 0.7 m, 0.1 m above the deck), input released at once,
        // air then ground deceleration. The hop stays small, the travel stays under 1.2 m, and the climber stops well inside a 3 m deck.
        [TestCase(30f)]
        [TestCase(45f)]
        [TestCase(60f)]
        [TestCase(80f)]
        public void TopOutLaunch_ThenRelease_StaysUnder1Point2MetresAndHopsUnder0Point3(float pitchDeg)
        {
            const float dt = 1f / 120f;
            MovementMath.ClimbWish(-90f, pitchDeg, 0f, 1f, out float wx, out float wy, out float wz);
            float len = (float)System.Math.Sqrt(wx * wx + wy * wy + wz * wz);
            float k = ClimbMomentum.Factor(ClimbMomentum.TopSpeed, wy / len);
            Assert.IsTrue(ClimbMomentum.TopOutLaunch(ClimbMomentum.TopSpeed, wx * k, wy * k, wz * k, -1f, 0f, out float vx, out float vy, out float vz));

            float x = 0f, y = VolumeTopCenterY - RootToCenter, peak = y, z = 0f, vzf = vz;
            bool grounded = false;
            for (int i = 0; i < 1200 && !(grounded && System.Math.Abs(vx) < 1e-3f && System.Math.Abs(vzf) < 1e-3f); i++)
            {
                MovementMath.Accelerate(ref vx, ref vzf, 0f, 0f, grounded ? MovementMath.GroundAccel : MovementMath.AirAccel, dt);
                float dy = MovementMath.VerticalStep(ref vy, MovementMath.Gravity, MovementMath.TerminalVelocity, dt);
                x += vx * dt;
                z += vzf * dt;
                y += dy;
                if (y <= DeckY) { y = DeckY; grounded = true; vy = 0f; }
                if (y > peak) peak = y;
            }

            float travel = (float)System.Math.Sqrt(x * x + z * z);
            TestContext.WriteLine($"pitch {pitchDeg}: travel {travel:F2} m, peak {peak - (VolumeTopCenterY - RootToCenter):F2} m above the exit");
            Assert.That(travel, Is.LessThan(1.2f), "overshoot at pitch " + pitchDeg);
            Assert.That(peak - (VolumeTopCenterY - RootToCenter), Is.LessThan(0.3f), "hop height at pitch " + pitchDeg);
            if (pitchDeg <= 60f) Assert.That(travel, Is.GreaterThan(0.5f), "a real step onto the deck, not a drop at the lip");
        }
    }
}
