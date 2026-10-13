// Runtime/World/MeshBuilder.cs: deterministic flat-shaded primitive meshes (spec 10.2, C 7.12). Authority: none (pure).
// Rules:
//  - Every primitive appends UNSHARED vertices, 3 per triangle in emission order; indices are 0,1,2,... (no welding).
//  - Each triangle (a, b, c) is clockwise seen from outside (Unity front face); its normal is normalize(Cross(b - a, c - a)).
//  - A face is one Triangle, one Quad (a,b,c + a,c,d) or one polygon cap; all its triangles share slot and grey.
//  - Slot 0..15 goes to UV channel 1 as (slot, 0) (TEXCOORD1). UV0 is not written.
//  - Colour (BF-7): stored grey is HALF-scale, 0.5 * FaceJitter.Grey, so it fits UNorm8 without clamping; the Toon
//    material sets _JitterEncoded 1 and doubles it. Nothing is rewritten after ToMesh.
//  - Determinism: arrays/lists in emission order only, no hash containers, no time, no Random, no Recalculate*/Optimize.
//    Angles are computed per index (2*pi*k/sides), never accumulated. Same calls + same seed = identical bytes.
using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Runtime
{
    public sealed class MeshBuilder
    {
        private readonly uint _seed;
        private readonly float _jitter;
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uv1 = new List<Vector2>();
        private readonly List<int> _indices = new List<int>();
        private int _faceIndex = -1;
        private Color _faceColor;
        private Vector2 _faceUv;

        public MeshBuilder(uint meshSeed, float faceJitter = 0.04f)
        {
            if (!(faceJitter >= 0f && faceJitter <= 0.5f)) throw new ArgumentOutOfRangeException("faceJitter", "must be in 0..0.5");
            _seed = meshSeed;
            _jitter = faceJitter;
        }

        public int TriangleCount { get { return _indices.Count / 3; } }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, byte slot)
        {
            CheckSlot(slot);
            BeginFace(slot);
            AddTri(a, b, c);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, byte slot)
        {
            CheckSlot(slot);
            BeginFace(slot);
            AddTri(a, b, c);
            AddTri(a, c, d);
        }

        // Faces in order +X, -X, +Y, -Y, +Z, -Z.
        public void Box(Vector3 center, Vector3 size, Quaternion rotation, byte slot)
        {
            CheckSlot(slot);
            if (!(size.x > 0f && size.y > 0f && size.z > 0f)) throw new ArgumentOutOfRangeException("size", "every component must be > 0");
            Vector3 h = size * 0.5f;
            for (int face = 0; face < 6; face++)
            {
                int axis = face / 2;
                bool positive = (face & 1) == 0;
                Vector3 c0 = AxisVector(axis, h, positive ? 1f : -1f);
                Vector3 u = AxisVector((axis + 1) % 3, h, 1f);
                Vector3 w = AxisVector((axis + 2) % 3, h, 1f);
                if (!positive) { Vector3 tmp = u; u = w; w = tmp; } // u x w must point along the face normal
                BeginFace(slot);
                Vector3 p0 = Place(center, rotation, c0 - u - w), p1 = Place(center, rotation, c0 + u - w);
                Vector3 p2 = Place(center, rotation, c0 + u + w), p3 = Place(center, rotation, c0 - u + w);
                AddTri(p0, p1, p2);
                AddTri(p0, p2, p3);
            }
        }

        public void Prism(Vector3 center, float radius, float height, int sides, Quaternion rotation, byte slot)
        {
            Cylinder(center, radius, radius, height, sides, rotation, slot);
        }

        // Side faces (quads, or triangles for a cone), then the top cap, then the bottom cap (one polygon face each).
        public void Cylinder(Vector3 center, float radiusBottom, float radiusTop, float height, int sides, Quaternion rotation, byte slot)
        {
            CheckSlot(slot);
            if (!(radiusBottom >= 0f) || !(radiusTop >= 0f) || !(radiusBottom > 0f || radiusTop > 0f))
                throw new ArgumentOutOfRangeException("radius", "radii must be >= 0 and not both 0");
            if (!(height > 0f)) throw new ArgumentOutOfRangeException("height", "must be > 0");
            if (sides < 3) throw new ArgumentOutOfRangeException("sides", "must be >= 3");
            float hh = height * 0.5f;
            for (int i = 0; i < sides; i++)
                BandFace(center, rotation, radiusBottom, -hh, radiusTop, hh, i, sides, slot);
            if (radiusTop > 0f) CapFace(center, rotation, radiusTop, hh, sides, slot, true);
            if (radiusBottom > 0f) CapFace(center, rotation, radiusBottom, -hh, sides, slot, false);
        }

        // height is the total height (CapsuleCollider convention). Top hemisphere, body, bottom hemisphere.
        public void Capsule(Vector3 center, float radius, float height, int sides, int rings, Quaternion rotation, byte slot)
        {
            CheckSlot(slot);
            if (!(radius > 0f)) throw new ArgumentOutOfRangeException("radius", "must be > 0");
            if (!(height > 0f)) throw new ArgumentOutOfRangeException("height", "must be > 0");
            if (sides < 3) throw new ArgumentOutOfRangeException("sides", "must be >= 3");
            if (rings < 1) throw new ArgumentOutOfRangeException("rings", "must be >= 1");
            float body = Math.Max(0f, height * 0.5f - radius);
            for (int b = 0; b < rings; b++)
            {
                Latitude(b, rings, radius, out float rLow, out float sLow);
                Latitude(b + 1, rings, radius, out float rHigh, out float sHigh);
                for (int i = 0; i < sides; i++)
                    BandFace(center, rotation, rLow, body + sLow, rHigh, body + sHigh, i, sides, slot);
            }
            if (body > 0f)
                for (int i = 0; i < sides; i++)
                    BandFace(center, rotation, radius, -body, radius, body, i, sides, slot);
            for (int b = 0; b < rings; b++)
            {
                Latitude(b, rings, radius, out float rHigh, out float sHigh);
                Latitude(b + 1, rings, radius, out float rLow, out float sLow);
                for (int i = 0; i < sides; i++)
                    BandFace(center, rotation, rLow, -body - sLow, rHigh, -body - sHigh, i, sides, slot);
            }
        }

        public void Icosphere(Vector3 center, float radius, int subdivisions, byte slot)
        {
            CheckSlot(slot);
            if (!(radius > 0f)) throw new ArgumentOutOfRangeException("radius", "must be > 0");
            if (subdivisions < 0 || subdivisions > 4) throw new ArgumentOutOfRangeException("subdivisions", "must be 0..4");
            for (int f = 0; f < 20; f++)
                Subdivide(center, radius, IcoVerts[IcoFaces[3 * f]], IcoVerts[IcoFaces[3 * f + 1]], IcoVerts[IcoFaces[3 * f + 2]], subdivisions, slot);
        }

        public Mesh ToMesh(string name)
        {
            if (TriangleCount == 0) throw new InvalidOperationException("MeshBuilder has no triangles");
            var mesh = new Mesh { name = name };
            mesh.indexFormat = _verts.Count <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32; // before the triangles
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetUVs(1, _uv1);
            mesh.SetTriangles(_indices, 0, true);
            return mesh;
        }

        // Shared vertices, centred on the origin, y 0, normals +Y, UV0 0..1, no colours (water is not Toon).
        public static Mesh Grid(int cellsX, int cellsZ, float cellSize, string name)
        {
            if (cellsX < 1) throw new ArgumentOutOfRangeException("cellsX", "must be >= 1");
            if (cellsZ < 1) throw new ArgumentOutOfRangeException("cellsZ", "must be >= 1");
            if (!(cellSize > 0f)) throw new ArgumentOutOfRangeException("cellSize", "must be > 0");
            int vx = cellsX + 1, vz = cellsZ + 1;
            var verts = new Vector3[vx * vz];
            var normals = new Vector3[vx * vz];
            var uv0 = new Vector2[vx * vz];
            for (int j = 0; j < vz; j++)
                for (int i = 0; i < vx; i++)
                {
                    int k = j * vx + i;
                    verts[k] = new Vector3(-cellsX * cellSize * 0.5f + i * cellSize, 0f, -cellsZ * cellSize * 0.5f + j * cellSize);
                    normals[k] = new Vector3(0f, 1f, 0f);
                    uv0[k] = new Vector2((float)i / cellsX, (float)j / cellsZ);
                }
            var tris = new int[cellsX * cellsZ * 6];
            int t = 0;
            for (int j = 0; j < cellsZ; j++)
                for (int i = 0; i < cellsX; i++)
                {
                    int p00 = j * vx + i, p10 = p00 + 1, p01 = p00 + vx, p11 = p01 + 1;
                    tris[t++] = p00; tris[t++] = p01; tris[t++] = p11; // geometric normal +Y
                    tris[t++] = p00; tris[t++] = p11; tris[t++] = p10;
                }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Length <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uv0;
            mesh.SetTriangles(tris, 0, true);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(cellsX * cellSize, 2f, cellsZ * cellSize)); // +-1 m covers shader displacement
            return mesh;
        }

        // ---- internals ----

        private static void CheckSlot(byte slot)
        {
            if (slot > 15) throw new ArgumentOutOfRangeException("slot", "must be 0..15");
        }

        private void BeginFace(byte slot)
        {
            _faceIndex++;
            float g = 0.5f * FaceJitter.Grey(_seed, _faceIndex, _jitter);
            _faceColor = new Color(g, g, g, 1f);
            _faceUv = new Vector2(slot, 0f);
        }

        private void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            for (int k = 0; k < 3; k++)
            {
                _normals.Add(n);
                _colors.Add(_faceColor);
                _uv1.Add(_faceUv);
                _indices.Add(i + k);
            }
        }

        private static Vector3 Place(Vector3 center, Quaternion rotation, Vector3 local)
        {
            return center + rotation * local;
        }

        private static Vector3 AxisVector(int axis, Vector3 h, float sign)
        {
            var v = Vector3.zero;
            v[axis] = h[axis] * sign;
            return v;
        }

        // Ring vertex k of n at local (radius * sin(theta), y, radius * cos(theta)); k wraps so the seam reuses vertex 0 exactly.
        private static Vector3 RingPoint(Vector3 center, Quaternion rotation, float radius, float y, int k, int n)
        {
            double theta = 2.0 * Math.PI * (k % n) / n;
            return Place(center, rotation, new Vector3(radius * (float)Math.Sin(theta), y, radius * (float)Math.Cos(theta)));
        }

        // Face i of a band between a low ring and a high ring. A zero radius means an apex, so the face is one triangle.
        private void BandFace(Vector3 center, Quaternion rotation, float rLow, float yLow, float rHigh, float yHigh, int i, int n, byte slot)
        {
            Vector3 l0 = RingPoint(center, rotation, rLow, yLow, i, n), l1 = RingPoint(center, rotation, rLow, yLow, i + 1, n);
            Vector3 h0 = RingPoint(center, rotation, rHigh, yHigh, i, n), h1 = RingPoint(center, rotation, rHigh, yHigh, i + 1, n);
            BeginFace(slot);
            if (rLow <= 0f) AddTri(l0, h1, h0);
            else if (rHigh <= 0f) AddTri(l0, l1, h0);
            else { AddTri(l0, l1, h1); AddTri(l0, h1, h0); }
        }

        // One polygon face fan-triangulated from ring vertex 0 (n - 2 triangles).
        private void CapFace(Vector3 center, Quaternion rotation, float radius, float y, int n, byte slot, bool top)
        {
            Vector3 v0 = RingPoint(center, rotation, radius, y, 0, n);
            BeginFace(slot);
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 a = RingPoint(center, rotation, radius, y, i, n), b = RingPoint(center, rotation, radius, y, i + 1, n);
                if (top) AddTri(v0, a, b); else AddTri(v0, b, a);
            }
        }

        // Latitude b of rings on a hemisphere: phi = (pi/2) * b / rings; the pole is exact.
        private static void Latitude(int b, int rings, float radius, out float ringRadius, out float rise)
        {
            if (b >= rings) { ringRadius = 0f; rise = radius; return; }
            double phi = Math.PI * 0.5 * b / rings;
            ringRadius = radius * (float)Math.Cos(phi);
            rise = radius * (float)Math.Sin(phi);
        }

        private void Subdivide(Vector3 center, float radius, Vector3 a, Vector3 b, Vector3 c, int depth, byte slot)
        {
            if (depth == 0)
            {
                BeginFace(slot);
                AddTri(center + a * radius, center + b * radius, center + c * radius);
                return;
            }
            Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
            Subdivide(center, radius, a, ab, ca, depth - 1, slot);
            Subdivide(center, radius, b, bc, ab, depth - 1, slot);
            Subdivide(center, radius, c, ca, bc, depth - 1, slot);
            Subdivide(center, radius, ab, bc, ca, depth - 1, slot);
        }

        // Icosahedron: 12 vertices normalized to unit length (raw (+-1, +-t, 0) has length ~1.9), 20 faces outward by Cross.
        private static readonly Vector3[] IcoVerts = MakeIcoVerts();
        private static readonly int[] IcoFaces =
        {
            0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
            1, 5, 9,  5, 11, 4,  11, 10, 2,  10, 7, 6,  7, 1, 8,
            3, 9, 4,  3, 4, 2,  3, 2, 6,  3, 6, 8,  3, 8, 9,
            4, 9, 5,  2, 4, 11,  6, 2, 10,  8, 6, 7,  9, 8, 1
        };

        private static Vector3[] MakeIcoVerts()
        {
            float t = (1f + (float)Math.Sqrt(5.0)) * 0.5f;
            var raw = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < raw.Length; i++) raw[i] = raw[i].normalized;
            return raw;
        }
    }
}
