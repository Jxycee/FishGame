// Authority: test-only (client-side UI; the in-game test drives the solo host's own DevWorldDriver through the panel). T22: the F3 mode cycle
// Off -> Overlay -> Panel -> Off through a fake input service, the panel and the journal excluding each other through the HUD-modal seam, Esc and
// Close leaving the panel, the host rows driving the world driver, the F8 scrub (press, release, Menu release), the Load map row (one button
// per registry spot, the current one disabled), the HUD clock label changing only on a 10-minute step, and the seed parser.
// The client read-only mode needs a second NetworkManager (a host + client harness) that does not exist yet: it stays a manual MPPM check
// (T22.6 step 7.10). The real key presses (F3, F8, Esc) are manual checks too: the input adapter owns the keyboard.
// Test panels bind their own presenter instances to the shared UXML elements next to the flow's own (which stay Off), so a button press reaches
// both: assertions on counters are therefore relative.
using System.Collections;
using FishGame.App;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using FishGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FishGame.Tests.PlayMode
{
    public class DevPanelUiTests
    {
        private UiFlowController _ui;
        private VisualElement _root;
        private FakeInputService _fake;
        private ServiceRegistry _services;
        private DevOverlayPresenter _overlay;
        private DevPanelPresenter _panel;
        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_panel != null) _panel.Unbind();
            if (_overlay != null) _overlay.Unbind();
            _panel = null;
            _overlay = null;
            _services = null;
            _fake = null;
            _ui = null;
            _root = null;
            EcologySession.Clear();           // the seed test replaces the session's run state
            FishingRig rig = _rig;
            _rig = null;
            if (rig != null) yield return FishingPlayModeDriver.TearDown(rig);
            else yield return PlayModeHarness.TearDownApp();
        }

        private IEnumerator BootMenu()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            Resolve();
        }

        private IEnumerator BootSolo()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            Resolve();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current != null && WorldState.Current.HasState, 10f, "WorldState never had state");
            yield return PlayModeHarness.WaitUntil(() => _ui.CurrentScreen == UiScreen.Hud, 5f, "the HUD never showed");
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
        }

        private void Resolve()
        {
            var app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "App");
            _ui = app.GetComponentInChildren<UiFlowController>(true);
            Assert.That(_ui, Is.Not.Null, "UiFlowController");
            _root = _ui.GetComponent<UIDocument>().rootVisualElement;
        }

        // A fresh overlay + panel pair on the real UXML, reading the keys from a fake input service (never the keyboard).
        private void BindPair()
        {
            ServiceRegistry app = AppBootstrap.Current.Services;
            _fake = new FakeInputService();
            _services = new ServiceRegistry();
            _services.Register<IInputService>(_fake);
            _services.Register(app.Get<EventBus>());
            _services.Register(app.Get<AudioBus>());
            if (app.TryGet(out ContentRegistry registry)) _services.Register(registry);
            _overlay = new DevOverlayPresenter();
            _panel = new DevPanelPresenter(_ui, _overlay);
            _overlay.Bind(_root, _services);
            _panel.Bind(_root, _services);
        }

        private void PressF3()
        {
            _fake.DevPressed.Add(DevKey.ToggleOverlay);
            _overlay.Tick(0.016f);
            _fake.DevPressed.Remove(DevKey.ToggleOverlay);
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

        // Catches: F3 skipping a step or wrapping early, the panel not holding the HUD-modal seam (cursor free, gameplay off), Esc/B (HandleBack)
        // or the Close button leaving the mode at Panel with the modal gone, and a screen change not closing the panel.
        [UnityTest]
        public IEnumerator DevHud_F3_CyclesOffOverlayPanelOff_AndEveryExitLeavesCleanly()
        {
            yield return BootMenu();
            BindPair();
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "starts Off");
            Assert.That(_root.Q<Label>("DevOverlay").ClassListContains("hidden"), Is.True);

            PressF3();
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Overlay), "F3 once: overlay");
            Assert.That(_root.Q<Label>("DevOverlay").ClassListContains("hidden"), Is.False, "the overlay label shows");
            Assert.That(_ui.ModalOpen, Is.False, "the overlay is not a modal");
            Assert.That(_panel.PanelShown, Is.False);

            PressF3();
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Panel), "F3 twice: panel");
            Assert.That(_panel.PanelShown, Is.True, "the panel shows");
            Assert.That(_ui.ModalOpen, Is.True, "the panel holds the modal seam");
            Assert.That(_root.Q<Label>("DevOverlay").ClassListContains("hidden"), Is.False, "the overlay label stays up beside the panel");

            PressF3();
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "F3 three times: everything off");
            Assert.That(_panel.PanelShown, Is.False);
            Assert.That(_ui.ModalOpen, Is.False, "the seam is released");
            Assert.That(_root.Q<Label>("DevOverlay").ClassListContains("hidden"), Is.True);

            // Esc / gamepad B: HandleBack closes the modal and nothing else.
            _overlay.SetMode(DevHudMode.Panel);
            Assert.That(_ui.ModalOpen, Is.True);
            UiScreen screen = _ui.CurrentScreen;
            _ui.HandleBack();
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "Back sets the mode Off");
            Assert.That(_ui.ModalOpen, Is.False);
            Assert.That(_panel.PanelShown, Is.False);
            Assert.That(_ui.CurrentScreen, Is.EqualTo(screen), "Back closed the panel, it changed no screen");

            // The Close button.
            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Press(_root.Q<Button>("DevClose"));
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "Close sets the mode Off");
            Assert.That(_ui.ModalOpen, Is.False);

            // A screen change closes it (Show closes the modal first).
            _overlay.SetMode(DevHudMode.Panel);
            Assert.That(_ui.ModalOpen, Is.True);
            _ui.Show(UiScreen.Settings);
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "a screen change sets the mode Off");
            Assert.That(_ui.ModalOpen, Is.False);
            Assert.That(_panel.PanelShown, Is.False);
        }

        // Catches: the panel opening over the journal (two modals, the cursor and gameplay input owned twice), the journal opening over the panel,
        // and the refused panel leaving the HUD mode at Panel with nothing shown.
        [UnityTest]
        public IEnumerator DevPanel_And_Journal_RefuseEachOther()
        {
            yield return BootMenu();
            BindPair();
            using var log = new PlayModeHarness.LogCapture();

            _ui.OpenJournal();
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            VisualElement journal = _root.Q<VisualElement>("JournalModal");
            Assert.That(journal.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the journal is open");

            _overlay.SetMode(DevHudMode.Panel);
            Assert.That(_panel.PanelShown, Is.False, "the panel refuses to open over the journal");
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Overlay), "the refused step falls back to the overlay");
            Assert.That(journal.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the journal is untouched");

            _ui.HandleBack();                                   // closes the journal
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(journal.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            _overlay.SetMode(DevHudMode.Panel);
            Assert.That(_panel.PanelShown, Is.True, "the panel opens once the journal is closed");
            _ui.OpenJournal();
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            Assert.That(journal.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the journal refuses to open over the panel");
            Assert.That(_panel.PanelShown, Is.True, "the panel is untouched");
            Assert.That(_ui.ModalOpen, Is.True);

            log.AssertNoWarnings("dev panel and journal exclusion");
        }

        // Catches: a host row calling the wrong driver method, the F8 scrub not starting on the press, not stopping on
        // the release or on a Menu context (alt-tab reads the same), the minute slider writing before its debounce, the Load map row not listing
        // both spots or not disabling the current one, and a header that does not say host.
        [UnityTest]
        public IEnumerator DevPanel_Host_RowsDriveTheDriver_ScrubFollowsF8_MapRowListsBothSpots()
        {
            yield return BootSolo();
            BindPair();
            using var log = new PlayModeHarness.LogCapture();
            WorldState world = WorldState.Current;
            DevWorldDriver driver = world.GetComponent<DevWorldDriver>();
            Assert.That(driver, Is.Not.Null, "the World root has a DevWorldDriver");

            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            Assert.That(_panel.PanelShown, Is.True);
            Assert.That(_ui.ModalOpen, Is.True, "the panel holds the seam in game");
            Assert.That(AppBootstrap.Current.Services.Get<IInputService>().MenuOpen, Is.True, "Menu context: cursor free, player frozen");
            _panel.Tick(0.3f);                                  // a refresh
            Assert.That(_root.Q<Label>("DevPanelTitle").text, Is.EqualTo("Dev panel (host)"));

            // Load map: one button per spot, MapOrder order, the current spot's disabled.
            Button lake = _root.Q<Button>("DevMap_lake_dock");
            Button harbour = _root.Q<Button>("DevMap_harbour_pier");
            Assert.That(lake, Is.Not.Null, "lake_dock button");
            Assert.That(harbour, Is.Not.Null, "harbour_pier button");
            Assert.That(world.SpotId, Is.EqualTo("lake_dock"));
            Assert.That(lake.enabledSelf, Is.False, "the current spot's button is disabled");
            Assert.That(harbour.enabledSelf, Is.True, "the other spot can be loaded");

            // Season / weather / new day / clock toggle.
            Press(_root.Q<Button>("DevSeason3"));
            Assert.That(world.Season, Is.EqualTo(Season.Winter), "Winter button");
            Press(_root.Q<Button>("DevWeather2"));
            Assert.That(driver.WeatherOverride, Is.EqualTo(WeatherMask.Rain), "Rain button");
            Press(_root.Q<Button>("DevWeather0"));
            Assert.That(driver.WeatherOverride, Is.EqualTo(WeatherMask.None), "Track button");
            int day = driver.Run.Day;
            Press(_root.Q<Button>("DevNewDay"));
            Assert.That(driver.Run.Day, Is.GreaterThan(day), "New day");
            Toggle clock = _root.Q<Toggle>("DevClockToggle");
            clock.value = false;
            Assert.That(driver.ClockRunning, Is.False, "the clock toggle stops the dev clock");
            clock.value = true;
            Assert.That(driver.ClockRunning, Is.True, "and restarts it");

            // The minute slider applies once, 0.2 s after the last change.
            driver.DevSetMinute(100);
            SliderInt slider = _root.Q<SliderInt>("DevMinute");
            slider.value = 700;
            Assert.That(world.Minute, Is.LessThan(200), "the slider has not written yet");
            _panel.Tick(0.05f);
            Assert.That(world.Minute, Is.LessThan(200), "still inside the debounce");
            _panel.Tick(0.3f);
            Assert.That(world.Minute, Is.InRange(700, 760), "the minute is applied after the debounce");

            // F8: scrub on the press, off on the release, off when a menu context opens.
            float baseRate = DayClock.DefaultMinutesPerSecond;
            Assert.That(world.NetClock.Value.MinutesPerSecond, Is.EqualTo(baseRate).Within(0.001f), "normal rate before the scrub");
            _fake.DevHeld.Add(DevKey.ScrubClock);
            _panel.Tick(0.016f);
            Assert.That(world.NetClock.Value.MinutesPerSecond, Is.EqualTo(baseRate * 60f).Within(0.01f), "F8 held: x60");
            _fake.DevHeld.Remove(DevKey.ScrubClock);
            _panel.Tick(0.016f);
            Assert.That(world.NetClock.Value.MinutesPerSecond, Is.EqualTo(baseRate).Within(0.001f), "F8 released: normal");
            _fake.DevHeld.Add(DevKey.ScrubClock);
            _panel.Tick(0.016f);
            Assert.That(world.NetClock.Value.MinutesPerSecond, Is.EqualTo(baseRate * 60f).Within(0.01f), "held again");
            _fake.MenuOpen = true;                              // a menu opening (or the window losing focus) reads as released
            _panel.Tick(0.016f);
            Assert.That(world.NetClock.Value.MinutesPerSecond, Is.EqualTo(baseRate).Within(0.001f), "a Menu context releases the scrub");
            _fake.MenuOpen = false;
            _fake.DevHeld.Clear();

            // The overlay WORLD line carries the world.
            _overlay.Tick(0.2f);
            string overlayText = _root.Q<Label>("DevOverlay").text;
            Assert.That(overlayText, Does.Contain("WORLD spot=lake_dock"), "the WORLD line");
            Assert.That(overlayText, Does.Contain("season=Winter"));
            Assert.That(overlayText, Does.Contain("scrub=off"));

            Press(_root.Q<Button>("DevClose"));
            Assert.That(_ui.ModalOpen, Is.False);
            log.AssertNoWarnings("dev panel host");
            yield return PlayModeHarness.LeaveAndWaitForMenu();
        }

        // Catches: the "Day over" button missing (ME8 could not be run: the slider stops at 1190 and a running clock wraps at 1200), not stopping
        // the clock, not landing on minute 1200, or casting staying open on the WORLD line.
        [UnityTest]
        public IEnumerator DevPanel_DayOverButton_StopsTheClockAtTwoAm_AndTheWorldLineSaysClosed()
        {
            yield return BootSolo();
            BindPair();
            using var log = new PlayModeHarness.LogCapture();
            WorldState world = WorldState.Current;
            DevWorldDriver driver = world.GetComponent<DevWorldDriver>();
            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            _panel.Tick(0.3f);

            Button dayOver = _root.Q<Button>("DevDayOver");
            Assert.That(dayOver, Is.Not.Null, "the Clock row has a Day over button");
            Assert.That(dayOver.text, Is.EqualTo("Day over (stop at 2:00 AM)"));
            driver.DevSetClockRunning(true);
            driver.DevSetMinute(600);
            Assert.That(world.CastingOpen, Is.True, "casting is open at 600");

            Press(dayOver);
            Assert.That(driver.ClockRunning, Is.False, "Day over stops the clock");
            yield return PlayModeHarness.WaitUntil(() => !WorldState.Current.CastingOpen, 3f, "Day over never closed casting");
            Assert.That(world.MinuteF, Is.EqualTo(1200f).Within(0.01f), "and parks it at minute 1200");
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            Assert.That(world.MinuteF, Is.EqualTo(1200f).Within(0.01f), "the stopped clock stays at 1200 (no new day)");
            _overlay.Tick(0.2f);
            Assert.That(_root.Q<Label>("DevOverlay").text, Does.Contain("open=no"), "the WORLD line shows casting closed");

            Press(_root.Q<Button>("DevNewDay"));
            yield return PlayModeHarness.WaitUntil(() => WorldState.Current.CastingOpen, 3f, "New day never reopened casting");

            Press(_root.Q<Button>("DevClose"));
            log.AssertNoWarnings("dev panel day over");
            yield return PlayModeHarness.LeaveAndWaitForMenu();
        }

        // Catches: the panel staying open (the owner stuck behind a modal, the fight unreachable) when the local bite lands, like the journal.
        [UnityTest]
        public IEnumerator DevPanel_ClosesWhenTheLocalBiteLands()
        {
            FishingDebugSettings.ForcedBiteWait = 2.5f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;
            Resolve();
            BindPair();
            IInputService adapter = AppBootstrap.Current.GetComponentInChildren<PlayerInputAdapter>(true);   // the UI's own input (the rod uses the rig's fake)

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Waiting, 5f);

            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(_panel.PanelShown, Is.True, "the panel opens over a waiting bobber");
            Assert.That(adapter.MenuOpen, Is.True);
            _panel.Tick(0.016f);
            Assert.That(_panel.PanelShown, Is.True, "no bite yet: the tick leaves it open");

            yield return PlayModeHarness.WaitUntil(
                () => rig.Machine.State == RodState.Bite || rig.Machine.State == RodState.Hooked, 8f, "the forced bite never came");
            _panel.Tick(0.016f);                                // the same frame: the rod is still biting
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "the bite sets the HUD mode Off");
            Assert.That(_panel.PanelShown, Is.False, "the bite closes the panel");
            Assert.That(_ui.ModalOpen, Is.False, "and releases the modal seam");
            Assert.That(adapter.MenuOpen, Is.False, "gameplay input is back for the fight");
        }

        // Catches: a label rewritten inside a 10-minute step (a sentinel survives it), a wrong step floor, a season or weather name that does not
        // follow the replicated world, and a glyph that stays on the old value.
        [UnityTest]
        public IEnumerator ClockHud_LabelChangesOnlyOnATenMinuteStep_AndFollowsSeasonAndWeather()
        {
            yield return BootSolo();
            using var log = new PlayModeHarness.LogCapture();
            WorldState world = WorldState.Current;
            DevWorldDriver driver = world.GetComponent<DevWorldDriver>();
            Label time = _root.Q<Label>("ClockTime");
            Assert.That(_root.Q<VisualElement>("ClockHud").ClassListContains("hidden"), Is.False, "the clock shows in game");

            driver.DevSetClockRunning(false);
            driver.DevSetMinute(100);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(time.text, Is.EqualTo("7:40 AM"), "minute 100 reads 7:40 AM");

            // A sentinel proves no write happens inside a step: only a step change may overwrite the label.
            time.text = "sentinel";
            driver.DevSetMinute(105);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(time.text, Is.EqualTo("sentinel"), "minute 105 is still the 7:40 step, so the label is not rewritten");

            driver.DevSetMinute(110);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(time.text, Is.EqualTo("7:50 AM"), "minute 110 steps to 7:50 AM");

            driver.DevSetSeason(Season.Winter);
            driver.DevSetWeatherOverride(WeatherMask.Rain);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(_root.Q<Label>("ClockSeasonName").text, Is.EqualTo("Winter"));
            Assert.That(_root.Q<GlyphElement>("ClockSeason").Glyph, Is.EqualTo(Glyph.Winter));
            Assert.That(_root.Q<Label>("ClockWeatherName").text, Is.EqualTo("Rain"));
            Assert.That(_root.Q<GlyphElement>("ClockWeather").Glyph, Is.EqualTo(Glyph.Rain));

            driver.DevSetSeason(Season.Summer);
            driver.DevSetWeatherOverride(WeatherMask.Fog);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);
            Assert.That(_root.Q<Label>("ClockSeasonName").text, Is.EqualTo("Summer"));
            Assert.That(_root.Q<GlyphElement>("ClockSeason").Glyph, Is.EqualTo(Glyph.Summer));
            Assert.That(_root.Q<Label>("ClockWeatherName").text, Is.EqualTo("Fog"));
            Assert.That(_root.Q<GlyphElement>("ClockWeather").Glyph, Is.EqualTo(Glyph.Fog));

            log.AssertNoWarnings("clock hud");
            yield return PlayModeHarness.LeaveAndWaitForMenu();
        }

        // Catches: the 4 Hz refresh writing the old seed back into the field after it lost focus (Tab or a click on Apply), so Apply would read
        // the OLD seed. Types a seed, moves focus to Apply, lets more than one refresh pass, then applies: the new seed must be the run's.
        [UnityTest]
        public IEnumerator DevPanel_TypedSeed_SurvivesFocusLoss_AndApplies()
        {
            yield return BootSolo();
            BindPair();
            using var log = new PlayModeHarness.LogCapture();
            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            _panel.Tick(0.3f);

            TextField field = _root.Q<TextField>("DevSeedField");
            Assert.That(field.value, Does.StartWith("0x"), "the field shows the run's seed first");
            ulong before = EcologySession.Current.RunSeed;
            field.Focus();
            field.value = "12345";
            _root.Q<Button>("DevSeedApply").Focus();            // the typed field loses focus, like Tab or a click on Apply
            yield return PlayModeHarness.WaitFrames(2, 0.1f);
            _panel.Tick(0.3f);
            _panel.Tick(0.3f);
            yield return PlayModeHarness.WaitFrames(2, 0.6f);
            _panel.Tick(0.3f);
            Assert.That(field.value, Is.EqualTo("12345"), "a refresh must not overwrite the typed seed");

            Press(_root.Q<Button>("DevSeedApply"));
            Assert.That(EcologySession.Current.RunSeed, Is.EqualTo(12345UL), "Apply used the typed seed");
            Assert.That(EcologySession.Current.RunSeed, Is.Not.EqualTo(before));
            _panel.Tick(0.3f);
            Assert.That(field.value, Is.EqualTo("0x0000000000003039"), "after Apply the field shows the applied seed");

            _overlay.SetMode(DevHudMode.Off);
            log.AssertNoWarnings("typed seed");
            yield return PlayModeHarness.LeaveAndWaitForMenu();
        }

        // Catches: F3 not closing the panel when nothing is focused (key events then target the panel's visualTree, above the document root),
        // and F3 closing it before the F3 that opened it was released.
        [UnityTest]
        public IEnumerator DevPanel_F3_ClosesIt_WhenNothingIsFocused_AfterTheOpeningKeyWasReleased()
        {
            yield return BootMenu();
            BindPair();
            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            VisualElement tree = _root.panel.visualTree;
            _root.panel.focusController.focusedElement?.Blur();
            Assert.That(_root.panel.focusController.focusedElement, Is.Null, "nothing focused");

            SendKey(tree, KeyCode.F3, down: true);
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Panel), "an F3 press before the opening F3 was released does nothing");
            SendKey(tree, KeyCode.F3, down: false);
            SendKey(tree, KeyCode.F3, down: true);
            Assert.That(_overlay.Mode, Is.EqualTo(DevHudMode.Off), "F3 closes the panel with nothing focused");
            Assert.That(_ui.ModalOpen, Is.False);
        }

        private static void SendKey(VisualElement target, KeyCode key, bool down)
        {
            using (EventBase e = down ? (EventBase)KeyDownEvent.GetPooled('\0', key, EventModifiers.None) : KeyUpEvent.GetPooled('\0', key, EventModifiers.None))
            {
                e.target = target;
                target.SendEvent(e);
            }
        }

        // Reproduction check (review item 4): a focused seed field inside a hidden panel. Reports through the assertion message whether focus
        // stays inside the hidden panel; the panel must not keep keyboard focus once it is closed.
        [UnityTest]
        public IEnumerator DevPanel_Closing_LeavesNoFocusInsideTheHiddenPanel()
        {
            yield return BootSolo();
            BindPair();
            _overlay.SetMode(DevHudMode.Panel);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            TextField field = _root.Q<TextField>("DevSeedField");
            field.Focus();
            var focus = _root.panel.focusController;
            Assert.That(focus.focusedElement, Is.Not.Null, "the seed field took focus");
            var focusedNow = (VisualElement)focus.focusedElement;
            Assert.That(focusedNow == field || field.Contains(focusedNow), Is.True, "the focus is on the seed field: " + focusedNow.name);

            _overlay.SetMode(DevHudMode.Off);
            yield return PlayModeHarness.WaitFrames(3, 0.2f);
            var now = focus.focusedElement as VisualElement;
            VisualElement panelElement = _root.Q<VisualElement>("DevPanel");
            bool inside = now != null && (now == panelElement || panelElement.Contains(now));
            Assert.That(inside, Is.False, "focus stayed inside the hidden dev panel: " + (now != null ? now.name + "/" + now.GetType().Name : "null"));
            yield return PlayModeHarness.LeaveAndWaitForMenu();
        }

        // Catches: a seed parser that accepts garbage or rejects the hex form the overlay prints ("0x" + 16 digits).
        [Test]
        public void DevPanel_SeedParser_AcceptsDecimalAndHex_RejectsTheRest()
        {
            Assert.That(DevPanelPresenter.TryParseSeed("12345", out ulong a), Is.True);
            Assert.That(a, Is.EqualTo(12345UL));
            Assert.That(DevPanelPresenter.TryParseSeed("  0xDEADBEEF ", out ulong b), Is.True);
            Assert.That(b, Is.EqualTo(0xDEADBEEFUL));
            Assert.That(DevPanelPresenter.TryParseSeed("0x00000000000000FF", out ulong c), Is.True);
            Assert.That(c, Is.EqualTo(255UL));
            Assert.That(DevPanelPresenter.TryParseSeed("18446744073709551615", out ulong d), Is.True);
            Assert.That(d, Is.EqualTo(ulong.MaxValue));
            foreach (string bad in new[] { null, "", "   ", "abc", "0x", "0xZZ", "-1", "1.5", "18446744073709551616", "0x10000000000000000" })
            {
                Assert.That(DevPanelPresenter.TryParseSeed(bad, out _), Is.False, "'" + bad + "' must be rejected");
            }
        }
    }
}
