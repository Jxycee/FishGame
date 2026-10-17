# Turn FishGame's prose rules into enforced gates

FishGame already runs Claude Code closer to published best practice than most solo projects. Anthropic's docs, the Claude Code team and outside practitioners agree on the same foundations, and FishGame has them: a **46-line CLAUDE.md**, a `Tools/unity.ps1` wrapper that reads Unity's logs because its exit codes can't be trusted, LSP-first navigation, reviews scaled to risk, and a file-based handoff. The next gains come from enforcement and calibration, not from more instructions. Four shifts matter:

- **Move must-always rules into configuration.** Use hooks and permission denies for scene files, raw Unity calls and the merge gate, and agent files that pin each role's model and effort. CLAUDE.md is advisory context; hooks are deterministic.
- **Recalibrate effort for the 5.5 family.** Opus 5.5 at its default `medium` matches or beats Opus 5 at `high`, so "Opus (effort high)" should be a measured exception for networking, save and state-machine work, not a standing default.
- **Shrink CURRENT.md.** At **225 lines and about 5,400 tokens**, over half of it reference material, it no longer survives compaction as content.
- **Stop uncalibrated critic loops.** The **2.2M-token polish run peaked in round 4 and slipped in round 5**, the plateau Anthropic's own harness research describes.

Two defects need fixing today:
- CLAUDE.md contradicts itself on which reviews go to Opus.
- Its raw Unity test command omits `-dev-profile fgtest`, the flag that keeps test runs off the user's real settings.

Cloud sessions cannot run Unity. Give them a Core-only `dotnet` check and a setup script, and route Unity work through the Windows PC.

## Three FishGame habits match the evidence; three have drifted

The six practices named in the brief, scored against the sources; the sections after this one carry the detailed recommendations.

| FishGame practice | Verdict | Key evidence | Change |
|---|---|---|---|
| Tiered roles: Sonnet implements; Opus `high` reviews networking/save; Opus `xhigh` branch review; Haiku for complete-code briefs; Fable escalates | **Aligns**, but CLAUDE.md contradicts itself on review scope | Sonnet "handles most coding tasks well", Opus is for "complex architectural decisions", simple subagents get `model: haiku` ([costs](https://code.claude.com/docs/en/costs#reduce-token-usage)); move to Fable 5.1 when Opus at `xhigh`/`max` falls short ([choosing a model](https://platform.claude.com/docs/en/about-claude/models/choosing-a-model)). In M1, reviews caught ~12 real bugs and Opus `xhigh` found 2 important cross-task bugs ([M2 handoff](../docs/superpowers/handoff/2026-10-10-m2-handoff.md)) | Resolve [CLAUDE.md](../CLAUDE.md) lines 39 vs 46; pin model and effort in `.claude/agents/` |
| "Opus (effort high)" plan authors | **Drifted** | Opus 5.5 at `medium` "matches or exceeds Claude Opus 5 at `high`" ([Opus 5.5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5)); "start at `medium` rather than carrying over the level you used on Opus 5" ([model config](https://code.claude.com/docs/en/model-config#choose-an-effort-level)) | `medium` by default; `high` for networking, save and state-machine sections |
| 225-line CURRENT.md rewritten after every step | **Right pattern, drifted in size** | Anthropic's long-run harness is a progress file, the git log and a fixed startup routine ([harnesses](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents)); compaction re-reads files only up to 5,000 tokens ([context window](https://code.claude.com/docs/en/context-window#what-survives-compaction)) | State-only file under ~4k tokens; archive the reference sections |
| Critic/polish workflows (~2.2M tokens, plateau ~6.8) | **Today's go-ahead rule aligns; the past run overshot** | Evaluator scores "improved over iterations before plateauing", and a middle iteration often beat the last ([harness design](https://www.anthropic.com/engineering/harness-design-long-running-apps)); default size guideline under 10 agents, warning above 25 agents or 1.5M tokens ([workflows](https://code.claude.com/docs/en/workflows#cost)); the run used 21 agents ([CURRENT.md](../docs/superpowers/handoff/CURRENT.md)) | Calibrate judges against the user, cap rounds, keep the best round, state the budget up front |
| No PRs; self-merge after review and a green `check` | **Aligns for low risk; drifts for high risk** | Anthropic's team needs no human for low-risk changes "once automated review and security checks are green", but "An agent's approval never counts as a person's" ([code review at Anthropic](https://claude.dev/blog/solving-code-review-at-anthropic/)). M3 merged on 2026-10-13 while most of the user's 50 board checks were open ([CURRENT.md](../docs/superpowers/handoff/CURRENT.md)) | Enforce "green before merge" with a hook; a one-line user go for networking, save and milestone merges |
| Windows-only Unity CLI; cloud can't run Unity | **Aligns with the docs' cloud scope; cloud has no runnable check** | Cloud VMs are Ubuntu with 4 vCPU, 16 GB, 30 GB; beyond that, "use Remote Control to run Claude Code on your own hardware" ([cloud environments](https://code.claude.com/docs/en/cloud-environments#resource-limits)) | Setup script, a Core-only `dotnet` check, Remote Control for Unity |

## CLAUDE.md is 46 lines but carries about 55 rules and a contradiction

Every source wants a short file. They differ only on how short:
- **Anthropic's docs** set the ceiling at **under 200 lines**: "Longer files consume more context and reduce adherence", and when two instructions contradict, "Claude may pick one arbitrarily" ([memory docs](https://code.claude.com/docs/en/memory#write-effective-instructions)).
- **Boris Cherny** is tighter: "under 1k tokens or so … Ours is maybe half that size. We remove from it with every model release" ([HN](https://news.ycombinator.com/item?id=46256682)).
- **HumanLayer** estimates frontier models follow **150–200 instructions** reasonably consistently, about 50 of which Claude Code's own prompt already uses, and keeps its root file under 60 lines ([HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)). By its own admission, that figure is not rigorously tested.
- **ETH Zurich**, in the one controlled study, found context files do not generally improve task success and **raise cost by over 20%**, though they help for "non-standard coding practices" ([arXiv 2602.11988](https://arxiv.org/abs/2602.11988)).

Thariq Shihipar's rule for Claude 5-generation models sums up the consensus: describe the repo briefly and "spend most of the tokens on gotchas" ([Claude blog](https://claude.com/blog/the-new-rules-of-context-engineering-for-claude-5-generation-models)).

FishGame passes the line test but fails the density test. **Its 46 lines hold 5.6 KB: about 1,400 tokens and, by my count, roughly 55 distinct directives**, and CURRENT.md adds more. Four specifics matter more than length:
- **It contradicts itself on review scope.** Line 39 says "Opus reviews only networking/host sync, save data and milestone merges". Line 46 says "Opus (high) reviews integration tasks", a category the M2 handoff defines to include gameplay, UI flow and builders ([M2 handoff](../docs/superpowers/handoff/2026-10-10-m2-handoff.md)).
- **Its test command is unsafe.** Lines 13–15 teach a raw `Unity.exe … -runTests` command without `-dev-profile fgtest`. CURRENT.md warns that without that flag, runs "touch the user's real LocalLow settings", and the wrapper adds it automatically ([CURRENT.md](../docs/superpowers/handoff/CURRENT.md); [unity.ps1](../Tools/unity.ps1)).
- **36% of its bytes serve only the controller or art tasks.** That is the agent-roles block plus the asset/art line. Subagents load the CLAUDE.md hierarchy ([subagents](https://code.claude.com/docs/en/sub-agents#what-loads-at-startup)), so every implementer and scanner carries about 500 tokens of rules it never applies. The larger cost is attention, not tokens.
- **The handoff instruction reaches subagents too.** "Read it first in every new conversation" applies to them as well, and Sonnet-class models follow instructions literally ([Sonnet 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5)). A spawned worker risks spending ~5k tokens on CURRENT.md. I have not observed this.

**Recommendations, in priority order:**
1. **Fix the two defects.** Replace the raw command lines with the wrapper's verbs (`compile`, `test-edit`, `test-play`, `check`) and its exit codes. Reduce Opus review scope to one sentence.
2. **Move procedures and area rules out.** The docs say multi-step procedures belong in skills and area-specific rules in path-scoped `.claude/rules/` ([memory docs](https://code.claude.com/docs/en/memory#when-to-add-to-claude-md)). The tier policy goes to agent files plus a controller skill. Asset licensing and art direction go to a rule scoped to `Art/**` and `Assets/ThirdParty/**`.
3. **Scope the handoff instruction** to the main session.
4. **Promote the durable rules hiding in CURRENT.md**: `fgtest`, never committing `.vscode/settings.json`, and never messaging a running workflow agent. One of these can be removed at the source instead. `.vscode/settings.json` is tracked even though `.gitignore` lists `.vscode/`, so untracking it deletes the rule outright.
5. **Delete lines the harness already follows.** "Send follow-ups to an existing agent" is one. The docs' test is whether removing a line would cause mistakes ([best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)).
6. **Move provenance dates into HTML comments**, which are stripped before injection ([memory docs](https://code.claude.com/docs/en/memory#how-claude-md-files-load)). Keep the one-clause reasons, because stated motivation improves compliance ([prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices)).
7. **Add a `# Compact instructions` section.** It names what must survive compaction: branch, modified files, live agent IDs, last check result, next step ([costs](https://code.claude.com/docs/en/costs#reduce-token-usage)).
8. **Audit and prune regularly.** Run `/doctor prompt-audit`, which flags contradictions and instructions written for older models ([memory docs](https://code.claude.com/docs/en/memory#audit-your-instruction-files)), and prune at every model release.

Mid-session edits do not apply until `/clear`, `/compact` or a restart, so restate any new rule in conversation ([prompt caching](https://code.claude.com/docs/en/prompt-caching)). One contradiction lives outside the file. The user-scope game-developer skill says "60+ FPS on all platforms", which the spec overrides. Patch or disable it rather than leave the conflict in context.

## Keep the tiers, pin effort in config, and start Opus 5.5 at medium

Model choice in FishGame's tiers matches the docs:
- **Claude Code's aliases**: Sonnet "for daily coding tasks", Opus "for complex reasoning tasks", Haiku "for simple tasks" ([model config](https://code.claude.com/docs/en/model-config#model-aliases)).
- **The platform guide** reserves Fable 5.1 for when evals "at `xhigh` or `max` effort still fall short" ([choosing a model](https://platform.claude.com/docs/en/about-claude/models/choosing-a-model)).

There is a case against tiering:
- **Anthropic's own split test**: its role-based planner/implementer/tester/reviewer split "spent more tokens on coordination than on actual work". The same piece says "an agent handling a feature should also handle its tests" ([Claude blog](https://claude.com/blog/building-multi-agent-systems-when-and-how-to-use-them)).
- **Boris's model choice**: the top model "is almost always faster than using a smaller model in the end" ([X](https://x.com/bcherny/status/2007179838864666847)). He said that of Opus 4.5 versus Sonnet 4.5, so treat it as a hypothesis for 5.5.

FishGame already blunts the split's overhead:
- implementers own red-first tests;
- small tasks are batched;
- fixes of 30 lines or fewer get only a controller read.

Its M1 accounting located the waste: **15M tokens on planning against about 7M on execution** ([M2 handoff](../docs/superpowers/handoff/2026-10-10-m2-handoff.md)). The design-session-first habit fits current advice too. The docs say to skip the plan when "you could describe the diff in one sentence" ([best practices](https://code.claude.com/docs/en/best-practices#explore-first-then-plan-then-code)). Thariq starts "almost every coding session with an exploration or brainstorming phase" ([X](https://x.com/trq212/status/2073100352921215386)).

Effort is where the drift is. In Claude Code, **Opus 5.5, Sonnet 5.5 and Haiku 5.5 all default to `medium`**. The docs map levels to work types ([model config](https://code.claude.com/docs/en/model-config#choose-an-effort-level)):
- `high` for work "where verification matters or edge cases are likely";
- `xhigh` for "deeper reasoning at higher token spend";
- `max` for autonomous hard problems, where it is "prone to overthinking".

Each model guide adds a caveat:
- **Opus 5.5** "tends to think more per turn than Claude Opus 5, especially at `xhigh` and `max`" ([Opus 5.5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5)).
- **Sonnet 5.5** at `low` "can skip verifying a change". At `xhigh`/`max` it can start its own review rounds, sometimes with subagents. A stop-and-report instruction "cut session cost by about a third" ([Sonnet 5.5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5-5)).
- **Haiku 5.5**: Anthropic recommends `high` for "strict instruction following" ([effort](https://platform.claude.com/docs/en/build-with-claude/effort)). That is exactly what a complete-code brief demands.

Thariq's own loop implements at low or medium, then runs "verification on high effort" ([X](https://x.com/trq212/status/2103576349499855160)). FishGame's CLAUDE.md names effort only for Opus, so Sonnet and Haiku run at whatever default applies.

**Recommendations:**
1. **Encode every tier as an agent file** with `model` and `effort` frontmatter ([subagents](https://code.claude.com/docs/en/sub-agents#choose-an-effort-level)). Keep `CLAUDE_CODE_EFFORT_LEVEL` unset, because it overrides all frontmatter.
2. **Run Opus plan authors at `medium` by default.** Keep `high` for networking, save-schema and state-machine sections. Those sections still carry plan code, and M1 traced most plan-mandated bugs to plan code. Keep `xhigh` only for the milestone branch review, the one place FishGame has measured a win.
3. **Pin Sonnet and Haiku effort.** Run Sonnet implementers at `medium`, with per-invocation `high` for networking and PlayMode tasks. Run Haiku at `high` for complete-code briefs and `medium` for scans.
4. **Run one A/B before M4.** On two comparable integration tasks, compare Sonnet-implements/Opus-reviews against Opus 5.5 at `medium` implementing with a Sonnet review. Score `/usage` tokens and bugs found later. This is the "effort sweep on your own evals" the docs ask for ([effort](https://platform.claude.com/docs/en/build-with-claude/effort)).
5. **Brief the Fable escalation with the failing evidence and the reason.** Anthropic says testing Fable "only on simpler workloads tends to undersell" it. Fable also trims its own work when shown remaining-token countdowns ([Fable 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-fable-5)), so keep usage-limit worries out of its briefs.
6. **Keep the controller on one model per session.** A `/model` switch re-reads history uncached, while an effort change on the 5.5 and Fable models keeps the cache ([prompt caching](https://code.claude.com/docs/en/prompt-caching)). Use `ultrathink` for a single deep turn.
7. **Leave ultracode off.** It "plans a dynamic workflow for each substantive task" ([model config](https://code.claude.com/docs/en/model-config#choose-an-effort-level)), which sidesteps the user's opt-in rule for workflows.

## The Unity wrapper is a sound gate, but prose still guards the risky paths

Verification is the one recommendation no source has walked back:
- **Boris Cherny:** a verification loop "will 2-3x the quality of the final result" ([X](https://x.com/bcherny/status/2007179861115511237)).
- **Anthropic's review team:** the agent shows output "instead of saying it's done" before any review ([claude.dev](https://claude.dev/blog/solving-code-review-at-anthropic/)).
- **The docs:** escalate from a prompt, to `/goal`, to a Stop hook as a "deterministic gate", to a fresh-context verifier ([best practices](https://code.claude.com/docs/en/best-practices#give-claude-a-way-to-verify-its-work)).
- **Outside practitioners:** confirm tests fail first ([Willison](https://simonwillison.net/guides/agentic-engineering-patterns/red-green-tdd/)), and never "send an LLM to do a linter's job" ([HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)).

FishGame's base is strong:
- **The wrapper.** `unity.ps1` tests whether the lock is held, not merely present. It returns distinct PASS, FAIL, LOCKED and TIMEOUT codes, and prints at most 40 matching log lines. That design is right, because Unity has "no common definition for exit codes" ([Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html)).
- **The tests.** There are **1,378 EditMode and 139 PlayMode tests**, and briefs require a real red run.
- **Generated scenes.** Scenes come from editor scripts, which a Unity forum user calls "the only deterministic interface Unity reliably accepts" ([Unity Discussions](https://discussions.unity.com/t/cli-mcp-ai-ide-chatgpt-codex-cursor-antigravity-claude-code-windsurf-next-steps-for-unity-workflow-automation/1705679)).

The weakness is that the most dangerous rules exist only as prose:
- **CLAUDE.md is advisory.** It "is delivered as a user message" with "no guarantee of strict compliance" ([memory docs](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)).
- **A deny is enforced.** A PreToolUse deny blocks a call "even in `bypassPermissions` mode" ([hooks guide](https://code.claude.com/docs/en/hooks-guide#limitations)).
- **The cost of an unguarded scene file is real.** On Cursor, an agent truncated an 8.3 MB `.unity` file while the tool reported success ([Cursor forum](https://forum.cursor.com/t/agent-truncated-unity-scene-file-without-approval-irreversible-data-loss/169064)).

**Recommendations, in priority order:**
1. **Add deny rules or a PreToolUse hook** that blocks:
   - Edit/Write on `*.unity` and `*.prefab`, plus `sed`/`perl`/`python` writes to them;
   - raw `Unity.exe`, which forces the wrapper and its `fgtest` profile;
   - `git push --force`;
   - in cloud sessions (`CLAUDE_CODE_REMOTE=true`), any push to `main`.
2. **Make "merge only when green" a guarantee.** `check` writes a PASS marker keyed to the tested commit, and a PreToolUse hook refuses `git merge` into `main` without a matching marker. Move the PlayMode UDP-port guard into `unity.ps1` itself, following Anthropic's "Poka-yoke your tools" ([Building effective agents](https://www.anthropic.com/engineering/building-effective-agents)).
3. **Script the mechanical checks the M2 handoff assigns to the controller** ([M2 handoff](../docs/superpowers/handoff/2026-10-10-m2-handoff.md)): BOM and non-ASCII characters, forbidden identifiers such as `DEVELOPMENT_BUILD` and bare `Input.`, and test count against the brief. Also flag newly `[Ignore]`d or deleted tests, the "cheating" Kent Beck warns about ([Beck](https://newsletter.kentbeck.com/p/augmented-coding-beyond-the-vibes)). Run the script as a PostToolUse hook whose exit code 2 shows violations to Claude ([hooks reference](https://code.claude.com/docs/en/hooks#exit-code-2-behavior-per-event)). Boris calls convention misses "failures of automation" ([X](https://x.com/bcherny/status/2077460395279692197)).
4. **Paste Anthropic's verification paragraph into implementer prompts.** Sonnet 5.5 and Haiku 5.5 at `low`/`medium` sometimes report done without a real check. The paragraph makes them run one or name the one they could not run ([Sonnet 5.5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5-5#verification-on-coding-tasks)). Keep "double-check" lines out of Opus prompts, because they cause over-verification ([Opus 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5)).
5. **Write a short REVIEW.md.**
   - Scope reviewers to correctness and stated requirements. Reviewers asked for gaps "will usually report some, even when the work is sound" ([best practices](https://code.claude.com/docs/en/best-practices#add-an-adversarial-review-step)).
   - Ask for every finding with severity and confidence, not "only high-severity", which 5-family models obey literally ([Opus 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5)).
   - Stop after three fix-review rounds and reconsider the approach. Anthropic's team did this after one PR ran 50 rounds overnight ([claude.dev](https://claude.dev/blog/solving-code-review-at-anthropic/)).
   - At the next milestone, run `/code-review xhigh` or `ultra` beside the Opus `xhigh` review and compare. Boris claims ultra "reliably catches >99% of bugs" for $3–20 a review ([HN](https://news.ycombinator.com/item?id=48296387)); test that rather than assume it.
6. **Turn R79's numeric layout rules into EditMode assertions:**
   - stands at least 3 m from the edge (CURRENT.md records stand 14 at 2.4 m);
   - a lamp at every stand;
   - nothing south of the boundary wall;
   - the 500k-triangle Low budget.

   Classmethod's Unity field tests found agents verify by warping the player, and misjudge lighting that a human found fine ([Classmethod 2D](https://dev.classmethod.jp/en/articles/unity-mcp-claude-code-2d-game-verification/); [Classmethod TPS](https://dev.classmethod.jp/en/articles/unity-mcp-tps-game-claude-code-modification/)). Deterministic tests should take what they can. The user's check board stays the gate for feel and look.
7. **Give cloud sessions a real check.** [`FishGame.Core.asmdef`](../Assets/_Project/Core/FishGame.Core.asmdef) already sets `noEngineReferences: true`, and **72 of 101 EditMode test files import no UnityEngine namespace**. A small `dotnet` test project over Core and its engine-free tests would let cloud sessions prove pure-C# changes. Whether it works is unverified, because the shared test assembly references Unity packages.

Keep Unity out of Stop hooks. A Stop hook fires "whenever Claude finishes responding, not only at task completion" ([hooks guide](https://code.claude.com/docs/en/hooks-guide#limitations)), so at most it should do a cheap marker check guarded by `stop_hook_active`.

## CURRENT.md has outgrown the 5,000-token compaction limit

Anthropic's recipe for multi-session work has five parts ([harnesses](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents)):
- a progress file;
- a JSON feature list, because models are "less likely to inappropriately change or overwrite JSON files compared to Markdown files";
- descriptive git commits;
- a startup routine that reads them;
- a smoke test before new work.

The prompting guide adds that a brand-new context window often beats compaction, since the latest models "are extremely effective at discovering state from the local filesystem" ([prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices)). Practitioners agree; Amp removed compaction because "It's lossy" ([Amp](https://ampcode.com/news/handoff)).

FishGame's single-writer CURRENT.md on `main` follows this pattern, but it has bloated:
- **Size:** 225 lines and 21.6 KB, about 5,400 tokens.
- **Content:** 52% of its bytes are not current state. That is the AI-tooling inventory (4.0 KB), harbour history (3.4 KB), deferred items and token notes (2.3 KB), and standing rules (1.6 KB).
- **Churn:** 36 commits in three days.

Size matters because after compaction Claude Code re-reads up to five recent files, and a file over 5,000 tokens "comes back as a path reference" ([context window](https://code.claude.com/docs/en/context-window#what-survives-compaction)). A controller that compacts mid-milestone loses the handoff's contents at the worst moment.

**Recommendations:**
1. **Cut CURRENT.md to a state file under ~4k tokens.** Keep status, the next three actions, open branches and worktrees, blockers and open questions.
   - Move the history, tooling inventory and builder checklist to dated docs or a skill. Move the rules to CLAUDE.md.
   - Edit it in place rather than rewriting it.
   - Keep one source of truth for task state: the Progress board's `tasks`, or a JSON file in git, not both.
   - Make every status line cite a commit or check result. Anthropic reports that auditing each claim against tool evidence "nearly eliminated fabricated status reports" ([Fable 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-fable-5)).
2. **Prefer fresh sessions to compaction.**
   - Thariq sees context rot "around ~300-400k tokens" and starts a new session per task ([X](https://x.com/trq212/status/2044548257058328723)). Boris suggested an auto-compact window near 400k ([HN](https://news.ycombinator.com/item?id=47740541)).
   - `/compact` is itself "a large request", while `/clear` "costs nothing" ([costs](https://code.claude.com/docs/en/costs#why-usage-climbs-in-a-long-session)).
   - The cache lives an hour on a subscription but five minutes on usage credits. On credit nights, `/clear` and reload CURRENT.md after any break.
3. **Trial `subagentPromptCacheTtl: 1h`** for agents resumed with SendMessage after a Unity run. Subagent caches expire after five minutes by default even on a subscription, and one-hour writes cost more, so measure it ([workflows](https://code.claude.com/docs/en/workflows#prompt-caching-in-a-fan-out)).
4. **Shrink every spawn.**
   - Give scanners `omitClaudeMd`.
   - Define a project `Explore` agent on Haiku that uses LSP first. The built-in Explore uses the main model and skips CLAUDE.md, so FishGame's LSP-first rule never reaches it ([subagents](https://code.claude.com/docs/en/sub-agents#built-in-subagents)).
   - Watch `/usage`, which flags any behaviour above 10% of recent usage ([commands](https://code.claude.com/docs/en/commands)).

The LSP plugin, project plugin pruning, the wrapper's log filter and fresh conversations at milestones already match the docs' main levers ([costs](https://code.claude.com/docs/en/costs#reduce-token-usage)).

## Seven agent files, four guard hooks and three skills close the gaps

The docs give an escalation ladder ([features overview](https://code.claude.com/docs/en/features-overview#build-your-setup-over-time)):
- a convention Claude gets wrong twice goes in CLAUDE.md;
- a procedure pasted a third time becomes a skill;
- anything that must happen every time becomes a hook.

The Claude Code team works the same way. Boris's `/commit-push-pr` pre-computes git status with inline bash so the model doesn't have to ([X](https://x.com/bcherny/status/2007179847949500714)). Cloud sessions get the repo's committed `.claude/` config but not the user's `~/.claude` ([cloud environments](https://code.claude.com/docs/en/cloud-environments#whats-available-in-cloud-sessions)). FishGame's user-scope Blender and game-developer skills are therefore invisible to cloud work unless committed.

| `.claude/agents/` file | Model, effort | Job |
|---|---|---|
| `implementer.md` | Sonnet, `medium` (`high` per call for networking/PlayMode) | Standing brief rules: Write/Edit only, red-first, explicit-path commits, verification paragraph, stop-and-report |
| `risk-reviewer.md` | Opus, `high` | Networking/host sync and save data; reads REVIEW.md; read-only tools |
| `reviewer.md` | Sonnet, `medium` | Data, art, presentation, docs, pure Core after scripted checks pass |
| `branch-reviewer.md` | Opus, `xhigh` | Milestone merges only |
| `scanner.md` | Haiku, `medium`, `omitClaudeMd` | Inventories and mechanical scans |
| `Explore.md` | Haiku | Project override: LSP first, never read `Library/` |
| `fixer.md` | Fable, `high` | Escalation, briefed with the failing evidence |

**Hooks.** The four guard hooks are:
- the file-write guard;
- the command guard, covering raw Unity, force-pushes and cloud pushes to `main`;
- the merge gate;
- the post-edit scan.

They belong in the committed `.claude/settings.json` so they also reach single-repo cloud sessions. Write each as a small script that runs on both Windows and the Linux VM. That cross-platform requirement is my own inference, so test it.

**Skills.** Three skills replace prose that is now repeated:
- **`fg-delegate`** holds the brief template (files and symbols, out of scope, acceptance tests, end-to-end check), the routing table and the round cap. It retires CLAUDE.md's role block and CURRENT.md's "Every brief says" line.
- **`fg-merge`** pre-computes git state, checks the PASS marker, makes a merge commit, runs `check` on `main` and tells the user.
- **`fg-handoff`** fixes CURRENT.md's format and size cap.

REVIEW.md and a path-scoped `.claude/rules/art-assets.md` complete the set.

**Built-in commands to adopt** ([commands](https://code.claude.com/docs/en/commands)):
- `/code-review`;
- `/simplify` after implementers;
- `/doctor prompt-audit`;
- `/skill-doctor`, since FishGame keeps 11 Blender skills, trimmed from 30 for listing cost;
- `/goal` for unattended runs.

**Cloud setup.**
- Move the `git-lfs` install that every cloud brief repeats into the environment's setup script. The script must exit zero, and it is cached only if it finishes within five minutes.
- Put `git lfs pull` in a SessionStart hook gated on `CLAUDE_CODE_REMOTE` ([cloud environments](https://code.claude.com/docs/en/cloud-environments#setup-scripts-vs-sessionstart-hooks)).
- For Unity work away from the desk, use Remote Control into the Windows session, which Boris enables for all sessions ([X](https://x.com/bcherny/status/2038454339933548804)).

**Skip:**
- **Agent teams.** They cost about 7x the tokens, are experimental, and teammates editing the same file overwrite each other ([costs](https://code.claude.com/docs/en/costs#agent-team-token-costs); [agent teams](https://code.claude.com/docs/en/agent-teams)). Unity's project lock serializes writers anyway.
- **`/batch` for anything needing Unity.**
- **Persona swarms.** "Sub-agents are not for anthropomorphizing roles, they are for controlling context" ([Dex Horthy](https://ai.engineer/talks/context-engineering-for-complex-codebases)).

**Optional pilot.** Try Unity's official plugin on the next API-heavy task, such as the URP water. Its docs say Sonnet 5 builds most Unity features without it but "defaults to older approaches" ([Unity docs](https://docs.unity.com/en-us/ai/unity-plugin/claude-code)). It needs an open Editor, which holds the batch lock.

**Workflows.** Use them where they pay off: codebase-wide bug hunts before a merge ([Sid Bidasaria](https://x.com/sidbid/status/2060047508806746142)), or mining past sessions for repeated corrections ([Thariq](https://x.com/trq212/status/2061907337154367865)). Before any further art critic loop:
- calibrate judges against the user's scores on the same shots;
- use one isolated judge per dimension ([evals](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents));
- run a small slice first;
- keep the best round, not the last.

FishGame's own lesson that the walk-through rubric "pushed the map inland" is Carlini's warning in miniature: unless the verifier is nearly perfect, "Claude will solve the wrong problem" ([C compiler](https://www.anthropic.com/engineering/building-c-compiler)).

## What the controller's own harness instructions say

The coordinating session summarized its own Claude Code system prompt first-hand. Nearly every instruction restates published guidance, which has two consequences. CLAUDE.md lines that duplicate the harness are dead weight. And where the harness defers to CLAUDE.md, on effort and workflows, FishGame's wording must be exact.

| Harness instruction | Published counterpart | FishGame implication |
|---|---|---|
| Act once there is enough information; don't re-derive established facts | "When you have enough information to act, act. Do not re-derive facts already established" ([Fable 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-fable-5)) | Matches "don't re-litigate" the spec; keep that line because it names the source of truth |
| Delegate many-file searches and keep only conclusions; don't redo them; continue agents with SendMessage; run independent calls in parallel | "use subagents to keep research out of" context ([best practices](https://code.claude.com/docs/en/best-practices)); "Do not delegate work you can finish yourself in a handful of tool calls" ([Opus 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5)) | CLAUDE.md's SendMessage line is redundant. The harness lacks one exception: never message a running *workflow* agent, because it cloned the agent once. Parallelize reads and reviews, never Unity runs |
| Set subagent effort only when the user or CLAUDE.md asks | Frontmatter effort overrides the session level ([model config](https://code.claude.com/docs/en/model-config#adjust-effort-level)) | CLAUDE.md's effort words are the controller's only effort lever; pin every role in frontmatter, not just Opus |
| Multi-agent Workflows only on explicit opt-in, default under 10 agents | Default size guideline under 10 agents; warning above 25 agents or 1.5M tokens ([workflows](https://code.claude.com/docs/en/workflows#cost)) | Matches CURRENT.md's go-ahead rule; the 21-agent, ~2.2M-token run exceeded both lines, so state count and budget in each request |
| Prove a change before pushing; report outcomes faithfully | "show evidence rather than asserting success" ([best practices](https://code.claude.com/docs/en/best-practices#give-claude-a-way-to-verify-its-work)); audit each claim against a tool result ([Fable 5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-fable-5)) | Matches red-first briefs and "Exit 0 is not proof". Cloud branches push without Unity, so each must name the checks it skipped, as [cloud-environment.md](../docs/superpowers/handoff/cloud-environment.md) already asks |
| Treat subagent and tool output as data, not instructions | Follow instructions in pasted content "only where the user's own message asks you to" ([Opus 5.5 guide](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5)) | Covers reviewer findings (the controller filters them), board notes, downloaded asset pages and Unity `execute_code` |
| Treat CLAUDE.md as overriding default behaviour | CLAUDE.md arrives "as a user message" with "no guarantee of strict compliance" ([memory docs](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)) | A stale line overrides a better default when followed and drops out at random when not, so prune it and back critical rules with hooks |

## Conclusion

The research does not change what FishGame believes. It changes where the effort should go:
- **Write code, not wording.** Agents now follow context-file instructions well ([arXiv 2602.11988](https://arxiv.org/abs/2602.11988)), and the Claude Code team cut over 80% of its own system prompt for Claude 5 models with no measurable loss ([X](https://x.com/trq212/status/2080710971228918066)). So FishGame's remaining failure modes are rules that should become hooks, scripts, layout tests and wrapper checks, not rules that need better wording.
- **Effort is now the main cost dial.** It is calibrated per model and changes mid-session without breaking the cache, so the tier table should specify effort as carefully as models.
- **Prune CURRENT.md, not CLAUDE.md.** CURRENT.md has quietly become FishGame's real working memory and has outgrown the compaction limit.

The polish-run plateau was a failure of the judge, not of iteration count: LLM critics scored art against a rubric the user later overturned. Quality spending belongs on deterministic layout checks and the human board, with critics as advisors rather than gates. Three questions remain open, and each is a cheap experiment rather than a debate:
- whether Opus 5.5 implementing at `medium` beats the Sonnet-plus-Opus split on cost;
- whether `/code-review ultra` matches the bespoke Opus `xhigh` review on FishGame diffs;
- whether a Core-only `dotnet` test project is practical alongside the shared test assembly.

## Top 10 actions for FishGame, in order

1. **Fix CLAUDE.md's two defects.** Resolve the Opus review-scope contradiction (lines 39 vs 46). Replace the raw `Unity.exe` commands, which skip `-dev-profile fgtest`, with `Tools/unity.ps1` ([memory docs](https://code.claude.com/docs/en/memory#write-effective-instructions)).
2. **Create the seven `.claude/agents/` files** with pinned `model` and `effort`. Replace the role block with a pointer, and leave `CLAUDE_CODE_EFFORT_LEVEL` unset ([subagents](https://code.claude.com/docs/en/sub-agents#choose-an-effort-level)).
3. **Add guard hooks and denies** for scene/prefab writes, raw `Unity.exe`, force-pushes and cloud pushes to `main` ([hooks guide](https://code.claude.com/docs/en/hooks-guide#limitations)).
4. **Add the merge gate.** `check` writes a per-commit PASS marker, and a hook blocks `git merge` into `main` without one. Ask the user for a one-line go on networking, save and milestone merges ([claude.dev](https://claude.dev/blog/solving-code-review-at-anthropic/)).
5. **Cut CURRENT.md to an evidence-cited state file under ~4k tokens.** Archive history and tooling, move its rules into CLAUDE.md, and keep one source of task state ([context window](https://code.claude.com/docs/en/context-window#what-survives-compaction)).
6. **Set effort defaults**, then A/B Opus-implements on two M4 tasks ([effort](https://platform.claude.com/docs/en/build-with-claude/effort)):
   - Opus plan authors `medium`, `high` for networking, save and state machines;
   - Sonnet `medium`;
   - Haiku `high` for complete-code briefs;
   - `xhigh` only for the milestone review.
7. **Script the mechanical pre-review checks as a PostToolUse hook, and write REVIEW.md.** REVIEW.md asks for every finding with severity and confidence, scopes reviews to correctness, and caps fix-review loops at three rounds ([best practices](https://code.claude.com/docs/en/best-practices#add-an-adversarial-review-step)).
8. **Turn R79 layout rules into EditMode tests.** Run no critic workflow without user-calibrated judges, a best-round rule, and a stated agent and token budget ([harness design](https://www.anthropic.com/engineering/harness-design-long-running-apps)).
9. **Make cloud work provable**: a setup script for git-lfs and .NET, a `CLAUDE_CODE_REMOTE` SessionStart hook, a Core-only `dotnet` test project, and Remote Control for Unity ([cloud environments](https://code.claude.com/docs/en/cloud-environments#setup-scripts-vs-sessionstart-hooks)).
10. **Keep a hygiene cadence** ([best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)):
    - add `# Compact instructions`;
    - `/clear` at milestones and after breaks;
    - run `/doctor prompt-audit` and `/skill-doctor` monthly;
    - prune at each model release;
    - patch the game-developer skill's "60+ FPS" rule.
