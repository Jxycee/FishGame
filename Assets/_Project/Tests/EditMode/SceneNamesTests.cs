using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class SceneNamesTests
    {
        [Test]
        public void SceneNames_BuildOrder_BootstrapFirst()
        {
            Assert.That(SceneNames.BuildOrder, Is.EqualTo(new[] { "Bootstrap", "Menu", "Spot_LakeDock" }));
        }

        // Catches: the constant drifting from the scene file / the harbour spot asset's scene name (the ecology builder no longer holds a literal).
        [Test]
        public void SceneNames_SpotHarbourPier_IsTheHarbourSceneName()
        {
            Assert.That(SceneNames.SpotHarbourPier, Is.EqualTo("Spot_HarbourPier"));
            Assert.That(SceneNames.IsGameplay(SceneNames.SpotHarbourPier), Is.True);
        }

        [Test]
        public void SceneNames_IsGameplay_OnlySpotScenes()
        {
            Assert.That(SceneNames.IsGameplay(SceneNames.SpotLakeDock), Is.True);
            Assert.That(SceneNames.IsGameplay("Spot_HarbourPier"), Is.True);
            Assert.That(SceneNames.IsGameplay(SceneNames.Bootstrap), Is.False);
            Assert.That(SceneNames.IsGameplay(SceneNames.Menu), Is.False);
            Assert.That(SceneNames.IsGameplay("spot_lakedock"), Is.False);
            Assert.That(SceneNames.IsGameplay(""), Is.False);
            Assert.That(SceneNames.IsGameplay(null), Is.False);
        }
    }
}
