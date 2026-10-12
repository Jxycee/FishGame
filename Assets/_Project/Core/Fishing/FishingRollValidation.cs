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
            if ((byte)roll.Catch.Quality > 2) return false;
            if (roll.Catch.IsJunk)
            {
                if (roll.Catch.ItemIndex >= db.Junk.Count) return false;
                if (roll.Catch.WeightG != 0) return false;
            }
            else
            {
                if (roll.Catch.ItemIndex >= db.Species.Count) return false;
                FishSpeciesData s = db.Species[roll.Catch.ItemIndex];
                if (roll.Catch.WeightG < s.WeightMinG || roll.Catch.WeightG > s.WeightMaxG) return false;
            }
            if (!IsFinite(roll.BiteWait) || roll.BiteWait < 0f) return false;
            if (roll.NibbleCount > FishingConstants.MaxNibbleSlots) return false;
            if (roll.FightSeed == 0) return false;
            if (roll.SizeClass > 2) return false;
            if (!(roll.WeightT >= 0f && roll.WeightT <= 1f)) return false;      // also rejects NaN
            if (!IsFinite(roll.Nibble0) || !IsFinite(roll.Nibble1) || !IsFinite(roll.Nibble2)) return false;
            if (!IsFinite(roll.HookWindow) || roll.HookWindow < 0f) return false;
            return true;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
