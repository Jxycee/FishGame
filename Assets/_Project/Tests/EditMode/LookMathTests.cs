// Authority: none (pure EditMode tests). FishGame.Core.LookMath.
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class LookMathTests
    {
        [Test]
        public void LookMath_Mouse_ScalesWithoutDt()
        {
            Assert.AreEqual(1.2f, LookMath.MouseToDegrees(10f, 0.12f), 1e-5f);
            Assert.AreEqual(-0.6f, LookMath.MouseToDegrees(-5f, 0.12f), 1e-5f);
            Assert.AreEqual(0f, LookMath.MouseToDegrees(0f, 0.3f), 0f);
        }

        [Test]
        public void LookMath_Stick_IsSquaredAndSigned()
        {
            Assert.AreEqual(45f, LookMath.StickToDegrees(0.5f, 1f, 1f), 1e-4f);
            Assert.AreEqual(-45f, LookMath.StickToDegrees(-0.5f, 1f, 1f), 1e-4f);
            Assert.AreEqual(180f, LookMath.StickToDegrees(1f, 2f, 0.5f), 1e-4f);
            Assert.AreEqual(3f, LookMath.StickToDegrees(1f, 1f, 1f / 60f), 1e-4f);
            Assert.AreEqual(0f, LookMath.StickToDegrees(0f, 1f, 1f), 0f);
        }
    }
}
