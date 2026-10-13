// Core/Ecology/FishingRoller.cs: the full cast roll on the resolver (C 7.6, M3-D13); replaces PlaceholderRoller.
// Authority: host only (FishingHostLedger at cast accept). Never sets FishingRoll.Pity (the ledger does; no draw).
// Fixed order (seeds depend on it): (a) item - resolver draws, or ForceJunk: one junk draw, or ForcedItemIndex: one RollFish draw
// (gates ignored, ruleIndex -1; B-D5); (b) BitePlanner.Plan (same draws as M2; ForcedBiteWait overwrites afterwards);
// (c) FightSeed = NextUInt (0 -> 1).
using System;

namespace FishGame.Core
{
    public static class FishingRoller
    {
        public static FishingRoll Roll(EcologyDatabase db, FishingResolver resolver, in FishingContext ctx, CatchLedger ledger,
                                       in RollOverrides overrides, int baitIndex, ref Pcg32 rng, out int ruleIndex)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            FishingDatabase fishing = db.Fishing;
            if ((uint)baitIndex >= (uint)fishing.Baits.Count) throw new ArgumentOutOfRangeException(nameof(baitIndex));
            if (!ctx.Events.IsValid || !ctx.Skills.IsValid)
                throw new ArgumentException("FishingContext Events/Skills are not valid (use the Neutral values).", nameof(ctx));
            BaitStats bait = fishing.Baits[baitIndex];

            // (a) item
            CatchResult item;
            float t = 0f;
            ruleIndex = -1;
            if (overrides.ForceJunk)
            {
                item = resolver.RollJunkItem(in ctx, ref rng);
            }
            else if (overrides.ForcedItemIndex >= 0)
            {
                if (overrides.ForcedItemIndex >= fishing.Species.Count)
                    throw new ArgumentOutOfRangeException(nameof(overrides), "ForcedItemIndex is not a species index.");
                FishSpeciesData forced = fishing.Species[overrides.ForcedItemIndex];
                item = CatchMath.RollFish(in forced, (ushort)overrides.ForcedItemIndex, FishingResolver.SaneLuck(ctx.Luck), ref rng, out t);
            }
            else
            {
                item = resolver.Resolve(in ctx, ref rng, ledger, out t, out ruleIndex);
            }

            // (b) bite plan (B-D6)
            FishSpeciesData species = item.IsJunk ? default : fishing.Species[item.ItemIndex];
            float depth = float.IsPositiveInfinity(ctx.Depth) ? FishingConstants.DepthMultFullDepth : (float.IsNaN(ctx.Depth) ? 0f : ctx.Depth);
            float eventMult = ctx.Events.BiteTimeMult < EcologyConstants.EventBiteMultMin ? EcologyConstants.EventBiteMultMin
                            : (ctx.Events.BiteTimeMult > EcologyConstants.EventBiteMultMax ? EcologyConstants.EventBiteMultMax : ctx.Events.BiteTimeMult);
            float mult = BitePlanner.WaitMultiplier(bait.BiteSpeedMod, item.IsJunk ? 1f : species.BiteWaitMod, depth, eventMult, 1f, ctx.InHotspot);
            BitePlan plan = BitePlanner.Plan(in fishing.Tuning, mult, bait.MaxNibbles, ref rng);
            if (overrides.ForcedBiteWait > 0f)
            {
                plan.BiteWait = overrides.ForcedBiteWait; plan.NibbleCount = 0;
                plan.Nibble0 = 0f; plan.Nibble1 = 0f; plan.Nibble2 = 0f;
            }

            // (c) fight seed
            uint seed = rng.NextUInt();
            if (seed == 0) seed = 1;

            return new FishingRoll
            {
                Catch = item, WeightT = item.IsJunk ? 0f : t,
                BiteWait = plan.BiteWait, NibbleCount = plan.NibbleCount,
                Nibble0 = plan.Nibble0, Nibble1 = plan.Nibble1, Nibble2 = plan.Nibble2,
                FightSeed = seed,
                SizeClass = item.IsJunk ? (byte)0 : CatchMath.SizeClass(t),
                HookWindow = item.IsJunk ? 0f : (overrides.Twitchy ? 1.0f : species.HookWindow),
                Pity = false,
            };
        }
    }
}
