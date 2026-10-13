// Core/World/RippleMath.cs: expanding ring ripples on the water (spec 10.4, C 7.12). Core, pure.
// The FishGame/Water shader (T21) evaluates the same ring, including age < 0 -> 0 (step(0, age)).
using System;

namespace FishGame.Core
{
    public static class RippleMath
    {
        public const int Slots = 8;
        public const float RingSpeed = 2.2f, RingWidth = 0.25f, Lifetime = 2.5f;
        public const float CastLanding = 0.6f, Nibble = 0.2f, Bite = 1.0f, BiteFollow = 0.3f, Snap = 0.8f, Landed = 0.7f, Shove = 1.0f;
        public const float Dive = 1.0f, DiveFollow = 0.5f;   // F9: the bobber's plunge on a back pull, and its two follow-up rings

        // (1 - sat(|dist - age*RingSpeed| / RingWidth)) * sat(1 - age/Lifetime) * strength.
        // age < 0 -> 0 (a ripple stamped ahead of the water clock is invisible; ruling C-Q5). NaN -> 0.
        public static float Ring(float dist, float age, float strength)
        {
            if (float.IsNaN(dist) || float.IsNaN(age) || float.IsNaN(strength)) return 0f;
            if (age < 0f) return 0f;
            float front = 1f - Sat(Math.Abs(dist - age * RingSpeed) / RingWidth);
            float fade = Sat(1f - age / Lifetime);
            float r = front * fade * strength;
            return float.IsNaN(r) ? 0f : r;
        }

        // Round robin: (lastSlot + 1) % Slots for lastSlot in 0..Slots-1, else 0 (-1 means "none yet"). The next slot is the oldest.
        public static int NextSlot(int lastSlot)
        {
            if (lastSlot < 0 || lastSlot >= Slots) return 0;
            return (lastSlot + 1) % Slots;
        }

        private static float Sat(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
