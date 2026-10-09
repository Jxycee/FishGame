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
