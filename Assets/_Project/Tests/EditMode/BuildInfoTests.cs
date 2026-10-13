using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class BuildInfoTests
    {
        [Test]
        public void BuildInfo_EffectiveProtocolVersion_AddsDevOffset()
        {
            Assert.That(BuildInfo.ProtocolVersion, Is.EqualTo(3), "R72 bumped the wire protocol (FlagRight, FlagDive meaning, host fight validation)");
            Assert.That(BuildInfo.GameVersion, Is.EqualTo("0.1.0"));
            Assert.That(BuildInfo.EffectiveProtocolVersion(0), Is.EqualTo(BuildInfo.ProtocolVersion));
            Assert.That(BuildInfo.EffectiveProtocolVersion(1), Is.EqualTo(BuildInfo.ProtocolVersion + 1));
        }
    }
}
