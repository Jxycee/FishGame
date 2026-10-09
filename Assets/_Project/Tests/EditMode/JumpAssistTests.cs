using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class JumpAssistTests
    {
        [Test]
        public void JumpAssist_PressWithinCoyoteWindow_Jumps()
        {
            var assist = JumpAssist.Create();
            Assert.That(assist.Tick(0.016f, true, false), Is.False); // grounded, no press
            Assert.That(assist.Tick(0.05f, false, false), Is.False); // walked off the edge 0.05 s ago
            Assert.That(assist.Tick(0.04f, false, true), Is.True);   // 0.09 s after leaving ground: still coyote
        }

        [Test]
        public void JumpAssist_PressAfterCoyoteWindow_DoesNotJump()
        {
            var assist = JumpAssist.Create();
            assist.Tick(0.016f, true, false);
            Assert.That(assist.Tick(0.2f, false, true), Is.False); // 0.2 s airborne > 0.1 s coyote
        }

        [Test]
        public void JumpAssist_BufferedPressBeforeLanding_JumpsOnLanding()
        {
            var assist = JumpAssist.Create();
            Assert.That(assist.Tick(0.02f, false, true), Is.False); // pressed in the air
            Assert.That(assist.Tick(0.03f, false, false), Is.False);
            Assert.That(assist.Tick(0.03f, true, false), Is.True);  // lands 0.06 s after the press
        }

        [Test]
        public void JumpAssist_StaleBufferedPress_DoesNotJump()
        {
            var assist = JumpAssist.Create();
            assist.Tick(0.02f, false, true);
            assist.Tick(0.2f, false, false);
            Assert.That(assist.Tick(0f, true, false), Is.False); // press is 0.2 s old > 0.1 s buffer
        }

        [Test]
        public void JumpAssist_AfterJump_DoesNotFireAgainWithoutNewPress()
        {
            var assist = JumpAssist.Create();
            Assert.That(assist.Tick(0.016f, true, true), Is.True);
            Assert.That(float.IsPositiveInfinity(assist.TimeSinceGrounded), Is.True);
            Assert.That(float.IsPositiveInfinity(assist.TimeSinceJumpPressed), Is.True);
            Assert.That(assist.Tick(0.016f, true, false), Is.False); // still grounded next frame, no new press
        }
    }
}
