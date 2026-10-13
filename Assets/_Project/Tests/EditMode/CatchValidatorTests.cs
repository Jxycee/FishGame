using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Pure host checks (spec 9.9, R64). Extra to the contract list: exact boundaries and order of the validator rules, so each
    // rule has its own failing test. Numbers are chosen to be exact in binary (0.5, 0.75, 2, 10, 12, 1.75) wherever a boundary is probed.
    public class CatchValidatorTests
    {
        static CastRequestData GoodReq() =>
            new CastRequestData { CastId = 5, OriginX = 1f, OriginY = 0f, OriginZ = 1f, YawDeg = 0f, ChargeNorm = 0.5f, BaitIndex = 1, Assist = false };

        static HostCastView GoodView() =>
            new HostCastView { PlayerX = 1f, PlayerY = 0f, PlayerZ = 1f, CastingAllowed = true, GridWater = true, Context = EcologyTestData.Context(),
                               Probe = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 3f } };

        // Catches (M3-D16): the grid check placed before the probe (a too-shallow or dry landing must keep its probe reason), before NoBait,
        // or missing (a valid probe on a not-water grid cell must be NotWater).
        [Test]
        public void CatchValidator_ValidateCast_GridCheckAfterProbe()
        {
            CastRequestData req = GoodReq();
            HostCastView view = GoodView();
            view.GridWater = false;

            HostCastView shallow = view;
            shallow.Probe.Depth = 0.4f;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in shallow, true, 0.5f), Is.EqualTo(CastRejectReason.TooShallow), "grid false + too shallow");
            HostCastView dry = view;
            dry.Probe.FirstHitIsWater = false;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in dry, true, 0.5f), Is.EqualTo(CastRejectReason.NotWater), "grid false + probe not water");
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.5f), Is.EqualTo(CastRejectReason.NotWater), "grid false + valid probe");
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, false, 0.5f), Is.EqualTo(CastRejectReason.NoBait), "grid false + no bait");
            view.GridWater = true;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.5f), Is.EqualTo(CastRejectReason.None), "grid true + valid");
        }

        // Catches: the casting-closed check (day over, M3-D16) moved after any other rule.
        [Test]
        public void CatchValidator_ValidateCast_CastingNotAllowedFirst()
        {
            CastRequestData req = GoodReq();
            req.CastId = 3; req.ChargeNorm = 2f; req.OriginX = 50f;    // stale id, bad charge, far origin
            HostCastView view = GoodView();
            view.CastingAllowed = false;
            view.GridWater = false;
            view.Probe.FirstHitIsWater = false;
            view.Probe.HasBed = false;
            Assert.That(CatchValidator.ValidateCast(in req, 3, true, in view, false, 0.5f), Is.EqualTo(CastRejectReason.NotAllowedNow));
        }

        [Test]
        public void CatchValidator_ValidateCast_RuleOrderIsFixed()
        {
            // Everything wrong at once, then fix one rule at a time in order.
            CastRequestData req = GoodReq();
            req.CastId = 3; req.ChargeNorm = 2f; req.OriginX = 50f;
            HostCastView view = GoodView();
            view.CastingAllowed = false;
            view.Probe.FirstHitIsWater = false;
            const float minDepth = 0.8f;

            Assert.That(CatchValidator.ValidateCast(in req, 3, true, in view, false, minDepth), Is.EqualTo(CastRejectReason.NotAllowedNow));
            view.CastingAllowed = true;
            Assert.That(CatchValidator.ValidateCast(in req, 3, true, in view, false, minDepth), Is.EqualTo(CastRejectReason.ActiveCast));
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, false, minDepth), Is.EqualTo(CastRejectReason.StaleCastId));
            req.CastId = 4;
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, false, minDepth), Is.EqualTo(CastRejectReason.BadCharge));
            req.ChargeNorm = 0.5f;
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, false, minDepth), Is.EqualTo(CastRejectReason.OriginTooFar));
            req.OriginX = 1f;
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, false, minDepth), Is.EqualTo(CastRejectReason.NoBait));
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, true, minDepth), Is.EqualTo(CastRejectReason.NotWater));
            view.Probe.FirstHitIsWater = true;
            view.Probe.Depth = 0.79f;
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, true, minDepth), Is.EqualTo(CastRejectReason.TooShallow));
            view.Probe.Depth = 0.8f;
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, true, minDepth), Is.EqualTo(CastRejectReason.None));
            view.GridWater = false;   // M3-D16: the grid cell check comes last
            Assert.That(CatchValidator.ValidateCast(in req, 3, false, in view, true, minDepth), Is.EqualTo(CastRejectReason.NotWater));
        }

        [Test]
        public void CatchValidator_ValidateCast_StaleBoundaryAndOriginBoundary()
        {
            CastRequestData req = GoodReq();
            HostCastView view = GoodView();
            req.CastId = 5;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "id = last + 1");
            Assert.That(CatchValidator.ValidateCast(in req, 5, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.StaleCastId), "id = last");
            Assert.That(CatchValidator.ValidateCast(in req, 6, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.StaleCastId), "id < last");
            req.OriginX = 4f; req.OriginZ = 1f;   // exactly 3.0 m east of the player
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "3.0 m allowed");
            req.OriginX = 4.01f;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.OriginTooFar), "3.01 m");
            req.OriginX = 1f; req.OriginZ = 1f; req.ChargeNorm = 1f;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "charge 1");
        }

        // Catches (final review R67 follow-up): the host accepting a charge below FishingConstants.MinChargeNorm (the owner's machine never
        // sends one, so a client that does is lying), or rejecting the minimum itself. Same reason as the range check, and the same slot in the order.
        [Test]
        public void CatchValidator_ValidateCast_ChargeBelowMinChargeNorm_IsBadCharge()
        {
            HostCastView view = GoodView();
            CastRequestData req = GoodReq();
            foreach (float bad in new[] { 0f, 0.05f, 0.0999f, -0.01f, 1.01f, float.NaN })
            {
                req.ChargeNorm = bad;
                Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.BadCharge), "charge " + bad);
            }
            foreach (float good in new[] { FishingConstants.MinChargeNorm, 0.1f, 0.11f, 1f })
            {
                req.ChargeNorm = good;
                Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "charge " + good);
            }
            Assert.That(CatchValidator.IsChargeAccepted(0.05f), Is.False);
            Assert.That(CatchValidator.IsChargeAccepted(0.1f), Is.True);
        }

        // Catches: ValidateCast letting a NaN / infinite yaw through (CastMath.Landing would then place the bobber at NaN), or
        // rejecting it with a different reason than the NaN-origin rule, or checking it after the bait and water rules.
        [Test]
        public void CatchValidator_ValidateCast_NonFiniteYaw_IsOriginTooFar()
        {
            HostCastView view = GoodView();
            foreach (float yaw in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                CastRequestData req = GoodReq();
                req.YawDeg = yaw;
                Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.OriginTooFar), "yaw " + yaw);
                Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, false, 0.8f), Is.EqualTo(CastRejectReason.OriginTooFar),
                            "the yaw rule sits before NoBait, yaw " + yaw);
            }
            CastRequestData stale = GoodReq();
            stale.YawDeg = float.NaN;
            Assert.That(CatchValidator.ValidateCast(in stale, 5, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.StaleCastId),
                        "StaleCastId still outranks the yaw rule");
            CastRequestData finite = GoodReq();
            finite.YawDeg = -720f;
            Assert.That(CatchValidator.ValidateCast(in finite, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "any finite yaw is fine");
            finite.YawDeg = 1e30f;
            Assert.That(CatchValidator.ValidateCast(in finite, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "large but finite");
        }

        [Test]
        public void CatchValidator_HookTooEarly_BoundaryAtBiteMinusTolerance()
        {
            // TAccepted 0, TFlight 0.5, BiteWait 2, tolerance 0.75 -> the hook is early before 1.75 s.
            var cast = new ActiveCast { TAccepted = 0.0, TFlight = 0.5f, Roll = new FishingRoll { BiteWait = 2f } };
            Assert.That(CatchValidator.HookTooEarly(1.74, in cast, 0.75f), Is.True);
            Assert.That(CatchValidator.HookTooEarly(1.75, in cast, 0.75f), Is.False, "exactly on the boundary is fine");
            Assert.That(CatchValidator.HookTooEarly(2.5, in cast, 0.75f), Is.False);
            Assert.That(CatchValidator.HookTooEarly(0.0, in cast, 0.75f), Is.True);
            var later = new ActiveCast { TAccepted = 10.0, TFlight = 0.5f, Roll = new FishingRoll { BiteWait = 2f } };
            Assert.That(CatchValidator.HookTooEarly(11.74, in later, 0.75f), Is.True, "relative to TAccepted");
            Assert.That(CatchValidator.HookTooEarly(11.76, in later, 0.75f), Is.False);
            Assert.That(CatchValidator.HookTooEarly(11.2, in later, 0.0f), Is.True, "tolerance 0: before the bite is early");
        }

        [Test]
        public void CatchValidator_LandedFloor_ScalesWithLineAndSpeed()
        {
            Assert.That(CatchValidator.LandedFloorSeconds(8.5f, 2f, 1f), Is.EqualTo(4.7f).Within(1e-4f));
            Assert.That(CatchValidator.LandedFloorSeconds(8.5f, 2f, 3f), Is.EqualTo(0.8f * 6.5f / 3f - 0.5f).Within(1e-4f), "junk drag speed 3");
            Assert.That(CatchValidator.LandedFloorSeconds(8.5f, 2f, 1.3f), Is.EqualTo(0.8f * 6.5f / 1.3f - 0.5f).Within(1e-4f));
            Assert.That(CatchValidator.LandedFloorSeconds(2.2f, 2f, 1f), Is.EqualTo(0f), "short lines have no floor (clamped at 0)");
            Assert.That(CatchValidator.LandedFloorSeconds(2f, 2f, 1f), Is.EqualTo(0f));
            Assert.That(CatchValidator.LandedFloorSeconds(1f, 2f, 1f), Is.EqualTo(0f), "line inside the landing distance");

            var cast = new ActiveCast { THooked = 100.0, LineLength0 = 8.5f };
            Assert.That(CatchValidator.LandedTooFast(104.6, in cast, 2f, 1f), Is.True);
            Assert.That(CatchValidator.LandedTooFast(104.8, in cast, 2f, 1f), Is.False);
            Assert.That(CatchValidator.LandedTooFast(101.3, in cast, 2f, 3f), Is.False, "junk speed");
            Assert.That(CatchValidator.LandedTooFast(101.1, in cast, 2f, 3f), Is.True);
        }

        [Test]
        public void CatchValidator_HostLineLength0_LeashesToHostDistance()
        {
            // The slack comes from the constant (it was a literal 1.5 until T19-R1 derived it from the rig's tip reach).
            const float slack = FishingConstants.HookedLineSlack;
            Assert.That(CatchValidator.HostLineLength0(10f, 2f), Is.EqualTo(10f - slack).Within(1e-5f));
            Assert.That(CatchValidator.HostLineLength0(3f, 2f), Is.EqualTo(2f), "never below the landing distance");
            Assert.That(CatchValidator.HostLineLength0(2f + slack - 0.1f, 2f), Is.EqualTo(2f));
            Assert.That(CatchValidator.HostLineLength0(2f + slack + 0.1f, 2f), Is.EqualTo(2.1f).Within(1e-5f));
            Assert.That(CatchValidator.HostLineLength0(float.NaN, 2f), Is.EqualTo(2f), "a NaN distance maps to the landing distance here; the ledger never feeds it in (Hooked fails closed on a non-finite distance)");
        }

        // Catches: a bound that replaces instead of raising the host line (max written as min), a sign slip on the walk term,
        // a missing HookedLineSlack, a missing floor at the landing distance, using the wrong speed constant, and a NaN elapsed time
        // poisoning the result.
        [Test]
        public void CatchValidator_WalkBoundedLineLength0_RaisesTheHostLineOnly()
        {
            const float sprint = MovementMath.SprintSpeed;
            const float slack = FishingConstants.HookedLineSlack;   // a literal 1.5 until T19-R1
            // host line 2 ("standing at the bobber"), cast 24 m, 1 s since accept: 24 - sprint - slack
            Assert.That(CatchValidator.WalkBoundedLineLength0(2f, 24f, 1.0, 2f), Is.EqualTo(24f - sprint - slack).Within(1e-4f), "uses the Core sprint speed");
            // elapsed 0: cast - slack
            Assert.That(CatchValidator.WalkBoundedLineLength0(2f, 24f, 0.0, 2f), Is.EqualTo(24f - slack).Within(1e-4f));
            // an honest longer host line wins
            Assert.That(CatchValidator.WalkBoundedLineLength0(30f, 24f, 1.0, 2f), Is.EqualTo(30f));
            // bound below host line and below the landing distance: host line (already floored by HostLineLength0)
            Assert.That(CatchValidator.WalkBoundedLineLength0(2f, 24f, 10.0, 2f), Is.EqualTo(2f), "negative bound: floor at landDistance");
            Assert.That(CatchValidator.WalkBoundedLineLength0(5f, 24f, 10.0, 2f), Is.EqualTo(5f), "negative bound: host line");
            // exact crossover: 24 - sprint * t - slack == 5  ->  t = (24 - slack - 5) / sprint
            double tCross = (24f - slack - 5f) / sprint;
            Assert.That(CatchValidator.WalkBoundedLineLength0(5f, 24f, tCross - 0.1, 2f), Is.EqualTo(5f + sprint * 0.1f).Within(1e-3f));
            Assert.That(CatchValidator.WalkBoundedLineLength0(5f, 24f, tCross + 0.1, 2f), Is.EqualTo(5f));
            // the landing-distance floor applies even when the host line is below it (defensive)
            Assert.That(CatchValidator.WalkBoundedLineLength0(0.5f, 1f, 5.0, 2f), Is.EqualTo(2f));
            // non-finite elapsed time must not poison the result
            Assert.That(CatchValidator.WalkBoundedLineLength0(7f, 24f, double.NaN, 2f), Is.EqualTo(7f));
            Assert.That(CatchValidator.WalkBoundedLineLength0(7f, 24f, double.PositiveInfinity, 2f), Is.EqualTo(7f));
        }

        [Test]
        public void CatchValidator_AnnounceFor_Table()
        {
            Assert.That(CatchValidator.AnnounceFor(FightOutcome.Landed, EscapeReason.None, true), Is.EqualTo(AnnounceKind.Landed));
            Assert.That(CatchValidator.AnnounceFor(FightOutcome.Snapped, EscapeReason.None, true), Is.EqualTo(AnnounceKind.Lost));
            foreach (EscapeReason r in new[] { EscapeReason.Pinned, EscapeReason.ThrewHook, EscapeReason.Dropped, EscapeReason.Timeout })
            {
                Assert.That(CatchValidator.AnnounceFor(FightOutcome.Escaped, r, true), Is.EqualTo(AnnounceKind.Lost), r + " hooked");
                Assert.That(CatchValidator.AnnounceFor(FightOutcome.Escaped, r, false), Is.EqualTo(AnnounceKind.None), r + " not hooked");
            }
            foreach (EscapeReason r in new[] { EscapeReason.Missed, EscapeReason.Invalid, EscapeReason.Disconnected, EscapeReason.None })
                Assert.That(CatchValidator.AnnounceFor(FightOutcome.Escaped, r, true), Is.EqualTo(AnnounceKind.None), r.ToString());
            Assert.That(CatchValidator.AnnounceFor(FightOutcome.Cancelled, EscapeReason.None, false), Is.EqualTo(AnnounceKind.None));
            Assert.That(CatchValidator.AnnounceFor(FightOutcome.None, EscapeReason.None, true), Is.EqualTo(AnnounceKind.None));
        }

        // Base: a fish hooked at t = 100 on a line of 8.5 (floor 4.7 at reel speed 1.0), now = 110 (10 s after the hook), threshold 0.8.
        static ActiveCast FishCast() => new ActiveCast { THooked = 100.0, LineLength0 = 8.5f,
                                                        Roll = new FishingRoll { Catch = new CatchResult { IsJunk = false } } };
        static FightReport Rep(float maxTension, float seconds, int recoveries = 0) =>
            new FightReport { MaxTension = maxTension, FightSeconds = seconds, NearSnapRecoveries = recoveries };
        static bool Accepts(in FightReport r, in ActiveCast c) => CatchValidator.CleanClaimAccepted(in r, 110.0, in c, 2f, 1f, 0.8f);

        // Catches: dropping rule (1), the junk check.
        [Test]
        public void CatchValidator_CleanClaim_JunkNeverGetsTheBonus()
        {
            ActiveCast fish = FishCast();
            FightReport good = Rep(0.5f, 9.5f);
            Assert.That(Accepts(in good, in fish), Is.True, "baseline");
            ActiveCast junk = FishCast(); junk.Roll.Catch.IsJunk = true;
            Assert.That(Accepts(in good, in junk), Is.False);
        }

        // Catches: rule (2) written with > instead of >=, a missing NaN / infinity / negative guard.
        [Test]
        public void CatchValidator_CleanClaim_MaxTensionMustBeFiniteNonNegativeAndBelowThreshold()
        {
            ActiveCast fish = FishCast();
            foreach (float mt in new[] { 0.8f, 0.9f, 1f, -0.001f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                FightReport r = Rep(mt, 9.5f);
                Assert.That(Accepts(in r, in fish), Is.False, "max tension " + mt);
            }
            FightReport justBelow = Rep(0.7999f, 9.5f);
            Assert.That(Accepts(in justBelow, in fish), Is.True, "0.7999 is clean");
            FightReport zero = Rep(0f, 9.5f);
            Assert.That(Accepts(in zero, in fish), Is.True, "tension 0 is clean");
        }

        // Catches: dropping rule (3): a near-snap recovery contradicts a clean fight.
        [Test]
        public void CatchValidator_CleanClaim_NearSnapRecoveryContradictsCleanFight()
        {
            ActiveCast fish = FishCast();
            FightReport rec = Rep(0.5f, 9.5f, 1);
            Assert.That(Accepts(in rec, in fish), Is.False);
            FightReport many = Rep(0.5f, 9.5f, 7);
            Assert.That(Accepts(in many, in fish), Is.False);
        }

        // Catches: dropping rule (4), a floor check with <=, no NaN / infinity guard on the seconds.
        [Test]
        public void CatchValidator_CleanClaim_FightSecondsMustReachTheLandingFloor()
        {
            ActiveCast fish = FishCast();
            FightReport nan = Rep(0.5f, float.NaN);
            FightReport inf = Rep(0.5f, float.PositiveInfinity);
            Assert.That(Accepts(in nan, in fish), Is.False, "NaN seconds");
            Assert.That(Accepts(in inf, in fish), Is.False, "infinite seconds");
            float floor = CatchValidator.LandedFloorSeconds(8.5f, 2f, 1f);
            FightReport atFloor = Rep(0.5f, floor);
            FightReport belowFloor = Rep(0.5f, floor - 0.01f);
            Assert.That(Accepts(in atFloor, in fish), Is.True, "exactly the floor is fine");
            Assert.That(Accepts(in belowFloor, in fish), Is.False, "below the floor");
        }

        // Catches: dropping rule (5), a bound with >=, a symmetric bound (the owner clock may run far behind the host).
        [Test]
        public void CatchValidator_CleanClaim_FightSecondsNeverAheadOfHostClockPlusSlack()
        {
            ActiveCast fish = FishCast();   // host: 10 s since the hook, slack 2
            FightReport atLimit = Rep(0.5f, 12f);
            FightReport past = Rep(0.5f, 12.01f);
            FightReport behind = Rep(0.5f, 6f);
            Assert.That(Accepts(in atLimit, in fish), Is.True, "exactly host + slack is fine");
            Assert.That(Accepts(in past, in fish), Is.False, "ahead of the host clock plus slack");
            Assert.That(Accepts(in behind, in fish), Is.True, "an owner clock far behind the host is allowed (frame hitches)");
        }

        // Catches (R72): a floor that uses the rod's plain reel speed instead of rod x ReelInMult x the tire maximum (a tired fish reels 3x faster,
        // so an honest fast landing would be refused), or a ReelInMult/TireReelMaxMult read from the wrong place.
        [Test]
        public void CatchValidator_MaxReelSpeed_IsRodTimesReelInTimesTireMax()
        {
            FishingTuning t = FishingTestData.Tuning();
            RodStats[] rods = FishingTestData.Rods();
            FishFightProfile[] profiles = FishingTestData.BaseProfiles();
            Assert.That(CatchValidator.MaxReelSpeed(rods[FishingTestData.RodHandmade], profiles[FishingTestData.ProfileSteady], in t), Is.EqualTo(2.16f).Within(1e-6f));
            Assert.That(CatchValidator.MaxReelSpeed(rods[FishingTestData.RodTwig], profiles[FishingTestData.ProfileRunner], in t), Is.EqualTo(1.458f).Within(1e-6f));
            Assert.That(CatchValidator.MaxReelSpeed(rods[FishingTestData.RodHandmade], profiles[FishingTestData.ProfileDarter], in t), Is.EqualTo(1.8f).Within(1e-6f));
        }

        // Catches (R72): an owner Timeout accepted before the host's own clock is within 2 s of FightTimeout, or refused at the boundary.
        [Test]
        public void CatchValidator_OwnerTimeoutPlausible_UsesTwoSecondSlack()
        {
            var cast = new ActiveCast { THooked = 100.0 };
            Assert.That(CatchValidator.OwnerTimeoutPlausible(217.99, in cast, 120f), Is.False);
            Assert.That(CatchValidator.OwnerTimeoutPlausible(218.0, in cast, 120f), Is.True);
        }
    }
}
