# harbour-village-states: village states and the river lock (Blender, cloud worker "Village")

Branch `harbour-village-states`, cut from `origin/harbour-art3`. Design 2026-10-17 sections 3.2 and 5.3. Blender 5.2.2 (Linux). No C#, scenes or prefabs. No downloaded assets (nothing procedural-from-photo was needed), so there is no `Art/Textures/Village` and no `SOURCES.md`. No new palette slot, no new texture layer.

## What was built

All of it lives in a new section at the end of `Art/Blender/fg_harbour.py` ("VILLAGE STATES"), the only edit to that file. Output: `Art/Export/Village/*.fbx` + `village_manifest.json`; previews in `Art/Previews/Village/`.

### The STATE option (env `FG_STATE`)

| `FG_STATE` | What runs |
|---|---|
| `restored` (default, unset) | `main()` exactly as before. Nothing of the new code runs. |
| `rundown` | `village_main()` only: builds, bakes, exports and renders the village models. It does not run `main()`, so `Art/Export/Harbour` is not touched. `FG_VILLAGE_PREVIEWS=0` skips the renders (10 s run). |
| `both` | `main()`, then `village_main()`. |

Command: `FG_STATE=rundown FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_harbour.py`

### Restored stays byte-identical (proof, same environment)

The only change to existing code is the last two lines of the file (the `__main__` block now dispatches on `STATE`). Proof, all in the cloud box with Blender 5.2.2:
1. Ran the ORIGINAL script (before my edit) on the committed tree: output hashed = H1 (112 files: the 109 `Art/Export/Harbour/*.fbx` and the 3 JSON files).
2. Put H1 back into `Art/Export/Harbour`, ran the EDITED script in the default state, hashed again = H2.
3. `sha256sum` H1 vs H2: **identical for all 112 files**; the pass-2 status line reports 101 `kept`, 0 `written` (phase-1 FBX are `kept` too).
4. A first attempt of that run crashed: my new helper `ladder` shadowed phase 1's `ladder(g, x0, x1, z0, z1, along_z)`. Renamed to `scaffold_ladder`; a script check (AST walk of every top-level name) shows no other name in the new section collides with an earlier one.
5. The `rundown` state leaves `git status` clean under `Art/Export/Harbour` (checked), and a second `rundown` run reports all 18 village FBX `kept` (deterministic).

**Environment note (important for the PC):** this Linux Blender 5.2.2 build does NOT reproduce the committed Harbour FBX bytes: running the unmodified script here re-wrote 92 of the 112 committed files (FBX serialisation differs from the PC's 5.2), which is why the proof compares before/after in the same environment. I restored the committed files with `git checkout`; this branch does not touch `Art/Export/Harbour`. On the PC, a default-state run must leave `git status` clean under `Art/Export/Harbour`; please confirm once.

### Models (triangles; every model is `M_Toon_Base`, no `_Tri`)

World models are authored in world coordinates: place each at the Unity origin, identity transform (like `Harbour_Core_Buildings`). Local models are placed with the manifest's `placements`.

| Model | Tris | Kind | Notes |
|---|---|---|---|
| `Harbour_Village_DockOffice_Rundown` | 3024 | world overlay | boarded window (one plank missing), peeling shutter hanging, door X-barred + chain + padlock, unopened mail, roof tarp + bricks + moss, weeds, paint flakes, sign "CLOSED - GONE FISHING (PERMANENTLY)" hung crooked over the name board |
| `Harbour_Village_BaitShop_Rundown` | 3766 | world overlay | boarded window and serving hatch, door locked up, torn awning strips, sign "CLOSED / THE WORMS HAVE UNIONISED", the A-frame board now reads DEAD WORMS (DEAD strip over LIVE), roof tarp, weeds |
| `Harbour_Village_NetShed_Rundown` | 2510 | world overlay | front barricaded (planks, padlocked board door), roof tarps, torn net, sign "CLOSED / NETS: SEE HOLES". **Adds a collider item `NetShedBarricade`** (see Unity steps) |
| `Harbour_Village_Smokehouse_Rundown` | 2960 | world overlay | door locked up, soot streaks, SMOKED FISH board painted over, sign "CLOSED - FIRE INSPECTOR WAS RIGHT" hung in front of the door, roof tarp, weeds |
| `Harbour_Village_Boatyard_Rundown` | 3122 | world overlay | the half-painted boat under a roped tarp, chained and padlocked, sign on two posts "BOATYARD CLOSED / BACK IN 5 MINUTES (2011)", kicked-over paint tin, weeds |
| `Harbour_Village_Chapel_Rundown` | 5062 | world overlay | tower door chained, six nave windows boarded, nave roof tarp, notice "CHAPEL CLOSED / THE ROOF IS NOT AN ACT OF GOD", weeds |
| `Harbour_Village_Scaffold_{DockOffice,BaitShop,NetShed,Smokehouse,Boatyard,Chapel}` | 3888 / 4440 / 2772 / 2144 / 1424 / 7344 | world kit | standards, ledgers, transoms, four boards a lift, toe boards, braces, a leaning ladder, hanging orange safety sheets, a plank stack; sized from each landmark's constants (two lifts on the cottages, five on the chapel tower); `lifts_y` and `mixer_placement` in the manifest |
| `Harbour_Village_Prop_CementMixer` | 1276 | local | one placement per scaffold (6, in the manifest) |
| `Harbour_Village_Lock_Broken` | 12602 | local | see below |
| `Harbour_Village_Lock_Repaired` | 9146 | local | all four leaves open in their recesses |
| `Harbour_Village_Lock_Repaired_Frame` | 5626 | local, optional | repaired lock without leaves, for animated gates |
| `Harbour_Village_Lock_GateLeaf` | 880 | local, optional | one repaired leaf, pivot at the hinge, runs along +x; `lock.hinges` gives 4 positions with open/closed yaw |
| `Harbour_Village_LockKeeperHut` | 6730 | local | wind-up chair, grandfather clock, 26 clocks none agreeing |

Total 78716 triangles in 18 models (the lock and the hut are the heavy ones; nothing near the 42k cap).

### Run-down overlays are additive, nothing is hidden

Every overlay sits on the restored mesh. Signs and doors are COVERED by bigger boards, not removed, so `hide_in_restored_mesh` is `[]` in every manifest entry and the Unity builder needs no split or hide. Restored meshes the overlays sit on: `Harbour_Core_Buildings` (office, bait shop, net shed, smokehouse), `Harbour_Core_Yard_B` (boat on sawhorses), `Harbour_Core_Buildings_Upper` (chapel). Their geometry was not changed (proved above).

### The river lock

Local frame: origin at the chamber centre on the water line (y 0 = the water surface), +z downstream (toward the harbour), x across. 9.0 m clear (8.68 m between the 0.16 m fender timbers), 24 m between the gates, floor at y -2.3, walls to y +2.2, outer walls at x +-6.7, guide walls out to z +-24.6. Gates are mitre pairs that open OUTWARD into recesses in the walls, so open leaves narrow nothing and nothing is overhead.

- **Broken** (`passable: false`): downstream gate (z +12): the west leaf hangs open off one hinge (50 deg into the passage, drooping, beam snapped, 3 planks missing); the east leaf is shut. Upstream gate (z -12): both leaves shut, rotten, one plank missing each, chain and padlock. No crank on any winch (sheared stub, two bolts, a rope and a tag "CRANK BORROWED - DAVE" on one). Rust, moss, weeds, a traffic cone, a board "LOCK OUT OF ORDER SINCE 2015" with a second notice "NO, REALLY".
- **Repaired** (`passable: true`): new painted leaves open, new winches with ratchet wheels and cranks, board "LOCK OPEN (FOR NOW)".
- **Tug fit** (measured from the real `Harbour_Core_CrewBoat` vertices, in `village_manifest.json` `tug_fit` and rendered in `Village_Lock_TugFit.png`): visual beam 5.76 m (with the tyres), length 16.2 m, keel -1.49, top +8.67. Clear water between fenders 8.58 m (half 4.288 measured), so 1.41 m each side; keel clearance 0.81 m; chamber 24 m for a 16.2 m boat. NOTE: the task text said about 15 m x 4 m; the real constants are `TUG_L0..TUG_L1` = 15.5 m, `TUG_B` = 5.0 m, and the visual (fenders, tyres) is 16.2 x 5.76. The lock was sized to the real numbers.
- **Where**: `LOCK_WORLD` = (-80.2, 0, -50.0), yaw 0 (on the river axis: `RIVER` runs x -79..-80.6 at z -36..-52; `CHANNEL_MARKERS` G (-72.5,-36.5) / R (-87.5,-34.5) sit just outside the outer faces at x -73.5 / -86.9; downstream gate z -38, upstream z -62). The river is 8.4-15 m wide there, so the Unity builder has to carve the banks (or the phase-B terrain must) to the lock's footprint; only the placement position changes when phase B moves the west wall to x -100.
- **Keeper's hut**: 3.8 x 3.0 m, door and porch toward +z; suggested placement `HUT_WORLD` = (-69.0, 0.2, -47.5) yaw -90 (door toward the lock, east bank); snap its y to the terrain.

### Jokes (one sign per building, as asked)
Office "CLOSED - GONE FISHING (PERMANENTLY)"; bait shop "THE WORMS HAVE UNIONISED" (+ DEAD WORMS); net shed "NETS: SEE HOLES"; smokehouse "FIRE INSPECTOR WAS RIGHT"; boatyard "BACK IN 5 MINUTES (2011)"; chapel "THE ROOF IS NOT AN ACT OF GOD"; lock "OUT OF ORDER SINCE 2015 / NO, REALLY / CRANK BORROWED - DAVE" and "OPEN (FOR NOW)"; hut "THE TIME IS PROBABLY" and "WIND ME".

## Previews (`Art/Previews/Village/`, Eevee, 1280x720, the harbour's toon preview look)
Landmarks (left RUN-DOWN overlay on the restored mesh, right RESTORED): `Village_{DockOffice,BaitShop,NetShed,Smokehouse,Boatyard,Chapel}_Rundown_vs_Restored.png`. Scaffold kits on their buildings with the mixers: `Village_Scaffold_{West,East,Chapel}.png`. The lock: `Village_Lock_Broken_vs_Repaired.png` (left broken, right repaired), `Village_Lock_Broken.png`, `Village_Lock_Repaired.png`, `Village_Lock_TugFit.png` (the real tug in it). `Village_LockKeeperHut.png`. 14 images where the brief says 2-4: the task asked for one per landmark plus the lock in both states.

## Unity steps (the PC must do)
1. Import `Art/Export/Village/*.fbx` (LFS). Same postprocessor as the harbour FBX; no `_Tri`, so all use `M_Toon_Base`. Meshes carry `Col` (AO) and `Layer` uv1; nothing new for the shader.
2. Run-down state: instantiate `Harbour_Village_<Landmark>_Rundown` at the origin on top of the restored mesh while the landmark is run-down; deactivate it once restored. Being restored: show `Harbour_Village_Scaffold_<Landmark>` (+ one `Harbour_Village_Prop_CementMixer` at that entry's `mixer_placement`) while the work is under way.
3. Colliders: the overlays and scaffolds carry no collider ("collider": "none" items) except `NetShedBarricade` (item in `Harbour_Village_NetShed_Rundown`): add a thin BoxCollider from its `bounds_min/max` while it is run-down, since the restored NetShed collider only covers the back half. Scaffold decks are walkable in the fiction but have no collider here: add decks from `lifts_y` if the player is meant to climb.
4. The smokehouse chimney is still a smoke point in `harbour_manifest.json`: do not emit smoke while the smokehouse is run-down.
5. Lock: place `Harbour_Village_Lock_Broken` / `_Repaired` at `lock.suggested_world` (origin on the water plane); carve the banks. Swap by state. For moving gates use `_Repaired_Frame` + 4 x `_GateLeaf` at `lock.hinges` (yaw about +y: closed leaves meet in a mitre, open leaves lie in the recesses). The lock models are meshes for water-side use: the water plane must pass through y 0 of the model.
6. Run a default-state generator on the PC and confirm `git status` is clean under `Art/Export/Harbour` (see the environment note above).

## Unverified / open
- Not seen in Unity: only Blender/Eevee previews with the harbour's toon look. The texture layers (Wood, StoneWall, Cloth, Rock) show only as palette colours in the previews.
- Whether the lock's real position works with the phase-B terrain (not built yet); the banks must be carved to it.
- Tris are a little above prop density for the lock (9-13k); the leaf and winch counts can be cut if it is too heavy for the lagoon view.
- Asset Store: nothing here needed it (a Unity free lock or scaffolding pack would only replace the scaffold/cranks; not checked since the packs are not reachable from the cloud).
