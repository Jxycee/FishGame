using System;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode.Components;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: owner only. PlayerNetState enables this component (after the CharacterController) on the owning peer.
    // Moves the owner's CharacterController from the routed PlayerIntent; NetworkTransform (AuthorityMode Owner) replicates
    // the result to everyone. The host never moves players. Rules: spec 7.3; contract 7.12, D25, D26, D36 (ramp foot only since R70), M2-D29, M2-D30.
    // Ladders (R70, arcade climb 2026-10-13): the owner alone decides a climb from the static LadderVolumes; no RPC, no ladder state on the wire (no NetClimbing in M2,
    // M2-D32). Entering a ladder writes NetSwimming false. While climbing there is no gravity and Move goes along the camera look.
    // No RequireComponent on purpose: PrefabBuilder (T15) adds the CharacterController itself (contract 6.3).
    // M2 (T16, M2-D1/D2): this stays the ONLY writer of IInputService.GameplayContext. Other components request a context through
    // IPlayerContextSource (fishing now, carry in M5); the first ENABLED source with a non-Free request wins, then
    // GameplayContextArbiter applies Climbing > Swimming > request > Free. While RodFighting (or a source reports LocksMovement) the wish
    // velocity is zero, there is no sprint, jump or ladder entry; gravity, swim buoyancy and yaw keep running. RodAiming: x0.6 speed, no sprint.
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        public Vector3 Velocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSwimming { get; private set; }
        public bool IsClimbing { get; private set; }
        public float SecondsInWater { get; private set; }

        // Owner only (reads the owner's climb gate and capsule). True while a ladder may (re)take the player at once: inside the re-entry
        // grace after a top-out, or with the capsule centre inside a LadderVolume. FishingRodController holds the rod away while this is
        // true, so a top-out bob does not equip and stow it again.
        public bool LadderCatchPending => _cc != null && (!_climbGate.CanEnter || LadderVolume.FindContaining(CapsuleCenter()) != null);

        // Owner only. Raised as the last statement of ApplyTeleport (TeleportRpc, out-of-bounds and water-timeout recovery).
        public event Action Teleported;

        // Internal: PlayerCamera reads this for the sprint FOV kick.
        internal bool IsSprinting { get; private set; }

        private CharacterController _cc;
        private NetworkTransform _nt;
        private PlayerNetState _net;
        private IInputService _input;
        private JumpAssist _jump;
        private PlayerCamera _camera;   // pitch for the ladder climb; null on test rigs (pitch 0 then)
        private ClimbGate _climbGate;
        private ClimbMomentum _climbMomentum;   // arcade climb: kick and ramp while heading up (playtest 2026-10-13)
        private Vector3 _climbWish;             // the boosted wish velocity of the last climb frame (the top-out launch direction)
        private LadderVolume _ladder;   // the volume that held the capsule centre on the last climbing frame
        private RampAssist _rampAssist;   // R70 shore-ramp foot: wade not swim, one kick on entry
        private float _vx, _vz;   // horizontal velocity, accelerated toward the wish velocity
        private float _kx, _kz;   // knockback (decays)
        private float _vy;        // vertical velocity
        private bool _warnedNoSpawn;
        private IPlayerContextSource[] _contextSources = Array.Empty<IPlayerContextSource>();

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _nt = GetComponent<NetworkTransform>();
            _net = GetComponent<PlayerNetState>();
            _camera = GetComponent<PlayerCamera>();
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
            _ladder = null;
            _climbGate.Reset();
            _climbMomentum.Reset();
            _rampAssist = RampAssist.Create();
            IsSprinting = false;
            SecondsInWater = 0f;
            _warnedNoSpawn = false;
            SetContext();
        }

        private void OnDisable()
        {
            _ladder = null;
            _climbGate.Reset();
            _climbMomentum.Reset();
            _rampAssist = RampAssist.Create();

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

            if (!_cc.enabled)
            {
                return;
            }

            // Resolved once per frame: the effective context (aiming speed) and whether movement is locked (no walk, jump or ladder entry).
            InputContext ctx = ResolveContext();
            bool locked = MovementLocked(ctx);
            bool aiming = GameplayContextArbiter.IsAiming(ctx);
            _climbGate.Tick(dt);

            if (IsClimbing)
            {
                UpdateLadderClimb(dt, in intent, locked);
                SetContext();
                return;
            }

            // R70: touching a ladder starts the climb (walking, wading, swimming or off the deck edge; no button). Not while locked
            // (RodFighting) and not during the re-entry grace after topping out.
            if (!locked && _climbGate.CanEnter)
            {
                LadderVolume ladder = LadderVolume.FindContaining(CapsuleCenter());
                if (ladder != null)
                {
                    BeginLadderClimb(ladder);
                    SetContext();
                    return;
                }
            }

            Vector3 pos = transform.position;
            float surfaceY = WaterSurface.HeightAt(pos.x, pos.z);
            bool inVolume = WaterVolume.FindContaining(pos) != null;
            bool inWater = inVolume && MovementMath.IsInWater(pos.y, surfaceY);
            bool swimDepth = SwimmingAt(pos);

            // R70 shore-ramp foot (the Ramp WaterExit; D36: wading or swimming): in water there the player wades instead of swimming, and a
            // swimmer arriving while pushing toward the ramp Top (DA7's facing rule) gets one small upward kick. No lerp, no pull.
            WaterExit foot = inWater ? WaterExit.FindContaining(pos) : null;
            bool towardTop = false;
            if (foot != null && !locked)
            {
                Vector3 toTop = foot.TopPoint - pos;
                Vector3 forward = transform.forward;
                towardTop = MovementMath.PushingToward(intent.MoveY, forward.x, forward.z, toTop.x, toTop.z);
            }

            bool swimming = _rampAssist.Step(foot != null, inWater, swimDepth, towardTop, out float kickVy);

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

                if (kickVy > 0f)
                {
                    _vy = Mathf.Max(_vy, kickVy);   // R70 ramp foot: one small upward help out of the water
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

            bool restoreController = _cc.enabled;
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
            _ladder = null;
            _climbGate.Reset();
            _climbMomentum.Reset();
            _rampAssist = RampAssist.Create();
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

        // R70 ladder entry (contract M2-D29). No snap and no rotation change; the CharacterController stays on.
        private void BeginLadderClimb(LadderVolume ladder)
        {
            ResetMotion();
            _climbMomentum.Reset();
            _climbWish = Vector3.zero;
            _ladder = ladder;
            IsClimbing = true;
            IsSprinting = false;
            IsGrounded = false;
            SecondsInWater = 0f;
            if (IsSwimming)
            {
                IsSwimming = false;
                if (_net != null)
                {
                    _net.OwnerSetSwimming(false);
                }
            }

            _rampAssist = RampAssist.Create();
        }

        // R70 + arcade climb (playtest 2026-10-13): no gravity. Move goes along the camera look (PlayerCamera.PitchDeg, 0 without a camera);
        // while the climb heads up it kicks and ramps (ClimbMomentum) to about 2.3x ClimbSpeed, otherwise it stays at ClimbSpeed. Zero input
        // hangs in place at once (the charge resets). Locked (a source reporting LocksMovement) also hangs. The rail, posts and deck still collide.
        private void UpdateLadderClimb(float dt, in PlayerIntent intent, bool locked)
        {
            float pitch = _camera != null ? _camera.PitchDeg : 0f;
            float moveX = locked ? 0f : intent.MoveX;
            float moveY = locked ? 0f : intent.MoveY;
            MovementMath.ClimbWish(transform.eulerAngles.y, pitch, moveX, moveY, out float wx, out float wy, out float wz);
            float len = Mathf.Sqrt(wx * wx + wy * wy + wz * wz);
            bool holding = len > 0f;
            float boost = _climbMomentum.Step(holding, holding ? wy / len : 0f, dt);
            _climbWish = new Vector3(wx, wy, wz) * boost;
            _cc.Move(_climbWish * dt);
            Velocity = _cc.velocity;
            SecondsInWater = 0f;

            Vector3 center = CapsuleCenter();
            LadderVolume ladder = LadderVolume.FindContaining(center);
            if (ladder != null)
            {
                _ladder = ladder;
                return;
            }

            EndLadderClimb(_ladder != null && _ladder.IsAtOrAboveTop(center));
        }

        // Leaving the volume ends the climb. Through the top the climber gets the arcade launch (ClimbMomentum.TopOutLaunch: a small hop up and
        // forward along the commanded climb direction, built from the wish and not from _cc.velocity, which spikes at high frame rates), so
        // it pops onto the deck and normal control takes over. Any other exit keeps the applied velocity (RV's flying -> falling keeps it),
        // clamped to ClimbSpeed so a depenetration spike or the boosted climb speed is never carried into the water. Topping out starts the re-entry grace.
        private void EndLadderClimb(bool throughTop)
        {
            Vector3 v = Velocity;
            float cx = v.x, cy = v.y, cz = v.z;
            bool launched = false;
            if (throughTop)
            {
                Vector3 facing = transform.forward;
                launched = ClimbMomentum.TopOutLaunch(_climbMomentum.Speed, _climbWish.x, _climbWish.y, _climbWish.z, facing.x, facing.z,
                    out cx, out cy, out cz);
            }

            if (!launched)
            {
                cx = v.x;
                cy = v.y;
                cz = v.z;
                MovementMath.ClampClimbCarry(ref cx, ref cy, ref cz);
            }

            IsClimbing = false;
            _ladder = null;
            _climbMomentum.Reset();
            _climbWish = Vector3.zero;
            _vx = cx;
            _vz = cz;
            _vy = cy;
            _kx = _kz = 0f;
            _jump = JumpAssist.Create();
            IsGrounded = false;
            _climbGate.OnExit(throughTop);

            // (SwimmingAt deliberately skips the ramp-foot wade rule: the ladders and the ramp foot do not overlap, and the next Update
            // applies the foot rule anyway.)
            // A swimmer who leaves the volume at swim depth is swimming again on this very frame. Without this IsSwimming stays false for one
            // frame, so FishingRodController sees "not away" and equips the rod, then stows it again on the next frame (NetTool churn).
            if (SwimmingAt(transform.position))
            {
                IsSwimming = true;
                if (_net != null)
                {
                    _net.OwnerSetSwimming(true);
                }
            }
        }

        // In a WaterVolume with the root deeper than SwimEnterDepth below the (swelling) surface. Shared by Update and EndLadderClimb.
        private static bool SwimmingAt(Vector3 pos)
        {
            return WaterVolume.FindContaining(pos) != null
                   && MovementMath.IsSwimmingDepth(pos.y, WaterSurface.HeightAt(pos.x, pos.z));
        }

        private Vector3 CapsuleCenter()
        {
            return transform.TransformPoint(_cc.center);
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
