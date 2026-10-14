// Harbour polish capture (harbour round 0, 2026-10-14). Authority: none (editor-only dev tool). Never saves the scene or any asset.
// Batch entry (needs a graphics device: run WITHOUT -nographics):
//   Unity.exe -batchmode -projectPath <proj> -quit -executeMethod FishGame.EditorTools.HarbourCapture.Capture -fgShotOut <abs dir>
// Opens Spot_HarbourPier, pushes the shader globals the runtime pushes (palette, waves, day fraction, lamp glow; LookDevCapture's helpers), poses
// the ambient life at a fixed time (AmbientLife.Pose), pre-warms the smoke (ParticleSystem.Simulate, fixed seeds) and renders a FIXED set of
// 1280 x 720 shots with stable names (Shots), so every polish round compares like with like. low_preset_overview is rendered with the Low preset
// applied: the detail hidden (AmbientLife.ApplyDetail(false)) and the URP shadow tier of Low set on the asset IN MEMORY and put back after.
// Writes tris.txt: per shot, the triangles of the renderers that camera sees (frustum and culling mask, enabled and active), with the detail
// drawn (High) and hidden (Low), the renderer counts, and the worst view of each.
using System.Collections.Generic;
using System.IO;
using System.Text;
using FishGame.Core;
using FishGame.Data;
using FishGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class HarbourCapture
    {
        const int Width = 1280, Height = 720;
        const int DayPhase = 1, DuskPhase = 2;
        const float PoseTime = 7.3f;        // seconds: the ambient-life pose every shot uses
        const float SmokeWarmSeconds = 12f;

        struct Shot
        {
            public string Name;
            public Vector3 Eye, Target;
            public float Fov;
            public bool Dusk, Low;
            public Shot(string name, Vector3 eye, Vector3 target, float fov, bool dusk = false, bool low = false)
            {
                Name = name; Eye = eye; Target = target; Fov = fov; Dusk = dusk; Low = low;
            }
        }

        static float Eye(float groundY) => groundY + MovementMath.EyeHeight;

        // Fixed views (world: x east, z out to sea, the coast along z -45, the plateau y 1.2, the deck y 1.5). Stable names: never rename a shot.
        static readonly Shot[] Shots =
        {
            new Shot("overview_high", new Vector3(48f, 30f, 8f), new Vector3(-4f, 0f, -52f), 50f),
            new Shot("pier_from_beach", new Vector3(-7f, Eye(0.25f), -47.5f), new Vector3(1.5f, 1.6f, -10f), 55f),
            new Shot("tug_hero", new Vector3(47f, 3.2f, -10.5f), new Vector3(36.5f, 2.4f, -22.5f), 46f),
            new Shot("village_street", new Vector3(-4f, 4.6f, -56.5f), new Vector3(14f, 2.6f, -64f), 58f),
            new Shot("bait_lane_player_eye", new Vector3(-11f, Eye(1.2f), -60.6f), new Vector3(-28f, 2.6f, -63.5f), 58f),
            new Shot("breakwater_walk", new Vector3(30f, Eye(1.5f), -53.5f), new Vector3(32.5f, 2.2f, -24f), 60f),
            new Shot("shoreline_east", new Vector3(36f, Eye(0.25f), -47.2f), new Vector3(80f, 5f, -22f), 58f),
            new Shot("dunes_backdrop", new Vector3(2f, 3.4f, -30f), new Vector3(-2f, 5.5f, -95f), 55f),
            new Shot("dusk_overview", new Vector3(48f, 30f, 8f), new Vector3(-4f, 0f, -52f), 50f, dusk: true),
            new Shot("low_preset_overview", new Vector3(48f, 30f, 8f), new Vector3(-4f, 0f, -52f), 50f, low: true),
            // extras
            new Shot("dusk_village", new Vector3(-1f, Eye(1.2f), -59.5f), new Vector3(-20f, 3f, -63f), 60f, dusk: true),
            new Shot("thead_seaward", new Vector3(0f, Eye(1.5f), -6f), new Vector3(25f, 2f, 60f), 58f),
            new Shot("west_beach_cliffs", new Vector3(-40f, Eye(0.25f), -47f), new Vector3(-85f, 6f, -10f), 58f),
        };

        public static void Capture()
        {
            string outDir = LookDevCapture.Arg("-fgShotOut");
            if (string.IsNullOrEmpty(outDir)) throw new System.ArgumentException("-fgShotOut <abs dir> is required");
            Directory.CreateDirectory(outDir);

            Scene scene = EditorSceneManager.OpenScene(AssetPaths.HarbourPierScene, OpenSceneMode.Single);
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            if (palette == null || water == null) throw new System.InvalidOperationException("palette or water settings missing");
            LookDevCapture.PushPalette(palette);
            LookDevCapture.PushWaves(water);
            Light sun = LookDevCapture.FindSun();
            Quaternion daySun = sun.transform.rotation;

            AmbientLife life = Object.FindAnyObjectByType<AmbientLife>();
            if (life == null) throw new System.InvalidOperationException("the harbour scene has no AmbientLife (run the build)");
            var detail = new List<GameObject>();
            for (int i = 0; i < life.DetailCount; i++) detail.Add(life.Detail(i));

            var cameraGo = new GameObject("HarbourCaptureCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = MovementMath.NearClip;
            camera.farClipPlane = MovementMath.FarClip;
            cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            Camera sceneCamera = GameObject.Find("SceneCamera")?.GetComponent<Camera>();
            if (sceneCamera != null) sceneCamera.enabled = false;

            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float shadowDistance = asset != null ? asset.shadowDistance : 0f;
            int shadowRes = asset != null ? asset.mainLightShadowmapResolution : 0, cascades = asset != null ? asset.shadowCascadeCount : 0;

            var report = new StringBuilder();
            report.Append("# shot | high_tris | low_tris | high_renderers | low_renderers   (renderers this camera sees; Low = detail hidden)\n");
            long worstHigh = 0, worstLow = 0;
            string worstHighShot = "", worstLowShot = "";
            try
            {
                foreach (Shot s in Shots)
                {
                    float dayFraction = s.Dusk ? DuskFraction(palette) : DayNightControllerHoldFraction;
                    float glow = DayNightMath.LampGlow(dayFraction, palette.SunMoon.sunriseFraction, palette.SunMoon.sunsetFraction, 0.05f);
                    Quaternion sunRot = s.Dusk ? DuskSun(palette, dayFraction) : daySun;
                    LookDevCapture.ApplyPhase(palette, s.Dusk ? DuskPhase : DayPhase, sun, sunRot);
                    Shader.SetGlobalFloat(ShaderGlobals.DayFraction, dayFraction);
                    Shader.SetGlobalFloat(ShaderGlobals.LampGlow, glow);
                    Color tint = s.Dusk ? Color.Lerp(palette.Phases[DuskPhase].toonAmbient, sun.color, Mathf.Clamp01(sun.intensity)) : Color.white;
                    tint.a = 1f;

                    life.ApplyDetail(true);
                    if (asset != null && s.Low)
                    {
                        ShadowParams low = GraphicsPresets.ShadowFor((int)ShadowTier.Low);
                        asset.shadowDistance = low.Distance;
                        asset.mainLightShadowmapResolution = low.Resolution;
                        asset.shadowCascadeCount = low.Cascades;
                    }
                    life.Pose(PoseTime, glow, tint);
                    WarmSmoke();

                    camera.transform.position = s.Eye;
                    camera.transform.rotation = Quaternion.LookRotation(s.Target - s.Eye, Vector3.up);
                    camera.fieldOfView = s.Fov;
                    Count(camera, detail, out long highTris, out long lowTris, out int highR, out int lowR);
                    report.Append(s.Name).Append(" | ").Append(highTris).Append(" | ").Append(lowTris).Append(" | ").Append(highR).Append(" | ").Append(lowR).Append('\n');
                    if (highTris > worstHigh) { worstHigh = highTris; worstHighShot = s.Name; }
                    if (lowTris > worstLow) { worstLow = lowTris; worstLowShot = s.Name; }
                    life.ApplyDetail(!s.Low);   // after the counts: an inactive renderer has no bounds
                    Render(camera, Path.Combine(outDir, s.Name + ".png"));

                    if (asset != null && s.Low)
                    {
                        asset.shadowDistance = shadowDistance;
                        asset.mainLightShadowmapResolution = shadowRes;
                        asset.shadowCascadeCount = cascades;
                    }
                }
            }
            finally
            {
                life.ApplyDetail(true);
                if (asset != null)
                {
                    asset.shadowDistance = shadowDistance;
                    asset.mainLightShadowmapResolution = shadowRes;
                    asset.shadowCascadeCount = cascades;
                }
                Object.DestroyImmediate(cameraGo);
            }
            report.Append("worst_high ").Append(worstHigh).Append(' ').Append(worstHighShot).Append('\n');
            report.Append("worst_low ").Append(worstLow).Append(' ').Append(worstLowShot).Append('\n');
            File.WriteAllText(Path.Combine(outDir, "tris.txt"), report.ToString());
            Debug.Log("FG HARBOUR CAPTURE " + Shots.Length + " shots out=" + outDir + " worst_high=" + worstHigh + " (" + worstHighShot + ") worst_low=" + worstLow + " (" + worstLowShot + ")");
        }

        // DayNightController.DayHoldFraction (internal to Presentation): the noon-ish hold the scene shows without a world state.
        const float DayNightControllerHoldFraction = 0.25f;

        // Late dusk: 0.02 of a day before sunset (the lamps are about two thirds on), the sun low in the west.
        static float DuskFraction(ArtPaletteDef palette) => palette.SunMoon.sunsetFraction - 0.02f;

        static Quaternion DuskSun(ArtPaletteDef palette, float f)
        {
            DayNightMath.SunDirection(f, palette.SunMoon.sunriseFraction, palette.SunMoon.sunsetFraction, palette.SunMoon.minElevationDeg,
                                      out float x, out float y, out float z);
            return Quaternion.LookRotation(new Vector3(-x, -y, -z), Vector3.up);
        }

        static void WarmSmoke()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(false))
                    ps.Simulate(SmokeWarmSeconds, true, true, true);
        }

        static bool Under(Transform t, List<GameObject> roots)
        {
            for (Transform u = t; u != null; u = u.parent) if (roots.Contains(u.gameObject)) return true;
            return false;
        }

        // Triangles of the mesh renderers this camera would draw: active, enabled, on a layer in its culling mask, inside its frustum. The Low
        // count leaves out everything under the detail objects (what Low hides). Shadow-caster passes are not counted.
        static void Count(Camera camera, List<GameObject> detail, out long high, out long low, out int highR, out int lowR)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            high = low = 0; highR = lowR = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    bool inDetail = Under(r.transform, detail);
                    // Detail is counted as High draws it (active), whatever the current shot set.
                    bool active = inDetail ? ActiveIgnoring(r.transform, detail) : r.gameObject.activeInHierarchy;
                    if (!active || !r.enabled || r.forceRenderingOff) continue;
                    if ((camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    long tris = 0;
                    for (int s = 0; s < f.sharedMesh.subMeshCount; s++) tris += f.sharedMesh.GetIndexCount(s) / 3;
                    high += tris; highR++;
                    if (!inDetail) { low += tris; lowR++; }
                }
        }

        // activeInHierarchy, treating the detail objects themselves as active (the Low shot deactivates them).
        static bool ActiveIgnoring(Transform t, List<GameObject> detail)
        {
            for (Transform u = t; u != null; u = u.parent)
                if (!u.gameObject.activeSelf && !detail.Contains(u.gameObject)) return false;
            return true;
        }

        static void Render(Camera camera, string path)
        {
            // Inactive detail renderers report empty bounds; render after the counts. Two frames: shadow and depth history settled.
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
