// Authority: none (pure EditMode tests). FishGame.Core.InputContextRouter, spec 8.2 M1 rows + M2 rod rows + contract D18.
using System;
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

        // Checks all 17 intent fields against AllActive(): "true" = equals the AllActive value, "false" = 0f / false.
        // primary covers the three Primary fields, secondary the three Secondary fields.
        private static void AssertRow(PlayerIntent i, string ctx, bool move, bool look, bool jump, bool sprint, bool primary,
                                      bool secondary, bool interact, bool shove, bool drop, bool equip, bool pause)
        {
            PlayerInputFrame a = AllActive();
            Assert.AreEqual(move ? a.MoveX : 0f, i.MoveX, 0f, ctx + " MoveX");
            Assert.AreEqual(move ? a.MoveY : 0f, i.MoveY, 0f, ctx + " MoveY");
            Assert.AreEqual(look ? a.LookYawDeg : 0f, i.LookYawDeg, 0f, ctx + " LookYawDeg");
            Assert.AreEqual(look ? a.LookPitchDeg : 0f, i.LookPitchDeg, 0f, ctx + " LookPitchDeg");
            Assert.AreEqual(jump, i.JumpPressed, ctx + " Jump");
            Assert.AreEqual(sprint, i.SprintHeld, ctx + " Sprint");
            Assert.AreEqual(primary, i.PrimaryHeld, ctx + " PrimaryHeld");
            Assert.AreEqual(primary, i.PrimaryPressed, ctx + " PrimaryPressed");
            Assert.AreEqual(primary, i.PrimaryReleased, ctx + " PrimaryReleased");
            Assert.AreEqual(secondary, i.SecondaryHeld, ctx + " SecondaryHeld");
            Assert.AreEqual(secondary, i.SecondaryPressed, ctx + " SecondaryPressed");
            Assert.AreEqual(secondary, i.SecondaryReleased, ctx + " SecondaryReleased");
            Assert.AreEqual(interact, i.InteractPressed, ctx + " Interact");
            Assert.AreEqual(shove, i.ShovePressed, ctx + " Shove");
            Assert.AreEqual(drop, i.DropPressed, ctx + " Drop");
            Assert.AreEqual(equip, i.EquipPressed, ctx + " Equip");
            Assert.AreEqual(pause, i.PausePressed, ctx + " Pause");
        }

        [Test]
        public void InputContextRouter_RodReady_PassesSecondaryAndMovement()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.RodReady, AllActive());
            AssertRow(i, "RodReady", move: true, look: true, jump: true, sprint: true, primary: false, secondary: true,
                      interact: true, shove: true, drop: false, equip: true, pause: true);
        }

        [Test]
        public void InputContextRouter_RodAiming_PassesChargeBlocksSprintAndInteract()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.RodAiming, AllActive());
            AssertRow(i, "RodAiming", move: true, look: true, jump: true, sprint: false, primary: true, secondary: true,
                      interact: false, shove: false, drop: false, equip: true, pause: true);
        }

        [Test]
        public void InputContextRouter_RodWaiting_PassesCancelAndMovementBlocksPrimary()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.RodWaiting, AllActive());
            AssertRow(i, "RodWaiting", move: true, look: true, jump: true, sprint: true, primary: false, secondary: true,
                      interact: true, shove: true, drop: false, equip: false, pause: true);
        }

        [Test]
        public void InputContextRouter_RodFighting_BlocksMovementPassesLookAndPrimary()
        {
            PlayerInputFrame f = AllActive();
            PlayerIntent i = InputContextRouter.Route(InputContext.RodFighting, in f);
            AssertRow(i, "RodFighting", move: false, look: true, jump: false, sprint: false, primary: true, secondary: false,
                      interact: false, shove: false, drop: false, equip: false, pause: true);
            Assert.AreEqual(0f, i.MoveX, 0f, "MoveX exactly 0");
            Assert.AreEqual(0f, i.MoveY, 0f, "MoveY exactly 0");

            FightInput fight = InputContextRouter.RouteFight(InputContext.RodFighting, in f);
            Assert.AreEqual(0.3f, fight.Steer, 1e-6f);
            Assert.IsTrue(fight.Reel);

            // Reel is "Reel || PrimaryHeld": either alone reels, neither does not.
            var reelOnly = new PlayerInputFrame { Reel = true };
            var primaryOnly = new PlayerInputFrame { PrimaryHeld = true };
            var neither = new PlayerInputFrame { Steer = 0.5f };
            Assert.IsTrue(InputContextRouter.RouteFight(InputContext.RodFighting, in reelOnly).Reel, "Reel alone");
            Assert.IsTrue(InputContextRouter.RouteFight(InputContext.RodFighting, in primaryOnly).Reel, "Primary alone");
            Assert.IsFalse(InputContextRouter.RouteFight(InputContext.RodFighting, in neither).Reel, "neither");
        }

        // Frame "S held + D held + stick": the same physical key is Move.Down and Reel, D is Move.Right and Steer.
        // In no rod context may it both move the player and drive the fight.
        [Test]
        public void InputContextRouter_RodContexts_SameKeyNeverMovesAndReels()
        {
            var f = new PlayerInputFrame { MoveX = 1f, MoveY = -1f, Reel = true, Steer = 1f, PrimaryHeld = false };
            InputContext[] contexts =
            {
                InputContext.RodReady, InputContext.RodAiming, InputContext.RodWaiting, InputContext.RodFighting, InputContext.Free
            };
            foreach (InputContext ctx in contexts)
            {
                PlayerIntent intent = InputContextRouter.Route(ctx, in f);
                FightInput fight = InputContextRouter.RouteFight(ctx, in f);
                bool moving = intent.MoveX != 0f || intent.MoveY != 0f;
                bool fighting = fight.Reel || fight.Steer != 0f;
                Assert.IsFalse(moving && fighting, ctx + " must not move and reel from the same frame");
                if (ctx == InputContext.RodFighting)
                {
                    Assert.IsTrue(fighting, "RodFighting reels / steers");
                    Assert.IsFalse(moving, "RodFighting does not move");
                }
                else
                {
                    Assert.IsTrue(moving, ctx + " still walks");
                    Assert.IsFalse(fighting, ctx + " does not reel or steer");
                }
            }
        }

        [Test]
        public void InputContextRouter_InteractLive_MatchesSpecRows()
        {
            int live = 0, dead = 0;
            foreach (InputContext ctx in (InputContext[])Enum.GetValues(typeof(InputContext)))
            {
                bool expected = ctx == InputContext.Free || ctx == InputContext.RodReady
                                || ctx == InputContext.RodWaiting;
                Assert.AreEqual(expected, InputContextRouter.InteractLive(ctx), ctx.ToString());
                if (expected) live++; else dead++;
                if (InputContextRouter.InteractLive(ctx))
                    Assert.IsTrue(InputContextRouter.Route(ctx, AllActive()).InteractPressed, ctx + ": live implies the router passes Interact");
            }
            Assert.AreEqual(3, live);
            Assert.AreEqual(10, dead);
            Assert.IsFalse(InputContextRouter.InteractLive(InputContext.Carrying), "C-Q2: Carrying is false in M2");
        }

        // Spec 8.2 Swimming row: Interact = "climb at exits" only. World interactables (the bell) are not live while swimming, yet the router
        // still passes Interact so PlayerLocomotion can climb at a WaterExit.
        [Test]
        public void InputContextRouter_Swimming_InteractClimbsButDoesNotReachInteractables()
        {
            Assert.IsFalse(InputContextRouter.InteractLive(InputContext.Swimming), "swimming must not drive PlayerInteractor");
            Assert.IsTrue(InputContextRouter.Route(InputContext.Swimming, AllActive()).InteractPressed, "the climb at exits still reads Interact");
        }

        [Test]
        public void InputContextRouter_NonM1Contexts_RouteLikeFree()
        {
            PlayerInputFrame f = AllActive();
            PlayerIntent free = InputContextRouter.Route(InputContext.Free, in f);
            InputContext[] notYetRouted =
            {
                InputContext.Carrying, InputContext.Staggered, InputContext.Downed, InputContext.Carried, InputContext.Seated
            };
            foreach (InputContext ctx in notYetRouted)
                Assert.AreEqual(free, InputContextRouter.Route(ctx, in f), ctx + " must still route like Free (contract D18, until M5/M6)");
        }
    }
}
