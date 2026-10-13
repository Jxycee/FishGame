// Not networked itself. The host decides when gameplay scenes load (NetworkSceneManager); clients follow NGO scene sync.
// Every peer reacts to INetworkSession events. Error toasts come only from Ended, never from start-task exceptions (contract D8).
// M1: joins are NOT closed before the Spot load. The dock is the in-world lobby (spec 11.2) and SessionState.NetJoinsOpen stays true.
// Ended arrives after the Offline StateChanged (which already returned to Menu), so Ended only publishes a toast and never navigates.
// M3 dev map switch (M3-D31): the host loads a spot's scene through NetworkSceneManager (Single) on DevLoadSpotRequested, debug builds only;
// clients ignore the message and follow NGO scene sync. Session state is kept (run state in EcologySession, creels in SessionState);
// players respawn through PlayerSpawner's OnLoadEventCompleted / OnLoadComplete paths (the late-joiner path is unchanged).
using System;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.App
{
    public sealed class SceneFlow : IDisposable
    {
        private readonly INetworkSession session;
        private readonly NetworkManager networkManager;
        private readonly EventBus bus;
        private readonly IDisposable devLoadSubscription;
        private bool disposed;

        public SceneFlow(INetworkSession session, NetworkManager networkManager, EventBus bus)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            session.StateChanged += HandleStateChanged;
            session.Ended += HandleEnded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            devLoadSubscription = bus.Subscribe<DevLoadSpotRequested>(HandleDevLoadSpot);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            devLoadSubscription?.Dispose();
            session.StateChanged -= HandleStateChanged;
            session.Ended -= HandleEnded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        // NGO loads with plain LoadSceneAsync, so Unity can still activate a gameplay scene after the session went Offline
        // (cancel on the loading screen, transport stop mid-load, host leaving during scene sync). Guard on Offline only:
        // a client legitimately activates the dock while still Connecting.
        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (disposed) return;
            if (SceneNames.IsGameplay(next.name) && session.State == ConnState.Offline)
            {
                SceneManager.LoadScene(SceneNames.Menu, LoadSceneMode.Single);
            }
        }

        private void HandleStateChanged(ConnState previous, ConnState current)
        {
            if (disposed) return;

            if (current == ConnState.Connected)
            {
                if (networkManager.IsServer) LoadFirstGameplayScene();
                return;
            }

            if (current == ConnState.Offline && SceneManager.GetActiveScene().name != SceneNames.Menu)
            {
                SceneManager.LoadScene(SceneNames.Menu, LoadSceneMode.Single);
            }
        }

        private void LoadFirstGameplayScene()
        {
            var sceneManager = networkManager.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogError("SceneFlow: NetworkManager.SceneManager is null (scene management disabled?); leaving the session.");
                ObserveLeave(session.LeaveAsync());
                return;
            }

            SceneEventProgressStatus status = sceneManager.LoadScene(SceneNames.SpotLakeDock, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError("SceneFlow: LoadScene(" + SceneNames.SpotLakeDock + ") returned " + status + "; leaving the session.");
                ObserveLeave(session.LeaveAsync());
            }
        }

        private void HandleDevLoadSpot(DevLoadSpotRequested msg)
        {
            if (disposed || !Debug.isDebugBuild) return;
            if (!networkManager.IsServer || session.State != ConnState.Connected) return;
            ServiceRegistry app = ServiceRoots.App;
            ContentRegistry registry = null;
            if (app != null) app.TryGet(out registry);
            if (registry == null || msg.SpotId == null || !registry.TryGetSpot(msg.SpotId, out SpotDef spot) || !SceneNames.IsGameplay(spot.SceneName))
            {
                Debug.LogWarning("SceneFlow: dev map switch ignored: unknown spot '" + msg.SpotId + "'.");
                return;
            }
            NetworkSceneManager sceneManager = networkManager.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogWarning("SceneFlow: dev map switch ignored: scene management is off.");
                return;
            }
            SceneEventProgressStatus status = sceneManager.LoadScene(spot.SceneName, LoadSceneMode.Single);   // same-scene reload allowed (D-F5)
            if (status != SceneEventProgressStatus.Started)
                Debug.LogWarning("SceneFlow: dev map switch to " + spot.SceneName + " returned " + status + "; staying.");   // never leaves the session
        }

        private void HandleEnded(EndReason reason)
        {
            if (disposed) return;
            string key = EndReasonText.ToastKey(reason);
            if (key != null) bus.Publish(new ToastRequested(key));
        }

        private static async void ObserveLeave(Task leave)
        {
            try
            {
                await leave;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
