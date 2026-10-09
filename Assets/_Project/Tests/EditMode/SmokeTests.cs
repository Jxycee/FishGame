using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // EditMode smoke test (T1). Proves the test asmdef compiles and the runner executes it.
    public class SmokeTests
    {
        [Test]
        public void Smoke_EditModeAssembly_Runs()
        {
            Assert.AreEqual(2, 1 + 1);
        }
    }
}
