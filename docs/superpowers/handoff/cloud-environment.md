# Cloud sessions for FishGame (environment notes)

How a Claude Code cloud session can work on FishGame. Cloud sessions run Linux, start from a clone of `github.com/Jxycee/FishGame`, and cannot reach this PC.

## Setup
1. **Environment setup script (once, in the cloud environment's settings).** Paste this so the VM has Git LFS; it runs before each session starts and is cached:
   ```bash
   #!/bin/bash
   set -e
   command -v git-lfs >/dev/null || { sudo apt-get update -qq && sudo apt-get install -y -qq git-lfs; }
   ```
2. **Automatic per session.** The repo's SessionStart hook (`.claude/hooks/cloud-session-start.sh`) runs `git lfs pull` in cloud sessions, so models and textures are real files rather than pointers. If it reports git-lfs missing, step 1 isn't set up; run the `apt-get` line by hand.
3. Read `CLAUDE.md` and `docs/superpowers/handoff/CURRENT.md` on `main` first.
4. Work on a feature branch and push it. Never push to `main` (the guard hook blocks it in cloud sessions); the local controller merges. Never edit `CURRENT.md` from a cloud session.

## What a cloud session CAN do
- Read, review and edit C# code, shaders, docs and Blender Python generators.
- Review a branch diff and report findings (e.g. `git diff origin/main...origin/<branch>`).
- Write plans, spec drafts and research notes.
- Run the Blender generators headless, after installing Blender:
  ```bash
  wget -q https://download.blender.org/release/Blender4.2/blender-4.2.3-linux-x64.tar.xz
  tar xf blender-*.tar.xz
  FG_ROOT=$PWD ./blender-*/blender -b --factory-startup --python Art/Blender/fg_harbour.py
  ```
  The user's machine runs Blender 5.2, so FBX bytes may differ from a 4.2 build: use this to check that the script runs, not to commit exports.
- Compile-check pure C# with the .NET SDK (`dotnet`) only for code with no UnityEngine references, and only with a throwaway project outside the repo.

## What a cloud session CANNOT do (needs the user's PC)
- **Unity:** importing, building scenes (`ProjectBuilder.BuildAll`), EditMode/PlayMode tests and `HarbourCapture` screenshots. Unity is not installed in the cloud and needs the user's licence. Every branch must still get a local `Tools/unity.ps1 check` before merging.
- **The live Blender MCP** (blender tools): it connects to the Blender running on the user's PC.
- **Local tools:** the crew-watch board and the AssetRipper/REA reference study.

## Briefing a cloud session
State in the brief:
- the branch to cut from and the branch name to push;
- exact files and symbols (file:line);
- that Unity tests will run locally afterwards, so the session must say which tests it expects to change;
- to return a short report: what changed, what is unverified, what the local run must check.
