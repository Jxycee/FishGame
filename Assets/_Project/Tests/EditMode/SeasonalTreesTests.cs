// Authority: test-only. Seasonal trees (user decision 2026-10-13 "Full seasons"): the palette slot rules, the per-tree foliage variant, the
// SeasonalVisibility rule and component, the PaletteGlobals -> SeasonalVisibility hand-over and the built lake dock scene and meshes.
// This assembly does not reference Presentation, so PaletteGlobals is reached by type name and its serialized field by SerializedObject.
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.Tests.EditMode
{
    public class SeasonalTreesTests
    {
        const int Spring = SeasonalLook.Spring, Summer = SeasonalLook.Summer, Autumn = SeasonalLook.Autumn, Winter = SeasonalLook.Winter;

        readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            SeasonalVisibility.SetSeason(SeasonalVisibility.DefaultSeason);
        }

        [TearDown]
        public void TearDown()
        {
            // SeasonalVisibility.CurrentSeason is static: never leak a test season into later tests, builds or validators.
            SeasonalVisibility.SetSeason(SeasonalVisibility.DefaultSeason);
            foreach (UnityEngine.Object o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ---- palette slot rules (seed data and the built asset) ----

        // Catches: a pine slot that changes with the season (pines turning orange again, the user's complaint); Blossom showing outside spring
        // (it must equal that season's Foliage) or not pink in spring; the three round-tree foliage slots not green in spring/summer or not
        // orange / red / yellow in autumn.
        [Test]
        public void Palette_SeasonalTreeSlots_FollowTheRules()
        {
            PaletteSeason[] seasons = FishingSeedData.PaletteSeasons();
            Assert.AreEqual("PineLight", FishingSeedData.PaletteSlotNames[LakeDockLayout.SlotPineLight]);
            Assert.AreEqual("PineDark", FishingSeedData.PaletteSlotNames[LakeDockLayout.SlotPineDark]);
            Assert.AreEqual("Blossom", FishingSeedData.PaletteSlotNames[LakeDockLayout.SlotBlossom]);
            Assert.AreEqual("FoliageAlt", FishingSeedData.PaletteSlotNames[LakeDockLayout.SlotFoliageAlt]);
            Assert.AreEqual("Grass", FishingSeedData.PaletteSlotNames[LakeDockLayout.SlotPineSnow], "the winter pine's snow is the ground snow (Grass)");

            for (int s = 0; s < 4; s++)
            {
                Color[] c = seasons[s].colors;
                Assert.AreEqual(seasons[Summer].colors[LakeDockLayout.SlotPineLight], c[LakeDockLayout.SlotPineLight], "PineLight is evergreen (season " + s + ")");
                Assert.AreEqual(seasons[Summer].colors[LakeDockLayout.SlotPineDark], c[LakeDockLayout.SlotPineDark], "PineDark is evergreen (season " + s + ")");
                Assert.That(IsGreen(c[LakeDockLayout.SlotPineLight]) && IsGreen(c[LakeDockLayout.SlotPineDark]), Is.True, "pines are green");
                Assert.That(Luma(c[LakeDockLayout.SlotPineLight]), Is.GreaterThan(Luma(c[LakeDockLayout.SlotPineDark])), "PineLight is lighter than PineDark");
                if (s == Spring)
                {
                    Color b = c[LakeDockLayout.SlotBlossom];
                    Assert.That(b.r > b.g && b.b > b.g, Is.True, "spring Blossom is pink, got " + b);
                }
                else
                {
                    Assert.AreEqual(c[LakeDockLayout.SlotFoliage], c[LakeDockLayout.SlotBlossom], "Blossom equals Foliage outside spring (season " + s + ")");
                }
                if (s == Spring || s == Summer)
                    foreach (byte slot in LakeDockLayout.RoundFoliageSlots)
                        Assert.That(IsGreen(c[slot]), Is.True, "round foliage slot " + slot + " is green in season " + s + ", got " + c[slot]);
            }

            Color[] autumn = seasons[Autumn].colors;
            Color orange = autumn[LakeDockLayout.SlotFoliage], red = autumn[LakeDockLayout.SlotFoliageDark], yellow = autumn[LakeDockLayout.SlotFoliageAlt];
            Assert.That(orange.r > orange.g && orange.g > orange.b && orange.g > 0.35f && orange.g < 0.65f, Is.True, "autumn Foliage is orange, got " + orange);
            Assert.That(red.r > 0.6f && red.g < 0.35f, Is.True, "autumn FoliageDark is red, got " + red);
            Assert.That(yellow.r > 0.75f && yellow.g > 0.65f && yellow.b < 0.35f, Is.True, "autumn FoliageAlt is yellow, got " + yellow);
            Color winterGrass = seasons[Winter].colors[LakeDockLayout.SlotGrass];
            Assert.That(winterGrass.r > 0.75f && winterGrass.g > 0.75f && winterGrass.b > 0.75f, Is.True, "winter ground (and pine snow) is white, got " + winterGrass);
        }

        // Catches: the content build not rerun after a seed change (the asset keeps the old Reserved names or colours).
        [Test]
        public void Palette_Asset_MatchesTheSeed()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            Assert.IsNotNull(asset, "missing " + AssetPaths.ArtPaletteAsset);
            CollectionAssert.AreEqual(FishingSeedData.PaletteSlotNames, asset.SlotNames, "slot names");
            PaletteSeason[] seed = FishingSeedData.PaletteSeasons();
            Assert.AreEqual(seed.Length, asset.Seasons.Count, "season count");
            for (int s = 0; s < seed.Length; s++)
            {
                Assert.AreEqual(seed[s].name, asset.Seasons[s].name, "season " + s + " name");
                CollectionAssert.AreEqual(seed[s].colors, asset.Seasons[s].colors, "season " + s + " colours");
            }
        }

        // ---- pure rules ----

        // Catches: a mask bit off by one (winter branches showing in autumn), an out-of-range season counted as visible.
        [Test]
        public void SeasonalLook_IsVisible_FollowsTheMaskBits()
        {
            for (int s = 0; s < 4; s++)
            {
                Assert.That(SeasonalLook.IsVisible(SeasonalLook.MaskLeafy, s), Is.EqualTo(s != Winter), "leafy, season " + s);
                Assert.That(SeasonalLook.IsVisible(SeasonalLook.MaskWinter, s), Is.EqualTo(s == Winter), "winter, season " + s);
                Assert.That(SeasonalLook.IsVisible(SeasonalLook.MaskSpring, s), Is.EqualTo(s == Spring), "spring, season " + s);
                Assert.That(SeasonalLook.IsVisible(SeasonalLook.MaskAll, s), Is.True, "all, season " + s);
                Assert.That(SeasonalLook.IsVisible(0, s), Is.False, "none, season " + s);
            }
            Assert.That(SeasonalLook.IsVisible(SeasonalLook.MaskAll, -1), Is.False);
            Assert.That(SeasonalLook.IsVisible(SeasonalLook.MaskAll, 4), Is.False);
            Assert.AreEqual(1, SeasonalLook.MaskSpring);
            Assert.AreEqual(2, SeasonalLook.MaskSummer);
            Assert.AreEqual(4, SeasonalLook.MaskAutumn);
            Assert.AreEqual(8, SeasonalLook.MaskWinter);
        }

        // Catches: an unseeded or order-dependent variant pick (a rebuild recolours the autumn trees), a lopsided grove (all orange), a value out
        // of range.
        [Test]
        public void SeasonalLook_BalancedVariants_AreSeededBalancedAndDeterministic()
        {
            for (int count = 0; count <= 13; count++)
            {
                Pcg32 a = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.FoliageStream, 0UL);
                Pcg32 b = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.FoliageStream, 0UL);
                int[] first = SeasonalLook.BalancedVariants(ref a, count, 3);
                int[] second = SeasonalLook.BalancedVariants(ref b, count, 3);
                CollectionAssert.AreEqual(first, second, "same seed, same variants (count " + count + ")");
                var tally = new int[3];
                foreach (int v in first) { Assert.That(v, Is.InRange(0, 2)); tally[v]++; }
                Assert.That(Mathf.Max(tally[0], Mathf.Max(tally[1], tally[2])) - Mathf.Min(tally[0], Mathf.Min(tally[1], tally[2])), Is.LessThanOrEqualTo(1),
                    "balanced (count " + count + ")");
            }
            Pcg32 x = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.FoliageStream, 0UL), y = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.FoliageStream, 1UL);
            CollectionAssert.AreNotEqual(SeasonalLook.BalancedVariants(ref x, 12, 3), SeasonalLook.BalancedVariants(ref y, 12, 3), "the seed matters");
            Pcg32 z = Rng.Stream(1UL, "s", 0UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => SeasonalLook.BalancedVariants(ref z, 3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SeasonalLook.BalancedVariants(ref z, -1, 3));
        }

        // ---- SeasonalVisibility ----

        // Catches: hiding by GameObject (would remove a canopy's collider) or by the serialized enabled flag; a component not following a season
        // change; an out-of-range season changing anything; a disabled component still following.
        [Test]
        public void SeasonalVisibility_SwitchesRenderersWithTheSeason()
        {
            var root = Track(new GameObject("SeasonalTree"));
            MeshRenderer canopy = Child(root, "Canopy"), blossoms = Child(root, "Blossoms"), branches = Child(root, "Branches");
            canopy.gameObject.AddComponent<SphereCollider>();
            var seasonal = root.AddComponent<SeasonalVisibility>();
            seasonal.Configure(new[]
            {
                new SeasonalVisibility.Entry(canopy, SeasonalLook.MaskLeafy),
                new SeasonalVisibility.Entry(blossoms, SeasonalLook.MaskSpring),
                new SeasonalVisibility.Entry(branches, SeasonalLook.MaskWinter),
            });
            AssertShown("summer (default)", canopy, true, blossoms, false, branches, false);

            SeasonalVisibility.SetSeason(Winter);
            AssertShown("winter", canopy, false, blossoms, false, branches, true);
            Assert.That(canopy.enabled && canopy.gameObject.activeSelf && canopy.GetComponent<SphereCollider>().enabled, Is.True,
                "winter hides by forceRenderingOff only: the canopy object, its renderer flag and its collider stay on");

            SeasonalVisibility.SetSeason(Spring);
            AssertShown("spring", canopy, true, blossoms, true, branches, false);
            SeasonalVisibility.SetSeason(Autumn);
            AssertShown("autumn", canopy, true, blossoms, false, branches, false);
            SeasonalVisibility.SetSeason(7);
            Assert.AreEqual(Autumn, SeasonalVisibility.CurrentSeason, "an out-of-range season is ignored");
            AssertShown("after an ignored season", canopy, true, blossoms, false, branches, false);

            seasonal.enabled = false;
            SeasonalVisibility.SetSeason(Winter);
            AssertShown("disabled component keeps its state", canopy, true, blossoms, false, branches, false);
            seasonal.enabled = true;
            AssertShown("re-enabled component catches up", canopy, false, blossoms, false, branches, true);
        }

        // Catches: PaletteGlobals pushing a season without handing it to SeasonalVisibility (the trees would ignore the season, and an M3 runtime
        // season change through PaletteGlobals.Apply would recolour the ground but leave the trees leafy).
        [Test]
        public void SeasonalVisibility_FollowsThePaletteGlobalsPush()
        {
            Type globalsType = Type.GetType("FishGame.Presentation.PaletteGlobals, FishGame.Presentation");
            Assert.IsNotNull(globalsType, "FishGame.Presentation.PaletteGlobals");
            var palette = Track(ScriptableObject.CreateInstance<ArtPaletteDef>());
            palette.Configure(FishingSeedData.PaletteSlotNames, FishingSeedData.PaletteSeasons(), FishingSeedData.PaletteFaceJitter,
                FishingSeedData.PaletteSkinTone, FishingSeedData.PalettePhases(), FishingSeedData.PaletteUiAccent, FishingSeedData.PaletteUiGood,
                FishingSeedData.PaletteUiWarn, FishingSeedData.PaletteUiBad, FishingSeedData.PaletteUiPanel, FishingSeedData.PaletteUiText, Winter);

            var tree = Track(new GameObject("Tree"));
            MeshRenderer canopy = Child(tree, "Canopy"), branches = Child(tree, "Branches");
            tree.AddComponent<SeasonalVisibility>().Configure(new[]
            {
                new SeasonalVisibility.Entry(canopy, SeasonalLook.MaskLeafy),
                new SeasonalVisibility.Entry(branches, SeasonalLook.MaskWinter),
            });
            AssertShown("before the push", canopy, true, null, false, branches, false);

            var host = Track(new GameObject("Presentation"));
            host.SetActive(false);
            Component globals = host.AddComponent(globalsType);
            var so = new SerializedObject(globals);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.ApplyModifiedPropertiesWithoutUndo();
            host.SetActive(true);   // OnEnable -> Apply -> the winter palette push

            Assert.AreEqual(Winter, SeasonalVisibility.CurrentSeason, "PaletteGlobals.Apply hands the pushed season on");
            AssertShown("after the winter push", canopy, false, null, false, branches, true);
        }

        // ---- the built lake dock ----

        // Catches: a tree without SeasonalVisibility, a missing or mis-masked renderer, a seasonal child with a collider, winter not baring the
        // round trees or not snowing the pines, spring without blossoms, the foliage variants not the seeded balanced shuffle.
        [Test]
        public void LakeDock_SeasonSwitch_BaresRoundTreesInWinterAndKeepsColliders()
        {
            Scene scene = EditorSceneManager.OpenScene(AssetPaths.LakeDockScene, OpenSceneMode.Additive);
            try
            {
                Transform shore = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Shore") shore = root.transform;
                Assert.IsNotNull(shore, "Shore root");

                var round = new List<Transform>();
                var pines = new List<Transform>();
                var variantMeshes = new List<Mesh>();
                foreach (string path in LakeDockLayout.RoundCanopyMeshPaths) variantMeshes.Add(AssetDatabase.LoadAssetAtPath<Mesh>(path));
                var pineMesh = AssetDatabase.LoadAssetAtPath<Mesh>(LakeDockLayout.CanopyPineMeshPath);
                var expectedVariants = new List<int>();
                foreach (Transform t in shore)
                {
                    if (!t.name.StartsWith("Tree_")) continue;
                    Assert.IsNotNull(t.GetComponent<SeasonalVisibility>(), t.name + " SeasonalVisibility");
                    Mesh canopyMesh = t.Find("Canopy").GetComponent<MeshFilter>().sharedMesh;
                    if (canopyMesh == pineMesh) pines.Add(t);
                    else
                    {
                        int v = variantMeshes.IndexOf(canopyMesh);
                        Assert.That(v, Is.GreaterThanOrEqualTo(0), t.name + ": canopy mesh " + canopyMesh.name + " is neither the pine nor a round variant");
                        expectedVariants.Add(v);
                        round.Add(t);
                    }
                    foreach (string visual in new[] { "Canopy/Blossoms", "Branches", "Canopy/CanopyWinter" })
                    {
                        Transform child = t.Find(visual);
                        if (child != null) Assert.IsNull(child.GetComponent<Collider>(), t.name + "/" + visual + " must have no collider");
                    }
                }
                Assert.AreEqual(LakeDockLayout.TreeCount, round.Count + pines.Count, "tree count");
                Assert.That(round.Count, Is.GreaterThan(3), "some round trees");
                Assert.That(pines.Count, Is.GreaterThan(3), "some pines");

                Pcg32 rng = Rng.Stream(LakeDockLayout.Seed, LakeDockLayout.FoliageStream, 0UL);
                CollectionAssert.AreEqual(SeasonalLook.BalancedVariants(ref rng, round.Count, 3), expectedVariants,
                    "round trees take the balanced shuffle of lake_dock.foliage in placement order");

                SeasonalVisibility.SetSeason(Winter);
                foreach (Transform t in round)
                    AssertShown(t.name + " winter", R(t, "Canopy"), false, R(t, "Canopy/Blossoms"), false, R(t, "Branches"), true);
                foreach (Transform t in pines)
                    AssertShown(t.name + " winter", R(t, "Canopy"), false, null, false, R(t, "Canopy/CanopyWinter"), true);

                SeasonalVisibility.SetSeason(Spring);
                foreach (Transform t in round)
                    AssertShown(t.name + " spring", R(t, "Canopy"), true, R(t, "Canopy/Blossoms"), true, R(t, "Branches"), false);
                foreach (Transform t in pines)
                    AssertShown(t.name + " spring", R(t, "Canopy"), true, null, false, R(t, "Canopy/CanopyWinter"), false);

                SeasonalVisibility.SetSeason(Autumn);
                foreach (Transform t in round)
                    AssertShown(t.name + " autumn", R(t, "Canopy"), true, R(t, "Canopy/Blossoms"), false, R(t, "Branches"), false);
                foreach (Transform t in round)
                {
                    Assert.IsNotNull(t.Find("Canopy").GetComponent<SphereCollider>(), t.name + " canopy collider");
                    Assert.IsTrue(t.Find("Canopy").gameObject.activeSelf, t.name + " canopy object stays active");
                }
            }
            finally
            {
                SeasonalVisibility.SetSeason(SeasonalVisibility.DefaultSeason);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Catches: a seasonal mesh poking out of the canopy collider sphere, the wrong slot or mapping on a seasonal mesh, a pine using a seasonal
        // slot, snow on a downward-facing ring, a foliage variant whose shape differs from the original canopy, a triangle budget blown, a zero
        // normal (tiny Radial blobs: the first blossom capture rendered black).
        [Test]
        public void LakeDock_SeasonalMeshes_StayInsideTheColliderAndUseTheirSlots()
        {
            Mesh branches = LoadMesh(LakeDockLayout.BranchesMeshPath);
            Vector3 canopyCentre = new Vector3(0f, 2.6f, 0f);   // tree space: the canopy primitive at y 2.6, scale 2.4 (radius 1.2)
            foreach (Vector3 v in branches.vertices)
                Assert.That((v - canopyCentre).magnitude, Is.LessThanOrEqualTo(1.2f), "branch vertex " + v + " outside the canopy collider");
            AssertSlots(branches, new[] { LakeDockLayout.SlotWoodDark }, SmoothMeshBuilder.MappingUv, "branches");
            Assert.That(Triangles(branches), Is.LessThanOrEqualTo(1000), "branches triangle budget");

            Mesh blossoms = LoadMesh(LakeDockLayout.BlossomsMeshPath);
            foreach (Vector3 v in blossoms.vertices) Assert.That(v.magnitude, Is.LessThanOrEqualTo(0.5f + 1e-4f), "blossom vertex outside the canopy sphere");
            AssertSlots(blossoms, new[] { LakeDockLayout.SlotBlossom }, SmoothMeshBuilder.MappingTriplanar, "blossoms");
            Assert.That(Triangles(blossoms), Is.LessThanOrEqualTo(2500), "blossoms triangle budget (spring only)");
            foreach (string path in new[] { LakeDockLayout.BranchesMeshPath, LakeDockLayout.BlossomsMeshPath, LakeDockLayout.CanopyPineSnowMeshPath,
                         LakeDockLayout.CanopyRoundDarkMeshPath, LakeDockLayout.CanopyRoundAltMeshPath })
                foreach (Vector3 n in LoadMesh(path).normals)
                    Assert.That(n.magnitude, Is.EqualTo(1f).Within(1e-3f), path + ": unit normals (a zero normal renders black)");

            Mesh baseCanopy = LoadMesh(LakeDockLayout.RoundCanopyMeshPaths[0]);
            for (int i = 0; i < LakeDockLayout.RoundCanopyMeshPaths.Length; i++)
            {
                Mesh m = LoadMesh(LakeDockLayout.RoundCanopyMeshPaths[i]);
                CollectionAssert.AreEqual(baseCanopy.vertices, m.vertices, m.name + ": same shape as the round canopy");
                AssertSlots(m, new[] { LakeDockLayout.RoundFoliageSlots[i] }, SmoothMeshBuilder.MappingTriplanar, m.name);
            }

            Mesh pine = LoadMesh(LakeDockLayout.CanopyPineMeshPath), snowy = LoadMesh(LakeDockLayout.CanopyPineSnowMeshPath);
            AssertSlots(pine, new[] { LakeDockLayout.SlotPineLight, LakeDockLayout.SlotPineDark }, SmoothMeshBuilder.MappingTriplanar, "pine");
            AssertSlots(snowy, new[] { LakeDockLayout.SlotPineLight, LakeDockLayout.SlotPineDark, LakeDockLayout.SlotPineSnow },
                SmoothMeshBuilder.MappingTriplanar, "snowy pine");
            CollectionAssert.AreEqual(pine.vertices, snowy.vertices, "the snowy pine has the pine's shape");
            var uv1 = new List<Vector4>();
            snowy.GetUVs(1, uv1);
            Vector3[] normals = snowy.normals;
            int snowCount = 0;
            for (int i = 0; i < uv1.Count; i++)
            {
                if ((int)uv1[i].x != LakeDockLayout.SlotPineSnow) continue;
                snowCount++;
                Assert.That(normals[i].y, Is.GreaterThan(0f), "snow only on top-facing vertices (vertex " + i + ")");
            }
            Assert.That(snowCount, Is.GreaterThan(uv1.Count / 4), "a real snow cap");
            Assert.That(snowCount, Is.LessThan(uv1.Count), "a green fringe stays");
        }

        // ---- helpers ----

        T Track<T>(T o) where T : UnityEngine.Object
        {
            _created.Add(o);
            return o;
        }

        static MeshRenderer Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>();
            return go.AddComponent<MeshRenderer>();
        }

        static Renderer R(Transform tree, string path)
        {
            Transform t = tree.Find(path);
            Assert.IsNotNull(t, tree.name + "/" + path);
            Renderer r = t.GetComponent<Renderer>();
            Assert.IsNotNull(r, tree.name + "/" + path + " renderer");
            return r;
        }

        static void AssertShown(string when, Renderer a, bool aShown, Renderer b, bool bShown, Renderer c, bool cShown)
        {
            Assert.That(!a.forceRenderingOff, Is.EqualTo(aShown), when + ": " + a.name);
            if (b != null) Assert.That(!b.forceRenderingOff, Is.EqualTo(bShown), when + ": " + b.name);
            Assert.That(!c.forceRenderingOff, Is.EqualTo(cShown), when + ": " + c.name);
        }

        static Mesh LoadMesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Assert.IsNotNull(mesh, "missing " + path);
            return mesh;
        }

        static int Triangles(Mesh m)
        {
            int n = 0;
            for (int s = 0; s < m.subMeshCount; s++) n += (int)(m.GetIndexCount(s) / 3);
            return n;
        }

        static void AssertSlots(Mesh m, byte[] allowed, byte mapping, string what)
        {
            var uv1 = new List<Vector4>();
            m.GetUVs(1, uv1);
            Assert.AreEqual(m.vertexCount, uv1.Count, what + ": uv1 on every vertex");
            var seen = new HashSet<int>();
            foreach (Vector4 v in uv1)
            {
                Assert.That(Array.IndexOf(allowed, (byte)v.x), Is.GreaterThanOrEqualTo(0), what + ": slot " + v.x + " not allowed");
                Assert.AreEqual((float)mapping, v.z, what + ": uv1.z mapping");
                seen.Add((int)v.x);
            }
            Assert.AreEqual(allowed.Length, seen.Count, what + ": every allowed slot is used");
        }

        static bool IsGreen(Color c) => c.g > c.r && c.g > c.b;

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
