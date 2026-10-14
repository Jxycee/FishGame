// The single seed source (contract 5.1): spec 9.6, 9.7, 9.12, 9.10 tables, contract 7.7 motion seeds, clean threshold 0.8. Used only to create missing assets and by tests. Owned by T14; T15 updates retuned values.
// Values are written exactly as in plan data-tuning section 1. Lists keep the table order of that section (not sorted); BuildDatabase sorts by id.
// Species and junk forward to EcologySeedData (M3 T10).
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
        private static readonly WaveSet s_waves;

        static FishingSeedData()
        {
            // Spec 9.12 tuning table + creel caps (9.10) + clean threshold + R65 pity seeds. Field order = the plan 1.1 table.
            s_tuning = new FishingTuning
            {
                CastChargeTime = 1.0f, ChargePingPong = true, CastMinDist = 2.5f, MinLandDepth = 0.5f,   // M3-D52 (Q1 default): reeds castable; was 0.8 in M2
                BiteWaitMin = 4f, BiteWaitMax = 18f, MinBiteWait = 1.0f, HookBeat = 0.25f, LandDistance = 1.0f,
                SpoolExtra = 12f, RelaxRate = 1.5f, StrainScale = 0.75f, StrainScaleAssist = 0.45f,
                SlackEscapeTime = 12f, LateralMaxDeg = 60f,
                HookedLeash = 3f, JunkDragSpeed = 3f,
                ResultTimeout = 3f, CommitTimeout = 3f, HookTolerance = 0.75f, MaxFightTime = 240f, RodStateHz = 15, CancelRefundsBait = true,
                CreelMaxEntries = 12, CreelMaxWeightG = 15000,
                CleanCatchMaxTension = 0.8f,
                PityAfterFailures = 3, PityStrainMult = 0.85f, PityGraceMult = 1.5f, PityHookWindowBonus = 0.3f,
                // R72 Rust fight baseline (spec 9.12; research note 8). Read by the new FightSim from F2.
                StrainBarFull = 6f, StrainSnap = 7f, StrainRestInput = 0.5f, StrainReelRun = 2.2f, StrainReelBack = 2.5f,
                StrainCounterSteer = 0.8f, StrainOtherSteer = 0.5f, SideMoveSpeed = 1.2f, BackMoveSpeed = 1.0f,
                TireSteerRate = 1.2f, TireCounterRate = 1.25f, TireDecayRate = 0.1f, TireRunFull = 12f, TireReelFull = 5f,
                TireReelMaxMult = 3f, BackPullMargin = 2f, FightTimeout = 120f,
                // F10 retune (user playtest 2026-10-13, spec 9.12 note): backMoveSpeed 1.3 -> 1.0, tireSteerRate 0.8 -> 1.2, tireRunFull 20 -> 12 (plus
                // reelInMult on four profiles below). Fish tire sooner and cover less line; the strain bar, snap, gains, drain and scale are untouched, so a
                // player who does not steer (Greedy) snaps at the same times as before, and a counter-steering player who never lets go (Reckless) still snaps.
                // User playtest 2026-10-13 (spec 9.12 note): F7 a hooked fish with no input for 30 s throws the hook; F1 the reel opens at x2.5 and
                // settles into the tire curve over 0.8 s (<= TireReelMaxMult, so the host's MaxReelSpeed floor still bounds it).
                IdleEscapeTime = 30f, ReelOpenMult = 2.5f, ReelOpenSeconds = 0.8f,
            };

            // Spec 9.7 rods.
            var rods = new List<(string id, string name, RodStats stats, int shopCost, int firstCamp)>
            {
                // R72: reelSpeed / steerRate follow Rust's rod (Handmade 0.6 / 0.8); the other rods keep their pre-R72 ratios.
                ("rod_twig", "Twig Rod", Rod(0, 14f, 0.54f, 0.8f, 0.50f, 0.67f, 0.9f), 0, 0),
                ("rod_handmade", "Handmade Rod", Rod(1, 18f, 0.6f, 1.0f, 0.60f, 0.8f, 1.0f), 120, 1),
                ("rod_carbon", "Carbon Rod", Rod(2, 24f, 0.72f, 1.3f, 0.70f, 0.93f, 1.1f), 300, 2),
                ("rod_deepsea", "Deep-Sea Rod", Rod(3, 30f, 0.78f, 1.6f, 0.75f, 1.07f, 1.2f), 600, 4),
            };
            s_rods = new ReadOnlyCollection<(string, string, RodStats, int, int)>(rods);

            // Spec 9.7 baits. M3 T3 (M3-D4): the last three arguments are affinityTags, affinityMult, specialMult.
            var baits = new List<(string id, string name, BaitStats stats)>
            {
                ("bait_bare_hook", "Bare Hook", Bait(1, 1, 1, 1.30f, 1.5f, 1, true, FishTags.None, 1.0f, 1.0f)),
                ("bait_worm", "Worm", Bait(3, 3, 9, 0.90f, 1.0f, 2, false, FishTags.Bottom, 1.3f, 1.0f)),
                ("bait_grub", "Grub", Bait(4, 3, 12, 0.85f, 0.9f, 2, false, FishTags.Surface, 1.3f, 1.0f)),
                ("bait_minnow", "Minnow", Bait(2, 2, 4, 1.00f, 1.1f, 1, false, FishTags.Predator, 1.4f, 1.0f)),
                ("bait_shrimp", "Shrimp", Bait(5, 2, 10, 0.80f, 0.8f, 1, false, FishTags.Schooling, 1.3f, 1.2f)),
                ("bait_raw_meat", "Raw Meat", Bait(10, 1, 10, 0.70f, 0.5f, 0, false, FishTags.Predator | FishTags.Nocturnal, 1.5f, 1.5f)),
            };
            s_baits = new ReadOnlyCollection<(string, string, BaitStats)>(baits);

            // R72: Rust tiers plus the five per-profile quirks (spec 9.6); the quirks change only how the fish moves. Columns: calmMin/Max,
            // burstMin/Max, flipRate, dive, burstRamp, calmDrift, calmDriftRamp, flipMinGap, jitter, then the tier (strainMod, moveMult,
            // reelInMult) and wavePeriod.
            var profiles = new List<(string id, string name, FishFightProfile profile)>
            {
                ("steady", "Steady", Profile(2f, 4f, 3f, 5f, 0f, 0.15f, 0.6f, 0f, 0f, 0.5f, 0f, 0.8f, 0.8f, 1.2f, 4f)),
                // F10 (user playtest 2026-10-13): reelInMult darter 1.0 -> 1.3, bulldog 0.9 -> 1.0, thrasher 1.0 -> 1.15, runner 0.9 -> 0.95. Steady is
                // unchanged (already 13-14 s; its careless-player race is a knife edge). reelInMult is movement only, so no snap time moves. Runner stays
                // near 0.9: a non-steering player must not land the big fish (FishingBots Pump pin).
                ("darter", "Darter", Profile(1f, 2f, 2f, 3.5f, 1.0f, 0.15f, 0f, 0f, 0f, 0.3f, 0f, 1.0f, 1.0f, 1.3f, 4f)),
                ("bulldog", "Bulldog", Profile(2f, 4f, 2.5f, 4.5f, 0f, 0.55f, 0f, 0.06f, 2.5f, 0.5f, 0f, 1.25f, 1.25f, 1.0f, 4f)),
                ("thrasher", "Thrasher", Profile(1.5f, 3f, 3f, 5f, 0.4f, 0.20f, 0f, 0f, 0f, 0.5f, 0.3f, 1.0f, 1.0f, 1.15f, 1.5f)),
                ("runner", "Runner", Profile(2f, 3.5f, 5f, 9f, 0f, 0.30f, 0.8f, 0f, 0f, 0.5f, 0f, 1.25f, 1.25f, 0.95f, 4f)),
            };
            s_profiles = new ReadOnlyCollection<(string, string, FishFightProfile)>(profiles);

            // Species and junk are NOT built here (M3 T10, CR-2/CR-3): Species/Junk forward to EcologySeedData through the getters below, read
            // lazily, never from this static constructor (EcologySeedData's static constructor reads Profiles).

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

        private static BaitStats Bait(int rating, int stackMax, int maxRating, float biteSpeedMod, float junkMult, int maxNibbles, bool unlimited,
            FishTags affinityTags, float affinityMult, float specialMult)
        {
            return new BaitStats
            {
                Rating = rating, StackMax = stackMax, MaxRating = maxRating, BiteSpeedMod = biteSpeedMod, JunkMult = junkMult, MaxNibbles = maxNibbles,
                Unlimited = unlimited, AffinityTags = affinityTags, AffinityMult = affinityMult, SpecialMult = specialMult,
            };
        }

        private static FishFightProfile Profile(float calmMin, float calmMax, float burstMin, float burstMax,
            float flipRate, float dive, float burstRamp, float calmDrift, float calmDriftRamp, float flipMinGap, float lateralJitter,
            float strainMod, float moveMult, float reelInMult, float wavePeriod)
        {
            return new FishFightProfile
            {
                CalmMin = calmMin, CalmMax = calmMax, BurstMin = burstMin, BurstMax = burstMax, FlipRate = flipRate, DiveChance = dive,
                BurstRamp = burstRamp, CalmDrift = calmDrift, CalmDriftRamp = calmDriftRamp, FlipMinGap = flipMinGap, LateralJitter = lateralJitter,
                StrainMod = strainMod, MoveMult = moveMult, ReelInMult = reelInMult, WavePeriod = wavePeriod,
            };
        }

        public static FishingTuning Tuning { get { return s_tuning; } }

        public static IReadOnlyList<(string id, string name, RodStats stats, int shopCost, int firstCamp)> Rods { get { return s_rods; } }

        public static IReadOnlyList<(string id, string name, BaitStats stats)> Baits { get { return s_baits; } }

        public static IReadOnlyList<(string id, string name, FishFightProfile profile)> Profiles { get { return s_profiles; } }

        // The M3 roster (25 species) and its 7 junk items, owned by EcologySeedData (M3 T10). Read lazily: never touch these from the static constructor.
        public static IReadOnlyList<SpeciesSeed> Species { get { return EcologySeedData.Species; } }

        public static IReadOnlyList<(string id, string name, int value, BodyShape shape)> Junk { get { return EcologySeedData.Junk; } }

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

            var speciesList = new List<SpeciesSeed>(Species);
            speciesList.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var species = new FishSpeciesData[speciesList.Count];
            for (int i = 0; i < species.Length; i++)
            {
                species[i] = speciesList[i].Data;
                species[i].FightProfileIndex = (ushort)Array.IndexOf(profileIds, speciesList[i].ProfileId);
            }

            var junkList = new List<(string id, int value)>();
            foreach (var j in Junk) junkList.Add((j.id, j.value));
            junkList.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var junk = new JunkData[junkList.Count];
            for (int i = 0; i < junk.Length; i++) junk[i] = new JunkData { ValueCoins = junkList[i].value };

            FishingTuning tuning = s_tuning;
            return new FishingDatabase(rods, baits, profiles, species, junk, in tuning);
        }

        // ---- art, water and audio seeds (plan 1.6-1.9); internal so the single seed file holds them without new public surface ----

        // Junk colour (plan 1.6); the public Junk tuple carries no colour. The colours moved to EcologySeedData with the 7 junk items (M3 T10).
        internal static Color JunkColor(string id)
        {
            return EcologySeedData.JunkColor(id);
        }

        internal static readonly string[] PaletteSlotNames =
        {
            "Grass", "Foliage", "FoliageDark", "Wood", "WoodDark", "Sand", "Rock", "Reed", "Dock", "Roof", "Accent", "Snow",
            "PineLight", "PineDark", "Blossom", "FoliageAlt",
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

        // Spring, Summer, Autumn, Winter. Summer is the base; the other seasons copy it and override slots 0, 1, 2, 7, 14 and 15.
        // Seasonal trees (user decision 2026-10-13 "Full seasons"): the round (deciduous) trees take one of three foliage slots per tree,
        // Foliage (1), FoliageDark (2) or FoliageAlt (15): close greens in spring and summer, orange / red / yellow in autumn; in winter the
        // canopies are hidden (bare branches), so their winter values are never seen on trees. PineLight (12) and PineDark (13) are the evergreen
        // pines, identical in every season (the winter snow on the pines is a separate renderer on the Grass slot, which is the ground snow).
        // Blossom (14) is the spring blossom specks (a spring-only renderer); outside spring it equals that season's Foliage.
        internal static PaletteSeason[] PaletteSeasons()
        {
            Color[] summer =
            {
                Rgb(0.42f, 0.66f, 0.30f), Rgb(0.30f, 0.52f, 0.24f), Rgb(0.22f, 0.42f, 0.19f), Rgb(0.66f, 0.48f, 0.30f),
                Rgb(0.40f, 0.27f, 0.17f), Rgb(0.88f, 0.80f, 0.58f), Rgb(0.55f, 0.55f, 0.53f), Rgb(0.62f, 0.68f, 0.36f),
                Rgb(0.58f, 0.44f, 0.30f), Rgb(0.70f, 0.30f, 0.24f), Rgb(0.95f, 0.45f, 0.15f), Rgb(0.95f, 0.96f, 0.98f),
                Rgb(0.24f, 0.46f, 0.28f), Rgb(0.13f, 0.30f, 0.20f), Rgb(0.30f, 0.52f, 0.24f), Rgb(0.36f, 0.56f, 0.22f),
            };
            return new[]
            {
                Season("Spring", summer, Rgb(0.50f, 0.74f, 0.34f), Rgb(0.48f, 0.74f, 0.32f), Rgb(0.38f, 0.62f, 0.26f), Rgb(0.56f, 0.70f, 0.34f),
                    Rgb(0.92f, 0.62f, 0.74f), Rgb(0.56f, 0.76f, 0.30f)),
                Season("Summer", summer, summer[0], summer[1], summer[2], summer[7], summer[14], summer[15]),
                Season("Autumn", summer, Rgb(0.62f, 0.60f, 0.30f), Rgb(0.88f, 0.50f, 0.16f), Rgb(0.76f, 0.24f, 0.15f), Rgb(0.72f, 0.62f, 0.32f),
                    Rgb(0.88f, 0.50f, 0.16f), Rgb(0.92f, 0.74f, 0.20f)),
                Season("Winter", summer, Rgb(0.80f, 0.84f, 0.86f), Rgb(0.32f, 0.44f, 0.36f), Rgb(0.18f, 0.28f, 0.24f), Rgb(0.66f, 0.62f, 0.48f),
                    Rgb(0.32f, 0.44f, 0.36f), Rgb(0.32f, 0.44f, 0.36f)),
            };
        }

        // The toon colour grade per season (Spring, Summer, Autumn, Winter; asset study 2026-10-14, ToonGrade): a warm light gain on upward faces,
        // a cool blue-purple gain on the shaded side, and a small per-object hue shift. Summer is ToonGrade.Default.
        internal static ToonGrade[] ToonGrades()
        {
            return new[]
            {
                Grade(new Vector3(1.12f, 1.10f, 0.94f), 0.8f, 0.35f, new Vector3(0.88f, 0.86f, 1.08f), 0.65f, 0.55f, 0.020f),
                ToonGrade.Default,
                Grade(new Vector3(1.16f, 1.06f, 0.86f), 0.85f, 0.35f, new Vector3(0.88f, 0.82f, 1.06f), 0.75f, 0.55f, 0.022f),
                Grade(new Vector3(1.08f, 1.08f, 1.06f), 0.7f, 0.40f, new Vector3(0.84f, 0.88f, 1.14f), 0.75f, 0.60f, 0.012f),
            };
        }

        private static ToonGrade Grade(Vector3 topGain, float topStrength, float topTightness, Vector3 bottomGain, float bottomStrength,
            float shadeWeight, float hueShift)
        {
            return new ToonGrade
            {
                topGain = topGain, topStrength = topStrength, topTightness = topTightness,
                bottomGain = bottomGain, bottomStrength = bottomStrength, shadeWeight = shadeWeight, hueShift = hueShift,
            };
        }

        private static PaletteSeason Season(string name, Color[] summer, Color grass, Color foliage, Color foliageDark, Color reed, Color blossom,
            Color foliageAlt)
        {
            var colors = (Color[])summer.Clone();
            colors[0] = grass;
            colors[1] = foliage;
            colors[2] = foliageDark;
            colors[7] = reed;
            colors[14] = blossom;
            colors[15] = foliageAlt;
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

        // Water look (FishGame/Water): saturated shallow teal -> deep blue over a vertical depth range; alpha is opacity (the bed shows only in the shallows).
        internal static readonly Color WaterShallowColor = new Color(0.30f, 0.86f, 0.80f, 0.68f);
        internal static readonly Color WaterDeepColor = new Color(0.03f, 0.36f, 0.62f, 0.97f);
        internal const float WaterDepthRange = 1.6f;
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
