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
