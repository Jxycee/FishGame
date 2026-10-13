// Authority: none (pure EditMode tests). FishGame.Core.ToolStowAnim: progress of the procedural rod stow / unstow (R70, contract M2-D31).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ToolStowAnimTests
    {
        // Catches: a retuned duration slipping in unnoticed.
        [Test]
        public void ToolStowAnim_Seconds_Is02()
        {
            Assert.AreEqual(0.2f, ToolStowAnim.Seconds);
        }

        // Catches: a spawn / late join that animates (Create must snap to the current tool).
        [Test]
        public void ToolStowAnim_Create_SnapsToTheCurrentTool()
        {
            ToolStowAnim shown = ToolStowAnim.Create(true);
            ToolStowAnim hidden = ToolStowAnim.Create(false);
            Assert.IsTrue(shown.Visible);
            Assert.AreEqual(0f, shown.Amount);
            Assert.IsFalse(hidden.Visible);
            Assert.AreEqual(1f, hidden.Amount);
        }

        // Catches: hiding at once (no stow animation) or never hiding.
        [Test]
        public void ToolStowAnim_Stow_StaysVisibleUntilTheEnd()
        {
            ToolStowAnim a = ToolStowAnim.Create(true);
            a.Tick(false, 0.1f);
            Assert.AreEqual(0.5f, a.Amount, 1e-6f);
            Assert.IsTrue(a.Visible, "half way: still visible");
            a.Tick(false, 0.1f);
            Assert.AreEqual(1f, a.Amount, 1e-6f);
            Assert.IsFalse(a.Visible, "fully stowed: hidden");
        }

        // Catches: an equip that waits for the end before showing the rod.
        [Test]
        public void ToolStowAnim_Equip_VisibleOnTheFirstTick()
        {
            ToolStowAnim a = ToolStowAnim.Create(false);
            a.Tick(true, 0.01f);
            Assert.IsTrue(a.Visible);
            Assert.AreEqual(0.95f, a.Amount, 1e-6f);
        }

        // Catches: a reversal that snaps to an end instead of continuing from where it is.
        [Test]
        public void ToolStowAnim_ReversalMidway_ContinuesFromTheCurrentAmount()
        {
            ToolStowAnim a = ToolStowAnim.Create(true);
            a.Tick(false, 0.1f);
            a.Tick(true, 0.05f);
            Assert.AreEqual(0.25f, a.Amount, 1e-6f);
        }

        // Catches: a non-smooth or non-monotonic curve, and endpoints off by a step.
        [Test]
        public void ToolStowAnim_Ease_SmoothstepEndpointsAndMidpoint()
        {
            Assert.AreEqual(0f, ToolStowAnim.Ease(0f));
            Assert.AreEqual(1f, ToolStowAnim.Ease(1f));
            Assert.AreEqual(0.5f, ToolStowAnim.Ease(0.5f), 1e-6f);
            Assert.AreEqual(0f, ToolStowAnim.Ease(float.NaN));
            float last = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float e = ToolStowAnim.Ease(i / 20f);
                Assert.Greater(e, last);
                last = e;
            }
        }

        // Catches: NaN or a negative dt corrupting the progress.
        [Test]
        public void ToolStowAnim_BadDt_IsIgnored()
        {
            ToolStowAnim a = ToolStowAnim.Create(true);
            a.Tick(false, float.NaN);
            a.Tick(false, -1f);
            a.Tick(false, float.PositiveInfinity);
            Assert.AreEqual(0f, a.Amount);
        }
    }
}
