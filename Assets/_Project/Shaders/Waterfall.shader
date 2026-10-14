// Shaders/Waterfall.shader - FishGame/Waterfall (harbour round 4, 2026-10-18). ASCII only.
// Stylised running water for the West Head brook and its fall (Harbour_Core_Waterfall): opaque toon water whose streaks scroll along the
// flow. uv0 = (across 0..1, metres along the flow; fg_harbour.py build_waterfall). Cheap: no textures, no depth reads, two value-noise octaves.
//   - Body: _DeepColor -> _BodyColor by the streak noise; steep parts (the falling sheet, normal near horizontal) lighten toward _FoamColor.
//   - Streaks: value noise stretched along the flow (_StreakScale) and scrolled at _FlowSpeed m/s by the shared _WaterTime, so every peer and
//     the captures see the same phase.
//   - Foam: white toward the foot of the fall (below _FoamHeight, world y) and in the lip's curl (_LipFoam over the first metres of the sheet).
//   - Edges: alpha-clipped against animated noise, so the sheet's sides are ragged and alive instead of a ruler-straight ribbon.
//   - Light: the main light's luminance with _LightTint of its hue, plus _ToonAmbient; URP fog.
// Opaque (AlphaTest queue), Cull Off (the brook is seen from above, the sheet from the front), no shadows. Single UnityPerMaterial CBUFFER.
Shader "FishGame/Waterfall"
{
    Properties
    {
        _DeepColor("Deep Colour", Color) = (0.06, 0.32, 0.38, 1)
        _BodyColor("Body Colour", Color) = (0.30, 0.66, 0.70, 1)
        _FoamColor("Foam Colour", Color) = (0.95, 0.98, 1.0, 1)
        _StreakScale("Streak Length (1/m)", Float) = 0.55
        _Columns("Streak Columns Across", Float) = 9
        _FlowSpeed("Flow Speed (m/s)", Float) = 2.6
        _FoamHeight("Foot Foam Height (world y)", Float) = 1.6
        _SteepFoam("Steep Lighten", Range(0, 1)) = 0.45
        _EdgeRag("Edge Raggedness", Range(0, 0.5)) = 0.22
        _LightTint("Light Hue", Range(0, 1)) = 0.4
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.45
        _FogAmount("Fog Amount", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WaterfallForward"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FallVert
            #pragma fragment FallFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _BodyColor;
                half4 _FoamColor;
                float _StreakScale;
                float _Columns;
                float _FlowSpeed;
                float _FoamHeight;
                half _SteepFoam;
                half _EdgeRag;
                half _LightTint;
                half _AmbientStrength;
                half _FogAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
            };

            float FallNoise(float2 p)
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

            Varyings FallVert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv0;
                o.fogFactor = FG_FogFactor(pos.positionCS.z, pos.positionWS);
                return o;
            }

            half4 FallFrag(Varyings input) : SV_Target
            {
                // the flow phase wraps every 64 m of travel (float precision at long session time); the streak lattice repeats with it
                float flow = input.uv.y * _StreakScale - fmod(_WaterTime * _FlowSpeed * _StreakScale, 64.0);
                float across = input.uv.x * _Columns;
                float n1 = FallNoise(float2(across, flow));
                float n2 = FallNoise(float2(across * 2.3 + 17.0, flow * 1.7 + 5.0));
                half streak = (half)smoothstep(0.42, 0.78, 0.65 * n1 + 0.35 * n2);

                // ragged, living edges
                half edge = (half)abs(input.uv.x * 2.0 - 1.0);
                clip(1.0h - _EdgeRag * (half)FallNoise(float2(flow * 2.0, input.uv.x * 3.0 + 9.0)) - edge);

                half3 n = normalize(input.normalWS);
                half steep = 1.0h - saturate(abs(n.y) * 1.4h);
                half3 body = lerp(_DeepColor.rgb, _BodyColor.rgb, streak);
                body = lerp(body, _FoamColor.rgb, steep * _SteepFoam * (0.55h + 0.45h * streak));
                // foam at the foot of the fall and in the lip's curl (the first metres where the sheet turns down)
                half foot = (half)smoothstep(_FoamHeight, 0.0, input.positionWS.y) * steep;
                half foamNoise = (half)FallNoise(float2(across * 1.3, flow * 2.4 + 31.0));
                half foam = saturate(foot * (0.7h + 0.6h * foamNoise));
                body = lerp(body, _FoamColor.rgb, foam);

                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half luma = dot(lightColor, half3(0.2126h, 0.7152h, 0.0722h));
                half3 lightTint = lerp(luma.xxx, lightColor, _LightTint);
                half ndl = saturate(dot(n, mainLight.direction)) * 0.35h + 0.65h;   // soft: running water reads lit from every side
                half3 rgb = body * (lightTint * ndl + _ToonAmbient.rgb * _AmbientStrength);
                rgb = lerp(rgb, FG_ApplyFog(rgb, input.fogFactor), _FogAmount);
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
