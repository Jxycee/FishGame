// Core/Fishing/BitePlanner.cs: bite wait and nibble plan (spec 9.4, C 7.6). Pure; seeded Pcg32 only.
// Authority: runs on the host inside the roll (PlaceholderRoller); the owner only plays the plan back.
// Fixed draw order (seeds depend on it): waitBase (float), n (int, always drawn), then one jitter per nibble.
using System;

namespace FishGame.Core
{
    public static class BitePlanner
    {
        // lerp(1.0, 0.8, clamp01(depth / 6))
        public static float DepthMult(float depth)
        {
            float t = depth / FishingConstants.DepthMultFullDepth;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return 1f + (FishingConstants.DepthMultMin - 1f) * t;
        }

        public static float WaitMultiplier(float baitBiteSpeedMod, float fishBiteWaitMod, float depth,
                                           float eventMult = 1f, float skillMult = 1f, bool inHotspot = false)
        {
            return baitBiteSpeedMod * fishBiteWaitMod * eventMult * DepthMult(depth) * skillMult * (inHotspot ? 0.6f : 1f);
        }

        // Requires BiteWaitMax > BiteWaitMin (Pcg32.Range throws otherwise; T14 validates the data).
        public static BitePlan Plan(in FishingTuning tuning, float waitMultiplier, int maxNibbles, ref Pcg32 rng)
        {
            float waitBase = rng.Range(tuning.BiteWaitMin, tuning.BiteWaitMax);          // draw 1
            float biteWait = Math.Max(tuning.MinBiteWait, waitBase * waitMultiplier);
            int cap = maxNibbles < 0 ? 0 : (maxNibbles > FishingConstants.MaxNibbleSlots ? FishingConstants.MaxNibbleSlots : maxNibbles);
            int n = rng.Range(0, cap + 1);                                               // draw 2, always made
            if (biteWait < FishingConstants.NibbleMinBiteWait) n = 0;

            float o0 = 0f, o1 = 0f, o2 = 0f;
            float hi = biteWait - FishingConstants.NibbleBiteGapSeconds;
            for (int k = 0; k < n; k++)                                                  // one jitter draw per nibble
            {
                float o = biteWait * (k + 1) / (n + 1)
                          + rng.Range(-FishingConstants.NibbleJitterSeconds, FishingConstants.NibbleJitterSeconds);
                if (o > hi) o = hi;
                if (o < FishingConstants.NibbleMinOffsetSeconds) o = FishingConstants.NibbleMinOffsetSeconds;
                if (k == 0) o0 = o; else if (k == 1) o1 = o; else o2 = o;
            }

            // Sort ascending (at most 3 values).
            if (n >= 2 && o0 > o1) { float s = o0; o0 = o1; o1 = s; }
            if (n >= 3 && o1 > o2) { float s = o1; o1 = o2; o2 = s; }
            if (n >= 3 && o0 > o1) { float s = o0; o0 = o1; o1 = s; }

            return new BitePlan { BiteWait = biteWait, NibbleCount = (byte)n, Nibble0 = o0, Nibble1 = o1, Nibble2 = o2 };
        }
    }
}
