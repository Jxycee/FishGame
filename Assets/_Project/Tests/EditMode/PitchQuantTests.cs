using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class PitchQuantTests
    {
        [Test]
        public void PitchQuant_Quantize_ClampsToLimit()
        {
            Assert.That(PitchQuant.Quantize(90f), Is.EqualTo((short)8500));
            Assert.That(PitchQuant.Quantize(-90f), Is.EqualTo((short)-8500));
            Assert.That(PitchQuant.Quantize(85f), Is.EqualTo((short)8500));
            Assert.That(PitchQuant.Quantize(45.678f), Is.EqualTo((short)4568));
            Assert.That(PitchQuant.Quantize(-45.678f), Is.EqualTo((short)-4568));
            Assert.That(PitchQuant.Quantize(float.NaN), Is.EqualTo((short)0));
        }

        [Test]
        public void PitchQuant_RoundTrip_WithinOneHundredthDegree()
        {
            for (int i = -8500; i <= 8500; i += 37)
            {
                float p = i / 100f + 0.0037f;
                if (p > PitchQuant.LimitDeg) p = PitchQuant.LimitDeg;
                float back = PitchQuant.Dequantize(PitchQuant.Quantize(p));
                Assert.That(back, Is.EqualTo(p).Within(0.01f), "pitch " + p);
            }
        }

        [Test]
        public void PitchQuant_ShouldSend_UsesHalfDegreeThreshold()
        {
            Assert.That(PitchQuant.ShouldSend(0, 49), Is.False);
            Assert.That(PitchQuant.ShouldSend(0, 50), Is.True);
            Assert.That(PitchQuant.ShouldSend(0, -50), Is.True);
            Assert.That(PitchQuant.ShouldSend(-8500, 8500), Is.True);
            Assert.That(PitchQuant.ShouldSend(1234, 1234), Is.False);
        }
    }
}
