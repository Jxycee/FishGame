// Shaders/Line.shader - FishGame/Line (spec 9.11/10.5, C 7.21). Owned by T21. ASCII only.
// Unlit opaque LineRenderer shader. uv.x runs 0..1 along the line (LineView sets textureMode = Stretch). _Tension drives the colour:
//   warm = smoothstep(0.6, 0.8, t), warn = step(0.8, t), rgb = lerp(lerp(_LineColor, _WarmColor, warm), _WarnColor, warn).
// Warn adds moving dark gaps (a pattern, not hue only, spec 10.2): dash = step(0.5, frac(uv.x * _DashCount - _Time.y * _DashSpeed)).
// Grace (t >= 1) adds a shimmer. The line width pulse lives in LineView, not here. Vertex colour (the LineRenderer colour) is multiplied in.
Shader "FishGame/Line"
{
    Properties
    {
        _LineColor("Line Colour", Color) = (0.92, 0.92, 0.88, 1)
        _WarmColor("Warm Colour", Color) = (1.0, 0.72, 0.35, 1)
        _WarnColor("Warn Colour", Color) = (1.0, 0.25, 0.18, 1)
        _Tension("Tension", Range(0, 1.2)) = 0
        _DashCount("Dash Count", Float) = 20
        _DashSpeed("Dash Speed", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "LineForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LineVert
            #pragma fragment LineFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LineColor;
                half4 _WarmColor;
                half4 _WarnColor;
                half _Tension;
                half _DashCount;
                half _DashSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  u          : TEXCOORD0;
                half4  color      : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
            };

            Varyings LineVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.u = input.uv.x;
                o.color = input.color;
                o.fogFactor = FG_FogFactor(o.positionCS.z);
                return o;
            }

            half4 LineFrag(Varyings input) : SV_Target
            {
                half t = _Tension;
                half warm = smoothstep(0.6h, 0.8h, t);
                half warn = step(0.8h, t);
                half3 rgb = lerp(lerp(_LineColor.rgb, _WarmColor.rgb, warm), _WarnColor.rgb, warn);
                half dash = step(0.5h, frac(input.u * _DashCount - _Time.y * _DashSpeed));
                rgb *= lerp(1.0h, lerp(0.3h, 1.0h, dash), warn);
                rgb += step(1.0h, t) * 0.6h * (0.5h + 0.5h * sin(_Time.y * 40.0 + input.u * 60.0));
                rgb *= input.color.rgb;
                return half4(FG_ApplyFog(rgb, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
