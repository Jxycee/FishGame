// Bootstrap and Menu scenes and the build list (contract 6.5; spec 4.6 steps 5 and 6). Owned by T15.
// Each scene starts from NewScene(EmptyScene, Single) and is saved to the same path, so its GUID stays stable.
using FishGame.App;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using FishGame.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FishGame.EditorTools
{
    public static class SceneBuilder
    {
        // ProjectBuilder.RunAll calls ApplyBuildSettings once, after this.
        public static void BuildAll()
        {
            BuildBootstrap();
            BuildMenu();
            LakeDockBuilder.Build();
        }

        public static void BuildBootstrap()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references (C 2.8 #14).
            NetworkObject sessionStatePrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.SessionStatePrefab);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionNames.AssetPath);
            if (inputActions == null) throw new System.IO.FileNotFoundException("Missing " + InputActionNames.AssetPath);
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.ContentRegistryAsset);
            var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetPaths.NetworkPrefabsListAsset);
            if (prefabList == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.NetworkPrefabsListAsset);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPaths.PanelSettingsAsset);
            if (panelSettings == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.PanelSettingsAsset);
            var appRoot = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetPaths.AppRootUxml);
            if (appRoot == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.AppRootUxml + " (T14)");

            // App: AppBootstrap, NetworkManager, UnityTransport, PlayerInputAdapter, AudioBus (contract 6.5 order).
            var app = new GameObject("App");
            AppBootstrap bootstrap = BuilderUtil.GetOrAdd<AppBootstrap>(app);
            NetworkManager networkManager = BuilderUtil.GetOrAdd<NetworkManager>(app);
            UnityTransport transport = BuilderUtil.GetOrAdd<UnityTransport>(app);
            BuilderUtil.GetOrAdd<PlayerInputAdapter>(app);
            BuilderUtil.GetOrAdd<AudioBus>(app);
            ConfigureNetworkManager(networkManager, transport, prefabList);
            bootstrap.Configure(sessionStatePrefab, inputActions, registry);

            var ui = new GameObject("UI");
            ui.transform.SetParent(app.transform, false);
            UIDocument document = BuilderUtil.GetOrAdd<UIDocument>(ui);
            document.panelSettings = panelSettings;
            var documentSerialized = new SerializedObject(document); // the property setter alone is not persisted into the scene file
            SerializedProperty panelProperty = documentSerialized.FindProperty("m_PanelSettings");
            if (panelProperty == null) throw new System.InvalidOperationException("m_PanelSettings not found on UIDocument");
            panelProperty.objectReferenceValue = panelSettings;
            documentSerialized.ApplyModifiedPropertiesWithoutUndo();
            document.visualTreeAsset = appRoot; // contract 6.5 calls this "sourceAsset"; the 6000.6 property is visualTreeAsset
            BuilderUtil.GetOrAdd<UiFlowController>(ui);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(app.transform, false);
            BuilderUtil.GetOrAdd<UnityEngine.EventSystems.EventSystem>(eventSystem);
            BuilderUtil.GetOrAdd<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(eventSystem); // default actions

            // Not under App (DontDestroyOnLoad): it unloads with Bootstrap, so Menu/gameplay keep exactly one listener.
            new GameObject("BootstrapListener").AddComponent<AudioListener>();

            BuilderUtil.SaveScene(scene, AssetPaths.BootstrapScene);
        }

        public static void BuildMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("MenuCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.10f, 0.14f);
            cameraObject.AddComponent<AudioListener>();

            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            BuilderUtil.SaveScene(scene, AssetPaths.MenuScene);
        }

        // Spec 4.6 step 6: build list in SceneNames.BuildOrder order (Bootstrap at index 0).
        public static void ApplyBuildSettings()
        {
            string[] names = SceneNames.BuildOrder;
            var entries = new EditorBuildSettingsScene[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = AssetPaths.ScenesFolder + "/" + names[i] + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new System.IO.FileNotFoundException("Missing scene " + path);
                entries[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = entries;
        }

        // Contract 6.5 NetworkManager configuration.
        static void ConfigureNetworkManager(NetworkManager networkManager, UnityTransport transport, NetworkPrefabsList prefabList)
        {
            // Null right after AddComponent when GenerateDefaultNetworkPrefabs is off (NGO's reset hook only builds it when on).
            if (networkManager.NetworkConfig == null) networkManager.NetworkConfig = new NetworkConfig();
            NetworkConfig config = networkManager.NetworkConfig;
            config.NetworkTransport = transport;
            config.TickRate = NetworkConstants.TickRate;
            config.ConnectionApproval = true;
            config.EnableSceneManagement = true;
            config.PlayerPrefab = null;
            config.ProtocolVersion = NetworkConstants.NgoProtocolVersion;
            config.ForceSamePrefabs = true;
            if (config.Prefabs == null) config.Prefabs = new NetworkPrefabs();
            config.Prefabs.NetworkPrefabsLists.Clear();
            config.Prefabs.NetworkPrefabsLists.Add(prefabList);
        }
    }
}
