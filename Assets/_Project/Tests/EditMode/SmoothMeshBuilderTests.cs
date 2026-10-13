// R74 look-dev (2026-10-12): SmoothMeshBuilder (smooth props, body meshes) and the painted texture arrays. Each test names the wrong build it catches.
using System.Collections.Generic;
using FishGame.EditorTools;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Tests.EditMode
{
    public class SmoothMeshBuilderTests
    {
        static Mesh Sample()
        {
            var b = new SmoothMeshBuilder();
            b.RoundedBox(new Vector3(0.2f, 0.1f, -0.3f), new Vector3(3f, 0.1f, 0.26f), 0.02f, Quaternion.Euler(0f, 0.7f, 0f), 3, EnvTextureLayers.Wood);
            b.Lathe(Vector3.zero, t => 0.3f * (1f - t), 1f, 12, 6, Quaternion.identity, 1, EnvTextureLayers.Foliage, SmoothMeshBuilder.MappingTriplanar, t => 0.8f, 0.3f);
            b.Radial(Vector3.up, d => 0.45f + 0.03f * SmoothMeshBuilder.Noise3(d * 2f, 7u), 4, Quaternion.identity, Vector3.one, 6, EnvTextureLayers.Rock,
                SmoothMeshBuilder.MappingTriplanar, d => 0.7f, 0.5f);
            return b.ToMesh("Sample");
        }

        // Catches: a non-deterministic build (time, Random, hash order) that would rewrite the mesh assets on every build.
        [Test]
        public void Build_IsByteIdentical()
        {
            Mesh a = Sample(), c = Sample();
            Assert.That(c.vertices, Is.EqualTo(a.vertices));
            Assert.That(c.normals, Is.EqualTo(a.normals));
            Assert.That(c.triangles, Is.EqualTo(a.triangles));
            Assert.That(c.colors, Is.EqualTo(a.colors));
            var u0a = new List<Vector2>(); var u0c = new List<Vector2>();
            a.GetUVs(0, u0a); c.GetUVs(0, u0c);
            Assert.That(u0c, Is.EqualTo(u0a), "uv0");
            var ua = new List<Vector4>(); var uc = new List<Vector4>();
            a.GetUVs(1, ua); c.GetUVs(1, uc);
            Assert.That(uc, Is.EqualTo(ua), "uv1");
        }

        // Catches: flat (unshared) vertices sneaking back in, zero or unnormalised normals, a missing vertex colour or uv channel (FishGame/Toon and
        // RenderValidators need the colour), a colour above the BF-7 half scale.
        [Test]
        public void Mesh_IsSmoothShared_WithUnitNormals_HalfScaleColours_AndLayerChannel()
        {
            Mesh m = Sample();
            Assert.That(m.vertexCount, Is.LessThan(m.triangles.Length), "shared vertices (a flat mesh has 3 per triangle)");
            Assert.That(m.HasVertexAttribute(VertexAttribute.Color), Is.True);
            Assert.That(m.HasVertexAttribute(VertexAttribute.TexCoord0), Is.True);
            Assert.That(m.GetVertexAttributeDimension(VertexAttribute.TexCoord1), Is.EqualTo(4), "uv1 = (slot, layer, mapping, 0)");
            foreach (Vector3 n in m.normals) Assert.That(n.magnitude, Is.EqualTo(1f).Within(1e-3f));
            foreach (Color c in m.colors) Assert.That(c.r, Is.InRange(0f, 0.5f));
            var uv1 = new List<Vector4>();
            m.GetUVs(1, uv1);
            var layers = new HashSet<float>();
            foreach (Vector4 v in uv1) layers.Add(v.y);
            Assert.That(layers, Is.EquivalentTo(new float[] { EnvTextureLayers.Wood, EnvTextureLayers.Foliage, EnvTextureLayers.Rock }));
        }

        // Catches: the zero-normal bug. Radial's finite-difference cross product scales with radius^2, so below r ~ 0.158 m its length falls under
        // Vector3.normalized's 1e-5 threshold and the normal came out (0,0,0), rendering the part black (the bobber stem ball r 0.008, the bell
        // crown knob r 0.07, the first blossom specks). Every normal must be unit length and point away from the centre, at any radius.
        [Test]
        public void Radial_SmallRadius_NormalsAreUnitAndOutward([Values(0.008f, 0.03f, 0.07f, 0.15f, 0.5f)] float radius)
        {
            Vector3 centre = new Vector3(0.1f, 0.2f, -0.3f);
            var b = new SmoothMeshBuilder();
            b.Radial(centre, d => radius, 3, Quaternion.identity, Vector3.one, 0, 0, 0, d => 1f, 0.5f);
            Mesh m = b.ToMesh("Knob");
            Vector3[] v = m.vertices, n = m.normals;
            Assert.That(n.Length, Is.GreaterThan(0));
            for (int i = 0; i < n.Length; i++)
            {
                Assert.That(n[i].magnitude, Is.EqualTo(1f).Within(1e-3f), "r " + radius + ": unit normal at vertex " + i);
                Assert.That(Vector3.Dot(n[i], (v[i] - centre).normalized), Is.GreaterThan(0.99f), "r " + radius + ": a sphere's normal points away from its centre, vertex " + i);
            }
        }

        // Catches: a saved mesh asset built before the zero-normal fix (the Bobber and Bell_Bell knobs) or any later mesh builder whose normals
        // collapse; reads the saved assets because those are what the prefabs render.
        [Test]
        public void SavedMeshes_BobberAndBell_HaveUnitNormals()
        {
            foreach (string path in new[] { AssetPaths.MeshesFolder + "/Bobber.asset", AssetPaths.MeshesFolder + "/Bell_Bell.asset" })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(mesh, "missing " + path);
                Vector3[] n = mesh.normals;
                Assert.That(n.Length, Is.EqualTo(mesh.vertexCount), path + ": normals on every vertex");
                for (int i = 0; i < n.Length; i++)
                    Assert.That(n[i].magnitude, Is.EqualTo(1f).Within(1e-3f), path + ": unit normal at vertex " + i + " (a zero normal renders black)");
            }
        }

        // Catches: inward-facing triangles (culled from outside): every triangle of a closed radial surface faces away from its centre.
        [Test]
        public void Radial_TrianglesFaceOutward()
        {
            var b = new SmoothMeshBuilder();
            b.Radial(Vector3.zero, d => 0.5f, 4, Quaternion.identity, Vector3.one, 0, 0, 0, d => 1f, 0.5f);
            Mesh m = b.ToMesh("Ball");
            Vector3[] v = m.vertices; int[] t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], c1 = v[t[i + 1]], c2 = v[t[i + 2]];
                Vector3 n = Vector3.Cross(c1 - a, c2 - a);
                Assert.That(Vector3.Dot(n, (a + c1 + c2) / 3f), Is.GreaterThan(0f), "triangle " + i / 3);
            }
        }

        // Every non-degenerate triangle's winding normal must agree with the average of its analytic vertex normals (the lit side is the culled-in side).
        static void AssertWindingMatchesNormals(Mesh m, string what)
        {
            Vector3[] v = m.vertices, nrm = m.normals; int[] t = m.triangles;
            int checkedTriangles = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 geo = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (geo.sqrMagnitude < 1e-12f) continue;   // a collapsed triangle at a closed pole
                Vector3 avg = nrm[t[i]] + nrm[t[i + 1]] + nrm[t[i + 2]];
                Assert.That(Vector3.Dot(geo, avg), Is.GreaterThan(0f), what + ": triangle " + i / 3 + " is wound against its normals");
                checkedTriangles++;
            }
            Assert.That(checkedTriangles, Is.GreaterThan(0), what + ": no triangles checked");
        }

        // Catches: a lathe wound inside out (its sides vanish from outside under Cull Back). The wall's winding must agree with its analytic normals
        // AND point away from the axis.
        [Test]
        public void Lathe_TrianglesFaceOutward()
        {
            var b = new SmoothMeshBuilder();
            b.Lathe(Vector3.zero, t => 0.3f * (1f - 0.5f * t), 1f, 12, 4, Quaternion.identity, 0, 0, SmoothMeshBuilder.MappingUv, t => 1f, 0.3f);
            Mesh m = b.ToMesh("Pole");
            AssertWindingMatchesNormals(m, "lathe");
            Vector3[] v = m.vertices; int[] t2 = m.triangles;
            for (int i = 0; i < t2.Length; i += 3)
            {
                Vector3 a = v[t2[i]], c1 = v[t2[i + 1]], c2 = v[t2[i + 2]];
                Vector3 n = Vector3.Cross(c1 - a, c2 - a);
                Vector3 centroid = (a + c1 + c2) / 3f;
                Assert.That(Vector3.Dot(n, new Vector3(centroid.x, 0f, centroid.z)), Is.GreaterThan(0f), "lathe triangle " + i / 3 + " faces the axis");
            }
        }

        // Catches: a rounded box wound inside out: every triangle faces away from the box centre (a convex shape), and agrees with its normals.
        [Test]
        public void RoundedBox_TrianglesFaceOutward()
        {
            var b = new SmoothMeshBuilder();
            b.RoundedBox(Vector3.zero, new Vector3(1f, 0.4f, 0.6f), 0.05f, Quaternion.identity, 0, 0);
            Mesh m = b.ToMesh("Box");
            AssertWindingMatchesNormals(m, "rounded box");
            Vector3[] v = m.vertices; int[] t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], c1 = v[t[i + 1]], c2 = v[t[i + 2]];
                Vector3 n = Vector3.Cross(c1 - a, c2 - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                Assert.That(Vector3.Dot(n, (a + c1 + c2) / 3f), Is.GreaterThan(0f), "box triangle " + i / 3 + " faces the centre");
            }
        }

        [Test]
        public void Radial_WindingMatchesNormals()
        {
            var b = new SmoothMeshBuilder();
            b.Radial(Vector3.zero, d => 0.5f, 4, Quaternion.identity, Vector3.one, 0, 0, 0, d => 1f, 0.5f);
            AssertWindingMatchesNormals(b.ToMesh("Ball"), "radial");
        }

        // Catches: a texture source re-imported as a plain 2D texture (the material's 2DArray slot would be empty), the wrong flipbook grid, or the
        // wrong colour space (the env array is linear detail centred on 0.5; the fish array is sRGB albedo; a swap shifts every tile's value).
        [Test]
        public void TextureArrays_ImportAsFlipbookArrays()
        {
            var env = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetPaths.EnvDetailArray);
            Assert.That(env, Is.Not.Null, AssetPaths.EnvDetailArray);
            Assert.That(env.depth, Is.EqualTo(EnvTextureLayers.Count));
            Assert.That(env.width, Is.EqualTo(512));
            var skin = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetPaths.FishSkinArray);
            Assert.That(skin, Is.Not.Null, AssetPaths.FishSkinArray);
            Assert.That(skin.depth, Is.EqualTo(32), "4 x 8 sheet: 26 used layers (0-25) plus 6 white spares (M3-D91)");
            Assert.That(skin.width, Is.EqualTo(512), "the 4096 max size keeps the 512 x 512 tiles");
            Assert.That(skin.height, Is.EqualTo(512));
            var envImporter = (TextureImporter)AssetImporter.GetAtPath(AssetPaths.EnvDetailArray);
            var skinImporter = (TextureImporter)AssetImporter.GetAtPath(AssetPaths.FishSkinArray);
            Assert.That(envImporter.sRGBTexture, Is.False, "the env detail array is linear");
            Assert.That(skinImporter.sRGBTexture, Is.True, "the fish skin array is sRGB albedo");
        }
    }
}
