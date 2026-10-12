using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class LogOnceTests
    {
        [Test]
        public void LogOnce_SameKey_LogsOnce()
        {
            var once = new LogOnce();
            Assert.That(once.ShouldLog("a"), Is.True);
            Assert.That(once.ShouldLog("a"), Is.False);
            Assert.That(once.ShouldLog("a"), Is.False);
            Assert.That(once.ShouldLog("b"), Is.True);
            Assert.That(once.ShouldLog(null), Is.True);
            Assert.That(once.ShouldLog(""), Is.False, "null maps to the empty key");
            Assert.That(once.Count, Is.EqualTo(3));
            Assert.That(once.ShouldLog("A"), Is.True, "keys are ordinal");
            Assert.That(once.Count, Is.EqualTo(4));
        }

        [Test]
        public void LogOnce_Reset_AllowsAgain()
        {
            var once = new LogOnce();
            Assert.That(once.ShouldLog("a"), Is.True);
            once.Reset();
            Assert.That(once.Count, Is.EqualTo(0));
            Assert.That(once.ShouldLog("a"), Is.True);
            Assert.That(once.Count, Is.EqualTo(1));
        }
    }
}
