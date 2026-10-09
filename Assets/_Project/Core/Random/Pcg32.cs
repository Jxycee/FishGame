// Core/Random/Pcg32.cs: PCG-XSH-RR 32 (O'Neill), seeded exactly like pcg32_srandom_r.
// Pure C#, no engine references. Mutable struct: keep it in a field or local and never copy it mid-stream.
// Always construct with the constructor; default(Pcg32) is not a valid stream.
using System;

namespace FishGame.Core
{
    public struct Pcg32
    {
        public const ulong ZeroSeedReplacement = 0x853C49E6748FEA9BUL;
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _inc;

        public Pcg32(ulong seed, ulong stream = 0)
        {
            if (seed == 0) seed = ZeroSeedReplacement;
            _inc = unchecked((stream << 1) | 1UL);
            _state = 0;
            Step();
            _state = unchecked(_state + seed);
            Step();
        }

        private void Step()
        {
            _state = unchecked(_state * Multiplier + _inc);
        }

        public uint NextUInt()
        {
            ulong old = _state;
            Step();
            uint xorshifted = unchecked((uint)(((old >> 18) ^ old) >> 27));
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        // [0, 1) with 24 bits of precision.
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        // [0, 1) with 53 bits of precision (27 high bits + 26 low bits).
        public double NextDouble()
        {
            ulong hi = NextUInt() >> 5;
            ulong lo = NextUInt() >> 6;
            return ((hi << 26) | lo) * (1.0 / 9007199254740992.0);
        }

        public float Range(float minInclusive, float maxExclusive)
        {
            return minInclusive + NextFloat() * (maxExclusive - minInclusive);
        }

        // Unbiased (rejection sampling). Throws ArgumentException when maxExclusive <= minInclusive.
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException("maxExclusive must be greater than minInclusive.");
            uint range = unchecked((uint)(maxExclusive - minInclusive));
            uint threshold = unchecked(0u - range) % range; // 2^32 mod range
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold)
                    return unchecked(minInclusive + (int)(r % range));
            }
        }
    }
}
