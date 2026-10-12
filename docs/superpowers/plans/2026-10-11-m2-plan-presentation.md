# FishGame M2 - Plan Section F: Presentation (T20, T21, T22, T23, T24)

- **Date:** 2026-10-11. **Branch:** `m2-fishing-core`.
- **Status (2026-10-12):** not started (T20 follows the T19-fix and the mini-fix batch). Each task header below carries a "Carry-forward (2026-10-12)" block: T21 (non-finite wave fields, palette push not per frame), T22.1, T23 (loops with `follow`, `StopLoop` in `OnDisable`, no cues during despawn), T24 (`TipAnchor` never moved, rest pose from the T19-fix). Contract C 16.2 lists them all.
- **Contract (binding, "C" below):** `docs/superpowers/plans/2026-10-11-m2-interfaces.md`. If this file disagrees with C, C wins; every doubt raised here is closed by a controller ruling, listed under "Contract questions" at the end.
- **Skeleton:** `docs/superpowers/plans/2026-10-11-m2-plan-skeleton.md` (rows T20-T24, section 6 row F, section 8 risk 7).
- **Spec (behaviour source):** `docs/superpowers/specs/2026-10-09-fishgame-design.md` 9.11 (feedback mapping), 10.1-10.5, 10.7, 3.2 (R62).
- **Sibling plans used:** `2026-10-11-m2-plan-core-support.md` (T12: wave direction, `MeshBuilder` UV channel, `ShaderGlobals` identifiers), `2026-10-11-m2-plan-data-tuning.md` (T14: `AssetPaths` constants, palette and water seeds).
- **Audience:** the Sonnet implementer of each task (reads section 0 plus its own task) and the Opus (high) reviewer.

---

## 0. Read first (every T20-T24 implementer)

### 0.1 Global constraints

Every brief carries C 2.8 by reference. These bite in this section:

- **#2 ASCII:** shader, HLSL and C# files are ASCII. After writing any file, check that no byte is above 0x7F (`Select-String -Path <file> -Pattern '[^\x00-\x7F]'` prints nothing).
- **#4 NetworkObject hash:** `PrefabBuilder` rebuilds `PlayerFP`. The existing `BuilderUtil.RefreshNetworkObjectHash` path must still run, and the prefab's `GlobalObjectIdHash` must stay non-zero and unchanged.
- **#6 scene churn:** after `build`, run `git restore Assets/_Project/Scenes/*.unity` unless the task really changes a scene. Only T22 changes `Spot_LakeDock`.
- **#8 EditMode reach:** EditMode tests reach Editor, Data, Core and Runtime, never Presentation. PlayMode tests reference Fishing and Gameplay, not Presentation. See 0.4.
- **#9 PlayMode output:** PlayMode fails on any unexpected `Debug.LogError` and must be warning-free. Presentation code never logs a warning in normal play. A missing optional reference is a silent no-op (or one `Debug.Log`).
- **#11 editor lock:** `Tools/unity.ps1` tests whether the lock is held. On `LOCKED` (exit 2), stop and report; a leftover `Temp/UnityLockfile` that is not held is not a lock. Never kill Unity.
- **#13 batchmode rendering:** any code that compiles shaders for validation or capture sets `ShaderUtil.allowAsyncCompilation = false` first and restores it after.
- **#14:** builders load assets after `EditorSceneManager.NewScene`. `PresentationSceneObjects.AddTo` loads its assets itself, inside the call.
- **#15:** scene objects added here are plain MonoBehaviours (no NetworkObject).
- **#17:** Presentation never writes gameplay state. It reads `FishingRodNetwork.ViewState`, `Cue`, `Announced`, `PlayerNetState`, and the read-only members of `FishingRodController`. It never calls an RPC, `OwnerWriteState`, `OwnerSetTool` or a machine method.
- **#18:** global arrays are always pushed full length: `_FGPalette` 16, `_Ripples` 8, `_Waves` 4.
- **#19:** every mesh drawn with `FishGame/Toon` has a vertex-colour channel. `RenderValidators` enforces this.
- **#20/#21:** commit explicit paths only (including new `.meta` files). Logs go in `$env:FG_OUT`.
- **Statics:** any static (`RippleField.Current`, caches) is reset in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method, the project pattern.

### 0.2 Commands

```
$env:FG_OUT = '<your session scratchpad>\fg'
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 compile
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit -Filter "<regex>"
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-play -TimeoutMinutes 15
```
Expected result lines are those in M1 contract 11 (`FG RESULT <cmd> PASS ...`). `build` runs `ValidationRunner.Run` as its last step, so a failing validator makes `build` print `FG BUILDALL FAIL`. Every builder step still runs and writes its assets.

### 0.3 Section decisions (F1-F9; pinned for T20-T24)

- **F1 `_Waves` packing (T22 pushes, T20/T21 read).** `_Waves[i] = (DirectionDeg, Amplitude, Wavelength, Speed)` for i = 0..2, the same as `WaterSettingsDef.dirAmpLenSpeed[i]` and `WaveSet.W0..W2`. `_Waves[3] = (0,0,0,0)`. The HLSL wave term mirrors T12's `WaveMath`: 0 deg = +Z, `dir = (sin th, cos th)` on (x, z), `k = 2 pi / L`, `A * sin(k * (dot(dir, xz) - S * t))`, and a term is 0 when `L <= 0`, `A == 0` or any of the four fields is non-finite (C 16.2; `FG_WaveTerm` in T20.3 carries the `all(isfinite(w))` guard).
- **F2 Palette slot channel.** `MeshBuilder` stores the slot in UV channel 1 (`mesh.SetUVs(1, ...)`), which is shader `TEXCOORD1`, the spec's `uv1.x` (T12 plan).
- **F3 Colour space.** The project is Linear (`ProjectSettings m_ActiveColorSpace: 1`).
  - `Shader.SetGlobalColor` and material colour properties are converted by Unity.
  - `Shader.SetGlobalVectorArray` and vertex colours are **not** converted.
  - Therefore `PaletteGlobals` pushes `((Color)v).linear` for every `_FGPalette` entry when `QualitySettings.activeColorSpace == ColorSpace.Linear` (T14's `PaletteVectors` returns the authored sRGB values).
  - `FishFallbackMesh` writes `colorA.linear` / `colorB.linear` into vertex colours.
  - The Toon shader treats vertex colour as linear and never converts it (ruling F-Q3, which supersedes G-Q7). The Blender fish are exported with linear vertex colours (`colors_type='LINEAR'`); the controller re-exports the staged `Art/Export/Fish_*.fbx` linear, and T29 imports and verifies them. `FishFallbackMesh` follows the same rule (linear).
- **F4 Shader validation mode.** T20 settles it (task T20, step 3) before T21 depends on it. `ShaderValidator` fails closed: an in-memory canary must be reported as broken, or `Run()` returns an error.
- **F5 Material ownership.** `RenderMaterialLibrary.cs` (new, T20) holds the T20 Toon materials. T21 adds an `internal static void BuildWorldMaterials(WaterSettingsDef water)` called from `RenderingSetupBuilder.Build()` (after T14's hook has created `WaterSettings.asset`). No new public members.
- **F6 Remote view rules (C 9 "Remote view and late join", C 7.17 `ViewState`, C 7.24).**
  - The first view value after spawn snaps: no arc and no replayed one-shot.
  - The cast arc plays only on an **observed change into Casting** (previous observed state != Casting, current == Casting), at most once per `CastId`.
  - Loops, rod bend, line and bobber placement are driven by polling `ViewState` every frame. One-shots (sounds, splashes, ripples, kicks) are driven only by `Cue` and `Announced`. A late joiner therefore sees the right steady state with no replayed bursts.
- **F7 Execution order (LateUpdate readers of the posed rig).** `PlayerBody` is 200. Then `BobberView` 240, `RodPresenter` 250, `LineView` 260, `CastMarkerView` 270. `FishingFeedback`, `CatchRevealView` and `SplashFx` use the default order. `WaterTimeDriver` is -200, so `_WaterTime` is current before any reader.
- **F8 Serialized references from builders.** Presentation components get asset references (materials, Defs) through private `[SerializeField]` fields. Builders set them with `SerializedObject` + `ApplyModifiedPropertiesWithoutUndo` + `EditorUtility.SetDirty`, so no public setter is added.
- **F9 Owner test.** "Owner" means `rod.IsSpawned && rod.IsOwner` on the same root's `FishingRodNetwork`. Before spawn, components do nothing.

### 0.4 Verification strategy for Presentation

C 10 lists only two tests for T20-T24 (both T20). EditMode cannot reference Presentation (C 2.8 #8), and the PlayMode asmdef does not reference it either. So T21-T24 are verified by:

1. `compile` and `build` (builders and validators run).
2. The full `test-edit` and `test-play`. T19's `FishingSoloTests` run the whole owner path with the presentation components on `PlayerFP`, so any exception, error or warning fails them.
3. A few **added** test files (whitelisted by ruling F-Q6: `ToonMaterialTests.cs`, `RenderingSetupTests.cs`, `PresentationWorldTests.cs`, `FeedbackSoloTests.cs`, `PlayerPresentationRigTests.cs`, `RodViewSoloTests.cs`; C 12 amended) that observe effects without referencing Presentation types. `test-play` from T22 on uses `-TimeoutMinutes 15`:
   - shader globals (`Shader.GetGlobalVectorArray`, `Shader.GetGlobalFloat`);
   - `WaterSurface`;
   - child transforms, `LineRenderer`s and `MeshRenderer`s found by `PlayerRigPaths`;
   - the `EventBus` (`CaptionRequested`);
   - prefab component type names in EditMode.
4. Manual checks by the controller and user, listed per task (MPPM for the remote rules of F6).

---

## T20 - Toon shading, ShaderValidator, Toon materials, body meshes, fish import

| | |
|---|---|
| Goal | The `FishGame/Toon` shader (spec 10.3) and shared HLSL; a fail-closed `ShaderValidator` that works under `-batchmode -nographics` (settled here); `RenderValidators`; Toon materials; MeshBuilder body meshes (vertex colours); the FBX import postprocessor and the perch reimport. |
| Owned files (C 12) | `Shaders/FishGameCommon.hlsl`, `Shaders/Toon.shader`, `Editor/Validation/ShaderValidator.cs`, `Editor/Validation/RenderValidators.cs`, `Editor/Builders/RenderMaterialLibrary.cs`, `Editor/Builders/Render/FishModelPostprocessor.cs`; generated `Content/Materials/M_Toon_Base.mat`, `M_Toon_Fish.mat`, `M_Toon_FishFallback.mat`, re-pointed `M_Body_Slot0..3.mat` and `M_Skin.mat` (GUIDs kept), `Content/Meshes/Body_UnitSphere.asset`, `Body_UnitBox.asset`, `Body_UnitCapsule.asset`, `Prefabs/PlayerFP.prefab`, `Content/Models/Fish_Perch.fbx.meta`. Tests: `Tests/EditMode/ValidationTests.cs` (add 2). `Tests/EditMode/ToonMaterialTests.cs` (whitelisted, ruling F-Q6). If the `-nographics` check in step 3 fails: a minimal `Tools/unity.ps1` change (ruling F-Q4). |
| Shared edits (C 2.6) | `Editor/Builders/MaterialLibrary.cs` (body/skin move out; hook `RenderMaterialLibrary.BuildAll()`), `Editor/Builders/PrefabBuilder.cs` (body meshes), `Editor/Validation/ValidationRunner.cs` (hook), `Editor/Builders/AssetPaths.cs` (T20 constants block). |
| Depends on | T12, T13, T19 committed (T14's `AssetPaths` block exists). |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: #2, #4, #6, #11, #13, #19, #20, #21 (section 0.1).

### T20.1 `ShaderValidator` (C 7.20; `Editor/Validation/ShaderValidator.cs`)

Public surface exactly C 7.20: `public static List<string> Run()` and `[MenuItem("FishGame/Validate Shaders")] public static void RunMenu()`. Everything else is private or internal.

`Run()` behaviour, in order:
1. Save `ShaderUtil.allowAsyncCompilation`, set it to `false`, and restore it in `finally`.
2. **Canary self-check (fail closed).** Build an in-memory shader with `ShaderUtil.CreateShaderAsset(CanarySource, false)` (no file under `Assets/`) and compile it with the step 4 routine. If no compile reports an error, return the single error `ShaderValidator: the shader compiler reported no error for the canary; shader errors are invisible in this mode`. Otherwise `Debug.Log("FG SHADERS canary ok: " + firstErrorMessage)`. Destroy the canary with `Object.DestroyImmediate`. Canary source (data, copy as is):
   ```
   Shader "Hidden/FishGame/ValidatorCanary" { SubShader { Pass { HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   float4 Vert(float4 p : POSITION) : SV_POSITION { return p; }
   float4 Frag() : SV_Target { return fg_canary_undeclared; }
   ENDHLSL } } }
   ```
3. **Enumerate** `AssetDatabase.FindAssets("t:Shader", new[] { AssetPaths.ShadersFolder })`, but only when `AssetDatabase.IsValidFolder(AssetPaths.ShadersFolder)` (Unity logs an error for a missing search folder, which would fail the run with an unhandled log instead of this error). A missing folder or none found -> error `no shaders under Assets/_Project/Shaders`. A name not starting with `FishGame/` -> error. There is no expected-name list, so T21's shaders are covered with no T20 edit.
4. **Per shader.**
   - (a) `ShaderUtil.ShaderHasError(shader)` -> one error per `ShaderUtil.GetShaderMessages(shader)` entry with Error severity. (b) `ShaderUtil.GetShaderData(shader).ActiveSubshader`; null -> error.
   - (c) For every pass `GetPass(i)`, each keyword set K0-K2 below, and the vertex and fragment stages: `pass.CompileVariant(<vertex|fragment>, keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64)`. Use the 6000.6 overload with these four arguments, or its nearest equivalent; record which one in the report.
   - `!Success` -> one error per Error message: `<shader> / <pass name> / <stage> [<keywords>]: <message> (<file>:<line>)`. `Success` with empty `ShaderData` bytes -> error `<...>: empty bytecode`. Warnings go to `Debug.Log("FG SHADER WARN ...")`, never `LogWarning`.
5. `Debug.Log($"FG SHADERS checked={shaders} passes={passes} compiles={n} ms={elapsed}")`.

Keyword sets (`private static readonly string[][]`). Extra defines on a pass that does not declare them are harmless:
- K0: none.
- K1: `_MAIN_LIGHT_SHADOWS_CASCADE _ADDITIONAL_LIGHTS _CLUSTER_LIGHT_LOOP _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _LIGHT_LAYERS _LIGHT_COOKIES FOG_EXP2 _CASTING_PUNCTUAL_LIGHT_SHADOW INSTANCING_ON`
- K2: `_MAIN_LIGHT_SHADOWS_SCREEN _ADDITIONAL_LIGHTS_VERTEX FOG_LINEAR`

If one keyword makes URP's own include fail in a way URP's SimpleLit would also fail on (not our code), drop that keyword from the set and name it in the report. Budget: about 40 compiles in total after T21. If `ms` is above 60000, drop K2 and report it.

`RunMenu()` logs `FG SHADERS PASS` or `FG SHADERS FAIL errors=N` plus one `FG SHADERS ERROR <e>` line per error, like `ValidationRunner.RunMenu`.

The class comment states the authority-free editor role and the T20 spike result (step 3), e.g. `Verified 2026-10-xx: CompileVariant reports HLSL errors under -batchmode -nographics (canary + temp-file spike).`

### T20.2 `RenderValidators` (C 7.20; `Editor/Validation/RenderValidators.cs`)

- `All()`: returns `ShaderValidator.Run()` then `ToonMeshesHaveVertexColours()`.
- `ToonMeshesHaveVertexColours()` visits every `Renderer` (include inactive) in every prefab under `Assets/_Project/Prefabs` (read-only `AssetDatabase.LoadAssetAtPath<GameObject>`) and every enabled build scene, opened the way T4's `Validators.MeshReferences` opens them (reuse its helper if internal; restore the scene setup after).
- For a renderer whose `sharedMaterials` contain a material with `shader.name == "FishGame/Toon"`: `MeshRenderer` -> sibling `MeshFilter.sharedMesh`; `SkinnedMeshRenderer` -> `sharedMesh`; any other renderer type -> error `unsupported renderer`. Null mesh -> error. `!mesh.HasVertexAttribute(VertexAttribute.Color)` -> error `<asset or scene>: <hierarchy path> (<mesh name>) renders FishGame/Toon without vertex colours`. Use `HasVertexAttribute`, never `mesh.colors` (FBX meshes are not readable).
- Also (BF-7), for each such Toon material: `_JitterEncoded` must be 1 unless the material is named `M_Toon_Fish` (which must be 0; `M_Toon_FishFallback` must be 1); otherwise error `<asset or scene>: <hierarchy path> material <name> has the wrong _JitterEncoded`.
- `ValidationRunner.Run` hook (one line, after T14's): `Add(all, "Render", RenderValidators.All());`.

### T20.3 `FishGameCommon.hlsl` (complete code; C 7.20, F1-F3)

```hlsl
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

half3 FG_PaletteColor(float slot)
{
    int i = clamp((int)round(slot), 0, 15);
    return (half3)_FGPalette[i].rgb;
}

// Mirrors FishGame.Core.WaveMath (T12) term for term: 0 deg = +Z, dir = (sin th, cos th) on (x, z), k = 2 pi / L,
// term = A * sin(k * (dot(dir, xz) - S * t)); L <= 0, A == 0 or ANY non-finite field (NaN, +/-Inf) -> 0 (T12 follow-up, C 16.2:
// WaveMath treats a non-finite field as a zero term; without this guard CPU and GPU heights diverge). Change WaveMath and this together, never one.
float FG_WaveTerm(float4 w, float2 xz, float t)
{
    if (!all(isfinite(w))) return 0.0;
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

// One fog path for every FishGame shader (URP built-in fog, spec 10.3).
half FG_FogFactor(float clipZ) { return ComputeFogFactor(clipZ); }
half3 FG_ApplyFog(half3 color, half fogFactor) { return MixFog(color, fogFactor); }

#endif
```

### T20.4 `Toon.shader` (complete code; spec 10.3, C 7.20)

Rules the code follows:
- `UnityPerMaterial` is declared once in `HLSLINCLUDE`, so it is byte-identical in every pass (SRP Batcher).
- **Never include `SimpleLitInput.hlsl`**: its CBUFFER differs.
- URP's `ShadowCasterPass.hlsl` and `DepthOnlyPass.hlsl` are included directly. Verified in URP 17.6: they declare no CBUFFER, and they touch `_BaseMap` only under `_ALPHATEST_ON`, which Toon never declares.
- The UniversalForward keyword list is the SimpleLit 17.6 "Universal Pipeline keywords" block verbatim (META_QUEST lines dropped), plus Fog and RenderingLayers `#include_with_pragmas` and instancing.
- The lightmap, `DEBUG_DISPLAY` and `LOD_FADE_CROSSFADE` blocks and DOTS instancing are left out. The project has no baked GI and no BRG (ruling F-Q9).

```hlsl
// Shaders/Toon.shader - FishGame/Toon (spec 10.3, C 7.20). Owned by T20. ASCII only.
// Albedo: vertexFactor = lerp(vertexColor.rgb, 2 * vertexColor.rgb, _JitterEncoded);
//         albedo = (_UsePalette > 0.5 ? _FGPalette[round(uv1.x)] : 1) * vertexFactor * _BaseColor.
// BF-7: MeshBuilder stores a half-scale grey 0.5 * (1 +/- jitter) (UNorm8-safe), so every MeshBuilder-built mesh is drawn
// with _JitterEncoded = 1 (props, lakebed/shore, rod, bobber, body slots, skin, FishFallbackMesh, silhouette); the Blender
// fish models on M_Toon_Fish use _JitterEncoded = 0 (their vertex colour is the linear albedo as stored).
// Lighting: band = smoothstep(mid - soft, mid + soft, saturate(dot(n, L)) * shadowAtten);
//           lit = albedo * lerp(_ToonShade, mainLight.color, band) + albedo * _ToonAmbient * 0.15; additional lights banded.
// Vertex colour is linear (F3): never converted here.
Shader "FishGame/Toon"
{
    Properties
    {
        _BaseColor("Base Colour", Color) = (1, 1, 1, 1)
        _RampMid("Ramp Mid", Range(0, 1)) = 0.5
        _RampSoft("Ramp Softness", Range(0.001, 0.5)) = 0.04
        [ToggleUI] _UsePalette("Use Palette (uv1.x slot)", Float) = 1
        [ToggleUI] _JitterEncoded("Vertex Colour Is MeshBuilder Half-Scale (x2)", Float) = 1
        _Desaturate("Desaturate", Range(0, 1)) = 0
        _RimPow("Rim Power", Range(0.5, 8)) = 4
        _RimStrength("Rim Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _RampMid;
            half _RampSoft;
            half _UsePalette;
            half _JitterEncoded;     // BF-7: in UnityPerMaterial (SRP Batcher), branch-free lerp below
            half _Desaturate;
            half _RimPow;
            half _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            // URP 17.6 SimpleLit "Universal Pipeline keywords" (META_QUEST lines omitted)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float2 uv1        : TEXCOORD1;   // x = palette slot (F2)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half3  albedo     : TEXCOORD2;   // per vertex: the 3 vertices of a flat face share it
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half FG_ToonBand(half ndl, half atten)
            {
                return smoothstep(_RampMid - _RampSoft, _RampMid + _RampSoft, ndl * atten);
            }

            half3 FG_ToonAdditional(Light light, half3 n, half3 albedo)
            {
                half band = FG_ToonBand(saturate(dot(n, light.direction)), light.shadowAttenuation);
                return albedo * light.color * (band * light.distanceAttenuation);
            }

            Varyings ToonVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                half3 vertexFactor = lerp(input.color.rgb, 2.0h * input.color.rgb, _JitterEncoded);   // BF-7: grey meshes have r == g == b
                half3 baseRgb = _UsePalette > 0.5h ? FG_PaletteColor(input.uv1.x) * vertexFactor : vertexFactor;
                half3 albedo = baseRgb * _BaseColor.rgb;
                half grey = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));   // Rec.709 luma (linear colours); was 0.299/0.587/0.114 until the polish batch 1e82274
                output.albedo = lerp(albedo, grey.xxx, _Desaturate);
                output.fogFactor = FG_FogFactor(pos.positionCS.z);
                return output;
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);

                InputData inputData = (InputData)0;     // the cluster loop macros read positionWS and normalizedScreenSpaceUV
                inputData.positionWS = input.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 shadowMask = CalculateShadowMask(inputData);
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
                half3 albedo = input.albedo;

                half band = FG_ToonBand(saturate(dot(n, mainLight.direction)), mainLight.shadowAttenuation);
                half3 tint = lerp(_ToonShade.rgb, mainLight.color, band);
                half3 lit = albedo * tint + albedo * _ToonAmbient.rgb * 0.15h;
                half rim = pow(1.0h - saturate(dot(n, inputData.viewDirectionWS)), _RimPow) * _RimStrength;
                lit += albedo * tint * rim;

            #if defined(_ADDITIONAL_LIGHTS)
                uint meshRenderingLayers = GetMeshRenderingLayer();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lit += FG_ToonAdditional(light, n, albedo);
                    }
                }
                #endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lit += FG_ToonAdditional(light, n, albedo);
                    }
                LIGHT_LOOP_END
            #endif

                return half4(FG_ApplyFog(lit, inputData.fogCoord), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
```
If `ShaderValidator` reports a real error in this block (for example an API that differs in 6000.6), fix the smallest thing and list the change in the report. The reviewer diffs this block against the file.

### T20.5 `RenderMaterialLibrary` (C 7.20; `Editor/Builders/RenderMaterialLibrary.cs`)

- `BuildAll()`:
  - `Shader.Find("FishGame/Toon")`; null -> `InvalidOperationException("Shader not found: FishGame/Toon")`. Ensure `AssetPaths.MaterialsFolder`.
  - Toon materials (load or create `MaterialsFolder/<name>.mat`):

    | Material | `_UsePalette` | `_BaseColor` |
    |---|---|---|
    | `M_Toon_Base` | 1 | white |
    | `M_Toon_Fish` | 0 | white |
    | `M_Toon_FishFallback` | 0 | white |
    | `M_Body_Slot0..3` | 0 | `SlotPalette.GetRgb(i)` |
    | `M_Skin` | 0 | `SlotPalette.GetSkinRgb` |

  - Every Toon material also gets `_RampMid` 0.5, `_RampSoft` 0.04, `_Desaturate` 0, `_RimPow` 4, `_RimStrength` 0, and `_JitterEncoded` (BF-7): **1** for `M_Toon_Base`, `M_Body_Slot0..3`, `M_Skin` and `M_Toon_FishFallback` (all MeshBuilder meshes; `M_Toon_FishFallback` is for `FishFallbackMesh` meshes and the snap silhouette), **0** for `M_Toon_Fish` (the Blender fish models; the vertex colour is linear albedo as stored).
  - **Ensure semantics** (same as `MaterialLibrary.Ensure`): switch the shader only when it differs, and then clear `shaderKeywords` (removes the Lit leftovers). Set each property only when it differs. `SetDirty` only on a change. GUIDs of existing `.mat` files are kept (in place). A second `build` changes nothing.
  - Last line: `FishModelPostprocessor.EnsureImportSettings();` (T20.7).
- `Get(string name)`: same contract as `MaterialLibrary.Get` (throws `InvalidOperationException` when missing).
- T21 later adds `internal static void BuildWorldMaterials(WaterSettingsDef water)` (F5). T20 leaves a comment where it goes.
- `MaterialLibrary.cs` (shared edit): remove the `M_Body_Slot` loop and the `M_Skin` `Ensure` line (otherwise Lit and Toon ping-pong every build); append `RenderMaterialLibrary.BuildAll();` as the last statement of `BuildAll`; header comment "body slot and skin materials are FishGame/Toon, built by RenderMaterialLibrary (T20)". `M_Water`, `M_Wood`, `M_WoodDark`, `M_Rock`, `M_Grass`, `M_Sand`, `M_Foliage` and `M_Brass` stay Lit (T28 moves props).

### T20.6 Body meshes (C 7.20 last bullet; `PrefabBuilder.cs` shared edit)

- Three unit meshes made with `MeshBuilder` (default `faceJitter` 0.04, slot 0; the slot is unused in vertex-colour mode):
  - `Body_UnitSphere.asset` = `Icosphere(Vector3.zero, 0.5f, 1, 0)`, seed `0xB0D10001`, 80 tris;
  - `Body_UnitBox.asset` = `Box(Vector3.zero, Vector3.one, Quaternion.identity, 0)`, seed `0xB0D10002`;
  - `Body_UnitCapsule.asset` = `Capsule(Vector3.zero, 0.5f, 2f, 8, 2, Quaternion.identity, 0)`, seed `0xB0D10003`. T12: Y axis, total height 2, the same as Unity's capsule primitive, 64 tris.
- **No colour pass (BF-7).** The body meshes keep MeshBuilder's half-scale grey as built. The non-palette body materials `M_Body_Slot0..3` and `M_Skin` (`_UsePalette` 0, `_BaseColor` carries the colour) set `_JitterEncoded` 1, so the shader doubles the grey and the full +/- jitter survives.
- Saved in `AssetPaths.MeshesFolder` in place: if the asset exists, `EditorUtility.CopySerialized(built, existing)`, `Object.DestroyImmediate(built)`, `SetDirty`; else `CreateAsset`. Reuse T19's mesh-save helper if it made one.
- In `BuildPlayerRoot`, after each existing `Prim(...)` for Head, Nose, Torso, ArmL/ArmR Mesh and LegL/LegR Mesh, set that object's `MeshFilter.sharedMesh` to the matching unit mesh (sphere: Head; box: Nose, Torso; capsule: arms, legs).
- The M1 transforms (positions, scales), materials and collider state are unchanged. Sizes therefore equal M1 contract 6.3. `PlayerBody` behaviour is unchanged.

### T20.7 `FishModelPostprocessor` (C 7.20; `Editor/Builders/Render/FishModelPostprocessor.cs`)

- `public sealed class FishModelPostprocessor : AssetPostprocessor`; `public override uint GetVersion() => 1;` (a changed postprocessor reimports).
- `void OnPreprocessModel()`: applies only when `assetPath` starts with `AssetPaths.ModelsFolder + "/"` (T14 constant; do not redeclare it) and ends with `.fbx` (ordinal ignore case). It calls `Apply((ModelImporter)assetImporter)`.
- `internal static bool Apply(ModelImporter mi)` sets the C 7.20 list (`materialImportMode = None`, `importNormals = Import`, `importAnimation = false`, `importCameras = false`, `importLights = false`, `globalScale = 1`, `useFileScale = true`, `isReadable = false`, `meshCompression = Off`) plus `animationType = None` (no Animator on a static fish), and returns true if anything changed. It never assigns materials.
- `internal static int EnsureImportSettings()`: for each `.fbx` under `ModelsFolder`, `Apply` the importer and `SaveAndReimport()` only if it changed. This writes the `.meta`, so `build` turns `Fish_Perch.fbx.meta` `materialImportMode: 2` into `0` once. Then log `FG MODELS <file> tris=<n> submeshes=<k> colours=<yes|no>` (`Debug.Log`) per model (C G6 evidence for the controller and T29). Returns the count reimported.
- `[MenuItem("FishGame/Dev/Reimport Fish Models")] public static void ReimportAll()`: `Apply` + `SaveAndReimport()` for every model (forced), then the same log lines.
- Note for T23: with material import None the spike perch keeps 5 submeshes (5 Blender materials) until T29 replaces it; the new staged species models (`Art/Export/Fish_*.fbx`) have ONE submesh, one material slot and per-face LINEAR vertex colours (ruling G-Q6). The reveal assigns `M_Toon_Fish` to every submesh either way.

### T20.8 `AssetPaths` (T20 block, constants only)

```csharp
// T20 additions (shaders, Toon materials, body meshes). ModelsFolder is T14's; do not redeclare it.
public const string ShadersFolder = "Assets/_Project/Shaders";
public const string BodyUnitSphereMesh = MeshesFolder + "/Body_UnitSphere.asset";
public const string BodyUnitBoxMesh = MeshesFolder + "/Body_UnitBox.asset";
public const string BodyUnitCapsuleMesh = MeshesFolder + "/Body_UnitCapsule.asset";
```

### T20 tests

`Tests/EditMode/ValidationTests.cs` (add; names exactly C 10.3):
- `Validation_Shaders_Pass`: `bool prev = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false; try { AssertNoErrors(ShaderValidator.Run()); } finally { ShaderUtil.allowAsyncCompilation = prev; }`.
- `Validation_ToonMeshesHaveVertexColours_Pass`: `AssertNoErrors(RenderValidators.ToonMeshesHaveVertexColours())`.

`Tests/EditMode/ToonMaterialTests.cs` (addition; class `ToonMaterialTests`):
- `ToonMaterials_BaseAndFish_UseToonShaderAndColourModes`: `M_Toon_Base`: shader `FishGame/Toon`, `_UsePalette` 1, `_JitterEncoded` 1, `_BaseColor` white. `M_Toon_Fish`: `_UsePalette` 0, `_JitterEncoded` 0, white. `M_Toon_FishFallback`: `_UsePalette` 0, `_JitterEncoded` 1, white. Also, over every `.mat` under `AssetPaths.MaterialsFolder` whose shader is `FishGame/Toon`: `_JitterEncoded` is 1 for all except `M_Toon_Fish` (BF-7).
- `ToonMaterials_BodySlotsAndSkin_UseToonWithSlotPaletteColours`: slots 0..3: Toon, `_UsePalette` 0, `_JitterEncoded` 1, `_BaseColor` within 1e-4 of `SlotPalette.GetRgb(i)`; `M_Skin` likewise with the skin rgb.
- `PlayerPrefab_BodyMeshes_AreUnitMeshesWithVertexColours`:
  - In `PlayerFP`, the 7 body parts (`PlayerRigPaths.Head, Nose, Torso, ArmLMesh, ArmRMesh, LegLMesh, LegRMesh`) have meshes whose asset paths are the three `Body_Unit*` constants and have a colour channel.
  - `Head.localScale == (0.28, 0.28, 0.28)` and `Torso.localScale == (0.42, 0.55, 0.24)` (M1 sizes kept).
- `FishModels_ImportSettings_MatchPostprocessor`: every `.fbx` under `ModelsFolder` has the T20.7 settings. It passes vacuously only when there is no model; the perch exists, so it is not vacuous.

### T20 steps (TDD order)

1. **Read** T12's `MeshBuilder` (`Capsule`/`Icosphere` conventions) and `ShaderGlobals`, T14's `AssetPaths` block, T4's `Validators.MeshReferences` scene opening, and T19's `PlayerFishingRigBuilder` (mesh-save helper; rod and bobber materials).
2. **Validators first.** Write `ShaderValidator`, `RenderValidators`, the `ValidationRunner` hook and the two `ValidationTests`. Run `test-edit -Filter "Validation_Shaders_Pass|Validation_ToonMeshesHaveVertexColours_Pass"`. Expected: Shaders FAIL with `no shaders under Assets/_Project/Shaders`; ToonMeshes PASS (no Toon material yet). The log must contain `FG SHADERS canary ok:` with the HLSL undeclared-identifier message.
3. **Spike: settle `-nographics` (skeleton risk 7, V13). Do this before writing any real shader.**
   - (a) From step 2's log, quote the `FG SHADERS canary ok:` line. If instead the test failed because the canary compile logged an error to the console, keep the canary in `Run()` (it is what makes the validator fail closed) and add `LogAssert.Expect(LogType.Error, new Regex("ValidatorCanary"))` at the start of `Validation_Shaders_Pass` (test body only; the name is unchanged). Report it.
   - (b) Create the temporary file `Assets/_Project/Shaders/ZzCanaryTemp.shader` (`Shader "FishGame/ZzCanaryTemp"`, one HLSL pass returning an undeclared identifier). Run the same filter. Expected: FAIL with an error naming `FishGame/ZzCanaryTemp` and the HLSL message.
   - Delete `ZzCanaryTemp.shader` and its `.meta` (`Remove-Item` on the two untracked files) and rerun. Expected: back to `no shaders`.
   - (c) Record the outcome, the overload used and the `ms` in the report and in the `ShaderValidator` class comment.
   - **Fallback (ruling F-Q4):** if neither (a) nor (b) detects the error under `unity.ps1` (`-nographics`), T20 owns a minimal change to `Tools/unity.ps1`: run the validation (`Validation_Shaders_Pass`) in a graphics-capable batchmode (no `-nographics` for that run only), keep every other command unchanged, document it in the script header and add the script to the commit. Report what changed. T21 must not start until `Validation_Shaders_Pass` is fail-closed and green.
4. Write `FishGameCommon.hlsl` and `Toon.shader` (T20.3, T20.4). Run `test-edit -Filter "Validation_Shaders_Pass"`. Expected: PASS, with `FG SHADERS checked=1`.
5. Write `ToonMaterialTests` (all four). Run `test-edit -Filter "ToonMaterialTests"`. Expected: FAIL (materials missing or Lit, body meshes primitive, perch materialImportMode 2).
6. Write `RenderMaterialLibrary`, the `MaterialLibrary` edit, the body meshes in `PrefabBuilder`, `FishModelPostprocessor` and the `AssetPaths` block. `compile`, then `build`. Expected: `FG BUILDALL PASS` and `FG MODELS Fish_Perch.fbx ... colours=<yes|no>` in the log. Then `test-edit -Filter "ToonMaterialTests|Validation_"`. Expected: PASS.
7. `git diff --stat`. Expected: the five `.mat` edits; new `M_Toon_*.mat` and `Body_Unit*.asset` (+ `.meta`); `PlayerFP.prefab`; `Fish_Perch.fbx.meta` (`materialImportMode: 0`, `importCameras: 0`, `importLights: 0`); **no `.meta` change for the five existing materials** (GUIDs kept). Then `git restore Assets/_Project/Scenes/*.unity`.
8. Run `build` again, then `git status`: no new changes (idempotent).
9. Full `test-edit`, then `test-play -TimeoutMinutes 15`. Both PASS.
10. Commit:
```
git add Assets/_Project/Shaders.meta Assets/_Project/Shaders/FishGameCommon.hlsl Assets/_Project/Shaders/FishGameCommon.hlsl.meta Assets/_Project/Shaders/Toon.shader Assets/_Project/Shaders/Toon.shader.meta Assets/_Project/Editor/Validation/ShaderValidator.cs Assets/_Project/Editor/Validation/ShaderValidator.cs.meta Assets/_Project/Editor/Validation/RenderValidators.cs Assets/_Project/Editor/Validation/RenderValidators.cs.meta Assets/_Project/Editor/Validation/ValidationRunner.cs Assets/_Project/Editor/Builders/RenderMaterialLibrary.cs Assets/_Project/Editor/Builders/RenderMaterialLibrary.cs.meta Assets/_Project/Editor/Builders/Render.meta Assets/_Project/Editor/Builders/Render/FishModelPostprocessor.cs Assets/_Project/Editor/Builders/Render/FishModelPostprocessor.cs.meta Assets/_Project/Editor/Builders/MaterialLibrary.cs Assets/_Project/Editor/Builders/PrefabBuilder.cs Assets/_Project/Editor/Builders/AssetPaths.cs Assets/_Project/Content/Materials/M_Toon_Base.mat Assets/_Project/Content/Materials/M_Toon_Base.mat.meta Assets/_Project/Content/Materials/M_Toon_Fish.mat Assets/_Project/Content/Materials/M_Toon_Fish.mat.meta Assets/_Project/Content/Materials/M_Toon_FishFallback.mat Assets/_Project/Content/Materials/M_Toon_FishFallback.mat.meta Assets/_Project/Content/Materials/M_Body_Slot0.mat Assets/_Project/Content/Materials/M_Body_Slot1.mat Assets/_Project/Content/Materials/M_Body_Slot2.mat Assets/_Project/Content/Materials/M_Body_Slot3.mat Assets/_Project/Content/Materials/M_Skin.mat Assets/_Project/Content/Meshes/Body_UnitSphere.asset Assets/_Project/Content/Meshes/Body_UnitSphere.asset.meta Assets/_Project/Content/Meshes/Body_UnitBox.asset Assets/_Project/Content/Meshes/Body_UnitBox.asset.meta Assets/_Project/Content/Meshes/Body_UnitCapsule.asset Assets/_Project/Content/Meshes/Body_UnitCapsule.asset.meta Assets/_Project/Prefabs/PlayerFP.prefab Assets/_Project/Content/Models/Fish_Perch.fbx.meta Assets/_Project/Tests/EditMode/ValidationTests.cs Assets/_Project/Tests/EditMode/ToonMaterialTests.cs Assets/_Project/Tests/EditMode/ToonMaterialTests.cs.meta
git commit -m "m2(shaders): FishGame/Toon, ShaderValidator (nographics settled), Toon materials, MeshBuilder bodies, fish import" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
Drop from the list any `.meta` that already existed (for example `Shaders.meta` or `Render.meta` if T19 created the folder). Check with `git status --short` before adding.

### T20 acceptance (reviewer)

- **Spike evidence.** The report quotes the `FG SHADERS canary ok:` line, the temp-file FAIL line and the `ms`, all from `unity.ps1` runs (`-nographics`). `Run()` returns an error when the canary is not reported (read the code).
- `FishGameCommon.hlsl` and `Toon.shader` match T20.3/T20.4 (code-block diff). Any deviation is listed in the report with its reason.
- One `UnityPerMaterial` (HLSLINCLUDE); no `SimpleLitInput.hlsl`. The forward keyword list matches SimpleLit 17.6's pipeline block.
- `Validation_Shaders_Pass` and `Validation_ToonMeshesHaveVertexColours_Pass` are green in `test-edit` (`-nographics`).
- Existing material GUIDs are kept. `PlayerFP` `GlobalObjectIdHash` is unchanged and non-zero. A second `build` is clean.
- `Fish_Perch.fbx.meta` has `materialImportMode: 0`. The report says whether the spike perch has vertex colours and how many submeshes (C G6; T14 leaves `perch.model` null if not exactly one submesh with colours; T29 then replaces it with the new perch).
- No `LogWarning` in `ShaderValidator` (warnings go to `Debug.Log`). ASCII scan clean.

### T20 manual check (controller, after the commit)

Ask the user to open `PlayerFP` in prefab mode. Expected: flat faces with a visible light/shade band and slot colours on the body. The shade side looks dark until T22 pushes `_ToonShade` (globals are unset before T22). This is expected, not a defect.

---

## T21 - Water, Line and GradientSky shaders; rendering setup

| | |
|---|---|
| Goal | `FishGame/Water` (spec 10.4), `FishGame/Line` (spec 10.5, 9.11), `FishGame/GradientSky` (spec 10.3), the world materials, the two volume profiles, `ApplySceneLighting`, and camera post-processing on `PlayerFP`. |
| Owned files (C 12) | `Shaders/Water.shader`, `Shaders/Line.shader`, `Shaders/GradientSky.shader`, `Editor/Builders/Render/RenderingSetupBuilder.cs`; generated `Content/Materials/M_Water_Surface.mat`, `M_Line.mat`, `M_Sky.mat`, `M_Particle.mat`, `M_Marker.mat`, `Content/Rendering/VP_WarmSunset.asset`, `VP_CoolNight.asset`, `Prefabs/PlayerFP.prefab`. `Tests/EditMode/RenderingSetupTests.cs` (whitelisted, ruling F-Q6). |
| Shared edits (C 2.6) | `RenderMaterialLibrary.cs` (adds `internal static void BuildWorldMaterials(WaterSettingsDef water)`), `ContentBuilder.cs` (hook after T14's: `RenderingSetupBuilder.Build();`), `PrefabBuilder.cs` (camera post-processing), `AssetPaths.cs` (T21 block). |
| Depends on | T20 committed, **and T20's spike resolved** (F4). |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: #2, #4, #6, #11, #13 (the shaders go through `Validation_Shaders_Pass`), #20, #21. V13 items owned here: water facet-normal sign (resolved in code below by forcing the normal up) and C#/HLSL wave parity (F1, reviewed line by line against T12's `WaveMath`). Fog variants in a player build stay with T28/T30.

**Carry-forward (2026-10-12, C 16.2).** (1) T12's follow-up made `WaveMath` treat ANY non-finite wave field (NaN or +/-Inf in direction, amplitude, wavelength or speed) as a zero term, in the height and in the amplitude sum. `Water.shader` must do the same per wave, or the CPU and GPU heights diverge. The guard lives in `FG_WaveTerm` (T20.3, `all(isfinite(w))` on the four fields of each `_Waves` entry, so a term is 0 when any field is non-finite, wavelength <= 0 or amplitude == 0): `Water.shader` must call `FG_WaveHeight`/`FG_WaveTerm` and never re-implement the term. Add the non-finite case to the F1 line-by-line review. (2) `ArtPaletteDef.PaletteVectors(season)` allocates a `Vector4[16]`: nothing in this task may call it per frame; the palette is pushed once on enable and on validate (T22.1 `PaletteGlobals` keeps one cached array).

### T21.1 `Water.shader` (complete code; spec 10.4, C 7.21)

```hlsl
// Shaders/Water.shader - FishGame/Water (spec 10.4, C 7.21). Owned by T21. ASCII only.
// Vertex-displaced MeshBuilder.Grid (shared vertices) via _Waves/_WaterTime (mirrors WaveMath, F1); flat facet normal;
// depth fade shallow -> deep over _DepthRange; foam band where scene depth is within _FoamWidth, broken by hash noise,
// plus _Ripples (foam and brightness only). Transparent, ZWrite Off, one UniversalForward pass, URP fog.
Shader "FishGame/Water"
{
    Properties
    {
        _ShallowColor("Shallow Colour", Color) = (0.36, 0.72, 0.70, 0.75)
        _DeepColor("Deep Colour", Color) = (0.08, 0.28, 0.42, 0.95)
        _FoamColor("Foam Colour", Color) = (0.92, 0.97, 1.0, 1.0)
        _DepthRange("Depth Range (m)", Float) = 3.0
        _FoamWidth("Foam Width (m)", Float) = 0.35
        _RampMid("Ramp Mid", Range(0, 1)) = 0.45
        _RampSoft("Ramp Softness", Range(0.001, 0.5)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _DepthRange;
                float _FoamWidth;
                half _RampMid;
                half _RampSoft;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  eyeDepth   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
            };

            Varyings WaterVert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += FG_WaveHeight(positionWS.xz, _WaterTime);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.eyeDepth = -TransformWorldToView(positionWS).z;
                o.fogFactor = FG_FogFactor(o.positionCS.z);
                return o;
            }

            half4 WaterFrag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                // Flat facet normal (spec 10.4). The cross-product sign depends on the API's derivative orientation,
                // so force it upward instead of trusting one sign (V13).
                float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
                n = n.y < 0.0 ? -n : n;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthBelow = max(0.0, sceneEye - input.eyeDepth);

                half4 body = lerp(_ShallowColor, _DeepColor, smoothstep(0.0, _DepthRange, depthBelow));
                float ripple = FG_RippleSum(positionWS.xz, _WaterTime);
                half foamEdge = 1.0h - saturate(depthBelow / max(_FoamWidth, 0.001));
                half noise = FG_Hash21(floor(positionWS.xz * 3.0) + floor(_WaterTime * 1.5));
                half foam = saturate(step(noise * 0.7h + 0.15h, foamEdge) + ripple * 0.6h);

                Light mainLight = GetMainLight();
                half band = smoothstep(_RampMid - _RampSoft, _RampMid + _RampSoft, saturate(dot(n, mainLight.direction)));
                half3 tint = lerp(_ToonShade.rgb, mainLight.color, band);
                half3 rgb = body.rgb * tint + body.rgb * _ToonAmbient.rgb * 0.15h;
                rgb += ripple * 0.25h * mainLight.color;              // ripples add brightness only (spec 10.4)
                rgb = lerp(rgb, _FoamColor.rgb * tint, foam);
                half alpha = saturate(max(body.a, foam * _FoamColor.a));
                return half4(FG_ApplyFog(rgb, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
```
The depth fade needs the camera depth texture. `PC_RPAsset` already has `m_RequireDepthTexture: 1` (checked). Toon's `DepthOnly` pass provides prop depth.

### T21.2 `Line.shader` (behaviour; C 7.21, spec 9.11/10.5)

- `Shader "FishGame/Line"`: unlit, opaque, `Queue Geometry`, `Cull Off`, one `UniversalForward` pass, URP fog (`#include_with_pragmas` Fog + `FG_FogFactor`/`FG_ApplyFog`).
- Inputs: POSITION, TEXCOORD0 (`uv.x` 0..1 along the line; `LineView` sets `textureMode = Stretch`), COLOR (the LineRenderer colour, multiplied in).
- `UnityPerMaterial` (single pass): `_LineColor` (0.92,0.92,0.88,1), `_WarmColor` (1.0,0.72,0.35,1), `_WarnColor` (1.0,0.25,0.18,1), `_Tension` (0, Range 0..1.2), `_DashCount` (20), `_DashSpeed` (3).
- Fragment, with `t = _Tension`:
  - `warm = smoothstep(0.6, 0.8, t)`; `warn = step(0.8, t)`.
  - `rgb = lerp(lerp(_LineColor, _WarmColor, warm), _WarnColor, warn)`.
  - Dash (C 7.21 formula): `dash = step(0.5, frac(uv.x * _DashCount - _Time.y * _DashSpeed))`, then `rgb *= lerp(1, lerp(0.3, 1, dash), warn)`. The moving dark gaps are a pattern, not hue only (spec 10.2).
  - Grace shimmer: `rgb += step(1.0, t) * 0.6 * (0.5 + 0.5 * sin(_Time.y * 40 + uv.x * 60))`.
  - Then `rgb *= vertexColor.rgb`, fog, alpha 1.
- The width pulse is not in the shader (C 7.21: `LineView`).

### T21.3 `GradientSky.shader` (behaviour; C 7.21, spec 10.3)

- `Shader "FishGame/GradientSky"`: tags `Queue Background`, `RenderType Background`, `PreviewType Skybox`, `RenderPipeline UniversalPipeline`; `Cull Off`, `ZWrite Off`; one pass with no LightMode tag. Includes Core and `FishGameCommon.hlsl` (sky globals).
- `UnityPerMaterial`: `_SunSize` (0.03), `_SunSoftness` (0.006), `_SunIntensity` (1.5), `_HorizonSharpness` (2.5).
- `dir = normalize(positionOS)`:
  - if `dir.y >= 0`: `rgb = lerp(_SkyHorizon, _SkyTop, 1 - pow(1 - saturate(dir.y), _HorizonSharpness))`;
  - else: `rgb = lerp(_SkyHorizon, _SkyGround, 1 - pow(1 - saturate(-dir.y), _HorizonSharpness))`.
- Sun disc: `d = dot(dir, _MainLightPosition.xyz)` (the URP main-light direction global), `rgb += smoothstep(1 - _SunSize - _SunSoftness, 1 - _SunSize, d) * _MainLightColor.rgb * _SunIntensity`. No fog on the sky.

### T21.4 `RenderMaterialLibrary.BuildWorldMaterials` (internal; F5)

Same ensure semantics as T20.5 (in place, GUIDs kept, `SetDirty` only on change).

| Material | Shader | Settings |
|---|---|---|
| `M_Water_Surface` | `FishGame/Water` | `_ShallowColor`, `_DeepColor`, `_DepthRange`, `_FoamWidth` copied from the `WaterSettingsDef` asset (getters `ShallowColor`, `DeepColor`, `DepthRange`, `FoamWidth`). A designer retune needs a `build`. |
| `M_Line` | `FishGame/Line` | defaults |
| `M_Sky` | `FishGame/GradientSky` | defaults |
| `M_Particle` | `Universal Render Pipeline/Particles/Unlit` | transparent alpha blend: `_Surface` 1, `_Blend` 0, `_SrcBlend` 5 (SrcAlpha), `_DstBlend` 10 (OneMinusSrcAlpha), `_ZWrite` 0, keyword `_SURFACE_TYPE_TRANSPARENT` on, `SetOverrideTag("RenderType", "Transparent")`, `renderQueue` 3000, `_BaseColor` white |
| `M_Marker` | `Universal Render Pipeline/Unlit` | `_BaseColor` white, `_Cull` 0 (Off: marker winding never matters), `renderQueue` 3100. Queue 3100 draws it after the transparent water (ZWrite Off), so the marker is never tinted or hidden by the water. |

A shader that `Shader.Find` cannot find -> `InvalidOperationException` naming it.

### T21.5 `RenderingSetupBuilder` (C 7.21; `Editor/Builders/Render/RenderingSetupBuilder.cs`)

- `Build()`:
  1. Load `WaterSettingsDef` at `AssetPaths.WaterSettingsAsset` (T14); null -> `InvalidOperationException` naming the path. Call `RenderMaterialLibrary.BuildWorldMaterials(water)`.
  2. Volume profiles in `AssetPaths.RenderingFolder`, in place.
     - Load or `CreateInstance<VolumeProfile>` + `CreateAsset`.
     - For each component: `TryGet<T>`, else `profile.Add<T>(true)` **and** `AssetDatabase.AddObjectToAsset(component, profile)`. Without that, the component is not saved: a known VolumeProfile pitfall.
     - Each parameter gets `overrideState = true` and a value. `SetDirty` on the profile and its components.
     - Exactly two components per profile: `ColorAdjustments` and `WhiteBalance`. No Tonemapping (spec 10.3: tonemapping None).

     | Profile | WhiteBalance temperature / tint | ColorAdjustments postExposure / saturation / colorFilter |
     |---|---|---|
     | `VP_WarmSunset` | +18 / +4 | 0 / +8 / (1.00, 0.94, 0.86) |
     | `VP_CoolNight` | -22 / -4 | -0.3 / -18 / (0.82, 0.88, 1.00) |

- `ApplySceneLighting()` sets the **active** scene's `RenderSettings` (T28's builder calls it after `NewScene`; nothing calls it in T21): `skybox = RenderMaterialLibrary.Get("M_Sky")`; `fog = true`, `fogMode = FogMode.ExponentialSquared`, `fogDensity = 0.008f`; `fogColor` = the Day phase (`phases[1]`) `fogColor` of the `ArtPaletteDef` at `AssetPaths.ArtPaletteAsset`; `ambientMode = AmbientMode.Flat`, `ambientLight` = the Day `toonAmbient` (only the remaining Lit/Particle materials use it; Toon uses `_ToonAmbient`). A missing asset -> `InvalidOperationException`. Fog is never toggled at runtime (C 6.4).
- `ContentBuilder.Build` hook: one line `RenderingSetupBuilder.Build();` after T14's `FishingContentBuilder.Build()` line.
- `PrefabBuilder` (shared edit): on the `PlayerRigPaths.Camera` object, `BuilderUtil.GetOrAdd<UniversalAdditionalCameraData>(go).renderPostProcessing = true;` (C 7.21). Nothing else on the camera changes.
- `AssetPaths` T21 block: `RenderingFolder = "Assets/_Project/Content/Rendering"`, `WarmVolumeProfile = RenderingFolder + "/VP_WarmSunset.asset"`, `CoolVolumeProfile = RenderingFolder + "/VP_CoolNight.asset"`.

### T21 tests

`Validation_Shaders_Pass` (T20) now covers four shaders. The log must show `FG SHADERS checked=4`.

`Tests/EditMode/RenderingSetupTests.cs` (addition; class `RenderingSetupTests`):
- `RenderingSetup_WorldMaterials_UseExpectedShadersAndSettings`:
  - each 6.2 world material has the shader in T21.4;
  - `M_Water_Surface` colours and floats equal the `WaterSettingsDef` asset (1e-4);
  - `M_Particle.renderQueue == 3000` and the keyword `_SURFACE_TYPE_TRANSPARENT` is enabled;
  - `M_Marker.renderQueue == 3100`.
- `RenderingSetup_VolumeProfiles_OnlyColorAdjustmentsAndWhiteBalance`: both assets load; `components.Count == 2`; the types are exactly `ColorAdjustments` and `WhiteBalance`; each component is a sub-asset (`AssetDatabase.IsSubAsset`); `TryGet<Tonemapping>` is false.
- `PlayerPrefab_Camera_RenderPostProcessingOn`: the `PlayerFP` camera's `UniversalAdditionalCameraData.renderPostProcessing` is true.

### T21 steps

1. Write the three shaders. Run `test-edit -Filter "Validation_Shaders_Pass"`. Expected: PASS and `checked=4` in the log. A shader error shows as FAIL with the shader, pass and line.
2. Write `RenderingSetupTests`. `compile`, then `test-edit -Filter "RenderingSetupTests"`. Expected: FAIL (assets missing).
3. Write `BuildWorldMaterials`, `RenderingSetupBuilder`, the hook, the camera edit and `AssetPaths`. `build`. Expected: `FG BUILDALL PASS`. Rerun the filter. Expected: PASS.
4. `git diff --stat`: new materials, profiles and prefab only. `git restore Assets/_Project/Scenes/*.unity`. A second `build` is clean.
5. Full `test-edit`; `test-play -TimeoutMinutes 15`.
6. Commit:
```
git add Assets/_Project/Shaders/Water.shader Assets/_Project/Shaders/Water.shader.meta Assets/_Project/Shaders/Line.shader Assets/_Project/Shaders/Line.shader.meta Assets/_Project/Shaders/GradientSky.shader Assets/_Project/Shaders/GradientSky.shader.meta Assets/_Project/Editor/Builders/Render/RenderingSetupBuilder.cs Assets/_Project/Editor/Builders/Render/RenderingSetupBuilder.cs.meta Assets/_Project/Editor/Builders/RenderMaterialLibrary.cs Assets/_Project/Editor/Builders/ContentBuilder.cs Assets/_Project/Editor/Builders/PrefabBuilder.cs Assets/_Project/Editor/Builders/AssetPaths.cs Assets/_Project/Content/Materials/M_Water_Surface.mat Assets/_Project/Content/Materials/M_Water_Surface.mat.meta Assets/_Project/Content/Materials/M_Line.mat Assets/_Project/Content/Materials/M_Line.mat.meta Assets/_Project/Content/Materials/M_Sky.mat Assets/_Project/Content/Materials/M_Sky.mat.meta Assets/_Project/Content/Materials/M_Particle.mat Assets/_Project/Content/Materials/M_Particle.mat.meta Assets/_Project/Content/Materials/M_Marker.mat Assets/_Project/Content/Materials/M_Marker.mat.meta Assets/_Project/Content/Rendering.meta Assets/_Project/Content/Rendering/VP_WarmSunset.asset Assets/_Project/Content/Rendering/VP_WarmSunset.asset.meta Assets/_Project/Content/Rendering/VP_CoolNight.asset Assets/_Project/Content/Rendering/VP_CoolNight.asset.meta Assets/_Project/Prefabs/PlayerFP.prefab Assets/_Project/Tests/EditMode/RenderingSetupTests.cs Assets/_Project/Tests/EditMode/RenderingSetupTests.cs.meta
git commit -m "m2(render): Water, Line and GradientSky shaders, world materials, volume profiles, scene lighting, camera post" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T21 acceptance (reviewer)

- `Water.shader` matches T21.1. Its wave term comes only from `FishGameCommon.hlsl`. Line and Sky follow T21.2/T21.3 (formulas, the C 7.21 dash, pattern not hue).
- `Validation_Shaders_Pass` is green with `checked=4` in a `-nographics` run.
- Volume profiles keep their components as sub-assets (the file contains both component objects). No Tonemapping.
- `ApplySceneLighting` matches C 6.4 (ExponentialSquared, 0.008, skybox `M_Sky`).
- Camera post-processing is on. `PlayerFP` hash unchanged. A second build is clean.

### T21 manual check (controller, after the commit)

Ask the user to look at the inspector previews of `M_Water_Surface`, `M_Line` and `M_Sky` and the `PlayerFP` prefab, and screenshot them. Expected: banded Toon bodies; water colour; a sky gradient (globals unset, so default black until T22). The full lake look is T28. This is the skeleton's "first screenshot check".

---

## T22 - World presentation: PaletteGlobals, WaterTimeDriver, RippleField

| | |
|---|---|
| Goal | The scene-level presentation drivers: palette and day-phase globals, the water clock and waves (C# and GPU on one clock), and the ripple slots. |
| Owned files (C 12) | `Presentation/World/PaletteGlobals.cs`, `Presentation/World/WaterTimeDriver.cs`, `Presentation/World/RippleField.cs`, `Editor/Builders/Render/PresentationSceneObjects.cs`; generated `Scenes/Spot_LakeDock.unity`. `Tests/PlayMode/PresentationWorldTests.cs` (whitelisted, ruling F-Q6). |
| Shared edits (C 2.6) | `LakeDockBuilder.cs`: one hook line `PresentationSceneObjects.AddTo(scene);` right after T19's `FishingSceneObjects.AddTo(scene);`. |
| Depends on | T12, T13, T21 committed. |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: #6 (this task really changes the lake scene: commit it), #8, #9, #14, #15, #17, #18. Every class has the comment `// Authority: local only (every peer); never writes gameplay state.` (C 7.22).

### T22.1 `PaletteGlobals` (C 7.22)

- `[ExecuteAlways]`. Private `[SerializeField] ArtPaletteDef palette` (set by the builder, F8). At runtime, a null field falls back to `ServiceRoots.App?.TryGet<ContentRegistry>()?.ArtPalette`. Still null -> no-op (no warning).
- `public void Apply()`:
  - `Shader.SetGlobalVectorArray(ShaderGlobals.Palette, v)` where `v = palette.PaletteVectors(palette.M2Season)` (length 16). Each entry is linearised when `QualitySettings.activeColorSpace == ColorSpace.Linear` (F3). Reuse one cached `Vector4[16]`. `PaletteVectors(season)` allocates, so `Apply()` runs from `OnEnable` and the editor `OnValidate` only, never from `Update` or `LateUpdate` (C 16.2).
  - Day phase = `palette.Phases[1]` (M2 has no clock; M3 adds `DayNightController`):
    - `Shader.SetGlobalColor` for `ShaderGlobals.ToonShade` = `toonShade`, `ToonAmbient` = `toonAmbient`, `SkyTop`, `SkyHorizon`, `SkyGround`;
    - only when `Application.isPlaying`: `RenderSettings.fogColor` = `fogColor` and `RenderSettings.fogDensity` = `fogDensity` (Day 0.008). In edit mode these writes would rewrite the fog of whatever scene is active (validators open the lake scene; `OnValidate` fires during `build`). Never `RenderSettings.fog`: the fog switch is not toggled (C 6.4).
  - Property ids are cached with `Shader.PropertyToID` (from the `ShaderGlobals` constants; no string literals).
- Calls `Apply()` from `OnEnable`, and from `OnValidate` in the editor (palette tweaks show in the scene view). Edit mode pushes globals only, which is harmless to tests.

### T22.2 `WaterTimeDriver` (C 7.22, C M2-D12)

- **Not `[ExecuteAlways]`.** It never touches `WaterSurface` statics outside Play mode. Validators open the lake scene inside the EditMode test process, so an edit-mode `OnEnable` would make `WaterSurface` wavy and break `WaterSurface_*` tests depending on run order.
- `[DefaultExecutionOrder(-200)]` (F7).
- Private `[SerializeField] WaterSettingsDef waterSettings` (builder, F8); a null field falls back to the registry's `WaterSettings`.
- `OnEnable` (play):
  - `WaterSurface.Configure(waterSettings.ToWaveSet())`.
  - `WaterSurface.SetTimeSource(_now)`. `_now` is a delegate cached once in `Awake` (no per-call allocation) returning `(double)(float)(nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeSinceLevelLoad)`, with `nm = NetworkManager.Singleton`. The float cast is the C-Q4 parity rule: the C# wave height is evaluated with the very float value that is pushed to `_WaterTime`, so C# and HLSL agree regardless of session length.
  - Push `_Waves` once: entries 0..2 = `dirAmpLenSpeed[i]` (F1), entry 3 zero, through a cached `Vector4[4]`.
- `Update`: `Shader.SetGlobalFloat(ShaderGlobals.WaterTime, (float)WaterSurface.Now)`. C# readers (`BobberView`) use `WaterSurface.HeightAt`, which reads the same (float-cast) `Now` in the same frame. That gives F1 parity: one formula, one clock, one float value.
- `OnDestroy` (play): `WaterSurface.ResetToFlat()` (C 7.22). Push `_Waves` all zero and `_WaterTime` 0, so a later scene with no driver renders flat water.
- With no `WaterSettingsDef`: `ResetToFlat()` and push zero waves. No warning.

### T22.3 `RippleField` (C 7.22, spec 10.4)

- `public static RippleField Current { get; }`: set in `OnEnable`, cleared in `OnDisable` only if it still points at this instance, and reset on `SubsystemRegistration`.
- A private `Vector4[RippleMath.Slots]` (8) and `int _last = -1`.
- `public void Spawn(float x, float z, float strength)`:
  - `slot = RippleMath.NextSlot(_last)`; `_slots[slot] = (x, z, (float)WaterSurface.Now, strength)`; `_last = slot`.
  - Push the full array: `Shader.SetGlobalVectorArray(ShaderGlobals.Ripples, _slots)`.
  - Non-finite inputs or `strength <= 0` -> ignored.
- `OnEnable` and `OnDisable` push all-zero slots. Callers pass `RippleMath` constants (`CastLanding`, `Nibble`, `Bite`, `BiteFollow`, `Snap`, `Landed`, `Shove`).

### T22.4 `PresentationSceneObjects` (C 7.22; `Editor/Builders/Render/PresentationSceneObjects.cs`)

- `public static void AddTo(Scene scene)`:
  - Find a root named `Presentation` in `scene.GetRootGameObjects()`. If missing, create it and `SceneManager.MoveGameObjectToScene` it into `scene`.
  - `BuilderUtil.GetOrAdd` in order: `PaletteGlobals`, `WaterTimeDriver`, `RippleField`.
  - Load `AssetPaths.ArtPaletteAsset` and `AssetPaths.WaterSettingsAsset` here (after `NewScene`, C 2.8 #14); null -> `InvalidOperationException` naming the path.
  - Assign the `palette` and `waterSettings` fields with `SerializedObject` (F8).
  - `EditorSceneManager.MarkSceneDirty(scene)`.
- Hook: one line in `LakeDockBuilder` (see header).

### T22 tests

`Tests/PlayMode/PresentationWorldTests.cs` (addition; class `PresentationWorldTests`; harness `PlayModeHarness`, port per C 10.4, `[UnityTearDown]` `TearDownApp`):
- `PresentationWorld_LakeDock_PushesGlobalsAndConfiguresWaves` (after `StartSoloAndWaitForPlayer`):
  - `Shader.GetGlobalVectorArray("_FGPalette").Length == 16`, `"_Ripples"` 8, `"_Waves"` 4;
  - `_Waves[0..2]` equal the registry's `WaterSettings.DirAmpLenSpeed` (1e-5) and `_Waves[3] == Vector4.zero`;
  - for 3 sample points, `WaterSurface.HeightAt(x, z, t)` equals `WaveMath.HeightAt(registry.WaterSettings.ToWaveSet(), x, z, t)` (1e-5);
  - over 10 frames (`PlayModeHarness.WaitFrames`) `_WaterTime` increases and stays within 0.05 of `(float)WaterSurface.Now`;
  - a `GameObject` named `Presentation` exists in the active scene.
- `PresentationWorld_LeaveSession_ResetsWaterToFlat`: after `TearDownApp` (the scene unloads), `WaterSurface.HeightAt(3, 7, 1.0)` == 0 and `Shader.GetGlobalVectorArray("_Waves")` is all zero.

Getter names (`DirAmpLenSpeed`, `ArtPalette`, `WaterSettings`, `Phases`, `M2Season`) follow C 7.13's "one getter per field (PascalCase)". Read them from T13's code.

### T22 steps

1. Write `PresentationWorldTests`. `compile`, then `test-play -Filter "PresentationWorldTests" -TimeoutMinutes 15`. Expected: FAIL (globals not pushed, no `Presentation` root).
2. Write the three components, `PresentationSceneObjects` and the hook. `build`. Expected: `FG BUILDALL PASS`. Rerun the filter. Expected: PASS.
3. `git diff --stat Assets/_Project/Scenes`: `Spot_LakeDock.unity` changed (the new root). Keep it; restore other scenes if only churn.
4. Full `test-edit`, then `test-play -TimeoutMinutes 15`.
   - **Risk:** M1/T3 swim tests now see waves of up to 0.115 m. If one fails because it expects an exact water height of 0, do not edit it (not owned). Report it to the controller with the failing assertion; T28 fixes it under the integration-fix rule (ruling F-Q12).
5. Commit:
```
git add Assets/_Project/Presentation/World.meta Assets/_Project/Presentation/World/PaletteGlobals.cs Assets/_Project/Presentation/World/PaletteGlobals.cs.meta Assets/_Project/Presentation/World/WaterTimeDriver.cs Assets/_Project/Presentation/World/WaterTimeDriver.cs.meta Assets/_Project/Presentation/World/RippleField.cs Assets/_Project/Presentation/World/RippleField.cs.meta Assets/_Project/Editor/Builders/Render/PresentationSceneObjects.cs Assets/_Project/Editor/Builders/Render/PresentationSceneObjects.cs.meta Assets/_Project/Editor/Builders/LakeDockBuilder.cs Assets/_Project/Scenes/Spot_LakeDock.unity Assets/_Project/Tests/PlayMode/PresentationWorldTests.cs Assets/_Project/Tests/PlayMode/PresentationWorldTests.cs.meta
git commit -m "m2(world): PaletteGlobals, WaterTimeDriver, RippleField, Presentation scene root" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T22 acceptance (reviewer)

- Linearised palette (F3), full-length arrays, cached arrays (no per-frame allocation).
- `WaterTimeDriver` is play-mode only for `WaterSurface` and uses one clock for C# and GPU.
- `RippleField.Current` is reset on `SubsystemRegistration`.
- The hook line is right after T19's. Assets are loaded inside `AddTo`.
- Both PlayMode tests are green; the full suites are green and warning-free.

### T22 manual check (controller)

Ask the user to enter Play mode solo on the lake dock. Toon bodies now have the Day shade and ambient colours. Before T28 the water visual is still M1's plane, so the wave shader shows up at T28.

---

## T23 - Feedback and catch reveal: FishingFeedback, CatchRevealView, FishFallbackMesh

| | |
|---|---|
| Goal | Spec 9.11 feel on every peer: sounds, loops, ducking and ripples. On the owner: camera trauma, FOV kicks, rumble and captions. The snap silhouette jump. The catch reveal (species model or fallback mesh). |
| Owned files (C 12) | `Presentation/Feedback/FishingFeedback.cs`, `Presentation/Feedback/CatchRevealView.cs`, `Presentation/Feedback/FishFallbackMesh.cs`; a block of up to 12 lines in `PlayerFishingRigBuilder.AddPresentation` (ruling F-Q5; C 2.6 amended); generated `Prefabs/PlayerFP.prefab`. `Tests/PlayMode/FeedbackSoloTests.cs` (whitelisted, ruling F-Q6). |
| Shared edits (C 2.6) | `Editor/Builders/Fishing/PlayerFishingRigBuilder.cs`: the T23 block in `AddPresentation` (up to 12 lines, ruling F-Q5). |
| Depends on | T11, T19, T22 committed. |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: #9 (no warnings: a missing `RippleField.Current`, a missing model or a missing clip are silent), #17, plus the statics rule (0.1). Both components have `// Authority: local only. On PlayerFP; reads its FishingRodNetwork (Cue, ViewState, Announced).` (C 7.23). Neither may reference a T24 type (C 3 rule: no later-task types).

**Carry-forward (2026-10-12, C 16.2).** (1) Every loop `FishingFeedback` starts passes a `follow` transform, owner (2D) loops included (`Line_Creak` follows `rodTip`, `Dive_Thrum` follows `FeedbackAnchor`; `StartLoop` keeps a `null`-follow loop at the bus, where it would play forever after a scene change) and `OnDisable` calls `StopLoop` on every loop (idempotent, null-safe). (2) No cues while the rod is despawning: the controller already suppresses its own local Recall/Escaped cues on the despawn path (mini-fix batch, C 16.3); `FishingFeedback` additionally ignores `Cue` and `Announced` events once `OnDisable` has run or `!isActiveAndEnabled`, and plays nothing from the polled section while disabled. (3) Test against the driver exactly as in C 7.19 (`FishingRig` first argument, `rig.Log`).

### T23.1 `FishingFeedback` (C 7.23, spec 9.11)

**Setup.**
- `Awake`: cache `FishingRodNetwork rod` (same root), `PlayerCamera cam` and `Transform rodTip = transform.Find(PlayerRigPaths.RodTipAnchor)` (the static anchor, ruling BF-3: audio positions do not follow the bend).
- Create a private child `FeedbackAnchor`, placed every `LateUpdate` at `rod.ViewState.BobberPos`. It is the follow target for bobber-side loops, so this task needs no `BobberView`.
- Services are resolved lazily through `ServiceRoots.App?.TryGet` (`AudioBus`, `EventBus`, `ISettings`, `IInputService`, `ContentRegistry`). A missing service is a silent no-op.
- `OnEnable` subscribes `rod.Cue` and `rod.Announced`. `OnDisable` unsubscribes, stops every loop and, if owner, calls `SetRumble(0, 0)`.

**One-shots from `Cue` (every peer unless marked).**
- The sound id is `RodCueAudio.SfxFor(cue)`; null means no sound.
- Position: "rod" = `rodTip.position`, "bobber" = `ViewState.BobberPos`.
- The owner plays rod-side one-shots 2D (`Play2D(id, AudioCategory.Sfx)`) and everything else with `PlayAt`. Remotes always use `PlayAt`.
- Trauma, FOV, haptics and captions are **owner only**.
- Trauma and FOV are passed raw: `PlayerCamera` already scales them by `cameraMotion` (code: `_motion`; C 7.16 `AddFovKick`). Never pre-multiply.
- Rumble pulses are multiplied by `settings.cameraMotion` here (spec 9.11: camera motion 0 removes rumble).

| Cue | Where | Ripple (RippleField.Current?.Spawn at BobberPos) | Owner camera | Owner rumble pulse (low, high, s) | Caption (owner, settings.captions) |
|---|---|---|---|---|---|
| Equip, Stow, Ready, InvalidRelease, Recall | rod | - | - | - | - |
| ChargeStart | rod, `PlayAt(id, pos, 0.8)` | - | - | - | - |
| CastRelease | rod, pitch `lerp(0.9, 1.3, Machine.ChargeNorm)` on the owner (`FishingRodController` read-only), 1.0 on remotes | - | `AddFovKick(+2, 0.15)` | (0.10, 0.20, 0.08) | - |
| Landing | bobber | `CastLanding` | - | (0.10, 0.20, 0.08) | - |
| Nibble | bobber | `Nibble` | - | (0.00, 0.15, 0.05) | - |
| Bite | bobber; owner also `Play2D` (spec 9.4); stinger -> `Duck(1.0)` | `Bite`, then `BiteFollow` at +0.25 s and +0.5 s | `AddTrauma(0.15)` | (0.80, 0.80, 0.25) | `cap.bite` |
| HookSet | rod: burst of 4 `Reel_Click` 0.05 s apart | - | `AddTrauma(0.10)` | (0.40, 0.50, 0.15) | - |
| SideRun | bobber | - | - | (0.30, 0.00, 0.30) | - |
| Dive | bobber; the loop starts by polling (below) | - | - | - | - |
| Exhausted, Escaped | bobber | - | - | Escaped: (0.20, 0.20, 0.10) | Escaped (not `Recalling`): `cap.escaped` |
| Snapped | rod; stinger -> `Duck(1.0)` | `Snap` | `AddTrauma(0.5)` | (1.00, 1.00, 0.30) | `cap.snapped` |
| LineLanded | rod | `Landed` | - | - | - |

Owner gating: ChargeStart and the owner pitch need `FishingRodController` (same root, T18). Remotes never get ChargeStart.

**From `Announced` (every peer).**
- `Landed`: `PlayAt(Catch_Flop)` + `PlayAt(Catch_Cheer)` at the rod tip.
- `ReleasedCreelFull`: `Catch_Flop` only.
- Owner on `Landed`: `AddFovKick(-3, 0.3)` and a double rumble pulse (0.5, 0.5, 0.1), a 0.1 s gap, then again.
- `Lost`: nothing (the HUD toast covers it).
- `PityCast` (R65 / M2-D23, toast-only kind announced when a bad-luck-nudge cast is accepted): nothing here (the HUD shows `toast.pity`).

**Polled each frame (state-driven, so late joiners are right; F6).**
- `vs = rod.ViewState`; `hooked = vs.State == Hooked`.
- Rod reel speed: `registry.RodAt(rod.NetRodIndex.Value).ReelSpeed`, guarding the index range.
- `cue = TensionCues.Evaluate(vs.Tension, vs.Tension >= 0.999f, reelSpeed, 1f)`. Pass motion 1, because `PlayerCamera` scales trauma itself.
- `Line_Creak` loop while `hooked && cue.Band >= Creak`:
  - `StartLoop(LineCreak, follow: rodTip, spatial: !owner)`;
  - `SetLoop(loop, cue.CreakVolume01, cue.CreakPitch)`;
  - stop otherwise.
- `Dive_Thrum` loop while `hooked && (vs.Flags & FlagDive) != 0`, following `FeedbackAnchor`, `spatial: !owner`.
- `Reel_Click` ticks while `hooked && (vs.Flags & FlagReel) != 0`, at `cue.ReelClickRate` per second (accumulator; at most 1 per frame, at most 30/s):
  - owner `Play2D`, remote `PlayAt(rodTip)`;
  - pitch rises with tension in 0.3-0.6: `PlayAt` pitch `1 + 0.25 * saturate((t - 0.3) / 0.3)`. The owner's 2D tick has no pitch overload; that is accepted (ruling C-Q8).
- Owner while `hooked`: `cam.AddTrauma(cue.TraumaPerSecond * dt)`.
- Owner `cap.straining`: on the first entry into `Warn` or `Grace` per fight, then at most once per 3 s while it stays there.
- **Rumble (owner).**
  - Target = `max(continuous, active pulse)` per motor. `continuous` = `(cue.RumbleLow, cue.RumbleHigh)` while `hooked`, plus (0.6, 0.1) while diving.
  - Everything is multiplied by `settings.cameraMotion`.
  - Terminal states (Landed, Snapped, Escaped; ruling BF-2): `continuous` is 0 (it only exists while `hooked`), but the state's one-shot pulse still plays to completion: Snapped (1.0, 1.0, 0.30), Escaped (0.2, 0.2, 0.10), Landed double pulse (0.5, 0.5, 0.1) x 2 with a 0.1 s gap (at most 0.3 s, inside the 1.5 s beat). Only after the pulse has ended is the output forced to 0 (spec 9.2 / 9.11: off on terminal states).
  - Forced to 0 at once, cancelling any active pulse, when: `vs.State` is Idle; `!input.HasFocus`; or on disable. `FishingFeedback` owns all of this: `FishingRodController` does not call `SetRumble` on terminal states (net T18).
  - Call `SetRumble` only when a motor changes by more than 0.01, or when it becomes 0. T19's test asserts rumble 0 at the end.
- All loops stop when `vs.State` leaves Hooked (and on disable or despawn).

**Snap silhouette (every peer; C 7.23).** On `Cue == Snapped` with `arg = sizeClass` (0..2). The owner's local Snapped cue carries the roll's size class as well (BF-1: the machine's `Cue(Snapped)` effect sets it), so owner and remotes see the same size:
- A private child `SnapSilhouette` with `MeshFilter = FishFallbackMesh.Silhouette()`.
- Material: the serialized `silhouetteMaterial` (the shared asset `M_Toon_FishFallback`, `_JitterEncoded` 1; BF-7). No material instance is created. The grey tint (0.35, 0.37, 0.40) is baked into the silhouette mesh's vertex colours (`FishFallbackMesh.Silhouette()`, T23.3).
- Length 0.25 / 0.4 / 0.6 m for sizeClass 0/1/2.
- It jumps from `BobberPos` at water height: up `0.6 + 0.4 * sizeClass` m over 0.7 s on a parabola, pitching nose-over.
- On re-entry: `Splash_Small` at the point and `RippleField.Current?.Spawn(..., RippleMath.Snap)`. Then hide.
- A second Snapped during a jump restarts it.

### T23.2 `CatchRevealView` (C 7.23)

- Private serialized `fishMaterial` (`M_Toon_Fish`), `fallbackMaterial` (`M_Toon_FishFallback`, BF-7) and `sparkleMaterial` (`M_Particle`) (F8).
- A runtime child `CatchReveal` holds a MeshFilter and a MeshRenderer (shadows off, hidden). A child `ParticleSystem` is configured in code:
  - world space, emission off, sphere shape radius 0.2, speed 1.5-3, lifetime 0.4-0.8, size 0.03-0.07, gravity 0.3;
  - renderer material `sparkleMaterial`.
  - If `sparkleMaterial` is null, there are no particles (silent).
- `Announced` (every peer), kind `Landed` or `ReleasedCreelFull`:
  - Mesh: `registry.SpeciesAt(itemIndex).Model` when `!IsJunk` and the model is non-null; else `FishFallbackMesh.Get(registry, isJunk, itemIndex)`.
  - `sharedMaterials` = an array of `mesh.subMeshCount` copies of `fishMaterial` for a model mesh, or of the serialized asset `fallbackMaterial` (`M_Toon_FishFallback`, `_JitterEncoded` 1; BF-7) for a `FishFallbackMesh` mesh, because `M_Toon_Fish` has `_JitterEncoded` 0 for the Blender models (the spike perch has 5 submeshes until T29 replaces it; the new species models have one; harmless either way).
  - Scale: uniform, so that `mesh.bounds.size.z` = `LengthMm / 1000` m. `LengthMm == 0` (junk) -> 0.3 m.
  - Pose: world position `CatchHold.position + Vector3.up * 0.25`, rotation `Quaternion.LookRotation(rootRight, Vector3.up)` (nose to the player's right, belly down), updated every `LateUpdate` while shown. It is held at `CatchHold` on every peer (the owner's right arm is posed in camera space by `PlayerBody`, so it is in view).
  - 0.15 s scale-in, hold `FishingConstants.CatchHoldSeconds` (1.5 s), then hide.
  - Bursts: Normal/Good 12 sparkles, Trophy 30 (spec 9.11 "bigger burst"), and +20 gold-tinted sparkles when `CleanBonus`.
  - `ReleasedCreelFull`: after the hold, the fish falls 1.5 m over 0.5 s (or to water height), then `Splash_Small` + `RippleField.Current?.Spawn(..., RippleMath.Landed)`, then hide.
  - `Lost` and `PityCast`: no reveal.
  - A new announcement during a reveal replaces it. `OnDisable` hides it.

### T23.3 `FishFallbackMesh` (C 7.23)

- `public static Mesh Get(ContentRegistry registry, bool isJunk, ushort itemIndex)`:
  - Cached per `(isJunk, itemIndex)`. The cache is cleared on `SubsystemRegistration`; the meshes are destroyed then (`Object.Destroy` if playing).
  - Built with `MeshBuilder(meshSeed = (isJunk ? 0x4A000000u : 0x46000000u) + itemIndex, 0.04f)`.
  - Colour pass after `ToMesh`: the body uses slot 0 and the fins/tail slot 1. Read UV1 (F2): slot 0 -> `colorA.linear`, slot 1 -> `colorB.linear`, multiplied into the existing HALF-scale grey `c.r` (so the stored colour is `colorA.linear * 0.5 * (1 +/- j)`, at most 0.75 per channel: no clamp, UNorm8-safe; F3, BF-7). The mesh is MeshBuilder-built, so it must be drawn with a material whose `_JitterEncoded` is 1 (the shader doubles per channel): `CatchRevealView` uses the asset material `M_Toon_FishFallback` (`fallbackMaterial`) for fallback meshes (see T23.2). Junk uses `JunkDef.Color` the same way.
  - Normalised so that `bounds.size.z == 1` (nose +Z, centred).
- Shapes (`FishSpeciesDef.BodyShape`):
  - Round: icosphere body squashed (x 0.35, y 0.8) + tail prism + dorsal fin.
  - Long: capsule body + tail.
  - Flat: tall thin box body + tail.
  - Eel: long thin capsule.
  - Squid: cylinder mantle + 6 thin prisms.
  - Junk `old_boot`: two boxes (shaft + foot). Junk `tin_can`: cylinder + top ring.
  - Pick the junk shape by `JunkDef.BodyShape`: Long -> boot, Round -> can.
- `internal static Mesh Silhouette()`: a cached Round fish for `FishingFeedback`, drawn with the asset material `M_Toon_FishFallback` (BF-7). Colour pass after `ToMesh`: each vertex colour = `(0.35, 0.37, 0.40) * c.r` of MeshBuilder's half-scale grey, so the shader's doubling gives the grey tint (0.35, 0.37, 0.40) times the face jitter (no material change).

### T23.4 `AddPresentation` block (ruling F-Q5: up to 12 lines)

Proposed T23 block, in C 6.3 order:
- `GetOrAdd<FishingFeedback>` and `GetOrAdd<CatchRevealView>`;
- set `FishingFeedback.silhouetteMaterial` and `CatchRevealView.fallbackMaterial` from `RenderMaterialLibrary.Get("M_Toon_FishFallback")`, `CatchRevealView.fishMaterial` from `Get("M_Toon_Fish")` and `CatchRevealView.sparkleMaterial` from `Get("M_Particle")` via `SerializedObject` (F8). Use a private `SetObjectRef(Component, string, Object)` helper in `PlayerFishingRigBuilder` if T19 has none. At most 12 lines in total; T24 owns the rest of the body.

### T23 tests

`Tests/PlayMode/FeedbackSoloTests.cs` (addition; class `FeedbackSoloTests`; the G9 fixture of C 7.19):
- Reuse T19's `internal static FishingPlayModeDriver` (in `FishingSoloTests.cs`, same PlayMode assembly; section E plan): `InstallFakeInput`, `Step`, `Arrange` and its cast-to-land script. Do not copy it (its member names are frozen after T19, ruling F-Q7).
- `FeedbackSolo_CastBiteLand_RippleCaptionAndReveal` (one scripted cast to land, forced 0.1 s bite, forced bluegill):
  - (a) Within 1.5 s after the bobber lands, `Shader.GetGlobalVectorArray("_Ripples")` has a slot with `w == RippleMath.CastLanding` and `(x, z)` within 0.5 m of `NetRodState.Value.BobberPos`.
  - (b) An `EventBus` subscription to `CaptionRequested` (`ISettings.Current.captions == true`) receives `cap.bite` at the bite.
  - (c) Within 0.5 s of the `CatchAnnounced` bus message, the root's `CatchReveal` child is active, with an enabled `MeshRenderer`, non-null mesh and `sharedMaterials.Length == mesh.subMeshCount`. It is hidden again within 2.5 s.
  - (d) At Idle the fake input's last rumble is (0, 0).

### T23 steps

1. Write `FeedbackSoloTests`. `compile`; run the filter with `-TimeoutMinutes 15`. Expected: FAIL (no ripple, caption or reveal).
2. Write the three classes and the `AddPresentation` lines. `build`. Expected: PASS for `FG BUILDALL` and the filter.
3. Full `test-edit` and `test-play -TimeoutMinutes 15`, including T19's `SoloHost_CastBiteFight_Lands` (rumble 0, no warnings). `git restore Assets/_Project/Scenes/*.unity`.
4. Commit:
```
git add Assets/_Project/Presentation/Feedback.meta Assets/_Project/Presentation/Feedback/FishingFeedback.cs Assets/_Project/Presentation/Feedback/FishingFeedback.cs.meta Assets/_Project/Presentation/Feedback/CatchRevealView.cs Assets/_Project/Presentation/Feedback/CatchRevealView.cs.meta Assets/_Project/Presentation/Feedback/FishFallbackMesh.cs Assets/_Project/Presentation/Feedback/FishFallbackMesh.cs.meta Assets/_Project/Editor/Builders/Fishing/PlayerFishingRigBuilder.cs Assets/_Project/Prefabs/PlayerFP.prefab Assets/_Project/Tests/PlayMode/FeedbackSoloTests.cs Assets/_Project/Tests/PlayMode/FeedbackSoloTests.cs.meta
git commit -m "m2(feedback): FishingFeedback cues, loops, rumble, captions, snap jump; catch reveal and fallback mesh" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T23 acceptance (reviewer)

- The cue table is implemented row by row. Trauma and FOV are not pre-scaled; rumble is scaled by `cameraMotion` and zeroed on terminal states, focus loss and disable.
- One-shots come only from `Cue`/`Announced`; loops come from polling (F6).
- Captions only on the owner and only when `settings.captions` is on. No T24 type is referenced.
- The reveal covers every submesh, the scale rule and the `ReleasedCreelFull` drop.
- Fallback colours are linearised (the one vertex-colour rule shared with the Blender fish, ruling F-Q3); statics are reset.
- Tests are green and warning-free.

### T23 manual check (controller, MPPM 2 players)

- Player B hears A's bite splash and sees the snap jump and the reveal.
- B gets no rumble or captions for A's fish.
- With camera motion 0 on A: no shake, no FOV kick and no rumble.

---

## T24 - Rod presentation: RodPresenter, LineView, BobberView, SplashFx, CastMarkerView

| | |
|---|---|
| Goal | The visible rod, line, bobber, splashes and owner cast marker on every peer from `ViewState`, with the arc-once and snap-on-spawn rules; the `AddPresentation` body. |
| Owned files (C 12) | `Presentation/Rod/RodPresenter.cs`, `LineView.cs`, `BobberView.cs`, `SplashFx.cs`, `CastMarkerView.cs`; the body of `PlayerFishingRigBuilder.AddPresentation` (T23's lines kept); generated `Content/Meshes/Marker_Valid.asset`, `Marker_Invalid.asset`, `Prefabs/PlayerFP.prefab`. `Tests/EditMode/PlayerPresentationRigTests.cs`, `Tests/PlayMode/RodViewSoloTests.cs` (whitelisted, ruling F-Q6). |
| Shared edits (C 2.6) | `PlayerFishingRigBuilder.cs` (`AddPresentation` body). |
| Depends on | T22, T23 committed. |
| Implementer | Sonnet |
| Reviewer | Opus high |

C 2.8 that bite: #4 (no NetworkBehaviour added; C 6.3 order), #9, #10 (remote view from NetVars only), #17, #19 (Toon renderers here use MeshBuilder meshes). Each class carries `// Authority: local only (every peer; CastMarkerView renders on the owner only). Reads FishingRodNetwork.ViewState; never writes gameplay state.`

**Carry-forward (2026-10-12, C 16.2, ruling T19-R1).** `RodPresenter` bends only `TipVisual` and the `Seg*` transforms: it must NEVER move or re-parent `TipAnchor` (the controller reads it for `RodTipPosition`, the fish point and `TipToBobber`, and the host slack `FishingConstants.HookedLineSlack` is derived from its measured reach). The rod REST pose (local rotation of `Rod`, so that in the owner arm pose the rod points camera-forward pitched up about 35 deg) is set by the T19-fix in `PlayerFishingRigBuilder`, not by T24; T24 keeps the rest pose and only adds bend on top. PlayMode tests for T24 reuse the driver as written in C 7.19 (every method takes a `FishingRig` first; `rig.Log` is the log watch; `FishingPlayModeDriver.TearDown(rig)` after the T19-fix).

### T24.1 `RodPresenter` (C 7.24, spec 10.5)

- `public Transform Tip { get; }` = `transform.Find(PlayerRigPaths.RodTipVisual)`: the BENT visual tip (ruling BF-3). `RodPresenter` also caches `Transform anchor = transform.Find(PlayerRigPaths.RodTipAnchor)` for reading only. It bends `TipVisual` and the segments, and **never writes `TipAnchor`**, which stays static: gameplay (`FishingRodController.RodTipPosition`, the fish point, `TipToBobber`) reads the anchor, so presentation cannot feed back into gameplay (C 2.8 #17).
- `Awake` caches `Seg0..Seg5` (`PlayerRigPaths.RodSegmentPrefix + i`) and the rest pose of each segment and of `Tip` (= `TipVisual`; local position and rotation under `Rod`).
- **Visibility:** the segment renderers are enabled iff `rod.Player.Tool == ToolKind.Rod`. Apply it on `ToolChanged` and once after spawn (late joiners: `NetTool` is synced on spawn). Toggle renderers only, never `SetActive`, because `TipAnchor` must stay valid for the controller.
- **Bend (LateUpdate, order 250).**
  - The chain links are the rest pivot-to-pivot offsets `d_i = restPos_{i+1} - restPos_i` (`restPos_6` = the rest position of `Tip`, i.e. `TipVisual`).
  - With accumulated angle `a_i = sum_{j<=i} bendDeg * w_j` and `R_i = AngleAxis(a_i, axis)`: `pos_0 = restPos_0`, `pos_{i+1} = pos_i + R_i * d_i`, `localRot_i = R_i * restRot_i`, and `Tip` (`TipVisual`) is set to `pos_6`.
  - Weights `w = (0.05, 0.10, 0.15, 0.20, 0.22, 0.28)` (sum 1; most bend at the tip).
  - `bendDeg = 50 * TensionCues.Evaluate(t, t >= 0.999f, reelSpeed, 1f).RodBend01` while Hooked, otherwise 0. Use motion 1 here: the bend is the no-HUD tension readout and must not vanish at camera motion 0 (spec 9.11 removes only shake, FOV kicks, bob and rumble). `Shake` below comes from a second call with the real `settings.cameraMotion`.
  - `axis` (in `Rod` local space): `cross(rodForward, toBobber)` with `toBobber = ViewState.BobberPos - anchor.position` (the static `TipAnchor`, so the bend does not depend on its own previous result). Use local X when the cross product is near zero or no line is out.
- **Extras.**
  - Nibble cue: a 4 deg wobble on local X, `4 * sin(40 t) * (1 - t / 0.3)` for 0.3 s (spec 9.4).
  - HookSet cue: a 15 deg upward jerk easing back over 0.25 s.
  - Warn shake: when `Evaluate(...).Shake` (already 0 at camera motion 0), add +-1.5 deg Perlin jitter at 25 Hz.

### T24.2 `BobberView` (C 7.24, F6)

- `public Vector3 Position { get; }` (the world position last set).
- The `Bobber` child (`PlayerRigPaths.Bobber`) gets its world position every `LateUpdate` (order 240). The renderer is hidden while no line is out.
- **Baseline (snap on spawn).** On the first `LateUpdate` with `rod.IsSpawned`, record `_lastState = vs.State` and `_arcCastId = vs.CastId`. If the state is line-out (Casting, Waiting, Bite, Hooked, Escaped), place the bobber at once at its steady position. No arc and no cue replay.
- **Arc rule (C 7.24 "on a CHANGE into Casting"; C 9; spec 9.3).**
  - Start an arc only when `_lastState != Casting && vs.State == Casting && vs.CastId != _arcCastId`. Then set `_arcCastId = vs.CastId`.
  - A repeated or resent Casting value never restarts it. Nothing else starts an arc.
  - Arc data, captured at the start:
    - `start = RodPresenter.Tip.position`;
    - `end = (BobberPos.x, WaterSurface.HeightAt(x, z), BobberPos.z)`;
    - `distance` = horizontal root-to-end;
    - `flight = CastMath.FlightTime(distance)`;
    - `apex = CastMath.Apex(distance, CastMath.PitchApexMult(pitchDeg))`.
  - `pitchDeg` = `PlayerCamera.PitchDeg` on the owner, `PitchQuant.Dequantize(rod.Player.NetPitchQ.Value)` on remotes (the C 7.24 "NetPitchQ" is the M1 `PlayerNetState.NetPitchQ`).
  - Each frame: `CastMath.ArcPoint(elapsed / flight, start, end, apex, out x, y, z)`.
  - The arc runs to completion even if the state moves on to Waiting meanwhile. `BobberPos` changes are ignored until it ends.
- **Steady positions after the arc.** With `h = WaterSurface.HeightAt(x, z)` at the current `BobberPos` (spec 10.5 "bobber bob from WaveMath"):

  | State / flag | Position |
  |---|---|
  | Waiting | `(x, h + 0.02, z)` |
  | Nibble cue | dip `-0.04 * sin(pi * t / 0.25)` for 0.25 s |
  | Bite | plunge to `h - 0.25` over 0.1 s and stay |
  | Hooked | the fish point `(x, h - 0.1, z)` |
  | Hooked + `FlagDive` | `h - 0.35` (line points down) |
  | Hooked + `FlagExhausted` | `h + 0.03` plus a slow roll (the fish rolls on the surface) |

  Positions ease toward their target at `1 - exp(-20 dt)`. Remotes' `BobberPos` is already smoothed by `FishingRodNetwork`.
- **Recall.** A recall arc runs on `Cue == Recall`, on `Escaped` with `FlagRecall`, or on a transition from a line-out state to Idle while the bobber is shown and not already hidden. It is a 0.4 s `ArcPoint` from the current position to `Tip` with apex 0.5 m. Then hide.
- **Hide at once** on Landed (the reveal takes over) and on Snapped (the silhouette jump takes over).
- `_lastState = vs.State` at the end of every frame.

### T24.3 `LineView` (C 7.24, spec 10.5, 9.11)

- The `Line` child `LineRenderer`. `Awake`:
  - `positionCount = 24`, `useWorldSpace = true`, `textureMode = Stretch`;
  - material instance = `lineRenderer.material` (one per player);
  - a cached `Vector3[24]`.
- Visible while `BobberView`'s renderer is shown (Casting, Waiting, Bite, Hooked, the Escaped beat, the recall arc). On Snapped: a 0.2 s whip, where the end point slides from `Position` to `Tip` with full sag, then hide. Otherwise hidden (C 7.24 "hidden while no line is out").
- Curve (LateUpdate, order 260), a quadratic Bezier:
  - `P0 = RodPresenter.Tip.position` (the bent `TipVisual`, BF-3), `P2 = BobberView.Position`, `L = |P2 - P0|`;
  - `sag = (1 - tension) * 0.15 * L`;
  - `P1 = (P0 + P2) / 2 + Vector3.down * 2 * sag`, so that the curve midpoint is `sag` below the chord midpoint;
  - point `k` at `u = k / 23`.
  - Tension: `ViewState.Tension` while Hooked, else 0.
- Material: `_Tension` (set when it changes by more than 0.005) and `_DashCount = max(1, L / 0.3)` (constant dash length).
- Width: base 0.012 m. In the Warn or Grace band, pulse `base * (1 + 0.5 * (0.5 + 0.5 * sin(2 pi 4 t)))` (C 7.21: the width pulse lives here).

### T24.4 `SplashFx` (C 7.24, spec 9.11 phase cues)

- One runtime child `SplashFx` with a world-space `ParticleSystem` configured in code:
  - emission off, max 200, gravity 1, cone shape 25 deg up;
  - renderer material = the serialized `particleMaterial` (`M_Particle`, F8);
  - start colour (0.85, 0.93, 1.0, 0.9).
- Bursts use `Emit(EmitParams { position, velocity }, n)`.
- From `Cue`, at `BobberPos` with water height:

  | Cue | Burst |
  |---|---|
  | Landing | 12 |
  | Nibble | 4 small |
  | Bite | 30 + 10 droplets |
  | HookSet | 10 droplets |
  | Snapped | 25 |
  | LineLanded (catch) | 15 |
  | Escaped (not `Recalling`) | 10 slow rising bubbles, size 0.03, gravity -0.2 |

- **Polled (late-join safe):**
  - Side run, while Hooked and `FlagBurst` without `FlagDive`: a sideways column, 20/s at the fish point, velocity up 2.5 + sideways 1.5 m/s. Sideways = perpendicular to the horizontal player-to-bobber line, signed by the frame-to-frame change of `ViewState.Lateral` (positive lateral = right of the cast line, `FishPointMath`). Spec 9.11: a dive has no sideways splash.
  - `FlagExhausted`: 3 particles every 0.6 s.
- A null material means no particles (silent).

### T24.5 `CastMarkerView` (C 7.24, C 6.3)

- Owner only (F9). Remotes, once `rod.IsSpawned && !rod.IsOwner`: renderers off, `enabled = false`. Prefab default: both marker renderers disabled, so a remote never flashes one.
- It reads the same root's `FishingRodController`: `MarkerVisible`, `MarkerValid`, `MarkerPosition`, `ChargeNorm`.
- `LateUpdate` (order 270), while `MarkerVisible`:
  - place `CastMarker` at `(MarkerPosition.x, WaterSurface.HeightAt + 0.03, MarkerPosition.z)`, identity rotation (flat);
  - scale `(1 + 0.03 * horizontal distance) * (1 + 0.1 * ChargeNorm)`. It slides with the charge because `MarkerPosition` follows it;
  - `Valid` child shown iff `MarkerValid`, `Invalid` child otherwise. Shape differs, not hue only;
  - colour via the owner's material instance: white when valid, grey (0.55) when invalid.
- On `Cue == InvalidRelease`: flash the `Invalid` shape on and off every 0.075 s for 0.45 s, even if the marker would be hidden.

### T24.6 `AddPresentation` body and marker meshes

- Keep T23's lines first (C 6.3 order). Then `GetOrAdd` in order: `RodPresenter`, `LineView`, `BobberView`, `SplashFx`, `CastMarkerView`.
- Rig materials: no reassignment here. T19's `RigMaterial(preferred, fallback)` (section E plan) already picks `M_Toon_Base` for `Seg0..Seg5` and `Bobber`, and `M_Line` for `Line`, once T20/T21 have built them. `PlayerPrefab_RodBobberAndLine_UseFinalMaterials` verifies it (ruling F-Q8). `LineView` sets `textureMode`, width and visibility at runtime.
- `SplashFx.particleMaterial` = `M_Particle` (F8).
- `CastMarker` child at root (`PlayerRigPaths.CastMarker`) with children `Valid` and `Invalid`: MeshFilter + MeshRenderer, `M_Marker`, shadows off, **renderer disabled**. Meshes built with `MeshBuilder` in the XZ plane at y 0 and saved in place (the T20.6 pattern) as `Marker_Valid.asset` and `Marker_Invalid.asset`:
  - Valid: a solid ring of 24 quads, inner radius 0.42, outer 0.5, plus a centre dot (Prism r 0.06, h 0.01, 8 sides). Seed `0x4D4B0001`.
  - Invalid: a dashed ring (every other one of the 24 quads) plus a cross (two boxes 0.5 x 0.01 x 0.06 rotated +-45 deg about Y). Seed `0x4D4B0002`.
- No NetworkBehaviour is added. The C 6.3 component order holds.

### T24 tests

`Tests/EditMode/PlayerPresentationRigTests.cs` (addition; class `PlayerPresentationRigTests`; loads `AssetPaths.PlayerPrefab`; compares components by `GetType().Name` because EditMode cannot reference Presentation):
- `PlayerPrefab_PresentationComponents_InContractOrder`: after `FishingRodController`, the root components are exactly, in order, `FishingFeedback, CatchRevealView, RodPresenter, LineView, BobberView, SplashFx, CastMarkerView`. No component after `FishingRodController` is a `NetworkBehaviour`.
- `PlayerPrefab_RodBobberAndLine_UseFinalMaterials`: `Seg0..5` and `Bobber` use `M_Toon_Base`; `Line` uses `M_Line` with `positionCount == 24` and `useWorldSpace`.
- `PlayerPrefab_CastMarker_HasValidAndInvalidShapesDisabled`: `CastMarker/Valid` and `CastMarker/Invalid` exist with `Marker_*.asset` meshes, `M_Marker`, and disabled renderers.

`Tests/PlayMode/RodViewSoloTests.cs` (addition; class `RodViewSoloTests`; G9 fixture through T19's `FishingPlayModeDriver`):
- `RodViewSolo_CastToLand_RodBobberLineAndMarker`:
  - After Equip, the `Seg0` renderer is enabled. In Ready, a `CastMarker` child renderer is enabled.
  - During flight, the `Bobber` y peaks at least 0.4 m above the landing water height (the apex minimum is 0.6 m times a pitch multiplier of at least 0.7).
  - At `Waiting`, the bobber is within 0.25 m horizontally of `NetRodState.Value.BobberPos` and within 0.1 m vertically of `WaterSurface.HeightAt + 0.02`, and the `CastMarker` renderers are disabled.
  - The `Line` `LineRenderer` is enabled with `positionCount 24`; point 0 is within 0.05 m of `Body/ArmR/Hand/Rod/TipVisual` (BF-3; equal to `TipAnchor` at rest) and point 23 within 0.05 m of the bobber.
  - After the land and the return to Idle, the line renderer is disabled.

The remote rules (arc once per `CastId`; snap on spawn, no arc replay for a late joiner) cannot be exercised in solo. The reviewer reads them against T24.2, and the controller checks them in MPPM (below).

### T24 steps

1. Write both test files. `compile`; run `test-edit -Filter "PlayerPresentationRigTests"` and `test-play -Filter "RodViewSoloTests" -TimeoutMinutes 15`. Expected: FAIL.
2. Write the five components and the `AddPresentation` body. `build`. Rerun both filters. Expected: PASS.
3. Full `test-edit`, `test-play -TimeoutMinutes 15`. `git restore Assets/_Project/Scenes/*.unity`. A second `build` is clean.
4. Commit:
```
git add Assets/_Project/Presentation/Rod.meta Assets/_Project/Presentation/Rod/RodPresenter.cs Assets/_Project/Presentation/Rod/RodPresenter.cs.meta Assets/_Project/Presentation/Rod/LineView.cs Assets/_Project/Presentation/Rod/LineView.cs.meta Assets/_Project/Presentation/Rod/BobberView.cs Assets/_Project/Presentation/Rod/BobberView.cs.meta Assets/_Project/Presentation/Rod/SplashFx.cs Assets/_Project/Presentation/Rod/SplashFx.cs.meta Assets/_Project/Presentation/Rod/CastMarkerView.cs Assets/_Project/Presentation/Rod/CastMarkerView.cs.meta Assets/_Project/Editor/Builders/Fishing/PlayerFishingRigBuilder.cs Assets/_Project/Content/Meshes/Marker_Valid.asset Assets/_Project/Content/Meshes/Marker_Valid.asset.meta Assets/_Project/Content/Meshes/Marker_Invalid.asset Assets/_Project/Content/Meshes/Marker_Invalid.asset.meta Assets/_Project/Prefabs/PlayerFP.prefab Assets/_Project/Tests/EditMode/PlayerPresentationRigTests.cs Assets/_Project/Tests/EditMode/PlayerPresentationRigTests.cs.meta Assets/_Project/Tests/PlayMode/RodViewSoloTests.cs Assets/_Project/Tests/PlayMode/RodViewSoloTests.cs.meta
git commit -m "m2(rodview): rod bend, line, bobber arc and bob, splashes, owner cast marker" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### T24 acceptance (reviewer)

- **BobberView:** a baseline snap on the first spawned frame; an arc only on an observed change into Casting, with `vs.CastId != _arcCastId`; no other arc trigger; the arc completes regardless of later states; remote pitch from `NetPitchQ`.
- **RodPresenter:** visibility from `NetTool`. **LineView:** sag, dash count and width pulse as specified.
- **SplashFx:** the side-run column is polled and absent in dives. **CastMarkerView:** owner only, shape-coded, flashes on InvalidRelease.
- No gameplay writes. C 6.3 order. No new NetworkBehaviour.
- All tests are green and warning-free.

### T24 manual check (controller, MPPM 2-3 players)

1. B watches A cast: B sees one arc from A's rod tip to the landing, never a second arc for the same cast. A's marker is never visible to B.
2. C joins while A is Waiting, then while A is Hooked: C sees A's bobber at once at the right spot, with no arc and no splash burst; C sees the line and the rod bend.
3. A side run vs a dive is visible from B's view (sideways column vs plunge with the line pointing down).
4. Keyboard and gamepad both show the marker slide and the invalid flash.

---

## Contract questions (all closed by controller rulings of 2026-10-11; C 15)

1. **Wave direction and `_Waves` packing (closed, F-Q1/Q2, C-Q4).** 0 deg = +Z, `dir = (sin, cos)`; `_Waves[i] = (DirectionDeg, Amplitude, Wavelength, Speed)`, `[3] = 0`. Recorded in C 5.3 and 7.12.
2. **Palette slot channel (closed, F-Q1/Q2).** UV channel 1 (`TEXCOORD1`); recorded in C 7.12.
3. **Colour space (closed, F-Q3; supersedes G-Q7).** `PaletteGlobals` linearises `_FGPalette`; vertex colours are never converted, so the Toon shader treats them as linear. The Blender fish export `colors_type='LINEAR'` (the controller re-exports the staged `Art/Export/Fish_*.fbx` linear); `FishFallbackMesh` writes linear colours. Recorded in C 7.29 and 2.8 #19.
4. **ShaderValidator fallback (closed, F-Q4).** If the `-nographics` check fails, T20 owns a minimal `Tools/unity.ps1` change (graphics-capable mode for the validation run); C 2.6 amended.
5. **`AddPresentation` block (closed, F-Q5).** T23 may add up to 12 lines; C 2.6 amended.
6. **Added test files (closed, F-Q6).** Whitelisted: `ToonMaterialTests.cs` (T20), `RenderingSetupTests.cs` (T21), `PresentationWorldTests.cs` (T22), `FeedbackSoloTests.cs` (T23), `PlayerPresentationRigTests.cs` and `RodViewSoloTests.cs` (T24); C 12 amended. `test-play` from T22 on uses `-TimeoutMinutes 15`.
7. **Scripted cast helper (closed, F-Q7).** `FishingPlayModeDriver` member names are frozen after T19.
8. **Rig materials (closed, F-Q8).** `RigMaterial` fallback: T19 prefers the new material, else the M1 one; T24 only verifies.
9. **Toon keyword set (closed, F-Q9).** The keyword subset described in T20 is accepted.
10. **Unspecified looks (closed, F-Q10).** Accepted; the user judges them at T28.
11. **`_WaterTime` precision (closed, F-Q11, C-Q4).** Float drift accepted; the C# time source returns the float-cast value (parity rule).
12. **M1 swim tests and waves (closed, F-Q12).** T22 reports any broken M1 swim or water-height test; T28 fixes it under the integration-fix rule.
