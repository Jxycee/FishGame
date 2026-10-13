// Not networked. Composition root on the root "App" object in Bootstrap; runs on every peer.
// Server only: spawns SessionState (destroyWithScene: false) from NetworkManager.OnServerStarted. This is the only place SessionState is created (contract D5).
// Uses GetComponent<NetworkManager>() only, never the NGO static accessor: this Awake runs at order -2000, before NetworkManager's own Awake/OnEnable.
using System;
using System.Collections;
using FishGame.Core;
using FishGame.Data;
using FishGame.Networking;
using FishGame.Runtime;
using FishGame.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FishGame.App
{
    [DefaultExecutionOrder(-2000)]
    public sealed class AppBootstrap : MonoBehaviour
    {
        private const ulong SfxSeed = 1UL; // private choice (not in the contract); fixed so generated SFX are reproducible

        // Runtime check (true in the editor and in Development Builds); replaces the deprecated DEVELOPMENT_BUILD define.
        private static bool IsDevBuild => Debug.isDebugBuild;

        [SerializeField] private NetworkObject sessionStatePrefab;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ContentRegistry contentRegistry;

        private ServiceRegistry services;
        private INetworkSession session;
        private SceneFlow sceneFlow;
        private UiFlowController ui;
        private NetworkObject spawnedSessionState;
        private bool composed;

        public static AppBootstrap Current { get; private set; }
        public static bool SuppressDevAutoStart { get; set; }
        // Test seam (PlayMode UiFlowCancelTests): null in production; read lazily when the first online attempt creates the backend.
        public static Func<INetworkSession> OnlineSessionFactoryOverride { get; set; }
        public ServiceRegistry Services => services;
        public NetworkManager NetworkManager { get; private set; }
        public ulong ContentHashValue { get; private set; }
        public DevFlags DevFlags { get; private set; }

        public void Configure(NetworkObject sessionStatePrefab, InputActionAsset inputActions, ContentRegistry contentRegistry)
        {
            this.sessionStatePrefab = sessionStatePrefab;
            this.inputActions = inputActions;
            this.contentRegistry = contentRegistry;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            OnlineSessionFactoryOverride = null;
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                // A second Bootstrap load. Deactivate first so this copy's NetworkManager, input and UI never wake up.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            try
            {
                Compose();
                composed = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError("AppBootstrap: composition failed; the app cannot start. Run FishGame > Build All and fix the error above.", this);
                enabled = false; // Start (and the Menu load) will not run
            }
        }

        private void Compose()
        {
            if (sessionStatePrefab == null || inputActions == null || contentRegistry == null)
            {
                throw new InvalidOperationException("AppBootstrap is not configured (sessionStatePrefab, inputActions or contentRegistry is missing). SceneBuilder must call Configure(...) before saving Bootstrap.");
            }
            if (transform.parent != null)
            {
                throw new InvalidOperationException("AppBootstrap: the App object must be a root object (DontDestroyOnLoad and NGO both require it).");
            }
            var networkManager = Require<NetworkManager>();
            var input = Require<PlayerInputAdapter>();
            var audio = Require<AudioBus>();
            ui = GetComponentInChildren<UiFlowController>(true);
            if (ui == null) throw new InvalidOperationException("AppBootstrap: no UiFlowController under App/UI.");

            DontDestroyOnLoad(gameObject);
            Current = this;
            Application.runInBackground = true; // spec 6.2: MPPM clones and alt-tabbed hosts must keep ticking

            NetworkManager = networkManager;
            var config = NetworkManager.NetworkConfig;
            if (config.NetworkTransport == null) config.NetworkTransport = GetComponent<NetworkTransport>();
            if (config.NetworkTransport == null) throw new InvalidOperationException("AppBootstrap: no NetworkTransport (UnityTransport) on the App object.");
            config.TickRate = NetworkConstants.TickRate;
            config.ConnectionApproval = true;
            config.EnableSceneManagement = true;
            // NGO's own protocol version stays at its constant (0). The dev protocol offset is applied only to the
            // payload protocol version passed to NetworkSessionConfig below (T8 reads no command-line arguments).
            config.ProtocolVersion = NetworkConstants.NgoProtocolVersion;
            config.PlayerPrefab = null;

            // Subscribe before any StartHost can happen (LocalNetworkSession, or the host the UGS SDK starts).
            NetworkManager.OnServerStarted += HandleServerStarted;

            DevFlags = DevFlags.Parse(DevStartup.CollectArgs());

            services = new ServiceRegistry();
            var bus = new EventBus();
            services.Register(bus);

            var settings = SettingsStore.CreateForThisInstance();
            settings.EnsureLocalPlayerId();
            services.Register<ISettings>(settings);

            input.Initialize(inputActions, settings);
            services.Register<IInputService>(input);
            services.Register<IInputBindings>(input);

            // SFX clips are generated before any scene loads: AmbienceBed (Spot_LakeDock) looks up its wind clip once in Start.
            var sfx = new SfxGenerator();
            sfx.GenerateAll(SfxSeed);
            services.Register(sfx);
            audio.Initialize(sfx, settings);
            services.Register<AudioBus>(audio); // concrete type (contract A7)

            contentRegistry.Build();
            ContentHashValue = contentRegistry.ComputeContentHash();
            audio.ApplyCueTable(contentRegistry.SfxCueTable != null ? contentRegistry.SfxCueTable.ToRecipes() : null);
            services.Register(contentRegistry);

            var bridge = new JoinApprovalBridge();
            services.Register(bridge);
            services.Register(this.DevFlags);

            int protocolVersion = BuildInfo.EffectiveProtocolVersion(IsDevBuild ? this.DevFlags.ProtocolOffset : 0);
            bool longTimeout = IsDevBuild && this.DevFlags.LongTimeout;
            var sessionConfig = new NetworkSessionConfig(NetworkManager, bridge, settings, protocolVersion, ContentHashValue, longTimeout);
            var local = new LocalNetworkSession(sessionConfig);
            services.Register(local);
            session = new CompositeNetworkSession(local,
                () => OnlineSessionFactoryOverride != null ? OnlineSessionFactoryOverride() : new UgsNetworkSession(sessionConfig));
            services.Register<INetworkSession>(session);

            ServiceRoots.SetApp(services);
            Application.quitting += HandleQuitting;
            sceneFlow = new SceneFlow(session, NetworkManager, bus);
        }

        // UiFlowController.Initialize runs here, not in Awake: by Start every OnEnable has run, so the child UIDocument
        // has built its rootVisualElement. All services it Get<T>s (INetworkSession, ISettings, IInputService,
        // IInputBindings, EventBus, AudioBus) are registered in Awake. It runs before the Menu load so its
        // activeSceneChanged hook sees Menu.
        private IEnumerator Start()
        {
            if (!composed) yield break;

            ui.Initialize(services);
            SceneManager.LoadScene(SceneNames.Menu, LoadSceneMode.Single);

            // Wait until the plain Menu load has finished, then one more frame (UI shows MainMenu first), so a dev
            // auto-start can never race the pending Menu load with an NGO scene load.
            while (SceneManager.GetActiveScene().name != SceneNames.Menu) yield return null;
            yield return null;

            if (!SuppressDevAutoStart) DevStartup.TryAutoStart(session, this.DevFlags);
        }

        // Raised synchronously inside StartHost after host self-approval; SpawnManager is ready. The spawn is
        // synchronous on purpose (no coroutine/await). If this throws, NGO catches it, shuts down and StartHost
        // returns false, so keep it exception-free and log instead.
        // The IsSpawned guard skips a second spawn while the previous instance is live; a destroyed one compares null.
        private void HandleServerStarted()
        {
            if (spawnedSessionState != null && spawnedSessionState.IsSpawned) return;
            spawnedSessionState = NetworkManager.SpawnManager.InstantiateAndSpawn(
                networkPrefab: sessionStatePrefab,
                destroyWithScene: false);
            if (spawnedSessionState == null)
            {
                Debug.LogError("AppBootstrap: SessionState spawn failed (is SessionState.prefab in FishGameNetworkPrefabs?).", this);
            }
        }

        private void HandleQuitting()
        {
            if (session == null) return;
            try
            {
                _ = session.LeaveAsync(); // best effort (spec 6.3)
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private T Require<T>() where T : Component
        {
            var component = GetComponent<T>();
            if (component == null) throw new InvalidOperationException("AppBootstrap: the App object has no " + typeof(T).Name + " component.");
            return component;
        }

        private void OnDestroy()
        {
            if (Current != this) return;

            Application.quitting -= HandleQuitting;
            if (NetworkManager != null) NetworkManager.OnServerStarted -= HandleServerStarted;

            sceneFlow?.Dispose();
            sceneFlow = null;
            if (services != null)
            {
                if (ServiceRoots.App == services) ServiceRoots.SetApp(null);
                services.Dispose();
                services = null;
            }
            session = null;
            Current = null;
        }

        public void DestroyForTests()
        {
            if (NetworkManager != null && !NetworkManager.ShutdownInProgress &&
                (NetworkManager.IsListening || NetworkManager.IsServer || NetworkManager.IsClient))
            {
                NetworkManager.Shutdown();
            }
            Destroy(gameObject);
        }
    }
}
