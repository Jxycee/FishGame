// The M3 roster (spec 12.6): 23 regular species plus 2 specials, and the one rule per (row, zone). Values are the spec rows verbatim (kg x 1000 to grams);
// presentation values are plan data-coverage 1.1 and M3-D35. Bluegill (row 2) equals T3's committed FishingSeedData row field for field.
// Sub-area ids: lake reeds / open_water / deep_water, harbour shallows / channel / open_sea. Never names MinBaitRating or MinDepth (they stay 0;
// T13 removes the fields without touching this file). Owned by T10; T11/T12 may record "// T12 retune: <old> -> <new>" next to a retuned value.
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static partial class EcologySeedData
    {
        private const SeasonMask Sp = SeasonMask.Spring, Su = SeasonMask.Summer, Au = SeasonMask.Autumn, Wi = SeasonMask.Winter, AllSeasons = SeasonMask.All;
        private const WeatherMask AnyWeather = WeatherMask.All, Clear = WeatherMask.Clear, Rain = WeatherMask.Rain, Fog = WeatherMask.Fog;
        private const TimeBandMask Da = TimeBandMask.Dawn, Mo = TimeBandMask.Morning, Mi = TimeBandMask.Midday, Af = TimeBandMask.Afternoon,
                                   Du = TimeBandMask.Dusk, Ni = TimeBandMask.Night, AllBands = TimeBandMask.All;
        private const string Lake = "lake_dock", Harbour = "harbour_pier";

        private static Color Rgb(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        private static void BuildRoster(out List<SpeciesSeed> species, out List<RuleSeed> lakeRules, out List<RuleSeed> harbourRules)
        {
            var sortedProfileIds = new string[FishingSeedData.Profiles.Count];
            for (int i = 0; i < sortedProfileIds.Length; i++) sortedProfileIds[i] = FishingSeedData.Profiles[i].id;
            Array.Sort(sortedProfileIds, StringComparer.Ordinal);

            var sp = new List<SpeciesSeed>();
            var lake = new List<RuleSeed>();
            var harbour = new List<RuleSeed>();

            // Columns: id, name, profile, g min, g max, baseValue, Sw, rarity, rod, tags | shape, lengthShapeK, colour A, colour B, model file | lake sub-areas, harbour sub-areas
            // | seasons, weather, bands.

            // 12.6 row 1
            Row(sp, lake, harbour, sortedProfileIds, "mirror_minnow", "Mirror Minnow", "darter", 50, 200, 5, 10f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Long, 1.0f, Rgb(0.80f, 0.84f, 0.88f), Rgb(0.20f, 0.62f, 0.66f), "Fish_MirrorMinnow.fbx",
                "reeds open_water", null, AllSeasons, AnyWeather, Da | Mo | Mi | Af);
            // 12.6 row 2 (equals T3's FishingSeedData row: 200-600 g, value 8, biteWaitMod 1.0, Schooling, gates 0/0, M2 colours, Fish_Bluegill.fbx)
            Row(sp, lake, harbour, sortedProfileIds, "bluegill", "Bluegill", "steady", 200, 600, 8, 10f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Round, 1.0f, Rgb(0.32f, 0.50f, 0.62f), Rgb(0.95f, 0.62f, 0.22f), "Fish_Bluegill.fbx",
                "reeds open_water", null, Sp | Su | Au, AnyWeather, Mo | Mi | Af | Du);
            // 12.6 row 3
            Row(sp, lake, harbour, sortedProfileIds, "yellow_perch", "Yellow Perch", "darter", 200, 1200, 9, 10f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Round, 1.0f, Rgb(0.62f, 0.68f, 0.30f), Rgb(0.20f, 0.24f, 0.16f), "Fish_Perch.fbx",
                "open_water deep_water", null, Sp | Au | Wi, AnyWeather, AllBands);
            // 12.6 row 4
            Row(sp, lake, harbour, sortedProfileIds, "grumpy_carp", "Grumpy Carp", "bulldog", 2000, 12000, 10, 9f, FishRarity.Common, 0, FishTags.Bottom,
                BodyShape.Round, 1.0f, Rgb(0.72f, 0.58f, 0.30f), Rgb(0.45f, 0.34f, 0.18f), "Fish_Carp.fbx",
                "open_water deep_water", null, Su | Au, AnyWeather, Mi | Af | Du | Ni);
            // 12.6 row 5
            Row(sp, lake, harbour, sortedProfileIds, "bucketmouth_bass", "Bucketmouth Bass", "bulldog", 500, 4000, 11, 9f, FishRarity.Common, 0, FishTags.Predator,
                BodyShape.Round, 1.0f, Rgb(0.36f, 0.50f, 0.26f), Rgb(0.88f, 0.86f, 0.70f), "Fish_BucketmouthBass.fbx",
                "deep_water open_water", null, Sp | Su, AnyWeather, Da | Mo | Du | Ni);
            // 12.6 row 6 (lake AND harbour: two rules)
            Row(sp, lake, harbour, sortedProfileIds, "silver_mullet", "Silver Mullet", "steady", 300, 2000, 8, 9f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Long, 1.0f, Rgb(0.70f, 0.74f, 0.78f), Rgb(0.30f, 0.36f, 0.44f), "Fish_SilverMullet.fbx",
                "reeds open_water", "shallows channel", AllSeasons, AnyWeather, Du | Ni | Da);
            // 12.6 row 7
            Row(sp, lake, harbour, sortedProfileIds, "lake_pike", "Lake Pike", "runner", 1500, 9000, 20, 9f, FishRarity.Common, 0, FishTags.Predator,
                BodyShape.Long, 1.0f, Rgb(0.40f, 0.52f, 0.30f), Rgb(0.86f, 0.84f, 0.52f), "Fish_LakePike.fbx",
                "deep_water reeds", null, Au | Wi, AnyWeather, Da | Mo | Du | Ni);
            // 12.6 row 8
            Row(sp, lake, harbour, sortedProfileIds, "sunny_trout", "Sunny Trout", "runner", 500, 3000, 30, 12f, FishRarity.Uncommon, 0, FishTags.Surface,
                BodyShape.Long, 1.0f, Rgb(0.55f, 0.62f, 0.55f), Rgb(0.88f, 0.45f, 0.48f), "Fish_Trout.fbx",
                "open_water", null, Sp | Su, Clear, Mi | Af);
            // 12.6 row 9
            Row(sp, lake, harbour, sortedProfileIds, "mudcat", "Mudcat", "steady", 800, 5000, 22, 12f, FishRarity.Uncommon, 0, FishTags.Bottom | FishTags.Nocturnal,
                BodyShape.Round, 1.0f, Rgb(0.36f, 0.34f, 0.30f), Rgb(0.70f, 0.66f, 0.56f), "Fish_Catfish.fbx",
                "reeds open_water", null, Sp | Su | Au, Rain | Fog, Du | Ni | Da);
            // 12.6 row 10
            Row(sp, lake, harbour, sortedProfileIds, "frost_char", "Frost Char", "runner", 400, 2500, 26, 10f, FishRarity.Uncommon, 0, FishTags.None,
                BodyShape.Long, 1.0f, Rgb(0.62f, 0.76f, 0.90f), Rgb(0.92f, 0.40f, 0.30f), "Fish_FrostChar.fbx",
                "deep_water reeds", null, Wi, AnyWeather, Mi | Af);
            // 12.6 row 11
            Row(sp, lake, harbour, sortedProfileIds, "glimmer_eel", "Glimmer Eel", "thrasher", 500, 2500, 60, 8f, FishRarity.Rare, 1, FishTags.Nocturnal,
                BodyShape.Eel, 1.6f, Rgb(0.16f, 0.22f, 0.40f), Rgb(0.40f, 0.95f, 0.85f), "Fish_GlimmerEel.fbx",
                "deep_water", null, Su | Au, Rain | Fog, Du | Ni);
            // 12.6 row 12
            Row(sp, lake, harbour, sortedProfileIds, "sleepy_sturgeon", "Sleepy Sturgeon", "bulldog", 6000, 25000, 75, 3f, FishRarity.Rare, 2, FishTags.Bottom,
                BodyShape.Long, 1.0f, Rgb(0.42f, 0.40f, 0.38f), Rgb(0.78f, 0.72f, 0.60f), "Fish_SleepySturgeon.fbx",
                "deep_water", null, Sp | Wi, AnyWeather, Mo | Mi | Af);
            // 12.6 row 13
            Row(sp, lake, harbour, sortedProfileIds, "pier_sardine", "Pier Sardine", "darter", 100, 300, 5, 10f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Long, 1.0f, Rgb(0.52f, 0.66f, 0.80f), Rgb(0.92f, 0.94f, 0.96f), "Fish_PierSardine.fbx",
                null, "shallows channel", Sp | Su | Au, AnyWeather, Da | Mo | Mi | Af);
            // 12.6 row 14
            Row(sp, lake, harbour, sortedProfileIds, "ice_herring", "Ice Herring", "darter", 200, 600, 8, 7f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Long, 1.0f, Rgb(0.78f, 0.88f, 0.96f), Rgb(0.30f, 0.44f, 0.62f), "Fish_IceHerring.fbx",
                null, "shallows channel", Au | Wi, AnyWeather, AllBands);
            // 12.6 row 15
            Row(sp, lake, harbour, sortedProfileIds, "grumble_mackerel", "Grumble Mackerel", "runner", 400, 1500, 12, 10f, FishRarity.Common, 0, FishTags.Schooling,
                BodyShape.Long, 1.0f, Rgb(0.20f, 0.48f, 0.46f), Rgb(0.10f, 0.14f, 0.20f), "Fish_GrumbleMackerel.fbx",
                null, "channel open_sea", AllSeasons, AnyWeather, Da | Mo | Mi | Af);
            // 12.6 row 16
            Row(sp, lake, harbour, sortedProfileIds, "moon_squid", "Moon Squid", "thrasher", 500, 3000, 16, 9f, FishRarity.Common, 0, FishTags.Nocturnal,
                BodyShape.Squid, 1.0f, Rgb(0.88f, 0.86f, 0.96f), Rgb(0.58f, 0.40f, 0.80f), "Fish_MoonSquid.fbx",
                null, "open_sea channel", AllSeasons, AnyWeather, Du | Ni);
            // 12.6 row 17
            Row(sp, lake, harbour, sortedProfileIds, "gawping_gurnard", "Gawping Gurnard", "steady", 300, 1800, 14, 8f, FishRarity.Common, 0, FishTags.Bottom,
                BodyShape.Round, 1.0f, Rgb(0.86f, 0.36f, 0.28f), Rgb(0.98f, 0.84f, 0.62f), "Fish_GawpingGurnard.fbx",
                null, "channel shallows", Sp | Su, AnyWeather, Mo | Mi | Af | Du | Ni);
            // 12.6 row 18
            Row(sp, lake, harbour, sortedProfileIds, "sneaky_flounder", "Sneaky Flounder", "steady", 500, 2000, 15, 10f, FishRarity.Common, 0, FishTags.Bottom,
                BodyShape.Flat, 1.0f, Rgb(0.62f, 0.54f, 0.40f), Rgb(0.30f, 0.26f, 0.20f), "Fish_SneakyFlounder.fbx",
                null, "shallows channel", Sp | Au | Wi, AnyWeather, Da | Mo | Mi | Af);
            // 12.6 row 19
            Row(sp, lake, harbour, sortedProfileIds, "red_snapper", "Red Snapper", "bulldog", 1000, 4000, 28, 12f, FishRarity.Uncommon, 0, FishTags.Predator,
                BodyShape.Round, 1.0f, Rgb(0.86f, 0.24f, 0.22f), Rgb(0.98f, 0.70f, 0.66f), "Fish_RedSnapper.fbx",
                null, "channel", Su | Au, Rain | Fog, Af | Du | Ni);
            // 12.6 row 20
            Row(sp, lake, harbour, sortedProfileIds, "grumpy_puffer", "Grumpy Puffer", "thrasher", 300, 1000, 32, 12f, FishRarity.Uncommon, 1, FishTags.Surface,
                BodyShape.Round, 0.8f, Rgb(0.92f, 0.82f, 0.40f), Rgb(0.40f, 0.30f, 0.20f), "Fish_GrumpyPuffer.fbx",
                null, "channel open_sea", Sp | Su, Clear, Mi | Af);
            // 12.6 row 21
            Row(sp, lake, harbour, sortedProfileIds, "bruiser_tuna", "Bruiser Tuna", "bulldog", 8000, 40000, 80, 3f, FishRarity.Rare, 2, FishTags.Predator,
                BodyShape.Long, 1.0f, Rgb(0.16f, 0.24f, 0.44f), Rgb(0.80f, 0.84f, 0.88f), "Fish_BruiserTuna.fbx",
                null, "open_sea", Su | Au, Clear | Rain, Da | Mo);
            // 12.6 row 22
            Row(sp, lake, harbour, sortedProfileIds, "storm_ray", "Storm Ray", "thrasher", 3000, 15000, 65, 7f, FishRarity.Rare, 1, FishTags.Bottom,
                BodyShape.Flat, 1.0f, Rgb(0.32f, 0.36f, 0.44f), Rgb(0.84f, 0.88f, 0.94f), "Fish_StormRay.fbx",
                null, "open_sea", Au | Wi, Rain | Fog, Af | Du);
            // 12.6 row 23
            Row(sp, lake, harbour, sortedProfileIds, "lanternfish", "Lanternfish", "darter", 100, 400, 70, 10f, FishRarity.Rare, 1, FishTags.Nocturnal,
                BodyShape.Long, 1.0f, Rgb(0.10f, 0.12f, 0.22f), Rgb(1.00f, 0.90f, 0.40f), "Fish_Lanternfish.fbx",
                null, "open_sea channel", Au | Wi, Fog, Du | Ni);

            // 12.6 specials: one rule each, all gates open, rod 0, special, chance 0.02 / 0.015, limit 1, Bulldog, Legendary, isSpecial.
            Special(sp, lake, harbour, sortedProfileIds, "gilded_carp", "Gilded Carp", 3000, 15000, 200, 0.02f,
                BodyShape.Round, Rgb(0.95f, 0.76f, 0.25f), Rgb(0.70f, 0.46f, 0.12f), "Fish_GildedCarp.fbx", "deep_water", null);
            Special(sp, lake, harbour, sortedProfileIds, "old_barnacle", "Old Barnacle", 5000, 20000, 250, 0.015f,
                BodyShape.Round, Rgb(0.40f, 0.44f, 0.40f), Rgb(0.86f, 0.82f, 0.74f), "Fish_OldBarnacle.fbx", null, "open_sea");

            species = sp;
            lakeRules = lake;
            harbourRules = harbour;
        }

        // One roster row: the species seed (weightSkew 2.2, biteWaitMod 1.0, hookWindow 0, not special) and one rule per named zone.
        private static void Row(List<SpeciesSeed> species, List<RuleSeed> lake, List<RuleSeed> harbour, string[] sortedProfileIds,
            string id, string name, string profileId, int weightMinG, int weightMaxG, int baseValue, float spawnWeight, FishRarity rarity, int rod, FishTags tags,
            BodyShape shape, float lengthShapeK, Color colorA, Color colorB, string modelFile,
            string lakeSubAreas, string harbourSubAreas, SeasonMask seasons, WeatherMask weather, TimeBandMask bands)
        {
            species.Add(MakeSeed(sortedProfileIds, id, name, profileId, weightMinG, weightMaxG, baseValue, lengthShapeK, rarity, false, tags,
                shape, colorA, colorB, modelFile));
            if (lakeSubAreas != null)
                lake.Add(new RuleSeed(id, lakeSubAreas.Split(' '), seasons, weather, bands, spawnWeight, (byte)rod, false, 0f, -1));
            if (harbourSubAreas != null)
                harbour.Add(new RuleSeed(id, harbourSubAreas.Split(' '), seasons, weather, bands, spawnWeight, (byte)rod, false, 0f, -1));
        }

        private static void Special(List<SpeciesSeed> species, List<RuleSeed> lake, List<RuleSeed> harbour, string[] sortedProfileIds,
            string id, string name, int weightMinG, int weightMaxG, int baseValue, float specialChance,
            BodyShape shape, Color colorA, Color colorB, string modelFile, string lakeSubAreas, string harbourSubAreas)
        {
            species.Add(MakeSeed(sortedProfileIds, id, name, "bulldog", weightMinG, weightMaxG, baseValue, 1.0f, FishRarity.Legendary, true, FishTags.None,
                shape, colorA, colorB, modelFile));
            // weight 0: ignored for specials (and a wrongly cleared special flag then fails the "non-special weight > 0" rule).
            if (lakeSubAreas != null)
                lake.Add(new RuleSeed(id, lakeSubAreas.Split(' '), SeasonMask.All, WeatherMask.All, TimeBandMask.All, 0f, 0, true, specialChance, 1));
            if (harbourSubAreas != null)
                harbour.Add(new RuleSeed(id, harbourSubAreas.Split(' '), SeasonMask.All, WeatherMask.All, TimeBandMask.All, 0f, 0, true, specialChance, 1));
        }

        private static SpeciesSeed MakeSeed(string[] sortedProfileIds, string id, string name, string profileId, int weightMinG, int weightMaxG, int baseValue,
            float lengthShapeK, FishRarity rarity, bool isSpecial, FishTags tags, BodyShape shape, Color colorA, Color colorB, string modelFile)
        {
            int index = Array.IndexOf(sortedProfileIds, profileId);
            if (index < 0) throw new InvalidOperationException("species seed '" + id + "' names unknown profile '" + profileId + "'");
            var data = new FishSpeciesData
            {
                FightProfileIndex = (ushort)index, WeightMinG = weightMinG, WeightMaxG = weightMaxG, BaseValue = baseValue,
                WeightSkew = 2.2f, LengthShapeK = lengthShapeK, BiteWaitMod = 1.0f, HookWindow = 0f, IsSpecial = isSpecial, Rarity = rarity, Tags = tags,
            };
            return new SpeciesSeed(id, name, profileId, data, shape, colorA, colorB, modelFile);
        }
    }
}
