using System;
using System.Collections.Generic;
using System.Linq;
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
            steady.Configure("steady", "Steady", 0.3f, 0.6f, 0.3f, 0.08f, 1f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0.6f, 0f, 0f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            FightProfileDef bulldog = MakeAsset<FightProfileDef>();
            bulldog.Configure("bulldog", "Bulldog", 0.6f, 0.6f, 0.5f, 0.06f, 1f, 3f, 2f, 4f, 0f, 0.55f, 0f, 0.06f, 2.5f, 0.5f, 0f, 1f, 1f, 1f, 4f);   // R72: diveChance 0.55, calmDrift 0.06
            set.Profiles = new[] { steady, bulldog };

            FishSpeciesDef carp = MakeAsset<FishSpeciesDef>();
            carp.Configure("carp", "Carp", FishRarity.Common, 12, 1000, 4000, 2.2f, 1f, bulldog, BodyShape.Round, Color.red, Color.green,
                1.2f, 0f, false, 1, 1.5f, null);
            FishSpeciesDef bluegill = MakeAsset<FishSpeciesDef>();
            bluegill.Configure("bluegill", "Bluegill", FishRarity.Common, 5, 200, 800, 2.2f, 1f, steady, BodyShape.Round, Color.blue, Color.white,
                0.9f, 0f, false, 1, 0.8f, null);
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
                1f, 0f, false, 3, 1.2f, null);
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
            foreign.Configure("darter", "Darter", 0.45f, 0.7f, 0.4f, 0.07f, 0.3f, 0.8f, 0.4f, 0.9f, 1f, 0f, 0f, 0f, 0f, 0.3f, 0f, 1f, 1f, 1f, 4f);
            FishSpeciesDef perch = MakeAsset<FishSpeciesDef>();
            perch.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, foreign, BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, 3, 1.2f, null);
            set.Species = new[] { perch };
            ContentRegistry reg = MakeFishingRegistry(set);
            IReadOnlyList<string> errors = reg.Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("species[0] 'perch' fight 'darter' is not listed in fightProfiles.", errors[0]);
            Assert.Throws<InvalidOperationException>(() => reg.Build());

            perch.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, null, BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, 3, 1.2f, null);
            errors = reg.Validate();
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("species[0] 'perch' has no fight profile.", errors[0]);
            Assert.Throws<InvalidOperationException>(() => reg.Build());

            // A different Def object that merely shares the id of a listed profile is still foreign (reference containment).
            FightProfileDef lookalike = MakeAsset<FightProfileDef>();
            lookalike.Configure("bulldog", "Bulldog copy", 0.6f, 0.6f, 0.5f, 0.06f, 1f, 3f, 2f, 4f, 0f, 0.35f, 0f, 0.12f, 2.5f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            perch.Configure("perch", "Perch", FishRarity.Common, 8, 300, 1200, 2.2f, 1f, lookalike, BodyShape.Long, Color.red, Color.green,
                1f, 0f, false, 3, 1.2f, null);
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
            d.Configure(id, "x", 0.3f, 0.6f, 0.3f, 0.08f, 1f, 2.5f, 1.5f, 2.5f, 0f, 0f, 0f, 0f, 0f, 0.5f, 0f, 1f, 1f, 1f, 4f);
            return d;
        }

        private FishSpeciesDef NewSpecies(string id, FightProfileDef fight)
        {
            FishSpeciesDef d = MakeAsset<FishSpeciesDef>();
            d.Configure(id, "x", FishRarity.Common, 5, 200, 800, 2.2f, 1f, fight, BodyShape.Round, Color.red, Color.green, 1f, 0f, false, 1, 1f, null);
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
    }
}
