// Authority: none (pure EditMode tests). FishGame.Core.GameplayContextArbiter, contract M2-D1.
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class GameplayContextArbiterTests
    {
        private static InputContext[] AllContexts() => (InputContext[])Enum.GetValues(typeof(InputContext));

        [Test]
        public void GameplayContextArbiter_ClimbingBeatsEverything()
        {
            foreach (bool swimming in new[] { false, true })
                foreach (InputContext requested in AllContexts())
                    Assert.AreEqual(InputContext.Climbing, GameplayContextArbiter.Resolve(true, swimming, requested),
                                    "swimming=" + swimming + " requested=" + requested);
        }

        [Test]
        public void GameplayContextArbiter_SwimmingBeatsRodRequest()
        {
            InputContext[] requests =
            {
                InputContext.Free, InputContext.RodReady, InputContext.RodAiming, InputContext.RodWaiting,
                InputContext.RodFighting, InputContext.Menu
            };
            foreach (InputContext r in requests)
                Assert.AreEqual(InputContext.Swimming, GameplayContextArbiter.Resolve(false, true, r), r.ToString());
        }

        [Test]
        public void GameplayContextArbiter_RodRequest_UsedWhenOnLand()
        {
            InputContext[] requests =
            {
                InputContext.RodReady, InputContext.RodAiming, InputContext.RodWaiting, InputContext.RodFighting,
                InputContext.Free, InputContext.Carrying
            };
            foreach (InputContext r in requests)
                Assert.AreEqual(r, GameplayContextArbiter.Resolve(false, false, r), r.ToString());
            // the other M5/M6 contexts pass through too (everything except Menu, Climbing, Swimming)
            foreach (InputContext r in new[] { InputContext.Staggered, InputContext.Downed, InputContext.Carried, InputContext.Seated })
                Assert.AreEqual(r, GameplayContextArbiter.Resolve(false, false, r), r.ToString());
        }

        [Test]
        public void GameplayContextArbiter_MenuRequest_TreatedAsFree()
        {
            Assert.AreEqual(InputContext.Free, GameplayContextArbiter.Resolve(false, false, InputContext.Menu));
            Assert.AreEqual(InputContext.Free, GameplayContextArbiter.Resolve(false, false, InputContext.Climbing), "Climbing request without the flag");
            Assert.AreEqual(InputContext.Free, GameplayContextArbiter.Resolve(false, false, InputContext.Swimming), "Swimming request without the flag");
            // Menu is never produced, whatever the flags
            foreach (bool climbing in new[] { false, true })
                foreach (bool swimming in new[] { false, true })
                    foreach (InputContext r in AllContexts())
                        Assert.AreNotEqual(InputContext.Menu, GameplayContextArbiter.Resolve(climbing, swimming, r));
        }

        [Test]
        public void GameplayContextArbiter_LocksMovement_OnlyRodFighting()
        {
            foreach (InputContext c in AllContexts())
                Assert.AreEqual(c == InputContext.RodFighting, GameplayContextArbiter.LocksMovement(c), c.ToString());
        }

        [Test]
        public void GameplayContextArbiter_RodContextAndAiming_MatchRows()
        {
            foreach (InputContext c in AllContexts())
            {
                bool rod = c == InputContext.RodReady || c == InputContext.RodAiming
                           || c == InputContext.RodWaiting || c == InputContext.RodFighting;
                Assert.AreEqual(rod, GameplayContextArbiter.IsRodContext(c), "IsRodContext " + c);
                Assert.AreEqual(c == InputContext.RodAiming, GameplayContextArbiter.IsAiming(c), "IsAiming " + c);
            }
        }
    }
}
