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
                half grey = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));   // Rec.709 luma (the colours here are linear, not gamma-encoded)
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
