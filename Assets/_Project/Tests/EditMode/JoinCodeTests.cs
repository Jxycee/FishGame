using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class JoinCodeTests
    {
        [Test]
        public void JoinCode_Normalize_TrimsAndUppercases()
        {
            Assert.That(JoinCode.Normalize("  ab7x  "), Is.EqualTo("AB7X"));
            Assert.That(JoinCode.Normalize("\tq2Rz\r\n"), Is.EqualTo("Q2RZ"));
            Assert.That(JoinCode.Normalize("ABC123"), Is.EqualTo("ABC123"));
        }

        [Test]
        public void JoinCode_Normalize_Null_ReturnsEmpty()
        {
            Assert.That(JoinCode.Normalize(null), Is.EqualTo(""));
            Assert.That(JoinCode.Normalize("   "), Is.EqualTo(""));
        }

        [Test]
        public void JoinCode_IsPlausible_RejectsSymbolsAndOverlong()
        {
            Assert.That(JoinCode.IsPlausible("ABC123"), Is.True);
            Assert.That(JoinCode.IsPlausible(new string('A', JoinCode.MaxLength)), Is.True);
            Assert.That(JoinCode.IsPlausible(null), Is.False);
            Assert.That(JoinCode.IsPlausible(""), Is.False);
            Assert.That(JoinCode.IsPlausible("ABC-12"), Is.False);
            Assert.That(JoinCode.IsPlausible("AB C1"), Is.False);
            Assert.That(JoinCode.IsPlausible("abc"), Is.False);
            Assert.That(JoinCode.IsPlausible(new string('A', JoinCode.MaxLength + 1)), Is.False);
        }
    }
}
