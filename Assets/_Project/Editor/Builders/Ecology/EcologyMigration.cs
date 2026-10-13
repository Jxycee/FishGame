// Editor/Builders/Ecology/EcologyMigration.cs - the one-off spec 12.6 step 1 (C 7.3, M3-D1): M2 placeholder species -> the M3 roster start.
// Authority: none (editor-only build step; runs from ContentBuilder.Build before FishingContentBuilder, and from a menu item).
// Gate: a registry at dataVersion >= 2 is never touched. Every step is also idempotent on its own and dataVersion is written last, so a run
// interrupted between steps is finished by the next build. Allowed in-place edits only (C 6.2): registry species list + dataVersion,
// bluegill (every field, model kept), the three bait M3 fields, FishingTuning.minLandDepth. The FBX models are never touched.
using System;
using System.IO;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class EcologyMigration
    {
        public const int MigratedDataVersion = 2;

        private const string RealContentRoot = "Assets/_Project/Content";
        private const string BluegillId = "bluegill";
        private static readonly string[] DeletedSpeciesIds = { "perch", "carp", "trout", "catfish" };   // spec 12.6 step 1

        public static bool MigrateIfNeeded(ContentRegistry registry)
        {
            return MigrateAt(RealContentRoot, registry);
        }

        // Test seam (Tests/__Temp, like FishingContentBuilder.BuildAt): every AssetPaths constant is re-rooted under contentRoot.
        internal static bool MigrateAt(string contentRoot, ContentRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (string.IsNullOrEmpty(contentRoot) || !contentRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("contentRoot must be a project-relative path under Assets/: " + contentRoot, nameof(contentRoot));
            if (registry.DataVersion >= MigratedDataVersion) return false;

            string Rooted(string path) => FishingContentBuilder.RootedPath(contentRoot, path);
            RemovePlaceholderSpecies(registry, Rooted(AssetPaths.SpeciesFolder));
            OverwriteBluegill(Rooted(AssetPaths.SpeciesFolder), Rooted(AssetPaths.FightProfilesFolder));
            WriteBaitFields(Rooted(AssetPaths.BaitsFolder));
            WriteMinLandDepth(Rooted(AssetPaths.FishingTuningAsset));
            SetDataVersion(registry);
            AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("FishGame/Ecology/Migrate M2 Placeholders")]
        public static void MigrateMenu()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
            {
                Debug.LogError("FG MIGRATION: no ContentRegistry at " + AssetPaths.ContentRegistryAsset + " (run the build first)");
                return;
            }
            bool migrated = MigrateIfNeeded(registry);
            Debug.Log(migrated ? "FG MIGRATION: migrated to dataVersion " + MigratedDataVersion
                               : "FG MIGRATION: nothing to do (dataVersion " + registry.DataVersion + ")");
        }

        // (1) Registry entries first, so no list ever points at a deleted file; then the files. Null entries are dropped too: an interrupted run
        // (files deleted, registry not yet saved) leaves missing references, and a null species fails ContentRegistry.Validate.
        private static void RemovePlaceholderSpecies(ContentRegistry registry, string speciesFolder)
        {
            int removed = 0;
            using (var so = new SerializedObject(registry))
            {
                SerializedProperty list = so.FindProperty("species");
                if (list == null) throw new InvalidOperationException("ContentRegistry has no serialized field 'species'");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty element = list.GetArrayElementAtIndex(i);
                    var def = element.objectReferenceValue as FishSpeciesDef;
                    if (def != null && Array.IndexOf(DeletedSpeciesIds, def.Id) < 0) continue;
                    int before = list.arraySize;
                    element.objectReferenceValue = null;   // on object-reference arrays the first delete may only null the element
                    list.DeleteArrayElementAtIndex(i);
                    if (list.arraySize != before - 1)
                        throw new InvalidOperationException("registry species entry " + i + " was not removed (arraySize " + list.arraySize + ")");
                    removed++;
                }
                if (removed > 0)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();   // ContentRegistry.OnValidate clears the index caches
                    EditorUtility.SetDirty(registry);
                }
            }

            int deleted = 0;
            foreach (string id in DeletedSpeciesIds)
            {
                string path = speciesFolder + "/" + id + ".asset";
                if (!File.Exists(BuilderUtil.Abs(path))) continue;
                if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("could not delete " + path);
                deleted++;
            }
            Debug.Log("FG MIGRATION step 1: removed " + removed + " registry species entries, deleted " + deleted + " placeholder species assets (models kept)");
        }

        // (2) bluegill gets every field from its seed (spec 12.6 row 2 + M3-D35, gates 0/0); its model reference is kept.
        private static void OverwriteBluegill(string speciesFolder, string profilesFolder)
        {
            string path = speciesFolder + "/" + BluegillId + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
            if (def == null)
            {
                Debug.Log("FG MIGRATION step 2: no " + path + "; the fishing build creates it from the seed");
                return;
            }
            SpeciesSeed seed = Seed(BluegillId);
            string profilePath = profilesFolder + "/" + seed.ProfileId + ".asset";
            var fight = AssetDatabase.LoadAssetAtPath<FightProfileDef>(profilePath);
            if (fight == null) throw new InvalidOperationException("bluegill: missing fight profile asset " + profilePath);
            FishSpeciesData x = seed.Data;
            def.Configure(seed.Id, seed.Name, x.Rarity, x.BaseValue, x.WeightMinG, x.WeightMaxG, x.WeightSkew, x.LengthShapeK, fight, seed.BodyShape,
                seed.ColorA, seed.ColorB, x.BiteWaitMod, x.HookWindow, x.IsSpecial, x.MinBaitRating, x.MinDepth, def.Model, x.Tags);
            EditorUtility.SetDirty(def);
            Debug.Log("FG MIGRATION step 2: bluegill overwritten with its roster values (model kept)");
        }

        // (3) The three M3 bait fields on every existing seed bait (B4: a missing float deserializes as 0 and specialMult 0 would disable specials).
        private static void WriteBaitFields(string baitsFolder)
        {
            int written = 0;
            foreach (var b in FishingSeedData.Baits)
            {
                var def = AssetDatabase.LoadAssetAtPath<BaitDef>(baitsFolder + "/" + b.id + ".asset");
                if (def == null) continue;   // created later from the seed by FishingContentBuilder, with the fields
                using (var so = new SerializedObject(def))
                {
                    Prop(so, "affinityTags").intValue = (int)b.stats.AffinityTags;   // flags: intValue, never enumValueIndex
                    Prop(so, "affinityMult").floatValue = b.stats.AffinityMult;
                    Prop(so, "specialMult").floatValue = b.stats.SpecialMult;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(def);
                written++;
            }
            Debug.Log("FG MIGRATION step 3: wrote affinityTags/affinityMult/specialMult on " + written + " bait assets");
        }

        // (3b) M3-D52 / Q1: the tuning asset takes the seed minLandDepth (0.5 by default; equal values make this a no-op).
        private static void WriteMinLandDepth(string tuningPath)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<FishingTuningDef>(tuningPath);
            if (tuning == null)
            {
                Debug.Log("FG MIGRATION step 3b: no " + tuningPath + "; the fishing build creates it from the seed");
                return;
            }
            float seed = FishingSeedData.Tuning.MinLandDepth;
            using (var so = new SerializedObject(tuning))
            {
                Prop(so, "minLandDepth").floatValue = seed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(tuning);
            Debug.Log("FG MIGRATION step 3b: minLandDepth = " + seed.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        }

        // (4) Written last: only a complete run ends the migration.
        private static void SetDataVersion(ContentRegistry registry)
        {
            using (var so = new SerializedObject(registry))
            {
                Prop(so, "dataVersion").intValue = MigratedDataVersion;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(registry);
            Debug.Log("FG MIGRATION step 4: dataVersion = " + MigratedDataVersion);
        }

        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) throw new InvalidOperationException(so.targetObject.GetType().Name + " has no serialized field '" + name + "'");
            return p;
        }

        private static SpeciesSeed Seed(string id)
        {
            foreach (SpeciesSeed s in FishingSeedData.Species) if (s.Id == id) return s;
            throw new InvalidOperationException("no species seed '" + id + "' in FishingSeedData");
        }
    }
}
