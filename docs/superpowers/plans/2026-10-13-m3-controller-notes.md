# M3 controller notes (review findings, rulings and queued brief items)

Copied from the controller's session notes on 2026-10-13 so later sessions keep them. Items marked QUEUED / BRIEF ADD are still to apply in T15, T17, T20-T25.

# M3 contract change requests collected from the section authors (resolve in one contract-amend pass after A-D report)

## Section D (docs/superpowers/plans/2026-10-13-m3-plan-host-world.md, uncommitted)
- CCR-D1 (blocks T13): removing the species MinBaitRating/MinDepth fields, PlaceholderRoller and RollRequest breaks files outside T13's list: FishingTestData.cs (R72-owned; only 2 field initializers), FishingDefsTests, FishingValidatorTests, FishingContentTests (T3), FishingRollValidationTests, FishingWireTests, and the Configure callers in EcologyMigration/EcologyContentBuilder/EcologySeedData (T3/T10). Two tests are deleted (V3 replaces them). Fallback: keep the fields unused until M4. Leaning: accept, since R72 lands first.
- CCR-D2: no API to remove a service from a scope, so "unregisters on despawn" becomes "has no state after despawn".
- CCR-D3: the driver can't see FishingHost, so the run seed comes through GetOrCreate/Clear and is adopted by reference.
- CCR-D4: new internal helper and test files.
- CCR-D5 (for T16): WorldState's serialized field is spotId; spawn calls the driver's ServerInitialize.
- Q for section B: HotspotPlanner.Advance catch-up across large jumps; null grid; run state starts at Day 1; ledger commit/release allocation-free.
- Q for section A: the name of T1's session-end reset (T15 extends it with Luck).
- USER Q: keep M2's fixed dev run seed (same season/weather each session until the dev panel changes it) vs a random seed per session. Default: fixed.

## Section C (docs/superpowers/plans/2026-10-13-m3-plan-data-coverage.md, uncommitted)
- CR-1: the R72 FishingBots members (BotKind, FightRecord, SeedFor, CastFor, RunFight) become internal, no behaviour change. FOLD INTO the R72 F4 brief (fight builder) before F4 is built.
- CR-2: routing species/junk through FishingSeedData breaks the count asserts in FishingContentTests and ContentAssetParityTests (T3 sets the counts to 1); the parity test's "every seed species has a model" rule contradicts C 2.8 #28. Needs T10 edit rows for those two files.
- CR-3: C 7.10 vs C 2.6 disagree on who creates species/junk assets. FishingContentBuilder stays the only creator; EcologyContentBuilder only checks the assets exist.
- CR-4: the harbour spot registration is gated until SceneNames.BuildOrder contains Spot_HarbourPier (it wires itself at T17).
- CR-5: V5 cycle times: T12 measures Rhythm-bot fight medians on all 4 rods itself. Cycle = cast overhead + bite wait + fight + 10 s walking.
- CR-6 (SPEC DELTA): the 13.3 rule "a species eligible in exactly one band" fails for every spot/season (all rows have 2+ bands). Proposed: "at most two adjacent bands" (Night and Dawn not adjacent).
- CR-7: V4/V5 use neutral bait; "eligible" means rod 3, bait 10, depth 6 m; "differs by >= 2" is the symmetric difference of Morning vs Night; specials are excluded from V2/13.3/V5 and included in V3/V4; best-for-coins ties go to the lower sub-area index.
- Field naming: serializable helpers use public camelCase fields (band.id), shared with sections E, F and G.

## Section B (docs/superpowers/plans/2026-10-13-m3-plan-core.md, untracked, 1,322 lines)
- CR-B1: add Tests/EditMode/Ecology/EcologyCoreTestData.cs to the C 12 T5 row (a helper T6-T8 read).
- CR-B2: add T5 to the C 2.6 row for Core/Fishing/BaitStats.cs (Mods).
- Depends on CCR-D1 being accepted (or EditMode breaks after T13).
- Answered D's questions (catch-up in one call, a null grid never spawns, no allocation, the season is rolled).
- User-level open questions (default keep): sunrise fade-in of about 18 s at minute 0; a dev-forced species ignores catch limits.

## Section A (docs/superpowers/plans/2026-10-13-m3-plan-carry-migration.md)
- CCR-A1: F6/F7 debug toggles reset on INetworkSession.Ended only (not on ServiceRoots.Game teardown, which fires on every Spot load). Wording.
- CCR-A2 (blocks T2): T2 adds InternalsVisibleTo("FishGame.Tests.EditMode") to Gameplay/AssemblyInfo.cs; moves that line from T14's row to T2's.
- CCR-A3 (blocks T2): test seam AppBootstrap.OnlineSessionFactoryOverride (public static, null in production).
- CCR-A4: Truncate stays a char-count helper inside the byte loop; SessionText.Name now drops lone surrogates (recorded behaviour change).
- CCR-A5: add to the manifests the test files T1 and T3 must edit.
- CCR-A6: minLandDepth 0.5 is a spec value change (pending the user's Q1; default applies), not a TunedDeviations entry.
- CCR-A7: new Configure parameters optional and trailing; bait multipliers default 1 with = 1f initializers.
- Defaults: A-Q2 the three shader cases T1 names; A-Q3 keep M1 behaviour (a late cancel loses the race and the game loads).

## CONTROLLER RULINGS (2026-10-12)
ACCEPT ALL: CCR-D1..D5, CR-1..CR-7, CR-B1, CR-B2, CCR-A1..A7, as proposed by their authors (the primary options, not the fallbacks).
- CR-1 is applied in the R72 F4 brief (the fight builder); the contract records it as done by R72 F4.
- CR-6 spec delta: spec 13.3 "a species eligible in exactly one band" becomes "a species eligible in at most two adjacent bands (Night and Dawn are not adjacent)". The controller applies it to the spec AFTER the fight builder's F0 lands (the same file). The contract records it as pending, with the exact text.
- B open questions: keep the defaults (the 18 s sunrise fade-in; dev-forced species ignore catch limits).

## STATUS
- Contract amended and committed in 1934701 (rows M3-D55..D77).
- TODO when the M3 branch is cut: apply the pending spec deltas M3-D65 (13.3 text), M3-D66 (12.9/13.3 definitions) and M3-D74 (9.3/9.12 minLandDepth 0.5, unless the user picks the lake shelf), plus the user's fixed-seed answer.

## Section F (docs/superpowers/plans/2026-10-13-m3-plan-presentation.md, uncommitted, 406 lines)
- CCR-F1: M3-D23 / 7.18: under R74 the palette swap IS the tint on textured albedo (env albedo = palette colour x low-chroma detail). T18 adds no shader term; _FGSeasonTint is pushed as a dormant hook. Option B (an opt-in _FGSeasonTintWeight in Toon.shader) is rejected. RULING: accept the primary option.
- CCR-F2: the 4 s weather cross-fade and the light/fog parts of the weather look move from WeatherFx to DayNightController. RULING: accept.
- CCR-F3: T18 edits PresentationWorldTests PresentationWorld_Palette_IsLinearAndAppliedOnce (pin minute 300, Clear, M2Season). RULING: accept.
- CCR-F4 (conditional): if day-1 segment 0 of the fixed dev seed is Rain, FeedbackSoloTests' FindLoop skips Loop_Rain_Patter. RULING: accept as conditional.
- Open questions: keep the defaults (rain 450/s in a 24 m box; M2 night data with moon 0.25; the 18 s sunrise fade).

## Section E (docs/superpowers/plans/2026-10-13-m3-plan-maps.md, uncommitted, 549 lines)
- E-CCR-1: internal WorldSceneObjects.FinalizeAfterSave(scene, path) plus one more T16 hook line in LakeDockBuilder after SaveScene (runs OnValidate by reflection, SetDirty, saves again), for the WorldState hash. RULING: accept.
- E-CCR-2: MapChecks reachability walks registry.Spots (keeps build green T16-T17; mirrors CR-4). RULING: accept.
- E-CCR-3: add Tests/PlayMode/EcologyMapPlayTests.cs to the T16 and T17 file lists. RULING: accept.
- E-CCR-4: T17 edits SceneNamesTests SceneNames_BuildOrder_BootstrapFirst (adds Spot_HarbourPier). RULING: accept.
- E-CCR-5: BakeAllMenu opens Spot scenes singly, not additively. RULING: accept (behaviour note).
- Open questions: keep the defaults (harbour play-through at T22/T25; invisible boundary walls and a 240 m water visual; the small layout deviations).
- Risk to raise with the user if it bites: the lake reeds' castable ring is ~1.4 m wide; if the bake gives < 25 cells, the lake-shelf alternative is the user's call.

## Section G (docs/superpowers/plans/2026-10-13-m3-plan-journal-ui.md, uncommitted, 864 lines)
- G-CR1: PlayerInputFrame.JournalPressed added in T21 (required for the router test). RULING: accept.
- G-CR2: T20 adds the three toast.journal_* keys to UiStrings. RULING: accept.
- G-CR3: IDevInput.IsHeld(DevKey) for "F8 held". RULING: accept the primary option.
- G-CR4: manifest entries: T20 JournalRecorderTests.cs; T21 internal WorldGlyphs.cs and the modal seam. RULING: accept.
- G-CR5: T23 owns T_FishSkinArray.png and the fish row of TextureArrayPostprocessor; the sheet goes to 4x8 (2048x4096); the size cap rises to 4096 for that array only; T4 tests pinning 4x2 are updated. NOTE: R74 adoption (d660ee9) already put the fish skins in layers 0-5 (bluegill 1, perch/carp/trout/catfish 2-5), so the M3 species layers continue from 6. RULING: accept.
- Open questions: keep the defaults (level-1 sub-areas per spot seen; T21's icon recipe with a user check; gamepad B closes the journal; batches 5/4/6/5).

## E-G STATUS: all three written. Apply the E/F/G CCRs to the contract in one Sonnet docs pass, then commit the plans.

## QUEUED (2026-10-13): seasonal trees, user chose "Full seasons". Dispatch AFTER the R74 review-fix agent finishes (same builder files).
- Pines evergreen: their own palette slots (use Reserved12/13 as PineLight/PineDark, the same in every season), snow on the top-facing tier verts via a "PineSnow" slot = the pine colour except winter = white.
- Round trees: spring lighter green + blossom specks (blossom-slot geometry; the slot equals the foliage colour outside spring, so it's invisible); summer deep green; autumn mixed orange/red/yellow via per-tree slot choice (Foliage vs a FoliageAlt slot, seeded); winter bare: a SeasonalVisibility component swaps the canopy for a bare-branch mesh on season change (PaletteGlobals season).
- Ground snow already exists (grass slot white in winter).
- Note for M3 T18: PaletteGlobals season change must notify SeasonalVisibility; the harbour uses the same tree recipe.
- Update the spec (R74 note + 10.x) and the M3 contract (T18 / M3-D78 area).

## QUEUED M3 FIX ROUND (after T8 finishes; Opus review of T0-T3 = APPROVED WITH FIXES)
1. UiSessionActions.cs:112: cancel losing the race to Connected (A-Q3, default: the attempt succeeds and the game loads). In finally: if (_cancelPending && _attempt == cts) { if (succeeded) StopCancelWatch() [clear the flag, unsubscribe OnStateAfterCancel; keep Busy, optional busy.loading label]; else ReturnToMenuAfterCancel(); }. Add a PlayMode case: the fake reaches Connected and succeeds, Cancel is pressed before the continuation runs, and the screen is never MainMenu.
2. UiFlowCancelTests.cs:176-179: fix the "Catches:" wording; re-run the mutation as subscribe-after-Cancel + no post-check, and report exactly what was mutated.
3. Spec deltas not applied by T0: M3-D2 (12.2 Rarity -> FishRarity), D3 (9.7 the bare hook can catch any species; also annotate the 17.2 M2 test line), D6 (12.5 reachability [4.9, 16.4] m, castable cells only), D19 (11.4 no Day N/7 in M3), D53 (12.4 IWorldContext adds CastingOpen, HasState, SpotId, Changed). Add them to R75's delta list or a sibling row. Use ASCII ">=" (fix 13.3's "≥").
NOTES FOR LATER BRIEFS:
- T13: keep the FishingHostLedgerTests alias `using Is = UnityEngine.TestTools.Constraints.Is;` plus the Is.Not.AllocatingGCMemory() probe and the Is.AllocatingGCMemory() positive control (GC.GetAllocatedBytesForCurrentThread is inert on Mono). Edit the Configure call in EcologyMigration (M3-D55).
- T10: flip the species-count asserts (M3-D61); restore the FishFallbackMeshTests shape check with a Long species; the bluegill row must equal T3's exactly (200-600 g, value 8, biteWaitMod 1.0, Schooling, gates 0/0, M2 colours, Fish_Bluegill.fbx).
- T23: ContentAssetParityTests.cs:79's uv1 map must gain each new model with its layer (6-25).
- M3 playtest list: online cancel on UGS shows "Cancelling..." for about one frame, then the menu with greyed buttons for up to 30 s.

## Sonnet review of T5-T8: APPROVED WITH FIXES (nothing blocks). Notes for later briefs:
- T13 (first real context builder): add resolver tests pinning Events.RareChanceMult (FishingResolver.cs:36), Events.WeightFor and Skills.RarityMult (:152), plus the B-D4/B-D6 sanitisers (NaN/±5 luck == 0/±1; +Inf depth == 6 in FishingRoller.cs:45). Every host-built context sets Events = EventMods.Neutral and Skills = SkillMods.Neutral (Roll throws on invalid values even on the forced paths). Optional: an internal NonZero(uint) seam for FishingRoller_FightSeed_NeverZero (currently can't fail).
- T14: harden HotspotPlanner.Advance (:37): clamp the minute to DayLengthMinutes + a margin or cap the iterations (int.MaxValue would spin and overflow).
- Any time: ShoreGridMathTests:78-81 should make cell 0 castable so the IsWater filter is exercised (expected [3,3,2]); EcologyTypesTests:174-182 comma spacing nit.
- Sent to the T9-T12 builder: ToCore no defaults; weather tables all positive; rules sorted; the T11 validators reject non-positive junk weights and unsorted rules; call Fishing() with the class name.

## Sonnet review of T9-T12: APPROVED WITH FIXES (nothing blocks T13-T16)
- #1 spec 12.6 retune cells (mirror_minnow 10->4, red_snapper 12->18): ALREADY in the T13 builder brief.
- BEFORE T17 (harbour registration): #7 ContentRegistry.Ecology BuildEcology must throw InvalidOperationException naming the spot or tuning asset (a missing zone/tuning currently gives a bare NRE); #6 replace the "Spot_HarbourPier" literal in EcologyContentBuilder with SceneNames.SpotHarbourPier (T17 adds it), or add an equality test.
- BEFORE T25: #3 make EcologyBalanceReports MeanValue/EconomyInputs/Economy private; #4 a one-line contract note on the test files touched (EcologyMigrationTests, FishFallbackMeshTests, FishingHostLedgerTests, FishingDefsTests, ContentAssetParityTests); #5 move the OnlyExistingAssets GUID guard into a shared BuilderUtil helper used by FishingContentBuilder.cs:153 too; #9 assert that a Cancel press after the race leaves the session (LeaveSession); optional #8: extract the Targets helper.
- HANDOFF NOTE (#2): the two retunes sit near a V5 cliff (minnow Sw 5 still fails; snapper 15 fails). Any later R72 fight retune can flip V5 red, so re-run EcologyBalanceReports after any fight tuning (the friendlier pass!).

## Opus review of T13/T14/T16: APPROVED WITH FIXES (nothing blocks the MPPM check)
- F8 RESOLVED: the implementer's report had the grid SHA 52B0A39C...53FD identical across both builds, and the hash experiment (0 after AddTo, 0 on the saved scene before refresh, non-zero after FinalizeAfterSave; different per build).
- F3 RULING: the HotspotPlanner.Advance clamp (T7's file) in T14 was a controller-authorised edit (brief); record it in contract 2.6. Add Tests/PlayMode/PlayerHooksTests.cs to T15's manifest (re-point from FishingHost.InterimContext to the world-built context). List T13's test edits (four T3/T9/T11 test files) in C 2.6/12.
- T15 BRIEF MUST INCLUDE: F2, the D-F6 OnDisable guard releasing reservations, landing in the same change as the EcologySession GetOrCreate switch, with a test (Reserved == 0 after a reload while a reservation is held) as an acceptance item. Plus the PlayerHooksTests re-point.
- T17 BRIEF MUST INCLUDE: F1, the bake order SaveScene -> FinalizeAfterSave -> Physics.SyncTransforms -> BakeOpenScene; amend contract 2.6 LakeDockBuilder row + 7.16 + maps plan T16.3/T17 to match. Plus the earlier T9-T12 items #6 (SceneNames literal) and #7 (BuildEcology clear errors).
- ANY TIME: F4 WorldState second instance should return after the error; F5 PlayMode asserts NetSeason==Run.Season, NetWeather==driver.Weather.At(day,0), NetClock.Running==1; F6 EcologyMapTests:68 comment -> "Shore removed"; F7 DevWorldDriver:43 SetPaused(Now,..).

## QUEUED: T23 import (after the cleanup round releases the m3 Unity)
- Copy Art/Export/T_FishSkinArray_m3.png over Assets/_Project/Content/Textures/T_FishSkinArray.png (keep the .meta). TextureArrayPostprocessor: fish array 4x8, maxTextureSize 4096 for that array only, version bump. SmoothMeshBuilderTests skin depth 8 -> 32. Fix the stale header comment if still present.
- Copy the 20 new FBX (Art/Export/Fish_*.fbx, layers 6-25, table in the artist's report) into Assets/_Project/Content/Models (new .meta generated by Unity; the FishModelPostprocessor pins uv1). Copy the 4 re-baked reused FBX (Perch/Carp/Trout/Catfish) over the Content copies, KEEPING their .meta/GUIDs.
- ContentAssetParityTests uv1 map: add all 20 models with layers 6-25.
- Build (the model fill picks up the roster `model` names), validators (one submesh, uv1 layer, triangle budget 5000), in-game lineup render check (catch reveal per species if cheap).
- .blend is Art/Blender/fishgame_m3_fish.blend (not the plan's fishgame_m3_species.blend).
- The mullet stays as is pending the user's icon answer.
- Flat fish (flounder, ray) are side-flattened discs, like FishFallbackMesh Flat; the moon_squid's arms point toward +Z.

## Opus review of T18/T19: APPROVED WITH FIXES (fixes 1-3 + the FeedbackSoloTests hardening sent to the cleanup builder)
- T15 BRIEF ADD: FishingWorldTests pin the world (clock stopped, weather and season set) like DayNightTests (sessions start in Autumn Fog); a natural hotspot appears 45-90 s in and takes 2-3 ripple slots (watch long HasRipple checks).
- T17 BRIEF ADD: roots named exactly Sun (Light), WarmVolume and CoolVolume (Volume) created BEFORE PresentationSceneObjects.AddTo (else it throws); an Ambience root with AmbienceBed; fog on, ExponentialSquared, via RenderingSetupBuilder; a Camera.main for WeatherFx; FinalizeAfterSave after the save; Spot_HarbourPier.unity committed whole (the lake scene too if it churns).
- RECORD FOR T22/T25/M4: the day wrap at minute 1200 pops (moon 0.25->0, Night->Dawn, cool->warm, fog 0.014->0.01); dev-only in M3. A join "snap" is really up to 3 snaps within ~0.5 s (light/fog, then palette and trees after <=0.25 s, then wind); acceptable behind the load.
- USER NOTE: default M3 solo = Dawn, Autumn, Fog for ~6 min, Autumn wind. The same every session (fixed seed).

## USER PLAN (2026-10-13), after M3 completes
1. A checks handoff (manual + multiplayer, M2 rework + M2 acceptance + M3 acceptance, in one ordered list) INCLUDING a new Artifact notes board: db-backed (persistent, never localStorage), where the user writes PASS/FAIL + notes per check, read via ArtifactData.
2. All fixes from those checks.
3. Then the M4 handoff (R76 river run, starfish god camp, boat ride, mafia, escape reward, cutscenes; the quota calibration note).

## M2 MERGED TO MAIN 2026-10-13: 235d460 (PR #1, merge commit). Pre-merge check on c0447af: build PASS, EditMode 976+1 skip, PlayMode 83/83. Seasonal trees review: SAFE TO MERGE.
QUEUED follow-ups (do on m3-fish-ecology after the fish import):
- ZERO NORMALS root fix in SmoothMeshBuilder.Radial (Runtime/World/SmoothMeshBuilder.cs:169-174): c = Cross(p1,p2); n = c.magnitude > 1e-5f ? c.normalized : Cross(p1.normalized, p2.normalized).normalized; drop the 100x hack in LakeDockMeshes.BuildBlossoms; EditMode test asserting unit normals on the built Bobber (PlayerFishingRigBuilder.cs:217, r 0.008) and bell knob (PrefabBuilder.cs:233, r 0.07) meshes. Existing larger meshes must stay byte-identical.
- SeasonalTreesTests SeasonalVisibility_FollowsThePaletteGlobalsPush TearDown: restore the Summer palette globals.
- An automated collider-count guard on the lake scene (22 box, 44 sphere, 30 capsule, 2 mesh), or a hash.
- docs/superpowers/plans/2026-10-11-m2-plan-data-tuning.md:155-163: add a "superseded by spec 10.2 (2026-10-13)" note.

## T17-B RULING (user, 2026-10-14): the harbour's art is made in Blender
The user asked for the map to be modelled in Blender instead of code-generated meshes. Free addons/assets are allowed once verified safe (record source + licence). Split:
- **Blender (phase 1, branch `m3-harbour-art`):** every visual set-dressing mesh: pier deck, T-head, ramp, posts, ladders, breakwater boulders + top path, rocks, hut, bollards, crates, barrels, lamp posts, plus extra harbour dressing (moored boats, buoys, nets, lobster pots, rope coils, signs) within budget. A committed Python generator `Art/Blender/fg_harbour.py` (also a text block in `Art/Blender/fishgame_harbour.blend`) rebuilds them from text. FBX in `Art/Export/Harbour/`, later copied to `Assets/_Project/Content/Models/Harbour/` (FishModelPostprocessor settings apply to the subfolder; the fish parity tests list files by name, so they are unaffected).
- **Encoding (same as the fish):** a second UV map "Layer" = (palette slot, texture layer) on every vertex; vertex colour = 0.5 x baked AO (half-scale grey); no materials exported; one mesh per FBX object; mapping is per material: objects whose name ends `_Tri` (rocks, boulders) get `M_Toon_Tri`, the rest `M_Toon_Base`. Slots: Grass 0, Foliage 1, FoliageDark 2, Wood 3, WoodDark 4, Sand 5, Rock 6, Reed 7, Dock 8, Roof 9, Accent 10. Layers: Grass 1, Foliage 2, Wood 3, Bark 4, Sand 5, Rock 6, Cloth 7.
- **Code stays (phase 2, T17 proper, after T15 merges):** `HarbourPierLayout` numbers, seabed and beach height fields (exact `SeabedY`/`BeachY`; the bake, analytic test and `CastProbe` need them), water, every collider, hooks, stand points, bake. `HarbourPierMeshes` shrinks to the code meshes (water, seabed, beach); `HarbourPier_Rebuild_ByteIdenticalMeshes` covers only those. The builder places the FBX visuals at the layout positions.
- **Replaces T17.3 rules** "no new texture / no LFS growth / SmoothMeshBuilder everywhere" for the Blender meshes (FBX are LFS; report the size). New checks for the FBX: an EditMode bounds test against `HarbourPierLayout` (deck top 1.5, deck and T-head footprints, ladder rails inside their rail boxes, breakwater top 1.5, posts from TerrainY to the deck underside), uv1 on every vertex with a defined layer, vertex colours, <= 21,000 triangles per mesh, no imported materials. Budget: harbour total near the lake's triangle count (user's GTX 900-class target, S3).
- The Blender script mirrors the T17.2 table by hand, with a comment pointing at it; the bounds test is what keeps the two in step.
- The user's look check uses Unity screenshots after phase 2 (Blender renders don't use the toon shader).
