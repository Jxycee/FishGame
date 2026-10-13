// Tests/EditMode/Ecology/EcologyTypesTests.cs - T5: event/skill/bait mods, zone and database validation, catch ledger, masks, run state.
using System;
using FishGame.Core;
using NUnit.Framework;
using static FishGame.Tests.EditMode.EcologyCoreTestData;

namespace FishGame.Tests.EditMode
{
    public class EcologyTypesTests
    {
        // ---- EventMods / SkillMods / BaitMods ----

        static EventMods SetField(EventMods m, int field, float v)
        {
            switch (field)
            {
                case 0: m.JunkMult = v; break;
                case 1: m.RareChanceMult = v; break;
                case 2: m.BiteTimeMult = v; break;
                case 3: m.NocturnalMult = v; break;
                case 4: m.PredatorMult = v; break;
                case 5: m.BottomMult = v; break;
                case 6: m.SchoolingMult = v; break;
                default: m.SurfaceMult = v; break;
            }
            return m;
        }

        [Test]
        public void EventMods_DefaultIsInvalidNeutralIsValid()
        {
            Assert.IsFalse(default(EventMods).IsValid);
            Assert.IsTrue(EventMods.Neutral.IsValid);
            float[] bad = { 0f, float.NaN, float.PositiveInfinity, -1f };
            for (int field = 0; field < 8; field++)
            {
                foreach (float v in bad)
                    Assert.IsFalse(SetField(EventMods.Neutral, field, v).IsValid, "field " + field + " = " + v);
            }
        }

        [Test]
        public void EventMods_WeightFor_MultipliesSetTags()
        {
            EventMods m = EventMods.Neutral;
            m.PredatorMult = 2f; m.NocturnalMult = 3f;
            Assert.AreEqual(6f, m.WeightFor(FishTags.Predator | FishTags.Nocturnal), 1e-6f);
            Assert.AreEqual(1f, m.WeightFor(FishTags.None), 1e-6f);
            Assert.AreEqual(1f, m.WeightFor(FishTags.Bottom), 1e-6f);
            Assert.AreEqual(2f, m.WeightFor(FishTags.Predator), 1e-6f);

            EventMods n = EventMods.Neutral;
            n.SchoolingMult = 0.5f;
            Assert.AreEqual(0.5f, n.WeightFor(FishTags.Schooling | FishTags.Surface), 1e-6f);
        }

        [Test]
        public void SkillMods_RarityMult_OnlyUncommonAndRare()
        {
            SkillMods s = SkillMods.Neutral;
            s.UncommonRareMult = 1.15f;
            Assert.AreEqual(1f, s.RarityMult(FishRarity.Common), 1e-6f);
            Assert.AreEqual(1.15f, s.RarityMult(FishRarity.Uncommon), 1e-6f);
            Assert.AreEqual(1.15f, s.RarityMult(FishRarity.Rare), 1e-6f);
            Assert.AreEqual(1f, s.RarityMult(FishRarity.Legendary), 1e-6f);
            Assert.IsFalse(default(SkillMods).IsValid);
            Assert.IsTrue(SkillMods.Neutral.IsValid);
            Assert.IsFalse(new SkillMods { UncommonRareMult = float.NaN }.IsValid);
        }

        [Test]
        public void BaitMods_Neutral_HasUnitMultipliers()
        {
            BaitMods n = BaitMods.Neutral(1);
            Assert.AreEqual(1, n.Rating);
            Assert.AreEqual(FishTags.None, n.AffinityTags);
            Assert.AreEqual(1f, n.AffinityMult);
            Assert.AreEqual(1f, n.JunkMult);
            Assert.AreEqual(1f, n.SpecialMult);

            BaitStats[] baits = Baits();
            BaitMods worm2 = baits[FishingTestData.BaitWorm].Mods(2);
            Assert.AreEqual(6, worm2.Rating);
            Assert.AreEqual(FishTags.Bottom, worm2.AffinityTags);
            Assert.AreEqual(1.3f, worm2.AffinityMult, 1e-6f);
            Assert.AreEqual(1.0f, worm2.JunkMult, 1e-6f);
            Assert.AreEqual(1.0f, worm2.SpecialMult, 1e-6f);
            Assert.AreEqual(3, baits[FishingTestData.BaitWorm].Mods(0).Rating);
            Assert.AreEqual(1.5f, baits[FishingTestData.BaitRawMeat].Mods(1).SpecialMult, 1e-6f);
            Assert.AreEqual(1.5f, baits[FishingTestData.BaitBareHook].Mods(1).JunkMult, 1e-6f);
        }

        // ---- ZoneData ----

        [Test]
        public void ZoneData_SubAreaIndexFor_ContiguousBandsAndEdges()
        {
            ZoneData lake = Lake();
            Assert.AreEqual(0, lake.SubAreaIndexFor(-1f));
            Assert.AreEqual(0, lake.SubAreaIndexFor(0f));
            Assert.AreEqual(0, lake.SubAreaIndexFor(4.999f));
            Assert.AreEqual(1, lake.SubAreaIndexFor(5f));
            Assert.AreEqual(1, lake.SubAreaIndexFor(9.99f));
            Assert.AreEqual(2, lake.SubAreaIndexFor(10f));
            Assert.AreEqual(2, lake.SubAreaIndexFor(1e6f));
            Assert.AreEqual(2, lake.SubAreaIndexFor(float.PositiveInfinity));
            Assert.AreEqual(2, lake.SubAreaIndexFor(float.NaN));
        }

        static SubAreaBandData B(float min, float max) => new SubAreaBandData { MinShoreM = min, MaxShoreM = max, JunkChance = 0.1f };

        static ZoneData MakeZone(SubAreaBandData[] bands, JunkEntry[] junk = null)
        {
            WeatherTableData w = Weather();
            return new ZoneData(bands, LakeRules(), junk ?? LakeJunk(), in w, null);
        }

        [Test]
        public void ZoneData_NonContiguousBands_Throws()
        {
            float inf = float.PositiveInfinity;
            Assert.DoesNotThrow(() => Lake(), "control: the fixture lake constructs");

            Assert.Catch<ArgumentException>(() => MakeZone(new[] { B(0f, 5f), B(6f, inf) }), "gap");
            Assert.Catch<ArgumentException>(() => MakeZone(new[] { B(1f, 5f), B(5f, inf) }), "first min 1");
            Assert.Catch<ArgumentException>(() => MakeZone(new[] { B(0f, 5f), B(5f, 20f) }), "last max 20");
            Assert.Catch<ArgumentException>(() => MakeZone(new[] { B(0f, 5f), B(5f, 5f), B(5f, inf) }), "band max <= min");
            Assert.Catch<ArgumentException>(() => MakeZone(new SubAreaBandData[0]), "no bands");
            var nine = new SubAreaBandData[9];
            for (int i = 0; i < 9; i++) nine[i] = B(i, i == 8 ? inf : i + 1);
            Assert.Catch<ArgumentException>(() => MakeZone(nine), "9 bands");
            var eight = new SubAreaBandData[8];
            for (int i = 0; i < 8; i++) eight[i] = B(i, i == 7 ? inf : i + 1);
            Assert.DoesNotThrow(() => MakeZone(eight), "control: 8 bands is the maximum");
            Assert.Catch<ArgumentException>(() => MakeZone(LakeBands(), new JunkEntry[0]), "empty junk");
        }

        [Test]
        public void ZoneData_NullArguments_Throw()
        {
            WeatherTableData w = Weather();
            Assert.Throws<ArgumentNullException>(() => new ZoneData(null, LakeRules(), LakeJunk(), in w, null));
            Assert.Throws<ArgumentNullException>(() => new ZoneData(LakeBands(), null, LakeJunk(), in w, null));
            Assert.Throws<ArgumentNullException>(() => new ZoneData(LakeBands(), LakeRules(), null, in w, null));
        }

        [Test]
        public void ZoneData_CopiesInputArrays()
        {
            SpawnRuleData[] rules = LakeRules();
            ZoneData z = Lake(rules);
            rules[0].Weight = 999f;
            Assert.AreEqual(10f, z.Rules[0].Weight);
        }

        // ---- EcologyDatabase ----

        [Test]
        public void EcologyDatabase_OutOfRangeIndices_Throw()
        {
            Assert.DoesNotThrow(() => Database(), "control: the fixture database constructs");

            SpawnRuleData[] badRules = LakeRules();
            badRules[0].SpeciesIndex = 7;                                       // 7 species: index 7 is out
            Assert.Catch<ArgumentException>(() => Database(lake: Lake(badRules)), "rule species 7");

            JunkEntry[] badJunk = LakeJunk();
            badJunk[0].JunkIndex = 4;                                           // 4 junk items: index 4 is out
            WeatherTableData w = Weather();
            ZoneData junkZone = new ZoneData(LakeBands(), LakeRules(), badJunk, in w, null);
            Assert.Catch<ArgumentException>(() => Database(lake: junkZone), "junk index 4");

            EcologyTuning t = Tuning();
            Assert.Catch<ArgumentException>(() => new EcologyDatabase(EcologyCoreTestData.Fishing(), new[] { Lake(), Harbour() },
                new[] { new SpotEcologyData { ZoneIndex = 2, MapOrder = 0 } }, in t), "spot zone 2");
            Assert.Catch<ArgumentException>(() => new EcologyDatabase(null, new[] { Lake() },
                new[] { new SpotEcologyData { ZoneIndex = 0 } }, in t), "null fishing");
            Assert.Catch<ArgumentException>(() => new EcologyDatabase(EcologyCoreTestData.Fishing(), new ZoneData[] { Lake(), null },
                new[] { new SpotEcologyData { ZoneIndex = 0 } }, in t), "null zone element");
            Assert.Catch<ArgumentException>(() => new EcologyDatabase(EcologyCoreTestData.Fishing(), null,
                new[] { new SpotEcologyData { ZoneIndex = 0 } }, in t), "null zones");
            Assert.Catch<ArgumentException>(() => new EcologyDatabase(EcologyCoreTestData.Fishing(), new[] { Lake() }, null, in t), "null spots");
        }

        // ---- CatchLedger ----

        [Test]
        public void CatchLedger_ReserveCommitRelease_LimitsCrewWide()
        {
            var l = new CatchLedger();
            Assert.IsFalse(l.AtLimit(1, 1));
            l.Reserve(1);
            Assert.IsTrue(l.AtLimit(1, 1));
            Assert.AreEqual(1, l.Reserved(1));
            l.Release(1);
            Assert.IsFalse(l.AtLimit(1, 1));
            Assert.AreEqual(0, l.Reserved(1));

            l.Reserve(1); l.Commit(1);
            Assert.AreEqual(1, l.Caught(1));
            Assert.AreEqual(0, l.Reserved(1));
            Assert.IsTrue(l.AtLimit(1, 1));

            Assert.IsFalse(l.AtLimit(1, -1), "limit -1 is unlimited");
            Assert.IsFalse(l.AtLimit(1, int.MinValue));

            l.Reserve(1);                                                        // Caught 1 + Reserved 1
            Assert.IsTrue(l.AtLimit(1, 2));
            Assert.IsFalse(l.AtLimit(1, 3));
            Assert.IsFalse(l.AtLimit(3, 1), "other species unaffected");
            Assert.AreEqual(0, l.Caught(3));

            l.Clear();
            Assert.AreEqual(0, l.Caught(1));
            Assert.AreEqual(0, l.Reserved(1));
            Assert.IsFalse(l.AtLimit(1, 1));
        }

        [Test]
        public void CatchLedger_CommitWithoutReserve_Throws()
        {
            var l = new CatchLedger();
            Assert.Throws<InvalidOperationException>(() => l.Commit(3));
            Assert.Throws<InvalidOperationException>(() => l.Release(3));
            Assert.AreEqual(0, l.Caught(3));
            Assert.AreEqual(0, l.Reserved(3));

            l.Reserve(3); l.Commit(3);
            Assert.Throws<InvalidOperationException>(() => l.Commit(3));
            Assert.AreEqual(1, l.Caught(3));
            Assert.AreEqual(0, l.Reserved(3));
        }

        // ---- extras ----

        [Test]
        public void EcologyMasks_OfAndWeatherColumn()
        {
            Assert.AreEqual(SeasonMask.Spring, EcologyMasks.Of(Season.Spring));
            Assert.AreEqual(SeasonMask.Summer, EcologyMasks.Of(Season.Summer));
            Assert.AreEqual(SeasonMask.Autumn, EcologyMasks.Of(Season.Autumn));
            Assert.AreEqual(SeasonMask.Winter, EcologyMasks.Of(Season.Winter));
            Assert.AreEqual(1, (int)SeasonMask.Spring);
            Assert.AreEqual(8, (int)SeasonMask.Winter);
            Assert.Throws<ArgumentOutOfRangeException>(() => EcologyMasks.Of((Season)4));

            Assert.AreEqual(0, EcologyMasks.WeatherColumn(WeatherMask.Clear));
            Assert.AreEqual(1, EcologyMasks.WeatherColumn(WeatherMask.Rain));
            Assert.AreEqual(2, EcologyMasks.WeatherColumn(WeatherMask.Fog));
            Assert.Throws<ArgumentException>(() => EcologyMasks.WeatherColumn(WeatherMask.Rain | WeatherMask.Storm));
            Assert.Throws<ArgumentException>(() => EcologyMasks.WeatherColumn(WeatherMask.None));
            Assert.Throws<ArgumentException>(() => EcologyMasks.WeatherColumn(WeatherMask.Storm));
        }

        [Test]
        public void EcologyRunState_New_DayOneCounterZeroSeasonRolled()
        {
            var run = new EcologyRunState(42UL);
            Assert.AreEqual(1, run.Day);
            Assert.AreEqual(0u, run.HostCastCounter);
            Assert.IsNotNull(run.Catches);
            Assert.AreEqual(42UL, run.RunSeed);
            Pcg32 r = Rng.Stream(42UL, "season", 0);
            Assert.AreEqual((Season)r.Range(0, 4), run.Season);
        }
    }
}
