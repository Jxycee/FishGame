// Creates the fishing, art and audio data assets (contract 6.2) from FishingSeedData and wires them into the ContentRegistry. Owned by T14.
// CREATE-IF-MISSING: every Configure below runs only inside the "asset does not exist yet" branch of Ensure(); an existing asset keeps the values
// a human or T15 tuned. The only edits to existing assets are the allowed ones (contract 6.2): adding a missing entry to a registry list, filling a
// null registry singleton, and filling a null FishSpeciesDef.model from Content/Models when the mesh has vertex colours and exactly one submesh.
// Reads the registry's serialized lists through SerializedObject, never the cached accessors (the registry may have been edited in memory).
// Does not call SaveAssets: ContentBuilder.Build saves after the hook.
// Existing assets are migrated by EcologyMigration (M3 T3), never here.
using System;
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class FishingContentBuilder
    {
        // The root the AssetPaths constants hang under; BuildAt re-roots every constant from here to the root it is given.
        private const string RealContentRoot = "Assets/_Project/Content";

        public static void Build()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
                throw new InvalidOperationException("ContentRegistry missing at " + AssetPaths.ContentRegistryAsset + " (ContentBuilder.EnsureRegistry runs first)");
            BuildAt(RealContentRoot, registry);
        }

        // Same build, rooted at contentRoot (the real Content folder, or a temp folder in tests) and wired into the given registry.
        // Every AssetPaths constant is re-rooted: contentRoot replaces "Assets/_Project/Content". Nothing outside contentRoot and the registry is written.
        internal static void BuildAt(string contentRoot, ContentRegistry registry)
        {
            if (string.IsNullOrEmpty(contentRoot) || !contentRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("contentRoot must be a project-relative path under Assets/: " + contentRoot, nameof(contentRoot));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            string Rooted(string path) => RootedPath(contentRoot, path);

            BuilderUtil.EnsureFolder(Rooted(AssetPaths.RodsFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.BaitsFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.FightProfilesFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.SpeciesFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.JunkFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.ArtFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.AudioFolder));

            var profiles = new List<FightProfileDef>();
            foreach (var p in FishingSeedData.Profiles)
            {
                FishFightProfile f = p.profile;
                profiles.Add(Ensure<FightProfileDef>(Rooted(AssetPaths.FightProfilesFolder) + "/" + p.id + ".asset", d =>
                    d.Configure(p.id, p.name, f.CalmMin, f.CalmMax, f.BurstMin, f.BurstMax,
                        f.FlipRate, f.DiveChance, f.BurstRamp, f.CalmDrift, f.CalmDriftRamp, f.FlipMinGap, f.LateralJitter,
                        f.StrainMod, f.MoveMult, f.ReelInMult, f.WavePeriod)));
            }

            var rods = new List<RodDef>();
            foreach (var r in FishingSeedData.Rods)
            {
                RodStats s = r.stats;
                rods.Add(Ensure<RodDef>(Rooted(AssetPaths.RodsFolder) + "/" + r.id + ".asset", d =>
                    d.Configure(r.id, r.name, s.Tier, s.CastMaxDist, s.ReelSpeed, s.LineStrength, s.AngleWindow, s.SteerRate, s.RelaxMult,
                        r.shopCost, r.firstCamp)));
            }

            var baits = new List<BaitDef>();
            foreach (var b in FishingSeedData.Baits)
            {
                BaitStats s = b.stats;
                baits.Add(Ensure<BaitDef>(Rooted(AssetPaths.BaitsFolder) + "/" + b.id + ".asset", d =>
                    d.Configure(b.id, b.name, s.Rating, s.StackMax, s.MaxRating, s.BiteSpeedMod, s.JunkMult, s.MaxNibbles, s.Unlimited,
                        s.AffinityTags, s.AffinityMult, s.SpecialMult)));
            }

            var junk = new List<JunkDef>();
            foreach (var j in FishingSeedData.Junk)
            {
                junk.Add(Ensure<JunkDef>(Rooted(AssetPaths.JunkFolder) + "/" + j.id + ".asset", d =>
                    d.Configure(j.id, j.name, j.value, j.shape, FishingSeedData.JunkColor(j.id))));
            }

            var species = new List<FishSpeciesDef>();
            foreach (SpeciesSeed seed in FishingSeedData.Species)
            {
                SpeciesSeed s = seed;
                species.Add(Ensure<FishSpeciesDef>(Rooted(AssetPaths.SpeciesFolder) + "/" + s.Id + ".asset", d =>
                {
                    string profilePath = Rooted(AssetPaths.FightProfilesFolder) + "/" + s.ProfileId + ".asset";
                    var fight = AssetDatabase.LoadAssetAtPath<FightProfileDef>(profilePath);
                    if (fight == null) throw new InvalidOperationException("species '" + s.Id + "': missing fight profile asset " + profilePath);
                    FishSpeciesData x = s.Data;
                    d.Configure(s.Id, s.Name, x.Rarity, x.BaseValue, x.WeightMinG, x.WeightMaxG, x.WeightSkew, x.LengthShapeK, fight, s.BodyShape,
                        s.ColorA, s.ColorB, x.BiteWaitMod, x.HookWindow, x.IsSpecial, x.MinBaitRating, x.MinDepth, null, x.Tags);
                }));
            }

            var tuning = Ensure<FishingTuningDef>(Rooted(AssetPaths.FishingTuningAsset), d =>
            {
                FishingTuning t = FishingSeedData.Tuning;
                d.Configure(in t);
            });
            var cues = Ensure<SfxCueTableDef>(Rooted(AssetPaths.SfxCueTableAsset), d => d.Configure(FishingSeedData.CueRows()));
            var palette = Ensure<ArtPaletteDef>(Rooted(AssetPaths.ArtPaletteAsset), d =>
                d.Configure(FishingSeedData.PaletteSlotNames, FishingSeedData.PaletteSeasons(), FishingSeedData.PaletteFaceJitter,
                    FishingSeedData.PaletteSkinTone, FishingSeedData.PalettePhases(), FishingSeedData.PaletteUiAccent, FishingSeedData.PaletteUiGood,
                    FishingSeedData.PaletteUiWarn, FishingSeedData.PaletteUiBad, FishingSeedData.PaletteUiPanel, FishingSeedData.PaletteUiText,
                    FishingSeedData.PaletteM2Season));
            var water = Ensure<WaterSettingsDef>(Rooted(AssetPaths.WaterSettingsAsset), d =>
            {
                WaveSet w = FishingSeedData.Waves;
                d.Configure(new[]
                {
                    new Vector4(w.W0.DirectionDeg, w.W0.Amplitude, w.W0.Wavelength, w.W0.Speed),
                    new Vector4(w.W1.DirectionDeg, w.W1.Amplitude, w.W1.Wavelength, w.W1.Speed),
                    new Vector4(w.W2.DirectionDeg, w.W2.Amplitude, w.W2.Wavelength, w.W2.Speed),
                }, FishingSeedData.WaterShallowColor, FishingSeedData.WaterDeepColor, FishingSeedData.WaterDepthRange, FishingSeedData.WaterFoamWidth);
            });

            FillSpeciesModels(species, ModelsFolderAt(contentRoot));
            WireRegistry(registry, rods, baits, profiles, species, junk, tuning, cues, palette, water);
        }

        // Re-roots one AssetPaths constant: contentRoot replaces RealContentRoot as the path prefix.
        // Throws when the path is not RealContentRoot or under it (Substring would otherwise cut a wrong prefix and silently build a made-up path).
        internal static string RootedPath(string contentRoot, string realPath)
        {
            if (realPath == null
                || !(realPath == RealContentRoot || realPath.StartsWith(RealContentRoot + "/", StringComparison.Ordinal)))
                throw new ArgumentException("path is not under " + RealContentRoot + ": " + realPath, nameof(realPath));
            return contentRoot + realPath.Substring(RealContentRoot.Length);
        }

        // The Models folder BuildAt reads species models from (FillSpeciesModels): re-rooted like every other constant.
        internal static string ModelsFolderAt(string contentRoot)
        {
            return RootedPath(contentRoot, AssetPaths.ModelsFolder);
        }

        // True when the mesh may be a species model: it has a vertex colour channel AND exactly one submesh (rulings D-Q4, G-Q6).
        internal static bool IsFishModelMesh(Mesh mesh)
        {
            return mesh != null && FishingValidators.HasVertexColours(mesh) && mesh.subMeshCount == 1;
        }

        // The creation branch is the only place configure runs: an existing asset is returned untouched.
        // A file that exists but does not load as T (wrong asset type, broken script reference) is never overwritten: it throws instead.
        private static T Ensure<T>(string path, Action<T> configureNew) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                if (File.Exists(BuilderUtil.Abs(path)) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                    throw new InvalidOperationException($"asset exists but is not a {typeof(T).Name}: {path}");
                asset = ScriptableObject.CreateInstance<T>();
                configureNew(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // C 6.2 allowed edit: fill a null model when Content/Models/<ModelFile> exists and its first mesh is usable; otherwise leave it null and log.
        private static void FillSpeciesModels(List<FishSpeciesDef> species, string modelsFolder)
        {
            foreach (SpeciesSeed seed in FishingSeedData.Species)
            {
                FishSpeciesDef def = species.Find(d => d.Id == seed.Id);
                if (def == null || def.Model != null) continue;

                string path = modelsFolder + "/" + seed.ModelFile;
                Mesh mesh = null;
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is Mesh m) { mesh = m; break; }
                }
                if (mesh == null) continue;   // no such file (or no mesh in it): nothing to fill, nothing to report

                if (!IsFishModelMesh(mesh))
                {
                    Debug.Log("FG CONTENT species " + seed.Id + ": " + seed.ModelFile + " has no vertex colours or has " + mesh.subMeshCount
                              + " submeshes; model left empty (fallback mesh)");
                    continue;
                }

                var so = new SerializedObject(def);
                SerializedProperty model = so.FindProperty("model");
                if (model == null) throw new InvalidOperationException("FishSpeciesDef has no serialized field 'model'");
                model.objectReferenceValue = mesh;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);
            }
        }

        // Append every seed asset the registry does not reference yet; fill a singleton only when it is null. Nothing else is touched.
        private static void WireRegistry(ContentRegistry registry, List<RodDef> rods, List<BaitDef> baits, List<FightProfileDef> profiles,
            List<FishSpeciesDef> species, List<JunkDef> junk, FishingTuningDef tuning, SfxCueTableDef cues, ArtPaletteDef palette, WaterSettingsDef water)
        {
            var so = new SerializedObject(registry);
            bool changed = false;
            changed |= AppendMissing(so, "rods", rods);
            changed |= AppendMissing(so, "baits", baits);
            changed |= AppendMissing(so, "fightProfiles", profiles);
            changed |= AppendMissing(so, "species", species);
            changed |= AppendMissing(so, "junk", junk);
            changed |= FillIfNull(so, "fishingTuning", tuning);
            changed |= FillIfNull(so, "sfxCueTable", cues);
            changed |= FillIfNull(so, "artPalette", palette);
            changed |= FillIfNull(so, "waterSettings", water);
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }

        private static bool AppendMissing<T>(SerializedObject so, string listName, List<T> assets) where T : UnityEngine.Object
        {
            SerializedProperty list = so.FindProperty(listName);
            if (list == null) throw new InvalidOperationException("ContentRegistry has no serialized field '" + listName + "'");
            bool changed = false;
            foreach (T asset in assets)
            {
                bool present = false;
                for (int i = 0; i < list.arraySize && !present; i++)
                    present = list.GetArrayElementAtIndex(i).objectReferenceValue == asset;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
                changed = true;
            }
            return changed;
        }

        private static bool FillIfNull(SerializedObject so, string propertyName, UnityEngine.Object asset)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) throw new InvalidOperationException("ContentRegistry has no serialized field '" + propertyName + "'");
            if (prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = asset;
            return true;
        }
    }
}
