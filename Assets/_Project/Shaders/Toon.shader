// Shaders/Toon.shader - FishGame/Toon (spec 10.3, C 7.20). Owned by T20. ASCII only.
// Albedo: vertexFactor = lerp(vertexColor.rgb, 2 * vertexColor.rgb, _JitterEncoded);
//         albedo = (_UsePalette > 0.5 ? _FGPalette[round(uv1.x)] : 1) * vertexFactor * _BaseColor.
// BF-7: MeshBuilder stores a half-scale grey 0.5 * (1 +/- jitter) and SmoothMeshBuilder a half-scale grey 0.5 * ao (UNorm8-safe), so every
// builder-made mesh is drawn with _JitterEncoded = 1 (props, lakebed/shore, rod, bobber, body slots, skin, FishFallbackMesh, silhouette);
// the Blender fish models on M_Toon_Fish use _JitterEncoded = 0 (their vertex colour is the linear albedo as stored).
// Lighting: band = smoothstep(mid - soft, mid + soft, saturate(dot(n, L)) * shadowAtten);
//           lit = albedo * lerp(_ToonShade, mainLight.color, band) + albedo * _ToonAmbient * 0.15; additional lights banded.
// Vertex colour is linear (F3): never converted here.
//
// R74 realistic-toon extension (look-dev 2026-10-12). Every new property defaults to "off", so a material that does not set them renders as before.
//  - Texture array: albedo *= lerp(1, sample(_FGAlbedoArray, uv, layer) * _FGArrayGain, _FGTexWeight). The layer is round(uv1.y). The mapping is
//    per material (R74 adoption): uv0 by default (metres from the mesh builder, or the fish UV tile), world triplanar (positionWS, blend by |n|^4)
//    with the _FG_TRIPLANAR keyword (toggle _UseTriplanar; M_Toon_Tri). uv1.z records which mapping a mesh was built for. Both are scaled by
//    _FGTiling (repeats per metre); _FGMacro multiplies in the same layer at 1/11 scale in world xz (tile-repeat breakup).
//    Layer 0 is neutral in every array, so a mesh without uv1.y (the flat-shaded MeshBuilder meshes) is untouched. The env array is linear
//    detail centred on 0.5 (gain 2, hue from the palette); the fish array is sRGB albedo (gain 1, layer 0 white).
//  - Light: the existing band (mid/soft; the realistic-toon materials use a wide soft ramp) plus a sky fill lerp(_SkyGround, _SkyTop, n.y) *
//    _SkyFill, a rim pow(1 - n.v, _RimPow) * _RimStrength tinted from the sky horizon (shadowed side) to the light (lit side), and a soft toon
//    highlight (_SpecStrength, _SpecPower) on the lit side only.
//  - No normal maps: the look gets its surface relief from the analytic smooth normals of SmoothMeshBuilder and the albedo/detail array
//    (the _NORMALMAP path and its tangent interpolator were removed in the review fixes).
//  - Ambient occlusion is baked into the vertex colour by the builders and the fish bake (no SSAO: it would force a DepthNormals prepass).
//
// Colour grade (asset study 2026-10-14, PEAK/RV "grey masks, colour from parameters"), on the materials with _FGGrade > 0 (M_Toon_Base, M_Toon_Tri;
// the fish, body, skin and brass keep 0 and render as before). Values come from the palette's per-season ToonGrade (FishGameCommon globals).
//  - Hue: albedo = HueRotate(albedo, FG_ObjectHueNoise() * _FGGradeParams.y * _FGGrade), per vertex, from the object origin (no material instances).
//  - Top: albedo *= lerp(1, _FGTopTint.rgb, smoothstep(_FGGradeParams.x, 1, n.y) * _FGTopTint.w * _FGGrade) (warmer, lighter upward faces).
//  - Bottom: the shade colour *= lerp(1, _FGBottomTint.rgb, _FGBottomTint.w * _FGGrade * lerp(_FGGradeParams.z, 1, saturate(-n.y))), so shadowed
//    and down-facing sides go cool blue-purple; the lit side is untouched. Multiplicative only: a dark phase stays dark.
//  ALU only (no texture sample, no keyword): the painted layers stay greyscale-multiplied by the palette as before.
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
        [NoScaleOffset] _FGAlbedoArray("Albedo/Detail Array (layer = uv1.y)", 2DArray) = "" {}
        _FGTexWeight("Texture Weight", Range(0, 1)) = 0
        _FGArrayGain("Array Gain", Range(0, 4)) = 1
        _FGTiling("Tiling (repeats per metre)", Float) = 1
        _FGMacro("Macro Breakup (world xz, 1/11 scale)", Range(0, 1)) = 0
        _SkyFill("Sky Fill", Range(0, 1)) = 0
        _SpecStrength("Toon Highlight Strength", Range(0, 1)) = 0
        _SpecPower("Toon Highlight Power", Range(4, 256)) = 48
        [Toggle(_FG_TRIPLANAR)] _UseTriplanar("World Triplanar Mapping", Float) = 0
        _FGGrade("Colour Grade (palette top / bottom tint, hue shift)", Range(0, 1)) = 0
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
            half _FGTexWeight;
            half _FGArrayGain;
            float _FGTiling;
            half _SkyFill;
            half _SpecStrength;
            half _SpecPower;
            half _UseTriplanar;
            half _FGMacro;
            half _FGGrade;           // asset study 2026-10-14: 0 = no colour grade (default), 1 = the palette's full grade
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _FG_TRIPLANAR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            TEXTURE2D_ARRAY(_FGAlbedoArray);
            SAMPLER(sampler_FGAlbedoArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float2 uv0        : TEXCOORD0;   // metres (mesh builders) or the fish skin tile
                float4 uv1        : TEXCOORD1;   // x = palette slot (F2), y = texture layer, z = mapping (0 uv0, 1 world triplanar)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half3  albedo     : TEXCOORD2;   // per vertex (interpolated: smooth meshes share vertices, flat MeshBuilder faces share one value)
                half   fogFactor  : TEXCOORD3;
                float3 uvLayer    : TEXCOORD4;   // xy = uv0 * tiling, z = texture-array layer
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half3 FG_SampleArray(float3 uvl)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_FGAlbedoArray, sampler_FGAlbedoArray, uvl.xy, uvl.z).rgb;
            }

            // Texture factor (1 = untouched). The mapping is chosen per MATERIAL by the _FG_TRIPLANAR keyword (R74 adoption: M_Toon_Tri for rocks and
            // canopies; every other textured material is uv0-mapped and pays one sample plus the macro sample). The mesh's uv1.z states which
            // mapping it was built for; the builders put those meshes on the matching material. The uniform _FGTexWeight branch skips every sample
            // on untextured materials.
            half3 FG_TextureFactor(float3 uvLayer, float3 positionWS, half3 geomNormal)
            {
                half3 factor = half3(1.0h, 1.0h, 1.0h);
                [branch] if (_FGTexWeight > 0.001h)
                {
                #if defined(_FG_TRIPLANAR)
                    float3 p = positionWS * _FGTiling;
                    half3 w = pow(abs(geomNormal), 4.0h);
                    w /= max(w.x + w.y + w.z, 1e-4h);
                    half3 s = (FG_SampleArray(float3(p.zy, uvLayer.z)) * w.x
                             + FG_SampleArray(float3(p.xz, uvLayer.z)) * w.y
                             + FG_SampleArray(float3(p.xy, uvLayer.z)) * w.z) * _FGArrayGain;
                #else
                    half3 s = FG_SampleArray(float3(uvLayer.xy, uvLayer.z)) * _FGArrayGain;
                #endif
                    // Macro breakup: the same layer at 1/11 scale in world xz, so large surfaces (shore, lakebed) never show the tile repeat.
                    // Uniform branch: only the materials that set _FGMacro pay for the second sample.
                    [branch] if (_FGMacro > 0.001h)
                    {
                        half3 macro = FG_SampleArray(float3(positionWS.xz * (_FGTiling * 0.09), uvLayer.z)) * _FGArrayGain;
                        s *= lerp(half3(1.0h, 1.0h, 1.0h), macro, _FGMacro);
                    }
                    factor = lerp(factor, s, _FGTexWeight);
                }
                return factor;
            }

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
                output.uvLayer = float3(input.uv0 * _FGTiling, round(input.uv1.y));
                half3 vertexFactor = lerp(input.color.rgb, 2.0h * input.color.rgb, _JitterEncoded);   // BF-7: grey meshes have r == g == b
                half3 baseRgb = _UsePalette > 0.5h ? FG_PaletteColor(input.uv1.x) * vertexFactor : vertexFactor;
                half3 albedo = baseRgb * _BaseColor.rgb;
                albedo = FG_HueRotate(albedo, FG_ObjectHueNoise() * (_FGGradeParams.y * _FGGrade));
                half grey = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));   // Rec.709 luma (the colours here are linear, not gamma-encoded)
                output.albedo = lerp(albedo, grey.xxx, _Desaturate);
                output.fogFactor = FG_FogFactor(pos.positionCS.z);
                return output;
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);
                half3 texFactor = FG_TextureFactor(input.uvLayer, input.positionWS, n);

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
                half3 albedo = input.albedo * texFactor;
                // Colour grade: a warm light gain on upward faces, a cool gain on the shaded and down-facing sides (header).
                half topMask = smoothstep(_FGGradeParams.x, 1.0h, n.y) * (_FGTopTint.w * _FGGrade);
                albedo *= lerp(half3(1.0h, 1.0h, 1.0h), _FGTopTint.rgb, topMask);
                half bottomMask = _FGBottomTint.w * _FGGrade * lerp(_FGGradeParams.z, 1.0h, saturate(-n.y));
                half3 shade = _ToonShade.rgb * lerp(half3(1.0h, 1.0h, 1.0h), _FGBottomTint.rgb, bottomMask);

                half band = FG_ToonBand(saturate(dot(n, mainLight.direction)), mainLight.shadowAttenuation);
                half3 tint = lerp(shade, mainLight.color, band);
                half3 lit = albedo * tint + albedo * _ToonAmbient.rgb * 0.15h;
                // Sky fill: a soft hemisphere term so shaded sides pick up sky colour from above and ground bounce from below.
                lit += albedo * lerp(_SkyGround.rgb, _SkyTop.rgb, saturate(n.y * 0.5h + 0.5h)) * _SkyFill;
                half rim = pow(1.0h - saturate(dot(n, inputData.viewDirectionWS)), _RimPow) * _RimStrength;
                lit += (albedo * 0.5h + 0.5h) * lerp(_SkyHorizon.rgb, mainLight.color, band) * rim;
                // Soft toon highlight: a smoothstepped Blinn-Phong lobe, lit side only.
                half3 h = normalize(mainLight.direction + inputData.viewDirectionWS);
                half lobe = smoothstep(0.25h, 0.6h, pow(saturate(dot(n, h)), _SpecPower));
                lit += mainLight.color * (lobe * band * _SpecStrength);

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

        // Kept although SSAO is off (spec: none; RenderingSetupBuilder disables it). With SSAO off, the depth FishGame/Water samples (depth fade, foam)
        // comes from URP's CopyDepth after the opaques (m_CopyDepthMode 0), which copies the opaque depth Toon rendered. When the platform cannot
        // CopyDepth (!CanCopyDepth) URP runs a depth prepass instead, and that prepass uses Toon's DepthOnly pass above. This DepthNormals pass is
        // kept for future requests for normals (SSAO or any renderer feature that asks for them), which would draw Toon geometry through it.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
