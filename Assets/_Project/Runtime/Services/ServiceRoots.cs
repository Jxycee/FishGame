using UnityEngine;

namespace FishGame.Runtime
{
    // The only static service access point. App is set by AppBootstrap; Game is set by GameScope in gameplay scenes.
    // Both are null in EditMode tests. Reset on SubsystemRegistration so disabled domain reload cannot leak a stale registry.
    public static class ServiceRoots
    {
        private static ServiceRegistry _app;
        private static ServiceRegistry _game;

        public static ServiceRegistry App => _app;
        public static ServiceRegistry Game => _game;

        public static void SetApp(ServiceRegistry registry) { _app = registry; }
        public static void SetGame(ServiceRegistry registry) { _game = registry; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _app = null;
            _game = null;
        }
    }
}
