// Editor/Builders/Ecology/ShoreGridBaker.cs - spec 12.5 bake, rulings M3-D6/D7/D8. Authority: none (editor; writes the grid asset only).
// One loaded scene with a WaterVolume at a time: two maps put a 120 m water box at the origin of the same physics scene (E-CCR-5, M3-D86).
// Per cell centre a down ray from y 30 over 80 m against Water | Shore | Ground with QueryTriggerInteraction.Collide; nearest hit Water ->
// water, Shore or Ground -> land, no hit -> void (neither water nor a distance seed). Castable = water AND the cast gate itself
// (C 2.8 #26: CastMath.ProbeReason(CastProbe.Probe(centre), minLandDepth) == None). Shore decimetres and reachable bits come from
// ShoreGridMath (Core). Deterministic: no Random, no time, no hash containers, so two bakes of one scene write identical arrays.
using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class ShoreGridBaker
    {
        // E0.6 grid constants (both maps, M3-D8): 120 x 120 cells of 1 m, origin (-60, -60).
        private const int GridWidth = 120, GridHeight = 120;
        private const float GridCellSizeM = EcologyConstants.GridCellSizeM;
        private const float GridOriginX = -60f, GridOriginZ = -60f;
        private const float RayTopY = 30f, RayLength = 80f;

        public static void BakeOpenScene(Scene scene, ShoreGridDef target, float minLandDepth)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("scene is not loaded", nameof(scene));
            if (string.IsNullOrEmpty(scene.name)) throw new ArgumentException("save the scene before the bake (bakedFromScene records its name)", nameof(scene));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!(minLandDepth > 0f) || float.IsInfinity(minLandDepth)) throw new ArgumentOutOfRangeException(nameof(minLandDepth));
            RequireNoOtherWaterScene(scene);
            Physics.SyncTransforms();

            const int w = GridWidth, h = GridHeight;
            int n = w * h;
            var water = new bool[n];
            var land = new bool[n];
            var castable = new bool[n];
            int mask = Layers.Mask(Layers.Water) | Layers.Mask(Layers.Shore) | Layers.Mask(Layers.Ground);
            var hits = new RaycastHit[64];
            int voids = 0;

            for (int cz = 0; cz < h; cz++)
            {
                float z = GridOriginZ + (cz + 0.5f) * GridCellSizeM;
                for (int cx = 0; cx < w; cx++)
                {
                    float x = GridOriginX + (cx + 0.5f) * GridCellSizeM;
                    int i = cz * w + cx;
                    int count = Physics.RaycastNonAlloc(new Ray(new Vector3(x, RayTopY, z), Vector3.down), hits, RayLength, mask,
                                                        QueryTriggerInteraction.Collide);
                    if (count >= hits.Length) throw new InvalidOperationException("bake ray buffer full at cell " + cx + "," + cz);
                    int best = -1;
                    for (int k = 0; k < count; k++)
                    {
                        if (best < 0 || hits[k].distance < hits[best].distance) { best = k; continue; }
                        // exact tie: a non-Water hit wins (land), so the result never depends on the unsorted hit order
                        if (hits[k].distance == hits[best].distance && hits[best].collider.gameObject.layer == Layers.Water
                            && hits[k].collider.gameObject.layer != Layers.Water) best = k;
                    }
                    if (best < 0) { voids++; continue; }                                   // void: no water, no distance seed (M3-D7)
                    if (hits[best].collider.gameObject.layer == Layers.Water)
                    {
                        water[i] = true;
                        // C 2.8 #26: the cast gate itself (owner marker and host), at the cell centre.
                        castable[i] = CastMath.ProbeReason(CastProbe.Probe(x, z), minLandDepth) == CastRejectReason.None;
                    }
                    else
                    {
                        land[i] = true;                                                     // Shore or Ground above the water
                    }
                }
            }

            ushort[] shoreDm = ShoreGridMath.ShoreDecimetres(water, land, w, h, GridCellSizeM);
            byte[] castableBits = ShoreGridMath.PackBits(castable);
            StandPoint[] stands = CollectStandPoints(scene);
            var standXZ = new List<float>(stands.Length * 2);
            var standPositions = new Vector3[stands.Length];
            for (int s = 0; s < stands.Length; s++)
            {
                Vector3 p = stands[s].transform.position;
                standPositions[s] = p;
                standXZ.Add(p.x);
                standXZ.Add(p.z);
            }
            byte[] reachableBits = ShoreGridMath.ReachableBits(w, h, GridCellSizeM, GridOriginX, GridOriginZ, castableBits, standXZ,
                                                               EcologyConstants.ReachMinM, EcologyConstants.ReachMaxM);

            target.WriteBake(w, h, GridCellSizeM, GridOriginX, GridOriginZ, shoreDm, castableBits, reachableBits, standPositions, scene.name);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            LogBake(scene, target, water, land, voids, castable, reachableBits, stands.Length);
        }

        // E-CCR-5: offers to save modified scenes, opens each registered Spot scene ALONE (Single), bakes it, restores the setup. Never saves a scene.
        [MenuItem("FishGame/Ecology/Bake Shore Grids")]
        public static void BakeAllMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
                if (registry == null) { Debug.LogError("FG BAKE no ContentRegistry at " + AssetPaths.ContentRegistryAsset); return; }
                float minLandDepth = registry.FishingTuning.MinLandDepth;
                foreach (SpotDef spot in registry.Spots)
                {
                    if (spot == null) continue;
                    ShoreGridDef grid = spot.Zone != null ? spot.Zone.ShoreGrid : null;
                    if (grid == null) { Debug.LogError("FG BAKE " + spot.Id + " has no zone shore grid; skipped"); continue; }
                    Scene scene = EditorSceneManager.OpenScene(AssetPaths.ScenesFolder + "/" + spot.SceneName + ".unity", OpenSceneMode.Single);
                    BakeOpenScene(scene, grid, minLandDepth);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void RequireNoOtherWaterScene(Scene scene)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene other = SceneManager.GetSceneAt(s);
                if (other == scene || !other.isLoaded) continue;
                foreach (GameObject root in other.GetRootGameObjects())
                {
                    if (root.GetComponentsInChildren<WaterVolume>(true).Length > 0)
                        throw new InvalidOperationException("another loaded scene holds a WaterVolume (" + other.name + "); bake one Spot scene at a time");
                }
            }
        }

        // Every StandPoint of the scene in hierarchy order (all roots, inactive included).
        private static StandPoint[] CollectStandPoints(Scene scene)
        {
            var list = new List<StandPoint>();
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<StandPoint>(true));
            if (list.Count == 0) throw new InvalidOperationException("no StandPoint in " + scene.name);
            return list.ToArray();
        }

        // The FG BAKE info line (never a warning). Sub-area counts through the scene's registered spot zone, else "n/a".
        private static void LogBake(Scene scene, ShoreGridDef target, bool[] water, bool[] land, int voids, bool[] castable, byte[] reachableBits,
                                    int stands)
        {
            int waterCount = 0, landCount = 0, castableCount = 0, reachableCount = 0;
            for (int i = 0; i < water.Length; i++)
            {
                if (water[i]) waterCount++;
                if (land[i]) landCount++;
                if (castable[i]) castableCount++;
                if (ShoreGrid.GetBit(reachableBits, i)) reachableCount++;
            }
            string subAreas = "n/a";
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry != null && registry.TryGetSpotByScene(scene.name, out SpotDef spot) && spot.Zone != null)
            {
                ZoneData zone = spot.Zone.ToCore(registry);
                int[] counts = ShoreGridMath.CountReachablePerSubArea(target.ToCore(), zone);
                var sb = new System.Text.StringBuilder();
                for (int s = 0; s < counts.Length; s++)
                {
                    if (s > 0) sb.Append(',');
                    sb.Append(spot.Zone.SubAreas[s].id).Append(':').Append(counts[s].ToString(CultureInfo.InvariantCulture));
                }
                subAreas = sb.ToString();
            }
            Debug.Log("FG BAKE " + scene.name + " water=" + waterCount + " land=" + landCount + " void=" + voids + " castable=" + castableCount
                      + " reachable=" + reachableCount + " stands=" + stands + " subAreas=" + subAreas);
        }
    }
}
