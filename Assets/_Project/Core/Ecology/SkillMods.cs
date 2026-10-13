// Core/Ecology/SkillMods.cs: map-skill modifiers on the resolver (M4; neutral in M2-M3). A default struct is NOT valid (M3-D46).
// Authority: none (plain data).
namespace FishGame.Core
{
    public struct SkillMods
    {
        public float UncommonRareMult;               // Rare Whisperer x1.15 in M4
        public static readonly SkillMods Neutral = new SkillMods { UncommonRareMult = 1f };

        // UncommonRareMult for Uncommon and Rare, else 1 (Common and Legendary are never boosted).
        public float RarityMult(FishRarity r)
        {
            return r == FishRarity.Uncommon || r == FishRarity.Rare ? UncommonRareMult : 1f;
        }

        public bool IsValid => UncommonRareMult > 0f && !float.IsInfinity(UncommonRareMult);
    }
}
