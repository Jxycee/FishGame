# FishGame — Design Spec

- **Date:** 2026-10-09
- **Status:** Draft
- **Repo:** `C:\Dev\FishGame` (github.com/Jxycee/FishGame, branch `main`)
- **Engine:** Unity 6000.6.5f1 (Unity 6.6), URP 17.6.0, Windows 11

This file merges the reviewed section specs (vision and loop, networking, player, fishing, ecology, run loop, friend slop and expansion, art/audio/UI, project and testing) into one document. Where those sections disagreed, this document picks one answer, and section 3 records every pick. It is the source of truth that `CLAUDE.md` points to.

**Markers used throughout**

| Marker | Meaning |
|---|---|
| **[tune]** | A starting value for playtest tuning. All [tune] values live in ScriptableObject data, never in code (Core may hold test defaults only). |
| **[verify]** | An API name or engine behaviour to confirm in IntelliSense or in resolved package source during the named milestone. It does not block the design. |
| **[user]** | Needs the user: an action, an approval or a decision. |

---

## 1. Understanding

### 1.1 What the user said (agreed, not re-opened)

- **Game:** "FishGame", an incremental rogue-lite fishing game in the "friend slop" category (Peak, Lethal Company, Gamble With Friends, How to Fish, RV There Yet). Simple low-poly, flat-colour look. Primitives and procedural content where possible. No paid assets.
- **Players:** online co-op from the start, 1 to 4 players. Single-player is also available; solo is a local host with no Relay. Difficulty scales a bit with player count.
- **Netcode:** Netcode for GameObjects (NGO), host-authoritative. Connections go through Unity Multiplayer Services (Relay + Lobby, via the Sessions API) with a short join code. The transport layer stays swappable so Steam (Facepunch.Steamworks) can slot in later.
- **Camera and bodies:** first-person camera. Other players see low-poly floppy bodies.
- **Fishing:** replicates Rust (Facepunch) fishing. Relaxing; charge cast; bobber; bite; the fish pulls left or right; the player holds the opposite direction while reeling; line tension; the line snaps if overstressed.
- **Ecology:** fish spawn by time of day, season, location and weather, like Stardew Valley.
- **Run structure ("Quota expedition"; amended at review 2026-10-09, R53–R60):** the crew picks a map in the Hub from its unlocked maps and goes on a **7-day "fishing trip gone crazy"**. Day 1 is mundane, each day gets weirder, and day 7 is the map's climax. Each day is a full clock from dawn to night. Each day has a cash or weight quota that scales with player count and rises every day. After each of days 1–6 (and every Infinite day) there is a Night Camp (shop with bait, lures, gear and rods; forecast radio; story beat; ready-up). The host auto-saves there, so a crew can stop and resume another evening. Failing a quota ends the run. The season, weather and modifiers vary per run.
- **Permanent progression (amended 2026-10-09):** beating a map (passing day 7) gives each player one permanent **skill** (a passive ability, not an item) for that map. There are 5 maps eventually, so at most 5 skills. Runs, won or lost, also pay a meta currency that **only buys cosmetics** (hats, rod skins, bobber skins, emotes, colours). After a day-7 win the crew can choose **"Keep Fishing (Infinite)"**: the quota keeps rising until a day fails.
- **Top worry:** quota loops get boring if the mechanics don't flow. Variety and moment-to-moment feel are the top priority.
- **Milestones:**
  - **M1 Foundation:** project, packages, menu (Host / Join code / Solo), first-person player, lake-dock test map.
  - **M2 Fishing core:** Rust-style fishing.
  - **M3 Fish ecology:** data-driven spawn tables, journal.
  - **M4 Run loop (amended 2026-10-09):** 7-day expedition with an escalation arc, clock, rising quota, Night Camp (shop, radio, story, ready-up), nightly save/resume, Hub map choice and map unlocks, map skills with the skill pick, Infinite mode, cosmetics currency in the meta save (the cosmetics store itself may land in M5/M6).
  - **M5 Friend slop:** physics fish carry/sell; seagull and raccoon thieves; co-op monster fish needing 2+ reelers; daily events and hazards (storm, frenzy, fog, heatwave spoilage); shove and light ragdoll.
  - **M6 Expansion:** Vivox proximity voice; boat driving; boat damage with a repair minigame and bucket bailing (press a button to scoop, hold Q to throw water); more spots (amended: 5 maps in total, so three more maps from M6 on).
- **Workflow:** Claude writes all code and files directly. Scenes are built by editor scripts where practical. The Unity batchmode CLI compiles and runs tests. The user playtests in the editor. Tests use the Unity Test Framework (EditMode + PlayMode). Multiplayer Play Mode (MPPM) handles local multi-client testing.
- **Process:** The user said "go with your recommendation" about the milestone order and the networked-vertical-slice approach. The design defaults in section 19 are *proposed* defaults, to be confirmed at spec review; section 20 lists the open questions.

### 1.2 Assumptions (ours, changeable)

- Friends-only game: anti-cheat is a non-goal. Host checks exist so that honest-but-lagged requests resolve consistently and bugs are caught, not to stop cheaters.
- No host migration. If the host leaves, the live session ends for everyone. The run can be resumed from the host's last Night Camp save (R56).
- One 7-day expedition takes about 2 real hours: ~105 minutes of fishing (7 × 15 min) plus Settles and Night Camps. It is meant to be played over one or more evenings, with nightly save/resume between sittings.
- Windows PC only, keyboard/mouse and gamepad. 16:9 and 16:10 at 1280×720 and up.
- English only, with all player-facing strings in one string table.
- Comedic tone throughout. The main differentiator from the horror-tinged "Dreadline: Net Quota" (a 3-day co-op fishing quota game) is: comedy, Rust-style skill fishing, Stardew-style ecology, a 7-day trip that escalates into absurdity, and a season, weather track and modifiers rolled per run on a map the crew chooses.
- The user will link a Unity Cloud project and enable Relay and Lobby. Solo and dev-direct play never need it.

---

## 2. Vision and pillars

FishGame is relaxed, skill-based fishing under light group pressure. Reference points:
- **Rust** for fishing feel: a calm wait, an unmistakable bite, and a fight read from the rod, line and sound rather than from a meter.
- **Stardew Valley** for ecology: the same spot plays differently at dawn, noon and night, and in each season and weather.
- **Lethal Company, Gamble With Your Friends and Peak** for the quota-and-chaos loop: a deadline, a shared wallet, thieves, physics comedy, and failure that is fun to watch.

**Pillars, in priority order**
1. **Moment-to-moment feel.** Cast, wait, bite, fight and land must be fun with no quota at all. M2 is judged on this alone.
2. **Variety over repetition.** The quota must never be the only thing that changes between days or runs (section 13).
3. **Calm by default, chaotic on purpose.** Reeling stays calm enough to talk over. Pressure comes from the clock, events, thieves and the quota, never from a frantic minigame.
4. **Friend-slop readability.** Failure is funny and watchable. Catches are shared moments: everyone sees the fish and its weight. Every co-op-only piece of content has a solo path.

**Anti-goal.** Not a "shoot the fish" game (How to Fish). Chaos comes from thieves, physical hauls, monster fish and events, not violence.

**Variety design rule.** A variety layer must change at least one of: which fish appear, when they bite, how they fight, or where the crew goes and what it decides. A flat stat boost alone doesn't count (the Dredge principle).

### 2.1 Loops

- **Micro loop (one player, 20 s to ~2 min):** ready rod (hold RMB) → charge cast (hold and release LMB) → bobber lands → wait 4–18 s with 0–2 nibbles → loud bite, auto-hook → fight (counter-steer side runs, let dives run, rest at the creak) → auto-land at 2 m → catch reveal to the whole crew → into the creel (M2–M4) or into your hands (M5) → sell at the dock.
- **Meso loop (one in-game day, ~15 real min):** fish the day's time windows, react to the day's modifier and its beats, decide what to sell and what to hold, meet the quota before the end of the day.
- **Macro loop (one expedition, 7 days, ~2 h over one or more evenings):** Hub (choose a map from the unlocked maps, or resume a saved run) → Briefing (chosen map, rolled season, forecast, all seven quotas) → Day 1 → Settle → Night Camp (shop, forecast radio, story beat, ready-up, auto-save) → Day 2 → … → Day 7 (the map's climax) → Settle → map skill award and pick → "Keep Fishing (Infinite)" vote → Result and payout → Hub. If the vote passes, infinite Days 8+ repeat Day → Settle → Night Camp with a rising quota. Any failed Settle ends the run at once. Each Night Camp can end the sitting with Save & quit.
- **Meta loop (across runs):** each map won grants one permanent skill per player (max 5, R58). Map N's win unlocks map N+1 (R57). Cosmetic currency from every run buys cosmetics only (R59). The best infinite day per map is a bragging stat.

---

## 3. Resolution log

The section specs disagreed in these places. Each row is the single answer this spec uses everywhere.

| # | Conflict | Sections involved | Chosen | Why |
|---|---|---|---|---|
| R1 | Day length and clock span: 1200 min 06:00–02:00 over 900 s; 960 min 06:00–22:00; 1260 min 05:00–02:00 over 840 s | vision, runloop, ecology, art | **1200 in-game minutes, 06:00 → 02:00, over 900 real s (1.333 min/s).** Last Call at minute 1140 (01:00). | Keeps night windows reachable. With the 7-day trip (R53) a run is ~105 min of fishing plus camps (~2 h), played over evenings with nightly save/resume (R56). Ecology's six bands are re-laid over 1200 minutes and its 240-min weather segments become 5 per day (section 12.2). |
| R2 | Two different M2 fight models (Calm/Burst model with 5 species vs. a perch/bass/pike model with different formulas) | fishing, project-testing | **The fishing section's model** (`FightSim`, 50 Hz, section 9). Project-testing's tests are rewritten against it. | It is the more complete and better-reasoned model, with bot targets. |
| R3 | Fight archetype names (darter/sinker/bruiser/floater; Darter/Steady/Runner/Bulldog/Thrasher) and how M2 species relate to M3 profiles | vision, fishing, ecology | **Five shared `FightProfileDef` assets: Steady, Darter, Bulldog, Thrasher, Runner**, numerically seeded from the five M2 placeholder species (section 9.6). M3 species reference them. | One fight model, no re-modelling in M3. |
| R4 | Reach: `castMinDist` 4 m vs ≤ 2.5 m; tier-0 rod 12 m vs ≥ 14 m; which rod is the starter | fishing, ecology, runloop, project-testing | **`castMinDist` = 2.5 m. Rod tiers: Twig (tier 0, starter, 14 m), Handmade (tier 1, 18 m), Carbon (tier 2, 24 m), Deep-sea (tier 3, 30 m).** M2 tuning reference rod is Handmade. **From R59, rod tiers are in-run Night Camp purchases; every run starts on Twig.** | Near sub-areas stay reachable. Rods are the in-run gear progression across the week, so the ecology roster's tier gates (rod 1–2 fish) mark what opens up as the crew buys better rods during a run. |
| R5 | Sub-area signal for M3: baked shore-distance grid vs depth raycast | ecology, project-testing | **Shore-distance grid defines sub-areas.** Depth from the cast raycast is still measured and is an optional `minDepth` gate on spawn rules. | Ecology's roster and coverage tests were built on shore distance. |
| R6 | When fish become physical: M4 physical selling with coolers vs M5 | runloop, vision, player, agreed milestones | **Agreed milestones win: M2–M4 use a per-player creel** (host-held, replicated list). Physical fish arrive in M5. The M4 hold-vs-sell decision works on creel contents. | The user agreed physics carry is M5. |
| R7 | Quota semantics: a threshold on sales vs "paid, only surplus is spendable" | vision, runloop | **Threshold.** All sales go into one shared crew wallet; the quota is checked, not deducted. Listed as an open question (Q1). | Simpler to explain and makes the shop meaningful. Every purchase spends the shared in-run wallet (rods, bait, gear), so shopping is a crew decision. Meta currency is cosmetic only and is never spent or reduced by shopping (R59). |
| R8 | Quota kind: cash only vs rolled cash/weight per day | vision, runloop | **`QuotaKind` per day (Cash or Weight)**, rolled from `SpotDef.weightDayChance`. Junk counts 0 kg. | The agreed decision says "cash/weight quota", and weight days are a variety lever. |
| R9 | Day multipliers and jitter: [0.6, 1.0, 1.6] ±10% vs [1.0, 1.35, 1.8] ±8% | vision, runloop | **Superseded by R55: 7-entry `dayMult = [0.5, 0.7, 0.9, 1.1, 1.35, 1.6, 2.0]` [tune], jitter ±8%, infinite days `2.0 × 1.2^(d−7)`.** Base quotas are calibrated from the measured economy, not set as absolute numbers (section 14.4). | Day 1 is onboarding and day 7 the climax. `dayMult × jitter` is strictly increasing for every seed when the jitter half-width j < (r − 1)/(r + 1), where r is the smallest consecutive `dayMult` ratio. The curve's smallest ratio is 1.6/1.35 ≈ 1.185, so j < 0.0847 and ±8% passes. Infinite growth ×1.2 needs j < 0.0909, which ±8% also passes. Absolute bases in the drafts (500–800 coins) did not match the ecology economy. |
| R10 | Shops: before every day with starting cash vs between days with a starter kit | vision, runloop, networking | **Superseded by R56: a shop at every Night Camp (6 per 7-day trip, after days 1–6, plus one before each infinite day, from night 7 on). Every player starts the run with a free starter kit (Twig rod + worms); crew wallet starts at 0.** | Day 1 quota is low. A camp after every day keeps the pacing even across the longer trip and gives gear a steady rise (rods, Cooler, lures). Each camp is short (≤ ~3 min), so six camps plus seven Settles and LastCasts add ~15–20 min on top of ~105 min of fishing (~2 h). |
| R11 | Ending a day early: unanimous vote any time with a warning vs only after the quota is met | vision, runloop | **"Call it a day" vote is available only once today's quota is met**, and passes when every connected player votes yes. | An early end before the quota is only ever a guaranteed loss. |
| R12 | Day-end handling: 30 s grace plus auto-sale at 50% vs a LastCast phase with 45 s grace and overnight holding | vision, runloop, slop | **LastCast phase, 45 s grace.** Unsold creel fish carry over to the next day with a freshness penalty; the carry-over crosses Night Camp and is part of the nightly snapshot (R56). Unsold fish at the run's last Settle (Settle(7) on a declined Infinite vote, or the failing Settle) are worthless; in Infinite mode fish carry over between infinite days by the same rule. In M5, loose fish on the ground at day end are lost to gulls. | Holding fish overnight is the core hold-vs-sell decision. |
| R13 | Join windows: Hub only; Hub + Shop with rejoin; Camp + Briefing + Shop; any time | vision, networking, runloop, art | **Joins open in Hub, Briefing and Night Camp (amended by R56; Night Camp replaces Shop). A dropped player can rejoin in those phases and reclaims their slot, also after a resume on another evening (matched by persistent player id).** Joins are closed during Day, LastCast, Settle, SkillPick, InfiniteVote and Result. | Quota crew size is fixed at day start, so this is a near-free rejoin. |
| R14 | Mid-day disconnect: quota stays fixed / ratio rescale / time interpolation | vision, networking, runloop | **Time interpolation:** `Q' = Q(nNow) + (Q(nStart) − Q(nNow)) × elapsedFraction`. | One flaky connection doesn't end the run, and it is fair to the time already fished. |
| R15 | Phase enums and controller names (ExpeditionPhase/RunPhase ×2; ExpeditionController/RunDirector/RunState) | vision, networking, runloop | **`RunPhase` (Core) and `RunDirector` (Gameplay, scene-placed in each Spot scene).** Hub is a session phase, not a run phase. | One name per concept. |
| R16 | Roster lifetime | networking, runloop | **`SessionState`** NetworkObject, spawned by the host at session start with `destroyWithScene: false`, holds the `Roster`. | The roster must survive Hub ↔ Spot loads. |
| R17 | Scene names: Hub / Camp / Game_LakeDock / Spot_<id> | networking, runloop, project-testing | **`Bootstrap`, `Menu`, `Hub` (from M4), `Spot_LakeDock`, `Spot_HarbourPier` (M3), and three more `Spot_<Id>` maps from M6 (R57).** In M1–M3 the host loads `Spot_LakeDock` directly and it acts as the hub. | One naming scheme. |
| R18 | Fishing RPC flow: four different sets; roll at cast accept / bobber settle / bite | fishing, ecology, networking, project-testing | **The fishing section's set** (`CastRequestRpc`, `CastResultRpc`, `CancelRpc`, `HookedRpc`, `ResultRpc`, `CommitRpc`, `CatchAnnouncedRpc`, `RodEventRpc`). **The host rolls at cast accept**, using the computed landing point (the flight is deterministic, so this equals the settle point). Ecology's settle validations fold into `CastRequestRpc`. | Gives the owner the fight profile before the bite with one round trip. |
| R19 | `castId` type: ushort vs uint | fishing, ecology, networking | **`uint`**, monotonic per player. | No wrap handling needed. |
| R20 | Remote one-shot cues: reliable `RodEventRpc` vs sequence counters inside RodState | fishing, art | **Reliable `RodEventRpc`** (`SendTo.NotMe`). RodState carries continuous state only. | Simple, and rare events cost nothing reliable. |
| R21 | Latency testing: `UnityTransport.SetDebugSimulatorParameters` vs Network Simulator | networking, fishing, art vs player, slop, project-testing | **Network Simulator from `com.unity.multiplayer.tools` 2.2.12.** The UnityTransport debug simulator is `[Obsolete]` and has no effect in NGO 2.13.3 (checked in source). | Verified against source. |
| R22 | UI technology: uGUI + TextMeshPro vs UI Toolkit | art, project-testing | **UI Toolkit** (UXML/USS text, default runtime theme font). uGUI stays installed, unused. Art's layout, flow and HUD content are kept and implemented in UI Toolkit. | TMP Essential Resources are not imported and bring a font that falls under LFS rules. UXML/USS are plain text. |
| R23 | Art pipeline scope in M1 (toon/water shaders, palette, day-night, procedural audio all in M1) vs a lean M1 | art, project-testing | **M1 lean:** flat URP Lit placeholder materials, one fixed light, `AudioBus` with UI and ambience recipes. **M2:** toon and water shaders, ripples, rod/line presentation, fishing audio. **M3:** seasonal palette swap and day-night controller (driven by the dev clock). | The agreed M1 scope is project/menu/player/map; M2 is judged on feel and needs the water and audio. |
| R24 | Player names: PlayerFP/PlayerNetState/PlayerActions vs PlayerNet vs FirstPersonPlayer vs PlayerInteractor | player, networking, project-testing, slop | **Prefab `PlayerFP`** with `PlayerNetState` (NetworkVariables), `PlayerActions` (all non-fishing player RPCs), `PlayerLocomotion`, `PlayerCamera`, `PlayerBody`, `PlayerInteractor`, and `FishingRodNetwork` (fishing RPCs and rod state). | One set of names. |
| R25 | Movement numbers: walk 4.2 vs 4.5, gravity −22 vs −18, eye 1.62 vs 1.6, pitch ±85 vs ±80 | player, project-testing | **Player section values:** walk 4.2, sprint 6.6, gravity −22, jump 1.1 m, eye 1.62, pitch ±85. | The player section is the more detailed one. |
| R26 | FOV: 75 vertical (60–100) vs 90 horizontal (80–110) | player, art | **Horizontal 90° default, range 80–110°**, converted with `Camera.HorizontalToVerticalFieldOfView`. | Consistent across aspect ratios. |
| R27 | Motion comfort: "Camera motion" 0–1 slider vs "Reduced motion" bool | player, fishing, art | **One "Camera motion" slider (0–1).** 0 disables head bob, shake, FOV kicks, rumble and lightning flashes. | One setting. |
| R28 | Body states in M5: server-run Upright/Staggered/Ragdolled with Downed deferred vs owner-write states with Downed, slap revive and seagull carry | player, slop | **Server-run `PlayerStateMachine`** with `Upright`, `Staggered`, `Downed`, `Carried`. Swimming is a separate owner-write locomotion flag. A player who is fighting a fish is only ever Staggered, never Downed. | Server authority for shared state; slop's comedy beats kept; the core loop protected. |
| R29 | Value and freshness: four value formulas, two freshness models (game-min vs real-sec) | fishing, ecology, runloop, slop | **Catch value is rolled once (ecology formula). Sale price = rolled value × freshness × condition × market × event.** Freshness decays per **in-game minute** (runloop model). | One formula; time-based effects follow the game clock. |
| R30 | Meta payout: full amount to every player vs split pool plus flat bonuses | vision, runloop | **Amended by R59: the payout is cosmetic currency ("Shells" [tune]). Every connected player earns the same flat amount per met day, and the same win bonus. Nothing is split and nothing is converted from the wallet** (proposed default, section 14.8). Open question Q2, re-scoped. | Per-head pace is the same solo and in co-op. Currency buys only cosmetics, so pace matters less. |
| R31 | Failed-run payout: 50% conversion vs full conversion at the failing settle | vision, runloop | **Amended by R59/R60: no conversion. Shells for every met day are paid at that day's Settle and kept whatever happens later. Only a day-7 win adds the win bonus (and the map skill). Infinite days pay per day survived.** | A failed run still pays, and a win is worth more. |
| R32 | Save schema: two different v1 shapes | ecology, runloop | **`meta.json` v1 in M3 (journal only); v2 in M4 (amended by R56–R60) adds cosmetic currency, owned cosmetics, unlocked maps, `skillByMap[5]`, best infinite day per map, stats, cosmetic loadout and a pending-run snapshot reference.** The run snapshot itself is a separate host-only file (`run_snapshot.json`). v2 has not shipped, so it is redefined in place (no v3). | M3 writes the file first. |
| R33 | Journal: 4 hint levels vs 3 card states | ecology, art | **Ecology's 4 hint levels.** | Finer progression. |
| R34 | Solo pause: `Time.timeScale = 0` vs never touching timeScale | vision, art | **Never touch `Time.timeScale`.** Solo pause writes a clock anchor with rate 0 and sets a `Paused` flag read by locomotion and the fishing tick. | NGO tick timing must not be scaled. |
| R35 | Content check: 64-bit hash in the connection payload vs 32-bit hash on WorldState | networking, ecology | **64-bit FNV-1a `ContentHash` in the connection payload**, checked by connection approval. No WorldState check. | The client is rejected before it joins. |
| R36 | Line gauge: shop gadget vs free accessibility setting | fishing, runloop, art | **Free accessibility setting (Off/Arc/Bar).** The shop sells a different gadget, the Fish Finder (shows hooked fish stamina). | Accessibility must not be paywalled. |
| R37 | Monster solo rule: stamina scaling table vs automatic solo profile | vision, slop | **Automatic solo profile** (run speed ×0.5, stamina ×0.5) when 1 player is connected; co-op stamina scales ×1.0/×1.2/×1.35 for 2/3/4 players. | Solo never hits a co-op-only gate. |
| R38 | Thief scaling: multiplier table vs interval formula | vision, slop | **Slop's interval formula** in `CrewScaling` (section 15.2). | One source. |
| R39 | Session classes and interface: `UgsNetworkSession`/`LocalNetworkSession` vs Sessions/Solo/Direct sessions; two `INetworkSession` signatures | networking, project-testing | **`UgsNetworkSession` and `LocalNetworkSession` (solo and dev-direct)**, networking's `INetworkSession`. | Networking's version is complete. |
| R40 | Season name: Fall vs Autumn | ecology, art | **Autumn.** | |
| R41 | Asmdef list: Presentation missing, App missing | art, project-testing | **Both added** (section 4.3). | Presentation must read Gameplay and Fishing state; the composition root needs everything. |
| R42 | Fight tick: 60 Hz vs 50 Hz | networking, fishing | **50 Hz (dt 0.02 s)**, accumulator in `Update`, max 5 steps per frame. | |
| R43 | Host fight timeout: 240 s vs 300 s | fishing, networking | **240 s.** | |
| R44 | RodState rate: 15 Hz vs 20 Hz | fishing, project-testing | **15 Hz.** | |
| R45 | Cast cancel refunds bait | fishing, project-testing | **Refunds** (proposed default; Rust consumes it). | Friendlier co-op. |
| R46 | Mid-day drop creel contents: lost vs kept | vision, networking | **Kept in the host's slot ledger** and returned on rejoin; not counted unless sold. The ledger, creels included, is part of the nightly snapshot, so it is also returned after a resume (R56). | Keyed by slot, costs nothing. |
| R47 | Fall damage downs a player (slop) vs no fall damage (player) | slop, player | **No fall damage.** | Simpler; shoves and thrashes are enough. |
| R48 | Shove range and cooldown: 2.2 m/50° cone, 1.0 s vs 1.8 m, 1.5 s vs 2.5 m, 1.5 s | player, slop, networking | **2.2 m, half-angle 50°, server slack +0.8 m/+15°, cooldown 1.5 s.** | |
| R49 | Run state persisted on host quit: forfeited vs banked | networking, runloop | **Amended by R56: a run snapshot is saved, not banked.** The host writes `run_snapshot.json` at every Night Camp, and the crew resumes from the Hub. Shells are paid at each met Settle, so no pending payout needs banking. | A crash or a quit never costs more than the current day. |
| R50 | Weather forecast: rolled per day vs pre-rolled per run | ecology, vision | **The planner pre-rolls the whole 7-day weather track (35 segments) at Briefing; infinite days continue the chain one day at a time.** The forecast is revealed a day at a time on the Night Camp radio. The track is stored in the run snapshot. | Deterministic from the run seed. |
| R51 | Value of junk and fish weight units | fishing, ecology | **Weight stored as integer grams.** Junk 0–5 coins, 0 kg. | Save- and wire-friendly. |
| R52 | Daily variety in M4: data-only modifiers vs market board + daily request | vision, runloop | **M4 ships the market board (price drift + one bounty species), 8 data-only daily modifiers (amended: 4 cannot cover 7 days without repeats) and per-map escalation tiers per day (R54) that bias modifier rolls and intensity.** Daily requests and hazards arrive in M5. | Variety in M4 playtests with no M5 system work. |

### 3.1 Amendment log (user review, 2026-10-09)

The user decided these at spec review. They override every earlier row and section where they conflict, and the sections named have been updated to match. Choices this spec makes to fill in the details are **proposed defaults** in section 19 (items 22–33), not part of these decisions.

| # | Amendment | Decision (user) | Updated in |
|---|---|---|---|
| R53 | A1 Run length | **A 7-day "fishing trip gone crazy"** (was 3 days). The day clock is unchanged: 1200 in-game minutes, 06:00 → 02:00, over 900 real s. A trip is ~105 min of fishing plus Night Camps (~2 h), played over one or more evenings. | 1, 2.1, R1, 5, 11.4, 14, 17.4, 20 (Q6) |
| R54 | A2 Story and escalation | Each day gets weirder: day 1 is mundane, then absurd events and modifiers escalate through the week, and day 7 is the climax (the map's signature boss, legendary fish or event). **Escalation is data-driven per map:** an escalation tier per day biases which daily modifiers and happenings roll, and how intense they are. The tone stays comedic. | 13, 14.5, 15.4, 9.4 |
| R55 | A3 Quota | A Settle every night, so 7 per trip. **`dayMult` = [0.5, 0.7, 0.9, 1.1, 1.35, 1.6, 2.0] [tune]**, jitter ±8%. The jitter is bounded so `dayMult × jitter` is strictly increasing day to day for every seed (bound: j < (r − 1)/(r + 1), where r is the smallest consecutive ratio; here r = 1.185, so j < 0.0847). Player-count scaling is unchanged. Any failed Settle ends the run. | R9, 14.4, 14.11 |
| R56 | A4 Night Camp and save/resume | After each day's Settle comes a **Night Camp** (shop, forecast radio, story beat, ready-up). The host auto-saves a run snapshot at every Night Camp. A crew can quit and resume another evening from the Hub: the host loads the snapshot, and players reclaim their slots by persistent player id. Joins open in Hub, Briefing and Night Camp. There is a shop at every Night Camp (6 per trip). This replaces old Q7 (answered: yes) and the old "banked pending payout" rule. | R10, R12, R13, R49, 6, 9.13, 11.5, 14.1, 14.2, 14.9 |
| R57 | A5 Maps | **5 maps eventually**: `Spot_LakeDock` first, `Spot_HarbourPier` in M3, three more from M6. **The map is chosen in the Hub** from unlocked maps. Season, weather track and modifiers are still rolled per run. Beating map N unlocks map N+1 [proposed default]. A map is a `SpotDef` plus its `Spot_<Id>` scene (no separate MapDef type). | 1, 6.2, 12.3, 12.5, 13.1, 14.1, 14.8, 16.6, 17.6 |
| R58 | A6 Permanent progression = map skills | Beating a map (passing day 7's Settle) grants each player in the roster at that Settle **one permanent skill** (a passive ability, not an item) for that map. "In the roster" means every slot of the run's roster at that Settle, connected or not (a disconnected slot is still reclaimable by persistent id, 6.6). Connected players are awarded at once; a disconnected roster member's award is held by the host as a **pending clear** and delivered when that player's machine next connects to the same session or resumes the snapshot (14.8), because each save is only ever written by its own machine. Free pick from the whole pool (~12 skills; no duplicates across a player's slots). One skill slot per map, so 5 skills at most. Re-beating a map lets the player keep or re-choose that map's slot. **Skills are the only permanent power progression.** Skills never change crew scaling or quotas. | 14.8, 14.9, 9.8, 9.9 |
| R59 | A7 Meta currency = cosmetics only | Runs, won or lost, pay a meta currency that **only buys cosmetics** (hats, rod skins, bobber skins, emotes, colours). No permanent rods, perks or map unlocks are bought with currency. Rod tiers (Twig/Handmade/Carbon/Deep-sea) become **in-run purchases at Night Camp shops**, and every run starts with the Twig starter kit. `meta.json` holds cosmetic currency, owned cosmetics, unlocked maps, `skillByMap[5]`, best infinite day per map, journal, stats and a pending-run snapshot reference. | R4, R7, R30–R32, 9.7, 14.7, 14.8, 14.10 |
| R60 | A8 Infinite mode | After a day-7 win, the crew is offered **"Keep Fishing (Infinite)"** by unanimous vote (otherwise the run ends as a win). The map skill is awarded at the day-7 win, **before** Infinite starts, so it can't be lost. Days 8+ use the same day loop. The quota keeps rising (`dayMult[d] = 2.0 × 1.2^(d−7)` [tune]) and escalation stays at the top tier with crazier modifier stacks, until a Settle fails. Infinite pays cosmetic currency per day survived and records the best day reached per map. The nightly save applies in Infinite too. | 14.2, 14.4, 14.8, 15.4 |

---

## 4. Engineering foundation

### 4.1 Session pre-flight rules

1. **Editor lock.** Before any batchmode run, test whether `Temp/UnityLockfile` is held open (`[IO.File]::Open(path, 'Open', 'ReadWrite', 'None')` in PowerShell).
   - Locked: the editor is open. Ask the user to close it, or use editor-open mode (4.8).
   - Present but not locked: stale from a crash; proceed.
   - Never kill Unity processes.
2. **Encoding.** Every text file (`.cs`, `.json`, `.asmdef`, `.inputactions`, `.uxml`, `.uss`, `.shader`, `.hlsl`, `.md`, `.meta`) is UTF-8 **without a BOM**. Windows PowerShell 5.1 `Set-Content`/`Out-File -Encoding utf8` writes a BOM and UPM then rejects `manifest.json`. Use the Write tool or `[IO.File]::WriteAllText(p, t, (New-Object System.Text.UTF8Encoding($false)))`.
3. **Paths.** Pass absolute paths to Unity `-logFile` and `-testResults`; paths with `..` were rejected empirically.
4. **Commits.** Only when the user asks or a plan step says so. On `main`. Never force-push. Commit messages end with a `Co-Authored-By:` trailer naming the model that wrote the change (implementation commits by Sonnet: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`).
5. **Agent roles (user preference):** Haiku swarms for research and drafting, each watched by an Opus (high effort) reviewer; Sonnet implements; Fable is the escalation fixer for anything the others can't solve.

### 4.2 Project cleanup (M1, before the first commit)

The URP Blank template is already cloned at the repo root; `.git` exists with origin set, branch `main`, no commits.

1. Delete `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings/SampleSceneProfile.asset`.
2. Clear the project-wide input actions entry (`InputSystem.actions = null` from an editor script [verify the setter in 1.20.0], or remove the `com.unity.input.settings.actions` config object), then delete `Assets/InputSystem_Actions.inputactions`.
3. Manifest: remove `com.unity.visualscripting` and `com.unity.collab-proxy`; add the pins in 4.4.
4. **Domain reload back on:** `EditorSettings.enterPlayModeOptionsEnabled = false`. The template ships with domain and scene reload off, which would leak NGO singletons, `AuthenticationService`, static events and registries between Play sessions. Revisit only once every static resets through `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
5. URP: set `PC_RPAsset` MSAA to 4x; keep Depth Texture on; HDR on; soft shadows, 2 cascades, shadow distance 70 m; Opaque Texture off. Leave the Mobile assets alone.
6. Already correct (asserted by the validator, 4.7): Force Text serialization, Input System only (`activeInputHandler: 1`), renderer CopyDepth After Opaques (`m_CopyDepthMode: 0`), Forward+ (`m_RenderingMode: 2`). Custom shaders must therefore support `_CLUSTER_LIGHT_LOOP`.
7. Steps 2, 4 and 5 are done by `ProjectSettingsBuilder`, not by hand-editing YAML.
8. (Resolved 2026-10-09: there is no stray `.tgz` at the repo root. The `com.unity.netcode.gameobjects-2.13.3.tgz` that exists is the editor's own bundled copy under `Editor/Data/Resources/PackageManager/Editor/`; leave it alone.)

### 4.3 Folder layout and assembly definitions

```
Assets/_Project/
  Core/          FishGame.Core          pure C#, noEngineReferences
  Data/          FishGame.Data          ScriptableObject types + ContentRegistry
  Content/       (no code)              .asset, .mat, generated data
  Runtime/       FishGame.Runtime       ServiceRegistry, EventBus, INetworkSession, InputService, SaveService, AudioBus, MeshBuilder, WaterSurface
    Input/                              FishGameInput.inputactions + generated FishGameInput.cs
  Networking/    FishGame.Networking    UgsNetworkSession, LocalNetworkSession, ConnectionApprover
  Gameplay/      FishGame.Gameplay      PlayerFP components, PlayerSpawner, SessionState, WorldState, RunDirector, interactables
  Fishing/       FishGame.Fishing       FishingRodNetwork, FishingRodController, rod/line/bobber views
  Presentation/  FishGame.Presentation  DayNightController, WaterTimeDriver, RippleField, RodPresenter, SfxPlayer, WeatherFx
  UI/            FishGame.UI            UI Toolkit menus/HUD (+ .uxml/.uss)
  App/           FishGame.App           AppBootstrap (composition root), GameScope
  Shaders/                              (M2) Toon, Water, Line, GradientSky
  Scenes/  Prefabs/                     builder output
  Editor/        FishGame.Editor        builders, validators, menu items, CLI entry points (namespace FishGame.EditorTools)
  Tests/EditMode/  FishGame.Tests.EditMode
  Tests/PlayMode/  FishGame.Tests.PlayMode
Tools/unity.ps1                         CLI wrapper (committed, outside Assets)
```

**Dependency direction:** Core ← Data ← Runtime ← Networking, Gameplay ← Fishing ← Presentation ← UI ← App. Editor and Tests sit on top. References are not transitive.
- Gameplay never references Fishing, Presentation or UI.
- Fishing references Gameplay (to set the player's input context) but never Networking.
- Presentation reads Gameplay and Fishing state (NetworkVariables, read-only properties, C# events) and never writes gameplay state (enforced by code review).
- UI talks to sessions only through `INetworkSession` (Runtime).

| Asmdef | References | Notes |
|---|---|---|
| FishGame.Core | none | `noEngineReferences: true`. `System.Math`, `System.Numerics`, own seeded RNG. Compiler-enforced. |
| FishGame.Data | Core | |
| FishGame.Runtime | Core, Data, Unity.InputSystem | Newtonsoft is an auto-referenced precompiled DLL; do not set `overrideReferences`. |
| FishGame.Networking | Core, Runtime, Unity.Netcode.Runtime, Unity.Networking.Transport, Unity.Services.Core, Unity.Services.Authentication, Unity.Services.Multiplayer | The only assembly that touches UGS. `UnityTransport` itself lives in Unity.Netcode.Runtime. |
| FishGame.Gameplay | Core, Data, Runtime, Unity.Netcode.Runtime, Unity.Collections | `FixedString` types need Unity.Collections. |
| FishGame.Fishing | Core, Data, Runtime, Gameplay, Unity.Netcode.Runtime | |
| FishGame.Presentation | Core, Data, Runtime, Gameplay, Fishing, Unity.Netcode.Runtime, Unity.Collections, Unity.RenderPipelines.Universal.Runtime, Unity.RenderPipelines.Core.Runtime (`Volume`) | Rumble goes through `IInputService.SetRumble(low, high)` (Runtime), so Presentation never references the Input System. |
| FishGame.UI | Core, Data, Runtime, Gameplay, Fishing, Presentation, Unity.InputSystem | UI Toolkit is an engine module; no reference needed. Input System only for binding display strings and interactive rebinding, never for gameplay input. |
| FishGame.App | all of the above, plus the MPPM runtime assembly for `CurrentPlayer` (V6; code under `#if UNITY_EDITOR`) | |
| FishGame.Editor | all runtime asmdefs, Unity.Netcode.Runtime, Unity.Netcode.Editor (prefab-list setting), MPPM assembly (V6) | `includePlatforms: ["Editor"]`. Namespace `FishGame.EditorTools`, because a namespace `FishGame.Editor` shadows `UnityEditor.Editor` (CS0118). |
| FishGame.Tests.EditMode | UnityEngine.TestRunner, UnityEditor.TestRunner, Core, Data, Runtime, Editor, Unity.InputSystem (`.inputactions` `FromJson` test) | `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]` (+ `Newtonsoft.Json.dll` when save tests arrive), `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. |
| FishGame.Tests.PlayMode | UnityEngine.TestRunner, Core, Data, Runtime, Networking, Gameplay, Fishing, App, Unity.Netcode.Runtime | Same shape, `includePlatforms: []`. |

No runtime code in Assembly-CSharp (test asmdefs cannot reference it).

**Note for CLAUDE.md [user doc tweak]:** its example `-executeMethod FishGame.Editor.<Class>.<Method>` should read `FishGame.EditorTools.<Class>.<Method>`.

### 4.4 Packages (`Packages/manifest.json`, written without a BOM)

| Package | Version | Why direct |
|---|---|---|
| com.unity.netcode.gameobjects | 2.13.3 | NGO (bundled). Never 3.x, which needs editor 6000.7. |
| com.unity.services.multiplayer | 2.3.3 | Sessions (Relay + Lobby), join codes. Re-check 2.4.0 after 2026-10-22. |
| com.unity.services.authentication | 3.8.0 | Referenced directly by Networking |
| com.unity.services.core | 1.18.0 | Referenced directly by Networking |
| com.unity.multiplayer.playmode | 3.0.0 | MPPM (built in) |
| com.unity.multiplayer.tools | 2.2.12 | Network Simulator for latency tests |
| com.unity.nuget.newtonsoft-json | 3.2.2 | SaveService |
| com.unity.inputsystem | 1.20.0 | Template |
| com.unity.render-pipelines.universal | 17.6.0 | Template |
| com.unity.test-framework | 1.8.0 | Template |
| com.unity.ugui | 2.6.0 | Template; kept, unused |
| com.unity.ai.navigation | 2.0.15 | Template; raccoon NavMesh in M5 |
| com.unity.timeline, com.unity.ide.rider, com.unity.ide.visualstudio | as in template | Harmless |
| com.unity.pipeline | 0.8.0-exp.1 | Kept; nothing depends on it (4.8) |

- `com.unity.transport` resolves transitively to 6.6.0. Do not list it.
- **Later:** `com.unity.services.vivox` 16.12.1 (M6).
- **Never add:** `com.unity.services.relay`, `.lobby`, `.matchmaker`, `.multiplay`, `com.unity.multiplayer.widgets`, `com.unity.textmeshpro`, `com.unity.cinemachine` (the first-person camera is a plain Camera with a custom trauma shake).
- Commit `packages-lock.json`.
- **[verify M1]** the lock matches this table; exact asmdef names inside multiplayer, authentication, core and tools; multiplayer.tools 2.2.12 compiles against NGO 2.13.3.

### 4.5 Naming and file rules

- One MonoBehaviour, NetworkBehaviour or ScriptableObject per file; file name equals class name. Keep other classes one per file too, under ~300 lines.
- RPC methods end in `Rpc`. NetworkVariable fields are prefixed `Net` (for example `NetPitchQ`).
- Every networked class opens with a class comment stating its authority (host or owner).
- Do not reuse Unity type names: no `PlayerInput` (use `PlayerInputFrame`), `BuildPipeline`, `NetworkManager`.
- ScriptableObject types end in `Def` (`RodDef`, `BaitDef`, `FishSpeciesDef`, `FightProfileDef`, `FishingZoneDef`, `JunkDef`, `SpotDef`, `ShopItemDef`, `EventDef`, `RunConfigDef`, `FishingTuningDef`, `WeatherTableDef`, `ArtPaletteDef`, `SfxCueTableDef`, `WaterSettingsDef`, `ShoreGridDef`, `DayBeatDef`, `RequestDef`, and from M4 `SkillDef` (map skill pool), `CosmeticDef` (hats, rod and bobber skins, emotes, colours) and `EscalationTierDef` (per-day escalation tier)). Each maps into a plain Core struct; Core never sees assets. A map is a `SpotDef` (with map order, climax and escalation fields, 12.3); there is no separate MapDef.
- **Ids:** stable lowercase snake_case string ids (`lake_dock`, `rod_handmade`) are save and network keys. `ContentRegistry` assigns `ushort` indices by ordinal-sorted id (`StringComparer.Ordinal`) per category, so host and clients agree. Never send ScriptableObject references, asset paths or GUIDs.
- **Versioning:** `BuildInfo.ProtocolVersion` (int, bumped by hand on any wire, RPC or networked-prefab change) goes **only into the connection payload**. `NetworkConfig.ProtocolVersion` stays at a constant 0. Reason (checked in NGO 2.13.3 source, `ConnectionRequestMessage.Deserialize`): NGO compares the config hash (`NetworkConfig.ProtocolVersion`, tick rate, approval flag, and the prefab list when `ForceSamePrefabs` is on) **before** the approval callback runs. On a mismatch it calls `DisconnectClient` with no reason string, so our version message would never reach the client. `ContentHash` = FNV-1a 64 over ordinal-sorted ids of every registry category plus a manually bumped `dataVersion`, computed at boot.
- Core RNG: `Pcg32` (PCG-XSH-RR, seed never 0). Streams derive with `Rng.Derive(runSeed, StreamId, index)` = SplitMix64 over FNV-1a of a constant stream id, so extra draws in one system never shift another's rolls. `UnityEngine.Random` and `System.Random` are not used in Core.
- Test names: `Subject_Condition_Expected`.

### 4.6 Scenes, prefabs and editor-script authoring

**Scenes (build order):** `Bootstrap` (index 0), `Menu`, `Spot_LakeDock`; M3 adds `Spot_HarbourPier`; M4 adds `Hub`; M6+ adds the three remaining `Spot_<Id>` maps (R57).

**One entry point:** `FishGame.EditorTools.ProjectBuilder.BuildAll`, which in batchmode ends with `EditorApplication.Exit(0 or 1)`:
1. `ProjectSettingsBuilder`: layers (via the TagManager SerializedObject), physics matrix, Enter Play Mode options, URP quality, input actions entry, `runInBackground = true`, Netcode settings.
2. `ContentBuilder`: data assets. **Create-if-missing only**; never overwrite an existing tuning asset, because the user tunes those in the Inspector.
3. `PrefabBuilder`: player prefab and other prefabs.
4. `NetworkPrefabsBuilder`: registers every networked prefab in an owned list at `Content/Network/FishGameNetworkPrefabs.asset`, with NGO's auto-generated default list turned off.
   - The setting is `Unity.Netcode.Editor.Configuration.NetcodeForGameObjectsProjectSettings.instance.GenerateDefaultNetworkPrefabs` (a public field, checked in 2.13.3 source; stored in `ProjectSettings/NetcodeForGameObjects.asset`).
   - Its `SaveSettings()` is internal. Persist it by reflection, or by writing that small YAML file.
   - Left on, NGO creates `Assets/DefaultNetworkPrefabs.asset`, which breaks invariant 4.7 #7. Fallback: point the public `NetworkPrefabsPath` at `Assets/_Project/Content/Network/DefaultNetworkPrefabs.asset` and validate that list.
5. `SceneBuilder`: all scenes.
6. `EditorBuildSettings.scenes` in build order.
7. `ValidationRunner.Run()`: fails on any invariant break.

**Layers:** `Player`, `RemotePlayer`, `Water` (trigger volume), `Ground` (lakebed), `Shore` (banks), `Structure` (docks, piers, posts), `Interactable`, `Fish` (M5), `Ragdoll` (M5). Physics matrix: `Fish` ignores `Player` and `RemotePlayer`; `Ragdoll` ignores `Player`, `RemotePlayer` and `Fish`.

**Overwrite rules (GUID stability):**
- Never `AssetDatabase.DeleteAsset` then recreate (that changes the GUID).
- Scenes: `NewScene(EmptyScene, Single)`, build, `SaveScene(scene, samePath)`.
- Prefabs: `LoadPrefabContents`, rebuild **reusing existing components** (`GetComponent`, else `AddComponent`), `SaveAsPrefabAsset`, `UnloadPrefabContents`. Never destroy and re-add a root `NetworkObject` (its `GlobalObjectIdHash` would change).
- Serialized fields: `SerializedObject` + `ApplyModifiedPropertiesWithoutUndo()`.
- Builders are deterministic (seeded placement) so rebuilds don't churn diffs.
- **Generated scenes are disposable output.** Hand edits are lost on the next build; to keep a tweak, change the builder or a data asset. The README says this.
- No in-scene-placed NetworkObjects in M1–M2. From M3, `WorldState` and from M4 `RunDirector` and the interactables are scene-placed (built by SceneBuilder).
- **Until M4, networked interactables (the `DockBell`) are network prefabs.** SceneBuilder places only an empty `NetSpawnAnchor` marker (with a prefab id). The host-only `PlayerSpawner` also spawns one instance per anchor on `OnLoadEventCompleted`, with `destroyWithScene: true`.

**Play from any scene:** an `[InitializeOnLoad]` script sets `EditorSceneManager.playModeStartScene` to Bootstrap. A dev toggle "Auto Solo" (EditorPrefs) applies only in the main editor (`CurrentPlayer.IsMainEditor` [verify namespace spelling `Unity.Multiplayer.PlayMode` vs `Unity.Multiplayer.Playmode`]); MPPM clones share EditorPrefs and must not auto-solo.

### 4.7 Validation invariants (EditMode tests and `BuildAll`)

1. Ids unique per category; every reference resolves.
2. Every networked prefab is in the network prefab list; the NetworkManager's list reference is set.
3. Build scenes exist in order with Bootstrap at index 0.
4. Required input maps and actions exist (`InputActionAsset.FromJson(File.ReadAllText(path))`).
5. The manifest contains the 4.4 pins and none of the never-add packages.
6. Settings: Force Text, Input System only, CopyDepth After Opaques, domain reload on, `runInBackground` on.
7. No asset under `Assets/` outside `_Project` except `Assets/Settings`.
8. From M3: spawn-rule and coverage validators (section 12.9).
9. Shaders: `FishGame > Validate Shaders` (`ShaderValidator.Run`, batchmode-callable) checks every `FishGame/*` shader with `ShaderUtil.ShaderHasError`/`GetShaderMessages`. The first Windows development player build is the authoritative variant check.

### 4.8 CLI pipeline

`Tools/unity.ps1` (PowerShell 5.1 compatible):

| Subcommand | Unity arguments |
|---|---|
| `compile` | `-batchmode -nographics -quit` |
| `build` | `compile` + `-executeMethod FishGame.EditorTools.ProjectBuilder.BuildAll` |
| `test-edit` | `-batchmode -nographics -runTests -testPlatform EditMode -testResults <out>/editmode.xml` |
| `test-play` | same with `-testPlatform PlayMode` |
| `check` | build → test-edit → test-play, stopping at the first failure |

- Every subcommand also passes `-projectPath "<repo root>"` and `-logFile "<OutDir>\<name>.log"` (absolute).
- Required `-OutDir` (the session scratchpad) receives `<name>.log` and XML. Logs and results never go in the repo.
- Never name a variable `$args`; use `$unityArgs`. Quote paths inside argument strings.
- `$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow; $null = $p.Handle; $p.WaitForExit($timeoutMs)`. Timeouts: 10 min after a package change, 5 min otherwise. On timeout, report the PID and ask the user; never kill it.
- Run the lock-file check first. **Never pass `-quit` with `-runTests`.**

**Success criteria (exit code alone is not proof):**
- compile/build: exit 0 **and** the log contains `Exiting batchmode successfully` **and** no `error CS`. On failure grep for `error CS`, `Exception`, `Scripts have compiler errors`.
- tests: the line `Test run completed. Exiting with code N` plus the NUnit `<test-run result passed failed>` attributes. Codes: 0 all passed, 2 failures, 1 setup failure, 3 run error, 4 platform not found.

**Editor-open mode** (the user keeps the editor open):
- Claude writes files; Unity recompiles when the editor regains focus.
- An `[InitializeOnLoad]` hook on `CompilationPipeline.assemblyCompilationFinished` writes `Logs/FishGame/compile.txt` (gitignored) with errors and warnings. Claude reads it.
- Menu items: `FishGame > Build All`, `FishGame > Run EditMode Tests` (via `TestRunnerApi`, results to `Logs/FishGame/editmode.xml` via `TestRunnerApi.SaveResultToFile` [verify in 1.8.0]), `FishGame > Run PlayMode Tests`.
- **Optional spike (M1, 1 hour, [user]):** `com.unity.pipeline` exposes the running editor over localhost (ports 7800–7849) for build/run_tests/play, driven by a beta `unity` CLI the user installs from Unity's CDN. If it works it replaces the menu clicks; if not, the package is removed.

### 4.9 Testing strategy

| Layer | Covers | From |
|---|---|---|
| EditMode, pure Core | State machines, FightSim, bite planner, validators, RNG, spawn resolver, quota, run state, save migrations, scaling, event rolls | M1 |
| EditMode, project | 4.7 validators, join-code normalization, registry index stability, architecture scan | M1 |
| EditMode `[Category("Tuning")]` / `[Category("Balance")]` | Distribution reports over hundreds of seeds, printed to the test output; fail only on gross regressions. Claude tunes data from these before playtests. From M4 also: the 7-day quota curve report (`dayMult × jitter` strictly increasing for every seed, infinite growth ×1.2 per day) and the 7-day economy calibration (V4/V5 over 7 days, 14.4) | M2 |
| PlayMode, single-process solo host | Boot, solo start, spawn, leave/restart; cast→land; run loop with a 3 s day (7 Settles, Night Camp save and resume) | M1 |
| Manual MPPM, dev-direct (offline) | Host plus up to 3 clones on 127.0.0.1 | M1 |
| Manual MPPM, Sessions/Relay | Real join code (needs the [user] cloud link) | M1 |
| Manual latency | Network Simulator: 150 ms RTT, 2% loss, on client instances | M1 |
| Playtest | The user plus friends, with telemetry CSVs | M2 |

**Known limitation:** NGO 2.13.3 does not ship `NetcodeIntegrationTest` as a usable dependency. Host-authority rules are pure Core functions (validators). Networking is covered by the solo PlayMode smoke tests plus MPPM. No milestone blocks on a multi-NetworkManager harness.

**Architecture test (EditMode):** scan `.cs` files under Gameplay, Fishing, Presentation and UI for `Unity.Services` or `Unity.Netcode.Transports`, and assert those asmdefs don't list `Unity.Services.*` or `Unity.Networking.Transport`. (An asmdef check alone is not enough: `UnityTransport` lives inside `Unity.Netcode.Runtime`.)

### 4.10 Git

- `.gitignore`: keep the existing GitHub Unity file; append `.vscode/`.
- `.gitattributes`: keep the existing Unity template (LFS on png, dll, gz, audio, fonts; unityyamlmerge on YAML and `.meta`).
- **LFS [user approval]:** run `git lfs install --local` once before the first push. The repo has no `pre-push` hook, so LFS objects would otherwise arrive as dangling pointers. M1 itself commits no LFS-tracked files.
- **UnityYAMLMerge (optional, [user approval]):** repo-local `merge.unityyamlmerge` driver pointing at the editor's `UnityYAMLMerge.exe`.
- **First commit (when asked):** after 4.2 cleanup, `git status --porcelain` must list no `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx`, `.vscode/`. Commit `Assets/`, `Packages/` (with lock), `ProjectSettings/`, `Tools/`, `docs/`, `CLAUDE.md`, `.gitignore`, `.gitattributes`.

---

## 5. Milestone roadmap

| Milestone | Goal | Main deliverables | Exit gate (full DoD in section 17) |
|---|---|---|---|
| **M1 Foundation** | Friends can be in the same world | Cleanup, packages, asmdefs, builders, CLI; Bootstrap/Menu/Spot_LakeDock; `INetworkSession` (Solo, dev-direct, UGS Sessions); connection approval and roster; first-person player with owner-authoritative movement, swimming and water exits; `DockBell` interactable; settings; `AudioBus` | Offline solo and 4-player dev-direct MPPM work; online join by code works once the cloud project is linked; `check` passes |
| **M2 Fishing core** | Fishing is fun with no quota | Rod state machine, charge cast, cosmetic bobber/line, bite planner, `FightSim`, host ledger and validators, 4 rods, 6 baits, 5 placeholder species on 5 fight profiles, creel, catch reveal, toon/water/line shaders, ripples, fishing audio, haptics, assist and line gauge | Bots hit tuning targets; playtest fun gate passes; client fights feel immediate at 150 ms RTT |
| **M3 Fish ecology** | Where and when matters | `FishingZoneDef`/`SpawnRule` data, `FishingResolver`, shore-distance grid, weather director, hotspot, 23 species + 2 specials across `lake_dock` and `harbour_pier`, journal with 4 hint levels, `meta.json` v1, day-night and seasonal palette on a dev clock | Coverage validators V1–V3 pass; balance reports within bounds; journal persists |
| **M4 Run loop** | A complete 7-day expedition | `RunStateMachine`, `RunDirector`, clock anchor, Hub scene with map board (choose from unlocked maps; map N unlocks N+1), quota (cash/weight, 7-entry curve), sell bin, Night Camp after every day (shop with in-run rods, forecast radio, story beat, ready-up), nightly run snapshot with save/resume from the Hub, market board, 8 data-only daily modifiers with per-map escalation tiers and a day-7 climax, beat schedule, freshness, map skills with the skill pick UI, Keep Fishing (Infinite) mode, meta save v2 (cosmetic currency, unlocked maps, `skillByMap`, best infinite day, pending-run ref), cosmetic payouts and equipping starter cosmetics. The cosmetics store UI ships in M5 (19 #33) | Full 7-day solo run offline in PlayMode, with a save/resume and infinite entry; 2–4 player MPPM run including a quit and resume; `runlog.csv` written |
| **M5 Friend slop** | Chaos on purpose | Physical fish carry/throw/sell, thieves, co-op monster fish, hazards and timed happenings, daily requests, shove/stagger/downed/ragdoll, slap and splash revive, seagull carry, cosmetics store and emotes | 4-player 7-day run hits every listed chaos beat along the escalation arc, ending in the day-7 boss climax; item conservation holds; solo run completes including a monster |
| **M6 Expansion** | More places, more ways to fail together | Vivox proximity voice, boat (spike first), leaks and bilge, bucket bailing, repair minigame, the three remaining maps (5 in total, each unlocked by beating the previous one), Steam transport evaluation | 4-player and solo crews survive a storm leak sequence offshore and return |

---

## 6. Networking architecture

### 6.1 Decisions

| # | Decision |
|---|---|
| N1 | NGO 2.13.3, client-server with a host. No Distributed Authority. |
| N2 | Multiplayer Services 2.3.3 Sessions for Lobby, Relay and the join code. |
| N3 | Transport 6.6.0 via the NGO `UnityTransport` component, configured in code before each start. |
| N4 | On the online path the Sessions SDK starts and stops NGO; our code never calls `StartHost`/`StartClient` there [verify M1, V1 in section 18]. Solo and dev-direct never touch UGS. |
| N5 | Menus talk only to `INetworkSession`. Gameplay, Fishing, Presentation and UI never import `Unity.Services.*`, `Unity.Netcode.Transports.*` or Steam types. A Steam backend is a new implementation, not a rewrite. |
| N6 | **The host owns every outcome that matters:** loot rolls, money, quota, inventory, spawns, world AI, run phase, clock. For a normal fish fight **the owner simulates** with zero input latency and **the host validates** the reported outcome; species and weight always come from the host roll. The M5 co-op monster fish is host-simulated. |
| N7 | Host leaves → the live session and run end for everyone. No host migration (the SDK's default migration handler targets Entities, not NGO state). From M4 the run is recoverable: the host's last Night Camp snapshot (`run_snapshot.json`, R56) lives **only on the host's machine**, so only that host can resume it. They host a new session from the Hub, and the crew reclaims slots by persistent player id. Progress since that Night Camp is lost. |
| N8 | Up to 4 players including the host. Solo is 1 player on the same gameplay code. |
| N9 | Joins open only in Hub, Briefing and Night Camp (R13, R56). A dropped player rejoins in those phases and reclaims their slot, including after a resume on another evening. |
| N10 | Connection state and run phase are separate state machines. |

### 6.2 Topology

```
Bootstrap scene (build index 0, loaded once)
  App (root GameObject)
    AppBootstrap         composition root; builds app-scope ServiceRegistry
    NetworkManager       EnableSceneManagement = true; ConnectionApproval = true; TickRate 30; PlayerPrefab = null
    UnityTransport       configured in code by each session implementation
    network prefab list  generated by NetworkPrefabsBuilder
Menu (plain SceneManager load) -> Spot_LakeDock (M1-M3, host NetworkSceneManager load)
From M4: Menu -> Hub (map board: choose an unlocked map) -> Spot_<mapId> (Briefing, Days 1-7 with a Night Camp
         at the map's campfire after days 1-6, then SkillPick, Infinite vote and Infinite days, each with its Night Camp) -> Hub
Resume:  Menu -> Hub (host: "Resume saved run" loads run_snapshot.json, roster restored with disconnected slots)
         -> Spot_<mapId> (opens at the Night Camp of the saved night) -> next day
Save & quit at a Night Camp: Spot_<mapId> -> Hub (snapshot kept, no Result, no payout)
```

- NGO calls `DontDestroyOnLoad` itself for a NetworkManager on a root object (verified in `NetworkManager.OnEnable`), so `App` must have no parent.
- `Application.runInBackground = true` at runtime and in PlayerSettings (builder). Otherwise MPPM clones and alt-tabbed hosts stop ticking and hit the disconnect timeout.

**Types and placement**

| Type | Assembly | Role |
|---|---|---|
| `INetworkSession` | Runtime | Surface the menus use (6.3) |
| `LocalNetworkSession` | Networking | Solo (`allowRemote = false`) and dev-direct (`allowRemote = true`, only when runtime `Debug.isDebugBuild` is true). Plain UnityTransport, no UGS. |
| `UgsNetworkSession` | Networking | Multiplayer Services Sessions with Relay; owns the join code |
| `SteamNetworkSession` | — | Reserved name; not built before M6 |
| `ConnectionStateMachine` | Core | 6.4 |
| `JoinRules` | Core | Pure `Evaluate(in JoinRequest, in JoinContext) → JoinVerdict` (6.6) |
| `PlayerKey` / `NetPlayerId` | Core / Gameplay | `{ IdentityKind kind (Ugs, Steam, Local); string id }`. `NetPlayerId` is the unmanaged wire version (`kind` + `FixedString64Bytes`). Save and roster key. This persistent id is also the **reclaim key when a run snapshot is resumed** on another evening. The snapshot stores each slot's `NetPlayerId`, and the id itself lives in `settings.json` (local) or the UGS/Steam account. **Local ids** are a GUID created once and persisted in this instance's `settings.json`. It uses the per-clone suffix (`settings_p2.json`, 11.6), so MPPM clones never share one (rule 7 would reject a duplicate). A killed process gets the same id back and reclaims its slot (M1 criterion 8). Several standalone builds on one machine pass `-dev-profile <name>`, which suffixes the settings and save files the same way. |
| `ConnectionApprover` | Networking | NGO approval callback: deserializes the payload, gets a `JoinContext` from `IJoinContextProvider`, calls `JoinRules` |
| `IJoinContextProvider` | Runtime | Implemented by `SessionState` (M1) and fed by `RunDirector` (M4): player count, joins open, identity present |
| `SessionState` | Gameplay | Host-spawned NetworkObject with `destroyWithScene: false` [verify it survives `LoadSceneMode.Single`; fallback: host re-spawns it per scene from a host-local copy]. Holds `NetworkList<PlayerSlot> Roster` and `NetworkVariable<bool> NetJoinsOpen`. **Restore path (M4):** `RestoreRoster(snapshot.slots)` fills the roster from a run snapshot with every slot except the host's marked `connected = false`, so joiners reclaim them by id (6.6 rule 8). The same host-local copy serves as the per-scene re-spawn fallback. |
| `PlayerSlot` | Gameplay | Unmanaged `INetworkSerializable, IEquatable<PlayerSlot>`: `{ ulong clientId; NetPlayerId id; FixedString32Bytes name; byte slot; bool connected; bool ready; }` (`NetworkList<T>` requires `unmanaged, IEquatable<T>`) |
| `PlayerSpawner` | Gameplay | Host-only, in each gameplay scene (6.5) |

### 6.3 `INetworkSession`

```csharp
public interface INetworkSession : IDisposable
{
    ConnState State { get; }
    event Action<ConnState, ConnState> StateChanged;
    event Action<EndReason> Ended;                                     // once per session, host and clients
    string JoinCode { get; }                                           // null for solo/direct
    Task<string> HostOnlineAsync(CancellationToken ct);               // returns session.Code
    Task JoinOnlineAsync(string code, CancellationToken ct);
    Task StartLocalAsync(bool allowRemote, CancellationToken ct);     // solo or dev-direct host
    Task JoinDirectAsync(string ip, ushort port, CancellationToken ct); // dev only
    Task SetJoinableAsync(bool open);                                 // mirrors N9 into the Lobby lock [verify API]
    Task LeaveAsync();                                                // idempotent
}
public enum EndReason { UserLeft, HostLeft, Kicked, Timeout, FullLobby, JoinsClosed, VersionMismatch,
                        InvalidCode, ServicesUnavailable, TransportFailure, Unknown }
```

**Rules for every implementation**
- Register the approval callback and set `NetworkConfig.ConnectionData` before any start. The SDK starts the client inside `JoinSessionByCodeAsync`, so the payload must already be in place.
- Before starting a new mode, wait for `NetworkManager.ShutdownInProgress == false`, then configure the transport. `SetConnectionData(...)` resets the protocol to plain UnityTransport and `SetRelayServerData` sets Relay (NGO source), so relay data can't leak into solo as long as every local start calls `SetConnectionData` (use the `forceOverrideCommandLineArgs: true` overload so `-port`/`-ip` args can't hijack it).
- UGS is lazy: `UnityServices.InitializeAsync` and anonymous sign-in run on the first online action, once per process. Offline start stays fast and a UGS failure never blocks Solo.
- Every async call has a 15 s timeout [tune] and a Cancel button. The SDK call can't be aborted mid-flight: on cancel or timeout, when it completes, call `LeaveAsync()` and shut NGO down.
- On `Application.quitting`, best-effort `LeaveAsync()`.

**`UgsNetworkSession`**
- Host: `CreateSessionAsync(new SessionOptions { MaxPlayers = 4, IsPrivate = true }.WithRelayNetwork())`; expose `session.Code` (the Lobby code).
- Join: trim and (if V3 confirms case-insensitivity) upper-case the code, then `JoinSessionByCodeAsync(code)`. Never assume a code length.
- Map `SessionException.Error` to `EndReason` (full → `FullLobby`, not found → `InvalidCode`, else `ServicesUnavailable`) and log the raw error [verify `SessionError` values].
- On an NGO client disconnect, the host removes the player from the session [verify `IHostSession.RemovePlayerAsync`]; otherwise the Lobby seat stays taken and rejoin fails as "full".
- On `Ended(HostLeft)`, the client calls `session.LeaveAsync()` and ignores errors.
- MPPM: Authentication 3.8.0 uses the instance `-name` argument as the auth profile, so clones get distinct anonymous player IDs [verify M1]. Several standalone builds on one machine call `new InitializationOptions().SetProfile("p2")` before `InitializeAsync` (profile names `[a-zA-Z0-9_-]`, ≤ 30 chars).

**`LocalNetworkSession`**
- Solo: `utp.SetConnectionData(true, "127.0.0.1", port, "127.0.0.1")` then `StartHost()`. Loopback listen address, so no Windows Firewall prompt. Port 7777; if `StartHost()` fails, shut down and retry 7778–7786. PlayMode tests use 7790.
- Solo `JoinRules` rejects every non-host client.
- Dev-direct: host listens on `0.0.0.0` (or 127.0.0.1 for MPPM) and clients join `127.0.0.1:7777`. Lets MPPM run before the cloud link exists and exercises the transport-swap seam.

### 6.4 Connection state machine (Core)

States: `Offline`, `Connecting`, `Connected`, `ShuttingDown`, `Failed(EndReason)`.

| From | Event | To | Side effects |
|---|---|---|---|
| Offline | Host / Join / Local / Direct called | Connecting | Spinner with Cancel; other buttons disabled |
| Connecting | Host: `OnServerStarted`. Client: `OnConnectionEvent` ClientConnected for the local id | Connected | Host loads the first gameplay scene with `NetworkManager.SceneManager.LoadScene(name, Single)` and checks for `SceneEventProgressStatus.Started`; clients follow |
| Connecting | Timeout, SDK error, approval rejected, Cancel | Failed(reason) | Read `NetworkManager.DisconnectReason` and map it; then ShuttingDown |
| Connected | Local NGO stops unexpectedly | Failed(HostLeft / Timeout / Kicked) | Then ShuttingDown |
| Connected | `LeaveAsync` | ShuttingDown | |
| ShuttingDown / Failed | Shutdown complete and session left | Offline | Plain-load Menu; show the reason if any |

`ConnectionStateMachine.Next(state, event)` throws `InvalidTransition` for any pair not in the table.

### 6.5 Startup, spawning, scene loads

- Approval returns `CreatePlayerObject = false`; `NetworkConfig.PlayerPrefab` stays empty, so no body spawns in Menu.
- `PlayerSpawner` (host only, each gameplay scene) calls `NetworkManager.SpawnManager.InstantiateAndSpawn(playerPrefabNetworkObject, clientId, destroyWithScene: true, isPlayerObject: true, position: pos, rotation: rot)` (named arguments: the 2.13.3 signature has `forceOverride` between `isPlayerObject` and `position`) at the spawn point for the player's slot (4 dock points, 1.5 m apart). Everyone spawns on `NetworkSceneManager.OnLoadEventCompleted`; late joiners spawn when their scene sync completes (server-side `OnSceneEvent` `SynchronizeComplete` [verify]).
- Each gameplay scene has a `SceneCamera` for pre-spawn frames, disabled when the local player spawns and re-enabled if it despawns.
- Close joins before `LoadScene`; reopen only in Hub, Briefing or Night Camp. Disconnect any client listed in `clientsTimedOut` with reason Timeout.
- Ready-up: `PlayerSlot.ready` gates Start Run / Resume Run (Hub), Briefing and End Night Camp. All ready → start at once. The host can force-start after a 10 s countdown [tune] in each of these, so an AFK friend never stalls the crew.
- Subscribers to NetworkVariables subscribe in `OnNetworkSpawn`, read `.Value` once there, and unsubscribe in `OnNetworkDespawn` (late subscribers don't get earlier changes replayed).
- **Dev flags:** `-dev-solo`, `-dev-host-direct`, `-dev-join-direct 127.0.0.1:7777`, `-dev-protocol-offset N`, `-dev-long-timeout` (raises `DisconnectTimeoutMS` to 120 s so a breakpoint on the host doesn't drop clients). On MPPM clones the same flags come from `CurrentPlayer.ReadOnlyTags()`.

### 6.6 Connection approval

**Client payload** (`NetworkConfig.ConnectionData`, written with `FastBufferWriter`, < 256 bytes):

```csharp
struct ConnectPayload : INetworkSerializable {
  int ProtocolVersion;        // BuildInfo.ProtocolVersion
  ulong ContentHash;          // FNV-1a 64, computed at boot
  NetPlayerId Id;             // UGS PlayerId / Steam id / local GUID
  FixedString32Bytes Name;    // truncated display name
}
```

**`JoinRules.Evaluate`, in order:**
1. The client is the server's own client → approve (the host's own client goes through the callback during `StartHost`).
2. Solo and the client is remote → reject `JoinsClosed`.
3. `ProtocolVersion` differs → reject `VersionMismatch`.
4. `ContentHash` differs → reject `VersionMismatch` ("different game version").
5. `joinsOpen == false` → reject `JoinsClosed` ("Run in progress. Join when the crew is in the Hub, at the Briefing or at Night Camp.").
6. Connected count ≥ 4 → reject `FullLobby` (backstop; the Lobby's MaxPlayers normally refuses first; it is the only cap on the dev-direct path).
7. Identity already connected → reject.
8. Approve. If the identity matches a disconnected slot, reclaim it (same slot, colour and name, and in a run the same creel and ledger rows). This includes slots restored from a run snapshot (6.2 `RestoreRoster`): their creel, pouch, rod, gear and ledger rows come from the snapshot, and the persistent id is always matched before any new slot is taken. Otherwise take the lowest free slot (a new player in a run gets the starter kit). **Fallback** if an identity changed between evenings (V20): in the Hub the host can assign a disconnected snapshot slot to a connected player who holds no slot from that snapshot.

Rejections set `response.Approved = false` and `response.Reason = EndReason.ToString()`; the client reads `NetworkManager.DisconnectReason`. **An empty `DisconnectReason` while Connecting** means NGO dropped the client before approval: a config-hash mismatch (a different build or networked-prefab list) or a transport failure. Map it by timing: a drop well inside `ConnectTimeoutMS × MaxConnectAttempts` (≈ 10 s) means `VersionMismatch`; using up the attempts means `Timeout`/`TransportFailure` [verify V15]. The UI shows `err.version` with "or the host's build differs" wording. When joins close the host also calls `SetJoinableAsync(false)` so the Lobby refuses before Relay is involved (nice-to-have; approval is the guarantee).

### 6.7 RPC and replication conventions

- Every player-intent RPC sits on a NetworkBehaviour of the player's own `PlayerFP` with `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]`.
- Intents aimed at server-owned objects (`RunDirector`, `MonsterFight`, the boat helm) use `InvokePermission = RpcInvokePermission.Everyone` and validate `rpcParams.Receive.SenderClientId` and the phase on the server. Owner permission would reject every client there.
- Host replies to one player use `SendTo.Owner` on the player's own object, or `SendTo.SpecifiedInParams` with `RpcTarget.Single(id, RpcTargetUse.Temp)`.
- Broadcasts use `SendTo.ClientsAndHost`, **never `NotServer`** (it skips the host's own player).
- Host → owner replies that can run locally (host fishing, solo) use `DeferLocal = true`, so solo gets the same one-tick ordering as a remote client and nothing re-enters the caller's stack.
- `RequireOwnership` is `[Obsolete]` in 2.13.3. `[ServerRpc]` and `[ClientRpc]` are legacy (not marked obsolete, and `[ClientRpc]` targets `NotServer`); don't use them.
- RPC targets are `NetworkObjectReference`/`NetworkBehaviourReference`, never raw `ulong` ids. Species, events, items and tags travel as `ushort` registry indices.
- Server range checks use the host's interpolated view of owner-authoritative players, with slack (typically +0.75 to +1.0 m).
- Throttled NetworkVariables use `SetUpdateTraits(new NetworkVariableUpdateTraits { MinSecondsBetweenUpdates = ... })` (names confirmed in 2.13.3 source).
- Cosmetics (line, bobber, ripples, shake, particles, audio, ragdoll bones) are never networked; each peer derives them from replicated state or one-shot RPCs.

### 6.8 Authority map

Legend: **H** host writes and validates · **O** owner writes, others display · **S** host-only AI · **L** local only.

| System | Auth | Mechanism | Milestone |
|---|---|---|---|
| Connection, roster, slots | H | `SessionState.Roster` (slots persist with `connected = false` for reclaim) | M1 |
| Joins open | H | `SessionState.NetJoinsOpen`, mirrored to the Lobby lock | M1 |
| Player movement | O | `NetworkTransform` AuthorityMode Owner. **The host never moves players**; knockback, carry and boat riding go through the owner | M1 |
| Camera pitch | O | `NetPitchQ` (short, 15 Hz cap) | M1 |
| Equipped tool | O | `NetTool` (byte) | M2 |
| Display name, slot | H | `NetDisplayName`, `NetSlot` | M1 |
| Rod state (continuous) | O | `NetRodState` (15 Hz) | M2 |
| Cast accept, bait, roll | H | `CastRequestRpc` → `CastResultRpc` | M2 |
| Normal fish fight | O | Owner runs `FightSim`; host validates `ResultRpc` | M2 |
| Catch commit and reveal | H | `CommitRpc` to owner, `CatchAnnouncedRpc` to all | M2 |
| Creel contents | H | Host-write `NetworkList<CreelEntry>` on `PlayerNetState` | M2 |
| Season, weather, hotspot, clock anchor | H | `WorldState` NetworkVariables. Season and the 7-day weather track are rolled per run (R50) and stored in the run snapshot | M3 |
| Run phase, day, plan, quota, wallet, tallies | H | `RunDirector` NetworkVariables: phase (incl. NightCamp, SkillPick, InfiniteVote), day 1–7 plus infinite day counter, 7-entry quota curve plus the infinite quota, the per-day escalation tiers in the published plan (the live tier is `DayDirector.NetEscalationTier`); settle and sale announcements via `ClientsAndHost` RPCs. 7 Settles per trip | M4 |
| Run snapshot (save/resume) | H, host-local file | `RunSnapshotService` writes `run_snapshot.json` at every Night Camp (14.9); never sent over the network | M4 |
| Votes, ready flags | H tallies | Intent RPCs: "Call it a day", Briefing and Night Camp ready-up, the unanimous Keep Fishing (Infinite) vote after the day-7 Settle | M4 |
| Shop stock and purchases | H | `NetworkList<ShopSlot>`; `BuyRequestRpc` → `BuyResultRpc`; open at every Night Camp (6 per trip), including the in-run rod tiers. Cosmetics are never run stock | M4 |
| Map choice (Hub) | H | `MapBoard` interactable; `NetSelectedMap`, from the union of connected players' unlocked maps | M4 |
| Skill and cosmetic loadout | L per player, H applies | Each peer's save is the source; `LoadoutRpc` on every join and rejoin (14.8); the host applies skills in rolls and validation | M4 |
| Selling | H | Sell bin interaction (M4) / server trigger (M5) | M4 |
| Meta save | L per player | Host sends `ShellsPaidRpc` at each met Settle and `MapClearedRpc` at the day-7 win; each peer writes its own file (cosmetic currency, `skillByMap`, unlocked maps, best infinite day) | M4 |
| Daily modifier, happenings | H | `DayDirector` NetworkVariables + beat RPCs; visuals local. Rolls are biased by the map's escalation tier for the day (R54); infinite days stay at the top tier with stacked modifiers | M4/M5 |
| Physical fish | H | Server-owned NetworkObject, `NetworkTransform` (Server), `NetworkRigidbody`, `AttachableBehaviour` | M5 |
| Thieves | S | Server `NetworkTransform`, `NetThiefState` | M5 |
| Body state (stagger/downed/carried) | H | Server-run `PlayerStateMachine` → `NetBodyState` + `ShovedRpc` to all | M5 |
| Swimming | O | `NetSwimming` (owner flag) | M1 (flag), M5 (consumers) |
| Monster fish fight | H | Host `MonsterFightSim` fed by unreliable `ReelInputRpc` | M5 |
| Boat | H sim, O input | Server `NetworkRigidbody`; `HelmInputRpc` from the lock holder | M6 |
| Leaks, bilge, buckets | H | `BoatHull` NetworkVariables/List | M6 |
| Voice | — | Vivox, never through NGO | M6 |
| Lighting, shake, particles, audio | L | Derived from replicated state | all |

### 6.9 Disconnect and failure handling

| Event | Host | Client |
|---|---|---|
| Client leaves (Hub, Briefing, Night Camp) | Mark the slot disconnected, despawn the player, remove from session | — |
| Client drops mid-day | As above, plus: clear its active cast (no commit), detach its held fish at its last hand position (M5), remove its votes and ready flag, recompute today's quota (R14). Its creel stays in the slot ledger; its sales stay in the tally | "Connection lost" → Menu. Can rejoin with the code at the next Night Camp, or on another evening after a resume |
| Client timeout | Same as a drop | Same |
| Host leaves, times out or quits | Graceful Save & quit at a Night Camp: the snapshot is current, `RunPausedRpc` tells everyone, then shutdown (or the host returns to Hub). Any other host loss: the session simply ends; the last Night Camp snapshot stays on the host's disk | `Ended(HostLeft)` → Menu with "Run saved at Night N; the host can resume it". Shells already paid are kept; progress since the last Night Camp is lost (14.9) |
| Transport or Relay error | Treated as the host leaving | Treated as the host leaving |
| UGS not linked, not enabled, offline | Host fails `ServicesUnavailable`; the message offers "Play Solo instead" | Join fails the same way |

**Transport timeouts [tune; names confirmed in NGO source]:** `ConnectTimeoutMS = 1000`, `MaxConnectAttempts = 10` (gives up after ~10 s), `DisconnectTimeoutMS = 10000`, `HeartbeatTimeoutMS = 500`.

**No mid-day reconnect in v1.** Rejoin at Night Camp covers the common case. **Mid-run save/resume is in scope from M4 (R56):** the host auto-saves at every Night Camp, and the crew resumes from the Hub, reclaiming slots by persistent player id. Only the original host's machine holds the snapshot. Each client's `meta.json` keeps an informational `pendingRun` reference.

### 6.10 Transport abstraction

```
Menu/UI -> INetworkSession -> UgsNetworkSession   -> NGO + UnityTransport (Relay, via the SDK)
                           -> LocalNetworkSession -> NGO + UnityTransport (loopback / dev direct)
                           -> SteamNetworkSession (M6+) -> NGO + community Facepunch transport or custom NetworkTransport
```

Steam replaces the whole session layer: the join code becomes a Steam lobby/invite behind the same interface. `NetPlayerId.kind` keeps save and roster keys stable across backends. `WithNetworkHandler(INetworkHandler)` is the documented hook for Sessions with a non-UTP transport; it is not planned.

---

## 7. Player controller and interaction

### 7.1 Principles

1. **The owner drives its own body.** Movement, look and local ragdoll are owner-authoritative, so input feels instant.
2. **The host decides anything that changes shared state:** pickups, drops, throws, sells, shoves, body state. The client predicts only cosmetics (animation, sound, prompt).
3. **Feel is cosmetic and local.** Walk cycle, head bob, springs, shake and ragdoll poses run on each client from replicated root motion and one-shot RPCs, never per bone.
4. **Pure logic in Core.** Core types use `System.Numerics` vectors and `System.Math`; adapters convert at the boundary.
5. **Input is never ambiguous.** One router decides what each key means in the current context (section 8). Gameplay code never reads `InputAction`s.

### 7.2 Player prefab `PlayerFP`

Network prefab, registered in the list, built by `PrefabBuilder`.

| Component | Type | Runs on | Role |
|---|---|---|---|
| `NetworkObject` | NGO | all | Spawned by `PlayerSpawner` |
| `NetworkTransform` | NGO | all | AuthorityMode Owner; sync position XYZ and rotation Y only; no scale; Interpolate on, Lerp for position and rotation. Set once on the prefab; `SwitchTransformSpaceWhenParented = true` (needed for seagull carry and boats; not combined with unreliable deltas or rigidbody motion) |
| `CharacterController` | Unity | owner only (enabled in `OnNetworkSpawn`) | Height 1.8, radius 0.35, centre y 0.9, step 0.35, slope 45°, skin 0.08 |
| `CapsuleCollider` | Unity | non-owners only | Same capsule on layer `RemotePlayer` so local players bump remote bodies |
| `PlayerNetState` | NetworkBehaviour | all | All replicated player state (7.5) |
| `PlayerActions` | NetworkBehaviour | all | Every non-fishing player RPC (7.6); MonoBehaviours call through it |
| `FishingRodNetwork` | NetworkBehaviour | all | Fishing RPCs and rod state (section 9) |
| `PlayerLocomotion` | MonoBehaviour | owner | Consumes `PlayerIntent`, moves the CharacterController |
| `PlayerCamera` | MonoBehaviour | owner | Mouse look, FOV, trauma shake, head bob; Camera and AudioListener enabled only on the owner |
| `PlayerBody` | MonoBehaviour | all | Primitive rig, slot material, walk cycle, owner shadow-only mode, nameplate on remotes; springs in M5 |
| `PlayerInteractor` | MonoBehaviour | owner | Sphere-cast prompt; sends `PlayerActions.InteractRpc` |
| `HoldController` | MonoBehaviour | all | `HoldNode` (`AttachableNode`), carry speed, throw charge (M5) |
| `RagdollController` | MonoBehaviour | all | Cosmetic ragdoll (M5) |

### 7.3 Movement and camera (M1)

**Locomotion (`PlayerLocomotion.Update`, owner)**
1. Read `PlayerIntent`. The router has already zeroed Move/Jump/Sprint in contexts that forbid them.
2. Wish speed: walk 4.2 m/s, sprint 6.6 m/s. `RodAiming` ×0.6, no sprint. `Carrying` (M5) × `CarrySpeedMult(kg) = clamp(1 − kg/40, 0.55, 1)`, no sprint above 15 kg. No sprint in the air.
3. Accelerate horizontally 14 m/s² ground, 4 m/s² air. Add `knockbackVel`, decaying 10 m/s² ground, 2 m/s² air.
4. Gravity −22 m/s², terminal −30 m/s; grounded with vy < 0 → vy = −2.
5. Jump: `vy = sqrt(2 × 22 × 1.1) ≈ 6.96 m/s` (1.1 m peak); coyote time 0.1 s; jump buffer 0.1 s.
6. `CharacterController.Move(velocity × dt)`.
7. Yaw: `transform.Rotate(0, LookDelta.x, 0)`; NetworkTransform replicates it.
8. While `Paused` (solo pause), locomotion does nothing.

**Look scaling (adapter only):** mouse `delta × sens` with no dt (default 0.12°/count, range 0.04–0.30); stick `value² (sign kept) × 180°/s × dt`; invert Y setting; drop the first look delta after the app regains focus (MPPM focus switching would otherwise jump the camera). Cursor locked and hidden in gameplay contexts, unlocked in `Menu`; re-applied on `OnApplicationFocus(true)`.

**Camera:** head pivot at local y 1.62; camera is a child of `HeadPivot/Shake`; near clip 0.05, far 400. Pitch clamp ±85°. FOV: horizontal 90° default (80–110), converted with `Camera.HorizontalToVerticalFieldOfView(h, aspect)` and re-applied on aspect change; sprint adds +4° vertical-equivalent over 0.25 s. **Trauma shake:** `trauma` 0..1 decays 1.2/s; offset `2.5° × trauma² × Perlin(t × 20, seed)` applied in `LateUpdate` to `Shake`; scaled by Camera motion. **Head bob:** vertical sine at `horizontalSpeed / 1.4 m`, amplitude 0.03 m, scaled by Camera motion.

**Replicated pitch:** `NetPitchQ` (`NetworkVariable<short>`, read Everyone, write Owner) = pitch × 100, clamped ±85 (`PitchQuant` in Core). The owner writes only when |new − last| ≥ 50 (0.5°), capped at 15 Hz. Remotes smooth with `pitch = lerp(pitch, NetPitchQ/100, 1 − exp(−15·dt))` and apply it to head and right-arm pose.

**Owner body visibility:** owner's head, torso, legs and left arm render `ShadowCastingMode.ShadowsOnly`; the right arm stays visible, posed in camera space (lower right, follows pitch), so held rods and fish read in first person. No separate view-model camera in M1–M5. The line starts at the rod tip on whichever arm pose is local.

**Teleport:** server-initiated teleports use `PlayerActions.TeleportRpc(Vector3 pos, float yaw)` (`SendTo.Owner, InvokePermission = Server`). Owner: `cc.enabled = false` → `networkTransform.Teleport(pos, Quaternion.Euler(0, yaw, 0), Vector3.one)` → zero velocity and knockback → `cc.enabled = true`. Only the owner may call `Teleport`. **Out of bounds:** if y < −20 the owner teleports itself to its spawn point.

**Water: swimming and exits**
- `WaterSurface.HeightAt(x, z)` (Runtime adapter over Core `WaveMath`, section 10.4) defines the surface. A player is swimming when the root is below `HeightAt − 1.2 m` inside a `WaterVolume`.
- Swimming: 2.2 m/s, no sprint, gravity −4 m/s² plus buoyancy holding the eyes ~0.15 m above the surface. The owner writes `NetSwimming`.
- Entering water stows the rod (remembered), ends any active fight as `Escaped(Dropped)`, and (M5) drops any carried item.
- `WaterExit` trigger volumes sit at every ladder and the shore ramp (the lake-dock map has ≥ 2 ladders and 1 ramp). Inside one, Jump or swimming forward plays a 0.4 s lerp to the exit's `topPoint`. After 60 s in water the owner teleports to the dock spawn.
- No fall damage.

### 7.4 Body, colours, nameplates

- Rig built by `PrefabBuilder` from primitives (M1: `GameObject.CreatePrimitive` meshes with URP Lit slot materials and all generated colliders removed except, from M5, the ragdoll part colliders; M2: `MeshBuilder` flat-shaded meshes on the Toon shader): head sphere r 0.14 at y 1.62 (child of `HeadPivot`); torso box 0.42×0.55×0.24 at y 1.2; arms capsule r 0.06 length 0.5 (shoulders y 1.42, x ±0.27); legs capsule r 0.08 length 0.8 (hips y 0.8, x ±0.11); a nose cube showing facing; `HoldNode` (empty + `AttachableNode`) at the right hand.
- Player colour: **4 prebuilt body materials**, one per slot (`M_Body_Slot0..3`). No runtime material instances and no `MaterialPropertyBlock` (it opts out of the SRP Batcher). Head and hands use the skin tone. The host assigns the lowest free slot at join; the renderer applies it on `NetSlot` change.
- **Procedural walk cycle (all clients):** legs ±30°, arms counter-swing ±20° at `speed / 1.4 m` stride frequency, with `speed` from the root's frame-to-frame delta, so remotes animate from interpolated motion.
- **Nameplate:** remotes only, screen-projected UI Toolkit label above the head, alpha fading from 15 m to 25 m; shows a speaker icon in M6.
- **M5 floppy springs** (`DampedSpring.Step`, semi-implicit Euler substepped at ≤ 1/60 s inside `LateUpdate`): head k 60 / c 9, arms k 35 / c 6, driven by root local velocity and acceleration; landing squash −0.3 scale-velocity on grounded false→true.

### 7.5 `PlayerNetState` fields

| Field | Type | Write | Purpose | From |
|---|---|---|---|---|
| `NetPitchQ` | `short` | Owner | Remote head/arm pitch | M1 |
| `NetSlot` | `byte` 0–3 | Server | Colour, spawn point, roster key | M1 |
| `NetDisplayName` | `FixedString32Bytes` | Server | Nameplate, HUD | M1 |
| `NetSwimming` | `bool` | Owner | Swim state for others and for host rules | M1 |
| `NetTool` | `byte` (`ToolKind`: None 0, Rod 1, Bucket 2) | Owner | Instant equip; cosmetic for others | M2 |
| `NetCreel` | `NetworkList<CreelEntry>` | Server | Creel contents (section 9.10) | M2 |
| `NetCarried` | `NetworkObjectReference` | Server | Carried item; the server decides because two players can grab the same fish | M5 |
| `NetBodyState` | `byte` (`BodyState`: Upright, Staggered, Downed, Carried) | Server | Copy of the server state machine for consistency and late joiners | M5 |

- **Effective hands:** if `NetCarried` is valid the player is `Carrying`; otherwise hands are `NetTool`. When the server sets `NetCarried`, the owner stows the rod (writes `Tool = None`, remembers it); when it clears, the owner restores it. Equip is ignored while Carrying or RodFighting.
- **Display name:** in `OnNetworkSpawn` the owner sends `SubmitNameRpc` with the Sessions player name online or the settings name offline. The server trims to 1–16 characters, strips control characters, and falls back to "Fisher {slot + 1}".

### 7.6 Player RPCs (`PlayerActions`; all set `InvokePermission` explicitly)

| RPC | Attribute | Server behaviour | From |
|---|---|---|---|
| `SubmitNameRpc(FixedString32Bytes)` | Server, Owner | Sanitise, write `NetDisplayName` | M1 |
| `InteractRpc(NetworkBehaviourReference target)` | Server, Owner | `TryGet(out NetworkInteractable i)`; `i.CanInteract(sender)`; distance from the server's copy of the sender's head ≤ `i.Range + 1.0 m`; then `i.ServerInteract(sender)`. No line-of-sight check | M1 |
| `TeleportRpc(Vector3, float yaw)` | Owner, Server | Owner handling in 7.3 | M1 |
| `SellRequestRpc(ulong creelMask)` | Server, Owner | Only while the sender is within range of a `SellBin` and the phase allows selling (section 14.6) | M4 |
| `ThrowRpc(Vector3 aimDir, float charge, Vector3 carrierVel)` | Server, Owner | Sender has a valid `NetCarried`; clamp charge 0..1, normalise and clamp aim to ±30° of the server look direction, clamp carrierVel to 7 m/s; server computes `ThrowMath.ThrowVelocity` itself | M5 |
| `DropRpc()` | Server, Owner | As throw with charge 0 plus a 1.5 m/s forward drop | M5 |
| `ShoveRequestRpc(NetworkBehaviourReference target)` | Server, Owner | Section 15.5; direction computed on the server from positions | M5 |
| `ReviveRequestRpc(NetworkBehaviourReference target)` | Server, Owner | Section 15.5 | M5 |
| `ShovedRpc(Vector3 impulse, byte newState, float duration)` | ClientsAndHost, Server (on the target's `PlayerActions`) | Owner applies knockback and local ragdoll; others play FX and remote ragdoll | M5 |
| `CarriedRpc(NetworkObjectReference gull)` / `DroppedRpc(Vector3 pos)` | Owner, Server | Section 15.6 | M5 |

### 7.7 Interaction framework (M1)

```csharp
public abstract class NetworkInteractable : NetworkBehaviour {
    public virtual float Range => 3.0f;
    public abstract string GetPrompt(ulong clientId);        // local display only, e.g. "E  Ring bell"
    public virtual bool CanInteract(ulong clientId) => true; // server truth; also evaluated locally to grey the prompt
    public abstract void ServerInteract(ulong clientId);     // server only
}
```

- Owner sphere-cast from camera forward: range 3.0 m, radius 0.15 m, mask `Interactable`, only in contexts where Interact is live. E sends `InteractRpc` once per press; no hold-to-interact.
- Subclasses: `DockBell` (M1; rings for everyone with a `ClientsAndHost` RPC, validates the whole path), `StartBell` (M4 Hub; starts or resumes the run once the crew is ready), `MapBoard` (M4 Hub; host picks the map, everyone sees it; also shows "Resume saved run"), a Hub `SkillBoard` (M4; view your map skills and pick any unpicked `""` slot), `CosmeticsStall` (Hub, M5), `DayEndBell` (M4; casts the "Call it a day" vote during Day only and has no role in Settle or Night Camp), `SellBin` (M4), and the Night Camp set at each map's campfire (M4): `ShopCounter` (the camp shop), `CampRadio` (forecast and story beat), `CampFire` (ready-up toggle) and `SkillBoard` (day-7 skill pick). Then `FishPickup` (M5), `LeakPatch` (M6), `BoatSeat`/`Helm` (M6). In-scene interactables are in-scene-placed NetworkObjects from M4; before that they are host-spawned at `NetSpawnAnchor` markers (4.6).

### 7.8 Player acceptance (M1)

1. Remote movement in a 4-instance MPPM session at 150 ms RTT and 2% loss shows no snap > 0.5 m during steady walking.
2. Remote pitch tracks within 5°.
3. A player climbs out of the lake within 2 s of reaching any ladder or the ramp.
4. Teleport lands within 0.05 m with no stale velocity.
5. Clients see the host's own player move (re-test of an old NGO 2.2.0 forum report; failure is a blocker).
6. The owner sees no body of their own except the right arm and their shadow; Camera and AudioListener are active only on the owner.

---

## 8. Input

### 8.1 Asset

- `Assets/_Project/Runtime/Input/FishGameInput.inputactions`, plain JSON without a BOM. Hand-written `.meta`: ScriptedImporter, `generateWrapperCode: 1`, `wrapperClassName: FishGameInput`, `wrapperCodeNamespace: FishGame.Input`, a fresh random 32-hex GUID. No action map may be named `FishGameInput`. The generated `FishGameInput.cs` is committed.
- **Maps** (separately rebindable):
  - `Player`: Move, Look, Jump, Sprint, Primary, Secondary, Interact, Shove, Drop, Equip, Journal, Pause.
  - `Fishing`: Reel, Steer.
  - `Boat` (M6): Bail, ThrowWater, Throttle, BoatSteer.
  - `UI`: Navigate, Submit, Cancel, Point, Click.
- `Player` and `Fishing` are enabled during gameplay; `UI` only in `Menu` context. **Only `PlayerInputAdapter` (Runtime, behind `IInputService`) reads actions**, once per frame, and asks Core `InputContextRouter` which actions count. It outputs `PlayerIntent` (Move, LookDelta in degrees, JumpPressed, SprintHeld, PrimaryHeld/Pressed/Released, SecondaryHeld/Pressed/Released, InteractPressed, ShovePressed, DropPressed, EquipPressed) and `FightInput { float steer; bool reel; }`. Because handlers never subscribe to actions, the same key in two maps can't double-fire.
- Window unfocused → neutral input (no reel, no steer).

### 8.2 Context routing (Core `InputContextRouter`, unit-tested)

`InputContext`: `Free`, `RodReady`, `RodAiming`, `RodWaiting`, `RodFighting`, `Carrying`, `Staggered`, `Downed`, `Carried`, `Swimming`, `Climbing`, `Menu`, `Seated` (M6). The rod contexts are set from the rod state machine (9.2). Players walk around Night Camp and the Hub in `Free`. Every panel opened there (shop, radio, map board, skill pick, Infinite vote, cosmetics) switches to `Menu`, so only the UI map is live. No new context is needed.

| Context | Move/Jump/Sprint | LMB / RT | RMB / LT | E | F | G | Equip | A/D, S |
|---|---|---|---|---|---|---|---|---|
| `Free` | yes | — | — | Interact | Shove | — | take rod | move |
| `RodReady` (rod out, not aiming) | yes | — | Ready (hold) | Interact | Shove | — | stow | move |
| `RodAiming` (Ready/Charging) | yes (×0.6, no sprint) | Charge: hold, release to cast | hold to stay ready | — | — | — | stow | move |
| `RodWaiting` (line out) | yes | — | Cancel (press) | Interact | Shove | — | — | move |
| `RodFighting` (Bite/Hooked) | **no** (look allowed) | Reel | — | — | — | — | — | **Steer / Reel** |
| `Carrying` (M5) | yes (carry speed) | Throw: hold to charge, release | — | Interact | Shove | Drop | — | move |
| `Staggered` (M5) | no | Reel if fighting | — | — | — | — | — | Steer/Reel if fighting |
| `Downed` / `Carried` (M5) | no | — | — | — | — | — | — | — |
| `Swimming` | swim | — | — | climb at exits | — | — | — | move |
| `Menu` | UI map only | | | | | | | |

**Fishing feel rule:** movement is suppressed only while hooked, because A/D/S are the fight keys (Rust-style). With the rod merely out, WASD walks.

### 8.3 Default bindings

| Action | Keyboard/mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Look | Mouse delta | Right stick |
| Jump | Space | South |
| Sprint | Left Shift (hold; toggle in settings) | Left stick press |
| Interact | E | West |
| Primary (charge cast, reel, throw) | Left mouse | Right trigger |
| Secondary (ready rod, cancel cast) | Right mouse (hold; aim-toggle setting) | Left trigger |
| Shove | F | Right stick press |
| Drop | G | East |
| Equip rod / stow | 1, mouse wheel | North |
| Steer (fight) | A / D (1D composite, A = −1, D = +1) | Left stick X |
| Reel (fight) | S (and Primary) | Right trigger (via Primary) |
| Journal | J | Select/View (tap) |
| Crew panel (M4, hold) | Tab | Select/View (hold) |
| Pause | Escape | Start |
| Boat ThrowWater (M6, `Seated`/bucket) | Q (hold) | — |

Gamepad bindings exist from M1; gamepad play is verified from M2. Rumble (`IInputService.SetRumble`, which wraps `Gamepad.current.SetMotorSpeeds` inside Runtime) is driven by tension, bite, throw and shove, scaled by Camera motion, and reset to 0 on every fishing terminal state, on focus loss and on despawn.

**Rebinding:** `PerformInteractiveRebinding`, persisted with `SaveBindingOverridesAsJson`/`LoadBindingOverridesFromJson` into `settings.json`.

---

## 9. Fishing core (M2)

Facepunch publishes no fishing numbers. Every value here is a starting point; the EditMode scripted bots (9.12) are the source of truth for tuning, and failing bots mean retuning data, not new mechanics.

### 9.1 Goals, non-goals, glossary

**Goals:** relaxed waiting; a loud bite you can't miss; a fight you read from the world (rod bend, creak, splash), not a HUD; a forgiving rhythm (steer against the run, reel when the fish calms); the agreed charge cast on top of Rust's ready-then-cast and blue-marker rule; one pure, seeded, data-driven fight model; zero input latency for the owner; host-owned outcomes; every species fighting *differently*, not just harder.

**Non-goals for M2:** real spawn tables, time/season/weather (M3; M2 uses a placeholder roller); shop, bait inventory, meta (M4: the in-run Night Camp shop with rods and bait, map skills, and cosmetics-only meta currency; the cosmetics store is M5; M2 uses a debug loadout); physical fish, thieves, monster fish, shove, boats (M5/M6); fight scaling by player count (never).

| Term | Meaning |
|---|---|
| Owner | The client that owns the player object; the rod is a component on `PlayerFP` |
| Lateral | Fish position across the line, −1..+1, **+1 = right**. Angle off the cast line = `lateral × lateralMaxDeg` (75°) |
| Steer | Input −1..+1, **A = −1, D = +1**. Steering pulls the fish toward the steer side; countering a fish running right means pressing A |
| Line length | Horizontal metres from rod tip to fish |
| Tension | 0..1; at 1.0 the snap grace timer runs |
| P | Effective pull = `pullStrength × weightFactor × fishStamina`, 0..1. A tired fish fights less |
| Burst / Calm | Fight phases. A Burst is a side run (`burstDir` ±1) or a dive (`burstDir` 0) |

Units: metres and seconds. The fight runs at a fixed **50 Hz (dt 0.02 s)** from an accumulator in `Update` (not `FixedUpdate`), at most 5 steps per frame; extra time is dropped.

### 9.2 Player flow and rod state machine (Core `RodStateMachine`, owner-run)

1. **Ready:** hold RMB for 0.25 s to raise the rod; the landing marker appears.
2. **Charge:** while Ready, press and hold LMB. The meter **ping-pongs**: full at 1.0 s, back to 0 at 2.0 s, and so on. A full cast takes timing; a short cast is a choice. The marker slides out and back with it.
3. **Cast:** release LMB on a valid marker. Releasing on an invalid marker gives a dull "thunk", stays in Ready, costs nothing.
4. **Wait:** RMB can be released; the player may walk. Press RMB again to reel in early (cancel).
5. **Bite:** loud splash, bobber yanked under, rod jerk, camera micro-shake. Auto-hook by default.
6. **Fight:** movement locks; A/D steer and S/LMB reel; look stays free.
7. **Land or lose.**

**States:** `Idle`, `Readying`, `Ready`, `Charging`, `Casting`, `Waiting`, `Bite`, `Hooked`, `Landed`, `Snapped`, `Escaped`. (Rust's "reeling" is the reel flag inside `Hooked`.)

| From | Event / guard | To | Side effects |
|---|---|---|---|
| Idle | Secondary down | Readying | Raise animation |
| Readying | Secondary held 0.25 s | Ready | Marker on |
| Readying / Ready | Secondary released | Idle | Marker off |
| Ready | Primary pressed (fresh edge) | Charging | Meter at 0 |
| Charging | Secondary released | Idle | No cast, no bait |
| Charging | Primary released, `chargeNorm ≥ 0.1`, marker valid | Casting | `CastRequestRpc`; bobber flies at once (prediction) |
| Charging | Primary released, `chargeNorm < 0.1` or marker invalid | Ready | Thunk, no bait |
| Casting | flight ended and valid `CastResult` held | Waiting | Plop, ripple; bite timer starts at `max(landing, resultReceived)` |
| Casting | flight ended, no result yet | Casting | Bobber floats; timer not running |
| Casting / Waiting | `CastResult.valid == false` | Idle | Recall animation (bobber bounces back) |
| Casting | no `CastResult` within 3 s (`resultTimeout`) | Idle | Recall; `CancelRpc(castId)` so the host refunds if it accepted |
| Waiting | Secondary pressed (edge) | Idle | `CancelRpc`; bait refunded |
| Waiting | player–bobber distance > `maxLineOut` | Idle | Auto reel-in; `CancelRpc`; refunded |
| Waiting | bite timer ≥ `biteWait` | Bite | Bite cue; `RodEventRpc(Bite)` |
| Bite | `hookWindow == 0`, after `hookBeat` 0.25 s | Hooked | `HookedRpc(castId)`; `FightSim` starts from `fightSeed` |
| Bite | `hookWindow > 0`, Primary within window | Hooked | `HookedRpc(castId)` |
| Bite | `hookWindow > 0`, window elapsed | Escaped (Missed) | `ResultRpc` |
| Hooked | `lineLength ≤ landDistance` while reeling this tick | Landed | `ResultRpc(Landed)` |
| Hooked | `snapTimer ≥ snapGrace` | Snapped | `ResultRpc(Snapped)`; `RodEventRpc(Snapped, sizeClass)` |
| Hooked | `pinnedTimer ≥ pinEscapeTime` | Escaped (Pinned) | Hook pulls free |
| Hooked | `slackTimer ≥ slackEscapeTime` | Escaped (ThrewHook) | Covers AFK and "steer but never reel" |
| Hooked | displaced > `hookedLeash` (3 m) from hook position, or entered water | Escaped (Dropped) | Only via external forces (M5 shove, M6 boat) or falling in |
| Any line-out state | teleport, respawn, stow, swim, disconnect | Idle (Cancelled), or Escaped (Dropped) when Hooked | `CancelRpc`/`ResultRpc` |
| Waiting / Bite / Hooked | **unsolicited** `CommitRpc` for the current castId (host-initiated: stuck-Waiting refund, `maxFightTime`, LastCast auto-reel or grace end, the pre-snapshot sweep on entering Night Camp (14.9), Invalid) | Idle (Cancelled; bait refunded if the host says so) or Escaped(reason from `finalOutcome`) | 1.5 s feedback beat (recall for a cancel, slack line for an escape), then Idle; no `ResultRpc` sent; rumble off |
| Landed / Snapped / Escaped | 1.5 s feedback beat done **and** (`CommitRpc` received or 3 s `commitTimeout`) | Idle | Clear the line; rumble off |

Rules:
- **Bait:** consumed when the host accepts a cast; refunded on cancel or recall; lost on snap, escape or miss.
- **Edge triggers:** Charging needs a fresh Primary press made while Ready; Readying needs Secondary pressed from Idle. A button still held through a catch never starts a new cast.
- No new cast until the rod is back in Idle. Any host reply whose `castId` doesn't match the current cast is ignored. Illegal events are ignored and logged once per state.
- In solo pause, the rod tick and bite timer stop.

### 9.3 Casting

```
u          = heldSeconds / castChargeTime                       // castChargeTime 1.0 s
chargeNorm = chargePingPong ? 1 - abs((u mod 2) - 1) : clamp01(u)
distance   = lerp(castMinDist, rod.castMaxDist, chargeNorm)     // castMinDist 2.5 m
```

- **Direction:** camera flat forward (yaw only). **Pitch never changes distance**; it scales only the cosmetic arc apex ×0.7..×1.3.
- **Flight (cosmetic, deterministic):** `tFlight = 0.45 + 0.012 × distance` s; `apex = max(0.6, 0.12 × distance) × pitchApexMult`; a parabola from rod tip to landing. Remotes replay the same arc from their render of that player's rod tip when `NetRodState.state` becomes Casting, using `bobberPos` (the landing target). No positions are sent during flight. There is **no bobber NetworkObject**.
- **Validity (same function on owner and host, `CastMath` + a Fishing adapter):**
  - Landing `L = originFlat + yawForward × distance`.
  - Ray 1 down from `(L.x, waterLevel + 20, L.z)` against `Water | Ground | Shore | Structure` with `QueryTriggerInteraction.Collide` (water is a trigger). The **first** hit must be Water (a dock, rock or bank above the water makes it invalid).
  - Ray 2 down from the water hit against `Ground`, up to 50 m: `depth = waterHitY − bedHitY`.
  - Valid if ray 1 hits Water first and `depth ≥ minLandDepth` (0.8 m). From M3 the bobber cell must also be a water cell of the spot's `ShoreGrid`.
  - The owner runs it every frame in Ready/Charging to colour the marker; the host runs it once per `CastRequestRpc`. On disagreement the host wins and the owner recalls.
- **Cancel:** Secondary press in Waiting; refunds. Not possible during Casting (`tFlight` ≤ 0.81 s at 30 m). Primary taps during nibbles never cancel.

### 9.4 Waiting, nibbles, bite

```
waitBase  = Uniform(biteWaitMin, biteWaitMax)                   // 4.0 .. 18.0 s
depthMult = lerp(1.0, 0.8, clamp01(depth / 6.0))
waitMult  = bait.biteSpeedMod * fish.biteWaitMod * eventMult * depthMult * skillMult * (inHotspot ? 0.6 : 1)   // eventMult, hotspot from M3/M4; skillMult from M4
biteWait  = max(minBiteWait, waitBase * waitMult)               // minBiteWait 1.0 s
```

- `eventMult` is the product of the day's active modifiers' and happenings' `biteTimeMult`. Each is scaled by the map's escalation intensity for the day (R54, 14.5), and the product is clamped to [0.5, 1.5] [tune], so escalation can't make a quota unfair. It is data-driven (`EventDef`, `EscalationTierDef`). `skillMult` is the angler's own map skills (Patient Hands, Night Owl; 14.8), floored at 0.8.

- The species is rolled first (it sets `biteWaitMod`). Rust's anecdotal 5–45 s is shortened on purpose: a quota loop punishes dead time.
- **Nibbles:** `nibbleCount` uniform 0..`bait.maxNibbles`, forced to 0 when `biteWait < 1.4 s` (the clamp interval below would be empty; reachable with frenzy or hotspot multipliers); offsets `biteWait × (k+1)/(n+1) ± 0.3 s`, clamped to [0.5 s, biteWait − 0.6 s]. Cue: small dip, quiet plip, 4° rod wobble. A nibble never hooks and input during it does nothing.
- **Hooking:** `hookWindow = 0` (auto-hook) for every M2 fish: bite cue, 0.25 s hook-set jerk, Hooked. Twitchy fish (`hookWindow` 0.8–1.2 s, Primary inside the window) are a per-species data field, off in M2 except via a debug toggle.
- **Bite audio:** spatial AudioSource on the bobber with a large `minDistance`, plus a 2D copy for the owner only, so the owner hears it while turned to chat. Friends hear it too.
- **Junk:** no fight. Bite → hook beat → while reeling, the junk comes in at `junkDragSpeed` (3 m/s) with no tension (the 12 s slack escape still applies, so an AFK junk hook clears itself). It lands at `landDistance`. The host's Landed floor (9.9) uses `junkDragSpeed` instead of `rod.reelSpeed` for junk rolls; otherwise every junk catch would fail validation as too fast.

### 9.5 The fight (`FightSim`, FishGame.Core)

```csharp
public enum FightPhase : byte { Calm, Burst }
public enum FightOutcome : byte { None, Landed, Snapped, Escaped, Cancelled }
public enum EscapeReason : byte { None, Missed, Pinned, ThrewHook, Dropped, Timeout, Invalid, Disconnected }
public struct FightInput { public float steer; public bool reel; }
public struct FightState {
    public float lineLength, lateral, tension, fishStamina;
    public FightPhase phase; public float phaseTimer; public sbyte burstDir;   // ±1 side run, 0 dive
    public float snapTimer, pinnedTimer, slackTimer, elapsed;
    public int nearSnapRecoveries; public float maxTension;
    public FightOutcome outcome; public EscapeReason reason;
}
public sealed class FightSim {
    public const float Step = 0.02f;
    public FightSim(in FishFightProfile fish, in RodStats rod, in FishingTuning tuning, float weightT, uint seed, float startLineLength, bool assist);
    public FightOutcome Tick(in FightInput input);   // one fixed step
    public ref readonly FightState State { get; }
}
```

- Own seeded `Pcg32` from `fightSeed`; never `UnityEngine.Random`.
- **Initial state:** `lineLength` = horizontal rod-tip-to-bobber distance; lateral 0; tension 0; stamina 1; phase Calm with `phaseTimer` 0.8 s (a lead-in so the first run never lands on the hook tick).
- **Weight factor:** `weightFactor = lerp(0.85, 1.25, weightT)` multiplies `pullStrength`, so even one species varies; trophies fight hardest.

**Phase schedule**
- Calm lasts Uniform(`calmMin`, `calmMax`); Burst lasts Uniform(`burstMin`, `burstMax`).
- New burst: dive with probability `diveChance`; otherwise a side run with `burstDir = −sign(lateral)` at probability 0.75 (random when |lateral| < 0.1), else random.
- Flips: during a side run, each tick has `flipRate × dt` chance to reverse `burstDir`, at least 0.5 s apart.
- **Exhaustion:** at `fishStamina ≤ 0.05` the current burst ends and the fish stays Calm with no new bursts.

**Per tick**
```
P = fish.pullStrength * weightFactor * fishStamina

// lateral
if phase == Burst and burstDir != 0: lateral += burstDir * P * fish.lateralSpeed * dt
lateral += input.steer * rod.steerRate * dt
if phase == Calm:                    lateral -= lateral * calmCenterPull * dt            // 0.15 /s
lateral = clamp(lateral, -1, 1)

against = (phase == Burst and burstDir != 0) ? max(0, -input.steer * burstDir) : 0

// stamina: fight the side runs, let the dives run
if phase == Burst and burstDir != 0: fishStamina -= fish.staminaDrain * against * dt
if phase == Burst and burstDir == 0: fishStamina -= fish.staminaDrain * (input.reel ? 0 : 1) * dt
if phase == Calm:                    fishStamina += fishStaminaRegen * dt                // 0.04 /s
fishStamina = clamp01(fishStamina)

// line
reelRate = (phase == Calm) ? rod.reelSpeed : rod.reelSpeed * max(0.1, 1 - reelBurstPenalty * P)   // penalty 1.0
if input.reel:     lineLength -= reelRate * dt
if phase == Burst: lineLength += fish.runSpeed * P * (burstDir == 0 ? diveRunMult : 1) * dt       // diveRunMult 1.5
maxLineOut = rod.castMaxDist + spoolExtra                                                         // spoolExtra 12 m
spooled    = phase == Burst and lineLength >= maxLineOut
lineLength = clamp(lineLength, 0, maxLineOut)

// tension
strainPos = 0
if phase == Burst and input.reel: strainPos += reelStrainBurst * P * (1 - counterRelief * against)   // 0.30, relief 0.5
strainPos += steerStrain * abs(input.steer)                                                          // 0.03
if abs(lateral) > rod.angleWindow: strainPos += outOfWindowStrain                                    // 0.20
if spooled:                        strainPos += spoolStrain * P                                      // 0.50
relax = !input.reel ? relaxRate * rod.relaxMult                      // 0.45 /s
      : phase == Calm ? relaxWhileReeling * rod.relaxMult            // 0.10 /s
      : 0
tension = clamp01(tension + (strainPos * strainScale / rod.lineStrength - relax) * dt)   // strainScale 1.0 (assist 0.6)
```

**Timers and endings**
- `snapTimer += dt` while tension ≥ 1, else 0. Snap at `snapGrace` 0.25 s (assist 0.75 s). Tension may touch 1.0 inside the grace window and the fish can still land.
- `pinnedTimer` counts while |lateral| ≥ 0.95; escape at `pinEscapeTime` 5 s.
- `slackTimer` counts while not reeling, resets on any reel; escape at `slackEscapeTime` 12 s.
- Landed when `lineLength ≤ landDistance` (2.0 m) **while reeling** on that tick.
- Near-snap recovery counts when tension rises above 0.8 and later falls below 0.5 without a snap.

The two lessons fall out of the rules: **counter-steer the side runs** (drains the fish, relieves strain) and **let the dives run** (reeling into a dive gets no relief; resting tires the fish). A weak fish barely slows the reel; a fresh trophy takes line.

**Hand-worked checks (Handmade rod; bots confirm or retune):** Catfish reel-no-steer on a side run leaves the window at ~0.9 s, creaks at ~2.0 s, snaps ~2.8 s; with full counter-steer ~0.16/s, ~0.6 after a 4 s run, so trophies need the rest rhythm. Carp greedy snaps ~3.8 s; Trout greedy ~3.1 s and flips punish wrong-way steering; Bluegill effectively can't snap. Resting from 0.9 gets under 0.3 within 1.5 s.

### 9.6 Fight profiles and M2 placeholder species

Five shared `FightProfileDef` assets (R3). M2 has one placeholder species per profile; M3's roster references the same five assets.

| Profile | Seeded from | pullStrength | lateralSpeed | runSpeed m/s | staminaDrain /s | calm s | burst s | flipRate /s | diveChance | Character |
|---|---|---|---|---|---|---|---|---|---|---|
| Steady | Bluegill | 0.30 | 0.60 | 0.30 | 0.08 | 1.0–2.5 | 1.5–2.5 | 0 | 0 | Tutorial: plain side runs, forgiving |
| Darter | Perch | 0.45 | 0.70 | 0.40 | 0.07 | 1.0–2.5 | 1.5–3.0 | 0.2 | 0 | Occasional flips: watch which way it runs |
| Bulldog | Carp | 0.60 | 0.60 | 0.50 | 0.06 | 1.0–3.0 | 2.0–4.0 | 0 | 0.35 | Heavy dives: "let it run" |
| Thrasher | Trout | 0.70 | 1.00 | 0.60 | 0.09 | 1.0–2.5 | 1.5–3.0 | 0.5 | 0 | Fast, frequent flips; outruns a weak rod's steer, so you chase |
| Runner | Catfish | 0.85 | 0.80 | 0.80 | 0.05 | 1.0–2.0 | 2.5–4.0 | 0.1 | 0.25 | Long strong runs that take line; needs the tire-then-reel rhythm |

**M2 placeholder species** (`FishSpeciesDef`):

| id | Profile | kg min–max | baseValue | biteWaitMod | minBaitRating | minDepth m |
|---|---|---|---|---|---|---|
| `bluegill` | Steady | 0.2–0.8 | 5 | 0.9 | 1 | 0.8 |
| `perch` | Darter | 0.3–1.2 | 8 | 1.0 | 3 | 1.2 |
| `carp` | Bulldog | 1.0–4.0 | 12 | 1.2 | 1 | 1.5 |
| `trout` | Thrasher | 0.6–2.5 | 20 | 0.9 | 5 | 2.0 |
| `catfish` | Runner | 2.0–7.0 | 35 | 1.4 | 6 | 2.5 |

Junk (`old_boot`, `tin_can`): 0 kg, 2 coins.

M2 species also fill the 12.3 presentation fields (`displayName`, `bodyShape`, `colorA`/`colorB`, `weightSkew` 2.2, `lengthShapeK` 1.0), because the M2 catch reveal builds its procedural fish mesh from them.

**Target fight lengths** (full 18 m Handmade cast, rhythm player): small 15–30 s; mid 35–55 s; trophy 60–110 s. Shorter casts land faster, traded against depth.

### 9.7 Rods, bait, line

**`RodDef`** (mirrored as Core `RodStats`): `id`, `tier` (0–3), `castMaxDist` (also sets `maxLineOut = castMaxDist + 12`), `reelSpeed` (m/s in Calm), `lineStrength` (divides positive strain), `angleWindow`, `steerRate`, `relaxMult`, `shopCost` (coins at Night Camp shops, M4; 0 for the starter), `firstCamp` (the first Night Camp where it is stocked).

| Rod | Tier | castMaxDist | reelSpeed | lineStrength | angleWindow | steerRate | relaxMult | In-run source [tune] |
|---|---|---|---|---|---|---|---|---|
| `rod_twig` | 0 | 14 | 0.9 | 0.8 | 0.50 | 0.50 | 0.9 | Starter kit (every run) |
| `rod_handmade` | 1 | 18 | 1.0 | 1.0 | 0.60 | 0.60 | 1.0 | 120 coins, from Night Camp 1 |
| `rod_carbon` | 2 | 24 | 1.2 | 1.3 | 0.70 | 0.70 | 1.1 | 300 coins, from Night Camp 2 |
| `rod_deepsea` | 3 | 30 | 1.3 | 1.6 | 0.75 | 0.80 | 1.2 | 600 coins, from Night Camp 4 |

M2 grants all four through a debug selector; Handmade is the M2 tuning reference and default only. From M4 a real run starts every player on `rod_twig` (starter kit, R59), and better rods are bought per player from the crew wallet at Night Camp shops (14.7). A bought rod is kept for the rest of that run, including across a save/resume, and is never carried into another run. Rod tiers are ours (Rust has one rod).

**`BaitDef`:** `id`, `rating`, `stackMax`, `maxRating` (`effRating = min(maxRating, rating × loadedCount)`; 1 per cast until M4), `biteSpeedMod`, `junkMult`, `maxNibbles`; from M3 also `affinityTags`, `affinityMult`, `specialMult` (Core `BaitMods`).

| Bait | rating | stackMax | maxRating | biteSpeedMod | junkMult | maxNibbles |
|---|---|---|---|---|---|---|
| `bait_bare_hook` | 1 | 1 | 1 | 1.30 | 1.5 | 1 |
| `bait_worm` | 3 | 3 | 9 | 0.90 | 1.0 | 2 |
| `bait_grub` | 4 | 3 | 12 | 0.85 | 0.9 | 2 |
| `bait_minnow` | 2 | 2 | 4 | 1.00 | 1.1 | 1 |
| `bait_shrimp` | 5 | 2 | 10 | 0.80 | 0.8 | 1 |
| `bait_raw_meat` | 10 | 1 | 10 | 0.70 | 0.5 | 0 |

- Worm/Grub rating, stack and max from Corrosion Hour's 2024 table; Minnow from Rust's Anchovy/Herring row; Shrimp and Raw meat placeholders; bite, junk and nibble values ours.
- **The bare hook is unlimited and always available**, so a crew with no bait is never soft-locked. It mostly brings junk and Steady/Bulldog fish, with slower bites.
- M2: 20 of each bait in the debug loadout; selected with a debug key (`SelectBaitRpc`), not while a cast is active. M4 replaces this with a real bait pouch: starter worms (14.3) plus bait bought at Night Camp shops from the crew wallet (6 per trip, never meta). The pouch is saved per slot in the run snapshot.
- **Line:** no line item. A snap costs the fish and the bait (and in M4 an equipped lure, 14.7).

### 9.8 Catch roll (host)

```csharp
public struct CatchResult {                // Core
    public bool IsJunk; public ushort ItemIndex;    // species or junk registry index
    public int WeightG; public ushort LengthMm; public CatchQuality Quality; public int ValueCoins;
}
public struct FishingRoll {                // Core
    public CatchResult Catch; public float WeightT;
    public float BiteWait; public byte NibbleCount; public float Nibble0, Nibble1, Nibble2;
    public uint FightSeed;                 // non-zero
    public byte SizeClass;                 // 0..2 from weight thirds; cosmetic "the one that got away" jump
}
```

- **Seeding:** `Rng.Derive(runSeed, StreamId.Catch, hostCastCounter)`, where the host increments `hostCastCounter` on every accepted cast. `runSeed` comes from the dev panel in M2–M3 and from the run in M4. **Resume (M4):** `runSeed`, `hostCastCounter` and `resumeCount` are saved in the nightly run snapshot. On resume the host restores `hostCastCounter`, increments `resumeCount` and saves it at once. The catch stream is salted by it: `Rng.Derive(Rng.Derive(runSeed, StreamId.Catch, resumeCount), StreamId.Catch, hostCastCounter)`. A day replayed after a crash therefore never repeats a catch seed, while plan-level rolls (weather, modifiers, quotas) replay identically.
- **M2 `PlaceholderRoller`** (replaced by the M3 `FishingResolver`): eligible = `effRating ≥ minBaitRating` and `depth ≥ minDepth`, each weight 1. Junk weight before `bait.junkMult`: 0.6 at effRating 1–2, 0.2 at 3–5, 0.05 at 6+. One draw over normalised weights; nothing eligible → junk.
- **Weight, quality, value (shared with M3):**
```
skew    = clamp(fish.weightSkew - 0.4 * ctx.Luck, 1.2, 3.0)     // weightSkew default 2.2; Luck 0 until M4 (daily modifier + angler's skills)
t       = pow(u, skew)                                         // skewed toward small fish
weightG = round(weightMinG + (weightMaxG - weightMinG) * t)
quality = t >= 0.9 ? Trophy : t >= 0.6 ? Good : Normal         // ~4.7% Trophy, ~16% Good at skew 2.2
qMult   = Trophy 1.5 | Good 1.15 | Normal 1.0
value   = max(1, round(baseValue * (0.6 + 0.8 * t) * qMult))   // MidpointRounding.AwayFromZero; junk uses its own value, may be 0
lengthMm = round(400 * cbrt(weightG / 1000.0) * fish.lengthShapeK)
```

- **Skill hooks into the roll (M4, R58):** `ctx.Luck = clamp(event luck + skill luck, −1, 1)`, where the angler's Lucky Lure skill adds +0.15 [tune]. Rare Whisperer multiplies the angler's Uncommon and Rare rule weights in the 12.4 pool (step 3) by 1.15 [tune]. Skills apply only to the casting player's own rolls; the host reads them from that player's loadout (14.8).

### 9.9 Networking and host ledger

**Principle (N6):** the host owns bait stock, rolls, the active-cast record and outcomes; the owner runs the bite timer and the fight. The owner reports only an **outcome**; species and weight are already in the host's stored roll. The host never replays the fight and never receives the input stream; determinism exists for tests.

**Components**

| Component | Assembly | Role |
|---|---|---|
| `FishingRodNetwork : NetworkBehaviour` (on `PlayerFP`) | Fishing | RPCs, `NetRodState`, bait NetworkVariables, bridge to the controller. Authority comment: host owns outcomes, owner owns rod state |
| `FishingRodController : MonoBehaviour` | Fishing | Owner: input → state machine, fixed-step tick, view calls |
| `FishingHostLedger` | Core | Host: bait stock, `ActiveCast` per player, validation, timeouts |
| `RodStateMachine`, `FightSim`, `CastMath`, `BitePlanner`, `CatchValidator`, `PlaceholderRoller` | Core | Pure logic |
| `RodPresenter`, `LineView`, `BobberView`, `FishingFeedback` | Presentation | Cosmetics on every peer |

`ActiveCast { uint castId; FishingRoll roll; phase (Waiting/Hooked); double tAccepted, tHooked; float lineLength0, tFlight; bool assist; }`.

**RPCs and NetworkVariables**

| Name | Attribute | Payload | Purpose |
|---|---|---|---|
| `CastRequestRpc` | `SendTo.Server, InvokePermission = Owner` | castId, origin, yaw, chargeNorm, baitIndex, assist | Request a cast |
| `CastResultRpc` | `SendTo.Owner, InvokePermission = Server, DeferLocal = true` | castId, valid, reason, landing, `FishingRollNet` | Accept/reject and send the roll |
| `CancelRpc` | `SendTo.Server`, Owner | castId | Refund in Waiting or on recall |
| `HookedRpc` | `SendTo.Server`, Owner | castId | Host mirror → Hooked |
| `ResultRpc` | `SendTo.Server`, Owner | castId, outcome, reason | Outcome report |
| `CommitRpc` | `SendTo.Owner, InvokePermission = Server, DeferLocal = true` | castId, accepted, finalOutcome | Unlocks the return to Idle |
| `CatchAnnouncedRpc` | `SendTo.ClientsAndHost, InvokePermission = Server` | catcherSlot (byte, like `SoldRpc`), `CatchResultNet`, outcome (Landed or Lost) | Everyone's reveal, toast, journal |
| `RodEventRpc` | `SendTo.NotMe, InvokePermission = Owner` (reliable) | type (Nibble, Bite, HookSet, Snapped, Escaped), sizeClass | One-shot remote cues |
| `SelectBaitRpc` | `SendTo.Server`, Owner | baitIndex | Debug in M2; pouch selection in M4 |
| `NetRodState` | `NetworkVariable<RodStateData>`, write Owner, 15 Hz | below | Continuous view state |
| `NetSelectedBait`, `NetSelectedBaitCount` | server write | byte, ushort | Bait HUD |
| `SelectRodRpc` | `SendTo.Server`, Owner | rodIndex | M2 debug rod selector only (refused while a cast is active). From M4 the host sets the rod itself: `rod_twig` at run start, then the rod a player buys at a Night Camp shop (14.7). The rod changes only during Night Camp and is restored from the snapshot on resume |
| `NetRodIndex` | server write | ushort | The equipped rod. The host uses it for cast validity (`castMaxDist`) and the Landed floor; the owner builds `RodStats` for `FightSim` from it, and remotes use it for rod visuals |

- `RodStateData` (Fishing, ~21 bytes, unmanaged, `INetworkSerializeByMemcpy` + `IEquatable` so it sends only on change [verify change detection uses IEquatable or memcmp; implement both]): `byte state`, `byte flags` (bit 0 Burst, 1 Reel, 2 Dive, 3 Exhausted), `sbyte lateralQ` (×127), `byte tensionQ` (×255), `Vector3 bobberPos` (landing target while Casting, then bobber/fish position), `uint castId`.
- `CatchResultNet`/`FishingRollNet` (Fishing): unmanaged copies with `From`/`ToCore`. Core structs can't implement NGO interfaces. Clients sanity-check received values (index in range, weight in species range, quality 0..2) and drop bad messages with a log.
- **Only the owner gets the roll.** Everyone else gets a "what is it?!" moment until `CatchAnnouncedRpc`. A hooked fish that is lost after the hook (Snapped or Escaped Pinned/ThrewHook/Dropped) is announced as Lost with its species revealed ("Snapped! It was a Lake Pike"). Cancels and misses are not announced.

**Host validation (`FishingHostLedger`, `CatchValidator`)**
- **CastRequest:** no `ActiveCast` for this player; `castId` > last accepted; `chargeNorm` in [0,1]; `origin` within 3 m of the host's view of the player; bait available (bare hook always); recomputed validity passes (from M3 also: grid cell is water, minute < 1200; from M4 also: run phase is Day, because LastCast can start before minute 1200 through the vote; casts are therefore refused in Briefing, LastCast, Settle, NightCamp, SkillPick, InfiniteVote, Result and the Hub, and infinite days use the same Day guard). Distance validity uses the host's `castMaxDist` **including the player's Long Arm skill** (14.8); the owner's marker uses the same value, so honest casts never disagree. Accept → consume bait, roll, create `ActiveCast`, reply valid. Reject → reply invalid with a reason, no bait used.
- **Cancel:** castId matches and phase is Waiting → refund, clear.
- **Hooked:** castId matches, phase Waiting, `now − tAccepted ≥ tFlight + biteWait − hookTolerance` (0.75 s). Too early → resolve Escaped(Invalid) and commit. On accept the host sets `ActiveCast.lineLength0` = horizontal distance from **its current view of the player** to the landing point, minus 1.5 m slack (floor `landDistance`). The player may walk toward the bobber while Waiting, so the cast-time distance would wrongly fail an honest fast land.
- **Result:** castId matches; phase Hooked (or Waiting for Missed). `Landed` requires `now − tHooked ≥ 0.8 × (lineLength0 − landDistance) / rod.reelSpeed − 0.5 s` (physical minimum on the host clock; junk uses `junkDragSpeed`; `rod` is the host's `NetRodIndex`, never a value the owner reports; skills never change `reelSpeed`, so the floor needs no skill term. Skills that change the fight, Iron Grip and Steady Nerves, alter only owner-side `FightSim` inputs, which the host does not replay). Landed → add to the creel (9.10), append to the catch log, `CatchAnnouncedRpc`, publish `FishCaught` on the host event bus. Snapped/Escaped → announce Lost (if hooked), discard the roll. Validation failure → resolve Escaped(Invalid) anyway so the owner never stalls. Always `CommitRpc` then clear.
- **Timeouts** (NetworkVariables send only when dirty, so the host never relies on silence): disconnect clears the cast with no commit (`OnClientDisconnectCallback`); stuck in Waiting after `tFlight + biteWait + 30 s` → refunded cancel; stuck in Hooked after `maxFightTime` 240 s → Escaped(Timeout) and commit.
- **Orphan messages:** a `HookedRpc`, `ResultRpc` or `CancelRpc` whose castId has no `ActiveCast` (already resolved by the host) gets an immediate `CommitRpc(castId, accepted = false, finalOutcome)` repeating the stored resolution. The owner then never waits on a cast the host has cleared.
- **The ledger clock is pause-aware.** The ledger measures `now` with its own clock, which stops while the solo `Paused` flag is set. All the timing checks above use it, so pausing mid-wait or mid-fight never times out a cast or shifts the hook and land floors. Co-op never pauses.

**Latency:** the owner's rod, line, bobber, cues and bite react with zero latency. Cast confirmation usually arrives during the 0.5–0.8 s flight (Relay RTT ~60–200 ms); on slow links the bobber floats until the roll arrives. Remote rod/line view lags ~150–250 ms at 150 ms RTT (one-way latency + up to 67 ms throttle + ~70–100 ms interpolation), which is fine for cosmetics. Remotes lerp `bobberPos`, `lateral` and `tension` toward the latest value.

**Solo:** same components; the owner is the host; host → owner replies are deferred one tick.

### 9.10 Creel (M2–M4)

- Per-player, host-held: `NetCreel` (`NetworkList<CreelEntry>` on `PlayerNetState`, server write). `CreelEntry : unmanaged, IEquatable`: `{ ushort entryId; ushort itemIndex; byte isJunk; int weightG; byte quality; int valueCoins; int caughtAtRunMinute; }`.
- Capacity 12 entries or 15 kg [tune]; a Cooler (M4 gear, bought in-run at a Night Camp shop and kept only for that run) raises it and slows decay. The Deep Pockets skill adds +2 entries and +2 kg (14.8). HUD warns at 80%.
- **Overweight rule:** a landed catch is refused only if the creel already holds 12 entries or is already at or over its kg cap.
  - A catch that pushes the creel over the cap is accepted, and the creel then reads "Full — sell!" until something is sold.
  - Without this rule, M3–M4 trophies heavier than the cap (sleepy_sturgeon up to 25 kg, bruiser_tuna up to 40 kg, both specials, big carp and pike) could never be kept.
  - A refused catch is announced, journaled, and released with a splash ("Creel full!").
- M2: a debug "sell creel" at the `DockBell` adds to a debug wallet. M4: sold at the `SellBin` during Day and LastCast only (section 14.6). The bin is shut at Night Camp, so the hold-vs-sell decision stands. Sale coins go to the in-run crew wallet; only Shells are meta.
- Disconnect: the creel stays in the host's slot ledger and returns on rejoin (R46). Creels are part of the nightly snapshot and come back with the reclaimed slot after a resume (joins open in Hub, Briefing and Night Camp).

### 9.11 Feedback mapping (no HUD meter by default)

| Tension | Cue |
|---|---|
| 0.0–0.3 | Line gently taut; rod at rest |
| 0.3–0.6 | Rod bend scales with tension; reel-click pitch rises |
| 0.6–0.8 | Creak loop (pitch follows tension); line warms in colour |
| 0.8–1.0 | Rod shake, rumble ramp, line in warn colour **plus** a moving dash pattern and width pulse, trauma +0.1/s |
| ≥ 1.0 (grace) | Crack SFX, line shimmer |

**Phase cues** (players must tell a side run from a dive): side run = splash column moving sideways and line swinging; dive = bobber plunges, line points down, deep thrum, no sideways splash; calm = ripples only; exhausted = the fish rolls on the surface.

| Event | Audio | VFX | Camera (owner) | Haptics |
|---|---|---|---|---|
| Ready | soft creak | marker | — | — |
| Charge | whoosh, pitch follows charge | marker slides | — | — |
| Cast release | whip | arc trail | +2° FOV kick 0.15 s | light tap |
| Invalid release | thunk | marker flashes | — | — |
| Landing | plop | ripple ring | — | light tap |
| Recall | soft thud | bounce-back arc | — | — |
| Nibble | quiet plip | small dip + ripple | — | tick |
| Bite | loud splash (spatial + owner 2D) | big splash, bobber under | trauma 0.15 | strong pulse |
| Hook set | reel-click burst | droplets | trauma 0.1 | medium |
| Side run | reel scream | sideways splash column | — | low rumble |
| Dive | deep thrum | plunge, line down | — | slow heavy rumble |
| Snap | snap + plink | line whip; **the fish jumps once, scaled by `sizeClass`, seen by everyone** | trauma 0.5 | strong |
| Escape | line goes slack | bubbles | — | short |
| Catch (everyone) | pop + cheer | fish held up 1.5 s (procedural mesh scaled by weight, species colours); toast "Name caught 3.2 kg Carp ($16)"; bigger burst for trophies | −3° FOV punch-in | double pulse |

**Accessibility (one definition):**
- **Fishing assist** (default off): `snapGrace` 0.75 s, `strainScale` 0.6; turns the line gauge on (which can then be turned off separately). The assist flag is sent with each cast so the host validator uses the matching grace.
- **Line gauge** (default off; Off/Arc/Bar): HUD arc below the crosshair (90°, tick at 0.8) or a thin bar; the local player's own line only. Always free.
- **Camera motion** slider: 0 removes shake, FOV kicks, bob and rumble.
- **Captions** (default on): "Bite!", "Line straining", "Snapped" captions for audio cues.
- **Dev overlay** (F3, editor/dev builds): phase, tension, stamina, line length, timers, last roll.

### 9.12 Tuning table (`FishingTuningDef`)

| Parameter | Default | Range |
|---|---|---|
| `castChargeTime` | 1.0 s | 0.6–1.5 |
| `chargePingPong` | true | |
| `castMinDist` | 2.5 m | 2–4 |
| `minLandDepth` | 0.8 m | 0.5–1.5 |
| `biteWaitMin` / `biteWaitMax` | 4 / 18 s | 2–8 / 10–30 |
| `minBiteWait` | 1.0 s | 0.5–2 |
| `hookBeat` | 0.25 s | 0.15–0.4 |
| `landDistance` | 2.0 m | 1.5–3 |
| `reelStrainBurst` | 0.30 /s | 0.2–0.45 |
| `counterRelief` | 0.5 | 0.3–0.7 |
| `steerStrain` | 0.03 /s | 0–0.08 |
| `outOfWindowStrain` | 0.20 /s | 0.1–0.4 |
| `spoolStrain` | 0.50 /s | 0.3–0.8 |
| `spoolExtra` | 12 m | 6–20 |
| `relaxRate` | 0.45 /s | 0.3–0.7 |
| `relaxWhileReeling` | 0.10 /s | 0.05–0.2 |
| `reelBurstPenalty` | 1.0 | 0.6–1.2 |
| `diveRunMult` | 1.5 | 1.2–2 |
| `snapGrace` | 0.25 s (assist 0.75) | 0.2–0.75 |
| `strainScale` | 1.0 (assist 0.6) | |
| `pinEscapeTime` | 5 s | 4–10 |
| `slackEscapeTime` | 12 s | 8–20 |
| `fishStaminaRegen` | 0.04 /s | 0.02–0.08 |
| `calmCenterPull` | 0.15 /s | 0.05–0.3 |
| `lateralMaxDeg` | 75° | 60–85 |
| `hookedLeash` | 3 m | 2–5 |
| `junkDragSpeed` | 3 m/s | 2–5 |
| `resultTimeout` / `commitTimeout` | 3 s / 3 s | |
| `hookTolerance` (host) | 0.75 s | 0.5–1.5 |
| `maxFightTime` (host) | 240 s | 180–400 |
| `rodStateHz` | 15 | 10–20 |
| `cancelRefundsBait` | true | |

**Bot targets (Bulldog/Carp, Handmade, 18 m cast):**

| Style | Target |
|---|---|
| Greedy (always reels, never steers) | Snaps ≥ 70% |
| Counter-reel (always reels, steers against side runs) | Snaps 10–40% |
| Rhythm (counters side runs, eases off in dives and at the creak) | Snaps ≤ 5%, lands in 35–55 s |

Also: Steady (Rhythm) median 15–30 s; Runner (Rhythm) median 60–110 s, snaps ≤ 15%. **Starter-rod check:** with the Twig rod (tier 0, the rod every new player starts on), the Rhythm bot lands ≥ 90% of fights for every profile at weight t = 0.5, and ≥ 75% at t = 0.9; if Thrasher or Runner fail, retune their `lateralSpeed` or Twig's `steerRate`.

### 9.13 Edge cases

| Case | Handling |
|---|---|
| Invalid spot under the marker | Release does nothing: thunk, stay Ready, no bait |
| Owner/host disagree on validity | Host wins; recall; no bait |
| Walking away while Waiting | Allowed; past `maxLineOut` auto-reel (refunded) |
| Fish over land or dock (cosmetic) | View samples water every 0.2 s and holds the last valid water point; sim unaffected |
| Fish takes all the line | Spooled: clamped, `spoolStrain` applies; tire it first |
| Alt-tab mid-fight | Neutral input → tension relaxes; Run In Background keeps the session alive; rumble off |
| Owner hangs but stays connected | Host timeouts resolve the cast |
| No `CommitRpc` | Owner returns to Idle after 3 s and logs a warning; the rod is never stuck |
| Host disconnects | Session ends (no migration). From M4 the run is recoverable from the host's last Night Camp snapshot via Hub resume; only the in-progress day is lost |
| Late joiner sees a rod mid-fight | `NetRodState` gives state, bobber and tension; species hidden until the reveal |
| Two bobbers overlap | Not handled; a possible M5 tangle event |
| Day end (minute 1200 or the vote) while Casting, Waiting, Bite or Hooked (M4) | LastCast (14.2): Casting and Waiting lines are auto-reeled as a refunded cancel via unsolicited `CommitRpc`. Bite/Hooked fights may finish within the 45 s grace, then resolve Escaped(Timeout), announced as Lost. Settle runs only once no `ActiveCast` remains |
| Night Camp snapshot while a cast or fight is live (M4) | Cannot happen: Night Camp follows Settle, which follows a resolved LastCast. As a guard, entering NightCamp sweeps any straggling `ActiveCast` as a refunded Cancelled commit **before** the snapshot is written. The snapshot has no active-cast field |
| Resume from a snapshot with a cast mid-air (M4) | Impossible by construction (row above): every rod restores to Idle with its bait already refunded. `castId` counters restart per session; the host's `hostCastCounter` and `resumeCount` come from the snapshot (9.8) |

---

## 10. Art, lighting, water and presentation

### 10.1 Principles

- Flat-shaded, low-poly, flat colour. No imported textures, meshes, fonts or sounds; no paid assets.
- Everything here is presentation, derived locally from replicated state, owner-local fishing state or RPCs. Presentation never writes gameplay state.
- Pure maths (wave height, day-clock maths, tension-to-cue mapping, audio sample recipes, quota pace) lives in Core with EditMode tests.
- Shaders are `.shader`/`.hlsl` text; meshes are built in C#; materials, palette, Volume profiles and UI assets are written by editor builders, overwritten in place for stable GUIDs.

**By milestone (R23):** M1 uses flat URP Lit placeholder materials (one per colour), a single fixed directional light and primitive props. M2 adds `MeshBuilder` flat props with palette indices, the Toon, Water, Line and GradientSky shaders, ripples, and fishing audio. M3 adds the seasonal palette swap and `DayNightController` on the dev clock. M4 drives it from the run clock. M5 adds event looks.

### 10.2 Palette and meshes

- **`ArtPaletteDef`** (`Content/Art/ArtPalette.asset`): `slotNames[16]` (0–11 environment: Grass, Foliage, FoliageDark, Wood, WoodDark, Sand, Rock, Reed, Dock, Roof, Accent, Snow; 12–15 reserved); `seasons[4]` (Spring, Summer, Autumn, Winter), each `Color[16]`; `faceJitter` 0.04; `slotTints[4]`; `skinTone`; `phases[4]` (Dawn, Day, Dusk, Night, 10.3); UI theme colours (`uiAccent`, `uiGood`, `uiWarn`, `uiBad`, `uiPanel`, `uiText`).
- Neighbouring slots differ in lightness, not only hue. Anything with gameplay meaning (bobber, cast marker, line warning, quota pace) is also shown by shape or pattern, never hue alone.
- At scene load `Shader.SetGlobalVectorArray("_FGPalette", Vector4[16])` with the run's season. **Always pass full-length arrays**: Unity fixes the size at the first call (16 palette, 8 ripples, 4 waves).
- **`MeshBuilder`** (Runtime): `Box`, `Prism`, `Cylinder`, `Capsule`, `Icosphere`, `Grid`. Flat props use unshared vertices (3 per triangle) with an explicit face normal; `uv1.x` holds the palette slot; vertex colour holds the `1 ± faceJitter` grey multiplier from a seeded hash of `(meshSeed, faceIndex)` so rebuilds are byte-identical. Water grids keep shared vertices. Colliders are Box/Capsule/Sphere; MeshCollider only for coarse terrain/shoreline. EditMode tests: `vertexCount == 3 × triangleCount` for flat primitives; per-face shared normal, slot and colour; byte-identical rebuilds.

### 10.3 Shaders and lighting

**`FishGame/Toon` (M2):** one base material `M_Toon_Base` for environment props; material *instances* are fine (players, fish freshness), `MaterialPropertyBlock` is not. `UnityPerMaterial` holds `_BaseColor`, `_RampMid`, `_RampSoft`, `_UsePalette`, `_Desaturate`, `_RimPow`, `_RimStrength`, identical in every pass. Albedo = `_FGPalette[round(uv1.x)] × vertexColor × _BaseColor` (palette mode) or `vertexColor × _BaseColor`. Lighting: `band = smoothstep(_RampMid − _RampSoft, _RampMid + _RampSoft, saturate(dot(n, L)) × shadowAtten)`; `lit = albedo × lerp(_ToonShade, mainLight.color, band) + albedo × _ToonAmbient × 0.15`; additional lights add a hard-stepped term. Passes: `UniversalForward` (SimpleLit 17.6 keyword set including `_CLUSTER_LIGHT_LOOP` and `_ADDITIONAL_LIGHTS`; `#include_with_pragmas` for Fog; `InputData inputData = (InputData)0` with at least `positionWS` and `normalizedScreenSpaceUV`; the additional-light loop copied from URP `Lighting.hlsl`), `ShadowCaster` (`_CASTING_PUNCTUAL_LIGHT_SHADOW`), `DepthOnly` (needed for water foam).

**`DayNightController` (Presentation, M3):** input `DayFraction01 = minuteSinceDawn / 1200`. Four `DayPhaseAmbient` entries (`startFraction` dawn 0, day 0.10, dusk 0.55, night 0.70 [tune]; light colour and intensity; sky top/horizon/ground; `toonShade`, `toonAmbient`; fog colour and density; warm/cool grade weights), smoothstep-blended over a 0.05 window.
- **One directional light** (Sun/Moon) so URP's main light never swaps. The sun arcs east→west over daylight with elevation ≥ 12°; within 0.02 of sunset intensity fades to 0, the light flips to a fixed high moon direction with a cool tint at ~0.25 intensity and fades back in (and the reverse at dawn). Night stays playable through moon intensity and `toonAmbient`.
- Sets `_ToonShade`, `_ToonAmbient`, `RenderSettings.fogColor/fogDensity`, sky globals for the `FishGame/GradientSky` skybox (Background queue, ZWrite Off, 3-colour gradient + sun/moon disc), and weights two global Volumes (warm sunset, cool night; `ColorAdjustments` + `WhiteBalance` only, tonemapping None) built by `RenderingSetupBuilder`. Camera post-processing must be on. Ambient mode is set once by the builder; particles use URP Unlit particles tinted by the controller.
- **Fog:** built-in URP fog, ExponentialSquared, saved on in every scene and never toggled at runtime (clear days use low density), so the variants survive stripping [verify in the first build]. **Readability rule (EditMode-tested over phase × event data):** fog factor at 25 m ≥ 0.5, i.e. density ≤ ~0.033. Day 0.008, night 0.014, fog event 0.03.

### 10.4 Water

- **`WaveMath` (Core):** sum of 3 directional sines; coefficients in `WaterSettingsDef` (`Vector4 dirAmpLenSpeed[3]`). `HeightAt(x, z, t)` drives the bobber, swimming, floating fish and boats. `WaterTimeDriver` pushes `_Waves` (length 4) and `_WaterTime` (`NetworkManager.ServerTime.Time` in gameplay scenes, `Time.timeSinceLevelLoad` in menus). Gameplay never reads `_WaterTime`; parity comes from one shared formula plus review. Wavelengths ≥ 4× the grid cell. Tests: bounded by Σ amplitudes, continuous in t, deterministic.
- **`FishGame/Water` (M2):** `MeshBuilder.Grid` with ~1–1.5 m cells (e.g. 96×96 over 120 m), vertex-displaced; flat facet normal `normalize(cross(ddy(posWS), ddx(posWS)))` [verify sign in-game]; depth fade `lerp(_ShallowColor, _DeepColor, smoothstep(0, _DepthRange, sceneDepth − fragDepth))`; foam band where depth < `_FoamWidth` broken by animated hash noise; Transparent, ZWrite Off, alpha blend, one `UniversalForward` pass; same fog path. `_DepthRange` 3.0, `_FoamWidth` 0.35.
- **`RippleField`** (scene object, Presentation): fixed `Vector4[8]` of `(x, z, spawnWaterTime, strength)` pushed as `_Ripples`; oldest slot recycled. Spawned by presentation listeners: cast landing 0.6, nibble 0.2, bite 1.0 + two 0.3 follow-ups, snap 0.8, landed 0.7, shove into water 1.0. Ring: `(1 − saturate(|dist − age × 2.2| / 0.25)) × saturate(1 − age / 2.5) × strength`. Ripples add foam and brightness only (sub-cell normals can't show).
- **M1 water:** a 120 × 120 m flat visual plane at y 0 with a flat blue placeholder material (its generated MeshCollider removed). On layer Water, a separate **`BoxCollider` trigger** (120 × 8 × 120 m, top face exactly at y 0) is both the cast raycast target and the `WaterVolume`. A concave MeshCollider can't be a trigger; `WaterSurface.HeightAt` returns 0 until M2's `WaveMath` lands.

### 10.5 Rod and line presentation (M2)

- Rod: 6 cylinder segments, each rotated toward the line by `bend × weight[i]`, `bend = curve(tension)`; visible to owner and remotes.
- Line: quadratic Bezier rod tip → bobber, sag `(1 − tension) × 0.15 × length`; `LineRenderer` with the `FishGame/Line` unlit shader (`_Tension` drives colour and the dash `frac(uv.x × k)`).
- Splash particles during Burst; bobber bob from `WaveMath.HeightAt`.

### 10.6 Event looks (M5)

| Event | Lighting/fog | World FX |
|---|---|---|
| Storm | fog ×1.5, light ×0.5, cool grade +0.3 | camera-local rain; `_RainAmount` speckle rings; lightning via host RPC with position + seed (same strike for everyone): light pulse 0.08 s + thunder delayed by distance/343 s |
| Fog | fog toward the readability max, colour toward horizon | muffled-wind ambience |
| Heatwave | warmer light, warm grade 1 | faster fish desaturation |
| Frenzy | none | bubble particles at the replicated spot + banner |

Camera motion 0 replaces lightning flashes with a brief sky tint. **Underwater:** when the camera is below `WaveMath.HeightAt`, a full-screen tint, raised local fog and a low-pass on the listener.

### 10.7 Performance baseline

Recorded at M1 on the dev machine: lake dock, 4 bodies, development build, 1080p, frame time and SRP batch count. A later regression > 10% needs a note in the milestone summary. The water pass GPU time is recorded at M2.

---

## 11. UI, HUD and audio

### 11.1 Technical setup (UI Toolkit, R22)

- One `PanelSettings` asset (Scale With Screen Size, reference 1920×1080, match 0.5) and the default runtime theme, created by `UiBuilder`. UXML/USS under `UI/`. Frequently updated HUD elements live in their own `VisualElement` subtree; static panels don't rebuild every frame.
- Input: UI Toolkit runtime input under Input-System-only mode [verify buttons receive clicks; if not, add an `EventSystem` with `InputSystemUIInputModule` using the `UI` map].
- Cursor locked in gameplay, unlocked while any modal is open (menu, pause, journal, shop, summary, creel).
- Views are bound to plain C# view models; a **presenter** per screen reads NetworkVariables and events and fills the view model. Views never touch NetworkBehaviours.
- Prompts come from `InputAction.GetBindingDisplayString()` for the last-used device. Strings live in one `UiStrings` table (message key → English).
- No icon sprites in M1. From M3, `IconFactory` generates runtime textures (journal silhouettes) from fish body proportions, cached, never saved as files.
- Font: the UI Toolkit default runtime theme font. Join codes and UI text are ASCII.
- Layout targets: 16:9 and 16:10 at 1280×720 and 1920×1080; UI scale 80–150% (PanelSettings scale) must not clip at 1280×720.

### 11.2 Screen flow (`UiFlowController`)

```
Boot -> MainMenu
MainMenu -[Host]-> Creating (spinner, Cancel) -> gameplay scene; join code shown in HUD toast + Pause
MainMenu -[Join]-> EnterCode -> Joining (spinner, Cancel) -> gameplay scene (NGO scene sync)
MainMenu -[Solo]-> Loading -> gameplay scene (local host, no UGS)
MainMenu -[Settings]-> Settings -> MainMenu
Game -[Esc]-> Pause (online: world keeps running; solo: clock + fishing + locomotion pause) -> Game | Settings | Leave -> MainMenu
Any -> [disconnect / host left / error] -> MainMenu + error toast
```

The lobby is **in-world**: every mode drops onto the dock (M1–M3) or into the Hub (M4+), where the crew can goof around while friends join. Dev builds add "Host Direct" and "Join 127.0.0.1" buttons.

| Situation | Key | Recovery |
|---|---|---|
| Code not found / invalid | `err.code_not_found` | Back to EnterCode with the text kept |
| Session full | `err.session_full` | MainMenu |
| Network start failed | `err.network` | Retry |
| No NGO connect within 15 s of a Lobby join | `err.timeout` | Retry |
| Services, auth or internet unavailable; project not linked | `err.services` | "Play Solo instead" |
| Host left / session ended | `err.host_left` | MainMenu |
| Rejected (joins closed, kicked) | `err.rejected` / `err.joins_closed` | MainMenu |
| Version mismatch | `err.version` ("Different game version — update your build") | MainMenu |
| Anything else | `err.generic` | MainMenu |

Raw exception text is logged, never shown.

### 11.3 Main menu, join code, pause

- Buttons: Host, Join, Solo, Settings, Quit; disabled while an operation is pending. Display name field (1–16 chars, saved in settings). Meta summary line from M3/M4: maps unlocked, map skills, Shells, journal %, and "Saved run: Lake Dock, Night 3" when this machine holds a run snapshot.
- Join field: `TextField`, max 16, trimmed, paste accepted, Join disabled while empty; auto-uppercase only if V3 confirms codes are case-insensitive.
- **Join code (host):** a 10 s HUD toast after creation; always visible in the HUD while in Hub, Briefing or Night Camp (when joins are open) and in the Pause menu, with **Copy** (`GUIUtility.systemCopyBuffer`), shown exactly as `session.Code`. **Streamer mode** masks it as `••••••` with hold-to-reveal.
- Pause: Resume, Settings, Leave (and from M6 per-player mute).

### 11.4 HUD

Minimal by default (Rust feel). Nothing over the screen centre except the crosshair and the cast charge ring.

| Element | Anchor | Source | From |
|---|---|---|---|
| Crosshair, interact prompt | centre | local raycast | M1 |
| Input prompt strip | bottom-left | binding strings | M1 |
| Toasts (code, catches, journal unlocks) | bottom-right, queued; crew catch toasts ≤ 1 per 3 s per player | RPC or local | M1 |
| Cast charge ring | around crosshair | local | M2 |
| Cast marker | **in world** on the water: valid = solid bright ring with centre dot; invalid = hollow grey dashed ring with × | local | M2 |
| Bite caption ("Bite!") | bottom-centre | only with captions on, or a "Click!" prompt when `hookWindow > 0` | M2 |
| Line gauge (optional) | below crosshair | own line | M2 |
| Creel count/kg | bottom-right | `NetCreel` | M2 |
| Clock and day (`Day 1/7  2:20 PM`; infinite `Day 9 (Infinite)`; 10-minute steps) | top-left | clock anchor | M3 (dev) / M4 |
| Quota bar | top-right | `RunDirector` | M4 |
| Crew panel (hold Tab): slot colour, name, contribution, catch count; disconnected players greyed | centre overlay | roster + run stats | M4 |
| Event / Last Call banner | top-centre, one at a time ≥ 2 s | `DayDirector` | M4 |

**Quota bar (M4):** shows the day's `QuotaKind` (`$` or `kg`) from host integer values; clients never recompute. Two segments: **banked** (solid; sold today) and **held** (hatched; estimated sale value or kg of the crew's unsold creel fish), so the hold-vs-sell gamble is visible. A pace tick at `QuotaPace.Expected(DayFraction01)` (gentle S-curve). Pace state: ahead ▲ `uiGood`, close (within 15%) ● `uiWarn`, behind ▼ `uiBad`; the shape always accompanies the colour.

### 11.5 Day summary, Night Camp, shop, awards (M4)

- **Settle screen** (~6 s, after each of the 7 days and every infinite day): sales vs quota (pass/fail), catches by rarity, best catch, Shells earned, next day's quota and kind (from the 7-entry curve, or the infinite growth), one forecast line. Then the crew is at Night Camp (after a won day 7: the skill pick, then the Infinite vote).
- **Awards** computed by the host from run stats, each with a short sting: "Biggest Catch", "Most Snapped Lines", "Overachiever", and from M5 "Seagull's Best Friend". Cheap, watchable friend-slop.
- **Night Camp** (in-world at the map's campfire, clock frozen at 02:00 under night lighting; everyone is teleported to the fire ring): the camp shop (`ShopCounter`), the `CampRadio` (tomorrow's weather, modifier and escalation tier; one story beat line for the night, 14.5), the crew's run totals, the join code, and a **Ready** toggle (panel button or E at the `CampFire`). **Advancing:** everyone ready, or the host force-continues after a 10 s countdown, or the 180 s cap [tune]. A corner tag "Run saved" appears once the host's snapshot is written (14.9). The host's pause menu adds **Save & quit to Hub**.
- **Skill pick screen** (after the day-7 win, at the `SkillBoard`): the 12-skill pool as cards with one-line effects. Skills already in another of the player's slots are greyed out. On a re-win the current pick for this map is pre-selected (keep or swap). 60 s [tune]; an unpicked slot can be picked later in the Hub.
- **Keep Fishing (Infinite) vote:** Yes/No per player, unanimous to pass, 30 s [tune]. It shows the next quota and "Your map skill is already saved."
- **Shop panel** (opened at the camp `ShopCounter`, every Night Camp, 6 per trip): grid of cost, owned count and one-line effect, plus the rod rack (your next rod tier and its price). No meta or cosmetic items. Pending state up to 3 s, then `err.shop_timeout`; rejections show their reason ("Not enough cash — someone just bought it").

### 11.6 Settings (`settings.json`, separate from `meta.json`)

Same atomic writer as saves. Saved when the panel closes or on quit, never per slider tick. Values apply live. In the editor, MPPM clones append a suffix from their player index/tag (`settings_p2.json`, `meta_p2.json`) [verify clones share `persistentDataPath` and which `CurrentPlayer` API keys the suffix].

| Setting | Type | Default | From |
|---|---|---|---|
| Master, Sfx, Ambience, Music, Ui, Voice volume | 0–100 | 80, 80, 70, 60, 80, 80 | M1 (Music M4, Voice M6) |
| Display name | text 1–16 | "Fisher" | M1 |
| Mouse sensitivity | 0.04–0.30 °/count | 0.12 | M1 |
| Gamepad look speed | 0.2–3.0 × 180°/s | 1.0 | M1 |
| Invert Y | bool | false | M1 |
| Field of view (horizontal) | 80–110° | 90° | M1 |
| Camera motion | 0–1 | 1 | M1 |
| Sprint toggle, aim toggle | bool | false | M1 |
| Streamer mode | bool | false | M1 |
| UI scale | 80–150% | 100% | M1 |
| Key/button rebinds | overrides JSON | — | M1 |
| Line gauge | Off/Arc/Bar | Off | M2 |
| Captions | bool | true | M2 |
| Fishing assist | bool | false | M2 |
| Voice mode | voice activation / push-to-talk | voice activation | M6 |

### 11.7 Audio

**`AudioBus`** (Runtime, code only; no AudioMixer asset, because there is no public editor API to author mixer groups and exposed parameters): categories `Sfx`, `Ambience`, `Music`, `Ui`, `Voice`. Each AudioSource registers with a category; effective volume `cueBase × cat² × master²` (from 0–100 settings). Ambience and Music duck to 50% over 0.3 s during big stingers (bite, snap, last call, summary) and, from M6, while Vivox reports speech. If a mixer is wanted later, the user authors it once.

**Procedural clips:** `SfxRecipes` (Core) are pure `float[] Generate(RecipeParams, sampleRate 44100, seed)`; `SfxGenerator` (Runtime) wraps them with `AudioClip.Create` + `SetData`, all generated at Bootstrap and cached. Loops are a whole number of base periods; noise loops get a 20 ms crossfade. Per play: pitch ±7%, volume ±1.5 dB. `SfxCueTableDef` maps cue id → recipe, category, base volume, `minInterval`, spatial, `maxDistance` (default 30 m, logarithmic rolloff).

| Id | Recipe | Length | From |
|---|---|---|---|
| `Ui_Click`, `Ui_Confirm`, `Ui_Error` | sine pairs, short envelopes | 0.08 s | M1 |
| `Wind_Ambient` | filtered pink noise, slow LFO | loop 6 s | M1 |
| `Water_Lap` | low-passed noise bursts scheduled every 0.6–1.4 s | 0.4 s | M1 |
| `Bell_Ring` | struck-bell sine partials | 1.0 s | M1 |
| `Cast_Whoosh` | band-passed noise, 600→200 Hz sweep | 0.35 s | M2 |
| `Splash_Small` / `Splash_Big` | noise burst τ 0.08 s / low noise + 90 Hz sweep τ 0.25 s | 0.25 / 0.6 s | M2 |
| `Bite_Plop` | 320 Hz sine + noise | 0.2 s | M2 |
| `Nibble_Tick` | 1800 Hz click | 0.03 s | M2 |
| `Reel_Click` | single tick scheduled at `reelSpeed × 6`/s | 0.02 s | M2 |
| `Line_Creak` | filtered saw 200 Hz, pitch up to ×2.1 with tension | loop 0.5 s | M2 |
| `Dive_Thrum` | low sine with slow AM | loop 1 s | M2 |
| `Snap` | high-passed noise + 2400 Hz ping | 0.4 s | M2 |
| `Catch_Flop`, `Catch_Cheer` | 3 low thumps / rising chord | 0.5 / 0.8 s | M2 |
| `Coin_Pop` | 900 + 1350 Hz blips | 0.15 s | M4 |
| `Horn_LastCall` | two detuned saws, slow attack | 1.2 s | M4 |
| `Award_Sting` | rising 3-note arpeggio | 0.6 s | M4 |
| `Seagull_Cry` | FM 1400 Hz carrier, 9 Hz modulator | 0.8 s | M5 |
| `Raccoon_Chitter` | short noise bursts, random pitch | 0.3 s | M5 |
| `Storm_Rumble` / `Storm_Thunder` | brown noise loop / crack + long decay | 4 / 2.5 s | M5 |

**Mapping:** spatial 3D at the source for everyone (bobber splashes, bites and snaps are heard by friends); only the owner's own reel/creak loops and UI are 2D. Rate-limited per source by `minInterval` (e.g. nibble 0.3 s). If a clip fails to generate, warn and stay silent; gameplay never waits on audio.

**Music and ambience:** M1 ambience bed (wind + scheduled water lap) in every gameplay scene; no music. M3 seasonal ambience parameter sets. M4 calm procedural day-phase pads (one sine-chord loop per phase, 4 s crossfades), quiet enough to talk over. M5 storm and frenzy stingers.

**Tests:** each recipe: correct sample count, `max(|s|) < 1.0`, no NaN/Inf, loop seam delta < 0.05. M2 adds a debug "audition all cues" menu that must play every clip without exceptions.

---

## 12. Fish ecology (M3, outline with the data later milestones depend on)

### 12.1 Goals

1. **Variety from narrow windows:** each spot plays differently by time band, season, weather and sub-area.
2. **No dead ends:** with a tier-0 rod and neutral bait, every season × band × weather × sub-area yields at least one fish (V1).
3. **Where you fish is a choice that changes during the day:** no sub-area is always best, and the best place for cash often differs from the best place for weight (V5).
4. **Explicit odds:** junk is a flat share per sub-area; fish odds are explicit weights; pool size doesn't change the junk rate.

### 12.2 Time, seasons, weather (Core)

```csharp
public static class DayClock {
    public const int DayLengthMinutes = 1200;              // 06:00 -> 02:00, never wraps; valid casting 0..1199
    public const int LastCallMinute   = 1140;              // 01:00
    // Bands [start, end): Dawn 06-08, Morning 08-11, Midday 11-14, Afternoon 14-17, Dusk 17-20, Night 20-02
    public static readonly int[] BandStart = { 0, 120, 300, 480, 660, 840 };
    public static TimeBandMask BandAt(int minute);
}
[Flags] public enum TimeBandMask : byte { None = 0, Dawn = 1, Morning = 2, Midday = 4, Afternoon = 8, Dusk = 16, Night = 32, All = 63 }
[Flags] public enum SeasonMask : byte { None = 0, Spring = 1, Summer = 2, Autumn = 4, Winter = 8, All = 15 }
public enum Season : byte { Spring, Summer, Autumn, Winter }
[Flags] public enum WeatherMask : byte { None = 0, Clear = 1, Rain = 2, Fog = 4, Storm = 8, Heatwave = 16, All = 31 }
[Flags] public enum FishTags : ushort { None = 0, Nocturnal = 1, Predator = 2, Bottom = 4, Schooling = 8, Surface = 16 }
public enum Rarity : byte { Common, Uncommon, Rare, Legendary }    // display only, never base odds; the one exception is the Rare Whisperer skill's weight term (12.4 step 3)
public enum CatchQuality : byte { Normal, Good, Trophy }
```

- Display time = `(06:00 + minute)`, shown in 10-minute steps. Day fraction for lighting = `minute / 1200`.
- **Weather segments:** five 240-minute blocks per day (06–10, 10–14, 14–18, 18–22, 22–02), constant within a segment. The M3 track produces only Clear, Rain or Fog. M5 events add `Storm` (always with Rain) and `Heatwave` (always with Clear), so Rain/Clear rules keep working on event days.
- **`WeatherTableDef`:** base Markov matrix (rows current, columns next): Clear → 0.60/0.25/0.15; Rain → 0.40/0.50/0.10; Fog → 0.40/0.20/0.40. Season column multipliers then per-row renormalisation: Spring Rain ×1.5; Summer Fog ×0.3; Autumn none; Winter Fog ×1.3.
- **`WeatherDirector`** (host, plain class): the run planner pre-rolls all 35 segments (7 days × 5) at Briefing from `Rng.Derive(runSeed, StreamId.Weather)`; segment 0 of day 1 uses the Clear row and each later segment continues the chain. Infinite days continue the chain one day (5 segments) at a time from `Rng.Derive(runSeed, StreamId.Weather, d)` at the preceding Night Camp. The track is part of the run snapshot. The season is rolled once per run and holds for the whole week; the map is chosen in the Hub, not rolled. In M3 the dev panel's "new day" re-rolls a day.

### 12.3 Data model (`FishGame.Data`)

**`FishSpeciesDef`:** `id` (no spot prefix), `displayName`, `rarity`, `baseValue` (coins for a Normal fish at t = 0.5), `weightMinG`/`weightMaxG`, `weightSkew` (2.2), `lengthShapeK` (1.0; eels ~1.6, puffer ~0.8), `fight` (`FightProfileDef`), `tags` (`FishTags`), `bodyShape` (`Round, Long, Flat, Eel, Squid`), `colorA`/`colorB`, `biteWaitMod` (1.0), `hookWindow` (0), `isSpecial`; from M5 `isBoss`, `minReelers`, `monsterProfile`.

**`SpotDef`** (run-level; **a map is a `SpotDef` plus its `Spot_<Id>` scene**, R57): `id`, `displayName`, `sceneName`, `mapOrder` (0–4; beating map N unlocks map N+1, with no currency cost), `zone` (`FishingZoneDef`), `baseQuotaCoins[4]` and `baseQuotaKg[4]` (per season; the 7-entry `dayMult` curve multiplies them, 14.4), `weightDayChance`, `escalation` (`EscalationTierDef[7]`, one per trip day, 14.5), `climaxEvent` (the day-7 `EventDef`) and `bossSpecies` (from M5), `campStoryKeys` (map-flavoured Night Camp lines), `palette` overrides, `beats` (`DayBeatDef[]`).

**`FishingZoneDef`:** `subAreas` (`SubAreaBand { id, minShoreM, maxShoreM, junkChance }`, contiguous from 0, last open-ended), `rules` (`SpawnRule[]`, sorted by `fish.id` ordinal at registry build), `junk` (`{ JunkDef, weight }[]`), `weather` (`WeatherTableDef`), `shoreGrid` (`ShoreGridDef`).

**`SpawnRule`** (serializable): `fish`; `subAreaIds` (empty = all; resolved to a byte mask); `seasons` (All); `weather` (All; matches on any shared bit); `bands` (All); `weight` (10, > 0, ignored for specials); `minRodTier` (0); `minBaitRating` (0); `minDepth` (0 m); `special` (false); `specialChance` (0..0.5); `catchLimitPerRun` (−1 = unlimited, crew-wide); from M5 `minPlayersInSession`.

**`JunkDef`:** `{ id, displayName, value 0..5, bodyShape }`; 0 kg toward weight quotas.

**`ContentRegistry`:** lists every species, spot (map), zone, junk, rod, bait, shop item, event, fight profile, and from M4 skill, cosmetic and escalation tier. At startup it builds Core databases with ordinal-sorted indices and computes `ContentHash`. Validated in EditMode and, in development builds, on load.

### 12.4 Core context and resolver

```csharp
public struct BaitMods { public int Rating; public FishTags AffinityTags; public float AffinityMult, JunkMult, SpecialMult; }
public struct FishingContext {
    public int ZoneIndex; public byte SubAreaIndex; public float Depth;
    public int Minute; public Season Season; public WeatherMask Weather;
    public int RodTier; public BaitMods Bait; public bool InHotspot;
    public float Luck;                        // -1..1, size skew only (dev panel M3, daily modifier + angler's skills M4)
    public EventMods Events;                  // multipliers by tag (M4 data-only modifiers scaled by escalation intensity, M5 hazards)
    public SkillMods Skills;                  // the angler's own map skills (M4, 14.8); neutral in M2–M3
    public int PlayersInSession;
}
public interface IWorldContext { Season Season { get; } WeatherMask Weather { get; } int Minute { get; } bool IsInHotspot(float x, float z); }
public sealed class FishingResolver {
    public FishingResolver(EcologyDatabase db);
    public CatchResult Resolve(in FishingContext ctx, ref Pcg32 rng, CatchLedger ledger);
    public bool Passes(in SpawnRuleData rule, in FishingContext ctx, CatchLedger ledger);   // reused by journal "biting now"
}
```

`IWorldContext` lives in Core and is implemented by `WorldState` (Gameplay). The host builds `FishingContext` at cast accept (R18); the minute drift until the bite (≤ ~45 in-game minutes in the worst case: 18 s × bare hook 1.3 × `biteWaitMod` 1.4 ≈ 33 s at 1.333 min/s) is accepted.

**Gate `Passes`:** sub-area mask empty or has the bit; season bit; any shared weather bit; band bit; `RodTier ≥ minRodTier`; `Bait.Rating ≥ minBaitRating`; `Depth ≥ minDepth`; `PlayersInSession ≥ minPlayersInSession`; catch limit not reached (skipped when `ledger` is null).

**Algorithm:**
1. **Junk share:** `junk = clamp(subArea.junkChance × Bait.JunkMult × (InHotspot ? 0.5 : 1) × Events.junkMult, 0, 0.6)`; `rng < junk` → junk.
2. **Special tier:** eligible special rules in array order; each rolls `min(0.5, specialChance × Bait.SpecialMult × (InHotspot ? 2 : 1) × Events.rareChanceMult)`; first success wins.
3. **Weighted pool:** each eligible non-special rule `w = weight × (tag match ? Bait.AffinityMult : 1) × Events.weightByTag(tags) × Skills.RarityMult(fish.rarity)` (1 unless the angler has Rare Whisperer: ×1.15 for Uncommon and Rare, 14.8).
4. **Empty pool → junk** (V1 makes this impossible at rod 0 with neutral bait; it can happen at a catch limit).
5. **Draw** `roll = rng × Σw`, walk in array order.
6. **Junk item** by weight from `zone.junk`.
7. Weight, quality and value as in 9.8.

The resolver runs only on the host; determinism is for tests and repeatable dev seeds.

### 12.5 Spots and sub-areas

| Spot | Sub-area | Shore distance | Junk chance |
|---|---|---|---|
| `lake_dock` (existing M1 map) | `reeds` / `open_water` / `deep_water` | 0–5 / 5–10 / 10+ m | 0.20 / 0.12 / 0.10 |
| `harbour_pier` (new map, built by SceneBuilder in M3; from M4 unlocked by beating `lake_dock`, map 1 → 2) | `shallows` / `channel` / `open_sea` | 0–5 / 5–12 / 12+ m | 0.20 / 0.12 / 0.10 |

**Shore-distance grid (`ShoreGridDef`):** `width`, `height`, `cellSizeM = 1`, `originX/Z`, `ushort[] shoreDm` (decimetres; `0xFFFF` = not water); ~30 KB for 120 × 120 m. **Bake (SceneBuilder):** `Physics.SyncTransforms()`; per cell a downward raycast where a first hit on `Water` = water, on `Shore` = land, `Structure` ignored (under a pier is water); Euclidean distance transform in Core (`DistanceField.Compute(bool[] land, w, h)`, two-pass chamfer); written into the existing asset in place. **Reachability check (fails the build):** each sub-area has ≥ 25 water cells within [`castMinDist`, 14 m] of some `StandPoint` marker. The lake-dock map gets StandPoint markers and the Shore/Structure layer split in M3; the dock end and pier end reach far bands, banks reach near bands.

### 12.6 Roster (23 regular + 2 specials)

Band shorthand Da Mo Mi Af Du Ni; Sw = spawn weight; Rod = min rod tier; "Any" weather = all bits.

| # | id | Zone: sub-areas | Seasons | Weather | Bands | kg | baseValue | Sw | Rarity | Rod | Profile | Tags |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | mirror_minnow | lake: reeds, open_water | All | Any | Da Mo Mi Af | 0.05–0.2 | 5 | 10 | Common | 0 | Darter | Schooling |
| 2 | bluegill | lake: reeds, open_water | Sp Su Au | Any | Mo Mi Af Du | 0.2–0.6 | 8 | 10 | Common | 0 | Steady | Schooling |
| 3 | yellow_perch | lake: open_water, deep_water | Sp Au Wi | Any | All | 0.2–1.2 | 9 | 10 | Common | 0 | Darter | Schooling |
| 4 | grumpy_carp | lake: open_water, deep_water | Su Au | Any | Mi Af Du Ni | 2–12 | 10 | 9 | Common | 0 | Bulldog | Bottom |
| 5 | bucketmouth_bass | lake: deep_water, open_water | Sp Su | Any | Da Mo Du Ni | 0.5–4 | 11 | 9 | Common | 0 | Bulldog | Predator |
| 6 | silver_mullet | lake: reeds, open_water; harbour: shallows, channel | All | Any | Du Ni Da | 0.3–2 | 8 | 9 | Common | 0 | Steady | Schooling |
| 7 | lake_pike | lake: deep_water, reeds | Au Wi | Any | Da Mo Du Ni | 1.5–9 | 20 | 9 | Common | 0 | Runner | Predator |
| 8 | sunny_trout | lake: open_water | Sp Su | Clear | Mi Af | 0.5–3 | 30 | 12 | Uncommon | 0 | Runner | Surface |
| 9 | mudcat | lake: reeds, open_water | Sp Su Au | Rain, Fog | Du Ni Da | 0.8–5 | 22 | 12 | Uncommon | 0 | Steady | Bottom, Nocturnal |
| 10 | frost_char | lake: deep_water, reeds | Wi | Any | Mi Af | 0.4–2.5 | 26 | 10 | Uncommon | 0 | Runner | — |
| 11 | glimmer_eel | lake: deep_water | Su Au | Rain, Fog | Du Ni | 0.5–2.5 | 60 | 8 | Rare | 1 | Thrasher | Nocturnal |
| 12 | sleepy_sturgeon | lake: deep_water | Sp Wi | Any | Mo Mi Af | 6–25 | 75 | 3 | Rare | 2 | Bulldog | Bottom |
| 13 | pier_sardine | harbour: shallows, channel | Sp Su Au | Any | Da Mo Mi Af | 0.1–0.3 | 5 | 10 | Common | 0 | Darter | Schooling |
| 14 | ice_herring | harbour: shallows, channel | Au Wi | Any | All | 0.2–0.6 | 8 | 7 | Common | 0 | Darter | Schooling |
| 15 | grumble_mackerel | harbour: channel, open_sea | All | Any | Da Mo Mi Af | 0.4–1.5 | 12 | 10 | Common | 0 | Runner | Schooling |
| 16 | moon_squid | harbour: open_sea, channel | All | Any | Du Ni | 0.5–3 | 16 | 9 | Common | 0 | Thrasher | Nocturnal |
| 17 | gawping_gurnard | harbour: channel, shallows | Sp Su | Any | Mo Mi Af Du Ni | 0.3–1.8 | 14 | 8 | Common | 0 | Steady | Bottom |
| 18 | sneaky_flounder | harbour: shallows, channel | Sp Au Wi | Any | Da Mo Mi Af | 0.5–2 | 15 | 10 | Common | 0 | Steady | Bottom |
| 19 | red_snapper | harbour: channel | Su Au | Rain, Fog | Af Du Ni | 1–4 | 28 | 12 | Uncommon | 0 | Bulldog | Predator |
| 20 | grumpy_puffer | harbour: channel, open_sea | Sp Su | Clear | Mi Af | 0.3–1 | 32 | 12 | Uncommon | 1 | Thrasher | Surface |
| 21 | bruiser_tuna | harbour: open_sea | Su Au | Clear, Rain | Da Mo | 8–40 | 80 | 3 | Rare | 2 | Bulldog | Predator |
| 22 | storm_ray | harbour: open_sea | Au Wi | Rain, Fog | Af Du | 3–15 | 65 | 7 | Rare | 1 | Thrasher | Bottom |
| 23 | lanternfish | harbour: open_sea, channel | Au Wi | Fog | Du Ni | 0.1–0.4 | 70 | 10 | Rare | 1 | Darter | Nocturnal |

**Specials** (`special`, independent roll, `catchLimitPerRun = 1`, Legendary, Bulldog): `gilded_carp` (lake deep_water, all gates, rod 0, chance 0.02, 3–15 kg, value 200); `old_barnacle` (harbour open_sea, all gates, rod 0, chance 0.015, 5–20 kg, value 250). Journal total: 25 entries.

**M2 placeholder migration (M3 step 1):**
- `ContentBuilder` is create-if-missing, so it won't apply this roster on its own. An explicit one-off M3 migration step:
  - deletes the M2-only species assets `perch`, `carp`, `trout` and `catfish`;
  - overwrites `bluegill` with its 12.6 values;
  - bumps `dataVersion`.
- The five `FightProfileDef` assets and the rods and baits are kept.
- Deleting assets that are no longer wanted is fine; the GUID rule in 4.6 is about recreating assets that are kept.

**Junk** (equal weights): lake_dock `old_boot`, `tin_can`, `soggy_newspaper`, `lost_sock`; harbour_pier `old_boot`, `rope_scrap`, `seaweed_clump`, `empty_bait_tub`.

The band layout in R1 keeps every roster row's intent: Night is now 20:00–02:00, so night fish get a longer window, which the balance reports (12.9) must re-check. Example moments: summer midday at the lake under clear sky, Sunny Trout in open water pays best; the same day turns to rain at dusk and the Mudcat arrives in the reeds and the Glimmer Eel in deep water (rod 1), so the crew splits; autumn fog at the harbour at night brings the Lanternfish; winter at the lake means Frost Char at midday, Pike after dusk.

### 12.7 Activity hotspot (M3, with a cut-line)

- `HotspotDirector` (host): at most one hotspot per spot; first 60–120 min after day start, then 60–120 min after the previous one ends; lasts 45–60 min; random reachable water cell; radius 3 m.
- Effects: junk ×0.5, special chance ×2, bite wait ×0.6.
- Replicated as `WorldState.NetHotspot` (`HotspotState : INetworkSerializeByMemcpy { float x, z, radius; int endMinute; byte active; }`); every client spawns local splash VFX. Hotspots count as day beats (13.2).
- Cut-line: if M3 runs long, ship with `InHotspot = false`; resolver and tests already cover it.

### 12.8 `WorldState` (Gameplay, scene-placed in each Spot scene from M3)

Server-write NetworkVariables: `NetSeason`, `NetWeather` (current segment mask), `NetClock` (`ClockAnchor`, 14.2), `NetHotspot`. The daily modifier(s) and escalation tier live on `DayDirector` from M4 (`NetDailyModifier`, `NetDailyModifier2`, `NetEscalationTier`; 15.4), not here. Registers as `IWorldContext`. In M3 a host dev clock writes the anchor (rate 1.333 min/s, toggleable); M4's `RunDirector` drives it.

### 12.9 Validators and tests (M3)

**Data validators (hard fail):** ids unique; references resolve; `weightMinG < weightMaxG`; `baseValue > 0`; non-special `weight > 0`; specials `specialChance` in (0, 0.5] and `fish.isSpecial == rule.special`; sub-areas contiguous from 0; `junkChance` in [0, 0.6]; every species has a fight profile.
- **V1, no dead sub-area:** every spot × season × band × {Clear, Rain, Fog} × sub-area at rod 0 with neutral bait has ≥ 1 passing non-special rule.
- **V2, variety:** same contexts with sub-areas mixed uniformly → ≥ 3 distinct species, none above 50%.
- **V3, reachability:** every species (specials included) passes in ≥ 1 context at rod 3.
- **Fightability:** the Rhythm bot lands every rod-0 species with the Twig rod at ≥ 90% (catches tier-0 Thrasher/Runner fish that outrun a weak rod's steer).

**Resolver tests (deterministic):** same seed and context → identical 1,000-cast sequence; each gate excludes correctly; junk share within ±0.015 of the expected value over 10,000 casts (halved in a hotspot, 100% with an empty pool); species shares within ±15% relative of `wᵢ/Σw`; a forced special at 0.02 within ±20% of `0.02 × (1 − junk)` over 50,000 casts; weight draws in range, mean `t` 0.3125 ± 0.01 at skew 2.2, trophy rate 4.7% ± 0.7%; fish value ≥ 1 and a minnow at t = 0 is 3; `DistanceField` on known grids; journal save round-trip and `.bak` fallback.

**Balance reports (`[Category("Balance")]`, loose bounds, printed tables):**
- **V4 expedition sim:** solo, 7 days, ~60 casts per day (~420 per run), per spot × season, 400 runs, real resolver and weather director, with the rod tier following the default in-run upgrade path (Twig days 1–2, Handmade 3–4, Carbon 5–6, Deep-sea 7) and the day's escalation modifiers applied from M4. In season and at the required rod: each Common ≥ 7 per run, Uncommon ≥ 3.5, Rare ≥ 0.7, special ≥ 0.4 [tune; the 3-day minimums scaled to 7 days]; junk ≤ 20% over the whole run and on every single day, escalation modifiers included; no species above 50% of a run's catches.
- **V5 economy:** expected coins and kg per real minute per sub-area × band × weather, with cycle time = M2's measured median cast-to-land per profile plus walking time. No sub-area best-for-coins in more than 85% of a season's contexts at rod 0. This report also produces the quota calibration inputs (14.4) for the whole 7-day trip: per-day expected sales on the default rod path, plus the infinite days' plateau, against which the ×1.2 growth is checked.

**PlayMode:** solo host in one process: load the lake spot, drive a cast through the resolver to Landed, assert `CatchAnnounced` fires and the journal updates; an off-grid cast is rejected.

**Manual (MPPM):** the client's catch shows the same species, weight and value on both peers; both journals update; hotspot VFX on both; a `dataVersion` bump on one instance is rejected at approval with the version message.

### 12.10 Journal

**Save (`meta.json` v1, first written in M3; R32).** M4 migrates it to v2 (14.10), which adds cosmetic currency, owned cosmetics, unlocked maps, `skillByMap[5]`, best infinite day per map, stats and the pending-run reference. The journal shape below is unchanged in v2.
```csharp
[Serializable] public class MetaSave { public int SchemaVersion = 1; public Dictionary<string, JournalEntry> Journal = new(); }
[Serializable] public class JournalEntry {
    public int Seen;           // hooked fish I witnessed (mine or crew's), landed or lost
    public int CrewLanded;     // landings I witnessed, including mine
    public int Caught;         // landed by me
    public int BestWeightG; public ushort BestLengthMm; public byte BestQuality;   // mine only
    public List<string> SeenInSpots = new();
}
```
- DTOs and migrations live in Core; the Newtonsoft serializer lives in Runtime (`SaveService`) unless it resolves under `noEngineReferences` [verify].
- **Atomic write:** write `meta.json.tmp` via `FileStream` + `Flush(true)`; if `meta.json` exists `File.Replace(tmp, path, path + ".bak")`, else `File.Move(tmp, path)` (not the overwrite overload). **Load order:** main → `.bak` → defaults, logging which was used. Unknown ids are kept and ignored.
- Written after each `CatchAnnounced` that changed the journal and on quit.

**Updates on every peer from `CatchAnnouncedRpc`:** fish (not junk) Landed or Lost → `Seen++`, add the spot; Landed → `CrewLanded++`; Landed by the local client → `Caught++` and bests ("New!" / "Personal best!" popup). Junk is not journaled. Late joiners get only what they witness.

| Level | Unlocks when | Shown |
|---|---|---|
| 0 | always | Black silhouette from `bodyShape` and "???" plus a vague spot hint |
| 1 | `Seen ≥ 1` | Name, spots and sub-areas seen |
| 2 | `CrewLanded ≥ 1` | Seasons, weather and bands as icons, generated from all of the species' rules |
| 3 | `Caught ≥ 3` | Weight range and personal bests |

- Completion counts `Caught ≥ 1` of 25.
- **"Biting now" badge** (level ≥ 2): any of the species' rules in the current spot pass `Passes()` with the replicated `WorldState` values, the local rod tier and a null ledger, ignoring sub-area. Every client can compute it.
- **UI:** read-only grid of 25 cards in id order, filters (spot, biting now), opened from the menu and with J in game. It can stay open while the bobber is out; **a bite closes it automatically**.

### 12.11 Dev tools (M3)

Dev panel (F3 cycles overlay/panel; runtime `Debug.isDebugBuild`; `Keyboard.current`): editable on the host, read-only on clients. Sets season, minute, weather override, run seed, luck, local rod tier, "new day", "spawn hotspot here", dev clock on/off. Clients see a read-only view.

---

## 13. Anti-boredom variety plan

### 13.1 Layers

| # | Layer | Changes per | Set by | Ships |
|---|---|---|---|---|
| 1 | Map (sub-areas, signature fish, climax) | run | **Chosen by the crew** on the Hub `MapBoard` from unlocked maps (R57); not rolled | M1 map; M3 two maps; M6+ three more (5 total) |
| 2 | Season | run | `RunPlanner` | M3 |
| 3 | Weather track | segment | `RunPlanner` (seeded Markov) | M3; M5 hazards |
| 4 | Quota kind (cash / weight) | day | `RunPlanner` | M4 |
| 5a | Data-only daily modifier (8 in M4: Calm, Market Day, Night Owls, Big-Fish Day, Hungry Fish, Junk Tide, Topsy-Turvy, Chonk Day; 14.5) | day | `RunPlanner` draw, applied by `DayDirector`; biased by layer 16 | M4 |
| 5b | Hazard modifier (Storm, Fog, Heatwave) | day | `DayDirector` | M5 |
| 6 | Time-band signature fish | band | `SpawnRule.bands` | M3 |
| 7 | Fight profiles (Steady, Darter, Bulldog, Thrasher, Runner) + weight factor | fish | `FightProfileDef` | M2 |
| 8 | Hotspot | ~1–2 h in-game | `HotspotDirector` | M3 |
| 9 | Market board (price drift + bounty species ×1.5) | day | `RunPlanner` | M4 |
| 10 | Shop stock (rolled subset) and the rod rack | Night Camp | `ShopService` (seeded per camp) | M4 |
| 11 | Timed happenings (Frenzy, Gull Flock, Monster Sighting) | within day | `DayDirector` | M5 |
| 12 | Thieves | within day | `ThiefDirector` | M5 |
| 13 | Physical hauls (condition loss) | fish | carry rules | M5 |
| 14 | Daily request (e.g. "Land a 3 kg+ fish before noon: +60") | day | `DayDirector` | M5 |
| 15 | Permanent map skills (one per beaten map, max 5; most nudge where, when or how a player fishes, 14.8; flat boosts such as Patient Hands don't count as variety under the section 2 rule) and in-run rod tiers bought at Night Camp (they open rod-gated fish mid-week) | run (skills) / day (rods) | `SaveService` (skills) / `ShopService` (rods) | M4 |
| 16 | **Escalation arc** (per-map tier per day: mundane day 1 → absurd → day-7 climax; story beats; infinite days at the top tier with stacked modifiers) | day | `DayDirector` from `SpotDef.escalation` (14.5) | M4 (data-only), M5 (hazards, happenings, boss climax) |

### 13.2 Within-day cadence

`DayDirector` runs a seeded **beat schedule**: **no more than ~240 in-game minutes (~3 real minutes) without a new stimulus.** Beats are data (`DayBeatDef[]` per spot or modifier). Example: 0 dawn fish active; ~180 radio hint about the evening's fish (diegetic teaching); ~360 the modifier pulse (e.g. a guaranteed hotspot); 660 dusk transition and lanterns; ~900 night event (M5 thieves or a monster surfacing); 1140 Last Call. Hotspot spawns and weather changes count as beats. Higher escalation tiers add beats (more happenings, weirder radio lines). Day 7 schedules the climax beat (14.5).

### 13.3 Variety acceptance (headless EditMode simulator, 1,000 seeds)

- No daily modifier repeats within days 1–7 (the M4 pool has 8 modifiers plus the climax event, so a full week can be drawn without replacement). A repeat is allowed only when a day's tier-eligible pool is exhausted, and never on consecutive days. Infinite days never repeat the previous day's modifier set.
- Escalation: mean intensity is non-decreasing over days 1–7, day 1 is always tier Mundane, and day 7 always carries the map's climax event.
- Each day differs from the previous in ≥ 2 of {weather (majority segment), modifier, quota kind, bounty species} in ≥ 95% of runs (M5 adds the daily request).
- Every (spot, season) has ≥ 4 eligible species and ≥ 1 species eligible in exactly one band.
- For every (spot, season), the eligible set differs by ≥ 2 species between Morning and Night.
- The beat-gap rule holds for every generated schedule.

---

## 14. Run loop and economy (M4, outline with formulas)

### 14.1 Structure and scenes

```
Bootstrap -> Menu -> Hub (networked camp scene; session open; crew forms; MapBoard map choice;
                          skill and cosmetic loadout; "Resume saved run" if the host has a snapshot;
                          cosmetics stall from M5)
  host picks a map on the MapBoard (from connected players' unlocked maps)
  host rings the StartBell -> all ready (or host force after 10 s) -> host rolls RunPlan for that map
  -> host loads Spot_<mapId> (LoadSceneMode.Single, check SceneEventProgressStatus.Started)
  -> RunDirector publishes the plan -> Briefing
  -> Day 1 -> LastCast -> Settle(1) -> NightCamp(1) [shop, radio, story, ready-up, auto-save]
  -> Day 2 ... -> Day 6 -> LastCast -> Settle(6) -> NightCamp(6)
  -> Day 7 (climax) -> LastCast -> Settle(7)
       met -> SkillPick (map skill awarded, Won recorded) -> InfiniteVote
                unanimous yes -> NightCamp(7) -> Day 8 -> ... (Settle -> NightCamp each day) until a Settle fails
                otherwise     -> Result (Won) -> host loads Hub
Any failed Settle (days 1-7) -> Result (Lost); a failed infinite Settle -> Result (InfiniteEnded).
Host "Abandon run" -> Result (Aborted before the day-7 win; Won after it while Day is still 7; InfiniteEnded from Day 8).
Host "Save & quit" at a NightCamp -> Ended without Result or payout; snapshot kept -> host loads Hub (or leaves).
Resume: Hub -> host chooses "Resume saved run" -> snapshot loaded, roster restored -> all ready
        -> host loads Spot_<mapId> -> NightCamp(N) of the saved night -> Day N+1
        (or, from the win snapshot taken on entering SkillPick, -> SkillPick -> InfiniteVote; day 7 is never replayed).
```

- **`RunPlanner.Roll(runSeed, mapId, config) → RunPlan`** (Core, deterministic per seed). The map is the crew's Hub choice and is an input, not a roll. Rolled: season, the 35-segment weather track for days 1–7, the escalation tier per day (from `SpotDef.escalation`), one daily modifier per day 1–6 (no repeats, tier-biased) plus the day-7 climax event, `QuotaKind` per day, quota jitter, market drift and bounty species per day, and (M5) the happening schedule and daily request per day. **`RunPlanner.RollInfiniteDay(runSeed, d, prev) → DayPlan`** rolls one infinite day (d ≥ 8) at the preceding Night Camp: weather continuation, top-tier modifier(s), kind, jitter, market and bounty. The roll is deterministic, so when a Weather Eye holder is present (14.8) the host also rolls day N + 2 at Night Camp N for the two-day forecast (the same result it would roll a night later); on Night 6 the second forecast day is shown as "Day 8, if you keep fishing".
- The session survives between runs; only Leave or the host quitting ends it. A host quit at a Night Camp ends the live session but keeps the run snapshot. Resuming restores the run from the snapshot, so the session does not need to survive between evenings.
- Within one sitting the Spot scene is loaded once and never reloaded between days. Night Camp is the same scene at night: the crew gathers at the map's campfire, where the camp shop, radio and fire stand. A resume reloads the map scene once from the snapshot. The rolled `RunPlan` is kept in a host-local session-scope service across the load, then published by the scene-placed `RunDirector` (`SynchronizeTransform = false`). **The `RunPlan` is also serialized into every run snapshot** (with `runSeed` and `ContentHash`), so it survives the scene unload and a resume. Payouts happen at each met Settle and at the run's end (win, loss, abort or infinite end), never on Save & quit.

### 14.2 Run state machine (Core) and clock

```csharp
namespace FishGame.Core.Run {
  public enum RunPhase : byte { Briefing, Day, LastCast, Settle, NightCamp, SkillPick, InfiniteVote, Result, Ended }
  public enum RunOutcome : byte { None, Won, Lost, Aborted, InfiniteEnded }   // InfiniteEnded = won day 7, then an infinite Settle (Day >= 8) failed, or abandoned on Day >= 8
  public enum RunEventKind : byte { BriefingDone, DayClockEnded, EndDayVotePassed, LastCastResolved, SettleShown,
                                    SkillPickDone, InfiniteVotePassed, InfiniteDeclined, NightCampDone, SaveAndQuit, ResultDone, HostAbort }
  public sealed class RunStateMachine {
      public RunPhase Phase { get; } public int Day { get; }  public RunOutcome Outcome { get; }
      public bool WonAtDay7 { get; }                                                    // true from the day-7 win on; infinite days are Day >= 8
      public static RunStateMachine Restore(int day, bool wonAtDay7, RunPhase resumeAt); // resume: resumeAt is NightCamp (any saved night) or SkillPick (day 7, wonAtDay7); anything else throws
      public bool TryApply(RunEventKind e, in RunInputs inputs, out RunTransition t);   // false = illegal, state unchanged
  }
}
```

| From | Event | Condition | To |
|---|---|---|---|
| Briefing | BriefingDone | 10 s, or all ready, or host skip | Day (1) |
| Day | DayClockEnded | minute ≥ 1200 | LastCast |
| Day | EndDayVotePassed | today's quota met and every connected player voted yes (solo: one press) | LastCast |
| LastCast | LastCastResolved | no hooked fish, or 45 s grace | Settle |
| Settle | SettleShown | met, Day < 7, or Day ≥ 8 (infinite) | NightCamp (auto-save, 14.9) |
| Settle | SettleShown | met, Day == 7 | SkillPick (sets `WonAtDay7`; map skill and map unlock awarded now, so they can't be lost; **win snapshot** written on entry, 14.9) |
| Settle | SettleShown | not met, Day ≤ 7 | Result (Lost) |
| Settle | SettleShown | not met, Day ≥ 8 | Result (InfiniteEnded; best day = Day − 1) |
| SkillPick | SkillPickDone | every connected player confirmed a pick, or 60 s [tune] | InfiniteVote |
| InfiniteVote | InfiniteVotePassed | every connected player voted yes (solo: one press) | NightCamp (Day stays 7; auto-save; the next Day is 8) |
| InfiniteVote | InfiniteDeclined | any player voted no, or 30 s [tune] | Result (Won) |
| NightCamp | NightCampDone | all connected ready, or 180 s [tune], or host force after 10 s | Day (+1) (snapshot rewritten first) |
| NightCamp | SaveAndQuit | host chose "Save & quit" | Ended (no Result, no payout; snapshot kept) |
| any except Result/Ended | HostAbort | host chose "Abandon run" | Result: Aborted if not `WonAtDay7`; Won if `WonAtDay7` and Day == 7 (SkillPick, InfiniteVote, NightCamp(7)); InfiniteEnded if Day ≥ 8 |
| Result | ResultDone | all ready or 30 s | Ended |

- Abandon is the only path to Aborted. It deletes the snapshot and pays nothing extra: Shells for met days (and the win bonus, if day 7 was won) were already paid. Save & quit is not an abort.
- **Best infinite day** = the last day whose Settle was met (a failing infinite Settle records Day − 1, so failing day 9 records 8; an abandon records the last met day); a Won run records 7.
- On resume, `RunStateMachine.Restore(day, wonAtDay7, resumeAt)` puts the host in NightCamp for the saved night, so the crew gets its camp (and the same remaining shop stock) again before Day N+1. The one exception is the **win snapshot** (`resumeAt = SkillPick`, day 7, `WonAtDay7`): a crash between the day-7 Settle and NightCamp(7) resumes in SkillPick, so the crew never replays day 7. From there the machine runs SkillPick → InfiniteVote as normal (players who already picked see keep/re-choose; the vote is re-taken).

- Met/missed is computed by `Settle(tally, quota)` inside the machine (`>=` counts as met), never passed in.
- Only the host holds a `RunStateMachine`; clients render `NetPhase`, `NetDay`, `NetOutcome`.
- **LastCast:** no new casts. On entering LastCast the host auto-reels every line still Waiting (refunded cancel, with the owner told through `CommitRpc`). Hooked fights may finish. A fight still running when the 45 s grace ends resolves as Escaped(Timeout) and is announced as Lost. Fish landed during LastCast may still be sold until the phase ends.
- **Joins** (R13): `joinsOpen = phase ∈ {Briefing, NightCamp}` plus the Hub; mirrored to the session lock. A joiner at Night Camp spawns at the campfire.
- **Clock replication:** `WorldState.NetClock = ClockAnchor : INetworkSerializeByMemcpy { double anchorServerTime; float anchorMinute; float minutesPerSecond; byte running; }`, written only on start, stop, pause, reset or rate change. Every peer computes `minute = min(1200, anchorMinute + running × (NetworkManager.ServerTime.Time − anchorServerTime) × minutesPerSecond)` (`NetworkTime.Time` is a `double`, confirmed in 2.13.3 source). No per-tick traffic, no drift, late joiners get it automatically. The host's Core `DayClock` (same function) is authoritative for rolls and day end.
- Rate 1200/900 = 1.333 min/s [tune via `RunConfigDef.dayRealSec`]. The clock runs only in Day; it stops in LastCast, Settle, NightCamp, SkillPick and InfiniteVote and resets to 0 at the next Day ("Day N" fade card for any day, e.g. "Day 4 of 7", "Day 9: Infinite"). Co-op cannot pause a day; solo pause writes rate 0. Last Call at 1140: horn, banner "Last call — boat leaves at 2:00!".
- A test `RunConfigDef` with `dayRealSec = 3` drives the full-run PlayMode test: 7 Settles with 6 Night Camps, a Save & quit and resume, and entry into Infinite.

### 14.3 Money path (threshold model, R7)

```
fish sold -> DayTally (cash, kg) and crew wallet (cash)
Settle(d): met = (kind == Cash ? tally.cash : tally.kg) >= Quota[d]     // quota is checked, not deducted
Night Camp shops spend the crew wallet (bait, lures, gear, rods); the wallet is in-run only and is never converted to meta
Shells (cosmetic meta currency) are paid per met day and at the day-7 win (14.8), independent of the wallet
```

- One shared crew wallet (`NetCrewCash`, host-authoritative). Meeting the quota early is fine; the crew keeps fishing and the surplus stays in the wallet. The wallet is saved in the run snapshot.
- **Starter kit:** every player starts each run with `rod_twig` and `bait_worm ×10` (`RunConfigDef.starterKit`) [tune]; the wallet starts at 0. A new player joining mid-run (not reclaiming a slot) gets the same kit.
- Unsold creel fish carry over to the next day with a freshness penalty (R12), through Night Camp and through a save/resume (they are in the snapshot). Infinite days follow the same rule. Unsold fish at the run's final Settle (Settle(7) followed by a declined vote, or any failing Settle) are worthless.

### 14.4 Quota

```
Quota[d] = RoundNice( base(kind, spot, season) * dayMult[d] * crewFactor[n_d] * jitter[d] )
base(Cash) = SpotDef.baseQuotaCoins[season]     base(Weight) = SpotDef.baseQuotaKg[season]
dayMult    = [0.5, 0.7, 0.9, 1.1, 1.35, 1.6, 2.0]           (d = 1..7) [tune]
dayMult[d] = dayMult[7] * InfiniteGrowth^(d - 7)             (d >= 8; InfiniteGrowth 1.2 [tune], so day 8 = 2.4, day 9 = 2.88, ...)
crewFactor = [1.0, 1.8, 2.5, 3.1]   (n = 1..4)               (unchanged)
jitter[d]  = 1 + (rng(Rng.Derive(runSeed, Quota, d)) * 2 - 1) * J        // J = 0.08
RoundNice  = nearest 5 coins / nearest 1 kg
n_d        = connected players when Day(d) starts
```

- **Monotonicity bound (R55).** `dayMult[d] × jitter[d]` is strictly increasing in d for every seed iff `(1 − J) × dayMult[d+1] > (1 + J) × dayMult[d]` for every d, i.e. **J < (r − 1)/(r + 1)** with r the smallest consecutive ratio. Days 1–7: r = 1.6/1.35 ≈ 1.185 → J < 0.0847. Days 7→8 and later: r = InfiniteGrowth = 1.2 → J < 0.0909. J = 0.08 passes both. **Any retune of `dayMult` or `InfiniteGrowth` must keep every consecutive ratio ≥ 1.174**, or lower J; a `RunConfigDef` validator fails the build otherwise. The guarantee is on the unrounded value. `RoundNice` is monotone, so with the same n and kind a rounded quota never drops, but ties are possible at small quotas. Tie-break: if `Quota[d]` rounds to ≤ `Quota[d−1]` recomputed at the same n and kind, add one rounding step (5 coins / 1 kg).
- **Kind:** Day 1 is always Cash (onboarding). Days 2–7 and every infinite day are Weight with probability `SpotDef.weightDayChance` (0.3 [tune]). Junk counts 0 kg.
- **Disconnect mid-day** (R14): `Q' = Q(nNow) + (Q(nStart) − Q(nNow)) × elapsedFraction`. A rejoiner counts from the next day.
- **All seven quotas and kinds are shown at Briefing** as a quota ladder (computed with the current n), so the crew can plan purchases and hold fish for the late days and the day-7 climax. Infinite quotas are shown one day ahead on the Night Camp radio.
- **Pressure target (acceptance gate):** `pressure[d][n] = Quota[d] / median sales of a competent crew of n on day d`. Targets [tune]:

| Day | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8+ (Infinite) |
|---|---|---|---|---|---|---|---|---|
| Pressure | 0.50 | 0.55 | 0.60 | 0.65 | 0.70 | 0.75 | 0.85 | rises ~×1.2/day over a sales plateau (day 8 ≈ 1.0, day 9 ≈ 1.2, day 10 ≈ 1.5); a competent crew's median run ends on day 8–9, a crew selling ~20% above median reaches day 9–10, and day 11+ needs ~40% above median |

  For any day, pressure across n = 1–4 must be within ±10% of solo. Rods are bought per player while per-head income at n = 4 is ~3.1/4 ≈ 78% of solo, so a fixed rod price would make the default rod path (the source of the 7-day sales growth below) proportionally dearer in a crew. **Design call (`CrewScaling` rod rate):** per-player rod prices scale by `crewFactor[n] / n` with n = players connected at that Night Camp: `rodPrice[n] = RoundNice(basePrice × crewFactor[n] / n)` → ×1.0 / ×0.90 / ×0.83 / ×0.78 for n = 1..4, so the full Twig → Deep-sea path costs 1,020 solo, ~920 duo, ~850 trio, ~790 quad per player and stays the same share of per-head income at every crew size. Bait, lures and gear keep flat prices (bait spend is per cast, so it already scales with crew catch rate). The V4/V5 reports still check, per n, that the default rod path is affordable after bait spend without breaking the ±10% rule; if the rate alone doesn't hold it, retune `crewFactor` or the base rod prices [tune], not the rate rule. `crewFactor` is sub-linear because coordination overhead grows while per-rod catch rate is near linear.
- **How the targets fit `dayMult`.** With base calibrated to day-4 pressure 0.65 (`dayMult[4]` = 1.1), the curve meets the table only if a competent crew's median sales, relative to day 4, are ≈ 0.59 / 0.75 / 0.89 / 1.00 / 1.14 / 1.26 / 1.39 on days 1–7. That is ~2.35× growth from day 1 to day 7, coming from rod upgrades (Twig → Deep-sea opens rod-gated fish), better bait and lures, the Cooler, held fish, the bounty and skills. With flat daily sales, day-1 pressure would be ≈ 0.30 and day-7 ≈ 1.18, so a crew that never upgrades fails late in the week by design. In Infinite, sales plateau near the day-7 level, so day-8 pressure ≈ 1.0 for a competent (median) crew. Infinite is meant to end.
- If `runlog.csv` shows flatter (or steeper) sales growth, retune `dayMult` (not `base`) to hit the targets, keeping the monotonicity bound.
- **Calibration rule:** set `baseQuotaCoins[season]` so day-4 solo pressure is 0.65, using the 7-day V4/V5 economy reports (12.9) on the default in-run rod path and M2 measured cycle times. Note the index shift: the old rule calibrated at day 2 (`dayMult` 1.0); now it is day 4 (1.1). Starting placeholders are `lake_dock` 150 coins / 10 kg and `harbour_pier` 170 coins / 12 kg in every season [tune]. Recheck them against the 7-day report: `harbour_pier` is now played only after a `lake_dock` win, so its crews carry one skill each. Calibrate both maps on skill-less crews; skills are meant to make later maps a little easier. (The drafts' 500–800 coins didn't match the roster's coin values.)
- An analytic EditMode test of pressure would be circular (crewFactor *is* the efficiency term); EditMode tests check plumbing, determinism and the monotonicity bound. Real tuning comes from **`runlog.csv`**: the host appends one line per Settle (crew size, day, kind, quota, sales cash, sales kg, catches, snaps, modifier, escalation tier, rods owned, skills present) to `Application.persistentDataPath/runlog.csv`.
- **Player-count levers** (`CrewScaling`, Core):

| Lever | Solo | Duo | Trio | Quad |
|---|---|---|---|---|
| Quota | ×1.0 | ×1.8 | ×2.5 | ×3.1 |
| Thief spawn interval (M5) | 120 s | 89 s | 71 s | 59 s |
| Max concurrent thieves (M5) | 1 | 1 | 2 | 2 |
| Monster stamina (M5) | solo profile (×0.5) | ×1.0 | ×1.2 | ×1.35 |
| Event intensity (gusts, gull flock, leak chance) (M5–M6) | ×0.8 | ×1.0 | ×1.0 | ×1.15 |
| Bite rate / spawn density | ×1.0 | ×1.0 | ×1.0 | ×1.0 (never scaled; solo must not be starved) |
| Rod price per player (`crewFactor[n] / n`, 14.4) | ×1.0 | ×0.90 | ×0.83 | ×0.78 |

Nothing may put a solo player behind an impossible gate. **Map skills never change any `CrewScaling` lever or quota (R58):** a crew with skills faces the same quota as one without, and skills are never scaled by crew size.

### 14.5 Market board, daily modifiers and the escalation arc (M4 variety)

- **Market board** (Briefing and each dawn): per-species price drift `market = 1 + U(−0.15, +0.15)` per day (Market stream) and one **bounty species** at ×1.5, drawn from fish eligible today at this spot, season and weather. It pushes the crew toward a particular band or sub-area each day.
- **Daily modifiers (data-only, `EventDef` kind Daily; 8 in M4 so a week can be drawn without repeats).** Allowed tiers are in brackets [tune]:

| Modifier | Effect | Tiers | Radio line (tone) |
|---|---|---|---|
| `calm` | none | Mundane | "Lovely weather for it." |
| `market_day` | one rolled tag sells ×2, others ×0.9, shown on the price board | Mundane–Weird | "The fishmonger is in a buying mood." |
| `night_owls` | Nocturnal weight ×3 | Mundane–Weird | "Something's keeping the fish up at night." |
| `big_fish_day` | Luck +0.75 (heavier rolls) | Odd–Unhinged | "Big ones about. Hold onto your rods." |
| `hungry_fish` | bite time ×0.75, line strength ×0.9 | Odd–Unhinged | "The fish skipped breakfast." |
| `junk_tide` | junk ×2, sell price ×1.2 (the town pays for the clean-up) | Weird–Unhinged | "Someone emptied a whole garage into the lake." |
| `topsy_turvy` | Nocturnal ×3 and Surface ×3, Bottom ×0.5 | Weird–Unhinged | "The fish have forgotten what time it is." |
| `chonk_day` | Luck +1.0, line strength ×0.85 | Unhinged | "Everything down there has been eating well. Too well." |

  The modifier draw belongs to `RunPlanner` (14.1): `RunPlanner.Roll` draws the week at Briefing (and `RollInfiniteDay` each infinite day), the draws are stored in the `RunPlan` and the snapshot, and `DayDirector` only applies the day's entry. A resume therefore replays the same modifiers. The draw takes one modifier per day for days 1–6 **without replacement**, weighted by `EventDef.weight × tierWeight[tier]` among those allowed at the day's tier. If a tier's pool runs dry it may repeat, but never on consecutive days. **Day 7's slot is the map's climax event** (`SpotDef.climaxEvent`). The player's first-ever run forces day 1 to `calm` (meta flag). `NetDailyModifier` drives the banner; effects apply on the host through `FishingContext.Events` and the sale formula.
- **Escalation arc (R54, data-driven per map).** `SpotDef.escalation[7]` names one `EscalationTierDef` per trip day. Default for both maps [tune]: Mundane, Odd, Odd, Weird, Weird, Unhinged, Climax.
  - **`EscalationTierDef`:** `id`, `tier` (0 Mundane … 4 Climax), `intensity`, `tierWeightKey` (picks the `EventDef.tierWeight` column), `stackedModifiers` (1; 2 in late Infinite), `happeningsMin/Max` (M5), `storyKeys[]` (Night Camp and morning radio lines), `musicStinger`.
  - **Intensity** scales every modifier multiplier toward its extreme: `m' = 1 + (m − 1) × intensity`, with intensity Mundane 0.8, Odd 1.0, Weird 1.15, Unhinged 1.3, Climax 1.5 [tune]. **Fairness caps** apply to the combined product per multiplier class: bite time [0.5, 1.5], sell price [0.6, 2.0], line strength ≥ 0.75, Luck [−1, 1]. Escalation therefore can't make a quota unwinnable, and the V4/V5 reports run with escalation on.
  - **Story beats:** each Night Camp the `CampRadio` plays one line from the next day's tier (`storyKeys`) and the map's `campStoryKeys`, so the week reads as a story. Examples for `lake_dock`, as comedic beats in the string table: night 1 "The bait man says the worms are 'acting funny'. He won't say how." Night 3 "The ducks are flying in formation. Spelling something." Night 5 "Someone's tent is gone. Not blown away. Gone." Night 6 "The lake is humming. Only the fish seem to mind." Morning 7 "Tonight, he surfaces."
  - **Day-7 climax (M4, data-only):** `lake_dock` → `legend_night` ("The Legend of Old Whiskers"; rare chance ×3, Luck +0.75, bounty ×2). `harbour_pier` → `leviathan_tide` (same shape, with the harbour's specials). From M5 the climax also guarantees the boss Monster Sighting (15.3, 15.4).
  - **Infinite (R60):** days 8+ use the top non-climax tier (Unhinged) with intensity +0.05 per infinite day, capped at 1.6 [tune]. Modifiers are drawn from the Unhinged-eligible pool (5 data-only modifiers in M4, plus the M5 hazards) with no repeat of the previous day's set. From day 10 two modifiers stack [tune]. Every third infinite day (days 10, 13, 16, … [tune]) re-runs the map's climax event ("He's back. He's angrier."). The fairness caps still hold.

### 14.6 Selling and freshness

- **M4 selling (creel):** E on the `SellBin` opens the creel panel ("Sell all" or select fish); `SellRequestRpc(ulong creelMask)`. The host checks range (bin `Range + 1 m`) and phase (Day or LastCast; the lid is shut otherwise), removes the entries, adds value and kg to `DayTally` and the wallet, credits the catcher's slot in the contribution ledger, and broadcasts `SoldRpc(ushort itemIndex, int value, byte catcherSlot, Vector3 pos)` for the coin pop.
- **Sale price** (host only, at sale time):
```
price     = round(rolledValue * Freshness * condition * market[item, day] * eventPriceMult * skillPriceMult[catcher])   // Silver Tongue 1.05, else 1 (14.8)
runMinute = (day - 1) * 1200 + minute
age       = runMinuteNow - caughtAtRunMinute + overnightsHeld * 150
Freshness = max(0.40, 1 - decayPerMin * age * storageMult)        // decayPerMin 0.0004 [tune]
```
  `condition` is 1 until M5. `storageMult` = 1 in the creel, 0.25 with a Cooler. Heatwave (M5) multiplies `decayPerMin` by 2.5. A fish held a whole day loses ~48%; the 0.40 floor means a fish is never worthless.
- **`RunLedger` (Core, host):** per-day tallies by kind, the wallet, the per-slot contribution ledger and the payout calculation (14.8). Its totals are replicated on `RunDirector`.
- **Ledgers (replicated on `RunDirector`):** `NetDayCash`, `NetDayKg`, `NetCrewCash`, `NetQuotas` (7 ints + 7 kinds for days 1–7, ~35 bytes, memcpy struct), `NetInfiniteQuota` (today's and tomorrow's infinite quota and kind, from day 8), `NetworkList<PlayerRunStat>` (`{ byte slot; int cashCaught; short fishCount; int kgG; short snaps; int shellsEarned; }`; `shellsEarned` is display only, since Shells are paid as they are earned, 14.9).

### 14.7 Night Camp shop (6 per trip after days 1–6, plus one before each infinite day, from night 7 on)

- `ShopService` (Core logic, host-run by `RunDirector`) rolls stock, prices and validates purchases.
- Stock rolled per Night Camp from the NightCamp stream (`Rng.Derive(runSeed, StreamId.NightCamp, day)`): 6 slots (3 bait, 2 lure, 1 gear) [tune], replicated as `NetworkList<ShopSlot>` (`{ ushort itemIndex; byte stock; int price; }`). A separate fixed **rod rack** is not rolled: each player can buy their next rod tier once it is stocked (9.7 `firstCamp`). Stock, `timesBoughtThisRun` and rods owned are in the run snapshot.
- Price `round(basePrice × (1 + 0.12 × timesBoughtThisRun[item]))`. Rods never inflate; their per-tier base price is scaled by the `CrewScaling` rod rate `crewFactor[n] / n` (14.4) with n = players connected at that Night Camp, shown on the rack as the "crew rate".
- `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)] BuyRequestRpc(byte slot, byte qty, uint requestId, RpcParams p = default)` on `RunDirector`. The host validates sender, phase NightCamp, `qty ≥ 1`, stock, wallet, and pouch room (4 bait stacks per player, 5 with Deep Pockets), serialises concurrent buys first-come-first-served, and replies `BuyResultRpc(requestId, result)` to the sender only. Items go to the buyer. Purchases spend only the in-run wallet and never touch Shells, so the crew conversation is about who gets the next rod and how much to keep spare.

| Class | Items [tune prices] | Effect | Lifetime |
|---|---|---|---|
| Bait | Worm ×5 (15), Grub ×5 (25), Minnow ×4 (20), Shrimp ×3 (30), Raw meat ×2 (40) | Rating, bite speed, junk mult (9.7) | per cast |
| Lure | Shiny Spoon (Predator ×2), Glow Jig (Nocturnal ×2), Bottom Bouncer (Bottom ×2) — 60–90 | Affinity weight bonus; never consumed; **lost if the line snaps** | run |
| Rods (rack, per player) | Handmade (120, from camp 1), Carbon (300, from camp 2), Deep-sea (600, from camp 4); solo base prices, × crew rate (14.4) | Replaces the buyer's rod for the rest of the run (9.7); opens rod-gated fish | run |
| Gear | Cooler (+6 slots, +10 kg, storage ×0.25, 80); Fish Finder (shows hooked fish stamina, 70) | | run |

There are no perks: their effects moved into the map skill pool (14.8). The line gauge is a free setting (R36). Cosmetics are never run stock.

### 14.8 Meta progression: map skills, map unlocks, cosmetics

**Two permanent tracks, per player, in each player's own save:** map skills (the only permanent power, R58) and cosmetics bought with Shells (R59). Rods, bait and gear are in-run only (14.7).

**Shells (cosmetic currency) [tune]:**
```
at each met Settle, days 1-7:    every connected player  += DayShells       // 10
at each met infinite Settle:     every connected player  += InfiniteShells  // 15 per infinite day survived
at the day-7 win (SkillPick):    every connected player  += WinShells       // 40
                                 + FirstClearShells the first time this player beats this map   // 60
```
- Flat per player, never split and never converted from the wallet (proposed default; Q2 re-scoped). A lost or abandoned run keeps every Shell already paid, so failure still pays, and a win is worth more. Contribution (`cashCaught`) is bragging rights only.
- Rough pace: a full solo or co-op win pays 70 + 40 = 110 (+60 on a first clear). Cosmetics cost 50–200 [tune], so a win buys about one item.
- **Cosmetics** (`CosmeticDef`: `id`, `slot` (Hat, RodSkin, BobberSkin, Emote, OutfitColour), `price`, `meshRecipe`/`palette` (procedural, no imported assets)): hats, rod skins, bobber skins, emotes and outfit colours. Slot colours stay fixed so players stay identifiable. Starter-owned: `hat_bucket`, the default rod and bobber. M4 earns, saves and equips Shells and cosmetics. The **cosmetics store** (`CosmeticsStall` in the Hub) and emotes (with an Emote binding added to 8.3) arrive in M5 [proposed].

**Maps and unlocks (R57):**
- `unlockedMaps` starts as `[lake_dock]`. Beating map N (its day-7 Settle) adds map N (if a friend brought you) and map N+1 for every player in the roster at that Settle, delivered with the skill award (immediately when connected, as a pending clear otherwise). No currency is involved.
- **Map board:** the Hub `MapBoard` offers the union of the connected players' `unlockedMaps` [proposed default], so a friend can bring you along. The host makes the pick. Winning a map you were brought to still gives you its skill and unlocks its successor.
- Five maps in total: `lake_dock` (1, M1), `harbour_pier` (2, M3), three more (M6+).

**Map skills (R58):**
- `skillByMap[5]`, indexed by `SpotDef.mapOrder`: `null` = map not yet beaten, `""` = beaten but not picked, else a skill id. Max 5 skills; a skill may sit in only one of a player's slots.
- **Award:** at the day-7 Settle (met), every slot of the run's roster at that Settle earns the map's slot (R58). A **connected** player is awarded immediately: `MapClearedRpc(runId, mapId)` → each peer saves, before Infinite starts, so it can't be lost. A **disconnected** roster slot (still reclaimable by persistent id, 6.6) can't be written to, because each save is only ever written by its own machine, so the host records a **pending clear** `(runId, mapId, persistentPlayerId)` in the session's `pendingMapClears` list and in the run snapshot. It is delivered as the same `MapClearedRpc` the moment that player's machine reconnects to the session (a later Night Camp in Infinite, or the Hub after Result while the session lives) or reclaims its slot on a resume, and the peer's `wonRunIds` check makes delivery idempotent. A pending clear is dropped when the session ends and the snapshot is deleted (Result recorded or "Abandon saved run"); a player who never reconnects before then earns the slot by beating the map again. A pending-clear recipient picks at the Hub `SkillBoard` (the `""` state), since SkillPick has passed.
- **Pick:** on the `SkillBoard` during SkillPick, a free choice from the full pool (no duplicates across the player's slots). A player who hasn't picked by the 60 s timeout keeps `""` and picks later at the Hub `SkillBoard`. On a re-win of the same map, the player keeps or re-chooses that map's slot. Slots are otherwise fixed.
- **Applying skills:** on every join and rejoin (Hub, Briefing or Night Camp) each client sends `LoadoutRpc(SkillLoadout skills, CosmeticLoadout cosmetics, byte unlockedMapsMask)` (Owner permission on `PlayerActions`; the mask feeds the `MapBoard` union). `SkillLoadout` = `ushort[5]` skill indices (0xFFFF empty); `CosmeticLoadout` = hat, rod skin, bobber skin, outfit colour. The host checks the ids exist, that there are no duplicates and at most 5, and trusts ownership (friends-only game). It builds the player's Core `SkillMods`, used in that player's rolls and in host validation (9.9 cast distance, 12.4 `FishingContext.Skills`). The owner applies the fight-side terms in `FightSim`. A pick made during SkillPick is re-sent at once, so it applies in Infinite.
- **Crew scaling:** skills never touch `CrewScaling` or quotas (14.4), and their values never scale with crew size. Each skill affects only its owner (their casts, fights, catches, sales, items and body), except Weather Eye's crew-wide forecast.

**Skill pool (12, `SkillDef`: `id`, `displayName`, `description`, `mods` (Core `SkillMods`), `availableFrom`; values [tune], each a modest bonus):**

| Skill | Effect (owner only unless stated) | Hooks into | From |
|---|---|---|---|
| Patient Hands | bite wait ×0.9 | 9.4 `skillMult` | M4 |
| Night Owl | bite wait ×0.85 during Dusk and Night bands | 9.4 `skillMult`, night fishing | M4 |
| Iron Grip | line strength ×1.1 | `FightSim` (owner), monster `lineStrength` | M4 |
| Steady Nerves | snap grace +0.15 s | `FightSim` snap timer | M4 |
| Long Arm | `castMaxDist` +2 m (and `maxLineOut`) | 9.3 cast; host validity (9.9) | M4 |
| Lucky Lure | Luck +0.15 (heavier fish, more Trophies) | 9.8 weight skew | M4 |
| Rare Whisperer | Uncommon and Rare rule weights ×1.15 | 12.4 pool step 3 | M4 |
| Silver Tongue | fish this player caught sell ×1.05 | 14.6 sale price (by catcher slot) | M4 |
| Deep Pockets | +1 bait pouch stack; creel +2 entries and +2 kg (M5: pocket +1 slot) | pouch, creel, carry | M4 |
| Weather Eye | the Night Camp radio forecasts two days ahead for the whole crew; own bite wait ×0.9 in Rain, Fog and Storm segments | forecast, weather | M4 |
| Gull Glare | thieves score this player's held items ×0.5, and telegraph +1 s near them | 15.2 thieves | M5 |
| Sure Footing | shove and gust knockback on this player ×0.6; three shoves within 2 s are needed to Down them | 15.5 shove resistance | M5 |

Combined skill bite-wait terms are floored at ×0.8. Skills listed "From M5" are in the pool data from M4 but can't be picked until their system ships.

### 14.9 Payout delivery, nightly snapshot and resume (R56)

**Payouts (Shells):**
- At each met Settle the host sends `[Rpc(SendTo.ClientsAndHost)] ShellsPaidRpc(runId, day, amount)`. Each connected peer adds `amount` once, keyed by `(runId, day)` in `meta.json` `paidRunDays` (a map `runId → days paid`, keeping the 8 most recent runs). A day replayed after a crash and resume can't pay twice, even if the player joined other runs between sittings.
- At the day-7 win the host sends `MapClearedRpc(runId, mapId)`. Each peer records the skill slot, the map unlock, `stats.runsWon`, `WinShells` and (first clear) `FirstClearShells`, then saves. It applies once per `runId` (`wonRunIds` in `meta.json`, the 8 most recent). The host writes a **win snapshot** on entering SkillPick (`phase = SkillPick`, `day = 7`, `wonAtDay7 = true`), so a crash or host loss between the day-7 Settle and NightCamp(7) resumes in SkillPick and never replays day 7; `wonRunIds` still makes the re-sent `MapClearedRpc` idempotent (no second `WinShells` or `runsWon`), and a player who already picked sees keep/re-choose. Disconnected roster slots' awards ride along as `pendingMapClears` (14.8). Only a crash **during** day 7 itself replays it, from NightCamp(6), like any other day.
- At Result (Won, Lost, Aborted or InfiniteEnded) the host sends `RunEndedRpc(runId, outcome, dayReached)`. Each peer updates `stats` and `bestInfiniteDayByMap[mapOrder]` (the last day whose Settle was met, 14.2; kept if higher), clears `pendingRun` and saves. The host deletes `run_snapshot.json`. This happens before the host loads Hub. Save & quit sends none of these. It sends only `RunPausedRpc(runId, night)`, on which each peer writes its informational `pendingRun` reference.
- A client's save is only ever written by its own machine.

**Nightly run snapshot:**
- `RunSnapshotService` (host only) writes `Application.persistentDataPath/run_snapshot.json` (per-instance suffix like `meta_p2.json`) with the same atomic writer and `.bak` fallback as `meta.json`. It writes **on entering NightCamp** (after the pre-snapshot sweep of any straggling `ActiveCast`, 9.13), **at NightCampDone** (so camp purchases are kept), **on entering SkillPick** (the win snapshot, so day 7 is never replayed) and **on Save & quit**. It runs in Infinite too. Writing it never blocks play; a failed write shows a host toast and retries at the next trigger.
- **`RunSnapshot` (Core DTO, `snapshotVersion` 1):** `runId`, `ContentHash`, `ProtocolVersion`, `mapId`, `runSeed`, `RunConfigDef` id, the serialized `RunPlan` (and infinite `DayPlan`s so far), `day`, `wonAtDay7`, `phase` (`NightCamp` or `SkillPick`, the `resumeAt` for `Restore`), `pendingMapClears` (14.8), `resumeCount`, `hostCastCounter`, quotas so far, wallet, `RunLedger` (tallies, contribution ledger), catch ledger (`catchLimitPerRun` counts), shop stock and `timesBoughtThisRun`. Per slot: `NetPlayerId`, name, slot index, creel entries (with `caughtAtRunMinute`), bait pouch, lures, rod tier, gear (Cooler, Fish Finder), run stats. From M5 also the carried-pocket contents. Not stored: active casts (none exist), loose items (resolved at day end, 15.1), thieves, happenings in progress.
- **Each client's `meta.json` `pendingRun`** (`runId`, `mapId`, `night`, host display name, `savedAtUtc`, `isHost`) is **informational only**: it shows "Your crew has a saved run" in the menu. Only the host's machine holds the snapshot and can resume it. A client never banks or resumes anything on its own.

**Resume:**
1. The host (the same machine that saved) opens a session as usual; the Hub `MapBoard` shows "Resume: Lake Dock, Night 3" (or "Resume: Lake Dock, Day 7 won" for a win snapshot).
2. On "Resume saved run" the host loads the snapshot. If `ContentHash` or `ProtocolVersion` differ it refuses ("Saved with a different game version"). It calls `SessionState.RestoreRoster` (6.2), so every saved slot except the host's waits as disconnected.
3. Friends join with the new code; approval (6.6 rule 8) reclaims their slot by persistent player id, with creel, pouch, rod and ledger rows from the snapshot. The host can hand an unclaimed slot to a joiner whose id changed (V20). New players take free slots with the starter kit.
4. When everyone present is ready (or the host forces it), the host loads `Spot_<mapId>`, `RunStateMachine.Restore(day, wonAtDay7, phase)` opens NightCamp of the saved night (or SkillPick from a win snapshot), delivers any `pendingMapClears` to players who reclaimed their slots, and play continues. Missing players can still join at any later Night Camp. Today's quota uses the crew connected at the next Day start, as always.
5. Crash or host loss mid-day: the same steps. The day in progress is lost and replays from its Night Camp with the same plan rolls and fresh catch seeds (9.8).
- "Abandon saved run" in the Hub is HostAbort on the snapshot: Result (Aborted, Won or InfiniteEnded by the 14.2 rule) is recorded for the connected crew, and the snapshot is deleted.

### 14.10 Save v2 (`meta.json`)

```json
{
  "schemaVersion": 2,
  "shells": 120,
  "ownedCosmetics": ["hat_bucket", "rodskin_default", "bobber_default", "hat_traffic_cone"],
  "cosmeticLoadout": { "hat": "hat_traffic_cone", "rodSkin": "rodskin_default", "bobberSkin": "bobber_default", "outfitColour": 0 },
  "unlockedMaps": ["lake_dock", "harbour_pier"],
  "skillByMap": ["skill_patient_hands", null, null, null, null],
  "bestInfiniteDayByMap": [8, 0, 0, 0, 0],
  "paidRunDays": { "6f2c...": [1, 2, 3], "a91d...": [1, 2, 3, 4, 5, 6, 7, 8] },
  "wonRunIds": ["a91d..."],
  "pendingRun": { "runId": "6f2c...", "mapId": "harbour_pier", "night": 3, "host": "Fisher", "savedAtUtc": "2026-10-12T21:40:00Z", "isHost": true },
  "stats": { "runsStarted": 4, "runsWon": 1, "mapsCleared": ["lake_dock"], "fishSold": 73, "bestRunCash": 640, "firstRunDone": true },
  "journal": { "lake_pike": { "seen": 4, "crewLanded": 2, "caught": 1, "bestWeightG": 4200, "bestLengthMm": 645, "bestQuality": 1, "seenInSpots": ["lake_dock"] } }
}
```

`MetaSaveV2` DTO and `Migrate(JObject, fromVersion)` in Core. v1 → v2 keeps the journal, sets `unlockedMaps = [lake_dock]`, `skillByMap` all `null`, `shells` 0, and the starter cosmetics. v2 is redefined by R56–R60 before it ever shipped, so there is no older v2 shape to migrate. `skillByMap` is indexed by `SpotDef.mapOrder` (`null` not beaten, `""` beaten but unpicked). No rods, perks or currency-bought maps exist in the save. The run itself lives in the host's `run_snapshot.json` (14.9); `pendingRun` only points at it (`isHost` marks the machine that can resume).

### 14.11 M4 tests

- **EditMode:** every legal `RunStateMachine` transition and rejection of all others; fail at each Settle 1–7 and at an infinite Settle; vote unavailable before the quota and passing only when all connected vote yes; SkillPick timeout; Infinite vote passes only when unanimous and declines on any no or the timeout; Save & quit only from NightCamp and never produces a Result; `Restore(day, wonAtDay7, resumeAt)` starts in NightCamp, or in SkillPick only for day 7 with `wonAtDay7`, and rejects any other phase; disconnect mid-day; all clients leaving (host continues as solo with `crewFactor[1]` from the next day); host abort. `QuotaCalculator` deterministic per seed. **Monotonicity:** `dayMult[d] × jitter[d]` strictly increasing in d over days 1–12 for 10,000 seeds (raw quotas of different kinds, coins vs kg, aren't comparable), plus a `RunConfigDef` validator asserting every consecutive `dayMult` ratio and `InfiniteGrowth` exceed `(1 + J)/(1 − J)`. Infinite growth formula (day 8 = 2.4, day 9 = 2.88); the rounded tie-break; each quota increasing in n; disconnect recompute endpoints and monotonicity; RNG substream independence (catch stream salted by `resumeCount`); `DayClock` never exceeds 1200, never wraps, doesn't advance when stopped; freshness monotonic and clamped at 0.40; settlement never makes the wallet or stock negative. `DayDirector`: no repeats over days 1–6, day 7 is the climax, intensity caps hold, infinite days never repeat the previous set. Shells formulas (flat per player, per met day, win and first-clear bonuses, `(runId, day)` paid once, the win applied once per `runId` even when day 7 is replayed after a resume); skill rules (award only at a met day-7 Settle, one per map, no duplicates across slots, re-win keep/re-choose, `SkillMods` neutral when empty, no effect on `CrewScaling`). Snapshot round-trip (every field in 14.9 restores equal, including `phase` and `pendingMapClears`), refusal on a `ContentHash` mismatch, and `.bak` fallback. Pending clears: a disconnected roster slot at a met day-7 Settle is recorded, delivered once on reconnect or slot reclaim, and dropped at Result. Save round-trips, v1 → v2 migration fixture, `.bak` fallback.
- **PlayMode (solo, offline, `dayRealSec = 3`, debug grant cash):** a full 7-day run reaches SkillPick then Won (vote declined) with phases matching the machine and a snapshot written at each of the 6 Night Camps plus the win snapshot on entering SkillPick; killing the run in SkillPick and resuming reopens SkillPick with day 7 not replayed and no second `WinShells`. A second run votes Infinite, survives day 8 and fails day 9 → InfiniteEnded with best day 8 recorded. A forced day-2 miss gives Lost with no Night Camp, and the day-1 Shells stay paid. Save & quit at Night Camp 3 then resume: the same wallet, creel, rods and shop stock, day 4 with the same quota and modifier. Selling happens only in Day/LastCast and never twice; a fish hooked at day end keeps LastCast open until landed or the grace ends.
- **MPPM (2–4, manual):** all clients agree on cash, wallet and clock (within 1 game minute) within 0.5 s of a sale or day start; a purchase updates stock and wallet for everyone. A join during Day is rejected with the reason text, a join during Night Camp succeeds, and a rejoin gets the old slot. The host does Save & quit at a Night Camp, everyone quits the game, then the host relaunches and resumes: each client reclaims its slot by persistent id with its creel and rod. Killing the host mid-day, then resuming, replays that day from its Night Camp with no double Shells. A client that dropped during day 7 and reconnects after the win (in Infinite or in the Hub) receives its pending map clear and picks at the Hub `SkillBoard`. A host abort records the result on every client; a mid-day drop lowers the displayed quota per the rule.
- **Feel:** a 2-player crew of average skill meets day 1 with time to spare; days 5–7 need rod upgrades, held fish or the bounty; day 7 feels like a finale; ~105 min of fishing per trip (~2 h with camps), comfortably split across two evenings. Day length (15 min) and trip length (7 days) are fixed by R53. If days drag, add escalation beats or tighten the beat schedule (13.2); change neither length.

---

## 15. Friend slop (M5, outline with the rules later work depends on)

**Exit beat list:** a 4-player crew completes a 7-day run whose chaos follows the escalation arc (calm day 1, hazards and happenings growing through the week) and ends in the map's day-7 climax with the boss Monster Sighting and a boss landing. The run includes at least one theft, one intercepted theft, one monster landing, a daily modifier every day, timed happenings every day at the tier's count (15.4: 1 on the Mundane day 1, 2–3 on days 6–7), a shove into Downed, a slap revive and one seagull carry (debug menu allowed where rolls are rare). A solo run completes with no step needing a second player, including a monster landing.

**Debug menu (host, editor/dev builds):** force a daily modifier, force a happening, spawn a thief, force the next bite to be a monster, set the clock.

### 15.1 Physical fish (carry, throw, drop, sell)

- **When a fish is physical** (default, Q3): landed fish of `carryThresholdKg` (5 kg [tune]) or more, and all monster fish, spawn as physical items; smaller fish keep going into the creel, which shrinks to a 6-slot pocket. The sell bin accepts both (thrown items by trigger, the pocket by interaction).
- **Prefab `CarryItem`** (one prefab for every fish and junk; visuals built procedurally from `itemIndex`): root `NetworkObject` (server-spawned, never re-owned), `NetworkTransform` (Server authority, reliable deltas), `Rigidbody` + `NetworkRigidbody` (`UseRigidBodyForMotion`, `AutoUpdateKinematicState`), collider on layer `Fish`; child `Held` with `AttachableBehaviour` (default `AutoDetach`; its `[Flags]` values are 0–3, so don't author combinations) and the meshes. `FishItem : NetworkBehaviour` on the root with server-write `NetItemIndex`, `NetWeightG`, `NetCondition` (0..1), `NetCaughtAtRunMinute`, `NetCatcherSlot`. Mass `clamp(kg, 0.2, 6)`.
- **How `AttachableBehaviour` works (NGO 2.13.3 source):** `Attach(node)` parents only the child holding the behaviour; the root, Rigidbody and collider stay put; `Detach()` snaps the child back under the root; both require authority (the server here). Therefore: on attach every instance disables the root collider and the server sets the root kinematic; **while attached, the server moves the kinematic root to the holder node's world pose every FixedUpdate**, so a detach (throw, drop, steal, disconnect) leaves the item at the holder, not at the pickup spot.
- **Hand slot:** one per player (rod, item or M6 bucket). Picking up stows the rod. **Starting a cast while holding an item sets it down gently at your feet**, which keeps fishing flowing and creates natural "runner" roles.
- **Pickup:** E → `InteractRpc` → `FishPickup.ServerInteract`: within range + slack, not attached, not Heavy, requester's hand empty, requester Upright and not swimming; first request wins. Server writes `NetCarried` and attaches to `HoldNode`.
- **Throw:** hold Primary to charge up to 1.0 s; `ThrowRpc` → server sets the root to the hold pose, detaches, sets it non-kinematic, `SetLinearVelocity(ThrowMath.ThrowVelocity(...))` (`ChargeToSpeed` 4..14 m/s, + 0.5 × carrier velocity capped at 7 m/s, +12° upward bias). The thrower sees the flight ~1 RTT late, masked by the instant arm swing and whoosh. No local ghost fish unless the 150 ms playtest says throws feel bad (fallback: owner-authority hand-off).
- **Value:** impact damage in the server's `OnCollisionEnter` (clients are kinematic) above 4 m/s: `condition −= min(0.25, (v − 4) × 0.05)`; a gull peck −0.1. Sale price uses the 14.6 formula with `condition`; the weight quota counts kg sold regardless of condition.
- **Water:** an item below `HeightAt − 0.1` floats and flops with a splash cue for 4 s, recoverable from the dock edge or by a swimmer; then the fish swims off (despawned, `lostToWater`); junk sinks.
- **Day end:** loose items on the ground at the end-of-day bell are taken by gulls (`spoiled`); creel pockets and Coolers carry over. A held item at Settle is moved into the holder's pocket if it fits, otherwise it is spoiled, so no loose or held physical item exists when the Night Camp snapshot is written. Pocket and Cooler contents are in the snapshot (14.9).
- **Cap:** 40 loose items; the oldest junk despawns first.
- **Sell bin:** a server trigger; any unattached item entering it sells immediately (trick shots welcome), during Day and LastCast; otherwise the lid bounces it off. `SoldRpc` for coins and sound. Anyone may sell any fish; value credit goes to the catcher.
- **Disconnect:** the server moves the root to the leaver's last hold pose, detaches and clears `NetCarried` in its disconnect handler before the player despawns.
- **`CrewLedger` (Core):** counts spawned, sold, stolen, lostToWater, spoiled per item kind, plus fun stats (shoves given, times downed, times carried by a gull). **Conservation test:** at day end every spawned item is in exactly one bucket.
- **Acceptance:** resting thrown items match the server within 0.05 m on every client; no snap-back toward the pickup spot on throw/drop/steal; an 8 m/s impact costs 0.2 condition; carry rules hold under spammed requests; a fish in water is recoverable for 4 s and lost after.

### 15.2 Thieves (seagull, raccoon)

- Server-simulated NetworkObjects: `NetworkTransform` (Server), `ThiefController` (server logic), `ThiefView` (local animation/audio) driven by `NetThiefState`. Raccoon uses `NavMeshAgent` (baked by SceneBuilder with `NavMeshSurface.BuildNavMesh()`; **enabled on the server only**). Seagull uses simple steering flight.
- **State machine:** `Offscreen → Telegraph → Approach → Grab → Flee → Escaped (despawn)`; shoved during Approach → Retreat (empty-handed, 20 s cooldown); shoved during Grab/Flee → drops the item (−0.1 condition) and Retreats.
- **Telegraph (fairness):** seagull circles squawking for 4–6 s; raccoon rustles a marked bush for 3–5 s. Every theft is preceded by a readable cue.
- **Targets:** loose items, items held by players, and (seagull only) a Downed player (15.6). Score `value × condition × glare / (1 + distance)`, run-RNG tiebreak (`glare` 0.5 for items held by a player with the Gull Glare skill, else 1; thieves also telegraph +1 s near that player; 14.8). Heavy (monster) items are never targeted. Creel pockets are not stealable.
- Approach speed seagull 9 m/s, raccoon 3.5 m/s; grab range 1.2 m (`item.Attach(thief.GrabNode)`, which detaches it from a hand); flee at 1.3× speed to the nearest exit or water edge → `stolen`.
- **`ThiefDirector`** (host) schedules thief spawns and owns the concurrency cap; each thief's behaviour runs in its `ThiefController`.
- **Scaling** (`CrewScaling`): `spawnInterval = 120 s / (1 + 0.35 × (n − 1))`; `maxConcurrent = 1 + (n − 1) / 2`; no thieves before 09:00 on day 1; events multiply the rate. Evening/night raise seagull→raccoon mix.
- **Counterplay is deterministic:** Shove (15.5) works on thieves within 2.2 m and below 2.5 m height during Approach, Grab or Flee.
- **Acceptance:** with 2 players a seagull steals from a hand within 2 s of reaching grab range, only after a ≥ 4 s telegraph; a shove during Flee always drops the item at 150 ms RTT; clients agree on thief state; the raccoon's agent is inactive on non-server instances.

### 15.3 Co-op monster fish

- Data: `FishSpeciesDef` with `isBoss`, `minReelers = 2`, `monsterProfile` (co-op and solo values), rolled through the special tier (+ `minPlayersInSession` gate if wanted). One boss per map, and it is the map's **day-7 climax** (R54, `SpotDef.bossSpecies`): M5 adds `lake_dock: old_whiskers` (climax `legend_night`) and `harbour_pier: harbour_leviathan` (climax `leviathan_tide`); each later map defines its own boss or legendary-fish climax.
- **Handoff:** when the cast-accept roll returns a boss, `CastResultRpc` flags it; at the bite the host spawns a server-owned `MonsterFight` NetworkObject and sends `MonsterHookedRpc(NetworkObjectReference)` to the angler (`SendTo.Owner`), whose client skips `FightSim`. The angler is reeler 1.
- **Joining:** any player within 4 m of the angler, Upright and not swimming, presses E ("Grab the line"); again to leave; ≤ 4 reelers.
- **Input:** `[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Everyone)] ReelInputRpc(byte flags, sbyte steer, RpcParams p = default)` on `MonsterFight`, sent every tick as state; non-reelers ignored; steer clamped; stale > 0.3 s counts as zero.
- **`MonsterFightSim` (Core, host-ticked):** state `lineLength`, `lateral`, `tension`, `stamina`, phase `Surface` (4–6 s telegraph, others can join, no strain) then repeating `Run` (5–8 s; pulls line at `runSpeed` 0.9 m/s; each reeler retrieves 0.5 m/s, so one reeler loses line and two gain; stamina drains `staminaDrain × against` per reeler steering against) and `Tire` (3–5 s; drift 0.3 m/s; each reeler retrieves 1.0 m/s; stamina regen 0.02/s); exhausted fish stay in Tire. Tension `dT/dt = (sqrt(R) × (Run ? reelStrainRun : reelStrainTire) + outOfWindowStrain) / lineStrength − (R == 0 ? relaxRate : 0)` with R = active reelers and `lineStrength` from the angler's rod. Exactly two escapes: tension ≥ 1 for 0.5 s (snap), or `lineLength ≥ spoolLength` 60 m (the readable "you need help" failure: the reel screams and the line visibly empties). Landed at ≤ 2 m. 0 reelers for 3 s → escape.
- **Solo profile (R37):** with 1 connected player at `Surface`, run speed ×0.5 and stamina ×0.5; a session dropping to 1 mid-fight switches at the next phase change. In co-op the co-op values apply even if one person fights alone: shouting for help is the point. Co-op stamina ×1.0/1.2/1.35 for 2/3/4 players.
- **Replication:** `NetPhase`, `NetTension`, `NetLateral`, `NetLineLength` (~10 Hz) and `NetworkList<ulong> Reelers`. Clients smooth tension and add an immediate visual nudge from their own input; switch to `AnticipatedNetworkVariable<float>` only if 150 ms feels laggy.
- **Landed monster:** a Heavy `CarryItem`; thieves can't take it and it can't be thrown; one carrier moves at 0.4× walk, each helper pressing E within 2 m adds +0.3× up to 1.0× (`NetCarrySpeedMult` read by the carrier's owner). It thrashes every 6–10 s (small knock within 2 m; in M6 a leak on deck).
- **Acceptance (EditMode bots):** co-op values with 1 bot → spool-out escape; 2 bots → Landed in 60–150 s; solo values with 1 bot → Landed in 60–150 s; a greedy always-reel bot snaps. The fight runs only on the host; a third player joining mid-fight sees correct phase and tension within 1 s.

### 15.4 Hazards, happenings, daily requests

- **`EventDef`** (one type, one asset per event): `id`, `kind` (Daily/Timed/Climax), `weight`, `minDay`, `minTier`/`maxTier` and `tierWeight[5]` (escalation bias, 14.5), `intensityScaled` (whether the tier intensity scales its multipliers), `seasons`, `setsWeather`, `durationSec` (Timed), `weightByTag`, `rareChanceMult`, `biteTimeMult`, `junkMult`, `luck`, `thiefRateMult`, `lineStrengthMult`, `freshnessDecayMult`, `sellPriceMult`, `priceBoostRandomTag`, `spawnsFrenzy`, `spawnsMonsterSighting`, `gustIntervalSec`, `visualPresetId`, `radioKey`. M4's eight data-only modifiers and the climax events use the same type. Hazards join the tier-biased pool at Weird and above (Storm, Heatwave) or Odd and above (Fog) [tune].
- **Hazard daily modifiers (each with an upside):**

| Modifier | Downside | Upside |
|---|---|---|
| Storm (sets Rain\|Storm) | line strength ×0.85; gusts every 40–70 s push players (owner-applied through the shove path, never strong enough to Down) and loose items | Predator weight ×2, bite time ×0.8, thief rate ×0.5 |
| Fog (sets Fog) | heavy fog (within the readability rule); raccoon rate ×1.5 | Nocturnal weight ×3, rare chance ×1.5 |
| Heatwave (sets Clear\|Heatwave; never Winter) | freshness decay ×2.5 | sell price ×1.25 |

- **Forecast:** the next day's modifier, weather and escalation tier are announced on the Night Camp `CampRadio` so purchases can react (two days ahead with Weather Eye); the morning radio and a HUD banner announce it again.
- **Timed happenings (1–3 per day, the count set by the day's escalation tier: Mundane 1, Odd–Weird 1–2, Unhinged and Climax 2–3, Climax including its guaranteed Monster Sighting):** **Frenzy** (90–150 s bubble patch within cast range; casts landing in it get bite time ×0.4 and a pool restricted to one species the host picks from rules passing the current gate; reuses the hotspot machinery); **Gull Flock** (60 s, up to 3 gulls ignoring the cap); **Monster Sighting** (120 s ripple zone where casts roll the boss rule at a high special chance; **guaranteed once on day 7** as the map's climax when the map has a boss, and again on every climax re-run in Infinite (every third infinite day, 14.5); rare otherwise).
- **Daily request** (one per day, `RequestDef` pool, paid into the wallet on completion, detected by the host from catch and sale streams): e.g. "Land a 3 kg+ fish before noon: +60", "Sell 5 night fish: +75", "Deliver 10 kg of perch: +50" [tune].
- **`DayDirector`** applies the day's `RunPlan` entry (modifiers, happening schedule, request), which `RunPlanner` draws with `Rng.Derive(runSeed, Events, day)`, biased by the day's escalation tier (14.5): no repeats over days 1–6 while the tier pool allows, never consecutive repeats, the day-7 climax forced, and infinite days cycling the top tier (stacked modifiers from day 10) without repeating the previous day's set; season masks respected; first-ever run day 1 forced Calm. Replicated: `NetDailyModifier` (and `NetDailyModifier2` when stacked), `NetEscalationTier`, `NetBoostedTag`, `NetworkList<ActiveHappening>` (`{ ushort eventIndex; Vector3 pos; double endServerTime; ushort speciesIndex; }`), `NetDailyRequest`. Effects are applied on the host; clients derive visuals. Intensity scales with crew size (gust frequency, flock size).
- **Acceptance:** `DayDirector` deterministic per seed; no consecutive repeats; season masks respected; frenzy species always valid for the spot and time; day 7 always has a Monster Sighting when a boss rule exists; escalation intensity caps hold; a debug overlay shows the multipliers used for the last bite roll; late joiners see the same modifier and happenings.

### 15.5 Shove, stagger, downed, revive (R28)

**`PlayerStateMachine` (Core, run only by the host):**

```
Upright   --shoved, not fighting-------------------------> Staggered (0.6 s, knockback 6 m/s + 2 m/s up)
Staggered --shoved again within 2 s, not fighting---------> Downed (6 s, ragdoll)
Upright/Staggered --shoved while fighting a fish----------> Staggered (0.5 s, 40% knockback, +0.3 tension spike)   // never Downed
Upright   --landed-monster thrash, not fighting-----------> Downed
Downed    --slap revive / splash revive / 6 s timer-------> Upright + 2 s shove immunity
Downed    --seagull grab----------------------------------> Carried --drop--> Downed (dock) or Upright + swimming (water)
any       --shoved while immune, Downed or Carried--------> ignored
```

- **Shove (F):** the owner picks the nearest target in `ShoveMath.InCone(range 2.2 m, half-angle 50°)`, highlights it, plays the arm swing and whoosh, sends `ShoveRequestRpc(target)`; no target → a funny air shove with nothing sent. Server checks: shover Upright; 1.5 s cooldown; target valid for its state; cone with slack (3.0 m, 65°) using server copies of both positions. Direction is computed on the server. Player targets get one `ShovedRpc(impulse, state, duration)` broadcast (the owner applies knockback through its CharacterController; others play FX); thieves and loose fish are handled by the server (loose fish get a 4 m/s impulse). A shoved carrier drops the item along the impulse at 3 m/s.
- The impulse can push a player off the dock: that is a deliberate laugh source, and water exits are quick (7.3).
- **Sure Footing skill (14.8):** shove and gust knockback on that player ×0.6, and Downing them needs a third shove within 2 s of the second (Staggered → Staggered → Downed).
- **While Downed:** no input except look; the held item drops; a monster reeler is removed; the seagull may carry them.
- **Slap revive:** a standing player within 1.2 m presses E → `ReviveRequestRpc(target)`; server checks state and a 3 s cooldown. **Splash revive:** a Downed player who lands in water wakes at once (also the M6 inboard bucket splash).
- **Ragdoll (cosmetic):** 6 body parts carry a Rigidbody, primitive collider and `CharacterJoint` (±40°), kinematic and collider-off until downed, on layer `Ragdoll`. The owner enables its ragdoll with the impulse and moves its CharacterController root toward the pelvis on the horizontal axes (gravity applies), so the resting place replicates through NetworkTransform; remotes enable theirs with the pelvis pulled to the replicated root by a stiff spring (k ~400). Parts below the surface get buoyancy and drag. Recovery blends to the rig pose over 0.3 s. Owner camera follows the head part with exponential smoothing (~12/s, yaw only); with Camera motion 0 it stays at eye height. No NetworkTransforms on bones.
- **Acceptance:** at 150 ms RTT a shove takes visible effect within 0.3 s; the server rejects a 3.5 m shove; a fighting player only Staggers; ragdolls end within 1 m of the same place on every screen; a slap revive works within 1.2 m + slack; a player in water can always get out within 60 s.

### 15.6 Seagull carries a downed player (flagship beat, Q4)

- At most 1 concurrent carry per session. Only a gull already active within 15 m of a player at the moment they go Downed may retarget (opportunistic), with a 1.5 s dive squawk.
- **Parenting, not following:** on grab the server sends `CarriedRpc(gull)` (the owner disables its CharacterController) and calls `playerNetworkObject.TrySetParent(gull.NetworkObject, worldPositionStays: true)`; the player's NetworkTransform has `SwitchTransformSpaceWhenParented = true`, so every client draws the player under its own view of the gull.
- **Drop:** the gull flies to the nearest authored `SafeDropPoint` (water surface or open dock, never in geometry); after ≤ 8 s, or at once if a teammate shoves the gull, the server calls `TryRemoveParent()` then `DroppedRpc(pos)`; the owner teleports there and becomes Downed (dock) or swimming and revived (water). **The server always unparents before the gull despawns.**
- **Acceptance:** no visible gap between gull and player on any client; input never frees the carried player; the drop always lands on a SafeDropPoint; despawning the gull mid-carry still drops the player safely.

### 15.7 Implementation order

1. CarryItem/FishItem, sell bin trigger, item water rules, `CrewLedger`, debug menu.
2. Body state: shove, stagger, downed, slap/splash revive, ragdoll.
3. Thieves: raccoon (NavMesh) → seagull → shove counterplay.
4. `DayDirector` hazards, happenings, daily requests.
5. Monster fight: Core sim and bots → network wrapper → heavy carry.
6. Seagull carry (needs 2, 3 and parenting).
7. "Repossessed" comedic result scene for a failed run (cosmetic).

---

## 16. Expansion (M6, outline)

**Exit:** a 4-player crew sails to an offshore sub-area, survives a storm leak sequence by bailing and patching, and returns; a solo player can do the same; voice works on online sessions.

### 16.1 Vivox proximity voice

- `com.unity.services.vivox` 16.12.1; the user enables Vivox in the Unity Cloud dashboard [user]. Online sessions only (solo never initialises UGS; a future Steam path still needs a UGS sign-in for Vivox).
- Flow: after joining, initialize and log in; join **one positional channel named after the session id**; each client updates its own 3D position from its camera at ~5 Hz; leave and log out when the session ends. Independent of NGO. API names are from memory [verify: `VivoxService.Instance.InitializeAsync/LoginAsync`, `JoinPositionalChannelAsync(name, ChatCapability.AudioOnly, Channel3DProperties)`, `Set3DPosition`].
- Defaults: audible 20 m, conversational 4 m [tune]; voice activation with a push-to-talk option; per-player mute in Pause; speaker icons on nameplates; ducking via `AudioBus`.
- Acceptance: two players hear each other clearly at 6 m and not beyond 20 m; leaving the session leaves the channel; solo never touches Vivox.

### 16.2 Boat (spike first)

- **Role:** reaches sub-areas of the current spot (offshore deep water, a reef, an island) with their own spawn rules; it is not travel between spots. **Time is the resource:** no fuel; sub-areas sit 60–150 s of sailing away.
- **Simulation:** server-authoritative `NetworkObject` + `NetworkRigidbody`; buoyancy sampled at 4 hull points from `WaveMath.HeightAt`; deliberately floppy handling (800 kg, max 6 m/s, 0.6 rad/s turn at full speed, drift term) [tune].
- **Helm:** E claims it (server single-owner lock); `HelmInputRpc(sbyte throttle, sbyte steer)` (unreliable, Everyone permission, accepted from the lock holder only; stale > 0.3 s = zero). The lock releases on disconnect, Downed, Carried, swimming, leaving the helm and boat despawn. Fallback if 150 ms helm feel is bad: give the helm player ownership with an Owner-authority NetworkTransform (only after measuring).
- **Passengers (top technical risk):** entering the deck volume → server `TrySetParent(boat)`, leaving → `TryRemoveParent()`; `SwitchTransformSpaceWhenParented = true`; the owner moves its CharacterController in boat-local space against the deck collider. Loose items rely on a high-friction deck and a cargo crate trigger. The fishing `hookedLeash` follows the boat and cast validity samples water relative to it.
- **Recovery:** capsized (`up.y < 0.3`) or stuck (speed < 0.2 with throttle > 0.5) for 8 s → teleport upright to the nearest recovery point with passengers.
- **Acceptance:** two passengers walk around a turning boat at 150 ms RTT without sliding or jitter beyond 0.1 m; the helm releases on disconnect; a capsize recovers within 10 s.

### 16.3 Leaks and bilge

- Server-owned `BoatHull` with `NetworkList<Leak>` (`{ byte id; byte slot; byte patchSteps; float rateLps; }`, ≤ 6 authored slots) and `NetBilgeLitres` (~5 Hz). No hull HP.
- Causes: rock impact > 2 m/s (one leak at the nearest slot); storm wave slams every 60–120 s while underway; a landed-monster thrash on deck. Leak rate 0.5–2 L/s; leak chance scales ×1 solo up to ×2.2 at 4 players.
- Feedback before failure: the boat sits lower; max speed × `1 − 0.5 × bilge / 200 L`.
- **Sinking** (bilge ≥ 200 L): crew teleported to the dock (wet); loose cargo on board goes into the water (lost); the boat respawns at the dock after 60 s with no leaks.

### 16.4 Bucket bailing (agreed: press to scoop, hold Q to throw)

- The bucket is a carry item (hand slot) with `NetBucketLitres`, capacity 12 L (3 scoops), so the rhythm is press, press, press, hold Q.
- **Scoop:** press E (`Boat/Bail`) within 1.5 m of bilge water → `ScoopRpc()`; server (0.4 s cooldown) moves `min(4, bilge, space)` litres into the bucket immediately.
- **Throw:** hold Q (`Boat/ThrowWater`) 0.5 s → `ThrowWaterRpc()`. Overboard if the server sees the player within 1 m of a gunwale zone looking outward (dot > 0.3); otherwise it splashes inboard: half returns to the bilge, players in a 2 m cone are splashed (cosmetic) and **a Downed player is revived**.
- One bailer manages ~4–5 L/s. Acceptance (EditMode Core sim): one player alternating patching and bailing keeps a solo storm boat afloat for a full storm sequence; a 4-player crew that ignores leaks sinks.

### 16.5 Repair minigame

- E at a leak starts a local timing bar: a marker sweeps over 1.2 s across a green zone of 20% (35% with fishing assist on). Three hits patch the leak; a miss removes a step and sprays 2 L into the bilge; ~4–6 s per leak.
- `PatchAttemptRpc(byte leakId, bool hit)`: server checks proximity (1.5 m + slack), that the leak exists, and ≥ 0.4 s since the last attempt. The client reports its own timing (anti-cheat is a non-goal).
- Patching and bailing are different players' jobs in co-op; solo alternates.

### 16.6 More spots and Steam

- Exactly three more maps (`SpotDef` + `Spot_<Id>` scene; maps 3–5, for 5 in total, R57) with their own zones, roster rows, escalation tiers, one signature fish or happening each (e.g. a night-only lantern reef) and a day-7 boss or legendary-fish climax. Each is unlocked by beating the previous map (never with currency), and each adds one map-skill slot. Boat-only offshore sub-areas.
- Steam transport evaluation behind `INetworkSession`: does the community Facepunch transport work with NGO 2.13, or is a custom `NetworkTransport` needed.
- Tone stays comedic: the boat is a stage for coordination and chaos, not survival-horror sinking.

---

## 17. Milestone plans and definitions of done

### 17.1 M1 Foundation (full detail)

**Build list**
1. Cleanup (4.2), manifest pins (4.4), asmdefs (4.3), `Tools/unity.ps1` (4.8), editor-open compile hook and menu items.
2. Builders: `ProjectSettingsBuilder`, `ContentBuilder`, `PrefabBuilder`, `NetworkPrefabsBuilder`, `SceneBuilder`, `ValidationRunner`, `ProjectBuilder.BuildAll`; play-mode start scene hook; Auto Solo toggle.
3. Core: `ConnectionStateMachine`, `JoinRules`, `PlayerKey`, `BuildInfo.ProtocolVersion`, `ContentHash` (FNV-1a 64), `Pcg32` + `Rng.Derive`, `InputContextRouter` (Free, Swimming, Climbing, Menu rows), `MovementMath`, `PitchQuant`, `SfxRecipes` (M1 set), `JoinCode.Normalize`.
4. Runtime: `ServiceRegistry` (`Register<T>`, `Get<T>` throwing on missing; not static), `EventBus` (typed `Publish<T>`, `Subscribe<T>` returning `IDisposable`; process-local), `INetworkSession`, `IInputService` + `PlayerInputAdapter`, `FishGameInput` asset and wrapper, `ISettings` + `settings.json` atomic writer, `AudioBus` + `SfxGenerator`, `WaterSurface` (flat).
5. Networking: `LocalNetworkSession` (solo, dev-direct), `UgsNetworkSession`, `ConnectionApprover`.
6. Gameplay: `SessionState` (roster, dev-settable `joinsOpen`), `PlayerSpawner`, `PlayerFP` prefab (7.2; fishing and M5 components absent), `NetworkInteractable`, `DockBell`, `WaterVolume`, `WaterExit`.
7. App: `AppBootstrap` (root `App` object with NetworkManager + UnityTransport), `GameScope` (session-scope services created and disposed per gameplay scene).
8. UI (UI Toolkit): main menu (Host, Join code, Solo, Settings, Quit, display name, dev Direct buttons), Creating/Joining spinners with Cancel, error toasts (11.2 table), Pause (Resume, Settings, Leave, join code with Copy and streamer mask), HUD placeholder (crosshair, interact prompt, prompt strip, join-code toast), M1 settings (11.6).
9. `Spot_LakeDock` scene (built by SceneBuilder): 120 × 120 m water plane at y 0 (flat blue material) plus its `BoxCollider` trigger on layer Water, top at y 0 (10.4); a low-poly lakebed bowl (layer Ground) from 0 m at the shoreline to −6 m at 40 m out; a 3 × 12 m dock deck at y 0.6 on posts (layer Structure) running out from the shore; seeded ring of primitive rocks and trees on a shore mesh (layer Shore); ≥ 2 ladders and 1 shore ramp with `WaterExit` volumes; 4 dock spawn points 1.5 m apart; a `NetSpawnAnchor` for the `DockBell` prefab (4.6); one fixed directional light; ambience bed; `SceneCamera`.
10. README section "Local multiplayer testing": MPPM 3.0 (Window > Play Mode > Scenarios, up to 3 additional editor instances), the Direct buttons, the Network Simulator, and that generated scenes are rebuilt by `FishGame > Build All`.

**M1 parameters:** walk 4.2 / sprint 6.6 m/s, jump 1.1 m, gravity −22, mouse 0.12°/count, pitch ±85°, eye 1.62 m, near 0.05 / far 400, capsule r 0.35 h 1.8, max players 4, tick rate 30.

**M1 tests**
- EditMode: `ServiceRegistry_MissingService_Throws`; `EventBus_DisposedSubscription_StopsReceiving`; `JoinCode_Normalize_TrimsAndUppercases`; `Registry_Index_IsOrdinalSortedAndStable`; `ConnectionStateMachine` legal transitions and `InvalidTransition` for every illegal pair; `JoinRules` every rule including host-self, solo-remote, version, joins-closed, full, duplicate and reclaim; `PitchQuant` (±90 → ±85, round-trip within 0.01°); `MovementMath` (jump peak 1.10 ± 0.01 m at g −22, coyote and buffer windows, `CarrySpeedMult(0) = 1`, `(40) = 0.55`); `InputContextRouter` M1 rows; `.inputactions` loads via `FromJson` with the listed maps, actions and types (Move/Look Vector2, Steer Axis, others Button); M1 audio recipe checks; 4.7 validators; architecture scan (4.9).
- PlayMode (batchmode `-nographics`, port 7790): `Bootstrap_Loads_Menu`; `Solo_Start_SpawnsOneOwnedPlayerWithCamera` (Camera and AudioListener on, remote capsule off); `Solo_LeaveThenRestart_Works` (asserts `ShutdownInProgress == false` before each start and plain UnityTransport protocol with 127.0.0.1); fake `IInputService` moves the player > 1 m in 1 s; `TeleportRpc` lands within 0.05 m with zero velocity.

**M1 acceptance criteria**
1. `Tools/unity.ps1 check` passes by the 4.8 criteria; the editor console shows zero errors and zero missing scripts.
2. **Offline solo:** with the network adapter disabled, Solo reaches the dock in under 5 s; the player walks, sprints, jumps, looks, swims, climbs out, pauses and leaves; no UGS call appears in the log; no join-code UI appears.
3. **Dev-direct:** MPPM with 1 host + 3 clones connects with no UGS link; all 4 peers show a roster of 4 in the right slot colours and nameplates; each Camera and AudioListener is active only for its owner; the `DockBell` rings on all clients.
4. **Latency:** under the Network Simulator at 150 ms RTT and 2% loss, the player acceptance in 7.8 holds.
5. **Version check:** a `-dev-protocol-offset 1` client is refused with the version message in dev-direct.
6. **Joins closed:** after the dev command `joinsOpen = false`, a join is refused with the "run in progress" message.
7. **Clean leave:** a leaving client is marked disconnected on all peers within 1 s and its player despawns.
8. **Process kill:** a killed client is detected within 10 s; the host keeps running; the same identity reconnects and reclaims its slot colour.
9. **Host leaves:** all clients return to Menu with "Host ended the session" within 10 s, with no exceptions.
10. **Mode switch:** Solo → Leave → Solo works in one process (PlayMode test); manual Online → Solo in one app session works.
11. **Settings:** M1 settings persist across restart; master volume change is audible; two editor instances don't corrupt each other's files; everything is operable by keyboard only and by gamepad only; 150% UI scale doesn't clip at 1280×720.
12. **Online (after the [user] cloud link):** Host shows a code within 5 s; an MPPM clone joins with it, including a lowercase or space-padded copy (if V3 allows); a bad code shows a readable error; a 5th joiner fails with `FullLobby`; the host leaving returns clients to Menu with a message. Record the real code format.
13. **Recorded findings** (in code comments): whether the Sessions NGO handler starts and stops NGO itself; whether NetworkManager must exist before `CreateSessionAsync`; whether the inspector Protocol Type matters on the Sessions path; that MPPM clones get distinct `PlayerId`s (if not, `SwitchProfile` while signed out); that Solo works after a Sessions run; the perf baseline (10.7).

**M1 definition of done**
- **Offline DoD:** criteria 1–11 and 13 pass.
- **Online DoD:** criterion 12 passes once the user has linked the project to Unity Cloud (Project Settings > Services) and enabled Relay and Lobby in the dashboard. Claude never accepts terms, enters billing details or clicks dashboard controls.
- **User actions due in M1:** cloud link; approve `git lfs install --local` before the first push; decide keep/remove for `com.unity.pipeline` (optional spike); optionally approve UnityYAMLMerge; close the editor before CLI runs or use editor-open mode.

### 17.2 M2 Fishing core (full detail)

**Build list**
1. Core: `RodStateMachine`, `CastMath`, `BitePlanner`, `FightSim`, `FishingHostLedger`, `CatchValidator`, `PlaceholderRoller`, weight/quality/value roll, `TensionCues`, `InputContextRouter` rod rows, M2 `SfxRecipes`.
2. Data: `RodDef` ×4, `BaitDef` ×6, `FightProfileDef` ×5, `FishSpeciesDef` ×5 + 2 junk, `FishingTuningDef`, `SfxCueTableDef`, `ArtPaletteDef` (one season used), `WaterSettingsDef` (create-if-missing).
3. Fishing: `FishingRodNetwork` (RPCs 9.9, `NetRodState`, bait vars), `FishingRodController` (fixed-step tick, input context, movement lock while hooked, stow/equip with `NetTool`), creel writes on the host (`NetCreel`), debug loadout and selectors (rod, bait, forced bite, twitchy toggle).
4. Presentation: `MeshBuilder` props, Toon/Water/Line/GradientSky shaders + `ShaderValidator`, `RenderingSetupBuilder`, `WaterTimeDriver`, `RippleField`, `RodPresenter` (rod bend, line sag/dash), bobber view (wave bob), splash particles, `FishingFeedback` (audio, camera trauma, rumble), catch reveal (procedural fish mesh), remote cast arc.
5. UI: charge ring, in-world cast marker (valid/invalid shapes), captions, line gauge (Arc/Bar), creel count, catch toasts, dev overlay (F3), settings (gauge, captions, assist).
6. Telemetry: in editor/dev builds each fight appends to `Application.persistentDataPath/fight-log.csv` (outside the repo): profile, species, weightG, castDist, castToBite, fightSec, outcome, reason, nearSnapRecoveries, maxTension, rod, assist.
7. Lake-dock rebuilt with MeshBuilder props and the water shader; ripple debug key.

**M2 tests**
- **EditMode, state machine and cast:** one test per row of the 9.2 table plus rejection of illegal events per state; ping-pong meter at 0/0.5/1.0/1.5/2.0 s gives 0/0.5/1/0.5/0; distance monotonic and clamped (charge 0, 0.25, 0.5, 0.75, 1 within 0.5 m of the formula); flight time and apex formulas, apex ≥ 0.6 m.
- **EditMode, bite:** 10,000 samples within bounds with the 1.0 s floor and the means following bait/species/depth multipliers; nibbles ordered, inside the wait and never within 0.6 s of the bite; same seed → same plan.
- **EditMode, fight (forced phases):** Runner reel-no-steer from lateral 0 snaps in 2.5–3.2 s; Runner reel + full counter over a 4 s side run doesn't snap and ends at tension 0.5–0.7; Steady reel-no-steer over a 2.5 s run stays below 0.3 tension and 0.6 lateral; from 0.9 with no reel ≤ 0.3 after 1.5 s; sign test (positive `burstDir` with steer −1 reduces |lateral| and drains stamina); dives drain only while not reeling; exhaustion stops bursts; pinned → Escaped at 5 s; slack → Escaped at 12 s; spooled line clamps and adds strain; tension touching 1 for less than the grace doesn't snap; line at land distance while reeling lands; never reeling ends by slack escape (no endless fights); same seed + inputs → identical state.
- **EditMode, roll and ledger:** species/junk frequencies within 2% of weights over 10,000 rolls; `minDepth` and `minBaitRating` gates; bare hook yields only Steady/Bulldog fish or junk at shallow depth; weight distribution mean and trophy rate (9.8); host ledger rejects a cast while active, stale/duplicate castId, `chargeNorm` outside [0,1], origin > 3 m off, no bait, an early hook and a catch below the duration floor; consumes on accept, refunds on cancel; both timeouts resolve; disconnect clears; a full creel releases the fish.
- **EditMode, data and input:** ids unique, ranges valid, every species reachable with some debug bait and depth; `Fishing/Reel` and `Fishing/Steer` exist; shaders validate; M2 recipe checks.
- **EditMode `[Category("Tuning")]`, bots (source of truth):** Greedy, CounterReel and Rhythm bots, 200 seeds each, per profile and per rod (Twig and Handmade); assert the 9.12 targets including the starter-rod check; print fight-length, snap-rate and recovery distributions.
- **PlayMode:** `SoloHost_CastBiteFight_Lands` (forced 0.1 s bite, scripted `FightInput`, asserts a creel entry and the announce); solo `DeferLocal` ordering has no re-entrant state change during `CastRequestRpc`.
- **Manual MPPM** (host + 1–3 clients, Network Simulator 150 ms RTT, 2% loss): the client's cast lands and bite/fight cues play on both peers; remote rod bend, line and bobber trail by ≤ ~250 ms; the cast arc appears once; the catch reveal shows on all screens after the host commits; a client disconnecting mid-fight leaves no outstanding hook on the host and the rod vanishes within 1 s; a host alt-tab doesn't stall clients.

**M2 acceptance criteria**
1. In solo, a player goes from Idle to a landed fish with no errors using only the 8.3 bindings, on keyboard/mouse and on gamepad.
2. Every 9.2 transition has a passing EditMode test.
3. In co-op, cues play on both peers and the reveal appears for everyone once the host commits.
4. Tension and phase (creak, shake, warn line with dash, side run vs dive) are readable with no HUD, and with sound off.
5. The rod returns to Idle within 3 s in every 9.13 case; no stuck states; no rumble left running.
6. Bait is consumed on accept, refunded on cancel/recall, lost on snap/escape/miss; the bare hook never runs out.
7. The host rejects forged or out-of-order casts and hooks and impossibly fast catches; species and weight always come from the host roll.
8. The bots meet the 9.12 targets (a tuning failure may pass only with its numbers recorded as a tuning item).
9. All tuning lives in assets; Core holds no tuning except test defaults.
10. Assist, line gauge, captions and Camera motion behave as defined in 9.11.
11. `FightSim` costs < 0.05 ms per step (Profiler).

**M2 fun gate** (≥ 30 logged playtest fights, user plus at least one friend)
- Median cast-to-bite 6–12 s; bite wait p90 ≤ 20 s with neutral modifiers; < 10% of casts end in cancel or timeout.
- Median fight: Steady/Darter 15–30 s, Bulldog/Thrasher 35–55 s, Runner 60–110 s; overall median cast-to-land 35–80 s.
- Overall snap rate 10–30% (failure should feel like greed, not bad luck); ≥ 40% of Bulldog/Thrasher/Runner fights include a near-snap recovery.
- Testers can tell the five profiles apart blind, and say the three play styles feel different.
- After 10 minutes the testers want to keep casting, and conversation was comfortable while waiting.
- A failed gate means tuning data, not new mechanics.

**M2 definition of done:** acceptance criteria and fun gate met; solo works offline; 2–4 players fish together over dev-direct (and over Sessions once linked); the owner feels no latency at 150 ms RTT.

**User actions due in M2 [user]**
- **Connect Blender's MCP server to Claude Code before the M2 plan is written.** First M2 step is a spike: build one low-poly fish through Blender MCP, export FBX into `Assets/_Project/Content/Models/` (LFS-tracked per `.gitattributes`), import it and swap it into the catch reveal. If the spike works, the five M2 species get Blender models; if not, fall back to Blender Python scripts the user runs, or keep the procedural catch-reveal mesh. Later Blender work: the M3 roster, floppy characters and cosmetics (M4/M5), seagull and raccoon (M5), boat (M6). M1 stays code-built primitives.

### 17.3 M3 Fish ecology — definition of done

- 23 species, 2 specials, 7 junk (`old_boot` shared by both spots), 2 spots, weather table, 5 fight profiles and 2 baked shore grids exist with the 12.6 values; the `WeatherDirector` pre-rolls a 7-day (35-segment) track from one seed, deterministically, with the season held for the whole week (12.2); SceneBuilder builds `Spot_HarbourPier` and re-bakes both grids in place (stable GUIDs); the lake-dock map passes reachability.
- `PlaceholderRoller` replaced by `FishingResolver`; bait gains `BaitMods`; registry indices and `ContentHash` identical across two builds and editor sessions.
- 12.9 validators, fightability, resolver tests and variety species checks (13.3) pass; balance reports pass or have recorded tuning items.
- Dev panel changes to season, band, weather, rod tier or hotspot visibly change catches in the direction the data predicts.
- `DayNightController` and seasonal palette work on the dev clock with no pops at phase boundaries or the sun/moon flip (F8 scrub); the bobber, marker and dock edge are readable at night and in maximum fog at 25 m; fog readability EditMode test passes.
- In MPPM every `CatchAnnounced` shows identical species, weight, quality and value on all peers; journals update correctly for catcher and witness; clients can't change world state.
- The journal persists in `meta.json` v1, recovers from a corrupt main file via `.bak`, and shows levels 0–3 and "biting now" correctly.
- Core compiles with `noEngineReferences`; Fishing doesn't reference Networking or UI.

### 17.4 M4 Run loop — definition of done

- `RunStateMachine` (with NightCamp, SkillPick, InfiniteVote, Save & quit and `Restore`), `RunPlanner` (7-day plan plus infinite days), `QuotaCalculator` (7-entry curve, infinite growth, monotonicity bound), `RunLedger`, `CrewScaling`, `DayClock`, `DayDirector` escalation, freshness, `ShopService`, Shells payout, skill rules and `RunSnapshot` round-trip pass the 14.11 EditMode tests. No meta rods or perks exist anywhere.
- A full 7-day solo expedition runs offline in PlayMode with the 3 s day config through 7 Settles and 6 Night Camps to SkillPick and Won. A second run enters Infinite and ends InfiniteEnded. A forced miss gives Lost. A Save & quit plus resume restores the run exactly. Shells are paid per met day and once only.
- Hub scene with `MapBoard` (map choice from unlocked maps, the lake → harbour unlock chain, "Resume saved run" and "Abandon saved run"), StartBell, skill and cosmetic loadout, and ready-up. Briefing shows the chosen map, rolled season, forecast, the seven-quota ladder with kinds, and the market board.
- Clock HUD (`Day N/7`, infinite day), quota bar (banked + held, pace tick), crew panel, Last Call horn and banner, settle screen with awards and Shells; the Night Camp set (camp shop with rod rack, forecast radio with story beats, ready-up, "Run saved" tag, Save & quit); skill pick screen; Keep Fishing (Infinite) vote UI; day-phase music pads.
- 8 data-only daily modifiers with per-map escalation tiers over the 7 days, a data-only day-7 climax event per map, and infinite-day cycling with stacked modifiers. Also the bounty species, shop stock seeded per Night Camp, bait pouch, lures, in-run rods (every run starts with the Twig starter kit), Cooler and Fish Finder.
- The 10 M4 skills of the 12-skill pool work, with host validation using the skill-adjusted cast distance.
- Meta save v2 with the v1 → v2 migration: Shells, owned cosmetics and cosmetic loadout, `unlockedMaps`, `skillByMap[5]`, `bestInfiniteDayByMap`, stats, journal and the `pendingRun` reference. No pending-run banking. The cosmetics store UI ships in M5 (19 #33); earning Shells and equipping starter cosmetics ship in M4.
- `runlog.csv` is written at every Settle (7 per trip, plus infinite days). The 2–4 player MPPM checklist in 14.11 passes, including Save & quit, resume and slot reclaim. Feel targets are checked in at least one 2-player playtest of a full 7-day trip played across two sittings.

### 17.5 M5 Friend slop — definition of done

- The section 15 exit beat list and solo run pass under MPPM (main + 3 instances) with 150 ms RTT on clients.
- **Item conservation:** every spawned item is in exactly one ledger bucket at each day end; no duplicates.
- **State agreement:** within 1 s of a change, every client shows the same clock, quota, daily modifier and escalation tier, run phase (including Night Camp and the Infinite vote), infinite day, thief state, monster phase and happenings. Each roster member's skills take effect identically for host-side rolls whatever their client.
- 15.1–15.6 acceptance items pass; monster bots pass; full 13.3 variety checks (with daily requests) and beat-gap checks pass; thieves, hazards and monster stamina follow `CrewScaling`.

### 17.6 M6 Expansion — definition of done

- The boat passenger spike passes before any other M6 work; then the 16.2–16.5 acceptance items pass.
- Vivox acceptance (16.1) passes on an online session.
- Exactly three new maps (maps 3–5, 5 in total) each pass the 12.9 validators and 13.3 variety checks, have a 7-day escalation table and a day-7 climax, and unlock only by beating the previous map; boat travel costs only clock time.
- A Steam transport evaluation note is written (feasible as-is / needs a custom transport).

---

## 18. Verification list ([verify] items)

| Id | Item | When |
|---|---|---|
| V1 | Sessions NGO handler starts/stops NGO itself; whether NetworkManager must exist before `CreateSessionAsync`; whether UnityTransport Protocol Type matters on that path (read the resolved Multiplayer Services source). Fallback: manual Relay (`AllocationUtils.ToRelayServerData` + `StartHost`/`StartClient`) inside `UgsNetworkSession` with no interface change | M1 |
| V2 | Multiplayer Services 2.3.3 names: `IHostSession` lock/unlock (`IsLocked` + save), `RemovePlayerAsync`, `SessionError` values for full/not-found | M1 |
| V3 | The real `session.Code` length, character set and case sensitivity | M1 |
| V4 | Server-side `OnSceneEvent` `SynchronizeComplete` for spawning late joiners (the enum value exists in 2.13.3; confirm it fires on the server per client) | M1 |
| V5 | `SessionState` with `destroyWithScene: false` survives `LoadSceneMode.Single` loads | M1 |
| V6 | MPPM namespace spelling (`Unity.Multiplayer.PlayMode` vs `Playmode`), `CurrentPlayer` APIs, shared `persistentDataPath`, distinct anonymous PlayerIds per clone | M1 |
| V7 | Exact asmdef names in multiplayer, authentication, core and tools; multiplayer.tools 2.2.12 compiles with NGO 2.13.3; Network Simulator works on MPPM clones and its 150 ms preset values | M1 |
| V8 | `InputSystem.actions` setter (1.20.0); `TestRunnerApi.SaveResultToFile` (1.8.0). (Resolved: NGO's setting is `NetcodeForGameObjectsProjectSettings.GenerateDefaultNetworkPrefabs`; see 4.6.) | M1 |
| V9 | UI Toolkit runtime buttons receive clicks in Input-System-only mode without an EventSystem | M1 |
| V10 | Clients see the host's own player move (old NGO 2.2.0 report) | M1 |
| V11 | `DeferLocal` behaviour on `SendTo.Owner` when the host is the owner (the field exists); change detection for memcpy structs (IEquatable vs memcmp). (Resolved: `NetworkVariableUpdateTraits.MinSecondsBetweenUpdates` and `SetUpdateTraits` exist in 2.13.3.) | M1–M2 |
| V12 | Raycasting a trigger water collider with `QueryTriggerInteraction.Collide`, water first when a dock is above it | M2 |
| V13 | Water facet-normal sign; C#/HLSL wave parity; fog variants kept in a player build; `ShaderUtil` messages in `-nographics` batchmode | M2 |
| V14 | Whether Newtonsoft resolves under `noEngineReferences` (else the serializer stays in Runtime) | M3 |
| V15 | (Resolved: `NetworkManager.ServerTime.Time` is a double.) `DisconnectReason`, `OnClientStopped`, approval `request.Payload`/`response.Reason`; a Sessions join rejected by approval surfaces the reason text | M4 |
| V16 | `AttachableBehaviour` `ComponentController` toggling and visual pop at 150 ms RTT (detach RPC one tick before the root's final transform) | M5 |
| V17 | `TrySetParent` + `SwitchTransformSpaceWhenParented` with an owner-authoritative CharacterController on a server-simulated gull and boat at 150 ms RTT | M5–M6 |
| V18 | Vivox 16.12.1 API names | M6 |
| V19 | Community Facepunch transport with NGO 2.13 | M6 |
| V20 | The anonymous UGS `PlayerId` survives an app relaunch on the same machine and profile (cached session token), so a resumed run's slots are reclaimed by persistent id on another evening (R56). Fallback: the host assigns a disconnected snapshot slot to a joiner in the Hub (6.6 rule 8) | M4 |

---

## 19. Proposed defaults (confirm at spec review)

Each is changeable later. They apply unless the user overrides them at review.

1. Fight authority hybrid: owner simulates, host owns rolls, loot and validation (N6).
2. Charge cast sets distance; aim pitch only shapes the arc; RMB-ready and blue-marker rule kept; **ping-pong charge meter on**.
3. Cast cancel refunds bait; unlimited bare hook.
4. Auto-hook by default; twitchy fish are a per-species option from M3.
5. Dives and flips as per-profile fight behaviours; five shared profiles.
6. Catch reveal and the "got away" jump broadcast to everyone; species revealed on a lost hooked fish; species hidden from others until then.
7. Slack escape after 12 s with no reeling; movement locked while hooked.
8. No HUD tension meter by default; free line gauge setting; Fish Finder as the shop gadget.
9. Overfishing skipped (the hotspot and market board already push crews to move); revisit in M5 only if playtests show camping, as a soft per-sub-area weight decay.
10. Shore-distance sub-areas; depth as an extra gate.
11. Journal: crew catches count as Seen/CrewLanded for everyone present; Caught and bests are the catcher's.
12. Hotspot in M3 (with a cut-line).
13. Roster of 23 + 2 specials (grown from ~20 so every season × band × weather has ≥ 3 species).
14. UI Toolkit; domain reload on; `com.unity.pipeline` kept but unused.
15. Network Simulator (`com.unity.multiplayer.tools`) as a dependency.
16. Joins in Hub/Briefing/Night Camp with slot reclaim (also after a resume, by persistent player id); quota crew size fixed at day start; time-interpolated rescale on a mid-day drop; no mid-day reconnect.
17. Shared in-run crew wallet; a shop at every Night Camp (6 per trip, plus one before each infinite day, from night 7 on); Twig starter kit for every run; lure lost on snap.
18. Daily modifier, weather and escalation tier forecast on the Night Camp radio; sunk boat cargo lost permanently.
19. Automatic solo monster profile (no purchasable winch).
20. Downed/revive and seagull carry in M5 (see Q4 for the flagship decision).
21. Gamepad: Sprint on L3, Shove on R3 (re-check comfort at M1 sign-off).

**Added with the 2026-10-09 amendments (R53–R60).** These are this spec's fill-ins, not user decisions:

22. Night Camp is in-world at the map's campfire in the same Spot scene (no scene load between days). The sell bin is shut at camp.
23. The run snapshot is host-local, and only the machine that saved it can resume. It is written on entering Night Camp, at NightCampDone, on entering SkillPick (the win snapshot) and on Save & quit. A resume reopens that night's Night Camp, or SkillPick from the win snapshot, so a won day 7 is never replayed. Clients' `pendingRun` is informational.
24. Map unlock chain: beating map N unlocks N+1 for every roster slot at that win (delivered with the skill award, #25). The Hub map board offers the union of connected players' unlocked maps, and the host picks.
25. The map skill goes to every roster slot at the day-7 Settle, connected or not. Connected players are awarded at once; a disconnected slot's award is held by the host as a pending clear and delivered when that player reconnects to the same session or reclaims the slot on a resume (14.8). It lapses only when the session ends and the snapshot is deleted. The pick has a 60 s timeout; an unpicked slot (and every pending-clear recipient) picks later at the Hub `SkillBoard`.
26. The 12-skill pool and its values (14.8): 10 skills in M4, Gull Glare and Sure Footing in M5. Combined skill bite-wait terms are floored at ×0.8.
27. Cosmetic currency named "Shells", paid flat per player: 10 per met day, 15 per infinite day, 40 for the win, 60 on a first clear. No wallet conversion, never split.
28. 8 data-only daily modifiers. Day 7's slot is the map's climax event. Default escalation is Mundane, Odd, Odd, Weird, Weird, Unhinged, Climax, with intensities 0.8 / 1.0 / 1.15 / 1.3 / 1.5 and the fairness caps in 14.5.
29. Infinite: growth ×1.2 per day; intensity +0.05 per infinite day (cap 1.6); two stacked modifiers from day 10; climax re-run every third infinite day.
30. Pressure targets 0.50 → 0.85 over days 1–7, with base calibrated at day 4 (14.4).
31. In-run rod prices and camp gating: Handmade 120 from camp 1, Carbon 300 from camp 2, Deep-sea 600 from camp 4 (solo base). Per-player rod prices scale by the `CrewScaling` rod rate `crewFactor[n] / n` (×1.0 / 0.90 / 0.83 / 0.78) so the default rod path stays the same share of per-head income at every crew size (14.4). There is no rental rod (an earlier draft had one).
32. Phase timeouts: Night Camp 180 s cap with a 10 s host force; SkillPick 60 s; Infinite vote 30 s.
33. The cosmetics store and emotes ship in M5. M4 earns Shells and equips starter cosmetics.

---

## 20. Open Questions for User

Each has a default that applies until you answer.

| # | Question | Default (applies if unanswered) |
|---|---|---|
| Q1 | **Quota semantics.** Is the daily quota a *threshold* (sales are checked, all money stays in the crew wallet) or *paid* (the company takes the quota; only surplus is spendable)? "Paid" makes every purchase tighter and the shop leaner. | Threshold |
| Q2 | **Cosmetic currency (Shells) in co-op** (re-scoped by R59/R60). Every player earns the same flat amount per met day, per infinite day survived and for the win, so co-op and solo earn at the same pace per head. Or should co-op earn a small crew bonus, or should part of it follow contribution? | Flat per player, same as solo (14.8) |
| Q3 | **Carry granularity in M5.** Walking every small fish to the bin one at a time risks the boring loop. Physical carry only for fish ≥ ~5 kg (small fish stay in a pocket creel), everything physical, or everything physical plus a carryable crew cooler? | ≥ 5 kg physical, small fish in a 6-slot pocket |
| Q4 | **Seagull carries a downed player** as the M5 flagship laugh beat (capped at 1 at a time, opportunistic only)? | Yes |
| Q5 | **Boat flooding and bucket bailing overlap with Dreadline: Net Quota.** Keep bailing as the signature boat twist (with the inboard-splash revive comedy), or design a different flooding mechanic? | Keep |
| Q6 | **Expedition length and clock span:** 06:00–02:00 over 15 real minutes per day. **Answered (2026-10-09): 15-minute days confirmed, with a 7-day trip** (~105 min of fishing plus Night Camps, ~2 h, over one or more evenings; R53). | Answered: confirmed |
| Q8 | **Player-vs-player shove toggle.** Add a host lobby option to turn off shoving between players (thieves always stay shoveable)? | Add the toggle, default on (shoving allowed) |

Q7 (mid-run save/resume) was answered yes at review (2026-10-09) and removed. It is now in scope as the nightly Night Camp snapshot (R56). The other questions keep their numbers.

---

## 21. Risks

| # | Risk | Mitigation |
|---|---|---|
| K1 | **Fun risk: the quota loop gets samey** (the user's top worry) | Feel-first M2 fun gate; five fight profiles; narrow ecology windows; market board, modifiers, beat schedule and the **escalation arc** in M4 (per-map tier per day across the 7-day schedule, nightly story beats and a day-7 climax, so no two days of a trip feel alike); mid-week rod upgrades that open new fish; hazards, happenings, requests and thieves in M5; 13.3 variety tests; `runlog.csv` and fight telemetry tuning |
| K2 | Fight numbers are hand-worked estimates (the ~5 s greedy-snap anchor comes from one forum account) | Bots are the source of truth; playtest gate; all values in data |
| K3 | Economy and pressure numbers come from a proxy model | V5 report re-derives them from the real resolver and M2 cycle times over the full 7-day trip on the default in-run rod path; quota bases calibrated by rule (day-4 pressure); the 7-entry `dayMult` curve and the infinite growth factor retuned from `runlog.csv` within the monotonicity bound (14.4); `crewFactor` retuned from `runlog.csv`; per-player rod prices follow the `crewFactor[n] / n` crew rate so the default rod path stays affordable at every crew size, and the V4/V5 per-n affordability check (after bait spend) is the gate on the ±10% pressure rule |
| K4 | Sessions handler behaviour differs from docs | V1; fallback to a manual Relay path inside `UgsNetworkSession` with no interface change |
| K5 | Relay latency above 150 ms affects host-validated actions (buy, pickup, throw) and the monster fish | Owner-simulated normal fights; region option in `WithRelayNetwork`; anticipated variables for the monster if needed |
| K6 | Lobby seat not freed after a drop blocks rejoin | V2; M1 criterion 8 |
| K7 | Owner-authoritative players on host-moved things (seagull carry, boat deck) | V17 spike early in M6 (and in M5 for the gull); `SwitchTransformSpaceWhenParented` set on the prefab (changing it at runtime forces a teleport); helm-owner fallback |
| K8 | `AttachableBehaviour` edge cases (one-frame pop on detach) | Root-follows-holder rule; V16 spike in M5 |
| K9 | Unity Cloud not linked, UGS outage or licence issue | Solo and dev-direct never depend on UGS (M1 criterion 2) |
| K10 | Night and fog readability kill fun | Fog readability rule tested in EditMode; tune `toonAmbient`, moon intensity and fog max early |
| K11 | Procedural audio sounds thin | Simple recipes, tested on headphones and speakers; recipe parameters are data |
| K12 | Shader pitfalls (Forward+ cluster loop, fog stripping, facet-normal sign, wave parity) | `ShaderValidator`; first player build as the authoritative check; M2 visual checks |
| K13 | Editor open vs batchmode lock slows iteration | Editor-open mode with the compile log hook and menu items; optional pipeline spike |
| K14 | Shove griefing stops being funny after an hour | Stagger-only for fighting players, immunity windows, cooldown, Q8 toggle, playtest |
| K15 | Derivative of Dreadline: Net Quota | Comedy, Rust-style skill fishing, Stardew ecology, a 7-day escalating "trip gone crazy" with map skills and Infinite mode, rolled season, weather and modifiers on a chosen map; Q5 |
| K16 | Domain reload on costs Play-mode entry time | Accepted for correctness; revisit once statics reset on `SubsystemRegistration` |
| K17 | GitHub LFS quota (1 GB free) | No textures or audio files (everything procedural); LFS hook installed before the first push |
| K18 | Experimental `com.unity.pipeline` runs a localhost HTTP server | Nothing depends on it; removed if the spike fails or the user prefers |
| K19 | **7-day length fatigue:** a ~2 h trip drags, or crews never finish one | The escalation arc (each day weirder, day-7 climax) and nightly story beats give the week a shape; Night Camp breaks after every day; nightly save/resume lets a trip span evenings; Infinite is an optional extension, never required; the 2-player playtest across two sittings (17.4) checks it |
| K20 | Save/resume misses state, or a reclaim fails across evenings (UGS id changes), so a resumed crew loses creels or rods | One `RunSnapshot` DTO with an EditMode round-trip test over every field; `ContentHash`/`ProtocolVersion` refusal; snapshot only at Night Camp, where no cast, fight or loose item exists; V20 plus the host slot-assign fallback; `.bak` fallback |

