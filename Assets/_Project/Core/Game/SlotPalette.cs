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
            0.90f, 0.36f, 0.24f, // slot 0 coral
            0.22f, 0.52f, 0.90f, // slot 1 blue
            0.30f, 0.72f, 0.32f, // slot 2 green
            0.95f, 0.78f, 0.20f  // slot 3 yellow
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
