using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One fish fight profile (spec 9.6: the Rust tier plus the R72 quirks). Owned by T13.
    public sealed class FightProfileDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private float calmMin;
        [SerializeField] private float calmMax;
        [SerializeField] private float burstMin;
        [SerializeField] private float burstMax;
        [SerializeField] private float flipRate;
        [SerializeField] private float diveChance;
        [SerializeField] private float burstRamp;
        [SerializeField] private float calmDrift;
        [SerializeField] private float calmDriftRamp;
        [SerializeField] private float flipMinGap;
        [SerializeField] private float lateralJitter;
        // R72 Rust tier (spec 9.6) and the pull-wave period quirk.
        [SerializeField] private float strainMod;
        [SerializeField] private float moveMult;
        [SerializeField] private float reelInMult;
        [SerializeField] private float wavePeriod;

        public string Id => id;
        public string DisplayName => displayName;
        public float CalmMin => calmMin;
        public float CalmMax => calmMax;
        public float BurstMin => burstMin;
        public float BurstMax => burstMax;
        public float FlipRate => flipRate;
        public float DiveChance => diveChance;
        public float BurstRamp => burstRamp;
        public float CalmDrift => calmDrift;
        public float CalmDriftRamp => calmDriftRamp;
        public float FlipMinGap => flipMinGap;
        public float LateralJitter => lateralJitter;
        public float StrainMod => strainMod;
        public float MoveMult => moveMult;
        public float ReelInMult => reelInMult;
        public float WavePeriod => wavePeriod;

        // Used by the content builder and by tests.
        public void Configure(string id, string displayName,
            float calmMin, float calmMax, float burstMin, float burstMax, float flipRate, float diveChance, float burstRamp,
            float calmDrift, float calmDriftRamp, float flipMinGap, float lateralJitter,
            float strainMod, float moveMult, float reelInMult, float wavePeriod)
        {
            this.id = id;
            this.displayName = displayName;
            this.calmMin = calmMin;
            this.calmMax = calmMax;
            this.burstMin = burstMin;
            this.burstMax = burstMax;
            this.flipRate = flipRate;
            this.diveChance = diveChance;
            this.burstRamp = burstRamp;
            this.calmDrift = calmDrift;
            this.calmDriftRamp = calmDriftRamp;
            this.flipMinGap = flipMinGap;
            this.lateralJitter = lateralJitter;
            this.strainMod = strainMod;
            this.moveMult = moveMult;
            this.reelInMult = reelInMult;
            this.wavePeriod = wavePeriod;
        }

        public FishFightProfile ToCore()
        {
            return new FishFightProfile
            {
                CalmMin = calmMin,
                CalmMax = calmMax,
                BurstMin = burstMin,
                BurstMax = burstMax,
                FlipRate = flipRate,
                DiveChance = diveChance,
                BurstRamp = burstRamp,
                CalmDrift = calmDrift,
                CalmDriftRamp = calmDriftRamp,
                FlipMinGap = flipMinGap,
                LateralJitter = lateralJitter,
                StrainMod = strainMod,
                MoveMult = moveMult,
                ReelInMult = reelInMult,
                WavePeriod = wavePeriod,
            };
        }
    }
}
