// Core/Fishing/FightViewMath.cs: pure presentation maths of the R72 fight (spec 9.11). Rust client values: UI bar smoothing clamp01(dt * 10),
// bobber roll +-30 deg toward the pull side, halved on a back pull, eased at clamp01(dt). Authority: none (every peer, view only).
namespace FishGame.Core
{
    public static class FightViewMath
    {
        public const float BarSmoothRate = 10f;
        public const float TiltMaxDeg = 30f;
        public const float CreakFadeSeconds = 0.25f;

        // Bobber plunge (playtest F9, 2026-10-13: "make it easier to tell when the bobber is diving"). Offsets are metres from the water height.
        // A back pull (dive) sinks the float 0.75 m, out of sight: its stem ball tops out StemTipHeight above the float centre, so even the biggest,
        // far-away bobber (DistanceScale) is more than 0.15 m under the highest wave. The old 0.35 m left the float visible through shallow water and
        // was too close to the 0.25 m Bite plunge. The plunge also eases at PlungeEaseRate (a yank); the way back up keeps the normal EaseRate.
        public const float HookedDepth = 0.10f, DiveDepth = 0.75f, ExhaustedLift = 0.03f;
        public const float StemTipHeight = 0.135f;   // top of the stem ball above the float centre (PlayerFishingRigBuilder.BuildBobber)
        public const float EaseRate = 20f, PlungeEaseRate = 50f;
        // A 7-10 cm float is a 4 pixel dot at cast distance (F6: "just a white ball"): it grows in step with the distance to the rod tip beyond
        // FarScaleStart metres, up to MaxDistanceScale. Pure scale, no gameplay meaning.
        public const float FarScaleStart = 5f, MaxDistanceScale = 3.5f;

        // Where a hooked fish's bobber sits relative to the water: a spent fish floats, a back pull plunges, otherwise the fish point just under.
        public static float HookedDepthOffset(bool dive, bool exhausted)
        {
            if (exhausted) return ExhaustedLift;
            return dive ? -DiveDepth : -HookedDepth;
        }

        // The vertical easing rate: fast only for a downward move while diving.
        public static float YEaseRate(bool diving, float currentY, float targetY)
        {
            return diving && targetY < currentY ? PlungeEaseRate : EaseRate;
        }

        // 1 up to FarScaleStart metres, then distance / FarScaleStart, capped at MaxDistanceScale. NaN and negative distances give 1.
        public static float DistanceScale(float distance)
        {
            if (!(distance > FarScaleStart)) return 1f;
            float s = distance / FarScaleStart;
            return s > MaxDistanceScale ? MaxDistanceScale : s;
        }

        // shown += (target - shown) * clamp01(dt * 10). A non-finite or negative dt keeps shown.
        public static float SmoothBar(float shown, float target, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return shown;
            return shown + (target - shown) * Clamp01(dt * BarSmoothRate);
        }

        // 0 = pulling left, 1 = pulling right, 0.5 = at rest; a back pull moves the target halfway back to 0.5.
        public static float TiltTarget(bool run, bool right, bool back)
        {
            if (!run) return 0.5f;
            float t = right ? 1f : 0f;
            return back ? (t + 0.5f) * 0.5f : t;
        }

        public static float TiltStep(float tilt01, float target, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return tilt01;
            return tilt01 + (target - tilt01) * Clamp01(dt);
        }

        // +30 deg (leaning to the player's left) at tilt 0, -30 at tilt 1.
        public static float RollDeg(float tilt01) => TiltMaxDeg - 2f * TiltMaxDeg * tilt01;

        // Fraction of the gauge track for a display tension when the track ends at the snap point.
        public static float GaugeFraction(float tension, float snapTension)
        {
            if (float.IsNaN(tension)) return 0f;
            return snapTension > 0f ? Clamp01(tension / snapTension) : Clamp01(tension);
        }

        // Creak loop gate: 1 while wanted (and the fade is re-armed); then a linear fade over CreakFadeSeconds; stop = true when it has finished.
        public static float CreakFadeStep(bool wanted, ref float fadeLeft, float dt, out bool stop)
        {
            stop = false;
            if (wanted) { fadeLeft = CreakFadeSeconds; return 1f; }
            if (dt > 0f && !float.IsInfinity(dt)) fadeLeft -= dt;
            if (fadeLeft <= 0f) { fadeLeft = 0f; stop = true; return 0f; }
            return fadeLeft / CreakFadeSeconds;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
