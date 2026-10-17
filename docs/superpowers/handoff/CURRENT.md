# FishGame - current state (running handoff)

**Start any new conversation with:** `Read C:\Dev\FishGame\docs\superpowers\handoff\CURRENT.md and continue.`

This file lives on `main` only. The controller (Claude) rewrites it after every meaningful step (a task landed, a review, a user decision, a merge) and commits and pushes it straight to `main`. Feature branches never edit it.

_Last updated: 2026-10-17._
- **DIRECTION PIVOT (user, 2026-10-17, cloud session):** "a very big fishing game with lots of different aspects to it. Exploration/Relaxing/Still a lot of the same concepts as the last game minus the rush from the mafia chasing us / Lots of challenges needed to finish a map but a map can also offer near endless possibilities due to variations in fish, building, and other things." This supersedes R76 (mafia chase) and parts of R79 (frozen harbour town) once the design draft is approved. Draft in progress on branch `design/open-world-pivot` (cloud session). Next map work: build up Harbour Pier for this direction.
- **Player character: look NOT settled (user, 2026-10-17 night).** Branch `character-fisher`. Rejected so far: robot eyeballs (REPO shape), a 1:1 PEAK copy, the stuck-on clay faces ("Mr Potato Head"), and the big-nose stubble dad v5 ("too Mario plumber"). Liked best: the PEAK-styled build (v3, 4f49aed). Tone: "goofy, but not childish; Schedule 1 is a good example". Latest step: a face direction study, painted-in faces with toon shading and outlines (`Art/Blender/studies/2026-10-17-face-study.png`, faces A deadpan, B sleepy dad, C graphic grin, D suspicious, E JRPG hero), awaiting the user's pick in the morning. **Fallback if nothing lands (user):** a Dark Cloud / old-school Zelda look, a JRPG-hero-type fisherman with PEAK-style facial stylisation and REPO-style tracking side-eyes. The worker sessions for the rig, textures and face parts (branches `character-fisher-rig`, `-tex` and `-parts`) are paused until the look is approved. Blender 4.5 runs headless in cloud containers; EEVEE works there with Mesa (`apt-get install libegl1 libegl-mesa0 libgl1-mesa-dri libgbm1`).
- **Open-world design + Harbour build:** `design/open-world-pivot` (R80-R92, all 6 questions decided with the recommended options) is awaiting the user's confirmation before being folded into the spec. The design session orchestrates 8 harbour art workers (roster: `docs/superpowers/handoff/art/ROSTER.md` on that branch). Its villager worker holds on faces and bodies until the character look is settled. The controller's own harbour worker (`harbour-art4`) was stood down as a duplicate.
- **Story and run loop: still in design with the user (not approved).** Agreed so far: the story premise (stolen tug, starfish god, upriver from Harbour Pier, mafia in cutscenes only), a hybrid journey + extraction direction, and modular river stretches. Don't plan M4 from the main spec's section 14 until the loop is approved. Trusted free asset/sound sites (e.g. the Poly Haven API) are now allowed (CLAUDE.md).
- **Map identity review accepted** (user, spec R79): maps are built out from the water.
  - The harbour town is frozen as backdrop.
  - The south boundary moves back to the foot of the terrace stairs.
  - The harbour-best critic workflow is cancelled.
- **M3 is merged into main** (7af8041; check on main PASS: EditMode 1378 incl. 1 skip, PlayMode 139). The user's M3 board checks are still open; the user is finishing them on 2026-10-15.
- **Next:** the board results, tonight's Blender queue (section 3), then the Unity step on harbour-art3.

## 1. Read these next (in order)
1. `C:\Dev\FishGame\CLAUDE.md`: project rules (Git: no PRs; merge yourself when reviewed and green; PlayMode guard).
2. `C:\Dev\FishGame\docs\superpowers\design\2026-10-15-map-identity-review.md`: the map rules (R79) and the harbour and Lake Dock plans.
3. `C:\Dev\FishGame\docs\superpowers\design\2026-10-14-m4-design-inputs.md`: the user's M4 answers (map scale, tug, mafia, stages, map identity).
4. `C:\Dev\FishGame\docs\superpowers\plans\2026-10-13-m3-controller-notes.md`: M3 rulings and follow-ups.
5. `C:\Dev\FishGame\docs\superpowers\handoff\2026-10-13-user-checks.md`: every hands-on check the user owes.

## 2. Where things are
- **main** carries M2 (merged 235d460), M3 (merged 7af8041) and the harbour detail pass with 4 polish rounds (harbour-art2, merged 36f8abb).
  - **M2:** the fishing core, the Rust-faithful fight (R72), RV ladders (R70), smooth water (R73), the realistic-toon look (R74) and seasonal trees.
  - **M3:** T0-T25 (ecology, 25 fish, journal, Harbour Pier, dev panel, save and journal recorder); fix batches A/B/C (m3-fix-fight R77, m3-fix-look, m3-fix-ui: the arcade ladders and the Graphics tab); the final-review fixes (m3-final-fixes).
  - **Story:** R76 (the mafia chase downriver, the starfish god, 7 days = 7 river maps) and R78 (the crew tug, ice chests) are planned for M4.
- **M3 manual checks:** the user owes these on the M3 Checks board (50 checks; link in section 5).
  - As of 2026-10-14: 23 done, no FAIL; SKIP F5 (no audio) and S15 (no gamepad).
  - The user is finishing the rest on 2026-10-15.
- **Open branches (pushed, not merged):**
  - `harbour-art3` (worktree `C:\Dev\FishGame-harbour-art`), quality-first round 1, bbf16d1:
    - **Render:** SSAO, bloom and height fog.
    - **Surfaces:** painted rock/stone/plaster/roof (env layers 9-14) and the lawn carpet.
    - **On the water:** the Gull Bar sandbar with a stilt shack, the sea arch, the far mainland and the crag, sailboats, leaping fish, and the tug's chase damage.
    - **Tests:** EditMode 1389/1390 (1 skipped), PlayMode 139/139.
    - **Sonnet review:** sound, with 2 medium findings: alpha-to-coverage swells the crown cards on Low; the far mountains sit past the fog and the 400 m far clip yet still cast shadows. Details are in the scratchpad file harbour-best/r1-review.md on the user's PC.
    - **R79:** kept as the base and merged after the Unity step.
  - `fix/save-newer-schema-readonly` and `fix/journal-write-debounce`: real backlog fixes. Each needs a Unity test run and an Opus review (save data) before merging. Brief: `docs/superpowers/handoff/cloud-briefs.md`.
- **Worktrees on the PC:**
  - `C:\Dev\FishGame-harbour-art` (harbour-art3): keep it until that branch merges.
  - Merged and idle (delete only with the user's OK): `C:\Dev\FishGame-lookdev`, plus the M3-era worktrees (`FishGame-m3`, `-t15`, `-t20`, `-fixb`) if still present.

## 3. Next actions
1. **M3 board results** (when the user says the checks are done):
   - Read them with ArtifactData: `results/{checkId}` (PASS/FAIL/SKIP plus a note), `notes/general` and `notes/ideas`.
   - Summarise them, turn each FAIL or note into a task or a question, and reply under each check via its `reply` field.
   - If not done yet after the fix batches:
     - update the check texts that quote changed numbers (F2, F4, F7, F10, S4, S11);
     - add re-check rows;
     - post an F10 before/after fight-duration table.
2. **Tonight: the user's local session, Blender only.** It may run on API credits until the weekly reset (2026-10-18 ~02:00 CDT), so run lean: a Sonnet main session, one task at a time, one preview render per change, no critic loop. Queue (R79 ring 1 first):
   1. **A reusable stand kit:** rod holder, bait bucket, crate seat, tackle box, lantern, rope coil.
      - Cheap props in REPO's density style (a crate is ~100 triangles).
      - The harbour FBX encoding: uv "Layer", AO in vertex colour, no materials.
      - Reused on every later map.
   2. **Tug fixes:**
      - the LUCKY BREAK lettering clear of the fender;
      - a clean bow waterline stripe;
      - four ice-chest variants, so each player knows theirs (R78: one per player).
   3. **Harbour ring-1 dressing with the kit:**
      - within ~3 m of every stand and on the tug deck;
      - a lamp at every stand for night (night is 30% of the day);
      - stand 14's jetty: it is 2.4 m from the edge; the rule is 3 m.
   4. **Stair-foot blockers for the boundary:** a closed gate, or a rope and a sign, at the three terrace stair feet (x about -47, 4.4 and 33; the flights rise from about z -72 to the level-1 parapets at z -78.9).
3. **The Unity step (local, on harbour-art3):**
   - Fix r1's two findings.
   - **The boundary:** move `HarbourPierLayout.WallSouthZ` from -106.5 to just north of the stair feet (about -72; read the exact feet from `fg_harbour.py`) and place the blockers.
   - **Boundary checks:**
     - walk the plateau for any other way up (the dunes collision mesh);
     - no stand, spawn or needed path lies south of the new wall (stands are at z >= -48.3, spawns at -55);
     - the castable grid starts at z -60, so the bake does not change.
   - Place the kit and the tug fixes.
   - Run `check` with `-dev-profile fgtest`.
   - Get a Sonnet review (art and layout; no networking).
   - Merge harbour-art3 into main.
4. **A short design pass** (before code):
   - per-zone harbour fish tables (reef, ledge, lagoon, creek marsh; a 12.5 delta);
   - the Lake Dock river stop: river in and out, a tug berth and a camp spot. This changes colliders the tests pin (the collider-count guard, the byte-identical lake colliders) and needs a grid rebake.
   - Then the M4 design session (section 4).
5. **The in-house toon water:** a design session first (plan in 6b). The water is on screen almost all the time.
6. **The Lake Dock Blender pass:** rings 1 and 2 only. Crown-card trees that keep the seasonal swap (R74); the far forest as cards or LODs; no town (stage 2 stays plain).
7. **Later, from the harbour list:**
   - **Graphics presets** (perf research): Medium shadow tier 50 m/2048/2; Low 35 m; extra lights 3/4/8; FSR on Low; LOD bias 0.7/1.0/1.5. On High, the research said 120 m/2048/3 cascades, but harbour-art3 r1 set 120 m/4096/4 under the user's quality-first direction; reconcile the two.
   - LODs or billboards for the forest, walls, hedges and ivy.
   - Thinner crown cards on Low.
8. **The two save fixes:** a Unity test run and an Opus review, then merge.

## 4. M4 and the design session
- **Session:** a design session with the user (superpowers:brainstorming, about an hour).
- **Inputs:**
  - `docs/superpowers/design/2026-10-13-fun-formula.md` and `2026-10-14-m4-design-inputs.md` (including the 2026-10-15 map-identity items);
  - the board's "Ideas for the game" box (`notes/ideas`);
  - the research notes on threat counter-play and voice-mimic seagulls.
- **Decide:**
  - the core ingredients, items, gambling and failure;
  - the mafia (M4 = atmosphere plus live night-camp events plus the finale cutscene; day-time pressure deferred);
  - the stage list and variants, and each map's river stop and camp spot;
  - the harbour finale's identity: the wildness goes on the water (mafia boats, the leviathan tide, monster signs);
  - per-zone fish tables;
  - the first vertical-slice day;
  - the modern menu redesign (S6: pick reference games first; some screens are hard to read).
  - Target: about 70 fish, a third of them with gameplay quirks.
  - Characters come later, in Blender (RV There Yet style, REPO side-eye eyes, PEAK); not now.
- **Then the M4 handoff:** the R76 river run, starfish god camp, boat ride, mafia escalation, escape reward, cutscenes, and quota growth calibration (earnings grow only ~1.14-1.38x from day 1 to day 7; the spec assumes ~2.35x).

## 5. Boards
- Progress: https://claude.ai/artifact/SSE5uoZ7ELPyZP5A11qdXR. Keep `status/now`, `tasks`, `todos` and `agents` current.
- M3 Checks (the user writes here): https://claude.ai/artifact/LaUKFoiQZ3SYC24BCG1V1P.
  - Collections: `checks`, `results/{id}`, `notes/general` and `notes/ideas` (game ideas; each has a `text` and a Claude `reply`).
  - The old Test Notes board NAcGRrYJYuPs2Rw6SBa93C is retired (no results on it).

## 6. Working rules (short)
- **Roles:** the controller delegates. Sonnet implements; Opus high reviews integration work; Sonnet reviews Core/data/docs; Opus xhigh does the branch review.
- **Every brief says:** Write/Edit only (no sed or python edits), tests first with a real red run, explicit-path commits, push after each task, and the PlayMode guard. Run Unity in a worktree with its own -projectPath.
- **Maps follow R79:**
  - Build out from the water; walkable land needs a fishing reason.
  - Review shots come from the stands, the tug deck, the camp and the arrival only, never walk-throughs.
- **Critic or polish workflows:** only with the user's go-ahead, and only with the R79 rubric. The 2026-10-14 polish run cost ~2.2M subagent tokens and plateaued at ~6.8.
- **Workflow agents:** never SendMessage a running one (it cloned the agent once; memory: workflow-agent-messaging).
- **Cloud work:**
  - Agent isolation "remote" fell back to local worktrees on the weekly plan (2026-10-14).
  - Cloud work goes through sessions the user starts: briefs in `docs/superpowers/handoff/cloud-briefs.md`, limits in `cloud-environment.md`.
  - Unity and live Blender work needs the PC.
- **Test runs:** always pass `-dev-profile fgtest`; without it they touch the user's real LocalLow settings.
- **Worktrees:** Unity rewrites `.vscode/settings.json` in them; never commit it.
- **Save often:** the user's usage can run out mid-session. Update this file at each step.
- **Token hygiene:** start a fresh conversation at milestones or after a long break, using the line at the top of this file.

## 6b. AI tooling (installed 2026-10-14)
- **Unity MCP** (CoplayDev MCP for Unity, MIT):
  - **Setup:** the package `com.coplaydev.unity-mcp` is pinned to `#v10.3.0` in `Packages/manifest.json`; the server `unity` is in `.mcp.json` (`uvx mcpforunityserver==10.3.0`, stdio, `DISABLE_TELEMETRY=true`).
  - **Editor lock:** it only works while the editor is open. An open editor holds `Temp/UnityLockfile`, so batchmode compile and test runs need the editor closed.
  - **Split:** use the MCP for scene, GameObject and console work in the live editor, and batchmode for `check`.
  - **First open:** run `Window > MCP for Unity`, check that telemetry shows as disabled, and do not let it rewrite the client config.
- **Blender MCP:** the server is `blender` (`uvx blender-mcp`, ahujasid).
  - User-scope skills in `~/.claude/skills`, from RobLe3/cc-blender-skill (MIT): `text-to-blender` (the router), `blender-modeling`, `-materials`, `-uv-texturing`, `-lighting`, `-rendering`, `-cameras`, `-animation`, `-export`, `-pro-workflow` and `reference-to-3d`.
  - The pack's other 19 skills were skipped (niche reconstruction loops) to save listing tokens.
- **game-developer** skill (Jeffallan/claude-skills v0.4.18, MIT, user scope): generic Unity patterns. Its "60+ FPS on all platforms" rule is overridden by the spec's quality-first performance target.
- **Asset Store packs** (free; Standard Unity Asset Store EULA, Extension Asset). Added to the user's account on 2026-10-14; not imported yet.
  - **How to use:** import through Package Manager > My Assets, under `Assets/ThirdParty/<pack>/`. Use them as painting and blending references or bases behind the look-dev, not as drop-in replacements.
  - **Surfaces:** 25+ Free Stylized Textures (Game Buffs 241895); FREE Stylized Textures - RPG Environment (Blink 204187); Free Stylized Textures (LowlyPoly 204742).
  - **Round 2:** Cartoon FX Remaster Free (Jean Moreno 109565, URP: splashes, hit pops); Fantasy Worlds: Forest FREE (282610, URP; check its LODs); Free Pack - Rocks Stylized (316250, URP); Free Stylized Nature Environment (96371, built-in RP, so its materials need converting to URP). Free Toon Water (387614) was skipped: its page didn't clearly show it as free.
  - **Round 3 (character reference):** Synty Sidekick 336970, ithappy Creative Characters 304841, OlyPoly Party Game Characters 342650, Supercyan 79870, Kevin Iglesias Human Basic Motions 154271.
  - **Round 4 (audio and VFX):** FREE Casual Game SFX Pack (Dustyroom 54116), SoundBits Free SFX (31837), Water Splash Pack (14039), Nature - Essentials ambience (208227), Stylized Water Effect Pack VFX (270114). These replace or augment the procedural placeholders.
- **Planned: in-house toon water** (user, 2026-10-14), instead of buying Stylized Water 3.
  - **Why not buy:** Stylized Water 3 is $50, plus $20 for Dynamic Effects and $25 for Underwater. Its page (https://assetstore.unity.com/packages/package/287769, Staggart Creations) is a feature reference only; don't copy its code.
  - **Scope, for a short design session before any code:**
    1. **URP Shader Graph water:** depth-based shallow/deep colour, intersection foam from scene depth, scrolling normals, fresnel, a cheap fake reflection or sky probe, and a projected caustics texture.
    2. **Rivers:** a flowmap or spline-UV flow direction, so the river maps share one material with a speed per section.
    3. **Waves:** Gerstner waves on the GPU, with the same function in pure C# (Core, EditMode-tested), so buoyancy for bobbers, the tug and props matches the visual surface on host and clients.
    4. **Dynamic ripples:** a render texture stamped by casts, bobbers, fish strikes and boats. Skip underwater rendering unless the design needs it.
    5. **Shoreline foam** and blending against the coast, shingle and jetty edges. Lake Dock shares the material.
  - **The forest is handled separately, for free:** Blender-baked LOD meshes and billboard cards, with thinner crown cards on Low.

## 7. Open questions for the user
- Fish keep/adjust; the Silver Mullet icon belly colour; the fight feel verdict (K.3); the bad-luck nudge (keep/tune/remove); deck-edge grab; the default test-session look; winter tree colliders; the online-cancel message (also on the board).
- The proposed $20 music buy: Casual Game Music by Andrew Sitkov (30191, $10) plus Casual Game Music Pack by Dustyroom (53575, $10).
- Deleting the merged, idle worktrees (section 2).

## 8. Harbour history (reference)
- **T17 (M3):** Blender art for every visual mesh, with the builder, colliders, stands and bake staying code (ruling T17-B in the M3 controller notes).
- **Detail pass (harbour-art2, 3bfd603 + dbeaf30):** 37 new FBX plus a `pass2` manifest block (placements, animHint, smoke/light points, ladders). The tug is moored off the breakwater; its gangway lands at z -26.
- **Polish workflow (wf_f8fbbf2a-074; 5 rounds, 21 agents, ~2.2M subagent tokens):**
  - **Critic averages:** 5.8 → 6.0 → 6.7 → 6.9 → 6.7, against a target of 8.5.
  - **Weakest:** layout (5.5-6.2) and surfaces (5.5). **Strongest:** lighting and dusk (7-7.6) and cohesion (7-7.5).
  - **r1** (897c31b, 51523e9): terraced town, chapel, windmill, lookout tower (stand 12), east-basin jetty (stand 11), creek mouth, farm hills, mafia hints.
  - **r2** (20b9c88, 3fd741c): crown-card trees, ~8k GPU ground tufts (hidden on Low), the coast reshape, the tug berth, calmer water (Lake Dock too), a warmer day.
  - **r3** (bb37300): West Horn and the creek lagoon, the bent lane and village square, bed-mapped water.
  - **r4** (8a390f0, 35d66b1): creek glen, West Head waterfall, the reef and Skerries, East Ledge (stand 14), villagers, dog and ducks, toon clouds.
  - **Final gate** (27cc5d6): the generator ROOT path fix.
  - **Branch results:** EditMode 1389/1389, PlayMode 139/139. Worst view: Low 384,592 triangles (budget 500k), High 534,204.
- **Quality-first run (wf_8dc74d12-0fa, harbour-art3):** r1 critics 6.6/6.8/6.7; r2 critics 6.8/6.7/7.2. The r2 artist was cut off before writing (usage). **Cancelled 2026-10-15 by R79: do not resume.**
- **Lessons:**
  - The walk-through rubric pushed the map inland (R79).
  - A SendMessage to a running workflow agent cloned it (memory).
- **Notes:**
  - Sapling, A.N.T. and Poly Haven were not used (determinism).
  - The parked r2-copy-work patches are obsolete (folded into r2).
  - Perf research: `docs/superpowers/research/2026-10-14-friendslop-graphics-settings.md` (Low budget 500k triangles per view, plus renderer, shadow-caster and alpha-coverage audits).
  - Style study: `docs/superpowers/research/2026-10-14-reference-style-notes.md`.
  - S3 (user's PC): the frame is GPU-bound (CPU 0.2 ms of ~5 ms, 210 fps).
  - Still open from the detail pass:
    - the 16 code village trees were cones/blobs (check whether r2's crown cards replaced them);
    - the ice chests are one model with 4 placements (the variants are in tonight's queue).
- **Builder checklist for any new harbour FBX** (from the detail-pass integration):
  - copy the FBX, the manifest and `HarbourPierLayout.Visuals` together;
  - `_Tri` models use `M_Toon_Tri`;
  - `Harbour_Core_River` uses the water material, with no collider and not on the Water layer;
  - `_Col` models get a MeshCollider only, and walls, pillars and dunes go on the Shore layer;
  - Low hides the group "detail";
  - the harbour grid stays byte-identical unless it is rebaked on purpose.
- **The user's map-feel notes (2026-10-14), now under R79:**
  - Each map is a stop on the downriver run: the crew's tug moored as the hero, a river mouth in the backdrop, light mafia hints, the world continuing past the walls.
  - Life comes from noise, clutter and detail.
  - The user has an RTX 5070.
  - Quality first: Low is a best-effort fallback (spec Graphics tab note, 53732a7).

## 9. Live pages
- Harbour build log (screenshots as the map is built): https://claude.ai/artifact/TmqCpXrHJBXRZBhZMKkMhW (db `shots`, `meta/status`; images uploaded as assets).

## 10. M3 branch review: deferred items (M4 or a post-merge round)
- **Dropped 2026-10-14** (user: no anti-cheat; mods and WeMod welcome): the cancel-reroll and cast-origin-slack exploits, and the rod/bait release-build guard.
- **In-scene `WorldState` hash:** its `GlobalObjectIdHash` changes on every Build All and is not in `ContentHash`. Put each Spot's World hash in the approval payload, or pin the file ids.
- **Newer-schema meta.json:** one that fails to parse is treated as corrupt; read `schemaVersion` first (fix branch exists).
- **Journal saves:** a synchronous Flush per change on every peer; debounce or move to a background write (fix branch exists).
- **Map switch:** loadouts and the pity streak reset on a switch (they live in the per-scene `FishingHostLedger`; real map travel must move them into session state).
- **Smaller items:** quota calibration; journal level-1 sub-areas; an Esc `HandleBack` test.

## 11. Ideas queued for the design session
- **Seagulls that steal voices** (user, 2026-10-14):
  - Like the Lethal Company voice-mimic mods (Skinwalkers, Mirage): gulls record short clips of players and squawk them back positionally.
  - Needs voice chat first (Vivox likely), and it must have an opt-out setting.
  - Research: `docs/superpowers/research/2026-10-14-voice-mimic-seagulls.md`.
- **Mafia counter-play research:** `docs/superpowers/research/2026-10-14-friendslop-threat-counterplay.md`.

## 12. Token efficiency (user, 2026-10-14)
- **Ponytail benchmark:** 2 tasks with and without its rules (Sonnet, no Unity). Diffs were 37-60% smaller, but tokens only 1-4% lower, and the save fix lost tests and the read-only .bak view. Not adopted.
- **Code navigation:** the .NET 10 SDK, `csharp-ls` 0.28.0 (global dotnet tool) and the csharp-lsp plugin (project scope) are installed. `Tools/sync-csproj.ps1` gives worktrees Unity's project files.
- **Turned off for this project** (`.claude/settings.json`):
  - plugins: playwright, typescript-lsp, security-guidance, superdesign, yapui;
  - synced plugins: brightdata, data, design, engineering, cowork-plugin-management, pdf-viewer;
  - the `rea` MCP server (it timed out every session).
- **Account default:** the claude.ai connectors Vercel, Supabase and Gmail are off for new sessions; re-enable them from the + menu, Connectors.
- **The REA study is finished.**
