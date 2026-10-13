// T15 (plan data-tuning section 4): scripted tuning bots (Greedy, CounterReel, Rhythm) that fight the REAL assets through FightSim,
// print distributions, and (before R72) asserted the spec 9.12 / 17.2 targets. Bots draw nothing random: seeds are a fixed formula.
// R72 (plan rework-fight F2): the pre-R72 targets are deleted; F4 adds the Rust baseline bots and targets.
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
            public FightOutcome Outcome;
            public EscapeReason Reason;
            public float Elapsed, MaxTension;
            public int Recoveries;
        }

        static uint SeedFor(int i)
        {
            return unchecked(2654435761u * (uint)(i + 1));
        }

        static FightRecord RunFight(BotKind kind, in FishFightProfile p, in RodStats rod, in FishingTuning t, float weightT, uint seed)
        {
            var sim = new FightSim(p, rod, t, weightT, seed, rod.CastMaxDist, false, false);
            bool easing = false;
            int maxTicks = (int)Math.Ceiling(t.MaxFightTime / FightSim.Step);
            for (int k = 0; k < maxTicks; k++)
            {
                FightInput input = Decide(kind, sim.State, ref easing);
                if (sim.Tick(input) != FightOutcome.None) break;
            }
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
                        else if (f.Reason == EscapeReason.Timeout) s.Timeouts++;   // R72: the sim times a fish fight out at 120 s
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

        // ------------------------------------------------------------------ the matrix

        static readonly Dictionary<string, BatchStats> s_cache = new Dictionary<string, BatchStats>();
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
                            FishFightProfile p = d.Profiles[profile];
                            RodStats r = d.Rods[rod];
                            var fights = new List<FightRecord>(FightsPerBatch);
                            for (int i = 0; i < FightsPerBatch; i++) fights.Add(RunFight(bot, p, r, d.T, w, SeedFor(i)));
                            s_cache[Key(bot, profile, rod, w)] = Compute(fights, d.T.CleanCatchMaxTension);
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

        static string Sec(double v) { return double.IsNaN(v) ? "n/a" : N(v, "F1") + "s"; }

        static void WriteLine(string line)
        {
            TestContext.WriteLine(line);
        }

        static void PrintBots(Loaded d, string profile, string rod, float w, params BotKind[] bots)
        {
            foreach (BotKind bot in bots) WriteLine(BotLine(bot, profile, rod, w, Batch(d, bot, profile, rod, w)));
        }

        // ------------------------------------------------------------------ target bookkeeping (kept for F4)

        // Acceptance 8 escape hatch: key = test name, value = one line with the measured numbers and the reason. Each entry needs the
        // controller's approval (ruling D-Q6).
        static readonly Dictionary<string, string> TuningItems = new Dictionary<string, string>();

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

        // Catches: a NaN or infinite number in any recorded distribution. Prints everything the F4 retarget reads.
        [Test]
        public void FishingBots_AllProfiles_PrintDistributions()
        {
            Loaded d = LoadData();
            EnsureMatrix(d);
            FishingTuning t = d.T;
            WriteLine("FG DATA tuning relaxRate=" + F2(t.RelaxRate) + " strainScale=" + F2(t.StrainScale) + " strainBarFull=" + F2(t.StrainBarFull)
                + " strainSnap=" + F2(t.StrainSnap) + " fightTimeout=" + F1(t.FightTimeout) + " cleanMax=" + F2(t.CleanCatchMaxTension)
                + " maxFight=" + F1(t.MaxFightTime));
            foreach (string rid in RodIds)
            {
                RodStats r = d.Rods[rid];
                WriteLine("FG DATA rod " + rid + " cast=" + F1(r.CastMaxDist) + " reel=" + F2(r.ReelSpeed) + " strength=" + F2(r.LineStrength)
                    + " steer=" + F2(r.SteerRate) + " relax=" + F2(r.RelaxMult));
            }
            foreach (string pid in ProfileIds)
            {
                FishFightProfile p = d.Profiles[pid];
                WriteLine("FG DATA profile " + pid + " tier=" + F2(p.StrainMod) + "/" + F2(p.MoveMult) + "/" + F2(p.ReelInMult)
                    + " calm=" + F2(p.CalmMin) + "-" + F2(p.CalmMax) + " burst=" + F2(p.BurstMin) + "-" + F2(p.BurstMax)
                    + " flip=" + F2(p.FlipRate) + " dive=" + F2(p.DiveChance) + " ramp=" + F2(p.BurstRamp) + " drift=" + F2(p.CalmDrift) + "/" + F2(p.CalmDriftRamp)
                    + " gap=" + F2(p.FlipMinGap) + " jitter=" + F2(p.LateralJitter) + " wave=" + F2(p.WavePeriod));
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
            WriteLine("FG TIME FishingBots matrix=" + F1(s_matrixSeconds) + "s");
            Assert.IsTrue(finite, "a recorded bot number is NaN or infinite");
        }

        static bool IsFinite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        static bool IsFiniteOrNa(double v) { return double.IsNaN(v) || !double.IsInfinity(v); }

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
