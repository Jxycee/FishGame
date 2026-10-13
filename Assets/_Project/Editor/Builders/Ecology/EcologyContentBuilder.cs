// Creates the M3 ecology data assets (zones, weather table, unbaked shore grids, tuning, the harbour SpotDef) from EcologySeedData and wires them into
// the ContentRegistry. Owned by T10 (contract 6.2, 7.10).
// CREATE-IF-MISSING: every Configure runs only in the "asset does not exist yet" branch of Ensure(); an existing asset keeps the values a human, T12 or
// the shore-grid bake wrote (a baked grid is never touched: T16/T17 bake in place and the GUID stays stable). The only edits to existing assets are the
// allowed ones (6.2): a null lake_dock zone, a missing registry list entry, a null registry singleton, and the default M3 fields of the palette and the
// cue table. It creates NO species or junk assets: FishingContentBuilder stays their only creator (M3-D62); this class only checks they exist.
// Does not call SaveAssets: ContentBuilder.Build saves after the hooks.
using System;
using System.Collections.Generic;
using System.IO;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class EcologyContentBuilder
    {
        private const string RealContentRoot = "Assets/_Project/Content";
        private const string HarbourScene = SceneNames.SpotHarbourPier;   // in SceneNames.BuildOrder since T17 (CR-4: the spot is registered only once its scene is listed)

        public static void Build()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
                throw new InvalidOperationException("ContentRegistry missing at " + AssetPaths.ContentRegistryAsset + " (ContentBuilder.EnsureRegistry runs first)");
            BuildAt(RealContentRoot, registry);
        }

        // Same build, rooted at contentRoot (the real Content folder, or a temp folder in tests) and wired into the given registry.
        // Nothing outside contentRoot and the registry is written.
        internal static void BuildAt(string contentRoot, ContentRegistry registry)
        {
            if (string.IsNullOrEmpty(contentRoot) || !contentRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("contentRoot must be a project-relative path under Assets/: " + contentRoot, nameof(contentRoot));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            string Rooted(string path) => FishingContentBuilder.RootedPath(contentRoot, path);

            // 1. Folders.
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.EcologyFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.ZonesFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.WeatherFolder));
            BuilderUtil.EnsureFolder(Rooted(AssetPaths.ShoreGridsFolder));
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(Rooted(AssetPaths.HarbourPierSpotAsset)));

            // 2. Every species and junk asset the zones name must exist (FishingContentBuilder ran first).
            var speciesAssets = new Dictionary<string, FishSpeciesDef>(StringComparer.Ordinal);
            var junkAssets = new Dictionary<string, JunkDef>(StringComparer.Ordinal);
            foreach (ZoneSeed zone in EcologySeedData.Zones)
            {
                foreach (RuleSeed rule in zone.Rules)
                {
                    if (speciesAssets.ContainsKey(rule.SpeciesId)) continue;
                    string path = Rooted(AssetPaths.SpeciesFolder) + "/" + rule.SpeciesId + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                    if (def == null) throw new InvalidOperationException("ecology: missing species asset " + path + " (FishingContentBuilder runs first)");
                    speciesAssets.Add(rule.SpeciesId, def);
                }
                foreach (var junk in zone.Junk)
                {
                    if (junkAssets.ContainsKey(junk.junkId)) continue;
                    string path = Rooted(AssetPaths.JunkFolder) + "/" + junk.junkId + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<JunkDef>(path);
                    if (def == null) throw new InvalidOperationException("ecology: missing junk asset " + path + " (FishingContentBuilder runs first)");
                    junkAssets.Add(junk.junkId, def);
                }
            }

            // 3. The shared weather table (M3-D44).
            var weather = Ensure<WeatherTableDef>(Rooted(AssetPaths.WeatherTableAsset), d =>
            {
                WeatherTableData w = EcologySeedData.Weather;
                d.Configure("weather_temperate", w.Base, w.SeasonColumnMult);
            });

            // 4 + 5. Unbaked grids, then the zones.
            var zones = new List<FishingZoneDef>();
            var zoneById = new Dictionary<string, FishingZoneDef>(StringComparer.Ordinal);
            foreach (ZoneSeed zone in EcologySeedData.Zones)
            {
                ZoneSeed z = zone;
                string gridId = z.Id + "_grid";
                var grid = Ensure<ShoreGridDef>(Rooted(AssetPaths.ShoreGridsFolder) + "/" + gridId + ".asset", d =>
                    d.Configure(gridId, 0, 0, 1f, -60f, -60f, new ushort[0], new byte[0], new byte[0], new Vector3[0], ""));
                var def = Ensure<FishingZoneDef>(Rooted(AssetPaths.ZonesFolder) + "/" + z.Id + ".asset", d =>
                {
                    var bands = new List<SubAreaBand>();
                    for (int i = 0; i < z.Bands.Length; i++)
                    {
                        bands.Add(new SubAreaBand { id = z.BandIds[i], minShoreM = z.Bands[i].MinShoreM, maxShoreM = z.Bands[i].MaxShoreM, junkChance = z.Bands[i].JunkChance });
                    }
                    var rules = new List<SpawnRule>();
                    foreach (RuleSeed r in z.Rules)
                    {
                        rules.Add(new SpawnRule
                        {
                            fish = speciesAssets[r.SpeciesId], subAreaIds = (string[])r.SubAreaIds.Clone(), seasons = r.Seasons, weather = r.Weather, bands = r.Bands,
                            weight = r.Weight, minRodTier = r.MinRodTier, minBaitRating = 0, minDepth = 0f, special = r.Special, specialChance = r.SpecialChance,
                            catchLimitPerRun = r.CatchLimitPerRun, minPlayersInSession = 0,
                        });
                    }
                    var junk = new List<JunkWeight>();
                    foreach (var j in z.Junk) junk.Add(new JunkWeight { junk = junkAssets[j.junkId], weight = j.weight });
                    d.Configure(z.Id, z.Name, bands, rules, junk, weather, grid);
                });
                zones.Add(def);
                zoneById.Add(z.Id, def);
            }

            // 6. Tuning.
            var tuning = Ensure<EcologyTuningDef>(Rooted(AssetPaths.EcologyTuningAsset), d =>
            {
                EcologyTuning t = EcologySeedData.Tuning;
                d.Configure(t.HotspotFirstMinMin, t.HotspotFirstMinMax, t.HotspotGapMin, t.HotspotGapMax, t.HotspotDurationMin, t.HotspotDurationMax, t.HotspotRadiusM);
            });

            // 7. Spots: lake_dock gets its zone only when it has none; the harbour spot asset is created.
            var lakeSpot = AssetDatabase.LoadAssetAtPath<SpotDef>(Rooted(AssetPaths.LakeDockSpotAsset));
            if (lakeSpot != null && lakeSpot.Zone == null)
            {
                var so = new SerializedObject(lakeSpot);
                SerializedProperty zoneProp = so.FindProperty("zone");
                if (zoneProp == null) throw new InvalidOperationException("SpotDef has no serialized field 'zone'");
                zoneProp.objectReferenceValue = zoneById[ContentIds.LakeDock];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lakeSpot);
                Debug.Log("FG CONTENT ecology: filled lake_dock zone");
            }
            var harbourSpot = Ensure<SpotDef>(Rooted(AssetPaths.HarbourPierSpotAsset), d =>
                d.Configure(ContentIds.HarbourPier, "Harbour Pier", HarbourScene, 1, zoneById[ContentIds.HarbourPier]));

            // 8. Registry lists and singletons (through SerializedObject, never the cached accessors).
            var reg = new SerializedObject(registry);
            bool changed = AppendMissing(reg, "zones", zones);
            changed |= FillIfNull(reg, "ecologyTuning", tuning);
            if (Array.IndexOf(SceneNames.BuildOrder, HarbourScene) >= 0)
            {
                changed |= AppendMissing(reg, "spots", new List<SpotDef> { harbourSpot });   // CR-4: only once the scene is in the build order
            }
            else
            {
                Debug.Log("FG CONTENT ecology: harbour_pier spot not registered until Spot_HarbourPier is in the build order (T17)");
            }
            if (changed)
            {
                reg.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);
            }

            // 9. M3 fields on the existing palette and cue table (allowed in-place fill; the two palette fields arrive together).
            ArtPaletteDef palette = registry.ArtPalette;
            if (palette != null && palette.WeatherLooks.Count == 0)
            {
                SunMoonSettings sunMoon = EcologySeedData.SunMoon;
                palette.ConfigureM3(in sunMoon, EcologySeedData.WeatherLooks);
                EditorUtility.SetDirty(palette);
                Debug.Log("FG CONTENT ecology: filled ArtPalette sunMoon and weatherLooks");
            }
            SfxCueTableDef cues = registry.SfxCueTable;
            if (cues != null)
            {
                var cso = new SerializedObject(cues);
                SerializedProperty arr = cso.FindProperty("seasonAmbience");
                if (arr == null) throw new InvalidOperationException("SfxCueTableDef has no serialized field 'seasonAmbience'");
                if (arr.arraySize == 0)
                {
                    SeasonAmbience[] seeds = EcologySeedData.SeasonAmbience;
                    arr.arraySize = seeds.Length;
                    for (int i = 0; i < seeds.Length; i++)
                    {
                        SerializedProperty e = arr.GetArrayElementAtIndex(i);
                        e.FindPropertyRelative("windGain").floatValue = seeds[i].windGain;
                        e.FindPropertyRelative("windLfoHz").floatValue = seeds[i].windLfoHz;
                        e.FindPropertyRelative("lapIntervalMin").floatValue = seeds[i].lapIntervalMin;
                        e.FindPropertyRelative("lapIntervalMax").floatValue = seeds[i].lapIntervalMax;
                    }
                    cso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cues);
                    Debug.Log("FG CONTENT ecology: filled SfxCueTable seasonAmbience");
                }
            }
        }

        // The creation branch is the only place configure runs: an existing asset is returned untouched. A file that exists but does not load as T
        // is never overwritten: it throws instead.
        private static T Ensure<T>(string path, Action<T> configureNew) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                if (BuilderUtil.AssetPathOccupied(path))
                    throw new InvalidOperationException($"asset exists but is not a {typeof(T).Name}: {path}");
                asset = ScriptableObject.CreateInstance<T>();
                configureNew(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
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
