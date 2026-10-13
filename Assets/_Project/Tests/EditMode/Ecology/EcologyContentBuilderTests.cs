// T10: EcologyContentBuilder is create-if-missing. Re-running it must never reconfigure an existing asset, never append a second copy of a registry
// entry, never fill a non-null zone, and must refuse to overwrite a file it cannot load as the expected type.
// SAFETY: every test works ONLY inside Assets/_Project/Tests/__Temp/EcologyContentBuilderTests (gitignored, deleted in OneTimeTearDown and on a failed
// setup) through EcologyContentBuilder.BuildAt(tempRoot, tempRegistry). No test path writes, edits or deletes anything under Assets/_Project/Content.
using System;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class EcologyContentBuilderTests
    {
        private const string RealContentRoot = "Assets/_Project/Content";
        private const string TempParent = "Assets/_Project/Tests/__Temp";
        private const string Root = TempParent + "/EcologyContentBuilderTests";
        private const string WrongTypeRoot = Root + "/WrongType";
        private const string MissingRoot = Root + "/NoSpecies";

        private ContentRegistry _registry;

        // The temp-root twin of a real AssetPaths constant.
        private static string T(string realPath, string root = Root)
        {
            Assert.IsTrue(realPath.StartsWith(RealContentRoot + "/", StringComparison.Ordinal), realPath);
            return root + realPath.Substring(RealContentRoot.Length);
        }

        private static void DeleteTempFolders()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            if (AssetDatabase.IsValidFolder(TempParent)
                && AssetDatabase.GetSubFolders(TempParent).Length == 0
                && AssetDatabase.FindAssets("", new[] { TempParent }).Length == 0)
                AssetDatabase.DeleteAsset(TempParent);
        }

        private static ContentRegistry NewRegistry(string root)
        {
            BuilderUtil.EnsureFolder(root);
            var registry = ScriptableObject.CreateInstance<ContentRegistry>();
            AssetDatabase.CreateAsset(registry, root + "/ContentRegistry.asset");
            return registry;
        }

        private static SpotDef NewLakeSpot(string root)
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(T(AssetPaths.LakeDockSpotAsset, root)));
            var spot = ScriptableObject.CreateInstance<SpotDef>();
            spot.Configure(ContentIds.LakeDock, "Lake Dock", "Spot_LakeDock");
            AssetDatabase.CreateAsset(spot, T(AssetPaths.LakeDockSpotAsset, root));
            return spot;
        }

        [OneTimeSetUp]
        public void BuildTempContent()
        {
            DeleteTempFolders();
            try
            {
                _registry = NewRegistry(Root);
                NewLakeSpot(Root);
                FishingContentBuilder.BuildAt(Root, _registry);
                EcologyContentBuilder.BuildAt(Root, _registry);
            }
            catch
            {
                // NUnit does not run OneTimeTearDown when OneTimeSetUp throws: clean up here so a failed build never leaves Tests/__Temp behind.
                DeleteTempFolders();
                throw;
            }
        }

        [OneTimeTearDown]
        public void DeleteTempContent()
        {
            DeleteTempFolders();
        }

        private TAsset Load<TAsset>(string realPath) where TAsset : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<TAsset>(T(realPath));
        }

        private FishingZoneDef Zone(string id)
        {
            return AssetDatabase.LoadAssetAtPath<FishingZoneDef>(T(AssetPaths.ZonesFolder) + "/" + id + ".asset");
        }

        private ShoreGridDef Grid(string zoneId)
        {
            return AssetDatabase.LoadAssetAtPath<ShoreGridDef>(T(AssetPaths.ShoreGridsFolder) + "/" + zoneId + "_grid.asset");
        }

        // Catches: an overwrite of tuned or baked data; duplicate list entries; the harbour spot wired before its scene is in the build order.
        [Test, Order(1)]
        public void EcologyContentBuilder_BuildAt_CreatesMissingOnly()
        {
            Assert.That(Zone("lake_dock"), Is.Not.Null);
            Assert.That(Zone("harbour_pier"), Is.Not.Null);
            Assert.That(Load<WeatherTableDef>(AssetPaths.WeatherTableAsset), Is.Not.Null);
            Assert.That(Grid("lake_dock").Width, Is.EqualTo(0), "the grid is created unbaked");
            Assert.That(Grid("harbour_pier").Width, Is.EqualTo(0));
            Assert.That(Grid("lake_dock").ToCore(), Is.Null);
            Assert.That(Load<EcologyTuningDef>(AssetPaths.EcologyTuningAsset), Is.Not.Null);
            Assert.That(Load<SpotDef>(AssetPaths.HarbourPierSpotAsset), Is.Not.Null);
            Assert.That(_registry.Zones.Count, Is.EqualTo(2));
            Assert.That(_registry.EcologyTuning, Is.Not.Null);

            // Tune a band, a weather value and a grid; run again: nothing is overwritten and nothing is appended twice.
            FishingZoneDef lake = Zone("lake_dock");
            var so = new SerializedObject(lake);
            so.FindProperty("subAreas").GetArrayElementAtIndex(0).FindPropertyRelative("junkChance").floatValue = 0.37f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lake);
            WeatherTableDef weather = Load<WeatherTableDef>(AssetPaths.WeatherTableAsset);
            var baseMatrix = weather.BaseMatrix.ToArray();
            baseMatrix[0] = 0.5f;
            weather.Configure(weather.Id, baseMatrix, weather.SeasonColumnMult.ToArray());
            EditorUtility.SetDirty(weather);
            ShoreGridDef grid = Grid("lake_dock");
            grid.WriteBake(2, 2, 1f, 0f, 0f, new ushort[4], new byte[1], new byte[1], new Vector3[0], "Spot_Baked");
            EditorUtility.SetDirty(grid);

            EcologyContentBuilder.BuildAt(Root, _registry);

            Assert.That(Zone("lake_dock").SubAreas[0].junkChance, Is.EqualTo(0.37f), "a tuned band survives");
            Assert.That(Load<WeatherTableDef>(AssetPaths.WeatherTableAsset).BaseMatrix[0], Is.EqualTo(0.5f), "a tuned weather value survives");
            Assert.That(Grid("lake_dock").BakedFromScene, Is.EqualTo("Spot_Baked"), "a baked grid is never touched");
            Assert.That(Grid("lake_dock").Width, Is.EqualTo(2));
            Assert.That(_registry.Zones.Count, Is.EqualTo(2), "no duplicate zone entry");
            SpotDef harbour = Load<SpotDef>(AssetPaths.HarbourPierSpotAsset);
            int inRegistry = _registry.Spots.Count(s => s == harbour);
            bool inBuildOrder = Array.IndexOf(SceneNames.BuildOrder, "Spot_HarbourPier") >= 0;
            Assert.That(inRegistry, Is.EqualTo(inBuildOrder ? 1 : 0), "the harbour spot is wired once, and only when its scene is in the build order");

            // Deleting one zone asset and re-running recreates only that one.
            FishingZoneDef lakeBefore = Zone("lake_dock");
            Assert.That(AssetDatabase.DeleteAsset(T(AssetPaths.ZonesFolder) + "/harbour_pier.asset"), Is.True);
            EcologyContentBuilder.BuildAt(Root, _registry);
            Assert.That(Zone("harbour_pier"), Is.Not.Null, "the deleted zone is recreated");
            Assert.That(Zone("lake_dock"), Is.SameAs(lakeBefore), "the other zone is the same asset");
            Assert.That(Zone("lake_dock").SubAreas[0].junkChance, Is.EqualTo(0.37f));
        }

        // Catches: filling a non-null zone; overwriting tuned looks; leaving an empty palette without the M3 fields.
        [Test, Order(2)]
        public void EcologyContentBuilder_FillsNullSpotZoneOnly()
        {
            var lakeSpot = AssetDatabase.LoadAssetAtPath<SpotDef>(T(AssetPaths.LakeDockSpotAsset));
            Assert.That(lakeSpot.Zone, Is.Not.Null.And.SameAs(Zone("lake_dock")), "the null zone was filled with the lake zone");
            Assert.That(lakeSpot.MapOrder, Is.EqualTo(0), "mapOrder stays 0");

            // A zone that is already set is never changed.
            var so = new SerializedObject(lakeSpot);
            so.FindProperty("zone").objectReferenceValue = Zone("harbour_pier");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lakeSpot);
            EcologyContentBuilder.BuildAt(Root, _registry);
            Assert.That(lakeSpot.Zone, Is.SameAs(Zone("harbour_pier")), "a non-null zone is kept");

            // Tuned looks stay; an empty palette is filled.
            ArtPaletteDef palette = _registry.ArtPalette;
            Assert.That(palette.WeatherLooks.Count, Is.EqualTo(3), "filled on the first run");
            var tuned = new[] { new WeatherAmbient { fogDensityMult = 9f }, new WeatherAmbient { fogDensityMult = 8f }, new WeatherAmbient { fogDensityMult = 7f } };
            SunMoonSettings sun = palette.SunMoon;
            palette.ConfigureM3(in sun, tuned);
            EcologyContentBuilder.BuildAt(Root, _registry);
            Assert.That(palette.WeatherLooks[0].fogDensityMult, Is.EqualTo(9f), "tuned looks are kept");

            palette.ConfigureM3(in sun, new WeatherAmbient[0]);
            EcologyContentBuilder.BuildAt(Root, _registry);
            Assert.That(palette.WeatherLooks.Count, Is.EqualTo(3), "an empty palette is filled");
            Assert.That(palette.WeatherLooks[1].rainAmount, Is.EqualTo(1f));
            Assert.That(_registry.SfxCueTable.SeasonAmbience.Count, Is.EqualTo(4), "the cue table season ambience is filled");
        }

        // Catches: silently replacing a foreign asset; building zones with null fish.
        [Test, Order(3)]
        public void EcologyContentBuilder_WrongTypeAsset_Throws()
        {
            // A JunkDef sits where the lake zone should be.
            ContentRegistry reg = NewRegistry(WrongTypeRoot);
            FishingContentBuilder.BuildAt(WrongTypeRoot, reg);
            string zonePath = T(AssetPaths.ZonesFolder, WrongTypeRoot) + "/lake_dock.asset";
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(zonePath));
            var foreign = ScriptableObject.CreateInstance<JunkDef>();
            foreign.Configure("not_a_zone", "Foreign", 3, BodyShape.Flat, Color.red);
            AssetDatabase.CreateAsset(foreign, zonePath);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => EcologyContentBuilder.BuildAt(WrongTypeRoot, reg));
            StringAssert.Contains(zonePath, ex.Message);
            var still = AssetDatabase.LoadAssetAtPath<JunkDef>(zonePath);
            Assert.That(still, Is.Not.Null, "the foreign asset is still there");
            Assert.That(still.Id, Is.EqualTo("not_a_zone"));
            Assert.That(still.ValueCoins, Is.EqualTo(3));

            // A root without the species assets: the builder tells the caller to run FishingContentBuilder first.
            ContentRegistry bare = NewRegistry(MissingRoot);
            InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() => EcologyContentBuilder.BuildAt(MissingRoot, bare));
            StringAssert.Contains("FishingContentBuilder runs first", missing.Message);
            StringAssert.Contains("species asset", missing.Message);
        }
    }
}
