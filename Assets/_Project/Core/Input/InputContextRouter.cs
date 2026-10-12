// Core, no engine references, unit-tested. Decides which raw frame fields count in each context (spec 8.2).
// M1 rows Free, Swimming, Climbing, Menu; M2 rod rows RodReady, RodAiming, RodWaiting, RodFighting; Carrying, Staggered, Downed, Carried, Seated route like Free until M5/M6 (contract D18).
namespace FishGame.Core
{
    public static class InputContextRouter
    {
        public static PlayerIntent Route(InputContext context, in PlayerInputFrame frame)
        {
            switch (context)
            {
                case InputContext.Swimming: return RouteSwimming(in frame);
                case InputContext.Climbing: return RouteClimbing(in frame);
                case InputContext.Menu: return RouteMenu(in frame);
                case InputContext.RodReady: return RouteRodReady(in frame);
                case InputContext.RodAiming: return RouteRodAiming(in frame);
                case InputContext.RodWaiting: return RouteRodWaiting(in frame);
                case InputContext.RodFighting: return RouteRodFighting(in frame);
                default: return RouteFree(in frame);
            }
        }

        // RodFighting / Staggered: (Steer, Reel || PrimaryHeld). Every other context: no fight input.
        public static FightInput RouteFight(InputContext context, in PlayerInputFrame frame)
        {
            if (context == InputContext.RodFighting || context == InputContext.Staggered)
                return new FightInput(frame.Steer, frame.Reel || frame.PrimaryHeld);
            return default;
        }

        // World interactables (PlayerInteractor): Free, RodReady, RodWaiting -> true (spec 8.2). Swimming is false: its Interact column is "climb at exits"
        // only, and that climb reads the routed Interact straight from RouteSwimming (PlayerLocomotion.TryBeginClimb), not through this gate.
        // Carrying is false in M2: the context is unused (ruling C-Q2; M5 revisits).
        public static bool InteractLive(InputContext context)
        {
            switch (context)
            {
                case InputContext.Free:
                case InputContext.RodReady:
                case InputContext.RodWaiting:
                    return true;
                default:
                    return false;
            }
        }

        // Menu -> UI only; Seated -> Player | Boat; everything else -> Player | Fishing.
        public static InputMaps EnabledMaps(InputContext context)
        {
            switch (context)
            {
                case InputContext.Menu: return InputMaps.UI;
                case InputContext.Seated: return InputMaps.Player | InputMaps.Boat;
                default: return InputMaps.Player | InputMaps.Fishing;
            }
        }

        private static PlayerIntent CopyAll(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                MoveX = f.MoveX,
                MoveY = f.MoveY,
                LookYawDeg = f.LookYawDeg,
                LookPitchDeg = f.LookPitchDeg,
                JumpPressed = f.JumpPressed,
                SprintHeld = f.SprintHeld,
                PrimaryHeld = f.PrimaryHeld,
                PrimaryPressed = f.PrimaryPressed,
                PrimaryReleased = f.PrimaryReleased,
                SecondaryHeld = f.SecondaryHeld,
                SecondaryPressed = f.SecondaryPressed,
                SecondaryReleased = f.SecondaryReleased,
                InteractPressed = f.InteractPressed,
                ShovePressed = f.ShovePressed,
                DropPressed = f.DropPressed,
                EquipPressed = f.EquipPressed,
                PausePressed = f.PausePressed,
            };
        }

        // Free: Move, Look, Jump, Sprint, Interact, Shove, Equip, Pause pass. Primary*, Secondary*, Drop zeroed.
        private static PlayerIntent RouteFree(in PlayerInputFrame f)
        {
            PlayerIntent i = CopyAll(in f);
            i.PrimaryHeld = false;
            i.PrimaryPressed = false;
            i.PrimaryReleased = false;
            i.SecondaryHeld = false;
            i.SecondaryPressed = false;
            i.SecondaryReleased = false;
            i.DropPressed = false;
            return i;
        }

        // Swimming: Move, Look, Jump, Interact (climb at exits), Pause pass. Sprint, Primary*, Secondary*, Shove, Drop, Equip zeroed.
        private static PlayerIntent RouteSwimming(in PlayerInputFrame f)
        {
            PlayerIntent i = CopyAll(in f);
            i.SprintHeld = false;
            i.PrimaryHeld = false;
            i.PrimaryPressed = false;
            i.PrimaryReleased = false;
            i.SecondaryHeld = false;
            i.SecondaryPressed = false;
            i.SecondaryReleased = false;
            i.ShovePressed = false;
            i.DropPressed = false;
            i.EquipPressed = false;
            return i;
        }

        // Climbing: Look, Pause pass. Everything else zeroed.
        private static PlayerIntent RouteClimbing(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                LookYawDeg = f.LookYawDeg,
                LookPitchDeg = f.LookPitchDeg,
                PausePressed = f.PausePressed,
            };
        }

        // RodReady: Move, Look, Jump, Sprint, Secondary*, Interact, Shove, Equip, Pause pass. Primary*, Drop zeroed.
        private static PlayerIntent RouteRodReady(in PlayerInputFrame f)
        {
            PlayerIntent i = CopyAll(in f);
            i.PrimaryHeld = false;
            i.PrimaryPressed = false;
            i.PrimaryReleased = false;
            i.DropPressed = false;
            return i;
        }

        // RodAiming: Move, Look, Jump, Primary*, Secondary*, Equip, Pause pass. Sprint, Interact, Shove, Drop zeroed.
        private static PlayerIntent RouteRodAiming(in PlayerInputFrame f)
        {
            PlayerIntent i = CopyAll(in f);
            i.SprintHeld = false;
            i.InteractPressed = false;
            i.ShovePressed = false;
            i.DropPressed = false;
            return i;
        }

        // RodWaiting: Move, Look, Jump, Sprint, Secondary*, Interact, Shove, Pause pass. Primary*, Drop, Equip zeroed.
        private static PlayerIntent RouteRodWaiting(in PlayerInputFrame f)
        {
            PlayerIntent i = CopyAll(in f);
            i.PrimaryHeld = false;
            i.PrimaryPressed = false;
            i.PrimaryReleased = false;
            i.DropPressed = false;
            i.EquipPressed = false;
            return i;
        }

        // RodFighting: Look, Primary*, Pause pass. Move, Jump, Sprint, Secondary*, Interact, Shove, Drop, Equip zeroed.
        // (Steer / Reel reach the fight through RouteFight; the router only zeroes fields. The movement lock itself is PlayerLocomotion.)
        private static PlayerIntent RouteRodFighting(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                LookYawDeg = f.LookYawDeg,
                LookPitchDeg = f.LookPitchDeg,
                PrimaryHeld = f.PrimaryHeld,
                PrimaryPressed = f.PrimaryPressed,
                PrimaryReleased = f.PrimaryReleased,
                PausePressed = f.PausePressed,
            };
        }

        // Menu: Pause passes. Everything else zeroed (look included).
        private static PlayerIntent RouteMenu(in PlayerInputFrame f)
        {
            return new PlayerIntent { PausePressed = f.PausePressed };
        }
    }
}
