using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RodWireQuantTests
    {
        [Test]
        public void RodWireQuant_LateralAndTension_RoundTripWithinOneStep()
        {
            for (int i = -100; i <= 100; i++)
            {
                float l = i * 0.01f;
                float back = RodWireQuant.DequantizeLateral(RodWireQuant.QuantizeLateral(l));
                Assert.That(Math.Abs(back - l), Is.LessThanOrEqualTo(1f / 127f), "lateral " + l);
            }
            for (int i = 0; i <= 100; i++)
            {
                float t = i * 0.01f;
                float back = RodWireQuant.DequantizeTension(RodWireQuant.QuantizeTension(t));
                Assert.That(Math.Abs(back - t), Is.LessThanOrEqualTo(1f / 255f), "tension " + t);
            }
        }

        [Test]
        public void RodWireQuant_OutOfRange_Clamps()
        {
            Assert.That(RodWireQuant.QuantizeLateral(2f), Is.EqualTo((sbyte)127));
            Assert.That(RodWireQuant.QuantizeLateral(-2f), Is.EqualTo((sbyte)-127));
            Assert.That(RodWireQuant.QuantizeLateral(float.NaN), Is.EqualTo((sbyte)0));
            Assert.That(RodWireQuant.QuantizeTension(1.5f), Is.EqualTo((byte)255));
            Assert.That(RodWireQuant.QuantizeTension(-1f), Is.EqualTo((byte)0));
            Assert.That(RodWireQuant.QuantizeTension(float.NaN), Is.EqualTo((byte)0));
        }
    }
}
