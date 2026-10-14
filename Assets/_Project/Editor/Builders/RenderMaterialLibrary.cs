// Toon materials (C 7.20, plan T20.5). Owned by T20; T21 adds BuildWorldMaterials (water, line, sky, particle, marker). Authority: none (editor).
// Ensure semantics (same as MaterialLibrary.Ensure): existing .mat files are updated in place so GUIDs never change; the shader is switched
// only when it differs (and then the Lit keyword leftovers are cleared); each property is set only when it differs; SetDirty only on a change.
// Ruling BF-7: _JitterEncoded is 1 for every material drawn on a MeshBuilder mesh (half-scale grey vertex colour, doubled by the shader) and
// 0 for M_Toon_Fish, whose Blender fish models carry full-scale linear albedo.
// R74 look-dev (2026-10-12): every Toon material also gets a ToonLook (soft ramp, sky fill, rim, toon highlight, texture array, gain, tiling, macro).
// M_Toon_Base and the body/skin materials use the env detail array (gain 2); M_Toon_Fish uses the fish skin array (gain 1, layer from the model's uv1.y).
using System.Collections.Generic;
using FishGame.Core;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class RenderMaterialLibrary
    {
        const string ToonShaderName = "FishGame/Toon";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int UsePaletteId = Shader.PropertyToID("_UsePalette");
        static readonly int JitterEncodedId = Shader.PropertyToID("_JitterEncoded");
        static readonly int RampMidId = Shader.PropertyToID("_RampMid");
        static readonly int RampSoftId = Shader.PropertyToID("_RampSoft");

        // FishGame/Water look parameters (variant C of the 2026-10-12 water look-dev); keep equal to the shader property defaults.
        static readonly KeyValuePair<string, float>[] WaterLook =
        {
            new KeyValuePair<string, float>("_LightTint", 0.35f),
            new KeyValuePair<string, float>("_AmbientStrength", 0.35f),
            new KeyValuePair<string, float>("_NormalBoost", 4.5f),
            new KeyValuePair<string, float>("_ShadeContrast", 0.1f),
            new KeyValuePair<string, float>("_ShadeSoft", 0.2f),
            new KeyValuePair<string, float>("_DetailScale", 1.6f),
            new KeyValuePair<string, float>("_DetailStrength", 0.09f),
            new KeyValuePair<string, float>("_DetailSpeed", 0.3f),
            new KeyValuePair<string, float>("_DetailFade", 60f),
            new KeyValuePair<string, float>("_BedCover", 0.35f),
            new KeyValuePair<string, float>("_CausticStrength", 0.12f),
            new KeyValuePair<string, float>("_CrestAmount", 0.25f),
            new KeyValuePair<string, float>("_CrestHeight", 0.11f),
            new KeyValuePair<string, float>("_GlintPower", 220f),
            new KeyValuePair<string, float>("_GlintThreshold", 0.62f),
            new KeyValuePair<string, float>("_GlintStrength", 0.85f),
            new KeyValuePair<string, float>("_GlintFlatten", 0.7f),
            new KeyValuePair<string, float>("_GlintRipple", 2.2f),
            new KeyValuePair<string, float>("_GlintNear", 6f),
            new KeyValuePair<string, float>("_ReflectStrength", 0.36f),
            new KeyValuePair<string, float>("_ReflectZenith", 0.6f),
            new KeyValuePair<string, float>("_FresnelPower", 4f),
            new KeyValuePair<string, float>("_Saturation", 0.95f),
            new KeyValuePair<string, float>("_FogAmount", 0.35f),
            // Harbour round 2 (2026-10-16, calmer toon water: user unlock): soft contact band and far calm (FishGame/Water header, item 10)
            new KeyValuePair<string, float>("_FoamSoft", 0.32f),
            new KeyValuePair<string, float>("_FoamSoftWidth", 2.4f),
            new KeyValuePair<string, float>("_CalmDistance", 70f),
            new KeyValuePair<string, float>("_FarCalm", 0.35f),
            new KeyValuePair<string, float>("_SteepDeepen", 0f),   // round 2b: off - its 2x2-pixel derivatives stair-step pile and rock edges (a per-pixel bed normal would fix it)
        };
        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        static readonly int RimPowId = Shader.PropertyToID("_RimPow");
        static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        static readonly int GradeId = Shader.PropertyToID("_FGGrade");

        // R74 realistic-toon look per material (FishGame/Toon header). Array null = untextured (_FGTexWeight 0).
        internal readonly struct ToonLook
        {
            public readonly float RampMid, RampSoft, RimPow, RimStrength, SkyFill, SpecStrength, SpecPower, Gain, Tiling, Macro;
            public readonly Texture2DArray Array;
            public readonly bool Triplanar;   // _FG_TRIPLANAR (world triplanar mapping); otherwise uv0
            public readonly float Grade;      // _FGGrade: the palette's toon colour grade (top / bottom tint, hue shift); 0 = off
            public ToonLook(float rampMid, float rampSoft, float rimPow, float rimStrength, float skyFill, float specStrength, float specPower,
                Texture2DArray array, float gain, float tiling, float macro = 0f, bool triplanar = false, float grade = 0f)
            {
                RampMid = rampMid; RampSoft = rampSoft; RimPow = rimPow; RimStrength = rimStrength; SkyFill = skyFill;
                SpecStrength = specStrength; SpecPower = specPower; Array = array; Gain = gain; Tiling = tiling; Macro = macro; Triplanar = triplanar;
                Grade = grade;
            }
        }

        // Soft ramp (band 0.02..0.58 of n.l), a little sky fill and rim. Environment and clothes share the env detail array (gain 2, hue from the
        // palette or _BaseColor); the fish read the sRGB skin array (gain 1). Tiling is repeats per metre of uv0 (mesh builders write metres).
        internal const float EnvTiling = 0.6f, ClothTiling = 2.5f;

        public static void BuildAll()
        {
            Shader toon = Shader.Find(ToonShaderName);
            if (toon == null) throw new System.InvalidOperationException("Shader not found: " + ToonShaderName);
            BuilderUtil.EnsureFolder(AssetPaths.MaterialsFolder);
            TextureArrayPostprocessor.EnsureImportSettings();
            Texture2DArray env = TextureArrayPostprocessor.Load(AssetPaths.EnvDetailArray);
            Texture2DArray skin = TextureArrayPostprocessor.Load(AssetPaths.FishSkinArray);

            // Asset study 2026-10-14: the environment materials take the palette's full colour grade; fish, clothes, skin and brass stay ungraded.
            var envLook = new ToonLook(0.30f, 0.28f, 3.5f, 0.18f, 0.22f, 0f, 48f, env, 2f, EnvTiling, 0.7f, grade: 1f);
            var clothLook = new ToonLook(0.30f, 0.28f, 3f, 0.25f, 0.22f, 0f, 48f, env, 2f, ClothTiling);
            // Skin: the env array at tiling 1 so the head's +Z face maps exactly onto the face tile (EnvTextureLayers.Face).
            var skinLook = new ToonLook(0.30f, 0.28f, 3f, 0.25f, 0.22f, 0.10f, 32f, env, 2f, 1f);
            var fishLook = new ToonLook(0.28f, 0.30f, 3f, 0.30f, 0.20f, 0.30f, 180f, skin, 1f, 1f);
            var fallbackLook = new ToonLook(0.30f, 0.28f, 3f, 0.25f, 0.20f, 0.2f, 48f, null, 1f, 1f);

            EnsureToon(toon, "M_Toon_Base", true, true, Color.white, envLook);
            // R74 adoption (shader split): the same environment look with world triplanar mapping, for meshes built with uv1.z = 1 (rocks, canopies).
            var envTriLook = new ToonLook(0.30f, 0.28f, 3.5f, 0.18f, 0.22f, 0f, 48f, env, 2f, EnvTiling, 0.7f, true, 1f);
            EnsureToon(toon, "M_Toon_Tri", true, true, Color.white, envTriLook);
            // Harbour round 2 (2026-10-16, user unlock "painterly crown trees"): crown cards. The env look (graded, palette colour, soft ramp) with the
            // painted card atlas clipped at 0.5 (_ALPHATEST_ON), both faces drawn; the array is off (the cards carry their own painted grey).
            var foliageLook = new ToonLook(0.30f, 0.34f, 3.5f, 0.08f, 0.30f, 0f, 48f, null, 1f, 1f, 0f, false, 1f);
            EnsureToon(toon, "M_Toon_Foliage", true, true, Color.white, foliageLook);
            EnsureFoliageCards(RenderMaterialLibrary.Get("M_Toon_Foliage"), EnsureCrownCardImport());
            EnsureToon(toon, "M_Toon_Fish", false, false, Color.white, fishLook);
            EnsureToon(toon, "M_Toon_FishFallback", false, true, Color.white, fallbackLook);
            // R74 adoption: the dock bell's brass (smooth mesh, vertex colour ao), a strong toon highlight instead of the Lit M_Brass.
            var brassLook = new ToonLook(0.30f, 0.28f, 3f, 0.30f, 0.20f, 0.45f, 40f, null, 1f, 1f);
            EnsureToon(toon, "M_Toon_Brass", false, true, new Color(0.82f, 0.62f, 0.22f), brassLook);
            for (int i = 0; i < SlotPalette.Count; i++)
            {
                SlotPalette.GetRgb(i, out float r, out float g, out float b);
                EnsureToon(toon, "M_Body_Slot" + i, false, true, new Color(r, g, b), clothLook);
            }
            SlotPalette.GetSkinRgb(out float sr, out float sg, out float sb);
            EnsureToon(toon, "M_Skin", false, true, new Color(sr, sg, sb), skinLook);

            // BuildWorldMaterials(WaterSettingsDef) (T21) is called from RenderingSetupBuilder.Build, after T14's WaterSettings asset exists (ruling F5).

            FishModelPostprocessor.EnsureImportSettings();
        }

        // T21 (ruling F5): water, line, sky, particle and marker materials. Same ensure semantics as BuildAll.
        // M_Water_Surface copies colours and ranges from the WaterSettingsDef asset (a designer retune needs a build).
        internal static void BuildWorldMaterials(FishGame.Data.WaterSettingsDef water)
        {
            if (water == null) throw new System.ArgumentNullException(nameof(water));
            BuilderUtil.EnsureFolder(AssetPaths.MaterialsFolder);

            Material surface = EnsureMaterial("M_Water_Surface", RequireShader("FishGame/Water"), out bool surfaceChanged);
            surfaceChanged |= SetColor(surface, Shader.PropertyToID("_ShallowColor"), water.ShallowColor);
            surfaceChanged |= SetColor(surface, Shader.PropertyToID("_DeepColor"), water.DeepColor);
            surfaceChanged |= SetFloat(surface, Shader.PropertyToID("_DepthRange"), water.DepthRange);
            surfaceChanged |= SetFloat(surface, Shader.PropertyToID("_FoamWidth"), water.FoamWidth);
            // The stylised look parameters (FishGame/Water header) are pinned to the shader defaults so an existing material follows a shader retune.
            foreach (var p in WaterLook) surfaceChanged |= SetFloat(surface, Shader.PropertyToID(p.Key), p.Value);
            surfaceChanged |= SetColor(surface, Shader.PropertyToID("_FoamColor"), new Color(0.96f, 0.99f, 1.0f, 1.0f));
            if (surfaceChanged) EditorUtility.SetDirty(surface);

            // Harbour round 1 (2026-10-15): the same water on shallow visual ribbons (the river, the creek, the tide pools) that sit a few cm over the
            // land. On M_Water_Surface their whole width is inside the shore-foam band and they drew as white strips; here the foam band is gone and
            // the depth range is a few cm, so they read as clear shallow water over the bed.
            Material creek = EnsureMaterial("M_Water_Creek", RequireShader("FishGame/Water"), out bool creekChanged);
            // Harbour round 4 (2026-10-18, critics: "a pale turquoise strip lying on the lawn"): a deeper green-teal, a little clearer, over the dark
            // gravel bed the creek and the river now run on (fg_harbour.py build_glen), so they read as running water in a bed, not paint.
            creekChanged |= SetColor(creek, Shader.PropertyToID("_ShallowColor"), new Color(0.16f, 0.50f, 0.46f, 0.6f));
            creekChanged |= SetColor(creek, Shader.PropertyToID("_DeepColor"), new Color(0.05f, 0.27f, 0.33f, 0.92f));
            foreach (var p in WaterLook) creekChanged |= SetFloat(creek, Shader.PropertyToID(p.Key), p.Value);
            creekChanged |= SetFloat(creek, Shader.PropertyToID("_DepthRange"), 0.16f);
            creekChanged |= SetFloat(creek, Shader.PropertyToID("_FoamWidth"), 0.001f);
            creekChanged |= SetColor(creek, Shader.PropertyToID("_FoamColor"), new Color(0.96f, 0.99f, 1.0f, 1.0f));
            if (creekChanged) EditorUtility.SetDirty(creek);

            // Harbour round 3 (2026-10-17): the harbour's own water - M_Water_Surface's look plus the bed map (FishGame/Water item 13): colour and
            // opacity from the natural bed (no turquoise ghosts over rocks and hulls, no crease at the floor's edge) and the creek lagoon's zone
            // tint. HarbourPierBuilder bakes T_HarbourBedMap and sets _BedMap / _BedMapST on it. The lake keeps M_Water_Surface (bed map off).
            Material harbour = EnsureMaterial("M_Water_Harbour", RequireShader("FishGame/Water"), out bool harbourChanged);
            harbourChanged |= SetColor(harbour, Shader.PropertyToID("_ShallowColor"), water.ShallowColor);
            harbourChanged |= SetColor(harbour, Shader.PropertyToID("_DeepColor"), water.DeepColor);
            harbourChanged |= SetFloat(harbour, Shader.PropertyToID("_DepthRange"), water.DepthRange);
            harbourChanged |= SetFloat(harbour, Shader.PropertyToID("_FoamWidth"), water.FoamWidth);
            foreach (var p in WaterLook) harbourChanged |= SetFloat(harbour, Shader.PropertyToID(p.Key), p.Value);
            harbourChanged |= SetColor(harbour, Shader.PropertyToID("_FoamColor"), new Color(0.96f, 0.99f, 1.0f, 1.0f));
            harbourChanged |= SetFloat(harbour, Shader.PropertyToID("_BedMapOn"), 1f);
            harbourChanged |= SetColor(harbour, Shader.PropertyToID("_ZoneShallow"), new Color(0.42f, 0.88f, 0.72f, 0.62f));
            harbourChanged |= SetColor(harbour, Shader.PropertyToID("_ZoneDeep"), new Color(0.1f, 0.52f, 0.52f, 0.95f));
            harbourChanged |= SetFloat(harbour, Shader.PropertyToID("_SolidShade"), 0.3f);
            if (harbourChanged) EditorUtility.SetDirty(harbour);

            // Harbour round 4: the West Head brook and its fall (FishGame/Waterfall, shader defaults: streaks scrolled by _WaterTime).
            Material fall = EnsureMaterial("M_Waterfall", RequireShader("FishGame/Waterfall"), out bool fallChanged);
            if (fallChanged) EditorUtility.SetDirty(fall);

            Material line = EnsureMaterial("M_Line", RequireShader("FishGame/Line"), out bool lineChanged);   // shader defaults
            if (lineChanged) EditorUtility.SetDirty(line);
            Material sky = EnsureMaterial("M_Sky", RequireShader("FishGame/GradientSky"), out bool skyChanged);   // shader defaults
            if (skyChanged) EditorUtility.SetDirty(sky);

            Material particle = EnsureMaterial("M_Particle", RequireShader("Universal Render Pipeline/Particles/Unlit"), out bool particleChanged);
            particleChanged |= SetTransparentAlphaBlend(particle, 3000);
            particleChanged |= SetColor(particle, BaseColorId, Color.white);
            // The URP importer writes (0, +Infinity, 0, 0) here while camera fading is off; match it so the build and the import agree.
            particleChanged |= SetVector(particle, Shader.PropertyToID("_CameraFadeParams"), new Vector4(0f, float.PositiveInfinity, 0f, 0f));
            if (particleChanged) EditorUtility.SetDirty(particle);

            // Harbour round 0 (2026-10-14): the night glow (FishGame/LampGlow, additive; _FGLampGlow drives it) for the string-light bulbs (palette
            // slot 10, Accent, of Harbour_Detail_Lights) and for the small lamp, mast and beacon glow spheres (every vertex), and the chimney smoke
            // (URP particles, alpha blend, the soft puff texture).
            Shader glowShader = RequireShader("FishGame/LampGlow");
            Material bulbs = EnsureMaterial("M_LampGlow", glowShader, out bool bulbsChanged);
            bulbsChanged |= SetFloat(bulbs, Shader.PropertyToID("_Slot"), 10f);
            bulbsChanged |= SetFloat(bulbs, Shader.PropertyToID("_Inflate"), 0.012f);
            bulbsChanged |= SetFloat(bulbs, Shader.PropertyToID("_GlowBoost"), 2.5f);
            bulbsChanged |= SetQueue(bulbs, 3050);
            if (bulbsChanged) EditorUtility.SetDirty(bulbs);
            Material spheres = EnsureMaterial("M_LampGlow_All", glowShader, out bool spheresChanged);
            spheresChanged |= SetFloat(spheres, Shader.PropertyToID("_Slot"), -1f);
            spheresChanged |= SetFloat(spheres, Shader.PropertyToID("_Inflate"), 0f);
            spheresChanged |= SetFloat(spheres, Shader.PropertyToID("_GlowBoost"), 3f);
            spheresChanged |= SetQueue(spheres, 3050);
            if (spheresChanged) EditorUtility.SetDirty(spheres);
            Material beam = EnsureMaterial("M_BeaconBeam", glowShader, out bool beamChanged);   // the lighthouse's sweeping beam: faint, every vertex
            beamChanged |= SetFloat(beam, Shader.PropertyToID("_Slot"), -1f);
            beamChanged |= SetFloat(beam, Shader.PropertyToID("_Inflate"), 0f);
            beamChanged |= SetFloat(beam, Shader.PropertyToID("_GlowBoost"), 0.42f);   // round 4: brighter at the lamp, faded along and at the edges
            beamChanged |= SetFloat(beam, Shader.PropertyToID("_BeamFade"), 1f);
            beamChanged |= SetColor(beam, Shader.PropertyToID("_GlowColor"), new Color(1.0f, 0.9f, 0.7f, 1f));
            beamChanged |= SetQueue(beam, 3060);
            if (beamChanged) EditorUtility.SetDirty(beam);

            Texture2D puff = EnsureSoftPuffTexture();
            Material smoke = EnsureMaterial("M_Smoke", RequireShader("Universal Render Pipeline/Particles/Unlit"), out bool smokeChanged);
            smokeChanged |= SetTransparentAlphaBlend(smoke, 3000);
            smokeChanged |= SetColor(smoke, BaseColorId, Color.white);
            smokeChanged |= SetVector(smoke, Shader.PropertyToID("_CameraFadeParams"), new Vector4(0f, float.PositiveInfinity, 0f, 0f));
            int baseMapId = Shader.PropertyToID("_BaseMap");
            if (smoke.GetTexture(baseMapId) != puff) { smoke.SetTexture(baseMapId, puff); smokeChanged = true; }
            if (smokeChanged) EditorUtility.SetDirty(smoke);

            // Queue 3100 draws after the transparent water (ZWrite Off), so the marker is never tinted or hidden by it. Cull Off: marker winding never matters.
            // URP's material importer recomputes the queue of every URP/Unlit material (opaque = 2000 + _QueueOffset, transparent = 3000 + _QueueOffset) and
            // there is no queue override on this shader, so an opaque marker would be reset to 2000 on the next import. The marker is therefore an alpha-blend
            // transparent surface with _QueueOffset 100, which URP itself resolves to 3100 (a stable fixed point; _BaseColor alpha 1 keeps it solid).
            Material marker = EnsureMaterial("M_Marker", RequireShader("Universal Render Pipeline/Unlit"), out bool markerChanged);
            markerChanged |= SetColor(marker, BaseColorId, Color.white);
            markerChanged |= SetFloat(marker, Shader.PropertyToID("_Cull"), 0f);
            markerChanged |= SetFloat(marker, Shader.PropertyToID("_QueueOffset"), 100f);
            markerChanged |= SetTransparentAlphaBlend(marker, 3100);
            markerChanged |= SetPassEnabled(marker, "MotionVectors", false);   // what the URP importer settles on for a double-sided (Cull Off) Unlit material
            if (!marker.doubleSidedGI) { marker.doubleSidedGI = true; markerChanged = true; }
            if (markerChanged) EditorUtility.SetDirty(marker);
        }

        // A 64 x 64 soft round puff (white, alpha = a smooth radial falloff with a little lumpy noise), written once and re-written only when the
        // bytes differ, so the asset and its GUID are stable. Deterministic: pure maths, no Random.
        internal const string SoftPuffPath = AssetPaths.TexturesFolder + "/T_SoftPuff.png";

        static Texture2D EnsureSoftPuffTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Atan2(v, u);
                    float lump = 1f + 0.08f * Mathf.Sin(a * 5f + 0.7f) + 0.05f * Mathf.Sin(a * 9f + 2.1f);
                    float k = Mathf.Clamp01(1f - r * lump);
                    float alpha = k * k * (3f - 2f * k);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(SoftPuffPath);
            if (!System.IO.File.Exists(abs) || !System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(abs), png))
            {
                BuilderUtil.EnsureFolder(AssetPaths.TexturesFolder);
                System.IO.File.WriteAllBytes(abs, png);
                AssetDatabase.ImportAsset(SoftPuffPath, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(SoftPuffPath);
            if (importer.alphaIsTransparency != true || importer.wrapMode != TextureWrapMode.Clamp || importer.sRGBTexture != true
                || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftPuffPath);
            if (asset == null) throw new System.InvalidOperationException("could not import " + SoftPuffPath);
            return asset;
        }

        // URP transparent surface, alpha blend, ZWrite off: exactly what BaseShaderGUI.SetupMaterialBlendMode would write (so an import changes nothing).
        static bool SetTransparentAlphaBlend(Material material, int queue)
        {
            bool changed = false;
            changed |= SetFloat(material, Shader.PropertyToID("_Surface"), 1f);
            changed |= SetFloat(material, Shader.PropertyToID("_Blend"), 0f);
            changed |= SetFloat(material, Shader.PropertyToID("_SrcBlend"), 5f);          // SrcAlpha
            changed |= SetFloat(material, Shader.PropertyToID("_DstBlend"), 10f);         // OneMinusSrcAlpha
            changed |= SetFloat(material, Shader.PropertyToID("_SrcBlendAlpha"), 1f);
            changed |= SetFloat(material, Shader.PropertyToID("_DstBlendAlpha"), 10f);
            changed |= SetFloat(material, Shader.PropertyToID("_ZWrite"), 0f);
            changed |= SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
            if (material.GetTag("RenderType", false) != "Transparent") { material.SetOverrideTag("RenderType", "Transparent"); changed = true; }
            changed |= SetQueue(material, queue);
            changed |= SetPassEnabled(material, "DepthOnly", false);       // a transparent surface does not write depth or cast shadows (URP importer does the same)
            changed |= SetPassEnabled(material, "ShadowCaster", false);
            return changed;
        }

        // Exact component compare (Vector4 == is approximate and never equal for Infinity).
        static bool SetVector(Material material, int id, Vector4 value)
        {
            if (material.HasProperty(id))
            {
                Vector4 current = material.GetVector(id);
                if (current.x == value.x && current.y == value.y && current.z == value.z && current.w == value.w) return false;
            }
            material.SetVector(id, value);
            return true;
        }

        static bool SetPassEnabled(Material material, string pass, bool enabled)
        {
            if (material.GetShaderPassEnabled(pass) == enabled) return false;
            material.SetShaderPassEnabled(pass, enabled);
            return true;
        }

        static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + name);
            return shader;
        }

        // Loads or creates MaterialsFolder/<name>.mat. The shader is switched only when it differs (then the keywords are cleared).
        static Material EnsureMaterial(string name, Shader shader, out bool changed)
        {
            string path = AssetPaths.MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            changed = false;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material.shader != shader)
            {
                material.shader = shader;
                material.shaderKeywords = new string[0];
                changed = true;
            }
            return material;
        }

        static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled) return false;
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
            return true;
        }

        static bool SetQueue(Material material, int queue)
        {
            if (material.renderQueue == queue) return false;
            material.renderQueue = queue;
            return true;
        }

        // name without extension, e.g. "M_Toon_Base".
        public static Material Get(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/" + name + ".mat");
            if (material == null) throw new System.InvalidOperationException("Material not built: " + name + " (run RenderMaterialLibrary.BuildAll first)");
            return material;
        }

        static readonly int AlbedoArrayId = Shader.PropertyToID("_FGAlbedoArray");
        static readonly int TexWeightId = Shader.PropertyToID("_FGTexWeight");
        static readonly int ArrayGainId = Shader.PropertyToID("_FGArrayGain");
        static readonly int TilingId = Shader.PropertyToID("_FGTiling");
        static readonly int MacroId = Shader.PropertyToID("_FGMacro");
        static readonly int SkyFillId = Shader.PropertyToID("_SkyFill");
        static readonly int SpecStrengthId = Shader.PropertyToID("_SpecStrength");
        static readonly int SpecPowerId = Shader.PropertyToID("_SpecPower");

        static void EnsureToon(Shader toon, string name, bool usePalette, bool jitterEncoded, Color baseColor, ToonLook look)
        {
            string path = AssetPaths.MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(toon);
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material.shader != toon)
            {
                material.shader = toon;
                material.shaderKeywords = new string[0];   // drops the Lit leftovers (_NORMALMAP and friends)
                changed = true;
            }
            changed |= SetColor(material, BaseColorId, baseColor);
            changed |= SetFloat(material, UsePaletteId, usePalette ? 1f : 0f);
            changed |= SetFloat(material, JitterEncodedId, jitterEncoded ? 1f : 0f);
            changed |= SetFloat(material, RampMidId, look.RampMid);
            changed |= SetFloat(material, RampSoftId, look.RampSoft);
            changed |= SetFloat(material, DesaturateId, 0f);
            changed |= SetFloat(material, RimPowId, look.RimPow);
            changed |= SetFloat(material, RimStrengthId, look.RimStrength);
            changed |= SetFloat(material, SkyFillId, look.SkyFill);
            changed |= SetFloat(material, SpecStrengthId, look.SpecStrength);
            changed |= SetFloat(material, SpecPowerId, look.SpecPower);
            changed |= SetFloat(material, TexWeightId, look.Array != null ? 1f : 0f);
            changed |= SetFloat(material, ArrayGainId, look.Gain);
            changed |= SetFloat(material, TilingId, look.Tiling);
            changed |= SetFloat(material, MacroId, look.Macro);
            changed |= SetFloat(material, Shader.PropertyToID("_UseTriplanar"), look.Triplanar ? 1f : 0f);
            changed |= SetKeyword(material, "_FG_TRIPLANAR", look.Triplanar);
            changed |= SetFloat(material, GradeId, look.Grade);
            if (material.GetTexture(AlbedoArrayId) != look.Array) { material.SetTexture(AlbedoArrayId, look.Array); changed = true; }
            if (changed) EditorUtility.SetDirty(material);
        }

        // T_CrownCards.png: linear painted grey + cutout alpha, mips that keep the 0.5 coverage (the crowns do not thin out with distance),
        // colour dilated under the transparent texels, clamped, 1024.
        static Texture2D EnsureCrownCardImport()
        {
            var importer = AssetImporter.GetAtPath(AssetPaths.CrownCardAtlas) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException("missing " + AssetPaths.CrownCardAtlas + " (run Art/Blender/fg_crowncards.py)");
            bool dirty = importer.textureType != TextureImporterType.Default || importer.sRGBTexture || !importer.alphaIsTransparency
                         || importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.mipmapEnabled || !importer.mipMapsPreserveCoverage
                         || importer.alphaTestReferenceValue != 0.5f || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 1024
                         || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 2;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 2;
                importer.SaveAndReimport();
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPaths.CrownCardAtlas);
            if (tex == null) throw new System.InvalidOperationException("could not load " + AssetPaths.CrownCardAtlas);
            return tex;
        }

        static void EnsureFoliageCards(Material material, Texture2D cards)
        {
            bool changed = SetKeyword(material, "_ALPHATEST_ON", true);
            changed |= SetFloat(material, Shader.PropertyToID("_AlphaClip"), 1f);
            changed |= SetFloat(material, Shader.PropertyToID("_Cutoff"), 0.5f);
            changed |= SetFloat(material, Shader.PropertyToID("_CardGain"), 2.0f);
            changed |= SetFloat(material, Shader.PropertyToID("_Cull"), (float)UnityEngine.Rendering.CullMode.Off);
            int baseMap = Shader.PropertyToID("_BaseMap");
            if (material.GetTexture(baseMap) != cards) { material.SetTexture(baseMap, cards); changed = true; }
            if (material.GetTextureScale(baseMap) != Vector2.one || material.GetTextureOffset(baseMap) != Vector2.zero)
            {
                material.SetTextureScale(baseMap, Vector2.one);
                material.SetTextureOffset(baseMap, Vector2.zero);
                changed = true;
            }
            if (!material.enableInstancing) { material.enableInstancing = true; changed = true; }   // the ground cover draws it instanced
            if (material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.AlphaTest) { material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; changed = true; }
            if (material.GetTag("RenderType", false) != "TransparentCutout") { material.SetOverrideTag("RenderType", "TransparentCutout"); changed = true; }
            if (changed) EditorUtility.SetDirty(material);
        }

        static bool SetFloat(Material material, int id, float value)
        {
            if (material.HasProperty(id) && material.GetFloat(id) == value) return false;
            material.SetFloat(id, value);
            return true;
        }

        static bool SetColor(Material material, int id, Color value)
        {
            if (material.HasProperty(id) && material.GetColor(id) == value) return false;
            material.SetColor(id, value);
            return true;
        }
    }
}
