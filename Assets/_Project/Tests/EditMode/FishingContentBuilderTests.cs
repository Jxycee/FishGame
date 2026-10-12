// T14 extra: FishingContentBuilder is create-if-missing. Re-running it must never reconfigure an existing asset, must never append a
// second copy of a registry entry, and must refuse to overwrite a file it cannot load as the expected type.
// SAFETY: every test works ONLY inside Assets/_Project/Tests/__Temp/FishingContentBuilderTests (gitignored, deleted in OneTimeTearDown) through
// FishingContentBuilder.BuildAt(tempRoot, tempRegistry). No test path writes, edits or deletes anything under Assets/_Project/Content, so a failing
// or aborted run can never leave a committed asset changed.
using System;
using System.Collections.Generic;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class FishingContentBuilderTests
    {
        private const string RealContentRoot = "Assets/_Project/Content";
        private const string TempParent = "Assets/_Project/Tests/__Temp";
        private const string Root = TempParent + "/FishingContentBuilderTests";
        private const string WrongTypeRoot = Root + "/WrongType";
        private const string ForeignPalettePath = Root + "/ForeignPalette.asset";

        private ContentRegistry registry;

        // The temp-root twin of a real AssetPaths constant.
        private static string T(string realPath)
        {
            Assert.IsTrue(realPath.StartsWith(RealContentRoot + "/", StringComparison.Ordinal), realPath);
            return Root + realPath.Substring(RealContentRoot.Length);
        }

        private static void DeleteTempFolders()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            if (AssetDatabase.IsValidFolder(TempParent)
                && AssetDatabase.GetSubFolders(TempParent).Length == 0
                && AssetDatabase.FindAssets("", new[] { TempParent }).Length == 0)
                AssetDatabase.DeleteAsset(TempParent);
        }

        [OneTimeSetUp]
        public void BuildTempContent()
        {
            DeleteTempFolders();
            BuilderUtil.EnsureFolder(Root);
            registry = ScriptableObject.CreateInstance<ContentRegistry>();
            AssetDatabase.CreateAsset(registry, Root + "/ContentRegistry.asset");
            FishingContentBuilder.BuildAt(Root, registry);
        }

        [OneTimeTearDown]
        public void DeleteTempContent()
        {
            DeleteTempFolders();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(o, "missing " + path);
            return o;
        }

        private static SerializedProperty Prop(SerializedObject so, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            Assert.IsNotNull(p, "no serialized property '" + path + "'");
            return p;
        }

        // Catches: BuildAt (or one of its Ensure calls) still using an AssetPaths constant un-rooted: the asset would be created in or loaded
        // from the real Content folder, and the temp registry would reference a path outside the temp root.
        [Test]
        public void FishingContentBuilder_BuildAt_ReRootsEveryAsset()
        {
            int referenced = 0;
            void CheckUnderRoot(UnityEngine.Object o, string what)
            {
                Assert.IsNotNull(o, what + " is null");
                string path = AssetDatabase.GetAssetPath(o);
                StringAssert.StartsWith(Root + "/", path, what + " must live under the temp root");
                referenced++;
            }
            foreach (RodDef d in registry.Rods) CheckUnderRoot(d, "rod");
            foreach (BaitDef d in registry.Baits) CheckUnderRoot(d, "bait");
            foreach (FightProfileDef d in registry.FightProfiles) CheckUnderRoot(d, "profile");
            foreach (FishSpeciesDef d in registry.Species) CheckUnderRoot(d, "species");
            foreach (JunkDef d in registry.Junk) CheckUnderRoot(d, "junk");
            CheckUnderRoot(registry.FishingTuning, "tuning");
            CheckUnderRoot(registry.SfxCueTable, "cue table");
            CheckUnderRoot(registry.ArtPalette, "palette");
            CheckUnderRoot(registry.WaterSettings, "water");

            Assert.AreEqual(FishingSeedData.Rods.Count, registry.Rods.Count);
            Assert.AreEqual(FishingSeedData.Species.Count, registry.Species.Count);
            Assert.AreEqual(referenced, FishingSeedData.Rods.Count + FishingSeedData.Baits.Count + FishingSeedData.Profiles.Count
                                        + FishingSeedData.Species.Count + FishingSeedData.Junk.Count + 4, "every seed asset is wired");

            // every constant lands at its re-rooted twin
            Load<FishingTuningDef>(T(AssetPaths.FishingTuningAsset));
            Load<SfxCueTableDef>(T(AssetPaths.SfxCueTableAsset));
            Load<ArtPaletteDef>(T(AssetPaths.ArtPaletteAsset));
            Load<WaterSettingsDef>(T(AssetPaths.WaterSettingsAsset));
            Load<RodDef>(T(AssetPaths.RodsFolder) + "/rod_twig.asset");
            Load<BaitDef>(T(AssetPaths.BaitsFolder) + "/bait_worm.asset");
            Load<FightProfileDef>(T(AssetPaths.FightProfilesFolder) + "/darter.asset");
            Load<FishSpeciesDef>(T(AssetPaths.SpeciesFolder) + "/perch.asset");
            Load<JunkDef>(T(AssetPaths.JunkFolder) + "/tin_can.asset");
        }

        // Catches: a BuildAt that accepts a root outside Assets/ or a null registry (it would write somewhere unintended / throw later).
        [Test]
        public void FishingContentBuilder_BuildAt_RejectsBadArguments()
        {
            Assert.Throws<ArgumentException>(() => FishingContentBuilder.BuildAt("", registry));
            Assert.Throws<ArgumentException>(() => FishingContentBuilder.BuildAt("Packages/x", registry));
            Assert.Throws<ArgumentNullException>(() => FishingContentBuilder.BuildAt(Root, null));
        }

        // Catches: a Configure outside an `if (asset == null)` branch (the value would reset to the seed), a singleton overwritten, and a
        // registry list that is appended again on every run (counts would double).
        [Test]
        public void FishingContentBuilder_Build_NeverReconfiguresExistingAssets()
        {
            var tuning = Load<FishingTuningDef>(T(AssetPaths.FishingTuningAsset));
            var rod = Load<RodDef>(T(AssetPaths.RodsFolder) + "/rod_twig.asset");
            var bait = Load<BaitDef>(T(AssetPaths.BaitsFolder) + "/bait_worm.asset");
            var profile = Load<FightProfileDef>(T(AssetPaths.FightProfilesFolder) + "/darter.asset");
            var species = Load<FishSpeciesDef>(T(AssetPaths.SpeciesFolder) + "/perch.asset");
            var junk = Load<JunkDef>(T(AssetPaths.JunkFolder) + "/tin_can.asset");
            var cues = Load<SfxCueTableDef>(T(AssetPaths.SfxCueTableAsset));
            var palette = Load<ArtPaletteDef>(T(AssetPaths.ArtPaletteAsset));
            var water = Load<WaterSettingsDef>(T(AssetPaths.WaterSettingsAsset));

            int rods = registry.Rods.Count, baits = registry.Baits.Count, profiles = registry.FightProfiles.Count;
            int speciesCount = registry.Species.Count, junkCount = registry.Junk.Count;

            float tuningOriginal = tuning.CastChargeTime;
            float rodOriginal = rod.CastMaxDist;
            int baitOriginal = bait.Rating;
            float profileOriginal = profile.CalmMin;
            int speciesOriginal = species.BaseValue;
            int junkOriginal = junk.ValueCoins;
            float cueOriginal = cues.Rows[0].baseVolume;
            float paletteOriginal = palette.FaceJitter;
            float waterOriginal = water.DepthRange;

            // A non-null singleton that is not the seed asset must stay exactly as it is.
            // It must be a real asset: the registry drops references to non-persistent objects.
            var foreignPalette = ScriptableObject.CreateInstance<ArtPaletteDef>();
            AssetDatabase.CreateAsset(foreignPalette, ForeignPalettePath);

            try
            {
                var regSo = new SerializedObject(registry);
                Prop(regSo, "artPalette").objectReferenceValue = foreignPalette;
                regSo.ApplyModifiedPropertiesWithoutUndo();

                Set(tuning, "castChargeTime", 1.23f);
                Set(rod, "castMaxDist", 77f);
                Set(bait, "rating", 7);
                Set(profile, "calmMin", 0.77f);
                Set(species, "baseValue", 99);
                Set(junk, "valueCoins", 4);
                Set(cues, "rows.Array.data[0].baseVolume", 0.37f);
                Set(palette, "faceJitter", 0.09f);
                Set(water, "depthRange", 7f);

                FishingContentBuilder.BuildAt(Root, registry);

                Assert.AreEqual(1.23f, tuning.CastChargeTime, "tuning was reconfigured");
                Assert.AreEqual(77f, rod.CastMaxDist, "rod was reconfigured");
                Assert.AreEqual(7, bait.Rating, "bait was reconfigured");
                Assert.AreEqual(0.77f, profile.CalmMin, "profile was reconfigured");
                Assert.AreEqual(99, species.BaseValue, "species was reconfigured");
                Assert.AreEqual(4, junk.ValueCoins, "junk was reconfigured");
                Assert.AreEqual(0.37f, cues.Rows[0].baseVolume, "cue table was reconfigured");
                Assert.AreEqual(0.09f, palette.FaceJitter, "palette was reconfigured");
                Assert.AreEqual(7f, water.DepthRange, "water settings were reconfigured");

                Assert.AreEqual(rods, registry.Rods.Count, "rods appended again");
                Assert.AreEqual(baits, registry.Baits.Count, "baits appended again");
                Assert.AreEqual(profiles, registry.FightProfiles.Count, "fightProfiles appended again");
                Assert.AreEqual(speciesCount, registry.Species.Count, "species appended again");
                Assert.AreEqual(junkCount, registry.Junk.Count, "junk appended again");
                Assert.AreSame(tuning, registry.FishingTuning);
                Assert.AreSame(cues, registry.SfxCueTable);
                Assert.AreSame(foreignPalette, registry.ArtPalette, "a non-null singleton was overwritten");
                Assert.AreSame(water, registry.WaterSettings);
            }
            finally
            {
                var restoreSo = new SerializedObject(registry);
                Prop(restoreSo, "artPalette").objectReferenceValue = palette;
                restoreSo.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.DeleteAsset(ForeignPalettePath);
                Set(tuning, "castChargeTime", tuningOriginal);
                Set(rod, "castMaxDist", rodOriginal);
                Set(bait, "rating", baitOriginal);
                Set(profile, "calmMin", profileOriginal);
                Set(species, "baseValue", speciesOriginal);
                Set(junk, "valueCoins", junkOriginal);
                Set(cues, "rows.Array.data[0].baseVolume", cueOriginal);
                Set(palette, "faceJitter", paletteOriginal);
                Set(water, "depthRange", waterOriginal);
            }
        }

        private static void Set(UnityEngine.Object target, string path, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(UnityEngine.Object target, string path, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Catches: the registry step not re-adding a seed asset that dropped out of a list (it is appended exactly once), a null singleton
        // not refilled, and a list entry that is already present being appended again.
        [Test]
        public void FishingContentBuilder_Build_AppendsMissingRegistryEntriesOnce()
        {
            var rod = Load<RodDef>(T(AssetPaths.RodsFolder) + "/rod_deepsea.asset");
            var water = Load<WaterSettingsDef>(T(AssetPaths.WaterSettingsAsset));

            var so = new SerializedObject(registry);
            SerializedProperty rodsProp = Prop(so, "rods");
            var original = new List<UnityEngine.Object>();
            for (int i = 0; i < rodsProp.arraySize; i++) original.Add(rodsProp.GetArrayElementAtIndex(i).objectReferenceValue);
            int index = original.IndexOf(rod);
            Assert.GreaterOrEqual(index, 0, "rod_deepsea is listed");

            try
            {
                rodsProp.DeleteArrayElementAtIndex(index);   // object-reference elements are cleared first; the second call removes the slot
                if (rodsProp.arraySize == original.Count) rodsProp.DeleteArrayElementAtIndex(index);
                Prop(so, "waterSettings").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(original.Count - 1, registry.Rods.Count, "setup removed one rod");
                Assert.IsNull(registry.WaterSettings, "setup cleared the water settings");

                FishingContentBuilder.BuildAt(Root, registry);

                Assert.AreEqual(original.Count, registry.Rods.Count, "the missing rod is appended once");
                int occurrences = 0;
                foreach (RodDef r in registry.Rods) if (r == rod) occurrences++;
                Assert.AreEqual(1, occurrences, "rod_deepsea appears exactly once");
                Assert.AreSame(water, registry.WaterSettings, "the null singleton is refilled with the seed asset");

                FishingContentBuilder.BuildAt(Root, registry);
                Assert.AreEqual(original.Count, registry.Rods.Count, "a second run appends nothing");
            }
            finally
            {
                var restore = new SerializedObject(registry);
                SerializedProperty list = Prop(restore, "rods");
                list.arraySize = original.Count;
                for (int i = 0; i < original.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = original[i];
                Prop(restore, "waterSettings").objectReferenceValue = water;
                restore.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Catches: Ensure<T> treating "exists but does not load as T" like "missing" and overwriting the file (CreateAsset replaces it).
        // A FishingTuningDef sits where the RodDef rod_twig belongs: the build must throw and leave that file as it was.
        [Test]
        public void FishingContentBuilder_BuildAt_RefusesToOverwriteAnAssetOfTheWrongType()
        {
            BuilderUtil.EnsureFolder(WrongTypeRoot);
            var wrongRegistry = ScriptableObject.CreateInstance<ContentRegistry>();
            AssetDatabase.CreateAsset(wrongRegistry, WrongTypeRoot + "/ContentRegistry.asset");

            string rodPath = WrongTypeRoot + "/Fishing/Rods/rod_twig.asset";
            BuilderUtil.EnsureFolder(WrongTypeRoot + "/Fishing/Rods");
            var squatter = ScriptableObject.CreateInstance<FishingTuningDef>();
            AssetDatabase.CreateAsset(squatter, rodPath);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<RodDef>(rodPath), "setup: the file does not load as a RodDef");

            var ex = Assert.Throws<InvalidOperationException>(() => FishingContentBuilder.BuildAt(WrongTypeRoot, wrongRegistry));
            Assert.AreEqual("asset exists but is not a RodDef: " + rodPath, ex.Message);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<FishingTuningDef>(rodPath), "the wrong-type asset was left in place, not overwritten");
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<RodDef>(rodPath), "and it was not replaced by a RodDef");
        }
    }
}
