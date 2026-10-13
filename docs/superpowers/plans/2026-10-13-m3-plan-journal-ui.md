# FishGame M3 - Section G plan: Journal, UI, art and acceptance (T20-T25)

- **Date:** 2026-10-13. Branch `m3-fish-ecology`. Author: section G (Opus, effort high).
- **Status:** written 2026-10-13 (all six tasks); contract change requests G-CR1..G-CR5 were all ACCEPTED as ruled (2026-10-13) and are applied to the contract as M3-D87..D91. The R74 adoption has landed (d660ee9): T4 is done and the fish skin sheet uses layers 0-5, so the M3 species layers are 6-25.
- **Binding contract:** `docs/superpowers/plans/2026-10-13-m3-interfaces.md` ("C"). Public names, paths and test names come from C 5.3, C 7.20-7.25, C 10.4 and C 12 and are not changed here; needed changes are listed below as contract change requests. **M2 contract:** `2026-10-11-m2-interfaces.md` ("C2"). **Skeleton:** `2026-10-13-m3-plan-skeleton.md` sections 2, 3, 6 (row G), 7.
- **Spec:** `docs/superpowers/specs/2026-10-09-fishgame-design.md` 11.1-11.4 (UI rules, HUD table), 12.10 (journal, hint levels, meta.json v1), 12.11 (dev tools), 12.9 (manual MPPM list), 17.3 (M3 definition of done), R74 (look).
- **Reads from other sections:** B (T8 `JournalRules`, `BitingNow`, `JournalHints`, `SilhouetteMath`, `DayClock.FormatTime`), C (T10 seed table 1.1: display names, body shapes, colours, model files), D (T14 `WorldState`, `DevWorldDriver`, `EcologySession`; T15 `FishingHost.SetRunSeed`, `FishingDebugSettings.Luck`, `DevLoadSpotRequested`), E (T16/T17 maps), F (T18/T19 looks), the R74 style guide (`docs/superpowers/research/2026-10-12-art-direction-lookdev.md`, merged on this branch with the T4 adoption, d660ee9).
- **User decisions (2026-10-13):** the R74 look is approved; the 20 new fish models do NOT gate M3's exit (batches with a user look check, fallback fish covers gaps); the M3 defaults stand (`minLandDepth` 0.5, fixed dev seed).
- **Shape:** the M2 section-plan format (`2026-10-11-m2-plan-ui-world.md`). Code appears only where a detail is load-bearing (the serializer settings, the modal seam, the router rows). No task in this section is Haiku-eligible.

---

## Contract change requests

None changes an existing public name, path or test name. Each is an addition or a manifest entry. **All five are ACCEPTED as ruled (controller, 2026-10-13): G-CR1 = M3-D87, G-CR2 = M3-D88, G-CR3 = M3-D89, G-CR4 = M3-D90, G-CR5 = M3-D91.** Every fallback below is marked "not used (CCR accepted 2026-10-13)".

1. **G-CR1 (T21): `Core/Input/PlayerInputFrame.cs` gains `public bool JournalPressed;`.** `InputContextRouter.Route` builds `PlayerIntent` from `PlayerInputFrame`, which has no journal field today (B12 only mentions `PlayerIntent`). Without it, `PlayerIntent.JournalPressed` cannot be routed and the required test `InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly` cannot be written. Add `PlayerInputFrame.cs` to the C 2.6 input row (T21) and the C 12 T21 row. No fallback: the field is required.
2. **G-CR2 (T20): T20 adds the three journal toast keys to `UI/UiStrings.cs`.** `toast.journal_new`, `toast.journal_best`, `toast.journal_unlock` are published by T20's `JournalRecorder` (Fishing cannot reference UI, so it publishes keys, not text), but C 5.3 gives the keys to T21, which runs later. Amend the C 2.6 UI row to "T2, T20 (three toast keys only), T21, T22" and the C 5.3 owner column. **Fallback (T20 publishes the keys anyway; until T21 lands, `UiStrings.Get` returns the raw key and T20's manual check accepts the raw key text): not used (CCR accepted 2026-10-13).**
3. **G-CR3 (T22): `Runtime/Input/IDevInput.cs` gains `bool IsHeld(DevKey key);`.** "F8 held = x60" (C 5.3, M3-D32) needs a held state, and `IDevInput` only has `WasPressed`. Same gating as `WasPressed` (dev build, focus, not Menu). Implemented by `PlayerInputAdapter` and `FakeInputService` (both already T22 files). Add `IDevInput.cs` to the C 2.6 input row (T22) and C 12. **Fallback (`PlayerInputAdapter.WasPressed(DevKey.ScrubClock)` returns the held state for that one key, documented in `DevKey.cs`; the fake mirrors it): not used (CCR accepted 2026-10-13).** `DevKey.ScrubClock = 6` (F8) and `DevKey.CycleForcedSpecies = 7` (F10, unchanged) match C 5.3 and `Runtime/Input/DevKey.cs`.
4. **G-CR4 (manifest, the M3-D58 precedent):** extra internal files and tests, recorded so C 12 stays complete:
   - T20: extra EditMode tests `Tests/EditMode/JournalRecorderTests.cs` (`JournalRecorder_*`, through the internal seam `JournalRecorder.Record`; Fishing already grants `InternalsVisibleTo("FishGame.Tests.EditMode")`).
   - T21: internal `UI/Journal/WorldGlyphs.cs` (painter2D glyphs for the 4 seasons, 3 M3 weathers and 6 bands; shape, not hue only). T22's clock HUD reuses it (read-only for T22).
   - T21: an internal HUD-modal seam inside `UiFlowController.cs` (T21.2); T22's dev panel uses it. No new file.
5. **G-CR5 (T23): the fish skin array grows.** C 12's T23 row lists models and "(+ R74 textures)" under `Art/Export/`, but the R74 pipeline keeps every fish skin as one 512 x 512 tile of `Assets/_Project/Content/Textures/T_FishSkinArray.png` (4 columns, imported as a `Texture2DArray`). **After the R74 adoption (d660ee9; style guide 2.2; fish commit 3de9de1) the sheet is still 4 x 2 and holds layers 0-5: 0 white, 1 bluegill, 2 perch, 3 carp, 4 trout, 5 catfish; layers 6 and 7 are white tiles.** T4 did NOT grow it, and the M3 species take layers 6-25 (batches 6-10, 11-14, 15-20, 21-25). Add to T23 (after T4, which created them):
   - `Content/Textures/T_FishSkinArray.png` (+ `.meta`), rows appended (layers 0-5 unchanged);
   - the `FishSkinArray` row of `Editor/Builders/Render/TextureArrayPostprocessor.cs` (`Rows` 2 -> 8, the max texture size 2048 -> 4096 for that array only, `GetVersion` +1; its header comment still says "2..7 white" and is corrected to the real layer list), and the T4 test that pins the 4 x 2 fish sheet: `Tests/EditMode/SmoothMeshBuilderTests.cs` `TextureArrays_ImportAsFlipbookArrays` (`skin.depth` 8 -> 32).
   Reason: 25 species + the white layer need 26 layers. A 4 x 8 sheet (32 layers) is 2048 x 4096 px, and today's `maxTextureSize = 2048` would make Unity downscale the whole sheet to 1024 x 2048 (256 px tiles). **Fallback (a second fish array `T_FishSkinArray2`, which needs a shader change): not used (CCR accepted 2026-10-13).**

## Open questions (each has a default that applies if unanswered; the controller kept all four defaults on 2026-10-13)

1. **Level 1 "sub-areas seen" (spec 12.10).** `JournalEntry` stores spots (`SeenInSpots`), not sub-areas. **Default:** level 1 lists each seen spot with the species' sub-areas in that spot's zone (from its rules). Alternative: add `SeenInSubAreas` to the DTO in M4's v2 migration (a Core DTO change; not M3).
2. **Journal icon style.** C 7.4 expected the R74 guide to cover icons; the approved guide has no icon section. **Default:** T21's recipe (T21.4): a soft anti-aliased silhouette with a back-to-belly gradient (`colorB` belly to `colorA` back), a darker rim and one comic eye. The user checks it at T21's look check. Level 0 is the same texture tinted black.
3. **Closing the journal with a gamepad.** In the Menu context only the UI map is enabled, so Select (the `Player/Journal` binding) cannot close it. **Default:** B (UI Cancel) and the Close button close it; keyboard J closes it through a UI Toolkit key handler.
4. **T23 batch order and skin layers.** **Default:** four batches (T23.2): lake commons first, then lake odd shapes and both specials, then harbour commons, then harbour odd shapes. Layers are assigned in batch order after the layers T4 used (0-5, confirmed after d660ee9), so 6-25, and never renumbered.

---

## G0. Rules shared by every task in this section

**The controller sends G0 with every T20, T21, T22, T24 and T25 brief.** T23 is the controller's own checklist.

### G0.1 Read before starting
- `CLAUDE.md`; C 2.8 (all 28 items); C 2.6 (shared-file edits); C 12 (your files); your C 7.2x block and C 10.4 rows.
- The spec sections in your task header.
- The committed code your task hooks into (named per task). Earlier M3 tasks are committed; read them, never assume their shape.

### G0.2 Constraints that bite here (C 2.8 by number)
- **#1** Dev-only surfaces (dev panel, F8, map switch, Luck) check `Debug.isDebugBuild` at runtime. Never `DEVELOPMENT_BUILD` or `#if DEBUG`.
- **#2** ASCII in code, docs and new UiStrings texts. `UI/UiStrings.cs` holds two non-ASCII M1 lines (`pause.masked`, `err.version`): edit in place with the Edit tool and never touch them. The ASCII scan checks added lines only: `git diff -U0 HEAD -- <file> | grep '^+' | grep -P '[^\x00-\x7F]'` prints nothing.
- **#8** EditMode never reaches UI or Presentation (C 4). UI tasks are gated by `compile`, `build`, full `test-edit`, full `test-play` and the written manual checks.
- **#9** PlayMode stays warning-free. A warning logged on a normal path fails PlayMode.
- **#11** Never kill Unity. Stop only if `unity.ps1` prints `LOCKED` (exit 2); a leftover `Temp/UnityLockfile` is not a lock.
- **#17** UI never writes gameplay state, except the dev-panel host calls C 7.22 names (the host calls `DevWorldDriver`, `FishingHost`, `FishingDebugSettings` directly; M3-D51).
- **#20, #21** Explicit commit paths; logs and results in `$env:FG_OUT` (scratchpad), never in the repo.
- **#23** Clients compute only cosmetics and the journal from replicated state. No new RPCs (C 8.1).
- **#24** From T14 on, a Spot scene is restored or committed as a WHOLE file, never a hunk (`WorldState`'s `GlobalObjectIdHash` follows the fileIDs).
- **#28** The procedural fallback fish stays. Missing art never blocks a build, a test or the exit.
- Tool discipline: create and edit files with the Write/Edit tools only. No sed, no Python, no heredoc rewrites. Run Unity only through `Tools/unity.ps1` with the PowerShell tool.

### G0.3 Commands (PowerShell tool; `<scratch>` = your absolute scratchpad path)
```powershell
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -TimeoutMinutes 15
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play -TimeoutMinutes 15
$env:FG_OUT = '<scratch>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 check
```
- Filtered runs in the foreground (`timeout: 600000`). Full `test-edit`, `test-play` and `check` runs with `run_in_background: true`; wait for the notification.
- The last line of every run is `FG RESULT <command> PASS|FAIL|LOCKED|TIMEOUT`. Exit 0 alone is not proof.
- `unity.ps1` passes `-dev-profile fgtest`, so PlayMode runs write `meta_fgtest.json` and `settings_fgtest.json`, never the user's own files.
- After `build`, restore scene churn with `git restore Assets/_Project/Scenes/<file>.unity` (whole files) unless your task changes the scene on purpose.

### G0.4 Pre-flight (every task)
1. `git -C C:\Dev\FishGame status --porcelain --untracked-files=all`; save it as the task baseline in the scratchpad.
2. `git -C C:\Dev\FishGame log --oneline -8`: the dependency commits in your header are present.
3. Delete the untracked `dev/` folder (git LFS hooks) if a git command leaves it behind.

### G0.5 Commit shape (C 2.1)
`git -C C:\Dev\FishGame add <explicit paths>; git -C C:\Dev\FishGame commit -m "m3(<area>): <summary>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"`. Areas here: `save`, `journal`, `ui`, `dev`, `models`, `integration`, `docs`. Integration fixes (T24, T25 only; C 2.6 rule) are named `fix(<file>): <what>` in the message. Never `git add -A` or `git add .`.

---

## Tasks

### T20 - Save service and journal recorder

**Header**
- **Goal:** spec 12.10 persistence and recording. `SaveService` reads and writes `meta.json` v1 (atomic write, `.bak` recovery, Newtonsoft, read-only for a newer schema). `JournalRecorder` applies every `CatchAnnounced` to the local save on every peer, saves when the journal changed, and raises local journal toasts. The app saves on quit.
- **Owned files (C 12 + G-CR4):** `Runtime/Save/SaveService.cs`, `Runtime/Save/MetaLoadSource.cs`, `Fishing/Journal/JournalRecorder.cs`; tests `Tests/EditMode/SaveServiceTests.cs` and the extra `Tests/EditMode/JournalRecorderTests.cs`.
- **Shared edits (C 2.6):** `App/AppBootstrap.cs` (register `SaveService` after settings; save on quit; after T2's seam), `Runtime/AssemblyInfo.cs` (`InternalsVisibleTo("FishGame.Tests.EditMode")`, marker kept), `Editor/Builders/Fishing/FishingSceneObjects.cs` (`JournalRecorder` on the `Fishing` root), `UI/UiStrings.cs` (three toast keys, G-CR2). Generated: `Scenes/Spot_LakeDock.unity` (committed whole: it gains a component).
- **Depends on:** T8 (`MetaSave`, `JournalEntry`, `JournalRules`), T15 (committed and reviewed; the lake `World` root exists from T16).
- **Implementer:** Sonnet. **Reviewer:** Opus high.
- **Spec:** 12.10 (save shape, atomic write, load order, update rules), 11.4 (toasts), R32. Rulings M3-D25 (journal kinds), M3-D30 (Newtonsoft, camelCase, read-only newer schema).
- **Constraints that bite (C 2.8):** #9 (no warnings on normal paths: "loaded from defaults" is an info line, not a warning), #23 (journal is local, from announcements only), #24 (the lake scene is committed whole), #21 (tests write under a temp directory, never under the repo or `persistentDataPath`).

#### T20.1 Public surface (C 7.20, exact)
`SaveService` (`Runtime/Save/SaveService.cs`), `MetaLoadSource` (`Runtime/Save/MetaLoadSource.cs`, `Defaults 0, Main 1, Backup 2`) and `JournalRecorder` (`Fishing/Journal/JournalRecorder.cs`) as C 7.20 gives them. Internal additions (free under C's helper rule): `SaveService.IsReadOnly { get; }` (internal), and the test seam
```csharp
// Fishing/Journal/JournalRecorder.cs - pure part of the handler (no Unity calls), so EditMode can test it.
internal static bool Record(MetaSave save, string speciesId, string speciesName, string spotId, in CatchAnnouncement a, bool isLocalCatcher,
                            out string toastKey, out string toastArg);   // returns JournalUpdate.Changed; toastKey null = no toast
```

#### T20.2 SaveService behaviour
- **Authority comment:** "Authority: local only (each machine writes its own meta file; spec 12.10, R32). Main thread. Never throws into gameplay."
- **Constructor:** `ArgumentException` on a null or empty path. `Current` starts as `new MetaSave()`, `LoadedFrom = Defaults`.
- **`CreateForThisInstance()`:** `Path.Combine(Application.persistentDataPath, MetaBaseName + InstanceProfile.Current.Suffix + ".json")`, the `SettingsStore.CreateForThisInstance` pattern.
- **`Load()`** (main -> `.bak` -> defaults, spec 12.10):
  1. `DeserializeOrNull(AtomicFile.ReadAllTextOrNull(path))`. Non-null: `Current` = it, `LoadedFrom = Main`.
  2. Else the same for `path + AtomicFile.BackupSuffix`: `LoadedFrom = Backup`, `Debug.LogWarning` once ("FG SAVE main meta file unreadable; loaded the .bak").
  3. Else `Current = new MetaSave()`, `LoadedFrom = Defaults`. If a main or `.bak` file existed but neither parsed, remember `_unreadableOnDisk = true` and log a warning once.
  4. Normalise: a null `Journal` becomes an empty dictionary. Entries and lists are kept as read (null entries stay; `JournalRules.Apply` and the UI handle them).
  5. `IsReadOnly = Current.SchemaVersion > 1`: log a warning once ("meta file schema N is newer than this build; journal is read-only this session").
  6. One info line: `FG SAVE loaded <Main|Backup|Defaults> <path>`. Then `NotifyChanged()`.
- **`Save()`:**
  - `IsReadOnly`: return (the warning was already logged). Nothing on disk changes.
  - If `_unreadableOnDisk` and the main file exists: `File.Copy(path, path + ".corrupt", true)` once, then clear the flag (an unparsable file is never silently destroyed: `File.Replace` would push it into `.bak` and the next save would overwrite it).
  - `AtomicFile.WriteAllText(path, Serialize(Current))`.
  - `IOException` and `UnauthorizedAccessException`: `Debug.LogWarning` once per service ("FG SAVE write failed: <message>"), never rethrow.
- **`NotifyChanged()`:** raises `Changed(Current)`. Each handler is invoked in its own try/catch with `Debug.LogException`, so one bad subscriber cannot block the others.
- **Serializer (the load-bearing part; M3-D30):**
```csharp
private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
{
    ContractResolver = new DefaultContractResolver
    {
        NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true },
    },
    Formatting = Formatting.Indented,
    ObjectCreationHandling = ObjectCreationHandling.Replace,   // never append to the DTO's initialiser collections
    MissingMemberHandling = MissingMemberHandling.Ignore,      // members from a newer build are skipped, not fatal
    NullValueHandling = NullValueHandling.Include,
    DateParseHandling = DateParseHandling.None,
    Culture = CultureInfo.InvariantCulture,
    MaxDepth = 32,
};
internal static string Serialize(MetaSave save) => JsonConvert.SerializeObject(save, JsonSettings);
internal static MetaSave DeserializeOrNull(string json)
{
    if (string.IsNullOrWhiteSpace(json)) return null;
    try
    {
        MetaSave s = JsonConvert.DeserializeObject<MetaSave>(json, JsonSettings);
        return s != null && s.SchemaVersion >= 1 ? s : null;      // "null", a non-object or schemaVersion <= 0 count as malformed
    }
    catch (JsonException) { return null; }
}
```
  - Newtonsoft serialises the DTOs' public fields (T8 defines fields, not properties), so the keys are `schemaVersion`, `journal`, `seen`, `crewLanded`, `caught`, `bestWeightG`, `bestLengthMm`, `bestQuality`, `seenInSpots`. Species ids are dictionary keys and stay unchanged.
  - A missing `schemaVersion` keeps the DTO initialiser value 1 (an M3 file always writes it).
  - Newtonsoft (`com.unity.nuget.newtonsoft-json`) is auto-referenced by Runtime (`overrideReferences: false`; B14). Tests never name a Newtonsoft type (C 4, M3-D30).

#### T20.3 JournalRecorder behaviour
- **Authority comment:** C 7.20 verbatim, plus "Every peer applies every announcement it receives; late joiners journal only what they witness (spec 12.10)."
- `[DisallowMultipleComponent]`. `OnEnable`: resolve the App `EventBus` (`ServiceRoots.App`, explicit null checks) and subscribe `CatchAnnounced`. `OnDisable`: dispose the subscription. No `Update`.
- **Handler** (`CatchAnnounced msg`):
  1. `a = msg.Announcement`. If `!JournalRules.ShouldJournal(a.Kind, a.Catch.IsJunk)` return (junk, `PityCast`, `None`: a PityCast's default catch is item 0, a real species; M3-D25).
  2. Resolve the App `ContentRegistry` and `SaveService` **on every announcement** (`TryGet`; cheap), so a service replaced by a test is honoured. Missing either: log once, return.
  3. `a.Catch.ItemIndex >= registry.Species.Count`: log once, return. Else `def = registry.SpeciesAt(index)`: id and display name.
  4. Spot id: the `IWorldContext` in `ServiceRoots.Game` (`SpotId`, even before `HasState`: it is serialized). Fallback: `registry.TryGetSpotByScene(gameObject.scene.name, out spot)`. Else null (`Apply` records no spot).
  5. `changed = Record(save.Current, id, name, spotId, a, msg.IsLocalCatcher, out key, out arg)`. If changed: `save.NotifyChanged(); save.Save();`.
  6. If `key != null`: publish `new ToastRequested(key, arg, 4f)` on the App bus (local only; never sent).
- **`Record`** (pure): `u = JournalRules.Apply(save, speciesId, spotId, a.Kind, a.Catch.IsJunk, isLocalCatcher, a.Catch.WeightG, a.Catch.LengthMm, a.Catch.Quality)`. At most one toast per announcement, by priority:
  - `u.FirstCatch` -> `toast.journal_new`, arg = species name;
  - else `u.PersonalBest` -> `toast.journal_best`, arg = `name + " " + (WeightG / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " kg"`;
  - else `u.LevelAfter > u.LevelBefore` -> `toast.journal_unlock`, arg = species name;
  - else none. Returns `u.Changed`.
- **Toast keys (G-CR2; ASCII, one argument each, so `ToastPresenter`'s single-argument format works):** `toast.journal_new` "New in the journal: {0}!", `toast.journal_best` "Personal best! {0}", `toast.journal_unlock` "Journal: new info on {0}".
- **Hooks:**
  - `FishingSceneObjects.AddTo`: `go.AddComponent<JournalRecorder>();` after `FightTelemetry` (C 6.3). T17's harbour gets it through the same call.
  - `AppBootstrap` (after the settings registration): `var meta = SaveService.CreateForThisInstance(); meta.Load(); services.Register(meta);`. In `HandleQuitting`, before `LeaveAsync`: `if (services != null && services.TryGet(out SaveService save)) save.Save();` (the registered instance, so a test-replaced service is the one saved). Spec 12.10 "on quit".

#### T20.4 Tests
**`SaveServiceTests.cs`** (C 10.4; each test uses its own `Path.Combine(Path.GetTempPath(), "fg-save-" + Guid.NewGuid().ToString("N"))` directory, deleted in `[TearDown]`; `LogAssert.Expect` for the expected warnings):

| Test | Cases | Catches |
|---|---|---|
| `SaveService_RoundTrip_PreservesJournal` | Two entries (`bluegill`: Seen 4, CrewLanded 3, Caught 2, BestWeightG 512, BestLengthMm 301, BestQuality 2, SeenInSpots [lake_dock, harbour_pier]; `moon_squid`: Seen 1). Save, then a new `SaveService(samePath).Load()`: `LoadedFrom == Main`, every field equal, list order kept, no `.tmp` left. | a field not serialised (for example a ushort dropped); a spot list reordered or lost |
| `SaveService_CorruptMain_LoadsBak` | Save A, then save B (so `.bak` holds A). Overwrite main with `{ "journal": ` (truncated). Load: `LoadedFrom == Backup`, `Current` equals A. Second case: main is the text `null`: Backup again. | skipping `.bak` (defaults instead); reading `.bak` first |
| `SaveService_NoFiles_Defaults` | Empty directory: `Load` gives `Defaults`, `SchemaVersion` 1, an empty journal, and creates no file. Then `Save` creates the main file and leaves no `.tmp`. | `Load` writing a file; a leftover `.tmp` |
| `SaveService_JsonKeys_CamelCasePropsIdsUnchanged` | `Serialize` of a save holding ids `gilded_carp` and `Mixed_Case_Id`: the text contains `"schemaVersion"`, `"journal"`, `"seen"`, `"crewLanded"`, `"caught"`, `"bestWeightG"`, `"bestLengthMm"`, `"bestQuality"`, `"seenInSpots"`, `"gilded_carp"`, `"Mixed_Case_Id"`, and contains neither `"SchemaVersion"` nor `"mixed_Case_Id"`. | `ProcessDictionaryKeys = true` (ids re-cased); PascalCase keys |
| `SaveService_UnknownIds_KeptOnRoundTrip` | A hand-written v1 file with `zzz_future_fish` (Caught 9) and an unknown member `"futureField": 3` inside an entry. Load: `Main`, the entry is present with Caught 9. Save, reload: still present. | filtering the journal by registry ids; `MissingMemberHandling.Error` (the file would fall back to `.bak`/defaults) |
| `SaveService_NewerSchema_NotOverwritten` | A file with `"schemaVersion": 2`, a v1-shaped journal and an extra top-level member. Load: `Main`, journal readable, one warning. `Save()` twice: the file text is byte-identical to the original, no `.tmp`, no `.bak` created. | saving over a newer file (its extra members would be lost) |
| extra `SaveService_BothCorrupt_DefaultsAndKeepsCorruptCopy` | Main and `.bak` both garbage: `Defaults`; the first `Save` leaves `meta.json.corrupt` with the old main text and a valid main. | an unreadable save silently destroyed |
| extra `SaveService_WriteFailure_LogsOnceNeverThrows` | Path whose parent "directory" is an existing file: `Save` does not throw; one warning; a second `Save` adds no second warning. | an IO exception reaching gameplay; log spam |

**`JournalRecorderTests.cs`** (extra, G-CR4; calls the internal `Record` with `new CatchAnnouncement(...)`):

| Test | Cases | Catches |
|---|---|---|
| `JournalRecorder_Record_LocalLandedNewThenBest` | Local Landed 500 g: changed, `toast.journal_new` with "Bluegill". Local Landed 400 g: no toast (level stays 2). Local Landed 700 g: `toast.journal_best` with "Bluegill 0.7 kg". | best toast on the first catch; culture-dependent decimal comma |
| `JournalRecorder_Record_WitnessUnlockOnly` | Witness Lost: `toast.journal_unlock` (level 0 -> 1). Witness Landed: unlock (1 -> 2). Witness Landed again: no toast. Caught stays 0. | a witness credited with a catch; an unlock toast on every catch |
| `JournalRecorder_Record_JunkPityAndNoneChangeNothing` | Junk Landed, `PityCast` with a default catch (item 0), `None`: returns false, no toast, the save has no entry. | journaling a PityCast's item 0 |
| `JournalRecorder_Record_OneToastPerAnnouncement` | First local Landed (FirstCatch and a level-up together): exactly the `new` toast. | two toasts for one catch |

Filter: `FishGame.Tests.EditMode.(SaveServiceTests|JournalRecorderTests)`.

#### T20.5 Steps
1. Pre-flight (G0.4). Read `Runtime/Settings/{AtomicFile,SettingsStore,InstanceProfile}.cs`, `App/AppBootstrap.cs`, `Fishing/Net/{CatchAnnounced,CatchAnnouncement}.cs`, `Fishing/Net/FishingRodNetwork.cs` (where `CatchAnnounced` is published and with which `IsLocalCatcher`), `Editor/Builders/Fishing/FishingSceneObjects.cs`, T8's `Core/Journal/*`, and `UI/ToastPresenter.cs` (single-argument format).
2. Add the `InternalsVisibleTo` line to `Runtime/AssemblyInfo.cs`. Write both test files. Red run with the filter (CS0246 for the new types counts as red).
3. Write `MetaLoadSource`, `SaveService`, `JournalRecorder`. Green run with the same filter. Mutations to show red once each, then restore: `ProcessDictionaryKeys = true`; `Save` ignoring `IsReadOnly`; `Load` skipping the `.bak` step.
4. Hooks: `FishingSceneObjects`, `AppBootstrap`, the three `UiStrings` keys (Edit tool, in place). `compile`, then `build`.
5. `git diff --stat Assets/_Project/Scenes`: `Spot_LakeDock.unity` changed (new component). Keep it WHOLE (C 2.8 #24); restore any other scene.
6. Full `test-edit` and `test-play` (background). Both PASS, PlayMode warning-free.
7. ASCII scan on every touched file (added lines only for `UiStrings.cs`).
8. Manual checks (controller, editor, solo):
   1. Land a fish: the "New in the journal" toast; `%USERPROFILE%\AppData\LocalLow\<company>\<product>\meta.json` holds the species with Caught 1 and the bests.
   2. Lose a fish (snap it): Seen +1 only.
   3. Quit play mode: the log shows no save warning. Corrupt `meta.json` by hand (truncate it), enter play mode: the log shows `FG SAVE loaded Backup` and the journal is intact.

#### T20.6 Acceptance (reviewer, Opus high)
- C 7.20 surface exact; `MetaLoadSource` values 0/1/2; `MetaBaseName` "meta".
- Load order main -> `.bak` -> defaults; each path logged; a newer schema never written; an unparsable file kept as `.corrupt` before the first overwrite.
- JSON keys camelCase, ids unchanged; no Newtonsoft type in tests.
- The recorder applies only `ShouldJournal` kinds, resolves services per announcement, saves only on change, publishes at most one local toast, sends nothing over the network.
- `JournalRecorder` is on the `Fishing` root of the lake scene; the scene is committed whole.
- `AppBootstrap` registers the service after settings and saves the registered instance on quit.
- Tests: 6 required + 2 extra in `SaveServiceTests`, 4 extra in `JournalRecorderTests`, all green; full suites green; the three mutation red runs are in the report.

#### T20.7 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Runtime/Save Assets/_Project/Runtime/Save.meta Assets/_Project/Runtime/AssemblyInfo.cs Assets/_Project/Fishing/Journal Assets/_Project/Fishing/Journal.meta Assets/_Project/App/AppBootstrap.cs Assets/_Project/Editor/Builders/Fishing/FishingSceneObjects.cs Assets/_Project/UI/UiStrings.cs Assets/_Project/Scenes/Spot_LakeDock.unity Assets/_Project/Tests/EditMode/SaveServiceTests.cs Assets/_Project/Tests/EditMode/SaveServiceTests.cs.meta Assets/_Project/Tests/EditMode/JournalRecorderTests.cs Assets/_Project/Tests/EditMode/JournalRecorderTests.cs.meta
git -C C:\Dev\FishGame commit -m "m3(save): SaveService (meta.json v1, atomic, .bak, read-only newer schema) and JournalRecorder with local journal toasts" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(Add a folder `.meta` only if `git status` lists it as new.)

---

### T21 - Journal UI (LD)

**Header**
- **Goal:** the spec 12.10 journal UI. A modal grid of 25 cards in species-id order with hint levels 0-3, the "biting now" badge, spot and biting-now filters and completion "n/25". It opens with J in game (Free, RodReady, RodWaiting) and from a main-menu Journal button (offline, from the save), and closes on J, Esc/B, the Close button and automatically on the local bite. `IconFactory` draws the R74-style silhouettes. The main menu gains the "Journal n/25" summary line.
- **Owned files (C 12 + G-CR1, G-CR4):** `UI/Journal/JournalPresenter.cs`, `UI/Journal/IconFactory.cs`, `UI/Journal/WorldGlyphs.cs` (new, internal). Edits: `UI/UiFlowController.cs`, `UI/UiStrings.cs`, `UI/MainMenuPresenter.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`, `Core/Input/{PlayerIntent,PlayerInputFrame,InputContextRouter}.cs`, `Runtime/Input/PlayerInputAdapter.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs`, `Tests/EditMode/InputContextRouterTests.cs` (one test).
- **Shared edits (C 2.6):** the UI files and the input files: T21 first, then T22. `UiStrings.cs` after T20's three keys.
- **Depends on:** T4 (look adopted; the fish colours and the icon recipe follow it), T20 (`SaveService` in App). Reads T8 (`JournalRules`, `BitingNow`, `JournalHints`, `SilhouetteMath`), T9 (`FishingZoneDef` getters, `SubAreaBand.id`, `SpawnRule.fish`/`subAreaIds`, `SpotDef.MapOrder`; M3-D76 names), T10 (species display names, shapes, colours), T14 (`IWorldContext` in `ServiceRoots.Game`).
- **Implementer:** Sonnet. **Reviewer:** Opus high, then the **user look check** (T21.8 step 9).
- **Tests:** C 10 lists only the router test (`InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly`). The UI is manual-check (C 4; M2 G-Q11 precedent); its logic rests on the T8 Core helpers, which have tests.
- **Spec:** 12.10 (levels, badge, UI), 11.1 (UI Toolkit rules, cursor unlocked while a modal is open, `IconFactory`), 11.3 (meta summary line), 11.4 (toasts), 10.2 (shape, not hue only). Rulings M3-D26..D29, M3-D50, M3-D76.
- **Constraints that bite:** #2 (UiStrings in place), #8 (no EditMode test reaches UI), #9 (no warnings on normal paths), #17 (the journal writes nothing; it reads the save, `WorldState` and the local rod's NetworkVariables).

#### T21.1 Surface (C 7.21; internal shapes are this plan's)
```csharp
// UI/Journal/JournalPresenter.cs - Authority: local only (reads the local save, replicated WorldState and the local rod; writes nothing).
internal sealed class JournalPresenter
{
    public JournalPresenter(VisualElement root, ServiceRegistry app, Action requestClose);
    public bool IsOpen { get; }
    public void Open(bool inGame);            // fills or refreshes the cards, applies the default filter, focuses Close
    public void Hide();                       // called by UiFlowController's modal-closed callback only
    public void Tick(float dt);               // only while open: auto-close on the local bite; biting-now refresh at 1 Hz
    public void Dispose();
    internal static string SummaryText(ServiceRegistry app);   // "Journal n/25" for the main menu; "" when no save or registry
}
// UI/Journal/IconFactory.cs - runtime textures only, cached by species id, never saved (spec 11.1, M3-D29)
internal static class IconFactory { internal static Texture2D Get(FishSpeciesDef def); internal static void Clear(); }
// UI/Journal/WorldGlyphs.cs (G-CR4) - season and weather glyphs and the band bar; read-only for T22
internal enum Glyph : byte { Spring, Summer, Autumn, Winter, Clear, Rain, Fog }
internal static class WorldGlyphs
{
    internal static Glyph Of(Season s);
    internal static Glyph Of(WeatherMask single);                                    // Clear, Rain, Fog; anything else -> Clear
    internal static void Draw(Painter2D p, Rect box, Glyph g, bool lit, Color ink);   // no allocation
    internal static void DrawBandBar(Painter2D p, Rect box, TimeBandMask bands, Color ink);
}
internal sealed class GlyphElement : VisualElement { /* Glyph, Lit, Ink; MarkDirtyRepaint on change; generateVisualContent -> WorldGlyphs.Draw */ }
```

#### T21.2 UiFlowController hooks (code for the modal seam; the only structural change)
```csharp
// T21: one HUD-level modal at a time (the journal; T22's dev panel). Opening sets IInputService.MenuOpen, so the Menu context applies:
// cursor free, gameplay input off, UI map on (spec 11.1). Closing restores the screen's own value. Show() and HandleBack close it first.
// The game is never paused by a modal (spec 12.10: the journal may stay open while the bobber is out).
private object _modalOwner;
private Action _modalClosed;
internal bool ModalOpen => _modalOwner != null;

internal bool TryOpenModal(object owner, Action closed)
{
    if (owner == null) throw new ArgumentNullException(nameof(owner));
    if (_modalOwner != null) return ReferenceEquals(_modalOwner, owner);
    if (_screen != UiScreen.Hud && _screen != UiScreen.MainMenu) return false;
    _modalOwner = owner;
    _modalClosed = closed;
    _input.MenuOpen = true;
    return true;
}

internal void CloseModal(object owner)
{
    if (_modalOwner == null || !ReferenceEquals(_modalOwner, owner)) return;
    Action closed = _modalClosed;
    _modalOwner = null;
    _modalClosed = null;
    _input.MenuOpen = UiFocusRouter.IsMenuScreen(_screen);
    closed?.Invoke();
}
```
Other `UiFlowController` edits (each one or two lines):
- `Show`: right after the `_bound` check, `if (_modalOwner != null) CloseModal(_modalOwner);`.
- `HandleBack`: first line `if (_modalOwner != null) { CloseModal(_modalOwner); return; }` (Esc and gamepad B close the modal, never open Pause behind it).
- `BindScreens`: `_journal = new JournalPresenter(_root, _app, () => CloseModal(_journal));` and `_menu.JournalRequested += OpenJournal;` (before the button click-sound loop, which covers the UXML buttons).
- `OpenJournal()`: `if (!TryOpenModal(_journal, OnJournalClosed)) return;` then, over the main menu, `UiQuery.Show(_mainMenu, false)` (so gamepad navigation cannot reach the menu behind it); then `_journal.Open(_screen == UiScreen.Hud)`.
- `OnJournalClosed()`: `_journal.Hide();` and, when `_screen == UiScreen.MainMenu`, show the menu again and schedule focus on `JournalButton`.
- `Update`:
  - opening: `if (_screen == UiScreen.Hud && _modalOwner == null && _input.Intent.JournalPressed && Time.frameCount != _screenChangedFrame) OpenJournal();`;
  - `if (_journal.IsOpen) _journal.Tick(Time.unscaledDeltaTime);` (also on the main menu);
  - skip `_focus.RefocusIfLost` while `ModalOpen` (it would focus the hidden menu).
- `Show(UiScreen.MainMenu)`: after `_menu.Refresh()`, `_menu.SetMetaSummary(JournalPresenter.SummaryText(_app))`.
- `OnDestroy`: `_journal?.Dispose()`.

#### T21.3 Input routing (G-CR1; code for the router rule)
- `PlayerInputFrame` gains `public bool JournalPressed;` and `PlayerIntent` gains `public bool JournalPressed;` (comment: "routed in Free, RodReady, RodWaiting only, M3-D27").
- `InputContextRouter.Route`: contexts without their own route fall to `RouteFree` (Carrying, Staggered, Downed, Carried, Seated; C2 D18), so the rule is enforced once after the switch:
```csharp
public static PlayerIntent Route(InputContext context, in PlayerInputFrame frame)
{
    PlayerIntent i;
    switch (context) { /* the existing cases, assigning i instead of returning */ }
    // M3-D27: the journal opens only on foot or with the rod ready or waiting.
    i.JournalPressed = frame.JournalPressed && (context == InputContext.Free || context == InputContext.RodReady || context == InputContext.RodWaiting);
    return i;
}
```
  `CopyAll` is unchanged (it does not copy `JournalPressed`).
- `PlayerInputAdapter`: `_journal = _actions.FindAction(InputActionNames.Journal, throwIfNotFound: true)`; `f.JournalPressed = _journal.WasPressedThisFrame();` (the action is disabled in the Menu context with the rest of the Player map, so it reads false there). Remove the M1 comment "Journal is bound ... not routed".
- `FakeInputService`: nothing beyond compiling (its `Frame` field gains the member automatically); confirm it routes through `InputContextRouter`.
- Test `InputContextRouter_Journal_PassesInFreeRodReadyRodWaitingOnly` (`InputContextRouterTests.cs`): a frame with `JournalPressed = true` routed through every `InputContext` value (`Enum.GetValues`): true exactly for Free, RodReady and RodWaiting. Catches: Journal passed in RodAiming/RodFighting/Menu, or in the fall-through contexts (Seated, Carrying) via `RouteFree`.

#### T21.4 IconFactory (the R74 icon recipe; open question 2)
- Canvas 256 x 128, `TextureFormat.RGBA32`, no mips, sRGB (`linear: false`: species colours are sRGB Inspector colours), `FilterMode.Bilinear`, `TextureWrapMode.Clamp`, `hideFlags = HideAndDontSave`, `name = "JournalIcon_" + id`. One static `Color32[]` scratch buffer; `SetPixels32`, then `Apply(false, true)` (the CPU copy is released).
- Layout: head on the left (u = 0), tail to the right. `aspect = SilhouetteMath.AspectRatio(shape, k)`; body length `L = min(196, 104 x aspect)` px; centre the body plus its tail vertically and horizontally.
- Body coverage per pixel: `u = (x - x0) / L`, `h = SilhouetteMath.HalfHeight(shape, k, u) x L`, `cov = clamp01(h - |dy| + 0.5)` (1 px anti-alias).
- Extras, by `BodyShape`: Round/Long/Flat get a forked tail fin (two triangles from u 0.92, length 0.22 L, spread 0.7 of the body's max half-height); Eel gets none; Squid gets three tentacle strips trailing from u 1 (3 px thick, 0.35 L long, rounded ends).
- Paint (soft "realistic toon", spec R74 guide 2.3/2.5): back-to-belly gradient `lerp(colorB, colorA, smoothstep(-0.7, 0.7, dy / h))` (back up); a soft rim: the outer 2.5 px darken toward `colorA x 0.55`; a faint top sheen (+8% value in the upper third); clamp the result into the guide's 0.15-0.85 value range. One comic eye at u 0.14 (Squid 0.30), `dy = +0.25 h`: white disc radius `max(3, 0.18 x maxHalfHeight)`, black pupil 0.55 of it shifted forward, a 1 px white glint.
- Level 0 reuses the same texture with `style.unityBackgroundImageTintColor = (0.05, 0.06, 0.08)`: a black silhouette, the eye hidden (spec 12.10).
- `Clear()` destroys every cached texture (`Object.Destroy`, explicit null checks) and empties the cache; called from `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
- Cost: 25 x 32,768 pixel evaluations once, on the first journal open; nothing per frame.

#### T21.5 UXML, USS and strings
**`AppRoot.uxml`:**
- Main menu: `<ui:Label name="MetaSummary" class="hint" />` under `MenuTitle`; `<ui:Button name="JournalButton" class="menu-button" />` after `SettingsButton`.
- After `ScreenPause` (drawn above the HUD and the menu; the runtime toast stack is appended later and stays on top):
```xml
<ui:VisualElement name="JournalModal" class="screen journal hidden">
    <ui:VisualElement name="JournalPanel" class="panel journal-panel">
        <ui:VisualElement class="row journal-header">
            <ui:Label name="JournalTitle" class="title small" />
            <ui:Label name="JournalCompletion" class="label journal-completion" />
            <ui:Button name="JournalClose" class="menu-button journal-close" />
        </ui:VisualElement>
        <ui:VisualElement name="JournalFilters" class="row journal-filters">
            <ui:Button name="JournalFilterAll" class="journal-filter" />
            <ui:VisualElement name="JournalSpotFilters" class="row" />
            <ui:Toggle name="JournalFilterBiting" class="setting journal-filter-biting" />
        </ui:VisualElement>
        <ui:ScrollView name="JournalScroll" class="grow">
            <ui:VisualElement name="JournalGrid" class="journal-grid" />
        </ui:ScrollView>
    </ui:VisualElement>
</ui:VisualElement>
```
**Cards** are built in code once (one per journal species) inside `JournalGrid`: `journal-card` (focusable, so keyboard and gamepad navigation reach every card; `FocusInEvent` scrolls it into view like `SettingsScroll`), with children `journal-icon` (128 x 64, background image), `journal-name`, `journal-badge` (hidden unless biting), `journal-info` (wrapping text), `journal-glyphs` (4 season `GlyphElement`s, 3 weather `GlyphElement`s, one band-bar element with the labels `Da Mo Mi Af Du Ni` under it).

**`App.uss`** (new classes only; no M1/M2 rule changes):
- `.journal` dims the screen (`rgba(6, 10, 16, 0.72)`); `.journal-panel` 92% wide (max 1500 px), 88% high, centred.
- `.journal-grid` `flex-direction: row; flex-wrap: wrap;`. `.journal-card` 232 px wide, min-height 230 px, margin 6 px, rounded 8 px, panel background; `.journal-card:focus` a 3 px accent border (shape cue, not hue only).
- Level classes `level-0` .. `level-3` (level 0: dimmer card, italic "???").
- `.journal-badge`: a pill with a 2 px border and a leading "!" (text plus shape; spec 10.2).
- `.journal-filter.selected`: bold with a 3 px bottom border.
- At 1280 x 720 and UI scale 150% the grid wraps to fewer columns and scrolls; nothing clips (spec 11.1 layout targets).

**UiStrings keys (T21; ASCII; C 5.3 `journal.*`, `menu.journal`, `menu.meta_journal`):**

| Key | Text |
|---|---|
| `menu.journal` | Journal |
| `menu.meta_journal` | Journal {0}/{1} |
| `journal.title` | Journal |
| `journal.completion` | {0}/{1} caught |
| `journal.close` | Close |
| `journal.filter_all` | All spots |
| `journal.filter_biting` | Biting now only |
| `journal.biting` | ! Biting now |
| `journal.unknown` | ??? |
| `journal.hint_spot` | Maybe at: {0} |
| `journal.seen_at` | Seen at: {0} |
| `journal.weight_range` | Weight: {0}-{1} kg |
| `journal.best` | Best: {0} kg, {1} mm, {2} |
| `journal.caught` | Caught: {0} |
| `journal.season_spring`, `_summer`, `_autumn`, `_winter` | Spring, Summer, Autumn, Winter |
| `journal.weather_clear`, `_rain`, `_fog` | Clear, Rain, Fog |
| `journal.quality_normal`, `_good`, `_trophy` | Normal, Good, Trophy |

(The season and weather names are reused by T22's clock HUD.)

#### T21.6 JournalPresenter behaviour
- **Services**, resolved on each `Open` with explicit null checks: App `ContentRegistry`, `SaveService`, `AudioBus`; the `IWorldContext` in `ServiceRoots.Game` (null on the menu). No registry or save: the modal shows the title and an empty grid, and logs once.
- **Journal species (M3-D50):** every species that has a rule in some zone of `registry.Ecology`, plus every `IsSpecial` species, sorted by id (ordinal). Built once per registry instance. The total (25) is the list count, never a literal.
- **Cards:** created once on the first `Open` and then updated in place (texts are assigned only when they change). Level = `JournalRules.HintLevel(entry)` (a missing or null entry is level 0).
  - **Level 0:** icon tinted black; name `journal.unknown`; info `journal.hint_spot` with the display names of every spot whose zone has a rule for the species (M3-D28: `JournalHints.Summarize` zones -> spots with that `ZoneIndex` in `eco.Spots` -> `registry.SpotAt(i).DisplayName`, joined by ", ").
  - **Level 1:** coloured icon; the display name; `journal.seen_at` with, per id in `SeenInSpots`, the spot's display name (an unknown id prints as the id) and in brackets that spot's sub-areas for the species (open question 1): the `SubAreaBand.id`s named by the species' rules in that zone's `FishingZoneDef` (empty `subAreaIds` = every band), title-cased with underscores as spaces ("open_water" -> "Open Water"). `journal.caught` when Caught > 0.
  - **Level 2:** adds the glyph row from `JournalHints.Summarize` (season and weather glyphs lit where the mask has the bit, unlit as faint outlines; the band bar filled per band) and makes the badge possible.
  - **Level 3:** adds `journal.weight_range` (`WeightMinG/1000` and `WeightMaxG/1000`, "0.0") and `journal.best` (best kg "0.0", mm, quality name). Invariant culture everywhere.
- **Biting now (level >= 2; M3-D26):** only when opened in game and the world context has `HasState`.
  - Context: `ZoneIndex` = the current spot's zone (`registry.SpotIndex(world.SpotId)` -> `eco.Spots[i].ZoneIndex`; any lookup failure: no badges); `Minute`, `Season`, `Weather` from the world; `RodTier` = the tier of the local rod (`FishingRodController.Local`'s `FishingRodNetwork.NetRodIndex` -> `registry.RodAt(i).Tier`, else 0); `Bait` = the local selected bait (`NetSelectedBait` -> `registry.BaitAt(b).ToCore().Mods(1)`, else `BaitMods.Neutral(0)`); `PlayersInSession` = the session's connected count (at least 1); `Events`/`Skills` neutral; `InHotspot` false; `Luck` 0. `BitingNow` itself forces any sub-area and infinite depth and passes a null ledger.
  - `BitingNow.IsBiting(resolver, eco.Zones[zone], speciesIndex, ctx)` with one cached `FishingResolver` per `EcologyDatabase` instance.
  - Refreshed on `Open`, at 1 Hz while open, and on the world's `Changed` (subscribed while open). No allocation in the refresh.
- **Filters:** "All spots" plus one button per registry spot, ordered by `MapOrder` then id (runtime-created buttons play `Ui_Click` themselves: M2 carry rule). A spot filter shows the species with a rule in that spot's zone. Default filter: the current spot in game, All on the menu. The "Biting now only" toggle is disabled on the menu and without world state; when on, only cards showing the badge are visible. Filtering toggles `display`; cards are never rebuilt.
- **Completion:** `journal.completion` with `JournalRules.CompletionCount(save.Current, ids)` and the list count. `SummaryText` gives `menu.meta_journal` with the same numbers.
- **Live updates while open:** subscribe `SaveService.Changed` on open (a crew catch can land while the journal is open) and refresh the cards; unsubscribe on hide.
- **Closing (M3-D27):**
  - `Tick` in game: if `FishingRodController.Local` has a machine in `Bite` or `Hooked`, call `requestClose` (the bite cue closes it; the owner is never left fighting behind a modal). `Open(inGame: true)` refuses in those two states as well.
  - The premise "a modal keeps the bobber out" was checked in code on 2026-10-13: no Fishing or Core fishing code reacts to `InputContext.Menu` or `MenuOpen` (only `PlayerInteractor` reads `MenuOpen`), and the M2 online Pause already holds `MenuOpen` with a cast out. A rod in Ready loses its held aim input while the modal is open, which is acceptable.
  - Keyboard J: a `KeyDownEvent` callback on `JournalModal` (`TrickleDown`) for `KeyCode.J` calls `requestClose` and stops propagation; it ignores key-repeat events and the frame the journal opened on. Esc and gamepad B arrive through `HandleBack`. The Close button calls `requestClose`.
  - Focus: `Open` schedules focus on `JournalClose`; closing over the menu returns focus to `JournalButton`.
- **`Dispose`:** unsubscribes everything; calling it twice is safe.

#### T21.7 MainMenuPresenter
- New: `public event Action JournalRequested;` raised by `JournalButton`; `public void SetMetaSummary(string text)` (hides `MetaSummary` when empty). Static texts from `menu.journal`.
- `UpdateButtons` leaves `JournalButton` enabled (it reads only the local save).
- No other change.

#### T21.8 Steps
1. Pre-flight (G0.4). Read `UI/UiFlowController.cs`, `UI/UiFocusRouter.cs`, `UI/MainMenuPresenter.cs`, `UI/Fishing/FishingHudPresenter.cs` (patterns: `UiQuery.Req`, painter2D, local rod lookup), `UI/SettingsPresenter.cs` (`FocusInEvent` scrolling), `Core/Input/InputContextRouter.cs`, `Runtime/Input/PlayerInputAdapter.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs`, T8's `Core/Journal/*` and `Core/Render/SilhouetteMath.cs`, T9's `Data/Ecology/*` getters, T20's `SaveService`, and the adopted R74 style guide.
2. Router first (TDD): add the test; red (CS1061 `JournalPressed`); add the two fields and the router rule; green with `test-edit -Filter "FishGame.Tests.EditMode.InputContextRouterTests"`. Mutation once: route Journal through `RouteFree` only (the Seated case goes red). Then the adapter line; `compile`.
3. `WorldGlyphs` and `IconFactory`. `compile`.
4. UXML, USS, `UiStrings` (Edit tool, in place), `MainMenuPresenter`, `JournalPresenter`, the `UiFlowController` hooks. `compile`.
5. `build`; restore scene churn (whole files). Full `test-edit` and `test-play` (background): PASS, PlayMode warning-free (the PlayMode fake never presses J).
6. ASCII scan on all touched files (added lines only for `UiStrings.cs`).
7. Commit (T21.10).
8. **Controller manual checks** (editor; record PASS/FAIL):
   1. Main menu: "Journal n/25" shows; Journal opens over the menu with the cursor free; gamepad navigation moves across cards and filters only; B and Esc close; focus returns to the Journal button.
   2. Solo, fresh profile (delete `meta.json`): all 25 cards at level 0 (black silhouettes, "???", "Maybe at: Lake Dock" or "Harbour Pier"). Land a bluegill: its card shows level 2 (name, "Seen at: Lake Dock (Reeds, Open Water)", glyphs). Land it two more times: level 3 with the weight range and best.
   3. Lose a fish: level 1 only for that species.
   4. With the rod waiting, press J: the journal opens, the bobber stays out, the player cannot move. When the fish bites, the journal closes by itself and the fight starts normally (also with F7 twitchy: "Click!" is reachable).
   5. J does nothing while charging, fighting, swimming or climbing.
   6. Biting now: with the dev panel (after T22) or by waiting, change the season, weather or band and see the badges follow the rules; switch rods (F4) to see a rod-gated species (for example `glimmer_eel`, rod 1, in Summer Dusk Rain) appear and disappear. "Biting now only" filters to badged cards.
   7. Layout at 1280 x 720 with UI scale 150%: nothing clips; the grid scrolls.
   8. MPPM (host + client): a client's catch updates the host's open journal live (witness: level 1 or 2), and the client's own journal shows Caught.
9. **User look check:** the controller sends the user a screenshot of the open journal (levels 0-3 visible) and of the icons for all 25 species (the user can force each species with F10 to reach level 1). The user approves the icon style (open question 2) or asks for changes; changes are an `IconFactory` follow-up commit.

#### T21.9 Acceptance (reviewer, Opus high)
- The router test passes; `JournalPressed` is set only in Free, RodReady and RodWaiting, including the fall-through contexts.
- The modal seam is the only structural `UiFlowController` change; `Show` and `HandleBack` close an open modal first; the modal never pauses the solo game.
- Level content matches spec 12.10 and M3-D28; the badge appears only at level >= 2 and uses the local rod tier and bait (M3-D26); completion counts Caught >= 1 over the journal list (M3-D50).
- The journal closes on the local Bite/Hooked state, J, Esc/B and Close; it refuses to open during a bite.
- `IconFactory`: cached runtime textures only, destroyed on `SubsystemRegistration`; nothing saved to disk; level 0 is a tint of the same texture.
- No gameplay writes; no allocation per frame while closed; the 1 Hz refresh does not allocate.
- Shape cues accompany colour (badge pill, selected-filter border, lit vs outline glyphs).

#### T21.10 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/UI/Journal Assets/_Project/UI/Journal.meta Assets/_Project/UI/UiFlowController.cs Assets/_Project/UI/UiStrings.cs Assets/_Project/UI/MainMenuPresenter.cs Assets/_Project/UI/Uxml/AppRoot.uxml Assets/_Project/UI/Uss/App.uss Assets/_Project/Core/Input/PlayerIntent.cs Assets/_Project/Core/Input/PlayerInputFrame.cs Assets/_Project/Core/Input/InputContextRouter.cs Assets/_Project/Runtime/Input/PlayerInputAdapter.cs Assets/_Project/Tests/PlayMode/Fakes/FakeInputService.cs Assets/_Project/Tests/EditMode/InputContextRouterTests.cs
git -C C:\Dev\FishGame commit -m "m3(ui): journal - 25 cards, hint levels 0-3, biting now, filters, IconFactory silhouettes, J routing, main-menu Journal" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(`FakeInputService.cs` only if it changed.)

### T22 - Dev panel and HUD clock

**Header**
- **Goal:** spec 12.11 dev tools and the M3 HUD clock. F3 cycles Off -> Overlay -> Panel -> Off. The panel edits, on the host, season, minute, weather override, run seed (before the session's first cast), luck, the local rod tier, "new day", "spawn hotspot at crosshair", the dev clock on/off and the map (dev map switch); clients see the same values read-only. F8 held scrubs the host clock x60. The HUD shows the time ("2:20 PM") with season and weather glyphs. The F3 overlay gains one WORLD line.
- **Owned files (C 12 + G-CR3):** `UI/World/ClockHudPresenter.cs`, `UI/Dev/DevPanelPresenter.cs` (new). Edits: `UI/Fishing/DevOverlayPresenter.cs`, `UI/UiFlowController.cs` (the hook lines), `UI/UiStrings.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`, `Runtime/Input/{DevKey,IDevInput,PlayerInputAdapter}.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs`.
- **Shared edits (C 2.6):** after T21 in every shared UI and input file. Uses T21's modal seam and `WorldGlyphs` read-only.
- **Depends on:** T15 (`FishingHost.SetRunSeed`, `FishingDebugSettings.Luck`, `DevLoadSpotRequested`, the world context in casts), T20, T21. Reads T14 (`WorldState`, `DevWorldDriver`, `EcologySession`), T8/T5 (`DayClock.FormatTime`).
- **Implementer:** Sonnet. **Reviewer:** Opus high.
- **Tests:** none in C 10 (manual-check UI task, C2 G-Q11 precedent). Gate: `compile`, `build`, full `test-edit`, full `test-play`, the manual checks.
- **Spec:** 12.11, 11.4 (clock row: top-left, 10-minute steps; M3-D19: no day counter), 17.3 bullet 4, R34 (solo pause stops the clock, never `timeScale`). Rulings M3-D31, M3-D32, M3-D51.
- **Constraints that bite:** #1 (every dev surface checks `Debug.isDebugBuild` at runtime; the non-development build shows the clock but no overlay, panel or F8), #17 and #23 (clients never change world state: the panel's host calls are made only when this peer is the server; no RPCs), #9 (no warnings on normal paths).

#### T22.1 Surface (C 7.22) and hooks
```csharp
internal sealed class ClockHudPresenter : IHudExtension   // UI/World/; Authority: local only (reads IWorldContext)
internal sealed class DevPanelPresenter : IHudExtension   // UI/Dev/; Authority: host edits through direct host calls; clients read-only
    // ctor (this plan): DevPanelPresenter(UiFlowController flow, DevOverlayPresenter overlay)
// UI/Fishing/DevOverlayPresenter.cs (edit) gains the shared F3 mode:
internal enum DevHudMode : byte { Off, Overlay, Panel }
//   internal DevHudMode Mode { get; }  internal void SetMode(DevHudMode m);  internal event Action<DevHudMode> ModeChanged;
// Runtime/Input/DevKey.cs: ScrubClock = 6 (F8)            Runtime/Input/IDevInput.cs (G-CR3): bool IsHeld(DevKey key);
```
- `UiFlowController.BindScreens`: `AttachHudExtension(new ClockHudPresenter());` after `FishingHudPresenter`, and the M2 dev line becomes
  `if (Debug.isDebugBuild) { var overlay = new DevOverlayPresenter(); AttachHudExtension(overlay); AttachHudExtension(new DevPanelPresenter(this, overlay)); }`.
- `PlayerInputAdapter.WasPressed`/`IsHeld`: `DevKey.ScrubClock` maps to `Key.F8`; `IsHeld` uses `isPressed` with the same gating as `WasPressed` (dev build, window focus, not Menu). `FakeInputService` implements `IsHeld` from a settable held set (false by default), so PlayMode never scrubs.

#### T22.2 F3 mode cycle (DevOverlayPresenter edit)
- The M2 `_visible` bool becomes `Mode`. F3 (through `IDevInput`, so only outside the Menu context) steps Off -> Overlay -> Panel. The overlay label shows in Overlay and Panel.
- The Panel -> Off step cannot arrive through `IDevInput` (the open panel is a modal, so the context is Menu). It comes from the panel: its own F3 key handler, its Close button, or Esc/B via the modal close.
- `DevPanelPresenter` subscribes `ModeChanged`:
  - entering Panel: `flow.TryOpenModal(this, OnModalClosed)`; refused (the journal is open): `overlay.SetMode(Overlay)`;
  - leaving Panel: `flow.CloseModal(this)`;
  - `OnModalClosed` (Esc, B, a screen change): if `Mode == Panel`, `SetMode(Off)`.
- **WORLD line**, after the LOADOUT line, built with the existing `StringBuilder`, invariant culture:
  `WORLD spot=<SpotId> min=<Minute> (<FormatTime>) season=<Season> weather=<Weather> hotspot=<x,z r=<R> until <EndMinute> | none> open=<CastingOpen>`, plus on the host ` day=<Run.Day> clock=<run|stop> scrub=<on|off> casts=<Run.HostCastCounter> seed=0x<RunSeed:X16>` (from `DevWorldDriver` and `EcologySession.Current`). No world or no state: `WORLD none`.
- USS: `.dev-overlay` moves from `top: 16px` to `top: 56px` (the clock takes the top-left; spec 11.4).

#### T22.3 Dev panel behaviour
**Layout** (`AppRoot.uxml`, inside `ScreenHud`): `<ui:VisualElement name="DevPanel" class="dev-panel hidden" picking-mode="Position">` with a header label, rows and a Close button:

| Row | Elements | Host action |
|---|---|---|
| Season | 4 buttons Spring..Winter (names from `journal.season_*`) | `driver.DevSetSeason(s)` |
| Time | `SliderInt DevMinute` 0..1200 and a label (`FormatTime`) | `driver.DevSetMinute(v)`, applied 0.2 s after the last slider change (one `NetClock` write per drag) |
| Weather | 4 buttons Track (None), Clear, Rain, Fog | `driver.DevSetWeatherOverride(w)` |
| Clock | Toggle "Dev clock running", button "New day", hint "Hold F8: clock x60" | `driver.DevSetClockRunning(b)`, `driver.DevNewDay()` |
| Hotspot | button "Spawn hotspot at crosshair", status label | ray from `Camera.main` at the viewport centre, `Physics.Raycast(ray, out hit, 100f, CastProbe.CastMask, QueryTriggerInteraction.Collide)`; a hit on layer Water: `driver.DevSpawnHotspotAt(hit.point.x, hit.point.z)`; else `dev.no_water` (the F9 rule: the first hit wins, so the dock blocks it) |
| Run seed | TextField (decimal or `0x` hex, invariant parse to `ulong`) and "Apply" | `FishingHost.Current.SetRunSeed(seed)`; the row is disabled with `dev.seed_locked` once `EcologySession.Current.HostCastCounter > 0`; a bad parse shows `dev.bad_seed` |
| Luck | `Slider` -1..1 and a value label | `FishingDebugSettings.Luck = v` |
| Rod tier | 4 buttons 0..3 | the lowest rod index whose `RodAt(i).Tier == t`; refused with `dev.refused_cast` unless the local machine is Idle; else `localRod.SelectRodRpc(index)` (the host's own rod; the host mirrors `NetRodIndex`) |
| Load map | one button per registry spot (runtime-created, `MapOrder` order; they play `Ui_Click` themselves); the current spot's button disabled | `bus.Publish(new DevLoadSpotRequested(spot.Id))` on the App bus (`SceneFlow` loads it, host only, M3-D31) |

**Rules:**
- Host = `NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer`. Driver = `WorldState.Current.GetComponent<DevWorldDriver>()`, cached per `WorldState` instance (a map switch replaces both).
- **Clients:** the header reads `dev.panel_title_client`, and the rows are disabled (`SetEnabled(false)`). Values come from the replicated world (season, minute, weather, hotspot); run-state values show "n/a (client)". No call is ever made.
- No world, no state or no driver: the header shows `dev.no_world`, rows disabled, Load map still enabled on the host (to leave a broken scene).
- **Refresh at 4 Hz while the panel is shown:** `SetValueWithoutNotify` for the slider and toggle, so a refresh never triggers an action; selected buttons get the `selected` class (bold plus a border: shape, not hue only).
- **Keys:** a `KeyDownEvent` callback (`TrickleDown`) on `DevPanel` for `KeyCode.F3` sets Off. It ignores key-repeat events and the frame the panel opened on, so the F3 press that opened the panel cannot close it. Open focuses the first season button.
- **F8 scrub** (`Tick`, every mode, host only): `held = dev != null && dev.IsHeld(DevKey.ScrubClock)`. When `held` differs from the last value sent to the current driver, call `driver.DevScrub(held)` (the driver re-anchors on press and release only). A new driver instance resets the sent value to false. `Unbind` sends `DevScrub(false)` if it was held. Focus loss and the Menu context read as "not held", so alt-tab or opening a menu always releases the scrub.
- Bind never throws (M2 carry rule): every lookup is null-checked.

#### T22.4 Clock HUD
- UXML (inside `ScreenHud`, `picking-mode="Ignore"`, not focusable): `ClockHud` (row) with `ClockTime` (Label), `ClockSeason` (`GlyphElement`), `ClockSeasonName` (Label), `ClockWeather` (`GlyphElement`), `ClockWeatherName` (Label). USS `.clock-hud`: `position: absolute; left: 16px; top: 16px;` 18 px text with the `.hud-prompt` outline; glyphs 20 x 20.
- `Tick`: resolve the `IWorldContext` in `ServiceRoots.Game` (re-resolve on a reference change; subscribe its `Changed`, unsubscribe the old one). None, or `!HasState`: hide.
- Time: `step = Minute / DayClock.DisplayStepMinutes`; only when `step` changes, `ClockTime.text = UiStrings.Format("hud.clock", DayClock.FormatTime(Minute))`. No string is built on other frames.
- Season and weather: on `Changed` and at bind, set the glyphs (`WorldGlyphs.Of`, lit) and the names (`journal.season_*`, `journal.weather_*`). The glyph shapes differ per value, so the read does not depend on colour (spec 10.2).
- Shown in every build (the clock runs in non-development builds too); M4 adds "Day n/7" (M3-D19).

#### T22.5 Strings (ASCII; `hud.clock` and `dev.*`, C 5.3)
`hud.clock` "{0}"; `dev.panel_title_host` "Dev panel (host)"; `dev.panel_title_client` "Dev panel (client: read-only)"; `dev.no_world` "No world state in this scene"; `dev.season` "Season"; `dev.time` "Time {0}"; `dev.weather` "Weather"; `dev.weather_track` "Track"; `dev.clock_running` "Dev clock running"; `dev.new_day` "New day"; `dev.scrub_hint` "Hold F8: clock x60"; `dev.spawn_hotspot` "Spawn hotspot at crosshair"; `dev.run_seed` "Run seed"; `dev.apply_seed` "Apply"; `dev.seed_locked` "Locked after the first cast"; `dev.bad_seed` "Not a seed (decimal or 0x hex)"; `dev.luck` "Luck {0}"; `dev.rod_tier` "Rod tier"; `dev.load_map` "Load map"; `dev.close` "Close". The M2 keys `dev.host_only`, `dev.refused_cast`, `dev.no_rod`, `dev.no_water` are reused.

#### T22.6 Steps
1. Pre-flight (G0.4). Read `UI/Fishing/DevOverlayPresenter.cs`, T21's `UiFlowController` modal seam and `WorldGlyphs`, `Runtime/Input/{DevKey,IDevInput,PlayerInputAdapter}.cs`, `FakeInputService.cs`, T14's `WorldState`/`DevWorldDriver`/`EcologySession`, T15's `FishingHost` (`SetRunSeed`, `RunSeed`), `FishingDebugSettings`, `SceneFlow`, `CastProbe`.
2. Input: `DevKey.ScrubClock = 6`, `IDevInput.IsHeld`, the adapter and the fake. `compile`.
3. `ClockHudPresenter`, the overlay mode and WORLD line, `DevPanelPresenter`, UXML, USS, strings (Edit tool, in place), hooks. `compile`.
4. `build` (restore scene churn, whole files); full `test-edit`; full `test-play` (background). PASS, PlayMode warning-free.
5. ASCII scan (added lines only for `UiStrings.cs`).
6. Commit (T22.8).
7. **Controller manual checks** (editor; record PASS/FAIL):
   1. Solo: the clock reads 6:00 AM at start, steps every 10 in-game minutes, shows the season and weather glyphs and names. Pause (Esc): the time freezes; resume: it continues without a jump (R34).
   2. F3: overlay (with the WORLD line) -> panel (cursor free, player frozen) -> F3 again: everything off. Esc and the Close button also close the panel. Opening the journal while the panel is open is refused and vice versa.
   3. Panel, host: each season button changes the HUD glyph, the WORLD line and the palette (T18); the time slider jumps the clock and the sky; weather Track/Clear/Rain/Fog change the WORLD line and the looks (T19); "New day" resets to 6:00 AM with day + 1; the dev clock toggle stops and restarts the time.
   4. Hold F8: the clock runs x60; release: normal. Alt-tab while holding: the scrub stops.
   5. "Spawn hotspot at crosshair" over water: the hotspot VFX appears there (T19) and the WORLD line shows it; over the dock: "No water under crosshair".
   6. Run seed: apply a new seed before casting: the season and weather track change (WORLD line); after one cast the row is locked.
   7. Luck 1.0: landed fish skew heavier over about 10 catches (F6 forced bite helps).
   8. Rod tier 3 while Idle: the LOADOUT line shows the Deep-sea rod; while Waiting: "Refused: cast active".
   9. Load map Harbour Pier (after T17): the harbour loads, the player respawns, the WORLD line keeps day, season and cast counter; load Lake Dock back.
   10. MPPM (host + client): the client's panel is read-only and shows the host's values; the client's F8 does nothing to the clock; the client cannot change anything.
   11. A non-development player build: the clock shows; F3, F8 and F4-F10 do nothing; no panel, no exception.

#### T22.7 Acceptance (reviewer, Opus high)
- Dev surfaces: the hook and every key path are guarded by `Debug.isDebugBuild`; no `#if`.
- Every world-changing call is behind the host check and goes to `DevWorldDriver`, `FishingHost` or `FishingDebugSettings` directly; there is no RPC and no client write (M3-D51). Rod tier uses the existing `SelectRodRpc` on the host's own rod only.
- The F3 cycle is Off -> Overlay -> Panel -> Off and the panel uses T21's modal seam; Esc closes it; a screen change closes it.
- F8 sends `DevScrub` only on press and release, releases on focus loss, Menu, unbind and driver change.
- The clock label allocates only on a 10-minute step; the panel refreshes at 4 Hz only while shown, with `SetValueWithoutNotify`.
- The time slider and seed field cannot write while a refresh runs; the seed is refused after the first accepted cast.
- Glyphs and selection carry shape cues; strings ASCII; the M1 non-ASCII lines untouched.

#### T22.8 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/UI/World Assets/_Project/UI/World.meta Assets/_Project/UI/Dev Assets/_Project/UI/Dev.meta Assets/_Project/UI/Fishing/DevOverlayPresenter.cs Assets/_Project/UI/UiFlowController.cs Assets/_Project/UI/UiStrings.cs Assets/_Project/UI/Uxml/AppRoot.uxml Assets/_Project/UI/Uss/App.uss Assets/_Project/Runtime/Input/DevKey.cs Assets/_Project/Runtime/Input/IDevInput.cs Assets/_Project/Runtime/Input/PlayerInputAdapter.cs Assets/_Project/Tests/PlayMode/Fakes/FakeInputService.cs
git -C C:\Dev\FishGame commit -m "m3(dev): dev panel (host-edited world, seed, luck, rod tier, map switch), F3 cycle, F8 scrub, HUD clock with season and weather glyphs" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(Folder `.meta` files only if new; `UI/World` may already exist from an earlier task.)

### T23 - Fish models (controller checklist; not a subagent brief)

**Header**
- **Who:** the controller (Opus), driving Blender through Blender MCP (`mcp__blender__*`). No subagent, no worktree. The Unity import waits for a free lock (between implementer runs).
- **Goal (C 7.23):** 20 new species models in the approved R74 look, in four batches with a user look check per batch, imported to `Content/Models/` and wired by `FishingContentBuilder`'s model fill. **Off the critical path (Q2, user decision 2026-10-13):** a species without a model reveals with `FishFallbackMesh` (C 2.8 #28); T25 may run with T23 partial.
- **The 20 (C 7.23, section C plan 1.1):** lake `mirror_minnow`, `bucketmouth_bass`, `silver_mullet`, `lake_pike`, `frost_char`, `glimmer_eel`, `sleepy_sturgeon`; harbour `pier_sardine`, `ice_herring`, `grumble_mackerel`, `moon_squid`, `gawping_gurnard`, `sneaky_flounder`, `red_snapper`, `grumpy_puffer`, `bruiser_tuna`, `storm_ray`, `lanternfish`; specials `gilded_carp`, `old_barnacle`. The other five reuse the M2 models as restyled by T4 (M3-D33; T4 restyled the bluegill and, by 3de9de1, perch, carp, trout and catfish, with skin layers 1-5); any model that proves un-restyled is listed as a gap in T25's art table, not modelled here.
- **Owned (C 12 + G-CR5):** `Art/Blender/fishgame_m3_species.blend`, `Art/Export/Fish_<Pascal>.fbx` (20), `Assets/_Project/Content/Models/Fish_<Pascal>.fbx` + `.meta`, `Assets/_Project/Content/Textures/T_FishSkinArray.png` + `.meta`, the `FishSkinArray` row of `Editor/Builders/Render/TextureArrayPostprocessor.cs`, and the regenerated species assets (`model:` refs only).
- **Depends on:** T4 (R74 adopted on the branch: `FishGame/Toon` with the skin array, `M_Toon_Fish`, `FishModelPostprocessor`, `FishingValidators.SpeciesModels` as T4 left it, the restyled reused fish and the layers they use), T10 (all 25 species assets exist with `model` null for the 20).
- **Review:** Sonnet per batch (import settings, LFS pointers, validators, asset diffs, triangle counts, layer table), then the **user look check**. The commit trailer names the controller's model (C 2.1).
- **Never:** `generate_3d`, `import_asset` or `search_assets` (CLAUDE.md: no external or generated assets; models are scripted from primitives). Never save over `C:\Dev\FishGame-lookdev\Art\Blender\fishgame_lookdev.blend`.

#### T23.1 Inputs
1. The adopted style guide (`docs/superpowers/research/2026-10-12-art-direction-lookdev.md`, merged with the T4 adoption, d660ee9): 2.1 (fish highlight 0.30 at power 180, the wet look), 2.2 (skin array: 512 x 512 sRGB albedo tiles, layer = `round(uv1.y)`, gain 1), 2.3 (lit albedo between about 0.15 and 0.85 linear; near-white only for highlights and eye whites, near-black for pupils), 2.4 (subdivision level 1 over a low cage, solidified fins 3.5-4.5 mm, about 4k triangles, test budget under 5,000, smooth normals), 2.5 Fish (loft of superellipse rings, side-projected UV0 boxes, eyes in the tile's top strip, UV "Layer" = (0, layer), baked AO in the vertex colour, one submesh, no materials, `bake_space_transform`).
2. The bluegill as the reference: 3,876 triangles, one 512 x 512 tile at layer 1 of `T_FishSkinArray`, generated by the text blocks `fg_bluegill.py` (model) and `fg_bluegill_paint.py` (paint and bake) in the look-dev `.blend`.
3. Per species: `bodyShape`, `colorA`, `colorB`, model file name (section C plan 1.1); kg range, tags, rarity and profile (spec 12.6); `lengthShapeK` (M3-D35: eel 1.6, puffer 0.8). The skin uses `colorA` for the back and `colorB` for the belly, so the model, the fallback mesh and the journal icon agree.
4. Character (memory rule "fish art direction": whacky or magical is welcome, but each stays recognisable and comic; big comic eyes like the bluegill). One line per fish, a starting point the user may change:

| id | Look |
|---|---|
| mirror_minnow | tiny silver minnow with a chrome-mirror stripe along the flank and oversized eyes |
| bucketmouth_bass | bass with a comically huge bucket mouth and a dark side stripe |
| silver_mullet | slim silver fish with a "mullet": a long tuft of dorsal fin at the back of the head |
| lake_pike | long duck-bill snout with a toothy grin; olive with pale spots |
| frost_char | char with frost-white fin edges, ice speckles and a red belly |
| glimmer_eel | dark blue eel with bright cyan spots along a ribbon fin (magical) |
| sleepy_sturgeon | armour scutes, droopy half-closed lids, whisker barbels |
| gilded_carp | golden carp with coin scales and a little crown-shaped dorsal fin (Legendary sheen) |
| old_barnacle | grizzled old fish crusted with barnacles, bushy brow |
| pier_sardine | small blue-backed sardine with a worried look |
| ice_herring | pale icy herring with a frosted blue back |
| grumble_mackerel | teal mackerel with tiger stripes and a permanent frown |
| gawping_gurnard | red gurnard with a huge open gape and wing-like pectoral fins |
| sneaky_flounder | flat flounder, both eyes on top with shifty sideways pupils, sandy camouflage |
| red_snapper | bright red with an underbite showing two teeth |
| grumpy_puffer | round yellow puffer with soft spikes and a grumpy brow |
| bruiser_tuna | chunky navy tuna with a boxer's jaw and a scar |
| storm_ray | slate ray with a lightning-bolt pattern on the wings (magical) |
| lanternfish | dark fish with a glowing yellow lure on a stalk and bright photophores |
| moon_squid | pale lavender squid with a crescent-moon mark on the mantle (magical) |

"Glow" is painted brightness only: `FishGame/Toon` has no emission, so keep it within the guide's highlight rule.

#### T23.2 Batches and skin layers (open question 4)
Lake first (the default map), the specials early (rare but memorable), then the harbour after T17 lands.

| Batch | Species (count) | Skin layers (T4 used 0-5, confirmed after d660ee9) |
|---|---|---|
| 1 | mirror_minnow, bucketmouth_bass, silver_mullet, lake_pike, frost_char (5) | 6-10 |
| 2 | glimmer_eel, sleepy_sturgeon, gilded_carp, old_barnacle (4) | 11-14 |
| 3 | pier_sardine, ice_herring, grumble_mackerel, gawping_gurnard, red_snapper, grumpy_puffer (6) | 15-20 |
| 4 | moon_squid, sneaky_flounder, bruiser_tuna, storm_ray, lanternfish (5) | 21-25 |

- Layer rule: the layers already used are 0 white, 1 bluegill, 2 perch, 3 carp, 4 trout, 5 catfish (style guide 2.2, section "Texture arrays"; do NOT trust the header comment of `TextureArrayPostprocessor.cs`, which still says "2..7 white" and is corrected in batch 1). Layers 0-5 keep their numbers and their tiles. The 20 new species take layers 6-25 in the order above. A layer is never renumbered (the FBX carries it in uv1).
- Sheet: 4 columns, layer = row x 4 + column, rows appended at the bottom. The sheet is 4 x 2 today (layers 0-7; layers 6 and 7 are white tiles that batch 1 fills). The 26 used layers need 7 rows; the sheet is made 4 x 8 (2048 x 4096 px, 32 layers) once, in batch 1, with the unused tiles 26-31 white (G-CR5).

#### T23.3 Batch 1 only: set up the source file and the sheet import
1. `mcp__blender__get_addon_status` and `get_scene_info`. Note the Blender version in the report.
2. New file, saved as `C:\Dev\FishGame\Art\Blender\fishgame_m3_species.blend`. Append the two bluegill text blocks without opening the look-dev file:
   `with bpy.data.libraries.load(r"C:\Dev\FishGame-lookdev\Art\Blender\fishgame_lookdev.blend") as (src, dst): dst.texts = [t for t in src.texts if t in ("fg_bluegill.py", "fg_bluegill_paint.py", "fg_fish.py")]` (the adoption added a generic `fg_fish.py` with one SPECIES entry per model, used for perch, carp, trout and catfish, style guide 2.5; load it too)
   (T4 has merged: the look-dev file is tracked on the branch as `C:\Dev\FishGame\Art\Blender\fishgame_lookdev.blend`; prefer that copy and load from it the same way, never saving over it).
3. Generalise them into the text blocks below; keep the bluegill's export call, smoothing and colour-attribute settings exactly (they produced the approved model). **The imported `fg_fish.py` is already generic (SPECIES entries with stations, fins, barbels, eye, paint): extend it with the 20 new entries instead of writing a second `fg_fish.py`, and fold the `build(id)` description below into it; the other two blocks are new:**
   - `fg_species_table.py`: one record per species: id, Pascal name, layer, `bodyShape`, `colorA`, `colorB`, ring profile (length, height and width ratios, superellipse exponents per ring), fin set, eye size and position, pattern recipe, the comic twist.
   - `fg_fish.py`: `build(id)`: cage loft, subdivision level 1 (applied), solidified fins, comic eyes, UV0 side-projection boxes, UV map "Layer" set to (0, layer), AO baked into the colour attribute, triangle count printed (`FG FISH <id> tris=<n>`).
   - `fg_fish_paint.py`: `paint(id)` bakes the 512 x 512 tile in object space; `compose_sheet()` writes `C:\Dev\FishGame\Assets\_Project\Content\Textures\T_FishSkinArray.png` (2048 x 4096, sRGB, tiles in layer order, layer 0 and unused tiles white, the existing layers copied unchanged from the current sheet). Tiles are not packed into the `.blend` (it stays small in LFS); the script regenerates them.
4. Edit `TextureArrayPostprocessor.cs` (G-CR5): give `ArraySpec` a max size, set the fish array to 4 x 8 and 4096 (the environment array stays 4 x 4 and 2048), bump `GetVersion`, update the layer comment (it must list layers 0-5 as above). The T4 test that pins the 4 x 2 fish shape, `Tests/EditMode/SmoothMeshBuilderTests.cs` `TextureArrays_ImportAsFlipbookArrays` (`skin.depth` 8 -> 32), is updated in the same commit.

#### T23.4 Per fish (every batch)
1. `build(id)` and `paint(id)` through `execute_blender_code`. Read the printed triangle count: target 3,500-4,500, hard limit 4,999.
2. Look at it (`mcp__blender__look`, small `max_size`): side, top and three-quarter views. Check the nose direction (nose ends up +Z in Unity after the bluegill's export axes), one mesh, eyes readable, fins solid, the silhouette matches its `bodyShape` (the journal icon and fallback use the same shape), colours match `colorA`/`colorB`, nothing faceted.
3. Export with the bluegill's call to `C:\Dev\FishGame\Art\Export\Fish_<Pascal>.fbx` (selection only, one submesh, no materials).

#### T23.5 Per batch: look check, import, verify, commit
1. **Blender look check (user):** render one lineup of the batch (side and three-quarter, the bluegill at one end for scale) to the scratchpad, show it to the user with the one-line looks, and add a "Look at fish batch N" row to the board's "Waiting on you" list (memory rule "user todo board"). Iterate in Blender until the user approves the shapes and skins. The user may also defer ("import as is"); record which.
2. `compose_sheet()`. Save the `.blend`.
3. **Import** (editor closed; the first `unity.ps1` command reports LOCKED otherwise; ask the user to close it):
   - copy the batch's FBX files from `Art/Export/` to `Assets/_Project/Content/Models/` (new files: Unity creates the `.meta`);
   - `build`. Expected log lines: `FG MODELS Fish_<Pascal>.fbx tris=<3500-4999> submeshes=1 colours=yes` per new model, the sheet reimported as a `Texture2DArray` of depth 32, and the model fill setting each batch species' `model` (it fills only null refs).
4. **Verify:**
   - `git diff Assets/_Project/Content/Fishing/Species`: only `model:` lines change, only for the batch's species.
   - Each new `.meta` has the `FishModelPostprocessor` settings (materials none, normals imported, no animation/cameras/lights, scale 1, not readable, no compression).
   - Full `test-edit` (`Validation_SpeciesModels_Pass`, `SeedSpecies_Models_AreFishModelMeshes_UnderTriangleBudget`, `EcologyData_ModelFiles_ReuseFourM2Models`, the render validators) and `test-play`: PASS.
   - **In-game render:** use T4's committed capture tool if the branch has one (`LookDevCapture`); otherwise the M2 T29 temporary lineup script pattern (`Editor/Dev/TempFishLineup.cs`, never committed, deleted after): each batch fish with `M_Toon_Fish` next to its `FishFallbackMesh`, a nose marker, and a log line per model with `subMeshCount`, colour attribute, triangle count and the first vertex's `uv1.y` (it must equal the layer table). Run it with a graphics device (no `-nographics`). Check: right skin on every fish (a wrong layer shows another fish's skin or plain white), nose direction, nothing pink or black, sizes plausible.
   - Restore unintended scene churn (whole files).
5. **LFS:**
   - `git -C C:\Dev\FishGame check-attr filter -- <each new .fbx, the .png, the .blend>` prints `filter: lfs` (`.gitattributes` tracks `*.fbx`, `*.png`, `*.blend`).
   - After staging: `git -C C:\Dev\FishGame show :Assets/_Project/Content/Models/Fish_<Pascal>.fbx` starts with `version https://git-lfs.github.com/spec/v1` (the index holds pointers).
6. **Commit** (explicit paths; one commit per batch):
```powershell
git -C C:\Dev\FishGame add Art/Blender/fishgame_m3_species.blend Art/Export/Fish_<P1>.fbx ... Assets/_Project/Content/Models/Fish_<P1>.fbx Assets/_Project/Content/Models/Fish_<P1>.fbx.meta ... Assets/_Project/Content/Textures/T_FishSkinArray.png Assets/_Project/Content/Fishing/Species/<id1>.asset ...
# batch 1 also: Assets/_Project/Editor/Builders/Render/TextureArrayPostprocessor.cs (and any updated T4 test)
git -C C:\Dev\FishGame commit -m "m3(models): fish batch <N> - <ids> (R74, skin layers <a>-<b>)" -m "LFS: +<x.x> MB this commit, <y.y> MB total stored" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
   Then `git -C C:\Dev\FishGame lfs ls-files` lists every new FBX (both folders), the sheet and the `.blend`. Delete the untracked `dev/` folder if it appears.
7. **LFS size report** (in the batch report and the commit message):
   - This commit: `git -C C:\Dev\FishGame lfs ls-files -s HEAD~1 HEAD` (files that changed between the two refs, with sizes); sum them. If that git-lfs version does not take two refs, list `git diff --name-only HEAD~1 HEAD`, keep the LFS-tracked paths (`check-attr`) and sum their working-tree sizes.
   - Total stored: every LFS object version counts against GitHub's 1 GB storage quota (K17), not only the current files. Sum the unique object sizes of `git -C C:\Dev\FishGame lfs ls-files --all -s` with a small scratchpad script (parse the `(<n> KB|MB|GB)` suffix; count each OID once). The 2026-10-13 baseline is about 8 MB.
   - Expected growth: per batch about 1 MB of FBX (two copies of about 105 KB per fish), 0.3-1 MB for the `.blend` version and 3-5 MB for the new sheet version; about 25 MB for the four batches.
   - Thresholds: above 500 MB total, note it in the board; above 800 MB, stop and ask the user before the next art commit. Also mention GitHub's monthly LFS bandwidth (every fresh clone downloads the current versions).
8. **Board and in-game check:** mark the batch's board row done when the user approves, and add the in-game check (land each new fish: F10 cycles the forced species, F6 forces the bite) to the board. In-game tweaks become a follow-up commit `m3(models): batch <N> fixes - <ids>` (the `.meta` files keep the GUIDs).

#### T23.6 Report per batch (for the Sonnet reviewer and T25)
The `FG MODELS` lines (triangles, submeshes, colours); the layer table rows; the meta checks; the validator and suite results; the lineup PNG paths (scratchpad); the user's verdict (approved, approved with notes, deferred); the LFS numbers (this commit and total stored).

#### T23.7 Acceptance (Sonnet reviewer, per batch)
- Every new model: one submesh, a colour attribute, 3,500-4,999 triangles, under `Content/Models/`, the importer settings above, its `uv1.y` equal to its table layer.
- The sheet is 2048 x 4096, and the imported `Texture2DArray` is 512 x 512 with depth 32 (this checks the G-CR5 premise that the size cap would otherwise shrink the tiles); earlier layers unchanged (a pixel compare of the old tiles, or the lineup showing the bluegill and the reused fish unchanged).
- Species asset diffs touch only `model:` and only the batch's species; no other asset or scene changes are committed.
- All binaries are LFS pointers in the commit; the size report is present.
- The user's verdict is recorded. A missing model never fails a build or a test (C 2.8 #28).

### T24 - PlayMode integration

**Header**
- **Goal:** prove the M3 systems together in one solo-host process (spec 12.9 PlayMode, 17.3): a lake cast through the resolver lands, announces and journals; an off-grid cast is rejected; the world clock runs and stops in solo pause; `WorldState` is spawned with a non-zero hash; the harbour loads and lands a fish; the dev map switch keeps the run state; a day-over cast is rejected and a new day re-opens casting. Fix the integration defects these tests expose.
- **Owned files (C 12):** `Tests/PlayMode/EcologySoloTests.cs` (+ integration fixes under the C 2.6 rule).
- **Integration-fix rule (C 2.6):** T24 may fix defects in earlier M3 files with the public surface frozen; each fix is named `fix(<file>): <what>` in the commit message. A signature change is a contract change: stop and report.
- **Depends on:** T16, T17, T20 (C 3), and in the serial order T21 and T22 are committed. Reads T14/T15 (`WorldState`, `DevWorldDriver`, `EcologySession`, `DevLoadSpotRequested`, `FishingDebugSettings`), T13 (`FishingHostLedger`), C2 7.19 (`FishingPlayModeDriver`, `FishingRig`, `LogWatch`, `PlayModeHarness`), T15's `FishingWorldTests` (the map-switch wait pattern).
- **Implementer:** Sonnet. **Reviewer:** Opus high.
- **Spec:** 12.9 (PlayMode paragraph), 17.3, 14.2 (clock replication), R34 (solo pause).
- **Constraints that bite:** C2 #9 (warning-free: every test ends with `rig.Log.AssertEmpty`), #24 (no scene edits; restore churn whole), C 4 (PlayMode does not reference Presentation; it may reference Gameplay and Fishing, and Fishing grants PlayMode its internals).

#### T24.1 Test rules
- Reuse `FishingPlayModeDriver` (C2 7.19) unchanged: `Arrange`, `EquipAndReady`, `ChargeAndRelease`, `WaitForState`, `FightRhythm`, `TearDown`.
- `[UnityTearDown]`: `FishingPlayModeDriver.ResetDebugSettings()`, then `EcologySession.Clear()` (the T14 note: `SubsystemRegistration` runs once per Play Mode entry, not per test), then the driver teardown.
- Counters and journal values are asserted RELATIVE (read before, compare after), never absolute: the `fgtest` meta file persists between runs.
- Fixed world state where a result depends on it: the host's `DevWorldDriver` (`WorldState.Current.GetComponent<DevWorldDriver>()`) is set before the cast (clock stopped, a fixed minute, season, weather override). Every wait is bounded (`PlayModeHarness.WaitUntil` with a message).
- The map-switch wait (T15 pattern): publish `DevLoadSpotRequested` on the App `EventBus`, then wait (20 s realtime) for a new `WorldState.Current` with `HasState` and the expected `SpotId`, a new `FishingHost.Current`, `PlayerNetState.Local` respawned, `FishingRodController.Local` non-null, and the loadout mirrored.
- Harbour aim: no Editor types in PlayMode, so the test finds the scene's `StandPoint` components, teleports to each in turn facing `FacingDeg`, readies the rod and keeps the first whose marker is valid (fail with the list of tried points if none is).
- Rejections: observe the host verdict the way the existing fishing PlayMode tests do (read `FishingSoloTests` and T15's `FishingWorldTests` first; reuse their probe of the owner's received result or the host ledger). The assertion is the `CastRejectReason`, plus "no active cast on the host" and "bait count unchanged".

#### T24.2 Tests (C 10.4, exact names)

| Test | Steps and asserts | Catches |
|---|---|---|
| `EcologySolo_LakeCastThroughResolver_LandsAnnouncesAndJournals` | Arrange (G9). The G9 short cast lands about 14 m from the shore, in `deep_water` (sub-area 2): the test computes the landing's `ShoreM` and sub-area from the zone grid first and asserts 2. Driver: clock stopped, minute 360 (Midday), season Spring, override Clear: per spec 12.6 the only regular deep-water species there at the Handmade rod is `yellow_perch` (Darter, a short fight), plus the rare `gilded_carp` special. The test logs the eligible set (`CollectEligible`) and, if T12 retuned the rules so that it holds a Bulldog or Runner, picks another season/band from the asset values whose eligible set is Steady/Darter only and says so. `ForcedBiteWait = 0.1`, no forced species. Subscribe `CatchAnnounced` on the App bus. Up to 4 casts: cast, wait Hooked, `FightRhythm` (150 s game-time bound), until a Landed FISH (junk continues the loop). Assert: one announce with `Kind == Landed`, `IsLocalCatcher`, a species index whose lake rule passes in that context for some sub-area (`FishingResolver.CollectEligible` with the world values and the local rod and bait); the App `SaveService` entry for that species has Seen +1, CrewLanded +1, Caught +1, `SeenInSpots` contains `lake_dock`, and `meta_fgtest.json` on disk holds the same Caught (reload a second `SaveService` on its path). | a roll bypassing the resolver; the recorder not attached or not saving; the journal keyed by the wrong spot |
| `EcologySolo_OffGridCast_RejectedNotWater` | Arrange (G9), then turn the player to face yaw 180 (toward the shore; re-teleport with that rotation) so the request is honest apart from its target. Equip and ready. Forge one `CastRequestRpc` from the current origin with yaw 180 and full charge (Handmade: the landing is near z -61, beyond the shore and the grid's edge), so the host's computed landing (`Ledger.TryComputeLanding`) is a cell the baked grid calls not water (assert `grid.IsWater(lx, lz) == false` first). Assert reason `NotWater`, no active cast, bait count unchanged, the owner back in Idle within 3 s. | a host that rolls without the grid/probe gate; a stuck owner after a rejection |
| `EcologySolo_WorldClock_RunsAndStopsInSoloPause` | Arrange. Read `MinuteF`; after 3 s realtime it advanced by 4 +- 0.5. Press Pause through the fake input (Esc): `NetClock.Value.Running == 0`, and `MinuteF` stays within 0.01 over 2 s. Press again (resume): no jump at resume (< 0.2) and it advances again. `Time.timeScale` is 1 throughout (R34). | a clock that ignores solo pause; a pause done with `timeScale`; a jump on resume |
| `EcologySolo_WorldState_SyncedWithNonZeroHash` | Arrange. `WorldState.Current` is spawned, its `NetworkObject.GlobalObjectIdHash != 0`, `HasState`, `SpotId == "lake_dock"`, it is the `IWorldContext` in `ServiceRoots.Game`, `Season == EcologySession.Current.Season`, `NetClock.Value.MinutesPerSecond == DayClock.DefaultMinutesPerSecond`, and `Weather` equals the driver's track at the current day and segment (override None). | an in-scene object with hash 0 (not spawned); a world not registered as the context |
| `EcologySolo_HarbourLoad_SpawnsPlayerAndLandsAFish` | Arrange (no teleport). Switch to `harbour_pier`. Find a stand point with a valid marker. `ForcedBiteWait = 0.1`, `ForcedSpeciesId = "pier_sardine"` (speed and determinism; the resolver path is test 1). Cast, Hooked, `FightRhythm`, Landed. Assert the announce, a creel +1, the journal entry's `SeenInSpots` contains `harbour_pier`. | a harbour scene missing a per-scene hook (spawn, rod, fishing host, recorder, grid) |
| `EcologySolo_DevMapSwitch_KeepsRunStateAndCastCounter` | Arrange; `Luck = 0.5`, `ForcedBiteWait = 30`. Cast and cancel at the lake: `n = run.HostCastCounter`. Switch to the harbour: same `EcologySession.Current` reference, same `Season`, `Day`, counter `n`, `Luck` still 0.5, minute restarted (< 30). Cast and cancel there: `n + 1`. Switch back to the lake: still `n + 1`; cast: `n + 2`. | a counter that restarts per scene (B11: replayed catch seeds); dev overrides lost on a Spot load (T1) |
| `EcologySolo_DayOver_CastRejectedThenNewDayAccepts` | Arrange (G9). Driver: `DevSetClockRunning(false)`, `DevSetMinute(1200)`: `CastingOpen` false. Equip and ready WITHOUT the driver's marker assert: `MarkerValid` is false over water. Forge a valid G9 `CastRequestRpc`: rejected `NotAllowedNow`, bait unchanged. `DevNewDay()`: `CastingOpen` true, `Run.Day` +1, minute < 1. The marker turns valid; a normal cast reaches Waiting. | an owner gate that disagrees with the host (M3-D16); a day-over that never reopens |

Filter: `FishGame.Tests.PlayMode.EcologySoloTests`.

#### T24.3 Steps
1. Pre-flight (G0.4). Read the files in the header, `Tests/PlayMode/FishingSoloTests.cs` (driver, `LogWatch`, how a rejection and the bait count are observed), T15's `Tests/PlayMode/FishingWorldTests.cs`, `PlayModeHarness.cs`, T14's `WorldState`/`DevWorldDriver`, T16's `StandPoint`.
2. Write the seven tests. Run `test-play -Filter "FishGame.Tests.PlayMode.EcologySoloTests" -TimeoutMinutes 15` (background).
3. Each failure: diagnose with `superpowers:systematic-debugging`. A test defect is fixed in the test; a product defect is fixed in its file under the integration-fix rule (one named fix each; public surface frozen). Re-run the filter.
4. Full `test-edit` and full `test-play` (background): PASS, warning-free. Record the PlayMode duration of the new file (`FG TIME` if printed, else the run time difference); over 3 minutes: report it.
5. Restore scene churn (whole files). Commit (T24.5).

#### T24.4 Acceptance (reviewer, Opus high)
- The seven names exactly; each asserts the behaviour named, with bounded waits and relative counters; teardown clears `EcologySession` and the debug settings.
- Test 1 goes through the resolver (no forced species) and checks both the announce and the saved journal.
- Forged requests only exercise the host gates; nothing in the tests writes gameplay state except through the dev driver, the debug settings and the input fake.
- Every integration fix is minimal, named, and keeps the public surface; no scene is changed.
- Full suites PASS, PlayMode warning-free.

#### T24.5 Commit
```powershell
git -C C:\Dev\FishGame add Assets/_Project/Tests/PlayMode/EcologySoloTests.cs Assets/_Project/Tests/PlayMode/EcologySoloTests.cs.meta <integration-fix files, if any>
git -C C:\Dev\FishGame commit -m "m3(integration): EcologySoloTests - resolver cast to journal, off-grid and day-over rejects, clock and pause, WorldState, harbour, map switch" -m "<one line per fix(<file>): <what>, if any>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T25 - Acceptance and branch review hand-off

**Header**
- **Goal (C 7.25):** write `docs/superpowers/plans/2026-10-13-m3-acceptance.md` (every 17.3 bullet with an owner and a how-to-check, the 12.9 MPPM list, the dev-panel direction checks, F8 scrub and readability at 25 m, balance tuning items, art status per species); update `README.md` ("Ecology dev panel", "Journal / meta.json"); check the two-build `ContentHash` equality; run the final `check`; hand off to the Opus xhigh branch review.
- **Owned files (C 12):** `docs/superpowers/plans/2026-10-13-m3-acceptance.md` (new), `README.md`. Integration fixes under the C 2.6 rule.
- **Depends on:** all of T1-T24 committed; T23 may be partial (Q2). Inputs: the T11 coverage and variety prints, T12's V4/V5 tables and `EcologyTuningItems`, T16/T17 reachability counts, T18's scrub numbers, T23's batch reports, the skeleton section 8 risks and their status. Ask the controller for any report not in the repo.
- **Implementer:** Sonnet. **Reviewer:** Sonnet (docs), then **Opus xhigh** on the whole branch (T25.6).
- **Tests:** none new. The gate is the final `check` and the two-build hash check.

#### T25.1 Acceptance doc: required structure
The delivered M2 doc's style (`2026-10-11-m2-acceptance.md`, read it first): a criteria table with a "Status <date>" column (automated rows filled with the pasted result, manual rows "Open (user)"), then one `### Criterion n:` block per row with Steps / Expected / Record / `Result: ______`; numbered sub-sections (`F.1`, `F.2`, ...) for reports; a checkbox per manual item; plain ASCII. Sections in order:

**Header block.** Date; spec 17.3; contract; who runs it (the user for manual rows, Claude for automated rows); how to record; setup help (README "Local multiplayer testing", "Ecology dev panel", "Journal / meta.json").

**Before you start.** All M3 tasks committed; `check` prints `FG RESULT check PASS`; `FishGame > Build All` has run; Auto Solo off for MPPM; the `Direct4P` scenario and the `NetSim_150ms_2pct` preset exist; a development player build exists for the version-mismatch item; the open M2 acceptance items are run or explicitly deferred by the user (entry gate 1.2 #5).

**A. Criteria table (17.3), then one block per row:**

| # | Criterion | Owner | How to check |
|---|---|---|---|
| 1 | 23 species, 2 specials, 7 junk (`old_boot` shared), 2 spots, the weather table, 5 profiles and 2 baked grids with the 12.6 values | Claude | `test-edit` rows `EcologyData_SeedsMatchRoster`, `EcologyData_AssetValuesEqualSeeds`, `EcologyData_SevenJunkOldBootShared`, `EcologyData_SubAreasMatchSpec125`, `Validation_EcologyMaps_Pass`; paste the pass lines |
| 2 | `WeatherDirector` pre-rolls 35 segments from one seed, deterministically, with the season held for the week | Claude + User | `WeatherDirector_SameSeed_SameThirtyFiveSegments`, `WeatherDirector_RollSeason_DeterministicAndRoughlyUniform`, `DevWorld_MapSwitchResume_RebuiltTrackIdentical`. User: press "New day" 7 times in the dev panel; the WORLD line's season never changes and the weather follows the same sequence in a second session with the same seed |
| 3 | SceneBuilder builds `Spot_HarbourPier`; both grids are re-baked in place (stable GUIDs); the lake passes reachability | Claude | `build` twice: the two grid `.asset.meta` GUIDs are unchanged (`git diff` empty for the `.meta` files); `ShoreGridBaker_LakeEverySubArea_TwentyFiveReachableCastableCells`, `ShoreGridBaker_HarbourEverySubArea_TwentyFiveReachableCastableCells`, `HarbourPier_SceneHasEveryPerSceneHook`, `HarbourPier_Rebuild_ByteIdenticalMeshes`; paste the per-sub-area cell counts |
| 4 | `PlaceholderRoller` replaced by `FishingResolver`; bait has `BaitMods`; registry indices and `ContentHash` identical across two builds and editor sessions | Claude (T25.3) | `git grep -n PlaceholderRoller -- Assets` finds nothing; the two-build hash check (T25.3) prints identical hashes and index lists |
| 5 | 12.9 validators, fightability, resolver tests and 13.3 variety pass; balance reports pass or have recorded tuning items | Claude | `test-edit` (the Tuning and Balance categories run in the full suite); paste the T11 coverage and variety prints and the T12 V4/V5 tables into section F; every miss is an `EcologyTuningItems` entry with numbers |
| 6 | Dev panel changes to season, band, weather, rod tier or hotspot visibly change catches in the direction the data predicts | User | Section C |
| 7 | `DayNightController` and the seasonal palette work on the dev clock with no pops (F8 scrub); bobber, marker and dock edge readable at night and in maximum fog at 25 m; the fog readability EditMode test passes | Claude + User | `DayNight_ScrubFullDay_NoLightOrFogPops`, `DayNight_FogReadableAt25m_EveryPhaseAndWeather`, `DayNightMath_RealPalette_EveryPhaseAndWeatherReadable`, `DayNightMath_IsReadable_DensityBoundAtPoint0333`; user: section D |
| 8 | MPPM: every `CatchAnnounced` shows identical species, weight, quality and value on all peers; journals right for catcher and witness; clients cannot change world state | User | Section B |
| 9 | The journal persists in `meta.json` v1, recovers from a corrupt main file via `.bak`, and shows levels 0-3 and "biting now" correctly | Claude + User | `SaveService_*` tests (6 required + extras); user: section E |
| 10 | Core compiles with `noEngineReferences`; Fishing does not reference Networking or UI | Claude | the existing architecture test (find it by grepping the EditMode tests for `noEngineReferences`; paste its name and pass line) plus a read of `Core/FishGame.Core.asmdef` and `Fishing/FishGame.Fishing.asmdef` references |
| 11 | M2 carry list (C 14) | Claude | the T1/T2 tests from C 10.1 pass; list them |
| 12 | R74 art: models, harbour props, journal icons | User | Section G (art status) and the T17/T21/T23 look verdicts |

**B. Manual MPPM list (12.9 manual, 17.3).** Host plus 1-3 clients, dev-direct, the 150 ms / 2% preset on the clients:
- ME1: a client's catch shows the same species, weight, quality and value in the host's and the client's toasts and reveal (and the owner's F3 ROLL line).
- ME2: journals: the catcher's card goes to Caught +1; every witness gets Seen and CrewLanded; a lost fish gives Seen only, on every peer.
- ME3: a hotspot spawned from the host panel shows its VFX on every peer at the same place.
- ME4: a `dataVersion` bump on one instance is rejected at approval with the version message. Recipe: MPPM clones share the assets, so use the development player build: bump `dataVersion` in the editor's `ContentRegistry.asset` Inspector, host in the editor, join from the player: "Different game version"; revert the asset.
- ME5: a late joiner sees the same minute (within one 10-minute display step), season, weather and an active hotspot.
- ME6: a client's dev panel is read-only, its F8 does nothing, and nothing a client does changes the world (WORLD lines match the host).
- ME7: a dev map switch with a client connected: the client follows to the harbour, respawns, and its WORLD line matches the host's (day, season); switch back.
- ME8: the day-over boundary: with the host clock stopped at 02:00 (panel), both peers' markers are invalid and a client cast is refused; "New day" re-opens casting on both.

**C. Dev-panel direction checks (17.3 bullet 4).** For each lever: the panel settings, the prediction from spec 12.6 (and the T11 coverage print), the method (F6 forced bite with no forced species; about 15 casts per setting; tally the toasts), and the record. Rows:

| Lever | Setting A -> B (other values fixed) | Predicted change |
|---|---|---|
| Season | lake, open water, Midday, Clear: Summer -> Winter | Summer: `mirror_minnow`, bluegill, `grumpy_carp`, `sunny_trout`; Winter: `mirror_minnow` and `yellow_perch` only |
| Band | lake, open water, Summer, Clear: Midday -> Night | Midday: `mirror_minnow`, bluegill, `sunny_trout`, `grumpy_carp`; Night: `grumpy_carp`, `bucketmouth_bass`, `silver_mullet` |
| Weather | lake, Summer, Dusk, reeds or open water: Clear -> Rain | `mudcat` appears only in Rain (and Fog) |
| Rod tier | lake, Summer, Dusk, Rain, deep water: tier 0 -> tier 1 | `glimmer_eel` appears only at tier 1 or more |
| Hotspot | lake, same spot, 20 casts without and with a hotspot at the landing | junk about halves; the bite wait drops by about 40% (F3 BITE line) |

The predictions are read from the spec 12.6 rows; if T11/T12 retuned a rule, the T25 implementer updates the row from the asset values and says so.

**D. Day-night and readability (17.3 bullet 5).** F8 scrub a full day at the lake: no pops at 08:00, 17:00, 20:00 (phase starts 0.10/0.55/0.70) or at the sun/moon flip; a season change re-tints the textured props without flattening them (M3-D23). Readability at 25 m at night and in the Fog override: from the bank, the bobber and marker at about 25 m and the dock edge are distinguishable. Record the T18 scrub numbers.

**E. Journal and save.** Levels 0-3 on one species (T21 manual check 2), the badge against the WORLD line, the filters, completion, the main-menu summary; the file at `persistentDataPath/meta.json` (suffix per profile); a hand-corrupted main loads the `.bak` (`FG SAVE loaded Backup`); `schemaVersion` set to 2 by hand: the read-only warning, and the file is unchanged after a catch and a quit.

**F. Balance and tuning items.** The T11 V1-V3 and variety prints, the T12 V4/V5 tables and the M4 quota-calibration print, the fightability results; every `EcologyTuningItems` and `EcologyTunedDeviations` entry with its numbers and the approving ruling.

**G. Art status per species.** 25 rows: species id, model file (or "fallback"), source (M2 model restyled by T4 / T23 batch N / fallback), skin layer, the user's verdict (approved / notes / pending). Plus the harbour props (T17 verdict) and the journal icons (T21 verdict). A "fallback" row is not a failure (Q2).

**H. Carried manual checks.** T20, T21 and T22 manual lists (by reference to this plan); a non-development player build: clock shown, F3/F8/F4-F10 no-ops, no exception; the M2 acceptance items still open; the pending spec deltas M3-D65, M3-D66, M3-D74 (applied or not); the skeleton section 8 risks with their status; the bare-hook design shift (M3-D3) to try at the playtest.

**I. User actions due.** Run this checklist; the MPPM session; approve or defer the remaining art batches; confirm the fixed dev seed is still wanted; decide merging `m3-fish-ecology`.

**J. Definition of done.** 17.3 by reference: rows 1-12 met, or a miss recorded as an approved tuning item (row 5) or a deferred art item (row 12).

#### T25.2 README edits (ASCII only)
- Top list: add `M3 manual acceptance checklist: docs/superpowers/plans/2026-10-13-m3-acceptance.md`.
- Dev keys table: F3 now cycles overlay -> panel -> off; add F8 (hold: host clock x60).
- New section **"Ecology dev panel"**: how to open it; each row and what it calls; host-only edits, clients read-only; the fixed dev run seed (M3-D57) and when the seed can change; the map switch (dev builds, host only, M3-D31); the WORLD overlay line.
- New section **"Journal / meta.json"**: path and per-profile suffix (`meta.json`, `meta_<profile>.json`; tests use `_fgtest`); atomic write with `.bak`, the `.corrupt` copy, read-only for a newer schema; J opens the journal in game, the main menu has a Journal button; the four levels in one line each; delete the file to reset the journal.
- "Generated scenes are disposable" table: add `Content/Ecology/**` (created if missing; grids written only by the bake), `Scenes/Spot_HarbourPier.unity` (rebuilt), and note that `T_FishSkinArray.png` is generated by the Blender script in `Art/Blender/fishgame_m3_species.blend` (never hand-edited). Confirm each row against the builders before writing it.

#### T25.3 Two-build ContentHash check (17.3 bullet 2)
1. If `build` already logs the content hash (search `ComputeContentHash` and `FG ` log lines in `Assets/_Project/Editor`), use those lines.
2. Otherwise write a temporary editor script `Assets/_Project/Editor/Dev/TempContentHashPrint.cs` (namespace `FishGame.EditorTools`, never committed) whose `Print()` loads the registry asset, calls `Build()`, and logs `FG HASH <hex>` plus the ordinal id lists of species, junk, zones, spots, rods and baits.
3. Run `build`, then the print in a separate batchmode session (`-executeMethod FishGame.EditorTools.TempContentHashPrint.Print -quit`, log in `$env:FG_OUT\t25\hash1.log`); run `build` again and the print again in a new session (`hash2.log`). The two `FG HASH` lines and the id lists are identical; paste both into criterion 4.
4. Delete the script and its `.meta`; `git status` must not list them.

#### T25.4 Steps
1. Pre-flight (G0.4). Collect the inputs in the header.
2. Run full `test-edit` (keep the output in `$env:FG_OUT\t25\`); extract the rows the criteria name and the printed tables.
3. Two-build hash check (T25.3).
4. Write the acceptance doc (T25.1) with the automated results pasted in; leave every manual `Result:` as `______`.
5. Edit `README.md` (T25.2).
6. **Docs self-check** (a scratchpad script, not committed): no byte above 0x7F in either file; the doc contains rows 1-12, ME1-ME8, the five direction-check levers, sections A-J and 25 art-status rows; every test name it cites exists in C 10 or in the code; the README has the headings "Ecology dev panel" and "Journal / meta.json".
7. **Final `check`** (background). On FAIL apply the integration-fix rule; rerun until `FG RESULT check PASS`.
8. Review the working tree against the baseline: only the two docs and any named fix files; restore scene churn (whole files).
9. Commit:
```powershell
git -C C:\Dev\FishGame add docs/superpowers/plans/2026-10-13-m3-acceptance.md README.md <integration-fix files, if any>
git -C C:\Dev\FishGame commit -m "m3(docs): M3 acceptance checklist, README ecology dev panel and journal sections" -m "<fix lines, if any>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
10. Report: the `check` line; the two hashes; the balance status and tuning items; the art status count (models / fallback); the integration fixes.

#### T25.5 Acceptance (Sonnet docs reviewer)
- Every 17.3 bullet has a row, an owner and a how-to-check; the MPPM list covers every 12.9 manual item; the direction checks cover season, band, weather, rod tier and hotspot with data-based predictions.
- The art table has 25 rows; fallback rows are marked as not failing.
- README keys match C 5.3 (F3 cycle, F8); paths match C 5.3 (meta file).
- ASCII only; no invented test names.

#### T25.6 Hand-off to the final branch review (controller)
After the docs review passes, the controller dispatches **one Opus (effort xhigh) reviewer** on the whole branch:
- **Scope:** `git diff <base>...m3-fish-ecology` and `git log <base>..m3-fish-ecology`, where `<base>` is the commit the branch was cut from (`git merge-base` with `m2-fishing-core`, or `main` if M2 was merged first).
- **Inputs:** the contract, the skeleton, the seven section plans, the acceptance doc; the controller's scripted checks (C 2.8 forbidden identifiers, BOM and non-ASCII scan, owned files vs C 12 and C 2.6 via `git diff --name-only`, test names vs C 10, LFS pointers for every binary); the skeleton section 8 risk status.
- **Focus:**
  1. Contract conformance: public names, paths, test names; the CCR rulings (M3-D55..D77 and this section's).
  2. Authority comments; host-only ecology (C 2.8 #23); no client value in a roll.
  3. Determinism: every new stream through `EcologyStreams` (#25); the resolver, roller, weather and hotspot draw orders pinned by tests.
  4. The in-scene `WorldState` (#24): whole-scene commits, non-zero hashes, late-join state, the despawn path.
  5. Owner and host gates agree (#26); the day-over boundary.
  6. Catch-limit reservations: commit, release, the Spot-unload guard.
  7. The textured tint (#27) and no per-frame allocation in presentation and HUD.
  8. Journal and save robustness: never throws into gameplay, never overwrites a newer or unreadable file, local only.
  9. UI never writes gameplay state except the dev-panel host calls (M3-D51); dev surfaces gated by `Debug.isDebugBuild`.
  10. PlayMode warning-freedom; the fallback fish (#28); the LFS footprint.
- **Output:** findings by severity (Critical / Important / Minor), each with `path:line`, evidence and a proposed fix.
- **Then:** one fix wave (Sonnet; Fable escalates anything Sonnet cannot solve); re-reviews per the skeleton tiers (Sonnet for small single-area fixes, Opus high for ordering, concurrency or networking); the final `check`; the user runs the acceptance doc; merging only with the user's consent.

---

## Risks (section G)

1. **The fish skin sheet outgrows the import size (G-CR5).** A 4 x 8 sheet is 2048 x 4096; with the current 2048 cap Unity would silently halve every tile. Mitigation: the batch 1 postprocessor change and the T23.7 check (32 layers, 512 px tiles). G-CR5 was accepted (M3-D91); the fallback of a second fish array (`T_FishSkinArray2`, which needs a shader change) is not used (CCR accepted 2026-10-13).
2. **LFS growth (K17).** Each batch adds a new `.blend` and a new full sheet version (the sheet is one file, so every batch stores another 3-5 MB copy). Estimated about 25 MB for M3 on an 8 MB baseline; the per-commit size report and the 500/800 MB thresholds keep it visible. Monthly LFS bandwidth matters for fresh clones.
3. **Modal input interplay.** The journal and dev panel use `MenuOpen`, which `Show` also writes. A missed restore would leave the player frozen or the cursor free. Mitigation: one owner at a time, `Show` and `HandleBack` close the modal first, and T21/T22 manual checks cover every exit path (Esc, B, J/F3, Close, bite, scene change, session end).
4. **UI Toolkit key events need focus inside the modal.** J (journal) and F3 (panel) close through `KeyDownEvent`, which needs a focused element in the modal; a mouse click on empty space can drop focus. Esc/B (input system) and the Close buttons always work.
5. **The journal never blocks the fight.** The auto-close polls the local rod state each frame while open; a missed frame would leave the first fight input in the Menu context for one frame. Accepted; T21 manual check 4 covers it (also with a twitchy hook).
6. **Save robustness.** IO on the main thread after each journal change is a small JSON file (about 4 KB for 25 entries); a slow disk could hitch once per catch. A newer or unreadable file is never overwritten (read-only, `.corrupt` copy).
7. **Test pollution of real saves.** PlayMode runs use the `fgtest` profile (`meta_fgtest.json`); T24's journal asserts are relative. A manual editor session without the profile writes the developer's own `meta.json`, as intended.
8. **Blender MCP availability and drift.** T23 needs the user's Blender with the MCP add-on connected; the generalised scripts are new code in the `.blend`. Mitigation: batch 1 is the smallest-risk shapes, the bluegill's export call is copied verbatim, and every batch is verified in Unity (layer, triangles, submesh) before commit. Missing models never block the exit (Q2).
9. **Spec text vs the DTO (open question 1).** Level 1 shows the species' sub-areas in the seen spots, not the sub-areas actually seen; the user may want the latter in M4's v2 save.
