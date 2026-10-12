using FishGame.Core;
using FishGame.Fishing;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    // T18 pure helpers behind FishingRodController: telemetry recorder, fish point sampler and the context/guard rules.
    // The MonoBehaviour itself is proven by T19's PlayMode tests (C 2.4).
    public class RodControllerHelperTests
    {
        private static FishingRoll Roll(int weightG, bool junk = false, bool pity = false)
        {
            return new FishingRoll { Catch = new CatchResult { IsJunk = junk, ItemIndex = 1, WeightG = weightG }, Pity = pity };
        }

        // Catches: a recorder that reads outcome/roll from the machine AFTER ResetCast (wiped), wrong CastToBite base, clean claim ignoring
        // the threshold, Pity/Assist/CastDistance dropped.
        [Test]
        public void FightRecorder_LandedCast_RecordsCapturedValues()
        {
            var r = new FightRecorder();
            r.OnCastRequested(10f, true);
            r.OnWaiting(Roll(1500, pity: true), 17f);
            r.OnBite(14f);
            r.OnTerminal(FightOutcome.Landed, EscapeReason.None, 0.5f, 8f, 2);

            Assert.That(r.ReachedWaiting, Is.True);
            Assert.That(r.TryFinish(23f, "steady", "bluegill", "rod_handmade", 0.8f, out FightEnded e), Is.True);
            Assert.That(e.ProfileId, Is.EqualTo("steady"));
            Assert.That(e.ItemId, Is.EqualTo("bluegill"));
            Assert.That(e.RodId, Is.EqualTo("rod_handmade"));
            Assert.That(e.WeightG, Is.EqualTo(1500));
            Assert.That(e.CastDistance, Is.EqualTo(17f));
            Assert.That(e.CastToBite, Is.EqualTo(4f).Within(1e-4f), "bite clock minus release clock");
            Assert.That(e.FightSeconds, Is.EqualTo(8f));
            Assert.That(e.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(e.NearSnapRecoveries, Is.EqualTo(2));
            Assert.That(e.MaxTension, Is.EqualTo(0.5f));
            Assert.That(e.Assist, Is.True);
            Assert.That(e.CleanClaimed, Is.True, "landed fish, 0.5 < 0.8");
            Assert.That(e.Pity, Is.True);
            Assert.That(r.ReachedWaiting, Is.False, "finishing clears the record");
        }

        // Catches: a recall (Waiting/Bite -> Idle in one machine call, Outcome already wiped to None) recorded as outcome None, or CastToBite
        // using the bite clock when no bite happened.
        [Test]
        public void FightRecorder_CancelledBeforeBite_IsRecordedAsCancelled_WithRollKept()
        {
            var r = new FightRecorder();
            r.OnCastRequested(10f, false);
            r.OnWaiting(Roll(900), 12f);

            Assert.That(r.TryFinish(15f, "steady", "perch", "rod_handmade", 0.8f, out FightEnded e), Is.True);
            Assert.That(e.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(e.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(e.CastToBite, Is.EqualTo(5f).Within(1e-4f), "no bite: end minus release");
            Assert.That(e.WeightG, Is.EqualTo(900), "the roll captured on entering Waiting survives the wipe");
            Assert.That(e.FightSeconds, Is.EqualTo(0f));
            Assert.That(e.CleanClaimed, Is.False);
        }

        // Catches: a cast that never reached Waiting (invalid result, result timeout) emitting a FightEnded, or leaking state into the next cast.
        [Test]
        public void FightRecorder_NeverReachedWaiting_EmitsNothing_AndNextCastIsFresh()
        {
            var r = new FightRecorder();
            r.OnCastRequested(1f, true);
            r.OnTerminal(FightOutcome.Escaped, EscapeReason.Invalid, 0.9f, 3f, 1);   // stale terminal data from a path without a wait
            Assert.That(r.TryFinish(5f, "x", "y", "z", 0.8f, out _), Is.False);

            r.OnCastRequested(6f, false);
            r.OnWaiting(Roll(100), 5f);
            Assert.That(r.TryFinish(8f, "p", "i", "r", 0.8f, out FightEnded e), Is.True);
            Assert.That(e.Outcome, Is.EqualTo(FightOutcome.Cancelled), "the earlier terminal capture was cleared");
            Assert.That(e.Assist, Is.False);
            Assert.That(e.MaxTension, Is.EqualTo(0f));
        }

        // Catches: the clean claim granted for junk, for a non-landed outcome, or at/above the threshold (strict <).
        [Test]
        public void FightRecorder_CleanClaim_OnlyForLandedFishBelowThreshold()
        {
            FightEnded Run(FightOutcome o, bool junk, float tension)
            {
                var r = new FightRecorder();
                r.OnCastRequested(0f, false);
                r.OnWaiting(Roll(500, junk), 10f);
                r.OnTerminal(o, EscapeReason.None, tension, 5f, 0);
                r.TryFinish(9f, "p", "i", "r", 0.8f, out FightEnded e);
                return e;
            }

            Assert.That(Run(FightOutcome.Landed, false, 0.79f).CleanClaimed, Is.True);
            Assert.That(Run(FightOutcome.Landed, false, 0.8f).CleanClaimed, Is.False, "strictly below the threshold");
            Assert.That(Run(FightOutcome.Landed, true, 0.1f).CleanClaimed, Is.False, "junk never claims");
            Assert.That(Run(FightOutcome.Snapped, false, 0.1f).CleanClaimed, Is.False);
            Assert.That(Run(FightOutcome.Escaped, false, 0.1f).CleanClaimed, Is.False);
        }

        // Catches: a bite recorded before the cast reached Waiting, or a second bite overwriting the first.
        [Test]
        public void FightRecorder_OnlyFirstBiteCounts_AndOnlyAfterWaiting()
        {
            var r = new FightRecorder();
            r.OnCastRequested(0f, false);
            r.OnBite(1f);   // not waiting yet: ignored
            r.OnWaiting(Roll(100), 5f);
            r.OnBite(4f);
            r.OnBite(9f);
            r.OnTerminal(FightOutcome.Escaped, EscapeReason.Missed, 0f, 0f, 0);
            r.TryFinish(12f, "p", "i", "r", 0.8f, out FightEnded e);
            Assert.That(e.CastToBite, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(e.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(e.Reason, Is.EqualTo(EscapeReason.Missed));
        }

        // Catches: probing every frame (the 0.2 s cadence ignored), a non-water candidate not holding the last water point, a water hit not
        // updating it, and y not being the flat water height.
        [Test]
        public void FishPointSampler_ProbesOnCadence_AndHoldsLastWaterPoint()
        {
            int probes = 0;
            bool water = true;
            var s = new FishPointSampler((x, z) => { probes++; return water; });
            s.Begin(new Vector3(0f, 0f, 10f));

            Vector3 p = s.Tick(0.05f, 0f, 0f, 0f, 0f, 30f, 10f);   // straight ahead 10 m
            Assert.That(p.z, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(p.y, Is.EqualTo(WaterSurface.FlatHeight));
            Assert.That(probes, Is.EqualTo(0), "0.05 s: not due yet");
            s.Tick(0.05f, 0f, 0f, 0f, 0f, 30f, 10f);
            s.Tick(0.05f, 0f, 0f, 0f, 0f, 30f, 10f);
            Assert.That(probes, Is.EqualTo(0));
            s.Tick(0.06f, 0f, 0f, 0f, 0f, 30f, 12f);   // 0.21 s total: due, water -> last water = (0, 12)
            Assert.That(probes, Is.EqualTo(1));

            water = false;
            s.Tick(0.2f, 0f, 0f, 0f, 0f, 30f, 25f);   // probe says land: hold the last water point
            Vector3 held = s.Tick(0.016f, 0f, 0f, 0f, 0f, 30f, 30f);
            Assert.That(probes, Is.EqualTo(2));
            Assert.That(held.z, Is.EqualTo(12f).Within(1e-3f), "land: hold the last water point");

            water = true;
            s.Tick(0.2f, 0f, 0f, 0f, 0f, 30f, 14f);
            Vector3 back = s.Tick(0.016f, 0f, 0f, 0f, 0f, 30f, 15f);
            Assert.That(back.z, Is.EqualTo(15f).Within(1e-3f), "water again: follows the candidate");
        }

        // Catches: a NaN line length / lateral leaking into the fish point (and from there into the NetVar).
        [Test]
        public void FishPointSampler_NonFiniteCandidate_HoldsLastPoint()
        {
            var s = new FishPointSampler((x, z) => true);
            s.Begin(new Vector3(3f, 0f, 4f));
            Vector3 p = s.Tick(0.016f, 0f, 0f, 0f, 0f, 30f, float.NaN);
            Assert.That(RodControllerRules.IsFinite(p), Is.True);
            Assert.That(p, Is.EqualTo(new Vector3(3f, 0f, 4f)));
        }

        // Catches: a wrong row of the C 7.18 context table, a lock outside Bite/Hooked, a stowed rod requesting a context.
        [Test]
        public void RodControllerRules_ContextTable_AndMovementLock()
        {
            Assert.That(RodControllerRules.ContextFor(RodState.Idle, true), Is.EqualTo(InputContext.RodReady));
            foreach (RodState s in new[] { RodState.Readying, RodState.Ready, RodState.Charging })
                Assert.That(RodControllerRules.ContextFor(s, true), Is.EqualTo(InputContext.RodAiming), s.ToString());
            foreach (RodState s in new[] { RodState.Casting, RodState.Waiting, RodState.Landed, RodState.Snapped, RodState.Escaped })
                Assert.That(RodControllerRules.ContextFor(s, true), Is.EqualTo(InputContext.RodWaiting), s.ToString());
            foreach (RodState s in new[] { RodState.Bite, RodState.Hooked })
            {
                Assert.That(RodControllerRules.ContextFor(s, true), Is.EqualTo(InputContext.RodFighting), s.ToString());
                Assert.That(RodControllerRules.LocksMovement(s, true), Is.True, s.ToString());
                Assert.That(RodControllerRules.LocksMovement(s, false), Is.False, "stowed: no lock");
            }

            foreach (RodState s in (RodState[])System.Enum.GetValues(typeof(RodState)))
            {
                Assert.That(RodControllerRules.ContextFor(s, false), Is.EqualTo(InputContext.Free), "stowed " + s);
                if (s != RodState.Bite && s != RodState.Hooked) Assert.That(RodControllerRules.LocksMovement(s, true), Is.False, s.ToString());
            }
        }

        [Test]
        public void RodControllerRules_EquipAllowed_OnlyInFreeReadyAiming()
        {
            Assert.That(RodControllerRules.EquipAllowed(InputContext.Free), Is.True);
            Assert.That(RodControllerRules.EquipAllowed(InputContext.RodReady), Is.True);
            Assert.That(RodControllerRules.EquipAllowed(InputContext.RodAiming), Is.True);
            foreach (InputContext c in new[] { InputContext.RodWaiting, InputContext.RodFighting, InputContext.Swimming, InputContext.Climbing, InputContext.Menu })
                Assert.That(RodControllerRules.EquipAllowed(c), Is.False, c.ToString());
        }

        // Catches: a NaN/Infinity bobber point passing the guard (RodStateData.Equals uses ==, so NaN would resend forever).
        [Test]
        public void RodControllerRules_FiniteOr_RejectsNonFinitePoints()
        {
            var good = new Vector3(1f, 2f, 3f);
            var fallback = new Vector3(4f, 5f, 6f);
            Assert.That(RodControllerRules.FiniteOr(good, fallback), Is.EqualTo(good));
            Assert.That(RodControllerRules.FiniteOr(new Vector3(float.NaN, 0f, 0f), fallback), Is.EqualTo(fallback));
            Assert.That(RodControllerRules.FiniteOr(new Vector3(0f, float.PositiveInfinity, 0f), fallback), Is.EqualTo(fallback));
            Assert.That(RodControllerRules.FiniteOr(new Vector3(0f, 0f, float.NegativeInfinity), fallback), Is.EqualTo(fallback));
            Assert.That(RodControllerRules.FiniteOr(new Vector3(float.NaN, 0f, 0f), new Vector3(float.NaN, 0f, 0f)), Is.EqualTo(Vector3.zero));
        }

        // Catches (T26 carry-forward, F4/F5 select race): the owner keeping its own landing when the host rolled a different one
        // (not adopting), the y taken from the host instead of the flat water height, and the delta measured in 3D or not reported.
        [Test]
        public void RodControllerRules_AdoptHostLanding_TakesHostXz_KeepsFlatHeight_ReportsFlatDelta()
        {
            var owner = new Vector3(10f, WaterSurface.FlatHeight, 20f);
            var host = new Vector3(13f, 7.5f, 24f);   // the host's y is the probe's water y: never copied

            Vector3 adopted = RodControllerRules.AdoptHostLanding(owner, host, WaterSurface.FlatHeight, out float delta);

            Assert.That(adopted.x, Is.EqualTo(13f));
            Assert.That(adopted.z, Is.EqualTo(24f));
            Assert.That(adopted.y, Is.EqualTo(WaterSurface.FlatHeight), "FlatHeight kept");
            Assert.That(delta, Is.EqualTo(5f).Within(1e-4f), "3-4-5 flat delta, y ignored");
        }

        // Catches: a NaN/Infinity host landing leaking into the owner's published bobber point, and a nonzero delta for an identical spot.
        [Test]
        public void RodControllerRules_AdoptHostLanding_IgnoresNonFinite_AndSameSpotHasZeroDelta()
        {
            var owner = new Vector3(1f, WaterSurface.FlatHeight, 2f);
            Assert.That(RodControllerRules.AdoptHostLanding(owner, new Vector3(float.NaN, 0f, 5f), WaterSurface.FlatHeight, out float d1), Is.EqualTo(owner));
            Assert.That(d1, Is.EqualTo(0f));
            Assert.That(RodControllerRules.AdoptHostLanding(owner, new Vector3(3f, 0f, float.PositiveInfinity), WaterSurface.FlatHeight, out _), Is.EqualTo(owner));

            Vector3 same = RodControllerRules.AdoptHostLanding(owner, new Vector3(1f, 9f, 2f), WaterSurface.FlatHeight, out float d2);
            Assert.That(same, Is.EqualTo(owner));
            Assert.That(d2, Is.LessThanOrEqualTo(RodControllerRules.LandingMismatchLogMeters), "same spot: below the log threshold");
        }

        // Catches (T23 review): FlagGrace not set while the snap timer runs (remotes would never hear the crack: their Tension is smoothed),
        // set while it does not, or a flag bit moved/merged (dive without burst, reel/exhausted dropped).
        [Test]
        public void RodControllerRules_HookedFlags_GraceFollowsSnapTimer_AndTheOtherBitsStay()
        {
            Assert.That(RodControllerRules.HookedFlags(FightPhase.Calm, 0, false, false, 0f), Is.EqualTo((byte)0));
            Assert.That(RodControllerRules.HookedFlags(FightPhase.Calm, 0, false, false, 0.01f), Is.EqualTo(RodWireQuant.FlagGrace), "timer running = grace");
            Assert.That(RodControllerRules.HookedFlags(FightPhase.Burst, 1, false, false, 0f), Is.EqualTo(RodWireQuant.FlagBurst), "side run");
            Assert.That(RodControllerRules.HookedFlags(FightPhase.Burst, -1, true, false, 0f), Is.EqualTo((byte)(RodWireQuant.FlagBurst | RodWireQuant.FlagReel)));
            Assert.That(RodControllerRules.HookedFlags(FightPhase.Burst, 0, false, false, 0f), Is.EqualTo((byte)(RodWireQuant.FlagBurst | RodWireQuant.FlagDive)), "dive");
            Assert.That(RodControllerRules.HookedFlags(FightPhase.Calm, 0, true, true, 0f), Is.EqualTo((byte)(RodWireQuant.FlagReel | RodWireQuant.FlagExhausted)));
            byte all = RodControllerRules.HookedFlags(FightPhase.Burst, 0, true, true, 0.2f);
            Assert.That(all, Is.EqualTo((byte)(RodWireQuant.FlagBurst | RodWireQuant.FlagReel | RodWireQuant.FlagDive | RodWireQuant.FlagExhausted | RodWireQuant.FlagGrace)));
            Assert.That(all & RodWireQuant.FlagRecall, Is.EqualTo(0), "recall is set by the caller, never here");
        }
    }
}
