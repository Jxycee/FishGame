# FishGame review guide

Used by the `reviewer`, `risk-reviewer` and `branch-reviewer` agents and by `/code-review`.

## What to look for
- **Correctness against the brief and the spec** (`docs/superpowers/specs/2026-10-09-fishgame-design.md`): wrong behaviour, missed acceptance cases, broken contracts between assemblies.
- **Networking:** host authority stated and respected; state changed only where the class comment says; late joiners and disconnects mid-action; RPC ordering; anything that desyncs peers.
- **Save data:** data loss, schema compatibility, and exceptions escaping into gameplay (the save service must never throw into gameplay).
- **Tests:** the tests check the behaviour, not the implementation; nothing skipped, deleted or `[Ignore]`d to get green; Core logic is covered in EditMode.

## What not to report
- Cheat-proofing or client exploits (no anti-cheat, by user decision).
- Style a formatter or `ArchitectureTests` already enforces.
- Design changes the spec already settled.

## How to report
- Report every finding you have, each with a severity (blocking / should-fix / nit) and a confidence (high / medium / low). The controller filters; don't drop findings yourself.
- Each finding: `file:line`, what goes wrong, and a concrete path from a real caller or input to the failure. If you can't trace one, mark it low confidence.
- A sound change with no findings is a valid result; say so plainly rather than hunting for something.
- Say which tests the local Unity run must pass to confirm the change.

## Rounds
At most three fix-and-review rounds per task. If findings keep coming after the third, stop and tell the controller the approach needs rethinking rather than patching again.
