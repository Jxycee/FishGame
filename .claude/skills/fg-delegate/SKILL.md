---
name: fg-delegate
description: Brief template and routing for delegating FishGame work to the project agents (implementer, reviewers, scanner, fixer). Use whenever the controller hands a task to a subagent.
---
# Delegating FishGame work

## Route by risk
| Work | Agent | Override per call |
|---|---|---|
| Any implementation | `implementer` (Sonnet, medium) | effort `high` for networking or PlayMode tasks; model `haiku`, effort `high` when the brief contains the complete code |
| Review: networking/host sync, save data | `risk-reviewer` (Opus, high) | |
| Review: everything else (gameplay, Core, data, art, presentation, docs) | `reviewer` (Sonnet, medium) | |
| Fix of 30 lines or fewer in one area | no agent: read the diff yourself | |
| Milestone branch before merging into `main` | `branch-reviewer` (Opus, xhigh) | |
| Locate symbols, inventories, counts | `scanner` (Haiku) | |
| Fails twice, or nobody can find the root cause | `fixer` (Fable) | |
| Plan section | write it yourself or one Opus author at `medium`; `high` for networking, save-schema and state-machine sections | |

Do it yourself when it takes a handful of tool calls. Batch small same-area tasks into one implementer run and one review. One implementer touches Unity at a time (the project lock); reviewers are read-only and can run alongside. Continue an agent with SendMessage instead of spawning a fresh one, but never SendMessage a running workflow agent.

## Brief template
```
Goal: <one sentence, and why it matters for the game>
Branch: <branch to work on / cut from>
Files and symbols: <path:line for each thing to change or read first>
Behaviour: <what must be true afterwards; network authority (host vs owner) if networked>
Tests: <test names and cases; which must fail first>
Out of scope: <what not to touch>
Check: <the Tools/unity.ps1 command that proves it, or "cloud: name the tests the local run must pass">
Report: what changed, the check result line, anything unverified.
```
Name exact files and symbols so the agent doesn't search. Give the reason behind non-obvious constraints. Put code in the brief only for the tricky parts (math, state machines, RPC sequences), or all of it when routing to Haiku.

## After the agent reports
- Check its evidence (the PASS/FAIL line, the diff), not its summary.
- Send review findings back to the same implementer. Fix blocking findings; decide should-fix ones; note nits for the next push. At most three fix-review rounds (`REVIEW.md`).
- Update `CURRENT.md` with `fg-handoff` when a task lands.
