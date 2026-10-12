using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FishGame.Tests.EditMode
{
    public class MeshBuilderTests
    {
        readonly List<Mesh> _meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            foreach (Mesh m in _meshes)
                if (m != null) Object.DestroyImmediate(m);
            _meshes.Clear();
        }

        Mesh Track(Mesh m)
        {
            _meshes.Add(m);
            return m;
        }

        static List<Vector2> Uv1(Mesh m)
        {
            var list = new List<Vector2>();
            m.GetUVs(1, list);
            return list;
        }

        static int Bits(float f) { return BitConverter.SingleToInt32Bits(f); }

        // every primitive with non-trivial rotations, positions and slots
        static MeshBuilder BuildSample(uint seed)
        {
            var b = new MeshBuilder(seed, 0.04f);
            b.Triangle(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0), 1);
            b.Quad(new Vector3(2, 0, 0), new Vector3(2, 1, 0), new Vector3(3, 1, 0), new Vector3(3, 0, 0), 2);
            b.Box(new Vector3(1, 2, 3), new Vector3(1, 2, 0.5f), Quaternion.Euler(15f, 33f, 71f), 3);
            b.Prism(new Vector3(-2, 0.5f, 1), 0.6f, 1.4f, 7, Quaternion.Euler(0f, 20f, 5f), 4);
            b.Cylinder(new Vector3(4, 1, -2), 0.5f, 0.2f, 2f, 9, Quaternion.Euler(80f, 0f, 12f), 5);
            b.Cylinder(new Vector3(4, 1, 2), 0.5f, 0f, 2f, 5, Quaternion.identity, 6);
            b.Capsule(new Vector3(-4, 1, -2), 0.4f, 2f, 8, 3, Quaternion.Euler(30f, 60f, 90f), 7);
            b.Icosphere(new Vector3(0, -3, 0), 0.9f, 2, 8);
            return b;
        }

        [Test]
        public void MeshBuilder_FlatPrimitives_HaveThreeVerticesPerTriangle()
        {
            var cases = new List<(string name, MeshBuilder b, int tris)>();
            var tri = new MeshBuilder(1);
            tri.Triangle(Vector3.zero, Vector3.up, Vector3.right, 0);
            cases.Add(("Triangle", tri, 1));
            var quad = new MeshBuilder(1);
            quad.Quad(Vector3.zero, Vector3.up, new Vector3(1, 1, 0), Vector3.right, 0);
            cases.Add(("Quad", quad, 2));
            var box = new MeshBuilder(1);
            box.Box(Vector3.zero, Vector3.one, Quaternion.identity, 0);
            cases.Add(("Box", box, 12));
            var prism = new MeshBuilder(1);
            prism.Prism(Vector3.zero, 0.5f, 1f, 6, Quaternion.identity, 0);
            cases.Add(("Prism6", prism, 20));
            var cone = new MeshBuilder(1);
            cone.Cylinder(Vector3.zero, 0.5f, 0f, 1f, 8, Quaternion.identity, 0);
            cases.Add(("Cone8", cone, 14));
            var cap = new MeshBuilder(1);
            cap.Capsule(Vector3.zero, 0.4f, 2f, 8, 3, Quaternion.identity, 0);
            cases.Add(("Capsule", cap, 96));
            var sphereCap = new MeshBuilder(1); // height == 2 * radius: no body section
            sphereCap.Capsule(Vector3.zero, 0.5f, 1f, 6, 2, Quaternion.identity, 0);
            cases.Add(("CapsuleNoBody", sphereCap, 36));
            var cyl = new MeshBuilder(1);
            cyl.Cylinder(Vector3.zero, 0.5f, 0.25f, 1f, 8, Quaternion.identity, 0);
            cases.Add(("Frustum8", cyl, 28));
            var ico = new MeshBuilder(1);
            ico.Icosphere(Vector3.zero, 1f, 1, 0);
            cases.Add(("Icosphere1", ico, 80));

            foreach (var c in cases)
            {
                Assert.AreEqual(c.tris, c.b.TriangleCount, c.name + " TriangleCount");
                Mesh m = Track(c.b.ToMesh(c.name));
                Assert.AreEqual(3 * c.tris, m.vertexCount, c.name + " vertexCount");
                int[] idx = m.triangles;
                Assert.AreEqual(3 * c.tris, idx.Length, c.name + " index count");
                for (int i = 0; i < idx.Length; i++)
                    Assert.AreEqual(i, idx[i], c.name + " indices are 0,1,2,... (no welding)");
            }

            foreach (int sub in new[] { 0, 2 })
            {
                var b = new MeshBuilder(5);
                var centre = new Vector3(1, 2, 3);
                b.Icosphere(centre, 0.7f, sub, 0);
                Assert.AreEqual(20 * (int)Math.Pow(4, sub), b.TriangleCount);
                Mesh m = Track(b.ToMesh("ico" + sub));
                foreach (Vector3 v in m.vertices)
                    Assert.AreEqual(0.7f, (v - centre).magnitude, 1e-5f, "icosphere subdivisions " + sub);
            }
        }

        [Test]
        public void MeshBuilder_FaceVertices_ShareNormalSlotAndColour()
        {
            var b = new MeshBuilder(7, 0.04f);
            Vector3 boxCentre = Vector3.zero;
            Vector3 icoCentre = new Vector3(5, 0, 0);
            b.Box(boxCentre, new Vector3(2f, 1f, 3f), Quaternion.Euler(10f, 20f, 30f), 3);
            b.Icosphere(icoCentre, 1f, 1, 7);
            Mesh m = Track(b.ToMesh("faces"));

            Vector3[] v = m.vertices;
            Vector3[] n = m.normals;
            Color[] c = m.colors;
            List<Vector2> uv1 = Uv1(m);
            int[] idx = m.triangles;
            Assert.AreEqual(0, m.uv.Length, "UV0 is not written");
            Assert.AreEqual(v.Length, n.Length);
            Assert.AreEqual(v.Length, c.Length);
            Assert.AreEqual(v.Length, uv1.Count);

            int tris = idx.Length / 3;
            Assert.AreEqual(12 + 80, tris);
            bool anyAboveHalf = false;
            var distinctGrey = new HashSet<int>();
            for (int t = 0; t < tris; t++)
            {
                int i0 = idx[3 * t], i1 = idx[3 * t + 1], i2 = idx[3 * t + 2];
                Assert.AreEqual(n[i0], n[i1], "tri " + t + " normal");
                Assert.AreEqual(n[i0], n[i2], "tri " + t + " normal");
                Assert.AreEqual(uv1[i0], uv1[i1], "tri " + t + " uv1");
                Assert.AreEqual(uv1[i0], uv1[i2], "tri " + t + " uv1");
                Assert.AreEqual(c[i0], c[i1], "tri " + t + " colour");
                Assert.AreEqual(c[i0], c[i2], "tri " + t + " colour");

                Vector3 expected = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]).normalized;
                Assert.Less((n[i0] - expected).magnitude, 1e-5f, "tri " + t + " normal is the geometric face normal");

                Vector3 centroid = (v[i0] + v[i1] + v[i2]) / 3f;
                Vector3 shapeCentre = t < 12 ? boxCentre : icoCentre;
                Assert.Greater(Vector3.Dot(n[i0], centroid - shapeCentre), 0f, "tri " + t + " normal points outward");

                float slot = t < 12 ? 3f : 7f;
                Assert.AreEqual(slot, uv1[i0].x, 0f, "tri " + t + " uv1.x is the slot");
                Assert.AreEqual(0f, uv1[i0].y, 0f);

                Assert.AreEqual(c[i0].r, c[i0].g, 0f);
                Assert.AreEqual(c[i0].r, c[i0].b, 0f);
                Assert.AreEqual(1f, c[i0].a, 0f);
                // BF-7: half-scale grey 0.5 * (1 +/- 0.04), plus UNorm8 quantisation of 1/255.
                Assert.GreaterOrEqual(c[i0].r, 0.48f - 0.006f, "tri " + t + " grey");
                Assert.LessOrEqual(c[i0].r, 0.52f + 0.006f, "tri " + t + " grey");
                if (c[i0].r > 0.5f) anyAboveHalf = true;
                distinctGrey.Add(Mathf.RoundToInt(c[i0].r * 255f));
            }
            for (int side = 0; side < 6; side++)
                Assert.AreEqual(c[idx[3 * (2 * side)]], c[idx[3 * (2 * side + 1)]], "box side " + side + " triangles share one colour");
            Assert.IsTrue(anyAboveHalf, "at least one face > 0.5: the + jitter half survives UNorm8 storage");
            Assert.Greater(distinctGrey.Count, 1, "jitter varies per face");
        }

        [Test]
        public void MeshBuilder_Rebuild_IsByteIdentical()
        {
            Mesh a = Track(BuildSample(42).ToMesh("a"));
            Mesh b = Track(BuildSample(42).ToMesh("b"));
            AssertIdentical(a, b, "seed 42 rebuild");

            // a second ToMesh from the same builder gives the same bytes too
            MeshBuilder reuse = BuildSample(42);
            AssertIdentical(Track(reuse.ToMesh("r1")), Track(reuse.ToMesh("r2")), "ToMesh twice");

            Mesh d = Track(BuildSample(43).ToMesh("d"));
            Vector3[] va = a.vertices, vd = d.vertices;
            Assert.AreEqual(va.Length, vd.Length);
            for (int i = 0; i < va.Length; i++) Assert.AreEqual(va[i], vd[i], "seed does not move positions");
            Color[] ca = a.colors, cd = d.colors;
            bool colourDiffers = false;
            for (int i = 0; i < ca.Length; i++) if (ca[i] != cd[i]) { colourDiffers = true; break; }
            Assert.IsTrue(colourDiffers, "a different seed changes at least one colour");

            AssertIdentical(Track(MeshBuilder.Grid(8, 6, 1.5f, "g1")), Track(MeshBuilder.Grid(8, 6, 1.5f, "g2")), "grid");
        }

        static void AssertIdentical(Mesh a, Mesh b, string what)
        {
            Vector3[] va = a.vertices, vb = b.vertices;
            Assert.AreEqual(va.Length, vb.Length, what + " vertex count");
            for (int i = 0; i < va.Length; i++)
            {
                Assert.AreEqual(Bits(va[i].x), Bits(vb[i].x), what + " vertex " + i);
                Assert.AreEqual(Bits(va[i].y), Bits(vb[i].y), what + " vertex " + i);
                Assert.AreEqual(Bits(va[i].z), Bits(vb[i].z), what + " vertex " + i);
            }
            Vector3[] na = a.normals, nb = b.normals;
            Assert.AreEqual(na.Length, nb.Length, what + " normal count");
            for (int i = 0; i < na.Length; i++)
            {
                Assert.AreEqual(Bits(na[i].x), Bits(nb[i].x), what + " normal " + i);
                Assert.AreEqual(Bits(na[i].y), Bits(nb[i].y), what + " normal " + i);
                Assert.AreEqual(Bits(na[i].z), Bits(nb[i].z), what + " normal " + i);
            }
            Color[] ca = a.colors, cb = b.colors;
            Assert.AreEqual(ca.Length, cb.Length, what + " colour count");
            for (int i = 0; i < ca.Length; i++)
            {
                Assert.AreEqual(Bits(ca[i].r), Bits(cb[i].r), what + " colour " + i);
                Assert.AreEqual(Bits(ca[i].g), Bits(cb[i].g), what + " colour " + i);
                Assert.AreEqual(Bits(ca[i].b), Bits(cb[i].b), what + " colour " + i);
                Assert.AreEqual(Bits(ca[i].a), Bits(cb[i].a), what + " colour " + i);
            }
            List<Vector2> ua = Uv1(a), ub = Uv1(b);
            Assert.AreEqual(ua.Count, ub.Count, what + " uv1 count");
            for (int i = 0; i < ua.Count; i++)
            {
                Assert.AreEqual(Bits(ua[i].x), Bits(ub[i].x), what + " uv1 " + i);
                Assert.AreEqual(Bits(ua[i].y), Bits(ub[i].y), what + " uv1 " + i);
            }
            Assert.AreEqual(a.triangles, b.triangles, what + " indices");
            Assert.AreEqual(a.indexFormat, b.indexFormat, what + " indexFormat");
            Assert.AreEqual(a.bounds, b.bounds, what + " bounds");
        }

        [Test]
        public void MeshBuilder_Grid_SharesVertices()
        {
            Mesh g = Track(MeshBuilder.Grid(4, 3, 2f, "grid"));
            Assert.AreEqual(20, g.vertexCount);
            int[] idx = g.triangles;
            Assert.AreEqual(72, idx.Length);
            Vector3[] v = g.vertices;
            Vector3[] n = g.normals;
            Assert.AreEqual(0, g.colors.Length, "water is not Toon: no vertex colours");
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.AreEqual(0f, v[i].y, 0f);
                Assert.AreEqual(new Vector3(0, 1, 0), n[i]);
                minX = Mathf.Min(minX, v[i].x); maxX = Mathf.Max(maxX, v[i].x);
                minZ = Mathf.Min(minZ, v[i].z); maxZ = Mathf.Max(maxZ, v[i].z);
            }
            Assert.AreEqual(-4f, minX, 1e-6f);
            Assert.AreEqual(4f, maxX, 1e-6f);
            Assert.AreEqual(-3f, minZ, 1e-6f);
            Assert.AreEqual(3f, maxZ, 1e-6f);

            var used = new bool[v.Length];
            foreach (int i in idx) used[i] = true;
            for (int i = 0; i < used.Length; i++) Assert.IsTrue(used[i], "vertex " + i + " is referenced");

            for (int t = 0; t < idx.Length / 3; t++)
            {
                Vector3 geo = Vector3.Cross(v[idx[3 * t + 1]] - v[idx[3 * t]], v[idx[3 * t + 2]] - v[idx[3 * t]]);
                Assert.Greater(geo.y, 0f, "triangle " + t + " faces +Y");
            }

            Mesh big = Track(MeshBuilder.Grid(96, 96, 1.25f, "big"));
            Assert.AreEqual(9409, big.vertexCount);
            Assert.AreEqual(IndexFormat.UInt16, big.indexFormat);
            Assert.AreEqual(new Vector3(120f, 2f, 120f), big.bounds.size);
            Assert.AreEqual(Vector3.zero, big.bounds.center);
        }

        // extra
        [Test]
        public void FaceJitter_Grey_DeterministicAndWithinJitter()
        {
            Assert.AreEqual(FaceJitter.Grey(9u, 17, 0.04f), FaceJitter.Grey(9u, 17, 0.04f), 0f);
            double sum = 0;
            bool otherSeedDiffers = false;
            for (int f = 0; f < 1000; f++)
            {
                float g = FaceJitter.Grey(9u, f, 0.04f);
                Assert.GreaterOrEqual(g, 0.96f - 1e-6f);
                Assert.LessOrEqual(g, 1.04f + 1e-6f);
                sum += g;
                if (FaceJitter.Grey(10u, f, 0.04f) != g) otherSeedDiffers = true;
            }
            Assert.AreEqual(1.0, sum / 1000.0, 0.005);
            Assert.IsTrue(otherSeedDiffers, "a different seed changes at least one value");
            Assert.AreEqual(1f, FaceJitter.Grey(9u, 3, 0f), 0f);
            Assert.AreEqual(1f, FaceJitter.Grey(9u, 3, -0.1f), 0f);
            Assert.AreEqual(1f, FaceJitter.Grey(9u, 3, float.NaN), 0f);
        }

        // extra: argument validation fails before any state change; empty ToMesh throws; big meshes switch to UInt32.
        [Test]
        public void MeshBuilder_InvalidArguments_ThrowAndLeaveBuilderUnchanged()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshBuilder(1, -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshBuilder(1, 0.51f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshBuilder(1, float.NaN));
            Assert.DoesNotThrow(() => new MeshBuilder(1, 0f));
            Assert.DoesNotThrow(() => new MeshBuilder(1, 0.5f));

            var b = new MeshBuilder(1);
            Assert.Throws<InvalidOperationException>(() => b.ToMesh("empty"));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Box(Vector3.zero, Vector3.one, Quaternion.identity, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Triangle(Vector3.zero, Vector3.up, Vector3.right, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Box(Vector3.zero, new Vector3(1, 0, 1), Quaternion.identity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Prism(Vector3.zero, 0.5f, 1f, 2, Quaternion.identity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Cylinder(Vector3.zero, 0f, 0f, 1f, 6, Quaternion.identity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Capsule(Vector3.zero, 0.4f, 2f, 8, 0, Quaternion.identity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Icosphere(Vector3.zero, 1f, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Icosphere(Vector3.zero, 1f, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Icosphere(Vector3.zero, float.NaN, 1, 0));
            Assert.AreEqual(0, b.TriangleCount, "rejected calls append nothing");
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshBuilder.Grid(0, 3, 1f, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshBuilder.Grid(3, 3, 0f, "x"));

            var many = new MeshBuilder(2);
            for (int i = 0; i < 5; i++) many.Icosphere(new Vector3(i * 3f, 0, 0), 1f, 4, 0);
            Mesh m = Track(many.ToMesh("many"));
            Assert.AreEqual(5 * 20 * 256 * 3, m.vertexCount);
            Assert.AreEqual(IndexFormat.UInt32, m.indexFormat);
        }
    }
}
