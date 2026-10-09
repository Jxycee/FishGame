// Authority: test-only (no networked state). Bootstrap composes the App services and lands on Menu with the menu open.
using System.Collections;
using FishGame.App;
using FishGame.Core;
using FishGame.Networking;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class BootTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest]
        public IEnumerator Bootstrap_Loads_Menu()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Menu), "Active scene must be Menu");

            var app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "AppBootstrap.Current missing");
            Assert.That(app.NetworkManager, Is.Not.Null, "AppBootstrap.NetworkManager missing");
            Assert.That(ServiceRoots.App, Is.Not.Null, "ServiceRoots.App not set");
            Assert.That(ServiceRoots.App, Is.SameAs(app.Services), "ServiceRoots.App must be the AppBootstrap registry");

            Assert.That(app.Services.Contains<IInputService>(), Is.True, "IInputService not registered");
            Assert.That(app.Services.Contains<ISettings>(), Is.True, "ISettings not registered");
            Assert.That(app.Services.Contains<EventBus>(), Is.True, "EventBus not registered");
            Assert.That(app.Services.Contains<INetworkSession>(), Is.True, "INetworkSession not registered");
            Assert.That(app.Services.Contains<LocalNetworkSession>(), Is.True, "LocalNetworkSession not registered");

            Assert.That(app.Services.Get<INetworkSession>().State, Is.EqualTo(ConnState.Offline), "Session must be Offline in Menu");
            Assert.That(app.NetworkManager.IsListening, Is.False, "NGO must not be running in Menu (dev auto-start must be suppressed)");
            Assert.That(ServiceRoots.Game, Is.Null, "GameScope services must not exist in Menu");
            Assert.That(app.Services.Get<IInputService>().MenuOpen, Is.True, "The main menu must set IInputService.MenuOpen");
        }
    }
}
