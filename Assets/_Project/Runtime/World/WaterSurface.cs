namespace FishGame.Runtime
{
    // Authority: none. Pure function shared by every peer. Flat at y 0 until M2's WaveMath lands (spec 10.4).
    public static class WaterSurface
    {
        public const float FlatHeight = 0f;

        public static float HeightAt(float x, float z)
        {
            return FlatHeight;
        }
    }
}
