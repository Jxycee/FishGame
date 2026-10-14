// Harbour round 2b (2026-10-16, user unlock "ground cover"). Authority: none (editor builder). The Blender generator scatters the ground cover
// (fg_harbour.py build_groundcover -> harbour_groundcover.json next to the manifest: per kind, x y z yaw scale per instance, world space). This
// builds one small tuft mesh per kind - crossed cards into T_CrownCards' grass and flower cells, palette slot in uv1.x, a vertex-colour ao ramp
// (dark roots, light tips; 0.5 x ao, _JitterEncoded 1), up-facing normals so the tufts light like the lawn they stand on - saves them in place
// (Harbour_GC_<kind>.asset, fixed order, no random: byte-identical rebuilds) and adds the GroundCover component (GPU instancing on
// M_Toon_Foliage) under the Detail root: hidden on Low with the rest of the detail, no colliders.
using System.Collections.Generic;
using FishGame.Presentation;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class HarbourGroundCover
    {
        public const string ScatterPath = HarbourModelPostprocessor.HarbourModelsFolder + "/harbour_groundcover.json";
        public const float CellSize = 16f;

        // palette slots (ArtPalette order; fg_harbour.py S / S2)
        const int Grass = 0, Foliage = 1, Reed = 7, Roof = 9, Accent = 10, Snow = 11, FoliageAlt = 15;

        // atlas cells (u0, v0, u1, v1): fg_crowncards.py CARD_UV
        static readonly Vector4 GrassCell = new Vector4(0.5f, 0f, 0.75f, 0.5f);
        static readonly Vector4 FlowerCell = new Vector4(0.75f, 0f, 1f, 0.5f);

        readonly struct Part
        {
            public readonly Vector4 Cell;
            public readonly int Slot, Cards;
            public readonly float Width, Height, Yaw0, Lift;
            public Part(Vector4 cell, int slot, int cards, float width, float height, float yaw0 = 0f, float lift = 0f)
            {
                Cell = cell; Slot = slot; Cards = cards; Width = width; Height = height; Yaw0 = yaw0; Lift = lift;
            }
        }

        // kind -> its parts (kinds as fg_harbour.py GC_KINDS)
        static readonly (string kind, Part[] parts)[] Kinds =
        {
            ("grass", new[] { new Part(GrassCell, Grass, 3, 0.34f, 0.42f), new Part(GrassCell, Foliage, 2, 0.26f, 0.3f, 30f) }),
            ("grass_dry", new[] { new Part(GrassCell, Reed, 3, 0.36f, 0.5f), new Part(GrassCell, Grass, 2, 0.26f, 0.32f, 30f) }),
            ("flower_white", new[] { new Part(GrassCell, Grass, 2, 0.3f, 0.34f), new Part(FlowerCell, Snow, 2, 0.32f, 0.46f, 45f) }),
            ("flower_orange", new[] { new Part(GrassCell, Grass, 2, 0.3f, 0.34f), new Part(FlowerCell, Accent, 2, 0.32f, 0.46f, 45f) }),
            ("flower_red", new[] { new Part(GrassCell, Grass, 2, 0.3f, 0.34f), new Part(FlowerCell, Roof, 2, 0.32f, 0.46f, 45f) }),
            ("dune", new[] { new Part(GrassCell, Reed, 3, 0.4f, 0.72f), new Part(GrassCell, Reed, 2, 0.3f, 0.5f, 30f) }),
            // quality-first round 1: the short lawn carpet (wide, low crossed cards) and its dry variant
            ("carpet", new[] { new Part(GrassCell, Grass, 3, 0.52f, 0.2f), new Part(GrassCell, FoliageAlt, 2, 0.42f, 0.15f, 30f) }),
            ("carpet_dry", new[] { new Part(GrassCell, Reed, 3, 0.5f, 0.2f), new Part(GrassCell, Grass, 2, 0.4f, 0.15f, 30f) }),
        };

        public static string MeshPath(string kind) => AssetPaths.MeshesFolder + "/Harbour_GC_" + kind + ".asset";

        // The tuft meshes, unsaved, in Kinds order. Test seam: the caller destroys them.
        internal static List<Mesh> BuildMeshesInMemory()
        {
            var list = new List<Mesh>();
            foreach (var (kind, parts) in Kinds) list.Add(BuildTuft("Harbour_GC_" + kind, parts));
            return list;
        }

        static Mesh BuildTuft(string name, Part[] parts)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var c = new List<Color>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var tris = new List<int>();
            foreach (Part p in parts)
            {
                for (int k = 0; k < p.Cards; k++)
                {
                    float yaw = (p.Yaw0 + 180f * k / p.Cards) * Mathf.Deg2Rad;
                    var u = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                    int b = v.Count;
                    for (int j = 0; j < 2; j++)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            v.Add(u * (p.Width * (i - 0.5f)) + new Vector3(0f, p.Lift + p.Height * j, 0f));
                            n.Add(Vector3.up);
                            float ao = j == 0 ? 0.5f : 1f;            // dark roots, light tips (the shader multiplies by 2 x colour)
                            c.Add(new Color(0.5f * ao, 0.5f * ao, 0.5f * ao, 1f));
                            uv0.Add(new Vector2(Mathf.Lerp(p.Cell.x, p.Cell.z, i), Mathf.Lerp(p.Cell.y, p.Cell.w, j)));
                            uv1.Add(new Vector4(p.Slot, 0f, 0f, 0f));
                        }
                    }
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Saves the tuft meshes (before the scene references them).
        public static void BuildMeshes()
        {
            List<Mesh> meshes = BuildMeshesInMemory();
            for (int i = 0; i < meshes.Count; i++) HarbourPierMeshes.Save(MeshPath(Kinds[i].kind), meshes[i]);
            AssetDatabase.SaveAssets();
        }

        // The GroundCover object under the Detail root (Default layer, like the other detail).
        public static GroundCover AddTo(Transform detailRoot, Material foliage)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(ScatterPath);
            if (json == null) throw new System.InvalidOperationException("missing " + ScatterPath + " (run Art/Blender/fg_harbour.py and copy it next to the manifest)");
            if (!foliage.enableInstancing)
            {
                foliage.enableInstancing = true;
                EditorUtility.SetDirty(foliage);
            }
            var kinds = new GroundCover.KindMesh[Kinds.Length];
            for (int i = 0; i < Kinds.Length; i++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(Kinds[i].kind));
                if (mesh == null) throw new System.InvalidOperationException("missing " + MeshPath(Kinds[i].kind) + " (BuildMeshes first)");
                kinds[i] = new GroundCover.KindMesh { Name = Kinds[i].kind, Mesh = mesh };
            }
            var go = new GameObject("GroundCover");
            go.transform.SetParent(detailRoot, false);
            go.layer = detailRoot.gameObject.layer;
            GroundCover cover = go.AddComponent<GroundCover>();
            cover.Configure(json, kinds, foliage, CellSize);
            return cover;
        }
    }
}
