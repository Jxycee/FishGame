// Core/Ecology/SubAreaBandData.cs: one shore-distance band of a zone (spec 12.5). The last band's MaxShoreM is +Infinity.
// Authority: none (plain data built from FishingZoneDef at registry build).
namespace FishGame.Core
{
    public struct SubAreaBandData { public float MinShoreM, MaxShoreM, JunkChance; }
}
