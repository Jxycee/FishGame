// Shaders/GradientSky.shader - FishGame/GradientSky (spec 10.3, C 7.21). Owned by T21. ASCII only.
// Skybox material shader. dir = normalize(positionOS): above the horizon lerp(_SkyHorizon, _SkyTop, 1 - pow(1 - sat(y), _HorizonSharpness)),
// below it the same towards _SkyGround. The sun disc uses the URP main-light direction and colour. No fog on the sky.
// _SkyTop/_SkyHorizon/_SkyGround are globals (FishGameCommon.hlsl) pushed by PaletteGlobals (T22).
// Harbour round 4 (2026-10-18, critics: "a pale hazy gradient with no clouds, it lacks punch next to RV There Yet's blue and puffy clouds"):
// stylised toon clouds on a plane above the camera (dir.xz / dir.y), two octaves of value noise thresholded into soft-edged puffs, faded
// out toward the horizon, drifting slowly with _WaterTime (every peer and the captures agree). Lit side = the main light's colour (white by
// day, gold at dusk), shade side = a cool tint of the sky, from a second sample offset toward the sun. _ZenithDeepen pushes the top of the
// sky a little bluer and deeper. _CloudAmount 0 = round 3's sky exactly.
Shader "FishGame/GradientSky"
{
    Properties
    {
        _SunSize("Sun Size", Float) = 0.03
        _SunSoftness("Sun Softness", Float) = 0.006
        _SunIntensity("Sun Intensity", Float) = 1.5
        _HorizonSharpness("Horizon Sharpness", Float) = 2.5
        _CloudAmount("Cloud Amount", Range(0, 1)) = 0.5
        _CloudScale("Cloud Scale", Float) = 1.6
        _CloudSpeed("Cloud Drift", Float) = 0.004
        _ZenithDeepen("Zenith Deepen", Range(0, 1)) = 0.3
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
                half _CloudAmount;
                float _CloudScale;
                float _CloudSpeed;
                half _ZenithDeepen;
            CBUFFER_END

            float SkyNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = FG_Hash21(i);
                float b = FG_Hash21(i + float2(1.0, 0.0));
                float c = FG_Hash21(i + float2(0.0, 1.0));
                float d = FG_Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float SkyCloud(float2 p)
            {
                // big rounded masses (octave 1) with cauliflower edges (octave 2)
                return 0.68 * SkyNoise(p) + 0.32 * SkyNoise(p * 2.7 + 13.0);
            }

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

                // deeper zenith (round 4)
                half up = (half)saturate(dir.y);
                rgb = lerp(rgb, rgb * half3(0.78h, 0.88h, 1.04h), up * up * _ZenithDeepen);

                float d = dot(dir, _MainLightPosition.xyz);
                rgb += smoothstep(1.0 - _SunSize - _SunSoftness, 1.0 - _SunSize, d) * _MainLightColor.rgb * _SunIntensity;

                // toon clouds (round 4)
                [branch] if (_CloudAmount > 0.001h && dir.y > 0.015)
                {
                    float drift = fmod(_WaterTime * _CloudSpeed, 256.0);   // only the drift wraps (a whole-lattice jump every ~18 h of play)
                    float2 uv = dir.xz / (dir.y + 0.12) * _CloudScale + float2(drift, drift * 0.37);
                    float c0 = SkyCloud(uv);
                    float cut = lerp(0.82, 0.5, (float)_CloudAmount);
                    half mask = (half)smoothstep(cut, cut + 0.07, c0) * (half)smoothstep(0.015, 0.2, dir.y);
                    float2 toSun = normalize(_MainLightPosition.xz + 1e-4) * 0.09;
                    half lit = (half)saturate((c0 - SkyCloud(uv + toSun)) * 9.0 + 0.55);
                    half3 sunTint = _MainLightColor.rgb / max(max(_MainLightColor.r, _MainLightColor.g), max(_MainLightColor.b, 0.001h));
                    half3 litCol = lerp(half3(1.0h, 1.0h, 1.0h), sunTint, 0.45h) * 1.02h;
                    half3 shadeCol = lerp(_SkyHorizon.rgb, _SkyTop.rgb, 0.35h) * half3(0.86h, 0.9h, 1.0h) + 0.12h;
                    half3 cloud = lerp(shadeCol, litCol, smoothstep(0.35h, 0.65h, lit));
                    cloud = lerp(cloud, _SkyHorizon.rgb, (half)(1.0 - smoothstep(0.02, 0.35, dir.y)) * 0.55h);   // hazier low down
                    rgb = lerp(rgb, cloud, mask);
                }
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
