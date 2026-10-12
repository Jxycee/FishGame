using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One fish fight profile (spec 9.6 columns plus the five motion parameters). Owned by T13.
    public sealed class FightProfileDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private float pullStrength;
        [SerializeField] private float lateralSpeed;
        [SerializeField] private float runSpeed;
        [SerializeField] private float staminaDrain;
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

        public string Id => id;
        public string DisplayName => displayName;
        public float PullStrength => pullStrength;
        public float LateralSpeed => lateralSpeed;
        public float RunSpeed => runSpeed;
        public float StaminaDrain => staminaDrain;
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

        // Used by the content builder and by tests.
        public void Configure(string id, string displayName, float pullStrength, float lateralSpeed, float runSpeed, float staminaDrain,
            float calmMin, float calmMax, float burstMin, float burstMax, float flipRate, float diveChance, float burstRamp,
            float calmDrift, float calmDriftRamp, float flipMinGap, float lateralJitter)
        {
            this.id = id;
            this.displayName = displayName;
            this.pullStrength = pullStrength;
            this.lateralSpeed = lateralSpeed;
            this.runSpeed = runSpeed;
            this.staminaDrain = staminaDrain;
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
        }

        public FishFightProfile ToCore()
        {
            return new FishFightProfile
            {
                PullStrength = pullStrength,
                LateralSpeed = lateralSpeed,
                RunSpeed = runSpeed,
                StaminaDrain = staminaDrain,
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
            };
        }
    }
}
