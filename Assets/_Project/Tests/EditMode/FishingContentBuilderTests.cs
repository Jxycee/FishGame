// T14 extra: FishingContentBuilder is create-if-missing. Re-running it must never reconfigure an existing asset and must never append a
// second copy of a registry entry. The test edits the loaded assets IN MEMORY (SerializedObject, no SaveAssets), runs Build() and checks that
// the edits survived; every edit is restored in finally.
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
        private const string TempPalettePath = AssetPaths.ArtFolder + "/__TestForeignPalette.asset";

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(o, "missing " + path + ": run Tools/unity.ps1 build first");
            return o;
        }

        private static SerializedProperty Prop(SerializedObject so, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            Assert.IsNotNull(p, "no serialized property '" + path + "'");
            return p;
        }

        // Catches: a Configure outside an `if (asset == null)` branch (the value would reset to the seed), a singleton overwritten, and a
        // registry list that is appended again on every run (counts would double).
        [Test]
        public void FishingContentBuilder_Build_NeverReconfiguresExistingAssets()
        {
            var registry = Load<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            var tuning = Load<FishingTuningDef>(AssetPaths.FishingTuningAsset);
            var rod = Load<RodDef>(AssetPaths.RodsFolder + "/rod_twig.asset");
            var bait = Load<BaitDef>(AssetPaths.BaitsFolder + "/bait_worm.asset");
            var profile = Load<FightProfileDef>(AssetPaths.FightProfilesFolder + "/darter.asset");
            var species = Load<FishSpeciesDef>(AssetPaths.SpeciesFolder + "/perch.asset");
            var junk = Load<JunkDef>(AssetPaths.JunkFolder + "/tin_can.asset");
            var cues = Load<SfxCueTableDef>(AssetPaths.SfxCueTableAsset);
            var palette = Load<ArtPaletteDef>(AssetPaths.ArtPaletteAsset);
            var water = Load<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);

            int rods = registry.Rods.Count, baits = registry.Baits.Count, profiles = registry.FightProfiles.Count;
            int speciesCount = registry.Species.Count, junkCount = registry.Junk.Count, spots = registry.Spots.Count;

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
            AssetDatabase.CreateAsset(foreignPalette, TempPalettePath);

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

                FishingContentBuilder.Build();

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
                Assert.AreEqual(spots, registry.Spots.Count, "spots changed");
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
                AssetDatabase.DeleteAsset(TempPalettePath);
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
            var registry = Load<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            var rod = Load<RodDef>(AssetPaths.RodsFolder + "/rod_deepsea.asset");
            var water = Load<WaterSettingsDef>(AssetPaths.WaterSettingsAsset);

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

                FishingContentBuilder.Build();

                Assert.AreEqual(original.Count, registry.Rods.Count, "the missing rod is appended once");
                int occurrences = 0;
                foreach (RodDef r in registry.Rods) if (r == rod) occurrences++;
                Assert.AreEqual(1, occurrences, "rod_deepsea appears exactly once");
                Assert.AreSame(water, registry.WaterSettings, "the null singleton is refilled with the seed asset");

                FishingContentBuilder.Build();
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
    }
}
