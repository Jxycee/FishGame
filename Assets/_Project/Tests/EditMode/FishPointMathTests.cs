using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class FishPointMathTests
    {
        [Test]
        public void FishPointMath_PositiveLateral_IsRightOfCastLine()
        {
            float x, z;
            FishPointMath.FishPoint(0f, 0f, 0f, 0.5f, 75f, 10f, out x, out z);
            Assert.That(x, Is.GreaterThan(0f));
            FishPointMath.FishPoint(0f, 0f, 0f, -0.5f, 75f, 10f, out x, out z);
            Assert.That(x, Is.LessThan(0f));
            FishPointMath.FishPoint(0f, 0f, 0f, 0f, 75f, 10f, out x, out z);
            Assert.That(x, Is.EqualTo(0f).Within(1e-4f)); Assert.That(z, Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void FishPointMath_LateralOne_IsAtMaxAngle()
        {
            float x, z;
            FishPointMath.FishPoint(0f, 0f, 0f, 1f, 75f, 10f, out x, out z);
            Assert.That(x, Is.EqualTo(10f * (float)Math.Sin(75.0 * Math.PI / 180.0)).Within(1e-3f));
            Assert.That(z, Is.EqualTo(10f * (float)Math.Cos(75.0 * Math.PI / 180.0)).Within(1e-3f));
            Assert.That(x, Is.EqualTo(9.659f).Within(1e-3f)); Assert.That(z, Is.EqualTo(2.588f).Within(1e-3f));
        }

        [Test]
        public void FishPointMath_NonzeroTipAndYaw_OffsetsAndRotates()
        {
            float x, z;
            FishPointMath.FishPoint(3f, -4f, 90f, 0f, 75f, 10f, out x, out z);
            Assert.That(x, Is.EqualTo(13f).Within(1e-3f)); Assert.That(z, Is.EqualTo(-4f).Within(1e-3f));
            // +lateral turns clockwise: 90 + 75 = 165 degrees.
            FishPointMath.FishPoint(3f, -4f, 90f, 1f, 75f, 10f, out x, out z);
            double a = 165.0 * Math.PI / 180.0;
            Assert.That(x, Is.EqualTo(3f + 10f * (float)Math.Sin(a)).Within(1e-3f));
            Assert.That(z, Is.EqualTo(-4f + 10f * (float)Math.Cos(a)).Within(1e-3f));
        }
    }
}
