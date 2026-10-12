// Core/Fishing/FishSpeciesData.cs: gameplay fields of a species; mirrors FishSpeciesDef (C 7.5). No default tuning (C 5.1).
namespace FishGame.Core
{
    public struct FishSpeciesData
    {
        public ushort FightProfileIndex;
        public int WeightMinG, WeightMaxG, BaseValue, MinBaitRating;
        public float WeightSkew, LengthShapeK, BiteWaitMod, HookWindow, MinDepth;
        public bool IsSpecial;
        public FishRarity Rarity;
    }
}
