// Authority: local only (each machine writes its own meta file; spec 12.10, R32). Main thread. Never throws into gameplay.
// meta.json v1 (MetaSave) serialized with Newtonsoft (M3-D30): camelCase property names, dictionary keys (species ids) unchanged.
// Load order main -> .bak -> defaults. A file with a newer SchemaVersion than 1 loads read-only and is never written.
// An unparsable file is copied to path + ".corrupt" before the first Save can push it into .bak and overwrite it.
using System;
using System.Globalization;
using System.IO;
using FishGame.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace FishGame.Runtime
{
    public sealed class SaveService
    {
        public const string MetaBaseName = "meta";
        private const string CorruptSuffix = ".corrupt";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true },
            },
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,   // never append to the DTO's initialiser collections
            MissingMemberHandling = MissingMemberHandling.Ignore,      // members from a newer build are skipped, not fatal
            NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 32,
        };

        private MetaSave _current = new MetaSave();
        private bool _unreadableOnDisk;
        private bool _writeWarned;

        public SaveService(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath is required", nameof(filePath));
            FilePath = filePath;
        }

        public string FilePath { get; }
        public MetaSave Current => _current;
        public MetaLoadSource LoadedFrom { get; private set; } = MetaLoadSource.Defaults;

        // True when the file on disk has a newer schema than this build understands; Save does nothing then.
        internal bool IsReadOnly { get; private set; }

        public event Action<MetaSave> Changed;

        // persistentDataPath is shared by MPPM clones and test runs, so the per-instance suffix keeps their files apart.
        public static SaveService CreateForThisInstance()
        {
            return new SaveService(Path.Combine(Application.persistentDataPath, MetaBaseName + InstanceProfile.Current.Suffix + ".json"));
        }

        public void Load()
        {
            string mainText = AtomicFile.ReadAllTextOrNull(FilePath);
            MetaSave loaded = DeserializeOrNull(mainText);
            MetaLoadSource source = MetaLoadSource.Main;
            _unreadableOnDisk = false;

            if (loaded == null)
            {
                string backupText = AtomicFile.ReadAllTextOrNull(FilePath + AtomicFile.BackupSuffix);
                loaded = DeserializeOrNull(backupText);
                // A main file that exists but did not load (corrupt, or locked so that it read as missing) is copied to .corrupt and
                // removed before the first Save, so it is neither rotated into .bak over a good backup nor replaced by an empty journal.
                bool mainExists = File.Exists(FilePath);
                _unreadableOnDisk = mainExists;
                if (loaded != null)
                {
                    source = MetaLoadSource.Backup;
                    Debug.LogWarning("FG SAVE main meta file unreadable; loaded the .bak (" + FilePath + ")");
                }
                else
                {
                    source = MetaLoadSource.Defaults;
                    if (mainExists || backupText != null)
                    {
                        Debug.LogWarning("FG SAVE meta file and .bak both unreadable; starting from defaults, the old file is kept as .corrupt on the next save (" + FilePath + ")");
                    }
                }
            }

            _current = loaded ?? new MetaSave();
            if (_current.Journal == null) _current.Journal = new System.Collections.Generic.Dictionary<string, JournalEntry>();
            LoadedFrom = source;

            IsReadOnly = _current.SchemaVersion > 1;
            if (IsReadOnly)
            {
                Debug.LogWarning("FG SAVE meta file schema " + _current.SchemaVersion + " is newer than this build; journal is read-only this session (" + FilePath + ")");
            }

            Debug.Log("FG SAVE loaded " + source + " " + FilePath);
            NotifyChanged();
        }

        public void Save()
        {
            if (IsReadOnly) return;
            try
            {
                if (_unreadableOnDisk && File.Exists(FilePath))
                {
                    // A failed copy or delete aborts the write; the next Save retries. With the main file gone, AtomicFile moves the
                    // tmp file in and does not rotate the unreadable file into .bak.
                    File.Copy(FilePath, FilePath + CorruptSuffix, true);
                    File.Delete(FilePath);
                }
                _unreadableOnDisk = false;
                AtomicFile.WriteAllText(FilePath, Serialize(_current));
            }
            catch (IOException ex)
            {
                WarnWriteFailed(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                WarnWriteFailed(ex);
            }
        }

        public void NotifyChanged()
        {
            Action<MetaSave> handlers = Changed;
            if (handlers == null) return;
            foreach (Delegate d in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<MetaSave>)d)(_current);
                }
                catch (Exception e)
                {
                    Debug.LogException(e); // one bad subscriber must not block the others
                }
            }
        }

        internal static string Serialize(MetaSave save)
        {
            return JsonConvert.SerializeObject(save, JsonSettings);
        }

        internal static MetaSave DeserializeOrNull(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                MetaSave s = JsonConvert.DeserializeObject<MetaSave>(json, JsonSettings);
                return s != null && s.SchemaVersion >= 1 ? s : null; // "null", a non-object or schemaVersion <= 0 count as malformed
            }
            catch (Exception)
            {
                return null; // Load runs inside AppBootstrap.Compose: nothing may escape and stop the app from starting
            }
        }

        private void WarnWriteFailed(Exception ex)
        {
            if (_writeWarned) return;
            _writeWarned = true;
            Debug.LogWarning("FG SAVE write failed: " + ex.Message);
        }
    }
}
