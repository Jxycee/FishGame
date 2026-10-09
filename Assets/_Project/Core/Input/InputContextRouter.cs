// Core, no engine references, unit-tested. Decides which raw frame fields count in each context (spec 8.2).
// M1 rows: Free, Swimming, Climbing, Menu. Every other context routes exactly like Free until its milestone (contract D18).
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

        // Menu: Pause passes. Everything else zeroed (look included).
        private static PlayerIntent RouteMenu(in PlayerInputFrame f)
        {
            return new PlayerIntent { PausePressed = f.PausePressed };
        }
    }
}
