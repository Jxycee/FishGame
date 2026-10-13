// R72 tuning bots (plan rework-fight F4; spec 9.12): Greedy, Reckless, Pump and Rhythm fight the REAL assets through FightSim at the casts a
// player makes (10-14 m from the rod tip, decision 4) with game-like weights, print distributions and signature stats, and assert the baseline
// targets. Bots draw nothing random: seeds are a fixed formula. A missed target is reported with its numbers; it is never retuned away from
// Rust's values (add a TuningItems entry instead and the controller decides).
// BotKind, FightRecord, SeedFor, CastFor and RunFight are internal so M3 tests in this assembly can run the Rhythm bot (contract M3-D60, CR-1).
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
        // Acceptance 8 escape hatch: key = test name, value = "<measured numbers>; R72 baseline, controller to decide". An entry turns a missed
        // target into an ignored test with its numbers printed. Needs the controller's approval.
        static readonly Dictionary<string, string> TuningItems = new Dictionary<string, string>();

        // R72 bots. Casts are drawn like real play (decision 4): Uniform(10, 14) m from the tip, weightT = u^2.2.
        // Weight cannot change a baseline fight (FightSimTests 23); it is drawn so a later weight term is measured at real weights.
        internal enum BotKind { Greedy, Reckless, Pump, Rhythm }

        const float CastMin = 10f, CastMax = 14f, WeightSkew = 2.2f;
        const float EaseOn = 0.85f, EaseOff = 0.30f, RestSteerDeadband = 0.05f;
        const int FightsPerBatch = 200;
        static readonly BotKind[] Bots = { BotKind.Greedy, BotKind.Reckless, BotKind.Pump, BotKind.Rhythm };
        static readonly string[] ProfileIds = { "steady", "darter", "bulldog", "thrasher", "runner" };
        static readonly string[] RodIds = { "rod_handmade", "rod_twig" };

        internal static uint SeedFor(int i) { return unchecked(2654435761u * (uint)(i + 1)); }

        // Per-fight cast distance and weight from their own Pcg32 stream (stream 7), independent of the fight seed.
        internal static void CastFor(int i, out float line, out float weightT)
        {
            var r = new Pcg32(SeedFor(i), 7);
            line = CastMin + (CastMax - CastMin) * r.NextFloat();
            weightT = (float)Math.Pow(r.NextFloat(), WeightSkew);
        }

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

        static void Ease(float tension, ref bool easing)
        {
            if (tension >= EaseOn) easing = true;
            else if (easing && tension <= EaseOff) easing = false;
        }

        // What a player can see: the run and its side (splash), a back pull (plunge), tension (creak / gauge), the fish's side of the line.
        static FightInput Decide(BotKind kind, in FightState s, ref bool easing)
        {
            bool run = s.Phase == FightPhase.Burst;
            switch (kind)
            {
                case BotKind.Greedy:                                   // reels non-stop, never steers
                    return new FightInput(0f, true);
                case BotKind.Reckless:                                 // reels non-stop and counter-steers every run (decision 3: careless still snaps)
                    return new FightInput(run ? -s.BurstDir : 0f, true);
                case BotKind.Pump:                                     // never steers; reels and lets go on the creak
                    Ease(s.Tension, ref easing);
                    return new FightInput(0f, !easing);
                default:                                               // Rhythm: the Rust pump
                    Ease(s.Tension, ref easing);
                    if (easing) return new FightInput(0f, false);
                    if (run) return new FightInput(s.BurstDir * s.Lateral > 0f ? -s.BurstDir : 0f, false);
                    float toLine = s.Lateral > RestSteerDeadband ? -1f : s.Lateral < -RestSteerDeadband ? 1f : 0f;
                    return new FightInput(toLine, true);
            }
        }

        // ------------------------------------------------------------------ one fight

        internal struct FightRecord
        {
            public FightOutcome Outcome;
            public EscapeReason Reason;
            public float Elapsed, MaxTension;
            public int Recoveries;
        }

        internal static FightRecord RunFight(BotKind kind, in FishFightProfile p, in RodStats rod, in FishingTuning t, int i)
        {
            return RunFight(kind, in p, in rod, in t, i, null);
        }

        static FightRecord RunFight(BotKind kind, in FishFightProfile p, in RodStats rod, in FishingTuning t, int i, SigAccum sig)
        {
            CastFor(i, out float line, out float weightT);
            var sim = new FightSim(p, rod, t, weightT, SeedFor(i), line, false, false);
            bool easing = false;
            int maxTicks = (int)Math.Ceiling(t.MaxFightTime / FightSim.Step);   // the sim ends at FightTimeout first
            for (int k = 0; k < maxTicks; k++)
            {
                FightState before = sim.State;
                FightInput input = Decide(kind, before, ref easing);
                FightOutcome o = sim.Tick(input);
                if (sig != null) sig.Collect(before, sim.State);
                if (o != FightOutcome.None) break;
            }
            sig?.EndFight();
            FightState f = sim.State;
            return new FightRecord { Outcome = f.Outcome, Reason = f.Reason, Elapsed = f.Elapsed, MaxTension = f.MaxTension, Recoveries = f.NearSnapRecoveries };
        }

        // ------------------------------------------------------------------ batch stats

        sealed class BatchStats
        {
            public int N, Landed, Snapped, Escaped, Timeouts;
            public double LandPct, SnapPct, EscPct, TimeoutPct;
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
                        if (f.Reason == EscapeReason.Timeout) s.Timeouts++;
                        break;
                    default:
                        s.Timeouts++;   // the bot loop ended with no outcome (the sim times out first, so this is a safety net)
                        break;
                }
            }
            landed.Sort(); snapped.Sort(); maxT.Sort();
            s.LandPct = Pct(s.Landed, s.N); s.SnapPct = Pct(s.Snapped, s.N); s.EscPct = Pct(s.Escaped, s.N); s.TimeoutPct = Pct(s.Timeouts, s.N);
            s.LenP10 = Nearest(landed, 0.1); s.LenMed = Nearest(landed, 0.5); s.LenP90 = Nearest(landed, 0.9);
            s.SnapTMed = Nearest(snapped, 0.5);
            s.RecMean = s.N == 0 ? 0.0 : recSum / s.N;
            s.RecAtLeast1Pct = Pct(rec1, s.N);
            s.CleanOfLandedPct = Pct(clean, s.Landed); s.CleanOfAllPct = Pct(clean, s.N);
            s.MaxTMed = Nearest(maxT, 0.5);
            return s;
        }

        // ------------------------------------------------------------------ signature stats (Rhythm, Handmade)

        // Compares the state before and after each tick. A rest-to-run transition counts a run (and a back run when Back is set); runs of
        // zero length (a spent fish: after the tick the run reads Burst with a negative PhaseTimer) are not counted. Run ticks are run seconds.
        // A BurstDir change while the fish runs before and after, with |Lateral| < 0.999 after the tick, is a mid-run switch (cone flips excluded).
        sealed class SigAccum
        {
            const float Dt = FightSim.Step;
            public int Runs, BackRuns, Switches;
            public double RunSeconds;
            bool _live;   // the current run is counted

            public void Collect(in FightState b, in FightState a)
            {
                if (b.Phase == FightPhase.Calm && a.Phase == FightPhase.Burst)
                {
                    _live = a.PhaseTimer + Dt > 1e-3f;
                    if (_live) { Runs++; if (a.Back) BackRuns++; }
                }
                if (a.Phase == FightPhase.Burst && _live)
                {
                    RunSeconds += Dt;
                    if (b.Phase == FightPhase.Burst && b.BurstDir == -a.BurstDir && Math.Abs(a.Lateral) < 0.999f) Switches++;
                }
                if (a.Phase != FightPhase.Burst) _live = false;
            }

            public void EndFight() { _live = false; }

            public double BackShare { get { return Runs > 0 ? 100.0 * BackRuns / Runs : 0.0; } }
            public double MeanRun { get { return Runs > 0 ? RunSeconds / Runs : double.NaN; } }
            public double SwitchRate { get { return RunSeconds > 0 ? Switches / RunSeconds : 0.0; } }
        }

        // ------------------------------------------------------------------ the matrix

        static readonly Dictionary<string, BatchStats> s_cache = new Dictionary<string, BatchStats>();
        static readonly Dictionary<string, SigAccum> s_sig = new Dictionary<string, SigAccum>();
        static double s_matrixSeconds = double.NaN;
        static bool s_matrixDone;

        static string Key(BotKind bot, string profile, string rod)
        {
            return bot + "|" + profile + "|" + rod;
        }

        static void EnsureMatrix(Loaded d)
        {
            if (s_matrixDone) return;
            var sw = Stopwatch.StartNew();
            foreach (string profile in ProfileIds)
                foreach (string rod in RodIds)
                    foreach (BotKind bot in Bots)
                    {
                        bool wantSig = bot == BotKind.Rhythm && rod == "rod_handmade";
                        SigAccum sig = wantSig ? new SigAccum() : null;
                        FishFightProfile p = d.Profiles[profile];
                        RodStats r = d.Rods[rod];
                        var fights = new List<FightRecord>(FightsPerBatch);
                        for (int i = 0; i < FightsPerBatch; i++) fights.Add(RunFight(bot, p, r, d.T, i, sig));
                        s_cache[Key(bot, profile, rod)] = Compute(fights, d.T.CleanCatchMaxTension);
                        if (wantSig) s_sig[profile] = sig;
                    }
            sw.Stop();
            s_matrixSeconds = sw.Elapsed.TotalSeconds;
            s_matrixDone = true;
        }

        static BatchStats Batch(Loaded d, BotKind bot, string profile, string rod)
        {
            EnsureMatrix(d);
            return s_cache[Key(bot, profile, rod)];
        }

        // ------------------------------------------------------------------ formatting

        static string N(double v, string fmt)
        {
            return double.IsNaN(v) ? "n/a" : v.ToString(fmt, CultureInfo.InvariantCulture);
        }
        static string F1(double v) { return N(v, "F1"); }
        static string F2(double v) { return N(v, "F2"); }
        static string P(double v) { return N(v, "F1") + "%"; }
        static string Sec(double v) { return double.IsNaN(v) ? "n/a" : N(v, "F1") + "s"; }

        static string BotLine(BotKind bot, string profile, string rod, BatchStats s)
        {
            return "FG BOT " + bot + " " + profile + " " + rod + " n=" + s.N
                + " land=" + P(s.LandPct) + " snap=" + P(s.SnapPct) + " to=" + P(s.TimeoutPct)
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

        static void PrintBots(Loaded d, string profile, string rod, params BotKind[] bots)
        {
            foreach (BotKind bot in bots) WriteLine(BotLine(bot, profile, rod, Batch(d, bot, profile, rod)));
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

        static void Check(Targets t, string what, FightInput got, float steer, bool reel)
        {
            t.Add(got.Steer == steer && got.Reel == reel, what + " -> (" + got.Steer + ", " + got.Reel + ")", "(" + steer + ", " + reel + ")");
        }

        // Catches: a bot rule drifting silently (retuning is only meaningful while the bots mean what the plan says): Rhythm reeling into a run,
        // steering when the steer would be dropped or in the wrong direction, easing at the wrong thresholds; Greedy or Reckless gaining brains.
        [Test]
        public void FishingBots_Decide_PinsTheFourBots()
        {
            var t = new Targets(nameof(FishingBots_Decide_PinsTheFourBots));
            bool easing = false;
            foreach (FightState s in new[] { Fs(FightPhase.Calm, 0, 0f, 0f), Fs(FightPhase.Burst, 1, 0.5f, 0.95f), Fs(FightPhase.Burst, -1, -0.5f, 0.2f) })
            {
                easing = false;
                Check(t, "Greedy " + s.Phase + " dir " + s.BurstDir, Decide(BotKind.Greedy, s, ref easing), 0f, true);
            }

            easing = false;
            Check(t, "Reckless run +1", Decide(BotKind.Reckless, Fs(FightPhase.Burst, 1, 0f, 0f), ref easing), -1f, true);
            Check(t, "Reckless run -1", Decide(BotKind.Reckless, Fs(FightPhase.Burst, -1, 0f, 0f), ref easing), 1f, true);
            Check(t, "Reckless rest", Decide(BotKind.Reckless, Fs(FightPhase.Calm, 0, 0f, 0f), ref easing), 0f, true);

            easing = false;
            Check(t, "Pump 0.85", Decide(BotKind.Pump, Fs(FightPhase.Burst, 1, 0.3f, 0.85f), ref easing), 0f, false);
            t.Add(easing, "Pump easing flag at 0.85", "true");
            Check(t, "Pump 0.6 still easing", Decide(BotKind.Pump, Fs(FightPhase.Calm, 0, 0f, 0.6f), ref easing), 0f, false);
            Check(t, "Pump 0.29 reels again", Decide(BotKind.Pump, Fs(FightPhase.Calm, 0, 0f, 0.29f), ref easing), 0f, true);
            t.Add(!easing, "Pump easing flag at 0.29", "false");

            easing = false;
            Check(t, "Rhythm run +1 at lateral +0.2", Decide(BotKind.Rhythm, Fs(FightPhase.Burst, 1, 0.2f, 0f), ref easing), -1f, false);
            Check(t, "Rhythm run +1 at lateral -0.2 (steer would be dropped)", Decide(BotKind.Rhythm, Fs(FightPhase.Burst, 1, -0.2f, 0f), ref easing), 0f, false);
            Check(t, "Rhythm run -1 at lateral -0.2", Decide(BotKind.Rhythm, Fs(FightPhase.Burst, -1, -0.2f, 0f), ref easing), 1f, false);
            Check(t, "Rhythm rest at lateral 0.3", Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0.3f, 0f), ref easing), -1f, true);
            Check(t, "Rhythm rest at lateral -0.3", Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, -0.3f, 0f), ref easing), 1f, true);
            Check(t, "Rhythm rest at lateral 0.02", Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0.02f, 0f), ref easing), 0f, true);
            Check(t, "Rhythm easing at 0.85", Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0.3f, 0.85f), ref easing), 0f, false);
            Check(t, "Rhythm still easing at 0.5", Decide(BotKind.Rhythm, Fs(FightPhase.Burst, 1, 0.3f, 0.5f), ref easing), 0f, false);
            Check(t, "Rhythm resumes at 0.29", Decide(BotKind.Rhythm, Fs(FightPhase.Calm, 0, 0.3f, 0.29f), ref easing), -1f, true);
            t.Complete();
        }

        // Catches (decision 3): Greedy not snapping, or snapping a Steady far from "about 7 s" (the model gives 7.4 s; a single Rust run would be 5.3 s).
        [Test]
        public void FishingBots_Greedy_SnapsEveryCell()
        {
            Loaded d = LoadData();
            var t = new Targets(nameof(FishingBots_Greedy_SnapsEveryCell));
            foreach (string profile in ProfileIds)
                foreach (string rod in RodIds)
                {
                    PrintBots(d, profile, rod, BotKind.Greedy);
                    BatchStats s = Batch(d, BotKind.Greedy, profile, rod);
                    t.Add(s.SnapPct >= 95.0, "Greedy " + profile + " " + rod + " snap=" + P(s.SnapPct), "snap >= 95%");
                }
            BatchStats steady = Batch(d, BotKind.Greedy, "steady", "rod_handmade");
            t.Add(In(steady.SnapTMed, 5.5, 9.0), "Greedy steady Handmade median snap=" + Sec(steady.SnapTMed), "median snap in [5.5, 9.0] s (model 7.4)");
            BatchStats runner = Batch(d, BotKind.Greedy, "runner", "rod_handmade");
            t.Add(In(runner.SnapTMed, 2.8, 3.8), "Greedy runner Handmade median snap=" + Sec(runner.SnapTMed), "median snap in [2.8, 3.8] s (Rust 3.4)");
            t.Complete();
        }

        // Catches (decision 3): a careless player who counter-steers every run but never lets go not snapping. Steady is allowed fewer snaps.
        [Test]
        public void FishingBots_Reckless_StillSnaps()
        {
            Loaded d = LoadData();
            var t = new Targets(nameof(FishingBots_Reckless_StillSnaps));
            foreach (string profile in ProfileIds)
                foreach (string rod in RodIds)
                {
                    PrintBots(d, profile, rod, BotKind.Reckless);
                    BatchStats s = Batch(d, BotKind.Reckless, profile, rod);
                    if (profile == "steady") t.Add(s.SnapPct >= 50.0, "Reckless steady " + rod + " snap=" + P(s.SnapPct), "snap >= 50%");
                    else t.Add(s.SnapPct >= 95.0, "Reckless " + profile + " " + rod + " snap=" + P(s.SnapPct), "snap >= 95%");
                }
            t.Complete();
        }

        // Catches: the pump rule letting a non-steerer snap, or a big fish landing without steering (Q4: big fish need steering).
        [Test]
        public void FishingBots_Pump_NeverSnapsButBigFishTimeOut()
        {
            Loaded d = LoadData();
            var t = new Targets(nameof(FishingBots_Pump_NeverSnapsButBigFishTimeOut));
            foreach (string profile in ProfileIds)
                foreach (string rod in RodIds)
                {
                    PrintBots(d, profile, rod, BotKind.Pump);
                    BatchStats s = Batch(d, BotKind.Pump, profile, rod);
                    // Structural cap, not a band: the pump lets go at 0.85, below the full bar (1.0) and the snap (7/6), so it can only snap if the rules break.
                    t.Add(s.SnapPct <= 5.0, "Pump " + profile + " " + rod + " snap=" + P(s.SnapPct), "snap <= 5%");
                }
            t.Add(Batch(d, BotKind.Pump, "steady", "rod_handmade").LandPct >= 85.0,
                "Pump steady Handmade land=" + P(Batch(d, BotKind.Pump, "steady", "rod_handmade").LandPct), "land >= 85%");
            t.Add(Batch(d, BotKind.Pump, "bulldog", "rod_handmade").TimeoutPct >= 60.0,
                "Pump bulldog Handmade timeout=" + P(Batch(d, BotKind.Pump, "bulldog", "rod_handmade").TimeoutPct), "timeout >= 60% (model 92%)");
            t.Add(Batch(d, BotKind.Pump, "runner", "rod_handmade").TimeoutPct >= 30.0,
                "Pump runner Handmade timeout=" + P(Batch(d, BotKind.Pump, "runner", "rod_handmade").TimeoutPct), "timeout >= 30% (model 52%)");
            t.Complete();
        }

        static readonly Dictionary<string, double[]> RhythmBands = new Dictionary<string, double[]>
        {
            { "steady|rod_handmade", new[] { 10.0, 20.0 } }, { "darter|rod_handmade", new[] { 13.0, 27.0 } },
            { "thrasher|rod_handmade", new[] { 13.0, 27.0 } }, { "runner|rod_handmade", new[] { 20.0, 41.0 } },
            { "bulldog|rod_handmade", new[] { 23.0, 44.0 } },
            { "steady|rod_twig", new[] { 10.0, 20.0 } }, { "darter|rod_twig", new[] { 15.0, 30.0 } },
            { "thrasher|rod_twig", new[] { 16.0, 31.0 } }, { "runner|rod_twig", new[] { 22.0, 44.0 } },
            { "bulldog|rod_twig", new[] { 23.0, 45.0 } },
        };

        // Catches: the baseline being unfishable for a player who pumps correctly, or fight lengths drifting out of the band around the model.
        [Test]
        public void FishingBots_Rhythm_LandsSafelyInBaselineTimes()
        {
            Loaded d = LoadData();
            var t = new Targets(nameof(FishingBots_Rhythm_LandsSafelyInBaselineTimes));
            foreach (string profile in ProfileIds)
                foreach (string rod in RodIds)
                {
                    PrintBots(d, profile, rod, BotKind.Rhythm);
                    BatchStats s = Batch(d, BotKind.Rhythm, profile, rod);
                    string cell = "Rhythm " + profile + " " + rod;
                    t.Add(s.LandPct >= 95.0, cell + " land=" + P(s.LandPct), "land >= 95%");
                    // Structural cap, not a band: Rhythm lets go at 0.85, below the full bar, so it can only snap if the rules break.
                    t.Add(s.SnapPct <= 2.0, cell + " snap=" + P(s.SnapPct), "snap <= 2%");
                    t.Add(s.TimeoutPct <= 2.0, cell + " timeout=" + P(s.TimeoutPct), "timeout <= 2%");
                    double[] band = RhythmBands[profile + "|" + rod];
                    t.Add(In(s.LenMed, band[0], band[1]), cell + " median=" + Sec(s.LenMed), "median in [" + F1(band[0]) + ", " + F1(band[1]) + "] s");
                }
            t.Complete();
        }

        // Catches (user Q1: recognisable blind): two styles collapsing into one in the numbers a player feels: back-pull share, run length,
        // mid-run side switches. Rhythm, Handmade.
        [Test]
        public void FishingBots_FiveStyles_AreDistinct()
        {
            Loaded d = LoadData();
            EnsureMatrix(d);
            var t = new Targets(nameof(FishingBots_FiveStyles_AreDistinct));
            SigAccum steady = s_sig["steady"], darter = s_sig["darter"], thrasher = s_sig["thrasher"], bulldog = s_sig["bulldog"], runner = s_sig["runner"];
            foreach (string pid in ProfileIds)
            {
                SigAccum g = s_sig[pid];
                WriteLine("FG SIG " + pid + " runs=" + g.Runs + " back=" + P(g.BackShare) + " meanRun=" + F2(g.MeanRun) + "s switches/run-s=" + F2(g.SwitchRate));
            }

            t.Add(bulldog.BackShare >= 40.0, "bulldog back share=" + P(bulldog.BackShare), ">= 40%");
            foreach (string pid in new[] { "steady", "darter", "thrasher", "runner" })
                t.Add(bulldog.BackShare > s_sig[pid].BackShare, "bulldog " + P(bulldog.BackShare) + " vs " + pid + " " + P(s_sig[pid].BackShare), "bulldog is the highest");
            t.Add(In(runner.BackShare, 15.0, 40.0), "runner back share=" + P(runner.BackShare), "in [15%, 40%]");
            t.Add(steady.BackShare <= 25.0, "steady back share=" + P(steady.BackShare), "<= 25%");
            t.Add(darter.BackShare <= 25.0, "darter back share=" + P(darter.BackShare), "<= 25%");
            t.Add(thrasher.BackShare <= 25.0, "thrasher back share=" + P(thrasher.BackShare), "<= 25%");

            t.Add(runner.MeanRun >= 3.8, "runner mean run=" + Sec(runner.MeanRun), ">= 3.8 s");
            t.Add(In(steady.MeanRun, 3.0, 4.4), "steady mean run=" + Sec(steady.MeanRun), "in [3.0, 4.4] s");
            t.Add(In(thrasher.MeanRun, 2.6, 3.8), "thrasher mean run=" + Sec(thrasher.MeanRun), "in [2.6, 3.8] s");
            t.Add(darter.MeanRun <= 2.7, "darter mean run=" + Sec(darter.MeanRun), "<= 2.7 s");
            t.Add(bulldog.MeanRun <= 3.1, "bulldog mean run=" + Sec(bulldog.MeanRun), "<= 3.1 s");
            t.Add(runner.MeanRun > steady.MeanRun && steady.MeanRun > thrasher.MeanRun && thrasher.MeanRun > darter.MeanRun,
                "runner " + F2(runner.MeanRun) + " steady " + F2(steady.MeanRun) + " thrasher " + F2(thrasher.MeanRun) + " darter " + F2(darter.MeanRun),
                "runner > steady > thrasher > darter");

            // Ordering margins (review finding 10): the pairs that are close in the table must stay apart by a visible amount, not just by a band edge.
            t.Add(darter.SwitchRate - thrasher.SwitchRate >= 0.2, "darter - thrasher switches/run-s=" + F2(darter.SwitchRate - thrasher.SwitchRate), ">= 0.2");
            t.Add(thrasher.MeanRun - darter.MeanRun >= 0.5, "thrasher - darter mean run=" + F2(thrasher.MeanRun - darter.MeanRun), ">= 0.5 s");
            t.Add(runner.MeanRun - steady.MeanRun >= 0.4, "runner - steady mean run=" + F2(runner.MeanRun - steady.MeanRun), ">= 0.4 s");
            t.Add(darter.SwitchRate >= 0.5, "darter switches/run-s=" + F2(darter.SwitchRate), ">= 0.5");
            t.Add(In(thrasher.SwitchRate, 0.15, 0.55), "thrasher switches/run-s=" + F2(thrasher.SwitchRate), "in [0.15, 0.55]");
            t.Add(steady.SwitchRate <= 0.05, "steady switches/run-s=" + F2(steady.SwitchRate), "<= 0.05");
            t.Add(bulldog.SwitchRate <= 0.05, "bulldog switches/run-s=" + F2(bulldog.SwitchRate), "<= 0.05");
            t.Add(runner.SwitchRate <= 0.05, "runner switches/run-s=" + F2(runner.SwitchRate), "<= 0.05");
            t.Complete();
        }

        // Catches: a NaN or infinite number in any recorded distribution. Prints everything F8 records.
        [Test]
        public void FishingBots_AllProfiles_PrintDistributions()
        {
            Loaded d = LoadData();
            EnsureMatrix(d);
            FishingTuning t = d.T;
            WriteLine("FG DATA tuning bar/snap=" + F2(t.StrainBarFull) + "/" + F2(t.StrainSnap) + " k rest/run/back/counter/other=" + F2(t.StrainRestInput) + "/"
                + F2(t.StrainReelRun) + "/" + F2(t.StrainReelBack) + "/" + F2(t.StrainCounterSteer) + "/" + F2(t.StrainOtherSteer)
                + " drain=" + F2(t.RelaxRate) + " scale=" + F2(t.StrainScale) + "/" + F2(t.StrainScaleAssist) + " timeout=" + F1(t.FightTimeout)
                + " land=" + F2(t.LandDistance) + " cleanMax=" + F2(t.CleanCatchMaxTension));
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
                    + " flip=" + F2(p.FlipRate) + "/" + F2(p.FlipMinGap) + " dive=" + F2(p.DiveChance) + " ramp=" + F2(p.BurstRamp)
                    + " drift=" + F2(p.CalmDrift) + "/" + F2(p.CalmDriftRamp) + " jitter=" + F2(p.LateralJitter) + " wave=" + F2(p.WavePeriod));
            }

            bool finite = true;
            foreach (string pid in ProfileIds)
                foreach (string rid in RodIds)
                    foreach (BotKind bot in Bots)
                    {
                        BatchStats s = Batch(d, bot, pid, rid);
                        WriteLine(BotLine(bot, pid, rid, s));
                        finite &= s.AllFinite && IsFinite(s.LandPct) && IsFinite(s.SnapPct) && IsFinite(s.EscPct) && IsFinite(s.TimeoutPct)
                            && IsFinite(s.RecMean) && IsFinite(s.MaxTMed) && IsFinite(s.RecAtLeast1Pct)
                            && IsFiniteOrNa(s.LenP10) && IsFiniteOrNa(s.LenMed) && IsFiniteOrNa(s.LenP90) && IsFiniteOrNa(s.SnapTMed)
                            && IsFinite(s.CleanOfLandedPct) && IsFinite(s.CleanOfAllPct);
                    }

            foreach (string pid in ProfileIds)
            {
                SigAccum g = s_sig[pid];
                WriteLine("FG SIG " + pid + " runs=" + g.Runs + " back=" + P(g.BackShare) + " meanRun=" + F2(g.MeanRun) + "s switches/run-s=" + F2(g.SwitchRate));
                finite &= IsFinite(g.BackShare) && IsFiniteOrNa(g.MeanRun) && IsFinite(g.SwitchRate) && IsFinite(g.RunSeconds);
            }

            // Informational fun-gate proxies (spec 17.2): Rhythm, Handmade, recoveries and clean catches on the hard profiles; overall snap per bot.
            foreach (string pid in ProfileIds)
            {
                BatchStats s = Batch(d, BotKind.Rhythm, pid, "rod_handmade");
                WriteLine("FG FUN " + pid + " Rhythm Handmade rec>=1=" + P(s.RecAtLeast1Pct) + " (fun-gate line >= 40% for bulldog/thrasher/runner) clean=" + P(s.CleanOfLandedPct) + "/" + P(s.CleanOfAllPct));
            }
            foreach (BotKind bot in Bots)
            {
                double snapped = 0, n = 0;
                foreach (string pid in ProfileIds)
                {
                    BatchStats s = Batch(d, bot, pid, "rod_handmade");
                    snapped += s.Snapped; n += s.N;
                }
                WriteLine("FG FUN snap overall " + bot + " Handmade over 5 profiles=" + P(100.0 * snapped / n));
            }
            WriteLine("FG TIME FishingBots matrix=" + F1(s_matrixSeconds) + "s");
            Assert.IsTrue(finite, "a recorded bot number is NaN or infinite");
        }

        static bool IsFinite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        static bool IsFiniteOrNa(double v) { return double.IsNaN(v) || !double.IsInfinity(v); }

        // ------------------------------------------------------------------ perf

        sealed class PerfDriver
        {
            readonly FishFightProfile _p; readonly RodStats _rod; readonly FishingTuning _t;
            FightSim _sim; bool _easing; int _next;
            public PerfDriver(FishFightProfile p, RodStats rod, FishingTuning t) { _p = p; _rod = rod; _t = t; }

            public void Step()
            {
                if (_sim == null || _sim.State.Outcome != FightOutcome.None || _sim.State.Elapsed >= _t.FightTimeout)
                {
                    _sim = new FightSim(_p, _rod, _t, 0.5f, SeedFor(_next++ % FightsPerBatch), 12f, false, false);
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
            var drv = new PerfDriver(d.Profiles["runner"], d.Rods["rod_handmade"], d.T);
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
