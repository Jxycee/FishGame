using System;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode.Components;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: owner only. PlayerNetState enables this component (after the CharacterController) on the owning peer.
    // Moves the owner's CharacterController from the routed PlayerIntent; NetworkTransform (AuthorityMode Owner) replicates
    // the result to everyone. The host never moves players. Rules: spec 7.3; contract 7.12, D25, D26, D36.
    // No RequireComponent on purpose: PrefabBuilder (T15) adds the CharacterController itself (contract 6.3).
    // M2 (T16, M2-D1/D2): this stays the ONLY writer of IInputService.GameplayContext. Other components request a context through
    // IPlayerContextSource (fishing now, carry in M5); the first ENABLED source with a non-Free request wins, then
    // GameplayContextArbiter applies Climbing > Swimming > request > Free. While RodFighting (or a source reports LocksMovement) the wish
    // velocity is zero, there is no sprint, jump or climb start; gravity, swim buoyancy and yaw keep running. RodAiming: x0.6 speed, no sprint.
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        public Vector3 Velocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSwimming { get; private set; }
        public bool IsClimbing { get; private set; }
        public float SecondsInWater { get; private set; }

        // Owner only. Raised as the last statement of ApplyTeleport (TeleportRpc, out-of-bounds and water-timeout recovery).
        public event Action Teleported;

        // Internal: PlayerCamera reads this for the sprint FOV kick.
        internal bool IsSprinting { get; private set; }

        private CharacterController _cc;
        private NetworkTransform _nt;
        private PlayerNetState _net;
        private IInputService _input;
        private JumpAssist _jump;
        private float _vx, _vz;   // horizontal velocity, accelerated toward the wish velocity
        private float _kx, _kz;   // knockback (decays)
        private float _vy;        // vertical velocity
        private float _climbT;
        private Vector3 _climbFrom;
        private Vector3 _climbTo;
        private bool _warnedNoSpawn;
        private IPlayerContextSource[] _contextSources = Array.Empty<IPlayerContextSource>();

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _nt = GetComponent<NetworkTransform>();
            _net = GetComponent<PlayerNetState>();
            _contextSources = GetComponents<IPlayerContextSource>();   // includes disabled components; enabled-ness is checked per frame
            _jump = JumpAssist.Create();
        }

        private void OnEnable()
        {
            _contextSources = GetComponents<IPlayerContextSource>();   // refresh: sources added after Awake are picked up on the next enable
            _input = ResolveInput();
            if (_input == null || _cc == null)
            {
                Debug.LogError("PlayerLocomotion: IInputService (ServiceRoots.App) or CharacterController is missing.", this);
                enabled = false;
                return;
            }

            ResetMotion();
            IsGrounded = false;
            IsSwimming = false;
            IsClimbing = false;
            IsSprinting = false;
            SecondsInWater = 0f;
            _warnedNoSpawn = false;
            SetContext();
        }

        private void OnDisable()
        {
            if (IsClimbing && _cc != null)
            {
                _cc.enabled = true;
            }

            IsClimbing = false;
            IsSwimming = false;
            IsSprinting = false;
            SecondsInWater = 0f;
            Velocity = Vector3.zero;

            IInputService input = _input;
            _input = null;
            if (input == null || (input is UnityEngine.Object unityObject && unityObject == null))
            {
                return;
            }

            if (input.GameplayContext != InputContext.Free)
            {
                input.GameplayContext = InputContext.Free;
            }
        }

        private void Update()
        {
            _input = ResolveInput();
            if (_input == null || _cc == null)
            {
                return;
            }

            if (IsPaused())
            {
                Velocity = Vector3.zero;
                IsSprinting = false;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            PlayerIntent intent = _input.Intent;
            transform.Rotate(0f, intent.LookYawDeg, 0f);

            if (IsClimbing)
            {
                UpdateClimb(dt);
                SetContext();
                return;
            }

            if (!_cc.enabled)
            {
                return;
            }

            // Resolved once per frame: the effective context (aiming speed) and whether movement is locked (no walk, jump or climb start).
            InputContext ctx = ResolveContext();
            bool locked = MovementLocked(ctx);
            bool aiming = GameplayContextArbiter.IsAiming(ctx);

            Vector3 pos = transform.position;
            float surfaceY = WaterSurface.HeightAt(pos.x, pos.z);
            bool inVolume = WaterVolume.FindContaining(pos) != null;
            bool inWater = inVolume && MovementMath.IsInWater(pos.y, surfaceY);
            bool swimming = inVolume && MovementMath.IsSwimmingDepth(pos.y, surfaceY);

            if (inWater && !locked && TryBeginClimb(pos, intent))
            {
                SetContext();
                return;
            }

            if (swimming != IsSwimming)
            {
                IsSwimming = swimming;
                if (_net != null)
                {
                    _net.OwnerSetSwimming(swimming);
                }
            }

            bool grounded = _cc.isGrounded;
            IsGrounded = grounded;

            // No sprint in the air or in water (spec 7.3 step 2). Aiming (M2): x0.6 speed and no sprint. Carry (M5) is 0 kg.
            // Locked (RodFighting, M2-D2): no walking, sprint or jump; gravity, buoyancy and yaw still run.
            bool sprint = !swimming && grounded && intent.SprintHeld && !locked && !aiming;
            float wish = MovementMath.WishSpeed(sprint, aiming, swimming, 0f);
            IsSprinting = sprint && (intent.MoveX != 0f || intent.MoveY != 0f);

            Vector3 dir = locked ? Vector3.zero : transform.right * intent.MoveX + transform.forward * intent.MoveY;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            bool firmFooting = grounded || swimming;
            MovementMath.Accelerate(ref _vx, ref _vz, dir.x * wish, dir.z * wish,
                firmFooting ? MovementMath.GroundAccel : MovementMath.AirAccel, dt);
            MovementMath.DecayKnockback(ref _kx, ref _kz,
                firmFooting ? MovementMath.KnockbackDecayGround : MovementMath.KnockbackDecayAir, dt);

            float dy;
            if (swimming)
            {
                _jump = JumpAssist.Create();
                _vy = MovementMath.SwimVerticalVelocity(pos.y, surfaceY, _vy, dt);
                dy = _vy * dt;
            }
            else
            {
                if (grounded && _vy < 0f)
                {
                    _vy = MovementMath.GroundedStickVelocity;
                }

                // A jump buffered just before the lock must not fire on landing while locked (RodFighting); same reset as swimming.
                if (locked)
                {
                    _jump = JumpAssist.Create();
                }

                if (_jump.Tick(dt, grounded, intent.JumpPressed && !locked))
                {
                    _vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, MovementMath.Gravity);
                }

                dy = MovementMath.VerticalStep(ref _vy, MovementMath.Gravity, MovementMath.TerminalVelocity, dt);
            }

            CollisionFlags flags = _cc.Move(new Vector3((_vx + _kx) * dt, dy, (_vz + _kz) * dt));
            if ((flags & CollisionFlags.Above) != 0 && _vy > 0f)
            {
                _vy = 0f;
            }

            // Applied (post-collision) velocity: about zero while standing, so no phantom -2 m/s from the grounded stick.
            Velocity = _cc.velocity;

            SecondsInWater = swimming ? SecondsInWater + dt : 0f;
            if (transform.position.y < MovementMath.OutOfBoundsY || SecondsInWater > MovementMath.WaterTimeoutSeconds)
            {
                TeleportToSpawn();
            }

            SetContext();
        }

        // Owner only (TeleportRpc, out-of-bounds and water-timeout recovery). The position is applied immediately.
        // Velocity, knockback and the jump assist are cleared.
        public void ApplyTeleport(Vector3 position, float yaw)
        {
            if (_cc == null)
            {
                return;
            }

            bool restoreController = _cc.enabled || IsClimbing;
            _cc.enabled = false;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_nt != null && _nt.IsSpawned && _nt.CanCommitToTransform)
            {
                _nt.Teleport(position, rotation, Vector3.one);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            ResetMotion();
            IsClimbing = false;
            IsGrounded = false;
            SecondsInWater = 0f;
            _cc.enabled = restoreController;
            EventIsolation.Invoke(Teleported);
        }

        // M5 hook. Horizontal only in M1.
        public void AddKnockback(Vector3 velocity)
        {
            _kx += velocity.x;
            _kz += velocity.z;
        }

        // Inside a WaterExit: Jump or Interact always climbs; holding forward climbs only when facing the exit's Top,
        // so walking down the ramp into the lake (inside the ramp volume) does not yank the player back out.
        private bool TryBeginClimb(Vector3 pos, PlayerIntent intent)
        {
            WaterExit exit = WaterExit.FindContaining(pos);
            if (exit == null)
            {
                return false;
            }

            Vector3 top = exit.TopPoint;
            bool pressed = intent.JumpPressed || intent.InteractPressed;
            bool pushingToward = false;
            if (intent.MoveY > 0.5f)
            {
                Vector3 toTop = top - pos;
                toTop.y = 0f;
                pushingToward = toTop.sqrMagnitude < 0.0001f || Vector3.Dot(transform.forward, toTop) > 0f;
            }

            if (!pressed && !pushingToward)
            {
                return false;
            }

            BeginClimb(top);
            return true;
        }

        private void BeginClimb(Vector3 target)
        {
            _climbFrom = transform.position;
            _climbTo = target;
            _climbT = 0f;
            ResetMotion();
            IsClimbing = true;
            IsSprinting = false;
            IsGrounded = false;
            _cc.enabled = false;
        }

        private void UpdateClimb(float dt)
        {
            Vector3 before = transform.position;
            _climbT = Mathf.Min(1f, _climbT + dt / MovementMath.ClimbDuration);
            transform.position = Vector3.Lerp(_climbFrom, _climbTo, Mathf.SmoothStep(0f, 1f, _climbT));
            Velocity = (transform.position - before) / dt;
            if (_climbT < 1f)
            {
                return;
            }

            IsClimbing = false;
            IsSwimming = false;
            if (_net != null)
            {
                _net.OwnerSetSwimming(false);
            }

            ResetMotion();
            SecondsInWater = 0f;
            _cc.enabled = true;
        }

        private void TeleportToSpawn()
        {
            PlayerSpawner spawner = PlayerSpawner.Current;
            byte slot = _net != null ? _net.Slot : (byte)0;
            if (spawner != null && spawner.TryGetSpawnPose(slot, out Vector3 position, out Quaternion rotation))
            {
                ApplyTeleport(position, rotation.eulerAngles.y);
                return;
            }

            SecondsInWater = 0f;
            if (!_warnedNoSpawn)
            {
                _warnedNoSpawn = true;
                Debug.LogWarning("PlayerLocomotion: no spawn pose available for out-of-bounds or water-timeout recovery.", this);
            }
        }

        private void ResetMotion()
        {
            _vx = _vz = _kx = _kz = _vy = 0f;
            _jump = JumpAssist.Create();
            Velocity = Vector3.zero;
        }

        private void SetContext()
        {
            if (_input == null)
            {
                return;
            }

            InputContext target = ResolveContext();
            if (_input.GameplayContext != target)
            {
                _input.GameplayContext = target;
            }
        }

        // The first enabled source with a non-Free request, passed through the arbiter (Climbing > Swimming > request > Free).
        private InputContext ResolveContext()
        {
            InputContext requested = InputContext.Free;
            for (int i = 0; i < _contextSources.Length; i++)
            {
                IPlayerContextSource source = _contextSources[i];
                if (!(source is Behaviour behaviour) || !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                InputContext request = source.RequestedContext;
                if (request != InputContext.Free)
                {
                    requested = request;
                    break;
                }
            }

            return GameplayContextArbiter.Resolve(IsClimbing, IsSwimming, requested);
        }

        // Belt and braces: the context row (RodFighting) OR any enabled source reporting LocksMovement (true on the first Bite frame,
        // before the router has seen RodFighting).
        private bool MovementLocked(InputContext resolved)
        {
            if (GameplayContextArbiter.LocksMovement(resolved))
            {
                return true;
            }

            for (int i = 0; i < _contextSources.Length; i++)
            {
                IPlayerContextSource source = _contextSources[i];
                if (source is Behaviour behaviour && behaviour.isActiveAndEnabled && source.LocksMovement)
                {
                    return true;
                }
            }

            return false;
        }

        private static IInputService ResolveInput()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out IInputService input) ? input : null;
        }

        private static bool IsPaused()
        {
            ServiceRegistry game = ServiceRoots.Game;
            return game != null && game.TryGet(out IPauseState pause) && pause.Paused;
        }
    }
}
