// Core/Journal/BitingNow.cs: the journal's "biting now" badge (spec 12.10, level >= 2; M3-D26). Pure and allocation-free.
// Authority: none (every client evaluates it locally from the replicated world and the local rod).
using System;

namespace FishGame.Core
{
    public static class BitingNow
    {
        // True when any rule of the species in this zone passes with the sub-area gate open (AnySubArea), Depth +Infinity and no ledger.
        // Specials count: all their gates are open, so they always show as biting.
        public static bool IsBiting(FishingResolver resolver, ZoneData zone, ushort speciesIndex, in FishingContext ctx)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            FishingContext open = ctx;
            open.SubAreaIndex = EcologyConstants.AnySubArea;
            open.Depth = float.PositiveInfinity;
            var rules = zone.Rules;
            for (int i = 0; i < rules.Count; i++)
            {
                SpawnRuleData r = rules[i];
                if (r.SpeciesIndex == speciesIndex && resolver.Passes(in r, in open, null)) return true;
            }
            return false;
        }
    }
}
