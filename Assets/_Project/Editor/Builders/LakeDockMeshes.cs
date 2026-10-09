// Radial height-field meshes for the lakebed and shore, saved as mesh assets and refilled in place (stable GUID, byte-identical rebuilds). Owned by T15.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class LakeDockMeshes
    {
        public const int Sectors = 48;

        // radii must be ascending; radii[0] == 0 makes a centre fan (disk), otherwise the mesh is a ring.
        public static Mesh BuildAsset(string assetPath, float[] radii, System.Func<float, float> heightAtRadius)
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(assetPath));
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.name = Path.GetFileNameWithoutExtension(assetPath);
            Fill(mesh, radii, heightAtRadius);
            if (created) AssetDatabase.CreateAsset(mesh, assetPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void Fill(Mesh mesh, float[] radii, System.Func<float, float> heightAtRadius)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int ring = 0; ring + 1 < radii.Length; ring++)
            {
                float r0 = radii[ring], r1 = radii[ring + 1];
                float y0 = heightAtRadius(r0), y1 = heightAtRadius(r1);
                for (int s = 0; s < Sectors; s++)
                {
                    Vector3 a0 = Point(r0, y0, s), b0 = Point(r0, y0, s + 1);
                    Vector3 a1 = Point(r1, y1, s), b1 = Point(r1, y1, s + 1);
                    AddTriangle(vertices, triangles, a0, a1, b1);
                    if (r0 > 0f) AddTriangle(vertices, triangles, a0, b1, b0);
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static Vector3 Point(float r, float y, int sector)
        {
            float angle = (sector % Sectors) * (Mathf.PI * 2f / Sectors);
            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        // Unshared vertices give flat facets. The winding is flipped when needed so every face normal points up (+y).
        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) { Vector3 swap = b; b = c; c = swap; }
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
    }
}
