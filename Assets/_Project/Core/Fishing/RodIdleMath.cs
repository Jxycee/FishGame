// Core/Fishing/RodIdleMath.cs: pure maths of the rod's idle life (playtest F6, 2026-10-13: "the rod seems a bit static outside of when it is being
// strained"). RodPresenter adds, on top of the strain bend: a slow breathing sway, a spring-damped lag of the tip behind the player's movement and
// look, and a slight sag while the line is out. Authority: none (every peer, view only).
// Strain stays dominant (S12: a near-snap must read from the rod alone): every idle term fades out as the bend grows (IdleWeight reaches 0 at
// FadeEndBend01, tension 0.58), the sag fades into the strain bend (TotalBendDeg), and MaxIdleDeg is under 60% of the bend at tension 0.5.
// Camera motion 0 (spec 9.11 removes bob) zeroes breathing and lag exactly; the sag is a pose, not a motion, so it stays.
using System;

namespace FishGame.Core
{
    public static class RodIdleMath
    {
        public const float SagDeg = 3.5f;             // tip-down bend with the line out and no tension
        public const float BreathXDeg = 0.7f;         // slow pitch breathing amplitude
        public const float BreathYDeg = 0.5f;         // slow yaw drift amplitude
        public const float LagMaxDeg = 3f;            // the most the tip trails behind a move or a look flick
        public const float LagDegPerMps = 0.4f;       // trail per m/s of the rod's own velocity (rod-local, perpendicular to the rod)
        // Faster than this is a teleport (respawn, ladder snap, spawn: tens of metres in one frame, hundreds of m/s): ignored, not a full lean. A hard
        // look flick moves the rod tip at about 25 m/s (720 deg/s at 1.5 m) and must still lean it, so the limit is well above that.
        public const float MaxSpeed = 40f;
        public const float FadeEndBend01 = 0.4f;      // idle motion is gone by here (RodBend01 0.4 = tension 0.58, a clearly bent rod)
        public const float MaxIdleDeg = SagDeg + BreathXDeg + LagMaxDeg;

        private const float BreathHzX = 0.23f, BreathHzY = 0.17f, BreathPhaseY = 1.3f;
        private const float SpringOmega = 14f, SpringZeta = 0.55f, MaxStep = 1f / 240f, MaxDt = 0.1f;

        // 1 at rest, 0 once the strain bend is clear (smoothstep over RodBend01 0..FadeEndBend01), scaled by camera motion and faded out by the stow.
        // NaN in any input gives 0.
        public static float IdleWeight(float bend01, float cameraMotion01, float stow01)
        {
            if (float.IsNaN(bend01) || float.IsNaN(cameraMotion01) || float.IsNaN(stow01)) return 0f;
            float s = Clamp01(bend01 / FadeEndBend01);
            return (1f - s * s * (3f - 2f * s)) * Clamp01(cameraMotion01) * (1f - Clamp01(stow01));
        }

        // The rod's total bend about the line axis: the strain bend plus the sag, the sag fading out as the strain takes over. Monotone in bend01.
        public static float TotalBendDeg(float maxBendDeg, float bend01, float sag01)
        {
            float b = Clamp01(bend01);
            return maxBendDeg * b + SagDeg * Clamp01(sag01) * (1f - b);
        }

        // Tip pitch lag about the rod's local X from its local Y velocity (m/s, rod space): moving up trails down (positive = tip down).
        public static float LagXDeg(float vy) => Clamp(LagDegPerMps * SanitizeSpeed(vy), LagMaxDeg);

        // Tip yaw lag about the rod's local Y from its local X velocity: moving right trails left (negative about Y).
        public static float LagYDeg(float vx) => Clamp(-LagDegPerMps * SanitizeSpeed(vx), LagMaxDeg);

        // NaN, infinity and a teleport-sized speed are zero.
        public static float SanitizeSpeed(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > MaxSpeed) return 0f;
            return v;
        }

        // Slow, small, smooth: two sines of different periods (about 4.3 s and 5.9 s) so the pattern does not read as a loop. NaN time gives 0.
        public static void Breath(float time, out float xDeg, out float yDeg)
        {
            if (float.IsNaN(time) || float.IsInfinity(time))
            {
                xDeg = 0f;
                yDeg = 0f;
                return;
            }

            const float twoPi = 6.2831853f;
            xDeg = BreathXDeg * (float)Math.Sin(twoPi * BreathHzX * (double)time);
            yDeg = BreathYDeg * (float)Math.Sin(twoPi * BreathHzY * (double)time + BreathPhaseY);
        }

        // One damped-spring step toward `target` (underdamped, zeta 0.55: a small overshoot, settled in about half a second). Sub-stepped at 1/240 s
        // and the frame capped at 0.1 s so a hitch cannot blow it up; a NaN or non-positive dt leaves it unchanged.
        public static void SpringStep(ref float pos, ref float vel, float target, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            if (float.IsNaN(target) || float.IsInfinity(target)) target = 0f;
            float left = Math.Min(dt, MaxDt);
            while (left > 1e-7f)
            {
                float h = Math.Min(left, MaxStep);
                float accel = SpringOmega * SpringOmega * (target - pos) - 2f * SpringZeta * SpringOmega * vel;
                vel += accel * h;
                pos += vel * h;
                left -= h;
            }
        }

        // current += (target - current) * (1 - exp(-rate * dt)): frame-rate independent; a bad dt or rate keeps current.
        public static float Ease(float current, float target, float rate, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt) || !(rate > 0f)) return current;
            return current + (target - current) * (1f - (float)Math.Exp(-(double)rate * dt));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float Clamp(float v, float limit) => v < -limit ? -limit : (v > limit ? limit : v);
    }
}
