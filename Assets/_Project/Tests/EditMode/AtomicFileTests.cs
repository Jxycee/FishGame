using System;
using System.IO;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class AtomicFileTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fgtests-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void AtomicFile_Write_LeavesNoTempFile()
        {
            string path = Path.Combine(_dir, "a.json");
            AtomicFile.WriteAllText(path, "{}");
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual("{}", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + AtomicFile.TempSuffix));
            Assert.IsFalse(File.Exists(path + AtomicFile.BackupSuffix)); // first write has nothing to back up
        }

        [Test]
        public void AtomicFile_SecondWrite_KeepsBackupOfPrevious()
        {
            string path = Path.Combine(_dir, "a.json");
            AtomicFile.WriteAllText(path, "first");
            AtomicFile.WriteAllText(path, "second");
            Assert.AreEqual("second", File.ReadAllText(path));
            Assert.AreEqual("first", File.ReadAllText(path + AtomicFile.BackupSuffix));
            Assert.IsFalse(File.Exists(path + AtomicFile.TempSuffix));

            AtomicFile.WriteAllText(path, "third"); // an existing .bak is overwritten
            Assert.AreEqual("third", File.ReadAllText(path));
            Assert.AreEqual("second", File.ReadAllText(path + AtomicFile.BackupSuffix));
        }

        [Test]
        public void AtomicFile_Write_HasNoBom()
        {
            string path = Path.Combine(_dir, "b.json");
            AtomicFile.WriteAllText(path, "café");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(5, bytes.Length); // "caf" = 3 bytes, U+00E9 = 2 bytes in UTF-8, no 3-byte BOM
            Assert.IsFalse(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.AreEqual("café", AtomicFile.ReadAllTextOrNull(path));
        }

        [Test]
        public void AtomicFile_Write_CreatesDirectory()
        {
            string path = Path.Combine(_dir, "nested", "c.json");
            AtomicFile.WriteAllText(path, "{}");
            Assert.IsTrue(File.Exists(path));
        }

        [Test]
        public void AtomicFile_ReadAllTextOrNull_MissingFile_ReturnsNull()
        {
            Assert.IsNull(AtomicFile.ReadAllTextOrNull(Path.Combine(_dir, "missing.json")));
        }
    }
}
