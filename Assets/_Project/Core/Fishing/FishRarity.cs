// Core/Fishing/FishRarity.cs: species rarity (C 7.5). Legendary is display only (M3 T3, spec 12.2; M3-D2 keeps the type name FishRarity).
namespace FishGame.Core
{
    public enum FishRarity : byte { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3 }
}
