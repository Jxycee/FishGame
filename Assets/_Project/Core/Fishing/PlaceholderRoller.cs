// Core/Fishing/PlaceholderRoller.cs: the M2 placeholder roll (C 7.6); replaced by the M3 FishingResolver.
// Authority: host only (FishingHostLedger rolls at cast accept). Never sets FishingRoll.Pity (the ledger does; no extra draw).
// Fixed draw order (seeds depend on it, overrides never shift it):
//   1 item draw (always one NextFloat, even when forced); 2 junk index (always one Range draw when junk) or CatchMath.RollFish (one draw);
//   3 BitePlanner.Plan (waitBase, n, one per nibble; always drawn, ForcedBiteWait overwrites afterwards); 4 FightSeed (one NextUInt).
using System;

namespace FishGame.Core
{
    public static class PlaceholderRoller
    {
        // effRating 1-2 -> 0.6, 3-5 -> 0.2, 6+ -> 0.05 (before bait.JunkMult)
        public static float JunkWeight(int effRating)
        {
            if (effRating <= 2) return FishingConstants.JunkWeightLowRating;
            if (effRating <= 5) return FishingConstants.JunkWeightMidRating;
            return FishingConstants.JunkWeightHighRating;
        }

        public static bool IsEligible(in FishSpeciesData s, int effRating, float depth)
        {
            return effRating >= s.MinBaitRating && depth >= s.MinDepth;
        }

        public static FishingRoll Roll(FishingDatabase db, in RollRequest req, ref Pcg32 rng)
        {
            BaitStats bait = db.Baits[req.BaitIndex];
            int eff = bait.EffRating(Math.Max(1, req.LoadedCount));
            int speciesCount = db.Species.Count;
            int junkCount = db.Junk.Count;

            // 1. item draw: always exactly one NextFloat
            float u = rng.NextFloat();
            bool pickJunk = false;
            int speciesIndex = -1;
            if (req.ForceJunk)
            {
                pickJunk = true;
            }
            else if (req.ForcedItemIndex >= 0)
            {
                if (req.ForcedItemIndex >= speciesCount) throw new ArgumentOutOfRangeException(nameof(req), "ForcedItemIndex is not a species index.");
                speciesIndex = req.ForcedItemIndex;
            }
            else
            {
                float junkW = junkCount == 0 ? 0f : JunkWeight(eff) * bait.JunkMult;
                float total = junkW;
                int eligible = 0, lastEligible = -1;
                for (int i = 0; i < speciesCount; i++)
                {
                    FishSpeciesData sp = db.Species[i];
                    if (!IsEligible(in sp, eff, req.Depth)) continue;
                    total += 1f; eligible++; lastEligible = i;
                }
                if (eligible == 0 || total <= 0f)
                {
                    pickJunk = true;                       // nothing eligible: junk
                }
                else
                {
                    float target = u * total, acc = 0f;
                    for (int i = 0; i < speciesCount; i++)
                    {
                        FishSpeciesData sp = db.Species[i];
                        if (!IsEligible(in sp, eff, req.Depth)) continue;
                        acc += 1f;
                        if (target < acc) { speciesIndex = i; break; }
                    }
                    if (speciesIndex < 0)                  // past all species: junk, or the last species if junk has no weight
                    {
                        if (junkW > 0f) pickJunk = true; else speciesIndex = lastEligible;
                    }
                }
            }

            if (pickJunk && junkCount == 0)
                throw new InvalidOperationException("PlaceholderRoller needs at least one junk item (T14 guarantees two).");

            // 2. what exactly
            CatchResult catchResult;
            float t;
            FishSpeciesData species = default;
            if (pickJunk)
            {
                int junkIndex = rng.Range(0, junkCount);   // always drawn, even for a single item
                JunkData jd = db.Junk[junkIndex];
                catchResult = CatchMath.Junk((ushort)junkIndex, in jd);
                t = 0f;
            }
            else
            {
                species = db.Species[speciesIndex];
                catchResult = CatchMath.RollFish(in species, (ushort)speciesIndex, req.Luck, ref rng, out t);
            }

            // 3. bite plan (always drawn)
            float mult = BitePlanner.WaitMultiplier(bait.BiteSpeedMod, pickJunk ? 1f : species.BiteWaitMod, req.Depth);
            BitePlan plan = BitePlanner.Plan(in db.Tuning, mult, bait.MaxNibbles, ref rng);
            if (req.ForcedBiteWait > 0f)
            {
                plan.BiteWait = req.ForcedBiteWait; plan.NibbleCount = 0;
                plan.Nibble0 = 0f; plan.Nibble1 = 0f; plan.Nibble2 = 0f;
            }

            // 4. fight seed
            uint seed = rng.NextUInt();
            if (seed == 0) seed = 1;

            return new FishingRoll
            {
                Catch = catchResult, WeightT = pickJunk ? 0f : t,
                BiteWait = plan.BiteWait, NibbleCount = plan.NibbleCount,
                Nibble0 = plan.Nibble0, Nibble1 = plan.Nibble1, Nibble2 = plan.Nibble2,
                FightSeed = seed,
                SizeClass = pickJunk ? (byte)0 : CatchMath.SizeClass(t),
                HookWindow = pickJunk ? 0f : (req.Twitchy ? 1.0f : species.HookWindow),
                Pity = false,
            };
        }
    }
}
