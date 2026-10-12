using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Spec 9.5 hand-worked checks and FightSim rules (T7). Profiles are the base spec 9.6 columns (every motion field 0).
    // The hand-check copies of Runner / Bulldog / Thrasher set FlipRate 0 and use weightT 0.375 (WeightFactor 1.0): the spec
    // arithmetic assumes no flip (ruling B-Q5).
    public class FightSimTests
    {
        const float Dt = FightSim.Step;

        static FishingTuning Tun() => FishingTestData.Tuning();
        static RodStats Handmade() => FishingTestData.Rods()[FishingTestData.RodHandmade];
        static FishFightProfile Base(int i) => FishingTestData.BaseProfiles()[i];
        static FishFightProfile NoFlip(int i) { FishFightProfile p = Base(i); p.FlipRate = 0f; return p; }
        static FightInput In(float steer, bool reel) => new FightInput(steer, reel);

        static FightSim Make(FishFightProfile f, float weightT = 0.375f, uint seed = 4242, float line = 18f, bool assist = false, bool pity = false)
        {
            RodStats rod = Handmade();
            FishingTuning t = Tun();
            return new FightSim(in f, in rod, in t, weightT, seed, line, assist, pity);
        }

        static FightSim MakeJunk(uint seed, float line)
        {
            RodStats rod = Handmade();
            FishingTuning t = Tun();
            return FightSim.ForJunk(in rod, in t, seed, line);
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
            return a.LineLength == b.LineLength && a.Lateral == b.Lateral && a.Tension == b.Tension && a.FishStamina == b.FishStamina
                && a.Phase == b.Phase && a.PhaseTimer == b.PhaseTimer && a.BurstDir == b.BurstDir
                && a.SnapTimer == b.SnapTimer && a.PinnedTimer == b.PinnedTimer && a.SlackTimer == b.SlackTimer && a.Elapsed == b.Elapsed
                && a.NearSnapRecoveries == b.NearSnapRecoveries && a.MaxTension == b.MaxTension
                && a.Outcome == b.Outcome && a.Reason == b.Reason;
        }

        [Test]
        public void FightSim_FirstPhase_IsCalmLeadIn()
        {
            FightSim s = Make(Base(FishingTestData.ProfileBulldog));
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm));
            Assert.That(s.State.PhaseTimer, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(s.DrawCount, Is.EqualTo(0));
            for (int i = 1; i <= 39; i++) s.Tick(In(0f, false));
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm), "still Calm after 39 ticks");
            Assert.That(s.DrawCount, Is.EqualTo(0));
            s.Tick(In(0f, false));
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Burst), "Burst after tick 40");
            Assert.That(s.DrawCount, Is.EqualTo(3));
        }

        [Test]
        public void FightSim_RunnerReelNoSteerSideRun_SnapsBetween2p5And3p2Seconds()
        {
            FightSim s = Make(NoFlip(FishingTestData.ProfileRunner));
            s.ForcePhase(FightPhase.Burst, 1, 10f);
            int firstOut = -1, ended = -1;
            for (int i = 1; i <= 400; i++)
            {
                FightOutcome o = s.Tick(In(0f, true));
                if (firstOut < 0 && Math.Abs(s.State.Lateral) > 0.6f) firstOut = i;
                if (o != FightOutcome.None) { ended = i; break; }
            }
            Assert.That(ended, Is.GreaterThan(0), "fight must end");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(s.State.Elapsed, Is.InRange(2.5f, 3.2f), "snap time (arithmetic says 2.84)");
            Assert.That(firstOut, Is.InRange(44, 46), "first tick with Lateral > 0.6");
        }

        [Test]
        public void FightSim_RunnerFullCounterFourSecondRun_NoSnapTensionPointFiveToPointSeven()
        {
            FightSim s = Make(NoFlip(FishingTestData.ProfileRunner));
            s.ForcePhase(FightPhase.Burst, 1, 4.0f);
            for (int i = 1; i <= 200; i++)
            {
                s.Tick(In(-1f, true));
                if (Math.Abs(s.State.Lateral) >= 0.6f) Assert.Fail("lateral left the window at tick " + i + ": " + s.State.Lateral);
            }
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.None));
            Assert.That(s.State.Tension, Is.InRange(0.5f, 0.7f), "about 0.579");
            Assert.That(s.State.MaxTension, Is.LessThan(1f));
        }

        [Test]
        public void FightSim_SteadyReelNoSteerRun_StaysBelowPointThreeTensionAndPointSixLateral()
        {
            FightSim s = Make(Base(FishingTestData.ProfileSteady), 1.0f);
            s.ForcePhase(FightPhase.Burst, 1, 2.5f);
            float maxT = 0f, maxL = 0f;
            for (int i = 1; i <= 125; i++)
            {
                s.Tick(In(0f, true));
                maxT = Math.Max(maxT, s.State.Tension);
                maxL = Math.Max(maxL, Math.Abs(s.State.Lateral));
            }
            Assert.That(maxT, Is.LessThan(0.3f), "tension");
            Assert.That(maxL, Is.LessThan(0.6f), "lateral");
        }

        [Test]
        public void FightSim_RestFromPointNine_BelowPointThreeAfterOnePointFiveSeconds()
        {
            foreach (bool dive in new[] { false, true })
            {
                FightSim s = Make(Base(FishingTestData.ProfileSteady));
                var st = new FightState
                {
                    Tension = 0.9f, Phase = dive ? FightPhase.Burst : FightPhase.Calm, BurstDir = 0,
                    PhaseTimer = 10f, FishStamina = 1f, LineLength = 10f,
                };
                s.ForceState(in st);
                for (int i = 1; i <= 75; i++) s.Tick(In(0f, false));
                Assert.That(s.State.Tension, Is.EqualTo(0.225f).Within(1e-3f), dive ? "dive" : "calm");
                Assert.That(s.State.Tension, Is.LessThan(0.3f));
            }
        }

        [Test]
        public void FightSim_PositiveBurstDirWithLeftSteer_ReducesLateralAndDrainsStamina()
        {
            FightSim s = Make(Base(FishingTestData.ProfileBulldog));
            var st = new FightState { Lateral = 0.5f, Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, LineLength = 10f };
            s.ForceState(in st);
            for (int i = 1; i <= 10; i++) s.Tick(In(-1f, false));
            // 0.5 - (0.6 - 0.36) x 0.2 = 0.452 (P falls slightly as stamina drains)
            Assert.That(s.State.Lateral, Is.EqualTo(0.452f).Within(2e-3f));
            Assert.That(s.State.FishStamina, Is.EqualTo(0.988f).Within(1e-4f));
        }

        [Test]
        public void FightSim_Dive_DrainsStaminaOnlyWhileNotReeling()
        {
            FightSim s = Make(Base(FishingTestData.ProfileBulldog), 0.375f, 4242, 10f);
            s.ForcePhase(FightPhase.Burst, 0, 10f);
            for (int i = 1; i <= 10; i++) s.Tick(In(0f, true));
            Assert.That(s.State.FishStamina, Is.EqualTo(1.0f), "no drain while reeling");
            for (int i = 1; i <= 10; i++) s.Tick(In(0f, false));
            Assert.That(s.State.FishStamina, Is.EqualTo(0.988f).Within(1e-4f));
        }

        [Test]
        public void FightSim_Exhausted_StopsNewBursts()
        {
            FightSim s = Make(Base(FishingTestData.ProfileRunner));
            var st = new FightState { FishStamina = 0.06f, Phase = FightPhase.Burst, BurstDir = 0, PhaseTimer = 10f, LineLength = 29f };
            s.ForceState(in st);
            int guard = 0;
            while (!s.Exhausted && guard++ < 200) s.Tick(In(0f, false));
            Assert.That(s.Exhausted, Is.True, "exhaustion reached");
            Assert.That(s.State.Phase, Is.EqualTo(FightPhase.Calm), "Calm on the exhaustion tick");
            int draws = s.DrawCount;
            for (int i = 1; i <= 500; i++)
            {
                s.Tick(In(0f, true));
                if (s.State.Phase != FightPhase.Calm) Assert.Fail("left Calm at tick " + i);
                if (s.State.Outcome != FightOutcome.None) Assert.Fail("fight ended at tick " + i + ": " + s.State.Outcome);
            }
            Assert.That(s.DrawCount, Is.EqualTo(draws), "no draws after exhaustion");
        }

        [Test]
        public void FightSim_Pinned_EscapesAtFiveSeconds()
        {
            FightSim s = Make(Base(FishingTestData.ProfileSteady));
            var st = new FightState { Lateral = 1f, Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, LineLength = 10f };
            s.ForceState(in st);
            int ended = RunUntilEnd(s, In(0f, false), 400);
            Assert.That(ended, Is.EqualTo(250));
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.Pinned));
            Assert.That(s.State.Elapsed, Is.EqualTo(5.0f).Within(0.021f));
        }

        [Test]
        public void FightSim_NoReel_EscapesThrewHookAtTwelveSeconds()
        {
            FightSim s = Make(Base(FishingTestData.ProfileSteady));
            var st = new FightState { Phase = FightPhase.Calm, PhaseTimer = 100f, FishStamina = 1f, LineLength = 10f };
            s.ForceState(in st);
            int ended = RunUntilEnd(s, In(0.2f, false), 1000);
            Assert.That(ended, Is.EqualTo(600));
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
            Assert.That(s.State.Elapsed, Is.EqualTo(12.0f).Within(0.021f));
        }

        [Test]
        public void FightSim_Spooled_ClampsLineAndAddsStrain()
        {
            FightSim a = Make(NoFlip(FishingTestData.ProfileRunner));
            FightSim b = Make(NoFlip(FishingTestData.ProfileRunner));
            var sa = new FightState { Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, LineLength = 29.99f, FishStamina = 1f };
            var sb = new FightState { Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, LineLength = 20f, FishStamina = 1f };
            a.ForceState(in sa);
            b.ForceState(in sb);
            a.Tick(In(0f, true));
            b.Tick(In(0f, true));
            Assert.That(a.Spooled, Is.True);
            Assert.That(b.Spooled, Is.False);
            Assert.That(a.State.LineLength, Is.EqualTo(30f));
            // 0.5 (SpoolStrain) x 0.85 (P) x 0.02
            Assert.That(a.State.Tension - b.State.Tension, Is.EqualTo(0.0085f).Within(1e-5f));
        }

        [Test]
        public void FightSim_TensionTouchesOneShorterThanGrace_DoesNotSnap()
        {
            var st = new FightState { Tension = 1f, Lateral = 0.9f, Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, LineLength = 15f };

            FightSim s = Make(NoFlip(FishingTestData.ProfileRunner));
            s.ForceState(in st);
            for (int i = 1; i <= 12; i++)
                Assert.That(s.Tick(In(0f, true)), Is.EqualTo(FightOutcome.None), "tick " + i);
            Assert.That(s.State.SnapTimer, Is.EqualTo(0.24f).Within(1e-4f));
            Assert.That(s.Tick(In(0f, false)), Is.EqualTo(FightOutcome.None), "tick 13 without reel");
            Assert.That(s.State.Tension, Is.EqualTo(0.995f).Within(1e-4f));
            Assert.That(s.State.SnapTimer, Is.EqualTo(0f));
            Assert.That(s.State.MaxTension, Is.EqualTo(1f));

            // Control: keep reeling and it snaps on tick 13.
            FightSim c = Make(NoFlip(FishingTestData.ProfileRunner));
            c.ForceState(in st);
            Assert.That(RunUntilEnd(c, In(0f, true), 30), Is.EqualTo(13));
            Assert.That(c.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
        }

        [Test]
        public void FightSim_LineAtLandDistanceWhileReeling_Lands()
        {
            FightSim s = Make(Base(FishingTestData.ProfileBulldog), 0.375f, 4242, 2.51f);
            for (int i = 1; i <= 25; i++)
                Assert.That(s.Tick(In(0f, true)), Is.EqualTo(FightOutcome.None), "tick " + i);
            Assert.That(s.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Landed), "tick 26");

            FightSim t = Make(Base(FishingTestData.ProfileBulldog), 0.375f, 4242, 1.5f);
            for (int i = 1; i <= 5; i++) t.Tick(In(0f, false));
            Assert.That(t.State.Outcome, Is.EqualTo(FightOutcome.None), "no landing without reeling");
        }

        [Test]
        public void FightSim_NeverReeling_EndsBySlackEscape()
        {
            FightSim s = Make(Base(FishingTestData.ProfileSteady), 0.375f, 7, 10f);
            int ended = RunUntilEnd(s, In(0f, false), 1000);
            Assert.That(ended, Is.GreaterThan(0));
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(s.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
            Assert.That(s.State.Elapsed, Is.EqualTo(12.0f).Within(0.021f));
        }

        [Test]
        public void FightSim_SameSeedAndInputs_IdenticalState()
        {
            FightSim a = Make(Base(FishingTestData.ProfileRunner), 0.375f, 99);
            FightSim b = Make(Base(FishingTestData.ProfileRunner), 0.375f, 99);
            for (int i = 0; i < 3000; i++)
            {
                FightInput input = In((float)Math.Sin(i * 0.05), i % 7 != 0);
                a.Tick(input);
                b.Tick(input);
                if (!SameState(a.State, b.State)) Assert.Fail("states differ at tick " + (i + 1));
                if (a.State.Outcome != FightOutcome.None) break;
            }
        }

        [Test]
        public void FightSim_Assist_UsesLongGraceAndLowerStrain()
        {
            // A: strain scale 0.6 -> dTension = 0.6 x 0.255 x 0.02
            FightSim a = Make(NoFlip(FishingTestData.ProfileRunner), 0.375f, 4242, 18f, assist: true);
            a.ForcePhase(FightPhase.Burst, 1, 10f);
            a.Tick(In(0f, true));
            Assert.That(a.State.Tension, Is.EqualTo(0.6f * 0.255f * 0.02f).Within(1e-5f));

            // B: grace 0.75 s -> no snap on tick 37, Snapped on tick 38
            FightSim b = Make(NoFlip(FishingTestData.ProfileRunner), 0.375f, 4242, 18f, assist: true);
            var st = new FightState { Tension = 1f, Lateral = 0.9f, Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, LineLength = 15f };
            b.ForceState(in st);
            for (int i = 1; i <= 37; i++)
                Assert.That(b.Tick(In(0f, true)), Is.EqualTo(FightOutcome.None), "tick " + i);
            Assert.That(b.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Snapped), "tick 38");
            Assert.That(b.State.Elapsed, Is.EqualTo(0.76f).Within(0.011f));
        }

        [Test]
        public void FightSim_NearSnapRecovery_Counted()
        {
            FightSim s = Make(NoFlip(FishingTestData.ProfileRunner));
            var st = new FightState { Tension = 0.79f, Lateral = 0.7f, Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, LineLength = 15f };
            s.ForceState(in st);
            s.Tick(In(0f, true));
            s.Tick(In(0f, true));
            Assert.That(s.State.Tension, Is.EqualTo(0.808f).Within(2e-3f));
            int recoveredAt = -1;
            for (int i = 1; i <= 80; i++)
            {
                s.Tick(In(0f, false));
                if (recoveredAt < 0 && s.State.NearSnapRecoveries == 1) recoveredAt = i;
            }
            Assert.That(s.State.NearSnapRecoveries, Is.EqualTo(1));
            Assert.That(recoveredAt, Is.InRange(58, 66), "falls -0.25 /s to under 0.5 near tick 62");

            // A second rise to 0.75 (never above 0.8) then rest leaves the count at 1.
            int guard = 0;
            while (s.State.Tension < 0.75f && guard++ < 200) s.Tick(In(0f, true));
            Assert.That(s.State.Tension, Is.InRange(0.75f, 0.8f));
            for (int i = 1; i <= 50; i++) s.Tick(In(0f, false));
            Assert.That(s.State.NearSnapRecoveries, Is.EqualTo(1));
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.None));
        }

        [Test]
        public void FightSim_Junk_DragsAtJunkSpeedWithoutTension()
        {
            FightSim s = MakeJunk(5, 10f);
            Assert.That(s.IsJunk, Is.True);
            for (int i = 1; i <= 50; i++) s.Tick(In(0f, true));
            Assert.That(s.State.LineLength, Is.EqualTo(7.0f).Within(1e-3f));
            Assert.That(s.State.Tension, Is.EqualTo(0f));
            Assert.That(s.State.MaxTension, Is.EqualTo(0f));
            Assert.That(s.DrawCount, Is.EqualTo(0));
            int landedAt = -1;
            for (int i = 51; i <= 300; i++)
                if (s.Tick(In(0f, true)) == FightOutcome.Landed) { landedAt = i; break; }
            Assert.That(landedAt, Is.EqualTo(134));
            Assert.That(s.State.LineLength, Is.EqualTo(1.96f).Within(1e-3f));
            Assert.That(s.DrawCount, Is.EqualTo(0));

            FightSim idle = MakeJunk(5, 10f);
            Assert.That(RunUntilEnd(idle, In(0f, false), 1000), Is.EqualTo(600));
            Assert.That(idle.State.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(idle.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
        }

        // A side-run fish with no line run-out: tension stays pinned at 1 while reeling, and the reel brings the line in at 0.004 m/tick.
        static FishFightProfile PinnedFish() { FishFightProfile p = NoFlip(FishingTestData.ProfileSteady); p.PullStrength = 0.8f; p.RunSpeed = 0f; return p; }

        // Same-tick ending priority (step 8: Landed, Snapped, Pinned, ThrewHook). Every state below satisfies two endings after one tick.

        // Catches: checking Snapped before Landed (tension 1 for 0.26 s and the line in on the same tick must land the fish).
        [Test]
        public void FightSim_SameTick_LandedBeatsSnapped()
        {
            // Tension 1, SnapTimer 0.24 (+0.02 >= grace 0.25), line 0.002 above LandDistance (reel 0.004/tick), reel on.
            FightSim a = Make(PinnedFish());
            var both = new FightState { Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, Tension = 1f, SnapTimer = 0.24f, LineLength = 2.002f };
            a.ForceState(in both);
            Assert.That(a.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Landed));
            Assert.That(a.State.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(a.State.LineLength, Is.LessThanOrEqualTo(2f), "the line really was in");
            Assert.That(a.State.SnapTimer, Is.GreaterThanOrEqualTo(0.25f), "the snap condition really was met on the same tick");

            // Same state, line far from the landing: only the snap fires (also catches a Landed check without the line condition).
            FightSim b = Make(PinnedFish());
            var snapOnly = both; snapOnly.LineLength = 10f;
            b.ForceState(in snapOnly);
            Assert.That(b.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Snapped));
        }

        // Catches: checking Pinned before Snapped.
        [Test]
        public void FightSim_SameTick_SnappedBeatsPinned()
        {
            FightSim c = Make(PinnedFish());
            var snapPin = new FightState { Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 10f, FishStamina = 1f, Tension = 1f, SnapTimer = 0.24f,
                                           Lateral = 0.97f, PinnedTimer = 4.99f, LineLength = 10f };
            c.ForceState(in snapPin);
            Assert.That(c.Tick(In(0f, true)), Is.EqualTo(FightOutcome.Snapped));
            Assert.That(c.State.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(c.State.PinnedTimer, Is.GreaterThanOrEqualTo(5f), "the pin condition really was met on the same tick");
        }

        // Catches: checking ThrewHook before Pinned (a pinned fish with a slack line must report Pinned), and ThrewHook never firing alone.
        [Test]
        public void FightSim_SameTick_PinnedBeatsThrewHook()
        {
            FightSim d = Make(PinnedFish());
            var pinSlack = new FightState { Phase = FightPhase.Calm, PhaseTimer = 10f, FishStamina = 1f, Lateral = 0.97f, PinnedTimer = 4.99f, SlackTimer = 11.99f, LineLength = 10f };
            d.ForceState(in pinSlack);
            Assert.That(d.Tick(In(0f, false)), Is.EqualTo(FightOutcome.Escaped));
            Assert.That(d.State.Reason, Is.EqualTo(EscapeReason.Pinned));
            Assert.That(d.State.SlackTimer, Is.GreaterThanOrEqualTo(12f), "the slack condition really was met on the same tick");

            // Control: slack alone ends as ThrewHook.
            FightSim e = Make(PinnedFish());
            var slackOnly = new FightState { Phase = FightPhase.Calm, PhaseTimer = 10f, FishStamina = 1f, SlackTimer = 11.99f, LineLength = 10f };
            e.ForceState(in slackOnly);
            Assert.That(e.Tick(In(0f, false)), Is.EqualTo(FightOutcome.Escaped));
            Assert.That(e.State.Reason, Is.EqualTo(EscapeReason.ThrewHook));
        }

        // Extra: the spec 9.5 greedy side-run arithmetic for three profiles (profile index, expected snap seconds).
        [TestCase(FishingTestData.ProfileRunner, 2.84f)]
        [TestCase(FishingTestData.ProfileBulldog, 3.76f)]
        [TestCase(FishingTestData.ProfileThrasher, 3.10f)]
        public void FightSim_GreedySideRun_SnapTimesMatchSpecChecks(int profile, float expectedSeconds)
        {
            FightSim s = Make(NoFlip(profile));
            s.ForcePhase(FightPhase.Burst, 1, 10f);
            int ended = RunUntilEnd(s, In(0f, true), 600);
            Assert.That(ended, Is.GreaterThan(0), "fight must end");
            Assert.That(s.State.Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(s.State.Elapsed, Is.EqualTo(expectedSeconds).Within(0.06f));
        }
    }
}
