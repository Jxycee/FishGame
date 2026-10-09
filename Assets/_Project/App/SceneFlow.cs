// Not networked itself. The host decides when gameplay scenes load (NetworkSceneManager); clients follow NGO scene sync.
// Every peer reacts to INetworkSession events. Error toasts come only from Ended, never from start-task exceptions (contract D8).
// M1: joins are NOT closed before the Spot load. The dock is the in-world lobby (spec 11.2) and SessionState.NetJoinsOpen stays true.
// Ended arrives after the Offline StateChanged (which already returned to Menu), so Ended only publishes a toast and never navigates.
using System;
using System.Threading.Tasks;
using FishGame.Core;
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
        private bool disposed;

        public SceneFlow(INetworkSession session, NetworkManager networkManager, EventBus bus)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            session.StateChanged += HandleStateChanged;
            session.Ended += HandleEnded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
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
