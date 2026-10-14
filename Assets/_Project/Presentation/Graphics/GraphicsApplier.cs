// Authority: local only (every peer); never writes gameplay state.
// Applies the Graphics settings (playtest notes S6/S3, 2026-10-13) at startup and live on every ISettings.Changed:
//  - URP: render scale, MSAA, and main-light shadow distance / resolution / cascades are set IN PLACE on the active URP asset (no pipeline swap:
//    a hidden runtime copy assigned to QualitySettings.renderPipeline could be serialized as null by a project save during Play and leave the
//    project with no render pipeline). The five original values are captured first and written back by Dispose, which also runs from the
//    editor's ExitingPlayMode and beforeAssemblyReload hooks in case OnDestroy never does. The setters do not mark the asset dirty and nothing
//    here calls SetDirty or saves, so Assets/Settings and ProjectSettings stay unchanged after a session.
//  - FXAA is a per-camera setting: beginCameraRendering sets UniversalAdditionalCameraData.antialiasing on Game cameras, so a player camera
//    that spawns later (the player prefab) is covered without registering anything.
//  - Screen effects (quality-first round 1, 2026-10-14; GraphicsPresets.UsesScreenEffects): the URP ScreenSpaceAmbientOcclusion renderer feature
//    of the active renderers is switched on or off IN PLACE (its original active state is captured and written back by Dispose, like the five
//    URP values), and ScreenEffectsVisible / ScreenEffectsChanged drive the quality post volume (QualityPostVolume: bloom, vignette). Off on Low.
//  - VSync, the frame-rate cap, resolution and window mode are skipped in the editor (Play Mode keeps its own settings) and in batch mode
//    (headless tests run at 1000+ fps and assume it; Screen calls do nothing there and Unity warns). Nothing here logs.
// Pure numbers live in FishGame.Core.GraphicsPresets; the stored rows are SettingsData.
using System;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FishGame.Presentation
{
    public sealed class GraphicsApplier : IDisposable
    {
        // Everything the applier reads from SettingsData, so an unrelated settings change (a volume slider) applies nothing.
        private readonly struct Snapshot : IEquatable<Snapshot>
        {
            public readonly int Scale, Shadows, Aa, FrameCap, WindowMode, Width, Height;
            public readonly bool Vsync;

            public Snapshot(SettingsData d)
            {
                Scale = d.renderScalePercent;
                Shadows = d.shadowQuality;
                Aa = d.antiAliasing;
                FrameCap = d.frameRateCap;
                WindowMode = d.windowMode;
                Width = d.resolutionWidth;
                Height = d.resolutionHeight;
                Vsync = d.vsync;
            }

            public bool PipelineEquals(Snapshot o) => Scale == o.Scale && Shadows == o.Shadows && Aa == o.Aa;
            public bool FrameEquals(Snapshot o) => Vsync == o.Vsync && FrameCap == o.FrameCap;
            public bool DisplayEquals(Snapshot o) => WindowMode == o.WindowMode && Width == o.Width && Height == o.Height;

            public bool Equals(Snapshot o) => PipelineEquals(o) && FrameEquals(o) && DisplayEquals(o);
            public override bool Equals(object obj) => obj is Snapshot s && Equals(s);
            public override int GetHashCode() => HashCode.Combine(Scale, Shadows, Aa, FrameCap, WindowMode, Width, Height) ^ (Vsync ? 1 : 0);
        }

        // The live applier, so the editor hooks below can put the asset back even when OnDestroy never runs.
        private static GraphicsApplier s_active;

        // Scene detail (harbour round 0): false on the Low preset (GraphicsPresets.ShowsSceneDetail). AmbientLife hides its collider-free detail
        // objects while it is false. True when no applier runs (tests, the editor), so a scene shows everything by default.
        public static bool SceneDetailVisible { get; private set; } = true;
        public static event Action<bool> SceneDetailChanged;

        private static void SetSceneDetail(bool visible)
        {
            if (visible == SceneDetailVisible) return;
            SceneDetailVisible = visible;
            SceneDetailChanged?.Invoke(visible);
        }

        // Screen effects (quality-first round 1): false on the Low preset (GraphicsPresets.UsesScreenEffects). QualityPostVolume zeroes the
        // quality post volume's weight while it is false. True when no applier runs (tests, the editor, captures).
        public static bool ScreenEffectsVisible { get; private set; } = true;
        public static event Action<bool> ScreenEffectsChanged;

        private static void SetScreenEffects(bool visible)
        {
            if (visible == ScreenEffectsVisible) return;
            ScreenEffectsVisible = visible;
            ScreenEffectsChanged?.Invoke(visible);
        }

        private readonly ISettings _settings;
        private readonly UniversalRenderPipelineAsset _asset;
        private readonly int _originalVSync, _originalTargetFrameRate;
        private readonly float _originalRenderScale, _originalShadowDistance;
        private readonly int _originalMsaa, _originalShadowResolution, _originalCascades;
        private readonly ScriptableRendererFeature[] _ssao;      // the SSAO features of the asset's renderers (quality-first round 1)
        private readonly bool[] _originalSsaoActive;
        private Snapshot _last;
        private bool _hasLast;
        private bool _touchedFrame;
        private bool _fxaa;
        private bool _disposed;

        public GraphicsApplier(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            s_active?.Dispose();   // a leaked earlier applier first puts its originals back, so ours are the true ones
            _originalVSync = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                _asset = asset;
                _originalRenderScale = asset.renderScale;
                _originalMsaa = asset.msaaSampleCount;
                _originalShadowDistance = asset.shadowDistance;
                _originalShadowResolution = asset.mainLightShadowmapResolution;
                _originalCascades = asset.shadowCascadeCount;
                _ssao = FindSsao(asset);
                _originalSsaoActive = new bool[_ssao.Length];
                for (int i = 0; i < _ssao.Length; i++) _originalSsaoActive[i] = _ssao[i].isActive;
            }

            s_active = this;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _settings.Changed += OnSettingsChanged;
            Apply(_settings.Current);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active = null;
            SceneDetailVisible = true;
            SceneDetailChanged = null;
            ScreenEffectsVisible = true;
            ScreenEffectsChanged = null;
        }

        // Every ScreenSpaceAmbientOcclusion feature on the asset's renderer list (none when the renderers are not UniversalRendererData).
        private static ScriptableRendererFeature[] FindSsao(UniversalRenderPipelineAsset asset)
        {
            var found = new System.Collections.Generic.List<ScriptableRendererFeature>();
            foreach (ScriptableRendererData data in asset.rendererDataList)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                    if (feature is ScreenSpaceAmbientOcclusion && !found.Contains(feature)) found.Add(feature);
            }
            return found.ToArray();
        }

        // True while the asset's SSAO features are switched on by this applier's current settings (tests).
        public bool SsaoActive => _ssao != null && _ssao.Length > 0 && _ssao[0] != null && _ssao[0].isActive;
        public int SsaoFeatureCount => _ssao?.Length ?? 0;

#if UNITY_EDITOR
        // Belt and braces for the editor: leaving Play Mode or reloading assemblies puts the URP asset back even if the app object's OnDestroy
        // never ran (an exception during teardown, a stopped test run).
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditor()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) s_active?.Dispose();
            };
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => s_active?.Dispose();
        }
#endif

        // The project's URP asset the values are applied to (edited in place, restored by Dispose); null when the active pipeline is not URP.
        public UniversalRenderPipelineAsset RuntimeAsset => _asset;

        // Same asset as RuntimeAsset (kept for callers that name the project asset).
        public UniversalRenderPipelineAsset SourceAsset => _asset;

        // The asset's values as they were before this applier touched them (tests compare the asset with these after teardown).
        public float OriginalRenderScale => _originalRenderScale;
        public int OriginalMsaa => _originalMsaa;
        public float OriginalShadowDistance => _originalShadowDistance;
        public int OriginalShadowResolution => _originalShadowResolution;
        public int OriginalCascades => _originalCascades;

        // True while FXAA is the selected anti-aliasing (cameras get it in beginCameraRendering).
        public bool FxaaSelected => _fxaa;

        // Re-applies the current settings (tests; a scene load needs nothing, the copy outlives scenes).
        public void Reapply()
        {
            _hasLast = false;
            Apply(_settings.Current);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (s_active == this) s_active = null;
            _settings.Changed -= OnSettingsChanged;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            SetSceneDetail(true);
            SetScreenEffects(true);

            if (_touchedFrame)
            {
                QualitySettings.vSyncCount = _originalVSync;
                Application.targetFrameRate = _originalTargetFrameRate;
            }

            if (_asset != null)   // a destroyed asset compares null
            {
                _asset.renderScale = _originalRenderScale;
                _asset.msaaSampleCount = _originalMsaa;
                _asset.shadowDistance = _originalShadowDistance;
                _asset.mainLightShadowmapResolution = _originalShadowResolution;
                _asset.shadowCascadeCount = _originalCascades;
                for (int i = 0; i < _ssao.Length; i++)
                    if (_ssao[i] != null) _ssao[i].SetActive(_originalSsaoActive[i]);
            }
        }

        private void OnSettingsChanged(SettingsData data)
        {
            if (_disposed) return;
            Apply(data);
        }

        private void Apply(SettingsData data)
        {
            SetSceneDetail(GraphicsPresets.ShowsSceneDetail(data.graphicsPreset, data.renderScalePercent));
            bool effects = GraphicsPresets.UsesScreenEffects(data.graphicsPreset, data.renderScalePercent);
            SetScreenEffects(effects);
            if (_ssao != null)
                for (int i = 0; i < _ssao.Length; i++)
                    if (_ssao[i] != null && _ssao[i].isActive != (effects && _originalSsaoActive[i])) _ssao[i].SetActive(effects && _originalSsaoActive[i]);
            var now = new Snapshot(data);
            if (_hasLast && now.Equals(_last)) return;

            if (!_hasLast || !now.PipelineEquals(_last)) ApplyPipeline(now);
            if (!_hasLast || !now.FrameEquals(_last)) ApplyFrameRate(now);
            if (!_hasLast || !now.DisplayEquals(_last)) ApplyDisplay(now);

            _last = now;
            _hasLast = true;
        }

        private void ApplyPipeline(Snapshot s)
        {
            _fxaa = GraphicsPresets.UsesFxaa(s.Aa);
            if (_asset == null) return;

            _asset.renderScale = s.Scale / 100f;
            _asset.msaaSampleCount = GraphicsPresets.MsaaSamples(s.Aa);

            ShadowParams shadows = GraphicsPresets.ShadowFor(s.Shadows);
            _asset.shadowDistance = shadows.Distance;   // 0 turns the main-light shadow pass off
            if (shadows.Distance > 0f)
            {
                _asset.mainLightShadowmapResolution = shadows.Resolution;
                _asset.shadowCascadeCount = shadows.Cascades;
            }
        }

        private void ApplyFrameRate(Snapshot s)
        {
            if (Application.isEditor || Application.isBatchMode) return;   // the editor keeps its own VSync; headless runs assume an uncapped frame rate

            _touchedFrame = true;
            QualitySettings.vSyncCount = s.Vsync ? 1 : 0;
            int cap = GraphicsPresets.FrameCaps[Mathf.Clamp(s.FrameCap, 0, GraphicsPresets.FrameCaps.Length - 1)];
            Application.targetFrameRate = (!s.Vsync && cap > 0) ? cap : -1;
        }

        private void ApplyDisplay(Snapshot s)
        {
            if (Application.isEditor || Application.isBatchMode) return;   // no effect there, and Unity warns about it

            FullScreenMode mode = s.WindowMode == (int)WindowModeKind.Fullscreen ? FullScreenMode.ExclusiveFullScreen
                : (s.WindowMode == (int)WindowModeKind.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
            if (s.Width <= 0 || s.Height <= 0)
            {
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
            else
            {
                Screen.SetResolution(s.Width, s.Height, mode);
            }
        }

        // FXAA is applied per camera because it is a camera setting. Game cameras only (the Scene view and previews are left alone). Switching
        // away from FXAA resets only a camera that FXAA was put on, so an authored mode on some other camera is never overwritten.
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            ApplyToCamera(camera);
        }

        // Public so a PlayMode test can drive it without a rendering GPU (headless runs never raise beginCameraRendering).
        public void ApplyToCamera(Camera camera)
        {
            if (_disposed || camera == null || camera.cameraType != CameraType.Game) return;
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data)) return;

            if (_fxaa)
            {
                if (data.antialiasing != AntialiasingMode.FastApproximateAntialiasing) data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
            else if (data.antialiasing == AntialiasingMode.FastApproximateAntialiasing)
            {
                data.antialiasing = AntialiasingMode.None;
            }
        }
    }
}
