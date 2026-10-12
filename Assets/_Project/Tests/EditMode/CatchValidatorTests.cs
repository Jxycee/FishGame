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
            new HostCastView { PlayerX = 1f, PlayerY = 0f, PlayerZ = 1f, CastingAllowed = true,
                               Probe = new WaterProbe { FirstHitIsWater = true, WaterY = 0f, HasBed = true, Depth = 3f } };

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
            req.ChargeNorm = 0f;
            Assert.That(CatchValidator.ValidateCast(in req, 4, false, in view, true, 0.8f), Is.EqualTo(CastRejectReason.None), "charge 0");
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
            Assert.That(CatchValidator.HostLineLength0(10f, 2f), Is.EqualTo(8.5f).Within(1e-5f));
            Assert.That(CatchValidator.HostLineLength0(3f, 2f), Is.EqualTo(2f), "never below the landing distance");
            Assert.That(CatchValidator.HostLineLength0(3.4f, 2f), Is.EqualTo(2f));
            Assert.That(CatchValidator.HostLineLength0(3.6f, 2f), Is.EqualTo(2.1f).Within(1e-5f));
            Assert.That(CatchValidator.HostLineLength0(float.NaN, 2f), Is.EqualTo(2f), "a NaN distance falls back to the landing distance");
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
    }
}
