// Core/Fishing/CreelRules.cs: the creel overweight rule (spec 9.10, C 7.9). Pure.
namespace FishGame.Core
{
    public static class CreelRules
    {
        // A catch is accepted until the creel is already full: Entries < MaxEntries && TotalWeightG < MaxWeightG.
        public static bool CanAccept(in CreelSnapshot c)
        {
            return c.Entries < c.MaxEntries && c.TotalWeightG < c.MaxWeightG;
        }

        // Entries >= 0.8 * MaxEntries || TotalWeightG >= 0.8 * MaxWeightG. Decimal maths: (decimal)0.8f is exactly 0.8m, so
        // exactly 80 percent of 15000 g is 12000 and warns (a float or double product can land a hair above the integer).
        public static bool IsWarn(in CreelSnapshot c)
        {
            decimal f = (decimal)FishingConstants.CreelWarnFraction;
            return c.Entries >= f * c.MaxEntries || c.TotalWeightG >= f * c.MaxWeightG;
        }

        public static bool IsFull(in CreelSnapshot c)
        {
            return !CanAccept(in c);
        }
    }
}
