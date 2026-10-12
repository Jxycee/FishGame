// Authority: none (pure presentation helper; builds meshes on whichever peer asks).
// MeshBuilder fish and junk shapes for catches that have no species model (every species until T29, and junk always), plus the grey snap
// silhouette. Nose +Z, centred, bounds.size.z == 1 so a caller scales uniformly to a length in metres.
// Colour rule (F-Q3 / C 2.8 #19): vertex colours are never converted, so the Toon shader reads them as LINEAR. MeshBuilder stores a HALF-scale
// grey 0.5 * (1 +/- jitter) in the colour channel; this class multiplies colorA.linear / colorB.linear into that grey (at most 0.75 per
// channel, UNorm8-safe, no clamp). These meshes must be drawn with a material whose _JitterEncoded is 1 (M_Toon_FishFallback), where the
// shader doubles the vertex colour.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Presentation
{
    public static class FishFallbackMesh
    {
        private const byte SlotBody = 0, SlotFin = 1;
        private const uint SpeciesSeedBase = 0x46000000u, JunkSeedBase = 0x4A000000u, SilhouetteSeed = 0x46534C48u;
        private static readonly Color SilhouetteTint = new Color(0.35f, 0.37f, 0.40f, 1f);

        private static readonly Dictionary<(bool, ushort), Mesh> Cache = new Dictionary<(bool, ushort), Mesh>();
        private static Mesh s_silhouette;

        // Cached per (isJunk, itemIndex). A bad index or a missing registry yields a plain Round fish (never null, never an error).
        public static Mesh Get(ContentRegistry registry, bool isJunk, ushort itemIndex)
        {
            var key = (isJunk, itemIndex);
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            BodyShape shape = BodyShape.Round;
            Color colorA = new Color(0.5f, 0.5f, 0.5f, 1f), colorB = colorA;
            if (registry != null)
            {
                if (isJunk && itemIndex < registry.Junk.Count)
                {
                    JunkDef junk = registry.JunkAt(itemIndex);
                    if (junk != null)
                    {
                        shape = junk.BodyShape;
                        colorA = junk.Color;
                        colorB = junk.Color;
                    }
                }
                else if (!isJunk && itemIndex < registry.Species.Count)
                {
                    FishSpeciesDef species = registry.SpeciesAt(itemIndex);
                    if (species != null)
                    {
                        shape = species.BodyShape;
                        colorA = species.ColorA;
                        colorB = species.ColorB;
                    }
                }
            }

            uint seed = (isJunk ? JunkSeedBase : SpeciesSeedBase) + itemIndex;
            Mesh mesh = isJunk ? BuildJunk(shape, seed, colorA) : BuildFish(shape, seed, colorA, colorB, false);
            mesh.name = (isJunk ? "FishFallback_Junk" : "FishFallback_Fish") + itemIndex;
            Cache[key] = mesh;
            return mesh;
        }

        // A cached Round fish for the snap jump; vertex colour = tint * the half-scale grey, so the doubling shader gives the tint times jitter.
        internal static Mesh Silhouette()
        {
            if (s_silhouette != null) return s_silhouette;
            s_silhouette = BuildFish(BodyShape.Round, SilhouetteSeed, SilhouetteTint, SilhouetteTint, true);
            s_silhouette.name = "FishFallback_Silhouette";
            return s_silhouette;
        }

        private static Mesh BuildFish(BodyShape shape, uint seed, Color colorA, Color colorB, bool rawTint)
        {
            var b = new MeshBuilder(seed, 0.04f);
            int squashVertices = 0;
            Vector3 squash = Vector3.one;
            Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

            switch (shape)
            {
                case BodyShape.Long:
                    b.Capsule(Vector3.zero, 0.12f, 0.9f, 8, 2, alongZ, SlotBody);
                    b.Prism(new Vector3(0f, 0f, -0.55f), 0.2f, 0.05f, 3, alongX, SlotFin);
                    b.Prism(new Vector3(0f, 0.12f, -0.05f), 0.1f, 0.04f, 3, alongX, SlotFin);
                    break;
                case BodyShape.Flat:
                    b.Box(Vector3.zero, new Vector3(0.12f, 0.6f, 0.8f), Quaternion.identity, SlotBody);
                    b.Prism(new Vector3(0f, 0f, -0.55f), 0.25f, 0.05f, 3, alongX, SlotFin);
                    break;
                case BodyShape.Eel:
                    b.Capsule(Vector3.zero, 0.04f, 1.0f, 6, 2, alongZ, SlotBody);
                    b.Prism(new Vector3(0f, 0f, -0.5f), 0.06f, 0.03f, 3, alongX, SlotFin);
                    break;
                case BodyShape.Squid:
                    b.Cylinder(new Vector3(0f, 0f, -0.2f), 0.05f, 0.14f, 0.6f, 8, alongZ, SlotBody);   // pointed tail end at -Z, wide head end at +Z
                    for (int i = 0; i < 6; i++)
                    {
                        double a = 2.0 * System.Math.PI * i / 6.0;
                        var at = new Vector3(0.07f * (float)System.Math.Sin(a), 0.07f * (float)System.Math.Cos(a), 0.3f);
                        b.Prism(at, 0.022f, 0.4f, 4, alongZ, SlotFin);
                    }
                    break;
                default:   // Round
                    b.Icosphere(Vector3.zero, 0.5f, 1, SlotBody);
                    squashVertices = b.TriangleCount * 3;
                    squash = new Vector3(0.35f, 0.8f, 1f);
                    b.Prism(new Vector3(0f, 0f, -0.62f), 0.28f, 0.06f, 3, alongX, SlotFin);
                    b.Prism(new Vector3(0f, 0.42f, -0.02f), 0.22f, 0.05f, 3, alongX, SlotFin);
                    break;
            }

            Mesh mesh = b.ToMesh("FishFallback");
            Finish(mesh, squashVertices, squash, colorA, colorB, rawTint, false);
            return mesh;
        }

        // Junk by body shape: Long = old boot (two boxes), anything else = tin can (cylinder + top ring).
        private static Mesh BuildJunk(BodyShape shape, uint seed, Color color)
        {
            var b = new MeshBuilder(seed, 0.04f);
            if (shape == BodyShape.Long)
            {
                b.Box(new Vector3(0f, -0.1f, 0.1f), new Vector3(0.28f, 0.2f, 0.8f), Quaternion.identity, SlotBody);   // foot
                b.Box(new Vector3(0f, 0.15f, -0.2f), new Vector3(0.26f, 0.5f, 0.28f), Quaternion.identity, SlotBody);   // shaft
            }
            else
            {
                Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
                b.Cylinder(Vector3.zero, 0.3f, 0.3f, 0.9f, 10, alongZ, SlotBody);
                b.Cylinder(new Vector3(0f, 0f, 0.45f), 0.33f, 0.33f, 0.08f, 10, alongZ, SlotBody);   // top ring
            }

            Mesh mesh = b.ToMesh("FishFallbackJunk");
            Finish(mesh, 0, Vector3.one, color, color, false, true);
            return mesh;
        }

        // 1. squash the first `squashVertices` vertices (the body), 2. centre and normalise bounds.size.z to 1, 3. flat normals again
        // (the squash is non-uniform), 4. colour pass.
        private static void Finish(Mesh mesh, int squashVertices, Vector3 squash, Color colorA, Color colorB, bool rawTint, bool singleSlot)
        {
            var verts = new List<Vector3>();
            var uv1 = new List<Vector2>();
            var colors = new List<Color>();
            mesh.GetVertices(verts);
            mesh.GetUVs(1, uv1);
            mesh.GetColors(colors);

            for (int i = 0; i < squashVertices && i < verts.Count; i++) verts[i] = Vector3.Scale(verts[i], squash);

            Vector3 min = verts[0], max = verts[0];
            for (int i = 1; i < verts.Count; i++)
            {
                min = Vector3.Min(min, verts[i]);
                max = Vector3.Max(max, verts[i]);
            }
            Vector3 center = (min + max) * 0.5f;
            float length = max.z - min.z;
            float k = length > 1e-5f ? 1f / length : 1f;
            for (int i = 0; i < verts.Count; i++) verts[i] = (verts[i] - center) * k;

            var normals = new List<Vector3>(verts.Count);
            for (int i = 0; i < verts.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(verts[i + 1] - verts[i], verts[i + 2] - verts[i]).normalized;
                normals.Add(n);
                normals.Add(n);
                normals.Add(n);
            }

            Color a = rawTint ? colorA : colorA.linear;
            Color bb = rawTint ? colorB : colorB.linear;
            for (int i = 0; i < colors.Count; i++)
            {
                float grey = colors[i].r;   // half-scale grey from MeshBuilder
                Color tint = !singleSlot && i < uv1.Count && uv1[i].x >= 0.5f ? bb : a;
                colors[i] = new Color(tint.r * grey, tint.g * grey, tint.b * grey, 1f);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
        }

        // Enter Play Mode options are on in this project, so statics survive play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (Mesh m in Cache.Values) DestroyMesh(m);
            Cache.Clear();
            DestroyMesh(s_silhouette);
            s_silhouette = null;
        }

        private static void DestroyMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Object.Destroy(m);
            else Object.DestroyImmediate(m);
        }
    }
}
