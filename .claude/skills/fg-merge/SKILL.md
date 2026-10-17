---
name: fg-merge
description: Merge a reviewed FishGame branch into main (no pull requests) - check, merge commit, re-check on main, push, tell the user. Use when a branch's review is done.
---
# Merging a FishGame branch into main

Current state:
- Branch: !`git rev-parse --abbrev-ref HEAD`
- Uncommitted: !`git status --short --untracked-files=no`

Steps:
1. **Preconditions.** The branch's review is done with no open blocking findings. Networking, save-data and milestone merges also need the user's one-line go: ask, and wait for it.
2. **Bring main in.** On the branch: `git fetch origin main` and `git merge origin/main` (merge commit, never rebase). Resolve conflicts; regenerate generated files with their builders, not by hand.
3. **Check the branch tip.** Run `Tools/unity.ps1 check` with the branch checked out, after the PlayMode guard (CLAUDE.md). It must print `FG RESULT check PASS` and an `FG MARKER <sha>` line matching `git rev-parse HEAD`. A FAIL is work: fix it on the branch and repeat.
4. **Merge.** `git checkout main`, `git pull origin main`, then `git merge --no-ff <branch> -m "Merge <branch>: <one-line summary>"`. The guard hook blocks this if step 3's marker is missing for the branch tip.
5. **Check main.** Run `check` again on `main`; it must PASS before pushing.
6. **Push.** `git push origin main`, then the branch if it changed.
7. **Record it.** Update `CURRENT.md` with `fg-handoff` (merge commit, check counts), commit and push it to `main`.
8. **Tell the user** what was merged: branch, merge commit, and the check result line.
