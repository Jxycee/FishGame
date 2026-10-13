# RV There Yet? - how ladders work, and a FishGame ladder design to match

Game-design reference only (user-approved 2026-10-11). Mechanics and numbers, paraphrased; no code or assets copied.
Tags: [V] verified in Blueprint bytecode / pak file list, [I] inferred (UE engine behaviour or absence-of-evidence), [U] unverified / not recoverable.
Source dumps (scratch): `...\scratchpad\re\rv\tool\dumps\` BP_Ladder_01, BP_LadderOutside_01, BP_FirstPersonCharacter (Move, K2_OnMovementModeChanged, IA_Jump handler),
BP_Interactable_BridgingLadder, BP_Interactable_Actor_Rideable_JungleVine. Pak search for Ladder/Climb/Mantle/Vault/Rung/IA_Climb found nothing else.

## 1. What RV actually has (three different things named ladder/climb)

1. **Climbable ladders (the real one)**: two Blueprints, `Interactables/TruckParts/BP_Ladder_01` (child actor "Ladder" on the landing-craft boat BP_Static_Boat
   - so it is the water-to-deck boarding ladder [V]) and `BP_LadderOutside_01` (the RV exterior ladder, base class = truck part) [V]. Meshes SM_TruckLadder_01
   (vehicle) and SM_WoodenLadder_01 (cave, dressing). Both Blueprints are identical in logic.
2. **BridgingLadder**: a carried, physics-prop ladder with 3 extension levels (0..2), primary/secondary fire change the level, replicated `Level` (push model +
   RepNotify) via a server RPC on the controller. NOT climbable by script; it is a bridge item [V].
3. **JungleVine**: a rideable swinging cable. Player attaches (sits), IA_Move up/down steps a "grab index" along the cable segments (0.2 s step gate), steep look
   up/down (|dot(forward, up)| > 0.9) decides step direction, server RPC `Server_JungleVine_Climb`, unmount launches the player. Different system; ignore for ladders [V].

## 2. The ladder, exactly [V unless tagged]

**It is a trigger box and a movement-mode switch. Nothing else.**
- Component: one BoxComponent (overlap) + a static mesh + the base interactable's significance component. No tick, no replicated variables, no RPC, no timer.
- BeginOverlap, other actor casts to BP_FirstPersonCharacter: cache the character's `BrakingDecelerationFlying`, `SetMovementMode(5 = Flying)`,
  set `BrakingDecelerationFlying = 10000`. Runs on whichever machine has the overlap (all of them; the overlap is local) [I: owner predicts, server validates].
- EndOverlap, same cast: restore `BrakingDecelerationFlying` from the cache; **if** the mode is still Flying, `SetMovementMode(3 = Falling)`. (Mode ids: 1 Walking,
  3 Falling, 4 Swimming, 5 Flying.) Leaving the box mid-ladder therefore means you fall from where you are. In water the engine then flips Falling to Swimming [I].
- **No interact**: `Interact` returns false; `GetLookAtPlayerInteractions` returns an empty map, so there is no E prompt and no look+press step. The ladder is
  not "used", you walk into it. [V]
- **No snap, no lerp, no mantle/vault, no dismount animation, no top/bottom special case**. [V by absence: no ladder asset has any such logic]
- Entry works from any direction and from any mode, including Swimming (the boat ladder is the way out of the water) [V code: no mode check; I: swim case].
- Entry from the top works too: step into the box from the deck, you start flying; look down and walk to descend [I].

**Movement while on it** (BP_FirstPersonCharacter `Move`, branch on MovementMode):
- Walking / Falling / Custom: W/S along the actor's forward, A/D along actor right (flat).
- **Swimming and Flying: W/S along the CAMERA forward vector (pitch included), A/D still strafe along actor right** (the camera branch jumps back into the strafe
  code) [V]. So: look up + W = climb up; look down + W = climb down; S reverses; A/D slide sideways along the rungs. No separate up/down keys.
- Look is not restricted in any mode (no ladder-specific look code) [V by absence]. Camera is fully free.
- Gravity does not act in Flying; with no input you hang in place. Braking 10000 cm/s^2 makes release stop almost instantly (no drift) [V number; I behaviour].
- Speed: not set in script anywhere (no MaxFlySpeed write). Stock UE default is 600 cm/s (6 m/s); the real value lives in the unversioned CMC defaults of the
  custom `RGCharacterMovementComponent` and is **[U]**. Sprint only changes MaxWalkSpeed, so it has no effect on the ladder [I].
- Jump: the IA_Jump handler just calls Jump(). Stock `CanAttemptJump` needs Walking or Falling, so jumping does nothing while Flying [I]; a native override in the
  RG CMC subclass is possible [U]. There is no jump-off code. To get off you move out of the box (strafe sideways, back away, or past the top/bottom).

**Box size [U/I]**: the ladder Box export's first three doubles are 49.06 / 60.29 / 191.0 cm (same numbers in both BPs); they are either the box location offset or
half-extents. If half-extents: about 1.0 x 1.2 x 3.8 m, i.e. the whole ladder height plus margin. Treat as "a box covering the ladder, taller than the player".

**Held items**: no held-item check anywhere in the ladder or in Move. Items stay in hand; nothing stops using or dropping them [V by absence]. (Pickup/attach code is
independent of movement mode.)
**Animation / first-person hands / sound**: no climb animation asset, no ladder or rung audio asset exists (name search over the 19k-file pak list) [V]. Footsteps are
floor-material driven and do not run in Flying [I]. So RV's ladder looks and sounds like floating up an invisible slope; rungs are pure collision/dressing.
**Damage**: no damage on the ladder. Falling out of the box mid-ladder is ordinary falling: fall damage only above 12.5 m/s downward (research-rv.md) [V].
**Networking**: no ladder state is replicated. Character movement mode rides the normal CMC prediction; the box fires on every machine [I]. Authority is the
owning client with server correction (UE default); the ladder actor owns nothing [V].
**[public knowledge, low confidence]**: players describe the RV-roof ladder and the boat ladder as "walk into it and look where you want to go", which matches the
bytecode. I found no public numbers (speed, box size).

## 3. Mapping to FishGame today (code read)

- `PlayerLocomotion.TryBeginClimb`: only while `inWater`, inside a `WaterExit` box, and only on Jump/Interact or W while facing the exit's Top; then a 0.4 s
  `Vector3.Lerp` (SmoothStep) to Top with `_cc.enabled=false` (`MovementMath.ClimbDuration`), `IsClimbing`, Climbing context. This is the "auto snap you onto them and up".
- `InputContextRouter.RouteClimbing` zeroes everything except Look and Pause; `RouteSwimming` passes Interact "for climb at exits"; `InteractLive` excludes Swimming and
  Climbing so the bell at (1.1, 0.6, -40.6) cannot ring from a ladder. `GameplayContextArbiter`: Climbing > Swimming > request > Free (unchanged by this design).
- Dock: `LakeDockBuilder.AddExits` makes `Exits/LadderL|LadderR|Ramp` trigger boxes + child "Top". Ladder exit box: center (+-2.1, -0.8, -41.5), size (1.2, 2.6, 1.2) =
  x 1.5..2.7, y -2.1..0.5 (below deck top 0.6!), z -42.1..-40.9. Ladder collider is a thin box at x +-1.58 (0.06 thick, 0.5 wide) from bed to deck top; rungs
  every 0.3 m (LakeDockMeshes). Deck x +-1.5, top y 0.6; bed under the ladder about -1.27.
- Spec 7.3 "Water: swimming and exits", 8.2 table (Swimming: Interact = climb at exits; there is no Climbing row), 7.8 #3 (climb out within 2 s), contract D36, DA7.
- `FishingRodController.HandleSwim` stows the rod on IsSwimming and re-equips when it clears; `RodFrame.InWater` ends a fight as Dropped.

## 4. Proposed FishGame ladder design (RV-equivalent)  [PROPOSED - spec amendments need user approval]

**States**: Free/Rod* (land), Swimming, **Climbing** (ladder). Climbing is no longer a timed lerp; it lasts exactly as long as the capsule centre is inside a
LadderVolume. Precedence unchanged (Climbing > Swimming > request > Free).

**Enter (owner, every Update in PlayerLocomotion, before the swim/ground branch)**: not paused, not `MovementLocked` (RodFighting; keeps today's rule), not already
Climbing, and `LadderVolume.FindContaining(pos + up*0.9)` != null. From any of: walking, falling, wading, swimming, from the deck edge or the water. No button.
On enter: `ResetMotion()`, `IsClimbing=true`, `IsSwimming=false` (+ `OwnerSetSwimming(false)`), `IsGrounded=false`. No snap, no rotation change.
**While climbing**: CharacterController stays ENABLED (collides with ladder/deck/posts, slides along them). Each frame:
  v = ( camForward(yaw,pitch) * MoveY + transform.right * MoveX ), length clamped to 1, times `ClimbSpeed`; `_cc.Move(v*dt)`. No gravity, no buoyancy, no accel ramp
  (stop instantly on release = RV braking 10000). Camera/look fully free (pitch limit stays 85). Pitch comes from `PlayerCamera.PitchDeg` (last frame's value is fine).
  Proposed `ClimbSpeed = 2.4 m/s` [tunable; RV value unknown]. Vertical speed = 2.4 * sin(pitch): 30 deg 1.2 m/s, 45 deg 1.7, 60 deg 2.1, 85 deg 2.4.
  Sprint, Jump, Interact, rod/primary/secondary, shove, drop, equip: zeroed (RV: Jump is a no-op, Interact is nothing). Pause and Look pass.
**Exit**: capsule centre leaves the volume (strafe off the side, back away, climb past the top, descend past the bottom) -> `IsClimbing=false`, `_vy=0`, next frame is
  normal: grounded on the deck, falling onto it a few cm, or swimming (in a WaterVolume below surface - 1.2) - exactly RV's Flying -> Falling. No mantle, no dismount lerp.
  `ApplyTeleport` and `OnDisable` clear it (already do). Mid-ladder drop = ordinary fall; FishGame has no fall damage, so it is harmless (lake = splash).
**From land / from water**: identical rule. Water: swim into the box, look at the deck, hold W. Land: walk off the deck edge into the box, look down, hold W to descend
  (faithful to RV; the box inner face sits 0.05 m beyond the deck edge so edge-walking does not trigger it).
**Rod / held items**: RV keeps items. FishGame recommendation: generalise `HandleSwim` to `away = IsSwimming || IsClimbing` (stow on enter via ForceEnd, remember, re-equip
  once both are false). Reasons: Climbing zeroes Primary/Secondary anyway, and without this the rod would pop out mid-ladder when IsSwimming clears on entry. Entry while
  RodWaiting therefore stows (line retrieved). Entry while RodFighting stays blocked. Alternative (pure RV): leave the rod equipped but inert - then line-out rods hang
  over the dock; not recommended.
**Interact**: nothing on a ladder (as RV). `RouteSwimming` stops passing Interact (E-to-climb is gone); `InteractLive` unchanged (false for Swimming and Climbing) so the bell
  cannot ring from the ladder. `PlayerInteractor` order attribute becomes defensive only.
**Sound/visual (FishGame extension, RV has none)**: optional rung tick every 0.3 m travelled (matches the rung mesh), procedural SfxRecipe; optional owner-write
  `NetClimbing` bool for a remote arms-up pose. Not needed for parity.
**Network**: owner-write, like all locomotion. Movement replicated by the existing owner-authoritative NetworkTransform; no server logic, no RPC, no ladder state.
  `NetSwimming` is written false on entry. Optional `NetClimbing` is the only possible new replicated field.

**Geometry (LakeDock)**: two `LadderVolume` triggers replace the LadderL/LadderR WaterExit boxes; the Ramp keeps its `WaterExit` + 0.4 s lerp (it is a walkable slope).
  Per ladder (mirror in x): x 1.55..2.55 (centre 2.05, size 1.0), z -42.0..-41.0 (size 1.0), y -1.4..1.6 (centre 0.1, size 3.0): bottom just under the local bed, top 1.0 m
  above the deck so feet can clear the deck lip before the box ends. Climb-out rise from swim root (-1.47, in practice -1.27 on the bed) to deck feet 0.6 is ~1.9-2.1 m.
  Known RV quirk kept: climbing straight out of the top of the box drops you back in and re-enters (a small bob); optional fix = 0.25 s re-entry cooldown after
  leaving through the top. Looking up while pressed against the rail and holding W is the fastest route; holding W while looking level goes nowhere (slides on the rail).

## 5. What changes in our code / spec / tests

Code:
- `Core/Input/InputContextRouter.cs`: `RouteClimbing` passes MoveX/MoveY (+Look, Pause); `RouteSwimming` zeroes Interact (update comments).
- `Core/Player/MovementMath.cs`: add `ClimbSpeed` and a pure `ClimbWish(yawRad, pitchDeg, moveX, moveY)` (floats, no UnityEngine) for EditMode tests; `ClimbDuration` stays
  for the ramp only.
- `Gameplay/Player/PlayerLocomotion.cs`: replace TryBeginClimb/BeginClimb/UpdateClimb (lerp, `_climbFrom/_climbTo/_climbT`) with volume-driven enter/exit + `_cc.Move`
  climb; keep CC enabled; ramp WaterExit path stays as a separate small lerp; read `PlayerCamera.PitchDeg`.
- `Gameplay/World/LadderVolume.cs` (new, like WaterExit: static list, `FindContaining(point)`); `WaterExit` keeps the ramp only.
- `Editor/Builders/LakeDockBuilder.cs`: `AddExits` -> ramp exit + two ladder volumes (names, roots and the T28 physics fingerprint "3 exits" change).
- `Fishing/Owner/FishingRodController.HandleSwim`: include IsClimbing (above).
Tests: `InputContextRouterTests` (Climbing row passes Move; Swimming no longer passes Interact), `SwimInteractGateTests` (rewrite: swim into LadderR volume, climbs with
no press, bell not rung), `PlayerUpdateOrderTests` (rationale comment), `PlayerHooksTests` (jump/climb gating wording), new MovementMath ClimbWish tests, physics
fingerprint test, rod stow-while-climbing test, M2 acceptance bot "climb out" step (must look up).
Spec (propose to user, do not decide): 7.3 "Water" exits bullet (ladders: walk into the volume; ramp keeps Jump/forward lerp); 8.2 Swimming cell "climb at exits" -> "-",
new `Climbing` row (move = climb along look direction, everything else "-"); 7.8 #3 reword "within 2 s of reaching a ladder while looking at the deck" (at 30 deg pitch
the rise takes ~1.8 s at 2.4 m/s; raise ClimbSpeed or reword); contract D36 and DA7 (forward-climb-needs-facing-Top) retire for ladders.
