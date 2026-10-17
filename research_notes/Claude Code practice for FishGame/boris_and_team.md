# How Boris Cherny and the Claude Code team personally use and prompt Claude Code (notes compiled 2026-10-17)

Evidence labels used below:
- **[P]** = primary and verified. I read the author's own words: X posts unrolled verbatim through Thread Reader App or the FxTwitter API, the author's own Hacker News comments pulled from the HN Algolia API, or official Anthropic/Claude blog and docs pages. The URLs cite the original post.
- **[P-ep]** = a quote from the publisher's own episode or transcript page (Latent Space, Every, Pragmatic Engineer), pulled out by a fetch tool. These are probably accurate, but I did not check them against audio.
- **[S]** = second-hand. This covers fan compilations, journalists, blogs and summaries. Treat the wording as paraphrase unless a reporter is quoting directly.
- Dates are US Pacific posting dates. Where the API's UTC timestamp falls on the next day, I converted it back.

## 1. What are Boris's concrete tips (the early-2026 setup thread and its follow-ups)?

### Takeaway
Boris posted a 13-tip thread on 2026-01-02 describing a "surprisingly vanilla" setup:
- 5 terminal sessions plus 5–10 web sessions.
- Opus 4.5 with thinking for everything.
- One team CLAUDE.md, checked into git and added to whenever Claude errs, including by tagging @.claude on PRs.
- Plan mode first, then auto-accept.
- Slash commands such as /commit-push-pr, and subagents code-simplifier and verify-app.
- A PostToolUse formatter hook, and a /permissions allowlist instead of skipping permissions.
- MCP and CLI tools, and Stop hooks or ralph-wiggum for long tasks.
- Above all, verification: "give Claude a way to verify its work" for "2-3x the quality".

The follow-ups (2026-01-31 through 2026-10) shift toward:
- worktrees, skills and loops/routines
- auto mode instead of allowlists
- effort levels in place of thinking toggles
- dynamic workflows with many agents, and phone or agent-view management
- encoding repeated corrections as lint rules, skills, REVIEW.md and CLAUDE.md, rather than ad-hoc prompting

Verification is the one tip that stays constant from Dec 2025 to Oct 2026.

### Cited Findings

#### A. Before the thread (Dec 2025): the same advice in his HN comments [P]
- On 2025-12-13 Boris gave four tips on HN. Each is a direct quote:
  1. "If there is anything Claude tends to repeatedly get wrong, not understand, or spend lots of tokens on, put it in your CLAUDE.md … I add to my team's CLAUDE.md multiple times a week."
  2. "Use Plan mode (press shift-tab 2x)… This easily 2-3x's results for harder tasks."
  3. "Give the model a way to check its work… This is another 2-3x."
  4. "Use Opus 4.5. It's a step change from Sonnet 4.5."

  — [HN comment 46256606](https://news.ycombinator.com/item?id=46256606)
- **CLAUDE.md size [P]**, 2025-12-13: "Try to keep it under 1k tokens or so. We will show you a warning if it might be too big. Ours is maybe half that size. We remove from it with every model release since smarter models need less hand-holding. You can also break up your CLAUDE.md into smaller files, link CLAUDE.mds, or lazy load them only when Claude works in nested dirs." — [HN comment 46256682](https://news.ycombinator.com/item?id=46256682)
- **Who writes CLAUDE.md [P]**, 2025-12-13: "It's not about fighting the model, rather it's giving the model a shortcut to get the context it needs to do its work. You don't have to have one. Ours is 100% written by Claude itself." — [HN comment 46256724](https://news.ycombinator.com/item?id=46256724)
- **Output stats [P]**, 2025-12-27: "In the last thirty days, I landed 259 PRs -- 497 commits, 40k lines added, 38k lines removed. Every single line was written by Claude Code + Opus 4.5. Claude consistently runs for minutes, hours, and days at a time (using Stop hooks)." — [x.com/bcherny/status/2004887829252317325](https://x.com/bcherny/status/2004887829252317325)

#### B. The setup thread, 2026-01-02 (verbatim from the Thread Reader unroll) [P]
- **Intro**: "My setup might be surprisingly vanilla! Claude Code works great out of the box, so I personally don't customize it much. There is no one correct way to use Claude Code… Each person on the Claude Code team uses it very differently." — [thread root](https://x.com/bcherny/status/2007179832300581177) ([unroll](https://threadreaderapp.com/thread/2007179832300581177.html))
- **1/ Parallel sessions**: "I run 5 Claudes in parallel in my terminal. I number my tabs 1-5, and use system notifications to know when a Claude needs input." — [1/](https://x.com/bcherny/status/2007179833990885678)
- **2/ Web and phone**: "I also run 5-10 Claudes on claude.ai/code, in parallel with my local Claudes… I will often hand off local sessions to web (using &)… sometimes I will --teleport back and forth. I also start a few sessions from my phone (from the Claude iOS app) every morning and throughout the day." — [2/](https://x.com/bcherny/status/2007179836704600237)
- **3/ Model**: "I use Opus 4.5 with thinking for everything. It's the best coding model I've ever used, and even though it's bigger & slower than Sonnet, since you have to steer it less and it's better at tool use, it is almost always faster than using a smaller model in the end." — [3/](https://x.com/bcherny/status/2007179838864666847)
- **4/ CLAUDE.md**: "Our team shares a single CLAUDE.md for the Claude Code repo. We check it into git, and the whole team contributes multiple times a week. Anytime we see Claude do something incorrectly we add it to the CLAUDE.md, so Claude knows not to do it next time. Other teams maintain their own CLAUDE.md's. It is each team's job to keep theirs up to date." — [4/](https://x.com/bcherny/status/2007179840848597422)
- **5/ PR review feeds CLAUDE.md**: "During code review, I will often tag @.claude on my coworkers' PRs to add something to the CLAUDE.md as part of the PR. We use the Claude Code Github action (/install-github-action) for this. It's our version of @danshipper's Compounding Engineering." — [5/](https://x.com/bcherny/status/2007179842928947333)
- **6/ Plan mode**: "Most sessions start in Plan mode (shift+tab twice). If my goal is to write a Pull Request, I will use Plan mode, and go back and forth with Claude until I like its plan. From there, I switch into auto-accept edits mode and Claude can usually 1-shot it. A good plan is really important!" — [6/](https://x.com/bcherny/status/2007179845336527000)
  - The fan site quotes this tip as "…important to avoid issues down the line", which differs from the unrolled text — [S: howborisusesclaudecode.com](https://howborisusesclaudecode.com/)
- **7/ Slash commands**:
  - "I use slash commands for every 'inner loop' workflow that I end up doing many times a day… Commands are checked into git and live in .claude/commands/."
  - "Claude and I use a /commit-push-pr slash command dozens of times every day. The command uses inline bash to pre-compute git status and a few other pieces of info to make the command run quickly and avoid back-and-forth with the model."

  — [7/](https://x.com/bcherny/status/2007179847949500714)
- **8/ Subagents**: "code-simplifier simplifies the code after Claude is done working, verify-app has detailed instructions for testing Claude Code end to end… I think of subagents as automating the most common workflows that I do for most PRs." — [8/](https://x.com/bcherny/status/2007179850139000872)
- **9/ Hooks**: "We use a PostToolUse hook to format Claude's code. Claude usually generates well-formatted code out of the box, and the hook handles the last 10% to avoid formatting errors in CI later." — [9/](https://x.com/bcherny/status/2007179852047335529)
- **10/ Permissions**: "I don't use --dangerously-skip-permissions. Instead, I use /permissions to pre-allow common bash commands that I know are safe in my environment… Most of these are checked into .claude/settings.json and shared with the team." — [10/](https://x.com/bcherny/status/2007179854077407667)
- **11/ Tools**: Claude "searches and posts to Slack (via the MCP server), runs BigQuery queries to answer analytics questions (using bq CLI), grabs error logs from Sentry… The Slack MCP configuration is checked into our .mcp.json and shared with the team." — [11/](https://x.com/bcherny/status/2007179856266789204)
- **12/ Long-running tasks**: "For very long-running tasks, I will either (a) prompt Claude to verify its work with a background agent when it's done, (b) use an agent Stop hook to do that more deterministically, or (c) use the ralph-wiggum plugin… I will also use either --permission-mode=dontAsk or --dangerously-skip-permissions in a sandbox… so Claude can cook without being blocked on me." — [12/](https://x.com/bcherny/status/2007179858435281082)
- **13/ Verification (his "most important" tip)**:
  - "probably the most important thing to get great results out of Claude Code -- give Claude a way to verify its work. If Claude has that feedback loop, it will 2-3x the quality of the final result."
  - "Claude tests every single change I land to claude.ai/code using the Claude Chrome extension… Verification looks different for each domain… Make sure to invest in making this rock-solid."

  — [13/](https://x.com/bcherny/status/2007179861115511237)

#### C. "Tips from the Claude Code team" thread, 2026-01-31 [P]
- **Framing**: "The way the team uses Claude is different than how I use it. Remember: there is no one right way to use Claude Code." — [root](https://x.com/bcherny/status/2017742741636321619) ([unroll](https://threadreaderapp.com/thread/2017742741636321619.html))
- **1. Parallelism**:
  - "Spin up 3–5 git worktrees at once, each running its own Claude session in parallel. It's the single biggest productivity unlock, and the top tip from the team. Personally, I use multiple git checkouts, but most of the Claude Code team prefers worktrees."
  - Shell aliases "(za, zb, zc)", and a "dedicated 'analysis' worktree that's only for reading logs and running BigQuery".

  — [tip 1](https://x.com/bcherny/status/2017742743125299476)
- **2. Plan mode**:
  - "Start every complex task in plan mode. Pour your energy into the plan so Claude can 1-shot the implementation."
  - "One person has one Claude write the plan, then they spin up a second Claude to review it as a staff engineer."
  - "the moment something goes sideways, they switch back to plan mode and re-plan. Don't keep pushing."
  - They "explicitly tell Claude to enter plan mode for verification steps."

  — [tip 2](https://x.com/bcherny/status/2017742745365057733)
- **3. CLAUDE.md**:
  - "After every correction, end with: 'Update your CLAUDE.md so you don't make that mistake again.' Claude is eerily good at writing rules for itself."
  - "Ruthlessly edit your CLAUDE.md over time. Keep iterating until Claude's mistake rate measurably drops."
  - One engineer has Claude "maintain a notes directory for every task/project, updated after every PR", and points CLAUDE.md at it.

  — [tip 3](https://x.com/bcherny/status/2017742747067945390)
- **4. Skills**:
  - "If you do something more than once a day, turn it into a skill or command."
  - "Build a /techdebt slash command and run it at the end of every session to find and kill duplicated code."
  - A command that "syncs 7 days of Slack, GDrive, Asana, and GitHub into one context dump".

  — [tip 4](https://x.com/bcherny/status/2017742748984742078)
- **5. Bugs**: "paste a Slack bug thread into Claude and just say 'fix.'"; "'Go fix the failing CI tests.' Don't micromanage how."; "Point Claude at docker logs." — [tip 5](https://x.com/bcherny/status/2017742750473720121)
- **6. Prompting**:
  - "Grill me on these changes and don't make a PR until I pass your test."
  - "Prove to me this works", having Claude "diff behavior between main and your feature branch".
  - "Knowing everything you know now, scrap this and implement the elegant solution."
  - "Write detailed specs and reduce ambiguity before handing work off."

  — [tip 6](https://x.com/bcherny/status/2017742752566632544)
- **7. Environment**:
  - "The team loves Ghostty!"
  - "use /statusline to customize your status bar to always show context usage and current git branch"
  - Color-coding and naming terminal tabs, "sometimes using tmux — one tab per task/worktree"
  - "Use voice dictation. You speak 3x faster than you type."

  — [tip 7](https://x.com/bcherny/status/2017742753971769626)
- **8. Subagents**:
  - "Append 'use subagents' to any request where you want Claude to throw more compute at the problem."
  - "Offload individual tasks to subagents to keep your main agent's context window clean."
  - "Route permission requests to Opus 4.5 via a hook — let it scan for attacks and auto-approve the safe ones."

  — [tip 8](https://x.com/bcherny/status/2017742755737555434)
- **9. Data**: "We have a BigQuery skill checked into the codebase… Personally, I haven't written a line of SQL in 6+ months." — [tip 9](https://x.com/bcherny/status/2017742757666902374)
- **10. Learning**:
  - "Explanatory" or "Learning" output styles
  - HTML presentation explainers and ASCII diagrams
  - A "spaced-repetition learning skill"

  — [tip 10](https://x.com/bcherny/status/2017742759218794768)

#### D. Follow-ups, Feb–Mar 2026 [P]
- **Customization thread, 2026-02-11**:
  - Effort: "Run /model to pick your preferred effort level… Personally, I use High for everything." — [2/](https://x.com/bcherny/status/2021699860869902424)
  - Custom agents: "drop .md files in .claude/agents. Each agent can have a custom name, color, tool set, pre-allowed and pre-disallowed tools, permission mode, and model." You can also set the main conversation's default agent via the "agent" setting or `--agent`. — [4/](https://x.com/bcherny/status/2021700144039903699)
  - Permissions: "Out of the box, we pre-approve a small set of safe commands… Check these into your team's settings.json. We support full wildcard syntax. Try 'Bash(bun run *)' or 'Edit(/docs/**)'." — [5/](https://x.com/bcherny/status/2021700332292911228)
  - Sandbox: "Run /sandbox… supports both file and network isolation." — [6/](https://x.com/bcherny/status/2021700506465579443)
  - Status line: "Everyone on the Claude Code team has a different statusline." — [7/](https://x.com/bcherny/status/2021700784019452195)
  - Hooks:
    - "Automatically route permission requests to Slack or Opus"
    - "Nudge Claude to keep going when it reaches the end of a turn (you can even kick off an agent or use a prompt to decide whether Claude should keep going)"
    - pre/post-process tool calls

    — [9/](https://x.com/bcherny/status/2021701059253874861)
  - Output styles: "explanatory" for a new codebase, "learning" for coaching. — [11/](https://x.com/bcherny/status/2021701379409273093)
  - "When you do customize, check your settings.json into git… We support 37 settings and 84 env vars." — [12/](https://x.com/bcherny/status/2021701636075458648)
- **Built-in worktrees, 2026-02-20**: "agents can run in parallel without interfering with one other. Each agent gets its own worktree" (`claude --worktree`, `--tmux`, Desktop worktree mode). — [x.com/bcherny/status/2025007393290272904](https://x.com/bcherny/status/2025007393290272904)
- **/simplify and /batch, 2026-02-27**: "I have been using both daily."
  - /simplify: "Use parallel agents to improve code quality, tune code efficiency, and ensure CLAUDE.md compliance. Usage: 'hey claude make this code change then run /simplify'".
  - /batch: "Interactively plan out code migrations, then execute in parallel using dozens of agents. Each agent runs with full isolation using git worktrees, testing its work before putting up a PR."

  — [root](https://x.com/bcherny/status/2027534984534544489), [/simplify](https://x.com/bcherny/status/2027534986178662573), [/batch](https://x.com/bcherny/status/2027534987462119828)
- **/loop, 2026-03-07**: "schedule recurring tasks, for up to 3 days at a time." Examples: "/loop babysit all my PRs. Auto-fix build issues and when comments come in, use a worktree agent to fix them". — [x.com/bcherny/status/2030193932404150413](https://x.com/bcherny/status/2030193932404150413)
- **Code Review product, 2026-03-09**: "A team of agents runs a deep review on every PR. We built it for ourselves first. Code output per Anthropic engineer is up 200% this year and reviews were the bottleneck… [it] catches many real bugs that I would not have noticed otherwise." — [x.com/bcherny/status/2031089411820228645](https://x.com/bcherny/status/2031089411820228645)
- **Auto mode launch, 2026-03-24**: "no 👏 more 👏 permission prompts 👏", quoting the launch post: "auto mode lets Claude make permission decisions on your behalf. Safeguards check each action before it runs." — [x.com/bcherny/status/2036555259997462541](https://x.com/bcherny/status/2036555259997462541)
- **Hidden-features thread, 2026-03-29** ([unroll](https://threadreaderapp.com/thread/2038454337811386436.html)):
  - Mobile: "Personally, I write a lot of my code from the iOS app." — [1/](https://x.com/bcherny/status/2038454337811386436)
  - Remote control: "Personally, I have 'Enable Remote Control for all sessions' set in my /config." — [2/](https://x.com/bcherny/status/2038454339933548804)
  - Loops: "/loop and /schedule… for up to a week at a time. I have a bunch of loops running locally:" — [3/](https://x.com/bcherny/status/2038454341884154269)
    - "/loop 5m /babysit, to auto-address code review, auto-rebase, and shepherd my PRs to production"
    - "/loop 30m /slack-feedback"
    - "/loop /post-merge-sweeper"
    - "/loop 1h /pr-pruner"
    - "Experiment with turning workflows into skills + loops."
  - Hooks: SessionStart to load context, PreToolUse to log bash, "Route permission prompts to WhatsApp" (PermissionRequest), "Poke Claude to keep going whenever it stops (Stop)". — [4/](https://x.com/bcherny/status/2038454343519932844)
  - Verification: "The most important tip for using Claude Code is: give Claude a way to verify its output… Personally, I use the Chrome extension every time I work on web code. It tends to work more reliably than other similar MCPs." — [6/](https://x.com/bcherny/status/2038454347156398333)
  - Worktrees: "Worktrees are essential for doing lots of parallel work in the same repository. I have dozens of Claudes running at all times, and this is how I do it." — [10/](https://x.com/bcherny/status/2038454353787519164)
  - /batch fans work out to "dozens, hundreds, even thousands" of worktree agents. — [11/](https://x.com/bcherny/status/2038454355469484142)
  - `--bare`: "This was a design oversight when we first built the SDK, and in a future version, we will flip the default to --bare." — [12/](https://x.com/bcherny/status/2038454357088457168)
  - `--add-dir` for multi-repo work. — [13/](https://x.com/bcherny/status/2038454359047156203)
  - `--agent` for a custom system prompt and tools. — [14/](https://x.com/bcherny/status/2038454360418787764)
  - Voice: "Fun fact: I do most of my coding by speaking to Claude, rather than typing." — [15/](https://x.com/bcherny/status/2038454362226467112)

#### E. Follow-ups, Apr–Oct 2026 [P]
- **Opus 4.7 tips, 2026-04-16** ([unroll](https://threadreaderapp.com/thread/2044847848035156457.html)):
  - Auto mode: "permission prompts are routed to a model-based classifier… This means no more babysitting… it means you can run more Claudes in parallel." — [1/](https://x.com/bcherny/status/2044847849662505288)
  - "/fewer-permission-prompts… scans through your session history… recommends a list of commands to add to your permissions allowlist… especially if you don't use auto mode." — [2/](https://x.com/bcherny/status/2044847851591856461)
  - Recaps. — [3/](https://x.com/bcherny/status/2044847853030580247)
  - Focus mode: "The model has reached a point where I generally trust it to run the right commands and make the right edits. I just look at the final result." — [4/](https://x.com/bcherny/status/2044847855006024147)
  - Effort: "Opus 4.7 uses adaptive thinking instead of thinking budgets… Personally, I use xhigh effort for most tasks, and max effort for the hardest tasks." — [5/](https://x.com/bcherny/status/2044847856872546639)
  - Verification: "This has always been a way to 2-3x what you get out of Claude, and with 4.7 it's more important than ever… many of my prompts these days look like 'Claude do blah blah /go'." /go is a skill that has Claude:
    1. "Test itself end to end using bash, browser, or computer use"
    2. "Run the /simplify skill"
    3. "Put up a PR"

    — [6/](https://x.com/bcherny/status/2044847858634064115)
- **Agent view, 2026-05-11**: "The best way to level up from 1 agent => many agents. No more cycling between terminal tabs." — [x.com/bcherny/status/2053982327123132846](https://x.com/bcherny/status/2053982327123132846)
- **/code-review consolidation, 2026-05-27 (HN)**: "Going forward it will just be the built-in /code-review skill." Usage:
  - `/code-review --fix`
  - effort levels low through max
  - "/code-review ultra … reliably catches >99% of bugs, costs $3-20 per review"

  — [HN 48296387](https://news.ycombinator.com/item?id=48296387)
- **Personal dynamic-workflow uses, 2026-05-28 (HN)**:
  - "Autonomously landed 20+ optimizations to reduce Claude Code's token usage by ~15%"
  - ported native modules to TypeScript
  - "repeatedly found and fixed flaky tests (with /loop)"
  - "Shipped 69 code simplification PRs, deleting >10k lines of code"

  — [HN 48312433](https://news.ycombinator.com/item?id=48312433)
- **Five tips for running Opus "for hours/days", 2026-06-07**:
  1. "Use auto mode for permissions"
  2. "Use dynamic workflows, to have Claude orchestrate hundreds/thousands of agents"
  3. "Use /goal or /loop, to nudge Claude to keep going until it's done"
  4. "Use Claude Code in the cloud, so you can close your laptop"
  5. "Make sure Claude has a way to self-verify its work end to end: Claude in Chrome… iOS/Android sim MCP… a way to start the full web server"

  — [x.com/bcherny/status/2063792263067754658](https://x.com/bcherny/status/2063792263067754658)
- **One-year retrospective with Cat Wu, 2026-06-08**: "why I use auto mode instead of plan mode, how routines fix bugs before I see them, why I do most of my coding from my phone now." — [x.com/bcherny/status/2064034799711588805](https://x.com/bcherny/status/2064034799711588805) (video: [YouTube Hth_tLaC2j8](https://www.youtube.com/watch?v=Hth_tLaC2j8))
- **Nested subagents, 2026-06-09**: "experiment more with agents kicking off agents as a way to better manage context. Capped at depth=5 to start." — [x.com/bcherny/status/2064327225504403752](https://x.com/bcherny/status/2064327225504403752)
- **Fable 5, 2026-06-09**:
  - "Fable is the best model I have used for coding, by a wide margin… less prompts and steers… more intelligent self-verification, longer running sessions." — [x.com/bcherny/status/2064402671898075579](https://x.com/bcherny/status/2064402671898075579)
  - "After 4.5 came out I uninstalled my IDE when I realized that I'd been doing 100% of my coding in a terminal for a few weeks… There's nothing in claude code's prompting telling the model to [verify before declaring victory], it's just part of its personality." — [x.com/bcherny/status/2064431111154053187](https://x.com/bcherny/status/2064431111154053187)
- **Claude Tag, about 2026-06-23**: "65% of our product team's new code is created by our internal version of Claude Tag." The date is inferred from the quoted [@claudeai post](https://x.com/claudeai/status/2069468693017268244). I read this in the "more from @bcherny" panel of [this unroll](https://threadreaderapp.com/thread/2007179832300581177.html).
- **"Steps of AI Adoption", undated (after June 2026)**:
  - "giving Claude ways to verify its own work end to end… enabling auto mode for permissions, defaulting on automated code review and security review, and using interfaces that let you manage multiple agents at once"
  - "To get to higher levels it means /loop, /batch, dynamic workflows, and worktree isolation for subagents."

  — same [unroll panel](https://threadreaderapp.com/thread/2007179832300581177.html)
- **Automation as infrastructure, 2026-07-15**:
  - "If Claude instead writes a lint rule, CI step, or routine, that class of issue can be fully automated forever. This is really what people are talking about when they talk about loops."
  - "Every team should be writing the CLAUDE.md's, REVIEW.md's, skills, and docs that enable agents to productively work in their codebase with zero additional context from the prompter."
  - A reviewer rejecting a PR for convention reasons: "these are failures of automation."

  — [x.com/bcherny/status/2077460395279692197](https://x.com/bcherny/status/2077460395279692197)
- **/checkup, 2026-07-08**: the post reads only "Here's what happened when I ran /checkup". — [x.com/bcherny/status/2074997571563479143](https://x.com/bcherny/status/2074997571563479143) [P]. A fan site summarizes the findings as a broken launcher, 38 unused skills and a ~10k-token CLAUDE.md. — [S: howborisusesclaudecode.com](https://howborisusesclaudecode.com/)
- **Opus 5, 2026-07-24**: "Opus 5 is our least prompt injectable model yet… combined with prompt injection probes, combined with Auto Mode in Claude Code -- the success rate for prompt injection attacks drops to ~0." — [x.com/bcherny/status/2080713091688583312](https://x.com/bcherny/status/2080713091688583312)
- **"Claude maintains the apps", 2026-08-13**:
  - Claude Tag runs daily routines in a Slack channel: crash fuzzer, dup unifier, dead-code remover, "abstraction police".
  - "these routines have opened 388 PRs across our repos, 180 of which we merged after Claude Code Review + human review."
  - "if it doesn't [get it right], we ask Claude to tune its routines so it's better the next day."

  — [x.com/bcherny/status/2088014489438621990](https://x.com/bcherny/status/2088014489438621990)
- **Reading code, 2026-09-10**:
  - "I read diffs when it's touching critical code, or code that's important to get right. I don't write the code." — [reply](https://x.com/bcherny/status/2098210496109572123)
  - On the Ink renderer: "I don't actually know how this impl works -- we treat it as a black box with a ton of property-based tests and benchmarks. Claude maintains it for us." — [reply](https://x.com/bcherny/status/2098210282464247871)
  - Launch of the /diff pane. — [post](https://x.com/bcherny/status/2098165206296011059)
- **Explainer artifacts on PRs, 2026-09-25 (HN)**:
  - "For complex diffs to core parts of the system, I will often ask it to make diagrams or even interactive demos."
  - "I ask Claude to attach these artifacts to its PRs also, so others can understand and future Claudes have the context."

  — [HN 49850929](https://news.ycombinator.com/item?id=49850929)

#### F. Other Claude Code team members
- **Cat Wu (PM/lead)**
  - Opus 4.7 thread, 2026-04-16 [P]:
    - "The model performs best if you treat it like an engineer you're delegating to, not a pair programmer you're guiding line by line."
    1. Use auto mode.
    2. "Give Claude Code your full task context upfront: goal, constraints, acceptance criteria in the first turn."
    3. "Tell the model how to verify its changes. Put your testing workflow in your claude.md, or add a /verify-app skill."
    - The default effort for 4.7 was set to xhigh.

    — [root](https://x.com/_catwu/status/2044808533905178822), [tip 3](https://x.com/_catwu/status/2044808538351100377), [effort](https://x.com/_catwu/status/2044808539663978970)
  - Agent view, 2026-05-11 [P]: "personally, i like to run `claude agents` from my root code dir to manage all my claude code agents in one place." — [x.com/_catwu/status/2053999857799672111](https://x.com/_catwu/status/2053999857799672111)
  - Dynamic workflows, 2026-05-28 [P]: "Mention 'workflow' in a prompt and Claude will dynamically create an orchestration plan that it strictly follows." — [x.com/_catwu/status/2060054180379689074](https://x.com/_catwu/status/2060054180379689074)
  - Memory, Oct 2025 [P-ep]: "if I say, 'Make the button pink,' I don't want you to remember to make all buttons pink in the future." — [Every, AI & I](https://every.to/podcast/how-to-use-claude-code-like-the-people-who-built-it)
  - Called herself "a context minimalist" in the June 2026 video. — [S: The Neuron](https://www.theneuron.ai/explainer-articles/claude-code-creators-boris-cherny-and-cat-wu-explain-how-to-use-agent-loops/)
- **Thariq Shihipar**
  - Effort, 2026-03-13 [P]: you can set effort to "max", which "will spend your usage limits more quickly so you have to activate it per session." — [x.com/trq212/status/2032632596572811575](https://x.com/trq212/status/2032632596572811575)
  - Session management, 2026-04-15 [P]:
    - "Our general rule of thumb is when you start a new task, you should also start a new session."
    - "If I had to pick one habit that signals good context management, it's rewind." Rewind (Esc Esc) and re-prompt rather than typing "that didn't work, try X".
    - "we see some level of context rot happen around ~300-400k tokens."
    - Compact proactively with directions, since "the model is at its least intelligent point when compacting."

    — [x.com/trq212/status/2044548257058328723](https://x.com/trq212/status/2044548257058328723)
  - Dynamic workflows article with Sid, 2026-06-02 [P]:
    - Names the failure modes workflows counter: "Agentic laziness", "Self-preferential bias", "Goal drift".
    - Example prompt: "Using a workflow, go through my last 50 sessions and mine them for corrections I keep making and turn the recurring ones into CLAUDE.md rules."
    - Trigger word "ultracode".
    - "Dynamic workflows often use more tokens."

    — [x.com/trq212/status/2061907337154367865](https://x.com/trq212/status/2061907337154367865)
  - "Finding Your Unknowns", 2026-07-03 [P]:
    - "Fable is the first model where I find the quality of the work is bottlenecked by my ability to clarify its unknowns."
    - "reducing and planning for your unknowns is the skill of agentic coding."
    - "I also start almost every coding session with an exploration or brainstorming phase."
    - Uses "blindspot pass" and "unknown unknowns" prompts and HTML artifacts.
    - "Watching someone like Boris or Jarred prompt, it is obvious to me that they know what they want in-detail."

    — [x.com/trq212/status/2073100352921215386](https://x.com/trq212/status/2073100352921215386)
  - Context engineering, 2026-07-24 [P]:
    - "We removed over 80% of Claude Code's system prompt for models like Claude Opus 5 and Claude Fable 5 with no measurable loss on our coding evaluations."
    - "Then: Give Claude rules / Now: Let Claude use judgement."
    - "giving examples actually constrains them."
    - CLAUDE.md: "Keep your CLAUDE.md lightweight and briefly describe what your repo is for, but spend most of the tokens on gotchas… Avoid stating 'the obvious'… Use progressive disclosure heavily."
    - "/doctor… to rightsize your skills, and CLAUDE.md files."

    — [x.com/trq212/status/2080710971228918066](https://x.com/trq212/status/2080710971228918066); [blog](https://claude.com/blog/the-new-rules-of-context-engineering-for-claude-5-generation-models)
  - Effort, 2026-09-25 [P]:
    - "For normal software engineering, I'm now running a loop of getting the model to interview me then implementing on low/medium effort, reviewing what it built, and then running verification on high effort." — [x.com/trq212/status/2103576349499855160](https://x.com/trq212/status/2103576349499855160)
    - "I use effort low a lot more when I want to stay in the loop, and effort max is basically only if I want zero input or want to find security vulnerabilities." — [x.com/trq212/status/2103577115010687067](https://x.com/trq212/status/2103577115010687067)
  - "Interview me" spec technique:
    - The official docs now teach it [P]: "I want to build [brief description]. Interview me in detail using the AskUserQuestion tool… Keep interviewing until we've covered everything, then write a complete spec to SPEC.md." — [code.claude.com/docs/en/best-practices](https://code.claude.com/docs/en/best-practices)
    - Attribution of the original prompt to Thariq is only second-hand. — [S: VelvetShark](https://velvetshark.com/stop-prompting-claude-code-let-it-interview-you)
- **Sid Bidasaria (built subagents and dynamic workflows)** — dynamic workflows thread, 2026-05-28 [P]:
  - "just mention the word 'workflow'… You can also turn on `/effort ultracode` which uses workflows aggressively."
  - "Reach for a workflow when a task is too big for a normal session, like a codebase wide bug hunt."
  - "Route the work through adversarial judges… You can go as far as a tournament-style bracket."
  - "workflows are reusable: save them, commit them to your repo, share them with your team and/or turn them into a skill."
  - "workflows can get expensive to run… Highly recommend getting a feel for token usage before unleashing them on larger tasks."

  — [x.com/sidbid/status/2060047508806746142](https://x.com/sidbid/status/2060047508806746142) ([unroll](https://threadreaderapp.com/thread/2060047508806746142.html))
- **Adam Wolff**: I found no verified statement about his personal setup. A Korean forum reports a late-Nov-2025 X post predicting software engineering would be "done" and that people would stop checking generated code, as they don't check compiler output — [S: damoang.net](https://damoang.net/free/5360168). His QCon SF bio covers how AI shifts the SDLC bottleneck to architecture — [S: qconsf.com](https://qconsf.com/speakers/adamwolff).
- **Team code-review process (Addy Osmani, Morgan Westlee Lunt, Qing Wang; claude.dev, 2026-10-15)** [P] — [claude.dev/blog/solving-code-review-at-anthropic](https://claude.dev/blog/solving-code-review-at-anthropic/):
  - **Verify before review**: "We give Claude something it can run—like tests, a build or a screenshot… and ask it to show the output instead of saying it's done. Then a subagent reviews the diff with fresh context." Before a PR, "one [skill] hunts for bugs, one simplifies the diff, and one checks the behavior end to end."
  - **Fix repeats at the source**: "When the automated reviewer keeps flagging the same thing, we fix it at the source: in the skill, with a lint rule, or with updates to instructional files."
    - CLAUDE.md: "Every review loads it, so in a large codebase we keep it short and point to other docs from there."
    - REVIEW.md: "written by code owners… The good ones are short."
  - **Risk tiers**: Low (docs, flagged additive changes) need "None required once automated review and security checks are green". Medium needs one person. High needs two. "The change sets the tier, not the author." "An agent's approval never counts as a person's."
  - **Convergence**: "One CLI PR went through more than fifty rounds of review and fixes overnight." Fix: "After three rounds, the reviewer asks the author to step back and reconsider the approach."
  - **Volume**: merged PRs "more than doubled" from June to September while the revert share "fell by more than 40 percent". Monorepo changes per day rose from "about 2,500 in May to more than 30,000 in September". Claude "writes about 90 percent of Claude Code's own code".
  - **Closing rule**: "When a review misses something, we ask what the model didn't have in front of it and put the fix into a CLAUDE.md, a skill or a new check."
  - **Adoption checklist** (abridged):
    - "Give your agent checks it can run, listed in a short CLAUDE.md, plus hooks that run formatters and linters."
    - "Run /code-review and /security-review locally."
    - "Add a REVIEW.md, and move repeated review comments into it."
    - "Cap review rounds."
    - "Never let a bot's approval replace a required human's."

### Inferences
- **What has stayed constant**:
  - verification loops, which Boris rates 2-3x and repeats in Dec 2025, Jan, Mar, Apr and Jun 2026, and which the Oct 2026 team process builds on
  - shared, checked-in configuration: CLAUDE.md, settings.json allowlists, .mcp.json, commands and skills
  - running many sessions at once
  - turning any repeated workflow into a command, skill or loop
- Boris's "vanilla" personal setup coexists with heavy team-level automation such as hooks, skills, routines and review agents. His point is that the leverage lives in shared repo config and verification, not in personal tweaks.
- **CLAUDE.md practice is "add often, prune often"**: he adds multiple times a week, but keeps the file under ~1k tokens (Dec 2025) and deletes material at every model release. By Oct 2026, repeated corrections go first into lint rules, skills, hooks or REVIEW.md, and CLAUDE.md stays short with pointers. The "2.5k tokens" figure that circulates for Boris's CLAUDE.md is second-hand and conflicts with his own HN statement.
- **Possible relevance for FishGame** (inference, not from the sources):
  - Boris's model stance ("almost always faster" to use the top model) and Thariq's effort loop (implement at low/medium effort, verify at high) both bear on a cost-tiered agent setup like FishGame's. Effort levels are an alternative cost lever to switching models.
  - The Oct 2026 team process (round caps, risk tiers, REVIEW.md, "fix at the source") maps closely onto a "scale reviews to risk" policy.

### Gaps
- **Replies to the Jan 2 thread**: I could not verify any reply where Boris states his CLAUDE.md length, such as the widely repeated "~2.5k tokens". Only aggregators repeat it — [S: mindwiredai](https://mindwiredai.com/2026/03/25/claude-code-creator-workflow-claudemd/).
- The details behind the /checkup post (2026-07-08) and the exact date of the "Steps of AI Adoption" post are unverified.
- I did not retrieve Thariq's original "interview me" tweet.
- I found no verified personal-setup statements from Adam Wolff, and little from Sid beyond dynamic workflows.
- I did not check whether `--bare` became the default as promised on 2026-03-29.

## 2. What did Boris (and teammates) say in podcasts and interviews?

### Takeaway
Interviews track the same arc as the X posts:
- **2025**: Sonnet by default, "Think hard. Don't write any code yet.", a human in the loop for bash, and $6/day average spend.
- **Late 2025 to early 2026**: Opus plus plan mode, CLAUDE.md compounding, stop hooks, /commit and /code-review commands, 5 parallel checkouts, 20–30 PRs/day and no hand-written code.
- **Mid-2026**: thousands of agents overnight, coding from his phone, auto mode instead of plan mode, and routines that fix bugs before he sees them.

Most 2026 podcast transcripts were paywalled or unavailable to me, so much of that material is second-hand.

### Cited Findings
- **Latent Space, "Claude Code: Anthropic's Agent in Your Terminal" (Boris and Cat Wu, 2025-05-07)** [P-ep] — [latent.space/p/claude-code](https://www.latent.space/p/claude-code):
  - Model: Boris: "We default to Sonnet for most everything just because we find that it outperforms."
  - Planning prompt: "Think hard. Don't write any code yet."
  - Parallel exploration: "I'll ask Claude to investigate a few paths in parallel" for hairy problems.
  - Tests: "I have not manually written a unit test in many months." He has Claude check its UI in Puppeteer until it matches the mock.
  - Permissions: "I'll just hit shift tab. Enter auto accept mode" for test-writing. He wants a human in the loop for bash because of destructive commands (paraphrase).
  - Hooks: suggests a `claude -p` call in a pre-commit hook, kept fast ("just types and lint maybe").
  - Cost: Cat: "we're seeing costs around like $6 per day" (a user average).
  - Principles: "do the simple thing first", and "We think of it as like a Unix utility."
  - Prompting: "in a month or two months or three months, you won't need this anymore."
- **Every "AI & I" with Dan Shipper, "How to Use Claude Code Like the People Who Built It" (Boris and Cat, 2025-10-29)** [P-ep] — [every.to](https://every.to/podcast/how-to-use-claude-code-like-the-people-who-built-it):
  - Boris's /feature-dev: "First ask me what exactly I want, build the specification, and then build a detailed plan."
  - A /commit command; /code-review runs parallel subagents, then five more to challenge findings: "it finds all the real issues without the false [ones]."
  - Stop hooks: "You can just make the model keep going until the thing is done."
  - A shared settings.json pre-approves routine commands and blocks risky files.
  - Some engineers have Claude write diary entries per task, and agents distill them into lessons.
- **YC Lightcone, "Inside Claude Code With Its Creator Boris Cherny" (about 2026-02-17, [YouTube PQU9o_5rHC4](https://www.youtube.com/watch?v=PQU9o_5rHC4))** [S]:
  - Summaries say plan mode came from users typing "think about this but don't code yet", and that Boris "built it in 30 minutes on a Sunday night".
  - They also say he predicted plan mode has a limited lifespan, possibly "about a month".

  — [traces.com summary](https://traces.com/s/jn7e26f5f3jg60zssbjy87c39981a054), [Engineered Intelligence](https://engineeredintelligence.substack.com/p/how-the-claude-code-team-works). Boris's own HN comment corroborates the Sunday-night origin [P]: "something I came up with late on a Sunday night many months ago" — [HN 49850929](https://news.ycombinator.com/item?id=49850929).
- **Lenny's Podcast, "Head of Claude Code: What happens after coding is solved" (2026-02-19)**: the transcript is paywalled ([lennysnewsletter.com](https://www.lennysnewsletter.com/p/head-of-claude-code-what-happens)). A near-verbatim Japanese write-up reports he has not edited a line by hand since November and lands 10–30 PRs a day — [S: note.com](https://note.com/ai_eng_tech/n/nf940a6dc47e6?hl=en).
- **Pragmatic Engineer podcast, "Building Claude Code with Boris Cherny" (2026-03-04)** — [newsletter.pragmaticengineer.com](https://newsletter.pragmaticengineer.com/p/building-claude-code-with-boris-cherny):
  - [P-ep] quotes:
    - "once there is a good plan, it will one-shot the implementation almost every time."
    - "always make sure that when you start a migration, you finish the migration." Partial migrations confuse humans and models.
    - "There's just no way we could have shipped this if we started with static mocks and Figma or if we started with a PRD."
  - Paraphrased by the page:
    - He runs about 5 instances, each in its own checkout, and ships roughly 20–30 PRs a day.
    - His Meta-era habit: log repeated review comments and write a lint rule after a comment recurs 3–4 times.
- **Sequoia AI Ascent (2026-04-20)**: as reported, he runs "five to ten sessions", each with several agents, and said "I usually have a few thousand [agents] working every night doing some deeper work", relying on /loop and Routines — [S: dev.ua citing Business Insider](https://dev.ua/en/news/tvorets-claude-code-rozpoviv-shcho-ioho-robochyi-protses-vkliuchaie-tysiachi-shi-subahentiv-iaki-pratsiuiut-vnochi-1778685531); event page: [sequoiacap.com](https://www.sequoiacap.com/article/ai-ascent-2026).
- **Fortune Brainstorm Tech (June 2026)**: "This morning I was managing maybe a few hundred". The headline says there are days he manages "tens of thousands" of agents — [S: Fortune](https://fortune.com/2026/06/08/anthropics-boris-cherny-creator-of-claude-code-says-there-are-days-he-manages-tens-of-thousands-of-ai-agents-at-once/).
- **Anthropic one-year-after-GA video with Cat Wu (2026-06-08)**:
  - Topics confirmed by Boris's own post [P]: "why I use auto mode instead of plan mode… why I do most of my coding from my phone now" — [x.com/bcherny/status/2064034799711588805](https://x.com/bcherny/status/2064034799711588805).
  - The Neuron [S] — [theneuron.ai](https://www.theneuron.ai/explainer-articles/claude-code-creators-boris-cherny-and-cat-wu-explain-how-to-use-agent-loops/):
    - Plan mode mattered for roughly Opus 4–4.5 and was unnecessary by 4.6/4.7.
    - When Claude errs, he has it write the lesson into CLAUDE.md or a skill.
    - About half his engineering happens on his phone.
    - Routines watch voice-mode tickets, GitHub issues and bug reports and draft fixes. Another fixes bug reports unanswered for 5 hours.
    - Verification test: "can the agent run the thing?"
    - Cat: "a context minimalist".
  - Fan-site quotes from the video, unverified [S] — [howborisusesclaudecode.com](https://howborisusesclaudecode.com/):
    - plan mode: "I don't use that anymore."
    - "Every single time Claude makes a mistake, I don't tell it to do it differently."
    - "When you accept 99% of requests, your eyes glaze over."

### Inferences
- The interviews add few new tactics beyond the X threads. Their value is in the dated snapshots of scale and habits:
  - Sonnet and a human in the loop (May 2025)
  - plan mode and 20–30 PRs/day (Mar 2026)
  - thousands of overnight agents (Apr–Jun 2026)
- Two habits run through Boris's career: converting repeated review feedback into lint rules (at Meta, per Pragmatic Engineer) and his 2026 "failures of automation" essay. That suggests the "update CLAUDE.md on every mistake" tip is one instance of a broader rule: encode repeated corrections as infrastructure.

### Gaps
- I could not get full transcripts of Lenny's (paywalled), YC Lightcone, the June 2026 Anthropic video (the transcript endpoint failed), Big Technology (2026-05-20), Acquired (June 2026; see [WorkOS takeaways](https://workos.com/blog/boris-cherny-claude-code-acquired-interview-takeaways)), YC Startup School (2026-07-28) or CHM Live (Oct 2026). Quotes attributed to those appearances are second-hand.
- I did not examine Anthropic's "Mastering Claude Code in 30 minutes" (Code with Claude, May 2025) or the Pragmatic Engineer article "How Claude Code is built" (Sept 2025).

## 3. Which of these tips were later contradicted or updated?

### Takeaway
Several early-2026 tips have been explicitly reversed or reworked, mostly because the models improved:
- **Plan mode**: from "most sessions start in plan mode" to "useful, and is no longer useful".
- **Permissions**: from an allowlist without skip-permissions to auto mode, now the default permission mode.
- **Model and thinking**: from "Opus 4.5 with thinking" to effort levels (High → xhigh/max, and Thariq's low-to-implement, high-to-verify loop) and newer models (Opus 4.8, Fable 5, Opus 5/5.5).
- **CLAUDE.md**: from "add a rule after every mistake" to a lean file of gotchas with progressive disclosure, plus auto-memory, skills, lint rules and REVIEW.md.

Verification and shared, checked-in config were never contradicted.

### Cited Findings
- **Plan mode, reversed**:
  - Earlier [P]:
    - "This easily 2-3x's results for harder tasks" (2025-12-13) — [HN](https://news.ycombinator.com/item?id=46256606)
    - "Most sessions start in Plan mode" (2026-01-02) — [6/](https://x.com/bcherny/status/2007179845336527000)
    - "Start every complex task in plan mode" (2026-01-31) — [tip 2](https://x.com/bcherny/status/2017742745365057733)
  - Later [P]:
    - 2026-06-08: "why I use auto mode instead of plan mode" — [post](https://x.com/bcherny/status/2064034799711588805).
    - 2026-09-25: "plan mode was useful, and is no longer useful… plan mode has always been a prompt — it has never changed the toolset because doing so would break the prompt cache… using early versions of Fable, I realized that I wasn't using plan mode anymore because the model just got it, and because… planning had become interactive and iterative. With Opus 5.5, I feel Opus has gotten to that point too." — [HN 49850929](https://news.ycombinator.com/item?id=49850929)
    - Same day: "plan mode isn't going away. You can always /plan or ask Claude to enter plan mode. We might re-map the shift+tab keyboard shortcut." — [HN 49851117](https://news.ycombinator.com/item?id=49851117)
  - Official docs (Oct 2026) [P]: "Plan mode is useful, but also adds overhead… If you could describe the diff in one sentence, skip the plan." — [best-practices docs](https://code.claude.com/docs/en/best-practices)
  - What replaced it: Thariq's interview-then-implement loop and exploration/brainstorm sessions [P] — [effort post](https://x.com/trq212/status/2103576349499855160), [unknowns article](https://x.com/trq212/status/2073100352921215386).
- **Permissions, updated**:
  - 2025: auto-accept only for "safe" work like tests, with a human in the loop for bash [P-ep] — [Latent Space](https://www.latent.space/p/claude-code).
  - 2026-01-02: "I don't use --dangerously-skip-permissions", with a /permissions allowlist [P] — [10/](https://x.com/bcherny/status/2007179854077407667).
  - 2026-01-31: route prompts to Opus via a hook [P] — [tip 8](https://x.com/bcherny/status/2017742755737555434).
  - 2026-02-11: sandboxing [P] — [6/](https://x.com/bcherny/status/2021700506465579443).
  - 2026-03-24: auto mode [P] — [post](https://x.com/bcherny/status/2036555259997462541).
  - 2026-04-16: "Auto mode = no more permission prompts", and allowlists "especially if you don't use auto mode" [P] — [1/](https://x.com/bcherny/status/2044847849662505288), [2/](https://x.com/bcherny/status/2044847851591856461).
  - Docs, Oct 2026 [P]: "With Claude Code v2.1.283 or later, auto mode is the built-in starting permission mode for interactive terminal and VS Code sessions… After the tenth approval you're clicking through rather than reviewing." — [docs](https://code.claude.com/docs/en/best-practices)
- **Model choice and thinking, updated repeatedly**:
  - Sonnet default (May 2025) [P-ep] — [Latent Space](https://www.latent.space/p/claude-code).
  - Opus 4.5 "with thinking for everything" (Jan 2026) [P] — [3/](https://x.com/bcherny/status/2007179838864666847).
  - "I use High for everything" (Feb 2026) [P] — [2/](https://x.com/bcherny/status/2021699860869902424).
  - "xhigh effort for most tasks, and max effort for the hardest" (Apr 2026); the 4.7 default became xhigh [P] — [5/](https://x.com/bcherny/status/2044847856872546639), [Opus 4.7 best practices](https://claude.com/blog/best-practices-for-using-claude-opus-4-7-with-claude-code).
  - Opus 4.8 is "our strongest coding model yet" (2026-05-28) [P] — [post](https://x.com/bcherny/status/2060048873440129073).
  - Fable 5 is "the best model I have used for coding, by a wide margin" (2026-06-09) [P] — [post](https://x.com/bcherny/status/2064402671898075579).
  - Opus 5 (2026-07-24) [P] — [post](https://x.com/bcherny/status/2080713091688583312).
  - By Sept 2026 he refers to "Opus 5.5/Fable 5.1" [P] — [HN](https://news.ycombinator.com/item?id=49851117).
  - Contrast on effort: Thariq (Sept 2026) prefers low/medium effort for implementing, high for verification, and max "basically only if I want zero input or want to find security vulnerabilities" [P] — [x.com/trq212/status/2103577115010687067](https://x.com/trq212/status/2103577115010687067).
  - Anthropic briefly lowered the default effort and reversed it: "The latter was not the right decision, so we rolled it back… people didn't understand to use /effort to increase intelligence" (2026-04-23) [P] — [HN 47880132](https://news.ycombinator.com/item?id=47880132).
- **Thinking keywords, retired**:
  - The original April 2025 best-practices post mapped ""think" < "think hard" < "think harder" < "ultrathink."" to thinking budgets [P, quoted by Simon Willison] — [simonwillison.net](https://simonwillison.net/2025/Apr/19/claude-code-best-practices/). That URL now redirects (308) to the living docs at [code.claude.com/docs/en/best-practices](https://code.claude.com/docs/en/best-practices).
  - By Opus 4.7: "Extended Thinking with a fixed thinking budget is not supported… Opus 4.7 offers adaptive thinking". Steer it with phrasing such as "Think carefully and step-by-step…" [P] — [Opus 4.7 best practices](https://claude.com/blog/best-practices-for-using-claude-opus-4-7-with-claude-code).
  - In April 2026 Boris suggested `CLAUDE_CODE_DISABLE_ADAPTIVE_THINKING=1` as an interim workaround when adaptive thinking under-allocated on some turns [P] — [HN 47668520](https://news.ycombinator.com/item?id=47668520).
- **CLAUDE.md, the emphasis shifted from adding to pruning and relocating**:
  - Earlier [P]:
    - "Anytime we see Claude do something incorrectly we add it" — [4/](https://x.com/bcherny/status/2007179840848597422)
    - "Update your CLAUDE.md so you don't make that mistake again" — [tip 3](https://x.com/bcherny/status/2017742747067945390)
    - Tagging @.claude on PRs — [5/](https://x.com/bcherny/status/2007179842928947333)
  - Already in Dec 2025 [P]: "under 1k tokens… We remove from it with every model release" — [HN 46256682](https://news.ycombinator.com/item?id=46256682).
  - July 2026 (Thariq) [P]:
    - "Then: Memory in CLAUDE.md files / Now: Auto-memory… We used to encourage users to save things to Claude's memory, by using the # hotkey."
    - "A common myth is that you want to make these a central repository for every known practice."
    - CLAUDE.md should be "lightweight… gotchas".

    — [context-engineering blog](https://claude.com/blog/the-new-rules-of-context-engineering-for-claude-5-generation-models)
  - July 2026 (Boris) [P]: encode recurring issues as a "lint rule, CI step, or routine" as well as CLAUDE.md, REVIEW.md and skills — [post](https://x.com/bcherny/status/2077460395279692197).
  - October 2026 (team) [P]: fix repeated reviewer flags "in the skill, with a lint rule, or with updates to instructional files", keeping CLAUDE.md "short" with pointers — [claude.dev](https://claude.dev/blog/solving-code-review-at-anthropic/).
  - Docs [P]: "For each line, ask: 'Would removing this cause Claude to make mistakes?' If not, cut it… run /doctor and Claude proposes cuts" — [docs](https://code.claude.com/docs/en/best-practices).
  - A conflicting size claim: "about 2.5k tokens / ~100 lines" for Boris's CLAUDE.md [S] — [mejba.me](https://www.mejba.me/blog/boris-cherny-claude-code-workflow) — versus his own "Ours is maybe half [of 1k tokens]" [P].
- **PR-review loop, systematized**:
  - "@.claude on PRs" via the GitHub Action (Jan 2026) [P].
  - The Code Review product (Mar 2026) [P] — [post](https://x.com/bcherny/status/2031089411820228645).
  - A single /code-review skill with effort tiers and "ultra" (May 2026) [P] — [HN](https://news.ycombinator.com/item?id=48296387).
  - The Oct 2026 process [P] — [claude.dev](https://claude.dev/blog/solving-code-review-at-anthropic/):
    - a reviewer on every PR
    - REVIEW.md
    - risk-tiered human approval
    - a round cap after 3
    - "Never let a bot's approval replace a required human's"
- **Parallelism and interface, scaled up**:
  - 5 terminal tabs plus 5–10 web sessions (Jan 2) — [1/](https://x.com/bcherny/status/2007179833990885678), [2/](https://x.com/bcherny/status/2007179836704600237).
  - "Personally, I use multiple git checkouts" while the team prefers worktrees (Jan 31) — [tip 1](https://x.com/bcherny/status/2017742743125299476).
  - Built-in `--worktree` (Feb 20) — [post](https://x.com/bcherny/status/2025007393290272904).
  - "dozens of Claudes running at all times" (Mar 29) — [10/](https://x.com/bcherny/status/2038454353787519164).
  - "a few thousand… every night" (Apr 20) [S].
  - Agent view, "No more cycling between terminal tabs" (May 11) — [post](https://x.com/bcherny/status/2053982327123132846).
  - Dynamic workflows with "hundreds/thousands of agents" (Jun 7) — [post](https://x.com/bcherny/status/2063792263067754658).
  - Nested subagents (Jun 9) — [post](https://x.com/bcherny/status/2064327225504403752).
  - Phone use grew [P]:
    - "start a few sessions from my phone… every morning" (Jan)
    - "I write a lot of my code from the iOS app" (Mar) — [1/](https://x.com/bcherny/status/2038454337811386436)
    - "most of my coding from my phone now" (Jun) — [post](https://x.com/bcherny/status/2064034799711588805)
- **Long-running tasks, tooling replaced the hacks**:
  - Jan 2026: Stop hook, ralph-wiggum, and dontAsk or skip-permissions in a sandbox — [12/](https://x.com/bcherny/status/2007179858435281082).
  - Later [P]:
    - /loop for "up to 3 days" (Mar 7) — [post](https://x.com/bcherny/status/2030193932404150413)
    - then "up to a week" (Mar 29) — [3/](https://x.com/bcherny/status/2038454341884154269)
    - /goal, cloud sessions, auto mode and dynamic workflows (Jun 7) — [post](https://x.com/bcherny/status/2063792263067754658)
    - Routines in Slack via Claude Tag (Aug 13) — [post](https://x.com/bcherny/status/2088014489438621990)
- **Subagents and slash commands, became built-ins and skills**:
  - Personal subagents code-simplifier and verify-app, and .claude/commands (Jan) [P].
  - Built-in /simplify and /batch (Feb 27) [P].
  - A personal /go skill chaining e2e test, /simplify and PR (Apr 16) [P] — [6/](https://x.com/bcherny/status/2044847858634064115).
  - Docs now point to built-in /verify and Stop hooks as the "deterministic gate" [P] — [docs](https://code.claude.com/docs/en/best-practices).
  - Opus 4.7 "spawns fewer subagents by default", so spell out fan-out [P] — [Opus 4.7 best practices](https://claude.com/blog/best-practices-for-using-claude-opus-4-7-with-claude-code).
  - The dynamic-workflow trigger changed from mentioning "workflow" to "use a workflow" because of false positives [S] — [howborisusesclaudecode.com](https://howborisusesclaudecode.com/).
- **How much code he reads, loosened**:
  - Wanting a human in the loop for bash (2025).
  - "I just look at the final result" (focus mode, Apr 2026) — [4/](https://x.com/bcherny/status/2044847855006024147).
  - "I read diffs when it's touching critical code… I don't write the code" (Sep 2026) — [reply](https://x.com/bcherny/status/2098210496109572123).
  - The Ink renderer is maintained as "a black box with a ton of property-based tests and benchmarks" (Sep 2026) — [reply](https://x.com/bcherny/status/2098210282464247871).
- **Context size, new guidance with 1M windows**:
  - Thariq: context rot at "~300-400k tokens", a new session per task, and rewind over correction (Apr 2026) [P] — [article](https://x.com/trq212/status/2044548257058328723).
  - Boris on HN: a stale session idle for more than 1 hour misses the prompt cache. They were investigating "defaulting to 400k context instead"; try `CLAUDE_CODE_AUTO_COMPACT_WINDOW=400000` (2026-04-12) [P] — [HN 47740541](https://news.ycombinator.com/item?id=47740541).
- **Unchanged — verification**:
  - Dec 2025: "another 2-3x" — [HN](https://news.ycombinator.com/item?id=46256606)
  - Jan 2026: "the most important thing" — [13/](https://x.com/bcherny/status/2007179861115511237)
  - Mar 2026: "The most important tip" — [6/](https://x.com/bcherny/status/2038454347156398333)
  - Apr 2026: "more important than ever" — [6/](https://x.com/bcherny/status/2044847858634064115)
  - Jun 2026: "self-verify its work end to end" — [post](https://x.com/bcherny/status/2063792263067754658)
  - The Oct 2026 team process quotes his Jan tip verbatim [P] — [claude.dev](https://claude.dev/blog/solving-code-review-at-anthropic/).

### Inferences
- The reversals line up with model generations. Boris ties plan mode to Opus 4–4.5 and calls it unneeded from Fable/Opus 5.5, and the team cut ~80% of the system prompt for Claude 5 models. Advice older than one model generation (about 3–6 months) should be checked before anyone adopts it.
- "Update CLAUDE.md after every mistake" has been refined rather than contradicted:
  - The trigger, encoding recurring corrections, survives.
  - The destination broadened to skills, lint/CI rules, hooks, REVIEW.md and auto-memory.
  - The file itself should stay short. Thariq's own example uses a workflow to mine past sessions for corrections, then turn the recurring ones into CLAUDE.md rules.
- Permission advice moved from "don't skip permissions" to "use auto mode". The team's argument is that rubber-stamped prompts give no real oversight ("After the tenth approval you're clicking through rather than reviewing").

### Gaps
- No verbatim transcript exists for the June 2026 video in which Boris explains dropping plan mode and his mistake-handling habit. The quotes there are second-hand.
- I found no source saying whether the "@.claude on PRs to update CLAUDE.md" habit is still used day to day, rather than replaced by Code Review plus REVIEW.md.
- I found no explicit retraction of the subagents code-simplifier and verify-app. That they were superseded by /simplify and /verify is my inference.
- The current status of the `--bare` default flip and of the shift+tab remap for plan mode is unknown.
