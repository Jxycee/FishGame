// Core/Fishing/WaterProbe.cs: result of the two cast raycasts (spec 9.3), filled by the Fishing adapter CastProbe (C 7.5).
namespace FishGame.Core
{
    public struct WaterProbe
    {
        public bool FirstHitIsWater;
        public float WaterY;
        public bool HasBed;
        public float Depth;
    }
}
