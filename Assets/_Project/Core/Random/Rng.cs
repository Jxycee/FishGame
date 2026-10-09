// Core/Random/Rng.cs: derives independent deterministic streams from a run seed (spec 4.5).
// Each system uses its own constant stream id, so extra draws in one system never shift another system's rolls.
namespace FishGame.Core
{
    public static class Rng
    {
        public static ulong SplitMix64(ulong x)
        {
            unchecked
            {
                ulong z = x + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // Never returns 0 (Pcg32 seeds must be non-zero).
        public static ulong Derive(ulong runSeed, string streamId, ulong index)
        {
            ulong z = SplitMix64(runSeed ^ Fnv1a64.Hash(streamId));
            z = SplitMix64(z ^ index);
            return z == 0 ? 1UL : z;
        }

        public static Pcg32 Stream(ulong runSeed, string streamId, ulong index)
        {
            return new Pcg32(Derive(runSeed, streamId, index));
        }
    }
}
