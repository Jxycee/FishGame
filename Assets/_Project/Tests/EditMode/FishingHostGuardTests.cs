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

        // Catches: the host pre-probe origin check using a wrong radius (not the ledger's 3 m, not inclusive at exactly 3 m), ignoring one axis
        // (X or Z), measuring from the wrong centre, or letting a NaN or infinite component through (every comparison with NaN is false).
        [Test]
        public void CatchValidator_OriginWithinSlack_ThreeMetreHorizontalRuleAndNonFinite()
        {
            Assert.That(CatchValidator.OriginWithinSlack(0f, 0f, 0f, 0f), Is.True);
            Assert.That(CatchValidator.OriginWithinSlack(3f, 0f, 0f, 0f), Is.True, "exactly 3 m on X is inside");
            Assert.That(CatchValidator.OriginWithinSlack(0f, -3f, 0f, 0f), Is.True, "exactly 3 m on Z is inside");
            Assert.That(CatchValidator.OriginWithinSlack(3.01f, 0f, 0f, 0f), Is.False, "just past 3 m on X");
            Assert.That(CatchValidator.OriginWithinSlack(0f, 3.01f, 0f, 0f), Is.False, "just past 3 m on Z");
            Assert.That(CatchValidator.OriginWithinSlack(2.2f, 2.2f, 0f, 0f), Is.False, "diagonal 3.11 m");
            Assert.That(CatchValidator.OriginWithinSlack(12f, -7f, 10f, -8f), Is.True, "relative to the player, not the world origin");
            Assert.That(CatchValidator.OriginWithinSlack(14.1f, -8f, 10f, -8f), Is.False, "4.1 m from a far-away player");
            Assert.That(CatchValidator.OriginWithinSlack(float.NaN, 0f, 0f, 0f), Is.False);
            Assert.That(CatchValidator.OriginWithinSlack(0f, float.NaN, 0f, 0f), Is.False);
            Assert.That(CatchValidator.OriginWithinSlack(float.PositiveInfinity, 0f, 0f, 0f), Is.False);
            Assert.That(CatchValidator.OriginWithinSlack(0f, 0f, float.NaN, 0f), Is.False, "a NaN host view also fails closed");
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
