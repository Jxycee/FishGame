// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.
// The cast marker (spec 9.11 Ready / Charge / Invalid release rows, C 6.3, C 7.24): OWNER ONLY. It reads the same root's FishingRodController
// (MarkerVisible, MarkerValid, MarkerPosition, ChargeNorm) and draws a flat marker on the water: Valid = solid ring + centre dot, Invalid = dashed
// ring + cross (shape, not hue only). It slides with the charge because MarkerPosition does. Remotes never show one: once the rod is spawned
// and is not ours, the renderers go off and this component disables itself (the prefab default is both renderers disabled, so a remote never
// flashes one before that). On Cue InvalidRelease the Invalid shape flashes every 0.075 s for 0.45 s, even if the marker would be hidden.
using FishGame.Core;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    // Order 270 (F7): last of the rod readers.
    [DefaultExecutionOrder(270)]
    public sealed class CastMarkerView : MonoBehaviour
    {
        private const string ValidName = "Valid", InvalidName = "Invalid";
        private const float FlashSeconds = 0.45f, FlashStep = 0.075f;
        private const float LiftAboveWater = 0.03f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color InvalidGrey = new Color(0.55f, 0.55f, 0.55f, 1f);

        private FishingRodNetwork _rod;
        private FishingRodController _controller;
        private Transform _marker;
        private MeshRenderer _valid, _invalid;
        private Material _validMaterial, _invalidMaterial;   // the owner's own instances (colour)
        private bool _live, _coloured;
        private float _flashStart = float.NegativeInfinity;

        private void Awake()
        {
            _rod = GetComponent<FishingRodNetwork>();
            _controller = GetComponent<FishingRodController>();
            _marker = transform.Find(PlayerRigPaths.CastMarker);
            if (_marker != null)
            {
                Transform valid = _marker.Find(ValidName), invalid = _marker.Find(InvalidName);
                _valid = valid != null ? valid.GetComponent<MeshRenderer>() : null;
                _invalid = invalid != null ? invalid.GetComponent<MeshRenderer>() : null;
            }

            SetRenderers(false, false);
        }

        private void OnDestroy()
        {
            if (_validMaterial != null) Destroy(_validMaterial);
            if (_invalidMaterial != null) Destroy(_invalidMaterial);
        }

        private void OnEnable()
        {
            if (_rod == null) return;
            _rod.Cue += HandleCue;
            _live = true;
        }

        private void OnDisable()
        {
            _live = false;
            if (_rod != null) _rod.Cue -= HandleCue;
            _flashStart = float.NegativeInfinity;
            SetRenderers(false, false);
        }

        private void HandleCue(RodCue cue, byte arg)
        {
            if (!_live || !isActiveAndEnabled || !_rod.IsOwner) return;   // a remote never flashes
            if (cue == RodCue.InvalidRelease) _flashStart = Time.time;
        }

        private void LateUpdate()
        {
            if (!_live || _rod == null || _marker == null || !_rod.IsSpawned) return;   // before spawn: nothing (F9)

            if (!_rod.IsOwner)
            {
                SetRenderers(false, false);
                enabled = false;   // OnDisable unsubscribes; a remote never needs this again
                return;
            }

            if (_controller == null) return;
            float now = Time.time;
            bool flashing = now - _flashStart < FlashSeconds;
            if (!_controller.MarkerVisible && !flashing)
            {
                SetRenderers(false, false);
                return;
            }

            Vector3 target = _controller.MarkerPosition;
            if (float.IsNaN(target.x) || float.IsNaN(target.z) || float.IsInfinity(target.x) || float.IsInfinity(target.z))
            {
                SetRenderers(false, false);
                return;
            }

            float y = WaterSurface.HeightAt(target.x, target.z) + LiftAboveWater;
            Vector3 root = transform.position;
            float dx = target.x - root.x, dz = target.z - root.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float scale = (1f + 0.03f * distance) * (1f + 0.1f * Mathf.Clamp01(_controller.ChargeNorm));
            _marker.SetPositionAndRotation(new Vector3(target.x, y, target.z), Quaternion.identity);   // flat
            _marker.localScale = Vector3.one * scale;
            EnsureColours();

            if (flashing)
            {
                bool on = ((int)((now - _flashStart) / FlashStep) & 1) == 0;
                SetRenderers(false, on);
            }
            else
            {
                bool valid = _controller.MarkerValid;
                SetRenderers(valid, !valid);
            }
        }

        // The owner's material instances: white when valid, grey (0.55) when invalid. Created on first use, so a remote never makes any.
        private void EnsureColours()
        {
            if (_coloured) return;
            _coloured = true;
            if (_valid != null && _valid.sharedMaterial != null)
            {
                _validMaterial = _valid.material;
                _validMaterial.SetColor(BaseColorId, Color.white);
            }

            if (_invalid != null && _invalid.sharedMaterial != null)
            {
                _invalidMaterial = _invalid.material;
                _invalidMaterial.SetColor(BaseColorId, InvalidGrey);
            }
        }

        private void SetRenderers(bool valid, bool invalid)
        {
            if (_valid != null && _valid.enabled != valid) _valid.enabled = valid;
            if (_invalid != null && _invalid.enabled != invalid) _invalid.enabled = invalid;
        }
    }
}
