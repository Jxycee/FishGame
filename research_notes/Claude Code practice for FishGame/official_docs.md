# Official Claude Code guidance on getting the best results (late 2026)

> Retrieved 2026-10-17 as the raw Markdown of each docs page, so quotes are verbatim. Claude Code docs pages carry no per-page dates. The docs match Claude Code **v2.1.302**, the newest changelog entry, dated **October 16, 2026** ([changelog](https://code.claude.com/docs/en/changelog)). Where the docs tie a feature to a version, these notes give the version's release date from the changelog. The two blog posts were read through a summarizing fetch tool, so treat their quotes as near-verbatim and check them before quoting in a final report.
>
> Release dates used below: v2.1.219 = Jul 24 2026; v2.1.234 = Aug 17; v2.1.251 = Aug 28; v2.1.257 = Sep 1; v2.1.271 = Sep 14; v2.1.277 = Sep 18; v2.1.280 = Sep 22 (Opus 5.5); v2.1.283 = Sep 25; v2.1.284 = Sep 28 (Sonnet 5.5); v2.1.288 = Oct 2; v2.1.292 = Oct 6; v2.1.293 = Oct 7 (Haiku 5.5) — [changelog](https://code.claude.com/docs/en/changelog)

## 1. What does Anthropic say about CLAUDE.md length, structure, and how instructions degrade when too long or contradictory?

### Takeaway
Keep each CLAUDE.md **under 200 lines**. The docs say "Longer files consume more context and reduce adherence", and "if two instructions contradict each other, Claude may pick one arbitrarily". CLAUDE.md is advisory context, delivered as a user message rather than enforced configuration. Anything that must always happen belongs in a hook. Procedures and area-specific rules belong in skills or path-scoped `.claude/rules/`, which load on demand.

### Cited Findings

#### Length and degradation
- "**Size**: target under 200 lines per CLAUDE.md file. Longer files consume more context and reduce adherence. Move instructions that matter for only part of the codebase into path-scoped rules, which load only when Claude works with matching files. Imports help you organize a long file but don't reduce its context cost, because imported files also load at launch." — [Memory: Write effective instructions](https://code.claude.com/docs/en/memory#write-effective-instructions)
- "Files over 200 lines consume more context and may reduce adherence. Claude Code skips a file over 4 MiB." There is a startup and `/status` warning for over-length files, and "You also see a warning when files that are each within that length add up past a combined limit at session start. Each CLAUDE.md, rules file, and `@path` import counts as a separate file." — [Memory: My CLAUDE.md is too large](https://code.claude.com/docs/en/memory#my-claude-md-is-too-large)
- "Claude Code loads a CLAUDE.md file of up to 4 MiB in full and skips a larger file. Shorter files produce better adherence." — [Memory: Auto memory, How it works](https://code.claude.com/docs/en/memory#auto-memory)
- "Keep it concise. For each line, ask: *'Would removing this cause Claude to make mistakes?'* If not, cut it. Bloated CLAUDE.md files cause Claude to ignore your actual instructions!" — [Best practices: Write an effective CLAUDE.md](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)
- "If Claude keeps doing something you don't want despite having a rule against it, the file is probably too long and the rule is getting lost. If Claude asks you questions that are answered in CLAUDE.md, the phrasing might be ambiguous. Treat CLAUDE.md like code: review it when things go wrong, prune it regularly, and test changes by observing whether Claude's behavior actually shifts." — [Best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)
- Failure pattern: "**The over-specified CLAUDE.md.** If your CLAUDE.md is too long, Claude ignores half of it because important rules get lost in the noise. > **Fix**: Ruthlessly prune. If Claude already does something correctly without the instruction, delete it or convert it to a hook." — [Best practices: Avoid common failure patterns](https://code.claude.com/docs/en/best-practices#avoid-common-failure-patterns)
- "Aim to keep CLAUDE.md under 200 lines by including only essentials." — [Costs: Move instructions from CLAUDE.md to skills](https://code.claude.com/docs/en/costs#reduce-token-usage)
- Every feature consumes context: "Too much can fill up your context window, but it can also add noise that makes Claude less effective; skills may not trigger correctly, or Claude may lose track of your conventions." — [Features overview: Understand context costs](https://code.claude.com/docs/en/features-overview#understand-context-costs)
- On context in general: "LLM performance degrades as context fills. When the context window is getting full, Claude may start 'forgetting' earlier instructions or making more mistakes. The context window is the most important resource to manage." — [Best practices](https://code.claude.com/docs/en/best-practices)

#### Contradictions and emphasis
- "**Consistency**: if two instructions contradict each other, Claude may pick one arbitrarily. Review your CLAUDE.md files, nested CLAUDE.md files in subdirectories, and `.claude/rules/` periodically to remove outdated or conflicting instructions." — [Memory](https://code.claude.com/docs/en/memory#write-effective-instructions)
- User rules and project rules: "Neither set overrides the other: if a user rule and a project rule conflict, Claude may follow either one, so keep the two consistent." — [Memory: User-level rules](https://code.claude.com/docs/en/memory#organize-rules-with-claude/rules/)
- "If Claude keeps skipping one instruction, add emphasis such as 'IMPORTANT' to that line alone. If you emphasize many lines, none of them stands out." — [Best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)
- The general prompting guide says newer models overtrigger on aggressive wording: "dial back any aggressive language. Where you might have said 'CRITICAL: You MUST use this tool when...', you can use more normal prompting like 'Use this tool when...'." It also says "Instructions like 'If in doubt, use [tool]' will cause overtriggering." **Possibly dated:** the passage is written about Opus 4.5/4.6, though it is still on the current page — [Prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices)
- Same page: "Providing context or motivation behind your instructions, such as explaining to Claude why such behavior is important, can help Claude better understand your goals and deliver more targeted responses." It also says "Claude's latest models are trained for precise instruction following." — [Prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices)
- Audit tool: "run `/doctor prompt-audit` ... Claude looks for problems such as instructions written for older models, references to files or commands that don't exist, and files that contradict each other ... nothing in your files changes until you ask Claude to apply them." By default it covers CLAUDE.md, CLAUDE.local.md, AGENTS.md, and the rules, skills, commands, subagents and output styles under `.claude/`. It requires v2.1.283 (Sep 25 2026). — [Memory: Audit your instruction files](https://code.claude.com/docs/en/memory#audit-your-instruction-files)
- `/doctor` "proposes trims for a checked-in CLAUDE.md: it cuts content Claude can derive from the codebase, such as directory layouts, dependency lists, and architecture overviews, and keeps pitfalls, rationale, and conventions that differ from tool defaults." — [Memory](https://code.claude.com/docs/en/memory#my-claude-md-is-too-large)

#### Why compliance isn't guaranteed (advisory vs enforced)
- "CLAUDE.md content is delivered as a user message after the system prompt, not as part of the system prompt itself. Claude reads it and tries to follow it, but there's no guarantee of strict compliance, especially for vague or conflicting instructions." — [Memory: Claude isn't following my CLAUDE.md](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)
- "Claude treats them as context, not enforced configuration. To block an action regardless of what Claude decides, use a PreToolUse hook instead. The more specific and concise your instructions, the more consistently Claude follows them." — [Memory: CLAUDE.md vs auto memory](https://code.claude.com/docs/en/memory)
- "If the instruction is something that must run at a specific point, such as before every commit or after each file edit, write it as a hook instead. Hooks execute as shell commands at fixed lifecycle events and apply regardless of what Claude decides to do." — [Memory](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)
- Watch for conflicts with built-in guidance: "If your CLAUDE.md sets commit or pull request rules, turn off the built-in ones with `includeGitInstructions` and set the attribution text with `attribution`." — [Memory](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)
- Instructions needed at system-prompt level: use `--append-system-prompt`, which "is better suited to scripts and automation than interactive use." — [Memory](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)

#### What to put in, and what to leave out
- Add to CLAUDE.md when "Claude makes the same mistake a second time", when "A code review catches something Claude should have known about this codebase", when "You type the same correction or clarification into chat that you typed last session", or when "A new teammate would need the same context to be productive". — [Memory: When to add to CLAUDE.md](https://code.claude.com/docs/en/memory#when-to-add-to-claude-md)
- "Keep it to facts Claude should hold in every session: build commands, conventions, project layout, 'always do X' rules. If an entry is a multi-step procedure or only matters for one part of the codebase, move it to a skill or a path-scoped rule instead." — [Memory](https://code.claude.com/docs/en/memory#when-to-add-to-claude-md)
- Include: "Bash commands Claude can't guess", "Code style rules that differ from defaults", "Testing instructions and preferred test runners", "Repository etiquette (branch naming, PR conventions)", "Architectural decisions specific to your project", "Developer environment quirks (required env vars)", "Common gotchas or behaviors that aren't self-evident". Exclude: "Anything Claude can figure out by reading code", "Standard language conventions Claude already knows", "Detailed API documentation (link to docs instead)", "Information that changes frequently", "Long explanations or tutorials", "File-by-file descriptions of the codebase", "Self-evident practices like 'write clean code'". — [Best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)
- Write instructions concrete enough to verify: "'Use 2-space indentation' instead of 'Format code properly'"; "'Run `npm test` before committing' instead of 'Test your changes'"; "'API handlers live in `src/api/handlers/`' instead of 'Keep files organized'". — [Memory](https://code.claude.com/docs/en/memory#write-effective-instructions)
- "**Structure**: group related instructions under markdown headers and bullets. Organized sections are easier for Claude to follow than dense paragraphs." — [Memory](https://code.claude.com/docs/en/memory#write-effective-instructions)
- "CLAUDE.md is loaded every session, so only include things that apply broadly. For domain knowledge or workflows that are only relevant sometimes, use skills instead." — [Best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md)
- When to add each feature: "Claude gets a convention or command wrong twice → Add it to CLAUDE.md"; "You paste the same playbook or multi-step procedure into chat for the third time → Capture it as a skill"; "You want something to happen every time without asking → Write a hook". — [Features overview: Build your setup over time](https://code.claude.com/docs/en/features-overview#build-your-setup-over-time)
- Compaction instructions can live in CLAUDE.md, e.g. "When compacting, always preserve the full list of modified files and any test commands". — [Best practices: Manage context aggressively](https://code.claude.com/docs/en/best-practices#manage-context-aggressively); a `# Compact instructions` section example is in [Costs](https://code.claude.com/docs/en/costs#reduce-token-usage)
- "Block-level HTML comments (`<!-- maintainer notes -->`) in CLAUDE.md files are stripped before the content is injected into Claude's context. Use them to leave notes for human maintainers without spending context tokens on them." — [Memory: How CLAUDE.md files load](https://code.claude.com/docs/en/memory#how-claude-md-files-load)
- `/init` generates a starter file; "If a CLAUDE.md already exists, `/init` suggests improvements rather than overwriting it." `CLAUDE_CODE_NEW_INIT=1` runs an interactive flow covering CLAUDE.md, skills and hooks. — [Memory: Set up a project CLAUDE.md](https://code.claude.com/docs/en/memory#set-up-a-project-claude-md)

#### Locations, load order, nesting, imports, rules
- Scopes, in load order: managed policy, then user `~/.claude/CLAUDE.md`, then project `./CLAUDE.md` or `./.claude/CLAUDE.md`, then local `./CLAUDE.local.md` ("add to `.gitignore`"). — [Memory: Choose where to put CLAUDE.md files](https://code.claude.com/docs/en/memory#choose-where-to-put-claude-md-files)
- "All discovered files are concatenated into context rather than overriding each other ... instructions closer to where you launched Claude are read last. Within each directory, `CLAUDE.local.md` is appended after `CLAUDE.md`." — [Memory: How CLAUDE.md files load](https://code.claude.com/docs/en/memory#how-claude-md-files-load)
- Nested CLAUDE.md files: "Instead of loading them at launch, Claude Code loads each one once Claude reads, writes, or edits another file in that subdirectory." — [Memory](https://code.claude.com/docs/en/memory#how-claude-md-files-load)
- Imports: "`@path/to/import` ... Relative paths resolve relative to the file containing the import, not the working directory. Imported files can recursively import other files, with a maximum depth of four hops." Also: "Import parsing skips Markdown code spans and fenced code blocks." An import in a project-level file whose path "resolves outside your working directory" is external and triggers a one-time approval dialog; user-scope files' imports load without it. — [Memory: Import additional files](https://code.claude.com/docs/en/memory#import-additional-files)
- Worktrees: "a gitignored `CLAUDE.local.md` only exists in the worktree where you created it. To share personal instructions across worktrees, import a file from your home directory instead." — [Memory](https://code.claude.com/docs/en/memory#import-additional-files)
- `.claude/rules/`: "Each file should cover one topic, with a descriptive filename like `testing.md`." "Rules without `paths` frontmatter are loaded at launch with the same priority as `.claude/CLAUDE.md`." Path-scoped rules ("`paths:` ... glob patterns") load "when Claude uses the Read, Write, or Edit tool on a matching file". "`paths` is the only field Claude Code reads from a rule." — [Memory: Organize rules](https://code.claude.com/docs/en/memory#organize-rules-with-claude/rules/)
- Rules vs skills: "Rules load into context every session or when matching files are opened. For task-specific instructions that don't need to be in context all the time, use skills instead, which only load when you invoke them or when Claude determines they're relevant to your prompt." — [Memory](https://code.claude.com/docs/en/memory#organize-rules-with-claude/rules/)
- `AGENTS.md` is read by default "only when you have no `CLAUDE.md` or `CLAUDE.local.md` in your working directory or above it". This needs v2.1.277 or later. — [Memory: My AGENTS.md isn't loading](https://code.claude.com/docs/en/memory#my-agentsmd-isnt-loading)
- Debugging: run `/context` and check **Memory files**. The `InstructionsLoaded` hook can "log which `CLAUDE.md` and rules files are loaded, when they load, and why." — [Memory](https://code.claude.com/docs/en/memory#claude-isnt-following-my-claude-md)

#### Auto memory
- Four note types (`user`, `feedback`, `project`, `reference`). "Claude skips anything it can derive from the codebase ... It also skips anything your CLAUDE.md files already say." — [Memory: Auto memory](https://code.claude.com/docs/en/memory#auto-memory)
- "The first 200 lines of `MEMORY.md`, or the first 25KB, whichever comes first, are loaded at the start of every conversation." Topic files are read on demand. — [Memory: How it works](https://code.claude.com/docs/en/memory#auto-memory)
- Auto memory is per repository and "shared across worktrees". The main conversation's auto memory "isn't loaded into subagents" except forks. — [Memory](https://code.claude.com/docs/en/memory)

#### Instructions after compaction, and edits mid-session
- "Project-root CLAUDE.md survives compaction: after `/compact`, Claude re-reads it from disk and re-injects it into the session. Nested CLAUDE.md files in subdirectories and rules with `paths:` frontmatter load again on demand." An instruction lost after compaction "was given only in conversation, lives in a nested CLAUDE.md that hasn't reloaded yet, or is a path-scoped rule that hasn't matched a file since. Add conversation-only instructions to CLAUDE.md to make them persist." — [Memory: Instructions seem lost after /compact](https://code.claude.com/docs/en/memory#instructions-seem-lost-after-compact)
- "If a rule must persist across compaction, drop the `paths:` frontmatter or move it to the project-root CLAUDE.md." — [Context window: What survives compaction](https://code.claude.com/docs/en/context-window#what-survives-compaction)
- Editing CLAUDE.md mid-session: "Editing them mid-session does not invalidate the cache, but the edit also doesn't apply. Claude keeps working with the version that was loaded at session start. The new content loads on the next `/clear`, `/compact`, or restart." — [Prompt caching: Editing CLAUDE.md mid-session](https://code.claude.com/docs/en/prompt-caching)

### Inferences
- FishGame's `CLAUDE.md` is 50 lines (`wc -l`), well under the 200-line target. Several lines are long paragraphs, though: the asset-sourcing bullet and the model-tier paragraph. The docs prefer "headers and bullets" over "dense paragraphs", so splitting those into bullets should help adherence more than cutting lines.
- Several FishGame entries are procedures or area-specific rules, which the docs would move out of CLAUDE.md:
  - Unity CLI batchmode steps → a skill.
  - Agent-role and model-tier policy → subagent definitions with `model`/`effort` frontmatter, or a skill used when orchestrating.
  - Asset licensing → a path-scoped rule on `Assets/**`.
- "Never commit `Library/`, `Temp/`…" and "check for another PlayMode run" are must-always rules. The docs recommend hooks for these, e.g. a PreToolUse hook on `git commit`/`git add`, or on the Unity test command.
- Dated "user, 2026-10-14" provenance tags cost tokens every session. The docs suggest HTML comments for maintainer-only notes. The docs also say rationale helps, and `/doctor` keeps "rationale", so keep short "why" clauses and move only the provenance into comments.
- `/doctor prompt-audit` is a cheap, official way to find contradictions between FishGame's CLAUDE.md, `.claude/settings.json`-driven skills, and handoff docs.
- FishGame updates CLAUDE.md with new user preferences mid-session. An edit made that way "doesn't apply" to the running session until `/clear`, `/compact` or restart. Long-running controller sessions should restate a new rule in conversation as well.

### Gaps
- No numeric value was found for the "combined limit" across multiple instruction files that triggers the startup warning.
- The docs give no quantitative adherence data (for example, how adherence falls off by line count). The 200-line figure is a recommendation without published evidence.
- The "dial back aggressive language" guidance is explicitly about Opus 4.5/4.6. The 5.5-family prompting guides I read neither restate nor contradict it.

## 2. What do the docs recommend for model choice per task and effort levels (low → max, xhigh, ultracode)?

### Takeaway
On the Anthropic API and on Pro/Max/Team/Enterprise plans, Claude Code's `default` is Opus 5.5, and Opus 5.5, Sonnet 5.5 and Haiku 5.5 default to **`medium`** effort. The docs map effort to work type:
- `low`: in-the-loop sketches.
- `medium`: scoped feature work.
- `high`: verification-heavy and brownfield bug fixes.
- `xhigh`: deeper reasoning.
- `max`: autonomous hard problems; "prone to overthinking".

For model choice, the costs page says Sonnet for most coding and Opus for complex architecture or multi-step reasoning. The platform docs say "Most workloads start with Claude Opus 5.5", with Haiku for sub-agent tasks and Fable 5.1 when Opus at `xhigh`/`max` falls short. Subagents, skills, workflow agents and teammates each take their own `model`/`effort`.

### Cited Findings

#### Models and aliases in Claude Code
- Aliases:
  - `best`: Fable where available, otherwise Opus.
  - `fable`: "for your hardest and longest-running tasks".
  - `sonnet`: "for daily coding tasks".
  - `opus`: "for complex reasoning tasks".
  - `haiku`: "for simple tasks".
  - `opusplan`: "uses `opus` during plan mode, then switches to `sonnet` for execution".

  On the Anthropic API, `opus`, `sonnet` and `haiku` resolve to Opus 5.5, Sonnet 5.5 and Haiku 5.5. — [Model config: Model aliases](https://code.claude.com/docs/en/model-config#model-aliases)
- `default` → "**Pro, Max, Team, Enterprise, and Anthropic API**: defaults to Opus 5.5". It resolved to other models before v2.1.280 (Sep 22 2026). "Fable models are not the account-type default on any plan or provider." — [Model config: default model setting](https://code.claude.com/docs/en/model-config#default-model-setting)
- `opusplan`: "In plan mode: uses `opus` for complex reasoning and architecture decisions. In execution mode: automatically switches to `sonnet` for code generation and implementation." Caveat from the caching page: "each plan-mode toggle is a model switch and starts a fresh cache." — [Model config: opusplan](https://code.claude.com/docs/en/model-config#opusplan-model-setting); [Prompt caching: Switching models](https://code.claude.com/docs/en/prompt-caching#switching-models)
- Cost-page guidance: "Sonnet handles most coding tasks well and costs less than Opus. Reserve Opus for complex architectural decisions or multi-step reasoning ... A switch to Opus also applies to the subagents that inherit your session's model. For simple subagent tasks, specify `model: haiku` in your subagent configuration." — [Costs: Choose the right model](https://code.claude.com/docs/en/costs#reduce-token-usage)
- Admin view: "Unexpectedly high spend ... usually traces back to long sessions that were never cleared or to Opus left as the default model." — [Costs: When a developer asks about a limit](https://code.claude.com/docs/en/costs)

#### Platform model-selection guidance (API docs, current 5.5 family)
- Matrix: "Most workloads start with Claude Opus 5.5."
  - Fable 5.1: "The highest available capability", e.g. "Agent sessions that run for hours".
  - Opus 5.5: "Complex agentic coding and enterprise work", e.g. "Multihour autonomous coding agents, large-scale refactoring".
  - Sonnet 5.5: "Speed and capability for everyday coding, agent, and enterprise workloads".
  - Haiku 5.5: "The lowest latency and price", e.g. "sub-agent tasks".

  — [Choosing a model](https://platform.claude.com/docs/en/about-claude/models/choosing-a-model)
- "Tuning effort is often a better lever than switching models." For Opus 5.5 and Haiku 5.5: "the default is `medium`; start there and adjust". Capability-first path: "If your evals at `xhigh` or `max` effort still fall short on demanding reasoning or long-horizon agentic work, move to Claude Fable 5.1." — [Choosing a model](https://platform.claude.com/docs/en/about-claude/models/choosing-a-model)
- Combining models: "Multi-model strategies pair a lower-cost model with a frontier model so that most tokens are billed at the lower rate ... an executor that escalates hard decisions to an advisor, and an orchestrator that delegates bulk work to lower-cost workers." — [Choosing a model](https://platform.claude.com/docs/en/about-claude/models/choosing-a-model). Claude Code exposes the advisor pattern as the "advisor tool" ([Model config](https://code.claude.com/docs/en/model-config#opusplan-model-setting)); I did not read that page.
- Blog, Jul 7 2026 (Lydia Hallie, via fetch summary): "Choose smaller models for more routine tasks and larger models for more complex or ambiguous tasks." "Larger models are also better at handling ambiguity." Dropping to a smaller model for "routine stretches saves real money at no quality cost". For effort, "for most tasks you should use the model's default effort level." **Partly dated:** it discusses Opus 4.8, before Opus 5/5.5. — [Choosing a Claude model and effort level in Claude Code](https://claude.com/blog/claude-model-and-effort-level-in-claude-code)

#### Effort levels in Claude Code
- Supported levels: Fable 5.1/5 and Opus 5.5, Sonnet 5.5, Haiku 5.5, Opus 5, Sonnet 5, Opus 4.8, Opus 4.7 support "`low`, `medium`, `high`, `xhigh`, `max`". Opus 4.6 and Sonnet 4.6 lack `xhigh`. An unsupported level falls back, so "`xhigh` runs as `high` on Opus 4.6". — [Model config: Adjust effort level](https://code.claude.com/docs/en/model-config#adjust-effort-level)
- Defaults: "`high` on every model that supports effort, except that Opus 5.5, Sonnet 5.5, and Haiku 5.5 default to `medium`, Opus 4.7 defaults to `xhigh`". — [Model config](https://code.claude.com/docs/en/model-config#adjust-effort-level)
- Choosing a level (verbatim table):
  - `low`: "Quick exchanges where you review each result, such as brainstorming, a first sketch, or a small change like a rename".
  - `medium`: "On Opus 5.5 and Sonnet 5.5, it fits day-to-day engineering work with a clear scope, such as implementing a new feature".
  - `high`: "Work where verification matters or edge cases are likely, such as fixing a bug in an existing codebase".
  - `xhigh`: "Deeper reasoning at higher token spend".
  - `max`: "Hard problems you want Claude to work through without you, such as finding security vulnerabilities. `max` may show diminishing returns and is prone to overthinking, so test before adopting it broadly".
  - `ultracode`: "A Claude Code setting rather than a level: plans a dynamic workflow for each substantive task at any effort level".

  — [Model config: Choose an effort level](https://code.claude.com/docs/en/model-config#choose-an-effort-level)
- "In tests on Opus 5.5 and Fable 5.1, Claude at a higher level tested more edge cases and verified more of its work before answering. It also made more choices on its own. At a lower level, Claude returned a starting point sooner, which fits work where you review each result and steer the next step." — [Model config](https://code.claude.com/docs/en/model-config#choose-an-effort-level)
- "The effort scale is calibrated per model, so the same level name does not represent the same underlying value across models." "In Anthropic's testing, Opus 5.5 at `medium` matches or exceeds Opus 5 at `high` on coding and knowledge-work evaluations ... When you move from Opus 5 to Opus 5.5, start at `medium` rather than carrying over the level you used on Opus 5." — [Model config](https://code.claude.com/docs/en/model-config#choose-an-effort-level)
- Resolution order: env var `CLAUDE_CODE_EFFORT_LEVEL`, `--effort` or `/effort` → saved settings (`modelSettings` per model, or `effortLevel`) → model default. "`max` ... Unless you set it through the `CLAUDE_CODE_EFFORT_LEVEL` environment variable, Claude Code applies `max` to the current session only." `max` isn't accepted in the settings keys. — [Model config](https://code.claude.com/docs/en/model-config#adjust-effort-level)
- One-off deep reasoning: "Include `ultrathink` anywhere in your prompt to request deeper reasoning on that turn without changing your session effort setting ... Claude Code passes other phrases such as 'think', 'think hard', and 'think more' through as ordinary prompt text and doesn't recognize them as keywords." — [Model config: Use ultrathink](https://code.claude.com/docs/en/model-config#adjust-effort-level)
- Prompts can steer thinking: "If you want Claude to think more or less often than the current level produces, you can say so directly in your prompt or in `CLAUDE.md`; the model responds to that guidance within its effort setting." — [Model config: Adaptive reasoning](https://code.claude.com/docs/en/model-config#adjust-effort-level)
- Thinking can't be turned off on Opus 5.5, Sonnet 5.5, Haiku 5.5 or Fable. "You are charged for all thinking tokens generated, even when collapsed or redacted." — [Model config: Extended thinking](https://code.claude.com/docs/en/model-config#extended-thinking)
- Ultracode: "With it on, Claude orchestrates dynamic workflows for substantive tasks." `claude --effort ultracode` "starts the session at `xhigh` effort with ultracode on". Each request "uses more tokens and takes longer ... a session with ultracode on reaches a session or weekly limit sooner." The docs advise "Turn it off with `/effort ultracode off` when you return to routine work." — [Model config](https://code.claude.com/docs/en/model-config#adjust-effort-level); [Workflows: Let Claude decide with ultracode](https://code.claude.com/docs/en/workflows#let-claude-decide-with-ultracode)

#### Model and effort per subagent, skill, workflow and teammate
- Subagent `model` field takes an alias (`sonnet`, `opus`, `haiku`, `fable`), a full ID, or `inherit`. Resolution order: "1. The per-invocation `model` parameter 2. The subagent definition's `model` frontmatter ... 3. The `CLAUDE_CODE_SUBAGENT_MODEL` environment variable ... 4. The main conversation's model". "Before v2.1.251, `CLAUDE_CODE_SUBAGENT_MODEL` came first in this order" (changed Aug 28 2026). — [Subagents: Choose a model](https://code.claude.com/docs/en/sub-agents#choose-a-model)
- To force one model on every subagent, teammate and workflow agent, set `CLAUDE_CODE_SUBAGENT_MODEL` plus `CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1`. This needs v2.1.257 (Sep 1 2026). — [Subagents: Run every subagent on one model](https://code.claude.com/docs/en/sub-agents#run-every-subagent-on-one-model)
- Subagent effort: "set the `effort` field in its definition." Claude can also pass a per-invocation `effort` parameter, which overrides the field and persists on resume; this needs v2.1.292 (Oct 6 2026). "The `CLAUDE_CODE_EFFORT_LEVEL` environment variable takes precedence over both." — [Subagents: Choose an effort level](https://code.claude.com/docs/en/sub-agents#choose-an-effort-level)
- Skill and subagent frontmatter: "Frontmatter effort applies when that skill or subagent is active, overriding the session level but not the environment variable." — [Model config: Set the effort level](https://code.claude.com/docs/en/model-config#adjust-effort-level). A skill's `model` "applies for the rest of the current turn and isn't saved to settings". — [Skills: Frontmatter reference](https://code.claude.com/docs/en/skills#frontmatter-reference)
- Built-in Explore's model is "the main conversation's model" (or the `opus` alias's model when the main conversation runs Fable on a subscription, Console or gateway connection). The docs suggest defining a project `Explore` "with `model: haiku` to run exploration on a lower-cost model." — [Subagents: Built-in subagents](https://code.claude.com/docs/en/sub-agents#built-in-subagents)
- Subagents inherit the session's thinking on/off (since v2.1.198). "a subagent's context window is sized by its own model, not the parent's." — [Subagents](https://code.claude.com/docs/en/sub-agents#what-loads-at-startup)
- Workflows: "Claude Code picks each workflow agent's model in the same order it uses for subagents ... Ask Claude to use a smaller model for stages that don't need the strongest one." — [Workflows: Cost](https://code.claude.com/docs/en/workflows#cost)
- Agent teams: "Use Sonnet for teammates. It balances capability and cost for coordination tasks." Teammates inherit the lead's effort by default, and `teammateDefaultModel` was removed in v2.1.234; name the model in the spawn prompt. — [Costs: Agent team token costs](https://code.claude.com/docs/en/costs#agent-team-token-costs); [Agent teams: Specify teammates and models](https://code.claude.com/docs/en/agent-teams)

#### Platform effort guidance (5.5 family)
- "Effort is a behavioral signal, not a strict token budget." Effort "affects **all tokens** in the response, including ... Tool calls and function arguments ... Lower effort also means fewer and terser tool calls." The table maps `low` to "Simpler tasks that need the best speed and lowest costs, such as subagents", and `xhigh` to "Long-running agentic and coding tasks (over 30 minutes)". — [Effort](https://platform.claude.com/docs/en/build-with-claude/effort)
- Opus 5.5: "`medium` is the default ... Run an effort sweep on your own evals rather than carrying settings over from an earlier model." The Opus 5.5 guide adds "Reserve `xhigh` and `max` for work where you've measured a quality gain" and "To get less thinking, lower the effort level first. Lowering effort reduces thinking, and with it cost and latency, more reliably than prompt instructions do." — [Effort](https://platform.claude.com/docs/en/build-with-claude/effort); [Prompting Opus 5.5: Calibrate effort](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5#calibrate-effort)
- Opus 5.5 capability note: "at its default `medium` effort the model matched or beat Claude Opus 5 at `high` effort on such tasks, in fewer steps and with fewer tokens." Early testers reported "stronger code review, with more bugs caught than on Claude Opus 5 and fewer false alarms". — [Prompting Opus 5.5](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5)
- Sonnet 5.5: "For agentic coding and multistep tool use, start at `medium` for well-specified tasks and move to `high` for harder or longer ones." At `low` it "can skip verifying a change". At `low`/`medium` "on long agentic tasks, it's more likely to stop and check in". — [Prompting Sonnet 5.5: Calibrate effort](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5-5#calibrate-effort)
- **Discrepancy to note:** Claude Code docs say Sonnet 5.5 defaults to `medium` in Claude Code ([Model config](https://code.claude.com/docs/en/model-config#adjust-effort-level)). The platform docs say "`high` is the default on the Claude API" for Sonnet 5.5 ([Effort](https://platform.claude.com/docs/en/build-with-claude/effort)). Both can be true, since Claude Code sets its own default, but don't conflate the two.
- Haiku 5.5: "**Start with `medium`** for most work, including agentic coding." "In long agent prompts, the model is more likely to skip a search, stop early, or skip a check at `low`." Use `high` "for knowledge work, longer agent tasks, and strict instruction following." — [Effort: Haiku 5.5](https://platform.claude.com/docs/en/build-with-claude/effort); [Prompting Haiku 5.5](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-haiku-5-5)
- Fable 5.1: "**Start with `high`, the default.** Step up to `xhigh` or `max` for the most capability-sensitive agentic and coding work." — [Effort](https://platform.claude.com/docs/en/build-with-claude/effort)
- Blog, Sep 25 2026 (Thariq Shihipar, via fetch summary): a rule of thumb.
  - Low: "brainstorming, sketching, easy changes".
  - Medium: "most of my regular software engineering work".
  - High: "work where verification is important or there are edge cases, e.g. fixing a bug in a brownfield codebase".
  - Max: "operate fully autonomously to solve difficult problems".

  Also: "increasing effort tends to reduce failures due to missing edgecases ..., but does not fix when the model has the wrong approach." The recommended loop is: implement on low, review and iterate on low, then "Verify and test on high." — [Using Claude Code: Spending your effort](https://claude.dev/blog/spending-your-effort/)

### Inferences
- FishGame's CLAUDE.md tiers:
  - "Opus (effort high)" authors and reviewers.
  - "Opus (xhigh) final branch review".
  - Sonnet implements.
  - Haiku "only when the brief contains complete code".
  - Fable escalation.

  This broadly matches official guidance: Haiku for simple, fully specified subagent work; Fable when Opus at `xhigh`/`max` falls short; `high` where verification matters.
- One calibration point: on Opus 5.5 the docs say `medium` ≈ Opus 5 `high` and recommend starting at `medium`. The CLAUDE.md "Opus (effort high)" reviews may cost more than needed. That is defensible for networking and host-sync reviews ("verification matters or edge cases are likely") but worth measuring.
- The tiers can be encoded in `.claude/agents/*.md` frontmatter (`model:` + `effort:`), so they become configuration rather than prose. Claude can still override them per invocation.
- `CLAUDE_CODE_EFFORT_LEVEL` overrides subagent and skill frontmatter effort. If set globally, it would flatten FishGame's per-role effort tiers.
- Toggling `opusplan` or switching `/model` mid-session costs a full cache miss. Switching effort does not on Opus/Sonnet/Haiku 5.5 or Fable 5.1 (API key or subscription), which favours per-agent model choice over mid-session switching.

### Gaps
- The docs give no official per-role matrix for "reviewer vs implementer vs planner" model and effort. Recommendations are by task type and are mostly qualitative.
- No official guidance found on `xhigh` in Claude Code beyond "Deeper reasoning at higher token spend". The Sep 2026 blog gives no rule of thumb for `xhigh` either.
- The Jul 2026 model/effort blog predates Opus 5/5.5. I found no updated equivalent; its model-specific claims may be outdated.
- I did not read the advisor tool page (`/docs/en/advisor`), which is relevant to the executor-plus-advisor pattern.

## 3. What verification patterns (hooks, tests, reviews) are recommended?

### Takeaway
The headline best practice is "Give Claude a way to verify its work": give it a check it can run (tests, build, linter, screenshot), and ask for evidence rather than claims. The docs list four escalating gates:
1. Ask in the prompt.
2. `/goal` across turns.
3. A deterministic **Stop hook** that blocks ending until the check passes (capped at 8 consecutive blocks).
4. A fresh-context **verification subagent** or cross-checking workflow, with the reviewer told to report only correctness gaps.

PostToolUse hooks handle per-edit format and lint. Cloud sessions need setup scripts or SessionStart hooks so the checks can actually run.

### Cited Findings

#### Core principle
- "Give Claude a check it can run: tests, a build, a screenshot to compare. It's the difference between a session you watch and one you walk away from." "Claude stops when the work looks done. Without a check it can run, 'looks done' is the only signal available, and you become the verification loop." — [Best practices: Give Claude a way to verify its work](https://code.claude.com/docs/en/best-practices#give-claude-a-way-to-verify-its-work)
- Prompt pattern examples: "run the tests after implementing"; "take a screenshot of the result and compare it to the original. list differences and fix them"; "address the root cause, don't suppress the error". — [Best practices](https://code.claude.com/docs/en/best-practices#give-claude-a-way-to-verify-its-work)
- Four gating levels (verbatim):
  - "**In one prompt**: ask Claude to run the check and iterate in the same message".
  - "**Across a session**: set the check as a `/goal` condition. A separate evaluator re-checks it after every turn".
  - "**As a deterministic gate**: a Stop hook runs your check as a script and blocks the turn from ending until it passes".
  - "**By a second opinion**: a verification subagent or a dynamic workflow that checks its own findings has a fresh model try to refute the result, so the agent doing the work isn't the one grading it".

  "The `/goal` and Stop hook versions are what let an unattended run finish correctly without you." — [Best practices](https://code.claude.com/docs/en/best-practices#give-claude-a-way-to-verify-its-work)
- "Have Claude show evidence rather than asserting success: the test output, the command it ran and what it returned, or a screenshot of the result." — [Best practices](https://code.claude.com/docs/en/best-practices#give-claude-a-way-to-verify-its-work)
- Failure pattern: "**The trust-then-verify gap.** ... **Fix**: Always provide verification (tests, scripts, screenshots). If you can't verify it, don't ship it." — [Best practices](https://code.claude.com/docs/en/best-practices#avoid-common-failure-patterns)
- Bundled skill `/verify`: "Confirm a code change does what it should by building your project's app, running it, and observing the result, rather than relying on tests or type checks." — [Commands](https://code.claude.com/docs/en/commands)
- "Test incrementally: Write one file, test it, then continue." — [Costs: Work efficiently on complex tasks](https://code.claude.com/docs/en/costs#reduce-token-usage)

#### Hooks as deterministic verification
- "Use hooks for actions that must happen every time with zero exceptions ... Unlike CLAUDE.md instructions which are advisory, hooks are deterministic and guarantee the action happens." Example prompts: "Write a hook that runs eslint after every file edit". — [Best practices: Set up hooks](https://code.claude.com/docs/en/best-practices#set-up-hooks)
- Post-edit format and lint recipe: "uses the `PostToolUse` event with an `Edit|Write` matcher ... `jq -r '.tool_input.file_path' | xargs npx prettier --write`". "To reformat a specific file however it changes, including when a `Bash` command rewrites it, use a FileChanged hook instead." — [Hooks guide: Auto-format code after edits](https://code.claude.com/docs/en/hooks-guide#auto-format-code-after-edits)
- Exit code 2 per event:
  - `PostToolUse`: "Shows stderr to Claude; the tool already ran". This is how a lint or test failure gets fed back.
  - `Stop`: "Prevents Claude from stopping, continues the conversation".
  - `PreToolUse`: "Blocks the tool call".
  - `PostToolBatch`: "Stops the agentic loop before the next model call".

  — [Hooks reference: Exit code 2 behavior per event](https://code.claude.com/docs/en/hooks#exit-code-2-behavior-per-event)
- PostToolUse JSON control: `decision: "block"` "adds the `reason` next to the tool result". `additionalContext` adds context, and `updatedToolOutput` replaces output. — [Hooks reference: PostToolUse decision control](https://code.claude.com/docs/en/hooks#posttooluse-decision-control)
- Long checks in the background: the hook field `asyncRewake` "runs in the background and wakes Claude on exit code 2. The hook's stderr ... is shown to Claude as a system reminder so it can react to a long-running background failure". — [Hooks reference](https://code.claude.com/docs/en/hooks)
- Stop hook semantics: it runs "when the main Claude Code agent has finished responding. Does not run if the stoppage occurred due to a user interrupt." "Claude Code applies an 8-consecutive-continuation cap ... The count of consecutive continuations resets each time Claude calls a tool. To raise the cap, set `CLAUDE_CODE_STOP_HOOK_BLOCK_CAP`." Input includes `stop_hook_active`, `last_assistant_message`, `background_tasks` and `session_crons`. — [Hooks reference: Stop](https://code.claude.com/docs/en/hooks#stop)
- Stop-hook hygiene: "Your hook script needs to check whether it already triggered a continuation. Parse the `stop_hook_active` field from the JSON input and exit early if it's `true`." — [Hooks guide: Stop hook hits the block cap](https://code.claude.com/docs/en/hooks-guide#stop-hook-hits-the-block-cap)
- "`Stop` hooks fire whenever Claude finishes responding, not only at task completion." — [Hooks guide: Limitations](https://code.claude.com/docs/en/hooks-guide#limitations)
- `/goal` "is a built-in shortcut for a session-scoped prompt-based Stop hook." — [Hooks reference: Stop](https://code.claude.com/docs/en/hooks#stop)
- Prompt-based hooks (`type: "prompt"`) are for "decisions that require judgment rather than deterministic rules". Example: a Stop hook asking "Check if all tasks are complete. If not, respond with {"ok": false, "reason": "what remains to be done"}." — [Hooks guide: Prompt-based hooks](https://code.claude.com/docs/en/hooks-guide#prompt-based-hooks)
- Agent-based hooks (`type: "agent"`) are "**experimental** ... For production workflows, prefer command hooks". They "spawn a subagent that can read files, search code, and use other tools", with a 60s default timeout and up to 50 tool-use turns. Example: a Stop hook that should "Verify that all unit tests pass. Run the test suite and check the results." "Use prompt hooks when the hook input data alone is enough to make a decision. Use agent hooks when you need to verify something against the actual state of the codebase." — [Hooks guide: Agent-based hooks](https://code.claude.com/docs/en/hooks-guide#agent-based-hooks)
- Hook timeouts: command, http and mcp_tool get 10 min; prompt 30 s; agent 60 s. "`PostToolUse` hooks can't undo actions since the tool has already executed." — [Hooks guide: Limitations](https://code.claude.com/docs/en/hooks-guide#limitations)
- Policy enforcement: "`PreToolUse` hooks fire before any permission-mode check, in every permission mode ... A hook that returns `permissionDecision: "deny"` blocks the tool even in `bypassPermissions` mode or with `--dangerously-skip-permissions`." Hooks "can tighten restrictions but not loosen them past what permission rules allow." — [Hooks guide: Hooks and permission modes](https://code.claude.com/docs/en/hooks-guide#limitations)
- Rules that must hold every time inside a skill: "move the rule into a hook ... To keep the rule with the skill, define the hook in the skill's `hooks` frontmatter." — [Skills: Claude stops following a skill](https://code.claude.com/docs/en/skills)

#### Reviews in a fresh context (subagent, writer/reviewer, workflows)
- "Before treating a task as done, have a subagent review the diff in a fresh context and report gaps." "A reviewer running in a fresh subagent context sees only the diff and the criteria you give it, not the reasoning that produced the change." Use bundled `/code-review` for correctness, or a custom prompt against the plan, e.g. "Report gaps, not style preferences." — [Best practices: Add an adversarial review step](https://code.claude.com/docs/en/best-practices#add-an-adversarial-review-step)
- Reviewer calibration: "A reviewer prompted to find gaps will usually report some, even when the work is sound ... Chasing every finding leads to over-engineering: extra abstraction layers, defensive code, and tests for cases that can't happen. Tell the reviewer to flag only gaps that affect correctness or the stated requirements, and treat the rest as optional." — [Best practices](https://code.claude.com/docs/en/best-practices#add-an-adversarial-review-step)
- Writer/Reviewer sessions: "A fresh context improves code review since Claude won't be biased toward code it just wrote." Similarly: "have one Claude write tests, then another write code to pass them." — [Best practices: Run multiple Claude sessions](https://code.claude.com/docs/en/best-practices#run-multiple-claude-sessions)
- `/code-review [low|medium|high|xhigh|max|ultra] [--fix] [--comment]` reviews the diff for correctness bugs. `/simplify` runs "Four review agents" for cleanup and "doesn't look for correctness bugs". `/ultrareview` (alias of `/code-review ultra`) runs a "deep, multi-agent code review in a cloud sandbox". — [Commands](https://code.claude.com/docs/en/commands)
- Workflows can "have independent agents adversarially review each other's findings before they're reported". — [Workflows: When to use a workflow](https://code.claude.com/docs/en/workflows#when-to-use-a-workflow)
- Agent teams can enforce gates with `TeammateIdle`/`TaskCompleted` hooks; exit 2 "Prevents the task from being marked as completed". — [Hooks reference](https://code.claude.com/docs/en/hooks#exit-code-2-behavior-per-event); [Agent teams](https://code.claude.com/docs/en/agent-teams)

#### Model-level verification prompts (platform, 5.5 family)
- Sonnet 5.5 at `low` "sometimes reports a change as done without running a check that exercises it". Haiku 5.5 "At `low` and `medium` effort ... sometimes reports a code change as done without running a check." Both guides recommend this system-prompt paragraph (verbatim): "When you change code that can be run, built, or type-checked, run a real check that exercises the change before reporting it done: the project's tests, type-checker, or build, or the changed command itself. A syntax-only check, or a check command that failed to start, does not count; ... Only if no real check can run here, say which one you did not run and why instead of reporting the change as done." — [Prompting Sonnet 5.5: Verification on coding tasks](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5-5#verification-on-coding-tasks); [Prompting Haiku 5.5](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-haiku-5-5)
- Unattended runs, Opus 5.5: "Treat a text-only end of turn as a report rather than as proof the task is done. Keep the task's parts in a checklist the model updates ... have a separate, smaller model check the conversation against it at each end of turn ... stop after two or three automatic continuations on the same task rather than repeating them indefinitely." — [Prompting Opus 5.5: Unattended agentic runs](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5#unattended-agentic-runs)
- Avoiding self-started review loops at high effort, Sonnet 5.5: "When the work the user asked for is done and its checks pass, stop and report. Don't start extra rounds of review or hardening on your own, and don't launch reviewer sub-agents unless the user asked for a review." In testing at `max`, this "cut session cost by about a third, with no change in quality." — [Prompting Sonnet 5.5: Steer initiative and scope](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5-5#steer-initiative-and-scope)

#### Making checks runnable in cloud sessions
- Setup script: "A setup script is a Bash script that runs when a new cloud session starts, before Claude Code launches. Use setup scripts to install dependencies, configure tools". "Scripts run as root on Ubuntu 24.04, so `apt install` and most language package managers work." Requirements: "**Exit zero** ... Append `|| true` to non-critical commands"; "**Finish within five minutes** ... When setup takes longer than that, the environment isn't cached"; "**Network access for installs**". — [Cloud environments: Setup scripts](https://code.claude.com/docs/en/cloud-environments#setup-scripts)
- Caching: the snapshot "keeps what the setup script writes to disk and loses anything that was only running." The cache rebuilds when the script or allowed hosts change, or after "roughly seven days". — [Cloud environments: Environment caching](https://code.claude.com/docs/en/cloud-environments#environment-caching)
- "Use a setup script to provision the VM itself ... Use a SessionStart hook for project setup that should run everywhere, cloud and local, like `npm install`." Scope cloud-only work with `if [ "$CLAUDE_CODE_REMOTE" != "true" ]; then exit 0; fi`. Caveat: "a session with several repositories doesn't load hooks from any repository's `.claude/settings.json`." — [Cloud environments: Setup scripts vs. SessionStart hooks](https://code.claude.com/docs/en/cloud-environments#setup-scripts-vs-sessionstart-hooks)
- VM limits: "4 vCPUs, 16 GB of RAM, 30 GB of disk". Foreground commands wait 2 minutes by default (up to 10). Timed-out commands move to the background for up to 30 more minutes. "For workloads beyond these limits, use Remote Control to run Claude Code on your own hardware". — [Cloud environments: Resource limits and Time limits](https://code.claude.com/docs/en/cloud-environments#resource-limits)
- What carries over: repo `CLAUDE.md`, `.claude/settings.json` hooks (single-repo sessions), `.claude/rules/`, `.claude/skills/`, `.claude/agents/` and `.claude/commands/` all reach cloud sessions. User `~/.claude/*` and repo-declared plugins do not. "To make your own configuration available in cloud sessions, commit it to the repo." — [Cloud environments: What carries over](https://code.claude.com/docs/en/cloud-environments#whats-available-in-cloud-sessions)

### Inferences
- FishGame's existing gate ("full `check` is green" before merge; grep the Unity log for `error CS` and `Exiting batchmode successfully`) maps onto the docs' Stop-hook gate. Two caveats:
  - A batchmode compile or test run is slow. A Stop hook fires on *every* end of turn, so it should be cheap or conditional, e.g. only when `.cs` files changed, and should check `stop_hook_active`.
  - `asyncRewake` background hooks may suit long Unity runs.
- The "Exit 0 alone is not proof — read the log" rule in FishGame's CLAUDE.md is exactly the "show evidence" principle. A hook that greps the log and exits 2 with the error lines would enforce it deterministically and keep the raw log out of context.
- FishGame's Haiku and Sonnet implementer briefs would benefit from the official "run a real check that exercises the change" paragraph, given the documented skip-check behaviour at `low`/`medium`.
- The docs' "flag only gaps that affect correctness or the stated requirements" aligns with FishGame's "don't report client exploits as findings" and "Scale reviews to risk". Put it in reviewer subagent prompts.
- Cloud VMs are Ubuntu x86_64 with 4 vCPU/16 GB/30 GB, and FishGame's Unity tooling is Windows-path based. Cloud sessions likely can't run Unity compile or tests as configured, so they are best for docs, data and pure-C# work, or Remote Control to the Windows machine. This is an inference; the docs don't mention Unity.

### Gaps
- No official example of a test-running PostToolUse or Stop hook for a slow, editor-based toolchain like Unity. The examples are Prettier, eslint and npm.
- The `/goal` page itself was not read, only the references to it on the best-practices and hooks pages.
- No guidance found on how often to run full test suites versus single tests. The only hint is the example CLAUDE.md line "Prefer running single tests, and not the whole test suite, for performance" in [Best practices](https://code.claude.com/docs/en/best-practices#write-an-effective-claude-md).

## 4. What token/cost-saving practices are recommended?

### Takeaway
Cost scales with context size, so the main levers are context hygiene and cheaper settings:
- `/clear` between tasks.
- Focused `/compact` or partial summarize.
- `/btw` for side questions.
- Delegate verbose work to subagents.
- Keep CLAUDE.md short and move procedures to on-demand skills.
- Prefer CLI tools and LSP code intelligence over MCP and grep-and-read.
- Filter output with hooks.
- Pick a cheaper model or lower effort for routine work.
- Plan before big changes.
- Avoid accidental cache misses: model switches, idle past the cache TTL.

Multi-agent features cost multiples: agent teams run about 7x in plan mode, and workflows warn above 25 agents or 1.5M tokens.

### Cited Findings

#### Context management
- "Token costs scale with context size: the more context Claude processes, the more tokens you use. Claude Code automatically optimizes costs through prompt caching ... and auto-compaction". — [Costs: Reduce token usage](https://code.claude.com/docs/en/costs#reduce-token-usage)
- "**Clear between tasks**: Use `/clear` to start fresh when switching to unrelated work. Stale context wastes tokens on every subsequent message. Use `/rename` before clearing so you can find the session later, then `/resume` to return to it." "**Add custom compaction instructions**: `/compact Focus on code samples and API usage`". — [Costs: Manage context proactively](https://code.claude.com/docs/en/costs#reduce-token-usage)
- "If you've corrected Claude more than twice on the same issue in one session, the context is cluttered with failed approaches. Run `/clear` and start fresh with a more specific prompt ... A clean session with a better prompt almost always outperforms a long session with accumulated corrections." — [Best practices: Course-correct early and often](https://code.claude.com/docs/en/best-practices#course-correct-early-and-often)
- Partial compaction: "use `Esc + Esc` or `/rewind`, select a message checkpoint, and choose **Summarize from here** or **Summarize up to here**." `/btw`: "The answer never enters conversation history." — [Best practices: Manage context aggressively](https://code.claude.com/docs/en/best-practices#manage-context-aggressively)
- Compaction itself costs: "`/compact` reads the conversation it summarizes, so compacting a large context is itself a large request. When you want a fresh start instead of continuity, `/clear` costs nothing." — [Costs: Why usage climbs in a long session](https://code.claude.com/docs/en/costs#why-usage-climbs-in-a-long-session)
- What survives compaction:
  - Project-root CLAUDE.md, unscoped rules, auto memory and the plan-mode plan are re-injected from disk.
  - Path-scoped rules and nested CLAUDE.md files reload on demand.
  - Claude Code "re-reads up to five" recent files; any file over 5,000 tokens comes back as a path reference.
  - Invoked skill bodies are capped at "5,000 tokens per skill and 25,000 tokens total; oldest dropped first".

  — [Context window: What survives compaction](https://code.claude.com/docs/en/context-window#what-survives-compaction)
- Auto-compact thresholds: models with a native 1M window, including Fable, Sonnet 5+, Haiku 5.5 and Opus 4.7+, "compact before the window fills, at about 967K tokens by default". "Cloud sessions compact as the conversation approaches the model's limit." `/autocompact 500k` sets the window per model (100K–1M; per-model since v2.1.288, Oct 2 2026). `CLAUDE_CODE_AUTO_COMPACT_WINDOW` suits "scripts and cloud environments". — [Model config: Context window and auto-compaction](https://code.claude.com/docs/en/model-config#default-auto-compact-thresholds)
- Cloud sessions: `/compact` and `/context` work but "`/clear` | No | Start a new session from the sidebar instead". Cloud sessions set `CLAUDE_AUTOCOMPACT_PCT_OVERRIDE` themselves. — [Claude Code on the web: Manage context](https://code.claude.com/docs/en/claude-code-on-the-web)
- Failure patterns:
  - "**The kitchen sink session** ... **Fix**: `/clear` between unrelated tasks."
  - "**The infinite exploration** ... **Fix**: Scope investigations narrowly or use subagents".

  — [Best practices](https://code.claude.com/docs/en/best-practices#avoid-common-failure-patterns)

#### Instructions, skills, MCP, LSP, hooks
- "Your CLAUDE.md file is loaded into context at session start. If it contains detailed instructions for specific workflows (like PR reviews or database migrations), those tokens are present even when you're doing unrelated work. Skills load on-demand only when invoked". — [Costs: Move instructions from CLAUDE.md to skills](https://code.claude.com/docs/en/costs#reduce-token-usage)
- Context cost by feature:
  - CLAUDE.md: "Every request".
  - Skills: "Low (descriptions every request)".
  - MCP: "Low until a tool is used".
  - Subagents: "Isolated from main session".
  - Hooks: "Zero, unless hook returns additional context".

  `disable-model-invocation: true` keeps a skill's description out of context. — [Features overview: Context cost by feature](https://code.claude.com/docs/en/features-overview#understand-context-costs)
- Skill listing budget: "The budget scales at 1% of the model's context window", and each entry's combined `description` + `when_to_use` "is capped at 1,536 characters". `/skill-doctor` "Show[s] what each of your skills costs in context and how often it gets used". A loaded skill body "stays in context across turns, so every line is a recurring token cost." — [Skills](https://code.claude.com/docs/en/skills); [Commands](https://code.claude.com/docs/en/commands)
- Subagent descriptions: a startup warning appears when custom subagent descriptions combined exceed 15,000 tokens. "Trim the `description` fields ... and move detail into each subagent's system prompt, which only loads when that subagent runs." — [Subagents](https://code.claude.com/docs/en/sub-agents)
- MCP: "tool definitions are deferred by default". "**Prefer CLI tools when available**: Tools like `gh`, `aws`, `gcloud`, and `sentry-cli` are still more context-efficient than MCP servers". "**Disable unused servers**". — [Costs: Reduce MCP server overhead](https://code.claude.com/docs/en/costs#reduce-token-usage); [Best practices: Use CLI tools](https://code.claude.com/docs/en/best-practices#use-cli-tools)
- Code intelligence: "A single 'go to definition' call replaces what might otherwise be a grep followed by reading multiple candidate files. Installed language servers also report type errors automatically after edits". — [Costs: Install code intelligence plugins](https://code.claude.com/docs/en/costs#reduce-token-usage)
- Hooks as filters: "Instead of Claude reading a 10,000-line log file to find errors, a hook can grep for `ERROR` and return only matching lines, reducing context from tens of thousands of tokens to hundreds." The page includes a PreToolUse example that rewrites test commands to show only failures. — [Costs: Offload processing to hooks and skills](https://code.claude.com/docs/en/costs#reduce-token-usage)
- A "codebase-overview" skill avoids repeated exploration: "When Claude invokes the skill, it gets this context immediately instead of spending tokens reading multiple files". — [Costs](https://code.claude.com/docs/en/costs#reduce-token-usage)
- "Vague requests like 'improve this codebase' trigger broad scanning. Specific requests ... let Claude work efficiently with minimal file reads." — [Costs: Write specific prompts](https://code.claude.com/docs/en/costs#reduce-token-usage)

#### Model, effort, thinking
- "Thinking tokens are billed as output tokens ... For simpler tasks where deep reasoning isn't needed, you can reduce costs by lowering the effort level". Thinking can't be disabled on the 5.5 or Fable models. — [Costs: Adjust extended thinking](https://code.claude.com/docs/en/costs#reduce-token-usage)
- Delegating verbose operations: "the verbose output stays in the subagent's context while only a summary returns ... The subagent's own requests still draw on your usage. To spend less on them, choose a smaller model for a subagent or run every subagent on one model." — [Costs: Delegate verbose operations to subagents](https://code.claude.com/docs/en/costs#reduce-token-usage)
- Parallel subagents warning: "Running many subagents that each return detailed results can consume significant context, and each subagent spends tokens of its own while it runs." — [Subagents: Common patterns](https://code.claude.com/docs/en/sub-agents#common-patterns)
- Sonnet 5.5 at `xhigh`/`max` may start "its own rounds of review and verification, sometimes with subagents"; a stop-and-report instruction cut session cost "by about a third". — [Prompting Sonnet 5.5](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-sonnet-5-5#steer-initiative-and-scope)

#### Prompt caching (avoid accidental full-price re-reads)
- "Each model has its own cache. Switching with `/model` means the next request reads the entire conversation history with no cache hits." A skill or command whose frontmatter names a different `model` "is also a model switch". — [Prompt caching: Switching models](https://code.claude.com/docs/en/prompt-caching#switching-models)
- "On Opus 5.5, Sonnet 5.5, Haiku 5.5, and Fable 5.1 with an API key or a Claude subscription, changing effort keeps the cache". It doesn't on Bedrock, Agent Platform or a Claude apps gateway, or with `CLAUDE_CODE_DISABLE_EXPERIMENTAL_BETAS` or HIPAA. — [Prompt caching: Changing effort level](https://code.claude.com/docs/en/prompt-caching#changing-effort-level)
- Cache lifetime: "an hour on a subscription and drops to five minutes once you're drawing on usage credits; on an API key or cloud provider, it's five minutes by default". The first message after a longer break "reprocesses your full context". — [Costs: Why usage climbs in a long session](https://code.claude.com/docs/en/costs#why-usage-climbs-in-a-long-session)
- Hidden recurring costs: scheduled tasks, cross-session messages, `/goal` idle check-ins (max 3 per goal), subagents and workflows, and agent teammates each send full context. — [Costs: Why usage climbs](https://code.claude.com/docs/en/costs#why-usage-climbs-in-a-long-session)
- Subagent and workflow-agent cache: these requests "fall outside the main conversation's cache TTL bucket, so its cache holds for five minutes by default, including on a Claude subscription. To keep it for an hour, set `subagentPromptCacheTtl` to `1h`." — [Workflows: Prompt caching in a fan-out](https://code.claude.com/docs/en/workflows#prompt-caching-in-a-fan-out)
- Background usage "typically under $0.04 per session". — [Costs: Background token usage](https://code.claude.com/docs/en/costs#background-token-usage)

#### Planning and multi-agent costs
- "**Use plan mode for complex tasks** ... preventing expensive re-work when the initial direction is wrong." "**Course-correct early**". "**Give verification targets**". — [Costs: Work efficiently on complex tasks](https://code.claude.com/docs/en/costs#reduce-token-usage)
- But "Plan mode is useful, but also adds overhead ... If you could describe the diff in one sentence, skip the plan." — [Best practices: Explore first, then plan, then code](https://code.claude.com/docs/en/best-practices#explore-first-then-plan-then-code)
- Agent teams "use approximately 7x more tokens than standard sessions when teammates run in plan mode". Advice: "Use Sonnet for teammates", "Keep teams small", "Keep spawn prompts focused", "Shut down teammates when their work is done". Teams start disabled (`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1`). — [Costs: Agent team token costs](https://code.claude.com/docs/en/costs#agent-team-token-costs)
- "For routine tasks, a single session is more cost-effective." "Start with 3-5 teammates for most workflows ... Three focused teammates often outperform five scattered ones." — [Agent teams](https://code.claude.com/docs/en/agent-teams)
- Workflows: "a single run can use meaningfully more tokens than working through the same task in conversation." The docs suggest "run the workflow on a small slice first". A `Large workflow` warning appears "When a workflow schedules more than 25 agents, or its projected token total passes 1.5 million"; the warning "is advisory: it doesn't pause or limit the run". The size guideline defaults to `medium` (<10 agents), or `small` (<5) on Pro. — [Workflows: Cost and Set a size guideline](https://code.claude.com/docs/en/workflows#cost)
- Monitoring: `/usage` (aliases `/cost`, `/stats`) shows session cost and plan usage, with a breakdown that "flags behaviors that account for 10% or more of your recent usage". `/context` gives "a live breakdown by category with optimization suggestions". `/insights` gives an HTML report of where things go wrong. — [Commands](https://code.claude.com/docs/en/commands); [Costs](https://code.claude.com/docs/en/costs#why-usage-climbs-in-a-long-session)

### Inferences
- FishGame's token-efficiency rules already follow official guidance: "Navigate C# with the LSP tool", turn off unused plugins and connectors, "Send follow-ups to an existing agent", and keep briefs naming exact files and symbols. The docs reward these as code intelligence, disabling unused servers, and specific prompts.
- Resuming an existing subagent keeps its warm context. On a subscription, though, subagent caches hold only 5 minutes unless `subagentPromptCacheTtl` is `1h`. Follow-ups sent after long gaps pay full price either way.
- Raw Unity batchmode logs are large. A PostToolUse or PreToolUse hook (or a small script) that returns only `error CS`, failed-test lines and the success marker matches the docs' "grep for `ERROR`" pattern, and would cut context sharply.
- FishGame's practice of batching small tasks into one implementer run and one review matches the docs' concern about many parallel subagents each spending their own tokens and returning results into the main context.

### Gaps
- The docs give no per-task dollar or token benchmarks for Claude Code sessions, beyond the "~7x" for agent teams and the workflow warning thresholds. The methodology behind "approximately 7x" isn't explained.
- I didn't read the pricing page, so there are no per-model price figures here. The cost comparisons in these notes are qualitative.
- I found no official guidance on choosing `subagentPromptCacheTtl` for a given cost trade-off; the docs only say "The API bills 1-hour cache writes at a higher rate."

## 5. Objective coverage: how do the docs position subagents, skills, slash commands, plan mode, workflows/agent teams and cloud sessions?

### Takeaway
The docs give one escalation ladder:
- CLAUDE.md for always-true facts.
- Skills for on-demand procedures and knowledge. Custom slash commands are now skills.
- Subagents for isolated, verbose, self-contained side work that returns a summary.
- Agent teams for peers that must talk to each other. Experimental and costly.
- Dynamic workflows for scripted fan-out of dozens or hundreds of agents with cross-checking.
- Hooks for must-always automation.
- Plan mode separates exploring from editing, but only when the change isn't trivial.
- Cloud sessions run from a fresh clone, so the setup must be committed to the repo.

### Cited Findings

#### Subagents
- "Use one when a side task would flood your main conversation with search results, logs, or file contents you won't reference again ... Define a custom subagent when you keep spawning the same kind of worker with the same instructions." — [Subagents](https://code.claude.com/docs/en/sub-agents)
- Use the **main conversation** when "The task needs frequent back-and-forth or iterative refinement", when "Multiple phases share significant context", for "a quick, targeted change", or when "Latency matters". Use **subagents** when "The task produces verbose output", "You want to enforce specific tool restrictions or permissions", or "The work is self-contained and can return a summary". — [Subagents: Choose between subagents and main conversation](https://code.claude.com/docs/en/sub-agents#choose-between-subagents-and-main-conversation)
- What a subagent sees: "It doesn't see your conversation history, the skills you've already invoked, or the files Claude has already read." It loads the CLAUDE.md hierarchy, except built-in Explore/Plan and agents with `omitClaudeMd` (v2.1.271, Sep 14 2026). "If a rule must, such as 'ignore the `vendor/` directory,' restate it in the prompt you give Claude when delegating." — [Subagents: What loads at startup](https://code.claude.com/docs/en/sub-agents#what-loads-at-startup)
- Delegation: "To encourage proactive delegation, include phrases like 'use proactively' in your subagent's description field." — [Subagents: Understand automatic delegation](https://code.claude.com/docs/en/sub-agents)
- Patterns: isolate high-volume operations ("run the test suite and report only the failing tests"), parallel research ("works best when the research paths don't depend on each other"), and chaining. — [Subagents: Common patterns](https://code.claude.com/docs/en/sub-agents#common-patterns)
- Limits:
  - Nesting depth defaults to 3 layers (since v2.1.219, Jul 24 2026; `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH`).
  - 20 concurrent subagents (`CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS`); ultracode sessions are exempt.

  — [Subagents](https://code.claude.com/docs/en/sub-agents#concurrent-subagent-limit)
- Forks (`/subtask`) inherit the full conversation; `context: fork` skills don't. — [Subagents](https://code.claude.com/docs/en/sub-agents); [Skills: Run skills in a subagent](https://code.claude.com/docs/en/skills#run-skills-in-a-subagent)

#### Skills and slash commands
- "Create a skill when you keep pasting the same instructions, checklist, or multi-step procedure into chat, or when a section of CLAUDE.md has grown into a procedure rather than a fact. Unlike CLAUDE.md content, a skill's body loads only when it's used". — [Skills](https://code.claude.com/docs/en/skills)
- "**Custom commands have been merged into skills.** A file at `.claude/commands/deploy.md` and a skill at `.claude/skills/deploy/SKILL.md` both create `/deploy` and work the same way." — [Skills](https://code.claude.com/docs/en/skills)
- Skill types: **reference content**, which runs inline, and **task content**. For task content, "Add `disable-model-invocation: true` to prevent Claude from triggering it automatically", and `context: fork` runs it in its own subagent. "Keep the body itself concise ... State what to do rather than narrating how or why". — [Skills: Types of skill content](https://code.claude.com/docs/en/skills)
- Lifecycle: "Claude Code does not re-read the skill file on later turns, so write guidance that should apply throughout a task as standing instructions rather than one-time steps." After compaction only the first 5,000 tokens of each skill return, so "put the most important instructions near the top of `SKILL.md`." — [Skills: Skill content lifecycle](https://code.claude.com/docs/en/skills#skill-content-lifecycle)
- Useful frontmatter fields: `description` ("Put the key use case first"), `when_to_use`, `disable-model-invocation`, `user-invocable`, `allowed-tools`, `model`, `effort`, `context: fork`, `agent`, `paths`, and `hooks`. — [Skills: Frontmatter reference](https://code.claude.com/docs/en/skills#frontmatter-reference)
- Bundled commands relevant to quality and cost: `/clear`, `/compact`, `/context`, `/btw`, `/rewind`, `/plan`, `/effort`, `/model`, `/usage`, `/memory`, `/init`, `/doctor`, `/code-review`, `/simplify`, `/verify`, `/goal`, `/batch` ("decomposes the work into 5 to 30 independent units ... one background subagent per unit in an isolated worktree"), `/subtask`, `/autocompact`, `/skill-doctor`, `/security-review`, `/ultrareview`. **Outdated:** "`/ultraplan` ... Removed. Use plan mode instead." — [Commands](https://code.claude.com/docs/en/commands)

#### Plan mode
- "Plan mode tells Claude to research and propose changes without making them." Enter it with `Shift+Tab`, `/plan`, or `claude --permission-mode plan`. Approval options include auto mode, manual approval of edits, or "No, keep planning". `Ctrl+G` edits the plan. `showClearContextOnPlanAccept` adds an option that "approves the plan and clears the planning context". — [Permission modes: Analyze before you edit with plan mode](https://code.claude.com/docs/en/permission-modes#analyze-before-you-edit-with-plan-mode)
- The recommended workflow is "Explore → Plan → Implement → Commit". "Planning is most useful when you're uncertain about the approach, when the change modifies multiple files, or when you're unfamiliar with the code being modified." — [Best practices](https://code.claude.com/docs/en/best-practices#explore-first-then-plan-then-code)
- The plan written in plan mode is "Re-injected from disk" after compaction. — [Context window](https://code.claude.com/docs/en/context-window#what-survives-compaction)
- "Let Claude interview you" appears as a best-practices section heading, and the Sep 2026 blog recommends "Give Claude a spec and ask it to interview me about any details I'm missing". — [Best practices](https://code.claude.com/docs/en/best-practices); [Spending your effort](https://claude.dev/blog/spending-your-effort/)

#### Workflows vs teams vs subagents (multi-agent)
- Comparison table: subagents scale to "A few delegated tasks per turn", agent teams to "A handful of long-running peers", and workflows to "Dozens to hundreds of agents per run". Who decides what runs next: "Claude, turn by turn" for subagents, "The lead agent" for teams, and "The script" for workflows. "A workflow script holds the loop, the branching, and the intermediate results itself, so Claude's context holds only the final answer." — [Workflows: When to use a workflow](https://code.claude.com/docs/en/workflows#when-to-use-a-workflow)
- Workflow limits:
  - "Up to 16 concurrent agents by default".
  - "1,000 agents total per run".
  - "No mid-run user input ... For sign-off between stages, run each stage as its own workflow".

  — [Workflows: Behavior and limits](https://code.claude.com/docs/en/workflows#behavior-and-limits)
- Agent teams are best for "Research and review", "New modules or features", "Debugging with competing hypotheses" and "Cross-layer coordination". "For sequential tasks, same-file edits, or work with many dependencies, a single session or subagents are more effective." "Two teammates editing the same file leads to overwrites." "Having 5-6 tasks per teammate keeps everyone productive". — [Agent teams](https://code.claude.com/docs/en/agent-teams)
- Opus 5.5 time budgets for multi-agent leads: add "elapsed 340s / 1200s" style lines; "Teams given a budget kept answer quality comparable to the single agent's while finishing considerably sooner." — [Prompting Opus 5.5: Time signals for multiagent harnesses](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5-5#time-signals-for-multi-agent-harnesses)
- Parallel sessions: worktrees, cross-session messaging, the Desktop app, cloud sessions, agent view (research preview), and agent teams ("experimental and disabled by default"). — [Best practices: Run multiple Claude sessions](https://code.claude.com/docs/en/best-practices#run-multiple-claude-sessions)

#### Cloud sessions and environments
- Each Anthropic-hosted session gets "a fresh virtual machine (VM) running Ubuntu 24.04 on x86_64 ... with your repository cloned and common toolchains pre-installed". A cloud session "counts toward your plan's usage limits alongside the rest of your Claude and Claude Code usage, and there's no separate charge for the cloud VM". — [Cloud environments](https://code.claude.com/docs/en/cloud-environments); [Claude Code on the web](https://code.claude.com/docs/en/claude-code-on-the-web)
- Personal preferences without committing: a setup script can write `~/.claude/CLAUDE.md`. Secrets go in "network secrets", not environment variables, because "Anyone who uses the environment can read its environment variables and setup script." — [Cloud environments](https://code.claude.com/docs/en/cloud-environments#whats-available-in-cloud-sessions)
- Limitations: rate limits are shared ("Running multiple tasks in parallel consumes more rate limits proportionately"), and cloning and PRs require GitHub. — [Claude Code on the web: Limitations](https://code.claude.com/docs/en/claude-code-on-the-web)

### Inferences
- For FishGame, the docs' ladder suggests:
  - Keep `CLAUDE.md` for invariant facts.
  - Move the Unity CLI procedure and the review-tier policy into skills or agent definitions.
  - Use subagents for log-heavy Unity runs and code exploration.
  - Avoid agent teams. Their benefits are peer discussion, but they cost ~7x and risk same-file overwrites in a Unity project with generated scenes and prefabs.
  - Reserve workflows for repo-wide audits.
- Built-in Explore and Plan skip CLAUDE.md, so FishGame rules such as "use LSP first" and "never read `Library/`" won't reach them unless restated in the delegation prompt, or unless a project-level `Explore` override is defined.
- Cloud sessions only see committed `.claude/` config, so FishGame's skills, agents and hooks must live in the repo, not in `~/.claude`, to apply in cloud sessions.

### Gaps
- I didn't read the subagent frontmatter field table in full: `tools`, `disallowedTools`, `permissionMode`, `skills`, `memory`, `isolation: worktree`, `background`.
- The agent view, cross-session messaging, routines and worktree pages were not read.
- No official guidance specific to game engines, Unity, or Windows-only toolchains in cloud sessions was found. The Unity remarks above are inferences.
