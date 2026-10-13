using System;
using FishGame.Core;

namespace FishGame.Data
{
    // Content data only (not networked). One spawn rule of a zone (spec 12.3): which fish, where, when, with what gates. subAreaIds null or
    // empty = all sub-areas. Mapped to SpawnRuleData inside FishingZoneDef.ToCore (it needs the registry's indices). Public camelCase
    // fields (M3-D76). Owned by T9 (M3).
    [Serializable]
    public sealed class SpawnRule
    {
        public FishSpeciesDef fish;
        public string[] subAreaIds;
        public SeasonMask seasons;
        public WeatherMask weather;
        public TimeBandMask bands;
        public float weight;
        public byte minRodTier;
        public int minBaitRating;
        public float minDepth;
        public bool special;
        public float specialChance;
        public int catchLimitPerRun;
        public int minPlayersInSession;
    }
}
