using FishGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Gameplay
{
    // Authority: none (local presentation, runs on every peer). The slot material follows PlayerNetState.SlotChanged,
    // including the host's own ServerInit write (OnValueChanged fires on the writer). Owner: head, nose, torso, legs and
    // left arm render ShadowsOnly; the right arm stays visible, posed in camera space (lower right, follows pitch).
    // Remotes: NetPitchQ is smoothed onto HeadPivot and the right arm. Every peer runs the procedural walk cycle from the
    // root's frame-to-frame motion (spec 7.3, 7.4). HeadPivot has one writer per peer: PlayerCamera on the owner, this on
    // remotes. Execution order 200: LateUpdate runs after PlayerCamera's, so the owner's arm follows the shaken camera.
    [DefaultExecutionOrder(200)]
    public sealed class PlayerBody : MonoBehaviour
    {
        private const float LegSwingDeg = 30f;
        private const float ArmSwingDeg = 20f;
        private const float PitchSmoothing = 15f;
        private const float RemoteRightArmRaiseDeg = 70f;
        private const float OwnerRightArmPitchDeg = -90f;
        private const float SwingMinSpeed = 0.2f;
        private const float SwingMaxSpeed = 10f;
        private const float SwingFadePerSecond = 5f;
        private static readonly Vector3 OwnerRightArmCameraOffset = new Vector3(0.22f, -0.28f, 0.42f);

        [SerializeField] private Material[] slotMaterials;
        [SerializeField] private Material skinMaterial;

        private Transform _headPivot;
        private Transform _armL;
        private Transform _armR;
        private Transform _legL;
        private Transform _legR;
        private Transform _cameraTransform;
        private Renderer _headMesh;
        private Renderer _noseMesh;
        private Renderer _torsoMesh;
        private Renderer _armLMesh;
        private Renderer _armRMesh;
        private Renderer _legLMesh;
        private Renderer _legRMesh;
        private bool _rigReady;

        private PlayerNetState _state;
        private bool _isOwner;
        private float _smoothedPitch;
        private float _walkPhase;
        private float _swingWeight;
        private Vector3 _lastPosition;
        private bool _hasLast;

        public Transform NameplateAnchor { get; private set; }

        // Builder entry point (edit mode, before Awake). At runtime it also re-applies the materials.
        public void Configure(Material[] slotMaterials, Material skinMaterial)
        {
            this.slotMaterials = slotMaterials;
            this.skinMaterial = skinMaterial;
            if (_rigReady)
            {
                ApplySkin();
                ApplySlot(_state != null ? _state.Slot : (byte)0);
            }
        }

        private void Awake()
        {
            _headPivot = transform.Find(PlayerRigPaths.HeadPivot);
            _armL = transform.Find(PlayerRigPaths.ArmL);
            _armR = transform.Find(PlayerRigPaths.ArmR);
            _legL = transform.Find(PlayerRigPaths.LegL);
            _legR = transform.Find(PlayerRigPaths.LegR);
            _cameraTransform = transform.Find(PlayerRigPaths.Camera);
            _headMesh = FindRenderer(PlayerRigPaths.Head);
            _noseMesh = FindRenderer(PlayerRigPaths.Nose);
            _torsoMesh = FindRenderer(PlayerRigPaths.Torso);
            _armLMesh = FindRenderer(PlayerRigPaths.ArmLMesh);
            _armRMesh = FindRenderer(PlayerRigPaths.ArmRMesh);
            _legLMesh = FindRenderer(PlayerRigPaths.LegLMesh);
            _legRMesh = FindRenderer(PlayerRigPaths.LegRMesh);
            NameplateAnchor = transform.Find(PlayerRigPaths.NameplateAnchor);

            _rigReady = _headPivot != null && _armL != null && _armR != null && _legL != null && _legR != null
                && _headMesh != null && _noseMesh != null && _torsoMesh != null && _armLMesh != null
                && _armRMesh != null && _legLMesh != null && _legRMesh != null;
            if (!_rigReady)
            {
                Debug.LogError("PlayerBody: the PlayerFP rig is incomplete (see PlayerRigPaths and contract 6.3).", this);
                return;
            }

            ApplySkin();
        }

        // Applies the current slot now and follows SlotChanged afterwards. Called from PlayerNetState.OnNetworkSpawn.
        public void Bind(PlayerNetState state, bool isOwner)
        {
            Unbind();
            if (state == null)
            {
                return;
            }

            _state = state;
            _isOwner = isOwner;
            _hasLast = false;
            _smoothedPitch = PitchQuant.Dequantize(state.NetPitchQ.Value);
            state.SlotChanged += ApplySlot;
            if (!_rigReady)
            {
                return;
            }

            ApplySlot(state.Slot);
            ApplyVisibility(isOwner);
        }

        public void Unbind()
        {
            if (_state != null)
            {
                _state.SlotChanged -= ApplySlot;
            }

            _state = null;
            _isOwner = false;
        }

        private void Update()
        {
            if (!_rigReady)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 position = transform.position;
            float speed = 0f;
            if (_hasLast)
            {
                Vector3 delta = position - _lastPosition;
                speed = Mathf.Min(new Vector2(delta.x, delta.z).magnitude / dt, SwingMaxSpeed);
            }

            _lastPosition = position;
            _hasLast = true;

            // Walk cycle on every peer: legs +-30 deg, left arm counter-swings +-20 deg at speed / stride frequency.
            _swingWeight = Mathf.MoveTowards(_swingWeight, speed > SwingMinSpeed ? 1f : 0f, SwingFadePerSecond * dt);
            _walkPhase = Mathf.Repeat(_walkPhase + speed * dt / MovementMath.HeadBobStride * Mathf.PI * 2f, Mathf.PI * 2f);
            float swing = Mathf.Sin(_walkPhase) * _swingWeight;
            _legL.localRotation = Quaternion.Euler(LegSwingDeg * swing, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(-LegSwingDeg * swing, 0f, 0f);
            _armL.localRotation = Quaternion.Euler(-ArmSwingDeg * swing, 0f, 0f);

            if (_state == null || _isOwner)
            {
                return;
            }

            // Remotes only: pitch = lerp(pitch, NetPitchQ / 100, 1 - exp(-15 dt)) on the head and the right arm.
            float target = PitchQuant.Dequantize(_state.NetPitchQ.Value);
            _smoothedPitch = Mathf.Lerp(_smoothedPitch, target, 1f - Mathf.Exp(-PitchSmoothing * dt));
            _headPivot.localRotation = Quaternion.Euler(-_smoothedPitch, 0f, 0f);
            _armR.localRotation = Quaternion.Euler(-RemoteRightArmRaiseDeg - _smoothedPitch, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (!_rigReady || !_isOwner || _cameraTransform == null)
            {
                return;
            }

            // Owner only: right arm in camera space, lower right, pointing along the view (follows pitch and shake).
            _armR.SetPositionAndRotation(
                _cameraTransform.TransformPoint(OwnerRightArmCameraOffset),
                _cameraTransform.rotation * Quaternion.Euler(OwnerRightArmPitchDeg, 0f, 0f));
        }

        private void ApplySlot(byte slot)
        {
            if (!_rigReady || slotMaterials == null || slotMaterials.Length == 0)
            {
                return;
            }

            Material material = slotMaterials[Mathf.Clamp(slot, 0, slotMaterials.Length - 1)];
            if (material == null)
            {
                return;
            }

            // sharedMaterial swap only: no runtime material instances, no MaterialPropertyBlock (spec 7.4).
            _torsoMesh.sharedMaterial = material;
            _armLMesh.sharedMaterial = material;
            _armRMesh.sharedMaterial = material;
            _legLMesh.sharedMaterial = material;
            _legRMesh.sharedMaterial = material;
        }

        private void ApplySkin()
        {
            if (!_rigReady || skinMaterial == null)
            {
                return;
            }

            _headMesh.sharedMaterial = skinMaterial;
            _noseMesh.sharedMaterial = skinMaterial;
        }

        private void ApplyVisibility(bool owner)
        {
            ShadowCastingMode hidden = owner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            _headMesh.shadowCastingMode = hidden;
            _noseMesh.shadowCastingMode = hidden;
            _torsoMesh.shadowCastingMode = hidden;
            _armLMesh.shadowCastingMode = hidden;
            _legLMesh.shadowCastingMode = hidden;
            _legRMesh.shadowCastingMode = hidden;
            _armRMesh.shadowCastingMode = ShadowCastingMode.On;
        }

        private Renderer FindRenderer(string path)
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<Renderer>() : null;
        }
    }
}
