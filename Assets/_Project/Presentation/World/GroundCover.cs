// Authority: local only (every peer draws its own copy); purely visual. Owns no collider and writes no gameplay state.
// Harbour round 2b (user unlock "ground cover"): GPU-instanced grass tufts, wildflowers and dune grass. The Blender generator writes the scatter
// (harbour_groundcover.json: per kind, x y z yaw scale per instance, world space; deterministic); the editor builder (HarbourGroundCover) makes
// one small crossed-card tuft mesh per kind and wires them here. On first draw the component parses the scatter, turns it into Matrix4x4
// batches (<= 1023 per draw, grouped in square spatial cells so each draw's worldBounds frustum-culls it) and every frame submits them with
// Graphics.RenderMeshInstanced (no shadow casting, receives shadows). It lives under the harbour's Detail root, so the Low preset
// (AmbientLife.ApplyDetail -> inactive) draws none of it. Edit-mode captures call Submit(camera) themselves (Update does not run there).
// Harbour round 3 (perf budget, user 2026-10-17): cells farther than drawDistance (40 m) from the viewing camera (Camera.main at runtime) are
// not submitted - grass tufts past that are a few pixels and pure alpha-test overdraw.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GroundCover : MonoBehaviour
    {
        public const int MaxPerDraw = 1023;
        public const int Stride = 5;   // x, y, z, yaw (radians), scale

        [Serializable]
        public struct KindMesh
        {
            public string Name;
            public Mesh Mesh;
        }

        [Serializable]
        public sealed class ScatterKind
        {
            public string name;
            public float[] points;
        }

        [Serializable]
        public sealed class Scatter
        {
            public string note;
            public ScatterKind[] kinds;
        }

        [SerializeField] TextAsset scatter;
        [SerializeField] KindMesh[] meshes = Array.Empty<KindMesh>();
        [SerializeField] Material material;
        [SerializeField] float cellSize = 16f;
        [SerializeField] float drawDistance = 60f;   // round 3: 0 = unlimited; quality-first round 1: 60 m (was 40)

        struct Batch
        {
            public Mesh Mesh;
            public Matrix4x4[] Matrices;
            public Bounds Bounds;
        }

        readonly List<Batch> batches = new List<Batch>();
        bool built;
        int instances;

        public void Configure(TextAsset scatterJson, KindMesh[] kindMeshes, Material instancedMaterial, float cell)
        {
            scatter = scatterJson;
            meshes = kindMeshes ?? Array.Empty<KindMesh>();
            material = instancedMaterial;
            cellSize = Mathf.Max(1f, cell);
            built = false;
        }

        public static Scatter Parse(TextAsset json) => json != null ? JsonUtility.FromJson<Scatter>(json.text) : null;

        public int InstanceCount
        {
            get { if (!built) Build(); return instances; }
        }

        void OnEnable() => built = false;

        Mesh MeshFor(string kind)
        {
            foreach (KindMesh k in meshes) if (k.Name == kind) return k.Mesh;
            return null;
        }

        void Build()
        {
            batches.Clear();
            instances = 0;
            built = true;
            Scatter data = Parse(scatter);
            if (data == null || data.kinds == null || material == null) return;
            foreach (ScatterKind kind in data.kinds)
            {
                Mesh mesh = MeshFor(kind.name);
                if (mesh == null || kind.points == null || kind.points.Length < Stride) continue;
                var cells = new SortedDictionary<long, List<Matrix4x4>>();
                float maxScale = 0f;
                for (int i = 0; i + Stride <= kind.points.Length; i += Stride)
                {
                    float x = kind.points[i], y = kind.points[i + 1], z = kind.points[i + 2], yaw = kind.points[i + 3], s = kind.points[i + 4];
                    maxScale = Mathf.Max(maxScale, s);
                    long key = ((long)Mathf.FloorToInt(x / cellSize) << 32) ^ (uint)Mathf.FloorToInt(z / cellSize);
                    if (!cells.TryGetValue(key, out List<Matrix4x4> list)) cells.Add(key, list = new List<Matrix4x4>());
                    list.Add(Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f), new Vector3(s, s, s)));
                    instances++;
                }
                Bounds local = mesh.bounds;
                Vector3 pad = Vector3.one * (2f * (local.extents.magnitude + local.center.magnitude) * Mathf.Max(1f, maxScale));
                foreach (List<Matrix4x4> list in cells.Values)
                {
                    for (int start = 0; start < list.Count; start += MaxPerDraw)
                    {
                        int n = Mathf.Min(MaxPerDraw, list.Count - start);
                        var m = new Matrix4x4[n];
                        list.CopyTo(start, m, 0, n);
                        var b = new Bounds(m[0].GetColumn(3), Vector3.zero);
                        foreach (Matrix4x4 mm in m) b.Encapsulate(mm.GetColumn(3));
                        b.Expand(pad);
                        batches.Add(new Batch { Mesh = mesh, Matrices = m, Bounds = b });
                    }
                }
            }
        }

        void Update() => Submit(null);

        bool InRange(Batch b, Camera viewer)
        {
            if (drawDistance <= 0f || viewer == null) return true;
            return b.Bounds.SqrDistance(viewer.transform.position) <= drawDistance * drawDistance;
        }

        // Queues every batch for this frame (camera null: every camera, ranged from Camera.main; otherwise that camera only, ranged from it).
        public void Submit(Camera camera)
        {
            if (!built) Build();
            Camera viewer = camera != null ? camera : Camera.main;
            foreach (Batch b in batches)
            {
                if (!InRange(b, viewer)) continue;
                var rp = new RenderParams(material)
                {
                    worldBounds = b.Bounds,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    layer = gameObject.layer,
                    camera = camera,
                };
                Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.Matrices);
            }
        }

        // The triangles of the batches inside the frustum planes (null: all), for the harbour capture's budget report. Draws nothing.
        public long CountTriangles(Plane[] planes, Camera viewer = null)
        {
            if (!built) Build();
            long tris = 0;
            foreach (Batch b in batches)
                if ((planes == null || GeometryUtility.TestPlanesAABB(planes, b.Bounds)) && InRange(b, viewer))
                    tris += (long)b.Matrices.Length * (b.Mesh.GetIndexCount(0) / 3);
            return tris;
        }

        // Round 3 audit: the instances drawn for this camera (frustum and draw distance), and those within `near` metres of it.
        public void CountInstances(Camera viewer, float near, out int drawn, out int close)
        {
            if (!built) Build();
            drawn = close = 0;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(viewer);
            Vector3 eye = viewer.transform.position;
            foreach (Batch b in batches)
            {
                if (!GeometryUtility.TestPlanesAABB(planes, b.Bounds) || !InRange(b, viewer)) continue;
                drawn += b.Matrices.Length;
                foreach (Matrix4x4 m in b.Matrices)
                    if (((Vector3)m.GetColumn(3) - eye).sqrMagnitude <= near * near) close++;
            }
        }
    }
}
