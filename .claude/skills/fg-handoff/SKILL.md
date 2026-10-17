---
name: fg-handoff
description: Update docs/superpowers/handoff/CURRENT.md, the running FishGame handoff on main, after a meaningful step (task landed, review, user decision, merge). Keeps it a short, evidence-cited state file.
---
# Updating CURRENT.md

`docs/superpowers/handoff/CURRENT.md` is the project's working memory between sessions. Only the controller on `main` edits it; feature branches and cloud sessions never do.

Keep it a **state file under 300 lines and about 4,000 tokens** (`wc -w` under ~2,800 words). Claude Code re-reads files after compaction only up to 5,000 tokens, so a bigger file drops out exactly when it is needed.

## Sections (in this order)
1. **Now:** date, the one-paragraph status, and what the user is doing or owes.
2. **Next:** the next three actions, in order.
3. **Branches and worktrees:** each open branch, its state (in progress / in review / green, waiting for merge), and its last check result with the commit.
4. **Blockers and open questions:** what is waiting on whom.
5. **Read next:** pointers (paths) to the design docs, plans and checklists that matter right now.

## Rules
- Edit in place; don't rewrite sections that didn't change.
- Every status claim cites its evidence: a commit, a check result line (`FG RESULT check PASS`, test counts), or the user's words with a date. No claim without one.
- History, tooling inventories, finished work and lessons don't belong here. Move them to a dated file in `docs/superpowers/handoff/` (history) or to `CLAUDE.md`, a skill or `.claude/rules/` (lasting rules), and leave a one-line pointer.
- If the file is over the cap, prune it before adding anything.
- Commit with a message saying what changed (`handoff: <step>`) and push to `main`.
