# FishGame M1 Foundation — Manual Acceptance Checklist

- **Date written:** 2026-10-09
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` §17.1 (acceptance criteria 1–13, definition of done, user actions), §7.8 (player acceptance), §10.7 (performance baseline).
- **Who runs it:** the user, in the Unity editor with Multiplayer Play Mode (MPPM) 3.0 and the Network Simulator (`com.unity.multiplayer.tools` 2.2.12). Claude wrote this list and runs only criterion 1's command-line part.
- **How to record:** tick the box, then replace `______` with `PASS`, `FAIL` or `BLOCKED`, the date, and a short note (what you saw, or the error text). A `FAIL` needs enough detail to reproduce.
- **Setup help:** `README.md`, sections "Local multiplayer testing" and "Generated scenes are disposable".

## Before you start

- [ ] All M1 tasks are committed and `Tools\unity.ps1 check` printed `FG RESULT check PASS` (criterion 1).
- [ ] `FishGame > Build All` has run on this checkout. Do not hand-edit generated scenes or prefabs during these tests.
- [ ] `FishGame > Dev > Auto Solo` is **off** for every multiplayer criterion (3–9, 12). Turn it on only if you want it for criterion 2.
- [ ] No `-dev-long-timeout` tag or flag is set, except where a step asks for it.
- [ ] An MPPM scenario `Direct 4P` exists: the Main Editor plus 3 Additional Editor Instances (README, "Set up a scenario").
- [ ] Each instance has its own display name, set on its main menu (for example Host, P2, P3, P4).
- [ ] Every extra standalone build or instance you launch for multi-instance testing uses `-dev-profile <name>` with a different name each (for example `p2`, `p3`), so each keeps its own `settings_<name>.json`. Without it, instances share one identity and the second join is refused as a duplicate. MPPM instances already get their own profile.

## A. Automated gate

### Criterion 1: `check` passes and the console is clean
- [ ] **Steps:** With the editor closed, run `Tools\unity.ps1 check` (README, "Build and test from the command line"). Then open the project in the editor, clear the Console, and open `Assets/_Project/Scenes/Bootstrap.unity`, `Menu.unity` and `Spot_LakeDock.unity` one by one. Select the prefabs `PlayerFP`, `DockBell` and `SessionState` in `Assets/_Project/Prefabs/`.
- **Expected:** The last output line is `FG RESULT check PASS`. The Console shows zero errors. No GameObject or prefab shows a "Missing (Mono Script)" component.
- **Result:** ______

## B. Offline checks (no Unity Cloud link needed)

### Criterion 2: Offline solo
- [ ] **Steps:** Disable the PC's network adapter. Press Play, then press **Solo** on the main menu and time it until you stand on the dock. Then walk, sprint, jump, look around, walk off the dock and swim, climb out at a ladder and at the shore ramp, press Esc to pause, then **Leave**. Clear the Console before you press Play. Afterwards, search the Console for `Unity.Services`, `Authentication`, `Relay`, `Lobby` and `Multiplayer Services`. Re-enable the adapter.
- **Expected:** The dock is reached in under 5 s. Every action works. Leave returns to the main menu. No Console line from the run comes from Unity Gaming Services (UGS). No join-code toast or join-code UI appears, in the HUD or in the Pause menu.
- **Result:** ______
- [ ] **Extra check (cancel while loading):** Press **Solo**, and press Esc on the busy screen right away ("Loading…").
- **Expected:** You end on the Main Menu, not stuck on the dock or on the loading screen. Pressing **Solo** again works.
- **Result:** ______

### Criterion 3: Dev-direct with 1 host and 3 clones
- [ ] **Steps:** Start the `Direct 4P` scenario. On the Main Editor, press **Host Direct**. On each additional instance, press **Join 127.0.0.1**. When all 4 stand on the dock, look at each other from every instance. In the Main Editor's Hierarchy (Play mode), expand each `PlayerFP` and check `HeadPivot/Shake/Camera`. Then walk to the `DockBell` at the end of the dock and press E.
- **Expected:**
  - Nothing goes through UGS.
  - Every instance sees the other 3 players. Each body shows its slot colour (slot 0 coral, 1 blue, 2 green, 3 yellow), and each has a nameplate with the right display name.
  - On each instance, only the local player's Camera and AudioListener are enabled. The remote players' cameras are disabled, and the scene camera is off once your own player has spawned.
  - The bell rings (sound) on all 4 instances.
- **Result:** ______
- [ ] **Extra check (host rename):** While hosting, the host opens Settings and changes their own display name, then closes Settings.
- **Expected:** The host's nameplate updates on every instance.
- **Result:** ______

### Criterion 4: Latency under the Network Simulator
- [ ] **Steps:** Set up the `NetSim_150ms_2pct` preset and add the Network Simulator to `App` on the **3 client instances only**, before they join (README, "Latency with the Network Simulator"). Host and join as in criterion 3. Run every item in "Player acceptance (7.8)" below (P1–P6).
- **Expected:** All of P1–P6 pass under about 150 ms RTT and 2% loss.
- **Result:** ______

### Criterion 5: Version check
- [ ] **Steps:** The host presses **Host Direct** with no offset. Give one client the flag `-dev-protocol-offset 1`: either an MPPM tag with that text, assigned to one additional instance (then restart the scenario), or a standalone development player started with `-dev-profile p5 -dev-protocol-offset 1`. Press **Join 127.0.0.1** on that client.
- **Expected:** The client is refused, returns to the main menu, and shows the `err.version` toast: "Different game version — update your build (or the host's build differs)." The host keeps running and shows no new player. Keyboard focus on the main menu is on **Host** (a failure other than a bad code or unavailable services puts focus on Host).
- **If the tagged MPPM client is accepted:** the tag may not have reached the protocol setting. Repeat with the standalone development player before you record `FAIL`, and note which path you used.
- **Result:** ______

### Criterion 6: Joins closed
- [ ] **Steps:** Host with **Host Direct** and let one client join. On the host, press Esc and choose the dev-only **Close joins** toggle. Join from a second client that has not joined yet. Then choose **Open joins** on the host and join from that second client again.
- **Expected:** While joins are closed, the join is refused with the `err.joins_closed` toast: "Run in progress. Join when the crew is in the Hub, at the Briefing or at Night Camp." After **Open joins**, the same client joins normally. The first client is not affected at any point.
- **Result:** ______

### Criterion 7: Clean leave
- [ ] **Steps:** With 4 players connected, one client presses Esc and chooses **Leave**. Watch the other 3 instances, and time how long the leaver stays visible.
- **Expected:** Within 1 s, the leaver's body and nameplate disappear on every other instance (the host marks the slot disconnected and the player object despawns). The leaver is back on the main menu with no error toast.
- **Record:** actual time until all peers show the leaver as disconnected: ______ s
- **Result:** ______

### Criterion 8: Process kill and reclaim
- [ ] **Steps:** With 4 players connected, note one client's slot colour. Kill that client's editor process in Task Manager (find the `Unity.exe` process of that instance). Time it on the host. Then restart that same instance (same MPPM instance, so the same settings file and identity) and press **Join 127.0.0.1**.
- **Expected:** The host removes the killed player within 10 s and keeps running without errors. The restarted client gets the **same slot colour** and name as before.
- **Record:** actual time until the host removed the killed player (unclean kill): ______ s
- **If MPPM ends the whole scenario when one instance is killed:** record `BLOCKED (MPPM)`, then repeat with a standalone development player as the killed client, started with `-dev-profile p2 -dev-join-direct 127.0.0.1:7777` and restarted with the same arguments.
- **Result:** ______

### Criterion 9: Host leaves
- [ ] **Steps:** With the host and at least 2 clients connected, the host presses Esc and chooses **Leave**. Time it on the clients.
- **Expected:** Within 10 s, every client is back on the main menu with the toast "Host ended the session." No instance logs an exception.
- **Extra check (cancel while loading):** The Esc-on-busy-screen check from criterion 2 (the screen reads "Joining session…" before "Loading…"), run as a client right after pressing **Join 127.0.0.1**, also ends on the Main Menu, not stuck.
- **Result:** ______

### Criterion 10: Mode switch
- [ ] **Steps:**
  1. Automated part: the PlayMode test `Solo_LeaveThenRestart_Works` passed inside criterion 1's `check`.
  2. Manual part, in one Play session of the Main Editor: start **Solo**, then **Leave**, then **Solo** again. Once, press Esc on the busy screen right after Solo starts.
  3. Online part (only after the user action "link the project to Unity Cloud"): **Host** online, **Leave**, then **Solo**, without leaving Play mode.
- **Expected:** Every start works. The second Solo after an online session reaches the dock and uses no Relay. Pressing Esc on the busy screen leaves you on the Main Menu. Record step 3 as `BLOCKED (no cloud link)` until the link exists.
- **Result:** ______

### Criterion 11: Settings
- [ ] **Steps:**
  1. Change several M1 settings (master volume, mouse sensitivity, field of view, invert Y, UI scale, display name, one key rebind). Close the settings panel, stop Play mode, close and reopen the editor, and press Play again.
  2. In the dock scene (`Spot_LakeDock`), move **Master volume** from 80 to 0 and then to 100 while listening to the ambience (wind, water lap) and ringing the bell.
  3. Run the `Direct 4P` scenario. Change different settings in the Main Editor and in one additional instance, and close each panel. Open both files under `Application.persistentDataPath` (`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`): `settings.json` (Main Editor) and `settings_<instance name>.json` (the clone).
  4. With no mouse, use the keyboard only to open every menu (main menu, Settings, Pause), change one value, and go back. Then do the same with a gamepad only.
  5. Set the Game view to a fixed 1280×720 resolution and UI scale to 150%. Open the main menu, Settings, Pause and the HUD.
  6. Gamepad: in a session, press **Start**. Then press **B** (UI Cancel). Open Pause again, open Settings from it, and press **B**. Then press **Start** while Pause is open, and again while Settings is open.
  7. Gamepad only: focus the join-code box, the display-name box and the Settings name box in turn, and try to move focus out of each with the d-pad or left stick.
  8. Rebinds: start a rebind in Settings and press keyboard **Escape**. Start another rebind with a gamepad only and press nothing.
  9. Trigger an error (for example a join with a bad code, or `err.version` from criterion 5) and listen to the UI error sound (`Ui_Error`).
- **Expected:**
  1. Every changed value is still set after the restart.
  2. The volume change is audible live (0 is silent). Wind and water lap ambience and the bell sound effect are audible at 80 and 100.
  3. Each file holds only its own instance's values. Both files are valid JSON and neither is corrupted. (The EditMode test `SettingsStore_TwoInstances_DoNotCorruptEachOther` covers the file-level case.)
  4. Every control can be reached and used by keyboard only and by gamepad only.
  5. No text or control is clipped or off-screen.
  6. **Start** opens Pause. **B** closes Pause, and **B** closes Settings. **Start** does not close Pause or Settings.
  7. You can always move focus out of a focused text field to the buttons, with a gamepad only.
  8. Keyboard Escape cancels a rebind and keeps the old binding. On a gamepad only, cancel is not available and the rebind ends at the 6 s timeout (known M1 limitation).
  9. The error sound reads as an error.
- **Record:** step 8 actual behaviour on gamepad: ______. Step 9 does `Ui_Error` sound like an error (yes/no): ______
- **Result:** ______

## C. Online (needs the user's Unity Cloud link)

### Criterion 12: Online join codes
- [ ] **Preconditions:** The user linked the project to Unity Cloud (`Edit > Project Settings > Services`) and enabled Relay and Lobby in the Unity dashboard. Claude never accepts terms, enters billing details or clicks dashboard controls.
- **Caution:** Do not open `Project Settings > Services > Unity Player Accounts`. That page creates `Assets/Resources/UnityPlayerAccountSettings.asset`, which breaks invariant 4.7 #7 (`FishGame > Validate` and `check` then fail on "unexpected folder Assets/Resources"). If the folder appears, remove it (with its `.meta`) before the next `check`.
- **Steps:**
  1. On the Main Editor, press **Host** and time how long the join code takes to appear (HUD toast and Pause menu).
  2. On an additional instance, press **Join** and enter the code. On two more instances, enter a lowercase copy and a copy with spaces before and after.
  3. On a fresh instance, enter a code that does not exist, such as `ZZZZZZ`.
  4. With 4 players connected, try to join with a 5th player: a standalone development player started with `-dev-profile p5`, or a 4th additional editor instance.
  5. The host closes joins (Pause menu, **Close joins**) and a new client tries to join mid-run. Reopen joins afterwards if you need to continue.
  6. While hosting, the host renames themselves in Settings.
  7. With the network adapter disabled, press **Host** or **Join** once.
  8. The host chooses **Leave**.
- **Expected:**
  1. The code appears within 5 s.
  2. All joins work (the code is normalized by trimming and upper-casing). If the lowercase copy fails, the codes are case-sensitive: record that as a finding.
  3. A readable "code not found" toast appears (`err.code_not_found`). Focus returns to the join-code box and the typed text is kept.
  4. The 5th player is refused with the full-session toast (`err.session_full`). Focus is on **Host**.
  5. The refusal reads "Run in progress" (`err.joins_closed`), not the `err.services` toast.
  6. The host's nameplate updates for everyone.
  7. The `err.services` toast ("Online services are unavailable. You can play Solo instead.") appears, and focus is on **Solo**.
  8. Every client returns to the main menu with "Host ended the session."
- **Record:** the real join-code format (length, character set, letter case), and whether any join was refused as a duplicate (that would mean two instances share one PlayerId, see criterion 13). Also record the exact message texts shown for LobbyLocked (step 5) and LobbyFull (step 4): LobbyLocked: ______; LobbyFull: ______; join-code format: ______
- **Result:** ______

## D. Recorded findings and code comments

### Criterion 13: Recorded findings
The class comment of `Assets/_Project/Networking/UgsNetworkSession.cs` holds the block "Recorded findings (M1 criterion 13)". The first four findings below were recorded there from the package source. Tick each one when the runtime run agrees. Record the last ones from your runs. Claude then updates the code comment in a follow-up commit (`fix(UgsNetworkSession.cs): record M1 runtime findings`).

- [ ] The Sessions NGO handler starts and stops NGO itself, and sets the relay data. Source finding: **yes**. Runtime check: online hosting worked without our code calling `StartHost` (criterion 12).
- [ ] NetworkManager must exist before `CreateSessionAsync`. Source finding: **yes**.
- [ ] The inspector Protocol Type of UnityTransport does not matter on the Sessions path. Source finding: **it does not matter**.
- [ ] Lobby-full and invalid-code errors arrive as `SessionError.Unknown` and are mapped by message text. Runtime check: criterion 12 steps 3 and 4 showed the right toasts. Record the exact LobbyLocked and LobbyFull message texts (criterion 12).
- [ ] MPPM clones get distinct `PlayerId`s (expected from the `-name` auth profile). Runtime result: ______ (yes if 4 instances joined online with no duplicate refusal; if not, use `SwitchProfile` while signed out).
- [ ] Solo works after a Sessions run (criterion 10, step 3). Runtime result: ______
- [ ] Join-code format (criterion 12). Runtime result: ______
- [ ] Performance baseline recorded (see "Performance baseline (10.7)").
- **Result:** ______

## Player acceptance (7.8)

Run with 4 MPPM instances in dev-direct, with the Network Simulator on the 3 clients (criterion 4). Watch from client instances.

- [ ] **P1.** Remote movement shows no snap larger than 0.5 m during steady walking. Result: ______
- [ ] **P2.** Remote head pitch follows the owner's look within 5°. Result: ______
- [ ] **P3.** A swimming or wading player climbs out of the lake within 2 s of reaching either ladder or the shore ramp. Result: ______
- [ ] **P4.** A teleport lands within 0.05 m with no leftover velocity. (Covered by the PlayMode test `TeleportRpc_OnDeck_LandsWithinFiveCentimetersWithZeroVelocity` in criterion 1; any visible teleport in a session must also land cleanly.) Result: ______
- [ ] **P5.** Clients see the **host's own** player move. A failure here blocks M1. Result: ______
- [ ] **P6.** The owner sees no part of their own body except the right arm and their shadow. Camera and AudioListener are active only on the owner. Result: ______

## Performance baseline (10.7)

Recorded once at M1 on the dev machine. A later regression of more than 10% needs a note in that milestone's summary.

- [ ] **Steps:**
  1. `File > Build Profiles`: Windows, tick **Development Build** (and **Autoconnect Profiler**), build to `C:\Dev\FishGame\Builds\M1Perf\` (the `Builds/` folder is gitignored).
  2. Start the host: `FishGame.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -dev-profile perf1 -dev-host-direct`.
  3. Start 3 clients so the dock has 4 bodies: `FishGame.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -dev-profile perfN -dev-join-direct 127.0.0.1:7777`, with `perf2`, `perf3` and `perf4`.
  4. On the host, stand on the dock facing the lake with the 3 other players in view. In the editor, open `Window > Analysis > Profiler`, connect to the host player, and read the CPU frame time and the Rendering module's batch and SetPass counts over about 10 s. Use `Window > Analysis > Frame Debugger` on the host player to count the SRP Batch entries.
- **Record:** machine (CPU, GPU), frame time (ms, average and worst), SRP batch count, Batches, SetPass calls. All 4 players run on the same machine, so note that with the numbers.
- **Result:** ______

## Definition of done

- [ ] **Offline DoD:** criteria 1–11 and 13 pass (P1–P6 included, through criterion 4).
- [ ] **Online DoD:** criterion 12 passes, once the user has linked the project to Unity Cloud and enabled Relay and Lobby.

## User actions due in M1

Claude does not do these. The user does.

- [ ] Link the project to Unity Cloud (`Edit > Project Settings > Services`) and enable Relay and Lobby in the Unity dashboard (unblocks criteria 10 step 3 and 12).
- [ ] Approve running `git lfs install --local` once, before the first push.
- [ ] Decide keep or remove for `com.unity.pipeline` (the optional 1-hour editor-control spike, spec §4.8).
- [ ] Optionally approve the repo-local UnityYAMLMerge driver (`merge.unityyamlmerge`).
- [ ] Close the editor before command-line runs, or work in editor-open mode (README, "Editor-open mode").
- [ ] Run this checklist and record the results above.
