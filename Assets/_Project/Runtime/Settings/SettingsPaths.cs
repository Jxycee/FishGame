using System;
using System.IO;

namespace FishGame.Runtime
{
    public static class SettingsPaths
    {
        public const string SettingsBaseName = "settings";

        // directory/settings{suffix}.json. Suffix "" for the main instance, "_<profile>" otherwise (InstanceProfile.Suffix).
        public static string SettingsFile(string directory, string suffix)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory is required", nameof(directory));
            return Path.Combine(directory, SettingsBaseName + (suffix ?? "") + ".json");
        }
    }
}
