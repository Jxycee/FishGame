# FishGame M3 - Plan Section D: Host, world and network (T13, T14, T15)

- **Date:** 2026-10-13 (written 2026-10-12). Branch `m3-fish-ecology`.
- **Contract (binding):** `docs/superpowers/plans/2026-10-13-m3-interfaces.md`, cited as "C". M2 contract `2026-10-11-m2-interfaces.md`, cited as "C2". Skeleton: `docs/superpowers/plans/2026-10-13-m3-plan-skeleton.md`.
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` 9.3 (cast validity, "from M3 the bobber cell must also be a water cell"), 9.9 (host ledger and the CastRequest checks, "from M3 also: grid cell is water, minute < 1200"), 12.2, 12.4, 12.7, 12.8, 12.11, 14.2 (clock replication), R34 (solo pause never touches `Time.timeScale`).
- **Scope:**
  - T13: the host roll on the resolver, the M3 cast checks, catch-limit reservations and the run-state cast counter. `PlaceholderRoller` is removed.
  - T14: `WorldState`, the wire structs, `DevWorldDriver`, `EcologySession`, ProtocolVersion 4 (R72 took 3).
  - T15: the world-built host context, the owner marker gates, `Luck`, and the dev map switch.
- **Serial order (skeleton 3):** T13 -> T14 -> (T16, section E) -> T15. T15 needs T16's `World` object and the baked lake grid.
- **Code note:** this file holds code only where skeleton 2.3 allows it:
  - the ledger's reservation lifecycle (T13);
  - the `DevWorldDriver` re-anchoring for pause, scrub, auto new day and map switch (T14);
  - the host context build and the shared gate helper (T15).

  Everything else is behaviour, signatures and tests.
- **Reading rule:** every brief carries C 2.8 by reference. Each task lists the constraints that bite hardest.

---

## Contract change requests (ALL ACCEPTED 2026-10-12, applied in the contract)

**Status: CCR-D1..D5 are all ACCEPTED as proposed (CCR accepted 2026-10-12): the primary option of each is the one to build, and the fallback or alternative offered under D1 and D2 is not used. Applied in the contract (2.6, 7.13, 7.14, 9, 10, 12); rulings M3-D55 = D1 (no longer blocking), D56 = D2, D57 = D3, D58 = D4, D59 = D5.**

**CCR-D1 (was BLOCKING for T13; accepted): T13's file list misses compile-breaking callers.**

T13 does three removals:
- `FishSpeciesData.MinBaitRating/MinDepth`;
- the `FishSpeciesDef` fields and their two `Configure` parameters (M3-D3);
- `PlaceholderRoller` and `RollRequest`.

They break files that C 12 / C 2.6 do not give T13. Each one below is a mechanical edit with the public surface frozen. The request: add these to the C 12 T13 row and to C 2.6.

| File | Owner today | What breaks | T13 fix |
|---|---|---|---|
| `Tests/EditMode/FishingTestData.cs` | R72 ("not edited", C 2.6) | `Fish(...)` sets `MinBaitRating` and `MinDepth` | Remove those two initializers only; no fight value changes. This is the main exception being requested. |
| `Tests/EditMode/FishingDefsTests.cs` | M2 | species `Configure`/`ToCore` asserts in the `CheckSpecies` helper; the `FishSpeciesDef` row of `FishingDefs_SerializedNames_MatchContract` | Drop the two asserts. In the csv, drop `minBaitRating,minDepth`; T3's appended `tags` stays last. |
| `Tests/EditMode/FishingValidatorTests.cs` | M2 | the species `Configure` caller; the `minBaitRating`/`minDepth` range rows; `FishingValidators_UnreachableSpecies_IsReported` | Drop the arguments and rows. **Delete** that test: V3 `EcologyCoverage_V3_EverySpeciesPassesSomewhereAtRodThree` replaces it. |
| `Tests/EditMode/FishingContentTests.cs` | T3 (C 2.6) | the range rows and seed-field asserts for the two fields; `FishingData_EverySpecies_ReachableWithSomeBaitAndDepth` (`PlaceholderRoller.IsEligible`) | Drop the rows and asserts. **Delete** that test: V3 replaces it. |
| `Tests/EditMode/FishingRollValidationTests.cs` | M2 | both tests use `PlaceholderRoller`/`RollRequest` | Point them at `FishingRoller.Roll` over `EcologyTestData`. Names are kept. |
| `Tests/EditMode/FishingWireTests.cs` | M2 T17 | `FishingRollNet_RoundTrip_PreservesRoll`, `FishingRollNet_Flags_OnlyBitZeroIsPity`, `FishingRollNet_InsaneRoll_TryToCoreFalse` | Point them at `FishingRoller.Roll`. Names are kept. |
| `Editor/Builders/Ecology/{EcologyMigration,EcologyContentBuilder,EcologySeedData,EcologySeedData.Roster}.cs` | T3, T10 | `FishSpeciesDef.Configure(...)` callers; `FishSpeciesData { MinBaitRating = .., MinDepth = .. }` initializers | Drop the two arguments and initializers. |
| `Core/Fishing/{BitePlanner,RollOverrides,FishingRoll}.cs` | M2 | comments only | Reword `PlaceholderRoller` to `FishingRoller`. |

- **Fallback if the controller rejects D1 (NOT used: CCR accepted 2026-10-12):** T13 keeps the two fields on `FishSpeciesData`/`FishSpeciesDef`, unused and marked `// M3-D3: unused, removed in M4`. T13 still deletes `PlaceholderRoller`/`RollRequest`, so the `FishingRollValidationTests` and `FishingWireTests` rows above are still needed.
- **Stale YAML:** the species `.asset` files keep the stale `minBaitRating:`/`minDepth:` keys until Unity re-saves them. Unity ignores unknown keys. Nobody hand-edits the assets.

**CCR-D2 (clarification, no code change): "unregisters on despawn" (C 7.14 `WorldState`).**
- `ServiceRegistry` has no remove method.
- `WorldState` calls `ServiceRoots.Game.Replace<IWorldContext>(this)` on spawn.
- On despawn it sets `HasState` false and clears `Current`. The entry dies with the scene, because `GameScope.OnDestroy` disposes the registry.
- Consumers already check `HasState` (C 7.5).
- Alternative, if the controller prefers it (NOT used: the primary option above, CCR accepted 2026-10-12): add `ServiceRegistry.TryRemove<T>(T instance)` (Runtime) to C 2.6 for T14.

**CCR-D3 (clarification): the run seed source (C 7.14 "GetOrCreate(FishingHost run seed)").**
- Gameplay does not reference Fishing, so `DevWorldDriver` cannot read `FishingHost.RunSeed`.
- Ruling used in this plan:
  - `EcologySession.GetOrCreate(seed)` returns the existing run state when there is one, and ignores the seed.
  - Otherwise it creates one, with the season rolled by `WeatherDirector.RollSeason(seed)` and `Day` 1.
  - `DevWorldDriver` calls `GetOrCreate(FishingConstants.DefaultDevRunSeed)`.
  - `FishingHost.SetRunSeed` (allowed only while `HostCastCounter == 0`) calls `Clear()` and then `GetOrCreate(newSeed)`.
  - `DevWorldDriver` notices the new instance by reference and re-adopts it: season, track, clock at minute 0, planner.
- No public surface changes.

**CCR-D4 (manifest additions, informational).** Internal helpers and extra tests ("others may be added", C 10):
- T14: `Gameplay/World/DevClock.cs` (internal `DevClock` and `DevWorldRules`), test `Tests/EditMode/DevWorldTests.cs`. (No `Gameplay/AssemblyInfo.cs` edit: T2 adds the `InternalsVisibleTo` line, CCR-A2 accepted 2026-10-12; T14 finds it present.)
- T15: `Fishing/Host/SpotEcology.cs` (internal), tests `Tests/EditMode/SpotEcologyTests.cs` and `Tests/PlayMode/FishingWorldTests.cs`.
- T13: one extra test, `FishingHostLedger_InterimLakeContext_BluegillEligible`, which T15 deletes together with the interim.

**CCR-D5 (for section E / T16, informational):**
- `WorldState`'s serialized spot field is named **`spotId`**. `WorldSceneObjects.AddTo` writes it through `SerializedObject.FindProperty("spotId")`.
- `WorldState.OnNetworkSpawn` (server) calls the internal `DevWorldDriver.ServerInitialize(this)` on the same GameObject. `WorldSceneObjects` must add both components to the one `World` root (C 6.3 already says so).

---

## 0. Section conventions

### 0.1 Commands
- Every Unity run uses the PowerShell tool with `timeout: 600000`. Full `test-edit`, `test-play` and `check` runs go in the background (`run_in_background`); wait for the notification.
- Stop only if `unity.ps1` reports LOCKED (exit 2). A leftover `Temp/UnityLockfile` is not a lock. Never kill Unity.
```powershell
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
$env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play -TimeoutMinutes 15
```
- Expect `FG RESULT <command> PASS ...`.
- After a `build`, restore scene churn with `git restore Assets/_Project/Scenes/*.unity`. From T14 on, restore or commit a Spot scene **as a whole file**, never a hunk (C 2.8 #24). None of T13-T15 changes a scene on purpose.
- Edit with the Write/Edit tools only. No sed or python rewrites (rework handoff 5).

### 0.2 Facts checked in code and package source (2026-10-12)

| Id | Fact | Source |
|---|---|---|
| D-F1 | `FishingHostLedger.Resolve(...)` is already the single function that every first resolution of an active cast passes through: Cancel in Waiting and Hooked (via `ResolveCancelledWaiting`), early/invalid `Hooked`, `Result`, both `Tick` timeouts. `Disconnect` is the only path that clears an active cast **without** `Resolve`. | `Core/Fishing/FishingHostLedger.cs` |
| D-F2 | The ledger already rolls with `counter = HostCastCounter + 1` and assigns the counter only after the roll succeeded, so "a bad override throws before any state changes". T13 keeps that order with the counter in the run state. | same |
| D-F3 | `HostCastView` defaults are traps after T13: `GridWater` defaults to false (every old view becomes `NotWater`) and a default `Context` has invalid `Events`/`Skills` (the resolver throws, C 7.5). Every builder of a view must set both. | C 7.13, C 7.5 |
| D-F4 | NGO sets `NetworkBehaviour.IsSpawned = true` in `InternalOnNetworkSpawn`, before `OnNetworkSpawn` runs, so server NetworkVariable writes inside `OnNetworkSpawn` are legal and travel with the spawn payload. | `Runtime/Core/NetworkBehaviour.cs` (`InternalOnNetworkSpawn`), `NetworkObject.InvokeBehaviourNetworkSpawn` |
| D-F5 | `NetworkSceneManager.LoadScene(name, Single)` refuses only while another scene event is active (`SceneEventInProgress`) or when the scene is not in build settings (`InvalidSceneName`, plus an error log). Reloading the active scene is allowed, so T15 can test the map switch lake -> lake before T17 exists. | `Runtime/SceneManagement/NetworkSceneManager.cs` (`ValidateSceneEvent`, `LoadScene`) |
| D-F6 | On a server Single `LoadScene` (synchronously, before the async load starts):<br>- `ServerDestroySpawnedSceneObjects` despawns in-scene placed objects, so `WorldState` goes first;<br>- then `MoveObjectsToDontDestroyOnLoad` despawns every dynamically spawned `destroyWithScene` object: players, rods, the bell.<br>So on a dev map switch the rods despawn while `FishingHost` is still enabled, and `HandleRodDespawned -> Ledger.Disconnect` releases reservations. On other unloads (session end -> Menu by plain `SceneManager.LoadScene`) the order relative to `FishingHost.OnDisable` is not defined. The `OnDisable` guard in T15 covers that case, and stays as belt-and-braces. | `NetworkSceneManager.LoadScene` / `MoveObjectsToDontDestroyOnLoad`, `NetworkSpawnManager.ServerDestroySpawnedSceneObjects`, `PlayerSpawner` (`destroyWithScene: true`) |
| D-F7 | `ServiceRoots.Game` is per Spot scene (`GameScope.Awake`, order -1500) and holds `IPauseState`. `ServiceRegistry` has `Register`, `Replace`, `TryGet` and no remove. | `App/GameScope.cs`, `Runtime/Services/ServiceRegistry.cs` |
| D-F8 | EditMode tests reference `FishGame.Gameplay`, `FishGame.Fishing`, `Unity.Netcode.Runtime` and `Unity.Collections`; Fishing already grants `InternalsVisibleTo("FishGame.Tests.EditMode")`; Gameplay grants PlayMode only until T2 adds the EditMode line (CCR-A2). | asmdefs, `*/AssemblyInfo.cs` |
| D-F9 | `INetworkSession.Ended` fires exactly once per attempt (session end, including a normal leave). `SceneFlow` gets the App `EventBus` in its constructor. | `Runtime/Net/INetworkSession.cs`, `App/AppBootstrap.cs` |

### 0.3 Cross-section questions (controller answers against the contract)
**Answers (2026-10-12):** Q1, Q2 and Q4 are answered by section B's plan (below); Q3 by section A's T1.4; the user question "fixed dev run seed" (Open questions, item 1) keeps its default (fixed). Q2: `new EcologyRunState(seed)` starts at Day 1 with the ROLLED season (B-D8), NOT Spring; T14's `run.Season = WeatherDirector.RollSeason(seed)` after construction is redundant but harmless. Q3: the reset is `FishingDebugSettings.ClearOnSessionEnd(INetworkSession)` (hooks `INetworkSession.Ended`; handler `OnSessionEnded`); T15's `Luck` is reset there too.

1. **Section B (T7):** `HotspotPlanner.Advance(minute)` must catch up in **one call** across a forward jump that spans one or more whole hotspot windows (F8 scrub runs about 1.3 min/frame; `DevSetMinute` jumps hundreds of minutes) and end in the same state as a minute-by-minute walk. Please pin it with a test (for example "Advance(600) from 0 equals Advance(1..600)"). And what does the constructor do with a null grid? This plan never passes null (no planner without a grid).
2. **Section B (T5):** confirm `new EcologyRunState(seed)` starts at `Day = 1` and `Season = Spring`; T14 sets the season right after construction. Confirm `CatchLedger.Commit/Release` on an existing key does not allocate (the ledger's `Tick` zero-alloc test runs releases).
3. **Section A (T1):** name of the session-end reset T1 adds to `FishingDebugSettings` (T15 adds `Luck` to it).
4. **Section C (T9):** this plan reads ecology data only through Core types (`ContentRegistry.Ecology`, `EcologyDatabase.Spots[spotIndex].ZoneIndex`, `Zones[i].Grid/Weather`, `Tuning`) plus the existing `TryGetSpot`/`SpotIndex`, so it does not depend on the T9 Data getter names.

---

## T13 - Host roll: ledger on the resolver

**Goal:**
- `FishingHostLedger` rolls through `FishingRoller`/`FishingResolver` with a host-built `FishingContext`.
- The M3 cast checks: grid water -> `NotWater` after the probe; casting closed -> `NotAllowedNow` first.
- Catch-limit reservations (M3-D14).
- The cast counter lives in `EcologyRunState`.
- `PlaceholderRoller`, `RollRequest` and the species-level gates are removed.
- `EcologyTestData` is the shared fixture.
- An interim `FishingHost` keeps every M2 cast landing.

**Owned files (C 12 + CCR-D1):**
- `Core/Fishing/{FishingHostLedger,CatchValidator,HostCastView,ActiveCast,FishSpeciesData}.cs`, `Data/Fishing/FishSpeciesDef.cs`;
- `Editor/Validation/FishingValidators.cs`, `Editor/Builders/Fishing/{FishingSeedData,FishingContentBuilder}.cs`;
- `git rm` of `Core/Fishing/{PlaceholderRoller,RollRequest}.cs` (+ `.meta`) and `Tests/EditMode/PlaceholderRollerTests.cs` (+ `.meta`);
- new `Tests/EditMode/Ecology/EcologyTestData.cs`; `Tests/EditMode/{FishingHostLedgerTests,CatchValidatorTests}.cs`;
- the interim edit in `Fishing/Host/FishingHost.cs`;
- the CCR-D1 rows.

**Shared edits (C 2.6):** `FishingHost.cs` (T13 interim, after T1), `FishSpeciesData/FishSpeciesDef` (after T3), `FishingValidators.cs` (after T3), `FishingSeedData/FishingContentBuilder` (callers, after T10).
**Depends on:** T6 (`FishingResolver`, `FishingRoller`), T10 (registry `Ecology` with zones; `lake_dock` zone). T5 through them.
**Implementer:** Sonnet. **Reviewer:** Opus high.
**Constraints that matter most (C 2.8):**
- #23: no client value enters a roll except the validated request fields; rod tier, bait and depth come from the ledger.
- #25: the roll stream is `Rng.Stream(Run.RunSeed, CatchStreamId, counter)`; no new draws.
- #26: the grid gate is the host half; T15 adds the owner half.
- C2 #8: EditMode reaches Core only for the ledger.
- C2 #9: `test-play` stays green and warning-free.
- R72 (C 1.2 #1): the `Result` path, `CatchValidator.MaxReelSpeed` and every fight field are R72's. T13 touches only `RequestCast`, `Resolve`, `Disconnect`, the constructor and the forwarders. Merge onto R72's version of the file and never revert an R72 line.

### Behaviour

**`HostCastView` (edit, C 7.13):**
- Append `public bool GridWater;` and `public FishingContext Context;`.
- Comment: "a default view rejects NotWater and its Context throws in the resolver; every builder sets both (D-F3)".

**`ActiveCast` (edit):** append `public bool LimitReserved;` with the comment "a CatchLedger reservation is held for Roll.Catch.ItemIndex; settled exactly once (Resolve or Disconnect)".

**`CatchValidator.ValidateCast` (edit; signature unchanged):**
- New order: `CastingAllowed` (NotAllowedNow), ActiveCast, StaleCastId, BadCharge, OriginTooFar (origin), OriginTooFar (non-finite yaw), NoBait, `CastMath.ProbeReason`, then `!view.GridWater` -> `NotWater` (M3-D16).
- Implement it as `r = ProbeReason(...); if (r != None) return r; return view.GridWater ? None : NotWater;`.
- Update the header comment.

**`FishSpeciesData` / `FishSpeciesDef` (edit, M3-D3):**
- Remove `MinBaitRating` and `MinDepth`: the fields, getters, `Configure` parameters and `ToCore` lines.
- T3's appended `tags` stays the last `Configure` parameter before `model` (or wherever T3 put it); only the two parameters go.
- Fix every caller listed in CCR-D1.

**`FishingValidators` (edit):**
- `CheckSpecies` loses the two `Range` rows and the "reachable with any bait at depth <= 6 m" block. The `baits` parameter goes too if it is now unused.
- V3 (T11) is the reachability check now.

**`FishingSeedData` / `FishingContentBuilder` (edit):** drop the two values from `MakeSpecies` (or whatever T10 left of it) and from the species `Configure` call.

**`FishingHostLedger` (edit, C 7.13): code below.**
- Fields:
  - `_eco` (`EcologyDatabase`);
  - `_db = _eco.Fishing` (the existing `FishingDatabase` uses stay as they are);
  - `_resolver = new FishingResolver(_eco)`, created once.
- `RunSeed` and `HostCastCounter` become forwarders to `Run`, so the M2 readers (`DevOverlayPresenter`, `FishingHost`) compile unchanged.

```csharp
// Core/Fishing/FishingHostLedger.cs (T13). Class comment gains: "M3: rolls through FishingRoller/FishingResolver with the host context
// (view.Context world part; rod tier, bait and depth overwritten here, M3-D15); the cast counter and catch limits live in the session's
// EcologyRunState (M3-D17); limited species are reserved at roll and committed or released on the first resolution (M3-D14)."

private readonly EcologyDatabase _eco;
private readonly FishingDatabase _db;          // == _eco.Fishing; every M2 use of _db is unchanged
private readonly FishingResolver _resolver;

public FishingHostLedger(EcologyDatabase db, EcologyRunState run, ushort defaultRodIndex, byte defaultBaitIndex)
{
    _eco = db ?? throw new ArgumentNullException(nameof(db));
    Run = run ?? throw new ArgumentNullException(nameof(run));
    _db = db.Fishing;
    if (defaultRodIndex >= _db.Rods.Count) throw new ArgumentOutOfRangeException(nameof(defaultRodIndex));
    if (defaultBaitIndex >= _db.Baits.Count) throw new ArgumentOutOfRangeException(nameof(defaultBaitIndex));
    _resolver = new FishingResolver(db);
    _defaultRod = defaultRodIndex;
    _defaultBait = defaultBaitIndex;
}

public EcologyRunState Run { get; }
public ulong RunSeed => Run.RunSeed;
public uint HostCastCounter => Run.HostCastCounter;

public CastVerdict RequestCast(ulong clientId, byte slot, in CastRequestData req, in HostCastView view, in RollOverrides overrides)
{
    if (!_slots.TryGetValue(slot, out SlotState s))
        return new CastVerdict { Valid = false, Reason = CastRejectReason.UnknownPlayer };
    ClientState c = GetOrAddClient(clientId, slot);
    ref readonly FishingTuning t = ref _db.Tuning;

    bool baitAvailable = req.BaitIndex < _db.Baits.Count
                         && (_db.Baits[req.BaitIndex].Unlimited || s.Stock[req.BaitIndex] > 0);
    CastRejectReason reason = CatchValidator.ValidateCast(in req, c.LastAccepted, c.HasActive, in view, baitAvailable, t.MinLandDepth);
    if (reason != CastRejectReason.None)
        return new CastVerdict { Valid = false, Reason = reason };

    // M3-D15: the world part is the host's (view.Context); rod tier, bait and depth are the ledger's own truth, never the owner's.
    // "Slot bait" (C 7.13) is the bait this cast validated and consumes: req.BaitIndex (checked against the slot stock above).
    FishingContext ctx = view.Context;
    ctx.RodTier = _db.Rods[s.RodIndex].Tier;
    ctx.Bait = _db.Baits[req.BaitIndex].Mods(1);
    ctx.Depth = view.Probe.Depth;

    // Roll first, counter assigned after (D-F2): a bad override, an invalid context or an out-of-range zone throws before any state changes.
    uint counter = Run.HostCastCounter + 1;
    Pcg32 rng = Rng.Stream(Run.RunSeed, FishingConstants.CatchStreamId, counter);
    FishingRoll roll = FishingRoller.Roll(_eco, _resolver, in ctx, Run.Catches, in overrides, req.BaitIndex, ref rng, out int ruleIndex);
    bool reserve = LimitApplies(ctx.ZoneIndex, ruleIndex, in roll);

    roll.Pity = t.PityAfterFailures > 0 && s.FailStreak >= t.PityAfterFailures;   // R65 unchanged: no extra draw
    bool announcePity = roll.Pity && !s.PityAnnounced;
    if (roll.Pity) s.PityAnnounced = true;

    float distance = CastMath.Distance(req.ChargeNorm, t.CastMinDist, _db.Rods[s.RodIndex].CastMaxDist);
    CastMath.Landing(req.OriginX, req.OriginZ, req.YawDeg, distance, out float lx, out float lz);

    // ---- state changes from here on (nothing below throws) ----
    if (!_db.Baits[req.BaitIndex].Unlimited) s.Stock[req.BaitIndex]--;
    Run.HostCastCounter = counter;
    if (reserve) Run.Catches.Reserve(roll.Catch.ItemIndex);   // the next roll of ANY client now sees Caught + Reserved >= limit
    c.Slot = slot;
    c.LastAccepted = req.CastId;
    c.HasActive = true;
    c.Active = new ActiveCast
    {
        CastId = req.CastId, Roll = roll, Phase = CastPhase.Waiting, TAccepted = Now, THooked = 0.0,
        LineLength0 = 0f, TFlight = CastMath.FlightTime(distance), CastDistance = distance, Assist = req.Assist,
        RodIndex = s.RodIndex, BaitIndex = req.BaitIndex, LandingX = lx, LandingZ = lz, LimitReserved = reserve,
    };
    return new CastVerdict { Valid = true, Reason = CastRejectReason.None, Roll = roll, LandingX = lx, LandingZ = lz, AnnouncePity = announcePity };
}

// A limit applies only to a fish rolled THROUGH a rule whose CatchLimitPerRun >= 0. Junk and forced rolls (ruleIndex -1: debug overrides
// bypass gates and limits, M3-D13) never reserve. Never re-derive the limit from the species: Commit/Release on an unreserved index throw.
private bool LimitApplies(int zoneIndex, int ruleIndex, in FishingRoll roll)
{
    if (ruleIndex < 0 || roll.Catch.IsJunk) return false;
    SpawnRuleData rule = _eco.Zones[zoneIndex].Rules[ruleIndex];
    return rule.CatchLimitPerRun >= 0 && rule.SpeciesIndex == roll.Catch.ItemIndex;
}

// M3-D14: the first resolution settles the reservation exactly once. Landed (which also covers the ReleasedCreelFull announcement:
// the outcome stays Landed) commits; every other outcome (Cancelled, Snapped, Escaped incl. Invalid, Missed, Timeout) releases.
private void SettleLimit(in ActiveCast a, bool landed)
{
    if (!a.LimitReserved) return;
    if (landed) Run.Catches.Commit(a.Roll.Catch.ItemIndex);
    else Run.Catches.Release(a.Roll.Catch.ItemIndex);
}

private void Resolve(ClientState c, FightOutcome outcome, EscapeReason reason)
{
    ActiveCast a = c.Active;
    SettleLimit(in a, outcome == FightOutcome.Landed);    // first line: every first resolution passes here (D-F1)
    // ... the existing R65 FailStreak bookkeeping, resolution history and clear, unchanged ...
}

public bool Disconnect(ulong clientId)
{
    if (!_clients.TryGetValue(clientId, out ClientState c)) return false;
    if (c.HasActive) SettleLimit(in c.Active, false);     // the one clear that bypasses Resolve (D-F1): release, never commit
    _clients.Remove(clientId);
    int at = _clientOrder.BinarySearch(clientId);
    if (at >= 0) _clientOrder.RemoveAt(at);
    return c.HasActive;
}
```
- `Result` sets `d.Roll.Catch` (the clean bonus) on the decision only. `SettleLimit` reads `c.Active.Roll.Catch.ItemIndex`, which is the same species index.
- Orphans never touch `Run.Catches`.
- `ResetSlot` touches no client state, so it never settles anything.
- The single host thread is the only concurrency. "Two concurrent casts" means two clients with casts in flight. The first accepted roll reserves, and the second roll's gate sees `Reserved` (C 7.5 `AtLimit = Caught + Reserved >= limit`).

**`EcologyTestData` (new, `Tests/EditMode/Ecology/EcologyTestData.cs`, `internal static`):**
- It is the shared M3 fixture (C 7.5 note, M3-D54).
- It never edits `FishingTestData`'s values. It reads rods, tuning, motion profiles and junk from whatever `FishingTestData` exposes after R72.

| Member | Content |
|---|---|
| `const int SpeciesSpecial = 5` | appended after `FishingTestData`'s five species (their constants stay valid) |
| `const int ZoneLake = 0, ZoneLimited = 1, ZoneSpecialOnly = 2`; `const byte SubShallow = 0, SubMid = 1, SubFar = 2` | indices |
| `BaitStats[] Baits()` | `FishingTestData.Baits()` copied in the same order, with explicit `AffinityTags = None, AffinityMult = 1, SpecialMult = 1` (M3-D54); it never returns the `FishingTestData` instances unchanged |
| `FishSpeciesData[] Species()` | `FishingTestData.Species()` + one special: `IsSpecial true, Rarity Legendary, FightProfileIndex = ProfileBulldog, 2000-6000 g, value 60, Tags None` |
| `ZoneData LakeZone()` | bands [0,5) / [5,10) / [10,+inf) with junk 0.20 / 0.12 / 0.10; one rule per regular species 0..4 (all masks, weight 1, rod 0, bait 0, depth 0, limit -1, players 0); the special rule (`Special true, SpecialChance 0.02, CatchLimitPerRun 1`, all masks); junk {0: 1, 1: 1}; weather table = spec 12.2 matrix; grid null |
| `ZoneData LimitedZone()` | one band [0, +inf) with **junkChance 0**; one rule: carp (`FishingTestData.SpeciesCarp`), weight 1, `CatchLimitPerRun 1`, all masks; junk {0: 1}. Deterministic: every cast rolls carp until the limit, then the empty pool gives junk |
| `EcologyDatabase Database()` | `new FishingDatabase(FishingTestData.Rods(), Baits(), <motion profiles>, Species(), FishingTestData.Junk(), FishingTestData.Tuning())`; zones {Lake, Limited, SpecialOnly}; spots {`ZoneIndex 0, MapOrder 0`}; tuning 60/120/60/120/45/60, radius 3 |
| `FishingContext Context(int zone = ZoneLake, byte subArea = SubMid, Season season = Summer, WeatherMask weather = Clear, int minute = 300, bool inHotspot = false, int players = 1)` | `Depth +Infinity, RodTier 0, Bait = BaitMods.Neutral(1), Luck 0, Events = EventMods.Neutral, Skills = SkillMods.Neutral` (the ledger overwrites rod tier, bait and depth) |
| `HostCastView View(float depth = 3f, bool water = true, bool hasBed = true, bool allowed = true, bool gridWater = true)` and an overload taking a `FishingContext` | player at the origin; `Context = Context()` unless given |
| `FishingHostLedger Ledger(EcologyDatabase db = null, EcologyRunState run = null, ulong seed = 42)` | handmade rod, worm bait; `EnsureSlot(0)` **only**. Kept tests such as `..._UnknownSlot_RejectedUnknownPlayer` and the `ResetSlot` tests treat other slots as unknown. Tests that need slot 1 call `EnsureSlot(1)` themselves. |
| `ZoneData SpecialOnlyZone()` (`ZoneSpecialOnly = 2`, also in `Database()`) | one band [0, +inf) with junkChance 0; one rule: the special (`Special true, SpecialChance 0.5, CatchLimitPerRun 1`, all masks); no regular rule, so a failed special roll falls through the empty pool to junk |

**Interim `FishingHost` (T13 compile fix, C 7.13; T15 replaces it):**
- `EnsureLedger`:
  - `eco = _registry.Ecology`. When it is null, `LogError` once ("no ecology database: run FishGame > Build All") and return false.
  - `_interimRun ??= new EcologyRunState(_runSeed)`.
  - `_interimZone = _registry.ZoneIndex(ContentIds.LakeDock)`, inside the existing try/catch.
  - `Ledger = new FishingHostLedger(eco, _interimRun, rod, bait)`.
- `SetRunSeed`: the existing counter check (now forwarded to the run). Set `_interimRun = null` before the rebuild.
- `HandleCastRequest`: the view gets `CastingAllowed = true, GridWater = true, Context = InterimContext(_interimZone)`.
- `internal static FishingContext InterimContext(int lakeZoneIndex)` returns:
  - `ZoneIndex = lakeZoneIndex, SubAreaIndex = 1` (open_water);
  - `Depth = +Infinity, Minute = 300, Season = Summer, Weather = Clear`;
  - `RodTier 0, Bait = BaitMods.Neutral(1), InHotspot false, Luck 0`;
  - `Events/Skills Neutral, PlayersInSession 1`.

### Steps (TDD, C 2.4)
1. **Fixture first.** Write `EcologyTestData.cs`. Rewrite the `FishingHostLedgerTests` helpers on top of it:
   - `View()` and `Request()` use `EcologyTestData.View()`, so `GridWater` is true and the context is valid.
   - `NewLedger(...)` uses `EcologyTestData.Ledger`.
   - `Expected(...)` replays the ledger: same `ctx` (with the overwrites), `Rng.Stream(seed, "catch", n)`, then `FishingRoller.Roll(..., RollOverrides, worm, ...)`.
2. Add the new ledger and validator tests below. Run `test-edit -Filter "FishGame.Tests.EditMode.(FishingHostLedgerTests|CatchValidatorTests)"`. Red is the compile error on the new ctor, `GridWater` and `Context`.
3. Edit `HostCastView`, `ActiveCast`, `CatchValidator` and the ledger. Re-run the same filter until it is green.
4. Do the removals: `git rm` the roller, the request and their test. Remove the species fields. Fix the CCR-D1 files, the builders and the validators. Run `compile`, then `test-edit -Filter "FishGame.Tests.EditMode.(Fishing|Ecology|CatchValidator)"`.
5. Make the interim `FishingHost` edit. Run `build` (validation must pass with the shortened species checks), then restore the scene churn.
6. Run the full `test-edit` and then `test-play` (both in the background). The M2 PlayMode cast tests are the proof that the interim context lands.
7. Grep checks (paste the output in the report):
   - `grep -rn "PlaceholderRoller\|RollRequest" Assets/_Project --include=*.cs` returns nothing.
   - `grep -rn "MinBaitRating\|MinDepth\|minBaitRating\|minDepth" Assets/_Project --include=*.cs`: every hit is on `SpawnRule`/`SpawnRuleData`/the resolver/T11 validators, never on a species type.

### Tests
**`FishingHostLedgerTests.cs` (rewrite against `EcologyTestData`).**
- Every C2 test name in the file is kept. The roll-content tests (`..._Accept_ConsumesBaitAndRolls`, `..._RollSeed_DerivesFromRunSeedAndCounter`, the pity tests) compare against `Expected(...)` through `FishingRoller`.
- `..._CastingNotAllowed_RejectedBeforeEverythingElse` and `..._InvalidWater_Rejected` keep their cases. Their views now set `GridWater = true`.

New tests:

| Test | Cases | Mutation it catches |
|---|---|---|
| `FishingHostLedger_Roll_UsesResolverWithHostContext` | (a) Lake ctx, no overrides: 20 accepted casts equal `Expected` (`AssertSameRoll`). (b) A ctx with `Season = Winter` on a zone variant whose only regular rule is Summer-only: 50 casts, never that species. | The ledger ignoring `view.Context` (building its own); the M2 roller kept. |
| `FishingHostLedger_HostRodTierAndBait_OverrideContext` | Zone variant with a rule `minRodTier 2` (species carp) and a rule `minDepth 5` (species trout). The view context is forged: `RodTier 3`, `Bait = BaitMods.Neutral(10)`, `Depth +Infinity`. Handmade rod (tier 1), worm, probe depth 3. Over 100 casts neither carp nor trout is ever rolled. Then `SelectRod(carbon)` (tier 2): carp appears within 100 casts. | Trusting `ctx.RodTier`, `Bait` or `Depth` from the view. |
| `FishingHostLedger_GridNotWater_RejectedNotWater` | Valid probe, `GridWater = false`: Reason `NotWater`, stock unchanged, `Run.HostCastCounter` unchanged, no active cast. | The grid check missing, or placed before NoBait (a no-bait + grid-false view must say `NoBait`). |
| `FishingHostLedger_DayOver_RejectedNotAllowedNow` | `CastingAllowed = false` with an otherwise valid view: `NotAllowedNow`. With `GridWater = false` and stock 0 too: still `NotAllowedNow`. No state change. | The casting check moved after the grid or bait checks. |
| `FishingHostLedger_CastCounter_LivesInRunState` | Ledger A on run R: 2 accepted casts. Ledger B (new instance) on the same R: the next accepted cast equals `Expected(seed, 3)`. `R.HostCastCounter == 3`, `B.HostCastCounter == 3`. | The counter kept per ledger (a map switch would replay seeds, B11). |
| `FishingHostLedger_LimitedSpecies_ReservedThenCommittedOnLanded` | `ZoneLimited` ctx. Client 100 / slot 0 casts and gets carp: `Reserved(carp) == 1, Caught == 0`. Client 200 / slot 1 casts: junk (limit reached by the reservation; the two-concurrent-casts acceptance). Client 100 hooks and lands validly: `Caught 1, Reserved 0`. Client 200 cancels, then casts again: junk. Second case (TestCase): the full creel (`CreelSnapshot` full) gives `Announce == ReleasedCreelFull` and still commits. Third case (TestCase, the skeleton's "limit-1 special"): on `ZoneSpecialOnly` with seed 42, client 100 casts and cancels (junk) until a cast rolls the special (bounded to 64 casts; the special tier's `ruleIndex` path). Hold that cast. Client 200 then makes 32 casts, all junk. Then client 100 lands: `Caught 1`, and client 200 still gets junk. | Reserving after the state changes (the second roll sees no reservation); committing only on `AddToCreel`; `LimitApplies` mishandling a special-tier `ruleIndex`. | Reserving after the state changes (the second roll sees no reservation); committing only on `AddToCreel`. |
| `FishingHostLedger_LimitedSpecies_ReleasedOnLossCancelAndDisconnect` | TestCase per path: `CancelWaiting`, `CancelHooked` (Dropped), `Snapped`, `EscapedPinned`, `Missed`, `EarlyHook` (Invalid), `LandedTooFast` (Invalid), `StuckWaitingTimeout`, `HookedTimeout`, `HookedOwnerTimeoutAccepted`, `HookedOwnerTimeoutTooEarly`, `Disconnect`. After each: `Reserved 0, Caught 0`, and the next cast (any client) rolls carp again. **R72 owner-Timeout cases (final review M8):** the owner's FightSim now ends a fish fight with `Escaped(Timeout)` at 120 s, and the host accepts it only on its own clock (`CatchValidator.OwnerTimeoutPlausible`, `HostTimeoutSlackSeconds` 2 s). `HookedOwnerTimeoutAccepted`: the owner's Timeout result at `tHooked + 118 s` or later (also at exactly 118.0 s and at 120 s) is accepted, resolves Escaped(Timeout) and releases the reservation. `HookedOwnerTimeoutTooEarly`: the same result below 118 s (for example 117.99 s) is refused and resolves Escaped(Invalid), and it still releases the reservation. `HookedTimeout` stays the host's own maxFightTime path (`FishingHostLedger_HookedPastMaxFightTime_TimesOutEscapedTimeout`). **`EscapedPinned` note:** R72 removed the pinned escape, so the owner never sends `Escaped(Pinned)` any more. Keep the case only as a host-side check that a (forged or legacy) Pinned reason still releases and never commits, and do not treat it as a path that occurs in play. | A commit on any non-Landed outcome; no release in `Disconnect`; a double settle (`Release` on an unreserved index throws); an owner Timeout accepted before 118 s, or refused after it, or left holding the reservation. |
| `FishingHostLedger_RollSeed_DerivesFromRunSeedAndCounter` | Kept name. The seed comes from `Run.RunSeed` and the counter from `Run.HostCastCounter`. | - |
| `FishingHostLedger_ForcedSpecies_NeverReserves` (extra) | `ZoneLimited`, `Ov` forcing carp: three casts all carp, `Reserved` stays 0, none throw on resolution. | Reserving by species instead of by `ruleIndex`. |
| `FishingHostLedger_InterimLakeContext_BluegillEligible` (extra, T15 deletes it) | Load the real registry the way `FishingContentTests` does and call `Build()`. `ctx = FishingHost.InterimContext(registry.ZoneIndex("lake_dock"))` with `Bait = worm Mods(1)` and `Depth 1.5`. `new FishingResolver(registry.Ecology).CollectEligible(ctx, null, false, list)` contains `SpeciesIndex("bluegill")`. | A seed or zone mismatch: this test catches it instead of a flaky PlayMode run. |

- Keep `FishingHostLedger_Tick_OrderSurvivesDisconnectAndRejoin_AndDoesNotAllocate` green: its casts are unlimited, so no reservation.
- Add one case to the limited-release test that calls `Tick` for the timeout paths after a warm-up tick. Assert that no GC allocation happens (same technique as that test).

**`CatchValidatorTests.cs` (add):**
- `GoodView()` sets `GridWater = true` and a valid context.
- `CatchValidator_ValidateCast_GridCheckAfterProbe`:
  - `GridWater false` + probe TooShallow -> `TooShallow`;
  - `GridWater false` + probe not water -> `NotWater`;
  - `GridWater false` + valid probe -> `NotWater`;
  - `GridWater false` + no bait -> `NoBait`;
  - `GridWater true` + valid -> `None`.
  - Mutation: the grid check placed first.
- `CatchValidator_ValidateCast_CastingNotAllowedFirst`: `CastingAllowed false` with active cast, stale id, bad charge, far origin, no bait, bad probe and `GridWater false` all set -> `NotAllowedNow`.
- `CatchValidator_ValidateCast_RuleOrderIsFixed` (existing) gains a last step: `GridWater = false` -> `NotWater`.

**Re-pointed (CCR-D1):**
- `FishingRollValidation_SaneRoll_Passes`: 1000 rolls over `EcologyTestData.Database()`, contexts cycling the sub-areas and seasons. All are sane, and at least one is junk.
- `FishingRollValidation_OutOfRangeFields_Rejected`: forced fish and forced junk through `FishingRoller` overrides.
- The three `FishingRollNet_*` wire tests use `FishingRoller.Roll` with `new CatchLedger()`.

### Acceptance (reviewer checks)
- `RequestCast` makes no state change before the roll returns. `Reserve` comes after the counter assignment and before `ActiveCast` is stored.
- `SettleLimit` is called from exactly two places: the first line of `Resolve`, and `Disconnect`.
- No reservation for junk or a forced roll.
- `ctx.RodTier`, `ctx.Bait` and `ctx.Depth` are always overwritten from the host slot, `req.BaitIndex` and `view.Probe`.
- `ValidateCast` order matches C 7.13. The new tests prove "grid after probe" and "casting first".
- Every `HostCastView` construction in the repo sets `GridWater` and `Context` (grep `new HostCastView`).
- The interim `FishingHost` compiles. `test-play` is green, with no new warnings.
- The CCR-D1 edits are mechanical. `FishingTestData` lost only the two initializers (diff shown in the report).
- No R72 line was changed (the reviewer diffs `FishingHostLedger.Result` and `CatchValidator` fight helpers against `HEAD~`).
- Both grep outputs are in the report.
- `test-edit` and `test-play` PASS.

### Commit
```powershell
git rm Assets/_Project/Core/Fishing/PlaceholderRoller.cs Assets/_Project/Core/Fishing/PlaceholderRoller.cs.meta Assets/_Project/Core/Fishing/RollRequest.cs Assets/_Project/Core/Fishing/RollRequest.cs.meta Assets/_Project/Tests/EditMode/PlaceholderRollerTests.cs Assets/_Project/Tests/EditMode/PlaceholderRollerTests.cs.meta
git add Assets/_Project/Core/Fishing/FishingHostLedger.cs Assets/_Project/Core/Fishing/CatchValidator.cs Assets/_Project/Core/Fishing/HostCastView.cs Assets/_Project/Core/Fishing/ActiveCast.cs Assets/_Project/Core/Fishing/FishSpeciesData.cs Assets/_Project/Core/Fishing/BitePlanner.cs Assets/_Project/Core/Fishing/RollOverrides.cs Assets/_Project/Core/Fishing/FishingRoll.cs Assets/_Project/Data/Fishing/FishSpeciesDef.cs Assets/_Project/Editor/Validation/FishingValidators.cs Assets/_Project/Editor/Builders/Fishing/FishingSeedData.cs Assets/_Project/Editor/Builders/Fishing/FishingContentBuilder.cs <the CCR-D1 Ecology builder files actually edited> Assets/_Project/Fishing/Host/FishingHost.cs Assets/_Project/Tests/EditMode/Ecology/EcologyTestData.cs Assets/_Project/Tests/EditMode/Ecology/EcologyTestData.cs.meta Assets/_Project/Tests/EditMode/FishingHostLedgerTests.cs Assets/_Project/Tests/EditMode/CatchValidatorTests.cs Assets/_Project/Tests/EditMode/FishingTestData.cs Assets/_Project/Tests/EditMode/FishingDefsTests.cs Assets/_Project/Tests/EditMode/FishingValidatorTests.cs Assets/_Project/Tests/EditMode/FishingContentTests.cs Assets/_Project/Tests/EditMode/FishingRollValidationTests.cs Assets/_Project/Tests/EditMode/FishingWireTests.cs
git commit -m "m3(ledger): host roll on FishingResolver, grid/day-over cast checks, catch-limit reservations, run-state cast counter; PlaceholderRoller removed" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
If `Tests/EditMode/Ecology/` is a new folder, also add `Assets/_Project/Tests/EditMode/Ecology.meta`.

---

## T14 - World state, wire, dev world driver

**Goal:**
- `ClockAnchor` and `HotspotState` wire structs.
- `WorldState`: the in-scene NetworkBehaviour with four NetworkVariables, implementing `IWorldContext`.
- `DevWorldDriver`: host dev clock, weather track, hotspot schedule, pause, scrub, auto new day, map-switch resume.
- `EcologySession`: the session-level run state.
- `ProtocolVersion = 4` (R72 took 3).

**Owned files (C 12 + CCR-D4):**
- `Gameplay/World/{ClockAnchor,HotspotState,WorldState,DevWorldDriver,EcologySession,DevClock}.cs`;
- `Core/Versioning/BuildInfo.cs`;
- no `Gameplay/AssemblyInfo.cs` edit: T2 already added `[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]` (CCR-A2 accepted 2026-10-12; the PlayMode line and `AssemblyMarker` stay). If the line is missing, T2 has not landed: stop and report;
- tests `Tests/EditMode/{WorldWireTests,BuildInfoTests,DevWorldTests}.cs`.

**Shared edits (C 2.6):** `BuildInfo.cs` + `BuildInfoTests.cs` (T14 only).
**Depends on:** T7 (`WeatherDirector`, `HotspotPlanner`, `HotspotData`), T10 (zones, weather table, tuning in the registry). T5 through them.
**Implementer:** Sonnet. **Reviewer:** Opus high.
**Constraints that matter most (C 2.8):**
- #23: host-authoritative; clients compute only from NetworkVariables.
- #24: `WorldState` is in-scene. T14 places nothing; T16 does. But the field name and init hook must suit T16 (CCR-D5).
- #25: no new random stream here (season, weather and hotspot streams belong to T7).
- C2 #4: hashes.
- C2 #9: warning-free.
- C2 #10: late joiners get everything from the NetworkVariables.
- C2 #16: no `UnityEngine.Random`.
- R34: pause writes `Running = false`; the rate stays positive and `Time.timeScale` is never touched.
- **Each T14 class opens with its authority comment** (texts below).

### Behaviour

**`ClockAnchor` (`Gameplay/World/ClockAnchor.cs`):**
- Authority comment: "none (wire data for WorldState.NetClock)".
- Fields are exactly C 7.14: `double AnchorServerTime; float AnchorMinute; float MinutesPerSecond; byte Running`. Unmanaged, `INetworkSerializeByMemcpy`, never `INetworkSerializable` (C2 G1).
- `From`: `Running = d.Running ? 1 : 0`. `ToCore`: `Running != 0`.
- `Equals` compares the four fields with `double.Equals`/`float.Equals`, so NaN equals NaN and a NaN can never cause endless re-sends (N3 in the M2 net plan: NGO uses `IEquatable`).
- `GetHashCode` is an unchecked multiply-add over the four.

**`HotspotState`:**
- Authority comment: "none (wire data for WorldState.NetHotspot)".
- Fields are C 7.14. `From(in HotspotData)` copies `X, Z, Radius, EndMinute`, with `Active ? 1 : 0`.
- `ToCore` returns `StartMinute = 0` (not replicated).
- Equality and hash work like `ClockAnchor`.

**`EcologySession` (code):**
```csharp
// Gameplay/World/EcologySession.cs
// Authority: host only (host memory, never sent). Session-level holder of the EcologyRunState (seed, season, day, cast counter, catch
// ledger; M3-D17) that survives Spot loads. Cleared when the session ends (INetworkSession.Ended, subscribed here) and on SubsystemRegistration.
// Ruling CCR-D3: GetOrCreate returns the existing run state and ignores the seed; a dev seed change is Clear() + GetOrCreate(seed).
public static class EcologySession
{
    private static EcologyRunState s_current;
    private static INetworkSession s_session;   // the session whose Ended we listen to (null in EditMode: ServiceRoots.App is null)

    public static EcologyRunState Current => s_current;

    public static EcologyRunState GetOrCreate(ulong runSeed)
    {
        // Stale guard: SubsystemRegistration runs once per Play Mode entry, not per PlayMode test. If a session ended without Ended
        // reaching us (App destroyed first), the run state belongs to a dead session: drop it.
        if (s_current != null && !ReferenceEquals(s_session, CurrentAppSession())) Clear();
        if (s_current != null) return s_current;
        var run = new EcologyRunState(runSeed);
        run.Season = WeatherDirector.RollSeason(runSeed);   // M3-D10: rolled once per session from the run seed; Day starts at 1 (T5)
        s_current = run;
        HookSessionEnd();
        return run;
    }

    public static void Clear()
    {
        s_current = null;
        if (s_session != null) { s_session.Ended -= HandleEnded; s_session = null; }
    }

    private static void HookSessionEnd()
    {
        if (s_session != null) return;
        INetworkSession session = CurrentAppSession();
        if (session != null) { s_session = session; s_session.Ended += HandleEnded; }
    }

    // null in EditMode (no App) - then s_session is null too and the stale guard matches.
    private static INetworkSession CurrentAppSession()
    {
        ServiceRegistry app = ServiceRoots.App;
        return app != null && app.TryGet(out INetworkSession session) ? session : null;
    }

    private static void HandleEnded(EndReason reason) { Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_current = null; s_session = null; }
}
```

**`WorldState` (`Gameplay/World/WorldState.cs`).**

Authority comment: C 7.14 verbatim, plus: "Registers as IWorldContext with ServiceRoots.Game.Replace on spawn (no remove API, CCR-D2): after despawn HasState is false and Current is null. Spawn on the server calls the sibling DevWorldDriver.ServerInitialize synchronously, so the first sync never carries default values."

- **Serialized:** `[SerializeField] private string spotId;` (the name is fixed for T16, CCR-D5). `SpotId => spotId`.
- **NetworkVariables** are readonly field initialisers, all `Everyone / Server`:
  - `NetSeason` default `(byte)Season.Spring`;
  - `NetWeather` default `(byte)WeatherMask.Clear`;
  - `NetClock` default (rate 0, so `HasState` is false until written);
  - `NetHotspot` default (inactive).
- **Statics:** `Current` (reset on `SubsystemRegistration`).
- **Derived reads:**
  - `HasState => IsSpawned && _spawnedHere && NetClock.Value.MinutesPerSecond > 0f`. `DevClock` never writes rate 0 (R34: pause is `Running = false`), so a positive rate means "the host has written the anchor".
  - `MinuteF => HasState ? ClockMath.MinuteAt(NetClock.Value.ToCore(), NetworkManager.ServerTime.Time) : 0f`.
  - `Minute`: floor of `MinuteF`, clamped to 0..1199 (for band and segment lookups).
  - `CastingOpen => HasState && MinuteF < DayClock.DayLengthMinutes` (the exact value, never `Minute`; M3-D53).
  - `Season => (Season)Math.Min(NetSeason.Value, (byte)3)`. `Weather`: `HasState ? (WeatherMask)NetWeather.Value : WeatherMask.Clear`; a stored `None` reads as `Clear`.
  - `IsInHotspot(x, z) => HasState && HotspotPlanner.Contains(NetHotspot.Value.ToCore(), x, z)`. The host clears expired hotspots through `ServerSetHotspot`. A client may show a stale one for a frame; only the host's answer enters a roll.
- **Events:**
  - `Changed` is raised from `NetSeason`/`NetWeather`/`NetHotspot.OnValueChanged` on every peer, the writer included. It is also raised once at the end of `OnNetworkSpawn` and once in `OnNetworkDespawn`.
  - `ClockChanged` is raised from `NetClock.OnValueChanged`, and once at spawn.
  - All are raised through `EventIsolation.Invoke`.
- **`OnNetworkSpawn` order:**
  1. If `Current` is another instance, `LogError` ("two WorldState objects in one scene") and keep the first.
  2. `Current = this`.
  3. If `IsServer`: `GetComponent<DevWorldDriver>()`. If it is non-null, call `ServerInitialize(this)`, which writes all four values (D-F4). Else `LogError` once ("World has no DevWorldDriver; the clock never starts").
  4. Subscribe the four `OnValueChanged`. They are subscribed after the init writes, so the host does not raise `Changed` three times while `HasState` is still false; step 7 covers every consumer.
  5. `_spawnedHere = true`.
  6. `ServiceRoots.Game?.Replace<IWorldContext>(this)` (explicit null check on the registry).
  7. Raise `Changed` and `ClockChanged`. Consumers that enabled before the spawn refresh now (M2 net plan N5: seed from `.Value`; never wait for a first `OnValueChanged`).
- **`OnNetworkDespawn`:** unsubscribe; `_spawnedHere = false`; clear `Current` if it is this; raise `Changed`.
- **Server writes:**
  - Each method returns with a `LogOnce` warning when `!IsServer || !IsSpawned`. Each writes only when the value differs.
  - `ServerSetWeather` ignores `None` (logs once). Masks with several bits are accepted (M5 Storm|Rain).
  - `ServerSetClock` ignores an anchor with a non-finite or non-positive rate (logs once): the `HasState` invariant.

**`DevClock` and `DevWorldRules` (`Gameplay/World/DevClock.cs`, internal, pure; code).**

They hold the re-anchoring decisions so EditMode can prove them: nothing places a `WorldState` before T16, so T14 cannot PlayMode-test the clock.
```csharp
// Authority: host only (a plain helper owned by DevWorldDriver). Pure: no Unity calls; time comes in as NetworkTime seconds.
// Three independent inputs: dev-running (panel/test), paused (solo IPauseState), scrub (F8 held). Effective running = devRunning && !paused;
// the scrub sets the rate. Every change re-anchors through ClockMath.Reanchor so the minute is continuous (14.2). The rate is never 0 (R34).
internal sealed class DevClock
{
    internal const float ScrubMultiplier = 60f;          // M3-D32: F8 held = x60
    private readonly float _baseRate;

    public DevClock(float baseRate)
    {
        if (!(baseRate > 0f) || float.IsInfinity(baseRate)) throw new ArgumentOutOfRangeException(nameof(baseRate));
        _baseRate = baseRate;
    }

    public ClockAnchorData Anchor { get; private set; }
    public bool DevRunning { get; private set; } = true;
    public bool Paused { get; private set; }
    public bool Scrub { get; private set; }
    public bool EffectiveRunning => DevRunning && !Paused;
    public float EffectiveRate => Scrub ? _baseRate * ScrubMultiplier : _baseRate;

    public float MinuteAt(double now) => ClockMath.MinuteAt(Anchor, now);

    // Session start, map-switch resume and new day: minute m now; keeps DevRunning/Paused/Scrub as they are.
    public void StartAt(double now, float minute) { Anchor = ClockMath.At(now, ClampMinute(minute), EffectiveRate, EffectiveRunning); }

    public bool SetPaused(double now, bool paused)  { if (Paused == paused) return false;      Paused = paused;      return Reanchor(now); }
    public bool SetRunning(double now, bool running) { if (DevRunning == running) return false; DevRunning = running; return Reanchor(now); }
    public bool SetScrub(double now, bool held)      { if (Scrub == held) return false;          Scrub = held;         return Reanchor(now); }

    // A running clock that reached 1200 starts the next day (M3-D11). A clock stopped at 1200 stays there (casting closed).
    public bool ShouldAutoNewDay(double now) => EffectiveRunning && MinuteAt(now) >= DayClock.DayLengthMinutes;

    // True when the anchor on the wire must change. A toggle that leaves the clock stopped before and after (scrub while paused,
    // pause while dev-stopped) writes nothing: the frozen minute is identical and the next start re-anchors with the right rate.
    private bool Reanchor(double now)
    {
        ClockAnchorData next = ClockMath.Reanchor(Anchor, now, EffectiveRate, EffectiveRunning);
        if (!next.Running && !Anchor.Running) return false;
        Anchor = next;
        return true;
    }

    internal static float ClampMinute(float m) => float.IsNaN(m) ? 0f : (m < 0f ? 0f : (m > DayClock.DayLengthMinutes ? DayClock.DayLengthMinutes : m));
}

internal static class DevWorldRules
{
    // M3 override values: None (follow the track) or exactly Clear, Rain or Fog (the dev panel never sets Storm/Heatwave in M3).
    public static bool IsValidOverride(WeatherMask w) => w == WeatherMask.None || w == WeatherMask.Clear || w == WeatherMask.Rain || w == WeatherMask.Fog;
    public static int LookupMinute(float minuteF) { int m = (int)Math.Floor(minuteF); return m < 0 ? 0 : (m > DayClock.DayLengthMinutes - 1 ? DayClock.DayLengthMinutes - 1 : m); }
}
```
- `StartAt` uses the current `EffectiveRunning`. When the driver adopts a run state, it first sets `DevRunning = true` through `SetRunning`. That gives "running at session start".

**`DevWorldDriver` (`Gameplay/World/DevWorldDriver.cs`; code for the re-anchoring paths):**
```csharp
// Authority: HOST ONLY (inert on clients: ServerInitialize runs only from WorldState.OnNetworkSpawn on the server, and Update returns
// unless the sibling WorldState is spawned as server). M3 dev clock, weather track and hotspot director on the scene root "World"
// (C 6.3); reads and writes the session run state (EcologySession). M4's RunDirector/DayDirector replace it. Writes go only through
// WorldState.ServerSet*; NetClock is written only on start, stop, pause, scrub, minute set and new day (14.2), never per frame.
public sealed class DevWorldDriver : MonoBehaviour
{
    private readonly DevClock _clock = new DevClock(DayClock.DefaultMinutesPerSecond);
    private readonly LogOnce _log = new LogOnce();
    private WorldState _world;
    private EcologyRunState _run;
    private WeatherDirector _weather;
    private HotspotPlanner _hotspots;                  // null without a baked grid (or at the 12.7 cut-line)
    private WeatherTableData _table; private ShoreGrid _grid; private EcologyTuning _tuning; private bool _hasZone;
    private IPauseState _pause;
    private WeatherMask _override = WeatherMask.None;  // driver-local: a map switch resets it (new driver instance)

    public EcologyRunState Run => _run;
    public bool ClockRunning => _clock.DevRunning;
    public WeatherMask WeatherOverride => _override;
    public WeatherDirector Weather => _weather;

    private bool Active => _world != null && _world.IsSpawned && _world.IsServer;
    private double Now => _world.NetworkManager.ServerTime.Time;

    internal void ServerInitialize(WorldState world)  // WorldState.OnNetworkSpawn (server), before HasState
    {
        _world = world;
        ResolveZone();                                  // registry.Ecology -> Spots[SpotIndex(world.SpotId)].ZoneIndex -> Zones[i]: Weather, Grid; eco.Tuning
        HookPause();                                    // ServiceRoots.Game IPauseState: read Paused now, subscribe Changed
        _clock.SetPaused(0.0, _pause != null && _pause.Paused);   // anchor not yet written: the return value is ignored
        Adopt(EcologySession.GetOrCreate(FishingConstants.DefaultDevRunSeed));   // CCR-D3
    }

    // Session start, map-switch resume (M3-D31) and dev seed change all land here: the run's season, the identical rebuilt
    // track, the clock at minute 0 of the run's current day running, a fresh planner for that day, weather of segment 0.
    private void Adopt(EcologyRunState run)
    {
        double now = Now;
        _run = run;
        _world.ServerSetSeason(run.Season);
        RebuildWeather();                               // _weather = _hasZone ? new WeatherDirector(_table, run.Season, run.RunSeed) : null; _weather?.EnsureDay(run.Day)
        _clock.SetRunning(now, true);
        _clock.StartAt(now, 0f);
        _world.ServerSetClock(_clock.Anchor);
        _hotspots = NewPlanner(run.Day);                // null when !_hasZone || _grid == null (log once) ; 12.7 cut-line: always null
        _world.ServerSetHotspot(default);
        PushWeather(0);
    }

    private void Update()
    {
        if (!Active || _run == null) return;
        EcologyRunState current = EcologySession.Current;
        if (current != null && !ReferenceEquals(current, _run)) { Adopt(current); return; }   // FishingHost.SetRunSeed replaced it
        double now = Now;
        if (_clock.ShouldAutoNewDay(now)) { DevNewDay(); return; }
        int minute = DevWorldRules.LookupMinute(_clock.MinuteAt(now));
        PushWeather(minute);
        if (_hotspots != null && _hotspots.Advance(minute)) _world.ServerSetHotspot(_hotspots.Current);
    }

    private void HandlePauseChanged(bool paused)       // solo pause (R34): stop the clock, never timeScale; resume is continuous
    {
        if (!Active) return;
        if (_clock.SetPaused(Now, paused)) _world.ServerSetClock(_clock.Anchor);
    }

    public void DevScrub(bool held)                     // F8 (T22): x60 while held; re-anchored on press and on release only
    {
        if (!Active) return;
        if (_clock.SetScrub(Now, held)) _world.ServerSetClock(_clock.Anchor);
    }

    public void DevSetClockRunning(bool running)
    {
        if (!Active) return;
        if (_clock.SetRunning(Now, running)) _world.ServerSetClock(_clock.Anchor);
    }

    // 0..1200. 1200 with a RUNNING clock means a new day on the next frame (auto new day); tests that need "day over" stop the clock first.
    public void DevSetMinute(int minute)
    {
        if (!Active) return;
        _clock.StartAt(Now, minute);
        _world.ServerSetClock(_clock.Anchor);
        int m = DevWorldRules.LookupMinute(_clock.MinuteAt(Now));
        _hotspots = NewPlanner(_run.Day);               // rebuild for the day, then catch up: a backward jump would otherwise keep a stale
        if (_hotspots != null) _hotspots.Advance(m);    // future hotspot; forward jumps give the same state (0.3 question 1)
        _world.ServerSetHotspot(_hotspots != null ? _hotspots.Current : default);
        PushWeather(m);
    }

    public void DevNewDay()                             // Day++, minute 0, the day's planner and track weather (M3-D11); run/scrub/pause kept
    {
        if (!Active) return;
        _run.Day++;
        _weather?.EnsureDay(_run.Day);                  // day 8+: infinite continuation, one day at a time (T7)
        _clock.StartAt(Now, 0f);
        _world.ServerSetClock(_clock.Anchor);
        _hotspots = NewPlanner(_run.Day);
        _world.ServerSetHotspot(default);
        PushWeather(0);
    }

    public void DevSetSeason(Season s)                  // the track depends on the season: rebuild it (same seed)
    {
        if (!Active) return;
        _run.Season = s;
        _world.ServerSetSeason(s);
        RebuildWeather();
        PushWeather(DevWorldRules.LookupMinute(_clock.MinuteAt(Now)));
    }

    public void DevSetWeatherOverride(WeatherMask single)
    {
        if (!Active) return;
        if (!DevWorldRules.IsValidOverride(single)) { if (_log.ShouldLog("bad-override")) Debug.LogWarning("DevWorldDriver: weather override must be None, Clear, Rain or Fog.", this); return; }
        _override = single;
        PushWeather(DevWorldRules.LookupMinute(_clock.MinuteAt(Now)));
    }

    public void DevSpawnHotspotAt(float x, float z)
    {
        if (!Active) return;
        if (_hotspots == null) { if (_log.ShouldLog("no-planner")) Debug.LogWarning("DevWorldDriver: no hotspot planner (grid not baked).", this); return; }
        _world.ServerSetHotspot(_hotspots.ForceAt(x, z, DevWorldRules.LookupMinute(_clock.MinuteAt(Now))));
    }

    private void PushWeather(int minute)                // WorldState skips unchanged values: no per-frame traffic
    {
        WeatherMask w = _override != WeatherMask.None ? _override : (_weather != null ? _weather.At(_run.Day, minute) : WeatherMask.Clear);
        _world.ServerSetWeather(w);
    }
}
```
- **Lifecycle:**
  - `OnDisable`/`OnDestroy` unsubscribe the pause event.
  - No `Awake` work, because in-scene objects wake before NGO spawns them.
  - `ResolveZone` failure (no ecology database, unknown spot, spot without a zone): `LogWarning` once. The clock still runs, the weather stays Clear, and there are no hotspots. C2 #9 makes a PlayMode run with that warning fail, which is intended.
- **12.7 cut-line:** if the controller calls it, `NewPlanner` returns null always (one line, commented). Nothing else changes.

**`BuildInfo` (edit):** `ProtocolVersion = 4`. History line: `4 = M3 (T14: in-scene WorldState NetworkObject with NetSeason, NetWeather, NetClock, NetHotspot)` (the existing `3 = R72 ...` line stays). `BuildInfoTests` expects 4.

### Steps (TDD)
1. Write `WorldWireTests.cs` and `DevWorldTests.cs`. Change `BuildInfoTests` to 3. Run `test-edit -Filter "FishGame.Tests.EditMode.(WorldWireTests|DevWorldTests|BuildInfoTests)"`. Red is `CS0246` for the new types.
2. Implement `ClockAnchor`, `HotspotState`, `DevClock`/`DevWorldRules`, `EcologySession` and `BuildInfo` (the `InternalsVisibleTo` line is already there from T2). Re-run the same filter until it is green.
3. Implement `WorldState` and `DevWorldDriver`. Run `compile`.
4. Run the full `test-edit`, then `test-play`. No scene has a `WorldState` yet, so PlayMode must be unchanged. `EcologySession` is created only by a driver, which does not exist in scenes yet.
5. Run `build` (validators unchanged) and restore the scene churn.

### Tests
**`WorldWireTests.cs` (C 10.4):**
- `ClockAnchor_FromToCore_RoundTripAndEquality`:
  - `{AnchorServerTime 12345.678901234, AnchorMinute 417.25, MinutesPerSecond 1.3333334, Running true}` -> `From` -> `ToCore`. Every field is exactly equal (the double too). Repeat with `Running false`.
  - Equality: identical anchors are equal with equal hashes. Changing each of the 4 fields alone breaks equality.
  - Two anchors with `AnchorMinute = NaN` are equal.
  - Mutations: `From` dropping `Running`; float `==` in `Equals` (the NaN case fails).
- `HotspotState_FromToCore_RoundTripAndEquality`:
  - `{Active true, X 3.5, Z -12.25, Radius 3, StartMinute 200, EndMinute 250}` round-trips with `StartMinute` 0.
  - Changing each of the 5 fields alone breaks equality.
  - `Active false` round-trips to `Active false`.
- `ClockAnchor_Size_AtMostTwentyFourBytes`: `UnsafeUtility.SizeOf<ClockAnchor>() <= 24` (log the actual size, expected 24). Also `SizeOf<HotspotState>() <= 20`.
  - Mutation: an added field or a `long` running flag.

**`DevWorldTests.cs` (extra, CCR-D4; base rate `DayClock.DefaultMinutesPerSecond`, times in seconds):**
- `DevClock_Pause_FreezesMinuteAndResumeIsContinuous`:
  - start at t=100 m=0;
  - at t=130, `SetPaused(true)` returns true and the minute is 40 (+-1e-3);
  - at t=200 the minute is still 40;
  - `SetPaused(false)` at t=200 returns true, and the minute at t=230 is 80.
  - Mutation: pause writing rate 0 with `Running` true.
- `DevClock_Scrub_SixtyTimesRateAndContinuous`:
  - at t=10 the minute is 13.33;
  - `SetScrub(true)` at t=10: the minute is continuous (|delta| < 1e-3) and the rate is x60;
  - at t=11 the minute is 13.33 + 80;
  - release at t=11: continuous again.
- `DevClock_ToggleWhileStopped_WritesNothing`: dev-stopped, then `SetPaused(true)`, `SetScrub(true)` and `SetScrub(false)` all return false, and the anchor is unchanged.
- `DevClock_AutoNewDay_OnlyWhileEffectivelyRunningAt1200`:
  - running from minute 1190: false at +7 s, true at +8 s;
  - stopped with `StartAt(1200)`: false;
  - paused at 1200: false.
- `DevClock_StartAt_ClampsAndKeepsRunState`: `StartAt(-5)` gives 0, `StartAt(5000)` gives 1200 and `StartAt(NaN)` gives 0. `DevRunning`/`Paused`/`Scrub` are unchanged.
- `DevClock_Rate_NeverZeroAfterAnyOperation`: a scripted sequence of every call; after each one `Anchor.MinutesPerSecond > 0` (the `HasState` invariant).
- `EcologySession_GetOrCreate_ReturnsExistingAndIgnoresSeed`: `Clear()`; `a = GetOrCreate(1)`; `GetOrCreate(2)` returns the same object `a`; `a.RunSeed == 1`.
- `EcologySession_NewRunState_SeasonFromSeedDayOne`: `Season == WeatherDirector.RollSeason(seed)` and `Day == 1`.
- `EcologySession_Clear_NextGetOrCreateIsFresh`: after `Clear()` you get a new instance with the new seed. Every test calls `Clear()` in `[TearDown]`.
- `EcologySession_StaleSession_DroppedOnGetOrCreate`:
  1. With `ServiceRoots.App` null, create run `a`.
  2. `ServiceRoots.SetApp(registry)`, where the registry holds a fake `INetworkSession` (a minimal stub class in the test file).
  3. `GetOrCreate(5)` returns a new instance with seed 5, subscribed to the fake's `Ended`.
  4. Raise the fake's `Ended`: `Current` becomes null.
  5. Restore `ServiceRoots.SetApp(null)` in `finally`.
  - Mutation: no stale guard, so the next PlayMode test inherits the previous counter, reservations and day.
- `DevWorld_MapSwitchResume_RebuiltTrackIdentical`: run `{seed 7, Season Autumn, Day 3}`. `new WeatherDirector(table, Autumn, 7)`, `EnsureDay(3)`, then `At(3, m)` for m in {0, 240, 480, 720, 960} equals a second, freshly built director's values. This is what `Adopt` relies on for a Spot load.
- `DevWorld_WeatherOverride_OnlyNoneOrSingleM3Weather`: None, Clear, Rain and Fog are valid; Storm, Heatwave and `Rain|Fog` are invalid.

**`BuildInfoTests.cs`:** the expectation changes 3 -> 4.

### Acceptance (reviewer checks)
- Authority comments open `ClockAnchor`, `HotspotState`, `WorldState`, `DevWorldDriver`, `EcologySession` and `DevClock`.
- `NetClock` is written only from `Adopt`, `DevSetMinute`, `DevNewDay` and the three `DevClock` toggles; grep `ServerSetClock`. No per-frame write.
- `WorldState` writes all four values inside the server's `OnNetworkSpawn`, before `HasState` can be true.
- `Changed` fires on spawn and despawn.
- No `Time.timeScale` anywhere (R34). No `UnityEngine.Random`.
- The driver is inert on clients: every public `Dev*` method starts with `if (!Active) return;`.
- `EcologySession` unsubscribes `Ended` in `Clear`. All statics are reset on `SubsystemRegistration`.
- The stale-session guard in `GetOrCreate` is there. One PlayMode test's run state must never reach the next.
- Note for T24 and every later PlayMode test that casts: call `EcologySession.Clear()` in teardown too.
- **Where each skeleton T14 acceptance item is proven:**
  - "late joiner sees the same minute": T16's MPPM sync check, T24 `EcologySolo_WorldState_SyncedWithNonZeroHash`, and the T25 manual MPPM list;
  - "solo pause stops the clock": `DevClock_Pause_FreezesMinuteAndResumeIsContinuous` here, and T24 `EcologySolo_WorldClock_RunsAndStopsInSoloPause`;
  - "protocol 4": `BuildInfoTests`.
- `test-edit` and `test-play` PASS. Scenes are untouched.

### Commit
```powershell
git add Assets/_Project/Gameplay/World/ClockAnchor.cs Assets/_Project/Gameplay/World/ClockAnchor.cs.meta Assets/_Project/Gameplay/World/HotspotState.cs Assets/_Project/Gameplay/World/HotspotState.cs.meta Assets/_Project/Gameplay/World/WorldState.cs Assets/_Project/Gameplay/World/WorldState.cs.meta Assets/_Project/Gameplay/World/DevWorldDriver.cs Assets/_Project/Gameplay/World/DevWorldDriver.cs.meta Assets/_Project/Gameplay/World/EcologySession.cs Assets/_Project/Gameplay/World/EcologySession.cs.meta Assets/_Project/Gameplay/World/DevClock.cs Assets/_Project/Gameplay/World/DevClock.cs.meta Assets/_Project/Core/Versioning/BuildInfo.cs Assets/_Project/Tests/EditMode/BuildInfoTests.cs Assets/_Project/Tests/EditMode/WorldWireTests.cs Assets/_Project/Tests/EditMode/WorldWireTests.cs.meta Assets/_Project/Tests/EditMode/DevWorldTests.cs Assets/_Project/Tests/EditMode/DevWorldTests.cs.meta
git commit -m "m3(world): WorldState (4 NetworkVariables, IWorldContext), ClockAnchor/HotspotState wire, DevWorldDriver dev clock, EcologySession, protocol 4" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## T15 - Fishing host and owner integration

**Goal:**
- `FishingHost` builds `HostCastView.Context`, `GridWater` and `CastingAllowed` from `WorldState` and the spot's zone grid, replacing T13's interim.
- The ledger runs on the session run state (`EcologySession`), with the reservation-leak guard.
- The owner marker runs the same two gates (M3-D16).
- `FishingDebugSettings.Luck`.
- `DevLoadSpotRequested` -> `SceneFlow` dev map switch.

**Owned files (C 12 + CCR-D4):**
- `Fishing/Host/FishingHost.cs`, `Fishing/Owner/FishingRodController.cs`, `Fishing/Debug/FishingDebugSettings.cs`;
- `Runtime/Services/DevLoadSpotRequested.cs`, `App/SceneFlow.cs`;
- new internal `Fishing/Host/SpotEcology.cs`;
- tests `Tests/EditMode/SpotEcologyTests.cs`, `Tests/PlayMode/FishingWorldTests.cs`;
- deletes T13's `FishingHostLedger_InterimLakeContext_BluegillEligible` from `FishingHostLedgerTests.cs`.

**Shared edits (C 2.6):** `FishingHost.cs` (T15, after T13), `FishingRodController.cs` (after T1), `FishingDebugSettings.cs` (after T1), `SceneFlow.cs` (T15 only).
**Integration-fix rule (C 2.6):** T15 may fix defects in earlier M3 files (T1-T14, T16) with the public surface frozen. Name each one `fix(<file>): <what>` in the commit message. A signature change is a contract change: stop and report.
**Depends on:** T13, T14, T16 (the lake `World` root, StandPoints, the baked `lake_dock_grid`, and T16's MPPM sync check passed or the M3-D18 fallback applied). Wait for the T16 review fixes (skeleton 5).
**Implementer:** Sonnet. **Reviewer:** Opus high.
**Constraints that matter most (C 2.8):**
- **#26: owner and host run the same gates.** Both go through `SpotEcology`. An honest cast at a grid edge is never host-rejected.
- #23: the context comes from host values only.
- #24: never partially restore a Spot scene; T15 changes no scene.
- C2 #9: warning-free PlayMode; the map switch must not warn.
- C2 #10: the late-joiner `OnLoadComplete` path is unchanged.
- C2 #1: `Luck` and the map switch are honoured only when `Debug.isDebugBuild`.

### Behaviour

**`SpotEcology` (new, internal static; code).** One function set used by the host view and the owner marker:
```csharp
// Fishing/Host/SpotEcology.cs
// Authority: none (pure lookups over replicated world state and static baked data). The host's cast view and the owner's marker both use
// these functions, so the grid-water and casting-open gates agree on every peer (C 2.8 #26, M3-D16).
internal static class SpotEcology
{
    // The zone and baked grid of the world's spot. False when anything is missing: no world or no state yet, an unknown spot id,
    // no ecology database, a spot index outside EcologyDatabase.Spots, or an unbaked grid (ZoneData.Grid null). Callers treat false
    // as "casting closed" (host: NotAllowedNow; owner: marker invalid), never as "no extra gate".
    public static bool TryResolve(ContentRegistry registry, IWorldContext world, out int zoneIndex, out ZoneData zone)
    {
        zoneIndex = -1; zone = null;
        if (registry == null || world == null || !world.HasState || string.IsNullOrEmpty(world.SpotId)) return false;
        EcologyDatabase eco = registry.Ecology;
        if (eco == null || !registry.TryGetSpot(world.SpotId, out _)) return false;
        ushort spotIndex = registry.SpotIndex(world.SpotId);
        if (spotIndex >= eco.Spots.Count) return false;
        int zi = eco.Spots[spotIndex].ZoneIndex;
        if (zi < 0 || zi >= eco.Zones.Count || eco.Zones[zi].Grid == null) return false;
        zoneIndex = zi; zone = eco.Zones[zi];
        return true;
    }

    public static bool GridWater(ZoneData zone, float x, float z) => zone != null && zone.Grid != null && zone.Grid.IsWater(x, z);

    // The world part of the host context (M3-D15). Rod tier, bait and depth are placeholders the ledger always overwrites.
    public static FishingContext WorldContext(IWorldContext world, ZoneData zone, int zoneIndex, float x, float z, float luck, int players)
    {
        return new FishingContext
        {
            ZoneIndex = zoneIndex,
            SubAreaIndex = zone.SubAreaIndexFor(zone.Grid.ShoreM(x, z)),   // not water -> +Infinity -> last band; GridWater false rejects anyway
            Depth = float.PositiveInfinity,
            Minute = world.Minute, Season = world.Season, Weather = world.Weather,
            RodTier = 0, Bait = BaitMods.Neutral(0),
            InHotspot = world.IsInHotspot(x, z),
            Luck = float.IsNaN(luck) ? 0f : Math.Max(-1f, Math.Min(1f, luck)),
            Events = EventMods.Neutral, Skills = SkillMods.Neutral,
            PlayersInSession = players < 1 ? 1 : players,
        };
    }
}
```

**`FishingHost` (edit; code for the host context build):**
```csharp
// Class comment gains: "M3 (T15): the cast view's world part comes from WorldState and the spot's zone grid via SpotEcology (the owner
// marker runs the same functions); the ledger runs on the session run state (EcologySession), so a Spot load keeps the cast counter
// and catch limits. No world, zone or baked grid -> CastingAllowed false -> NotAllowedNow (never a roll without a context)."

private bool EnsureLedger()
{
    if (Ledger != null) return true;
    if (!IsServerActive) return false;
    ResolveRegistry();                                                    // as M2: App ContentRegistry, _db = registry.Fishing
    EcologyDatabase eco = _registry != null ? _registry.Ecology : null;
    if (eco == null) { if (_log.ShouldLog("no-eco")) Debug.LogError("FishingHost: no ecology database; the ledger cannot start.", this); return false; }
    try
    {
        EcologyRunState run = EcologySession.GetOrCreate(_runSeed);       // existing run state wins (CCR-D3): map switches keep it
        Ledger = new FishingHostLedger(eco, run, _registry.RodIndex(DefaultRodId), (byte)_registry.BaitIndex(DefaultBaitId));
    }
    catch (Exception e) { Ledger = null; if (_log.ShouldLog("ledger-create-failed")) Debug.LogError("FishingHost: the ledger could not be created: " + e.Message, this); return false; }
    return true;
}

public ulong RunSeed
{
    get
    {
        if (Ledger != null) return Ledger.RunSeed;
        EcologyRunState run = EcologySession.Current;
        return run != null ? run.RunSeed : _runSeed;
    }
}

// Dev (T22 panel): only before the session's first accepted cast. A new seed replaces the session run state; DevWorldDriver adopts
// the new instance on its next Update (season re-roll, track rebuild, clock to minute 0 of day 1).
public void SetRunSeed(ulong seed)
{
    EcologyRunState cur = EcologySession.Current;
    if (cur != null && cur.HostCastCounter > 0)
    {
        if (_log.ShouldLog("seed-late")) Debug.LogWarning("FishingHost: SetRunSeed ignored after the first accepted cast.", this);
        return;
    }
    _runSeed = seed;
    if (cur != null && cur.RunSeed == seed) return;
    EcologySession.Clear();
    EcologySession.GetOrCreate(seed);
    if (Ledger == null) return;
    Ledger = null;
    if (!EnsureLedger()) return;
    RemirrorAllRods();                                                    // the M2 loop: OnPlayerReady for every server-initialized rod
}

// Inside HandleCastRequest, replacing the M2 / T13-interim view construction (after the finite-origin and 3 m slack checks):
var view = new HostCastView { PlayerX = seen.x, PlayerY = seen.y, PlayerZ = seen.z };
WorldState world = WorldState.Current;
bool ecology = SpotEcology.TryResolve(_registry, world, out int zoneIndex, out ZoneData zone);
if (!ecology && _log.ShouldLog("no-ecology"))
    Debug.LogWarning("FishingHost: no world state, spot zone or baked shore grid in this scene; casts are rejected NotAllowedNow.", this);
view.CastingAllowed = ecology && world.CastingOpen;                       // M3-D16: day over -> NotAllowedNow (checked first)
if (Ledger.TryComputeLanding(slot, in req, out float lx, out float lz))
{
    view.Probe = CastProbe.Probe(lx, lz);
    if (ecology)
    {
        view.GridWater = SpotEcology.GridWater(zone, lx, lz);             // M3-D16: after the probe in ValidateCast
        float luck = Debug.isDebugBuild ? FishingDebugSettings.Luck : 0f;
        view.Context = SpotEcology.WorldContext(world, zone, zoneIndex, lx, lz, luck, PlayersInSession());
    }
}
// When !ecology, or the landing could not be computed, the ledger rejects (NotAllowedNow / BadCharge / OriginTooFar) before it rolls,
// so the default Context never reaches the resolver (D-F3).

private static int PlayersInSession()
{
    SessionState s = SessionState.Current;
    return s != null && s.ConnectedCount > 0 ? s.ConnectedCount : 1;
}

private void OnDisable()
{
    // ... the M2/T1 unsubscriptions ...
    // Reservation-leak guard (D-F6): a Spot unload may disable this host before the rods despawn. The run state outlives this ledger,
    // so release every outstanding reservation now; Disconnect is idempotent with the later Despawned path.
    if (Ledger != null)
    {
        var rods = FishingRodNetwork.Active;
        for (int i = 0; i < rods.Count; i++) { FishingRodNetwork rod = rods[i]; if (rod != null) Ledger.Disconnect(rod.OwnerClientId); }
    }
    Ledger = null;
    if (s_current == this) s_current = null;
}
```
- Remove T13's `_interimRun`, `_interimZone`, `InterimContext` and the extra test.
- The F6/F7 reset is not in `OnDisable` any more (T1). T15 must not re-add it.

**`FishingRodController` (edit; owner marker, M3-D16):**
- In `UpdateMarker` (wherever T1 left it), compute `bool probeOk = CastMath.IsValidLanding(in probe, _tuning.MinLandDepth)` and `MarkerValid = probeOk && OwnerWorldGates(lx, lz)`.
- `MarkerPosition` is unchanged.
- `OwnerWorldGates` code:
```csharp
// The same two gates the host adds (C 2.8 #26): grid water through SpotEcology on the same baked asset, CastingOpen from the replicated
// clock. Missing world/state/zone/grid -> false, mirroring the host's NotAllowedNow (a red marker instead of a cast the host would refuse).
private bool OwnerWorldGates(float x, float z)
{
    WorldState world = WorldState.Current;
    if (world == null || !world.HasState) return false;
    if (!ReferenceEquals(world, _gateWorld) || _gateZone == null)          // resolve once per world instance (no per-frame registry scans)
    {
        _gateWorld = world;
        if (!SpotEcology.TryResolve(_registry, world, out _, out _gateZone)) { _gateZone = null; return false; }
    }
    return world.CastingOpen && SpotEcology.GridWater(_gateZone, x, z);
}
```
- No allocation per frame.
- `_gateWorld` and `_gateZone` are cleared in `OnDisable`.
- On a disagreement the host still wins (recall), as in M2.

**`FishingDebugSettings.Luck` (edit):**
- `public static float Luck { get; set; }`. The setter clamps to [-1, 1], and NaN becomes 0.
- It is reset in `SubsystemRegistration` and in T1's session-end reset (`OnSessionEnded`, hooked by `ClearOnSessionEnd(INetworkSession)`; see 0.3), never on a Spot change.
- Honoured only when `Debug.isDebugBuild`; the host reads it, as shown above.

**`DevLoadSpotRequested` (new, `Runtime/Services/DevLoadSpotRequested.cs`):**
- `public readonly struct DevLoadSpotRequested { public readonly string SpotId; public DevLoadSpotRequested(string spotId) { SpotId = spotId; } }`.
- Comment: "EventBus message; host dev panel -> SceneFlow (M3-D31)".

**`SceneFlow` (edit; code):**
```csharp
// Ctor: devLoadSubscription = bus.Subscribe<DevLoadSpotRequested>(HandleDevLoadSpot);   Dispose: devLoadSubscription?.Dispose();
// Class comment gains: "M3 dev map switch (M3-D31): the host loads a spot's scene through NetworkSceneManager (Single) on
// DevLoadSpotRequested, debug builds only; clients ignore the message and follow NGO scene sync. Session state is kept (run state in
// EcologySession, creels in SessionState); players respawn through PlayerSpawner's OnLoadEventCompleted / OnLoadComplete paths."
private void HandleDevLoadSpot(DevLoadSpotRequested msg)
{
    if (disposed || !Debug.isDebugBuild) return;
    if (!networkManager.IsServer || session.State != ConnState.Connected) return;
    ServiceRegistry app = ServiceRoots.App;
    ContentRegistry registry = null;
    if (app != null) app.TryGet(out registry);
    if (registry == null || msg.SpotId == null || !registry.TryGetSpot(msg.SpotId, out SpotDef spot) || !SceneNames.IsGameplay(spot.SceneName))
    {
        Debug.LogWarning("SceneFlow: dev map switch ignored: unknown spot '" + msg.SpotId + "'.");
        return;
    }
    NetworkSceneManager sceneManager = networkManager.SceneManager;
    if (sceneManager == null) { Debug.LogWarning("SceneFlow: dev map switch ignored: scene management is off."); return; }
    SceneEventProgressStatus status = sceneManager.LoadScene(spot.SceneName, LoadSceneMode.Single);   // same-scene reload allowed (D-F5)
    if (status != SceneEventProgressStatus.Started)
        Debug.LogWarning("SceneFlow: dev map switch to " + spot.SceneName + " returned " + status + "; staying.");   // never leaves the session
}
```
- `SceneNames.IsGameplay` already accepts any `Spot_` prefix, so it covers T17's `Spot_HarbourPier`.

### Steps
1. Write `SpotEcologyTests.cs` and `FishingWorldTests.cs`. Run `test-edit -Filter "FishGame.Tests.EditMode.SpotEcologyTests"`. Red is `CS0246 SpotEcology`.
2. Implement `SpotEcology`. Re-run the filter until it is green.
3. Make the `FishingHost`, `FishingRodController`, `FishingDebugSettings`, `DevLoadSpotRequested` and `SceneFlow` edits. Delete the interim and its test. Run `compile`, then `build`. Restore the whole-file scene churn: T15 changes no scene.
4. Run the full `test-edit`, then `test-play` (background).
   - Every M2 PlayMode cast test now goes through the real world context: the G9 deck-end cast must be inside the baked lake grid and `CastingOpen`.
   - Diagnose failures with `superpowers:systematic-debugging` and apply the integration-fix rule.
5. Manual check (controller, editor solo):
   - The F3 overlay `HOST casts=` keeps counting across a dev map switch. Trigger it from the PlayMode test helper or a temporary debug call; the T22 button comes later.
   - The marker goes red over the shore and over a dock-shadowed cell the grid calls land.

### Tests
**`SpotEcologyTests.cs` (EditMode, extra):**

Fixture:
- a 6 x 6 synthetic `ShoreGrid` (cell 1 m, origin (0, 0)) whose left 3 columns are land and right 3 water;
- `ZoneData` with the lake bands;
- a `FakeWorld : IWorldContext` with settable `HasState`, `CastingOpen`, `Minute`, `Season`, `Weather`, a hotspot circle and `SpotId`.

| Test | Cases | Mutation it catches |
|---|---|---|
| `SpotEcology_OwnerAndHostGates_AgreeAcrossGridEdge` | Sweep `x` from 2.0 to 4.0 in 0.01 m steps at `z = 2.5`, with `CastingOpen` true and false. Owner: `probeOk && CastingOpen && GridWater(zone, x, z)`. Host: `CatchValidator.ValidateCast` with `CastingAllowed = CastingOpen`, `GridWater = GridWater(zone, x, z)`, a valid probe and a valid request equals `None`. The two booleans agree at every step, and `x = 2.999` vs `3.0` flips both. | The host using a different lookup (for example rounding to the cell centre) or the owner skipping a gate. |
| `SpotEcology_WorldContext_MapsWorldValuesAndSubArea` | x at shore distance 2, 7 and 12 m gives `SubAreaIndex` 0, 1 and 2. `Minute`, `Season` and `Weather` are copied. `InHotspot` is true inside the circle and false outside. `Events.IsValid && Skills.IsValid`. `Luck` 3 -> 1 and NaN -> 0. Players 0 -> 1. | A swapped field; a default (invalid) `Events`. |
| `SpotEcology_TryResolve_FalseWithoutStateSpotOrGrid` | On the real registry (built): a fake world with `lake_dock` and `HasState` true gives true with a non-null grid. `HasState` false, an unknown spot id, or a null registry each give false. | Resolving without `HasState` (a client marker valid before the first sync). |
| `FishingDebugSettings_Luck_ClampedAndHonouredAsSet` | Set 0.5 and read 0.5. 2 -> 1, -7 -> -1, NaN -> 0. The reset clears it. | The clamp missing. |

**`FishingWorldTests.cs` (PlayMode, extra).**
- Reuse `FishingPlayModeDriver` (C2 7.19) unchanged.
- Teardown resets the debug settings, as `FishingSoloTests` does, **and calls `EcologySession.Clear()`** before `PlayModeHarness.TearDownApp()`. `SubsystemRegistration` runs once per Play Mode entry, not per test.
- Counter asserts are relative (n, then n + 1), never absolute.
- `FishingWorld_UnforcedCast_AcceptedWithWorldContext`:
  1. Set `ForcedBiteWait = 30`; force no species. Arrange (G9).
  2. Assert `WorldState.Current.HasState` and `CastingOpen`.
  3. EquipAndReady (the marker is valid: the owner gates pass at the G9 landing), then ChargeAndRelease.
  4. Wait for `Waiting`.
  5. Assert that `FishingHost.Current.Ledger.TryGetActive(...)` is true (the host accepted, the context built and the resolver did not throw).
  6. Cancel. Assert the log watch is empty.
- `FishingWorld_LakeReload_KeepsRunStateAndCastCounter`:
  1. Set `ForcedBiteWait = 30`, `FishingDebugSettings.Luck = 0.5f`. Arrange.
  2. Cast, wait for `Waiting`, cancel, wait for `Idle`.
  3. Record `run = EcologySession.Current`, `season = run.Season`, `n = run.HostCastCounter` (expected 1), `oldWorld = WorldState.Current` and `oldHost = FishingHost.Current`.
  4. Publish `new DevLoadSpotRequested("lake_dock")` on the App `EventBus`.
  5. Wait (bounded 20 s realtime) for all of these: `WorldState.Current != oldWorld`, `WorldState.Current.HasState`, `FishingHost.Current != oldHost`, `PlayerNetState.Local` respawned, `FishingRodController.Local != null`, and the loadout mirrored (as in Arrange).
  6. Assert:
     - `ReferenceEquals(EcologySession.Current, run)`;
     - `run.Season == season`;
     - `run.HostCastCounter == n`;
     - `FishingDebugSettings.Luck == 0.5f` (T1: dev overrides survive a Spot load);
     - `WorldState.Current.Minute < 30` (the clock restarted at minute 0 of the same day).
  7. Re-teleport to G9, then cast and cancel again. Assert `run.HostCastCounter == n + 1`.
  8. Assert the log watch is empty.
  - This covers the skeleton's T15 bullet "the map switch keeps the cast counter" with the lake -> lake reload (D-F5). T24 repeats it with the harbour.

### Acceptance (reviewer checks)
- The host view and the owner marker both call `SpotEcology.TryResolve` and `SpotEcology.GridWater`. No second implementation of the grid or clock gate exists (grep `IsWater(` and `CastingOpen` in Fishing).
- `CastingAllowed` is false whenever the ecology context is missing. `Context` is only set when it is present.
- `FishingHost.OnDisable` releases reservations before dropping the ledger.
- `EnsureLedger` uses `EcologySession.GetOrCreate`. `SetRunSeed` refuses after the session's first accepted cast.
- `Luck` reaches the context only in debug builds.
- The map switch runs only on the host, only in debug builds, and never leaves the session on failure. The late-joiner path is untouched.
- T13's interim code and its test are gone.
- The skeleton T15 bullets: grid edge, proven by `SpotEcology_OwnerAndHostGates_AgreeAcrossGridEdge`; counter across a switch, proven by `FishingWorld_LakeReload_KeepsRunStateAndCastCounter`.
- Every integration fix is named in the commit.
- `test-edit` and `test-play` PASS, warning-free.

### Commit
```powershell
git add Assets/_Project/Fishing/Host/FishingHost.cs Assets/_Project/Fishing/Host/SpotEcology.cs Assets/_Project/Fishing/Host/SpotEcology.cs.meta Assets/_Project/Fishing/Owner/FishingRodController.cs Assets/_Project/Fishing/Debug/FishingDebugSettings.cs Assets/_Project/Runtime/Services/DevLoadSpotRequested.cs Assets/_Project/Runtime/Services/DevLoadSpotRequested.cs.meta Assets/_Project/App/SceneFlow.cs Assets/_Project/Tests/EditMode/FishingHostLedgerTests.cs Assets/_Project/Tests/EditMode/SpotEcologyTests.cs Assets/_Project/Tests/EditMode/SpotEcologyTests.cs.meta Assets/_Project/Tests/PlayMode/FishingWorldTests.cs Assets/_Project/Tests/PlayMode/FishingWorldTests.cs.meta <integration-fix files, if any>
git commit -m "m3(fishing): host context from WorldState and the shore grid, owner marker gates, Luck, dev map switch" -m "<one line per fix(<file>): <what>, if any>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Open questions (section D)

1. **For the user, via the controller: fixed dev run seed.** M3 keeps M2's fixed `DefaultDevRunSeed`, so every M3 session rolls the same season and the same 35-segment weather track until the dev panel changes the seed or the season.
   - **Default:** keep it fixed (reproducible playtests and bug reports; the dev panel varies it).
   - **Alternative:** a random seed per session, shown on the F3 overlay.
2. **For the controller:** the CCR-D1 to D3 rulings. ANSWERED 2026-10-12: all accepted (M3-D55..D57), D1 no longer blocks T13.
3. **For sections A/B:** the 0.3 questions (`HotspotPlanner.Advance` jump-safety, `EcologyRunState` initial values, the T1 reset method name). ANSWERED 2026-10-12 (see the answers at the top of 0.3).

## Risks (section D)

1. **The day-over boundary race.** At exactly minute 1200 the owner's replicated clock and the host's clock can disagree for about a frame (ServerTime estimate). A cast released in that frame can be host-rejected `NotAllowedNow` and recalls through the normal path.
   - With the clock running, the host starts the next day on the same frame.
   - With the clock stopped at 1200, both sides agree.
   - Accepted; it is not an M3-D16 violation in practice. M4's LastCast vote removes the edge.
2. **The reservation leak on Spot unload (D-F6).** The run state outlives the per-scene ledger.
   - On a dev map switch, NGO despawns the rods inside `LoadScene`, so `HandleRodDespawned` releases.
   - On other unloads, `FishingHost.OnDisable` releases through `Disconnect`.
   - The PlayMode reload test exercises the switch path. A missed path would block a limit-1 special for the rest of the session (skeleton risk 6 accepts only the brief in-flight block).
3. **`HasState` relies on the rate never being 0.** The `DevClock` invariant test pins this. M4's RunDirector must keep it (or `WorldState` gains a flag). This is recorded in the `WorldState` class comment.
4. **CCR-D1 (accepted 2026-10-12) edits files of R72, T3 and T10.** If R72 lands after this plan is approved and reshapes `FishingTestData`, T13 re-applies the same two-initializer removal to R72's version.
5. **The first in-scene NetworkObject (C 15 #2).** T14 only writes the class. T16 proves the MPPM sync, and the M3-D18 fallback keeps T14's public API. `ServerInitialize` is called from `OnNetworkSpawn` either way (scene-placed or prefab).
