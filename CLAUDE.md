# FishGame — project instructions

Co-op (1–4) friend-slop rogue-lite fishing game. Unity 6000.6.5f1 (URP), Netcode for GameObjects, Unity Multiplayer Services (Relay + Lobby join codes). Design spec: `docs/superpowers/specs/2026-10-09-fishgame-design.md` — source of truth for decisions; don't re-litigate agreed items there. Performance target is the spec's quality-first one; ignore generic "60+ FPS on all platforms" rules from skills.

## Running handoff
The main (controller) session reads `docs/superpowers/handoff/CURRENT.md` (on `main` only) first in every new conversation; subagents skip it unless their brief names it. The controller keeps it current after every meaningful step (a task landed, a review, a user decision, a merge) with the `fg-handoff` skill, then commits and pushes it to `main`. Feature branches never edit it.

## Unity (Windows PC only)
- Run Unity only through the wrapper: `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 <compile|build|test-edit|test-play|check> -OutDir <scratchpad dir>`. Exit codes: 0 PASS, 1 FAIL, 2 LOCKED, 3 TIMEOUT, 4 usage. It reads the log (exit 0 alone is not proof), adds `-dev-profile fgtest` to test runs (without it tests touch the user's real LocalLow settings), and records a PASS marker that the merge gate checks.
- `LOCKED` means the user has the editor open: ask them to close it. Never kill Unity processes and never delete `Temp/UnityLockfile`.
- PlayMode runs in different worktrees collide on the same UDP port: before `test-play` or `check`, look for another Unity process with `-testPlatform PlayMode` and wait for it to finish.
- Editor scripts build scenes and prefabs (`FishGame.EditorTools.*`); never edit `.unity`/`.prefab` YAML by hand (a hook blocks it).
- Logs and test results go in the session scratchpad, never in the repo.

## Cloud sessions (Linux)
No Unity and no Windows paths. Follow `docs/superpowers/handoff/cloud-environment.md`: push a feature branch (a hook blocks pushes to `main`), never edit `CURRENT.md`, and report which Unity tests the local run must check.

## Code conventions
- Gameplay code under `Assets/_Project/`, split by assembly definition (see spec for the list). `ArchitectureTests` enforces the forbidden references.
- Pure C# logic (tension model, spawn resolver, quota math) lives in classes with no MonoBehaviour dependency so EditMode tests cover it.
- Host-authoritative networking. Every new networked system states its authority (host vs owner) in a class-level comment.
- No anti-cheat (user): modded clients and trainers are welcome. Host authority keeps peers in sync; don't add cheat-proofing or report client exploits as findings.
- Navigate C# with the LSP tool before reading whole files. In a worktree with no `.csproj`/`.slnx`, first run `Tools/sync-csproj.ps1` (gitignored output; never commit it).
- Art and asset rules (licences, art direction) live in `.claude/rules/art-assets.md` and load when you touch art.

## Git
- Commit when the user asks or a plan step says to. Never force-push. Milestone work happens on a feature branch.
- No pull requests (user): push branches and merge them yourself, including into `main`, once reviewed and a full `check` is green on the branch tip. Merge commits, never squash. Tell the user what was merged. Use the `fg-merge` skill.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vscode/`, `*.csproj`, `*.slnx`.

## Agents and review
- Delegate with the `fg-delegate` skill: it holds the brief template and the routing. Roles, models and effort are pinned in `.claude/agents/`; don't set `CLAUDE_CODE_EFFORT_LEVEL` (it overrides them).
- Review scaled to risk: `risk-reviewer` (Opus) for networking/host sync and save data, `branch-reviewer` (Opus xhigh) before a milestone merge, `reviewer` (Sonnet) for everything else; a fix of 30 lines or fewer in one area gets the controller's own diff read. Reviewers follow `REVIEW.md`.
- Plan big features in a short design session with the user before any code. Multi-agent workflows and critic loops only with the user's go-ahead and a stated agent count and token budget.
- Never SendMessage a running workflow agent (it cloned the agent once).
- Plugins and connectors unused here are off in `.claude/settings.json`; don't turn them back on without asking.

# Compact instructions
When compacting, keep: the current branch and worktree, files modified, live agent IDs and what each is doing, the last `check`/test result with its commit, open review findings, and the next step.
