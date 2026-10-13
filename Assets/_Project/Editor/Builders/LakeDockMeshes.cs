// Meshes of Spot_LakeDock (contract 6.4 / 7.28, plan T28). Owned by T15 (lakebed, shore), T28 (everything else). Authority: none (editor).
// Radial height-field meshes for the lakebed and shore (positions and triangles are the M1 physics numbers; T28 adds per-face vertex colours and uv1 palette slots)
// plus the MeshBuilder props: water grid, rock, trunk, two canopies, the combined dock visual and the combined reeds.
// Every mesh is saved IN PLACE (EditorUtility.CopySerialized into the existing asset), so GUIDs never change and a rebuild is byte-identical:
// fixed mesh seeds, fixed draw order, the MeshBuilder default face jitter (never ArtPaletteDef.faceJitter), new random streams only.
// R74 look-dev (2026-10-12): the dock, rock, trunk and canopies are SmoothMeshBuilder meshes (rounded, smooth normals, uv0 in metres, texture layer in
// uv1.y, baked ao in the vertex colour); the lakebed and shore get smooth VISUAL twins while their collider meshes stay byte-identical. The water
// grid and the reeds are unchanged.
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    internal static class LakeDockMeshes
    {
        public const int Sectors = 48;

        // Builds and saves every lake dock mesh asset (steps 1 of LakeDockBuilder.Build, before NewScene).
        public static void BuildAll()
        {
            BuildRadialAsset(AssetPaths.LakebedMesh, LakeDockLayout.LakebedRadii, LakeDockLayout.BedY, LakeDockLayout.LakebedMeshSeed, y => LakeDockLayout.SlotSand);
            BuildRadialAsset(AssetPaths.ShoreMesh, LakeDockLayout.ShoreRadii, LakeDockLayout.ShoreY, LakeDockLayout.ShoreMeshSeed,
                y => y < LakeDockLayout.ShoreSandMaxY ? LakeDockLayout.SlotSand : LakeDockLayout.SlotGrass);
            Save(LakeDockLayout.LakebedVisualMeshPath, FillRadialSmooth(LakeDockLayout.LakebedRadii, LakeDockLayout.BedY,
                y => LakeDockLayout.SlotSand));
            Save(LakeDockLayout.ShoreVisualMeshPath, FillRadialSmooth(LakeDockLayout.ShoreRadii, LakeDockLayout.ShoreY,
                y => y < LakeDockLayout.ShoreSandMaxY ? LakeDockLayout.SlotSand : LakeDockLayout.SlotGrass));
            Save(LakeDockLayout.WaterMeshPath, MeshBuilder.Grid(LakeDockLayout.WaterGridCells, LakeDockLayout.WaterGridCells, LakeDockLayout.WaterCellSize, "LakeDock_Water"));
            Save(LakeDockLayout.DockMeshPath, BuildDock());
            Save(LakeDockLayout.RockMeshPath, BuildRock());
            Save(LakeDockLayout.TrunkMeshPath, BuildTrunk());
            Save(LakeDockLayout.CanopyRoundMeshPath, BuildCanopyRound());
            Save(LakeDockLayout.CanopyPineMeshPath, BuildCanopyPine());
            Save(LakeDockLayout.ReedsMeshPath, BuildReeds());
            AssetDatabase.SaveAssets();   // meshes are on disk before the scene references them
        }

        // ---- save helper ----

        // Copies the built mesh into the existing asset (GUID kept) or creates the asset. The temporary mesh is destroyed.
        static Mesh Save(string assetPath, Mesh built)
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(assetPath));
            string name = Path.GetFileNameWithoutExtension(assetPath);
            built.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, assetPath);
                return built;
            }
            EditorUtility.CopySerialized(built, existing);
            existing.name = name;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---- lakebed and shore ----

        // radii must be ascending; radii[0] == 0 makes a centre fan (disk), otherwise the mesh is a ring.
        // slotAtCentreY maps a face's centre height to its palette slot (uv1.x).
        static void BuildRadialAsset(string assetPath, float[] radii, System.Func<float, float> heightAtRadius, uint meshSeed, System.Func<float, byte> slotAtCentreY)
        {
            Save(assetPath, FillRadial(radii, heightAtRadius, meshSeed, slotAtCentreY));
        }

        static Mesh FillRadial(float[] radii, System.Func<float, float> heightAtRadius, uint meshSeed, System.Func<float, byte> slotAtCentreY)
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

            // One face = one triangle (unshared vertices). Half-scale grey (BF-7, the same as MeshBuilder) and the palette slot on all three vertices.
            var colors = new List<Color>(vertices.Count);
            var uv1 = new List<Vector2>(vertices.Count);
            for (int face = 0; face < vertices.Count / 3; face++)
            {
                float g = 0.5f * FaceJitter.Grey(meshSeed, face, 0.04f);
                float centreY = (vertices[face * 3].y + vertices[face * 3 + 1].y + vertices[face * 3 + 2].y) / 3f;
                byte slot = slotAtCentreY(centreY);
                for (int k = 0; k < 3; k++)
                {
                    colors.Add(new Color(g, g, g, 1f));
                    uv1.Add(new Vector2(slot, 0f));
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.SetColors(colors);
            mesh.SetUVs(1, uv1);
            mesh.RecalculateBounds();
            return mesh;
        }

        // R74 visual twin of FillRadial. The adoption sweep (2026-10-13) found the 48-sector rim reading as straight segments (shoreline, foam,
        // sand/grass edge), so the twin is finer than the collider: VisualSectors around (4x) and each collider band split into RingSubdivisions
        // with heights LINEARLY interpolated between the collider rings (so it lies on the collider surface radially; on the arc between collider
        // sectors it sits at most ~3 cm off on the steepest shore slope). Each band shares its vertices across sectors; normals come from the
        // radial slope (central difference of heightAtRadius over +-1 m). The palette slot is PER VERTEX (sand below ShoreSandMaxY, grass above),
        // and FishGame/Toon resolves the palette colour per vertex, so the band that crosses the edge blends sand into grass; the texture layer is per
        // band (its mid height) and neighbouring bands duplicate the ring. UV0 = world xz (metres). Vertex colour 0.5 (no ao; BF-7 half scale).
        const int VisualSectors = Sectors * 4, RingSubdivisions = 2;

        static Mesh FillRadialSmooth(float[] radii, System.Func<float, float> heightAtRadius, System.Func<float, byte> slotAtY)
        {
            var b = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int ring = 0; ring + 1 < radii.Length; ring++)
            {
                float ra = radii[ring], rb = radii[ring + 1];
                float ya = heightAtRadius(ra), yb = heightAtRadius(rb);
                for (int sub = 0; sub < RingSubdivisions; sub++)
                {
                    float t0 = sub / (float)RingSubdivisions, t1 = (sub + 1) / (float)RingSubdivisions;
                    float r0 = Mathf.Lerp(ra, rb, t0), r1 = Mathf.Lerp(ra, rb, t1);
                    float y0 = Mathf.Lerp(ya, yb, t0), y1 = Mathf.Lerp(ya, yb, t1);
                    byte bandSlot = slotAtY((y0 + y1) * 0.5f);
                    byte layer = bandSlot == LakeDockLayout.SlotGrass ? EnvTextureLayers.Grass : EnvTextureLayers.Sand;
                    int start = b.Count;
                    for (int k = 0; k < 2; k++)
                    {
                        float r = k == 0 ? r0 : r1, y = k == 0 ? y0 : y1;
                        float slope = (heightAtRadius(r + 1f) - heightAtRadius(Mathf.Max(0f, r - 1f))) / (r + 1f - Mathf.Max(0f, r - 1f));
                        byte slot = slotAtY(y);
                        for (int s = 0; s < VisualSectors; s++)
                        {
                            float angle = s * (Mathf.PI * 2f / VisualSectors);
                            Vector3 p = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                            Vector3 radial = r > 1e-4f ? new Vector3(p.x, 0f, p.z) / r : Vector3.zero;
                            b.Add(p);
                            normals.Add((Vector3.up - radial * slope).normalized);
                            uv0.Add(new Vector2(p.x, p.z));
                            uv1.Add(new Vector4(slot, layer, SmoothMeshBuilder.MappingUv, 0f));
                            colors.Add(new Color(0.5f, 0.5f, 0.5f, 1f));
                        }
                    }
                    for (int s = 0; s < VisualSectors; s++)
                    {
                        int a0 = start + s, b0 = start + (s + 1) % VisualSectors, a1 = start + VisualSectors + s, b1 = start + VisualSectors + (s + 1) % VisualSectors;
                        AddIndexedUp(b, triangles, a0, a1, b1);
                        if (r0 > 0f) AddIndexedUp(b, triangles, a0, b1, b0);
                    }
                }
            }
            var mesh = new Mesh();
            mesh.indexFormat = b.Count <= 65535 ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(b);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Same rule as AddTriangle: the winding is flipped when needed so the face normal points up (+y).
        static void AddIndexedUp(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).y < 0f) { int swap = b; b = c; c = swap; }
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
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

        // ---- props (unit sizes match the Unity primitives the M1 transforms were authored for) ----

        // R74: smooth props (SmoothMeshBuilder) inside the same unit volumes as the M1 primitives, so the sphere/capsule colliders still bound them.

        // A soft boulder inside the unit sphere: lumpy radius 0.40..0.5, flattened underneath, darker toward the bottom. Rock layer, world triplanar.
        static Mesh BuildRock()
        {
            var b = new SmoothMeshBuilder();
            uint seed = LakeDockLayout.RockMeshSeed;
            b.Radial(Vector3.zero, d =>
            {
                float r = 0.43f + 0.065f * SmoothMeshBuilder.Noise3(d * 1.3f + new Vector3(5.1f, 1.7f, 2.3f), seed)
                        + 0.018f * SmoothMeshBuilder.Noise3(d * 3.7f, seed + 1u);
                // A chunkier, faceted-but-soft silhouette: push the radius toward a rounded box (planes with soft edges).
                float box = 1f / Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y) * 1.15f, Mathf.Abs(d.z)));
                r *= Mathf.Lerp(1f, Mathf.Min(box * 0.62f, 1.12f), 0.45f);
                if (d.y < -0.35f) r *= Mathf.Lerp(1f, 0.84f, Mathf.InverseLerp(-0.35f, -1f, d.y));
                return Mathf.Min(r, 0.5f);
            }, 6, Quaternion.identity, Vector3.one, LakeDockLayout.SlotRock, EnvTextureLayers.Rock, SmoothMeshBuilder.MappingTriplanar,
                d => 0.62f + 0.38f * Mathf.SmoothStep(0f, 1f, d.y * 0.5f + 0.5f), 0.5f);
            return b.ToMesh("LakeDock_Rock");
        }

        // Unity cylinder primitive volume (radius 0.5, height 2, centred): a tapering trunk with a root flare, bark ridges running up (uv mapping),
        // darker at the foot.
        static Mesh BuildTrunk()
        {
            var b = new SmoothMeshBuilder();
            b.Lathe(Vector3.zero, t => Mathf.Lerp(LakeDockLayout.TrunkRadiusBottom, LakeDockLayout.TrunkRadiusTop, t) * 0.86f + 0.07f * Mathf.Pow(1f - t, 6f),
                LakeDockLayout.TrunkHeight, 14, 8, Quaternion.identity, LakeDockLayout.SlotWoodDark, EnvTextureLayers.Bark, SmoothMeshBuilder.MappingUv,
                t => 0.6f + 0.4f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.5f)), 0.45f);
            return b.ToMesh("LakeDock_Trunk");
        }

        // A lumpy foliage ball inside the unit sphere (radius <= 0.5), darker underneath (ao), foliage clumps triplanar in world space.
        static Mesh BuildCanopyRound()
        {
            var b = new SmoothMeshBuilder();
            uint seed = LakeDockLayout.CanopyRoundMeshSeed;
            b.Radial(Vector3.zero, d => Mathf.Min(0.5f, 0.43f + 0.06f * SmoothMeshBuilder.Noise3(d * 2.2f + Vector3.one * 3f, seed)),
                8, Quaternion.identity, Vector3.one, LakeDockLayout.SlotFoliage, EnvTextureLayers.Foliage, SmoothMeshBuilder.MappingTriplanar,
                d => 0.55f + 0.45f * Mathf.SmoothStep(0f, 1f, d.y * 0.5f + 0.5f), 0.5f);
            return b.ToMesh("LakeDock_CanopyRound");
        }

        // Two stacked cones inside the unit sphere (radius 0.5, the canopy collider): the lower dark, the upper light. The lower rim (r 0.3 at y -0.4)
        // keeps r^2 + y^2 <= 0.25, so the visual never overhangs the collider.
        // R74: the same two tiers as smooth lathes with a rounded, closed lower lip (radius 0 at t = 0) and a soft tip.
        static Mesh BuildCanopyPine()
        {
            var b = new SmoothMeshBuilder();
            System.Func<float, float> tier = t => 0.3f * Mathf.Pow(1f - t, 1.15f) * Mathf.Sqrt(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f)));
            b.Lathe(new Vector3(0f, -0.15f, 0f), tier, 0.5f, 16, 10, Quaternion.identity, LakeDockLayout.SlotFoliageDark, EnvTextureLayers.Foliage,
                SmoothMeshBuilder.MappingTriplanar, t => 0.55f + 0.45f * t, 0.3f);
            b.Lathe(new Vector3(0f, 0.225f, 0f), tier, 0.55f, 16, 10, Quaternion.identity, LakeDockLayout.SlotFoliage, EnvTextureLayers.Foliage,
                SmoothMeshBuilder.MappingTriplanar, t => 0.6f + 0.4f * t, 0.3f);
            return b.ToMesh("LakeDock_CanopyPine");
        }

        // ---- dock visual ----

        // One combined world-space mesh (the Dock root sits at the origin with identity rotation). Visual only: the M1 box colliders stay the physics.
        static Mesh BuildDock()
        {
            // R74: smooth rounded planks, beams and rails (SmoothMeshBuilder), wood grain along each plank, ao darker underneath. Same boxes,
            // same plank draws (stream and order), same positions; only the edges are rounded inside each box.
            var b = new SmoothMeshBuilder();
            Pcg32 planks = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.PlankStream, 0UL);

            // Deck planks: every top is exactly DeckTopY (the deck collider top); the yaw is about the vertical axis, so it never moves a top.
            float pitch = LakeDockLayout.PlankWidth + LakeDockLayout.PlankGap;
            float centreY = LakeDockLayout.DeckTopY - LakeDockLayout.DeckThickness * 0.5f;
            for (int i = 0; i < LakeDockLayout.PlankCount; i++)
            {
                float z = LakeDockLayout.DeckNearZ + pitch * (i + 0.5f);
                AddPlank(b, ref planks, new Vector3(0f, centreY, z), Quaternion.identity,
                    new Vector3(LakeDockLayout.PlankLength, LakeDockLayout.DeckThickness, LakeDockLayout.PlankWidth));
            }

            float deckUnderside = LakeDockLayout.DeckTopY - LakeDockLayout.DeckThickness;
            float deckLength = LakeDockLayout.DeckFarZ - LakeDockLayout.DeckNearZ;
            float stringerY = deckUnderside - LakeDockLayout.StringerSize * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                b.RoundedBox(new Vector3(LakeDockLayout.StringerX * side, stringerY, (LakeDockLayout.DeckNearZ + LakeDockLayout.DeckFarZ) * 0.5f),
                    new Vector3(LakeDockLayout.StringerSize, LakeDockLayout.StringerSize, deckLength), 0.025f, Quaternion.identity, LakeDockLayout.SlotWoodDark,
                    EnvTextureLayers.Wood, 0.35f);

            // Posts at the M1 post positions, from the bed to the deck underside.
            int postRows = Mathf.RoundToInt(deckLength / LakeDockLayout.PostSpacing) + 1;
            for (int i = 0; i < postRows; i++)
            {
                float z = LakeDockLayout.DeckNearZ + LakeDockLayout.PostSpacing * i;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = LakeDockLayout.PostX * side;
                    float bed = LakeDockLayout.BedYAt(x, z);
                    // Round post, grain running up; ao darker toward the bed and just under the deck.
                    b.Lathe(new Vector3(x, (deckUnderside + bed) * 0.5f, z), t => LakeDockLayout.PostRadius, deckUnderside - bed, 12, 6,
                        Quaternion.identity, LakeDockLayout.SlotWoodDark, EnvTextureLayers.Wood, SmoothMeshBuilder.MappingUv,
                        t => Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(t)) * (1f - 0.3f * Mathf.SmoothStep(0.8f, 1f, t)), LakeDockLayout.PostRadius, true);
                }
            }

            AddLadder(b, -LakeDockLayout.LadderX);
            AddLadder(b, LakeDockLayout.LadderX);

            // Ramp: planks along the RampDeck direction, centred on the collider mid-plane so their top is the collider top.
            Vector3 low = LakeDockLayout.RampLow, high = LakeDockLayout.RampHigh;
            Vector3 along = high - low;
            float rampLength = along.magnitude;
            Quaternion rampRotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            Vector3 rampCentre = (low + high) * 0.5f;
            int rampPlanks = Mathf.FloorToInt(rampLength / pitch);
            float rampPitch = rampLength / rampPlanks;
            for (int i = 0; i < rampPlanks; i++)
            {
                float local = -rampLength * 0.5f + rampPitch * (i + 0.5f);
                AddPlank(b, ref planks, rampCentre + rampRotation * new Vector3(0f, 0f, local), rampRotation,
                    new Vector3(LakeDockLayout.RampWidth, LakeDockLayout.RampThickness, rampPitch - LakeDockLayout.PlankGap));
            }

            if (b.TriangleCount > LakeDockLayout.MaxCombinedTriangles)
                throw new System.InvalidOperationException("LakeDock_Dock has " + b.TriangleCount + " triangles (limit " + LakeDockLayout.MaxCombinedTriangles + ")");
            return b.ToMesh("LakeDock_Dock");
        }

        // Draw order per plank (stream lake_dock.planks): slot (Wood or Dock), then yaw +-PlankYawDegrees. No height jitter.
        static void AddPlank(SmoothMeshBuilder b, ref Pcg32 rng, Vector3 centre, Quaternion baseRotation, Vector3 size)
        {
            byte slot = rng.NextFloat() < 0.5f ? LakeDockLayout.SlotWood : LakeDockLayout.SlotDock;
            float yaw = rng.Range(-LakeDockLayout.PlankYawDegrees, LakeDockLayout.PlankYawDegrees);
            b.RoundedBox(centre, size, 0.02f, baseRotation * Quaternion.Euler(0f, yaw, 0f), slot, EnvTextureLayers.Wood, 0.45f);
        }

        // Two rails and rungs every RungSpacing inside the M1 ladder collider volume (x +-0.03, z +-0.25 around LadderZ), ending at the deck top.
        static void AddLadder(SmoothMeshBuilder b, float x)
        {
            float bed = LakeDockLayout.BedYAt(x, LakeDockLayout.LadderZ);
            float top = LakeDockLayout.DeckTopY;
            float height = top - bed;
            for (int side = -1; side <= 1; side += 2)
                b.RoundedBox(new Vector3(x, (top + bed) * 0.5f, LakeDockLayout.LadderZ + LakeDockLayout.RailInset * side),
                    new Vector3(LakeDockLayout.RailSize, height, LakeDockLayout.RailSize), 0.018f, Quaternion.identity, LakeDockLayout.SlotWoodDark,
                    EnvTextureLayers.Wood, 0.3f);
            float rungSpan = LakeDockLayout.RailInset * 2f;
            for (int k = 1; ; k++)
            {
                float y = bed + LakeDockLayout.RungSpacing * k;
                if (y > top - LakeDockLayout.RungThickness) break;
                b.RoundedBox(new Vector3(x, y, LakeDockLayout.LadderZ), new Vector3(LakeDockLayout.RungDepth, LakeDockLayout.RungThickness, rungSpan),
                    0.012f, Quaternion.identity, LakeDockLayout.SlotWoodDark, EnvTextureLayers.Wood, 0.2f);
            }
        }

        // ---- reeds ----

        // One combined mesh. Draws (stream lake_dock.reeds): per attempt angle and radius (a blocked spot costs those two draws), then per placed cluster
        // the blade count, then per blade offset x, offset z, height, lean x, lean z.
        static Mesh BuildReeds()
        {
            // R74 adoption: smooth tapered blades (lathe, grass strokes running up, darker at the foot) and a cattail head on every third blade.
            // Same draws, same positions, heights and leans as the flat version; the cattails use no draws.
            var b = new SmoothMeshBuilder();
            Pcg32 rng = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.ReedStream, 0UL);
            int placed = 0;
            for (int attempt = 0; attempt < LakeDockLayout.MaxAttempts && placed < LakeDockLayout.ReedClusterCount; attempt++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float r = rng.Range(LakeDockLayout.ReedMinRadius, LakeDockLayout.ReedMaxRadius);
                float cx = Mathf.Cos(angle) * r, cz = Mathf.Sin(angle) * r;
                if (LakeDockLayout.Blocked(cx, cz, LakeDockLayout.ReedClearance)) continue;
                int blades = rng.Range(LakeDockLayout.ReedMinBlades, LakeDockLayout.ReedMaxBlades + 1);
                for (int k = 0; k < blades; k++)
                {
                    float bx = cx + rng.Range(-LakeDockLayout.ReedClusterSpread, LakeDockLayout.ReedClusterSpread);
                    float bz = cz + rng.Range(-LakeDockLayout.ReedClusterSpread, LakeDockLayout.ReedClusterSpread);
                    float height = rng.Range(LakeDockLayout.ReedMinHeight, LakeDockLayout.ReedMaxHeight);
                    float leanX = rng.Range(-LakeDockLayout.ReedMaxLeanDegrees, LakeDockLayout.ReedMaxLeanDegrees);
                    float leanZ = rng.Range(-LakeDockLayout.ReedMaxLeanDegrees, LakeDockLayout.ReedMaxLeanDegrees);
                    // The base sinks 0.1 m below the faceted lakebed so no gap shows; the tip is `height` above the bed.
                    float sink = 0.1f;
                    float length = height + sink;
                    Vector3 baseWorld = new Vector3(bx, LakeDockLayout.BedYAt(bx, bz) - sink, bz);
                    Quaternion lean = Quaternion.Euler(leanX, 0f, leanZ);
                    float blade = LakeDockLayout.ReedBladeRadius;
                    b.Lathe(baseWorld + lean * new Vector3(0f, length * 0.5f, 0f), t => blade * (1.35f - 1.25f * t * t), length, 5, 3, lean,
                        LakeDockLayout.SlotReed, EnvTextureLayers.Grass, SmoothMeshBuilder.MappingUv, t => 0.62f + 0.38f * Mathf.Sqrt(t), blade);
                    if (k % 3 == 0)
                    {
                        // Cattail: a soft brown sausage just below the tip.
                        float headLength = 0.16f, headRadius = blade * 2.4f;
                        Vector3 head = baseWorld + lean * new Vector3(0f, length - 0.1f - headLength * 0.5f, 0f);
                        b.Lathe(head, t => headRadius * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t))), headLength, 6, 4, lean,
                            LakeDockLayout.SlotWoodDark, EnvTextureLayers.None, SmoothMeshBuilder.MappingUv, t => 0.8f + 0.2f * t, headRadius);
                    }
                }
                placed++;
            }
            if (placed < LakeDockLayout.ReedClusterCount)
                throw new System.InvalidOperationException("Placed only " + placed + " of " + LakeDockLayout.ReedClusterCount + " reed clusters");
            if (b.TriangleCount > LakeDockLayout.MaxCombinedTriangles)
                throw new System.InvalidOperationException("LakeDock_Reeds has " + b.TriangleCount + " triangles (limit " + LakeDockLayout.MaxCombinedTriangles + ")");
            return b.ToMesh("LakeDock_Reeds");
        }
    }
}
