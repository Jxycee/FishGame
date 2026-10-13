// Core/Game/SlotPalette.cs: player slot body colours and the skin tone (spec 7.4, contract 5.4), as 0..1 RGB floats.
// Core has no engine references, so callers build the engine colour themselves (PrefabBuilder materials, T15).
using System;

namespace FishGame.Core
{
    public static class SlotPalette
    {
        public const int Count = 4;

        private static readonly float[] Rgb =
        {
            0.22f, 0.52f, 0.90f, // slot 0 blue (host)
            0.93f, 0.93f, 0.90f, // slot 1 white (off-white so it doesn't clip under lighting)
            0.45f, 0.82f, 0.35f, // slot 2 green (light, so it differs from red in lightness too)
            0.78f, 0.16f, 0.14f  // slot 3 red (dark)
        };

        // Throws ArgumentOutOfRangeException when slot is outside 0..Count-1.
        public static void GetRgb(int slot, out float r, out float g, out float b)
        {
            if (slot < 0 || slot >= Count)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot must be in 0.." + (Count - 1) + ".");
            int i = slot * 3;
            r = Rgb[i];
            g = Rgb[i + 1];
            b = Rgb[i + 2];
        }

        public static void GetSkinRgb(out float r, out float g, out float b)
        {
            r = 0.96f;
            g = 0.80f;
            b = 0.66f;
        }
    }
}
