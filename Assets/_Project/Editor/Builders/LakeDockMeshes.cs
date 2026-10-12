// Meshes of Spot_LakeDock (contract 6.4 / 7.28, plan T28). Owned by T15 (lakebed, shore), T28 (everything else). Authority: none (editor).
// Radial height-field meshes for the lakebed and shore (positions and triangles are the M1 physics numbers; T28 adds per-face vertex colours and uv1 palette slots)
// plus the MeshBuilder props: water grid, rock, trunk, two canopies, the combined dock visual and the combined reeds.
// Every mesh is saved IN PLACE (EditorUtility.CopySerialized into the existing asset), so GUIDs never change and a rebuild is byte-identical:
// fixed mesh seeds, fixed draw order, the MeshBuilder default face jitter (never ArtPaletteDef.faceJitter), new random streams only.
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

        static Mesh BuildRock()
        {
            var b = new MeshBuilder(LakeDockLayout.RockMeshSeed);
            b.Icosphere(Vector3.zero, 0.5f, 1, LakeDockLayout.SlotRock);
            return b.ToMesh("LakeDock_Rock");
        }

        // Unity cylinder primitive: radius 0.5, height 2, centred.
        static Mesh BuildTrunk()
        {
            var b = new MeshBuilder(LakeDockLayout.TrunkMeshSeed);
            b.Cylinder(Vector3.zero, LakeDockLayout.TrunkRadiusBottom, LakeDockLayout.TrunkRadiusTop, LakeDockLayout.TrunkHeight, LakeDockLayout.TrunkSides,
                Quaternion.identity, LakeDockLayout.SlotWoodDark);
            return b.ToMesh("LakeDock_Trunk");
        }

        static Mesh BuildCanopyRound()
        {
            var b = new MeshBuilder(LakeDockLayout.CanopyRoundMeshSeed);
            b.Icosphere(Vector3.zero, 0.5f, 1, LakeDockLayout.SlotFoliage);
            return b.ToMesh("LakeDock_CanopyRound");
        }

        // Two stacked cones inside the unit sphere (radius 0.5, the canopy collider): the lower dark, the upper light. The lower rim (r 0.3 at y -0.4)
        // keeps r^2 + y^2 <= 0.25, so the visual never overhangs the collider.
        static Mesh BuildCanopyPine()
        {
            var b = new MeshBuilder(LakeDockLayout.CanopyPineMeshSeed);
            b.Cylinder(new Vector3(0f, -0.15f, 0f), 0.3f, 0f, 0.5f, LakeDockLayout.TrunkSides, Quaternion.identity, LakeDockLayout.SlotFoliageDark);
            b.Cylinder(new Vector3(0f, 0.225f, 0f), 0.3f, 0f, 0.55f, LakeDockLayout.TrunkSides, Quaternion.identity, LakeDockLayout.SlotFoliage);
            return b.ToMesh("LakeDock_CanopyPine");
        }

        // ---- dock visual ----

        // One combined world-space mesh (the Dock root sits at the origin with identity rotation). Visual only: the M1 box colliders stay the physics.
        static Mesh BuildDock()
        {
            var b = new MeshBuilder(LakeDockLayout.DockMeshSeed);
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
                b.Box(new Vector3(LakeDockLayout.StringerX * side, stringerY, (LakeDockLayout.DeckNearZ + LakeDockLayout.DeckFarZ) * 0.5f),
                    new Vector3(LakeDockLayout.StringerSize, LakeDockLayout.StringerSize, deckLength), Quaternion.identity, LakeDockLayout.SlotWoodDark);

            // Posts at the M1 post positions, from the bed to the deck underside.
            int postRows = Mathf.RoundToInt(deckLength / LakeDockLayout.PostSpacing) + 1;
            for (int i = 0; i < postRows; i++)
            {
                float z = LakeDockLayout.DeckNearZ + LakeDockLayout.PostSpacing * i;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = LakeDockLayout.PostX * side;
                    float bed = LakeDockLayout.BedYAt(x, z);
                    b.Prism(new Vector3(x, (deckUnderside + bed) * 0.5f, z), LakeDockLayout.PostRadius, deckUnderside - bed, LakeDockLayout.PostSides,
                        Quaternion.identity, LakeDockLayout.SlotWoodDark);
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
        static void AddPlank(MeshBuilder b, ref Pcg32 rng, Vector3 centre, Quaternion baseRotation, Vector3 size)
        {
            byte slot = rng.NextFloat() < 0.5f ? LakeDockLayout.SlotWood : LakeDockLayout.SlotDock;
            float yaw = rng.Range(-LakeDockLayout.PlankYawDegrees, LakeDockLayout.PlankYawDegrees);
            b.Box(centre, size, baseRotation * Quaternion.Euler(0f, yaw, 0f), slot);
        }

        // Two rails and rungs every RungSpacing inside the M1 ladder collider volume (x +-0.03, z +-0.25 around LadderZ), ending at the deck top.
        static void AddLadder(MeshBuilder b, float x)
        {
            float bed = LakeDockLayout.BedYAt(x, LakeDockLayout.LadderZ);
            float top = LakeDockLayout.DeckTopY;
            float height = top - bed;
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(x, (top + bed) * 0.5f, LakeDockLayout.LadderZ + LakeDockLayout.RailInset * side),
                    new Vector3(LakeDockLayout.RailSize, height, LakeDockLayout.RailSize), Quaternion.identity, LakeDockLayout.SlotWoodDark);
            float rungSpan = LakeDockLayout.RailInset * 2f;
            for (int k = 1; ; k++)
            {
                float y = bed + LakeDockLayout.RungSpacing * k;
                if (y > top - LakeDockLayout.RungThickness) break;
                b.Box(new Vector3(x, y, LakeDockLayout.LadderZ), new Vector3(LakeDockLayout.RungDepth, LakeDockLayout.RungThickness, rungSpan),
                    Quaternion.identity, LakeDockLayout.SlotWoodDark);
            }
        }

        // ---- reeds ----

        // One combined mesh. Draws (stream lake_dock.reeds): per attempt angle and radius (a blocked spot costs those two draws), then per placed cluster
        // the blade count, then per blade offset x, offset z, height, lean x, lean z.
        static Mesh BuildReeds()
        {
            var b = new MeshBuilder(LakeDockLayout.ReedsMeshSeed);
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
                    b.Prism(baseWorld + lean * new Vector3(0f, length * 0.5f, 0f), LakeDockLayout.ReedBladeRadius, length, 3, lean, LakeDockLayout.SlotReed);
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
