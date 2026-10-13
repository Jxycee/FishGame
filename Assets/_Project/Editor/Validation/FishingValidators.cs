// Fishing data validators (plan data-tuning 3.4, contract 7.14). Owned by T14.
// FishingContent: every seed id has an asset at its contract 6.2 path that the registry lists, and every value is inside the plan section 1
// ranges (NaN-safe comparisons: written as !(v >= min && v <= max)). Null list entries and id problems are reported by ContentRegistry.Validate
// (Validators.ContentIds), not repeated here. SpeciesModels: a non-null species model must come from Content/Models/*.fbx with vertex colours
// and exactly one submesh (ruling G-Q6).
// The registry checks read the registry's serialized lists and the Defs directly (never the cached index accessors), and take a ContentRegistry so
// tests can break one value in memory (internal seams CheckRegistry, CheckSeedAssets, CheckSpeciesModel).
using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishGame.EditorTools
{
    public static class FishingValidators
    {
        private const float DefaultMaxDistance = 30f;   // AudioBus default when a cue's maxDistance is 0
        private const float MaxFogDensity = 0.033f;     // spec 10.3 readability rule

        public static List<string> All()
        {
            return Prefixed(FishingContent(), SpeciesModels());
        }

        // The prefixing seam: tests feed it in-memory results so the prefixes can be checked without editing any committed asset.
        internal static List<string> Prefixed(List<string> contentErrors, List<string> modelErrors)
        {
            var all = new List<string>();
            foreach (string error in contentErrors) all.Add("FishingContent: " + error);
            foreach (string error in modelErrors) all.Add("SpeciesModels: " + error);
            return all;
        }

        public static List<string> FishingContent()
        {
            var errors = new List<string>();
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
            {
                errors.Add("ContentRegistry missing at " + AssetPaths.ContentRegistryAsset);
                return errors;
            }
            errors.AddRange(CheckSeedAssets(registry, path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)));
            errors.AddRange(CheckRegistry(registry));
            return errors;
        }

        public static List<string> SpeciesModels()
        {
            var errors = new List<string>();
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            if (registry == null)
            {
                errors.Add("ContentRegistry missing at " + AssetPaths.ContentRegistryAsset);
                return errors;
            }
            foreach (FishSpeciesDef species in registry.Species)
            {
                if (species == null || species.Model == null) continue;
                errors.AddRange(CheckSpeciesModel(species.Id, species.Model, AssetDatabase.GetAssetPath(species.Model)));
            }
            return errors;
        }

        // ---- species models ----

        internal static bool HasVertexColours(Mesh mesh)
        {
            return mesh != null && mesh.HasVertexAttribute(VertexAttribute.Color);
        }

        // A null model is allowed (the fallback mesh is used).
        internal static List<string> CheckSpeciesModel(string speciesId, Mesh model, string assetPath)
        {
            var errors = new List<string>();
            if (model == null) return errors;
            string path = assetPath ?? "";
            string label = "species " + speciesId + ": ";
            bool fromModels = path.StartsWith(AssetPaths.ModelsFolder + "/", StringComparison.Ordinal)
                              && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            if (!fromModels) errors.Add(label + "model is not from " + AssetPaths.ModelsFolder + "/*.fbx (" + path + ")");
            if (!HasVertexColours(model)) errors.Add(label + "model " + path + " has no vertex colours");
            if (model.subMeshCount != 1) errors.Add(label + "model " + path + " has " + model.subMeshCount + " submeshes, expected 1");
            return errors;
        }

        // ---- seed assets: exist at their paths and are listed in the registry ----

        internal static List<string> CheckSeedAssets(ContentRegistry registry, Func<string, UnityEngine.Object> load)
        {
            var errors = new List<string>();
            foreach (var r in FishingSeedData.Rods) CheckListed(errors, load, registry.Rods, AssetPaths.RodsFolder + "/" + r.id + ".asset", "rods");
            foreach (var b in FishingSeedData.Baits) CheckListed(errors, load, registry.Baits, AssetPaths.BaitsFolder + "/" + b.id + ".asset", "baits");
            foreach (var p in FishingSeedData.Profiles)
                CheckListed(errors, load, registry.FightProfiles, AssetPaths.FightProfilesFolder + "/" + p.id + ".asset", "fightProfiles");
            foreach (SpeciesSeed s in FishingSeedData.Species)
                CheckListed(errors, load, registry.Species, AssetPaths.SpeciesFolder + "/" + s.Id + ".asset", "species");
            foreach (var j in FishingSeedData.Junk) CheckListed(errors, load, registry.Junk, AssetPaths.JunkFolder + "/" + j.id + ".asset", "junk");
            CheckSingleton(errors, load, AssetPaths.FishingTuningAsset, registry.FishingTuning, "fishingTuning");
            CheckSingleton(errors, load, AssetPaths.SfxCueTableAsset, registry.SfxCueTable, "sfxCueTable");
            CheckSingleton(errors, load, AssetPaths.ArtPaletteAsset, registry.ArtPalette, "artPalette");
            CheckSingleton(errors, load, AssetPaths.WaterSettingsAsset, registry.WaterSettings, "waterSettings");
            return errors;
        }

        private static void CheckListed<T>(List<string> errors, Func<string, UnityEngine.Object> load, IReadOnlyList<T> list, string path, string listName)
            where T : UnityEngine.Object
        {
            UnityEngine.Object asset = load(path);
            if (asset == null)
            {
                errors.Add("missing asset " + path);
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == asset) return;
            }
            errors.Add(path + " is not listed in ContentRegistry." + listName);
        }

        private static void CheckSingleton(List<string> errors, Func<string, UnityEngine.Object> load, string path, UnityEngine.Object current, string propertyName)
        {
            UnityEngine.Object asset = load(path);
            if (asset == null)
            {
                errors.Add("missing asset " + path);
                return;
            }
            if (current != asset) errors.Add(path + " is not listed in ContentRegistry." + propertyName);
        }

        // ---- values: ranges and cross-field rules (plan section 1) ----

        private static string Fmt(float v)
        {
            return v.ToString("G", CultureInfo.InvariantCulture);
        }

        private static void Range(List<string> errors, string label, string field, float v, float min, float max)
        {
            if (!(v >= min && v <= max))
                errors.Add(label + ": " + field + " = " + Fmt(v) + " outside [" + Fmt(min) + ", " + Fmt(max) + "]");
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        // v >= min with no upper bound; a non-finite value is reported as such.
        private static void AtLeast(List<string> errors, string label, string field, float v, float min)
        {
            if (!IsFinite(v)) errors.Add(label + ": " + field + " = " + Fmt(v) + " is not finite");
            else if (!(v >= min)) errors.Add(label + ": " + field + " = " + Fmt(v) + " below minimum " + Fmt(min));
        }

        private static void Positive(List<string> errors, string label, string field, float v)
        {
            if (!IsFinite(v)) errors.Add(label + ": " + field + " = " + Fmt(v) + " is not finite");
            else if (!(v > 0f)) errors.Add(label + ": " + field + " = " + Fmt(v) + " must be greater than 0");
        }

        private static void Rule(List<string> errors, string label, bool ok, string text)
        {
            if (!ok) errors.Add(label + ": " + text);
        }

        internal static List<string> CheckRegistry(ContentRegistry registry)
        {
            var errors = new List<string>();
            FishingTuningDef tuning = registry.FishingTuning;
            if (tuning != null) CheckTuning(errors, tuning);
            foreach (RodDef rod in registry.Rods) if (rod != null) CheckRod(errors, rod, tuning);
            CheckBaits(errors, registry.Baits);
            foreach (FightProfileDef profile in registry.FightProfiles) if (profile != null) CheckProfile(errors, profile);
            foreach (FishSpeciesDef species in registry.Species) if (species != null) CheckSpecies(errors, species, registry.Baits);
            foreach (JunkDef junk in registry.Junk) if (junk != null) Range(errors, "junk " + junk.Id, "valueCoins", junk.ValueCoins, 0f, 5f);
            if (registry.ArtPalette != null) CheckPalette(errors, registry.ArtPalette);
            if (registry.WaterSettings != null) CheckWater(errors, registry.WaterSettings);
            if (registry.SfxCueTable != null) CheckCues(errors, registry.SfxCueTable);
            return errors;
        }

        private static void CheckTuning(List<string> e, FishingTuningDef t)
        {
            const string L = "tuning";
            Range(e, L, "castChargeTime", t.CastChargeTime, 0.6f, 1.5f);
            Range(e, L, "castMinDist", t.CastMinDist, 2f, 4f);
            Range(e, L, "minLandDepth", t.MinLandDepth, 0.5f, 1.5f);
            Range(e, L, "biteWaitMin", t.BiteWaitMin, 2f, 8f);
            Range(e, L, "biteWaitMax", t.BiteWaitMax, 10f, 30f);
            Rule(e, L, t.BiteWaitMin < t.BiteWaitMax, "biteWaitMin < biteWaitMax");
            Range(e, L, "minBiteWait", t.MinBiteWait, 0.5f, 2f);
            Rule(e, L, t.MinBiteWait <= t.BiteWaitMin, "minBiteWait <= biteWaitMin");
            Range(e, L, "hookBeat", t.HookBeat, 0.15f, 0.4f);
            Range(e, L, "landDistance", t.LandDistance, 0.5f, 3f);
            Range(e, L, "spoolExtra", t.SpoolExtra, 6f, 20f);
            Range(e, L, "relaxRate", t.RelaxRate, 0.5f, 3f);
            Range(e, L, "strainScale", t.StrainScale, 0.3f, 1.5f);
            Range(e, L, "strainScaleAssist", t.StrainScaleAssist, 0.2f, 1f);
            Rule(e, L, t.StrainScaleAssist <= t.StrainScale, "strainScaleAssist <= strainScale");
            Range(e, L, "slackEscapeTime", t.SlackEscapeTime, 8f, 20f);
            Range(e, L, "lateralMaxDeg", t.LateralMaxDeg, 60f, 85f);
            Range(e, L, "hookedLeash", t.HookedLeash, 2f, 5f);
            Range(e, L, "junkDragSpeed", t.JunkDragSpeed, 2f, 5f);
            Range(e, L, "resultTimeout", t.ResultTimeout, 1f, 10f);
            Range(e, L, "commitTimeout", t.CommitTimeout, 1f, 10f);
            Range(e, L, "hookTolerance", t.HookTolerance, 0.5f, 1.5f);
            Range(e, L, "maxFightTime", t.MaxFightTime, 180f, 400f);
            Range(e, L, "rodStateHz", t.RodStateHz, 10f, 20f);
            Range(e, L, "creelMaxEntries", t.CreelMaxEntries, 1f, 99f);
            Range(e, L, "creelMaxKg", t.CreelMaxKg, 1f, 100f);
            Range(e, L, "cleanCatchMaxTension", t.CleanCatchMaxTension, 0.5f, 0.95f);
            Range(e, L, "pityAfterFailures", t.PityAfterFailures, 0f, 10f);
            Range(e, L, "pityStrainMult", t.PityStrainMult, 0.5f, 1f);
            Range(e, L, "pityGraceMult", t.PityGraceMult, 1f, 3f);
            Range(e, L, "pityHookWindowBonus", t.PityHookWindowBonus, 0f, 1f);
            // R72 Rust fight baseline
            Range(e, L, "strainBarFull", t.StrainBarFull, 1f, 20f);
            Range(e, L, "strainSnap", t.StrainSnap, 1f, 30f);
            Rule(e, L, t.StrainSnap > t.StrainBarFull, "strainSnap > strainBarFull");
            Range(e, L, "strainRestInput", t.StrainRestInput, 0f, 3f);
            Range(e, L, "strainReelRun", t.StrainReelRun, 0f, 6f);
            Range(e, L, "strainReelBack", t.StrainReelBack, 0f, 6f);
            Range(e, L, "strainCounterSteer", t.StrainCounterSteer, 0f, 3f);
            Range(e, L, "strainOtherSteer", t.StrainOtherSteer, 0f, 3f);
            Range(e, L, "sideMoveSpeed", t.SideMoveSpeed, 0f, 5f);
            Range(e, L, "backMoveSpeed", t.BackMoveSpeed, 0f, 5f);
            Range(e, L, "tireSteerRate", t.TireSteerRate, 0f, 5f);
            Range(e, L, "tireCounterRate", t.TireCounterRate, 0f, 5f);
            Range(e, L, "tireDecayRate", t.TireDecayRate, 0f, 1f);
            Range(e, L, "tireRunFull", t.TireRunFull, 1f, 100f);
            Range(e, L, "tireReelFull", t.TireReelFull, 0.5f, 50f);
            Range(e, L, "tireReelMaxMult", t.TireReelMaxMult, 1f, 5f);
            Range(e, L, "backPullMargin", t.BackPullMargin, 0f, 10f);
            Range(e, L, "fightTimeout", t.FightTimeout, 30f, 300f);
            Rule(e, L, t.MaxFightTime > t.FightTimeout, "maxFightTime > fightTimeout");
        }

        private static void CheckRod(List<string> e, RodDef r, FishingTuningDef tuning)
        {
            string l = "rod " + r.Id;
            Range(e, l, "tier", r.Tier, 0f, 3f);
            Range(e, l, "castMaxDist", r.CastMaxDist, 8f, 40f);
            if (tuning != null) Rule(e, l, r.CastMaxDist > tuning.CastMinDist, "castMaxDist > castMinDist");
            Range(e, l, "reelSpeed", r.ReelSpeed, 0.3f, 2f);
            Range(e, l, "lineStrength", r.LineStrength, 0.5f, 2.5f);
            Range(e, l, "angleWindow", r.AngleWindow, 0.3f, 0.9f);
            // PARKED (R72): angleWindow and PinnedLateral are unused by the baseline fight (no pinned state, no out-of-window strain). The rule and
            // the range above stay so the parked data keeps a sane shape for the friendlier pass; neither feeds any fight maths today.
            Rule(e, l, r.AngleWindow < FishingConstants.PinnedLateral, "angleWindow < PinnedLateral");
            Range(e, l, "steerRate", r.SteerRate, 0.3f, 1.2f);
            Range(e, l, "relaxMult", r.RelaxMult, 0.5f, 1.5f);
            AtLeast(e, l, "shopCost", r.ShopCost, 0f);
            Range(e, l, "firstCamp", r.FirstCamp, 0f, 6f);
        }

        private static void CheckBaits(List<string> e, IReadOnlyList<BaitDef> baits)
        {
            int unlimited = 0;
            foreach (BaitDef b in baits)
            {
                if (b == null) continue;
                string l = "bait " + b.Id;
                Range(e, l, "rating", b.Rating, 1f, 10f);
                Range(e, l, "stackMax", b.StackMax, 1f, 5f);
                Range(e, l, "maxRating", b.MaxRating, b.Rating, 20f);
                Range(e, l, "biteSpeedMod", b.BiteSpeedMod, 0.5f, 1.5f);
                Range(e, l, "junkMult", b.JunkMult, 0f, 2f);
                Range(e, l, "maxNibbles", b.MaxNibbles, 0f, FishingConstants.MaxNibbleSlots);
                if (b.Unlimited) unlimited++;
            }
            Rule(e, "baits", unlimited == 1, "exactly one bait must be unlimited (found " + unlimited + ")");
        }

        private static void CheckProfile(List<string> e, FightProfileDef p)
        {
            string l = "profile " + p.Id;
            Range(e, l, "calmMin", p.CalmMin, 0.2f, 5f);
            Range(e, l, "calmMax", p.CalmMax, p.CalmMin, 6f);
            Range(e, l, "burstMin", p.BurstMin, 0.3f, 6f);
            Range(e, l, "burstMax", p.BurstMax, p.BurstMin, 10f);
            Range(e, l, "flipRate", p.FlipRate, 0f, 2f);
            Range(e, l, "diveChance", p.DiveChance, 0f, 1f);
            Range(e, l, "burstRamp", p.BurstRamp, 0f, 2f);
            Range(e, l, "calmDrift", p.CalmDrift, 0f, 0.5f);
            Range(e, l, "calmDriftRamp", p.CalmDriftRamp, 0f, 5f);
            Range(e, l, "flipMinGap", p.FlipMinGap, 0.2f, 2f);
            Range(e, l, "lateralJitter", p.LateralJitter, 0f, 0.5f);
            Range(e, l, "strainMod", p.StrainMod, 0.1f, 3f);
            Range(e, l, "moveMult", p.MoveMult, 0f, 3f);
            Range(e, l, "reelInMult", p.ReelInMult, 0.1f, 5f);
            Range(e, l, "wavePeriod", p.WavePeriod, 0.5f, 8f);
            if (p.Id == "thrasher") Rule(e, l, p.CalmMin >= 1.0f, "calmMin >= 1.0");
        }

        private static void CheckSpecies(List<string> e, FishSpeciesDef s, IReadOnlyList<BaitDef> baits)
        {
            string l = "species " + s.Id;
            AtLeast(e, l, "baseValue", s.BaseValue, 1f);
            AtLeast(e, l, "weightMinG", s.WeightMinG, 1f);
            Rule(e, l, s.WeightMaxG > s.WeightMinG, "weightMaxG > weightMinG");
            Range(e, l, "weightSkew", s.WeightSkew, FishingConstants.SkewMin, FishingConstants.SkewMax);
            Range(e, l, "lengthShapeK", s.LengthShapeK, 0.5f, 2f);
            Range(e, l, "biteWaitMod", s.BiteWaitMod, 0.5f, 2f);
            Rule(e, l, s.HookWindow == 0f || (s.HookWindow >= 0.8f && s.HookWindow <= 1.2f), "hookWindow is 0 or in [0.8, 1.2]");
            Range(e, l, "minBaitRating", s.MinBaitRating, 0f, 20f);
            Range(e, l, "minDepth", s.MinDepth, 0f, FishingConstants.DepthMultFullDepth);

            // Reachable: some bait reaches the species at the full depth (a null fight is reported by ContentRegistry.Validate).
            FishSpeciesData data = s.ToCore(0);
            bool reachable = false;
            foreach (BaitDef b in baits)
            {
                if (b == null) continue;
                if (PlaceholderRoller.IsEligible(in data, b.ToCore().EffRating(1), FishingConstants.DepthMultFullDepth)) { reachable = true; break; }
            }
            if (!reachable) e.Add(l + ": not reachable with any bait at depth <= 6 m");
        }

        private static void CheckPalette(List<string> e, ArtPaletteDef a)
        {
            const string L = "palette";
            Rule(e, L, a.SlotNames.Count == 16, "16 slot names (found " + a.SlotNames.Count + ")");
            Rule(e, L, a.Seasons.Count == 4, "4 seasons (found " + a.Seasons.Count + ")");
            for (int i = 0; i < a.Seasons.Count; i++)
            {
                PaletteSeason s = a.Seasons[i];
                int n = s == null || s.colors == null ? 0 : s.colors.Length;
                Rule(e, L + " season " + i, n == 16, "16 colours (found " + n + ")");
            }
            Rule(e, L, a.Phases.Count == 4, "4 phases (found " + a.Phases.Count + ")");
            for (int i = 0; i < a.Phases.Count; i++)
            {
                string l = L + " phase " + i;
                float start = a.Phases[i].startFraction;
                if (i == 0) Rule(e, l, start == 0f, "startFraction must be 0");
                else Rule(e, l, start > a.Phases[i - 1].startFraction, "startFraction must be greater than the previous phase");
                Rule(e, l, start < 1f, "startFraction must be below 1");
                float fog = a.Phases[i].fogDensity;
                if (!(fog > 0f && fog <= MaxFogDensity)) e.Add(l + ": fogDensity = " + Fmt(fog) + " outside (0, " + Fmt(MaxFogDensity) + "]");
            }
            Range(e, L, "faceJitter", a.FaceJitter, 0f, 0.1f);
            Range(e, L, "m2Season", a.M2Season, 0f, 3f);
            for (int i = 0; i < a.Seasons.Count; i++)
            {
                PaletteSeason s = a.Seasons[i];
                if (s == null || s.colors == null) continue;
                for (int c = 0; c < s.colors.Length; c++) FiniteColour(e, L + " season " + i, "colour " + c, s.colors[c]);
            }
            FiniteColour(e, L, "skinTone", a.SkinTone);
            FiniteColour(e, L, "uiAccent", a.UiAccent);
            FiniteColour(e, L, "uiGood", a.UiGood);
            FiniteColour(e, L, "uiWarn", a.UiWarn);
            FiniteColour(e, L, "uiBad", a.UiBad);
            FiniteColour(e, L, "uiPanel", a.UiPanel);
            FiniteColour(e, L, "uiText", a.UiText);
        }

        private static void CheckWater(List<string> e, WaterSettingsDef w)
        {
            Rule(e, "water", w.DirAmpLenSpeed.Count == 3, "exactly 3 waves (found " + w.DirAmpLenSpeed.Count + ")");
            float sum = 0f;
            for (int i = 0; i < w.DirAmpLenSpeed.Count; i++)
            {
                Vector4 v = w.DirAmpLenSpeed[i];
                string l = "water wave " + i;
                if (!IsFinite(v.x)) e.Add(l + ": direction = " + Fmt(v.x) + " is not finite");
                Range(e, l, "amplitude", v.y, 0f, 0.08f);
                AtLeast(e, l, "wavelength", v.z, 5f);
                Range(e, l, "speed", v.w, 0f, 3f);
                sum += v.y;
            }
            if (!(sum >= 0f && sum <= 0.12f)) e.Add("water: amplitude sum = " + Fmt(sum) + " outside [0, 0.12]");
            Positive(e, "water", "depthRange", w.DepthRange);
            Positive(e, "water", "foamWidth", w.FoamWidth);
            FiniteColour(e, "water", "shallowColor", w.ShallowColor);
            FiniteColour(e, "water", "deepColor", w.DeepColor);
        }

        private static void FiniteColour(List<string> e, string label, string field, Color c)
        {
            if (!IsFinite(c.r) || !IsFinite(c.g) || !IsFinite(c.b) || !IsFinite(c.a))
                e.Add(label + ": " + field + " has a non-finite channel (" + Fmt(c.r) + ", " + Fmt(c.g) + ", " + Fmt(c.b) + ", " + Fmt(c.a) + ")");
        }

        private static void CheckCues(List<string> e, SfxCueTableDef table)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SfxCueRow row in table.Rows)
            {
                string l = "cue " + row.id;
                if (!seen.Add(row.id ?? "")) e.Add(l + ": duplicate id");
                if (!SfxRecipes.TryGet(row.id, out _)) e.Add(l + ": unknown to SfxRecipes");
                Range(e, l, "baseVolume", row.baseVolume, 0f, 1f);

                bool minOk = IsFinite(row.minDistance), maxOk = IsFinite(row.maxDistance);
                if (!minOk) e.Add(l + ": minDistance = " + Fmt(row.minDistance) + " is not finite");
                if (!maxOk) e.Add(l + ": maxDistance = " + Fmt(row.maxDistance) + " is not finite");
                if (!IsFinite(row.minInterval)) e.Add(l + ": minInterval = " + Fmt(row.minInterval) + " is not finite");
                else if (!(row.minInterval >= 0f)) e.Add(l + ": minInterval = " + Fmt(row.minInterval) + " must be >= 0");

                if (minOk && !(row.minDistance > 0f)) e.Add(l + ": minDistance = " + Fmt(row.minDistance) + " must be greater than 0");
                if (minOk && maxOk && row.minDistance > 0f)
                {
                    if (row.maxDistance < 0f)
                    {
                        e.Add(l + ": maxDistance = " + Fmt(row.maxDistance) + " must be 0 (default) or greater than minDistance (" + Fmt(row.minDistance) + ")");
                    }
                    else
                    {
                        // 0 means the AudioBus default 30: compare the EFFECTIVE max, never the raw value (the five M1 rows have 0).
                        float effective = row.maxDistance == 0f ? DefaultMaxDistance : row.maxDistance;
                        if (!(effective > row.minDistance))
                            e.Add(l + ": effective maxDistance (" + Fmt(effective) + ") must be greater than minDistance (" + Fmt(row.minDistance) + ")");
                    }
                }
            }
        }
    }
}
