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
