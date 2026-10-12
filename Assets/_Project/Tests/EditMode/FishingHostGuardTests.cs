using FishGame.Core;
using FishGame.EditorTools;
using FishGame.Fishing;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // T17 review follow-up: the pure checks behind the host's finite-origin guard and the crew announcement plausibility.
    public class FishingHostGuardTests
    {
        private static CatchResult Fish(FishingDatabase db, int weightG, CatchQuality q = CatchQuality.Normal)
        {
            return new CatchResult { IsJunk = false, ItemIndex = 0, WeightG = weightG, Quality = q };
        }

        private static CatchAnnouncement Ann(AnnounceKind kind, in CatchResult c)
        {
            return new CatchAnnouncement(1, c, kind, 7UL, false);
        }

        // Catches: a NaN/Infinity origin component (x, y or z) or yaw reaching the Physics probe because only some components are checked.
        [Test]
        public void CatchValidator_IsOriginFinite_RejectsEveryNonFiniteComponent()
        {
            var ok = new CastRequestData { OriginX = 1f, OriginY = 2f, OriginZ = 3f, YawDeg = 90f };
            Assert.That(CatchValidator.IsOriginFinite(in ok), Is.True);

            for (int i = 0; i < 4; i++)
            {
                foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    var r = ok;
                    if (i == 0) r.OriginX = bad;
                    else if (i == 1) r.OriginY = bad;
                    else if (i == 2) r.OriginZ = bad;
                    else r.YawDeg = bad;
                    Assert.That(CatchValidator.IsOriginFinite(in r), Is.False, "component " + i + " = " + bad);
                }
            }
        }

        // Catches: quality above Trophy, species weight outside [min, max] (either side), junk with a weight, indices outside the database.
        [Test]
        public void FishingRollValidation_IsSaneCatch_ChecksQualityWeightAndIndex()
        {
            FishingDatabase db = FishingSeedData.BuildDatabase();
            FishSpeciesData s = db.Species[0];
            Assert.That(FishingRollValidation.IsSaneCatch(Fish(db, s.WeightMinG), db), Is.True);
            Assert.That(FishingRollValidation.IsSaneCatch(Fish(db, s.WeightMaxG, CatchQuality.Trophy), db), Is.True);
            Assert.That(FishingRollValidation.IsSaneCatch(Fish(db, s.WeightMinG - 1), db), Is.False, "below the species minimum");
            Assert.That(FishingRollValidation.IsSaneCatch(Fish(db, s.WeightMaxG + 1), db), Is.False, "above the species maximum");
            Assert.That(FishingRollValidation.IsSaneCatch(Fish(db, s.WeightMinG, (CatchQuality)3), db), Is.False, "quality 3");
            Assert.That(FishingRollValidation.IsSaneCatch(new CatchResult { ItemIndex = (ushort)db.Species.Count, WeightG = s.WeightMinG }, db), Is.False);

            Assert.That(FishingRollValidation.IsSaneCatch(new CatchResult { IsJunk = true, ItemIndex = 0, WeightG = 0 }, db), Is.True);
            Assert.That(FishingRollValidation.IsSaneCatch(new CatchResult { IsJunk = true, ItemIndex = 0, WeightG = 5 }, db), Is.False, "junk has no weight");
            Assert.That(FishingRollValidation.IsSaneCatch(new CatchResult { IsJunk = true, ItemIndex = (ushort)db.Junk.Count }, db), Is.False);
            Assert.That(FishingRollValidation.IsSaneCatch(Fish(db, s.WeightMinG), null), Is.False, "no database");
        }

        // Catches: IsPlausible checking only the index (as before): a crew announcement with quality 3 or an impossible weight must be dropped.
        [Test]
        public void FishingRodNetwork_IsPlausible_AppliesTheSameCatchChecksAsTheRoll()
        {
            FishingDatabase db = FishingSeedData.BuildDatabase();
            FishSpeciesData s = db.Species[0];
            CatchResult good = Fish(db, s.WeightMinG);
            CatchResult heavy = Fish(db, s.WeightMaxG + 1);
            CatchResult odd = Fish(db, s.WeightMinG, (CatchQuality)3);

            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.Landed, in good), db), Is.True);
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.Lost, in good), db), Is.True);
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.ReleasedCreelFull, in good), db), Is.True);
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.Landed, in heavy), db), Is.False, "weight out of the species range");
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.Landed, in odd), db), Is.False, "quality 3");
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.Landed, in odd), null), Is.True, "no database: only the kind is checked");

            CatchResult empty = default;
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.PityCast, in empty), db), Is.True, "PityCast carries a default catch");
            Assert.That(FishingRodNetwork.IsPlausible(Ann(AnnounceKind.None, in good), db), Is.False, "kind None");
            Assert.That(FishingRodNetwork.IsPlausible(Ann((AnnounceKind)5, in good), db), Is.False, "kind 5");
        }
    }
}
