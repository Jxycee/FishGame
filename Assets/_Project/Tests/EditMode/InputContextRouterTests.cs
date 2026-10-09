// Authority: none (pure EditMode tests). FishGame.Core.InputContextRouter, spec 8.2 M1 rows + contract D18.
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class InputContextRouterTests
    {
        private static PlayerInputFrame AllActive()
        {
            return new PlayerInputFrame
            {
                MoveX = 0.5f,
                MoveY = -1f,
                LookYawDeg = 5f,
                LookPitchDeg = 2f,
                JumpPressed = true,
                SprintHeld = true,
                PrimaryHeld = true,
                PrimaryPressed = true,
                PrimaryReleased = true,
                SecondaryHeld = true,
                SecondaryPressed = true,
                SecondaryReleased = true,
                InteractPressed = true,
                ShovePressed = true,
                DropPressed = true,
                EquipPressed = true,
                PausePressed = true,
                Steer = 0.3f,
                Reel = true,
            };
        }

        [Test]
        public void InputContextRouter_Free_PassesMovementAndInteract()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Free, AllActive());
            Assert.AreEqual(0.5f, i.MoveX, 1e-6f);
            Assert.AreEqual(-1f, i.MoveY, 1e-6f);
            Assert.AreEqual(5f, i.LookYawDeg, 1e-6f);
            Assert.AreEqual(2f, i.LookPitchDeg, 1e-6f);
            Assert.IsTrue(i.JumpPressed, "Jump");
            Assert.IsTrue(i.SprintHeld, "Sprint");
            Assert.IsTrue(i.InteractPressed, "Interact");
            Assert.IsTrue(i.ShovePressed, "Shove");
            Assert.IsTrue(i.EquipPressed, "Equip");
            Assert.IsTrue(i.PausePressed, "Pause");
            Assert.IsFalse(i.PrimaryHeld, "PrimaryHeld");
            Assert.IsFalse(i.PrimaryPressed, "PrimaryPressed");
            Assert.IsFalse(i.PrimaryReleased, "PrimaryReleased");
            Assert.IsFalse(i.SecondaryHeld, "SecondaryHeld");
            Assert.IsFalse(i.SecondaryPressed, "SecondaryPressed");
            Assert.IsFalse(i.SecondaryReleased, "SecondaryReleased");
            Assert.IsFalse(i.DropPressed, "Drop");
        }

        [Test]
        public void InputContextRouter_Swimming_BlocksSprintAndShove()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Swimming, AllActive());
            Assert.AreEqual(0.5f, i.MoveX, 1e-6f);
            Assert.AreEqual(-1f, i.MoveY, 1e-6f);
            Assert.AreEqual(5f, i.LookYawDeg, 1e-6f);
            Assert.AreEqual(2f, i.LookPitchDeg, 1e-6f);
            Assert.IsTrue(i.JumpPressed, "Jump");
            Assert.IsTrue(i.InteractPressed, "Interact");
            Assert.IsTrue(i.PausePressed, "Pause");
            Assert.IsFalse(i.SprintHeld, "Sprint");
            Assert.IsFalse(i.ShovePressed, "Shove");
            Assert.IsFalse(i.DropPressed, "Drop");
            Assert.IsFalse(i.EquipPressed, "Equip");
            Assert.IsFalse(i.PrimaryHeld, "PrimaryHeld");
            Assert.IsFalse(i.PrimaryPressed, "PrimaryPressed");
            Assert.IsFalse(i.PrimaryReleased, "PrimaryReleased");
            Assert.IsFalse(i.SecondaryHeld, "SecondaryHeld");
            Assert.IsFalse(i.SecondaryPressed, "SecondaryPressed");
            Assert.IsFalse(i.SecondaryReleased, "SecondaryReleased");
        }

        [Test]
        public void InputContextRouter_Climbing_PassesOnlyLookAndPause()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Climbing, AllActive());
            Assert.AreEqual(5f, i.LookYawDeg, 1e-6f);
            Assert.AreEqual(2f, i.LookPitchDeg, 1e-6f);
            Assert.IsTrue(i.PausePressed, "Pause");
            Assert.AreEqual(0f, i.MoveX, 0f);
            Assert.AreEqual(0f, i.MoveY, 0f);
            Assert.IsFalse(i.JumpPressed, "Jump");
            Assert.IsFalse(i.SprintHeld, "Sprint");
            Assert.IsFalse(i.InteractPressed, "Interact");
            Assert.IsFalse(i.ShovePressed, "Shove");
            Assert.IsFalse(i.DropPressed, "Drop");
            Assert.IsFalse(i.EquipPressed, "Equip");
            Assert.IsFalse(i.PrimaryHeld, "PrimaryHeld");
            Assert.IsFalse(i.PrimaryPressed, "PrimaryPressed");
            Assert.IsFalse(i.PrimaryReleased, "PrimaryReleased");
            Assert.IsFalse(i.SecondaryHeld, "SecondaryHeld");
            Assert.IsFalse(i.SecondaryPressed, "SecondaryPressed");
            Assert.IsFalse(i.SecondaryReleased, "SecondaryReleased");
        }

        [Test]
        public void InputContextRouter_Menu_ZeroesGameplay()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Menu, AllActive());
            Assert.IsTrue(i.PausePressed, "Pause");
            Assert.AreEqual(0f, i.MoveX, 0f);
            Assert.AreEqual(0f, i.MoveY, 0f);
            Assert.AreEqual(0f, i.LookYawDeg, 0f);
            Assert.AreEqual(0f, i.LookPitchDeg, 0f);
            Assert.IsFalse(i.JumpPressed, "Jump");
            Assert.IsFalse(i.SprintHeld, "Sprint");
            Assert.IsFalse(i.InteractPressed, "Interact");
            Assert.IsFalse(i.ShovePressed, "Shove");
            Assert.IsFalse(i.DropPressed, "Drop");
            Assert.IsFalse(i.EquipPressed, "Equip");
            Assert.IsFalse(i.PrimaryHeld, "PrimaryHeld");
            Assert.IsFalse(i.PrimaryPressed, "PrimaryPressed");
            Assert.IsFalse(i.PrimaryReleased, "PrimaryReleased");
            Assert.IsFalse(i.SecondaryHeld, "SecondaryHeld");
            Assert.IsFalse(i.SecondaryPressed, "SecondaryPressed");
            Assert.IsFalse(i.SecondaryReleased, "SecondaryReleased");
        }

        [Test]
        public void InputContextRouter_EnabledMaps_MenuIsUiOnly()
        {
            Assert.AreEqual(InputMaps.UI, InputContextRouter.EnabledMaps(InputContext.Menu));
            Assert.AreEqual(InputMaps.Player | InputMaps.Fishing, InputContextRouter.EnabledMaps(InputContext.Free));
            Assert.AreEqual(InputMaps.Player | InputMaps.Fishing, InputContextRouter.EnabledMaps(InputContext.Swimming));
            Assert.AreEqual(InputMaps.Player | InputMaps.Fishing, InputContextRouter.EnabledMaps(InputContext.Climbing));
            Assert.AreEqual(InputMaps.Player | InputMaps.Boat, InputContextRouter.EnabledMaps(InputContext.Seated));
        }

        [Test]
        public void InputContextRouter_RouteFight_OnlyWhileFightingOrStaggered()
        {
            PlayerInputFrame f = new PlayerInputFrame { Steer = 0.4f, Reel = false, PrimaryHeld = true };

            FightInput fighting = InputContextRouter.RouteFight(InputContext.RodFighting, in f);
            Assert.AreEqual(0.4f, fighting.Steer, 1e-6f);
            Assert.IsTrue(fighting.Reel, "Primary counts as reel while fighting");

            FightInput staggered = InputContextRouter.RouteFight(InputContext.Staggered, in f);
            Assert.AreEqual(0.4f, staggered.Steer, 1e-6f);
            Assert.IsTrue(staggered.Reel, "Primary counts as reel while staggered");

            foreach (InputContext ctx in new[] { InputContext.Free, InputContext.Swimming, InputContext.Climbing, InputContext.Menu })
            {
                FightInput idle = InputContextRouter.RouteFight(ctx, in f);
                Assert.AreEqual(0f, idle.Steer, 0f, ctx.ToString());
                Assert.IsFalse(idle.Reel, ctx.ToString());
            }
        }

        [Test]
        public void InputContextRouter_NonM1Contexts_RouteLikeFree()
        {
            PlayerInputFrame f = AllActive();
            PlayerIntent free = InputContextRouter.Route(InputContext.Free, in f);
            InputContext[] notYetRouted =
            {
                InputContext.RodReady, InputContext.RodAiming, InputContext.RodWaiting, InputContext.RodFighting,
                InputContext.Carrying, InputContext.Staggered, InputContext.Downed, InputContext.Carried, InputContext.Seated
            };
            foreach (InputContext ctx in notYetRouted)
                Assert.AreEqual(free, InputContextRouter.Route(ctx, in f), ctx + " must route like Free in M1 (contract D18)");
        }
    }
}
