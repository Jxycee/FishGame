// Fish icon baker (T21 icon upgrade, 2026-10-13). Authority: none (editor-only dev tool; writes Content/Textures/T_FishIcons.png and the tile table
// on ArtPalette.asset). Renders every species' real R74 model on M_Toon_Fish with its skin layer, one 256 x 128 tile each, into a 5-column atlas.
// Needs a graphics device, so it cannot run inside Tools/unity.ps1 build (-nographics). Run it after the species models or colours change:
//   Unity.exe -batchmode -projectPath <proj> -quit -logFile <log> -executeMethod FishGame.EditorTools.FishIconBaker.Bake      (NO -nographics)
// or the menu item FishGame > Bake Fish Icons. Then commit T_FishIcons.png(.meta) and ArtPalette.asset.
// Deterministic: an empty scene, a fixed orthographic camera (fit to each model's projected vertices), one directional light at a fixed angle,
// the Day phase globals; the same inputs give the same PNG on the same GPU. Transparency comes from difference matting (the model is rendered over
// black and over white, 2x supersampled), so it does not depend on the shader writing alpha.
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FishGame.EditorTools
{
    public static class FishIconBaker
    {
        public const int TileWidth = 256;
        public const int TileHeight = 128;
        public const int Columns = 5;
        private const int Supersample = 2;
        private const int DayPhase = 1;
        private const float Fill = 0.93f;              // the projected model fills this fraction of the tile (the larger of width and height)
        private const float YawDeg = 28f;              // the camera orbits toward the head by this much: a three-quarter view
        private const float ElevationDeg = 16f;        // and looks down by this much
        private static readonly Quaternion SunRotation = Quaternion.Euler(48f, 24f, 0f);   // from above and behind the camera, lighting the visible side

        [MenuItem("FishGame/Bake Fish Icons")]
        public static void BakeFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Bake();
        }

        public static void Bake()
        {
            // The empty scene first: opening it unloads unused assets, so everything is loaded after it.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Registry order is the ordinal sort of the species ids (RegistryIndex), so scanning the species assets and sorting by id gives the same
            // order without loading the registry (a batch -executeMethod can run before the registry's references resolve).
            var species = new List<FishSpeciesDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { AssetPaths.SpeciesFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) species.Add(def);
            }
            species.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (species.Count == 0) throw new System.InvalidOperationException("no species assets under " + AssetPaths.SpeciesFolder + " (run Tools/unity.ps1 build first)");
            var palette = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            if (palette == null || palette.Phases.Count <= DayPhase) throw new System.InvalidOperationException("ArtPalette asset missing or without a Day phase");
            var fishMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetPaths.MaterialsFolder + "/M_Toon_Fish.mat");
            if (fishMaterial == null) throw new System.InvalidOperationException("M_Toon_Fish.mat missing");

            int count = species.Count;
            int rows = (count + Columns - 1) / Columns;
            int atlasW = Columns * TileWidth, atlasH = rows * TileHeight;
            var atlas = new Color32[atlasW * atlasH];                      // starts fully transparent (0, 0, 0, 0)
            var tiles = new List<FishIconTile>();

            var sunGo = new GameObject("IconSun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            LookDevCapture.PushPalette(palette);
            LookDevCapture.ApplyPhase(palette, DayPhase, sun, SunRotation);

            var cameraGo = new GameObject("IconCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.allowMSAA = true;
            var urpCamera = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            urpCamera.renderPostProcessing = false;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    FishSpeciesDef def = species[i];
                    Mesh mesh = def != null ? def.Model : null;
                    if (mesh == null)
                    {
                        Debug.Log("FG ICONBAKE " + (def != null ? def.Id : "#" + i) + " has no model; the journal keeps the silhouette fallback for it");
                        continue;
                    }

                    int col = i % Columns, row = i / Columns;
                    Color32[] tile = RenderTile(camera, mesh, fishMaterial);
                    int x0 = col * TileWidth, y0 = (rows - 1 - row) * TileHeight;       // row 0 is the top of the picture (texture y runs up)
                    for (int y = 0; y < TileHeight; y++)
                        System.Array.Copy(tile, y * TileWidth, atlas, (y0 + y) * atlasW + x0, TileWidth);
                    tiles.Add(new FishIconTile { id = def.Id, x = x0, y = y0, w = TileWidth, h = TileHeight });
                    Debug.Log("FG ICONBAKE " + def.Id + " tile " + col + "," + row + " tris=" + TriangleCount(mesh));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(sunGo);
            }

            var texture = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(atlas);
            string disk = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPaths.FishIconAtlas));
            Directory.CreateDirectory(Path.GetDirectoryName(disk));
            File.WriteAllBytes(disk, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AssetPaths.FishIconAtlas, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPaths.FishIconAtlas);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPaths.FishIconAtlas);
            palette.ConfigureFishIcons(atlasTexture, tiles.ToArray());
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            Debug.Log("FG ICONBAKE done " + tiles.Count + " of " + count + " species, atlas " + atlasW + "x" + atlasH + " " + AssetPaths.FishIconAtlas);
        }

        // One tile: the model side on (head to the left), the camera orbited toward the head and raised, fitted by the projected vertices.
        private static Color32[] RenderTile(Camera camera, Mesh mesh, Material material)
        {
            var go = new GameObject("IconFish");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int k = 0; k < mats.Length; k++) mats[k] = material;
                renderer.sharedMaterials = mats;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // The model's forward (+Z) points to -X: a camera looking along +Z sees the fish side on with its head to the left.
                go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
                go.transform.position = Vector3.zero;

                Vector3 centre = go.transform.TransformPoint(mesh.bounds.center);
                float reach = Mathf.Max(0.01f, mesh.bounds.extents.magnitude);
                float yaw = YawDeg * Mathf.Deg2Rad, el = ElevationDeg * Mathf.Deg2Rad;
                Vector3 toCamera = new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(el), Mathf.Sin(el), -Mathf.Cos(yaw) * Mathf.Cos(el));
                Quaternion look = Quaternion.LookRotation(-toCamera, Vector3.up);
                Vector3 right = look * Vector3.right, up = look * Vector3.up;

                // The projected extent of the real vertices on the view plane.
                Vector3[] verts = mesh.vertices;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 d = go.transform.TransformPoint(verts[v]) - centre;
                    float px = Vector3.Dot(d, right), py = Vector3.Dot(d, up);
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;
                }
                float aspect = TileWidth / (float)TileHeight;
                float halfH = Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / aspect) / Fill;
                Vector3 viewCentre = centre + right * ((minX + maxX) * 0.5f) + up * ((minY + maxY) * 0.5f);

                camera.transform.SetPositionAndRotation(viewCentre + toCamera * (reach * 3f + 1f), look);
                camera.orthographicSize = Mathf.Max(1e-4f, halfH);

                int w = TileWidth * Supersample, h = TileHeight * Supersample;
                Color32[] onBlack = RenderPass(camera, Color.black, w, h);
                Color32[] onWhite = RenderPass(camera, Color.white, w, h);
                return Matte(onBlack, onWhite, w, h);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Color32[] RenderPass(Camera camera, Color background, int w, int h)
        {
            camera.backgroundColor = background;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();           // a second frame, as the look-dev captures do: depth and shadow history settled
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Color32[] pixels = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            return pixels;
        }

        // Difference matte: over black the pixel is c * a, over white it is c * a + (1 - a), so a = 1 - (white - black). The supersampled result is
        // averaged premultiplied (Supersample x Supersample), then unpremultiplied; transparent pixels carry the model's mean colour so bilinear
        // filtering never fringes dark.
        private static Color32[] Matte(Color32[] onBlack, Color32[] onWhite, int w, int h)
        {
            int outW = w / Supersample, outH = h / Supersample;
            var result = new Color32[outW * outH];
            float sumR = 0f, sumG = 0f, sumB = 0f, sumA = 0f;
            var straightA = new float[outW * outH];
            var premul = new Vector3[outW * outH];
            float inv = 1f / (Supersample * Supersample);
            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    float pr = 0f, pg = 0f, pb = 0f, a = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            int i = (y * Supersample + sy) * w + (x * Supersample + sx);
                            Color32 b = onBlack[i], wh = onWhite[i];
                            float alpha = 1f - ((wh.r - b.r) + (wh.g - b.g) + (wh.b - b.b)) / (3f * 255f);
                            alpha = Mathf.Clamp01(alpha);
                            pr += b.r / 255f;
                            pg += b.g / 255f;
                            pb += b.b / 255f;
                            a += alpha;
                        }
                    }
                    int o = y * outW + x;
                    premul[o] = new Vector3(pr * inv, pg * inv, pb * inv);
                    straightA[o] = a * inv;
                    sumR += premul[o].x; sumG += premul[o].y; sumB += premul[o].z; sumA += straightA[o];
                }
            }
            Color32 fill = sumA > 0f ? new Color32(ToByte(sumR / sumA), ToByte(sumG / sumA), ToByte(sumB / sumA), 0) : new Color32(0, 0, 0, 0);
            for (int o = 0; o < result.Length; o++)
            {
                float a = straightA[o];
                if (a < 1f / 255f) { result[o] = fill; continue; }
                Vector3 c = premul[o] / a;
                result[o] = new Color32(ToByte(c.x), ToByte(c.y), ToByte(c.z), ToByte(a));
            }
            return result;
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static long TriangleCount(Mesh mesh)
        {
            long tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetIndexCount(s) / 3;
            return tris;
        }
    }
}
