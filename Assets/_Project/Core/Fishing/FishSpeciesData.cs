// Core/Fishing/FishSpeciesData.cs: gameplay fields of a species; mirrors FishSpeciesDef (C 7.5). No default tuning (C 5.1).
// M3-D3 (T13): no species-level bait or depth gate; gates live only on the zone's SpawnRuleData.
namespace FishGame.Core
{
    public struct FishSpeciesData
    {
        public ushort FightProfileIndex;
        public int WeightMinG, WeightMaxG, BaseValue;
        public float WeightSkew, LengthShapeK, BiteWaitMod, HookWindow;
        public bool IsSpecial;
        public FishRarity Rarity;
        public FishTags Tags;   // M3 T3 (spec 12.2)
    }
}
