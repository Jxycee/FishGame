// Core/Fishing/FishingRollValidation.cs: client-side sanity check of a received roll (spec 9.9, C 7.6). Pure.
// Authority: owner-side check of host data (FishingRollNet.TryToCore). FishingRoll.Pity is ignored.
using System;

namespace FishGame.Core
{
    public static class FishingRollValidation
    {
        public static bool IsSane(in FishingRoll roll, FishingDatabase db)
        {
            if (db == null) return false;
            if (!IsSaneCatch(in roll.Catch, db)) return false;
            if (!IsFinite(roll.BiteWait) || roll.BiteWait < 0f) return false;
            if (roll.NibbleCount > FishingConstants.MaxNibbleSlots) return false;
            if (roll.FightSeed == 0) return false;
            if (roll.SizeClass > 2) return false;
            if (!(roll.WeightT >= 0f && roll.WeightT <= 1f)) return false;      // also rejects NaN
            if (!IsFinite(roll.Nibble0) || !IsFinite(roll.Nibble1) || !IsFinite(roll.Nibble2)) return false;
            if (!IsFinite(roll.HookWindow) || roll.HookWindow < 0f) return false;
            return true;
        }

        // The catch part of IsSane, also used to vet a crew announcement: quality 0..2, item inside the database, species weight in its range,
        // junk weight 0.
        public static bool IsSaneCatch(in CatchResult c, FishingDatabase db)
        {
            if (db == null) return false;
            if ((byte)c.Quality > 2) return false;
            if (c.IsJunk)
            {
                if (c.ItemIndex >= db.Junk.Count) return false;
                if (c.WeightG != 0) return false;
            }
            else
            {
                if (c.ItemIndex >= db.Species.Count) return false;
                FishSpeciesData s = db.Species[c.ItemIndex];
                if (c.WeightG < s.WeightMinG || c.WeightG > s.WeightMaxG) return false;
            }
            return true;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
