// Shaders/GradientSky.shader - FishGame/GradientSky (spec 10.3, C 7.21). Owned by T21. ASCII only.
// Skybox material shader. dir = normalize(positionOS): above the horizon lerp(_SkyHorizon, _SkyTop, 1 - pow(1 - sat(y), _HorizonSharpness)),
// below it the same towards _SkyGround. The sun disc uses the URP main-light direction and colour. No fog on the sky.
// _SkyTop/_SkyHorizon/_SkyGround are globals (FishGameCommon.hlsl) pushed by PaletteGlobals (T22).
Shader "FishGame/GradientSky"
{
    Properties
    {
        _SunSize("Sun Size", Float) = 0.03
        _SunSoftness("Sun Softness", Float) = 0.006
        _SunIntensity("Sun Intensity", Float) = 1.5
        _HorizonSharpness("Horizon Sharpness", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GradientSky"
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _SunSize;
                half _SunSoftness;
                half _SunIntensity;
                half _HorizonSharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS      : TEXCOORD0;
            };

            Varyings SkyVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dirOS = input.positionOS.xyz;
                return o;
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dirOS);
                half3 rgb;
                if (dir.y >= 0.0)
                    rgb = lerp(_SkyHorizon.rgb, _SkyTop.rgb, 1.0 - pow(1.0 - saturate(dir.y), _HorizonSharpness));
                else
                    rgb = lerp(_SkyHorizon.rgb, _SkyGround.rgb, 1.0 - pow(1.0 - saturate(-dir.y), _HorizonSharpness));

                float d = dot(dir, _MainLightPosition.xyz);
                rgb += smoothstep(1.0 - _SunSize - _SunSoftness, 1.0 - _SunSize, d) * _MainLightColor.rgb * _SunIntensity;
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
