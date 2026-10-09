// Tests for FishGame.Core.JoinRules (spec 6.6, R13/R56 reclaim by persistent id; contract 7.3).
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class JoinRulesTests
    {
        private const int Proto = 1;
        private const ulong LocalHash = 0xABCDUL;

        private static readonly PlayerKey Alice = new PlayerKey(IdentityKind.Ugs, "alice");
        private static readonly PlayerKey Bob = new PlayerKey(IdentityKind.Ugs, "bob");

        private static JoinContext Ctx(SessionMode mode = SessionMode.Online, int proto = Proto,
                                       ulong hash = LocalHash, bool joinsOpen = true,
                                       IReadOnlyList<RosterEntry> roster = null)
        {
            return new JoinContext(mode, proto, hash, joinsOpen, NetworkConstants.MaxPlayers,
                roster ?? new RosterEntry[0]);
        }

        private static JoinRequest Req(PlayerKey key, bool self = false, int proto = Proto, ulong hash = LocalHash)
        {
            return new JoinRequest(clientId: self ? 0UL : 1UL, isServerSelf: self, protocolVersion: proto,
                contentHash: hash, key: key, name: key.Id);
        }

        private static RosterEntry Entry(string id, byte slot, bool connected)
        {
            return new RosterEntry(new PlayerKey(IdentityKind.Ugs, id), slot, connected);
        }

        private static RosterEntry[] FourConnected(string firstId = "p0")
        {
            return new[]
            {
                Entry(firstId, 0, true), Entry("p1", 1, true), Entry("p2", 2, true), Entry("p3", 3, true)
            };
        }

        // ---- Rule 1: host self ----

        [Test]
        public void JoinRules_HostSelf_ApprovedEvenWhenSolo()
        {
            var verdict = JoinRules.Evaluate(Req(Alice, self: true, proto: Proto + 5, hash: 7UL),
                Ctx(mode: SessionMode.Solo, joinsOpen: false));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)0, verdict.Slot);
            Assert.IsFalse(verdict.Reclaimed);
        }

        [Test]
        public void JoinRules_HostSelf_ReclaimsOwnDisconnectedSlot()
        {
            var roster = new[] { Entry("alice", 2, false) };
            var verdict = JoinRules.Evaluate(Req(Alice, self: true), Ctx(roster: roster));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)2, verdict.Slot);
            Assert.IsTrue(verdict.Reclaimed);
        }

        // ---- Rules 2-7: rejections ----

        [Test]
        public void JoinRules_SoloRemote_RejectedJoinsClosed()
        {
            var verdict = JoinRules.Evaluate(Req(Bob), Ctx(mode: SessionMode.Solo));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.JoinsClosed, verdict.Reason);
            Assert.AreEqual(JoinVerdict.NoSlot, verdict.Slot);
            Assert.IsFalse(verdict.Reclaimed);
        }

        [Test]
        public void JoinRules_ProtocolMismatch_RejectedVersionMismatch()
        {
            var verdict = JoinRules.Evaluate(Req(Bob, proto: Proto + 1), Ctx());
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.VersionMismatch, verdict.Reason);
        }

        [Test]
        public void JoinRules_ContentHashMismatch_RejectedVersionMismatch()
        {
            var verdict = JoinRules.Evaluate(Req(Bob, hash: LocalHash + 1UL), Ctx());
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.VersionMismatch, verdict.Reason);
        }

        [Test]
        public void JoinRules_JoinsClosed_Rejected()
        {
            var verdict = JoinRules.Evaluate(Req(Bob), Ctx(joinsOpen: false));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.JoinsClosed, verdict.Reason);
        }

        [Test]
        public void JoinRules_Full_RejectedFullLobby()
        {
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: FourConnected()));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.FullLobby, verdict.Reason);
            Assert.AreEqual(JoinVerdict.NoSlot, verdict.Slot);
        }

        [Test]
        public void JoinRules_DuplicateConnectedIdentity_Rejected()
        {
            var roster = new[] { Entry("alice", 0, true) };
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: roster));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.DuplicateIdentity, verdict.Reason);
        }

        [Test]
        public void JoinRules_SameIdDifferentKind_IsNotDuplicate()
        {
            var roster = new[] { Entry("alice", 0, true) };
            var localAlice = new PlayerKey(IdentityKind.Local, "alice");
            var verdict = JoinRules.Evaluate(Req(localAlice), Ctx(roster: roster));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)1, verdict.Slot);
            Assert.IsFalse(verdict.Reclaimed);
        }

        // ---- Rule 8: seating and reclaim ----

        [Test]
        public void JoinRules_DisconnectedIdentity_ReclaimsSlot()
        {
            var roster = new[] { Entry("alice", 2, false), Entry("bob", 0, true) };
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: roster));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)2, verdict.Slot, "The persistent id is matched before the free slot 1 is taken.");
            Assert.IsTrue(verdict.Reclaimed);
        }

        [Test]
        public void JoinRules_NewIdentity_TakesLowestFreeSlot()
        {
            var roster = new[] { Entry("bob", 0, true), Entry("carol", 2, true) };
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: roster));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)1, verdict.Slot);
            Assert.IsFalse(verdict.Reclaimed);
        }

        [Test]
        public void JoinRules_NewIdentity_FreeSlotPreferredOverTakeover()
        {
            var roster = new[] { Entry("gone", 0, false), Entry("bob", 1, true) };
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: roster));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)2, verdict.Slot, "A dropped player's slot is kept for their rejoin while a free slot exists.");
            Assert.IsFalse(verdict.Reclaimed);
        }

        [Test]
        public void JoinRules_AllSlotsHeldButOneDisconnected_TakesOverDisconnectedSlot()
        {
            var roster = new[]
            {
                Entry("p0", 0, true), Entry("p1", 1, true), Entry("p2", 2, true), Entry("gone", 3, false)
            };
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: roster));
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual((byte)3, verdict.Slot);
            Assert.IsFalse(verdict.Reclaimed, "A takeover of another identity's slot is not a reclaim.");
        }

        // ---- Rule order ----

        [Test]
        public void JoinRules_RuleOrder_SoloCheckedBeforeVersion()
        {
            var verdict = JoinRules.Evaluate(Req(Bob, proto: Proto + 1), Ctx(mode: SessionMode.Solo));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.JoinsClosed, verdict.Reason);
        }

        [Test]
        public void JoinRules_RuleOrder_VersionCheckedBeforeJoinsOpen()
        {
            var verdict = JoinRules.Evaluate(Req(Bob, proto: Proto + 1), Ctx(joinsOpen: false));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.VersionMismatch, verdict.Reason);
        }

        [Test]
        public void JoinRules_RuleOrder_JoinsClosedCheckedBeforeFull()
        {
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(joinsOpen: false, roster: FourConnected()));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.JoinsClosed, verdict.Reason);
        }

        [Test]
        public void JoinRules_RuleOrder_FullCheckedBeforeDuplicate()
        {
            var verdict = JoinRules.Evaluate(Req(Alice), Ctx(roster: FourConnected(firstId: "alice")));
            Assert.IsFalse(verdict.Approved);
            Assert.AreEqual(EndReason.FullLobby, verdict.Reason);
        }

        // ---- Helpers on the public surface ----

        [Test]
        public void JoinRules_ChooseSlot_AllConnected_ReturnsNoSlot()
        {
            byte slot = JoinRules.ChooseSlot(Alice, FourConnected(), NetworkConstants.MaxPlayers, out bool reclaimed);
            Assert.AreEqual(JoinVerdict.NoSlot, slot);
            Assert.IsFalse(reclaimed);

            byte first = JoinRules.ChooseSlot(Alice, null, NetworkConstants.MaxPlayers, out bool reclaimedNull);
            Assert.AreEqual((byte)0, first, "A null roster counts as empty.");
            Assert.IsFalse(reclaimedNull);
        }

        // Approval-to-absorb window: slot 2 has the provider's disconnected row (y) AND a pending connected row (a) that took it over.
        private static RosterEntry[] PendingTakeoverRoster()
        {
            return new[]
            {
                Entry("p0", 0, true), Entry("p1", 1, true),
                Entry("y", 2, false), Entry("z", 3, false),
                Entry("a", 2, true),
            };
        }

        [Test]
        public void JoinRules_ChooseSlot_PendingTakeover_SlotNotReissued()
        {
            var b = new PlayerKey(IdentityKind.Ugs, "b");
            byte slot = JoinRules.ChooseSlot(b, PendingTakeoverRoster(), NetworkConstants.MaxPlayers, out bool reclaimed);
            Assert.AreEqual((byte)3, slot);
            Assert.IsFalse(reclaimed);
        }

        [Test]
        public void JoinRules_ChooseSlot_ReclaimWhileSlotHeldByConnected_FallsThrough()
        {
            var y = new PlayerKey(IdentityKind.Ugs, "y");
            byte slot = JoinRules.ChooseSlot(y, PendingTakeoverRoster(), NetworkConstants.MaxPlayers, out bool reclaimed);
            Assert.AreEqual((byte)3, slot);
            Assert.IsFalse(reclaimed);
        }

        [Test]
        public void JoinRules_ConnectedCount_CountsOnlyConnected()
        {
            var roster = new[] { Entry("a", 0, true), Entry("b", 1, false), Entry("c", 2, true) };
            Assert.AreEqual(2, JoinRules.ConnectedCount(roster));
            Assert.AreEqual(0, JoinRules.ConnectedCount(new RosterEntry[0]));
            Assert.AreEqual(0, JoinRules.ConnectedCount(null));
        }

        [Test]
        public void JoinRules_VerdictFactories_SetContractFields()
        {
            var approve = JoinVerdict.Approve(3, true);
            Assert.IsTrue(approve.Approved);
            Assert.AreEqual((byte)3, approve.Slot);
            Assert.IsTrue(approve.Reclaimed);
            Assert.AreEqual(EndReason.UserLeft, approve.Reason);

            var reject = JoinVerdict.Reject(EndReason.FullLobby);
            Assert.IsFalse(reject.Approved);
            Assert.AreEqual(EndReason.FullLobby, reject.Reason);
            Assert.AreEqual(JoinVerdict.NoSlot, reject.Slot);
            Assert.IsFalse(reject.Reclaimed);
        }
    }
}
