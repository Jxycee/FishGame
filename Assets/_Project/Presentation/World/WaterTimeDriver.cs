// Authority: local only (every peer); writes the shared WaterSurface (C 7.22); no networked or host state.
// The water clock and waves (spec 10.4, C 7.22, ruling C-Q4). C# (WaterSurface) and the GPU (_WaterTime, _Waves) share one formula,
// one clock and one float value: the time source returns (double)(float)t, and that same float is pushed to _WaterTime.
// Not ExecuteAlways: it never touches WaterSurface statics outside Play mode (EditMode tests open the lake scene).
using System;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Presentation
{
    [DefaultExecutionOrder(-200)]
    public sealed class WaterTimeDriver : MonoBehaviour
    {
        private static readonly int WavesId = Shader.PropertyToID(ShaderGlobals.Waves);
        private static readonly int WaterTimeId = Shader.PropertyToID(ShaderGlobals.WaterTime);

        // The driver that configured WaterSurface last. A teardown from an older scene's driver (destroyed after the new one enabled) must
        // not flatten the newer driver's water. Reset on SubsystemRegistration like RippleField.Current.
        private static WaterTimeDriver s_owner;

        [SerializeField] private WaterSettingsDef waterSettings;

        private readonly Vector4[] _waves = new Vector4[ShaderGlobals.WavesLength];
        private Func<double> _now;

        private void Awake()
        {
            _now = ReadClock;   // cached once: no per-call allocation
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (_now == null) _now = ReadClock;
            s_owner = this;

            WaterSettingsDef settings = ResolveSettings();
            if (settings == null)
            {
                WaterSurface.ResetToFlat();
                PushZeroWaves();
                return;
            }

            WaterSurface.Configure(settings.ToWaveSet());
            WaterSurface.SetTimeSource(_now);

            for (int i = 0; i < _waves.Length; i++)
            {
                _waves[i] = i < 3 && i < settings.DirAmpLenSpeed.Count ? settings.DirAmpLenSpeed[i] : Vector4.zero;
            }
            Shader.SetGlobalVectorArray(WavesId, _waves);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            Shader.SetGlobalFloat(WaterTimeId, (float)WaterSurface.Now);
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying || s_owner != this) return;
            s_owner = null;
            WaterSurface.ResetToFlat();
            PushZeroWaves();
            Shader.SetGlobalFloat(WaterTimeId, 0f);
        }

        // Enter Play Mode options are on in this project, so statics survive play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_owner = null;
        }

        private void PushZeroWaves()
        {
            for (int i = 0; i < _waves.Length; i++) _waves[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(WavesId, _waves);
        }

        // Session ServerTime while listening (all peers agree), else the level clock. The float cast is the parity rule.
        private static double ReadClock()
        {
            NetworkManager nm = NetworkManager.Singleton;
            double t = nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeSinceLevelLoad;
            return (double)(float)t;
        }

        private WaterSettingsDef ResolveSettings()
        {
            if (waterSettings != null) return waterSettings;
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out ContentRegistry registry)) return registry.WaterSettings;
            return null;
        }
    }
}
