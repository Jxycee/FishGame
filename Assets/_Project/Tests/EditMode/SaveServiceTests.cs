using System;
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    // T20. Every test works under its own directory below the OS temp path (never the repo, never persistentDataPath).
    public class SaveServiceTests
    {
        private string _dir;
        private string _path;
        private readonly List<string> _warnings = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fg-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, SaveService.MetaBaseName + ".json");
            _warnings.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) _warnings.Add(condition);
        }

        private static MetaSave SampleSave()
        {
            var save = new MetaSave();
            save.Journal["bluegill"] = new JournalEntry
            {
                Seen = 4, CrewLanded = 3, Caught = 2, BestWeightG = 512, BestLengthMm = 301, BestQuality = 2,
                SeenInSpots = new List<string> { "lake_dock", "harbour_pier" },
            };
            save.Journal["moon_squid"] = new JournalEntry { Seen = 1 };
            return save;
        }

        private static void AssertEntry(JournalEntry expected, JournalEntry actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Seen, actual.Seen);
            Assert.AreEqual(expected.CrewLanded, actual.CrewLanded);
            Assert.AreEqual(expected.Caught, actual.Caught);
            Assert.AreEqual(expected.BestWeightG, actual.BestWeightG);
            Assert.AreEqual(expected.BestLengthMm, actual.BestLengthMm);
            Assert.AreEqual(expected.BestQuality, actual.BestQuality);
            CollectionAssert.AreEqual(expected.SeenInSpots, actual.SeenInSpots, "spot order is kept");
        }

        private SaveService SavedService(int seen)
        {
            var svc = new SaveService(_path);
            svc.Load();
            svc.Current.Journal["bluegill"] = new JournalEntry { Seen = seen };
            svc.Save();
            return svc;
        }

        [Test]
        public void SaveService_RoundTrip_PreservesJournal()
        {
            var first = new SaveService(_path);
            MetaSave sample = SampleSave();
            first.Current.Journal.Clear();
            foreach (KeyValuePair<string, JournalEntry> kv in sample.Journal) first.Current.Journal[kv.Key] = kv.Value;
            first.Save();

            var second = new SaveService(_path);
            second.Load();

            Assert.AreEqual(MetaLoadSource.Main, second.LoadedFrom);
            Assert.AreEqual(1, second.Current.SchemaVersion);
            Assert.AreEqual(2, second.Current.Journal.Count);
            AssertEntry(sample.Journal["bluegill"], second.Current.Journal["bluegill"]);
            AssertEntry(sample.Journal["moon_squid"], second.Current.Journal["moon_squid"]);
            Assert.IsFalse(File.Exists(_path + ".tmp"), "no temp file left behind");
        }

        [Test]
        public void SaveService_CorruptMain_LoadsBak()
        {
            SavedService(1); // main = A
            SavedService(2); // main = B, .bak = A
            Assert.IsTrue(File.Exists(_path + ".bak"));

            File.WriteAllText(_path, "{ \"journal\": "); // truncated
            var truncated = new SaveService(_path);
            truncated.Load();
            Assert.AreEqual(MetaLoadSource.Backup, truncated.LoadedFrom);
            Assert.AreEqual(1, truncated.Current.Journal["bluegill"].Seen, "the .bak holds A");

            File.WriteAllText(_path, "null");
            var nullText = new SaveService(_path);
            nullText.Load();
            Assert.AreEqual(MetaLoadSource.Backup, nullText.LoadedFrom);
            Assert.AreEqual(1, nullText.Current.Journal["bluegill"].Seen);
            Assert.AreEqual(2, _warnings.FindAll(w => w.Contains("FG SAVE")).Count, "one warning per backup load");
        }

        [Test]
        public void SaveService_CorruptMainGoodBak_KeepsCorruptAndBak()
        {
            SavedService(1); // main = A
            SavedService(2); // main = B, .bak = A
            const string truncated = "{ \"journal\": ";
            File.WriteAllText(_path, truncated);

            var svc = new SaveService(_path);
            svc.Load();
            Assert.AreEqual(MetaLoadSource.Backup, svc.LoadedFrom);

            svc.Save();
            svc.Save();

            Assert.IsTrue(File.Exists(_path + ".corrupt"), "the unreadable main file is kept");
            Assert.AreEqual(truncated, File.ReadAllText(_path + ".corrupt"));
            MetaSave bak = SaveService.DeserializeOrNull(File.ReadAllText(_path + ".bak"));
            Assert.IsNotNull(bak, "the good .bak must not be overwritten by the broken main");
            Assert.AreEqual(1, bak.Journal["bluegill"].Seen);
            var reloaded = new SaveService(_path);
            reloaded.Load();
            Assert.AreEqual(MetaLoadSource.Main, reloaded.LoadedFrom);
            Assert.AreEqual(1, reloaded.Current.Journal["bluegill"].Seen);
        }

        [Test]
        public void SaveService_NoFiles_Defaults()
        {
            var svc = new SaveService(_path);
            svc.Load();

            Assert.AreEqual(MetaLoadSource.Defaults, svc.LoadedFrom);
            Assert.AreEqual(1, svc.Current.SchemaVersion);
            Assert.AreEqual(0, svc.Current.Journal.Count);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(_dir).Length, "Load must not create a file");
            Assert.AreEqual(0, _warnings.FindAll(w => w.Contains("FG SAVE")).Count, "a first run is not a warning");

            svc.Save();
            Assert.IsTrue(File.Exists(_path));
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void SaveService_JsonKeys_CamelCasePropsIdsUnchanged()
        {
            var save = new MetaSave();
            save.Journal["gilded_carp"] = new JournalEntry { Seen = 1, SeenInSpots = new List<string> { "lake_dock" } };
            save.Journal["Mixed_Case_Id"] = new JournalEntry { Caught = 1 };

            string json = SaveService.Serialize(save);

            foreach (string key in new[] { "schemaVersion", "journal", "seen", "crewLanded", "caught", "bestWeightG", "bestLengthMm", "bestQuality", "seenInSpots", "gilded_carp", "Mixed_Case_Id" })
            {
                StringAssert.Contains("\"" + key + "\"", json);
            }
            StringAssert.DoesNotContain("\"SchemaVersion\"", json);
            StringAssert.DoesNotContain("\"mixed_Case_Id\"", json);
        }

        [Test]
        public void SaveService_UnknownIds_KeptOnRoundTrip()
        {
            File.WriteAllText(_path,
                "{ \"schemaVersion\": 1, \"journal\": { \"zzz_future_fish\": { \"seen\": 9, \"caught\": 9, \"futureField\": 3, \"seenInSpots\": [\"mars_pier\"] } } }");

            var svc = new SaveService(_path);
            svc.Load();
            Assert.AreEqual(MetaLoadSource.Main, svc.LoadedFrom, "an unknown member must not make the file unreadable");
            Assert.AreEqual(9, svc.Current.Journal["zzz_future_fish"].Caught);

            svc.Save();
            var again = new SaveService(_path);
            again.Load();
            Assert.AreEqual(MetaLoadSource.Main, again.LoadedFrom);
            Assert.AreEqual(9, again.Current.Journal["zzz_future_fish"].Caught);
            CollectionAssert.AreEqual(new[] { "mars_pier" }, again.Current.Journal["zzz_future_fish"].SeenInSpots);
        }

        [Test]
        public void SaveService_NewerSchema_NotOverwritten()
        {
            string original = "{ \"schemaVersion\": 2, \"extra\": { \"keep\": true }, \"journal\": { \"bluegill\": { \"seen\": 3, \"caught\": 1 } } }";
            File.WriteAllText(_path, original);

            var svc = new SaveService(_path);
            svc.Load();
            Assert.AreEqual(MetaLoadSource.Main, svc.LoadedFrom);
            Assert.IsTrue(svc.IsReadOnly);
            Assert.AreEqual(3, svc.Current.Journal["bluegill"].Seen, "the journal stays readable");
            Assert.AreEqual(1, _warnings.FindAll(w => w.Contains("FG SAVE")).Count, "one warning on load");

            svc.Current.Journal["bluegill"].Seen = 99;
            svc.Save();
            svc.Save();

            Assert.AreEqual(original, File.ReadAllText(_path), "a newer file is byte-identical after Save");
            Assert.IsFalse(File.Exists(_path + ".tmp"));
            Assert.IsFalse(File.Exists(_path + ".bak"));
            Assert.AreEqual(1, _warnings.FindAll(w => w.Contains("FG SAVE")).Count, "Save adds no further warning");
        }

        [Test]
        public void SaveService_BothCorrupt_DefaultsAndKeepsCorruptCopy()
        {
            const string garbage = "this is not json at all";
            File.WriteAllText(_path, garbage);
            File.WriteAllText(_path + ".bak", "{ also broken");

            var svc = new SaveService(_path);
            svc.Load();
            Assert.AreEqual(MetaLoadSource.Defaults, svc.LoadedFrom);
            Assert.AreEqual(0, svc.Current.Journal.Count);
            Assert.AreEqual(1, _warnings.FindAll(w => w.Contains("FG SAVE")).Count);

            svc.Current.Journal["bluegill"] = new JournalEntry { Seen = 1 };
            svc.Save();

            Assert.IsTrue(File.Exists(_path + ".corrupt"));
            Assert.AreEqual(garbage, File.ReadAllText(_path + ".corrupt"), "the unreadable main file is kept");
            var reloaded = new SaveService(_path);
            reloaded.Load();
            Assert.AreEqual(MetaLoadSource.Main, reloaded.LoadedFrom);
            Assert.AreEqual(1, reloaded.Current.Journal["bluegill"].Seen);
        }

        [Test]
        public void SaveService_WriteFailure_LogsOnceNeverThrows()
        {
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "i am a file, not a directory");
            var svc = new SaveService(Path.Combine(blocker, "meta.json"));

            Assert.DoesNotThrow(() => svc.Save());
            Assert.AreEqual(1, _warnings.FindAll(w => w.Contains("FG SAVE")).Count);
            Assert.DoesNotThrow(() => svc.Save());
            Assert.AreEqual(1, _warnings.FindAll(w => w.Contains("FG SAVE")).Count, "the warning is logged once per service");
        }

        [Test]
        public void SaveService_MarkDirty_DefersWriteUntilFlush()
        {
            var svc = new SaveService(_path);
            svc.Load();
            Assert.IsFalse(svc.IsDirty);
            Assert.IsFalse(svc.FlushIfDirty(), "nothing marked: no write");
            Assert.IsFalse(File.Exists(_path));

            svc.Current.Journal["bluegill"] = new JournalEntry { Seen = 7 };
            svc.MarkDirty();
            svc.MarkDirty();
            Assert.IsTrue(svc.IsDirty);
            Assert.IsFalse(File.Exists(_path), "marking does not touch the disk");

            Assert.IsTrue(svc.FlushIfDirty());
            Assert.IsFalse(svc.IsDirty);
            Assert.IsFalse(svc.FlushIfDirty(), "already flushed: no second write");

            var reloaded = new SaveService(_path);
            reloaded.Load();
            Assert.AreEqual(7, reloaded.Current.Journal["bluegill"].Seen);
        }

        [Test]
        public void SaveService_Save_ClearsDirty()
        {
            var svc = new SaveService(_path);
            svc.Load();
            svc.MarkDirty();
            svc.Save();
            Assert.IsFalse(svc.IsDirty, "an explicit Save (e.g. on quit) already wrote the latest state");
            Assert.IsFalse(svc.FlushIfDirty());
        }
    }
}
