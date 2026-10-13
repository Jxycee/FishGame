// Core/Ecology/SpawnRuleData.cs: one spawn rule of a zone (spec 12.3), species and sub-areas resolved to indices and bits.
// Authority: none (plain data). Rules carry no default tuning (2.8 #22).
namespace FishGame.Core
{
    public struct SpawnRuleData
    {
        public ushort SpeciesIndex; public byte SubAreaMask;              // 0 = all sub-areas
        public SeasonMask Seasons; public WeatherMask Weather; public TimeBandMask Bands;
        public float Weight; public byte MinRodTier; public int MinBaitRating; public float MinDepth;
        public bool Special; public float SpecialChance; public int CatchLimitPerRun;   // -1 unlimited
        public int MinPlayersInSession;                                   // 0 in M3 (M3-D36)
    }
}
