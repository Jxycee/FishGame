// The single seed source (contract 5.1): spec 9.6, 9.7, 9.12, 9.10 tables, contract 7.7 motion seeds, clean threshold 0.8. Used only to create missing assets and by tests. Owned by T14; T15 updates retuned values.
// Values are written exactly as in plan data-tuning section 1. Lists keep the table order of that section (not sorted); BuildDatabase sorts by id.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.EditorTools
{
    // One species row: the Core data plus the presentation fields that only the Def carries.
    public readonly struct SpeciesSeed
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string ProfileId;
        public readonly FishSpeciesData Data;   // FightProfileIndex = the ordinal-sorted index of ProfileId (0.2)
        public readonly BodyShape BodyShape;
        public readonly Color ColorA;
        public readonly Color ColorB;
        public readonly string ModelFile;       // file name under AssetPaths.ModelsFolder, for example "Fish_Bluegill.fbx"

        public SpeciesSeed(string id, string name, string profileId, FishSpeciesData data, BodyShape bodyShape, Color colorA, Color colorB, string modelFile)
        {
            Id = id;
            Name = name;
            ProfileId = profileId;
            Data = data;
            BodyShape = bodyShape;
            ColorA = colorA;
            ColorB = colorB;
            ModelFile = modelFile;
        }
    }

    public static class FishingSeedData
    {
        private static readonly FishingTuning s_tuning;
        private static readonly ReadOnlyCollection<(string id, string name, RodStats stats, int shopCost, int firstCamp)> s_rods;
        private static readonly ReadOnlyCollection<(string id, string name, BaitStats stats)> s_baits;
        private static readonly ReadOnlyCollection<(string id, string name, FishFightProfile profile)> s_profiles;
        private static readonly ReadOnlyCollection<SpeciesSeed> s_species;
        private static readonly ReadOnlyCollection<(string id, string name, int value, BodyShape shape)> s_junk;
        private static readonly WaveSet s_waves;

        static FishingSeedData()
        {
            // Spec 9.12 tuning table + creel caps (9.10) + clean threshold + R65 pity seeds. Field order = the plan 1.1 table.
            s_tuning = new FishingTuning
            {
                CastChargeTime = 1.0f, ChargePingPong = true, CastMinDist = 2.5f, MinLandDepth = 0.8f,
                BiteWaitMin = 4f, BiteWaitMax = 18f, MinBiteWait = 1.0f, HookBeat = 0.25f, LandDistance = 2.0f,
                ReelStrainBurst = 0.30f, CounterRelief = 0.5f, SteerStrain = 0.03f, OutOfWindowStrain = 0.20f, SpoolStrain = 0.50f, SpoolExtra = 12f,
                RelaxRate = 0.45f, RelaxWhileReeling = 0.10f, ReelBurstPenalty = 1.0f, DiveRunMult = 1.5f,
                SnapGrace = 0.25f, SnapGraceAssist = 0.75f, StrainScale = 1.0f, StrainScaleAssist = 0.6f,
                PinEscapeTime = 5f, SlackEscapeTime = 12f, FishStaminaRegen = 0.04f, CalmCenterPull = 0.15f, LateralMaxDeg = 75f,
                HookedLeash = 3f, JunkDragSpeed = 3f,
                ResultTimeout = 3f, CommitTimeout = 3f, HookTolerance = 0.75f, MaxFightTime = 240f, RodStateHz = 15, CancelRefundsBait = true,
                CreelMaxEntries = 12, CreelMaxWeightG = 15000,
                CleanCatchMaxTension = 0.8f,
                PityAfterFailures = 3, PityStrainMult = 0.85f, PityGraceMult = 1.5f, PityHookWindowBonus = 0.3f,
            };

            // Spec 9.7 rods.
            var rods = new List<(string id, string name, RodStats stats, int shopCost, int firstCamp)>
            {
                ("rod_twig", "Twig Rod", Rod(0, 14f, 0.9f, 0.8f, 0.50f, 0.50f, 0.9f), 0, 0),
                ("rod_handmade", "Handmade Rod", Rod(1, 18f, 1.0f, 1.0f, 0.60f, 0.60f, 1.0f), 120, 1),
                ("rod_carbon", "Carbon Rod", Rod(2, 24f, 1.2f, 1.3f, 0.70f, 0.70f, 1.1f), 300, 2),
                ("rod_deepsea", "Deep-Sea Rod", Rod(3, 30f, 1.3f, 1.6f, 0.75f, 0.80f, 1.2f), 600, 4),
            };
            s_rods = new ReadOnlyCollection<(string, string, RodStats, int, int)>(rods);

            // Spec 9.7 baits.
            var baits = new List<(string id, string name, BaitStats stats)>
            {
                ("bait_bare_hook", "Bare Hook", Bait(1, 1, 1, 1.30f, 1.5f, 1, true)),
                ("bait_worm", "Worm", Bait(3, 3, 9, 0.90f, 1.0f, 2, false)),
                ("bait_grub", "Grub", Bait(4, 3, 12, 0.85f, 0.9f, 2, false)),
                ("bait_minnow", "Minnow", Bait(2, 2, 4, 1.00f, 1.1f, 1, false)),
                ("bait_shrimp", "Shrimp", Bait(5, 2, 10, 0.80f, 0.8f, 1, false)),
                ("bait_raw_meat", "Raw Meat", Bait(10, 1, 10, 0.70f, 0.5f, 0, false)),
            };
            s_baits = new ReadOnlyCollection<(string, string, BaitStats)>(baits);

            // Spec 9.6 profile columns with the C 7.7 motion seeds (they override the spec Darter row).
            var profiles = new List<(string id, string name, FishFightProfile profile)>
            {
                ("steady", "Steady", Profile(0.30f, 0.60f, 0.30f, 0.08f, 1.0f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0.6f, 0f, 0f, 0.5f, 0f)),
                ("darter", "Darter", Profile(0.45f, 0.70f, 0.40f, 0.07f, 0.3f, 0.8f, 0.4f, 0.9f, 1.0f, 0f, 0f, 0f, 0f, 0.3f, 0f)),
                ("bulldog", "Bulldog", Profile(0.60f, 0.60f, 0.50f, 0.06f, 1.0f, 3.0f, 2.0f, 4.0f, 0f, 0.35f, 0f, 0.12f, 2.5f, 0.5f, 0f)),
                ("thrasher", "Thrasher", Profile(0.70f, 1.00f, 0.60f, 0.09f, 1.0f, 2.5f, 1.5f, 3.0f, 0.5f, 0f, 0f, 0f, 0f, 0.5f, 0.3f)),
                ("runner", "Runner", Profile(0.85f, 0.80f, 0.80f, 0.05f, 1.0f, 2.0f, 2.5f, 4.0f, 0.1f, 0.25f, 0.8f, 0f, 0f, 0.5f, 0f)),
            };
            s_profiles = new ReadOnlyCollection<(string, string, FishFightProfile)>(profiles);

            // Spec 9.6 species. weightSkew 2.2, lengthShapeK 1.0, hookWindow 0, not special; presentation fields are plan seeds.
            string[] sortedProfileIds = new string[profiles.Count];
            for (int i = 0; i < profiles.Count; i++) sortedProfileIds[i] = profiles[i].id;
            Array.Sort(sortedProfileIds, StringComparer.Ordinal);
            var species = new List<SpeciesSeed>
            {
                MakeSpecies("bluegill", "Bluegill", "steady", sortedProfileIds, 200, 800, 5, 0.9f, 1, 0.8f, FishRarity.Common, BodyShape.Round,
                    new Color(0.32f, 0.50f, 0.62f, 1f), new Color(0.95f, 0.62f, 0.22f, 1f), "Fish_Bluegill.fbx"),
                MakeSpecies("perch", "Perch", "darter", sortedProfileIds, 300, 1200, 8, 1.0f, 3, 1.2f, FishRarity.Common, BodyShape.Long,
                    new Color(0.62f, 0.68f, 0.30f, 1f), new Color(0.20f, 0.24f, 0.16f, 1f), "Fish_Perch.fbx"),
                MakeSpecies("carp", "Carp", "bulldog", sortedProfileIds, 1000, 4000, 12, 1.2f, 1, 1.5f, FishRarity.Common, BodyShape.Round,
                    new Color(0.72f, 0.58f, 0.30f, 1f), new Color(0.45f, 0.34f, 0.18f, 1f), "Fish_Carp.fbx"),
                MakeSpecies("trout", "Trout", "thrasher", sortedProfileIds, 600, 2500, 20, 0.9f, 5, 2.0f, FishRarity.Uncommon, BodyShape.Long,
                    new Color(0.55f, 0.62f, 0.55f, 1f), new Color(0.88f, 0.45f, 0.48f, 1f), "Fish_Trout.fbx"),
                MakeSpecies("catfish", "Catfish", "runner", sortedProfileIds, 2000, 7000, 35, 1.4f, 6, 2.5f, FishRarity.Uncommon, BodyShape.Long,
                    new Color(0.36f, 0.34f, 0.30f, 1f), new Color(0.70f, 0.66f, 0.56f, 1f), "Fish_Catfish.fbx"),
            };
            s_species = new ReadOnlyCollection<SpeciesSeed>(species);

            var junk = new List<(string id, string name, int value, BodyShape shape)>
            {
                ("old_boot", "Old Boot", 2, BodyShape.Long),
                ("tin_can", "Tin Can", 2, BodyShape.Round),
            };
            s_junk = new ReadOnlyCollection<(string, string, int, BodyShape)>(junk);

            // Plan 1.8: three gentle waves, amplitude sum 0.115 m, wavelengths >= 6 m.
            WaveSet waves = default;
            waves.W0 = new Wave { DirectionDeg = 20f, Amplitude = 0.050f, Wavelength = 9.0f, Speed = 1.2f };
            waves.W1 = new Wave { DirectionDeg = 75f, Amplitude = 0.040f, Wavelength = 7.0f, Speed = 1.0f };
            waves.W2 = new Wave { DirectionDeg = 320f, Amplitude = 0.025f, Wavelength = 6.5f, Speed = 0.8f };
            s_waves = waves;
        }

        private static RodStats Rod(byte tier, float cast, float reel, float strength, float window, float steer, float relax)
        {
            return new RodStats { Tier = tier, CastMaxDist = cast, ReelSpeed = reel, LineStrength = strength, AngleWindow = window, SteerRate = steer, RelaxMult = relax };
        }

        private static BaitStats Bait(int rating, int stackMax, int maxRating, float biteSpeedMod, float junkMult, int maxNibbles, bool unlimited)
        {
            return new BaitStats { Rating = rating, StackMax = stackMax, MaxRating = maxRating, BiteSpeedMod = biteSpeedMod, JunkMult = junkMult, MaxNibbles = maxNibbles, Unlimited = unlimited };
        }

        private static FishFightProfile Profile(float pull, float lateral, float run, float drain, float calmMin, float calmMax, float burstMin, float burstMax,
            float flipRate, float dive, float burstRamp, float calmDrift, float calmDriftRamp, float flipMinGap, float lateralJitter)
        {
            return new FishFightProfile
            {
                PullStrength = pull, LateralSpeed = lateral, RunSpeed = run, StaminaDrain = drain,
                CalmMin = calmMin, CalmMax = calmMax, BurstMin = burstMin, BurstMax = burstMax, FlipRate = flipRate, DiveChance = dive,
                BurstRamp = burstRamp, CalmDrift = calmDrift, CalmDriftRamp = calmDriftRamp, FlipMinGap = flipMinGap, LateralJitter = lateralJitter,
            };
        }

        private static SpeciesSeed MakeSpecies(string id, string name, string profileId, string[] sortedProfileIds, int weightMinG, int weightMaxG, int baseValue,
            float biteWaitMod, int minBaitRating, float minDepth, FishRarity rarity, BodyShape shape, Color colorA, Color colorB, string modelFile)
        {
            int index = Array.IndexOf(sortedProfileIds, profileId);
            if (index < 0) throw new InvalidOperationException("species seed '" + id + "' names unknown profile '" + profileId + "'");
            var data = new FishSpeciesData
            {
                FightProfileIndex = (ushort)index, WeightMinG = weightMinG, WeightMaxG = weightMaxG, BaseValue = baseValue, MinBaitRating = minBaitRating,
                WeightSkew = 2.2f, LengthShapeK = 1.0f, BiteWaitMod = biteWaitMod, HookWindow = 0f, MinDepth = minDepth, IsSpecial = false, Rarity = rarity,
            };
            return new SpeciesSeed(id, name, profileId, data, shape, colorA, colorB, modelFile);
        }

        public static FishingTuning Tuning { get { return s_tuning; } }

        public static IReadOnlyList<(string id, string name, RodStats stats, int shopCost, int firstCamp)> Rods { get { return s_rods; } }

        public static IReadOnlyList<(string id, string name, BaitStats stats)> Baits { get { return s_baits; } }

        public static IReadOnlyList<(string id, string name, FishFightProfile profile)> Profiles { get { return s_profiles; } }

        public static IReadOnlyList<SpeciesSeed> Species { get { return s_species; } }

        public static IReadOnlyList<(string id, string name, int value, BodyShape shape)> Junk { get { return s_junk; } }

        public static WaveSet Waves { get { return s_waves; } }

        // Test convenience: the Core database straight from the seeds, every category sorted by id (ordinal) like ContentRegistry.
        public static FishingDatabase BuildDatabase()
        {
            var rodList = new List<(string id, RodStats stats)>();
            foreach (var r in s_rods) rodList.Add((r.id, r.stats));
            rodList.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var rods = new RodStats[rodList.Count];
            for (int i = 0; i < rods.Length; i++) rods[i] = rodList[i].stats;

            var baitList = new List<(string id, BaitStats stats)>();
            foreach (var b in s_baits) baitList.Add((b.id, b.stats));
            baitList.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var baits = new BaitStats[baitList.Count];
            for (int i = 0; i < baits.Length; i++) baits[i] = baitList[i].stats;

            var profileList = new List<(string id, FishFightProfile profile)>();
            foreach (var p in s_profiles) profileList.Add((p.id, p.profile));
            profileList.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var profiles = new FishFightProfile[profileList.Count];
            var profileIds = new string[profileList.Count];
            for (int i = 0; i < profiles.Length; i++) { profiles[i] = profileList[i].profile; profileIds[i] = profileList[i].id; }

            var speciesList = new List<SpeciesSeed>(s_species);
            speciesList.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var species = new FishSpeciesData[speciesList.Count];
            for (int i = 0; i < species.Length; i++)
            {
                species[i] = speciesList[i].Data;
                species[i].FightProfileIndex = (ushort)Array.IndexOf(profileIds, speciesList[i].ProfileId);
            }

            var junkList = new List<(string id, int value)>();
            foreach (var j in s_junk) junkList.Add((j.id, j.value));
            junkList.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var junk = new JunkData[junkList.Count];
            for (int i = 0; i < junk.Length; i++) junk[i] = new JunkData { ValueCoins = junkList[i].value };

            FishingTuning tuning = s_tuning;
            return new FishingDatabase(rods, baits, profiles, species, junk, in tuning);
        }

        // ---- art, water and audio seeds (plan 1.6-1.9); internal so the single seed file holds them without new public surface ----

        // Junk colour (plan 1.6); the public Junk tuple carries no colour.
        internal static Color JunkColor(string id)
        {
            switch (id)
            {
                case "old_boot": return new Color(0.30f, 0.22f, 0.16f, 1f);
                case "tin_can": return new Color(0.68f, 0.70f, 0.72f, 1f);
                default: throw new ArgumentException("no junk seed '" + id + "'", nameof(id));
            }
        }

        internal static readonly string[] PaletteSlotNames =
        {
            "Grass", "Foliage", "FoliageDark", "Wood", "WoodDark", "Sand", "Rock", "Reed", "Dock", "Roof", "Accent", "Snow",
            "Reserved12", "Reserved13", "Reserved14", "Reserved15",
        };

        internal const float PaletteFaceJitter = 0.04f;
        internal const int PaletteM2Season = 1;   // Summer

        internal static Color PaletteSkinTone
        {
            get
            {
                SlotPalette.GetSkinRgb(out float r, out float g, out float b);
                return new Color(r, g, b, 1f);
            }
        }

        internal static readonly Color PaletteUiAccent = new Color(0.95f, 0.62f, 0.20f, 1f);
        internal static readonly Color PaletteUiGood = new Color(0.40f, 0.80f, 0.40f, 1f);
        internal static readonly Color PaletteUiWarn = new Color(0.98f, 0.78f, 0.25f, 1f);
        internal static readonly Color PaletteUiBad = new Color(0.90f, 0.30f, 0.25f, 1f);
        internal static readonly Color PaletteUiPanel = new Color(0.08f, 0.10f, 0.14f, 0.85f);
        internal static readonly Color PaletteUiText = new Color(0.95f, 0.95f, 0.92f, 1f);

        private static Color Rgb(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        // Spring, Summer, Autumn, Winter. Summer is the base; the other seasons copy it and override slots 0, 1, 2 and 7. Slots 12-15 are mid grey.
        internal static PaletteSeason[] PaletteSeasons()
        {
            Color[] summer =
            {
                Rgb(0.42f, 0.66f, 0.30f), Rgb(0.30f, 0.52f, 0.24f), Rgb(0.17f, 0.34f, 0.17f), Rgb(0.66f, 0.48f, 0.30f),
                Rgb(0.40f, 0.27f, 0.17f), Rgb(0.88f, 0.80f, 0.58f), Rgb(0.55f, 0.55f, 0.53f), Rgb(0.62f, 0.68f, 0.36f),
                Rgb(0.58f, 0.44f, 0.30f), Rgb(0.70f, 0.30f, 0.24f), Rgb(0.95f, 0.45f, 0.15f), Rgb(0.95f, 0.96f, 0.98f),
                Rgb(0.5f, 0.5f, 0.5f), Rgb(0.5f, 0.5f, 0.5f), Rgb(0.5f, 0.5f, 0.5f), Rgb(0.5f, 0.5f, 0.5f),
            };
            return new[]
            {
                Season("Spring", summer, Rgb(0.50f, 0.74f, 0.34f), Rgb(0.40f, 0.62f, 0.30f), Rgb(0.22f, 0.42f, 0.20f), Rgb(0.56f, 0.70f, 0.34f)),
                Season("Summer", summer, summer[0], summer[1], summer[2], summer[7]),
                Season("Autumn", summer, Rgb(0.62f, 0.60f, 0.30f), Rgb(0.85f, 0.50f, 0.18f), Rgb(0.55f, 0.26f, 0.12f), Rgb(0.72f, 0.62f, 0.32f)),
                Season("Winter", summer, Rgb(0.80f, 0.84f, 0.86f), Rgb(0.32f, 0.44f, 0.36f), Rgb(0.18f, 0.28f, 0.24f), Rgb(0.66f, 0.62f, 0.48f)),
            };
        }

        private static PaletteSeason Season(string name, Color[] summer, Color grass, Color foliage, Color foliageDark, Color reed)
        {
            var colors = (Color[])summer.Clone();
            colors[0] = grass;
            colors[1] = foliage;
            colors[2] = foliageDark;
            colors[7] = reed;
            return new PaletteSeason { name = name, colors = colors };
        }

        // Dawn, Day, Dusk, Night (spec 10.3 order).
        internal static DayPhaseAmbient[] PalettePhases()
        {
            return new[]
            {
                Phase(0f, Rgb(1.00f, 0.78f, 0.60f), 0.8f, Rgb(0.38f, 0.52f, 0.78f), Rgb(0.98f, 0.74f, 0.56f), Rgb(0.36f, 0.34f, 0.36f),
                    Rgb(0.42f, 0.40f, 0.52f), Rgb(0.70f, 0.62f, 0.66f), Rgb(0.86f, 0.74f, 0.66f), 0.010f, 0.6f, 0f),
                Phase(0.10f, Rgb(1.00f, 0.96f, 0.88f), 1.1f, Rgb(0.33f, 0.58f, 0.92f), Rgb(0.74f, 0.86f, 0.96f), Rgb(0.40f, 0.40f, 0.36f),
                    Rgb(0.46f, 0.50f, 0.62f), Rgb(0.80f, 0.86f, 0.92f), Rgb(0.74f, 0.84f, 0.92f), 0.008f, 0f, 0f),
                Phase(0.55f, Rgb(1.00f, 0.62f, 0.40f), 0.8f, Rgb(0.30f, 0.30f, 0.58f), Rgb(0.98f, 0.56f, 0.36f), Rgb(0.30f, 0.26f, 0.30f),
                    Rgb(0.40f, 0.32f, 0.46f), Rgb(0.66f, 0.52f, 0.56f), Rgb(0.80f, 0.56f, 0.46f), 0.010f, 1f, 0f),
                Phase(0.70f, Rgb(0.62f, 0.70f, 0.92f), 0.25f, Rgb(0.04f, 0.06f, 0.16f), Rgb(0.12f, 0.16f, 0.30f), Rgb(0.05f, 0.05f, 0.08f),
                    Rgb(0.12f, 0.14f, 0.26f), Rgb(0.30f, 0.36f, 0.52f), Rgb(0.10f, 0.13f, 0.22f), 0.014f, 0f, 1f),
            };
        }

        private static DayPhaseAmbient Phase(float start, Color light, float intensity, Color skyTop, Color skyHorizon, Color skyGround,
            Color toonShade, Color toonAmbient, Color fog, float fogDensity, float warm, float cool)
        {
            return new DayPhaseAmbient
            {
                startFraction = start, lightColor = light, lightIntensity = intensity, skyTop = skyTop, skyHorizon = skyHorizon, skyGround = skyGround,
                toonShade = toonShade, toonAmbient = toonAmbient, fogColor = fog, fogDensity = fogDensity, warmWeight = warm, coolWeight = cool,
            };
        }

        internal static readonly Color WaterShallowColor = new Color(0.36f, 0.72f, 0.70f, 0.75f);
        internal static readonly Color WaterDeepColor = new Color(0.08f, 0.28f, 0.42f, 0.95f);
        internal const float WaterDepthRange = 3.0f;
        internal const float WaterFoamWidth = 0.35f;

        // One row per recipe in SfxRecipes.All order, mixing fields copied from the Core recipe (maxDistance 0 stays 0: the AudioBus default 30).
        internal static List<SfxCueRow> CueRows()
        {
            var rows = new List<SfxCueRow>(SfxRecipes.All.Count);
            foreach (SfxRecipe r in SfxRecipes.All)
            {
                rows.Add(new SfxCueRow
                {
                    id = r.Id, category = r.Category, baseVolume = r.BaseVolume, minDistance = r.MinDistance,
                    maxDistance = r.MaxDistance, minInterval = r.MinInterval, spatial = r.Spatial,
                });
            }
            return rows;
        }
    }
}
