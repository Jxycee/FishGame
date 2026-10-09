// Tests for FishGame.Core.ConnectionStateMachine (spec 6.4, contract 7.3 and D9).
using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ConnectionStateMachineTests
    {
        // The exact 8 legal transitions (contract 7.3). Every other pair of the 5 x 6 grid is illegal.
        private static readonly (ConnState From, ConnEvent Event, ConnState To)[] LegalTable =
        {
            (ConnState.Offline, ConnEvent.Begin, ConnState.Connecting),
            (ConnState.Connecting, ConnEvent.Established, ConnState.Connected),
            (ConnState.Connecting, ConnEvent.Fail, ConnState.Failed),
            (ConnState.Connected, ConnEvent.UnexpectedStop, ConnState.Failed),
            (ConnState.Connected, ConnEvent.Leave, ConnState.ShuttingDown),
            (ConnState.Failed, ConnEvent.Leave, ConnState.ShuttingDown),
            (ConnState.Failed, ConnEvent.ShutdownComplete, ConnState.Offline),
            (ConnState.ShuttingDown, ConnEvent.ShutdownComplete, ConnState.Offline),
        };

        private static ConnState[] AllStates()
        {
            return (ConnState[])Enum.GetValues(typeof(ConnState));
        }

        private static ConnEvent[] AllEvents()
        {
            return (ConnEvent[])Enum.GetValues(typeof(ConnEvent));
        }

        private static bool IsLegal(ConnState from, ConnEvent ev)
        {
            foreach (var t in LegalTable)
            {
                if (t.From == from && t.Event == ev) return true;
            }
            return false;
        }

        public static IEnumerable<TestCaseData> LegalCases()
        {
            foreach (var t in LegalTable)
            {
                yield return new TestCaseData(t.From, t.Event, t.To);
            }
        }

        public static IEnumerable<TestCaseData> IllegalCases()
        {
            foreach (ConnState from in AllStates())
            {
                foreach (ConnEvent ev in AllEvents())
                {
                    if (!IsLegal(from, ev)) yield return new TestCaseData(from, ev);
                }
            }
        }

        // Drives a fresh machine into the target state using only legal events.
        private static ConnectionStateMachine MachineAt(ConnState target)
        {
            var m = new ConnectionStateMachine();
            switch (target)
            {
                case ConnState.Offline:
                    break;
                case ConnState.Connecting:
                    m.Fire(ConnEvent.Begin);
                    break;
                case ConnState.Connected:
                    m.Fire(ConnEvent.Begin);
                    m.Fire(ConnEvent.Established);
                    break;
                case ConnState.Failed:
                    m.Fire(ConnEvent.Begin);
                    m.Fire(ConnEvent.Fail, EndReason.Timeout);
                    break;
                case ConnState.ShuttingDown:
                    m.Fire(ConnEvent.Begin);
                    m.Fire(ConnEvent.Established);
                    m.Fire(ConnEvent.Leave);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }
            Assert.AreEqual(target, m.State, "Setup failed to reach the target state.");
            return m;
        }

        [Test]
        public void ConnectionStateMachine_TransitionGrid_Has8LegalPairsOf30()
        {
            Assert.AreEqual(5, AllStates().Length, "ConnState must have exactly 5 values.");
            Assert.AreEqual(6, AllEvents().Length, "ConnEvent must have exactly 6 values.");

            int legal = 0;
            foreach (ConnState from in AllStates())
            {
                foreach (ConnEvent ev in AllEvents())
                {
                    if (ConnectionStateMachine.TryNext(from, ev, out _)) legal++;
                }
            }
            Assert.AreEqual(8, legal);
        }

        [TestCaseSource(nameof(LegalCases))]
        public void ConnectionStateMachine_LegalTransitions_ReachExpectedState(ConnState from, ConnEvent ev, ConnState to)
        {
            Assert.IsTrue(ConnectionStateMachine.TryNext(from, ev, out ConnState next));
            Assert.AreEqual(to, next);
            Assert.AreEqual(to, ConnectionStateMachine.Next(from, ev));

            var fired = MachineAt(from);
            fired.Fire(ev);
            Assert.AreEqual(to, fired.State);

            var tried = MachineAt(from);
            Assert.IsTrue(tried.TryFire(ev));
            Assert.AreEqual(to, tried.State);
        }

        [TestCaseSource(nameof(IllegalCases))]
        public void ConnectionStateMachine_IllegalPairs_ThrowInvalidTransition(ConnState from, ConnEvent ev)
        {
            var staticEx = Assert.Throws<InvalidTransitionException>(() => ConnectionStateMachine.Next(from, ev));
            Assert.AreEqual(from, staticEx.From);
            Assert.AreEqual(ev, staticEx.Event);
            Assert.IsFalse(ConnectionStateMachine.TryNext(from, ev, out _));

            var m = MachineAt(from);
            EndReason reasonBefore = m.FailureReason;
            int changedCount = 0;
            m.Changed += (a, b) => changedCount++;

            var ex = Assert.Throws<InvalidTransitionException>(() => m.Fire(ev, EndReason.Kicked));
            Assert.AreEqual(from, ex.From);
            Assert.AreEqual(ev, ex.Event);
            Assert.IsFalse(m.TryFire(ev, EndReason.Kicked));

            Assert.AreEqual(from, m.State, "State must not change on an illegal event.");
            Assert.AreEqual(reasonBefore, m.FailureReason, "FailureReason must not change on an illegal event.");
            Assert.AreEqual(0, changedCount, "Changed must not fire on an illegal event.");
        }

        [Test]
        public void ConnectionStateMachine_InvalidTransition_MessageMatchesFormat()
        {
            var ex = new InvalidTransitionException(ConnState.Offline, ConnEvent.Established);
            Assert.AreEqual("Invalid connection transition: Offline --Established-->", ex.Message);
            Assert.IsInstanceOf<InvalidOperationException>(ex);
        }

        [Test]
        public void ConnectionStateMachine_Fail_RecordsReason()
        {
            var m = new ConnectionStateMachine();
            Assert.AreEqual(ConnState.Offline, m.State);
            Assert.AreEqual(EndReason.Unknown, m.FailureReason);

            m.Fire(ConnEvent.Begin);
            m.Fire(ConnEvent.Fail, EndReason.Timeout);
            Assert.AreEqual(ConnState.Failed, m.State);
            Assert.AreEqual(EndReason.Timeout, m.FailureReason);

            m.Fire(ConnEvent.Leave);
            m.Fire(ConnEvent.ShutdownComplete);
            Assert.AreEqual(ConnState.Offline, m.State);
            Assert.AreEqual(EndReason.Timeout, m.FailureReason, "The reason survives until the next Begin, so the menu can show it.");

            m.Fire(ConnEvent.Begin);
            Assert.AreEqual(EndReason.Unknown, m.FailureReason, "Begin resets the failure reason.");
        }

        [Test]
        public void ConnectionStateMachine_UnexpectedStop_RecordsReason()
        {
            var m = MachineAt(ConnState.Connected);
            m.Fire(ConnEvent.UnexpectedStop, EndReason.HostLeft);
            Assert.AreEqual(ConnState.Failed, m.State);
            Assert.AreEqual(EndReason.HostLeft, m.FailureReason);
        }

        [Test]
        public void ConnectionStateMachine_Changed_RaisedAfterStateUpdate()
        {
            var m = new ConnectionStateMachine();
            var raised = new List<(ConnState From, ConnState To, ConnState StateInHandler)>();
            m.Changed += (from, to) => raised.Add((from, to, m.State));

            m.Fire(ConnEvent.Begin);
            Assert.IsFalse(m.TryFire(ConnEvent.Begin));

            Assert.AreEqual(1, raised.Count, "Changed fires once, and not for the rejected event.");
            Assert.AreEqual(ConnState.Offline, raised[0].From);
            Assert.AreEqual(ConnState.Connecting, raised[0].To);
            Assert.AreEqual(ConnState.Connecting, raised[0].StateInHandler);
        }
    }
}
