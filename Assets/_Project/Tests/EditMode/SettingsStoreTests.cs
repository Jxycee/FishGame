using System;
using System.IO;
using System.Text.RegularExpressions;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.EditMode
{
    public class SettingsStoreTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fgtests-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void SettingsStore_SaveThenLoad_RoundTripsAllFields()
        {
            string path = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(path);
            store.Load(); // missing file -> defaults
            SettingsData d = store.Current;
            Assert.AreEqual(80, d.masterVolume);
            Assert.AreEqual("Fisher", d.displayName);

            d.masterVolume = 10; d.sfxVolume = 20; d.ambienceVolume = 30;
            d.musicVolume = 40; d.uiVolume = 50; d.voiceVolume = 60;
            d.displayName = "Captain Hook";
            d.mouseSensitivity = 0.2f; d.gamepadLookSpeed = 2.5f; d.invertY = true;
            d.fieldOfView = 100f; d.cameraMotion = 0.5f;
            d.sprintToggle = true; d.aimToggle = true; d.streamerMode = true;
            d.uiScalePercent = 125;
            d.bindingOverridesJson = "{\"bindings\":[]}";
            d.localPlayerId = "0123456789abcdef0123456789abcdef";
            store.Save();

            var reloaded = new SettingsStore(path);
            reloaded.Load();
            SettingsData r = reloaded.Current;

            Assert.IsFalse(reloaded.LoadedFromBackup);
            Assert.AreEqual(path, reloaded.FilePath);
            Assert.AreEqual(1, r.version);
            Assert.AreEqual(10, r.masterVolume);
            Assert.AreEqual(20, r.sfxVolume);
            Assert.AreEqual(30, r.ambienceVolume);
            Assert.AreEqual(40, r.musicVolume);
            Assert.AreEqual(50, r.uiVolume);
            Assert.AreEqual(60, r.voiceVolume);
            Assert.AreEqual("Captain Hook", r.displayName);
            Assert.AreEqual(0.2f, r.mouseSensitivity, 1e-5f);
            Assert.AreEqual(2.5f, r.gamepadLookSpeed, 1e-5f);
            Assert.IsTrue(r.invertY);
            Assert.AreEqual(100f, r.fieldOfView, 1e-5f);
            Assert.AreEqual(0.5f, r.cameraMotion, 1e-5f);
            Assert.IsTrue(r.sprintToggle);
            Assert.IsTrue(r.aimToggle);
            Assert.IsTrue(r.streamerMode);
            Assert.AreEqual(125, r.uiScalePercent);
            Assert.AreEqual("{\"bindings\":[]}", r.bindingOverridesJson);
            Assert.AreEqual("0123456789abcdef0123456789abcdef", r.localPlayerId);
        }

        [Test]
        public void SettingsStore_CorruptMain_LoadsBackup()
        {
            string path = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(path);
            store.Load();
            store.Current.displayName = "Backed";
            store.Save();                 // first write: main only
            store.Current.displayName = "Latest";
            store.Save();                 // second write: main = Latest, .bak = Backed
            File.WriteAllText(path, "{ this is not json");

            LogAssert.Expect(LogType.Warning, new Regex("loaded backup"));
            var recovered = new SettingsStore(path);
            recovered.Load();

            Assert.IsTrue(recovered.LoadedFromBackup);
            Assert.AreEqual("Backed", recovered.Current.displayName);
        }

        [Test]
        public void SettingsStore_OutOfRangeValues_AreClamped()
        {
            string path = Path.Combine(_dir, "settings.json");
            // Hand-written file (as a user might edit it): out-of-range values must be clamped on Load.
            File.WriteAllText(path,
                "{\"version\":1,\"masterVolume\":500,\"sfxVolume\":-5,\"displayName\":\"   \"," +
                "\"mouseSensitivity\":9.0,\"gamepadLookSpeed\":-1.0,\"fieldOfView\":20.0," +
                "\"cameraMotion\":3.0,\"uiScalePercent\":10}");

            var store = new SettingsStore(path);
            store.Load();
            SettingsData r = store.Current;

            Assert.IsFalse(store.LoadedFromBackup);
            Assert.AreEqual(100, r.masterVolume);
            Assert.AreEqual(0, r.sfxVolume);
            Assert.AreEqual(70, r.ambienceVolume); // absent field keeps its default
            Assert.AreEqual("Fisher", r.displayName);
            Assert.AreEqual(0.30f, r.mouseSensitivity, 1e-5f);
            Assert.AreEqual(0.2f, r.gamepadLookSpeed, 1e-5f);
            Assert.AreEqual(80f, r.fieldOfView, 1e-5f);
            Assert.AreEqual(1f, r.cameraMotion, 1e-5f);
            Assert.AreEqual(80, r.uiScalePercent);
        }

        [Test]
        public void SettingsStore_LongDisplayName_TruncatesTo16()
        {
            string path = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(path);
            store.Load();
            store.Current.displayName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            store.Save();
            Assert.AreEqual("ABCDEFGHIJKLMNOP", store.Current.displayName); // Save clamps the live object too

            var reloaded = new SettingsStore(path);
            reloaded.Load();
            Assert.AreEqual("ABCDEFGHIJKLMNOP", reloaded.Current.displayName);
        }

        [Test]
        public void SettingsData_Normalize_DoesNotSplitSurrogatePair()
        {
            var data = new SettingsData();
            data.displayName = new string('a', 15) + "\uD83D\uDE00" + "tail"; // emoji occupies chars 15-16, straddling the 16 limit
            data.Clamp();
            Assert.AreEqual(new string('a', 15), data.displayName);
            Assert.IsFalse(char.IsHighSurrogate(data.displayName[data.displayName.Length - 1]));

            data.displayName = new string('a', 14) + "\uD83D\uDE00" + "tail"; // emoji fits exactly in 16 chars
            data.Clamp();
            Assert.AreEqual(new string('a', 14) + "\uD83D\uDE00", data.displayName);
        }

        [Test]
        public void SettingsStore_NotifyChanged_ClampsRaisesAndDoesNotWrite()
        {
            string path = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(path);
            store.Load();
            int calls = 0;
            SettingsData seen = null;
            store.Changed += s => { calls++; seen = s; };

            store.Current.masterVolume = 250;
            store.Current.displayName = "  Bob\u0007 ";
            store.NotifyChanged();

            Assert.AreEqual(1, calls);
            Assert.AreSame(store.Current, seen);
            Assert.AreEqual(100, seen.masterVolume);
            Assert.AreEqual("Bob", seen.displayName);
            Assert.IsFalse(File.Exists(path)); // values apply live; only Save writes

            SettingsData copy = store.Current.Clone();
            Assert.AreNotSame(store.Current, copy);
            Assert.AreEqual("Bob", copy.displayName);
            copy.masterVolume = 5;
            Assert.AreEqual(100, store.Current.masterVolume);
        }

        [Test]
        public void SettingsStore_TwoInstances_DoNotCorruptEachOther()
        {
            // Main editor vs. an MPPM clone (Authentication-style "-editor-mode -name <x>" args) get different files.
            InstanceProfile mainProfile = InstanceProfile.FromArgs(new string[0]);
            InstanceProfile cloneProfile = InstanceProfile.FromArgs(new[] { "-editor-mode", "-name", "Player 2" });
            string mainPath = SettingsPaths.SettingsFile(_dir, mainProfile.Suffix);
            string clonePath = SettingsPaths.SettingsFile(_dir, cloneProfile.Suffix);
            Assert.AreEqual(Path.Combine(_dir, "settings.json"), mainPath);
            Assert.AreEqual(Path.Combine(_dir, "settings_player2.json"), clonePath);

            var main = new SettingsStore(mainPath);
            main.Load();
            var clone = new SettingsStore(clonePath);
            clone.Load();

            for (int i = 0; i < 20; i++)
            {
                main.Current.displayName = "Main" + i;
                main.Current.masterVolume = i;
                main.Save();
                clone.Current.displayName = "Clone" + i;
                clone.Current.masterVolume = 100 - i;
                clone.Save();
            }

            var mainReload = new SettingsStore(mainPath);
            mainReload.Load();
            var cloneReload = new SettingsStore(clonePath);
            cloneReload.Load();

            Assert.IsFalse(mainReload.LoadedFromBackup);
            Assert.IsFalse(cloneReload.LoadedFromBackup);
            Assert.AreEqual("Main19", mainReload.Current.displayName);
            Assert.AreEqual(19, mainReload.Current.masterVolume);
            Assert.AreEqual("Clone19", cloneReload.Current.displayName);
            Assert.AreEqual(81, cloneReload.Current.masterVolume);
            StringAssert.Contains("Main18", File.ReadAllText(mainPath + AtomicFile.BackupSuffix));
            StringAssert.Contains("Clone18", File.ReadAllText(clonePath + AtomicFile.BackupSuffix));
            Assert.IsFalse(File.Exists(mainPath + AtomicFile.TempSuffix));
            Assert.IsFalse(File.Exists(clonePath + AtomicFile.TempSuffix));
        }

        [Test]
        public void SettingsStore_EnsureLocalPlayerId_IsStable()
        {
            string path = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(path);
            store.Load();
            string first = store.EnsureLocalPlayerId();
            Assert.IsTrue(Regex.IsMatch(first, "^[0-9a-f]{32}$"), first); // Guid "N" format
            Assert.IsTrue(File.Exists(path));                             // created id is saved immediately
            Assert.AreEqual(first, store.EnsureLocalPlayerId());

            var reloaded = new SettingsStore(path);
            reloaded.Load();
            Assert.AreEqual(first, reloaded.EnsureLocalPlayerId());
        }

        [Test]
        public void SettingsStore_FishingFields_RoundTripAndClamp()
        {
            string path = Path.Combine(_dir, "settings.json");

            // 1. defaults on a missing file
            var store = new SettingsStore(path);
            store.Load();
            Assert.AreEqual(0, store.Current.lineGauge);
            Assert.IsTrue(store.Current.captions);
            Assert.IsFalse(store.Current.fishingAssist);

            // 2. round trip
            store.Current.lineGauge = 2;
            store.Current.captions = false;
            store.Current.fishingAssist = true;
            store.Save();
            var reloaded = new SettingsStore(path);
            reloaded.Load();
            Assert.AreEqual(2, reloaded.Current.lineGauge);
            Assert.IsFalse(reloaded.Current.captions);
            Assert.IsTrue(reloaded.Current.fishingAssist);
            Assert.AreEqual(1, reloaded.Current.version, "C-Q10: version stays 1");

            // 3. clamp
            store.Current.lineGauge = 7;
            store.NotifyChanged();
            Assert.AreEqual(2, store.Current.lineGauge);
            store.Current.lineGauge = -3;
            store.NotifyChanged();
            Assert.AreEqual(0, store.Current.lineGauge);

            // 4. an M1-era file keeps the new defaults
            string oldPath = Path.Combine(_dir, "m1.json");
            File.WriteAllText(oldPath, "{\"version\":1,\"masterVolume\":50}");
            var old = new SettingsStore(oldPath);
            old.Load();
            Assert.AreEqual(50, old.Current.masterVolume);
            Assert.IsTrue(old.Current.captions);
            Assert.AreEqual(0, old.Current.lineGauge);
            Assert.IsFalse(old.Current.fishingAssist);

            // 5. Clamp does not couple assist to the gauge (that rule is the settings UI's, T25 / G-Q4)
            var plain = new SettingsStore(Path.Combine(_dir, "plain.json"));
            plain.Load();
            plain.Current.fishingAssist = true;
            plain.NotifyChanged();
            Assert.AreEqual(0, plain.Current.lineGauge);
            Assert.IsTrue(plain.Current.fishingAssist);
        }
    }
}
