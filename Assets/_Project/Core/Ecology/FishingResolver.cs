// Core/Ecology/FishingResolver.cs: spec 12.4 gate and algorithm with a fixed draw order (C 7.6, M3-D12).
// Authority: host only for Resolve (FishingRoller inside FishingHostLedger). Passes/CollectEligible are pure and also feed the
// journal's "biting now" on every peer. Fixed draw order (seeds depend on it; FishingResolver_DrawOrder_HandReplayed):
//   1 junk-share draw (always, B-D1); 2 one draw per eligible special, in rule order, until the first success;
//   3 one pool draw (only when the pool is non-empty); 4 junk item draw (junk path) or CatchMath.RollFish (one draw). No allocation.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class FishingResolver
    {
        private readonly EcologyDatabase _db;

        public FishingResolver(EcologyDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public CatchResult Resolve(in FishingContext ctx, ref Pcg32 rng, CatchLedger ledger)
        {
            return Resolve(in ctx, ref rng, ledger, out _, out _);
        }

        public CatchResult Resolve(in FishingContext ctx, ref Pcg32 rng, CatchLedger ledger, out float weightT, out int ruleIndex)
        {
            ZoneData zone = CheckedZone(in ctx, true);
            IReadOnlyList<SpawnRuleData> rules = zone.Rules;
            weightT = 0f;
            ruleIndex = -1;

            // 1. junk share: always one draw
            if (rng.NextFloat() < JunkShareOf(zone, in ctx)) return JunkItem(zone, ref rng);

            // 2. specials in rule order: one draw each until the first success
            float specialMult = ctx.Bait.SpecialMult * (ctx.InHotspot ? EcologyConstants.HotspotSpecialMult : 1f) * ctx.Events.RareChanceMult;
            for (int i = 0; i < rules.Count; i++)
            {
                SpawnRuleData r = rules[i];
                if (!r.Special || !Passes(in r, in ctx, ledger)) continue;
                float p = Math.Min(EcologyConstants.SpecialChanceCap, r.SpecialChance * specialMult);
                if (rng.NextFloat() < p)
                {
                    ruleIndex = i;
                    return Fish(r.SpeciesIndex, in ctx, ref rng, out weightT);
                }
            }

            // 3. weighted pool of eligible non-special rules; an empty pool gives junk (spec 12.4 step 4)
            float total = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                SpawnRuleData r = rules[i];
                total += PoolWeight(in r, in ctx, ledger);
            }
            if (!(total > 0f) || float.IsInfinity(total)) return JunkItem(zone, ref rng);

            // 4. one draw, walk in rule order (rounding past the end picks the last positive rule)
            float target = rng.NextFloat() * total, acc = 0f;
            int pick = -1;
            for (int i = 0; i < rules.Count; i++)
            {
                SpawnRuleData r = rules[i];
                float w = PoolWeight(in r, in ctx, ledger);
                if (w <= 0f) continue;
                pick = i;
                acc += w;
                if (target < acc) break;
            }
            ruleIndex = pick;
            return Fish(rules[pick].SpeciesIndex, in ctx, ref rng, out weightT);
        }

        // Spec 12.4 gate (C 7.6, B-D7). ledger null skips the catch limit.
        public bool Passes(in SpawnRuleData rule, in FishingContext ctx, CatchLedger ledger)
        {
            if (ctx.SubAreaIndex != EcologyConstants.AnySubArea && rule.SubAreaMask != 0)
            {
                if (ctx.SubAreaIndex >= EcologyConstants.MaxSubAreas || ((rule.SubAreaMask >> ctx.SubAreaIndex) & 1) == 0) return false;
            }
            if ((byte)ctx.Season > (byte)Season.Winter || (rule.Seasons & EcologyMasks.Of(ctx.Season)) == 0) return false;
            if ((rule.Weather & ctx.Weather) == 0) return false;
            if ((rule.Bands & DayClock.BandAt(ctx.Minute)) == 0) return false;
            if (ctx.RodTier < rule.MinRodTier) return false;
            if (ctx.Bait.Rating < rule.MinBaitRating) return false;
            if (!(ctx.Depth >= rule.MinDepth)) return false;                        // +Infinity passes, NaN fails
            if (ctx.PlayersInSession < rule.MinPlayersInSession) return false;
            if (ledger != null && ledger.AtLimit(rule.SpeciesIndex, rule.CatchLimitPerRun)) return false;
            return true;
        }

        public float JunkShare(in FishingContext ctx)
        {
            return JunkShareOf(CheckedZone(in ctx, true), in ctx);
        }

        public int CollectEligible(in FishingContext ctx, CatchLedger ledger, bool specials, List<int> ruleIndicesInto)
        {
            if (ruleIndicesInto == null) throw new ArgumentNullException(nameof(ruleIndicesInto));
            ruleIndicesInto.Clear();
            IReadOnlyList<SpawnRuleData> rules = CheckedZone(in ctx, false).Rules;
            for (int i = 0; i < rules.Count; i++)
            {
                SpawnRuleData r = rules[i];
                if (r.Special == specials && Passes(in r, in ctx, ledger)) ruleIndicesInto.Add(i);
            }
            return ruleIndicesInto.Count;
        }

        // FishingRoller's ForceJunk path: one junk item draw from the context's zone (sub-area not needed).
        internal CatchResult RollJunkItem(in FishingContext ctx, ref Pcg32 rng)
        {
            return JunkItem(CheckedZone(in ctx, false), ref rng);
        }

        // B-D4: NaN gives 0, then clamp to [-1, 1].
        internal static float SaneLuck(float luck)
        {
            if (float.IsNaN(luck)) return 0f;
            return luck < -1f ? -1f : (luck > 1f ? 1f : luck);
        }

        private ZoneData CheckedZone(in FishingContext ctx, bool forRoll)
        {
            if ((uint)ctx.ZoneIndex >= (uint)_db.Zones.Count)
                throw new ArgumentOutOfRangeException(nameof(ctx), "ZoneIndex " + ctx.ZoneIndex + " is not a zone index.");
            ZoneData zone = _db.Zones[ctx.ZoneIndex];
            if (!forRoll) return zone;
            if (!ctx.Events.IsValid) throw new ArgumentException("FishingContext.Events is not valid (use EventMods.Neutral).", nameof(ctx));
            if (!ctx.Skills.IsValid) throw new ArgumentException("FishingContext.Skills is not valid (use SkillMods.Neutral).", nameof(ctx));
            if (ctx.SubAreaIndex >= zone.SubAreas.Count)
                throw new ArgumentOutOfRangeException(nameof(ctx), "SubAreaIndex " + ctx.SubAreaIndex + " is not a sub-area of the zone (AnySubArea is gate-only).");
            return zone;
        }

        // clamp(junkChance * bait.JunkMult * (hotspot ? 0.5 : 1) * events.JunkMult, 0, 0.6); NaN gives 0
        private static float JunkShareOf(ZoneData zone, in FishingContext ctx)
        {
            float j = zone.SubAreas[ctx.SubAreaIndex].JunkChance * ctx.Bait.JunkMult
                      * (ctx.InHotspot ? EcologyConstants.HotspotJunkMult : 1f) * ctx.Events.JunkMult;
            if (!(j > 0f)) return 0f;
            return j > EcologyConstants.JunkShareCap ? EcologyConstants.JunkShareCap : j;
        }

        // Step 3 weight; 0 for specials, failed gates and non-positive or non-finite results (B-D3).
        private float PoolWeight(in SpawnRuleData rule, in FishingContext ctx, CatchLedger ledger)
        {
            if (rule.Special || !Passes(in rule, in ctx, ledger)) return 0f;
            FishSpeciesData s = _db.Fishing.Species[rule.SpeciesIndex];
            float w = rule.Weight;
            if ((s.Tags & ctx.Bait.AffinityTags) != 0) w *= ctx.Bait.AffinityMult;
            w *= ctx.Events.WeightFor(s.Tags) * ctx.Skills.RarityMult(s.Rarity);
            return w > 0f && !float.IsInfinity(w) ? w : 0f;
        }

        private CatchResult Fish(ushort speciesIndex, in FishingContext ctx, ref Pcg32 rng, out float weightT)
        {
            FishSpeciesData s = _db.Fishing.Species[speciesIndex];
            return CatchMath.RollFish(in s, speciesIndex, SaneLuck(ctx.Luck), ref rng, out weightT);
        }

        // Spec 12.4 step 6: one draw over the zone's junk weights, walked in order.
        private CatchResult JunkItem(ZoneData zone, ref Pcg32 rng)
        {
            IReadOnlyList<JunkEntry> junk = zone.Junk;
            float total = 0f;
            for (int i = 0; i < junk.Count; i++) if (junk[i].Weight > 0f) total += junk[i].Weight;
            float target = rng.NextFloat() * total, acc = 0f;
            int pick = 0;
            for (int i = 0; i < junk.Count; i++)
            {
                if (!(junk[i].Weight > 0f)) continue;
                pick = i;
                acc += junk[i].Weight;
                if (target < acc) break;
            }
            ushort index = junk[pick].JunkIndex;
            JunkData data = _db.Fishing.Junk[index];
            return CatchMath.Junk(index, in data);
        }
    }
}
