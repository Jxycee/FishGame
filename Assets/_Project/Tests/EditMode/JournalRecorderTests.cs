using System.Globalization;
using System.Threading;
using FishGame.Core;
using FishGame.Fishing;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // JournalRecorder.Record is the pure part of the CatchAnnounced handler (T20). Fishing grants this assembly its internals.
    public class JournalRecorderTests
    {
        private const string Spot = "lake_dock";

        private static CatchAnnouncement Announce(AnnounceKind kind, int weightG, bool junk = false, ushort item = 0, ushort lengthMm = 300)
        {
            var c = new CatchResult { IsJunk = junk, ItemIndex = item, WeightG = weightG, LengthMm = lengthMm, Quality = CatchQuality.Normal };
            return new CatchAnnouncement(0, c, kind, 0UL, false);
        }

        private static bool Record(MetaSave save, in CatchAnnouncement a, bool local, out string key, out string arg)
        {
            return JournalRecorder.Record(save, "bluegill", "Bluegill", Spot, a, local, out key, out arg);
        }

        [Test]
        public void JournalRecorder_Record_LocalLandedNewThenBest()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); // a decimal comma if the code forgets InvariantCulture
            try
            {
                var save = new MetaSave();

                Assert.IsTrue(Record(save, Announce(AnnounceKind.Landed, 500), true, out string key, out string arg));
                Assert.AreEqual("toast.journal_new", key);
                Assert.AreEqual("Bluegill", arg);

                Assert.IsTrue(Record(save, Announce(AnnounceKind.Landed, 400), true, out key, out arg));
                Assert.IsNull(key, "a lighter second catch at level 2 has nothing to announce");

                Assert.IsTrue(Record(save, Announce(AnnounceKind.Landed, 700), true, out key, out arg));
                Assert.AreEqual("toast.journal_best", key);
                Assert.AreEqual("Bluegill 0.7 kg", arg);
                Assert.AreEqual(3, save.Journal["bluegill"].Caught);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void JournalRecorder_Record_WitnessUnlockOnly()
        {
            var save = new MetaSave();

            Assert.IsTrue(Record(save, Announce(AnnounceKind.Lost, 0), false, out string key, out string arg));
            Assert.AreEqual("toast.journal_unlock", key); // level 0 -> 1
            Assert.AreEqual("Bluegill", arg);

            Assert.IsTrue(Record(save, Announce(AnnounceKind.Landed, 500), false, out key, out arg));
            Assert.AreEqual("toast.journal_unlock", key); // level 1 -> 2

            Assert.IsTrue(Record(save, Announce(AnnounceKind.Landed, 500), false, out key, out arg));
            Assert.IsNull(key, "no unlock toast on every witnessed catch");

            JournalEntry e = save.Journal["bluegill"];
            Assert.AreEqual(0, e.Caught, "a witness is never credited with a catch");
            Assert.AreEqual(3, e.Seen);
            Assert.AreEqual(2, e.CrewLanded);
        }

        [Test]
        public void JournalRecorder_Record_JunkPityAndNoneChangeNothing()
        {
            var save = new MetaSave();

            Assert.IsFalse(Record(save, Announce(AnnounceKind.Landed, 0, junk: true), true, out string key, out string arg));
            Assert.IsNull(key);
            Assert.IsFalse(Record(save, Announce(AnnounceKind.PityCast, 0), true, out key, out arg), "a PityCast's default catch is item 0, a real species");
            Assert.IsNull(key);
            Assert.IsFalse(Record(save, Announce(AnnounceKind.None, 500), true, out key, out arg));
            Assert.IsNull(key);

            Assert.AreEqual(0, save.Journal.Count);
        }

        [Test]
        public void JournalRecorder_Record_OneToastPerAnnouncement()
        {
            var save = new MetaSave();

            // A first local catch is both FirstCatch and a level-up (0 -> 2): exactly the "new" toast.
            Assert.IsTrue(Record(save, Announce(AnnounceKind.Landed, 500), true, out string key, out string arg));
            Assert.AreEqual("toast.journal_new", key);
            Assert.AreEqual("Bluegill", arg);
        }
    }
}
