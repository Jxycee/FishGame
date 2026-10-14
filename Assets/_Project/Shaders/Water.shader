// Shaders/Water.shader - FishGame/Water (spec 10.4, C 7.21). Owned by T21. ASCII only.
// Stylised toon lake: vivid, saturated and lit relative to the palette phase (Day now; M3 dawn/dusk/night push the same globals).
// Vertex: MeshBuilder.Grid (shared vertices) displaced by _Waves/_WaterTime (mirrors WaveMath, F1). The wave term comes ONLY from
// FishGameCommon.hlsl (FG_WaveHeight / FG_WaveTerm, with the non-finite guard): never re-implement it here.
// Fragment techniques:
//   1. Vertical water depth: the scene depth is reconstructed to a world position and depth = surface y - bed y (not the eye-ray
//      difference, which saturates to "deep" at low view angles). Body = shallow -> deep over 1 - exp(-depth / _DepthRange), lerped in
//      sqrt space so the midpoint stays saturated. Alpha follows the same curve (deep is near opaque, the bed only shows in the shallows).
//   2. Smooth per-pixel normal (no triangle facets): the wave slope is a central finite difference of FG_WaveHeight at the pixel
//      (float, +/- 0.2 m so long-session time rounding does not grain it), exaggerated by _NormalBoost for shading only (real slopes are a few degrees), plus two off-axis octaves of
//      drifting value-noise ripples (analytic gradient, lattice period 256 so the wrapped drift is seamless) faded out by _DetailFade metres.
//      Slope shading is RELATIVE to a flat surface (dot(nB, L) - dot(up, L)) through a soft smoothstep (_ShadeSoft), a +/- _ShadeContrast
//      brightness swing, never a swap to the shade colour.
//   3. Light: the body is lit by the main light's luminance with only _LightTint of its hue (blue times orange is mud) plus _ToonAmbient.
//      Warm phase colour arrives through the sky reflection, glints and foam instead.
//   4. Crests (per-pixel wave height / _CrestHeight, about the sum of the wave amplitudes) shift toward the shallow colour.
//   5. Toon glints: thresholded Blinn-Phong on a glint normal (half the wave boost plus _GlintRipple times the ripples, so glints are
//      sparkles rather than crest-long lenses; light direction optionally flattened toward the horizon by
//      _GlintFlatten), edge anti-aliased with fwidth; near-white tinted by the light hue and scaled by its intensity; faded in past _GlintNear.
//   6. Sky reflection: fresnel toward lerp(_SkyHorizon, _SkyTop, _ReflectZenith), capped at _ReflectStrength (colour, not grey).
//   7. Saturation lift (_Saturation) before fog; fog at _FogAmount of URP fog (water would otherwise wash to the fog colour).
//   8. Shore foam: vertical depth band with a smooth edge wobbled by hash value noise (stepped, wrapped time cross-faded), plus _Ripples
//      (foam and brightness only); lit by light + ambient.
//   10. Harbour round 2 (2026-10-16, calmer toon water): the slope shade and the crest tint fade to _FarCalm of their strength by
//      _CalmDistance metres (seen from above, the far sea no longer blotches), and a soft contact band (_FoamSoft over _FoamSoftWidth x
//      _FoamWidth of vertical depth) eases the hard toon foam line into the water where rocks, piles and hulls meet it.
//   12. Harbour round 2b: steep beds (hulls, piles, walls) read deeper in the colour ramp (_SteepDeepen); the foam keeps the true depth.
//   11. Harbour round 2b: a low sun widens and brightens the glints (the dusk sun path); a high sun keeps them small and sparse.
//   9. Lakebed cover: the bed is flat-shaded low-poly, so over a shallow bed the opacity is raised toward 1 (_BedCover, keeping the first
//      few cm at the waterline clear) and soft caustic veins (|ripple octave 1 - octave 2| near zero, _CausticStrength) break up what
//      still shows through. The caustics fade with depth and distance.
// Adds over the facet version: four FG_WaveHeight calls (12 wave sin + 6 direction sin/cos), 8 hashes and one fwidth per pixel; no new textures.
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
        _NormalBoost("Wave Normal Boost", Range(1, 16)) = 4.5
        _ShadeContrast("Slope Shade Contrast", Range(0, 0.5)) = 0.1
        _ShadeSoft("Slope Shade Softness", Range(0.01, 1)) = 0.2
        _DetailScale("Ripple Size (m)", Float) = 1.6
        _DetailStrength("Ripple Slope", Range(0, 1)) = 0.09
        _DetailSpeed("Ripple Drift (m/s)", Float) = 0.3
        _DetailFade("Ripple Fade Distance (m)", Float) = 60
        _BedCover("Shallow Bed Cover", Range(0, 1)) = 0.35
        _CausticStrength("Shallow Caustics", Range(0, 1)) = 0.12
        _CrestAmount("Crest Lighten", Range(0, 1)) = 0.25
        _CrestHeight("Crest Height (m, about the sum of wave amplitudes)", Float) = 0.11
        _GlintPower("Glint Power", Range(4, 512)) = 220
        _GlintThreshold("Glint Threshold", Range(0.01, 0.99)) = 0.62
        _GlintStrength("Glint Strength", Range(0, 2)) = 0.85
        _GlintFlatten("Glint Sun Height Scale", Range(0.1, 1)) = 0.7
        _GlintRipple("Glint Ripple Weight", Range(0, 6)) = 2.2
        _GlintNear("Glint Fade-In Distance (m)", Float) = 6
        _ReflectStrength("Sky Reflection Strength", Range(0, 1)) = 0.36
        _ReflectZenith("Sky Reflection Zenith Mix", Range(0, 1)) = 0.6
        _FresnelPower("Fresnel Power", Range(1, 8)) = 4
        _Saturation("Saturation", Range(0, 2)) = 0.95
        _FogAmount("Fog Amount", Range(0, 1)) = 0.35
        _FoamSoft("Soft Contact Band Strength", Range(0, 1)) = 0.32
        _FoamSoftWidth("Soft Contact Band Width (x Foam Width)", Range(1, 8)) = 2.4
        _CalmDistance("Far Calm Distance (m)", Float) = 70
        _FarCalm("Far Shade And Crest Strength", Range(0, 1)) = 0.35
        _SteepDeepen("Steep Bed Extra Depth (m)", Range(0, 8)) = 0
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
                half _NormalBoost;
                half _ShadeContrast;
                half _ShadeSoft;
                float _DetailScale;
                half _DetailStrength;
                float _DetailSpeed;
                float _DetailFade;
                half _BedCover;
                half _CausticStrength;
                half _CrestAmount;
                float _CrestHeight;
                half _GlintPower;
                half _GlintThreshold;
                half _GlintStrength;
                half _GlintFlatten;
                half _GlintRipple;
                float _GlintNear;
                half _ReflectStrength;
                half _ReflectZenith;
                half _FresnelPower;
                half _Saturation;
                half _FogAmount;
                half _FoamSoft;
                half _FoamSoftWidth;
                float _CalmDistance;
                half _FarCalm;
                half _SteepDeepen;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  eyeDepth   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                float  waveHeight : TEXCOORD3;   // vertex stage kept as is; the fragment uses its own per-pixel height (no longer read by the fragment; kept so the vertex stage is unchanged)
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

            // Lattice wrap to [0, 256): the ripple noise repeats every 256 cells, so a drift wrapped by 256 never jumps.
            float2 WaterWrap256(float2 i) { return i - 256.0 * floor(i / 256.0); }

            // Smooth value noise over the shared hash with its analytic gradient: (value, d/dp.x, d/dp.y). C1, so normals built from it are continuous.
            float3 WaterNoiseGrad(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                float2 u = f * f * (3.0 - 2.0 * f);
                float2 du = 6.0 * f * (1.0 - f);
                float2 i0 = WaterWrap256(i);
                float2 i1 = WaterWrap256(i + 1.0);
                float a = FG_Hash21(i0);
                float b = FG_Hash21(float2(i1.x, i0.y));
                float c = FG_Hash21(float2(i0.x, i1.y));
                float d = FG_Hash21(i1);
                float k = a - b - c + d;
                return float3(a + (b - a) * u.x + (c - a) * u.y + k * u.x * u.y,
                              du * float2((b - a) + k * u.y, (c - a) + k * u.x));
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
                // 2. Smooth per-pixel normal. The grid only displaces y, so the interpolated xz is exact and the wave function can be
                // evaluated per pixel: a central difference in float. The 0.2 m half-step keeps the sine-argument rounding at long session
                // time (w * t in the thousands) out of the boosted slope; its bias is about 1 % on the shortest (6.5 m) wave.
                float2 xz = positionWS.xz;
                const float fdStep = 0.2;
                float hxp = FG_WaveHeight(xz + float2(fdStep, 0.0), _WaterTime);
                float hxm = FG_WaveHeight(xz - float2(fdStep, 0.0), _WaterTime);
                float hzp = FG_WaveHeight(xz + float2(0.0, fdStep), _WaterTime);
                float hzm = FG_WaveHeight(xz - float2(0.0, fdStep), _WaterTime);
                float2 waveSlope = float2(hxp - hxm, hzp - hzm) / (2.0 * fdStep);
                float h0 = 0.25 * (hxp + hxm + hzp + hzm);   // per-pixel height for the crest tint

                // Ripple detail: two drifting octaves rotated off the world axes (37 and 74 degrees) so the lattice never lines up.
                float invScale = 1.0 / max(_DetailScale, 0.01);
                float drift = fmod(_WaterTime * _DetailSpeed * invScale, 256.0);
                float2 p1 = float2(0.8 * xz.x - 0.6 * xz.y, 0.6 * xz.x + 0.8 * xz.y) * invScale + float2(drift, 0.0);
                float2 p2 = float2(0.28 * xz.x - 0.96 * xz.y, 0.96 * xz.x + 0.28 * xz.y) * (invScale * 2.1) - float2(0.0, drift);   // whole-lattice drift only, so the wrap at 256 is seamless
                float3 n1 = WaterNoiseGrad(WaterWrap256(p1));
                float3 n2 = WaterNoiseGrad(WaterWrap256(p2));
                // Gradients rotated back to world axes (transpose of each rotation); lattice units, so the strength does not depend on _DetailScale; octave 2 at 60 % strength.
                float2 g1 = float2(0.8 * n1.y + 0.6 * n1.z, -0.6 * n1.y + 0.8 * n1.z);
                float2 g2 = float2(0.28 * n2.y + 0.96 * n2.z, -0.96 * n2.y + 0.28 * n2.z);
                half detailFade = (half)(1.0 - saturate(input.eyeDepth / max(_DetailFade, 0.001)));
                float2 rippleSlope = (g1 + 0.6 * g2) * (_DetailStrength * detailFade);
                float2 slope = waveSlope * _NormalBoost + rippleSlope;
                half3 nB = (half3)normalize(float3(-slope.x, 1.0, -slope.y));
                // Glint normal: half the wave boost and _GlintRipple times the ripples, so glints break into sparkles instead of crest-long lenses.
                float2 slopeG = waveSlope * (0.5 * _NormalBoost) + rippleSlope * _GlintRipple;
                float3 nG = normalize(float3(-slopeG.x, 1.0, -slopeG.y));
                half3 nR = (half3)normalize(float3(-0.5 * slope.x, 1.0, -0.5 * slope.y));   // milder normal for the fresnel

                // 1. Vertical depth to the bed (standard URP depth -> world reconstruction).
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
            #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif
                float3 sceneWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float depthV = max(0.0, positionWS.y - sceneWS.y);
                // 12. Steep beds (round 2b): under a hull, a pile or a wall the vertical depth stays small all the way down, so the body read as
                // a glowing turquoise sheet. Where the bed is steep (its screen-space normal from the reconstructed position) the colour ramp
                // reads up to _SteepDeepen metres deeper; the foam still uses the true depth (the contact ring stays).
                float3 bedDx = ddx(sceneWS), bedDy = ddy(sceneWS);
                float3 bedN = cross(bedDy, bedDx);
                half steep = (half)(1.0 - smoothstep(0.2, 0.45, abs(bedN.y) * rsqrt(max(dot(bedN, bedN), 1e-12))));
                // not across a depth discontinuity (an underwater rock's silhouette): there the 2x2-pixel derivatives are noise, not a slope
                float bedJump = max(length(bedDx), length(bedDy)) / max(input.eyeDepth, 0.1);
                steep *= (half)(1.0 - smoothstep(0.012, 0.03, bedJump));
                half depthT = (half)(1.0 - exp(-(depthV + steep * _SteepDeepen) / max(_DepthRange, 0.001)));

                half3 body = WaterSqr(lerp(sqrt(_ShallowColor.rgb), sqrt(_DeepColor.rgb), depthT));
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depthT);
                // 9. Cover the flat-shaded bed: opacity toward 1 once the bed is more than a few cm down (the waterline stays clear).
                alpha = lerp(alpha, 1.0h, _BedCover * (half)smoothstep(0.08, 0.9, depthV));

                // 4. Crests toward the shallow colour (per-pixel height, so no interpolation kinks).
                half crest = (half)saturate(h0 / max(_CrestHeight, 0.001) * 0.5 + 0.5);
                // 10. Far calm: shade and crest contrast fade with distance (round 2).
                half farCalm = (half)lerp(1.0, _FarCalm, saturate(input.eyeDepth / max(_CalmDistance, 0.001)));
                body = lerp(body, _ShallowColor.rgb, crest * crest * _CrestAmount * farCalm);

                // 3. Light: luminance with partial hue, plus ambient.
                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half lightLuma = dot(lightColor, half3(0.2126h, 0.7152h, 0.0722h));
                half3 lightTint = lerp(lightLuma.xxx, lightColor, _LightTint);

                // 2. Soft slope shading relative to a flat surface.
                half3 L = mainLight.direction;
                half slopeDelta = dot(nB, L) - L.y;
                half band = smoothstep(-_ShadeSoft, _ShadeSoft, slopeDelta);
                half shade = 1.0h + _ShadeContrast * farCalm * (band * 2.0h - 1.0h);

                half3 rgb = body * (lightTint * shade + _ToonAmbient.rgb * _AmbientStrength);

                // 9. Caustic veins over a shallow bed (where the two ripple octaves cross), near the camera only.
                half veins = 1.0h - (half)saturate(abs(n1.x - n2.x) * 9.0);
                half causticMask = (1.0h - depthT) * (half)smoothstep(0.03, 0.25, depthV) * detailFade;
                rgb += veins * veins * _CausticStrength * causticMask * lightTint;

                // 6. Sky reflection (colour, not grey).
                half3 V = (half3)normalize(_WorldSpaceCameraPos - positionWS);
                half fresnel = pow(1.0h - saturate(dot(nR, V)), _FresnelPower);
                half3 skyColor = lerp(_SkyHorizon.rgb, _SkyTop.rgb, _ReflectZenith);
                rgb = lerp(rgb, skyColor, fresnel * _ReflectStrength);
                alpha = saturate(alpha + fresnel * 0.5h);

                // 7. Saturation lift.
                half luma = dot(rgb, half3(0.2126h, 0.7152h, 0.0722h));
                rgb = max(0.0h, lerp(luma.xxx, rgb, _Saturation));

                // 5. Toon glints (after the saturation lift so they stay near white). Faded in past _GlintNear: a large glint right
                // under the camera reads as a hole, not a glint. Colour: the light's hue at 60 % over white, at the light's peak channel.
                half3 Lg = normalize(half3(L.x, L.y * _GlintFlatten, L.z));
                half3 H = normalize(Lg + V);
                // 11. Low sun (round 2b): as the sun nears the horizon the glints widen (power x0.4, threshold -0.12) and brighten (x1.3), so dusk
                // keeps round 1's golden sun path while the high day sun stays sparse and small.
                half lowSun = (half)(1.0 - saturate(L.y / 0.45));
                float spec = pow(saturate(dot(nG, (float3)H)), (float)_GlintPower * lerp(1.0, 0.4, (float)lowSun));
                float glintAA = max(0.02, fwidth(spec));   // edge width follows the screen-space rate of change (no shimmer at distance)
                half glintCut = _GlintThreshold - 0.12h * lowSun;
                half glint = (half)smoothstep(glintCut - glintAA, glintCut + glintAA, spec);
                glint *= (half)smoothstep(_GlintNear, _GlintNear * 2.5, input.eyeDepth);
                half lightPeak = max(max(lightColor.r, lightColor.g), max(lightColor.b, 0.001h));
                half3 glintColor = lerp(1.0h.xxx, lightColor / lightPeak, 0.6h) * lightPeak;
                rgb += glint * _GlintStrength * (1.0h + 0.3h * lowSun) * glintColor;
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
                // 10. Soft contact band: a quadratic fade from the waterline out to _FoamSoftWidth foam widths (round 2).
                half softEdge = 1.0h - (half)saturate(depthV / max(_FoamWidth * _FoamSoftWidth, 0.001));
                foam = max(foam, softEdge * softEdge * _FoamSoft * (half)(0.75 + 0.25 * wobble));
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
