# FishGame

Co-op (1-4 players) friend-slop rogue-lite fishing game. Unity 6000.6.5f1 (URP), Netcode for GameObjects 2.13.3, Unity Multiplayer Services 2.3.3 (Relay + Lobby join codes).

- Design spec (source of truth): `docs/superpowers/specs/2026-10-09-fishgame-design.md`
- M1 manual acceptance checklist: `docs/superpowers/plans/2026-10-09-m1-acceptance.md`
- M2 manual acceptance checklist: `docs/superpowers/plans/2026-10-11-m2-acceptance.md`
- M3 manual acceptance checklist: `docs/superpowers/plans/2026-10-13-m3-acceptance.md`

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
| Open or close the journal | J | Select (View) |
| Ready the rod | Right mouse (hold) | Left trigger (hold) |
| Charge and cast | Left mouse (hold, release to cast) | Right trigger (hold, release to cast) |
| Cancel a cast | Right mouse while Waiting | Left trigger while Waiting |
| Reel (while hooked) | S, or left mouse | Right trigger |
| Steer (while hooked) | A / D | Left stick X |

You can walk with the rod out, but movement is locked while a fish is hooked (M2-D2).

The journal opens only when you are free, have the rod readied or have a line waiting. It does nothing while you charge, fight, swim or climb, and it closes by itself when a bite arrives (see "Journal / meta.json").

## Fishing dev keys

These keys exist only in the editor and in development builds. In a build made without "Development Build" they do nothing. They are ignored while a menu is open, and they are not rebindable.

| Key | Effect |
|---|---|
| F3 | Cycles overlay, then dev panel, then off. The overlay shows rod state, the fight (strain, tire, back and p, the pull wave), line, timers, the last roll, the loadout, a WORLD line and (on the host) the ledger, including the forced species. The panel is described in "Ecology dev panel". |
| F4 | Next rod (cycles through all four rods). |
| F5 | Next bait. |
| F6 | Toggle a forced 0.1 s bite (host only). |
| F7 | Toggle the twitchy hook (host only). |
| F8 | Hold: the host's world clock runs x60 (the scrub). Release it to return to normal speed. On a client it does nothing. |
| F9 | Ripple at the crosshair. |
| F10 | Cycle the forced species (host only; does nothing while paused). None, then each of the 25 species in database order (sorted by id: `bluegill`, `bruiser_tuna`, `bucketmouth_bass`, and so on), then None again. The F3 overlay's HOST line shows `species=`. On a client it shows "Host only". The pick stays set (it is not cleared when a session ends) until you cycle back to None. |

The M2 species were renamed in M3: perch is `yellow_perch`, carp is `grumpy_carp`, trout is `sunny_trout` and catfish is `mudcat`.

The dock bell rings a debug "sell creel" that empties your creel and adds coins.

The debug loadout every player starts with: the Handmade rod, the worm selected, 20 of each bait, and the bare hook unlimited. The host refuses a rod or bait selection while that player has an active cast (a cast that has been accepted and not yet resolved). Do not switch rod or bait in the middle of a charge: only the landing is reconciled.

## Fight log

In the editor and in development builds each cast that reaches Waiting appends one row to `fight-log.csv` (cancels and misses included), so the fun-gate stats can be computed. It is written under `Application.persistentDataPath`, which on Windows is `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\` (currently `DefaultCompany\My project`). Each player's log holds only their own casts.

The file name carries the instance profile: `fight-log.csv` for the main editor, and `fight-log_<name>.csv` for `-dev-profile <name>` players and for MPPM clones. The automated test runs use `-dev-profile fgtest`, so they write `fight-log_fgtest.csv`. Delete the file to start a fresh log.

Columns: `profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity`. `castDist` is the cast distance from the rod tip (add the 2.4 m tip reach for origin-to-landing). The fun-gate stats recipe (a PowerShell script) is in the M2 acceptance doc, section D.

## Ecology dev panel

The M3 world (a day clock, a season, a weather track, hotspots and two maps) is driven by the host. The dev panel is the host's remote control for it. It exists only in the editor and in development builds (`Debug.isDebugBuild`, checked at run time): in a normal player build the clock shows, and F3, F8 and F4-F10 do nothing.

**Open it.** Press **F3** once for the overlay, twice for the panel (cursor free, gameplay input off), three times to turn everything off. Esc, gamepad B and the **Close** button also close the panel. The journal and the panel exclude each other: opening one while the other is open is refused. In a menu or while Pause is open, no dev key does anything.

**Rows (host only).** Every edit is a direct host call, not a network message. A client sees the same rows read-only, with the host's values and the title "Dev panel (client: read-only)".

| Row | What it does |
|---|---|
| Season (4 buttons) | Sets the run season (`DevWorldDriver.DevSetSeason`). The weather track is rebuilt for the new season. The HUD glyph, the WORLD line and the palette follow. |
| Time slider | Jumps the world clock to a minute of the day (0 to 1190, so 6:00 AM to 1:50 AM). One write 0.2 s after you stop dragging. |
| Dev clock running | Starts and stops the clock. A stopped clock stays stopped during an F8 scrub. |
| Weather (Track, Clear, Rain, Fog) | Track follows the pre-rolled 35-segment weather track. The other three force that weather for the current segment (an override that lives in the host's driver). |
| New day | Starts the next day: day + 1, the clock back at 6:00 AM, and casting reopens after a day-over. |
| Spawn hotspot at crosshair | Puts a hotspot (3 m radius) where the crosshair meets water. Over the dock it says "No water under crosshair". |
| Run seed + Apply | Replaces the session's run seed (decimal or `0x` hex). See below. |
| Luck slider (-1 to 1) | Sets `FishingDebugSettings.Luck`, which skews the size (weight) of the fish the host rolls, up at +1 and down at -1. It does not change which species bite. Host only; it resets when the session ends. |
| Rod tier (0 to 3) | Asks the host's own rod to switch to the lowest rod of that tier (while Idle; "Refused: cast active" otherwise). |
| Load map | One button per registered spot (Lake Dock, Harbour Pier). See below. |

**F8 scrub.** Hold F8 and the host's clock runs at 60 times normal speed (a whole day, 6:00 AM to 2:00 AM, takes about 15 real seconds instead of 15 minutes). Release it and the clock returns to normal. Alt-tab or opening a menu counts as a release. On a client it does nothing.

**The WORLD overlay line.** With the overlay or the panel on, the F3 text has a line `WORLD spot=<id> min=<n> (<time>) season=<s> weather=<w> hotspot=<x,z r= until <minute>|none> open=<yes|no>`. On the host it continues with `day=`, `clock=run|stop`, `scrub=on|off`, `casts=` (the host's accepted-cast counter) and `seed=0x...`. `open=no` means the day is over and the host refuses casts.

**The run seed (M3-D57).** The M3 default run seed is fixed (`0xF15F15F1`), so a solo session opens the same way every time: dawn, one season rolled from the seed and the same weather track. To try another world, type a seed and press Apply **before the first cast**: the season, the weather track and the clock restart on day 1. After the first accepted cast the row says "Locked after the first cast" and the host ignores the change. A new session (back to the menu, then Solo or Host) starts again from the fixed seed.

**The map switch (M3-D31).** In a development build the host can load another spot from the panel. The player respawns at that map's start, the clock restarts at 6:00 AM, and the session's run state survives (same season, day, seed, catch ledger, `casts=` counter and Luck). The slot loadout (rod, bait stock) and the creel are per scene, so they reset. Clients follow the host. There is no map switch in a normal build: the Hub's map board arrives in M4.

**HUD clock.** The top of the screen shows the world time (6:00 AM at the start of a day, in 10-minute steps) with the season and weather glyphs. It is in every build.

## Journal / meta.json

The journal lists every species (25 in M3) with a card that fills in as you learn about it. It is saved per machine in `meta.json`; nothing about it is sent over the network.

**Open it.** Press **J** (gamepad: **Select**) in game, or use the **Journal** button on the main menu (it shows `Journal n/25`). In game it opens only when you are free or have the rod readied or waiting, and it closes by itself when a bite arrives so you are never left fighting behind it. Close it with J, Esc, gamepad B or the Close button. The filters are "All spots", one button per spot, and "Biting now only" (in game only).

**The four levels.**

| Level | Shows | Earned by |
|---|---|---|
| 0 | A black silhouette, "???" and "Maybe at: <spots>" | nothing yet |
| 1 | The coloured icon, the name and "Seen at: <spot> (<sub-areas>)" | a catch announcement for it on your machine, landed or lost, by any player |
| 2 | Adds the season, weather and time-of-day glyphs and the "! Biting now" badge when the current spot, time, season, weather and your rod and bait would let it bite | anyone in the crew landing it (you or a friend) |
| 3 | Adds the weight range and your best weight, length and quality | landing it yourself three times |

**The file.**
- Path: `Application.persistentDataPath\meta.json`, which on Windows is `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\`. The name follows the instance profile, like the fight log: `meta.json` for the main editor, `meta_<profile>.json` for `-dev-profile <profile>` players and MPPM clones. The automated runs use `-dev-profile fgtest`, so they write `meta_fgtest.json` and never your own file.
- It is written atomically on every journal change and on quit. The previous version is kept as `meta.json.bak`.
- If the main file cannot be read, the game loads the `.bak` (the log says `FG SAVE loaded Backup`) and copies the broken file to `meta.json.corrupt` before the next save can overwrite anything.
- The file has `schemaVersion` 1. A file with a newer schema loads read-only (a warning in the log): the game never overwrites it.
- To reset the journal, delete `meta.json` (and its `.bak`) while the game is closed.

**Re-baking the journal icons.** The cards use `Assets/_Project/Content/Textures/T_FishIcons.png`, a 5-column atlas of 256 x 128 tiles rendered from the real fish models with their skin layers (M3-D96). It needs a graphics device, so `Tools\unity.ps1 build` (`-nographics`) cannot make it. After a species model or colour changes, run the baker without `-nographics`:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -quit -projectPath C:\Dev\FishGame -logFile <abs scratch dir>\icons.log -executeMethod FishGame.EditorTools.FishIconBaker.Bake
```

or use the menu item `FishGame > Bake Fish Icons`. Then commit `T_FishIcons.png` (and its `.meta`) and `Assets/_Project/Content/Art/ArtPalette.asset`, which holds the tile table. The result is deterministic for the same inputs on the same GPU.

## Generated scenes are disposable

Scenes, prefabs and most project settings are generated by editor code. **Hand edits are lost on the next `FishGame > Build All`** (or `Tools\unity.ps1 build`, or `check`). To keep a change, edit the builder under `Assets/_Project/Editor/Builders/`, or a data asset, then run `FishGame > Build All`.

| Rebuilt on every build (edits lost) | Created only if missing (Inspector edits kept) |
|---|---|
| `Assets/_Project/Scenes/*.unity` (Bootstrap, Menu, Spot_LakeDock, Spot_HarbourPier) | `Assets/_Project/Content/ContentRegistry.asset` |
| `Assets/_Project/Prefabs/*.prefab` (PlayerFP, DockBell, SessionState) | `Assets/_Project/Content/Spots/*.asset` (lake_dock, harbour_pier) |
| `Assets/_Project/Content/Ecology/ShoreGrids/*_grid.asset`: re-baked in place by every build, so the values are overwritten but the GUID is kept | `Assets/_Project/Content/Ecology/**` (zones, the weather table, `EcologyTuning.asset`, and the shore grids as empty assets until the first bake) |
| `Assets/_Project/Content/Materials/*.mat` | `Assets/_Project/Content/UI/PanelSettings.asset` |
| `Assets/_Project/Content/Meshes/*.asset` | `Assets/_Project/Content/Fishing/**` (rods, baits, fight profiles, species, junk, `FishingTuning.asset`: this is where tuning lives) |
| `Assets/_Project/Content/Rendering/*` (the volume profiles are rewritten every build; the GUID is kept) | `Assets/_Project/Content/Audio/SfxCueTable.asset` |
| `Assets/_Project/Content/Network/FishGameNetworkPrefabs.asset` | `Assets/_Project/Content/Art/ArtPalette.asset` |
| Layers, physics matrix, URP quality, Enter Play Mode options, Run In Background, Netcode settings, build scene list | `Assets/_Project/Content/Art/WaterSettings.asset` |

A create-if-missing asset is never overwritten, so a new seed row added in code (for example a new sound cue) does not appear in an existing asset: edit the asset by hand, or delete it and run `FishGame > Build All` to recreate it from the seed. The one exception is a species' model slot, which the builder fills when it is empty and the FBX exists.

Generated prefabs and content are byte-stable across rebuilds. Scene files get their internal fileIDs renumbered on each Build All with identical content, so discard those diffs with `git restore Assets/_Project/Scenes/*.unity` unless you meant to change a scene. Restore or commit a scene as a **whole file**, never as a hunk: the in-scene `World` object's network id follows those fileIDs.

**Art files are imported, not generated.** The build never writes them; they come from Blender and are committed (Git LFS):

| File | Made by | How to change it |
|---|---|---|
| `Content/Models/Fish_*.fbx` (25 species) | `Art/Blender/fishgame_m3_fish.blend` (the M2 five from `fishgame_m2_species.blend`), exported to `Art/Export/` and copied here keeping the `.meta` files | Edit the `.blend`, re-export, copy over, then re-bake the journal icons |
| `Content/Textures/T_FishSkinArray.png` (4 x 8 sheet, 32 layers; layer 0 white, species 1-25) | The skin script in `Art/Blender/fishgame_m3_fish.blend`, staged as `Art/Export/T_FishSkinArray_m3.png` and copied over the Content file (keep its `.meta`) | Never hand-edit it |
| `Content/Models/Harbour/*.fbx` and `harbour_manifest.json` | `Art/Blender/fg_harbour.py` (also stored as a text block in `fishgame_harbour.blend`), exported to `Art/Export/Harbour/` | Edit the script and re-run it in Blender. The code builder (`HarbourPierBuilder`) owns the terrain, water, colliders, hooks, stand points and the shore-grid bake, and only places the FBX |
| `Content/Textures/T_FishIcons.png` | `FishGame > Bake Fish Icons` (needs a GPU; see "Journal / meta.json") | Re-bake, then commit it with `ArtPalette.asset` |

## Online play (join codes)

Host, Join and the join code need the project linked to Unity Cloud (`Edit > Project Settings > Services`), with Relay and Lobby enabled in the Unity dashboard. The project owner does this. Without it, Solo and dev-direct still work, and the online buttons show the `err.services` error toast.

## Repository notes

- Run `git lfs install --local` once before the first push (`.gitattributes` tracks binary types with LFS).
- The FBX models under `Assets/_Project/Content/Models/` are stored in LFS. Run `git lfs pull` if a model imports as a tiny text file.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx` or `.vscode/`.
