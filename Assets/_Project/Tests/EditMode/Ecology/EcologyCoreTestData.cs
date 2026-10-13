// Tests/EditMode/Ecology/EcologyCoreTestData.cs - Core ecology fixture for T5-T8 (M3 section B; read-only after T5).
// Seven spec 12.6 species (ordinal id order) with their lake rules, plus silver_mullet's harbour rule.
// Baits = FishingTestData.Baits() (M2 numbers) with EXPLICIT M3 fields (M3-D54, M3-D4). Rods, profiles and fight tuning
// come from FishingTestData (R72-owned) so a fight retune never breaks these tests. T13's EcologyTestData is a separate fixture.
using FishGame.Core;

namespace FishGame.Tests.EditMode
{
    internal static class EcologyCoreTestData
    {
        public const ushort Bluegill = 0, GildedCarp = 1, GlimmerEel = 2, GrumpyCarp = 3, MirrorMinnow = 4, SilverMullet = 5, SunnyTrout = 6;
        public const ushort LostSock = 0, OldBoot = 1, SoggyNewspaper = 2, TinCan = 3;
        public const int ZoneLake = 0, ZoneHarbour = 1;
        public const byte Reeds = 0, OpenWater = 1, DeepWater = 2;           // lake sub-areas (masks 1, 2, 4)
        public const byte Shallows = 0, Channel = 1, OpenSea = 2;            // harbour sub-areas
        public const int MinuteDawn = 60, MinuteMorning = 200, MinuteMidday = 300, MinuteAfternoon = 500, MinuteDusk = 700, MinuteNight = 900;

        const SeasonMask Sp = SeasonMask.Spring, Su = SeasonMask.Summer, Au = SeasonMask.Autumn;
        const TimeBandMask Da = TimeBandMask.Dawn, Mo = TimeBandMask.Morning, Mi = TimeBandMask.Midday,
                           Af = TimeBandMask.Afternoon, Du = TimeBandMask.Dusk, Ni = TimeBandMask.Night;

        static FishSpeciesData Fish(int profile, int minG, int maxG, int value, FishRarity rarity, FishTags tags,
                                    float k = 1f, bool special = false) =>
            new FishSpeciesData { FightProfileIndex = (ushort)profile, WeightMinG = minG, WeightMaxG = maxG, BaseValue = value,
                                  WeightSkew = 2.2f, LengthShapeK = k, BiteWaitMod = 1f, HookWindow = 0f,
                                  IsSpecial = special, Rarity = rarity, Tags = tags };

        public static FishSpeciesData[] Species() => new[]
        {
            Fish(FishingTestData.ProfileSteady, 200, 600, 8, FishRarity.Common, FishTags.Schooling),                     // bluegill
            Fish(FishingTestData.ProfileBulldog, 3000, 15000, 200, FishRarity.Legendary, FishTags.None, special: true),  // gilded_carp
            Fish(FishingTestData.ProfileThrasher, 500, 2500, 60, FishRarity.Rare, FishTags.Nocturnal, 1.6f),             // glimmer_eel
            Fish(FishingTestData.ProfileBulldog, 2000, 12000, 10, FishRarity.Common, FishTags.Bottom),                   // grumpy_carp
            Fish(FishingTestData.ProfileDarter, 50, 200, 5, FishRarity.Common, FishTags.Schooling),                      // mirror_minnow
            Fish(FishingTestData.ProfileSteady, 300, 2000, 8, FishRarity.Common, FishTags.Schooling),                    // silver_mullet
            Fish(FishingTestData.ProfileRunner, 500, 3000, 30, FishRarity.Uncommon, FishTags.Surface),                   // sunny_trout
        };

        public static JunkData[] Junk() => new[]
        {
            new JunkData { ValueCoins = 1 }, new JunkData { ValueCoins = 2 }, new JunkData { ValueCoins = 0 }, new JunkData { ValueCoins = 2 },
        };

        static void Affinity(ref BaitStats b, FishTags tags, float affinityMult, float specialMult)
        {
            b.AffinityTags = tags; b.AffinityMult = affinityMult; b.SpecialMult = specialMult;
        }

        public static BaitStats[] Baits()
        {
            BaitStats[] b = FishingTestData.Baits();
            Affinity(ref b[FishingTestData.BaitBareHook], FishTags.None, 1f, 1f);
            Affinity(ref b[FishingTestData.BaitGrub], FishTags.Surface, 1.3f, 1f);
            Affinity(ref b[FishingTestData.BaitMinnow], FishTags.Predator, 1.4f, 1f);
            Affinity(ref b[FishingTestData.BaitRawMeat], FishTags.Predator | FishTags.Nocturnal, 1.5f, 1.5f);
            Affinity(ref b[FishingTestData.BaitShrimp], FishTags.Schooling, 1.3f, 1.2f);
            Affinity(ref b[FishingTestData.BaitWorm], FishTags.Bottom, 1.3f, 1f);
            return b;
        }

        public static FishingDatabase Fishing()
        {
            FishingTuning t = FishingTestData.Tuning();
            return new FishingDatabase(FishingTestData.Rods(), Baits(), FishingTestData.BaseProfiles(), Species(), Junk(), in t);
        }

        // Spec 12.2 matrix (rows current Clear/Rain/Fog) and season column multipliers (season * 3 + column).
        public static WeatherTableData Weather() => new WeatherTableData
        {
            Base = new[] { 0.60f, 0.25f, 0.15f, 0.40f, 0.50f, 0.10f, 0.40f, 0.20f, 0.40f },
            SeasonColumnMult = new[] { 1f, 1.5f, 1f, 1f, 1f, 0.3f, 1f, 1f, 1f, 1f, 1f, 1.3f },
        };

        public static EcologyTuning Tuning() => new EcologyTuning
        {
            HotspotFirstMinMin = 60, HotspotFirstMinMax = 120, HotspotGapMin = 60, HotspotGapMax = 120,
            HotspotDurationMin = 45, HotspotDurationMax = 60, HotspotRadiusM = 3f,
        };

        public static SpawnRuleData Rule(ushort species, byte subAreaMask, SeasonMask seasons, WeatherMask weather, TimeBandMask bands,
                                         float weight, byte rod = 0, bool special = false, float chance = 0f, int limit = -1) =>
            new SpawnRuleData { SpeciesIndex = species, SubAreaMask = subAreaMask, Seasons = seasons, Weather = weather, Bands = bands,
                                Weight = weight, MinRodTier = rod, MinBaitRating = 0, MinDepth = 0f, Special = special,
                                SpecialChance = chance, CatchLimitPerRun = limit, MinPlayersInSession = 0 };

        static SubAreaBandData Band(float min, float max, float junk) =>
            new SubAreaBandData { MinShoreM = min, MaxShoreM = max, JunkChance = junk };

        public static SubAreaBandData[] LakeBands() => new[] { Band(0f, 5f, 0.20f), Band(5f, 10f, 0.12f), Band(10f, float.PositiveInfinity, 0.10f) };
        public static SubAreaBandData[] HarbourBands() => new[] { Band(0f, 5f, 0.20f), Band(5f, 12f, 0.12f), Band(12f, float.PositiveInfinity, 0.10f) };

        // Spec 12.6 lake rows, sorted by species id (rule index == species index).
        public static SpawnRuleData[] LakeRules() => new[]
        {
            Rule(Bluegill, 0b011, Sp | Su | Au, WeatherMask.All, Mo | Mi | Af | Du, 10f),
            Rule(GildedCarp, 0b100, SeasonMask.All, WeatherMask.All, TimeBandMask.All, 0f, special: true, chance: 0.02f, limit: 1),
            Rule(GlimmerEel, 0b100, Su | Au, WeatherMask.Rain | WeatherMask.Fog, Du | Ni, 8f, rod: 1),
            Rule(GrumpyCarp, 0b110, Su | Au, WeatherMask.All, Mi | Af | Du | Ni, 9f),
            Rule(MirrorMinnow, 0b011, SeasonMask.All, WeatherMask.All, Da | Mo | Mi | Af, 10f),
            Rule(SilverMullet, 0b011, SeasonMask.All, WeatherMask.All, Du | Ni | Da, 9f),
            Rule(SunnyTrout, 0b010, Sp | Su, WeatherMask.Clear, Mi | Af, 12f),
        };

        public static SpawnRuleData[] HarbourRules() => new[]
        {
            Rule(SilverMullet, 0b011, SeasonMask.All, WeatherMask.All, Du | Ni | Da, 9f),
        };

        public static JunkEntry[] LakeJunk() => new[]
        {
            new JunkEntry { JunkIndex = LostSock, Weight = 1f }, new JunkEntry { JunkIndex = OldBoot, Weight = 1f },
            new JunkEntry { JunkIndex = SoggyNewspaper, Weight = 1f }, new JunkEntry { JunkIndex = TinCan, Weight = 1f },
        };

        public static JunkEntry[] HarbourJunk() => new[] { new JunkEntry { JunkIndex = OldBoot, Weight = 1f } };

        public static ZoneData Lake(SpawnRuleData[] rules = null)
        {
            WeatherTableData w = Weather();
            return new ZoneData(LakeBands(), rules ?? LakeRules(), LakeJunk(), in w, null);
        }

        public static ZoneData Harbour(SpawnRuleData[] rules = null)
        {
            WeatherTableData w = Weather();
            return new ZoneData(HarbourBands(), rules ?? HarbourRules(), HarbourJunk(), in w, null);
        }

        public static EcologyDatabase Database(ZoneData lake = null, ZoneData harbour = null)
        {
            EcologyTuning t = Tuning();
            return new EcologyDatabase(Fishing(), new[] { lake ?? Lake(), harbour ?? Harbour() },
                new[] { new SpotEcologyData { ZoneIndex = ZoneLake, MapOrder = 0 }, new SpotEcologyData { ZoneIndex = ZoneHarbour, MapOrder = 1 } },
                in t);
        }

        // Neutral context: M3-D5 neutral bait (rating 1), neutral events and skills, depth 3 m, luck 0, one player.
        public static FishingContext Context(byte subArea, int minute, Season season, WeatherMask weather, int rodTier = 0, int zone = ZoneLake) =>
            new FishingContext { ZoneIndex = zone, SubAreaIndex = subArea, Depth = 3f, Minute = minute, Season = season, Weather = weather,
                                 RodTier = rodTier, Bait = BaitMods.Neutral(1), InHotspot = false, Luck = 0f,
                                 Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = 1 };
    }
}
