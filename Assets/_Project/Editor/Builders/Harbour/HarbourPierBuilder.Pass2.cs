// HarbourPierBuilder pass 2 (harbour-art2 detail pass, round 0 2026-10-14). Owned by the harbour art pass. Authority: none (editor; builds scene
// objects). Places the pass-2 Blender models from the manifest (HarbourPierLayout.Visuals parents Land / Village / Detail / CrewBoat / CodeTrees):
//  - world models at the origin; local models once per manifest placement (position + yaw); the tug's ice chests, ladders, mast light and
//    smokestack in the tug's local frame, parented to it;
//  - colliders: box items -> BoxColliders (Land: Shore, the stone wall and gate pillars; Village: Structure; trunk-only trees and the net shed's
//    back half come from the manifest's bounds), Harbour_Dunes_Col and Harbour_Core_CrewBoat_Col -> MeshCollider only, Shore. No detail model
//    has a collider. The tug's hull visual bobs (AmbientLife); its collision mesh, chest boxes and ladder volumes are static on the tug root;
//  - ambient life: animHint motion, seven smoke points (light particles), lamp and string-light glow at dusk, the lighthouse beacon and the
//    tug's mast light; the Detail root (and the detail pieces riding on the tug) hide on the Low preset (AmbientLife.ApplyDetail).
// The castable grid changes on purpose with this pass: the tug hull (Shore) is land to the bake and the gangway (Structure) blocks CastProbe.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Presentation;
using FishGame.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.EditorTools
{
    public static partial class HarbourPierBuilder
    {
        sealed class LifeBuild
        {
            public readonly List<AmbientLife.Motion> Motions = new List<AmbientLife.Motion>();
            public readonly List<GameObject> Detail = new List<GameObject>();
            public readonly List<Light> Lamps = new List<Light>();
            public readonly List<Renderer> Glows = new List<Renderer>();
            public readonly List<ParticleSystem> Smoke = new List<ParticleSystem>();
            public Light Beacon, Blink;
            public Renderer BlinkGlow;

            public void Move(Transform target, AmbientLife.MotionKind kind, float amplitude, Vector3 axis = default)
            {
                // Deterministic phase from the order of creation (golden-angle spacing), so identical props never move in step.
                float phase = (Motions.Count * 2.39996f) % (2f * Mathf.PI);
                Motions.Add(new AmbientLife.Motion { Target = target, Kind = kind, Phase = phase, Amplitude = amplitude, Axis = axis });
            }
        }

        // The scene's pass-2 content and its AmbientLife root. Runs after AddProps (Build order).
        static void AddPass2(ArtAssets art)
        {
            var life = new LifeBuild();
            AddLand(art, life);
            AddVillage(art);
            Transform hull = AddCrewBoat(art, life);
            AddDetail(art, life, hull);
            AddSmoke(art, life, hull);
            AddHarbourLights(art, life, hull);

            GameObject root = Root(HarbourPierLayout.RootLife, Layers.Default);
            root.AddComponent<AmbientLife>().Configure(life.Motions.ToArray(), life.Detail.ToArray(), life.Lamps.ToArray(), life.Glows.ToArray(),
                                                       life.Beacon, life.Blink, life.BlinkGlow, life.Smoke.ToArray());
        }

        static int VisualIndex(string meshName)
        {
            for (int i = 0; i < HarbourPierLayout.Visuals.Length; i++) if (HarbourPierLayout.Visuals[i].MeshName == meshName) return i;
            throw new System.InvalidOperationException("HarbourPierLayout.Visuals has no " + meshName);
        }

        static Mesh VisualMesh(ArtAssets art, string meshName) => art.VisualMeshes[VisualIndex(meshName)];

        // Round 1: the river ribbon (with the creek and the tide pools) draws with M_Water_Creek (no shore-foam band: it sits cm over the land).
        static Material MaterialFor(HarbourPierLayout.VisualModel visual, ArtAssets art) =>
            visual.MeshName == "Harbour_Core_River" ? art.Creek : visual.Cards ? art.Foliage : visual.Triplanar ? art.ToonTri : art.Toon;

        // Flat ground ribbons, the river and the small low dressing cast nothing worth a shadow pass (GTX 900 budget).
        static bool CastsShadows(string meshName) =>
            meshName != "Harbour_Core_River" && meshName != "Harbour_Core_Paths" && meshName != "Harbour_Detail_Plants_A" && meshName != "Harbour_Detail_Plants_B"
            && meshName != "Harbour_Detail_Reeds" && meshName != "Harbour_Detail_TidePools_Tri" && meshName != "Harbour_Detail_Shore" && meshName != "Harbour_Detail_Lights"
            && meshName != "Harbour_Detail_Terraces" && meshName != "Harbour_Detail_Anim_GullFly" && meshName != "Harbour_Detail_StorySigns"
            && meshName != "Harbour_Detail_Anim_Duck" && meshName != "Harbour_Detail_Anim_Crab";

        // World models of one parent at the origin (collision models get a MeshCollider only); returns the root.
        static GameObject AddWorldModels(ArtAssets art, string rootName, int layer)
        {
            GameObject root = Root(rootName, layer);
            foreach (HarbourPierLayout.VisualModel visual in HarbourPierLayout.Visuals)
            {
                if (visual.Parent != rootName) continue;
                HarbourPierManifest.Model model = HarbourPierManifest.Find(art.Manifest, visual.MeshName);
                if (model.local_space) continue;
                Mesh mesh = VisualMesh(art, visual.MeshName);
                if (HarbourPierManifest.IsCollision(model))
                {
                    var go = new GameObject(visual.Name);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    continue;
                }
                MeshRenderer r = AddVisual(root.transform, visual.Name, mesh, MaterialFor(visual, art), Quaternion.identity, CastsShadows(visual.MeshName));
                if (visual.MeshName == "Harbour_Core_River") r.receiveShadows = false;
            }
            return root;
        }

        // One BoxCollider per "box" item of the model (bounds_min/max, world, unrotated), named <item>_<index>.
        static void AddBoxItems(Transform parent, HarbourPierManifest.Model model)
        {
            var holder = new GameObject(model.name.Replace("Harbour_Core_", "").Replace("_Tri", "") + "Colliders");
            holder.transform.SetParent(parent, false);
            int i = 0;
            foreach (HarbourPierManifest.Item item in model.items)
            {
                if (item.collider == null || !item.collider.StartsWith("box", System.StringComparison.Ordinal)) continue;
                Vector3 min = HarbourPierManifest.ToVector(item.bounds_min), max = HarbourPierManifest.ToVector(item.bounds_max);
                AddBox(holder.transform, item.name + "_" + (i++).ToString("00"), (min + max) * 0.5f, max - min, Quaternion.identity);
            }
        }

        // Shore: the land shape, the backdrop, the river (water material; Shore, never Water; no collider), the stone wall boxes, the dune collision.
        // Round 1: local core models under Land (the windmill sails) are instanced per placement, visual only, and turn (AmbientLife Rotor).
        static void AddLand(ArtAssets art, LifeBuild life)
        {
            GameObject root = AddWorldModels(art, HarbourPierLayout.RootLand, Layers.Shore);
            foreach (string name in HarbourPierLayout.LandBoxModels) AddBoxItems(root.transform, HarbourPierManifest.Find(art.Manifest, name));
            foreach (HarbourPierLayout.VisualModel visual in HarbourPierLayout.Visuals)
            {
                if (visual.Parent != HarbourPierLayout.RootLand) continue;
                HarbourPierManifest.Model model = HarbourPierManifest.Find(art.Manifest, visual.MeshName);
                if (!model.local_space) continue;
                if (visual.MeshName != HarbourPierLayout.WindmillSailsModel) throw new System.InvalidOperationException("no placement rule for the local Land model " + visual.MeshName);
                foreach (HarbourPierManifest.Placement p in model.placements)
                {
                    var go = new GameObject(p.name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(HarbourPierManifest.ToVector(p.position), Quaternion.Euler(0f, p.yaw_deg, 0f));
                    AddMesh(go, VisualMesh(art, visual.MeshName), MaterialFor(visual, art), true);
                    life.Move(go.transform, AmbientLife.MotionKind.Rotor, HarbourPierLayout.WindmillDegreesPerSecond);
                }
            }
            BuilderUtil.SetLayerRecursive(root, Layers.Shore);
        }

        // Structure: buildings, yards (the gangway and its rails included), the village trees (trunk boxes).
        static void AddVillage(ArtAssets art)
        {
            GameObject root = AddWorldModels(art, HarbourPierLayout.RootVillage, Layers.Structure);
            foreach (string name in HarbourPierLayout.VillageBoxModels) AddBoxItems(root.transform, HarbourPierManifest.Find(art.Manifest, name));
            BuilderUtil.SetLayerRecursive(root, Layers.Structure);
        }

        // The crew's tug (R76) on the breakwater's east face. Root at the placement (no rotation: the ladder volumes must have none); HullVisual bobs;
        // Collision (Shore), the ice-chest boxes (Structure) and the two LadderVolumes (Default, triggers) stay still on the root.
        static Transform AddCrewBoat(ArtAssets art, LifeBuild life)
        {
            HarbourPierManifest.Model boat = HarbourPierManifest.Find(art.Manifest, HarbourPierLayout.CrewBoatModel);
            if (boat.placements == null || boat.placements.Length != 1) throw new System.InvalidOperationException("CrewBoat: expected one placement");
            HarbourPierManifest.Placement at = boat.placements[0];
            if (at.yaw_deg != 0f) throw new System.InvalidOperationException("CrewBoat yaw must be 0 (LadderVolumes and their parents carry no rotation)");

            GameObject root = Root(HarbourPierLayout.RootCrewBoat, Layers.Structure);
            root.transform.position = HarbourPierManifest.ToVector(at.position);

            MeshRenderer hull = AddVisual(root.transform, "HullVisual", VisualMesh(art, HarbourPierLayout.CrewBoatModel), art.Toon, Quaternion.identity, true);
            hull.gameObject.layer = Layers.Structure;
            life.Move(hull.transform, AmbientLife.MotionKind.BoatBob, HarbourPierLayout.TugBobMetres);

            var col = new GameObject("Collision") { layer = Layers.Shore };
            col.transform.SetParent(root.transform, false);
            col.AddComponent<MeshCollider>().sharedMesh = VisualMesh(art, HarbourPierLayout.CrewBoatColModel);

            HarbourPierManifest.Model chest = HarbourPierManifest.Find(art.Manifest, HarbourPierLayout.IceChestModel);
            Mesh chestMesh = VisualMesh(art, HarbourPierLayout.IceChestModel);
            foreach (HarbourPierManifest.Placement p in chest.placements)
            {
                Vector3 local = HarbourPierManifest.ToVector(p.position);
                Quaternion yaw = Quaternion.Euler(0f, p.yaw_deg, 0f);
                MeshRenderer visual = AddVisual(hull.transform, p.name, chestMesh, art.Toon, yaw, true);
                visual.transform.localPosition = local;
                visual.gameObject.layer = Layers.Structure;
                Vector3 size = HarbourPierManifest.ToVector(p.size);
                var box = new GameObject(p.name + "_Collider") { layer = Layers.Structure };
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = local + new Vector3(0f, size.y * 0.5f, 0f);
                box.transform.localRotation = yaw;
                box.AddComponent<BoxCollider>().size = size;
            }

            foreach (HarbourPierManifest.Ladder ladder in boat.ladders)
            {
                Vector3 min = HarbourPierManifest.ToVector(ladder.volume_min), max = HarbourPierManifest.ToVector(ladder.volume_max);
                var go = new GameObject(ladder.name) { layer = Layers.Default };
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = (min + max) * 0.5f;
                BoxCollider trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = max - min;
                go.AddComponent<LadderVolume>();
            }
            return hull.transform;
        }

        // Every "detail" model under Detail (Default layer, no collider), hidden on Low. Local models: one instance per placement; a placement on the
        // tug (inside its hull's footprint) rides on the hull visual and is listed as detail on its own.
        static void AddDetail(ArtAssets art, LifeBuild life, Transform hull)
        {
            GameObject root = Root(HarbourPierLayout.RootDetail, Layers.Default);
            life.Detail.Add(root);
            HarbourGroundCover.AddTo(root.transform, art.Foliage);   // round 2b: instanced grass and flowers (hidden on Low with the root)
            HarbourPierManifest.Model boat = HarbourPierManifest.Find(art.Manifest, HarbourPierLayout.CrewBoatModel);
            Vector3 tugMin = hull.position + HarbourPierManifest.ToVector(boat.bounds_min), tugMax = hull.position + HarbourPierManifest.ToVector(boat.bounds_max);

            foreach (HarbourPierLayout.VisualModel visual in HarbourPierLayout.Visuals)
            {
                if (visual.Parent != HarbourPierLayout.RootDetail) continue;
                HarbourPierManifest.Model model = HarbourPierManifest.Find(art.Manifest, visual.MeshName);
                if (!HarbourPierManifest.IsDetail(model)) throw new System.InvalidOperationException(visual.MeshName + " is under Detail but its group is " + model.group);
                Mesh mesh = VisualMesh(art, visual.MeshName);
                Material material = MaterialFor(visual, art);
                if (!model.local_space)
                {
                    MeshRenderer r = AddVisual(root.transform, visual.Name, mesh, material, Quaternion.identity, CastsShadows(visual.MeshName));
                    if (visual.MeshName == HarbourPierLayout.LightsModel)
                    {
                        MeshRenderer glow = AddVisual(r.transform, "StringLightsGlow", mesh, art.Glow, Quaternion.identity, false);
                        glow.receiveShadows = false;
                        life.Glows.Add(glow);
                    }
                    continue;
                }

                foreach (HarbourPierManifest.Placement p in model.placements)
                {
                    Vector3 pos = HarbourPierManifest.ToVector(p.position);
                    bool onTug = pos.x >= tugMin.x && pos.x <= tugMax.x && pos.z >= tugMin.z && pos.z <= tugMax.z;
                    var go = new GameObject(p.name);
                    go.transform.SetParent(onTug ? hull : root.transform, false);
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, p.yaw_deg, 0f));
                    if (onTug) life.Detail.Add(go);

                    if (visual.MeshName == HarbourPierLayout.WeatherVaneModel)
                    {
                        AddMesh(go, art.VaneBase, material, true);
                        var head = new GameObject("Head");
                        head.transform.SetParent(go.transform, false);
                        AddMesh(head, art.VaneHead, material, true);
                        life.Move(head.transform, AmbientLife.MotionKind.Spin, 12f);
                        continue;
                    }
                    AddMesh(go, mesh, material, CastsShadows(visual.MeshName));
                    switch (visual.Name)
                    {
                        case "Gull": life.Move(go.transform, AmbientLife.MotionKind.Gull, 35f); break;
                        case "Float": life.Move(go.transform, AmbientLife.MotionKind.Bob, 0.05f); break;
                        case "Flag": life.Move(go.transform, AmbientLife.MotionKind.Flag, 9f); break;
                        case "WindSock": life.Move(go.transform, AmbientLife.MotionKind.WindSock, 14f); break;
                        case "SwingSign": life.Move(go.transform, AmbientLife.MotionKind.Swing, 7f); break;
                        case "HangingNet": life.Move(go.transform, AmbientLife.MotionKind.Sway, 3.5f, Vector3.right); break;
                        case "Bunting": life.Move(go.transform, AmbientLife.MotionKind.Sway, 7f, PrincipalAxisXZ(mesh)); break;
                        case "GullFly": life.Move(go.transform, AmbientLife.MotionKind.Circle, p.radius > 0f ? p.radius : 6f); break;
                        // round 2: the derrick's swinging load, ducks, crabs and cats (the perched-gull look-about), the washing line
                        case "CargoNet": life.Move(go.transform, AmbientLife.MotionKind.Sway, 3f, Vector3.right); break;
                        case "Duck": life.Move(go.transform, AmbientLife.MotionKind.Bob, 0.03f); break;
                        case "Crab": life.Move(go.transform, AmbientLife.MotionKind.Gull, 50f); break;
                        case "Cat": life.Move(go.transform, AmbientLife.MotionKind.Gull, 30f); break;
                        case "Laundry": life.Move(go.transform, AmbientLife.MotionKind.Sway, 9f, PrincipalAxisXZ(mesh)); break;
                        default: throw new System.InvalidOperationException("no motion for the local detail model " + visual.MeshName);
                    }
                }
            }
        }

        static MeshRenderer AddMesh(GameObject go, Mesh mesh, Material material, bool castShadows)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }

        // The bunting's string direction (a unit vector in the xz plane, local): the principal axis of its vertices (2 x 2 covariance).
        static Vector3 PrincipalAxisXZ(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            double mx = 0, mz = 0;
            foreach (Vector3 p in v) { mx += p.x; mz += p.z; }
            mx /= v.Length; mz /= v.Length;
            double sxx = 0, sxz = 0, szz = 0;
            foreach (Vector3 p in v) { double dx = p.x - mx, dz = p.z - mz; sxx += dx * dx; sxz += dx * dz; szz += dz * dz; }
            double angle = 0.5 * System.Math.Atan2(2 * sxz, sxx - szz);
            return new Vector3((float)System.Math.Cos(angle), 0f, (float)System.Math.Sin(angle)).normalized;
        }

        // The weather vane split at local y 0.25 (the manifest note: only the upper part turns): head = triangles wholly above, base = the rest.
        // Saved in place as Harbour_WeatherVane_Head / _Base (GUIDs kept). Deterministic.
        static void BuildWeatherVaneMeshes(Mesh vane)
        {
            Vector3[] v = vane.vertices;
            int[] tris = vane.triangles;
            var head = new List<int>();
            var lower = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                bool up = v[tris[i]].y > HarbourPierLayout.WeatherVaneSplitY && v[tris[i + 1]].y > HarbourPierLayout.WeatherVaneSplitY
                          && v[tris[i + 2]].y > HarbourPierLayout.WeatherVaneSplitY;
                (up ? head : lower).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
            }
            if (head.Count == 0 || lower.Count == 0) throw new System.InvalidOperationException("weather vane split left an empty part");
            HarbourPierMeshes.Save(HarbourPierLayout.WeatherVaneHeadMeshPath, Subset(vane, head));
            HarbourPierMeshes.Save(HarbourPierLayout.WeatherVaneBaseMeshPath, Subset(vane, lower));
        }

        static Mesh Subset(Mesh source, List<int> triangles)
        {
            Mesh m = Object.Instantiate(source);
            m.subMeshCount = 1;
            m.SetTriangles(triangles, 0, true);
            return m;
        }

        // The lighthouse beam: two long thin cones back to back along +-z from the lamp (8 sides, 70 m, 0.3 -> 5 m radius), drawn additive.
        static Mesh BuildBeaconBeam() => BuildBeam("Harbour_BeaconBeam", 70f, 0.3f, 5f, true);

        // Round 3: the hill searchlight's beam - one cone along +z (8 sides, 46 m, 0.22 -> 3.2 m radius).
        static Mesh BuildSearchBeam() => BuildBeam("Harbour_SearchBeam", 46f, 0.22f, 3.2f, false);

        static Mesh BuildBeam(string name, float length, float r0, float r1, bool both)
        {
            const int sides = 8;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            foreach (float dir in both ? new[] { 1f, -1f } : new[] { 1f })
            {
                int b = verts.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    var n = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    verts.Add(n * r0); normals.Add(n);
                    verts.Add(n * r1 + new Vector3(0f, 0f, length * dir)); normals.Add(n);
                }
                for (int i = 0; i < sides; i++)
                {
                    int a0 = b + i * 2, a1 = b + ((i + 1) % sides) * 2;
                    if (dir > 0f) tris.AddRange(new[] { a0, a1, a0 + 1, a1, a1 + 1, a0 + 1 });
                    else tris.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Seven light smoke columns (manifest pass2.smoke_points): chimneys, the office stove pipe, the campfire wisp and the tug's smokestack
        // (CrewBoat local, rides on the hull). URP particles on M_Smoke, world space, a small fixed seed each, drifting downwind.
        static void AddSmoke(ArtAssets art, LifeBuild life, Transform hull)
        {
            GameObject root = Root(HarbourPierLayout.RootSmoke, Layers.Default);
            HarbourPierManifest.Pass2 pass2 = HarbourPierManifest.LoadPass2();
            for (int i = 0; i < pass2.smoke_points.Length; i++)
            {
                HarbourPierManifest.Point sp = pass2.smoke_points[i];
                bool onTug = sp.frame == "CrewBoat local";
                var go = new GameObject("Smoke_" + sp.name + "_" + i);
                go.transform.SetParent(onTug ? hull : root.transform, false);
                go.transform.localPosition = HarbourPierManifest.ToVector(sp.position);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // the cone's +z points up
                bool wisp = sp.name.StartsWith("Campfire", System.StringComparison.Ordinal);
                bool far = go.transform.position.magnitude > 80f;
                life.Smoke.Add(AddSmokeSystem(go, art.Smoke, (uint)(1001 + i), wisp ? 0.45f : (far ? 1.8f : 1f), wisp ? 0.6f : 1f));
            }
        }

        static ParticleSystem AddSmokeSystem(GameObject go, Material material, uint seed, float scale, float rate)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed;
            ParticleSystem.MainModule main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f * scale, 0.8f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.93f, 0.93f, 0.95f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 3f * rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.08f * scale;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.35f * scale);   // drift downwind (the windsock streams toward +x, -z)
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.45f * scale, -0.2f * scale);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 2.6f)));
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.86f, 0.86f, 0.88f), 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        // Lamps (warm point lights + a glow sphere, on at dusk): the five pass-2 lamp posts, the two phase-1 pier lamps, the tug's cabin light;
        // the tug's blinking mast light; the lighthouse beacon (a glow sphere and a sweeping beam). No shadows.
        static void AddHarbourLights(ArtAssets art, LifeBuild life, Transform hull)
        {
            GameObject root = Root(HarbourPierLayout.RootLights, Layers.Default);
            // Every pass-2 lamp post (round 0 the village's Yard_A; round 1 also the upper lane and the jetty head), in manifest order.
            foreach (HarbourPierManifest.Model model in art.Manifest)
            {
                if (model.group == null || model.items == null) continue;
                foreach (HarbourPierManifest.Item item in model.items)
                    if (item.light != null && item.light.Length == 3)
                    {
                        Light lamp = AddLamp(root.transform, art, life, "Lamp_" + item.name, HarbourPierManifest.ToVector(item.light), 7f, 1.6f, 0.26f);
                        if (item.light_colour == "green") { lamp.color = new Color(0.45f, 1f, 0.5f); lamp.range = 9f; }   // round 2: the mole's harbour light
                    }
            }
            foreach (HarbourPierManifest.Item item in HarbourPierManifest.Find(art.Manifest, "Harbour_Props").items)
            {
                if (!item.name.StartsWith("Lamp_", System.StringComparison.Ordinal)) continue;
                Vector3 min = HarbourPierManifest.ToVector(item.bounds_min), max = HarbourPierManifest.ToVector(item.bounds_max);
                AddLamp(root.transform, art, life, "PierLamp_" + item.name, new Vector3((min.x + max.x) * 0.5f, max.y - HarbourPierLayout.PierLampLightBelowTop, (min.z + max.z) * 0.5f), 7f, 1.6f, 0.26f);
            }

            HarbourPierManifest.Model boat = HarbourPierManifest.Find(art.Manifest, HarbourPierLayout.CrewBoatModel);
            if (boat.interior_light != null && boat.interior_light.Length == 3)
            {
                Light cabin = AddLamp(hull, art, life, "CabinLight", Vector3.zero, 4.5f, 1.3f, 0f);
                cabin.transform.localPosition = HarbourPierManifest.ToVector(boat.interior_light);
            }
            foreach (HarbourPierManifest.Point lp in boat.light_points)
            {
                var go = new GameObject(lp.name);
                go.transform.SetParent(hull, false);
                go.transform.localPosition = HarbourPierManifest.ToVector(lp.position);
                life.Blink = AddPointLight(go, 5f, 1.8f, new Color(1f, 0.93f, 0.8f));
                life.BlinkGlow = AddGlowSphere(go.transform, art, 0.2f);
            }

            foreach (HarbourPierManifest.Point lp in HarbourPierManifest.LoadPass2().light_points)
            {
                if (lp.frame == "CrewBoat local") continue;   // the mast light, above
                if (lp.name == "Searchlight")
                {
                    // Round 3: the mafia sedan's searchlight on the hill track. A pivot turned toward the harbour (yaw_deg) that wanders
                    // slowly (AmbientLife WindSock: +-amplitude degrees of yaw), a one-sided beam tipped down over the town, a small glow at
                    // the lamp; both glow only at dusk and night (life.Glows). No point light: it is a backdrop beat, not a light source.
                    var pivot = new GameObject("Searchlight");
                    pivot.transform.SetParent(root.transform, false);
                    pivot.transform.localPosition = HarbourPierManifest.ToVector(lp.position);
                    pivot.transform.localRotation = Quaternion.Euler(0f, lp.yaw_deg, 0f);
                    life.Move(pivot.transform, AmbientLife.MotionKind.WindSock, 26f);
                    life.Glows.Add(AddGlowSphere(pivot.transform, art, 0.5f));
                    var sweep = new GameObject("Beam");
                    sweep.transform.SetParent(pivot.transform, false);
                    sweep.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
                    MeshRenderer sr = AddMesh(sweep, art.SearchBeamMesh, art.Beam, false);
                    sr.receiveShadows = false;
                    life.Glows.Add(sr);
                    continue;
                }
                var go = new GameObject("Beacon_" + lp.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = HarbourPierManifest.ToVector(lp.position);
                life.Beacon = AddPointLight(go, 12f, 3f, new Color(1f, 0.9f, 0.7f));
                life.Glows.Add(AddGlowSphere(go.transform, art, 1.1f));
                var beam = new GameObject("Beam");
                beam.transform.SetParent(go.transform, false);
                MeshRenderer r = AddMesh(beam, art.BeamMesh, art.Beam, false);
                r.receiveShadows = false;
                life.Glows.Add(r);
            }
        }

        static Light AddLamp(Transform parent, ArtAssets art, LifeBuild life, string name, Vector3 position, float range, float intensity, float glowDiameter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = AddPointLight(go, range, intensity, new Color(1f, 0.76f, 0.48f));
            life.Lamps.Add(light);
            if (glowDiameter > 0f) life.Glows.Add(AddGlowSphere(go.transform, art, glowDiameter));
            return light;
        }

        static Light AddPointLight(GameObject go, float range, float intensity, Color colour)
        {
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = colour;
            light.shadows = LightShadows.None;
            light.enabled = false;   // AmbientLife switches it on at dusk
            return light;
        }

        static MeshRenderer AddGlowSphere(Transform parent, ArtAssets art, float diameter)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;
            MeshRenderer r = AddMesh(go, art.Sphere, art.GlowAll, false);
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }
    }
}
