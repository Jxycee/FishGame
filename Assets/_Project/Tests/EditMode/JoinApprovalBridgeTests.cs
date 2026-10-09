using System.Collections.Generic;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Authority: none (EditMode tests). Covers the approver -> SessionState handoff (contract §7.8, D4).
    public sealed class JoinApprovalBridgeTests
    {
        private static ApprovedJoin MakeJoin(ulong clientId, string id, byte slot)
        {
            return new ApprovedJoin(clientId, new PlayerKey(IdentityKind.Local, id), "Fisher " + (slot + 1), slot, false);
        }

        [Test]
        public void JoinApprovalBridge_TryTakeApproved_ReturnsThenRemovesEntry()
        {
            var bridge = new JoinApprovalBridge();
            bridge.AddApproved(MakeJoin(5UL, "alpha", 1));

            bool first = bridge.TryTakeApproved(5UL, out ApprovedJoin taken);
            bool second = bridge.TryTakeApproved(5UL, out _);

            Assert.IsTrue(first);
            Assert.AreEqual(5UL, taken.ClientId);
            Assert.AreEqual(new PlayerKey(IdentityKind.Local, "alpha"), taken.Key);
            Assert.AreEqual((byte)1, taken.Slot);
            Assert.IsFalse(second);
        }

        [Test]
        public void JoinApprovalBridge_AddApproved_SameClientReplacesEntry()
        {
            var bridge = new JoinApprovalBridge();
            bridge.AddApproved(MakeJoin(7UL, "alpha", 1));
            bridge.AddApproved(MakeJoin(7UL, "beta", 2));

            var pending = new List<ApprovedJoin>();
            bridge.GetPending(pending);

            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual(new PlayerKey(IdentityKind.Local, "beta"), pending[0].Key);
            Assert.AreEqual((byte)2, pending[0].Slot);
        }

        [Test]
        public void JoinApprovalBridge_GetPending_ClearsTargetListFirst()
        {
            var bridge = new JoinApprovalBridge();
            bridge.AddApproved(MakeJoin(1UL, "alpha", 0));
            bridge.AddApproved(MakeJoin(2UL, "beta", 1));

            var pending = new List<ApprovedJoin> { MakeJoin(99UL, "stale", 3) };
            bridge.GetPending(pending);

            Assert.AreEqual(2, pending.Count);
            foreach (ApprovedJoin join in pending)
            {
                Assert.AreNotEqual(99UL, join.ClientId);
            }
        }

        [Test]
        public void JoinApprovalBridge_RemovePending_DropsOnlyThatClient()
        {
            var bridge = new JoinApprovalBridge();
            bridge.AddApproved(MakeJoin(1UL, "alpha", 0));
            bridge.AddApproved(MakeJoin(2UL, "beta", 1));

            bridge.RemovePending(1UL);

            Assert.IsFalse(bridge.TryTakeApproved(1UL, out _));
            Assert.IsTrue(bridge.TryTakeApproved(2UL, out _));
        }

        [Test]
        public void JoinApprovalBridge_Clear_RemovesPendingAndKeepsProvider()
        {
            var bridge = new JoinApprovalBridge();
            var provider = new FakeProvider();
            bridge.Provider = provider;
            bridge.AddApproved(MakeJoin(3UL, "alpha", 0));

            bridge.Clear();

            var pending = new List<ApprovedJoin>();
            bridge.GetPending(pending);
            Assert.AreEqual(0, pending.Count);
            Assert.AreSame(provider, bridge.Provider);
        }

        private sealed class FakeProvider : IJoinContextProvider
        {
            public bool JoinsOpen => true;

            public void GetRoster(List<RosterEntry> into)
            {
                into.Clear();
            }

            public bool TryGetKey(ulong clientId, out PlayerKey key)
            {
                key = default;
                return false;
            }
        }
    }
}
