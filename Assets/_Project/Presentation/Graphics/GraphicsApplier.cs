// Authority: local only (every peer); never writes gameplay state.
// Applies the Graphics settings (playtest notes S6/S3, 2026-10-13) at startup and live on every ISettings.Changed:
//  - URP: render scale, MSAA, and main-light shadow distance / resolution / cascades go onto a RUNTIME COPY of the active URP asset, which is
//    swapped in through QualitySettings.renderPipeline. The project's URP asset on disk is never touched (the copy lives in memory only),
//    and Dispose puts the original back, so a PlayMode session leaves ProjectSettings and Assets/Settings unchanged.
//  - FXAA is a per-camera setting: beginCameraRendering sets UniversalAdditionalCameraData.antialiasing on Game cameras, so a player camera
//    that spawns later (the player prefab) is covered without registering anything.
//  - VSync and the frame-rate cap are skipped in batch mode (headless tests run at 1000+ fps and assume it); resolution and window mode are
//    skipped in the editor and in batch mode, where they do nothing and Unity warns. Nothing here logs.
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

        private readonly ISettings _settings;
        private readonly UniversalRenderPipelineAsset _source;
        private readonly RenderPipelineAsset _previousQualityAsset;
        private readonly int _originalVSync, _originalTargetFrameRate;
        private UniversalRenderPipelineAsset _runtimeAsset;
        private Snapshot _last;
        private bool _hasLast;
        private bool _touchedFrame;
        private bool _fxaa;
        private bool _disposed;

        public GraphicsApplier(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _originalVSync = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;
            _previousQualityAsset = QualitySettings.renderPipeline;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)
            {
                _source = source;
                _runtimeAsset = UnityEngine.Object.Instantiate(source);
                _runtimeAsset.name = source.name + " (runtime copy)";
                _runtimeAsset.hideFlags = HideFlags.HideAndDontSave;
                QualitySettings.renderPipeline = _runtimeAsset;
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _settings.Changed += OnSettingsChanged;
            Apply(_settings.Current);
        }

        // The in-memory copy of the URP asset that carries the applied values; null when the active pipeline is not URP.
        public UniversalRenderPipelineAsset RuntimeAsset => _runtimeAsset;

        // The project's URP asset the copy was made from; never modified (tests assert that).
        public UniversalRenderPipelineAsset SourceAsset => _source;

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
            _settings.Changed -= OnSettingsChanged;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            if (_touchedFrame)
            {
                QualitySettings.vSyncCount = _originalVSync;
                Application.targetFrameRate = _originalTargetFrameRate;
            }

            if (_runtimeAsset != null)
            {
                QualitySettings.renderPipeline = _previousQualityAsset;
                UnityEngine.Object.Destroy(_runtimeAsset);
                _runtimeAsset = null;
            }
        }

        private void OnSettingsChanged(SettingsData data)
        {
            if (_disposed) return;
            Apply(data);
        }

        private void Apply(SettingsData data)
        {
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
            if (_runtimeAsset == null) return;

            _runtimeAsset.renderScale = s.Scale / 100f;
            _runtimeAsset.msaaSampleCount = GraphicsPresets.MsaaSamples(s.Aa);

            ShadowParams shadows = GraphicsPresets.ShadowFor(s.Shadows);
            _runtimeAsset.shadowDistance = shadows.Distance;   // 0 turns the main-light shadow pass off
            if (shadows.Distance > 0f)
            {
                _runtimeAsset.mainLightShadowmapResolution = shadows.Resolution;
                _runtimeAsset.shadowCascadeCount = shadows.Cascades;
            }
        }

        private void ApplyFrameRate(Snapshot s)
        {
            if (Application.isBatchMode) return;   // headless runs assume an uncapped frame rate

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
