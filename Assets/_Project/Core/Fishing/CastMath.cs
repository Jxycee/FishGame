// Core/Fishing/CastMath.cs: cast charge, distance, flight and landing math (spec 9.3, C 7.5). Pure functions, no state.
// Authority: none (used by the owner for prediction and by the host to re-derive the same numbers).
using System;

namespace FishGame.Core
{
    public static class CastMath
    {
        private const double DegToRad = Math.PI / 180.0;

        // u = held / chargeTime; pingPong ? 1 - |(u mod 2) - 1| : clamp01(u). held <= 0 -> 0.
        public static float ChargeNorm(float heldSeconds, float castChargeTime, bool pingPong)
        {
            if (!(heldSeconds > 0f) || !(castChargeTime > 0f)) return 0f;
            float u = heldSeconds / castChargeTime;
            if (pingPong)
            {
                float m = u % 2f;
                float v = 1f - Math.Abs(m - 1f);
                return v < 0f ? 0f : (v > 1f ? 1f : v);
            }
            return u > 1f ? 1f : u;
        }

        // lerp(min, max, clamp01(chargeNorm))
        public static float Distance(float chargeNorm, float castMinDist, float castMaxDist)
        {
            float c = chargeNorm < 0f ? 0f : (chargeNorm > 1f ? 1f : chargeNorm);
            return castMinDist + (castMaxDist - castMinDist) * c;
        }

        // 0.45 + 0.012 * distance
        public static float FlightTime(float distance)
        {
            return FishingConstants.FlightBaseSeconds + FishingConstants.FlightSecondsPerMeter * distance;
        }

        // 0.7..1.3, linear over -30..45 degrees, clamped.
        public static float PitchApexMult(float pitchDeg)
        {
            float t = (pitchDeg - FishingConstants.PitchApexLowDeg)
                      / (FishingConstants.PitchApexHighDeg - FishingConstants.PitchApexLowDeg);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return FishingConstants.PitchApexMultMin + (FishingConstants.PitchApexMultMax - FishingConstants.PitchApexMultMin) * t;
        }

        // max(0.6, 0.12 * distance) * mult (the floor applies before the pitch multiplier; B-Q8).
        public static float Apex(float distance, float pitchApexMult)
        {
            float a = FishingConstants.ApexPerMeter * distance;
            if (a < FishingConstants.ApexMin) a = FishingConstants.ApexMin;
            return a * pitchApexMult;
        }

        // Flat forward from yaw only: yaw in degrees, clockwise from +Z (x += sin(yaw) * d, z += cos(yaw) * d).
        public static void Landing(float originX, float originZ, float yawDeg, float distance, out float x, out float z)
        {
            double yaw = yawDeg * DegToRad;
            x = originX + (float)(Math.Sin(yaw) * distance);
            z = originZ + (float)(Math.Cos(yaw) * distance);
        }

        // Parabola from start to end with the apex above the higher end: y(0.5) = max(startY, endY) + apex.
        public static void ArcPoint(float t01, float startX, float startY, float startZ, float endX, float endY, float endZ,
                                    float apex, out float x, out float y, out float z)
        {
            float t = t01;
            float s = 1f - t;
            x = startX * s + endX * t;
            z = startZ * s + endZ * t;
            float lift = 4f * (apex + 0.5f * Math.Abs(startY - endY)) * t * s;
            y = startY * s + endY * t + lift;
        }

        // FirstHitIsWater && HasBed && Depth >= minLandDepth
        public static bool IsValidLanding(in WaterProbe probe, float minLandDepth)
        {
            return ProbeReason(in probe, minLandDepth) == CastRejectReason.None;
        }

        // None | NotWater | TooShallow
        public static CastRejectReason ProbeReason(in WaterProbe probe, float minLandDepth)
        {
            if (!probe.FirstHitIsWater) return CastRejectReason.NotWater;
            if (!probe.HasBed || probe.Depth < minLandDepth) return CastRejectReason.TooShallow;
            return CastRejectReason.None;
        }
    }
}
