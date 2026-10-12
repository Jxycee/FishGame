// Shaders/Water.shader - FishGame/Water (spec 10.4, C 7.21). Owned by T21. ASCII only.
// Vertex-displaced MeshBuilder.Grid (shared vertices) via _Waves/_WaterTime (mirrors WaveMath, F1); flat facet normal;
// depth fade shallow -> deep over _DepthRange; foam band where scene depth is within _FoamWidth, broken by hash noise,
// plus _Ripples (foam and brightness only). Water-specific toon ramp (_RampMid 0.78, _RampSoft 0.04: the sun dot of a facet is about 0.77, so wave facets
// band against the sun), ambient 0.4, sky-horizon fresnel (0.2). Transparent, ZWrite Off, one UniversalForward pass, URP fog.
// The wave term comes ONLY from FishGameCommon.hlsl (FG_WaveHeight / FG_WaveTerm, with the non-finite guard): never re-implement it here.
Shader "FishGame/Water"
{
    Properties
    {
        _ShallowColor("Shallow Colour", Color) = (0.36, 0.72, 0.70, 0.75)
        _DeepColor("Deep Colour", Color) = (0.14, 0.42, 0.58, 0.85)
        _FoamColor("Foam Colour", Color) = (0.92, 0.97, 1.0, 1.0)
        _DepthRange("Depth Range (m)", Float) = 3.0
        _FoamWidth("Foam Width (m)", Float) = 0.35
        _RampMid("Ramp Mid", Range(0, 1)) = 0.78
        _RampSoft("Ramp Softness", Range(0.001, 0.5)) = 0.04
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
                // Flat facet normal (spec 10.4). The cross-product sign depends on the API derivative orientation,
                // so force it upward instead of trusting one sign (V13).
                float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
                n = n.y < 0.0 ? -n : n;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthBelow = max(0.0, sceneEye - input.eyeDepth);

                half4 body = lerp(_ShallowColor, _DeepColor, smoothstep(0.0, _DepthRange, depthBelow));
                float ripple = FG_RippleSum(positionWS.xz, _WaterTime);
                half foamEdge = 1.0h - saturate(depthBelow / max(_FoamWidth, 0.001));
                half noise = FG_Hash21(floor(positionWS.xz * 3.0) + fmod(floor(_WaterTime * 1.5), 1024.0));   // wrapped: float precision at long session time
                half foam = saturate(step(noise * 0.7h + 0.15h, foamEdge) + ripple * 0.6h);

                Light mainLight = GetMainLight();
                half band = smoothstep(_RampMid - _RampSoft, _RampMid + _RampSoft, saturate(dot(n, mainLight.direction)));
                half3 tint = lerp(_ToonShade.rgb, mainLight.color, band);
                half3 rgb = body.rgb * tint + body.rgb * _ToonAmbient.rgb * 0.4h;
                // Sky-horizon fresnel: grazing views pick up the horizon colour (global, linear), so the far lake reads as lit water, not a dark slab.
                half fresnel = pow(1.0h - saturate(dot(n, normalize(_WorldSpaceCameraPos - positionWS))), 3.0h);
                rgb = lerp(rgb, _SkyHorizon.rgb, fresnel * 0.2h);
                rgb += ripple * 0.25h * mainLight.color;              // ripples add brightness only (spec 10.4)
                rgb = lerp(rgb, _FoamColor.rgb * mainLight.color, foam);   // foam is lit by the sun, never banded to shade (facet bands would tint it blue)
                half alpha = saturate(max(body.a, foam * _FoamColor.a));
                return half4(FG_ApplyFog(rgb, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
