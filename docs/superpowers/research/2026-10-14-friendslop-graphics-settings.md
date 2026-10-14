# Friend-slop graphics settings and minimum specs (2026-10-14)

Purpose: set FishGame's quality presets and the per-view budget from what comparable games ship, not from guesswork. Study only.

Sources:
- **Published specs:** Steam store pages and aggregators (links at the end).
- **Shipped settings:** read with AssetRipper from the user's installed copies of REPO, PEAK and Gamble With Your Friends (QualitySettings plus the URP pipeline assets). Values only; no assets copied.
- **RV There Yet:** an Unreal game (install folder `Ride`), so its settings are not readable this way.

## Minimum and recommended GPUs
| Game | Minimum GPU | Recommended GPU |
|---|---|---|
| R.E.P.O. | GTX 970 | GTX 1070 |
| RV There Yet | GTX 970 | RTX 3060 |
| Gamble With Your Friends | GTX 970 | GTX 1070 |
| Lethal Company | GTX 1050 | (none listed) |
| Content Warning | GTX 1050 Ti / R9 380 | GTX 1060 / RX 470 |
| PEAK | GTX 1060 | RTX 2060 |

The typical minimum is a **GTX 970 / GTX 1050** class card, which confirms FishGame's target: a GTX 900-series card at Low-Medium.

## What they ship
**REPO** (built-in render pipeline, six quality levels):

| Level | Shadows | Distance | Cascades | Pixel lights | LOD bias | AA |
|---|---|---|---|---|---|---|
| Very Low | off | 15 m | 1 | 0 | 0.3 | off (half-resolution textures) |
| Low | off | 20 m | 1 | 0 | 0.4 | off |
| Medium | hard | 20 m | 1 | 1 | 0.7 | off |
| High | soft | 40 m | 2 | 2 | 1.0 | off |
| Very High | soft | 70 m | 2 | 3 | 1.5 | 2x MSAA |

**Gamble With Your Friends** (URP, one pipeline asset per level):

| Level | Shadow distance | Shadow map | Cascades | MSAA | Extra lights per object | HDR | LOD bias |
|---|---|---|---|---|---|---|---|
| Low | 50 m | 1024 | 1 | off | 3 (no shadows) | off | 2 |
| Medium | 100 m | 2048 | 2 | 2x | 6 | on | 2 |
| High | 200 m | 4096 | 4 | 4x | 8 | off | 2 |

Soft shadows are on at every level, and render scale is 100% everywhere.

**PEAK** (URP, one quality level, scaled at runtime from the settings menu):
- **Pipeline asset:** render scale 0.8 with an upscaling filter, so it does not render at native resolution by default. Shadow distance 200 m, 2048 shadow map, 2 cascades, soft shadows, MSAA off, 4 extra lights per object, HDR on, and the depth and opaque textures on (for the water and fog effects).
- **Menu:** render scale, shadow distance, "world quality", ambient occlusion on/off, texture quality (added later for low-VRAM cards), V-sync and a frame cap. Community low-end advice for PEAK is 70% render scale, low shadow distance, low world quality and AO off.

## Averages, compared with FishGame today
| Setting | Their Low (average) | FishGame Low | Their Medium | FishGame Medium | Their High | FishGame High |
|---|---|---|---|---|---|---|
| Render scale | 75-80% (PEAK upscales) | 75% | 100% | 100% | 80-100% | 100% |
| Shadows | hard, 20-50 m, 1 cascade, 1024 | 25 m, 1024, 1 cascade | 20-100 m, 2048, 1-2 cascades | 25 m, 1024, 1 cascade | 40-200 m, 2048-4096, 2-4 cascades | 70 m, 2048, 2 cascades |
| AA | off | FXAA | 2x MSAA | 2x MSAA | 4x MSAA | 4x MSAA |
| Extra lights per object | 0-3 | URP default | 1-6 | URP default | 2-8 | URP default |
| LOD bias | 0.4 (REPO) | no LODs yet | 0.7 | no LODs yet | 1.0-1.5 | no LODs yet |

Low and High are already close to the average. Medium is below it: it reuses the Low shadows.

## Changes to make (following the average)
1. **Medium shadows:** a new middle tier at 50 m, 2048, 2 cascades.
2. **Low shadows:** 25 m to 35 m, hard shadows only. This sits between REPO's 20 m and GWYF's 50 m, and fits a harbour-sized map.
3. **High shadows:** 70 m to 120 m, 2048, 3 cascades. The map is about 150 m across, so 70 m shadows pop in the overview.
4. **Extra lights per object:** 3 / 4 / 8 for Low / Medium / High, with no shadows from extra lights on Low or Medium. This matters now that the harbour has dusk lamps.
5. **Upscaling on Low:** FSR 1 when render scale is below 100%, which is cheap and sharper, as PEAK does.
6. **LODs:** add LODGroups to hero meshes, with LOD bias 0.7 / 1.0 / 1.5 per preset (REPO's curve).
7. **Budget per view:** no game publishes a triangle count, and a GTX 970 manages a few million triangles per frame. The real costs are shadow passes, draw calls and the overdraw from alpha-tested foliage. So the Low budget goes from 350k to **500k triangles**, with captures also tracking renderer count, shadow casters and alpha coverage. PEAK's hero rocks are 3-4k triangles at LOD0 with 3 cheaper LODs.

Items 1-6 are a small presets change (GraphicsPresets plus GraphicsApplier, with tests). Item 7 goes into the harbour polish brief from round 3.

## Sources
- REPO specs: [systemrequirementslab](https://www.systemrequirementslab.com/cyri/requirements/repo/26063), [pcgamebenchmark](https://www.pcgamebenchmark.com/r-e-p-o-system-requirements)
- PEAK specs and settings: [pcgamesn](https://www.pcgamesn.com/peak/system-requirements), [peak.wiki.gg settings](https://peak.wiki.gg/wiki/Setting), [gamerblurb graphics guide](https://gamerblurb.com/articles/peak-game-best-graphics-settings-guide)
- Lethal Company: [gameskinny](https://www.gameskinny.com/tips/lethal-company-recommended-and-minimum-pc-requirements-explained/)
- Content Warning: [Steam](https://store.steampowered.com/app/2881650/?l=english)
- RV There Yet: [Steam](https://store.steampowered.com/app/3949040/RV_There_Yet/), [sportskeeda](https://www.sportskeeda.com/esports/rv-there-yet-system-requirements-minimum-recommended-pc-specifications-explored)
- Gamble With Your Friends: [systemrequirementslab](https://systemrequirementslab.com/requirements/gamble-with-your-friends/29886)
