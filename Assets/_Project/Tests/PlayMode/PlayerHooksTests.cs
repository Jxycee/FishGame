// Authority: test-only. T16 gameplay hooks that EditMode cannot reach (NetworkBehaviours, MonoBehaviour update loops):
//  - PlayerLocomotion context arbitration, movement lock and aiming speed, on a standalone rig (CharacterController + test
//    IPlayerContextSource components + the real PlayerLocomotion) so no fishing component is needed and nothing else writes GameplayContext;
//  - PlayerCamera.AddFovKick on a standalone camera rig;
//  - PlayerNetState.ServerInitialized and NetTool through the real solo host.
// The rigs live far from the Menu scene content at (500, y, 500) on their own floor and are destroyed in TearDown.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class PlayerHooksTests
    {
        private sealed class TestSource : MonoBehaviour, IPlayerContextSource
        {
            public InputContext Requested = InputContext.Free;
            public bool Locks;

            public InputContext RequestedContext => Requested;
            public bool LocksMovement => Locks;
        }

        private static readonly Vector3 RigOrigin = new Vector3(500f, 0.05f, 500f);
        private readonly List<GameObject> _made = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }

            _made.Clear();
            yield return null;
            yield return PlayModeHarness.TearDownApp();
        }

        private FakeInputService InstallFakeInput()
        {
            Assert.That(ServiceRoots.App, Is.Not.Null, "ServiceRoots.App missing; load Bootstrap first");
            var fake = new FakeInputService { MenuOpen = false, GameplayContext = InputContext.Free, Frame = default };
            ServiceRoots.App.Replace<IInputService>(fake);
            return fake;
        }

        private void MakeFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestRigFloor";
            floor.transform.position = new Vector3(RigOrigin.x, -0.5f, RigOrigin.z);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            _made.Add(floor);
        }

        // Components are added while the object is inactive, so PlayerLocomotion.Awake (which caches the sources) sees every source.
        private PlayerLocomotion MakeLocomotionRig(int sourceCount, out TestSource[] sources)
        {
            MakeFloor();
            var go = new GameObject("TestLocomotionRig");
            _made.Add(go);
            go.SetActive(false);
            go.transform.position = RigOrigin;
            var cc = go.AddComponent<CharacterController>();
            cc.height = MovementMath.CapsuleHeight;
            cc.radius = MovementMath.CapsuleRadius;
            cc.center = new Vector3(0f, MovementMath.CapsuleCenterY, 0f);
            cc.stepOffset = MovementMath.StepOffset;
            cc.slopeLimit = MovementMath.SlopeLimit;
            cc.skinWidth = MovementMath.SkinWidth;
            cc.minMoveDistance = 0f;   // as PrefabBuilder: batchmode frames move < 1 mm, which the default 0.001 would drop
            sources = new TestSource[sourceCount];
            for (int i = 0; i < sourceCount; i++) sources[i] = go.AddComponent<TestSource>();
            var locomotion = go.AddComponent<PlayerLocomotion>();
            go.SetActive(true);
            return locomotion;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        private static IEnumerator Settle(PlayerLocomotion locomotion, FakeInputService fake)
        {
            fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => locomotion.IsGrounded, PlayModeHarness.DefaultTimeoutSeconds, "rig never became grounded");
            yield return PlayModeHarness.WaitGameSeconds(0.6f, PlayModeHarness.DefaultTimeoutSeconds, "settle");
        }

        // ---- locomotion ----

        // Catches: the lock ignored or applied only through the router (the source alone, with the context still Free, must stop walking and
        // jumping), jump gating dropped (ladder entry while locked is LadderClimbTests.Ladder_EntryWhileMovementLocked_WaitsForTheLockToEnd), gravity or yaw frozen by the lock, and the no-source path (M1 behaviour) being changed.
        [UnityTest]
        public IEnumerator Locomotion_LockingSource_BlocksWalkAndJump_ButGravityAndYawContinue()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            FakeInputService fake = InstallFakeInput();
            PlayerLocomotion locomotion = MakeLocomotionRig(1, out TestSource[] sources);
            TestSource source = sources[0];
            yield return Settle(locomotion, fake);

            // M1 behaviour with no request: walks.
            Vector3 start = locomotion.transform.position;
            fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitGameSeconds(0.5f, PlayModeHarness.DefaultTimeoutSeconds, "free walk");
            Assert.That(Flat(start, locomotion.transform.position), Is.GreaterThan(1f), "without any request the rig walks (M1 behaviour)");
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.Free));
            yield return Settle(locomotion, fake);

            // The source alone locks (context stays Free, so the router passes Move and Jump through): the locomotion itself must lock.
            source.Locks = true;
            start = locomotion.transform.position;
            float maxY = start.y;
            fake.Frame = new PlayerInputFrame { MoveY = 1f, SprintHeld = true, JumpPressed = true };
            double end = Time.timeAsDouble + 0.6;
            while (Time.timeAsDouble < end)
            {
                yield return null;
                maxY = Mathf.Max(maxY, locomotion.transform.position.y);
            }

            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.Free), "LocksMovement does not change the context");
            Assert.That(Flat(start, locomotion.transform.position), Is.LessThan(0.05f), "locked: no walking");
            Assert.That(maxY - start.y, Is.LessThan(0.05f), "locked: no jump");

            // Yaw keeps running while locked.
            float yaw0 = locomotion.transform.eulerAngles.y;
            fake.Frame = new PlayerInputFrame { LookYawDeg = 1f };
            for (int i = 0; i < 20; i++) yield return null;
            float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, locomotion.transform.eulerAngles.y));
            Assert.That(turned, Is.GreaterThan(10f), "locked: yaw still rotates (turned " + turned.ToString("F1") + ")");

            // Gravity keeps running while locked: lift the rig 3 m, it must fall back to the floor.
            fake.Frame = default;
            var cc = locomotion.GetComponent<CharacterController>();
            cc.enabled = false;
            locomotion.transform.position += Vector3.up * 3f;
            cc.enabled = true;
            float liftedY = locomotion.transform.position.y;
            yield return PlayModeHarness.WaitUntil(() => locomotion.transform.position.y < liftedY - 2.5f, PlayModeHarness.DefaultTimeoutSeconds,
                "locked: gravity still pulls the rig down");

            // Released: walks again.
            source.Locks = false;
            yield return Settle(locomotion, fake);
            start = locomotion.transform.position;
            fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitGameSeconds(0.5f, PlayModeHarness.DefaultTimeoutSeconds, "walk after release");
            Assert.That(Flat(start, locomotion.transform.position), Is.GreaterThan(1f), "unlocked again: walks");
        }

        // Catches (I-1): a jump buffered while airborne and unlocked surviving a lock, so the player pops up on landing while locked
        // (RodFighting). The wrong implementation passes jumpPressed=false while locked but keeps the 0.1 s buffer alive.
        [UnityTest]
        public IEnumerator Locomotion_JumpBufferedBeforeLock_DoesNotFireOnLandingWhileLocked()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            FakeInputService fake = InstallFakeInput();
            PlayerLocomotion locomotion = MakeLocomotionRig(1, out TestSource[] sources);
            yield return Settle(locomotion, fake);
            float groundY = locomotion.transform.position.y;

            // Drop the rig from 0.8 m: the fall to the pre-landing window (about 0.2 s) outlasts the 0.1 s coyote time, so the press below can
            // only be BUFFERED (a coyote jump would fire at once and make the test meaningless). Press when the rig is within 0.15 m of the
            // floor, so it lands inside the 0.1 s jump buffer.
            var cc = locomotion.GetComponent<CharacterController>();
            cc.enabled = false;
            locomotion.transform.position += Vector3.up * 0.8f;
            cc.enabled = true;
            fake.Frame = default;
            yield return PlayModeHarness.WaitUntil(
                () => !locomotion.IsGrounded && locomotion.transform.position.y - groundY < 0.15f,
                PlayModeHarness.DefaultTimeoutSeconds, "rig never fell to the pre-landing window");

            fake.Frame = new PlayerInputFrame { JumpPressed = true };
            yield return null;   // the press frame: unlocked, airborne -> the jump is only buffered
            Assert.That(locomotion.IsGrounded, Is.False, "precondition: the rig was still airborne on the press frame (the jump only buffers)");
            float pressY = locomotion.transform.position.y;
            fake.Frame = default;
            sources[0].Locks = true;   // lock from the very next frame

            float maxY = pressY;
            double end = Time.timeAsDouble + 0.8;
            while (Time.timeAsDouble < end)
            {
                yield return null;
                maxY = Mathf.Max(maxY, locomotion.transform.position.y);
            }

            Assert.That(locomotion.IsGrounded, Is.True, "the rig landed");
            Assert.That(maxY - pressY, Is.LessThan(0.05f), "landing while locked must not fire the buffered jump (rose " + (maxY - pressY).ToString("F2") + " m)");
        }

        // Catches: RodFighting not being written as the GameplayContext through the arbiter, and the context row not locking movement.
        [UnityTest]
        public IEnumerator Locomotion_RodFightingRequest_IsWrittenAndLocksMovement()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            FakeInputService fake = InstallFakeInput();
            PlayerLocomotion locomotion = MakeLocomotionRig(1, out TestSource[] sources);
            yield return Settle(locomotion, fake);

            sources[0].Requested = InputContext.RodFighting;
            fake.Frame = new PlayerInputFrame { MoveY = 1f, JumpPressed = true, SprintHeld = true };
            yield return null;
            yield return null;
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.RodFighting), "locomotion writes the requested context");

            Vector3 start = locomotion.transform.position;
            yield return PlayModeHarness.WaitGameSeconds(0.5f, PlayModeHarness.DefaultTimeoutSeconds, "fighting");
            Assert.That(Flat(start, locomotion.transform.position), Is.LessThan(0.05f), "RodFighting: no walking");
            Assert.That(locomotion.transform.position.y - start.y, Is.LessThan(0.05f), "RodFighting: no jump");

            sources[0].Requested = InputContext.Free;
            yield return null;
            yield return null;
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.Free), "request withdrawn: back to Free");
        }

        // Catches: aiming ignored by the wish speed (x0.6 of walk), or aiming not producing the context at all.
        [UnityTest]
        public IEnumerator Locomotion_RodAimingRequest_WalksAtSixtyPercent()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            FakeInputService fake = InstallFakeInput();
            PlayerLocomotion locomotion = MakeLocomotionRig(1, out TestSource[] sources);
            yield return Settle(locomotion, fake);

            fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitGameSeconds(0.8f, PlayModeHarness.DefaultTimeoutSeconds, "free walk");
            float freeSpeed = new Vector2(locomotion.Velocity.x, locomotion.Velocity.z).magnitude;
            Assert.That(freeSpeed, Is.EqualTo(MovementMath.WalkSpeed).Within(0.3f), "free walk speed");
            yield return Settle(locomotion, fake);

            sources[0].Requested = InputContext.RodAiming;
            fake.Frame = new PlayerInputFrame { MoveY = 1f, SprintHeld = true };
            yield return PlayModeHarness.WaitGameSeconds(0.8f, PlayModeHarness.DefaultTimeoutSeconds, "aiming walk");
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.RodAiming));
            float aimSpeed = new Vector2(locomotion.Velocity.x, locomotion.Velocity.z).magnitude;
            Assert.That(aimSpeed, Is.EqualTo(MovementMath.WalkSpeed * MovementMath.AimSpeedMult).Within(0.3f), "aiming walk speed is x0.6 and never sprint");
        }

        // Catches: the first-source rule (a Free request must not shadow a later request, an earlier request wins), a disabled source still
        // counting, and the arbiter precedence being bypassed (a request must not override Swimming/Climbing; covered in EditMode).
        [UnityTest]
        public IEnumerator Locomotion_FirstEnabledNonFreeSourceWins_DisabledIgnored()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            FakeInputService fake = InstallFakeInput();
            PlayerLocomotion locomotion = MakeLocomotionRig(3, out TestSource[] s);
            yield return Settle(locomotion, fake);

            s[1].Requested = InputContext.RodWaiting;
            yield return null;
            yield return null;
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.RodWaiting), "a Free first source does not shadow the second");

            s[0].Requested = InputContext.RodReady;
            yield return null;
            yield return null;
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.RodReady), "the first non-Free source wins");

            s[0].enabled = false;
            yield return null;
            yield return null;
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.RodWaiting), "a disabled source is ignored");

            s[2].Requested = InputContext.RodFighting;
            s[1].enabled = false;
            s[2].enabled = false;
            yield return null;
            yield return null;
            Assert.That(fake.GameplayContext, Is.EqualTo(InputContext.Free), "no enabled source requests: Free");

            // A disabled source that LOCKS movement must not lock.
            s[2].Requested = InputContext.Free;
            s[2].Locks = true;
            Vector3 start = locomotion.transform.position;
            fake.Frame = new PlayerInputFrame { MoveY = 1f };
            yield return PlayModeHarness.WaitGameSeconds(0.5f, PlayModeHarness.DefaultTimeoutSeconds, "walk with a disabled locking source");
            Assert.That(Flat(start, locomotion.transform.position), Is.GreaterThan(1f), "a disabled locking source does not lock");
        }

        // Catches: Teleported not raised, raised for a missing CharacterController path, or raised BEFORE the position is applied
        // (the FishingRodController cancels from this event and reads the new pose).
        [UnityTest]
        public IEnumerator Locomotion_ApplyTeleport_RaisesTeleportedAfterThePositionIsApplied()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            FakeInputService fake = InstallFakeInput();
            PlayerLocomotion locomotion = MakeLocomotionRig(0, out _);
            yield return Settle(locomotion, fake);

            var target = new Vector3(RigOrigin.x + 5f, RigOrigin.y, RigOrigin.z + 5f);
            int raised = 0;
            Vector3 seenAt = Vector3.zero;
            float seenYaw = -1f;
            locomotion.Teleported += () =>
            {
                raised++;
                seenAt = locomotion.transform.position;
                seenYaw = locomotion.transform.eulerAngles.y;
            };
            locomotion.ApplyTeleport(target, 90f);
            Assert.That(raised, Is.EqualTo(1), "raised exactly once");
            Assert.That(Vector3.Distance(seenAt, target), Is.LessThan(0.05f), "the event sees the new position");
            Assert.That(Mathf.DeltaAngle(seenYaw, 90f), Is.EqualTo(0f).Within(1f), "the event sees the new yaw");
        }

        // ---- camera ----

        // Catches: no additive kick, kick not decaying to 0, stacking replaced instead of added, cameraMotion not scaling it (0 = no kick),
        // and the clamp to [1, 179] missing.
        [UnityTest]
        public IEnumerator Camera_AddFovKick_AddsDecaysScalesWithCameraMotionAndClamps()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            InstallFakeInput();
            ISettings settings = ServiceRoots.App.Get<ISettings>();
            float originalMotion = settings.Current.cameraMotion;
            try
            {
                var root = new GameObject("TestCameraRig");
                _made.Add(root);
                root.SetActive(false);
                root.transform.position = RigOrigin;
                Transform pivot = new GameObject("HeadPivot").transform;
                pivot.SetParent(root.transform, false);
                Transform shake = new GameObject("Shake").transform;
                shake.SetParent(pivot, false);
                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(shake, false);
                UnityEngine.Camera cam = camGo.AddComponent<UnityEngine.Camera>();
                root.AddComponent<PlayerCamera>();
                root.SetActive(true);
                Assert.That(pivot.parent, Is.SameAs(root.transform));
                Assert.That(root.transform.Find(PlayerRigPaths.Camera), Is.SameAs(camGo.transform), "rig matches PlayerRigPaths");

                settings.Current.cameraMotion = 1f;
                yield return null;
                yield return null;
                float baseFov = cam.fieldOfView;

                var kicker = root.GetComponent<PlayerCamera>();
                kicker.AddFovKick(10f, 0.5f);
                yield return null;
                float first = cam.fieldOfView - baseFov;
                Assert.That(first, Is.InRange(4f, 10.01f), "a kick shows on the next frame (was " + first.ToString("F2") + ")");
                yield return PlayModeHarness.WaitGameSeconds(0.8f, PlayModeHarness.DefaultTimeoutSeconds, "decay");
                Assert.That(cam.fieldOfView - baseFov, Is.EqualTo(0f).Within(0.01f), "the kick decays back to 0");

                kicker.AddFovKick(4f, 1f);
                kicker.AddFovKick(4f, 1f);
                yield return null;
                Assert.That(cam.fieldOfView - baseFov, Is.GreaterThan(6f), "two kicks add up (4 + 4)");
                yield return PlayModeHarness.WaitGameSeconds(1.2f, PlayModeHarness.DefaultTimeoutSeconds, "decay");

                kicker.AddFovKick(-6f, 0.5f);
                yield return null;
                Assert.That(cam.fieldOfView - baseFov, Is.LessThan(-2f), "a negative kick narrows the view");
                yield return PlayModeHarness.WaitGameSeconds(0.8f, PlayModeHarness.DefaultTimeoutSeconds, "decay");

                settings.Current.cameraMotion = 0.5f;
                kicker.AddFovKick(10f, 1f);
                yield return null;
                float half = cam.fieldOfView - baseFov;
                Assert.That(half, Is.InRange(2.5f, 5.01f), "cameraMotion 0.5 halves the kick (was " + half.ToString("F2") + ")");
                yield return PlayModeHarness.WaitGameSeconds(1.2f, PlayModeHarness.DefaultTimeoutSeconds, "decay");

                settings.Current.cameraMotion = 0f;
                kicker.AddFovKick(10f, 1f);
                yield return null;
                Assert.That(cam.fieldOfView - baseFov, Is.EqualTo(0f).Within(0.001f), "cameraMotion 0 shows no kick");
                yield return PlayModeHarness.WaitGameSeconds(1.2f, PlayModeHarness.DefaultTimeoutSeconds, "decay");

                settings.Current.cameraMotion = 1f;
                kicker.AddFovKick(500f, 1f);
                yield return null;
                Assert.That(cam.fieldOfView, Is.InRange(178.9f, 179.001f), "clamped to 179");
                kicker.AddFovKick(-5000f, 1f);
                yield return null;
                Assert.That(cam.fieldOfView, Is.GreaterThanOrEqualTo(1f), "clamped to 1 at the low end");
            }
            finally
            {
                settings.Current.cameraMotion = originalMotion;
            }
        }

        // ---- PlayerNetState through the real solo host ----

        // Catches: ServerInitialized not raised, raised twice, raised before the host name/slot are written, a throwing subscriber breaking
        // ServerInit (EventIsolation), and IsServerInitialized not set before the event.
        [UnityTest]
        public IEnumerator PlayerNetState_ServerInitialized_RaisedOnceAfterInit_AndThrowingSubscriberIsIsolated()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            InstallFakeInput();

            int raised = 0;
            bool flagAtRaise = false;
            string nameAtRaise = null;
            byte slotAtRaise = 255;
            Action<PlayerNetState> onSpawned = p =>
            {
                p.ServerInitialized += _ => throw new InvalidOperationException("boom-server-initialized");   // first: must not stop the next
                p.ServerInitialized += q =>
                {
                    raised++;
                    flagAtRaise = q.IsServerInitialized;
                    nameAtRaise = q.DisplayName;
                    slotAtRaise = q.Slot;
                };
            };

            LogAssert.Expect(LogType.Exception, new Regex("boom-server-initialized"));
            PlayerNetState.Spawned += onSpawned;
            try
            {
                yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            }
            finally
            {
                PlayerNetState.Spawned -= onSpawned;
            }

            PlayerNetState player = PlayerNetState.Local;
            Assert.That(player, Is.Not.Null);
            Assert.That(raised, Is.EqualTo(1), "ServerInitialized is raised exactly once per ServerInit");
            Assert.That(flagAtRaise, Is.True, "IsServerInitialized is already true inside the event");
            Assert.That(slotAtRaise, Is.EqualTo(player.Slot), "NetSlot is written before the event");
            Assert.That(nameAtRaise, Is.Not.Null.And.Not.Empty, "the display name is set before the event");
            Assert.That(player.IsServerInitialized, Is.True);
        }

        // Catches (M-7): ServerInitialized raised before NetSlot is written (a subscriber reading Slot inside the event, as PlayerCreel's mirror
        // does, would mirror the wrong slot). Re-runs ServerInit with a different slot so a stale slot 0 cannot pass by accident.
        [UnityTest]
        public IEnumerator PlayerNetState_ServerInit_SlotIsAlreadyWrittenInsideTheEvent()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            PlayerNetState player = PlayerNetState.Local;
            Assert.That(player.Slot, Is.Not.EqualTo((byte)2), "precondition: the solo host is not already in slot 2");
            int raised = 0;
            int slotInEvent = -1;
            player.ServerInitialized += p =>
            {
                raised++;
                slotInEvent = p.Slot;
            };

            player.ServerInit(2, "x");

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(slotInEvent, Is.EqualTo(2), "Slot is already 2 inside ServerInitialized");
            Assert.That(player.Slot, Is.EqualTo((byte)2));
        }

        // Catches (M-2): ToolChanged raised for a no-op change (previous == current), which would replay the Equip/Stow cue on remotes.
        [UnityTest]
        public IEnumerator PlayerNetState_ToolChangedHandler_IgnoresEqualValues()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            PlayerNetState player = PlayerNetState.Local;
            var seen = new List<ToolKind>();
            player.ToolChanged += seen.Add;

            player.HandleToolChanged((byte)ToolKind.Rod, (byte)ToolKind.Rod);
            Assert.That(seen, Is.Empty, "equal previous/current raises nothing");

            player.HandleToolChanged((byte)ToolKind.None, (byte)ToolKind.Rod);
            Assert.That(seen, Is.EqualTo(new[] { ToolKind.Rod }), "a real change still raises");
        }

        // Catches (T16-R1): a player who takes over another identity's slot inheriting that slot's creel and debug coins; ALSO the opposite
        // bug, a same-identity rejoin (R46) losing its creel; ALSO clearing every slot instead of only the taken-over one.
        // Catches (takeover follow-up): FishingHost not hearing SessionState.SlotTakenOver, so the newcomer keeps the old occupant's rod, bait,
        // bait stock and pity streak; the NetRodIndex / NetSelectedBait mirror not refreshed on the spawned rod; a SAME-identity rejoin
        // wrongly resetting the fishing loadout (R46 covers it too).
        [UnityTest]
        public IEnumerator SessionState_SlotTakeover_ClearsCreelForNewIdentity_KeepsItForSameIdentity()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            SessionState session = SessionState.Current;
            Assert.That(session, Is.Not.Null);
            CreelLedger ledger = session.ServerCreels;
            Assert.That(ledger, Is.Not.Null, "the solo host owns the ledger");
            byte slot = PlayerNetState.Local.Slot;
            byte other = (byte)((slot + 1) % NetworkConstants.MaxPlayers);
            Assert.That(session.TryGetKey(Unity.Netcode.NetworkManager.ServerClientId, out PlayerKey hostKey), Is.True);

            var item = new CreelItem { ItemIndex = 1, WeightG = 100, ValueCoins = 5, Quality = CatchQuality.Normal };
            ledger.Add(slot, item);
            ledger.SellAll(slot);          // wallet = 5
            ledger.Add(slot, item);        // one item in the creel
            ledger.Add(other, item);       // a neighbour that must never be touched

            // Host fishing state for the slot: a non-default rod and bait (mirrored to the rod), and three failed casts by a stand-in
            // client (stock 20 -> 17, FailStreak 3 = the next cast would be Pity).
            FishingHost fishingHost = FishingHost.Current;
            Assert.That(fishingHost, Is.Not.Null, "the Fishing scene root must be present");
            FishingHostLedger fishing = fishingHost.Ledger;
            Assert.That(fishing, Is.Not.Null, "the host ledger exists once the local player is ready");
            FishingRodNetwork rodNet = PlayerNetState.Local.GetComponent<FishingRodNetwork>();
            Assert.That(ServiceRoots.App.TryGet(out ContentRegistry registry), Is.True);
            PlayerLoadout fresh = fishing.GetLoadout(slot);
            Assert.That(fresh.BaitCount, Is.EqualTo(FishingConstants.DebugBaitStock), "precondition: a fresh slot loadout");
            ushort otherRod = (ushort)((fresh.RodIndex + 1) % registry.Fishing.Rods.Count);
            byte otherBait = (byte)((fresh.BaitIndex + 1) % registry.Fishing.Baits.Count);
            const ulong standIn = 9001;
            var water = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 3f };
            var view = new HostCastView { CastingAllowed = true, Probe = water };
            RollOverrides none = RollOverrides.None;
            var report = new FightReport();
            var emptyCreel = new CreelSnapshot(0, 0, 12, 15000);
            for (uint id = 1; id <= 3; id++)
            {
                var req = new CastRequestData { CastId = id, ChargeNorm = 0.5f, BaitIndex = fresh.BaitIndex };
                Assert.That(fishing.RequestCast(standIn, slot, in req, in view, in none).Valid, Is.True, "stand-in cast " + id);
                CommitDecision miss = fishing.Result(standIn, id, FightOutcome.Escaped, EscapeReason.Missed, in report, in emptyCreel);
                Assert.That(miss.Outcome, Is.EqualTo(FightOutcome.Escaped));
            }
            fishingHost.HandleSelectRod(rodNet, otherRod);
            fishingHost.HandleSelectBait(rodNet, otherBait);
            Assert.That(rodNet.NetRodIndex.Value, Is.EqualTo(otherRod), "precondition: the rod mirror shows the selected rod");
            Assert.That(rodNet.NetSelectedBait.Value, Is.EqualTo(otherBait), "precondition: the bait mirror shows the selected bait");

            // Same identity rejoins its own slot: R46 keeps the creel and the wallet, and the fishing loadout too.
            Assert.That(session.Upsert(new ApprovedJoin(Unity.Netcode.NetworkManager.ServerClientId, hostKey, "Fisher", slot, true)), Is.True);
            Assert.That(ledger.Items(slot).Count, Is.EqualTo(1), "same identity keeps its creel");
            Assert.That(ledger.DebugCoins(slot), Is.EqualTo(5), "same identity keeps its debug coins");
            Assert.That(fishing.GetLoadout(slot).RodIndex, Is.EqualTo(otherRod), "same identity keeps its rod");
            Assert.That(fishing.GetLoadout(slot).BaitIndex, Is.EqualTo(otherBait), "same identity keeps its selected bait");
            Assert.That(rodNet.NetRodIndex.Value, Is.EqualTo(otherRod), "same identity: the mirror is not refreshed to defaults");

            // A different identity takes the slot over: fresh creel and wallet, fresh rod, bait, stock and pity streak.
            var newcomer = new PlayerKey(IdentityKind.Local, "someone-else");
            Assert.That(session.Upsert(new ApprovedJoin(Unity.Netcode.NetworkManager.ServerClientId, newcomer, "Other", slot, false)), Is.True);
            PlayerLoadout after = fishing.GetLoadout(slot);
            Assert.That(after.RodIndex, Is.EqualTo(fresh.RodIndex), "takeover: default rod");
            Assert.That(after.BaitIndex, Is.EqualTo(fresh.BaitIndex), "takeover: default bait");
            Assert.That(after.BaitCount, Is.EqualTo(FishingConstants.DebugBaitStock), "takeover: bait stock back to the starting loadout (was 17)");
            Assert.That(rodNet.NetRodIndex.Value, Is.EqualTo(fresh.RodIndex), "takeover: the spawned rod's NetRodIndex mirror is refreshed");
            Assert.That(rodNet.NetSelectedBait.Value, Is.EqualTo(fresh.BaitIndex), "takeover: NetSelectedBait is refreshed");
            Assert.That(rodNet.NetSelectedBaitCount.Value, Is.EqualTo(FishingConstants.DebugBaitStock), "takeover: NetSelectedBaitCount is refreshed");
            var next = new CastRequestData { CastId = 4, ChargeNorm = 0.5f, BaitIndex = fresh.BaitIndex };
            CastVerdict verdict = fishing.RequestCast(standIn, slot, in next, in view, in none);
            Assert.That(verdict.Valid, Is.True);
            Assert.That(verdict.Roll.Pity, Is.False, "takeover: the previous occupant's three failures do not make the next cast Pity");
            fishing.Disconnect(standIn);

            Assert.That(ledger.Items(slot), Is.Empty, "a different identity starts with an empty creel");
            Assert.That(ledger.DebugCoins(slot), Is.EqualTo(0), "a different identity starts with no debug coins");
            Assert.That(ledger.Items(other).Count, Is.EqualTo(1), "the neighbour slot is untouched");
        }

        // Catches: NetTool not replicated to Tool, ToolChanged missing on the writer, raised for the initial value, raised for an unchanged set.
        [UnityTest]
        public IEnumerator PlayerNetState_OwnerSetTool_RaisesToolChangedOncePerChange()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForMenu();
            InstallFakeInput();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            PlayerNetState player = PlayerNetState.Local;
            var seen = new List<ToolKind>();
            player.ToolChanged += seen.Add;
            Assert.That(player.Tool, Is.EqualTo(ToolKind.None), "initial tool is None");
            Assert.That(seen, Is.Empty, "no event for the initial value");

            player.OwnerSetTool(ToolKind.Rod);
            Assert.That(player.Tool, Is.EqualTo(ToolKind.Rod));
            Assert.That(player.NetTool.Value, Is.EqualTo((byte)ToolKind.Rod));
            player.OwnerSetTool(ToolKind.Rod);
            Assert.That(seen, Is.EqualTo(new[] { ToolKind.Rod }), "an unchanged set raises nothing");

            player.OwnerSetTool(ToolKind.Bucket);
            player.OwnerSetTool(ToolKind.None);
            Assert.That(seen, Is.EqualTo(new[] { ToolKind.Rod, ToolKind.Bucket, ToolKind.None }));
            Assert.That(player.Tool, Is.EqualTo(ToolKind.None));
        }
    }
}
