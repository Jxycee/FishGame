// Every project-relative path used by the T15 builders and validators (contract 6.2 plus T15 additions). Constants only. Owned by T15.
using FishGame.Runtime;

namespace FishGame.EditorTools
{
    public static class AssetPaths
    {
        // Contract 6.2
        public const string InputActions = InputActionNames.AssetPath;
        public const string ContentRegistryAsset = "Assets/_Project/Content/ContentRegistry.asset";
        public const string LakeDockSpotAsset = "Assets/_Project/Content/Spots/lake_dock.asset";
        public const string MaterialsFolder = "Assets/_Project/Content/Materials";
        public const string PanelSettingsAsset = "Assets/_Project/Content/UI/PanelSettings.asset";
        public const string ThemeStyleSheet = "Assets/_Project/UI/FishGameTheme.tss";
        public const string AppRootUxml = "Assets/_Project/UI/Uxml/AppRoot.uxml";
        public const string AppUss = "Assets/_Project/UI/Uss/App.uss";
        public const string NetworkPrefabsListAsset = "Assets/_Project/Content/Network/FishGameNetworkPrefabs.asset";
        public const string PlayerPrefab = "Assets/_Project/Prefabs/PlayerFP.prefab";
        public const string DockBellPrefab = "Assets/_Project/Prefabs/DockBell.prefab";
        public const string SessionStatePrefab = "Assets/_Project/Prefabs/SessionState.prefab";
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string MenuScene = "Assets/_Project/Scenes/Menu.unity";
        public const string LakeDockScene = "Assets/_Project/Scenes/Spot_LakeDock.unity";

        // T15 additions (generated output folders and project files read or written by the builders)
        public const string ProjectFolder = "Assets/_Project";
        public const string MeshesFolder = "Assets/_Project/Content/Meshes";
        public const string LakebedMesh = "Assets/_Project/Content/Meshes/LakeDock_Lakebed.asset";
        public const string ShoreMesh = "Assets/_Project/Content/Meshes/LakeDock_Shore.asset";
        public const string PcRenderPipelineAsset = "Assets/Settings/PC_RPAsset.asset";
        public const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
        public const string MobileRenderPipelineAsset = "Assets/Settings/Mobile_RPAsset.asset";
        public const string DefaultVolumeProfile = "Assets/Settings/DefaultVolumeProfile.asset";
        public const string TemplateInputActions = "Assets/InputSystem_Actions.inputactions";
        public const string DefaultNetworkPrefabs = "Assets/DefaultNetworkPrefabs.asset";
        public const string ManifestFile = "Packages/manifest.json";
        public const string TagManagerFile = "ProjectSettings/TagManager.asset";
        public const string DynamicsManagerFile = "ProjectSettings/DynamicsManager.asset";
        public const string EditorSettingsFile = "ProjectSettings/EditorSettings.asset";
        public const string ProjectSettingsFile = "ProjectSettings/ProjectSettings.asset";
        public const string EditorBuildSettingsFile = "ProjectSettings/EditorBuildSettings.asset";
        public const string NetcodeSettingsFile = "ProjectSettings/NetcodeForGameObjects.asset";

        // T14 additions (fishing, art and audio data; C 6.2). T20/T21 reuse ModelsFolder; do not redeclare it.
        public const string FishingFolder = "Assets/_Project/Content/Fishing";
        public const string RodsFolder = FishingFolder + "/Rods";
        public const string BaitsFolder = FishingFolder + "/Baits";
        public const string FightProfilesFolder = FishingFolder + "/FightProfiles";
        public const string SpeciesFolder = FishingFolder + "/Species";
        public const string JunkFolder = FishingFolder + "/Junk";
        public const string FishingTuningAsset = FishingFolder + "/FishingTuning.asset";
        public const string ArtFolder = "Assets/_Project/Content/Art";
        public const string ArtPaletteAsset = ArtFolder + "/ArtPalette.asset";
        public const string WaterSettingsAsset = ArtFolder + "/WaterSettings.asset";
        public const string AudioFolder = "Assets/_Project/Content/Audio";
        public const string SfxCueTableAsset = AudioFolder + "/SfxCueTable.asset";
        public const string ModelsFolder = "Assets/_Project/Content/Models";

        // T20 additions (shaders, Toon materials, body meshes). ModelsFolder is T14's; do not redeclare it.
        public const string ShadersFolder = "Assets/_Project/Shaders";
        public const string BodyUnitSphereMesh = MeshesFolder + "/Body_UnitSphere.asset";
        public const string BodyUnitBoxMesh = MeshesFolder + "/Body_UnitBox.asset";
        public const string BodyUnitCapsuleMesh = MeshesFolder + "/Body_UnitCapsule.asset";

        // T21 additions (rendering setup: volume profiles).
        public const string RenderingFolder = "Assets/_Project/Content/Rendering";
        public const string WarmVolumeProfile = RenderingFolder + "/VP_WarmSunset.asset";
        public const string CoolVolumeProfile = RenderingFolder + "/VP_CoolNight.asset";
    }
}
