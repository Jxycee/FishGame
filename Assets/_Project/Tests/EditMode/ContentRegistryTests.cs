using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FishGame.Core;
using FishGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class ContentRegistryTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in created)
            {
                if (o != null)
                {
                    UnityEngine.Object.DestroyImmediate(o);
                }
            }
            created.Clear();
        }

        private SpotDef MakeSpot(string id, string displayName = "Name")
        {
            SpotDef spot = ScriptableObject.CreateInstance<SpotDef>();
            created.Add(spot);
            spot.Configure(id, displayName, "Spot_" + id);
            return spot;
        }

        private ContentRegistry MakeRegistry(int dataVersion, params SpotDef[] spots)
        {
            ContentRegistry reg = ScriptableObject.CreateInstance<ContentRegistry>();
            created.Add(reg);
            reg.Configure(dataVersion, spots);
            return reg;
        }

        [Test]
        public void ContentRegistry_Hash_StableAcrossInstances()
        {
            SpotDef lake = MakeSpot(ContentIds.LakeDock, "Lake Dock");
            SpotDef pond = MakeSpot("pond_north", "Pond North");
            ContentRegistry a = MakeRegistry(1, lake, pond);
            ContentRegistry b = MakeRegistry(1, pond, lake);

            ulong hashA = a.ComputeContentHash();
            ulong hashB = b.ComputeContentHash();
            Assert.AreEqual(hashA, hashB);
            Assert.AreEqual(hashA, a.ComputeContentHash());

            // Independent expected value. Sorted ids: "lake_dock" < "pond_north" ('l' < 'p').
            ulong expected = ContentHash.Compute(
                new[] { new ContentCategoryIds(ContentRegistry.CategorySpots, new[] { "lake_dock", "pond_north" }) },
                1);
            Assert.AreEqual(expected, hashA);
            Assert.AreNotEqual(expected, MakeRegistry(2, lake, pond).ComputeContentHash());
        }

        [Test]
        public void ContentRegistry_Validate_FlagsBadIds()
        {
            SpotDef bad = MakeSpot("Bad Id");
            SpotDef good = MakeSpot("ok_spot");
            SpotDef dup = MakeSpot("ok_spot");
            ContentRegistry reg = MakeRegistry(1, bad, good, dup, null);

            IReadOnlyList<string> errors = reg.Validate();

            Assert.AreEqual(3, errors.Count);
            Assert.AreEqual("spots[0] has invalid id 'Bad Id'.", errors[0]);
            Assert.AreEqual("spots[2] duplicate id 'ok_spot'.", errors[1]);
            Assert.AreEqual("spots[3] is null.", errors[2]);
            Assert.AreEqual(0, MakeRegistry(1, good).Validate().Count);
        }

        [Test]
        public void ContentRegistry_Build_InvalidData_Throws()
        {
            ContentRegistry reg = MakeRegistry(1, MakeSpot("Bad Id"));
            Assert.Throws<InvalidOperationException>(() => reg.Build());
            Assert.Throws<InvalidOperationException>(() => reg.ComputeContentHash());

            ContentRegistry dupReg = MakeRegistry(1, MakeSpot("same_id"), MakeSpot("same_id"));
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => dupReg.Build());
            StringAssert.Contains("duplicate id 'same_id'", ex.Message);
        }

        [Test]
        public void ContentRegistry_SpotAt_RoundTripsSpotIndex()
        {
            SpotDef lake = MakeSpot(ContentIds.LakeDock, "Lake Dock");
            SpotDef pond = MakeSpot("pond_north", "Pond North");
            ContentRegistry reg = MakeRegistry(1, pond, lake);

            // Sorted order: "lake_dock" (index 0), "pond_north" (index 1).
            Assert.AreEqual((ushort)0, reg.SpotIndex("lake_dock"));
            Assert.AreEqual((ushort)1, reg.SpotIndex("pond_north"));
            Assert.AreSame(lake, reg.SpotAt((ushort)0));
            Assert.AreSame(pond, reg.SpotAt((ushort)1));
            Assert.IsTrue(reg.TryGetSpot("lake_dock", out SpotDef found));
            Assert.AreSame(lake, found);
            Assert.IsFalse(reg.TryGetSpot("missing", out _));
            Assert.IsFalse(reg.TryGetSpot(null, out _));
            Assert.Throws<KeyNotFoundException>(() => reg.SpotIndex("missing"));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.SpotAt((ushort)2));
            Assert.AreEqual(2, reg.Spots.Count);
            Assert.AreEqual(1, reg.DataVersion);
            Assert.AreEqual("Lake Dock", lake.DisplayName);
            Assert.AreEqual("Spot_lake_dock", lake.SceneName);
        }

        // ---- T13: fishing categories ------------------------------------------------------------------------------------------

        private sealed class FishingSet
        {
            public RodDef[] Rods;
            public BaitDef[] Baits;
            public FightProfileDef[] Profiles;
            public FishSpeciesDef[] Species;
            public JunkDef[] Junk;
            public FishingTuningDef Tuning;
            public SfxCueTableDef Cues;
            public ArtPaletteDef Palette;
            public WaterSettingsDef Water;
        }

        private T MakeAsset<T>() where T : ScriptableObject
        {
            T o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        // 2 rods, 2 baits (one unlimited), 2 profiles, 2 species, 1 junk and the four singletons, listed in scrambled id order.
        private FishingSet MakeFishingSet()
        {
            var set = new FishingSet();
            RodDef twig = MakeAsset<RodDef>();
            twig.Configure("rod_twig", "Twig Rod", 0, 14f, 0.9f, 0.8f, 0.5f, 0.5f, 0.9f, 0, 0);
            RodDef carbon = MakeAsset<RodDef>();
            carbon.Configure("rod_carbon", "Carbon Rod", 2, 24f, 1.2f, 1.3f, 0.7f, 0.7f, 1.1f, 300, 2);
            set.Rods = new[] { twig, carbon };

            BaitDef worm = MakeAsset<BaitDef>();
            worm.Configure("bait_worm", "Worm", 3, 3, 9, 0.9f, 1f, 2, false);
            BaitDef bare = MakeAsset<BaitDef>();
            bare.Configure("bait_bare_hook", "Bare Hook", 1, 1, 1, 1.3f, 1.5f, 1, true);
            set.Baits = new[] { worm, bare };

            FightProfileDef steady = MakeAsset<FightProfileDef>();
            steady.Configure("steady", "Steady", 1f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0.6f, 0f, 0f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            FightProfileDef bulldog = MakeAsset<FightProfileDef>();
            bulldog.Configure("bulldog", "Bulldog", 1f, 3f, 2f, 4f, 0f, 0.55f, 0f, 0.06f, 2.5f, 0.5f, 0f, 1f, 1f, 1f, 4f);   // R72: diveChance 0.55, calmDrift 0.06
            set.Profiles = new[] { steady, bulldog };

            FishSpeciesDef carp = MakeAsset<FishSpeciesDef>();
            carp.Configure("carp", "Carp", FishRarity.Common, 12, 1000, 4000, 2.2f, 1f, bulldog, BodyShape.Round, Color.red, Color.green,
                1.2f, 0f, false, null);
            FishSpeciesDef bluegill = MakeAsset<FishSpeciesDef>();
            bluegill.Configure("bluegill", "Bluegill", FishRarity.Common, 5, 200, 800, 2.2f, 1f, steady, BodyShape.Round, Color.blue, Color.white,
                0.9f, 0f, false, null);
            set.Species = new[] { carp, bluegill };

            JunkDef boot = MakeAsset<JunkDef>();
            boot.Configure("old_boot", "Old Boot", 2, BodyShape.Long, Color.gray);
            set.Junk = new[] { boot };

            set.Tuning = MakeAsset<FishingTuningDef>();
            FishingTuning t = FishingTestData.Tuning();
            set.Tuning.Configure(in t);
            set.Cues = MakeAsset<SfxCueTableDef>();
            set.Palette = MakeAsset<ArtPaletteDef>();
            set.Water = MakeAsset<WaterSettingsDef>();
            return set;
        }

        private ContentRegistry MakeFishingRegistry(FishingSet set, params SpotDef[] spots)
        {
            ContentRegistry reg = MakeRegistry(1, spots);
            ConfigureFishing(reg, set);
            return reg;
        }

        private static void ConfigureFishing(ContentRegistry reg, FishingSet set)
        {
            reg.ConfigureFishing(set.Rods, set.Baits, set.Profiles, set.Species, set.Junk, set.Tuning, set.Cues, set.Palette, set.Water);
        }

        // Catches: ordinal sort not applied (insertion order used), one category wired to another's list (rods <-> baits), index
        // and AtIndex out of step, wrong exception types, DisplayNameOf mixing up junk and species.
        [Test]
        public void ContentRegistry_FishingCategories_IndexedOrdinal()
        {
            FishingSet set = MakeFishingSet();
            ContentRegistry reg = MakeFishingRegistry(set, MakeSpot(ContentIds.LakeDock));

            Assert.AreEqual((ushort)0, reg.RodIndex("rod_carbon"));
            Assert.AreEqual((ushort)1, reg.RodIndex("rod_twig"));
            Assert.AreEqual("rod_twig", reg.RodAt(1).Id);
            Assert.AreEqual("rod_carbon", reg.RodAt(0).Id);
            Assert.AreEqual((ushort)0, reg.BaitIndex("bait_bare_hook"));
            Assert.AreEqual((ushort)1, reg.BaitIndex("bait_worm"));
            Assert.AreEqual("bait_worm", reg.BaitAt(1).Id);
            Assert.AreEqual((ushort)0, reg.FightProfileIndex("bulldog"));
            Assert.AreEqual((ushort)1, reg.FightProfileIndex("steady"));
            Assert.AreEqual("steady", reg.FightProfileAt(1).Id);
            Assert.AreEqual((ushort)0, reg.SpeciesIndex("bluegill"));
            Assert.AreEqual((ushort)1, reg.SpeciesIndex("carp"));
            Assert.AreEqual("carp", reg.SpeciesAt(1).Id);
            Assert.AreEqual((ushort)0, reg.JunkIndex("old_boot"));
            Assert.AreEqual("old_boot", reg.JunkAt(0).Id);

            Assert.IsTrue(reg.TryGetSpecies("carp", out FishSpeciesDef carp));
            Assert.AreSame(set.Species[0], carp);
            Assert.IsFalse(reg.TryGetSpecies("trout", out FishSpeciesDef none));
            Assert.IsNull(none);
            Assert.IsFalse(reg.TryGetSpecies(null, out _));

            Assert.Throws<KeyNotFoundException>(() => reg.RodIndex("rod_nope"));
            Assert.Throws<KeyNotFoundException>(() => reg.BaitIndex("rod_twig"), "ids are per category");
            Assert.Throws<KeyNotFoundException>(() => reg.FightProfileIndex("nope"));
            Assert.Throws<KeyNotFoundException>(() => reg.SpeciesIndex("nope"));
            Assert.Throws<KeyNotFoundException>(() => reg.JunkIndex("nope"));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.RodAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.BaitAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.FightProfileAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.SpeciesAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.JunkAt(1));

            Assert.AreEqual("Bluegill", reg.DisplayNameOf(false, 0));
            Assert.AreEqual("Carp", reg.DisplayNameOf(false, 1));
            Assert.AreEqual("Old Boot", reg.DisplayNameOf(true, 0));

            Assert.AreEqual(2, reg.Rods.Count);
            Assert.AreEqual(2, reg.Baits.Count);
            Assert.AreEqual(2, reg.FightProfiles.Count);
            Assert.AreEqual(2, reg.Species.Count);
            Assert.AreEqual(1, reg.Junk.Count);
            Assert.AreSame(set.Tuning, reg.FishingTuning);
            Assert.AreSame(set.Cues, reg.SfxCueTable);
            Assert.AreSame(set.Palette, reg.ArtPalette);
            Assert.AreSame(set.Water, reg.WaterSettings);
        }

        // Catches (ruling D-Q1): an empty fishing category still hashed (a spots-only registry would no longer hash as in M1);
        // a category left out of the hash; order dependence; a category hashed under the wrong name.
        [Test]
        public void ContentRegistry_FishingCategories_ChangeHash()
        {
            SpotDef lake = MakeSpot(ContentIds.LakeDock);
            ContentRegistry spotsOnly = MakeRegistry(1, lake);
            ulong spotsHash = ContentHash.Compute(
                new[] { new ContentCategoryIds(ContentRegistry.CategorySpots, new[] { "lake_dock" }) }, 1);
            Assert.AreEqual(spotsHash, spotsOnly.ComputeContentHash(), "spots-only hashes as in M1");

            FishingSet set = MakeFishingSet();
            ContentRegistry full = MakeFishingRegistry(set, lake);
            ulong fullHash = full.ComputeContentHash();
            Assert.AreNotEqual(spotsHash, fullHash);
            ulong expected = ContentHash.Compute(new[]
            {
                new ContentCategoryIds(ContentRegistry.CategorySpots, new[] { "lake_dock" }),
                new ContentCategoryIds(ContentRegistry.CategoryRods, new[] { "rod_carbon", "rod_twig" }),
                new ContentCategoryIds(ContentRegistry.CategoryBaits, new[] { "bait_bare_hook", "bait_worm" }),
                new ContentCategoryIds(ContentRegistry.CategoryFightProfiles, new[] { "bulldog", "steady" }),
                new ContentCategoryIds(ContentRegistry.CategorySpecies, new[] { "bluegill", "carp" }),
                new ContentCategoryIds(ContentRegistry.CategoryJunk, new[] { "old_boot" }),
            }, 1);
            Assert.AreEqual(expected, fullHash);
            Assert.AreEqual("rods", ContentRegistry.CategoryRods);
            Assert.AreEqual("baits", ContentRegistry.CategoryBaits);
            Assert.AreEqual("fight_profiles", ContentRegistry.CategoryFightProfiles);
            Assert.AreEqual("species", ContentRegistry.CategorySpecies);
            Assert.AreEqual("junk", ContentRegistry.CategoryJunk);

            // Without junk: the junk category is left out of the hash entirely (not appended empty).
            FishingSet noJunk = MakeFishingSet();
            noJunk.Junk = new JunkDef[0];
            ulong noJunkHash = MakeFishingRegistry(noJunk, lake).ComputeContentHash();
            ulong expectedNoJunk = ContentHash.Compute(new[]
            {
                new ContentCategoryIds(ContentRegistry.CategorySpots, new[] { "lake_dock" }),
                new ContentCategoryIds(ContentRegistry.CategoryRods, new[] { "rod_carbon", "rod_twig" }),
                new ContentCategoryIds(ContentRegistry.CategoryBaits, new[] { "bait_bare_hook", "bait_worm" }),
                new ContentCategoryIds(ContentRegistry.CategoryFightProfiles, new[] { "bulldog", "steady" }),
                new ContentCategoryIds(ContentRegistry.CategorySpecies, new[] { "bluegill", "carp" }),
            }, 1);
            Assert.AreEqual(expectedNoJunk, noJunkHash);
            Assert.AreNotEqual(fullHash, noJunkHash);

            // One more species changes it.
            FishingSet more = MakeFishingSet();
            FishSpeciesDef extra = MakeAsset<FishSpeciesDef>();
            extra.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, more.Profiles[0], BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, null);
            more.Species = new[] { more.Species[0], more.Species[1], extra };
            Assert.AreNotEqual(fullHash, MakeFishingRegistry(more, lake).ComputeContentHash());

            // The same content in another order hashes the same.
            FishingSet reordered = MakeFishingSet();
            Array.Reverse(reordered.Rods);
            Array.Reverse(reordered.Baits);
            Array.Reverse(reordered.Profiles);
            Array.Reverse(reordered.Species);
            Assert.AreEqual(fullHash, MakeFishingRegistry(reordered, lake).ComputeContentHash());
        }

        // Catches: a fight reference that is not in fightProfiles passing (id-only lookup), a null fight crashing Build, Build
        // not refusing an invalid fishing set, a missing singleton or missing unlimited bait not reported, wrong message text.
        [Test]
        public void ContentRegistry_SpeciesWithForeignProfile_FailsValidate()
        {
            FishingSet set = MakeFishingSet();
            FightProfileDef foreign = MakeAsset<FightProfileDef>();
            foreign.Configure("darter", "Darter", 0.3f, 0.8f, 0.4f, 0.9f, 1f, 0f, 0f, 0f, 0f, 0.3f, 0f, 1f, 1f, 1f, 4f);
            FishSpeciesDef perch = MakeAsset<FishSpeciesDef>();
            perch.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, foreign, BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, null);
            set.Species = new[] { perch };
            ContentRegistry reg = MakeFishingRegistry(set);
            IReadOnlyList<string> errors = reg.Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("species[0] 'perch' fight 'darter' is not listed in fightProfiles.", errors[0]);
            Assert.Throws<InvalidOperationException>(() => reg.Build());

            perch.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, null, BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, null);
            errors = reg.Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("species[0] 'perch' has no fight profile.", errors[0]);
            Assert.Throws<InvalidOperationException>(() => reg.Build());

            // A different Def object that merely shares the id of a listed profile is still foreign (reference containment).
            FightProfileDef lookalike = MakeAsset<FightProfileDef>();
            lookalike.Configure("bulldog", "Bulldog copy", 1f, 3f, 2f, 4f, 0f, 0.35f, 0f, 0.12f, 2.5f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            perch.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, lookalike, BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, null);
            errors = reg.Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("species[0] 'perch' fight 'bulldog' is not listed in fightProfiles.", errors[0]);

            FishingSet noUnlimited = MakeFishingSet();
            noUnlimited.Baits[1].Configure("bait_bare_hook", "Bare Hook", 1, 1, 1, 1.3f, 1.5f, 1, false);
            errors = MakeFishingRegistry(noUnlimited).Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("baits has no unlimited bait.", errors[0]);

            FishingSet noTuning = MakeFishingSet();
            noTuning.Tuning = null;
            errors = MakeFishingRegistry(noTuning).Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("fishingTuning is missing.", errors[0]);

            FishingSet noSingletons = MakeFishingSet();
            noSingletons.Cues = null;
            noSingletons.Palette = null;
            noSingletons.Water = null;
            errors = MakeFishingRegistry(noSingletons).Validate();
            CollectionAssert.AreEqual(new[] { "sfxCueTable is missing.", "artPalette is missing.", "waterSettings is missing." }, errors);

            FishingSet noRods = MakeFishingSet();
            noRods.Rods = new RodDef[0];
            errors = MakeFishingRegistry(noRods).Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("rods is empty.", errors[0]);

            FishingSet noBaits = MakeFishingSet();
            noBaits.Baits = new BaitDef[0];
            CollectionAssert.AreEqual(new[] { "baits is empty.", "baits has no unlimited bait." }, MakeFishingRegistry(noBaits).Validate());

            FishingSet noProfiles = MakeFishingSet();
            noProfiles.Profiles = new FightProfileDef[0];
            CollectionAssert.AreEqual(new[]
            {
                "species[0] 'carp' fight 'bulldog' is not listed in fightProfiles.",
                "species[1] 'bluegill' fight 'steady' is not listed in fightProfiles.",
                "fightProfiles is empty.",
            }, MakeFishingRegistry(noProfiles).Validate());

            // Per-entry list messages use the serialized list names.
            FishingSet bad = MakeFishingSet();
            RodDef badRod = MakeAsset<RodDef>();
            badRod.Configure("Bad Id", "x", 0, 1f, 1f, 1f, 0.5f, 0.5f, 1f, 0, 0);
            RodDef dupRod = MakeAsset<RodDef>();
            dupRod.Configure("rod_twig", "dup", 0, 1f, 1f, 1f, 0.5f, 0.5f, 1f, 0, 0);
            bad.Rods = new[] { bad.Rods[0], badRod, dupRod, null };
            CollectionAssert.AreEqual(new[]
            {
                "rods[1] has invalid id 'Bad Id'.",
                "rods[2] duplicate id 'rod_twig'.",
                "rods[3] is null.",
            }, MakeFishingRegistry(bad).Validate());

            // No fishing content: nothing added (the M1 registry stays valid).
            Assert.AreEqual(0, MakeRegistry(1, MakeSpot("ok_spot")).Validate().Count);
        }

        private RodDef NewRod(string id)
        {
            RodDef d = MakeAsset<RodDef>();
            d.Configure(id, "x", 0, 1f, 1f, 1f, 0.5f, 0.5f, 1f, 0, 0);
            return d;
        }

        private BaitDef NewBait(string id)
        {
            BaitDef d = MakeAsset<BaitDef>();
            d.Configure(id, "x", 1, 1, 1, 1f, 1f, 1, false);
            return d;
        }

        private FightProfileDef NewProfile(string id)
        {
            FightProfileDef d = MakeAsset<FightProfileDef>();
            d.Configure(id, "x", 1f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0f, 0f, 0f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            return d;
        }

        private FishSpeciesDef NewSpecies(string id, FightProfileDef fight)
        {
            FishSpeciesDef d = MakeAsset<FishSpeciesDef>();
            d.Configure(id, "x", FishRarity.Common, 5, 200, 800, 2.2f, 1f, fight, BodyShape.Round, Color.red, Color.green, 1f, 0f, false, null);
            return d;
        }

        private JunkDef NewJunk(string id)
        {
            JunkDef d = MakeAsset<JunkDef>();
            d.Configure(id, "x", 2, BodyShape.Long, Color.gray);
            return d;
        }

        // Each fishing list reports its own serialized field name in the per-entry messages (invalid id, duplicate id, null).
        // Catches: a typo or a copy-paste in one list-name literal of Validate (for example "fightProfile" or "rod").
        [TestCase("rods")]
        [TestCase("baits")]
        [TestCase("fightProfiles")]
        [TestCase("species")]
        [TestCase("junk")]
        public void ContentRegistry_FishingLists_ValidateMessagesUseListName(string list)
        {
            FishingSet set = MakeFishingSet();
            int n;
            string firstId;
            switch (list)
            {
                case "rods":
                    n = set.Rods.Length; firstId = set.Rods[0].Id;
                    set.Rods = set.Rods.Concat(new[] { NewRod("Bad Id"), NewRod(firstId), null }).ToArray();
                    break;
                case "baits":
                    n = set.Baits.Length; firstId = set.Baits[0].Id;
                    set.Baits = set.Baits.Concat(new[] { NewBait("Bad Id"), NewBait(firstId), null }).ToArray();
                    break;
                case "fightProfiles":
                    n = set.Profiles.Length; firstId = set.Profiles[0].Id;
                    set.Profiles = set.Profiles.Concat(new[] { NewProfile("Bad Id"), NewProfile(firstId), null }).ToArray();
                    break;
                case "species":
                    n = set.Species.Length; firstId = set.Species[0].Id;
                    FightProfileDef fight = set.Profiles[0];
                    set.Species = set.Species.Concat(new[] { NewSpecies("Bad Id", fight), NewSpecies(firstId, fight), null }).ToArray();
                    break;
                case "junk":
                    n = set.Junk.Length; firstId = set.Junk[0].Id;
                    set.Junk = set.Junk.Concat(new[] { NewJunk("Bad Id"), NewJunk(firstId), null }).ToArray();
                    break;
                default:
                    Assert.Fail("unknown list " + list);
                    return;
            }
            CollectionAssert.AreEqual(new[]
            {
                list + "[" + n + "] has invalid id 'Bad Id'.",
                list + "[" + (n + 1) + "] duplicate id '" + firstId + "'.",
                list + "[" + (n + 2) + "] is null.",
            }, MakeFishingRegistry(set).Validate());
        }

        // A SerializedObject edit (the content builder, the inspector) must not leave a stale built index behind.
        // Catches: a missing OnValidate cache reset (the second SpotIndex would throw KeyNotFoundException).
        [Test]
        public void ContentRegistry_SerializedEdit_ClearsBuiltCache()
        {
            SpotDef pond = MakeSpot("pond_north");
            SpotDef lake = MakeSpot(ContentIds.LakeDock);
            ContentRegistry reg = MakeRegistry(1, pond);
            Assert.AreEqual((ushort)0, reg.SpotIndex("pond_north"));
            Assert.Throws<KeyNotFoundException>(() => reg.SpotIndex("lake_dock"));

            var so = new UnityEditor.SerializedObject(reg);
            UnityEditor.SerializedProperty spots = so.FindProperty("spots");
            spots.arraySize = 2;
            spots.GetArrayElementAtIndex(1).objectReferenceValue = lake;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(2, reg.Spots.Count);
            Assert.AreEqual((ushort)0, reg.SpotIndex("lake_dock"));
            Assert.AreEqual((ushort)1, reg.SpotIndex("pond_north"));
        }

        // Catches: species FightProfileIndex taken from the list position instead of the ordinal profile index, the database built
        // from list order (not index order), Tuning not converted, Fishing non-null for a spots-only registry, a stale database after
        // ConfigureFishing.
        [Test]
        public void ContentRegistry_Fishing_BuildsDatabase()
        {
            FishingSet set = MakeFishingSet();
            ContentRegistry reg = MakeFishingRegistry(set, MakeSpot(ContentIds.LakeDock));
            FishingDatabase db = reg.Fishing;
            Assert.IsNotNull(db);
            Assert.AreEqual(2, db.Rods.Count);
            Assert.AreEqual(2, db.Baits.Count);
            Assert.AreEqual(2, db.Profiles.Count);
            Assert.AreEqual(2, db.Species.Count);
            Assert.AreEqual(1, db.Junk.Count);

            for (ushort i = 0; i < db.Species.Count; i++)
            {
                Assert.AreEqual(reg.FightProfileIndex(reg.SpeciesAt(i).Fight.Id), db.Species[i].FightProfileIndex, "species " + i);
            }
            // bluegill (index 0) uses steady (index 1); carp (index 1) uses bulldog (index 0).
            Assert.AreEqual((ushort)1, db.Species[0].FightProfileIndex);
            Assert.AreEqual((ushort)0, db.Species[1].FightProfileIndex);

            Assert.AreEqual(reg.BaitIndex("bait_bare_hook"), (ushort)db.UnlimitedBaitIndex);
            Assert.AreEqual(14f, db.Rods[reg.RodIndex("rod_twig")].CastMaxDist, "rods are in index order");
            Assert.AreEqual(24f, db.Rods[reg.RodIndex("rod_carbon")].CastMaxDist);
            Assert.AreEqual(1.2f, db.Species[reg.SpeciesIndex("carp")].BiteWaitMod);
            Assert.AreEqual(0.9f, db.Species[reg.SpeciesIndex("bluegill")].BiteWaitMod);
            Assert.AreEqual(0.55f, db.Profiles[reg.FightProfileIndex("bulldog")].DiveChance);

            FishingTuning expected = set.Tuning.ToCore();
            Assert.AreEqual(expected.CastChargeTime, db.Tuning.CastChargeTime);
            Assert.AreEqual(expected.CreelMaxWeightG, db.Tuning.CreelMaxWeightG);
            Assert.AreEqual(15000, db.Tuning.CreelMaxWeightG);
            Assert.AreEqual(expected.PityHookWindowBonus, db.Tuning.PityHookWindowBonus);

            ContentRegistry spotsOnly = MakeRegistry(1, MakeSpot(ContentIds.LakeDock));
            Assert.IsNull(spotsOnly.Fishing);

            // ConfigureFishing after a build drops the old database; M1 Configure keeps the fishing fields.
            Assert.IsNotNull(reg.Fishing);
            reg.Configure(1, new[] { MakeSpot(ContentIds.LakeDock), MakeSpot("pond_north") });
            Assert.AreEqual(2, reg.Rods.Count, "Configure does not touch the fishing fields");
            Assert.IsNotNull(reg.Fishing);
            set.Junk = new JunkDef[0];
            ConfigureFishing(reg, set);
            Assert.AreEqual(0, reg.Fishing.Junk.Count);
            Assert.AreEqual(2, reg.Spots.Count, "ConfigureFishing does not touch the spots");
            Assert.AreEqual(1, reg.DataVersion);
        }

        [Test]
        public void ContentRegistry_Configure_ResetsCachedIndex()
        {
            SpotDef pond = MakeSpot("pond_north");
            ContentRegistry reg = MakeRegistry(1, pond);
            Assert.AreEqual((ushort)0, reg.SpotIndex("pond_north"));
            ulong before = reg.ComputeContentHash();

            SpotDef lake = MakeSpot(ContentIds.LakeDock);
            reg.Configure(1, new[] { pond, lake });

            Assert.AreEqual((ushort)0, reg.SpotIndex("lake_dock"));
            Assert.AreEqual((ushort)1, reg.SpotIndex("pond_north"));
            Assert.AreNotEqual(before, reg.ComputeContentHash());
        }

        // ---- T9: ecology zones, the Core ecology database, spot lookup by scene -------------------------------------------------

        private sealed class EcoSet
        {
            public FishingSet Fishing;
            public FishingZoneDef ZoneA, ZoneB;
            public WeatherTableDef Weather;
            public EcologyTuningDef Tuning;
            public SpotDef SpotA, SpotB;
        }

        private static List<SubAreaBand> TwoBands()
        {
            return new List<SubAreaBand>
            {
                new SubAreaBand { id = "near", minShoreM = 0f, maxShoreM = 5f, junkChance = 0.1f },
                new SubAreaBand { id = "far", minShoreM = 5f, maxShoreM = 0f, junkChance = 0.2f },
            };
        }

        private static SpawnRule EcoRule(FishSpeciesDef fish, params string[] subAreaIds)
        {
            return new SpawnRule { fish = fish, subAreaIds = subAreaIds, seasons = SeasonMask.All, weather = WeatherMask.All, bands = TimeBandMask.All, weight = 1f };
        }

        private static void FillZone(FishingZoneDef zone, string id, SpawnRule rule, JunkDef junk, WeatherTableDef weather, List<SubAreaBand> bands = null)
        {
            zone.Configure(id, id, bands ?? TwoBands(), new List<SpawnRule> { rule }, new List<JunkWeight> { new JunkWeight { junk = junk, weight = 1f } }, weather, null);
        }

        // zone_b (carp) and zone_a (bluegill); spot_a -> zone_b (mapOrder 1) and spot_b -> zone_a (mapOrder 2), so zone and spot index mix-ups show.
        private EcoSet MakeEcologySet()
        {
            var e = new EcoSet { Fishing = MakeFishingSet() };
            FishSpeciesDef carp = e.Fishing.Species[0], bluegill = e.Fishing.Species[1];
            e.Weather = MakeAsset<WeatherTableDef>();
            e.Weather.Configure("weather_temperate", new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, new float[] { 1, 1.5f, 1, 1, 1, 1, 1, 1, 0.3f, 1, 1, 1.3f });
            e.ZoneB = MakeAsset<FishingZoneDef>();
            FillZone(e.ZoneB, "zone_b", EcoRule(carp, "far"), e.Fishing.Junk[0], e.Weather);
            e.ZoneA = MakeAsset<FishingZoneDef>();
            FillZone(e.ZoneA, "zone_a", EcoRule(bluegill, "near", "far"), e.Fishing.Junk[0], e.Weather);
            e.Tuning = MakeAsset<EcologyTuningDef>();
            e.Tuning.Configure(60, 120, 61, 121, 45, 60, 3f);
            e.SpotA = MakeSpot("spot_a");
            e.SpotA.Configure("spot_a", "Spot A", "Spot_spot_a", 1, e.ZoneB);
            e.SpotB = MakeSpot("spot_b");
            e.SpotB.Configure("spot_b", "Spot B", "Spot_spot_b", 2, e.ZoneA);
            return e;
        }

        private ContentRegistry MakeEcologyRegistry(EcoSet e, bool zoneBFirst = true)
        {
            ContentRegistry reg = MakeFishingRegistry(e.Fishing, e.SpotA, e.SpotB);
            reg.ConfigureEcology(zoneBFirst ? new[] { e.ZoneB, e.ZoneA } : new[] { e.ZoneA, e.ZoneB }, e.Tuning);
            return reg;
        }

        // Catches: zones hashed when empty (would change every M2 hash); list-order hashing; the category missing from the hash or misnamed.
        [Test]
        public void ContentRegistry_Zones_IndexedOrdinalAndHashedWhenNonEmpty()
        {
            EcoSet e = MakeEcologySet();
            ContentRegistry reg = MakeEcologyRegistry(e);
            Assert.AreEqual("zones", ContentRegistry.CategoryZones);
            Assert.AreEqual((ushort)0, reg.ZoneIndex("zone_a"));
            Assert.AreEqual((ushort)1, reg.ZoneIndex("zone_b"));
            Assert.AreEqual("zone_b", reg.ZoneAt(1).Id);
            Assert.AreSame(e.ZoneA, reg.ZoneAt(0));
            Assert.Throws<KeyNotFoundException>(() => reg.ZoneIndex("zone_x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => reg.ZoneAt(2));
            Assert.AreEqual(2, reg.Zones.Count);
            Assert.AreSame(e.Tuning, reg.EcologyTuning);

            // A fishing-only registry hashes as in M2: no zones category.
            var spotsWithoutZone = new[] { MakeSpot("spot_a"), MakeSpot("spot_b") };
            ContentRegistry fishingOnly = MakeFishingRegistry(MakeFishingSet(), spotsWithoutZone);
            ulong m2Hash = ContentHash.Compute(new[]
            {
                new ContentCategoryIds(ContentRegistry.CategorySpots, new[] { "spot_a", "spot_b" }),
                new ContentCategoryIds(ContentRegistry.CategoryRods, new[] { "rod_carbon", "rod_twig" }),
                new ContentCategoryIds(ContentRegistry.CategoryBaits, new[] { "bait_bare_hook", "bait_worm" }),
                new ContentCategoryIds(ContentRegistry.CategoryFightProfiles, new[] { "bulldog", "steady" }),
                new ContentCategoryIds(ContentRegistry.CategorySpecies, new[] { "bluegill", "carp" }),
                new ContentCategoryIds(ContentRegistry.CategoryJunk, new[] { "old_boot" }),
            }, 1);
            Assert.AreEqual(m2Hash, fishingOnly.ComputeContentHash());

            ulong withZones = reg.ComputeContentHash();
            Assert.AreNotEqual(m2Hash, withZones);
            ulong expected = ContentHash.Compute(new[]
            {
                new ContentCategoryIds(ContentRegistry.CategorySpots, new[] { "spot_a", "spot_b" }),
                new ContentCategoryIds(ContentRegistry.CategoryRods, new[] { "rod_carbon", "rod_twig" }),
                new ContentCategoryIds(ContentRegistry.CategoryBaits, new[] { "bait_bare_hook", "bait_worm" }),
                new ContentCategoryIds(ContentRegistry.CategoryFightProfiles, new[] { "bulldog", "steady" }),
                new ContentCategoryIds(ContentRegistry.CategorySpecies, new[] { "bluegill", "carp" }),
                new ContentCategoryIds(ContentRegistry.CategoryJunk, new[] { "old_boot" }),
                new ContentCategoryIds(ContentRegistry.CategoryZones, new[] { "zone_a", "zone_b" }),
            }, 1);
            Assert.AreEqual(expected, withZones);

            EcoSet other = MakeEcologySet();
            Assert.AreEqual(withZones, MakeEcologyRegistry(other, zoneBFirst: false).ComputeContentHash(), "list order does not matter");
        }

        // Catches: recursion (stack overflow: BuildEcology must run after built = true), a stale or half-built cache, a spot/zone index mix-up.
        [Test]
        public void ContentRegistry_Ecology_BuildsDatabase()
        {
            EcoSet e = MakeEcologySet();
            ContentRegistry reg = MakeEcologyRegistry(e);
            EcologyDatabase db = reg.Ecology;
            Assert.IsNotNull(db);
            Assert.AreSame(reg.Fishing, db.Fishing);
            Assert.AreEqual(2, db.Zones.Count);
            // zone_a (index 0) holds bluegill (species index 0) with the mask of near + far; zone_b (index 1) holds carp (species index 1).
            Assert.AreEqual((ushort)0, db.Zones[0].Rules[0].SpeciesIndex);
            Assert.AreEqual((byte)0b11, db.Zones[0].Rules[0].SubAreaMask);
            Assert.AreEqual((ushort)1, db.Zones[1].Rules[0].SpeciesIndex);
            Assert.AreEqual((byte)0b10, db.Zones[1].Rules[0].SubAreaMask);
            Assert.AreEqual(2, db.Spots.Count);
            Assert.AreEqual(1, db.Spots[0].ZoneIndex, "spot_a (index 0) -> zone_b (index 1)");
            Assert.AreEqual(1, db.Spots[0].MapOrder);
            Assert.AreEqual(0, db.Spots[1].ZoneIndex, "spot_b (index 1) -> zone_a (index 0)");
            Assert.AreEqual(2, db.Spots[1].MapOrder);
            EcologyTuning expected = e.Tuning.ToCore();
            Assert.AreEqual(expected.HotspotGapMin, db.Tuning.HotspotGapMin);
            Assert.AreEqual(expected.HotspotRadiusM, db.Tuning.HotspotRadiusM);
            Assert.AreEqual(61, db.Tuning.HotspotGapMin);

            // A fishing-only registry has no ecology.
            ContentRegistry fishingOnly = MakeFishingRegistry(MakeFishingSet(), MakeSpot("spot_a"));
            Assert.IsNull(fishingOnly.Ecology);

            // ConfigureEcology after a build: the next access reflects the new zones.
            FishingZoneDef zoneC = MakeAsset<FishingZoneDef>();
            FillZone(zoneC, "zone_c", EcoRule(e.Fishing.Species[0], "near"), e.Fishing.Junk[0], e.Weather);
            reg.ConfigureEcology(new[] { e.ZoneA, e.ZoneB, zoneC }, e.Tuning);
            Assert.AreEqual(3, reg.Ecology.Zones.Count);

            // A zone that passes Validate but fails in ZoneData (a gap between bands): Ecology throws, and throws again (no half-built cache).
            var gap = new List<SubAreaBand>
            {
                new SubAreaBand { id = "near", minShoreM = 0f, maxShoreM = 5f, junkChance = 0.1f },
                new SubAreaBand { id = "far", minShoreM = 6f, maxShoreM = 0f, junkChance = 0.2f },
            };
            FillZone(e.ZoneA, "zone_a", EcoRule(e.Fishing.Species[1], "near"), e.Fishing.Junk[0], e.Weather, gap);
            reg.ConfigureEcology(new[] { e.ZoneA, e.ZoneB }, e.Tuning);
            Assert.AreEqual(0, reg.Validate().Count, "the gap is a data-quality problem, not a registry error");
            Assert.Throws<ArgumentException>(() => { var x = reg.Ecology; });
            Assert.Throws<ArgumentException>(() => { var x = reg.Ecology; }, "the second access throws again instead of returning null");
        }

        // Catches: a culture compare, a null crash.
        [Test]
        public void ContentRegistry_TryGetSpotByScene_Resolves()
        {
            EcoSet e = MakeEcologySet();
            ContentRegistry reg = MakeEcologyRegistry(e);
            Assert.IsTrue(reg.TryGetSpotByScene("Spot_spot_b", out SpotDef found));
            Assert.AreSame(e.SpotB, found);
            Assert.IsFalse(reg.TryGetSpotByScene("spot_spot_b", out SpotDef none), "ordinal: case matters");
            Assert.IsNull(none);
            Assert.IsFalse(reg.TryGetSpotByScene("Nope", out _));
            Assert.IsFalse(reg.TryGetSpotByScene(null, out _));

            ContentRegistry spotsOnly = MakeRegistry(1, MakeSpot("pond_north"));
            Assert.IsTrue(spotsOnly.TryGetSpotByScene("Spot_pond_north", out SpotDef pond));
            Assert.AreEqual("pond_north", pond.Id);
            Assert.IsFalse(spotsOnly.TryGetSpotByScene("Spot_x", out _));
        }

        // Catches: weak validation letting Build throw an index exception at app start.
        [Test]
        public void ContentRegistry_RuleWithForeignSpecies_FailsValidate()
        {
            // The foreign species is not listed in species.
            EcoSet e = MakeEcologySet();
            FishSpeciesDef stray = MakeAsset<FishSpeciesDef>();
            stray.Configure("stray", "Stray", FishRarity.Common, 5, 200, 800, 2.2f, 1f, e.Fishing.Profiles[0], BodyShape.Round, Color.red, Color.green, 1f, 0f, false, null);
            FillZone(e.ZoneA, "zone_a", EcoRule(stray, "near"), e.Fishing.Junk[0], e.Weather);
            ContentRegistry reg = MakeEcologyRegistry(e, zoneBFirst: false);
            IReadOnlyList<string> errors = reg.Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("zones[0] 'zone_a' rules[0] fish 'stray' is not listed in species.", errors[0]);
            Assert.Throws<InvalidOperationException>(() => reg.Build());

            // Other exact messages, one defect each (zone_a first in the list; spot_b is spots[1]).
            Assert.AreEqual("zones[0] 'zone_a' rules[0] has no fish.", OnlyError(z => FillZone(z.ZoneA, "zone_a", EcoRule(null, "near"), z.Fishing.Junk[0], z.Weather)));
            Assert.AreEqual("zones[0] 'zone_a' rules[0] fish 'bluegill' names unknown sub-area 'nowhere'.",
                OnlyError(z => FillZone(z.ZoneA, "zone_a", EcoRule(z.Fishing.Species[1], "near", "nowhere"), z.Fishing.Junk[0], z.Weather)));
            JunkDef strayJunk = MakeAsset<JunkDef>();
            strayJunk.Configure("tin_can", "Tin Can", 2, BodyShape.Round, Color.gray);
            Assert.AreEqual("zones[0] 'zone_a' junk[0] 'tin_can' is not listed in junk.",
                OnlyError(z => FillZone(z.ZoneA, "zone_a", EcoRule(z.Fishing.Species[1], "near"), strayJunk, z.Weather)));
            Assert.AreEqual("zones[0] 'zone_a' junk[0] has no junk item.",
                OnlyError(z => FillZone(z.ZoneA, "zone_a", EcoRule(z.Fishing.Species[1], "near"), null, z.Weather)));
            Assert.AreEqual("spots[1] 'spot_b' has no zone.", OnlyError(z => z.SpotB.Configure("spot_b", "Spot B", "Spot_spot_b", 2, null)));
            FishingZoneDef foreignZone = MakeAsset<FishingZoneDef>();
            FillZone(foreignZone, "zone_x", EcoRule(e.Fishing.Species[1], "near"), e.Fishing.Junk[0], e.Weather);
            Assert.AreEqual("spots[1] 'spot_b' zone 'zone_x' is not listed in zones.", OnlyError(z => z.SpotB.Configure("spot_b", "Spot B", "Spot_spot_b", 2, foreignZone)));
            WeatherTableDef shortWeather = MakeAsset<WeatherTableDef>();
            shortWeather.Configure("weather_short", new float[8], new float[12]);
            Assert.AreEqual("zones[0] 'zone_a' weather 'weather_short' needs 9 base and 12 season values.",
                OnlyError(z => FillZone(z.ZoneA, "zone_a", EcoRule(z.Fishing.Species[1], "near"), z.Fishing.Junk[0], shortWeather)));
            Assert.AreEqual("zones[0] 'zone_a' has no weather table.", OnlyError(z => z.ZoneA.Configure("zone_a", "zone_a", TwoBands(),
                new List<SpawnRule> { EcoRule(z.Fishing.Species[1], "near") }, new List<JunkWeight> { new JunkWeight { junk = z.Fishing.Junk[0], weight = 1f } }, null, null)));

            // A missing tuning (set through ConfigureEcology with null).
            EcoSet noTuning = MakeEcologySet();
            ContentRegistry regNoTuning = MakeEcologyRegistry(noTuning, zoneBFirst: false);
            regNoTuning.ConfigureEcology(new[] { noTuning.ZoneA, noTuning.ZoneB }, null);
            CollectionAssert.AreEqual(new[] { "ecologyTuning is missing." }, regNoTuning.Validate());
            Assert.Throws<InvalidOperationException>(() => regNoTuning.Build());
        }

        // Runs the private BuildEcology on an already-built registry and returns the exception it throws (unwrapped). Validate() runs in
        // Build() first and rejects both defects below, so BuildEcology's own guards are only reachable by mutating after the build.
        private static Exception RunBuildEcology(ContentRegistry reg)
        {
            MethodInfo build = typeof(ContentRegistry).GetMethod("BuildEcology", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(build, "ContentRegistry.BuildEcology was renamed: update this test");
            try
            {
                build.Invoke(reg, null);
            }
            catch (TargetInvocationException ex)
            {
                return ex.InnerException;
            }
            return null;
        }

        // Catches: a bare NullReferenceException from BuildEcology when a spot has no zone or the tuning is missing (#7): the error must be
        // an InvalidOperationException that names the spot or the tuning asset.
        [Test]
        public void ContentRegistry_BuildEcology_NamesTheMissingZoneOrTuning()
        {
            EcoSet noZone = MakeEcologySet();
            ContentRegistry regNoZone = MakeEcologyRegistry(noZone);
            Assert.IsNotNull(regNoZone.Ecology);   // built
            noZone.SpotB.Configure("spot_b", "Spot B", "Spot_spot_b", 2, null);
            Exception zoneError = RunBuildEcology(regNoZone);
            Assert.IsInstanceOf<InvalidOperationException>(zoneError);
            StringAssert.Contains("spot_b", zoneError.Message);
            StringAssert.Contains("zone", zoneError.Message);

            EcoSet noTuning = MakeEcologySet();
            ContentRegistry regNoTuning = MakeEcologyRegistry(noTuning);
            Assert.IsNotNull(regNoTuning.Ecology);
            typeof(ContentRegistry).GetField("ecologyTuning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(regNoTuning, null);
            Exception tuningError = RunBuildEcology(regNoTuning);
            Assert.IsInstanceOf<InvalidOperationException>(tuningError);
            StringAssert.Contains("ecologyTuning", tuningError.Message);
        }

        // Applies one mutation to a fresh set (zone_a first in the list) and returns the single validation error.
        private string OnlyError(Action<EcoSet> mutate)
        {
            EcoSet e = MakeEcologySet();
            mutate(e);
            IReadOnlyList<string> errors = MakeEcologyRegistry(e, zoneBFirst: false).Validate();
            Assert.AreEqual(1, errors.Count, string.Join(" | ", errors));
            return errors[0];
        }
    }
}
