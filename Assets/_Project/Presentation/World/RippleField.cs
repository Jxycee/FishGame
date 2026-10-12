// Authority: local only (every peer); never writes gameplay state.
// The eight ripple slots read by the water shader (spec 10.4, C 7.22). Slot = (x, z, spawn time on the water clock, strength).
// The array is always pushed full length (C 2.8 #18). Round robin: the next slot is the oldest (RippleMath.NextSlot).
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    public sealed class RippleField : MonoBehaviour
    {
        private static readonly int RipplesId = Shader.PropertyToID(ShaderGlobals.Ripples);

        public static RippleField Current { get; private set; }

        private readonly Vector4[] _slots = new Vector4[RippleMath.Slots];
        private int _last = -1;

        public void Spawn(float x, float z, float strength)
        {
            if (!IsFinite(x) || !IsFinite(z) || !IsFinite(strength) || strength <= 0f) return;
            int slot = RippleMath.NextSlot(_last);
            _slots[slot] = new Vector4(x, z, (float)WaterSurface.Now, strength);
            _last = slot;
            Shader.SetGlobalVectorArray(RipplesId, _slots);
        }

        private void OnEnable()
        {
            Current = this;
            ClearAndPush();
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            ClearAndPush();
        }

        private void ClearAndPush()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = Vector4.zero;
            _last = -1;
            Shader.SetGlobalVectorArray(RipplesId, _slots);
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        // Enter Play Mode options are on in this project, so statics survive play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }
    }
}
