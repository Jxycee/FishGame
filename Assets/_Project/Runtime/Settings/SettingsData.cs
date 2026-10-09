using System;
using System.Text;
using UnityEngine;

namespace FishGame.Runtime
{
    // M1 settings (spec 11.6). Serialized with JsonUtility: public fields only, unknown/missing fields keep defaults.
    // Clamp() runs on every Load, NotifyChanged and Save.
    [Serializable]
    public sealed class SettingsData
    {
        private const string DefaultName = "Fisher";

        public int version = 1;
        public int masterVolume = 80, sfxVolume = 80, ambienceVolume = 70, musicVolume = 60, uiVolume = 80, voiceVolume = 80; // 0..100
        public string displayName = DefaultName;   // 1..16 chars after Clamp (empty/whitespace -> "Fisher")
        public float mouseSensitivity = 0.12f;     // 0.04..0.30 deg/count
        public float gamepadLookSpeed = 1.0f;      // 0.2..3.0 (x 180 deg/s)
        public bool invertY = false;
        public float fieldOfView = 90f;            // horizontal, 80..110
        public float cameraMotion = 1f;            // 0..1
        public bool sprintToggle = false, aimToggle = false, streamerMode = false;
        public int uiScalePercent = 100;           // 80..150
        public string bindingOverridesJson = "";   // InputActionAsset.SaveBindingOverridesAsJson output
        public string localPlayerId = "";          // GUID "N" format, created once per instance file

        public void Clamp()
        {
            masterVolume = ClampInt(masterVolume, 0, 100);
            sfxVolume = ClampInt(sfxVolume, 0, 100);
            ambienceVolume = ClampInt(ambienceVolume, 0, 100);
            musicVolume = ClampInt(musicVolume, 0, 100);
            uiVolume = ClampInt(uiVolume, 0, 100);
            voiceVolume = ClampInt(voiceVolume, 0, 100);
            displayName = NormalizeName(displayName);
            mouseSensitivity = ClampFloat(mouseSensitivity, 0.04f, 0.30f, 0.12f);
            gamepadLookSpeed = ClampFloat(gamepadLookSpeed, 0.2f, 3.0f, 1.0f);
            fieldOfView = ClampFloat(fieldOfView, 80f, 110f, 90f);
            cameraMotion = ClampFloat(cameraMotion, 0f, 1f, 1f);
            uiScalePercent = ClampInt(uiScalePercent, 80, 150);
            if (bindingOverridesJson == null) bindingOverridesJson = "";
            if (localPlayerId == null) localPlayerId = "";
        }

        public SettingsData Clone()
        {
            return JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(this));
        }

        // Removes control chars, trims, truncates to DisplayNameRules.MaxLength, trims the end again; empty -> "Fisher".
        private static string NormalizeName(string raw)
        {
            if (raw == null) return DefaultName;
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (!char.IsControl(c)) sb.Append(c);
            }
            string trimmed = sb.ToString().Trim();
            int max = FishGame.Core.DisplayNameRules.MaxLength;
            if (trimmed.Length > max) trimmed = trimmed.Substring(0, max).TrimEnd();
            return trimmed.Length == 0 ? DefaultName : trimmed;
        }

        private static int ClampInt(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static float ClampFloat(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value)) return fallback;
            return value < min ? min : (value > max ? max : value);
        }
    }
}
