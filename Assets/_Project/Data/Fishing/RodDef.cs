using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One fishing rod: Core numbers plus shop cost and first-camp unlock. Owned by T13.
    public sealed class RodDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private byte tier;
        [SerializeField] private float castMaxDist;
        [SerializeField] private float reelSpeed;
        [SerializeField] private float lineStrength;
        [SerializeField] private float angleWindow;
        [SerializeField] private float steerRate;
        [SerializeField] private float relaxMult;
        [SerializeField] private int shopCost;
        [SerializeField] private int firstCamp;

        public string Id => id;
        public string DisplayName => displayName;
        public byte Tier => tier;
        public float CastMaxDist => castMaxDist;
        public float ReelSpeed => reelSpeed;
        public float LineStrength => lineStrength;
        public float AngleWindow => angleWindow;
        public float SteerRate => steerRate;
        public float RelaxMult => relaxMult;
        public int ShopCost => shopCost;
        public int FirstCamp => firstCamp;

        // Used by the content builder and by tests.
        public void Configure(string id, string displayName, byte tier, float castMaxDist, float reelSpeed, float lineStrength,
            float angleWindow, float steerRate, float relaxMult, int shopCost, int firstCamp)
        {
            this.id = id;
            this.displayName = displayName;
            this.tier = tier;
            this.castMaxDist = castMaxDist;
            this.reelSpeed = reelSpeed;
            this.lineStrength = lineStrength;
            this.angleWindow = angleWindow;
            this.steerRate = steerRate;
            this.relaxMult = relaxMult;
            this.shopCost = shopCost;
            this.firstCamp = firstCamp;
        }

        // shopCost, firstCamp and displayName are Data-only.
        public RodStats ToCore()
        {
            return new RodStats
            {
                Tier = tier,
                CastMaxDist = castMaxDist,
                ReelSpeed = reelSpeed,
                LineStrength = lineStrength,
                AngleWindow = angleWindow,
                SteerRate = steerRate,
                RelaxMult = relaxMult,
            };
        }
    }
}
