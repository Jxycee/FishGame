// Authority: none (pure EditMode tests). FishGame.Fishing.RodControllerRules.IsAway / AwayStep (spec 7.3, R70, contract M2-D31):
// swimming or climbing puts the rod away (remembered) and brings it back once both are over.
using FishGame.Fishing;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RodAwayStepTests
    {
        private bool _wasAway, _stowed;

        private RodAwayAction Step(bool swimming, bool climbing, bool rodOut)
        {
            return RodControllerRules.AwayStep(ref _wasAway, ref _stowed, RodControllerRules.IsAway(swimming, climbing), rodOut);
        }

        [SetUp]
        public void SetUp()
        {
            _wasAway = false;
            _stowed = false;
        }

        // Catches: the M1 swim behaviour lost (stow on entering water, equip on leaving it).
        [Test]
        public void AwayStep_SwimWithRodOut_StowsThenEquipsOnLeaving()
        {
            Assert.AreEqual(RodAwayAction.Stow, Step(true, false, true));
            Assert.AreEqual(RodAwayAction.None, Step(true, false, false), "still swimming");
            Assert.AreEqual(RodAwayAction.Equip, Step(false, false, false));
        }

        // Catches (the research bug): away read from IsSwimming only, so the swim -> ladder hand-over (IsSwimming clears on ladder entry)
        // equips the rod mid-ladder.
        [Test]
        public void AwayStep_SwimThenClimb_StaysStowedAcrossTheHandOver()
        {
            Assert.AreEqual(RodAwayAction.Stow, Step(true, false, true));
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false), "swim -> climb: still away");
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false));
            Assert.AreEqual(RodAwayAction.Equip, Step(false, false, false), "off the ladder onto the deck");
        }

        // Catches: climbing not counted as away (the rod would stay out on a ladder).
        [Test]
        public void AwayStep_ClimbFromTheDeck_StowsThenEquips()
        {
            Assert.AreEqual(RodAwayAction.Stow, Step(false, true, true));
            Assert.AreEqual(RodAwayAction.Equip, Step(false, false, false));
        }

        // Catches: equipping a rod the player did not have out.
        [Test]
        public void AwayStep_RodNotOut_NeverEquipsAfterwards()
        {
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false));
            Assert.AreEqual(RodAwayAction.None, Step(true, false, false));
            Assert.AreEqual(RodAwayAction.None, Step(false, false, false));
        }

        // Catches: actions repeated every frame in a steady state.
        [Test]
        public void AwayStep_SteadyState_NoAction()
        {
            Assert.AreEqual(RodAwayAction.None, Step(false, false, true));
            Assert.AreEqual(RodAwayAction.None, Step(false, false, true));
            Assert.AreEqual(RodAwayAction.Stow, Step(false, true, true));
            Assert.AreEqual(RodAwayAction.None, Step(false, true, false));
        }
    }
}
