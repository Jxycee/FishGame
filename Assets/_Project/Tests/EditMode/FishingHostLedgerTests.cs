using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Host ledger (T9, spec 9.9). Shared setup: ledger seed 42, handmade rod, worm bait, slot 0 / client 100; cast 0.5 charge lands at
    // distance 10.25 (flight 0.573 s). Ov forces a 2 s bite on a carp, so HookAt(tf + 2.0) hooks exactly on time. A hook at HookDistance
    // (8.5 + HookedLineSlack, so it tracks the constant; it was a literal 10 while the slack was 1.5, T19-R1) gives LineLength0 8.5, so the
    // handmade landing floor is 0.8 x 6.5 / 1.0 - 0.5 = 4.7 s (junk: 0.8 x 6.5 / 3 - 0.5 = 1.233 s).
    public class FishingHostLedgerTests
    {
        const ulong Client = 100;
        const float HookDistance = 8.5f + FishingConstants.HookedLineSlack;   // host distance whose LineLength0 is exactly 8.5
        const ulong Seed = 42;
        static readonly float Tf = CastMath.FlightTime(CastMath.Distance(0.5f, 2.5f, 18f));

        // ---- helpers -----------------------------------------------------------------------------------------------

        static FishingDatabase Db() => FishingTestData.Database();

        static FishingDatabase DbPity(int after)
        {
            FishingTuning t = FishingTestData.Tuning();
            t.PityAfterFailures = after;
            return new FishingDatabase(FishingTestData.Rods(), FishingTestData.Baits(), FishingTestData.MotionProfiles(),
                                       FishingTestData.Species(), FishingTestData.Junk(), in t);
        }

        static FishingHostLedger NewLedger(ulong seed = Seed, FishingDatabase db = null)
        {
            var l = new FishingHostLedger(db ?? Db(), seed, FishingTestData.RodHandmade, FishingTestData.BaitWorm);
            l.EnsureSlot(0);
            return l;
        }

        static CastRequestData Req(uint id, float charge = 0.5f, int bait = FishingTestData.BaitWorm) =>
            new CastRequestData { CastId = id, OriginX = 0f, OriginY = 0f, OriginZ = 0f, YawDeg = 0f, ChargeNorm = charge,
                                  BaitIndex = (byte)bait, Assist = false };

        static HostCastView View(float depth = 3f, bool water = true, bool hasBed = true, bool allowed = true) =>
            new HostCastView { PlayerX = 0f, PlayerY = 0f, PlayerZ = 0f, CastingAllowed = allowed,
                               Probe = new WaterProbe { FirstHitIsWater = water, WaterY = 0f, HasBed = hasBed, Depth = depth } };

        static RollOverrides Ov => new RollOverrides { ForcedBiteWait = 2f, ForcedItemIndex = FishingTestData.SpeciesCarp };
        static RollOverrides OvJunk => new RollOverrides { ForcedBiteWait = 2f, ForcedItemIndex = -1, ForceJunk = true };

        static FightReport Clean(float fs, int recoveries = 0, float maxTension = 0.5f) =>
            new FightReport { MaxTension = maxTension, FightSeconds = fs, NearSnapRecoveries = recoveries };
        static FightReport NotClean(float fs) => new FightReport { MaxTension = 0.9f, FightSeconds = fs, NearSnapRecoveries = 0 };
        static CreelSnapshot Creel() => new CreelSnapshot(0, 0, 12, 15000);

        static CastVerdict Request(FishingHostLedger l, uint id, RollOverrides ov, ulong client = Client, byte slot = 0)
        {
            return l.RequestCast(client, slot, Req(id), View(), in ov);
        }

        static CastVerdict Accept(FishingHostLedger l, uint id, RollOverrides ov, ulong client = Client, byte slot = 0)
        {
            CastVerdict v = Request(l, id, ov, client, slot);
            Assert.That(v.Valid, Is.True, "cast " + id + " rejected: " + v.Reason);
            return v;
        }

        static List<CommitDecision> Tick(FishingHostLedger l, double dt, bool paused = false)
        {
            var sink = new List<CommitDecision>();
            l.Tick(dt, paused, sink);
            return sink;
        }

        // Tick to the forced bite (tf + 2.0 s after accept) and send the hook at HookDistance (LineLength0 8.5).
        static void HookAt(FishingHostLedger l, uint id, ulong client = Client)
        {
            List<CommitDecision> sink = Tick(l, Tf + 2.0);
            Assert.That(sink, Is.Empty, "nothing may time out before the hook");
            bool commit = l.Hooked(client, id, HookDistance, out CommitDecision d);
            Assert.That(commit, Is.False, "hook refused: " + d.Outcome + "/" + d.Reason);
        }

        static CommitDecision Result(FishingHostLedger l, uint id, FightOutcome o, EscapeReason r, FightReport report, ulong client = Client)
        {
            CreelSnapshot creel = Creel();
            return l.Result(client, id, o, r, in report, in creel);
        }

        static CommitDecision CancelCast(FishingHostLedger l, uint id, ulong client = Client) => l.Cancel(client, id);

        // Accept a cast and resolve it as a failure. kind 0: hooked then Snapped; 1: Missed in Waiting; 2: hooked then Escaped/Pinned.
        static CastVerdict CastAndFail(FishingHostLedger l, uint id, int kind, ulong client = Client, byte slot = 0)
        {
            CastVerdict v = Accept(l, id, Ov, client, slot);
            CommitDecision d;
            if (kind == 1) d = Result(l, id, FightOutcome.Escaped, EscapeReason.Missed, NotClean(0f), client);
            else
            {
                HookAt(l, id, client);
                d = kind == 0 ? Result(l, id, FightOutcome.Snapped, EscapeReason.None, NotClean(3f), client)
                              : Result(l, id, FightOutcome.Escaped, EscapeReason.Pinned, NotClean(3f), client);
            }
            Assert.That(d.Accepted, Is.True);
            Assert.That(d.Outcome, Is.EqualTo(kind == 0 ? FightOutcome.Snapped : FightOutcome.Escaped), "cast " + id + " should resolve as a failure");
            Assert.That(d.Reason, Is.Not.EqualTo(EscapeReason.Invalid), "cast " + id + " must be a real failure, not a validation reject");
            return v;
        }

        // Accept a cast and land it validly (hooked, 10 s later), no clean bonus.
        static CommitDecision CastAndLand(FishingHostLedger l, uint id, ulong client = Client)
        {
            Accept(l, id, Ov, client);
            HookAt(l, id, client);
            Tick(l, 10.0);
            CommitDecision d = Result(l, id, FightOutcome.Landed, EscapeReason.None, NotClean(10f), client);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed), "cast " + id + " should land");
            return d;
        }

        static void AssertSameRoll(in FishingRoll want, in FishingRoll got, bool comparePity, string why = "")
        {
            Assert.That(got.Catch.IsJunk, Is.EqualTo(want.Catch.IsJunk), why + " IsJunk");
            Assert.That(got.Catch.ItemIndex, Is.EqualTo(want.Catch.ItemIndex), why + " ItemIndex");
            Assert.That(got.Catch.WeightG, Is.EqualTo(want.Catch.WeightG), why + " WeightG");
            Assert.That(got.Catch.LengthMm, Is.EqualTo(want.Catch.LengthMm), why + " LengthMm");
            Assert.That(got.Catch.Quality, Is.EqualTo(want.Catch.Quality), why + " Quality");
            Assert.That(got.Catch.ValueCoins, Is.EqualTo(want.Catch.ValueCoins), why + " ValueCoins");
            Assert.That(got.WeightT, Is.EqualTo(want.WeightT), why + " WeightT");
            Assert.That(got.BiteWait, Is.EqualTo(want.BiteWait), why + " BiteWait");
            Assert.That(got.NibbleCount, Is.EqualTo(want.NibbleCount), why + " NibbleCount");
            Assert.That(got.Nibble0, Is.EqualTo(want.Nibble0), why + " Nibble0");
            Assert.That(got.Nibble1, Is.EqualTo(want.Nibble1), why + " Nibble1");
            Assert.That(got.Nibble2, Is.EqualTo(want.Nibble2), why + " Nibble2");
            Assert.That(got.FightSeed, Is.EqualTo(want.FightSeed), why + " FightSeed");
            Assert.That(got.SizeClass, Is.EqualTo(want.SizeClass), why + " SizeClass");
            Assert.That(got.HookWindow, Is.EqualTo(want.HookWindow), why + " HookWindow");
            if (comparePity) Assert.That(got.Pity, Is.EqualTo(want.Pity), why + " Pity");
        }

        static void AssertSameCatch(in CatchResult want, in CatchResult got)
        {
            Assert.That(got.IsJunk, Is.EqualTo(want.IsJunk));
            Assert.That(got.ItemIndex, Is.EqualTo(want.ItemIndex));
            Assert.That(got.WeightG, Is.EqualTo(want.WeightG));
            Assert.That(got.LengthMm, Is.EqualTo(want.LengthMm));
            Assert.That(got.Quality, Is.EqualTo(want.Quality));
            Assert.That(got.ValueCoins, Is.EqualTo(want.ValueCoins));
        }

        // What the placeholder roller makes for cast number n of a ledger seed, with the Ov overrides (forced carp, 2 s bite).
        static FishingRoll Expected(FishingDatabase db, ulong seed, ulong n, bool overrides)
        {
            Pcg32 rng = Rng.Stream(seed, "catch", n);
            var rr = new RollRequest { BaitIndex = FishingTestData.BaitWorm, LoadedCount = 1, Depth = 3f, Luck = 0f,
                                       ForcedBiteWait = overrides ? 2f : 0f, ForcedItemIndex = overrides ? FishingTestData.SpeciesCarp : -1 };
            return PlaceholderRoller.Roll(db, in rr, ref rng);
        }

        static int Stock(FishingHostLedger l, byte slot = 0) => l.GetLoadout(slot).BaitCount;

        // ---- validation order and reject reasons -----------------------------------------------------------------

        [Test]
        public void FishingHostLedger_CastWhileActive_Rejected()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            CastVerdict v = Request(l, 2, Ov);
            Assert.That(v.Valid, Is.False);
            Assert.That(v.Reason, Is.EqualTo(CastRejectReason.ActiveCast));
            Assert.That(Stock(l), Is.EqualTo(19), "the rejected request must not consume bait");
            Assert.That(l.HostCastCounter, Is.EqualTo(1u));
        }

        [Test]
        public void FishingHostLedger_StaleOrDuplicateCastId_Rejected()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            CommitDecision c = CancelCast(l, 1);
            Assert.That(c.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            CastVerdict dup = Request(l, 1, Ov);
            Assert.That(dup.Valid, Is.False);
            Assert.That(dup.Reason, Is.EqualTo(CastRejectReason.StaleCastId), "same id as the last accepted cast");
            CastVerdict zero = Request(l, 0, Ov);
            Assert.That(zero.Valid, Is.False);
            Assert.That(zero.Reason, Is.EqualTo(CastRejectReason.StaleCastId), "id 0 is never valid");
            Accept(l, 2, Ov);
            CancelCast(l, 2);
            CastVerdict back = Request(l, 1, Ov);
            Assert.That(back.Reason, Is.EqualTo(CastRejectReason.StaleCastId), "an older id after a newer accept");
            Assert.That(Request(l, 5, Ov).Valid, Is.True, "ids may skip ahead");
        }

        [Test]
        public void FishingHostLedger_ChargeOutsideUnitRange_Rejected()
        {
            FishingHostLedger l = NewLedger();
            foreach (float bad in new[] { -0.01f, 1.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                CastVerdict v = l.RequestCast(Client, 0, Req(1, bad), View(), RollOverrides.None);
                Assert.That(v.Valid, Is.False, "charge " + bad);
                Assert.That(v.Reason, Is.EqualTo(CastRejectReason.BadCharge), "charge " + bad);
            }
            Assert.That(Stock(l), Is.EqualTo(20));
            CastVerdict zero = l.RequestCast(Client, 0, Req(1, 0f), View(), RollOverrides.None);
            Assert.That(zero.Valid, Is.True, "charge 0 is allowed");
            CancelCast(l, 1);
            CastVerdict one = l.RequestCast(Client, 0, Req(2, 1f), View(), RollOverrides.None);
            Assert.That(one.Valid, Is.True, "charge 1 is allowed");
            CancelCast(l, 2);
        }

        static CastRequestData ReqAt(uint id, float x, float y, float z)
        {
            CastRequestData r = Req(id);
            r.OriginX = x; r.OriginY = y; r.OriginZ = z;
            return r;
        }

        [Test]
        public void FishingHostLedger_OriginBeyondThreeMeters_Rejected()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict far = l.RequestCast(Client, 0, ReqAt(1, 3.01f, 0f, 0f), View(), RollOverrides.None);
            Assert.That(far.Valid, Is.False);
            Assert.That(far.Reason, Is.EqualTo(CastRejectReason.OriginTooFar));
            CastVerdict farZ = l.RequestCast(Client, 0, ReqAt(1, 0f, 0f, -3.01f), View(), RollOverrides.None);
            Assert.That(farZ.Reason, Is.EqualTo(CastRejectReason.OriginTooFar), "both horizontal axes count");
            CastVerdict diag = l.RequestCast(Client, 0, ReqAt(1, 2.2f, 0f, 2.2f), View(), RollOverrides.None);
            Assert.That(diag.Reason, Is.EqualTo(CastRejectReason.OriginTooFar), "3.11 m diagonal is horizontal distance, not per axis");
            CastVerdict nan = l.RequestCast(Client, 0, ReqAt(1, float.NaN, 0f, 0f), View(), RollOverrides.None);
            Assert.That(nan.Reason, Is.EqualTo(CastRejectReason.OriginTooFar), "a NaN origin is never accepted");
            Assert.That(Stock(l), Is.EqualTo(20));

            CastVerdict edge = l.RequestCast(Client, 0, ReqAt(1, 3.0f, 5f, 0f), View(), RollOverrides.None);
            Assert.That(edge.Valid, Is.True, "exactly 3.0 m horizontal is allowed; the vertical offset is ignored");
        }

        [Test]
        public void FishingHostLedger_NoBait_Rejected()
        {
            FishingHostLedger l = NewLedger();
            for (uint id = 1; id <= 20; id++) CastAndFail(l, id, 1);
            Assert.That(Stock(l), Is.EqualTo(0), "Missed never refunds");
            CastVerdict v = Request(l, 21, Ov);
            Assert.That(v.Valid, Is.False);
            Assert.That(v.Reason, Is.EqualTo(CastRejectReason.NoBait));
            Assert.That(l.GetLoadout(0).BaitCount, Is.EqualTo(0));
            Assert.That(l.HostCastCounter, Is.EqualTo(20u));
        }

        [Test]
        public void FishingHostLedger_InvalidWater_Rejected()
        {
            FishingHostLedger l = NewLedger();
            RollOverrides none = RollOverrides.None;
            HostCastView notWater = View(water: false);
            HostCastView shallow = View(depth: 0.5f);
            HostCastView noBed = View(hasBed: false);
            Assert.That(l.RequestCast(Client, 0, Req(1), notWater, in none).Reason, Is.EqualTo(CastRejectReason.NotWater));
            Assert.That(l.RequestCast(Client, 0, Req(1), shallow, in none).Reason, Is.EqualTo(CastRejectReason.TooShallow));
            Assert.That(l.RequestCast(Client, 0, Req(1), noBed, in none).Reason, Is.EqualTo(CastRejectReason.TooShallow));
            Assert.That(Stock(l), Is.EqualTo(20));
            Assert.That(l.HostCastCounter, Is.EqualTo(0u));
        }

        [Test]
        public void FishingHostLedger_CastingNotAllowed_RejectedBeforeEverythingElse()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);   // an active cast too: NotAllowedNow still wins (first in the order)
            RollOverrides none = RollOverrides.None;
            HostCastView blocked = View(allowed: false);
            CastVerdict v = l.RequestCast(Client, 0, Req(1, float.NaN), blocked, in none);
            Assert.That(v.Reason, Is.EqualTo(CastRejectReason.NotAllowedNow));
        }

        [Test]
        public void FishingHostLedger_UnknownSlot_RejectedUnknownPlayer()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Request(l, 1, Ov, 777, 3);
            Assert.That(v.Valid, Is.False);
            Assert.That(v.Reason, Is.EqualTo(CastRejectReason.UnknownPlayer));
            Assert.That(l.HostCastCounter, Is.EqualTo(0u));
            Assert.That(l.TryComputeLanding(3, Req(1), out _, out _), Is.False);
            l.EnsureSlot(3);
            Assert.That(Request(l, 1, Ov, 777, 3).Valid, Is.True, "valid once the slot is ensured");
        }

        // ---- accept, rolls, landing --------------------------------------------------------------------------------

        [Test]
        public void FishingHostLedger_Accept_ConsumesBaitAndRolls()
        {
            FishingDatabase db = Db();
            FishingHostLedger l = NewLedger(Seed, db);
            Assert.That(l.RunSeed, Is.EqualTo(Seed));
            RollOverrides none = RollOverrides.None;
            CastVerdict v = l.RequestCast(Client, 0, Req(1), View(), in none);
            Assert.That(v.Valid, Is.True);
            Assert.That(v.Reason, Is.EqualTo(CastRejectReason.None));
            Assert.That(Stock(l), Is.EqualTo(19));
            Assert.That(l.HostCastCounter, Is.EqualTo(1u));

            Pcg32 rng = Rng.Stream(42, "catch", 1);
            var rr = new RollRequest { BaitIndex = FishingTestData.BaitWorm, LoadedCount = 1, Depth = 3f, Luck = 0f, ForcedItemIndex = -1 };
            FishingRoll want = PlaceholderRoller.Roll(db, in rr, ref rng);
            AssertSameRoll(in want, in v.Roll, true, "verdict");

            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            Assert.That(a.CastId, Is.EqualTo(1u));
            Assert.That(a.Phase, Is.EqualTo(CastPhase.Waiting));
            Assert.That(a.TAccepted, Is.EqualTo(0.0));
            Assert.That(a.TFlight, Is.EqualTo(Tf).Within(1e-6f));
            Assert.That(a.TFlight, Is.EqualTo(0.573f).Within(1e-3f));
            Assert.That(a.LandingX, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.LandingZ, Is.EqualTo(10.25f).Within(1e-4f));
            Assert.That(a.RodIndex, Is.EqualTo((ushort)FishingTestData.RodHandmade));
            Assert.That(a.BaitIndex, Is.EqualTo((byte)FishingTestData.BaitWorm));
            Assert.That(a.Assist, Is.False);
            AssertSameRoll(in want, in a.Roll, true, "active");
            Assert.That(v.LandingX, Is.EqualTo(a.LandingX));
            Assert.That(v.LandingZ, Is.EqualTo(a.LandingZ));
            Assert.That(l.TryComputeLanding(0, Req(1), out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(a.LandingX));
            Assert.That(z, Is.EqualTo(a.LandingZ));
        }

        [Test]
        public void FishingHostLedger_SelectedRod_DrivesLandingDistanceAndFlightTime()
        {
            FishingHostLedger l = NewLedger();
            Assert.That(l.SelectRod(Client, 0, FishingTestData.RodTwig), Is.True);
            Assert.That(l.GetLoadout(0).RodIndex, Is.EqualTo((ushort)FishingTestData.RodTwig));
            Assert.That(l.TryComputeLanding(0, Req(1), out float x, out float z), Is.True);
            Assert.That(z, Is.EqualTo(2.5f + (14f - 2.5f) * 0.5f).Within(1e-4f), "twig rod reaches 14 m at full charge");
            Accept(l, 1, Ov);
            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            Assert.That(a.RodIndex, Is.EqualTo((ushort)FishingTestData.RodTwig));
            Assert.That(a.LandingZ, Is.EqualTo(8.25f).Within(1e-4f));
            Assert.That(a.TFlight, Is.EqualTo(CastMath.FlightTime(8.25f)).Within(1e-5f));
            Assert.That(l.TryComputeLanding(0, Req(1, float.NaN), out _, out _), Is.False);
            Assert.That(l.TryComputeLanding(0, Req(1, 1.01f), out _, out _), Is.False);
            Assert.That(l.TryComputeLanding(0, Req(1, -0.1f), out _, out _), Is.False);
        }

        [Test]
        public void FishingHostLedger_BareHook_NeverRunsOut()
        {
            FishingHostLedger l = NewLedger();
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitBareHook), Is.True);
            Assert.That(l.GetLoadout(0).BaitCount, Is.EqualTo(FishingConstants.UnlimitedBaitCount));
            RollOverrides none = RollOverrides.None;
            for (uint id = 1; id <= 25; id++)
            {
                CastVerdict v = l.RequestCast(Client, 0, Req(id, 0.5f, FishingTestData.BaitBareHook), View(), in none);
                Assert.That(v.Valid, Is.True, "cast " + id + ": " + v.Reason);
                CommitDecision d = Result(l, id, FightOutcome.Escaped, EscapeReason.Missed, NotClean(0f));
                Assert.That(d.Refunded, Is.False);
            }
            Assert.That(l.GetLoadout(0).BaitCount, Is.EqualTo((ushort)0xFFFF));
            Assert.That(l.GetLoadout(0).BaitIndex, Is.EqualTo((byte)FishingTestData.BaitBareHook));
            Assert.That(l.HostCastCounter, Is.EqualTo(25u));
        }

        [Test]
        public void FishingHostLedger_RollSeed_DerivesFromRunSeedAndCounter()
        {
            FishingDatabase db = Db();
            FishingHostLedger l = NewLedger(42, db);
            CastVerdict first = Accept(l, 1, RollOverrides.None);
            CancelCast(l, 1);
            CastVerdict second = Accept(l, 2, RollOverrides.None);
            FishingRoll want1 = Expected(db, 42, 1, false);
            FishingRoll want2 = Expected(db, 42, 2, false);
            AssertSameRoll(in want1, in first.Roll, true, "cast 1");
            AssertSameRoll(in want2, in second.Roll, true, "cast 2");
            Assert.That(second.Roll.FightSeed, Is.Not.EqualTo(first.Roll.FightSeed));

            FishingHostLedger other = NewLedger(43, db);
            CastVerdict o1 = Accept(other, 1, RollOverrides.None);
            FishingRoll want43 = Expected(db, 43, 1, false);
            AssertSameRoll(in want43, in o1.Roll, true, "seed 43");
            Assert.That(o1.Roll.FightSeed, Is.Not.EqualTo(first.Roll.FightSeed), "a different run seed gives a different stream");
        }

        // ---- loadout selection -------------------------------------------------------------------------------------

        [Test]
        public void FishingHostLedger_SelectWhileActive_Refused()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitGrub), Is.False);
            Assert.That(l.SelectRod(Client, 0, FishingTestData.RodTwig), Is.False);
            Assert.That(l.GetLoadout(0).BaitIndex, Is.EqualTo((byte)FishingTestData.BaitWorm), "refused selection changes nothing");
            Assert.That(l.GetLoadout(0).RodIndex, Is.EqualTo((ushort)FishingTestData.RodHandmade));
            CancelCast(l, 1);
            Assert.That(l.SelectBait(Client, 0, 6), Is.False, "bait index 6 does not exist");
            Assert.That(l.SelectRod(Client, 0, 4), Is.False, "rod index 4 does not exist");
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitGrub), Is.True);
            Assert.That(l.SelectRod(Client, 0, FishingTestData.RodTwig), Is.True);
            PlayerLoadout lo = l.GetLoadout(0);
            Assert.That(lo.BaitIndex, Is.EqualTo((byte)FishingTestData.BaitGrub));
            Assert.That(lo.RodIndex, Is.EqualTo((ushort)FishingTestData.RodTwig));
            Assert.That(lo.BaitCount, Is.EqualTo((ushort)20), "grub stock is its own");
            Assert.That(l.SelectBait(Client, 5, (byte)FishingTestData.BaitGrub), Is.False, "unknown slot");
        }

        // ---- resolutions -------------------------------------------------------------------------------------------

        [Test]
        public void FishingHostLedger_CancelInWaiting_RefundsBait()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, Ov);
            Assert.That(Stock(l), Is.EqualTo(19));
            CommitDecision d = CancelCast(l, 1);
            Assert.That(d.Accepted, Is.True);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(d.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(d.Refunded, Is.True);
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.None));
            Assert.That(d.AddToCreel, Is.False);
            Assert.That(d.ClientId, Is.EqualTo(Client));
            Assert.That(d.Slot, Is.EqualTo((byte)0));
            Assert.That(d.CastId, Is.EqualTo(1u));
            Assert.That(Stock(l), Is.EqualTo(20));
            AssertSameRoll(in v.Roll, in d.Roll, true, "BF-4 decision roll");
            Assert.That(l.TryGetActive(Client, out _), Is.False);
        }

        [Test]
        public void FishingHostLedger_CancelInWaiting_UnlimitedBaitIsNotRefundedOrCounted()
        {
            FishingHostLedger l = NewLedger();
            l.SelectBait(Client, 0, (byte)FishingTestData.BaitBareHook);
            RollOverrides ov = Ov;
            l.RequestCast(Client, 0, Req(1, 0.5f, FishingTestData.BaitBareHook), View(), in ov);
            CommitDecision d = CancelCast(l, 1);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(d.Refunded, Is.False, "nothing was consumed, nothing is refunded");
            Assert.That(l.GetLoadout(0).BaitCount, Is.EqualTo(FishingConstants.UnlimitedBaitCount));
        }

        [Test]
        public void FishingHostLedger_CancelInHooked_ResolvesDroppedAnnouncedLostNoRefund()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, Ov);
            HookAt(l, 1);
            CommitDecision d = CancelCast(l, 1);
            Assert.That(d.Accepted, Is.True);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(d.Reason, Is.EqualTo(EscapeReason.Dropped));
            Assert.That(d.Refunded, Is.False);
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.Lost));
            Assert.That(d.AddToCreel, Is.False);
            AssertSameRoll(in v.Roll, in d.Roll, true, "BF-4 decision roll");
            Assert.That(Stock(l), Is.EqualTo(19));
            Assert.That(l.TryGetActive(Client, out _), Is.False);
        }

        [Test]
        public void FishingHostLedger_SnapEscapeMiss_DoNotRefund()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov); HookAt(l, 1);
            CommitDecision snap = Result(l, 1, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));
            Accept(l, 2, Ov); HookAt(l, 2);
            CommitDecision pinned = Result(l, 2, FightOutcome.Escaped, EscapeReason.Pinned, NotClean(3f));
            Accept(l, 3, Ov);
            CommitDecision missed = Result(l, 3, FightOutcome.Escaped, EscapeReason.Missed, NotClean(0f));
            Assert.That(Stock(l), Is.EqualTo(17));
            Assert.That(snap.Refunded, Is.False);
            Assert.That(pinned.Refunded, Is.False);
            Assert.That(missed.Refunded, Is.False);
            Assert.That(snap.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(pinned.Reason, Is.EqualTo(EscapeReason.Pinned));
            Assert.That(missed.Reason, Is.EqualTo(EscapeReason.Missed));
        }

        [Test]
        public void FishingHostLedger_EarlyHook_ResolvesEscapedInvalid()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            Tick(l, Tf + 2.0 - 0.75 - 0.05);   // hook tolerance is 0.75 s
            bool commit = l.Hooked(Client, 1, HookDistance, out CommitDecision d);
            Assert.That(commit, Is.True, "too early: a commit must be sent");
            Assert.That(d.Accepted, Is.True);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(d.Reason, Is.EqualTo(EscapeReason.Invalid));
            Assert.That(d.Refunded, Is.False);
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.None));
            Assert.That(d.AddToCreel, Is.False);
            Assert.That(Stock(l), Is.EqualTo(19));
            Assert.That(l.TryGetActive(Client, out _), Is.False);

            Accept(l, 2, Ov);
            Tick(l, Tf + 2.0 - 0.75 + 0.01);
            bool commit2 = l.Hooked(Client, 2, HookDistance, out CommitDecision d2);
            Assert.That(commit2, Is.False, "inside the tolerance: accepted, no commit");
            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            Assert.That(a.Phase, Is.EqualTo(CastPhase.Hooked));
            Assert.That(a.THooked, Is.EqualTo(l.Now));
            Assert.That(a.LineLength0, Is.EqualTo(8.5f).Within(1e-5f));
            Assert.That(d2.Accepted, Is.False, "no decision is produced for an accepted hook");
        }

        [Test]
        public void FishingHostLedger_DuplicateHook_ReturnsFalseAndChangesNothing()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            HookAt(l, 1);
            Assert.That(l.TryGetActive(Client, out ActiveCast before), Is.True);
            Tick(l, 1.0);
            bool commit = l.Hooked(Client, 1, 30f, out _);
            Assert.That(commit, Is.False);
            Assert.That(l.TryGetActive(Client, out ActiveCast after), Is.True);
            Assert.That(after.THooked, Is.EqualTo(before.THooked), "THooked is set once");
            Assert.That(after.LineLength0, Is.EqualTo(before.LineLength0), "LineLength0 is set once");
            Assert.That(after.Phase, Is.EqualTo(CastPhase.Hooked));
        }

        [Test]
        public void FishingHostLedger_LandedBelowDurationFloor_ResolvesEscapedInvalid()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            HookAt(l, 1);
            Tick(l, 4.6);
            CommitDecision d = Result(l, 1, FightOutcome.Landed, EscapeReason.None, NotClean(4.6f));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(d.Reason, Is.EqualTo(EscapeReason.Invalid));
            Assert.That(d.AddToCreel, Is.False);
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.None));
            Assert.That(d.Accepted, Is.True);
            Assert.That(l.CatchLog, Is.Empty, "a rejected landing is not logged");

            Accept(l, 2, Ov);
            HookAt(l, 2);
            Tick(l, 4.8);
            CommitDecision ok = Result(l, 2, FightOutcome.Landed, EscapeReason.None, NotClean(4.8f));
            Assert.That(ok.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(ok.AddToCreel, Is.True);
            Assert.That(ok.Announce, Is.EqualTo(AnnounceKind.Landed));
            Assert.That(l.CatchLog.Count, Is.EqualTo(1));
        }

        [Test]
        public void FishingHostLedger_JunkLanded_UsesJunkDragSpeedFloor()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, OvJunk);
            Assert.That(v.Roll.Catch.IsJunk, Is.True);
            HookAt(l, 1);
            Tick(l, 1.3);
            CommitDecision d = Result(l, 1, FightOutcome.Landed, EscapeReason.None, NotClean(1.3f));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed), "junk floor is 1.233 s");
            Assert.That(d.AddToCreel, Is.True);
            Assert.That(d.CleanBonus, Is.False);

            Accept(l, 2, Ov);
            HookAt(l, 2);
            Tick(l, 1.3);
            CommitDecision fish = Result(l, 2, FightOutcome.Landed, EscapeReason.None, NotClean(1.3f));
            Assert.That(fish.Outcome, Is.EqualTo(FightOutcome.Escaped), "a fish needs 4.7 s");
            Assert.That(fish.Reason, Is.EqualTo(EscapeReason.Invalid));

            Accept(l, 3, OvJunk);
            HookAt(l, 3);
            Tick(l, 1.1);
            CommitDecision tooFastJunk = Result(l, 3, FightOutcome.Landed, EscapeReason.None, NotClean(1.1f));
            Assert.That(tooFastJunk.Reason, Is.EqualTo(EscapeReason.Invalid), "junk below 1.233 s is still too fast");
        }

        [Test]
        public void FishingHostLedger_StuckWaiting_TimesOutAsRefundedCancel()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            List<CommitDecision> first = Tick(l, Tf + 2.0 + 29.9);
            Assert.That(first, Is.Empty);
            Assert.That(l.TryGetActive(Client, out _), Is.True);
            List<CommitDecision> second = Tick(l, 0.2);
            Assert.That(second.Count, Is.EqualTo(1));
            CommitDecision d = second[0];
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(d.Refunded, Is.True);
            Assert.That(d.Accepted, Is.True);
            Assert.That(d.ClientId, Is.EqualTo(Client));
            Assert.That(d.CastId, Is.EqualTo(1u));
            Assert.That(d.Roll.FightSeed, Is.Not.EqualTo(0u), "BF-4: the timeout decision carries the cast roll");
            Assert.That(Stock(l), Is.EqualTo(20));
            Assert.That(l.TryGetActive(Client, out _), Is.False);
        }

        [Test]
        public void FishingHostLedger_HookedPastMaxFightTime_TimesOutEscapedTimeout()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, Ov);
            HookAt(l, 1);
            Assert.That(Tick(l, 239.9), Is.Empty);
            List<CommitDecision> late = Tick(l, 0.2);
            Assert.That(late.Count, Is.EqualTo(1));
            CommitDecision d = late[0];
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(d.Reason, Is.EqualTo(EscapeReason.Timeout));
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.Lost));
            Assert.That(d.Accepted, Is.True);
            Assert.That(d.Refunded, Is.False);
            AssertSameRoll(in v.Roll, in d.Roll, true, "BF-4 timeout decision roll");
            Assert.That(l.TryGetActive(Client, out _), Is.False);
            Assert.That(Stock(l), Is.EqualTo(19));
        }

        [Test]
        public void FishingHostLedger_PausedClock_DoesNotTimeOut()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            double before = l.Now;
            List<CommitDecision> sink = Tick(l, 1000.0, paused: true);
            Assert.That(l.Now, Is.EqualTo(before));
            Assert.That(sink, Is.Empty);
            Assert.That(l.TryGetActive(Client, out _), Is.True);
            HookAt(l, 1);   // timing is unaffected by the paused interval
            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            Assert.That(a.Phase, Is.EqualTo(CastPhase.Hooked));
            Assert.That(l.Now, Is.EqualTo(Tf + 2.0).Within(1e-9));
        }

        [Test]
        public void FishingHostLedger_Disconnect_ClearsWithoutCommit()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            Assert.That(l.Disconnect(Client), Is.True);
            Assert.That(l.TryGetActive(Client, out _), Is.False);
            Assert.That(Tick(l, 100.0), Is.Empty);
            Assert.That(l.Disconnect(Client), Is.False);
            Assert.That(Stock(l), Is.EqualTo(19), "no refund on disconnect");
        }

        [Test]
        public void FishingHostLedger_Rejoin_KeepsSlotBaitAndRestartsCastIds()
        {
            FishingHostLedger l = NewLedger();
            l.EnsureSlot(1);
            for (uint id = 1; id <= 3; id++) CastAndFail(l, id, 1, Client, 1);
            Assert.That(Stock(l, 1), Is.EqualTo(17));
            Assert.That(l.Disconnect(Client), Is.False, "no active cast to clear");
            l.EnsureSlot(1);   // a no-op: stock is kept
            Assert.That(Stock(l, 1), Is.EqualTo(17));
            CastVerdict v = Request(l, 1, Ov, 200, 1);
            Assert.That(v.Valid, Is.True, "the new client starts at id 1 again");
            Assert.That(Stock(l, 1), Is.EqualTo(16));
            Assert.That(l.HostCastCounter, Is.EqualTo(4u));
            Assert.That(Stock(l, 0), Is.EqualTo(20), "slot 0 untouched");
        }

        [Test]
        public void FishingHostLedger_FullCreel_ReleasesFishAndAnnounces()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            HookAt(l, 1);
            Tick(l, 5.0);
            var full = new CreelSnapshot(12, 5000, 12, 15000);
            FightReport report = NotClean(5f);
            CommitDecision d = l.Result(Client, 1, FightOutcome.Landed, EscapeReason.None, in report, in full);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(d.AddToCreel, Is.False);
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.ReleasedCreelFull));
            Assert.That(d.Accepted, Is.True);
            Assert.That(l.CatchLog.Count, Is.EqualTo(1), "released fish are still journaled");
            AssertSameCatch(in d.Roll.Catch, l.CatchLog[0]);
        }

        [Test]
        public void FishingHostLedger_OrphanMessage_RepeatsStoredResolution()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            HookAt(l, 1);
            CommitDecision first = Result(l, 1, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));
            Assert.That(first.Accepted, Is.True);
            Assert.That(Stock(l), Is.EqualTo(19));

            CommitDecision viaResult = Result(l, 1, FightOutcome.Landed, EscapeReason.None, Clean(9.5f));
            bool commit = l.Hooked(Client, 1, HookDistance, out CommitDecision viaHook);
            CommitDecision viaCancel = CancelCast(l, 1);
            Assert.That(commit, Is.True, "an orphan hook always answers with a commit");
            foreach (CommitDecision d in new[] { viaResult, viaHook, viaCancel })
            {
                Assert.That(d.Accepted, Is.False);
                Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Snapped));
                Assert.That(d.Reason, Is.EqualTo(EscapeReason.None));
                Assert.That(d.CastId, Is.EqualTo(1u));
                Assert.That(d.ClientId, Is.EqualTo(Client));
                Assert.That(d.Refunded, Is.False);
                Assert.That(d.Announce, Is.EqualTo(AnnounceKind.None));
                Assert.That(d.AddToCreel, Is.False);
                Assert.That(d.CleanBonus, Is.False);
                FishingRoll none = default;
                AssertSameRoll(in none, in d.Roll, true, "orphan roll is default (BF-4)");
            }

            CommitDecision unknown = CancelCast(l, 99);
            Assert.That(unknown.Accepted, Is.False);
            Assert.That(unknown.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(unknown.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(unknown.CastId, Is.EqualTo(99u));
            Assert.That(Stock(l), Is.EqualTo(19), "orphans never refund");
            Assert.That(l.CatchLog, Is.Empty, "an orphan Landed is never journaled");
        }

        [Test]
        public void FishingHostLedger_OrphanForUnknownClient_IsCancelledNotCrash()
        {
            FishingHostLedger l = NewLedger();
            CommitDecision d = Result(l, 1, FightOutcome.Landed, EscapeReason.None, Clean(9f), 12345);
            Assert.That(d.Accepted, Is.False);
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(l.Hooked(12345, 1, HookDistance, out CommitDecision h), Is.True);
            Assert.That(h.Accepted, Is.False);
            Assert.That(l.CatchLog, Is.Empty);
        }

        [Test]
        public void FishingHostLedger_OrphanHistory_KeepsLastSixteenResolutions()
        {
            FishingHostLedger l = NewLedger();
            for (uint id = 1; id <= 17; id++) CastAndFail(l, id, 1);
            CommitDecision newest = CancelCast(l, 17);
            Assert.That(newest.Accepted, Is.False);
            Assert.That(newest.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(newest.Reason, Is.EqualTo(EscapeReason.Missed));
            CommitDecision oldestKept = CancelCast(l, 2);
            Assert.That(oldestKept.Outcome, Is.EqualTo(FightOutcome.Escaped), "id 2 is the 16th newest");
            CommitDecision evicted = CancelCast(l, 1);
            Assert.That(evicted.Accepted, Is.False);
            Assert.That(evicted.Outcome, Is.EqualTo(FightOutcome.Cancelled), "id 1 fell out of the 16-entry history");
        }

        [Test]
        public void FishingHostLedger_SnapAfterHook_AnnouncedLost()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            HookAt(l, 1);
            Tick(l, 3.0);
            CommitDecision d = Result(l, 1, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.Lost));
            Assert.That(d.AddToCreel, Is.False);
            Assert.That(d.Accepted, Is.True);
            Assert.That(l.TryGetActive(Client, out _), Is.False);
        }

        [Test]
        public void FishingHostLedger_Missed_NotAnnounced()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            Tick(l, Tf + 2.5);
            CommitDecision d = Result(l, 1, FightOutcome.Escaped, EscapeReason.Missed, NotClean(0f));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(d.Reason, Is.EqualTo(EscapeReason.Missed));
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.None));

            Accept(l, 2, Ov);
            Tick(l, Tf + 2.5);
            CommitDecision pinned = Result(l, 2, FightOutcome.Escaped, EscapeReason.Pinned, NotClean(3f));
            Assert.That(pinned.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(pinned.Reason, Is.EqualTo(EscapeReason.Invalid), "Pinned without a hook is not possible");
            Assert.That(pinned.Announce, Is.EqualTo(AnnounceKind.None));
        }

        [Test]
        public void FishingHostLedger_ResultValidity_FollowsHookedState()
        {
            // (outcome, reason, hooked) -> expected (outcome, reason)
            var rows = new (FightOutcome o, EscapeReason r, bool hooked, FightOutcome wantO, EscapeReason wantR)[]
            {
                (FightOutcome.Landed, EscapeReason.Missed, true, FightOutcome.Landed, EscapeReason.None),            // reason ignored on Landed
                (FightOutcome.Landed, EscapeReason.None, false, FightOutcome.Escaped, EscapeReason.Invalid),         // never hooked
                (FightOutcome.Snapped, EscapeReason.Pinned, true, FightOutcome.Snapped, EscapeReason.None),
                (FightOutcome.Snapped, EscapeReason.None, false, FightOutcome.Escaped, EscapeReason.Invalid),
                (FightOutcome.Escaped, EscapeReason.Missed, true, FightOutcome.Escaped, EscapeReason.Invalid),       // Missed means never hooked
                (FightOutcome.Escaped, EscapeReason.Missed, false, FightOutcome.Escaped, EscapeReason.Missed),
                (FightOutcome.Escaped, EscapeReason.Pinned, true, FightOutcome.Escaped, EscapeReason.Pinned),
                (FightOutcome.Escaped, EscapeReason.ThrewHook, true, FightOutcome.Escaped, EscapeReason.ThrewHook),
                (FightOutcome.Escaped, EscapeReason.Dropped, true, FightOutcome.Escaped, EscapeReason.Dropped),
                (FightOutcome.Escaped, EscapeReason.ThrewHook, false, FightOutcome.Escaped, EscapeReason.Invalid),
                (FightOutcome.Escaped, EscapeReason.Timeout, true, FightOutcome.Escaped, EscapeReason.Invalid),     // host-only reason
                (FightOutcome.Escaped, EscapeReason.None, true, FightOutcome.Escaped, EscapeReason.Invalid),
                (FightOutcome.Escaped, EscapeReason.Disconnected, true, FightOutcome.Escaped, EscapeReason.Invalid),
                (FightOutcome.Cancelled, EscapeReason.None, false, FightOutcome.Escaped, EscapeReason.Invalid),     // owner cancels via Cancel, not Result
                (FightOutcome.None, EscapeReason.None, true, FightOutcome.Escaped, EscapeReason.Invalid),
            };
            foreach (var row in rows)
            {
                FishingHostLedger l = NewLedger();
                Accept(l, 1, Ov);
                if (row.hooked) { HookAt(l, 1); Tick(l, 10.0); }
                CommitDecision d = Result(l, 1, row.o, row.r, NotClean(10f));
                string who = row.o + "/" + row.r + " hooked=" + row.hooked;
                Assert.That(d.Outcome, Is.EqualTo(row.wantO), who);
                Assert.That(d.Reason, Is.EqualTo(row.wantR), who);
                Assert.That(d.Accepted, Is.True, who);
                Assert.That(l.TryGetActive(Client, out _), Is.False, who + ": every path clears the cast");
            }
        }

        [Test]
        public void FishingHostLedger_Tick_ResolvesClientsInAscendingIdOrder()
        {
            FishingHostLedger l = NewLedger();
            l.EnsureSlot(1);
            l.EnsureSlot(2);
            Accept(l, 1, Ov, 300, 2);
            Accept(l, 1, Ov, 100, 0);
            Accept(l, 1, Ov, 200, 1);
            List<CommitDecision> late = Tick(l, Tf + 2.0 + 31.0);
            Assert.That(late.Count, Is.EqualTo(3));
            Assert.That(late[0].ClientId, Is.EqualTo(100UL));
            Assert.That(late[1].ClientId, Is.EqualTo(200UL));
            Assert.That(late[2].ClientId, Is.EqualTo(300UL));
            Assert.That(late[0].Slot, Is.EqualTo((byte)0));
            Assert.That(late[2].Slot, Is.EqualTo((byte)2));
        }

        // ---- clean-catch bonus (R64, M2-D16) -----------------------------------------------------------------------

        [Test]
        public void FishingHostLedger_CleanCatch_UpgradesQualityOneTier()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, Ov);
            CatchResult rolled = v.Roll.Catch;
            Assert.That(rolled.Quality, Is.Not.EqualTo(CatchQuality.Trophy), "precondition: this seed rolls below Trophy");
            HookAt(l, 1);
            Tick(l, 10.0);
            CommitDecision d = Result(l, 1, FightOutcome.Landed, EscapeReason.None, Clean(9.5f));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(d.CleanBonus, Is.True);
            Assert.That(d.Roll.Catch.Quality, Is.EqualTo(CatchMath.UpgradeOneTier(rolled.Quality)));
            Assert.That(d.Roll.Catch.ValueCoins, Is.EqualTo(CatchMath.Value(12, v.Roll.WeightT, d.Roll.Catch.Quality)));
            Assert.That(d.Roll.Catch.ValueCoins, Is.GreaterThan(rolled.ValueCoins - 1));
            Assert.That(d.Roll.Catch.WeightG, Is.EqualTo(rolled.WeightG));
            Assert.That(d.Roll.Catch.LengthMm, Is.EqualTo(rolled.LengthMm));
            Assert.That(d.Roll.Catch.ItemIndex, Is.EqualTo(rolled.ItemIndex));
            Assert.That(d.Roll.WeightT, Is.EqualTo(v.Roll.WeightT));
            Assert.That(d.AddToCreel, Is.True);
            Assert.That(d.Announce, Is.EqualTo(AnnounceKind.Landed));
            AssertSameCatch(in d.Roll.Catch, l.CatchLog[0]);
        }

        [Test]
        public void FishingHostLedger_CleanCatchAtTrophy_StaysTrophy()
        {
            FishingDatabase db = Db();
            FishingHostLedger found = null;
            CastVerdict v = default;
            for (ulong seed = 1; seed <= 5000 && found == null; seed++)
            {
                FishingHostLedger l = NewLedger(seed, db);
                CastVerdict cand = Accept(l, 1, Ov);
                if (cand.Roll.Catch.Quality == CatchQuality.Trophy) { found = l; v = cand; }
            }
            if (found == null) { Assert.Fail("no run seed 1..5000 rolled a Trophy carp on its first cast"); return; }
            HookAt(found, 1);
            Tick(found, 10.0);
            CommitDecision d = Result(found, 1, FightOutcome.Landed, EscapeReason.None, Clean(9.5f));
            Assert.That(d.CleanBonus, Is.True);
            Assert.That(d.Roll.Catch.Quality, Is.EqualTo(CatchQuality.Trophy));
            Assert.That(d.Roll.Catch.ValueCoins, Is.EqualTo(v.Roll.Catch.ValueCoins), "value unchanged at the cap");
            Assert.That(d.Roll.Catch.WeightG, Is.EqualTo(v.Roll.Catch.WeightG));
        }

        [Test]
        public void FishingHostLedger_ImplausibleCleanClaim_KeepsRolledQuality()
        {
            FishingHostLedger l = NewLedger();
            // host clock 10 s after the hook: the claim may not exceed 12 s (slack 2) and may not undercut the 4.7 s floor
            float[] claims = { 12.5f, 4.0f };
            uint id = 0;
            foreach (float claim in claims)
            {
                id++;
                CastVerdict v = Accept(l, id, Ov);
                HookAt(l, id);
                Tick(l, 10.0);
                CommitDecision d = Result(l, id, FightOutcome.Landed, EscapeReason.None, Clean(claim));
                Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed), "claim " + claim + " still lands");
                Assert.That(d.AddToCreel, Is.True, "claim " + claim);
                Assert.That(d.CleanBonus, Is.False, "claim " + claim);
                AssertSameCatch(in v.Roll.Catch, in d.Roll.Catch);
            }
            id++;
            CastVerdict control = Accept(l, id, Ov);
            HookAt(l, id);
            Tick(l, 10.0);
            CommitDecision ok = Result(l, id, FightOutcome.Landed, EscapeReason.None, Clean(11.9f));
            Assert.That(ok.CleanBonus, Is.True, "control: 11.9 s is inside the bounds");
            Assert.That(ok.Roll.Catch.Quality, Is.EqualTo(CatchMath.UpgradeOneTier(control.Roll.Catch.Quality)));
        }

        [Test]
        public void FishingHostLedger_SelfContradictoryCleanClaim_KeepsRolledQuality()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, Ov);
            HookAt(l, 1);
            Tick(l, 10.0);
            CommitDecision d = Result(l, 1, FightOutcome.Landed, EscapeReason.None, Clean(9.5f, recoveries: 1));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(d.CleanBonus, Is.False);
            Assert.That(d.AddToCreel, Is.True);
            AssertSameCatch(in v.Roll.Catch, in d.Roll.Catch);
        }

        [Test]
        public void FishingHostLedger_NonCleanCatch_KeepsRolledQuality()
        {
            FishingHostLedger l = NewLedger();
            uint id = 0;
            foreach (float mt in new[] { 0.8f, 0.85f, float.NaN, float.PositiveInfinity, -0.1f })
            {
                id++;
                CastVerdict v = Accept(l, id, Ov);
                HookAt(l, id);
                Tick(l, 10.0);
                CommitDecision d = Result(l, id, FightOutcome.Landed, EscapeReason.None, Clean(9.5f, 0, mt));
                Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed), "max tension " + mt);
                Assert.That(d.CleanBonus, Is.False, "max tension " + mt);
                AssertSameCatch(in v.Roll.Catch, in d.Roll.Catch);
            }
        }

        [Test]
        public void FishingHostLedger_JunkCleanClaim_NoBonus()
        {
            FishingHostLedger l = NewLedger();
            CastVerdict v = Accept(l, 1, OvJunk);
            HookAt(l, 1);
            Tick(l, 1.5);
            CommitDecision d = Result(l, 1, FightOutcome.Landed, EscapeReason.None, Clean(1.5f));
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(d.CleanBonus, Is.False);
            Assert.That(d.Roll.Catch.IsJunk, Is.True);
            Assert.That(d.Roll.Catch.ValueCoins, Is.EqualTo(2));
            AssertSameCatch(in v.Roll.Catch, in d.Roll.Catch);
        }

        // ---- bad-luck nudge (R65, M2-D23) --------------------------------------------------------------------------

        [Test]
        public void FishingHostLedger_ThreeFailures_NextCastPity()
        {
            FishingDatabase db = Db();   // PityAfterFailures 3
            FishingHostLedger l = NewLedger(Seed, db);
            Assert.That(CastAndFail(l, 1, 0).Roll.Pity, Is.False, "cast 1");
            Assert.That(CastAndFail(l, 2, 1).Roll.Pity, Is.False, "cast 2");
            Assert.That(CastAndFail(l, 3, 2).Roll.Pity, Is.False, "cast 3");

            CastVerdict four = Accept(l, 4, Ov);
            Assert.That(four.Roll.Pity, Is.True, "cast 4 follows three failures");
            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            Assert.That(a.Roll.Pity, Is.True, "ActiveCast.Roll carries Pity");
            FishingRoll want = Expected(db, Seed, 4, true);
            want.Pity = true;
            AssertSameRoll(in want, in four.Roll, true, "Pity must not change any other roll field (no extra draw)");

            HookAt(l, 4);
            CommitDecision d4 = Result(l, 4, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));
            Assert.That(d4.Roll.Pity, Is.True, "the decision carries the Pity roll");
            CastVerdict five = Accept(l, 5, Ov);
            Assert.That(five.Roll.Pity, Is.False, "a resolved Pity cast resets the streak whatever its outcome");
            CancelCast(l, 5);

            // PityAfterFailures 0 = off
            FishingHostLedger off = NewLedger(Seed, DbPity(0));
            for (uint id = 1; id <= 8; id++)
                Assert.That(CastAndFail(off, id, 1).Roll.Pity, Is.False, "off: cast " + id);

            // A different threshold moves the trigger.
            FishingHostLedger two = NewLedger(Seed, DbPity(2));
            Assert.That(CastAndFail(two, 1, 1).Roll.Pity, Is.False);
            Assert.That(CastAndFail(two, 2, 1).Roll.Pity, Is.False);
            Assert.That(Accept(two, 3, Ov).Roll.Pity, Is.True, "threshold 2: the third cast is Pity");
        }

        [Test]
        public void FishingHostLedger_Landed_ResetsPityCount()
        {
            FishingHostLedger l = NewLedger();
            CastAndFail(l, 1, 0);
            CastAndFail(l, 2, 1);
            CastAndLand(l, 3);                                   // streak back to 0
            CastAndFail(l, 4, 0);
            CastAndFail(l, 5, 1);                                // streak 2
            CastVerdict six = Accept(l, 6, Ov);
            Assert.That(six.Roll.Pity, Is.False, "two failures after the Landed: not Pity yet");
            HookAt(l, 6);
            Result(l, 6, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));   // streak 3
            Assert.That(Accept(l, 7, Ov).Roll.Pity, Is.True, "a third failure after the reset makes the next cast Pity");
        }

        [Test]
        public void FishingHostLedger_CancelDoesNotCountTowardPity()
        {
            FishingHostLedger l = NewLedger();
            CastAndFail(l, 1, 0);
            CastAndFail(l, 2, 1);                                // streak 2
            for (uint id = 3; id <= 5; id++)
            {
                CastVerdict v = Accept(l, id, Ov);
                Assert.That(v.Roll.Pity, Is.False, "cast " + id);
                CommitDecision c = CancelCast(l, id);
                Assert.That(c.Outcome, Is.EqualTo(FightOutcome.Cancelled));
                Assert.That(c.Refunded, Is.True);
            }
            Assert.That(Accept(l, 6, Ov).Roll.Pity, Is.False, "three cancels did not count: streak is still 2");
            List<CommitDecision> timedOut = Tick(l, Tf + 2.0 + 31.0);   // stuck-Waiting refund is a Cancelled resolution
            Assert.That(timedOut.Count, Is.EqualTo(1));
            Assert.That(timedOut[0].Outcome, Is.EqualTo(FightOutcome.Cancelled));
            CastVerdict seven = Accept(l, 7, Ov);
            Assert.That(seven.Roll.Pity, Is.False, "the timeout refund did not count either");
            HookAt(l, 7);
            Result(l, 7, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));    // third real failure

            CastVerdict eight = Accept(l, 8, Ov);
            Assert.That(eight.Roll.Pity, Is.True, "a third real failure makes the next cast Pity");
            CancelCast(l, 8);                                    // a cancelled Pity cast changes nothing
            CastVerdict nine = Accept(l, 9, Ov);
            Assert.That(nine.Roll.Pity, Is.True, "the cancelled Pity cast leaves the next cast Pity again");

            // Disconnect (cast 9 still active) and rejoin keep the slot streak.
            Assert.That(l.Disconnect(Client), Is.True);
            CastVerdict rejoin = Accept(l, 1, Ov, 200);
            Assert.That(rejoin.Roll.Pity, Is.True, "the streak belongs to the slot, not the connection");
            Result(l, 1, FightOutcome.Escaped, EscapeReason.Missed, NotClean(0f), 200);   // the Pity cast resolves: streak 0
            Assert.That(Accept(l, 2, Ov, 200).Roll.Pity, Is.False, "a resolved Pity cast resets the streak");
        }

        [Test]
        public void FishingHostLedger_OrphansAndEarlyHooksAndDisconnect_PityAccounting()
        {
            FishingHostLedger l = NewLedger();
            CastAndFail(l, 1, 0);
            CastAndFail(l, 2, 0);                                // streak 2
            Accept(l, 3, Ov);                                    // early hook: Escaped/Invalid is a failure
            Tick(l, 0.1);
            Assert.That(l.Hooked(Client, 3, HookDistance, out CommitDecision early), Is.True);
            Assert.That(early.Reason, Is.EqualTo(EscapeReason.Invalid));
            CommitDecision orphan = CancelCast(l, 3);            // orphans never count
            Assert.That(orphan.Accepted, Is.False);
            CommitDecision orphan2 = Result(l, 3, FightOutcome.Snapped, EscapeReason.None, NotClean(1f));
            Assert.That(orphan2.Accepted, Is.False);
            Assert.That(Accept(l, 4, Ov).Roll.Pity, Is.True, "streak 3 from two snaps plus the early-hook failure; orphans added nothing");
        }

        // ---- T9 review follow-up: non-finite input, walk-bounded line, dt guard, pity rows, other rods ---------------------

        // Catches: ValidateCast / TryComputeLanding trusting a NaN or infinite yaw (CastMath.Landing would give a NaN landing and
        // the host would store an unreachable bobber); a yaw check placed after the bait is spent; a reject that still consumes bait.
        [Test]
        public void FishingHostLedger_NonFiniteYaw_RejectedOriginTooFarAndNoStateChange()
        {
            foreach (float yaw in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                FishingHostLedger l = NewLedger();
                CastRequestData bad = Req(1);
                bad.YawDeg = yaw;
                RollOverrides ov = Ov;
                CastVerdict v = l.RequestCast(Client, 0, bad, View(), in ov);
                Assert.That(v.Valid, Is.False, "yaw " + yaw);
                Assert.That(v.Reason, Is.EqualTo(CastRejectReason.OriginTooFar), "yaw " + yaw);
                Assert.That(Stock(l), Is.EqualTo(20), "a rejected cast spends no bait, yaw " + yaw);
                Assert.That(l.HostCastCounter, Is.EqualTo(0u));
                Assert.That(l.TryGetActive(Client, out _), Is.False);
                Assert.That(Accept(l, 1, Ov).Valid, Is.True, "the same cast id is still usable after the reject");
            }
        }

        // Catches: TryComputeLanding returning true (and a NaN landing) for a non-finite yaw.
        [Test]
        public void FishingHostLedger_TryComputeLanding_NonFiniteYaw_ReturnsFalse()
        {
            FishingHostLedger l = NewLedger();
            foreach (float yaw in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                CastRequestData r = Req(1);
                r.YawDeg = yaw;
                Assert.That(l.TryComputeLanding(0, r, out float x, out float z), Is.False, "yaw " + yaw);
                Assert.That(x, Is.EqualTo(0f));
                Assert.That(z, Is.EqualTo(0f));
            }
            CastRequestData ok = Req(1);
            ok.YawDeg = 90f;
            Assert.That(l.TryComputeLanding(0, ok, out float ox, out _), Is.True, "a finite yaw still works");
            Assert.That(ox, Is.EqualTo(10.25f).Within(1e-3f));
        }

        // Catches: Hooked feeding a non-finite host distance to HostLineLength0 (NaN -> landDistance gives a zero landing floor,
        // an anti-cheat hole), or accepting the hook. On-time hook, so the early-hook rule cannot be what rejects it.
        [Test]
        public void FishingHostLedger_HookedWithNonFiniteHostDistance_FailsClosedEscapedInvalid()
        {
            foreach (float dist in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                FishingHostLedger l = NewLedger();
                CastVerdict v = Accept(l, 1, Ov);
                Assert.That(Tick(l, Tf + 2.0), Is.Empty);
                bool commit = l.Hooked(Client, 1, dist, out CommitDecision d);
                Assert.That(commit, Is.True, "distance " + dist);
                Assert.That(d.Accepted, Is.True);
                Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Escaped));
                Assert.That(d.Reason, Is.EqualTo(EscapeReason.Invalid));
                Assert.That(d.Announce, Is.EqualTo(AnnounceKind.None), "same announce as the early-hook invalid path");
                AssertSameRoll(in v.Roll, in d.Roll, true, "decision carries the cast roll");
                Assert.That(l.TryGetActive(Client, out _), Is.False, "the cast is resolved");
                Assert.That(Stock(l), Is.EqualTo(19), "no refund");
                CommitDecision again = Result(l, 1, FightOutcome.Landed, EscapeReason.None, Clean(10f));
                Assert.That(again.Accepted, Is.False, "an orphan now");
                Assert.That(again.Outcome, Is.EqualTo(FightOutcome.Escaped));
                Assert.That(again.Reason, Is.EqualTo(EscapeReason.Invalid), "the stored resolution is repeated");
            }
        }

        static void AcceptFullCharge(FishingHostLedger l, uint id)
        {
            RollOverrides ov = Ov;
            CastVerdict v = l.RequestCast(Client, 0, Req(id, 1f), View(), in ov);
            Assert.That(v.Valid, Is.True, "cast " + id + " rejected: " + v.Reason);
        }

        // The anti-teleport bound (T9-R1). Carbon rod (cast 24 m, reel 1.2), full charge, 2 s bite: the player hooks at the earliest
        // allowed instant (bite - tolerance) claiming to stand beside the bobber. Without the bound the line is 2.0 and the landing
        // floor is 0 s; with it the line is cast - sprint * (hooked - accepted) - HookedLineSlack.
        // Catches: LineLength0 = HostLineLength0 only (no walk bound); the bound using the wrong elapsed time (e.g. Now alone);
        // a sign slip; the bound not floored; the floor not following the bounded length.
        [Test]
        public void FishingHostLedger_TeleportToBobberAfterCast_LandingFloorUsesWalkBoundedLine()
        {
            FishingHostLedger l = NewLedger();
            Assert.That(l.SelectRod(Client, 0, FishingTestData.RodCarbon), Is.True);
            AcceptFullCharge(l, 1);
            Assert.That(l.TryGetActive(Client, out ActiveCast fresh), Is.True);
            float cast = 24f;
            float tf = CastMath.FlightTime(cast);
            Assert.That(fresh.TFlight, Is.EqualTo(tf).Within(1e-5f));
            Assert.That(fresh.CastDistance, Is.EqualTo(cast).Within(1e-4f), "the cast distance is stored at accept");
            double elapsed = tf + 1.3;                       // bite is at tf + 2.0, tolerance 0.75: tf + 1.25 is the earliest hook
            Tick(l, tf + 1.3);
            Assert.That(l.Hooked(Client, 1, 2f, out CommitDecision early), Is.False, "hook accepted: " + early.Reason);   // host measured 2 m: "teleported"
            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            float expected = cast - MovementMath.SprintSpeed * (float)elapsed - FishingConstants.HookedLineSlack;
            Assert.That(expected, Is.GreaterThan(5f), "test sanity: the bound must matter here");
            Assert.That(a.LineLength0, Is.EqualTo(expected).Within(1e-3f));

            float floor = 0.8f * (expected - 2f) / 1.2f - 0.5f;
            Assert.That(floor, Is.GreaterThan(1.5f), "test sanity: a real landing floor");
            Tick(l, floor - 0.05);
            CommitDecision tooFast = Result(l, 1, FightOutcome.Landed, EscapeReason.None, NotClean((float)floor - 0.05f));
            Assert.That(tooFast.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(tooFast.Reason, Is.EqualTo(EscapeReason.Invalid), "landing at the teleport-short floor is refused");

            FishingHostLedger m = NewLedger();
            m.SelectRod(Client, 0, FishingTestData.RodCarbon);
            AcceptFullCharge(m, 1);
            Tick(m, tf + 1.3);
            m.Hooked(Client, 1, 2f, out _);
            Tick(m, floor + 0.05);
            CommitDecision ok = Result(m, 1, FightOutcome.Landed, EscapeReason.None, NotClean((float)floor + 0.05f));
            Assert.That(ok.Outcome, Is.EqualTo(FightOutcome.Landed), "just past the bounded floor lands");
        }

        // Catches: the walk bound overriding a LONGER honest host line (it must only raise the length).
        [Test]
        public void FishingHostLedger_HonestFarPlayer_KeepsHostLineLength()
        {
            FishingHostLedger l = NewLedger();
            l.SelectRod(Client, 0, FishingTestData.RodCarbon);
            AcceptFullCharge(l, 1);
            Tick(l, CastMath.FlightTime(24f) + 1.3);
            l.Hooked(Client, 1, 40f, out _);   // host says 40 m from the landing: longer than any walk bound
            l.TryGetActive(Client, out ActiveCast a);
            Assert.That(a.LineLength0, Is.EqualTo(40f - FishingConstants.HookedLineSlack).Within(1e-4f));
        }

        // Catches: the walk bound lowering the line when it is negative (late hook), or the line dipping below the landing distance.
        [Test]
        public void FishingHostLedger_LateHook_WalkBoundIsInactiveAndLineFloorsAtLandDistance()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);                       // 10.25 m cast, hook at tf + 2.0: the bound is negative
            HookAt(l, 1);                           // host distance HookDistance -> 8.5
            l.TryGetActive(Client, out ActiveCast a);
            Assert.That(a.LineLength0, Is.EqualTo(8.5f).Within(1e-5f), "walk bound below the host line changes nothing");

            FishingHostLedger m = NewLedger();
            Accept(m, 1, Ov);
            Tick(m, Tf + 2.0);
            m.Hooked(Client, 1, 0f, out _);         // standing on the bobber, bound negative: floors at the landing distance
            m.TryGetActive(Client, out ActiveCast b);
            Assert.That(b.LineLength0, Is.EqualTo(2f), "floored at landDistance");
        }

        // Catches: a ledger that ignores the selected rod's reel speed in the landing floor (default rod, rod 0, or the cast's
        // first rod). Twig rod: reel 0.9, cast 14 m (8.25 m at 0.5 charge), so the floor is 0.8 x 6.5 / 0.9 - 0.5 = 5.278 s;
        // the Handmade floor (4.7 s) and the Carbon floor (3.83 s) would both accept 5.2 s.
        [Test]
        public void FishingHostLedger_LandedFloor_UsesTheSelectedRodReelSpeed()
        {
            float floor = CatchValidator.LandedFloorSeconds(8.5f, 2f, 0.9f);
            Assert.That(floor, Is.EqualTo(5.2778f).Within(1e-3f));
            FishingHostLedger l = NewLedger();
            Assert.That(l.SelectRod(Client, 0, FishingTestData.RodTwig), Is.True);
            Accept(l, 1, Ov);
            HookAt(l, 1);
            l.TryGetActive(Client, out ActiveCast a);
            Assert.That(a.LineLength0, Is.EqualTo(8.5f).Within(1e-5f));
            Tick(l, floor - 0.08);
            CommitDecision early = Result(l, 1, FightOutcome.Landed, EscapeReason.None, NotClean(5.2f));
            Assert.That(early.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(early.Reason, Is.EqualTo(EscapeReason.Invalid), "5.2 s is under the twig floor");

            Accept(l, 2, Ov);
            HookAt(l, 2);
            Tick(l, floor + 0.05);
            CommitDecision ok = Result(l, 2, FightOutcome.Landed, EscapeReason.None, NotClean(5.33f));
            Assert.That(ok.Outcome, Is.EqualTo(FightOutcome.Landed), "just over the twig floor");
        }

        // Catches: a Tick that accepts NaN / negative / infinite dt (the clock goes NaN, backwards, or jumps to infinity and
        // every active cast times out at once).
        [Test]
        public void FishingHostLedger_Tick_IgnoresNanNegativeAndInfiniteDt()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            Tick(l, 1.0);
            foreach (double bad in new[] { double.NaN, -0.5, double.PositiveInfinity, double.NegativeInfinity, 0.0 })
            {
                List<CommitDecision> sink = Tick(l, bad);
                Assert.That(l.Now, Is.EqualTo(1.0), "dt " + bad);
                Assert.That(sink, Is.Empty, "dt " + bad);
                Assert.That(l.TryGetActive(Client, out _), Is.True, "dt " + bad);
            }
            Tick(l, 0.25);
            Assert.That(l.Now, Is.EqualTo(1.25), "a normal dt still advances the clock");
        }

        // Catches: pity accounting that only sees Snapped / Result escapes: the host-resolved Timeout (Tick) and Hooked-Cancel
        // (Dropped) paths must count as failures too (R65).
        [Test]
        public void FishingHostLedger_TimeoutAndDroppedResolutions_CountTowardPity()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            HookAt(l, 1);
            List<CommitDecision> timeout = Tick(l, 240.1);
            Assert.That(timeout.Count, Is.EqualTo(1));
            Assert.That(timeout[0].Reason, Is.EqualTo(EscapeReason.Timeout));

            Accept(l, 2, Ov);
            HookAt(l, 2);
            CommitDecision dropped = CancelCast(l, 2);
            Assert.That(dropped.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(dropped.Reason, Is.EqualTo(EscapeReason.Dropped));

            CastVerdict three = Accept(l, 3, Ov);
            Assert.That(three.Roll.Pity, Is.False, "two failures so far");
            HookAt(l, 3);
            Assert.That(Tick(l, 240.1).Count, Is.EqualTo(1));       // third failure, again through Tick

            Assert.That(Accept(l, 4, Ov).Roll.Pity, Is.True, "Timeout + Dropped + Timeout = three failures");

            FishingHostLedger m = NewLedger();
            Accept(m, 1, Ov); HookAt(m, 1); CancelCast(m, 1);
            Accept(m, 2, Ov); HookAt(m, 2); CancelCast(m, 2);
            Accept(m, 3, Ov); HookAt(m, 3); CancelCast(m, 3);
            Assert.That(Accept(m, 4, Ov).Roll.Pity, Is.True, "three Dropped resolutions alone make the next cast Pity");
        }

        // ---- slot takeover reset -------------------------------------------------------------------------------------

        // Catches: a slot taken over by a different identity inheriting the previous occupant's rod, selected bait, bait stock or bad-luck
        // streak (pity); a reset that restores only some of them.
        [Test]
        public void FishingHostLedger_ResetSlot_RestoresFreshLoadoutAndClearsPityStreak()
        {
            FishingHostLedger l = NewLedger();
            CastAndFail(l, 1, 0); CastAndFail(l, 2, 1); CastAndFail(l, 3, 2);   // stock 17, streak 3
            Assert.That(Stock(l), Is.EqualTo(17));
            Assert.That(l.SelectRod(Client, 0, FishingTestData.RodTwig), Is.True);
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitGrub), Is.True);
            Assert.That(l.GetLoadout(0).RodIndex, Is.EqualTo((ushort)FishingTestData.RodTwig), "precondition");
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitWorm), Is.True);

            Assert.That(l.ResetSlot(0), Is.True);

            PlayerLoadout lo = l.GetLoadout(0);
            Assert.That(lo.RodIndex, Is.EqualTo((ushort)FishingTestData.RodHandmade), "default rod");
            Assert.That(lo.BaitIndex, Is.EqualTo((byte)FishingTestData.BaitWorm), "default bait");
            Assert.That(lo.BaitCount, Is.EqualTo(FishingConstants.DebugBaitStock), "worm stock back to the starting loadout");
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitGrub), Is.True);
            Assert.That(Stock(l), Is.EqualTo(FishingConstants.DebugBaitStock), "every bait is back at the starting stock");
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitBareHook), Is.True);
            Assert.That(Stock(l), Is.EqualTo(FishingConstants.UnlimitedBaitCount), "the unlimited bait is still uncounted");
            Assert.That(l.SelectBait(Client, 0, (byte)FishingTestData.BaitWorm), Is.True);

            CastVerdict four = Accept(l, 4, Ov);
            Assert.That(four.Roll.Pity, Is.False, "the previous occupant's three failures do not make the newcomer's first cast Pity");
            Assert.That(l.TryGetActive(Client, out ActiveCast a), Is.True);
            Assert.That(a.RodIndex, Is.EqualTo((ushort)FishingTestData.RodHandmade));
        }

        // Catches: a reset of one slot touching another slot's loadout or pity streak, or cancelling another client's ActiveCast, or
        // resetting the whole ledger instead of one slot.
        [Test]
        public void FishingHostLedger_ResetSlot_LeavesOtherSlotsAndActiveCastsAlone()
        {
            const ulong Other = 200;
            FishingHostLedger l = NewLedger();
            l.EnsureSlot(1);
            CastAndFail(l, 1, 0, Other, 1); CastAndFail(l, 2, 1, Other, 1); CastAndFail(l, 3, 2, Other, 1);   // slot 1: streak 3, stock 17
            Assert.That(l.SelectRod(Other, 1, FishingTestData.RodTwig), Is.True);
            CastAndFail(l, 1, 0); CastAndFail(l, 2, 1);                                                       // slot 0: streak 2, stock 18
            CastVerdict active = Accept(l, 3, Ov);                                                            // client 100 mid-cast, stock 17
            Assert.That(active.Roll.Pity, Is.False);

            Assert.That(l.ResetSlot(1), Is.True);

            Assert.That(l.GetLoadout(1).RodIndex, Is.EqualTo((ushort)FishingTestData.RodHandmade), "slot 1 reset");
            Assert.That(Stock(l, 1), Is.EqualTo(FishingConstants.DebugBaitStock), "slot 1 reset");
            Assert.That(Stock(l, 0), Is.EqualTo(17), "slot 0 stock untouched");
            Assert.That(l.TryGetActive(Client, out ActiveCast still), Is.True, "another client's ActiveCast survives");
            Assert.That(still.CastId, Is.EqualTo(3u));
            HookAt(l, 3);
            CommitDecision d = Result(l, 3, FightOutcome.Snapped, EscapeReason.None, NotClean(3f));
            Assert.That(d.Accepted, Is.True, "the surviving cast still resolves normally");
            Assert.That(d.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(Accept(l, 4, Ov).Roll.Pity, Is.True, "slot 0 streak (2 + the snap) is intact, so its next cast is Pity");
            Assert.That(Accept(l, 4, Ov, Other, 1).Roll.Pity, Is.False, "slot 1 streak was cleared");
        }

        // Catches: ResetSlot creating a slot that was never ensured (which would defeat the UnknownPlayer rule B-Q10) or throwing on a bad index.
        [Test]
        public void FishingHostLedger_ResetSlot_UnknownOrOutOfRangeSlot_ReturnsFalseAndCreatesNothing()
        {
            FishingHostLedger l = NewLedger();
            Assert.That(l.ResetSlot(5), Is.False);
            Assert.That(l.ResetSlot(-1), Is.False);
            Assert.That(l.ResetSlot(256), Is.False);
            Assert.That(l.ResetSlot(int.MinValue), Is.False);
            Assert.That(Request(l, 1, Ov, 300, 5).Reason, Is.EqualTo(CastRejectReason.UnknownPlayer), "slot 5 is still never-ensured");
            Assert.That(l.ResetSlot(0), Is.True, "a known slot is reset");
        }

        // ---- pause-aware clock extras ------------------------------------------------------------------------------

        [Test]
        public void FishingHostLedger_PausedTick_DoesNotShiftHookWindow()
        {
            FishingHostLedger l = NewLedger();
            Accept(l, 1, Ov);
            Tick(l, Tf + 1.0);
            Tick(l, 500.0, paused: true);
            Tick(l, 1.0);   // total running time Tf + 2.0
            bool commit = l.Hooked(Client, 1, HookDistance, out _);
            Assert.That(commit, Is.False, "paused time does not count toward the bite wait");
        }
    }
}
