---
paths:
  - "Art/**"
  - "Assets/_Project/Content/**"
  - "Assets/_Project/Shaders/**"
  - "Assets/_Project/Presentation/**"
  - "Assets/_Project/Editor/**"
---
# Art and assets

- No paid assets. Free assets are allowed once the source is checked as safe and trustworthy (user, 2026-10-14): official Blender extensions, Poly Haven CC0, Unity's own free packages, free Unity Asset Store items the user adds to their account, and trusted free asset APIs and sites for models, textures, HDRIs and sound such as the Poly Haven API (user, 2026-10-15). Check the licence allows use in a commercial game and record source + licence for each asset.
- Art direction (spec R74): smooth stylised "realistic toon" (references RV There Yet, Gamble With Your Friends): rounded models, soft toon lighting, painted-style textures made for the project (Blender, procedural or baked). The old low-poly flat-colour look is the M2 baseline being replaced.
- Maps are built out from the water (spec R79); review shots come from the stands, the tug deck, the camp and the arrival, never walk-throughs.
- Blender generators live in `Art/Blender/`; exports go through them so models are reproducible from text. `.blend`, FBX and PNG files are Git LFS.
