using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One bait type. Owned by T13.
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

        public string Id => id;
        public string DisplayName => displayName;
        public int Rating => rating;
        public int StackMax => stackMax;
        public int MaxRating => maxRating;
        public float BiteSpeedMod => biteSpeedMod;
        public float JunkMult => junkMult;
        public int MaxNibbles => maxNibbles;
        public bool Unlimited => unlimited;

        // Used by the content builder and by tests.
        public void Configure(string id, string displayName, int rating, int stackMax, int maxRating, float biteSpeedMod,
            float junkMult, int maxNibbles, bool unlimited)
        {
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
            };
        }
    }
}
