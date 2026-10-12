using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Fight motion parameters (C 7.7, M2-D15), the draw table (T7) and the R65 pity multipliers. Forced phases keep the maths exact.
    public class FightMotionTests
    {
        const float Dt = FightSim.Step;

        static FishingTuning Tun() => FishingTestData.Tuning();
        static RodStats Handmade() => FishingTestData.Rods()[FishingTestData.RodHandmade];
        static FishFightProfile Base(int i) => FishingTestData.BaseProfiles()[i];
        static FishFightProfile Motion(int i) => FishingTestData.MotionProfiles()[i];
        static FightInput In(float steer, bool reel) => new FightInput(steer, reel);

        static FightSim Make(FishFightProfile f, float weightT = 0.375f, uint seed = 4242, float line = 18f, bool pity = false)
        {
            RodStats rod = Handmade();
            FishingTuning t = Tun();
            return new FightSim(in f, in rod, in t, weightT, seed, line, false, pity);
        }

        static bool SameState(in FightState a, in FightState b)
        {
            return a.LineLength == b.LineLength && a.Lateral == b.Lateral && a.Tension == b.Tension && a.FishStamina == b.FishStamina
                && a.Phase == b.Phase && a.PhaseTimer == b.PhaseTimer && a.BurstDir == b.BurstDir
                && a.SnapTimer == b.SnapTimer && a.PinnedTimer == b.PinnedTimer && a.SlackTimer == b.SlackTimer && a.Elapsed == b.Elapsed
                && a.NearSnapRecoveries == b.NearSnapRecoveries && a.MaxTension == b.MaxTension
                && a.Outcome == b.Outcome && a.Reason == b.Reason;
        }

        // ---- base model --------------------------------------------------------------------------------------------

        [Test]
        public void FightMotion_ZeroParameters_MatchBaseModel()
        {
            const float tol = 1e-5f;
            FishingTuning t = Tun();
            RodStats rod = Handmade();
            for (int p = 0; p < 5; p++)
            {
                FishFightProfile prof = Base(p);
                float pull = prof.PullStrength;   // weightT 0.375 -> WeightFactor 1.0, stamina 1 -> P = pull
                string who = "profile " + p;

                // (a) side run +1, steer +0.5 (not against), reel. FlipRate 0 here: only the tick maths is under test.
                FishFightProfile noFlip = prof; noFlip.FlipRate = 0f;
                FightSim a = Make(noFlip, 0.375f, 4242, 10f);
                var sa = new FightState { Lateral = 0.2f, FishStamina = 1f, LineLength = 10f };
                a.ForceState(in sa);
                a.ForcePhase(FightPhase.Burst, 1, 10f);
                a.Tick(In(0.5f, true));
                Assert.That(a.State.Lateral - 0.2f, Is.EqualTo((pull * prof.LateralSpeed + 0.5f * rod.SteerRate) * Dt).Within(tol), who + " dLateral");
                float reelRate = rod.ReelSpeed * Math.Max(0.1f, 1f - t.ReelBurstPenalty * pull);
                Assert.That(a.State.LineLength - 10f, Is.EqualTo((prof.RunSpeed * pull - reelRate) * Dt).Within(tol), who + " dLine");
                Assert.That(a.State.Tension, Is.EqualTo((t.ReelStrainBurst * pull + t.SteerStrain * 0.5f) * Dt).Within(tol), who + " dTension");

                // (b) dive, no reel
                FightSim b = Make(prof, 0.375f, 4242, 10f);
                var sb = new FightState { Lateral = 0.2f, FishStamina = 1f, LineLength = 10f };
                b.ForceState(in sb);
                b.ForcePhase(FightPhase.Burst, 0, 10f);
                b.Tick(In(0f, false));
                Assert.That(b.State.LineLength - 10f, Is.EqualTo(prof.RunSpeed * pull * t.DiveRunMult * Dt).Within(tol), who + " dive dLine");

                // (c) Calm, no drift
                FightSim c = Make(prof, 0.375f, 4242, 10f);
                var sc = new FightState { Lateral = 0.2f, FishStamina = 1f, LineLength = 10f };
                c.ForceState(in sc);
                c.ForcePhase(FightPhase.Calm, 0, 10f);
                c.Tick(In(0f, false));
                Assert.That(c.State.LineLength, Is.EqualTo(10f), who + " calm line");

                // Draw table: burst start = 3 draws (no jitter on base profiles), calm start = 1 draw.
                FightSim d = Make(prof, 0.375f, 4242, 10f);
                var sd = new FightState { FishStamina = 1f, LineLength = 10f };
                d.ForceState(in sd);
                d.ForcePhase(FightPhase.Calm, 0, Dt);
                d.Tick(In(0f, true));
                Assert.That(d.State.Phase, Is.EqualTo(FightPhase.Burst), who + " burst started");
                Assert.That(d.DrawCount, Is.EqualTo(3), who + " burst-start draws");

                FightSim e = Make(prof, 0.375f, 4242, 10f);
                var se = new FightState { FishStamina = 1f, LineLength = 10f };
                e.ForceState(in se);
                e.ForcePhase(FightPhase.Burst, 0, Dt);   // dive: no flip draws
                e.Tick(In(0f, true));
                Assert.That(e.State.Phase, Is.EqualTo(FightPhase.Calm), who + " calm started");
                Assert.That(e.DrawCount, Is.EqualTo(1), who + " calm-start draws");
            }
        }

        // ---- motion parameters -------------------------------------------------------------------------------------

        [Test]
        public void FightMotion_SteadyBurstRamp_LateralPushRampsOverRampTime()
        {
            FightSim s = Make(Motion(FishingTestData.ProfileSteady));
            s.ForcePhase(FightPhase.Burst, 1, 2.0f);
            float prev = s.State.Lateral;
            var d = new float[46];
            for (int k = 1; k <= 45; k++)
            {
                s.Tick(In(0f, false));
                d[k] = s.State.Lateral - prev;
                prev = s.State.Lateral;
            }
            foreach (int k in new[] { 1, 15, 30, 45 })
            {
                float want = 0.3f * 0.6f * Math.Min(1f, k * 0.02f / 0.6f) * 0.02f;
                Assert.That(d[k], Is.EqualTo(want).Within(1e-6f), "k = " + k);
            }
            for (int k = 2; k <= 45; k++)
                Assert.That(d[k], Is.GreaterThanOrEqualTo(d[k - 1] - 1e-7f), "non-decreasing at k = " + k);
            Assert.That(d[45], Is.EqualTo(d[30]).Within(1e-7f), "flat once ramped");
        }

        [Test]
        public void FightMotion_RunnerBurstRamp_LineOutRateRampsUp()
        {
            FightSim s = Make(Motion(FishingTestData.ProfileRunner), 0.375f, 4242, 10f);
            s.ForcePhase(FightPhase.Burst, 1, 3.0f);
            float prev = s.State.LineLength;
            var d = new float[61];
            for (int k = 1; k <= 60; k++)
            {
                s.Tick(In(0f, false));
                d[k] = s.State.LineLength - prev;
                prev = s.State.LineLength;
            }
            foreach (int k in new[] { 10, 20, 40, 60 })
            {
                float want = 0.8f * 0.85f * Math.Min(1f, k * 0.02f / 0.8f) * 0.02f;
                Assert.That(d[k], Is.EqualTo(want).Within(1e-5f), "k = " + k);
            }
            Assert.That(d[10], Is.LessThan(d[20]));
            Assert.That(d[20], Is.LessThan(d[40]));
            Assert.That(d[40], Is.EqualTo(d[60]).Within(1e-5f));
        }

        [Test]
        public void FightMotion_BulldogCalmDrift_LineCreepsOutDuringCalm()
        {
            FightSim s = Make(Motion(FishingTestData.ProfileBulldog), 0.375f, 4242, 10f);
            s.ForcePhase(FightPhase.Calm, 0, 5.0f);
            float prev = s.State.LineLength;
            float d1 = 0f, d125 = 0f;
            for (int k = 1; k <= 250; k++)
            {
                s.Tick(In(0f, false));
                if (s.Spooled) Assert.Fail("spooled at tick " + k);
                float d = s.State.LineLength - prev;
                if (k == 1) d1 = d;
                if (k == 125) d125 = d;
                prev = s.State.LineLength;
            }
            // 0.12 x (2.5 / 2 + 2.5) = 0.45
            Assert.That(s.State.LineLength - 10f, Is.EqualTo(0.45f).Within(0.01f));
            Assert.That(d1, Is.LessThan(d125));
        }

        [Test]
        public void FightMotion_BulldogCalmDrift_StopsWhenExhausted()
        {
            FightSim s = Make(Motion(FishingTestData.ProfileBulldog), 0.375f, 4242, 10f);
            var st = new FightState { FishStamina = 0.06f, Phase = FightPhase.Burst, BurstDir = 0, PhaseTimer = 10f, LineLength = 10f };
            s.ForceState(in st);
            int guard = 0;
            while (!s.Exhausted && guard++ < 200) s.Tick(In(0f, false));
            Assert.That(s.Exhausted, Is.True);
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm));
            float line = s.State.LineLength;
            for (int i = 1; i <= 100; i++)
            {
                s.Tick(In(0f, false));
                if (s.State.LineLength != line) Assert.Fail("line moved at tick " + i + ": " + line + " -> " + s.State.LineLength);
            }
            Assert.That(s.Exhausted, Is.True);
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm));
        }

        static FightInput DarterInput(in FightState s, int i) =>
            In(-Math.Max(-1f, Math.Min(1f, s.Lateral * 5f)), i % 100 == 0);

        [Test]
        public void FightMotion_DarterMicroBursts_FlipRateInRangeAndGapRespected()
        {
            int flips = 0, flipDraws = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                FightSim s = Make(Motion(FishingTestData.ProfileDarter), 0.375f, seed, 10f);
                var st = new FightState { Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 1000f, FishStamina = 1f, LineLength = 10f };
                s.ForceState(in st);
                int drawsBefore = s.DrawCount;
                int lastFlip = -1000;
                for (int i = 0; i < 750; i++)
                {
                    sbyte dirBefore = s.State.BurstDir;
                    FightInput input = DarterInput(s.State, i);
                    s.Tick(input);
                    if (s.State.Outcome != FightOutcome.None) Assert.Fail("seed " + seed + ": fight ended at tick " + (i + 1) + " " + s.State.Outcome);
                    if (s.State.BurstDir != 0 && dirBefore != 0 && s.State.BurstDir != dirBefore)   // a real flip, not exhaustion
                    {
                        flips++;
                        if ((i - lastFlip) * Dt < 0.3f - 1e-4f) Assert.Fail("seed " + seed + ": flip gap " + (i - lastFlip) * Dt + " s");
                        lastFlip = i;
                    }
                }
                flipDraws += s.DrawCount - drawsBefore;
            }
            Assert.That(flipDraws, Is.GreaterThan(5000), "the hazard estimate needs thousands of flip checks (exhaustion must not cut the bursts short)");
            double hazard = flips / (flipDraws * (double)Dt);
            Assert.That(hazard, Is.InRange(0.8, 1.2), "flips " + flips + ", draws " + flipDraws);

            // Natural fight: burst and calm durations stay inside the darter ranges.
            FightSim n = Make(Motion(FishingTestData.ProfileDarter), 0.375f, 3, 10f);
            var bursts = new List<float>();
            var calms = new List<float>();
            int run = 0;
            bool firstCalm = true;
            for (int i = 0; i < 3000; i++)
            {
                FightPhase before = n.State.Phase;
                FightInput input = DarterInput(n.State, i);
                n.Tick(input);
                run++;
                if (n.Exhausted) break;                          // exhaustion cuts a burst short; the ranges apply to scheduled phases
                if (n.State.Phase != before)
                {
                    float seconds = run * Dt;
                    if (before == FightPhase.Burst) bursts.Add(seconds);
                    else if (firstCalm) firstCalm = false;       // the 0.8 s lead-in
                    else calms.Add(seconds);
                    run = 0;
                }
                if (n.State.Outcome != FightOutcome.None) break;
            }
            Assert.That(bursts.Count, Is.GreaterThan(5), "bursts seen");
            Assert.That(calms.Count, Is.GreaterThan(5), "calms seen");
            foreach (float b in bursts) Assert.That(b, Is.InRange(0.4f - 0.021f, 0.9f + 0.021f), "burst duration");
            foreach (float c in calms) Assert.That(c, Is.InRange(0.3f - 0.021f, 0.8f + 0.021f), "calm duration");
        }

        [Test]
        public void FightMotion_ThrasherJitter_LateralSpeedVariesPerBurstWithinBounds()
        {
            FightSim s = Make(Motion(FishingTestData.ProfileThrasher));
            float min = float.MaxValue, max = float.MinValue;
            int sideRuns = 0;
            for (int n = 0; n < 60; n++)
            {
                var st = new FightState { Phase = FightPhase.Calm, PhaseTimer = 0.02f, Lateral = 0f, FishStamina = 1f, LineLength = 10f };
                s.ForceState(in st);
                int d0 = s.DrawCount;
                s.Tick(In(0f, true));                                   // burst starts
                Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Burst), "burst " + n);
                Assert.That(s.DrawCount - d0, Is.EqualTo(4), "burst-start draws (3 + jitter) at burst " + n);
                float mult = s.BurstLateralMult;
                Assert.That(mult, Is.InRange(0.7f, 1.3f));
                min = Math.Min(min, mult); max = Math.Max(max, mult);

                float lat0 = s.State.Lateral;
                s.Tick(In(0f, true));                                   // a flip (if any) happens before the push
                if (s.State.BurstDir != 0)
                {
                    sideRuns++;
                    float want = s.State.BurstDir * s.EffectivePull * 1.0f * mult * 0.02f;
                    Assert.That(s.State.Lateral - lat0, Is.EqualTo(want).Within(1e-6f), "dLateral at burst " + n);
                }
                Assert.That(s.DrawCount - d0, Is.InRange(4, 5), "only flip draws follow");
            }
            Assert.That(max - min, Is.GreaterThan(0.2f), "multipliers must vary");
            Assert.That(sideRuns, Is.GreaterThan(0));
        }

        [Test]
        public void FightMotion_Pity_SnapsLaterThanNormalSameSeed()
        {
            FishFightProfile runner = Base(FishingTestData.ProfileRunner);
            runner.FlipRate = 0f;
            FightSim normal = Make(runner, 0.375f, 777, 18f, false);
            FightSim pity = Make(runner, 0.375f, 777, 18f, true);
            RodStats rod = Handmade();
            FishingTuning t = Tun();
            var plain = new FightSim(in runner, in rod, in t, 0.375f, 777, 18f, false);   // argument omitted == pity false
            normal.ForcePhase(FightPhase.Burst, 1, 10f);
            pity.ForcePhase(FightPhase.Burst, 1, 10f);
            plain.ForcePhase(FightPhase.Burst, 1, 10f);
            for (int i = 1; i <= 400; i++)
            {
                normal.Tick(In(0f, true));
                pity.Tick(In(0f, true));
                plain.Tick(In(0f, true));
                if (normal.DrawCount != pity.DrawCount) Assert.Fail("draw counts differ at tick " + i);
                if (!SameState(normal.State, plain.State) || normal.DrawCount != plain.DrawCount) Assert.Fail("default ctor differs from pity:false at tick " + i);
            }
            Assert.That(normal.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(pity.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(normal.State.Elapsed, Is.EqualTo(2.84f).Within(0.06f));
            Assert.That(pity.State.Elapsed, Is.GreaterThan(normal.State.Elapsed + 0.4f));
            Assert.That(pity.State.Elapsed, Is.InRange(3.2f, 3.5f));
        }

        // Starts a burst from a forced Calm state (profile Steady: DiveChance 0, no jitter, so every start draws exactly 3 values)
        // and counts the burst directions chosen.
        static void SideRunDirectionCounts(float lateral, int starts, uint seed, out int minus, out int plus)
        {
            FightSim s = Make(Base(FishingTestData.ProfileSteady), 0.375f, seed);
            minus = 0; plus = 0;
            for (int i = 0; i < starts; i++)
            {
                var st = new FightState { Phase = FightPhase.Calm, PhaseTimer = 0.02f, Lateral = lateral, FishStamina = 1f, LineLength = 10f };
                s.ForceState(in st);
                int d0 = s.DrawCount;
                s.Tick(In(0f, true));
                if (s.State.Phase != FightPhase.Burst) Assert.Fail("burst " + i + " did not start");
                if (s.DrawCount - d0 != 3) Assert.Fail("burst start " + i + " made " + (s.DrawCount - d0) + " draws, want 3");
                if (s.State.BurstDir < 0) minus++;
                else if (s.State.BurstDir > 0) plus++;
                else Assert.Fail("a dive from a DiveChance 0 profile");
            }
        }

        // Catches: the 0.875 away-share changed (0.75 = 'one draw, 75% away' mistake, or 1.0), away/toward swapped, the mirror case
        // for negative lateral broken, the centre deadzone (|lateral| < 0.1 -> 50/50) ignored, or an extra/missing draw at burst start.
        [Test]
        public void FightMotion_SideRunDirection_EightySevenPointFivePercentAwayFromCurrentSide()
        {
            const int n = 20000;
            SideRunDirectionCounts(+0.5f, n, 11, out int minus, out int plus);
            Assert.That(minus / (double)n, Is.EqualTo(0.875).Within(0.03), "lateral +0.5: away is -1; counts -/+ " + minus + "/" + plus);
            SideRunDirectionCounts(-0.5f, n, 12, out minus, out plus);
            Assert.That(plus / (double)n, Is.EqualTo(0.875).Within(0.03), "lateral -0.5: away is +1; counts -/+ " + minus + "/" + plus);
            SideRunDirectionCounts(0f, n, 13, out minus, out plus);
            Assert.That(minus / (double)n, Is.EqualTo(0.5).Within(0.03), "lateral 0: 50/50; counts -/+ " + minus + "/" + plus);
            SideRunDirectionCounts(0.05f, n, 14, out minus, out plus);
            Assert.That(minus / (double)n, Is.EqualTo(0.5).Within(0.03), "lateral 0.05 (inside the deadzone): 50/50; counts -/+ " + minus + "/" + plus);
        }

        // Catches: pity changing anything but the strain gain: lateral, line length or stamina differing from the normal sim on any tick,
        // pity applied to tension with another factor, or pity multiplying relax (tension would no longer be a clean 0.85x while rising).
        [Test]
        public void FightMotion_Pity_OnlyTensionScalesLateralLineStaminaIdentical()
        {
            FishFightProfile runner = Base(FishingTestData.ProfileRunner);
            runner.FlipRate = 0f;
            FightSim normal = Make(runner, 0.375f, 777, 18f, false);
            FightSim pity = Make(runner, 0.375f, 777, 18f, true);
            normal.ForcePhase(FightPhase.Burst, 1, 10f);
            pity.ForcePhase(FightPhase.Burst, 1, 10f);
            int compared = 0;
            float maxNormalTension = 0f;
            for (int i = 1; i <= 400; i++)
            {
                normal.Tick(In(0f, true));
                pity.Tick(In(0f, true));
                if (normal.State.Lateral != pity.State.Lateral) Assert.Fail("Lateral differs at tick " + i + ": " + normal.State.Lateral + " vs " + pity.State.Lateral);
                if (normal.State.LineLength != pity.State.LineLength) Assert.Fail("LineLength differs at tick " + i);
                if (normal.State.FishStamina != pity.State.FishStamina) Assert.Fail("FishStamina differs at tick " + i);
                if (normal.State.Tension > 0f && normal.State.Tension < 1f)
                {
                    if (Math.Abs(pity.State.Tension - 0.85f * normal.State.Tension) > 1e-5f)
                        Assert.Fail("tick " + i + ": pity tension " + pity.State.Tension + " is not 0.85 x " + normal.State.Tension);
                    compared++;
                    maxNormalTension = Math.Max(maxNormalTension, normal.State.Tension);
                }
                if (normal.State.Outcome != FightOutcome.None) break;
            }
            Assert.That(compared, Is.GreaterThanOrEqualTo(80), "ticks compared while tension was rising");
            Assert.That(maxNormalTension, Is.GreaterThan(0.8f), "the comparison must cover the high-tension range");
            Assert.That(normal.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
        }

        [Test]
        public void FightMotion_AllProfiles_SameSeedSameState()
        {
            for (int p = 0; p < 5; p++)
            {
                FightSim a = Make(Motion(p), 0.375f, 555);
                FightSim b = Make(Motion(p), 0.375f, 555);
                FightSim c = Make(Motion(p), 0.375f, 556);
                bool differs = false;
                for (int i = 0; i < 3000; i++)
                {
                    FightInput input = In((float)Math.Sin(i * 0.05), i % 7 != 0);
                    a.Tick(input);
                    b.Tick(input);
                    c.Tick(input);
                    if (!SameState(a.State, b.State)) Assert.Fail("profile " + p + ": same seed differs at tick " + (i + 1));
                    if (!SameState(a.State, c.State)) differs = true;
                    if (a.State.Outcome != FightOutcome.None && c.State.Outcome != FightOutcome.None) break;
                }
                Assert.That(differs, Is.True, "profile " + p + ": seed 556 must differ from seed 555 somewhere");
            }
        }
    }
}
