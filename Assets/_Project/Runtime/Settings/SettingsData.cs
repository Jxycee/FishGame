using System;
using System.Text;
using UnityEngine;

namespace FishGame.Runtime
{
    // M1 + M2 settings (spec 11.6). Serialized with JsonUtility: public fields only, unknown/missing fields keep defaults.
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
        public int lineGauge = 0;                  // 0..2 = FishGame.Core.LineGaugeMode (Off, Arc, Bar)
        public bool captions = true;
        public bool fishingAssist = false;         // assist-on sets the gauge to Arc: UI rule (T25, ruling G-Q4), never coupled in Clamp
        public string bindingOverridesJson = "";   // InputActionAsset.SaveBindingOverridesAsJson output
        public string localPlayerId = "";          // GUID "N" format, created once per instance file

        // Graphics tab (playtest S6/S3, 2026-10-13). Added without a version bump (C-Q10): an older settings.json simply lacks these keys and
        // JsonUtility.FromJsonOverwrite leaves the defaults below (the High preset), so every other saved setting survives. Pure tables:
        // FishGame.Core.GraphicsPresets; applied by FishGame.Presentation.GraphicsApplier.
        public int graphicsPreset = (int)FishGame.Core.QualityPreset.High;   // 0..3 = QualityPreset (Low, Medium, High, Custom)
        public int renderScalePercent = 100;       // 50..100
        public int shadowQuality = (int)FishGame.Core.ShadowTier.High;       // 0..2 = ShadowTier (Off, Low, High)
        public int antiAliasing = (int)FishGame.Core.AaMode.Msaa4x;          // 0..3 = AaMode (Off, FXAA, MSAA 2x, MSAA 4x)
        public bool vsync = true;
        public int frameRateCap = FishGame.Core.GraphicsPresets.DefaultFrameCapIndex;   // 0..4 = index into GraphicsPresets.FrameCaps (30, 60, 120, 144, Unlimited); only with VSync off
        public int windowMode = (int)FishGame.Core.WindowModeKind.Borderless;   // 0..2 = WindowModeKind (Fullscreen, Borderless, Windowed); Borderless is the project default
        public int resolutionWidth = 0, resolutionHeight = 0;   // 0 x 0 = native (leave the desktop resolution alone); otherwise 640x480 and up

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
            lineGauge = ClampInt(lineGauge, 0, 2);
            ClampGraphics();
            if (bindingOverridesJson == null) bindingOverridesJson = "";
            if (localPlayerId == null) localPlayerId = "";
        }

        // Clamps the Graphics rows. A named preset (Low, Medium, High) whose three rows no longer match its table entry becomes Custom: that is
        // how "changing any row switches to Custom" holds for the UI and for a hand-edited or older file. Custom is never promoted back.
        private void ClampGraphics()
        {
            graphicsPreset = ClampInt(graphicsPreset, 0, 3);
            renderScalePercent = ClampInt(renderScalePercent, FishGame.Core.GraphicsPresets.MinRenderScalePercent, FishGame.Core.GraphicsPresets.MaxRenderScalePercent);
            shadowQuality = ClampInt(shadowQuality, 0, 2);
            antiAliasing = ClampInt(antiAliasing, 0, 3);
            frameRateCap = ClampInt(frameRateCap, 0, FishGame.Core.GraphicsPresets.FrameCaps.Length - 1);
            windowMode = ClampInt(windowMode, 0, 2);
            if (resolutionWidth < 640 || resolutionHeight < 480 || resolutionWidth > 16384 || resolutionHeight > 16384)
            {
                resolutionWidth = 0;
                resolutionHeight = 0;
            }

            if (graphicsPreset != (int)FishGame.Core.QualityPreset.Custom
                && FishGame.Core.GraphicsPresets.Match(renderScalePercent, shadowQuality, antiAliasing) != graphicsPreset)
            {
                graphicsPreset = (int)FishGame.Core.QualityPreset.Custom;
            }
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
            if (trimmed.Length > max)
            {
                int cut = max;
                if (char.IsHighSurrogate(trimmed[cut - 1]) && char.IsLowSurrogate(trimmed[cut])) cut--; // never split a pair
                trimmed = trimmed.Substring(0, cut).TrimEnd();
            }
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
