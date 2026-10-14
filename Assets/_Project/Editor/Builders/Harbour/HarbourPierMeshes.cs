// Meshes of Spot_HarbourPier that stay generated in code (T17-B; plan T17.3 reduced to the code meshes): the water grid, the seabed (Ground) and the
// beach and plateau (Shore). The pier, breakwater, rocks and props are Blender FBX now (Content/Models/Harbour). Owned by T17. Authority: none (editor).
// The seabed and beach are exact height fields (HarbourPierLayout.SeabedY / BeachY): the bake, the analytic test, CastProbe and the posts depend on
// the numbers, so one mesh serves the MeshFilter and the MeshCollider (a new map has no M1 collider to keep). Smooth shared vertices, normals from the
// slope (central difference over 1 m), uv0 = world xz in metres, uv1 = (palette slot, texture layer, mapping, 0) on every vertex, vertex colour =
// half-scale grey 0.5 x ao (BF-7; FishGame/Toon with _JitterEncoded 1). Saved IN PLACE (EditorUtility.CopySerialized into the existing asset, GUIDs
// kept); fixed order, no random: two builds leave every Harbour_*.asset byte-identical.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using FishGame.Runtime;

namespace FishGame.EditorTools
{
    internal static class HarbourPierMeshes
    {
        // Builds and saves Harbour_Water, Harbour_Seabed and Harbour_Beach (step 1 of HarbourPierBuilder.Build, before NewScene).
        public static void BuildAll()
        {
            string[] paths = { HarbourPierLayout.WaterMeshPath, HarbourPierLayout.WaterSkirtMeshPath, HarbourPierLayout.SeabedMeshPath, HarbourPierLayout.BeachMeshPath };
            List<Mesh> meshes = BuildAllInMemory();
            for (int i = 0; i < meshes.Count; i++) Save(paths[i], meshes[i]);
            AssetDatabase.SaveAssets();   // meshes are on disk before the scene references them
        }

        // The same meshes, unsaved, in a fixed order (water, water skirt, seabed, beach). Test seam: the caller destroys them.
        internal static List<Mesh> BuildAllInMemory()
        {
            return new List<Mesh> { BuildWater(), BuildWaterSkirt(), BuildSeabed(), BuildBeach() };
        }

        // mirrors LakeDockMeshes.Save. Copies the built mesh into the existing asset (GUID kept) or creates the asset. The temporary mesh is destroyed.
        internal static void Save(string assetPath, Mesh built)
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(assetPath));
            string name = Path.GetFileNameWithoutExtension(assetPath);
            built.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, assetPath);
                return;
            }
            EditorUtility.CopySerialized(built, existing);
            existing.name = name;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
        }

        // 160 x 160 cells of 1.5 m = 240 m, centred, y 0 (25,921 vertices, 16-bit safe). 1.5 m cells keep spec 10.4's wavelength >= 4 cells (seeds >= 6 m).
        static Mesh BuildWater()
        {
            return MeshBuilder.Grid(HarbourPierLayout.WaterGridCells, HarbourPierLayout.WaterGridCells, HarbourPierLayout.WaterCellSize, "Harbour_Water");
        }

        // The sea beyond the water grid: a ring between the grid's 640-vertex border (the very same expression as MeshBuilder.Grid, so the vertices match
        // bit for bit and leave no crack) and a square at +-SkirtOuterHalfExtent with 8 segments per side. The two loops are zipped by perimeter fraction
        // (outer vertex k sits at k / 32 of the way round, inner vertex i at i / 640), which is the four trapezoid strips of the ring. Normals up, y 0,
        // 16-bit safe. Not Toon (water shader): no vertex colours or uv1.
        static Mesh BuildWaterSkirt()
        {
            int cells = HarbourPierLayout.WaterGridCells;
            float size = HarbourPierLayout.WaterCellSize;
            var inner = new List<Vector3>();   // counter-clockwise from (min, min): along z = min, along x = max, back along z = max, down x = min
            for (int i = 0; i <= cells; i++) inner.Add(new Vector3(GridCoord(cells, size, i), 0f, GridCoord(cells, size, 0)));
            for (int j = 1; j <= cells; j++) inner.Add(new Vector3(GridCoord(cells, size, cells), 0f, GridCoord(cells, size, j)));
            for (int i = cells - 1; i >= 0; i--) inner.Add(new Vector3(GridCoord(cells, size, i), 0f, GridCoord(cells, size, cells)));
            for (int j = cells - 1; j >= 1; j--) inner.Add(new Vector3(GridCoord(cells, size, 0), 0f, GridCoord(cells, size, j)));

            float e = HarbourPierLayout.SkirtOuterHalfExtent;
            int seg = HarbourPierLayout.SkirtOuterSegmentsPerSide;
            var outer = new List<Vector3>();
            for (int m = 0; m < seg; m++) outer.Add(new Vector3(-e + 2f * e * m / seg, 0f, -e));
            for (int m = 0; m < seg; m++) outer.Add(new Vector3(e, 0f, -e + 2f * e * m / seg));
            for (int m = 0; m < seg; m++) outer.Add(new Vector3(e - 2f * e * m / seg, 0f, e));
            for (int m = 0; m < seg; m++) outer.Add(new Vector3(-e, 0f, e - 2f * e * m / seg));

            var vertices = new List<Vector3>(inner);
            vertices.AddRange(outer);
            int ni = inner.Count, no = outer.Count;
            var triangles = new List<int>();
            int a = 0, b = 0;
            while (a < ni || b < no)
            {
                int ia = a % ni, ib = ni + b % no;
                if (b >= no || (a < ni && (long)(a + 1) * no <= (long)(b + 1) * ni))
                {
                    AddIndexedUp(vertices, triangles, ia, (a + 1) % ni, ib);   // advance the inner loop
                    a++;
                }
                else
                {
                    AddIndexedUp(vertices, triangles, ib, ia, ni + (b + 1) % no);   // advance the outer loop
                    b++;
                }
            }

            var normals = new List<Vector3>(vertices.Count);
            var uv0 = new List<Vector2>(vertices.Count);
            foreach (Vector3 v in vertices)
            {
                normals.Add(Vector3.up);
                uv0.Add(new Vector2(v.x, v.z) / (2f * e) + new Vector2(0.5f, 0.5f));
            }
            var mesh = new Mesh { name = "Harbour_WaterSkirt" };
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f * e, 2f, 2f * e));   // +-1 m covers the shader displacement, as MeshBuilder.Grid
            return mesh;
        }

        // MeshBuilder.Grid's vertex coordinate along one axis, written the same way so the floats are identical.
        static float GridCoord(int cells, float size, int i) => -cells * size * 0.5f + i * size;

        // x -400..400 (20 m columns), z -47..121 on 2 m rows: the kinks of SeabedY (z -45 and z -9) are rows, so the surface is exactly the formula.
        static Mesh BuildSeabed()
        {
            int nx = Mathf.RoundToInt((HarbourPierLayout.SeabedMaxX - HarbourPierLayout.SeabedMinX) / HarbourPierLayout.SeabedStepX) + 1;
            int nz = Mathf.RoundToInt((HarbourPierLayout.SeabedMaxZ - HarbourPierLayout.SeabedMinZ) / HarbourPierLayout.SeabedStepZ) + 1;
            var vertices = new List<Vector3>(nx * nz);
            var normals = new List<Vector3>(nx * nz);
            var uv0 = new List<Vector2>(nx * nz);
            var uv1 = new List<Vector4>(nx * nz);
            var colors = new List<Color>(nx * nz);
            var triangles = new List<int>((nx - 1) * (nz - 1) * 6);
            for (int iz = 0; iz < nz; iz++)
            {
                float z = HarbourPierLayout.SeabedMinZ + iz * HarbourPierLayout.SeabedStepZ;
                float y = HarbourPierLayout.SeabedY(z);
                float slope = (HarbourPierLayout.SeabedY(z + 1f) - HarbourPierLayout.SeabedY(z - 1f)) * 0.5f;
                Vector3 normal = new Vector3(0f, 1f, -slope).normalized;
                // ao 1 at the waterline to 0.8 at 9 m depth (the water's depth fade does the rest).
                float ao = Mathf.Lerp(1f, 0.8f, Mathf.Clamp01(-y / HarbourPierLayout.SeabedMaxDepth));
                float g = 0.5f * ao;
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = HarbourPierLayout.SeabedMinX + ix * HarbourPierLayout.SeabedStepX;
                    vertices.Add(new Vector3(x, y, z));
                    normals.Add(normal);
                    uv0.Add(new Vector2(x, z));
                    uv1.Add(new Vector4(LakeDockLayout.SlotSand, EnvTextureLayers.Sand, SmoothMeshBuilder.MappingUv, 0f));
                    colors.Add(new Color(g, g, g, 1f));
                }
            }
            for (int iz = 0; iz + 1 < nz; iz++)
            {
                for (int ix = 0; ix + 1 < nx; ix++)
                {
                    int a = iz * nx + ix, b = (iz + 1) * nx + ix, c = (iz + 1) * nx + ix + 1, d = iz * nx + ix + 1;
                    AddIndexedUp(vertices, triangles, a, b, c);
                    AddIndexedUp(vertices, triangles, a, c, d);
                }
            }
            return Finish("Harbour_Seabed", vertices, normals, uv0, uv1, colors, triangles);
        }

        // Rows (z): the submerged lip -44.5, then -45 down to -55 every metre (the two kinks), then -100 (flat plateau). Each band between two rows has its
        // own vertices, so its texture layer (uv1.y) is constant over the band; the palette slot is per vertex (sand below BeachSandMaxY, grass above),
        // so the band that crosses the edge blends sand into grass.
        static Mesh BuildBeach()
        {
            var rows = new List<float> { HarbourPierLayout.BeachLipZ };
            for (float z = HarbourPierLayout.CoastZ; z >= HarbourPierLayout.BeachFlatFromZ - 0.001f; z -= 1f) rows.Add(z);
            rows.Add(HarbourPierLayout.BeachEndZ);
            int nx = Mathf.RoundToInt((HarbourPierLayout.BeachMaxX - HarbourPierLayout.BeachMinX) / HarbourPierLayout.BeachStep) + 1;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int band = 0; band + 1 < rows.Count; band++)
            {
                float zA = rows[band], zB = rows[band + 1];
                float yA = HarbourPierLayout.BeachY(zA), yB = HarbourPierLayout.BeachY(zB);
                byte layer = (yA + yB) * 0.5f < HarbourPierLayout.BeachSandMaxY ? EnvTextureLayers.Sand : EnvTextureLayers.Grass;
                int start = vertices.Count;
                for (int k = 0; k < 2; k++)
                {
                    float z = k == 0 ? zA : zB, y = k == 0 ? yA : yB;
                    float slope = (HarbourPierLayout.BeachY(z + 1f) - HarbourPierLayout.BeachY(z - 1f)) * 0.5f;
                    Vector3 normal = new Vector3(0f, 1f, -slope).normalized;
                    byte slot = y < HarbourPierLayout.BeachSandMaxY ? LakeDockLayout.SlotSand : LakeDockLayout.SlotGrass;
                    for (int ix = 0; ix < nx; ix++)
                    {
                        float x = HarbourPierLayout.BeachMinX + ix * HarbourPierLayout.BeachStep;
                        vertices.Add(new Vector3(x, y, z));
                        normals.Add(normal);
                        uv0.Add(new Vector2(x, z));
                        uv1.Add(new Vector4(slot, layer, SmoothMeshBuilder.MappingUv, 0f));
                        colors.Add(new Color(0.5f, 0.5f, 0.5f, 1f));
                    }
                }
                for (int ix = 0; ix + 1 < nx; ix++)
                {
                    int a = start + ix, b = start + nx + ix, c = start + nx + ix + 1, d = start + ix + 1;
                    AddIndexedUp(vertices, triangles, a, b, c);
                    AddIndexedUp(vertices, triangles, a, c, d);
                }
            }
            return Finish("Harbour_Beach", vertices, normals, uv0, uv1, colors, triangles);
        }

        static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv0, List<Vector4> uv1, List<Color> colors, List<int> triangles)
        {
            if (triangles.Count / 3 > LakeDockLayout.MaxCombinedTriangles)
                throw new System.InvalidOperationException(name + " has " + triangles.Count / 3 + " triangles (limit " + LakeDockLayout.MaxCombinedTriangles + ")");
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // The winding is flipped when needed so the face normal points up (+y), as the lake's smooth height fields.
        static void AddIndexedUp(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).y < 0f) { int swap = b; b = c; c = swap; }
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
    }
}
