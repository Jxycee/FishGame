# FishGame M2 Fishing Core - Manual Acceptance Checklist

- **Date written:** 2026-10-12
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` section 17.2 (M2 acceptance criteria 1-11, fun gate, definition of done, user actions), 9.11-9.13 (feedback, tuning, edge cases), 10.7 (performance baseline).
- **Contract:** `docs/superpowers/plans/2026-10-11-m2-interfaces.md` (section 16 holds the review deltas; section 10 the test inventory).
- **Who runs it:** the user does the manual rows (editor, Multiplayer Play Mode (MPPM) 3.0, the Network Simulator, a gamepad, a friend). Claude ran the automated rows (T30) and pasted their numbers below.
- **How to record:** tick the box, then replace `______` with `PASS`, `FAIL` or `BLOCKED`, the date, and a short note (what you saw, or the error text). A `FAIL` needs enough detail to reproduce.
- **Setup help:** `README.md`, sections "Local multiplayer testing", "Fishing dev keys" and "Fight log".
- **Measured on:** branch `m2-fishing-core`, commit `1e82274` (the tree T30 started from), Unity 6000.6.5f1, 2026-10-12.

## Before you start

- [ ] All M2 tasks (T1-T30) are committed and `Tools\unity.ps1 check` prints `FG RESULT check PASS`.
- [ ] `FishGame > Build All` has run on this checkout. Do not hand-edit generated scenes or prefabs during these tests.
- [ ] `FishGame > Dev > Auto Solo` is **off** for every multiplayer item (section B and the multiplayer rows of criteria 3, 5, 6). Turn it on for criterion 1.
- [ ] The MPPM scenario `Direct 4P` exists (README, "Set up a scenario"), and the `NetSim_150ms_2pct` preset exists (README, "Latency with the Network Simulator").
- [ ] A gamepad is connected for criterion 1, criterion 5 (rumble) and criterion 10 (skeleton risk 8).
- [ ] The M1 checklist's multiplayer criteria (3-9, 12 in `docs/superpowers/plans/2026-10-09-m1-acceptance.md`) are run or scheduled: they were still open on 2026-10-11 (handoff). M2 co-op rests on them.
- [ ] You have read section F.1 (the cast-distance decision). It blocks the fun gate (section D).

## A. Criteria 1-11 (spec 17.2)

| # | Criterion (17.2) | Owner | How to check (short) | Status 2026-10-12 |
|---|---|---|---|---|
| 1 | Solo: Idle to a landed fish, no errors, only the 8.3 bindings, keyboard/mouse **and** gamepad | User | Solo run on both devices, Console cleared | Open (user) |
| 2 | Every 9.2 transition has a passing EditMode test | Claude | `test-edit -Filter RodStateMachine` | PASS: 62 NUnit results, 62 passed |
| 3 | Co-op: cues on both peers; reveal for everyone after the host commits | User | MPPM items MP1 and MP4 (section B) | Open (user) |
| 4 | Tension and phase readable with no HUD and with sound off | User | Three fights with gauge Off, captions off, volume 0 | Open (user) |
| 5 | Rod back to Idle within 3 s in every 9.13 case; no stuck states; no rumble left | User + Claude | Section C walk, gamepad connected | Open (user); automated rows pass |
| 6 | Bait consumed on accept, refunded on cancel/recall, lost on snap/escape/miss; bare hook never runs out | Claude (tests) + User | EditMode + PlayMode tests, then F3 LOADOUT count | Tests PASS; manual open |
| 7 | Host rejects forged or out-of-order casts and hooks and impossibly fast catches; species and weight come from the host roll | Claude (tests) + Opus xhigh review | Ledger tests, branch review | Tests PASS; review pending |
| 8 | Bots meet the 9.12 targets (a failure passes only with numbers recorded as a tuning item) | Claude | `test-edit -Filter "FishGame.Tests.EditMode.FishingBots"` | PASS: 9 of 9, every asserted target met (section F.2) |
| 9 | All tuning lives in assets; Core holds none except test defaults | Claude + User | Branch-review grep, then the Inspector edit | Review pending; manual open |
| 10 | Assist, line gauge, captions and Camera motion behave as 9.11 defines | User | Settings toggles in a fight | Open (user) |
| 11 | `FightSim` costs < 0.05 ms per step | Claude + User | Printed per-step time of `FishingBots_FightSimStep_UnderFiftyMicroseconds` | PASS: 0.16 us per step (0.00016 ms) |

### Criterion 1: Solo, Idle to landed fish, both devices
- [ ] **Owner:** User. **How to check:** Solo with Auto Solo on and the Console cleared (Clear before pressing Play).
- **Steps (keyboard/mouse):** press **1** (equip the rod), hold **RMB** (ready), hold **LMB** and release it (charge and cast), wait for the bite, then reel with **S** or **LMB** and steer with **A**/**D** during the fight until the fish lands. Use only the 8.3 bindings: no dev keys.
- **Steps (gamepad only):** **North** (equip), hold **LT** (ready), hold and release **RT** (cast), **RT** to reel, left stick **X** to steer.
- **Expected (each device):** the fish lands, the creel count goes +1, the catch toast and the reveal show, the rod returns to Idle, the Console shows 0 errors.
- **Record:** keyboard/mouse: ______. Gamepad: ______.
- **Result:** ______

### Criterion 2: Every 9.2 transition has a passing EditMode test
- [x] **Owner:** Claude (T30). **How to check:** `Tools\unity.ps1 test-edit -Filter RodStateMachine`.
- **Recount from the file (2026-10-12):** `RodStateMachineTests.cs` has 52 test methods. `RodStateMachine_IllegalEvents_IgnoredInEveryState` is a `TestCaseSource` over 11 states, so NUnit reports **62 results** (51 + 11), all passing. The contract's earlier figures (45 methods, 55 results, BF-8) were written before the T8 review added tests; contract section 16.1 says T30 recounts, so 62 is the figure.
- **Count line:** `FG RESULT test-edit PASS total=62 passed=62 failed=0 skipped=0`
- **Result:** PASS 2026-10-12.

### Criterion 3: Co-op cues and reveal
- [ ] **Owner:** User. **How to check:** MPPM with 2-4 instances: items MP1 and MP4 in section B.
- **Result:** ______

### Criterion 4: Readable with no HUD and with sound off
- [ ] **Owner:** User (with a second person watching).
- **Steps:** set the line gauge to Off, captions off, master volume 0. Fight 3 fish (one Bulldog; bait and rod via the dev keys F4/F5 are fine here). Name aloud:
  - side run versus dive (a splash column moving sideways and the line swinging, versus the bobber plunging with the line pointing down);
  - the creak band (the rod bends and the line warms in colour);
  - the warn band (the rod shakes, and the line shows the dash and the width pulse).
  A second person watches your screen and confirms each call. Then repeat with sound on and your eyes on the rod only.
- **Expected:** you and the watcher agree on side run / dive and on creak / warn in all 3 fights, silent and with sound.
- **Record:** fights: ______. Misreads: ______.
- **Result:** ______

### Criterion 5: Rod back to Idle within 3 s in every 9.13 case
- [ ] **Owner:** User + Claude. **How to check:** the section C walk below, with a gamepad connected so rumble is observable.
- **Result:** ______

### Criterion 6: Bait accounting
- [ ] **Owner:** Claude (tests) + User.
- **Automated (Claude, all in `check`):** EditMode `FishingHostLedger_Accept_ConsumesBaitAndRolls`, `FishingHostLedger_CancelInWaiting_RefundsBait`, `FishingHostLedger_SnapEscapeMiss_DoNotRefund`, `FishingHostLedger_BareHook_NeverRunsOut`; PlayMode `SoloHost_CancelInWaiting_RefundsBait`.
- **Manual steps:** press F3 and read the LOADOUT line (`x20`). A cast takes it to `x19`. **RMB** while Waiting cancels it: the count is back to 20. A miss (F7 twitchy hook on, do not click) keeps 19. Select the bare hook with F5: the count shows `inf` and does not drop on a cast.
- **Result:** ______

### Criterion 7: The host rejects forged and out-of-order messages
- [ ] **Owner:** Claude (tests) + Opus xhigh branch review.
- **Wording (2026-10-12, contract 16.2):** the criterion is "the host rejects forged and out-of-order messages (stale or duplicate cast ids, non-finite or far origins and yaw, no bait, early hooks, landings faster than the floor, implausible or self-contradictory clean claims) and bounds the line start by what a player could have walked (T9-R1)". Position checks are bounded by the OWNER-WRITTEN transform (the player moves under owner authority), which is accepted for a friends-only game (spec 1.2). A bare-hook reroll by cancelling is accepted design. The criterion does NOT claim protection against a skilled cheater who forges a plausible whole fight (spec 9.9: the host never replays the fight).
- **Automated:** EditMode ledger tests (contract 10.1 T9 rows: stale or duplicate id, charge range, origin more than 3 m off, no bait, early hook, landed below the floor, implausible or self-contradictory clean claims).
- **Branch review:** confirms that no owner-side path decides species, weight or quality. The owner only displays the roll from `CastResultRpc`; the creel and the announce use `CommitDecision.Roll`.
- **Result:** ______

### Criterion 8: Bots meet the 9.12 targets
- [x] **Owner:** Claude (T30). **How to check:** `Tools\unity.ps1 test-edit -Filter "FishGame.Tests.EditMode.FishingBots"`.
- **Result line:** `FG RESULT test-edit PASS total=9 passed=9 failed=0 skipped=0`. Every asserted target is met. The printed tables are in section F.2; marginal and informational numbers are in F.3.
- **Result:** PASS 2026-10-12 (with tuning items in F.3).

### Criterion 9: All tuning lives in assets
- [ ] **Owner:** Claude + User.
- **Claude (branch review):** grep for the 9.12 default literals in `Assets/_Project/Core/` outside `FishingConstants` (formula constants) and tests.
- **User steps:** in Play mode select `Assets/_Project/Content/Fishing/FishingTuning.asset` and change `castChargeTime` from 1.0 to 0.6 in the Inspector. The charge ring fills faster on the next charge. Revert it, or keep it if you want (the content builder is create-if-missing, so Inspector edits survive `Build All`).
- **Result:** ______

### Criterion 10: Assist, gauge, captions, Camera motion
- [ ] **Owner:** User.
- **Steps:**
  - Assist on: the grace feels longer (the line snaps later). While the gauge is Off, turning assist on switches the gauge to Arc each time (T25 rule, ruling G-Q4).
  - Line gauge Arc / Bar / Off: the arc below the crosshair, the thin bar, nothing.
  - Captions on / off: "Bite!", "Line straining", "Snapped", "Got away".
  - Camera motion 0: no camera shake, no FOV kicks, no rod shake and no rumble.
- **Expected:** each setting does exactly that, and persists after a restart.
- **Result:** ______

### Criterion 11: `FightSim` cost
- [x] **Owner:** Claude + User.
- **Claude:** the printed line of `FishingBots_FightSimStep_UnderFiftyMicroseconds` (the test fails only above 0.2 ms, so its pass is not the criterion; the printed number is): `FG PERF FightSim.Tick 0.16 us/step over 200000 steps`. That is 0.00016 ms per step, against the limit of 0.05 ms (50 us). It was measured in an EditMode run in the editor (Mono), not in a player build.
- **User option:** Profiler deep profile of a fight in a development build.
- **Result:** PASS 2026-10-12 (0.16 us per step).

## B. Manual MPPM list (spec 17.2)

Host plus 1-3 clients, dev-direct (`Host Direct` / `Join 127.0.0.1`), Network Simulator at 150 ms RTT and 2% loss on the **client instances only** (README, "Latency with the Network Simulator"). Auto Solo off.

- [ ] **MP1:** a client's cast lands, and the bite and fight cues play on both peers. Result: ______
- [ ] **MP2:** the remote rod bend, line and bobber trail the owner by no more than about 250 ms (side-by-side instances; a slow-motion screen capture is optional). Check the remote rod angle too. Result: ______
- [ ] **MP3:** the cast arc appears once on remotes (no replay; a late joiner sees no arc). Result: ______
- [ ] **MP4:** the catch reveal shows on all screens after the host commits, plus the toast. Crew toasts are limited to 1 per 3 s per catcher (T25: only MPPM can check this). Result: ______
- [ ] **MP5:** a client disconnecting mid-fight (Leave, then a process kill) leaves no outstanding hook on the host (the F3 HOST lines list that client's slot as `idle`, or no longer list it), and its rod vanishes within 1 s. Result: ______
- [ ] **MP6:** a host alt-tab (unfocused for 10 s) does not stall the clients. Result: ______
- [ ] **MP7:** late join mid-fight. Client B joins while client A is Hooked. B sees A's rod bent, the line and the bobber at the fish point, with no species until the reveal. Result: ______
- [ ] **MP8:** the owner feels no latency at 150 ms RTT (definition of done). Charge, cast and reel respond on the frame. Result: ______
- [ ] **MP9:** online over Sessions (join code), once the project is linked to Unity Cloud. Repeat MP1 and MP4. Result: ______ (BLOCKED until linked)
- [ ] **MP10 (T23/T24 review items):** the cast arc plays once per remote cast, and the landing marker is owner-only (a remote player never sees your cast marker). Result: ______
- [ ] **MP11 (150 ms clean-claim slack):** under the simulator, land at least 10 fish, playing each cleanly (reel and steer well). The host's 2.0 s `CleanClaimClockSlackSeconds` is a guess (skeleton risk 3). Compare the fight log `clean` column with what you earned. Record: ______
- **Hint:** start each extra client with the same `-dev-profile` rule as M1. A client you pause in the editor needs `-dev-long-timeout` (section C).

## C. 9.13 edge-case walk (criterion 5)

For every row: **Steps**, **Expected** (always "the rod is Idle within 3 s, no stuck state, rumble 0", plus the row's own expectation) and **Result**. Gamepad connected. The first column is the spec 9.13 case name.

- [ ] **Invalid spot under the marker**
  - Steps: aim at the dock or the shore, charge, release.
  - Expected: a thunk, the marker flashes, the rod stays Ready, the bait count is unchanged.
  - Result: ______
- [ ] **Owner/host disagree on validity**
  - Steps: hard to force by hand. Run a client under the simulator and strafe along the shore edge while releasing at a marginal spot. Covered by `RodStateMachine_Casting_InvalidResult_RecallsToIdle` and `RodStateMachine_Waiting_InvalidResult_RecallsToIdle`.
  - Expected: recall, no bait used.
  - Result: ______
- [ ] **Walking away while Waiting**
  - Steps: cast about 15 m, then walk back along the dock to the shore until you are past `maxLineOut`. R67: the limit is measured from the rod tip (Handmade 18 + 12 = 30 m), so it trips at about 32.4 m from the player to the bobber (30 m + `castTipReach` 2.4 m).
  - Expected: auto-reel, bait refunded (check the F3 count).
  - Result: ______
- [ ] **Fish over land or dock (cosmetic)**
  - Steps: during a fight, let a side run swing the fish point toward the dock.
  - Expected: the bobber holds the last water point; the fight itself is unaffected.
  - Result: ______
- [ ] **Fish takes all the line**
  - Steps: Twig rod (F4), raw meat (F5), cast long, hook a big fish and never reel.
  - Expected: spooled: the line clamps and the tension rises (`spoolStrain`); tiring the fish first recovers it.
  - Result: ______
- [ ] **Alt-tab mid-fight**
  - Steps: alt-tab away for 5 s while Hooked, with a gamepad rumbling.
  - Expected: neutral input, the tension relaxes, the session stays alive, the rumble stops on focus loss.
  - Result: ______
- [ ] **Owner hangs but stays connected**
  - Steps: start the client with `-dev-long-timeout` (otherwise the 10 s disconnect fires first and you test MP5 instead). Cast, then press the client editor's Pause during Waiting for `BiteWait` + 30 s (watch F3 on the host).
  - Expected: the host refunds (the stuck-Waiting rule). Unpaused, the client plays the recall beat and is Idle within 3 s. The Hooked path (240 s) is covered by `FishingHostLedger_HookedPastMaxFightTime_TimesOutEscapedTimeout`.
  - Result: ______
- [ ] **No `CommitRpc`**
  - Steps: pause the host editor (under 10 s, or give the client `-dev-long-timeout`) right as the client lands a fish.
  - Expected: the client is back in Idle 3 s after the terminal state, with one warning in its Console. Also covered by `RodStateMachine_Terminal_NoCommit_ReturnsIdleAfterCommitTimeout`.
  - Result: ______
- [ ] **Host disconnects**
  - Steps: the host chooses Leave mid-fight.
  - Expected: the clients return to the menu with "Host ended the session.", no exception, rumble 0.
  - Result: ______
- [ ] **Late joiner sees a rod mid-fight**
  - Steps: the same as MP7.
  - Expected: rod state, bobber and tension are shown; the species is hidden.
  - Result: ______
- [ ] **Two bobbers overlap**
  - Steps: two players cast to the same spot.
  - Expected: not handled (an M5 tangle). Record only that nothing breaks.
  - Result: ______
- [x] **Day end (minute 1200 or the vote) while Casting, Waiting, Bite or Hooked (M4)**: `N/A (M4)`
- [x] **Night Camp snapshot while a cast or fight is live (M4)**: `N/A (M4)`
- [x] **Resume from a snapshot with a cast mid-air (M4)**: `N/A (M4)`

## D. Fun gate (you plus at least one friend, at least 30 logged fights)

**Do not start this before the cast-distance decision in F.1 is made.** Short casts land instantly as clean catches and would pollute every median below.

### Setup
- Telemetry runs only when `Debug.isDebugBuild`. The friend plays a **development build** (`File > Build Profiles`, Development Build) over a join code, or dev-direct on a LAN.
- Each player's log holds only their own casts: `Application.persistentDataPath/fight-log{Suffix}.csv`. On Windows that is `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\`; on 2026-10-12 that was `DefaultCompany\My project` (check `ProjectSettings/ProjectSettings.asset` if it changed). The suffix is empty for the main instance and `_<name>` for `-dev-profile <name>` players and MPPM clones (`-editor-mode -name <x>`). The automated test runs write `fight-log_fgtest.csv`.
- The friend sends their CSV. Collect every `fight-log*.csv` into one scratch folder and run the stats recipe below on that folder.
- **Coverage:** worm and grub reach only bluegill, perch and carp (rating versus `minBaitRating`). Trout needs shrimp or raw meat. Catfish needs raw meat and a landing at least 2.5 m deep. Testers rotate baits with F5, cast long, and aim for **at least 5 hooked fights per profile**. Leave F6 and F7 **off** during gate sessions (forced bites distort cast-to-bite).

### Columns (contract 5.3)
`profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity`

`castDist` is the cast distance from the rod tip (R67, `9278b2e`); the horizontal origin-to-landing distance is `castDist` + `castTipReach` (2.4 m). Rows logged before `9278b2e` measured it from the player.

`outcome` and `reason` are enum names (Landed, Snapped, Escaped, Cancelled / None, Missed, Pinned, ThrewHook, Dropped, Timeout, Invalid, Disconnected). `assist`, `clean` and `pity` are 1 or 0. One row per cast that reached Waiting (cancels and misses included). Delete a file to start a fresh log.

### Stats recipe (Windows PowerShell 5.1)
Save as `FunGateStats.ps1` and run `powershell -NoProfile -ExecutionPolicy Bypass -File FunGateStats.ps1 -Folder <folder with the CSVs>`. It:
1. imports every `fight-log*.csv` (`Import-Csv`) and parses numbers with `CultureInfo.InvariantCulture`;
2. reads `outcome` and `reason` as enum names, and maps digits through the contract enum order if a log holds digits; reads `assist`, `clean`, `pity` as `true` or `1`;
3. defines the row sets: all rows (casts that reached Waiting); bite rows (`castToBite > 0`); hooked (outcome Landed or Snapped, or Escaped with a reason other than Missed and Invalid); landed (outcome Landed); fish rows (profile not empty and not a junk species);
4. prints the totals and the hooked fights per profile (the gate needs at least 30 hooked, at least 5 per profile), the median and p90 of `castToBite` (median 6-12 s, p90 at most 20 s), the cancel-or-timeout rate (under 10%), the per-profile median `fightSec` over landed fish rows (Steady/Darter 15-30 s, Bulldog/Thrasher 35-55 s, Runner 60-110 s; the hooked median for information), the overall median of `castToBite + fightSec` over landed fish rows (35-80 s), the snap rate (10-30%), the near-snap recovery share for Bulldog, Thrasher and Runner (at least 40%), and, for information, the clean-catch rate, the number of instant landings (Landed with `fightSec` under 1 s), the assist row count and the pity numbers;
5. marks each target PASS or FAIL. Percentiles use linear interpolation between ranks.

Note: an `Escaped` row with reason `Timeout` counts as hooked by this definition even when the timeout happened in Waiting (`castToBite` 0). If such rows appear, read them by hand.

```powershell
# FunGateStats.ps1 - M2 fun-gate stats over fight-log*.csv (Windows PowerShell 5.1, ASCII only).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File FunGateStats.ps1 -Folder <dir holding fight-log*.csv>
param([Parameter(Mandatory = $true)][string]$Folder)

$inv = [System.Globalization.CultureInfo]::InvariantCulture

# Enum order from the contract (FightOutcome, EscapeReason); used only if a log holds digits instead of names.
$OutcomeNames = @('None', 'Landed', 'Snapped', 'Escaped', 'Cancelled')
$ReasonNames = @('None', 'Missed', 'Pinned', 'ThrewHook', 'Dropped', 'Timeout', 'Invalid', 'Disconnected')
$JunkIds = @('old_boot', 'tin_can')

function ToNum([string]$s) {
    $v = 0.0
    if ([double]::TryParse($s, [System.Globalization.NumberStyles]::Float, $inv, [ref]$v)) { return $v }
    return 0.0
}
function ToFlag([string]$s) { return ($s.Trim() -ieq 'true' -or $s.Trim() -eq '1') }
function EnumName([string]$s, [string[]]$names) {
    $t = $s.Trim()
    if ($t -match '^\d+$') { $i = [int]$t; if ($i -lt $names.Length) { return $names[$i] } }
    return $t
}
# Linear-interpolation percentile (p in 0..1) over a numeric array; $null when empty.
function Pct([double[]]$xs, [double]$p) {
    if ($null -eq $xs -or $xs.Count -eq 0) { return $null }
    $s = @($xs | Sort-Object)
    $pos = $p * ($s.Count - 1)
    $lo = [math]::Floor($pos); $hi = [math]::Ceiling($pos)
    return $s[$lo] + ($s[$hi] - $s[$lo]) * ($pos - $lo)
}
function Fmt($v, [string]$f = 'F1') { if ($null -eq $v) { return 'n/a' } return ([double]$v).ToString($f, $inv) }
function Verdict($v, [double]$lo, [double]$hi) { if ($null -eq $v) { return 'n/a' } if ($v -ge $lo -and $v -le $hi) { return 'PASS' } return 'FAIL' }
function Share($num, $den) { if ($den -eq 0) { return $null } return 100.0 * $num / $den }

$files = @(Get-ChildItem -Path $Folder -Filter 'fight-log*.csv' -File)
if ($files.Count -eq 0) { Write-Output ('No fight-log*.csv in ' + $Folder); exit 1 }

$rows = @()
foreach ($f in $files) {
    foreach ($r in (Import-Csv -LiteralPath $f.FullName)) {
        $rows += [pscustomobject]@{
            Profile  = [string]$r.profile
            Species  = [string]$r.species
            Bite     = ToNum $r.castToBite
            Fight    = ToNum $r.fightSec
            Outcome  = EnumName ([string]$r.outcome) $OutcomeNames
            Reason   = EnumName ([string]$r.reason) $ReasonNames
            Near     = [int](ToNum $r.nearSnapRecoveries)
            Assist   = ToFlag ([string]$r.assist)
            Clean    = ToFlag ([string]$r.clean)
            Pity     = ToFlag ([string]$r.pity)
        }
    }
}

$all = $rows
$bite = @($all | Where-Object { $_.Bite -gt 0 })
$hooked = @($all | Where-Object { $_.Outcome -eq 'Landed' -or $_.Outcome -eq 'Snapped' -or ($_.Outcome -eq 'Escaped' -and $_.Reason -ne 'Missed' -and $_.Reason -ne 'Invalid') })
$landed = @($all | Where-Object { $_.Outcome -eq 'Landed' })
$isFish = { $_.Profile -ne '' -and ($JunkIds -notcontains $_.Species) }
$fish = @($all | Where-Object $isFish)
$landedFish = @($landed | Where-Object $isFish)

Write-Output ('Files: ' + $files.Count + '   casts (rows that reached Waiting): ' + $all.Count)
Write-Output ('Hooked fights: ' + $hooked.Count + '   (gate needs >= 30: ' + $(if ($hooked.Count -ge 30) { 'PASS' } else { 'FAIL' }) + ')')
foreach ($g in ($hooked | Where-Object { $_.Profile -ne '' } | Group-Object Profile | Sort-Object Name)) {
    Write-Output ('  hooked ' + $g.Name + ': ' + $g.Count + '   (aim >= 5)')
}

$med = Pct @($bite | ForEach-Object { $_.Bite }) 0.5
$p90 = Pct @($bite | ForEach-Object { $_.Bite }) 0.9
Write-Output ('castToBite median ' + (Fmt $med) + ' s [6-12] ' + (Verdict $med 6 12) + '   p90 ' + (Fmt $p90) + ' s [<= 20] ' + (Verdict $p90 0 20) + '   (bite rows ' + $bite.Count + ')')

$cancelTimeout = @($all | Where-Object { $_.Outcome -eq 'Cancelled' -or $_.Reason -eq 'Timeout' }).Count
$cr = Share $cancelTimeout $all.Count
Write-Output ('cancel-or-timeout rate ' + (Fmt $cr) + ' % [< 10] ' + $(if ($null -eq $cr) { 'n/a' } elseif ($cr -lt 10) { 'PASS' } else { 'FAIL' }))

$targets = @{ steady = @(15, 30); darter = @(15, 30); bulldog = @(35, 55); thrasher = @(35, 55); runner = @(60, 110) }
foreach ($name in @('steady', 'darter', 'bulldog', 'thrasher', 'runner')) {
    $lf = @($landedFish | Where-Object { $_.Profile -eq $name } | ForEach-Object { $_.Fight })
    $hk = @($hooked | Where-Object { $_.Profile -eq $name } | ForEach-Object { $_.Fight })
    $m = Pct $lf 0.5
    $t = $targets[$name]
    Write-Output ('fightSec ' + $name + ' landed median ' + (Fmt $m) + ' s [' + $t[0] + '-' + $t[1] + '] ' + (Verdict $m $t[0] $t[1]) + '   (landed ' + $lf.Count + '; hooked median ' + (Fmt (Pct $hk 0.5)) + ' s, info)')
}

$tot = Pct @($landedFish | ForEach-Object { $_.Bite + $_.Fight }) 0.5
Write-Output ('cast-to-land (castToBite + fightSec) median ' + (Fmt $tot) + ' s [35-80] ' + (Verdict $tot 35 80) + '   (landed fish rows ' + $landedFish.Count + ')')

$snapped = @($hooked | Where-Object { $_.Outcome -eq 'Snapped' }).Count
$sr = Share $snapped $hooked.Count
Write-Output ('snap rate ' + (Fmt $sr) + ' % [10-30] ' + (Verdict $sr 10 30) + '   (' + $snapped + ' of ' + $hooked.Count + ' hooked)')

foreach ($name in @('bulldog', 'thrasher', 'runner')) {
    $hk = @($hooked | Where-Object { $_.Profile -eq $name })
    $rec = @($hk | Where-Object { $_.Near -ge 1 }).Count
    $sh = Share $rec $hk.Count
    Write-Output ('near-snap recovery ' + $name + ' ' + (Fmt $sh) + ' % [>= 40, HUMAN metric] ' + $(if ($null -eq $sh) { 'n/a' } elseif ($sh -ge 40) { 'PASS' } else { 'FAIL' }) + '   (' + $rec + ' of ' + $hk.Count + ' hooked)')
}

$cl = @($landedFish | Where-Object { $_.Clean }).Count
Write-Output ('clean-catch rate ' + (Fmt (Share $cl $landedFish.Count)) + ' % (info, R64)   (' + $cl + ' of ' + $landedFish.Count + ' landed fish)')
$short = @($landed | Where-Object { $_.Fight -lt 1.0 }).Count
Write-Output ('instant landings (Landed with fightSec < 1.0 s): ' + $short + ' of ' + $landed.Count + '   (short-cast issue; they pull fight medians down and inflate the clean rate)')
Write-Output ('assist rows: ' + @($all | Where-Object { $_.Assist }).Count)
$pity = @($all | Where-Object { $_.Pity })
$rest = @($all | Where-Object { -not $_.Pity })
$pl = @($pity | Where-Object { $_.Outcome -eq 'Landed' }).Count
$rl = @($rest | Where-Object { $_.Outcome -eq 'Landed' }).Count
Write-Output ('pity rows: ' + $pity.Count + '   landed share of pity casts ' + (Fmt (Share $pl $pity.Count)) + ' % vs the rest ' + (Fmt (Share $rl $rest.Count)) + ' % (info, R65)')
```

**Self-test (done by Claude on 2026-10-12, in the scratchpad):** run on this hand-made 6-row CSV, the script printed hooked 5 (bulldog 2, darter 1, runner 1, steady 1), castToBite median 9.0 s and p90 12.4 s, cancel-or-timeout 16.7% (FAIL), steady / bulldog / runner landed medians 20.0 / 40.0 / 70.0 s (PASS), cast-to-land median 50.0 s, snap rate 20.0%, bulldog near-snap recovery 100%, runner 0%, clean-catch rate 66.7% (2 of 3), assist rows 1, pity rows 1 with landed share 0.0% against 60.0% for the rest. A second CSV with an `Escaped/Missed` row (a bite row, not hooked), an `Escaped/Invalid` row, a landed `old_boot` (not a fish row) and an `Escaped/Timeout` row printed the matching exclusions. The same file with digits instead of enum names printed identical numbers, and a copy of the real `fight-log_fgtest.csv` parsed.

```
profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity
steady,bluegill,300,12.000,8.000,20.000,Landed,None,0,0.300,rod_handmade,0,1,0
bulldog,carp,2500,18.000,10.000,40.000,Landed,None,1,0.900,rod_handmade,0,0,0
bulldog,carp,3100,17.500,7.000,12.000,Snapped,None,2,1.000,rod_handmade,0,0,1
darter,perch,200,15.000,9.000,25.000,Escaped,ThrewHook,0,0.500,rod_handmade,0,0,0
,,0,10.000,0.000,0.000,Cancelled,None,0,0.000,rod_handmade,1,0,0
runner,trout,1800,20.000,14.000,70.000,Landed,None,0,0.800,rod_twig,0,1,0
```

### Fun-gate log
Fill this after the stats script runs. A failed target is **tuning data, not new mechanics** (17.2): retune the assets plus the matching `FishingSeedData` seeds.

| Target | Value | PASS/FAIL |
|---|---|---|
| Hooked fights (at least 30; at least 5 per profile) | ______ | ______ |
| castToBite median 6-12 s | ______ | ______ |
| castToBite p90 at most 20 s | ______ | ______ |
| Cancel-or-timeout rate under 10% | ______ | ______ |
| Steady median fight 15-30 s | ______ | ______ |
| Darter median fight 15-30 s | ______ | ______ |
| Bulldog median fight 35-55 s | ______ | ______ |
| Thrasher median fight 35-55 s | ______ | ______ |
| Runner median fight 60-110 s | ______ | ______ |
| Overall median cast-to-land 35-80 s | ______ | ______ |
| Snap rate 10-30% | ______ | ______ |
| Bulldog near-snap recovery at least 40% | ______ | ______ |
| Thrasher near-snap recovery at least 40% | ______ | ______ |
| Runner near-snap recovery at least 40% | ______ | ______ |
| Info: clean-catch rate (R64) / instant landings / assist rows / pity rows | ______ | n/a |

- **Near-snap recovery of at least 40% is a HUMAN fun-gate metric (2026-10-12, contract 16.2).** The T15 bots only print it (the Rhythm bot at `5a652bd`: bulldog 3%, thrasher 6%, runner 57.5%; bots do not model a human reacting to the warn cue). Read this row from the playtest CSVs, never from `FishingBots`. If the Bulldog or Thrasher share is low: `tuning.relaxWhileReeling` already sits at 0.20, the TOP of the 9.12 range (no headroom upward). The ruling's hints are to lower `relaxWhileReeling` toward 0.15, or raise the Thrasher `pullStrength`; and, because relax is at its ceiling, prefer raising the Bulldog/Thrasher `pullStrength` or lowering their calm minimum over relax. Record the per-profile shares and the chosen change: ______
- **Duration notes for the playtest:** the Twig-rod Thrasher trophy (weightT 0.9) has a bot median of about 93 s, and the Bulldog Rhythm median is 37.4 s against the 35 s floor. Watch both in real fights. If testers find the 93 s fight tedious, retune in data (assets plus seeds plus `TunedDeviations`; `thrasher.staminaDrain` is 0.035 after T15) and let the controller pick the lever. Notes: ______

### Blind tell-apart sheet
Before each reveal or loss toast, the tester writes a guess: Steady, Darter, Bulldog, Thrasher or Runner. The actual profile comes from the CSV `profile` column, matched by order among that tester's hooked rows.

| Fight # | Tester | Guess | Confidence (1-3) | Actual | Match |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| 6 | | | | | |
| 7 | | | | | |
| 8 | | | | | |
| 9 | | | | | |
| 10 | | | | | |

Add rows as needed. **Proposed bar (you confirm it):** 70% or more correct overall, and every profile at least 50%. Overall: ______ %. Per profile: Steady ______ Darter ______ Bulldog ______ Thrasher ______ Runner ______

### Play styles and the ten-minute check
- [ ] **Play-style question:** do the three play styles (greedy, counter-reel, rhythm) feel different? Free text: ______
- [ ] **Ten-minute check:** after 10 minutes, does each tester want to keep casting? Was conversation comfortable while waiting? Free text: ______
- **Result (fun gate):** ______

## E. Performance record (spec 10.7)

- [ ] **Steps (as in the M1 checklist):**
  1. `File > Build Profiles`: Windows, tick **Development Build** and **Autoconnect Profiler**, build to `C:\Dev\FishGame\Builds\M2Perf\` (the `Builds/` folder is gitignored).
  2. Start the host: `FishGame.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -dev-profile perf1 -dev-host-direct`.
  3. Start 3 clients so the dock has 4 bodies: `FishGame.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -dev-profile perfN -dev-join-direct 127.0.0.1:7777`, with `perf2`, `perf3` and `perf4`.
  4. On the host, stand on the dock facing the lake with the other 3 players in view. In the editor open `Window > Analysis > Profiler`, connect to the host player, and read the CPU frame time, the Batches, SetPass calls and the SRP batch count over about 10 s (the Frame Debugger on the host player counts the SRP Batch entries). Read the **water pass GPU time** in the Profiler GPU module ("Render Transparents") or as the Water draw in the Frame Debugger.
- **Record:** machine (CPU, GPU): ______. Frame time average / worst (ms): ______ / ______. SRP batch count: ______. Batches: ______. SetPass: ______. Water pass GPU time (ms): ______. All 4 players run on one machine: note that.
- **T28 stats line (2026-10-12, from this run's `build.log`):** `FG LAKEDOCK renderers=79 triangles=26260 meshes=9 materials=2`. Per mesh: Water 18432, Lakebed 1200, Shore 1152, Rock 80, Trunk 24, CanopyPine 24, CanopyRound 80, Dock 1300, Reeds 944 triangles.
- **Baseline:** there is no recorded M1 baseline (the M1 perf step was never filled in). Write "No M1 baseline; these are the first numbers" here if so (ruling G-Q8): ______. A regression above 10% against a recorded baseline needs a note in the milestone summary.
- **V13 items to verify in the development build:**
  - [ ] fog variants survive in a player build (fog is visible in the dev build). Result: ______
  - [ ] the water facet lighting sign (the lit side of each facet faces the sun). Result: ______
  - [ ] C# / HLSL wave parity: the bobber sits on the visible wave. Result: ______
- **Result:** ______

## F. Open decisions, bot report and tuning items

### F.1 OPEN USER DECISION (blocks the fun gate): where is the cast distance measured from?
- **Problem:** casts shorter than about 4.4 m (the bottom ~12% of the Handmade rod charge, from `(4.4 - 2.5) / (18 - 2.5)`) start with a line length at or below `landDistance` (2.0 m; the host's line start is `max(landDistance, hostDistance - HookedLineSlack 3.1 m)`), so they land instantly as clean catches. `FightSim` has no lead-in gate on landing (`CalmLeadInSeconds` 0.8 s only sets the first calm phase), and the host's landed-too-fast floor is 0 when the line start equals `landDistance`, so the host accepts the instant landing as honest. This is a design gap, not a criterion 7 hole. Seen in the real fight log: a 4.8 m cast reached Landed after 0.46 s at zero tension, `clean` = 1.
- **Options (the user decides):**
  - **A (recommended):** measure the cast distance from the rod tip, not from the player origin, so a minimum-charge cast is a real cast.
  - **B:** raise `castMinDist` to about 5 m. This changes R4 and is outside the spec 9.12 range of 2-4 m, so the spec table changes too.
  - **C:** accept it as is (tell testers not to count short casts).
- **Also consider (can combine with A or B):** a minimum fight time or lead-in gate before a fish can be landed, and a minimum fight duration before the clean bonus can be earned.
- **Until decided:** the fun-gate stats print an "instant landings" count; testers should cast at least half charge.
- **Decision and date:** A, 2026-10-12 (user). Spec R67: the cast distance is measured from the rod tip (`castTipReach` constant on owner and host). Note: at minimum charge the owner line start becomes about `castMinDist` (2.5 m) against `landDistance` 2.0 m, so the very shortest casts are still short fights; the implementation report records the measured minimum-cast fight time per rod and whether a lead-in gate is still wanted.
  - **Measured after R67 (`9278b2e`):** in-game minimum charge is 0.10 (`MinChargeNorm`; charge 0 is not castable). At charge 0.10 the owner start line is 3.65-5.25 m across the shipped rods, and with an always-reel bot the time to Landed is 2.4-3.0 s at the minimum and 2.6-4.3 s at the median. Clean claims are 48-77% at charge 0.10.

### F.2 Bot report (criterion 8): T15 `FishingBots`, 200 seeds per cell
Command: `Tools\unity.ps1 test-edit -Filter "FishGame.Tests.EditMode.FishingBots"`. Result: 9 of 9 passed, matrix time 2.4 s. Columns: `land` = landed share, `snap` = snapped share, `len` = fight length of landed fights (p10 / median / p90, seconds), `snapT` = median time to the snap, `near-snap rec>=1` = share of fights with at least one near-snap recovery, `clean` = the printed clean pair (claimed share / landed-and-clean share).

**Asserted targets (all PASS):**

| Test | Value | Target |
|---|---|---|
| Bulldog Handmade, Greedy snap | 100.0% | at least 70% |
| Bulldog Handmade, CounterReel snap | 33.0% | 10-40% |
| Bulldog Handmade, Rhythm snap | 0.0% | at most 5% |
| Bulldog Handmade, Rhythm median | 37.4 s | 35-55 s |
| Steady Rhythm median | 19.5 s | 15-30 s |
| Darter Rhythm median | 22.9 s | 15-30 s |
| Thrasher Rhythm median | 41.6 s | 35-55 s |
| Runner Rhythm median | 70.2 s | 60-110 s |
| Runner Rhythm snap | 0.0% | at most 15% |
| Twig rod, Rhythm land share (every profile) | 100% at weight 0.5 and 0.9 | at least 90% at 0.5, at least 75% at 0.9 |

**Full matrix (printed `FG BOT` lines, reformatted):**

| profile | rod | w | bot | land | snap | len p10/med/p90 (s) | snapT med (s) | near-snap rec>=1 | clean |
|---|---|---|---|---|---|---|---|---|---|
| steady | rod_handmade | 0.5 | Greedy | 100.0% | 0.0% | 19.3/19.7/20.0 | n/a | 1.0% | 99.0%/99.0% |
| steady | rod_handmade | 0.5 | CounterReel | 100.0% | 0.0% | 19.2/19.5/19.8 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_handmade | 0.5 | Rhythm | 100.0% | 0.0% | 19.2/19.5/19.8 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_handmade | 0.9 | Greedy | 98.5% | 1.5% | 19.9/20.4/20.9 | 13.0 | 0.0% | 100.0%/98.5% |
| steady | rod_handmade | 0.9 | CounterReel | 100.0% | 0.0% | 19.8/20.2/20.5 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_handmade | 0.9 | Rhythm | 100.0% | 0.0% | 19.8/20.2/20.5 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_twig | 0.5 | Greedy | 98.0% | 2.0% | 16.2/16.5/16.9 | 10.4 | 0.0% | 99.5%/97.5% |
| steady | rod_twig | 0.5 | CounterReel | 100.0% | 0.0% | 16.0/16.3/16.6 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_twig | 0.5 | Rhythm | 100.0% | 0.0% | 16.0/16.3/16.6 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_twig | 0.9 | Greedy | 96.5% | 3.5% | 16.7/17.1/17.6 | 10.0 | 0.0% | 96.4%/93.0% |
| steady | rod_twig | 0.9 | CounterReel | 100.0% | 0.0% | 16.5/16.9/17.2 | n/a | 0.0% | 100.0%/100.0% |
| steady | rod_twig | 0.9 | Rhythm | 100.0% | 0.0% | 16.5/16.9/17.2 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_handmade | 0.5 | Greedy | 99.5% | 0.5% | 24.0/24.6/25.2 | 12.9 | 0.0% | 99.5%/99.0% |
| darter | rod_handmade | 0.5 | CounterReel | 100.0% | 0.0% | 22.6/22.9/23.2 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_handmade | 0.5 | Rhythm | 100.0% | 0.0% | 22.6/22.9/23.2 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_handmade | 0.9 | Greedy | 99.0% | 1.0% | 26.1/26.9/27.6 | 10.5 | 0.0% | 97.5%/96.5% |
| darter | rod_handmade | 0.9 | CounterReel | 100.0% | 0.0% | 23.8/24.1/24.5 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_handmade | 0.9 | Rhythm | 100.0% | 0.0% | 23.8/24.1/24.5 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_twig | 0.5 | Greedy | 94.5% | 5.5% | 20.3/20.9/21.4 | 14.8 | 0.0% | 94.2%/89.0% |
| darter | rod_twig | 0.5 | CounterReel | 100.0% | 0.0% | 19.0/19.2/19.5 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_twig | 0.5 | Rhythm | 100.0% | 0.0% | 19.0/19.2/19.5 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_twig | 0.9 | Greedy | 75.0% | 25.0% | 22.1/22.8/23.3 | 18.3 | 0.0% | 52.7%/39.5% |
| darter | rod_twig | 0.9 | CounterReel | 100.0% | 0.0% | 20.1/20.4/20.7 | n/a | 0.0% | 100.0%/100.0% |
| darter | rod_twig | 0.9 | Rhythm | 100.0% | 0.0% | 20.1/20.4/20.7 | n/a | 0.0% | 100.0%/100.0% |
| bulldog | rod_handmade | 0.5 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 6.8 | 5.5% | 0.0%/0.0% |
| bulldog | rod_handmade | 0.5 | CounterReel | 67.0% | 33.0% | 32.8/35.8/38.4 | 27.4 | 39.0% | 50.7%/34.0% |
| bulldog | rod_handmade | 0.5 | Rhythm | 100.0% | 0.0% | 33.6/37.4/42.9 | n/a | 3.0% | 97.0%/97.0% |
| bulldog | rod_handmade | 0.9 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 6.3 | 8.0% | 0.0%/0.0% |
| bulldog | rod_handmade | 0.9 | CounterReel | 33.0% | 67.0% | 35.1/39.2/43.3 | 22.6 | 50.5% | 27.3%/9.0% |
| bulldog | rod_handmade | 0.9 | Rhythm | 100.0% | 0.0% | 37.2/40.8/46.1 | n/a | 7.5% | 92.5%/92.5% |
| bulldog | rod_twig | 0.5 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.8 | 8.0% | 0.0%/0.0% |
| bulldog | rod_twig | 0.5 | CounterReel | 13.0% | 87.0% | 26.0/27.3/29.0 | 17.5 | 34.0% | 11.5%/1.5% |
| bulldog | rod_twig | 0.5 | Rhythm | 100.0% | 0.0% | 28.7/32.3/37.5 | n/a | 27.5% | 72.5%/72.5% |
| bulldog | rod_twig | 0.9 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.5 | 8.5% | 0.0%/0.0% |
| bulldog | rod_twig | 0.9 | CounterReel | 4.0% | 96.0% | 28.3/29.4/32.3 | 14.7 | 33.0% | 12.5%/0.5% |
| bulldog | rod_twig | 0.9 | Rhythm | 100.0% | 0.0% | 31.7/35.2/40.7 | n/a | 38.0% | 62.0%/62.0% |
| thrasher | rod_handmade | 0.5 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 6.4 | 3.0% | 0.0%/0.0% |
| thrasher | rod_handmade | 0.5 | CounterReel | 97.5% | 2.5% | 39.0/41.4/43.6 | 18.3 | 3.5% | 96.4%/94.0% |
| thrasher | rod_handmade | 0.5 | Rhythm | 100.0% | 0.0% | 39.0/41.6/43.8 | n/a | 6.0% | 94.0%/94.0% |
| thrasher | rod_handmade | 0.9 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 5.8 | 2.5% | 0.0%/0.0% |
| thrasher | rod_handmade | 0.9 | CounterReel | 33.5% | 66.5% | 50.1/52.9/57.5 | 26.7 | 22.5% | 55.2%/18.5% |
| thrasher | rod_handmade | 0.9 | Rhythm | 100.0% | 0.0% | 51.3/57.6/63.6 | n/a | 81.5% | 18.5%/18.5% |
| thrasher | rod_twig | 0.5 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 4.3 | 1.0% | 0.0%/0.0% |
| thrasher | rod_twig | 0.5 | CounterReel | 9.0% | 91.0% | 31.9/33.5/35.1 | 17.6 | 13.5% | 55.6%/5.0% |
| thrasher | rod_twig | 0.5 | Rhythm | 100.0% | 0.0% | 36.2/45.6/55.3 | n/a | 95.0% | 5.0%/5.0% |
| thrasher | rod_twig | 0.9 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.4 | 1.0% | 0.0%/0.0% |
| thrasher | rod_twig | 0.9 | CounterReel | 0.0% | 100.0% | n/a/n/a/n/a | 9.9 | 8.0% | 0.0%/0.0% |
| thrasher | rod_twig | 0.9 | Rhythm | 100.0% | 0.0% | 68.1/93.2/133.3 | n/a | 100.0% | 0.0%/0.0% |
| runner | rod_handmade | 0.5 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.9 | 2.5% | 0.0%/0.0% |
| runner | rod_handmade | 0.5 | CounterReel | 0.0% | 100.0% | n/a/n/a/n/a | 17.7 | 16.5% | 0.0%/0.0% |
| runner | rod_handmade | 0.5 | Rhythm | 100.0% | 0.0% | 61.0/70.2/79.0 | n/a | 57.5% | 42.5%/42.5% |
| runner | rod_handmade | 0.9 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.7 | 0.5% | 0.0%/0.0% |
| runner | rod_handmade | 0.9 | CounterReel | 0.0% | 100.0% | n/a/n/a/n/a | 13.4 | 7.5% | 0.0%/0.0% |
| runner | rod_handmade | 0.9 | Rhythm | 100.0% | 0.0% | 74.5/83.4/92.9 | n/a | 83.0% | 17.0%/17.0% |
| runner | rod_twig | 0.5 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.4 | 0.5% | 0.0%/0.0% |
| runner | rod_twig | 0.5 | CounterReel | 0.0% | 100.0% | n/a/n/a/n/a | 10.3 | 2.0% | 0.0%/0.0% |
| runner | rod_twig | 0.5 | Rhythm | 100.0% | 0.0% | 60.2/69.2/77.0 | n/a | 94.0% | 6.0%/6.0% |
| runner | rod_twig | 0.9 | Greedy | 0.0% | 100.0% | n/a/n/a/n/a | 3.2 | 0.0% | 0.0%/0.0% |
| runner | rod_twig | 0.9 | CounterReel | 0.0% | 100.0% | n/a/n/a/n/a | 6.4 | 0.0% | 0.0%/0.0% |
| runner | rod_twig | 0.9 | Rhythm | 100.0% | 0.0% | 79.3/87.9/99.0 | n/a | 99.0% | 1.0%/1.0% |

**Signature motion (printed `FG SIG`):** per profile side-run seconds, flips per second, dives and bursts over the signature run: steady 2072.6 s, 0.00/s, 0.0% dives; darter 2349.3 s, 0.83/s; bulldog 3004.3 s, 0.00/s, 33.4% dives, creep 0.038 m/s; thrasher 4637.7 s, 0.40/s, jitter sd 0.17; runner 7055.3 s, 0.09/s, 25.6% dives.

**Fun-gate lines printed by the bots (informational):** near-snap recovery, Rhythm, Handmade, weight 0.5: bulldog 3.0%, thrasher 6.0%, runner 57.5% (fun-gate line at least 40%). Overall snap rate over the 5 profiles, Handmade, weight 0.5: Greedy 60.1%, CounterReel 27.1%, Rhythm 0.0% (fun-gate 10-30% applies to human play, not to the Rhythm bot).

**Tuning values in force (printed `FG DATA`):** relaxWhileReeling 0.20, reelStrainBurst 0.30, counterRelief 0.50, snapGrace 0.25, cleanMax 0.80, maxFight 240; thrasher drain 0.035, runner drain 0.035.

### F.3 Open tuning items (numbers from F.2)
No asserted bot target failed. These are the marginal or informational numbers to watch in the fun gate:

1. **Near-snap recovery (human metric).** Rhythm bot: Bulldog 3.0%, Thrasher 6.0% (both far under the 40% line), Runner 57.5%. Bots do not model a human reacting to the warn cue, so the gate row is read from the playtest CSVs. Hints in section D (relax is at its 0.20 ceiling).
2. **Bulldog Rhythm median is 37.4 s** against the 35 s floor (40.8 s at weight 0.9; Twig 32.3 s at weight 0.5 is below 35 s but the target is Handmade).
3. **Twig-rod Thrasher at weight 0.9 is long:** Rhythm median 93.2 s (p10 68.1, p90 133.3). Handmade at weight 0.9 is 57.6 s (above the 35-55 s band; the asserted cell is weight 0.5, 41.6 s). Watch it in real fights.
4. **Rhythm snap rate is 0.0%** in every Rhythm cell, so an expert human may find the game too safe; the 10-30% line is a human metric.
5. **Clean catches are rare for a Rhythm bot on the hard profiles:** Thrasher Handmade weight 0.9 clean 18.5%, Twig 5% / 0%; Runner Handmade 42.5% / 17.0%, Twig 6% / 1%. Informational (R64).
6. **Greedy Darter, Twig, weight 0.9** snaps 25.0%; Greedy Steady, Twig, snaps 2.0-3.5%. Informational.

### F.4 Skeleton section 8 risks and status
| # | Risk | Status 2026-10-12 |
|---|---|---|
| 1 | Perch FBX (single submesh, vertex colours) | Closed: T29 imported all five models (`FG MODELS`: bluegill 222, carp 238, catfish 262, perch 242, trout 230 triangles; one submesh each; linear vertex colours). **User look check pending** (section I). |
| 2 | V11 `DeferLocal` on a host-owned object | Source-confirmed; the runtime check is PlayMode `SoloHost_CastResult_ArrivesAfterCastRequestReturns` (in `check`). A failure needs a manual one-frame deferral in `FishingRodNetwork` (contract change). |
| 3 | Clean-claim 2.0 s slack at 150 ms RTT | Open: MPPM item MP11. |
| 4 | Motion seeds are first guesses | Decided by T15 data (retuned 2026-10-12, section F.2); the human gate confirms. |
| 5 | `RodStateData` change detection | Closed (NGO uses `Equals`). |
| 6 | NetworkBehaviour indices on the player prefab | Only T19 adds NetworkBehaviours; the branch review confirms the order. |
| 7 | `-nographics` shader validation | Closed (T20, `54e40c0`): the validator runs under `-nographics` and the canary proves it detects errors. `build.log` prints `FG SHADERS checked=4 passes=13`. |
| 8 | Gamepad verification | Open: needs your controller (criterion 1, criterion 5 rumble). |

## G. Definition of done (spec 17.2)

- [ ] Acceptance criteria 1-11 and the fun gate are met.
- [ ] Solo works offline (network adapter disabled, as in M1 criterion 2).
- [ ] 2-4 players fish together over dev-direct (and over Sessions once linked).
- [ ] The owner feels no latency at 150 ms RTT (MP8).

## H. Bad-luck nudge (R65 / M2-D23) - USER

- **Steps:** in the Inspector set `Content/Fishing/FishingTuning.asset` `pityAfterFailures` to 3 (the seed). Fail three casts in a row (miss with F7 twitchy and no click, or let a fish snap the line). The next cast shows "<name> has a lucky line!" on every peer; that fight has a longer snap grace and lower strain; the fight log row has `pity` 1. A Landed catch or a cancel never shows the toast.
- **Expected:** the nudge is noticeable but not a free win (the same species, weight and quality odds).
- **USER playtest line (2026-10-12):** after at least 10 nudged casts, write down whether the longer snap grace and the lucky-line toast are noticed, whether failures now feel less punishing, and whether anyone feels the game is helping too much: ______
- **Record:** **USER playtests the bad-luck nudge and decides keep / tune / remove.** Decision: ______
- **Result:** ______

## I. User actions due

- [ ] **Decide the cast-distance origin (F.1): A, B or C.** This blocks the fun gate.
- [ ] Run this checklist (sections A-E, H) and record the results.
- [ ] Run the M1 multiplayer criteria (3-9, 12) that are still open.
- [ ] Run the fun gate with a friend (development build, section D).
- [ ] Decide the bad-luck nudge: keep / tune / remove (section H).
- [ ] Approve the T29 look: the five fish models and the catch reveal per species (bluegill, perch, carp, trout, catfish), and your opinion on the fallback fish shape (T23).
- [ ] Approve merging `m2-fishing-core` to `main` after the final branch review.
- **Manual checks already queued for you:**
  - [ ] Menu / UI split smoke steps (T2): the menu and settings still work after the UI split.
  - [ ] HUD check (T25): charge ring, prompts, captions, gauge, creel count, toasts.
  - [ ] Dev overlay check (T26): F3 updates through a cast, a bite and a fight.
  - [ ] Dock look check, frame-time baseline and V13 facet lighting (T28).
  - [ ] Five-fish look check and the catch reveal per species (T29).
  - [ ] Swim / wade at the ladder (`LadderR`): E climbs out, the bell is not rung.
  - [ ] Two-player fishing check (T23/T24): remote rod angle, the arc plays once, the marker is owner-only.
  - [ ] Fishing sounds listen (u01).
  - [ ] Toon look check (T20).

## J. Carried manual checks and later-milestone hand-offs (2026-10-12, contract 16.2)

### Manual checks carried from reviews
- [ ] **F3 overlay in the editor and in a NON-development player build:** in the editor, F3 toggles the overlay and it updates through a cast, a bite and a fight. In a build made WITHOUT "Development Build", F3 and F4-F9 do nothing at all (T8-fix/T10 review). Result: ______
- [ ] **Swimming interact gate:** swim to the ladder and to the bell, with the rod out and stowed. Expected: **E** climbs out at the ladder exit and never rings the bell or sells the creel; in open water **E** does nothing (T16 note, swim-gate fix `a0eb655`/`8320ad5`; recorded by the T28 look check). Result: ______
- [ ] **150 ms RTT clean-claim slack:** the 2.0 s `CleanClaimClockSlackSeconds` is a guess (skeleton risk 3); note the MPPM result (MP11). Result: ______
- [ ] **Gamepad:** criterion 1 and rumble 0 on every terminal state need a controller. Result: ______
- [ ] **Rod geometry (ruling T19-R1):** cast from the deck end at minimum and maximum charge, at camera pitch -60, 0 and +60. The rod tip stays in front of the camera, and the line never starts shorter than the host expects (no `Invalid` escapes from the early-hook or landed-floor checks on honest casts). Result: ______

### Known residuals (accepted for M2)
- **Dev-key rod switch mid-cast (dev keys only, M2-D18; contract 16.3):** only the landing is reconciled; the owner machine keeps the OLD rod for that cast. A slower rod (deepsea to twig): the host `LandedTooFast` floor uses the new rod's `ReelSpeed`, so an honest long-line landing can come back Escaped(Invalid). A longer rod (twig to deepsea): the old `MaxLineOut` is below the host landing, so the rod recalls at Waiting entry with a refund. The bobber may visibly slide after adoption, and the owner's landing splash or ripple can sit at the old spot. Do not use F4/F5 mid-cast during gate sessions.
- **`Line_Crack` remote path is untested:** the crack SFX is played from the rising edge of `RodWireQuant.FlagGrace`; only the owner path has a test.
- **T25 crew toast limit (1 per 3 s per catcher)** is checkable only under MPPM (MP4).
- **Fallback fish shape:** the procedural fallback mesh is used only when a species has no imported model; your opinion is in section I.

### M4 hand-off note
`FishCaught` is published even when the host's creel store was missing (the fish was not stored). Quota consumers (M4) must count creel data (`PlayerCreel` / `CreelLedger`), never `FishCaught` events.

### M5 hand-off notes
1. Knockback (`PlayerLocomotion.AddKnockback`) must END the cast, or the host walk bound (`CatchValidator.WalkBoundedLineLength0`, sprint speed) must be raised by the maximum knockback; otherwise a knocked-back player can look "teleported" to the line-start check.
2. The router's `Staggered` row must zero `Move` (M2 only guarantees RodFighting does; ruling C-Q7).
