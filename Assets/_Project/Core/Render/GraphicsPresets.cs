// Core/Render/GraphicsPresets.cs: the Graphics tab's pure numbers (playtest notes S6/S3, 2026-10-13). No engine references.
// A quality preset sets three rows: render scale, shadow tier and anti-aliasing. Display (resolution, window mode), VSync and the frame-rate
// cap are independent of the preset. GraphicsApplier (Presentation) turns these values into URP asset and camera settings; SettingsData
// stores them as plain ints (JsonUtility) and demotes a preset to Custom when a row no longer matches (SettingsData.Clamp).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    // Stored as an int in settings.json: Low=0, Medium=1, High=2, Custom=3.
    public enum QualityPreset { Low = 0, Medium = 1, High = 2, Custom = 3 }

    // Stored as an int. Off disables main-light shadows (URP shadow distance 0).
    public enum ShadowTier { Off = 0, Low = 1, High = 2 }

    // Stored as an int. FXAA is a camera setting (post-processing); MSAA is the URP asset's sample count.
    public enum AaMode { Off = 0, Fxaa = 1, Msaa2x = 2, Msaa4x = 3 }

    // Stored as an int. Fullscreen is exclusive; Borderless is a full-screen window at the desktop size (the project default).
    public enum WindowModeKind { Fullscreen = 0, Borderless = 1, Windowed = 2 }

    public readonly struct GraphicsPresetValues
    {
        public readonly int RenderScalePercent;
        public readonly int Shadows;   // ShadowTier
        public readonly int Aa;        // AaMode

        public GraphicsPresetValues(int renderScalePercent, int shadows, int aa)
        {
            RenderScalePercent = renderScalePercent;
            Shadows = shadows;
            Aa = aa;
        }
    }

    // What a ShadowTier means for URP: main-light shadow distance (m), shadow map resolution (px, a URP ShadowResolution value) and cascades.
    public readonly struct ShadowParams
    {
        public readonly float Distance;
        public readonly int Resolution;
        public readonly int Cascades;

        public ShadowParams(float distance, int resolution, int cascades)
        {
            Distance = distance;
            Resolution = resolution;
            Cascades = cascades;
        }
    }

    public static class GraphicsPresets
    {
        public const int MinRenderScalePercent = 50, MaxRenderScalePercent = 100, RenderScaleStepPercent = 5;
        public const int PresetCount = 3;   // Low, Medium, High (Custom is not a table row)
        public const QualityPreset DefaultPreset = QualityPreset.High;

        // Frame-rate cap choices (index into the settings row); 0 fps means unlimited. The cap only applies with VSync off.
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };
        public const int DefaultFrameCapIndex = 4;   // Unlimited

        // Low is the GTX 900 class target (GTX 960/970 at 1080p, 60 fps): 75 percent render scale (about 56 percent of the pixels), low shadows
        // (a third of the shadow distance, a quarter of the shadow map texels, one cascade), no MSAA, FXAA instead.
        // High is the project's authored look: full resolution, 120 m shadows at 4096 with 4 cascades, 4x MSAA (quality first, user 2026-10-14).
        private static readonly GraphicsPresetValues[] Table =
        {
            new GraphicsPresetValues(75, (int)ShadowTier.Low, (int)AaMode.Fxaa),
            new GraphicsPresetValues(100, (int)ShadowTier.Low, (int)AaMode.Msaa2x),
            new GraphicsPresetValues(100, (int)ShadowTier.High, (int)AaMode.Msaa4x),
        };

        // Shadow distance, ShadowResolution value, cascades. High matches the checked-in PC_RPAsset (120 m, 4096, 4 cascades; RenderingSetupBuilder
        // writes it). Quality first (user, 2026-10-14): High was 70 m / 2048 / 2 cascades while the GTX 900 class capped the look.
        private static readonly ShadowParams[] Shadows =
        {
            new ShadowParams(0f, 512, 1),       // Off: distance 0 makes URP skip the main-light shadow pass; the rest is unused
            new ShadowParams(25f, 1024, 1),     // Low
            new ShadowParams(120f, 4096, 4),    // High
        };

        // False for Custom or any out-of-range preset (the caller keeps the rows as they are).
        public static bool TryGet(int preset, out GraphicsPresetValues values)
        {
            if (preset >= 0 && preset < PresetCount)
            {
                values = Table[preset];
                return true;
            }

            values = default;
            return false;
        }

        // Scene detail (harbour round 0, 2026-10-14): collider-free dressing (plants, clutter, gulls, flags, string lights) is hidden on Low, the
        // GTX 900 class target. Custom keeps it while its render scale is above Low's 75 percent (a Custom tweak of Low stays lean). Gameplay never
        // changes: no detail object carries a collider.
        public static bool ShowsSceneDetail(int preset, int renderScalePercent)
        {
            if (preset == (int)QualityPreset.Low) return false;
            if (preset == (int)QualityPreset.Custom) return renderScalePercent > Table[(int)QualityPreset.Low].RenderScalePercent;
            return true;
        }

        // Screen-space effects (quality-first round 1, 2026-10-14): URP SSAO (sampled by FishGame/Toon) and the quality post volume (bloom,
        // vignette) follow the same rule as the scene detail: on for Medium and High, off for the Low fallback, and on for a Custom set above Low's
        // render scale. Not a settings row (no save-data change): derived from the preset like ShowsSceneDetail.
        public static bool UsesScreenEffects(int preset, int renderScalePercent)
        {
            return ShowsSceneDetail(preset, renderScalePercent);
        }

        // The preset whose three rows equal these values, or Custom.
        public static int Match(int renderScalePercent, int shadows, int aa)
        {
            for (int i = 0; i < PresetCount; i++)
            {
                GraphicsPresetValues v = Table[i];
                if (v.RenderScalePercent == renderScalePercent && v.Shadows == shadows && v.Aa == aa) return i;
            }

            return (int)QualityPreset.Custom;
        }

        public static ShadowParams ShadowFor(int tier)
        {
            int i = tier < 0 ? 0 : (tier >= Shadows.Length ? Shadows.Length - 1 : tier);
            return Shadows[i];
        }

        // MSAA samples a mode needs (1 = none).
        public static int MsaaSamples(int aa)
        {
            return aa == (int)AaMode.Msaa2x ? 2 : (aa == (int)AaMode.Msaa4x ? 4 : 1);
        }

        public static bool UsesFxaa(int aa)
        {
            return aa == (int)AaMode.Fxaa;
        }

        // Render scale rows are percentages in steps of RenderScaleStepPercent, 50..100.
        public static int RenderScaleCount => (MaxRenderScalePercent - MinRenderScalePercent) / RenderScaleStepPercent + 1;

        public static int RenderScaleFromIndex(int index)
        {
            int clamped = index < 0 ? 0 : (index >= RenderScaleCount ? RenderScaleCount - 1 : index);
            return MinRenderScalePercent + clamped * RenderScaleStepPercent;
        }

        // Nearest row for any stored percent.
        public static int RenderScaleToIndex(int percent)
        {
            int p = percent < MinRenderScalePercent ? MinRenderScalePercent : (percent > MaxRenderScalePercent ? MaxRenderScalePercent : percent);
            return (p - MinRenderScalePercent + RenderScaleStepPercent / 2) / RenderScaleStepPercent;
        }

        // The resolution choices: index 0 is "native" (0 x 0 = leave the desktop resolution alone), then the distinct (width, height)
        // pairs of the monitor's modes, ascending, at least 640 x 480. A stored resolution the monitor does not list is kept as an extra entry
        // so it survives a round trip (a laptop unplugged from its external screen must not silently lose the setting).
        public static List<(int Width, int Height)> BuildResolutionList(IEnumerable<(int Width, int Height)> modes, int storedWidth, int storedHeight)
        {
            var list = new List<(int Width, int Height)> { (0, 0) };
            var distinct = new SortedSet<(int Width, int Height)>();
            if (modes != null)
            {
                foreach ((int w, int h) in modes)
                {
                    if (w >= 640 && h >= 480) distinct.Add((w, h));
                }
            }

            if (storedWidth >= 640 && storedHeight >= 480) distinct.Add((storedWidth, storedHeight));
            list.AddRange(distinct);
            return list;
        }

        // Index of (width, height) in a BuildResolutionList result; 0 (native) when it is not there.
        public static int ResolutionIndex(IReadOnlyList<(int Width, int Height)> list, int width, int height)
        {
            if (list == null) return 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Width == width && list[i].Height == height) return i;
            }

            return 0;
        }
    }
}
