// M3 T3: EcologyMigration (spec 12.6 step 1). Each test builds a complete temp content from the current seeds, ages it back to the M2 shape
// (four placeholder species, bluegill with its M2 values and gates, bait M3 fields 0, minLandDepth 0.8, dataVersion 1) and migrates it.
// SAFETY: every test works ONLY inside Assets/_Project/Tests/__Temp/EcologyMigrationTests (gitignored, deleted in TearDown) through
// EcologyMigration.MigrateAt(tempRoot, tempRegistry). No test path writes, edits or deletes anything under Assets/_Project/Content, so a failing
// or aborted run can never leave a committed asset changed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class EcologyMigrationTests
    {
        private const string TempParent = "Assets/_Project/Tests/__Temp";
        // A unique prefix per run (the fixtures share the gitignored __Temp parent, which is never deleted); the per-test serial follows it.
        private static readonly string RootBase = TempParent + "/EcologyMigrationTests_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        // A fresh folder per test: rebuilding the same asset paths right after deleting them makes the FishingContentBuilder wrong-type guard
        // trip on the stale GUID entry of the deleted file ("asset exists but is not a ...").
        private static string Root = RootBase;
        private static int s_serial;

        private static readonly string[] PlaceholderIds = { "perch", "carp", "trout", "catfish" };

        // The M3-D4 table, written out as literals so a seed typo cannot hide behind the seeds themselves.
        private static readonly (string id, FishTags tags, float affinity, float special)[] BaitTable =
        {
            ("bait_bare_hook", FishTags.None, 1.0f, 1.0f),
            ("bait_worm", FishTags.Bottom, 1.3f, 1.0f),
            ("bait_grub", FishTags.Surface, 1.3f, 1.0f),
            ("bait_minnow", FishTags.Predator, 1.4f, 1.0f),
            ("bait_shrimp", FishTags.Schooling, 1.3f, 1.2f),
            ("bait_raw_meat", FishTags.Predator | FishTags.Nocturnal, 1.5f, 1.5f),
        };

        private ContentRegistry registry;
        private Mesh tempMesh;

        private static string Rooted(string realPath) => FishingContentBuilder.RootedPath(Root, realPath);

        private static void DeleteTempFolders()
        {
            if (AssetDatabase.IsValidFolder(TempParent))
            {
                foreach (string folder in AssetDatabase.GetSubFolders(TempParent))
                    if (folder.StartsWith(RootBase, StringComparison.Ordinal)) AssetDatabase.DeleteAsset(folder);   // this run's and any leftover one
            }
        }

        [OneTimeTearDown]
        public void CleanUpAfterAll()
        {
            DeleteTempFolders();
        }

        [TearDown]
        public void DeleteTempContent()
        {
            DeleteTempFolders();
        }

        [SetUp]
        public void BuildAgedTempContent()
        {
            DeleteTempFolders();
            Root = RootBase + "_" + (++s_serial);
            try
            {
                BuilderUtil.EnsureFolder(Root);
                registry = ScriptableObject.CreateInstance<ContentRegistry>();   // dataVersion defaults to 1
                AssetDatabase.CreateAsset(registry, Root + "/ContentRegistry.asset");
                FishingContentBuilder.BuildAt(Root, registry);   // complete temp content from the current seeds, independent of any Configure signature

                // Age it to M2.
                string modelsFolder = FishingContentBuilder.ModelsFolderAt(Root);
                BuilderUtil.EnsureFolder(modelsFolder);
                tempMesh = new Mesh { name = "TempFish" };
                AssetDatabase.CreateAsset(tempMesh, modelsFolder + "/TempFish.asset");

                // The four M2 placeholder species with their M2 values (spec 9.6 table, copied from the pre-migration FishingSeedData).
                CreatePlaceholder("perch", "Perch", "darter", 300, 1200, 8, 1.0f, 3, 1.2f, FishRarity.Common, BodyShape.Long);
                CreatePlaceholder("carp", "Carp", "bulldog", 1000, 4000, 12, 1.2f, 1, 1.5f, FishRarity.Common, BodyShape.Round);
                CreatePlaceholder("trout", "Trout", "thrasher", 600, 2500, 20, 0.9f, 5, 2.0f, FishRarity.Uncommon, BodyShape.Long);
                CreatePlaceholder("catfish", "Catfish", "runner", 2000, 7000, 35, 1.4f, 6, 2.5f, FishRarity.Uncommon, BodyShape.Long);

                // bluegill with its M2 values and gates, model = the temp mesh, no tags.
                var bluegill = Load<FishSpeciesDef>(SpeciesPath("bluegill"));
                bluegill.Configure("bluegill", "Bluegill", FishRarity.Common, 5, 200, 800, 2.2f, 1.0f, ProfileAsset("steady"), BodyShape.Round,
                    new Color(0.32f, 0.50f, 0.62f, 1f), new Color(0.95f, 0.62f, 0.22f, 1f), 0.9f, 0f, false, tempMesh, FishTags.None);
                EditorUtility.SetDirty(bluegill);

                // Bait M3 fields as an M2 asset deserializes them: 0.
                foreach (var row in BaitTable) SetBaitFields(row.id, 0, 0f, 0f);

                SetTuningMinLandDepth(0.8f);
                AssetDatabase.SaveAssets();
            }
            catch
            {
                // NUnit does not run TearDown when SetUp throws: clean up here so a failed fixture never leaves Tests/__Temp behind.
                DeleteTempFolders();
                throw;
            }
        }

        // ---- helpers -----------------------------------------------------------------------------------------------

        private static string SpeciesPath(string id) => Rooted(AssetPaths.SpeciesFolder) + "/" + id + ".asset";

        private static string BaitPath(string id) => Rooted(AssetPaths.BaitsFolder) + "/" + id + ".asset";

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(o, "missing " + path);
            return o;
        }

        private static FightProfileDef ProfileAsset(string id) => Load<FightProfileDef>(Rooted(AssetPaths.FightProfilesFolder) + "/" + id + ".asset");

        private void CreatePlaceholder(string id, string name, string profileId, int minG, int maxG, int value, float biteWaitMod, int minBaitRating,
            float minDepth, FishRarity rarity, BodyShape shape)
        {
            var def = ScriptableObject.CreateInstance<FishSpeciesDef>();
            // minBaitRating / minDepth stay in the rows as the M2 values; T13 (M3-D3, M3-D55) removed the species fields they used to set.
            def.Configure(id, name, rarity, value, minG, maxG, 2.2f, 1.0f, ProfileAsset(profileId), shape, Color.red, Color.green, biteWaitMod, 0f,
                false, tempMesh);
            AssetDatabase.CreateAsset(def, SpeciesPath(id));
            AppendToRegistrySpecies(def);
        }

        private void AppendToRegistrySpecies(FishSpeciesDef def)
        {
            using (var so = new SerializedObject(registry))
            {
                SerializedProperty list = so.FindProperty("species");
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = def;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBaitFields(string id, int tags, float affinity, float special)
        {
            using (var so = new SerializedObject(Load<BaitDef>(BaitPath(id))))
            {
                so.FindProperty("affinityTags").intValue = tags;
                so.FindProperty("affinityMult").floatValue = affinity;
                so.FindProperty("specialMult").floatValue = special;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetTuningMinLandDepth(float value)
        {
            using (var so = new SerializedObject(Load<FishingTuningDef>(Rooted(AssetPaths.FishingTuningAsset))))
            {
                so.FindProperty("minLandDepth").floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private Dictionary<string, byte[]> SnapshotFiles()
        {
            var snapshot = new Dictionary<string, byte[]>();
            foreach (string file in Directory.GetFiles(BuilderUtil.Abs(Root), "*", SearchOption.AllDirectories))
                snapshot[file] = File.ReadAllBytes(file);
            return snapshot;
        }

        // ---- tests -------------------------------------------------------------------------------------------------

        // Catches: DeleteAsset without removing the list entry (a null entry); list removal without the file delete; DeleteArrayElementAtIndex
        // only nulling the element (arraySize stays 5); a migration that deletes model files; a stale index cache.
        [Test]
        public void EcologyMigration_RemovesM2PlaceholderSpeciesKeepsModels()
        {
            // M3 T10: the temp build creates the whole roster (EcologySeedData), so the placeholders come on top of it.
            int roster = FishingSeedData.Species.Count;
            Assert.That(registry.Species.Count, Is.EqualTo(roster + 4), "setup: the roster plus the four placeholders");

            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.True);

            foreach (string id in PlaceholderIds)
            {
                string path = SpeciesPath(id);
                Assert.That(File.Exists(BuilderUtil.Abs(path)), Is.False, id + ": the asset file is gone");
                Assert.That(AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path), Is.Null, id + ": no asset at its path");
            }

            using (var so = new SerializedObject(registry))
            {
                SerializedProperty list = so.FindProperty("species");
                Assert.That(list.arraySize, Is.EqualTo(roster), "the serialized species list holds the roster only");
                var listed = new List<FishSpeciesDef>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i).objectReferenceValue as FishSpeciesDef;
                    Assert.That(entry, Is.Not.Null, "no null entry is left in the list (index " + i + ")");
                    listed.Add(entry);
                }
                Assert.That(listed, Contains.Item(Load<FishSpeciesDef>(SpeciesPath("bluegill"))));
            }

            Assert.That(registry.TryGetSpecies("perch", out _), Is.False, "the index cache was cleared");
            Assert.That(registry.Species.Count, Is.EqualTo(roster));

            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(FishingContentBuilder.ModelsFolderAt(Root) + "/TempFish.asset"), Is.Not.Null,
                "the temp model mesh asset still exists");
            Assert.That(Load<FishSpeciesDef>(SpeciesPath("bluegill")).Model, Is.SameAs(tempMesh), "bluegill keeps its model");
            foreach (string file in new[] { "Fish_Perch.fbx", "Fish_Carp.fbx", "Fish_Trout.fbx", "Fish_Catfish.fbx" })
                Assert.That(File.Exists(BuilderUtil.Abs(AssetPaths.ModelsFolder + "/" + file)), Is.True, "the real model " + file + " stays");
        }

        // Catches: a partial overwrite (biteWaitMod 0.9 kept); a wrong seed (the literal checks); the model nulled by passing null to Configure;
        // a fight resolved from the real Content folder instead of the root.
        [Test]
        public void EcologyMigration_OverwritesBluegillWithRosterValues()
        {
            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.True);

            var def = Load<FishSpeciesDef>(SpeciesPath("bluegill"));
            SpeciesSeed seed = FishingSeedData.Species.First(s => s.Id == "bluegill");
            FishSpeciesData x = seed.Data;
            Assert.That(def.Id, Is.EqualTo(seed.Id));
            Assert.That(def.DisplayName, Is.EqualTo(seed.Name));
            Assert.That(def.Rarity, Is.EqualTo(x.Rarity));
            Assert.That(def.BaseValue, Is.EqualTo(x.BaseValue));
            Assert.That(def.WeightMinG, Is.EqualTo(x.WeightMinG));
            Assert.That(def.WeightMaxG, Is.EqualTo(x.WeightMaxG));
            Assert.That(def.WeightSkew, Is.EqualTo(x.WeightSkew));
            Assert.That(def.LengthShapeK, Is.EqualTo(x.LengthShapeK));
            Assert.That(def.Fight, Is.SameAs(ProfileAsset(seed.ProfileId)), "the fight profile comes from the migrated root");
            Assert.That(def.BodyShape, Is.EqualTo(seed.BodyShape));
            Assert.That(def.ColorA, Is.EqualTo(seed.ColorA));
            Assert.That(def.ColorB, Is.EqualTo(seed.ColorB));
            Assert.That(def.BiteWaitMod, Is.EqualTo(x.BiteWaitMod));
            Assert.That(def.HookWindow, Is.EqualTo(x.HookWindow));
            Assert.That(def.IsSpecial, Is.EqualTo(x.IsSpecial));
            Assert.That(def.Tags, Is.EqualTo(x.Tags));

            // The literal roster values (spec 12.6 row 2, M3-D35).
            Assert.That(def.WeightMinG, Is.EqualTo(200));
            Assert.That(def.WeightMaxG, Is.EqualTo(600));
            Assert.That(def.BaseValue, Is.EqualTo(8));
            Assert.That(def.Rarity, Is.EqualTo(FishRarity.Common));
            Assert.That(def.Tags, Is.EqualTo(FishTags.Schooling));
            Assert.That(def.BiteWaitMod, Is.EqualTo(1.0f));
            Assert.That(def.Fight.Id, Is.EqualTo("steady"));

            Assert.That(def.Model, Is.SameAs(tempMesh), "the model reference is kept");
        }

        // Catches: a bait skipped; tags written via enumValueIndex (Predator | Nocturnal = 3 would be read as index 3); other bait fields
        // reconfigured (create-if-missing values lost); the tuning write missing.
        [Test]
        public void EcologyMigration_WritesBaitM3FieldsOnExistingAssets()
        {
            var before = new Dictionary<string, (int rating, int stackMax, int maxRating, float biteSpeed, float junk, int nibbles, bool unlimited)>();
            foreach (var row in BaitTable)
            {
                var b = Load<BaitDef>(BaitPath(row.id));
                before[row.id] = (b.Rating, b.StackMax, b.MaxRating, b.BiteSpeedMod, b.JunkMult, b.MaxNibbles, b.Unlimited);
                Assert.That(b.AffinityMult, Is.EqualTo(0f), "setup: " + row.id + " starts as an M2 asset (affinityMult 0)");
            }

            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.True);

            foreach (var row in BaitTable)
            {
                var b = Load<BaitDef>(BaitPath(row.id));
                Assert.That(b.AffinityTags, Is.EqualTo(row.tags), row.id + " affinityTags");
                Assert.That(b.AffinityMult, Is.EqualTo(row.affinity), row.id + " affinityMult");
                Assert.That(b.SpecialMult, Is.EqualTo(row.special), row.id + " specialMult");
                var was = before[row.id];
                Assert.That((b.Rating, b.StackMax, b.MaxRating, b.BiteSpeedMod, b.JunkMult, b.MaxNibbles, b.Unlimited), Is.EqualTo(was),
                    row.id + ": the other bait fields are unchanged");
            }

            Assert.That(Load<FishingTuningDef>(Rooted(AssetPaths.FishingTuningAsset)).ToCore().MinLandDepth,
                Is.EqualTo(FishingSeedData.Tuning.MinLandDepth), "minLandDepth takes the seed value");
            Assert.That(FishingSeedData.Tuning.MinLandDepth, Is.EqualTo(0.5f), "M3-D52: the seed is 0.5");
        }

        // Catches: no version gate; a gate on > 2; a version bump that happens on every run; a second run that rewrites assets.
        [Test]
        public void EcologyMigration_BumpsDataVersionOnce_SecondRunIsNoOp()
        {
            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.True);
            Assert.That(registry.DataVersion, Is.EqualTo(2));

            Dictionary<string, byte[]> snapshot = SnapshotFiles();
            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.False, "the second run is a no-op");
            Dictionary<string, byte[]> after = SnapshotFiles();
            CollectionAssert.AreEquivalent(snapshot.Keys, after.Keys, "no file appeared or vanished");
            foreach (KeyValuePair<string, byte[]> kv in snapshot)
                Assert.That(after[kv.Key], Is.EqualTo(kv.Value), "unchanged: " + kv.Key);

            // Gate case: with the version already 2, planted M2-shaped data is NOT touched. The version gate, not step idempotency, prevents the run.
            var planted = ScriptableObject.CreateInstance<FishSpeciesDef>();
            planted.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1.0f, ProfileAsset("darter"), BodyShape.Long, Color.red, Color.green,
                1.0f, 0f, false, tempMesh);
            AssetDatabase.CreateAsset(planted, SpeciesPath("perch"));
            SetBaitFields("bait_worm", 0, 0f, 0f);
            AssetDatabase.SaveAssets();

            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.False, "version 2: the migration never runs again");
            Assert.That(File.Exists(BuilderUtil.Abs(SpeciesPath("perch"))), Is.True, "perch.asset still exists");
            Assert.That(Load<BaitDef>(BaitPath("bait_worm")).AffinityMult, Is.EqualTo(0f), "the bait is still 0");
        }

        // Catches: a migration that throws or keeps a null entry after a crash between steps (files deleted, registry not yet saved).
        [Test]
        public void EcologyMigration_InterruptedRun_ResumesCleanly()
        {
            Assert.That(AssetDatabase.DeleteAsset(SpeciesPath("perch")), Is.True, "setup: perch.asset deleted behind the registry's back");

            Assert.That(EcologyMigration.MigrateAt(Root, registry), Is.True);

            using (var so = new SerializedObject(registry))
            {
                SerializedProperty list = so.FindProperty("species");
                Assert.That(list.arraySize, Is.EqualTo(FishingSeedData.Species.Count), "the dangling perch entry and the other placeholders are gone");
                for (int i = 0; i < list.arraySize; i++)
                    Assert.That(list.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, "no null entry at " + i);
            }
            foreach (string id in PlaceholderIds)
                Assert.That(File.Exists(BuilderUtil.Abs(SpeciesPath(id))), Is.False, id + ": gone");
            Assert.That(registry.DataVersion, Is.EqualTo(2));
        }
    }
}
