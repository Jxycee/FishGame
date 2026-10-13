// Tests/EditMode/Ecology/EcologyTestData.cs - the shared M3 host-ledger fixture (T13; C 7.5 note, M3-D54).
// FishingTestData's rods, tuning, motion profiles, species and junk (R72-owned values, never edited here) plus one special, with BAITS
// rebuilt with explicit M3 fields (AffinityMult 1, SpecialMult 1: a default 0 would silently disable specials, M3-D54).
// Zones: Lake (three spec 12.5 bands, one all-gates rule per regular species, the special), Limited (carp, limit 1, no junk share)
// and SpecialOnly (the special at chance 0.5, limit 1). Extra zones a test needs are appended after these three.
// A separate data set from T5's EcologyCoreTestData (M3-D67); it only reads that fixture's weather table and hotspot tuning.
using FishGame.Core;

namespace FishGame.Tests.EditMode
{
    internal static class EcologyTestData
    {
        public const int SpeciesSpecial = 5;                                   // after FishingTestData's five species (their constants stay valid)
        public const int ZoneLake = 0, ZoneLimited = 1, ZoneSpecialOnly = 2;
        public const int FirstExtraZone = 3;
        public const byte SubShallow = 0, SubMid = 1, SubFar = 2;

        // FishingTestData.Baits() in the same order with explicit M3 fields (never the FishingTestData instances unchanged).
        public static BaitStats[] Baits()
        {
            BaitStats[] b = FishingTestData.Baits();
            for (int i = 0; i < b.Length; i++)
            {
                b[i].AffinityTags = FishTags.None;
                b[i].AffinityMult = 1f;
                b[i].SpecialMult = 1f;
            }
            return b;
        }

        public static FishSpeciesData[] Species()
        {
            FishSpeciesData[] regular = FishingTestData.Species();
            var all = new FishSpeciesData[regular.Length + 1];
            for (int i = 0; i < regular.Length; i++) all[i] = regular[i];
            all[SpeciesSpecial] = new FishSpeciesData
            {
                FightProfileIndex = FishingTestData.ProfileBulldog, WeightMinG = 2000, WeightMaxG = 6000, BaseValue = 60,
                WeightSkew = 2.2f, LengthShapeK = 1f, BiteWaitMod = 1f, HookWindow = 0f,
                IsSpecial = true, Rarity = FishRarity.Legendary, Tags = FishTags.None,
            };
            return all;
        }

        // Every gate open: all sub-areas, seasons, weathers and bands; rod 0, bait 0, depth 0, no player minimum.
        public static SpawnRuleData Rule(int species, float weight = 1f, int limit = -1, bool special = false, float chance = 0f) =>
            new SpawnRuleData
            {
                SpeciesIndex = (ushort)species, SubAreaMask = 0, Seasons = SeasonMask.All, Weather = WeatherMask.All, Bands = TimeBandMask.All,
                Weight = weight, MinRodTier = 0, MinBaitRating = 0, MinDepth = 0f,
                Special = special, SpecialChance = chance, CatchLimitPerRun = limit, MinPlayersInSession = 0,
            };

        static SubAreaBandData Band(float min, float max, float junk) => new SubAreaBandData { MinShoreM = min, MaxShoreM = max, JunkChance = junk };

        public static SubAreaBandData[] OneBand(float junkChance) => new[] { Band(0f, float.PositiveInfinity, junkChance) };

        static JunkEntry[] JunkBoth() => new[] { new JunkEntry { JunkIndex = 0, Weight = 1f }, new JunkEntry { JunkIndex = 1, Weight = 1f } };
        public static JunkEntry[] JunkFirst() => new[] { new JunkEntry { JunkIndex = 0, Weight = 1f } };

        public static ZoneData Zone(SubAreaBandData[] bands, SpawnRuleData[] rules, JunkEntry[] junk = null)
        {
            WeatherTableData w = EcologyCoreTestData.Weather();   // spec 12.2 matrix
            return new ZoneData(bands, rules, junk ?? JunkFirst(), in w, null);
        }

        public static ZoneData LakeZone()
        {
            var rules = new SpawnRuleData[6];
            for (int s = 0; s < 5; s++) rules[s] = Rule(s);
            rules[SpeciesSpecial] = Rule(SpeciesSpecial, 0f, 1, true, 0.02f);
            return Zone(new[] { Band(0f, 5f, 0.20f), Band(5f, 10f, 0.12f), Band(10f, float.PositiveInfinity, 0.10f) }, rules, JunkBoth());
        }

        // Deterministic: every cast rolls carp until the limit is reached (junk share 0), then the empty pool gives junk.
        public static ZoneData LimitedZone() => Zone(OneBand(0f), new[] { Rule(FishingTestData.SpeciesCarp, 1f, 1) });

        // No regular rule: a failed special roll falls through the empty pool to junk.
        public static ZoneData SpecialOnlyZone() => Zone(OneBand(0f), new[] { Rule(SpeciesSpecial, 0f, 1, true, 0.5f) });

        public static EcologyDatabase Database(ZoneData[] extraZones = null, FishingTuning? tuning = null)
        {
            FishingTuning t = tuning ?? FishingTestData.Tuning();
            var fishing = new FishingDatabase(FishingTestData.Rods(), Baits(), FishingTestData.MotionProfiles(), Species(),
                                              FishingTestData.Junk(), in t);
            int extra = extraZones == null ? 0 : extraZones.Length;
            var zones = new ZoneData[FirstExtraZone + extra];
            zones[ZoneLake] = LakeZone();
            zones[ZoneLimited] = LimitedZone();
            zones[ZoneSpecialOnly] = SpecialOnlyZone();
            for (int i = 0; i < extra; i++) zones[FirstExtraZone + i] = extraZones[i];
            EcologyTuning et = EcologyCoreTestData.Tuning();   // 60/120/60/120/45/60, radius 3
            return new EcologyDatabase(fishing, zones, new[] { new SpotEcologyData { ZoneIndex = ZoneLake, MapOrder = 0 } }, in et);
        }

        // The world part of a host context; the ledger overwrites RodTier, Bait and Depth (M3-D15). Events and Skills are Neutral.
        public static FishingContext Context(int zone = ZoneLake, byte subArea = SubMid, Season season = Season.Summer,
                                             WeatherMask weather = WeatherMask.Clear, int minute = 300, bool inHotspot = false, int players = 1) =>
            new FishingContext
            {
                ZoneIndex = zone, SubAreaIndex = subArea, Depth = float.PositiveInfinity, Minute = minute, Season = season, Weather = weather,
                RodTier = 0, Bait = BaitMods.Neutral(1), InHotspot = inHotspot, Luck = 0f,
                Events = EventMods.Neutral, Skills = SkillMods.Neutral, PlayersInSession = players,
            };

        // Player at the origin; GridWater and Context are always set (a default view rejects NotWater and its Context throws, D-F3).
        public static HostCastView View(float depth = 3f, bool water = true, bool hasBed = true, bool allowed = true, bool gridWater = true) =>
            View(Context(), depth, water, hasBed, allowed, gridWater);

        public static HostCastView View(in FishingContext ctx, float depth = 3f, bool water = true, bool hasBed = true, bool allowed = true,
                                        bool gridWater = true) =>
            new HostCastView
            {
                PlayerX = 0f, PlayerY = 0f, PlayerZ = 0f, CastingAllowed = allowed, GridWater = gridWater, Context = ctx,
                Probe = new WaterProbe { FirstHitIsWater = water, WaterY = 0f, HasBed = hasBed, Depth = depth },
            };

        // Handmade rod, worm bait, EnsureSlot(0) ONLY: other slots are unknown until a test ensures them.
        public static FishingHostLedger Ledger(EcologyDatabase db = null, EcologyRunState run = null, ulong seed = 42)
        {
            var l = new FishingHostLedger(db ?? Database(), run ?? new EcologyRunState(seed), FishingTestData.RodHandmade, FishingTestData.BaitWorm);
            l.EnsureSlot(0);
            return l;
        }
    }
}
