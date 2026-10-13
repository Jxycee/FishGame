using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One bait type. Owned by T13.
    // affinity fields (M3, T3; spec 9.7/12.4; the migration wrote them onto the M2 assets).
    public sealed class BaitDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int rating;
        [SerializeField] private int stackMax;
        [SerializeField] private int maxRating;
        [SerializeField] private float biteSpeedMod;
        [SerializeField] private float junkMult;
        [SerializeField] private int maxNibbles;
        [SerializeField] private bool unlimited;
        [SerializeField] private FishTags affinityTags;
        [SerializeField] private float affinityMult = 1f;   // neutral safety default (M3-D75): a 0 here would silently disable specials (B4)
        [SerializeField] private float specialMult = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public int Rating => rating;
        public int StackMax => stackMax;
        public int MaxRating => maxRating;
        public float BiteSpeedMod => biteSpeedMod;
        public float JunkMult => junkMult;
        public int MaxNibbles => maxNibbles;
        public bool Unlimited => unlimited;
        public FishTags AffinityTags => affinityTags;
        public float AffinityMult => affinityMult;
        public float SpecialMult => specialMult;

        // Used by the content builder and by tests. The three M3 parameters are optional and trailing (M3-D75); the builder and the migration
        // always pass explicit values.
        public void Configure(string id, string displayName, int rating, int stackMax, int maxRating, float biteSpeedMod,
            float junkMult, int maxNibbles, bool unlimited, FishTags affinityTags = FishTags.None, float affinityMult = 1f, float specialMult = 1f)
        {
            this.affinityTags = affinityTags;
            this.affinityMult = affinityMult;
            this.specialMult = specialMult;
            this.id = id;
            this.displayName = displayName;
            this.rating = rating;
            this.stackMax = stackMax;
            this.maxRating = maxRating;
            this.biteSpeedMod = biteSpeedMod;
            this.junkMult = junkMult;
            this.maxNibbles = maxNibbles;
            this.unlimited = unlimited;
        }

        public BaitStats ToCore()
        {
            return new BaitStats
            {
                Rating = rating,
                StackMax = stackMax,
                MaxRating = maxRating,
                MaxNibbles = maxNibbles,
                BiteSpeedMod = biteSpeedMod,
                JunkMult = junkMult,
                Unlimited = unlimited,
                AffinityTags = affinityTags,
                AffinityMult = affinityMult,
                SpecialMult = specialMult,
            };
        }
    }
}
