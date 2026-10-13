// Authority: none (pure helpers for the owner's FishingRodController, kept apart so EditMode can test them).
using FishGame.Core;
using UnityEngine;

namespace FishGame.Fishing
{
    // What swimming or climbing asks the owner controller to do this frame (R70, M2-D31).
    internal enum RodAwayAction : byte { None = 0, Stow = 1, Equip = 2 }

    internal static class RodControllerRules
    {
        // Swimming or on a ladder: the rod is put away (spec 7.3, R70).
        public static bool IsAway(bool swimming, bool climbing)
        {
            return swimming || climbing;
        }

        // The away flag the controller feeds to AwayStep. Coming back is held until the player is grounded and no ladder can retake them: a
        // climber who tops out is in the air for a moment, or lands on the rail inside the volume, and the ladder catches the fall again (the
        // re-entry grace is 0.25 s), which would equip the rod and stow it again with a visible half-animation. Swimming or climbing is
        // always away.
        public static bool EffectiveAway(bool swimming, bool climbing, bool wasAway, bool grounded, bool ladderCatchPending)
        {
            return IsAway(swimming, climbing) || (wasAway && (!grounded || ladderCatchPending));
        }

        // Edge-driven: Stow when going away with the rod out (remembered), Equip when coming back if it was put away by this rule.
        public static RodAwayAction AwayStep(ref bool wasAway, ref bool stowedByAway, bool away, bool rodOut)
        {
            if (away == wasAway) return RodAwayAction.None;
            wasAway = away;
            if (away)
            {
                if (!rodOut) return RodAwayAction.None;
                stowedByAway = true;
                return RodAwayAction.Stow;
            }

            if (!stowedByAway) return RodAwayAction.None;
            stowedByAway = false;
            return RodAwayAction.Equip;
        }

        // C 7.18 table. Free when the rod is stowed.
        public static InputContext ContextFor(RodState state, bool rodOut)
        {
            if (!rodOut) return InputContext.Free;
            switch (state)
            {
                case RodState.Idle:
                    return InputContext.RodReady;
                case RodState.Readying:
                case RodState.Ready:
                case RodState.Charging:
                    return InputContext.RodAiming;
                case RodState.Casting:
                case RodState.Waiting:
                case RodState.Landed:
                case RodState.Snapped:
                case RodState.Escaped:
                    return InputContext.RodWaiting;
                case RodState.Bite:
                case RodState.Hooked:
                    return InputContext.RodFighting;
                default:
                    return InputContext.Free;
            }
        }

        public static bool LocksMovement(RodState state, bool rodOut)
        {
            return rodOut && (state == RodState.Bite || state == RodState.Hooked);
        }

        // Contexts in which the Equip key toggles the rod (C 7.18).
        public static bool EquipAllowed(InputContext context)
        {
            return context == InputContext.Free || context == InputContext.RodReady || context == InputContext.RodAiming;
        }

        public static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        // RodStateData.Equals compares floats with ==, so a NaN BobberPos would look "changed" every frame and resend at the NetVar rate
        // forever. The owner therefore never writes a non-finite point: it keeps the last finite one.
        public static Vector3 FiniteOr(Vector3 candidate, Vector3 fallback)
        {
            return IsFinite(candidate) ? candidate : (IsFinite(fallback) ? fallback : Vector3.zero);
        }

        // R72: FlagBurst while the fish runs (not when spent), FlagRight for a right pull, FlagDive for a back pull; FlagGrace while the
        // strain is over the full bar (SnapTimer > 0): remotes cannot see that in their smoothed Tension, so they read the flag.
        public static byte HookedFlags(FightPhase phase, sbyte burstDir, bool back, bool reeling, bool exhausted, float snapTimer)
        {
            byte flags = 0;
            if (phase == FightPhase.Burst && !exhausted)
            {
                flags |= RodWireQuant.FlagBurst;
                if (burstDir > 0) flags |= RodWireQuant.FlagRight;
                if (back) flags |= RodWireQuant.FlagDive;
            }
            if (reeling) flags |= RodWireQuant.FlagReel;
            if (exhausted) flags |= RodWireQuant.FlagExhausted;
            if (snapTimer > 0f) flags |= RodWireQuant.FlagGrace;
            return flags;
        }

        // Host/owner landing disagreement worth a log line (metres on the water plane). Below it the two are the same spot (float noise).
        public const float LandingMismatchLogMeters = 0.05f;

        // F4/F5 select race: the host answers SelectRodRpc/SelectBaitRpc and derives the landing from ITS rod's castMaxDist, while the owner
        // may have charged with the old rod. The host wins (spec 9.3): the owner adopts the host's x/z and keeps the flat water height, so
        // the published bobber point, the distance gauges and the fish-point sampler all use the spot the host rolled for.
        // A non-finite host landing is ignored (the owner's own landing stays). flatDelta = flat distance between the two landings.
        public static Vector3 AdoptHostLanding(Vector3 ownerLanding, Vector3 hostLanding, float flatHeight, out float flatDelta)
        {
            flatDelta = 0f;
            if (!IsFinite(hostLanding.x) || !IsFinite(hostLanding.z)) return ownerLanding;
            float dx = hostLanding.x - ownerLanding.x, dz = hostLanding.z - ownerLanding.z;
            flatDelta = Mathf.Sqrt(dx * dx + dz * dz);
            return new Vector3(hostLanding.x, flatHeight, hostLanding.z);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }
    }
}
