// Shaders/Water.shader - FishGame/Water (spec 10.4, C 7.21). Owned by T21. ASCII only.
// Stylised toon lake: vivid, saturated and lit relative to the palette phase (Day now; M3 dawn/dusk/night push the same globals).
// Vertex: MeshBuilder.Grid (shared vertices) displaced by _Waves/_WaterTime (mirrors WaveMath, F1). The wave term comes ONLY from
// FishGameCommon.hlsl (FG_WaveHeight / FG_WaveTerm, with the non-finite guard): never re-implement it here.
// Fragment techniques:
//   1. Vertical water depth: the scene depth is reconstructed to a world position and depth = surface y - bed y (not the eye-ray
//      difference, which saturates to "deep" at low view angles). Body = shallow -> deep over 1 - exp(-depth / _DepthRange), lerped in
//      sqrt space so the midpoint stays saturated. Alpha follows the same curve (deep is near opaque, the bed only shows in the shallows).
//   2. Flat facet normal from ddx/ddy, plus a shading-only exaggerated normal (xz * _FacetBoost, faded to 1 by _FacetFade metres:
//      real wave slopes are only a few degrees). The facet band is RELATIVE to a flat surface (dot(nB, L) - dot(up, L)), so about half the
//      facets sit on each side at any sun elevation, and it is a +/- _FacetContrast brightness step, never a swap to the shade colour.
//   3. Light: the body is lit by the main light's luminance with only _LightTint of its hue (blue times orange is mud) plus _ToonAmbient.
//      Warm phase colour arrives through the sky reflection, glints and foam instead.
//   4. Crests (wave height / _CrestHeight, about the sum of the wave amplitudes) shift toward the shallow colour.
//   5. Toon glints: quantised Blinn-Phong on the exaggerated normal (light direction optionally flattened toward the horizon by
//      _GlintFlatten), near-white tinted by the light hue and scaled by its intensity; faded in past _GlintNear metres.
//   6. Sky reflection: fresnel toward lerp(_SkyHorizon, _SkyTop, _ReflectZenith), capped at _ReflectStrength (colour, not grey).
//   7. Saturation lift (_Saturation) before fog; fog at _FogAmount of URP fog (water would otherwise wash to the fog colour).
//   8. Shore foam: vertical depth band with a smooth edge wobbled by hash value noise (stepped, wrapped time cross-faded), plus _Ripples
//      (foam and brightness only); lit by light + ambient.
// Transparent, ZWrite Off, one UniversalForward pass, URP fog. Single UnityPerMaterial CBUFFER (SRP Batcher).
Shader "FishGame/Water"
{
    Properties
    {
        _ShallowColor("Shallow Colour (a = opacity)", Color) = (0.30, 0.86, 0.80, 0.68)
        _DeepColor("Deep Colour (a = opacity)", Color) = (0.03, 0.36, 0.62, 0.97)
        _FoamColor("Foam Colour", Color) = (0.96, 0.99, 1.0, 1.0)
        _DepthRange("Depth Range (m)", Float) = 1.6
        _FoamWidth("Foam Width (m)", Float) = 0.35
        _LightTint("Light Hue On Body", Range(0, 1)) = 0.35
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.35
        _FacetBoost("Facet Normal Boost", Range(1, 16)) = 7
        _FacetContrast("Facet Contrast", Range(0, 0.5)) = 0.14
        _FacetSoft("Facet Band Softness", Range(0.001, 0.2)) = 0.02
        _FacetFade("Facet Fade Distance (m)", Float) = 55
        _CrestAmount("Crest Lighten", Range(0, 1)) = 0.35
        _CrestHeight("Crest Height (m, about the sum of wave amplitudes)", Float) = 0.11
        _GlintPower("Glint Power", Range(4, 512)) = 96
        _GlintThreshold("Glint Threshold", Range(0.01, 0.99)) = 0.5
        _GlintStrength("Glint Strength", Range(0, 2)) = 1.0
        _GlintFlatten("Glint Sun Height Scale", Range(0.1, 1)) = 0.7
        _GlintNear("Glint Fade-In Distance (m)", Float) = 6
        _ReflectStrength("Sky Reflection Strength", Range(0, 1)) = 0.4
        _ReflectZenith("Sky Reflection Zenith Mix", Range(0, 1)) = 0.6
        _FresnelPower("Fresnel Power", Range(1, 8)) = 4
        _Saturation("Saturation", Range(0, 2)) = 1.15
        _FogAmount("Fog Amount", Range(0, 1)) = 0.35
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
                half _LightTint;
                half _AmbientStrength;
                half _FacetBoost;
                half _FacetContrast;
                half _FacetSoft;
                float _FacetFade;
                half _CrestAmount;
                float _CrestHeight;
                half _GlintPower;
                half _GlintThreshold;
                half _GlintStrength;
                half _GlintFlatten;
                float _GlintNear;
                half _ReflectStrength;
                half _ReflectZenith;
                half _FresnelPower;
                half _Saturation;
                half _FogAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  eyeDepth   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                float  waveHeight : TEXCOORD3;
            };

            // Smooth value noise over the shared hash (foam edge wobble).
            float WaterValueNoise(float2 p, float seed)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = FG_Hash21(i + seed);
                float b = FG_Hash21(i + float2(1.0, 0.0) + seed);
                float c = FG_Hash21(i + float2(0.0, 1.0) + seed);
                float d = FG_Hash21(i + float2(1.0, 1.0) + seed);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings WaterVert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float h = FG_WaveHeight(positionWS.xz, _WaterTime);
                positionWS.y += h;
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.eyeDepth = -TransformWorldToView(positionWS).z;
                o.fogFactor = FG_FogFactor(o.positionCS.z);
                o.waveHeight = h;
                return o;
            }

            half3 WaterSqr(half3 c) { return c * c; }

            half4 WaterFrag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                // Flat facet normal (spec 10.4). The cross-product sign depends on the API derivative orientation,
                // so force it upward instead of trusting one sign (V13).
                float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
                n = n.y < 0.0 ? -n : n;
                // Shading-only exaggerated normal, faded back to the true facet far away (1.25 m cells go sub-pixel and would sparkle).
                half boost = lerp(_FacetBoost, 1.0h, saturate(input.eyeDepth / max(_FacetFade, 0.001)));
                half3 nB = normalize(half3(n.x * boost, n.y, n.z * boost));

                // 1. Vertical depth to the bed (standard URP depth -> world reconstruction).
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
            #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif
                float3 sceneWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float depthV = max(0.0, positionWS.y - sceneWS.y);
                half depthT = (half)(1.0 - exp(-depthV / max(_DepthRange, 0.001)));

                half3 body = WaterSqr(lerp(sqrt(_ShallowColor.rgb), sqrt(_DeepColor.rgb), depthT));
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depthT);

                // 4. Crests toward the shallow colour.
                half crest = (half)saturate(input.waveHeight / max(_CrestHeight, 0.001) * 0.5 + 0.5);
                body = lerp(body, _ShallowColor.rgb, crest * crest * _CrestAmount);

                // 3. Light: luminance with partial hue, plus ambient.
                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half lightLuma = dot(lightColor, half3(0.2126h, 0.7152h, 0.0722h));
                half3 lightTint = lerp(lightLuma.xxx, lightColor, _LightTint);

                // 2. Facet band relative to a flat surface.
                half3 L = mainLight.direction;
                half facetDelta = dot(nB, L) - L.y;
                half band = smoothstep(-_FacetSoft, _FacetSoft, facetDelta);
                half facet = 1.0h + _FacetContrast * (band * 2.0h - 1.0h);

                half3 rgb = body * (lightTint * facet + _ToonAmbient.rgb * _AmbientStrength);

                // 6. Sky reflection (colour, not grey).
                half3 V = (half3)normalize(_WorldSpaceCameraPos - positionWS);
                half3 nR = normalize(lerp((half3)n, nB, 0.5h));
                half fresnel = pow(1.0h - saturate(dot(nR, V)), _FresnelPower);
                half3 skyColor = lerp(_SkyHorizon.rgb, _SkyTop.rgb, _ReflectZenith);
                rgb = lerp(rgb, skyColor, fresnel * _ReflectStrength);
                alpha = saturate(alpha + fresnel * 0.5h);

                // 7. Saturation lift.
                half luma = dot(rgb, half3(0.2126h, 0.7152h, 0.0722h));
                rgb = max(0.0h, lerp(luma.xxx, rgb, _Saturation));

                // 5. Toon glints (after the saturation lift so they stay near white). Faded in past _GlintNear: a whole near facet
                // flashing reads as a hole, not a glint. Colour: the light's hue at 60 % over white, at the light's peak channel.
                half3 Lg = normalize(half3(L.x, L.y * _GlintFlatten, L.z));
                half3 H = normalize(Lg + V);
                half spec = pow(saturate(dot(nB, H)), _GlintPower);
                half glint = smoothstep(_GlintThreshold - 0.02h, _GlintThreshold + 0.02h, spec);
                glint *= (half)smoothstep(_GlintNear, _GlintNear * 2.5, input.eyeDepth);
                half lightPeak = max(max(lightColor.r, lightColor.g), max(lightColor.b, 0.001h));
                half3 glintColor = lerp(1.0h.xxx, lightColor / lightPeak, 0.6h) * lightPeak;
                rgb += glint * _GlintStrength * glintColor;
                alpha = saturate(alpha + glint);

                // 8. Foam and ripples.
                float ripple = FG_RippleSum(positionWS.xz, _WaterTime);
                // Wobble: value noise cross-faded between two stepped time layers (the step is wrapped: float precision at long session time).
                float tStep = fmod(floor(_WaterTime * 1.5), 1024.0);
                float tNext = fmod(tStep + 1.0, 1024.0);
                float tFade = smoothstep(0.0, 1.0, frac(_WaterTime * 1.5));
                float2 foamUV = positionWS.xz * 1.4;
                half wobble = (half)(0.35 + 0.4 * lerp(WaterValueNoise(foamUV, tStep * 7.0), WaterValueNoise(foamUV, tNext * 7.0), tFade));
                half foamEdge = 1.0h - (half)saturate(depthV / max(_FoamWidth, 0.001));
                half foam = saturate(smoothstep(wobble - 0.04h, wobble + 0.04h, foamEdge) + (half)ripple * 0.6h);
                rgb += (half)ripple * 0.25h * lightColor;              // ripples add brightness only (spec 10.4)
                half3 foamLit = _FoamColor.rgb * (lightColor * 0.9h + _ToonAmbient.rgb * 0.3h);
                rgb = lerp(rgb, foamLit, foam);
                alpha = saturate(max(alpha, foam * _FoamColor.a));

                rgb = lerp(rgb, FG_ApplyFog(rgb, input.fogFactor), _FogAmount);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
