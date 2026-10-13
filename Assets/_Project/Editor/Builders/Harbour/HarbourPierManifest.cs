// Reader for the artist's Content/Models/Harbour/harbour_manifest.json (T17-B). Owned by T17. Authority: none (editor).
// The manifest lists, per FBX, every placed piece: for sphere items (rocks, boulders, rubble) the sphere centre and radius, for box items the world
// AABB (bounds_min / bounds_max, Unity axes, yaw included). The builder places colliders from these numbers and never redraws the rocks in C#
// (the Blender generator's Python random is not Pcg32). The JSON copy next to the FBX copies is the single source for the collider data, so it
// must be updated together with them. Field names follow the JSON (JsonUtility needs exact names).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class HarbourPierManifest
    {
        [Serializable]
        public sealed class Item
        {
            public string name;
            public string collider;        // "sphere", "box", "box (optional)", "none (...)", "LadderVolume (layout)"
            public float[] position;
            public float radius;           // sphere items
            public float[] bounds_min;     // box and sphere items: the item's world AABB
            public float[] bounds_max;
        }

        [Serializable]
        public sealed class Model
        {
            public string name;            // the FBX mesh name, for example "Harbour_Rocks_Tri"
            public int triangles;          // the artist's count (Unity's import drops a few degenerate faces: HarbourPierModelTests compares with a margin)
            public float[] bounds_min;     // the model's world AABB
            public float[] bounds_max;
            public Item[] items;
        }

        [Serializable]
        sealed class Doc
        {
            public Model[] fbx;
        }

        public static Model[] Load()
        {
            string path = BuilderUtil.Abs(HarbourPierLayout.ManifestPath);
            if (!System.IO.File.Exists(path)) throw new FileNotFoundException("Missing harbour manifest " + HarbourPierLayout.ManifestPath);
            Doc file = JsonUtility.FromJson<Doc>(System.IO.File.ReadAllText(path));
            if (file == null || file.fbx == null || file.fbx.Length == 0) throw new InvalidOperationException("Harbour manifest has no fbx list");
            return file.fbx;
        }

        public static Model Find(Model[] models, string name)
        {
            foreach (Model model in models) if (model.name == name) return model;
            throw new InvalidOperationException("Harbour manifest has no model " + name);
        }

        public static Vector3 ToVector(float[] v)
        {
            if (v == null || v.Length != 3) throw new InvalidOperationException("Harbour manifest: expected a 3-component vector");
            return new Vector3(v[0], v[1], v[2]);
        }

        // Items of the model whose collider text starts with the given prefix ("sphere" or "box"), in manifest order.
        public static List<Item> ItemsWithCollider(Model model, string prefix)
        {
            var list = new List<Item>();
            foreach (Item item in model.items)
                if (item.collider != null && item.collider.StartsWith(prefix, StringComparison.Ordinal)) list.Add(item);
            return list;
        }

        // The sphere items of a model, validated: the expected count, a positive radius and a centre on every one. colliderPrefix matches the
        // item's "collider" text ("sphere" for rocks and boulders, "none (underwater" for the rubble base, whose spheres are optional in the art).
        public static List<Item> Spheres(Model[] models, string modelName, string colliderPrefix, int expectedCount)
        {
            Model model = Find(models, modelName);
            var list = new List<Item>();
            foreach (Item item in model.items)
            {
                if (item.collider == null || !item.collider.StartsWith(colliderPrefix, StringComparison.Ordinal)) continue;
                if (!(item.radius > 0f)) throw new InvalidOperationException("Harbour manifest: " + modelName + "/" + item.name + " has no sphere radius");
                ToVector(item.position);
                list.Add(item);
            }
            if (list.Count != expectedCount)
                throw new InvalidOperationException("Harbour manifest: " + modelName + " lists " + list.Count + " sphere items, expected " + expectedCount
                                                    + " (the art changed: re-check the colliders)");
            return list;
        }
    }
}
