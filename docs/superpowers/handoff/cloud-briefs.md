# Ready-to-paste briefs for cloud sessions

Start each one yourself as a cloud session (claude.ai/code or the app's cloud option) on the repo `Jxycee/FishGame`, so it runs on cloud credits. Paste the whole block. Agents started from a local session (`isolation: remote`) fell back to local and used the weekly plan.

Every brief assumes the setup in `docs/superpowers/handoff/cloud-environment.md`.

## Brief 1: Opus review of two save-data fixes (use Opus)
```
Setup: sudo apt-get update -qq && sudo apt-get install -y -qq git-lfs && git lfs install && git lfs pull. Read CLAUDE.md and docs/superpowers/handoff/cloud-environment.md. Unity is not available: you can't compile against UnityEngine or run tests, so reason carefully and list the tests the local Unity run must check. Never push to main or edit CURRENT.md. End commits with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>

Task: review two save-data fixes as the project's save-data reviewer, then make each branch merge-ready.
1. fix/save-newer-schema-readonly (c5b48d5): Assets/_Project/Runtime/Save/SaveService.cs TryReadNewerSchemaVersion. A newer-schema meta.json that fails to deserialize is no longer treated as corrupt; the session runs read-only and still tries the .bak. 4 new EditMode tests in Assets/_Project/Tests/EditMode/SaveServiceTests.cs.
2. fix/journal-write-debounce (be18558): SaveService IsDirty/MarkDirty/FlushIfDirty (Save clears dirty). Assets/_Project/Fishing/Journal/JournalRecorder.cs: NotifyChanged stays immediate, the disk write is debounced 3 s (WaitForSecondsRealtime) and flushed in OnDisable; AppBootstrap still saves on quit. Tests in SaveServiceTests.cs, plus a FlushIfDirty() call in Assets/_Project/Tests/PlayMode/EcologySoloTests.cs.
Both were cut from an older main: merge origin/main into each (merge commit).
Review for:
- data loss, and exceptions escaping into gameplay (SaveService must never throw into gameplay);
- whether the read-only decision is correct;
- how the two fixes compose when both are merged (for example FlushIfDirty on a read-only session);
- coroutine/OnDisable edge cases (scene unload, quit order with AppBootstrap.HandleQuitting, the service being replaced);
- test quality.
Don't report cheat or modding concerns. Fix real problems with small commits and push both branches.
Report: per branch, merge-ready or not, findings (severity, file:line), what you changed, and the exact EditMode/PlayMode tests the local run must pass.
```

## Brief 2: M4 design draft (use Opus)
```
Setup: sudo apt-get update -qq && sudo apt-get install -y -qq git-lfs && git lfs install && git lfs pull. Read CLAUDE.md, docs/superpowers/handoff/CURRENT.md and docs/superpowers/handoff/cloud-environment.md. This is a docs task. Work on a new branch m4-design-draft from origin/main and push it. Never push to main or edit CURRENT.md. End commits with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>

Task: prepare the M4 design session. This is a draft; the user decides.
Read in full:
- docs/superpowers/design/2026-10-14-m4-design-inputs.md (the user's answers);
- the spec docs/superpowers/specs/2026-10-09-fishgame-design.md (source of truth; find R76, R78 and the milestone list; don't re-litigate agreed items);
- docs/superpowers/research/.
Write docs/superpowers/design/2026-10-14-m4-design-draft.md:
1. Proposed spec rows in the spec's style:
   - R76 run structure: 7 days = 7 river stages with variants; day length by stage; dusk docking at each map's camp spot; the starfish-god vendor at camp; live night-camp mafia events; the finale cutscene.
   - R78 tugboat: free driving; walkable interior; 4 chairs with the user's saved-seat logic for the morning cutscene; per-player ice chests lost if left behind; several fishing spots aboard.
   - Each row states its network authority (host vs owner).
2. Open questions for the user, each with 2-3 options and a recommendation.
3. M4 task outline: dependencies, Unity work vs pure-C# Core logic with EditMode tests (seat assignment, chest-loss rules, stage progression, quota by stage), rough sizes.
4. Risks: networking, scope, and performance (quality first; Low is a fallback).
Keep it tight, with tables where they help. Commit and push.
Report: the branch name, the file path, the 5 most important open questions, and any conflicts with the spec.
```

## Not cloud-able
The harbour art rounds (harbour-art3, run wf_8dc74d12-0fa) need Unity and the live Blender on the PC. Resume them locally after the weekly reset. The round 1 cloud review findings for the r2 artist are in the session scratchpad (harbour-best/r1-review.md), and the workflow script points the r2 artist at that file.
