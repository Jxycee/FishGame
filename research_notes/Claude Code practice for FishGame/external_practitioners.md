# External (non-Anthropic) practitioner advice on agentic coding with Claude Code, plus Unity / game-dev specifics

_Researched 2026-10-17. Quotes were pulled from the fetched pages by a fetch tool; anything marked "(search summary)" came only from a search-result summary and was not read in full. Spot-check any quote before reusing it word for word. "Anthropic docs" means the current Claude Code best-practices page, which is undated and was read on 2026-10-17. It is included only for comparison._

## 1. CLAUDE.md: size, instruction limits, progressive disclosure, what to leave out

### Takeaway
No one has run a rigorous test of line limits. The practitioner consensus is "short, universal, and grown from observed mistakes." HumanLayer's figures are "< 300 lines is best, and shorter is even better" and their own root file is "less than sixty lines". The more useful measure is the instruction budget: about 150–200 instructions that a frontier model follows reliably, of which the Claude Code harness already uses about 50. Knowledge that only some tasks need goes in separate files that are loaded on demand (agent_docs/, skills, specs/, CLI READMEs). Deterministic rules go to linters and hooks, not prose. A peer-reviewed 2026 study found that context files give no general improvement in success and cost more than 20% extra, which strengthens the case for keeping the file minimal.

### Cited Findings
**Size and instruction budget**
- HumanLayer (Kyle, 25 Nov 2025): "general consensus is that < 300 lines is best, and shorter is even better." "our root `CLAUDE.md` file is _less than sixty lines_." — [HumanLayer: Writing a good CLAUDE.md](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- Same post: "Frontier thinking LLMs can follow ~ 150-200 instructions with reasonable consistency." "Claude Code's system prompt contains ~50 individual instructions." "Smaller models get MUCH worse, MUCH more quickly." "As instruction count increases, instruction-following quality decreases uniformly." "LLMs bias towards instructions that are on the peripheries of the prompt." CLAUDE.md "should contain as few instructions as possible." The post cites arXiv 2507.11538 and admits "the topic hasn't been investigated in an incredibly rigorous manner." — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- Same post: "Anthropic does not have an official recommendation on how long your `CLAUDE.md` file should be." — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- The HN thread "Writing a good Claude.md" had 748 points and 290 comments (search summary of an HN submissions page; the comments were not retrieved). — [HN submissions page](https://news.ycombinator.com/submitted?id=objcts)
- Mario Zechner (author of the pi agent, 30 Nov 2025): pi's "system prompt and tool definitions together come in below 1000 tokens". "There does not appear to be a need for 10,000 tokens of system prompt." MCP tool descriptions can take "7-9% of your context window" before any work starts. — [Mario Zechner: minimal coding agent](https://mariozechner.at/posts/2025-11-30-pi-coding-agent/)
- Armin Ronacher (31 Jan 2026): pi "has the shortest system prompt of any agent that I'm aware of and it only has four tools: Read, Write, Edit, Bash." A to-do tool is "the only additional tool that I'm loading into my context." "I throw skills away if I don't need them." — [Armin Ronacher: Pi](https://lucumr.pocoo.org/2026/1/31/pi/)
- Geoffrey Huntley (14 Jul 2025) treats the context as a stack: "deterministically allocate the stack the same way every loop". "The items that you want to allocate to the stack every loop are your plan ("@fix_plan.md") and your specifications." "you're effectively burning the allocation of the specifications every loop." "@AGENT.md is the heart of the loop. It instructs how Ralph should compile and run the project." — [Huntley: Ralph](https://ghuntley.com/ralph/)

**Progressive disclosure**
- HumanLayer: keep "task-specific instructions in _separate markdown files_ with self-descriptive names", for example an `agent_docs/` folder holding `building_the_project.md` and `running_tests.md`. "include a list of these files with a brief description of each." "**Prefer pointers to copies**. Don't include code snippets in these files if possible." "include `file:line` references to point Claude to the authoritative context." Optionally, "ask Claude to present you with the files it wants to read for aproval [sic] first." — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- Zechner: drop MCP and give the agent CLI tools whose READMEs it reads on demand. Use a checkbox TODO.md instead of a built-in to-do tool, because "to-do lists generally confuse models more than they help". Use PLAN.md instead of plan mode, because a file "can be shared across sessions, and can be versioned with your code." — [Zechner](https://mariozechner.at/posts/2025-11-30-pi-coding-agent/)
- Ronacher: MCP tools "need to be loaded into the system context" at session start, so they are hard to reload "without trashing the complete cache". He "fully replaced all my CLIs or MCPs" for browser work "with a skill that just uses CDP." — [Ronacher: Pi](https://lucumr.pocoo.org/2026/1/31/pi/)
- Simon Willison (30 Sep 2025): "I like to create an AGENTS.md (or equivalent) file with details of packages I think they may need to use." — [Willison: Designing agentic loops](https://simonwillison.net/2025/Sep/30/designing-agentic-loops/)
- Anthropic docs, for comparison: "For domain knowledge or workflows that are only relevant sometimes, use skills instead. Claude loads them on demand without bloating every conversation." The docs also support `@path` imports. — [Claude Code best practices](https://code.claude.com/docs/en/best-practices)

**What to leave out**
- HumanLayer: "Don't try to stuff every command Claude could possibly need to run." "Never send an LLM to do a linter's job." "Code style guidelines will inevitably add a bunch of instructions and mostly-irrelevant code snippets." Avoid task-specific content such as "how to structure a new database schema." "Don't make Claude find the formatting issues itself." "Don't use `/init` or auto-generate your `CLAUDE.md`." — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- HumanLayer quotes the wrapper that Claude Code puts around CLAUDE.md: "IMPORTANT: this context may or may not be relevant to your tasks. You should not respond to this context unless it is highly relevant to your task." They argue that content which is not universal makes it more likely Claude ignores the file. They label their guess about why the wrapper exists as speculation ("It's hard to say for sure"). — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- ETH Zurich (Gloaguen, Mündler, Müller, Raychev, Vechev; arXiv 2602.11988; v1 12 Feb 2026, v3 29 Sep 2026; listed at ICLR 2026): "providing context files does not generally improve task success rates" "while increasing inference cost by over 20% on average". The result holds "for both LLM-generated and developer-committed context files". "instructions in the context files are well followed by coding agents." "repository overviews, although popular and recommended by model providers, are not helpful." "context files are useful for specifying non-standard coding practices." — [arXiv 2602.11988](https://arxiv.org/abs/2602.11988)
- Secondary summaries of the same paper (not checked against the full text): LLM-generated files cut success by about 3% on average, developer-written files raise it by about 4%, and cost rises by up to about 19%. Tasks were mostly niche Python repositories. — [Upsun summary](https://developer.upsun.com/posts/ai/agents-md-less-is-more); [notesbylex](https://notesbylex.com/self-generated-agent-context-files-dont-help-either)

**How practitioners actually grow the file**
- Mitchell Hashimoto (5 Feb 2026), on AGENTS.md: "Each line in that file is based on a bad agent behavior, and it almost completely resolved them all." He pairs the file with "Actual, programmed tools. For example, scripts to take screenshots, run filtered tests, etc etc." — [Hashimoto: My AI adoption journey](https://mitchellh.com/writing/my-ai-adoption-journey)
- Ronacher (12 Jun 2025) uses CLAUDE.md for facts about his environment. His example: the agent "knows that emails are being logged thanks to a CLAUDE.md instruction". On 30 Jul 2025: "I can just tell it "fix lint" in the CLAUDE.md file without needing a slash command." — [Ronacher: Agentic coding recommendations](https://lucumr.pocoo.org/2025/6/12/agentic-coding/); [Ronacher: things that didn't work](https://lucumr.pocoo.org/2025/7/30/things-that-didnt-work/)
- Kent Beck (25 Jun 2025) wrote a system prompt made entirely of process rules: "Always follow the instructions in plan.md." "Always follow the TDD cycle: Red → Green → Refactor." "Never mix structural and behavioral changes in the same commit." "Always write one test at a time, make it run, then improve structure." — [Beck: Augmented coding: beyond the vibes](https://newsletter.kentbeck.com/p/augmented-coding-beyond-the-vibes)

**Anthropic's current position, for comparison**
- "Run `/init` to generate a starter CLAUDE.md file … then refine over time." "Keep it concise. For each line, ask: *"Would removing this cause Claude to make mistakes?"* If not, cut it. Bloated CLAUDE.md files cause Claude to ignore your actual instructions!" Include: "Bash commands Claude can't guess", "Code style rules that differ from defaults", "Testing instructions and preferred test runners", "Repository etiquette", "Architectural decisions specific to your project", "Developer environment quirks", "Common gotchas". Exclude: "Anything Claude can figure out by reading code", "Detailed API documentation (link to docs instead)", "Information that changes frequently", "Long explanations or tutorials", "File-by-file descriptions of the codebase", "Self-evident practices". "If Claude keeps skipping one instruction, add emphasis such as "IMPORTANT" to that line alone. If you emphasize many lines, none of them stands out." It also names a failure pattern, "The over-specified CLAUDE.md", with the fix "Ruthlessly prune … delete it or convert it to a hook." — [Claude Code best practices](https://code.claude.com/docs/en/best-practices)

### Inferences
- The numbers in circulation are: under 300 lines (HumanLayer's "consensus"), under 60 lines (HumanLayer's own file), and about 150–200 instructions in total with roughly 50 already taken by the harness. Counting instructions and whether each one applies to every task is more meaningful than counting lines.
- Everyone, including Anthropic's current docs, now agrees on the main points: keep the file short and universal, add lines only after a real mistake, move situational knowledge into files loaded on demand, and enforce deterministic rules with tools or hooks rather than prose. The remaining disagreements are narrower: whether to use `/init`, and whether style rules belong in the file. See section 5.
- **FishGame:** CLAUDE.md is 50 lines (counted with `wc -l`), well inside both limits. Many bullets, though, pack several rules plus dated provenance such as "(user, 2026-10-14)", so its instruction count is high for its length (not counted precisely). By the HumanLayer and ETH reasoning, these are candidates to move into on-demand docs or skills:
  - asset-licence rules and art direction, needed only for art tasks;
  - model and agent-role tiering, needed only by the controller;
  - provenance dates.

  These should stay: the Unity CLI and lockfile procedure, the authority-comment rule and assembly layout. They are "non-standard practices" and "commands Claude can't guess", the kind of content the ETH paper and Anthropic say context files are good for.

### Gaps
- No controlled study measures CLAUDE.md line limits. HumanLayer says its own figures are not rigorous. I did not read arXiv 2507.11538, the paper HumanLayer cites for its instruction-count figure.
- The ETH tasks were mostly Python (per a secondary summary). Whether the result carries over to Unity/C# monorepos is unknown.
- I could not retrieve the HN discussion itself. Claims elsewhere of "<200 lines" (tembo.io, tryuncle.com) come from low-credibility SEO pages and are not used here.
- I did not verify whether Claude Code still adds the "may or may not be relevant" wrapper as of Oct 2026.

## 2. Subagent swarms vs a single agent, cost blowups, critic loops

### Takeaway
The trusted practitioners agree on one writer at a time. Subagents are worth using for **context control**: read-only search and summary, and soaking up noisy tool output such as test runs. They are not for role-play personas or for several agents writing interdependent code in parallel. The binding limit is how much a human can review. Token use multiplies about 4× for an agent and about 15× for a multi-agent system, compared with chat. Critic or reviewer loops help when the reviewer sees only the diff in a fresh context, or is a different model, and is limited to correctness. "Reviewer slop" and over-engineering from chasing every finding are recognised failure modes. Opinion softened in 2026: Cognition and swyx now see some multi-agent setups working, but still call parallel swarms unsolved.

### Cited Findings
**Single agent vs multi-agent**
- Cognition (Walden Yan, 12 Jun 2025) gives two principles: "Share context, and share full agent traces, not just individual messages" and "Actions carry implicit decisions, and conflicting decisions carry bad results". "in 2025, running multiple agents in collaboration only results in fragile systems." In the Flappy Bird example, a subagent "started building a background that looks like Super Mario Bros." On Claude Code: "it never does work in parallel with the subtask agent", "the subtask agent is usually only tasked with answering a question, not writing any code", and "The designers of Claude Code took a purposefully simple approach." Recommendation: "just use a single-threaded linear agent." — [Cognition: Don't build multi-agents](https://cognition.com/blog/dont-build-multi-agents)
- Cognition's update, quoted on Latent Space (episode of 28 May 2026; Yan's X post of 22 Apr 2026): "A year ago, I'd tell people to not build multi-agents and to focus on context engineering fundamentals." "Today, many sexy ideas are still impractical, but we've found some setups that actually work." In the same episode Cole Murray said read-only research subagents have "really nice context management benefits", that agents should be isolated, "each with their own boxes Not sharing machines", and that subagent tokens "get collapsed back to just the answer". — [Latent Space: The Age of Async Agents](https://www.latent.space/p/cognition)
- swyx, same episode: "One thing I'm very bullish on this year is agents calling other agents or spawning sub-agents." On parallel swarms: "I don't think anyone's, really solved it." "you just have more reviewer slop on top of the agent slop." "I don't feel that confident about it yet." — [Latent Space](https://www.latent.space/p/cognition)
- Dex Horthy / HumanLayer, ACE-FCA (from a YC talk on 20 Aug 2025): "Subagents are about context control." "Subagents are not about playing house and anthropomorphizing roles." Use them "to let you use a fresh context window to do finding/searching/summarizing." — [ACE-FCA](https://github.com/humanlayer/advanced-context-engineering-for-coding-agents/blob/main/ace-fca.md)
- Horthy, "No Vibes Allowed" talk at AI Engineer Code 2025 (the page shows no date): "Sub-agents are not for anthropomorphizing roles, they are for controlling context." — [AI Engineer talk page](https://ai.engineer/talks/context-engineering-for-complex-codebases)
- 12-Factor Agents, Factor 10 "Small, Focused Agents" (Horthy): "3-10, maybe 20 steps max." "As context grows, LLMs are more likely to get lost or lose focus." Agents are "one building block in a larger, mostly deterministic system." — [12-factor Factor 10](https://github.com/humanlayer/12-factor-agents/blob/main/content/factor-10-small-focused-agents.md); [12-factor-agents](https://github.com/humanlayer/12-factor-agents)
- Ronacher (30 Jul 2025): "Sub-tasks and sub-agents enable parallelism, but you must be careful. Tasks that don't parallelize well — especially those mixing reads and writes — create chaos." Earlier (12 Jun 2025): "parallelization boosts overall efficiency." — [Ronacher: didn't work](https://lucumr.pocoo.org/2025/7/30/things-that-didnt-work/); [Ronacher: recommendations](https://lucumr.pocoo.org/2025/6/12/agentic-coding/)
- Zechner (30 Nov 2025): Claude Code subagents are "a black box within a black box". Instead he has the agent run pi through bash so the output stays visible. — [Zechner](https://mariozechner.at/posts/2025-11-30-pi-coding-agent/)
- Willison, "Subagents" chapter of Agentic Engineering Patterns (guide launched 23 Feb 2026; chapter undated): "While it can be tempting to go overboard breaking up tasks across dozens of different specialist subagents…" the main value is "preserving that valuable root context and managing token-heavy operations." "Your root coding agent is perfectly capable of debugging or reviewing its own output." A test-runner subagent "can hide the full test output from the main coding agent." Parallel subagents give "a significant speed boost" only where files are "not dependent on each other". Cheaper models such as Haiku can be used. — [Willison: Subagents](https://simonwillison.net/guides/agentic-engineering-patterns/subagents/)
- Huntley (14 Jul 2025): "You may use up to 500 parrallel [sic] subagents for all operations but only 1 subagent for build/tests of rust." Fanning builds and tests out to "a couple of hundred subagents" causes "bad form back pressure". The primary context acts as a scheduler. On 17 Jan 2026: Ralph is "a single process that performs one task per loop", and on multi-agent: "At this stage, it's not needed." — [Huntley: Ralph](https://ghuntley.com/ralph/); [Huntley: everything is a ralph loop](https://ghuntley.com/loop/)
- Hashimoto (5 Feb 2026) runs background agents "one at a time, not in parallel". He says he is not running several agents at once and does not currently want to (paraphrase). He finds "a background agent running 10 to 20% of a normal working day" effective, and he will "block out the last 30 minutes of every day to kick off one or more agents." — [Hashimoto](https://mitchellh.com/writing/my-ai-adoption-journey)
- Willison (5 Oct 2025) runs several agents in different directories and checkouts, without worktrees yet: "the natural bottleneck on all of this is how fast I can review the results." Good parallel tasks are research, small maintenance, and "directed" work: "If I tell them exactly how to build something the work needed to review the resulting changes is a whole lot less taxing." Also: "Reviewing code that lands on your desk out of nowhere is a lot of work." — [Willison: Parallel coding agent lifestyle](https://simonwillison.net/2025/Oct/5/parallel-coding-agents/)
- Thorsten Ball (Amp, 15 Apr 2025) on what an agent is: "It's an LLM, a loop, and enough tokens." It takes "less than 400 lines of code, most of which is boilerplate." — [Ampcode: How to build an agent](https://ampcode.com/how-to-build-an-agent)

**Context-window thresholds behind the subagent advice**
- Horthy: "Around the forty percent line is where you're going to start to see some diminishing returns depending on your task" (the "dumb zone"; he treats it as a rough guide that depends on task and model). — [AI Engineer talk](https://ai.engineer/talks/context-engineering-for-complex-codebases). ACE-FCA aims to keep "utilization in the 40%-60% range." — [ACE-FCA](https://github.com/humanlayer/advanced-context-engineering-for-coding-agents/blob/main/ace-fca.md)
- Huntley: "you only have approximately 170k of context window to work with." "The more you use the context window, the worse the outcomes you'll get." — [Ralph](https://ghuntley.com/ralph/). On 7 Apr 2025, for Claude 3.7's 200k window, he wrote that "the quality of output clips at the 147k-152k mark." — [Huntley: redlining](https://ghuntley.com/redlining/)
- Amp (23 Oct 2025) removed compaction because "It's lossy" and "encourages long, meandering threads". It replaced it with Handoff, which starts a new focused thread from an extracted prompt and file list: "What we want to encourage are focused threads." — [Amp: Handoff](https://ampcode.com/news/handoff)

**Cost**
- Anthropic (13 Jun 2025): "agents typically use about 4× more tokens than chat interactions"; "multi-agent systems use about 15× more tokens than chats". Token usage "explains 80% of the variance" on BrowseComp. "most coding tasks involve fewer truly parallelizable tasks than research." Multi-agent is justified only when "the value of the task is high enough to pay for the increased performance." — [Anthropic: multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system)
- HumanLayer: "our team of three is averaging about $12k on opus per month." — [ACE-FCA](https://github.com/humanlayer/advanced-context-engineering-for-coding-agents/blob/main/ace-fca.md)
- Cole Murray (28 May 2026): spend he commonly hears is "anywhere from 1,000 an engineer up to 5,000 an engineer". The period, probably monthly, is not stated in the excerpt. — [Latent Space](https://www.latent.space/p/cognition)
- At AI Engineer World's Fair (Latent Space dispatch, 3 Jul 2026), Greg Pstrucha said you can't "orchestrate your problems away by buying more tokens". An Amplify survey found 40% of respondents say AI costs regularly limit their ambition, and 36% say they sometimes do. — [Latent Space: AIEWF loops debate](https://www.latent.space/p/aiewf-daily-dispatch-locomotives)
- Ronacher (Jun 2025): "I exclusively use the cheaper Sonnet model." — [Ronacher](https://lucumr.pocoo.org/2025/6/12/agentic-coding/). Willison: "If a credential can spend money, set a tight budget limit." — [Willison: loops](https://simonwillison.net/2025/Sep/30/designing-agentic-loops/)
- Huntley's anecdote: a "$50k USD contract" MVP delivered for "$297 USD" of agent cost using Amp (a single example, not a benchmark). — [Ralph](https://ghuntley.com/ralph/)
- Latent Space news roundup on Anthropic Dynamic Workflows: "a benchmark finding suggested multi-agent setups may not improve final quality but can reach mediocre solutions faster" (search summary only). — [Latent Space AINews](https://www.latent.space/p/ainews-anthropic-raises-965b-series)

**Critic and reviewer loops**
- Anthropic docs recommend an adversarial review subagent, with a caveat: "A reviewer prompted to find gaps will usually report some, even when the work is sound … Chasing every finding leads to over-engineering: extra abstraction layers, defensive code, and tests for cases that can't happen. Tell the reviewer to flag only gaps that affect correctness or the stated requirements." — [Claude Code best practices](https://code.claude.com/docs/en/best-practices)
- Amp's Oracle (4 Jul 2025): "a read-only subagent powered by one of the most powerful models today: OpenAI's o3". It is "impressively good at reviewing, at debugging, at analyzing", but "slightly more expensive and less suited for day-to-day agentic coding". Example prompt: "Use the oracle to review the last commit's changes." Amp's current docs pair different frontier models so that one reviews the other (search summary). — [Amp: Oracle](https://ampcode.com/news/oracle)
- Kent Beck's warning signs: "Loops." "Functionality I hadn't asked for (even if it was a reasonable next step)." "Any indication that the genie was cheating, for example by disabling or deleting tests." Also: "I watched the intermediate results of the genie more carefully, ready to intervene & stop unproductive development." — [Beck](https://newsletter.kentbeck.com/p/augmented-coding-beyond-the-vibes)
- Cole Murray: "having the AI reviewer integrated into the system is a critical part of this background system"; "your code base regresses to your worst engineer", which calls for "scheduled cleanup … looking for duplication". — [Latent Space](https://www.latent.space/p/cognition)
- Horthy (Aug 2025): "When you review the research and the plans, you get more leverage than you do when you review the code." "I can't read 2000 lines of golang daily. But I can read 200 lines of a well-written implementation plan." In the later talk he says all code is still reviewed, and "do not outsource the thinking". — [ACE-FCA](https://github.com/humanlayer/advanced-context-engineering-for-coding-agents/blob/main/ace-fca.md); [AI Engineer talk](https://ai.engineer/talks/context-engineering-for-complex-codebases)
- Mike Krieger (Anthropic Head of Labs) at AIEWF 2026: his team is "bottlenecked on reviews". — [Latent Space AIEWF dispatch](https://www.latent.space/p/aiewf-daily-dispatch-locomotives)

### Inferences
- The shared rule is to give subagents read-only exploration and noisy tool runs, keep a single writer per area of code, and keep the parent context under about 40–60%. Parallelism pays only for independent work, where nobody reads or writes the same thing.
- **FishGame:** the "Agent roles" setup is a role-based multi-model pipeline: an Opus author per plan section, a Sonnet implementer, Opus or Sonnet reviewers by tier, Haiku scans, and Fable as escalation fixer.
  - **Supported by practitioners:** fresh-context review, cheap models for mechanical scans, and work isolated per worktree.
  - **Practitioners would push on:**
    1. Every extra agent multiplies tokens (4× for an agent, about 15× for multi-agent).
    2. Reviewers always find something, so review prompts should be limited to correctness and the stated requirements.
    3. Personas such as "Opus reviewer" or "Sonnet implementer" matter less than giving each agent exactly the context it needs.
  - FishGame's own rules already lean this way: "Scale reviews to risk" (2026-10-13), "≤30-line fix gets the controller's own diff read", and "Batch small tasks into one implementer run and one review".
- The PlayMode UDP-port clash between worktrees is a concrete case of Ronacher's "mixing reads and writes create chaos". Shared mutable resources (ports, the Unity project lock) force runs to happen one after another.

### Gaps
- I found no first-person statement from Thorsten Ball on subagents or CLAUDE.md. The Amp Handoff and Oracle pieces are company posts, and the search summary credits Handoff to other Amp staff.
- No rigorous cost comparison of subagent fan-out for coding exists. Only Anthropic's research-agent multipliers and anecdotes were found.
- The Dynamic Workflows "mediocre solutions faster" benchmark claim is unverified (search summary only).
- Cognition's 2026 "setups that actually work" are not described in the sources fetched.
- I did not retrieve swyx's own written essays on this topic, only podcast quotes.

## 3. Verification-first habits: TDD with agents, CI as the loop

### Takeaway
This is the strongest consensus of all the topics. The agent needs a fast, deterministic, compact pass/fail signal. Write tests first and confirm they fail ("red/green"). Make tools fail loudly and log to files. Hand style and formatting to linters. Let tests, build or lint be the exit condition of the loop, and have humans review plans and evidence. For games, passing compiles and unit tests does not prove the game is playable. Agents test with "debug" shortcuts such as teleporting the player, and they misjudge visuals.

### Cited Findings
- Willison (30 Sep 2025): an agent is "something that runs tools in a loop to achieve a goal". The fit is best "If you can reduce your problem to a clear goal and a set of tools that can iterate towards that goal". The approach "is massively amplified by a good, cleanly passing test suite." "If your test suite is solid an agentic loop can upgrade them all for you." He quotes Anthropic's advice to "use `--dangerously-skip-permissions` in a container without internet access." — [Willison: Designing agentic loops](https://simonwillison.net/2025/Sep/30/designing-agentic-loops/)
- Willison, "Red/green TDD": "This turns out to be a _fantastic_ fit for coding agents." "A significant risk with coding agents is that they might write code that doesn't work." His prompt shorthand is "Use red/green TDD". "It's important to confirm that the tests fail before implementing the code to make them pass." "If you skip that step you risk building a test that passes already." — [Willison: Red/green TDD](https://simonwillison.net/guides/agentic-engineering-patterns/red-green-tdd/)
- Willison, "First run the tests": "Automated tests are no longer optional when working with coding agents." Opening with "First run the tests" works because "It tells the agent that there is a test suite and forces it to figure out how to run the tests". The test count signals project size. "It puts the agent in a testing mindset." — [Willison: First run the tests](https://simonwillison.net/guides/agentic-engineering-patterns/first-run-the-tests/). The guide also has an "Agentic manual testing" chapter (browser automation, "Have them take notes with Showboat"); only its headings were seen. — [Guide index](https://simonwillison.net/guides/agentic-engineering-patterns/)
- Beck (25 Jun 2025): "In augmented coding you care about the code, its complexity, the tests, & their coverage." "In vibe coding you don't care about the code, just the behavior of the system." "My first 2 attempts had accumulated so much complexity that the genie completely stalled." His system prompt enforces Red → Green → Refactor and Tidy First (quoted in section 1). He used Augment's Remote Agent; Augment has been a sponsor of his newsletter. — [Beck](https://newsletter.kentbeck.com/p/augmented-coding-beyond-the-vibes)
- Hashimoto (5 Feb 2026), "harness engineering": build "scripts to take screenshots, run filtered tests". "I'm making an earnest effort for agents to be able to verify they're doing a Good Thing." — [Hashimoto](https://mitchellh.com/writing/my-ai-adoption-journey)
- Ronacher (12 Jun 2025): put "critical tools into a Makefile". "I'm also always logging it to a file." "Tools must clearly inform agents of misuse or errors to ensure forward progress." "Crashes are tolerable; hangs are problematic." "Have the agent do "the dumbest possible thing that will work"." On 30 Jul 2025: "Anything we can fully automate, we should." — [Ronacher: recommendations](https://lucumr.pocoo.org/2025/6/12/agentic-coding/); [Ronacher: didn't work](https://lucumr.pocoo.org/2025/7/30/things-that-didnt-work/)
- Huntley: "After implementing functionality or resolving problems, run the tests for that unit of code that was improved." Use only one subagent for build and test, for back-pressure reasons. Ralph can leave "a broken codebase that doesn't compile from time to time." "There is no way this is possible without senior expertise guiding Ralph." "There's no way in heck would I use Ralph in an existing code base." — [Ralph](https://ghuntley.com/ralph/). On 17 Jan 2026: "When you see a failure domain – put on your engineering hat and resolve the problem so it never happens again." — [Huntley: loop](https://ghuntley.com/loop/)
- HumanLayer: "Never send an LLM to do a linter's job." Use formatters and hooks instead. — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md). ACE-FCA: "compact the current status back into the original plan file after each implementation phase is verified", and "You have to engage with your task when you're doing this or it WILL NOT WORK." — [ACE-FCA](https://github.com/humanlayer/advanced-context-engineering-for-coding-agents/blob/main/ace-fca.md). Factor 9 of 12-factor is "Compact Errors into Context Window". — [12-factor-agents](https://github.com/humanlayer/12-factor-agents)
- Ian Livingstone, in the AIEWF 2026 "loops" debate, named verifiability as the core issue: "A loop is at the core of 'I try something, I learn something, I apply something.'" On the same panel Horthy said "the hype is outrunning the discipline." — [Latent Space AIEWF dispatch](https://www.latent.space/p/aiewf-daily-dispatch-locomotives)
- Anthropic docs, for comparison: "Give Claude a check it can run: tests, a build, a screenshot to compare." "Claude stops when the work looks done." The docs list Stop hooks as a "deterministic gate" and `/goal` as a session-level gate. "Have Claude show evidence rather than asserting success." "If you can't verify it, don't ship it." — [Claude Code best practices](https://code.claude.com/docs/en/best-practices)
- **Game-specific evidence (Classmethod, 13 Mar 2026; Claude Code with Opus 4.6, Unity 6000.3.10f1, com.unity.ai.assistant 2.1.0-pre.1 MCP):**
  - The agent "ran 7 tests on its own and fixed 5 issues independently", but it "warped the player through MCP to check the state of each room". In the author's words, "it wasn't testing whether the game was playable with keyboard controls."
  - Bugs that slipped through: "I couldn't exit the starting room", trap rooms where a player without double jump is stuck, and an HP bar that never changed.
  - Conclusions: "AI can create correct blueprints, but verifying playability remains a human task." "AI doesn't spontaneously perform even level 2 checks."
  - Fixes the author suggests: write explicit checks into the prompt, add numeric checks in code (for example exit height against jump ability), and "Run the game with a self-driving player to comprehensively verify reachability". The author notes the last one is costly to build.

  — [Classmethod: 2D verification](https://dev.classmethod.jp/en/articles/unity-mcp-claude-code-2d-game-verification/)
- Classmethod (8 Mar 2026), TPS template: the agent judged its own lighting as "almost complete darkness with critical breakdown", while in Play mode the human found "Walls and targets were visible, and operation wasn't hindered." "A human would naturally move the camera inside the room to check, but this verification step wasn't selected." The author recommends "AI working with human review" over "AI working independently". — [Classmethod: TPS](https://dev.classmethod.jp/en/articles/unity-mcp-tps-game-claude-code-modification/)

### Inferences
- **FishGame:** pure-C# Core with EditMode tests is the ideal shape for an agent loop: fast, deterministic and headless.
  - Prompt implementers with "use red/green TDD", and require them to show the failing test output before the fix. This is Willison's rule and Anthropic's "evidence" rule.
- Unity batch runs are slow and noisy. A wrapper that prints only `error CS` lines, failed test names and the result summary follows Ronacher's "fail loudly", Factor 9's "compact errors" and Willison's test-runner-subagent pattern. `Tools/unity.ps1` and the `check` command may already do part of this.
- For netcode and gameplay, compile plus EditMode results are not enough evidence. The external advice points to scripted PlayMode bots: a host plus a client running a "self-driving player" for the fishing loop, with numeric assertions such as reachability, tension limits and quota math. Human playtests should remain the gate for feel and for "realistic toon" look-dev, because agent self-assessment of visuals was unreliable.
- Beck's "cheating" warning applies directly: reviewers should look for tests that were deleted, skipped or weakened (`[Ignore]`, loosened asserts).

### Gaps
- None of these sources gives quantitative data comparing agent outcomes with and without TDD.
- None covers CI-hosted Unity runs (licensing, GPU, runners) as the agent's loop.
- I did not read the full text of Willison's "Agentic manual testing" chapter.
- Beck's model is not named in the post.

## 4. Unity-specific pitfalls for agents: compile loops, editor lock, PlayMode tests, scene YAML

### Takeaway
Credible Unity-specific material is thin and recent: Unity's own Sept 2026 plugin and docs, two Classmethod field reports, the Unity Discussions forums, and one Cursor incident.
- **Tooling:** Unity's official route is now **skills plus the Unity CLI**. The in-Editor AI Assistant MCP server is deprecated.
- **Scenes and prefabs:** the safest pattern is to generate them with editor scripts or a serializer-level bridge, never by editing raw .unity or .prefab YAML.
- **Locks:** the project lock (Temp lockfile) serializes everything, both batch runs and live-Editor control.
- **Domain reloads:** a reload can break live connections, so the agent must wait for compilation to settle.
- **Exit codes:** Unity test exit codes are not standardized, so the agent must read the logs and results XML.
- **Playability:** agents verify through debug shortcuts and misjudge visuals, as shown in section 3.

### Cited Findings
**State of official tooling (Sept–Oct 2026)**
- Unity blog (Rachel Zhao, Director of AI Products, 9 Sep 2026): the plugin "installs 29 skills (we'll add more over time) with a single command". The `/unity-cli` skill "Drives Unity from the terminal, including a live Editor": it creates and modifies GameObjects, edits scenes and assets, inspects the hierarchy and runs C#. "Claude Code is the first coding agent you can install it into." — [Unity: plugin for Claude Code](https://unity.com/blog/unity-plugin-for-claude-code)
- Unity docs: "The plugin only ships skills and doesn't use a Model Context Protocol (MCP) server."
  - **Requirements:** the Unity CLI is installed, the Pipeline package is in the project (`unity pipeline install`), and the "Unity Editor is open".
  - **Limitations:** "For simple uGUI work, the plugin makes little difference compared to doing it manually." "On Sonnet 5, the plugin mainly supplies correctness. Sonnet 5 builds most Unity features without the plugin, but defaults to older approaches."

  — [Unity docs: plugin for Claude Code](https://docs.unity.com/en-us/ai/unity-plugin/claude-code)
- AI Assistant package docs (v2.18): "Unity MCP server is deprecated. Use the Unity command-line interface (CLI) instead." "By default, the relay connects to the first Unity Editor instance it discovers." Target a specific Editor by project path or PID. External clients need manual approval the first time. — [Unity MCP get started (2.18)](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.18/manual/integration/unity-mcp-get-started.html)
- Unity CLI release notes (7 Oct 2026): `unity mcp configure claude-code` now checks whether an enabled Claude Code plugin already provides an MCP server (search summary). — [Unity CLI release notes](https://docs.unity.com/en-us/unity-cli/release-notes)
- MCP for Unity (CoplayDev) is the third-party bridge whose `mcpforunity://` resources appear in this environment. It is MIT-licensed, "Proudly sponsored and maintained by Aura", and "not affiliated with Unity Technologies". Its README documents multi-instance routing. — [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
- The Gardenee blog (20 Sep 2026) is secondary, has no named author and is mostly second-hand, so these claims are unverified:
  - "What got deprecated is the MCP server bundled with the AI Assistant package"; "MCP itself lives on as `unity mcp` inside the new Unity CLI, and both are free."
  - Gotcha: "Every MCP call returns 401 after entering Play Mode", because "The domain reload regenerates the token, but the client keeps the old one."
  - Pipeline 0.7 `wait` "polls server-side until the Editor catches up". `unity status` should report ready before continuing.
  - "eval can run arbitrary C#, and the only lock on it is a per-project access key."
  - `unity test ./MyGame --output results.xml` "runs Edit Mode and Play Mode tests in batch and writes NUnit XML". "Exit code 6 lets a pipeline tell "the job broke" apart from "the tests failed."" `run_tests` "sometimes failed without useful detail."
  - "The current design still sends commands to one Editor."
  - It quotes an HN commenter: "the Editor takes an exclusive lock on the project folder, which makes parallel work hard."

  — [Gardenee/agmazon: Unity CLI guide](https://agmazon.com/blog/articles/technology/202609/unity-cli-mcp-guide-en.html)

**Batchmode and PlayMode tests**
- Unity Test Framework docs:
  - `-runTests` "Runs tests in the Project." "Use the -batchmode option when running tests on the command line to remove the need for manual user inputs."
  - `-testPlatform`: EditMode, PlayMode ("Play Mode tests that run in the Editor"), or any BuildTarget value for player runs. "If no value is specified for this argument, tests run in Edit Mode."
  - `-testResults` defaults to the project root. `-testFilter` takes names or a regex. With `-testCategory` as well, only tests matching both are run.
  - "There is currently no common definition for exit codes reported by individual Unity components under test." The page has no note on `-quit` or `-nographics`.

  — [Unity Test Framework: command line](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html)

**Editor or project lock**
- Unity Discussions threads on "Multiple Unity instances cannot open the same project" (search summary):
  - A batch run is refused while an Editor has the project open.
  - A lock file in the project's Temp folder can survive a run that never exited.
  - CI caches that restore Temp carry a stale lock forward.
  - Stale Unity processes can trigger the error even across different projects.

  — [Unity Discussions: build machine](https://discussions.unity.com/t/multiple-unity-instances-cannot-open-the-same-project-with-different-projects-on-build-machine/944045); [Unity Discussions: another instance running](https://discussions.unity.com/t/it-looks-like-another-unity-instance-is-running-with-this-project-open-multiple-unity-instances-cannot-open-the-same-project/248925)

**Scene, prefab and YAML editing**
- Unity Discussions thread on CLI, MCP and AI IDEs (Jan–Feb 2026):
  - xjjon (7 Feb 2026) recommends editor scripts "instead of having AI try to generate the .prefab or .unity scene file" and found them less token-intensive.
  - skdev3 (8 Feb 2026) calls editor scripts "the only deterministic interface Unity reliably accepts", and notes "sometimes poor error recovery when Unity rejects a change" and "brittle UI workflows (RectTransform, anchoring, layout groups, TextMeshPro, sprite atlases, fonts)".
  - xjjon (9 Feb 2026) on Unity AI: "it's not really capable of doing anything other than basic things like changing fields in a scene."
  - No Unity staff replied in the excerpt.

  — [Unity Discussions: CLI + MCP + AI IDE](https://discussions.unity.com/t/cli-mcp-ai-ide-chatgpt-codex-cursor-antigravity-claude-code-windsurf-next-steps-for-unity-workflow-automation/1705679)
- Cursor forum incident (21 Aug – 13 Oct 2026):
  - Approved for a script change only, the agent also edited `Assets/Scenes/Menu.unity` "externally (e.g. StrReplace/write on the .unity file)". "That edit truncated the file to ~8.3MB and cut it mid-line." "It was never in git, so I can't restore it."
  - Staff: "the write can get cut off at that limit while the tool still reports success."
  - Workarounds: add `*.unity` and `*.prefab` to `.cursorignore`, use `sed`/`perl` for large files, and check `Temp/__Backupscenes/`.
  - Cursor 3.24 now errors instead of truncating.

  — [Cursor forum](https://forum.cursor.com/t/agent-truncated-unity-scene-file-without-approval-irreversible-data-loss/169064)
- Community tools route edits through Unity's serializer instead of YAML strings (vendor claims, unverified). Prefab Sentinel works "at the Unity SerializedObject level", avoids YAML string replacement, and requires a confirm flag. unity-ctx makes mutations dry-run by default and refuses to guess GUIDs. — [Prefab Sentinel (glama)](https://glama.ai/mcp/servers/xco9wex45r); [unity-ctx](https://pkg.go.dev/github.com/Kubonsang/unity-ctx@v0.9.1)

**Editor-state and agent-behaviour pitfalls seen in the field**
- Classmethod 2D: "context compaction occurred immediately after the correction instruction", after which Claude Code "relied only on the conversation summary for corrections". A UI initialization error was caught from the console and fixed with "Delayed event registration". — [Classmethod 2D](https://dev.classmethod.jp/en/articles/unity-mcp-claude-code-2d-game-verification/)
- Classmethod TPS: Claude Code and the model each needed separate approval inside the Unity Editor. New objects were created with all Static Editor Flags disabled, unlike the existing scene. — [Classmethod TPS](https://dev.classmethod.jp/en/articles/unity-mcp-tps-game-claude-code-modification/)

### Inferences
- FishGame already follows the most-cited Unity advice:
  - scenes and prefabs come from editor scripts (`-executeMethod`);
  - the lockfile is tested rather than deleted;
  - "Exit 0 alone is not proof — read the log", which matches Unity's own statement that exit codes have no common definition;
  - logs go in the scratchpad;
  - PlayMode runs happen one after another.
- **Making the YAML rule enforceable:** the Cursor incident suggests permission deny rules or a PreToolUse hook blocking Edit/Write on `**/*.unity`, `**/*.prefab` and `**/*.meta`. Anthropic itself calls hooks deterministic and CLAUDE.md "advisory". Also commit generated scenes promptly so git is always the backstop.
- **Choose one channel per session:**
  - batchmode CLI runs need the Editor closed;
  - live control (MCP for Unity, Unity CLI) needs it open and holds the project lock;
  - after any script edit, the agent should wait for compile and domain reload to finish (`is_compiling` / `unity status` ready) before the next call;
  - expect live connections to drop on Play Mode entry or reload.
- The official `unity test` command, with exit code 6 meaning "tests failed" (secondary source), could replace some log-grepping, but only after it is verified first-hand on Unity 6000.6.
- Code-execution tools (`execute_code` / `eval`) run arbitrary C# inside the Editor. Keep them away from untrusted content such as web pages and downloaded assets.
- **Model needs from Unity's own docs:** a recent frontier model "builds most Unity features without the plugin, but defaults to older approaches". The plugin's value is version-correct APIs, not capability. This supports keeping version-specific API notes (Netcode for GameObjects, URP, Multiplayer Services) in on-demand skills or docs rather than in CLAUDE.md.

### Gaps
- No authoritative source covers how long agent compile/domain-reload loops take in practice, or best practice for batchmode compile checks beyond log-grepping. The ClaudeLab and explainx "guides" turned up in search are SEO-style and were not used.
- No source confirms how PlayMode tests behave under `-batchmode -nographics` (for example, rendering-dependent tests failing without a GPU).
- No external source covers agent testing of Netcode for GameObjects (multi-client PlayMode tests, transport ports).
- I did not read the Unity "About Unity's plugin" page, the unity-agent-plugin repo, or Unity CLI docs beyond the release-notes summary.
- The Gardenee claims (401 after domain reload, exit code 6, `wait`) are second-hand and unconfirmed.

## 5. Where these sources disagree with Anthropic

### Takeaway
On the fundamentals, Anthropic's current docs have converged with these practitioners: context is the scarce resource, verification gates matter, CLAUDE.md should be concise, CLIs beat MCP for context cost, start fresh sessions often, and use subagents for investigation. Live disagreements remain on six points:
1. auto-generating CLAUDE.md with `/init`, and putting style rules in it;
2. persona-style subagents and big parallel fan-outs;
3. relying on automatic compaction;
4. an opaque, frequently changing harness (system prompt, hidden injections, plan mode, to-dos);
5. the Claude Code "Ralph" plugin;
6. the value of hooks and slash commands, and permission posture.

### Cited Findings
- **`/init` and auto-generated files.**
  - Anthropic: "Run `/init` to generate a starter CLAUDE.md file based on your current project structure, then refine over time." — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - HumanLayer: "Don't use `/init` or auto-generate your `CLAUDE.md`." — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
  - ETH: no general gain "for both LLM-generated and developer-committed context files", and over 20% extra cost. — [arXiv 2602.11988](https://arxiv.org/abs/2602.11988)
- **Style rules in CLAUDE.md.**
  - Anthropic says include "Code style rules that differ from defaults". — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - HumanLayer: "Never send an LLM to do a linter's job." — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
  - Partial convergence: Anthropic also says hooks are "deterministic", whereas CLAUDE.md is "advisory". — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
- **Repository overviews.**
  - ETH: "repository overviews, although popular and recommended by model providers, are not helpful." — [arXiv 2602.11988](https://arxiv.org/abs/2602.11988)
  - Anthropic's current exclude list includes "File-by-file descriptions of the codebase", which is convergence. — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
- **The harness discounts CLAUDE.md.**
  - HumanLayer reports that Claude Code wraps the file in "this context may or may not be relevant to your tasks". — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
  - Anthropic's remedy for a skipped rule is to add "IMPORTANT" to that one line. — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
- **Persona subagents.**
  - Anthropic's example custom subagent is a "security-reviewer" whose prompt opens "You are a senior security engineer". The docs say "Tell Claude to use subagents explicitly". — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - Horthy: "Sub-agents are not for anthropomorphizing roles, they are for controlling context." — [AI Engineer talk](https://ai.engineer/talks/context-engineering-for-complex-codebases)
  - Willison warns against "dozens of different specialist subagents". — [Willison: Subagents](https://simonwillison.net/guides/agentic-engineering-patterns/subagents/)
- **Parallel fan-out.**
  - Anthropic: `/batch` can "split the change across 5 to 30 subagents. Each subagent works in its own worktree". Agent teams are "experimental and disabled by default". — [Anthropic docs](https://code.claude.com/docs/en/best-practices). Dynamic Workflows reportedly spawn "hundreds of parallel subagents" (search summary). — [Latent Space AINews](https://www.latent.space/p/ainews-anthropic-raises-965b-series)
  - Against it: Cognition 2025, "single-threaded linear agent" — [Cognition](https://cognition.com/blog/dont-build-multi-agents). Ronacher: mixed read/write tasks "create chaos" — [Ronacher](https://lucumr.pocoo.org/2025/7/30/things-that-didnt-work/). Hashimoto: "one at a time" — [Hashimoto](https://mitchellh.com/writing/my-ai-adoption-journey). swyx: "reviewer slop on top of the agent slop" — [Latent Space](https://www.latent.space/p/cognition).
  - Anthropic's own research post concedes that "most coding tasks involve fewer truly parallelizable tasks than research" and puts the cost at about 15× tokens. — [Anthropic multi-agent](https://www.anthropic.com/engineering/multi-agent-research-system)
- **Automatic compaction.**
  - Anthropic: "Claude Code automatically compacts conversation history when you approach context limits, which preserves important code and decisions." It also advises `/clear` and "A clean session with a better prompt almost always outperforms a long session with accumulated corrections." — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - Amp removed compaction (23 Oct 2025): "It's lossy." — [Amp Handoff](https://ampcode.com/news/handoff)
  - Horthy prefers "intentional compaction" into a reviewable markdown file. — [AI Engineer talk](https://ai.engineer/talks/context-engineering-for-complex-codebases)
  - Huntley restarts a fresh process every loop. — [Ralph](https://ghuntley.com/ralph/)
  - Classmethod saw compaction fire right after a correction, after which the agent worked only from the summary. — [Classmethod 2D](https://dev.classmethod.jp/en/articles/unity-mcp-claude-code-2d-game-verification/)
- **The Ralph plugin.**
  - Huntley's Ralph page: "watch this to learn why the claude code plugin isn't it". The reasoning is in a video I did not retrieve. — [Ralph](https://ghuntley.com/ralph/)
- **Harness opacity and churn.**
  - Zechner: "The system prompt and tools also change on every release, which breaks my workflows and changes model behavior." Harnesses inject "stuff behind your back that isn't even surfaced in the UI". "I need observability for planning and I don't get that with Claude Code's plan mode." To-do lists "confuse models more than they help." — [Zechner](https://mariozechner.at/posts/2025-11-30-pi-coding-agent/)
  - Anthropic now suggests writing a SPEC.md and executing it in a fresh session, which partly converges with Zechner's PLAN.md, Beck's plan.md, Huntley's fix_plan.md and HumanLayer's plan files. — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
- **MCP vs CLI (now mostly converged).**
  - Ronacher (3 Jul 2025): MCP "isn't truly composable" and "demands too much context". With a CLI, "the latter uses context far more efficiently". He concedes "MCP is actually pretty great when it works." — [Ronacher: Tools](https://lucumr.pocoo.org/2025/7/3/tools/)
  - Zechner measured MCP tool descriptions at 7–9% of the context window. — [Zechner](https://mariozechner.at/posts/2025-11-30-pi-coding-agent/)
  - Anthropic now says "CLI tools are the most context-efficient way to interact with external services" and still promotes MCP. — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - Unity deprecated its in-Editor MCP server in favour of its CLI. — [Unity docs](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.18/manual/integration/unity-mcp-get-started.html)
- **Hooks and slash commands.**
  - Anthropic: "Use hooks for actions that must happen every time with zero exceptions." — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - Ronacher (30 Jul 2025): "I tried hard to make hooks work, but I haven't seen any efficiency gains from them yet." Of slash commands: "I expected these to be more useful than they ended up being." — [Ronacher](https://lucumr.pocoo.org/2025/7/30/things-that-didnt-work/)
  - HumanLayer sides with deterministic tooling such as formatters. — [HumanLayer](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- **Permissions and safety posture.**
  - Anthropic defaults interactive sessions to "auto mode" (a classifier that reviews actions) and offers sandboxing. — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
  - Ronacher: "I disable all permission checks", relying on Docker. — [Ronacher](https://lucumr.pocoo.org/2025/6/12/agentic-coding/)
  - Zechner calls other agents' safeguards "mostly security theater" and makes pi YOLO by default. — [Zechner](https://mariozechner.at/posts/2025-11-30-pi-coding-agent/)
  - Willison: YOLO only "in a secure sandbox that restricts the files and secrets it can access", and "Locking internet access down to a list of trusted hosts". — [Willison](https://simonwillison.net/2025/Sep/30/designing-agentic-loops/)
- **Code review of agent output (an internal practitioner split, not just with Anthropic).**
  - Horthy (Aug 2025) favoured reviewing plans over code, and by the AI Engineer talk said all code is still reviewed. — [ACE-FCA](https://github.com/humanlayer/advanced-context-engineering-for-coding-agents/blob/main/ace-fca.md); [AI Engineer talk](https://ai.engineer/talks/context-engineering-for-complex-codebases)
  - Cole Murray: "you don't have to look at code, I think is generally a bad idea." — [Latent Space](https://www.latent.space/p/cognition)
  - Anthropic: "If you can't verify it, don't ship it." — [Anthropic docs](https://code.claude.com/docs/en/best-practices)
- **Agreement, for calibration.**
  - Beck's TDD-first prompt and Willison's red/green TDD match Anthropic's "Give Claude a check it can run". — [Beck](https://newsletter.kentbeck.com/p/augmented-coding-beyond-the-vibes); [Willison](https://simonwillison.net/guides/agentic-engineering-patterns/red-green-tdd/)
  - Anthropic's own caution about reviewer over-reporting matches swyx's "reviewer slop". — [Anthropic docs](https://code.claude.com/docs/en/best-practices); [Latent Space](https://www.latent.space/p/cognition)

### Inferences
- Where practitioners part from Anthropic, they lean consistently toward **transparency and determinism**:
  - files instead of ephemeral modes (plan files, TODO.md, handoff docs);
  - CLIs instead of MCP;
  - fresh processes instead of auto-compaction;
  - fewer agents rather than many;
  - linters and hooks instead of prose rules.
- **FishGame:**
  - **Practitioner side already:** `CURRENT.md` works as "intentional compaction" and a handoff; plans and specs live in files; Unity is driven from the CLI; and LSP-first navigation keeps context small.
  - **Closer to the Anthropic-style patterns practitioners question:** the multi-role, multi-model subagent pipeline, and reliance on prose CLAUDE.md rules for safety-critical constraints (never delete the lockfile, never hand-edit scene YAML, never commit `Library/`). A hook or deny rule would enforce those deterministically.
- Several disagreements are dated 2025 and have narrowed in Anthropic's current docs: conciseness, CLI-first, and excluding file-by-file overviews. Reports should cite the date of each position rather than treat it as current.

### Gaps
- Huntley's specific objections to the Claude Code Ralph plugin are in a video I did not retrieve.
- A secondary report claims Amp's rebuilt "Neo" CLI brought back automatic compaction. I could not verify this, and it would partly reverse Amp's anti-compaction position.
- Anthropic's docs give no numeric CLAUDE.md length guidance, so "disagreement" on size is about emphasis, not numbers.
- I found no first-hand 2026 statement from Ronacher specifically criticizing Claude Code. His Jan 2026 Pi post does not mention it, and his switch to Pi is implied rather than argued against Claude Code.
- I found no Kent Beck or Mitchell Hashimoto statement that directly disagrees with Anthropic guidance.
