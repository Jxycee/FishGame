using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class InstanceProfileTests
    {
        [Test]
        public void InstanceProfile_NoArgs_IsMain()
        {
            InstanceProfile profile = InstanceProfile.FromArgs(new string[0]);
            Assert.AreEqual("", profile.Name);
            Assert.AreEqual("", profile.Suffix);
            Assert.IsFalse(profile.IsMppmClone);

            Assert.AreEqual("", InstanceProfile.FromArgs(null).Suffix);
            Assert.AreEqual("", InstanceProfile.FromArgs(new[] { "-editor-mode" }).Suffix);          // no -name
            Assert.AreEqual("", InstanceProfile.FromArgs(new[] { "-name", "Player 2" }).Suffix);     // no -editor-mode
            Assert.AreEqual("", InstanceProfile.FromArgs(new[] { "-editor-mode", "-name" }).Suffix); // -name without value
        }

        [Test]
        public void InstanceProfile_MppmEditorModeName_UsesName()
        {
            InstanceProfile profile = InstanceProfile.FromArgs(new[] { "Unity.exe", "-editor-mode", "-name", "Player 2" });
            Assert.AreEqual("player2", profile.Name);
            Assert.AreEqual("_player2", profile.Suffix);
            Assert.IsTrue(profile.IsMppmClone);

            InstanceProfile reordered = InstanceProfile.FromArgs(new[] { "-name", "Clone_2", "-editor-mode" });
            Assert.AreEqual("clone_2", reordered.Name);
            Assert.IsTrue(reordered.IsMppmClone);
        }

        [Test]
        public void InstanceProfile_DevProfile_Wins()
        {
            InstanceProfile profile = InstanceProfile.FromArgs(new[] { "-editor-mode", "-name", "p2", "-dev-profile", "fgtest" });
            Assert.AreEqual("fgtest", profile.Name);
            Assert.AreEqual("_fgtest", profile.Suffix);
            Assert.IsFalse(profile.IsMppmClone);
        }

        [Test]
        public void InstanceProfile_Sanitize_LowercasesAndFilters()
        {
            Assert.AreEqual("myplayer1", InstanceProfile.Sanitize("My Player!@#1"));
            Assert.AreEqual("a_b-c", InstanceProfile.Sanitize("A_b-C"));
            Assert.AreEqual(30, InstanceProfile.Sanitize(new string('a', 40)).Length);
            Assert.AreEqual("", InstanceProfile.Sanitize(null));
            Assert.AreEqual("", InstanceProfile.Sanitize("!!!"));
        }
    }
}
