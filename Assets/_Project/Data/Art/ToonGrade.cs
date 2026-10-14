using System;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). The toon colour grade of one season (asset study 2026-10-14, "The shared trick": PEAK's top / bottom
    // colours and per-object hue shift). PaletteGlobals pushes it as the _FGTopTint / _FGBottomTint / _FGGradeParams globals; FishGame/Toon
    // applies it on the materials with _FGGrade > 0 (M_Toon_Base, M_Toon_Tri).
    // The two tints are GAINS, not colours: linear-space multipliers on the albedo (top) and on the shaded side's light (bottom), 1 = neutral. They
    // are pushed as raw vectors (no sRGB conversion: a conversion would move the neutral point and over-boost gains above 1).
    [Serializable]
    public struct ToonGrade
    {
        public Vector3 topGain;          // albedo gain on upward faces (lighter, warmer): e.g. (1.16, 1.10, 0.90)
        [Range(0f, 1f)] public float topStrength;
        [Range(0f, 0.95f)] public float topTightness;   // n.y where the top tint starts (0 = every upward face, higher = only near-flat tops)
        public Vector3 bottomGain;       // gain on the shade-side light (cool blue-purple): e.g. (0.86, 0.84, 1.10)
        [Range(0f, 1f)] public float bottomStrength;
        [Range(0f, 1f)] public float shadeWeight;       // how much of the bottom tint a lit-from-the-side shadowed face gets (down faces get all of it)
        [Range(0f, 0.1f)] public float hueShift;        // per-object hue shift, +/- turns of the colour wheel (0.02 = +/- 7 degrees)

        // Used when a palette has no grade for a season (an asset built before the grades existed): the Summer seed.
        public static ToonGrade Default => new ToonGrade
        {
            topGain = new Vector3(1.14f, 1.09f, 0.92f),
            topStrength = 0.8f,
            topTightness = 0.35f,
            bottomGain = new Vector3(0.86f, 0.84f, 1.10f),
            bottomStrength = 0.7f,
            shadeWeight = 0.55f,
            hueShift = 0.018f,
        };

        // _FGTopTint: xyz gain, w strength. Strength 0 (and an unset global, all zero) leaves the albedo untouched.
        public Vector4 TopTintVector => new Vector4(topGain.x, topGain.y, topGain.z, topStrength);

        // _FGBottomTint: xyz gain, w strength.
        public Vector4 BottomTintVector => new Vector4(bottomGain.x, bottomGain.y, bottomGain.z, bottomStrength);

        // _FGGradeParams: x top tightness, y hue shift (turns), z shade weight, w unused.
        public Vector4 ParamsVector => new Vector4(topTightness, hueShift, shadeWeight, 0f);
    }
}
