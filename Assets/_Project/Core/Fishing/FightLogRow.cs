// Core/Fishing/FightLogRow.cs: one line of the local fight log (C 7.27, spec 9.12). Plain data; FightLogCsv formats it.
namespace FishGame.Core
{
    public struct FightLogRow
    {
        public string Profile, Species, Rod;
        public int WeightG;
        public float CastDist, CastToBite, FightSec, MaxTension;
        public FightOutcome Outcome;
        public EscapeReason Reason;
        public int NearSnapRecoveries;
        public bool Assist, Clean, Pity;   // Pity: R65 / M2-D23, last column
    }
}
