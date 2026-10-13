// Authority: test-only (client-side UI; no networked state). Drives the real journal (T21) through UiFlowController: the main-menu button,
// the hint levels from the local save, the in-game open (the UI end of the J path: OpenJournal and HandleBack, internal to FishGame.UI and
// visible to this assembly), the modal seam (MenuOpen, Show and Back close it, the solo game is never paused), the Journal key closing it
// (a fake input service reports the press) and the bite closing it. The input adapter owns the keyboard, so the real key press is a manual
// check; the router rule is covered by the EditMode router test.
using System.Collections;
using System.Collections.Generic;
using FishGame.App;
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Runtime;
using FishGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FishGame.Tests.PlayMode
{
    public class JournalUiTests
    {
        private const int JournalSpecies = 25;

        private UiFlowController _ui;
        private VisualElement _root;
        private SaveService _save;
        private IInputService _input;

        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _ui = null;
            _root = null;
            _save = null;
            _input = null;
            FishingRig rig = _rig;
            _rig = null;
            if (rig != null) yield return FishingPlayModeDriver.TearDown(rig);
            else yield return PlayModeHarness.TearDownApp();
        }

        private IEnumerator BootMenu()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            Resolve();
            _ui.Show(UiScreen.MainMenu);          // refresh the summary against the (possibly replaced) save service
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
        }

        private void Resolve()
        {
            var app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "App");
            _ui = app.GetComponentInChildren<UiFlowController>(true);
            Assert.That(_ui, Is.Not.Null, "UiFlowController");
            _root = _ui.GetComponent<UIDocument>().rootVisualElement;
            _save = app.Services.Get<SaveService>();
            _input = app.Services.Get<IInputService>();
        }

        private static void Press(VisualElement element)
        {
            Assert.That(element, Is.Not.Null, "element to press");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = element;
                element.SendEvent(e);
            }
        }

        private VisualElement Modal => _root.Q<VisualElement>("JournalModal");

        private bool ModalShown => Modal.resolvedStyle.display == DisplayStyle.Flex;

        private List<VisualElement> Cards() => _root.Query<VisualElement>(className: "journal-card").ToList();

        private VisualElement Card(string speciesId)
        {
            var card = _root.Q<VisualElement>("JournalCard_" + speciesId);
            Assert.That(card, Is.Not.Null, "card for " + speciesId);
            return card;
        }

        private static string NameOf(VisualElement card) => card.Q<Label>(className: "journal-name").text;

        // The shown info lines (seen at, caught, weight, best), one Label each, joined with a newline.
        private static string InfoOf(VisualElement card)
        {
            var lines = new List<string>();
            foreach (Label line in card.Query<Label>(className: "journal-line").ToList())
            {
                if (line.resolvedStyle.display == DisplayStyle.Flex) lines.Add(line.text);
            }
            return string.Join("\n", lines);
        }

        // Catches: the Journal button or its summary line missing, the modal not opening over the menu, the menu staying reachable behind it
        // (gamepad), the cursor-free Menu context not held, a card count that is not the journal list, level 0 showing a name, and the modal
        // not closing on its Close button or when a screen change shows over it.
        [UnityTest]
        public IEnumerator Journal_MainMenu_OpensTwentyFiveHiddenCardsAndCloses()
        {
            yield return BootMenu();

            Assert.That(_root.Q<Label>("MetaSummary").text, Is.EqualTo("Journal 0/" + JournalSpecies), "summary line on the menu");
            Assert.That(_root.Q<Button>("JournalButton").text, Is.EqualTo("Journal"));
            Assert.That(ModalShown, Is.False, "closed at start");

            Press(_root.Q<Button>("JournalButton"));
            yield return PlayModeHarness.WaitFrames(3, 0.2f);

            Assert.That(ModalShown, Is.True, "the journal is open");
            Assert.That(_input.MenuOpen, Is.True, "Menu context while the journal is open");
            Assert.That(_root.Q<VisualElement>("ScreenMainMenu").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "menu hidden behind the modal");

            List<VisualElement> cards = Cards();
            Assert.That(cards.Count, Is.EqualTo(JournalSpecies), "one card per journal species");
            foreach (VisualElement card in cards)
            {
                Assert.That(card.ClassListContains("level-0"), Is.True, card.name + " starts at level 0");
                Assert.That(card.focusable, Is.True, card.name + " is a focus stop");
                Assert.That(card.Q<Label>(className: "journal-name").text, Is.EqualTo("???"), card.name + " hides its name");
                Assert.That(InfoOf(card), Does.StartWith("Maybe at: "), card.name + " hints a spot");
            }
            Assert.That(_root.Q<Label>("JournalCompletion").text, Is.EqualTo("0/" + JournalSpecies + " caught"));
            Assert.That(_root.Q<Button>("JournalFilterAll").ClassListContains("selected"), Is.True, "All spots is the menu default");
            Assert.That(_root.Q<Toggle>("JournalFilterBiting").enabledSelf, Is.False, "biting-now needs a world");

            Press(_root.Q<Button>("JournalClose"));
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            Assert.That(ModalShown, Is.False, "closed by the Close button");
            Assert.That(_root.Q<VisualElement>("ScreenMainMenu").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "menu is back");
            Assert.That(_input.MenuOpen, Is.True, "the menu screen keeps the Menu context");

            // A screen change closes an open modal first (UiFlowController.Show).
            Press(_root.Q<Button>("JournalButton"));
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(ModalShown, Is.True);
            _ui.Show(UiScreen.Settings);
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(ModalShown, Is.False, "Show closes the journal");
            Assert.That(_root.Q<VisualElement>("ScreenSettings").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        // Catches: a hint level that does not follow the save, the sub-area text, the level 3 weight range and best, the completion count
        // (Caught >= 1 only), and the card list not following a catch that lands while the journal is open (SaveService.Changed).
        [UnityTest]
        public IEnumerator Journal_Levels_FollowTheSave()
        {
            yield return BootMenu();
            _save.Current.Journal.Clear();
            _save.Current.Journal["bluegill"] = new JournalEntry { Seen = 1, SeenInSpots = new List<string> { "lake_dock" } };
            _save.Current.Journal["yellow_perch"] = new JournalEntry { Seen = 2, CrewLanded = 1, SeenInSpots = new List<string> { "lake_dock" } };
            _save.Current.Journal["grumpy_carp"] = new JournalEntry
            {
                Seen = 4, CrewLanded = 3, Caught = 3, BestWeightG = 4200, BestLengthMm = 500, BestQuality = 1,
                SeenInSpots = new List<string> { "lake_dock" },
            };
            _save.NotifyChanged();

            _ui.Show(UiScreen.MainMenu);
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(_root.Q<Label>("MetaSummary").text, Is.EqualTo("Journal 1/" + JournalSpecies), "only Caught >= 1 counts");

            Press(_root.Q<Button>("JournalButton"));
            yield return PlayModeHarness.WaitFrames(3, 0.2f);

            VisualElement bluegill = Card("bluegill");
            Assert.That(bluegill.ClassListContains("level-1"), Is.True, "Seen only is level 1");
            Assert.That(NameOf(bluegill), Is.EqualTo("Bluegill"));
            Assert.That(InfoOf(bluegill), Does.Contain("Seen at: Lake Dock ("), "level 1 names the spot and its sub-areas");
            Assert.That(InfoOf(bluegill), Does.Contain("Reeds").And.Contain("Open Water"), "title-cased sub-areas of the bluegill rules");
            Assert.That(bluegill.Q<VisualElement>(className: "journal-glyphs").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "no glyphs below level 2");

            VisualElement perch = Card("yellow_perch");
            Assert.That(perch.ClassListContains("level-2"), Is.True, "a crew landing is level 2");
            Assert.That(perch.Q<VisualElement>(className: "journal-glyphs").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "glyph row from level 2");

            VisualElement carp = Card("grumpy_carp");
            Assert.That(carp.ClassListContains("level-3"), Is.True, "three catches is level 3");
            Assert.That(InfoOf(carp), Does.Contain("Caught: 3"));
            Assert.That(InfoOf(carp), Does.Contain("Weight: 2.0-12.0 kg"));
            Assert.That(InfoOf(carp), Does.Contain("Best: 4.2 kg, 500 mm, Good"));
            Assert.That(_root.Q<Label>("JournalCompletion").text, Is.EqualTo("1/" + JournalSpecies + " caught"));

            VisualElement unseen = Card("lake_pike");
            Assert.That(unseen.ClassListContains("level-0"), Is.True);
            Assert.That(NameOf(unseen), Is.EqualTo("???"));

            // A catch landing while the journal is open updates it live.
            _save.Current.Journal["lake_pike"] = new JournalEntry { Seen = 1, CrewLanded = 1, Caught = 1, BestWeightG = 1500, BestLengthMm = 400, SeenInSpots = new List<string> { "lake_dock" } };
            _save.NotifyChanged();
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(unseen.ClassListContains("level-2"), Is.True, "the open journal follows SaveService.Changed");
            Assert.That(NameOf(unseen), Is.EqualTo("Lake Pike"));
            Assert.That(_root.Q<Label>("JournalCompletion").text, Is.EqualTo("2/" + JournalSpecies + " caught"));

            Press(_root.Q<Button>("JournalClose"));
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
        }

        // Catches: the in-game open (default spot filter, biting-now available with world state), the modal not holding the Menu context,
        // Esc/B (HandleBack) opening Pause behind it instead of closing it, and the solo game being paused by the modal (spec 12.10).
        [UnityTest]
        public IEnumerator Journal_InGame_OpensOnTheCurrentSpotAndBackClosesIt()
        {
            using (var log = new PlayModeHarness.LogCapture())
            {
                yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
                Resolve();
                yield return PlayModeHarness.StartSoloAndWaitForPlayer();
                yield return PlayModeHarness.WaitFrames(5, 0.3f);
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.Hud), "in game");
                Assert.That(_input.MenuOpen, Is.False, "gameplay context before the journal");

                _ui.OpenJournal();
                yield return PlayModeHarness.WaitFrames(3, 0.2f);

                Assert.That(ModalShown, Is.True, "the journal opened over the HUD");
                Assert.That(_input.MenuOpen, Is.True, "Menu context: cursor free, gameplay input off");
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.Hud), "the HUD screen stays; the modal is not a screen");
                Assert.That(Time.timeScale, Is.EqualTo(1f), "the modal never pauses the game");

                Button selectedChip = null;
                foreach (Button chip in _root.Query<Button>(className: "journal-filter").ToList())
                {
                    if (chip.ClassListContains("selected")) selectedChip = chip;
                }
                Assert.That(selectedChip, Is.Not.Null, "a spot filter is selected");
                Assert.That(selectedChip.text, Is.EqualTo("Lake Dock"), "the default filter is the current spot");
                Assert.That(_root.Q<Toggle>("JournalFilterBiting").enabledSelf, Is.True, "biting-now is available with world state");

                int shownForLake = 0;
                foreach (VisualElement card in Cards())
                {
                    if (card.resolvedStyle.display == DisplayStyle.Flex) shownForLake++;
                }
                Assert.That(shownForLake, Is.GreaterThan(0).And.LessThan(JournalSpecies), "the lake filter hides the harbour-only species");

                Press(_root.Q<Button>("JournalFilterAll"));
                yield return PlayModeHarness.WaitFrames(2, 0.1f);
                int shownAll = 0;
                foreach (VisualElement card in Cards())
                {
                    if (card.resolvedStyle.display == DisplayStyle.Flex) shownAll++;
                }
                Assert.That(shownAll, Is.EqualTo(JournalSpecies), "All spots shows every card");

                Assert.That(_ui.ModalOpen, Is.True, "the modal seam is held");
                _ui.HandleBack();
                yield return PlayModeHarness.WaitFrames(3, 0.2f);
                Assert.That(_ui.ModalOpen, Is.False);
                Assert.That(ModalShown, Is.False, "Back closes the journal");
                Assert.That(_ui.CurrentScreen, Is.EqualTo(UiScreen.Hud), "Back closed the modal, it did not open Pause");
                Assert.That(_input.MenuOpen, Is.False, "gameplay context restored");

                log.AssertNoWarnings("journal in game");
            }
            yield return PlayModeHarness.LeaveAndWaitForMenu();
        }

        // Catches: the journal key (J, Select or a rebind) not closing the journal (the input service reports it in every context), the
        // press that opened it closing it again the same frame, and a stale flag closing it every frame. Also the gameplay context: a context
        // the journal may not open in (swimming, climbing, a fight) closes it. A fake input service stands in for the keyboard.
        [UnityTest]
        public IEnumerator Journal_JournalKey_AndForeignContext_CloseIt()
        {
            yield return BootMenu();
            var fake = new FakeInputService();
            int closes = 0;
            var presenter = new JournalPresenter(_root, AppBootstrap.Current.Services, fake, () => closes++);
            try
            {
                presenter.Open(false);
                fake.JournalPressedAnyContext = true;
                presenter.Tick(0.016f);
                Assert.That(closes, Is.EqualTo(0), "the press of the opening frame is ignored");

                yield return null;
                fake.JournalPressedAnyContext = false;
                presenter.Tick(0.016f);
                Assert.That(closes, Is.EqualTo(0), "no press, no close");

                fake.JournalPressedAnyContext = true;
                presenter.Tick(0.016f);
                Assert.That(closes, Is.EqualTo(1), "the journal key closes it");
                fake.JournalPressedAnyContext = false;
                presenter.Hide();

                // In game: swimming closes it, the contexts the journal opens in do not.
                presenter.Open(true);
                yield return null;
                foreach (InputContext ok in new[] { InputContext.Free, InputContext.RodReady, InputContext.RodWaiting })
                {
                    fake.GameplayContext = ok;
                    presenter.Tick(0.016f);
                    Assert.That(closes, Is.EqualTo(1), ok + " keeps it open");
                }
                fake.GameplayContext = InputContext.Swimming;
                presenter.Tick(0.016f);
                Assert.That(closes, Is.EqualTo(2), "swimming closes it");
                presenter.Hide();
            }
            finally
            {
                presenter.Dispose();
                HideModalElement();
            }
        }

        private void HideModalElement() => Modal.style.display = DisplayStyle.None;

        // Catches: the journal staying open (and the owner stuck behind a modal) when the local bite lands, a journal that opens during the
        // bite or the fight, and MenuOpen left true afterwards. A long forced bite wait lets the journal open over a waiting bobber first.
        [UnityTest]
        public IEnumerator Journal_BiteClosesIt_AndItRefusesToOpenDuringTheFight()
        {
            FishingDebugSettings.ForcedBiteWait = 2.5f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            var app = AppBootstrap.Current;
            _ui = app.GetComponentInChildren<UiFlowController>(true);
            _root = _ui.GetComponent<UIDocument>().rootVisualElement;
            IInputService adapter = app.GetComponentInChildren<PlayerInputAdapter>(true);   // the UI's own input (the rod uses the fake)

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 5f);

            _ui.OpenJournal();
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(ModalShown, Is.True, "the journal opens over a waiting bobber");
            Assert.That(adapter.MenuOpen, Is.True);
            Assert.That(rig.Machine.State, Is.EqualTo(RodState.Waiting), "the bobber stays out");

            yield return PlayModeHarness.WaitUntil(
                () => rig.Machine.State == RodState.Bite || rig.Machine.State == RodState.Hooked, 8f, "the forced bite never came");
            yield return PlayModeHarness.WaitFrames(3, 0f);
            Assert.That(ModalShown, Is.False, "the bite closes the journal");
            Assert.That(_ui.ModalOpen, Is.False);
            Assert.That(adapter.MenuOpen, Is.False, "gameplay input is back for the fight");

            if (rig.Machine.State == RodState.Bite || rig.Machine.State == RodState.Hooked)
            {
                _ui.OpenJournal();
                yield return PlayModeHarness.WaitFrames(2, 0f);
                if (rig.Machine.State == RodState.Bite || rig.Machine.State == RodState.Hooked)
                {
                    Assert.That(ModalShown, Is.False, "it refuses to open while the local rod bites or fights");
                    Assert.That(adapter.MenuOpen, Is.False);
                }
            }
        }
    }
}
