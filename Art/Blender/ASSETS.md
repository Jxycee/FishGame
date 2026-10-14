# Art sources and licences

Every asset under `Art/` and the textures under `Assets/_Project/Content/Textures/` are made for FishGame by the project's own scripts. No downloaded, bought or third-party asset is used yet. When a free asset is vendored (Poly Haven CC0, an official Blender extension, a Unity package), add a row here with its source URL and licence.

| Asset | Made by | Source | Licence |
|---|---|---|---|
| Harbour Pier meshes (`Art/Export/Harbour/*.fbx`, manifest, ground cover, bed map) | `Art/Blender/fg_harbour.py` (procedural, deterministic) | this repo | project-owned |
| Crown-card atlas `T_CrownCards.png` | `Art/Blender/fg_crowncards.py` | this repo | project-owned |
| Env detail array `T_EnvDetailArray.png`, layers 0-8 | look-dev bake: `Art/Blender/fishgame_lookdev.blend`, text block `fg_textures.py` | this repo | project-owned |
| Env detail array layers 9-14 (cliff rock, stone wall, plaster, dirt, meadow, roof tile) | `Art/Blender/fg_envtextures.py` (numpy, deterministic; quality-first round 1, 2026-10-14) | this repo | project-owned |
