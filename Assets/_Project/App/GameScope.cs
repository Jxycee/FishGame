// Not networked; runs locally on every peer. Session-scope services for one gameplay scene.
// Lives on the root object "GameScope" in each Spot_* scene. Registers IPauseState and publishes itself as ServiceRoots.Game.
// Execution order -1500 so it is awake before any other scene object (PlayerSpawner, SceneCameraSwitch, players) looks it up.
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.App
{
    [DefaultExecutionOrder(-1500)]
    public sealed class GameScope : MonoBehaviour
    {
        public ServiceRegistry Services { get; private set; }

        private void Awake()
        {
            Services = new ServiceRegistry();
            Services.Register<IPauseState>(new PauseState());
            ServiceRoots.SetGame(Services);
        }

        private void OnDestroy()
        {
            if (Services == null) return;
            var mine = Services;
            Services = null;
            if (ServiceRoots.Game == mine) ServiceRoots.SetGame(null);
            mine.Dispose();
        }
    }
}
