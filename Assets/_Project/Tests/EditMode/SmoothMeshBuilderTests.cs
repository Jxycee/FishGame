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
            var ua = new List<Vector4>(); var uc = new List<Vector4>();
            a.GetUVs(1, ua); c.GetUVs(1, uc);
            Assert.That(uc, Is.EqualTo(ua));
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

        // Catches: a texture source re-imported as a plain 2D texture (the material's 2DArray slot would be empty) or the wrong flipbook grid.
        [Test]
        public void TextureArrays_ImportAsFlipbookArrays()
        {
            var env = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetPaths.EnvDetailArray);
            Assert.That(env, Is.Not.Null, AssetPaths.EnvDetailArray);
            Assert.That(env.depth, Is.EqualTo(EnvTextureLayers.Count));
            Assert.That(env.width, Is.EqualTo(512));
            var skin = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetPaths.FishSkinArray);
            Assert.That(skin, Is.Not.Null, AssetPaths.FishSkinArray);
            Assert.That(skin.depth, Is.EqualTo(8));
            Assert.That(skin.width, Is.EqualTo(512));
        }
    }
}
