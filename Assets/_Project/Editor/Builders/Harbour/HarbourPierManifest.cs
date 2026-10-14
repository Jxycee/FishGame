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
            public float[] light;          // pass 2: a lamp post's light point (world)
            public string light_colour;    // round 2: "green" for the mole's harbour light; else the warm lamp colour
        }

        // Pass 2: one instance of a local model (position + yaw about +y; a frame other than world is named in `frame`).
        [Serializable]
        public sealed class Placement
        {
            public string name;
            public float[] position;
            public float yaw_deg;
            public string animHint;
            public string frame;
            public string collider;        // ice chests: "box" with `size`
            public float[] size;
            public float height_scale;     // code trees: the Pcg32 height (the parent Tree_NN's localScale)
            public bool pine;              // code trees
            public float radius;           // round 1: a circling gull's circle radius (animHint circle)
        }

        // Pass 2: a named point (smoke, lights); `frame` "CrewBoat local" for the points on the tug, else world.
        [Serializable]
        public sealed class Point
        {
            public string name;
            public float[] position;
            public string frame;
            public string animHint;
            public float yaw_deg;   // round 3: the searchlight's resting heading
        }

        // Pass 2: the tug's ladders (CrewBoat local frame): the LadderVolume box is volume_min..volume_max.
        [Serializable]
        public sealed class Ladder
        {
            public string name;
            public float[] volume_min;
            public float[] volume_max;
        }

        [Serializable]
        public sealed class Model
        {
            public string name;            // the FBX mesh name, for example "Harbour_Rocks_Tri"
            public int triangles;          // the artist's count (Unity's import drops a few degenerate faces: HarbourPierModelTests compares with a margin)
            public float[] bounds_min;     // the model's world AABB
            public float[] bounds_max;
            public Item[] items;
            // pass 2 (absent on the phase-1 models)
            public string group;           // "core", "detail", "col"
            public bool local_space;       // authored around its own pivot: instantiate once per placement
            public Placement[] placements;
            public Ladder[] ladders;       // CrewBoat
            public Point[] light_points;
            public Point[] smoke_points;
            public float[] interior_light; // CrewBoat local
        }

        [Serializable]
        public sealed class Pass2
        {
            public Point[] smoke_points;   // every smoke point, world or CrewBoat local
            public Point[] light_points;
        }

        [Serializable]
        sealed class Doc
        {
            public Model[] fbx;
            public Pass2 pass2;
        }

        public static Model[] Load()
        {
            string path = BuilderUtil.Abs(HarbourPierLayout.ManifestPath);
            if (!System.IO.File.Exists(path)) throw new FileNotFoundException("Missing harbour manifest " + HarbourPierLayout.ManifestPath);
            Doc file = JsonUtility.FromJson<Doc>(System.IO.File.ReadAllText(path));
            if (file == null || file.fbx == null || file.fbx.Length == 0) throw new InvalidOperationException("Harbour manifest has no fbx list");
            return file.fbx;
        }

        public static Pass2 LoadPass2()
        {
            string path = BuilderUtil.Abs(HarbourPierLayout.ManifestPath);
            Doc file = JsonUtility.FromJson<Doc>(System.IO.File.ReadAllText(path));
            if (file == null || file.pass2 == null || file.pass2.smoke_points == null) throw new InvalidOperationException("Harbour manifest has no pass2 block");
            return file.pass2;
        }

        public static bool IsDetail(Model model) => model.group == "detail";
        public static bool IsCollision(Model model) => model.group == "col";

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
