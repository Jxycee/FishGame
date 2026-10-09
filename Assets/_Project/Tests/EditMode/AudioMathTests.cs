using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class AudioMathTests
    {
        [Test]
        public void AudioMath_EffectiveVolume_IsSquaredCategoryTimesSquaredMaster()
        {
            Assert.AreEqual(1f, AudioMath.EffectiveVolume(1f, 100, 100), 1e-6f);
            Assert.AreEqual(0.25f, AudioMath.EffectiveVolume(1f, 50, 100), 1e-6f);
            Assert.AreEqual(0.2f, AudioMath.EffectiveVolume(0.8f, 100, 50), 1e-6f);
            Assert.AreEqual(0.5f * 0.49f * 0.64f, AudioMath.EffectiveVolume(0.5f, 70, 80), 1e-6f);
            Assert.AreEqual(0f, AudioMath.EffectiveVolume(1f, 0, 100), 1e-6f);
            Assert.AreEqual(0f, AudioMath.EffectiveVolume(1f, 100, 0), 1e-6f);
            Assert.AreEqual(1f, AudioMath.EffectiveVolume(1f, 150, 100), 1e-6f);   // clamped to 100
            Assert.AreEqual(0f, AudioMath.EffectiveVolume(1f, -20, 100), 1e-6f);   // clamped to 0
        }

        [Test]
        public void AudioMath_DbToLinear_MatchesFormula()
        {
            Assert.AreEqual(1f, AudioMath.DbToLinear(0f), 1e-6f);
            Assert.AreEqual(10f, AudioMath.DbToLinear(20f), 1e-4f);
            Assert.AreEqual(0.1f, AudioMath.DbToLinear(-20f), 1e-6f);
            Assert.AreEqual(0.5f, AudioMath.DbToLinear(-6.0206f), 1e-4f);
        }
    }
}
