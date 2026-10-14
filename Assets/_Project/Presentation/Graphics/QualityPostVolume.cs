// Authority: local only (every peer); purely visual, never writes gameplay state.
// Quality-first round 1 (user, 2026-10-14: "make the best map possible"): the scene's global quality post volume (bloom and vignette,
// VP_QualityPost, built by RenderingSetupBuilder) is drawn on Medium and High and switched off on the Low fallback. It follows
// GraphicsApplier.ScreenEffectsVisible (GraphicsPresets.UsesScreenEffects) by setting the Volume's weight to 1 or 0; with no applier running
// (tests, the editor, captures) the volume keeps weight 1. Tonemapping stays None (spec 10.3): this volume only adds bloom and a soft vignette.
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class QualityPostVolume : MonoBehaviour
    {
        private Volume _volume;

        private void OnEnable()
        {
            _volume = GetComponent<Volume>();
            GraphicsApplier.ScreenEffectsChanged += Apply;
            Apply(GraphicsApplier.ScreenEffectsVisible);
        }

        private void OnDisable()
        {
            GraphicsApplier.ScreenEffectsChanged -= Apply;
        }

        public void Apply(bool visible)
        {
            if (_volume == null) _volume = GetComponent<Volume>();   // edit-mode captures call Apply without OnEnable
            if (_volume != null) _volume.weight = visible ? 1f : 0f;
        }
    }
}
