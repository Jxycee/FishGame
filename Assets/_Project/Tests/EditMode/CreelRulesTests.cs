using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Creel rules (spec 9.10) and the slot-keyed creel store (R46). Caps used here are the tuning defaults: 12 entries, 15000 g.
    public class CreelRulesTests
    {
        static CreelItem Item(int weightG, int value) =>
            new CreelItem { ItemIndex = 1, IsJunk = false, WeightG = weightG, Quality = CatchQuality.Normal, ValueCoins = value, CaughtAtRunMinute = 3 };

        [Test]
        public void CreelRules_OverweightRule_AcceptsUntilAlreadyFull()
        {
            var nearFull = new CreelSnapshot(11, 14999, 12, 15000);
            var atWeight = new CreelSnapshot(11, 15000, 12, 15000);
            var atEntries = new CreelSnapshot(12, 0, 12, 15000);
            var over = new CreelSnapshot(11, 16000, 12, 15000);
            Assert.That(CreelRules.CanAccept(in nearFull), Is.True, "one gram under the cap still accepts (and may overshoot it)");
            Assert.That(CreelRules.CanAccept(in atWeight), Is.False, "total weight at the cap is full");
            Assert.That(CreelRules.CanAccept(in atEntries), Is.False, "entry count at the cap is full");
            Assert.That(CreelRules.CanAccept(in over), Is.False);
            Assert.That(CreelRules.IsFull(in atWeight), Is.True);
            Assert.That(CreelRules.IsFull(in nearFull), Is.False);
            var empty = new CreelSnapshot(0, 0, 12, 15000);
            Assert.That(CreelRules.CanAccept(in empty), Is.True);
            Assert.That(CreelRules.IsFull(in empty), Is.False);
        }

        [Test]
        public void CreelRules_Warn_AtEightyPercent()
        {
            var e9 = new CreelSnapshot(9, 0, 12, 15000);
            var e10 = new CreelSnapshot(10, 0, 12, 15000);
            var w11999 = new CreelSnapshot(1, 11999, 12, 15000);
            var w12000 = new CreelSnapshot(1, 12000, 12, 15000);
            Assert.That(CreelRules.IsWarn(in e9), Is.False, "9 of 12 is 75 percent");
            Assert.That(CreelRules.IsWarn(in e10), Is.True, "10 of 12 passes 80 percent (9.6)");
            Assert.That(CreelRules.IsWarn(in w11999), Is.False);
            Assert.That(CreelRules.IsWarn(in w12000), Is.True, "exactly 80 percent of the weight cap warns");
            var both = new CreelSnapshot(10, 12500, 12, 15000);
            Assert.That(CreelRules.IsWarn(in both), Is.True);
            var empty = new CreelSnapshot(0, 0, 12, 15000);
            Assert.That(CreelRules.IsWarn(in empty), Is.False);
        }

        // Catches: the entries comparison written with > instead of >= when the entry count is exactly on the 80 percent line
        // (8 of 10 is 8.0: 0.8 * 10 must not land a hair above 8 in float or double maths).
        [Test]
        public void CreelRules_Warn_EntriesExactlyOnTheEightyPercentLine()
        {
            var on = new CreelSnapshot(8, 0, 10, 15000);
            var under = new CreelSnapshot(7, 0, 10, 15000);
            Assert.That(CreelRules.IsWarn(in on), Is.True, "8 of 10 is exactly 80 percent");
            Assert.That(CreelRules.IsWarn(in under), Is.False, "7 of 10 is 70 percent");
        }

        // Catches: an off-by-one in the slot bounds check (last valid slot rejected), a constructor that accepts zero slots.
        [Test]
        public void CreelLedger_LastValidSlot_Works_AndZeroSlotsThrows()
        {
            var ledger = new CreelLedger(4);
            CreelItem stored = ledger.Add(3, Item(700, 9));
            Assert.That(stored.EntryId, Is.EqualTo((ushort)1));
            Assert.That(ledger.Items(3).Count, Is.EqualTo(1));
            Assert.That(ledger.Snapshot(3, 12, 15000).TotalWeightG, Is.EqualTo(700));
            Assert.That(ledger.SellAll(3), Is.EqualTo(9));
            Assert.That(ledger.DebugCoins(3), Is.EqualTo(9));
            Assert.That(ledger.Items(2), Is.Empty, "the neighbour slot is untouched");

            Assert.Throws<ArgumentOutOfRangeException>(() => new CreelLedger(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreelLedger(-1));
            Assert.DoesNotThrow(() => new CreelLedger(1));
        }

        [Test]
        public void CreelLedger_SellAll_ClearsSlotAndAddsCoins()
        {
            var ledger = new CreelLedger(4);
            ledger.Add(2, Item(1000, 5));
            ledger.Add(2, Item(2500, 12));
            ledger.Add(0, Item(300, 7));
            CreelSnapshot snap = ledger.Snapshot(2, 12, 15000);
            Assert.That(snap.Entries, Is.EqualTo(2));
            Assert.That(snap.TotalWeightG, Is.EqualTo(3500));
            Assert.That(snap.MaxEntries, Is.EqualTo(12));
            Assert.That(snap.MaxWeightG, Is.EqualTo(15000));
            Assert.That(ledger.SellAll(2), Is.EqualTo(17));
            Assert.That(ledger.Items(2), Is.Empty);
            Assert.That(ledger.DebugCoins(2), Is.EqualTo(17));
            Assert.That(ledger.Items(0).Count, Is.EqualTo(1), "slot 0 untouched");
            Assert.That(ledger.DebugCoins(0), Is.EqualTo(0));
            Assert.That(ledger.Snapshot(2, 12, 15000).Entries, Is.EqualTo(0));
            ledger.Add(2, Item(100, 3));
            Assert.That(ledger.SellAll(2), Is.EqualTo(3));
            Assert.That(ledger.DebugCoins(2), Is.EqualTo(20), "sales accumulate");
            Assert.That(ledger.SellAll(1), Is.EqualTo(0), "selling an empty creel pays nothing");

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.SellAll(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Add(4, Item(1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Items(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Snapshot(4, 12, 15000));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.DebugCoins(4));
        }

        [Test]
        public void CreelLedger_EntryIds_MonotonicPerSlot()
        {
            var ledger = new CreelLedger(4);
            Assert.That(ledger.Add(0, Item(1, 1)).EntryId, Is.EqualTo((ushort)1));
            Assert.That(ledger.Add(0, Item(1, 1)).EntryId, Is.EqualTo((ushort)2));
            Assert.That(ledger.Add(0, Item(1, 1)).EntryId, Is.EqualTo((ushort)3));
            ledger.SellAll(0);
            CreelItem next = ledger.Add(0, Item(1, 1));
            Assert.That(next.EntryId, Is.EqualTo((ushort)4), "the counter is never reset by SellAll");
            Assert.That(ledger.Items(0)[0].EntryId, Is.EqualTo((ushort)4), "the stored item carries the assigned id");
            Assert.That(ledger.Add(1, Item(1, 1)).EntryId, Is.EqualTo((ushort)1), "slot 1 has its own counter");
            Assert.That(ledger.Add(0, new CreelItem { EntryId = 99 }).EntryId, Is.EqualTo((ushort)5), "a caller-supplied id is overwritten");
        }
    }
}
