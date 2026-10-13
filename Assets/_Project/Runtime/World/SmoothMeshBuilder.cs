// Runtime/World/SmoothMeshBuilder.cs: deterministic SMOOTH meshes for the R74 realistic-toon look (look-dev 2026-10-12). Authority: none (pure).
// The flat-shaded MeshBuilder stays as it is (its rules and tests are unchanged); this builder is its smooth sibling:
//  - Shared vertices inside a patch; every normal is ANALYTIC (rounded box: direction from the inner box; lathe: from the profile slope; radial
//    surface: finite differences of the surface function), so patch seams shade continuously and no Recalculate* is ever called.
//  - UV0 is in METRES along the surface (the material's _FGTiling sets repeats per metre). UV1 is a Vector4 (slot, layer, mapping, 0): x = palette
//    slot (FishGame/Toon palette mode), y = texture-array layer, z = 0 for uv0 mapping or 1 for world triplanar.
//  - Colour (BF-7): HALF-scale grey 0.5 * ao, so materials keep _JitterEncoded 1. ao is baked per vertex from simple rules (darker toward the local
//    bottom of a box, a lathe's own profile curve, the lower half of a radial surface): the look's ambient occlusion and its subtle value gradient.
//  - Determinism: lists in emission order, angles computed per index, no time, no Random, no hash containers. Same calls = identical bytes.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Runtime
{
    public sealed class SmoothMeshBuilder
    {
        public const byte MappingUv = 0, MappingTriplanar = 1;

        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector4> _uv1 = new List<Vector4>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _indices = new List<int>();

        public int TriangleCount { get { return _indices.Count / 3; } }
        public int VertexCount { get { return _verts.Count; } }

        private int Vertex(Vector3 p, Vector3 n, Vector2 uv, byte slot, byte layer, byte mapping, float ao)
        {
            _verts.Add(p);
            _normals.Add(n);
            _uv0.Add(uv);
            _uv1.Add(new Vector4(slot, layer, mapping, 0f));
            float g = 0.5f * Mathf.Clamp01(ao);
            _colors.Add(new Color(g, g, g, 1f));
            return _verts.Count - 1;
        }

        // A (cols x rows) vertex grid starting at index `start`, row-major; quads wound clockwise seen from outside when `flip` is false.
        private void GridQuads(int start, int cols, int rows, bool flip)
        {
            for (int r = 0; r + 1 < rows; r++)
                for (int c = 0; c + 1 < cols; c++)
                {
                    int a = start + r * cols + c, b = a + 1, d = a + cols, e = d + 1;
                    if (!flip) { _indices.Add(a); _indices.Add(d); _indices.Add(e); _indices.Add(a); _indices.Add(e); _indices.Add(b); }
                    else { _indices.Add(a); _indices.Add(e); _indices.Add(d); _indices.Add(a); _indices.Add(b); _indices.Add(e); }
                }
        }

        // ---- rounded box ----

        // Box of `size` with edges and corners rounded by `radius` (clamped to 0.95 of the smallest half extent). bandSteps 1 gives each 90 degree
        // edge two segments (enough with smooth normals for planks); 2 gives four (close-up objects). UV0 runs along the box's LONGEST axis as u
        // (wood grain follows the plank), metres. aoBottom darkens toward the local -Y face (0 = none).
        public void RoundedBox(Vector3 center, Vector3 size, float radius, Quaternion rotation, byte slot, byte layer, float aoBottom = 0.25f, int bandSteps = 1)
        {
            Vector3 h = size * 0.5f;
            float r = Mathf.Min(radius, 0.95f * Mathf.Min(h.x, Mathf.Min(h.y, h.z)));
            int longest = h.x >= h.y && h.x >= h.z ? 0 : (h.y >= h.z ? 1 : 2);
            Vector3 inner = new Vector3(h.x - r, h.y - r, h.z - r);

            for (int axis = 0; axis < 3; axis++)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    // Face axes a, b: the long axis first when it lies in this face (grain along u).
                    int a = (axis + 1) % 3, b = (axis + 2) % 3;
                    if (b == longest) { int t = a; a = b; b = t; }
                    float[] sa = Samples(h[a], r, bandSteps), sb = Samples(h[b], r, bandSteps);
                    int start = _verts.Count;
                    for (int j = 0; j < sb.Length; j++)
                        for (int i = 0; i < sa.Length; i++)
                        {
                            Vector3 q = Vector3.zero;
                            q[axis] = sign * h[axis]; q[a] = sa[i]; q[b] = sb[j];
                            Vector3 c = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                            Vector3 d = (q - c).normalized;
                            Vector3 local = c + d * r;
                            float up = h.y > 1e-6f ? (local.y / h.y) * 0.5f + 0.5f : 1f;
                            float ao = 1f - aoBottom * (1f - up) * (1f - up);
                            Vector2 uv = new Vector2(q[a], q[b]);
                            Vertex(center + rotation * local, rotation * d, uv, slot, layer, MappingUv, ao);
                        }
                    // Winding: (a, b, axis) right-handed with sign +1 needs the flip that makes Cross(da, db) point along +axis.
                    Vector3 ea = Vector3.zero, eb = Vector3.zero, en = Vector3.zero;
                    ea[a] = 1f; eb[b] = 1f; en[axis] = sign;
                    bool outward = Vector3.Dot(Vector3.Cross(eb, ea), en) > 0f;   // GridQuads(!flip) emits (a, d, e): clockwise when Cross(b-dir, a-dir) is outward
                    GridQuads(start, sa.Length, sb.Length, !outward);
                }
        }

        // -h, [-h + r/2], -h + r, h - r, [h - r/2], h  (the middle segment is dropped when the half extent equals r).
        private static float[] Samples(float h, float r, int bandSteps)
        {
            var list = new List<float> { -h };
            if (bandSteps >= 2) list.Add(-h + r * 0.5f);
            list.Add(-h + r);
            if (h - r > -h + r + 1e-5f) list.Add(h - r);
            if (bandSteps >= 2) list.Add(h - r * 0.5f);
            list.Add(h);
            return list.ToArray();
        }

        // ---- lathe ----

        // A surface of revolution about local +Y from y = -height/2 (t = 0) to +height/2 (t = 1). radius(t) is the profile; ao(t) the per-ring ao.
        // The ring seam repeats vertex 0 (u wraps in UV). Caps: a closed end is made by a profile radius of 0 at t = 0 or 1 (no separate cap).
        // UV0: u = angle * uRadius (metres around), v = profile arc length (metres up); swapUv exchanges them. Normal: (h cos, -dr/dt, h sin) normalised (analytic in theta,
        // central difference in t).
        public void Lathe(Vector3 center, Func<float, float> radius, float height, int sides, int rings, Quaternion rotation, byte slot, byte layer,
            byte mapping, Func<float, float> ao, float uRadius, bool swapUv = false)
        {
            int start = _verts.Count;
            float v = 0f;
            Vector2 previous = new Vector2(radius(0f), -height * 0.5f);
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float rt = Mathf.Max(0f, radius(t));
                float y = -height * 0.5f + t * height;
                Vector2 profile = new Vector2(rt, y);
                v += (profile - previous).magnitude;
                previous = profile;
                const float e = 1e-3f;
                float dr = (radius(Mathf.Min(1f, t + e)) - radius(Mathf.Max(0f, t - e))) / ((Mathf.Min(1f, t + e) - Mathf.Max(0f, t - e)) * height);
                float a0 = ao(t);
                for (int k = 0; k <= sides; k++)
                {
                    float angle = (k % sides) * (Mathf.PI * 2f / sides);
                    float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
                    Vector3 n = new Vector3(s, -dr, c).normalized;
                    Vector3 local = new Vector3(rt * s, y, rt * c);
                    Vector2 uv = new Vector2(k * (Mathf.PI * 2f / sides) * uRadius, v);
                    if (swapUv) uv = new Vector2(uv.y, uv.x);   // wood grain (u in the array) running up the post
                    Vertex(center + rotation * local, rotation * n, uv, slot, layer, mapping, a0);
                }
            }
            GridQuads(start, sides + 1, rings + 1, true);
        }

        // ---- radial surface (cube sphere) ----

        // A closed surface p(d) = d * radius(d) over unit directions d, sampled on a cube sphere (6 faces of res x res quads, no poles). Normals by
        // finite differences of p, so they agree across the cube seams. ao(d) per vertex; UV0 = the cube-face coordinate times uvScale (metres-ish;
        // pair it with triplanar mapping when seams matter).
        // faceLayer (optional) overrides the layer per cube face, given the face's outward axis (for example a face decal on +Z only); uvOffset is
        // added to every UV (0.5 with uvScale 0.5 puts each cube face exactly on one texture tile).
        public void Radial(Vector3 center, Func<Vector3, float> radius, int res, Quaternion rotation, Vector3 scale, byte slot, byte layer, byte mapping,
            Func<Vector3, float> ao, float uvScale, Func<Vector3, byte> faceLayer = null, Vector2 uvOffset = default)
        {
            for (int axis = 0; axis < 3; axis++)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    int a = (axis + 1) % 3, b = (axis + 2) % 3;
                    int start = _verts.Count;
                    Vector3 faceAxis = Vector3.zero;
                    faceAxis[axis] = sign;
                    byte faceLayerValue = faceLayer != null ? faceLayer(faceAxis) : layer;
                    for (int j = 0; j <= res; j++)
                        for (int i = 0; i <= res; i++)
                        {
                            Vector3 q = Vector3.zero;
                            q[axis] = sign; q[a] = -1f + 2f * i / res; q[b] = -1f + 2f * j / res;
                            Vector3 d = q.normalized;
                            Vector3 p = Surface(d, radius, scale);
                            Vector3 t1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                            Vector3 t2 = Vector3.Cross(d, t1);
                            const float e = 0.01f;
                            Vector3 p1 = Surface((d + t1 * e).normalized, radius, scale) - Surface((d - t1 * e).normalized, radius, scale);
                            Vector3 p2 = Surface((d + t2 * e).normalized, radius, scale) - Surface((d - t2 * e).normalized, radius, scale);
                            Vector3 n = Vector3.Cross(p1, p2).normalized;
                            if (Vector3.Dot(n, p) < 0f) n = -n;
                            Vector2 uv = new Vector2(q[a], q[b]) * uvScale + uvOffset;
                            Vertex(center + rotation * p, rotation * n, uv, slot, faceLayerValue, mapping, ao(d));
                        }
                    Vector3 ea = Vector3.zero, eb = Vector3.zero, en = Vector3.zero;
                    ea[a] = 1f; eb[b] = 1f; en[axis] = sign;
                    bool outward = Vector3.Dot(Vector3.Cross(eb, ea), en) > 0f;
                    GridQuads(start, res + 1, res + 1, !outward);
                }
        }

        private static Vector3 Surface(Vector3 d, Func<Vector3, float> radius, Vector3 scale)
        {
            return Vector3.Scale(d * radius(d), scale);
        }

        // ---- output ----

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = _verts.Count <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uv0);
            mesh.SetUVs(1, _uv1);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0, true);
            return mesh;
        }

        // ---- deterministic smooth noise for shape functions (value noise on a lattice, smoothstep blended) ----

        public static float Noise3(Vector3 p, uint seed)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float fx = p.x - xi, fy = p.y - yi, fz = p.z - zi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float Lerp(float a, float b, float t) => a + (b - a) * t;
            float x00 = Lerp(Hash(xi, yi, zi, seed), Hash(xi + 1, yi, zi, seed), fx);
            float x10 = Lerp(Hash(xi, yi + 1, zi, seed), Hash(xi + 1, yi + 1, zi, seed), fx);
            float x01 = Lerp(Hash(xi, yi, zi + 1, seed), Hash(xi + 1, yi, zi + 1, seed), fx);
            float x11 = Lerp(Hash(xi, yi + 1, zi + 1, seed), Hash(xi + 1, yi + 1, zi + 1, seed), fx);
            return Lerp(Lerp(x00, x10, fy), Lerp(x01, x11, fy), fz) * 2f - 1f;
        }

        private static float Hash(int x, int y, int z, uint seed)
        {
            unchecked
            {
                uint h = seed ^ ((uint)x * 0x8DA6B343u) ^ ((uint)y * 0xD8163841u) ^ ((uint)z * 0xCB1AB31Fu);
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
