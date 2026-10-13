// Look-dev capture (R74 look-dev, 2026-10-12). Authority: none (editor-only dev tool). Never saves the scene or any asset.
// Batch entry (needs a graphics device, so run WITHOUT -nographics; never use it for UI Toolkit/HUD screenshots):
//   Unity.exe -batchmode -projectPath <proj> -quit -executeMethod FishGame.EditorTools.LookDevCapture.Capture
//     -fgLookOut <abs dir> [-fgLookTag before|after] [-fgLookSheet <abs png> -fgLookBefore <abs dir>]
// Opens Spot_LakeDock, pushes the same shader globals the runtime pushes (PaletteGlobals, WaterTimeDriver), drops a Player prefab instance and the
// bluegill model into the scene, renders five fixed shots (dock end, shore, player close-up, bluegill reveal close-up, dusk) to PNG, and writes a
// collider dump (colliders_<tag>.txt) so a look change can prove the physics fingerprint did not move. With -fgLookSheet it also composes a
// before | after sheet (rows in shot order) from the PNGs in -fgLookBefore and this run's PNGs.
using System.Collections.Generic;
using System.IO;
using System.Text;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class LookDevCapture
    {
        const int Width = 960, Height = 540;
        const int DayPhase = 1, DuskPhase = 2;
        public static readonly string[] Shots = { "dock_end", "shore", "player", "bluegill", "dusk" };

        public static void Capture()
        {
            string outDir = Arg("-fgLookOut");
            if (string.IsNullOrEmpty(outDir)) throw new System.ArgumentException("-fgLookOut <dir> is required");
            string tag = Arg("-fgLookTag") ?? "shot";
            Directory.CreateDirectory(outDir);

            Scene scene = EditorSceneManager.OpenScene(AssetPaths.LakeDockScene, OpenSceneMode.Single);
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            if (palette == null || water == null) throw new System.InvalidOperationException("palette or water settings missing");

            File.WriteAllText(Path.Combine(outDir, "colliders_" + tag + ".txt"), DumpColliders(scene));

            PushPalette(palette);
            PushWaves(water);
            Light sun = FindSun();
            Quaternion daySun = sun.transform.rotation;

            GameObject player = SpawnPlayer();
            GameObject fish = SpawnFish();

            var cameraGo = new GameObject("LookDevCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = MovementMath.NearClip;
            camera.farClipPlane = MovementMath.FarClip;
            cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            Camera sceneCamera = GameObject.Find("SceneCamera")?.GetComponent<Camera>();
            if (sceneCamera != null) sceneCamera.enabled = false;

            float wideFov = Camera.HorizontalToVerticalFieldOfView(MovementMath.DefaultHorizontalFov, Width / (float)Height);

            // 1 dock end: from the water off the far end, the deck, posts and ladders, the shore behind.
            ApplyPhase(palette, DayPhase, sun, daySun);
            player.SetActive(false); fish.SetActive(false);
            Shoot(camera, new Vector3(4.2f, 1.9f, -36.2f), new Vector3(-0.3f, 0.2f, -45f), wideFov, outDir, "dock_end");

            // 2 shore: a rock on the shore from the shallows, trees behind it.
            Transform rock = FindRock();
            Vector3 rockPos = rock.position;
            Vector3 toCentre = new Vector3(-rockPos.x, 0f, -rockPos.z).normalized;
            Shoot(camera, rockPos + toCentre * 4.5f + Vector3.up * 1.5f, rockPos + Vector3.up * 1.2f - toCentre * 5f, 50f, outDir, "shore");

            // 3 player close-up: three-quarter front, on the dock.
            player.SetActive(true);
            Vector3 playerEye = new Vector3(-1.5f, 2.05f, -50.2f), playerAt = new Vector3(0f, LakeDockLayoutDeckTop, -47f);
            Vector3 toCamera = new Vector3(playerEye.x - playerAt.x, 0f, playerEye.z - playerAt.z);
            player.transform.SetPositionAndRotation(playerAt, Quaternion.LookRotation(toCamera, Vector3.up) * Quaternion.Euler(0f, -20f, 0f));
            Shoot(camera, playerEye, new Vector3(0f, 1.55f, -47f), 40f, outDir, "player");
            player.SetActive(false);

            // 4 bluegill: the reveal hold, close to the camera, side on, the lake behind.
            fish.SetActive(true);
            Vector3 eye = new Vector3(0.3f, 1.85f, -44.5f);
            Vector3 look = new Vector3(-0.15f, 1.7f, -43.6f);
            PlaceFish(fish, eye + (look - eye).normalized * 0.55f, Quaternion.LookRotation(Vector3.Cross(look - eye, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, -8f));
            Shoot(camera, eye, look, 40f, outDir, "bluegill");
            fish.SetActive(false);

            // 5 dusk: the dock end against a low sun.
            Quaternion duskSun = Quaternion.Euler(9f, 200f, 0f);
            ApplyPhase(palette, DuskPhase, sun, duskSun);
            Vector3 toSun = -(duskSun * Vector3.forward);
            Vector3 duskEye = new Vector3(1.6f, 1.7f, -49.5f);
            Shoot(camera, duskEye, duskEye + new Vector3(toSun.x, -0.12f, toSun.z).normalized * 10f, wideFov, outDir, "dusk");

            Object.DestroyImmediate(cameraGo);
            Debug.Log("FG LOOKDEV captured " + Shots.Length + " shots tag=" + tag + " out=" + outDir);

            string sheet = Arg("-fgLookSheet");
            string before = Arg("-fgLookBefore");
            if (!string.IsNullOrEmpty(sheet) && !string.IsNullOrEmpty(before)) ComposeSheet(before, outDir, sheet);
        }

        const float LakeDockLayoutDeckTop = 0.6f;

        // R74 adoption shots (2026-10-13): the five fish side by side, the sweep viewpoints and the props. Same batch arguments as Capture
        // (-fgLookOut, -fgLookTag, -fgLookSheet + -fgLookBefore). Logs per-shot render stats (FG LOOKDEV STATS) for the spec 10.7 record.
        public static readonly string[] AdoptionShots = { "fish_lineup", "overview", "far_shore", "ramp", "ladders", "from_water", "bell", "reeds" };
        static readonly string[] FishFiles = { "Fish_Bluegill.fbx", "Fish_Perch.fbx", "Fish_Carp.fbx", "Fish_Trout.fbx", "Fish_Catfish.fbx" };

        public static void CaptureAdoption()
        {
            string outDir = Arg("-fgLookOut");
            if (string.IsNullOrEmpty(outDir)) throw new System.ArgumentException("-fgLookOut <dir> is required");
            string tag = Arg("-fgLookTag") ?? "shot";
            Directory.CreateDirectory(outDir);

            Scene scene = EditorSceneManager.OpenScene(AssetPaths.LakeDockScene, OpenSceneMode.Single);
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            var water = AssetDatabase.LoadAssetAtPath<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);
            File.WriteAllText(Path.Combine(outDir, "colliders_" + tag + ".txt"), DumpColliders(scene));
            PushPalette(palette);
            PushWaves(water);
            Light sun = FindSun();
            ApplyPhase(palette, DayPhase, sun, sun.transform.rotation);

            var cameraGo = new GameObject("LookDevCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = MovementMath.NearClip;
            camera.farClipPlane = MovementMath.FarClip;
            cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            Camera sceneCamera = GameObject.Find("SceneCamera")?.GetComponent<Camera>();
            if (sceneCamera != null) sceneCamera.enabled = false;
            float wideFov = Camera.HorizontalToVerticalFieldOfView(MovementMath.DefaultHorizontalFov, Width / (float)Height);

            // Fish lineup: all five species at 0.30 m, side on, in a row above the deck.
            var fishMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/M_Toon_Fish.mat");
            var fishes = new List<GameObject>();
            Vector3 lineEye = new Vector3(0f, 1.78f, -47.9f), lineAt = new Vector3(0f, 1.62f, -46.4f);
            Vector3 view = (lineAt - lineEye).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, view).normalized;
            for (int i = 0; i < FishFiles.Length; i++)
            {
                Mesh mesh = null;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetPaths.ModelsFolder + "/" + FishFiles[i]))
                    if (asset is Mesh m) { mesh = m; break; }
                var go = new GameObject("LookDevFish" + i);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int k = 0; k < mats.Length; k++) mats[k] = fishMaterial;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                float scale = 0.30f / Mathf.Max(1e-5f, mesh.bounds.size.z);
                go.transform.localScale = Vector3.one * scale;
                Quaternion rot = Quaternion.LookRotation(Vector3.Cross(view, Vector3.up).normalized, Vector3.up);
                go.transform.rotation = rot;
                Vector3 centre = lineAt + right * ((i - 2) * 0.36f) + Vector3.up * ((i % 2) * 0.07f - 0.03f);
                go.transform.position = centre - rot * (mesh.bounds.center * scale);
                fishes.Add(go);
            }
            Shoot(camera, lineEye, lineAt, 42f, outDir, "fish_lineup");
            foreach (GameObject go in fishes) Object.DestroyImmediate(go);

            Shoot(camera, new Vector3(16f, 9f, -24f), new Vector3(1f, 0f, -46f), wideFov, outDir, "overview");
            Shoot(camera, new Vector3(0.4f, 2.2f, -40.6f), new Vector3(0f, 0.6f, 40f), 22f, outDir, "far_shore");
            Shoot(camera, new Vector3(5.5f, 1.6f, -39.5f), new Vector3(12f, -0.2f, -48f), 55f, outDir, "ramp");
            Shoot(camera, new Vector3(3.6f, 0.45f, -38.6f), new Vector3(1.6f, -0.1f, -41.5f), 55f, outDir, "ladders");
            Shoot(camera, new Vector3(-5.5f, 0.4f, -39f), new Vector3(0f, 0.4f, -47f), 55f, outDir, "from_water");

            // Bell: the networked prefab is spawned at BellAnchor at runtime; place an instance there for the shot.
            GameObject anchor = GameObject.Find("BellAnchor");
            var bellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths.DockBellPrefab);
            var bell = (GameObject)PrefabUtility.InstantiatePrefab(bellPrefab);
            bell.transform.position = anchor != null ? anchor.transform.position : new Vector3(1.1f, 0.6f, -40.6f);
            Vector3 b = bell.transform.position;
            Shoot(camera, b + new Vector3(-1.4f, 1.2f, -1.7f), b + new Vector3(0f, 0.9f, 0f), 45f, outDir, "bell");
            Object.DestroyImmediate(bell);

            // Reeds: the first reed vertex marks a cluster; look at it from the shallows.
            GameObject reeds = GameObject.Find("Reeds");
            Vector3 reed = reeds != null ? reeds.GetComponent<MeshFilter>().sharedMesh.vertices[0] : new Vector3(-30f, 0f, 36f);
            Vector3 inward = new Vector3(-reed.x, 0f, -reed.z).normalized;
            Shoot(camera, new Vector3(reed.x, 0f, reed.z) + inward * 3.2f + Vector3.up * 1.1f, new Vector3(reed.x, 0.5f, reed.z), 50f, outDir, "reeds");

            Object.DestroyImmediate(cameraGo);
            Debug.Log("FG LOOKDEV adoption captured " + AdoptionShots.Length + " shots tag=" + tag);
            string sheet = Arg("-fgLookSheet");
            string before = Arg("-fgLookBefore");
            if (!string.IsNullOrEmpty(sheet) && !string.IsNullOrEmpty(before)) ComposeSheet(before, outDir, sheet, AdoptionShots);
        }

        // Batch entry (no graphics needed): only the collider dump of the scene on disk, colliders_<tag>.txt in -fgLookOut.
        public static void DumpCollidersBatch()
        {
            string outDir = Arg("-fgLookOut");
            if (string.IsNullOrEmpty(outDir)) throw new System.ArgumentException("-fgLookOut <dir> is required");
            string tag = Arg("-fgLookTag") ?? "dump";
            Directory.CreateDirectory(outDir);
            Scene scene = EditorSceneManager.OpenScene(AssetPaths.LakeDockScene, OpenSceneMode.Single);
            File.WriteAllText(Path.Combine(outDir, "colliders_" + tag + ".txt"), DumpColliders(scene));
            Debug.Log("FG LOOKDEV colliders tag=" + tag);
        }

        static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        // ---- globals (same values as PaletteGlobals / WaterTimeDriver at runtime) ----

        static void PushPalette(ArtPaletteDef palette)
        {
            Vector4[] authored = palette.PaletteVectors(palette.M2Season);
            var linear = new Vector4[ShaderGlobals.PaletteLength];
            for (int i = 0; i < linear.Length; i++)
            {
                Color c = new Color(authored[i].x, authored[i].y, authored[i].z, authored[i].w).linear;
                linear[i] = new Vector4(c.r, c.g, c.b, c.a);
            }
            Shader.SetGlobalVectorArray(ShaderGlobals.Palette, linear);
        }

        static void PushWaves(WaterSettingsDef water)
        {
            var waves = new Vector4[ShaderGlobals.WavesLength];
            for (int i = 0; i < 3 && i < water.DirAmpLenSpeed.Count; i++) waves[i] = water.DirAmpLenSpeed[i];
            Shader.SetGlobalVectorArray(ShaderGlobals.Waves, waves);
            Shader.SetGlobalFloat(ShaderGlobals.WaterTime, 12.5f);
            Shader.SetGlobalVectorArray(ShaderGlobals.Ripples, new Vector4[8]);
        }

        static void ApplyPhase(ArtPaletteDef palette, int index, Light sun, Quaternion sunRotation)
        {
            DayPhaseAmbient p = palette.Phases[index];
            sun.color = p.lightColor;
            sun.intensity = p.lightIntensity;
            sun.transform.rotation = sunRotation;
            Shader.SetGlobalColor(ShaderGlobals.ToonShade, p.toonShade);
            Shader.SetGlobalColor(ShaderGlobals.ToonAmbient, p.toonAmbient);
            Shader.SetGlobalColor(ShaderGlobals.SkyTop, p.skyTop);
            Shader.SetGlobalColor(ShaderGlobals.SkyHorizon, p.skyHorizon);
            Shader.SetGlobalColor(ShaderGlobals.SkyGround, p.skyGround);
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogDensity = p.fogDensity;
            RenderSettings.ambientLight = p.toonAmbient;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
            {
                if (volume.name == "WarmVolume") volume.weight = p.warmWeight;
                else if (volume.name == "CoolVolume") volume.weight = p.coolWeight;
            }
        }

        // ---- scene props ----

        static Light FindSun()
        {
            GameObject go = GameObject.Find("Sun");
            if (go == null || go.GetComponent<Light>() == null) throw new System.InvalidOperationException("no Sun light in the lake scene");
            return go.GetComponent<Light>();
        }

        static Transform FindRock()
        {
            GameObject shore = GameObject.Find("Shore");
            Transform best = null;
            float bestScore = float.MaxValue;
            foreach (Transform child in shore.transform)
            {
                if (!child.name.StartsWith("Rock_")) continue;
                // The rock with the most trees close behind it makes the richest shore shot.
                float score = 0f;
                foreach (Transform other in shore.transform)
                    if (other.name.StartsWith("Tree_")) score += Mathf.Min(0f, Vector3.Distance(other.position, child.position) - 18f);
                if (best == null || score < bestScore) { best = child; bestScore = score; }
            }
            if (best == null) throw new System.InvalidOperationException("no Rock_* under Shore");
            return best;
        }

        static GameObject SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths.PlayerPrefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (Camera c in go.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            return go;
        }

        static GameObject SpawnFish()
        {
            Mesh mesh = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetPaths.ModelsFolder + "/Fish_Bluegill.fbx"))
                if (asset is Mesh m) { mesh = m; break; }
            if (mesh == null) throw new System.InvalidOperationException("Fish_Bluegill.fbx has no mesh");
            var go = new GameObject("LookDevBluegill");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/M_Toon_Fish.mat");
            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;   // the reveal renderer casts and receives no shadows (CatchRevealView)
            renderer.receiveShadows = false;
            return go;
        }

        // Scales the model to a 0.22 m fish along its +Z bounds, as CatchRevealView does, and centres its bounds on `centre`.
        static void PlaceFish(GameObject fish, Vector3 centre, Quaternion rotation)
        {
            Mesh mesh = fish.GetComponent<MeshFilter>().sharedMesh;
            float scale = 0.22f / Mathf.Max(1e-5f, mesh.bounds.size.z);
            fish.transform.localScale = Vector3.one * scale;
            fish.transform.rotation = rotation;
            fish.transform.position = centre - rotation * (mesh.bounds.center * scale);
        }

        // ---- render ----

        // Spec 10.7 record: what this camera sees (frustum-culled renderers, their triangles and distinct materials) plus the editor's UnityStats
        // for the last render (batches, SetPass calls, draw calls, triangles; these read 0 when the editor does not collect stats in batchmode).
        static string s_statsLine = "";

        static void LogStats(Camera camera, string name)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            int renderers = 0; long triangles = 0;
            var materials = new HashSet<Material>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!r.enabled || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    renderers++;
                    for (int s = 0; s < f.sharedMesh.subMeshCount; s++) triangles += f.sharedMesh.GetIndexCount(s) / 3;
                    foreach (Material m in r.sharedMaterials) if (m != null) materials.Add(m);
                }
            Debug.Log("FG LOOKDEV STATS " + name + " visibleRenderers=" + renderers + " visibleTris=" + triangles + " materials=" + materials.Count +
                      " | recorder " + s_statsLine);
        }

        static void Shoot(Camera camera, Vector3 eye, Vector3 target, float verticalFov, string outDir, string name)
        {
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(target - eye, Vector3.up);
            camera.fieldOfView = verticalFov;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            using (var batches = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count"))
            using (var setPass = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count"))
            using (var draws = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count"))
            using (var tris = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count"))
            {
                camera.Render();   // second frame: shadow and depth history settled
                s_statsLine = "batches=" + batches.CurrentValue + " setPass=" + setPass.CurrentValue + " drawCalls=" + draws.CurrentValue + " tris=" + tris.CurrentValue;
            }
            LogStats(camera, name);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // Rows in Shots order; left column before, right column after; a 6 px dark gutter.
        public static void ComposeSheet(string beforeDir, string afterDir, string sheetPath) => ComposeSheet(beforeDir, afterDir, sheetPath, Shots);

        public static void ComposeSheet(string beforeDir, string afterDir, string sheetPath, string[] Shots)
        {
            const int gutter = 6;
            int w = Width * 2 + gutter, h = Height * Shots.Length + gutter * (Shots.Length - 1);
            var sheet = new Texture2D(w, h, TextureFormat.RGB24, false);
            var fill = new Color32[w * h];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(24, 24, 28, 255);
            sheet.SetPixels32(fill);
            for (int row = 0; row < Shots.Length; row++)
            {
                int y = h - (row + 1) * Height - row * gutter;   // texture rows go bottom-up
                Blit(sheet, Path.Combine(beforeDir, Shots[row] + ".png"), 0, y);
                Blit(sheet, Path.Combine(afterDir, Shots[row] + ".png"), Width + gutter, y);
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(sheetPath));
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("FG LOOKDEV sheet " + sheetPath);
        }

        static void Blit(Texture2D sheet, string path, int x, int y)
        {
            if (!File.Exists(path)) { Debug.LogWarning("FG LOOKDEV missing " + path); return; }
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(path));
            int tw = Mathf.Min(tex.width, Width), th = Mathf.Min(tex.height, Height);
            sheet.SetPixels(x, y, tw, th, tex.GetPixels(0, 0, tw, th));
            Object.DestroyImmediate(tex);
        }

        // ---- physics fingerprint ----

        // One line per collider in hierarchy order: path, type, layer, trigger, enabled, world transform and shape; MeshColliders add the mesh asset,
        // vertex/index counts and an FNV-1a hash of the vertex and index data.
        public static string DumpColliders(Scene scene)
        {
            var sb = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    Transform t = c.transform;
                    sb.Append(HierPath(t)).Append(" | ").Append(c.GetType().Name).Append(" | layer ").Append(c.gameObject.layer)
                      .Append(" | trig ").Append(c.isTrigger).Append(" | en ").Append(c.enabled)
                      .Append(" | pos ").Append(F(t.position)).Append(" rot ").Append(F(t.rotation.eulerAngles)).Append(" scl ").Append(F(t.lossyScale));
                    switch (c)
                    {
                        case BoxCollider b: sb.Append(" | box ").Append(F(b.center)).Append(' ').Append(F(b.size)); break;
                        case SphereCollider s: sb.Append(" | sphere ").Append(F(s.center)).Append(' ').Append(s.radius.ToString("R")); break;
                        case CapsuleCollider k: sb.Append(" | capsule ").Append(F(k.center)).Append(' ').Append(k.radius.ToString("R")).Append(' ').Append(k.height.ToString("R")).Append(' ').Append(k.direction); break;
                        case MeshCollider m:
                            Mesh mesh = m.sharedMesh;
                            sb.Append(" | mesh ").Append(mesh == null ? "null" : AssetDatabase.GetAssetPath(mesh) + " v" + mesh.vertexCount + " i" + mesh.triangles.Length + " h" + Hash(mesh).ToString("x16"))
                              .Append(" convex ").Append(m.convex);
                            break;
                    }
                    sb.Append('\n');
                }
            return sb.ToString();
        }

        static ulong Hash(Mesh mesh)
        {
            ulong h = 14695981039346656037UL;
            void Add(int v) { unchecked { for (int k = 0; k < 4; k++) { h ^= (byte)(v >> (8 * k)); h *= 1099511628211UL; } } }
            foreach (Vector3 v in mesh.vertices) { Add(System.BitConverter.SingleToInt32Bits(v.x)); Add(System.BitConverter.SingleToInt32Bits(v.y)); Add(System.BitConverter.SingleToInt32Bits(v.z)); }
            foreach (int i in mesh.triangles) Add(i);
            return h;
        }

        static string F(Vector3 v) => v.x.ToString("R") + "," + v.y.ToString("R") + "," + v.z.ToString("R");

        static string HierPath(Transform t)
        {
            string p = t.name;
            for (Transform u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
            return p;
        }
    }
}
