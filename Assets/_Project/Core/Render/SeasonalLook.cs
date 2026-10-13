// Core/Render/SeasonalLook.cs: pure rules of the seasonal scenery (user decision 2026-10-13 "Full seasons", spec 10.2). Authority: none (pure).
// Seasons are 0 Spring, 1 Summer, 2 Autumn, 3 Winter (ArtPaletteDef.seasons order). A season mask uses bit (1 << season), the same layout as
// M3's planned Core/Ecology SeasonMask (Spring 1, Summer 2, Autumn 4, Winter 8), so a later retype keeps serialized bytes.
// Visibility: SeasonalVisibility shows a renderer only in the seasons of its mask (round canopies Spring-Autumn, blossoms Spring, bare
// branches Winter, the snowy pine Winter). Variants: the round trees' foliage variant (autumn orange / red / yellow) is a seeded shuffle of a
// balanced list, so a small grove never comes out lopsided.
using System;

namespace FishGame.Core
{
    public static class SeasonalLook
    {
        public const int SeasonCount = 4;
        public const int Spring = 0, Summer = 1, Autumn = 2, Winter = 3;

        public const byte MaskSpring = 1, MaskSummer = 2, MaskAutumn = 4, MaskWinter = 8;
        public const byte MaskLeafy = MaskSpring | MaskSummer | MaskAutumn;   // deciduous canopies and the snowless pine
        public const byte MaskAll = MaskLeafy | MaskWinter;

        public static bool IsSeason(int season) => season >= 0 && season < SeasonCount;

        // True when `mask` contains `season`. A season outside 0..3 is never visible (callers skip it before asking).
        public static bool IsVisible(byte mask, int season)
        {
            if (!IsSeason(season)) return false;
            return (mask & (1 << season)) != 0;
        }

        // count entries of 0..variants-1, as balanced as possible (each value appears floor or ceil of count / variants times), in an order
        // shuffled by `rng` (Fisher-Yates, from the end). Same rng state = same result. Throws for count < 0 or variants < 1.
        public static int[] BalancedVariants(ref Pcg32 rng, int count, int variants)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "count must be >= 0");
            if (variants < 1) throw new ArgumentOutOfRangeException(nameof(variants), variants, "variants must be >= 1");
            var result = new int[count];
            for (int i = 0; i < count; i++) result[i] = i % variants;
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int swap = result[i]; result[i] = result[j]; result[j] = swap;
            }
            return result;
        }
    }
}
