// Authority: none (pure EditMode tests). FishGame.Core.ClimbGate: the 0.25 s ladder re-entry grace after topping out (spec 7.3, R70).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ClimbGateTests
    {
        // Catches: a gate that starts closed (the first ladder could never be entered).
        [Test]
        public void ClimbGate_Default_CanEnter()
        {
            var gate = new ClimbGate();
            Assert.IsTrue(gate.CanEnter);
            Assert.AreEqual(0f, gate.BlockedFor);
        }

        // Catches: no grace, a wrong length, or a grace that never ends.
        [Test]
        public void ClimbGate_ExitThroughTop_BlocksForTheGraceThenOpens()
        {
            var gate = new ClimbGate();
            gate.OnExit(throughTop: true);
            Assert.IsFalse(gate.CanEnter);
            gate.Tick(0.24f);
            Assert.IsFalse(gate.CanEnter, "0.24 s < 0.25 s");
            gate.Tick(0.011f);
            Assert.IsTrue(gate.CanEnter, "0.251 s");
        }

        // Catches: a grace after every exit (R70 puts it at the top only, open question 4).
        [Test]
        public void ClimbGate_ExitSidewaysOrBottom_NoGrace()
        {
            var gate = new ClimbGate();
            gate.OnExit(throughTop: false);
            Assert.IsTrue(gate.CanEnter);
        }

        // Catches: NaN poisoning (NaN would keep the gate shut forever) and a negative dt extending the grace.
        [Test]
        public void ClimbGate_BadDt_IsIgnored()
        {
            var gate = new ClimbGate();
            gate.OnExit(true);
            gate.Tick(float.NaN);
            gate.Tick(-1f);
            gate.Tick(float.PositiveInfinity);
            gate.Tick(0f);
            Assert.AreEqual(0.25f, gate.BlockedFor, 1e-6f);
        }

        // Catches: a second top exit inside the grace not restarting it, and Reset leaving it shut (teleports and respawns clear it).
        [Test]
        public void ClimbGate_SecondTopExitRestarts_ResetClears()
        {
            var gate = new ClimbGate();
            gate.OnExit(true);
            gate.Tick(0.2f);
            gate.OnExit(true);
            Assert.AreEqual(0.25f, gate.BlockedFor, 1e-6f);
            gate.Reset();
            Assert.IsTrue(gate.CanEnter);
        }
    }
}
