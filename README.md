# FishGame

Co-op (1-4 players) friend-slop rogue-lite fishing game. Unity 6000.6.5f1 (URP), Netcode for GameObjects 2.13.3, Unity Multiplayer Services 2.3.3 (Relay + Lobby join codes).

- Design spec (source of truth): `docs/superpowers/specs/2026-10-09-fishgame-design.md`
- M1 manual acceptance checklist: `docs/superpowers/plans/2026-10-09-m1-acceptance.md`
- M2 manual acceptance checklist: `docs/superpowers/plans/2026-10-11-m2-acceptance.md`

## Build and test from the command line

Every command-line run goes through `Tools\unity.ps1`. It needs an absolute output directory **outside the repo** for logs and test results. Pass it as `-OutDir`, or set `FG_OUT` first:

```powershell
$env:FG_OUT = 'C:\Temp\fishgame-out'
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 check
```

| Command | What it does |
|---|---|
| `compile` | Opens the project in batchmode and checks for compile errors. |
| `build` | Runs `FishGame.EditorTools.ProjectBuilder.BuildAll` (rebuilds generated scenes, prefabs and settings, then validates). |
| `test-edit` | Runs the EditMode tests (`-Filter <regex>` narrows the run). |
| `test-play` | Runs the PlayMode tests. |
| `check` | `build`, then `test-edit`, then `test-play`. Stops at the first failure. |

Optional parameters: `-OutDir <abs dir>`, `-Filter <regex>`, `-TimeoutMinutes <int>` (default 5).

The last line of each run is an `FG RESULT ...` line, for example `FG RESULT check PASS`. Exit codes: 0 pass, 1 fail, 2 locked, 3 timeout, 4 usage error.

**Close the Unity editor before any command-line run.** If the editor holds `Temp/UnityLockfile`, the script prints `FG RESULT <command> LOCKED (close the Unity editor, then rerun)` and exits 2. Never kill Unity to free the lock.

### Editor-open mode

If you keep the editor open, use the menu instead:

- `FishGame > Build All`: rebuilds every generated scene, prefab and setting, then validates.
- `FishGame > Validate`: runs the validators only.
- `FishGame > Run EditMode Tests` / `FishGame > Run PlayMode Tests`: results go to `Logs/FishGame/editmode.xml` / `Logs/FishGame/playmode.xml`.
- Every compile writes `Logs/FishGame/compile.txt` (first line `FG COMPILE PASS|FAIL ...`).

Pressing Play from any scene starts from `Bootstrap`. `FishGame > Dev > Auto Solo` (a checkable menu item, main editor only) starts Solo automatically when you press Play.

## Local multiplayer testing

M1 and M2 multiplayer are tested on one machine with **Multiplayer Play Mode (MPPM) 3.0**, using **dev-direct** connections on `127.0.0.1:7777`. Dev-direct needs no Unity Cloud link and no internet.

### 1. Set up a scenario

1. Turn **off** `FishGame > Dev > Auto Solo`. A Solo host refuses every join.
2. Open `Window > Play Mode > Scenarios`. Press **+** to create a scenario, for example `Direct 4P`. If MPPM saves the scenario as an asset under `Assets/`, keep it inside `Assets/_Project/Content/Dev/`: invariant 4.7 #7 allows nothing else under `Assets/` (only `_Project` and `Settings`), so `check` fails on any other location.
3. Keep the **Main Editor** instance. Under **Additional Editor Instances**, press **+** for each extra player: up to 3 additional editor instances, so 4 players in total.
4. Leave **Multiplayer Role** at its default. Select the scenario and press Play. MPPM starts every instance.
5. In an additional instance, the **Layout** dropdown (top right of its window) adds the Hierarchy, Inspector and Console panels. Hierarchy and Inspector work only in Play mode.

Each MPPM instance gets its own settings file (`settings_<instance name>.json` under `Application.persistentDataPath`) and so its own player identity and display name. Give each instance a different display name on the main menu, so the nameplates are easy to tell apart.

### 2. Connect with the Direct buttons

The main menu in the editor and in development builds has two dev-only buttons:

1. On the host instance (usually the Main Editor), press **Host Direct**. The host listens on `127.0.0.1:7777` only, with no port retry.
2. On each other instance, press **Join 127.0.0.1**.

If a join attempt times out because the host was not up yet, press **Join 127.0.0.1** again.

### 3. Optional: start automatically with tags or command-line flags

Dev builds (the editor and development players) read these flags from the command line. In MPPM, the `-dev-*` flags in the table below, except `-dev-profile`, can also come from the instance's **Tag**:

1. Create the tag under `Edit > Project Settings > Multiplayer > PlayMode > Player Tags` (saved in `ProjectSettings/VirtualProjectsConfig.json`).
2. Assign it in the Play Mode Scenarios window, in the **Tag** dropdown of that instance. MPPM allows one tag per instance.
3. Write the flags as the tag's text. The game splits each tag on whitespace, so one tag can hold a flag and its value, such as `-dev-join-direct 127.0.0.1:7777`. If the tag editor refuses spaces or a colon, use the buttons instead.

| Flag | Effect |
|---|---|
| `-dev-solo` | Start Solo on boot. |
| `-dev-host-direct` | Start a dev-direct host on boot (`127.0.0.1:7777`). |
| `-dev-join-direct <ip[:port]>` | Join a dev-direct host on boot. The port defaults to 7777. |
| `-dev-protocol-offset <N>` | Adds N to the protocol version that this instance sends. `1` tests the version-mismatch refusal. |
| `-dev-long-timeout` | Raises the disconnect timeout from 10 s to 120 s, so a breakpoint on the host does not drop clients. Do not use it when you test disconnect detection. |
| `-dev-profile <name>` | Command line only (not read from tags). Uses its own settings file and player identity (`settings_<name>.json`). Give every standalone development player a different profile, or they share one identity and the second join is refused as a duplicate. MPPM instances don't need it: each already gets its own profile. |

Start order on boot: Solo, then Host Direct, then Join Direct. In standalone development players, pass the same flags on the command line, for example `FishGame.exe -dev-profile p2 -dev-join-direct 127.0.0.1:7777`. Starting the host first is no longer required for the player to appear: a client approved while the host is still loading the dock gets its player when its own load of that scene completes (unverified at runtime; the join itself still fails if the host is not listening yet).

### 4. Host-only dev toggle: Close joins / Open joins

In a dev-direct or online session, the host's Pause menu has a dev-only **Close joins** / **Open joins** toggle. While joins are closed, new joins are refused with "Run in progress. Join when the crew is in the Hub, at the Briefing or at Night Camp."

### 5. Latency with the Network Simulator

The Network Simulator comes from `com.unity.multiplayer.tools` 2.2.12, which is already in the manifest. Acceptance uses about 150 ms RTT and 2% loss, on the **client instances only**.

1. Once, create the preset in the Project window: create the folder `Assets/_Project/Content/Dev/`, then right-click it and choose `Create > Multiplayer > NetworkSimulatorPresetAsset`. Name it `NetSim_150ms_2pct`. Set **Packet delay** 75 ms, **Packet jitter** 0, **Packet loss interval** 0 and **Packet loss percent** 2. The transport's simulator stage delays that instance's packets in both directions, so 75 ms should give about 150 ms RTT. If you measure RTT (for example with `Add Component > Netcode > Runtime Network Stats Monitor`), adjust the delay until it reads about 150 ms. Committing this asset is optional.
2. In each client instance, enter Play mode and wait for the main menu. Before you join, open the Hierarchy, select `App` (under `DontDestroyOnLoad`) and use `Add Component > Netcode > Network Simulator`.
3. On that component, set **Connection Preset** to **Custom** and drag `NetSim_150ms_2pct` into **Custom Preset**. Then join.

Components added in Play mode are gone when Play mode ends. Never save the simulator into the `Bootstrap` scene: generated scenes are rebuilt (see below).

The M2 co-op fishing checks (acceptance doc, section B) use this same setup: host with **Host Direct**, clients with the simulator on, and `-dev-long-timeout` on a client you pause in the editor.

## Fishing (M2)

Fishing works in Solo and in co-op, on keyboard/mouse and on a gamepad (spec 8.3):

| Action | Keyboard/mouse | Gamepad |
|---|---|---|
| Equip or stow the rod | 1, mouse wheel | North |
| Ready the rod | Right mouse (hold) | Left trigger (hold) |
| Charge and cast | Left mouse (hold, release to cast) | Right trigger (hold, release to cast) |
| Cancel a cast | Right mouse while Waiting | Left trigger while Waiting |
| Reel (while hooked) | S, or left mouse | Right trigger |
| Steer (while hooked) | A / D | Left stick X |

You can walk with the rod out, but movement is locked while a fish is hooked (M2-D2).

## Fishing dev keys

These keys exist only in the editor and in development builds. In a build made without "Development Build" they do nothing. They are ignored while a menu is open, and they are not rebindable.

| Key | Effect |
|---|---|
| F3 | Toggle the dev overlay: rod state, tension, stamina, line, timers, the last roll, the loadout and (on the host) the ledger. |
| F4 | Next rod (cycles through all four rods). |
| F5 | Next bait. |
| F6 | Toggle a forced 0.1 s bite (host only). |
| F7 | Toggle the twitchy hook (host only). |
| F9 | Ripple at the crosshair. |

F8 is unused. The dock bell rings a debug "sell creel" that empties your creel and adds coins.

The debug loadout every player starts with: the Handmade rod, the worm selected, 20 of each bait, and the bare hook unlimited. The host refuses a rod or bait selection while that player has an active cast (a cast that has been accepted and not yet resolved). Do not switch rod or bait in the middle of a charge: only the landing is reconciled.

## Fight log

In the editor and in development builds each cast that reaches Waiting appends one row to `fight-log.csv` (cancels and misses included), so the fun-gate stats can be computed. It is written under `Application.persistentDataPath`, which on Windows is `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\` (currently `DefaultCompany\My project`). Each player's log holds only their own casts.

The file name carries the instance profile: `fight-log.csv` for the main editor, and `fight-log_<name>.csv` for `-dev-profile <name>` players and for MPPM clones. The automated test runs use `-dev-profile fgtest`, so they write `fight-log_fgtest.csv`. Delete the file to start a fresh log.

Columns: `profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity`. The fun-gate stats recipe (a PowerShell script) is in the M2 acceptance doc, section D.

## Generated scenes are disposable

Scenes, prefabs and most project settings are generated by editor code. **Hand edits are lost on the next `FishGame > Build All`** (or `Tools\unity.ps1 build`, or `check`). To keep a change, edit the builder under `Assets/_Project/Editor/Builders/`, or a data asset, then run `FishGame > Build All`.

| Rebuilt on every build (edits lost) | Created only if missing (Inspector edits kept) |
|---|---|
| `Assets/_Project/Scenes/*.unity` (Bootstrap, Menu, Spot_LakeDock) | `Assets/_Project/Content/ContentRegistry.asset` |
| `Assets/_Project/Prefabs/*.prefab` (PlayerFP, DockBell, SessionState) | `Assets/_Project/Content/Spots/lake_dock.asset` |
| `Assets/_Project/Content/Materials/*.mat` | `Assets/_Project/Content/UI/PanelSettings.asset` |
| `Assets/_Project/Content/Meshes/*.asset` | `Assets/_Project/Content/Fishing/**` (rods, baits, fight profiles, species, junk, `FishingTuning.asset`: this is where tuning lives) |
| `Assets/_Project/Content/Rendering/*` (the volume profiles are rewritten every build; the GUID is kept) | `Assets/_Project/Content/Audio/SfxCueTable.asset` |
| `Assets/_Project/Content/Network/FishGameNetworkPrefabs.asset` | `Assets/_Project/Content/Art/ArtPalette.asset` |
| Layers, physics matrix, URP quality, Enter Play Mode options, Run In Background, Netcode settings, build scene list | `Assets/_Project/Content/Art/WaterSettings.asset` |

A create-if-missing asset is never overwritten, so a new seed row added in code (for example a new sound cue) does not appear in an existing asset: edit the asset by hand, or delete it and run `FishGame > Build All` to recreate it from the seed. The one exception is a species' model slot, which the builder fills when it is empty and the FBX exists.

Generated prefabs and content are byte-stable across rebuilds. Scene files get their internal fileIDs renumbered on each Build All with identical content, so discard those diffs with `git restore Assets/_Project/Scenes/*.unity` unless you meant to change a scene.

## Online play (join codes)

Host, Join and the join code need the project linked to Unity Cloud (`Edit > Project Settings > Services`), with Relay and Lobby enabled in the Unity dashboard. The project owner does this. Without it, Solo and dev-direct still work, and the online buttons show the `err.services` error toast.

## Repository notes

- Run `git lfs install --local` once before the first push (`.gitattributes` tracks binary types with LFS).
- The FBX models under `Assets/_Project/Content/Models/` are stored in LFS. Run `git lfs pull` if a model imports as a tiny text file.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx` or `.vscode/`.
