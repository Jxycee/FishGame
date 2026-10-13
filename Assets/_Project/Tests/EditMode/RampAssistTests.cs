// Authority: none (pure EditMode tests). FishGame.Core.RampAssist and MovementMath.PushingToward: the R70 shore-ramp foot (contract M2-D30).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RampAssistTests
    {
        // Catches: a retuned kick slipping in unnoticed.
        [Test]
        public void RampAssist_Kick_Is2()
        {
            Assert.AreEqual(2.0f, MovementMath.RampAssistKick);
        }

        // Catches: the assist leaking outside the foot (swimming suppressed everywhere, or kicks in open water).
        [Test]
        public void RampAssist_OutsideTheFoot_PassesSwimmingThrough()
        {
            RampAssist a = RampAssist.Create();
            Assert.IsTrue(a.Step(false, true, true, true, out float k1));
            Assert.AreEqual(0f, k1);
            Assert.IsFalse(a.Step(false, true, false, true, out float k2));
            Assert.AreEqual(0f, k2);
        }

        // Catches: no kick, a kick every frame (a launch, not a step), and still swimming inside the foot.
        [Test]
        public void RampAssist_SwimmerEntersPushingTowardTop_KicksOnceAndWades()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, true, out _);                          // swimming toward the ramp
            Assert.IsFalse(a.Step(true, true, true, true, out float kick), "wades in the foot even at swim depth");
            Assert.AreEqual(MovementMath.RampAssistKick, kick);
            Assert.IsFalse(a.Step(true, true, true, true, out float again));
            Assert.AreEqual(0f, again, "one kick per entry");
        }

        // Catches: kicking a wader who walks in (they are already on their feet).
        [Test]
        public void RampAssist_WaderEnters_NoKick()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, false, true, out _);
            a.Step(true, true, false, true, out float kick);
            Assert.AreEqual(0f, kick);
        }

        // Catches: a kick for a swimmer who drifts in without pushing, or a delayed kick when they push later (a random hop).
        [Test]
        public void RampAssist_SwimmerEntersNotPushing_NoKickEvenIfPushingLater()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, false, out _);
            a.Step(true, true, true, false, out float k1);
            a.Step(true, true, true, true, out float k2);
            Assert.AreEqual(0f, k1);
            Assert.AreEqual(0f, k2);
        }

        // Catches: a kick that can only ever fire once per session.
        [Test]
        public void RampAssist_LeaveAndReenterSwimming_KicksAgain()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, true, out _);
            a.Step(true, true, true, true, out float first);
            a.Step(false, true, true, false, out _);                         // back out into deep water, swimming
            a.Step(true, true, true, true, out float second);
            Assert.AreEqual(MovementMath.RampAssistKick, first);
            Assert.AreEqual(MovementMath.RampAssistKick, second);
        }

        // Catches: the foot acting on dry ground (the box top reaches y 0.4 over the ramp).
        [Test]
        public void RampAssist_InFootButOutOfWater_IsIgnored()
        {
            RampAssist a = RampAssist.Create();
            a.Step(false, true, true, true, out _);
            Assert.IsFalse(a.Step(true, false, false, true, out float kick));
            Assert.AreEqual(0f, kick);
        }

        // DA7's rule, now only for the kick. Catches: a sign error (walking down the ramp would kick), and the 0.5 stick threshold lost.
        [Test]
        public void PushingToward_ForwardFacingTheTop_Only()
        {
            Assert.IsTrue(MovementMath.PushingToward(1f, 0f, -1f, 0f, -8f), "facing the top");
            Assert.IsFalse(MovementMath.PushingToward(1f, 0f, 1f, 0f, -8f), "facing away");
            Assert.IsFalse(MovementMath.PushingToward(1f, 1f, 0f, 0f, -8f), "side-on (dot 0)");
            Assert.IsFalse(MovementMath.PushingToward(0.5f, 0f, -1f, 0f, -8f), "half stick is not pushing");
            Assert.IsTrue(MovementMath.PushingToward(1f, 1f, 0f, 0f, 0f), "standing on the top point");
        }
    }
}
