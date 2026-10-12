// T16: CreelEntry is the NGO wire mirror of Core CreelItem (G1). EditMode cannot instantiate NetworkBehaviours, so only the struct is tested here.
using System;
using FishGame.Core;
using FishGame.Gameplay;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class CreelEntryTests
    {
        private static CreelItem Item(bool isJunk, CatchQuality quality)
        {
            return new CreelItem
            {
                EntryId = 7, ItemIndex = 3, IsJunk = isJunk, WeightG = 1234, Quality = quality, ValueCoins = 56, CaughtAtRunMinute = 789,
            };
        }

        private static void AssertSame(CreelItem expected, CreelItem actual)
        {
            Assert.AreEqual(expected.EntryId, actual.EntryId, "EntryId");
            Assert.AreEqual(expected.ItemIndex, actual.ItemIndex, "ItemIndex");
            Assert.AreEqual(expected.IsJunk, actual.IsJunk, "IsJunk");
            Assert.AreEqual(expected.WeightG, actual.WeightG, "WeightG");
            Assert.AreEqual(expected.Quality, actual.Quality, "Quality");
            Assert.AreEqual(expected.ValueCoins, actual.ValueCoins, "ValueCoins");
            Assert.AreEqual(expected.CaughtAtRunMinute, actual.CaughtAtRunMinute, "CaughtAtRunMinute");
        }

        // Catches: a field dropped or swapped in From/ToCore (every field gets a distinct value), IsJunk mapped the wrong way round
        // or always 0/1, and Quality lost or shifted in the byte cast.
        [Test]
        public void CreelEntry_FromAndToCore_RoundTrip()
        {
            CreelItem junkTrophy = Item(true, CatchQuality.Trophy);
            CreelEntry wire = CreelEntry.From(in junkTrophy);
            Assert.AreEqual((byte)1, wire.IsJunk, "junk maps to 1");
            Assert.AreEqual((byte)CatchQuality.Trophy, wire.Quality);
            Assert.AreEqual((ushort)7, wire.EntryId);
            Assert.AreEqual((ushort)3, wire.ItemIndex);
            Assert.AreEqual(1234, wire.WeightG);
            Assert.AreEqual(56, wire.ValueCoins);
            Assert.AreEqual(789, wire.CaughtAtRunMinute);
            AssertSame(junkTrophy, wire.ToCore());

            CreelItem fishNormal = Item(false, CatchQuality.Normal);
            CreelEntry wire2 = CreelEntry.From(in fishNormal);
            Assert.AreEqual((byte)0, wire2.IsJunk, "fish maps to 0");
            Assert.AreEqual((byte)CatchQuality.Normal, wire2.Quality);
            AssertSame(fishNormal, wire2.ToCore());

            foreach (CatchQuality q in new[] { CatchQuality.Normal, CatchQuality.Good, CatchQuality.Trophy })
            {
                CreelItem item = Item(false, q);
                AssertSame(item, CreelEntry.From(in item).ToCore());
            }

            // extremes survive (the wire fields are the same widths as the Core ones)
            var big = new CreelItem
            {
                EntryId = ushort.MaxValue, ItemIndex = ushort.MaxValue, IsJunk = false, WeightG = int.MaxValue, Quality = CatchQuality.Good,
                ValueCoins = int.MaxValue, CaughtAtRunMinute = int.MaxValue,
            };
            AssertSame(big, CreelEntry.From(in big).ToCore());
        }

        // Catches: Equals ignoring one of the 7 fields (the NetworkList would then miss a change to that field), equal entries hashing
        // differently, a field left out of the multiply-add hash (stricter than the contract, which only needs equal => same hash), and
        // Equals(object) disagreeing with Equals(CreelEntry).
        [Test]
        public void CreelEntry_Equality_FieldWise()
        {
            CreelItem item = Item(true, CatchQuality.Good);
            CreelEntry a = CreelEntry.From(in item);
            CreelEntry b = CreelEntry.From(in item);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "equal entries hash equal");

            var changes = new (string field, Action<CreelEntry[]> change)[]
            {
                ("EntryId", e => e[0].EntryId = 8),
                ("ItemIndex", e => e[0].ItemIndex = 4),
                ("IsJunk", e => e[0].IsJunk = 0),
                ("WeightG", e => e[0].WeightG = 1235),
                ("Quality", e => e[0].Quality = (byte)CatchQuality.Trophy),
                ("ValueCoins", e => e[0].ValueCoins = 57),
                ("CaughtAtRunMinute", e => e[0].CaughtAtRunMinute = 790),
            };
            Assert.AreEqual(7, changes.Length, "all 7 fields are covered");
            foreach (var (field, change) in changes)
            {
                var box = new[] { b };
                change(box);
                Assert.IsFalse(a.Equals(box[0]), field + " must take part in Equals");
                Assert.IsFalse(a.Equals((object)box[0]), field + " must take part in Equals(object)");
                Assert.AreNotEqual(a.GetHashCode(), box[0].GetHashCode(), field + " should change the hash (single-field change)");
            }

            Assert.IsFalse(a.Equals((object)null));
            Assert.IsFalse(a.Equals((object)"not an entry"));
        }
    }
}
