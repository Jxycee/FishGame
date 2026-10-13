using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // R72 Rust-faithful FightSim (plan rework-fight F2; spec 9.5; research note 2026-10-12 section 8.7).
    // Tests 1-29 use the quirk-free Rust profiles (FishingTestData.BaseProfiles); tests 30-37 use the five styles (MotionProfiles).
    // Hand numbers are Rust's: they come from the decompiled server maths, never from this implementation.
    public class FightSimTests
    {
        const float Dt = FightSim.Step;
        const int Small = FishingTestData.ProfileSteady, Medium = FishingTestData.ProfileDarter, Large = FishingTestData.ProfileRunner;

        static FishingTuning Tun() => FishingTestData.Tuning();
        static RodStats Handmade() => FishingTestData.Rods()[FishingTestData.RodHandmade];
        static FishFightProfile P(int i) => FishingTestData.BaseProfiles()[i];
        static FishFightProfile M(int i) => FishingTestData.MotionProfiles()[i];
        static FightInput In(float steer, bool reel) => new FightInput(steer, reel);

        static FightSim Make(FishFightProfile f, float line = 18f, uint seed = 4242, bool assist = false, bool pity = false,
                             RodStats? rod = null, float weightT = 0.5f)
        {
            RodStats r = rod ?? Handmade();
            FishingTuning t = Tun();
            return new FightSim(in f, in r, in t, weightT, seed, line, assist, pity);
        }

        static FightSim MakeJunk(uint seed, float line)
        {
            RodStats rod = Handmade();
            FishingTuning t = Tun();
            return FightSim.ForJunk(in rod, in t, seed, line);
        }

        static FightState RunState(sbyte dir, bool back, float lateral, float line, float strain = 0f, float phaseTimer = 30f, float tire = 0f)
        {
            return new FightState { LineLength = line, Lateral = lateral, Strain = strain, Tire = tire, Phase = FightPhase.Burst,
                                    BurstDir = dir, Back = back, PhaseTimer = phaseTimer };
        }

        static FightState RestState(float line, float strain = 0f, float phaseTimer = 100f, float lateral = 0f, float tire = 0f)
        {
            return new FightState { LineLength = line, Lateral = lateral, Strain = strain, Tire = tire, Phase = FightPhase.Calm, PhaseTimer = phaseTimer };
        }

        // Returns the 1-based tick on which the fight ended, or -1.
        static int RunUntilEnd(FightSim s, FightInput input, int maxTicks)
        {
            for (int i = 1; i <= maxTicks; i++)
                if (s.Tick(input) != FightOutcome.None) return i;
            return -1;
        }

        static bool SameState(in FightState a, in FightState b)
        {
            return a.LineLength == b.LineLength && a.Lateral == b.Lateral && a.Strain == b.Strain && a.Tension == b.Tension && a.Tire == b.Tire
                && a.Phase == b.Phase && a.PhaseTimer == b.PhaseTimer && a.BurstDir == b.BurstDir && a.Back == b.Back
                && a.SnapTimer == b.SnapTimer && a.SlackTimer == b.SlackTimer && a.Elapsed == b.Elapsed
                && a.NearSnapRecoveries == b.NearSnapRecoveries && a.MaxTension == b.MaxTension
                && a.Outcome == b.Outcome && a.Reason == b.Reason;
        }

        // ---- 1-3: research 8.7 snap times -------------------------------------------------------------------------------------------

        // Catches: a wrong strain table entry for reeling into a side run (k 2.2), a missing x StrainMod or x 0.75 scale, a snap threshold other
        // than strain > 7, or a landing/timeout that fires first. The numbers are Rust's (7 / rate), not read from the code.
        [TestCase(Small, 0.8f, 266, 5.303f)]
        [TestCase(Medium, 1.0f, 213, 4.242f)]
        [TestCase(Large, 1.25f, 170, 3.394f)]
        public void FightSim_ReelIntoSideRun_SnapsAtRustTime(int tier, float mod, int snapTick, float seconds)
        {
            FightSim s = Make(P(tier));
            s.ForceState(RunState(1, false, 0.5f, 18f));
            s.Tick(In(0f, true));
            Assert.That(s.State.Strain, Is.EqualTo(2.2f * mod * 0.75f * Dt).Within(1e-6f), "first tick dStrain");
            int end = 1 + RunUntilEnd(s, In(0f, true), 600);
            Assert.That(end, Is.EqualTo(snapTick).Within(1), "snap tick");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(s.State.Elapsed, Is.EqualTo(seconds).Within(0.03f));
        }

        // Catches: reeling into a back pull using the side-run rate (2.2) instead of 2.5, or the back flag not being read by the strain table.
        [TestCase(Small, 0.8f, 234, 4.667f)]
        [TestCase(Medium, 1.0f, 187, 3.733f)]
        [TestCase(Large, 1.25f, 150, 2.987f)]
        public void FightSim_ReelIntoBackPull_SnapsAtRustTime(int tier, float mod, int snapTick, float seconds)
        {
            FightSim s = Make(P(tier));
            s.ForceState(RunState(1, true, 0.5f, 18f));
            s.Tick(In(0f, true));
            Assert.That(s.State.Strain, Is.EqualTo(2.5f * mod * 0.75f * Dt).Within(1e-6f), "first tick dStrain");
            int end = 1 + RunUntilEnd(s, In(0f, true), 600);
            Assert.That(end, Is.EqualTo(snapTick).Within(1), "snap tick");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(s.State.Elapsed, Is.EqualTo(seconds).Within(0.03f));
        }

        // Catches: reeling at rest costing as much as reeling into a run (the old model's "calm reel is free" is also wrong: Rust charges 0.5),
        // and a Landed that fires before the snap (the line is 29.9 m, far from landing).
        [TestCase(Small, 0.8f, 1167, 23.33f)]
        [TestCase(Medium, 1.0f, 934, 18.67f)]
        [TestCase(Large, 1.25f, 747, 14.93f)]
        public void FightSim_ReelWhileResting_SnapsAtRustTime(int tier, float mod, int snapTick, float seconds)
        {
            FightSim s = Make(P(tier));
            s.ForceState(RestState(29.9f));
            s.Tick(In(0f, true));
            Assert.That(s.State.Strain, Is.EqualTo(0.5f * mod * 0.75f * Dt).Within(1e-6f), "first tick dStrain");
            int end = 1 + RunUntilEnd(s, In(0f, true), 1500);
            Assert.That(end, Is.EqualTo(snapTick).Within(1), "snap tick");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Snapped), "snapped, not landed");
            Assert.That(s.State.Elapsed, Is.EqualTo(seconds).Within(0.03f));
        }

        // ---- 4: counter-steer rate --------------------------------------------------------------------------------------------------

        // Catches: a counter-steer charged at the reel rate or at the "other steer" rate (0.5), a counter that is dropped by the steer rule,
        // and tire not rising at 1.25/s. Rust 8.7: time to 7 at this rate is 14.58 / 11.67 / 9.33 s.
        [TestCase(Small, 0.96f)]
        [TestCase(Medium, 1.2f)]
        [TestCase(Large, 1.5f)]
        public void FightSim_CounterSteerOnly_LoadsAtRustRate(int tier, float strainAfter100)
        {
            FightSim s = Make(P(tier));
            s.ForceState(RunState(1, false, 0.5f, 18f));
            for (int i = 0; i < 100; i++)
            {
                s.Tick(In(-1f, false));
                Assert.That(s.State.Lateral, Is.GreaterThan(0f).And.LessThan(1f), "the steer was never dropped (tick " + (i + 1) + ")");
            }
            Assert.That(s.State.Strain, Is.EqualTo(strainAfter100).Within(1e-4f));
            Assert.That(s.State.Tire, Is.EqualTo(2.5f).Within(1e-4f));
        }

        // ---- 5-6: release drain, bar and snap thresholds ----------------------------------------------------------------------------

        // Catches: a drain rate other than 1.5/s (the old 0.45), a drain that reaches below 0, a back pull that loads the line by itself.
        [Test]
        public void FightSim_FullRelease_DrainsOnePointFivePerSecond()
        {
            FightSim a = Make(P(Medium));
            a.ForceState(RestState(10f, strain: 6f));
            for (int i = 0; i < 120; i++) a.Tick(In(0f, false));
            Assert.That(a.State.Strain, Is.EqualTo(2.4f).Within(1e-3f));

            FightSim b = Make(P(Medium));
            b.ForceState(RestState(10f, strain: 5.4f));
            for (int i = 0; i < 120; i++) b.Tick(In(0f, false));
            Assert.That(b.State.Strain, Is.EqualTo(1.8f).Within(1e-3f));
            Assert.That(b.State.Tension, Is.EqualTo(0.3f).Within(1e-3f));

            FightSim c = Make(P(Medium));
            c.ForceState(RestState(10f, strain: 6f));
            for (int i = 0; i < 199; i++) c.Tick(In(0f, false));
            Assert.That(c.State.Strain, Is.EqualTo(0.03f).Within(1e-3f));
            c.Tick(In(0f, false));
            Assert.That(c.State.Strain, Is.LessThanOrEqualTo(1e-5f).And.GreaterThanOrEqualTo(0f));
            c.Tick(In(0f, false));
            Assert.That(c.State.Strain, Is.EqualTo(0f), "no floor below 0 strain");

            FightSim d = Make(P(Medium));
            d.ForceState(RunState(1, true, 0.5f, 10f, strain: 6f));
            for (int i = 0; i < 120; i++) d.Tick(In(0f, false));
            Assert.That(d.State.Strain, Is.EqualTo(2.4f).Within(1e-3f), "a back pull does not load the line by itself");
        }

        // Catches: a bar that is not full at strain 6 (tension = strain / 6), a snap at 6 instead of above 7, a SnapTension other than 7/6,
        // and SnapTimer not counting seconds over the full bar.
        [Test]
        public void FightSim_BarFullAtSix_SnapAboveSeven()
        {
            FightSim a = Make(P(Large));
            a.ForceState(RestState(10f, strain: 6f));
            Assert.That(a.State.Tension, Is.EqualTo(1.0f).Within(1e-6f));
            Assert.That(a.SnapTension, Is.EqualTo(7f / 6f).Within(1e-6f));

            FightSim b = Make(P(Large));
            b.ForceState(RunState(1, false, 0.5f, 10f, strain: 6.95f));
            Assert.That(b.Tick(In(0f, true)), Is.EqualTo(FightOutcome.None));
            Assert.That(b.State.Strain, Is.EqualTo(6.99125f).Within(1e-5f));
            Assert.That(b.State.Tension, Is.GreaterThan(1f));
            Assert.That(b.State.SnapTimer, Is.EqualTo(0.02f).Within(1e-6f));

            FightSim c = Make(P(Large));
            c.ForceState(RunState(1, false, 0.5f, 10f, strain: 6.99f));
            Assert.That(c.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Snapped));
        }

        // ---- 7-11: the fish state machine, draw order and the pull wave -------------------------------------------------------------

        // Catches: the old 0.8 s calm lead-in (Rust starts the first run at once), a constructor that draws more or fewer than one value
        // (the wave phase), a first run that draws a different number of values.
        [Test]
        public void FightSim_FirstTick_StartsARunAtOnce()
        {
            FightSim s = Make(P(Medium));
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm));
            Assert.That(s.State.PhaseTimer, Is.EqualTo(0f));
            Assert.That(s.DrawCount, Is.EqualTo(1), "constructor: the wave phase");
            s.Tick(In(0f, false));
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Burst));
            Assert.That(s.DrawCount, Is.EqualTo(4), "run start: side, back, duration");
            Assert.That(s.State.BurstDir == -1 || s.State.BurstDir == 1, Is.True, "a run always has a side");
            Assert.That(s.State.PhaseTimer + 0.02f, Is.InRange(3f - 1e-5f, 7f + 1e-5f));
        }

        // Catches: run durations that ignore tire (x (1 - tire01)), rest durations that ignore tire (x (1 + tire01)), tire not clamped to 0..1,
        // a run range other than 3-7 s or a rest range other than 2-4 s, and draw counts other than 3 per run start and 1 per rest start.
        [Test]
        public void FightSim_RunAndRestDurations_ScaleWithTire()
        {
            double sumRun = 0;
            for (uint seed = 1; seed <= 200; seed++)
            {
                (float tire, float runLo, float runHi, float restLo, float restHi)[] rows =
                {
                    (0f, 3f, 7f, 2f, 4f), (10f, 1.5f, 3.5f, 3f, 6f), (20f, 0f, 1e-4f, 4f, 8f), (-5f, 3f, 7f, 2f, 4f),
                };
                foreach (var r in rows)
                {
                    FightSim run = Make(P(Medium), seed: seed);
                    run.ForceState(RestState(10f, phaseTimer: 0f, tire: r.tire));
                    int d0 = run.DrawCount;
                    run.Tick(In(0f, false));
                    float runDur = run.State.PhaseTimer + 0.02f;
                    Assert.That(runDur, Is.InRange(r.runLo - 1e-5f, r.runHi + 1e-5f), "run at tire " + r.tire + " seed " + seed);
                    Assert.That(run.DrawCount - d0, Is.EqualTo(3), "run start draws");
                    if (r.tire == 0f) sumRun += runDur;

                    FightSim rest = Make(P(Medium), seed: seed);
                    rest.ForceState(RunState(1, false, 0.5f, 10f, phaseTimer: 0f, tire: r.tire));
                    int d1 = rest.DrawCount;
                    rest.Tick(In(0f, false));
                    Assert.That(rest.State.Phase, Is.EqualTo(FightPhase.Calm));
                    float restDur = rest.State.PhaseTimer + 0.02f;
                    Assert.That(restDur, Is.InRange(r.restLo - 1e-5f, r.restHi + 1e-5f), "rest at tire " + r.tire + " seed " + seed);
                    Assert.That(rest.DrawCount - d1, Is.EqualTo(1), "rest start draws");
                }
            }
            Assert.That(sumRun / 200.0, Is.EqualTo(5.0).Within(0.3), "mean run at tire 0 is the middle of 3-7");
        }

        // Catches: a side split other than 50/50, a back chance other than the profile's DiveChance (0.39 here), a back gate at the wrong
        // distance (castMaxDist - 2 = 16 m: at 16.0 no back pull, at 15.9 one), and the back roll being skipped (its draw is always made).
        [Test]
        public void FightSim_RunSide_FiftyFifty_BackThirtyNinePercentInsideTheGate()
        {
            int left = 0, back = 0, backAtGate = 0, backInside = 0;
            for (uint seed = 1; seed <= 1000; seed++)
            {
                FightSim s = Make(P(Medium), seed: seed);
                s.ForceState(RestState(10f, phaseTimer: 0f));
                s.Tick(In(0f, false));
                if (s.State.BurstDir < 0) left++;
                if (s.State.Back) back++;

                FightSim g = Make(P(Medium), seed: seed);
                g.ForceState(RestState(16.0f, phaseTimer: 0f));
                int d0 = g.DrawCount;
                g.Tick(In(0f, false));
                if (g.State.Back) backAtGate++;
                Assert.That(g.DrawCount - d0, Is.EqualTo(3), "the back roll is drawn even when the gate is closed");

                FightSim h = Make(P(Medium), seed: seed);
                h.ForceState(RestState(15.9f, phaseTimer: 0f));
                h.Tick(In(0f, false));
                if (h.State.Back) backInside++;
            }
            Assert.That(left / 1000.0, Is.EqualTo(0.5).Within(0.05), "left share");
            Assert.That(back / 1000.0, Is.EqualTo(0.39).Within(0.05), "back share");
            Assert.That(backAtGate, Is.EqualTo(0), "no back pull at castMaxDist - 2 m");
            Assert.That(backInside / 1000.0, Is.EqualTo(0.39).Within(0.05), "back share at 15.9 m");
        }

        // Catches: draws made in a different order (side, back, duration, then rest duration), the wave phase not being the first draw, a
        // wrong mapping of a draw onto a range, or the pull wave not following Wave(phase + elapsed).
        [Test]
        public void FightSim_DrawOrder_ReplaysByHand()
        {
            // Seed 77 is the documented replay; the loop over more seeds makes sure a swapped pair of draws cannot hide behind a lucky seed
            // (two swapped coin-flip draws agree by chance on one seed in about two).
            for (uint seed = 77; seed < 77 + 60; seed++)
            {
                var r = new Pcg32(seed);
                float u0 = r.NextFloat(), u1 = r.NextFloat(), u2 = r.NextFloat(), u3 = r.NextFloat(), u4 = r.NextFloat();
                float phase = Pcg32.MapUnitToRange(u0, 0f, 4f);

                FightSim s = Make(P(Medium), line: 10f, seed: seed);
                s.Tick(In(0f, false));
                Assert.That(s.State.BurstDir, Is.EqualTo((sbyte)(u1 < 0.5f ? -1 : 1)), "u1 is the side, seed " + seed);
                Assert.That(s.State.Back, Is.EqualTo(u2 < 0.39f), "u2 is the back roll, seed " + seed);
                Assert.That(s.State.PhaseTimer + 0.02f, Is.EqualTo(Pcg32.MapUnitToRange(u3, 3f, 7f)).Within(1e-5f), "u3 is the run duration, seed " + seed);
                Assert.That(s.PullWave, Is.EqualTo(FightSim.Wave(phase + 0.02f, 4f)).Within(1e-6f), "u0 is the wave phase, seed " + seed);

                FightState st = s.State;
                st.PhaseTimer = 0f;
                s.ForceState(st);
                s.Tick(In(0f, false));
                Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm));
                Assert.That(s.State.PhaseTimer + 0.02f, Is.EqualTo(Pcg32.MapUnitToRange(u4, 2f, 4f)).Within(1e-5f), "u4 is the rest duration, seed " + seed);
            }
        }

        // Catches: a wave that is not Rust's clamp(PingPong(t, 2), 0.2, 2), a period that is not honoured (and no fallback to 4 s for a
        // non-positive period), and a wave mean other than 1.01 (4.04 over a 4 s period, whatever the phase).
        [Test]
        public void FightSim_Wave_IsRustPingPongClamped()
        {
            float[] t = { 0f, 0.1f, 1f, 2f, 3f, 3.9f, 4f, 5f, -1f };
            float[] want = { 0.2f, 0.2f, 1f, 2f, 1f, 0.2f, 0.2f, 1f, 1f };
            for (int i = 0; i < t.Length; i++)
                Assert.That(FightSim.Wave(t[i], 4f), Is.EqualTo(want[i]).Within(1e-6f), "Wave(" + t[i] + ")");
            Assert.That(FightSim.Wave(1f, 0f), Is.EqualTo(FightSim.Wave(1f, 4f)).Within(1e-6f), "fallback to Rust's 4 s");
            for (uint seed = 1; seed <= 20; seed++)
            {
                FightSim s = Make(P(Medium), seed: seed);
                double sum = 0;
                for (int i = 0; i < 200; i++) { s.Tick(In(0f, false)); sum += s.PullWave * Dt; }
                Assert.That(sum, Is.EqualTo(4.04).Within(1e-3), "wave integral over one period, seed " + seed);
            }
        }

        // ---- 12-16: steering and the cone ---------------------------------------------------------------------------------------------

        // Catches (user decision 2): countering that does not drag the fish back (no halved run, no push), that does not tire it, or that
        // charges the wrong strain. Numbers: no input 0.8858 / tire -0.4; counter only 0.4382 / tire 5.0 / strain 2.4 (research 8.7 wave sums).
        [Test]
        public void FightSim_Counter_DragsTheFishBackAndTiresIt()
        {
            FightSim a = Make(P(Medium));
            FightSim b = Make(P(Medium));
            a.ForceState(RunState(1, false, 0.5f, 12f));
            b.ForceState(RunState(1, false, 0.5f, 12f));
            for (int i = 0; i < 200; i++) { a.Tick(In(0f, false)); b.Tick(In(-1f, false)); }
            Assert.That(a.State.Lateral, Is.EqualTo(0.8858f).Within(0.005f));
            Assert.That(a.State.Tire, Is.EqualTo(-0.4f).Within(1e-4f));
            Assert.That(a.State.Strain, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(b.State.Lateral, Is.EqualTo(0.4382f).Within(0.005f));
            Assert.That(b.State.Tire, Is.EqualTo(5.0f).Within(1e-4f));
            Assert.That(b.State.Strain, Is.EqualTo(2.4f).Within(1e-3f));
            Assert.That(b.State.LineLength, Is.EqualTo(12f).Within(1e-5f));
            Assert.That(b.State.Lateral, Is.LessThan(0.5f));
            Assert.That(0.5f, Is.LessThan(a.State.Lateral));
        }

        // Catches: a steer that is not dropped when it pushes the fish away from the cast line, a digital threshold other than 0.5 (0.4 must
        // be dropped, 0.6 must count), a steer counted at lateral exactly 0, NaN steer leaking into the maths, and a push of the wrong size.
        [Test]
        public void FightSim_Steer_CountsOnlyTowardTheCastLine()
        {
            FightSim a = Make(P(Medium));
            a.ForceState(RestState(10f, strain: 3f, lateral: 0.3f));
            a.Tick(In(1f, false));
            Assert.That(a.State.Strain, Is.EqualTo(2.97f).Within(1e-5f), "away from the line: dropped, so it drains");
            Assert.That(a.State.Tire, Is.EqualTo(-0.002f).Within(1e-6f));
            Assert.That(a.State.Lateral, Is.EqualTo(0.3f).Within(1e-7f));

            FightSim b = Make(P(Medium));
            b.ForceState(RestState(10f, strain: 3f, lateral: 0.3f));
            b.Tick(In(-1f, false));
            Assert.That(b.State.Lateral, Is.EqualTo(0.3f - 0.8f * 0.02f / (10f * 1.0471976f)).Within(1e-6f));
            Assert.That(b.State.Tire, Is.EqualTo(0.016f).Within(1e-6f));
            Assert.That(b.State.Strain, Is.EqualTo(3.0075f).Within(1e-5f));

            FightSim c = Make(P(Medium));
            c.ForceState(RestState(10f, strain: 3f, lateral: 0f));
            c.Tick(In(-1f, false));
            Assert.That(c.State.Strain, Is.EqualTo(2.97f).Within(1e-5f), "lateral exactly 0: dropped");

            FightSim d = Make(P(Medium));
            d.ForceState(RestState(10f, strain: 3f, lateral: 0.3f));
            d.Tick(In(-0.4f, false));
            Assert.That(d.State.Strain, Is.EqualTo(2.97f).Within(1e-5f), "0.4 is below the digital threshold");

            FightSim e = Make(P(Medium));
            e.ForceState(RestState(10f, strain: 3f, lateral: 0.3f));
            e.Tick(In(-0.6f, false));
            Assert.That(e.State.Strain, Is.EqualTo(3.0075f).Within(1e-5f), "0.6 counts as a key press");

            FightSim f = Make(P(Medium));
            f.ForceState(RestState(10f, strain: 3f, lateral: 0.3f));
            f.Tick(In(float.NaN, false));
            Assert.That(f.State.Strain, Is.EqualTo(2.97f).Within(1e-5f), "NaN steer is dropped");
            Assert.That(f.State.Lateral, Is.EqualTo(0.3f).Within(1e-7f));
        }

        // Catches: a push applied when steering the same way as the run (Rust adds no force there), the other-steer rate charged as a counter
        // (0.8) or as a reel, and tire not rising at 0.8/s for a steer that is not a plain counter.
        [Test]
        public void FightSim_SameWaySteer_NoPushHalfRateTire()
        {
            FightSim a = Make(P(Medium));
            FightSim b = Make(P(Medium));
            a.ForceState(RunState(1, false, -0.5f, 10f));
            b.ForceState(RunState(1, false, -0.5f, 10f));
            a.Tick(In(0f, false));
            b.Tick(In(1f, false));
            Assert.That(b.State.Lateral, Is.EqualTo(a.State.Lateral).Within(1e-7f), "no push, no halving");
            Assert.That(b.State.Strain, Is.EqualTo(0.0075f).Within(1e-6f), "k 0.5");
            Assert.That(b.State.Tire, Is.EqualTo(0.016f).Within(1e-6f));
        }

        // Catches (review finding 1): the strain table checking the counter-steer before the reel (Rust: reeling takes precedence, so reel plus a
        // counter is still 2.2, or 2.5 on a back pull), or the rates summing instead. The counter still halves the run, pushes, and tires.
        [Test]
        public void FightSim_ReelPlusCounter_ReelTakesPrecedence()
        {
            FightSim a = Make(P(Medium));
            a.ForceState(RunState(1, false, 0.5f, 18f));
            a.Tick(In(-1f, true));
            Assert.That(a.State.Strain, Is.EqualTo(0.033f).Within(1e-6f), "2.2 x 0.75 x 0.02, not the 0.8 of a counter alone");
            Assert.That(a.State.Tire, Is.EqualTo(0.025f).Within(1e-6f), "a counter of a plain run tires at 1.25/s even while reeling");
            Assert.That(a.State.LineLength - 18f, Is.EqualTo(-0.012f).Within(1e-6f));
            float expect = (0.6f * a.PullWave - 0.8f) * 0.02f / (18f * 1.0471976f);
            Assert.That(a.State.Lateral - 0.5f, Is.EqualTo(expect).Within(1e-6f), "run halved plus the 0.8 push");

            FightSim b = Make(P(Medium));
            b.ForceState(RunState(1, true, 0.5f, 18f));
            b.Tick(In(-1f, true));
            Assert.That(b.State.Strain, Is.EqualTo(0.0375f).Within(1e-6f), "2.5 on a back pull");
            Assert.That(b.State.Tire, Is.EqualTo(0.016f).Within(1e-6f), "0.8/s during a back pull");

            FightSim c = Make(P(Medium));
            c.ForceState(RestState(10f, lateral: 0.3f));
            c.Tick(In(-1f, true));
            Assert.That(c.State.Strain, Is.EqualTo(0.0075f).Within(1e-6f), "at rest k is 0.5 once, not a sum of reel and steer");
        }

        // Catches (review finding 2): rod stats read from the wrong place. Twig: reel 0.54, steerRate 0.67, relaxMult 0.9, lineStrength 0.8.
        // A gain that ignores lineStrength, a drain that ignores relaxMult, a push that uses another rate, a reel that ignores the rod.
        [Test]
        public void FightSim_TwigRod_UsesItsOwnStats()
        {
            RodStats twig = FishingTestData.Rods()[FishingTestData.RodTwig];

            FightSim a = Make(P(Small), rod: twig);
            a.ForceState(RunState(1, false, 0.5f, 18f));
            a.Tick(In(0f, true));
            Assert.That(a.State.Strain, Is.EqualTo(0.033f).Within(1e-6f), "2.2 x 0.8 x 0.75 / 0.8 x 0.02");
            int end = 1 + RunUntilEnd(a, In(0f, true), 600);
            Assert.That(end, Is.EqualTo(213).Within(1));
            Assert.That(a.State.Outcome, Is.EqualTo(FightOutcome.Snapped));

            FightSim d = Make(P(Medium), rod: twig);
            d.ForceState(RestState(10f, strain: 6f));
            for (int i = 0; i < 120; i++) d.Tick(In(0f, false));
            Assert.That(d.State.Strain, Is.EqualTo(2.76f).Within(1e-3f), "6 - 1.5 x 0.9 x 2.4 s");

            FightSim p = Make(P(Medium), rod: twig);
            p.ForceState(RestState(10f, lateral: 0.3f));
            p.Tick(In(-1f, false));
            Assert.That(p.State.Lateral - 0.3f, Is.EqualTo(-0.67f * 0.02f / (10f * 1.0471976f)).Within(1e-6f), "the push is the rod's steerRate");

            FightSim r = Make(P(Medium), rod: twig);
            r.ForceState(RestState(10f));
            r.Tick(In(0f, true));
            Assert.That(r.State.LineLength - 10f, Is.EqualTo(-0.0108f).Within(1e-6f), "0.54 x 1.0 x 0.02");
        }

        // Catches: a counter during a back pull tiring at the plain-counter rate (1.25) or charging the counter rate when the table says the
        // run is a back pull (it is still counter-only, k 0.8, but tire +0.8/s).
        [Test]
        public void FightSim_CounterDuringBackPull_SlowerTireCounterStrain()
        {
            FightSim s = Make(P(Medium));
            s.ForceState(RunState(1, true, 0.5f, 10f));
            s.Tick(In(-1f, false));
            Assert.That(s.State.Tire, Is.EqualTo(0.016f).Within(1e-6f), "0.8/s, not 1.25");
            Assert.That(s.State.Strain, Is.EqualTo(0.012f).Within(1e-6f), "k 0.8");
        }

        // Catches: a cone edge that lets lateral exceed 1, that does not flip the run's side, or that spends a random draw to do it.
        [Test]
        public void FightSim_ConeEdge_FlipsTheRunWithoutADraw()
        {
            FightSim s = Make(P(Medium));
            s.ForceState(RunState(1, false, 0.9999f, 10f));
            int d0 = s.DrawCount;
            s.Tick(In(0f, false));
            Assert.That(s.State.Lateral, Is.EqualTo(1f));
            Assert.That(s.State.BurstDir, Is.EqualTo((sbyte)-1));
            Assert.That(s.DrawCount, Is.EqualTo(d0));
            for (int i = 0; i < 10; i++) s.Tick(In(0f, false));
            Assert.That(s.State.Lateral, Is.LessThan(1f));
        }

        // ---- 17-20: tire reel, tire rates, back pulls, landing -----------------------------------------------------------------------

        // Catches: a reel that ignores tire, a multiplier that is not clamped to 1..3 (x3 at tire 5 and beyond, x1 at negative tire), a
        // missing ReelInMult (small x1.2, large x0.9).
        [Test]
        public void FightSim_Tire_RaisesReelSpeedUpToThreeTimes()
        {
            (int tier, float tire, float dLine)[] rows =
            {
                (Medium, 0f, -0.012f), (Medium, 2.5f, -0.024f), (Medium, 5f, -0.036f), (Medium, 50f, -0.036f), (Medium, -3f, -0.012f),
                (Small, 0f, -0.0144f), (Large, 0f, -0.0108f),
            };
            foreach (var r in rows)
            {
                FightSim s = Make(P(r.tier));
                s.ForceState(RestState(10f, tire: r.tire));
                s.Tick(In(0f, true));
                Assert.That(s.State.LineLength - 10f, Is.EqualTo(r.dLine).Within(1e-5f), "tier " + r.tier + " tire " + r.tire);
            }
        }

        // Catches: tire rates other than +0.8 (steer), +1.25 (counter of a plain side run), -0.1 (no steer); reeling tiring the fish; a floor at 0.
        [Test]
        public void FightSim_TireRates_SteerCounterDecayNoFloor()
        {
            FightSim a = Make(P(Medium));
            a.ForceState(RestState(10f, lateral: 0.3f));
            a.Tick(In(-1f, false));
            Assert.That(a.State.Tire, Is.EqualTo(0.016f).Within(1e-6f), "rest steer toward the line");

            FightSim b = Make(P(Medium));
            b.ForceState(RunState(1, false, 0.5f, 10f));
            b.Tick(In(-1f, false));
            Assert.That(b.State.Tire, Is.EqualTo(0.025f).Within(1e-6f), "counter on a plain run");

            FightSim c = Make(P(Medium));
            c.ForceState(RestState(10f));
            c.Tick(In(0f, false));
            Assert.That(c.State.Tire, Is.EqualTo(-0.002f).Within(1e-6f), "no steer");

            FightSim d = Make(P(Medium));
            d.ForceState(RestState(10f));
            d.Tick(In(0f, true));
            Assert.That(d.State.Tire, Is.EqualTo(-0.002f).Within(1e-6f), "reeling never tires");

            FightSim e = Make(P(Medium));
            e.ForceState(RestState(10f));
            for (int i = 0; i < 100; i++) e.Tick(In(0f, false));
            Assert.That(e.State.Tire, Is.EqualTo(-0.2f).Within(1e-4f), "no floor");
        }

        // Catches: a back pull that does not take line, one that is not halved while reeling (x0.5), or one that scales with the wrong speed.
        // Numbers: 12 + 1.3 x 4.04 = 17.252 with no reel; 12 + 0.65 x 4.04 - 0.6 x 4 = 12.226 while reeling. The reeling run uses a rod with
        // a huge lineStrength so 4 s of reeling into a back pull cannot snap (the strain rules are tested elsewhere).
        [Test]
        public void FightSim_BackPull_TakesLineHalvedWhileReeling()
        {
            FightSim a = Make(P(Medium));
            a.ForceState(RunState(1, true, 0.5f, 12f));
            for (int i = 0; i < 200; i++) a.Tick(In(0f, false));
            Assert.That(a.State.LineLength, Is.EqualTo(17.252f).Within(0.01f));

            RodStats strong = Handmade();
            strong.LineStrength = 1000f;
            FightSim b = Make(P(Medium), rod: strong);
            b.ForceState(RunState(1, true, 0.5f, 12f));
            for (int i = 0; i < 200; i++) b.Tick(In(0f, true));
            Assert.That(b.State.Outcome, Is.EqualTo(FightOutcome.None));
            Assert.That(b.State.LineLength, Is.EqualTo(12.226f).Within(0.01f));
        }

        // Catches: a landing distance other than 1.0 m, a landing that fires without reeling, a landing checked before the reel is applied.
        [Test]
        public void FightSim_LandsAtOneMetreWhileReeling()
        {
            FightSim s = Make(P(Medium));
            s.ForceState(RestState(1.5f));
            for (int i = 1; i <= 41; i++) Assert.That(s.Tick(In(0f, true)), Is.EqualTo(FightOutcome.None), "tick " + i + " line " + s.State.LineLength);
            Assert.That(s.State.LineLength, Is.EqualTo(1.008f).Within(1e-4f));
            Assert.That(s.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Landed), "tick 42");

            FightSim t = Make(P(Medium));
            t.ForceState(RestState(0.5f));
            for (int i = 0; i < 5; i++) t.Tick(In(0f, false));
            Assert.That(t.State.Outcome, Is.EqualTo(FightOutcome.None), "inside the landing distance but not reeling");
        }

        // ---- 21-24: timeout, priority, weight, determinism ----------------------------------------------------------------------------

        // Catches: no fish timeout, a timeout at the old 12 s slack or 240 s host fallback, a ThrewHook reason, a timeout that needs input.
        [Test]
        public void FightSim_Timeout_EscapesAtOneTwentySeconds()
        {
            FightSim s = Make(P(Medium), line: 12f, seed: 7);
            int end = RunUntilEnd(s, In(0f, false), 7000);
            Assert.That(end, Is.EqualTo(6000).Within(1));
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.Timeout));
            Assert.That(s.State.Elapsed, Is.EqualTo(120f).Within(0.021f));
        }

        // Catches: a priority other than Landed > Snapped > Timeout when two endings fall on the same tick.
        [Test]
        public void FightSim_SameTick_LandedThenSnappedThenTimeout()
        {
            FightSim a = Make(P(Large));
            a.ForceState(RunState(1, false, 0.5f, 1.005f, strain: 6.99f));
            Assert.That(a.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Landed), "landed beats snapped");

            FightState late = RunState(1, false, 0.5f, 10f, strain: 6.99f);
            late.Elapsed = 119.99f;
            FightSim b = Make(P(Large));
            b.ForceState(late);
            Assert.That(b.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Snapped), "snapped beats timeout");

            FightState rest = RestState(10f);
            rest.Elapsed = 119.99f;
            FightSim c = Make(P(Large));
            c.ForceState(rest);
            Assert.That(c.Tick(In(0f, false)), Is.EqualTo(FightOutcome.Escaped));
            Assert.That(c.State.Reason, Is.EqualTo(EscapeReason.Timeout));
        }

        // Catches: any weight term creeping back into the fight (Rust has none; size lives in the tier).
        [Test]
        public void FightSim_WeightT_DoesNotChangeTheFight()
        {
            FightSim a = Make(P(Large), seed: 99, weightT: 0f);
            FightSim b = Make(P(Large), seed: 99, weightT: 1f);
            for (int i = 0; i < 3000; i++)
            {
                FightInput input = In((float)Math.Sin(i * 0.05), i % 7 != 0);
                FightOutcome oa = a.Tick(input), ob = b.Tick(input);
                Assert.That(SameState(a.State, b.State), Is.True, "tick " + (i + 1));
                Assert.That(oa, Is.EqualTo(ob));
                if (oa != FightOutcome.None) break;
            }
        }

        // Catches: hidden state (time, static RNG, UnityEngine.Random) leaking into the fight; the seed having no effect.
        [Test]
        public void FightSim_SameSeedAndInputs_IdenticalState()
        {
            FightSim a = Make(P(Medium), seed: 99);
            FightSim b = Make(P(Medium), seed: 99);
            FightSim c = Make(P(Medium), seed: 100);
            bool differs = false;
            for (int i = 0; i < 3000; i++)
            {
                FightInput input = In((float)Math.Sin(i * 0.05), i % 7 != 0);
                a.Tick(input); b.Tick(input); c.Tick(input);
                Assert.That(SameState(a.State, b.State), Is.True, "tick " + (i + 1));
                if (!SameState(a.State, c.State)) differs = true;
                if (a.State.Outcome != FightOutcome.None) break;
            }
            Assert.That(differs, Is.True, "a different seed must change the fight");
        }

        // ---- 25-26: assist and Pity -----------------------------------------------------------------------------------------------------

        // Catches: assist scaling the drain or the headroom (it scales only the gains: 0.45 instead of 0.75), a SnapTension that moved.
        [Test]
        public void FightSim_Assist_ScalesGainsOnly()
        {
            FightSim s = Make(P(Small), assist: true);
            s.ForceState(RunState(1, false, 0.5f, 18f));
            s.Tick(In(0f, true));
            Assert.That(s.State.Strain, Is.EqualTo(0.01584f).Within(1e-6f));
            int end = 1 + RunUntilEnd(s, In(0f, true), 800);
            Assert.That(end, Is.EqualTo(442).Within(1));
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(s.SnapTension, Is.EqualTo(7f / 6f).Within(1e-6f));

            FightSim d = Make(P(Small), assist: true);
            d.ForceState(RestState(10f, strain: 3f));
            d.Tick(In(0f, false));
            Assert.That(d.State.Strain, Is.EqualTo(2.97f).Within(1e-5f), "the drain is unchanged");
        }

        // Catches: Pity not lowering the gains (x0.85), Pity not raising the snap strain to 7.5 (headroom x1.5), or Pity spending a draw.
        [Test]
        public void FightSim_Pity_LowerGainsAndLongerHeadroom()
        {
            FightSim n = Make(P(Small));
            FightSim p = Make(P(Small), pity: true);
            n.ForceState(RunState(1, false, 0.5f, 18f));
            p.ForceState(RunState(1, false, 0.5f, 18f));
            Assert.That(p.SnapTension, Is.EqualTo(1.25f).Within(1e-6f));
            Assert.That(n.SnapTension, Is.EqualTo(7f / 6f).Within(1e-6f));
            int nEnd = -1, pEnd = -1;
            for (int i = 1; i <= 800 && (nEnd < 0 || pEnd < 0); i++)
            {
                if (nEnd < 0 && n.Tick(In(0f, true)) != FightOutcome.None) nEnd = i;
                if (pEnd < 0 && p.Tick(In(0f, true)) != FightOutcome.None) pEnd = i;
                if (i == 1) Assert.That(p.State.Strain, Is.EqualTo(0.02244f).Within(1e-6f));
                if (nEnd < 0 && pEnd < 0) Assert.That(p.DrawCount, Is.EqualTo(n.DrawCount), "pity draws nothing (tick " + i + ")");
            }
            Assert.That(nEnd, Is.EqualTo(266).Within(1));
            Assert.That(pEnd, Is.EqualTo(335).Within(1));
        }

        // ---- 27: near-snap recovery ----------------------------------------------------------------------------------------------------

        // Catches: a recovery counted without the bar going over full, a threshold left at the old 0.8, or one counted before the bar is under half.
        [Test]
        public void FightSim_NearSnapRecovery_OverTheFullBarThenUnderHalf()
        {
            FightSim s = Make(P(Medium));
            s.ForceState(RestState(10f, strain: 6.1f));
            for (int i = 0; i < 103; i++) s.Tick(In(0f, false));
            Assert.That(s.State.NearSnapRecoveries, Is.EqualTo(0), "6.1 - 103 x 0.03 = 3.01, still over half the bar");
            s.Tick(In(0f, false));
            Assert.That(s.State.NearSnapRecoveries, Is.EqualTo(1), "6.1 - 104 x 0.03 = 2.98, under half");

            FightSim t = Make(P(Medium));
            t.ForceState(RestState(10f, strain: 5.9f));
            for (int i = 0; i < 200; i++) t.Tick(In(0f, false));
            Assert.That(t.State.NearSnapRecoveries, Is.EqualTo(0), "never over the full bar");
        }

        // ---- 28-29: junk and exhaustion ------------------------------------------------------------------------------------------------

        // Catches: junk drawing random numbers or building strain, a junk drag speed other than 3 m/s, a junk slack escape other than 12 s.
        [Test]
        public void FightSim_Junk_DragsWithoutStrainAndThrowsTheHookWhenSlack()
        {
            FightSim a = MakeJunk(5, 10.03f);
            for (int i = 0; i < 50; i++) a.Tick(In(0f, true));
            Assert.That(a.State.LineLength, Is.EqualTo(7.03f).Within(1e-3f));
            Assert.That(a.State.Strain, Is.EqualTo(0f));
            Assert.That(a.State.Tension, Is.EqualTo(0f));
            Assert.That(a.DrawCount, Is.EqualTo(0));
            Assert.That(50 + RunUntilEnd(a, In(0f, true), 200), Is.EqualTo(151).Within(1));
            Assert.That(a.State.Outcome, Is.EqualTo(FightOutcome.Landed));

            FightSim b = MakeJunk(5, 10.03f);
            int end = RunUntilEnd(b, In(0f, false), 800);
            Assert.That(end, Is.EqualTo(600).Within(1));
            Assert.That(b.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(b.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
        }

        // Catches: an Exhausted that latches (it is tire >= 20, and falls again when tire falls) or one that triggers below 20.
        [Test]
        public void FightSim_Exhausted_AtTireRunFull_NotLatched()
        {
            FightSim s = Make(P(Medium));
            s.ForceState(RestState(10f, lateral: 0.3f, tire: 19.99f));
            Assert.That(s.Exhausted, Is.False);
            s.Tick(In(-1f, false));
            Assert.That(s.State.Tire, Is.EqualTo(20.006f).Within(1e-4f));
            Assert.That(s.Exhausted, Is.True);
            for (int i = 0; i < 10; i++) s.Tick(In(0f, false));
            Assert.That(s.State.Tire, Is.EqualTo(19.986f).Within(1e-4f));
            Assert.That(s.Exhausted, Is.False);
        }

        // ================== quirks (R72, spec 9.6): each changes behaviour, none touches the strain rules ==================

        // Catches: a burst ramp that is ignored, applied to the wrong speed (side or back), or that leaks into strain.
        [TestCase(0.6f)]   // Steady's shipped ramp
        [TestCase(0.8f)]   // Runner's shipped ramp
        public void FightSim_Quirk_BurstRampEasesTheRunIn(float ramp)
        {
            FishFightProfile withRamp = P(Medium); withRamp.BurstRamp = ramp;
            FishFightProfile without = P(Medium);
            foreach (bool back in new[] { false, true })
            {
                FightSim a = Make(withRamp), b = Make(without);
                a.ForceState(RunState(1, back, 0.2f, 10f));
                b.ForceState(RunState(1, back, 0.2f, 10f));
                for (int k = 1; k <= 60; k++)
                {
                    float la = a.State.Lateral, lb = b.State.Lateral, na = a.State.LineLength, nb = b.State.LineLength;
                    a.Tick(In(0f, false)); b.Tick(In(0f, false));
                    float expect = Math.Min(1f, k * Dt / ramp);
                    if (k == 1 || k == 20 || k == 40 || k == 60)
                    {
                        if (back) Assert.That((a.State.LineLength - na) / (b.State.LineLength - nb), Is.EqualTo(expect).Within(1e-4f), "back tick " + k);
                        else Assert.That((a.State.Lateral - la) / (b.State.Lateral - lb), Is.EqualTo(expect).Within(1e-4f), "side tick " + k);
                    }
                }
            }
            FightSim c = Make(withRamp), d = Make(without);
            c.ForceState(RunState(1, false, 0.2f, 10f));
            d.ForceState(RunState(1, false, 0.2f, 10f));
            for (int k = 0; k < 60; k++)
            {
                c.Tick(In(0f, true)); d.Tick(In(0f, true));
                Assert.That(c.State.Strain, Is.EqualTo(d.State.Strain).Within(1e-7f), "the ramp never touches strain (tick " + (k + 1) + ")");
            }
        }

        // Catches: a FlipRate that never flips, a flip hazard other than FlipRate per second (draws only when eligible), a minimum gap not
        // honoured, and flips (or draws) in a quirk-free profile.
        [Test]
        public void FightSim_Quirk_FlipRateSwitchesSidesMidRun()
        {
            int flips = 0, draws = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                FightSim s = Make(M(Medium), seed: seed);
                s.ForceState(RunState(1, false, 0.05f, 10f, phaseTimer: 1000f));
                int d0 = s.DrawCount;
                sbyte dir = s.State.BurstDir;
                int lastFlipTick = -1000;
                for (int k = 1; k <= 750; k++)
                {
                    float lat = s.State.Lateral;
                    s.Tick(In(lat > 0f ? -1f : 1f, false));
                    if (s.State.BurstDir != dir && Math.Abs(s.State.Lateral) < 0.999f)
                    {
                        flips++;
                        Assert.That((k - lastFlipTick) * Dt, Is.GreaterThanOrEqualTo(0.3f - 1e-4f), "flip gap, seed " + seed + " tick " + k);
                        lastFlipTick = k;
                    }
                    dir = s.State.BurstDir;
                }
                draws += s.DrawCount - d0;
            }
            double hazard = flips / (draws * (double)Dt);
            Assert.That(hazard, Is.InRange(0.8, 1.2), "flips " + flips + " over " + draws + " eligible ticks");

            FightSim b = Make(P(Medium), seed: 3);
            b.ForceState(RunState(1, false, 0.05f, 10f, phaseTimer: 1000f));
            int bd0 = b.DrawCount;
            sbyte bdir = b.State.BurstDir;
            for (int k = 1; k <= 750; k++)
            {
                b.Tick(In(b.State.Lateral > 0f ? -1f : 1f, false));
                if (Math.Abs(b.State.Lateral) < 0.999f) Assert.That(b.State.BurstDir, Is.EqualTo(bdir), "no mid-run flips without FlipRate");
                bdir = b.State.BurstDir;
            }
            Assert.That(b.DrawCount - bd0, Is.EqualTo(0), "no flip draws without FlipRate");
        }

        // Catches: LateralJitter ignored, a multiplier outside [1 - j, 1 + j], a jitter draw made when j is 0, or the multiplier not applied to
        // the side speed.
        [Test]
        public void FightSim_Quirk_LateralJitterSetsAPerRunSpeed()
        {
            FishFightProfile thrasher = M(FishingTestData.ProfileThrasher);
            thrasher.FlipRate = 0f;
            FightSim s = Make(thrasher);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 60; i++)
            {
                s.ForceState(RestState(16.5f, phaseTimer: 0f));
                int d0 = s.DrawCount;
                s.Tick(In(0f, false));
                float mult = s.RunSpeedMult;
                Assert.That(mult, Is.InRange(0.7f - 1e-6f, 1.3f + 1e-6f));
                min = Math.Min(min, mult); max = Math.Max(max, mult);
                Assert.That(s.DrawCount - d0, Is.EqualTo(4), "side, back, duration, jitter");
                float before = s.State.Lateral;
                s.Tick(In(0f, false));
                float expect = s.State.BurstDir * 1.2f * s.PullWave * 1.0f * mult * Dt / (16.5f * 1.0471976f);
                Assert.That(s.State.Lateral - before, Is.EqualTo(expect).Within(1e-6f), "second tick, run " + i);
            }
            Assert.That(max - min, Is.GreaterThan(0.2f));

            FishFightProfile plain = thrasher; plain.LateralJitter = 0f;
            FightSim t = Make(plain);
            t.ForceState(RestState(16.5f, phaseTimer: 0f));
            int td = t.DrawCount;
            t.Tick(In(0f, false));
            Assert.That(t.DrawCount - td, Is.EqualTo(3));
            Assert.That(t.RunSpeedMult, Is.EqualTo(1f));
        }

        // Catches: a WavePeriod that is ignored (Rust 4 s used everywhere), a wave whose mean changes with the period, and a constructor that
        // draws a phase outside [0, period).
        [Test]
        public void FightSim_Quirk_WavePeriodSpeedsTheSurge()
        {
            Assert.That(FightSim.Wave(0.375f, 1.5f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(FightSim.Wave(0.75f, 1.5f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(FightSim.Wave(1.125f, 1.5f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(FightSim.Wave(1.5f, 1.5f), Is.EqualTo(0.2f).Within(1e-5f));
            for (uint seed = 1; seed <= 10; seed++)
            {
                FightSim s = Make(M(FishingTestData.ProfileThrasher), seed: seed);
                Assert.That(s.DrawCount, Is.EqualTo(1), "the constructor makes one draw");
                double sum = 0;
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 75; i++)
                {
                    s.Tick(In(0f, false));
                    sum += s.PullWave * Dt;
                    lo = Math.Min(lo, s.PullWave); hi = Math.Max(hi, s.PullWave);
                }
                Assert.That(sum, Is.EqualTo(1.515).Within(1e-2), "one 1.5 s period, seed " + seed);
                Assert.That(hi, Is.GreaterThan(1.95f), "a 1.5 s period always reaches the peak within 75 ticks (seed " + seed + ")");
                Assert.That(lo, Is.LessThan(0.25f), "and the floor (seed " + seed + ")");
            }
        }

        // Catches: CalmDrift ignored, a drift that does not ramp in, one that ignores tire (x (1 - tire01)), one that continues when exhausted,
        // or one that moves the line during a run.
        [Test]
        public void FightSim_Quirk_CalmDriftCreepsLineOutAtRest()
        {
            FishFightProfile bulldog = M(FishingTestData.ProfileBulldog);
            FightSim s = Make(bulldog);
            s.ForceState(RestState(10f));
            float d1 = 0f, d125 = 0f;
            for (int i = 1; i <= 250; i++)
            {
                float before = s.State.LineLength;
                s.Tick(In(0f, false));
                if (i == 1) d1 = s.State.LineLength - before;
                if (i == 125) d125 = s.State.LineLength - before;
            }
            Assert.That(s.State.LineLength - 10f, Is.EqualTo(0.225f).Within(0.005f));
            Assert.That(d1, Is.LessThan(d125), "the creep ramps in");

            FightSim t = Make(bulldog);
            t.ForceState(RestState(10f, tire: 10f));
            for (int i = 0; i < 250; i++) t.Tick(In(0f, false));
            Assert.That(t.State.LineLength - 10f, Is.EqualTo(0.1139f).Within(0.003f), "drift scales with 1 - tire01");

            FightSim u = Make(bulldog);
            u.ForceState(RestState(10f, tire: 20.5f));
            for (int i = 0; i < 100; i++) u.Tick(In(0f, false));
            Assert.That(u.State.LineLength, Is.EqualTo(10f).Within(1e-6f), "an exhausted fish does not creep");

            FightSim v = Make(bulldog);
            v.ForceState(RunState(1, false, 0.5f, 10f));
            v.Tick(In(0f, false));
            Assert.That(v.State.LineLength, Is.EqualTo(10f).Within(1e-7f), "no drift in a side run");
        }

        // Catches: a profile whose DiveChance, rest range or run range is not what the table says (shipped values from MotionProfiles).
        [Test]
        public void FightSim_Quirk_ProfileTimingAndBackChance()
        {
            (int i, float dive, float calmMin, float calmMax, float burstMin, float burstMax)[] rows =
            {
                (FishingTestData.ProfileSteady, 0.15f, 2f, 4f, 3f, 5f), (FishingTestData.ProfileDarter, 0.15f, 1f, 2f, 2f, 3.5f),
                (FishingTestData.ProfileThrasher, 0.20f, 1.5f, 3f, 3f, 5f), (FishingTestData.ProfileBulldog, 0.55f, 2f, 4f, 2.5f, 4.5f),
                (FishingTestData.ProfileRunner, 0.30f, 2f, 3.5f, 5f, 9f),
            };
            foreach (var r in rows)
            {
                int back = 0;
                for (uint seed = 1; seed <= 1000; seed++)
                {
                    FightSim run = Make(M(r.i), seed: seed);
                    run.ForceState(RestState(10f, phaseTimer: 0f));
                    run.Tick(In(0f, false));
                    if (run.State.Back) back++;
                    Assert.That(run.State.PhaseTimer + 0.02f, Is.InRange(r.burstMin - 1e-5f, r.burstMax + 1e-5f), "run " + r.i);

                    FightSim rest = Make(M(r.i), seed: seed);
                    rest.ForceState(RunState(1, false, 0.5f, 10f, phaseTimer: 0f));
                    rest.Tick(In(0f, false));
                    Assert.That(rest.State.PhaseTimer + 0.02f, Is.InRange(r.calmMin - 1e-5f, r.calmMax + 1e-5f), "rest " + r.i);
                }
                Assert.That(back / 1000.0, Is.EqualTo(r.dive).Within(0.05), "back share, profile " + r.i);
            }
        }

        // Catches: a quirk that changes the strain formula, the tire or the reel (the five styles differ only in how the fish moves).
        [Test]
        public void FightSim_Quirks_NeverTouchTheStrainRules()
        {
            for (int i = 0; i < 5; i++)
            {
                FishFightProfile q = M(i), b = P(i);
                float mod = b.StrainMod;
                (string what, FightState st, FightInput input, float dStrain)[] cases =
                {
                    ("side-run reel", RunState(1, false, 0.2f, 10f), In(0f, true), 2.2f * mod * 0.75f * Dt),
                    ("back reel", RunState(1, true, 0.2f, 10f), In(0f, true), 2.5f * mod * 0.75f * Dt),
                    ("rest reel", RestState(10f), In(0f, true), 0.5f * mod * 0.75f * Dt),
                    ("counter only", RunState(1, false, 0.5f, 10f), In(-1f, false), 0.8f * mod * 0.75f * Dt),
                    ("release", RestState(10f, strain: 3f), In(0f, false), -0.03f),
                };
                foreach (var c in cases)
                {
                    FightSim sq = Make(q), sb = Make(b);
                    sq.ForceState(c.st); sb.ForceState(c.st);
                    sq.Tick(c.input); sb.Tick(c.input);
                    Assert.That(sq.State.Strain - c.st.Strain, Is.EqualTo(c.dStrain).Within(1e-7f), "profile " + i + " " + c.what);
                    Assert.That(sq.State.Strain, Is.EqualTo(sb.State.Strain).Within(1e-7f), "profile " + i + " " + c.what + " vs quirk-free");
                    Assert.That(sq.State.Tire, Is.EqualTo(sb.State.Tire).Within(1e-7f), "profile " + i + " " + c.what + " tire");
                }
                FightSim rq = Make(q), rb = Make(b);
                rq.ForceState(RestState(10f)); rb.ForceState(RestState(10f));
                rq.Tick(In(0f, true)); rb.Tick(In(0f, true));
                Assert.That(rq.State.LineLength - 10f, Is.EqualTo(-0.6f * b.ReelInMult * Dt).Within(2e-5f), "profile " + i + " rest reel dLine");
            }
        }

        // Catches: a style whose fight depends on anything but the seed and the inputs.
        [Test]
        public void FightSim_FiveStyles_SameSeedSameState()
        {
            for (int p = 0; p < 5; p++)
            {
                FightSim a = Make(M(p), seed: 555), b = Make(M(p), seed: 555), c = Make(M(p), seed: 556);
                bool differs = false;
                for (int i = 0; i < 3000; i++)
                {
                    FightInput input = In((float)Math.Sin(i * 0.05), i % 7 != 0);
                    a.Tick(input); b.Tick(input); c.Tick(input);
                    Assert.That(SameState(a.State, b.State), Is.True, "profile " + p + " tick " + (i + 1));
                    if (!SameState(a.State, c.State)) differs = true;
                    if (a.State.Outcome != FightOutcome.None) break;
                }
                Assert.That(differs, Is.True, "profile " + p + ": a different seed must change the fight");
            }
        }
    }
}
