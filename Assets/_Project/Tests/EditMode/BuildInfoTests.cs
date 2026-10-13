using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class BuildInfoTests
    {
        [Test]
        public void BuildInfo_EffectiveProtocolVersion_AddsDevOffset()
        {
            Assert.That(BuildInfo.ProtocolVersion, Is.EqualTo(4), "M3 T14: the in-scene WorldState with four NetworkVariables (R72 took 3)");
            Assert.That(BuildInfo.GameVersion, Is.EqualTo("0.1.0"));
            Assert.That(BuildInfo.EffectiveProtocolVersion(0), Is.EqualTo(BuildInfo.ProtocolVersion));
            Assert.That(BuildInfo.EffectiveProtocolVersion(1), Is.EqualTo(BuildInfo.ProtocolVersion + 1));
        }
    }
}
