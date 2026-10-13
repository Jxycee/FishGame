// Authority: none (pure EditMode tests). FishGame.Core.MovementMath.ClimbWish (spec 7.3, R70): Move along the camera look at ClimbSpeed.
// Unity axes: +y up; yaw clockwise from +z, so forward(yaw, pitch) = (sin yaw cos pitch, sin pitch, cos yaw cos pitch), right = (cos yaw, 0, -sin yaw).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ClimbWishTests
    {
        private const float Tol = 1e-4f;

        private static void Wish(float yawDeg, float pitchDeg, float moveX, float moveY, out float x, out float y, out float z)
        {
            MovementMath.ClimbWish(yawDeg, pitchDeg, moveX, moveY, out x, out y, out z);
        }

        // Catches: a retuned seed slipping in unnoticed (spec R70: 2.4 m/s, 0.25 s).
        [Test]
        public void ClimbWish_Constants_MatchSpec()
        {
            Assert.AreEqual(2.4f, MovementMath.ClimbSpeed);
            Assert.AreEqual(0.25f, MovementMath.ClimbReentryGrace);
        }

        // Catches: gravity, drift or a decaying velocity on the ladder (R70: no input = hang, exactly).
        [Test]
        public void ClimbWish_NoInput_IsExactlyZero()
        {
            Wish(37f, 60f, 0f, 0f, out float x, out float y, out float z);
            Assert.AreEqual(0f, x, 0f);
            Assert.AreEqual(0f, y, 0f);
            Assert.AreEqual(0f, z, 0f);
        }

        // Catches: pitch ignored (y would be 0) or the pitch sign flipped (W while looking up would descend).
        [Test]
        public void ClimbWish_ForwardLookingUp45_ClimbsAlongTheLook()
        {
            Wish(0f, 45f, 0f, 1f, out float x, out float y, out float z);
            float c = 2.4f * 0.70710678f;
            Assert.AreEqual(0f, x, Tol);
            Assert.AreEqual(c, y, Tol);
            Assert.AreEqual(c, z, Tol);
        }

        // Catches: descending only through S (R70: look down + W descends).
        [Test]
        public void ClimbWish_ForwardLookingDown60_Descends()
        {
            Wish(0f, -60f, 0f, 1f, out float x, out float y, out float z);
            Assert.AreEqual(0f, x, Tol);
            Assert.AreEqual(-2.4f * 0.8660254f, y, Tol);
            Assert.AreEqual(2.4f * 0.5f, z, Tol);
        }

        // Catches: S not reversing the look direction.
        [Test]
        public void ClimbWish_BackwardLookingUp_DescendsAndBacksOff()
        {
            Wish(0f, 45f, 0f, -1f, out float x, out float y, out float z);
            float c = 2.4f * 0.70710678f;
            Assert.AreEqual(-c, y, Tol);
            Assert.AreEqual(-c, z, Tol);
        }

        // Catches: yaw ignored or applied with the wrong handedness (yaw 90 must face +x).
        [Test]
        public void ClimbWish_Yaw90Forward_PointsPlusX()
        {
            Wish(90f, 0f, 0f, 1f, out float x, out float y, out float z);
            Assert.AreEqual(2.4f, x, Tol);
            Assert.AreEqual(0f, y, Tol);
            Assert.AreEqual(0f, z, Tol);
        }

        // Catches: strafe using the pitched vector (A/D would climb) or the right vector with the wrong sign.
        [Test]
        public void ClimbWish_StrafeRight_IsFlatAlongTransformRight()
        {
            Wish(90f, 60f, 1f, 0f, out float x, out float y, out float z);   // right at yaw 90 = (0, 0, -1)
            Assert.AreEqual(0f, x, Tol);
            Assert.AreEqual(0f, y, Tol);
            Assert.AreEqual(-2.4f, z, Tol);
        }

        // Catches: no length clamp (a diagonal would climb at 3.39 m/s) and, the other way, normalising small stick input up to full speed.
        [Test]
        public void ClimbWish_InputLength_ClampedToOneButNotNormalisedUp()
        {
            Wish(0f, 0f, 1f, 1f, out float x, out float y, out float z);
            Assert.AreEqual(2.4f, (float)System.Math.Sqrt(x * x + y * y + z * z), Tol, "diagonal");

            Wish(0f, 0f, 0.5f, 0f, out x, out y, out z);
            Assert.AreEqual(1.2f, (float)System.Math.Sqrt(x * x + y * y + z * z), Tol, "half stick");
        }

        // Catches: no pitch clamp (the camera never exceeds PitchLimit 85, but a bad caller must not climb faster or flip).
        [Test]
        public void ClimbWish_PitchBeyondLimit_IsClampedTo85()
        {
            Wish(0f, 120f, 0f, 1f, out float _, out float y, out float _);
            Assert.AreEqual(2.4f * 0.9961947f, y, Tol);
        }

        // Catches: NaN or infinity reaching CharacterController.Move.
        [Test]
        public void ClimbWish_NonFiniteInput_IsZero()
        {
            float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float b in bad)
            {
                Wish(b, 0f, 0f, 1f, out float x, out float y, out float z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "yaw " + b);
                Wish(0f, b, 0f, 1f, out x, out y, out z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "pitch " + b);
                Wish(0f, 0f, b, 1f, out x, out y, out z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "moveX " + b);
                Wish(0f, 0f, 0f, b, out x, out y, out z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "moveY " + b);
            }
        }

        // Catches (final review I4): the top-out carry copying a raw CharacterController velocity above ClimbSpeed (a depenetration spike
        // would launch the climber). The speed never exceeds ClimbSpeed and the direction is kept.
        [Test]
        public void ClampClimbCarry_SpikeAboveClimbSpeed_IsClampedKeepingDirection()
        {
            float vx = 6f, vy = 8f, vz = 0f;   // length 10
            MovementMath.ClampClimbCarry(ref vx, ref vy, ref vz);
            float len = (float)System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
            Assert.AreEqual(MovementMath.ClimbSpeed, len, Tol, "clamped to exactly ClimbSpeed");
            Assert.AreEqual(0.6f, vx / len, Tol, "direction kept (x)");
            Assert.AreEqual(0.8f, vy / len, Tol, "direction kept (y)");

            float a = -50f, b = 20f, c = 30f;
            MovementMath.ClampClimbCarry(ref a, ref b, ref c);
            Assert.LessOrEqual(a * a + b * b + c * c, MovementMath.ClimbSpeed * MovementMath.ClimbSpeed + Tol, "any spike stays within ClimbSpeed");
        }

        // Catches: the clamp touching an ordinary carry (the top-out carry test measures about 1.64 m/s flat) or a hang turning into motion.
        [Test]
        public void ClampClimbCarry_AtOrBelowClimbSpeed_IsUnchanged()
        {
            float vx = 1.2f, vy = -0.5f, vz = 1.1f;
            MovementMath.ClampClimbCarry(ref vx, ref vy, ref vz);
            Assert.AreEqual(1.2f, vx);
            Assert.AreEqual(-0.5f, vy);
            Assert.AreEqual(1.1f, vz);

            float ax = MovementMath.ClimbSpeed, ay = 0f, az = 0f;
            MovementMath.ClampClimbCarry(ref ax, ref ay, ref az);
            Assert.AreEqual(MovementMath.ClimbSpeed, ax, "exactly ClimbSpeed is kept");

            float zx = 0f, zy = 0f, zz = 0f;
            MovementMath.ClampClimbCarry(ref zx, ref zy, ref zz);
            Assert.IsTrue(zx == 0f && zy == 0f && zz == 0f, "a hang stays a hang");
        }

        // Catches: NaN or infinity reaching the locomotion velocity fields.
        [Test]
        public void ClampClimbCarry_NonFinite_IsZero()
        {
            float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float b in bad)
            {
                float x = b, y = 1f, z = 1f;
                MovementMath.ClampClimbCarry(ref x, ref y, ref z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "x " + b);
                x = 1f; y = 1f; z = b;
                MovementMath.ClampClimbCarry(ref x, ref y, ref z);
                Assert.IsTrue(x == 0f && y == 0f && z == 0f, "z " + b);
            }
        }
    }
}
