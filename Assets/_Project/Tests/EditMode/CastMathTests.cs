using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class CastMathTests
    {
        const float Eps = 1e-5f;

        [Test]
        public void CastMath_ChargeNorm_PingPongAtSampleTimes()
        {
            float[] held = { 0f, 0.5f, 1f, 1.5f, 2f };
            float[] want = { 0f, 0.5f, 1f, 0.5f, 0f };
            for (int i = 0; i < held.Length; i++)
                Assert.That(CastMath.ChargeNorm(held[i], 1f, true), Is.EqualTo(want[i]).Within(Eps), "held " + held[i]);
            Assert.That(CastMath.ChargeNorm(-1f, 1f, true), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void CastMath_ChargeNorm_NoPingPong_Clamps()
        {
            Assert.That(CastMath.ChargeNorm(0.5f, 1f, false), Is.EqualTo(0.5f).Within(Eps));
            Assert.That(CastMath.ChargeNorm(1f, 1f, false), Is.EqualTo(1f).Within(Eps));
            Assert.That(CastMath.ChargeNorm(1.5f, 1f, false), Is.EqualTo(1f).Within(Eps));
            Assert.That(CastMath.ChargeNorm(3f, 1f, false), Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void CastMath_Distance_MonotonicAndWithinHalfMeterOfFormula()
        {
            float[] charge = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            float prev = float.NegativeInfinity;
            foreach (float c in charge)
            {
                float d = CastMath.Distance(c, 2.5f, 18f);
                Assert.That(d, Is.GreaterThan(prev), "charge " + c);
                Assert.That(d, Is.EqualTo(2.5f + 15.5f * c).Within(0.5f), "charge " + c);
                prev = d;
            }
            Assert.That(CastMath.Distance(1.5f, 2.5f, 18f), Is.EqualTo(18f).Within(Eps));
        }

        [Test]
        public void CastMath_FlightTimeAndApex_MatchFormulas()
        {
            Assert.That(CastMath.FlightTime(30f), Is.EqualTo(0.81f).Within(Eps));
            Assert.That(CastMath.FlightTime(10.25f), Is.EqualTo(0.573f).Within(Eps));
            Assert.That(CastMath.Apex(30f, 1f), Is.EqualTo(3.6f).Within(Eps));
            Assert.That(CastMath.Apex(10f, 1.3f), Is.EqualTo(1.56f).Within(Eps));
        }

        [Test]
        public void CastMath_Apex_NeverBelowPointSixMeters()
        {
            // C 7.5 applies the 0.6 floor before the pitch multiplier, so the multiplier is fixed at 1 here.
            foreach (float d in new[] { 0f, 1f, 2.5f, 5f })
                Assert.That(CastMath.Apex(d, 1f), Is.GreaterThanOrEqualTo(0.6f - Eps), "distance " + d);
            Assert.That(CastMath.Apex(2.5f, 1f), Is.EqualTo(0.6f).Within(Eps));
        }

        [Test]
        public void CastMath_PitchApexMult_ClampedPointSevenToOnePointThree()
        {
            Assert.That(CastMath.PitchApexMult(-90f), Is.EqualTo(0.7f).Within(Eps));
            Assert.That(CastMath.PitchApexMult(-30f), Is.EqualTo(0.7f).Within(Eps));
            Assert.That(CastMath.PitchApexMult(7.5f), Is.EqualTo(1.0f).Within(Eps));
            Assert.That(CastMath.PitchApexMult(45f), Is.EqualTo(1.3f).Within(Eps));
            Assert.That(CastMath.PitchApexMult(90f), Is.EqualTo(1.3f).Within(Eps));
        }

        // R67: the landing is origin + yawForward * (CastTipReach + distance); the distance argument is measured from the rod tip.
        [Test]
        public void CastMath_Landing_UsesYawOnly()
        {
            const float reach = FishingConstants.CastTipReach;
            float x, z;
            CastMath.Landing(1f, 2f, 0f, 10f, out x, out z);
            Assert.That(x, Is.EqualTo(1f).Within(1e-4f)); Assert.That(z, Is.EqualTo(12f + reach).Within(1e-4f));
            CastMath.Landing(1f, 2f, 90f, 10f, out x, out z);
            Assert.That(x, Is.EqualTo(11f + reach).Within(1e-4f)); Assert.That(z, Is.EqualTo(2f).Within(1e-4f));
            CastMath.Landing(1f, 2f, 180f, 10f, out x, out z);
            Assert.That(x, Is.EqualTo(1f).Within(1e-4f)); Assert.That(z, Is.EqualTo(-8f - reach).Within(1e-4f));
        }

        // Catches (R67): the cast distance measured from the player root instead of the rod tip (a min-charge cast then lands only 2.5 m out,
        // inside the tip's own reach, and the owner's line starts at <= landDistance); the tip reach dropped from either end of the range;
        // the reach applied as a function of charge or pitch instead of a fixed constant.
        [Test]
        public void CastMath_Landing_MinAndMaxCharge_AreMeasuredFromTheRodTip()
        {
            const float minDist = 2.5f, maxDist = 18f, reach = FishingConstants.CastTipReach;
            float x, z;

            CastMath.Landing(0f, 0f, 0f, CastMath.Distance(0f, minDist, maxDist), out x, out z);
            Assert.That(x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(z, Is.EqualTo(reach + minDist).Within(1e-4f), "min charge: castTipReach + castMinDist along the yaw");

            CastMath.Landing(0f, 0f, 0f, CastMath.Distance(1f, minDist, maxDist), out x, out z);
            Assert.That(z, Is.EqualTo(reach + maxDist).Within(1e-4f), "max charge: castTipReach + castMaxDist along the yaw");

            // Any yaw and origin: the horizontal distance from the origin is exactly reach + distance.
            foreach (float yaw in new[] { 0f, 25f, 90f, 137.5f, 180f, 271f })
            {
                CastMath.Landing(3f, -7f, yaw, minDist, out x, out z);
                float flat = (float)System.Math.Sqrt((x - 3f) * (x - 3f) + (z + 7f) * (z + 7f));
                Assert.That(flat, Is.EqualTo(reach + minDist).Within(1e-4f), "yaw " + yaw);
            }
        }

        // The tip reach is a rig-geometry constant between 1 and 4 m (a negative or huge value is a typo, not a rig).
        [Test]
        public void FishingConstants_CastTipReach_IsAPlausibleRoundedRigDistance()
        {
            Assert.That(FishingConstants.CastTipReach, Is.InRange(1f, 4f));
            Assert.That(FishingConstants.CastTipReach * 10f, Is.EqualTo(System.Math.Round(FishingConstants.CastTipReach * 10f)).Within(1e-3f), "rounded to 0.1 m");
        }

        [Test]
        public void CastMath_ArcPoint_EndpointsAndApex()
        {
            float x, y, z;
            CastMath.ArcPoint(0f, 1f, 1f, 2f, 11f, 3f, 12f, 2f, out x, out y, out z);
            Assert.That(x, Is.EqualTo(1f)); Assert.That(y, Is.EqualTo(1f)); Assert.That(z, Is.EqualTo(2f));
            CastMath.ArcPoint(1f, 1f, 1f, 2f, 11f, 3f, 12f, 2f, out x, out y, out z);
            Assert.That(x, Is.EqualTo(11f)); Assert.That(y, Is.EqualTo(3f)); Assert.That(z, Is.EqualTo(12f));
            CastMath.ArcPoint(0.5f, 1f, 1f, 2f, 11f, 3f, 12f, 2f, out x, out y, out z);
            Assert.That(y, Is.EqualTo(3f + 2f).Within(1e-5f), "apex above the higher end");
            Assert.That(x, Is.EqualTo(6f).Within(1e-5f)); Assert.That(z, Is.EqualTo(7f).Within(1e-5f));
            // Start higher than end: the apex is above the start.
            CastMath.ArcPoint(0.5f, 0f, 4f, 0f, 8f, 0f, 8f, 1.5f, out x, out y, out z);
            Assert.That(y, Is.EqualTo(4f + 1.5f).Within(1e-5f));
        }

        [Test]
        public void CastMath_IsValidLanding_RequiresWaterFirstAndMinDepth()
        {
            var ok = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 0.8f };
            Assert.That(CastMath.IsValidLanding(in ok, 0.8f), Is.True);
            Assert.That(CastMath.ProbeReason(in ok, 0.8f), Is.EqualTo(CastRejectReason.None));

            var shallow = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 0.79f };
            Assert.That(CastMath.IsValidLanding(in shallow, 0.8f), Is.False);
            Assert.That(CastMath.ProbeReason(in shallow, 0.8f), Is.EqualTo(CastRejectReason.TooShallow));

            var noBed = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = false, Depth = 5f };
            Assert.That(CastMath.IsValidLanding(in noBed, 0.8f), Is.False);
            Assert.That(CastMath.ProbeReason(in noBed, 0.8f), Is.EqualTo(CastRejectReason.TooShallow));

            var land = new WaterProbe { FirstHitIsWater = false, WaterY = 0f, HasBed = true, Depth = 5f };
            Assert.That(CastMath.IsValidLanding(in land, 0.8f), Is.False);
            Assert.That(CastMath.ProbeReason(in land, 0.8f), Is.EqualTo(CastRejectReason.NotWater));
        }

        [Test]
        public void CastMath_Apex_FloorAppliesBeforeMultiplier()
        {
            Assert.That(CastMath.Apex(2.5f, 0.7f), Is.EqualTo(0.42f).Within(Eps));
        }

        [Test]
        public void CastMath_ChargeNorm_NaNAndInfinityGuards()
        {
            Assert.That(CastMath.ChargeNorm(float.NaN, 1f, true), Is.EqualTo(0f));
            Assert.That(CastMath.ChargeNorm(1f, 0f, true), Is.EqualTo(0f));
            Assert.That(CastMath.ChargeNorm(float.PositiveInfinity, 1f, true), Is.EqualTo(0f));
            Assert.That(CastMath.ChargeNorm(float.PositiveInfinity, 1f, false), Is.EqualTo(1f));
            Assert.That(CastMath.Distance(float.NaN, 2.5f, 18f), Is.EqualTo(2.5f).Within(Eps));
            var nanDepth = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = float.NaN };
            Assert.That(CastMath.IsValidLanding(in nanDepth, 0.8f), Is.False);
            Assert.That(CastMath.ProbeReason(in nanDepth, 0.8f), Is.EqualTo(CastRejectReason.TooShallow));
        }
    }
}
