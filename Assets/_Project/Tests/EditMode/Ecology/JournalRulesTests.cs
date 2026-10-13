// Tests/EditMode/Ecology/JournalRulesTests.cs - T8: the spec 12.10 journal update table, hint levels and completion count (M3-D25).
using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class JournalRulesTests
    {
        const string Bluegill = "bluegill", Lake = "lake_dock", Pier = "harbour_pier";

        static JournalUpdate Landed(MetaSave save, bool local, int g, string spot = Lake, AnnounceKind kind = AnnounceKind.Landed) =>
            JournalRules.Apply(save, Bluegill, spot, kind, false, local, g, (ushort)(g / 2), CatchQuality.Good);

        [Test]
        public void JournalRules_LandedByMe_SeenCrewLandedCaughtAndBests()
        {
            var save = new MetaSave();
            JournalUpdate u = JournalRules.Apply(save, Bluegill, Lake, AnnounceKind.Landed, false, true, 750, 310, CatchQuality.Good);
            JournalEntry e = save.Journal[Bluegill];
            Assert.AreEqual(1, e.Seen);
            Assert.AreEqual(1, e.CrewLanded);
            Assert.AreEqual(1, e.Caught);
            Assert.AreEqual(750, e.BestWeightG);
            Assert.AreEqual(310, e.BestLengthMm);
            Assert.AreEqual(1, e.BestQuality);
            CollectionAssert.AreEqual(new[] { Lake }, e.SeenInSpots);
            Assert.IsTrue(u.Changed);
            Assert.IsTrue(u.FirstCatch);
            Assert.IsFalse(u.PersonalBest);
            Assert.AreEqual(0, u.LevelBefore);
            Assert.AreEqual(2, u.LevelAfter);
        }

        [Test]
        public void JournalRules_CrewLanded_NoCaughtNoBests()
        {
            var save = new MetaSave();
            JournalUpdate u = Landed(save, false, 800);
            JournalEntry e = save.Journal[Bluegill];
            Assert.AreEqual(1, e.Seen);
            Assert.AreEqual(1, e.CrewLanded);
            Assert.AreEqual(0, e.Caught);
            Assert.AreEqual(0, e.BestWeightG);
            Assert.AreEqual(0, e.BestLengthMm);
            Assert.AreEqual(0, e.BestQuality);
            Assert.IsTrue(u.Changed);
            Assert.IsFalse(u.FirstCatch);
            Assert.IsFalse(u.PersonalBest);
        }

        [Test]
        public void JournalRules_Lost_SeenOnly()
        {
            var save = new MetaSave();
            JournalUpdate u = JournalRules.Apply(save, Bluegill, Lake, AnnounceKind.Lost, false, true, 500, 250, CatchQuality.Normal);
            JournalEntry e = save.Journal[Bluegill];
            Assert.AreEqual(1, e.Seen);
            Assert.AreEqual(0, e.CrewLanded);
            Assert.AreEqual(0, e.Caught);
            Assert.AreEqual(0, e.BestWeightG);
            CollectionAssert.AreEqual(new[] { Lake }, e.SeenInSpots);
            Assert.AreEqual(0, u.LevelBefore);
            Assert.AreEqual(1, u.LevelAfter);
            Assert.IsFalse(u.FirstCatch);
        }

        [Test]
        public void JournalRules_ShouldJournal_IgnoresJunkPityCastAndNone()
        {
            Assert.IsFalse(JournalRules.ShouldJournal(AnnounceKind.PityCast, false));
            Assert.IsFalse(JournalRules.ShouldJournal(AnnounceKind.None, false));
            Assert.IsFalse(JournalRules.ShouldJournal(AnnounceKind.Landed, true));
            Assert.IsFalse(JournalRules.ShouldJournal(AnnounceKind.Lost, true));
            Assert.IsFalse(JournalRules.ShouldJournal(AnnounceKind.ReleasedCreelFull, true));
            Assert.IsTrue(JournalRules.ShouldJournal(AnnounceKind.Landed, false));
            Assert.IsTrue(JournalRules.ShouldJournal(AnnounceKind.Lost, false));
            Assert.IsTrue(JournalRules.ShouldJournal(AnnounceKind.ReleasedCreelFull, false));

            // A PityCast's default catch is item 0 (a real species): it must not create an entry.
            var save = new MetaSave();
            JournalUpdate u = JournalRules.Apply(save, Bluegill, Lake, AnnounceKind.PityCast, false, true, 0, 0, CatchQuality.Normal);
            Assert.IsFalse(save.Journal.ContainsKey(Bluegill));
            Assert.IsFalse(u.Changed);
            Assert.AreEqual(0, save.Journal.Count);
            u = JournalRules.Apply(save, Bluegill, Lake, AnnounceKind.Landed, true, true, 0, 0, CatchQuality.Normal);
            Assert.IsFalse(u.Changed);
            Assert.AreEqual(0, save.Journal.Count, "junk never journals");
        }

        [Test]
        public void JournalRules_ReleasedCreelFull_JournaledLikeLanded()
        {
            var a = new MetaSave();
            var b = new MetaSave();
            Landed(a, true, 400, kind: AnnounceKind.ReleasedCreelFull);
            Landed(b, true, 400, kind: AnnounceKind.Landed);
            JournalEntry ea = a.Journal[Bluegill], eb = b.Journal[Bluegill];
            Assert.AreEqual(eb.Seen, ea.Seen);
            Assert.AreEqual(eb.CrewLanded, ea.CrewLanded);
            Assert.AreEqual(eb.Caught, ea.Caught);
            Assert.AreEqual(eb.BestWeightG, ea.BestWeightG);
            Assert.AreEqual(1, ea.Caught);
            Assert.AreEqual(400, ea.BestWeightG);
        }

        [Test]
        public void JournalRules_HintLevels_ZeroToThree()
        {
            Assert.AreEqual(0, JournalRules.HintLevel(null));
            Assert.AreEqual(1, JournalRules.HintLevel(new JournalEntry { Seen = 1 }));
            Assert.AreEqual(0, JournalRules.HintLevel(new JournalEntry()));
            Assert.AreEqual(2, JournalRules.HintLevel(new JournalEntry { Seen = 1, CrewLanded = 1 }));
            Assert.AreEqual(2, JournalRules.HintLevel(new JournalEntry { Seen = 2, CrewLanded = 2, Caught = 2 }));
            Assert.AreEqual(3, JournalRules.HintLevel(new JournalEntry { Seen = 3, CrewLanded = 3, Caught = 3 }));
            Assert.AreEqual(3, JournalRules.HintLevel(new JournalEntry { Caught = 3 }), "Caught alone satisfies level 3");

            // A sequence: Lost, a crew landing, then three local landings.
            var save = new MetaSave();
            var after = new List<int>();
            var before = new List<int>();
            JournalUpdate u = JournalRules.Apply(save, Bluegill, Lake, AnnounceKind.Lost, false, false, 0, 0, CatchQuality.Normal);
            before.Add(u.LevelBefore); after.Add(u.LevelAfter);
            u = Landed(save, false, 300); before.Add(u.LevelBefore); after.Add(u.LevelAfter);
            for (int i = 0; i < 3; i++) { u = Landed(save, true, 300 + i); before.Add(u.LevelBefore); after.Add(u.LevelAfter); }
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 2, 3 }, after);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 2 }, before);
        }

        [Test]
        public void JournalRules_FirstCatchAndPersonalBest_Flags()
        {
            var save = new MetaSave();
            JournalUpdate u = Landed(save, true, 500);
            Assert.IsTrue(u.FirstCatch); Assert.IsFalse(u.PersonalBest);
            u = Landed(save, true, 400);
            Assert.IsFalse(u.FirstCatch); Assert.IsFalse(u.PersonalBest);
            Assert.AreEqual(500, save.Journal[Bluegill].BestWeightG, "a lighter fish keeps the best");
            u = Landed(save, true, 600);
            Assert.IsFalse(u.FirstCatch); Assert.IsTrue(u.PersonalBest);
            Assert.AreEqual(600, save.Journal[Bluegill].BestWeightG);
            Assert.AreEqual(300, save.Journal[Bluegill].BestLengthMm);
            u = Landed(save, true, 600);
            Assert.IsFalse(u.PersonalBest, "an equal weight is not a personal best");
            u = Landed(save, false, 900);
            Assert.IsFalse(u.PersonalBest);
            Assert.AreEqual(600, save.Journal[Bluegill].BestWeightG, "a crew catch never sets the best");
        }

        [Test]
        public void JournalRules_SeenInSpots_NoDuplicates()
        {
            var save = new MetaSave();
            foreach (string spot in new[] { Lake, Lake, Pier, Lake })
                JournalRules.Apply(save, Bluegill, spot, AnnounceKind.Lost, false, true, 0, 0, CatchQuality.Normal);
            JournalRules.Apply(save, Bluegill, null, AnnounceKind.Lost, false, true, 0, 0, CatchQuality.Normal);
            JournalRules.Apply(save, Bluegill, "", AnnounceKind.Lost, false, true, 0, 0, CatchQuality.Normal);
            CollectionAssert.AreEqual(new[] { Lake, Pier }, save.Journal[Bluegill].SeenInSpots);
            Assert.AreEqual(6, save.Journal[Bluegill].Seen, "every announcement still counts as seen");
        }

        [Test]
        public void JournalRules_UnknownIds_KeptUntouched()
        {
            var save = new MetaSave();
            var future = new JournalEntry { Caught = 9, Seen = 4 };
            save.Journal["zzz_future_fish"] = future;
            Landed(save, true, 500);
            Assert.AreSame(future, save.Journal["zzz_future_fish"]);
            Assert.AreEqual(9, future.Caught);
            Assert.AreEqual(4, future.Seen);
            Assert.AreEqual(1, JournalRules.CompletionCount(save, new[] { Bluegill }), "ids outside the journal list are not counted");
        }

        [Test]
        public void JournalRules_Completion_CountsCaughtAtLeastOne()
        {
            var save = new MetaSave();
            save.Journal["a"] = new JournalEntry { Caught = 1 };
            save.Journal["b"] = new JournalEntry { Seen = 5, Caught = 0 };
            save.Journal["c"] = new JournalEntry { Caught = 4 };
            Assert.AreEqual(2, JournalRules.CompletionCount(save, new[] { "a", "b", "c", "d" }));
            Assert.AreEqual(2, JournalRules.CompletionCount(save, new[] { "a", null, "c" }), "null ids are skipped");
            save.Journal = null;
            Assert.AreEqual(0, JournalRules.CompletionCount(save, new[] { "a" }));
            Assert.Throws<ArgumentNullException>(() => JournalRules.CompletionCount(new MetaSave(), null));
            Assert.Throws<ArgumentNullException>(() => JournalRules.CompletionCount(null, new[] { "a" }));
        }

        [Test]
        public void JournalRules_Apply_RejectsBadArguments_AndSaturates()
        {
            Assert.Throws<ArgumentNullException>(() => JournalRules.Apply(null, Bluegill, Lake, AnnounceKind.Lost, false, true, 0, 0, CatchQuality.Normal));
            Assert.Throws<ArgumentException>(() => JournalRules.Apply(new MetaSave(), "", Lake, AnnounceKind.Lost, false, true, 0, 0, CatchQuality.Normal));
            Assert.Throws<ArgumentException>(() => JournalRules.Apply(new MetaSave(), null, Lake, AnnounceKind.Lost, false, true, 0, 0, CatchQuality.Normal));

            var save = new MetaSave();
            save.Journal[Bluegill] = new JournalEntry { Seen = int.MaxValue, CrewLanded = int.MaxValue, Caught = int.MaxValue };
            Landed(save, true, 100);
            Assert.AreEqual(int.MaxValue, save.Journal[Bluegill].Seen);
            Assert.AreEqual(int.MaxValue, save.Journal[Bluegill].CrewLanded);
            Assert.AreEqual(int.MaxValue, save.Journal[Bluegill].Caught);
        }

        [Test]
        public void MetaSave_Defaults_MatchSpec()
        {
            var save = new MetaSave();
            Assert.AreEqual(1, save.SchemaVersion);
            Assert.IsNotNull(save.Journal);
            var e = new JournalEntry();
            Assert.IsNotNull(e.SeenInSpots);
            Assert.AreEqual(0, e.Seen);
        }
    }
}
