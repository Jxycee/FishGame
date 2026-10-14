// Shaders/FishGameCommon.hlsl - FishGame shared globals and helpers (C 5.3, 7.20; spec 10.2-10.4). Owned by T20. ASCII only.
// Include AFTER "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl".
// Colour convention (Linear project): every colour reaching these helpers is linear. _FGPalette is pushed already
// linearised by PaletteGlobals (SetGlobalVectorArray does not convert); _ToonShade/_ToonAmbient/_Sky* come from
// SetGlobalColor (Unity converts); vertex colours are used as stored, except that a material with _JitterEncoded = 1
// (every MeshBuilder-built mesh) doubles them: MeshBuilder stores a half-scale grey (ruling BF-7). The Blender fish
// models (M_Toon_Fish, _JitterEncoded = 0) are full-scale linear albedo.
#ifndef FISHGAME_COMMON_INCLUDED
#define FISHGAME_COMMON_INCLUDED

float4 _FGPalette[16];   // palette slot colours (rgb linear)
float4 _Ripples[8];      // (x, z, spawnWaterTime, strength); strength 0 = empty slot
float4 _Waves[4];        // (directionDeg, amplitude, wavelength, speed); [3] is always zero
float  _WaterTime;       // (float)WaterSurface.Now, pushed by WaterTimeDriver
half4  _ToonShade;
half4  _ToonAmbient;
half4  _SkyTop;
half4  _SkyHorizon;
half4  _SkyGround;
// Toon colour grade (asset study 2026-10-14; ArtPaletteDef.ToonGradeFor(season), pushed by PaletteGlobals.PushToonGrade). GAINS, not colours:
// pushed raw with SetGlobalVector (no sRGB conversion), 1 = neutral; w is the strength. Never pushed (all zero) = no change anywhere.
half4  _FGTopTint;       // xyz albedo gain on upward faces, w strength
half4  _FGBottomTint;    // xyz gain on the shade-side light (cool blue-purple), w strength
half4  _FGGradeParams;   // x top tightness (n.y where the top tint starts), y per-object hue shift (+/- turns), z shade weight

half3 FG_PaletteColor(float slot)
{
    int i = clamp((int)round(slot), 0, 15);
    return (half3)_FGPalette[i].rgb;
}

// Mirrors FishGame.Core.WaveMath (T12) term for term: 0 deg = +Z, dir = (sin th, cos th) on (x, z), k = 2 pi / L,
// term = A * sin(k * (dot(dir, xz) - S * t)); L <= 0, A == 0 or ANY non-finite field (NaN, +/-Inf) -> 0 (T12 follow-up, C 16.2:
// WaveMath treats a non-finite field as a zero term; without this guard CPU and GPU heights diverge). The guard is a bit test on the
// exponent field (all ones for NaN and +/-Inf), NOT isfinite/isnan/isinf: the shader compiler's fast-math folds those away, which the GPU parity
// test (WaveGpuParityTests, graphics run) caught. Change WaveMath and this together, never one.
float FG_WaveTerm(float4 w, float2 xz, float t)
{
    if (any((asuint(w) & 0x7F800000u) == 0x7F800000u)) return 0.0;
    if (w.z <= 0.0 || w.y == 0.0) return 0.0;
    float th = radians(w.x);
    float k = 6.28318530718 / w.z;
    return w.y * sin(k * (dot(float2(sin(th), cos(th)), xz) - w.w * t));
}

float FG_WaveHeight(float2 xz, float t)
{
    return FG_WaveTerm(_Waves[0], xz, t) + FG_WaveTerm(_Waves[1], xz, t) + FG_WaveTerm(_Waves[2], xz, t);
}

// Mirrors RippleMath.Ring (T12): (1 - sat(|d - age * 2.2| / 0.25)) * sat(1 - age / 2.5) * strength; age < 0 -> 0.
float FG_RippleSum(float2 xz, float t)
{
    float sum = 0.0;
    [unroll] for (int i = 0; i < 8; i++)
    {
        float4 r = _Ripples[i];
        float age = t - r.z;
        float ring = (1.0 - saturate(abs(distance(xz, r.xy) - age * 2.2) / 0.25)) * saturate(1.0 - age / 2.5) * r.w;
        sum += step(0.0, age) * ring;
    }
    return sum;
}

float FG_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Per-object hue-shift input in [-1, 1]: a smooth sum of sines of the object origin's world xz (about 3-5 m periods), so neighbouring props differ
// while a carried or bobbing object only drifts slowly (never pops), and vertical bobbing never changes it (y is left out). Needs no per-object
// material instance (SRP Batcher safe: the object matrix is per draw). Merged meshes share their object's value.
half FG_ObjectHueNoise()
{
    float2 o = float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m23);
    return (half)(sin(dot(o, float2(1.37, 0.71))) * 0.55 + sin(dot(o, float2(-0.53, 1.91)) + 1.7) * 0.45);
}

// Hue rotation by `turns` of the colour wheel around the grey axis (Rodrigues), luma-neutral to first order; cheaper than an HSV round trip.
half3 FG_HueRotate(half3 c, half turns)
{
    half s, co;
    sincos(turns * 6.2831853h, s, co);
    const half3 k = half3(0.57735h, 0.57735h, 0.57735h);
    return max(c * co + cross(k, c) * s + k * (dot(k, c) * (1.0h - co)), 0.0h);
}

// One fog path for every FishGame shader (URP built-in fog, spec 10.3).
half FG_FogFactor(float clipZ) { return ComputeFogFactor(clipZ); }
half3 FG_ApplyFog(half3 color, half fogFactor) { return MixFog(color, fogFactor); }

#endif
