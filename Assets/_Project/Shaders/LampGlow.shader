// Shaders/LampGlow.shader - FishGame/LampGlow (harbour round 0, 2026-10-14). ASCII only.
// Additive night glow drawn as a second renderer over a toon mesh (the string-light bulbs) or on its own (small lamp and beacon glow spheres).
// Only the vertices whose palette slot (uv1.x, the Blender "Layer" uv) equals _Slot glow; every other vertex is pushed outside the clip volume, so
// the wires and posts of the same mesh draw nothing. _Slot < 0 makes every vertex glow (the glow spheres, whose meshes carry no uv1).
// Brightness = _GlowColor * _GlowBoost * _FGLampGlow (DayNightMath.LampGlow, pushed by DayNightController; 0 by day, so nothing draws by day).
// The vertices are pushed out along the normal by _Inflate metres so the glow wraps the lit surface. Fog fades the glow to black (additive).
Shader "FishGame/LampGlow"
{
    Properties
    {
        _GlowColor("Glow Colour", Color) = (1.0, 0.74, 0.42, 1)
        _GlowBoost("Glow Boost", Range(0, 8)) = 2.5
        _Slot("Palette Slot That Glows (-1 = all)", Float) = 10
        _Inflate("Inflate (m)", Float) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LampGlowForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GlowVert
            #pragma fragment GlowFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Shaders/FishGameCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _GlowBoost;
                float _Slot;
                float _Inflate;
            CBUFFER_END

            float _FGLampGlow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   fogFactor  : TEXCOORD0;
            };

            Varyings GlowVert(Attributes input)
            {
                Varyings o;
                float on = (_Slot < -0.5 || abs(input.uv1.x - _Slot) < 0.5) && _FGLampGlow > 0.001 ? 1.0 : 0.0;
                float3 p = input.positionOS.xyz + input.normalOS * (_Inflate * on);
                float4 clip = TransformObjectToHClip(p);
                o.positionCS = on > 0.5 ? clip : float4(0.0, 0.0, -2.0, 1.0);   // outside the clip volume: the triangle is culled
                o.fogFactor = FG_FogFactor(clip.z);
                return o;
            }

            half4 GlowFrag(Varyings input) : SV_Target
            {
                half3 rgb = _GlowColor.rgb * (_GlowBoost * saturate(_FGLampGlow));
                return half4(MixFogColor(rgb, half3(0.0h, 0.0h, 0.0h), input.fogFactor), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
