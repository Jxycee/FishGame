// Core, no engine references. Precedence of the effective gameplay context (C M2-D1): Climbing > Swimming > requested > Free.
// PlayerLocomotion (T16) is the only caller that writes the result. Pure and stateless.
namespace FishGame.Core
{
    public static class GameplayContextArbiter
    {
        // Menu is never produced here: MenuOpen stays the input adapter's overlay. A Menu, Climbing or Swimming request
        // while the matching flag is not set falls back to Free.
        public static InputContext Resolve(bool climbing, bool swimming, InputContext requested)
        {
            if (climbing) return InputContext.Climbing;
            if (swimming) return InputContext.Swimming;
            if (requested == InputContext.Menu || requested == InputContext.Climbing || requested == InputContext.Swimming)
                return InputContext.Free;
            return requested;
        }

        // RodReady, RodAiming, RodWaiting, RodFighting (byte values 1..4).
        public static bool IsRodContext(InputContext context)
        {
            return context == InputContext.RodReady || context == InputContext.RodAiming
                || context == InputContext.RodWaiting || context == InputContext.RodFighting;
        }

        // M5 adds Staggered/Downed/Carried.
        public static bool LocksMovement(InputContext context)
        {
            return context == InputContext.RodFighting;
        }

        // RodAiming: the consumer (T16) applies x0.6 speed and no sprint.
        public static bool IsAiming(InputContext context)
        {
            return context == InputContext.RodAiming;
        }
    }
}
