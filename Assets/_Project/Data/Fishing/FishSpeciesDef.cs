using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One fish species: gameplay fields (mirrored by FishSpeciesData) plus presentation fields
    // (name, body shape, colours, model) that stay Data-only. Peers refer to species by ContentRegistry index. Owned by T13.
    // tags (M3, T3): spec 12.2 FishTags flags, appended after model; the migration wrote bluegill's value.
    public sealed class FishSpeciesDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private FishRarity rarity;
        [SerializeField] private int baseValue;
        [SerializeField] private int weightMinG;
        [SerializeField] private int weightMaxG;
        [SerializeField] private float weightSkew;
        [SerializeField] private float lengthShapeK;
        [SerializeField] private FightProfileDef fight;
        [SerializeField] private BodyShape bodyShape;
        [SerializeField] private Color colorA;
        [SerializeField] private Color colorB;
        [SerializeField] private float biteWaitMod;
        [SerializeField] private float hookWindow;
        [SerializeField] private bool isSpecial;
        [SerializeField] private int minBaitRating;
        [SerializeField] private float minDepth;
        [SerializeField] private Mesh model;
        [SerializeField] private FishTags tags;

        public string Id => id;
        public string DisplayName => displayName;
        public FishRarity Rarity => rarity;
        public int BaseValue => baseValue;
        public int WeightMinG => weightMinG;
        public int WeightMaxG => weightMaxG;
        public float WeightSkew => weightSkew;
        public float LengthShapeK => lengthShapeK;
        public FightProfileDef Fight => fight;
        public BodyShape BodyShape => bodyShape;
        public Color ColorA => colorA;
        public Color ColorB => colorB;
        public float BiteWaitMod => biteWaitMod;
        public float HookWindow => hookWindow;
        public bool IsSpecial => isSpecial;
        public int MinBaitRating => minBaitRating;
        public float MinDepth => minDepth;
        public Mesh Model => model;
        public FishTags Tags => tags;

        // Used by the content builder and by tests. fight and model may be null (the registry validator reports a null fight).
        // tags is an optional trailing parameter (M3-D75): existing callers compile unchanged; the builder and the migration always pass it.
        public void Configure(string id, string displayName, FishRarity rarity, int baseValue, int weightMinG, int weightMaxG,
            float weightSkew, float lengthShapeK, FightProfileDef fight, BodyShape bodyShape, Color colorA, Color colorB,
            float biteWaitMod, float hookWindow, bool isSpecial, int minBaitRating, float minDepth, Mesh model, FishTags tags = FishTags.None)
        {
            this.tags = tags;
            this.id = id;
            this.displayName = displayName;
            this.rarity = rarity;
            this.baseValue = baseValue;
            this.weightMinG = weightMinG;
            this.weightMaxG = weightMaxG;
            this.weightSkew = weightSkew;
            this.lengthShapeK = lengthShapeK;
            this.fight = fight;
            this.bodyShape = bodyShape;
            this.colorA = colorA;
            this.colorB = colorB;
            this.biteWaitMod = biteWaitMod;
            this.hookWindow = hookWindow;
            this.isSpecial = isSpecial;
            this.minBaitRating = minBaitRating;
            this.minDepth = minDepth;
            this.model = model;
        }

        // fightProfileIndex is the ContentRegistry index of `fight` (the registry supplies it).
        public FishSpeciesData ToCore(ushort fightProfileIndex)
        {
            return new FishSpeciesData
            {
                FightProfileIndex = fightProfileIndex,
                WeightMinG = weightMinG,
                WeightMaxG = weightMaxG,
                BaseValue = baseValue,
                MinBaitRating = minBaitRating,
                WeightSkew = weightSkew,
                LengthShapeK = lengthShapeK,
                BiteWaitMod = biteWaitMod,
                HookWindow = hookWindow,
                MinDepth = minDepth,
                IsSpecial = isSpecial,
                Rarity = rarity,
                Tags = tags,
            };
        }
    }
}
