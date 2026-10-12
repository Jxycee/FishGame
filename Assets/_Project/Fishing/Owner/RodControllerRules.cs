// Authority: none (pure helpers for the owner's FishingRodController, kept apart so EditMode can test them).
using FishGame.Core;
using UnityEngine;

namespace FishGame.Fishing
{
    internal static class RodControllerRules
    {
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

        // The Hooked-state wire flags (RodWireQuant.Flag*). A dive is a burst with direction 0. Grace = the snap timer is running, i.e. tension
        // has been at 1.0 on the last fight tick (spec 9.5); remotes cannot see that in their smoothed Tension, so they read the flag.
        public static byte HookedFlags(FightPhase phase, sbyte burstDir, bool reeling, bool exhausted, float snapTimer)
        {
            byte flags = 0;
            if (phase == FightPhase.Burst)
                flags |= burstDir == 0 ? (byte)(RodWireQuant.FlagBurst | RodWireQuant.FlagDive) : RodWireQuant.FlagBurst;
            if (reeling) flags |= RodWireQuant.FlagReel;
            if (exhausted) flags |= RodWireQuant.FlagExhausted;
            if (snapTimer > 0f) flags |= RodWireQuant.FlagGrace;
            return flags;
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }
    }
}
