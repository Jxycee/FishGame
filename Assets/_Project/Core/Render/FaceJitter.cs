// Core/Render/FaceJitter.cs: per-face grey jitter for the flat-shaded look (spec 10.2). Core, pure, deterministic.
// Grey is 1 +/- jitter from a SplitMix64 hash of (meshSeed, faceIndex). MeshBuilder stores 0.5 * Grey (BF-7).
// Hash only, no stateful random source (C 2.8 #16).
namespace FishGame.Core
{
    public static class FaceJitter
    {
        // jitter <= 0 or NaN -> exactly 1.
        public static float Grey(uint meshSeed, int faceIndex, float jitter)
        {
            if (!(jitter > 0f)) return 1f;
            ulong h = Rng.SplitMix64(((ulong)meshSeed << 32) | unchecked((uint)faceIndex));
            float u = (float)(h >> 40) * (1f / 16777216f); // 24 bits: [0, 1) and exact
            return 1f + jitter * (2f * u - 1f);
        }
    }
}
