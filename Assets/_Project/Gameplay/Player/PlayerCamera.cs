using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: owner only (enabled by PlayerNetState on the owning peer, which also enables the Camera and AudioListener).
    // Look pitch is owner-local and replicated through PlayerNetState.OwnerSetPitch (NetPitchQ). FOV, the sprint kick,
    // trauma shake (HeadPivot/Shake, LateUpdate) and head bob are local presentation, all scaled by settings.cameraMotion
    // (spec 7.3). Cursor lock is owned by PlayerInputAdapter (contract D28), not here.
    public sealed class PlayerCamera : MonoBehaviour
    {
        private const float FallbackAspect = 16f / 9f;
        private const float BobMinSpeed = 0.1f;
        private const float BobFadePerSecond = 6f;

        public UnityEngine.Camera Camera { get; private set; }
        public float PitchDeg { get; private set; }

        private Transform _headPivot;
        private Transform _shake;
        private PlayerNetState _net;
        private PlayerLocomotion _locomotion;
        private float _trauma;
        private float _traumaSeed;
        private float _kick;
        private float _fovKickDeg;    // additive vertical-equivalent kick (AddFovKick), decays to 0
        private float _fovKickRate;   // degrees per second the kick decays at
        private float _bobDistance;
        private float _bobWeight;
        private float _bobY;
        private float _motion = 1f;

        private void Awake()
        {
            _headPivot = transform.Find(PlayerRigPaths.HeadPivot);
            _shake = transform.Find(PlayerRigPaths.Shake);
            Transform cameraTransform = transform.Find(PlayerRigPaths.Camera);
            Camera = cameraTransform != null ? cameraTransform.GetComponent<UnityEngine.Camera>() : null;
            _net = GetComponent<PlayerNetState>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _traumaSeed = UnityEngine.Random.value * 100f;
        }

        private void OnEnable()
        {
            if (_headPivot == null || _shake == null || Camera == null)
            {
                Debug.LogError("PlayerCamera: rig is missing HeadPivot, HeadPivot/Shake or the Camera on HeadPivot/Shake/Camera.", this);
                enabled = false;
                return;
            }

            if (ResolveInput() == null || ResolveSettings() == null)
            {
                Debug.LogError("PlayerCamera: IInputService or ISettings is not registered in ServiceRoots.App.", this);
                enabled = false;
                return;
            }

            Camera.nearClipPlane = MovementMath.NearClip;
            Camera.farClipPlane = MovementMath.FarClip;
            PitchDeg = 0f;
            _headPivot.localRotation = Quaternion.identity;
            _trauma = 0f;
            _kick = 0f;
            _fovKickDeg = 0f;
            _fovKickRate = 0f;
            _bobWeight = 0f;
            _bobY = 0f;
        }

        private void OnDisable()
        {
            _trauma = 0f;
            _kick = 0f;
            _fovKickDeg = 0f;
            _fovKickRate = 0f;
            _bobWeight = 0f;
            _bobY = 0f;
            if (_shake != null)
            {
                _shake.localRotation = Quaternion.identity;
                _shake.localPosition = Vector3.zero;
            }
        }

        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        // Additive vertical-equivalent FOV kick that decays linearly to 0 over `seconds`. Stacking calls add their degrees and the decay rate
        // is recomputed so the sum still reaches 0 in `seconds`. Scaled by settings.cameraMotion when applied (a motion of 0 shows no kick).
        public void AddFovKick(float degrees, float seconds)
        {
            _fovKickDeg += degrees;
            _fovKickRate = Mathf.Abs(_fovKickDeg) / Mathf.Max(0.01f, seconds);
        }

        private void Update()
        {
            IInputService input = ResolveInput();
            ISettings settings = ResolveSettings();
            if (input == null || settings == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            SettingsData data = settings.Current;
            _motion = data != null ? Mathf.Clamp01(data.cameraMotion) : 1f;
            float horizontalFov = data != null ? Mathf.Clamp(data.fieldOfView, 80f, 110f) : MovementMath.DefaultHorizontalFov;

            // Pitch: positive = look up. HeadPivot rotates about local X (negative X angle tilts up).
            PlayerIntent intent = input.Intent;
            PitchDeg = Mathf.Clamp(PitchDeg + intent.LookPitchDeg, -MovementMath.PitchLimit, MovementMath.PitchLimit);
            _headPivot.localRotation = Quaternion.Euler(-PitchDeg, 0f, 0f);
            if (_net != null)
            {
                _net.OwnerSetPitch(PitchDeg);
            }

            // FOV from the horizontal setting, recomputed every frame so an aspect change is picked up.
            float aspect = Camera.aspect;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
            {
                aspect = FallbackAspect;
            }

            float verticalFov = UnityEngine.Camera.HorizontalToVerticalFieldOfView(horizontalFov, aspect);
            bool sprinting = _locomotion != null && _locomotion.isActiveAndEnabled && _locomotion.IsSprinting;
            _kick = Mathf.MoveTowards(_kick, sprinting ? 1f : 0f, dt / MovementMath.SprintFovKickTime);
            _fovKickDeg = Mathf.MoveTowards(_fovKickDeg, 0f, _fovKickRate * dt);
            Camera.fieldOfView = Mathf.Clamp(verticalFov + MovementMath.SprintFovKickDeg * _kick * _motion + _fovKickDeg * _motion, 1f, 179f);

            _trauma = Mathf.MoveTowards(_trauma, 0f, MovementMath.TraumaDecayPerSecond * dt);

            // Head bob: vertical sine at horizontalSpeed / stride, faded in and out so it never snaps.
            Vector3 velocity = _locomotion != null ? _locomotion.Velocity : Vector3.zero;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            bool bobbing = _locomotion != null && _locomotion.IsGrounded && horizontalSpeed > BobMinSpeed;
            if (bobbing)
            {
                _bobDistance = Mathf.Repeat(_bobDistance + horizontalSpeed * dt, MovementMath.HeadBobStride);
            }

            _bobWeight = Mathf.MoveTowards(_bobWeight, bobbing ? 1f : 0f, BobFadePerSecond * dt);
            _bobY = Mathf.Sin(_bobDistance / MovementMath.HeadBobStride * Mathf.PI * 2f)
                * MovementMath.HeadBobAmplitude * _bobWeight * _motion;
        }

        private void LateUpdate()
        {
            if (_shake == null)
            {
                return;
            }

            float t = Time.time * MovementMath.TraumaFrequency;
            float amplitude = MovementMath.TraumaMaxAngleDeg * _trauma * _trauma * _motion;
            float pitch = (Mathf.PerlinNoise(_traumaSeed, t) - 0.5f) * 2f * amplitude;
            float yaw = (Mathf.PerlinNoise(_traumaSeed + 31.7f, t) - 0.5f) * 2f * amplitude;
            float roll = (Mathf.PerlinNoise(_traumaSeed + 63.1f, t) - 0.5f) * 2f * amplitude;
            _shake.localRotation = Quaternion.Euler(pitch, yaw, roll);
            _shake.localPosition = new Vector3(0f, _bobY, 0f);
        }

        private static IInputService ResolveInput()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out IInputService input) ? input : null;
        }

        private static ISettings ResolveSettings()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out ISettings settings) ? settings : null;
        }
    }
}
