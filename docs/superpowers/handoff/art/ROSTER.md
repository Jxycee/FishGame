# Harbour build: parallel cloud workers (started 2026-10-17 07:40 UTC)

Orchestrator: session_01H1ZV7xiNZCUURUzwXrnHEZ (branch design/open-world-pivot). All workers are Sonnet 5.5, Blender 5.2.2 headless, and follow `BRIEF.md`. Each one pushes its own branch and writes `docs/superpowers/handoff/art/<branch>.md` there.

| Branch | Base | Session | Scope | Script |
|---|---|---|---|---|
| harbour-village-states | harbour-art3 | session_01QbbRTxSSu3SowSUk3SqNUN | run-down / scaffold / restored landmarks, river lock | fg_harbour.py (sole owner) |
| harbour-village-buildings | harbour-art3 | session_01BgQTeRpmUPhSfD29neqQJE | placeable cottages, café, aquarium, tanks, 8 village lots | fg_village_buildings.py |
| harbour-berth-kit | harbour-art3 | session_01SR579bqwouWfXKD3N4wQSt | stand kit, crew berth kit, tacky decor, berth site | fg_berthkit.py |
| harbour-villagers | character-fisher | session_01Stb5W9WYfMYEDTAVDhzAwd | the 10 villagers (static, riggable) | fg_villagers.py |
| harbour-props | harbour-art3 | session_013cHcMRdFHULiGcjZmBWCNv | belongings, collectibles, technique gear | fg_props.py |
| harbour-tug-upgrades | harbour-art3 | session_01BvVXsKMToty34Pvph2g8Kp | tug upgrade modules | fg_tugmods.py |
| fish-genome | main | session_01P8P8Nq9zL8jmFWixBz3jrC | fish blendshapes, pattern masks, morph skins, shader contract | (fish blends) |
| harbour-waterlife | harbour-art3 | session_01KZdjyriFtqQGH4jd82i5Wo | bait balls, birds, kelp, schools, the tide set | fg_waterlife.py |

**Not reachable from the cloud:** the Unity Asset Store packs (they are on the user's PC and not imported). The workers note where a pack would fit.
**Free sources used:** Poly Haven API and ambientCG (both CC0), recorded per area in `Art/Textures/<Area>/SOURCES.md`.
**Unity integration** (PC, after harbour-art3 merges): each worker's report lists its steps. The village-states branch cancels nothing in Unity: the R79 boundary move is cancelled separately (design 5.5).

## Palette slots (coordination, 2026-10-17)
- **Existing:** fg_harbour.py uses slots 0-15.
- **harbour-props:** owns 16-31: Brass 16, Bronze 17, Steel 18, Iron 19, GlassGreen 20, GlassBrown 21, GlassBlue 22, GlassClear 23, Gold 24, Paper 25, ShellPink 26, ShellCream 27, Ice 28, Khaki 29, White 30, Navy 31 (`fg_props.py:41`).
- **New slots by range:** village-states 32-39, village-buildings 40-47, berth-kit 48-55, tug-upgrades 56-63, waterlife 64-71, villagers 72-79.
- **On the PC:** the Unity palette texture and lookup must be extended to cover every slot in use.

## Status
- **fish-genome:** done, 5a24fa0.
- **harbour-props:** done, 8e2a59c; a small redo of 4 weak models was requested.
- **harbour-berth-kit:** done. It adds slot 48 FlamingoPink. The tug's beam is 5 m. The berth's deep-water spot is the pontoon edge; the jetty perk goes to the camp plots.
- **harbour-village-buildings:** done, af6ad44. 8 lots. Unity must suppress grass and hedges inside the lots.
- **harbour-villagers:** reworked to proxy bodies, outfits and props at 45d31e1. The first pass on the rejected fisher base is superseded. Slots 72-79 are tintable, so Unity needs a per-object tint (a MaterialPropertyBlock). The face briefs are in `harbour-villagers-face-briefs.md`.
- **harbour-village-states:** done, 9e8287b. 6 run-down overlays, scaffold kits, the river lock (broken and repaired; tug fit measured), and the lock-keeper's hut. The default state leaves every harbour export unchanged.
  - **Important for every branch:** Blender 5.2.2 on Linux does NOT reproduce the committed Harbour FBX bytes; 92 of 112 files are rewritten even when nothing changed. Cloud branches must not commit regenerated `Art/Export/Harbour` files. The PC regenerates them.
  - The follow-ups (stand-14 jetty, tug lettering, bow stripe) are requested as script-only commits.
