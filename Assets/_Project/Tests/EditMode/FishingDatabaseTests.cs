using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class FishingDatabaseTests
    {
        [Test]
        public void FishingDatabase_BadProfileIndex_Throws()
        {
            FishFightProfile[] profiles = FishingTestData.BaseProfiles();   // 5 profiles
            FishSpeciesData[] species = FishingTestData.Species();
            species[0].FightProfileIndex = 5;
            FishingTuning t = FishingTestData.Tuning();
            Assert.Throws<ArgumentException>(() =>
                new FishingDatabase(FishingTestData.Rods(), FishingTestData.Baits(), profiles, species, FishingTestData.Junk(), in t));
        }

        [Test]
        public void FishingDatabase_NoUnlimitedBait_Throws()
        {
            BaitStats[] baits = FishingTestData.Baits();
            for (int i = 0; i < baits.Length; i++) baits[i].Unlimited = false;
            FishingTuning t = FishingTestData.Tuning();
            Assert.Throws<ArgumentException>(() =>
                new FishingDatabase(FishingTestData.Rods(), baits, FishingTestData.BaseProfiles(), FishingTestData.Species(), FishingTestData.Junk(), in t));
        }

        [Test]
        public void FishingDatabase_UnlimitedBaitIndex_IsBareHook()
        {
            RodStats[] rods = FishingTestData.Rods();
            FishingTuning t = FishingTestData.Tuning();
            var db = new FishingDatabase(rods, FishingTestData.Baits(), FishingTestData.BaseProfiles(), FishingTestData.Species(), FishingTestData.Junk(), in t);
            Assert.That(db.UnlimitedBaitIndex, Is.EqualTo(FishingTestData.BaitBareHook));
            Assert.That(FishingTestData.Database().UnlimitedBaitIndex, Is.EqualTo(0));

            float before = db.Rods[FishingTestData.RodHandmade].CastMaxDist;
            rods[FishingTestData.RodHandmade].CastMaxDist = 999f;           // later caller mutation has no effect
            Assert.That(db.Rods[FishingTestData.RodHandmade].CastMaxDist, Is.EqualTo(before));
            Assert.That(db.Rods[FishingTestData.RodHandmade].CastMaxDist, Is.EqualTo(18f));
        }

        [Test]
        public void BaitStats_EffRating_ClampsAndTreatsZeroAsOne()
        {
            BaitStats grub = FishingTestData.Baits()[FishingTestData.BaitGrub];   // rating 4, max 12
            Assert.That(grub.EffRating(0), Is.EqualTo(4));      // zero loaded counts as one
            Assert.That(grub.EffRating(-3), Is.EqualTo(4));
            Assert.That(grub.EffRating(1), Is.EqualTo(4));
            Assert.That(grub.EffRating(2), Is.EqualTo(8));
            Assert.That(grub.EffRating(3), Is.EqualTo(12));
            Assert.That(grub.EffRating(4), Is.EqualTo(12));     // clamped to MaxRating
        }

        [Test]
        public void RodStats_MaxLineOut_AddsSpoolExtra()
        {
            RodStats handmade = FishingTestData.Rods()[FishingTestData.RodHandmade];   // cast 18
            Assert.That(handmade.MaxLineOut(12f), Is.EqualTo(30f).Within(1e-5f));
        }
    }
}
