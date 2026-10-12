// T15 (plan data-tuning section 4): scripted tuning bots (Greedy, CounterReel, Rhythm) that fight the REAL assets through FightSim,
// print distributions and signature-motion stats, and assert the spec 9.12 / 17.2 targets. Bots draw nothing random: seeds are a fixed formula.
// A failing target is fixed by retuning data (assets + FishingSeedData + TunedDeviations), never by changing FightSim.
// This file must not reference FishingSeedData: data comes from AssetDatabase.LoadAssetAtPath + ToCore().
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace FishGame.Tests.EditMode
{
    [Category("Tuning")]
    public class FishingBots
    {
        // Acceptance 8 escape hatch: key = test name, value = one line with the measured numbers and the reason. Each entry needs the
        // controller's approval (ruling D-Q6) and is carried into the T30 acceptance checklist.
        static readonly Dictionary<string, string> TuningItems = new Dictionary<string, string>();

        // Ruling D-Q3: equal to FishingConstants.CenterDeadzone; the counter-steer holds until the fish is this far past centre.
        const float CounterHold = 0.1f;
        const float EaseOnTension = 0.8f;
        const float EaseOffTension = 0.5f;
        const int FightsPerBatch = 200;

        enum BotKind { Greedy, CounterReel, Rhythm }

        static readonly BotKind[] Bots = { BotKind.Greedy, BotKind.CounterReel, BotKind.Rhythm };
        static readonly string[] ProfileIds = { "steady", "darter", "bulldog", "thrasher", "runner" };
        static readonly string[] RodIds = { "rod_handmade", "rod_twig" };
        static readonly float[] Weights = { 0.5f, 0.9f };

        // ------------------------------------------------------------------ data

        sealed class Loaded
        {
            public FishingTuning T;
            public readonly Dictionary<string, RodStats> Rods = new Dictionary<string, RodStats>();
            public readonly Dictionary<string, FishFightProfile> Profiles = new Dictionary<string, FishFightProfile>();
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, "missing " + path + ": run Tools/unity.ps1 build first");
            return a;
        }

        static Loaded LoadData()
        {
            var d = new Loaded();
            d.T = Load<FishingTuningDef>(AssetPaths.FishingTuningAsset).ToCore();
            foreach (string r in RodIds) d.Rods[r] = Load<RodDef>(AssetPaths.RodsFolder + "/" + r + ".asset").ToCore();
            foreach (string p in ProfileIds) d.Profiles[p] = Load<FightProfileDef>(AssetPaths.FightProfilesFolder + "/" + p + ".asset").ToCore();
            return d;
        }

        // ------------------------------------------------------------------ bots

        static FightInput Decide(BotKind kind, in FightState s, ref bool easing)
        {
            bool sideRun = s.Phase == FightPhase.Burst && s.BurstDir != 0;
            bool dive = s.Phase == FightPhase.Burst && s.BurstDir == 0;
            bool counter = sideRun && s.BurstDir * s.Lateral > -CounterHold;
            float counterSteer = counter ? -s.BurstDir : 0f;
            switch (kind)
            {
                case BotKind.Greedy:
                    return new FightInput(0f, true);
                case BotKind.CounterReel:
                    return new FightInput(counterSteer, true);
                default:
                    if (s.Tension >= EaseOnTension) easing = true;
                    else if (easing && s.Tension < EaseOffTension) easing = false;
                    return new FightInput(counterSteer, !easing && !dive);
            }
        }

        // ------------------------------------------------------------------ one fight

        struct FightRecord
        {
            public FightOutcome Outcome;     // None = bot-side Timeout
            public EscapeReason Reason;
            public float Elapsed, MaxTension;
            public int Recoveries;
        }

        static uint SeedFor(int i)
        {
            return unchecked(2654435761u * (uint)(i + 1));
        }

        static FightRecord RunFight(BotKind kind, in FishFightProfile p, in RodStats rod, in FishingTuning t, float weightT, uint seed, SigAccum sig)
        {
            var sim = new FightSim(p, rod, t, weightT, seed, rod.CastMaxDist, false, false);
            bool easing = false;
            int maxTicks = (int)Math.Ceiling(t.MaxFightTime / FightSim.Step);
            for (int k = 0; k < maxTicks; k++)
            {
                FightState b = sim.State;
                FightInput input = Decide(kind, b, ref easing);
                bool exhaustedBefore = sim.Exhausted;
                FightOutcome o = sim.Tick(input);
                if (sig != null) sig.Collect(b, sim.State, input, rod, p, t, sim.WeightFactor, exhaustedBefore);
                if (o != FightOutcome.None) break;
            }
            if (sig != null) sig.EndFight();
            FightState f = sim.State;
            return new FightRecord { Outcome = f.Outcome, Reason = f.Reason, Elapsed = f.Elapsed, MaxTension = f.MaxTension, Recoveries = f.NearSnapRecoveries };
        }

        // ------------------------------------------------------------------ batch stats

        sealed class BatchStats
        {
            public int N, Landed, Snapped, Escaped, Pinned, ThrewHook, Timeouts;
            public double LandPct, SnapPct, EscPct, PinPct, SlackPct, TimeoutPct;
            public double LenP10 = double.NaN, LenMed = double.NaN, LenP90 = double.NaN, SnapTMed = double.NaN;
            public double RecMean, RecAtLeast1Pct, CleanOfLandedPct, CleanOfAllPct, MaxTMed;
            public bool AllFinite = true;
        }

        static double Pct(int count, int n)
        {
            return n == 0 ? 0.0 : 100.0 * count / n;
        }

        static double Nearest(List<float> sorted, double q)
        {
            if (sorted.Count == 0) return double.NaN;
            int idx = (int)Math.Ceiling(q * sorted.Count) - 1;
            if (idx < 0) idx = 0;
            if (idx >= sorted.Count) idx = sorted.Count - 1;
            return sorted[idx];
        }

        static BatchStats Compute(List<FightRecord> fights, float cleanMax)
        {
            var s = new BatchStats { N = fights.Count };
            var landed = new List<float>();
            var snapped = new List<float>();
            var maxT = new List<float>();
            int rec1 = 0, clean = 0;
            double recSum = 0;
            foreach (FightRecord f in fights)
            {
                if (float.IsNaN(f.Elapsed) || float.IsInfinity(f.Elapsed) || float.IsNaN(f.MaxTension) || float.IsInfinity(f.MaxTension)) s.AllFinite = false;
                maxT.Add(f.MaxTension);
                recSum += f.Recoveries;
                if (f.Recoveries >= 1) rec1++;
                switch (f.Outcome)
                {
                    case FightOutcome.Landed:
                        s.Landed++; landed.Add(f.Elapsed);
                        if (f.MaxTension < cleanMax) clean++;
                        break;
                    case FightOutcome.Snapped:
                        s.Snapped++; snapped.Add(f.Elapsed);
                        break;
                    case FightOutcome.Escaped:
                        s.Escaped++;
                        if (f.Reason == EscapeReason.Pinned) s.Pinned++;
                        else if (f.Reason == EscapeReason.ThrewHook) s.ThrewHook++;
                        break;
                    default:
                        s.Timeouts++;
                        break;
                }
            }
            landed.Sort(); snapped.Sort(); maxT.Sort();
            s.LandPct = Pct(s.Landed, s.N); s.SnapPct = Pct(s.Snapped, s.N); s.EscPct = Pct(s.Escaped, s.N);
            s.PinPct = Pct(s.Pinned, s.N); s.SlackPct = Pct(s.ThrewHook, s.N); s.TimeoutPct = Pct(s.Timeouts, s.N);
            s.LenP10 = Nearest(landed, 0.1); s.LenMed = Nearest(landed, 0.5); s.LenP90 = Nearest(landed, 0.9);
            s.SnapTMed = Nearest(snapped, 0.5);
            s.RecMean = s.N == 0 ? 0.0 : recSum / s.N;
            s.RecAtLeast1Pct = Pct(rec1, s.N);
            s.CleanOfLandedPct = Pct(clean, s.Landed); s.CleanOfAllPct = Pct(clean, s.N);
            s.MaxTMed = Nearest(maxT, 0.5);
            return s;
        }

        // ------------------------------------------------------------------ signature-motion stats (Rhythm, handmade, w 0.5)

        sealed class SigAccum
        {
            const float Dt = FightSim.Step;
            public double SideRunSec;
            public int Flips;
            public double MinFlipGap = double.PositiveInfinity;
            public double CreepSum; public int CreepTicks;
            public readonly List<double> FullForceTimes = new List<double>();
            public readonly List<double> BurstMedians = new List<double>();
            public int Dives, Bursts;

            // current burst
            bool _open, _side;
            int _burstTicks;
            double _lastFlipTime = double.NaN;
            readonly List<double> _ms = new List<double>();
            readonly List<double> _msTimes = new List<double>();

            void FinishBurst()
            {
                if (!_open) return;
                _open = false;
                if (!_side || _ms.Count * (double)Dt < 0.5) return;
                double mMax = 0;
                foreach (double m in _ms) if (m > mMax) mMax = m;
                for (int i = 0; i < _ms.Count; i++)
                    if (_ms[i] >= 0.95 * mMax) { FullForceTimes.Add(_msTimes[i]); break; }
                _postRamp.Clear();
                for (int i = 0; i < _ms.Count; i++)
                    if (_msTimes[i] >= _ramp - 1e-6) _postRamp.Add(_ms[i]);
                if (_postRamp.Count > 0)
                {
                    _postRamp.Sort();
                    BurstMedians.Add(_postRamp[(int)Math.Ceiling(0.5 * _postRamp.Count) - 1]);
                }
            }

            readonly List<double> _postRamp = new List<double>();
            double _ramp;

            public void EndFight()
            {
                FinishBurst();
            }

            public void Collect(in FightState b, in FightState a, in FightInput input, in RodStats rod, in FishFightProfile p, in FishingTuning t,
                float weightFactor, bool exhaustedBefore)
            {
                bool bBurst = b.Phase == FightPhase.Burst, aBurst = a.Phase == FightPhase.Burst;
                if (bBurst && _open) _burstTicks++;
                double burstTime = _burstTicks * (double)Dt;

                if (bBurst && b.BurstDir != 0 && aBurst)
                {
                    SideRunSec += Dt;
                    if (a.BurstDir == -b.BurstDir)
                    {
                        Flips++;
                        if (!double.IsNaN(_lastFlipTime) && burstTime - _lastFlipTime < MinFlipGap) MinFlipGap = burstTime - _lastFlipTime;
                        _lastFlipTime = burstTime;
                    }
                    if (Math.Abs(a.Lateral) < 0.999f && Math.Abs(b.Lateral) < 0.999f && b.FishStamina > 0.05f)
                    {
                        double push = (a.Lateral - b.Lateral - input.Steer * rod.SteerRate * Dt) / Dt;
                        double m = Math.Abs(push) / (p.PullStrength * weightFactor * b.FishStamina * p.LateralSpeed);
                        _ms.Add(m); _msTimes.Add(burstTime);
                    }
                }

                if (b.Phase == FightPhase.Calm && a.Phase == FightPhase.Calm && !exhaustedBefore
                    && a.LineLength > 0f && a.LineLength < rod.CastMaxDist + t.SpoolExtra)
                {
                    CreepSum += (a.LineLength - b.LineLength) + (input.Reel ? rod.ReelSpeed * Dt : 0f);
                    CreepTicks++;
                }

                if (!bBurst && aBurst)
                {
                    FinishBurst();
                    _open = true; _side = a.BurstDir != 0; _burstTicks = 0; _lastFlipTime = double.NaN; _ramp = p.BurstRamp;
                    _ms.Clear(); _msTimes.Clear();
                    Bursts++;
                    if (a.BurstDir == 0) Dives++;
                }
                else if (bBurst && !aBurst)
                {
                    FinishBurst();
                }
            }
        }

        // ------------------------------------------------------------------ the matrix

        static readonly Dictionary<string, BatchStats> s_cache = new Dictionary<string, BatchStats>();
        static readonly Dictionary<string, SigAccum> s_sig = new Dictionary<string, SigAccum>();
        static double s_matrixSeconds = double.NaN;
        static bool s_matrixDone;

        static string Key(BotKind bot, string profile, string rod, float w)
        {
            return bot + "|" + profile + "|" + rod + "|" + w.ToString("F1", CultureInfo.InvariantCulture);
        }

        static void EnsureMatrix(Loaded d)
        {
            if (s_matrixDone) return;
            var sw = Stopwatch.StartNew();
            foreach (string profile in ProfileIds)
                foreach (string rod in RodIds)
                    foreach (float w in Weights)
                        foreach (BotKind bot in Bots)
                        {
                            bool wantSig = bot == BotKind.Rhythm && rod == "rod_handmade" && w == 0.5f;
                            SigAccum sig = wantSig ? new SigAccum() : null;
                            FishFightProfile p = d.Profiles[profile];
                            RodStats r = d.Rods[rod];
                            var fights = new List<FightRecord>(FightsPerBatch);
                            for (int i = 0; i < FightsPerBatch; i++) fights.Add(RunFight(bot, p, r, d.T, w, SeedFor(i), sig));
                            s_cache[Key(bot, profile, rod, w)] = Compute(fights, d.T.CleanCatchMaxTension);
                            if (wantSig) s_sig[profile] = sig;
                        }
            sw.Stop();
            s_matrixSeconds = sw.Elapsed.TotalSeconds;
            s_matrixDone = true;
        }

        static BatchStats Batch(Loaded d, BotKind bot, string profile, string rod, float w)
        {
            EnsureMatrix(d);
            return s_cache[Key(bot, profile, rod, w)];
        }

        // ------------------------------------------------------------------ formatting

        static string N(double v, string fmt)
        {
            return double.IsNaN(v) ? "n/a" : v.ToString(fmt, CultureInfo.InvariantCulture);
        }
        static string F1(double v) { return N(v, "F1"); }
        static string F2(double v) { return N(v, "F2"); }
        static string F3(double v) { return N(v, "F3"); }
        static string P(double v) { return N(v, "F1") + "%"; }
        static string Sec(double v) { return double.IsNaN(v) ? "n/a" : N(v, "F1") + "s"; }

        static string BotLine(BotKind bot, string profile, string rod, float w, BatchStats s)
        {
            return "FG BOT " + bot + " " + profile + " " + rod + " w=" + F1(w) + " n=" + s.N
                + " land=" + P(s.LandPct) + " snap=" + P(s.SnapPct) + " esc=" + P(s.EscPct)
                + " (pin=" + P(s.PinPct) + " slack=" + P(s.SlackPct) + ") timeout=" + P(s.TimeoutPct)
                + " len p10/med/p90=" + F1(s.LenP10) + "/" + F1(s.LenMed) + "/" + F1(s.LenP90)
                + " snapT med=" + F1(s.SnapTMed)
                + " rec mean=" + F2(s.RecMean) + " rec>=1=" + P(s.RecAtLeast1Pct)
                + " clean=" + P(s.CleanOfLandedPct) + "/" + P(s.CleanOfAllPct)
                + " maxT med=" + F2(s.MaxTMed);
        }

        static void WriteLine(string line)
        {
            TestContext.WriteLine(line);
        }

        static void PrintBots(Loaded d, string profile, string rod, float w, params BotKind[] bots)
        {
            foreach (BotKind bot in bots) WriteLine(BotLine(bot, profile, rod, w, Batch(d, bot, profile, rod, w)));
        }

        // ------------------------------------------------------------------ target bookkeeping

        sealed class Targets
        {
            readonly string _test;
            readonly List<string> _failures = new List<string>();
            public Targets(string test) { _test = test; }

            public void Add(bool pass, string measured, string target)
            {
                string status = pass ? "PASS" : (TuningItems.ContainsKey(_test) ? "TUNING-ITEM" : "FAIL");
                WriteLine("FG TARGET " + _test + " " + status + " " + measured + " (target " + target + ")");
                if (!pass) _failures.Add(measured + " (target " + target + ")");
            }

            public void Complete()
            {
                if (_failures.Count == 0)
                {
                    if (TuningItems.ContainsKey(_test)) WriteLine("FG TUNING STALE " + _test + " passes now: remove its TuningItems entry");
                    return;
                }
                if (TuningItems.TryGetValue(_test, out string note)) Assert.Ignore("FG TUNING ITEM " + note);
                Assert.Fail(_test + ": " + string.Join("; ", _failures));
            }
        }

        static bool In(double v, double lo, double hi)
        {
            return !double.IsNaN(v) && v >= lo && v <= hi;
        }

        // ------------------------------------------------------------------ tests

        // The matrix cache is static and survives a domain that is not reloaded between runs; start every run from an empty one.
        [OneTimeSetUp]
        public void ClearBotCaches()
        {
            s_cache.Clear();
            s_sig.Clear();
            s_matrixSeconds = double.NaN;
            s_matrixDone = false;
        }

        static FightState Fs(FightPhase phase, sbyte burstDir, float lateral, float tension)
        {
            return new FightState { Phase = phase, BurstDir = burstDir, Lateral = lateral, Tension = tension };
        }

        // Catches: a bot rule drifting silently (retuning is only meaningful while the bots mean what the plan says): Rhythm reeling during a
        // dive, counter-steering at the wrong side or too long, hysteresis easing off at the wrong thresholds; Greedy/CounterReel gaining brains.
        [Test]
        public void FishingBots_RhythmDecide_PinsDiveEasingAndSteerRules()
        {
            Assert.AreEqual(FishingConstants.CenterDeadzone, CounterHold, "ruling D-Q3: CounterHold == CenterDeadzone");

            bool easing = false;
            FightInput dive = Decide(BotKind.Rhythm, Fs(FightPhase.Burst, 0, 0f, 0f), ref easing);
            Assert.AreEqual(0f, dive.Steer, "dive: no steer");
            Assert.IsFalse(dive.Reel, "dive: Rhythm does not reel");

            easing = false;
            FightInput run = Decide(BotKind.Rhythm, Fs(FightPhase.Burst, 1, 0f, 0f), ref easing);
            Assert.AreEqual(-1f, run.Steer, "side run +1 at centre: counter-steer -1");
            Assert.IsTrue(run.Reel, "side run: Rhythm keeps reeling");

            easing = false;
            FightInput past = Decide(BotKind.Rhythm, Fs(FightPhase.Burst, 1, -0.2f, 0f), ref easing);
            Assert.AreEqual(0f, past.Steer, "side run +1 already past centre beyond CounterHold: stop steering");

            easing = false;
            FightInput calm = Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0f, 0f), ref easing);
            Assert.AreEqual(0f, calm.Steer, "calm: no steer");
            Assert.IsTrue(calm.Reel, "calm: reel");

            easing = false;
            FightInput hot = Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0f, 0.8f), ref easing);
            Assert.IsTrue(easing, "tension 0.8 starts easing");
            Assert.IsFalse(hot.Reel, "tension 0.8: reel off");
            FightInput mid = Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0f, 0.6f), ref easing);
            Assert.IsTrue(easing, "0.6 is inside the hysteresis band: still easing");
            Assert.IsFalse(mid.Reel, "0.6 while easing: reel stays off");
            FightInput cool = Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0f, 0.49f), ref easing);
            Assert.IsFalse(easing, "0.49 clears easing");
            Assert.IsTrue(cool.Reel, "0.49: reel again");

            foreach (FightState s in new[] { Fs(FightPhase.Calm, 0, 0f, 0f), Fs(FightPhase.Burst, 1, 0f, 0.95f), Fs(FightPhase.Burst, 0, 0f, 0.9f) })
            {
                easing = false;
                FightInput g = Decide(BotKind.Greedy, s, ref easing);
                Assert.AreEqual(0f, g.Steer, "Greedy never steers");
                Assert.IsTrue(g.Reel, "Greedy always reels");
            }

            easing = false;
            FightInput counterDive = Decide(BotKind.CounterReel, Fs(FightPhase.Burst, 0, 0f, 0.9f), ref easing);
            Assert.AreEqual(0f, counterDive.Steer, "CounterReel in a dive: no steer");
            Assert.IsTrue(counterDive.Reel, "CounterReel in a dive: still reels");
        }

        // Catches: Bulldog tuned so that a good player cannot or cannot fail: Greedy must snap (reel-and-pray punished), CounterReel must snap
        // sometimes (steering alone is not enough), Rhythm must land safely in 35-55 s (spec 9.12 / 17.2).
        [Test]
        public void FishingBots_BulldogHandmade_MeetsGreedyCounterRhythmTargets()
        {
            Loaded d = LoadData();
            var g = Batch(d, BotKind.Greedy, "bulldog", "rod_handmade", 0.5f);
            var c = Batch(d, BotKind.CounterReel, "bulldog", "rod_handmade", 0.5f);
            var r = Batch(d, BotKind.Rhythm, "bulldog", "rod_handmade", 0.5f);
            PrintBots(d, "bulldog", "rod_handmade", 0.5f, BotKind.Greedy, BotKind.CounterReel, BotKind.Rhythm);
            var t = new Targets(nameof(FishingBots_BulldogHandmade_MeetsGreedyCounterRhythmTargets));
            t.Add(g.SnapPct >= 70.0, "Greedy snap=" + P(g.SnapPct), "snap >= 70%");
            t.Add(In(c.SnapPct, 10.0, 40.0), "CounterReel snap=" + P(c.SnapPct), "snap in [10%, 40%]");
            t.Add(r.SnapPct <= 5.0, "Rhythm snap=" + P(r.SnapPct), "snap <= 5%");
            t.Add(In(r.LenMed, 35.0, 55.0), "Rhythm median=" + Sec(r.LenMed), "median in [35, 55] s");
            t.Complete();
        }

        // Catches: Steady drifting out of 15-30 s median under the Rhythm bot.
        [Test]
        public void FishingBots_SteadyRhythm_MedianFifteenToThirtySeconds()
        {
            MedianTarget(nameof(FishingBots_SteadyRhythm_MedianFifteenToThirtySeconds), "steady", 15.0, 30.0);
        }

        // Catches: Darter drifting out of 15-30 s median under the Rhythm bot.
        [Test]
        public void FishingBots_DarterRhythm_MedianFifteenToThirtySeconds()
        {
            MedianTarget(nameof(FishingBots_DarterRhythm_MedianFifteenToThirtySeconds), "darter", 15.0, 30.0);
        }

        // Catches: Thrasher fights too short (or too long) under the Rhythm bot: 35-55 s.
        [Test]
        public void FishingBots_ThrasherRhythm_MedianThirtyFiveToFiftyFiveSeconds()
        {
            MedianTarget(nameof(FishingBots_ThrasherRhythm_MedianThirtyFiveToFiftyFiveSeconds), "thrasher", 35.0, 55.0);
        }

        static void MedianTarget(string test, string profile, double lo, double hi)
        {
            Loaded d = LoadData();
            var r = Batch(d, BotKind.Rhythm, profile, "rod_handmade", 0.5f);
            PrintBots(d, profile, "rod_handmade", 0.5f, BotKind.Rhythm);
            var t = new Targets(test);
            t.Add(In(r.LenMed, lo, hi), "Rhythm median=" + Sec(r.LenMed), "median in [" + F1(lo) + ", " + F1(hi) + "] s");
            t.Complete();
        }

        // Catches: Runner fights too short, or Runner snapping a Rhythm player too often (spec 9.12: 60-110 s, snaps stay rare).
        [Test]
        public void FishingBots_RunnerRhythm_MedianSixtyToOneTenSnapsAtMostFifteenPercent()
        {
            Loaded d = LoadData();
            var r = Batch(d, BotKind.Rhythm, "runner", "rod_handmade", 0.5f);
            PrintBots(d, "runner", "rod_handmade", 0.5f, BotKind.Rhythm);
            var t = new Targets(nameof(FishingBots_RunnerRhythm_MedianSixtyToOneTenSnapsAtMostFifteenPercent));
            t.Add(In(r.LenMed, 60.0, 110.0), "Rhythm median=" + Sec(r.LenMed), "median in [60, 110] s");
            t.Add(r.SnapPct <= 15.0, "Rhythm snap=" + P(r.SnapPct), "snap <= 15%");
            t.Complete();
        }

        // Catches: the starter Twig rod being unfishable for a careful player on any profile (spec 9.12: Twig lands >= 90% at half weight, >= 75% at 0.9).
        [Test]
        public void FishingBots_TwigRod_RhythmLandsNinetyPercentAtHalfWeightSeventyFiveAtPointNine()
        {
            Loaded d = LoadData();
            var t = new Targets(nameof(FishingBots_TwigRod_RhythmLandsNinetyPercentAtHalfWeightSeventyFiveAtPointNine));
            foreach (string profile in ProfileIds)
            {
                PrintBots(d, profile, "rod_twig", 0.5f, BotKind.Rhythm);
                PrintBots(d, profile, "rod_twig", 0.9f, BotKind.Rhythm);
                var lo = Batch(d, BotKind.Rhythm, profile, "rod_twig", 0.5f);
                var hi = Batch(d, BotKind.Rhythm, profile, "rod_twig", 0.9f);
                t.Add(lo.LandPct >= 90.0, profile + " w=0.5 land=" + P(lo.LandPct), "land >= 90%");
                t.Add(hi.LandPct >= 75.0, profile + " w=0.9 land=" + P(hi.LandPct), "land >= 75%");
            }
            t.Complete();
        }

        // Catches: a NaN or infinite number in any recorded distribution or signature stat. Prints everything the retune reads.
        [Test]
        public void FishingBots_AllProfiles_PrintSignatureMotionAndDistributions()
        {
            Loaded d = LoadData();
            EnsureMatrix(d);
            FishingTuning t = d.T;
            WriteLine("FG DATA tuning relaxWhileReeling=" + F2(t.RelaxWhileReeling) + " reelStrainBurst=" + F2(t.ReelStrainBurst)
                + " counterRelief=" + F2(t.CounterRelief) + " snapGrace=" + F2(t.SnapGrace) + " cleanMax=" + F2(t.CleanCatchMaxTension)
                + " maxFight=" + F1(t.MaxFightTime));
            foreach (string rid in RodIds)
            {
                RodStats r = d.Rods[rid];
                WriteLine("FG DATA rod " + rid + " cast=" + F1(r.CastMaxDist) + " reel=" + F2(r.ReelSpeed) + " strength=" + F2(r.LineStrength)
                    + " window=" + F2(r.AngleWindow) + " steer=" + F2(r.SteerRate) + " relax=" + F2(r.RelaxMult));
            }
            foreach (string pid in ProfileIds)
            {
                FishFightProfile p = d.Profiles[pid];
                WriteLine("FG DATA profile " + pid + " pull=" + F2(p.PullStrength) + " lat=" + F2(p.LateralSpeed) + " run=" + F2(p.RunSpeed)
                    + " drain=" + F3(p.StaminaDrain) + " calm=" + F2(p.CalmMin) + "-" + F2(p.CalmMax) + " burst=" + F2(p.BurstMin) + "-" + F2(p.BurstMax)
                    + " flip=" + F2(p.FlipRate) + " dive=" + F2(p.DiveChance) + " ramp=" + F2(p.BurstRamp) + " drift=" + F2(p.CalmDrift) + "/" + F2(p.CalmDriftRamp)
                    + " gap=" + F2(p.FlipMinGap) + " jitter=" + F2(p.LateralJitter));
            }

            bool finite = true;
            foreach (string pid in ProfileIds)
                foreach (string rid in RodIds)
                    foreach (float w in Weights)
                        foreach (BotKind bot in Bots)
                        {
                            BatchStats s = Batch(d, bot, pid, rid, w);
                            WriteLine(BotLine(bot, pid, rid, w, s));
                            finite &= s.AllFinite && IsFinite(s.LandPct) && IsFinite(s.SnapPct) && IsFinite(s.EscPct) && IsFinite(s.TimeoutPct)
                                && IsFinite(s.RecMean) && IsFinite(s.MaxTMed) && IsFinite(s.RecAtLeast1Pct)
                                && IsFiniteOrNa(s.LenP10) && IsFiniteOrNa(s.LenMed) && IsFiniteOrNa(s.LenP90) && IsFiniteOrNa(s.SnapTMed)
                                && IsFinite(s.CleanOfLandedPct) && IsFinite(s.CleanOfAllPct);
                        }

            foreach (string pid in ProfileIds)
            {
                SigAccum g = s_sig[pid];
                double flipsPerSec = g.SideRunSec > 0 ? g.Flips / g.SideRunSec : 0.0;
                double minGap = double.IsInfinity(g.MinFlipGap) ? double.NaN : g.MinFlipGap;
                double creep = g.CreepTicks > 0 ? g.CreepSum / (g.CreepTicks * (double)FightSim.Step) : double.NaN;
                double fullForce = Mean(g.FullForceTimes);
                double jMin = double.NaN, jMax = double.NaN, jSd = double.NaN;
                if (g.BurstMedians.Count > 0)
                {
                    jMin = double.PositiveInfinity; jMax = double.NegativeInfinity;
                    double mean = Mean(g.BurstMedians);
                    double acc = 0;
                    foreach (double m in g.BurstMedians)
                    {
                        if (m < jMin) jMin = m;
                        if (m > jMax) jMax = m;
                        acc += (m - mean) * (m - mean);
                    }
                    jSd = Math.Sqrt(acc / g.BurstMedians.Count);
                }
                double diveShare = g.Bursts > 0 ? 100.0 * g.Dives / g.Bursts : 0.0;
                WriteLine("FG SIG " + pid + " sideRunSec=" + F1(g.SideRunSec) + " flips/s=" + F2(flipsPerSec) + " minFlipGap=" + F2(minGap)
                    + " creep=" + F3(creep) + "m/s fullForce=" + F2(fullForce) + "s jitter min/max/sd=" + F2(jMin) + "/" + F2(jMax) + "/" + F2(jSd)
                    + " dives=" + P(diveShare) + " bursts=" + g.Bursts);
                finite &= IsFinite(g.SideRunSec) && IsFinite(flipsPerSec) && IsFiniteOrNa(minGap) && IsFiniteOrNa(creep) && IsFiniteOrNa(fullForce)
                    && IsFiniteOrNa(jMin) && IsFiniteOrNa(jMax) && IsFiniteOrNa(jSd) && IsFinite(diveShare);
            }

            // Informational fun-gate proxies (spec 17.2): Rhythm, handmade, w 0.5, near-snap recovery share on the three hard profiles.
            foreach (string pid in new[] { "bulldog", "thrasher", "runner" })
            {
                BatchStats s = Batch(d, BotKind.Rhythm, pid, "rod_handmade", 0.5f);
                WriteLine("FG FUN " + pid + " Rhythm rec>=1=" + P(s.RecAtLeast1Pct) + " (fun-gate line >= 40%) clean=" + P(s.CleanOfLandedPct) + "/" + P(s.CleanOfAllPct));
            }
            foreach (BotKind bot in Bots)
            {
                double snapped = 0, n = 0;
                foreach (string pid in ProfileIds)
                {
                    BatchStats s = Batch(d, bot, pid, "rod_handmade", 0.5f);
                    snapped += s.Snapped; n += s.N;
                }
                WriteLine("FG FUN snap overall " + bot + " handmade w=0.5 over 5 profiles=" + P(100.0 * snapped / n) + " (fun-gate 10-30%)");
            }
            WriteLine("FG TIME FishingBots matrix=" + F1(s_matrixSeconds) + "s");
            Assert.IsTrue(finite, "a recorded bot number is NaN or infinite");
        }

        static bool IsFinite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        static bool IsFiniteOrNa(double v) { return double.IsNaN(v) || !double.IsInfinity(v); }

        static double Mean(List<double> xs)
        {
            if (xs.Count == 0) return double.NaN;
            double s = 0;
            foreach (double x in xs) s += x;
            return s / xs.Count;
        }

        // ------------------------------------------------------------------ perf

        sealed class PerfDriver
        {
            readonly FishFightProfile _p; readonly RodStats _rod; readonly FishingTuning _t; readonly float _w;
            FightSim _sim; bool _easing; int _next;
            public PerfDriver(FishFightProfile p, RodStats rod, FishingTuning t, float w) { _p = p; _rod = rod; _t = t; _w = w; }

            public void Step()
            {
                if (_sim == null || _sim.State.Outcome != FightOutcome.None || _sim.State.Elapsed >= _t.MaxFightTime)
                {
                    _sim = new FightSim(_p, _rod, _t, _w, SeedFor(_next++ % FightsPerBatch), _rod.CastMaxDist, false, false);
                    _easing = false;
                }
                FightState s = _sim.State;
                FightInput input = Decide(BotKind.Rhythm, s, ref _easing);
                _sim.Tick(input);
            }
        }

        // Catches: FightSim.Tick becoming expensive (acceptance 11 target < 0.05 ms; this test fails only above 0.2 ms, constructor time included).
        [Test]
        public void FishingBots_FightSimStep_UnderFiftyMicroseconds()
        {
            Loaded d = LoadData();
            var drv = new PerfDriver(d.Profiles["runner"], d.Rods["rod_handmade"], d.T, 0.9f);
            for (int i = 0; i < 2000; i++) drv.Step();
            const int steps = 200000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < steps; i++) drv.Step();
            sw.Stop();
            double us = sw.Elapsed.TotalSeconds * 1e6 / steps;
            WriteLine("FG PERF FightSim.Tick " + F2(us) + " us/step over " + steps + " steps");
            Assert.Less(us, 200.0, "FightSim.Tick average step time (us)");
        }
    }
}
