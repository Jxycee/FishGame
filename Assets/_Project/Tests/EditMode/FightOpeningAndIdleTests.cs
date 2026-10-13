using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // User playtest 2026-10-13 (batch A): F7 the 30 s idle escape and F1 the opening reel surge (spec 9.12 note).
    // Authority: pure Core maths, owner side (FightSim); the host cases live in FishingHostLedgerTests.
    public class FightOpeningAndIdleTests
    {
        const float Dt = FightSim.Step;

        static FishingTuning Shipped()
        {
            FishingTuning t = FishingTestData.Tuning();   // the idle escape is on (30 s); the surge is off in the shared fixture
            t.ReelOpenMult = 2.5f; t.ReelOpenSeconds = 0.8f;  // the shipped surge (FishingSeedData)
            return t;
        }

        static RodStats Rod(int i) => FishingTestData.Rods()[i];
        static FishFightProfile Profile(int i) => FishingTestData.MotionProfiles()[i];
        static FightInput In(float steer, bool reel) => new FightInput(steer, reel);

        static FightSim Make(int profile, int rod, FishingTuning t, uint seed, float line)
        {
            FishFightProfile p = Profile(profile);
            RodStats r = Rod(rod);
            return new FightSim(in p, in r, in t, 0.5f, seed, line, false, false);
        }

        static FightState Rest(float line, float elapsed = 0f, float tire = 0f, float lateral = 0f)
        {
            return new FightState { LineLength = line, Lateral = lateral, Tire = tire, Phase = FightPhase.Calm, PhaseTimer = 100f, Elapsed = elapsed };
        }

        static int RunUntilEnd(FightSim s, FightInput input, int maxTicks)
        {
            for (int i = 1; i <= maxTicks; i++)
                if (s.Tick(input) != FightOutcome.None) return i;
            return -1;
        }

        // ---- F7: the idle escape -----------------------------------------------------------------------------------------

        // Catches: a fish that never leaves a player who walked away until the 120 s cap; the wrong reason (Timeout would be refused by the
        // host before 118 s); the clock starting late or counting only reel input.
        [Test]
        public void FightSim_NoInput_FishThrowsTheHookAtThirtySeconds()
        {
            FishingTuning t = Shipped();
            FightSim s = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 7, 12f);
            int end = RunUntilEnd(s, In(0f, false), 4000);
            Assert.That(end, Is.EqualTo(1500).Within(1), "30 s of ticks");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
            Assert.That(s.State.Elapsed, Is.EqualTo(30f).Within(0.021f));
        }

        // Catches: an idle clock that is never reset (the escape would fire at 30 s no matter what the player did), or reset only by a reel.
        [Test]
        public void FightSim_InputAtTwentyNineSeconds_ResetsTheIdleClock()
        {
            FishingTuning t = Shipped();
            FightSim s = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 7, 12f);
            for (int i = 0; i < 1450; i++) Assert.That(s.Tick(In(0f, false)), Is.EqualTo(FightOutcome.None), "idle tick " + i);
            Assert.That(s.Tick(In(0f, true)), Is.EqualTo(FightOutcome.None), "one reel tick at 29 s");
            int end = RunUntilEnd(s, In(0f, false), 4000);
            Assert.That(end, Is.EqualTo(1500).Within(1), "a fresh 30 s after the input");
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
            Assert.That(s.State.Elapsed, Is.EqualTo(59.02f).Within(0.05f));
        }

        // Catches: the reset reading the EFFECTIVE steer (the sim drops a steer that pulls the wrong way; at lateral 0 every steer is dropped),
        // so a player pressing steer would still look idle; NaN steer counting as input.
        [Test]
        public void FightSim_RawSteerResetsTheIdleClock_NaNDoesNot()
        {
            FishingTuning t = Shipped();
            FightSim s = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 7, 12f);
            s.ForceState(Rest(12f, lateral: 0f));
            for (int i = 0; i < 1450; i++) s.Tick(In(0f, false));
            s.Tick(In(1f, false));                                   // dropped by the sim at lateral 0, still a press
            Assert.That(s.State.Tire, Is.LessThanOrEqualTo(0f), "the steer was dropped (no tire gain)");
            int end = RunUntilEnd(s, In(0f, false), 4000);
            Assert.That(end, Is.EqualTo(1500).Within(1), "the dropped steer still reset the clock");

            FightSim n = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 7, 12f);
            int nanEnd = RunUntilEnd(n, In(float.NaN, false), 4000);
            Assert.That(nanEnd, Is.EqualTo(1500).Within(1), "NaN steer is no input");
        }

        // Catches: the idle escape using IdleEscapeTime <= 0 as an instant escape (a fixture that never set the field), or ignoring the off switch.
        [Test]
        public void FightSim_IdleEscapeTimeZero_IsOff()
        {
            FishingTuning t = Shipped();
            t.IdleEscapeTime = 0f;
            FightSim s = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 7, 12f);
            Assert.That(RunUntilEnd(s, In(0f, false), 4000), Is.EqualTo(-1), "80 s with no input and the escape off");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.None));
        }

        // Catches: the 120 s overall cap lost once the idle escape exists, or an attended fight still ending at 30 s.
        [Test]
        public void FightSim_AttendedFight_StillTimesOutAtTheOneTwentySecondCap()
        {
            FishingTuning t = Shipped();
            FightSim s = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 7, 12f);
            int end = -1;
            for (int i = 1; i <= 7000 && end < 0; i++)
            {
                bool pulse = i % 1250 == 0;                          // a reel tick every 25 s: never 30 s idle
                if (s.Tick(In(0f, pulse)) != FightOutcome.None) end = i;
            }
            Assert.That(end, Is.EqualTo(6000).Within(1));
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.Timeout));
            Assert.That(s.State.Elapsed, Is.EqualTo(120f).Within(0.021f));
        }

        // Catches: junk picking up the 30 s fish rule (junk keeps SlackEscapeTime 12 s, reel only).
        [Test]
        public void FightSim_Junk_StillThrowsTheHookAtTwelveSeconds()
        {
            FishingTuning t = Shipped();
            RodStats rod = Rod(FishingTestData.RodHandmade);
            FightSim j = FightSim.ForJunk(in rod, in t, 3, 12f);
            int end = RunUntilEnd(j, In(0f, false), 1000);
            Assert.That(end, Is.EqualTo(600).Within(1));
            Assert.That(j.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));

            FightSim steered = FightSim.ForJunk(in rod, in t, 3, 12f);
            int end2 = RunUntilEnd(steered, In(1f, false), 1000);
            Assert.That(end2, Is.EqualTo(600).Within(1), "steer is not a reel for junk");
        }

        // ---- F1: the opening reel surge ------------------------------------------------------------------------------------

        // Catches (F1): reeling that looks halted for the first second. Full reel from the hook-set, every profile on every rod, 120 seeds each,
        // the WORST seed: the line must visibly shorten by 0.2 s and by 0.5 s even when the opening run is a back pull at the wave peak.
        [Test]
        public void FightSim_FullReelFromTheHook_TakesLineWithinTwoTenthsOfASecond()
        {
            FishingTuning t = Shipped();
            const float start = 11.5f;
            float worst02 = float.MaxValue, worst05 = float.MaxValue;
            string where02 = "", where05 = "";
            for (int p = 0; p < 5; p++)
                for (int rod = 0; rod < 4; rod++)
                    for (uint seed = 1; seed <= 120; seed++)
                    {
                        FightSim s = Make(p, rod, t, seed * 2654435761u, start);
                        float at02 = 0f, at05 = 0f;
                        for (int k = 1; k <= 25; k++)
                        {
                            s.Tick(In(0f, true));
                            if (k == 10) at02 = start - s.State.LineLength;
                            if (k == 25) at05 = start - s.State.LineLength;
                        }
                        if (at02 < worst02) { worst02 = at02; where02 = "profile " + p + " rod " + rod + " seed " + seed; }
                        if (at05 < worst05) { worst05 = at05; where05 = "profile " + p + " rod " + rod + " seed " + seed; }
                    }
            Assert.That(worst02, Is.GreaterThanOrEqualTo(0.15f), "line taken by 0.2 s, worst case at " + where02);
            Assert.That(worst05, Is.GreaterThanOrEqualTo(0.2f), "line taken by 0.5 s, worst case at " + where05);
            TestContext.WriteLine("FG F1 worst line taken over 600 opening fights (5 profiles x 4 rods x 120 seeds): 0.2 s = " + worst02.ToString("F2")
                                  + " m (" + where02 + "), 0.5 s = " + worst05.ToString("F2") + " m (" + where05 + ")");
        }

        // Catches: the surge missing at the hook-set, or applied as a PRODUCT with the tire curve (it must be a floor: max(tire, surge)).
        [Test]
        public void FightSim_Surge_IsAFloorUnderTheTireCurve_NotAProduct()
        {
            FishingTuning t = Shipped();
            RodStats rod = Rod(FishingTestData.RodHandmade);
            FishFightProfile prof = Profile(FishingTestData.ProfileDarter);
            float perSecond = rod.ReelSpeed * prof.ReelInMult;

            FightSim open = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 5, 12f);
            open.ForceState(Rest(12f));
            open.Tick(In(0f, true));
            float expectOpen = 1f + (t.ReelOpenMult - 1f) * (1f - Dt / t.ReelOpenSeconds);   // ReelOpenMult -> 1 over ReelOpenSeconds, sampled at the tick's Elapsed
            Assert.That(12f - open.State.LineLength, Is.EqualTo(perSecond * expectOpen * Dt).Within(1e-5f), "first tick");

            FightSim settled = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 5, 12f);
            settled.ForceState(Rest(12f, elapsed: 3f));
            settled.Tick(In(0f, true));
            Assert.That(12f - settled.State.LineLength, Is.EqualTo(perSecond * Dt).Within(1e-5f), "after the surge, tire 0: x1");

            FightSim tired = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 5, 12f);
            tired.ForceState(Rest(12f, elapsed: 0.2f, tire: t.TireReelFull));            // tire curve already x3
            tired.Tick(In(0f, true));
            Assert.That(12f - tired.State.LineLength, Is.EqualTo(perSecond * t.TireReelMaxMult * Dt).Within(1e-5f), "max of the two curves, not their product");
        }

        // Catches (host floor): any honest reel faster than CatchValidator.MaxReelSpeed, which the host's Landed floor divides by. The surge must
        // never push a tick above reelSpeed x reelInMult x tireReelMaxMult.
        [Test]
        public void FightSim_ReelPerTick_NeverExceedsTheHostsMaxReelSpeed()
        {
            FishingTuning t = Shipped();
            for (int p = 0; p < 5; p++)
                for (int rod = 0; rod < 4; rod++)
                    foreach (float elapsed in new[] { 0f, 0.1f, 0.5f, 0.99f, 1.5f, 10f })
                        foreach (float tire in new[] { 0f, 1f, 2.5f, 5f, 12f, 25f })
                        {
                            FightSim s = Make(p, rod, t, 9, 12f);
                            s.ForceState(Rest(12f, elapsed, tire));
                            s.Tick(In(0f, true));
                            float moved = 12f - s.State.LineLength;
                            float cap = CatchValidator.MaxReelSpeed(Rod(rod), Profile(p), in t) * Dt;
                            Assert.That(moved, Is.LessThanOrEqualTo(cap + 1e-5f), "profile " + p + " rod " + rod + " elapsed " + elapsed + " tire " + tire);
                        }
        }

        // Catches: the fish's back pull cancelling the opening reel. During the surge the back pull ramps in with the same clock (movement only:
        // it never touches strain, so the snap times do not move).
        [Test]
        public void FightSim_BackPull_RampsInOverTheSurgeWindow_StrainUntouched()
        {
            FishingTuning on = Shipped();
            FishingTuning off = Shipped();
            off.ReelOpenMult = 1f;
            var state = new FightState
            {
                LineLength = 8f, Lateral = 0.3f, Phase = FightPhase.Burst, BurstDir = 1, Back = true, PhaseTimer = 30f, Elapsed = 0.1f
            };
            FightSim a = Make(FishingTestData.ProfileBulldog, FishingTestData.RodHandmade, on, 5, 8f);
            FightSim b = Make(FishingTestData.ProfileBulldog, FishingTestData.RodHandmade, off, 5, 8f);
            a.ForceState(state);
            b.ForceState(state);
            a.Tick(In(0f, false));
            b.Tick(In(0f, false));
            float pullOn = a.State.LineLength - 8f, pullOff = b.State.LineLength - 8f;
            Assert.That(pullOff, Is.GreaterThan(0f), "the control back pull moves line out");
            float ramp = (0.1f + Dt) / on.ReelOpenSeconds;
            Assert.That(pullOn, Is.EqualTo(pullOff * ramp).Within(1e-6f), "back pull x elapsed / surge window");
            Assert.That(a.State.Strain, Is.EqualTo(b.State.Strain), "strain is identical with the surge on or off");

            state.Elapsed = 5f;
            FightSim c = Make(FishingTestData.ProfileBulldog, FishingTestData.RodHandmade, on, 5, 8f);
            c.ForceState(state);
            c.Tick(In(0f, false));
            FightSim d = Make(FishingTestData.ProfileBulldog, FishingTestData.RodHandmade, off, 5, 8f);
            d.ForceState(state);
            d.Tick(In(0f, false));
            Assert.That(c.State.LineLength, Is.EqualTo(d.State.LineLength), "after the window the fight is unchanged");
        }

        // Catches: the surge switched on by a fixture that leaves the fields at 0 (a Rust hand check would shift), or a surge that changes strain.
        [Test]
        public void FightSim_SurgeOff_WhenMultiplierIsOneOrWindowIsZero()
        {
            FishingTuning baseline = FishingTestData.Tuning();                      // ReelOpenMult 1
            FishingTuning zero = Shipped();
            zero.ReelOpenSeconds = 0f;
            foreach (FishingTuning t in new[] { baseline, zero })
            {
                FightSim s = Make(FishingTestData.ProfileDarter, FishingTestData.RodHandmade, t, 5, 12f);
                s.ForceState(Rest(12f));
                s.Tick(In(0f, true));
                float perSecond = Rod(FishingTestData.RodHandmade).ReelSpeed * Profile(FishingTestData.ProfileDarter).ReelInMult;
                Assert.That(12f - s.State.LineLength, Is.EqualTo(perSecond * Dt).Within(1e-5f));
            }
        }
    }
}
