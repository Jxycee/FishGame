using System;
using System.IO;
using UnityEngine;

namespace FishGame.Runtime
{
    // Load order: main file -> FilePath + ".bak" -> defaults (warning logged when the backup or defaults were used
    // because the main file was bad). Writes go through AtomicFile. Never logs at error level (EditMode/PlayMode safe).
    public sealed class SettingsStore : ISettings
    {
        private SettingsData _current = new SettingsData();

        public SettingsStore(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath is required", nameof(filePath));
            FilePath = filePath;
        }

        public string FilePath { get; }
        public SettingsData Current => _current;
        public bool LoadedFromBackup { get; private set; }

        public event Action<SettingsData> Changed;

        public void Load()
        {
            LoadedFromBackup = false;
            string mainText = AtomicFile.ReadAllTextOrNull(FilePath);
            SettingsData loaded = TryParse(mainText);
            if (loaded == null)
            {
                string backupPath = FilePath + AtomicFile.BackupSuffix;
                loaded = TryParse(AtomicFile.ReadAllTextOrNull(backupPath));
                if (loaded != null)
                {
                    LoadedFromBackup = true;
                    Debug.LogWarning("Settings: main file missing or unreadable, loaded backup " + backupPath);
                }
                else if (mainText != null)
                {
                    Debug.LogWarning("Settings: " + FilePath + " is corrupt and no backup is readable, using defaults");
                }
                // else: first run (no file yet) -> defaults, nothing logged.
            }
            _current = loaded ?? new SettingsData();
            _current.Clamp();
        }

        public void NotifyChanged()
        {
            _current.Clamp();
            Action<SettingsData> handler = Changed;
            if (handler != null) handler(_current);
        }

        public void Save()
        {
            _current.Clamp();
            try
            {
                AtomicFile.WriteAllText(FilePath, JsonUtility.ToJson(_current, true));
            }
            catch (IOException ex)
            {
                Debug.LogWarning("Settings: save failed for " + FilePath + ": " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning("Settings: save failed for " + FilePath + ": " + ex.Message);
            }
        }

        public string EnsureLocalPlayerId()
        {
            if (string.IsNullOrEmpty(_current.localPlayerId))
            {
                _current.localPlayerId = Guid.NewGuid().ToString("N");
                Save();
            }
            return _current.localPlayerId;
        }

        // persistentDataPath is shared by MPPM clones, so the per-instance suffix keeps their files apart.
        public static SettingsStore CreateForThisInstance()
        {
            var store = new SettingsStore(
                SettingsPaths.SettingsFile(Application.persistentDataPath, InstanceProfile.Current.Suffix));
            store.Load();
            return store;
        }

        private static SettingsData TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var data = new SettingsData();
                JsonUtility.FromJsonOverwrite(json, data);
                return data;
            }
            catch (Exception)
            {
                return null; // JsonUtility throws ArgumentException on malformed JSON
            }
        }
    }
}
