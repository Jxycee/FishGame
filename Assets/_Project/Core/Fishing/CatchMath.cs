// Core/Fishing/CatchMath.cs: weight, quality, value and length of a catch (spec 9.8, C 7.6; shared with M3).
// Authority: host-side maths (the roll); the host also uses WithCleanBonus for the clean-catch tier (M2-D16).
// One RNG draw per RollFish. All AwayFromZero rounding is explicit.
using System;

namespace FishGame.Core
{
    public static class CatchMath
    {
        // clamp(weightSkew - 0.4 * luck, 1.2, 3.0)
        public static float Skew(float weightSkew, float luck)
        {
            float s = weightSkew - FishingConstants.LuckSkew * luck;
            return s < FishingConstants.SkewMin ? FishingConstants.SkewMin : (s > FishingConstants.SkewMax ? FishingConstants.SkewMax : s);
        }

        // pow(u, skew)
        public static float SkewedT(float u, float skew)
        {
            return (float)Math.Pow(u, skew);
        }

        // round(min + (max - min) * t), AwayFromZero
        public static int WeightG(int minG, int maxG, float t)
        {
            double tc = t < 0f ? 0.0 : (t > 1f ? 1.0 : (double)t);
            return (int)Math.Round(minG + (maxG - minG) * tc, MidpointRounding.AwayFromZero);
        }

        // >= 0.9 Trophy, >= 0.6 Good, else Normal
        public static CatchQuality Quality(float t)
        {
            if (t >= FishingConstants.TrophyT) return CatchQuality.Trophy;
            if (t >= FishingConstants.GoodT) return CatchQuality.Good;
            return CatchQuality.Normal;
        }

        public static float QualityMult(CatchQuality q)
        {
            switch (q)
            {
                case CatchQuality.Trophy: return FishingConstants.TrophyMult;
                case CatchQuality.Good: return FishingConstants.GoodMult;
                default: return 1.0f;
            }
        }

        // max(1, round(base * (0.6 + 0.8 t) * qMult)), AwayFromZero, computed in double
        public static int Value(int baseValue, float t, CatchQuality q)
        {
            double v = baseValue * (FishingConstants.ValueBase + FishingConstants.ValueSpan * (double)t) * QualityMult(q);
            return Math.Max(1, (int)Math.Round(v, MidpointRounding.AwayFromZero));
        }

        // round(400 * cbrt(weightG / 1000.0) * k), AwayFromZero, clamped to the ushort range
        public static ushort LengthMm(int weightG, float lengthShapeK)
        {
            double mm = Math.Round(FishingConstants.LengthScaleMm * Math.Cbrt(weightG / 1000.0) * lengthShapeK, MidpointRounding.AwayFromZero);
            if (mm < 0.0) mm = 0.0;
            if (mm > ushort.MaxValue) mm = ushort.MaxValue;
            return (ushort)mm;
        }

        // t < 1/3 -> 0, < 2/3 -> 1, else 2
        public static byte SizeClass(float t)
        {
            if (t < 1f / 3f) return 0;
            if (t < 2f / 3f) return 1;
            return 2;
        }

        // One draw: u = rng.NextFloat(); t = u ^ skew; weight, quality, value and length follow from t.
        public static CatchResult RollFish(in FishSpeciesData s, ushort speciesIndex, float luck, ref Pcg32 rng, out float t)
        {
            float u = rng.NextFloat();
            t = SkewedT(u, Skew(s.WeightSkew, luck));
            int weight = WeightG(s.WeightMinG, s.WeightMaxG, t);
            CatchQuality q = Quality(t);
            return new CatchResult
            {
                IsJunk = false, ItemIndex = speciesIndex, WeightG = weight, LengthMm = LengthMm(weight, s.LengthShapeK),
                Quality = q, ValueCoins = Value(s.BaseValue, t, q),
            };
        }

        // Weight 0, length 0, Normal, value = the junk item's value (may be 0).
        public static CatchResult Junk(ushort junkIndex, in JunkData junk)
        {
            return new CatchResult { IsJunk = true, ItemIndex = junkIndex, WeightG = 0, LengthMm = 0, Quality = CatchQuality.Normal, ValueCoins = junk.ValueCoins };
        }

        // Normal -> Good -> Trophy -> Trophy
        public static CatchQuality UpgradeOneTier(CatchQuality q)
        {
            switch (q)
            {
                case CatchQuality.Normal: return CatchQuality.Good;
                default: return CatchQuality.Trophy;
            }
        }

        // Clean-catch bonus (M2-D16): +1 tier (capped), value recomputed from the same t; weight and length unchanged; junk unchanged.
        public static CatchResult WithCleanBonus(in CatchResult fish, in FishSpeciesData s, float t)
        {
            if (fish.IsJunk) return fish;
            CatchResult r = fish;
            r.Quality = UpgradeOneTier(fish.Quality);
            r.ValueCoins = Value(s.BaseValue, t, r.Quality);
            return r;
        }
    }
}
